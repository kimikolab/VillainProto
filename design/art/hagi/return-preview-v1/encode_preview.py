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
args = p.parse_args()
meta = json.loads((HERE / "capture-segments.json").read_text(encoding="utf-8"))
dest = HERE / "video"
dest.mkdir(exist_ok=True)
labels = {
    "after-hit-return": "通常斬撃 → 返し刃で刈り取り",
    "isolated-twin": "孤立した敵への二度斬り（往復）",
    "carry-chain": "通常斬撃 → 返し刃 → 再行動 → 返し刃",
}

def run(options):
    subprocess.run([str(FFMPEG), "-hide_banner", "-loglevel", "error", "-y", *options],
                   cwd=HERE, check=True)

def title_filter(textfile):
    return ("pad=iw:ih+64:0:64:color=0x13161c,"
            "drawtext=fontfile='C\\:/Windows/Fonts/meiryo.ttc':"
            f"textfile='{textfile}':fontsize=27:fontcolor=0xffe9d2:x=26:y=16")

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
    if name == "after-hit-return":
        contact = next(o["frame"] for o in meta["observations"] if o["shot"] == name and o["swings"] == 1)
        # 踏み込み前の待機は全体動画に残し、拡大GIFでは命中直前から返しへ接続する。
        begin = max(0, (contact - segment["startFrame"]) / meta["fps"] - .12) * 4
        run(["-i", str(outputs["quarter"]), "-filter_complex",
             f"[0:v]trim=start={begin}:end={duration * 4 - .6},setpts=PTS-STARTPTS,"
             "crop=1040:700:400:64," + title_filter("caption-after-hit-return-quarter.txt")
             + ",fps=24,split[x][y];"
             "[x]palettegen=max_colors=160:stats_mode=diff[p];"
             "[y][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle",
             "-an", str(dest / "after-hit-return-quarter.gif")])

listing = dest / "concat.txt"
listing.write_text("".join(f"file '{part.name}'\n" for part in parts), encoding="utf-8", newline="\n")
run(["-f", "concat", "-safe", "0", "-i", str(listing), "-map", "0:v:0", "-an",
     "-c:v", "copy", "-movflags", "+faststart", str(dest / "hagi-return-preview.mp4")])
print("HAGI_RETURN_PREVIEW_ENCODE_OK original-render normal=1 slow=0.25 no-interpolation")
