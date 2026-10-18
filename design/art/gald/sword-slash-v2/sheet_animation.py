"""一枚の原画シートから6姿勢を分離し、後ろ足を基準に単発の斬撃へ並べる。"""
import json
import shutil
from pathlib import Path
import av
import cv2
import numpy as np
from scipy import ndimage
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
SOURCE_SIZE = (832, 768)
CROP = (32, 128, 816, 736)
SIZE = (784, 608)
FPS = 25

def plate(im):
    bg = Image.new('RGBA', im.size, '#182634')
    bg.alpha_composite(im)
    return bg.convert('RGB')

def main():
    # 補間候補の最終出力は比較用に保存してから、採用版で置き換える。
    for ext in ['gif', 'webp', 'mp4']:
        f = HERE / f'gald-jodan-cleave.{ext}'
        candidate = HERE / f'candidateC.{ext}'
        if f.exists() and not candidate.exists():
            shutil.copy2(f, candidate)
    src = Image.open(HERE / 'clean-six-poses.png').convert('RGBA')
    a = np.asarray(src)
    n, labels, stats, centers = cv2.connectedComponentsWithStats((a[...,3] > 128).astype('uint8'), 8)
    ids = [i for i in range(1,n) if stats[i,4] > 10000]
    assert len(ids) == 6, ('姿勢数', len(ids))
    ids.sort(key=lambda i: (0 if centers[i,1] < 560 else 1, centers[i,0]))
    seeds = np.where(np.isin(labels, ids), labels, 0)
    nearest = ndimage.distance_transform_edt(seeds == 0, return_distances=False, return_indices=True)
    owners = seeds[tuple(nearest)]
    poses = []
    pose_record = []
    pose_dir = HERE / 'poses'
    pose_dir.mkdir(exist_ok=True)
    for k, ident in enumerate(ids):
        x,y,w,h,area = stats[ident]
        solid = labels == ident
        yy,xx = np.nonzero(solid & (np.indices(solid.shape)[1] < x+w*.35))
        rear_y = int(yy.max())
        rear_x = int(round(xx[yy >= rear_y-8].mean()))
        tx, ty = 180-rear_x, 700-rear_y
        pixels = a.copy()
        pixels[...,3] = np.where(owners == ident, pixels[...,3], 0)
        im = Image.fromarray(pixels)
        placed = Image.new('RGBA', SOURCE_SIZE)
        placed.alpha_composite(im, (tx,ty))
        placed = placed.crop(CROP)
        placed.save(pose_dir / f'pose{k:02}.png')
        poses.append(placed)
        pose_record.append({'pose': k, 'component': ident, 'source_bbox': [int(x),int(y),int(w),int(h)],
                            'rear_foot': [rear_x,rear_y], 'translation': [tx,ty]})
    # 上段の保持→一直線の一刀→振り切りの余韻。斬撃を逆再生して戻さない。
    mapping = [0]*13 + [1,2,3,4,5] + [5]*10
    frames = [poses[k] for k in mapping]
    out = HERE / 'frames'
    out.mkdir(exist_ok=True)
    for f in out.glob('f*.png'):
        if f.stem[1:].isdigit() and int(f.stem[1:]) >= len(frames):
            f.unlink()
    record = {'method': '原画6姿勢・接地位置合わせ・区間別の保持', 'fps': FPS, 'frames': len(frames),
              'unique_poses': 6, 'size': SIZE, 'seconds': len(frames)/FPS, 'contact_frame': 15,
              'rear_anchor': [148,572], 'crop': CROP, 'mapping': mapping, 'poses': pose_record, 'measurements': []}
    for i,im in enumerate(frames):
        im.save(out / f'f{i:03}.png')
        p = np.asarray(im).astype(int)
        s = p[...,3] > 128
        green = s & (p[...,1] > p[...,0]+45) & (p[...,1] > p[...,2]+45)
        record['measurements'].append({'frame':i, 'pose':mapping[i],
            'bbox': im.getchannel('A').point(lambda v:255 if v>128 else 0).getbbox(),
            'border':int(s[0].sum()+s[-1].sum()+s[:,0].sum()+s[:,-1].sum()), 'green':int(green.sum())})
    (HERE / 'clip.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
    shown = [plate(im) for im in frames]
    shown[0].save(HERE / 'gald-jodan-cleave.gif',save_all=True,append_images=shown[1:]+[shown[-1]]*20,
                  duration=40,loop=0,optimize=False)
    frames[0].save(HERE / 'gald-jodan-cleave.webp',save_all=True,append_images=frames[1:],duration=40,
                   loop=0,lossless=True)
    with av.open(str(HERE / 'gald-jodan-cleave.mp4'),'w') as container:
        stream = container.add_stream('libx264',rate=FPS)
        stream.width,stream.height = SIZE
        stream.pix_fmt='yuv420p'
        stream.options={'crf':'18','preset':'medium'}
        for im in shown:
            for packet in stream.encode(av.VideoFrame.from_image(im)): container.mux(packet)
        for packet in stream.encode(): container.mux(packet)
    sheet = Image.new('RGB',(392*3,304*2),'#182634')
    for i,im in enumerate(poses):
        sheet.paste(plate(im).resize((392,304),Image.Resampling.LANCZOS),((i%3)*392,(i//3)*304))
    sheet.save(HERE / 'gald-jodan-cleave-keyposes.jpg',quality=94)
    print('JODAN_SHEET_ANIMATION_READY',len(frames),len(frames)/FPS)

if __name__ == '__main__':
    main()
