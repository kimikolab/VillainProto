import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


folder = Path(__file__).resolve().parent
repo = folder.parents[3]
destination = repo / "DemoApp/assets/portraits/battle/idle_loop/gare"
chosen = sorted((folder / "frames").glob("*.png"))
installed = sorted(destination.glob("*.png"))
if len(chosen) != 30 or len(installed) != 30:
    raise ValueError("採用元と配置先は両方とも30コマ必要です")
rows = []
feet = []
for frame_index, (source, target) in enumerate(zip(chosen, installed)):
    if target.name != f"gare_idle_{frame_index:02d}.png":
        raise ValueError(f"連番名が不一致: {target}")
    with Image.open(source) as source_image, Image.open(target) as target_image:
        if source_image.mode != "RGBA" or target_image.mode != "RGBA" or source_image.size != (640, 960) or target_image.size != (640, 960):
            raise ValueError(f"RGBA・寸法が不一致: {target}")
        source_pixels, target_pixels = np.asarray(source_image), np.asarray(target_image)
        if not np.array_equal(source_pixels, target_pixels):
            raise ValueError(f"選択された修正版と画素が不一致: {target}")
        feet.append(int(np.nonzero(target_pixels[..., 3] > 128)[0].max()))
    rows.append({"source": source.relative_to(repo).as_posix(), "installed": target.relative_to(repo).as_posix(), "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(), "installed_sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "bytes": target.stat().st_size, "pixels_equal": True})
manifest = {"tag": "gare_A70_coherent_aligned", "date": "2026-10-20", "seed": 2022, "amplitude": 0.7, "crossfade": 6, "frames": 30, "fps": 16, "size": [640, 960], "foot_rows": [min(feet), max(feet)], "total_bytes": sum(row["bytes"] for row in rows), "files": rows}
(folder / "installed-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(f"GARE_IDLE_V3_INSTALL_OK frames=30 pixels_equal=true bytes={manifest['total_bytes']} feet={min(feet)}..{max(feet)}")
