# sweep の凍結庫（第279期）

description: `sweep`（全診断の exit 検査）の一覧から外した本の台帳と、外し方・戻し方。**凍結は「消す」ではない**——コード・器具・コマンド表の行はそのまま残り、sweep が回さないだけ。第278期 §6 の凍結候補（上限 90 秒に3回とも当たった 78 本）をポンの決定（第279期の指示書 §1）で仕分けた。

## 1. 仕分け

| 扱い | 本数 | どこに書いてあるか | sweep では |
|---|--:|---|---|
| **凍結**（G2 火の段1 探索 24 ／ G3 重い総当たり 25 ／ G4 旧期の1コアの器具 21） | 70 | `SweepDiag.Frozen`（`BattleSim/Sweep.cs`） | 回さない |
| **audit が正の docs 生成器**（`layout` ／ `reseat`） | 2 | `SweepDiag.DocsByAudit` | 回さない。**docs のずれは `audit` が正**（下の注） |
| docs 生成器で残すもの（`roster audit`） | 1 | `SweepDiag.LimitOverride`（600 秒） | 回す（上限だけ延ばして完走させる） |
| **煙試験に置き換えた本体**（`relic grid` ／ `junk` ／ `mainwin` ／ `rejudge` ／ `expand`） | 5 | `SweepDiag.SmokeReplaced` | 代わりに `relic … smoke`（seed 200 → 20）を回す |

- **docs 生成器3本と `audit` の重複**: `audit` は戦闘0回で `docs/layout.md` ／ `docs/reseat.md` に `compare` の現行の編成名が1つ残らず現れるか（節の見出しも）を見る。生成器そのものは docs の再生成で毎回最後まで走るので、sweep の 90 秒はその部分集合でしかなかった——**重複**として sweep から外した。
  `roster audit` の出力（`docs/roster_audit.md`）は `audit` の対象に無い——**重複ではない**ので凍結せず残し、上限を 600 秒に延ばした（単独で 130〜280 秒・全コア）。
- **煙試験の形**（`design/COMMANDS.md` に明記）: 5本とも本体の `GridSeeds`（帯A）と `ConfirmSeeds`（帯B の追試）を 200 → 20 に縮めた版。行・札・枠・波は縮めていない。目的は「完走して同じ指紋を返す」ことで、**数字は測定に使わない**。所要は 14 ／ 14 ／ 14 ／ 35 ／ 19 秒（本体は上限 90 秒で一度も完走していなかった）。
- **既定の上限は 90 → 150 秒**。残った本は直列で最長 89 秒（第278期の full）——境目の本（`creak` は第277期に上限・第278期に 84.2 秒）で判定が揺れないだけの余白。**残った本は全部完走するので、上限は異常終了に数える**（第141〜278期の `KnownLong`＝「既知の長い本」は消した。載っていた 26 本はすべて凍結か audit が正へ移った）。

## 2. 凍結した 70 本と封印

**封印 ＝ 凍結した時点のソースで、上限を延ばして完走させた標準出力の指紋**（所要の行を落とした SHA-256 の先頭 16 桁・sweep の照合と同じ関数 `OutFingerprint`）。台帳は `design/freezer/seals.tsv`（`sweep seal` が書く・手で編集しない）、完走した出力の全文は `design/freezer/out/<本の名>.txt`。

**第278期の full の指紋は使えなかった**: 70 本とも上限 90 秒で切られていて、出力の指紋が1つも無い（`—`）。指示書の「各本の最後の full の指紋を封印として保存」はそのままでは書けないので、**凍結の直前の版（`363c1c8`＝第278期のコミット）を worktree に置き、上限を延ばして走らせ直した**（その worktree で違うのは `Sweep.cs` だけ——凍結した本の出力には効かない）。

（表は §5 の `sweep frozen` の出力）

## 3. 解凍の手順

凍結した本を再び使う期は、**Phase 0 で1回走らせて封印と照合する**:

1. `dotnet run --project BattleSim -c Release 0 sweep thaw <絞り込み>`（**`chcp 65001` の端末で**——cp932 では字が化けて指紋が食い違う。モードが自分で止める）。既定の上限は 7200 秒、出力は `.sweep/thaw/<本の名>.txt`
2. 判定:
   - **一致** → そのまま使える。`Frozen` から外すかは期の判断（外すなら sweep の一覧に戻り、上限 150 秒で完走する形にしてから）
   - **不一致** → 封印の版（`seals.tsv` の4列目）から HEAD までを `git bisect` で割り、出力を変えたコミットを特定する。変化が意図した盤面の変更（転生・規定化）によるものなら、`sweep seal <絞り込み>` で封印を取り直す（**取り直した期と理由を §4 に書く**）
   - **封印なし**（「未完」＝凍結のときにも完走しなかった本・G2 は取っていない）→ 封印の版を `git worktree add <tmp> <sha>` で立てて同じコマンドを走らせ、HEAD の出力と比べる（`.sweep/thaw/` の全文どうしの diff）
   - **完走せず（異常終了）** → §4 の「凍結のときに既に落ちていた本」に載っていないか先に見る
