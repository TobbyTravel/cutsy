# Pack format v1 contract

Status: design contract to implement in C04. The legacy manifest is source provenance, not a loadable runtime pack. Add JSON Schema and runnable examples with the implementation.

## Contents and minimum fields

pack.json has schemaVersion=1, id, displayName, version, referenceHeightPx, defaultBehaviorId, sprites, sounds, and behaviorFile. Paths are relative to the pack root and use forward slashes. Each sprite defines a stable id, PNG path, source asset ID, facing (left/right/front/none), content bounds, named pixel-space anchors (feet/seat/hands/sideGrip as applicable), scale correction, body interaction-mask path, and optional bakedInProps. Each sound maps an ID to a WAV and gain multiplier.

behaviors.json has schemaVersion=1 and behaviors. Each behavior has id, family, selectionWeight, cooldownMs, requiredCapabilities, eligibility conditions, interrupt policy, ordered steps, and weighted next-behavior choices. Each step has an explicit type and parameters. All durations are milliseconds; coordinates within images are pixels with origin at top left and y down. Movement is measured in character heights per second to remain independent of display scale.

Engine primitives: playFrames (sprite ID plus duration per frame), hold, moveAlongSurface, jump, fall, attach, detach, react, and playSound. Movement primitives may reference a simultaneous animation sequence. attach specifies contact anchor and target surface/segment, not native handles. Surface geometry and gravity belong to core primitives; JSON selects parameters rather than embedding OS logic.

Eligibility is a small documented set of predicates: surface kind/space, facing, grounded/attached status, interaction event, quiet state, cooldown, and capability. No arbitrary code evaluation. Transitions target existing behavior IDs; default idle is mandatory. Unknown capabilities disable a behavior rather than attempting unsupported host calls.

## Execution and editing

Positive frame durations and bounded step durations are mandatory. Continuous idle/locomotion loops must be explicitly interruptible. User drag and quiet-state entry interrupt any recipe; lost support transfers to the engine's recovery path. Bounded transition counts prevent a zero-time transition loop freezing the app. Use a seeded random source for reproducible previews/tests.

Support deterministic preview and forced behavior selection. Reload parses, validates paths/IDs/anchors/audio, and swaps the entire pack atomically only on success. A failed reload keeps the previous validated pack and reports filename, JSON location/field, and reason.

Reject paths escaping the pack root, missing required images, invalid dimensions, nonexistent frame/transition IDs, nonpositive durations, invalid weights and unsafe numeric values. Optional sound failures warn and mute that cue. Set documented limits on image dimensions, decoded image memory, step counts, and file sizes during C04; oversized packs fail clearly without partially loading.

## Authoring guide to deliver in C06/C09

Include actual working examples for changing idle duration; adding a frame sequence; tuning a contact anchor; changing action selection weight/cooldown; attaching a sound; and adding a capability-specific window action. Validate all examples with Cutsy.PackTool.

## Coverage/provenance

Record each of the 80 source frame IDs as used, superseded by a corrected extraction, or excluded with a reason. Target at least 64 usable frames in shipped behaviors, without forcing poor animation. New derivatives retain their source image hash and crop/transformation metadata. Never rename a legacy asset and lose its mapping.

## Runtime validation and limits

See the JSON schemas in packs/schema and the self-contained packs/example for the wire format. Run the CLI with `dotnet run --project tools/Cutsy.PackTool -- validate packs/example`. The CLI and application share the same parser and reference checks. The CLI returns 1 for invalid packs and 0 for a valid pack, with separate warnings for unavailable optional sounds.

Use non-interlaced 8-bit PNG with grayscale, RGB, grayscale-alpha or RGBA pixels. Image files are limited to 8 MiB each and 2048×2048 pixels, with a 96 MiB aggregate decoded budget across images and masks. Each JSON file is limited to 2 MiB. Packs allow up to 160 sprites and 160 behaviors, with up to 64 steps per behavior and 64 frames per sequence. IDs are case-sensitive. Optional WAV cues use canonical mono 16-bit PCM, 8–48 kHz, at most two seconds, and up to 256 KiB each.

A step's `durationMs` overrides its animation sequence duration. If it is longer, the animation repeats for that step. `speed` is signed and measured in character heights per second for surface movement, or in character heights of horizontal travel for a jump. `height` sets the jump arc in character heights. An attach step selects a nearby edge by `target`; `anchor` names the matching sprite contact. Set `clip` to keep pixels on the exposed side. Steps with only an action such as `playSound` can take zero time, but each behavior must have at least one timed step. The engine limits transitions per simulation tick.

A clipped step can set `peekSlide` from 0 to 0.6 character heights. The renderer eases the sprite out from the hidden side of the current support and back over that step. Clipping stays aligned with the edge if the support moves. A floor step can use the work-area boundary to peek over the taskbar.
