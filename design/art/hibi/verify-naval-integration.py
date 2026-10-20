import hashlib
import json
from pathlib import Path

from PIL import Image


folder = Path(__file__).resolve().parent
repo = folder.parents[2]
pairs = [
    (folder / "hibi-naval-bodyguard-front-v1.png", repo / "DemoApp/assets/portraits/hibi.png"),
    (folder / "hibi-naval-bodyguard-idle-right-v4.png", repo / "DemoApp/assets/portraits/battle/hibi_idle_right.png"),
]
rows = []
for source, target in pairs:
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    target_hash = hashlib.sha256(target.read_bytes()).hexdigest()
    if source_hash != target_hash:
        raise ValueError(f"採用画像と配置先が不一致: {target}")
    with Image.open(target) as picture:
        if picture.mode != "RGBA" or picture.size != (1024, 1536):
            raise ValueError(f"寸法・RGBAが不一致: {target}")
        alpha = picture.getchannel("A")
        bounds = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
        if alpha.getextrema()[0] != 0 or bounds[0] <= 0 or bounds[1] <= 0 or bounds[2] >= 1024 or bounds[3] >= 1536:
            raise ValueError(f"透過・画像端の確認に失敗: {target}")
    rows.append({"source": source.relative_to(repo).as_posix(), "installed": target.relative_to(repo).as_posix(), "sha256": target_hash, "bytes": target.stat().st_size, "size": [1024, 1536], "foreground_bbox": bounds, "bottom_padding": 1536 - bounds[3]})
candidate = folder / "idle-loop-v2/candidate-natural"
metrics = json.loads((candidate / "metrics.json").read_text(encoding="utf-8"))
loop_folder = repo / "DemoApp/assets/portraits/battle/idle_loop/hibi"
frames = sorted(loop_folder.glob("*.png"))
if len(frames) != 30 or metrics["geometry_postprocessing"]:
    raise ValueError("採用した未変形の30コマが必要です")
motion_rows = []
for frame_index, (target, entry) in enumerate(zip(frames, metrics["native_frame_hashes"])):
    source = candidate / "frames" / entry["file"]
    if target.name != f"hibi_idle_{frame_index:02d}.png" or hashlib.sha256(source.read_bytes()).hexdigest() != entry["sha256"]:
        raise ValueError(f"採用連番が不一致: {source}")
    with Image.open(source) as original, Image.open(target) as installed:
        if installed.mode != "RGBA" or installed.size != (640, 960) or original.tobytes() != installed.tobytes():
            raise ValueError(f"連番の画素・透過・寸法が不一致: {target}")
        bounds = installed.getchannel("A").point(lambda value: 255 if value > 128 else 0).getbbox()
        if bounds[3] != 869 or bounds[0] <= 0 or bounds[1] <= 0 or bounds[2] >= 640:
            raise ValueError(f"連番の接地・画像端が不一致: {target}")
    motion_rows.append({"file": target.relative_to(repo).as_posix(), "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "source_sha256": entry["sha256"], "bytes": target.stat().st_size})
manifest = {"front": "naval-front-v1", "battle": "naval-battle-v4", "world_height": 3.15, "battle_bottom_padding_ratio": "146/1536", "hud_height_ratio": "1212/1536", "motion_installed": True, "motion": {"tag": "hibi_s2022", "version": "idle-loop-v2/candidate-natural", "seed": 2022, "frames": 30, "fps": 16, "crossfade": 0, "geometry_postprocessing": False, "foot_row": 868, "files": motion_rows}, "files": rows}
(folder / "naval-installed-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print("HIBI_NAVAL_INSTALL_OK front=v1 battle=v4 motion=native-v2 frames=30 unchanged_pixels=true")
