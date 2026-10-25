"""生成した待機の固有周期を保ち、採用前の比較用GIFと透過連番を保存する。"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
COMFY = Path("D:/comfyui/output/VillainProto/idle_loop")
BACKGROUND = (23, 34, 31, 255)
FPS = 16


def composite(image, size):
    canvas = Image.new("RGBA", size, BACKGROUND)
    canvas.alpha_composite(image.resize(size, Image.Resampling.LANCZOS))
    return canvas.convert("RGB")


def save_gif(images, destination):
    swatches = Image.new("RGB", (1280, 960))
    for index in range(16):
        pick = images[min(len(images) - 1, index * len(images) // 16)]
        swatches.paste(pick.resize((320, 240)), ((index % 4) * 320, (index // 4) * 240))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    indexed = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in images]
    durations = [round((index + 1) * 100 / FPS) * 10 - round(index * 100 / FPS) * 10 for index in range(len(images))]
    indexed[0].save(destination, save_all=True, append_images=indexed[1:], duration=durations, loop=0, optimize=False)
    # 提示するGIF自身の末尾→先頭を読み戻す。候補の途中で切らない。
    rendered, actual_durations = [], []
    with Image.open(destination) as result:
        if result.n_frames != len(images):
            raise ValueError(f"プレビューのコマ数が変わっています: {destination}")
        for index in range(result.n_frames):
            result.seek(index)
            rendered.append(np.asarray(result.convert("RGB")).astype(float))
            actual_durations.append(result.info["duration"])
    steps = [float(np.abs(rendered[index + 1] - rendered[index]).mean()) for index in range(len(rendered) - 1)]
    wrap = float(np.abs(rendered[-1] - rendered[0]).mean())
    return {"frames": len(rendered), "duration_ms": sum(actual_durations),
            "wrap_ratio": wrap / max(float(np.median(steps)), 1e-6), "wrap_pair": [len(rendered) - 1, 0]}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("tag")
    parser.add_argument("candidate", choices=("A", "B", "C"))
    parser.add_argument("--crossfade", type=int, default=0)
    parser.add_argument("--source-dir", type=Path)
    parser.add_argument("--anchor-report", type=Path)
    args = parser.parse_args()
    source_dir = args.source_dir or COMFY / f"{args.tag}_rgba"
    paths = sorted(source_dir.glob("*.png"))
    if not paths:
        raise SystemExit(f"透過連番がありません: {args.tag}")
    frames = [Image.open(path).convert("RGBA") for path in paths]
    if any(frame.size != (640, 960) for frame in frames):
        raise ValueError("連番の寸法を確認してください")
    dst = HERE / args.candidate
    (dst / "frames").mkdir(parents=True, exist_ok=True)
    old = list((dst / "frames").glob("*.png"))
    if old and len(old) != len(paths):
        raise ValueError("別の周期の連番が既にあります。別の候補フォルダーを使ってください")
    hashes = []
    for index, path in enumerate(paths):
        target = dst / "frames" / f"frame_{index:03d}.png"
        shutil.copy2(path, target)
        hashes.append({"file": str(target.relative_to(HERE)), "sha256": hashlib.sha256(target.read_bytes()).hexdigest()})
    loop_metrics = save_gif([composite(frame, (420, 630)) for frame in frames], HERE / f"{args.candidate}-loop.gif")
    still_path = REPO / "DemoApp/assets/portraits/battle/hagi_idle_right.png"
    still = Image.open(still_path).convert("RGBA").resize((320, 480), Image.Resampling.LANCZOS)
    comparison = []
    for frame in frames:
        canvas = Image.new("RGBA", (640, 480), BACKGROUND)
        canvas.alpha_composite(still)
        canvas.alpha_composite(frame.resize((320, 480), Image.Resampling.LANCZOS), (320, 0))
        comparison.append(canvas.convert("RGB"))
    comparison_metrics = save_gif(comparison, HERE / f"still-vs-{args.candidate}.gif")
    # 関節と握りの確認用。元のコマを変形せず、代表コマと境界の前後を並べる。
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)
    for name, picks in (("contact", [0, len(frames) // 4, len(frames) // 2, 3 * len(frames) // 4]),
                        ("seam", [(len(frames) - 3 + index) % len(frames) for index in range(6)])):
        sheet = Image.new("RGB", (240 * len(picks), 388), BACKGROUND[:3])
        for column, index in enumerate(picks):
            sheet.paste(composite(frames[index], (240, 360)), (column * 240, 28))
            ImageDraw.Draw(sheet).text((column * 240 + 8, 4), f"{args.candidate} / {index}", fill="white", font=font)
        sheet.save(HERE / f"{args.candidate}-{name}.jpg", quality=94)
    picks = [0, len(frames) // 4, len(frames) // 2, 3 * len(frames) // 4]
    detail = Image.new("RGB", (240 * len(picks), 3 * 268), BACKGROUND[:3])
    for row, (label, box) in enumerate((("lower grip", (205, 320, 345, 470)),
                                      ("upper grip", (390, 170, 500, 320)),
                                      ("blade root", (475, 25, 620, 190)))):
        for column, index in enumerate(picks):
            crop = composite(frames[index].crop(box), (240, 240))
            detail.paste(crop, (column * 240, row * 268 + 28))
            ImageDraw.Draw(detail).text((column * 240 + 8, row * 268 + 4), f"{label} / {index}", fill="white", font=font)
    detail.save(HERE / f"{args.candidate}-grip-and-blade.jpg", quality=94)
    for suffix in ("_check.jpg",):
        source = COMFY / f"{args.tag}{suffix}"
        if source.exists():
            shutil.copy2(source, dst / source.name)
    prompt = json.loads((HERE / "prompt.json").read_text(encoding="utf-8"))
    alphas = [np.asarray(frame)[..., 3] for frame in frames]
    bottoms = [int(np.nonzero(alpha > 128)[0].max()) for alpha in alphas]
    border_hits = [int(sum(np.count_nonzero(edge > 128) for edge in (alpha[0], alpha[-1], alpha[:, 0], alpha[:, -1]))) for alpha in alphas]
    metadata = {"status": "candidate-awaiting-user-selection", "tag": args.tag, "fps": FPS,
                "crossfade_frames": args.crossfade, "bottom_range": [min(bottoms), max(bottoms)],
                "maximum_border_pixels": max(border_hits),
                "geometry_postprocessing": bool(args.anchor_report), "still": str(still_path.relative_to(REPO)),
                "still_sha256": hashlib.sha256(still_path.read_bytes()).hexdigest(),
                "prompt": prompt, "loop_gif": loop_metrics, "comparison_gif": comparison_metrics, "frames": hashes}
    if args.anchor_report:
        metadata["foot_anchor"] = json.loads(args.anchor_report.read_text(encoding="utf-8"))
    (dst / "manifest.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"candidate": args.candidate, "tag": args.tag, **loop_metrics}, ensure_ascii=False))


if __name__ == "__main__":
    main()
