"""導入済み24コマとユーザーが選んだ候補Aの画素を照合する。"""
import json
from pathlib import Path
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
candidate = sorted((HERE / "candidates/A/frames").glob("*.png"))
installed = sorted((REPO / "DemoApp/assets/portraits/battle/idle_loop/utsu").glob("*.png"))
assert len(candidate) == len(installed) == 24
for source, target in zip(candidate, installed):
    with Image.open(source) as image_a, Image.open(target) as image_b:
        assert image_b.mode == "RGBA" and image_b.size == (640, 960)
        assert np.array_equal(np.asarray(image_a), np.asarray(image_b)), target
manifest = {"selected": "A", "tag": "utsu_idle_v1_s2022_final", "frames": 24,
            "fps": 16, "seconds": 1.5, "pixels_match": True,
            "installed_bytes": sum(path.stat().st_size for path in installed)}
(HERE / "integration.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("UTSU_IDLE_INSTALL_OK", json.dumps(manifest, ensure_ascii=False))
