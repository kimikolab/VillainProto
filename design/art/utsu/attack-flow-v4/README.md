# ウツ 双剣連撃の組み直し

2026-10-27。短剣を手先で刻む印象を減らすため、刀身の幅を残した双剣と、腰の先行・肩の回転・振り抜きの全身原画へ差し替えた。本編とFxViewerの `utsu.dance2`〜`utsu.dance5`、`utsu.live` は v4 を使う。

## 動き

1. 荷重・蹴り出し・踏着から、右の横薙ぎ。背中が見えるまで肩を回す。
2. 左を低く運んで斬り上げ、胸を開き、軸足の旋回へつなぐ。
3. 旋回から右の横薙ぎ。右腕に肩当てを描いた候補は採用せず、背面の原画を別に作った。
4. 小さく跳躍し、左の斬り下ろしで着地。着地の沈みを第五打の溜めにする。
5. 双剣の交差斬りから、両腕を開いて振り抜く。

各段階は同じ前半を使い、終了時だけ振り抜き・身体を起こす姿勢へ分岐する。第一打の前進率は原画と同じ時計で読み、踏着後は足元を止める。接触姿勢は2〜3コマ保持し、終端では振り抜きも残す。全画面を止めるヒットストップは追加していない。

通常立ち絵と採用済み待機Aは維持し、攻撃の最初と最後は待機Aの第0フレーム・倍率・足元に戻す。

## 原画と配置

内蔵 ImageGen で原案8回、クロス斬りの修正2回、頭部の修正7回を生成。指示と元出力は [generation-prompts.json](generation-prompts.json)、第三打の修正指示は [generation-sweep.json](generation-sweep.json)、交差刀身と握りの修正指示は [generation-cross-fix.json](generation-cross-fix.json)、頭部の修正指示と採用原画は [generation-head-fix.json](generation-head-fix.json)。人物・衣装は `DemoApp/assets/portraits/battle/utsu_idle_right.png` を参照し、各生成に前段階の原画も参照した。

- 現在は `body-*-refit.png` と `shared-heads.png` を合成する。`body-*.png`（refitなし）と `source-*-headfix.png` は前回の修正履歴として残す。
- `step` は第2〜4セルを採用。第1セルの類似待機 `ready_new` は元の `ready` と同じ原画・配置へ統一。
- `drive`、`rise`、`hop` は各4セルを採用。
- `cross` の第2セルは、刀身と握りを直した `body-cross-fixed-refit.png` に置換。他の3セルは `body-cross-refit.png` を採用。
- `turn` は第1・2・4セルを採用。第3セルは `body-sweep-refit.png` の右手斬りに置換。
- `source-cleave.png` は比較候補として保存し、不採用。
- 25姿勢キー（`ready_new` と `ready` は同一絵）、9クリップ、保持込み162コマ。

`build-spec.py` が頭部パーツの接続点・傾き、刀身・足元の点とタイミングを定義し、[body-landmarks.json](body-landmarks.json) が原画の首元・骨盤・膝・足首を記録する。`UtsuCelExport` が身体を等比配置した後、共通の頭部を同じ倍率で接続する。頭部の非等比ワープ・逆再生・クロスフェードは行わない。キャンバスは1600×1100、足元の基準は(800,1000)。外接矩形へ合わせた人物ごとの拡縮はしない。

初版は頭の投影長から拡大率を決め、4姿勢だけ胴とすねを確認していたため、特に第三打の横薙ぎで人物が大きくなっていた。その後の体長合わせでも頭部が大きい原画は残り、待機へ戻る際に顔が縮んで見えた。そこで胴脚の倍率を [body-scale-reference.json](body-scale-reference.json) に固定し、大きい頭部を原画側で縮小して描き直した。頭部を修正した後に全身の再正規化は行わない。

