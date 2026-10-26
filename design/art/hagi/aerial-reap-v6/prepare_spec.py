"""採用済みv5原画を使い、刃を先行させてから着地する露出表を作る。"""
import json
from pathlib import Path

HERE=Path(__file__).resolve().parent
base=json.loads((HERE/'../aerial-reap-v5/cel-animation.json').read_text('utf-8'))
assert base['sourceId']=='hagi-aerial-reap-v5'
for pose in base['poses']:
    pose['sheet']='../aerial-reap-v5/'+pose['sheet']
clips=[]
def clip(key,keys,heights,contact):
    assert sum(n for _,n in keys)==len(heights)
    clips.append(dict(key=key,keys=keys,vertical=heights,contact=contact))

start=[['guard',2],['load',3],['launch',3],['twist',3]]
rise=[0]*5+[.20,.48,.82,1.12,1.36,1.48]
for finish in (False,True):
    apex=8 if finish else 5
    # 頭上姿勢のまま足を下ろさない。2コマで刃を運び、空中で振り切る。
    prefix=start+[['peak',apex],['drop',2],['impact',3]]
    y=rise+[1.48]*(apex-2)+[1.46,1.42]+[1.32,1.16,.70,.46,.25]
    contact=11+apex+2
    tag='air_finish' if finish else 'air_reap'
    clip(tag+'_v6',prefix+[['land',5 if finish else 3],['guard',3]],
         y+[0]*(8 if finish else 6),contact)
    clip(tag+'_cut_hold_v6',prefix,y,contact)
    clip(tag+'_hold_v6',prefix+[['land',3]],y+[0]*3,contact)
clip('air_after_v6',[['impact',2],['land',3],['guard',3]],[.25,.12]+[0]*6,1)
clip('air_after_hold_v6',[['impact',2],['land',3]],[.25,.12,0,0,0],1)
clip('air_after_finish_v6',[['impact',2],['land',5],['guard',3]],[.25,.12]+[0]*8,1)
base.update(sourceId='hagi-aerial-reap-v6',clips=clips,
    motion='空中で頭上→前方へ素早く振り下ろし、刃が接触してから着地する。原画はv5の全身セルを継続。')
(HERE/'cel-animation.json').write_text(json.dumps(base,ensure_ascii=False,indent=2)+'\n','utf-8')
