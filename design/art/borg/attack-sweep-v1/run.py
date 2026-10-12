# ボルグの薙ぎ（攻撃開始の差分 borg-sweep-windup-v1.png から、大剣を大きく振るう単発モーション）。
# 待機ループの道具（tools/comfy_idle_loop/idle_loop.py）の節点を使い回し、ループの区間探しだけ外す（単発なので全コマを残す）。
#   D:/comfyui/venv/Scripts/python.exe design/art/borg/attack-sweep-v1/run.py A,B 2022,5055
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(REPO, "tools", "comfy_idle_loop"))
import idle_loop as il  # noqa: E402

from PIL import Image  # noqa: E402

SRC = os.path.join(HERE, "..", "borg-sweep-windup-v1.png")
# 振り抜く余地（左, 上, 右）と生成の大きさ。49 コマ（約 3 秒）
#   A/B: 右へ 768・上へ 128・左へ 128 → 2432×1152 → 1280×608
#   C/D: 振り抜きが体の向こう側・頭上まで回るので、左へ 256・上へ 512 に広げる → 2560×1536 → 1280×768
#   E: D-2022 の最後のコマ（start_from_D2022.png・1280×768・剣を頭の後ろへ巻き上げた姿勢）をそのまま起点にする。余白は足さない
CANVAS = {"AB": ((128, 128, 768), (1280, 608)), "CD": ((256, 512, 768), (1280, 768)), "E": ((0, 0, 0), (1280, 768))}
SOURCE = {"E": os.path.join(HERE, "start_from_D2022.png")}
LENGTH = 49


def canvas_of(variant):
    return next(v for k, v in CANVAS.items() if variant[0] in k)

CHAR = ("The long-black-haired armored swordsman in dark spiked plate armor starts in the wind-up pose shown: low wide "
        "stance, body twisted, both hands gripping the hilt of his huge black greatsword, the blade cocked far back and up "
        "behind him on the left. ")
BLADE = ("The greatsword is one rigid, perfectly straight, flat steel slab with red glowing cracks; it never bends, never "
         "curves, never stretches and never disappears, and both hands stay on the hilt the whole time. ")
MOTION = {
    "A": ("He immediately unleashes one powerful horizontal sweeping slash toward the right: his front knee drives forward, "
          "his hips turn first, then his shoulders and arms, and the heavy blade swings fast from behind him on the left, "
          "across in front of his body at waist height, to far out on the right side. He follows through with the sword "
          "extended to the right and holds the finished pose, leaning into the swing. "),
    "B": ("He immediately unleashes a huge full-body sweeping slash toward the right: he pushes off with his back foot, his "
          "front knee bends deeply, his hips and torso twist hard so his back turns slightly toward the viewer, and the heavy "
          "blade sweeps fast from behind him on the left, low across in front of his body, to far out on the right side and "
          "up. He follows through with his whole body wound up in the opposite direction and holds the finished pose. "),
    # v1 の A/B は最後が突きの形になり、刃の側面でひっぱたくように見えた（ユーザー評価）。振り抜いた後の姿勢と刃の向きを書く
    "C": ("He immediately unleashes one powerful level slash: his front knee drives forward, his hips turn first, then his "
          "shoulders and arms, and the sharp edge of the blade leads the cut as it slices fast from behind him on the left, "
          "across in front of his body at waist height. He does not stop in front: the blade keeps travelling and wraps around "
          "to the far side of his body, so the follow-through ends with the blade low behind his rear hip pointing back, his "
          "arms crossed in front of his chest and his torso fully turned so part of his back shows. He holds that "
          "follow-through pose. "),
    "D": ("He immediately unleashes one huge sweeping slash: his front knee drives forward, his hips turn first, then his "
          "shoulders and arms, and the sharp edge of the blade leads the cut as it slices fast from behind him on the left, "
          "across in front of his body at waist height. He does not stop in front: the blade keeps travelling up and over his "
          "far shoulder, so the follow-through ends with both hands high near his head and the blade raised behind him "
          "pointing back and up, his torso fully turned so part of his back shows. He holds that follow-through pose. "),
    # ユーザー評価（D-2022）: 剣の軌道は良いが、途中で脇が空いて軌道が上寄りになる。起点は D-2022 の終点（頭の後ろへ巻き上げた姿勢）にしたい
    "E1": ("He unwinds his whole body and unleashes one powerful level sweeping slash toward the right: his hips turn first, "
           "then his shoulders, and his elbows stay tucked in close to his body the whole time. The blade comes around from "
           "behind his head and down to waist height, then slices flat and level across in front of his body toward the right, "
           "staying at waist height and never rising above his chest. The blade keeps travelling past his front and the "
           "follow-through ends low on the far side of his body with the blade pointing back, his chest facing right. He holds "
           "that follow-through pose. "),
    "E2": ("He unwinds his whole body and unleashes one powerful diagonal sweeping slash toward the right: his hips turn "
           "first, then his shoulders, and his elbows stay tucked in close to his body the whole time. The blade comes down "
           "from behind his head in a big diagonal cut across in front of his body, from his upper left down to his lower "
           "right, and the follow-through ends low in front of his front leg with the blade tip pointing down to the right "
           "and both hands near his hip. He holds that follow-through pose. "),
}
START = {"E": ("The long-black-haired armored swordsman in dark spiked plate armor starts in the wound-up pose shown: low wide "
               "stance, torso twisted away so his back is half turned toward the viewer, both hands gripping the hilt above "
               "his head, the huge black greatsword held horizontally behind his head pointing back to the left. ")}
