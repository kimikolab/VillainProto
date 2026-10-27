# ウツ 双剣の演舞 v1

現在のゲーム採用は [腰・体重移動と体格を修正したv2](../attack-weight-v2/README.md)。このフォルダは旧版の比較記録。

2→3→4→5連撃で共通の振り付けが伸びる攻撃差分と、本編へ接続したモーション。2026-10-27制作。

## 採用した動作

| 打数 | 追加される動作 |
|---|---|
| 2 | 身を巻く構え→踏み込みの横払い→逆手側の剣による上昇斬り返し |
| 3 | 斬り返しの勢いから背面を通る旋回→回転斬り |
| 4 | 踏み替えから跳躍→空中で身体を開く交差斬り |
| 5 | 空中で膝を畳む前方宙返り→降下斬り→膝で衝撃を吸収して着地 |

続く打では前クリップの最終全身セルを次の最初のセルとして使い、跳躍高も一致させる。短い段階は実際の最終打から着地・構え直しへ分岐する。間に待機絵を挟まない。全打終了後はユーザーが選んだ待機Aへ復帰する。

原画12姿勢と承認済み待機の開始セルを合わせた13全身セル。30fps、9クリップ、保持時間を含む計113コマ。部分貼り合わせ、身体のワープ、静止画の回転、逆再生、攻撃中のクロスフェードは使わない。通常の1撃も専用の終了分岐で再生する。

## 原画・生成指示

内蔵 **ImageGen** で、承認済みの右向き双剣立ち絵を参照して制作。CLI/APIによる代替生成は行っていない。

- `source-ground.png`: 溜め、初撃、振り抜き、上昇斬り返し。
- `source-air.png`: 背面の旋回、回転接触、踏切、空中接触。
- `source-finish.png`: 前方の抱え込み、降下接触、着地、構え直し。
- `source-ready.png`: 採用済み戦闘立ち絵のコピー。
- [生成指示全文](generation-prompts.json)、[終盤シートの修正指示全文](generation-finish-correction.json)。
- `source-finish-v1.png` は剣先の余白が足りなかった不採用原本。余白を確保した `source-finish.png` に全身単位で置き換えた。

[cel-animation.json](cel-animation.json) が姿勢・頭の基準点・接地・二本の刃の根元と先端・保持時間・接触コマ・跳躍高の正。頭頂〜顎を100pxに校正し、全身と追跡点を同じ等比変換で1280×960へ置く。しゃがみ姿勢を全身外接高さで引き伸ばさない。透明部のRGBが輪郭へ混ざらないよう乗算済みアルファで縮小する。

採用セルは [poses/](poses/)、本番用連番と `clip.json` / `motion.json` は `DemoApp/assets/portraits/battle/attack_clip/utsu_hit*_v1/`。

## 本編への接続

`UtsuPresentation` は確定済みの `Attack` 列から実際の連撃順と終端を読み、各打の接続／終了クリップを選ぶ。呪いの量から打数を計算し直さない。Mainは接触セルまで待って従来のDamageを再生し、台本の対象と確定HPを維持する。一打につき命中通知と攻撃音は一度だけ。

二本の実際の刃を追跡し、振っている側だけに短い銀青色の残光を出す。構え直しや別の打に移る際は残光履歴を切る。購入済みNatureBlocks／EffectBlocksを先に確認し、既存背景と `HagiLight3D` のEffectBlocks光粒を流用した。素材原本・共有リソースは変更していない。

FxViewerで「ウツ」を検索すると「双剣の演舞・2〜5連撃」と「双剣の演舞（実戦）」を選べる。固定台本と実戦の両方が本編Mainと同じ再生入口を使う。

## 確認映像

- [2→3→4→5連撃・等速・実音声](video/utsu-dance-2-to-5-1x.mp4)
- 各段階: `video/dance-N-1x.mp4`、低速 `video/dance-N-slow.gif`、コマ一覧 `video/dance-N-sheet.jpg`（N=2,3,4,5）。

GodotのGPU実描画と実音声を30fpsで収録。待機→踏み込み→全打→帰還後の待機を含む。確認用に画面を切り出し、拡縮と打数ラベルだけを付けた。動作補間・剣閃・効果音の後付けはしていない。GIFのみ2/3速。ゲーム内ズーム1.3。収録区間は `capture-segments.json`、復号結果とハッシュは `verification.json`。

## 検証

- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 成功、警告0・エラー0。
- `UTSU_DANCE_CHECK_OK checks=135`: 共通接頭セル、接続セルと跳躍高、一打一命中、対象切替、確定HP、左右反転、停止、途中の倍速変更、命中前の中断、死亡・蘇生、空中での勝利、再戦、シーク・再生、実戦台本との一致、待機A復帰。
- 既存検査: `IDLE_LOOP_CHECK_OK`、`PORTRAIT_STATE_CHECK_OK`、`FIRE_CHECK_OK`、`MOVEMENT_PORTRAIT_CHECK_OK`、`RICA_MOTION_CHECK_OK checks=520`、`HAGI_AIR_VISUAL_OK` / `HAGI_VISUAL_CHECK_OK ok=True`。
- 通常起動から逆しま（ネル×ウツ）、第5波、seed7を再生し `DEMO_SMOKE_COMPLETE events=330 turns=5 won=True`。
- 動画収録の2〜5連撃も `UTSU_DANCE_CHECK_OK checks=83 movie=True`。全段階を等速とコマ一覧で目視確認。

Godot終了時に既存のFontAdvanced/CanvasItem/ObjectDBの解放警告が出る。検査の成功マーカーと終了コード0を確認した。

## 再現

リポジトリ直下から実行する。既存素材への再書き出しはこの版のウツ専用クリップだけを更新する。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://UtsuCelExport.tscn -- --utsu-reexport
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://UtsuDanceCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 --disable-vsync --write-movie 'C:/works/VillainProto/output/utsu-dance-v1/raw-dance.avi' res://UtsuDanceCheck.tscn -- --movie
& 'D:/comfyui/venv/Scripts/python.exe' design/art/utsu/attack-dance-v1/encode-preview.py --raw C:/works/VillainProto/output/utsu-dance-v1/raw-dance.avi
& 'D:/comfyui/venv/Scripts/python.exe' design/art/utsu/attack-dance-v1/verify-preview.py
```

映像変換は既存ローカルffmpeg、復号検査は既存のPyAV/Pillowを使用する。`output/utsu-dance-v1/raw-dance.avi` は収録用の中間ファイル。
