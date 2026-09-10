# DimToOff v0.5.0

v0.5.0 gives DimToOff the second half of the idea it started from: the screen goes off, the PC keeps running. Until now that
only happened when you turned the brightness down yourself. Now it also happens when you simply step away, and Windows is
kept from turning the screen off or sleeping behind your back.

## Highlights

- **Away blanking.** DimToOff blanks the screen after a chosen idle time, 10 minutes plugged in and 5 minutes on battery by
  default. Separate timeouts per power source, or `Never` to leave that one to Windows.
- **The Windows screen timeout and sleep are suspended while it runs.** Downloads, builds, backups and playback keep going
  behind the black screen, and the first key press or touchpad touch brings the desktop back with no sign-in screen. Nothing
  is written to your power plan: the idle timers are only reset periodically, so quitting DimToOff hands the Windows
  behavior back immediately.
- **It stands down when it should.** Automatic blanking waits while a fullscreen app is in the foreground, or while another
  app asks Windows to keep the display on, such as a video player during playback. The whole takeover can be limited to the
  built-in panel by standing down while an external monitor is connected.
- **Rebuilt settings window.** Four sections instead of one long column of cards, a status card that says in one sentence
  what DimToOff is doing right now, and changes that apply as you make them instead of a Save button.
- **Refreshed tray experience.** The quick menu opens with a status line and a switch for away blanking, follows the Windows
  light or dark theme, and the tray icon tooltip says what the app is currently waiting for.
- **The brightness trigger can be turned off on its own,** for people who only want the away behavior.
- **Update notifications and brightness preservation** land in a release for the first time: an opt-out daily check of GitHub
  Releases, and restoring the last stable brightness when Windows or an OEM utility moves it after a power mode change.

## Fixes

- Waking from a blackout you asked for from the tray no longer writes a brightness value; that blackout never lowered the
  brightness in the first place.
- The tray icon handles are released instead of leaked.

## Upgrade Notes

- Away blanking is on by default after the update. Turn it off in Settings under `Away` if you would rather keep the Windows
  screen timeout.
- The two reserved settings `DisableWhileFullscreen` and `DisableWhenExternalMonitorConnected`, which were never
  implemented, are replaced by `IdleSkipWhileFullscreenApp` and `IdleSkipWhenExternalMonitorConnected`.

## Downloads

- `DimToOff-v0.5.0-setup.exe`
  - Recommended for most testers.
  - Installs per user under `%LOCALAPPDATA%\Programs\DimToOff`.
  - Does not require administrator privileges.
  - Not code-signed yet, so Windows SmartScreen may show an extra warning.
- `DimToOff-v0.5.0-win-x64.zip`
  - Standalone ZIP with the .NET runtime and Windows App SDK files included.
- `DimToOff-v0.5.0-win-x64-small.zip`
  - Smaller framework-dependent ZIP for PCs that already have the required .NET 8 Desktop Runtime and Windows App Runtime.

## Checksums

```text
1d45b18dab9405772ede5ff121f9d22efa1b6617360a70556e48742f4c15809c  DimToOff-v0.5.0-setup.exe
a86f11850a172914364a462682029822d930bdbb7eb05bd681765d2cf4b533df  DimToOff-v0.5.0-win-x64.zip
ba73e53d38bd50c2b7def83f834a278f040663f2233774f19175562ca654fa48  DimToOff-v0.5.0-win-x64-small.zip
```

## Known Limitations

- An away blackout never locks or suspends the session, so a Windows sign-in requirement on wake does not apply to it. Press
  Win+L before leaving if the PC has to be locked.
- While away blanking is on, the PC will not sleep on its own, including on battery. Set the on-battery timeout to `Never`
  to leave Windows in charge there.
- Fullscreen detection uses `SHQueryUserNotificationState`. A borderless fullscreen window is not always reported as
  fullscreen.
- Brightness detection depends on Windows WMI support for the built-in laptop display.
- External monitor per-display control is still outside the scope.
- `MonitorPower` mode can behave differently depending on laptop firmware, GPU drivers, and Modern Standby behavior. The
  default Blackout mode is recommended.
- Low-level input hooks only detect that input happened, and idle detection reads only the timestamp of the last input.
  DimToOff does not save key contents, mouse coordinates, telemetry, or network data.
- The installer and executables are not code-signed yet. Windows SmartScreen may warn on first run.
