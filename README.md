# DimToOff

DimToOff is a Windows tray utility for laptops. It turns only the display off, never the PC. Lower the built-in display brightness to the minimum threshold and the screen goes dark; step away for a while and it goes dark on its own. Keyboard, mouse, or touchpad input brings the desktop back, with no lock screen in between, and a brightness-triggered blackout restores the last usable brightness saved before the threshold was reached.

While DimToOff is watching for you to step away, Windows is held back from its own screen timeout and sleep, so the PC keeps downloading, building, syncing, and playing audio behind a black screen.

## Supported Environment

- Windows 10 or Windows 11
- Laptop built-in display with WMI brightness support
- .NET 8 SDK for building
- Windows App SDK packages are restored automatically for the WinUI 3 interface
- No administrator privileges required

## Best-Fit Displays

DimToOff is most useful on OLED and mini-LED displays.

The default `Blackout` mode keeps Windows awake and unlocked by placing a fullscreen black overlay on top of the desktop. On OLED panels, black pixels emit little to no light, and on mini-LED panels, local dimming may reduce visible output. On typical LCD panels, the backlight usually remains on even when the screen is black, so the app can still hide the desktop but may not meaningfully reduce panel power use or backlight wear.

`MonitorPower` mode can request a real display power-off through Windows, but some laptops route that request into lock, sleep, or Modern Standby behavior. For that reason, `Blackout` is the default.

## Away Blanking

`Blank the screen when I am away` replaces the Windows screen timeout with the DimToOff blackout.

- Windows never reaches its own display-off or sleep timeout while the option is on. DimToOff resets the Windows idle timers periodically instead of editing the power plan, so quitting the app hands the policy back immediately and nothing has to be undone by hand.
- After the configured idle time, DimToOff blanks the screen itself. Defaults are 10 minutes plugged in and 5 minutes on battery, matching the feel of the Windows defaults.
- The PC keeps running the whole time. Downloads, builds, backups, and playback continue behind the black screen.
- The first key press, mouse move, or touchpad touch brings the desktop straight back, because the session was never locked or suspended.
- Automatic blanking stands down while a fullscreen app is in the foreground, or while another app asks Windows to keep the display on, such as a video player during playback. Both conditions are optional.
- The takeover can be limited to the built-in panel by standing down while an external monitor is connected.

Set either timeout to `Never` to keep the Windows behavior for that power source.

## How It Works

- Watches `WmiMonitorBrightnessEvent` in `root\wmi`.
- Reads how long the user has been idle with `GetLastInputInfo`, and blanks the screen when the away timeout is reached.
- Suspends the Windows display and sleep timeouts by pulsing `SetThreadExecutionState` rather than holding `ES_CONTINUOUS`, which also leaves the system-wide execution state readable so other apps' display requests can be honored.
- Treats brightness `<= 1%` as the MVP off trigger.
- Debounces the trigger for 800 ms, then shows a fullscreen black blanking layer by default. This keeps Windows unlocked and awake, so audio and normal background work continue.
- `WM_SYSCOMMAND / SC_MONITORPOWER` remains available as `DisplayOffMode: "MonitorPower"` in settings, but it is not the default because some laptops route it into lock or Modern Standby behavior.
- Installs low-level keyboard and mouse hooks only while the display is off by the app.
- Restores `lastUsableBrightness`, never the minimum brightness value itself.
- Remembers only a usable brightness level at or above `minimumRestoreBrightness`, so the last tiny step before display-off is not used as the restore target.
- Uses a cooldown after restore to prevent immediate re-off loops.
- Requires brightness to return to a usable level before automatic display-off can arm again after a failed or partial restore.
- Keeps the system awake while the display is off; the app turns off the display only, never the PC.
- Restores brightness only for a blackout that the brightness keys triggered. Away and manual blackouts never touched the brightness, so waking from them leaves it alone.
- Saves restore brightness only after the brightness has stayed stable for a short period, so holding the brightness-down key does not accidentally store a too-dark value.
- Fades the blackout overlay in instead of showing it abruptly.
- Can optionally preserve the last stable brightness after Windows power mode, battery saver, AC/DC, or performance-mode changes that try to adjust panel brightness.
- Does not store key contents, mouse coordinates, input history, telemetry, or update-check history.
- When update notifications are enabled, contacts GitHub Releases once a day to check whether a newer installer is available.

