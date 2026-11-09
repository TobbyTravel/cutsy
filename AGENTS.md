# Cutsy agent instructions

- Before changing app behavior, read [product](docs/PRODUCT.md), [architecture](docs/ARCHITECTURE.md), and [roadmap](docs/ROADMAP.md). Check the live issue and working tree.
- For artwork or recipes, read [pack format](docs/PACK_FORMAT.md) and [asset audit](docs/ASSET_AUDIT.md). Verify poses visually and record the source hashes, crops, anchors, masks, and exclusions for derivatives.
- Keep Cutsy.Core portable. Windows, UI, filesystem, and audio-output code belongs in the host or tooling.
- Preserve body clicks, transparent-area click-through, drag, and keyboard focus. Fullscreen hiding and quiet audio are release requirements.
- Follow [testing](docs/TESTING.md) for desktop changes and releases. Put observed results and untested cases in the PR and issue.
- Work in focused branches and PRs. Preserve unrelated changes and keep project context in repository docs. The [handoff](docs/IMPLEMENTATION_HANDOFF.md) maps common tasks to the relevant files.
