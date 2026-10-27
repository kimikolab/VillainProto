"""生成済みの動画を既存ノードで処理。待機の継ぎ目補修／演奏の単発48コマ。"""
from pathlib import Path
import argparse,json,sys,shutil,uuid
REPO=Path(__file__).resolve().parents[4];sys.path.insert(0,str(REPO/"tools/comfy_idle_loop"))
import idle_loop as il
p=argparse.ArgumentParser();p.add_argument("kind",choices=["idle12","skill"]);p.add_argument("tag");args=p.parse_args()
assert args.tag.startswith("ere_") and "/" not in args.tag and "\\" not in args.tag
root=Path(__file__).resolve().parent
prefix=args.tag+"_seam12" if args.kind=="idle12" else args.tag.replace("ere_song_","ere_skill_")
assert not Path(il.OUT,prefix+"_rgba").exists()
raws=sorted(Path(il.OUT).glob(args.tag+"_raw_*.mp4"));assert raws
video="vp_"+args.tag+"_raw.mp4";shutil.copy2(raws[-1],Path(il.COMFY,"input",video))
nodes=il.nodes09(video,prefix,12,64,min_length=32)
if args.kind=="skill":
 nodes=[(i,"ImageFromBatch",{"image":[2,0],"batch_index":0,"length":48},pos,"演奏の1フレーズ・単発") if i==40 else
 (i,c,{**inp,"filename_prefix":f"VillainProto/idle_loop/{prefix}_performance"},pos,title) if i==44 else (i,c,inp,pos,title)
 for i,c,inp,pos,title in nodes if i!=45]
graph=il.api_graph(nodes)
(root/(prefix+"-workflow.json")).write_text(json.dumps(graph,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
q=il.http_json("/queue")
owned=all(r[2].get("43",{}).get("inputs",{}).get("filename_prefix","").startswith("VillainProto/idle_loop/ere_") for r in q["queue_pending"])
r=il.http_json("/prompt",{"prompt":graph,"client_id":str(uuid.uuid4()),"front":owned})
jobs=json.loads((root/"jobs.json").read_text());jobs.append({"tag":prefix,"prompt_id":r["prompt_id"],"source":args.tag,"kind":args.kind})
(root/"jobs.json").write_text(json.dumps(jobs,indent=2)+"\n",encoding="utf-8")
print(prefix,r["prompt_id"])