## Build

Install the .NET 8 SDK, then run:

```powershell
dotnet restore
dotnet build
```

For a release build:

```powershell
dotnet build -c Release
```

The solution contains two executables:

- `DimToOff.exe`: the tray resident app and display/brightness controller
- `DimToOff.Settings.exe`: the WinUI 3 settings window and tray quick panel

For release packaging, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Publish-Release.ps1 -Version v0.5.0
```

The script creates release zips under `release\<version>`.

For a Microsoft Store-oriented installer, install Inno Setup 6 and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build-Installer.ps1 -Version v0.5.0
```

This creates `DimToOff-<version>-setup.exe` from the standalone publish folder. The installer is per-user by default and does not require administrator privileges.
The setup executable and its SHA256 file are written under `release\<version>`. If `ISCC.exe` is not on `PATH`, pass `-InnoSetupCompiler` with the full path to the compiler.

## Release Downloads

GitHub releases provide three Windows x64 downloads:

- `DimToOff-<version>-setup.exe`: recommended for most testers. It installs DimToOff for the current user under `%LOCALAPPDATA%\Programs\DimToOff` and does not require administrator privileges.
- `DimToOff-<version>-win-x64.zip`: standalone build. This is the largest download, but it includes the .NET runtime and Windows App SDK files needed by the tray app and WinUI settings surface.
- `DimToOff-<version>-win-x64-small.zip`: smaller framework-dependent build. Use this when the target PC already has the .NET 8 Desktop Runtime and the Windows App Runtime required by the Windows App SDK.

DimToOff is not published as a lone `DimToOff.exe` download because the settings window is a second WinUI executable and needs native Windows App SDK assets beside it. Downloading only the tray executable would make Settings and the tray quick panel fail to open.

For Microsoft Store preparation notes, see:

- `PRIVACY.md`
- `docs/store-listing.md`
- `docs/store-readiness-checklist.md`
- `installer/DimToOff.iss`

## Run

From the repository root:

```powershell
dotnet build
dotnet run --project .\src\DimToOff\DimToOff.csproj
```

The app appears in the Windows notification area as `DimToOff`. Build the full solution before running from source so the WinUI 3 settings app is available to the tray process.

From a published build, run:

```powershell
.\publish\win-x64-standalone\DimToOff.exe
```

## Use

1. Start DimToOff.
2. Lower laptop display brightness to 1% or lower.
3. After about 800 ms, the display turns off while the PC keeps running.
4. Press a key, move/click the mouse, or use the touchpad.
5. After Windows wakes the display, DimToOff restores the last brightness above the threshold.

Or leave the brightness alone and simply step away. After the away timeout the screen goes black on its own, and the first
key press or touchpad touch brings the desktop back.

Left-click the tray icon for settings. Right-click it for the quick panel:

- DimToOff enabled
- Blank when I am away
- Blank screen now
- Restore brightness
- Settings
- Open at boot
- About
- Exit

The settings window also includes `Preserve brightness`, `Update notifications`, and `Check now`. `Preserve brightness` restores the last stable brightness when Windows or supported OEM performance mode changes try to move the panel brightness. Update checks only read the latest GitHub Release metadata and notify when a newer installer is available. Clicking the notification opens the release page so the user can download and run the installer.

## Exit

Right-click the tray icon and choose `Exit`. The app stops WMI watching and removes input hooks before the process exits.

## Settings

MVP settings are stored at:

```text
%APPDATA%\DimToOff\settings.json
```

Default values:

