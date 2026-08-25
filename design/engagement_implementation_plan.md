# 会戦（Engagement）実装計画 — 作業計画書

作成: 2026-08-25。`ENGAGEMENT_PLAN.md`（指示書）を基準コミット d223a8e の現物コードと
突き合わせた結果と、フェーズごとの具体的な作業内容。指示書の判断（D1〜D11 / T1〜T4）は
すべて踏襲する。**指示書の記述と現物が食い違っていた箇所は §0 に列挙した**（指示書 §6.7 の
「実装前に止まって報告する」に相当する部分。実装はこの計画書の解釈に従う）。

---

## 0. 事前調査の結果（指示書 §6.7 への回答と、追加の発見）

### 0-1. `AtkBonus` 一律リセットで壊れる特性 → **無し**

`OnBattleStart` で `AtkBonus` を書くのは 呪詛（ネル）・号令の鬨（ガン、`OpeningGain`）・
萎縮（クビ）の3つ。いずれも「毎戦闘の開始時に掛け直す」設計で、境界で `AtkBonus` を 0 に
してから次の Battle の `OnBattleStart` が走る順序なら、二重に積まれることはない。
生き残ったネルが第2 Battle の新しい敵にも呪詛を撒き直すのは意図通り（Battle スコープの効果）。

なお戦闘中に `AtkBonus` を積む特性（被弾強化・処刑・棘・分かち・澱み喰い・軋み・縛め・
号令・移り木・墓守の `EnemyGain`）はすべて境界で消える。これは D2 の意図そのもので、
**処刑（勇者候補）の積み上げも消える**＝持ち越された敵部隊は処刑スタックを失う。
仕様として受け入れ、Phase C の報告に含める。

### 0-2. `StatusKeys` 全消去で壊れる特性 → **無し。むしろ全消去が必要条件**

- `Armor`（破片）を「持ち越せる」前提の特性は無い。供給源（砕け盾のヒビ）は Battle 内で完結。
- `IdleTurn` は**ターン番号**を持つカウンタ。消さずに持ち越すと、据え（バン）の
  `Counter(IdleTurn) >= Turn` が第2 Battle の序盤で偽に成立し続ける（前戦の大きい番号が
  残るため）。D2 の全消去はこれを正しく防ぐ。号令の毎ターン判定（`idle != Turn-1`）も同様。
- `Burn` が消えると熾火（ホタ）は次の Battle 開始時に湿った薪に戻る。ボルグが再着火する
  まで無力。これは「状態異常は Battle スコープ」（コンセプトメモ §10）の帰結で仕様。

### 0-3. `Revive` の `AtkBonus = 0` と `OnCarryOver` の整合 → **整合する。ただし既存の潜在バグを発見**

D4 の `OnCarryOver` は「`necroBonus` を 0 にしてから再適用」なので、帳簿（`necroBonus`）と
実体（`AtkBonus`）が同時にリセットされ整合する。

一方、**既存の `ctx.Revive`（BattleEngine.cs:809）は `AtkBonus = 0` にするが `necroBonus`
カウンタを残す**。蘇生されたリィカが次に層を更新すると `desired - applied` の差分しか
加算されず、実際の攻撃力が層の三角数より恒久的に低くなる（帳簿だけが正しい値を指す）。
死の連鎖はリィカ＋ヴェルの同居編成なので現行の Battle 内でも起こり得る。
**今回の作業では挙動を変えない**（指示書 §6.6）。README「未解決の課題」に転記する（§5）。

### 0-4. `MaxTurns` 引き分けの実在 → **現行 compare では 0 件（実測）**

scratchpad の使い捨てハーネスで 31編成 × 5波 × seed 0..199 = 31,000 戦を測定し、
「`PlayerWon=false` かつ `PlayerSurvivors>0`」（＝30ターン到達の引き分け）は **0 件**だった。
T1（引き分け＝味方が退く）の現在の影響範囲はゼロ。ただし会戦では消耗した部隊同士の
Battle が発生する（火力が落ち切った盤面は膠着しやすい）ので、規則としては T1 を実装し、
`engage` 実行時に発動回数を数えて Phase C で報告する。

### 0-5. 【指示書の訂正】`lastDeathTurn` の境界処理 — 分析が逆向き

指示書 §4 Phase B は「`lastDeathTurn` は次の Battle の Turn 1 で `>= Turn-1` を満たさない
ので自然減衰が1回入る → 二重減衰になるからゼロ化する」と書くが、**現物は逆**。
Turn 1 では `ctx.Turn - 1 = 0` で、カウンタは常に `>= 0` なので**連鎖中と判定され減衰は
起きない**（号令の鬨のコメントにある「カウンタ未設定の 0 が Turn-1 と一致する」のと同じ形）。

