# 二連撃の頭上の構え・切り下ろし v11

2026-10-25。ユーザーの5枚の指摘を受け、6–8層の二連撃で1撃目を振り抜いた後の構え・2撃目の全身ポーズ・命中時の剣閃を作り直した。

## 問題と動きの組み直し

v10は準備動作の残光だけを止めていた。横向きに構えた姿勢から低い切り下ろしへ飛ぶ原画の並びが残り、
命中後の剣閃も武器と別の円弧で回っていた。構えが予告する動きと実際の振りが一致していなかった。

今回は**頭上に構え直す→振り出す→正面を通過→斜め前へ振り抜く**という一本の動きに統一する。
1撃目の切り上げと振り抜きは維持する。2撃目の頭上の構えを明確にし、手が下がって刃が正面を通る中間姿勢も追加した。
全身の並びは [原画の8つの主要姿勢](sequence-check.jpg)、実描画は [連続コマの確認](render-sequence-check.jpg)。

## 原画の制作と採用

**内蔵image_gen**で新しい全身10姿勢を制作。入力参照・保存元・全プロンプト4回分は [generation-record.json](generation-record.json)。

- `overhead-return-b.png` の8枚を基準にする。
- そのうち全身セル0・3だけを `overhead-return-c.png` の立てた武器の姿勢へ置き換える。
- `return-mid-cels.png` の2枚で、刃が正面を通る途中の手・胴体・踏み込みを補う。
- 採用8枚のシートは [overhead-return-selected.png](overhead-return-selected.png)。共通キャンバスへ配置した全身セルは `overhead-cels/` と `mid-cels/`。
- `overhead-return-a.png` は隣のセルに触れていたため不採用。修正後は全10枚が独立した全身セルで、同じシャベルと1本の三つ編みを保つ。

人物の部位合成・ワープ・クロスフェード・逆再生を使わず、全身セルを足元に合わせて配置する。
原画には物理シャベルだけを描き、エネルギーの武器・身体のオーラ・剣閃はゲームで重ねる。
購入済みEffectBlocks v4の `assets/impacts/impact_1.tscn` と採用済み `source_files/textures/sparkle.png` を確認。
全身ポーズは含まれないため専用原画を作り、購入の火花を継続する。購入素材の原本・共有リソースは変更しない。

## 剣閃と命中点

2撃目は `RicaWeaponTrail3D` が実際のエネルギー刃を追い、頭上から前方斜め下へ残光を描く。
余韻は0.26秒、明るさは従来の短い残光の1.15倍。準備動作では履歴を切り、残光を出さない。
`rica_return` の命中時には独立した円弧を回さず、短い閃光と火花を出す。
その点は敵の平均位置を現在の刃の線分へ投影して決める。武器とは別方向へ剣閃が飛ぶのを防ぐ。
左右反転でも同じ実座標から求める。

つなぎ9コマ、2撃目16コマ・接触8コマ目を維持する。
他の8クリップの指定、全7実攻撃の接触コマ、打数・対象・確定HPは変更しない。
仕様の正は [cel-animation.json](../attack-motion-v1/cel-animation.json)。変更前の指定は `previous-spec-v10.json`。
全体は実攻撃157コマ＋つなぎ27コマ、採用全身57姿勢＋移動4姿勢。

## 確認動画

- [前回v10と今回v11の二連撃・通常→1/4速](rica-double-overhead-before-after-v11.mp4)
- [4段階の待機→踏み込み→攻撃→待機](../attack-motion-v1/rica-four-stage-flow-comparison-overhead-v11.mp4)
- [盤面全体](../attack-motion-v1/rica-four-stage-flow-full-board-overhead-v11.mp4)

比較動画は左がv10、右がv11。実際のFxViewerを同倍率・同時刻で切り出したもので、動画側の補間や剣閃の描き足しはない。
本編とFxViewerは共通の攻撃入口を使用。FxViewerでは「墓守リィカ」→6–8層の「攻撃」、速度0.5倍で確認できる。

## 検証

ビルド成功（エラー0、BattleCoreの既存警告2）。RicaMotionCheckは実描画・撮影込み555項目、FxViewerは8 Cue・左右反転で成功。
新しく、頭上から斜め下へ剣先が一方向に下がること、原画間の角度が50度未満であること、
命中時に独立した円弧を使わず実際の刃上に閃光を置くことも検査する。
停止・倍速・死亡・非表示・再戦・シーク後の対象と確定HPも既存の検査を通す。

原画と追跡の検査は [asset-check.json](asset-check.json)。つなぎの最終セルと攻撃の先頭セルが同一で、透明余白は最小16px。
4段階を各300コマ連続撮影し、二連撃の姿勢番号・再生コマ・移動・打数の時刻をv10と照合して差分0。
準備動作に残光を出さず、全段が元の席の待機へ戻ることも確認。記録は [flow-check.json](flow-check.json)。
MP4は3本とも全コマ再デコードし、コマ数・30fps・寸法を検査。4段階版の打撃音もピーク0.411でクリップなし。
記録は [video-check.json](video-check.json)。
実描画のログは `output/rica-motion-integration/v11-*.log`。
既存のGodot終了時のRID・Font・CanvasItem・ObjectDB解放警告は、動作・Shaderエラーと分けて記録する。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://RicaMotionCheck.tscn -- --capture-dir=C:/works/VillainProto/output/rica-motion-integration/v11-shots
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix overhead-v11 --shovel-audio
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/return-cut-v11/encode_comparison.py
```
