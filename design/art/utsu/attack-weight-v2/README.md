# ウツ 双剣の演舞 v2 — 腰・体重移動と体格の修正

現在のゲーム採用は [踏み込み3姿勢を追加したv3](../attack-step-v3/README.md)。v2は斬撃と体格の修正記録。書き出し器・検査シーンの既定対象もv3へ移っている。

2026-10-27。「手だけで斬っているように軽い」「攻撃時に小さくなる」という指摘に対する修正。本編は `utsu_hit*_v2` を採用し、v1の原画・映像・素材は比較用に残した。

## 変更

- 溜めで背面を見せ、足の押し出し→骨盤の回転→胸郭・肩→剣、という順で向きが変わる全身原画へ描き直した。初撃は腕を伸ばす突きから、肘を残して身体の前を通す斬撃へ変更。
- 初撃と斬り返しに腰が先行する中間姿勢 `drive1` / `drive2` を追加。初撃は溜め5コマ、中間2コマ、接触1コマ、振り抜き4コマとし、溜めから加速して切り抜ける。
- 待機絵のまま接近してから斬る手順を外した。踏み込みを攻撃の予備動作と同時に進め、接触前に前足へ体重を運ぶ。停止・倍速・中断は原画と同じ時計に従う。
- 待機の頭頂〜顎を再測定し、頭だけでなく胴・すねも照合。姿勢ごとの外接高さをそろえる拡大は使わず、全身・二本の刃の追跡点を同じ等比変換で配置した。
- 2→3→4→5連撃の共通接頭列は維持。旋回から跳躍、宙返り、体重を前足で受ける降下斬りへ続き、短い連撃は実際の打数で終了して待機Aへ戻る。

## 体格の検品

[旧版／新版を同倍率・同じ足元で並べた画像](scale-before-after.png)。各画像の外接矩形に合わせた拡縮はしていない。上段はv1、下段はv2。左から待機・初撃・構え直し。

頭110pxの共通制作倍率で、追跡した4姿勢の実測は以下。ゲームは全身を一律2倍の表示基準で使用する。

| 姿勢 | 首〜骨盤 | 膝〜足首 |
|---|---:|---:|
| 待機 | 216.4 | 194.6 |
| 溜め | 215.4 | 205.6 |
| 初撃 | 204.9 | 188.6 |
| 構え直し | 230.9 | 183.1 |

最大差は待機に対して約7%。[測定値](body-calibration.json)と基準点は [build-spec.py](build-spec.py) / [cel-animation.json](cel-animation.json) に保存。膝を曲げたり上体を傾けたりした際の頭の高さは変わるが、身体を縮小して低くする方式にはしていない。空中の全身姿勢はゲーム映像とセルで目視確認する。

## 原画と採用

内蔵 **ImageGen** で承認済み右向き双剣立ち絵を参照し、全身セルとして生成。CLI/API生成は使っていない。

| 画像 | 採用セル |
|---|---|
| `source-ready.png` | 承認済み待機絵のコピー |
| `source-ground1.png` | 溜め・初撃 |
| `source-ground2.png` | 振り抜き・斬り返し |
| `source-drive.png` | 初撃と斬り返しの重心移動 |
| `source-air.png` | 背面の旋回・回転接触・踏切・空中接触 |
| `source-finish.png` | 前方の抱え込み・降下斬り（上段2セル） |
| `source-recovery.png` | 着地・構え直し |

計15姿勢、9クリップ、30fpsで保持時間を含む131コマ。本番素材は `DemoApp/assets/portraits/battle/attack_clip/utsu_hit*_v2/`。全身セルの部位貼り合わせ・輪郭ワープ・静止画回転・逆再生・クロスフェードは行っていない。

[初期の生成指示全文](generation-prompts.json)、[追加・修正の生成指示全文](generation-corrections.json)。`source-ground.png` は最初の4セル候補で後続のデザイン参照に使用。地上斬りと復帰は2セルずつの画像に作り直して採用した。`source-air-overlap.png` は別人物の剣同士が接して分離できなかった不採用候補、`source-power-rejected.png` は余白・剣先の不備がある不採用候補。

購入済みNatureBlocks／EffectBlocksの一覧とローカル素材を再確認。背景、刃から作る残光、`HagiLight3D` のEffectBlocks `sparkle.png` は既存の採用を継続。素材原本・共有リソースは変更していない。

## 映像・検証

- [2→3→4→5連撃・等速・実音声](video/utsu-weight-2-to-5-1x.mp4)
- 段階別 `video/dance-N-1x.mp4`、2/3速 `video/dance-N-slow.gif`、コマ一覧 `video/dance-N-sheet.jpg`。
- Godot GPU実描画の待機→踏み込み→全打→帰還後の待機を30fpsで収録。確認用の切り出し・拡縮・打数ラベル以外に、動作補間・剣閃・音の後付けはない。
- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 成功、警告0・エラー0。
- `UTSU_CEL_EXPORT_OK`: 15姿勢・9クリップを書き出し。
- `UTSU_DANCE_CHECK_OK checks=150 movie=False`: 体格の照合、共通接頭セルと跳躍高、一打一命中、対象切替、確定HP、停止、倍速、左右反転、中断、死亡・蘇生、勝利、再戦、実戦台本、シーク、待機A復帰。
- 収録時も `UTSU_DANCE_CHECK_OK checks=98 movie=True`。全段階を実描画のコマ一覧で確認。
- `verify-preview.py` で原画の透過余白、131コマの素材、等速MP4全フレームと低速GIF全フレームを復号確認。結果とハッシュは `verification.json`。

Godot終了時の既存FontAdvanced/CanvasItem/ObjectDB解放警告は残る。検査の成功マーカーと終了コード0を確認した。

## 再現

リポジトリ直下で順番に実行。再書き出しする場合だけ `--utsu-reexport` を付ける（v2専用素材のみ更新）。

```powershell
python design/art/utsu/attack-weight-v2/build-spec.py
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://UtsuCelExport.tscn -- --utsu-reexport
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://UtsuDanceCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 --disable-vsync --write-movie 'C:/works/VillainProto/output/utsu-dance-v1/weight-v2.avi' res://UtsuDanceCheck.tscn -- --movie
& 'D:/comfyui/venv/Scripts/python.exe' design/art/utsu/attack-weight-v2/encode-preview.py --raw C:/works/VillainProto/output/utsu-dance-v1/weight-v2.avi
& 'D:/comfyui/venv/Scripts/python.exe' design/art/utsu/attack-weight-v2/verify-preview.py
```

FxViewerの「ウツ 双剣の演舞・2〜5連撃」「ウツ 双剣の演舞（実戦）」もv2へ切り替わる。BattleCoreの打数・条件・対象・ダメージ計算には変更を加えていない。
