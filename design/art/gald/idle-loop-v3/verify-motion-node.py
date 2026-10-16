"""振幅調整ノードを、既知の運動で確かめる（人物の自然さは別途目視する）。"""
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
rng = np.random.default_rng(741)
source = np.empty((128, 128, 3), np.float32)
source[:] = [0, .7, .25]
source[30:96, 30:96] = rng.uniform(.25, .95, (66, 66, 3))
sequence = []
for t in range(12):
    dx = 6 * np.sin(2 * np.pi * t / 12)
    sequence.append(cv2.warpAffine(source, np.float32([[1, 0, dx], [0, 1, 0]]), (128, 128), borderValue=(0, .7, .25)))
reference = torch.from_numpy(source[None].copy())
images = torch.from_numpy(np.stack(sequence))
before_images, before_reference = images.clone(), reference.clone()
zero, *_ = node.run(images, reference, 0, 24, 2, 1)
assert torch.equal(zero, reference.expand(24, -1, -1, -1))
output, *_ = node.run(images, reference, .4, 24, 2, 1)
centers, widths = [], []
for image in output.numpy():
    _, xs = np.nonzero(image[:, :, 0] > .15)
    centers.append(xs.mean())
    widths.append(xs.max() - xs.min())
span = np.ptp(centers)
steps = np.abs(np.diff(centers))
assert 3.8 < span < 5.8, span
assert abs(centers[-1] - centers[0]) < max(steps) * 1.5
assert np.ptp(widths) <= 2
assert torch.equal(before_images, images) and torch.equal(before_reference, reference)
assert output.shape == (24, 128, 128, 3) and torch.isfinite(output).all()
print(f"MOTION_SCALE_CHECK_OK range={span:.3f}")

points = np.array([[0., 0.], [0., 40.], [300., 0.], [300., 40.], [150., 20.]])
angle = .12
rotation = np.array([[np.cos(angle), -np.sin(angle)], [np.sin(angle), np.cos(angle)]])
translation = np.array([4., -7.])
target = points @ rotation.T + translation
fit, shift = node.fit_rigid(points, target)
assert np.allclose(fit, rotation) and np.allclose(shift, translation)
assert np.isclose(np.linalg.det(fit), 1) and np.allclose(fit.T @ fit, np.eye(2))
transformed = points @ fit.T + shift
assert np.isclose(np.linalg.norm(transformed[2] - transformed[0]), 300)
print("RIGID_MOTION_CHECK_OK")
