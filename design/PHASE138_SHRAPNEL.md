# 第138期 報告書 —— 破片に「出口」を作る（新規駒・礫のガレ）

指示書は `design/PHASE138_SHRAPNEL_SPEC.md`。**HEAD 前提: 第137期 `70bca99`。**

---

## 段1 —— Phase 0（コード変更なし）

### Q0-0. origin との同期

`git fetch` 済み。`## master...origin/master`（ahead / behind の表示なし ＝ **同期**）。
作業ツリーは未追跡2件（本指示書・`design/art/hota/compare-validation.txt`）のみで、
**第137期が記録した「未コミットの `BattleEventKind.Parry`」は既にコミット済み**
（`BattleCore/Models.cs:1732` に列挙子として入っている）。**この期も触らない。**

### Q0-1. 破片の在庫の時系列 → **段2 に繰り越す**

**現行の計数では出せない。** `ShatterGiven` は配布の総量、`ScaleWornTurns` は
ウロの纏い率（二値）で、**どちらも「ある時点で1体が纏っている量」を持っていない。**
`ScaleLeftover` は決着時の残量だが、これも**ウロだけ**を合計している（`BattleEngine.cs:6447`）。

指示書の commit 計画どおり、**段2 でターン頭の在庫センサス（計数のみ）を足してから実測する。**
紙からは導かない（第133期・第137期）。

### Q0-2. ループ検査（**最優先**）—— **閉ループは発生しない**

礫が味方を砕いたダメージで、破片を作る側が再発火しないか。3経路すべて確認した。

| 経路 | 発火口 | 礫のダメージで走るか | 根拠 |
|---|---|---|---|
| 砕け（ヒビ・`ShatterTrait`） | `OnDamaged` | **走らない** | `Traits.cs:6646` が `source.CurrentPattern == AttackPattern.Single` で早期 return。ガレの `Def.Pattern` は **単体**で、`ModifyPattern` を持つ札を1枚も載せないので `CurrentPattern` は常に `Single` |
| 集約（ウケ・`BearTrait`） | `BattleContext.Dull` の中（`BattleEngine.cs:5547`） | **走らない** | 破片を書くのは `Dull` の内側 1 箇所だけ。ガレは `Dull` を1度も呼ばない |
| 鱗（ウロ・`ScaleTrait`） | `OnAllyDeath`（供給）／ `OnAfterAttack`（消費） | **走らない** | `OnAfterAttack` の呼び出し元は `PerformAttack` の 2 箇所（`BattleEngine.cs:4278` / `4337`）だけ。`ActionKind.Skill` は `PerformAttack` を通らない（`TakeTurnCore` の `Skill` 分岐は `t.OnAction` を流すだけ） |

**`source.CurrentPattern` を読む規則は盤面に1本しかない**（`Traits.cs:6646` の砕けだけ。
grep で全数：他の 11 件は `PerformAttack` / `SelectTarget` / `ResolvePierce` の
`patternOverride ?? ...` と、台本の表示用）。**したがってループの入口は砕けの1本に限られ、
そこが単体で閉じている以上、破片 → 砕く → ダメージ → 破片 の閉ループは構造的に立たない。**

副次の確認:

- **棘（カド）・仇討ち（ザン）は味方の刃に反撃しない**（`Traits.cs:4588` / `4605` が
  `source.TeamId == self.TeamId` で return）。味方を砕いても反撃は返ってこない
- **`ApplyDamage` に渡す `pattern` は砕けが読まない。** 砕けが見るのは
  `source.CurrentPattern` であって `HitFrame.Pattern` ではない（Q0-3 の判断に効く）

### Q0-3. `Skill` で敵全体に `ApplyDamage` する前例

**味方側の前例は破裂（ゾト・`BomberTrait.OnDeath`・`Traits.cs:1357-1361`）1本だけ。**

```
foreach (UnitState foe in ctx.LivingMembersShuffled(ctx.Opponent(self.TeamId)))
    ctx.ApplyDamage(foe, EnemyBlast, self);
```

`pattern:` を**渡していない**（＝ `DamageRoute.Other` に落ちる）。
敵の詠唱兵は `Charge → 全体` だが、あれは `PerformAttack(patternOverride: All)` なので別経路。
**手番（`OnAction`）から敵全体へ回す前例**は瘴気（グザ・`MiasmaTrait.Spread`）で、
こちらは `ctx.LivingMembers(Opponent)`（**スロット昇順・乱数を1つも引かない**）を使っている。

**決めたこと（2つとも既存の形に揃える）:**

1. **列挙は `LivingMembers`（非シャッフル）を使う。** 破裂の `LivingMembersShuffled` ではない
   ——`Shuffle` は乱数を消費するので、火選り（第58期）が「`FavorRule(0,0)` が素体と1セルも違わない」を
   検算に使えなくなったのと同じ穴が開く。効果は全員一律なので順序に意味が無く、瘴気・火選りと同じ判断。
