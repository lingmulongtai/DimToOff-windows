using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace DimToOff.Settings.Services;

internal static class NativeWindow
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int WsCaption = 0x00C00000;
    private const int WsThickFrame = 0x00040000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsMaximizeBox = 0x00010000;
    private const int WsExAppWindow = 0x00040000;
    private const int WsExToolWindow = 0x00000080;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;
    private const int SwShow = 5;
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    public static void MakeBorderlessToolWindow(IntPtr hwnd)
    {
        int style = GetWindowLong(hwnd, GwlStyle);
        style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox);
        SetWindowLong(hwnd, GwlStyle, style);

        int exStyle = GetWindowLong(hwnd, GwlExStyle);
        exStyle = (exStyle | WsExToolWindow) & ~WsExAppWindow;
        SetWindowLong(hwnd, GwlExStyle, exStyle);

        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
    }

    public static void ResizeForDips(IntPtr hwnd, AppWindow appWindow, int widthDips, int heightDips)
    {
        SizeInt32 size = ToPhysicalSize(hwnd, widthDips, heightDips);
        appWindow.Resize(size);
    }

    public static void BringToFront(IntPtr hwnd)
    {
        ShowWindow(hwnd, SwShow);
        SetWindowPos(hwnd, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize);
        SetForegroundWindow(hwnd);
        SetWindowPos(hwnd, HwndNoTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize);
    }

    public static void MoveNearCursorForDips(IntPtr hwnd, AppWindow appWindow, int widthDips, int heightDips)
    {
        SizeInt32 size = ToPhysicalSize(hwnd, widthDips, heightDips);
        int offset = ToPhysicalPixels(hwnd, 12);

        _ = GetCursorPos(out Point point);
        DisplayArea displayArea = DisplayArea.GetFromPoint(
            new PointInt32(point.X, point.Y),
            DisplayAreaFallback.Nearest);
        RectInt32 workArea = displayArea.WorkArea;

        int maxX = Math.Max(workArea.X, workArea.X + workArea.Width - size.Width);
        int maxY = Math.Max(workArea.Y, workArea.Y + workArea.Height - size.Height);
        int x = Math.Clamp(point.X - size.Width + offset, workArea.X, maxX);
        int y = Math.Clamp(point.Y - size.Height + offset, workArea.Y, maxY);
        appWindow.MoveAndResize(new RectInt32(x, y, size.Width, size.Height));
    }

    public static void ApplyRoundedRegionForDips(IntPtr hwnd, int widthDips, int heightDips, int radiusDips)
    {
        SizeInt32 size = ToPhysicalSize(hwnd, widthDips, heightDips);
        int radius = ToPhysicalPixels(hwnd, radiusDips);
        IntPtr region = CreateRoundRectRgn(0, 0, size.Width + 1, size.Height + 1, radius * 2, radius * 2);
        if (region == IntPtr.Zero)
        {
            return;
        }

        if (SetWindowRgn(hwnd, region, true) == 0)
        {
            DeleteObject(region);
        }
    }

    private static SizeInt32 ToPhysicalSize(IntPtr hwnd, int widthDips, int heightDips) =>
        new(ToPhysicalPixels(hwnd, widthDips), ToPhysicalPixels(hwnd, heightDips));

    public static int ToPhysicalPixels(IntPtr hwnd, int dips)
    {
        uint dpi = GetDpiForWindow(hwnd);
        if (dpi == 0)
        {
            dpi = 96;
        }

        return (int)Math.Ceiling(dips * dpi / 96d);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int widthEllipse,
        int heightEllipse);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr value);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
