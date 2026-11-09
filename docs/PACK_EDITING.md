# Editing the Cutsy pack

The editable pack has pack.json, behaviors.json, art, masks and optional sounds. Use the portable ZIP's pack directory or the local pack folder shown in Cutsy Settings. Keep the embedded fallback available by removing or renaming a broken override. The tray's Settings window can reload a saved edit and preview a recipe.

- **Hold longer.** In behaviors.json, find idle and change its stand step's durationMs from 5100 to 6500.
- **Build a sequence.** Find shy-glance. In its playFrames step, append an entry with spriteId set to smile and durationMs set to 300.
- **Tune the pivot.** In pack.json, find edge-sit and adjust anchors.seat.y by one pixel. Preview window-sit to check where it contacts the edge.
- **Tune scheduling.** Change the weight of phone from 1 to 2 or change cooldownMs from 26000 to 36000. Weight is relative to other eligible idle behaviors.
- **Make an edge action.** Copy top-peek, choose a new ID, then change its hold duration. Keep windowEdges in requiredCapabilities, windowTop in eligibility, and the hands anchor for the clipped peek.
- **Add a cue.** Declare a sound ID, PCM WAV path and gain in pack.json, then add soundId to a timed step that should trigger it.

From the source checkout, validate a pack directory with `dotnet run --project tools/Cutsy.PackTool -- validate <pack-directory>`. See the JSON schemas in packs/schema for editor validation.
