# トウの粉の演出

`design/CODEX_BRIEF_TOU_POWDER.md`・追補 `design/CODEX_BRIEF_DUST_BURST.md` と `design/PHASE291_CODEX_MEMO.md` §19〜26 に合わせた再生側の実装。
戦闘ルール・台本・HP計算は変更しない。

- 濃さ：胴の周囲の金色の粒と小さな「粉 n」。糸玉の濃さも独立して表示する。数値は台本の実数、常駐粒は最大12粒。帯電が弾けても残り、死亡・終了で消える。
- 粉の印：台本に印が出た駒に少量の粒を残す。規定の台本では敵だけ。
- カタ：1発目の雷と「濃さ・移した」を先読みで接続。体の粉を上へ吸い上げ、金色の雷と粉を敵へ落とす。移した通知でカタの濃さを0にする。
- クグ：トウ→クグの粉と、クグ→組み付いた相手の糸に沿う粉。到着後に相手の濃さを更新し、今回の粉はクグに残さない。以前に積もった濃さがあっても、流す通知だけで消さない。
- 重い放電：台本の `Discharge.Amount` に応じて青白い芯を重ねる。糸玉・召喚獣の端点にも対応。濃さで太さは増すが、隣の糸玉へ走る短い雷が太い塊にならない上限にする。
- 粉焼：本人の胴から赤みの白い芯・朱赤の長い針状火花・赤い光の輪が弾け、立ち絵には赤い内光と焦げ色を重ねる。麻痺の黄色と区別する。粉の濃さに応じて火花の本数・半径・残光が増す。濃い粉では最大3回の閃光。残光は約0.77〜1.04秒、点火0.28秒後に台本の本人へのダメージを表示。青白い放電と区別できる色・形にする。隣がいないボス戦でも本人だけに出る。通常の燃焼状態は付けない。
- 伝染：金色の粒を伝う短い火花が0.20秒で這う。到着してから、対応する `StatusGain` の帯電を表示。普通の放電や味方への漏れ雷と混同しない。
- 粉塵爆発：`ShockGaugeLabels.DustBurst` の受け手（本人・糸玉）で小さな赤い閃光が破裂し、直後の台本の着弾先へ青白い枝と赤い粒が走る。濃さによって針の数・半径・破裂回数（最大4回）を増す。味方側の火花は明るさ75%。
- 粉が降る：`ShockGaugeLabels.DustFall` の爆発元（糸玉にも対応）から、通知された相手へ9粒の粉を弧状に飛ばす。0.20秒の飛行が到着してから常駐粒・粉粒のマーク・「粉印」を残す。濃さがある相手は「粉 n ◇」。帯電・濃さ・HPは変更しない。印は死亡・終了・離脱・再戦で消す。

第328期から粉塵爆発とクグの電池は規定。追補の「版のみ」は§24の追記で更新されており、台本に通知があればプリセット名に関係なく再生する。
試遊の「試遊・感電 ポンの台」で確認できる。

## 爆竹の連なり（2026-10-14）

`DustBurstPresentation` は行動の境界を越えず、放電連鎖と粉塵爆発の直後の実際の着弾だけを読む。
第330期の `ShockGaugeLabels.DustFall`（粉が降る）も同じ連鎖の内側として扱い、途中で束を分割しない。
粉の飛行は爆発の0.035秒後に予約し、到着は遅くても一団の0.55秒時点。個別の待ち時間を足さず、0.60秒の束の中で印まで見せる。
単独の「粉が降る」通知も同じ飛行を使う。到着前は印を付けず、停止中は到着を保留し、中断された飛行や既に死亡した相手には印を付けない。
一団の放電を0〜0.24秒にずらし、着火は各放電の0.075秒後。細い飛び火は着火の0.02秒後に走る。
間隔は後半ほど詰まり、全体0.60秒で終了する。HP・死亡・回復・帯電・糸玉の再帯電は元の通知順で処理する。
この一団では古い大きな粉焼の閃光・重複SEを重ねず、爆竹の束で表す。通常の粉焼の台本は従来の赤い演出を使う。

