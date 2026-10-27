"""Godotの実描画を切り出す。動作補間・後付け剣閃は行わない。"""
import argparse
import json
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
parser = argparse.ArgumentParser()
parser.add_argument("--raw", type=Path, required=True)
args = parser.parse_args()
ffmpeg = REPO / "output/hagi-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe"
manifest = json.loads((HERE / "capture-segments.json").read_text(encoding="utf-8"))
dest = HERE / "video"
dest.mkdir(exist_ok=True)

def run(arguments):
    subprocess.run([str(ffmpeg), "-hide_banner", "-loglevel", "error", "-y", *arguments], check=True)

parts = []
for segment in manifest["segments"]:
    start = segment["start_frame"] / manifest["fps"]
    duration = (segment["end_frame"] - segment["start_frame"]) / manifest["fps"]
    name = segment["name"]
    video = dest / (name + "-1x.mp4")
    run(["-ss", str(start), "-i", str(args.raw), "-t", str(duration),
         "-vf", f"crop=960:540:320:80,scale=1280:720:flags=lanczos,drawtext=text='{segment['count']}-HIT DANCE':fontcolor=white:fontsize=30:x=24:y=18:box=1:boxcolor=black@0.55:boxborderw=10", "-c:v", "libx264", "-crf", "18",
         "-preset", "medium", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", str(video)])
    parts.append(video)
    run(["-i", str(video), "-vf", f"fps={16 / duration},scale=400:225:flags=lanczos,tile=4x4",
         "-frames:v", "1", str(dest / (name + "-sheet.jpg"))])
    run(["-i", str(video), "-filter_complex",
         "[0:v]setpts=1.5*(PTS-STARTPTS),fps=15,scale=960:540:flags=lanczos,split[x][y];"
         "[x]palettegen=max_colors=192:stats_mode=diff[p];[y][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle",
         "-an", str(dest / (name + "-slow.gif"))])
listing = dest / "concat.txt"
listing.write_text("".join("file '" + part.name + "'\n" for part in parts), encoding="utf-8")
run(["-f", "concat", "-safe", "0", "-i", str(listing), "-c", "copy", "-movflags", "+faststart", str(dest / "utsu-step-2-to-5-1x.mp4")])
print("UTSU_PREVIEW_ENCODE_OK actual-game-frames speed=1 slow=2/3 no-interpolation")
