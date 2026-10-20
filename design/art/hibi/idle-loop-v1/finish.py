import argparse
import json
import shutil
import urllib.request
from pathlib import Path

from PIL import Image, ImageDraw


folder = Path(__file__).resolve().parent
repo = folder.parents[3]
comfy = Path("D:/comfyui")
parser = argparse.ArgumentParser()
parser.add_argument("seed", type=int)
parser.add_argument("--amplitude", type=float, default=0.4)
parser.add_argument("--offset-y", type=int, default=0)
args = parser.parse_args()
tag = f"hibi_s{args.seed}_coherent_a{round(args.amplitude * 100):02d}_y{args.offset_y}"
if (comfy / f"output/VillainProto/idle_loop/{tag}_rgba").exists():
    raise FileExistsError(f"既存の候補は上書きしません: {tag}")
raws = sorted((comfy / "output/VillainProto/idle_loop").glob(f"hibi_s{args.seed}_raw_*.mp4"))
if not raws:
    raise FileNotFoundError("先に動画生成の保存完了を確認してください")
raw_name = f"vp_hibi_s{args.seed}_raw.mp4"
shutil.copy2(raws[-1], comfy / "input" / raw_name)
shutil.copy2(raws[-1], folder / f"source-s{args.seed}.mp4")
mask = Image.new("L", (640, 960))
shield_polygon = [(398, 130), (427, 129), (442, 150), (478, 163), (512, 186), (546, 221), (571, 262), (590, 314), (603, 372), (607, 434), (602, 496), (593, 554), (580, 594), (557, 633), (538, 637), (502, 618), (471, 583), (449, 544), (427, 498), (409, 450), (391, 398), (379, 347), (369, 295), (368, 246), (378, 201), (390, 177)]
ImageDraw.Draw(mask).polygon(shield_polygon, fill=255)
mask_name = "hibi-v1-shield-rigid-mask.png"
mask.save(folder / mask_name)
shutil.copy2(folder / mask_name, comfy / "input" / mask_name)
graph = json.loads((repo / "design/art/gare/idle-loop-v2/gare-A80-workflow.json").read_text(encoding="utf-8"))
graph["1"]["inputs"]["file"] = raw_name
graph["5"]["inputs"]["image"] = "vp_hibi.png"
graph["40"]["inputs"].update(crossfade=6)
graph["50"] = {"class_type": "LoadImageMask", "inputs": {"image": mask_name, "channel": "red"}}
graph["22"]["inputs"].update(amplitude=args.amplitude, frames=32, harmonics=3, spatial_smooth=6.0, rigid_mask_a=["50", 0], elastic_constraints=True)
image_link = ["22", 3]
if args.offset_y:
    graph["70"] = {"class_type": "EmptyImage", "inputs": {"width": 640, "height": 960, "batch_size": 32, "color": 0}}
    graph["71"] = {"class_type": "SolidMask", "inputs": {"value": 1, "width": 640, "height": 960}}
    graph["72"] = {"class_type": "JoinImageWithAlpha", "inputs": {"image": ["70", 0], "alpha": ["71", 0]}}
    graph["73"] = {"class_type": "ImageCompositeMasked", "inputs": {"destination": ["72", 0], "source": image_link, "x": 0, "y": args.offset_y, "resize_source": False}}
    image_link = ["73", 0]
graph["24"]["inputs"].update(images=image_link, filename_prefix=f"VillainProto/idle_loop/{tag}_rgba/f")
graph["25"]["inputs"].update(images=image_link, filename_prefix=f"VillainProto/idle_loop/{tag}_loop", lossless=True)
graph["26"] = {"class_type": "PreviewAny", "inputs": {"source": ["40", 4]}}
(folder / f"{tag}-workflow.json").write_text(json.dumps(graph, indent=2) + "\n", encoding="utf-8")
request = urllib.request.Request("http://127.0.0.1:8188/prompt", data=json.dumps({"prompt": graph}).encode(), headers={"Content-Type": "application/json"})
with urllib.request.urlopen(request, timeout=30) as response:
    result = json.load(response)
result.update(tag=tag, seed=args.seed, amplitude=args.amplitude, offset_y=args.offset_y)
(folder / f"{tag}-job.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result))
