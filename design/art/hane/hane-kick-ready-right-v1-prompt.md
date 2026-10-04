# ハネ・蹴り主体の通常戦闘立ち絵 v1

2026-10-04、内蔵 image_gen で生成し、一度は通常時の戦闘立ち絵へ採用。同日の比較で後屈立ち案へ差し替え、本案は `hane-kick-ready-right-v1.png` に保存。
片脚で支え、前脚の膝を上げた蹴りの準備姿勢。両手は胸元へ引き、手で押す印象を抑えた。

- 比較用画像: `hane-kick-ready-right-v1.png`（当初の採用先: `DemoApp/assets/portraits/battle/hane_idle_right.png`）
- 参照: 差し替え前の同画像、`design/art/hane/hane-flying-kick-right-v1.png`
- 形式: 1024 × 1536、透過 RGBA PNG。
- アルファ > 128 の外接範囲: (42, 27)–(947, 1487)。本案を使う場合の下端余白は48px（48 / 1536）。

## 生成プロンプト

Use case: precise-object-edit. Asset type: transparent full-body 2D normal battle idle sprite for Hane. Image 1 is the edit target and definitive identity/costume/style reference. Image 2 is only a reference for the same character's kick-fighter attitude. Replace the hand-pushing stance in image 1 with a poised KICKING SPECIALIST'S IDLE GUARD, facing SCREEN RIGHT. This is a sustained normal ready stance between attacks. One boot planted firmly under the hips as the supporting leg, its knee softly bent; the forward leg lifted with knee bent at about hip height, shin relaxed downward and boot clearly separate from support leg, ready to snap a front kick. Torso upright with a slight natural backward counterbalance. Both elbows bent close to torso, relaxed compact fists near chest for balance, hands secondary and NOT extended or open-palmed pushing. Focused confident closed-mouth expression looking right. Athletic natural anatomy, balanced readable silhouette. Preserve the exact same adult rabbit-eared woman, silver-gray high ponytail, amber eyes, large white floppy rabbit ears with pink insides, rust-red short fur-trimmed jacket, white shirt, dark brown shorts, leather belts/pouch/bracers, heavy fur-trimmed buckled brown boots, tattered red waist sash, detailed softly shaded hand-painted anime fantasy rendering and muted earthy palette. No redesign. Entire character fully visible including ears and both boots, portrait 1024x1536 canvas, comfortable margins, highest ear near y=60 and lowest planted boot near y=1408 to maintain existing sprite scale and ground alignment. True transparent alpha background. Remove ALL dark gradient/backdrop/glow from the references. No floor, cast shadow, aura, effects, other characters, text or watermark. Exactly two legs and two arms. Do not depict an extended attack or airborne jump.
