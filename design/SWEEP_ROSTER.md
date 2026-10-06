# sweep の現役網（第280期）

description: `sweep`（全診断の exit 検査）が回す本の台帳。**加点方式**——ここに載る本だけを回し、載らない本はすべて凍結庫（`design/SWEEP_FREEZER.md`）。一覧の本体は `SweepDiag.Active`（`BattleSim/Sweep.cs`）で、この表は `sweep roster` の出力（所要は単独で走った回の記録）。

## 1. 選ぶ基準（指示書 design/PHASE280_SWEEP_REBUILD_SPEC.md §1）

次のどちらかに当てはまる本だけが入る:

- **(a) 現行の規定の盤面を直接測る本**: `compare`・docs/ の生成器・`bosswave`・`checkwave`・`relic` の現役（器と現役の札の自己検査・煙試験）・台本の指紋（`shockdigest` と燃焼の `… digest`）・`roster audit`・`audit`
- **(b) 現行の判定・歯止めが依存する本**: 主判定19行・歯止め（第五波）・情報セルの算出（`spread`）と、その分母（`compare` の全セル・交差帯12行）を検める自己検査（`cross check` ／ `derive check`）

**入らないもの**（凍結庫・群 G6・封印つき）: 過去の実験の再現確認（採用済みの機構の当時の `pick` ／ `run`、棄却した版の診断、各期の `phase0` ／ `check` ／ `seats` ／ `confirm`）と、旧版に固定した器具（`SomS0` 固定の類）。対照としての価値は封印の指紋が保存する——使う期に `sweep thaw` で解凍する。
**転生の期の器具（`som276` ／ `nomi277` ／ `tou279`）も入れていない**——規定の盤面ではなく版を測る器具なので。採否が決まった後の規定は `compare` と指紋が見る（`som276 digest` だけは指紋として入れた）。

## 2. sweep の外で回すもの

- **DemoApp の門**（`--map11-phase0` ／ `phase171` ／ `phase172` ／ `phase173`）——sweep は BattleSim しか回さないので、夜間のスクリプト `tools/sweep_nightly.ps1` が回してサマリに足す（`design/SWEEP_NIGHTLY.md`）
- **`layout` ／ `reseat`**（audit が正）——docs の再生成で毎回完走し、`audit` が出力の整合を見る

## 3. 足す・外すとき

- **新しい器具は現役網に入れない限り sweep に入らない**。入れるときは `SweepDiag.Active` に層と理由を1行足し、`sweep roster` の出力でこの表を差し替える
- 外すときは `Active` から消すだけで凍結庫（G6）に入る。その期の `sweep seal <本>` で封印を取る（夜間の full が回っていれば、キャッシュの指紋を写してもよい——第280期の G6 はそうした）
- 検算は `sweep check` の (k) ／ (l)（下の §4 と同じ式）

## 4. 現役網（`sweep roster`）

