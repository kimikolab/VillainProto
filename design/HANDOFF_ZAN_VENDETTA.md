# ザン・仇討ち演出（2026-10-08）

依頼: `CODEX_BRIEF_ZAN_VENDETTA.md`。台本契約: `PHASE291_CODEX_MEMO.md` §7。

## 追加内容

- 味方への直前の被弾を読み、味方からザンへ赤い線と光を走らせる。
- 専用の低い踏み込み差分へ切り替え、敵の手前へ突進。紺の姿と赤い残像、二本の短剣の細い軌跡で斬り抜け、元の席へ戻る。
- 同じ攻撃の流れ・同じザン・同じ敵への仇討ちを1束にする。剣閃は回数に応じて最大7本、実際の回数は敵側の「仇討ち ×N」に表示する。
- 敵には赤い菱形の照準と「仇」。既存の標の丸いマーカーとは別の形。
- 束のダメージをまとめて表示。ザンの頭上には「このターン 仇討ち×N」と合計を常駐表示し、ターン開始でリセットする。敵の既存の標は「標N層」と明記する。
- 自傷の台本から小さな返り血を1束に1回表示。その後、束内のヒサの叫びと回復光を回復先ごとにまとめて表示する。
- HP、標、回復、死亡の状態更新は元のイベント順。演出のためにダメージや回復を再計算しない。
- 次のAttack、ターン、死亡、蘇生、移動、Skill、Chargeで束を切る。死亡通知を挟んだ返り血も取りこぼさない。
- 終了・非表示・再戦でHUD、動き、演出の予約、専用音を終了する。

BattleCore、編成、戦闘数値、`docs/` は変更していない。

## 回数に応じた剣閃（2026-10-09）

| 束の仇討ち回数 | 剣閃 |
|---|---|
| 1〜2 | 1本 |
| 3 | 交差する2本 |
| 4〜5 | 交差する3本 |
| 6〜7 | 4本。少し明るく太い |
| 8〜9 | 5本。少し明るく太い |
| 10以上 | 放射状6本＋太い締めの1本、短い白み |

剣閃の発生間隔は40〜80ms、最初から最後までは最大0.24秒。踏み込みと帰還を含めて単閃0.52秒、複数0.72秒（通常速度）。
90回でも7本・0.72秒。敵の周囲にだけ重ね、全画面を横切る線は使わない。
締めの白みは不透明度16%から0.09秒で消える。既存の暗転演出OFF設定にも従う。
追加音源は不要。指定済みの4音を使用し、音の重なりは従来の上限内に保つ。

剣閃のサイズ調整: 通常・連続・締めの弧と光線を一律1.20倍に拡大。
長さと太さだけを変え、本数・時間・効果音は維持。調整箇所は `DrawZanCut` の `slashScale`。
画像と検証ログは `output/zan-slash-large/`、`output/zan-slash-large-console.log`。

段階検証は `ShockMarkCheck.tscn -- --zan-tiers`。1〜10回の境界と90回、左右×通常／4倍速の44ケースで、
実描画の本数・白みの回数と消去・帰還・HP不変・途中終了を確認する。
画像は `output/zan-tiers-final/`、ログは `output/zan-tiers-final.log`。
実戦再生は `output/zan-tiers-replay.log`、compare／dump／audit は `output/zan-tiers-balance.md`、
`output/zan-tiers-units.md`、`output/zan-tiers-audit.log`。
描画44ケースと実戦16再生が合格。compare／dumpは検証時点のdocsとの差分0、auditはずれ0件。
演出検証時のビルドは警告0・エラー0。その後のラベル位置調整時のビルドには並行作業中のBattleCoreから
CS0162（BattleEngine.cs:6211）とCS8602（同:11424）が出ている。本作業ではBattleCoreを変更していない。
描画検証終了時のMonoリソース解放エラーは、既存検証シーン同様にGCとフレーム待ちを入れて解消した。
正常終了（終了コード0）は `output/zan-tiers-cleanup-console.log` でも確認している。

## 表示値の意味

### 標の視認性調整（2026-10-09）

