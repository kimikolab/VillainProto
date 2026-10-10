# 粛の伝令 — 砕けた後 v4

- 作成日: 2026-10-10
- 生成: 内蔵image_gen。ComfyUIは未使用。
- 編集元: `husher-shattered-right-v3.png` と同一のゲーム用PNG。
- 保存原本: `husher-shattered-right-v4.png`
- ゲーム素材: `DemoApp/assets/portraits/battle/husher_shattered_idle_right.png`
- 1024×1536、RGBA透過。旧原本も保存。
- v3の胸元に残った不自然な腕・手の形を除去。左肩から肘、胸の前を横切る前腕、反対の上腕を押さえる左手までを描き直した。右腕は腰の布を押さえる形を維持。
- 二つの肩からそれぞれ一本の腕へつながることを目視確認。顔・衣装・脚・剣なしの状態を維持。
- 透過を確認し、原本とゲーム用PNGのSHA-256一致。攻撃演出と戦闘処理の変更なし。

## 使用プロンプト

```text
Use case: precise-object-edit.
This is a local anatomy correction of the provided adult character sprite. The previous edit left an incorrect extra right-arm/hand shape near the chest on the character's left side (viewer RIGHT). Completely erase and reconstruct that faulty bent-arm cluster, including the existing hand at the gold brooch and the disconnected flesh-colored elbow shape beneath it.
Give her exactly TWO anatomically connected arms total:
- Preserve the existing near/right arm on viewer LEFT: shoulder down to elbow, forearm diagonally down across the abdomen, hand holding the waist cloth.
- Rebuild ONLY the far/left arm as one clearly continuous limb: from the shoulder on viewer RIGHT, upper arm down along her side to a single elbow at the right edge of her ribcage, then forearm crossing horizontally in front of the upper torso to ONE left hand resting on her opposite upper arm on viewer LEFT. The hand has natural left-hand anatomy with five digits, and one wrist connected to this forearm. This makes a clear self-covering pose. There must be no hand remaining at the gold brooch, no short detached arm below it, no third arm crossing behind the new forearm, no duplicate fingers.
Preserve the face, expression, hairstyle, costume and its coverage, standing legs, boots, torn cloth, colors, proportions, framing and original cel-shaded style. Do not add or remove clothing or add exposure. Keep the sword absent.
Full-body 1024x1536 portrait with genuine transparent RGBA background and all existing transparent gaps. No text or new objects.
```
