using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        FileStream fs;
        using (SafeVirtualChannelHandle channel = VirtualChannel.OpenDynamicChannel(channelName))
        {
            fs = VirtualChannel.GetStream(channel);
        }
        using (fs)
        {
            await RunWithStream(fs);
        }
    }

    private static async Task RunWithStream(FileStream fs)
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
        // The max should be 1600 but it seems like 1602 is the max. I have no
        // idea where the extra two bytes are from.
        Memory<byte> buffer = new byte[1602];

        Console.WriteLine("Writing");
        byte[] test = Encoding.UTF8.GetBytes("Testing 1");
        await fs.WriteAsync(test);

        ProcessManifest manifest = await ReadManifest(fs);
        Console.WriteLine($"Read: {manifest}");

        // Console.WriteLine("Writing");
        // fs.Write("Testing 2"u8);

        // Console.WriteLine("Reading");
        // read = fs.Read(outBuffer);
        // Console.WriteLine($"Read {read}: {Convert.ToHexString(outBuffer[..read])}");
    }

    private static async Task<ProcessManifest> ReadManifest(FileStream fs)
    {
        // This should be 1600 but first payload max is 1602 and subsequent
        // ones are 1604. Will need to investigate further.
        Memory<byte> buffer = new byte[1604];
        StringBuilder sb = new();

        int flags;
        do
        {
            Console.WriteLine("Reading");
            int read = await fs.ReadAsync(buffer);
            Console.WriteLine($"Read {read}");  // : {Convert.ToHexString(buffer.Span[..read])}");
            (int length, flags) = GetPduHeader(buffer.Span);
            Console.WriteLine($"Length: {length} - Flags: {flags}");

            sb.Append(Encoding.UTF8.GetString(buffer.Span[8..(read - 8)]));
        }
        while ((flags & CHANNEL_PDU_HEADER.CHANNEL_FLAG_LAST) == 0);

        return JsonSerializer.Deserialize(
            sb.ToString(),
            SourceGenerationContext.Default.ProcessManifest);
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
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

public record ProcessManifest(
    string channelName,
    string executable,
    string commandLine,
    string? workingDirectory,
    Dictionary<string, string>? environment
);