ゼロ化が必要な本当の理由: 前戦の `lastDeathTurn = T`（例: 6）が残ると、第2 Battle の
Turn 2..T+1 まで `T >= Turn-1` が成立し続け、**味方が誰も死んでいないのに減衰が止まる**
（タダで層を維持できる）。つまりゼロ化は「二重減衰の防止」ではなく「偽の連鎖判定の防止」。
実装（`OnCarryOver` で `lastDeathTurn = 0`）は指示書どおりで、コメントにはこの理由を書く。

### 0-6. 【追加】`guardPending`（庇いの印）が境界を越える

`GuardianTrait.PendingKey`（"guardPending"）は `StatusKeys` に無いので D2 の掃除で消えない。
庇い成立の印が立ったままダメージが `OnDamaged` まで届かず戦闘が終わると（破片が全額
吸った場合など）、次の Battle の最初の被弾を「庇いで受けた」と誤認して攻撃力が伸びる。
→ **`GuardianTrait.OnCarryOver` で `PendingKey` を 0 にする**（1行。`compare` には影響しない
——`OnCarryOver` は会戦からしか呼ばれない）。
`pursuit_chain`（追い打ち）は毎ターン頭に 0 リセットされるので持ち越しても無害。触らない。

### 0-7. 【追加】勝敗分岐の判定と召喚駒（胞子）の扱い

`BattleResult` は `PlayerWon` しか持たないので、指示書 §4 のアルゴリズムの3分岐
（味方全滅 / 両軍全滅 / MaxTurns）を戻り値だけでは区別できない。新 `Run` に渡した
`UnitState` リストは呼び出し側が保持しているので、**Run 後に渡した駒の `IsAlive` を見て
判定する**。このとき戦闘中に召喚された胞子（Ephemeral）はリストに居ないため、規則を明文化する:

- **持ち越すのは会戦エンジンが投入した駒だけ。召喚駒は Battle 限りで消える**
  （「儚い」のフレーバーそのもの。現ロスターで召喚は胞子のみ、敵側に召喚持ちは無い）。
- 判定は2フラグに正規化できる（指示書の3分岐と同じ動作になることを確認済み）:

      clearedE = 渡した敵駒の生存が 0
      lostP    = 渡した味方駒の生存が 0 、または r.PlayerWon == false

  - 勝利（`PlayerWon=true`）→ `clearedE` 成立。味方 originals も全滅していれば
    （＝胞子だけで勝った）`lostP` も成立し、**勝ったが部隊も尽きた**として次の部隊を投入する。
  - 敗北（味方全滅）→ `lostP` 成立。敵 originals も全滅なら `clearedE` も成立（両軍全滅）。
  - どちらも生存（MaxTurns 引き分け）→ T1 により `lostP` のみ成立。敵は持ち越し。
- フラグの立った側は次の部隊を `Materialize`、立たなかった側は `CarryOver` して続行。
  どちらかの部隊列が尽きたら終了（両方同時に尽きたら味方敗北。指示書 §4 どおり）。
- 敵側に召喚持ちを足したらこの判定（`clearedE` = originals のみ）を見直すこと、を
  コメントに残す。

なお毎 Battle 必ずどちらかのフラグが立つので、Battle 数は高々 `P + E - 1`（5v1 なら 5）。
`MaxBattles = 10` は保険であり、到達したら味方敗北で打ち切る（実際には到達しない）。

### 0-8. 【追加】会戦を跨ぐ駒の同定 — 「Slot が固定」は不正確

指示書は「D5 で Slot が固定なので `Openings` の Slot + TeamId で同定できる」と書くが、
**Slot は戦闘中に動く**（臆病の後退・喧噪の入れ替え・庇いの押し出し）。D5 の「維持」は
「境界で再配置しない＝Battle 終了時のスロットのまま次へ入る」の意味に取る（`HasFallenBack`
維持（D6）とも整合し、後衛特化のセロが第2 Battle 開幕から実績つき貫きで撃てる）。

同定の正しい手段: **前の Battle の Move イベントを再生し切った後の各駒のスロット =
次の `Openings` のスロット**。GodotApp は台本再生でスロットを追跡しているので、
Battle 終了時の表示状態と次の Opening が (TeamId, Slot) で 1:1 に対応する。
`UnitId` では足りない（敵部隊は同名・同 Id の駒を複数含む: 第一波 新兵×2、第四波 重装兵×3）。

