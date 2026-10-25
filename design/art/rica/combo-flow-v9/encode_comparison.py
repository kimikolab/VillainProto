"""同じ倍率の実描画から、二連撃・三連撃の追加前後を通常速度と半速で比べる。"""
from pathlib import Path
import json
import av
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
FLOW = HERE.parent / 'attack-motion-v1'
FILES = ['rica-four-stage-flow-comparison-approach-v8.mp4',
         'rica-four-stage-flow-comparison-combo-flow-v9.mp4']
START, END = 84, 224
panels = []
for name in FILES:
    frames = []
    with av.open(str(FLOW/name)) as container:
        for i, frame in enumerate(container.decode(video=0)):
            if START <= i < END:
                source = frame.to_image()
                frames.append([source.crop((0,650,560,1200)), source.crop((560,650,1120,1200))])
    assert len(frames) == END-START
    panels.append(frames)

font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',23)
small = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',19)
output = HERE/'rica-double-triple-before-after-v9.mp4'
with av.open(str(output),'w') as container:
    stream = container.add_stream('libx264',rate=30)
    stream.width, stream.height = 1120,1280
    stream.pix_fmt = 'yuv420p'
    stream.options = {'crf':'17','preset':'medium'}
    for repeat, label in [(1,'通常速度 ×1'),(2,'半速 ×0.5')]:
        for i in range(END-START):
            canvas = Image.new('RGB',(1120,1280),'#0b1110')
            draw = ImageDraw.Draw(canvas)
            for row, title in enumerate(['6–8層｜大剣の二連撃','9層以上｜覚醒の三連撃']):
                for col, version in enumerate(['追加前','追加後｜つなぎの全身原画を追加']):
                    x, y = col*560,row*600
                    canvas.paste(panels[col][i][row],(x,y+50))
                    draw.text((x+12,y+7),title,font=small,fill='#d2eeff')
                    draw.text((x+12,y+29),version,font=small,fill='#beb7d2' if col==0 else '#e2d4ff')
            draw.text((18,1210),'実際のFxViewerを同倍率・同時刻で比較',font=font,fill='#d2eeff')
            draw.text((18,1244),'踏み込み → 各打の振り抜き → 次の構え → 帰還',font=small,fill='#becdcb')
            draw.text((1102,1244),label,font=small,fill='#e2d4ff',anchor='ra')
            if repeat==1 and i in [28,36,48,60,72,84,96]:
                canvas.save(HERE/f'comparison-check-{i+START:03}.jpg',quality=94)
            for _ in range(repeat):
                for packet in stream.encode(av.VideoFrame.from_image(canvas)):
                    container.mux(packet)
    for packet in stream.encode(): container.mux(packet)
(HERE/'comparison-report.json').write_text(json.dumps(dict(
    sources=FILES, frames=(END-START)*3,fps=30,seconds=(END-START)/10,
    size=[1120,1280], source_frames=[START,END], columns=['v8','v9'],
    rows=['6–8層・二連撃','9層以上・三連撃'], speed=[1,.5],audio=False,
    processing='actual rendered video crop and layout only; no optical flow or animation interpolation'
),ensure_ascii=False,indent=2),encoding='utf-8')
print('COMBO_BEFORE_AFTER_OK frames=420 fps=30 seconds=14 size=1120x1280')
