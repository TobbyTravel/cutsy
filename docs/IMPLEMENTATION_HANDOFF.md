# Contributor handoff

Start with [AGENTS.md](../AGENTS.md) and the live issue you are working on. The Windows 11 v0.1 prerelease is available through [GitHub Releases](https://github.com/TobbyTravel/cutsy/releases). The [roadmap](ROADMAP.md) tracks implementation and remaining qualification.

## Choose the entry point

| Work | Start here |
| --- | --- |
| Desktop behavior, focus, or window tracking | [Product contract](PRODUCT.md) and [architecture](ARCHITECTURE.md). Portable scheduling is in src/Cutsy.Core. Native overlay, window observation, audio, and settings are in src/Cutsy.Windows. |
| Artwork, animation, or editable packs | [Asset audit](ASSET_AUDIT.md), [pack format](PACK_FORMAT.md), and [editing guide](PACK_EDITING.md). The shipped pack is under packs/cutsy/runtime. Cutsy.PackTool validates the pack without launching the desktop app. |
| Verification or packaging | [Testing and release gates](TESTING.md), [baseline results](V0.1_VERIFICATION.md), and [walking and window results](WALK_AND_WINDOWS_VERIFICATION.md). |

## Finish the change

1. Read the relevant contract and live issue. Check the working tree and preserve unrelated changes.
2. Run the relevant build, test, and pack-validation commands from the [scripts guide](../scripts/README.md). For desktop changes, exercise the user-visible behavior on Windows and distinguish observed results from remaining checks.
3. Record the commit, commands, and evidence in the PR and linked issue. Release publication needs explicit authorization.
