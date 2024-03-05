using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ServerChannel;


[EventSource(Name = "ServerChannel")]
public sealed class ServerChannelManifest : EventSource
{
    public static ServerChannelManifest Log = new();

    private ServerChannelManifest()
        : base(EventSourceSettings.EtwSelfDescribingEventFormat) // Needed for AOT
    { }

    [Event(1, Message = "{0}")]
    public void Info(string msg)
        => WriteEvent(1, msg);
}

public static class Program
{
    public static async Task Main(params string[] args)
    {
        string channelName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "ProcessChannel";
        if (args.Length > 0)
        {
            channelName = args[0];
        }

        ServerChannelManifest.Log.Info($"Opening {channelName} channel");
        using NamedPipeServerStream channel = CreateChannelStream(channelName);
        await RunWithStream(channel);

        ServerChannelManifest.Log.Info("Exiting");
    }

    private static async Task RunWithStream(NamedPipeServerStream channel)
    {
        // The server must write for the client to send data.
        await channel.WriteAsync(new byte[1] { 0 });

        CancellationTokenSource cancelSource = new();
        Pipe pipe = new();
        Task readerTask = Task.Run(() => PumpPipe(channel, pipe.Writer, cancelSource.Token));

        ServerChannelManifest.Log.Info("Reading input manifest data");
        PipeReader reader = pipe.Reader;
        ReadResult result = await reader.ReadAsync();
        ReadOnlySequence<byte> buffer = result.Buffer;

        ProcessManifest manifest = ReadManifest(buffer.IsSingleSegment ? buffer.FirstSpan : buffer.ToArray());
        reader.AdvanceTo(buffer.Start, buffer.End);
        ServerChannelManifest.Log.Info($"Process manifest: {manifest}");

        await RunProcess(channel, manifest);

        await reader.CompleteAsync();

        cancelSource.Cancel();
        try
        {
            ServerChannelManifest.Log.Info("Awaiting main reader to end");
            await readerTask;
        }
        catch (OperationCanceledException)
        { }
    }

    private static async Task RunProcess(NamedPipeServerStream channel, ProcessManifest manifest)
    {
        using CancellationTokenSource cancelSource = new();
        ServerChannelManifest.Log.Info($"Creating {manifest.ChannelName}-stdin");
        using NamedPipeServerStream stdinChannel = CreateChannelStream($"{manifest.ChannelName}-stdin");
        ServerChannelManifest.Log.Info($"Creating {manifest.ChannelName}-stdout");
        using NamedPipeServerStream stdoutChannel = CreateChannelStream($"{manifest.ChannelName}-stdout");
        ServerChannelManifest.Log.Info($"Creating {manifest.ChannelName}-stderr");
        using NamedPipeServerStream stderrChannel = CreateChannelStream($"{manifest.ChannelName}-stderr");

        using AnonymousPipeServerStream stdinPipe = new(PipeDirection.Out, HandleInheritability.Inheritable);
        using AnonymousPipeServerStream stdoutPipe = new(PipeDirection.In, HandleInheritability.Inheritable);
        using AnonymousPipeServerStream stderrPipe = new(PipeDirection.In, HandleInheritability.Inheritable);

        Task stdinTask = Task.Run(() => PumpProcessInput(stdinPipe, stdinChannel, cancelSource.Token));
        Task stdoutTask = Task.Run(() => PumpProcessOutput(stdoutPipe, stdoutChannel, cancelSource.Token));
        Task stderrTask = Task.Run(() => PumpProcessOutput(stderrPipe, stderrChannel, cancelSource.Token));

        StringBuilder commandLine = new($"\"{manifest.Executable}\"");
        if (!string.IsNullOrWhiteSpace(manifest.Arguments))
        {
            commandLine.AppendFormat(" {0}", manifest.Arguments);
        }
        ServerChannelManifest.Log.Info($"Starting process");
        using Win32Process process = Win32Process.Create(
            manifest.Executable,
            commandLine.ToString(),
            manifest.WorkingDirectory,
            manifest.Environment,
            stdinPipe.ClientSafePipeHandle,
            stdoutPipe.ClientSafePipeHandle,
            stderrPipe.ClientSafePipeHandle);

        stdinPipe.DisposeLocalCopyOfClientHandle();
        stdoutPipe.DisposeLocalCopyOfClientHandle();
        stderrPipe.DisposeLocalCopyOfClientHandle();

        ProcessInfo processInfo = new(process.ProcessId, process.ThreadId);
        ServerChannelManifest.Log.Info($"Writing new process info {processInfo}");
        // await JsonSerializer.SerializeAsync(
        //     channel,
        //     processInfo,
        //     SourceGenerationContext.Default.ProcessInfo);

        ServerChannelManifest.Log.Info("Waiting for process to end");
        int rc = await process.WaitForExitAsync();
        ServerChannelManifest.Log.Info($"Process ended with {rc}");
        cancelSource.Cancel();

        ServerChannelManifest.Log.Info("Awaiting stdin task");
        await stdinTask;
        ServerChannelManifest.Log.Info("Awaiting stdout task");
        await stdoutTask;
        ServerChannelManifest.Log.Info("Awaiting stderr task");
        await stderrTask;

        ProcessResult procRes = new(ReturnCode: rc);
        ServerChannelManifest.Log.Info($"Writing process result {procRes}");
        // await JsonSerializer.SerializeAsync(
        //     channel,
        //     procRes,
        //     SourceGenerationContext.Default.ProcessResult);
    }

