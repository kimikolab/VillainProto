"""既存の呼吸動画をComfyUIで仕上げる。固定部は元絵、胸・肩・布は生成動画を使う。"""
import argparse
import json
from pathlib import Path
import shutil
import sys

from PIL import Image, ImageDraw, ImageFilter


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("tag")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[4]
    sys.path.insert(0, str(repo / "tools/comfy_idle_loop"))
    import idle_loop as m

    # これは元絵の加工ではなく、ComfyUI合成用の領域マスク。合成・透過はComfyUI内で行う。
    mask = Image.new("L", (640, 960), 0)
    draw = ImageDraw.Draw(mask)
    draw.rectangle((274, -20, 442, 214), fill=255)
    draw.polygon([(18, 380), (145, 380), (173, 440), (600, 702),
                  (588, 746), (500, 729), (100, 532), (35, 500)], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(4))
    mask_path = Path(__file__).with_name("fixed-helmet-sword-mask.png")
    mask.save(mask_path)
    shutil.copy2(mask_path, Path(m.COMFY) / "input" / mask_path.name)
    raws = sorted(Path(m.OUT).glob(args.tag + "_raw_*.mp4"))
    assert raws, "生成の生動画が必要"
    input_name = args.tag + "_raw.mp4"
    shutil.copy2(raws[-1], Path(m.COMFY) / "input" / input_name)
    prefix = args.tag + "_stable"
    nodes = [
        (1, "LoadVideo", {"file": input_name}, (0, 0), None),
        (2, "GetVideoComponents", {"video": [1, 0]}, (0, 1), None),
        (5, "LoadImage", {"image": "vp_gald_breath_v2.png"}, (0, 2), None),
        (6, "ImageScale", {"image": [5, 0], "upscale_method": "lanczos", "width": 640, "height": 960, "crop": "disabled"}, (1, 2), None),
        (7, "LoadImageMask", {"image": mask_path.name, "channel": "red"}, (1, 3), None),
        (8, "ImageCompositeMasked", {"destination": [2, 0], "source": [6, 0], "x": 0, "y": 0, "resize_source": False, "mask": [7, 0]}, (2, 1), None),
        (20, "CreateVideo", {"images": [8, 0], "fps": 16.0}, (3, 0), None),
        (21, "SaveVideo", {"video": [20, 0], "filename_prefix": f"VillainProto/idle_loop/{prefix}_raw", "format": "auto", "format.codec": "auto"}, (4, 0), None),
        *m.post_nodes([8, 0], prefix, crossfade=0, max_length=64, min_length=24),
    ]
    out = Path(__file__).with_name(prefix + "-workflow.json")
    out.write_text(json.dumps(m.api_graph(nodes), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(out)


if __name__ == "__main__":
    main()
