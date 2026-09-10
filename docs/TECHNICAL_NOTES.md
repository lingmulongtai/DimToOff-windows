# Technical Notes

DimToOff is intentionally small for the MVP. The app centers state transitions in `DimToOffApplicationContext` and keeps platform calls behind services.

## MVP State Flow

1. `Idle`
2. Brightness event or poll reports brightness above threshold: queue a stable-brightness save. Comfortable stable brightness updates `lastUsableBrightness`; any above-threshold stable brightness updates the power-mode guard target.
3. Brightness reports at or below threshold: enter `PendingDisplayOff`.
4. After debounce, re-read current brightness.
5. If still at or below threshold: install input hooks, enter `DisplayOffByApp`, and send monitor power off.
6. Input detection after `ignoreInputMs`: enter `RestoringBrightness`.
7. Restore calculated brightness and remove hooks.
8. Enter `Cooldown`.
9. Return to `Idle`.

## Privacy

The low-level hooks do not marshal or store hook payload structures. They inspect only the Windows message kind needed to know that input happened.

Update checks are isolated in `UpdateCheckService`. They read GitHub Releases metadata, keep installer asset URLs available for a future installer-driven update flow, and do not send settings, logs, key contents, mouse coordinates, or telemetry.

Brightness preservation uses local Windows power notifications only. It registers for power setting changes and effective power mode changes, then briefly watches for brightness movement after those notifications. If brightness moves away from the last stable value, DimToOff restores that local value and suppresses auto-off for a short cooldown.

## Not Implemented In MVP

- Fullscreen suppression
- External monitor suppression
- Automatic installer download and installation
