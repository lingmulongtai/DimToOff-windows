using System.ComponentModel;
using System.Diagnostics;
using DimToOff.Native;

namespace DimToOff.Services;

internal sealed class InputHookService : IDisposable
{
    private readonly LogService log;
    private readonly User32.LowLevelHookProc keyboardProc;
    private readonly User32.LowLevelHookProc mouseProc;
    private nint keyboardHook;
    private nint mouseHook;
    private bool disposed;

    public event EventHandler? UserInputDetected;

    public InputHookService(LogService log)
    {
        this.log = log;
        keyboardProc = KeyboardHookCallback;
        mouseProc = MouseHookCallback;
    }

    public void Start()
    {
        if (keyboardHook != nint.Zero || mouseHook != nint.Zero)
        {
            return;
        }

        nint moduleHandle = GetCurrentModuleHandle();
        keyboardHook = User32.SetWindowsHookEx(NativeConstants.WH_KEYBOARD_LL, keyboardProc, moduleHandle, 0);
        mouseHook = User32.SetWindowsHookEx(NativeConstants.WH_MOUSE_LL, mouseProc, moduleHandle, 0);

        if (keyboardHook == nint.Zero || mouseHook == nint.Zero)
        {
            log.Error("Failed to install input hooks", new Win32Exception());
            Stop();
            return;
        }

        log.Info("Input hooks installed");
    }

    public void Stop()
    {
        if (keyboardHook != nint.Zero)
        {
            if (!User32.UnhookWindowsHookEx(keyboardHook))
            {
                log.Error("Failed to remove keyboard hook", new Win32Exception());
            }

            keyboardHook = nint.Zero;
        }

        if (mouseHook != nint.Zero)
        {
            if (!User32.UnhookWindowsHookEx(mouseHook))
            {
                log.Error("Failed to remove mouse hook", new Win32Exception());
            }

            mouseHook = nint.Zero;
        }

        log.Info("Input hooks removed");
    }

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && IsKeyboardWakeMessage((int)wParam))
        {
            log.Info("User input detected: keyboard");
            UserInputDetected?.Invoke(this, EventArgs.Empty);
        }

        return User32.CallNextHookEx(keyboardHook, nCode, wParam, lParam);
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && IsMouseWakeMessage((int)wParam))
        {
            log.Info("User input detected: mouse or touchpad");
            UserInputDetected?.Invoke(this, EventArgs.Empty);
        }

        return User32.CallNextHookEx(mouseHook, nCode, wParam, lParam);
    }

    private static bool IsKeyboardWakeMessage(int message) =>
        message is NativeConstants.WM_KEYDOWN or NativeConstants.WM_SYSKEYDOWN;

    private static bool IsMouseWakeMessage(int message) =>
        message is NativeConstants.WM_MOUSEMOVE
            or NativeConstants.WM_LBUTTONDOWN
            or NativeConstants.WM_RBUTTONDOWN
            or NativeConstants.WM_MBUTTONDOWN
            or NativeConstants.WM_MOUSEWHEEL
            or NativeConstants.WM_XBUTTONDOWN;

    private static nint GetCurrentModuleHandle()
    {
        using Process process = Process.GetCurrentProcess();
        string? moduleName = process.MainModule?.ModuleName;
        return User32.GetModuleHandle(moduleName);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
    }
}
