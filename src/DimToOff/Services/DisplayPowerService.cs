using DimToOff.Native;

namespace DimToOff.Services;

internal sealed class DisplayPowerService
{
    private readonly LogService log;

    public DisplayPowerService(LogService log)
    {
        this.log = log;
    }

    public void TurnOffDisplay()
    {
        PreventSystemSleepWhileDisplayIsOff();
        log.Info("Turning display off");
        User32.SendMessage(
            User32.HwndBroadcast,
            NativeConstants.WM_SYSCOMMAND,
            new nint(NativeConstants.SC_MONITORPOWER),
            new nint(2));
    }

    public void TurnOnDisplay()
    {
        log.Info("Turning display on requested");
        User32.SendMessage(
            User32.HwndBroadcast,
            NativeConstants.WM_SYSCOMMAND,
            new nint(NativeConstants.SC_MONITORPOWER),
            new nint(-1));
    }

    public void AllowNormalSleepPolicy()
    {
        Kernel32.SetThreadExecutionState(Kernel32.ExecutionState.Continuous);
        log.Info("Normal system sleep policy restored");
    }

    private void PreventSystemSleepWhileDisplayIsOff()
    {
        Kernel32.SetThreadExecutionState(
            Kernel32.ExecutionState.Continuous |
            Kernel32.ExecutionState.SystemRequired);
        log.Info("System sleep prevention requested while display is off");
    }
}
