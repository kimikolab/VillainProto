"""検品済み連番と比較画像をリポジトリ内へ保存する。ゲームには適用しない。"""
import hashlib
import json
import shutil
from pathlib import Path
import numpy as np
from PIL import Image

DEST = Path(__file__).resolve().parent
REPO = DEST.parents[3]
OUT = Path("D:/comfyui/output/VillainProto/idle_loop")
source = REPO / "DemoApp/assets/portraits/battle/utsu_idle_right.png"
manifest = {"source": str(source.relative_to(REPO)), "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            "fps": 16, "status": "pending_user_visual_choice", "candidates": []}
for label, tag, seed in [("A", "utsu_idle_v1_s2022_final", 2022), ("B", "utsu_idle_v1_s5055_final", 5055)]:
    frames = sorted((OUT / (tag + "_rgba")).glob("*.png"))
    target = DEST / "candidates" / label / "frames"
    target.mkdir(parents=True, exist_ok=True)
    bottoms, tops, border = [], [], []
    for i, frame in enumerate(frames):
        with Image.open(frame) as image:
            assert image.mode == "RGBA" and image.size == (640, 960)
            alpha = np.asarray(image)[..., 3] > 128
            ys, xs = np.nonzero(alpha)
            bottoms.append(int(ys.max()))
            tops.append(int(ys.min()))
            border.append(int(alpha[0].sum() + alpha[-1].sum() + alpha[:, 0].sum() + alpha[:, -1].sum()))
        shutil.copy2(frame, target / ("utsu_idle_%02d.png" % i))
    for suffix, output in [("_compare.gif", "compare.gif"), ("_check.jpg", "check.jpg")]:
        shutil.copy2(OUT / (tag + suffix), target.parent / output)
    webp = sorted(OUT.glob(tag + "_loop_*.webp"))[-1]
    shutil.copy2(webp, target.parent / "loop.webp")
    info = {"label": label, "tag": tag, "seed": seed, "frames": len(frames), "seconds": len(frames) / 16,
            "size": [640, 960], "bottom_range": [min(bottoms), max(bottoms)], "head_vertical_range_px": max(tops) - min(tops),
            "max_opaque_border_pixels": max(border), "crossfade": 6,
            "frame_bytes": sum(frame.stat().st_size for frame in target.glob("*.png")),
            "preview": str((target.parent / "compare.gif").relative_to(REPO))}
    assert min(bottoms) == max(bottoms) == 926
    assert max(border) == 0, "刃先または全身が画面端にかかっている"
    manifest["candidates"].append(info)
    print(json.dumps(info, ensure_ascii=False))
(DEST / "candidates.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
