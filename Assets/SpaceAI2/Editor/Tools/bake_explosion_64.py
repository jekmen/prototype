"""Bake a 64-frame RGBA flipbook from the approved 16-frame ImageGen source.

Run from the project root with Python + numpy + opencv-python + Pillow.
Motion is computed offline; playback uses Unity's native Texture Sheet Animation.
The original source is never overwritten. Intermediate previews live under Temp.
"""
import hashlib
import json
import os
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[4]
RESOURCES = ROOT / "Assets/SpaceAI2/Runtime/Effects/Resources"
SOURCE = RESOURCES / "ExplosionGenerated.png"
OUTPUT = RESOURCES / "ExplosionGenerated64.png"
PREVIEW = ROOT / "Temp/SpaceAI2Captures/OpticalFlow64"
SIZE, PADDING, COUNT = 512, 8, 64
cv2.setNumThreads(4)
Y, X = np.mgrid[:SIZE, :SIZE].astype(np.float32)


def linear(rgb):
    return np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)


def srgb(rgb):
    rgb = np.clip(rgb, 0, 1)
    return np.where(rgb <= 0.0031308, rgb * 12.92, 1.055 * rgb ** (1 / 2.4) - 0.055)


def sample(field, x, y, border=cv2.BORDER_CONSTANT):
    return cv2.remap(field, x.astype(np.float32), y.astype(np.float32), cv2.INTER_LINEAR, borderMode=border)


