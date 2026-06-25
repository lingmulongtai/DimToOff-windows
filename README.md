# DimToOff

DimToOff is a Windows tray utility for laptops. When the built-in display brightness is lowered to the minimum threshold, the app turns only the display off without putting the PC to sleep. Keyboard, mouse, or touchpad input wakes the display, and DimToOff restores the last usable brightness it saved before the brightness reached the off threshold.

## Supported Environment

- Windows 10 or Windows 11
- Laptop built-in display with WMI brightness support
- .NET 8 SDK for building
- No administrator privileges required

## How It Works

- Watches `WmiMonitorBrightnessEvent` in `root\wmi`.
- Treats brightness `<= 1%` as the MVP off trigger.
- Debounces the trigger for 800 ms, then sends `WM_SYSCOMMAND / SC_MONITORPOWER` to turn the display off.
- Installs low-level keyboard and mouse hooks only while the display is off by the app.
- Restores `lastUsableBrightness`, never the minimum brightness value itself.
- Remembers only a usable brightness level at or above `minimumRestoreBrightness`, so the last tiny step before display-off is not used as the restore target.
- Uses a cooldown after restore to prevent immediate re-off loops.
- Requires brightness to return to a usable level before automatic display-off can arm again after a failed or partial restore.
- Keeps the system awake while the display is off by requesting `ES_SYSTEM_REQUIRED`; the app turns off the display only, not the PC.
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

## Run

From the repository root:

```powershell
dotnet run --project .\src\DimToOff\DimToOff.csproj
```

The app appears in the Windows notification area as `DimToOff`.

## Use

1. Start DimToOff.
2. Lower laptop display brightness to 1% or lower.
3. After about 800 ms, the display turns off while the PC keeps running.
4. Press a key, move/click the mouse, or use the touchpad.
5. After Windows wakes the display, DimToOff restores the last brightness above the threshold.

Right-click the tray icon for:

- Enable/disable DimToOff
- Turn Off Display Now
- Restore Brightness Now
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
  "enabled": true,
  "offThreshold": 1,
  "debounceMs": 800,
  "cooldownMs": 1500,
  "ignoreInputMs": 300,
  "restoreMode": "LastUsableWithMinimum",
  "minimumRestoreBrightness": 30,
  "defaultRestoreBrightness": 50
}
```

The graphical settings screen and start-with-Windows registration are intentionally deferred in this MVP.

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
