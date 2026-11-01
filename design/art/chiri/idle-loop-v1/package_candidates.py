"""検品済みのチリ待機A/Bを保存し、同じ倍率・接地位置の比較GIFを作る。"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil

import av
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
OUTPUT = Path("D:/comfyui/output/VillainProto/idle_loop")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("tags", nargs=2)
    args = parser.parse_args()
    source = REPO / "DemoApp/assets/portraits/battle/chiri_idle_right.png"
    expected = json.loads((HERE / "generation.json").read_text(encoding="utf-8"))["source_sha256"]
    assert hashlib.sha256(source.read_bytes()).hexdigest() == expected, "生成後に元絵が変更されている"
    still = Image.open(source).convert("RGBA").resize((640, 960), Image.Resampling.LANCZOS)
    source_bottom = int(np.nonzero(np.asarray(still)[..., 3] > 128)[0].max())
    manifest = {"source": str(source.relative_to(REPO)), "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                "status": "pending_user_visual_choice", "fps": 16, "candidates": []}
    sequences = []
    for label, tag in zip(("A", "B"), args.tags):
        files = sorted((OUTPUT / (tag + "_rgba")).glob("*.png"))
        assert len(files) >= 16, f"候補が不足: {tag}"
        dest = HERE / "candidates" / label
        frames = dest / "frames"
        frames.mkdir(parents=True, exist_ok=True)
        bottoms, tops, border, green = [], [], [], []
        sequence = []
        for i, path in enumerate(files):
            image = Image.open(path).convert("RGBA")
            assert image.size == (640, 960)
            rgba = np.asarray(image).astype(int)
            alpha = rgba[..., 3] > 128
            ys, xs = np.nonzero(alpha)
            bottoms.append(int(ys.max())); tops.append(int(ys.min()))
            border.append(int(alpha[0].sum() + alpha[-1].sum() + alpha[:, 0].sum() + alpha[:, -1].sum()))
            green.append(int(((rgba[..., 3] > 200) & (rgba[..., 1] - np.maximum(rgba[..., 0], rgba[..., 2]) > 20)).sum()))
            target = frames / f"chiri_idle_{i:02d}.png"
            assert not target.exists() or target.read_bytes() == path.read_bytes(), f"別候補で上書きしない: {target}"
            shutil.copy2(path, target)
            sequence.append(image.resize((320, 480), Image.Resampling.LANCZOS))
        assert max(abs(b - source_bottom) for b in bottoms) <= 1, f"接地が元絵からずれた: {bottoms}"
        assert max(border) == 0, "体がキャンバス端に達した"
        assert max(green) == 0, "緑の抜け残り"
        for suffix, name in [("_compare.gif", "compare.gif"), ("_check.jpg", "check.jpg")]:
            shutil.copy2(OUTPUT / (tag + suffix), dest / name)
        shutil.copy2(sorted(OUTPUT.glob(tag + "_loop_*.webp"))[-1], dest / "loop.webp")
        raw_tag = tag.removesuffix("_final")
        shutil.copy2(sorted(OUTPUT.glob(raw_tag + "_raw_*.mp4"))[-1], dest / "source.mp4")
        info = {"label": label, "tag": tag, "frames": len(files), "seconds": len(files) / 16,
                "size": [640, 960], "source_bottom": source_bottom, "bottom_range": [min(bottoms), max(bottoms)],
                "head_vertical_range_px": max(tops) - min(tops), "max_opaque_border_pixels": max(border),
                "max_green_pixels": max(green), "frame_bytes": sum(p.stat().st_size for p in frames.glob("*.png"))}
        manifest["candidates"].append(info)
        sequences.append(sequence)
        print(json.dumps(info, ensure_ascii=False))
    # GIF自体の折り返しでも両候補が周期を閉じる長さを使う。
    # 最小公倍数が長すぎる場合は、各候補の比較GIFと6秒の比較動画を使う。
    period = math.lcm(*(len(sequence) for sequence in sequences))
    gif_count = period * math.ceil(96 / period) if period <= 240 else 0
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 23)
    small = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 14)
    comparison = []
    for frame in range(max(96, gif_count)):
        canvas = Image.new("RGBA", (960, 530), (28, 37, 41, 255))
        draw = ImageDraw.Draw(canvas)
        for x, title in [(0, "REFERENCE"), (320, "A"), (640, "B")]:
            draw.text((x + 16, 8), title, font=font, fill=(255, 209, 137))
            draw.line((x + 10, 518, x + 310, 518), fill=(92, 111, 116))
        canvas.alpha_composite(still.resize((320, 480), Image.Resampling.LANCZOS), (0, 44))
        for i, sequence in enumerate(sequences):
            canvas.alpha_composite(sequence[frame % len(sequence)], ((i + 1) * 320, 44))
            draw.text(((i + 1) * 320 + 51, 14), f"{len(sequence)}f / {len(sequence) / 16:.2f}s", font=small, fill=(193, 207, 211))
        comparison.append(canvas.convert("RGB"))
    swatches = Image.new("RGB", (960 * 4, 530 * 4))
    for i in range(16):
        swatches.paste(comparison[i * 6], ((i % 4) * 960, (i // 4) * 530))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    if gif_count:
        indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in comparison[:gif_count]]
        indexed[0].save(HERE / "comparison.gif", save_all=True, append_images=indexed[1:],
                        duration=[(60, 60, 60, 70)[i % 4] for i in range(gif_count)], loop=0, optimize=False)
    with av.open(str(HERE / "comparison.mp4"), "w") as output:
        video = output.add_stream("libx264", rate=16)
        video.width, video.height, video.pix_fmt = 960, 530, "yuv420p"
        video.options = {"crf": "20", "preset": "fast"}
        for i, image in enumerate(comparison[:96]):
            frame = av.VideoFrame.from_image(image)
            frame.pts = i
            for packet in video.encode(frame):
                output.mux(packet)
        for packet in video.encode(None):
            output.mux(packet)
    (HERE / "candidates.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
