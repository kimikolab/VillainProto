"""本番の待機・強化比較と戦闘録画を、加工を加えず切り出す。"""
import json
import subprocess
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[4]
HERE=Path(__file__).resolve().parent
FFMPEG=ROOT/'output/hagi-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
meta=json.loads((HERE/'stage-capture.json').read_text(encoding='utf8'))
a=meta['startFrame']/meta['fps'];duration=(meta['endFrame']-meta['startFrame'])/meta['fps']
raw=ROOT/'output/hagi-movie/raw-blade-stages-v1.avi';dest=HERE/'video'

def run(args):
    subprocess.run([str(FFMPEG),'-hide_banner','-loglevel','error','-y',*args],check=True)

run(['-ss',str(a),'-i',str(raw),'-t',str(duration),'-vf','crop=1440:500:0:180',
     '-c:v','libx264','-crf','17','-preset','slow','-pix_fmt','yuv420p','-an','-movflags','+faststart',
     str(dest/'hagi-empower-stages-1x.mp4')])
run(['-i',str(dest/'hagi-empower-stages-1x.mp4'),'-filter_complex',
     '[0:v]fps=16,scale=1280:-1:flags=lanczos,split[x][y];[x]palettegen=max_colors=160[p];'
     '[y][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-an',str(dest/'hagi-empower-stages.gif')])
subprocess.run([sys.executable,str(ROOT/'design/art/hagi/attack-acrobatics-v1/encode_preview.py'),
    '--raw',str(ROOT/'output/hagi-movie/raw-blade-empower-battle-v1.avi'),
    '--output-dir',str(dest),'--name','hagi-empowered-battle'],check=True)
print('HAGI_EMPOWER_PREVIEW_OK stages=0,3,6,9 idle-speed=1 battle-speed=1 original-render')
