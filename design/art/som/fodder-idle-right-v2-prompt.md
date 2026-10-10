# 背いた獣 戦闘立ち絵 v2 — 瞳孔の修正

生成: Codex 内蔵 image_gen、2026-10-10。
参照・編集元: `fodder-idle-right-v1.png`。
出力: `fodder-idle-right-v2.png`。ゲームの `DemoApp/assets/portraits/battle/fodder_idle_right.png` に適用。

v1で手前の目に瞳孔が2本あったため、両目それぞれ1本の縦長の瞳孔になるよう編集。右向き・半目の表情・衣装と体格を維持。

## 生成プロンプト

```text
Use case: precise-object-edit.
Edit this EXACT transparent cat demon battle sprite. Fix ONLY the duplicated pupils INSIDE the two existing eye openings. Preserve the exact pose, facing screen right, half-lidded aloof facial expression, eyelid outlines, face fur, body, ears, horns, collar, tail, paws, silhouette, original framing and transparent background.

The larger NEAR eye at image coordinates approximately x890..1020,y397..455 currently contains TWO separate dark vertical pupil shapes, one around x932 and one around x1004. This is an error. Redraw only its interior as ONE coherent golden amber iris with exactly ONE single narrow solid dark vertical slit pupil positioned at approximately x984,y424, looking screen right. Remove BOTH old slit shapes before drawing the single new slit. A single ordinary feline eye, NOT multiple irises. No dark vertical iris streaks or dark elongated reflections anywhere else in that eye. One small WHITE catchlight only, so it cannot resemble a second pupil.
The smaller FAR eye at approximately x1092..1134,y381..420 must likewise contain exactly ONE single amber iris and ONE single dark vertical slit pupil aligned toward screen right, no duplicated black lines. These are the cat's only TWO eyes, exactly ONE pupil per eye.
Keep golden amber iris color and partially lowered lids, same smug indifferent expression. Do not change mouth or nose or the position or shape of either eyelid. Do not redesign or repaint the rest of the sprite. Do not add effects, objects, labels or panels. Preserve all existing genuine transparency, with alpha zero outside the character, including gaps under the curled tail. Keep original square composition and full uncut character, same ground line and margins.
```
