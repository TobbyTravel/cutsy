# Cutsy agent instructions

- Before application work, read [product](docs/PRODUCT.md), [architecture](docs/ARCHITECTURE.md), and the current [roadmap](docs/ROADMAP.md); inspect linked live issues and the worktree.
- Before asset or recipe changes, read [pack format](docs/PACK_FORMAT.md) and [asset audit](docs/ASSET_AUDIT.md). Verify poses visually: the legacy manifest is not an authoritative animation specification.
- Before completing a milestone or publishing artifacts, follow [testing](docs/TESTING.md) and record measured results and remaining limitations.
- Keep Cutsy.Core free of Windows, UI, filesystem, and audio-output dependencies. Desktop APIs belong in Cutsy.Windows.
- Preserve source artwork and source hashes. Record derivations, corrected crops, anchors, masks, and exclusions in provenance/audit records.
- Preserve clickable body, transparent-area click-through, drag repositioning, and keyboard focus. Fullscreen hiding and quiet audio are release requirements.
- Work in focused branches and PRs. Update the issue with implementation and verification evidence; compilation alone does not establish GUI behavior.
- Use the existing checkout at C:/Users/Tobby/claude/cutsy. Preserve unrelated changes. This is a standalone project; companion-service secrets and infrastructure are outside its scope.
- Keep repository documentation as the source of truth. A new agent must not need the originating chat or Downloads folder.
