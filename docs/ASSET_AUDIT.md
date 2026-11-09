# Asset audit: required before release

Status: NOT complete. All 102 PNGs are present and their bytes are verified against the supplied pack. This does not establish visual correctness, frame alignment, natural animation, or release-ready transparency.

## Known findings

- Legacy descriptions in sheet_04_expressions do not match several images: the bottom row includes carrying a box, laptop use, sweeping and affection. They are not the surprise/annoyed/sulky/thinking sequence claimed in the original manifest.
- sheet_01 bottom row contains opposite-facing walking poses. The original walk_right recommendation must not be replayed blindly.
- sheet_02 is largely jump/fall/landing poses, despite several seated descriptions. Reclassify visually.
- sheet_07 action ordering and sheet_09 prop names also need visual correction.
- Every legacy pivot is a generic 0.5,0.92 suggestion. Measure per-frame foot/seat/hand contacts.
- Fixed-grid extraction produced widths/heights 443 or 444 pixels in the first series and 362x543 in the second. Character scale, padding, and contact position differ.
- Substantial alpha touches left/right crop boundaries in sheet_07 r1c3, r1c4, r2c3, r2c4; sheet_09 r1c1–c4, r2c1–c2. These are candidates for clipping or neighboring-fragment contamination, not proof that every frame is broken.
- sheet_06 originally had no fully transparent pixels. An edge-connected light-neutral flood-fill made 906696 pixels transparent. Colored-background inspection still suggests edge halos around hair and decorative marks. Recheck; preserve the original.
- Some ledge/hanging frames include rails. Use rail-free art where suitable or explicitly classify the rail as a prop; do not make an unexplained rail appear on an existing window edge.
- Many original image pixels are partially opaque, including reference portraits. Preserve intended antialiasing; do not treat the mere presence of alpha=0 as a quality pass.

## Audit workflow and evidence

1. Inspect each original sheet and all individual crops over dark, white, checkerboard, and saturated backgrounds at native and intended display size.
2. Record frame ID, actual pose/emotion, direction, props, quality, intended behavior, and inclusion/exclusion decision for all 80.
3. Repair crop boundaries using the original sheet where possible; retain source provenance. Keep originals immutable.
4. Measure content bounds, scale correction, contact anchors and body-only input masks. Exclude particles/hearts/shadows from click targets.
5. Preview each compatible sequence at intended FPS and with fixed world contacts. Reject abrupt direction/scale/contact jumps.
6. Produce an updated machine-readable mapping and an HTML/contact-sheet preview with anchored sequence playback. Record unresolved defects rather than declaring cosmetic checks passed.

Deliver reviewed frames and corrected runtime metadata in the pack, leaving historical reference files available under assets/source. Any image-editing workflow must follow the tools/skills available to that implementation session.

## Runtime derivatives

The source files and legacy labels remain available as provenance. The runtime art in packs/cutsy/runtime uses the visually assigned names and directions in scripts/derive-sprites.py. Its sprite index contains per-frame contact and scale corrections. The corresponding runtime-provenance.json accounts for all 80 originals, including original sheet hashes, actual extraction bounds, anchor coordinates and runtime hashes.

The extraction uses full connected components from the processed sheets to recover art crossing the legacy cells. It separates body input masks from detached decorations. Eight sheet 06 poses were excluded because of the matte halo and built-in rails, with sheet 03 supplying the rail-free alternatives. The clipped sheet 07 surprise pose was excluded in favor of the complete surprise pose from sheet 02. The result is 71 runtime sprites with masks.

To regenerate the derivatives and 160 px contact sheets, use Python with Pillow, NumPy and SciPy, then run python scripts/derive-sprites.py. It writes review images against dark, white, saturated and checkerboard backgrounds under artifacts. The previews mark the selected contact. In-motion continuity still depends on the authored recipes.
