# 二連撃の切り下ろし速度・青白い剣閃 v12

2026-10-25。ユーザーの「一貫性は出たが、2撃目がもたつく」「青白い弧を派手で美しく」という要望に対応。

## モーションの配分

承認されたv11の全身原画を再利用し、構えと刃が通る向きを保ったまま保持時間を変える。
正面を通る中間姿勢の連続保持を短くし、命中後の中間1姿勢を省く。空いた時間を頭上の溜めと振り切った余韻へ移す。

| 時点（30fps・0始まり） | v11 | v12 |
|---|---:|---:|
| 頭上の構え | 0〜1 | 0〜3 |
| 振り出し | 2 | 4 |
| 命中 | 8 | 8 |
| 低い振り抜き姿勢へ到達 | 12 | 10 |
| クリップ全体 | 16コマ | 16コマ |

振り出し→命中は0.20秒から0.133秒（約1.5倍速）、振り出し→低い振り抜きは0.333秒から0.20秒（約1.67倍速）。
再生全体の一律倍速は使わず、溜めから加速して振り切る。
命中コマ・クリップ全体の長さ・移動・台本の打数・対象・確定HPは維持する。
仕様の正は [cel-animation.json](../attack-motion-v1/cel-animation.json)。変更前は `previous-spec-v11.json`。
原画・元の生成プロンプトは [v11の制作記録](../return-cut-v11/README.md)。今回は新規画像生成なし。

## 剣閃

実際の刃を追う `RicaWeaponTrail3D` の2撃目専用の描画を強化。
白熱する太い芯、水色の光、深い青の外光、細い2本の残光を一つの軌道へ重ねる。
古い部分は細く薄く、新しい部分は明るく太くし、速い振りを光の弧で読めるようにする。
曲線の分割を約3倍に細かくし、細線が角ばって見えるのを抑える。
点間距離を使う滑らかな補間で測った剣先の点を結び、継ぎ目の角を丸める。
軌跡に沿った距離から古い端の幅と透明度を絞り、帯が四角く途切れないようにする。

余韻は0.26秒から0.32秒。振り終わってクリップ名が外れても、同じ描画を使って自然に消す。
2撃目のエネルギー刃と残光だけは深度検査を外し、地面との交差で水平に切れるのを防ぐ。
準備動作には軌跡を出さず、命中時の閃光もv11と同じ刃上へ置く。独立した円弧は追加しない。
1撃目や他段の剣閃・武器は従来の描画を使用する。

購入済みEffectBlocks v4の `assets/impacts/impact_1.tscn`・`assets/other/shockwave.tscn` と光粒を再確認。
今回の弧は剣先に追従する必要があるため専用のメッシュ・シェーダーを改修し、既存の購入火花を継続する。
購入素材の原本・共有リソースは変更しない。原画のワープ・クロスフェード・逆再生は使わない。

## 動画

- [二連撃のv11とv12の比較・通常→1/4速](rica-double-fast-slash-before-after-v12.mp4)
- [4段階の待機→踏み込み→攻撃→待機](../attack-motion-v1/rica-four-stage-flow-comparison-fast-slash-v12.mp4)
- [盤面全体](../attack-motion-v1/rica-four-stage-flow-full-board-fast-slash-v12.mp4)

左が前回v11、右が今回v12。通常速度の後に1/4速を再生する。
本編とFxViewerは同じ入口へ適用済み。FxViewerで「墓守リィカ」→6–8層の「攻撃」を選ぶ。
実描画を同倍率で切り出し、動画側での動き補間・剣閃の描き足しはしない。

## 検証と再生成

ビルド成功（エラー0）。RicaMotionCheckは実描画・撮影込み556項目、FxViewerは8 Cue・左右反転で成功。
溜めから4コマ以内に命中すること、刃の方向・命中点、停止・倍速・死亡・非表示・再戦・シーク後のHPも検査する。
描画の変更はGPUで実写し、地面での切れ・残光の曲線・人物とHPの読みやすさを確認する。
Godot終了時の既存RID・Font・CanvasItem・ObjectDB解放警告は、描画中のShaderエラーと分けて記録する。
ログは `output/rica-motion-integration/v12-*.log`。
4段階を各300コマ連続撮影し、最終の2撃目は段別撮影でも再確認した。
命中は135コマ目のまま、振り出し131→命中135→低い振り抜き137と記録された。全段が元の席の待機へ戻る。
記録は [asset-check.json](asset-check.json) と [flow-check.json](flow-check.json)。
MP4を3本とも全コマ再デコードし、コマ数・30fps・寸法を確認。4段階版の打撃音はピーク0.411でクリップなし。
記録は [video-check.json](video-check.json)、命中時の実描画は [剣閃の比較](arc-check-135.jpg)。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://RicaMotionCheck.tscn -- --capture-dir=C:/works/VillainProto/output/rica-motion-integration/v12-shots
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix fast-slash-v12 --shovel-audio
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/fast-slash-v12/encode_comparison.py
```

段別の撮り直しには `--fx-rica-flow-stage=2` を併記する。ほかの段の撮影データは残る。
