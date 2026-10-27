"""自分の生成処理の進捗通知を読む。生成ジョブには手を加えない。"""
import asyncio
import json
from pathlib import Path
from urllib.request import urlopen
import aiohttp

DEST = Path(__file__).resolve().parent

async def main():
    with urlopen("http://127.0.0.1:8188/queue", timeout=10) as response:
        queue = json.load(response)
    jobs = json.loads((DEST / "jobs.json").read_text(encoding="utf-8"))
    owned = {job["prompt_id"]: job["tag"] for job in jobs}
    running = next((item for item in queue["queue_running"] if item[1] in owned), None)
    if running is None:
        print("自分の生成ジョブは現在実行中ではありません。", flush=True)
        return
    client_id = running[3].get("client_id", "utsu_progress")
    async with aiohttp.ClientSession() as session:
        async with session.ws_connect("http://127.0.0.1:8188/ws", params={"clientId": client_id}) as ws:
            try:
                async with asyncio.timeout(50):
                    async for msg in ws:
                        if msg.type != aiohttp.WSMsgType.TEXT:
                            continue
                        event = json.loads(msg.data)
                        data = event.get("data", {})
                        if data.get("prompt_id") not in owned:
                            continue
                        kind = event.get("type")
                        if kind in ("progress", "executing", "execution_success", "execution_error"):
                            (DEST / "progress.json").write_text(json.dumps(event, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
                            print(owned[data["prompt_id"]], kind, {k: data[k] for k in ("node", "value", "max", "exception_message") if k in data}, flush=True)
            except TimeoutError:
                pass

asyncio.run(main())
