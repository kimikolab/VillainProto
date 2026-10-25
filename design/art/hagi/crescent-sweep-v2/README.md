# ハギ：幅のある三日月の斬撃

2026-10-26。水平円弧の方向性を維持し、「全体を太く、三日月状に。手前の白い部分も太く」という要望を反映した。

この後、承認済みの形を維持して[赤い炎を強めた版](../flame-emphasis-v3/README.md)へ進めた。ここでは変更前の動画と仕様を残している。

- [通常攻撃→刈り取り→連撃、通常速度・ゲーム実音声](video/hagi-crescent-sweep-1x.mp4)
- [刈り取り、半速GIF](video/reap-half.gif)
- [連撃、半速GIF](video/chain-half.gif)
- [刈り抜きの静止画](peak.png)
- [変更前の水平円弧](../horizontal-sweep-v1/README.md)

白い発光を細線から、外周と内周の間を塗る刃状の面へ変更した。手前の中央を最も広く、両端を鋭く絞った三日月にする。奥の弧も太さと明るさを増した。白い芯と残光のメッシュ幅は0.26から0.60、炎の幅は0.70から0.90へ広げた。赤い炎の根元も、新しい三日月の内縁へ揃えている。

水平の回転面、中心と半径、攻撃のコマ数、命中時刻、停止・倍速の時計は前版を継続する。通常攻撃・刈り取り・連撃に適用した。購入済み素材の採用記録と使用中のEffectBlocks `sparkle.png` を確認し、既存の火花を継続利用した。購入素材の原本や全身原画、BattleCore、生成済みdocsは変更していない。

DemoAppビルドは警告0・エラー0。`HagiPresentationCheck`で9種類の実戦台本、打数・最終HP、水平面・中心・半径、停止・倍速・反転・連撃・中断・死亡・復活・再戦・シークを確認した。GPU録画は60fpsで518フレーム、通常1打、列の刈り取り1打2体、連撃2打2体で正常終了。顔・HP表示と三日月の形を実描画で確認した。

詳細は [verification.json](verification.json) と [capture-segments.json](video/capture-segments.json)。headless検査終了時には前版と同じRID/ObjectDB解放警告がある。検査本体は成功し、GPU録画には警告・描画エラーがない。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiPresentationCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 60 --write-movie 'C:/works/VillainProto/output/hagi-movie/raw-crescent-sweep-v2.avi' res://HagiMovieCapture.tscn -- --hagi-acrobatics --hagi-movie-dir=res://../design/art/hagi/crescent-sweep-v2/video
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/attack-acrobatics-v1/encode_preview.py --raw output/hagi-movie/raw-crescent-sweep-v2.avi --output-dir design/art/hagi/crescent-sweep-v2/video --name hagi-crescent-sweep
```
