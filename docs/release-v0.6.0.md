# DimToOff v0.6.0

v0.6.0 is about the two things that got in the way of using v0.5.0 every day: changing the away timeout took a trip through
the settings window, and on OLED laptops the vendor's own burn-in saver would come up behind the black screen and light the
panel straight back up.

## Highlights

- **The away timeout is on the tray menu.** `Blank after` opens a second page of the quick panel with the eight timeouts
  people actually reach for, from `Never` to `1 hour`. It edits the timeout for the power source you are on, which is the one
  the menu was already reporting. The full list of values is still in Settings.
- **Screen savers are kept off the black screen.** A saver that starts behind a DimToOff blackout undoes the whole point of
  it. DimToOff now refuses the Windows screen saver while the overlay is up, switches it off for as long as the guard runs,
  and puts the black screen back on top of anything else that jumps in front.
- **OEM burn-in savers are covered too.** The savers that ship with OLED laptops, such as ASUS OLED Care, are not screen
  savers as far as Windows is concerned, so no Windows setting keeps them away. DimToOff watches for a fullscreen window from
  another process instead, and takes the screen back within a second.
- **Your OLED protection is left alone the rest of the time.** By default the guard only covers the time the screen is
  actually black, which on an OLED panel is already the best case for the panel. `The whole time DimToOff runs` is there for
  people who want savers held off for the entire session. Nothing is held after the app exits, including the Windows screen
  saver setting, which is read before it is changed and written back afterwards.

## Upgrade Notes

- The screen saver guard is on by default, scoped to the blacked-out screen. Settings, under `Screen off` -> `Screen savers`.
- The guard needs the `Blackout` blanking method to put the black screen back in front of a saver. With `MonitorPower` only
  the Windows screen saver half applies.
- A burn-in saver that draws in a window smaller than a whole screen is outside what the guard can recognize. If one still
  gets through, `%LOCALAPPDATA%\DimToOff\logs\dimtooff.log` shows a `Taking the screen back from ...` line naming the process
  that took the front, or no line at all when the saver is drawing in a way the guard cannot see.
- Three new settings: `ScreenSaverGuardEnabled`, `ScreenSaverGuardScope` and `ScreenSaverGuardSuspendsWindowsScreenSaver`.
  Existing settings files pick up the defaults on first run.

## Downloads

- `DimToOff-v0.6.0-setup.exe`
  - Recommended for most testers.
  - Installs per user under `%LOCALAPPDATA%\Programs\DimToOff`.
  - Does not require administrator privileges.
  - Not code-signed yet, so Windows SmartScreen may show an extra warning.
- `DimToOff-v0.6.0-win-x64.zip`
  - Standalone ZIP with the .NET runtime and Windows App SDK files included.
- `DimToOff-v0.6.0-win-x64-small.zip`
  - Smaller framework-dependent ZIP for PCs that already have the required .NET 8 Desktop Runtime and Windows App Runtime.

## Known Limitations

- The screen saver guard recognizes a saver by its window covering a whole screen from another process. A saver that draws
  smaller than that, or that bypasses the desktop window manager, is outside what it can see.
- `ScreenSaverGuardScope: "WhileRunning"` holds the Windows screen saver off even while you are using the PC. On an OLED
  panel that is a trade against burn-in, which is why `WhileBlanked` is the default.
- The tray timeout page edits the power source you are on. Changing the other one still means opening Settings.
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
