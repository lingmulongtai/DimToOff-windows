# Microsoft Store Listing Draft

This draft targets Microsoft Store MSI/EXE distribution first. It avoids claiming that DimToOff physically powers off every panel in every mode, because the default mode is a blackout overlay that keeps Windows awake and unlocked.

## Product Name

DimToOff

## Category

Utilities & tools

## Short Description

Blank the laptop screen from the brightness keys or after an idle timeout, without ever sleeping the PC.

## Long Description

DimToOff is a lightweight Windows tray utility for laptops. When the built-in display brightness is lowered to the configured minimum threshold, DimToOff blanks the screen while keeping Windows awake, unlocked, and running. Audio playback, downloads, background tasks, and other desktop work can continue.

The default Blackout mode shows a fullscreen black overlay instead of putting the PC to sleep. Keyboard, mouse, or touchpad input restores the display, and DimToOff returns brightness to the last usable level it saved before the brightness reached the off threshold.

DimToOff is most useful on OLED and mini-LED displays. On OLED panels, black pixels emit little to no light. On mini-LED panels, local dimming may reduce visible output. On typical LCD panels, the backlight may remain on even when the screen is black, so DimToOff can hide the desktop but may not reduce backlight power use.

DimToOff can also take over the Windows screen timeout. With away blanking enabled, Windows no longer turns the screen off or sleeps on its own; DimToOff blanks the screen after the idle time chosen for AC and battery power, and the first key press or touchpad touch brings the desktop back with no sign-in screen in between. Downloads, builds, backups, and playback keep running the whole time. Nothing is written to the Windows power plan, and quitting DimToOff returns the Windows behavior immediately. Automatic blanking stands down while a fullscreen app is running or while another app asks Windows to keep the display on.

DimToOff can also preserve the user's last stable brightness when Windows or supported laptop performance modes try to adjust panel brightness automatically.

Privacy is intentionally simple: DimToOff does not include telemetry, analytics, advertising, or input-content logging. It only uses network access for optional GitHub release update checks. It does not record key contents, typed text, mouse coordinates, pointer paths, touchpad gestures, or input history. Low-level input hooks are used only while the app has blanked the display, and only to detect that input occurred so the display can be restored.

DimToOff runs without administrator privileges. Settings are stored locally in the current user's profile.

## Feature Bullets

- Blank the display when built-in laptop brightness reaches the configured threshold.
- Keep Windows awake, unlocked, and running in the default Blackout mode.
- Blank the display after a chosen idle time, with separate values for plugged in and on battery.
- Replace the Windows screen timeout and sleep timeout while the app runs, without editing the power plan.
- Keep working in the background: downloads, playback, and long jobs continue behind the black screen.
- Restore brightness after keyboard, mouse, or touchpad input.
- Avoid saving the minimum brightness itself as the restore brightness.
- Preserve brightness after supported power or performance mode changes.
- Configure threshold, debounce, cooldown, fade timing, and restore brightness.
- Optional Start with Windows support for the current user.
- No telemetry, no advertising, and no input-content logging. Optional update checks contact GitHub Releases.

## Search Keywords

brightness, display off, screen off, idle timeout, screen timeout, keep awake, OLED, mini LED, laptop, tray utility, blackout, monitor, dim, power, Windows, sleep prevention

## Privacy Policy URL

Use the repository-hosted policy after it is merged to the default branch:

```text
https://github.com/lingmulongtai/DimToOff-windows/blob/main/PRIVACY.md
```

## Support URL

```text
https://github.com/lingmulongtai/DimToOff-windows/issues
```

## Store Notes for Certification

DimToOff uses low-level keyboard and mouse hooks only while the app has blanked the display. The hook result is used only as a wake/restore signal. The app does not store key contents, mouse coordinates, pointer paths, touchpad gestures, or input history.

DimToOff detects that the user has stepped away by reading the timestamp of the last input through `GetLastInputInfo`. That call returns a timestamp only, with no key, button, or coordinate information. To hold back the Windows screen timeout while it runs, DimToOff calls `SetThreadExecutionState` periodically without `ES_CONTINUOUS`; it does not read or modify Windows power plans, and the behavior stops when the app exits.

DimToOff monitors laptop brightness through WMI to detect brightness changes and determine when to blank or restore. WMI data is used locally only.

DimToOff does not use telemetry, analytics, advertising, or remote configuration. Optional update checks contact GitHub Releases to see whether a newer installer is available.

The app may appear to keep running after the display turns black; this is expected. The default mode intentionally keeps Windows awake and unlocked rather than sleeping or locking the PC.

## Suggested Screenshots

- Tray icon and right-click quick panel.
- Settings window with the General section and the status card visible.
- Settings window showing the away blanking section with its idle timeouts.
- Settings window showing restore/timing controls.
- A simple before/after explanatory graphic showing brightness-down leading to Blackout mode.

Avoid screenshots that make the app look like it powers off all monitor hardware in every configuration. The default behavior is screen blanking with Windows still awake.
