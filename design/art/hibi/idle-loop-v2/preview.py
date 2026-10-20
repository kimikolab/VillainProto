import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


folder = Path(__file__).resolve().parent
repo = folder.parents[3]
sys.path.insert(0, str(repo / "tools/comfy_idle_loop"))
import idle_loop


parser = argparse.ArgumentParser()
parser.add_argument("tags", nargs="+")
args = parser.parse_args()
background_color = (23, 34, 31, 255)
with Image.open(repo / "DemoApp/assets/portraits/battle/hibi_idle_right.png") as image:
    still = image.convert("RGBA").resize((640, 960), Image.Resampling.LANCZOS)
still_values = np.asarray(still)
still_bottom = int(np.nonzero(still_values[..., 3] > 128)[0].max())


def composite(image):
    canvas = Image.new("RGBA", image.size, background_color)
    canvas.alpha_composite(image)
    return canvas.convert("RGB")


def lean_and_top(alpha):
    vertical, horizontal = np.nonzero(alpha)
    top = int(vertical.min())
    head_vertical, head_horizontal = np.nonzero(alpha[top:top + int(alpha.shape[0] * 0.08)])
    waist_vertical, waist_horizontal = np.nonzero(alpha[int(alpha.shape[0] * 0.45):int(alpha.shape[0] * 0.60)])
    return float(np.percentile(head_horizontal, 75) - waist_horizontal.mean()), top, int(vertical.max())


