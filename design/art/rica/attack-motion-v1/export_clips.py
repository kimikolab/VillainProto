"""検品済みの動画を汎用AttackClipへ配置する。保持コマ以外の中割りは生成動画そのもの。"""
import json
import shutil
from pathlib import Path
from PIL import Image
import numpy as np

HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
OUT=Path('D:/comfyui/output/VillainProto/rica_motion')
DEST=REPO/'DemoApp/assets/portraits/battle/attack_clip'


def export(key,entry):
    tag=entry['tag']; name='rica_'+key
    frames=sorted((OUT/(tag+'_rgba')).glob('*.png'))
    if len(frames)!=25: raise ValueError(f'{tag}: 25コマ揃っていない')
    track=json.loads((HERE/f'{tag}-tracking.json').read_text(encoding='utf-8'))
    hold=entry.get('startHold',4)
    original=entry.get('indices',list(range(entry.get('first',0),entry.get('last',24)+1)))
    indices=[original[0]]*hold+original
    destination=DEST/name
    destination.mkdir(parents=True,exist_ok=True)
    # この道具が管理する連番だけを差し替える。他のファイルは消さない。
    for old in destination.glob(name+'_*.png'): old.unlink()
    for i,index in enumerate(indices):shutil.copyfile(frames[index],destination/f'{name}_{i:03}.png')
    info={
        'source':tag,'frames':len(indices),'size':list(Image.open(frames[0]).size),'fps':entry.get('fps',30),
        'contact':hold+original.index(entry['contact']),'anchor':[424,704],
        'idleAnchor':[512,1496],'scale':2.80,'referencePortrait':'rica','forward':[0]*len(indices),
        'motion':'Wan2.2 Fun Inpaint 14B, true generated in-betweens; static anticipation holds only',
    }
    if key=='descend':
        contact=info['contact']
        info['vertical']=[round(.8*max(0,1-i/max(1,contact))**.7,4) for i in range(len(indices))]
    (destination/'clip.json').write_text(json.dumps(info,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    data={'source':tag,'coordinates':'normalized UV','frames':[track['frames'][i] for i in indices]}
    assert all(np.linalg.norm(np.array(f['socket'])-np.array(f['tip']))>.012 for f in data['frames'])
    (destination/'motion.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(name,len(indices),'frames contact',info['contact'],'fps',info['fps'])


if __name__=='__main__':
    for key,entry in json.loads((HERE/'selected.json').read_text(encoding='utf-8')).items():export(key,entry)
