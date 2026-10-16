# VillainProto 用: 動画生成のフレーム列から「待機ループ」を切り出す小さなノード群。
# 基本ノードは torch、動きの振幅調整だけ numpy / OpenCV を遅延読込する。
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


class VPScaleIdleMotion:
    """全身の連動を保って動きだけ弱める。固定部の貼り付けや画像の半透明合成はしない。"""

    @classmethod
    def INPUT_TYPES(cls):
        return {"required": {
            "images": ("IMAGE",),
            "reference": ("IMAGE",),
            "amplitude": ("FLOAT", {"default": 0.3, "min": 0.0, "max": 1.0, "step": 0.05}),
            "frames": ("INT", {"default": 32, "min": 4, "max": 240}),
            "harmonics": ("INT", {"default": 3, "min": 1, "max": 8,
                                  "tooltip": "動きの周期を低次の波で表し、末尾→先頭も連続させる。画像自体は混ぜない"}),
            "spatial_smooth": ("FLOAT", {"default": 2.0, "min": 0.0, "max": 8.0, "step": 0.5}),
        }, "optional": {
            "rigid_mask_a": ("MASK", {"tooltip": "元絵の硬い部位。停止させず、移動・回転しながら形を保つ"}),
            "rigid_mask_b": ("MASK",),
            "rigid_mask_c": ("MASK",),
            "elastic_constraints": ("BOOLEAN", {"default": False, "tooltip": "硬い部位の周囲を連続した変形でつなぎ、細かな波打ちを抑える"}),
            "reference_rgba": ("IMAGE", {"tooltip": "元絵の透過を一度だけ作り、RGBと同じ写像で全コマへ引き継ぐ"}),
        }}

    RETURN_TYPES = ("IMAGE", "STRING", "MASK", "IMAGE")
    RETURN_NAMES = ("loop_images", "report", "alpha", "rgba")
    FUNCTION = "run"
    CATEGORY = "VillainProto"

    def run(self, images, reference, amplitude, frames, harmonics, spatial_smooth, rigid_mask_a=None, rigid_mask_b=None,
            rigid_mask_c=None, elastic_constraints=False, reference_rgba=None):
        import cv2
        import numpy as np

        source = reference[0, ..., :3].detach().cpu().numpy().astype(np.float32)
        batch = images[..., :3].detach().cpu().numpy()
        if len(batch) < 4 or source.shape != batch.shape[1:]:
            raise ValueError("同じ解像度の元絵と、4コマ以上の動きが必要")
        material = source if reference_rgba is None else reference_rgba[0].detach().cpu().numpy().astype(np.float32)
        if material.shape[:2] != source.shape[:2]:
            raise ValueError("透過済み元絵も同じ解像度が必要")
        alpha = material[..., 3] if material.shape[-1] == 4 else np.ones(source.shape[:2], np.float32)
        premultiplied = np.concatenate([material[..., :3] * alpha[..., None], alpha[..., None]], axis=-1)
        if amplitude == 0:
            rgb = np.repeat(source[None], frames, axis=0)
            rgba = np.repeat(np.concatenate([material[..., :3], alpha[..., None]], axis=-1)[None], frames, axis=0)
            return (torch.from_numpy(rgb), "振幅0: 元絵", torch.from_numpy(rgba[..., 3]), torch.from_numpy(rgba))
        source_gray = cv2.cvtColor(np.uint8(np.clip(source * 255, 0, 255)), cv2.COLOR_RGB2GRAY)
        dis = cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
        fields = []
        for image in batch:
            gray = cv2.cvtColor(np.uint8(np.clip(image * 255, 0, 255)), cv2.COLOR_RGB2GRAY)
            field = dis.calc(source_gray, gray, None)
            if spatial_smooth > 0:
                field = cv2.GaussianBlur(field, (0, 0), spatial_smooth)
            fields.append(field)
        field = np.stack(fields)
        # 全身の同じ運動場に同じ係数を掛ける。頭・首・腕を別々の領域で固定しない。
        spectrum = np.fft.rfft(field, axis=0) / len(field)
        count = min(harmonics, (len(field) - 1) // 2)
        h, w = source.shape[:2]
        grid_y, grid_x = np.mgrid[:h, :w].astype(np.float32)
        coordinates = np.stack([grid_x, grid_y], axis=-1)
        constraints = []
        foreground = (source[..., 1] - np.maximum(source[..., 0], source[..., 2])) < (30 / 255)
        for mask in (rigid_mask_a, rigid_mask_b, rigid_mask_c):
            if mask is None:
                continue
            region = mask[0].detach().cpu().numpy().astype(np.float32)
            if region.shape != (h, w):
                raise ValueError("硬い部位のマスクも元絵と同じ解像度が必要")
            ys, xs = np.nonzero((region > 0.5) & foreground)
            stride = max(1, len(xs) // 2048)
            ys, xs = ys[::stride], xs[::stride]
            if len(xs) < 6:
                raise ValueError("硬い部位のマスクに前景がほとんど無い")
            weight = cv2.dilate(region, np.ones((25, 25), np.uint8))
            weight = np.clip(cv2.GaussianBlur(weight, (0, 0), 4) * 1.03, 0, 1)[..., None]
            constraints.append((xs, ys, weight, region))
        rendered, rendered_rgba = [], []
        for t in range(frames):
            motion = spectrum[0].real.copy()
            for k in range(1, count + 1):
                motion += 2 * (spectrum[k] * np.exp(2j * np.pi * k * t / frames)).real
            motion = np.float32(motion * amplitude)
            rigid_fields = []
            for xs, ys, weight, region in constraints:
                points = coordinates[ys, xs]
                target = points + motion[ys, xs]
                rotation, translation = self.fit_rigid(points, target)
                rigid = coordinates @ rotation.T + translation - coordinates
                rigid_fields.append((np.float32(rigid), region))
                if not elastic_constraints:
                    motion = np.float32(motion * (1 - weight) + rigid * weight)
            if elastic_constraints:
                motion = self.elastic_field(motion, rigid_fields)
            # 順方向の運動を逆写像で描画する。元絵の線を1回だけ補間し、二重像を作らない。
            map_x, map_y = grid_x.copy(), grid_y.copy()
            for _ in range(6):
                sampled = cv2.remap(motion, map_x, map_y, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
                map_x = grid_x - sampled[..., 0]
                map_y = grid_y - sampled[..., 1]
            if reference_rgba is None:
                image = cv2.remap(source, map_x, map_y, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
                rgba = np.concatenate([image, np.ones((h, w, 1), np.float32)], axis=-1)
            else:
                warped = cv2.remap(premultiplied, map_x, map_y, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
                a = np.clip(warped[..., 3:4], 0, 1)
                color = np.divide(warped[..., :3], a, out=np.zeros_like(warped[..., :3]), where=a > 1e-6)
                rgba = np.concatenate([np.clip(color, 0, 1), a], axis=-1)
                image = color * a + np.array([0, 1, 0], np.float32) * (1 - a)
            rendered.append(image)
            rendered_rgba.append(rgba)
        report = f"全身の振幅 {amplitude:.2f} / 元の周期 {len(batch)} → {frames} コマ / 周期の次数 {count} / 硬い部位 {len(constraints)} / 周囲の連続変形 {elastic_constraints} / 共通の透過 {reference_rgba is not None} / 固定レイヤーなし"
        print("[VP Scale Idle Motion]", report)
        rgba = np.stack(rendered_rgba)
        return (torch.from_numpy(np.stack(rendered)), report, torch.from_numpy(rgba[..., 3]), torch.from_numpy(rgba))

    @staticmethod
    def elastic_field(motion, rigid_fields):
        """硬い部位の境界条件を、なだらかな変形として周囲へ伝える。"""
        import cv2
        import numpy as np
        h, w = motion.shape[:2]
        size = (max(2, w // 4), max(2, h // 4))
        target = cv2.resize(cv2.GaussianBlur(motion, (0, 0), 8), size, interpolation=cv2.INTER_AREA)
        locked = np.zeros(target.shape[:2], bool)
        fixed = np.zeros_like(target)
        for rigid, region in rigid_fields:
            # 小さい突起も粗い格子から落とさない。格子上では境界を少し広く取る。
            core = cv2.resize(cv2.dilate(region, np.ones((5, 5), np.uint8)), size, interpolation=cv2.INTER_AREA) > 0.05
            locked |= core
            fixed[core] = cv2.resize(rigid, size, interpolation=cv2.INTER_AREA)[core]
        value = target.copy()
        value[locked] = fixed[locked]
        kernel = np.array([[0, 1, 0], [1, 0, 1], [0, 1, 0]], np.float32)
        # 剛体境界と元の全身の動きを両立する screened Poisson の緩和。
        fidelity = 0.015
        for _ in range(220):
            value = (cv2.filter2D(value, -1, kernel, borderType=cv2.BORDER_REFLECT_101) + fidelity * target) / (4 + fidelity)
            value[locked] = fixed[locked]
        result = cv2.resize(value, (w, h), interpolation=cv2.INTER_CUBIC)
        # 格子の補間による硬い部位の誤差だけを内側で補正。周囲は既に解いてあるので差は小さい。
        for rigid, region in rigid_fields:
            weight = cv2.GaussianBlur(cv2.dilate(region, np.ones((5, 5), np.uint8)), (0, 0), 2)
            weight = np.clip(weight * 1.05, 0, 1)[..., None]
            result = result * (1 - weight) + rigid * weight
        return np.float32(result)

    @staticmethod
    def fit_rigid(points, target):
        import numpy as np
        origin, destination = points.mean(axis=0), target.mean(axis=0)
        u, _, vt = np.linalg.svd((points - origin).T @ (target - destination))
        rotation = vt.T @ u.T
        if np.linalg.det(rotation) < 0:
            vt[-1] *= -1
            rotation = vt.T @ u.T
        return rotation, destination - rotation @ origin


NODE_CLASS_MAPPINGS = {"VPIdleLoopFind": VPIdleLoopFind, "VPGreenKeyAlpha": VPGreenKeyAlpha, "VPScaleIdleMotion": VPScaleIdleMotion}
NODE_DISPLAY_NAME_MAPPINGS = {"VPIdleLoopFind": "VP Idle Loop Find", "VPGreenKeyAlpha": "VP Green Key Alpha", "VPScaleIdleMotion": "VP Scale Idle Motion"}
