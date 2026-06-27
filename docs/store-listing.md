# Microsoft Store Listing Draft

This draft targets Microsoft Store MSI/EXE distribution first. It avoids claiming that DimToOff physically powers off every panel in every mode, because the default mode is a blackout overlay that keeps Windows awake and unlocked.

## Product Name

DimToOff

## Category

Utilities & tools

## Short Description

Turn minimum laptop brightness into a display-blanking shortcut that keeps Windows awake.

## Long Description

DimToOff is a lightweight Windows tray utility for laptops. When the built-in display brightness is lowered to the configured minimum threshold, DimToOff blanks the screen while keeping Windows awake, unlocked, and running. Audio playback, downloads, background tasks, and other desktop work can continue.

The default Blackout mode shows a fullscreen black overlay instead of putting the PC to sleep. Keyboard, mouse, or touchpad input restores the display, and DimToOff returns brightness to the last usable level it saved before the brightness reached the off threshold.

DimToOff is most useful on OLED and mini-LED displays. On OLED panels, black pixels emit little to no light. On mini-LED panels, local dimming may reduce visible output. On typical LCD panels, the backlight may remain on even when the screen is black, so DimToOff can hide the desktop but may not reduce backlight power use.

Privacy is intentionally simple: DimToOff does not include telemetry, analytics, advertising, or network communication. It does not record key contents, typed text, mouse coordinates, pointer paths, touchpad gestures, or input history. Low-level input hooks are used only while the app has blanked the display, and only to detect that input occurred so the display can be restored.

DimToOff runs without administrator privileges. Settings are stored locally in the current user's profile.

## Feature Bullets

- Blank the display when built-in laptop brightness reaches the configured threshold.
- Keep Windows awake, unlocked, and running in the default Blackout mode.
- Restore brightness after keyboard, mouse, or touchpad input.
- Avoid saving the minimum brightness itself as the restore brightness.
- Configure threshold, debounce, cooldown, fade timing, and restore brightness.
- Optional Start with Windows support for the current user.
- No telemetry, no network communication, and no input-content logging.

## Search Keywords

brightness, display off, screen off, OLED, mini LED, laptop, tray utility, blackout, monitor, dim, power, Windows, sleep prevention

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

DimToOff monitors laptop brightness through WMI to detect brightness changes and determine when to blank or restore. WMI data is used locally only.

DimToOff does not use network communication, telemetry, analytics, advertising, or remote configuration.

The app may appear to keep running after the display turns black; this is expected. The default mode intentionally keeps Windows awake and unlocked rather than sleeping or locking the PC.

## Suggested Screenshots

- Tray icon and right-click quick panel.
- Settings window with the General and Blanking sections visible.
- Settings window showing restore/timing controls.
- A simple before/after explanatory graphic showing brightness-down leading to Blackout mode.

Avoid screenshots that make the app look like it powers off all monitor hardware in every configuration. The default behavior is screen blanking with Windows still awake.
