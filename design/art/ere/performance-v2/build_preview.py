"""現行Cと新候補を、同じ16fps・体格で比較する。部位の変形やコマの間引きはしない。"""
from pathlib import Path
from fractions import Fraction
import argparse, hashlib, json
import av
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[3]
SOURCE = Path('D:/comfyui/output/VillainProto/idle_loop')
parser = argparse.ArgumentParser()
parser.add_argument('--start-a', type=int, default=0)
parser.add_argument('--start-b', type=int, default=0)
args = parser.parse_args()
font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc', 19)
reference = Image.open(REPO / 'DemoApp/assets/portraits/battle/ere_singing_idle_right.png').convert('RGBA').resize((640, 960), Image.Resampling.LANCZOS)
sole = lambda im: int(np.where(np.array(im.getchannel('A')) > 128)[0].max())
sequences = [list(sorted((REPO / 'DemoApp/assets/portraits/battle/idle_loop/ere_singing').glob('*.png')))]
report = []
for label, seed, start in [('A', 2022, args.start_a), ('B', 3033, args.start_b)]:
    tag = f'ere_performance_v2_s{seed}_phrase{start}'
    files = sorted((SOURCE / (tag + '_rgba')).glob('*.png'))
    assert len(files) == 48, (tag, len(files))
    images = [Image.open(p).convert('RGBA') for p in files]
    bottoms = [sole(im) for im in images]
    shift = sole(reference) - round(float(np.median(bottoms)))
    assert abs(shift) <= 12, '元絵と足元が大きく違うため自動補正しない'
    target = ROOT / 'candidates' / label
    target.mkdir(parents=True, exist_ok=False)
    for i, im in enumerate(images):
        canvas = Image.new('RGBA', im.size)
        canvas.paste(im, (0, shift))
        canvas.save(target / f'{i:03}.png')
    sequences.append(sorted(target.glob('*.png')))
    arrays = [np.asarray(Image.open(p).convert('RGBA')) for p in sequences[-1]]
    green = max(int(((a[..., 3] > 200) & (a[..., 1].astype(int) - np.maximum(a[..., 0], a[..., 2]) > 20)).sum()) for a in arrays)
    report.append({'candidate': label, 'tag': tag, 'seed': seed, 'frames': 48, 'fps': 16,
        'type': 'one_shot', 'source_range': [start, start + 48], 'translation_y': shift,
        'sole_range': [min(bottoms) + shift, max(bottoms) + shift], 'reference_sole': sole(reference),
        'green_pixels': green, 'sha256_first': hashlib.sha256(sequences[-1][0].read_bytes()).hexdigest(),
        'directory': str(target.relative_to(REPO)).replace('\\', '/')})

sequences = [[Image.open(p).convert('RGBA') for p in files] for files in sequences]
assert all(len(seq) == 48 for seq in sequences)
idle = Image.open(REPO / 'DemoApp/assets/portraits/battle/idle_loop/ere/ere_idle_00.png').convert('RGBA')
preview = []
for n in range(72):
    page = Image.new('RGB', (960, 752), (23, 34, 31))
    draw = ImageDraw.Draw(page)
    for k, (label, sequence) in enumerate(zip(['現行C', 'A：新しい演奏', 'B：新しい演奏'], sequences)):
        frame = idle if n < 8 or n >= 64 else sequence[min(n - 8, 47)]
        small = frame.resize((320, 480), Image.Resampling.LANCZOS)
        page.paste(small, (k * 320, 32), small)
        close = frame.crop((235, 210, 440, 400)).resize((256, 237), Image.Resampling.LANCZOS)
        page.paste(close, (k * 320 + 32, 512), close)
        draw.text((k * 320 + 12, 3), label, font=font, fill=(232, 226, 203))
    preview.append(page)

swatches = Image.new('RGB', (960, 752 * 4))
for i, n in enumerate((12, 24, 36, 48)):
    swatches.paste(preview[n], (0, i * 752))
palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
gif = [im.resize((640, 501), Image.Resampling.LANCZOS).quantize(palette=palette, dither=Image.Dither.NONE) for im in preview]
gif[0].save(ROOT / 'performance-CAB.gif', save_all=True, append_images=gif[1:], duration=[60, 60, 60, 70] * 18, loop=0, optimize=False)
with av.open(str(ROOT / 'performance-CAB.mp4'), 'w') as output:
    stream = output.add_stream('libx264', rate=16)
    stream.width, stream.height, stream.pix_fmt = 960, 752, 'yuv420p'
    stream.options = {'crf': '18'}
    for n, im in enumerate(preview):
        frame = av.VideoFrame.from_image(im)
        frame.pts, frame.time_base = n, Fraction(1, 16)
        for packet in stream.encode(frame): output.mux(packet)
    for packet in stream.encode(): output.mux(packet)
(ROOT / 'candidates.json').write_text(json.dumps({'status': 'review', 'candidates': report}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
