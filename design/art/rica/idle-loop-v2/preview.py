"""候補Bを固有の39コマ周期のまま表示する。連番の画素は変更しない。"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
SOURCE = HERE.parent / "idle-loop-v1/B/frames"
BACKGROUND = (23, 34, 31, 255)
paths = sorted(SOURCE.glob("*.png"))
if len(paths) != 39:
    raise SystemExit(f"候補Bの固有周期を確認してください: {len(paths)}")
frames = [Image.open(path).convert("RGBA") for path in paths]


def composite(frame, size):
    canvas = Image.new("RGBA", size, BACKGROUND)
    canvas.alpha_composite(frame.resize(size, Image.Resampling.LANCZOS))
    return canvas.convert("RGB")


def save_gif(images, destination):
    swatches = Image.new("RGB", (1280, 960))
    for index in range(16):
        swatches.paste(images[index * len(images) // 16].resize((320, 240)), ((index % 4) * 320, (index // 4) * 240))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in images]
    durations = [round((index + 1) * 6.25) * 10 - round(index * 6.25) * 10 for index in range(len(images))]
    indexed[0].save(destination, save_all=True, append_images=indexed[1:], duration=durations, loop=0, optimize=False)
    with Image.open(destination) as result:
        if result.n_frames != len(images):
            raise ValueError("プレビューの周期が変わっています")
        rendered = []
        actual_durations = []
        for index in range(result.n_frames):
            result.seek(index)
            rendered.append(np.asarray(result.convert("RGB")).astype(float))
            actual_durations.append(result.info["duration"])
    steps = [float(np.abs(rendered[index + 1] - rendered[index]).mean()) for index in range(len(rendered) - 1)]
    wrap = float(np.abs(rendered[-1] - rendered[0]).mean())
    return {"frames": len(rendered), "duration_ms": sum(actual_durations), "wrap_ratio": wrap / float(np.median(steps)), "wrap_pair": [len(rendered) - 1, 0]}


preview = [composite(frame, (420, 630)) for frame in frames]
metrics = save_gif(preview, HERE / "B-loop.gif")
still = Image.open(HERE.parents[3] / "DemoApp/assets/portraits/battle/rica_idle_right.png").convert("RGBA").resize((320, 480), Image.Resampling.LANCZOS)
comparison = []
for frame in frames:
    canvas = Image.new("RGBA", (640, 480), BACKGROUND)
    canvas.alpha_composite(still)
    canvas.alpha_composite(frame.resize((320, 480), Image.Resampling.LANCZOS), (320, 0))
    comparison.append(canvas.convert("RGB"))
save_gif(comparison, HERE / "still-vs-B.gif")
font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)
sheet = Image.new("RGB", (1200, 328), BACKGROUND[:3])
for column, index in enumerate((36, 37, 38, 0, 1, 2)):
    sheet.paste(composite(frames[index], (200, 300)), (column * 200, 28))
    ImageDraw.Draw(sheet).text((column * 200 + 8, 4), str(index), fill="white", font=font)
sheet.save(HERE / "seam-contact.jpg", quality=92)
metrics["source_tag"] = "rica_s5055"
metrics["source_frames"] = [{"file": str(path.relative_to(HERE.parent)), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()} for path in paths]
(HERE / "metrics.json").write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")
print(json.dumps({key: value for key, value in metrics.items() if key != "source_frames"}, indent=2))
