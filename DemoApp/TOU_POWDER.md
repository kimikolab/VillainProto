# トウの粉の演出

`design/CODEX_BRIEF_TOU_POWDER.md` と `design/PHASE291_CODEX_MEMO.md` §19〜21 に合わせた再生側の実装。
戦闘ルール・台本・HP計算は変更しない。

- 濃さ：胴の周囲の金色の粒と小さな「粉 n」。数値は台本の実数、常駐粒は最大12粒。帯電が弾けても残り、死亡・終了で消える。
- 粉の印：台本に印が出た駒に少量の粒を残す。規定の台本では敵だけ。
- カタ：1発目の雷と「濃さ・移した」を先読みで接続。体の粉を上へ吸い上げ、金色の雷と粉を敵へ落とす。移した通知でカタの濃さを0にする。
- クグ：トウ→クグの粉と、クグ→組み付いた相手の糸に沿う粉。到着後に相手の濃さを更新し、今回の粉はクグに残さない。以前に積もった濃さがあっても、流す通知だけで消さない。
- 重い放電：台本の `Discharge.Amount` に応じて青白い芯を重ねる。糸玉・召喚獣の端点にも対応。濃さで太さは増すが、隣の糸玉へ走る短い雷が太い塊にならない上限にする。
- 粉焼：本人の胴から赤みの白い芯・朱赤の長い針状火花・赤い光の輪が弾け、立ち絵には赤い内光と焦げ色を重ねる。麻痺の黄色と区別する。粉の濃さに応じて火花の本数・半径・残光が増す。濃い粉では最大3回の閃光。残光は約0.77〜1.04秒、点火0.28秒後に台本の本人へのダメージを表示。青白い放電と区別できる色・形にする。隣がいないボス戦でも本人だけに出る。通常の燃焼状態は付けない。
- 伝染：金色の粒を伝う短い火花が0.20秒で這う。到着してから、対応する `StatusGain` の帯電を表示。普通の放電や味方への漏れ雷と混同しない。

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
```

確認対象は「試遊・感電 粉 隣あり／隣なし」× ボス／近衛／大隊 × seed 0、初戦・再戦。
7種類の粉通知の再生件数、雷と粉返しの接続、伝染と帯電付与の接続、最終HP、終了時の消去を照合。
隣ありのボスは粉返し8回・粉焼22回、近衛は糸の粉返し2回・粉焼7回・伝染25回。
隣なしのボスは濃さ・粉返し・粉焼が0回。台本にない活躍は追加しない。
左右・1倍／2倍、途中の速度変更、死亡・離脱・再戦、購入火花を外した代替描画も確認する。
`--powder-mix` は小・中・大×左右×1・2・4倍速でMasterの実出力を採取する。
音量を10 dB上げた設定では単発のピーク約0.35〜0.62を確認し、全18条件で無音・単発のクリップなし。

`compare` は全セル一致、`audit` はずれているファイル0件。`BattleCore` と `docs/` の変更はない。
頭なしの複数戦再生は完了後に既存のCanvasItem／Font／ObjectDB解放警告が残ることがある。
合否は `TOU_POWDER_*_OK` と `SHOCK_MARK_CHECK_OK` で確認する。
