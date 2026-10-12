# 薙ぎの候補を確かめる: 全コマの帯（4 コマおき）・等速と 2 倍速の GIF・枠に触れるコマ（剣先の見切れ）・足元の範囲。
#   D:/comfyui/venv/Scripts/python.exe design/art/borg/attack-sweep-v1/inspect_sweep.py borg_sweepA_s2022 [...]
import glob, os, sys
import numpy as np
from PIL import Image, ImageDraw

OUT = "D:/comfyui/output/VillainProto/attack_motion"
BG = (23, 34, 31, 255)

for tag in sys.argv[1:]:
    files = sorted(glob.glob(f"{OUT}/{tag}_rgba/*.png"))
    fr = [Image.open(f).convert("RGBA") for f in files]
    W, H = fr[0].size
    touch, bottoms = [], []
    for i, im in enumerate(fr):
        a = np.asarray(im)[..., 3] > 128
        if a[:2].any() or a[-2:].any() or a[:, :2].any() or a[:, -2:].any():
            touch.append(i)
        bottoms.append(int(np.nonzero(a)[0].max()))
    print(f"{tag}: {len(fr)} コマ  枠に触れる {len(touch)} コマ {touch[:12]}{'…' if len(touch) > 12 else ''}  足元 {min(bottoms)}〜{max(bottoms)}")

    def on_bg(im, scale=0.5):
        c = Image.new("RGBA", im.size, BG); c.alpha_composite(im)
        return c.convert("RGB").resize((int(W * scale), int(H * scale)))

    picks = list(range(0, len(fr), 4))
    tw, th = int(W * 0.35), int(H * 0.35)
    cols = 4
    sheet = Image.new("RGB", (tw * cols, th * ((len(picks) + cols - 1) // cols)), BG[:3])
    for k, i in enumerate(picks):
        t = on_bg(fr[i], 0.35); ImageDraw.Draw(t).text((6, 4), str(i), fill="white")
        sheet.paste(t, ((k % cols) * tw, (k // cols) * th))
    sheet.save(f"{OUT}/{tag}_sheet.jpg", quality=88)
    seq = [on_bg(im) for im in fr]
    hold = [seq[-1]] * 8 + [seq[0]] * 8  # 単発: 最後で少し止めてから頭へ戻る（ループとしての採用ではない）
    seq[0].save(f"{OUT}/{tag}_x1.gif", save_all=True, append_images=seq[1:] + hold, duration=62, loop=0)
    fast = seq[::2]
    fast[0].save(f"{OUT}/{tag}_x2.gif", save_all=True, append_images=fast[1:] + hold[::2], duration=62, loop=0)
    print(f"  {OUT}/{tag}_sheet.jpg / _x1.gif / _x2.gif")