```json
{
  "Enabled": true,
  "BrightnessBlackoutEnabled": true,
  "OffThreshold": 1,
  "DebounceMs": 800,
  "CooldownMs": 1500,
  "IgnoreInputMs": 300,
  "BrightnessSaveStableMs": 2500,
  "FadeToBlackMs": 280,
  "DisplayOffMode": "Blackout",
  "RestoreMode": "LastUsableWithMinimum",
  "MinimumRestoreBrightness": 30,
  "DefaultRestoreBrightness": 50,
  "StartWithWindows": false,
  "ShowErrorNotifications": true,
  "CheckForUpdates": true,
  "UpdateCheckIntervalHours": 24,
  "LastUpdateCheckUtc": null,
  "LastNotifiedUpdateVersion": "",
  "PreserveBrightnessOnPowerModeChange": true,
  "BrightnessGuardWindowMs": 12000,
  "BrightnessGuardTolerancePercent": 2,
  "IdleBlackoutEnabled": true,
  "IdleTimeoutPluggedInSeconds": 600,
  "IdleTimeoutOnBatterySeconds": 300,
  "IdleRespectAppDisplayRequests": true,
  "IdleSkipWhileFullscreenApp": true,
  "IdleSkipWhenExternalMonitorConnected": false
}
```

The settings window edits every value above except the update bookkeeping fields. An idle timeout of `0` means never.
`Start with Windows` uses the current user's Run key and does not require administrator privileges.

## Logs

Logs are written to:

```text
%LOCALAPPDATA%\DimToOff\logs\dimtooff.log
```

The log records app lifecycle events, brightness changes, WMI errors, display-off requests, input-detected facts, and restore attempts. It does not record key values, mouse coordinates, or input history.

If update notifications are enabled, the log may record whether an update check succeeded or failed and which newer release tag was found. It does not record personal identifiers or usage analytics.

## Known Limitations

- Some Windows laptops and external monitors do not expose WMI brightness events.
- DimToOff includes a polling fallback, but brightness behavior still depends on OEM firmware, GPU drivers, and Modern Standby behavior.
- External monitor per-display off/on control is outside the MVP.
- `SC_MONITORPOWER` may affect all connected displays.
- Low-level input hooks are used only to detect that input occurred; key contents and mouse coordinates are not recorded.
- Some touchpads or mice may generate tiny input immediately after display off. The MVP ignores input for 300 ms after turning the display off.
- An away blackout never locks or suspends the session, so a Windows sign-in requirement on wake does not apply to it. Press Win+L before leaving if the PC has to be locked.
- While away blanking is on, the PC will not sleep on its own, including on battery. Set the on-battery timeout to `Never` to leave Windows in charge there.
- Fullscreen detection uses `SHQueryUserNotificationState`, which reports fullscreen, presentation, and busy states. A borderless fullscreen window is not always reported as fullscreen.
- The installer and executables are not code-signed yet. Windows SmartScreen may warn on first run.
- Update notifications require access to `api.github.com` and `github.com`. If these are blocked, manual or automatic update checks will fail without affecting screen blanking.
- Brightness preservation depends on Windows power notifications. Some OEM gaming/performance utilities may adjust brightness without sending a standard notification, so those changes may not always be caught.
- If the user intentionally changes brightness immediately after a power/performance mode switch, `Preserve brightness` may treat that change as automatic and restore the previous stable level.

## Troubleshooting

- If the app does not build, confirm the .NET 8 SDK is installed with `dotnet --list-sdks`.
- If brightness changes are not detected, check `%LOCALAPPDATA%\DimToOff\logs\dimtooff.log` for WMI errors.
- If the display turns off but brightness does not restore, try increasing `defaultRestoreBrightness` or `minimumRestoreBrightness` in the settings file.
- If the screen immediately wakes after turning off, increase `ignoreInputMs`.
- If using external monitors, disconnect them while validating the MVP behavior on the laptop panel.
- If the screen never blanks while you are away, check that `Blank the screen when I am away` is on, that the timeout for the current power source is not `Never`, and look for `Idle blackout postponed because ...` in the log.
- If Windows still turns the screen off by itself, another app may be forcing it; check the log and the Windows power plan for a display timeout shorter than one minute.
