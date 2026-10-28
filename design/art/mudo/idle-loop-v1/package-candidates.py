"""検品済みの候補を制作フォルダーへ保存する。ゲームへの導入はしない。"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("tags", nargs=2, help="検品済みのA/Bの出力タグ")
    args = parser.parse_args()
    dest = Path(__file__).resolve().parent
    repo = dest.parents[3]
    output = Path("D:/comfyui/output/VillainProto/idle_loop")
    source = repo / "DemoApp/assets/portraits/battle/mudo_idle_right.png"
    still = Image.open(source).convert("RGBA").resize((640, 960), Image.Resampling.LANCZOS)
    source_bottom = int(np.nonzero(np.asarray(still)[..., 3] > 128)[0].max())
    manifest = {
        "source": str(source.relative_to(repo)),
        "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "fps": 16,
        "status": "pending_user_visual_choice",
        "candidates": [],
    }
    for label, tag in zip(("A", "B"), args.tags):
        frames = sorted((output / (tag + "_rgba")).glob("*.png"))
        assert len(frames) >= 16, f"候補のコマが不足: {tag}"
        target = dest / "candidates" / label / "frames"
        target.mkdir(parents=True, exist_ok=True)
        bottoms, tops, border, body_border, green = [], [], [], [], []
        for i, frame in enumerate(frames):
            with Image.open(frame) as image:
                assert image.mode == "RGBA" and image.size == (640, 960)
                rgba = np.asarray(image).astype(int)
                alpha = rgba[..., 3] > 128
                ys, xs = np.nonzero(alpha)
                bottoms.append(int(ys.max()))
                tops.append(int(ys.min()))
                border.append(int(alpha[0].sum() + alpha[-1].sum() + alpha[:, 0].sum() + alpha[:, -1].sum()))
                # 下部の泥だまりの端への接触は記録し、頭・腕・外套本体の欠けとは分けて確認する。
                body_border.append(int(alpha[0].sum() + alpha[:768, 0].sum() + alpha[:768, -1].sum()))
                green.append(int(((rgba[..., 3] > 200) & (rgba[..., 1] - np.maximum(rgba[..., 0], rgba[..., 2]) > 20)).sum()))
            frame_target = target / f"mudo_idle_{i:02d}.png"
            assert not frame_target.exists() or frame_target.read_bytes() == frame.read_bytes(), f"別の候補へ上書きしない: {frame_target}"
            shutil.copy2(frame, frame_target)
        for suffix, name in [("_compare.gif", "compare.gif"), ("_check.jpg", "check.jpg")]:
            shutil.copy2(output / (tag + suffix), target.parent / name)
        shutil.copy2(sorted(output.glob(tag + "_loop_*.webp"))[-1], target.parent / "loop.webp")
        info = {
            "label": label, "tag": tag, "frames": len(frames), "seconds": len(frames) / 16,
            "size": [640, 960], "source_bottom": source_bottom,
            "bottom_range": [min(bottoms), max(bottoms)],
            "head_vertical_range_px": max(tops) - min(tops),
            "max_opaque_border_pixels": max(border), "max_green_pixels": max(green),
            "max_body_border_pixels": max(body_border),
            "frame_bytes": sum(p.stat().st_size for p in target.glob("*.png")),
            "preview": str((target.parent / "compare.gif").relative_to(repo)),
        }
        assert max(abs(b - source_bottom) for b in bottoms) <= 1, "接地が元絵からずれている"
        assert max(body_border) == 0, "頭・腕・外套本体がキャンバス端に達している"
        assert max(green) == 0, "緑が残っている"
        manifest["candidates"].append(info)
        print(json.dumps(info, ensure_ascii=False))
    (dest / "candidates.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
