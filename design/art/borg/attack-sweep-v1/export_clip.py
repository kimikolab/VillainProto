# 攻撃の連番を DemoApp の attack_clip/<名前>/ へ書き出す（ゲームでの見え方の確認用）。
# clip.json に、通常の待機絵と腰・足底を合わせるための値を書く:
#   anchor      … 連番の 1 コマ目での腰（腰布の上端の中央）の x と足底の y（連番の画素）
#   idleAnchor  … 通常の待機絵（borg_idle_right.png）での同じ点（待機絵の画素）
#   scale       … 連番の 1 画素が待機絵の何画素ぶんか（足底から腰布の上端までの高さの比）
#   fps / contact … 再生の速さと、刃が体の前を通る（敵へ当たる）コマ
#   forward     … コマごとに体を前へ足す量（連番の画素）。生成の途中で前へ踏み込んだ胴が、振り切る前に元の位置へ引き戻されるので
#                 （終点の絵が元の位置にあるため）、いちばん前に出た位置を振り切りまで保つ（胴の x の累積最大 − 胴の x）
#   D:/comfyui/venv/Scripts/python.exe design/art/borg/attack-sweep-v1/export_clip.py borg_flfF_s5055 borg_sweep 5
import glob, json, os, shutil, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, HERE)
from keyframes import measure  # noqa: E402

OUT = "D:/comfyui/output/VillainProto/attack_motion"
tag, clip, contact = sys.argv[1], sys.argv[2], int(sys.argv[3])
fps = float(sys.argv[4]) if len(sys.argv) > 4 else 24.0

files = sorted(glob.glob(f"{OUT}/{tag}_rgba/*.png"))
dst = os.path.join(REPO, "DemoApp", "assets", "portraits", "battle", "attack_clip", clip)
shutil.rmtree(dst, ignore_errors=True)
os.makedirs(dst)
for i, f in enumerate(files):
    Image.open(f).convert("RGBA").save(os.path.join(dst, f"{clip}_{i:02d}.png"), optimize=True)

def torso_x(im):
    """胴（腰布の上端から 200px 上までの帯）の不透明画素の重心 x。腕と刃の先は帯の外に出やすいので胴の前後を拾う。"""
    a = np.asarray(im)[..., 3] > 128
    _, cloth_top, _ = measure(im)
    _, xs = np.nonzero(a[max(0, cloth_top - 200):cloth_top])
    return float(xs.mean())


tx = [torso_x(Image.open(f).convert("RGBA")) for f in files]
peak = np.maximum.accumulate(tx)
forward = [round(float(p - t), 1) for p, t in zip(peak, tx)]
first = Image.open(files[0]).convert("RGBA")
bottom, cloth_top, hip = measure(first)
idle = Image.open(os.path.join(REPO, "DemoApp", "assets", "portraits", "battle", "borg_idle_right.png")).convert("RGBA")
i_bottom, i_cloth, i_hip = measure(idle)
info = {
    "source": tag, "frames": len(files), "size": list(first.size), "fps": fps, "contact": contact,
    "anchor": [hip, bottom], "idleAnchor": [i_hip, i_bottom],
    "scale": round((i_bottom - i_cloth) / (bottom - cloth_top), 4),
    "forward": forward,
}
with open(os.path.join(dst, "clip.json"), "w", encoding="utf-8") as f:
    json.dump(info, f, ensure_ascii=False, indent=2)
print(clip, json.dumps(info, ensure_ascii=False))