def save_gif(frames, target):
    swatches = Image.new("RGB", (1280, 960))
    for sample_index in range(16):
        frame = frames[min(len(frames) - 1, sample_index * len(frames) // 16)]
        swatches.paste(frame.resize((320, 240)), ((sample_index % 4) * 320, (sample_index // 4) * 240))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    durations = [round((frame_index + 1) * 6.25) * 10 - round(frame_index * 6.25) * 10 for frame_index in range(len(frames))]
    indexed[0].save(target, save_all=True, append_images=indexed[1:], duration=durations, loop=0, optimize=False)
    with Image.open(target) as result:
        if result.n_frames != len(frames):
            raise ValueError(f"GIFのコマ数が変わっています: {target}")


metrics_path = folder / "metrics.json"
metrics = json.loads(metrics_path.read_text(encoding="utf-8")) if metrics_path.exists() else {}
jobs = json.loads((folder / "jobs.json").read_text(encoding="utf-8"))
original_jobs = json.loads((folder.parent / "idle-loop-v1/jobs.json").read_text(encoding="utf-8"))
for tag in args.tags:
    job = next(job for job in jobs + original_jobs if job["tag"] == tag)
    result = idle_loop.http_json(f"/history/{job['prompt_id']}").get(job["prompt_id"])
    if result is None or result["status"]["status_str"] != "success":
        raise RuntimeError(f"保存が完了していません: {tag}")
    workflow_path = folder / f"{tag}-workflow.json"
    if not workflow_path.exists():
        workflow_path = folder.parent / f"idle-loop-v1/hibi-s{job['seed']}-workflow.json"
    graph = json.loads(workflow_path.read_text(encoding="utf-8"))
    if any(node["class_type"] == "VPScaleIdleMotion" for node in graph.values()):
        raise ValueError("元絵を変形するノードはv2では使用しません")
    paths = sorted((Path(idle_loop.OUT) / f"{tag}_rgba").glob("*.png"))
    if not 16 <= len(paths) <= 64:
        raise ValueError(f"コマ数を確認してください: {tag} {len(paths)}")
    target = folder / ("candidate-natural" if tag == "hibi_s2022" else f"native-s{job['seed']}")
    frame_folder = target / "frames"
    frame_folder.mkdir(parents=True, exist_ok=True)
    (target / "workflow.json").write_text(json.dumps(graph, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (target / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if set(path.name for path in frame_folder.glob("*.png")) - set(path.name for path in paths):
        raise ValueError("出力先に別の連番があります")
    sequence, pixels, colored, tops, feet, leans, shoe_boxes = [], [], [], [], [], [], []
    border_contacts = 0
    green_maximum = 0
    manifest = []
    for frame_index, path in enumerate(paths):
        destination = frame_folder / path.name
        shutil.copy2(path, destination)
        source_hash = hashlib.sha256(path.read_bytes()).hexdigest()
        if hashlib.sha256(destination.read_bytes()).hexdigest() != source_hash:
            raise ValueError("元の生成コマと一致していません")
        manifest.append({"file": destination.name, "sha256": source_hash})
        with Image.open(destination) as source:
            if source.mode != "RGBA" or source.size != (640, 960):
                raise ValueError(f"画像形式不一致: {path}")
            frame = source.copy()
        values = np.asarray(frame)
        foreground = values[..., 3] > 128
        vertical, horizontal = np.nonzero(foreground)
        lean, top, bottom = lean_and_top(foreground)
        tops.append(int(top))
        feet.append(int(bottom))
        leans.append(float(lean))
        border_contacts += bool(vertical.min() == 0 or vertical.max() == 959 or horizontal.min() == 0 or horizontal.max() == 639)
        green_maximum = max(green_maximum, int(((values[..., 3] > 200) & (values[..., 1].astype(int) - np.maximum(values[..., 0], values[..., 2]) > 20)).sum()))
        row = []
        for bounds in ((30, 790, 140, 885), (350, 680, 470, 780)):
            left, upper, right, lower = bounds
            shoe_y, shoe_x = np.nonzero(foreground[upper:lower, left:right])
            row.append([int(shoe_x.min() + left), int(shoe_y.min() + upper), int(shoe_x.max() + left), int(shoe_y.max() + upper)])
        shoe_boxes.append(row)
        sequence.append(frame)
        pixels.append(values)
        colored.append(composite(frame))
    differences = [float(np.abs(pixels[frame_index + 1][..., :3].astype(float) - pixels[frame_index][..., :3]).mean()) for frame_index in range(len(pixels) - 1)]
    wrap = float(np.abs(pixels[0][..., :3].astype(float) - pixels[-1][..., :3]).mean())
    metrics[tag] = {
        "seed": job["seed"], "frames": len(paths), "fps": 16, "duration_seconds": len(paths) / 16,
        "head_range_px": max(tops) - min(tops), "foot_rows": [min(feet), max(feet)], "source_foot_row": still_bottom,
        "lean_range_px": max(leans) - min(leans), "border_contacts": int(border_contacts), "green_pixels_maximum": green_maximum,
        "seam_ratio": wrap / max(float(np.median(differences)), 1e-6), "shoe_boxes": shoe_boxes,
        "geometry_postprocessing": False, "native_frame_hashes": manifest,
        "source_prompt_id": job["prompt_id"], "reused_original_generated_frames": tag == "hibi_s2022",
        "loop_report": result.get("outputs", {}).get("45", {}).get("text", []),
    }
    sequence[0].save(target / "loop.webp", save_all=True, append_images=sequence[1:], duration=[round((frame_index + 1) * 62.5) - round(frame_index * 62.5) for frame_index in range(len(sequence))], loop=0, lossless=True)
    save_gif([frame.resize((480, 720), Image.Resampling.LANCZOS) for frame in colored], target / "loop.gif")
    save_gif([frame.crop((80, 105, 615, 640)).resize((536, 536), Image.Resampling.LANCZOS) for frame in colored], target / "detail.gif")
    comparison = []
    for frame in colored:
        canvas = Image.new("RGB", (1280, 960), background_color[:3])
        canvas.paste(composite(still), (0, 0))
        canvas.paste(frame, (640, 0))
        ImageDraw.Draw(canvas).text((20, 24), "STILL", fill="white")
        ImageDraw.Draw(canvas).text((660, 24), f"RETAKE / {tag}", fill="white")
        comparison.append(canvas.resize((768, 576), Image.Resampling.LANCZOS))
    save_gif(comparison, target / "still-vs-loop.gif")
    sheet = Image.new("RGB", (1920, 1440), background_color[:3])
    for sample_index in range(8):
        frame_index = sample_index * len(colored) // 8
        tile = colored[frame_index].resize((480, 720), Image.Resampling.LANCZOS)
        ImageDraw.Draw(tile).text((12, 12), str(frame_index), fill="white")
        sheet.paste(tile, ((sample_index % 4) * 480, (sample_index // 4) * 720))
    sheet.save(target / "contact.jpg", quality=93)
    (target / "metrics.json").write_text(json.dumps(metrics[tag], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in metrics[tag].items() if key not in ("native_frame_hashes", "shoe_boxes")}, ensure_ascii=False, indent=2))
(folder / "metrics.json").write_text(json.dumps(metrics, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("HIBI_NATIVE_V2_PREVIEW_OK", flush=True)
