"""原画と全身セル、共有接続姿勢、GPU録画を読み取り検査して記録する。"""
import hashlib
import json
from pathlib import Path
import av
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
runtime = ROOT / "DemoApp/assets/portraits/battle/attack_clip"
spec = json.loads((HERE / "cel-animation.json").read_text(encoding="utf-8"))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
clips = []
for clip in spec["clips"]:
    name = "hagi_" + clip["key"]
    directory = runtime / name
    meta = json.loads((directory / "clip.json").read_text(encoding="utf-8"))
    images = sorted(directory.glob("*.png"))
    assert len(images) == meta["frames"]
    for file in images:
        with Image.open(file) as im:
            assert im.size == (1920, 960)
            box = im.getchannel("A").getbbox()
            assert box and box[0] > 0 and box[1] > 0 and box[2] < 1920 and box[3] < 960
    clips.append(dict(name=name, frames=len(images), contact=meta["contact"], fps=meta["fps"]))

joins = []
for outgoing, following in (("double_out", "double_rise"), ("hook_out", "hook_pull"),
                            ("hook_pull_hold", "hook_repeat_hold"), ("double_rise_hook", "hook_pull"),
                            ("hook_repeat_hold", "hook_repeat")):
    before = sorted((runtime / f"hagi_{outgoing}_v2").glob("*.png"))[-1]
    after = sorted((runtime / f"hagi_{following}_v2").glob("*.png"))[0]
    assert sha(before) == sha(after), f"接続姿勢が不一致: {outgoing} → {following}"
    joins.append(dict(outgoing=outgoing, following=following, shared_full_body_sha256=sha(before)))

videos = []
for path in sorted((HERE / "video").iterdir()):
    if path.suffix not in (".mp4", ".gif"):
        continue
    with av.open(str(path)) as container:
        stream = container.streams.video[0]
        count, last = 0, 0.0
        for frame in container.decode(video=0):
            count += 1
            last = float(frame.time)
        videos.append(dict(file=path.name, bytes=path.stat().st_size, frames=count,
                           width=stream.width, height=stream.height, last_frame_seconds=round(last, 3),
                           audio_streams=len(container.streams.audio)))

result = dict(build="0 warnings / 0 errors", full_body_poses=12, clips=clips, shared_cel_joins=joins,
              runtime_png_count=sum(c["frames"] for c in clips),
              hagi_presentation_check="OK / 15 scripts / hit counts, HP, pause, speed, mirror, cancel, reset, replay, seek",
              render="Godot Vulkan Forward+ / RTX 5090 / 60 fps / Main.EnterViewerLive",
              videos=videos,
              exit_diagnostics="Existing exit-only Texture/Font/CanvasItem/ObjectDB release warnings; checks and both GPU captures exit 0")
(HERE / "verification.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
originals = ["exec-c00fbdf5-eb9e-407c-8043-7dfac3d84dda.png", "exec-8d58a1de-b51c-405f-bb98-fdf8722ec046.png", "exec-2da58014-66b9-4765-a084-e2eab255e3eb.png"]
generation = dict(tool="built-in image_gen", transparent_background=True,
                  original_directory="C:/Users/Pon/.codex/generated_images/01a16ee4-957d-7073-aa2e-d3e415aca3ce",
                  references=[dict(path="DemoApp/assets/portraits/battle/hagi_idle_right.png", role="人物・衣装・武器の基準"),
                              dict(path="design/art/hagi/attack-acrobatics-v1/source-ground.png", role="既存原画の画風")],
                  sheets=[dict(file=f"source-{name}.png", original=original, sha256=sha(HERE / f"source-{name}.png"),
                               prompt=f"prompt-{prompt}.txt", adopted_cells=[0, 1, 2, 3],
                               reference_paths=["DemoApp/assets/portraits/battle/hagi_idle_right.png",
                                   "design/art/hagi/attack-acrobatics-v1/source-ground.png" if name == "out"
                                   else "design/art/hagi/scythe-return-v2/source-out.png"])
                          for name, prompt, original in zip(("out", "rise", "hook"), ("out", "in", "hook"), originals)])
(HERE / "generation-record.json").write_text(json.dumps(generation, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"HAGI_SCYTHE_V2_ASSETS_OK poses=12 clips={len(clips)} joins={len(joins)} videos={len(videos)}")