さらに嬉しい副作用: カードは (team, slot) に紐づくので、**持ち越された駒は前の Battle で
終わった位置にそのまま表示され続ける**。「左の生存駒は動かさず」（Phase D）はほぼ自動で成立する。

### 0-9. 【追加】`BattleOpening` に `BaseAttack` を足す（指示書の型からの逸脱）

GodotApp は攻撃力を「素 → 現在」（`攻5→35`）で出すが、敵の `UnitDef` には一覧 API が無く
（`EnemyCatalog` は静的フィールドのみ）、`UnitId` から素の攻撃力を引けない。
`BattleOpening` に `int BaseAttack`（`Def.Attack`）を1フィールド足す。純粋な追加で、
verbose 時にしか作られない型なので `compare` には影響しない。

### 0-10. 【報告】文書の食い違い（実装は止めない）

- CLAUDE.md の「構成」節に **GodotApp が載っていない**（CONTRIBUTING.md は言及している）。
  §5 の design/ 追記と同じコミットで GodotApp の1行も足す。
- 指示書は「27編成」と書くが `CompareBuilds` は現在 **31編成**（指示書作成後に4つ増えた）。
  実行時間の見積もりにのみ影響。問題なし。
- 指示書の行番号ずれ: `Revive` の `AtkBonus=0` は BattleEngine.cs:813（指示書 809）、
  Summon の逆引きは Main.cs:588（指示書 590）。いずれも内容は一致。

---

## 1. Phase A — `BattleEngine.Run` の分割（挙動変更ゼロ / 1コミット）

1. `public static List<UnitState> Materialize(Formation f, int teamId)` を `BattleEngine` に
   切り出す。中身は現 `Deploy` と同一（スロット昇順、`TraitCatalog.Resolve`、`Hp = MaxHp`）。
2. `public static BattleResult Run(IReadOnlyList<UnitState> player, IReadOnlyList<UnitState> enemy,
   int seed, bool verbose = true)` を追加。渡された順に `ctx.Add`（味方リスト → 敵リスト）。
   以降は現 `Run` と同一の本文。
3. 既存 `Run(Formation, Formation, int, bool)` は `Materialize` して新 `Run` を呼ぶ
   ラッパーにする。**シグネチャ不変**（BattleSim / GodotApp / PrototypeApp を触らない）。
4. `Add` 順が現行（味方→敵、スロット昇順）と一致することを確認。行動順・`OnBattleStart` 順は
   安定ソートなので Add 順が同じなら同一。

**受け入れ**: `compare` / `chain` / `pulse` / `dump` を吐き直して差分ゼロ
（`docs/balance.md` `docs/chain.md` `docs/pulse.md` `docs/units.md`）。`layout` は回さない（D5）。
コミットメッセージに「compare 差分ゼロ」と明記。

## 2. Phase B — `Engagement` の追加（純粋な追加 / 1コミット）

**新規 `BattleCore/Engagement.cs`** — 指示書 §4 の型に §0-9 の `BaseAttack` を足したもの:

```csharp
public sealed record BattleOpening(int InstanceId, int TeamId, string UnitId, string Name,
                                   int Slot, int Hp, int MaxHp, int Attack, int BaseAttack,
                                   AttackPattern Pattern);
```

`EngagementResult` / `EngagementEngine` は指示書どおり（`MaxBattles = 10`）。

**アルゴリズム**は §0-7 の2フラグ正規化で実装する。`DeriveSeed(seed, battleIndex) =
unchecked(seed * 1000003 + battleIndex)`（D9）。

**`CarryOver(units)`**（生存駒のみ、現スロット昇順に整列して返す——Openings と Add 順を
決定的に揃えるため）:

```
foreach u in 生存駒:
    foreach key in StatusKeys.All: u.Counters.Remove(key)   // Armor も消す（§0-2）
    u.AtkBonus = 0
    foreach t in u.Traits: t.OnCarryOver(u)
```

- `StatusKeys` に `public static readonly string[] All`（6キー）を足し、掃除はこれを回す。
  「新しい状態異常はキーを足して TickStatuses に書くだけ」の手順に「`All` にも足す」を
  1行追記する（CLAUDE.md の状態異常の節）。
- `Slot` / `HasFallenBack` / `MaxHp` / 特性私有カウンタ（`charges` `sewn` 等）は触らない
  （D5〜D7）。

**`Trait.OnCarryOver(UnitState self)`** を `Trait` に追加（既定は空。`BattleContext` は渡さない）。

