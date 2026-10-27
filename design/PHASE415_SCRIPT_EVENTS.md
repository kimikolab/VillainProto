# 第415期 報告 —— 【小さな期・表示専用】デバフ軸の演出に要る台本の出来事（カーズの印・ウツの手数・ワタの引き受け元）

指示書は `design/PHASE415_SCRIPT_EVENTS_SPEC.md`。**盤面は1ビットも動かしていない**（足したのは verbose のときだけ積む台本の出来事）。

## 1. 足した出来事と出す場所

| 出来事 | 中身 | 出す場所 |
|---|---|---|
| カーズの印 | `StatusGain`・`Text` ＝ `"karz_mark"`・`ActorId` ＝ カーズ・`TargetId` ＝ 印の敵・量 1。同じ手番の `Skill`（呪いを刻む）の `TargetId` に印の敵を載せ直した（**元は入っていなかった**——`Skill` は的を選ぶ前に打たれる） | `KariMark409` が印を付ける1箇所 |
| ウツの手数 | `StatusGain`・`Text` ＝ `"utsu_hits"`・`ActorId` ＝ `TargetId` ＝ ウツ・量 ＝ 新しい手数 1〜5。開戦（ターン 0）に初期値を1件 | 手数（`SwingTurnHits` と同じ `ModifyHitCount` の畳み込み）が前に出した値と違ったとき。見る場所は開戦 ／ `Dull` ／ `Whet` で攻撃力が動いた直後 ／ 手番の枠の前後 ／ 決着の直前 |
| ワタの引き受け元 | `StatusGain`・`Text` ＝ `"wata_take"`・受け手 ＝ ワタ・`ActorId` ＝ 元の弱体の書き手・量 ＝ 引き受けた量・**新しい欄 `BattleEvent.FromId` ＝ 元の受け手** | `RelayThrough` の頭（配り ／ 押し付けより前） |

**指示書との違い（1点）**: 指示書 §1-3 は「ワタが引き受けた `StatusGain`（受け手ワタ）に元の受け手を載せる」だったが、**その出来事はもともと無かった**。渡しはワタの攻撃力を下げずに `Dull` から return するので、ワタにも元の受け手にも `StatusGain` が出ていなかった（`CODEX_BRIEF_DEBUFF.md` §1 の表の「弱体の `StatusGain`（受け手ワタ）」は実在しない）。そこで、引き受けた瞬間の出来事を新しく `"wata_take"` として足し、`FromId` をそこに載せた。`Text` を `dull` にしなかったのは、再生側が `dull` を「その駒の攻撃力が下がった」と読むため（ワタは下がらない）。
なお、配りの `CursePass`（回す）と押し付けの `CursePass` には、もともと `PartnerId` ＝ 元の受け手が入っている。

3 つとも verbose が偽なら1件も作らない。`EmitStatusGain` を通さず `Emit` へ直に積む（あちらの1回きりの読み飛ばし札 `_flSkipGainId` を消費しないため）。`replay` の JSON には載せていない（DemoApp は `BattleEvent` を直に読む）。

## 2. 確かめ

- **`compare` 0 セル**（`docs/balance.md` は1バイトも動かない）。`docs/` 全再生成の差は 2 行だけ、どちらも盤面ではない:
  - `watch.md`: `BattleEngine.cs` の初期化子 169 → 170 箇所（ソースを数える行・新しい口 `EmitScript415` の1つ）
  - `roster_audit.md`: 所要の秒数だけ
  - `audit` ずれ 0 件。`CLAUDE.md` は触っていない（280 行・49,037 バイト）