EDGE = ("He cuts with the sharp edge of the blade, never hitting with the flat side. ")
RULES = ("His long black hair and the red loincloth whip around and follow through with the swing. His feet stay planted on "
         "the ground. Same face, same armor, same colors. Fixed camera, no zoom, the whole body and the whole sword always "
         "visible, flat plain green background. No fire, no flames, no glow, no slash effect, no motion trails.")
NEG_EXTRA = ("bent sword, curved blade, wavy blade, melting sword, sword disappearing, extra sword, second blade, sword "
             "passing through the body, one-handed, letting go of the sword, slow motion, standing still, cropped sword, "
             "turning to face the camera")

NEG_ARMS = ("elbows flaring out, arms opening wide, armpits open, blade rising high, blade swinging above the head, "
            "overhead chop")
NEG_FOLLOW = ("thrust, stabbing, lunge with the sword pointing forward, sword pointing straight ahead at the end, flat of the "
              "blade, slapping with the side of the blade, paddle, stopping the swing in front")


def prepare(variant):
    (pl, pt, pr), _ = canvas_of(variant)
    im = Image.open(SOURCE.get(variant[0], SRC)).convert("RGBA")
    w, h = im.width + pl + pr, im.height + pt
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    canvas.alpha_composite(im, (pl, pt))
    name = f"vp_borg_sweep_{variant[0]}_{w}x{h}.png"
    canvas.save(f"{il.COMFY}/input/{name}")
    return name, w, h


def nodes(variant, seed, image, w, h):
    _, (gen_w, gen_h) = canvas_of(variant)
    pos = il.HEAD.replace("idle animation, like a Street Fighter or Guilty Gear idle loop", "attack animation, like a Street Fighter or Guilty Gear special move") \
        + START.get(variant[0], CHAR) + BLADE + MOTION[variant] + (EDGE if variant[0] in "CDE" else "") + RULES
    neg = il.NEG.replace("jumping, hopping, feet leaving the ground, stepping, ", "jumping, hopping, ") + ", " + NEG_EXTRA
    if variant[0] in "CDE":
        neg += ", " + NEG_FOLLOW
    if variant[0] == "E":
        neg += ", " + NEG_ARMS
    tag = f"borg_sweep{variant}_s{seed}"
    base = [n for n in il.nodes08(image, pos, neg, seed, tag, w, h) if n[0] < 40]
    out = []
    for nid, cls, inp, at, title in base:
        inp = dict(inp)
        if cls == "ImageScale":
            inp.update(width=gen_w, height=gen_h)
        if cls == "WanImageToVideo":
            inp.update(width=gen_w, height=gen_h, length=LENGTH)
        if cls == "SaveVideo":
            inp["filename_prefix"] = f"VillainProto/attack_motion/{tag}_raw"
        out.append((nid, cls, inp, at, title))
    # 単発なので区間探しは通さず、全コマを透過する
    out += [
        (41, "BiRefNetRMBG", {"image": [13, 0], "model": "BiRefNet_toonout", "sensitivity": 1.0, "mask_blur": 0, "mask_offset": 0,
                              "invert_output": False, "refine_foreground": False, "background": "Alpha", "background_color": "#222222"}, (5, 0), None),
        (42, "VPGreenKeyAlpha", {"images": [13, 0], "mask": [41, 1], "key_low": 25.0, "key_high": 75.0, "despill": True, "edge_width": 12}, (6, 0), None),
        (43, "SaveImage", {"images": [42, 0], "filename_prefix": f"VillainProto/attack_motion/{tag}_rgba/f"}, (7, 0), None),
    ]
    return tag, pos, neg, out


if __name__ == "__main__":
    il.require_comfy()
    variants, seeds = sys.argv[1].split(","), [int(s) for s in sys.argv[2].split(",")]
    path = os.path.join(HERE, "manifest.json")
    manifest = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}
    jobs = []
    for v in variants:
        image, w, h = prepare(v)
        pad, gen = canvas_of(v)
        for s in seeds:
            tag, pos, neg, ns = nodes(v, s, image, w, h)
            manifest[tag] = {"positive": pos, "negative": neg, "seed": s, "size": list(gen), "length": LENGTH, "canvas": [w, h],
                             "pad": list(pad)}
            jobs.append((tag, il.submit(ns)))
            print("投入:", tag, flush=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    for tag, pid in jobs:
        il.wait(pid, tag)
