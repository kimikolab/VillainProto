"""9層以上を同じ実描画倍率で比較。通常→1/2速、待機から帰還まで。"""
from pathlib import Path
import json,av
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).resolve().parent;F=P.parent/'attack-motion-v1';REPO=P.parents[3]
START,END=60,225
panels=[]
for name in ['rica-four-stage-flow-comparison-white-v13.mp4','rica-four-stage-flow-comparison-proportions-v14.mp4']:
 frames=[]
 with av.open(str(F/name)) as c:
  for i,f in enumerate(c.decode(video=0)):
   if START<=i<END:frames.append(f.to_image().crop((560,650,1120,1200)).resize((700,688),Image.Resampling.LANCZOS))
 assert len(frames)==END-START;panels.append(frames)
records=[json.loads((P/'before-capture-v13.json').read_text(encoding='utf-8'))['records'],json.loads((REPO/'output/rica-fxviewer-flow/stage3/capture.json').read_text(encoding='utf-8'))['records']]
labels={'idle':'待機','approach':'武器を構えて踏み込む','link':'連撃のつなぎ','attack':'攻撃','return':'元の席へ戻る','returned':'待機へ復帰'}
clips={'rica_thrust':'1撃目：突き','rica_sweep':'2撃目：空中回転','rica_descend':'3撃目：落下斬り'}
font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',24);small=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',21)
out=P/'rica-triple-proportions-before-after-v14.mp4'
with av.open(str(out),'w') as c:
 stream=c.add_stream('libx264',rate=30);stream.width=1400;stream.height=860;stream.pix_fmt='yuv420p';stream.options={'crf':'17','preset':'medium'}
 for repeat,speed in [(1,'通常速度 ×1'),(2,'1/2速 ×0.5')]:
  for i in range(END-START):
   im=Image.new('RGB',(1400,860),'#0b1110');d=ImageDraw.Draw(im)
   for col,title in enumerate(['修正前｜コマごとに体格が変わる','修正後｜待機絵の体格・頭の寸法へ統一']):
    x=col*700;im.paste(panels[col][i],(x,50));d.text((x+14,11),title,font=font,fill='#e1c9a9' if col==0 else '#d7e9ff')
    r=records[col][i+START];phase=clips.get(r['clip'],labels.get(r['phase'],r['phase']))
    d.text((x+14,750),phase,font=small,fill='#d1dfdc')
   d.text((18,795),'9層以上・三連撃｜実際のFxViewer・同倍率・同時刻',font=small,fill='#d1dfdc')
   d.text((1382,795),speed,font=small,fill='#d7e9ff',anchor='ra')
   d.text((18,827),'待機 → 踏み込み → 突き → 空中回転 → 落下斬り → 待機',font=small,fill='#d1dfdc')
   if repeat==1 and i+START in [65,85,95,101,110,120,135,145,160,170,195,220]:im.save(P/f'comparison-{i+START:03}.jpg',quality=94)
   for _ in range(repeat):
    for packet in stream.encode(av.VideoFrame.from_image(im)):c.mux(packet)
 for packet in stream.encode():c.mux(packet)
print('PROPORTIONS_COMPARISON_OK frames=495 fps=30 seconds=16.5')
