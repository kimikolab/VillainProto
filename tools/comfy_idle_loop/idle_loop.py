# 戦闘待機ループ（格ゲーの待機）を ComfyUI で作り、DemoApp に入れるまでの道具。
# 手順書は design/IDLE_LOOP_COMFYUI_GUIDE.md。ComfyUI の venv の Python で動かす（PIL / numpy / av が要る）:
#
#   D:\comfyui\venv\Scripts\python.exe tools\comfy_idle_loop\idle_loop.py <命令> ...
#
#   status                         ComfyUI が動いているか・待ち行列・自作ノードの有無
#   gen <key> [--seeds 2022,5055]  prompts.json の <key> で 08 の生成を回す（1 本 約 13 分）。出力名は <key>_s<seed>
#   recut <tag> [--crossfade 6] [--max-length 48]
#                                  <tag> の生の動画から区間と透過だけやり直す（1 分ほど）。出力名は <tag>_final
#   check <tag> <key>              確認シート・元絵と並べた GIF・数値（足元・頭の上下・前傾・緑の抜け残り）
#   install <tag> <loop>           <tag> の透過連番を DemoApp/assets/portraits/battle/idle_loop/<loop>/ へ入れる
#   workflows                      ComfyUI の画面用 JSON（08 / 09）を書き直す
#   install-node                   comfy_node/ を ComfyUI の custom_nodes へ写す（ComfyUI の再起動が要る）
#   prompt <key>                   組み立てた正・負のプロンプトを表示する（生成しない）
import argparse, copy, glob, json, os, shutil, sys, time, urllib.error, urllib.request, uuid

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
COMFY = os.environ.get("VP_COMFY_DIR", "D:/comfyui")
HOST = os.environ.get("VP_COMFY_HOST", "http://127.0.0.1:8188")
OUT = f"{COMFY}/output/VillainProto/idle_loop"
IDLE_LOOP_DIR = os.path.join(REPO, "DemoApp", "assets", "portraits", "battle", "idle_loop")

HEAD = "A 2D anime fighting game character idle animation, like a Street Fighter or Guilty Gear idle loop. "
RULES = ("Both feet stay flat on the ground in exactly the same spots; {p} never jumps and never steps. "
         "{P} body keeps facing right and does not turn toward the camera. {sway} "
         "Same face, same costume, same colors. Fixed camera, no zoom, full body always visible, flat plain green background.")
NEG = ("static image, frozen pose, walking, moving across the frame, turning around, camera movement, zoom, "
       "cuts, jumping, hopping, feet leaving the ground, stepping, turning to face the camera, cropped feet, cropped ears, "
       "multiple characters, changing costume, changing face, extra limbs, extra fingers, deformed hands, flicker, text, "
       "watermark, background change, shadows on background")

W, H, LENGTH = 640, 960, 81


def load_prompts():
    with open(os.path.join(HERE, "prompts.json"), encoding="utf-8") as f:
        return json.load(f)


def compose(e):
    """④b の項目から正・負のプロンプトを組む。full があればそのまま使う（ハネの最初の試作）。"""
    if "full" in e:
        pos = e["full"]
    else:
        p = e.get("pronoun", "she")
        P = {"she": "Her", "he": "His"}.get(p, "Their")
        pos = HEAD + e["char"] + e["motion"] + RULES.format(p=p, P=P, sway=e["sway"])
    neg = NEG + (", " + e["neg"] if e.get("neg") else "")
    return pos, neg


# ---- ワークフロー（API 形式の節点）。(id, class, inputs, 画面の位置[列, 行], タイトル) ----

