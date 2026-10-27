"""納品映像を全フレーム復号し、原画とクリップの採用情報を記録する。"""
import hashlib
import json
import sys
from pathlib import Path

import av
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
spec = json.loads((HERE / "cel-animation.json").read_text(encoding="utf-8"))
report = {"source": "utsu-step-v3", "fps": spec["fps"], "clips": [], "poses": [], "media": []}
assets_only = "--assets-only" in sys.argv
for clip in spec["clips"]:
    directory = REPO / "DemoApp/assets/portraits/battle/attack_clip" / clip["name"]
    frames = sorted(directory.glob("*.png"))
    report["clips"].append({"name": clip["name"], "frames": len(frames), "contact": clip["contact"]})
assert len(report["clips"]) == 9
assert sum(c["frames"] for c in report["clips"]) == 145
for path in sorted((HERE / "poses").glob("*.png")):
    with Image.open(path) as im:
        assert im.mode == "RGBA" and im.size == (1280, 960)
        box = im.getchannel("A").getbbox()
        assert box and box[0] > 0 and box[1] > 0 and box[2] < im.width and box[3] < im.height
        report["poses"].append({"name": path.stem, "alpha_bbox": box})
assert len(report["poses"]) == 18
board = Image.new("RGB", (1200, 330), "#333942")
draw = ImageDraw.Draw(board)
for col, (key, title) in enumerate([("step_load", "LOAD"), ("step_push", "PUSH"), ("step_plant", "PLANT")]):
    with Image.open(HERE / "poses" / (key + ".png")) as im:
        # 共通キャンバスを同じ倍率で並べる。外接矩形ごとの拡大・縮小はしない。
        im.thumbnail((400, 300), Image.Resampling.LANCZOS)
        board.paste(im, (col*400, 25), im)
    draw.text((col*400+12, 6), title, fill="white")
    draw.line((col*400, 306, (col+1)*400, 306), fill="#8b9ba5")
board.save(HERE / "step-poses.png")
for path in sorted((HERE / "video").glob("*.mp4")):
    with av.open(str(path)) as container:
        stream = container.streams.video[0]
        count = sum(1 for _ in container.decode(video=0))
        assert count > 90 and (stream.width, stream.height) == (1280, 720)
        report["media"].append({"name": path.name, "frames": count, "fps": str(stream.average_rate),
                                "size": [stream.width, stream.height], "audio": len(container.streams.audio),
                                "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
for path in sorted((HERE / "video").glob("*-slow.gif")):
    with Image.open(path) as im:
        assert im.size == (960, 540) and im.n_frames > 50
        duration = 0
        for n in range(im.n_frames):
            im.seek(n)
            im.load()
            duration += im.info.get("duration", 0)
        report["media"].append({"name": path.name, "frames": im.n_frames, "duration_ms": duration})
if not assets_only:
    assert sum(m["name"].endswith(".mp4") for m in report["media"]) == 5
    assert sum(m["name"].endswith(".gif") for m in report["media"]) == 4
(HERE / ("verification-assets.json" if assets_only else "verification.json")).write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"UTSU_MEDIA_VERIFY_OK poses=18 clips=9 frames=145 media={len(report['media'])} assets_only={assets_only} same-scale-step-board")