しかし頭の縦寸法の検査だけでは、顔幅や髪に対する顔の面積の違いを見逃した。前回の [head-landmarks.json](head-landmarks.json) は履歴として残し、現在の見た目の合格判定には使わない。

前回、内蔵 ImageGen で共通頭部1枚・身体パーツ8枚を作成した。[generation-shared-parts.json](generation-shared-parts.json) に指示・入力・採用出力を保存。同じ向きには同じ頭部画像を使い、姿勢ごとの頭部倍率を廃止した。右斜め・右横・後ろ斜めの3方向を同じ縮尺で使用し、位置と傾きだけを変える。この段階では胴脚の倍率を当時の校正値に維持していた。最新の身体修正は下記を参照。頭部は先に縮小してから乗算済みアルファで回転補間する。

基準は実際の待機Aの `utsu_idle_00.png`（640×960）。髪頂点〜顎の目視投影長134pxを0.8倍し、共通キャンバス上107.2pxへ合わせた。共通頭部シートの対応長555pxに対して倍率107.2/555を一箇所で定義する。背面・傾きによる投影形の違いは残るが、同じ向きの顔幅・輪郭は同一画像なので変化しない。顔の縦線だけの数値検査に頼らず、`compare-parts.py` で全25姿勢の上半身を等倍で並べ、GPU録画でも待機→踏み込みと帰還を確認した。

`ready` と `ready_new` は両方とも待機Aの第0フレーム。攻撃の画素倍率2/1536に対して0.8倍で配置し、待機の1/960と一致させる。戻り際の画素一致と実表示倍率を検査する。屈みや跳躍は維持。倍率・頭部寸法・刀身長は [body-calibration.json](body-calibration.json) に記録している。

クロス斬りは切先を左上と右上の二本に限定し、刀身・鍔・柄頭の軸をそろえた。右下へ伸びていた余分な刀身を除去し、遠い手も柄を握る位置へ描き直した。新原画の鍔・切先を再計測し、剣閃の追跡座標も同時に更新した。

## 身体の比率修正（頭部統一後）

頭部統一後も、攻撃原画の肩・胴・太腿・手袋・靴が待機より大柄に見えた。内蔵ImageGenで身体8シートを再修正し、各部位の厚みと装備の大きさ、関節位置を調整した。指示・入力・採用出力は [generation-body-refit.json](generation-body-refit.json)。頭部パーツとその倍率は維持する。

最初に待機・踏み込み・初撃を同じ倍率で並べ、身体原画の修正に加えて身体側の表示倍率を前回から0.94倍へ調整した。その基準を残りへ展開。頭部は縮小しない。胴脚の昔の合計長が近いことを「同じ体格」の根拠にはせず、肩と胸の厚み、上腕・前腕、太腿・すね、手袋・膝当て・靴を全身比較で見る。関節の曲がりや遠近を含む2D原画なので、厳密な同一3D骨格の保証ではない。

旧 `body-landmarks.json` は配置用の履歴であり、今回の描き直し後の骨長ではない。誤って最新の実測値として扱わないよう `body-calibration.json` の体長合計を削除し、状態を明記した。検査は旧身体セルの混入防止・共通頭部・再生動作を担い、体格は全25姿勢の同倍率比較とGPU録画の両方で確認する。クロス斬りの鍔・切先・柄頭も修正原画から更新した。

- [3姿勢の修正前後](body-refit-three.png)：上段が前回、下段が今回。左列の待機は変更なし。
- [全25姿勢の全身比較](body-refit-all.png)：自動拡縮なし。
- [今回の等速2〜5連撃](body-refit-flow.mp4)／[身体・帰還の拡大動画](body-refit-review.mp4)

## 剣閃と当たり

購入済みEffectBlocksの `sparkle.png` を確認し、既存 `HagiLight3D` の火花を継続利用。原本は変更しない。剣閃は実際の刀身を追う既存リボンを使い、ウツだけ外縁と残光を広げる。最終打のみ両方の刀身を有効にするが、命中通知は一打に一回。

