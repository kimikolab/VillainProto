# 棘鎧のカド 本番立ち絵 v1

生成日: 2026-09-20
生成方法: Codex 組み込み ImageGen
キャラクター基準: `design/art/kado/kado-concept-v2-androgynous.png`

## 採用先

- `DemoApp/assets/portraits/kado.png` — 編成・選択画面用の正面立ち絵
- `DemoApp/assets/portraits/battle/kado_idle_right.png` — 戦闘中の右向き待機立ち絵

両方とも実透過 RGBA。既存キャラとの統一を優先し、明確な輪郭、抑えた彩度、セル調の面、
全身が収まる縦長構図を参照した。戦闘用はカドの `Immobile` と棘守りを反映し、攻撃姿勢ではなく
味方と敵の間へ割り込む待機姿勢にした。

## 正面立ち絵

```text
Use case: identity-preserve
Asset type: production-ready FRONT-FACING full-body character selection portrait for the VillainProto game roster
Input images: Image 1 is the authoritative identity and costume reference for the approved androgynous Kado redesign; preserve that character exactly. Images 2, 3, and 4 are PRIMARY GAME-ROSTER STYLE AND FRAMING references only: match their vertical full-body scale, clean dark anime outlines, readable cel-shaded shapes, restrained muted color, modest soft shading, simple game-portrait finish, centered footprint, and safe margins. Do not copy those characters, costumes, weapons, or anatomy. Image 1 defines Kado; Images 2-4 define how the final asset must sit beside the existing cast.
Primary request: create ONE standalone full-body, nearly straight-on front-facing selection portrait of redesigned Kado. He is a tall, extremely slender, androgynous adult male with long limbs, narrow shoulders and waist, pale tired complexion, elegant fine-boned oval face, narrow jaw, softly pointed chin, high cheekbones, straight slender nose, restrained lips, sharp dark almond eyes, and medium-long black hair with a subtle wine-red sheen, loosely tied back with fine strands around the face. Gender presentation is beautifully ambiguous at first glance while remaining an adult man. His expression is severe, emotionally distant, and quietly exhausted.
Pose: perfectly calm and unnaturally still, standing squarely toward the viewer with both boots planted and visible. Head and torso face front, only a tiny natural asymmetry. Arms hang slightly away from the body so the forearm and rib thorns remain readable; hands relaxed but clawlike, not fists. Protective interposition rather than attack. No contrapposto runway pose, no dramatic combat action, no weapon, no shield.
Costume identity: faithfully reproduce Image 1's fitted articulated blackened-steel armor following the slim anatomy; overlapping plates shaped like rose petals and sepals; black iron rose embedded over the sternum; thorny dark-metal vines clasping torso and limbs; large functional backward-swept hooked thorns on shoulders, outer forearms, ribs, hips, spine, greaves and gauntlets; subtle dried dark-crimson blood and fine dim red seams where the curse presses into him; narrow deep desaturated burgundy split cloth tails; worn gunmetal and black iron with oxblood accents and faint tarnished silver edges. Armor itself is the weapon. Keep the silhouette dangerous and unmistakably thorn-armored at small roster size.
Style/finish: simplify Image 1's concept-sheet micro-detail just enough to match Images 2-4 at normal game scale. Prioritize clear silhouette, medium-size material shapes, definite linework, restrained highlights, modest cel shading, and readable facial features. Not glossy splash art and not painterly realism.
Composition/framing: vertical portrait canvas matching existing roster assets, one character only, centered, full head, tied hair, thorn tips, fingertips, cloth tails, and both boots completely inside frame with comfortable narrow margin. Feet share a stable baseline. No floor shadow.
Background: flat perfectly uniform opaque chroma-key green RGB(0,255,0), exact #00FF00, filling the entire canvas and all gaps between hair, thorns, arms, legs, and cloth. No gradient, texture, checkerboard, vignette, floor, or green bounce light on the character.
Constraints: same Kado as Image 1; one adult male character only; genuinely slender and androgynous; front-facing selection portrait; coherent repeatable armor; no weapon, shield, helmet, cape, bare chest, text, UI, border, logo, signature, or watermark.
Avoid: broad shoulders, giant or muscular build, square rugged face, ordinary generic plate armor, losing or simplifying the thorn-vine cage, thorns cropped by canvas, feminine breasts or childlike bishounen anatomy, seductive expression, fashion-model stance, action pose, facing sideways, scene lighting, glossy high-saturation marketing art, photorealism, 3D render.
```

生成結果は指定より扱いやすい実透過 RGBA になったため、そのまま採用した。

## 戦闘中の立ち絵

