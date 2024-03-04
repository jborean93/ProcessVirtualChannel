using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using ServerChannel.Native;

namespace ServerChannel;

internal sealed class Win32Process : IDisposable
{
    public int ProcessId { get; }
    public int ThreadId { get; }
    public SafeProcessHandle ProcessHandle { get; }
    public SafeProcessHandle ThreadHandle { get; }

    private Win32Process(ref Kernel32.PROCESS_INFORMATION pi)
    {
        ProcessId = pi.dwProcessId;
        ThreadId = pi.dwThreadId;
        ProcessHandle = new(pi.hProcess, true);
        ThreadHandle = new(pi.hThread, true);
    }

    public static Win32Process Create(
        string? applicationName,
        string? commandLine,
        string? workingDirectory,
        Dictionary<string, string>? environment)
    {
        int creationFlags = Kernel32.CREATE_NEW_CONSOLE |
            Kernel32.CREATE_UNICODE_ENVIRONMENT |
            Kernel32.EXTENDED_STARTUPINFO_PRESENT;
        Kernel32.STARTUPINFOEXW startupInfo = new()
        {
            StartupInfo = new()
            {
                cb = Marshal.SizeOf<Kernel32.STARTUPINFOEXW>(),
                dwFlags = Kernel32.STARTF_USESTDHANDLES,
            }
        };
        Kernel32.PROCESS_INFORMATION processInfo;

        string? envBlock = null;
        if (environment?.Count > 0)
        {
            StringBuilder envBuilder = new();
            foreach (KeyValuePair<string, string> kvp in environment)
            {
                envBuilder.AppendFormat("{0}={1}\0", kvp.Key, kvp.Value);
            }
            envBuilder.Append('\0');
            envBlock = envBuilder.ToString();
        }

        bool res;
        unsafe
        {
            fixed (char* commandLinePtr = commandLine)
            fixed (char* envPtr = envBlock)
            fixed (char* workingDirPtr = workingDirectory)
            {
                res = Kernel32.CreateProcessW(
                    applicationName,
                    commandLinePtr,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    creationFlags,
                    envPtr,
                    workingDirPtr,
                    ref startupInfo,
                    out processInfo);
            }
        }

        if (!res)
        {
            throw new Win32Exception();
        }

        return new(ref processInfo);
    }

    public void Dispose()
    {
        ProcessHandle.Dispose();
        ThreadHandle.Dispose();
        GC.SuppressFinalize(this);
    }
    ~Win32Process() => Dispose();
}