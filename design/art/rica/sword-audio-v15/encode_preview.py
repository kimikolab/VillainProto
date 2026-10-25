"""FxViewerの実映像へ、ゲームと同じSEを接触コマで同期した段別の確認動画。"""
import json,hashlib,argparse
from pathlib import Path
from fractions import Fraction
import av,numpy as np
from PIL import Image,ImageDraw,ImageFont
HERE=Path(__file__).resolve().parent;REPO=HERE.parents[3];FLOW=HERE.parent/'attack-motion-v1'
parser=argparse.ArgumentParser();parser.add_argument('--hit-layer',action='store_true')
parser.add_argument('--hit-boost-db',type=float,default=0);parser.add_argument('--suffix',default='');args=parser.parse_args()
assert all(c.isalnum() or c=='-' for c in args.suffix)
suffix='-'+args.suffix if args.suffix else ''
P=HERE.parent/'sword-hit-v16' if args.hit_layer else HERE;P.mkdir(parents=True,exist_ok=True)
START,END=60,255;COUNT=END-START;FPS=30;RATE=44100
keys=[['rica_shovel'],['rica_crescent'],['rica_rise','rica_return'],['rica_thrust','rica_sweep','rica_descend']]
clips=['kado_hit.mp3']+[f'rica_sword_critical_{i:03}.wav' for i in range(1,4)]
gains=[-8,-10,-10,-10]
hit_clips=[None,'rica_sword_hit_5.mp3','rica_sword_hit_4.mp3','rica_sword_hit_1.mp3']
audio=np.zeros((2,round(COUNT/FPS*4*RATE)),np.float32);report=[]
for stage in range(4):
 layer_files=[clips[stage]]+([hit_clips[stage]] if args.hit_layer and stage>0 else [])
 sounds=[];layers=[]
 for layer_index,filename in enumerate(layer_files):
  gain=gains[stage]+(args.hit_boost_db if layer_index==1 else 0)
  source=REPO/'DemoApp/assets/audio/se'/filename
  resampler=av.AudioResampler(format='fltp',layout='stereo',rate=RATE)
  with av.open(str(source)) as c:pieces=[p.to_ndarray() for f in c.decode(audio=0) for p in resampler.resample(f)]
  pieces.extend(p.to_ndarray() for p in resampler.resample(None));sounds.append(np.concatenate(pieces,axis=1).astype(np.float32)*10**(gain/20))
  layers.append({'file':filename,'gainDb':gain,'sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
 records=json.loads((REPO/f'output/rica-fxviewer-flow/stage{stage}/capture.json').read_text(encoding='utf-8'))['records']
 hits=[]
 for name in keys[stage]:
  meta=json.loads((REPO/f'DemoApp/assets/portraits/battle/attack_clip/{name}/clip.json').read_text(encoding='utf-8'))
  tick=next(r['frame'] for r in records if r['clip']==name and r['clipFrame']>=meta['contact'])
  offset=round((stage*COUNT+tick-START)/FPS*RATE)
  for sound in sounds:
   n=min(sound.shape[1],audio.shape[1]-offset);audio[:,offset:offset+n]+=sound[:,:n]
  hits.append({'clip':name,'captureFrame':tick,'previewSeconds':offset/RATE})
 report.append({'stage':stage,'file':clips[stage],'gainDb':gains[stage],'hits':hits,'sha256':layers[0]['sha256'],'layers':layers})
panels=[[] for _ in range(4)]
with av.open(str(FLOW/'rica-four-stage-flow-comparison-proportions-v14.mp4')) as c:
 for i,f in enumerate(c.decode(video=0)):
  if START<=i<END:
   im=f.to_image()
   for stage in range(4):
    x=(stage%2)*560;y=(stage//2)*600+50
    panels[stage].append(im.crop((x,y,x+560,y+550)).resize((800,786),Image.Resampling.LANCZOS))
assert all(len(p)==COUNT for p in panels)
font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',25);small=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',20)
titles=['0–2層｜シャベル打撃','3–5層｜回転薙ぎ','6–8層｜大剣の二連撃','9層以上｜覚醒の三連撃']
names=['金属叩き（従来）','Sword Critical 001','Sword Critical 002','Sword Critical 003']
if args.hit_layer:
 for stage,n in enumerate([5,4,1],1):names[stage]+=f' ＋ 刀で斬る{n}'
out=P/(('rica-four-stage-layered-audio-v16' if args.hit_layer else 'rica-four-stage-sword-audio-v15')+suffix+'.mp4')
with av.open(str(out),'w') as c:
 stream=c.add_stream('libx264',rate=FPS);stream.width=800;stream.height=900;stream.pix_fmt='yuv420p';stream.options={'crf':'18','preset':'medium'}
 snd=c.add_stream('aac',rate=RATE);snd.layout='stereo'
 for stage in range(4):
  for i,im in enumerate(panels[stage]):
   canvas=Image.new('RGB',(800,900),'#0b1110');canvas.paste(im,(0,50));d=ImageDraw.Draw(canvas)
   d.text((14,10),titles[stage],font=font,fill='#dae7f3')
   levels=f'  / 剣{gains[stage]:+}・刀{gains[stage]+args.hit_boost_db:+g} dB' if args.hit_layer and stage>0 else f'  /  {gains[stage]:+} dB'
   d.text((14,846),names[stage]+levels,font=small,fill='#d4dce7')
   d.text((14,873),'SE確認用｜命中コマに同期・BGMなし・通常速度',font=small,fill='#aab8c5')
   for packet in stream.encode(av.VideoFrame.from_image(canvas)):c.mux(packet)
 for packet in stream.encode():c.mux(packet)
 for n in range(0,audio.shape[1],1024):
  f=av.AudioFrame.from_ndarray(np.ascontiguousarray(audio[:,n:n+1024]),format='fltp',layout='stereo');f.sample_rate=RATE;f.time_base=Fraction(1,RATE);f.pts=n
  for packet in snd.encode(f):c.mux(packet)
 for packet in snd.encode():c.mux(packet)
peak=float(np.abs(audio).max());assert peak<1
(P/('preview-check'+suffix+'.json')).write_text(json.dumps({'fps':FPS,'frames':COUNT*4,'seconds':COUNT*4/FPS,'audioPeakBeforeEncoding':peak,'stages':report},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('RICA_SE_PREVIEW_OK frames=780 seconds=26 fps=30 peak='+str(peak))
