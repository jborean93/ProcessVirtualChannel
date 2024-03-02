using System.Runtime.InteropServices;

namespace ServerChannel.Native;

internal partial class WtsApi32
{
    internal enum WTS_VIRTUAL_CLASS
    {
        WTSVirtualClientData = 0,
        WTSVirtualFileHandle = 1,
    }

    [LibraryImport("Wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSVirtualChannelQuery(
        nint hChannelHandle,
        WTS_VIRTUAL_CLASS channelClass,
        nint ppBuffer,
        out int pBytesReturned);
}