- `NecroTrait.OnCarryOver`: 層を1減らし（D4/T2）、`necroBonus` を 0 にしてから三角数を
  再計算して `AtkBonus` に載せ、**`lastDeathTurn` を 0 にする**（理由は §0-5 —
  「前戦のターン番号が偽の連鎖判定になって減衰が止まる」のを防ぐ。二重減衰防止ではない）。
  既存 `SetStack` は `ctx` を要求するので、ログ無しの純計算部を private ヘルパに切って共用する。
- `GuardianTrait.OnCarryOver`: `PendingKey` を 0 に（§0-6）。

**`Openings` の組み立て**: 各 Battle の `Run` **前**に投入駒の (参照, Hp, MaxHp,
CurrentAttack, Def.Attack, Slot, CurrentPattern) を控え、`Run` **後**に確定した
`InstanceId` を読んで record を組む（`Add` の振り順を外から推測して複製しない）。
`Attack` は `CurrentAttack`（墓守再適用後の値）、`Pattern` は **`CurrentPattern`**
（`Def.Pattern` 直読み禁止の絶対ルール。3層持ち越しのリィカは開幕から薙ぎで載る）。
verbose=false のときは空リスト。

**`FirstBattleAttrition`**: 分母 = 敵第1部隊の投入時 MaxHp 合計、分子 = 第1 Battle 終了時の
HP 減少合計（死亡は MaxHp 全額）。胞子は敵側に存在しないのでズレ無し。

**`BattleResult` は触らない**（指示書どおり。Phase B が純粋な追加である保証）。

**受け入れ**:
- `compare` / `chain` / `pulse` / `dump` 差分ゼロ（誰もまだ会戦を呼んでいない）
- scratchpad ハーネス（既存の `EngageProbe` を拡張）で:
  - 同一引数で2回走らせ `Battles.Count` / `PlayerWon` / 各 `Turns` が一致（決定性）
  - verbose true / false で `PlayerWon` / `Turns` / `EnemySquadsCleared` が一致
    （イベント・Opening の記録が盤面を変えていない証明）
  - 「死の連鎖 (リィカ軸)」× `Stages` 5連戦・verbose で、第2 Battle の `Openings` に
    リィカが素の攻撃力 5 より高い `Attack` と持ち越し HP で載っていることを確認し、
    ログをコミットメッセージ（または PR）に残す

## 3. Phase C — BattleSim `engage` モード（1コミット）

1. `EnemyCatalog` に `EngagementColumn`（= `Stages.Select(s => s.Enemy)`、D11）を追加。
2. `Program.cs` に `engage [絞り込み]` を追加（`reseat`/`ablate` と同じカンマ区切り部分一致）。
   各編成（味方1部隊）× `EngagementColumn` × seed 0..199。出力列:

   | 列 | 意味 |
   |---|---|
   | 突破率 | 5部隊すべて抜いた試行の割合 |
   | 期待突破数 | `EnemySquadsCleared` の平均 |
   | 第1削り | `FirstBattleAttrition` の平均 |
   | 突破分布 | 0/1/2/3/4/5 部隊抜きの試行数 |
   | 独立積 | **同じ seed 群で独立5戦を測り直した勝率の積**（理論全抜き率）。balance.md はパースしない（docs/ は読む対象ではなく出力先） |
   | 引き分け | T1 発動回数（§0-4 の追跡） |

   行順は `CompareBuilds` の並びのまま（balance.md と突き合わせて読むため）。
   実行時間は compare の2倍強（31編成 × 200seed × 最大5 Battle + 独立5戦）≒ 数分。前景で待つ。
3. 出力先 `docs/engage.md`。CLAUDE.md / CONTRIBUTING.md のコマンド一覧と「手で編集しない」
   生成物リストに各1行足す。
4. `engage2 [絞り込み]`（任意）: 同一編成2部隊。非線形性（1部隊2.3抜き vs 2部隊4.6抜き）を見る。

**受け入れ**: `compare` 差分ゼロ（engage は読むだけ）。`docs/engage.md` をコミットに含める。

**報告（README「検証で分かったこと」に追記する4点＋α）**:
- 独立5戦100%の編成が会戦で何部隊目に落ちるか
- リィカ軸が会戦で相対的に強いか（コンセプト通りか）
- ノノ / 回復持ちが会戦で浮いていないか（T4 の見直し材料）
- 「第1削り」だけ高くて突破0の編成（特攻隊候補）
- 追加: 処刑スタック消滅（§0-1）と生贄の毎戦支払い（リィカは Battle ごとに隣接味方を
  削り直す）が会戦の形にどう出たか

## 4. Phase D — GodotApp: 会戦の再生（1コミット）

1. `Load(buildIdx, stageIdx)` → `Load(buildIdx)`。
   `EngagementEngine.Run(new[]{player}, EnemyCatalog.EngagementColumn, seed: 0, verbose: true)`。
   波ボタン列は「部隊 n/5」の進行表示に置き換える（編成ボタンは残す）。
