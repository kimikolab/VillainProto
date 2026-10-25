# 命中後も敵の向こうへ振り抜く回転薙ぎ v7

2026-10-24。ユーザーの「当たった瞬間に切っ先が止まり、弾かれているように見える」を反映。

## 原画・生成記録

[振り抜きの全身原画3枚](follow-through-sheet.png)を**内蔵image_gen**で制作。
生成元・入力参照・全プロンプトは [generation-record.json](generation-record.json)。

1. 命中後も両腕を右へ伸ばし、肩と腰がさらに回る斜め後ろ。
2. 柄と鋼部を奥へ送り、背中を見せて回り続ける途中。
3. 刃を左側まで運び終える、反対側の振り切り。

3枚とも全身セルとして採用し、顔・腕の貼り替えや輪郭ワープを使わない。
`cels/pose00.png`〜`pose02.png` が共通1152×896へ位置合わせした素材。
制作用のポーズ番号12〜14に対応し、原画の体格は既存の回転と合わせる。
購入済みEffectBlocks v4の `impacts/impact_1.tscn` を再確認。
全身の振り抜きは含まないため原画を追加し、命中の火花・既存の音は継続使用する。

## 変更内容

従来は命中姿勢を3コマ保持し、直後の角速度も25度/コマから約8.3度/コマへ低下していた。
命中後に回る角度も36度に留まり、接触点で刃を押し戻されたように見えていた。
以前の配分は [previous-motion-v6.json](previous-motion-v6.json) に保持する。

- 命中姿勢は**1コマ**。実接触は従来と同じ27コマ目。
- 命中直後も**24度/コマ**で進み、敵を通り過ぎてから徐々に減速する。
- 接触後にさらに**約155度**回り、右→奥→左へ刃を通す新しい全身ポーズへつなぐ。
- 身体の前進も接触後に続け、振り抜いてから構えへ戻す。
- 終盤の残光と先端の光点は `trailAlpha` で消し、回復姿勢の位置で止まった光点を残さない。
- 回転薙ぎは44コマ/30fps、使う全身姿勢は14枚。全7打の本番は164コマ。

仕様の正は [cel-animation.json](../attack-motion-v1/cel-animation.json)。
回転角と残光濃度は本番クリップの `motion.json` に書き出し、同じ戦闘の時計で補間する。
ダメージ・対象・攻撃回数・シャベルの命中音は既存のものを使う。

## 比較動画

- [v6→v7：通常速度と半速の比較](rica-spin-before-after-v7.mp4)
- [4段階の待機→攻撃→待機・30fps](../attack-motion-v1/rica-four-stage-flow-comparison-follow-through-v7.mp4)
- [盤面全体版](../attack-motion-v1/rica-four-stage-flow-full-board-follow-through-v7.mp4)

同倍率・同時刻の3–5層を比較し、接触後までの動きを確認する。
半速は各フレームを2回表示する。画像の補間や攻撃の逆再生は行わない。
4段階版の音声は左上のシャベルの実接触へ同期した金属音だけ。

## 再生成

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://RicaMotionCheck.tscn -- --capture-dir=C:/works/VillainProto/output/rica-motion-integration/v7-shots
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix follow-through-v7 --shovel-audio
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/follow-through-v7/encode_comparison.py
```

RicaMotionCheckは実描画・撮影込み459項目が成功。
命中直後に急停止しないこと、100度以上の振り抜き、振り終わりの光点の消失も検査する。
FxViewerの全8Cue・左右反転、DeathAxisCheckの実戦15台本も成功。
本番164コマの寸法・追跡数・透明余白（最小42px）を確認した。
ログは `output/rica-motion-integration/v7-*.log`。
終了時の既存RID・Font・ObjectDB解放警告は、動作やShaderエラーと分けて記録する。
