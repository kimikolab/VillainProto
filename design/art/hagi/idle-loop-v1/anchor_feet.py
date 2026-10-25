"""接地行だけを揃える。コマ全体を整数画素で平行移動し、伸縮・補間・部分変形はしない。"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--bottom", type=int, default=940)
    args = parser.parse_args()
    files = sorted(args.source.glob("*.png"))
    if not files:
        raise SystemExit("元の連番がありません")
    args.destination.mkdir(parents=True, exist_ok=True)
    records = []
    for index, path in enumerate(files):
        image = Image.open(path).convert("RGBA")
        original = np.asarray(image)
        bottom = int(np.nonzero(original[..., 3] > 128)[0].max())
        dy = args.bottom - bottom
        translated = Image.new("RGBA", image.size)
        # maskを使わずRGBAをそのまま写す。二度のアルファ乗算を避ける。
        translated.paste(image, (0, dy))
        result = np.asarray(translated)
        if np.count_nonzero(result[..., 3] > 128) != np.count_nonzero(original[..., 3] > 128):
            raise ValueError("平行移動で不透明部分がキャンバスから切れました")
        if int(np.nonzero(result[..., 3] > 128)[0].max()) != args.bottom:
            raise ValueError("接地行を揃えられませんでした")
        translated.save(args.destination / f"frame_{index:03d}.png", optimize=True)
        records.append({"source": path.name, "original_bottom": bottom, "dy": dy})
    report = {"method": "whole-frame integer vertical translation", "scaling": False,
              "warping": False, "interpolation": False, "target_bottom": args.bottom,
              "frames": records}
    (args.destination / "anchor.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"{len(files)}コマ / 接地行 {args.bottom} / 上下移動 {min(r['dy'] for r in records)}〜{max(r['dy'] for r in records)}px")


if __name__ == "__main__":
    main()