def nodes08(image, pos, neg, seed, prefix, width=1024, height=1536):
    return [
        (1, "UNETLoader", {"unet_name": "wan2.2_i2v_high_noise_14B_fp8_scaled.safetensors", "weight_dtype": "default"}, (0, 0), None),
        (2, "UNETLoader", {"unet_name": "wan2.2_i2v_low_noise_14B_fp8_scaled.safetensors", "weight_dtype": "default"}, (0, 1), None),
        (3, "CLIPLoader", {"clip_name": "umt5_xxl_fp8_e4m3fn_scaled.safetensors", "type": "wan", "device": "default"}, (0, 2), None),
        (4, "VAELoader", {"vae_name": "wan_2.1_vae.safetensors"}, (0, 3), None),
        (5, "LoadImage", {"image": image}, (0, 4), "① 透過PNGの立ち絵（右向き）"),
        (30, "EmptyImage", {"width": width, "height": height, "batch_size": 1, "color": 0x00B140}, (1, 3), "② 緑の板（元画像と同じ大きさにする）"),
        (31, "InvertMask", {"mask": [5, 1]}, (1, 4), None),
        (32, "ImageCompositeMasked", {"destination": [30, 0], "source": [5, 0], "x": 0, "y": 0, "resize_source": False, "mask": [31, 0]}, (1, 5), "③ 立ち絵を緑の板に乗せる"),
        (19, "ImageScale", {"image": [32, 0], "upscale_method": "lanczos", "width": W, "height": H, "crop": "disabled"}, (2, 5), None),
        (6, "CLIPTextEncode", {"clip": [3, 0], "text": pos}, (1, 0), "④ 動きの指示"),
        (7, "CLIPTextEncode", {"clip": [3, 0], "text": neg}, (1, 1), "ネガティブ"),
        (8, "ModelSamplingSD3", {"model": [1, 0], "shift": 8.0}, (2, 0), None),
        (9, "ModelSamplingSD3", {"model": [2, 0], "shift": 8.0}, (2, 1), None),
        (10, "WanImageToVideo", {"positive": [6, 0], "negative": [7, 0], "vae": [4, 0], "width": W, "height": H, "length": LENGTH,
                                 "batch_size": 1, "start_image": [19, 0]}, (2, 2), "⑤ 81フレーム = 約5秒（周期が3回ほど入る）"),
        (11, "KSamplerAdvanced", {"model": [8, 0], "add_noise": "enable", "noise_seed": seed, "steps": 20, "cfg": 3.5, "sampler_name": "euler",
                                  "scheduler": "simple", "positive": [10, 0], "negative": [10, 1], "latent_image": [10, 2],
                                  "start_at_step": 0, "end_at_step": 10, "return_with_leftover_noise": "enable"}, (3, 0), "high 0–10"),
        (12, "KSamplerAdvanced", {"model": [9, 0], "add_noise": "disable", "noise_seed": 0, "steps": 20, "cfg": 3.5, "sampler_name": "euler",
                                  "scheduler": "simple", "positive": [10, 0], "negative": [10, 1], "latent_image": [11, 0],
                                  "start_at_step": 10, "end_at_step": 20, "return_with_leftover_noise": "disable"}, (3, 1), "low 10–20"),
        (13, "VAEDecode", {"samples": [12, 0], "vae": [4, 0]}, (3, 2), None),
        (14, "CreateVideo", {"images": [13, 0], "fps": 16.0}, (4, 0), None),
        (15, "SaveVideo", {"video": [14, 0], "filename_prefix": f"VillainProto/idle_loop/{prefix}_raw", "format": "auto", "codec": "auto"}, (4, 1), "生の動画（緑背景・全フレーム）"),
        *post_nodes([13, 0], prefix, crossfade=0, max_length=48),
    ]


