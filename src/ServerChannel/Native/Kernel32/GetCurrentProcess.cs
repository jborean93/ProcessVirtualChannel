using System.Runtime.InteropServices;

namespace ServerChannel.Native;

internal partial class Kernel32
{
    [LibraryImport("Kernel32.dll")]
    internal static partial nint GetCurrentProcess();
}
