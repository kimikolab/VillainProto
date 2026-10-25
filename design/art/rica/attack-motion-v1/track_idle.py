"""待機動画の原コマを保ち、シャベルと目の追跡情報だけを追加する。"""
import argparse
import json
import sys
from pathlib import Path
import cv2
import numpy as np
from PIL import Image, ImageDraw

HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
sys.path.insert(0,str(REPO/'tools/comfy_idle_loop'))
import idle_loop as il


def gray(im):
    a=np.asarray(im.convert('RGBA'))
    rgb=(a[:,:,:3].astype(float)*(a[:,:,3:4]/255)).astype('uint8')
    return cv2.cvtColor(rgb,cv2.COLOR_RGB2GRAY)


def write_track(tag,key,loop):
    files=[Path(f) for f in il.frames_of(tag)]
    frames=[Image.open(f).convert('RGBA') for f in files]
    w,h=frames[0].size
    manifest=json.loads((REPO/'DemoApp/assets/portraits/battle/motion_sources/rica/v1/manifest.json').read_text(encoding='utf-8'))
    stage=int(key[-1]);pose=next(p for p in manifest['poses'] if p['stage']==stage and p['key']=='idle')
    entry=il.load_prompts()[key]
    reference=Image.open(REPO/entry['still']).convert('RGBA').resize((w,h),Image.Resampling.LANCZOS)
    layout=REPO/'DemoApp/assets/portraits/battle/idle_layout'/f'{key}.json'
    if layout.exists():
        source=json.loads(layout.read_text(encoding='utf-8'));points=[source[k] for k in ['socket','tip','eye']]
    else:points=[pose[k] for k in ['weaponSocket','weaponTip','eyeAnchor']]
    p=np.array(points,dtype=np.float32)*[w,h]
    manual=HERE/f'{tag}-anchors.json'
    if manual.exists():
        initial=json.loads(manual.read_text(encoding='utf-8'))
        p=np.array([initial[k] for k in ['socket','tip','eye']],dtype=np.float32)
    p=p.astype(np.float32).reshape(-1,1,2)
    previous=gray(frames[0] if manual.exists() else reference);records=[]
    for im in frames:
        current=gray(im)
        q,status,error=cv2.calcOpticalFlowPyrLK(previous,current,p,None,winSize=(45,45),maxLevel=4,
            criteria=(cv2.TERM_CRITERIA_EPS|cv2.TERM_CRITERIA_COUNT,50,.001))
        good=(status[:,0]>0)&(np.linalg.norm(q[:,0]-p[:,0],axis=1)<60)
        q[~good]=p[~good]
        q[:,0,0]=np.clip(q[:,0,0],0,w-1);q[:,0,1]=np.clip(q[:,0,1],0,h-1)
        uv=q[:,0]/[w,h]
        records.append({'socket':uv[0].round(6).tolist(),'tip':uv[1].round(6).tolist(),'eye':uv[2].round(6).tolist(),'shape':min(stage,2)})
        previous=current;p=q
    data={'source':tag,'size':[w,h],'coordinates':'normalized UV; inspected first-frame anchors + adjacent-frame LK' if manual.exists() else 'normalized UV; source-to-first + adjacent-frame LK; visually inspected','frames':records}
    dest=REPO/'DemoApp/assets/portraits/battle/idle_loop'/loop
    assert dest.is_dir(),f'先にidle_loop.py installを実行する: {loop}'
    (dest/'motion.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    out=HERE/f'{loop}-tracking.jpg';sheet=Image.new('RGB',(4*240,380),(27,37,47));draw=ImageDraw.Draw(sheet)
    for k,index in enumerate([0,len(frames)//3,2*len(frames)//3,len(frames)-1]):
        bg=Image.new('RGBA',(w,h),(27,37,47,255));bg.alpha_composite(frames[index]);sheet.paste(bg.convert('RGB').resize((240,360)),(k*240,20))
        points=[np.array(records[index][n])*[240,360]+[k*240,20] for n in ['socket','tip','eye']]
        draw.line([tuple(points[0]),tuple(points[1])],fill='cyan',width=2)
        for point in points:draw.ellipse((point[0]-3,point[1]-3,point[0]+3,point[1]+3),fill='orange')
        draw.text((k*240+5,3),f'{tag} frame {index}',fill='white')
    sheet.save(out,quality=92)
    print(loop,'追跡',len(records),'コマ')


def compare(stage):
    paths=[REPO/'DemoApp/assets/portraits/battle/idle_loop'/f'rica_stage{stage}{suffix}' for suffix in ['', '_b']]
    candidates=[[Image.open(p).convert('RGBA') for p in sorted(path.glob('*.png'))] for path in paths]
    if not all(candidates):raise ValueError('A/Bの両方が必要')
    frames=[]
    for i in range(max(map(len,candidates))*2):
        canvas=Image.new('RGB',(640,510),(27,37,47));d=ImageDraw.Draw(canvas)
        for k,sequence in enumerate(candidates):
            im=sequence[i%len(sequence)].resize((320,480));bg=Image.new('RGBA',(320,480),(27,37,47,255));bg.alpha_composite(im)
            canvas.paste(bg.convert('RGB'),(k*320,30));d.text((k*320+12,8),'A / seed 2022' if k==0 else 'B / seed 5055',fill='white')
        frames.append(canvas.convert('P',palette=Image.Palette.ADAPTIVE,colors=192))
    frames[0].save(HERE/f'idle-stage{stage}-A-vs-B.gif',save_all=True,append_images=frames[1:],duration=63,loop=0)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('tag',nargs='?');p.add_argument('key',nargs='?');p.add_argument('loop',nargs='?');p.add_argument('--compare',type=int);a=p.parse_args()
    if a.compare:compare(a.compare)
    else:write_track(a.tag,a.key,a.loop)