2. **`pattern: AttackPattern.All` を渡す。** 渡しても**規則は1本も動かない**——
   `HitFrame.Pattern` の読み手は `NoteNourishPath`（糧・保持者 **0 枚**）と
   `NoteHarm` の経路分類（`docs/harm.md` の帳簿）と台本の表示だけで、**分岐する規則は 0 件**
   （`BattleEngine.cs:2818` / `2696-2703` / `BattleEvent.Pattern`）。
   渡さないと `DamageRoute.Other` に落ちて**帳簿の上で「型なし」になる**ので、
   害の帳簿と再生の側で全体攻撃として読めなくなる。
   **砕かれた味方への自傷には渡さない**（`isFriendlyFire: true` ＝ `DamageRoute.Friendly`）
   ——あれは攻撃ではなく代金である。

### Q0-4. `CanAct` 偽 → `IdleTurn` → 手番市場

`BattleEngine.cs:5165-5182`（`TakeTurnCore`）で確認した。

- `CanAct` がどれか1つでも偽 → **`actor.SetCounter(StatusKeys.IdleTurn, Turn)` を立てて `Stalled` で返る**。
  理解は正しい
- **買い手が通すのは `Trait.SurrenderedTurn`**（`Traits.cs:296`）で、これは
  **`CanAct(ctx, u, ActionKind.Attack)` を偽にした札のうち `SurrendersTurnIn` が偽のものがあるか**を見る。
  ガレの `CanAct` は **`kind == Skill` のときだけ**偽になるので、`Attack` で問われると真
  → 否決した札が 0 → **`SurrenderedTurn` は真** → **号令（ガン）・据え（バン）が買い取る**
- 号令側のゲートは `idle == ctx.Turn - 1`（`Traits.cs:5659`）なので、
  **実際に潰れたターンの翌ターンだけ**払われる。無償の毎ターン収入にはならない
- **`SurrendersTurn` は既定（true）のまま何も上書きしない。** ガレが失うのは
  「本来使えたはずの手番」であって、不動（カド）・追い打ち（ハギ）のような
  「もともと持っていないターン」ではない——**ここを偽にすると市場が消える**（指示書 §1-3 の狙いの逆）
- **`Actions` は `[Skill]` 1要素にする。** `actor.ActionIndex++` は `CanAct` 通過**後**にあるので
  （`BattleEngine.cs:5195`）、周期に永久に実行できない種別を混ぜると**その要素で止まって二度と進まない**
  ——engine のコメントが明示的に警告している穴

### Q0-5. `compare` 61行・交差帯12行に触れるか → **触らない**

**ガレを `Presets` に1行も足さない。** 測定は §4-2 のローカル台で行う。

**ただし `UnitCatalog.All` にも足さない**（指示書 §5「ガレを `UnitCatalog` に残したまま」の解釈）。
理由は2つ:

1. **ロスターの上限は 52 ＝ トランプ1組で、第103期に確定している**（`UnitCatalog.All` の doc）。
   足すと 53 になり、**どの既存駒と差し替えるかは指示書自身が次期へ送っている**（Q0-5 末尾）
2. 測って採らなかった素材（オゴ・ゴウ・ヌキ・オノ・ハリ）と器具の札
   （`Undying` / `Regen` / `Reprieve` / `Tempered` / `Grade*` / `Wildfire`）は
   **全部この形**——`UnitDef` / `Trait` は残すが `All` には載せない。**新しい作法を作らない**

**採否が決まった時点で `All` へ入れる**（差し替えと同時）。段3 で「`All` に入れない」ことを
自己検査の1項目にする（`compare` が 0 件差分であることの構造的な保証にもなる）。

### Q0-6. 過去に同じ実験が無いか（`design/*.md` に限定して走査）

- **「破片を消費する」新機構は 0 件。** 第48期の棚卸しが
  「**破片 `armor` の2枚目の読み手**（書き手3・読み手1）」を**埋まっていない空白 4 件のうちの1つ**として
  名指ししている（`design/PHASE48_CENSUS.md:499` / `:319`）が、
  **第47期にウロ（1枚目）が出てから第138期まで 91 期、2枚目は作られていない。**
  第137期は**供給側**（鍵の出どころ）を触った期で、出口には1行も触れていない
- **「全体攻撃を味方に持たせる」は第127期が測っている**（`GradeAll` / `GradeStep`）。
  結論は「**全体の値段は『介入を抜けること』ではなく『一度に全員へ届くこと』**」
  ——介入の素通りは**薙ぎの時点で既に起きている**（`SelectTargetChain` の早期リターンは
  `pattern != Single` の1本）ので、**全体化の代金として数えてはいけない**
