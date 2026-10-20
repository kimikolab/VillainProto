# 女海賊・散弾砲筒版 — 正面・戦闘立ち絵 v1

生成: 内蔵 image_gen（2026-10-20）。
基準: `gare-pirate-scattergun-concept-v1.png`。
用途: 正面待機画面と右向き戦闘待機の静止画。
作成時はユーザー指定により画像のみを保存。その後、2026-10-20の適用指示でDemoAppのガレに採用。
現在の適用先と実画面確認は `gare-pirate-demo-integration.md` の「散弾砲筒版への差し替え」を参照。

## 保存画像

- 正面待機: `gare-pirate-scattergun-front-v1.png`
- 右向き戦闘待機: `gare-pirate-scattergun-idle-right-v1.png`

両方1024×1536のRGBA透過PNG。生成アルファを加工せず保持し、生成原本と保存画像のSHA-256一致を確認。
顔・赤髪・強い目つき・ミニスカート・長い海賊コートを維持。短い散弾砲筒を両手で支え、砲口と腰袋に拾った破片を描く。
発砲・煙・飛散する弾・術のエフェクトは焼き込まない。

## 画像確認

両方の四隅のalpha=0。alpha 128以上の画素が四辺に接していないことと、帽子・砲口・銃床・靴底の欠けがないことを確認。

| 画像 | alpha 128以上の領域（左・上・右exclusive・下exclusive） | 下端余白 |
|---|---|---:|
| 正面 | 20, 11, 1009, 1514 | 22px |
| 戦闘右向き | 26, 68, 1008, 1505 | 31px |

採用時は表示高2.65を維持し、戦闘絵の足元余白率を `31 / 1536` に変更。実際の編成・戦闘画面で表示を確認。

## 正面待機プロンプト

Use case: identity-preserve. Production full-body standing character illustration for VillainProto. The supplied image is the AUTHORITATIVE approved pirate SCATTERGUN concept. Preserve EXACTLY the same adult HUMAN woman around 30: mature face, strong narrowed amber eyes, confident challenging smirk, long wavy red-auburn hair, black tricorn captain hat with worn brass edging, natural adult body proportions and skin tone. Preserve the exact outfit: weathered open black/navy long pirate coat and flowing tails with antique-gold trim and brass buttons, rolled sleeves and ivory cuffs, low-neckline ivory blouse, cropped fitted brown leather bodice, narrow bare midriff, burgundy sash and broad belt, layered burgundy MINI skirt with short ivory edging, bare thighs and knee-high brown leather boots with broad low heels. Keep the SAME two small open leather hip pouches full of collected silver-blue mirror fragments. No redesign or changed exposure.
Weapon invariant: retain the same SHORT STOCKED SCATTERGUN / BLUNDERBUSS-LIKE CANNON TUBE, held in BOTH hands across waist-to-lower-chest height. Chunky wooden gunstock, wooden fore-end cradle, blackened single iron barrel, reinforcing hoops, leather bindings, wide flared trumpet muzzle and visible SMALL reflective silver-blue mirror fragments just inside the bore. One hand naturally grips the rear stock at the trigger, other supports the barrel fore-end. Do not replace it with a huge long shoulder-carried naval cannon or a modern shotgun. No extra weapons, magazine, cartridges, pump mechanism, double barrels or scope. Character's face and hat unobscured; two arms and two hands with plausible fingers and support.
Style: same fine Japanese anime outlines, restrained softly painted cel shading, muted burgundy/ivory/navy palette and worn wood, cloth, leather and iron. ONE full-body character only. Genuine TRANSPARENT RGBA cutout, all empty space alpha zero including gaps between limbs, coat tails and gun; opaque skin, garments and equipment. NO colored backdrop, floor, ground shadow, vignette, checkerboard painting, scenery, text, watermark or panels. No muzzle flash, smoke, firing, loose projectiles, magical effects or floating particles. Portrait approximately 1024x1536; complete hat, all hair and coat tails, BOTH boot soles and the entire scattergun muzzle and wooden stock fully within frame with comfortable transparent margins. Reusable sustained standing idle posture, not attack, running, lunging or pinup. FRONT STANDBY / formation screen portrait: redraw into a nearly straight-on FRONT-FACING pose, head, chest and hips toward viewer. Both feet firmly planted in a comfortable moderate stance. Eyes near viewer, proud calm captain attitude. The scattergun held at waist level on a slight downward diagonal toward SCREEN RIGHT, rather than aimed straight at camera, keeps face, blouse and silhouette readable. Both hands remain on gun. Do not simply reuse the three-quarter original concept pose; make this a clear selection-screen frontal standing portrait.

