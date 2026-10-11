# VillainProto 用: 動画生成のフレーム列から「待機ループ」を切り出す小さなノード群（依存は torch のみ）。
#
#   VP Idle Loop Find   … 始点終点を縛らない I2V の出力から、継ぎ目のいちばん目立たない区間 [start, end) を探して返す
#   VP Green Key Alpha  … 緑背景で生成したフレームと BiRefNet のマスクを合わせ、緑の抜け残りと緑かぶりを取った RGBA を返す
#
# 原本は VillainProto の tools/comfy_idle_loop/comfy_node/__init__.py。ComfyUI 側（custom_nodes/VillainProto_IdleLoop）へは
# `idle_loop.py install-node` で写す（直接直さない）。消すときは custom_nodes 側のフォルダーごと削除して ComfyUI を再起動する。
# 手順書: design/IDLE_LOOP_COMFYUI_GUIDE.md
import torch
import torch.nn.functional as F


def _small(images, size):
    # [B,H,W,C] → 比較用の縮小 [B,C,h,w]
    x = images[..., :3].permute(0, 3, 1, 2)
    h = size
    w = max(8, round(size * images.shape[2] / images.shape[1]))
    return F.interpolate(x, size=(h, w), mode="area")


class VPIdleLoopFind:
    @classmethod
    def INPUT_TYPES(cls):
        return {"required": {
            "images": ("IMAGE",),
            "min_length": ("INT", {"default": 16, "min": 2, "max": 4096, "tooltip": "ループの最短フレーム数"}),
            "max_length": ("INT", {"default": 48, "min": 2, "max": 4096, "tooltip": "ループの最長フレーム数"}),
            "skip_head": ("INT", {"default": 8, "min": 0, "max": 4096,
                                  "tooltip": "先頭の何フレームを候補から外すか（元絵から動き出す途中は周期に乗っていない）"}),
            "min_motion": ("FLOAT", {"default": 3.0, "min": 0.0, "max": 100.0, "step": 0.1,
                                     "tooltip": "区間内で始点からいちばん離れたフレームの差が、1歩の差の何倍以上あるか（止まっている区間を選ばない）"}),
            "compare_size": ("INT", {"default": 192, "min": 32, "max": 1024}),
            "min_step": ("FLOAT", {"default": 2.5, "min": 0.0, "max": 100.0, "step": 0.1,
                                   "tooltip": "1コマの差（中央値・0..255）がこれ未満なら『動きが小さすぎる』と報告する"}),
            "crossfade": ("INT", {"default": 0, "min": 0, "max": 16,
                                  "tooltip": "ループの最後の何コマを、始点の直前のコマへ少しずつ混ぜるか。継ぎ目が 1.5x を超えるとき 4〜8。髪がわずかに二重になる"}),
        }}

    RETURN_TYPES = ("IMAGE", "INT", "INT", "FLOAT", "STRING")
    RETURN_NAMES = ("loop_images", "start", "end", "seam_ratio", "report")
    FUNCTION = "run"
    CATEGORY = "VillainProto"

    def run(self, images, min_length, max_length, skip_head, min_motion, compare_size, min_step=2.5, crossfade=0):
        n = images.shape[0]
        x = _small(images.float(), compare_size)
        flat = x.reshape(n, -1)
        # 全組の平均絶対差（0..255 の尺度）
        dist = torch.cdist(flat, flat, p=1) / flat.shape[1] * 255.0
        steps = torch.tensor([dist[k, k + 1].item() for k in range(n - 1)])
        step = float(steps.median()) if len(steps) else 1.0
        best = None
        for i in range(min(skip_head, n - 1), n):
            for j in range(i + min_length, min(n, i + max_length + 1)):
                # 区間 [i, j) を回す。継ぎ目は f[j-1] → f[i] なので、f[j] ≈ f[i] なら滑らか
                seam = float(dist[i, j])
                motion = float(dist[i, i:j].max())
                if motion < min_motion * step:
                    continue
                if best is None or seam < best[0]:
                    best = (seam, i, j)
        no_cycle = best is None
        if no_cycle:
            best = (float(dist[0, n - 1]), 0, n - 1)
        seam, i, j = best
        ratio = seam / max(step, 1e-6)
        report = (f"{n} frames / loop [{i}, {j}) = {j - i} frames / seam {seam:.2f} = {ratio:.2f}x of median step {step:.2f}"
                  f" ({'滑らか' if ratio <= 1.0 else '少し目立つ' if ratio <= 1.5 else '目立つ'})")
        if no_cycle:
            report += " / 周期が見つからない（min_motion を満たす区間なし・全フレームを返した）: seed を替える"
        # 1コマの差が小さすぎる＝ほぼ静止（ハネの採用版は 4.5、静止に近い失敗は 0.6〜1.6）
        if step < min_step:
            report += f" / 動きが小さすぎる（1コマの差 {step:.2f} < {min_step:.1f}）: 動きの指示を強めるか seed を替える"
        loop = images[i:j].clone()
        k = min(crossfade, i, j - i - 1)
        if k > 0:
            # 末尾の k コマを「始点の直前の k コマ」へ寄せる: 最後のコマが f[i-1] に近づき、f[i] へ自然に続く
            for t in range(k):
                w = (t + 1) / (k + 1)
                loop[j - i - k + t] = images[j - k + t] * (1.0 - w) + images[i - k + t] * w
            # 混ぜた後の継ぎ目（末尾 → 先頭）を同じ物差しで測り直す
            ends = _small(torch.stack([loop[-1], loop[0]]).float(), compare_size).reshape(2, -1)
            after = float((ends[0] - ends[1]).abs().mean()) * 255.0
            report += f" / 末尾 {k} コマを混ぜた → 継ぎ目 {after:.2f} = {after / max(step, 1e-6):.2f}x"
        print("[VP Idle Loop Find]", report)
        return (loop, i, j, ratio, report)


