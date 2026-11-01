# チリ 新衣装のゲーム適用 — 生成プロンプト

2026-11-01。内蔵image_genによる編集（CLI不使用）。
通常用は承認済みの outfit-revision-v1/chiri-pumpkin-shorts-darkstripe-v3.png を無加工コピー。
戦闘待機と仕掛けるは旧の各ポーズを編集対象、上記通常絵を衣装の参照として使用。
手を離すは、新しい仕掛ける差分を編集対象として手元の爆弾だけを除去。

## idle

```text
Use case: precise-object-edit / identity-preserve.
Asset type: a production battle portrait costume revision for Chiri, to match the approved normal portrait.
Image 1 is the existing RIGHT-FACING BATTLE POSE EDIT TARGET. Image 2 is the APPROVED UPDATED COSTUME REFERENCE, used ONLY for the shorts and socks design. Preserve image 1's exact pose, orientation, face/head position, cheerful expression, arm/hand arrangement, bag placement, anatomy and overall canvas.
CHANGE ONLY THE LOWER OUTFIT to exactly match image 2:
1. Replace ALL of image 1's loose below-knee charcoal culotte trousers with the approved compact, feminine, slightly rounded PUMPKIN SHORT SHORTS. Same charcoal main fabric, pumpkin-orange inset side panels/vertical seams, cream X-shaped repair stitches and narrow subtly scalloped charcoal leg cuffs with tiny brass detail. The hem ends on the upper/middle thigh, never at knees. Keep the current waist belt and orange ties. This is one coherent pair of short shorts, not skirt, long trousers, loose long underwear or lingerie.
2. Extend both original socks to OVER THE KNEE and recolor their pale stripes to warm CHARCOAL BLACK. Exact approved alternating RUST/PUMPKIN ORANGE and DARK CHARCOAL horizontal stripes all the way up. Both kneecaps covered; tops sit on the lower/middle thigh and a modest clear bare-thigh gap remains between shorts and socks, matching image 2. Preserve soft opaque knitted fabric, same stripe rhythm and width relative to legs, natural wrap/perspective around knee bends, and seamless sock entry into the original boots. No white/cream stripes on socks. Simple knit top band, no garters or extra decorations.
Keep image 1's upper body, costume above the waistband, ivory blouse, cape, orange scarf, low blond ponytails, teal eyes, cheek soot, gloves, bombs, satchel, brass star charm and tassel, and boots as identical as possible. Preserve the original legs' locations and planted stance; reconstruct the bare thighs and sock-covered knees naturally where the old trouser tubes were. Do not copy image 2's frontal pose, do not change her body shape or adult proportions.
Same textured fine anime linework and painterly cel shading as image 1. 1024x1536 portrait with exact same framing/scale/margins, entire figure, boots and all props visible. Keep a genuine transparent RGBA background and internal gaps. Opaque figure with clean antialiasing, no backdrop, no halo, no floor, no cast shadow, no smoke, no sparks, no flames, no motion blur, no text or labels. ONE character only.
POSE INVARIANT: preserve image 1's steady right-facing waiting stance with bent forward elbow supporting one black bomb at waist/chest height and rear gloved hand resting at the satchel. Same face looking to screen right, same feet. Change the shorts and socks only.
```

## plant

```text
Use case: precise-object-edit / identity-preserve.
Asset type: a production battle portrait costume revision for Chiri, to match the approved normal portrait.
Image 1 is the existing RIGHT-FACING BATTLE POSE EDIT TARGET. Image 2 is the APPROVED UPDATED COSTUME REFERENCE, used ONLY for the shorts and socks design. Preserve image 1's exact pose, orientation, face/head position, cheerful expression, arm/hand arrangement, bag placement, anatomy and overall canvas.
CHANGE ONLY THE LOWER OUTFIT to exactly match image 2:
1. Replace ALL of image 1's loose below-knee charcoal culotte trousers with the approved compact, feminine, slightly rounded PUMPKIN SHORT SHORTS. Same charcoal main fabric, pumpkin-orange inset side panels/vertical seams, cream X-shaped repair stitches and narrow subtly scalloped charcoal leg cuffs with tiny brass detail. The hem ends on the upper/middle thigh, never at knees. Keep the current waist belt and orange ties. This is one coherent pair of short shorts, not skirt, long trousers, loose long underwear or lingerie.
2. Extend both original socks to OVER THE KNEE and recolor their pale stripes to warm CHARCOAL BLACK. Exact approved alternating RUST/PUMPKIN ORANGE and DARK CHARCOAL horizontal stripes all the way up. Both kneecaps covered; tops sit on the lower/middle thigh and a modest clear bare-thigh gap remains between shorts and socks, matching image 2. Preserve soft opaque knitted fabric, same stripe rhythm and width relative to legs, natural wrap/perspective around knee bends, and seamless sock entry into the original boots. No white/cream stripes on socks. Simple knit top band, no garters or extra decorations.
Keep image 1's upper body, costume above the waistband, ivory blouse, cape, orange scarf, low blond ponytails, teal eyes, cheek soot, gloves, bombs, satchel, brass star charm and tassel, and boots as identical as possible. Preserve the original legs' locations and planted stance; reconstruct the bare thighs and sock-covered knees naturally where the old trouser tubes were. Do not copy image 2's frontal pose, do not change her body shape or adult proportions.
Same textured fine anime linework and painterly cel shading as image 1. 1024x1536 portrait with exact same framing/scale/margins, entire figure, boots and all props visible. Keep a genuine transparent RGBA background and internal gaps. Opaque figure with clean antialiasing, no backdrop, no halo, no floor, no cast shadow, no smoke, no sparks, no flames, no motion blur, no text or labels. ONE character only.
POSE INVARIANT: preserve image 1's forward-leaning HAND-OFF contact pose: forward arm extended to screen right, palm supporting exactly one black round bomb at chest level, rear hand at the satchel. Do not bend the arm back into idle pose. Same gaze, face, body lean and feet; change the shorts and socks only.
```

## release

```text
Use case: precise-object-edit.
Image 1 is the exact updated Chiri PLANT / HAND-OFF battle portrait edit target, with approved pumpkin shorts and orange/dark-charcoal OVER-KNEE striped socks.
Produce the matching RELEASE / EMPTY HAND pose by editing ONLY the bomb and the fingers of the extended forward glove at the far SCREEN RIGHT.
Completely remove the ONE black spherical bomb and rope fuse being held in that forward hand. Leave the same outstretched palm-up gloved hand, fingers gently relaxed open in a natural friendly 'here you go' gesture. No object, ghost circle, sparks or glow above this palm.
Keep EVERYTHING ELSE as identical as possible: face and expression, head/hair position, teal eyes looking screen right, low ash-blond ponytails, exact torso/shoulders/arm angle, ivory shirt, cape, orange scarf, bag and all bombs INSIDE the bag, charm and tassel, belts and gold details, the newly approved charcoal-and-orange short pumpkin shorts with X stitches and scalloped cuffs, the exact orange/dark-charcoal horizontal striped socks and their over-knee height, bare thigh gaps, exact knee angles, leg stance, boots and foot locations. Do not redesign or reposition any garment or limb apart from the small adjustment to forward fingertips. Do not remove bombs in the satchel.
Same fine anime painterly linework, lighting, texture, whole-figure framing and margins, 1024x1536 portrait. Preserve actual transparent RGBA background and gaps, opaque character interior, no backdrop or halo, no ground, no cast shadow, no effects or text.
```
