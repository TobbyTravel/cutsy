# Bootstrap verification

Verified during repository setup on 2026-11-09:

- 102 PNG files copied and compared by SHA-256 against the supplied bundle: 80 individual sprites, 10 original sheets, 10 processed sheets, 2 reference anchors.
- 80 distinct source-sheet/row/column mappings preserved; original download filenames and source hashes recorded in provenance.json.
- All PNGs staged as ordinary Git blobs; no external asset download or Git LFS dependency.
- Local Markdown links and all 12 roadmap/issue mappings validated by scripts/verify-bootstrap.ps1.
- Windows 11 v0.1 milestone contains C00–C10; K01 is open and deferred outside that milestone.
- Product, architecture, pack contract, known asset defects, testing, issue specifications, and Sol handoff are self-contained in this checkout.

Not verified or implemented by this bootstrap: visual repairs, runnable application, runtime pack/recipes, sounds, window/input behavior, fullscreen handling, packaging, performance, clean-machine launch, or soak testing. These remain the milestone's implementation work.

The source bundle's old manifest is retained as historical evidence and contains obsolete relative paths and provisional descriptions. Use provenance.json for repository-relative source mapping; use ASSET_AUDIT.md for known defects.
