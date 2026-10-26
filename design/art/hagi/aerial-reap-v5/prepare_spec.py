"""鎌の基準を共有する一枚原画の計測と、反り・ひねりを使う跳躍の露出表。"""
import json
import math
from pathlib import Path

HERE=Path(__file__).resolve().parent
poses=[]
def pose(key, head, ground, socket, tip, contour, butt, heel, airborne=False):
    if airborne:
        scale=124/math.dist(*head)
        ground=[ground[0],round(head[0][1]+650/scale,3)]
    poses.append(dict(key=key,sheet=f'source-{key}.png',cell=0,head=head,ground=ground,
                      socket=socket,tip=tip,contour=contour,butt=butt,heel=heel))

pose('guard',[[440,146],[465,334]],[510,1506],[797,254],[298,107],
     [[298,107],[470,23],[663,23],[847,93],[954,342],[952,760]],[29,1172],[952,760])
pose('load',[[1173,176],[1204,350]],[977,996],[310,273],[796,113],
     [[796,113],[592,43],[383,76],[233,182],[107,435],[119,789]],[1510,727],[119,789])
pose('launch',[[1116,150],[1150,304]],[1245,1018],[251,428],[762,819],
     [[762,819],[514,801],[303,720],[141,585],[51,416],[96,256]],[1314,18],[96,256],True)
pose('twist',[[908,175],[944,327]],[978,1263],[214,356],[590,724],
     [[590,724],[382,695],[204,597],[90,474],[54,321],[92,202]],[1194,23],[92,202],True)
pose('peak',[[1106,137],[1147,291]],[1245,1001],[253,435],[794,789],
     [[794,789],[541,785],[310,693],[141,576],[55,438],[78,286]],[1304,13],[78,286],True)
pose('drop',[[727,481],[757,655]],[620,1449],[821,249],[344,105],
     [[344,105],[544,20],[736,21],[899,100],[998,360],[965,714]],[20,941],[965,714],True)
pose('impact',[[793,101],[807,282]],[715,1005],[1218,761],[816,975],
     [[816,975],[1074,995],[1289,917],[1468,751],[1511,577],[1437,432]],[229,11],[1437,432])
pose('land',[[966,47],[973,220]],[968,986],[217,416],[482,893],
     [[482,893],[284,791],[142,615],[51,423],[72,215],[177,37]],[1516,418],[177,37])

clips=[]
def clip(key,keys,heights,contact):
    assert sum(n for _,n in keys)==len(heights)
    clips.append(dict(key=key,keys=keys,vertical=heights,contact=contact))
start=[['guard',2],['load',3],['launch',3],['twist',3]]
rise=[0]*5+[.20,.48,.82,1.12,1.36,1.48]
for finish in (False,True):
    apex=8 if finish else 5
    prefix=start+[['peak',apex],['drop',3],['impact',2]]
    y=rise+[1.48]*(apex-2)+[1.45,1.35]+[1.02,.62,.28,.07,.02]
    contact=11+apex+3
    tag='air_finish' if finish else 'air_reap'
    clip(tag+'_v5',prefix+[['land',5 if finish else 3],['guard',3]],
         y+[0]*(8 if finish else 6),contact)
    clip(tag+'_cut_hold_v5',prefix,y,contact)
    clip(tag+'_hold_v5',prefix+[['land',3]],y+[0]*3,contact)
clip('air_after_v5',[['impact',2],['land',3],['guard',3]],[.02,.01]+[0]*6,1)
clip('air_after_hold_v5',[['impact',2],['land',3]],[.02,.01,0,0,0],1)
clip('air_after_finish_v5',[['impact',2],['land',5],['guard',3]],[.02,.01]+[0]*8,1)
spec=dict(sourceId='hagi-aerial-reap-v5',cellsPerSheet=1,canvas=[2304,1664],anchor=[1120,1400],
    motion='同じ大鎌を使う一枚原画。高い跳躍・胸の反り・肩と腰のひねり・落下斬り。部位ワープ・逆再生なし。',
    poses=poses,clips=clips)
(HERE/'cel-animation.json').write_text(json.dumps(spec,ensure_ascii=False,indent=2)+'\n','utf-8')
