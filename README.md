# Cutsy

A quiet, cute desktop companion for Windows 11. She walks along window tops, peeks, rests, and reacts when clicked. Click and drag her out of the way.

**Status: specification and asset bootstrap. No application executable exists yet.** The Windows 11 v0.1 milestone tracks implementation.

## Start here

- [Product and interaction specification](docs/PRODUCT.md)
- [Architecture and portability](docs/ARCHITECTURE.md)
- [Roadmap and GitHub issues](docs/ROADMAP.md)
- [Implementation handoff for GPT-6.1 Sol](docs/IMPLEMENTATION_HANDOFF.md)
- [Asset audit and known defects](docs/ASSET_AUDIT.md)
- [Pack format contract](docs/PACK_FORMAT.md)
- [Acceptance and release tests](docs/TESTING.md)

The repo includes 80 individual PNGs plus 22 source/reference PNGs. They are ordinary Git files; no additional download or Git LFS is required. The sprites can be loaded immediately, but their labels, crops, contact anchors, and animation compatibility require the tracked visual audit.

## Development

Planned stack: C#/.NET 10; platform-independent core; Windows layered-window host; Windows Forms settings/tray. See each directory README for responsibilities. Build/test/publish scripts and CI are implemented in C01, not supplied as nonworking placeholders here.

This repository and its artwork are private. No public license or redistribution grant is supplied.
