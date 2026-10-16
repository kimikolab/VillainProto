"""ガルド専用の目視補助。兜・剣の局所移動を追い、全画面平均が隠す継ぎ目を記録する。"""
import argparse
import json
from pathlib import Path

import av
import cv2
import numpy as np
from PIL import Image


REGIONS = {
    "helmet": (302, 52, 399, 176),
    "sword_hand": (42, 389, 145, 481),
    "sword_tip": (494, 650, 560, 720),
    "shield_top": (482, 142, 580, 238),
    "cloth": (208, 694, 300, 793),
}


def load_frames(path):
    if path.is_dir():
        return [np.asarray(Image.open(p).convert("RGBA")) for p in sorted(path.glob("*.png"))]
    with av.open(str(path)) as video:
        return [f.to_ndarray(format="rgba") for f in video.decode(video=0)]


def track(images, box):
    gray = [cv2.cvtColor(im[:, :, :3], cv2.COLOR_RGB2GRAY) for im in images]
    first = images[0]
    mask = np.zeros(gray[0].shape, np.uint8)
    x0, y0, x1, y1 = box
    mask[y0:y1, x0:x1] = 255
    # 緑背景や透過部分に点を取らない。
    rgb = first[:, :, :3].astype(np.int16)
    mask[(first[:, :, 3] < 200) | (rgb[:, :, 1] - np.maximum(rgb[:, :, 0], rgb[:, :, 2]) > 30)] = 0
    points = cv2.goodFeaturesToTrack(gray[0], 45, 0.015, 5, mask=mask, blockSize=5)
    if points is None or len(points) < 4:
        return {"error": "追跡点不足"}
    origin = points.copy()
    alive = np.ones(len(points), bool)
    displacements = [[0.0, 0.0]]
    support = [int(alive.sum())]
    for prev, nxt in zip(gray, gray[1:]):
        moved, status, _ = cv2.calcOpticalFlowPyrLK(prev, nxt, points, None, winSize=(31, 31), maxLevel=3)
        back, back_status, _ = cv2.calcOpticalFlowPyrLK(nxt, prev, moved, None, winSize=(31, 31), maxLevel=3)
        alive &= status.ravel().astype(bool) & back_status.ravel().astype(bool)
        alive &= np.linalg.norm(back[:, 0] - points[:, 0], axis=1) < 1.5
        if alive.sum() < 4:
            return {"error": "追跡が途切れた", "frame": len(displacements), "remaining": int(alive.sum())}
        delta = (moved - origin)[:, 0]
        displacements.append(np.median(delta[alive], axis=0).tolist())
        support.append(int(alive.sum()))
        points = moved
    d = np.asarray(displacements)
    return {
        "range_x_px": round(float(np.ptp(d[:, 0])), 3),
        "range_y_px": round(float(np.ptp(d[:, 1])), 3),
        "wrap_displacement_px": round(float(np.linalg.norm(d[-1] - d[0])), 3),
        "wrap_velocity_difference_px": round(float(np.linalg.norm((d[-1] - d[-2]) - (d[1] - d[0]))), 3),
        "min_tracking_points": min(support),
        "displacements": np.round(d, 3).tolist(),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    images = load_frames(args.input)
    assert len(images) >= 3
    assert images[0].shape[:2] == (960, 640), "この領域指定は640×960のガルド専用"
    result = {"input": str(args.input), "frames": len(images), "method": "領域内特徴点の往復LK追跡・中央値。目視の代用ではない", "regions": {}}
    for name, box in REGIONS.items():
        value = track(images, box)
        result["regions"][name] = value
        print(name, {k: v for k, v in value.items() if k != "displacements"})
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
