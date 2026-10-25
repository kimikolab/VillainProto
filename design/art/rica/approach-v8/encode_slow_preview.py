"""4段階の踏み込みから攻撃開始を半速で並べて確認する。"""
from pathlib import Path
import av
from PIL import Image,ImageDraw,ImageFont

HERE=Path(__file__).resolve().parent
source=HERE.parent/'attack-motion-v1/rica-four-stage-flow-comparison-approach-v8.mp4'
font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',20)
output=HERE/'rica-four-stage-approach-slow-v8.mp4'
with av.open(str(source)) as incoming,av.open(str(output),'w') as outgoing:
    stream=outgoing.add_stream('libx264',rate=30)
    stream.width,stream.height=1120,1280;stream.pix_fmt='yuv420p'
    stream.options={'crf':'18','preset':'medium'}
    count=0
    for i,frame in enumerate(incoming.decode(video=0)):
        if not 66<=i<144:continue
        im=frame.to_image();d=ImageDraw.Draw(im)
        d.rectangle((0,1200,1120,1280),fill='#0b1110')
        d.text((18,1218),'武器を構えて前進 → 攻撃｜4段階・同倍率｜半速 ×0.5',font=font,fill='#dfedf0')
        for _ in range(2):
            for packet in stream.encode(av.VideoFrame.from_image(im)):outgoing.mux(packet)
            count+=1
    for packet in stream.encode():outgoing.mux(packet)
assert count==156
print('APPROACH_SLOW_OK frames=156 fps=30 seconds=5.2')
