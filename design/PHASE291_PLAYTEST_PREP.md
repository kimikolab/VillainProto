# 第291期 報告 —— クグ KG-b の規定化 ＋ 試遊の準備（入口・プリセット・演出用の台本）

指示書は `design/PHASE291_PLAYTEST_PREP_SPEC.md`。**段0 はポンが採った（クグ ＝ KG-b）**。器具は新しい `playtest291`（`BattleSim/Modes/Playtest291.cs`）。
Codex 向けの台本メモは `design/PHASE291_CODEX_MEMO.md`。

## 0. 結論

- **段0 クグ ＝ KG-b は門をすべて通った。** `compare` は全 320 セル 0 件（第290期の予告どおり・クグの3行には感電の書き手がいない）。台本の指紋・過去の器具の Phase 0 ／ 自己検査は、旧のクグ（`KuguKG0`）に固定した後で第290期（`c04e128`）とバイト一致（所要時間の行と、文言を差し替えた `shock290 check` (d) の1行だけが違う・判定は ○）。
- **段1 入口**: デモの「敵ウェーブ」に区切り「── 試遊 ──」と **ボス・勇者（規定形）／ 精鋭・近衛 ／ 精鋭・大隊** を足した。倍率は波ごとに固定（ボス ＝ 素の値・精鋭 ＝ HP 1000% ／ 攻 300%）。精鋭の定義は `BattleSim/Modes/Elite.cs` から `EnemyCatalog`（`EliteGuardWave` ／ `EliteBattalionWave` ／ `EliteScale`）へ移し、`elite` はそれを読むだけにした（`elite check` ／ `boss283 check` はバイト一致）。
- **段1 試遊プリセット**: `Presets.Playtest`（5行）をデモのプリセットの末尾に連結した。`Compare` ／ `Cross` には入れていない。
- **段2 演出用の台本**: 表示専用の出来事を4種（`ShockGauge` ／ `Feather` ／ `Scar` ／ `MarkLayer`）と、既存の出来事に印を2つ（トウの粉の経路 `PowderRoute`・糸の放電の `PartnerId` ／ 「ほどけ」）足した。**盤面は1ビットも変わっていない**（verbose の有無で 1,580 戦が一致・`compare` 0 件）。
- **段3**: `design/PHASE291_CODEX_MEMO.md` を書いた。Web 側のブリーフ `design/CODEX_BRIEF_SHOCK_MARK.md` は実装の後にポンが置いた。本文は書き換えず、台本と食い違う所を末尾の「注記」に9項目足した（指示書 §5）。

## 1. 段0 —— クグ ＝ KG-b

- `UnitCatalog.Kugu` ＝ KG-b（札: 組み付き ＋ `Thread` ＋ `ThreadCharge`）。`KuguKGb = Kugu`、**旧の規定は `KuguKG0`**（名前は決めたこと 1）。`KuguKGa` は `KuguKG0` の末尾に `Thread` を足した版のまま。
- 文面（規定）: 「…動けなくする。組み付いた敵とは糸で繋がっている。自分に流れ込む電気は、糸を伝ってその敵へ流れ、浴びた敵は帯電する」（指示書の文のとおり・`playtest291 check` (a) が文字列で照合）。
- **過去の器具の固定**:
  - `BattleSim/Grade.cs` と `BattleSim/Modes` の 13 ファイルで `UnitCatalog.Kugu` を `KuguKG0` に機械置換した（`Burst` ／ `Confirm` ／ `Creak3` ／ `EnemyScale.Run` ／ `Lastslot` ／ `RebirthA2` ／ `Scorch` ／ `Shock.P216` ／ `Shock.R216` ／ `Shock287` ／ `ShockDigest` ／ `Swap` ／ `Whip`）。
  - `shock290` は手で直した: 名前で引く駒（`ByShort`）のクグを `KuguKG0` に、`compare` の行を `Rows290()`（クグ → `KuguKG0`）に、版の差し替え元を `KuguKG0` に。自己検査 (d) は「旧のクグは組み付きだけ・糸の札の保持者は規定のクグ1枚」に差し替えた。
  - `shock289` の `Pin`・`shiga288` の `Seat` ／ `Rows288`・`ShockDigest` の `CompareTou0`・`elite check` (b)・`tou279` の `Row` にクグ → `KuguKG0` を足した。