    private static NamedPipeServerStream CreateChannelStream(string name)
    {
        using (SafeVirtualChannelHandle channel = VirtualChannel.OpenDynamicChannel(name))
        {
            return VirtualChannel.GetStream(channel);
        }
    }

    private static async Task PumpProcessOutput(
        AnonymousPipeServerStream pipe,
        NamedPipeServerStream channel,
        CancellationToken cancellationToken)
    {
        try
        {
            await pipe.CopyToAsync(channel, cancellationToken);
        }
        catch (OperationCanceledException)
        { }
    }

    private static async Task PumpProcessInput(
        AnonymousPipeServerStream pipe,
        NamedPipeServerStream channel,
        CancellationToken cancellationToken)
    {
        // The server must write for the client to send data.
        await channel.WriteAsync(new byte[1] { 0 });

        Pipe pipeline = new();
        Task pumpTask = Task.Run(() => PumpPipe(channel, pipeline.Writer, cancellationToken));
        try
        {
            await pipeline.Reader.CopyToAsync(pipe, cancellationToken);
        }
        catch (OperationCanceledException)
        { }

        await pipeline.Reader.CompleteAsync();

        try
        {
            await pumpTask;
        }
        catch (OperationCanceledException)
        { }
    }

    private static ProcessManifest ReadManifest(ReadOnlySpan<byte> value)
    {
        ProcessManifest? manifest = JsonSerializer.Deserialize(
            value,
            SourceGenerationContext.Default.ProcessManifest);
        if (manifest == null || manifest.ChannelName is null || manifest.Executable is null)
        {
            throw new InvalidOperationException($"Invalid JSON provided");
        }
        return manifest;
    }

    private static (int, int) GetPduHeader(ReadOnlySpan<byte> buffer)
    {
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                CHANNEL_PDU_HEADER* header = (CHANNEL_PDU_HEADER*)ptr;
                return (header->length, header->flags);
            }
        }
    }

    private static async Task PumpPipe(
        NamedPipeServerStream channel,
        PipeWriter writer,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            int totalRead = 0;
            int length;
            do
            {
                Memory<byte> buffer = writer.GetMemory(1600);
                await channel.ReadExactlyAsync(buffer[..8], cancellationToken);
                (length, int _) = GetPduHeader(buffer.Span);

                int read = await channel.ReadAsync(buffer, cancellationToken);
                writer.Advance(read);
                totalRead += read;
            }
            while (totalRead < length);

            FlushResult result = await writer.FlushAsync();
            if (result.IsCompleted)
            {
                break;
            }
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct CHANNEL_PDU_HEADER
{
    public const int CHANNEL_FLAG_FIRST = 1;
    public const int CHANNEL_FLAG_LAST = 2;

    public int length;
    public int flags;
}

[JsonSerializable(typeof(ProcessManifest))]
[JsonSerializable(typeof(ProcessInfo))]
[JsonSerializable(typeof(ProcessResult))]
internal partial class SourceGenerationContext : JsonSerializerContext
{ }

public record ProcessManifest(
    [property: JsonRequired]
    string ChannelName,
    [property: JsonRequired]
    string Executable,
    string? Arguments,
    string? WorkingDirectory,
    Dictionary<string, string>? Environment
);

public record ProcessInfo(
    [property: JsonRequired]
    int ProcessId,
    [property: JsonRequired]
    int ThreadId
);

public record ProcessResult(
    [property: JsonRequired]
    int ReturnCode
);