```text
Use case: identity-preserve
Asset type: production-ready RIGHT-FACING full-body battle idle character cutout for the VillainProto game roster
Input images: Image 1 is the authoritative approved standalone front portrait of redesigned Kado; preserve his exact face, androgynous adult male identity, proportions, hair, armor, palette, and rendering finish. Image 2 is the authoritative concept-sheet reference for back, side, thorn placement, and cloth construction. Images 3 and 4 are PRIMARY EXISTING-GAME BATTLE ASSET references only: match their right-facing three-quarter orientation, vertical full-body framing, scale, safe margins, clear silhouette, clean anime linework, restrained cel shading, and practical idle-pose presentation. Do not copy their characters, costumes, weapons, or anatomy.
Primary request: create ONE standalone full-body battle idle illustration of redesigned Kado, facing toward SCREEN RIGHT where the enemy stands. This is a passive guarding/interposition pose, not an attack frame. Kado never attacks first: he braces, turns his thorns outward, and waits to intercept a single-target blow meant for an ally.
Identity: same tall, extremely slender, androgynous adult man from Image 1—long limbs, narrow shoulders and waist, pale tired complexion, elegant fine-boned oval face, narrow jaw, sharp dark almond eyes, medium-long black hair with subtle wine-red sheen loosely tied back. Preserve the severe, emotionally distant, quietly exhausted expression. His gaze is fixed at an enemy off screen right, never at the viewer.
Pose: three-quarter right-facing body and face. Feet planted in a stable staggered guard, front foot toward screen right, knees only slightly bent, torso upright and unnaturally still. Near-side forearm raised diagonally across the chest as a ward, palm half-open; far arm held slightly outward and back so shoulder, elbow, forearm, rib, and hand thorns all point away from his body toward likely impacts. His body interposes between an unseen ally behind him on screen left and the enemy on screen right. Hands are relaxed clawlike armored hands, not weapon grips. Hair and split cloth tails trail slightly toward screen left. No attack swing, lunge, spellcasting, roar, or exaggerated motion.
Costume invariants: faithfully preserve Image 1's fitted articulated blackened-steel rose-petal armor, black iron rose embedded over the sternum, thorny dark-metal vine cage around torso and limbs, large functional backward-swept hooked thorns on shoulders, outer forearms, ribs, hips, spine, greaves, and gauntlets, subtle dried dark-crimson blood, dim red seams, narrow deep-burgundy split cloth tails, worn gunmetal/black iron/oxblood palette and tarnished silver edges. Armor itself is the weapon. No weapon or shield.
Style/finish: match the production roster look of Images 1, 3, and 4—definite dark lines, clear medium-size shapes, readable cel shading with modest soft transitions, restrained highlights, muted colors, face readable at game scale. Avoid glossy marketing splash-art lighting and excessive concept-sheet micro-detail. Strong silhouette at small size.
Composition/framing: vertical battle-character canvas, one character only, full body from highest hair and shoulder thorn to lowest boot visible, centered with safe transparent margin on every side. Neither cloth tails, hair, fingers, nor any thorn tip may be cropped. Feet share a believable baseline but there is no drawn ground or shadow.
Background: genuinely transparent RGBA alpha with clean cutout edges and transparent gaps between hair, thorns, arms, legs, and cloth. No black, white, gray, green, checkerboard, gradient, vignette, scenery, floor, or cast shadow drawn into pixels.
Constraints: same Kado as Image 1; one adult male character; clearly right-facing; idle guarding pose; genuinely slender and androgynous; no weapon, shield, helmet, cape, text, UI, border, logo, signature, or watermark.
Avoid: frontal selection pose, facing left, attacking, punching, claw strike, crouching too low, broad muscular build, square rugged face, generic plate armor, missing thorns, cropped spikes, extra limbs, feminine breasts or childlike anatomy, seductive pose, huge magical aura, glossy splash art, photorealism, 3D render.
```

## 戦闘用の背景修正

初回出力の透明画素に暗赤色の RGB が残っていたため、人物を変えずに背景抽出を再実行した。
採用PNGは外周と空隙のアルファが 0 で、サイズは 1024×1536。

```text
Use case: background-extraction
Asset type: production battle character cutout correction
Input images: Image 1 is the exact approved battle-idle illustration and the edit target.
Primary request: remove ONLY the entire dark red, brown, black, vignette, glow, and gradient background and replace it with genuinely transparent RGBA alpha. Preserve the character exactly as drawn.
Edge handling: create a clean professional cutout around every outer armor thorn, hair strand, fingertip, boot, and cloth-tail edge. Make every empty gap between hair strands, shoulder spikes, arms, torso, legs, and split cloth tails genuinely transparent. Remove all reddish background halos and color spill from the silhouette, while preserving intentional dark-crimson hair, cloth, blood marks, and red armor seams that belong to the character.
Invariants: preserve exact face and identity, pose, gaze, anatomy, hands, armor design, thorn count and placement, chest rose, hair, cloth, colors, linework, shading, highlights, character scale, canvas dimensions, framing, and margins. Do not redraw, restyle, crop, reposition, resize, sharpen, blur, recolor, or add anything. No floor or cast shadow.
Constraints: transparent alpha only outside the character; one character; no colored fill, black fill, white fill, gray fill, green fill, checkerboard drawn into pixels, gradient, vignette, scenery, text, logo, signature, or watermark.
```
