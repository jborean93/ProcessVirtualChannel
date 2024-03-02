using System.Runtime.InteropServices;

namespace ServerChannel.Native;

internal partial class WtsApi32
{
    [LibraryImport("Wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSVirtualChannelClose(
        nint hChannelHandle);
}