using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClientChannel;

[GeneratedComInterface]
[Guid("a1230207-d6a7-11d8-b9fd-000bdbd1f198")]
internal partial interface IWTSVirtualChannel
{
    void Write(int cbSize, nint pBuffer, nint pReserved);
    void Close();
}

[GeneratedComInterface]
[Guid("a1230204-d6a7-11d8-b9fd-000bdbd1f198")]
internal partial interface IWTSVirtualChannelCallback
{
    void OnDataReceived(int cbSize, nint pBuffer);
    void OnClose();
}

[GeneratedComInterface]
[Guid("a1230203-d6a7-11d8-b9fd-000bdbd1f198")]
internal partial interface IWTSListenerCallback
{
    IWTSVirtualChannelCallback? OnNewChannelConnection(
        [MarshalAs(UnmanagedType.Interface)] IWTSVirtualChannel pChannel,
        [MarshalAs(UnmanagedType.BStr)] string data,
        [MarshalAs(UnmanagedType.Bool)] out bool pbAccept);
}

[GeneratedComClass]
internal partial class WTSListenerCallbackDelegate : IWTSListenerCallback
{
    private readonly OnNewChannelConnectionDelegate _callback;
    public delegate IWTSVirtualChannelCallback? OnNewChannelConnectionDelegate(IWTSVirtualChannel channel);

    public WTSListenerCallbackDelegate(OnNewChannelConnectionDelegate callback)
    {
        _callback = callback;
    }

    public IWTSVirtualChannelCallback? OnNewChannelConnection(
        IWTSVirtualChannel pChannel,
        string data,
        out bool pbAccept)
    {
        IWTSVirtualChannelCallback? result = _callback(pChannel);
        pbAccept = result != null;
        return result;
    }
}

[GeneratedComInterface]
[Guid("a1230205-d6a7-11d8-b9fd-000bdbd1f198")]
internal partial interface IWTSVirtualChannelManager
{
    nint CreateListener(
        [MarshalAs(UnmanagedType.LPStr)] string pszChannelName,
        int uFlags,
        [MarshalAs(UnmanagedType.Interface)] IWTSListenerCallback pListenerCallback);
}

[GeneratedComInterface]
[Guid("a1230201-1439-4e62-a414-190d0ac3d40e")]
internal partial interface IWTSPlugin
{
    void Initialize(
        [MarshalAs(UnmanagedType.Interface)] IWTSVirtualChannelManager pChannelMgr);
    void Connected();
    void Disconnected(int dwDisconnectCode);
    void Terminated();
}

[GeneratedComClass]
internal partial class WTSVirtualChannelCallback : IWTSVirtualChannelCallback
{
    private readonly IWTSVirtualChannelManager _manager;
    private readonly IWTSVirtualChannel _channel;
    private readonly string _purpose;
    internal StreamWriter? _log;

    public WTSVirtualChannelCallback(
        IWTSVirtualChannelManager manager,
        IWTSVirtualChannel channel,
        string purpose)
    {
        _manager = manager;
        _channel = channel;
        _purpose = purpose;
    }

    public void OnClose()
    {
        Log("OnClose");
    }

    public void OnDataReceived(int cbSize, nint pBuffer)
    {
        Span<byte> data;
        unsafe
        {
            data = new((void*)pBuffer, cbSize);
        }
        Log($"OnDataReceived({cbSize}) - {_purpose} - {Convert.ToHexString(data)}");

        switch (_purpose)
        {
            case "process":
                Process();
                break;

            case "stdin":
                Stdin();
                break;

            case "stdout":
                Stdout("STDOUT", data);
                break;

            case "stderr":
                Stdout("STDERR", data);
                break;
        }
    }