爆発ダメージは台本の `Damage.Amount` を同じ相手ごとに合計し、連鎖の0.44秒時点で各相手に1つだけ表示。
通常攻撃の数字へ足し込まず、500以上だけ大きくする。数字は0.8秒で消える。
戦闘上の隣接・濃さ・ダメージは計算しない。存在しない相手への飛び火も追加しない。
時計は一時停止・途中の速度変更と共通。離脱・再戦では予約した破裂・音・数字と終了待ちを取り消す。

音は指定済みのPhoenix Spark 001〜003の冒頭を軽く短く重ねる。小-13 / 中-11 / 大-9 dB、4枠まで。
最低35msの実時間間隔と同フレームの集約で密集を抑え、音程は維持する。追加のSE素材は不要。
購入済み `electric_sparks_2.tscn` を短い補助火花として複製し、一団に最大8 emitter。
`impact_1.tscn` は複数の衝撃面・光源を持ち、一発が大きくなるため採用しない。
赤い小破裂と青白い枝は既存の専用メッシュを使い、素材を外しても再生できる。
粉の飛行には既存の `tou_powder_grain.svg` を再利用。印の `assets/fx/tou_powder_mark.svg` は自作の粉粒模様。
購入済み `smoke_light.tscn` も確認したが、64粒・2秒の雲は短い飛行と小さな常駐印には大きいため採用しない。

粉の移動・常駐・焼の時計は本編の一時停止と途中の倍速変更に接続。
クグの糸・伝染の到着と粉焼の点火は演出の時計で待ち、画面離脱・再戦では待ちをキャンセルする。

## 素材

購入済みEffectBlocks v4の導入済み実ファイルを確認した。
`assets/energy/electric_sparks_2.tscn` を粉焼の短い補助火花に採用。
濃さに応じて粒数・速度・針の大きさ・寿命を増す。赤みの白→朱赤→深紅、単発に調整し、粒子処理材を複製して描画材を専用に作る。
細い長い火花・芯・輪は `PowderBurnBurst3D` の専用描画で補う。購入素材の火花とともに立ち絵や糸玉の奥へ埋まらないように描く。
参考画像は火花の形・明暗の参考として使い、画像そのものは素材へ取り込まない。
原本・共有リソースは変更しない。素材本体はGitに追加しない。

- 使用パス：`res://PolyBlocks/EffectBlocks/assets/energy/electric_sparks_2.tscn`
- 作者・製品：Bukkbeek / EffectBlocks v4。購入・導入情報は `../GodotApp/ASSET_RESEARCH_AND_PLAN.md`。
- 導入：既存の `PrepareAssetPreview.ps1`。素材がない環境でも自作の胴の点火・粉・亀裂で再生できる。
- `lightning.tscn`：固有の台本の端点と移動速度を維持するため、既存の雷と専用の描画を使う。
- `smoke_light.tscn`：小さな粉の印や30回前後の伝染には雲が大きいため使わない。既存 `tou_powder.png` を焦げ色の短い残像に使う。
- 新規 `assets/fx/tou_powder_grain.svg`：この作業で作成した粉粒。外部素材・画像生成サービスは使用していない。

## 追加すると効果的なSE

粉焼は指定音源 `DSGNImpt_EXPLOSION-Phoenix Spark_HY_PC-001/002/003.wav` を小・中・大へ接続済み。
火花の連続閃光と同じ段階で選ぶ：濃さ1〜2は001、3〜14は002、15以上は003。
点火と同時に小-6 / 中-4 / 大-2 dB。1倍速の上限は小0.45 / 中0.55 / 大0.65秒、末尾28%をフェード。
倍速でも音程は維持し、最低0.30秒を確保する。
同じフレームまたは85ms以内の密集は最大の段階の1音へまとめ、専用3枠で通常の雷・攻撃音と分離。一時停止・途中の倍速変更・再戦・離脱に対応。
先に指定されたSand ImpactはPhoenix Sparkへ差し替え、原本と既存の配置ファイルは保存する。
元の音源は無加工。購入SEはGitに含めず、配置元は `assets/audio/se/SOURCES.md`。
カタ・クグの粉返しと伝染の専用SEは未指定。既存の散粉・雷・放電の音を維持する。

