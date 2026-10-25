"""初版2候補が静止した段だけ、既存genへ強めた動作と第3seedを渡す。"""
import argparse
from types import SimpleNamespace
import recut_priority as local

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('key');p.add_argument('--seeds',default='3033');a=p.parse_args()
    local.il.submit=local.priority
    local.il.cmd_gen(SimpleNamespace(key=a.key,seeds=a.seeds))