def post_nodes(images, prefix, crossfade, max_length, min_length=16):
    return [
        (40, "VPIdleLoopFind", {"images": images, "min_length": min_length, "max_length": max_length, "skip_head": 8, "min_motion": 3.0,
                                "compare_size": 192, "min_step": 2.5, "crossfade": crossfade}, (4, 2), "⑥ 継ぎ目のいちばん目立たない区間を自動で切る"),
        (45, "PreviewAny", {"source": [40, 4]}, (5, 2), "区間と継ぎ目の報告（1.0x 以下なら滑らか）"),
        (41, "BiRefNetRMBG", {"image": [40, 0], "model": "BiRefNet_toonout", "sensitivity": 1.0, "mask_blur": 0, "mask_offset": 0,
                              "invert_output": False, "refine_foreground": False, "background": "Alpha", "background_color": "#222222"},
         (5, 0), "⑦ 前景マスク（アニメ絵向けの toonout）"),
        (42, "VPGreenKeyAlpha", {"images": [40, 0], "mask": [41, 1], "key_low": 25.0, "key_high": 75.0, "despill": True, "edge_width": 12},
         (6, 0), "⑧ 指の間の緑を抜き、緑かぶりと縁の黄緑を消す"),
        (43, "SaveImage", {"images": [42, 0], "filename_prefix": f"VillainProto/idle_loop/{prefix}_rgba/f"}, (7, 0), "透過PNGの連番（ゲームへ持っていく）"),
        (44, "SaveAnimatedWEBP", {"images": [42, 0], "filename_prefix": f"VillainProto/idle_loop/{prefix}_loop", "fps": 16.0, "lossless": False,
                                  "quality": 90, "method": "default"}, (7, 1), "確認用のループWebP（透過）"),
    ]


def nodes09(video, prefix, crossfade, max_length, min_length=16):
    return [
        (1, "LoadVideo", {"file": video}, (0, 0), "① 08 が保存した生の動画（*_raw_*.mp4 を input へ置く）"),
        (2, "GetVideoComponents", {"video": [1, 0]}, (0, 1), None),
        *post_nodes([2, 0], prefix, crossfade=crossfade, max_length=max_length, min_length=min_length),
    ]


def api_graph(nodes):
    fix = lambda v: [str(v[0]), v[1]] if isinstance(v, list) and len(v) == 2 and isinstance(v[0], int) else v
    return {str(i): {"class_type": c, "inputs": {k: fix(v) for k, v in inp.items()}} for i, c, inp, _, _ in nodes}


# ---- ComfyUI の API ----

def http_json(path, body=None):
    req = urllib.request.Request(HOST + path, data=None if body is None else json.dumps(body).encode(),
                                 headers={"Content-Type": "application/json"})
    try:
        return json.load(urllib.request.urlopen(req, timeout=30))
    except urllib.error.HTTPError as e:
        sys.exit(f"ComfyUI がエラーを返した: {e.read().decode()[:2000]}")


def queue_counts():
    q = http_json("/queue")
    return len(q["queue_running"]), len(q["queue_pending"])


def submit(nodes):
    return http_json("/prompt", {"prompt": api_graph(nodes), "client_id": str(uuid.uuid4())})["prompt_id"]


def wait(pid, tag):
    t0 = time.time()
    while True:
        h = http_json(f"/history/{pid}")
        if pid in h:
            st = h[pid]
            ok = st["status"].get("status_str")
            report = st.get("outputs", {}).get("45", {}).get("text", [""])[0]
            print(f"{tag}: {ok} {time.time() - t0:.0f}s  {report}", flush=True)
            if ok != "success":
                print(json.dumps(st["status"].get("messages", [])[-2:], ensure_ascii=False)[:2000], flush=True)
            return ok == "success"
        time.sleep(10)


def require_comfy():
    try:
        urllib.request.urlopen(HOST + "/system_stats", timeout=5)
    except Exception:
        sys.exit(f"ComfyUI に繋がらない（{HOST}）。手順書の「ComfyUI を起動する」を見る")
    if b"VPIdleLoopFind" not in urllib.request.urlopen(HOST + "/object_info/VPIdleLoopFind", timeout=10).read():
        sys.exit("自作ノード VPIdleLoopFind が ComfyUI に無い。install-node を実行して ComfyUI を再起動する")


# ---- 命令 ----

def cmd_status(_):
    try:
        urllib.request.urlopen(HOST + "/system_stats", timeout=5)
    except Exception:
        print(f"ComfyUI: 停止中（{HOST}）"); return
    r, p = queue_counts()
    node = b"VPIdleLoopFind" in urllib.request.urlopen(HOST + "/object_info/VPIdleLoopFind", timeout=10).read()
    print(f"ComfyUI: 動作中  実行中 {r} / 待ち {p}  自作ノード: {'あり' if node else 'なし（install-node → 再起動）'}")


def cmd_prompt(a):
    e = load_prompts()[a.key]
    pos, neg = compose(e)
    print("[正]\n" + pos + "\n\n[負]\n" + neg)


