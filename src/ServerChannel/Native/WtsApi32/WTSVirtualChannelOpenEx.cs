using System.Runtime.InteropServices;

namespace ServerChannel.Native;

internal partial class WtsApi32
{
    internal const int WTS_CURRENT_SESSION = -1;

    internal const int WTS_CHANNEL_OPTION_DYNAMIC = 0x00000001;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_PRI_LOW = 0x00000000;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_PRI_MED = 0x00000002;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_PRI_HIGH = 0x00000004;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_PRI_REAL = 0x00000006;
    internal const int WTS_CHANNEL_OPTION_DYNAMIC_NO_COMPRESS = 0x00000008;

    [LibraryImport("Wtsapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint WTSVirtualChannelOpenEx(
        int SessionId,
        string pVirtualName,
        int flags);
}
