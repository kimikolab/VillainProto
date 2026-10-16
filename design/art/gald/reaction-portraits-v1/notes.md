# ガルド 被弾・受け流し差分 v1（2026-10-17）

内蔵 `image_gen` を imagegen スキルで使用し、既存の `DemoApp/assets/portraits/battle/gald_idle_right.png` を編集対象として2枚生成。元絵・採用済み待機ループは維持。

- `gald-hit-right-v1.png`: 盾越しの衝撃で上体が後ろへ反り、膝でこらえる被弾姿勢。盾・剣を保持する。
- `gald-parry-right-v1.png`: 上体を前へ乗せ、斜めにした盾で攻撃を外へ逸らす受け流し姿勢。剣は低く保つ。

どちらも1024×1536 RGBA・透過PNG。兜・剣先・盾先・両足が画面内に収まり、左右の装備と肩から手へのつながりを目視確認。画像外周のalpha、寸法、透過領域、ファイルhashは `manifest.json` に記録。背景RGBには生成画像の色が残るが、alphaで透過する。切り抜きや再着色などの画像後処理は行っていない。

購入済みNatureBlocks v1 / EffectBlocks v4の導入記録と原本ディレクトリを確認。背景・粒子演出用で、同一人物のポーズ差分を含まないため元絵の編集を使用。火花・攻撃の軌跡を画像に焼き込まず、キャラクターだけの差分とした。

制作時は比較・確認用の画像をこのディレクトリに保存し、ゲーム本体へは接続していなかった。続くユーザーの適用指示を受け、以下の通り組み込んだ。

## ゲームへの適用（2026-10-17）

`DemoApp/assets/portraits/battle/gald_hit_idle_right.png` / `gald_parry_idle_right.png` へ原寸・画素変更なしでコピー。
既存の `AnimateHit` / `AnimateParry` から `ShowMovementPortrait` を呼び、通常の被弾0.28秒・盾受け流し0.32秒だけ差分を表示する。既存の色変化・動き・受け流しの弧・効果音と同時に表示され、戦闘速度に追従して待機連番へ戻る。体格は表示倍率1.05、靴底は被弾109px・受け流し91pxの下余白で揃えた。

毒では盾の被弾差分を出さない。抜剣時に進行中の盾差分を即座に解除し、剣の受け流しは従来の専用演出を使用。死亡・蘇生・勝利・再戦で差分と復帰予約を解除する既存経路を使用し、BattleCoreは変更していない。

`GaldReactionCheck.tscn -- --verify` を追加。両陣営×1倍／2倍速で、実際の被弾・受け流し入口、シェーダー同期、左右反転、実画像の靴底（呼吸の拡縮込み）、待機連番への復帰、連続通知と古い復帰予約、ノードの停止と再開、毒・燃焼通知、抜剣・膝つき・死亡・蘇生・勝利・再戦を検査。
ノード停止の検査はGodotのProcessModeによる停止を指す。既存の本編の一時停止・速度変更の方式は変更していない。

- Debugビルド成功（既存の `BorgAttackClipCheck.cs:47` にCS8602警告1件）。
- `GALD_REACTION_CHECK_OK` / `IDLE_LOOP_CHECK_OK` / `PORTRAIT_STATE_CHECK_OK` / `MOVEMENT_PORTRAIT_CHECK_OK` / `SPECIALS_CHECK_OK` / `FIRE_CHECK_OK` を確認。
- `SpecialsCheck --verify` は実台本を2回ずつ再生し、抜剣・斬り返しの件数も一致。
- 窓ありの同検査で `game/` へ左右・1倍／2倍の待機／被弾／受け流しを保存。体格・接地・背景透過と、差分の見え方を目視確認。
- SpecialsCheckとFireCheck終了時の既存RID／ObjectDB解放警告は残るが、成功マーカー・終了コード0を確認。

再確認とキャプチャ:

```powershell
dotnet build DemoApp/DemoApp.csproj -c Debug
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://GaldReactionCheck.tscn -- --verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://GaldReactionCheck.tscn -- '--capture-dir=C:/works/VillainProto/design/art/gald/reaction-portraits-v1/game'
```

