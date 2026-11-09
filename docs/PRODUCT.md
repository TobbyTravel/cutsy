# Windows 11 v0.1 product contract

## Outcome and scope

Cutsy is one offline mascot, started by double-clicking a portable EXE. Initial platform: Windows 11 x64. The UI is a transparent mascot plus tray/settings; no main dashboard, account, telemetry, network service, automatic updater, or runtime AI is required.

Ship one character with editable JSON behavior recipes, quiet sounds, multiple-monitor support, window interactions, and a self-contained release. Linux/macOS ports, multiple pets, and a visual recipe editor are deferred.

## Defaults

| Setting | Default |
| --- | --- |
| Character height | 160 logical pixels, configurable 80–320 |
| Activity | Calm: 46% idle/rest, 30% movement, 19% edge interaction, 5% reactions |
| Sprite playback | Usually 4–8 FPS, explicit frame durations allowed |
| Position updates | 30 Hz while moving; redraw only when necessary |
| Volume | 80%; no startup sound |
| Ambient sound cooldown | 90 seconds globally |
| Click sound cooldown | 5 seconds |
| Fullscreen hiding | Enabled, all monitors |
| Startup with Windows | Off, opt-in |
| Instances | One per user session |

Weights are selection weights among eligible behaviors, not guaranteed wall-clock percentages. Cooldowns and long holds prevent repetition. On the same monitor, walking tends toward the active work area while leaving some space around it. Exposed edges of the active window get priority when reachable. Cutsy sometimes peeks over the taskbar when on the floor.

## Click and drag

The character body is always clickable; transparent surroundings and decorative particles pass clicks to unrelated apps. Preserve keyboard focus on mascot click/drag. Explicitly opened settings can take focus.

Left button movement below four logical pixels is a click. At four pixels or more, begin drag and capture the mouse; retain the original grab offset. Click selects a short happy/heart reaction; pickup selects a suitable surprised pose. Release within 16 logical pixels of a valid support surface snaps to it; otherwise fall onto the first valid surface below. Handle lost capture, display removal, and quiet-mode interruption safely.

After drag, suppress autonomous travel away from the drop location for 30 seconds. Stay here restricts movement to 1.5 character widths around that anchor until disabled, but still recovers if support disappears. Right-click opens Pause, Stay here, Mute, Size, Settings, Quit. Tray offers recovery even if the mascot is offscreen.

## Behavior library

Implement idle stand/smile/glance/blush/curious/thinking/sulk/yawn; separate directional walks and turns; a rare short run; jump anticipation/takeoff/airborne/landing/recovery; window-top walk/sit/lean/climb; hand hang/swing; left/right peeks and clipped behind-edge peeks; phone, boba, laptop, sweeping, parcel, plush and affection activities; seated and floor naps and a compatible wake transition.

Single-pose actions may use restrained transform motion. Never present opposite-facing or incompatible poses as a walk cycle. Props remain separate activities, connected through a compatible neutral pose.

## Surface behavior

Use exposed edges of visible windows and monitor work areas. Skip edges without sufficient width/headroom. Avoid placing the mascot over window caption controls when another eligible segment exists. Follow a moving support at its contact point; when minimized, closed, hidden, or obscured, recover/fall to a valid support. Exclude shell, mascot, tool, hidden, minimized, and cloaked windows. A maximized window generally has no room for a top perch.

Render behind-edge peeks using explicit clipping. Ordinary foreground changes must not make the character appear to stand on an invisible surface. Desktop/taskbar work-area floor is always the fallback. Treat virtual desktop changes as a surface invalidation, not a reason to move other applications.

## Quiet mode and audio

Hide overlay, cancel drag safely, stop audio, and suspend animation during fullscreen/presentation, lock/inactive session, manual Gaming mode, or application exclusions. Combine foreground bounds with notification-state signals. Check every 500 ms and on foreground changes; resume after 3 seconds continuously eligible.

Exclusions match executable names and support pause while foreground or while running. A quiet state hides on all monitors by default. No graphics injection, in-process hooks, game-memory reading, driver, or administrator access. This design is not a universal anti-cheat guarantee.

Eight original nonverbal WAV effects: pop, hop, landing puff, happy chirp, affectionate chime, surprise squeak, sleepy tone, completion sparkle. Use soft attack/release, controlled peaks, no overlapping cues, no continuous footsteps/loops. Missing devices/sounds disable sound gracefully. Mute and quiet mode stop playback immediately.

## Release behavior

Embed the default pack in a self-contained EXE. Also provide a ZIP with editable pack, portable.flag, and instructions. Portable mode stores settings/overrides beside the executable; normal mode uses local application data. Read-only portable storage gives a clear warning and retains session changes in memory; it must not silently write into unrelated directories.

Offer pack reload, behavior preview, restore defaults, recover position, and bounded diagnostics. Invalid edits retain the last working pack. Single-instance relaunch opens existing controls.
