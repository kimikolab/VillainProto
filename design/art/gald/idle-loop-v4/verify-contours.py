"""局所の波打ちと、透過を同じ運動で引き継ぐ処理を既知の入力で確認する。"""
import importlib.util
from pathlib import Path
import cv2
import numpy as np
import torch

repo = Path(__file__).resolve().parents[4]
spec = importlib.util.spec_from_file_location("vp_motion", repo / "tools/comfy_idle_loop/comfy_node/__init__.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
node = module.VPScaleIdleMotion()
y, x = np.mgrid[:128, :128].astype(np.float32)
rough = np.stack([3 * np.sin(x * .8), 3 * np.cos(y * .8)], axis=-1)
region = np.zeros((128, 128), np.float32)
region[40:90, 40:90] = 1
rigid = np.zeros_like(rough)
rigid[:] = [2, -3]
smooth = node.elastic_field(rough, [(rigid, region)])
assert np.max(np.abs(smooth[45:85, 45:85] - rigid[45:85, 45:85])) < .05
def roughness(a):
    return float(np.mean(np.abs(cv2.Laplacian(a, -1))))
assert roughness(smooth) < roughness(rough) * .1
ux_y, ux_x = np.gradient(smooth[..., 0])
uy_y, uy_x = np.gradient(smooth[..., 1])
jacobian = (1 + ux_x) * (1 + uy_y) - ux_y * uy_x
assert jacobian.min() > 0, float(jacobian.min())
print(f"ELASTIC_FIELD_CHECK_OK roughness={roughness(rough):.4f}->{roughness(smooth):.4f} min_jacobian={jacobian.min():.3f}")

rng = np.random.default_rng(741)
source = np.empty((128, 128, 3), np.float32)
source[:] = [0, .7, .25]
source[30:96, 30:96] = rng.uniform(.25, .95, (66, 66, 3))
matte = np.zeros((128, 128, 1), np.float32)
matte[30:96, 30:96] = 1
rgba = np.concatenate([source, matte], axis=-1)
images = []
for t in range(12):
    dx = 6 * np.sin(2 * np.pi * t / 12)
    images.append(cv2.warpAffine(source, np.float32([[1, 0, dx], [0, 1, 0]]), (128, 128), borderValue=(0, .7, .25)))
_, _, alpha, rendered = node.run(torch.from_numpy(np.stack(images)), torch.from_numpy(source[None]), .4, 24, 2, 1,
                                elastic_constraints=True, reference_rgba=torch.from_numpy(rgba[None]))
assert torch.equal(alpha, rendered[..., 3])
edge = (alpha > .05) & (alpha < .95)
assert edge.any()
assert rendered[..., :3][edge].min() >= .25 - 1e-5
assert rendered[..., :3][edge].max() <= .95 + 1e-5
assert torch.isfinite(rendered).all()
assert torch.max(torch.abs(rendered[..., :3][alpha == 0])) == 0
print("SHARED_ALPHA_CHECK_OK: 同一の写像でRGBと透過を駆動、縁の色を維持")
