"""Godotの実描画・実音声から、通常速度MP4と半速の確認GIFを書き出す。"""
import argparse
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
p = argparse.ArgumentParser()
p.add_argument("--raw", type=Path, required=True)
p.add_argument("--ffmpeg", type=Path, default=ROOT / "output/hagi-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe")
p.add_argument("--output-dir", type=Path, default=HERE / "video")
p.add_argument("--name", default="hagi-acrobatics")
args = p.parse_args()
dest = args.output_dir.resolve()
manifest = json.loads((dest / "capture-segments.json").read_text(encoding="utf-8"))

def run(options):
    subprocess.run([str(args.ffmpeg), "-hide_banner", "-loglevel", "error", "-y", *options], check=True)

parts = []
for s in manifest["segments"]:
    a, b = s["startFrame"] / manifest["fps"], s["endFrame"] / manifest["fps"]
    out = dest / (s["name"] + "-1x.mp4")
    run(["-ss", str(a), "-i", str(args.raw), "-t", str(b-a), "-vf", "crop=1440:700:0:100",
         "-c:v", "libx264", "-crf", "18", "-preset", "slow", "-pix_fmt", "yuv420p", "-c:a", "aac",
         "-b:a", "192k", "-movflags", "+faststart", str(out)])
    parts.append(out)
    # コマを補間せず時間だけ2倍にする。構え、握り、回転、着地を確認できる。
    run(["-ss", str(a), "-i", str(args.raw), "-t", str((b-a)*2), "-filter_complex",
         f"[0:v]trim=duration={b-a},setpts=2*(PTS-STARTPTS),crop=1440:700:0:100,fps=12,scale=960:-1:flags=lanczos,split[x][y];"
         "[x]palettegen=max_colors=128:stats_mode=diff[p];[y][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle", "-an", str(dest / (s["name"] + "-half.gif"))])

listing = dest / "concat.txt"
listing.write_text("".join("file '" + x.name + "'\n" for x in parts), encoding="utf-8", newline="\n")
run(["-f", "concat", "-safe", "0", "-i", str(listing), "-c", "copy", "-movflags", "+faststart", str(dest / (args.name + "-1x.mp4"))])
print("HAGI_PREVIEW_ENCODE_OK sweep reap chain speed=1 mp4 speed=0.5 gif no-interpolation original-game-audio")
