"""生成条件と履歴の保存。ComfyUIの仕事の変更・停止は行わない。"""
import json
from pathlib import Path
from urllib.request import urlopen

DEST = Path(__file__).resolve().parent
HOST = "http://127.0.0.1:8188"

def read(path):
    with urlopen(HOST + path, timeout=15) as response:
        return json.load(response)

def save(name, value):
    (DEST / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

jobs_path = DEST / "jobs.json"
jobs = json.loads(jobs_path.read_text(encoding="utf-8")) if jobs_path.exists() else []
queue = read("/queue")
recent = read("/history?max_items=10")
for item in queue["queue_running"] + queue["queue_pending"] + [value["prompt"] for value in recent.values()]:
    graph = item[2]
    prefix = graph.get("15", {}).get("inputs", {}).get("filename_prefix", "")
    frame_prefix = graph.get("43", {}).get("inputs", {}).get("filename_prefix", "")
    if prefix.startswith("VillainProto/idle_loop/utsu_idle_v1_") or frame_prefix.startswith("VillainProto/idle_loop/utsu_idle_v1_"):
        tag = prefix.rsplit("/", 1)[-1].removesuffix("_raw") if prefix else frame_prefix.rsplit("/", 2)[-2].removesuffix("_rgba")
        if not any(job["prompt_id"] == item[1] for job in jobs):
            jobs.append({"tag": tag, "prompt_id": item[1], "seed": graph.get("11", {}).get("inputs", {}).get("noise_seed")})
        save(tag + "-workflow.json", graph)
save("jobs.json", jobs)
for job in jobs:
    history = read("/history/" + job["prompt_id"])
    if job["prompt_id"] in history:
        value = history[job["prompt_id"]]
        save(job["tag"] + "-history.json", value)
        report = value.get("outputs", {}).get("45", {}).get("text", [])
        print(job["tag"], value["status"].get("status_str"), report)
    else:
        state = "running" if any(item[1] == job["prompt_id"] for item in queue["queue_running"]) else "pending"
        print(job["tag"], state)