- `checkup ideal` の札の分類表に `Thread` ／ `ThreadCharge` を足した（第289期の `roster audit` の止まりの再発防止）。

| 門 | 結果 |
|---|---|
| `compare` | ○ 全 320 セル 0 件（`docs/balance.md` と一致） |
| 台本の指紋 27 本 ＋ `tome281` ／ `tome282` ／ `boss283` ／ `elite` ／ `shock289 p0` ／ `shiga288 p0` ／ `tou279` ／ `shock287` ／ `shock290 p0` ／ `shock290 check` ／ `spread` ／ `cross check` ／ `derive check` | ○ 第290期とバイト一致（所要時間の行と `shock290 check` (d) の文言の1行だけが違う） |

## 2. 段1 —— 入口と試遊プリセット

### 2-1. Phase 0 §6-2（デモの戦闘の起動口）

デモの起動口（`Main.StartBattle`）は、検証の波（`TestStages`）のために **`EnemyWave` ＋ 倍率（`BattleEngine.MaterializeEnemy`）を受ける口を既に持っていた**（9体の検証波 `TestStages` を選べるようにしたとき・`NineWaveCheck`）。足したのは選択肢と、倍率を画面の欄ではなく波の定義から取る分岐だけ。

### 2-2. DemoApp で触った行（Codex の担当外の最小限）

| ファイル | 触った所 |
|---|---|
| `DemoApp/Main.cs` | `SelectedTestStage` の範囲・`PlaytestStageBase` ／ `SelectedPlaytestStage` ／ `SelectedStageName`（波の選択の添字）、`PresetRows()`（`Presets.Playtest` を末尾に連結）、起動引数 `--demo-playtest-stage=<0|1|2>`、`_stagePicker` に区切りと3項目、`ShowEnemyDetails`（試遊の波は倍率を掛けた後の数値を見せる・見出し「PLAYTEST WAVE」）、`StartBattle`（試遊の波は波の倍率で `MaterializeEnemy`・背景はボス ＝ 最終波 ／ 精鋭 ＝ 第一波）、`UpdatePresetState`（帯の表示「試遊 n/5」） |
| `DemoApp/NineWaveCheck.cs` | 波の項目数の門を「本編 ＋ 区切り ＋ 検証 ＋ 区切り ＋ 試遊」に合わせ、試遊の3波も同じ手順（選ぶ → 説明 → 戦う → 敵の席と倍率 → 背景 → 戦績 → 再生し直し → 編成に戻る）で確かめるようにした |

- 確かめた: `NineWaveCheck` は 11 波すべて `ok=True`（`NINE_WAVE_CHECK_COMPLETE ok=True`）。頭なしの単発（`--demo-autoplay --demo-quit --demo-fast --demo-preset=… --demo-playtest-stage=…`）で 標 ボス台 × ボス（勝ち T6）・雷の型 × 近衛（勝ち T5）・糸 × 大隊（負け T6）が `DEMO_SMOKE_COMPLETE` まで走り、BattleSim の同じ seed と勝敗・決着T が一致した。

### 2-3. 試遊プリセット（`Presets.Playtest`）

| 行名 | 席（前1・前3・中央・後1・後3） | 出典 |
|---|---|---|
| 試遊・感電 火の型 | シガ・ガルド・ベニ・トウ・ソラ | 第287期 R4 近衛1 |
| 試遊・感電 雷の型 | トウ・ソラ・シガ・カタ・ドハ | 第290期 `shock290 grid3 guard sib krb` の1位 |
| 試遊・感電 糸 | ソラ・ガルド・クグ・トウ・カタ | 第290期 `shock290 gridk guard kgb` で糸が働いた上位台（3位） |
| 試遊・標 ボス台 | ザン・ゴルム・ミサ・バン・ヒサ | 第283期 `BOSS_SUMMARY.md` §6-1 |
| 試遊・標 道中 | ミサ・ハギ・ソラ・ドルガ・ボルグ | `compare` の行 `見境改 (ミサ×薙ぎ)`（`Compare` から引く・写しを持たない） |

