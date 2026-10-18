"""承認された一刀両断をゲームの単発連番として配置する。原画は変更しない。"""
import json
import shutil
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
DST = ROOT / 'DemoApp/assets/portraits/battle/attack_clip/gald_cleave'

def main():
    files = sorted((HERE / 'frames').glob('f*.png'))
    assert len(files) == 28
    DST.mkdir(parents=True, exist_ok=True)
    for i, path in enumerate(files):
        shutil.copy2(path, DST / f'gald_cleave_{i:02}.png')
    meta = {'source': 'gald-sword-slash-v2/sheet_animation', 'frames': 28, 'size': [784,608],
            'fps': 25., 'contact': 15, 'anchor': [318,572], 'idleAnchor': [470,1432], 'scale': 3.25,
            'referencePortrait': 'gald_sword', 'forward': [0.]*28}
    (DST / 'clip.json').write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding='utf-8', newline='\n')
    print('GALD_CLEAVE_EXPORTED', len(files))

if __name__ == '__main__':
    main()
