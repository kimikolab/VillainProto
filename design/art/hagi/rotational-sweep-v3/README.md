# ハギ：押し引きから、身体で振り抜く回転斬りへ

2026-10-26。前案v2は「薙ぐより鎌を左右へ押したり引いたりしている」とユーザーが確認したため、原画と接続を作り直した比較用の版。ゲームの接続先をv3へ変更した。v2の原画・動画は比較元として残す。

## プレビュー

- `video/isolated-twin-motion-1x.gif`: 斬撃を隠した二度斬り・通常速度。
- `video/after-hit-return-motion-1x.gif`: 斬撃を隠した返し刃・通常速度。
- `video/*-motion-half.gif`: 同じ動作の半速。
- `video/hagi-orbit-motion-v3.mp4`: 斬撃ありの返し刃、二度斬り、連続狩り。各場面を通常速度 → 1/4速で並べる。
- `video/*-1x.mp4`: 場面ごとの通常速度・ゲーム内SE付き。結合比較と動作確認用は無音。

動作確認用もMainの実台本をそのまま再生し、専用の撮影モードだけで周囲の駒、画面上の斬撃、数字を隠してカメラを寄せる。武器上の強化光は残す。映像は切り出し、縮小、見出し追加、再生時間の変更だけで、動作補間・後付けの斬撃はない。

## 変えたところ

前案では柄の角度がほぼ水平なまま両腕を前へ伸ばし、引き戻す姿勢が続いていた。今回は柄の角度、刃の手前・奥の見え方、身体の正面・背面を描き分けた。

1. 刃を左上に残して構える。
2. 腰と肩が先に回り、刃が左側から手前を通る。
3. 相手の前を通過して右奥へ振り抜き、背面姿勢まで回る。
4. その姿勢から回転を返し、右手前から左側へ斬り抜ける。

二撃目で後退して柄を引く移動は外し、その場で軸を保って回転を返す。連続する刃は、直前の保持姿勢に応じて左右の回転を交互に使う。表示クリップの選択だけで、攻撃回数・対象・HPを求め直していない。

## 原画・接続

組み込みimage_genで全身原画12姿勢を制作。原画、全プロンプト、参照と生成元は `source-*.png`、`prompt-*.txt`、`generation-record.json`。戻り側の最初のシートは下段2姿勢が接触したため不採用とし、同じツールで配置を修正した。`source-back-initial.png` と修正プロンプトも保存。

`cel-animation.json` が採用姿勢、頭の基準124px、足元、刃の根元・先端・輪郭、保持・命中コマの正。全身を等比で配置し、部位貼り替え、ワープ、逆再生、クロスフェードは使わない。立った姿勢と高い大鎌を収めるためキャンバスは1920×1280、接地は(880,1160)。画素の表示倍率は従来と同じで、背の高い絵に合わせて人物全体を縮めない。

本番は `hagi_orbit_out[_hold]_v3` と `hagi_orbit_back[_hold]_v3` の4クリップ。連撃の接続部は同じ全身セルを共有する。従来の事前刈り取りの空中モーションは継続。購入済み素材は前案で確認済みのEffectBlocksの光粒と既存の赤い三日月を使い、素材原本や斬撃の色は変更していない。

### 片脚タイツの修正

ユーザーが動きと二本の斬撃を確認した後、`source-out.png` 右下の背面姿勢だけが両脚タイツになっていると指摘。最初の修正案は左右が逆だったため不採用。最終版はユーザーの訂正に合わせ、**画面右の脚はタイツ、画面左の脚は素脚**にした。組み込みimagegenで `source-out-stocking-fix.png` を作り、その右下1姿勢だけを採用。他の3姿勢は元のシートを使用する。

固定切り出し範囲で再書き出しし、49枚中、対象姿勢を使う4枚だけの画像が変わり、他の45枚と全clip.json・motion.jsonがバイト単位で同一であることを確認した。動きの保持時間、命中コマ、接地、刃の追跡点、斬撃の軌道は維持。修正の全プロンプト・生成元・不採用案・比較結果は `stocking-fix-record.json`。単体確認は `contact-stocking-fixed-preview.png`。

## 確認

ビルド警告0・エラー0。HagiPresentationCheckの実台本15本でHP・打数・刈り取り数が一致。停止・速度・反転・中断・死亡・復活・再戦・シークも成功。接続セルはSHAでも照合。GPUで動作確認用と斬撃ありの両方を60fps収録した。件数・時刻は2つの `capture-segments.json`、画像・映像の検査結果は `verification.json`。

BattleCore、BattleSim、生成済みdocsに変更なし。Godot終了時の既存Texture/Font/CanvasItem/ObjectDB解放警告は残るが、検査と両GPU録画は終了コード0。

## 再現

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://HagiCelExport.tscn -- --hagi-art=rotational-sweep-v3
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://HagiPresentationCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie 'C:/works/VillainProto/output/hagi-return-preview/raw-orbit-v3-stocking-study.avi' --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn -- --hagi-preview-dir=res://../design/art/hagi/rotational-sweep-v3/study --hagi-motion-study
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie 'C:/works/VillainProto/output/hagi-return-preview/raw-orbit-v3-stocking-battle.avi' --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn -- --hagi-preview-dir=res://../design/art/hagi/rotational-sweep-v3
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/rotational-sweep-v3/encode_preview.py --raw output/hagi-return-preview/raw-orbit-v3-stocking-battle.avi --study-raw output/hagi-return-preview/raw-orbit-v3-stocking-study.avi
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/rotational-sweep-v3/verify_assets.py
```

今回の原画を同じ生成元から再書き出しする場合だけ `--hagi-reexport` を付ける。旧版は上書きしない。