def unpack_frames():
    source = np.asarray(Image.open(SOURCE).convert("RGBA"), dtype=np.float32) / 255
    # Transparent background quantization must not contribute RGB to interpolation.
    source[..., 3][source[..., 3] <= 2 / 255] = 0
    source[..., :3] = linear(source[..., :3]) * source[..., 3:4]
    h, w = source.shape[:2]
    frames = []
    inner = SIZE - PADDING * 2
    u = (np.arange(inner, dtype=np.float32) + 0.5) / inner
    for index in range(16):
        # Sample fractional source cells consistently (1254 is not divisible by 4).
        mx, my = np.meshgrid(index % 4 * w / 4 + 1 + u * (w / 4 - 2) - 0.5,
            index // 4 * h / 4 + 1 + u * (h / 4 - 2) - 0.5)
        frame = np.zeros((SIZE, SIZE, 4), np.float32)
        frame[PADDING:-PADDING, PADDING:-PADDING] = sample(source, mx, my)
        frames.append(frame)
    return frames


def tracking_image(frame):
    alpha = frame[..., 3]
    rgb = frame[..., :3] / np.maximum(alpha[..., None], 1e-6)
    luma = srgb(rgb) @ np.array([0.25, 0.6, 0.15], np.float32)
    return np.rint(np.clip(alpha * (0.4 + 0.6 * luma), 0, 1) * 255).astype(np.uint8)


def moments(frame):
    weight = frame[..., 3] ** 2
    total = max(float(weight.sum()), 1e-6)
    cx, cy = float((X * weight).sum() / total), float((Y * weight).sum() / total)
    radius = np.sqrt((((X-cx)**2 + (Y-cy)**2) * weight).sum() / total)
    return cx, cy, float(radius)


def flow_pair(a, b):
    ga, gb = tracking_image(a), tracking_image(b)
    ax, ay, ar = moments(a)
    bx, by, br = moments(b)
    scale = float(np.clip(br / max(ar, 1), 0.75, 2))
    solver = cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
    solver.setFinestScale(0)
    solver.setGradientDescentIterations(40)
    solver.setVariationalRefinementIterations(8)

    # Remove the large overall expansion before solving fine smoke motion.
    aligned_b = sample(gb, (X-ax)*scale+bx, (Y-ay)*scale+by)
    residual = solver.calc(ga, aligned_b, None)
    forward = np.dstack(((X+residual[..., 0]-ax)*scale+bx-X,
        (Y+residual[..., 1]-ay)*scale+by-Y))
    aligned_a = sample(ga, (X-bx)/scale+ax, (Y-by)/scale+ay)
    residual = solver.calc(gb, aligned_a, None)
    backward = np.dstack(((X+residual[..., 0]-bx)/scale+ax-X,
        (Y+residual[..., 1]-by)/scale+ay-Y))
    forward = cv2.GaussianBlur(forward, (0, 0), 0.6)
    backward = cv2.GaussianBlur(backward, (0, 0), 0.6)

    def confidence(f, other):
        reverse = sample(other, X+f[..., 0], Y+f[..., 1], cv2.BORDER_REPLICATE)
        error = np.sum((f+reverse)**2, axis=2)
        return np.exp(-error / (4 + 0.04*np.sum(f*f, axis=2))).astype(np.float32)

    return forward, backward, confidence(forward, backward), confidence(backward, forward)


def inverse_warp(flow, fraction):
    # Invert x_mid = x_source + fraction * flow(x_source), including radial growth.
    mx, my = X.copy(), Y.copy()
    for _ in range(10):
        f = sample(flow, mx, my, cv2.BORDER_REPLICATE)
        mx = 0.2*mx + 0.8*(X - fraction*f[..., 0])
        my = 0.2*my + 0.8*(Y - fraction*f[..., 1])
    return mx, my


def interpolate(a, b, flows, t):
    if t <= 1e-6:
        return a.copy()
    if t >= 1-1e-6:
        return b.copy()
    f, back, ca, cb = flows
    ax, ay = inverse_warp(f, t)
    bx, by = inverse_warp(back, 1-t)
    wa = (1-t) * (0.35 + 0.65*sample(ca, ax, ay))
    wb = t * (0.35 + 0.65*sample(cb, bx, by))
    # Blend premultiplied linear RGB and alpha together to avoid colored fringes.
    return (sample(a, ax, ay)*wa[..., None] + sample(b, bx, by)*wb[..., None]) / (wa+wb)[..., None]


def encode(frame):
    frame = np.clip(frame, 0, 1).copy()
    # A shared transparent gutter prevents native grid sampling from reaching neighbors.
    frame[:PADDING] = frame[-PADDING:] = 0
    frame[:, :PADDING] = frame[:, -PADDING:] = 0
    alpha = frame[..., 3]
    rgb = srgb(frame[..., :3] / np.maximum(alpha[..., None], 1e-6))
    # Extend edge color into alpha-zero pixels for native straight-alpha frame blending.
    known = (alpha > 2 / 255).astype(np.uint8)
    for _ in range(PADDING):
        count = cv2.boxFilter(known.astype(np.float32), -1, (3, 3), normalize=False)
        summed = cv2.boxFilter(rgb * known[..., None], -1, (3, 3), normalize=False)
        new = (known == 0) & (count > 0)
        rgb[new] = summed[new] / count[new, None]
        known[new] = 1
    return np.rint(np.clip(np.dstack((rgb, alpha)), 0, 1) * 255).astype(np.uint8)


def preview(frame):
    background = np.array([0.012, 0.017, 0.025], np.float32)
    return Image.fromarray(np.rint(srgb(frame[..., :3] + (1-frame[..., 3:4])*background)*255).astype(np.uint8))


def main():
    PREVIEW.mkdir(parents=True, exist_ok=True)
    frames = unpack_frames()
    flows = []
    for i in range(15):
        pair = flow_pair(frames[i], frames[i+1])
        assert all(np.isfinite(field).all() for field in pair), "Non-finite flow"
        flows.append(pair)
        print(f"FLOW {i+1}/15", flush=True)

    atlas = Image.new("RGBA", (SIZE*8, SIZE*8))
    hashes = set()
    for i in range(COUNT):
        position = i * 15 / (COUNT-1)
        pair_index = min(int(position), 14)
        frame = interpolate(frames[pair_index], frames[pair_index+1], flows[pair_index], position-pair_index)
        assert np.isfinite(frame).all(), "Non-finite interpolated pixels"
        encoded = encode(frame)
        assert not encoded[:PADDING, :, 3].any() and not encoded[-PADDING:, :, 3].any()
        assert not encoded[:, :PADDING, 3].any() and not encoded[:, -PADDING:, 3].any()
        hashes.add(hashlib.sha256(encoded.tobytes()).hexdigest())
        atlas.paste(Image.fromarray(encoded, "RGBA"), ((i % 8)*SIZE, (i // 8)*SIZE))
        preview(frame).save(PREVIEW / f"{i:04d}.png")
    assert len(hashes) == COUNT, "Duplicate output frames"
    assert np.array_equal(interpolate(frames[0], frames[1], flows[0], 0), frames[0])
    assert np.array_equal(interpolate(frames[-2], frames[-1], flows[-1], 1), frames[-1])

    comparison = Image.new("RGB", (SIZE*4, SIZE*3))
    for row, index in enumerate([0, 4, 10]):
        for col, frame in enumerate([frames[index], interpolate(frames[index], frames[index+1], flows[index], 0.5),
                (frames[index]+frames[index+1])*0.5, frames[index+1]]):
            comparison.paste(preview(frame), (col*SIZE, row*SIZE))
    draw = ImageDraw.Draw(comparison)
    for row, index in enumerate([0, 4, 10]):
        for col, label in enumerate([f"Source {index+1}", "Optical flow 50%", "Plain blend 50%", f"Source {index+2}"]):
            draw.text((col*SIZE+12, row*SIZE+12), label, fill="white")
    comparison.save(PREVIEW / "comparison.png")
    staged = PREVIEW / OUTPUT.name
    atlas.save(staged)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    os.replace(staged, OUTPUT)
    (PREVIEW / "report.json").write_text(json.dumps({
        "source": str(SOURCE), "source_sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        "output": str(OUTPUT), "output_sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
        "source_frames": 16, "output_frames": COUNT, "unique_output_frames": len(hashes),
        "grid": [8, 8], "tile_size": SIZE, "gutter": PADDING, "linear_premultiplied_interpolation": True,
        "method": "Bidirectional DIS with expansion compensation and consistency weighting",
    }, indent=2))
    print(f"BAKED {OUTPUT}: {COUNT} unique frames, {SIZE*8} square", flush=True)


if __name__ == "__main__":
    main()
