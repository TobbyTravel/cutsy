# Scripts

Use PowerShell 7.4 or later. Build and test use the SDK in global.json and the committed NuGet lock files.

- **build.ps1** restores and builds the solution. Defaults to Release. Pass -Configuration Debug for a debug build.
- **test.ps1** restores and runs the xUnit suite with the same configuration option.
- **verify-bootstrap.ps1** checks source hashes, provenance, and repository links.

- **publish.ps1** publishes the locked self-contained win-x64 EXE and stages the editable ZIP and checksums under dist.
- **derive-sprites.py** regenerates the reviewed runtime art and contact sheets with Pillow, NumPy and SciPy.
- **generate-sounds.py** regenerates the original WAV cues using Python's standard library.

- **import-walk-inbetweens.py** regenerates the four supplemental walk sprites, masks and pack metadata using Pillow, NumPy and SciPy.
