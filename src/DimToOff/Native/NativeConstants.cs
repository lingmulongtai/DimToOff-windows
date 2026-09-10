namespace DimToOff.Native;

internal static class NativeConstants
{
    public const int WM_SYSCOMMAND = 0x0112;
    public const int WM_POWERBROADCAST = 0x0218;
    public const int SC_MONITORPOWER = 0xF170;
    public const int SC_SCREENSAVE = 0xF140;
    /// <summary>WM_SYSCOMMAND reserves the low four bits of wParam for the system.</summary>
    public const int SYSCOMMAND_MASK = 0xFFF0;
    public const int PBT_APMPOWERSTATUSCHANGE = 0x000A;
    public const int PBT_APMRESUMEAUTOMATIC = 0x0012;
    public const int PBT_POWERSETTINGCHANGE = 0x8013;
    public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
    public const uint EFFECTIVE_POWER_MODE_V2 = 2;
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MOUSEWHEEL = 0x020A;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int SW_SHOWNORMAL = 1;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SPI_GETSCREENSAVEACTIVE = 0x0010;
    public const uint SPI_SETSCREENSAVEACTIVE = 0x0011;
    public const uint SPI_GETSCREENSAVERRUNNING = 0x0072;
    public const uint SPIF_SENDCHANGE = 0x0002;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
}
