"""生成動画の連続48コマを、既存のBiRefNet＋クロマキーで単発の演奏にする。"""
from pathlib import Path
import argparse, json, shutil, sys

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[3]
sys.path.insert(0, str(REPO / 'tools/comfy_idle_loop'))
import idle_loop as il

parser = argparse.ArgumentParser()
parser.add_argument('seed', type=int, choices=(2022, 3033))
parser.add_argument('--start', type=int, default=0)
args = parser.parse_args()
assert 0 <= args.start <= 33
source = f'ere_performance_v2_s{args.seed}'
prefix = f'{source}_phrase{args.start}'
assert not Path(il.OUT, prefix + '_rgba').exists(), '既存の候補は上書きしない'
raw = sorted(Path(il.OUT).glob(source + '_raw_*.mp4'))[-1]
video = 'vp_' + source + '_raw.mp4'
shutil.copy2(raw, Path(il.COMFY, 'input', video))
nodes = il.nodes09(video, prefix, 0, 48)
nodes = [
    (i, 'ImageFromBatch', {'image': [2, 0], 'batch_index': args.start, 'length': 48}, pos, '連続した演奏の1フレーズ') if i == 40 else
    (i, kind, {**inputs, 'filename_prefix': f'VillainProto/idle_loop/{prefix}_performance'}, pos, title) if i == 44 else
    (i, kind, inputs, pos, title)
    for i, kind, inputs, pos, title in nodes if i != 45
]
graph = il.api_graph(nodes)
(ROOT / (prefix + '-workflow.json')).write_text(json.dumps(graph, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
prompt_id = il.submit(nodes)
jobs = json.loads((ROOT / 'jobs.json').read_text(encoding='utf-8'))
jobs.append({'tag': prefix, 'prompt_id': prompt_id, 'source': source, 'source_range': [args.start, args.start + 48]})
(ROOT / 'jobs.json').write_text(json.dumps(jobs, indent=2) + '\n', encoding='utf-8')
print('SUBMITTED', prefix, prompt_id, flush=True)
if not il.wait(prompt_id, prefix):
    raise SystemExit(1)
history = il.http_json('/history/' + prompt_id)[prompt_id]
(ROOT / (prefix + '-history.json')).write_text(json.dumps(history, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
