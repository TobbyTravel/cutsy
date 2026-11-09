# Verification and release gates

## Bootstrap verification

scripts/verify-bootstrap.ps1 verifies the 102 PNG hashes and provenance, expected sprite mapping count, tracked inventory, repository-relative asset references, local documentation links, and roadmap issue-file mapping. It does not validate runtime behavior. GitHub visibility/default branch and remote issues are separately checked during setup.

## Core and pack tests (C01/C04)

Use deterministic tests with a fake clock, seeded randomness, and desktop snapshots: transition eligibility, cooldowns, interruption, weighted selection, loop limits, step timing, missing references, invalid paths/numbers, unsupported capabilities, lost support, clipping geometry, and hot-reload rollback. Cover mixed-scale conversions at the host boundary. Validate all shipped recipes and documented examples.

## Windows manual matrix

Record Windows/build version, hardware, commit, pack hash, scenario, observed result, and evidence. Untested is distinct from passed.

- Character body click; transparency/decorations pass through to another application's button; focus retained while typing in that application.
- Click/drag threshold, grab offset, release on floor/perch/in midair, lost capture, drag across monitors, right-click and tray recovery.
- Supporting window move/resize/minimize/close/occlusion; invalid target; insufficient headroom; virtual desktop change.
- Monitor origins left/above primary, 100/150/200% mixed DPI, taskbar positions, disconnect/reconnect, sleep/resume.
- Fullscreen video, borderless fullscreen, presentation mode, lock, manual Gaming mode, foreground/running exclusions; hide/audio stop promptly and delayed stable resume.
- Volume/mute, cue cooldowns, no overlap, missing optional sound, audio device switch/disconnect.
- Invalid JSON, missing image, corrupt/oversized pack; useful message and last-good fallback.
- Stay here, 30-second post-drag grace, settings persistence, read-only portable folder, second-instance controls, optional startup toggle.
- Clean Windows 11 user/account without developer tools or installed .NET: EXE launch, native extraction, embedded pack, external editing/reload, portable ZIP.
- Visual audit of every used frame and sequence on contrasting backgrounds at final size.

Do not install games or change anti-cheat settings for tests. Test existing user-authorized environments only; report limitations. No universal anti-cheat compatibility claim.

## Performance and soak

On the development PC measure average total-machine CPU below 1% idle and 3% moving, working set below 150 MB. State sampling method and hardware; these are targets until measured. Run a two-hour soak with window changes, pauses and monitor/DPI events where available; compare memory, handles, and GDI objects over time. Separate stable caches from continuing growth.

## Release checklist

All C01–C09 acceptance criteria addressed; any exception explicitly documented. At least 64 usable frames targeted, all 80 accounted for. Build from a clean checkout with pinned dependencies. Ship EXE and portable ZIP with editable pack, instructions, checksums, source/build scripts and measured results. Record unsigned status and native extraction behavior; signing is a later distribution decision.
