"""Re-extract reviewed runtime art from the immutable processed sheets.

Developer dependencies: Pillow, NumPy and SciPy. Run from the repository root.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

# Visual review, in row/column order. Height is the visible body fraction of
# an upright pose. Contact coordinates are fractions of the body bounds.
# The named contact is additional to a floor/feet reference for each frame.
REVIEW = [
    [
        ("phone-stand", "front", 1, .49, 1, "phone,boba,bag"),
        ("phone-smile", "front", 1, .49, 1, "phone,boba,bag"),
        ("phone-glance", "left", 1, .47, 1, "phone,boba,bag"),
        ("phone-turn-right", "right", 1, .50, 1, "phone,boba,bag"),
        ("phone-walk-left-a", "left", 1, .50, 1, "phone,boba,bag"),
        ("phone-walk-left-b", "left", 1, .49, 1, "phone,boba,bag"),
        ("phone-walk-right-a", "right", 1, .50, 1, "phone,boba,bag"),
        ("phone-walk-right-b", "right", 1, .50, 1, "phone,boba,bag"),
    ],
    [
        ("boba-crouch", "left", .73, .61, 1, "bag"),
        ("jump-prep-left", "left", .81, .58, 1, "bag"),
        ("jump-up", "left", .87, .53, 1, "bag"),
        ("jump-cheer", "front", .82, .48, 1, "bag"),
        ("jump-startled", "front", .90, .48, 1, "bag"),
        ("surprised-drop", "front", .88, .49, 1, "phone,boba,bag"),
        ("landing-crouch", "front", .76, .49, 1, "bag"),
        ("jump-recover", "front", .91, .50, 1, "bag"),
    ],
    [
        ("top-peek", "front", .55, .51, 1, "", ("hands", .51, .94)),
        ("top-climb", "front", .83, .48, 1, "bag", ("hands", .50, .68)),
        ("edge-sit", "front", 1, .50, 1, "bag", ("seat", .50, .74)),
        ("edge-hang", "front", 1, .50, 1, "bag", ("hands", .50, .07)),
        ("edge-swing", "front", 1, .49, 1, "bag", ("hands", .52, .08)),
        ("side-peek-right", "left", .91, .45, 1, "bag", ("sideGrip", 0, .64)),
        ("side-peek-left", "right", .91, .55, 1, "bag", ("sideGrip", 1, .64)),
        ("edge-inverted", "front", 1, .51, 1, "bag", ("hands", .50, .06)),
    ],
    [
        ("phone-tap", "left", 1, .49, 1, "phone,boba,bag"),
        ("boba-love", "front", 1, .50, 1, "boba,bag"),
        ("boba-wave", "front", 1, .52, 1, "boba,bag"),
        ("phone-curious", "front", 1, .49, 1, "phone,boba,bag"),
        ("small-parcel", "left", 1, .53, 1, "parcel,bag"),
        ("small-laptop", "front", .80, .48, 1, "laptop,bag"),
        ("small-sweep", "left", 1, .60, .99, "broom,bag"),
        ("small-plush", "left", 1, .57, 1, "plush,bag"),
    ],
    [
        ("stand", "front", 1, .48, 1, "bag"),
        ("smile", "front", 1, .48, 1, "bag"),
        ("heart-eyes", "front", 1, .49, 1, "bag"),
        ("sulk", "front", 1, .49, 1, "bag"),
        ("startled", "front", 1, .49, 1, "bag"),
        ("yawn", "front", 1, .50, 1, "bag"),
        ("sit-thinking", "front", .76, .49, 1, "bag"),
        ("floor-nap", "left", .50, .48, 1, "bag"),
    ],
    [None] * 8,  # Every pose has a visible baked rail and matte halo.
    [
        ("fall-anticipation", "left", .77, .50, 1, "bag"),
        ("fall-takeoff", "left", .89, .50, 1, "bag"),
        ("fall-airborne", "left", .87, .50, 1, "bag"),
        None,  # Hair is clipped at the right source-sheet edge. Use surprised-drop.
        ("fall-tumble", "front", .77, .51, 1, "bag"),
        ("fall-impact", "front", .77, .50, .93, "bag"),
        ("fall-recover", "left", .76, .49, 1, "bag"),
        ("fall-celebrate", "front", .94, .49, 1, "bag"),
    ],
    [
        ("boba-idle", "front", 1, .49, 1, "boba,bag"),
        ("boba-sip", "front", 1, .49, 1, "boba,bag"),
        ("phone-scroll", "left", 1, .51, 1, "phone,boba,bag"),
        ("boba-hug", "front", 1, .48, 1, "boba,bag"),
        ("shy", "front", 1, .49, 1, "bag"),
        ("proud", "front", 1, .49, 1, "bag"),
        ("sleepy", "front", 1, .49, 1, "bag"),
        ("seated-nap", "left", .74, .49, 1, "bag"),
    ],
    [
        ("parcel-carry", "left", 1, .53, 1, "parcel,bag"),
        ("folder-push", "left", .90, .72, 1, "folder,bag"),
        ("card-carry", "left", .94, .63, 1, "window card,bag"),
        ("laptop", "front", .78, .48, 1, "laptop,bag"),
        ("sweep", "left", 1, .61, 1, "broom,bag"),
        ("notice", "front", 1, .56, 1, "tablet,bag"),
        ("plush-hug", "left", .96, .50, 1, "plush,bag"),
        ("offer-heart", "front", 1, .49, 1, "heart,bag"),
    ],
    [
        ("walk-prep-left", "left", .91, .49, 1, "phone,boba,bag"),
        ("walk-prep-right", "right", .91, .49, 1, "phone,boba,bag"),
        ("walk-left", "left", 1, .50, 1, "phone,boba,bag"),
        ("walk-right", "right", 1, .50, 1, "phone,boba,bag"),
        ("run-left", "left", .93, .49, 1, "phone,boba,bag"),
        ("run-right", "right", .93, .49, 1, "phone,boba,bag"),
        ("look-up", "front", 1, .49, 1, "phone,boba,bag"),
        ("look-back", "right", 1, .47, 1, "phone,boba,bag"),
    ],
]

ROOT = Path("packs/cutsy")
OUT = ROOT / "runtime"
REFERENCE = 400


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, data):
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def extract_sheet(entries, reviews):
    source = Path(entries[0]["sourceProcessedPath"])
    rgba = np.array(Image.open(source).convert("RGBA"))
    alpha = rgba[:, :, 3]
    labels, count = ndimage.label(alpha >= 96)
    sizes = np.bincount(labels.ravel())
    centers = ndimage.center_of_mass(alpha, labels, range(1, count + 1))
    objects = ndimage.find_objects(labels)
    assigned = [[] for _ in entries]
    for label, (y, x) in enumerate(centers, 1):
        if sizes[label] < 12:
            continue
        for i, entry in enumerate(entries):
            box = entry["cropRectPx"]
            if box["left"] <= x < box["right"] and box["top"] <= y < box["bottom"]:
                assigned[i].append(label)
                break

    sprites, audit = [], []
    for entry, review, components in zip(entries, reviews, assigned):
        record = {
            "sourceAssetId": entry["assetId"],
            "legacyCrop": entry["cropRectPx"],
            "sourceProcessedPath": source.as_posix(),
            "sourceProcessedSha256": digest(source),
            "sourceRawPath": entry["sourceRawPath"],
            "sourceRawSha256": digest(Path(entry["sourceRawPath"])),
        }
        if review is None:
            is_rail = source.name.startswith("sheet_06")
            reason = ("Baked rail and matte halo. Use rail-free sheet 03 variants." if is_rail
                      else "Hair is clipped by the source sheet itself. Use surprised-drop.")
            record.update(status="excluded", reason=reason)
            audit.append(record)
            continue

        name, facing, visible_height, fx, fy, props, *extra = review
        body = max(components, key=lambda index: sizes[index])
        # Recover complete connected components even when the old fixed-grid cut crossed them.
        # Drop detached slivers and the clipped dust at the bottom of the takeoff pose.
        if name == "fall-takeoff":
            components = [label for label in components if label == body or
                          centers[label - 1][0] < objects[body - 1][0].stop]
        visible = ndimage.binary_dilation(np.isin(labels, components), iterations=2)
        interactive = ndimage.binary_dilation(labels == body, iterations=1)
        if name == "fall-impact":
            # The impact star touches the body. End the input silhouette at the boots.
            y_bounds = objects[body - 1][0]
            interactive[y_bounds.start + round((y_bounds.stop - y_bounds.start) * .93):] = False

        pixels = rgba.copy()
        pixels[:, :, 3] = np.where(visible & (alpha >= 18), alpha, 0)
        pixels[pixels[:, :, 3] == 0, :3] = 0
        image = Image.fromarray(pixels)
        l, t, r, b = image.getbbox()
        image.crop((l, t, r, b)).save(OUT / "art" / f"{name}.png", optimize=True)
        Image.fromarray((interactive * 255).astype(np.uint8)).crop((l, t, r, b)).save(
            OUT / "masks" / f"{name}.png", optimize=True)
        by, bx = objects[body - 1]
        bw, bh = bx.stop - bx.start, by.stop - by.start

        def contact(x, y):
            return {"x": max(0, min(r-l, round(bx.start-l+bw*x))),
                    "y": max(0, min(b-t, round(by.start-t+bh*y)))}

        anchors = {"feet": contact(fx, fy)}
        for key, x, y in extra:
            anchors[key] = contact(x, y)
        sprite = {
            "id": name,
            "sourceAssetId": entry["assetId"],
            "facing": facing,
            "path": f"art/{name}.png",
            "maskPath": f"masks/{name}.png",
            "contentBounds": {"x": 0, "y": 0, "width": r-l, "height": b-t},
            "anchors": anchors,
            "scaleCorrection": round(REFERENCE * visible_height / bh, 6),
            "bakedInProps": props.split(",") if props else [],
        }
        sprites.append(sprite)
        record.update(status="superseded", runtimeId=name, facing=facing, bakedInProps=sprite["bakedInProps"],
                      runtimeCropLTRBExclusive=[l,t,r,b], anchors=anchors, scaleCorrection=sprite["scaleCorrection"],
                      imageSha256=digest(OUT / sprite["path"]), maskSha256=digest(OUT / sprite["maskPath"]))
        audit.append(record)
    return sprites, audit


def preview(sprites):
    size = (1600, ((len(sprites) + 7) // 8) * 198)
    for variant, background in enumerate(("#242635", "#ffffff", "#009486", "#c4b8c7")):
        sheet = Image.new("RGBA", size, background)
        draw = ImageDraw.Draw(sheet)
        for i, sprite in enumerate(sprites):
            x, y = (i % 8) * 200, (i // 8) * 198
            if variant == 3:
                for sy in range(y, y+180, 20):
                    for sx in range(x, x+200, 20):
                        if ((sx-x)//20 + (sy-y)//20) % 2:
                            draw.rectangle((sx,sy,sx+19,sy+19),fill="#e5dde4")
            scale = 160 * sprite["scaleCorrection"] / REFERENCE
            anchor = sprite["anchors"].get("hands") or sprite["anchors"].get("sideGrip") or sprite["anchors"].get("seat") or sprite["anchors"]["feet"]
            im = Image.open(OUT / sprite["path"]).convert("RGBA")
            im = im.resize((round(im.width*scale), round(im.height*scale)), Image.Resampling.LANCZOS)
            ay = y + min(169, 20 + round(anchor["y"]*scale))
            sheet.alpha_composite(im, (x+100-round(anchor["x"]*scale), ay-round(anchor["y"]*scale)))
            draw.line((x+95,ay,x+105,ay),fill="#fc5575",width=1)
            draw.text((x+10, y+180),sprite["id"],fill="#ffffff" if variant == 0 else "#252030")
        target = Path("artifacts")
        target.mkdir(exist_ok=True)
        sheet.convert("RGB").save(target / f"art-review-{variant}.jpg", quality=91)


def main():
    (OUT / "art").mkdir(parents=True, exist_ok=True)
    (OUT / "masks").mkdir(parents=True, exist_ok=True)
    provenance = json.loads(Path("assets/source/provenance.json").read_text(encoding="utf-8-sig"))
    sprites, audit = [], []
    for sheet, reviews in enumerate(REVIEW, 1):
        entries = sorted((row for row in provenance["files"] if row["role"] == "sprites" and
                          row["sourceSheet"].startswith(f"sheet_{sheet:02}_")), key=lambda e: (e["row"], e["column"]))
        if all(entry is None for entry in reviews):
            # Record exclusions without segmenting the old matte.
            for entry in entries:
                raw, processed = Path(entry["sourceRawPath"]), Path(entry["sourceProcessedPath"])
                audit.append({"sourceAssetId": entry["assetId"], "status": "excluded",
                              "reason": "Baked rail and matte halo. Use rail-free sheet 03 variants.",
                              "sourceRawPath": raw.as_posix(), "sourceRawSha256": digest(raw),
                              "sourceProcessedPath": processed.as_posix(), "sourceProcessedSha256": digest(processed)})
            continue
        new_sprites, new_audit = extract_sheet(entries, reviews)
        sprites.extend(new_sprites)
        audit.extend(new_audit)
    assert len(audit) == 80 and len(sprites) == 71
    write_json(OUT / "sprite-index.json", {"referenceHeightPx": REFERENCE, "sprites": sprites})
    write_json(ROOT / "runtime-provenance.json", {
        "schemaVersion": 1, "algorithm": "Four-connected alpha>=96 components assigned by weighted centroid on original sheet; full components recovered, two-pixel matte edge preserved, alpha below 18 cleared. Largest component supplies body mask.",
        "frames": audit,
    })
    preview(sprites)
    print(f"Reviewed {len(audit)} source frames; derived {len(sprites)} runtime sprites and masks.")


if __name__ == "__main__":
    main()
