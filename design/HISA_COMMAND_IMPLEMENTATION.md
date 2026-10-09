# ヒサの号令・庇い演出（2026-10-09）

対象：`CODEX_BRIEF_HISA_COMMAND.md`、台本の正は `PHASE291_CODEX_MEMO.md` §9〜11。BattleCore・戦闘の規則・docs生成物は変更していない。

## 実装

- ヒサの頭上に3つの玉枠。`CommandBall.Amount` で点灯数を更新する。満杯で捨てる通知は、使うまでの連続区間につき1回だけ小さく光が漏れる。
- `Command` 直前の同じヒサ・同じ標的の `MarkLayer` を表示上だけ保留し、玉が指先を通って敵へ届いた瞬間に最後の層数を表示する。`Highlight` は種類で読み飛ばし、文章を解析しない。元の全層数・全イベントを維持する。
- 指差しで味方ミサの浮遊羽の向きを揃える。射撃は実際の `FeatherMark` のみ。号令→標撃ち→ミサの連射→ザンの仇巡りの境目に間を入れる。
- `Cover` でヒサが味方の前へ移動し、自分の胸を指す専用差分と「…こっちだ」。大柄な味方に隠れないようカメラ側にも寄せる。HP・死亡・自分の標・仇討ちは後続台本どおりに再生し、席やHomeを変えない。
- `Sealed`「粛」または `Framed`「黙る」で「…」。同じターンの重複通知をまとめる。第303期の規定に合わせ、叫び・庇いは台本にない限り生成しない。保持者が倒れれば後続の叫び・庇いをそのまま再生する。
- 玉の通知が `Heal` と `MarkRally` の間に入っても、元の1声を分割せず、回復の光も重複させない。
- 頭なし再生の自動終了では、Tweenを止め、ノードの遅延削除を処理し、C#参照をSceneTreeの終了前に解放する。画像読み込みの一時Imageも明示的にDisposeする。
- 未追跡だったトウのPNG2点の `.import` は、他の画像と同様に必要な素材設定としてステージした。削除・無視の対象にはしない。
- 鼓舞・粛を狩る身振りは未採用版のため今回の通常演出には追加しない。

## 素材

- `DemoApp/assets/portraits/battle/hisa_command_idle_right.png`：手番の号令・標付与専用。片手を腰に置き、敵を指し示して薄く笑う差分。`Command` で使用し、玉の経由点を指先（1007, 204）へ合わせた。組み込みimagegenのプロンプトは `design/art/hisa/hisa-command-prompt.md`。
- `DemoApp/assets/portraits/battle/hisa_cover_idle_right.png`：組み込みimagegenで既存絵から生成した透過PNG。プロンプトは `design/art/hisa/hisa-cover-prompt.md`。
- `DemoApp/assets/fx/hisa_command_orb.svg`：玉の空枠。
- 支給音のファイル名・台詞・尺は `design/HISA_COMMAND_AUDIO.md`。未配置時は専用音だけ無音。

