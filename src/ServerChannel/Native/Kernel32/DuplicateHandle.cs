using System.Runtime.InteropServices;

namespace ServerChannel.Native;

internal partial class Kernel32
{
    internal const int DUPLICATE_CLOSE_SOURCE = 0x00000001;
    internal const int DUPLICATE_SAME_ACCESS = 0x00000002;

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
}
