"""MainのGPU録画から場面を切り出す。コマ補間・後付けのエフェクトなし。"""
import argparse
import json
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
FFMPEG = ROOT/'output/hagi-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
p = argparse.ArgumentParser()
p.add_argument('--raw', type=Path, required=True)
args = p.parse_args()
dest = HERE/'video'
dest.mkdir(exist_ok=True)
labels = {'air-opener':'初撃の刈り取り：跳躍 → 空中の溜め → 落下斬り',
          'air-boss-finish':'ボスへのフィニッシュ：大きく振りかぶって刈り下ろす',
          'air-chain-finish':'連続狩り：速い返し刃 → 最後の敵へ跳躍フィニッシュ'}

def run(options):
    subprocess.run([str(FFMPEG),'-hide_banner','-loglevel','error','-y',*options],cwd=HERE,check=True)

def title(name, text, size=23):
    caption = f'caption-{name}.txt'
    (HERE/caption).write_text(text,'utf-8')
    return ("pad=iw:ih+60:0:60:color=0x13161c,"
            "drawtext=fontfile='C\\:/Windows/Fonts/meiryo.ttc':"
            f"textfile='{caption}':fontsize={size}:fontcolor=0xffe9d2:x=20:y=16")

meta = json.loads((HERE/'capture-segments.json').read_text('utf-8'))
parts=[]
for shot in meta['segments']:
    name=shot['name']; start=shot['startFrame']/60; duration=(shot['endFrame']-shot['startFrame'])/60
    vf=f'trim=duration={duration},setpts=PTS-STARTPTS,crop=1440:700:0:100,'+title(name,labels[name]+'　｜　通常速度')
    out=dest/f'{name}-1x.mp4'
    run(['-ss',str(start),'-i',str(args.raw.resolve()),'-vf',vf,
         '-af',f'atrim=duration={duration},asetpts=PTS-STARTPTS',
         '-c:v','libx264','-crf','18','-preset','slow','-pix_fmt','yuv420p','-r','60',
         '-c:a','aac','-b:a','160k','-movflags','+faststart',str(out)])
    parts.append(out)
    if name == 'air-boss-finish':
        vf=f'trim=duration={duration},setpts=2*(PTS-STARTPTS),crop=1440:700:0:100,'+title(name+'-fx-half',labels[name]+'　｜　斬撃と動作・1/2速')
        half=dest/f'{name}-fx-half.mp4'
        run(['-ss',str(start),'-i',str(args.raw.resolve()),'-vf',vf,'-an',
             '-c:v','libx264','-crf','18','-preset','slow','-pix_fmt','yuv420p','-r','60',
             '-movflags','+faststart',str(half)])
        run(['-i',str(half),'-filter_complex',
             '[0:v]fps=24,scale=1080:-1:flags=lanczos,split[x][y];[x]palettegen=max_colors=160:stats_mode=diff[p];'
             '[y][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle',
             '-an',str(dest/f'{name}-fx-half.gif')])
(dest/'concat.txt').write_text(''.join(f"file '{f.name}'\n" for f in parts),'utf-8')
run(['-f','concat','-safe','0','-i',str(dest/'concat.txt'),'-map','0:v:0','-an',
     '-c:v','copy','-movflags','+faststart',str(dest/'hagi-aerial-reap-v7.mp4')])

print('HAGI_AIR_PREVIEW_ENCODE_OK battle=3 compilation=1 fx-half=2')
