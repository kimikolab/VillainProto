# 3D の作戦マップ —— 素材の入れ方と土台の口

発注は `BRIEF_MAP3D_STAGE11.md`。ここは「素材をどこに置くか」と「土台が用意した口」の 1 枚。
購入素材の一覧・利用条件・これまでの採用記録は `GodotApp/ASSET_RESEARCH_AND_PLAN.md`。

## 素材（コミットしない）

- 製品: Bukkbeek「PolyBlocks / NatureBlocks v1」（Godot 4.6 用・再配布禁止）。DemoApp は Godot 4.7.2 で取り込んで使えている
- 原本: `D:\Assets\GodotAssets\PolyBlocks-NatureBlocks-[v1]`（機械ごと。場所が違えば `-AssetRoot` で渡す）
- プロジェクト内: `DemoApp/PolyBlocks/NatureBlocks/`（`.gitignore` 済み・`res://PolyBlocks/...` の道は原本のまま）
- 入れ方（作業ツリーごとに 1 回。何度走らせてもよい・既にあるフォルダは上書きしない）:

      powershell -ExecutionPolicy Bypass -File DemoApp/PrepareAssetPreview.ps1
      Godot_console.exe --path DemoApp --headless --import

  写すのは `assets/`（木・茂み・岩・崖・山・環境）・`source_files/`（メッシュ・マテリアル・シェーダ）・`tools/`（道具）。
- **原本と共有マテリアルは書き換えない。** 色や大きさは実体か複製したリソースで変える（`CONTRIBUTING.md` の背景・演出の節）

## 道具（`tools/`）

GDScript の `@tool` だが、実行時に C# から呼べる（4.7.2 で確かめた・`--map3d-tool-check`）。

| 道具 | 場面 | 渡すもの | 呼ぶ関数 |
|---|---|---|---|
| 道 | `tools/generator_road/nb_gen_road.tscn` | `road_path`（Path3D） | `generate_road` |
| 川 | `tools/generator_river/nb_gen_river.tscn` | `river_path`（Path3D） | `generate_river` |
| 湖・崖・山 | `tools/generator_lake` ／ `_cliff` ／ `_mountain` | Path3D | 各 `generate_*`（土台では未確認） |
| ばらまき | `tools/scatter_*`（草・花・茂み・木・地面の石ほか） | —— | 土台では未確認 |

幅・高さ・マテリアルは道具の書き出しの値（`road_width` ほか）で変える。既定の幅は 1-1 の盤には太すぎる。
C# からの呼び方の見本は `Map3DStage.Generate`（`Map11Main.Map3D.cs`）。

## 土台が用意した口

| 口 | 何をする |
|---|---|
| `--map3d` ／ F2 | 3D の層に切り替える（既定は 2D）。F2 は場面を読み直すだけで、通しは `Map11Session` に残る |
| `Map3DMode.Active` | 画面の側が読む 1 本。3D を選んでいて素材があるときだけ真。素材が無ければ 2D のまま（`MAP3D_MODE ... reason=no-assets`） |
| `--map3d-no-assets` | 素材が無い環境の真似（2D に落ちることの確かめ） |
| 画面の基準 | 3D の層にいる間だけ 1920×1080・縦横比を保って窓の全面を使う。戦闘・編成（2D の経路）は `project.godot` の 1440×900 のまま |
| `Map3DStage` | ジオラマの根（土台では空）。地形・道・駒はこの下に置く |
| `Map3DDebugLayer` ／ F3 ／ `--map3d-debug` | 検証用の UI の層（既定は隠す）。seed・作戦ターン・fps・カメラの値。検証の表示はここに足し、遊ぶ UI に混ぜない |
| `--map3d-tool-check` | 道と川を 1 本ずつ生成する確かめ。合否は `MAP3D_TOOL_CHECK_COMPLETE` |
| `Map3DMode.CompareSeed`（11） | 2 本の担当が写しと fps を揃える seed |

写しの撮り方（1920×1080・窓で）:

    Godot.exe --path DemoApp --resolution 1920x1080 --quit-after 900 res://Map11Main.tscn -- --map3d --map-seed=11 "--map11-capture=<出力>.png,reform"

## 土台がやっていないこと

- 地形・道・駒・光・カメラ・UI の配置（見た目の段）。土台の 3D は 2D と同じ盤に検証用の層が付くだけ
- `project.godot` の基準の変更。3D を既定にするときに 1920×1080 へ上げ、2D の画面の文字と画素の決め打ち（発注書 §7-3 の 3）を一緒に直す
