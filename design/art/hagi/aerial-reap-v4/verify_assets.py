"""制作原画・全身セル・接続・実録画を読み取り検査し、出典と結果を残す。"""
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
for outgoing in ('air_reap_cut_hold_v4','air_finish_cut_hold_v4'):
    a=sorted((RUNTIME/('hagi_'+outgoing)).glob('*.png'))[-1]
    for follow in ('air_after_v4','air_after_hold_v4','air_after_finish_v4'):
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
assert len(videos)==16
for name in ('study.log','battle.log'):
    log=(ROOT/'output/hagi-air-v4'/name).read_text('utf-8-sig')
    assert 'HAGI_RETURN_PREVIEW_CAPTURE_OK' in log and 'System.Exception' not in log
full=(ROOT/'output/hagi-air-check.log').read_text('utf-8-sig')
visual=(ROOT/'output/hagi-air-visual-check-final.log').read_text('utf-8-sig')
assert 'HAGI_PRESENTATION_CHECK_OK' in full and full.count('HAGI_REPLAY_OK')==15
assert 'HAGI_AIR_VISUAL_OK' in visual and 'HAGI_VISUAL_CHECK_OK' in visual
verification=dict(build='0 warnings / 0 errors',new_whole_body_poses=7,reused_guard_poses=1,
    canvas=spec['canvas'],clips=clips,runtime_png_count=sum(c['frames'] for c in clips),shared_joins=joins,
    full_check='15 actual scripts: identical events/HP/attacks/reaps; replay/seek/reset',
    final_visual_check='pause at apex, 2x, contact transform, shared contact, one landing, reverse/mirror, death/cancel',
    render='Godot Vulkan Forward+ / RTX 5090 / 60fps / Main.EnterViewerLive',videos=videos,
    exit_diagnostics='Existing Font/CanvasItem/ObjectDB exit-only release warnings; checks/captures exit 0')
(HERE/'verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2)+'\n','utf-8')

generation=dict(tool='built-in image_gen',transparent_background=True,
    original_directory='C:/Users/Pon/.codex/generated_images/01a16ee4-957d-7073-aa2e-d3e415aca3ce',
    references=[dict(path='DemoApp/assets/portraits/battle/hagi_idle_right.png',role='Hagi identity/costume/weapon'),
        dict(path='C:/Users/Pon/AppData/Local/Temp/codex-clipboard-963ae91d-7171-408a-a68c-328a3683b61e.png',
             role='Airborne wind-up pose only; no character, costume, hood or style copying')],
    sheets=[dict(file='source-rise.png',original='exec-1e3f20d1-873d-4b47-a372-7736f21905ac.png',
                 prompt='prompt-rise.txt',adopted_cells=[0,1,2],sha256=sha(HERE/'source-rise.png')),
        dict(file='source-fall.png',original='exec-4e698658-51c4-41b2-9f22-7e2fefd8a4a7.png',
             prompt='prompt-fall-layout.txt',edit_target='source-fall-initial.png',
             initial_prompt='prompt-fall.txt',adopted_cells=[0,1,2,3],sha256=sha(HERE/'source-fall.png'))],
    rejected=[dict(file='source-rise.png',cell=3,reason='The two hands are not collinear on the rigid pole.'),
        dict(file='source-fall-initial.png',original='exec-c9f673d0-455a-4968-93b6-7a6a28ad166d.png',
             reason='The two lower silhouettes touched; repaired layout using built-in image_gen.')],
    reused_guard='../rotational-sweep-v3/source-link.png cell 3',
    exporter='Godot HagiCelExport: connected whole-body alpha cells, uniform scale only; no warp/reverse/crossfade',
    specification='cel-animation.json / prepare_spec.py')
(HERE/'generation-record.json').write_text(json.dumps(generation,ensure_ascii=False,indent=2)+'\n','utf-8')
print(f'HAGI_AIR_V4_ASSETS_OK poses=8 clips={len(clips)} frames={verification["runtime_png_count"]} joins={len(joins)} videos={len(videos)}')
