"""ComfyUI の生成物を保存し、静止画・候補A/Bの比較GIFを作る。"""
import json
import math
import shutil
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO / "tools/comfy_idle_loop"))
import idle_loop as pipeline


def main():
    tags = sys.argv[1:]
    if len(tags) != 2:
        raise SystemExit("候補AとBのタグを指定してください")
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)
    source = REPO / "DemoApp/assets/portraits/battle/rica_idle_right.png"
    still = Image.open(source).convert("RGBA").resize((320, 480), Image.Resampling.LANCZOS)
    candidates = []
    for label, tag in zip(("A", "B"), tags):
        destination = HERE / label
        destination.mkdir(exist_ok=True)
        files = pipeline.frames_of(tag)
        frames_dir = destination / "frames"
        frames_dir.mkdir(exist_ok=True)
        if list(frames_dir.glob("*.png")):
            raise SystemExit(f"保存先に連番が既にあります: {frames_dir}")
        for index, path in enumerate(files):
            shutil.copy2(path, frames_dir / f"rica_idle_{index:02d}.png")
        for suffix in ("check.jpg", "compare.gif"):
            shutil.copy2(Path(pipeline.OUT) / f"{tag}_{suffix}", destination / suffix)
        webps = sorted(Path(pipeline.OUT).glob(f"{tag}_loop_*.webp"))
        if webps:
            shutil.copy2(webps[-1], destination / "loop.webp")
        original_tag = tag.removesuffix("_final")
        raws = sorted(Path(pipeline.OUT).glob(f"{original_tag}_raw_*.mp4"))
        if raws:
            shutil.copy2(raws[-1], destination / "source.mp4")
        frames = [Image.open(path).convert("RGBA").resize((320, 480), Image.Resampling.LANCZOS) for path in files]
        candidates.append(frames)
        (destination / "candidate.json").write_text(json.dumps({"label": label, "tag": tag, "frames": len(files), "fps": 16}, indent=2) + "\n", encoding="utf-8")
    preview = []
    period = math.lcm(*(len(frames) for frames in candidates))
    # 固有周期の途中でGIFを打ち切ると、プレビュー自身に偽の継ぎ目が生じる。
    # 共通周期が長すぎる候補は個別のcompare.gifで見比べる。
    for index in range(period if period <= 160 else 1):
        canvas = Image.new("RGBA", (960, 512), (23, 34, 31, 255))
        draw = ImageDraw.Draw(canvas)
        for x, title in enumerate(("STILL", f"A / {tags[0]}", f"B / {tags[1]}")):
            draw.text((x * 320 + 12, 6), title, fill="white", font=font)
        canvas.alpha_composite(still, (0, 32))
        for column, frames in enumerate(candidates, 1):
            canvas.alpha_composite(frames[index % len(frames)], (column * 320, 32))
        preview.append(canvas.convert("RGB"))
    preview[0].save(HERE / "comparison.jpg", quality=92)
    if period > 160:
        print("共通周期が長いため比較GIFは作成しません。A/compare.gif と B/compare.gif を使用してください。")
        return
    swatches = Image.new("RGB", (960, 512 * 8))
    for index in range(8):
        swatches.paste(preview[index * len(preview) // 8], (0, index * 512))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    gif = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in preview]
    durations = [round((index + 1) * 6.25) * 10 - round(index * 6.25) * 10 for index in range(len(gif))]
    gif[0].save(HERE / "comparison.gif", save_all=True, append_images=gif[1:], duration=durations, loop=0, optimize=False)
    print("比較GIF保存:", HERE / "comparison.gif")


if __name__ == "__main__":
    main()
