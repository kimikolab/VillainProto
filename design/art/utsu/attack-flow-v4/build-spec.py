"""身体セルに同一縮尺の頭部を接続し、連撃の共通接頭列へ組む。"""
import json
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
old = json.loads((HERE.parent / 'attack-step-v3/cel-animation.json').read_text(encoding='utf-8'))
spec = dict(sourceId='utsu-flow-v4', canvas=[1600,1100], anchor=[800,1000], headPixels=125,
            clipScale=2, idleAnchor=old['idleAnchor'], fps=30, sheets={}, poses=[], clips=[])

def pose(key, sheet, cell, head, ground, blades, scale=None, body=None):
    p=dict(key=key, sheet='source-'+sheet+'.png', cell=cell, head=head, ground=ground, blades=blades)
    if scale is not None: p['scale']=scale
    if body: p['body']=dict(zip(['neck','pelvis','knee','ankle'],body))
    spec['poses'].append(p)
    spec['sheets'][p['sheet']]=1 if sheet in ('sweep','cross-fixed') else 4

# 点は元原画の座標。刀身根元は鍔の刃側、先端は実際の切先。
pose('ready_new','step',0,[[340,20],[345,138]],[310,631],[[[121,350],[10,533]],[[460,230],[587,50]]],scale=1.18,body=[[336,143],[310,337],[225,449],[157,578]])
pose('step_load','step',1,[[1005,78],[1018,193]],[995,623],[[[861,305],[601,388]],[[1091,270],[1239,169]]])
pose('step_push','step',2,[[471,667],[488,777]],[430,1185],[[[201,900],[18,1037]],[[280,939],[84,1016]]])
pose('step_plant','step',3,[[1101,674],[1120,784]],[1040,1214],[[[830,773],[591,782]],[[905,906],[664,1002]]])
pose('drive1','drive',0,[[489,85],[507,205]],[417,665],[[[200,191],[12,187]],[[539,340],[657,393]]])
pose('drive2','drive',1,[[989,86],[1000,204]],[985,662],[[[950,228],[1242,131]],[[998,301],[719,367]]])
pose('cut1','drive',2,[[467,682],[478,785]],[479,1198],[[[678,831],[899,859]],[[190,926],[15,1011]]],body=[[467,791],[441,966],[317,1067],[150,1134]])
pose('pass1','drive',3,[[1123,720],[1139,831]],[1045,1215],[[[1170,1036],[1250,1166]],[[904,900],[693,994]]])
pose('load2','rise',0,[[404,124],[418,226]],[365,590],[[[152,407],[26,550]],[[484,314],[667,271]]])
pose('rise_drive','rise',1,[[1002,61],[1019,168]],[1010,607],[[[802,279],[642,335]],[[954,391],[752,535]]])
pose('cut2','rise',2,[[337,713],[355,817]],[390,1206],[[[140,952],[13,998]],[[554,706],[706,606]]])
pose('pivot2','rise',3,[[1008,746],[1028,836]],[1075,1217],[[[818,958],[645,995]],[[1063,721],[1241,599]]])
pose('pivot3','turn',0,[[502,52],[520,137]],[500,520],[[[293,260],[30,307]],[[593,75],[801,17]]])
pose('unwind3','turn',1,[[1201,105],[1227,198]],[1190,524],[[[957,295],[726,353]],[[1305,92],[1517,17]]])
pose('sweep3','sweep',0,[[833,136],[851,279]],[780,969],[[[1130,362],[1520,410]],[[326,183],[21,36]]])
pose('spring3','turn',3,[[1329,628],[1352,719]],[1270,1002],[[[1361,833],[1513,924]],[[1309,596],[1138,505]]])
pose('launch4','hop',0,[[450,43],[477,137]],[500,568],[[[232,292],[17,402]],[[541,49],[801,115]]])
pose('fall4','hop',1,[[1193,141],[1211,241]],[1210,537],[[[936,333],[726,390]],[[1258,113],[1508,11]]])
pose('land4','hop',2,[[552,600],[568,695]],[470,1001],[[[218,619],[27,533]],[[702,812],[933,967]]])
pose('load5','hop',3,[[1251,626],[1264,719]],[1250,1006],[[[1032,654],[882,539]],[[1385,658],[1523,526]]])
pose('wind5','cross',0,[[474,117],[489,209]],[465,541],[[[351,57],[60,187]],[[503,113],[768,225]]])
pose('cross5','cross-fixed',0,[[760,263],[781,431]],[760,990],[[[1034,404],[1347,81]],[[1187,434],[828,72]]])
pose('pass5','cross',2,[[580,596],[599,698]],[474,984],[[[304,746],[18,901]],[[703,779],[864,916]]])
pose('recover','cross',3,[[1198,541],[1213,630]],[1200,999],[[[1007,778],[869,956]],[[1315,794],[1518,963]]],scale=1.52,body=[[1194,637],[1161,774],[1098,858],[1042,960]])
# 最初と最後は採用済み待機と完全に同じ絵、縮尺、足元。
ready=next(p.copy() for p in old['poses'] if p['key']=='ready')
ready['sheet']='../attack-weight-v2/source-ready.png'
ready['scale']=.5
spec['poses'].append(ready)
spec['sheets'][ready['sheet']]=1