def cmd_gen(a):
    require_comfy()
    e = load_prompts()[a.key]
    pos, neg = compose(e)
    from PIL import Image
    still = os.path.join(REPO, e["still"])
    w, h = Image.open(still).size
    image = f"vp_{a.key}.png"
    shutil.copy(still, f"{COMFY}/input/{image}")
    r, p = queue_counts()
    if r or p:
        print(f"注意: 待ち行列に他の仕事がある（実行中 {r} / 待ち {p}）。その後ろに並ぶ")
    jobs = []
    for seed in [int(s) for s in a.seeds.split(",")]:
        tag = f"{a.key}_s{seed}"
        if glob.glob(f"{OUT}/{tag}_rgba/*.png"):
            sys.exit(f"{tag} の出力が既にある（{OUT}/{tag}_rgba）。消すか別の seed にする")
        jobs.append((tag, submit(nodes08(image, pos, neg, seed, tag, w, h))))
        print(f"投入: {tag}", flush=True)
    for tag, pid in jobs:
        wait(pid, tag)


def cmd_recut(a):
    require_comfy()
    raws = sorted(glob.glob(f"{OUT}/{a.tag}_raw_*.mp4"))
    if not raws:
        sys.exit(f"{a.tag} の生の動画が無い（{OUT}/{a.tag}_raw_*.mp4）")
    video = f"vp_{a.tag}_raw.mp4"
    shutil.copy(raws[-1], f"{COMFY}/input/{video}")
    prefix = f"{a.tag}_final"
    shutil.rmtree(f"{OUT}/{prefix}_rgba", ignore_errors=True)  # 前の切り直しの連番と混ざらないように
    wait(submit(nodes09(video, prefix, a.crossfade, a.max_length, a.min_length)), prefix)


def frames_of(tag):
    files = sorted(glob.glob(f"{OUT}/{tag}_rgba/*.png"))
    if not files:
        sys.exit(f"{tag} の透過連番が無い（{OUT}/{tag}_rgba）")
    return files


