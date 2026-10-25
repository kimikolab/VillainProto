"""待機の検品画像と実行したワークフローを保存する。素材の画素は変更しない。"""
import json
import shutil
import sys
from pathlib import Path
from urllib.request import urlopen

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO / 'tools/comfy_idle_loop'))
import idle_loop as il

chosen = [(1, 'rica_stage1_s5055_final', 5055, 6, 62),
          (2, 'rica_stage2_s3033', 3033, 0, 25),
          (3, 'rica_stage3_s2022_final', 2022, 8, 43)]
dest = HERE / 'idle-review'
dest.mkdir(exist_ok=True)
entries = il.load_prompts()
for stage, tag, seed, fade, frames in chosen:
    for suffix in ['_compare.gif', '_check.jpg']:
        shutil.copyfile(Path(il.OUT) / (tag + suffix), dest / (tag + suffix))
    key = f'rica_stage{stage}'
    entries[key]['note'] = ('2026-10-24 覚醒待機を生成・検品し、ゲームとFxViewerへ仮適用。'
        '最終の好みはユーザー確認待ち。通常ricaの承認済み39コマは維持。'
        '採否・足元・継ぎ目はdesign/art/rica/attack-motion-v1/idle-results.md。')
    entries[key]['adopted'] = dict(tag=tag, seed=seed, crossfade=fade, frames=frames,
        fps=16, geometry_postprocessing=False, loop=key, date='2026-10-24',
        status='provisional_pending_user_visual_choice',
        preview=f'design/art/rica/attack-motion-v1/idle-review/{tag}_compare.gif')
prompt_path = REPO / 'tools/comfy_idle_loop/prompts.json'
prompt_path.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')

# 履歴の実行グラフを使う。途中で強めたmotionや余白追加前のグラフも実際の値を保持する。
history = json.load(urlopen(il.HOST + '/history', timeout=30))
records = []
workflow_dir = dest / 'workflows'
workflow_dir.mkdir(exist_ok=True)
for pid, job in history.items():
    graph = job['prompt'][2]
    prefixes = [n.get('inputs', {}).get('filename_prefix', '') for n in graph.values()]
    if not any('/rica_stage' in p and p.endswith('_rgba/f') for p in prefixes):
        continue
    # f は最後、直前が <tag>_rgba。フォルダ名から出力タグを復元する。
    prefix = next(p for p in prefixes if '/rica_stage' in p and p.endswith('_rgba/f'))
    tag = prefix.split('/')[-2][:-5]
    filename = f'{tag}-{pid[:8]}.json'
    (workflow_dir / filename).write_text(json.dumps(graph, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    records.append(dict(tag=tag, prompt_id=pid, workflow=filename,
        completed=job.get('status', {}).get('completed'),
        messages=job.get('status', {}).get('messages', [])))
(dest / 'jobs.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('検品画像', len(chosen) * 2, '実行グラフ', len(records), '候補を仮適用として記録')
