# ハギ 通常・戦闘立ち絵 新規描き起こし

生成日: 2026-10-25
生成方法: Codex 組み込み ImageGen
状態: 2026-10-25 採用・ゲーム適用済み。詳細は ../integration-v1/integration.md。
戦闘絵は大鎌の穴数を修正した ../scythe-unify-v1/ の画像へ更新。現在の適用元は同フォルダの integration.md を参照。

## 確認対象

- hagi-standing-fresh-v1.png: 通常立ち絵。過去の画像を参照入力として使わず、確定した要素を文章から新規生成。
- hagi-battle-idle-right-fresh-v2.png: 右向き戦闘待機立ち絵。上記の新規通常絵をキャラクター・描画の参照にして新構図で生成。古い反復修正済み画像は入力していない。

ユーザー指摘の、反復編集で髪や布が硬くなった見え方に対して、新規描画で細い髪の束・後れ毛と、柔らかく大きな布の折り目へ戻す。基本要素（ボブ、強気な顔、黒・灰・赤・金、短いが下着が見えないスカート、片脚の黒ストッキング、銀の大鎌、鐘なし、肘を曲げた握り）は引き継ぐ。細部は新規描画に合わせて整理。

最初の戦闘候補は柄の曲がりで不採用。最終戦闘用はその画像を編集せず、新規通常絵を再び参照して生成した。

## 通常用・新規生成プロンプト

```text
Use case: stylized-concept.
Create a completely NEW, freshly drawn full-body transparent game portrait of HAGI, an original young adult female reaper. This is a new illustration from a clean start, with graceful organic drawing and SOFT HAIR AND FABRIC. One character only.

IDENTITY: youthful adult woman about 20, slender athletic 7-to-7.5-head proportions, pale natural skin, sharp silver-gray eyes, slightly lifted eyebrows and a small confident mischievous smile. Short charcoal-black tousled BOB ending at jaw/nape, airy asymmetric bangs, softly separated locks and wispy flyaway hairs, one tiny braid beside her anatomical LEFT ear with a muted crimson accent. Small ordinary human ears, no horns or elf ears. Face has a lively delicate anime expression.

SOFT, FRESH RENDERING is essential: polished Japanese fantasy RPG anime illustration, fine elegant variable-weight lines, soft clean natural color transitions, gentle cel shading with some supple painted transitions, restrained highlights. Hair is light, silky and slightly messy, individual tapered strands separate at edges; no solid helmet, sculpted chunks or hard glossy curls. Clothes have believable SOFT WOVEN FABRIC, broad rounded folds that follow gravity, relaxed edges and slight flutter. Sleeves and skirt must not look like rigid leather armor, folded sheet metal or crystalline angular facets. Metal alone has crisp hard reflections. Keep the figure readable at game size, without dense microtexture, excessive shine or jagged detail. Not 3D, photorealism or chibi.

DESIGN: black sleeveless high-neck fitted cropped bodice with tasteful angular straps and modest antique-gold fasteners; exposed shoulders and midriff. Detached black forearm sleeves with loose soft upper folds and fitted gloves, black fingerless gloves, small gold wrist cuffs. Black asymmetric layered SHORT SKIRT reaching UPPER-MID THIGH, opaque overlapping center front and side panels. Hem subtly irregular but fluid and rounded. It completely covers crotch/underwear in this standing pose: no visible panties, briefs, shorts edge or exposed wedge between thighs. One smooth black thigh-high stocking on ANATOMICAL LEFT leg, opposite right leg bare, a simple thin thigh strap, slim black ankle-to-midcalf boots with restrained gold fittings. A dark belt and one small old cemetery key. Long lightweight split back panels of charcoal and ash-gray with muted crimson lining, a few restrained antique-gold motifs, drape and flutter as soft cloth rather than hard pointed shards. Clothes are dark, matte and light, not heavy armor. Keep some airy negative space around legs and tails.

WEAPON: one imposing giant reaper SCYTHE taller than her, one very long straight slender dark cylindrical shaft and one graceful large silver crescent blade. Antique-gold openwork concentrated at blade ROOT, dark structural spine, CLEAN SILVER cutting edge and three fine tally-like etched marks. The ENTIRE blade is neutral silver/steel/black/gold with NO red on blade, no blood or stains. NO bells, dangling chains, pendants or weapon tassels. Small tightly secured red cloth wrap near shaft butt is fine. Blade silhouette elegant and clear, not excessively jagged.

POSE: normal relaxed standing portrait in a front three-quarter view, eyes toward viewer, subtle cheeky smile, weight on one leg and free knee soft. Holds scythe diagonally IN FRONT of her torso. Her RIGHT arm (screen-left) has elbow BENT OUTWARD, forearm runs toward waist, right hand firmly closes around shaft BESIDE WAIST, not down beside thigh. Upper LEFT hand on screen-right grips farther up shaft near shoulder level. Both wrists naturally align with forearms. Both closed hands physically ENCOMPASS the pole: opposing thumb and four curled fingers, palm seated against cylinder, correct occlusion, shaft disappears inside grip and re-emerges. No fingers merely laid on pole, floating fist, extended straight fingertips or extra digits. ONE ruler-straight shaft connects pommel, both grasp centers and blade mount. Face clear of weapon.

COMPOSITION / OUTPUT: fresh full-body vertical 1024x1536 illustration, eye-level camera. Entire hair, huge scythe blade and butt, cloth and both boots visible with comfortable 5% clear margins. Keep natural slim proportions and fine readable face. GENUINELY TRANSPARENT RGBA background, including all gaps, solid opaque figure and weapon. No background, gradient, aura, vignette, floor, cast shadow, checkerboard, lettering, watermark or border.
```

