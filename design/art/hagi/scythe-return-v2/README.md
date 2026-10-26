# ハギ：八の字の二度斬り・引き刈りの返し刃

ユーザー確認で「鎌を左右へ押し引きしているように見える」と指摘された比較元。[回転斬りv3](../rotational-sweep-v3/README.md)へ接続を変更した。このフォルダは前案の制作記録として残す。

2026-10-26。エフェクトだけが往復し、身体が二度斬ったように見えなかったため、二度斬りと返し刃を専用の全身セルで制作した。ゲームへ接続済み。BattleCore、対象、打数、確定ダメージとHP、待機原画は変更していない。

## 見るもの

- **二度斬り**: `video/isolated-twin-motion-half.gif`。横薙ぎ → 低く回す → 斜めに薙ぎ上げる → 回転して振り抜く。
- **返し刃**: `video/after-hit-return-motion-half.gif`。横薙ぎ → 敵の向こうで刃を返す → 腰を落として引き刈る。
- **実戦比較**: `video/hagi-scythe-motion-v2.mp4`。返し刃・二度斬り・連続狩りを、各場面の通常速度 → 1/4速の順で収録。
- `video/*-1x.mp4` は各場面の通常速度・ゲーム内SE付き。スローと結合比較は無音。

動作確認GIFは本編と同じMain再生・モーションを使い、専用撮影モードで周囲の駒、画面上の斬撃と数字を隠してカメラを寄せた。武器上の強化光は残す。1/2速。実戦比較では対象と斬撃を表示する。動画編集は切り出し、縮小、時間軸の変更、見出し追加だけで、動作補間や剣閃の後付けはない。

## 原画・接続

組み込みimage_genで透過2×2シートを3枚生成し、12姿勢を全身単位で採用。顔、黒いボブと小さな赤い編み込み、黒・くすんだ赤・グレー・金の衣装、根元に穴1つの大鎌を基準にした。動作中の被覆は不透明な黒い安全用ショートパンツで確保。原画と正確なプロンプトは `source-*.png`、`prompt-*.txt`、参照とSHAは `generation-record.json`。

`cel-animation.json` が頭頂〜顎、足元、刃先・根元・輪郭、各姿勢の保持と命中コマを持つ。HagiCelExportで全身を等比拡縮し、1920×960、頭の基準124px、共通接地(880,940)へ配置。腕・手の貼り替え、ワープ、逆再生、クロスフェードは使っていない。

二度斬りの一振り目は低い姿勢まで振り抜き、その姿勢と同じセルから二振り目を始める。返し刃も刃を返した姿勢を共有する。複数の敵へ返しが続くときは、引き切った姿勢から鎌を運び直して次の引き刈りへ進む。二撃目の後に刈る場合にも接続専用クリップを用意した。

以前の「命中2コマ前へ移動して1.5倍速」の処理を外した。連撃は命中後も振り抜きまで再生し、次へ渡す。通常ダメージの数字とHPも接触へ合わせるが、巻き込み途中の返し刃より後のダメージは先取りしない。引き刈りでは半歩後ろへ重心を移し、二度斬りの二撃目は斜めの弧、返し刃は小さな弧を使う。

購入済み素材の一覧と導入済みEffectBlocksのsparkleを確認し、既存の光粒と赤い三日月を継続した。人物固有の全身動作は生成原画で補った。購入素材原本・共有リソースには変更していない。

## 確認

- DemoAppビルド: 警告0・エラー0。
- HagiPresentationCheck: 15実台本で打数、刈り取り、最終HPが一致。停止、倍速、反転、中断、死亡・復活、再戦、シークも成功。
- 専用セルの先頭から再生し、接続部で武器の座標が一致することを追加検査。画像SHAでも5組の接続姿勢が同じ全身セルであることを照合。
- GPUで本編と動作確認用をそれぞれ60fps収録。通常1＋返し1、通常2振り、通常2＋返し3の件数を実台本と照合。
- 全動画をデコードし、全PNGの透過余白・寸法・枚数を検査。詳細は `verification.json`。

Godot終了時の既存Texture/Font/CanvasItem/ObjectDB解放警告は残る。検査と両GPU録画は終了コード0。ログと原録画は `output/hagi-return-preview/`。

## 再現

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://HagiCelExport.tscn -- --hagi-art=scythe-return-v2
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://HagiPresentationCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie 'C:/works/VillainProto/output/hagi-return-preview/raw-motion-v2-final.avi' --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn -- --hagi-preview-dir=res://../design/art/hagi/scythe-return-v2
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie 'C:/works/VillainProto/output/hagi-return-preview/raw-motion-v2-study.avi' --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn -- --hagi-preview-dir=res://../design/art/hagi/scythe-return-v2/study --hagi-motion-study
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/scythe-return-v2/encode_preview.py --raw output/hagi-return-preview/raw-motion-v2-final.avi --study-raw output/hagi-return-preview/raw-motion-v2-study.avi
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/scythe-return-v2/verify_assets.py
```

書き出しは既存連番を保護する。今回のv2を同じ生成元から再書き出しする場合だけ `--hagi-reexport` を付ける。旧v1は書き換えない。
