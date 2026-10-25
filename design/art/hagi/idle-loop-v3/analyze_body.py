"""候補の身体と口元を確認する。確認用の顔の位置合わせは連番本体には適用しない。"""
import argparse
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

from package_previews import composite, save_gif


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("frames", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--offset-y", type=int, default=0, help="最初のコマの構えに合わせた確認窓の縦位置")
    args = parser.parse_args()
    files = sorted(args.frames.glob("*.png"))
    if not files:
        raise SystemExit("連番がありません")
    frames = [Image.open(file).convert("RGBA") for file in files]
    images = [np.asarray(composite(frame, (640, 960))) for frame in frames]
    gray = [cv2.cvtColor(image, cv2.COLOR_RGB2GRAY) for image in images]
    boxes = {"eyes": (260, 135, 328, 175), "shoulder": (252, 230, 292, 266), "waist": (305, 380, 343, 415)}
    tracks = {}
    for label, (x0, y0, x1, y1) in boxes.items():
        y0 += args.offset_y
        y1 += args.offset_y
        template = gray[0][y0:y1, x0:x1]
        rows = []
        for image in gray:
            search = image[y0 - 45:y1 + 45, x0 - 40:x1 + 40]
            result = cv2.matchTemplate(search, template, cv2.TM_CCOEFF_NORMED)
            _, score, _, at = cv2.minMaxLoc(result)
            rows.append({"dx": at[0] - 40, "dy": at[1] - 45, "score": score})
        tracks[label] = {"vertical_range": max(row["dy"] for row in rows) - min(row["dy"] for row in rows),
                         "horizontal_range": max(row["dx"] for row in rows) - min(row["dx"] for row in rows),
                         "minimum_score": min(row["score"] for row in rows), "frames": rows}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.with_suffix(".json").write_text(json.dumps(tracks, indent=2) + "\n", encoding="utf-8")
    # 頭とともに動く顔の確認窓。元の頭や口を固定レイヤーで上貼りしない。
    faces = []
    for image, row in zip(images, tracks["eyes"]["frames"]):
        dx, dy = row["dx"], row["dy"]
        face = Image.fromarray(image).crop((258 + dx, 125 + args.offset_y + dy, 348 + dx, 215 + args.offset_y + dy))
        faces.append(face.resize((360, 360), Image.Resampling.LANCZOS))
    save_gif(faces, args.output.with_name(args.output.name + "-face.gif"))
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)
    sheet = Image.new("RGB", (1200, 464), (23, 34, 31))
    for index in range(12):
        at = min(len(faces) - 1, index * len(faces) // 12)
        x, y = index % 6 * 200, index // 6 * 232
        sheet.paste(faces[at].resize((200, 200)), (x, y + 28))
        ImageDraw.Draw(sheet).text((x + 8, y + 3), str(at), font=font, fill="white")
    sheet.save(args.output.with_name(args.output.name + "-face.jpg"), quality=94)
    print(json.dumps({key: {name: value for name, value in item.items() if name != "frames"} for key, item in tracks.items()}))


if __name__ == "__main__":
    main()
