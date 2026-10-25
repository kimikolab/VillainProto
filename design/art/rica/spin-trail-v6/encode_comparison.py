"""実描画のv5/v6から3–5層を同倍率で抜き出し、通常速度と半速で比べる。"""
from pathlib import Path
import av
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
FLOW = HERE.parent / 'attack-motion-v1'
FILES = ['rica-four-stage-flow-comparison-overhead-spin-v5.mp4',
         'rica-four-stage-flow-comparison-orbit-v6.mp4']
panels = []
for name in FILES:
    frames = []
    with av.open(str(FLOW/name)) as container:
        for i, frame in enumerate(container.decode(video=0)):
            if 84 <= i < 171:
                frames.append(frame.to_image().crop((640,180,1040,580)).resize((560,560),Image.Resampling.LANCZOS))
    assert len(frames) == 87
    panels.append(frames)

font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',24)
small = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',20)
output = HERE/'rica-spin-before-after-v6.mp4'
with av.open(str(output),'w') as container:
    stream = container.add_stream('libx264',rate=30)
    stream.width, stream.height = 1120,660
    stream.pix_fmt = 'yuv420p'
    stream.options = {'crf':'17','preset':'medium'}
    for repeat, label in [(1,'通常速度 ×1'),(2,'半速 ×0.5')]:
        for i in range(87):
            canvas = Image.new('RGB',(1120,660),'#0b1110')
            canvas.paste(panels[0][i],(0,50)); canvas.paste(panels[1][i],(560,50))
            draw = ImageDraw.Draw(canvas)
            draw.text((18,10),'修正前（v5）',font=font,fill='#b9ced1')
            draw.text((578,10),'修正後（v6）',font=font,fill='#d2eeff')
            draw.text((18,624),'3–5層・回転薙ぎ｜同倍率・同時刻',font=small,fill='#cfddd9')
            draw.text((1102,624),label,font=small,fill='#d2eeff',anchor='ra')
            if repeat==1 and i==40:canvas.save(HERE/'comparison-check.jpg',quality=94)
            for _ in range(repeat):
                for packet in stream.encode(av.VideoFrame.from_image(canvas)):
                    container.mux(packet)
    for packet in stream.encode(): container.mux(packet)
print('SPIN_BEFORE_AFTER_OK frames=261 fps=30 seconds=8.7 size=1120x660')
