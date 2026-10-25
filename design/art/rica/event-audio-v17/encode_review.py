"""Godot Movie Makerの実録画をMP4へ変換。音の後付け・動作補間はしない。"""
from pathlib import Path
from fractions import Fraction
import json,hashlib,shutil,argparse
import av,numpy as np
HERE=Path(__file__).resolve().parent;REPO=HERE.parents[3]
parser=argparse.ArgumentParser()
parser.add_argument('--input',type=Path,default=REPO/'.tmp/rica-event-audio-v17/review.avi')
parser.add_argument('--output-dir',type=Path,default=HERE)
parser.add_argument('--name',default='rica-awaken-absorb-landing-v17.mp4')
args=parser.parse_args();P=args.output_dir;P.mkdir(parents=True,exist_ok=True)
source=args.input;dest=P/args.name
count=0;sample_count=0
with av.open(str(source)) as original,av.open(str(dest),'w') as out:
 v=original.streams.video[0];video=out.add_stream('libx264',rate=30)
 video.width=v.width;video.height=v.height;video.pix_fmt='yuv420p';video.options={'crf':'18','preset':'medium'}
 audio=out.add_stream('aac',rate=48000);audio.layout='stereo'
 resampler=av.AudioResampler(format='fltp',layout='stereo',rate=48000)
 def sound(frame):
  global sample_count
  frame.pts=sample_count;frame.time_base=Fraction(1,48000);sample_count+=frame.samples
  for p in audio.encode(frame):out.mux(p)
 for packet in original.demux():
  for frame in packet.decode():
   if packet.stream.type=='video':
    if count in [45,141,237,333,485,500]:frame.to_image().save(P/f'check-{count:03}.jpg',quality=93)
    frame=av.VideoFrame.from_image(frame.to_image());frame.pts=count;frame.time_base=Fraction(1,30);count+=1
    for p in video.encode(frame):out.mux(p)
   elif packet.stream.type=='audio':
    for f in resampler.resample(frame):sound(f)
 for p in video.encode():out.mux(p)
 for f in resampler.resample(None):sound(f)
 for p in audio.encode():out.mux(p)
assert count==660
shutil.copyfile(REPO/'output/rica-event-audio-v17/capture.json',P/'capture.json')
files={'rica_awaken_3.wav':'DSGNSynth_BUFF-Plus Damage_HY_PC-001.wav','rica_awaken_6.wav':'DSGNSynth_BUFF-Plus Damage_HY_PC-003.wav','rica_awaken_9.wav':'DSGNSynth_BUFF-Plus Damage_HY_PC-006.wav','rica_landing.wav':'DSGNImpt_EXPLOSION-Mecha Limb Explosion_HY_PC-001.wav','rica_absorb.wav':'MAGAngl_BUFF-Effect Success_HY_PC-001.wav'}
root=Path("D:/Assets/SE/Helton Yan's Pixel Combat - Single Files")
hashes={}
for local,raw in files.items():
 digest=hashlib.sha256((REPO/'DemoApp/assets/audio/se'/local).read_bytes()).hexdigest()
 assert digest==hashlib.sha256((root/raw).read_bytes()).hexdigest()
 hashes[local]={'source':raw,'sha256':digest}
(P/'source-check.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8')
with av.open(str(dest)) as c:
 s=c.streams.video[0];fps=float(s.average_rate);decoded=sum(1 for _ in c.decode(video=0));size=[s.width,s.height]
with av.open(str(dest)) as c:
 resampler=av.AudioResampler(format='fltp',layout='stereo',rate=48000)
 parts=[a.to_ndarray() for f in c.decode(audio=0) for a in resampler.resample(f)]
 parts.extend(a.to_ndarray() for a in resampler.resample(None));samples=np.concatenate(parts,axis=1)
peak=float(np.abs(samples).max());assert peak<1 and decoded==660 and fps==30
records=json.loads((P/'capture.json').read_text(encoding='utf-8'))['records']
energy=[]
for e in records:
 t=e['engineFrame']/30;a=samples[:,int(t*48000):int((t+1)*48000)]
 rms=float(np.sqrt(np.mean(a*a)));assert rms>.002
 energy.append({'cue':e['cue'],'seconds':t,'peak':float(np.abs(a).max()),'rms':rms})
report={'frames':decoded,'fps':fps,'size':size,'seconds':decoded/fps,'audioPeak':peak,'audio':'Godot Movie Maker actual engine mix; no added sounds','cues':energy}
(P/'video-check.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('RICA_EVENT_MOVIE_OK',json.dumps(report))
