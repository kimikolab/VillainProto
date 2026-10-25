"""同じ盤面で連続撮影した4段の待機→攻撃→待機を、等倍率の4分割動画にする。"""
import argparse
import json
from fractions import Fraction
from pathlib import Path
import av
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
ROOT = REPO / 'output/rica-fxviewer-flow'
parser = argparse.ArgumentParser()
parser.add_argument('--suffix', default='')
parser.add_argument('--shovel-audio', action='store_true')
parser.add_argument('--comparison-only', action='store_true', help='4段階の追従比較だけを書き出す')
args = parser.parse_args()
assert all(c.isalnum() or c=='-' for c in args.suffix)
suffix = '-' + args.suffix if args.suffix else ''
stages = []
for stage in range(4):
    folder = ROOT / f'stage{stage}'
    info = json.loads((folder / 'capture.json').read_text(encoding='utf-8'))
    files = sorted(folder.glob('f*.webp'))[:info['frames']]
    assert len(files) == info['frames'] and len(info['records']) == info['frames']
    assert info['frames'] == info['fps'] * 10
    assert info['returned'] >= 30
    stages.append((files, info))

fps = stages[0][1]['fps']
frame_count = stages[0][1]['frames']
assert all(info['fps'] == fps and info['frames'] == frame_count for _, info in stages)

font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc', 22)
small = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc', 18)
labels = ['0–2層｜シャベル打撃', '3–5層｜回転薙ぎ',
          '6–8層｜大剣の二連撃', '9層以上｜覚醒の三連撃']
colors = ['#c8d9db', '#c7edff', '#9b9bff', '#c18aff']
phases = {'idle': '待機', 'approach': '踏み込み', 'attack': '攻撃',
          'link': '連撃のつなぎ', 'return': '構えへ戻る', 'returned': '待機へ復帰'}
clip_labels = {'rica_shovel': 'シャベル打撃', 'rica_crescent': '回転薙ぎ',
               'rica_rise': '切り上げ', 'rica_return': '斬り返し',
               'rica_thrust': '大槍の突き', 'rica_sweep': '回転斬り',
               'rica_descend': '落下斬り'}
report = dict(fps=fps, frames=frame_count, seconds=10, continuous=True,
              leadSeconds=2, crop=[560,550], tracking='projected ground point; equal pixel scale', stages=[])
for stage, (_, info) in enumerate(stages):
    records = info['records']
    report['stages'].append(dict(stage=stage, attacks=info['expected'],
        approach_frames=sum(bool(r.get('approach')) for r in records),
        first_attack=next(r['frame'] for r in records if r['phase']=='attack'),
        first_returned=next(r['frame'] for r in records if r['phase']=='returned'),
        returned_frames=info['returned'],
        returned_idle_frames=len({r['idleFrame'] for r in records if r['phase']=='returned'}),
        final_distance=records[-1]['distance']))

# 確認用の金属打撃音だけを左上の実接触コマへ同期する。ゲームでも同じ素材を-8 dBで使う。
audio_samples = None
audio_rate = 44100
if args.shovel_audio:
    import numpy as np
    meta = json.loads((REPO/'DemoApp/assets/portraits/battle/attack_clip/rica_shovel/clip.json').read_text(encoding='utf-8'))
    hit = next(r['frame'] for r in stages[0][1]['records'] if r['clip']=='rica_shovel' and r['clipFrame']>=meta['contact'])
    sound = REPO/'DemoApp/assets/audio/se/kado_hit.mp3'
    resampler = av.AudioResampler(format='fltp', layout='stereo', rate=audio_rate)
    pieces = []
    with av.open(str(sound)) as container:
        for f in container.decode(audio=0):
            pieces.extend(p.to_ndarray() for p in resampler.resample(f))
    pieces.extend(p.to_ndarray() for p in resampler.resample(None))
    sample = np.concatenate(pieces, axis=1).astype('float32') * (10**(-8/20))
    audio_samples = np.zeros((2, audio_rate*10), dtype='float32')
    start = round(hit/fps*audio_rate)
    count = min(sample.shape[1], audio_samples.shape[1]-start)
    audio_samples[:,start:start+count] = sample[:,:count]
    report['audio'] = dict(source='DemoApp/assets/audio/se/kado_hit.mp3', panel='top-left shovel only',
        gain_db=-8, offset_seconds=hit/fps, sample_rate=audio_rate)