| 動作 | 希望の音 | 長さ | 鳴らし方 |
|---|---|---|---|
| 粉焼 | 指定のPhoenix Spark 001〜003を採用済み | 小0.45 / 中0.55 / 大0.65秒上限 | 濃さによる火花の大きさで切り替え。原音程、短い末尾フェード |
| カタの粉返し | 粉が吸い上がって細く収束する | 0.15〜0.25秒 | 1発目の雷の前。既存の落雷音は維持 |
| クグの粉返し／伝染 | 粒を伝う小さな連続火花 | 0.10〜0.20秒 | 小音量。同時発動はまとめる。30回鳴っても疲れない音 |

濃さの加算ごとには新しい音を鳴らさない。必要な素材はWAV、余分な無音・長い余韻なしを推奨。

## 確認

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --powder --verify
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --powder --powder-mix
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --powder --verify --replay-only --powder-live
Godot_console.exe --path DemoApp res://ShockMarkCheck.tscn -- --powder --capture-dir=C:/works/VillainProto/output/tou-powder-check
Godot_console.exe --path DemoApp res://ShockMarkCheck.tscn -- --powder-readability --capture-dir=C:/works/VillainProto/output/tou-powder-readable
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --dust-burst --verify
Godot_console.exe --headless --path DemoApp res://ShockMarkCheck.tscn -- --dust-burst --verify --replay-only --dust-live
Godot_console.exe --path DemoApp res://ShockMarkCheck.tscn -- --dust-burst --dust-clip --capture-dir=C:/works/VillainProto/output/dust-burst
Godot_console.exe --path DemoApp res://ShockMarkCheck.tscn -- --dust-fall --verify --dust-fall-clip --capture-dir=C:/works/VillainProto/output/dust-fall
```

確認対象は「試遊・感電 粉 隣あり／隣なし」× ボス／近衛／大隊 × seed 0、初戦・再戦。
7種類の粉通知の再生件数、雷と粉返しの接続、伝染と帯電付与の接続、最終HP、終了時の消去を照合。
隣ありのボスは粉返し8回・粉焼22回、近衛は糸の粉返し2回・粉焼7回・伝染25回。
隣なしのボスは濃さ・粉返し・粉焼が0回。台本にない活躍は追加しない。
左右・1倍／2倍、途中の速度変更、死亡・離脱・再戦、購入火花を外した代替描画も確認する。
`--powder-mix` は小・中・大×左右×1・2・4倍速でMasterの実出力を採取する。
音量を10 dB上げた設定では単発のピーク約0.35〜0.62を確認し、全18条件で無音・単発のクリップなし。

粉塵爆発は濃さ1・12・30、8つの糸玉からの飛び火、合計表示、一時停止、倍速、離脱・再戦を確認。
粉が降るは左右×1・2・4倍速で、糸玉からの飛行、到着後だけの印、帯電・濃さ・HPの不変、死亡・離脱・再戦の消去を確認する。
実台本の通知件数と最終HPも照合。粉が降るの追加で、爆竹の一団を分割したり長くしたりしない。
連続SEは1・2・4倍速でMasterの実出力を測る。第328期の「ポンの台」×ボス／近衛／大隊×seed 0、初戦・再戦では
粉塵爆発がそれぞれ78／22／1回、連鎖は17／15／1組。通知件数と最終HPを照合した。

`compare` は全セル一致、`audit` はずれているファイル0件。`BattleCore` と `docs/` の変更はない。
頭なしの複数戦再生は完了後に既存のCanvasItem／Font／ObjectDB解放警告が残ることがある。
合否は `TOU_POWDER_*_OK` と `SHOCK_MARK_CHECK_OK` で確認する。