駒はすべて規定（版の駒は使っていない）。席は出典の並びを写し、出典をコメントに書いた。

### 2-4. Phase 0 §6-3 試遊の目安（`playtest291 rates`・seed 0..49）

| 行 | ボス・勇者（規定形） | 精鋭・近衛 | 精鋭・大隊 |
|---|--:|--:|--:|
| 試遊・感電 火の型 | 0%（負けT 9.7） | **100%**（倒しT 7.3） | **100%**（6.6） |
| 試遊・感電 雷の型 | 0%（5.9） | 96%（5.9） | 46%（6.3） |
| 試遊・感電 糸 | 0%（8.5） | **100%**（7.7） | 2%（9.0） |
| 試遊・標 ボス台 | **100%**（6.0） | 0%（6.0） | 0%（4.5） |
| 試遊・標 道中 | 0%（15.0） | **100%**（7.9） | **100%**（6.9） |

- ボスはほぼ乱数を引かない（R378）——0% ／ 100% は1本の筋書きに近い。
- 雷の型の近衛 96% は第290期の格子（seed 0..199）の 97.5% と合う。糸の台の近衛 100% も第290期の 100% と合う。
- **標のボス台は精鋭に全く届かない**（近衛 ／ 大隊とも T4〜6 で全滅）、感電の台はボスに全く届かない。試遊ではこの非対称がそのまま見える。

## 3. 段2 —— 演出用の出来事（表示専用）

### 3-1. Phase 0 §6-1 欲しい絵 → 今ある台本

| 駒・機構 | 今ある台本で足りたか | 本期に足したもの |
|---|---|---|
| シガ・蓄電 | ✕（私有キーとログだけ） | `ShockGauge`「蓄電・増えた ／ 使った ／ 使い果たした」 |
| シガ・割り込み | △（割り込みの鞭の `Attack` は `Reaction = true` で区別できたが、合図・主目標の見出しが無い） | `ShockGauge`「割り込み」（見出し・合図 ＝ `PartnerId`） |
| シガ・怖気 | ✕（痺れのカウンタとログだけ。`Cowed` は敵の竦みで別物） | `ShockGauge`「怖気」 |
| カタ・雷雲 | ✕（雷雲は `Thunder` の `Amount` に溶けていた） | `ShockGauge`「雷雲」（育つ瞬間）・「雷雲の雷」（その雷の雷雲） |
| トウ・粉 | △（`StatusGain` shock は出るが、主目標 ／ 隣 ／ 漏れの区別が無い） | `StatusGain` に `PowderRoute`（新しい欄）・隣は `SpreadFromId` ＝ 主目標・漏れは `FriendlyFire` |
| クグ・糸 | △（第290期から `Discharge` に `SourceTrait = Thread`。① ／ ② とほどけの区別が無い） | 糸の `Discharge` に `PartnerId`（② の元の駒）と `Text = "ほどけ"` |
| ミサ・羽 | ✕（追う発は普通の `Attack`、乱射は `Damage` だけ） | `Feather`「増えた ／ 失った ／ 連射 ／ 追う ／ 流れた ／ 乱射」 |
| ミサ・爪痕 | ✕（ログだけ・最大HP は台本に無い） | `Scar`（縮んだ量 ・新しい最大HP） |
| 標の層 | ✕（ザンの初回だけ `StatusGain` marked・積み増しは無い） | `MarkLayer`（層が意味を持つ戦だけ） |

### 3-2. 実装

