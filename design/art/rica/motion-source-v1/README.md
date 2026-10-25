# リィカの段階別モーション元絵・オーラ素材

2026-10-24。採用した `../attack-rough-v1/` の攻撃構成と、待機時にも身体の覚醒を見せる合意に基づく素材一式。

この20枚を使った後続のモーション制作・ゲーム接続は [`../attack-motion-v1/README.md`](../attack-motion-v1/README.md)。
本記録は元絵を渡した時点の範囲。現在のゲーム用連番は後続の記録を参照する。

## 作成物

元絵は `DemoApp/assets/portraits/battle/motion_sources/rica/v1/`。**透過PNG 20枚（待機4＋攻撃16）**。

| 段 | 待機の違い | 攻撃キー（再生順） |
|---|---|---|
| 0–2層・stage0 | シャベルを低く両手で持つ | windup → hit → finish |
| 3–5層・stage1 | 腰を落として横構え | windup → hit → finish |
| 6–8層・stage2 | 肩へ引いた構え・顎を上げる | windup → rise → return → finish |
| 9層以上・stage3 | 直立で槍を携える・マントが広がる | windup → thrust → sweep → air_raise → descend → land |

**本体に霊刃・光・残像は描き込まない。** 木の柄・Dグリップ・金属のシャベルを描いた本体に、別の武器・身体エフェクトを重ねる。顔、衣装、物理シャベルを各段で保持する。

作成は内蔵 `image_gen`。元画像は `DemoApp/assets/portraits/battle/rica_idle_right.png`、構えと動作の参考は承認済みのラフ4枚。最終出力ごとの生成指示・生成原本のパスは `prompts.json`、最初の構想は `generation-spec.json`。

## 動画化へ渡す情報

`manifest.json` は素材フォルダー内に置いた。段、動作順、命中キー、武器形状、配色、シャベルのソケット・先端・両手・接地・目の基準点を含む。

- 待機は **1024×1536（2:3）**、攻撃キーは **1254×1254**。各ファイルのキャンバスと余白を保った透過素材で、画像全体の高さだけで人物を拡大・縮小しない。
- アンカーは元絵を目視した**正規化UVの初期基準**。動画化後は各フレームでシャベルと手を追跡し、霊刃の長さ・向き・重なりを合わせる。最終の追跡データとして流用しない。
- 武器の軸は `weaponSocket → weaponTip`。木の柄の根元や画面中央から発光させない。
- 二連撃は `rise` と `return` がそれぞれ実際の命中。三連撃は `thrust`、`sweep`、`descend` が命中で、`air_raise` は3打目の予備動作。命中キー名を連番のフレーム番号と取り違えない。
- 攻撃動画は一撃ごとに区切る。**台本の次の標的への移動は再生側**で行い、動画に敵や背景・長い移動を描き込まない。命中コマを実際の生成後に決め、既存 `BattlePawn3D.AttackClip.cs` の `contact` へ渡す。
- 胴・顔・武器を独立に止めた貼り合わせや、静止画のクロスフェードを完成した攻撃モーションとして扱わない。今回の画像は動画生成の元絵で、フレーム連番ではない。
- 待機モーションは `design/IDLE_LOOP_COMFYUI_GUIDE.md` の既存 ComfyUI 手順を使う。今ある通常リィカの採用待機ループは変更していない。

## 分離したエフェクト素材

`DemoApp/assets/fx/rica_motion_v1/`:

- `body_aura.gdshader` — 本体アルファの外へ淡い輪郭光。顔の付近を空け、マントと肩から下へ流れる。
- `soul_veil.gdshader` — 背中・両側・足元から立ち上る2本の霊気。段が上がると濃くなる。
- `weapon_aura.gdshader` — 三日月・大剣・大槍の3形状。大剣は平行な両刃と三角の先端、大槍は細長い穂先。対応する `crescent.tres` / `greatsword.tres` / `lance.tres` も付属。
- `soul_light.svg` — 購入素材未配置時に使う自作の光粒。

シェーダーは **canvas_item の確認用レイヤー素材**。位相 `phase` を外から渡すので、速度変更・停止・覚醒時の間に追従できる。Godotの自動 `TIME` には依存しない。3D本編へ接続する際は、既存のビルボード描画に同じ形・軸・位相を渡す。

武器は本体より後ろに置き、元の金属と木を常に手前に見せる。光を武器に焼き込まないため、最終段で大槍から大剣へ切り替えても物理シャベルは同じまま。

購入済み素材は EffectBlocks v4 の `other/sparkles.tscn`、`fire/fire_magic.tscn` と `source_files/textures/sparkle.png` を確認。小さな光粒のみ購入の `sparkle.png` を利用し、霊刃と身体の流れは再生位相を統一するため専用シェーダーで作成した。原本・共有材質は変更せず、購入素材そのものをGitへ含めない。

## 確認画面と出力

`DemoApp/RicaMotionAssets.tscn` が20枚とエフェクトを確認する独立シーン。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://RicaMotionAssets.tscn
# 検査
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaMotionAssets.tscn -- --verify-assets
# 同じ描画を撮影
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --windowed --resolution 2000x1250 --rendering-method gl_compatibility res://RicaMotionAssets.tscn -- --capture-assets
```

0＝待機比較、1–4＝各段、左右＝1枚ずつ、E＝効果切替、P＝効果停止、Space＝キーポーズ順送り。順送りは静止キーの確認であり補間アニメーションではない。

`idle-stages-fx.png` が待機4段の比較、`stage0-keys-fx.png` 〜 `stage3-keys-fx.png` が各段の合成見本。`idle-stages-base.png` と `stage*-contact.png` は光を重ねる前の原画確認。

## 検証結果と範囲

- RGBA20枚、隅の透過、宣言したキャンバス寸法、基準点の範囲、武器軸の存在を確認。
- 全段の身体・武器レイヤーを実描画し、シャベルの芯が見えることを目視確認。
- 効果の位相が停止中に進まず、再開で進むことを確認。`RICA_MOTION_ASSETS_OK poses=20`。
- 本番の戦闘画面への差し替え、待機ループの新規生成、攻撃動画生成、連番の本番接続はこの素材作成には含めていない。モーションの元絵として渡せる状態まで用意した。
- BattleCore・BattleSim・docs、および既存のキャラ絵と採用済み待機ループは変更していない。
