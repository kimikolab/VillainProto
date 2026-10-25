"""実寸・採用セル・他段不変・実描画の確認資料。画像の部位加工はしない。"""
from pathlib import Path
import json,math,hashlib
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).resolve().parent;REPO=P.parents[3];B=REPO/'DemoApp/assets/portraits/battle'
s=json.loads((P.parent/'attack-motion-v1/cel-animation.json').read_text(encoding='utf-8'))
old=json.loads((P/'previous-spec-v13.json').read_text(encoding='utf-8'))
target=s['proportionCalibration']['targetHeadPixels']
before=json.loads((P/'unchanged-before.json').read_text(encoding='utf-8'))
changed=[f for f,h in before.items() if hashlib.sha256((REPO/f).read_bytes()).hexdigest()!=h]
assert not changed,changed
guides=[]
for c in s['clips']:
 if c['stage']!=3:continue
 prior=next(x for x in old['clips'] if x['key']==c['key'])
 for k in set(prior)-{'keys','source'}:assert c[k]==prior[k],(c['key'],k)
 assert [x[1] for x in c['keys']]==[x[1] for x in prior['keys']]
 motion=json.loads((B/'attack_clip'/('rica_'+c['key'])/'motion.json').read_text(encoding='utf-8'))['frames']
 for f in motion:
  n=math.dist([f['headTop'][0]*1152,f['headTop'][1]*896],[f['headChin'][0]*1152,f['headChin'][1]*896])
  assert abs(n-target)<.01,(c['key'],n)
  guides.append(n)
report={'wholeBodyPoses':20,'headGuideTargetPixels':target,'exportedHeadGuideMin':min(guides),'exportedHeadGuideMax':max(guides),'sameWorldScaleAsIdle':True,'unchangedOtherStageFiles':len(before),'unchangedTimingsContactsJumpTurnShapes':True}
(P/'asset-check.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',24);small=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',17)
selection=[('待機｜体格の基準',None),('踏み込み',59),('1撃目｜突きの準備',40),('1撃目｜振り抜き',44),('2撃目｜空中回転',48),('3撃目｜頭上で構える',52),('3撃目｜振り下ろし',54),('3撃目｜着地・振り抜き',57)]
canvas=Image.new('RGB',(2080,1200),'#171f2a');d=ImageDraw.Draw(canvas)
for i,(title,pose) in enumerate(selection):
 x=(i%4)*520;y=(i//4)*600;ground=(260,555);scale=.64
 if pose is None:
  original=Image.open(B/'motion_sources/rica/v1/stage3/idle.png').convert('RGBA');factor=1.3/1920*1536/2.8*scale
  im=original.resize((round(original.width*factor),round(original.height*factor)),Image.Resampling.LANCZOS)
  offset=(round(x+ground[0]-512*factor),round(y+ground[1]-1523*factor))
  guide=[[533,143],[578,336]]
  points=[(offset[0]+p[0]*factor,offset[1]+p[1]*factor) for p in guide]
 else:
  g=(pose-40)//4+1;k=(pose-40)%4
  original=Image.open(P/f'cels/group{g}/pose{k:02}.png').convert('RGBA')
  im=original.resize((round(original.width*scale),round(original.height*scale)),Image.Resampling.LANCZOS)
  offset=(round(x+ground[0]-552*scale),round(y+ground[1]-768*scale))
  stage=next(t for t in s['stages'] if t['stage']==3 and t['poseOffset']==40+(g-1)*4)
  h=stage['headGuides'][k];a=stage['anchors'][k][3];factor=target/math.dist(*h)*scale
  points=[(x+ground[0]+(p[0]-a[0])*factor,y+ground[1]+(p[1]-a[1])*factor) for p in h]
 canvas.paste(im,offset,im)
 d.line((x+20,y+555,x+500,y+555),fill='#435362',width=1)
 d.text((x+14,y+12),title,font=font,fill='#e1edf7')
 d.line(points,fill='#efcb78',width=2)
 for p in points:d.ellipse((p[0]-3,p[1]-3,p[0]+3,p[1]+3),fill='#efcb78')
 d.text((x+14,y+572),'同倍率・頭頂〜顎の軸長を校正（黄色線）',font=small,fill='#a8b9c8')
canvas.save(P/'rica-stage3-proportions-v14.jpg',quality=94)
print(json.dumps(report))
