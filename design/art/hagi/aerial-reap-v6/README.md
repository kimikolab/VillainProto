# ハギ：空中で先に斬り、縦の弧を抜いてから着地

2026-10-27。v5の「頭上に鎌を残したまま着地する」「下向きの横薙ぎが振りかぶりと合わない」という確認への修正。DemoAppの空中クリップをv6へ切り替えた。

## 動き

採用済みv5の全身8原画を継続し、新しい絵の生成は行っていない。頂点の反り・ひねりと高さ1.48を保ち、頭上に鎌を運ぶdrop姿勢を3コマから2コマへ短縮。この間の高さは1.32→1.16とし、足を地面へ先に下ろさない。impactの振り切りへ素早く切り替えて高さ0.70で接触し、0.46→0.25へ下降してから着地する。直後に「刈る」が続く場合も、共有するimpact姿勢から0.25→0.12で接触し、その後に一度着地する。

倍速や描画負荷で接触コマを飛び越した場合、ハギは接触セルを一度描いてから経過時間に追いつく。接触時の刃・高さを着地姿勢で通知しない。停止・速度・接触件数を保つ。

## 斬撃

従来の左右軸を45度傾ける方式から、円弧の面を横軸まわりに78度起こす方式へ変更。光の進行は頭上→前方→足元。根元の斬撃・赤い帯・残光・火花の移動に同じ面を使い、キャラクターの左右反転時も上から下へ斬る。直後の刈り取りも同じ面・方向で少し小さく重ね、途中で水平の返し斬りに戻さない。

半径は空中の初撃0.82倍、続く刈る0.72倍。中心は地面からの高さで決め、体が落ちても弧が地面へ沈まないようにした。通常の地上横薙ぎ・回転斬りの軌道と原画は継続。

購入済み一覧と実素材EffectBlocksのsparkle.pngを確認。既存の赤い三日月・光粒を活用し、素材原本は変更していない。戦闘判定・ダメージ・対象・回数・空中動作の適用条件は変更なし。

## プレビュー

- [実戦3場面・通常速度](video/hagi-aerial-reap-v6.mp4)
- [ボスへの止め・斬撃あり半速GIF](video/air-boss-finish-fx-half.gif)
- [ボスへの止め・エフェクトなし通常速度](video/air-boss-finish-motion-1x.mp4)
- [ボスへの止め・エフェクトなし半速GIF](video/air-boss-finish-motion-half.gif)

Main.EnterViewerLiveのGPU実描画。切り出し・見出し・表示速度変更のみ。補間や映像への斬撃の後付けなし。v5の比較元は保存。実際の打数・接触時HP・撮影区間はcapture-segments.json、study/capture-segments.jsonに保存。

## 検証・再現

ビルド成功、今回の増分ビルドは警告0・エラー0。15実台本の出来事整合、空中停止・倍速・着地より先の接触・共有接触セル・一回の着地・反転・中断・死亡を確認。追加の幾何検査で初撃／続く刈る／左右反転の弧が上から下へ抜け、地面に埋まらないことを検査。9クリップ178PNG、6組の接続SHA、全18映像のデコードを確認。Godot終了時の既存Font/CanvasItem/ObjectDB解放警告は残る。BattleCore・BattleSim・生成docsは変更なし。

原画の生成履歴・プロンプトは [v5](../aerial-reap-v5/README.md)。今回の参照元SHAはgeneration-record.json、検証結果はverification.json。仕様はprepare_spec.pyとcel-animation.json。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/aerial-reap-v6/prepare_spec.py
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiCelExport.tscn -- --hagi-art=aerial-reap-v6 --hagi-reexport
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiPresentationCheck.tscn -- --visual-only
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie C:/works/VillainProto/output/hagi-air-v6/raw-battle.avi --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn -- --hagi-preview-dir=res://../design/art/hagi/aerial-reap-v6 --hagi-air-preview
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie C:/works/VillainProto/output/hagi-air-v6/raw-study.avi --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn -- --hagi-preview-dir=res://../design/art/hagi/aerial-reap-v6/study --hagi-motion-study --hagi-air-preview
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/aerial-reap-v6/encode_preview.py --raw output/hagi-air-v6/raw-battle.avi --study-raw output/hagi-air-v6/raw-study.avi
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/aerial-reap-v6/verify_assets.py
```