- 口は `BattleEngine.cs` の「第291期 —— 表示専用の口」の区間（`EmitShockGauge` ／ `EmitFeather` ／ `EmitScar` ／ `EmitMarkLayer`）。**verbose でなければ最初の比較で抜ける**。呼び出し側は保持者の札の後ろ（`_chargeLive`・`Thundercloud` の保持者・`_featherLive`・`RuptureScar` の保持者・`_ruptureLive`・`ChargedPowder` の保持者・`_threadLive`）。
- `BattleEventKind` は末尾に4つ（既存の番号は動いていない）。`BattleEvent` に欄を1つ（`PowderRoute`）。
- `MarkShock` に省略可の引数を2つ（経路・隣の元）——渡すのはトウの粉だけで、他の書き手は1ビットも変わらない。

### 3-3. 自己検査（`playtest291 check`・すべて ○）

| 項目 | 結果 |
|---|---|
| (a) 規定のクグ ＝ KG-b・`KuguKGb` ＝ 規定・`KuguKG0` は組み付きだけ・文面 | ○ |
| (b) 試遊の波（ボス ＝ 素の値・精鋭 ＝ `elite` と同じ定義）・`TestStages` 3本 ／ `Stages` 5本のまま | ○ |
| (c) 試遊の行は5行・`Compare` 64 ／ `Cross` 12 に入っていない・駒は規定・道中は `見境改` と同じ台 | ○ |
| (d) **表示専用**: verbose の有無で勝敗・決着T・全員の HP が同じ（試遊 5 台 × 3 波 × seed 20 ＋ `compare` 64 行 × 5 波 × seed 4 ＝ 1,580 戦） | ○ 0 戦 |
| (e) 保持者のいない戦では新しい種類・印が1件も出ない | ○ |
| (f) 蓄電 ／ 雷雲の値の向き | ○ |
| (g) 割り込みの見出しの次の `Attack` はシガの手番外（`Reaction`）で的が見出しと同じ | ○ |
| (h) 連射の見出しの後の1発ごとの札が 1, 2, … と並び、数が発数以下 | ○ |
| (i) 爪痕の量 > 0・最大HP ≧ 1 ／ 標の層 ＝ 前 ＋ 1 | ○ |
| (j) 試遊の台で主な札が実際に出る | ○（糸 ① ＝ クグ自身の弾けを糸へ、は試遊の台では「ほどけ」の中だけに出る） |
| (k) 表示専用の口は乱数を引かず盤面を書かない（区間の走査） | ○ |

### 3-4. 1戦あたりの件数（`playtest291 events`・seed 0..49・抜粋）

| 行 × 波 | 割り込み | 怖気 | 蓄電 増 | 雷雲 | 雷雲の雷 | 粉 主 ／ 隣 ／ 漏れ | 糸 ② ／ ほどけ | 羽 連射 ／ 追う ／ 乱射 ／ 失った | 爪痕 | 標の層 |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| 火の型 × 近衛 | 4.88 | 0.98 | 5.32 | | | 5.68 ／ 10.04 ／ 6.32 | | | | |
| 雷の型 × 近衛 | 7.64 | 0.12 | 8.52 | 2.88 | 4.52 | 4.26 ／ 6.44 ／ 5.58 | | | | |
| 糸 × 近衛 | | | | 2.94 | 6.52 | 6.50 ／ 10.60 ／ 4.94 | 2.98 ／ 0 | | | |
| 糸 × ボス | | | | 5.76 | 2.52 | 2.00 ／ — ／ 4.00 | 1.16 ／ 1.80 | | | |
| 標 ボス台 × ボス | | | | | | | | 5.00 ／ 54.00 ／ 0 ／ 0 | 53.00 | 15.00 |
| 標 道中 × 大隊 | | | | | | | | 1.80 ／ 3.14 ／ 0.66 ／ 0.40 | 2.88 | 1.28 |

## 4. 台本の指紋（指示書 §4-3）

表示専用の出来事を足すと、**その駒がいる台本の指紋は変わる**。比べ方は第231期以降の作法（前の期の器具は新しい種類を外して比べる）に揃えた。

