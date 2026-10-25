# 武器を構えて敵前へ移動する差分 v8

2026-10-24。ユーザーの「待機立ち絵のまま敵前へ行くより、武器を構えて移動する差分があると良い」を反映。

## 原画と生成記録

[4段階の移動構え](approach-sheet.png)を**内蔵image_gen**で制作。
入力参照・保存元・全プロンプトは [generation-record.json](generation-record.json)。

| 層 | 移動時の構え |
|---|---|
| 0–2 | 両手でシャベルを低く構え、前へ踏み出す |
| 3–5 | 腰を低くし、横回転へつなぐ水平の構え |
| 6–8 | 大剣の芯となるシャベルを肩の高さで保持して前進 |
| 9以上 | 低い突進姿勢で武器を後ろへ引き、到着後の突きへつなぐ |

原画は全身・物理シャベルだけを描き、身体のオーラとエネルギーの武器はゲームで重ねる。
単一の移動用差分を既存の移動軌道と組み合わせる。足踏みのループアニメーションは追加しない。
`cels/pose00.png`〜`pose03.png` を共通1152×896へ配置した。
購入済みEffectBlocks v4の `impacts/impact_1.tscn` と光粒を確認したが、全身の構えは含まないため原画を追加。
購入素材の原本・共有リソースは変更しない。

## ゲームとの接続

- 本編とFxViewerの共通の攻撃入口で、初回の踏み込みから段別の移動構えへ切り替える。
- 移動中は待機ループを止め、武器・目・身体のオーラをこの差分の追跡点へ合わせる。
- 敵前では待機絵へ戻さず、既存の攻撃クリップへ直接引き継ぐ。
- 連撃の途中は直前の振り抜きや空中姿勢を維持し、地上の移動構えへ戻さない。
- 停止・倍速・左右反転に対応。移動中の死亡・蘇生・非表示で構えを解除する。
- 移動距離・移動時間・攻撃の接触コマ・件数・対象・HPは既存の再生経路を使う。

仕様の正は [cel-animation.json](../attack-motion-v1/cel-animation.json) の `approaches`。
本番は `DemoApp/assets/portraits/battle/approach/rica_approach_stage0/`〜`stage3/`。
各フォルダーの `portrait.png`、足元基準の `layout.json`、武器と目の `motion.json` を使う。
攻撃本体は従来どおり7打・164コマ。全体の採用原画は攻撃35姿勢＋移動4姿勢。

## 確認動画とFxViewer

- [待機→武器を構えて移動→攻撃→待機・4段階比較](../attack-motion-v1/rica-four-stage-flow-comparison-approach-v8.mp4)
- [踏み込みから攻撃開始・4段階を半速で確認](rica-four-stage-approach-slow-v8.mp4)
- [盤面全体版](../attack-motion-v1/rica-four-stage-flow-full-board-approach-v8.mp4)

FxViewerで「墓守リィカ」→各段の「攻撃」を選ぶ。速度0.25倍で踏み込みの差分と到着時の接続を確認できる。
4段階の動画は30fps・各10秒。同じ駒を作り直さず、移動から攻撃を連続撮影する。
音声は左上のシャベルの接触へ同期した金属音だけ。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://RicaMotionCheck.tscn -- --capture-dir=C:/works/VillainProto/output/rica-motion-integration/v8-shots
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix approach-v8 --shovel-audio
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/approach-v8/encode_slow_preview.py
```

RicaMotionCheckは実描画・撮影込み528項目が成功。
移動中の差分・待機停止・左右・停止時計・攻撃への引継ぎ・連撃の保持・移動中の死亡／蘇生／非表示も検査。
FxViewer全8Cue・左右反転、DeathAxisCheckの実戦15台本も成功。
連続撮影では全段で10コマの移動構えを記録し、攻撃開始時に解除して元の待機へ帰還した。
全4枚の寸法・追跡点・武器形状と透明余白（最小124px）も検査。
ログは `output/rica-motion-integration/v8-*.log`。
終了時の既存RID・Font・ObjectDB解放警告は、動作やShaderエラーと分けて記録する。
