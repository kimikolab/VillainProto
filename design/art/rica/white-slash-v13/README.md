# 二連撃の青白い発光を統一 v13

2026-10-25。ユーザーの「2撃目の水色より、1撃目の青白く光る剣閃が好み。剣の色にも合う」に対応。

1撃目の `slash_arc.gdshader` と同じ白い芯の色、青紫の縁を求める式を、2撃目の軌跡へ適用する。
固定の水色・緑の強い発光を外し、帯の内側も白い光の余韻へ変えた。
1撃目・剣の発光・他段は変更せず、2撃目の配色をそろえる。
v12の高速の振り下ろし、刃から描く曲線、太さ、二重の残光、端のフェードを維持する。
命中や移動の時刻、全身原画・打数・対象・確定HPも維持する。新規画像生成なし。

購入済みEffectBlocks v4の `assets/impacts/impact_1.tscn` と既存の光粒を再確認。
今回は既存の剣閃の配色の調整なので専用シェーダーを直し、購入の火花を継続する。原本・共有リソースは変更しない。

## 動画

- [水色のv12と、青白く統一したv13の二連撃比較](rica-double-white-slash-before-after-v13.mp4)
- [最新4段階の連続比較](../attack-motion-v1/rica-four-stage-flow-comparison-white-v13.mp4)

左がv12、右がv13。通常速度→1/4速。同倍率・同時刻の実際のFxViewerを切り出す。
本編とFxViewerに共通適用。FxViewerでは「墓守リィカ」→6–8層の「攻撃」で確認できる。

## 確認と再現

DemoAppをビルドし、対象段を30fps・300コマ連続撮影する。
配色以外の動作ファイルは `animation-hashes-before.json` と照合し、変更がないことを確認する。
1撃目の軌跡の処理もv12の保存コードと照合する。
ビルド成功（警告0・エラー0）、FxViewerは300コマの連続再生を完走。
原画・追跡・動作指定19ファイルのハッシュと、1撃目の処理の一致を確認。
300コマの移動・再生コマ・打数・軌跡の表示区間もv12と一致した。
記録は [verification.json](verification.json)、実描画の配色比較は [color-check-135.jpg](color-check-135.jpg)。
ログは `output/rica-motion-integration/v13-*.log`。
終了時の既存RID・Font・CanvasItem・ObjectDB解放警告は、Shader・動作エラーと分けて記録する。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture --fx-rica-flow-stage=2
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix white-v13 --shovel-audio --comparison-only
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/white-slash-v13/encode_comparison.py
```

4段階比較のほかの3段は、変更のないv12の実描画を継続使用する。
`--comparison-only` は追従4分割だけを出力する。省略時は従来どおり盤面全体も作る。
