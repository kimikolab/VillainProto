import argparse
import json
import shutil
import urllib.request
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw


folder = Path(__file__).resolve().parent
output = Path("D:/comfyui/output/VillainProto/idle_loop")
parser = argparse.ArgumentParser()
parser.add_argument("tags", nargs=2)
args = parser.parse_args()
repo = folder.parents[3]
with Image.open(repo / "DemoApp/assets/portraits/battle/hibi_idle_right.png") as reference:
    reference = reference.convert("RGBA").resize((640, 960), Image.Resampling.LANCZOS)
    reference_canvas = Image.new("RGBA", reference.size, (23, 34, 31, 255))
    reference_canvas.alpha_composite(reference)
    reference_gray = cv2.cvtColor(np.asarray(reference_canvas.convert("RGB")), cv2.COLOR_RGB2GRAY)
    shield_mask = np.asarray(Image.open(folder / "hibi-v1-shield-rigid-mask.png"))
    shield_mask = np.where(np.asarray(reference)[..., 3] > 240, shield_mask, 0).astype(np.uint8)
    shield_mask = cv2.erode(shield_mask, np.ones((7, 7), np.uint8))
    shield_points = cv2.goodFeaturesToTrack(reference_gray, 250, 0.03, 5, mask=shield_mask)
sequences = []
metrics = {}
for label, tag in zip(("A", "B"), args.tags):
    job = json.loads((folder / f"{tag}-job.json").read_text(encoding="utf-8"))
    with urllib.request.urlopen(f"http://127.0.0.1:8188/history/{job['prompt_id']}", timeout=30) as response:
        result = json.load(response).get(job["prompt_id"])
    if result is None or result["status"]["status_str"] != "success":
        raise RuntimeError(f"ComfyUIの保存完了を待ってください: {tag}")
    (folder / f"{tag}-result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    paths = sorted((output / f"{tag}_rgba").glob("*.png"))
    if len(paths) != 32:
        raise ValueError(f"32コマ必要です: {tag}")
    target = folder / f"{label}-frames"
    target.mkdir(exist_ok=True)
    if any(path.name not in {source.name for source in paths} for path in target.glob("*.png")):
        raise ValueError("出力先に別の連番が残っています")
    for path in paths:
        shutil.copy2(path, target / path.name)
    shutil.copy2(next(output.glob(f"{tag}_loop_*.webp")), folder / f"{label}-loop.webp")
    for suffix in ("check.jpg", "compare.gif"):
        shutil.copy2(output / f"{tag}_{suffix}", folder / f"{label}_{suffix}")
    sequence = []
    tops, feet, border_contacts = [], [], []
    shield_residuals, shield_centers = [], []
    pixels = []
    for path in paths:
        with Image.open(path) as image:
            if image.mode != "RGBA" or image.size != (640, 960):
                raise ValueError(f"RGBA・寸法不一致: {path}")
            values = np.asarray(image)
            vertical, horizontal = np.nonzero(values[..., 3] > 128)
            tops.append(int(vertical.min()))
            feet.append(int(vertical.max()))
            border_contacts.append(bool(vertical.min() == 0 or vertical.max() == 959 or horizontal.min() == 0 or horizontal.max() == 639))
            pixels.append(values)
            background = Image.new("RGBA", image.size, (23, 34, 31, 255))
            background.alpha_composite(image)
            sequence.append(background.convert("RGB"))
            gray = cv2.cvtColor(np.asarray(sequence[-1]), cv2.COLOR_RGB2GRAY)
            tracked, status, error = cv2.calcOpticalFlowPyrLK(reference_gray, gray, shield_points, None, winSize=(21, 21), maxLevel=4)
            valid = (status[:, 0] != 0) & (error[:, 0] < 30)
            original_points = shield_points[valid, 0].astype(float)
            target_points = tracked[valid, 0].astype(float)
            origin, destination = original_points.mean(axis=0), target_points.mean(axis=0)
            left, singular, right = np.linalg.svd((original_points - origin).T @ (target_points - destination))
            rotation = right.T @ left.T
            fitted = (original_points - origin) @ rotation.T + destination
            shield_residuals.append(float(np.median(np.linalg.norm(fitted - target_points, axis=1))))
            shield_centers.append(destination - origin)
    steps = [float(np.abs(pixels[frame_index + 1][..., :3].astype(float) - pixels[frame_index][..., :3]).mean()) for frame_index in range(31)]
    seam = float(np.abs(pixels[0][..., :3].astype(float) - pixels[-1][..., :3]).mean())
    metrics[label] = {"tag": tag, "seed": job["seed"], "amplitude": job["amplitude"], "frames": 32, "fps": 16, "head_range_px": max(tops) - min(tops), "foot_rows": [min(feet), max(feet)], "border_contacts": sum(border_contacts), "seam_ratio": seam / max(float(np.median(steps)), 1e-6), "total_bytes": sum(path.stat().st_size for path in paths)}
    metrics[label].update(shield_nonrigid_residual_median_px=float(np.median(shield_residuals)), shield_center_motion_range_px=np.ptp(np.asarray(shield_centers), axis=0).tolist())
    sequences.append(sequence)


def save_gif(frames, filename):
    swatches = Image.new("RGB", (1280, 960))
    for sample_index in range(16):
        swatches.paste(frames[sample_index * 2].resize((320, 240)), ((sample_index % 4) * 320, (sample_index // 4) * 240))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    durations = [round((frame_index + 1) * 6.25) * 10 - round(frame_index * 6.25) * 10 for frame_index in range(32)]
    indexed[0].save(folder / filename, save_all=True, append_images=indexed[1:], duration=durations, loop=0, optimize=False)


full_frames, detail_frames = [], []
for frame_index in range(32):
    full = Image.new("RGB", (640, 480), (23, 34, 31))
    detail = Image.new("RGB", (800, 480), (23, 34, 31))
    for column, sequence in enumerate(sequences):
        full.paste(sequence[frame_index].resize((320, 480), Image.Resampling.LANCZOS), (column * 320, 0))
        detail.paste(sequence[frame_index].crop((205, 120, 605, 600)).resize((400, 480)), (column * 400, 0))
    for canvas, width in ((full, 320), (detail, 400)):
        draw = ImageDraw.Draw(canvas)
        draw.text((12, 8), "A", fill=(244, 240, 221))
        draw.text((width + 12, 8), "B", fill=(244, 240, 221))
    full_frames.append(full)
    detail_frames.append(detail)
save_gif(full_frames, "A-vs-B.gif")
save_gif(detail_frames, "shield-detail.gif")
sheet = Image.new("RGB", (2400, 960))
for sample_index, frame_index in enumerate((0, 5, 10, 16, 21, 26)):
    sheet.paste(detail_frames[frame_index], ((sample_index % 3) * 800, (sample_index // 3) * 480))
sheet.save(folder / "shield-contact.jpg", quality=92)
(folder / "metrics.json").write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")
print(json.dumps(metrics, indent=2))
print("HIBI_IDLE_PREVIEW_OK frames=32")
