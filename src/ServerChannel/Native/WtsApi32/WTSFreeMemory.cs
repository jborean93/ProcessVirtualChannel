using System.Runtime.InteropServices;

namespace ServerChannel.Native;

internal partial class WtsApi32
{
    [LibraryImport("Wtsapi32.dll")]
    internal static partial void WTSFreeMemory(
        nint pMemory);
}
