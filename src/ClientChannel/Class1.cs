using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

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
    private readonly IWTSVirtualChannel _channel;
    internal StreamWriter? _log;

    public WTSVirtualChannelCallback(IWTSVirtualChannel channel)
    {
        _channel = channel;
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

        Log($"OnDataReceived({cbSize}) - {Convert.ToHexString(data)}");
        _channel.Write(cbSize, pBuffer, 0);
    }

    private void Log(string msg)
    {
        string logPath = @"C:\temp\ProcessVirtualChannel\WTSVirtualChannelCallback-log.txt";
        _log ??= new(logPath, false);
        string now = DateTime.Now.ToString("[HH:mm:ss.fff]");
        _log.WriteLine($"{now} - {msg}");
        _log.Flush();
    }
}

[GeneratedComClass]
internal partial class WTSPlugin : IWTSPlugin, IWTSListenerCallback
{
    internal StreamWriter? _log;

    public void Initialize(IWTSVirtualChannelManager pChannelMgr)
    {
        Log("Initialize");
        pChannelMgr.CreateListener(
            "MyChannel",
            0,
            this);
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

    public IWTSVirtualChannelCallback? OnNewChannelConnection(
        IWTSVirtualChannel pChannel,
        string data,
        out bool pbAccept)
    {
        Log($"OnNewChannelConnection");
        pbAccept = true;
        return new WTSVirtualChannelCallback(pChannel);
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
