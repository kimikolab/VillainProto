"""採用済みの通常39コマは保ち、覚醒3段の待機候補を既存の道具へ登録する。"""
import json
import shutil
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
BATTLE = REPO / 'DemoApp/assets/portraits/battle'
PATH = REPO / 'tools/comfy_idle_loop/prompts.json'
entries = json.loads(PATH.read_text(encoding='utf-8'))
guards = {
    1: 'a low stable wide stance, both hands holding the shovel almost horizontally across her waist, the steel spade to the right',
    2: 'a confident two-handed high guard, hands near the shoulder, the shovel spade raised above and to the right of her head',
    3: 'a composed upright stance, holding the shovel nearly vertically beside her body with the steel spade above her head, cape slightly spread',
}
for stage, guard in guards.items():
    key = f'rica_stage{stage}'
    prior=entries.get(key,{})
    shutil.copyfile(BATTLE / f'motion_sources/rica/v1/stage{stage}/idle.png', BATTLE / f'{key}_idle_right.png')
    entries[key] = {
        'still': f'DemoApp/assets/portraits/battle/{key}_ready_right.png' if (BATTLE/f'{key}_ready_right.png').exists() else f'DemoApp/assets/portraits/battle/{key}_idle_right.png',
        'pronoun': 'she',
        'char': 'A petite adult elf woman with silver bob hair and one chest braid, a charcoal hooded cape worn down, dark teal dress, worn tan work apron, brown gloves and boots. She keeps ' + guard + '. One intact straight rigid wooden shovel with a single D grip and one pointed steel spade. ',
        'motion': ('She performs a clearly visible rhythmic fighting-game knee-bend and rise, repeatedly lowering her pelvis by HALF A HEAD HEIGHT then rising back. Both knees deeply flex and extend, her hips and upright torso visibly travel down and up, and both shoulders, elbows and hands travel together with the torso. Her feet remain fully planted in exactly the same spots. Her shovel rises and falls rigidly with both hands while staying in the high guard. Chest lifted, calm alert face. This is lively full-body movement, not a still portrait with moving clothes. ' if stage==2 else 'Like a skilled fighting game character, she makes steady rhythmic idle motion: both knees clearly bend and straighten, her hips and chest lower then rise as one connected body, shoulders, neck, elbows and wrists follow naturally. Keep her back straight, chest lifted and calm alert face. Both hands retain their positions on the shovel shaft. '),
        'sway': 'The cape, apron hem and braid sway naturally with her whole body. The shovel shaft is straight and rigid and moves together with both hands without bending. No fire, no flames, no aura, no glow, no energy blade.',
        'neg': 'bent shovel, flexible shaft, missing spade, duplicate shovel, extra limbs, floating foot, exhausted posture, slouching, hunching, sword, spear, aura, glow',
        'note': '2026-10-24 段別モーション元絵から。通常の採用39コマは維持。2022/5055の比較用候補を生成し、FxViewerと比較GIFで確認する。',
    }
    if prior.get('adopted'):entries[key]['adopted']=prior['adopted']
PATH.write_text(json.dumps(entries, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('覚醒3段の元絵と待機プロンプトを登録')
