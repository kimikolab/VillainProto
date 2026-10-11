# ヒヨの自然な戦闘待機 v1

作成: 2026-10-11。Codex 組み込み ImageGen、透過PNG、1024×1536。

通常・待機を、両足を接地して杖を低く持つ立ち姿に変更。閉じた口の穏やかな表情、落とした肩、重力に沿う衣服を、今後の呼吸・重心・髪や布の小さな待機モーションの基準にする。

- 新待機: `DemoApp/assets/portraits/battle/hiyo_ready_right.png`。既存の `UiKit.FindBattlePortrait` の ready 優先で読み込む。
- 煽り: 旧 `hiyo_idle_right.png` をそのまま `hiyo_stoke_idle_right.png` に複製し、`FireLevelLabels.Stoke` に接続。0.65秒表示後に新待機へ戻る。
- 元の待機・煽り（`hiyo_fan_idle_right.png`）は素材として保存。ターンギフト・火の粉の画像も維持。
- 新待機の表示高は1.89、足元余白は0.0163。旧動作絵は2.10相当を保ち、待機と動作間の本体の身長を揃える。
- 画像には炎や発光を焼き込まず、既存の演出が重なる。戦闘規則の変更はない。

## 最終プロンプト

検証: `dotnet build DemoApp/DemoApp.csproj --no-restore` 成功（警告・エラー0）。既存の `FireCheck.tscn --replay` で2回の再生を通し `FIRE_CHECK_OK` を確認。ゲーム画面の新待機は `hiyo-neutral-game.png` で確認。Godot終了時には既存のリソース解放警告が出る。

```text
Use case: identity-preserve.
Asset type: transparent full-body 2D anime fantasy RPG battle idle sprite, base for future subtle idle animation.
Input image 1 is the edit target and authoritative identity/style reference: Hiyo, the blonde elf girl with amber eyes, red ribbon ponytail, red-and-gold short cape and split overskirt, white patterned dress, brown gloves belts pouches boots, and exactly the same flame-shaped gold staff finial containing a red gemstone.
Primary request: replace ONLY the energetic raised-staff action pose with a natural quiet NEUTRAL standing pose. Preserve exact character identity, face proportions, slender build, pointed ears, hair design, outfit details, material colors, staff design, clean delicate linework and polished soft anime shading.
POSE: three-quarter body and face looking toward screen RIGHT. Both boots firmly planted on the same baseline, feet comfortably about hip-width, knees nearly straight but unlocked, balanced weight, upright relaxed torso, level relaxed shoulders. Closed mouth with a gentle attentive small smile, eyes looking right rather than at viewer. One arm loosely down beside body holding the staff at hip height, relaxed elbow, the staff shaft angled gently down so its lower gold point is beside the boot and flame finial is around shoulder to face height, never above the head. The free hand rests relaxed close to the waist, no pointing, reaching, waving or incantation. Keep staff full length and physically coherent; use a low grip appropriate to staff's balance rather than holding it aloft. Long red ribbons and cape panels fall mostly downward by gravity, only small soft curves, hair settles naturally. Silhouette quiet, stable, easy to animate breathing and slight cloth motion. NOT a walking step, not a wide battle crouch, not a casting pose.
Composition: one character, portrait canvas 1024x1536, full body including hair staff and both boots fully in frame. Centered, ample safe margins, boot soles around y=1495, head around y=120, consistent reference head-to-body ratio. Nothing cropped. Actual transparent alpha background and transparent internal gaps, character interior opaque. No ground plane, shadow, glow, aura, fire effects, background colors, gradient, text, labels, watermark or extra objects. Keep original game sprite style exactly.
```
