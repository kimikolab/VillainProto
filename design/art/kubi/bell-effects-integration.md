# 萎縮のクビ — 鐘の震え・鐘音・萎縮の波紋

鐘番の立ち絵を使うクビの手番演出を追加。戦闘ルール・数値・通知の対象や件数は変更していない。

## ゲーム内の動き

- 既存の萎縮の付与通知を束ねた1組につき、書き手のクビが鐘を押さえながら0.62秒だけ小さく震える。席は動かさず、縮む分を靴底で補正する。
- 鐘口の座標（右向き原画の670,645）から淡い銅金の楕円波紋と短い光が漏れる。敵側の左右反転は既存の立ち絵座標変換に従う。
- 既存通知の実際の受け手へ短い音の弧を描き、胸元で内側へ縮む輪を添える。味方への漏れは薄紫。顔やHP表示を覆う煙・大きな閃光は使わない。
- 敵味方の付与と縮こまりは既存どおり同じ拍で始まる。波紋は表示のみで、飛翔による命中判定・対象選び直し・追加の待ち時間を持たない。
- くぐもった短い鐘音を1回だけ鳴らす。4つの減衰する正弦波からAudioStreamWavを作るため、外部音源や戦闘側の乱数を使わない。既存の戦場音バスに接続する。
- 既存の萎縮の印・次の一撃の消費と解除を利用する。常時の待機モーションは追加していない。

## 接続と時計

Main.Kubiが連続する同じ書き手・ターンの実際の萎縮付与を束ね、1組1回ShowKubiBellを呼ぶ。対象選択・付与の処理や待ち時間を追加しない。
KubiBellFx3DはNode3Dを継承し、既存の停止・倍速時計で進む。共通デバフ演出が未導入の版でも独立して動く。導入済みの場合も鐘を二重に鳴らさない。
クビの震えも同じ時計を使い、停止中は姿勢を保持する。鐘音のStreamPausedとPitchScaleを時計に同期する。
死亡・勝利・再戦・編成帰還で音と波紋を消し、震えもリセットする。
共通デバフ演出のSourceの無い旧診断用呼び出しでは従来の共通萎縮音を使用する。

## 購入素材の確認

原本: D:/Assets/GodotAssets/EffectBlocks v4/PolyBlocks/EffectBlocks/（Bukkbeek、導入情報はGodotApp/ASSET_RESEARCH_AND_PLAN.md）。

- source_files/textures/sparkle.pngを鐘の縁の光1枚に採用。既存HagiLight3D.Sparkle経由で共有し、未導入環境では既存の光画像へフォールバックする。
- assets/other/shockwave.tscnを確認。GPUParticles3Dの0.74秒のTorusMeshで、広い衝撃波に見えることと、購入シーンの反復に依存しない短い音の弧が必要なためシーンは使わない。
- assets/energy/electric_waves.tscn、assets/other/sparkles.tscnも確認。電気の面や自動反復する粒子は採らず、波紋を専用メッシュで描く。
- 購入原本・共有リソース・素材本体のGit管理は変更しない。

## 確認

- DemoAppビルド成功、警告0・エラー0。
- コミット対象のソースだけを別フォルダへ取り出した状態でもビルドとKubiBellCheckを確認（BattleCoreの既存警告2件）。
- KubiBellCheckを独立した検査として追加。本番の通知入口・1組1回・停止・音と姿勢の保持・4倍速切替・左右反転・死亡・勝利・再戦・中断を検査し、KUBI_BELL_STANDALONE_COMPLETE ok=Trueを確認。
- DebuffCheck: 試遊・呪い／試遊・カーズ 呪い × ボス／第4波の完走と再戦。元台本・表示台本の不変、最終HP、付与件数を照合。
- 各実台本で、クビの付与1組につき鐘が1回だけ鳴ることを検査。
- KUBI_BELL_CHECK_OK: 実対象・重複排除・敵味方の同時萎縮・波紋と震えと音の停止・途中の4倍速切替・左右反転・死亡・勝利・再戦・中断・編成帰還を確認。
- 既存の呪符・漏れ・カーズの刻印・ウツの連撃・シークの検査も通過。
- BattleSim compareの前後で表68行（66編成とヘッダー）が一致。戦闘バランスの差分0。
- GPUで左右両陣営×1倍／4倍を撮影し、顔・HP・接地と鐘口の位置を目視確認。画像はeffects-game-check/kubi-bell-{player|enemy}-{1|4}x.png。
- 終了時の既存と同種のRID/ObjectDB解放警告は残る。実行中の検査・描画は成功。
- ログ: .tmp/kubi-effects/debuff-check-final.log、capture.log、compare-before.md、compare-after.md。

```powershell
dotnet build DemoApp/DemoApp.csproj -v minimal
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://KubiBellCheck.tscn --audio-driver Dummy
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://DebuffCheck.tscn --audio-driver Dummy
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://DebuffCheck.tscn --audio-driver Dummy -- --capture-dir=C:/works/VillainProto/design/art/kubi/effects-game-check
```
