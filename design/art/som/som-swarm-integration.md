# ソム：群れ・緊急下僕・萎縮の演出（2026-10-11）

`CODEX_BRIEF_SOM_SWARM.md`／`CODEX_BRIEF_SOM_SUMMONER.md` の絵の意図を、`PHASE291_CODEX_MEMO.md` §14-7〜14-9 の規定台本に接続した。変更はDemoAppの表示と素材、実装記録のみ。BattleCoreの規則・数値は変更していない。

## 実装

- `SomPresentation` が隣り合う `Summon`／`BeastBurst` を一団として索引化。最初の召喚で全匹を出し、一度の走りの後に一斉に弾ける。後続の同じ団の通知では演出を重ねない。深さ0の起点が複数あっても同じ連鎖の光をすべて回収する。
- `SomBeast3D` は表示専用の置物。盤面の駒・HPバー・陣営札を追加しない。弾けた後は姿を消し、放電・光・鞭の収束が参照する位置だけを戦闘終了まで保持する。元の席番号・対象・台本順は維持。
- 通常は小型猫、緊急は獅子型の大型下僕。既存の得意げ／驚き差分を再利用し、「危険の赤い鼓動 → 味方の前の魔法陣と堂々とした下僕 → 裏切りの走り → 救援」の4拍を描く。走り0.62秒、通常の振り返り0.07秒（初回・群れの増加は0.20秒）、緊急0.32秒。台詞は初回・過去最大の群れ数更新・緊急だけ。
- `Spark 降る` の `TargetId` があるとその1体へ直接集光。`TargetId` なし／`PartnerId` ありでは、その相手へ寄った光が紫の輪で散って全員へ降る。回復・衣は元の通知で更新する。
- 既存の `StatusGain daunted` の印をそのまま使い、短いよろめきを追加。次の `Attack` だけ縮んだ構えと「萎縮 · 威力½」の札を出して印を消す。攻撃力・ダメージを画面側で算出しない。
- 粛で止められたソムは召喚の表情だけ出し、魔法陣を作らず既存の封印・ひびの表示に渡す。台本に緊急／手番の区別がない `Sealed` から原因は推測しない。
- 群れの走りと4拍の待ち、ソムの表情・光・衣・音は一時停止に対応。走行と主要な光のTweenは途中の速度切替にも追従。終了・編成帰還・再戦で置物、粒、衣、表情を消去。

## 素材と購入素材の確認

大型下僕の透過PNG2枚を `DemoApp/assets/portraits/battle/fodder_majestic_{lookback,run}_idle_right.png` に配置。既存の猫のスタイルを参照し、組み込み image_gen で作成。プロンプトは [som-emergency-v1-prompts.md](som-emergency-v1-prompts.md)。危険の縁は既存SVG素材と同じ方式の `DemoApp/assets/fx/som_danger.svg`。

購入済みEffectBlocks v4の実ファイルを確認した。`energy/electric_sparks_1.tscn` を獣の爆発に採用（8粒・寿命0.24秒・青白い電気）。処理材を発動ごとに複製し、原本・共有素材を編集しない。配置は `DemoApp/PolyBlocks/EffectBlocks/`、原本は `D:/Assets/GodotAssets/EffectBlocks v4/`。購入素材本体はGitに含めない。

`other/portal_magic.tscn` は大きな常駐ポータルで、短い召喚と顔の視認性に合わないため不採用。`other/sparkles.tscn` は回復の光と役割が重なるため不採用。魔法陣・光滴・衣は既存の薄いSVGを継続。購入素材がない環境では既存の電弧による爆発だけを描く（素材を外した実行は未検証）。

## 検証

Godot 4.7.2／Forward+／1440×900。`SomCheck` でseed 0の実台本を初戦・再戦の2回ずつ完走し、召喚・爆発・光・衣・萎縮・集光・散光の件数と全駒の最終HPを照合。

| 台と相手 | 走りの団 | 獣の爆発 | 最大群れ | 緊急 | 集光 | 散光 | 萎縮 | 弱まった攻撃 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 光の盾 鞭 × ボス | 19 | 81 | 5 | 0 | 0 | 0 | 16 | 16 |
| 光の盾 重 × 大隊 | 7 | 12 | 3 | 2 | 2 | 0 | 14 | 3 |
| 光の盾 × ボス | 23 | 100 | 5 | 0 | 0 | 0 | 45 | 42 |
| 光の盾 × 近衛 | 7 | 19 | 4 | 1 | 1 | 0 | 12 | 6 |
| 光の盾 × 第2波 | 4 | 7 | 2 | 0 | 0 | 0 | 4 | 1 |
| トウ・クグ・ベニ・カタ・ソム × 近衛 | 9 | 18 | 4 | 4 | 2 | 2 | 0 | 0 |

ソム不在の「雷の型」では専用召喚・光・衣0件。群れの5起点を1連鎖として数える最小台本、次の一撃だけ萎縮を表示する索引、終了／再戦／編成帰還の消去も通過。

左右両陣営・1倍／4倍の群れ、緊急、散光、萎縮を撮影して目視確認。走行途中の一時停止で位置が固定され、途中の倍速切替で全匹が到着。旧来の小型猫・雨・衣の描画確認も通過。画像はGit除外の `design/art/som/game-check/swarm/`、ログは `output/som-swarm-*.log`。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe' --headless --path DemoApp res://SomCheck.tscn -- --verify-only
& 'C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe' --path DemoApp --windowed --resolution 1440x900 res://SomCheck.tscn -- --visual-only --swarm-only
```

通常のDemoAppビルド成功。`SOM_SWARM_PLAN_OK`／左右速度4ケースの `SOM_SWARM_VISUAL_OK`／実台本の `SOM_REPLAY_OK`／`SOM_CHECK_COMPLETE` を確認。ヘッドレス終了時のFont／CanvasItem解放警告は既存の確認シーンでも記録されているものが残る。描画検証はエラーなし。

BattleSimの `compare` は `docs/balance.md` と差分0行、`audit` はずれているファイル0件。`dump` の追加3行は作業中に別途変更されたBattleCoreの `SharePush`／`SharePushPower`／`ShareSin` の列挙で、ソムの演出の差分ではない。`docs/` は変更していない。
