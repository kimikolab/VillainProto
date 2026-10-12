# 始点終点（Wan2.2 Fun Inp 14B）で、揃えたキーフレームの間をつなぐ。keyframes.py を先に回す。
#   S1: 構え → 低い振り切り（刃が体の前を通る）   S2: 低い振り切り → 頭上の振り切り（S1 の続き）
#   D/comfyui/venv/Scripts/python.exe design/art/borg/attack-sweep-v1/flf.py S1,S2 2022,5055
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(REPO, "tools", "comfy_idle_loop"))
import idle_loop as il  # noqa: E402

KEYSETS = {"": json.load(open(os.path.join(HERE, "keyframes.json"), encoding="utf-8"))}  # 1920×1472（S1 / S2）
if os.path.exists(os.path.join(HERE, "keyframes_D.json")):
    KEYSETS["_D"] = json.load(open(os.path.join(HERE, "keyframes_D.json"), encoding="utf-8"))  # 1120×720（F）
GEN = {"": (1040, 800), "_D": (1120, 720)}   # 生成の大きさ（キャンバスと同じ縦横比）
LENGTH = 25                                  # 約 1.5 秒（4n+1）

HEAD = "A 2D anime fighting game character attack animation, like a Street Fighter or Guilty Gear special move. "
CHAR = ("The long-black-haired armored swordsman in dark spiked plate armor swings his huge black greatsword with both hands. "
        "The greatsword is one rigid, perfectly straight, flat steel slab with red glowing cracks; it never bends, never "
        "curves, never stretches and never disappears, and both hands stay on the hilt the whole time. He cuts with the sharp "
        "edge of the blade, never hitting with the flat side. ")
SEG = {
    "S1": ("windup", "low",
           "From the wind-up in the first frame, his front knee drives forward, his hips turn first, then his shoulders and "
           "arms, and the blade sweeps fast from behind him on the upper left, down and across in front of his body, ending in "
           "the low follow-through of the final frame with the blade pointing down to the right. "),
    "S2": ("low", "over",
           "Continuing the same swing without stopping, the blade keeps travelling from low on the right, rising up past his "
           "front and over his far shoulder while his torso keeps turning until his back shows, ending with both hands high and "
           "the blade raised behind him as in the final frame. "),
    # ユーザー評価: D-2022 の軌道は良いが脇が空いて上寄り。D-2022 の終点（頭の後ろへ巻き上げ）を起点に、低い振り切り（添付 2 枚目）で終わらせる
    "F": ("dend", "low",
          "From the wound-up pose in the first frame, with the blade held behind his head and his back half turned, he unwinds "
          "his whole body: his hips turn first, then his shoulders, his elbows stay tucked in close to his body, and the blade "
          "comes around from behind him and sweeps fast across in front of his body from his upper left down to his lower "
          "right, ending in the low follow-through of the final frame with the blade pointing down to the right. ", "_D"),
}
RULES = ("His long black hair and the red loincloth whip around and follow through with the swing. He stays in place on the "
         "same spot. Same face, same armor, same colors. Fixed camera, no zoom, the whole body and the whole sword always "
         "visible, flat plain green background. No fire, no flames, no glow, no slash effect, no motion trails.")
NEG = (il.NEG.replace("jumping, hopping, feet leaving the ground, stepping, ", "jumping, hopping, ")
       + ", elbows flaring out, arms opening wide, thrust, flat of the blade, slapping with the side of the blade"
       + ", bent sword, curved blade, wavy blade, melting sword, sword disappearing, extra sword, second blade, sword passing "
         "through the body, one-handed, letting go of the sword, cropped sword, turning to face the camera, crossfade, "
         "dissolve, morphing, ghosting, double exposure")