## 右向き戦闘待機プロンプト

Use case: identity-preserve. Production full-body standing character illustration for VillainProto. The supplied image is the AUTHORITATIVE approved pirate SCATTERGUN concept. Preserve EXACTLY the same adult HUMAN woman around 30: mature face, strong narrowed amber eyes, confident challenging smirk, long wavy red-auburn hair, black tricorn captain hat with worn brass edging, natural adult body proportions and skin tone. Preserve the exact outfit: weathered open black/navy long pirate coat and flowing tails with antique-gold trim and brass buttons, rolled sleeves and ivory cuffs, low-neckline ivory blouse, cropped fitted brown leather bodice, narrow bare midriff, burgundy sash and broad belt, layered burgundy MINI skirt with short ivory edging, bare thighs and knee-high brown leather boots with broad low heels. Keep the SAME two small open leather hip pouches full of collected silver-blue mirror fragments. No redesign or changed exposure.
Weapon invariant: retain the same SHORT STOCKED SCATTERGUN / BLUNDERBUSS-LIKE CANNON TUBE, held in BOTH hands across waist-to-lower-chest height. Chunky wooden gunstock, wooden fore-end cradle, blackened single iron barrel, reinforcing hoops, leather bindings, wide flared trumpet muzzle and visible SMALL reflective silver-blue mirror fragments just inside the bore. One hand naturally grips the rear stock at the trigger, other supports the barrel fore-end. Do not replace it with a huge long shoulder-carried naval cannon or a modern shotgun. No extra weapons, magazine, cartridges, pump mechanism, double barrels or scope. Character's face and hat unobscured; two arms and two hands with plausible fingers and support.
Style: same fine Japanese anime outlines, restrained softly painted cel shading, muted burgundy/ivory/navy palette and worn wood, cloth, leather and iron. ONE full-body character only. Genuine TRANSPARENT RGBA cutout, all empty space alpha zero including gaps between limbs, coat tails and gun; opaque skin, garments and equipment. NO colored backdrop, floor, ground shadow, vignette, checkerboard painting, scenery, text, watermark or panels. No muzzle flash, smoke, firing, loose projectiles, magical effects or floating particles. Portrait approximately 1024x1536; complete hat, all hair and coat tails, BOTH boot soles and the entire scattergun muzzle and wooden stock fully within frame with comfortable transparent margins. Reusable sustained standing idle posture, not attack, running, lunging or pinup. RIGHT-FACING BATTLE IDLE: rotate the ENTIRE body to a clear three-quarter SIDE view facing SCREEN RIGHT. Nose, eyes, chest, hips, knees and boots all oriented toward enemies on the right, NOT at viewer, NOT left. Preserve recognizable mature face in three-quarter profile, strong eyes and slight confident grin. Both boots planted and modestly staggered with knees lightly bent, balanced and ready for recoil but not currently firing. The scattergun held in BOTH hands across waist-to-lower-chest, muzzle pointing horizontally or very slightly upward SCREEN RIGHT, viewed predominantly from the side with a little muzzle interior visible. Stock rests near rear hip/ribs at screen left, fore-end supported forward toward screen right. Keep weapon substantial but the same shorter portable stocked scattergun, never a shoulder-mounted naval cannon. Long coat and red hair fall naturally behind toward screen left, hip shard pouches remain. Full muzzle and stock inside frame. A stationary combat-ready stance, no recoil or attack effect.
