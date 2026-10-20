from pathlib import Path

from PIL import Image, ImageDraw


folder = Path(__file__).resolve().parent
original_paths = sorted((folder.parent / "idle-loop-v1" / "A-frames").glob("*.png"))
revised_paths = sorted((folder / "A80-frames").glob("*.png"))
if len(original_paths) != 30 or len(revised_paths) != 30:
    raise ValueError("比較する両方の連番に30コマ必要です")

comparison = []
for original_path, revised_path in zip(original_paths, revised_paths):
    with Image.open(original_path) as original, Image.open(revised_path) as revised:
        if original.size != (640, 960) or revised.size != (640, 960):
            raise ValueError("比較する画像の寸法が一致しません")
        canvas = Image.new("RGBA", (1280, 960), (23, 34, 31, 255))
        canvas.alpha_composite(original.convert("RGBA"), (0, 0))
        canvas.alpha_composite(revised.convert("RGBA"), (640, 0))
        preview = canvas.convert("RGB").resize((640, 480), Image.Resampling.LANCZOS)
        labels = ImageDraw.Draw(preview)
        labels.text((12, 8), "A: original", fill=(244, 240, 221))
        labels.text((332, 8), "A80: reduced bounce", fill=(244, 240, 221))
        comparison.append(preview)

swatches = Image.new("RGB", (1280, 960))
for sample_index in range(16):
    sample = comparison[min(len(comparison) - 1, sample_index * len(comparison) // 16)]
    swatches.paste(sample.resize((320, 240)), ((sample_index % 4) * 320, (sample_index // 4) * 240))
palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
indexed = [preview.quantize(palette=palette, dither=Image.Dither.NONE) for preview in comparison]
durations = [round((frame_index + 1) * 6.25) * 10 - round(frame_index * 6.25) * 10 for frame_index in range(30)]
indexed[0].save(folder / "A-vs-A80.gif", save_all=True, append_images=indexed[1:], duration=durations, loop=0, optimize=False)
print("GARE_A80_PREVIEW_OK frames=30")
