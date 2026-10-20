import json
import shutil
import sys
from pathlib import Path

from PIL import Image


folder = Path(__file__).resolve().parent
repo = folder.parents[3]
sys.path.insert(0, str(repo / "tools/comfy_idle_loop"))
import idle_loop


idle_loop.require_comfy()
prompt_path = folder / "prompt.json"
entry = json.loads(prompt_path.read_text(encoding="utf-8")) if prompt_path.exists() else idle_loop.load_prompts()["hibi"]
positive, negative = idle_loop.compose(entry)
source = repo / entry["still"]
with Image.open(source) as image:
    width, height = image.size
input_name = "vp_hibi_native_v2.png"
shutil.copy2(source, Path(idle_loop.COMFY) / "input" / input_name)
prompt_path.write_text(json.dumps(entry, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
jobs = []
for seed in (2022, 3033):
    tag = f"hibi_native_v2_s{seed}"
    if list(Path(idle_loop.OUT).glob(f"{tag}*")):
        raise RuntimeError(f"既存の出力を上書きしません: {tag}")
    nodes = idle_loop.nodes08(input_name, positive, negative, seed, tag, width, height)
    for index, class_name, inputs, position, title in nodes:
        if class_name == "SaveAnimatedWEBP":
            inputs["lossless"] = True
    graph = idle_loop.api_graph(nodes)
    (folder / f"{tag}-workflow.json").write_text(json.dumps(graph, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    prompt_id = idle_loop.submit(nodes)
    jobs.append({"tag": tag, "seed": seed, "prompt_id": prompt_id})
    (folder / "jobs.json").write_text(json.dumps(jobs, indent=2) + "\n", encoding="utf-8")
    print(f"投入: {tag} {prompt_id}", flush=True)
for job in jobs:
    success = idle_loop.wait(job["prompt_id"], job["tag"])
    result = idle_loop.http_json(f"/history/{job['prompt_id']}")[job["prompt_id"]]
    (folder / f"{job['tag']}-result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if not success:
        raise RuntimeError(f"生成に失敗: {job['tag']}")
    raw_video = next(Path(idle_loop.OUT).glob(f"{job['tag']}_raw_*.mp4"))
    shutil.copy2(raw_video, folder / f"source-s{job['seed']}.mp4")
print("HIBI_NATIVE_V2_GENERATION_OK", flush=True)
