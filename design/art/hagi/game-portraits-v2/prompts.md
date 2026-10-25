# ハギ 通常・戦闘立ち絵 修正 v2

生成日: 2026-10-25
生成方法: Codex 組み込み ImageGen
状態: 確認用。ゲーム未適用。DemoApp の画像・設定には書き込んでいない。

## 確認する画像

- hagi-standing-v2.png: 通常の待機立ち絵
- hagi-battle-idle-right-v4.png: 右向きの戦闘待機立ち絵
- grip-reference.png: 画面左（本人右手）の握りを組み直すための拡大参照

## 変更

- 下側の手を、親指と残り4本の指が柄を囲む握りへ描き直し。戦闘用は元の手の形を維持せず、肘を曲げて手を腰側へ移した。
- スカート前面と側面の不透明な布を延長し、中央の裾を重ねて下着が見えない形へ変更。
- 武器の鐘・ぶら下がる鎖・重りを削除し、鎌の根元と柄尻を整理。衣装の鍵・耳飾りは保持。

## 握りの拡大参照プロンプト

```text
Use case: precise-object-edit.
Create a LARGE CLOSE-UP DRAWING of ONLY the anatomical RIGHT hand / lower forearm of the attached adult reaper character gripping her dark cylindrical scythe shaft. This is a grip-construction reference for fixing the full artwork, NOT another full-body portrait. Crop tightly to wrist, whole hand and a short straight shaft section; the hand fills most of a square canvas. Same black fingerless glove, small antique-gold wrist cuff, pale exposed fingertips, refined clean anime linework.

We need an OBVIOUSLY CLOSED HAND AROUND A POLE, with a visible thumb. The previous full artwork's lower hand does not convincingly enclose the handle, so invent a different secure grip instead of enlarging that faulty hand.
Shaft oriented from lower-left toward upper-right, rigid and straight. The RIGHT wrist enters from UPPER LEFT. Rotate forearm so the right hand is viewed from the thumb/palm side in three-quarter view. Show ONE clearly identifiable THUMB curving across the NEAR side of the shaft and pressing against the curled index finger. FOUR fingers curl around the OPPOSITE side, fingertips bend back toward the palm. Pole seats in the thumb-index web and contacts palm firmly, no gap. Thumb and index form a CLOSED ENCIRCLING LOOP around cylinder with shaft inside it. Other three fingers wrap below index sequentially. The skin thumb is visibly separate from the row of four fingers, not a fifth parallel fingertip; some fingertips can be hidden. Palm/thenar mound and thumb joint must be clear. Pole disappears inside the closed grip and emerges at top-right and bottom-left; cannot lie outside fist or run along fingertips. No extra digits, no straight dangling fingers, no mitten, no hand merely touching a pole. Correct joints and wrist without severe bend. A conventional full power grip like holding a broom handle.
One right hand only. No body, no sleeve beyond short wrist, no labels, no extra views or text. Plain light gray background for clear anatomy. Focus on readable thumb opposition and actual encirclement of the shaft.
```

## standing 修正プロンプト

