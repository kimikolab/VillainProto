"""ゲーム内で撮影した15fps連番を動画へ梱包する。原画の形状は変更しない。"""
import json
from pathlib import Path
from PIL import Image
import av

HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
ROOT=REPO/'output/rica-fxviewer'
all_frames=[]
for stage in range(4):
    directory=ROOT/f'stage{stage}'
    files=sorted(directory.glob('f*.webp'))
    info=directory/'capture.json'
    if info.exists():files=files[:json.loads(info.read_text())['frames']]
    frames=[Image.open(f).convert('RGB') for f in files]
    if not frames:continue
    all_frames+=frames
    gif=[f.crop((388,85,1440,755)).resize((842,536)).convert('P',palette=Image.Palette.ADAPTIVE,colors=192) for f in frames]
    gif[0].save(HERE/f'stage{stage}-ingame.gif',save_all=True,append_images=gif[1:]+[gif[-1]]*8,duration=67,loop=0)
temporary=HERE/'rica-four-stages-ingame.tmp.mp4'
with av.open(str(temporary),'w') as container:
    stream=container.add_stream('libx264',rate=15)
    stream.width,stream.height=all_frames[0].size;stream.pix_fmt='yuv420p';stream.options={'crf':'19','preset':'medium'}
    for im in all_frames:
        for packet in stream.encode(av.VideoFrame.from_image(im)):container.mux(packet)
    for packet in stream.encode():container.mux(packet)
temporary.replace(HERE/'rica-four-stages-ingame.mp4')
print('ゲーム内4段の動画',len(all_frames),'frames / 15fps')
