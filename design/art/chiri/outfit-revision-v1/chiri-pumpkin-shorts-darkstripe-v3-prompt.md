# チリ — オレンジ×チャコールの膝上ソックス v3

**採用記録:** この通常絵と同じ衣装をゲームの通常・戦闘・行動差分へ適用済み。詳細は [game-portraits-v2](../game-portraits-v2/README.md)。以下は候補生成時点の記録。

2026-11-01。内蔵image_genで局所編集（CLI不使用）。

編集元: chiri-pumpkin-shorts-overknee-v2.png
出力: chiri-pumpkin-shorts-darkstripe-v3.png
依頼: ソックスの白い部分を黒系にして、オレンジ×ダークの縞模様にする。

両脚の白い縞をチャコールへ変更。膝上丈・かぼちゃショートパンツなどを維持する方針。生成時の透過を維持して保存。ゲーム内配置は未変更。

## プロンプト全文

```text
Use case: precise-object-edit.
Image 1 is the exact Chiri pumpkin shorts and over-the-knee striped socks portrait EDIT TARGET.
Make ONLY this localized color edit on BOTH socks: replace every WHITE / CREAM horizontal stripe with a deep warm CHARCOAL BLACK / very dark soot-gray cloth color, roughly #29262c. Keep ALL existing rusty pumpkin-ORANGE stripes unchanged. The socks must now alternate ORANGE and DARK CHARCOAL horizontal stripes all the way from above the knees down into the boots. Preserve the exact stripe positions, widths, spacing, curved perspective, soft knit texture, natural shading, knee bends, sock tops and current over-the-knee height. Keep soft dimensional highlights on the dark stripes so they still read as fabric, not a flat painted black patch. Remove the pale cream stripes from the SOCKS only, including the low stripes just above both boots.
Preserve absolutely everything outside the former light sock stripes: same face and cheerful expression, hair, teal eyes, skin and bare thighs, costume, short charcoal-and-orange pumpkin shorts, cape, orange scarf, ivory blouse, gloves, satchel and bombs, gold details, striped sock ORANGE parts, leg shape/pose, boots, entire composition, camera angle, scale, canvas and margins. Keep all CREAM/WHITE parts of the BLOUSE and other clothing unchanged; do not darken them. Do not add any new patterns, accessories, texture or effects. No redesign, no change to sock length.
Retain genuine transparent RGBA background including gaps, fully opaque character interior, same 1024x1536 portrait dimensions, no background, no floor or shadow, no text or watermark. One full-body figure, exact same framing and pose.
```
