# 二連撃の振りかぶり・剣閃の流れ v10

2026-10-25。ユーザーの添付2枚で指摘された、6–8層の1撃目から2撃目へ移るときの不自然な半円の残光を修正。

## 確認結果と修正

v9の実描画をコマ送りで確認。`rica_link_rise_return` の振りかぶり3姿勢にも、実攻撃と同じ剣先の残光が出ていた。
この原画には高い右向き→頭越しの左向き→右向きの構えがあり、2枚の武器方向をSlerpで結ぶ処理が、
描かれていない大きな半円を生成していた。振りかぶっているだけの動きが、もう一度斬ったように見えていた。

`RicaWeaponTrail3D` へ振りかぶりの区分を渡し、その間はメッシュと剣先の履歴を消す。
武器を覆うビームのオーラ・身体のオーラは保ち、次の実攻撃から新しく剣先を追う。
2撃目の構えを追跡の起点にし、振り下ろしで残光が伸びる。
同じ理由で、三連撃の2つのつなぎも準備動作として扱う。

1撃目の切り上げと振り抜き、各攻撃の全身原画、移動、接触コマ、命中時の剣閃、打数・対象・確定HPは変更しない。
新規原画・画像生成は不要。購入済みEffectBlocks v4の `assets/impacts/impact_1.tscn` と `source_files/textures/sparkle.png` を再確認したが、
今回の原因はキャラクターの準備動作と残光の発生区分なので既存の専用軌跡を修正し、購入の火花を継続する。

## 動画

- [二連撃の該当区間・修正前後（通常→1/4速）](rica-double-recovery-before-after-v10.mp4)
- [4段階の連続比較](../attack-motion-v1/rica-four-stage-flow-comparison-recovery-v10.mp4)
- [盤面全体](../attack-motion-v1/rica-four-stage-flow-full-board-recovery-v10.mp4)

左は問題があったv9、右は修正後のv10。同倍率・同時刻の実描画を切り出す。
動画側で斬撃を消したり、動きを補間したりしない。本編とFxViewerに同じ修正を適用済み。
FxViewerで「墓守リィカ」→6–8層の「攻撃」を選び、0.25倍で振りかぶりから斬り返しを見る。
`before-capture-v9.json` は修正前の時刻・姿勢の記録。

## 検証

DemoAppビルド成功（警告0・エラー0）。RicaMotionCheckは549項目、FxViewerは8 Cue・左右反転が成功。
既存の停止・倍速・死亡・中断・再戦・対象とHPの一致に加え、振りかぶり中に斬撃の軌跡が出ないことを検査。
4段階の実描画では `capture.json` に実メッシュの `weaponTrail` を記録して確認する。
二連撃は118〜127コマの振りかぶり全10コマで残光メッシュが非表示、129コマ（2撃目の実コマ2）から再び表示された。
v9と300コマ分の姿勢・再生コマ・移動位置・打数を照合し、差分0。1撃目から帰還までのモーションと接触の時刻を維持する。
記録は [double-check.json](double-check.json)、該当する実描画は [修正前後の123コマ目](recovery-check-123.jpg)。
4段階の全つなぎ30コマで斬撃残光が出ないことと、全段が元の席の待機へ戻ることを確認。
3本のMP4を全コマ再デコードし、コマ数・30fps・寸法と4段階版の音声ピーク0.411（クリップなし）を検査。
記録は [flow-check.json](flow-check.json) と [video-check.json](video-check.json)。
ログは `output/rica-motion-integration/v10-*.log`。
Godot終了時の既存RID・Font・CanvasItem・ObjectDB解放警告は、動作・描画エラーと分けて記録する。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://RicaMotionCheck.tscn -- --capture-dir=C:/works/VillainProto/output/rica-motion-integration/v10-shots
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix recovery-v10 --shovel-audio
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/combo-recovery-v10/encode_comparison.py
```
