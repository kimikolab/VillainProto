"""9層以上の原画を登録。全身セルを頭の実寸で校正し、既存の時刻・軌道は保存する。"""
import json, math, hashlib
from pathlib import Path
P=Path(__file__).resolve().parent
REPO=P.parents[3]
SPEC=P.parent/'attack-motion-v1/cel-animation.json'
spec=json.loads((P/'previous-spec-v13.json').read_text(encoding='utf-8'))
heads=[
[[[340,87],[355,173]],[[990,158],[1008,237]],[[350,726],[366,801]],[[977,759],[996,836]]],
[[[391,57],[407,141]],[[1014,49],[1038,129]],[[287,644],[263,719]],[[1022,641],[1040,725]]],
[[[419,42],[439,136]],[[1040,42],[1060,138]],[[382,748],[394,825]],[[1042,734],[1023,813]]],
[[[389,122],[413,201]],[[969,106],[994,188]],[[448,681],[469,766]],[[1033,706],[1050,791]]],
[[[415,19],[434,114]],[[1058,106],[1082,191]],[[407,669],[425,761]],[[1158,646],[1180,739]]]]
anchors=[
[[[493,350],[588,361],[355,136],[333,644]],[[1133,416],[1212,436],[1014,206],[943,640]],[[552,892],[636,899],[366,770],[287,1175]],[[1162,888],[1235,890],[999,806],[918,1171]]],
[[[670,167],[775,166],[409,110],[344,581]],[[852,292],[751,293],[1037,99],[994,613]],[[170,860],[59,857],[261,691],[365,1212]],[[1136,919],[1235,928],[1042,691],[991,1221]]],
[[[551,281],[653,298],[439,97],[372,617]],[[1168,351],[1230,417],[1060,96],[978,574]],[[470,670],[558,648],[394,785],[378,1209]],[[866,696],[770,695],[1008,779],[989,1220]]],
[[[516,79],[601,12],[414,165],[370,618]],[[1112,95],[1183,7],[991,151],[940,654]],[[577,1065],[664,1156],[470,732],[360,1221]],[[1154,1106],[1238,1196],[1050,755],[953,1235]]],
[[[587,482],[658,570],[427,78],[315,599]],[[1192,495],[1250,584],[1083,160],[930,601]],[[590,1087],[668,1171],[424,727],[320,1191]],[[805,809],[676,791],[1184,697],[989,1188]]]]
reference=[[533,143],[578,336]]
target=math.dist(*reference)*1.3/1920*1536/2.8
spec['proportionCalibration']={'reference':'DemoApp/assets/portraits/battle/motion_sources/rica/v1/stage3/idle.png','referenceGuide':reference,'idlePaddedHeight':1920,'idleHeightScale':1.3,'attackReferenceHeight':1536,'attackScale':2.8,'targetHeadPixels':target,'method':'頭頂から顎の軸長。全身セルを縦横同率で拡縮。膝を畳んだ全身高では正規化しない'}
spec['stages']=[s for s in spec['stages'] if s['stage']!=3]
for g in range(5):
 s={'stage':3,'poseOffset':40+g*4,'rows':2,'scale':1,'sheet':f'design/art/rica/proportions-v14/group{g+1}-'+('selected' if g==3 else 'adult')+'.png','poseDirectory':f'design/art/rica/proportions-v14/cels/group{g+1}','anchors':anchors[g],'headGuides':heads[g]}
 if g==3:
  s['overrides']=[{'sheet':'design/art/rica/proportions-v14/group4-adult.png','rect':[0,0,640,630]}]
  s['selectedPreview']='design/art/rica/proportions-v14/group4-final.png'
 if g==1:s['hiddenEyes']=[2]
 if g==2:s['hiddenEyes']=[3]
 spec['stages'].append(s)
mapping={old:40+g*4+k for g,group in enumerate(json.loads((P/'pose-map-index.json').read_text())) for k,old in enumerate(group)}
for c in spec['clips']:
 if c['stage']==3:
  c['source']='rica-adult-proportions-v14'
  c['keys']=[[mapping[p],n] for p,n in c['keys']]
for a in spec['approaches']:
 if a['stage']==3:a.update(sourceStage=3,pose=59,source='rica-adult-proportions-v14')
SPEC.write_text(json.dumps(spec,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
(P/'calibration.json').write_text(json.dumps({'targetHeadPixels':target,'mapping':mapping,'heads':heads,'anchors':anchors},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
# 変更対象外の生成物もハッシュで保存する。
files=[]
for p in (REPO/'DemoApp/assets/portraits/battle/attack_clip').glob('rica_*/*'):
 if p.suffix in ('.png','.json') and not any(k in p.parent.name for k in ['thrust','sweep','descend']):
  files.append(p)
for stage in range(3):files.extend((REPO/f'DemoApp/assets/portraits/battle/approach/rica_approach_stage{stage}').glob('*'))
baseline=P/'unchanged-before.json'
if not baseline.exists():
 baseline.write_text(json.dumps({str(p.relative_to(REPO)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files if p.is_file()},indent=2)+'\n',encoding='utf-8')
print('STAGE3_SOURCE_REGISTERED poses=20 targetHeadPixels=',target)
