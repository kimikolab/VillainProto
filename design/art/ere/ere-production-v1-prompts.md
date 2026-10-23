# 哀歌のエレ 正面・戦闘立ち絵 v1

2026-10-23。内蔵 image_gen で各画像を生成。
採用デザイン: ere-concept-v2.png（葬列を歩く吟遊詩人）。
ユーザー指示: 待機立ち絵と戦闘中立ち絵を作り、ゲームへの適用は確認後に行う。
状態: ユーザーの適用指示を受け、2026-10-23にDemoAppへ採用。適用先・表示調整・実画面確認は demo-integration.md を参照。

## 保存画像と検証

- ere-front-v1.png: 待機画面用の正面立ち絵。
- ere-idle-right-v1.png: 右向きの戦闘待機立ち絵。
- 両画像は1024×1536、RGBA透過PNG。生成時のアルファを加工せず保持し、保存画像は原本とSHA-256一致。
- 両画像の四隅と、人物外側の背景サンプル5点はalpha=0。
- 帽子、ヴェール、指、楽器の先端、裾、両靴底の収まりを目視確認。
- alpha>=128の領域（左・上・右exclusive・下exclusive）: 正面 (121, 8, 964, 1517)、戦闘 (105, 10, 1001, 1510)。四辺に接触なし。
- 表情、編み髪、喪装、配色、リュートの共通性と、戦闘絵が右を向いていることを確認。

## 正面用プロンプト

Use case: identity-preserve.
Asset type: production full-body standing portrait for the Japanese fantasy strategy game VillainProto, for user review before any game integration.
The supplied Ere v2 concept is the AUTHORITATIVE APPROVED CHARACTER DESIGN. Preserve this SAME adult human woman, apparent age around thirty, mature elegant anime face, narrow plum-brown eyes, pale warm skin, dark chestnut hair in a thick loose side braid with loose face-framing strands, small soft charcoal traveling cap, and long dark mourning veil flowing behind her. Same calm melancholy and quiet inner strength. No childlike face. Ordinary rounded human ears.
Preserve the EXACT APPROVED OUTFIT AND COLORS: short charcoal shoulder cape with subdued antique-gold trim and dark wine lining; fitted dark wine-purple bodice with silver clasps; warm ivory gathered long sleeves and flowing cuffs; layered charcoal, muted burgundy and ivory long skirt with the approved asymmetric leg opening and simple thigh strap; narrow leather belts, small brown leather belt pouch, modest silver pendant details; dark-brown knee-high leather traveling boots with low sturdy heels. Same body proportions, chest size, trim patterns, dress silhouette and material style. Do not redesign, recolor, add new jewelry, new costume elements or change into another concept variant.
Instrument invariant: SAME warm wooden medieval lute, pear-shaped soundboard, detailed carved circular rosette, short fretted neck, bent-back pegbox and visible strings, supported at her waist by the same brown leather shoulder strap; same narrow dark wine and charcoal mourning ribbons tied near the pegbox. Character's anatomical LEFT hand frets the neck, anatomical RIGHT hand poised lightly at the soundboard strings. Keep instrument construction, string direction and hand placement coherent. Never obscure the face with the lute.
Style: EXACTLY the approved clean anime illustration, fine dark contour lines, softly blended controlled cel shading, clear fabric planes and tidy selective embroidery. Keep mature feminine appeal and readable shapes at small game sizes. No photorealism, gritty texture, heavy brown wash or painterly realism.
Output ONE character on a GENUINELY TRANSPARENT RGBA background. Empty area must have alpha zero, including all open spaces between arms, lute, skirt and legs. Retain sensible semitransparency only on the sheer mourning veil, keep body, clothes and lute solid. Absolutely no colored backdrop, vignette, painted checkerboard, ground plane, cast shadow, fog, glow, music notes or effects. Portrait format approximately 1024x1536. Full cap, complete veil, entire lute including all tuning pegs and ribbon ends, all fingers, complete skirt hems and BOTH boot soles inside the image, with a comfortable safety margin of at least a few percent all around. Balanced composition, no cropping. No text, watermark, border, inset image, extra characters, scenery, weapons or magic.

FRONT STANDBY / FORMATION SCREEN PORTRAIT:
Repose her into a nearly straight-on frontal relaxed standing pose. Head, shoulders, chest and hips face the viewer, eyes directed softly toward the viewer. Face fully readable, quiet composed melancholy, mouth gently closed, relaxed shoulders and a natural slight shift of weight. Boots planted in a stable comfortable stance, toes mostly forward. Hold the lute naturally diagonally across her waist, lute neck rising toward screen right, both hands gently resting in a ready-to-play position. This is a calm menu standby portrait, no dramatic singing or wind. Veil and skirt settle naturally with a few gentle folds. Maintain the full mature adult design from the concept. Redraw the pose as frontal rather than simply extracting the concept background.

