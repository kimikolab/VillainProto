import json
import shutil
import urllib.request
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw


folder = Path(__file__).resolve().parent
output = Path("D:/comfyui/output/VillainProto/idle_loop")
tag = "gare_A70_coherent_aligned"
job = json.loads((folder / "job.json").read_text(encoding="utf-8"))["prompt_id"]
with urllib.request.urlopen(f"http://127.0.0.1:8188/history/{job}", timeout=30) as response:
    result = json.load(response).get(job)
if result is None or result["status"]["status_str"] != "success":
    raise RuntimeError("ComfyUIの保存完了を待ってからプレビューを作成してください")
(folder / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
paths = sorted((output / f"{tag}_rgba").glob("*.png"))
previous_paths = sorted((folder.parent / "idle-loop-v2/A80-frames").glob("*.png"))
if len(paths) != 30 or len(previous_paths) != 30:
    raise ValueError("両方の候補に30コマ必要です")
frames_folder = folder / "frames"
frames_folder.mkdir(exist_ok=True)
for path in paths:
    shutil.copy2(path, frames_folder / path.name)
shutil.copy2(next(output.glob(f"{tag}_loop_*.webp")), folder / "loop.webp")
for suffix in ("check.jpg", "compare.gif"):
    shutil.copy2(output / f"{tag}_{suffix}", folder / suffix)


def composite(path):
    with Image.open(path) as source:
        if source.size != (640, 960) or source.mode != "RGBA":
            raise ValueError(f"透過画像の形式が違います: {path}")
        canvas = Image.new("RGBA", source.size, (23, 34, 31, 255))
        canvas.alpha_composite(source)
        return canvas.convert("RGB")


def save_gif(frames, filename):
    swatches = Image.new("RGB", (1280, 960))
    for sample_index in range(16):
        sample = frames[sample_index * len(frames) // 16]
        swatches.paste(sample.resize((320, 240)), ((sample_index % 4) * 320, (sample_index // 4) * 240))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    durations = [round((frame_index + 1) * 6.25) * 10 - round(frame_index * 6.25) * 10 for frame_index in range(30)]
    indexed[0].save(folder / filename, save_all=True, append_images=indexed[1:], duration=durations, loop=0, optimize=False)


full_frames = []
detail_frames = []
for previous_path, path in zip(previous_paths, paths):
    previous, revised = composite(previous_path), composite(path)
    full = Image.new("RGB", (640, 480))
    full.paste(previous.resize((320, 480), Image.Resampling.LANCZOS), (0, 0))
    full.paste(revised.resize((320, 480), Image.Resampling.LANCZOS), (320, 0))
    draw = ImageDraw.Draw(full)
    draw.text((12, 8), "Previous: A80", fill=(244, 240, 221))
    draw.text((332, 8), "Revised: A70", fill=(244, 240, 221))
    full_frames.append(full)
    detail = Image.new("RGB", (800, 480), (23, 34, 31))
    for column, picture in enumerate((previous, revised)):
        crop = picture.crop((100, 155, 300, 395)).resize((400, 480), Image.Resampling.LANCZOS)
        detail.paste(crop, (column * 400, 0))
    draw = ImageDraw.Draw(detail)
    draw.text((12, 8), "Previous: A80", fill=(244, 240, 221))
    draw.text((412, 8), "Revised: A70", fill=(244, 240, 221))
    detail_frames.append(detail)
save_gif(full_frames, "A80-vs-A70.gif")
save_gif(detail_frames, "sleeve-detail.gif")
detail_frames[0].save(folder / "sleeve-detail.png")
sheet = Image.new("RGB", (800 * 3, 480 * 2))
for sample_index, frame_index in enumerate((0, 5, 10, 15, 20, 25)):
    sheet.paste(detail_frames[frame_index], ((sample_index % 3) * 800, (sample_index // 3) * 480))
sheet.save(folder / "sleeve-contact.jpg", quality=92)

repo = folder.parents[3]
reference = np.asarray(Image.open(repo / "DemoApp/assets/portraits/battle/gare_idle_right.png").convert("RGBA").resize((640, 960), Image.Resampling.LANCZOS))
reference_canvas = Image.new("RGBA", (640, 960), (23, 34, 31, 255))
reference_canvas.alpha_composite(Image.fromarray(reference))
reference_gray = cv2.cvtColor(np.asarray(reference_canvas.convert("RGB")), cv2.COLOR_RGB2GRAY)
metrics = {}
for name, candidate_paths in (("A80", previous_paths), ("A70", paths)):
    tops, feet, border_contacts = [], [], []
    residuals = []
    mask = np.asarray(Image.open(folder / "gare-v3-sleeve-left-mask.png"))
    points = cv2.goodFeaturesToTrack(reference_gray, 160, 0.03, 5, mask=mask)
    for path in candidate_paths:
        rgba = np.asarray(Image.open(path))
        vertical, horizontal = np.nonzero(rgba[..., 3] >= 128)
        tops.append(int(vertical.min()))
        feet.append(int(vertical.max()))
        border_contacts.append(bool(vertical.min() == 0 or vertical.max() == 959 or horizontal.min() == 0 or horizontal.max() == 639))
        gray = cv2.cvtColor(np.asarray(composite(path)), cv2.COLOR_RGB2GRAY)
        tracked, status, error = cv2.calcOpticalFlowPyrLK(reference_gray, gray, points, None, winSize=(21, 21), maxLevel=4)
        valid = (status[:, 0] != 0) & (error[:, 0] < 30)
        source_points = points[valid, 0].astype(float)
        target_points = tracked[valid, 0].astype(float)
        origin, destination = source_points.mean(axis=0), target_points.mean(axis=0)
        left, singular, right = np.linalg.svd((source_points - origin).T @ (target_points - destination))
        rotation = right.T @ left.T
        fitted = (source_points - origin) @ rotation.T + destination
        residuals.append(float(np.median(np.linalg.norm(fitted - target_points, axis=1))))
    metrics[name] = {"head_range_px": max(tops) - min(tops), "foot_rows": [min(feet), max(feet)], "border_contacts": sum(border_contacts), "sleeve_nonrigid_residual_median_px": float(np.median(residuals))}
(folder / "metrics.json").write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")
print(json.dumps(metrics, indent=2))
print("GARE_A70_PREVIEW_OK frames=30")
