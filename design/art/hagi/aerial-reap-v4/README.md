# ハギ：初撃・フィニッシュの空中刈り下ろし

2026-10-26。ユーザーが追記した演出メモ§3の2場面に、新しい全身原画の跳躍モーションをゲームへ接続。

## 動きと適用条件

しゃがんで踏み切る → 膝を引き上げて空中で体をひねる → 大鎌を背中へ引き絞る → 頭上へ振りかぶって一瞬ためる → 落下しながら斜めに刈り下ろす → 低く着地して振り抜く。

- ハギ自身の手番の初撃が刈り取りで終わるとき。通常攻撃の直後に「刈る」がある場合も、親の攻撃から先読みする。
- その刈り取りで最後の敵が倒れて戦闘が終わるとき。追撃・再行動・二撃目でも使う。頂点の溜めと着地の余韻を少し長くする。

通常命中→刈り取りが直結する場合は、接触姿勢を共有する短い続きのクリップへ渡し、跳躍を二重に行わない。両方の台本の命中は各1回通知する。他の返し刃と再行動は採用済みv3の速い回転斬りを使う。

演出の選択はHagiPresentation.Aerial.csで確定済みReap/Damage/HPの列だけを読む。刈れる線・威力・対象・勝敗・回数の再判定はない。後で敵が蘇生・召喚される場面をフィニッシュにしない。BattleCore・BattleSim・生成docsの変更なし。

## プレビュー

- `video/hagi-aerial-reap-v4.mp4`：初撃／ボスへの止め／連続狩りからの止め。すべて通常速度。
- `video/air-*-1x.mp4`：各場面のゲーム内SE付き。
- `video/air-*-motion-1x.gif`：斬撃と周囲の駒を隠した動作確認。
- `video/air-*-motion-half.gif`：同じ動作の半速。

実際のMain.EnterViewerLive経由でGPU収録した映像。撮影側でHP・攻撃・動きを作らない。切り出し・見出し・表示速度の変更だけで、動作補間や後付けの斬撃はない。撮影範囲・実命中・HPはcapture-segments.jsonとstudy/capture-segments.jsonに残す。

## 原画・演出

組み込みimage_genで新規全身7姿勢を制作し、v3の構え1姿勢を共有。衣装・黒いボブ・片脚タイツ・銀と金の大鎌はハギ自身のもの。添付画像は空中のひねりと振りかぶりのポーズだけを参考にした。安全なスカート丈を保ち、頭の実寸124pxで全身を等比配置する。1920×1536、接地(880,1400)、表示倍率1.6。原画の採否と出典はgeneration-record.json、プロンプトはprompt-*.txt。

落下斬りは既存の赤い三日月を45度傾け、刃の接触セルの位置と高さで発光する。広い列を刈る場合も弧が地面に埋まらない高さに置く。二撃目に使う場合も空中原画の振り下ろす向きへ揃える。着地音は描画セルの着地コマを通過した時に一度だけ鳴らし、中断・死亡・再戦で予約を捨てる。

購入済み候補はGodotApp/ASSET_RESEARCH_AND_PLAN.mdと実素材EffectBlocksのsparkle.pngを確認。既存の購入光粒と赤い三日月を再利用し、購入原本は変更・追加していない。

## 検証・再現

ビルド警告0・エラー0。実台本15本の出来事・HP・打数・刈り取り数・再生・シーク・再戦が一致。専用の検査で初撃の先読み、追撃と反撃、再行動、最後の敵、蘇生と召喚、空中停止、2倍速、接触セルの変換、一度の跳躍・着地、反転、死亡・中断を確認。全身の切れ、接続セルのSHA、全映像のデコード結果はverification.json。Godot終了時の既存Font/CanvasItem/ObjectDB解放警告は残る。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/aerial-reap-v4/prepare_spec.py
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiCelExport.tscn -- --hagi-art=aerial-reap-v4 --hagi-reexport
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiPresentationCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie 'C:/works/VillainProto/output/hagi-air-v4/raw-study.avi' --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn -- --hagi-preview-dir=res://../design/art/hagi/aerial-reap-v4/study --hagi-motion-study --hagi-air-preview
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie 'C:/works/VillainProto/output/hagi-air-v4/raw-battle.avi' --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn -- --hagi-preview-dir=res://../design/art/hagi/aerial-reap-v4 --hagi-air-preview
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/aerial-reap-v4/encode_preview.py --raw output/hagi-air-v4/raw-battle.avi --study-raw output/hagi-air-v4/raw-study.avi
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/aerial-reap-v4/verify_assets.py
```
