# シャベル打撃と全身の回転薙ぎ v4

2026-10-24。ユーザーの「ガンッという打撃」「バックハンドブローやハンマー投げのような横回転」を反映。

## シャベル打撃（0–2層）

命中位置へ白い角形の衝撃光、琥珀色の短い放射線、7個の金属の火花を出す。
表示の中心はシャベルの実際の鋼部先端と標的の中間に置く。大きい魔法の剣閃は出さない。
衝撃の寿命は0.24秒。命中姿勢を2コマから5コマへ延ばし、0.10秒の短い止めを加えた。
攻撃25コマ/30fps、接触は従来と同じ10コマ目。

接触時の共通の斬撃音を、既存のSpringin素材`kado_hit.mp3`（元名「金属叩き.mp3」）へ差し替えた。
通常SEから+2 dB、既定では-8 dB。攻撃の音程は再生速度で変えず、既存のSEプール・停止処理を使う。
素材の出所は`DemoApp/assets/audio/se/SOURCES.md`。
購入済みEffectBlocksの`impacts/impact_1.tscn`の光・火花も確認し、同じ`sparkle.png`を流用。
自動放出は使わず、本編の停止・倍速の時計で衝撃を進める。

## 回転薙ぎ（3–5層）

新しい全身原画8枚を使用。右の低い構えから、背面側のひねり、背中、反対の斜め後ろ、
左向き、正面からの右への打ち抜き、振り切り、右の構えへつなぐ。
腰・肩・足の向きが一緒に変わり、マントと一本の三つ編みが回転へ追従する。
全33コマ/30fps、接触19コマ目。1回転を1打として再生し、攻撃回数や実際の標的は増やさない。
背中を向ける原画では`eyesVisible=false`を記録し、後頭部へ目の発光を出さない。

全身原画は**内蔵image_gen**で制作。最初に8枚のシートを生成し、左向きの姿勢、一本の三つ編みを順に修正した。
最終採用は2回目の7セルと3回目の左向きセル1枚。Godotの書き出し器が全身セル単位で選ぶ。
手・腕・顔の個別の貼り替え、部位ワープ、逆再生は行わない。

- [採用原画のアトラス](spin-sheet-selected.png)
- [生成時のプロンプト2回分](generation-record.json)
- [最後の修正指示と全身セルの採用記録](final-cell-selection.json)
- `spin-sheet-a.png` / `spin-sheet-b.png` / `spin-sheet-c.png`: 修正前後の出力
- `cels/pose00.png`〜`pose07.png`: 位置合わせした全身セル

元の生成ファイルは`C:/Users/Pon/.codex/generated_images/01a16bc5-e3bf-7170-9b0e-cd60c92ccc3f/`に保持し、
ゲームが参照する素材はすべてこのワークスペースへ保存した。
並び・コマ数・接触・鋼部と目と足元の座標は`../attack-motion-v1/cel-animation.json`が正。
本番は`DemoApp/assets/portraits/battle/attack_clip/rica_shovel/`と`rica_crescent/`。
全段では147コマ、使う全身姿勢は29枚。

## 確認動画

[4段階の連続比較・打撃SE入り](../attack-motion-v1/rica-four-stage-flow-comparison-spin-impact-v4.mp4)と
[盤面全体版](../attack-motion-v1/rica-four-stage-flow-full-board-spin-impact-v4.mp4)。
動画の音は、左上のシャベル命中を確認するためのSEだけを実接触コマへ同期している。
ゲーム内で再生する同じ「金属叩き」を同じ既定音量で使用する。全ゲーム音の録音ではない。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaMotionCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix spin-impact-v4 --shovel-audio
```

RicaMotionCheckは403項目成功。命中回数・背面コマの目の発光停止・打撃衝撃1つ・停止・倍速・死亡・再戦・HPを確認。
実戦のDeathAxisCheckも15台本で成功。ログは`output/rica-motion-integration/spin-v4-*.log`。
Godot終了時の既存RID/Font/ObjectDB解放警告は、機能検査やShaderのエラーと分けて扱う。
