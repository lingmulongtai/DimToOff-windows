# DimToOff Privacy Policy

Last updated: 2026-06-28

DimToOff is a Windows tray utility that turns the display dark or requests display power-off when the laptop brightness reaches a configured threshold. This document explains what the app does and does not collect.

## Summary

- DimToOff does not send data over the network.
- DimToOff does not include telemetry, analytics, advertising, or crash-report upload code.
- DimToOff does not record key contents.
- DimToOff does not record mouse coordinates, pointer paths, touchpad gestures, or input history.
- DimToOff stores settings locally on the current Windows user profile.
- DimToOff stores local diagnostic logs on the current Windows user profile.

## Network Communication

DimToOff does not make network requests and does not transmit data to the developer, Microsoft, or any third party.

The app can open the project GitHub page only when the user explicitly clicks the GitHub button. That action opens the user's default browser.

## Telemetry and Analytics

DimToOff does not implement telemetry, analytics, advertising identifiers, remote configuration, or usage tracking.

## Brightness Monitoring

DimToOff monitors the built-in display brightness through Windows WMI, including `WmiMonitorBrightnessEvent` under `root\wmi`. It uses that information only to decide when to blank the display and what brightness value is safe to restore later.

The app may also poll the current brightness as a fallback when WMI events are unavailable or delayed.

## Keyboard, Mouse, and Touchpad Input

DimToOff uses low-level keyboard and mouse hooks only while the display is blanked by the app. The hooks are used only to detect that user input occurred so the app can restore the display and brightness.

DimToOff does not store:

- key values,
- typed text,
- shortcuts,
- mouse coordinates,
- pointer movement paths,
- touchpad gesture contents,
- input history.

DimToOff logs only the fact that restore-related input was detected when needed for app lifecycle diagnostics.

## Local Settings

DimToOff stores settings here:

```text
%APPDATA%\DimToOff\settings.json
```

The settings file can include values such as:

- enabled/disabled state,
- brightness threshold,
- debounce and cooldown timing,
- fade timing,
- restore brightness preferences,
- whether DimToOff starts with Windows.

## Local Logs

DimToOff stores diagnostic logs here:

```text
%LOCALAPPDATA%\DimToOff\logs\dimtooff.log
```

Logs can include:

- app start/stop events,
- brightness percentage changes,
- WMI errors,
- display blank/restore lifecycle events,
- restore failures,
- startup registration changes.

Logs do not include key contents, mouse coordinates, input history, telemetry identifiers, or network identifiers.

## Start With Windows

If the user enables Start with Windows, DimToOff writes a current-user registry value here:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DimToOff
```

This does not require administrator privileges. Disabling Start with Windows removes that registry value.

## Removing Local Data

Uninstalling DimToOff removes the installed application files. User settings and logs may remain so that a reinstall can preserve preferences and diagnostics.

To remove local data manually, close DimToOff and delete:

```text
%APPDATA%\DimToOff
%LOCALAPPDATA%\DimToOff
```

To remove the Start with Windows registration manually, delete:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DimToOff
```

## Contact

Project page:

```text
https://github.com/lingmulongtai/DimToOff-windows
```
