"""FxViewerの実描画を待機動画と4段比較へ梱包する。原画・モーションは変更しない。"""
import json
from pathlib import Path
import av
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
ROOT = REPO / 'output/rica-fxviewer-idle'
stages = []
for stage in range(4):
    folder = ROOT / f'stage{stage}'
    info = json.loads((folder / 'capture.json').read_text())
    files = sorted(folder.glob('f*.webp'))[:info['frames']]
    assert len(files) >= 120, (stage, len(files))
    stages.append(files)


def movie(path, frames, size):
    temporary = path.with_suffix('.tmp.mp4')
    count = 0
    with av.open(str(temporary), 'w') as container:
        stream = container.add_stream('libx264', rate=15)
        stream.width, stream.height = size
        stream.pix_fmt = 'yuv420p'
        stream.options = {'crf': '18', 'preset': 'medium'}
        for im in frames:
            for packet in stream.encode(av.VideoFrame.from_image(im)):
                container.mux(packet)
            count += 1
        for packet in stream.encode():
            container.mux(packet)
    temporary.replace(path)
    return count


def full_frames():
    for files in stages:
        for file in files:
            with Image.open(file) as im:
                yield im.convert('RGB')


font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc', 20)
labels = ['0–2層：通常のシャベル', '3–5層：霊刃の低い構え',
          '6–8層：大剣の上段構え', '9層以上：覚醒大剣と紫の焔']
colors = ['#c8d9db', '#c7edff', '#9b9bff', '#c18aff']


def comparison_frames():
    for index in range(min(map(len, stages))):
        canvas = Image.new('RGB', (820, 1016), '#0b1110')
        draw = ImageDraw.Draw(canvas)
        for stage, files in enumerate(stages):
            x, y = (stage % 2) * 410, (stage // 2) * 508
            with Image.open(files[index]) as im:
                # 全段で同じ画面領域を使い、人物の大きさ・武器の伸びを比較する。
                canvas.paste(im.convert('RGB').crop((520, 110, 930, 570)), (x, y + 48))
            draw.text((x + 14, y + 12), labels[stage], font=font, fill=colors[stage])
        if index == 30:
            canvas.save(HERE / 'rica-four-idles-comparison.jpg', quality=92)
        yield canvas


full = movie(HERE / 'rica-four-idles-ingame.mp4', full_frames(), (1440, 900))
comparison = movie(HERE / 'rica-four-idles-comparison.mp4', comparison_frames(), (820, 1016))
report = dict(fps=15, stage_frames=[len(f) for f in stages], full_frames=full,
    comparison_frames=comparison, comparison_crop=[520, 110, 930, 570],
    source='Vulkan Forward+ FxViewer capture; unmodified game rendering')
(HERE / 'idle-preview.json').write_text(json.dumps(report, indent=2) + '\n')
print('IDLE_VIDEO_OK', report)