| 本 | 新しい種類を含めたまま | 新しい種類 ／ 欄を外して（本期の器具の形） |
|---|---|---|
| `shockdigest` 18 本（`k0` ／ `aka` ／ `t216` ／ `w217` ／ `m218` ／ `m219 cat` ／ `m219 m5` ／ `e223` ／ `k226` ／ `l227` ／ `h228` ／ `g229` ／ `s232` ／ `a231` ／ `w230` ／ `r225` ／ `f224` ／ `d222`） | **18 本とも変わった**——`BattleEvent` に欄（`PowderRoute`）を1つ足したので、全プロパティを名前で畳む指紋は全行が動く。加えて旧シガ（`ShigaG3K`・責め苦）の怖気・旧カタ・旧トウの粉の印が乗る | ○ 18 本とも第290期とバイト一致（`SkipAlways` に4種・`SkipProps` に `PowderRoute`） |
| `som276 digest`（旧シガ ＋ 旧カタ ＋ 旧ソムの台） | 台×波の指紋 16 本のうち 14 本と行数が変わった（旧シガの怖気が乗る） | ○ 一致（`CheckWave.Dig` が4種を外す） |
| 火の台本 10 本（`borgfront` ／ `fireward` ／ `firescale` ／ `fireburst` ／ `enemyfire` ／ `firecycle` ／ `firetri` ／ `firefinish` ／ `fireatk` ／ `firekindle`・**全文**で比較） | 9 本一致。**`firescale` だけ 6 行増えた**——すべて `E ShockGauge … 怖気`（台の旧シガが動ける主目標に怖気づいた瞬間）。前後の行・勝敗・決着T・ログは1行も動いていない | （外していない・差は上の6行だけ） |
| `tome281` ／ `tome282` ／ `boss283` ／ `elite` ／ `shock289 p0` ／ `shock289 check` ／ `shiga288 p0` ／ `shiga288 check` ／ `tou279` ／ `shock287` ／ `shock290 p0` ／ `shock290 check` ／ `spread` ／ `cross check` ／ `derive check` ／ `compare` | ○ 第290期と一致（所要時間の行と `shock290 check` (d) の文言の1行だけ違う） | — |

**盤面が変わっていないこと**: `compare` 全 320 セル 0 件、`docs/` の数値の出力（`balance` ／ `quality` ／ `chain` ／ `ablation` ／ `pulse` ／ `engage` ／ `crossing` ／ `stock` ／ `harm` ／ `elite` ／ `layout` ／ `reseat`）が差分 0、`playtest291 check` (d) で verbose の有無が 1,580 戦一致。
**封印し直した本**: 無い（凍結庫の封印 `design/freezer/seals.tsv` の本はこの期に回していない。`shockdigest` は外す種類を足した形で第290期の値のまま）。

## 5. 決めたこと（指示書に書いていない判断）

1. **旧の規定のクグは `KuguKG0`**（KG-a ／ KG-b に並ぶ「糸なし」の名前）。
2. **精鋭の定義は `EnemyCatalog` に置き、試遊の波の一覧 `EnemyCatalog.PlaytestStages` も BattleCore に置いた**（DemoApp が写しを持たないため）。ボスの倍率は素の値（`EnemyScaleRule.None`）——第269期以降のボスの器具と同じ。
3. **シガ・カタの出来事は1つの種類 `ShockGauge` に札でまとめた**（火勢 `FireLevel` と同じ作り）。種類を増やすと再生側の分岐が増えるため。
4. **トウの粉の経路は新しい欄 `PowderRoute`** にした（`StatusGain` の `Text` は状態のキーで埋まっている）。隣の敵は `SpreadFromId`、漏れは `FriendlyFire` にも重ねた（ラウの伝染と同じ線の引き方が使える）。
5. **標の層は層が意味を持つ戦（炸裂の保持者がいる戦）だけ**出す。それ以外の戦では層は常に 1 で、既存の `StatusGain` で足りる。
6. **怖気を足した**（指示書は「既存なら不要」）——既存の `Cowed` は敵の竦みで、シガ自身の怖気は台本に無かった。
7. **前の期の台本の指紋は新しい種類を外して比べる**（`shockdigest` の `SkipAlways` ／ `SkipProps`・`CheckWave.Dig`）。第231期以降の作法と同じ。
8. **`NineWaveCheck.cs` を直した**（Codex の確認シーンだが、波の項目数を門にしているので、入口を足すと必ず落ちる）。演出のファイルではないので最小限に合わせた。

