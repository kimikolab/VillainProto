"""Godotの実描画連番を、演奏C→ギフト→追加攻撃の確認動画にする。"""
from pathlib import Path
from fractions import Fraction
import argparse, json
import av
from PIL import Image

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('--source', type=Path, default=REPO / '.tmp/ere-song-start-v1')
parser.add_argument('--name', default='ere-song-start')
args = parser.parse_args()
assert '/' not in args.name and '\\' not in args.name
SOURCE = args.source.resolve()
groups = {side: sorted((SOURCE / side).glob('*.png')) for side in ('ally', 'enemy')}
assert all(len(paths) > 150 for paths in groups.values())
for side, paths in groups.items():
    assert [p.name for p in paths] == [f'{i:04}.png' for i in range(len(paths))], side

frames = [path for paths in groups.values() for path in paths]
with av.open(str(ROOT / (args.name + '.mp4')), 'w') as output:
    stream = output.add_stream('libx264', rate=30)
    stream.width, stream.height, stream.pix_fmt = 1440, 900, 'yuv420p'
    stream.options = {'crf': '19', 'preset': 'medium'}
    for n, path in enumerate(frames):
        with Image.open(path) as image:
            frame = av.VideoFrame.from_image(image.convert('RGB'))
        frame.pts, frame.time_base = n, Fraction(1, 30)
        for packet in stream.encode(frame): output.mux(packet)
    for packet in stream.encode(): output.mux(packet)

paths = groups['ally']
indices = list(range(0, len(paths), 2))
preview = []
for n in indices:
    with Image.open(paths[n]) as image:
        preview.append(image.crop((290, 205, 1010, 610)).convert('RGB').resize((608, 342), Image.Resampling.LANCZOS))
swatches = Image.new('RGB', (608, 342 * 6))
for k in range(6): swatches.paste(preview[k * (len(preview) - 1) // 5], (0, k * 342))
palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
gif = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in preview]
times = [round(n / 30 * 100) * 10 for n in indices + [len(paths)]]
durations = [b - a for a, b in zip(times, times[1:])]
gif[0].save(ROOT / (args.name + '.gif'), save_all=True, append_images=gif[1:], duration=durations, loop=0, optimize=False)
with av.open(str(ROOT / (args.name + '.mp4'))) as check:
    assert sum(1 for _ in check.decode(video=0)) == len(frames)
report = {'source': str(SOURCE.relative_to(REPO)), 'fps': 30,
    'frames': {side: len(paths) for side, paths in groups.items()}, 'total_frames': len(frames),
    'seconds': len(frames) / 30, 'gif_seconds': sum(durations) / 1000, 'motion': '既存C・変更なし', 'audio': False}
metadata = 'preview.json' if args.name == 'ere-song-start' else args.name + '.json'
(ROOT / metadata).write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('SONG_START_PREVIEW_OK', report['total_frames'], 'frames', round(report['seconds'], 2), 'seconds')
