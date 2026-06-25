# DimToOff

DimToOff is a Windows tray utility for laptops. When the built-in display brightness is lowered to the minimum threshold, the app turns only the display off without putting the PC to sleep. Keyboard, mouse, or touchpad input wakes the display, and DimToOff restores the last usable brightness it saved before the brightness reached the off threshold.

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

## How It Works

- Watches `WmiMonitorBrightnessEvent` in `root\wmi`.
- Treats brightness `<= 1%` as the MVP off trigger.
- Debounces the trigger for 800 ms, then shows a fullscreen black blanking layer by default. This keeps Windows unlocked and awake, so audio and normal background work continue.
- `WM_SYSCOMMAND / SC_MONITORPOWER` remains available as `DisplayOffMode: "MonitorPower"` in settings, but it is not the default because some laptops route it into lock or Modern Standby behavior.
- Installs low-level keyboard and mouse hooks only while the display is off by the app.
- Restores `lastUsableBrightness`, never the minimum brightness value itself.
- Remembers only a usable brightness level at or above `minimumRestoreBrightness`, so the last tiny step before display-off is not used as the restore target.
- Uses a cooldown after restore to prevent immediate re-off loops.
- Requires brightness to return to a usable level before automatic display-off can arm again after a failed or partial restore.
- Keeps the system awake while the display is off by requesting `ES_SYSTEM_REQUIRED`; the app turns off the display only, not the PC.
- Saves restore brightness only after the brightness has stayed stable for a short period, so holding the brightness-down key does not accidentally store a too-dark value.
- Fades the blackout overlay in instead of showing it abruptly.
- Does not store key contents, mouse coordinates, input history, telemetry, or network data.

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

For a local self-contained Windows x64 publish, place both executables in the same output folder:

```powershell
dotnet publish .\src\DimToOff\DimToOff.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -o .\publish\win-x64
dotnet publish .\src\DimToOff.Settings\DimToOff.Settings.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -o .\publish\win-x64
```

## Run

From the repository root:

```powershell
dotnet build
dotnet run --project .\src\DimToOff\DimToOff.csproj
```

The app appears in the Windows notification area as `DimToOff`. Build the full solution before running from source so the WinUI 3 settings app is available to the tray process.

From a published build, run:

```powershell
.\publish\win-x64\DimToOff.exe
```

## Use

1. Start DimToOff.
2. Lower laptop display brightness to 1% or lower.
3. After about 800 ms, the display turns off while the PC keeps running.
4. Press a key, move/click the mouse, or use the touchpad.
5. After Windows wakes the display, DimToOff restores the last brightness above the threshold.

Right-click the tray icon to open the WinUI 3 quick panel for:

- Enable/disable DimToOff
- Turn Off Display Now
- Restore Brightness Now
- Settings
- Start with Windows
- About
- Exit

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
  "DisableWhileFullscreen": false,
  "DisableWhenExternalMonitorConnected": false
}
```

The WinUI 3 settings screen can edit these values. `Start with Windows` uses the current user's Run key and does not require administrator privileges.

## Logs

Logs are written to:

```text
%LOCALAPPDATA%\DimToOff\logs\dimtooff.log
```

The log records app lifecycle events, brightness changes, WMI errors, display-off requests, input-detected facts, and restore attempts. It does not record key values, mouse coordinates, or input history.

## Known Limitations

- Some Windows laptops and external monitors do not expose WMI brightness events.
- DimToOff includes a polling fallback, but brightness behavior still depends on OEM firmware, GPU drivers, and Modern Standby behavior.
- External monitor per-display off/on control is outside the MVP.
- `SC_MONITORPOWER` may affect all connected displays.
- Low-level input hooks are used only to detect that input occurred; key contents and mouse coordinates are not recorded.
- Some touchpads or mice may generate tiny input immediately after display off. The MVP ignores input for 300 ms after turning the display off.
- Fullscreen-game detection is outside the MVP.

## Troubleshooting

- If the app does not build, confirm the .NET 8 SDK is installed with `dotnet --list-sdks`.
- If brightness changes are not detected, check `%LOCALAPPDATA%\DimToOff\logs\dimtooff.log` for WMI errors.
- If the display turns off but brightness does not restore, try increasing `defaultRestoreBrightness` or `minimumRestoreBrightness` in the settings file.
- If the screen immediately wakes after turning off, increase `ignoreInputMs`.
- If using external monitors, disconnect them while validating the MVP behavior on the laptop panel.
