using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ServerChannel;

public static class Program
{
    public static int Main(params string[] args)
    {
        Debug.Assert(args.Length == 1, "channel name needs to be set as an argument");
        nint wtsFileHandle = IntPtr.Zero;
        Console.WriteLine($"Opening {args[0]} channel");
        nint wtsHandle = WtsApi.WTSVirtualChannelOpenEx(
            WtsApi.WTS_CURRENT_SESSION,
            args[0],
            WtsApi.WTS_CHANNEL_OPTION_DYNAMIC);
        if (wtsHandle == IntPtr.Zero)
        {
            throw new Win32Exception();
        }

        try
        {
            bool queryRes;
            Console.WriteLine("Getting channel file handle");
            unsafe
            {
                queryRes = WtsApi.WTSVirtualChannelQuery(
                    wtsHandle,
                    WtsApi.WTS_VIRTUAL_CLASS.WTSVirtualFileHandle,
                    (nint)(void*)&wtsFileHandle,
                    out var _);
            }

            if (!queryRes)
            {
                throw new Win32Exception();
            }

            Console.WriteLine("Creating FileStream");
            using SafeFileHandle fsHandle = new(Marshal.ReadIntPtr(wtsFileHandle), false);
            using FileStream fs = new(fsHandle, FileAccess.ReadWrite, 0, true);

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
        finally
        {
            if (wtsFileHandle != IntPtr.Zero)
            {
                WtsApi.WTSFreeMemory(wtsFileHandle);
            }
            WtsApi.WTSVirtualChannelClose(wtsHandle);
        }

        return 0;
    }
}

internal partial class Kernel32
{
    [LibraryImport("Kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DuplicateHandle(
        nint hSourceProcessHandle,
        nint hSourceHandle,
        nint hTargetProcessHandle,
        out nint lpTargetHandle,
        int dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int dwOptions);


    [LibraryImport("Kernel32.dll")]
    internal static partial nint GetCurrentProcess();
}

internal partial class WtsApi
{
    internal const int WTS_CURRENT_SESSION = -1;

    internal const int WTS_CHANNEL_OPTION_DYNAMIC = 0x00000001;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_PRI_LOW = 0x00000000;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_PRI_MED = 0x00000002;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_PRI_HIGH = 0x00000004;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_PRI_REAL = 0x00000006;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_NO_COMPRESS = 0x00000008;

    internal enum WTS_VIRTUAL_CLASS
    {
        WTSVirtualClientData = 0,
        WTSVirtualFileHandle = 1,
    }

    [LibraryImport("Wtsapi32.dll")]
    internal static partial void WTSFreeMemory(
        nint pMemory);

    [LibraryImport("Wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSVirtualChannelClose(
        nint hChannelHandle);

    [LibraryImport("Wtsapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint WTSVirtualChannelOpenEx(
        int SessionId,
        string pVirtualName,
        int flags);

    [LibraryImport("Wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSVirtualChannelQuery(
        nint hChannelHandle,
        WTS_VIRTUAL_CLASS channelClass,
        nint ppBuffer,
        out int pBytesReturned);
}
