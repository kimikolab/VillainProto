# ミオ 呪われた泉の邪神 本番立ち絵 v1

Codex 内蔵 image_gen で生成。基準は銀環が正面の画面右（本人の左こめかみ）にあるボブ版。移動後の案は不採用。

- mio-concept-v1.png: 採用コンセプト。
- mio-front-v1.png: 正面待機。DemoApp/assets/portraits/mio.png に同一画像を配置。
- mio-idle-right-v1.png: 右向き戦闘待機。DemoApp/assets/portraits/battle/mio_idle_right.png に同一画像を配置。

2枚とも1024×1536、32bit ARGB。角と空白の代表点の alpha=0、中央の代表点は正面253・戦闘252。生成原本の透過を維持。UiKit.cs の既存パス規約により読み込まれる。ゲーム画面上の確認は未実施。

## 正面待機プロンプト

```text
Production transparent full-body anime RPG portrait of Mio, exact cursed spring deity in reference. Preserve face, apparent age and petite natural proportions, indigo chin-length bob, soft quiet expression, tiny black tear marks, blackened silver collar, broken silver waist seals, cursed layered indigo petal dress with eroded translucent outer veils over opaque torso and skirt, bare feet, ancient silver bowl held in both hands, a few compact glossy black liquid ribbons floating around body. Head accessory is broken double silver ring with ONE black droplet on HER LEFT temple (screen right in frontal view), same as reference. Opposite ear mostly covered by bob, do not expose both ears sharply. No flowers on head. Simplify tiny filigree for game-size readability. Full body all feet and liquid effects inside frame, 7 percent empty margin all sides. TRUE TRANSPARENT RGBA background, no scenery, ground, pool, flowers, mist, glow, shadow, vignette, or painted checkerboard. All empty space alpha zero, skin and core clothing opaque. One character, no text or watermark. FRONT STANDBY portrait, almost frontal head and torso, preserve reference gentle expression and stance, upright balanced feet, calmly cradle bowl at waist. Preserve hair accessory on SCREEN RIGHT. Remove background only while refining into clean game cutout.
```

## 右向き戦闘待機プロンプト

```text
Production transparent full-body anime RPG portrait of Mio, exact cursed spring deity in reference. Preserve face, apparent age and petite natural proportions, indigo chin-length bob, soft quiet expression, tiny black tear marks, blackened silver collar, broken silver waist seals, cursed layered indigo petal dress with eroded translucent outer veils over opaque torso and skirt, bare feet, ancient silver bowl held in both hands, a few compact glossy black liquid ribbons floating around body. Head accessory is broken double silver ring with ONE black droplet on HER LEFT temple (screen right in frontal view), same as reference. Opposite ear mostly covered by bob, do not expose both ears sharply. No flowers on head. Simplify tiny filigree for game-size readability. Full body all feet and liquid effects inside frame, 7 percent empty margin all sides. TRUE TRANSPARENT RGBA background, no scenery, ground, pool, flowers, mist, glow, shadow, vignette, or painted checkerboard. All empty space alpha zero, skin and core clothing opaque. One character, no text or watermark. BATTLE IDLE facing SCREEN RIGHT, full body 3/4 right-facing pose: gaze, face, torso, hips and both feet toward enemy off-screen right. Calm ready stance, slight staggered feet at same ground baseline, bowl held forward at waist by both hands. Keep head modest three-quarter rather than strict profile so far LEFT temple silver ring can partially peek above hair naturally; do not relocate to the other side and do not duplicate. No looking at viewer. Dress veils drift slightly left behind body, compact floating liquid effects. No attack motion.
```
