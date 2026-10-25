# 身体の手前と奥を通る回転の軌跡 v6

2026-10-24。ユーザーの「剣先の軌跡が下半分の弧にしか見えず、回転に見えない」を反映。

## 原因と変更

従来はビルボードの平面上で、コマ間の武器の向きを最短の球面補間で結んでいた。
360度の回転方向や身体の前後を保持していないため、左右が切り替わる部分を同じ半周で通りやすかった。
さらに命中側へ半円状の剣閃を重ね、身体を囲む回転より下側の横振りが強調されていた。

`RicaSpinTrail3D` が腰の周りの立体的な楕円軌道を描く。
3–5層の回転薙ぎと9層以上の空中回転に適用し、その他の攻撃は既存の追跡方式を使う。

- 回転角は `cel-animation.json` の `turn`（度）を正とし、書き出し時に各クリップの `motion.json` へ記録する。
- 角度を360度で巻き戻さず、中間も同じ方向へ連続補間。3–5層は396度、空中回転は405度まで進む。
- 溜めで140度を保持し、背面→左側→正面→右への打ち抜きと角度を合わせる。
- 軌跡の奥側は原画の深度で隠れ、手前側は身体の前を通る。画面の上下だけで円を描かない。
- 先頭の光点と白い芯が進み、古い部分を薄く残す。一周を描いた直後には両側の軌跡が見える。
- 奥行き方向は0.65倍の楕円として見せ、腰の周囲を回る輪郭を読めるようにする。
- 半円状だった命中の剣閃は短い閃光と火花へ変更し、実際の回転軌跡を主役にする。
- 連撃の次の動作で軌跡をリセットし、通常終了では0.22秒で消す。停止・倍速は既存の戦闘再生時計に従う。

購入済みEffectBlocks v4の `energy/electric_waves.tscn` を確認した。
駒のコマ別の回転角・前後の深度・停止に合わせる必要があるため、専用メッシュとShaderで制作する。
命中の火花は既存の購入素材 `sparkle.png` を継続使用し、原本・共有材質は変更しない。
キャラクター原画とシャベルの打撃音はv5を継続使用する。新しい生成画像は使わない。

また、倍速や描画負荷で最終コマを飛び越した際も、最後の絵・武器・高さを適用してから姿勢を保持するよう修正した。
ダメージ・攻撃回数・対象・覚醒条件は変更しない。

## 比較動画

- [修正前後・通常速度と半速](rica-spin-before-after-v6.mp4)
- [4段階の待機→攻撃→待機・30fps](../attack-motion-v1/rica-four-stage-flow-comparison-orbit-v6.mp4)
- [盤面全体版](../attack-motion-v1/rica-four-stage-flow-full-board-orbit-v6.mp4)

修正前後は実描画したv5とv6の同じ3–5層を同倍率・同時刻で比較する。
比較用の半速はフレームを2回ずつ表示し、補間画像を加えない。
4段階版の音声は、左上のシャベルの実接触に同期した金属音だけ。

## 再生成と検証

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://RicaMotionCheck.tscn -- --capture-dir=C:/works/VillainProto/output/rica-motion-integration/v6-shots
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix orbit-v6 --shovel-audio
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/spin-trail-v6/encode_comparison.py
```

RicaMotionCheckは実描画・撮影込み452項目が成功。
回転角が一周を超えて逆行しないこと、手前・奥の両半周が描かれること、回転中の停止も検査する。
FxViewerは全8Cue・左右反転で成功。DeathAxisCheckは実戦15台本で成功。
共通の最終コマ処理を変更したため、ボルグの25コマ攻撃・接触・帰還も再検査して成功。
4段階の連続撮影は各300コマ／30fps、全段が元の席の待機へ復帰した。
ログは `output/rica-motion-integration/v6-*.log`。
終了時の既存RID・Font・ObjectDB解放警告は、動作やShaderエラーと分けて記録する。
