# ハギの大鎌・演出追加

2026-10-25。`design/CODEX_BRIEF_HAGI.md` の見せ方を DemoApp に実装。
黒・くすんだ赤・グレーの採用済み立ち絵を維持し、エフェクトは銀白。
BattleCore・ダメージ・標的選択・戦闘の乱数には変更を加えていない。

## 反映したもの

- 通常斬撃は小さな銀の弧。刈り取りは振りかぶって静止 → 大きな弧 → 光粒への消散と大きな銀色の実ダメージ数字。
- 敵のHPバーに、実際の刈り取り線の位置を示す小さな鎌。線以下になると銀の輪郭。傷は本数で表し最大5本。
- 追撃は短い滑り込み。追撃の刈り取りは同じ大きな弧を短い溜めで返す。
- 魂の実数に合わせ最大9個の銀白の欠片。3・6・9で刃が段階的に伸びる。減衰する欠片は静かに消える。
- 敵の死からハギへ光粒が流れる。連続攻撃では振り抜き姿勢と踏み込み位置を保持。列刈りは実際の対象を一振りで覆う。
- 味方の巻き込みは小さい銀のかすり傷。盾・肩代わりの通常反応を刈り取りへ重ねない。
- ハギの既存Highlight字幕も戦闘ログだけへ送り、技名・説明のテロップやその待ちで連続する振りを切らない。

## 素材

`hagi_windup_right.png` と `hagi_follow_right.png` は ImageGen で採用済みの一穴版から作った動作差分の原本。
ゲーム用の同一PNGは `DemoApp/assets/portraits/battle/hagi_windup_idle_right.png` と
`hagi_follow_idle_right.png`。足元は刃先ではなく靴の接地で補正する。
短いボブ、握った両手、覆われたスカート、灰色の後ろ布、大きな楕円穴1つを引き継ぐ。
血・鐘・ぶら下がる鎖・紫の配色は追加していない。生成時のプロンプトは `generation-prompts.json`。

購入素材は `GodotApp/ASSET_RESEARCH_AND_PLAN.md` と EffectBlocks v4 の実ファイルを確認した。

|候補|判断|
|---|---|
|`source_files/textures/sparkle.png`|採用。魂の欠片、消散、吸収に使用。色・寸法・密度・寿命を実再生時計で制御|
|`assets/other/sparkles.tscn`|自動反復を持つためシーン丸ごとの利用は見送り。テクスチャを共有|
|`assets/impacts/impact_1.tscn`|円形の衝撃・火花が主体。鎌がすり抜ける横の一振りには合わないので見送り|

円弧と伸びる刃はシェーダー／メッシュで描く。購入素材の原本は改変・追加していない。
EffectBlocksが未導入の環境では既存の `death_light.svg` を光粒の代替に使用する。

## データと再生

`DeathPresentation` の既存 CounterProbe に読み取り専用の `HagiPresentation` を接続。
魂・印・刈り取り線は戦闘中の実値の写しを保存し、画面側で増減の規則を再実装しない。
刈り取りは `ReapBody388` の構造（同じActorのHighlight直後のDamage）を索引にし、日本語ログは解析しない。
列の刈り取りだけを同じ接触時刻へ寄せる。元の台本・Death・回復の順序は維持する。
イベント構造への依存は、実戦9本で `R390ReapKills` と全件一致する検査で保護する。

時計は停止・速度変更に追従。中断・再戦では世代を破棄し、古い接触や動作を残さない。
併せてリィカの時計が完了済みTweenを毎イベント再開していた箇所を、時計が実際に止めたTweenだけ再開するよう修正した。

## 確認方法

`FxViewer.bat` → 検索「ハギ」→「死・ハギの大鎌」。指定3編成×3波の9本を登録。
精鋭のHP・攻撃倍率も `EnemyCatalog.PlaytestStages` の実値を使う。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --path DemoApp res://HagiPresentationCheck.tscn
Godot_console.exe --path DemoApp res://HagiPresentationCheck.tscn -- --capture
Godot_console.exe --path DemoApp res://HagiPresentationCheck.tscn -- --real-2x --capture
```

確認画像は `captures/`。`souls-0/3/6/9` は銀白の段とリィカの青白／紫の比較、
`windup`・`column-contact` は振りかぶりと列の消散、`mirrored-pursuit` は敵側。
`hagi.live.*-2x-*.png` は2倍速の実戦。

## 検証

- DemoAppビルド成功。
- 3編成×ボス・近衛・大隊の9戦で、観測あり／なしのイベント全体・勝敗・ターンが一致。
- 魂の最大値・印と魂の最終値・刈り取り件数がBattleCoreの実値／集計と一致。
- 実再生の振りの件数・刈り取り件数・全駒の終了時HPが実戦と一致。
- 魂と振りかぶりの停止、倍速、左右反転、中断、再戦、勝利、シークを確認。
- `compare` と `dump` の再生成結果は `docs/balance.md`・`docs/units.md` と完全一致（改行を正規化して比較）。`audit` はずれ0件。
- リィカ／死軸の既存 `DeathAxisCheck` も実行。

Godot終了時の既存RID／ObjectDB解放警告は残る。1000倍速でシークと再戦を繰り返す検査では、
既存の `GatherCast3D.Sound` のリソース再ロードにGodotのGCHandle警告が出る場合がある。
演出台本の完走・件数・HPの検査結果とは分けて記録する。
