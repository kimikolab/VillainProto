# チリ 演出素材 v1 — 生成プロンプト

作成日: 2026-11-01
画像生成: 内蔵 image_gen（CLI不使用）。画像の透過はそのまま維持。

元のCだけを基準に作成。仕掛ける差分は右向き待機絵、手を離す差分は仕掛ける差分、爆弾本体はCの設定画をそれぞれ参照。

## 仕掛ける

```text
Use case: identity-preserve. Asset type: production battle pose variant for Chiri in an existing Japanese fantasy RPG.
Image 1 is the exact approved transparent RIGHT-FACING battle idle portrait to edit. Preserve her identity and ALL design: ash blonde messy short low twin ponytails, teal eyes, soot cheeks, playful open grin with little canine, charcoal hooded cape with ragged hems, orange scarf, rolled ivory sleeves, leather gloves, large brown satchel with bombs, brass star-cut spherical charm and orange tassel, dark roomy below-knee trousers, striped socks, brown boots. Original C design ONLY, no Japanese festival motifs or outfit redesign.
Maintain same canvas and character scale, 1024x1536 portrait, same head height, hip, legs, boots and planted feet as closely as possible. Keep lower half exact. Same beautiful fine anime line art, textured painterly cel shading. Whole character, bag, hand and bomb within image with small clear margins, no clipping. Keep her torso/head/eyes facing screen RIGHT, never toward viewer. Genuine transparent RGBA background and gaps, opaque figure. NO backdrop or dark gradient, no floor, no cast shadow, no particles, no sparks, no flames, no text, no labels or extra figure.
Change only the action in the shoulders/arms and a small forward lean:
'PLANT / HAND OFF A BOMB' key pose. Her front arm reaches clearly farther forward toward SCREEN RIGHT at chest height, elbow nearly straight but natural, palm up carrying exactly one sealed black iron spherical bomb with short braided rope fuse. The gloved hand securely supports the bomb; fingers readable and anatomically sound. Shoulder and torso lean slightly forward with eager mischievous smile, as if placing or passing the bomb onto someone. Her rear hand remains at the satchel mouth, no second handheld bomb. This is the held contact pose, NOT an overhead throw or attack, no motion blur. Preserve the reference's wide stable leg stance. Do not add effects; the game will animate the bomb separately.
```

## 手を離す

```text
Use case: precise-object-edit. Image 1 is the exact transparent Chiri 'plant a bomb' battle pose edit target. Create the matching 'AFTER HANDING OFF / RELEASE' keyframe.
Change ONLY the contents of the outstretched gloved hand at the far screen right: completely remove the single round bomb and its rope fuse from that hand. Keep the same extended palm-up hand; adjust fingers just enough so it is an empty relaxed open hand with all five fingers naturally defined. A friendly 'here you go!' gesture after passing the bomb. No bomb, spark, flame or ghost circle above that palm.
Preserve EVERYTHING ELSE as closely as possible: exact canvas 1024x1536 and margins, exact cheerful face and rightward gaze, blond low ponytails, head, teal eyes, scarf, cape, sleeves, other gloved hand at satchel, bombs INSIDE SATCHEL unchanged, belt, leg stance, boots and foot positions. Do not remove satchel bombs, charm or costume. Same original C design, no redesign, no head movement, no change of pose outside the forward hand, no expression change, no effects, no text. Keep genuine transparent RGBA background and empty gaps, fully opaque character interior. No backdrop, floor, shadow or halo. A single full-body standing figure only.
```

## 爆弾本体

```text
Use case: stylized-concept. Asset type: a single transparent fantasy RPG bomb BODY sprite to be used as a small readable status icon and projectile.
Image 1 is a character concept style and prop design reference only. Recreate the same spherical black iron bomb seen in the bottom-right prop study, with worn charcoal iron, subtle warm rubbed bronze scratches, small brass rivets and collar at top, faint panel seam and small star-shaped metal accent matching Chiri's bag charm. Original C design, not a red festival ball. Beautiful refined anime painterly shading matching the reference, clear simple silhouette readable at tiny size.
ONE closed bomb body only, front three-quarter view. Important: REMOVE THE ENTIRE ROPE FUSE AND FIRE SPARK, leaving only the short brass socket at the top center so the game can draw and shorten a separate fuse. No rope, no flame, no glow, no smoke, no loose particles. No exploded or opened bomb, no interior diagrams.
Square canvas 1024x1024, body including socket occupies about 82% of width and height, centered, entire round lower edge visible with clean margins. Opaque iron/bronze object with genuine transparent RGBA background and clean antialiased edge. NO ground, NO shadow or ambient glow behind object, NO backdrop or paper, NO text or labels, no hands, no other props or people. Single object only.
```
