"""自分のリィカ生成だけが待機中の場合、検品の切り直しを先に行う。実行中の生成は止めない。"""
import argparse
import json
import sys
from pathlib import Path
from types import SimpleNamespace

HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
sys.path.insert(0,str(REPO/'tools/comfy_idle_loop'))
import idle_loop as il


def priority(nodes):
    for item in il.http_json('/queue')['queue_pending']:
        if 'rica_' not in json.dumps(item[2]):raise RuntimeError('他の仕事が待機しているので優先しない')
    return il.http_json('/prompt',{'prompt':il.api_graph(nodes),'client_id':'rica-idle-recut','front':True})['prompt_id']


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('tag');p.add_argument('--crossfade',type=int,default=6);a=p.parse_args()
    root=(Path(il.OUT)/f'{a.tag}_final_rgba').resolve()
    if root.parent!=Path(il.OUT).resolve():raise ValueError('切り直し出力が既定フォルダの外')
    print('出力先確認:',root,flush=True)
    il.submit=priority
    il.cmd_recut(SimpleNamespace(tag=a.tag,crossfade=a.crossfade,max_length=64,min_length=16))