def cmd_check(a):
    import numpy as np
    from PIL import Image
    e = load_prompts()[a.key]
    fr = [Image.open(f).convert("RGBA") for f in frames_of(a.tag)]
    Wf, Hf = fr[0].size
    arr = [np.asarray(im) for im in fr]

    def lean_and_top(al):
        ys, xs = np.nonzero(al)
        top = ys.min()
        _, hx = np.nonzero(al[top:top + int(Hf * 0.08)])
        _, wx = np.nonzero(al[int(Hf * 0.45):int(Hf * 0.60)])
        return np.percentile(hx, 75) - wx.mean(), top, ys.max()

    rows = [lean_and_top(x[..., 3] > 128) for x in arr]
    lean = np.array([r[0] for r in rows]); top = np.array([r[1] for r in rows]); bottom = np.array([r[2] for r in rows])
    steps = [float(np.abs(arr[i + 1][..., :3].astype(float) - arr[i][..., :3]).mean()) for i in range(len(arr) - 1)]
    wrap = float(np.abs(arr[0][..., :3].astype(float) - arr[-1][..., :3]).mean())
    green = max(int(((x[..., 3] > 200) & (x[..., 1].astype(int) - np.maximum(x[..., 0], x[..., 2]) > 20)).sum()) for x in arr)

    still = Image.open(os.path.join(REPO, e["still"])).convert("RGBA").resize((Wf, Hf), Image.LANCZOS)
    s = np.asarray(still).astype(int)
    if s[..., 3].min() == 255:  # 透過なしの緑背景の元絵（ホタ）
        s[..., 3] = np.where(s[..., 1] - np.maximum(s[..., 0], s[..., 2]) > 60, 0, 255)
        still = Image.fromarray(s.astype(np.uint8), "RGBA")
    s_lean, _, s_bottom = lean_and_top(np.asarray(still)[..., 3] > 128)

    print(f"{a.tag}: {len(fr)} コマ（{len(fr) / 16:.2f} 秒）")
    print(f"  靴底の行     元絵 {s_bottom} / 連番 {bottom.min()}〜{bottom.max()}   （1px 以内なら足元が合う）")
    print(f"  頭頂の上下   {np.ptp(top)}px                  （参考: ヒヨ 34・ハネ 23・ボルグ 17・ホタ燃焼 1。小さくても手足や髪が自然に揺れていれば採れる。動画で決める）")
    print(f"  頭と腰の横ずれ 平均 {lean.mean():.1f}（元絵 {s_lean:.1f}）幅 {np.ptp(lean):.1f}px  （幅 25px 超は前かがみ・猫背を疑う）")
    print(f"  継ぎ目       {wrap:.2f} / 1 コマの差の中央値 {np.median(steps):.2f} = {wrap / max(np.median(steps), 1e-6):.2f}x  （1.5x 超は recut --crossfade 6）")
    print(f"  緑の抜け残り {green}px（0 が正常）")

    picks = [0, len(fr) // 4, len(fr) // 2, 3 * len(fr) // 4]
    chk = Image.new("RGBA", (Wf, Hf))
    for y in range(0, Hf, 16):
        for x in range(0, Wf, 16):
            chk.paste((200, 200, 200, 255) if (x // 16 + y // 16) % 2 else (150, 150, 150, 255), (x, y, x + 16, y + 16))
    sheet = Image.new("RGBA", (Wf * 4, Hf * 2))
    for k, i in enumerate(picks):
        d = Image.new("RGBA", (Wf, Hf), (23, 34, 31, 255)); d.alpha_composite(fr[i]); sheet.paste(d, (k * Wf, 0))
        c = chk.copy(); c.alpha_composite(fr[i]); sheet.paste(c, (k * Wf, Hf))
    sheet.convert("RGB").resize((Wf * 2, Hf)).save(f"{OUT}/{a.tag}_check.jpg", quality=88)
    gif = []
    for im in fr:
        c = Image.new("RGBA", (Wf * 2, Hf), (23, 34, 31, 255)); c.alpha_composite(still, (0, 0)); c.alpha_composite(im, (Wf, 0))
        gif.append(c.convert("RGB").resize((Wf, Hf // 2)))
    # コマごとに別の色表・ディザを使うと、輪郭が実際の動きと無関係にちらつく。
    swatches = Image.new("RGB", (1280, 960))
    for k in range(16):
        swatches.paste(gif[min(len(gif) - 1, k * len(gif) // 16)].resize((320, 240)), ((k % 4) * 320, (k // 4) * 240))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    gif = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in gif]
    gif[0].save(f"{OUT}/{a.tag}_compare.gif", save_all=True, append_images=gif[1:], duration=62, loop=0, optimize=False)
    print(f"  確認シート {OUT}/{a.tag}_check.jpg\n  比較 GIF   {OUT}/{a.tag}_compare.gif（左が元絵・右が連番。これをユーザーに見せて選んでもらう）")


def cmd_install(a):
    import numpy as np
    from PIL import Image
    files = frames_of(a.tag)
    dst = os.path.join(IDLE_LOOP_DIR, a.loop)
    os.makedirs(dst, exist_ok=True)
    for f in glob.glob(os.path.join(dst, "*")):  # 古い連番（と .import）を残すとコマが混ざる
        os.remove(f)
    total = 0
    for i, f in enumerate(files):
        p = os.path.join(dst, f"{a.loop}_idle_{i:02d}.png")
        Image.open(f).convert("RGBA").save(p, optimize=True)
        total += os.path.getsize(p)
    bottoms = [int(np.nonzero(np.asarray(Image.open(f))[..., 3] > 128)[0].max()) for f in files]
    print(f"{a.tag} → {os.path.relpath(dst, REPO)}  {len(files)} コマ  {total / 1e6:.1f} MB  靴底 {min(bottoms)}〜{max(bottoms)}")
    print("次: dotnet build DemoApp → IdleLoopCheck / PortraitStateCheck を回す（手順書の「ゲームに入れる」）")


UI_RULES = ("Both feet stay flat on the ground in exactly the same spots; the character never jumps and never steps. "
            "The body keeps facing right and does not turn toward the camera. Same face, same costume, same colors. "
            "Fixed camera, no zoom, full body always visible, flat plain green background.")


def ui_nodes08(key, seed, prefix):
    """画面用の 08。④ を「④a 共通の頭 / ④b キャラごと / ④c 共通の縛り」の 3 箱にして、替えるのは ④b だけにする。"""
    e = load_prompts()[key]
    _, neg = compose(e)
    shutil.copy(os.path.join(REPO, e["still"]), f"{COMFY}/input/vp_{key}.png")
    nodes = [n for n in nodes08(f"vp_{key}.png", "", neg, seed, prefix) if n[0] != 6]
    char = e["char"] + e["motion"] + e["sway"]
    return nodes + [
        (50, "PrimitiveStringMultiline", {"value": HEAD.strip()}, (-1, 0), "④a 共通: 格ゲーの待機"),
        (51, "PrimitiveStringMultiline", {"value": char}, (-1, 1), "④b キャラごと: 姿・構え・弾み方・揺れるもの（ここだけ替える）"),
        (52, "PrimitiveStringMultiline", {"value": UI_RULES}, (-1, 2), "④c 共通: 足は固定・跳ばない・向きを変えない"),
        (53, "StringConcatenate", {"string_a": [50, 0], "string_b": [51, 0], "delimiter": " "}, (-1, 3), None),
        (54, "StringConcatenate", {"string_a": [53, 0], "string_b": [52, 0], "delimiter": " "}, (-1, 4), None),
        (6, "CLIPTextEncode", {"clip": [3, 0], "text": [54, 0]}, (1, 0), "④ 動きの指示"),
    ]


def cmd_workflows(_):
    info = http_json("/object_info")
    ui = load_ui_builder()
    for name, nodes, note in [
        ("08_戦闘待機ループ_Wan22_I2V", ui_nodes08("hiyo", 2022, "wf08"), NOTE08),
        ("09_待機ループの切り直し", nodes09("vp_recut.mp4", "recut", 6, 48), NOTE09),
    ]:
        path = f"{COMFY}/user/default/workflows/VillainProto/{name}.json"
        with open(path, "w", encoding="utf-8") as f:
            json.dump(ui(info, nodes, note), f, ensure_ascii=False, indent=2)
        print("wrote", path)


NOTE08 = ("戦闘待機ループ: Wan2.2 I2V 14B・Lightningなし 20 steps / CFG 3.5・640×960・81 コマ。手順書は VillainProto の design/IDLE_LOOP_COMFYUI_GUIDE.md。\n"
          "・始点=終点（06/07）は「動かないのが正解」と解かれて止まる。始点だけ縛って回し、周期の区間を ⑥ で切る。\n"
          "・①の画像、②の大きさ、④の文を替える。④は tools/comfy_idle_loop/prompts.json の書き方を写す（『膝がはっきり曲がって伸びる・弾む』を必ず入れる）。\n"
          "・seed の当たり外れが大きい。2〜3 本回す。報告に『動きが小さすぎる』『周期が見つからない』が出たら seed を替える。継ぎ目 1.5x 超は 09 で crossfade 6。")
NOTE09 = ("08 の生の動画から、ループ区間と透過だけをやり直す（1 分ほど）。\n"
          "・D:\\comfyui\\output\\VillainProto\\idle_loop\\<名前>_raw_00001_.mp4 を D:\\comfyui\\input へコピーして①で選ぶ。\n"
          "・継ぎ目が目立つ（報告 1.5x 超）とき crossfade を 4〜8。髪がわずかに二重になる代わりに継ぎ目が消える。")


def load_ui_builder():
    """API 形式の節点を ComfyUI の画面用 JSON に変える（object_info から widget の並びを引く）。"""
    def to_ui(info, nodes, note):
        out, links, pending, link_id = [], [], [], 0
        for nid, cls, inp, (col, row), title in nodes:
            spec = info[cls]
            order = spec["input_order"].get("required", []) + spec["input_order"].get("optional", [])
            allspec = {**spec["input"].get("required", {}), **spec["input"].get("optional", {})}
            ins, wv = [], []
            for name in order:
                t, opts = allspec[name][0], (allspec[name][1] if len(allspec[name]) > 1 else {})
                is_widget = isinstance(t, list) or t in ("INT", "FLOAT", "STRING", "BOOLEAN", "COLORCODE", "COMBO")
                v = inp.get(name)
                if isinstance(v, list) and len(v) == 2 and isinstance(v[0], int) and not isinstance(t, list):
                    link_id += 1
                    entry = {"name": name, "type": t, "link": link_id}
                    if is_widget:
                        entry["widget"] = {"name": name}
                        wv.append(opts.get("default"))
                    ins.append(entry)
                    pending.append([link_id, v[0], v[1], nid, len(ins) - 1, t])
                    continue
                if not is_widget:
                    ins.append({"name": name, "type": t, "link": None})
                    continue
                wv.append(v if v is not None else opts.get("default", t[0] if isinstance(t, list) else None))
                if opts.get("control_after_generate") or name in ("seed", "noise_seed"):
                    wv.append("fixed")
                if opts.get("image_upload") or opts.get("video_upload"):
                    wv.append("image")
            outs = [{"name": n, "type": ty, "links": [], "slot_index": k} for k, (ty, n) in enumerate(zip(spec["output"], spec["output_name"]))]
            node = {"id": nid, "type": cls, "pos": [col * 470, row * (330 if col >= 0 else 260)], "size": [440, 300], "flags": {}, "order": 0, "mode": 0,
                    "inputs": ins, "outputs": outs, "properties": {"Node name for S&R": cls}, "widgets_values": wv}
            if title:
                node["title"] = title
            out.append(node)
        byid = {n["id"]: n for n in out}
        for lid, src, slot, dst, dslot, ty in pending:
            byid[src]["outputs"][slot]["links"].append(lid)
            links.append([lid, src, slot, dst, dslot, ty])
        out.append({"id": 99, "type": "Note", "pos": [0, -400], "size": [900, 330], "flags": {}, "order": 0, "mode": 0,
                    "inputs": [], "outputs": [], "properties": {}, "widgets_values": [note]})
        for k, n in enumerate(out):
            n["order"] = k
        return {"id": str(uuid.uuid4()), "revision": 0, "last_node_id": 99, "last_link_id": link_id, "nodes": out,
                "links": links, "groups": [], "config": {}, "extra": {"ds": {"scale": 0.5, "offset": [60, 450]}}, "version": 0.4}
    return to_ui


def cmd_install_node(_):
    dst = f"{COMFY}/custom_nodes/VillainProto_IdleLoop"
    os.makedirs(dst, exist_ok=True)
    shutil.copy(os.path.join(HERE, "comfy_node", "__init__.py"), f"{dst}/__init__.py")
    print(f"写した: {dst}/__init__.py  → ComfyUI を再起動する（待ち行列が空のときだけ。手順書の「再起動」）")


def main():
    ap = argparse.ArgumentParser(description="戦闘待機ループを ComfyUI で作って DemoApp に入れる")
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("status").set_defaults(f=cmd_status)
    p = sub.add_parser("prompt"); p.add_argument("key"); p.set_defaults(f=cmd_prompt)
    p = sub.add_parser("gen"); p.add_argument("key"); p.add_argument("--seeds", default="2022,5055"); p.set_defaults(f=cmd_gen)
    p = sub.add_parser("recut"); p.add_argument("tag"); p.add_argument("--crossfade", type=int, default=6)
    p.add_argument("--max-length", type=int, default=48); p.add_argument("--min-length", type=int, default=16); p.set_defaults(f=cmd_recut)
    p = sub.add_parser("check"); p.add_argument("tag"); p.add_argument("key"); p.set_defaults(f=cmd_check)
    p = sub.add_parser("install"); p.add_argument("tag"); p.add_argument("loop"); p.set_defaults(f=cmd_install)
    sub.add_parser("workflows").set_defaults(f=cmd_workflows)
    sub.add_parser("install-node").set_defaults(f=cmd_install_node)
    a = ap.parse_args()
    a.f(a)


if __name__ == "__main__":
    main()
