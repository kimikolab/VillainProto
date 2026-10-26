"""原画の全身セルの計測点と、30fpsの露出表。画像処理はGodotの制作書き出しに任せる。"""
import json
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
poses = []

def pose(key, sheet, cell, head, ground, socket, tip, contour, airborne=False):
    if airborne:
        # 膝の曲げ伸ばしで体の高さが跳ねないよう、頭から測った仮想接地点を使う。
        scale = 124 / math.dist(*head)
        ground = [ground[0], round(head[0][1] + 650 / scale, 3)]
    poses.append(dict(key=key, sheet=sheet, cell=cell, head=head, ground=ground,
                      socket=socket, tip=tip, contour=contour))

pose('load','source-rise.png',0,[[519,152],[550,243]],[441,560],
     [89,247],[338,22],[[338,22],[192,49],[90,125],[35,224],[48,337],[83,382]])
pose('launch','source-rise.png',1,[[1103,61],[1121,149]],[1105,511],
     [906,69],[895,20],[[895,20],[807,40],[728,118],[675,218],[657,306],[747,226]],True)
pose('twist','source-rise.png',2,[[477,627],[491,719]],[461,1173],
     [235,677],[222,626],[[222,626],[153,653],[84,717],[31,809],[12,874],[92,805]],True)
pose('peak','source-fall.png',0,[[462,92],[480,165]],[459,511],
     [254,84],[379,10],[[379,10],[290,13],[223,46],[177,91],[174,163],[210,224]],True)
pose('drop','source-fall.png',1,[[1137,65],[1155,144]],[1134,559],
     [1349,143],[1192,15],[[1192,15],[1295,39],[1372,92],[1406,166],[1396,252],[1336,301]],True)
pose('impact','source-fall.png',2,[[535,547],[558,625]],[524,1007],
     [736,890],[914,1005],[[914,1005],[828,999],[753,975],[715,928],[727,866],[741,809]])
pose('land','source-fall.png',3,[[1195,538],[1212,615]],[1176,982],
     [923,815],[1074,980],[[1074,980],[995,958],[925,918],[883,868],[877,815],[977,741]])
guard = next(p for p in json.loads((HERE.parent/'rotational-sweep-v3/cel-animation.json').read_text('utf-8'))['poses'] if p['key']=='guard')
guard['sheet'] = '../rotational-sweep-v3/source-link.png'
poses.append(guard)

clips = []
def clip(key, keys, heights, contact):
    assert sum(n for _,n in keys)==len(heights)
    clips.append(dict(key=key,keys=keys,vertical=heights,contact=contact))

start=[['guard',2],['load',3],['launch',3],['twist',3]]
rise=[0]*5+[.15,.38,.65,.90,1.05,1.12]
for finish in (False,True):
    apex=7 if finish else 4
    prefix=start+[['peak',apex],['drop',2],['impact',2]]
    y=rise+[1.12]*(apex-2)+[1.10,1.03]+[.72,.40,.12,.04]
    contact=11+apex+2
    tag='air_finish' if finish else 'air_reap'
    clip(tag+'_v4',prefix+[['land',5 if finish else 3],['load',1],['guard',2]],
         y+[0]*(8 if finish else 6),contact)
    clip(tag+'_cut_hold_v4',prefix,y,contact)
    clip(tag+'_hold_v4',prefix+[['land',3]],y+[0]*3,contact)
clip('air_after_v4',[['impact',2],['land',3],['load',1],['guard',2]],[.04,.02]+[0]*6,1)
clip('air_after_hold_v4',[['impact',2],['land',3]],[.04,.02,0,0,0],1)
clip('air_after_finish_v4',[['impact',2],['land',5],['load',1],['guard',2]],[.04,.02]+[0]*8,1)

spec=dict(sourceId='hagi-aerial-reap-v4',canvas=[1920,1536],anchor=[880,1400],
          motion='跳躍・空中ひねり・背中へ引き絞る溜め・落下斬り・着地。全身原画、部位ワープ・逆再生なし。',poses=poses,clips=clips)
(HERE/'cel-animation.json').write_text(json.dumps(spec,ensure_ascii=False,indent=2)+'\n','utf-8')
