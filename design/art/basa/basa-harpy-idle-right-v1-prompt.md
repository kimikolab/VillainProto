# 喧噪のバサ（ハーピー娘）戦闘待機・右向き v1

生成日: 2026-09-19
生成方法: Codex 組み込み ImageGen
キャラクター参照: `design/art/basa/basa-harpy-concept-v3.png`
構図・仕上げ参照: `DemoApp/assets/portraits/battle/basa_idle_right.png`、`DemoApp/assets/portraits/battle/rica_idle_right.png`

## デザイン意図

- 地上の立ち姿ではなく、敵陣側である画面右へ向いた低空ホバリングを戦闘待機姿勢にする。
- 翼は上下にずらして羽ばたきの途中を表し、攻撃ではなく次の行動を待つ状態に留める。
- 鳥脚は着地させず後方へ畳み、飛行中であることを静止画でも読めるようにする。
- 小さな風の輪と数枚の羽根だけを加え、常に風を操る性質を示しながら本体の視認性を保つ。
- ゲーム素材化を前提に、全身を収めた透過背景と安全な外周余白を確保する。

## Prompt

```text
Use case: stylized-concept
Asset type: production-ready right-facing battle idle character cutout for the VillainProto game roster
Input images: Image 1 is the authoritative identity and anatomy reference for the redesigned harpy Basa; preserve her design exactly. Image 2 is layout reference only for the existing battle asset's vertical full-body scale, right-facing orientation, readable silhouette, and transparent cutout; do not copy the old male character. Image 3 is style/finish reference for a roster-consistent battle pose and clean transparent edges; do not copy Rica's identity, clothing, weapon, or pose.
Primary request: create a new full-body battle idle illustration of the harpy woman from Image 1, airborne in a low hovering flight and facing toward screen-right where the enemy stands. This is an idle/ready pose, not a finished attack. Her torso leans slightly forward and turns three-quarter right, head and bright amber eyes looking right, with the same delighted innocent mischievous smile. Her two arm-integrated wings are caught mid-flap: the nearer wing sweeps slightly downward and forward, the farther wing lifts back and upward, creating a dynamic but compact silhouette. Keep the humanlike shoulder and elbow articulation, avian scaled forearms, dexterous clawed harpy hands, and large feather fans growing biologically from the arms exactly as established in Image 1. Her bird legs bend and trail beneath/behind her in flight, with both huge black-taloned feet visible and not touching the ground.
Motion language: a small restrained crescent of pale wind and two or three loose feathers curl beneath and behind her, showing constant gust control without obscuring the body. Hair, scarf, tunic tails, and feathers stream slightly backward from forward motion. No ground, no floor shadow.
Composition/framing: vertical battle-character canvas, one character only, full body from highest wingtip to lowest talon visible, centered with safe transparent margins on every side. Keep wings compact enough that neither wingtip is cropped. Strong silhouette readable at game scale. Overall footprint and visual weight comparable to the existing battle portraits.
Identity invariants: same silver-gray tousled hair with feather accents, amber eyes, face, joyful expression, stormy teal/cream/charcoal feather pattern, cream layered flight tunic, fitted dark shorts, leather straps and small weathered metal fittings, bare human shoulders transitioning organically to feather roots, scaled avian forearms, long black hand claws, feathered lower legs, huge bird feet and black talons. Preserve modest, practical non-sexualized clothing and the polished Japanese fantasy RPG linework/cel-shaded rendering of Image 1.
Background: genuinely transparent background with clean alpha; no colored backdrop, black fill, gradient, vignette, floor, or scenery.
Constraints: exactly two arms, two arm-integrated wings, two bird legs, two hands, and two feet; no separate back wings; no weapon; no text, UI, border, card frame, logo, signature, or watermark.
Avoid: standing on the ground, landing pose, attacking impact pose, frontal presentation, facing left, back-mounted angel wings, wings replacing the arms, ordinary human hands, normal human feet, extra limbs, cropped wing tips or talons, huge tornado effects, feathers hiding the anatomy, costume redesign, seductive pose, photorealism, 3D render.
```
