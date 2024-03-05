using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ServerChannel.Native;

namespace ServerChannel;

public sealed class SafeVirtualChannelHandle : SafeHandle
{
    internal SafeVirtualChannelHandle(nint handle) : base(handle, true)
    { }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
        => WtsApi32.WTSVirtualChannelClose(handle);
}

[Flags]
public enum ChannelOption
{
    None = 0,
    PriorityLow = WtsApi32.WTS_CHANNEL_OPTION_DYNAMIC_PRI_LOW,
    PriorityMedium = WtsApi32.WTS_CHANNEL_OPTION_DYNAMIC_PRI_MED,
    PriorityHigh = WtsApi32.WTS_CHANNEL_OPTION_DYNAMIC_PRI_HIGH,
    PriorityReadTime = WtsApi32.WTS_CHANNEL_OPTION_DYNAMIC_PRI_REAL,
    NoCompression = WtsApi32.WTS_CHANNEL_OPTION_DYNAMIC_NO_COMPRESS,
}

public static class VirtualChannel
{
    public static SafeVirtualChannelHandle OpenDynamicChannel(string name)
        => OpenDynamicChannel(name, WtsApi32.WTS_CURRENT_SESSION, ChannelOption.None);

    public static SafeVirtualChannelHandle OpenDynamicChannel(string name, ChannelOption options)
        => OpenDynamicChannel(name, WtsApi32.WTS_CURRENT_SESSION, options);

    public static SafeVirtualChannelHandle OpenDynamicChannel(
        string name,
        int sessionId,
        ChannelOption options)
    {
        int flags = (int)options | WtsApi32.WTS_CHANNEL_OPTION_DYNAMIC;
        return OpenChannelCore(sessionId, name, flags);
    }

    public static SafeVirtualChannelHandle OpenStaticChannel(string name)
        => OpenStaticChannel(name, WtsApi32.WTS_CURRENT_SESSION);

    public static SafeVirtualChannelHandle OpenStaticChannel(string name, int sessionId)
        => OpenChannelCore(sessionId, name, 0);

    public static NamedPipeServerStream GetStream(SafeVirtualChannelHandle channel)
    {
        if (channel.IsInvalid || channel.IsClosed)
        {
            throw new ArgumentException("Provided channel is either invalid or closed");
        }

        nint fileHandlePtr;
        bool queryRes;
        unsafe
        {
            queryRes = WtsApi32.WTSVirtualChannelQuery(
                channel.DangerousGetHandle(),
                WtsApi32.WTS_VIRTUAL_CLASS.WTSVirtualFileHandle,
                (nint)(void*)&fileHandlePtr,
                out var _);
        }

        if (!queryRes)
        {
            throw new Win32Exception();
        }

        try
        {
            // The stream an outlive the channel by duplicating the handle.
            nint currentProcess = Kernel32.GetCurrentProcess();
            bool dupRes = Kernel32.DuplicateHandle(
                currentProcess,
                Marshal.ReadIntPtr(fileHandlePtr),
                currentProcess,
                out nint fileHandle,
                0,
                false,
                Kernel32.DUPLICATE_SAME_ACCESS);

            // We use a pipe as it can handle non buffered reads in sections.
            // Using FileStream will result in "More data is available" on a
            // read that doesn't read the whole buffer.
            SafePipeHandle fsHandle = new(fileHandle, true);
            return new(PipeDirection.InOut, true, true, fsHandle);
        }
        finally
        {
            WtsApi32.WTSFreeMemory(fileHandlePtr);
        }
    }

    private static SafeVirtualChannelHandle OpenChannelCore(
        int sessionId,
        string name,
        int flags)
    {
        nint channelHandle = WtsApi32.WTSVirtualChannelOpenEx(sessionId, name, flags);
        if (channelHandle == IntPtr.Zero)
        {
            int err = Marshal.GetLastPInvokeError();
            if (err == 0x0000001F) // ERROR_GEN_FAILURE - error used when the channel does not exist.
            {
                throw new ArgumentException($"VirtualChannel '{name}' does not exist", nameof(name));
            }
            else
            {
                throw new Win32Exception(err);
            }
        }

        return new(channelHandle);
    }
}
