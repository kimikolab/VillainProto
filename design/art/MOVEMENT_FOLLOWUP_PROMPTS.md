# 移動軸・追加の画像差分 v1

生成日: 2026-09-29
生成方法: Codex 組み込み ImageGen（全生成で transparent_background=true）
用途: 移動射撃・隣への弾き返し・緊急退避の差分素材。既存のゲーム内衣装を参照。
状態: DemoApp の `MoveShot`・`SpringGuard` に続く `Spring`・`Retreat` に組み込み済み。
ゲーム用PNGは `DemoApp/assets/portraits/battle/` の `sero_move_shot_idle_right.png`・
`hane_spring_guard_idle_right.png`・`shio_retreat_idle_right.png`。元画像は無加工でコピー。
靴底の余白を透過画像から計測し、ハネの片掌の接点・セロの矢の出どころ・シオの蔓の先端を原画に合わせた。

## セロ：移動射撃

出力: [sero-move-shot-right-v1.png](sero/sero-move-shot-right-v1.png)
寸法: 1024×1536、透過PNG
参照: `DemoApp/assets/portraits/battle/sero_idle_right.png` / `DemoApp/assets/portraits/battle/sero_attack_idle_right.png`

### 生成プロンプト

```text
Use case: identity-preserve. Asset type: one full-body transparent PNG action-pose variant for an existing 2.5D Japanese fantasy RPG battle sprite. Preserve the exact character identity, costume construction, colors, age, body proportions, detailed delicate linework and softly shaded painterly anime rendering of the reference. Single isolated character only. Genuine transparent alpha background, including gaps between limbs, hair, weapons and fabric; no background, ground plane, shadow, halo, smoke, glow, text, UI or watermark. All extremities completely in frame with safe margin. Match the existing side-on three-quarter battle camera, action aimed toward SCREEN RIGHT. This is a production pose variant, not a redesign. Input image 1 is Sero's exact idle identity/costume reference; image 2 is his bow and shooting anatomy reference. Change the pose to RETREATING WHILE FIRING OVER HIS SHOULDER: his hips and legs are urgently sidestepping toward screen LEFT, one foot bracing the next landing and the other knee bent mid-stride, but his torso twists back and his focused face, extended bow arm and ONE nocked arrow aim toward screen RIGHT. A clear twisting evasive mobile shot silhouette rather than the planted standing shot. Keep brown tousled hair, olive ragged hooded cape, off-white shirt, brown leather harness, fingerless gloves/bracers, red waist sash, olive trousers, brown buckled boots, hip quiver and the SAME rustic recurved wood bow with silver reinforcements. Draw the bowstring physically correctly from both bow tips to the pulling hand at his cheek; one straight arrow goes from nock by the cheek through the bow toward right, fletching at the nock and steel arrowhead at the forward right end. Wind sweeps the cape and sash toward screen RIGHT, opposite his leftward escape. No magical arrow, speed lines, afterimages, duplicated limbs or additional loose arrows. Full-body portrait canvas approximately 1024x1536, maintain reference human anatomy scale.
```

## ハネ：味方救援

出力: [hane-spring-guard-right-v1.png](hane/hane-spring-guard-right-v1.png)
寸法: 1536×1024、透過PNG
参照: `DemoApp/assets/portraits/battle/hane_idle_right.png` / `DemoApp/assets/portraits/battle/hane_palm_idle_right.png`

### 生成プロンプト