| # | 本 | 層 | 所要（秒） | 理由 |
|--:|---|:-:|--:|---|
| 1 | `0 compare` | a | 5.2 | 代表編成 × 全波の勝率（`docs/balance.md`）。盤面そのもの |
| 2 | `0 compare quality` | a | 5.7 | 勝ち方の質（`docs/quality.md`） |
| 3 | `0 dump` | a | 0.1 | 駒・札・ステージの一覧（`docs/units.md`）。説明文と挙動のずれを止める |
| 4 | `0 chain` | a | 6.1 | 連鎖の深さ（`docs/chain.md`） |
| 5 | `0 ablate` | a | 17.7 | 1体抜きの勝率変化（`docs/ablation.md`） |
| 6 | `0 pulse` | a | 6.2 | 駒ごとの活動量（`docs/pulse.md`） |
| 7 | `0 engage` | a | 23.6 | 会戦（`docs/engage.md`） |
| 8 | `0 cross quality` | a | 29.9 | 交差帯 12 行（`docs/crossing.md`） |
| 9 | `0 grade2 stock` | a | 5.0 | 在庫（`docs/stock.md`） |
| 10 | `0 parry harm` | a | 17.2 | 害の帳簿（`docs/harm.md`） |
| 11 | `0 watch` | a | 10.0 | 見る地図の主表（`docs/watch.md` の前半） |
| 12 | `0 watch phase0` | a | 0.1 | 出来事の窓口の一覧（`docs/watch.md` の後半・ソースを読む） |
| 13 | `0 derive rules` | a | 0.4 | ノブ一覧（`docs/rules.md`・ソースと文書を読む） |
| 14 | `0 roster audit` | a | 319.0 | ロスターの棚卸し（`docs/roster_audit.md`・`audit` の対象に無い） |
| 15 | `0 audit` | a | 0.1 | docs/ の整合と `CLAUDE.md` の門（戦闘0回） |
| 16 | `0 bosswave run` | a | 0.9 | ボスの規定形 × 7台 |
| 17 | `0 bosswave compare` | a | 2.6 | `compare` の全行 × ボスの規定形 |
| 18 | `0 bosswave check` | a | 2.2 | ボスの規則の自己検査（第265〜269期の 53 項目） |
| 19 | `0 checkwave run` | a | 1.9 | チェック波の規定の組 × 7台 |
| 20 | `0 checkwave check` | a | 3.0 | チェック波の規則の自己検査 |
| 21 | `0 relic check` | a | 1.0 | レリックの器の自己検査（付けない編成は素と同一 ほか） |
| 22 | `0 relic check273` | a | 3.6 | 現役の札 12 枚の自己検査 |
| 23 | `0 relic grid smoke` | a | 13.9 | レリックの物差し（第271期）の煙試験 |
| 24 | `0 relic junk smoke` | a | 13.9 | ゴミの成立の煙試験 |
| 25 | `0 relic mainwin smoke` | a | 14.0 | 本編の固有の勝者の煙試験 |
| 26 | `0 relic rejudge smoke` | a | 34.1 | 再判定（第272期）の煙試験 |
| 27 | `0 relic expand smoke` | a | 18.2 | 確定版の物差し（第273期）の煙試験 |
| 28 | `0 shockdigest k0|aka` | a | 0.7 | 台本の指紋（感電・カタ ／ スス） |
| 29 | `0 shockdigest t216` | a | 12.2 | 台本の指紋（第216期の台） |
| 30 | `0 shockdigest w217` | a | 1.3 | 台本の指紋（シガの台） |
| 31 | `0 shockdigest m218` | a | 1.9 | 台本の指紋（ミオの台） |
| 32 | `0 shockdigest m219 cat|m5` | a | 2.4 | 台本の指紋（規定のミオ） |
| 33 | `0 shockdigest e223` | a | 2.9 | 台本の指紋（セロの台） |
| 34 | `0 shockdigest k226` | a | 3.1 | 台本の指紋（第226期の台） |
| 35 | `0 shockdigest l227` | a | 3.0 | 台本の指紋（第227期の台） |
| 36 | `0 shockdigest h228` | a | 2.9 | 台本の指紋（第228期の台） |
| 37 | `0 shockdigest g229` | a | 2.9 | 台本の指紋（第229期の台） |
| 38 | `0 shockdigest s232` | a | 3.2 | 台本の指紋（第232期の台） |
| 39 | `0 shockdigest a231` | a | 3.1 | 台本の指紋（第231期の台） |
| 40 | `0 shockdigest w230` | a | 3.1 | 台本の指紋（第230期の台・今の既定の規則で回す） |
| 41 | `0 shockdigest r225` | a | 0.6 | 台本の指紋（シオ ／ セロの台） |
| 42 | `0 shockdigest f224` | a | 3.4 | 台本の指紋（第224期の台） |
| 43 | `0 shockdigest d222` | a | 0.9 | 台本の指紋（シオ ／ ヨミの台） |
| 44 | `0 borgfront digest` | a | 1.9 | 燃焼の指紋（第235期の台） |
| 45 | `0 fireward digest` | a | 1.9 | 燃焼の指紋（第238期の台） |
| 46 | `0 firescale digest` | a | 3.1 | 燃焼の指紋（火の段の台） |
| 47 | `0 fireburst digest` | a | 1.1 | 燃焼の指紋（第244期の台） |
| 48 | `0 enemyfire digest` | a | 1.1 | 燃焼の指紋（第245期の台） |
| 49 | `0 firecycle digest` | a | 1.0 | 燃焼の指紋（第246期の台） |
| 50 | `0 firetri digest` | a | 1.0 | 燃焼の指紋（第247期の台） |
| 51 | `0 firefinish digest` | a | 1.0 | 燃焼の指紋（第249期の台） |
| 52 | `0 fireatk digest` | a | 0.9 | 燃焼の指紋（第250期の台） |
| 53 | `0 firekindle digest` | a | 0.9 | 燃焼の指紋（第252期の台） |
| 54 | `0 som276 digest` | a | 2.0 | 感電の指紋（ソムの転生の台） |
| 55 | `0 spread` | b | 20.5 | 主判定19行・歯止め（第五波）・情報セルの算出（§4） |
| 56 | `0 cross check` | b | 5.7 | `compare` の全セル・規則の既定・`PickOne` の自己検査（主判定の分母） |
| 57 | `0 derive check` | b | 24.0 | `compare` の全セル・交差帯12行・印の全列挙の自己検査 |

現役網 57 本（a 54 ／ b 3）・所要の合計 11.2 分（単独で走った回の記録）。

## 検算（コマンド表の本の分割）

- コマンド表 838 本 ＝ 現役網 57 ＋ 凍結庫 774（第279期 70 ＋ 第280期 704）＋ audit が正 2 ＋ 煙試験の本体 5 ＝ 838
- 第279期の full の 761 本 ＝ 現役網 57 ＋ 第280期に凍結 704 ＝ 761
