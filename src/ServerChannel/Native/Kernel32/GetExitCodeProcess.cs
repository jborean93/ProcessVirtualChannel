using System.Runtime.InteropServices;

namespace ServerChannel.Native;

internal partial class Kernel32
{
    [LibraryImport("Kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetExitCodeProcess(
        nint hProcess,
        out int lpExitCode);
}