密集した敵と城壁背景で標が埋もれるため、中心の大照準と周囲の小照準に強弱を付けた。
中心は暗い縁・朱色・象牙色の三重線で強調し、緩い拡縮を付ける。
小照準は2層目から1個ずつ増え、最大6個（中心込み7個）。中心より小さく淡い赤色で、体を囲む縦長の軌道をゆっくり巡る。
初回調整で1個に集約した後、ユーザー希望により複数照準の包囲感を復活。深い層でも外周を増やさず、隣の敵へ広がりすぎないようにした。
「標 N層」は暗い背景と朱色の枠を持つ2D札へ移動。HPバーと一緒に配置・重なり回避するため、奥の敵でも文字が縮まない。
表示する層数には上限を付けず、消失と演出終了時には札を消す。

確認: `ShockMarkCheck.tscn -- --mark-readability`。城壁／草原×左右の4配置で密集した重装兵、
1／9／10／32／90層、消失・終了処理を検証。ビルド成功、`MARK_READABILITY_OK` 4件・正常終了。
複数照準復活後は1／2／3／6／7／10／90層と減少・消失時の照準数も確認済み。
最終画像: `output/mark-multi/`、ログ: `output/mark-multi-console.log`。ビルド警告0・エラー0、4配置とも正常終了。
compare／dump／auditも実行。auditはずれ0件。今回のcompareは既存docsと2行、dumpはドハの説明・特性等に差分あり。
同時進行のBattleCore変更（ドハのShareBack等）を含む作業ツリーであり、演出変更によるバランス差分0の証明には使わない。
本調整の変更先はDemoAppとこの記録のみ。生成物は `output/mark-readability-*` に保存し、docsは上書きしていない。

ダメージは通常の浮き数字と同じ `Damage.Amount` の合計（オーバーキル込み）。
`VendettaDealt` は残りHPから実際に削った量なので、別の値になることがある。
回数は `VendettaFires` と照合している。「干渉」の値を仇討ちの回数として使用しない。

現行の指定ケースでは、守り型×精鋭・近衛×seed 7は仇討ち30回・1,810を10束に表示。
循環×ボス規定形×seed 0は6回・225を6束に表示。指示書の戦績全体1,847とは集計範囲が異なる。

## 素材と効果音

- `DemoApp/assets/portraits/battle/zan_vendetta_idle_right.png`: 新規の攻撃差分。内蔵 image_gen で既存の右向き待機絵を参照して生成。既存絵は保持。
- `DemoApp/assets/fx/zan_dagger_arc.svg`: 赤と白の先細りの刃の軌跡。コードでカメラ面内の回転・拡縮・消失を行う。
- 生成プロンプト: `design/art/zan/zan-vendetta-v1-prompt.md`。
- 指定された効果音ラボのMP3を4種類組み込み済み。元音源との対応・ファイル名・音量は `design/ZAN_VENDETTA_SFX.md`。ファイルが欠けた環境のみ既存合成音へフォールバック。

## 検証コマンド

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --rally --verify --replay-only
Godot_console.exe --path DemoApp --rendering-method gl_compatibility --resolution 1440x900 res://ShockMarkCheck.tscn -- --rally --capture-dir=C:/works/VillainProto/output/zan-captures-final
```

確認項目は12回の束ね、Attack境界、自傷・型付き攻撃の除外、死亡後の返り血、左右×0.5/1/2/4倍速、踏み込み・帰還、HUD消去。
実戦は循環／三人組／守り型×ボス／近衛 seed 0、三人組×近衛 seed 2、守り型×近衛 seed 7を各2回再生。
回数、合計、叫び、回復光、最終HP・標・最大HP、再戦時の初期化を照合する。

最終結果: ビルド警告0・エラー0。`ZAN_PLAN_OK`、実戦16再生、描画8ケース、途中での非表示／復元、ターン集計のリセットがすべて合格。
ログは `output/zan-replay-final.log` / `output/zan-visual-final.log`、画像は `output/zan-captures-final/`。
追加PNGは1024×1536のARGBで、背景のアルファ0を確認済み。

`compare` / `dump` は `output/zan-balance.md` / `output/zan-units.md` に再生成して既存docsと差分0。
`audit`: ずれているファイル0件。ログは `output/zan-audit.log`。
Godot頭なし終了時に既存検証にもあるRID／ObjectDB解放警告が出るため、本体の合否は `SHOCK_MARK_CHECK_OK` を読む。

作業終盤に並行作業による第297期のBattleCore差分が現れた。ここで報告した数値と検証は演出用ビルド時点の台本に対するもの。
その別作業の差分には手を加えず、統合後の戦闘ルールの検証には含めていない。
