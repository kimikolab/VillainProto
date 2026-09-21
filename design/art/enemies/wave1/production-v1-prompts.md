# 第一波の敵・戦闘立ち絵 v1

生成日: 2026-09-21
生成方法: Codex 組み込み ImageGen

- `recruit-idle-right-v1.png`: 討伐隊の新兵。真面目そうな若者、正統派の両手剣の構え。
- `axeman-idle-right-v1.png`: 戦斧兵。短気な若者、大柄な体と大型の両手斧。

両方1024×1536のRGBA PNG。外周・脚間のアルファ0を確認。右向きは既存の戦闘画像仕様に合わせた（敵はBattlePawn3Dで左右反転）。ゲームへの組み込みは未実施。

## 討伐隊の新兵 プロンプト

```text
Use case: stylized-concept. Asset: VillainProto full-body battle idle character illustration. Japanese dark fantasy strategy RPG roster art: clean definite dark anime outlines, readable medium-size cel shaded shapes, modest soft shading, restrained muted colors, natural adult proportions, matching a grounded illustrated fantasy roster. One adult man only. Entire body and entire weapon inside vertical 1024x1536 canvas with safe margins, both boots visible. Three-quarter side view with face, chest, gaze and weapon readiness toward SCREEN RIGHT. Standing combat-ready idle, no active attack. Genuine transparent RGBA background and transparent gaps, no background color, scenery, ground, floor shadow, vignette or checkerboard. No text, watermark, UI or frame. Simple practical rank-and-file equipment, no elaborate hero ornament or magical effects. Subject: 討伐隊の新兵, earnest serious young adult male recruit around 20, slim athletic ordinary build, short tidy chestnut hair, clean-shaven youthful face, focused slightly nervous but resolute expression. Practical lightly worn steel breastplate and modest shoulder plates over muted off-white padded uniform, brown leather belts and boots, small desaturated navy cloth accents. Orthodox disciplined two-handed straight longsword middle guard: both hands correctly grip the hilt in front of lower chest, blade points diagonally upward toward screen right, tip entirely visible. Feet shoulder width apart with forward foot to the right, knees slightly bent, upright balanced training-manual posture. Sword is ordinary military issue, straight double edged blade and simple crossguard. Clear youthful face unobscured, no helmet, no shield. The design reads as a sincere novice soldier, not a veteran knight.
```

## 戦斧兵 プロンプト

```text
Use case: stylized-concept. Create one standalone full body battle-idle portrait for the same Japanese dark-fantasy strategy RPG roster as the previous recruit. Subject 戦斧兵: a young adult male soldier about 23, very large muscular body, broad thick shoulders, powerful arms and neck, youthful clean-shaven angular face, short tousled copper brown hair, impatient hotheaded scowl with furrowed brows and clenched teeth. Brawn-before-brains personality. Same ordinary army clothing family: practical worn steel partial breastplate and simple shoulder protection, off-white padded cloth and desaturated navy fabric accents, brown leather belts, bracers and sturdy boots; sleeves rolled back to expose powerful forearms. Not a barbarian or senior commander. One HUGE functional two-handed battle axe, thick long wooden haft, broad heavy steel axe blade. Both hands grip the same straight continuous haft well apart, axe held diagonally across the front of his body in a low ready guard with its big head at upper screen right, ready for a horizontal sweeping blow. Solid wide planted stance, knees slightly flexed, torso slightly forward, restless aggressive energy but idle not mid-swing. Three-quarter view facing SCREEN RIGHT, face and gaze aimed right. All anatomy, both boots and complete axe safely inside frame, full body centered, generous margin. Portrait 1024x1536. Clean dark anime linework, restrained muted palette, clear cel shaded medium-size shapes and modest painterly texture, natural adult proportions, detailed but readable at game scale, same finish as previous recruit. Truly TRANSPARENT RGBA background with clean transparent gaps, no colored backdrop or vignette or ground shadow. No text, extra people, extra weapons, shield, helmet, horns, magic, ornate costume, frame, watermark.
```