## 検証

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --hisa --verify
Godot_console.exe --path DemoApp res://ShockMarkCheck.tscn -- --hisa --capture-dir=C:/works/VillainProto/.tmp/hisa-images
Godot_console.exe --headless --path DemoApp -- --demo-autoplay --demo-quit --demo-fast "--demo-preset=試遊・標 循環" --demo-playtest-stage=0 --demo-seed=0
```

- DemoAppビルド：警告0、エラー0。
- 演出：左右×1倍/2倍、空→3玉、80回の溢れ通知の間引き、玉の到着前後の層、勝手に羽を発射しないこと、庇いの移動と帰還、ヒサの死亡、途中再戦を確認。20枚の画像も確認用に保存した。
- 実戦：循環/三人組×ボス/近衛/大隊（seed 0）、三人組×第2波（seed 0）、三人組×大隊（seed 5）、計8条件を各2回。`SHOCK_MARK_CHECK_OK`。号令・玉・庇い・層・羽・仇討ちの件数、叫びの束、最終HP・最大HPを照合した。
- 第2波は沈黙2回。その後に保持者が倒れ、庇い1回が発生する現在の台本を確認。大隊seed 5でも庇い1回を確認。
- ボス規定形の終了：750イベント、4ターン、`DEMO_SMOKE_COMPLETE`、終了コード0。`Illegal instruction` と `Leaked unsafe reference`（Tween/Tweener）は再発しない。GodotのFont/CanvasItem等、少数のリソース解放警告はまだ残る。Linuxの終了コード132そのものはWindows環境では検証していない。
- `compare` / `dump` は一時出力と既存 `docs/balance.md` / `docs/units.md` の差分0。`audit` はずれているファイル0件。`git diff --check` 合格。

検証ログ・キャプチャは `.tmp/hisa-*`（作業出力・コミット対象外）。

## 標撃ちのレーザー調整（追補）

敵への `FeatherMark` が通常射撃より細く短く、着弾音も無かった点を修正。
さらに最寄りの羽を繰り返し使うと、短い軌道が敵の標・HP表示に隠れていた。

- 敵へは画面幅の18%以上離れた羽を優先し、候補を順番に使う。候補がなければ在庫の羽へ戻る。味方への誤射は従来の最寄りの羽を使う。
- 照準待ちを75msから45msへ短縮。光線幅を0.055から0.13へ増やし、全長を即時表示。白い芯を70ms保持してから消し、細い残光を添える。2倍速でも保持45ms・減衰100ms以上。
- 銃口光・着弾光・火花・小さなカメラの動きを追加。発射音と着弾音を同時に鳴らし、音程は変えず、2倍速でも音の尺は1.25倍相当までに留める。
- 標撃ち後のAttackの待ち45msを外し、被弾表示を光線に揃える。Damage後は160msから90ms、Parry後は300msから100msへ短縮。HP更新・発数・イベント順は台本どおり。
- 既存SEを使用。追加の音声素材は不要。

左右×1倍/2倍で、実際の発射位置、1通知1本、発射/着弾SE各1回、次の羽への交代、在庫・HP不変を確認。
キャプチャは `.tmp/hisa-laser-final-images/`。ビルド成功（BattleCoreの既存警告CS0162/CS8602のみ）。
実戦8条件×再戦2回で発数・号令・最終HPが一致し、`SHOCK_MARK_CHECK_OK`・終了コード0。
標循環の左右×1倍/2倍も合格（最寄りの味方への誤射、仇巡り、途中再戦・終了を含む）。
頭なし実戦検証の終了時には既存のTexture/Font/CanvasItem解放警告が残る。`git diff --check` 合格。

## 玉を消費する号令の強調と標SE（2026-10-10）

- `Command` の手番ラベルかつ消費量 `Slot > 0` のとき、ヒサと標的の周囲を明るく残す暗転と「号令 ◆×消費玉数」を表示。
- 玉の点灯を強調→指先へ集束→短い溜め→速い射出→大きな刻印が縮んで定着、の約1.22秒。再生倍率に追従し、次の羽の射撃へ渡す。
- 初回の `StatusGain(Marked)` も対応する号令があれば着弾まで表示・音を保留。層を持たない台本にも対応し、通常の標付与と層加算の重複音を防ぐ。
- 指定WAVを `mark_add.wav` として配置。一般のLock音とは分離し、標追加にだけ使用。減少・同値更新は無音。
- 再戦・画面離脱で暗転とタイトルを即時破棄。溜め中の中断でも浮遊玉を片付ける。
- 左右×1倍/2倍の表示・標音の発音回数・加算/同値/除去・再戦を検証。指定WAVは2.5秒、復号した先頭のピーク0.752、`SHOCK_MARK_AUDIO_CHECK_OK`。
- キャプチャとログは `.tmp/hisa-charge-*`。
- 最終描画は `.tmp/hisa-charge-final-images/`。消費なし・層なし初回付与も合格。ビルド警告0/エラー0、実戦8条件×2回の層数・発数・最終HPが一致し `SHOCK_MARK_CHECK_OK`・終了コード0。頭なし終了時の既存のリソース解放警告は残る。