- **指示書の「ロスター初の全体攻撃」は半分外れ。** `Def.Pattern` が全体の味方は
  **いまも 0 枚**だが、第128期に `GradeStep` がドルガへ載ったので
  **条件付き（`AtkBonus >= 10`）の全体は既に盤面にある。**
  ガレが初なのは「**`Def.Pattern` にも `ModifyPattern` にも依らず、`Skill` から敵全体へ能動的に撃つ**」形である

### Q0-7. 敵側で実際に回復する駒（**次期のための下見。実装はしない**）

`ctx.Heal` の呼び出し口は **10 本**（`Traits.cs` を全数走査。CLAUDE.md の記載と一致）:
`Drain` / `Necro` / `Colossus` / `Mender` / `Alms` / `Suture` / `Devour` / `Regen` / `Drifter` / `Forsake`。

このうち**敵の `UnitDef` が持つのは 2 枚だけ**（`EnemyCatalog` 全体を走査）:

| 駒 | 札 | `Stages` の在席 | 実態 |
|---|---|---|---|
| 従軍司祭（`Priest` / `PriestG`） | **なし**（`MakeStill` の素の 40/9/8） | `Priest` は第四波 後3 | **1点も回復しない** |
| 従軍司祭長（`Chaplain`） | `Mender` | **0 波**（定義だけ残置） | 盤面に出ない |
| **施しの司祭長（`Almoner`）** | `Alms`（毎ターン 14） | **第二波 後1 のみ** | **実際に回復する唯一の敵** |
| 渇きの祭司（`Droughter`） | 盤面ルール | 第三波 中央 | 回復を**封じる**側 |

**指示書の表と1件も食い違わない。** 第134期の実測「渇きの味方の取り分 100.0%」の原因
（敵の回復役は第二波にいて第三波の渇きと一度も同席しない）がそのまま出ている。

> **§7-1（場の回復を破片に変える）の分母はこれで確定した**——
> 現状、敵側に回るのは **第二波の Almoner の毎ターン14 だけ**である。

---

## 予測（段1 で書き切った。外れても消さない）

指示書 §2 の 7 本を、実装を1回たどってから確定させたもの。

| # | 予測 | 実装のどこから来るか（たどった結果） |
|---|---|---|
| **P1** | ヒビと同席する台で最も大きく動く | 破片の書き手は **ヒビ（砕け）／ウケ（集約・`Dull` の中）／ウロ（味方の死）** の3本。台にウケが居ない以上、供給はヒビとウロの死拾いだけ |
| **P2** | ウロと同席すると取り合いになり、**符号は負** | ウロの貫きは `ModifyPattern` が `Armor > 0` の**二値**（`Traits.cs:3407`）。ガレが最大保持者を 0 にするので、ウロが最大保持者なら**その場で単体へ戻る**。纏い率（`ScaleWornTurns`）が下がる |
| **P3** | 供給が無い台では手番を捨て続け、`IdleTurn` の回数が礫の生存Tに近づく | `CanAct` 偽 → `IdleTurn` は毎ターン必ず立つ（Q0-4）。例外は痺れ・まどろみが先に潰したターンだけ |
| **P4** | 敵側分岐の発火は **0 件** | 敵の `UnitDef` に `Shatter` / `Bear` / `Scale` が **0 枚**（Q0-7 と同じ走査）。敵に `Armor` を書く経路が盤面に1本も無い |
| **P5** | ガレを含まない 61行 ＋ 12行は **±0.0** | ガレを `Presets` にも `All` にも足さないので、`Formation` から参照されない（Q0-5） |
| **P6** | 第四波（軛・1発 25 上限）で全体攻撃が切られ、`Multiplier` を上げるほど伸びが鈍る | 軛は `ApplyDamage` の **`target.Hp -= amount` の直前**で 1 回のダメージを `YokeTrait.Cap`(25) に切る。ガレの一撃は**敵1体ごとに独立した `ApplyDamage` 呼び出し**なので、**体ごとに独立して切られる**。第132期の実測では**全体が切られた率 100.0%** で、型の中でいちばん上限に食われる |
| **P7** | 纏い率（稼働率）が下がっても勝率は上がる（第137期の逆） | 第137期は「稼働率 47.4% → 62.9% で勝率は落ちた」＝**二値の読み手の稼働率と通貨の総量は逆を向く**。今回は総量を出口に変えるので、**同じ2量が逆向きに動けば §0 の読みが裏を取る** |

**P6 の補足（実装をたどって分かったこと）**: 軛が切るのは 1 回の `ApplyDamage` なので、
**全体攻撃は「1 発 25 × 敵の体数」まで通る。** 「全体化すると第四波で丸ごと潰れる」ではなく
「**1 体あたりの上振れだけが潰れる**」——`shards * Multiplier + 攻9` が 25 を超えた分が捨てられる。
したがって **P6 は「伸びが鈍る」であって「効かなくなる」ではない。**

---

## 段2 以降

（段2 以降の結果はこの下に追記する）