3. 凍結したまま器具を直した場合も、直した期に `sweep seal <絞り込み>` で封印を取り直す

## 4. 凍結のときに分かったこと

**上限で切る exit 検査は「落ちない」を保証していなかった。** G4（旧期の1コアの器具 21 本）を上限 1800 秒で完走させたら:

| 結果 | 本数 | 本 |
|---|--:|---|
| 完走（封印した） | 13 | `gradient` 1310 秒 ／ `bridge` ／ `seats2 degree` ／ `scapegoat` ／ `divert` ／ `divert seats` ／ `carry solo` ／ `creak alt` ／ `hold tables` ／ `wall run` ／ `shard check docs/balance.md` ／ `gust check` ／ `yoke`（190〜483 秒） |
| **異常終了（上限 90 秒より後ろで落ちる）** | **6** | `wave`（535 秒）／ `dissect`（308）／ `output`（410）／ `convert`（421）／ `spend`（387）／ `spend alt`（663）——**どれも `InvalidOperationException: Sequence contains no elements`**（`wave` ／ `dissect` ／ `output` ／ `convert` は「寄与する波」の一覧 `conW` が空で `Average`、`spend` は経路の一覧が空で `Average`） |
| 1800 秒でも完走しない（封印なし） | 2 | `seats2` ／ `tumult scan probe` |

- 6 本は第141期から sweep に入っていたが、**一度も 90 秒を越えて走ったことがない**ので落ちていることが見えなかった（第141期の `KnownLong` にも `spend alt` ／ `creak alt` が「既知の長い本」として載っていた）。落ちる場所はどれも測り終えた後の集計で、**波や駒が入れ替わって「寄与する波」が 0 本になった**ことが原因と読んでいる（直していない——凍結した本なので、使う期が直す。指紋は落ちる前の出力の分を控えてある）。
- 異常終了した本の `seals.tsv` の行は exit が負（`0xE0434352` ＝ .NET の未処理例外）で、**封印としては使わない**（`sweep thaw` は「封印なし」と同じに扱う）。
- G3（重い総当たり 25 本）は sweep の full の後で上限 600 秒の封印を取る（§5）。**G2（火の段1 探索 24 本）は封印を取っていない**——1本 20 分〜2 時間（第233〜252期の報告の実測）で、24 本で 1 日を超える。解凍の期に封印の版と HEAD の両方で走らせて比べる（§3 の「封印なし」）。

## 5. 封印の結果（`sweep frozen`・第279期のコミットの時点）

封印の版の `363c1c8+作業中` は「第278期のコミット ＋ 第279期の `Sweep.cs`（封印の器具）」——凍結した本の出力には効かない差分。G3 の封印は第279期のコミットの後に別のコミットで足す（§4）。

