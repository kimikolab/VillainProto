# リィカ・二連撃と三連撃のつなぎ v9

2026-10-25。ユーザーの「回転斬りのような流れるモーションを二連撃・三連撃にも」という要望に対応。

## 動きと追加原画

**内蔵image_gen**で二連撃8姿勢・三連撃12姿勢を制作し、本編とFxViewerへ適用した。
シャベルだけを全身原画へ描き、エネルギーの大剣・大槍、身体のオーラ、剣閃は既存の別レイヤーを重ねる。
武器の鋼部の欠け、セル同士の重なり、三つ編みの重複を生成編集で修正し、全身セル単位で選んだ。
顔・手足の部位合成、人物のワープ、クロスフェード、斬撃の逆再生は使わない。

| 層 | 追加した流れ |
|---|---|
| 6–8・二連撃 | 腰を落とす→切り上げ→高く振り抜く→肩越しに武器を返す→斬り返す→低く振り抜く |
| 9以上・三連撃 | 突きの中割り→伸び切る→背面へ踏み替える→回転と上昇→空中で武器を頭上へ運ぶ→落下→着地して振り抜く |

二連撃は `double-b.png` を基準に、`double-c.png` の全身セル0・6・7を採用。
採用結果は [double-selected.png](double-selected.png)。三連撃は `triple-b.png` の全12枚。
`double-cels/`・`triple-cels/` は、原画を1152×896へ足元基準で配置した本番用セル。
生成5回の入力意図・全プロンプト・保存元は [generation-record.json](generation-record.json) と [final-selection.json](final-selection.json)。
`previous-spec-v8.json` は変更前のセル指定を保存した比較元。

購入済みEffectBlocks v4の `impacts/impact_1.tscn` と既存の剣閃・光粒の採用を確認済み。
全身ポーズは含まれないため専用原画を追加し、剣閃・身体オーラ・打撃音は既存の採用素材を継続する。
購入素材の原本・共有リソースは変更しない。

## 再生への接続

従来は、各打の間の0.3秒の移動で直前の振り抜き姿勢を止めていた。
`PounceRicaTo` で同じ移動中につなぎのセルを進め、最終セルと次の攻撃の最初のセルを一致させた。

| つなぎ | コマ数・30fps | 内容 |
|---|---:|---|
| `rica_link_rise_return` | 9 | 切り上げの勢いを肩越しの斬り返しへ運ぶ |
| `rica_link_thrust_sweep` | 9 | 突きの伸びから背面へ踏み替え、大槍を大剣へ戻す |
| `rica_link_sweep_descend` | 9 | 高さ2.8を保ったまま、次の落下斬りへ武器を構える |

実攻撃は7本・157コマ、つなぎは3本・27コマ。全身の採用数は攻撃・つなぎ54姿勢＋移動4姿勢。
実攻撃の接触コマは9/8/6/4/16（切り上げ／斬り返し／突き／回転／落下）を維持する。
つなぎの再生へ接触コールバックを渡さず、打数・攻撃対象・確定HPへ影響させない。
初回の踏み込みは承認済みv8の構えを使う。0–2層の打撃と3–5層の回転薙ぎも維持する。
回転斬りの軌跡は初期角を基準に全周を描き、空中のつなぎと落下斬りへ高さを引き継ぐ。
停止・倍速・左右反転・死亡・非表示・再戦は共通の再生時計と後片付けへ接続する。

仕様の正は [cel-animation.json](../attack-motion-v1/cel-animation.json)。
本番の全身セル・足元・武器・目の追跡データは `RicaCelExport.tscn` で再生成する。

## 動画と確認

- [二連撃・三連撃の追加前後（通常→半速、14秒）](rica-double-triple-before-after-v9.mp4)
- [待機→踏み込み→連撃→待機・4段階比較](../attack-motion-v1/rica-four-stage-flow-comparison-combo-flow-v9.mp4)
- [盤面全体・4段階比較](../attack-motion-v1/rica-four-stage-flow-full-board-combo-flow-v9.mp4)

FxViewerで「墓守リィカ」→6–8層または9層以上の「攻撃」を選ぶ。
比較動画は同じ実描画を同倍率で切り出し、左がv8・右がv9、上が二連撃・下が三連撃。
動画側での動きの補間はしない。4段階版の音声は左上のシャベルの金属打撃音だけ。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://RicaMotionCheck.tscn -- --capture-dir=C:/works/VillainProto/output/rica-motion-integration/v9-shots
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://DeathAxisCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix combo-flow-v9 --shovel-audio
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/combo-flow-v9/encode_comparison.py
```

ビルド成功（警告0・エラー0）。RicaMotionCheckは実描画・撮影込み548項目が成功。
つなぎの停止・死亡・非表示による中断、7実攻撃の接触と打数、左右反転、シーク後の対象とHPも確認。
FxViewerは8 Cue・左右反転、DeathAxisCheckは実戦15台本が成功。
4段階を各300コマ連続撮影し、つなぎ3本と帰還後の待機を確認。攻撃開始は全段95コマ目、帰還は136/149/157/193コマ目。
原画184コマの寸法・透過余白（最小41px）・追跡点と、3つの接続の前後で全身セルが一致することも確認。
MP4は3本とも全コマを再デコードし、フレーム数・30fps・寸法を検査。4段階版の音声ピークは0.411でクリップなし。
詳細は [asset-check.json](asset-check.json)、[flow-check.json](flow-check.json)、[video-check.json](video-check.json)。
ログは `output/rica-motion-integration/v9-*.log`。
終了時に出る既存のRID・Font・CanvasItem・ObjectDB解放警告は、動作・描画中のエラーと分けて記録する。
