"""本編のGPU録画を切り出す。補間せず、通常速度と1/4速を並べる。"""
import argparse
import json
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
FFMPEG = ROOT / "output/hagi-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe"
p = argparse.ArgumentParser()
p.add_argument("--raw", type=Path, required=True)
p.add_argument("--study-raw", type=Path)
args = p.parse_args()
meta = json.loads((HERE / "capture-segments.json").read_text(encoding="utf-8"))
dest = HERE / "video"
dest.mkdir(exist_ok=True)
labels = {
    "after-hit-return": "返し刃：刃を返す → 腰へ引き刈る",
    "isolated-twin": "二度斬り：横薙ぎ → 低く回す → 薙ぎ上げ",
    "carry-chain": "連続狩り：引き刈り → 鎌を運び直す → 次の獲物",
}

def run(options):
    subprocess.run([str(FFMPEG), "-hide_banner", "-loglevel", "error", "-y", *options],
                   cwd=HERE, check=True)

def title_filter(textfile, fontsize=27):
    return ("pad=iw:ih+64:0:64:color=0x13161c,"
            "drawtext=fontfile='C\\:/Windows/Fonts/meiryo.ttc':"
            f"textfile='{textfile}':fontsize={fontsize}:fontcolor=0xffe9d2:x=26:y=16")

parts = []
for segment in meta["segments"]:
    name = segment["name"]
    start = segment["startFrame"] / meta["fps"]
    duration = (segment["endFrame"] - segment["startFrame"]) / meta["fps"]
    outputs = {}
    for rate, factor in (("1x", 1), ("quarter", 4)):
        caption = f"{labels[name]}　｜　" + ("通常速度" if factor == 1 else "1/4速・コマ補間なし")
        textfile = f"caption-{name}-{rate}.txt"
        (HERE / textfile).write_text(caption, encoding="utf-8", newline="\n")
        out = dest / f"{name}-{rate}.mp4"
        filters = (f"trim=duration={duration},setpts={factor}*(PTS-STARTPTS),"
                   "crop=1440:700:0:100," + title_filter(textfile))
        audio = ["-af", f"atrim=duration={duration},asetpts=PTS-STARTPTS", "-c:a", "aac", "-b:a", "160k"]
        if factor != 1:
            audio = ["-an"]
        run(["-ss", str(start), "-i", str(args.raw.resolve()), "-vf", filters,
             "-c:v", "libx264", "-crf", "18", "-preset", "slow", "-pix_fmt", "yuv420p",
             "-r", "60", *audio, "-movflags", "+faststart", str(out)])
        outputs[rate] = out
    # 結合版は通常→低速を各場面ごとに並べる。音声は単品の通常速度版で確認する。
    parts.extend([outputs["1x"], outputs["quarter"]])

listing = dest / "concat.txt"
listing.write_text("".join(f"file '{part.name}'\n" for part in parts), encoding="utf-8", newline="\n")
run(["-f", "concat", "-safe", "0", "-i", str(listing), "-map", "0:v:0", "-an",
     "-c:v", "copy", "-movflags", "+faststart", str(dest / "hagi-scythe-motion-v2.mp4")])

if args.study_raw:
    study = json.loads((HERE / "study/capture-segments.json").read_text(encoding="utf-8"))
    for segment in study["segments"]:
        name = segment["name"]
        if name == "carry-chain":
            continue
        a = segment["startFrame"] / study["fps"]
        duration = (segment["endFrame"] - segment["startFrame"]) / study["fps"]
        textfile = f"caption-{name}-study.txt"
        (HERE / textfile).write_text(labels[name] + "　｜　動作確認・1/2速", encoding="utf-8", newline="\n")
        filters = (f"trim=duration={duration},setpts=2*(PTS-STARTPTS),crop=800:620:320:130,"
                   + title_filter(textfile, fontsize=19))
        run(["-ss", str(a), "-i", str(args.study_raw.resolve()), "-vf", filters,
             "-c:v", "libx264", "-crf", "17", "-preset", "slow", "-pix_fmt", "yuv420p",
             "-r", "60", "-an", "-movflags", "+faststart", str(dest / f"{name}-motion-half.mp4")])
        run(["-i", str(dest / f"{name}-motion-half.mp4"), "-filter_complex",
             "[0:v]fps=24,scale=1100:-1:flags=lanczos,split[x][y];"
             "[x]palettegen=max_colors=160:stats_mode=diff[p];"
             "[y][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle",
             "-an", str(dest / f"{name}-motion-half.gif")])
print("HAGI_SCYTHE_MOTION_V2_ENCODE_OK original-render normal=1 slow=0.25 study=0.5 no-interpolation")
