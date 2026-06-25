# Technical Notes

DimToOff is intentionally small for the MVP. The app centers state transitions in `DimToOffApplicationContext` and keeps platform calls behind services.

## MVP State Flow

1. `Idle`
2. Brightness event or poll reports brightness above threshold: update `lastUsableBrightness`.
3. Brightness reports at or below threshold: enter `PendingDisplayOff`.
4. After debounce, re-read current brightness.
5. If still at or below threshold: install input hooks, enter `DisplayOffByApp`, and send monitor power off.
6. Input detection after `ignoreInputMs`: enter `RestoringBrightness`.
7. Restore calculated brightness and remove hooks.
8. Enter `Cooldown`.
9. Return to `Idle`.

## Privacy

The low-level hooks do not marshal or store hook payload structures. They inspect only the Windows message kind needed to know that input happened.

## Not Implemented In MVP

- Full settings UI
- Startup shortcut or Run key registration
- Fullscreen suppression
- External monitor suppression
- Display power setting notifications
