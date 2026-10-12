# ソム：背きの雷・共鳴返し（2026-10-13）

`design/CODEX_BRIEF_SOM_RESONANCE.md` の追補をDemoAppの本編へ接続。
チェックポイントは `design/PLAYTEST_GUIDE_SHOCK.md`。BattleCoreと生成物docsは変更していない。
着手時にあった臨戦態勢・群れ・電撃鞭の作業差分を引き継いだ。

## 台本への接続

- 立つ獣の `Summon` と帯電の通知を一団へ結び、召喚・寝返り・威嚇・帯電を一度に描く。
  HPと攻撃対象を持つ駒は同じInstanceIdのまま保持し、絵は `SomBeast3D` が描く。敵のHP札は着地後に表示。
- `BeastThrow` の往路は0.24秒。獣が短く反動を受け、ソムは既存の驚き顔と初回の「おまえまで……」。
- `共鳴`：受けた身体の電気を0.09秒膨らませ、同じ弧を逆向きに0.20秒で戻す。復路の芯は往路の約2.3倍。
  `走る` の対象だけへ、獣に届く直前に枝を伸ばす。
- `打ち返し`：雷の到着後の帯電・蓄電を台本から反映し、シガが怖気づきから立ち直って既存の割り込み構えへ。
  鞭は直後の `Attack` で一度だけ再生する。新たな攻撃イベントや蓄電の消費を作らない。
- `糸`：クグから台本の `PartnerId` へ既存の導電糸を走らせる。
- `共鳴が止まった`：光りかけた身体を紫の二枚の札で押さえ、復路を出さない。
- `投げて暴発`・`共鳴返し`・`弾けて消えた`：それぞれの `BeastBurst` で獣の絵と駒を消す。
  鞭で死亡する獣は `Death` で弾け、通常の倒れた立ち絵を残さない。糸・拘束もこの位置で片付ける。
- ハギの追い打ちは既存のAttack経路を使用。ツギの `Plank 瓦礫を拾った` は保持した獣の消滅位置から飛ぶ。
  光の回収・魔導書・衣は既存のSpark経路を使う。

群れは全獣の往路、体表の共鳴、復路、枝分かれをそれぞれ一拍にまとめる。
投射だけの追加時間は0.33秒、共鳴ありは0.57秒で、匹数では増えない。
鞭のAttack、暴発後の放電・追い打ち・光などは台本順に続ける。
先読みしてまとめるのは絵だけで、HP・帯電・Attackの件数・消去の通知は並べ替えない。
`BeastThrow`／`BeastBurst` は放電段の境界にも加え、次の投射の前に後続の放電が先走らないようにした。

## 素材

既存の `som_stunned`、`fodder_stance`、`shiga_interrupt` を再利用し、立ち絵の置換はしていない。
追加素材は `DemoApp/assets/fx/som_resonance.svg`（身体の外側の電気）と、
`DemoApp/assets/audio/se/som_throw.wav`／`som_resonance.wav`（合成の放出音・重なる共鳴音）。
音の再生成は `python design/art/som/generate-resonance-sfx.py`。外部音源を使わず、44.1kHz／16bit／モノラル。

購入済みEffectBlocks v4の `energy/electric_sparks_1.tscn`、`lightning.tscn`、`electric_waves.tscn` の実ファイルを確認した。
1の火花を着弾・共鳴・暴発へ採用。8粒・寿命0.24秒・小さいサイズ・単発。処理材は発動ごとに複製する。
lightningは既成の落雷形、electric_wavesは平面の波なので、同じ道を往復する投射には不採用。
往復は `SomLightning3D` の進む先端と消える尾で描く。顔やHP札を白く塗り潰さない。
原本と共有マテリアルは変更せず、購入素材本体をGitへ含めない。
配置は `DemoApp/PolyBlocks/EffectBlocks/`、原本と導入手順は `GodotApp/ASSET_RESEARCH_AND_PLAN.md`。

## 時計と後片付け

新しい雷、共鳴のTween、購入火花、獣、ソムの表情は `SetSomClock`／`DelaySom` で一時停止と途中の倍速切替を受ける。
シガの鞭は既存の `SetWhipClock` を使う。
編成に戻る・再戦・終了ではソムの演出ルート、雷、獣の絵と駒、火花、音を消す。
獣の消えた位置だけは戦闘中の光・瓦礫の参照用に残し、終了時に消す。

## 検証

- DemoAppビルド成功。既存の `BorgAttackClipCheck.cs` のCS8602警告1件。
- 4台×ボス／本編第2波×12seedの96台本で、全往路・全召喚・共鳴の枝・打ち返しのAttackを結べることを確認。
  複数本を一団にまとめた区間は347件。
- 実台本11ケースを初戦・再戦の22戦で照合。召喚・構え・投射・各分岐・消去・光・衣・瓦礫の件数と最終HPが一致。
  光の盾、打ち返し、追い打ち、瓦礫、緊急、粛、反転、ソム不在を含む。
- 左右両陣営×1倍／4倍×共鳴・通常命中・糸・粛・鞭の描画20ケースを確認。
  往路と復路の一時停止、途中の倍速切替、敵HP札、鞭の接触、中断時の消去を検査。
- 本編画面でも光の盾・打ち返しを再生し、一時停止ボタンと途中の編成への離脱を確認。
- ソムの購入火花を無効にした設定でも同じ11ケースを初戦・再戦の22戦で完走し、全件数・最終HPが一致。
- `compare`／`dump` は `docs/balance.md`／`docs/units.md` と差分0行、`audit` はずれているファイル0件。

実行ログは `output/som-resonance-*.log`、キャプチャはGit除外の `design/art/som/game-check/resonance/`。
完了の印は `SOM_RESONANCE_PLAN_OK`、`SOM_RESONANCE_VISUAL_OK`、`SOM_RESONANCE_LIVE_OK`、`SOM_REPLAY_OK`、`SOM_CHECK_COMPLETE`。
頭なしと本編検査シーンの終了時に、従来のFont／CanvasItemの解放警告が残る。
起動コマンドは `DemoApp/README.md` の「ソムの背きの雷・共鳴返し」。
