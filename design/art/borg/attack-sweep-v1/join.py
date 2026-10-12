# 2 区間（S1: 構え→低い振り切り、S2: 低い振り切り→頭上）を 1 本の攻撃につなぐ。
# S1 は終わりの数コマが低い振り切りのまま止まるので、終点とほぼ同じになった最初のコマで切り、S2 の頭へ続ける（途中で止まらないように）。
# 絵は変形・補間しない（コマを選んで並べるだけ）。
#   D:/comfyui/venv/Scripts/python.exe design/art/borg/attack-sweep-v1/join.py borg_flfS1_s2022 borg_flfS2_s5055 borg_flf_join_a
import glob, os, shutil, sys
import numpy as np
from PIL import Image

OUT = "D:/comfyui/output/VillainProto/attack_motion"
s1, s2, name = sys.argv[1], sys.argv[2], sys.argv[3]
THRESH = float(sys.argv[4]) if len(sys.argv) > 4 else 2.5

f1 = sorted(glob.glob(f"{OUT}/{s1}_rgba/*.png"))
f2 = sorted(glob.glob(f"{OUT}/{s2}_rgba/*.png"))
last = np.asarray(Image.open(f1[-1]).convert("RGBA"), dtype=float)
diffs = [float(np.abs(np.asarray(Image.open(f).convert("RGBA"), dtype=float) - last).mean()) for f in f1]
cut = next(i for i, d in enumerate(diffs) if d < THRESH)  # 終点とほぼ同じになった最初のコマ
seam = float(np.abs(np.asarray(Image.open(f1[cut]).convert("RGBA"), dtype=float)
                    - np.asarray(Image.open(f2[0]).convert("RGBA"), dtype=float)).mean())
seq = f1[:cut + 1] + f2[1:]
dst = f"{OUT}/{name}_rgba"
shutil.rmtree(dst, ignore_errors=True); os.makedirs(dst)
for i, f in enumerate(seq):
    shutil.copy(f, f"{dst}/f_{i + 1:05d}_.png")
print(f"{name}: S1 0..{cut}（{s1} の終点との差 {diffs[cut]:.2f}）+ S2 1..{len(f2) - 1} = {len(seq)} コマ  継ぎ目の差 {seam:.2f}")
print("S1 の終点との差:", " ".join(f"{d:.1f}" for d in diffs))
