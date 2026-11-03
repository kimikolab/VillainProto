# 作戦マップ追記 A1〜A4・B1〜B5 の実装（2026-11-03）

対象は `CODEX_BRIEF_MAP_SCREEN.md` 末尾の追記。着手時に第447期 `0de87274`、第448期 `86b0b28c` のコミット済みを確認した。

## 実装

| 項目 | 内容 |
|---|---|
| A1 | 通常兵10種＋粛・渇き・軛・告発人の4種の顔。`UnitFaceIcon` が専用画像を読み、地図・接敵・結果など既存の共通部品から利用。通常兵の数値違いIDは同じ画像へ対応付ける。未用意の画像は従来の立ち絵／波の印へフォールバック |
| A2 | `burn.svg`（燃焼）・`poison.svg`（毒）の2枚。火・霧の輪郭を小さく読めるSVGで作成。第449期で札に載せるためのアセットを準備し、現行の敵に将来の特性を推測して付けてはいない |
| A3 / B5 | 砦・浅瀬の野営地・関所、赤い敵色／青緑の味方色の6枚。ひし形の台を絵に替え、`HeldOutposts` で差し替える。中間拠点の名札・＋8は保持し、行き先や予備隊と重ならない位置へ移した |
| A4 | 既存の軛・渇きの効果に黄／緑の鎖を追加。開戦・召喚の `DemoOpening.Traits` の保持者から、実際の `Sealed` の対象へ結ぶ。保持者の死亡でその保持者の鎖だけ砕け、対象死亡・蘇生・再戦・再生停止・倍速にも対応。粛の鎖の部品を共用 |
| B1 | 行き先ボタンのホバーで経路線・矢印、現在その経路にいる敵の輪、移動の刻数。確定した経路も停止中は表示。`MapGraph.Path` と経路の辺数を読み、ホバーは行き先や盤面を書き換えない。敵拠点で待つ予備隊も表示。`CanWarp` が真の行き先は直通1刻として表示。帰還先では回復・補充の短い案内 |
| B2 | `GoldLog` の新しい場所付きの収入に金貨が弾けて上部の金の表示へ集まる演出。既存の＋数字・敵の＋6／＋8は保持。同じ記録は二度出さず、新しい通しは表示済み件数をリセット |
| B3 | 無料枠が残る間は出撃より候補の札を優先して光らせ、候補までスクロール。選ぶと通常の編成案内へ戻る。候補にも共通の顔を表示 |
| B4 | `IsCross` の辺を床板・欄干付きの細い板橋へ。主街道の土の帯、拠点間ワープの点線と形で区別 |

移動の刻は「現在位置からの移動の辺数」。敵の未来の移動、戦闘の結果、勝敗の助言は予測しない。経路上の敵は現在位置を表示する。

## アセットと購入素材

- 顔: `DemoApp/assets/map/enemies/`（14 PNG）。
- 地形: `DemoApp/assets/map/terrain/`（3種類 × enemy / ally、6 PNG）。透過アルファを確認。実行時は384×256へ縮小して共用する。
- 帯Bの印: `DemoApp/assets/map/status/`（2 SVG、64×64）。表示名は「燃焼」「毒」。
- 画像は組み込み `image_gen` で生成。味方色は敵色の画像から布・旗を変更。原画像は保持し、ゲームが参照する全画像をリポジトリ内へコピーした。プロンプトと各ファイルの対応は `design/art/map-addendum/prompts.json`。
- 購入済みNatureBlocksの実ファイルと `GodotApp/ASSET_RESEARCH_AND_PLAN.md` を確認。今回の砦・野営地・関所に使う建物一式がないため専用画像を作成した。
- EffectBlocks v4 の `impacts/impact_3.tscn` なども確認。今回必要なのは持続する鎖・その破砕と2D HUDの金貨で、既存の `HushChain3D` とGodotの描画を使う。購入素材の原本は変更していない。

## 検証

| 検証 | 結果 |
|---|---|
| DemoApp Debug / BattleSim Release ビルド | 成功。全体再ビルド時の既存警告はBattleCore 2件、BattleSim側を含むRelease 57件、エラー0 |
| `--map-addendum-smoke` 1-1 / 1-2 | 両方成功。ホバーの実入力、経路表示中に命令・時刻が変わらないこと、横断1刻、14顔、2印、所有者による画像変更、無料選択、金貨の重複抑止・消去 |
| `--map-layout-smoke` 1-1 / 1-2 | 両方成功。編成・作戦切替、顔の交代、接敵、結果、再戦を含む |
| `--map-guide-smoke --map-stage=1-2` | 成功 |
| `--map11-clock-smoke` / `--map11-graph-smoke` | 成功 |
| `--map12-smoke` / `--map12-change-smoke` | 成功。第448期を含む現行1-2総合検査も完走 |
| `RuleChainCheck.tscn` | 成功。複数保持者・黄/緑の別々の解放、停止・2倍速、移動、対象／保持者の死亡と蘇生、再戦時に破片まで破棄 |
| `SealEffectCheck.tscn` | 成功。粛の鎖の既存検査 |
| `compare` / `dump` | 一時出力と現行 `docs/balance.md` / `docs/units.md` の差分0行 |
| `audit` | ずれているファイル0件 |
| 規則ファイル | Map11 / Map11Orders / Map11Session / MapGraph / MapStage は作業前後のハッシュ一致。BattleCore / BattleSim / docs の差分もなし |
| 描画 | Forward+、1440×900と1280×720で確認。実画面は `output/map-addendum/`、ログは `.tmp/map-addendum/` |

`--map11-verify` の800 seed評価は引き続き `ok=False`。第168期の数値目標に対して正逆とも踏破100%、2本抜き100%、挽回0%となる既存不合格で、`MAP_SCREEN_GUIDE_IMPLEMENTATION.md` にある第441期の対照実行と同じ結果。UIだけの依頼なので期待値・規則は変更していない。

## 確認方法

```powershell
dotnet build DemoApp/DemoApp.csproj
& 'C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe' --path DemoApp res://Map11Main.tscn -- --map-stage=1-2
# 操作検査と画面保存（1-1は --map-stage を省略）
& 'C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe' --path DemoApp res://Map11Main.tscn -- --map-addendum-smoke --map-stage=1-2 --map-addendum-capture=C:/works/VillainProto/output/map-addendum/1-2
# 封じの鎖の確認シーン（検査後に終了）
& 'C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe' --path DemoApp res://RuleChainCheck.tscn -- --capture-dir=C:/works/VillainProto/output/map-addendum/chains
```
