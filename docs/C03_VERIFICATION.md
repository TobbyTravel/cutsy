# C03 shell verification

Windows 11 Pro build 22631, Ryzen 9 7900X, one 2560×1440 display at 96 DPI. Proof sprite SHA-256 143EFFB6524DD717B35C14273A28DA5DF858B46298EA8369F4D74F01A9D603CC. The proof uses the front standing pose in sheet 05 row 1 column 1. See packs/cutsy/proof/provenance.json for the source hash and derivative.

- Launched the Release executable on the interactive desktop. The window measured 110×160 physical pixels. Win32 WindowFromPoint resolved the center to Cutsy and its zero-alpha corner to another process.
- Tobby observed the heart on body click and confirmed typing remained in the previously focused app.
- Initial drag failed because the separate async key-state call returned zero during a held click. After switching to delivered mouse messages, Tobby confirmed pointer drag and release to the bottom work-area floor.
- The heart is a separate transparent input-pass-through window. Direct click-through on the displayed heart, exact threshold/grab offset, focus after drag, tray recovery, capture-loss interruption and mixed-DPI/negative-origin interaction still need interactive checks.

The visual cursor issue was corrected in commit d22b16f. Tobby confirmed the normal pointer over the mascot.
