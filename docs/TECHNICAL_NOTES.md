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

## Screen Saver Guard

A screen saver that starts while DimToOff owns the screen lights the panel back up, which cancels the blackout in the only
way that matters. `ScreenSaverGuardService` handles it in two halves, because the two kinds of saver are stopped by
completely different means.

The Windows screen saver is a documented mechanism, so it is refused where Windows offers the refusal. The blackout overlay
is the foreground window while the screen is black, which makes it the window Windows sends `WM_SYSCOMMAND / SC_SCREENSAVE`
to; the overlay returns zero and no screen saver starts. In addition, `SPI_SETSCREENSAVEACTIVE` switches the screen saver off
for as long as the guard is on. The previous value is read with `SPI_GETSCREENSAVEACTIVE` first and written back when the
guard stands down, so a user who does run a screen saver keeps it.

The OEM burn-in savers that ship with OLED laptops, such as ASUS OLED Care, are not screen savers as far as Windows is
concerned. They are ordinary always-on-top windows started by a vendor process on its own idle timer, so nothing in the
screen saver API reaches them and `ES_DISPLAY_REQUIRED` does not either, since that resets the display idle timer rather than
the vendor's. What is left is what the user sees: a window covering the screen that is not ours. Once a second the guard
compares `GetForegroundWindow` against its own process and `GetWindowRect` against the monitor bounds, and reports anything
full-screen that belongs to somebody else. `ScreenTaken` then puts the blackout overlay back at the top of the topmost band,
which hides the saver and, for savers that watch for it, makes them exit.

Both halves are scoped so that nothing outlives the app. The sweep only runs while the black overlay is up, since that is
the only time there is a window of ours to put back in front and the only time a full-screen window from another process is
unwanted by definition. `ScreenSaverGuardScope` decides whether the Windows screen saver stays suspended for the whole
session or only for the blackout; `WhileBlanked` is the default, because on an OLED panel the vendor protection is worth
having the rest of the time.

`MonitorPower` blanking gets the Windows screen saver half only. With the panel powered off there is no overlay to reclaim
the front with, and every foreground window looks full-screen, so the sweep would report the desktop itself.

## Privacy

The low-level hooks do not marshal or store hook payload structures. They inspect only the Windows message kind needed to know that input happened.

Update checks are isolated in `UpdateCheckService`. They read GitHub Releases metadata, keep installer asset URLs available for a future installer-driven update flow, and do not send settings, logs, key contents, mouse coordinates, or telemetry.

Brightness preservation uses local Windows power notifications only. It registers for power setting changes and effective power mode changes, then briefly watches for brightness movement after those notifications. If brightness moves away from the last stable value, DimToOff restores that local value and suppresses auto-off for a short cooldown.

Idle detection reads only the timestamp of the last input. `GetLastInputInfo` exposes no key, button, or coordinate data.

The screen saver guard reads the class name and owning process name of the window in front, and only while the screen is
blanked. It logs that name so the user can tell what interrupted the blackout. Window contents are never read.

## Not Implemented

- Automatic installer download and installation
- Per-display control of external monitors