    private void Process()
    {
        string channelName = "TestChannel";
        _manager.CreateListener(
            $"{channelName}-stdin",
            0,
            new WTSListenerCallbackDelegate((c) =>
            {
                Log($"OnNewChannelConnection - {channelName}-stdin");
                return new WTSVirtualChannelCallback(_manager, c, "stdin");
            }));
        _manager.CreateListener(
            $"{channelName}-stdout",
            0,
            new WTSListenerCallbackDelegate((c) =>
            {
                Log($"OnNewChannelConnection - {channelName}-stdout");
                return new WTSVirtualChannelCallback(_manager, c, "stdout");
            }));
        _manager.CreateListener(
            $"{channelName}-stderr",
            0,
            new WTSListenerCallbackDelegate((c) =>
            {
                Log($"OnNewChannelConnection - {channelName}-stderr");
                return new WTSVirtualChannelCallback(_manager, c, "stderr");
            }));

        ProcessManifest manifest = new(
            ChannelName: channelName,
            Executable: @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
            Arguments: "-Command -",
            WorkingDirectory: null,
            Environment: null);
        string inJson = JsonSerializer.Serialize(
            manifest,
            SourceGenerationContext.Default.ProcessManifest);

        byte[] buffer = Encoding.UTF8.GetBytes(inJson);
        Log($"Writing {inJson.Length} - {buffer.Length}\n{inJson}");
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                _channel.Write(buffer.Length, (nint)ptr, 0);
            }
        }
        return;
    }

    private void Stdin()
    {
        string msg = @". {
    ""ProcessId: $pid""
    $host.UI.WriteLine('stdout')
    $host.UI.WriteErrorLine('stderr')

    exit 1
}

";
        Span<byte> data = Encoding.UTF8.GetBytes(msg);
        unsafe
        {
            fixed (byte* dataPtr = data)
            {
                _channel.Write(data.Length, (nint)dataPtr, 0);
            }
        }
    }

    private void Stdout(string stream, ReadOnlySpan<byte> data)
    {
        string msg = Encoding.UTF8.GetString(data);
        Log($"{stream}: {msg}");
    }

    private void Log(string msg)
    {
        string logPath = string.Format(@"C:\temp\ProcessVirtualChannel\WTSVirtualChannelCallback-{0}-log.txt", _purpose);
        _log ??= new(logPath, false);
        string now = DateTime.Now.ToString("[HH:mm:ss.fff]");
        _log.WriteLine($"{now} - {msg}");
        _log.Flush();
    }
}

[GeneratedComClass]
internal partial class WTSPlugin : IWTSPlugin
{
    internal StreamWriter? _log;

    public void Initialize(IWTSVirtualChannelManager pChannelMgr)
    {
        Log("Initialize");
        pChannelMgr.CreateListener(
            "MyChannel",
            0,
            new WTSListenerCallbackDelegate((c) =>
            {
                Log("OnNewChannelConnection - MyChannel");
                return new WTSVirtualChannelCallback(pChannelMgr, c, "process");
            }));
    }

    public void Connected()
    {
        Log("Connected");
    }

    public void Disconnected(int dwDisconnectCode)
    {
        Log($"Disconnected {dwDisconnectCode}");
    }

    public void Terminated()
    {
        Log("Terminated");
    }

    private void Log(string msg)
    {
        string logPath = @"C:\temp\ProcessVirtualChannel\WTSPlugin-log.txt";
        _log ??= new(logPath, false);
        string now = DateTime.Now.ToString("[HH:mm:ss.fff]");
        _log.WriteLine($"{now} - {msg}");
        _log.Flush();
    }
}

internal static class DVCClient
{
    [UnmanagedCallersOnly(
        EntryPoint = "VirtualChannelGetInstance",
        CallConvs = [typeof(CallConvStdcall)])]
    private unsafe static int VirtualChannelGetInstance(
        Guid* refiid,
        int* pNumObjs,
        void** ppObjArray)
    {
        if (*refiid != typeof(IWTSPlugin).GUID)
        {
            return unchecked((int)0x80004002);  // E_NOINTERFACE
        }

        *pNumObjs = 1;
        if (ppObjArray != null)
        {
            *ppObjArray = ComInterfaceMarshaller<IWTSPlugin>.ConvertToUnmanaged(
                new WTSPlugin());
        }

        return 0;
    }
}

[JsonSerializable(typeof(ProcessManifest))]
[JsonSerializable(typeof(ProcessInfo))]
[JsonSerializable(typeof(ProcessResult))]
internal partial class SourceGenerationContext : JsonSerializerContext
{ }

public record ProcessManifest(
    [property: JsonRequired]
    string ChannelName,
    [property: JsonRequired]
    string Executable,
    string? Arguments,
    string? WorkingDirectory,
    Dictionary<string, string>? Environment
);

public record ProcessInfo(
    [property: JsonRequired]
    int ProcessId,
    [property: JsonRequired]
    int ThreadId
);

public record ProcessResult(
    [property: JsonRequired]
    int ReturnCode
);
