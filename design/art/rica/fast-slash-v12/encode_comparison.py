"""前回v11と今回v12の二連撃を、同じ実描画倍率で通常→1/4速の順に比較する。"""
from pathlib import Path
import json
import av
from PIL import Image,ImageDraw,ImageFont

HERE=Path(__file__).resolve().parent
FLOW=HERE.parent/'attack-motion-v1'
REPO=HERE.parents[3]
START,END=95,161
panels=[]
for filename in ['rica-four-stage-flow-comparison-overhead-v11.mp4',
                 'rica-four-stage-flow-comparison-fast-slash-v12.mp4']:
    frames=[]
    with av.open(str(FLOW/filename)) as c:
        for i,f in enumerate(c.decode(video=0)):
            if START<=i<END:
                frames.append(f.to_image().crop((0,650,560,1200)).resize((700,688),Image.Resampling.LANCZOS))
    assert len(frames)==END-START
    panels.append(frames)
records=[json.loads((HERE/'before-capture-v11.json').read_text(encoding='utf-8'))['records'],
         json.loads((REPO/'output/rica-fxviewer-flow/stage2/capture.json').read_text(encoding='utf-8'))['records']]
def phase(r,col):
    key,n=r['clip'],r['clipFrame']
    if r.get('link'):return '頭上へ構え直す' if n>=2 else '1撃目の振り抜き'
    if key=='rica_rise':return '1撃目の切り上げ・振り抜き'
    if key=='rica_return':
        if n<0:return '振り抜きの余韻'
        if n<(2 if col==0 else 4):return '頭上で溜める'
        if n<8:return '振り下ろす'
        if n==8:return '刃が敵を通る'
        return '一気に振り抜く'
    return '待機へ戻る'

font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',24)
small=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',21)
with av.open(str(HERE/'rica-double-fast-slash-before-after-v12.mp4'),'w') as c:
    stream=c.add_stream('libx264',rate=30);stream.width=1400;stream.height=860
    stream.pix_fmt='yuv420p';stream.options={'crf':'17','preset':'medium'}
    for repeat,speed in [(1,'通常速度 ×1'),(4,'1/4速 ×0.25')]:
        for i in range(END-START):
            canvas=Image.new('RGB',(1400,860),'#0b1110');d=ImageDraw.Draw(canvas)
            for col,title in enumerate(['前回（v11）','今回｜振りを加速・青白い剣閃を強化']):
                x=col*700;canvas.paste(panels[col][i],(x,50))
                d.text((x+14,11),title,font=font,fill='#e1c9a9' if col==0 else '#d7e9ff')
                d.text((x+14,750),phase(records[col][i+START],col),font=small,fill='#d1dfdc')
            d.text((18,795),'6–8層・二連撃｜ゲームの実描画・同倍率・同時刻',font=small,fill='#d1dfdc')
            d.text((1382,795),speed,font=small,fill='#d7e9ff',anchor='ra')
            d.text((18,827),'頭上で溜める → 加速して振り下ろす → 光の弧を残して振り抜く',font=small,fill='#d1dfdc')
            if repeat==1 and i+START in [108,125,129,131,132,133,135,136,137,139,142]:
                canvas.save(HERE/f'comparison-check-{i+START:03}.jpg',quality=94)
            for _ in range(repeat):
                for packet in stream.encode(av.VideoFrame.from_image(canvas)):c.mux(packet)
    for packet in stream.encode():c.mux(packet)
print('FAST_SLASH_BEFORE_AFTER_OK frames=330 fps=30 seconds=11 size=1400x860')
