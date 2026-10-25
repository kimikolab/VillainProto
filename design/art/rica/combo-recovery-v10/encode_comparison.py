"""二連撃の振り抜きから斬り返しまで、v9/v10の実描画を通常速度と1/4速で比べる。"""
from pathlib import Path
import json
import av
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
FLOW = HERE.parent / 'attack-motion-v1'
REPO = HERE.parents[3]
FILES = ['rica-four-stage-flow-comparison-combo-flow-v9.mp4',
         'rica-four-stage-flow-comparison-recovery-v10.mp4']
START, END = 95, 161
panels = []
for name in FILES:
    frames = []
    with av.open(str(FLOW/name)) as container:
        for i, frame in enumerate(container.decode(video=0)):
            if START <= i < END:
                frames.append(frame.to_image().crop((0,650,560,1200)))
    assert len(frames) == END-START
    panels.append(frames)

before = json.loads((HERE/'before-capture-v9.json').read_text(encoding='utf-8'))['records']
after = json.loads((REPO/'output/rica-fxviewer-flow/stage2/capture.json').read_text(encoding='utf-8'))['records']
def phase(record):
    if record.get('link'):
        return '次の一撃へ振りかぶる'
    clip, frame = record['clip'],record['clipFrame']
    if clip == 'rica_rise':
        return '1撃目の振り抜き' if frame<0 or frame>=9 else '1撃目の切り上げ'
    if clip == 'rica_return':
        return '2撃目の構え' if frame<2 and frame>=0 else '2撃目の振り下ろし・振り抜き'
    return '待機へ戻る'

font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',22)
small = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',19)
output = HERE/'rica-double-recovery-before-after-v10.mp4'
with av.open(str(output),'w') as container:
    stream = container.add_stream('libx264',rate=30)
    stream.width, stream.height = 1120,720
    stream.pix_fmt = 'yuv420p'
    stream.options = {'crf':'17','preset':'medium'}
    for repeat, speed in [(1,'通常速度 ×1'),(4,'1/4速 ×0.25')]:
        for i in range(END-START):
            canvas = Image.new('RGB',(1120,720),'#0b1110')
            draw = ImageDraw.Draw(canvas)
            for col, label in enumerate(['修正前（v9）','修正後｜振りかぶりの斬撃軌跡を除去']):
                x = col*560
                canvas.paste(panels[col][i],(x,50))
                draw.text((x+12,12),label,font=font,fill='#e1c9a9' if col==0 else '#d7e9ff')
                draw.text((x+12,614),phase([before,after][col][i+START]),font=small,fill='#cfddd9')
            draw.text((18,654),'6–8層・二連撃｜実描画・同倍率・同時刻',font=small,fill='#cfddd9')
            draw.text((1102,654),speed,font=small,fill='#d2eeff',anchor='ra')
            draw.text((18,685),'1撃目の振り抜き → 武器を構え直す → 2撃目を振り下ろす',font=small,fill='#cfddd9')
            if repeat==1 and i+START in [108,120,123,125,128,132,136]:
                canvas.save(HERE/f'comparison-check-{i+START:03}.jpg',quality=94)
            for _ in range(repeat):
                for packet in stream.encode(av.VideoFrame.from_image(canvas)):
                    container.mux(packet)
    for packet in stream.encode(): container.mux(packet)
print('RECOVERY_BEFORE_AFTER_OK frames=330 fps=30 seconds=11 size=1120x720')