# 体長で校正した全身倍率を固定し、頭が大きい原画そのものを描き直した。
# 小さく描き直した頭を基準に全身を再拡大すると修正が相殺されるため再正規化しない。
landmarks=json.loads((HERE/'body-landmarks.json').read_text(encoding='utf-8'))
head_landmarks=json.loads((HERE/'head-landmarks.json').read_text(encoding='utf-8'))
body_scales=json.loads((HERE/'body-scale-reference.json').read_text(encoding='utf-8'))
replacements={p['source']:p['adopted'] for p in json.loads((HERE/'generation-head-fix.json').read_text(encoding='utf-8'))}
def body_length(points):
    return sum(math.dist(a,b) for a,b in zip(points,points[1:]))
spec['headReferencePixels']=math.dist(*head_landmarks['ready'])*.5
new_sheets={}
for p in spec['poses']:
    if p['key']=='ready_new':
        p.update(ready)
        p['key']='ready_new'
        landmarks['ready_new']=landmarks['ready']
        body_scales['ready_new']=.5
    points=landmarks[p['key']]
    p['body']=dict(zip(['neck','pelvis','knee','ankle'],points))
    p['scale']=body_scales[p['key']]
    p['head']=head_landmarks[p['key']]
    source=p['sheet']
    p['sheet']=replacements.get(source,source)
    new_sheets[p['sheet']]=spec['sheets'][source]
spec['sheets']=new_sheets

# 髪の縦寸法だけでは顔幅を保証できない。向きごとに同じ頭部テクスチャを共有する。
spec['headSheet']='shared-heads.png'
spec['headPartScale']=107.2/555
spec['headParts']={
    'front':dict(rect=[60,90,595,640],anchor=[240,598]),
    'profile':dict(rect=[660,90,600,640],anchor=[270,598]),
    'back':dict(rect=[1280,90,600,640],anchor=[270,598])}
attachments={
    'step_load':('front',990,172,4), 'step_push':('front',449,755,8),
    'step_plant':('front',1084,765,4), 'drive1':('front',467,173,6),
    'drive2':('front',972,177,2), 'cut1':('back',464,750,5),
    'pass1':('profile',1104,800,12), 'load2':('front',383,209,6),
    'rise_drive':('front',979,153,-6), 'cut2':('profile',339,797,-20),
    'pivot2':('back',1010,807,8), 'pivot3':('back',509,130,3),
    'unwind3':('front',1197,178,6), 'sweep3':('back',789,246,7),
    'spring3':('front',1325,696,10), 'launch4':('front',461,125,-8),
    'fall4':('front',1172,215,10), 'land4':('front',524,677,16),
    'load5':('front',1233,695,4), 'wind5':('front',464,198,3),
    'cross5':('front',740,391,12), 'pass5':('front',554,672,14),
    'recover':('front',1178,618,0)}
