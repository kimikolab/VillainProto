# 始点終点（Fun Inp）用に、構え・低い振り切り・頭上の振り切りを同じキャンバス・同じ倍率・同じ後ろ足に揃える。
# 倍率は「足底から赤い腰布の上端まで」の高さを構えに合わせる。位置は腰（赤い腰布の上端の中央）の x と足底の y を合わせる
# （後ろ足で合わせると胴が 250px ほど左右にずれ、薙ぎが「体ごと滑る」動きになる。薙ぎは腰を軸に回るので腰で合わせる）。
# 絵の画素は変形しない（一様拡縮と平行移動だけ）。
#   D:/comfyui/venv/Scripts/python.exe design/art/borg/attack-sweep-v1/keyframes.py [A|D]
import json, os
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
BORG = os.path.join(HERE, "..")
COMFY = "D:/comfyui"
# 組: 名前 → (絵, 倍率の基準, 出力名の接尾辞)
SETS = {
    # S1 / S2 用: 構え → 低い振り切り → 頭上の振り切り
    "A": ({
        "windup": "borg-sweep-windup-v1.png",                                # ユーザーが描いた攻撃開始の差分
        "low": "attack-keyposes-v2/borg_attack_contact_right.png",           # 刃を右下へ通した振り切り（ユーザー添付 2 枚目）
        "over": "attack-keyposes-v3/borg_attack_followthrough_arms_v5.png",  # 頭上へ振り抜いた振り切り（ユーザー添付 1 枚目）
    }, "windup", ""),
    # F 用: D-2022 の終点（頭の後ろへ巻き上げた姿勢・生成のコマ）→ 低い振り切り。D-2022 のコマの倍率に揃える
    "D": ({
        "dend": "attack-sweep-v1/start_from_D2022.png",
        "low": "attack-keyposes-v2/borg_attack_contact_right.png",
    }, "dend", "_D"),
}
# 絵の中身の外接矩形からの余白（左, 上, 右, 下）。None なら画像の枠ごと置く（A は既に生成に使ったので変えない）
CROP = {"D": (160, 160, 240, 48)}
MARGIN = 96


def measure(im):
    a = np.asarray(im).astype(int)
    al = a[..., 3] > 128
    bottom = int(np.nonzero(al)[0].max())
    red = al & (a[..., 0] > 110) & (a[..., 0] - a[..., 1] > 70) & (a[..., 0] - a[..., 2] > 60)
    lab, n = ndimage.label(ndimage.binary_opening(red, iterations=2))
    big = int(np.argmax(ndimage.sum(red, lab, range(1, n + 1)))) + 1
    cy, cx = np.nonzero(lab == big)
    cloth_top = int(cy.min())
    hip_x = int(cx[cy < cloth_top + 40].mean())  # 腰布の上端の帯の中央＝腰
    return bottom, cloth_top, hip_x


def main(set_name="A"):
    POSES, ref, suffix = SETS[set_name]
    ims = {k: Image.open(os.path.join(BORG, f)).convert("RGBA") for k, f in POSES.items()}
    m = {k: measure(im) for k, im in ims.items()}
    ref_h = m[ref][0] - m[ref][1]
    placed = {}
    for k, im in ims.items():
        bottom, top, hip = m[k]
        s = ref_h / (bottom - top)
        im2 = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
        # 足底を y=0、腰を x=0 とする座標で、画像の左上を置く
        placed[k] = (im2, -hip * s, -bottom * s, s)
    x0 = min(p[1] for p in placed.values()) - MARGIN
    y0 = min(p[2] for p in placed.values()) - MARGIN
    x1 = max(p[1] + p[0].width for p in placed.values()) + MARGIN
    y1 = max(p[2] + p[0].height for p in placed.values()) + MARGIN
    if CROP.get(set_name):
        # 中身の外接矩形（全ポーズの和）に余白を足した範囲だけを使う
        l, t, r, b = CROP[set_name]
        boxes = []
        for im2, x, y, _ in placed.values():
            bx = im2.getchannel("A").point(lambda v: 255 if v > 128 else 0).getbbox()
            boxes.append((x + bx[0], y + bx[1], x + bx[2], y + bx[3]))
        x0 = min(bb[0] for bb in boxes) - l; y0 = min(bb[1] for bb in boxes) - t
        x1 = max(bb[2] for bb in boxes) + r; y1 = max(bb[3] for bb in boxes) + b
    W, H = int(x1 - x0), int(y1 - y0)
    W += (-W) % 16; H += (-H) % 16
    manifest = {"canvas": [W, H], "poses": {}}
    for k, (im2, x, y, s) in placed.items():
        c = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        c.alpha_composite(im2, (round(x - x0), round(y - y0)))
        name = f"vp_borg_key{suffix}_{k}.png"
        c.save(f"{COMFY}/input/{name}")
        c.save(os.path.join(HERE, f"key{suffix}_{k}.png"))
        manifest["poses"][k] = {"source": POSES[k], "scale": round(s, 4), "offset": [round(x - x0), round(y - y0)], "input": name}
    with open(os.path.join(HERE, f"keyframes{suffix}.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print(json.dumps(manifest, ensure_ascii=False))
    # 確認: 半透明で重ねる
    over = Image.new("RGBA", (W, H), (235, 235, 235, 255))
    for k in POSES:
        c = Image.open(os.path.join(HERE, f"key{suffix}_{k}.png"))
        c.putalpha(Image.fromarray((np.asarray(c)[..., 3] * 0.45).astype(np.uint8)))
        over.alpha_composite(c)
    over.convert("RGB").resize((W // 2, H // 2)).save(os.path.join(HERE, f"keyframes{suffix}-overlay.jpg"), quality=88)


if __name__ == "__main__":
    import sys
    main(sys.argv[1] if len(sys.argv) > 1 else "A")
