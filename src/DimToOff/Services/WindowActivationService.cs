using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DimToOff.Services;

internal static class WindowActivationService
{
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private static readonly IntPtr HwndTop = IntPtr.Zero;

    public static bool TryActivate(Process process, LogService log)
    {
        try
        {
            IntPtr hwnd = WaitForMainWindow(process);
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            ShowWindow(hwnd, IsIconic(hwnd) ? SwRestore : SwShow);
            SetWindowPos(hwnd, HwndTop, 0, 0, 0, 0, SwpNoMove | SwpNoSize);
            SetForegroundWindow(hwnd);
            return true;
        }
        catch (Exception ex)
        {
            log.Error("Failed to activate existing settings window", ex);
            return false;
        }
    }

    private static IntPtr WaitForMainWindow(Process process)
    {
        try
        {
            process.WaitForInputIdle(500);
        }
        catch
        {
        }

        DateTime deadline = DateTime.UtcNow.AddMilliseconds(1200);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                return IntPtr.Zero;
            }

            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return process.MainWindowHandle;
            }

            Thread.Sleep(80);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

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
}