def nodes(seg, seed):
    a, b, motion, *rest = SEG[seg]
    ks = rest[0] if rest else ""
    KEYS = KEYSETS[ks]
    CW, CH = KEYS["canvas"]
    GEN_W, GEN_H = GEN[ks]
    pos = HEAD + CHAR + motion + RULES
    tag = f"borg_flf{seg}_s{seed}"
    pre = lambda nid, key: [  # 透過の絵を緑の板に乗せて縮める
        (nid, "LoadImage", {"image": KEYS["poses"][key]["input"]}, (0, 4), None),
        (nid + 1, "EmptyImage", {"width": CW, "height": CH, "batch_size": 1, "color": 0x00B140}, (1, 3), None),
        (nid + 2, "InvertMask", {"mask": [nid, 1]}, (1, 4), None),
        (nid + 3, "ImageCompositeMasked", {"destination": [nid + 1, 0], "source": [nid, 0], "x": 0, "y": 0, "resize_source": False,
                                           "mask": [nid + 2, 0]}, (1, 5), None),
        (nid + 4, "ImageScale", {"image": [nid + 3, 0], "upscale_method": "lanczos", "width": GEN_W, "height": GEN_H, "crop": "disabled"}, (2, 5), None),
    ]
    g = [
        (1, "UNETLoader", {"unet_name": "wan2.2_fun_inpaint_high_noise_14B_fp8_scaled.safetensors", "weight_dtype": "default"}, (0, 0), None),
        (2, "UNETLoader", {"unet_name": "wan2.2_fun_inpaint_low_noise_14B_fp8_scaled.safetensors", "weight_dtype": "default"}, (0, 1), None),
        (3, "CLIPLoader", {"clip_name": "umt5_xxl_fp8_e4m3fn_scaled.safetensors", "type": "wan", "device": "default"}, (0, 2), None),
        (4, "VAELoader", {"vae_name": "wan_2.1_vae.safetensors"}, (0, 3), None),
        *pre(60, a), *pre(70, b),
        (6, "CLIPTextEncode", {"clip": [3, 0], "text": pos}, (1, 0), None),
        (7, "CLIPTextEncode", {"clip": [3, 0], "text": NEG}, (1, 1), None),
        (8, "ModelSamplingSD3", {"model": [1, 0], "shift": 5.0}, (2, 0), None),
        (9, "ModelSamplingSD3", {"model": [2, 0], "shift": 5.0}, (2, 1), None),
        (10, "WanFunInpaintToVideo", {"positive": [6, 0], "negative": [7, 0], "vae": [4, 0], "width": GEN_W, "height": GEN_H,
                                      "length": LENGTH, "batch_size": 1, "start_image": [64, 0], "end_image": [74, 0]}, (2, 2), None),
        (11, "KSamplerAdvanced", {"model": [8, 0], "add_noise": "enable", "noise_seed": seed, "steps": 20, "cfg": 3.5, "sampler_name": "euler",
                                  "scheduler": "simple", "positive": [10, 0], "negative": [10, 1], "latent_image": [10, 2],
                                  "start_at_step": 0, "end_at_step": 10, "return_with_leftover_noise": "enable"}, (3, 0), None),
        (12, "KSamplerAdvanced", {"model": [9, 0], "add_noise": "disable", "noise_seed": 0, "steps": 20, "cfg": 3.5, "sampler_name": "euler",
                                  "scheduler": "simple", "positive": [10, 0], "negative": [10, 1], "latent_image": [11, 0],
                                  "start_at_step": 10, "end_at_step": 20, "return_with_leftover_noise": "disable"}, (3, 1), None),
        (13, "VAEDecode", {"samples": [12, 0], "vae": [4, 0]}, (3, 2), None),
        (14, "CreateVideo", {"images": [13, 0], "fps": 16.0}, (4, 0), None),
        (15, "SaveVideo", {"video": [14, 0], "filename_prefix": f"VillainProto/attack_motion/{tag}_raw", "format": "auto", "codec": "auto"}, (4, 1), None),
        (41, "BiRefNetRMBG", {"image": [13, 0], "model": "BiRefNet_toonout", "sensitivity": 1.0, "mask_blur": 0, "mask_offset": 0,
                              "invert_output": False, "refine_foreground": False, "background": "Alpha", "background_color": "#222222"}, (5, 0), None),
        (42, "VPGreenKeyAlpha", {"images": [13, 0], "mask": [41, 1], "key_low": 25.0, "key_high": 75.0, "despill": True, "edge_width": 12}, (6, 0), None),
        (43, "SaveImage", {"images": [42, 0], "filename_prefix": f"VillainProto/attack_motion/{tag}_rgba/f"}, (7, 0), None),
    ]
    return tag, pos, g


if __name__ == "__main__":
    il.require_comfy()
    segs, seeds = sys.argv[1].split(","), [int(s) for s in sys.argv[2].split(",")]
    path = os.path.join(HERE, "manifest.json")
    manifest = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}
    jobs = []
    for sg in segs:
        for s in seeds:
            tag, pos, g = nodes(sg, s)
            ks = SEG[sg][3] if len(SEG[sg]) > 3 else ""
            manifest[tag] = {"mode": "Fun Inp 始点終点", "from": SEG[sg][0], "to": SEG[sg][1], "positive": pos, "negative": NEG,
                             "seed": s, "size": list(GEN[ks]), "length": LENGTH, "canvas": KEYSETS[ks]["canvas"]}
            jobs.append((tag, il.submit(g)))
            print("投入:", tag, flush=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    for tag, pid in jobs:
        il.wait(pid, tag)