### 5-1. ブリーフとの突き合わせ（`CODEX_BRIEF_SHOCK_MARK.md` の末尾の注記）

| ブリーフの箇所 | 台本の実際 |
|---|---|
| §0 ／ §5 クグ「規定に入った場合」 | 入った（KG-b） |
| §1 1発ごとの攻撃は `Attack` → `Damage` | 乱射の発は `Damage` だけ |
| §1 羽で削った分だけ最大HPが縮む | 爪痕は標を追う発だけ（乱射は残さない） |
| §1 撃ち出した羽は戻らない | 失うのは乱射した数だけ・連射の後に1回 |
| §2 怖気「既存の演出があれば」 | 既存は無かった → `ShockGauge`「怖気」を足した・割り込みでは出ない |
| §2 感電が付くたび蓄電 | 新しく付いたときだけ・上限では出ない |
| §4 トウの眼状紋が光ってから粉 | 光る瞬間の出来事は無い・粉は `Damage` の後 |
| §5 自分の弾けの放電（①） | 試遊の台ではほぼ「ほどけ」の中だけ |
| §6 標の層 | ミサのいる戦の敵の標だけ・ヒサの標は層にならない・消える瞬間は出ない |

## 6. 回帰・作法

| 項目 | 結果 |
|---|---|
| `compare` | ○ 全 320 セル 0 件 |
| docs/ 再生成（16 ファイル ＋ `rules.md`） | ○ 動いたのは `units.md`（クグの文面・`Thread` ／ `ThreadCharge` の保持者が規定のクグになった）、`watch.md`（`BattleEventKind` 72 → 76 種・初期化子 90 → 94 箇所・新しい4種の行）、`roster_audit.md`（クグの札の列・所要時間）、`rules.md`（`EnemyScaleRule` の「測った診断」に `playtest291`——ノブの既定は動いていない）。ほかの 12 ファイルは差分 0 |
| `audit` | ○ ずれ 0 件。`CLAUDE.md` の門: 275 行 ／ 42,981 B |
| `sweep fast` | ○ 合格（22 本・異常終了 0・上限 0） |
| `sweep full` | 回していない（指示書 §7: 夜間の `sweep nightly` に任せる） |
| 自己検査 `playtest291`（11 項目）／ `shock290` ／ `shock289` ／ `elite` ／ `boss283` ／ `misa285` | ○ すべて ○ |
| DemoApp | ビルド 0 エラー。`--map11-phase0` ／ `171` ／ `172` ／ `173` すべて `ok=True`。`NineWaveCheck` `ok=True`（11 波）。Codex の確認シーン `DischargeCheck`（「第220期B2・第五波 seed4 放電(敵)=79」）／ `SpecialsCheck`（「毒系の陽性対照」）は `--verify` で例外を出すが、**第290期（`c04e128`）の worktree でも同じ例外**で、本期の変更によるものではない（触っていない） |
| 採らなかった版 | `KuguKGa` は残置（`All` にも `Retired` にも入れていない）。旧の規定 `KuguKG0` も残置 |

## 7. ポンに出す選択肢

| 項目 | 材料 |
|---|---|
| 試遊の順番 | 感電は近衛（火の型 ／ 糸が 100%）から、標はボス台 × ボス（100%）から触ると、出来事の多い戦を先に見られる。雷の型 × 大隊（46%）と糸 × 大隊（2%）は負け筋が見える台 |
| Codex への発注 | `PHASE291_CODEX_MEMO.md` とブリーフ `CODEX_BRIEF_SHOCK_MARK.md`（末尾に注記つき）の2本で発注できる状態 |
