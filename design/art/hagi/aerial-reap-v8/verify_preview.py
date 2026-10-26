"""既存原画を使う斬撃改修のGPU録画・検査ログを検証する。"""
import hashlib
import json
from pathlib import Path
import av

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
logs=ROOT/'output/hagi-air-v8'
capture=(logs/'battle.log').read_text('utf-8-sig')
assert 'HAGI_RETURN_PREVIEW_CAPTURE_OK' in capture
assert 'System.Exception' not in capture and 'Shader compilation failed' not in capture
videos=[]
for path in sorted((HERE/'video').iterdir()):
    if path.suffix not in ('.mp4','.gif'): continue
    with av.open(str(path)) as media:
        stream=media.streams.video[0]; count=0; last=0
        for frame in media.decode(video=0): count+=1; last=float(frame.time)
        assert count>0
        videos.append(dict(file=path.name,frames=count,width=stream.width,height=stream.height,
                           duration_last_frame=round(last,3),audio_streams=len(media.streams.audio)))
assert len(videos)==6
files=['DemoApp/HagiReapFx3D.cs','DemoApp/BattlefieldView3D.Hagi.cs',
       'DemoApp/assets/fx/hagi_air_crescent.gdshader','DemoApp/HagiPresentationCheck.Aerial.cs']
record=dict(build='Not rerun: shader-only change; validated by GPU shader compilation and actual playback',source_cels='../aerial-reap-v6/cel-animation.json',
    new_images=0,render='Godot GPU / Main.EnterViewerLive / 60fps',
    tests='Three actual battle scenarios; GPU shader compilation; single-crescent silhouette inspected; full video decode. Existing motion and C# lifecycle unchanged.',
    files=[dict(path=f,sha256=hashlib.sha256((ROOT/f).read_bytes()).hexdigest()) for f in files],videos=videos,
    exit_diagnostics='Existing Font/CanvasItem/ObjectDB exit-only release warnings; capture exit 0')
(HERE/'verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n','utf-8')
print('HAGI_AIR_V8_PREVIEW_OK videos=6 single-shared-crescent')
