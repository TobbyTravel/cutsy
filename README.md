# Cutsy

A quiet, cute desktop companion for Windows 11. She walks along window tops, peeks, rests, and reacts when clicked. Click and drag her out of the way.

**Status: Windows 11 v0.1 review build.** The Windows 11 v0.1 milestone tracks implementation and acceptance evidence.

## Start here

- [Product and interaction specification](docs/PRODUCT.md)
- [Architecture and portability](docs/ARCHITECTURE.md)
- [Roadmap and GitHub issues](docs/ROADMAP.md)
- [Implementation handoff for GPT-6.1 Sol](docs/IMPLEMENTATION_HANDOFF.md)
- [Asset audit and known defects](docs/ASSET_AUDIT.md)
- [Pack format contract](docs/PACK_FORMAT.md)
- [Acceptance and release tests](docs/TESTING.md)

The repo includes 80 individual PNGs plus 22 source/reference PNGs. They are ordinary Git files; no additional download or Git LFS is required. The runtime pack uses 71 reviewed derivatives with separate masks, editable recipes and eight original sound cues.

## Development

Install the Windows x64 .NET SDK version pinned in global.json and PowerShell 7.4 or later. From the repository root, run these commands.

~~~powershell
pwsh -File scripts/build.ps1
pwsh -File scripts/test.ps1
pwsh -File scripts/verify-bootstrap.ps1
pwsh -File scripts/publish.ps1
~~~

The publish command creates Cutsy.exe, the editable portable ZIP and SHA256SUMS.txt under dist. See [portable instructions](docs/PORTABLE.md), [pack editing](docs/PACK_EDITING.md) and [acceptance results](docs/V0.1_VERIFICATION.md).

The solution contains a portable core, a Windows Forms host, a pack CLI and xUnit core tests. Build output stays in each bin directory.

This repository and its artwork are private. No public license or redistribution grant is supplied.
