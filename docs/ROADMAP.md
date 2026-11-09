# Roadmap

Status: Windows 11 v0.1 implementation and packaging are available in focused review branches. See [acceptance results](V0.1_VERIFICATION.md) for verified behavior and open checks. [Windows 11 v0.1 milestone](https://github.com/TobbyTravel/cutsy/milestone/1) contains the umbrella and C01–C10. K01 is deferred and outside the milestone.

| Task | GitHub issue | Depends on |
| --- | --- | --- |
| C00 | [Deliver Windows 11 v0.1](https://github.com/TobbyTravel/cutsy/issues/1) | [C01](https://github.com/TobbyTravel/cutsy/issues/2), [C02](https://github.com/TobbyTravel/cutsy/issues/3), [C03](https://github.com/TobbyTravel/cutsy/issues/4), [C04](https://github.com/TobbyTravel/cutsy/issues/5), [C05](https://github.com/TobbyTravel/cutsy/issues/6), [C06](https://github.com/TobbyTravel/cutsy/issues/7), [C07](https://github.com/TobbyTravel/cutsy/issues/8), [C08](https://github.com/TobbyTravel/cutsy/issues/9), [C09](https://github.com/TobbyTravel/cutsy/issues/10), [C10](https://github.com/TobbyTravel/cutsy/issues/11) |
| C01 | [Establish the solution, build, and CI](https://github.com/TobbyTravel/cutsy/issues/2) | None |
| C02 | [Audit and normalize the mascot pack](https://github.com/TobbyTravel/cutsy/issues/3) | [C01](https://github.com/TobbyTravel/cutsy/issues/2) |
| C03 | [Implement the interactive desktop shell](https://github.com/TobbyTravel/cutsy/issues/4) | [C01](https://github.com/TobbyTravel/cutsy/issues/2) |
| C04 | [Implement the JSON pack loader and behavior engine](https://github.com/TobbyTravel/cutsy/issues/5) | [C01](https://github.com/TobbyTravel/cutsy/issues/2) |
| C05 | [Add window surfaces and edge interactions](https://github.com/TobbyTravel/cutsy/issues/6) | [C03](https://github.com/TobbyTravel/cutsy/issues/4), [C04](https://github.com/TobbyTravel/cutsy/issues/5) |
| C06 | [Author the complete default behavior library](https://github.com/TobbyTravel/cutsy/issues/7) | [C02](https://github.com/TobbyTravel/cutsy/issues/3), [C04](https://github.com/TobbyTravel/cutsy/issues/5), [C05](https://github.com/TobbyTravel/cutsy/issues/6) |
| C07 | [Implement fullscreen and gaming quiet mode](https://github.com/TobbyTravel/cutsy/issues/8) | [C03](https://github.com/TobbyTravel/cutsy/issues/4), [C04](https://github.com/TobbyTravel/cutsy/issues/5) |
| C08 | [Create and integrate quiet sound effects](https://github.com/TobbyTravel/cutsy/issues/9) | [C04](https://github.com/TobbyTravel/cutsy/issues/5), [C07](https://github.com/TobbyTravel/cutsy/issues/8) |
| C09 | [Complete settings and pack editing workflow](https://github.com/TobbyTravel/cutsy/issues/10) | [C03](https://github.com/TobbyTravel/cutsy/issues/4), [C04](https://github.com/TobbyTravel/cutsy/issues/5), [C05](https://github.com/TobbyTravel/cutsy/issues/6), [C06](https://github.com/TobbyTravel/cutsy/issues/7), [C07](https://github.com/TobbyTravel/cutsy/issues/8), [C08](https://github.com/TobbyTravel/cutsy/issues/9) |
| C10 | [Package and qualify the portable release](https://github.com/TobbyTravel/cutsy/issues/11) | [C01](https://github.com/TobbyTravel/cutsy/issues/2), [C02](https://github.com/TobbyTravel/cutsy/issues/3), [C03](https://github.com/TobbyTravel/cutsy/issues/4), [C04](https://github.com/TobbyTravel/cutsy/issues/5), [C05](https://github.com/TobbyTravel/cutsy/issues/6), [C06](https://github.com/TobbyTravel/cutsy/issues/7), [C07](https://github.com/TobbyTravel/cutsy/issues/8), [C08](https://github.com/TobbyTravel/cutsy/issues/9), [C09](https://github.com/TobbyTravel/cutsy/issues/10) |
| K01 | [Investigate KDE Wayland and KWin integration](https://github.com/TobbyTravel/cutsy/issues/12) | Deferred |

## Implementation order

1. C01 creates buildable projects and CI.
2. C03 proves the native transparent window and click/drag/focus behavior with one reviewed sprite. C02 asset audit and C04 engine work can proceed independently after C01.
3. C05 combines the shell and core with real window surfaces.
4. C06 authors the complete corrected content library. C07 establishes quiet-mode behavior; C08 then integrates audio.
5. C09 completes settings and editing; C10 qualifies and packages the release.

The umbrella tracks all acceptance criteria. Close implementation issues only with evidence. C10 cannot pass merely because all code has compiled. Testing exceptions must be explicit, and untested claims remain unverified.

## Working agreements

Use focused feature branches and PRs. GitHub issues are the live progress record; docs/issues contains initial specifications. Product and architecture documents are authoritative for shared behavior. Keep dependencies in this roadmap and linked issue bodies consistent when changing the order.

A basic shell can use a single audited sprite before all 80 are corrected. Final content coverage cannot bypass C02. Runtime art repairs must preserve source hashes and provenance, using new derivative records rather than overwriting original sources.

## After v0.1

[Smoother directional walking](https://github.com/TobbyTravel/cutsy/issues/23) tracks the missing in-between art, asset handling, import and editable pack verification.