```text
Use case: precise-object-edit.
Image 1 is the FULL-BODY EDIT TARGET. Image 2 is a CLOSE-UP RIGHT-HAND GRIP REFERENCE generated specifically for this correction. Use image 2 ONLY for correct thumb/finger encirclement, not as another composition. Produce one full-body transparent character portrait.

Make these THREE requested corrections together:

1. LOWER HAND ON VIEWER-LEFT — REBUILD THE GRIP:
Replace image 1's current false lower grip with the actual encircling grasp in image 2. It is her anatomical RIGHT hand. Copy the REFERENCE'S CLEAR OPPOSING THUMB, which bends across the near side and meets the curled index, plus FOUR other fingers wrapping from the far side. The thumb must be visibly distinct from the four-finger row. The shaft seats deep against the palm and thumb web; palm, thumb and fingers form a closed loop around it. No hovering fist, fingertips merely pressed flat onto shaft, five parallel fingertips or missing thumb. Do NOT preserve the old faulty hand silhouette. Adapt hand rotation to her forearm, slightly bend elbow / turn wrist if necessary to make anatomy credible; correct hand scale. Same black fingerless glove and gold cuff. Natural contact pressure and occlusion match image 2. One connected right forearm, one thumb and four fingers only.
Keep scythe shaft ONE exact rigid straight diagonal IN FRONT of torso, all segments collinear with blade socket. Move the lower grip and wrist minimally to meet this rigid shaft rather than bending the pole.

2. SKIRT — A LITTLE LONGER, ALWAYS COVERED:
Extend the short skirt's opaque black front and side panels by roughly 8-12 cm so hem reaches the UPPER-TO-MID THIGH. Keep the stylish short asymmetric layered skirt, bare legs and original agile fashion; do not make a knee-length skirt or dress.
Most important: use overlapping solid front panels to cover the CENTER of the lap/crotch completely, including the old exposed underwear/shorts wedge between thighs. Smoothly extend the skirt cloth down over that region. No visible underwear, panties, panty-shaped lines, shorts edge, crotch exposure or open slit exposing underneath. A short skirt with the suggestion of movement while the view stays covered. Keep long flowing rear tails separate and unchanged. Small side slits only below the safe front coverage. Eye-level view, no upskirt angle.

3. WEAPON — REMOVE THE BELLS:
REMOVE ALL bells hanging from the scythe blade/root and butt, their dangling metal chains, attachment loops, hanging pendants and the long hanging tassel at the blade. Remove dangling metal weights at shaft butt as well. Reconstruct a clean integrated antique-gold filigree blade root and clean balanced shaft butt. Preserve the HUGE SILVER CRESCENT BLADE, black structure, gold openwork, three tally marks and rigid shaft. A small tight red cloth wrap with short secured ends may remain; no long dangling chains, bells or tassels anywhere on weapon. The character's belt key and earrings stay; this removal is WEAPON ONLY.

PRESERVE everything else: exactly the same adult Hagi, short tousled black bob, tiny side braid, silver-gray eyes, same face and expression, same proportions, body and leg stance, upper gripping hand, black sleeveless crop bodice, exposed shoulders/waist, gold details, forearm sleeves, one black thigh-high stocking and opposite bare leg, boots, skirt palette and rear fabric, anime art style and lighting. Same full-body framing with whole scythe, cloth and boots inside frame. No new character or costume redesign.
GENUINE TRANSPARENT RGBA background including gaps, solid opaque character and weapon. No background, colored glow, floor, shadow, checkerboard, labels, text or watermark. Vertical full-body PNG matching original dimensions.
NORMAL PORTRAIT: Preserve image 1's relaxed front three-quarter pose and confident look toward viewer. Do not turn into a battle stance.
```

## battle 修正プロンプト

