using System.Runtime.InteropServices;

namespace ServerChannel.Native;

internal partial class Kernel32
{
    internal const int DEBUG_PROCESS = 0x00000001;
    internal const int DEBUG_ONLY_THIS_PROCESS = 0x00000002;
    internal const int CREATE_SUSPENDED = 0x00000004;
    internal const int DETACHED_PROCESS = 0x00000008;
    internal const int CREATE_NEW_CONSOLE = 0x00000010;
    internal const int NORMAL_PRIORITY_CLASS = 0x00000020;
    internal const int IDLE_PRIORITY_CLASS = 0x00000040;
    internal const int HIGH_PRIORITY_CLASS = 0x00000080;
    internal const int REALTIME_PRIORITY_CLASS = 0x00000100;
    internal const int CREATE_NEW_PROCESS_GROUP = 0x00000200;
    internal const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    internal const int CREATE_SEPARATE_WOW_VDM = 0x00000800;
    internal const int CREATE_SHARED_WOW_VDM = 0x00001000;
    internal const int CREATE_FORCEDOS = 0x00002000;
    internal const int BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    internal const int ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
    internal const int INHERIT_PARENT_AFFINITY = 0x00010000;
    internal const int INHERIT_CALLER_PRIORITY = 0x00020000;
    internal const int CREATE_PROTECTED_PROCESS = 0x00040000;
    internal const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    internal const int PROCESS_MODE_BACKGROUND_BEGIN = 0x00100000;
    internal const int PROCESS_MODE_BACKGROUND_END = 0x00200000;
    internal const int CREATE_SECURE_PROCESS = 0x00400000;
    internal const int CREATE_BREAKAWAY_FROM_JOB = 0x01000000;
    internal const int CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000;
    internal const int CREATE_DEFAULT_ERROR_MODE = 0x04000000;
    internal const int CREATE_NO_WINDOW = 0x08000000;
    internal const int PROFILE_USER = 0x10000000;
    internal const int PROFILE_KERNEL = 0x20000000;
    internal const int PROFILE_SERVER = 0x40000000;
    internal const int CREATE_IGNORE_SYSTEM_DEFAULT = unchecked((int)0x80000000);

    internal const int STARTF_USESHOWWINDOW = 0x00000001;
    internal const int STARTF_USESIZE = 0x00000002;
    internal const int STARTF_USEPOSITION = 0x00000004;
    internal const int STARTF_USECOUNTCHARS = 0x00000008;
    internal const int STARTF_USEFILLATTRIBUTE = 0x00000010;
    internal const int STARTF_RUNFULLSCREEN = 0x00000020;
    internal const int STARTF_FORCEONFEEDBACK = 0x00000040;
    internal const int STARTF_FORCEOFFFEEDBACK = 0x00000080;
    internal const int STARTF_USESTDHANDLES = 0x00000100;
    internal const int STARTF_USEHOTKEY = 0x00000200;
    internal const int STARTF_TITLEISLINKNAME = 0x00000800;
    internal const int STARTF_TITLEISAPPID = 0x00001000;
    internal const int STARTF_PREVENTPINNING = 0x00002000;
    internal const int STARTF_UNTRUSTEDSOURCE = 0x00008000;
    internal const int STARTF_HOLOGRAPHIC = 0x00040000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTINFOW
    {
        public int cb;
        public nint lpReserved;
        public nint lpDesktop;
        public nint lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public nint lpReserved2;
        public nint hStdInput;
        public nint hStdOutput;
        public nint hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTUPINFOEXW
    {
        public STARTINFOW StartupInfo;
        public nint lpAttributeList;
    }

    [LibraryImport("Kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal unsafe static partial bool CreateProcessW(
        string? lpApplicationName,
        char* lpCommandLine,
        nint lpProcessAttributes,
        nint lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInherithandles,
        int dwCreationFlags,
        char* lpEnvironment,
        char* lpCurrentDirectory,
        ref STARTUPINFOEXW lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);
}