parts=json.loads((HERE/'generation-shared-parts.json').read_text(encoding='utf-8'))['assets']
body_files={p['source']:p['adopted'] for p in parts if p['name']!='heads'}
refit_files={p['source']:p['adopted'] for p in json.loads((HERE/'generation-body-refit.json').read_text(encoding='utf-8'))['assets']}
spec['sheets']={}
for p in spec['poses']:
    if p['key'] in ('ready','ready_new'):
        p.update(sheet='../../../../DemoApp/assets/portraits/battle/idle_loop/utsu/utsu_idle_00.png',
            cell=0,scale=.8,ground=[320,928],head=[[412,45],[412,179]],
            body={k:[v[0]*.625,v[1]*.625] for k,v in ready['body'].items()} if 'body' in ready else
                 dict(zip(['neck','pelvis','knee','ankle'],[[400,194],[369,463],[250,650],[144,869]])))
        # 待機原画の剣座標も同じ縮尺の画像座標に移す。
        p['blades']=[[[x*.625,y*.625] for x,y in blade] for blade in p['blades']]
        spec['sheets'][p['sheet']]=1
    else:
        p['sheet']=body_files[p['sheet']]
        if p['sheet'] in refit_files:
            p['sheet']=refit_files[p['sheet']]
            # 部位の太さ・装備寸法を原画で直した上で、頭部とは独立に身体の実表示寸法を調整。
            p['scale']*=.94
        part,x,y,angle=attachments[p['key']]
        p['headPart']=dict(id=part,neck=[x,y],angle=angle)
        # 旧原画の頭部測定値を合成後の値として残さない。
        p['head']=[[x,y-107.2/p['scale']],[x,y]]
        spec['sheets'][p['sheet']]=1 if p['key'] in ('cross5','sweep3') else 4
spec['headReferencePixels']=107.2

# 交差刀身は各柄から各切先まで一本の直線。鍔の後ろに刃を延ばさない。
cross=next(p for p in spec['poses'] if p['key']=='cross5')
cross['blades']=[[[1026,411],[1361,61]],[[1196,443],[833,55]]]
cross['pommels']=[[939,525],[1264,523]]

# 接触後を2〜3コマ保持して短い当たりを作る。次の打の予備姿勢へそのまま渡す。
prefix={
1:[('ready',1),('ready_new',2),('step_load',3),('step_push',4),('step_plant',2),('drive1',2),('drive2',1),('cut1',2),('pass1',2)],
2:[('pass1',1),('load2',2),('rise_drive',2),('cut2',2),('pivot2',2)],
3:[('pivot2',1),('pivot3',2),('unwind3',2),('sweep3',2),('spring3',2)],
4:[('spring3',1),('launch4',2),('fall4',2),('land4',3)],
5:[('load5',2),('wind5',3),('cross5',3),('pass5',5)]}
contacts={1:15,2:5,3:5,4:5,5:5}
for hit in range(1,6):
    for keep in ([True,False] if hit<5 else [False]):
        keys=list(prefix[hit])
        if keep:
            if hit==4: keys += [('load5',2)]
        else:
            # 各段の終端では専用の振り抜きを残してから、開いた両剣を下ろし身体を起こす。
            if hit<5: keys += [('pass5',3)]
            keys += [('recover',4),('ready_new',3),('ready',2)]
        count=sum(n for _,n in keys)
        clip=dict(name=f'utsu_hit{hit}_{"link" if keep else "end"}_v4',hit=hit,keep=keep,keys=keys,
                  contact=contacts[hit],activeBlade=-1 if hit==5 else (hit-1)%2,
                  trailStart=contacts[hit]-2,trailEnd=contacts[hit]+3,vertical=[0]*count,forward=[0]*count)
        if hit==1:
            clip['advance']=[0]*6+[0,.3,.7,.95]+[1]*(count-10)
        if hit==4:
            clip['vertical']=[0,.1,.23,.27,.15]+[0]*(count-5)
        spec['clips'].append(clip)

(HERE/'cel-animation.json').write_text(json.dumps(spec,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
cal={}
for p in spec['poses']:
    scale=p.get('scale',125/math.dist(*p['head']))
    cal[p['key']]=dict(scale=scale,head=math.dist(*p['head'])*scale,
        blades=[round(math.dist(*b)*scale,1) for b in p['blades']])
    if 'headPart' in p:
        cal[p['key']]['headPart']=p['headPart']
        cal[p['key']]['headPartScale']=spec['headPartScale']
    cal[p['key']]['bodyGuideStatus']='旧原画の配置用基準点。身体修正後の骨長測定値ではなく、体格の合否判定には使わない。'
(HERE/'body-calibration.json').write_text(json.dumps(cal,indent=2)+'\n',encoding='utf-8')
print('UTSU_FLOW_SPEC_OK',len(spec['poses']),sum(sum(n for _,n in c['keys']) for c in spec['clips']))
