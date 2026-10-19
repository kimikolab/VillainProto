# 第357期 報告 —— 【小さな期・試遊の台だけ】「試遊・呪い」をネル中央の並びに入れ替えた

指示書は `design/PHASE357_CURSE_BOARD_SWAP_SPEC.md`。前期の報告は `design/PHASE356_CURSE_WALL.md`。
**規則は1つも変えていない**（`Presets.Versions` の「試遊・呪い」の並びだけ）。版は並べていない。

## 1. 決めたこと

- **「試遊・呪い」＝ 第356期の C（ヒーラーなし）**: 前1 ワタ ／ 前3 ムド ／ 中央 **ネル** ／ 後1 **クビ** ／ 後3 ウツ（`Presets.CurseCenterNel()`・名前は「試遊・呪い」のまま・置き場所も `Presets.Versions` の4行目のまま）。ポンの判断。
- **旧の並び（中央 クビ ／ 後1 ネル）は `Common.CurseBoardH356`**（`Presets.CursePlay(Wata, Kubi)`・第351〜356期の「試遊・呪い」）。`Presets.CursePlay` は旧の並びを作る口として残した（中身は変えていない）。
  `Presets.Versions` を走査する器具には `Common.VersionsH356`（「試遊・呪い」だけを旧の並びに替えた写し・行の数と並びはそのまま）。
- **「試遊・呪い 壁」（ウツ・ワタ・ネル・ムド・ツギ）は比べる台として残した**（変えていない）。デバフ軸の基準はヒーラーなしの「試遊・呪い」。
- 確かめの口は `db356 s357`（1モード足した・`design/COMMANDS.md`）。`db356 log` に台 `now`（いまの「試遊・呪い」）を足した——`play` は第356期までの並びのまま。

## 2. 新しい「試遊・呪い」の確かめ

`db356 s357`（seed 0..99・`boards` と同じ計数）: いまの「試遊・呪い」＝ ワタ・ムド・ネル・クビ・ウツ（規定の駒）で、**ボス 100・9.0T ／ 近衛 100・5.0T ／ 大隊 100・4.7T ／ 強 第4波 79・10.1T**（強 第2 ／ 3 ／ 5波 0 ／ 1 ／ 0）——第356期 §5 の C の列と **7 波 7 セルとも同じ**（倒れた数まで）。

## 3. 直した器具（旧の並びに固定）

| 器具 | 直したところ |
|---|---|
| `db351` | `Top1` ＝ `Pin353(CurseBoardH356)`（`db353` は `D.Top1` を引くのでそのまま固定される） |
| `db354` | `Play` ＝ `Pin354(CurseBoardH356)` |
| `db355` | `Play` ＝ `Pin355(CurseBoardH356)`・`check` の段0 の素の台（`raw`）＝ `CurseBoardH356` |
| `db356` | `Play` ＝ `CurseBoardH356`（`boards` ／ `axes` の「試遊・呪い」の行・`log play`）・いまの台は `PlayNow` |
| `inv348 roster` | ボスを測る試遊の台の走査を `Presets.Versions` → `VersionsH356` |

`burn349 s350`（ホタかヒヨのいる台）／ `golm343 p0`（ゴルムのいる台）／ `flow347`（クグのいる台）も `Presets.Versions` を走査するが、呪いの台は条件に当たらないので触っていない。

新旧の突き合わせ（HEAD 6ce184f3 を別の worktree で建てた BattleSim ／ いまの BattleSim・「所要」の行を除く）: `db351` check ／ s352 ／ p0 ／ seats ／ boards ／ compare ／ flow ／ axes・`db353` check ／ reach ／ ukseats ／ boards ／ compare・`db354` check ／ p0 ／ boards ／ compare・`db355` check ／ p0 ／ boards ／ compare ／ axes・`db356` check ／ seats ／ boards ／ axes・`inv348` roster ／ check の **28 本すべて同じ**。

### 回帰

| 項目 | 結果 |
|---|---|
| `compare`（規定） | `docs/balance.md` と 0 セル差（試遊の台は `compare` の行ではない） |
| `docs/` | 再生成していない（「試遊・呪い」が載る生成物は無い——`docs/*.md` に「試遊・呪い」は 0 件）。`audit` ずれ 0・`CLAUDE.md` の門 OK |
| 過去の器具の照合 | `tools/digest_cache.ps1 light -Units nel,wata,utsu,uke`（HEAD 6ce184f3 は第356期の保存）: **106 本・一致 106 ／ 差 0**。照合の一覧に入っている呪いの器具は `db351 check` だけなので、`db353`〜`db356` ／ `inv348 roster` は §3 の新旧の突き合わせ（28 本）で見た |
| `sweep fast` | 合格（異常終了 0 本・上限 0 本・現役網 57 本）。`Presets` に触ったので、残りは夜間の `sweep nightly` が拾う |
| push の前 | ローカルの master に自分以外の未 push のコミットは無かった（`origin/master..HEAD` は空・`origin/master` も進んでいない） |
| DemoApp | ビルド 0 エラー。`--map11-phase0` ／ `171` ／ `172` ／ `173` すべて `ok=True`。`NineWaveCheck` ／ `DischargeCheck` ／ `ScorePanelCheck` `ok=True`。新しい「試遊・呪い」の単発の再生（seed 10・`--demo-preset=試遊・呪い` が「試遊・呪い」に当たり、席は ワタ 前1 ／ ムド 前3 ／ ネル 中央 ／ クビ 後1 ／ ウツ 後3）: **ボス T9 勝ち（420 件）／ 近衛 T5 勝ち（346 件）**——どちらも `DEMO_SMOKE_COMPLETE` |
