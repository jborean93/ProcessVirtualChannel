using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using ServerChannel.Native;

namespace ServerChannel;

internal sealed class Win32Process : IDisposable
{
    public int ProcessId { get; }
    public int ThreadId { get; }
    public SafeProcessHandle ProcessHandle { get; }
    public SafeProcessHandle ThreadHandle { get; }

    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeProcessHandle handle)
        {
            SafeWaitHandle = new(handle.DangerousGetHandle(), false);
        }
    }

    private Win32Process(ref Kernel32.PROCESS_INFORMATION pi)
    {
        ProcessId = pi.dwProcessId;
        ThreadId = pi.dwThreadId;
        ProcessHandle = new(pi.hProcess, true);
        ThreadHandle = new(pi.hThread, true);
    }

    public async Task<int> WaitForExitAsync()
    {
        ProcessWaitHandle waitHandle = new(ProcessHandle);
        TaskCompletionSource tcs = new();
        RegisteredWaitHandle taskWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            waitHandle,
            (s, t) => tcs.SetResult(),
            null,
            -1,
            true);

        await tcs.Task;

        taskWaitHandle.Unregister(waitHandle);

        if (!Kernel32.GetExitCodeProcess(ProcessHandle.DangerousGetHandle(), out var rc))
        {
            throw new Win32Exception();
        }

        return rc;
    }

    public static Win32Process Create(
        string? applicationName,
        string? commandLine,
        string? workingDirectory,
        Dictionary<string, string>? environment,
        SafeHandle? stdinPipe,
        SafeHandle? stdoutPipe,
        SafeHandle? stderrPipe)
    {
        int creationFlags = Kernel32.CREATE_NEW_CONSOLE |
            Kernel32.CREATE_UNICODE_ENVIRONMENT |
            Kernel32.EXTENDED_STARTUPINFO_PRESENT;
        Kernel32.STARTUPINFOEXW startupInfo = new()
        {
            StartupInfo = new()
            {
                cb = Marshal.SizeOf<Kernel32.STARTUPINFOEXW>(),
            }
        };
        Kernel32.PROCESS_INFORMATION processInfo;

        if (stdinPipe != null)
        {
            if (stdinPipe.IsInvalid || stdinPipe.IsClosed)
            {
                throw new ArgumentException("Stdin SafeHandle is close or invalid", nameof(stdinPipe));
            }
            startupInfo.StartupInfo.dwFlags |= Kernel32.STARTF_USESTDHANDLES;
            startupInfo.StartupInfo.hStdInput = stdinPipe.DangerousGetHandle();
        }

        if (stdoutPipe != null)
        {
            if (stdoutPipe.IsInvalid || stdoutPipe.IsClosed)
            {
                throw new ArgumentException("Stdout SafeHandle is close or invalid", nameof(stdoutPipe));
            }
            startupInfo.StartupInfo.dwFlags |= Kernel32.STARTF_USESTDHANDLES;
            startupInfo.StartupInfo.hStdOutput = stdoutPipe.DangerousGetHandle();
        }

        if (stderrPipe != null)
        {
            if (stderrPipe.IsInvalid || stderrPipe.IsClosed)
            {
                throw new ArgumentException("Stderr SafeHandle is close or invalid", nameof(stderrPipe));
            }
            startupInfo.StartupInfo.dwFlags |= Kernel32.STARTF_USESTDHANDLES;
            startupInfo.StartupInfo.hStdError = stderrPipe.DangerousGetHandle();
        }

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
                    true,  // TODO: Pass in only stdio handles through the ProcThreadAttributeList
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