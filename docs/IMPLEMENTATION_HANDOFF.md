# Implementation handoff

Use a fresh **GPT-6.1 Sol** session with this repository attached. No prior conversation or files outside this checkout are required. The Windows 11 v0.1 milestone and [roadmap](ROADMAP.md) are the work queue.

## Copyable session prompt

Implement Cutsy's Windows 11 v0.1 milestone in TobbyTravel/cutsy, using the existing checkout at C:/Users/Tobby/claude/cutsy.

Start by reading AGENTS.md, then the product, architecture, roadmap, pack-format, asset-audit, and testing documents it references. Inspect the live milestone issues and current repository state before choosing work.

The repository contains the source artwork and individual PNG sprites. Resolve the documented asset-label, crop, transparency, and anchor defects before treating the pack as release-ready.

Build the agreed C#/.NET application: a portable Windows 11 desktop mascot with a platform-independent behavior engine, editable JSON recipes, quiet sounds, window-edge interactions, fullscreen suppression, and tray/settings controls. The character must be clickable and draggable, transparent surroundings must pass clicks through, and ordinary interaction must preserve the user's keyboard focus.

Follow the issue dependency order. First establish a working solution and prove the transparent Windows shell with one suitable sprite, clicking, dragging, tray controls, and DPI handling. Then implement the behavior engine, window tracking, corrected content pack, sounds, settings, and packaging.

Work through implementation and verification rather than returning another architecture proposal. Keep changes focused, use branches and PRs, and update issues with concrete evidence. Report blockers precisely and continue independent in-scope work when possible. Do not auto-merge PRs or publish a public release without authorization.

Preserve source artwork, keep desktop APIs inside the Windows host, and keep the core reusable for future KDE and macOS hosts. Use the repository's documented defaults for routine decisions.

The delivery target is a runnable self-contained EXE, an editable portable ZIP, completed behavior recipes covering most of the usable sprites, build/publish scripts, and documented acceptance-test results. Do not describe GUI behavior, clean-machine compatibility, performance targets, or anti-cheat compatibility as verified unless actually tested.

## Entry point and scope

C01 creates the buildable solution; C03 is the early interaction proof. C02 can progress alongside this work without delaying the first single-sprite shell. Complete C04 before integration with C05; use the roadmap dependency graph for later issues.

This bootstrap is complete when assets/documents/issues exist and verify, not when the app runs. Application implementation begins in the new session. Start with git status and the bootstrap verification script; preserve the baseline import and record any intentional runtime sprite derivatives.

The private repo includes all source/reference images. No companion service secrets, credentials beyond normal GitHub access, or external asset downloads are needed.