## 被弾のプロンプト（実際の入力）

```text
Use case: identity-preserve. Asset type: full-body 2D game character damage reaction sprite.
Edit target: the supplied local image gald_idle_right.png. Create ONE pose variant of this exact same helmeted knight Gald for the moment he receives a heavy hit while holding his shield. Preserve his exact identity and the original hand-painted anime game rendering: battered ivory silver plate, the same dark crimson cracks (not new cracks), closed angular helmet with its small pointed crown, black visor slit, maroon and ivory ragged tabard, large pointed ivory kite shield with the same central cross ridge, broad straight sword with the same proportions. Exactly two arms, exactly two legs, exactly one sword and one shield in the SAME hands as the reference.
POSE: facing image right in the same three-quarter view. A brief compact impact recoil, not a death pose: his shield-bearing forearm has been pushed back towards his chest by a strike from the right, shoulders and torso recoil modestly towards image left, knees brace under the weight and his helmet tilts back a little in continuous alignment with the neck and chest. Keep his head attached naturally to his neck. He still looks toward the opponent. The sword stays gripped low in the other hand diagonally down toward image right, never lifted overhead or dropped. Both boots remain on the ground in a broad stable stance. Clear readable hit-reaction silhouette with restrained changes, heavy disciplined knight rather than exaggerated flailing. The shield continues to protect the front; do not completely hide the helmet or torso.
FRAMING: one character only, entire helmet crown, shield tip, sword tip and both boots fully visible. Portrait 1024x1536. Match original body proportions and overall scale; approximately 5% transparent space above crown and below boots. Feet on one consistent baseline around 94.5% canvas height. Camera and lighting match original. No ground, no shadow plane, no scenery, no attacker, no extra objects, no action lines, no dust, no sparks, no text, no added magic or glow. Genuine transparent background, clean alpha silhouette without green fringe. This is a single static pose illustration, not a sprite sheet or multiple poses.
```

## 受け流しのプロンプト（実際の入力）

```text
Use case: identity-preserve. Asset type: full-body 2D game character shield parry reaction sprite.
Edit target: gald_idle_right.png. Create ONE alternate pose for this exact same knight Gald successfully deflecting a strike with his shield. Strictly preserve his identity and hand-painted anime game art: worn ivory silver full plate armor, existing dark crimson cracks, closed angular helmet with the same short pointed crown and black visor slit, maroon and ivory ragged tabard, broad straight sword, large pointed kite shield with its raised cross ridge. Exactly two anatomically connected arms, two legs, one sword and one shield. The weapons stay in the same hands as reference.
POSE: a decisive but compact SHIELD PARRY, facing image right in the same three-quarter view. Lean forward slightly from the hips with the knees firmly braced. The shield-bearing elbow extends forward to the right, rotating the kite shield 25-35 degrees into an oblique angle so an incoming strike would slide off the shield. Show the shield raised in front of the chest with its point down and its convex face still visibly recognizable; it should be visibly different from the passive upright shield of the source. The helmet and torso turn together slightly into the shield, visor looking toward the opponent. The free sword hand stays low near the hip and retains the straight blade diagonally down/right, no sword attack. Both feet firmly planted on one baseline in the original broad stance. Portray controlled redirection of force, not taking a hit or falling backward. Natural continuous neck-shoulder-elbow-wrist anatomy. Keep all armor pieces and weapon shapes rigid, consistent proportions, no extra helmet spike.
FRAMING: one character only, full body from helmet crown to boot soles, entire sword and shield visible inside the canvas with 4-5% margin. Portrait 1024x1536, original overall scale and camera angle, boot baseline around 94.5% height. Match original linework, painterly shading and neutral lighting.
BACKGROUND: actual completely transparent alpha outside the isolated knight and his equipment, including the gaps between legs and arms. No painted backdrop, no gradient, no aura or vignette, no floor or cast shadow. No attacker, no incoming weapon, no sparks, no slash arcs, no dust, no magic effects, no motion blur, no text. Single static character pose, no contact sheet.
```