class VPGreenKeyAlpha:
    @classmethod
    def INPUT_TYPES(cls):
        return {"required": {
            "images": ("IMAGE", {"tooltip": "緑背景のまま生成したフレーム"}),
            "mask": ("MASK", {"tooltip": "BiRefNet などの前景マスク（前景 = 1）"}),
            "key_low": ("FLOAT", {"default": 25.0, "min": 0.0, "max": 255.0, "step": 1.0,
                                  "tooltip": "G - max(R,B) がこれ以下なら不透明のまま"}),
            "key_high": ("FLOAT", {"default": 75.0, "min": 1.0, "max": 255.0, "step": 1.0,
                                   "tooltip": "G - max(R,B) がこれ以上なら完全に透明（指の間の緑など）"}),
            "despill": ("BOOLEAN", {"default": True, "tooltip": "G を max(R,B) で頭打ちにして緑かぶりを消す（緑の衣装のキャラでは切る）"}),
            "edge_width": ("INT", {"default": 12, "min": 0, "max": 64,
                                   "tooltip": "輪郭から何pxの帯で、G を (R+B)/2 まで強く抑えるか（オレンジや金の髪の縁に出る黄緑を消す）。0 で切る"}),
        }}

    RETURN_TYPES = ("IMAGE", "MASK")
    RETURN_NAMES = ("rgba", "alpha")
    FUNCTION = "run"
    CATEGORY = "VillainProto"

    def run(self, images, mask, key_low, key_high, despill, edge_width=12):
        img = images[..., :3].float()
        if mask.dim() == 2:
            mask = mask.unsqueeze(0)
        if mask.shape[1:] != img.shape[1:3]:
            mask = F.interpolate(mask.unsqueeze(1), size=img.shape[1:3], mode="bilinear").squeeze(1)
        mask = mask.to(img)
        if mask.shape[0] != img.shape[0]:
            mask = mask[:1].expand(img.shape[0], -1, -1)
        r, g, b = img[..., 0], img[..., 1], img[..., 2]
        lim = torch.maximum(r, b)
        green = (g - lim) * 255.0
        key = torch.clamp(1.0 - (green - key_low) / max(key_high - key_low, 1e-3), 0.0, 1.0)
        alpha = mask * key
        if despill:
            g = torch.minimum(g, lim)
            if edge_width > 0:
                # 透明に近い画素から edge_width 以内の帯 = 緑が混ざりやすい縁
                k = edge_width * 2 + 1
                near = F.max_pool2d((1.0 - alpha).unsqueeze(1), k, stride=1, padding=edge_width).squeeze(1) > 0.05
                g = torch.where(near, torch.minimum(g, (r + b) * 0.5), g)
        rgb = torch.stack([r, g, b], dim=-1)
        return (torch.cat([rgb, alpha.unsqueeze(-1)], dim=-1), alpha)


NODE_CLASS_MAPPINGS = {"VPIdleLoopFind": VPIdleLoopFind, "VPGreenKeyAlpha": VPGreenKeyAlpha}
NODE_DISPLAY_NAME_MAPPINGS = {"VPIdleLoopFind": "VP Idle Loop Find", "VPGreenKeyAlpha": "VP Green Key Alpha"}
