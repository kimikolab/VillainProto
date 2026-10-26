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
for outgoing in ('air_reap_cut_hold_v5','air_finish_cut_hold_v5'):
    a=sorted((RUNTIME/('hagi_'+outgoing)).glob('*.png'))[-1]
    for follow in ('air_after_v5','air_after_hold_v5','air_after_finish_v5'):
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
    log=(ROOT/'output/hagi-air-v5'/name).read_text('utf-8-sig')
    assert 'HAGI_RETURN_PREVIEW_CAPTURE_OK' in log and 'System.Exception' not in log
visual=(ROOT/'output/hagi-air-v5/visual-check.log').read_text('utf-8-sig')
assert visual.count('HAGI_SCRIPT_OK')==15
assert 'HAGI_AIR_VISUAL_OK' in visual and 'HAGI_VISUAL_CHECK_OK ok=True' in visual
verification=dict(build='2 existing BattleCore warnings CS0162/CS8602 / 0 errors',
    new_whole_body_poses=7,reused_guard_poses=1,canvas=spec['canvas'],clips=clips,
    runtime_png_count=sum(c['frames'] for c in clips),shared_joins=joins,
    script_check='15 actual scripts: event parity',
    visual_check='pause at apex, 2x, contact transform, shared contact, one landing, reverse/mirror, death/cancel',
    peak_height=dict(previous=1.12,current=1.48),
    weapon_review='Single master: continuous asymmetric crescent, one oval opening, one trio of grooves, straight shaft, inner-curve socket. Projected proportions still vary with painted perspective.',
    render='Godot GPU / 60fps / Main.EnterViewerLive',videos=videos,
    exit_diagnostics='Existing Font/CanvasItem/ObjectDB exit-only release warnings; captures/check exit 0')
(HERE/'verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2)+'\n','utf-8')

originals={
    'scythe-master':'0ef834c8-4266-4c78-b417-b13b4256ee5f',
    'load':'bc0cbeab-1cea-46a0-a470-3370e31703d1',
    'launch':'7d193977-8bfd-4ffd-9118-0852214d0d98',
    'twist':'2f6d104e-99a1-40dd-a984-75e898bc579f',
    'peak':'eb197ab8-c425-4034-86a3-51337c65039a',
    'drop':'f9b231be-7956-4356-8498-a1396e8711a9',
    'impact':'1cff20db-9f09-4b2a-9214-75f86f42cb9b',
    'land':'c525ce76-cf24-41a2-9946-3f4144742344'}
sources=[]
for key,original in originals.items():
    path=HERE/('scythe-master.png' if key=='scythe-master' else f'source-{key}.png')
    prompt='prompt-land-horizontal.txt' if key=='land' else f'prompt-{key}.txt'
    refs=['DemoApp/assets/portraits/battle/hagi_idle_right.png']
    if key!='scythe-master': refs.insert(0,'scythe-master.png')
    if key=='peak': refs.append('C:/Users/Pon/AppData/Local/Temp/codex-clipboard-963ae91d-7171-408a-a68c-328a3683b61e.png (pose only)')
    elif key!='scythe-master': refs.append('source-peak.png')
    sources.append(dict(file=path.name,original=f'exec-{original}.png',prompt=prompt,references=refs,sha256=sha(path)))
generation=dict(tool='built-in image_gen',transparent_background=True,
    original_directory='C:/Users/Pon/.codex/generated_images/01a16ee4-957d-7073-aa2e-d3e415aca3ce',
    sources=sources,
    rejected=[dict(file='rejected-rise-sheet.png',original='exec-ba342b4e-2ad1-4a14-916f-74d6c4e82a59.png',
                   prompt='prompt-rise-sheet.txt',reason='Scythe shrinks and changes between cells.'),
              dict(file='rejected-land-curved.png',original='exec-e7995d43-ab9b-469b-b231-370c4b125391.png',
                   prompt='prompt-land.txt',reason='Diagonal staff bends between grips.'),
              dict(file='rejected-land-fix.png',original='exec-ced02052-9bf6-44af-a4bb-5aaaabc5940b.png',
                   prompt='prompt-land-fix.txt',references=['rejected-land-curved.png','scythe-master.png'],reason='Staff still bends; replaced with newly generated horizontal hold.')],
    reused_guard='DemoApp/assets/portraits/battle/hagi_idle_right.png (copied to source-guard.png)',
    exporter='Godot HagiCelExport: connected whole-body alpha, uniform scale; no part warp/reverse/crossfade',
    specification='cel-animation.json / prepare_spec.py')
(HERE/'generation-record.json').write_text(json.dumps(generation,ensure_ascii=False,indent=2)+'\n','utf-8')
print(f'HAGI_AIR_V5_ASSETS_OK poses=8 clips={len(clips)} frames={verification["runtime_png_count"]} joins={len(joins)} videos={len(videos)}')
