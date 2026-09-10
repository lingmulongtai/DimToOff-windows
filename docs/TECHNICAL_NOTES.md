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

## Away Blanking Takeover

`IdleWatchService` polls `GetLastInputInfo` once a second, and every 100 ms while the screen is blanked so that waking is
immediate. Only the timestamp of the last input is read.

`PowerKeepAliveService` suspends the Windows display and sleep timeouts. It calls `SetThreadExecutionState` every 25 seconds
with `ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED` and deliberately without `ES_CONTINUOUS`:

- A non-continuous call only resets the Windows idle timers, so nothing survives the process. If DimToOff exits or crashes,
  Windows returns to its own policy on the next timer tick, and no power plan value was ever written.
- Because DimToOff holds no continuous request of its own, the system-wide execution state read back through
  `CallNtPowerInformation(SystemExecutionState)` describes only other applications. That is how a media player asking to keep
  the display on can postpone an away blackout.

While the screen is blanked, the mode depends on `DisplayOffMode`. `Blackout` keeps the display request alive, since the
overlay is what the user sees as "screen off" and the panel has to stay powered. `MonitorPower` drops to system-only, so
Windows can really cut the panel while the PC stays awake.

`BlackoutTrigger` records what caused the blackout. Only `Brightness` writes a brightness value on wake; `Idle` and `Manual`
never lowered the brightness, so they leave it untouched.

## Privacy

The low-level hooks do not marshal or store hook payload structures. They inspect only the Windows message kind needed to know that input happened.

Update checks are isolated in `UpdateCheckService`. They read GitHub Releases metadata, keep installer asset URLs available for a future installer-driven update flow, and do not send settings, logs, key contents, mouse coordinates, or telemetry.

Brightness preservation uses local Windows power notifications only. It registers for power setting changes and effective power mode changes, then briefly watches for brightness movement after those notifications. If brightness moves away from the last stable value, DimToOff restores that local value and suppresses auto-off for a short cooldown.

Idle detection reads only the timestamp of the last input. `GetLastInputInfo` exposes no key, button, or coordinate data.

## Not Implemented

- Automatic installer download and installation
- Per-display control of external monitors
