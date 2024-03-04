using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
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

public static class Program
{
    public static async Task Main(params string[] args)
    {
        string channelName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "ProcessChannel";
        if (args.Length > 0)
        {
            channelName = args[0];
        }

        Console.WriteLine($"Opening {channelName} channel");
        NamedPipeServerStream fs;
        using (SafeVirtualChannelHandle channel = VirtualChannel.OpenDynamicChannel(channelName))
        {
            fs = VirtualChannel.GetStream(channel);
        }
        using (fs)
        {
            await RunWithStream(fs);
        }
    }

    private static async Task RunWithStream(NamedPipeServerStream fs)
    {
        /*
        + Read input for manifest to run
            + Channel name
            + Executable - command line
            + WorkingDir
            + Environment
            + ECDA public secret?
        + Create pipes for stdio
        + Start a new thread for each stdio
            + Start new channel with "$ChannelName-std..." for each stdio pipe
            + Process accordingly
        + Start process
        + Send back proc info (pid, tid)
        + Resume process
        + Wait for process exit
        + Send back return code
        + Loop back until process ends
        */
        // The server must write for the client to send data.
        Console.WriteLine("Writing");
        byte[] test = Encoding.UTF8.GetBytes("Testing 1");
        await fs.WriteAsync(test);

        CancellationTokenSource cancelSource = new();
        Pipe pipe = new();
        Task readerTask = Task.Run(() => PumpPipe(fs, pipe.Writer, cancelSource.Token));

        PipeReader reader = pipe.Reader;
        Console.WriteLine("Reader read");
        ReadResult result = await reader.ReadAsync();
        Console.WriteLine($"Reader read done - {result.Buffer.Length}");
        ReadOnlySequence<byte> buffer = result.Buffer;

        Console.WriteLine($"Read IsSingleSegment - {buffer.IsSingleSegment}");
        ProcessManifest manifest = ReadManifest(buffer.IsSingleSegment ? buffer.FirstSpan : buffer.ToArray());

        reader.AdvanceTo(buffer.Start, buffer.End);
        await reader.CompleteAsync();
        Console.WriteLine("Reader complete");

        cancelSource.Cancel();
        try
        {
            await readerTask;
        }
        catch (OperationCanceledException)
        { }

        Console.WriteLine(manifest);
        Console.WriteLine(manifest.WorkingDirectory.Length);

        // int payloadSize = await GetPayloadLength(fs);
        // byte[] rentedArray = ArrayPool<byte>.Shared.Rent(payloadSize);
        // try
        // {
        //     Memory<byte> buffer = rentedArray.AsMemory(0, payloadSize);
        //     await ReadIntoBuffer(fs, buffer);

        //     string inJson = Encoding.UTF8.GetString(buffer.Span);
        //     Console.WriteLine($"Input json: {inJson}");

        //     ProcessManifest? manifest = JsonSerializer.Deserialize(
        //         inJson,
        //         ProcessManifestSourceGenerationContext.Default.ProcessManifest);

        // }
        // finally
        // {
        //     ArrayPool<byte>.Shared.Return(rentedArray);
        // }

        // ProcessManifest manifest = await ReadManifest(fs);

        // Console.WriteLine("Writing");
        // fs.Write("Testing 2"u8);

        // Console.WriteLine("Reading");
        // read = fs.Read(outBuffer);
        // Console.WriteLine($"Read {read}: {Convert.ToHexString(outBuffer[..read])}");
    }

    private static async Task<int> GetPayloadLength(NamedPipeServerStream channel)
    {
        int channelSize = Marshal.SizeOf<CHANNEL_PDU_HEADER>();
        byte[] rentedArray = ArrayPool<byte>.Shared.Rent(channelSize);
        try
        {
            Memory<byte> buffer = rentedArray.AsMemory(0, channelSize);
            Console.WriteLine($"GetPayloadLength - ReadExactlyAsync({channelSize})");
            await channel.ReadExactlyAsync(buffer);
            Console.WriteLine($"GetPayloadLength - ReadExactlyAsync done - {Convert.ToHexString(buffer.Span)}");
            (int length, int _) = GetPduHeader(buffer.Span);
            return length;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedArray);
        }
    }

    private static async Task ReadIntoBuffer(NamedPipeServerStream channel, Memory<byte> buffer)
    {
        while (true)
        {
            // Each read operation will read up to the next CHANNEL_PDU_HEADER,
            // we just need to skip each of those headers as we know the value.
            Console.WriteLine($"ReadIntoBuffer - ReadAsync({buffer.Length})");
            int read = await channel.ReadAsync(buffer);
            Console.WriteLine($"ReadIntoBuffer - ReadAsync - {read} - {Convert.ToHexString(buffer[..read].Span)}");

            buffer = buffer[read..];
            if (buffer.Length <= 0)
            {
                break;
            }

            // Read the PDU header value so that the next read won't read that section.
            await GetPayloadLength(channel);
        }
    }

    private static ProcessManifest ReadManifest(ReadOnlySpan<byte> value)
    {
        ProcessManifest? manifest = JsonSerializer.Deserialize(
            value,
            ProcessManifestSourceGenerationContext.Default.ProcessManifest);
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
                Console.WriteLine($"CHANNEL_PDU_HEADER(length: {header->length}, flags: {header->flags})");
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
                Console.WriteLine("ReadExactlyAsync");
                await channel.ReadExactlyAsync(buffer[..8], cancellationToken);
                Console.WriteLine("ReadExactlyAsync - done");
                (length, int _) = GetPduHeader(buffer.Span);

                Console.WriteLine("ReadAsync()");
                int read = await channel.ReadAsync(buffer, cancellationToken);
                Console.WriteLine($"ReadAsync - {read}");
                writer.Advance(read);
                totalRead += read;
                Console.WriteLine($"TotalRead - {totalRead} - Length - {length}");
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
internal partial class ProcessManifestSourceGenerationContext : JsonSerializerContext
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
