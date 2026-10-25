# ハギ・踏み込み差分

2026-10-25。「踏込の差分も作ってください」に対応。
採用済みの黒・くすんだ赤・グレーと、一穴の大鎌を維持したまま、重心を下げて前へ滑り込む差分を作成した。

- 原本: `hagi-step-right-v1.png`（1024×1536、RGBA透過）。ImageGenで待機絵と振りかぶり絵を参照して生成。
- 本編: `DemoApp/assets/portraits/battle/hagi_step_idle_right.png`。原本と同一。
- 生成指示: `generation-prompt.txt`。
- 靴底は後足の1433pxを接地へ合わせる。下余白103/1536。しゃがんだ姿勢を引き伸ばさず、既存の頭・体の画素スケールを保つ。
- 両手で柄を握り、前膝を曲げ、髪と灰色・赤の後ろ布を後方へ流す。鎌に血・鐘・追加の穴を足していない。

移動がある攻撃で `踏み込み → 振りかぶり → 振り抜き` の順に表示。
足元が動かない攻撃は振りかぶりから始める。通常斬撃は到着後0.04秒構え、刈り取りの溜め時間は既存と同じ。
銀の刃の輪郭も新しい差分の鎌に合わせ、開戦時に画像を先読みする。
背景・光粒・弧は既存素材と実装を継続使用し、新しいエフェクト素材は追加していない。

## 確認

DemoAppビルド成功。`HagiPresentationCheck`で指定9戦の台本・勝敗・最終HP・攻撃／刈り取り件数の一致を確認。
踏み込み差分の表示・靴底の接地・途中停止・倍速・敵側の反転・中断／再戦・シークも確認した。
Godot終了時の既存RID／ObjectDB解放警告は残る。

`HagiMovieCapture`で実戦の攻撃列を抜き出して録画し、踏み込み・構え・振り抜きを目視確認。
周囲の駒や他の演出は省略した確認用映像。以前の動画は保持し、新版は `video/` に置いた。

- [刈り取り・等速MP4](video/hagi-reap-step-1x.mp4)
- [刈り取り・0.5倍速GIF](video/hagi-reap-step-half-speed.gif)
- [連続攻撃・等速MP4](video/hagi-chain-step-1x.mp4)
- [連続攻撃・0.5倍速GIF](video/hagi-chain-step-half-speed.gif)

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --path DemoApp res://HagiPresentationCheck.tscn
Godot_console.exe --path DemoApp --fixed-fps 60 --write-movie C:/works/VillainProto/output/hagi-movie/raw-step-v1.avi res://HagiMovieCapture.tscn -- --hagi-movie-dir=res://../design/art/hagi/step-v1/video
```