## 戦闘用プロンプト

参照順序: 1. ere-concept-v2.png（採用デザイン）、2. ere-front-v1.png（正面用原本、同一人物・衣装・仕上げの照合）。

Use case: identity-preserve.
Asset type: production full-body standing portrait for the Japanese fantasy strategy game VillainProto, for user review before any game integration.
The supplied Ere v2 concept is the AUTHORITATIVE APPROVED CHARACTER DESIGN. Preserve this SAME adult human woman, apparent age around thirty, mature elegant anime face, narrow plum-brown eyes, pale warm skin, dark chestnut hair in a thick loose side braid with loose face-framing strands, small soft charcoal traveling cap, and long dark mourning veil flowing behind her. Same calm melancholy and quiet inner strength. No childlike face. Ordinary rounded human ears.
Preserve the EXACT APPROVED OUTFIT AND COLORS: short charcoal shoulder cape with subdued antique-gold trim and dark wine lining; fitted dark wine-purple bodice with silver clasps; warm ivory gathered long sleeves and flowing cuffs; layered charcoal, muted burgundy and ivory long skirt with the approved asymmetric leg opening and simple thigh strap; narrow leather belts, small brown leather belt pouch, modest silver pendant details; dark-brown knee-high leather traveling boots with low sturdy heels. Same body proportions, chest size, trim patterns, dress silhouette and material style. Do not redesign, recolor, add new jewelry, new costume elements or change into another concept variant.
Instrument invariant: SAME warm wooden medieval lute, pear-shaped soundboard, detailed carved circular rosette, short fretted neck, bent-back pegbox and visible strings, supported at her waist by the same brown leather shoulder strap; same narrow dark wine and charcoal mourning ribbons tied near the pegbox. Character's anatomical LEFT hand frets the neck, anatomical RIGHT hand poised lightly at the soundboard strings. Keep instrument construction, string direction and hand placement coherent. Never obscure the face with the lute.
Style: EXACTLY the approved clean anime illustration, fine dark contour lines, softly blended controlled cel shading, clear fabric planes and tidy selective embroidery. Keep mature feminine appeal and readable shapes at small game sizes. No photorealism, gritty texture, heavy brown wash or painterly realism.
Output ONE character on a GENUINELY TRANSPARENT RGBA background. Empty area must have alpha zero, including all open spaces between arms, lute, skirt and legs. Retain sensible semitransparency only on the sheer mourning veil, keep body, clothes and lute solid. Absolutely no colored backdrop, vignette, painted checkerboard, ground plane, cast shadow, fog, glow, music notes or effects. Portrait format approximately 1024x1536. Full cap, complete veil, entire lute including all tuning pegs and ribbon ends, all fingers, complete skirt hems and BOTH boot soles inside the image, with a comfortable safety margin of at least a few percent all around. Balanced composition, no cropping. No text, watermark, border, inset image, extra characters, scenery, weapons or magic.

Input roles: Image 1 is the authoritative approved Ere v2 concept for design. Image 2 is the newly made frontal production portrait of the SAME woman for exact identity, costume and finish consistency.
RIGHT-FACING BATTLE IDLE:
ROTATE THE WHOLE CHARACTER into a clear three-quarter side view facing SCREEN RIGHT. Her nose, gaze, shoulders, chest, pelvis, knees and boot toes all point toward the unseen enemies on screen right. Do not leave the torso facing the viewer. Show the recognizable adult face in 3/4 profile looking right, calm focused eyes, a quiet slightly parted mouth as she prepares to sing. Body is upright with a slight intent forward inclination, both boots planted and modestly staggered, knees relaxed, a sustainable ready-to-play idle pose rather than an attack or dance.
The SAME lute is supported snugly at her waist on its shoulder strap; its body lies naturally against her torso and neck extends forward/upward toward screen right in coherent three-quarter perspective. Anatomical left hand frets the neck, right hand hovers over strings ready to pluck, graceful relaxed fingers. Both hands and the wooden soundboard remain readable. The mourning veil and long skirt fall naturally behind toward screen left without theatrical wind. The cap, braid, garment layering and leg opening remain the SAME outfit rotated in space, no redesign, extra drapery or exposed areas.
This is a reusable standing battlefield sprite. No magical aura, singing wave, notes, attack motion blur or dramatic lunge. Preserve transparent empty pixels; all surrounding darkness or gradients in references are NOT part of the character and must be completely removed. Full figure, both soles, entire lute and trailing ribbons inside the canvas with safe margins.
