# 継ぎ当てのツギ 本番立ち絵 v1

Codex 内蔵 image_gen で生成。採用コンセプトは `tsugi-concept-v1.png`。
ノースリーブ、調整済みの体型、ハンマー側の肘当てを維持。

- 正面待機: `tsugi-front-v1.png` → `DemoApp/assets/portraits/tsugi.png`
- 右向き戦闘待機: `tsugi-idle-right-v1.png` → `DemoApp/assets/portraits/battle/tsugi_idle_right.png`

両方とも1024×1536、32bit ARGB。角と空白の代表点のalpha=0、中央の代表点は253。生成時のアルファを維持。
`UiKit.cs` の駒IDによるパス規約で読み込む。ゲーム画面上の確認は未実施。

## 正面待機プロンプト

```text
Use case: identity-preserve. Produce a game-ready full-body FRONT STANDBY selection portrait of Tsugi, the exact petite ADULT female battlefield repair craftswoman in reference. Preserve approved face, stubborn mildly worried expression, ash-brown short hair, riveted metal hair clip on HER LEFT temple, natural fuller clothed bust and belted waist, sleeveless teal work tunic and cream chest cloth with full modest chest coverage, patched leather apron and tails, dark trousers, asymmetric kneepads, stout boots, oversized gloves, tools. Preserve small improvised iron-on-leather elbow guard on HER RIGHT elbow, the HAMMER arm; opposite arm bare. Huge heavy wooden pack frame overloaded with salvaged iron plates, broken round wooden shield with faded teal crest, timber and clean bone splints lashed with leather straps. Same material hierarchy and recognizable outline, no redesign. Pose: nearly frontal head chest and hips facing viewer, both feet firmly planted, slight forward lean under heavy pack, face fully readable, holding short riveting hammer comfortably in right hand near waist and a patch plate in left hand slightly outward. Calm standing ready to work, not attacking. Keep feminine torso silhouette and bare shoulders; equipment should not conceal face. Fine anime outlines, softly shaded painterly surfaces, muted teal rust brown gray and ivory, match reference exactly. One character only. Vertical 1024x1536 composition, full body and ALL backpack contents, boots and props inside frame with 5 percent safe transparent margins. GENUINE TRANSPARENT RGBA background: empty pixels alpha zero including gaps between limbs and supplies. Opaque character skin clothing metal. NO gray backdrop, floor, cast shadow, vignette, scenery, painted checkerboard, text or watermark.
```

## 右向き戦闘待機プロンプト

```text
Use case: identity-preserve. Game-ready full-body RIGHT-FACING BATTLE IDLE transparent character sprite of Tsugi, exact petite ADULT female battlefield craftswoman in reference image 1. Image 2 is orientation and transparent game asset reference ONLY, do not copy its character. Preserve Tsugi's face, ash-brown bob, metal clip on HER LEFT temple, stubborn focused expression, approved natural fuller clothed bust and belted waist, sleeveless teal workwear and cream chest cloth fully covering chest, patched leather apron, dark trousers, asymmetric kneepads, stout boots and big gloves. Small iron-on-leather elbow guard stays on HER RIGHT elbow (HAMMER hand), opposite arm unarmored; do not swap arms. Her right hand grips short riveting hammer, left hand holds salvaged rivet-hole plate. Huge overloaded salvage pack secured with harness and waist straps: weathered iron plates, broken teal-crest round wooden shield, bundled timber and pale bone splints. Reconstruct consistently in rotated pose, not a new backpack. BATTLE pose: rotate whole body into a clear three-quarter side view facing SCREEN RIGHT, nose eyes chest hips knees boots all directed toward screen right; NOT looking at viewer. Slight forward lean under backpack weight, knees softly bent, feet staggered but planted, holding metal repair patch forward and hammer poised near waist ready to work, NOT swinging or attacking. Keep compact readable silhouette, two arms two hands, recognizable face in three-quarter view, naturally hide far-side hairclip rather than moving it. Same detailed anime linework, softly painted shading and muted palette as image 1. Vertical 1024x1536 composition, entire pack and boots safely inside image with 5 percent margins. Genuine transparent RGBA cutout, alpha zero in empty space and gaps. NO scenery, background color, vignette, glow, ground, floor shadow, checkerboard painting, text or effects. Opaque character materials. Single character only.
```

