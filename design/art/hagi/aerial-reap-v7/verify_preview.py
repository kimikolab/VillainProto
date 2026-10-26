"""既存原画を使う斬撃改修のGPU録画・検査ログを検証する。"""
import hashlib
import json
from pathlib import Path
import av

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
logs=ROOT/'output/hagi-air-v7'
check=(logs/'visual-check.log').read_text('utf-8-sig')
capture=(logs/'battle.log').read_text('utf-8-sig')
assert check.count('HAGI_SCRIPT_OK')==15
assert 'HAGI_AIR_VISUAL_OK' in check and 'HAGI_VISUAL_CHECK_OK ok=True' in check
assert 'HAGI_RETURN_PREVIEW_CAPTURE_OK' in capture
assert 'ERROR:' not in check and 'System.Exception' not in capture and 'Shader compilation failed' not in capture
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
record=dict(build='0 warnings / 0 errors',source_cels='../aerial-reap-v6/cel-animation.json',
    new_images=0,render='Godot GPU / Main.EnterViewerLive / 60fps',
    tests='15 script parity; single shared crescent with no clock reset; open ends; vertical path; ground clearance; pause/speed/mirror/death/cancel/landing',
    files=[dict(path=f,sha256=hashlib.sha256((ROOT/f).read_bytes()).hexdigest()) for f in files],videos=videos,
    exit_diagnostics='Existing Font/CanvasItem/ObjectDB exit-only release warnings; capture/check exit 0')
(HERE/'verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n','utf-8')
print('HAGI_AIR_V7_PREVIEW_OK videos=6 single-shared-crescent')