## 戦闘用・新規生成プロンプト

```text
Use case: stylized-concept.
Draw a FRESH right-facing battle-idle illustration of the SAME adult reaper girl shown in this newly drawn character reference. New composition with the same light silky hair and soft matte fabric. This is not an incremental repair of any earlier battle picture.

IMPORTANT WEAPON LAYOUT: preserve the reference's SAME STEEP STRAIGHT DIAGONAL scythe and same approximate locations of both hand grips. The weapon is one RIGID SPEAR-LIKE STRAIGHT SHAFT, NOT a curved staff.
In the final 1024x1536 composition its shaft runs roughly from (95,1120) at butt to (800,255) at blade mount, one exact ruler-straight centerline. Lower right-hand grip around (490,635), upper left-hand grip around (690,390). These are guides; adjust framing slightly, but every point must stay on a SINGLE LINE. Fingers and arms accommodate the straight pole; never bend or bow the pole to accommodate arms. The line extends unbroken through each hand and into blade socket. Retain exact elegant huge silver crescent/gold root design of reference, NO red on blade and NO bells/chains/tassels.

POSE:
Turn Hagi's HEAD, CHEST, gaze and both feet toward SCREEN RIGHT about 50-60 degrees from frontal, watching an offscreen enemy with a small confident grin. Keep her head clear of pole. Short grounded staggered stance, front left foot right, rear right foot left, both knees softly bent 15 degrees, hips stay high, relaxed sustainable readiness. Fit this body pose behind the fixed straight scythe, not the other way around.
Right elbow screen-left bends outward, right forearm across waist into lower grip. Left elbow naturally flexes into upper grip near shoulder. Same two real closed grasps from reference: opposing thumb and four curled fingers physically enclose shaft, shaft buried inside grip, correct finger/pole occlusion, natural wrists and elbows. Pole in FRONT of body. No low dangling right arm, no fingertips merely touching pole.

CHARACTER AND MATERIALS:
Same youthful adult face, silver-gray eyes, short tousled charcoal bob and tiny crimson-accent side braid, slender 7-7.5-head proportions. Fine airy wisps and silky separated strands, delicate changing line weight, gentle smooth anime shading, soft highlights. Clothes hang with gravity in broad curved folds, not rigid angular sheet-metal facets. Match reference's black cropped high-neck sleeveless fitted top and gold straps/fasteners; soft detached black sleeves with crimson linings, fingerless gloves/gold wrist cuffs, layered overlapping black SHORT SKIRT reaching upper-mid thigh with complete opaque front coverage (no visible underwear or crotch), same belt and cemetery key, one black thigh-high stocking on LEFT/front leg and opposite bare leg with thigh strap, slim black boots and gold fittings, flowing charcoal/ash-gray/crimson rear panels with sparse gold motifs. No stiff shiny clothing, dense noise, extra jewelry or outfit changes. Keep warm lively delicate anime expression and supple fabric.

OUTPUT: one fresh full-body vertical 1024x1536 transparent RGBA portrait, whole figure, complete large scythe and boots, safe margins. Eye-level view. No background, halo, glow, vignette, floor or cast shadow, checkerboard, lettering or watermark. Opaque character/weapon, genuinely transparent gaps.
```
