import json
import shutil
import urllib.request
from pathlib import Path

from PIL import Image, ImageDraw


folder = Path(__file__).resolve().parent
comfy = Path("D:/comfyui")
graph = json.loads((folder.parent / "idle-loop-v2/gare-A80-workflow.json").read_text(encoding="utf-8"))
regions = {
    "sleeve-left": [(208, 178), (264, 181), (282, 203), (278, 255), (254, 293), (231, 308), (207, 322), (190, 337), (163, 341), (146, 325), (145, 299), (134, 278), (171, 255), (177, 232), (180, 211)],
    "sleeve-right": [(397, 337), (424, 341), (438, 360), (443, 380), (424, 397), (406, 388), (399, 365)],
    "scattergun": [(171, 330), (177, 307), (250, 297), (331, 281), (505, 267), (583, 241), (610, 248), (630, 295), (631, 326), (620, 348), (594, 348), (546, 328), (505, 322), (452, 333), (403, 338), (359, 338), (349, 352), (332, 354), (323, 342), (230, 375), (190, 378)],
}
for node_index, (name, polygon) in enumerate(regions.items(), start=50):
    mask = Image.new("L", (640, 960))
    ImageDraw.Draw(mask).polygon(polygon, fill=255)
    filename = f"gare-v3-{name}-mask.png"
    mask.save(folder / filename)
    shutil.copy2(folder / filename, comfy / "input" / filename)
    graph[str(node_index)] = {"class_type": "LoadImageMask", "inputs": {"image": filename, "channel": "red"}}
    graph["22"]["inputs"][f"rigid_mask_{'abc'[node_index - 50]}"] = [str(node_index), 0]

graph["22"]["inputs"].update(amplitude=0.7, harmonics=3, spatial_smooth=6.0, elastic_constraints=True)
tag = "gare_A70_coherent_aligned"
graph["70"] = {"class_type": "EmptyImage", "inputs": {"width": 640, "height": 960, "batch_size": 30, "color": 0}}
graph["71"] = {"class_type": "SolidMask", "inputs": {"value": 1, "width": 640, "height": 960}}
graph["72"] = {"class_type": "JoinImageWithAlpha", "inputs": {"image": ["70", 0], "alpha": ["71", 0]}}
graph["73"] = {"class_type": "ImageCompositeMasked", "inputs": {"destination": ["72", 0], "source": ["22", 3], "x": 0, "y": 1, "resize_source": False}}
graph["24"]["inputs"]["images"] = ["73", 0]
graph["25"]["inputs"]["images"] = ["73", 0]
graph["24"]["inputs"]["filename_prefix"] = f"VillainProto/idle_loop/{tag}_rgba/f"
graph["25"]["inputs"].update(filename_prefix=f"VillainProto/idle_loop/{tag}_loop", lossless=True)
output = comfy / f"output/VillainProto/idle_loop/{tag}_rgba"
if output.exists():
    raise FileExistsError(f"出力済みの候補は上書きしません: {output}")
(folder / "workflow.json").write_text(json.dumps(graph, indent=2) + "\n", encoding="utf-8")
request = urllib.request.Request("http://127.0.0.1:8188/prompt", data=json.dumps({"prompt": graph}).encode(), headers={"Content-Type": "application/json"})
with urllib.request.urlopen(request, timeout=30) as response:
    result = json.load(response)
(folder / "job.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result))
