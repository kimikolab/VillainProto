# ハギの動作確認動画

2026-10-25。動きを見やすくするため、ハギと攻撃対象だけを表示した確認用映像。
本編の `BattlefieldView3D.PlayHagiSwing` を等速で録画した。戦闘ロジックや本編の演出は変更していない。

|内容|等速・音あり MP4|0.5倍速・ループ GIF|
|---|---|---|
|刈り取り（一振りで2体）|[hagi-reap-1x.mp4](hagi-reap-1x.mp4)|[hagi-reap-half-speed.gif](hagi-reap-half-speed.gif)|
|連続攻撃（2連続の刈り取り）|[hagi-chain-1x.mp4](hagi-chain-1x.mp4)|[hagi-chain-half-speed.gif](hagi-chain-half-speed.gif)|

「試遊・ハギ 台B」×精鋭・大隊、seed 0（`hagi.live.0.2`）の実戦台本から、
イベント365の一振り、イベント225→241の連続攻撃を使用。
対象・実ダメージ・刈り取り／追撃／連続の分類・魂と印の開始値は、その台本から取得した。
他の駒・標などの演出・攻撃間の付随イベントは省略し、連続攻撃の間には8フレームの余韻を置いた。
戦闘全体をそのまま録画したものとは区別する。

録画用シーンは `DemoApp/HagiMovieCapture.tscn`。Godot Movie Makerで60fpsのAVIを生成し、
FFmpegでMP4へ変換。GIFは範囲を寄せ、960×413・15fps・0.5倍速で作成した。
フレーム範囲と攻撃件数は `capture-segments.json` に記録。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --path DemoApp --resolution 1440x900 --fixed-fps 60 --write-movie output/hagi-movie/raw-clean.avi res://HagiMovieCapture.tscn
```

ビルド成功、`HAGI_MOVIE_CAPTURE_OK`、一振り／二振りの件数を確認した。
MP4全フレームのデコード、GIFの複数フレーム・再生時間・代表フレームの目視も確認済み。
