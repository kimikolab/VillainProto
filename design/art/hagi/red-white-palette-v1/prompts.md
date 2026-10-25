# ハギ 赤・白のアクセント配色 v1

生成日: 2026-10-25
生成方法: Codex 組み込み ImageGen
状態: 確認用。ゲーム未適用。

## 現在の確認対象

- hagi-standing-red-white-v1.png: 通常立ち絵
- hagi-battle-idle-right-red-white-v1.png: 右向き戦闘待機立ち絵

ユーザーは紫・月・花の配色案（../mystic-palette-v1/）を没とした。その画像は参照せず、新規描き起こし版（../fresh-redraw-v1/）へ戻した。元の意匠のまま、くすんだ赤い布を鮮明な赤、グレーの布を白へ変更。黒・赤・白の明暗を明確にする。衣装の柔らかさ、握り、スカート丈、鐘なし・血の色なしの銀の鎌を維持。

## standing プロンプト

参照: C:/works/VillainProto/design/art/hagi/fresh-redraw-v1/hagi-standing-fresh-v1.png

```text
Use case: identity-preserve.
COLOR-ONLY REVISION of the supplied original Hagi portrait. Keep this exact character, pose, drawing, face, hairstyle, costume cut, folds, all hands/grips and scythe geometry. Make only the two palette changes below.

1. DULL RED -> CLEAR SATURATED RED:
Recolor the existing dusty brown-red fabric lining, rear cloth reverses, braid accent and small shaft cloth wrap to a rich clean SCARLET / CARMINE RED, around #D51E32 in local color. Preserve rounded soft shading, red remains clearly red even in medium shadows, no desaturated muddy maroon. Attractive vivid accent, not neon, magenta, purple or orange.

2. GRAY CLOTH -> WHITE:
Recolor the deliberately GRAY/LIGHT-GRAY fabric panels and long gray outer cloth tails to clean WHITE fabric, approximately #F5F4F0 in local color, with gentle neutral or slightly cool-gray fold shadows. The white areas must read as actual WHITE MATERIAL and distinct high-value accents against the black outfit, rather than remaining medium gray. Preserve existing panel shapes, folds, thin antique-gold motifs and trim.
Do NOT turn the black bodice, black skirt, gloves, black stocking, boots or black hair white. Do NOT confuse gray highlights/shadows on BLACK fabric with the separate gray cloth panels. Keep the main dark clothing rich charcoal-black, with original soft natural shading.

The final coherent palette is BLACK + CLEAR RED + WHITE, with the existing restrained ANTIQUE GOLD fittings and silver scythe. Stronger value/color separation, the same original design. NO new pattern, jewelry, embroidery, crescent, flower, gemstone, moon clip, celestial symbol, purple/indigo fabric or blue/purple hair tint. Do not bring in anything from a different variant.
Preserve current same short skirt length and opaque overlapping front coverage, same one stocking and bare opposite leg, natural hair wisps and soft flexible fabric folds. No visible underwear. Preserve both hands and finger anatomy EXACTLY, bent lower arm and secure waist-level grip, straight rigid shaft in front, whole clean SILVER blade with its three tally marks. Absolutely no red/blood-like staining or reflection on scythe blade. No bells/chains/tassels added. Red on cloth only.

Polished delicate anime rendering remains soft and natural, not shinier, sharper or harder. Same 1024x1536 full-body composition, complete blade, butt, cloth and boots. Preserve genuine transparent RGBA background and all gaps; opaque figure. No background, glow, aura, floor, shadow, checkerboard, lettering or watermark.
Preserve this normal front-three-quarter portrait and eyes toward viewer.
```

## battle プロンプト

参照: C:/works/VillainProto/design/art/hagi/fresh-redraw-v1/hagi-battle-idle-right-fresh-v2.png

```text
Use case: identity-preserve.
COLOR-ONLY REVISION of the supplied original Hagi portrait. Keep this exact character, pose, drawing, face, hairstyle, costume cut, folds, all hands/grips and scythe geometry. Make only the two palette changes below.

1. DULL RED -> CLEAR SATURATED RED:
Recolor the existing dusty brown-red fabric lining, rear cloth reverses, braid accent and small shaft cloth wrap to a rich clean SCARLET / CARMINE RED, around #D51E32 in local color. Preserve rounded soft shading, red remains clearly red even in medium shadows, no desaturated muddy maroon. Attractive vivid accent, not neon, magenta, purple or orange.

2. GRAY CLOTH -> WHITE:
Recolor the deliberately GRAY/LIGHT-GRAY fabric panels and long gray outer cloth tails to clean WHITE fabric, approximately #F5F4F0 in local color, with gentle neutral or slightly cool-gray fold shadows. The white areas must read as actual WHITE MATERIAL and distinct high-value accents against the black outfit, rather than remaining medium gray. Preserve existing panel shapes, folds, thin antique-gold motifs and trim.
Do NOT turn the black bodice, black skirt, gloves, black stocking, boots or black hair white. Do NOT confuse gray highlights/shadows on BLACK fabric with the separate gray cloth panels. Keep the main dark clothing rich charcoal-black, with original soft natural shading.

The final coherent palette is BLACK + CLEAR RED + WHITE, with the existing restrained ANTIQUE GOLD fittings and silver scythe. Stronger value/color separation, the same original design. NO new pattern, jewelry, embroidery, crescent, flower, gemstone, moon clip, celestial symbol, purple/indigo fabric or blue/purple hair tint. Do not bring in anything from a different variant.
Preserve current same short skirt length and opaque overlapping front coverage, same one stocking and bare opposite leg, natural hair wisps and soft flexible fabric folds. No visible underwear. Preserve both hands and finger anatomy EXACTLY, bent lower arm and secure waist-level grip, straight rigid shaft in front, whole clean SILVER blade with its three tally marks. Absolutely no red/blood-like staining or reflection on scythe blade. No bells/chains/tassels added. Red on cloth only.

Polished delicate anime rendering remains soft and natural, not shinier, sharper or harder. Same 1024x1536 full-body composition, complete blade, butt, cloth and boots. Preserve genuine transparent RGBA background and all gaps; opaque figure. No background, glow, aura, floor, shadow, checkerboard, lettering or watermark.
Preserve this screen-right-facing battle-idle stance, head/gaze and both foot directions.
```
