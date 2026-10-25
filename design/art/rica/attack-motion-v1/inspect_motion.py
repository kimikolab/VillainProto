"""生成動画を検品する接触シートとGIF。RGB・透過の原コマは加工しない。"""
import argparse
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import cv2

HERE=Path(__file__).resolve().parent
OUT=Path('D:/comfyui/output/VillainProto/rica_motion')
INDICES=[0,2,4,6,8,10,12,14,16,18,20,24]


def track(frames, start, end):
    keys=['weaponSocket','weaponTip','eyeAnchor']
    grays=[cv2.cvtColor(np.asarray(f)[:,:,:3],cv2.COLOR_RGB2GRAY) for f in frames]
    def walk(indices, anchors):
        p=np.array([anchors[k] for k in keys],dtype=np.float32).reshape(-1,1,2)
        results={indices[0]:p[:,0].copy()}
        for a,b in zip(indices,indices[1:]):
            q,status,err=cv2.calcOpticalFlowPyrLK(grays[a],grays[b],p,None,winSize=(45,45),maxLevel=5,
                criteria=(cv2.TERM_CRITERIA_EPS|cv2.TERM_CRITERIA_COUNT,40,.005))
            good=(status[:,0]>0)&(np.linalg.norm(q[:,0]-p[:,0],axis=1)<200)
            q[~good]=p[~good]
            results[b]=q[:,0].copy(); p=q
        return results
    fw=walk(list(range(len(frames))),start)
    bw=walk(list(reversed(range(len(frames)))),end)
    # 前後から追跡し、距離が大きいときは確認シート上で手直しする。
    arr=[]
    for i in range(len(frames)):
        t=i/(len(frames)-1)
        arr.append(fw[i]*(1-t)+bw[i]*t)
    return np.array(arr)


def inspect(tag):
    files=sorted((OUT/f'{tag}_rgba').glob('*.png'))
    if len(files)!=25:
        print(tag,'未完了',len(files)); return
    frames=[Image.open(f).convert('RGBA') for f in files]
    jobs=json.loads((HERE/'jobs.json').read_text(encoding='utf-8'))
    info=jobs[tag]
    tracked=track(frames,info['startAnchors'],info['endAnchors'])
    corrections=HERE/f'{tag}-corrections.json'
    if corrections.exists():
        edits=json.loads(corrections.read_text(encoding='utf-8'))
        for k,key in enumerate(['socket','tip','eye']):
            anchors={int(i):np.array(e[key],dtype=float) for i,e in edits.items() if key in e}
            if anchors:
                indices=sorted(anchors)
                for dim in [0,1]:
                    tracked[:,k,dim]=np.interp(np.arange(25),indices,[anchors[i][dim] for i in indices])
    w,h=frames[0].size
    key=tag.split('_')[1]
    shape=0 if key=='shovel' else 1 if key=='crescent' else 3 if key=='thrust' else 2
    records=[{'socket':(p[0]/[w,h]).round(6).tolist(),'tip':(p[1]/[w,h]).round(6).tolist(),
              'eye':(p[2]/[w,h]).round(6).tolist(),'shape':shape} for p in tracked]
    (HERE/f'{tag}-tracking.json').write_text(json.dumps({'frames':records,'method':'bidirectional LK + inspected manual anchors'},ensure_ascii=False,indent=2),encoding='utf-8')
    sheet=Image.new('RGB',(1200,4*282),(31,40,50)); draw=ImageDraw.Draw(sheet)
    for n,i in enumerate(INDICES):
        x=(n%4)*300; y=(n//4)*282
        im=Image.new('RGBA',(w,h),(31,40,50,255)); im.alpha_composite(frames[i])
        im=im.convert('RGB').resize((300,257))
        sheet.paste(im,(x,y))
        points=tracked[i]*[300/w,257/h]+[x,y]
        draw.line([tuple(points[0]),tuple(points[1])],fill=(0,255,255),width=2)
        for j,p in enumerate(points):
            r=3;draw.ellipse((p[0]-r,p[1]-r,p[0]+r,p[1]+r),fill=(255,80+j*50,50))
        draw.text((x+8,y+258),f'f{i:02}   {w}x{h}',fill='white')
    sheet.crop((0,0,1200,3*282)).save(HERE/f'{tag}-sheet.jpg',quality=93)
    gif=[]
    for f in frames:
        bg=Image.new('RGBA',f.size,(31,40,50,255));bg.alpha_composite(f)
        gif.append(bg.convert('RGB').resize((672,576)))
    gif[0].save(HERE/f'{tag}.gif',save_all=True,append_images=gif[1:]+[gif[-1]]*9,duration=55,loop=0)
    print(tag,'25コマ', '枠接触',sum(np.any(np.asarray(f)[[0,-1],:,3]>128) or np.any(np.asarray(f)[:,[0,-1],3]>128) for f in frames))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('tags',nargs='*');a=p.parse_args()
    for tag in a.tags or json.loads((HERE/'jobs.json').read_text(encoding='utf-8')):
        inspect(tag)
