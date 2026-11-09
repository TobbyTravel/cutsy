"""Import the four reviewed walking poses from the supplemental source sheet."""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path("packs/cutsy/runtime")
SOURCE = Path("assets/derivatives/walk-inbetweens.png")
POSES = [
    ("walk-pass-left", "left", .42, 1.0),
    ("walk-lift-left", "left", .43, 1.01),
    ("walk-pass-right", "right", .60, 1.0),
    ("walk-lift-right", "right", .58, 1.01),
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path, data):
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def main():
    image = Image.open(SOURCE).convert("RGBA")
    w, h = image.size
    manifest_path = ROOT / "pack.json"
    pack = json.loads(manifest_path.read_text(encoding="utf-8"))
    derived, entries = [], []
    for index, (name, facing, pivot, height) in enumerate(POSES):
        col, row = index % 2, index // 2
        x0, y0 = col*w//2, row*h//2
        cell = np.array(image.crop((x0, y0, (col+1)*w//2, (row+1)*h//2)))
        alpha = cell[:, :, 3]
        labels, _ = ndimage.label(alpha >= 96)
        counts = np.bincount(labels.ravel())
        if len(counts) < 2:
            raise ValueError(f"No visible sprite in cell {index}")
        body = labels == counts[1:].argmax() + 1
        visible = ndimage.binary_dilation(body, iterations=2) & (alpha >= 18)
        cell[:, :, 3] = np.where(visible, alpha, 0)
        cell[~visible, :3] = 0
        art = Image.fromarray(cell)
        l, t, r, b = art.getbbox()
        ys, xs = np.nonzero(body)
        path, mask = f"art/{name}.png", f"masks/{name}.png"
        art.crop((l,t,r,b)).save(ROOT / path, optimize=True)
        mask_array = (ndimage.binary_dilation(body) * 255).astype(np.uint8)
        Image.fromarray(mask_array).crop((l,t,r,b)).save(ROOT / mask, optimize=True)
        feet = {"x": round(xs.min() - l + (xs.max() - xs.min() + 1) * pivot),
                "y": int(ys.max() - t + 1)}
        sprite = dict(id=name, path=path, sourceAssetId=name + "-generated",
            facing=facing, contentBounds=dict(x=0, y=0, width=r-l, height=b-t),
            anchors={"feet": feet}, scaleCorrection=round(pack["referenceHeightPx"] * height /
            int(ys.max() - ys.min() + 1), 6), maskPath=mask, bakedInProps=["phone", "boba", "bag"])
        derived.append(sprite)
        entries.append(dict(spriteId=name, cropLTRBExclusive=[l+x0,t+y0,r+x0,b+y0],
            feet=feet, scaleCorrection=sprite["scaleCorrection"], imageSha256=sha(ROOT/path), maskSha256=sha(ROOT/mask)))
    ids = {item["id"] for item in derived}
    pack["sprites"] = [item for item in pack["sprites"] if item["id"] not in ids] + derived
    write(manifest_path, pack)
    write(ROOT.parent / "walk-inbetweens-provenance.json", dict(
        source=SOURCE.as_posix(), sourceSha256=sha(SOURCE), method="Built-in image generation with existing directional art as the visual reference. Split into four cells, retain the largest connected body and its antialiased edge, measure feet and size.",
        prompt=(
            'Create a production animation sprite sheet of four NEW in-between WALKING poses for the same '
            'chibi girl in the reference. This is for animation in Cutsy. Preserve the original artist look, '
            'warm shading, hair, face, cream outfit, black ankle boots, bag, pink phone and yellow boba cup. '
            'The reference images are style/model reference, not the composition. Output a 2x2 evenly spaced '
            'sheet of exactly four isolated full-body sprites, equal height and consistent head/body '
            'proportions, on true transparent alpha, each centered in its cell with roomy clear padding '
            'between figures. No labels or baselines, no shadows, no decorations. TOP LEFT: facing LEFT, mid '
            'passing pose, weight on vertical near leg under hips, rear/far boot lifting behind just off the '
            'ground. TOP RIGHT: facing LEFT, raised passing pose with near boot planted vertically and other '
            'boot crossing under skirt ahead of it in the air. BOTTOM LEFT: facing RIGHT, mid passing pose, '
            'weight on vertical near leg under hips, rear/far boot lifting just off ground. BOTTOM RIGHT: '
            'facing RIGHT, raised passing pose, support foot directly under the hips, other boot crossing '
            'slightly forward just off ground. Crucial: the center of mass is high, body nearly upright, knees '
            'soft, feet close together underneath the hips. Do not use a wide lunge, crouch, big jump, or run. '
            'Match strict left and right silhouettes of the reference profile walking poses. Keep entire '
            'character in each cell.'
        ),
        referenceSpriteIds=["phone-walk-left-a", "phone-walk-left-b", "walk-left", "walk-prep-left",
                            "phone-walk-right-a", "phone-walk-right-b", "walk-right", "walk-prep-right"],
        frames=entries))


if __name__ == "__main__":
    main()
