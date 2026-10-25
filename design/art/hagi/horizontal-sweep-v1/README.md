# ハギ：水平に回り込む斬撃

2026-10-26。ユーザーの[添付参考](reference/user-sweep-reference.png)に合わせ、ハギの周囲を水平に回る円弧へ変更した。

太さは、この後の[三日月版](../crescent-sweep-v2/README.md)で拡大した。ここでは変更前の動画と仕様を残している。

- [通常攻撃→刈り取り→連撃、通常速度・ゲーム実音声](video/hagi-horizontal-sweep-1x.mp4)
- [刈り取り、半速GIF](video/reap-half.gif)
- [連撃、半速GIF](video/chain-half.gif)
- [刈り抜きの静止画](peak.png)

## 形とタイミング

`HagiReapFx3D` が地面と平行な約338度の円弧を描く。中心は攻撃時のハギの位置と跳躍高から決め、接触時に固定する。帰還や着地へ引っ張らず、その場所で余韻を消す。カメラからは奥行きのある楕円に見え、人物の背後に回った部分は立ち絵の深度で隠れる。

手前の白い芯を太く強く、奥を細く淡くした。赤い炎は円弧の内側へ巻き、光粒・火花も同じ半径へ配置する。円弧を約0.105秒で走らせ、刈り取りは0.85秒、通常攻撃は強度を抑えて0.42秒で消す。通常攻撃も同じ水平の軌道を使う。命中光は実際の標的に残す。

以前の敵位置を中心とする板状の放物線と、全身セルの離れた刃先を結ぶ直線状の残像を置き換えた。`RicaWeaponTrail3D` 本体には変更を加えていない。全身原画・攻撃のコマ数・ダメージ・対象・打数・BattleCoreはそのまま。

## 素材と検証

購入済み素材の一覧と `EffectBlocks/assets/fire/fire_magic.tscn`、`circle_1.png` を確認した。周回軌道はコードのメッシュとシェーダーで作り、購入済みEffectBlocksの `sparkle.png` を引き続き使用した。素材原本を変更せず、画像の追加生成も行っていない。

`HagiMovieCapture` をGPU上の60fpsで録画した。実戦Cue `hagi.live.0.2` の通常攻撃37、刈り取り365、連撃225＋241を使う。プレビューはゲームの描画と音を切り出したもので、斬撃の後付け・動作補間は行っていない。収録した件数は [capture-segments.json](video/capture-segments.json)、検査記録は [verification.json](verification.json)。

- DemoAppビルド：警告0、エラー0。
- `HagiPresentationCheck`：9種類の実戦台本で打数・刈り取り数・最終HP一致。
- 追加確認：円弧の中心がハギ、全周が地面と平行、半径一定、通常攻撃も同じ軌道、着地後の残光の位置固定、再戦で通常・刈り取り両方の破棄。
- 既存確認：停止・倍速・左右反転・中断・死亡・復活・連撃・シーク・再戦。
- GPU録画：518フレーム、通常1打、列の刈り取り1打2体、連撃2打2体、描画エラーなし。

headless検査は成功後の終了時に、前版と同じRID/ObjectDB解放の警告を出す。GPU録画は警告なしで終了した。詳細ログはローカルの `output/hagi-horizontal-sweep/` に保存した。

## 再現

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiPresentationCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 60 --write-movie 'C:/works/VillainProto/output/hagi-movie/raw-horizontal-sweep-v1-final.avi' res://HagiMovieCapture.tscn -- --hagi-acrobatics --hagi-movie-dir=res://../design/art/hagi/horizontal-sweep-v1/video
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/attack-acrobatics-v1/encode_preview.py --raw output/hagi-movie/raw-horizontal-sweep-v1-final.avi --output-dir design/art/hagi/horizontal-sweep-v1/video --name hagi-horizontal-sweep
```