ウツの被弾リアクションは攻撃者から対象へ向かう方向に1.35倍の押し返し。既存攻撃音を打順で選び、通常はピッチ0.92、終端は0.82・音量+1.5dB。倍速によって音程を変えない。BattleCoreの判定・台本・HPは変更しない。

## 確認映像

- [共通頭部と身体修正版・拡大動画](shared-heads-review.mp4)。旧プレビューのキャッシュと区別する別名。下記の顔拡大動画と同一内容。
- [全25姿勢の上半身・等倍比較](shared-heads-compare.png)。先頭2枚は採用待機A、残りは共通頭部の合成結果。
- [2→3→4→5連撃 等速・実音声](video/utsu-flow-2-to-5-1x.mp4)
- [最初の2連撃 剣閃と火花なし](video/dance-2-bare.mp4)。共通の被弾表示は残る。音声なし。
- [5連撃 剣閃と火花なし](video/dance-5-bare.mp4)。交差した刀身を確認できる。
- [顔・帰還の拡大確認、1/2速](video/dance-5-face-closeup.mp4)。ゲームの録画を拡大し、フレーム保持で低速化。動作補間なし。
- [5連撃の2/3速GIF](video/dance-5-slow.gif)

GodotのGPU実描画、30fps・ゲーム内速度1・ズーム1.3。切り出し、表示サイズ変更、打数ラベル以外の後付け効果や動作補間はない。各段階の等速動画、低速GIF、コマ一覧を `video/` に保存。

## 検証

- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 成功、最終ビルドは警告0・エラー0。
- `UTSU_CEL_EXPORT_OK`: 25姿勢・9クリップ。
- `UTSU_DANCE_CHECK_OK checks=237 movie=False`: 全25姿勢の共通頭部参照・旧身体セルの混入防止、待機直前の原画一致、実表示倍率、交差刀身と柄の軸、同じ前半からの分岐、左右交互と最後の双剣、予備動作と帰還の剣閃抑制、足と前進、一時停止・倍速・中断・死亡・蘇生・勝利・対象変更・反転・実戦・再戦・シーク・確定HP・待機A復帰。
- GPU収録も `checks=169 movie=True`、エフェクトなしの2連撃・5連撃はそれぞれ `checks=151` で終了。
- `UTSU_MEDIA_VERIFY_OK poses=25 clips=9 frames=162 media=12`: 全PNGの透過余白、8本のMP4と4本のGIFの全フレームを復号確認。ハッシュは [verification.json](verification.json)。

Godot終了時の既存FontAdvanced・CanvasItem・ObjectDB解放警告は残る。成功マーカーと終了コード0で確認した。

## 再現

リポジトリ直下から順に実行。初回の書き出しでは `--utsu-reexport` は不要。

```powershell
python design/art/utsu/attack-flow-v4/build-spec.py
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://UtsuCelExport.tscn -- --utsu-reexport
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://UtsuDanceCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 --disable-vsync --write-movie 'C:/works/VillainProto/output/utsu-flow-v4/bare.avi' res://UtsuDanceCheck.tscn -- --movie --bare --two-only
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 --disable-vsync --write-movie 'C:/works/VillainProto/output/utsu-flow-v4/bare-five.avi' res://UtsuDanceCheck.tscn -- --movie --bare --five-only
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 --disable-vsync --write-movie 'C:/works/VillainProto/output/utsu-flow-v4/all.avi' res://UtsuDanceCheck.tscn -- --movie
python design/art/utsu/attack-flow-v4/encode-preview.py --raw C:/works/VillainProto/output/utsu-flow-v4/all.avi
python design/art/utsu/attack-flow-v4/compare-parts.py
python design/art/utsu/attack-flow-v4/compare-body.py
& 'D:/comfyui/venv/Scripts/python.exe' design/art/utsu/attack-flow-v4/verify-preview.py
```
