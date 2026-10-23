# 墓守リィカ 立ち絵リメイク v1

生成日: 2026-10-24
生成方法: Codex 組み込み ImageGen（CLI 不使用）
出力: `rica-standing-remake-v1.png`（1024×1536、透過PNG）
状態: 確認用のリメイク案。ゲーム未適用。
参照: `DemoApp/assets/portraits/` の rica（元絵・デザインの正）、hane / shio / wata（絵柄・頭身の参照）。

白銀の片側三つ編み、青い瞳、長い耳、フードを下ろした黒い外套、青緑のワンピース、茶色の前掛け、ベルトと小袋、黒いストッキング、茶色のブーツ、木柄のシャベルを踏襲。頭身と顔の比率、線・陰影・素材の描き込みを現在の立ち絵へ合わせた。元画像とゲームの参照先は変更していない。

## 最終プロンプト

```text
Use case: style-transfer.
Asset type: single full-body ordinary standing portrait of 墓守リィカ (Rica), a Japanese fantasy RPG party member, high-quality remake of the early artwork.
Input images: Image 1 (rica.png) is the authoritative EDIT TARGET for character identity, exact hair, clothing, palette, equipment and quiet temperament. Images 2 (hane.png), 3 (shio.png), 4 (wata.png) are ART STYLE AND BODY PROPORTION references ONLY. Do not draw those characters or copy their costume, ears, equipment, background, or pose.
Primary request: redraw Rica from scratch in the current roster's polished illustrated anime style, with the same detailed delicate linework, softly layered painterly cel shading, refined facial rendering, subtle cloth folds and worn leather/metal materials as references 2-4. Match their approximately 7 to 7.5 heads-tall full-body proportions; reduce the oversized early-anime head, lengthen and naturally articulate the torso and limbs, preserve her slender youthful female elf identity and restrained gentle, slightly melancholy neutral expression. She should still be immediately recognizable as Rica, not a newly designed character.
Identity invariants from image 1: pale skin, pale ice-blue eyes, white/silver hair with side-swept bangs, straight face-framing locks and one single thick braid draped forward over HER LEFT shoulder (viewer-right). Long horizontally pointed elf ears, no earrings. No dark hair, ponytail, double braids, horns, animal ears, or tail.
Costume invariants: plain black/dark navy hooded long cloak, hood DOWN behind neck, simple dark tie at throat, cloak to the lower calf; high-neck dark teal long-sleeved calf-length dress, sleeves slightly loose with narrow dark cuffs; muted tan/brown plain work apron from chest to below knees, hand-stitched edges and gently worn cloth; practical dark brown waist belt with rectangular iron buckle and hanging belt tail, diagonal brown utility strap across apron at hips, small plain brown leather flap pouch at HER LEFT hip (viewer-right); opaque black stockings and sturdy brown folded-cuff short work boots. Preserve the original modest practical coverage, colors, layers, and simple design. Refine materials, seams and folds without adding elaborate ornaments or changing cut.
Equipment: one ordinary gravedigger's shovel with straight wooden shaft and black iron pointed spade blade, held in HER RIGHT hand (viewer-left). Entire shovel visibly continuous, hand correctly wrapped around wooden shaft, metal blade resting naturally beside her right boot. It is a practical shovel, NOT a scythe, spear, axe or magical staff.
Pose/composition: graceful understated standing pose in a slight three-quarter turn toward the viewer, looking toward viewer, weight naturally on one leg, feet comfortably apart and both boots visible. Right hand holds shovel at about hip level, shaft tilts gently outward to viewer-left with blade down; left hand relaxed near pouch and apron, all hands anatomically coherent. Cloak parts just enough to read apron and dress, softly drapes naturally rather than dramatic wind. Preserve her quiet gravekeeper personality; no action pose or giant smile. Single character only; no companions or summoned dead.
Framing: vertical 1024x1536 portrait, entire head, ears, braid, cloak hem, both boots and entire shovel inside frame with clear safe margin. Refined adult anatomy, no chibi, no exaggerated fashion-model limb length. Fully opaque solid character.
Background: genuinely transparent RGBA everywhere outside character and shovel, including gaps between arms and clothing. No background, scene, gradient, glow, halo, floor, cast shadow, black vignette or fake checkerboard. No text, border, watermark or labels.
Avoid: flat crude early cel shading, thick heavy outlines, photorealism, 3D rendering, gritty noisy textures, excessive decoration, oversized weapons, changed hairstyle or equipment, battle effects, cropped feet or shovel.
```