```text
Use case: precise-object-edit.
Image 1 is the FULL-BODY EDIT TARGET. Image 2 is a CLOSE-UP RIGHT-HAND GRIP REFERENCE generated specifically for this correction. Use image 2 ONLY for correct thumb/finger encirclement, not as another composition. Produce one full-body transparent character portrait.

Make these THREE requested corrections together:

1. LOWER HAND ON VIEWER-LEFT — REBUILD THE GRIP:
Replace image 1's current false lower grip with the actual encircling grasp in image 2. It is her anatomical RIGHT hand. Copy the REFERENCE'S CLEAR OPPOSING THUMB, which bends across the near side and meets the curled index, plus FOUR other fingers wrapping from the far side. The thumb must be visibly distinct from the four-finger row. The shaft seats deep against the palm and thumb web; palm, thumb and fingers form a closed loop around it. No hovering fist, fingertips merely pressed flat onto shaft, five parallel fingertips or missing thumb. Do NOT preserve the old faulty hand silhouette. Adapt hand rotation to her forearm, slightly bend elbow / turn wrist if necessary to make anatomy credible; correct hand scale. Same black fingerless glove and gold cuff. Natural contact pressure and occlusion match image 2. One connected right forearm, one thumb and four fingers only.
Keep scythe shaft ONE exact rigid straight diagonal IN FRONT of torso, all segments collinear with blade socket. Move the lower grip and wrist minimally to meet this rigid shaft rather than bending the pole.

2. SKIRT — A LITTLE LONGER, ALWAYS COVERED:
Extend the short skirt's opaque black front and side panels by roughly 8-12 cm so hem reaches the UPPER-TO-MID THIGH. Keep the stylish short asymmetric layered skirt, bare legs and original agile fashion; do not make a knee-length skirt or dress.
Most important: use overlapping solid front panels to cover the CENTER of the lap/crotch completely, including the old exposed underwear/shorts wedge between thighs. Smoothly extend the skirt cloth down over that region. No visible underwear, panties, panty-shaped lines, shorts edge, crotch exposure or open slit exposing underneath. A short skirt with the suggestion of movement while the view stays covered. Keep long flowing rear tails separate and unchanged. Small side slits only below the safe front coverage. Eye-level view, no upskirt angle.

3. WEAPON — REMOVE THE BELLS:
REMOVE ALL bells hanging from the scythe blade/root and butt, their dangling metal chains, attachment loops, hanging pendants and the long hanging tassel at the blade. Remove dangling metal weights at shaft butt as well. Reconstruct a clean integrated antique-gold filigree blade root and clean balanced shaft butt. Preserve the HUGE SILVER CRESCENT BLADE, black structure, gold openwork, three tally marks and rigid shaft. A small tight red cloth wrap with short secured ends may remain; no long dangling chains, bells or tassels anywhere on weapon. The character's belt key and earrings stay; this removal is WEAPON ONLY.

PRESERVE everything else: exactly the same adult Hagi, short tousled black bob, tiny side braid, silver-gray eyes, same face and expression, same proportions, body and leg stance, upper gripping hand, black sleeveless crop bodice, exposed shoulders/waist, gold details, forearm sleeves, one black thigh-high stocking and opposite bare leg, boots, skirt palette and rear fabric, anime art style and lighting. Same full-body framing with whole scythe, cloth and boots inside frame. No new character or costume redesign.
GENUINE TRANSPARENT RGBA background including gaps, solid opaque character and weapon. No background, colored glow, floor, shadow, checkerboard, labels, text or watermark. Vertical full-body PNG matching original dimensions.
BATTLE PORTRAIT: Preserve image 1's screen-RIGHT-facing combat waiting stance, rightward gaze, softly bent knees and foot directions. Do not turn face toward viewer or mirror the character.
```

## 戦闘用・手と肘の再配置プロンプト

```text
Use case: precise-object-edit.
Image 1 is the full-body right-facing Hagi edit target. Image 2 is the exact RIGHT HAND grip construction to use.
CHANGE THE LOWER HAND AND LOWER ARM POSE. Do not retain the existing faulty fist.

Move her anatomical RIGHT hand (currently viewer-left near upper thigh) about ONE HAND-LENGTH UP THE SCYTHE SHAFT TOWARD THE WAIST. Bend the right elbow outward on screen-left so the forearm enters the new hand from UPPER LEFT, matching the forearm/hand orientation in image 2. Hand ends near the left edge of her belt/waist. This is an intentional small pose change to achieve the reference's correct grip. Keep the pole fixed and straight in front of torso. Upper left hand remains at upper position. Hands stay spaced about 30-40 cm apart.

Faithfully reproduce image 2's clearly VISIBLE LARGE THUMB curving OVER THE NEAR SIDE of the pole, with the thumb's exposed tip visibly separate and OPPOSING the four fingers. Rotate the lower right palm/thenar toward the viewer as in image 2, rather than presenting the old black back-of-hand silhouette. The pole seats deep in thumb-index web and palm. Four other fingers curve from far side and close toward palm; their tips may hide. Show the actual loop of opposed thumb and fingers around cylinder. No five parallel fingertips, no old fist shape, no tiny invisible thumb. This must look like the CLOSE-UP REFERENCE shrunk to proper hand scale and naturally attached to her slightly bent arm, not pasted over the old hand. Exactly five digits total. Keep fingerless black glove and gold cuff. Proper wrist/forearm/shoulder chain. The hand overlaps and encloses the pole.

Keep ALL OTHER CURRENT CHANGES in image 1: longer black overlapping skirt completely concealing underwear, NO bells or dangling metal/weapon tassels, clean weapon root and butt. Same face and rightward gaze, black bob, tiny braid, earrings, same upper arm/hand, legs and boots, right-facing stance, black crop bodice, rear tails and palette. No change to weapon size/position/shape or torso. Do not restore short skirt, underwear or bells.
Output one same full-body 1024x1536 transparent RGBA PNG. Preserve genuine transparency and all gaps, no backdrop, floor, cast shadow, text or labels.
```
