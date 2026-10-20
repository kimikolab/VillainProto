# 女海賊キャプテン — 正面・戦闘立ち絵 v1

生成: 内蔵 image_gen（2026-10-20）。
採用基準: `gare-pirate-captain-concept-v2.png`。
用途: 待機画面用の正面立ち絵と、右向き戦闘待機の静止画。
作成時はユーザー指定により画像のみを保存。その後、2026-10-20の差し替え指示でDemoAppのガレに採用。
適用先・表示調整・実画面確認は `gare-pirate-demo-integration.md` を参照。

## 保存画像

- 正面待機: `gare-pirate-captain-front-v1.png`
- 右向き戦闘待機: `gare-pirate-captain-idle-right-v1.png`

両方1024×1536のRGBA透過PNG。生成時のアルファを加工せず保持。生成原本と保存画像のSHA-256一致。
顔・赤髪・強い目つき・ミニスカート・長い海賊コート・肩に担ぐ巨大砲をコンセプト v2 に合わせる。
正面は顔・胸・腰を正面寄りにし、戦闘絵は敵側の画面右へ顔と身体を向ける。
発砲・煙・術のエフェクトは焼き込まない。

## 画像検証

両画像は四隅のalpha=0。alpha 128以上の画素は画像の四辺に接しておらず、武器・帽子・靴底の欠けがないことを目視確認。

| 画像 | alpha 128以上の領域（左・上・右exclusive・下exclusive） | 下端余白 |
|---|---|---:|
| 正面 | 24, 10, 1017, 1511 | 25px |
| 戦闘右向き | 14, 18, 1021, 1516 | 20px |

ゲーム採用時の足元余白率は `20 / 1536`、表示高は成人女性のキャプテンとして `2.65` に設定し、実画面で確認。

## 正面待機プロンプト

Use case: identity-preserve. Production game-ready standing character illustration for VillainProto. The supplied image is the AUTHORITATIVE APPROVED female pirate captain v2, not loose style inspiration. Preserve EXACTLY the same adult human woman around 30, confident commanding expression, narrowed strong amber eyes, red-auburn wavy long hair, black tricorn with brass trim, natural adult body proportions, same skin tone and same chest size. Preserve her costume: long weathered open black/navy pirate coat with antique-gold trim and brass buttons, rolled sleeves, ivory low-neckline blouse, cropped fitted brown leather bodice, narrow exposed midriff, burgundy sash and broad leather belt, layered burgundy MINI skirt with short ivory edging, bare thighs and sturdy knee-high brown leather boots with broad low heels. Keep the skirt mini length and fully covering pelvis, and the long coat tails framing the legs. Preserve the enormous thick iron naval hand cannon with wooden cradle, reinforcing hoops, leather fittings and huge flared muzzle, resting convincingly across ONE shoulder with one hand holding its grip/underside near that shoulder. Other hand rests confidently on her hip. Normal rounded human ears, no goblin characteristics. Same fine Japanese anime linework, softly painted restrained cel shading, muted weathered fabrics and material texture. No redesign, additional weapons, pistol, sword, loose particles, muzzle flash, blast smoke or firing effect.
Output: ONE character on a GENUINE TRANSPARENT RGBA background. Empty pixels alpha zero including gaps between legs, arms, coat and weapon; opaque character materials. NO gray studio backdrop, vignette, painted checkerboard, floor, ground plane or cast shadow. Portrait composition approximately 1024x1536. Entire hat, cannon, coat tails, fingertips, legs and BOTH boot soles fully within safe margins: reserve space around the entire weapon so the muzzle or rear stock is not cropped. No panel, typography, watermark, extra characters or scene. Full-body sustained STANDING pose, not firing, lunging, running or pinup. FRONT STANDBY / selection portrait: redraw her in a nearly straight-on frontal standing pose, head, chest and hips toward viewer. Face fully readable, chin lifted slightly, proud captain's look directed near viewer. Feet planted with a comfortable moderate spread, weight shifted naturally. Keep the massive cannon carried across the shoulder with its muzzle rising diagonally toward screen right, clearly above the head so it cannot obscure her face or hat. Other hand at the hip. Full mini skirt and long outer coat remain readable. Compact balanced silhouette suited to the formation menu; do not merely copy the original three-quarter concept pose.

## 右向き戦闘待機プロンプト

Use case: identity-preserve. Production game-ready standing character illustration for VillainProto. The supplied image is the AUTHORITATIVE APPROVED female pirate captain v2, not loose style inspiration. Preserve EXACTLY the same adult human woman around 30, confident commanding expression, narrowed strong amber eyes, red-auburn wavy long hair, black tricorn with brass trim, natural adult body proportions, same skin tone and same chest size. Preserve her costume: long weathered open black/navy pirate coat with antique-gold trim and brass buttons, rolled sleeves, ivory low-neckline blouse, cropped fitted brown leather bodice, narrow exposed midriff, burgundy sash and broad leather belt, layered burgundy MINI skirt with short ivory edging, bare thighs and sturdy knee-high brown leather boots with broad low heels. Keep the skirt mini length and fully covering pelvis, and the long coat tails framing the legs. Preserve the enormous thick iron naval hand cannon with wooden cradle, reinforcing hoops, leather fittings and huge flared muzzle, resting convincingly across ONE shoulder with one hand holding its grip/underside near that shoulder. Other hand rests confidently on her hip. Normal rounded human ears, no goblin characteristics. Same fine Japanese anime linework, softly painted restrained cel shading, muted weathered fabrics and material texture. No redesign, additional weapons, pistol, sword, loose particles, muzzle flash, blast smoke or firing effect.
Output: ONE character on a GENUINE TRANSPARENT RGBA background. Empty pixels alpha zero including gaps between legs, arms, coat and weapon; opaque character materials. NO gray studio backdrop, vignette, painted checkerboard, floor, ground plane or cast shadow. Portrait composition approximately 1024x1536. Entire hat, cannon, coat tails, fingertips, legs and BOTH boot soles fully within safe margins: reserve space around the entire weapon so the muzzle or rear stock is not cropped. No panel, typography, watermark, extra characters or scene. Full-body sustained STANDING pose, not firing, lunging, running or pinup. RIGHT-FACING BATTLE IDLE: ROTATE THE WHOLE BODY to a clear three-quarter side view facing SCREEN RIGHT. Nose, gaze, chest, hips, knees and boots oriented toward the right-side enemies, NOT toward the viewer and NOT left. Keep recognizable face in three-quarter profile, strong narrowed eyes and confident smirk. Both boots planted and modestly staggered, slight braced bend in knees to balance the heavy cannon. Carry the SAME giant cannon firmly on the shoulder, barrel running from rear on screen left to muzzle on screen right, angled modestly upward and seen in side-three-quarter perspective. Entire flared muzzle inside the image. Her hand grips near the shoulder, other hand on hip, no firing or recoil. Long outer coat tails and red hair fall naturally behind toward screen left; preserve mini skirt, bare thighs, midriff and exact costume. This is a reusable idle combat sprite, not an attack frame. Preserve approved identity and reconstruct all equipment coherently in the rotated pose.
