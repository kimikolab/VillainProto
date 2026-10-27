# ウツ 踏み込み差分 v3

現在の採用版は [双剣連撃 v4](../attack-flow-v4/README.md)。v3は比較用に保存している。

2026-10-27。「踏み込み時の差分も作ってみて」への追加。全身の荷重・蹴り出し・踏着を作り、本編とFxViewerに接続した。

## 新しい3姿勢

1. `step_load` — 待機から後ろ足へ重心を預け、前足の踵を軽くする。
2. `step_push` — 後ろ足で地面を押し、腰を前へ運んで前足を振り出す。
3. `step_plant` — 前足を着き、膝と腰で体重を受けて斬り出しの構えへ入る。

[3姿勢の一覧](step-poses.png)。同じ1280×960のキャンバスを同倍率で並べた。元画像の外接矩形ごとの拡縮はしていない。

初撃は `待機2 → 荷重3 → 蹴り出し4 → 踏着3 → 腰の溜め2 → 斬り出し中間2 → 接触1 → 振り抜き` の順。30fpsで、初撃の接触は16コマ目。2〜5連撃の初動で同じ列を使い、既存v2の斬り返し・旋回・跳躍・宙返りへ接続する。

## 足と移動の同期

初撃の `motion.json` に各コマの前進率 `advance` を保存した。0〜4コマ目はその場で荷重、5〜8コマ目の蹴り出しで前進し、9コマ目の踏着で前進率1へ到達する。その後の腰の溜めでは足元を動かさない。

`BattlePawn3D.ProcessUtsuAdvance` が原画と同じ時計で前進率を読み、人物と刃を配置してから接触を通知する。一時停止・倍速も共通の時計。移動完了を別タイマーの推測に任せず、踏着セルで止める。2撃目以降で攻撃対象が替わる場合の距離合わせは継続する。

攻撃判定は増やさず、踏み込み中に剣閃や命中音を鳴らさない。台本の打数・対象・確定HPと待機Aへの復帰を維持する。

## 原画と体格

内蔵 **ImageGen** を使用。承認済みの `DemoApp/assets/portraits/battle/utsu_idle_right.png` を人物・衣装・双剣・体格の基準、v2の地上斬りを接続先として参照した。

- `source-step-load.png`
- `source-step-push.png`
- `source-step-plant.png`
- [生成指示全文と参照画像](generation-prompts.json)

3姿勢を個別の透過全身画像として生成。部分貼り合わせ・輪郭ワープ・静止画の回転・攻撃の逆再生・クロスフェードは使わない。頭110pxの基準で全身と二本の刃の追跡点を同じ等比変換へ載せ、胴とすねの長さも待機と照合した。新しい3姿勢の胴・すねの実測差は待機に対して約9%以内。[基準点](build-spec.py)、[測定値](body-calibration.json)。

v2の15姿勢は元画像を参照して再利用し、計18姿勢・9クリップ・保持時間を含む145コマ。ゲーム素材は `DemoApp/assets/portraits/battle/attack_clip/utsu_hit*_v3/`。旧版は比較用に保存。購入済みEffectBlocksの火花・既存の剣の残光と背景は採用を継続し、原本は変更していない。

## 確認

- [踏み込みから2連撃・等速・実音声](video/dance-2-1x.mp4)
- [同じ動作の2/3速GIF](video/dance-2-slow.gif)
- [2→3→4→5連撃の等速動画](video/utsu-step-2-to-5-1x.mp4)
- 各段階のコマ一覧は `video/dance-N-sheet.jpg`。

映像はGodotのGPU実描画。待機から踏み込み、全打、帰還後の待機までを含み、ゲーム内ズーム1.3・30fpsで実音声とともに収録した。切り出し・拡縮・打数ラベル以外に、動作補間や効果の後付けはしていない。

- ビルド成功、エラー0。作業対象外のBattleCoreにCS0162/CS8602の警告2件。
- `UTSU_CEL_EXPORT_OK`: 全18姿勢と9クリップを正常に書き出し。
- `UTSU_DANCE_CHECK_OK checks=173 movie=False`: 荷重中はその場、蹴り出し中は前進、踏着後は足元を維持、蹴り出し中の停止と再開、体格、全段階の接続、命中回数、対象変更、確定HP、倍速、左右反転、中断、死亡・蘇生、勝利、実戦台本、再戦・シーク、待機A復帰。
- 収録も `UTSU_DANCE_CHECK_OK checks=111 movie=True`。
- `verify-preview.py` で全18姿勢の透過余白、145コマ、MP4とGIFの全フレームを検査。結果・ハッシュは `verification.json`。

Godot終了時のFontAdvanced/CanvasItem/ObjectDB解放警告は残る。検査の成功マーカーと終了コード0で合否を確認した。

## 再現

リポジトリ直下から順番に実行。再書き出し時だけ `--utsu-reexport` を付ける。

```powershell
python design/art/utsu/attack-step-v3/build-spec.py
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://UtsuCelExport.tscn -- --utsu-reexport
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://UtsuDanceCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 --disable-vsync --write-movie 'C:/works/VillainProto/output/utsu-dance-v1/step-v3.avi' res://UtsuDanceCheck.tscn -- --movie
& 'D:/comfyui/venv/Scripts/python.exe' design/art/utsu/attack-step-v3/encode-preview.py --raw C:/works/VillainProto/output/utsu-dance-v1/step-v3.avi
& 'D:/comfyui/venv/Scripts/python.exe' design/art/utsu/attack-step-v3/verify-preview.py
```