2. `_roster` を `Openings[b]` から組む: `Id=InstanceId, Hp=持ち越しHp, MaxHp, Attack=現在値,
   BaseAttack, Pattern`。`BuildState` の複製が `Hp = p.MaxHp` で初期化している箇所を
   **Opening の Hp** に変える（ここを見落とすと持ち越しHPが表示に出ない）。
   Summon の逆引き（Main.cs:588）は既存のまま。
3. 台本を Battle ごとに切り替え（`_result.Events` → `Battles[b].Events`、スクラブは Battle 内）。
   Battle 末尾で: 敵全滅 → `ENEMY REINFORCEMENTS` バナー → 一拍 → 次 Battle。
   味方全滅 → `2nd SQUAD` バナー（当面 `[player]` 1部隊なので表示だけ作る）。
   持ち越し駒は終了時と同じ (team, slot) のカードに載るので、左側は自動的に据え置きになる（§0-8）。
4. 上部に会戦全体の進行（部隊 n/5、突破数、会戦の決着）。`_lVerdict`/`_lTurns`/`_lChain` は
   Battle 単位のまま。
5. 左右対面レイアウトは触らない（指示書どおり別作業）。

**受け入れ**: 指示書どおり（リィカの持ち越しが画面で分かる / Godot 側に判定・計算の分岐が
増えていない / BattleCore に Godot 参照が無い）。

## 5. 文書の更新（Phase C または最終コミット）

- README「調整メモ」に「会戦」の項（D1〜D11 の要約と理由、T1〜T4）。
- README「未解決の課題」に指示書 §3.3 の4点 ＋ **§0-3 の Revive/necroBonus 帳簿ずれ**を転記。
- `ENGAGEMENT_PLAN.md` を `design/ENGAGEMENT_PLAN.md` へ移動（現在ルート・未追跡）。
  `design/concept_wave_engagement.md`・本計画書と合わせて design/ を初コミット。
- CLAUDE.md「構成」に `design/`（設計文書。生成物ではないので手で編集する）と
  `GodotApp/` の行を追加（§0-10）。状態異常の追加手順に「`StatusKeys.All` にも足す」を追記。
- AGENTS.md は触らない。

## 6. 作業ルール（指示書 §6 の再掲＋本計画での具体化）

1. A → B → C → D の順。フェーズを跨いで1コミットにしない。
2. 各コミットメッセージに動いた docs/ の行（無ければ「compare 差分ゼロ」）。
3. 数値は一切調整しない。会戦で壊れて見えても記録だけして Phase C の報告に書く。
4. Trait にインスタンスフィールドを足さない / 再入フラグを static に置かない /
   `Def.Pattern` を直接読まない（**Opening の Pattern も `CurrentPattern`**）/
   `LivingMembers` はスナップショット。
5. 長時間ジョブは前景で待ち切る。`layout` は回さない。
6. 検証ハーネスは scratchpad（`EngageProbe`）。repo にテストプロジェクトは足さない。

## 7. チェックリスト（指示書 §7 に本計画の追加分を含めたもの）

- [ ] A: `Materialize` 切り出し、`Run(IReadOnlyList<UnitState>,…)` 追加、旧 `Run` はラッパー。compare/chain/pulse/dump 差分ゼロ
- [ ] B: `Engagement.cs`（`BattleOpening`+`BaseAttack` / `EngagementResult` / `EngagementEngine` 2フラグ分岐）、`StatusKeys.All`、`Trait.OnCarryOver`、`NecroTrait.OnCarryOver`（層-1・帳簿リセット・`lastDeathTurn=0`、理由コメントは §0-5）、`GuardianTrait.OnCarryOver`（`PendingKey=0`）。差分ゼロ・決定性・verbose 不変・リィカ持ち越しの目視確認
- [ ] C: `EngagementColumn`、`engage`（独立積・引き分け列つき）、`docs/engage.md`、CLAUDE.md / CONTRIBUTING.md のコマンド一覧、（任意）`engage2`
- [ ] C: 報告4点＋α を README「検証で分かったこと」へ
- [ ] D: GodotApp が `EngagementResult` を再生。`Openings` から盤面（持ち越しHPで初期化）。バナー・進行表示
- [ ] README「調整メモ」会戦の項 / 「未解決の課題」に §3.3 + Revive/necroBonus 帳簿ずれ
- [ ] `design/` に3文書（指示書の移動を含む）、CLAUDE.md の構成に design/ と GodotApp、状態異常手順に `StatusKeys.All`
