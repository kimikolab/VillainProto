# ハギ：空中斬りを一本の太い三日月へ

2026-10-27。v6の最後が大小の二重丸に見えるという確認への修正。空中モーションの原画とタイミングはv6を継続し、赤い斬撃の形と直後の刈り取りへの接続を変更した。

原因は通常命中で半径0.82倍の弧、直後の刈るで半径0.72倍の別の弧を発生させていたこと。さらに元の弧が約338度のほぼ一周だったため、縦の面に起こすと円に見えていた。

空中斬りだけを216度の開いた弧と専用シェーダーに切り替えた。中央は半径の約56%の厚みを持ち、両端へ尖る三日月。白く強い芯と赤い刃・外光を重ね、背面をつなぐ円周と小さな残響の弧は描かない。頭上から前方・足元へ抜ける78度の面は継続。

通常命中と直後の刈り取りは同じエフェクトを共有する。刈り取り時はその三日月と命中光を強くするだけで、二本目の弧を追加しない。円弧の描き始めや寿命時計を戻さない。再戦で共有先を破棄し、中断したエフェクトを再利用しない。シーク等で共有元が無ければ一本だけ生成する。通常の地上横薙ぎ・二度斬り・返しの形は継続。台本の命中件数・HP・音は維持する。

購入済みEffectBlocksの採用一覧と実sparkle.pngを確認し、既存光粒を継続。新しい画像生成は行わず、ゲーム内メッシュとシェーダーで形を変更した。素材原本・BattleCore・BattleSim・生成docsは変更なし。

## プレビュー

- [実戦3場面・通常速度](video/hagi-aerial-reap-v7.mp4)
- [ボスへの止め・半速](video/air-boss-finish-fx-half.mp4)
- [半速GIF](video/air-boss-finish-fx-half.gif)

Main.EnterViewerLiveのGPU実描画。編集は切り出し・見出し・半速化のみ。命中観測と撮影区間はcapture-segments.json。v6の映像は比較元として保存。

## 検証

ビルド警告0・エラー0。15実台本の整合、停止・倍速・反転・死亡・中断・一回の着地に加え、続く刈るで同じエフェクトが一本のまま増光すること、時計を巻き戻さないこと、三日月の両端が離れていること、縦方向と地面の余白を確認。3場面のGPU収録と全6映像のデコードを検査。Godot終了時の既存Font/CanvasItem/ObjectDB解放警告は残る。詳細はverification.json。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiPresentationCheck.tscn -- --visual-only
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie C:/works/VillainProto/output/hagi-air-v7/raw-battle.avi --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn -- --hagi-preview-dir=res://../design/art/hagi/aerial-reap-v7 --hagi-air-preview
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/aerial-reap-v7/encode_preview.py --raw output/hagi-air-v7/raw-battle.avi
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/aerial-reap-v7/verify_preview.py
```
