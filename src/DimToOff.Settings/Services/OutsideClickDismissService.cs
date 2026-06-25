using System.Runtime.InteropServices;

namespace DimToOff.Settings.Services;

internal sealed class OutsideClickDismissService : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;
    private const int WmXButtonDown = 0x020B;

    private readonly IntPtr windowHandle;
    private readonly Action dismiss;
    private readonly HookProc hookProc;
    private IntPtr hookHandle;
    private int dismissed;

    public OutsideClickDismissService(IntPtr windowHandle, Action dismiss)
    {
        this.windowHandle = windowHandle;
        this.dismiss = dismiss;
        hookProc = OnMouseHook;
    }

    public void Start()
    {
        hookHandle = SetWindowsHookEx(WhMouseLl, hookProc, GetModuleHandle(null), 0);
    }

    public void Dispose()
    {
        if (hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hookHandle);
            hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr OnMouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && IsDismissMouseMessage(wParam))
        {
            var mouse = Marshal.PtrToStructure<MouseHookStruct>(lParam);
            if (!IsInsideWindow(mouse.Point) && Interlocked.Exchange(ref dismissed, 1) == 0)
            {
                dismiss();
            }
        }

        return CallNextHookEx(hookHandle, code, wParam, lParam);
    }

    private bool IsInsideWindow(Point point)
    {
        if (!GetWindowRect(windowHandle, out Rect rect))
        {
            return false;
        }

        return point.X >= rect.Left &&
               point.X <= rect.Right &&
               point.Y >= rect.Top &&
               point.Y <= rect.Bottom;
    }

    private static bool IsDismissMouseMessage(IntPtr message)
    {
        int value = message.ToInt32();
        return value is WmLButtonDown or WmRButtonDown or WmMButtonDown or WmXButtonDown;
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int hookType, HookProc callback, IntPtr moduleHandle, uint threadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookStruct
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
