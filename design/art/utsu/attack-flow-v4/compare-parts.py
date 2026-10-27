"""実際に書き出したセルをffmpegで等倍比較する（画像の修正はしない）。"""
import json, math, subprocess
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
FFMPEG=ROOT/'output/hagi-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
spec=json.loads((HERE/'cel-animation.json').read_text(encoding='utf-8'))
poses=sorted(spec['poses'],key=lambda p: 0 if p['key']=='ready' else 1)
args=[str(FFMPEG),'-y','-loglevel','error']
filters=[]
for i,p in enumerate(poses):
    args+=['-i',str(HERE/'poses'/f"{p['key']}.png")]
    if 'headPart' in p:
        x,y=p['headPart']['neck']; a=math.radians(p['headPart']['angle'])
        dx,dy=13*math.cos(a)+55*math.sin(a),13*math.sin(a)-55*math.cos(a)
    else:
        x,y=412,110; dx,dy=0,0
    x=round(800+(x-p['ground'][0])*p['scale']+dx)
    y=round(1000+(y-p['ground'][1])*p['scale']+dy)
    filters.append(f"[{i}:v]crop=190:230:{x-95}:{y-90},drawtext=text='{p['key']}':x=8:y=8:fontsize=16:fontcolor=white:box=1:boxcolor=black@0.7[p{i}]")
layout='|'.join(f'{(i%5)*190}_{(i//5)*230}' for i in range(len(poses)))
filters.append(''.join(f'[p{i}]' for i in range(len(poses)))+f'xstack=inputs={len(poses)}:layout={layout}[parts]')
filters.append('color=c=0x777777:s=950x1150[bg];[bg][parts]overlay=shortest=1[out]')
dest=HERE/'shared-heads-compare.png'
subprocess.run(args+['-filter_complex',';'.join(filters),'-map','[out]','-frames:v','1',str(dest)],check=True)
print(dest)