```text
Use case: identity-preserve. Asset type: one full-body transparent PNG action-pose variant for an existing 2.5D Japanese fantasy RPG battle sprite. Preserve the exact character identity, costume construction, colors, age, body proportions, detailed delicate linework and softly shaded painterly anime rendering of the reference. Single isolated character only. Genuine transparent alpha background, including gaps between limbs, hair, weapons and fabric; no background, ground plane, shadow, halo, smoke, glow, text, UI or watermark. All extremities completely in frame with safe margin. Match the existing side-on three-quarter battle camera, action aimed toward SCREEN RIGHT. This is a production pose variant, not a redesign. Input image 1 is Hane's exact idle identity/costume reference; image 2 is her ordinary double-palm strike, which this new ally-rescue pose must be distinguishable from. Create a SIDEWAYS RESCUE INTERVENTION: Hane urgently steps diagonally sideways into an unseen ally's place, head and eyes focused right, her leading open palm drives forward to screen RIGHT to push away an unseen attacker, while her other arm extends low backward toward screen LEFT in a protective warding gesture toward the unseen ally. Strong asymmetric silhouette with one forward palm and one rearward arm; NOT the ordinary two parallel palms forward, not a kick. Weight low, one boot planted and other foot following from a quick lateral hop, scarf and ponytail trail left. Preserve silver-gray high ponytail, two long rabbit ears, amber eyes, cropped rust-red fur-trim jacket, off-white blouse, brown leather shorts, belts, small hip pouch, brown leather bracers and fur-trim boots, long red waist scarf. Face determined and alert. Two anatomically correct arms and hands, five fingers each; keep exact costume and adult proportions. No ally or enemy in the image; no effect trails. Full-body landscape canvas approximately 1536x1024; ears, both boots, both hands and scarf entirely visible.
```

## シオ：緊急退避

出力: [shio-retreat-right-v1.png](shio/shio-retreat-right-v1.png)
寸法: 1024×1536、透過PNG
参照: `DemoApp/assets/portraits/battle/shio_idle_right.png`

### 生成プロンプト

```text
Use case: identity-preserve. Asset type: one full-body transparent PNG action-pose variant for an existing 2.5D Japanese fantasy RPG battle sprite. Preserve the exact character identity, costume construction, colors, age, body proportions, detailed delicate linework and softly shaded painterly anime rendering of the reference. Single isolated character only. Genuine transparent alpha background, including gaps between limbs, hair, weapons and fabric; no background, ground plane, shadow, halo, smoke, glow, text, UI or watermark. All extremities completely in frame with safe margin. Match the existing side-on three-quarter battle camera, action aimed toward SCREEN RIGHT. This is a production pose variant, not a redesign. Input image 1 is Shio's exact identity and costume reference. Change only action pose and expression: URGENT VINE RESCUE. Shio braces her boots, bends her knees and leans her body sharply backward toward screen LEFT, urgently pulling one taut living vine that extends from her two hands a short distance toward screen RIGHT and ends within the frame. Both hands gripping that same vine, the near hand pulled to her chest and far hand extended right, clear hauling tension; rescuing an unseen injured ally. Concentrated alarmed expression, brows raised inward and small open mouth calling out, rather than the cheerful relaxed idle smile. Keep her exact short fluffy chestnut hair, small round animal ears only (no human ears), huge fluffy brown tail with pale edge sweeping left, green short hooded herbalist coat, cream fur-trim cape sleeves, off-white blouse, belts, dark shorts, brown tights, brown boots, tiny daisies and leaves, amber medicine bottles on belt. Put the bottle and herbal dressing from her idle hands securely in belt pouches so her hands are free. Vine has a few modest fresh green leaves, no spell glow. Preserve recognizable tail volume and readable boot/hand silhouette. No injured ally or other character in image. Full-body portrait canvas approximately 1024x1536; ears, tail, hands and boots entirely within canvas.
```

### 仕上げの編集プロンプト

蔓の先端だけを画像内へ収める修正を追加。

```text
Use case: precise-object-edit. This is Shio's transparent full-body game sprite. Preserve this EXACT character illustration, pose, face, hair, tail, outfit, hands, boots, detailed rendering and all colors. Only correct the vine at the upper RIGHT edge: shorten the portion beyond her outstretched hand so its entire natural tapering green leafy tip is visible, ending at least 45 pixels inside the right canvas edge. A slim flexible vine held taut in her hands, ending in a short tapered green tip to the right, not a severed thick rope and not a blunt cut. Keep both hands gripping it and the pulling action unchanged. Do not alter the rest of the character or crop. Genuine transparent alpha background, preserve clean transparency, including internal gaps. No backdrop, halo, glow, floor, text or additional objects.
```
