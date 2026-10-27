"""元絵と同じ足底行へ、連番全体を同じ整数画素だけ平行移動する。部位変形はしない。"""
from pathlib import Path
from PIL import Image
import numpy as np,json,argparse
p=argparse.ArgumentParser();p.add_argument("tag");p.add_argument("key");args=p.parse_args()
root=Path("D:/comfyui/output/VillainProto/idle_loop").resolve()
src=(root/(args.tag+"_rgba")).resolve();dst=(root/(args.tag+"_aligned_rgba")).resolve()
assert src.is_relative_to(root) and dst.is_relative_to(root) and src.is_dir() and not dst.exists()
prompts=json.loads(Path("tools/comfy_idle_loop/prompts.json").read_text(encoding="utf-8"))
reference=Image.open(prompts[args.key]["still"]).convert("RGBA").resize((640,960),Image.Resampling.LANCZOS)
sole=lambda im:int(np.where(np.array(im.getchannel("A"))>128)[0].max())
files=sorted(src.glob("*.png")); bottoms=[sole(Image.open(f).convert("RGBA")) for f in files]
shift=sole(reference)-int(round(float(np.median(bottoms))))
assert abs(shift)<=12,"元絵と足元が大きく違うため、自動で補正しない"
dst.mkdir()
for f in files:
 im=Image.open(f).convert("RGBA"); canvas=Image.new("RGBA",im.size)
 canvas.paste(im,(0,shift));canvas.save(dst/f.name)
report={"source":args.tag,"output":args.tag+"_aligned","reference":prompts[args.key]["still"],"reference_sole":sole(reference),"source_sole_range":[min(bottoms),max(bottoms)],"translation_y":shift,"frames":len(files)}
Path(__file__).with_name(args.tag+"-alignment.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(json.dumps(report,ensure_ascii=False))
