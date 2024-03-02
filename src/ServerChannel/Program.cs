using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ServerChannel;

public static class Program
{
    public static void Main(params string[] args)
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
            RunWithStream(fs);
        }
    }

    private static void RunWithStream(FileStream fs)
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
        Span<byte> buffer = stackalloc byte[1604];

        Console.WriteLine("Writing");
        fs.Write("Testing 1"u8);

        Console.WriteLine("Reading");
        Span<byte> outBuffer = stackalloc byte[1024];
        int read = fs.Read(outBuffer);
        Console.WriteLine($"Read {read}: {Convert.ToHexString(outBuffer[..read])}");

        Console.WriteLine("Writing");
        fs.Write("Testing 2"u8);

        Console.WriteLine("Reading");
        read = fs.Read(outBuffer);
        Console.WriteLine($"Read {read}: {Convert.ToHexString(outBuffer[..read])}");
    }
}

internal readonly struct CHANNEL_PDU_HEADER
{
    public const int CHANNEL_FLAG_FIRST = 1;
    public const int CHANNEL_FLAG_LAST = 2;

    public readonly int length;
    public readonly int flags;
}
