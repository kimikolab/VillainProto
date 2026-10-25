# ハギ：大鎌の回転斬りと空中ひねり

2026-10-26。上下に耕すような振り方を、腰から肩まで回して大鎌を横へ運ぶ全身セルへ変更した。通常攻撃は回転横薙ぎ、刈り取りは跳び込み→背面のひねり→空中横薙ぎ→着地。連撃では着地姿勢を保持し、次の回転へつなぐ。

斬撃の軌道は、この後の[水平円弧版](../horizontal-sweep-v1/README.md)へ変更した。このフォルダの動画と武器残像の説明は、変更前の制作記録として残している。

## 確認用動画

- [通常攻撃→刈り取り→連撃、通常速度・ゲーム実音声](video/hagi-acrobatics-1x.mp4)
- [通常攻撃、半速GIF](video/sweep-half.gif)
- [刈り取り、半速GIF](video/reap-half.gif)
- [連撃、半速GIF](video/chain-half.gif)

GPU上で `HagiMovieCapture` を60fps録画したもの。実戦Cue `hagi.live.0.2` のAttack 37、365、225＋241を使う。待機→攻撃→待機まで収録し、編集では切り出し・縮小・再生速度だけを変更した。動作補間や斬撃の後付けは行っていない。各件数と時間は [capture-segments.json](video/capture-segments.json) に記録した。

## 素材と採用

制作手順は [ATTACK_CEL_ANIMATION_GUIDE.md](../../../ATTACK_CEL_ANIMATION_GUIDE.md)。原画は組み込みimagegenで生成した透過2×2シート3枚。参照画像とプロンプト全文は [generation-calls.json](generation-calls.json)、生成原本の場所と採用セルは [generation-record.json](generation-record.json)。

`source-ground.png` は構えと地上接触の2姿勢を採用した。残る背面2姿勢は衣装の被覆が不足したため採用していない。`source-air.png` と `source-connect.png` は4姿勢ずつ採用。空中・背面では衣装の下に不透明な黒いショートパンツを描き、下着の露出を避けた。銀の刃、根元の穴1個、黒・くすんだ赤・グレー・金の配色を維持した。

全身セルをGodotで一体のまま抽出・等比縮小し、頭頂〜顎124pxを基準にした1920×960の共通キャンバスへ配置した。しゃがみを囲みの高さで拡大せず、足元・跳躍の仮想接地・刃の根元・先端・刃の輪郭を同じ座標変換に通している。部位の貼り替え、ワープ、静止絵の回転、逆再生、クロスフェードは使用していない。

本番は `DemoApp/assets/portraits/battle/attack_clip/hagi_*_v1/` の4クリップ、合計61コマ。保持を含み、原画は10姿勢。30fpsで通常12、刈り取り19、連撃前半16、連撃後半14コマを再生する。ゲームの左右反転・停止・倍速を使う。

赤い白芯＋炎状オーラの刈り取りエフェクトは既存版を継続し、溜めの光と武器の残光を新しい刃先の追跡点へ合わせた。着地・待機・次の打撃へ残光をつながない。購入済みNatureBlocks/EffectBlocksの一覧と実ファイルを確認し、既存の光粒・リングを継続利用した。購入素材の原本には変更を加えていない。

## 接続と検証

`BattlefieldView3D.Hagi` が実台本の一打に対応するクリップを選び、contactのコマで命中と音を一度だけ出す。命中前の中断・死亡・再戦では接触を出さない。BattleCore、ダメージ、標的、打数、生成済みdocsには変更を加えていない。

- `dotnet build DemoApp/DemoApp.csproj --no-restore -v:q`：警告0・エラー0。
- `HagiPresentationCheck`：9種類の実戦台本で打数・刈り取り数・最終HP一致。連撃保持と帰着、足元と画素スケール、停止・倍速・反転・中断・死亡・復活・再戦・シークを確認。
- `IdleLoopCheck`：採用済みハギ48コマを含む待機・攻撃切替、死亡・復活・勝利・反転が成功。
- `RicaMotionCheck`：共通の連番再生変更に対して520検査成功。
- GPU録画：通常1打、列の刈り取り1打2体、連撃2打2体で正常終了。

headless検査は成功後のGodot終了時にRID/ObjectDB解放の警告を出す。検査本体の失敗・再生例外はなく、GPU録画は警告なしで完了した。検査ログはローカルの `output/hagi-acrobatics/` に保存している。

## 再現

リポジトリ直下で実行する。書き出しは既存連番を上書きしないため、再制作時は版名と出力先を分ける。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiPresentationCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 60 --write-movie 'C:/works/VillainProto/output/hagi-movie/raw-acrobatics-v1-final.avi' res://HagiMovieCapture.tscn -- --hagi-acrobatics --hagi-movie-dir=res://../design/art/hagi/attack-acrobatics-v1/video
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/attack-acrobatics-v1/encode_preview.py --raw output/hagi-movie/raw-acrobatics-v1-final.avi
```
