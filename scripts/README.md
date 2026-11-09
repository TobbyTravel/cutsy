# Scripts

Use PowerShell 7.4 or later. Build and test use the SDK in global.json and the committed NuGet lock files.

- **build.ps1** restores and builds the solution. Defaults to Release. Pass -Configuration Debug for a debug build.
- **test.ps1** restores and runs the xUnit suite with the same configuration option.
- **verify-bootstrap.ps1** checks source hashes, provenance, and repository links.
