# トウ 通常・戦闘立ち絵 v1

生成: Codex 内蔵 image_gen、2026-10-07。
参照: 採用方向の `tou-concept-v1.png`。

- `tou-front-v1.png`: 通常立ち絵。触角の付け根を左右に分け、浮遊する粉・雷を除いた。
- `tou-idle-right-v1.png`: 右向きの戦闘待機。片手を前へ差し出し、もう片手で羽を寄せる。
- 両方 1024×1536、透過PNG。ゲームへの適用は未実施。
- `front-alpha-check.png` / `battle-alpha-check.png`: 検証用の灰色背景合成。背景、羽・触角の縁、脚の間の透過を目視確認。画像編集は内蔵 image_gen、合成は検証用のみ。
- 生成ツールの画像表示では透明部分のRGB（茶色の背景）が見えるため、アルファを合成して確認した。

## 通常立ち絵

```text
Use case: identity-preserve with precise antenna anatomy correction and background extraction.
Asset: final production NORMAL FRONT STANDING portrait of Tou for a Japanese fantasy RPG.
Use the reference as the authoritative approved version 1 identity and costume: exact face, sleepy honey-gold eyes, short ash-brown tousled hair, petite androgynous adult proportions, soft ivory moth fur around neck, dark brown layered tunic, short dark shorts and bare legs, worn brown boots, broad luxurious brown/ivory organic moth wings with large eye spots. Preserve the appealing ornate wing silhouette and subdued warm palette. Do not redesign into long sleeves or trousers.
CRITICAL ANTENNA CORRECTION: two feathery moth antennae with BILATERALLY SYMMETRIC ROOT POSITIONS relative to the head, at the left and right frontotemporal hairline, just above each outer eyebrow/temple. Each has its own short clearly visible curved stalk emerging from its respective side of the forehead through the hair. Both roots are at the same anatomical height on the head, widely separated across the forehead. Neither stalk emerges from the central crown or center hair part. No shared central root. The feathery fans sweep upward and outward away from the face. The head is upright with only a very slight tilt so the paired root positions are easy to read. Preserve two antennae only, no horns.
Pose: calm near-frontal full-body standing pose, shoulders slightly drawn in; one hand gently gathers the wing edge near the chest, other arm relaxed outward and low with palm softly open. Natural hands, each five fingers. Both boots on same ground baseline. Wings partly close around the body like the reference, softly framing the figure. Face and eyes oriented toward viewer.
Clean production sprite finish: controlled anime linework and cel shading with refined painted moth textures, readable at game scale.
BACKGROUND MUST BE GENUINE TRANSPARENT ALPHA, fully isolated character. Remove the reference's entire brown glow/backdrop, black corners and vignette. No opaque background, no gray fill, no floor or ground shadow, no checkerboard painted into image. Remove all floating gold dust and lightning effects from the reference for clean game use. No external particles or aura. Character interior remains fully opaque.
One character only, one pose, portrait framing preferably 1024x1536, full antennae, wings, hands and boots within canvas with generous transparent margins on every side. No text, collage or watermark.
```

## 通常立ち絵の背景抽出

```text
Edit this image into a transparent game sprite. Remove ALL background pixels: the brown gradient, glow, black corners and the background visible between legs, boots and wings. Replace the entire background with actual alpha transparency, NOT white, gray or checkerboard. Preserve the complete character exactly, including both antennae, hands and wings. No other changes. Output one full-body isolated character PNG with transparent background.
```

## 戦闘待機

```text
Use case: identity-preserve.
Create ONE production full-body BATTLE IDLE sprite of Tou from the supplied approved character. Same exact face, short ash-brown hair, honey-gold eyes, ivory moth fur ruff, broad brown/ivory moth wings with eye spots, dark layered short tunic, shorts, bare legs and brown boots. Do not redesign costume.
Turn the entire character to face SCREEN RIGHT in three-quarter side view, face and eyes looking toward the enemy at right, not looking at viewer. Calm cautious ready stance, knees softly bent, feet staggered and both boots planted at the same ground baseline. One forearm extends forward toward right at waist height with an open relaxed palm, ready to scatter powder; other hand holds the nearer wing edge against the chest. Shoulder-elbow-wrist anatomy must be clear, exactly two arms and two hands with five fingers each. Wings remain half-closed, extend gracefully behind toward screen left, preserve their broad elegant silhouette and large eye-spots. No attack lunge, no weapon.
CRITICAL: two moth antennae emerge from a symmetric pair of roots at the left and right upper-forehead/temple hairline. Their roots sit at matching anatomical height on opposite sides of the head. Neither grows from the center crown or central part. In this three-quarter view show the near-side stalk clearly above the visible temple and the far-side stalk foreshortened on the far side. Two separate short curved stalks, each carrying a feathery fan sweeping up and outward. No shared root, no extra antenna.
Polished anime fantasy game linework, controlled cel shading, beautiful refined moth textures and readable forms. Keep the approved version 1's striking wings and sleepy delicate face.
Genuine transparent alpha background. The supplied brown and black background must be discarded completely. No background or ground, no vignette, no glow, no shadow, no particles, no powder cloud, no lightning or visual effects. One isolated opaque character only on transparency. Full antennae, wings, hands and both boots completely inside portrait canvas with comfortable margin. No cropping, text, label or watermark.
```
