"""実録画の三連撃を、着地音の調整前→調整後で比較する。音の後付けなし。"""
from pathlib import Path
from fractions import Fraction
import json
import av,numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.signal import correlate
P=Path(__file__).resolve().parent;REPO=P.parents[3]
START,END=13.5,20.5;RATE=48000;FPS=30
paths=[REPO/'.tmp/rica-event-audio-v17/review.avi',REPO/'.tmp/rica-event-audio-v18/review.avi']
def read_audio(path):
 r=av.AudioResampler(format='fltp',layout='stereo',rate=RATE)
 with av.open(str(path)) as c:parts=[a.to_ndarray() for f in c.decode(audio=0) for a in r.resample(f)]
 parts.extend(a.to_ndarray() for a in r.resample(None));return np.concatenate(parts,axis=1)
raw=[read_audio(p) for p in paths]
sounds=np.concatenate([a[:,round(START*RATE):round(END*RATE)] for a in raw],axis=1)
font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',24)
out=P/'rica-landing-before-after-v18.mp4'
frames=0
with av.open(str(out),'w') as c:
 v=c.add_stream('libx264',rate=FPS);v.width=1120;v.height=744;v.pix_fmt='yuv420p';v.options={'crf':'18','preset':'medium'}
 a=c.add_stream('aac',rate=RATE);a.layout='stereo'
 for path,title in zip(paths,['調整前｜着地音 -12 dB','調整後｜着地音 -6 dB（着地音だけ +6 dB）']):
  with av.open(str(path)) as original:
   for i,f in enumerate(original.decode(video=0)):
    if START*FPS<=i<END*FPS:
     canvas=Image.new('RGB',(1120,744),'#0b1110');canvas.paste(f.to_image().resize((1120,700),Image.Resampling.LANCZOS),(0,44))
     ImageDraw.Draw(canvas).text((14,7),title,font=font,fill='#dce9f7')
     frame=av.VideoFrame.from_image(canvas);frame.pts=frames;frame.time_base=Fraction(1,FPS);frames+=1
     for p in v.encode(frame):c.mux(p)
 for p in v.encode():c.mux(p)
 for n in range(0,sounds.shape[1],1024):
  f=av.AudioFrame.from_ndarray(np.ascontiguousarray(sounds[:,n:n+1024]),format='fltp',layout='stereo');f.sample_rate=RATE;f.pts=n;f.time_base=Fraction(1,RATE)
  for p in a.encode(f):c.mux(p)
 for p in a.encode():c.mux(p)
# 元の着地音と実ミックスを照合し、呼び出し記録だけでなく実際の音成分を調べる。
source=read_audio(REPO/'DemoApp/assets/audio/se/rica_landing.wav').mean(axis=0)[:24000]
diagnosis=[]
for label,samples in zip(['before','after'],raw):
 start=round(15.8*RATE);window=samples.mean(axis=0)[start:round(19.8*RATE)]
 corr=correlate(window,source,mode='valid',method='fft');lag=int(np.argmax(np.abs(corr)));part=window[lag:lag+len(source)]
 gain=float(np.dot(part,source)/np.dot(source,source))
 diagnosis.append({'version':label,'landingAudioStart':(start+lag)/RATE,'estimatedLandingDbInMix':float(20*np.log10(abs(gain))),'sourceCorrelation':float(np.corrcoef(part,source)[0,1])})
assert diagnosis[1]['estimatedLandingDbInMix']-diagnosis[0]['estimatedLandingDbInMix']>5
with av.open(str(out)) as c:
 rate=float(c.streams.video[0].average_rate);decoded=sum(1 for _ in c.decode(video=0))
encoded=read_audio(out);peak=float(np.abs(encoded).max())
assert frames==decoded==420 and rate==30 and peak<1
report={'frames':decoded,'fps':rate,'seconds':decoded/rate,'audioPeak':peak,'landingComponent':diagnosis,'audio':'actual Godot mixes, before then after; no added sounds'}
(P/'comparison-check.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('LANDING_COMPARISON_OK',json.dumps(report))
