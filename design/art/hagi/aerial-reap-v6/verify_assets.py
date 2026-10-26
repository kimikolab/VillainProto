"""原画・全身セル・接続・実録画を読み取り検査し、出典を残す。"""
import hashlib
import json
from pathlib import Path
import av
from PIL import Image

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
RUNTIME=ROOT/'DemoApp/assets/portraits/battle/attack_clip'
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
spec=json.loads((HERE/'cel-animation.json').read_text('utf-8'))
clips=[]
for clip in spec['clips']:
    name='hagi_'+clip['key']; folder=RUNTIME/name
    meta=json.loads((folder/'clip.json').read_text('utf-8'))
    frames=sorted(folder.glob('*.png'))
    assert len(frames)==meta['frames'] and len(meta['vertical'])==len(frames)
    for path in frames:
        with Image.open(path) as im:
            assert im.size==tuple(meta['size']) and im.mode=='RGBA'
            box=im.getchannel('A').getbbox()
            assert box and 0<box[0]<box[2]<im.width and 0<box[1]<box[3]<im.height
    clips.append(dict(name=name,frames=len(frames),contact=meta['contact'],fps=meta['fps']))
joins=[]
for outgoing in ('air_reap_cut_hold_v6','air_finish_cut_hold_v6'):
    a=sorted((RUNTIME/('hagi_'+outgoing)).glob('*.png'))[-1]
    for follow in ('air_after_v6','air_after_hold_v6','air_after_finish_v6'):
        b=sorted((RUNTIME/('hagi_'+follow)).glob('*.png'))[0]
        assert sha(a)==sha(b)
        joins.append(dict(outgoing=outgoing,following=follow,shared_cel_sha256=sha(a)))
videos=[]
for path in sorted((HERE/'video').iterdir()):
    if path.suffix not in ('.mp4','.gif'): continue
    with av.open(str(path)) as media:
        stream=media.streams.video[0]; count=0; last=0
        for frame in media.decode(video=0): count+=1; last=float(frame.time)
        assert count>0
        videos.append(dict(file=path.name,frames=count,width=stream.width,height=stream.height,
                           duration_last_frame=round(last,3),audio_streams=len(media.streams.audio)))
assert len(videos)==18
for name in ('study.log','battle.log'):
    log=(ROOT/'output/hagi-air-v6'/name).read_text('utf-8-sig')
    assert 'HAGI_RETURN_PREVIEW_CAPTURE_OK' in log and 'System.Exception' not in log
visual=(ROOT/'output/hagi-air-v6/visual-check.log').read_text('utf-8-sig')
assert visual.count('HAGI_SCRIPT_OK')==15
assert 'HAGI_AIR_VISUAL_OK' in visual and 'HAGI_VISUAL_CHECK_OK ok=True' in visual
verification=dict(build='0 warnings / 0 errors (incremental build)',
    new_whole_body_poses=0,reused_whole_body_poses=8,canvas=spec['canvas'],clips=clips,
    runtime_png_count=sum(c['frames'] for c in clips),shared_joins=joins,
    script_check='15 actual scripts: event parity',
    visual_check='pause at apex, 2x, airborne contact before landing, shared contact, vertical root/follow/mirror arcs, ground clearance, one landing, death/cancel',
    peak_height=dict(previous=1.48,current=1.48),
    weapon_review='Single master: continuous asymmetric crescent, one oval opening, one trio of grooves, straight shaft, inner-curve socket. Projected proportions still vary with painted perspective.',
    render='Godot GPU / 60fps / Main.EnterViewerLive',videos=videos,
    exit_diagnostics='Existing Font/CanvasItem/ObjectDB exit-only release warnings; captures/check exit 0')
(HERE/'verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2)+'\n','utf-8')

generation=dict(tool='No new image generation; reuse accepted v5 whole-body images',
    original_generation_record='../aerial-reap-v5/generation-record.json',
    sources=[dict(file=q['sheet'],sha256=sha(HERE/q['sheet'])) for q in spec['poses']],
    changes='Drop pose 3 to 2 frames; strike at height 0.70 before landing; follow at 0.12 before landing; red arc plane pitched 78 degrees about horizontal axis.',
    specification='cel-animation.json / prepare_spec.py')
(HERE/'generation-record.json').write_text(json.dumps(generation,ensure_ascii=False,indent=2)+'\n','utf-8')
print(f'HAGI_AIR_V6_ASSETS_OK poses=8 clips={len(clips)} frames={verification["runtime_png_count"]} joins={len(joins)} videos={len(videos)}')