| コマンド | 群 | 期 | コマンド表 | 封印の版 | 封印 |
|---|---|--:|:-:|---|---|
| `0 burnaudit pick` | G2 | 233 | ○ | — | — |
| `0 burnaudit run` | G2 | 233 | ○ | — | — |
| `0 borgguard pick` | G2 | 234 | ○ | — | — |
| `0 borgguard run` | G2 | 234 | ○ | — | — |
| `0 borgfront pick` | G2 | 235 | ○ | — | — |
| `0 borgfront run` | G2 | 235 | ○ | — | — |
| `0 fireward pick` | G2 | 238 | ○ | — | — |
| `0 fireward run` | G2 | 238 | ○ | — | — |
| `0 firelevel pick` | G2 | 242 | ○ | — | — |
| `0 firelevel run` | G2 | 242 | ○ | — | — |
| `0 fireburst pick` | G2 | 244 | ○ | — | — |
| `0 fireburst run` | G2 | 244 | ○ | — | — |
| `0 enemyfire pick` | G2 | 245 | ○ | — | — |
| `0 enemyfire run` | G2 | 245 | ○ | — | — |
| `0 firecycle pick` | G2 | 246 | ○ | — | — |
| `0 firecycle run` | G2 | 246 | ○ | — | — |
| `0 firetri pick` | G2 | 247 | ○ | — | — |
| `0 firetri run` | G2 | 247 | ○ | — | — |
| `0 firefinish pick` | G2 | 249 | ○ | — | — |
| `0 firefinish run` | G2 | 249 | ○ | — | — |
| `0 fireatk pick` | G2 | 250 | ○ | — | — |
| `0 fireatk run` | G2 | 250 | ○ | — | — |
| `0 firekindle pick` | G2 | 252 | ○ | — | — |
| `0 firekindle run` | G2 | 252 | ○ | — | — |
| `0 draft` | G3 | 69 | ○ | — | — |
| `0 draft alt` | G3 | 69 | ○ | — | — |
| `0 draft2` | G3 | 70 | ○ | — | — |
| `0 draft2 alt` | G3 | 70 | ○ | — | — |
| `0 draft3` | G3 | 71 | ○ | — | — |
| `0 draft3 alt` | G3 | 71 | ○ | — | — |
| `0 slope` | G3 | 72 | ○ | — | — |
| `0 slope alt` | G3 | 72 | ○ | — | — |
| `0 creak3` | G3 | 77 | ○ | — | — |
| `0 creak3 alt` | G3 | 77 | ○ | — | — |
| `0 traits` | G3 | 78 | ○ | — | — |
| `0 traits alt` | G3 | 78 | ○ | — | — |
| `0 pairs` | G3 | 80 | ○ | — | — |
| `0 pairs2` | G3 | 81 | ○ | — | — |
| `0 soak redo89` | G3 | 90 | ○ | — | — |
| `0 hold2 seats` | G3 | 107 | ○ | — | — |
| `0 lit reseat` | G3 | 113 | ○ | — | — |
| `0 stage rho` | G3 | 158 | ○ | — | — |
| `0 stage perm` | G3 | 159 | ○ | — | — |
| `0 stage permsd` | G3 | 159 | ○ | — | — |
| `0 stage cross` | G3 | 163 | ○ | — | — |
| `0 stage catalog` | G3 | 164 | ○ | — | — |
| `0 stage short` | G3 | 165 | ○ | — | — |
| `0 stage catalog seeds=800` | G3 | 165 | ○ | — | — |
| `0 form2 run202` | G3 | 202 | ○ | — | — |
| `0 gradient` | G4 | 5 | ○ | 363c1c8+作業中 | `CE4CDC06BDE03D40`（1310 秒・266 行） |
| `0 bridge` | G4 | 7 | ○ | 363c1c8+作業中 | `FCEB104F98F8AEAD`（465 秒・267 行） |
| `0 wave` | G4 | 15 | ○ | 363c1c8+作業中 | 異常終了 0xE0434352 |
| `0 dissect` | G4 | 16 | ○ | 363c1c8+作業中 | 異常終了 0xE0434352 |
| `0 output` | G4 | 17 | ○ | 363c1c8+作業中 | 異常終了 0xE0434352 |
| `0 convert` | G4 | 18 | ○ | 363c1c8+作業中 | 異常終了 0xE0434352 |
| `0 seats2 degree` | G4 | 45 | ○ | 363c1c8+作業中 | `B0B6CE16D0F18171`（294 秒・84 行） |
| `0 seats2` | G4 | 45 | ○ | 363c1c8+作業中 | 未完（1800 秒で切った） |
| `0 scapegoat` | G4 | 49 | ○ | 363c1c8+作業中 | `2E777FCBECBC1375`（299 秒・142 行） |
| `0 divert` | G4 | 50 | ○ | 363c1c8+作業中 | `EBD4FFEFE3A7267A`（484 秒・386 行） |
| `0 divert seats` | G4 | 50 | ○ | 363c1c8+作業中 | `5D94EB55F97C6386`（261 秒・85 行） |
| `0 spend` | G4 | 65 | ○ | 363c1c8+作業中 | 異常終了 0xE0434352 |
| `0 spend alt` | G4 | 65 | ○ | 363c1c8+作業中 | 異常終了 0xE0434352 |
| `0 carry solo` | G4 | 68 | ○ | 363c1c8+作業中 | `049400EC1715842C`（255 秒・411 行） |
| `0 creak alt` | G4 | 77 | ○ | 363c1c8+作業中 | `AB57AA95B249195C`（441 秒・51 行） |
| `0 hold tables` | G4 | 106 | ○ | 363c1c8+作業中 | `D82ADBA0A5A3E465`（346 秒・138 行） |
| `0 wall run` | G4 | 136 | ○ | 363c1c8+作業中 | `C996FA996C860DFF`（267 秒・271 行） |
| `0 shard check docs/balance.md` | G4 | 138 | ○ | 363c1c8+作業中 | `58F2485D162F920E`（243 秒・87 行） |
| `0 tumult scan probe` | G4 | 144 | ○ | 363c1c8+作業中 | 未完（1800 秒で切った） |
| `0 gust check` | G4 | 151 | ○ | 363c1c8+作業中 | `D950CD5D211024B7`（251 秒・50 行） |
| `0 yoke` | G4 | 25 | ○ | 363c1c8+作業中 | `1EFAF0692483D6BE`（191 秒・241 行） |