def movie(name, frames, size):
    path = HERE / name
    temporary = path.with_suffix('.tmp.mp4')
    with av.open(str(temporary), 'w') as container:
        stream = container.add_stream('libx264', rate=fps)
        stream.width, stream.height = size
        stream.pix_fmt = 'yuv420p'
        stream.options = {'crf': '18', 'preset': 'medium'}
        sound_stream = None
        if audio_samples is not None:
            sound_stream = container.add_stream('aac', rate=audio_rate)
            sound_stream.layout = 'stereo'
        for im in frames:
            for packet in stream.encode(av.VideoFrame.from_image(im)):
                container.mux(packet)
        for packet in stream.encode():
            container.mux(packet)
        if sound_stream is not None:
            for n in range(0, audio_samples.shape[1], 1024):
                f = av.AudioFrame.from_ndarray(np.ascontiguousarray(audio_samples[:,n:n+1024]),format='fltp',layout='stereo')
                f.sample_rate = audio_rate; f.time_base = Fraction(1,audio_rate); f.pts = n
                for packet in sound_stream.encode(f):container.mux(packet)
            for packet in sound_stream.encode():container.mux(packet)
    temporary.replace(path)


def comparison():
    for frame in range(frame_count):
        canvas = Image.new('RGB', (1120, 1280 if args.shovel_audio else 1252), '#0b1110')
        draw = ImageDraw.Draw(canvas)
        for stage, (files, info) in enumerate(stages):
            x, y = stage % 2 * 560, stage // 2 * 600
            r = info['records'][frame]
            gx, gy = r['ground']
            # リィカの地面上の位置へ追従する。倍率は全段・全コマで同一。
            left = round(max(388, min(880, gx-260)))
            top = round(max(100, min(196, gy-430)))
            with Image.open(files[frame]) as source:
                canvas.paste(source.convert('RGB').crop((left, top, left+560, top+550)), (x, y+50))
            draw.text((x+12, y+13), labels[stage], font=font, fill=colors[stage])
            phase = clip_labels.get(r['clip'], phases[r['phase']]) if r['phase']=='attack' else phases[r['phase']]
            draw.text((x+548, y+16), phase, font=small, fill=colors[stage], anchor='ra')
        draw.text((18, 1214), '同じ駒を連続再生｜追従拡大・全段同倍率｜通常速度 ×1', font=small, fill='#d1dcd8')
        draw.text((1100, 1214), f'{frame/fps:04.1f} 秒', font=small, fill='#d1dcd8', anchor='ra')
        if args.shovel_audio:
            draw.text((18,1241),'SE確認：左上のシャベル命中音に同期',font=small,fill='#bdb6a3')
        if frame in [round(t * fps) for t in [1, 2.8, 3.0, 3.1, 3.4, 3.7, 4, 4.2, 4.4, 4.6, 5, 6, 9]]:
            canvas.save(HERE / f'flow-check-{frame:03}{suffix}.jpg', quality=92)
        yield canvas


def full_board():
    for frame in range(frame_count):
        canvas = Image.new('RGB', (2104, 1392), '#0b1110')
        draw = ImageDraw.Draw(canvas)
        for stage, (files, info) in enumerate(stages):
            x, y = stage % 2 * 1052, stage // 2 * 696
            with Image.open(files[frame]) as source:
                canvas.paste(source.convert('RGB').crop((388, 100, 1440, 746)), (x, y+50))
            draw.text((x+16,y+12), labels[stage], font=font, fill=colors[stage])
            draw.text((x+1036,y+16), phases[info['records'][frame]['phase']], font=small,
                      fill=colors[stage], anchor='ra')
        yield canvas


movie(f'rica-four-stage-flow-comparison{suffix}.mp4', comparison(), (1120,1280 if args.shovel_audio else 1252))
if not args.comparison_only:
    movie(f'rica-four-stage-flow-full-board{suffix}.mp4', full_board(), (2104,1392))
(HERE / f'flow-preview{suffix}.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('FLOW_VIDEO_OK', report)
