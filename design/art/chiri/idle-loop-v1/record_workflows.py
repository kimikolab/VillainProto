"""チリ待機候補の入力・プロンプト・投入済みワークフローを記録する。"""
import hashlib
import importlib.util
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
spec = importlib.util.spec_from_file_location("idle_loop", REPO / "tools/comfy_idle_loop/idle_loop.py")
idle = importlib.util.module_from_spec(spec)
spec.loader.exec_module(idle)
entry = idle.load_prompts()["chiri"]
positive, negative = idle.compose(entry)
source = REPO / entry["still"]
record = {
    "source": entry["still"],
    "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
    "positive": positive,
    "negative": negative,
    "seeds": [2022, 5055],
    "generation_size": [640, 960],
    "generation_frames": 81,
    "fps": 16,
    "status": "candidates_pending_user_choice",
    "jobs": [],
}
saved = HERE / "generation.json"
if saved.exists():
    previous = json.loads(saved.read_text(encoding="utf-8"))
    record["jobs"] = previous.get("jobs", [])
    record["status"] = previous.get("status", record["status"])
queue = idle.http_json("/queue")
for job in queue["queue_running"] + queue["queue_pending"]:
    graph = job[2]
    tag = graph.get("15", {}).get("inputs", {}).get("filename_prefix", "").split("/")[-1]
    if not tag.startswith("chiri_idle_v1_"):
        continue
    if not any(item["prompt_id"] == job[1] for item in record["jobs"]):
        record["jobs"].append({"prompt_id": job[1], "tag": tag})
    (HERE / f"{tag}-workflow.json").write_text(json.dumps(graph, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
for job in record["jobs"]:
    history = idle.http_json(f"/history/{job['prompt_id']}")
    if job["prompt_id"] in history:
        (HERE / f"{job['tag']}-history.json").write_text(json.dumps(history, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
(HERE / "generation.json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"jobs": record["jobs"], "source_sha256": record["source_sha256"]}, ensure_ascii=False))
