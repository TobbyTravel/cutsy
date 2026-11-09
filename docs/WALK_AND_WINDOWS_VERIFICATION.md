# Walking and window behavior verification

Windows 11 Pro build 22631, single 2560×1440 display at 96 DPI. This follow-up uses the [v0.1 review build](V0.1_VERIFICATION.md) as its base.

- A live WinForms fixture ran in a separate Windows process. The desktop tracker read its real exposed window top and the engine snapped to (2125, 1002) from a release eight pixels above it. The probe refreshed the real desktop snapshot after each change. Moving the fixture by +35 px horizontally and +45 px vertically moved the engine contact by those amounts. Closing it triggered a fall to the work-area floor at y=1392.
- Nine core test cases passed. The two new cases verify preferred window selection and that animated peek clipping moves with the window and clears when support disappears.
- The default pack validated with 75 sprites, 54 recipes and eight sound cues. All 102 immutable source PNGs still match their recorded hashes. The four new walk poses are stored as separate generated art with measured feet contacts. Both directional sequences were reviewed in order at 160 px against a shared floor line.
- Fresh settings and Restore defaults use 80% volume. Explicit saved settings retain their previous volume. Existing portable packs retain their edits and need the updated pack from the rebuilt ZIP to use the new recipes.
- The fixture proves native support discovery and contact updates. The prior mixed-DPI, live input, fullscreen and soak limitations in the v0.1 record still apply.
