"""同倍率の全身と各部位をffmpegで並べる検品用。素材は変更しない。"""
import json, subprocess
from pathlib import Path
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
FFMPEG=ROOT/'output/hagi-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
def contact(items,dest,columns,crop,size):
    args=[str(FFMPEG),'-y','-loglevel','error']
    filters=[]
    for i,(path,label) in enumerate(items):
        args+=['-i',str(path)]
        filters.append(f"[{i}:v]crop={crop},scale={size[0]}:{size[1]},drawtext=text='{label}':x=8:y=8:fontsize=18:fontcolor=white:box=1:boxcolor=black@0.7[p{i}]")
    layout='|'.join(f'{(i%columns)*size[0]}_{(i//columns)*size[1]}' for i in range(len(items)))
    filters.append(''.join(f'[p{i}]' for i in range(len(items)))+f'xstack=inputs={len(items)}:layout={layout}[parts]')
    filters.append(f'color=c=0x777777:s={columns*size[0]}x{((len(items)+columns-1)//columns)*size[1]}[bg];[bg][parts]overlay=shortest=1[out]')
    subprocess.run(args+['-filter_complex',';'.join(filters),'-map','[out]','-frames:v','1',str(dest)],check=True,stderr=subprocess.PIPE)
keys=['ready','step_load','drive2']
items=[(ROOT/'output/utsu-flow-v4/body-before'/f'{k}.png',f'BEFORE {k}') for k in keys]
items +=[(HERE/'poses'/f'{k}.png',f'AFTER {k}') for k in keys]
contact(items,HERE/'body-refit-three.png',3,'1000:900:250:150',(500,450))
spec=json.loads((HERE/'cel-animation.json').read_text(encoding='utf-8'))
keys=['ready']+[p['key'] for p in spec['poses'] if p['key']!='ready']
contact([(HERE/'poses'/f'{k}.png',k) for k in keys],HERE/'body-refit-all.png',5,'1600:1100:0:0',(400,275))
print('UTSU_BODY_COMPARISON_OK same-scale no-auto-fit')
