# ハギ 鮮やかな赤・グレー 配色 v1

生成日: 2026-10-25
生成方法: Codex 組み込み ImageGen
状態: 確認用。ゲーム未適用。

## 現在の確認対象

- hagi-standing-red-gray-v1.png: 通常立ち絵
- hagi-battle-idle-right-red-gray-v1.png: 右向き戦闘待機立ち絵

ユーザーの「白部分はグレーで」に合わせ、../red-white-palette-v1/ の白い布だけを中間のアッシュグレーへ戻した。鮮やかな赤は維持。鎌の刃は銀、基本の衣装は黒のまま。紫・月の配色案は没のまま。ゲームの画像・設定は変更していない。

## standing プロンプト

参照: C:/works/VillainProto/design/art/hagi/red-white-palette-v1/hagi-standing-red-white-v1.png

```text
Use case: precise-object-edit.
Make ONE COLOR-ONLY correction to the supplied Hagi portrait: change the WHITE FABRIC PANELS back to medium ASH GRAY, while KEEPING the vivid clean RED accents exactly as they are.

ONLY EDIT: the currently white flowing cloth strips, long white rear panels and white cloth inside/around the detached sleeves. Their local material color should be a clearly MEDIUM NEUTRAL ASH GRAY, around #959299, with natural darker gray fold shadows and softer light-gray highlights. It should read as gray woven cloth again, not white or pale lavender. Preserve exactly the shapes, soft rounded folds, linework and all gold trim on these panels.
Do not change white/silver metal on the SCYTHE BLADE: that stays clean SILVER. Do not recolor skin, eyes, highlights in hair, or metal. Do not turn black bodice/skirt/gloves/stocking/boots gray.

KEEP ALL CURRENT SATURATED RED: existing scarlet/crimson linings, rear cloth reverses, braid accent and small shaft cloth wrap remain just as vivid and clear as the input. Do not mute them back to dusty brown-red.
Everything else identical: same adult woman, face/expression, airy dark bob and tiny braid, clothing cut and approved skirt length/opaque coverage, both hand grips and fingers, bent waist-level lower arm, straight shaft in front, silver crescent and gold root design, tally marks, key, boots, exact pose/feet/gaze, whole-body framing and soft natural anime rendering. No new motif, ornament, purple color, moon accessory, bell, chain or blade stain. No pose or material/shininess change.
Same vertical 1024x1536 full-body portrait, full weapon and boots. Preserve genuine transparent RGBA background and all gaps, opaque figure. No background, halo, aura, floor, shadow, checkerboard, text or watermark.
```

## battle プロンプト

参照: C:/works/VillainProto/design/art/hagi/red-white-palette-v1/hagi-battle-idle-right-red-white-v1.png

```text
Use case: precise-object-edit.
Make ONE COLOR-ONLY correction to the supplied Hagi portrait: change the WHITE FABRIC PANELS back to medium ASH GRAY, while KEEPING the vivid clean RED accents exactly as they are.

ONLY EDIT: the currently white flowing cloth strips, long white rear panels and white cloth inside/around the detached sleeves. Their local material color should be a clearly MEDIUM NEUTRAL ASH GRAY, around #959299, with natural darker gray fold shadows and softer light-gray highlights. It should read as gray woven cloth again, not white or pale lavender. Preserve exactly the shapes, soft rounded folds, linework and all gold trim on these panels.
Do not change white/silver metal on the SCYTHE BLADE: that stays clean SILVER. Do not recolor skin, eyes, highlights in hair, or metal. Do not turn black bodice/skirt/gloves/stocking/boots gray.

KEEP ALL CURRENT SATURATED RED: existing scarlet/crimson linings, rear cloth reverses, braid accent and small shaft cloth wrap remain just as vivid and clear as the input. Do not mute them back to dusty brown-red.
Everything else identical: same adult woman, face/expression, airy dark bob and tiny braid, clothing cut and approved skirt length/opaque coverage, both hand grips and fingers, bent waist-level lower arm, straight shaft in front, silver crescent and gold root design, tally marks, key, boots, exact pose/feet/gaze, whole-body framing and soft natural anime rendering. No new motif, ornament, purple color, moon accessory, bell, chain or blade stain. No pose or material/shininess change.
Same vertical 1024x1536 full-body portrait, full weapon and boots. Preserve genuine transparent RGBA background and all gaps, opaque figure. No background, halo, aura, floor, shadow, checkerboard, text or watermark.
```