- **自己検査**（`script415 check`・新しい器具・すべて OK）:
  - 「試遊・カーズ 呪い」× ボス・seed 0（勝ち・7T・出来事 436 件）: `karz_mark` 1 件 ＝ `K409Marks` 1。印ごとに同じターンのカーズの `Skill` の `TargetId` ＝ 印の敵
  - 同: `utsu_hits` 4 件（T0:2 → T1:3 → T1:4 → T1:5・開戦時は クビの呪いで既に 2 発）・最後の量 5 ＝ 決着時のウツの手数 5
  - 「試遊・呪い」× 4 波 × seed 0..9: `wata_take` 395 件すべてに `FromId`（受け手 ＝ ワタ・元の受け手 ≠ ワタ）・量の和 ＝ ワタが拾った量（`Db351Picked`）40 戦すべて一致・`FromId` はほかの出来事に入らない
  - verbose の有無で勝敗 ／ T ／ 生存が変わらない（2 台 × 4 波 × seed 0..19 ＝ 160 戦・違い 0）
- **`light -Units kari,utsu,wata,mudo,nel`**: HEAD は Codex の DemoApp のコミット 3 本（`2f500a3e` ／ `61b3c469` ／ `17a7e1df`）が第414期の `d94a49a4` の上に乗っていた。3 本とも BattleCore ／ BattleSim ／ docs ／ tools に触れていないので、`d94a49a4` の保存を HEAD（`17a7e1df`）の保存として写して比べた。
  - 1 回目: 35 本のうち一致 17 ／ 差 18——差はすべて `shockdigest`。勝敗 ／ T は全行同じで、差は (a) 数えた出来事の件数が増えた行（新しい出来事）と (b) 件数が同じでハッシュだけ違う行（`BattleEvent` の全欄をハッシュするので、新しい欄 `FromId=` が全出来事に乗る）。**件数の情報行だけ**であることを確かめた
  - `shockdigest` に、第291期以来の作法どおり「表示専用の欄 ／ 出来事を数えない」を足した（`SkipProps` に `FromId`・`StatusGain` のうち 3 つの `Text` を飛ばす）。2 回目: **35 本すべて一致・差 0**
  - 1 回目の前に一度、ビルドが一時的なファイルの取り合い（`docs/` の再生成と同時に走った）で落ちた。回し直して通った
- **`sweep fast`**: 合格（異常終了 0 本・上限 0 本）
- **DemoApp**（触っていない・Codex の作業中のファイルはそのまま）: ビルド 0 エラー。門 `--map11-phase0` ／ `171` ／ `172` ／ `173` は `ok=True`。`DischargeCheck` ／ `ScorePanelCheck` ／ **`NineWaveCheck` は `ok=True`**（Codex の修正が入っていた）。単発の再生「試遊・呪い」「試遊・カーズ 呪い」× ボス（`--demo-playtest-stage=0`）／ 本編 第4波（`--demo-stage=3`）・seed 0 の 4 本はすべて `DEMO_SMOKE_COMPLETE`（新しい出来事を読み飛ばして最後まで通る）

## 3. Codex への一言

`CODEX_BRIEF_DEBUFF.md` §4 の3つが台本に揃いました（すべて `StatusGain`・`Text` で見分ける）。

- カーズの印: `Text == "karz_mark"`（`ActorId` カーズ → `TargetId` 印の敵）。カーズの手番の `Skill`「呪いを刻む」にも `TargetId` が入った。印が消えるのはカーズの `Death`。
- ウツの手数: `Text == "utsu_hits"`・`Amount` ＝ 新しい手数（1〜5）。開戦時（Turn 0）に初期値が1件、以後は変わったときだけ。
- ワタの引き受け元: `Text == "wata_take"`（受け手 ＝ ワタ）・新しい欄 **`FromId`** ＝ 元の受け手。直後に配りの `CursePass` ／ 書き手ワタの `StatusGain`（dull）が続く。ワタ自身の攻撃力は下がらないので、`dull` の印は出さないでほしい。
- 定数は `BattleContext.KarzMarkText` ／ `UtsuHitsText` ／ `WataTakeText`。いまの DemoApp はこの 3 つを読み飛ばして最後まで再生する。
