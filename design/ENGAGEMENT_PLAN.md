# 会戦（Engagement）実装計画 — Claude Code 向け指示書

対象リポジトリ: `kimikolab/VillainProto`（基準コミット `d223a8e`）
関連文書: `design/concept_wave_engagement.md`（ゲームコンセプトメモ。このファイルと一緒に repo に置く）

> **作業を始める前に必ず読むもの**: `CLAUDE.md` → `CONTRIBUTING.md` → `README.md` の「調整メモ」「検証で分かったこと」「未解決の課題」。
> この指示書はそれらを上書きしない。矛盾したら CLAUDE.md / CONTRIBUTING.md が正で、矛盾している旨を報告して止まること。

---

## 0. この作業の目的

現在の BattleSim は「第一波〜第五波」を**独立した 5 戦**として測っている（`EnemyCatalog.Stages` の各ステージに対し、毎回 `BattleEngine.Run` が `Formation` から新品の `UnitState` を作る）。波の難度は敵の強さだけで決まり、「波が易しすぎる」問題を敵の強化以外で解決する手段がない。

この作業では、**部隊戦（Battle）を連結し、生存駒の状態を持ち越す「会戦（Engagement）」**を BattleCore に導入する。狙いは三つ。

1. 難度の源泉を「敵の強さ」から「消耗」に移す（1 部隊で何部隊抜けるかが指標になる）
2. 「勝てない編成」にも価値を与える（敵第 1 部隊を半壊させる特攻隊）
3. リィカ（墓守）の「仲間の死を背負って第 2・第 3 部隊を単独で屠る」体験を、システムから自然に発生させる

**やらないこと**（この作業のスコープ外。手を出さない）:

- マップ・戦線・兵站・士気
- 会戦の途中で投入部隊を選ぶ UI
- 既存の特性の数値調整（会戦の結果を見てから、別の作業として行う）
- 敵の新規追加
- `AttackPattern` の追加（4 つまで、の絶対ルール）

---

## 1. 用語

| 語 | 意味 | 寿命の例 |
|---|---|---|
| Turn | 既存のターン | 痺れ |
| Battle / 部隊戦 | 1 部隊 vs 1 部隊。既存の `BattleEngine.Run` 1 回 | 毒・燃焼・標的・一時バフ |
| Engagement / 会戦 | 同じ地点で、どちらかの部隊列が尽きるまで Battle を連結したもの | 墓守の層・蘇生回数・最大 HP の損耗 |
| Squad / 部隊 | `Formation` 1 つ | — |
| 部隊列 | `IReadOnlyList<Formation>`。投入順は事前固定 | — |

Stage / Map スコープは**作らない**。

---

## 2. 現状の構造（コードで確認済みの事実）

実装前提として参照する箇所。ここに書いていない挙動を前提にしないこと。

- `BattleEngine.Run(Formation player, Formation enemy, int seed, bool verbose)` — `BattleEngine.cs:932`。内部で `Deploy` が `Formation` から `UnitState` を生成し `ctx.Add` する。`Run` は seed 決定的な純関数。
- `UnitState`（`Models.cs:54`）は `Hp / MaxHp / Slot / AtkBonus / Counters / HasFallenBack / InstanceId` を持つ。`InstanceId` は `BattleContext.Add` が振る戦闘内連番。
- `AtkBonus` は **恒久（墓守の層）と一時（バフ・デバフ）が同じ 1 つの int に混ざっている**。層由来の分は `Counters["necroBonus"]` に「適用済み量」が別途記録されている（`NecroTrait.SetStack`）。
- `NecroTrait`（`Traits.cs:409`）は **既にターン単位の減衰を持つ**（前ターンに味方の死がなければ 1 層落ちる）。コンセプトメモの「自然減衰」は、境界で 1 層落とす処理を足すだけでほぼ成立する。
- `NecroTrait.EnemyGain`（敵撃破ごとに `AtkBonus += 3`）は層とは別で、`necroBonus` に記録されない。
- `ReviverTrait`（`Traits.cs:553`）は `Counters["charges"]`（蘇生側）と `Counters["sewn"]`（蘇生された側）で回数を管理し、蘇生側の `MaxHp` を半減する。**`UnitState` を持ち越せば、これらは何もしなくても会戦スコープになる。**
- `ctx.Revive` は `AtkBonus = 0` にリセットする（`BattleEngine.cs:809`）。
- 状態異常は `StatusKeys`（`Poison / Marked / Stun / Burn / IdleTurn / Armor …`）のカウンタ。`TickStatuses` がターン頭に処理する。
- `BattleResult` は `Events`（verbose 時のみ）を持ち、GodotApp はこれを再生するだけ。**GodotApp は初期盤面を `Formation` から `def.MaxHp` で組み立てている**（`Main.cs:503-512`）ので、HP を引き継いだ盤面は現状では表示できない。
- `EnemyCatalog.Stages` は `record Stage(string Name, Formation Enemy)` の 5 要素。
- BattleSim の `compare` は 編成 × Stage の勝率表を `docs/balance.md` に吐く。

---

## 3. 設計判断

### 3.1 確定（この作業で採用する）

| # | 判断 | 理由 |
|---|---|---|
| D1 | 生存した `UnitState` をそのまま次の Battle に渡す。死んだ駒は持ち越さない | 「同じ駒が続けて戦う」を最も素直に表す。蘇生回数・MaxHp 損耗・`sewn` が自動で会戦スコープになる |
| D2 | 部隊戦の境界で **エンジンが一律に消すもの**: `StatusKeys` の全カウンタ、`AtkBonus`（→ 0） | 状態異常は Battle スコープ（コンセプトメモ §10）。`AtkBonus` は恒久と一時が混ざっていて分離できないので一律 0 にし、恒久分は特性側が再適用する（D3） |
| D3 | `Trait.OnCarryOver(UnitState self)` を追加する。D2 の後にエンジンが呼び、特性は**持ち越したい状態だけ**ここで再構成する。既定は何もしない | 「何が会戦を跨ぐか」を特性ごとに 1 箇所で宣言させる。エンジンにホワイトリストを持たせない（`Counters` のキーは特性の私有物） |
| D4 | `NecroTrait.OnCarryOver`: 層を 1 減らし、`necroBonus` を 0 にしてから `SetStack` で再適用する。`EnemyGain` 由来の分は持ち越さない | コンセプトメモ §12 の減衰。既存のターン減衰と同じ「連鎖が途切れたら罰する」思想。`EnemyGain` を持ち越すと会戦中に単調増加する量ができる（README「積み上げにはコストを」に反する）|
| D5 | `Slot` は維持。再配置しない。敵の新規部隊は常に初期配置 | 再配置を許すと `layout` / `reseat` の評価が会戦文脈で意味を変える。まず維持で測る |
| D6 | `HasFallenBack` は維持 | 「下がった実績」であって状態異常ではない |
| D7 | `MaxHp` の変化（継ぎ接ぎの半減）は維持 | D1 の帰結。会戦スコープのコストとして意図通り |
| D8 | 勝った側の部隊はそのまま次の相手と戦う。負けた側は次の部隊を投入。どちらかの部隊列が尽きたら終了 | コンセプトメモ §4 |
| D9 | 各 Battle の seed は `seed` から決定的に派生する（`unchecked(seed * 1000003 + battleIndex)` 等）。`Random` を跨いで共有しない | `Run` の純関数性を保つ。同じ引数なら同じ会戦 |
| D10 | 会戦のルールは **BattleCore に閉じる**。GodotApp は `EngagementResult` を再生するだけ | CLAUDE.md の絶対ルール |
| D11 | 会戦の敵部隊列の第 1 号は、**既存の `EnemyCatalog.Stages` 5 つをそのまま順に並べたもの** | 「5 波を独立に戦う」と「5 波を持ち越して戦う」の差が、そのまま今回の変更の効き目になる。新しい敵を作らない |

### 3.2 仮置き（実装はするが、数値・規則は会戦の計測結果を見て見直す）

| # | 仮置き | 見直しの条件 |
|---|---|---|
| T1 | `MaxTurns` 到達（引き分け）は「味方部隊が退く」扱い。敵部隊は現状維持で次の味方部隊と戦う | 引き分けが会戦の結果を大きく左右する編成が出たら再検討 |
| T2 | 墓守の境界減衰は 1 層 | `engage` で「第 1 部隊で育てて全抜き」が常に最適になったら、減衰でなく**上限**を検討する（README「係数の崖」参照） |
| T3 | 部隊列の最大長は 5 | テンポの問題。GodotApp で見てから |
| T4 | HP は現在値をそのまま持ち越す（回復なし） | 回復系（ノノ）の価値が跳ねたら、境界回復ではなく「回復のコスト」側で対処する |

### 3.3 未決定（この作業では触らない。README「未解決の課題」に転記する）

- 味方部隊の投入順を途中で選べるか
- 敵部隊の情報を事前にどこまで見せるか
- 戦闘後の負傷 / 復元の扱い（会戦を跨ぐ寿命）
- 死亡の永久ロスト（コンセプト上は「復元可能」前提）

---

## 4. 実装フェーズ

**1 フェーズ = 1 コミット（または PR）。** 各フェーズの受け入れ条件を満たしてから次へ進む。フェーズの途中で構造的な問題（例: `AtkBonus` の一律リセットで壊れる特性が見つかった）を見つけたら、**実装を進めずに報告して止まる**。

### Phase A — `BattleEngine.Run` の分割（挙動変更ゼロ）

**やること**

1. `BattleEngine.Run(IReadOnlyList<UnitState> player, IReadOnlyList<UnitState> enemy, int seed, bool verbose)` を追加する。渡された `UnitState` を `ctx.Add` して戦う。`Deploy` は `Formation → List<UnitState>` を返す `public static` ヘルパ（`Materialize(Formation, teamId)` 等）に切り出す。
2. 既存の `Run(Formation, Formation, …)` は `Materialize` してから新 `Run` を呼ぶラッパーにする。**シグネチャは変えない**（BattleSim / GodotApp / PrototypeApp の呼び出しを壊さない）。
3. `ctx.Add` が `InstanceId` を振り直すこと、`Add` の順序（味方 → 敵、スロット昇順）が変わらないことを確認する。GodotApp は「Deploy の順で数えれば一致する」前提で `_roster` を組んでいる（`Main.cs:502`）。

**受け入れ条件**

- `dotnet run --project BattleSim -c Release 0 compare > docs/balance.md` の差分が **ゼロ**
- `chain` / `pulse` の出力も差分ゼロ（`Run` の分割はイベントも集計も変えない）
- `dump` の差分ゼロ

### Phase B — `Engagement` の追加（純粋な追加）

**新規型（`BattleCore/Engagement.cs`）**

```csharp
/// <summary>部隊戦の開始時点の盤面。持ち越した HP を再生側が知るための写し。verbose 時のみ。</summary>
public sealed record BattleOpening(int InstanceId, int TeamId, string UnitId, string Name,
                                   int Slot, int Hp, int MaxHp, int Attack, AttackPattern Pattern);

public sealed class EngagementResult
{
    public required bool PlayerWon { get; init; }
    public required IReadOnlyList<BattleResult> Battles { get; init; }
    /// <summary>各 Battle の開始盤面。Battles と同じ長さ。verbose=false なら空。</summary>
    public required IReadOnlyList<IReadOnlyList<BattleOpening>> Openings { get; init; }
    /// <summary>各 Battle で戦った (味方部隊番号, 敵部隊番号)。Battles と同じ長さ。</summary>
    public required IReadOnlyList<(int PlayerSquad, int EnemySquad)> Pairings { get; init; }
    public required int EnemySquadsCleared { get; init; }
    public required int PlayerSquadsLost { get; init; }
    /// <summary>最初の Battle で敵第1部隊の総 MaxHp のうち削った割合（0..1）。特攻隊の価値を測る。</summary>
    public required double FirstBattleAttrition { get; init; }
}

public static class EngagementEngine
{
    public const int MaxBattles = 10;
    public static EngagementResult Run(IReadOnlyList<Formation> playerSquads,
                                       IReadOnlyList<Formation> enemySquads,
                                       int seed, bool verbose = true);
}
```

**アルゴリズム**

```
pi = 0, ei = 0
current = Materialize(playerSquads[0]), enemyCur = Materialize(enemySquads[0])
loop (最大 MaxBattles 回):
    r = BattleEngine.Run(current, enemyCur, DeriveSeed(seed, battleIndex), verbose)
    記録: r, Opening（Run の前に写す）, (pi, ei)
    if r.PlayerWon:
        ei++; EnemySquadsCleared++
        if ei == enemySquads.Count → 味方勝利で終了
        enemyCur = Materialize(enemySquads[ei])
        current = CarryOver(current の生存駒)
    else if 敵が生きていて味方全滅、または MaxTurns 到達 (T1):
        pi++; PlayerSquadsLost++
        if pi == playerSquads.Count → 味方敗北で終了
        current = Materialize(playerSquads[pi])
        enemyCur = CarryOver(enemyCur の生存駒)
    else (両軍全滅):
        pi++; ei++; 両方の Cleared/Lost を加算
        どちらかが尽きたら、尽きていない側の勝ち。両方尽きたら味方敗北
```

`CarryOver(units)`:

```
foreach u in units where u.IsAlive:
    foreach key in StatusKeys の全定数: u.Counters.Remove(key)   // Armor も含めて消す（破片は Battle 内の供給に依存する）
    u.AtkBonus = 0
    foreach t in u.Traits: t.OnCarryOver(u)
```

`InstanceId` は次の `Run` の `ctx.Add` で振り直される。**会戦を跨いで駒を同定する手段は `Openings` の `Slot + TeamId`**（D5 で Slot が固定なので一意）。`UnitState` に会戦 ID を足さない。

**`Trait.OnCarryOver` の追加**

- `Trait` に `public virtual void OnCarryOver(UnitState self) { }` を足す。`BattleContext` を渡さない（ログもイベントも無い場所）。
- `NecroTrait` に D4 の実装。
- 他の特性は既定（何もしない）のまま。`Counters` に残った特性私有のカウンタ（`charges` / `sewn` / `lastDeathTurn` 等）は**消さない**。`lastDeathTurn` は次の Battle の Turn 1 で `>= Turn-1` を満たさないので、Turn 1 の頭に自然に減衰が 1 回入る。これは D4 の境界減衰と**二重**になる。→ **`OnCarryOver` で `lastDeathTurn` を 0 にし、減衰は境界の 1 回だけにする**（そうしないと実質 2 層落ちる）。コメントに理由を残す。

**`BattleResult` への追加**

- なし。`Opening` は `EngagementResult` 側に持つ。`BattleResult` を触らないことで Phase B が純粋な追加であることを保証する。

**受け入れ条件**

- `compare` / `chain` / `pulse` / `dump` の差分 **ゼロ**（会戦は誰もまだ呼んでいない）
- `EngagementEngine.Run` を同じ引数で 2 回呼ぶと `Battles.Count` / `PlayerWon` / 各 `Battles[i].Turns` が一致する（決定性）
- 敵部隊列 = `Stages` 5 つ、味方 = `CompareBuilds` の「死の連鎖 (リィカ軸)」で `verbose: true` に走らせ、第 2 Battle の `Openings` にリィカが `necro` の層を持ったまま（`Attack` が素の値より高い状態で）載っていることを目で確認する。ログに残すこと

### Phase C — BattleSim `engage` モード

**やること**

1. `EnemyCatalog` に `public static IReadOnlyList<Formation> EngagementColumn => Stages.Select(s => s.Enemy).ToList()` を足す（D11）。
2. `Program.cs` に `engage [絞り込み]` を足す。`reseat` / `ablate` と同じ絞り込み書式。`CompareBuilds` の各編成（味方 1 部隊）を `EngagementColumn` にぶつけ、seed 200 で以下を出す:

| 列 | 意味 |
|---|---|
| 突破率 | 5 部隊すべて抜いた試行の割合 |
| 期待突破数 | `EnemySquadsCleared` の平均 |
| 第1削り | `FirstBattleAttrition` の平均（特攻隊の指標） |
| 突破分布 | 0 / 1 / 2 / 3 / 4 / 5 部隊抜きの試行数（ヒストグラム。**「第 2 部隊で落ちる」と「第 5 部隊で落ちる」を区別する**） |

出力先は `docs/engage.md`。`CLAUDE.md` と `CONTRIBUTING.md` のコマンド一覧・生成物リストに **1 行足す**（`docs/` は手で編集しない、の対象に加える）。

3. `engage2 [絞り込み]`（任意・時間があれば）: 味方 2 部隊。`CompareBuilds` から 2 つを選ぶ組み合わせは多すぎるので、**同じ編成を 2 部隊**（同一編成の複製）だけを測る。「1 部隊で 2.3 抜ける編成」と「2 部隊で 4.6 抜ける編成」の非線形性が見えれば十分。

**受け入れ条件**

- `compare` の差分ゼロ（`engage` は読むだけ）
- `docs/engage.md` が生成され、コミットに含まれる
- 出力に **「独立 5 戦の勝率（balance.md）」と「会戦の期待突破数」が一目で比較できる列** があること。例: 各編成の行に balance.md の 5 波勝率の積（独立なら理論上の全抜き率）を並べる。これが「波が易しすぎる」問題に会戦がどれだけ効いたかの直接の証拠になる

**この段階で報告してほしいこと**（実装ではなく分析。README の「検証で分かったこと」に書く）

- 独立 5 戦では 100% だった編成が、会戦で何部隊目で落ちるか
- リィカ軸が他より会戦に強いか（コンセプト通りか）
- ノノ / 回復持ちの編成が会戦で相対的に浮いていないか（T4）
- 「第 1 削り」だけ高くて突破 0 の編成があるか（特攻隊候補）

### Phase D — GodotApp: 会戦の再生

**前提**: Phase B の `EngagementResult` を Godot が直接参照する（既存の方針通り、JSON を経由しない）。

**やること**

1. `Load(buildIdx, stageIdx)` を `Load(buildIdx)` に変え、`EngagementEngine.Run([player], EnemyCatalog.EngagementColumn, seed: 0, verbose: true)` を呼ぶ。ステージ選択 UI は「部隊 n/5」の進行表示に置き換える。
2. `_roster` を **`Openings[b]` から組む**（`Formation` から `def.MaxHp` で組むのをやめる）。これで持ち越した HP / 攻撃力がそのまま初期盤面になる。`Summon` の `UnitDef` 逆引き（`Main.cs:590`）は既存のまま。
3. 台本を Battle ごとに切り替える。`_result.Events` を `Battles[b].Events` に読み替え、スクラブバーは Battle 内。Battle の末尾で:
   - 敵全滅なら `ENEMY REINFORCEMENTS` バナー → 一拍 → 次の Battle をロード。**左の生存駒は動かさず**、右に新部隊が現れる
   - 味方全滅なら `2nd SQUAD` バナー → 左に新部隊が現れる（味方複数部隊は当面 `[player]` 1 つなので実際には会戦終了。表示だけ作っておく）
4. 上部に会戦全体の進行（`1 / 5` 部隊、突破数、`PlayerWon`）を出す。既存の `_lVerdict` / `_lTurns` / `_lChain` は Battle 単位のまま。
5. 左右対面レイアウト（コンセプトメモ §2）は **この作業では触らない**。現在の縦配置のまま会戦の切り替えだけを入れる。レイアウト変更は別作業（`EnemyLaneOrder` / `PlayerLaneOrder` の要点コメントを読んでから）。

**受け入れ条件**

- 「死の連鎖 (リィカ軸)」をロードし、第 1 Battle でリィカ以外が倒れ、第 2 Battle 開始時にリィカの HP と攻撃力が持ち越されているのが画面で分かる
- Godot 側に戦闘の判定・数値計算が**一切増えていない**（差分レビューで確認。`Events` と `Openings` を読む以外の分岐が無いこと）
- `BattleCore` に Godot の参照が無いこと（変わらないはずだが確認する）

---

## 5. 文書の更新

- `README.md` の「調整メモ」に「会戦」の項を追加。§3.1 の判断と理由を要約し、§3.3 の未決定を「未解決の課題」に転記する
- `design/concept_wave_engagement.md` としてコンセプトメモを置く。この指示書も `design/ENGAGEMENT_PLAN.md` に置く。**`docs/` には置かない**（生成物の場所）
- `CLAUDE.md` の「構成」に `design/` を 1 行足す（「設計文書。生成物ではないので手で編集する」）
- `AGENTS.md` は触らない（参照だけ、の方針）

---

## 6. Claude Code への作業ルール

1. Phase A → B → C → D の順。**フェーズを跨いで 1 コミットにしない**
2. 各コミットメッセージに「動いた docs/ の行」を書く。動いていないなら「compare 差分ゼロ」と書く
3. コード・コメント・ログは日本語。既存ファイルのコメントの文体（理由を書く、却下した案も書く）に揃える
4. `Trait` にインスタンスフィールドを足さない。再入フラグを static に置かない。`Def.Pattern` を直接読まない。`LivingMembers` はスナップショットで使う（CONTRIBUTING.md のチェックリスト）
5. 長時間ジョブは前景で待ち切る。`engage` は `compare` と同程度（seed 200 × 27 編成 × 最大 5 Battle）なので数分。`layout` は今回走らせない（配置は D5 で固定）
6. **数値を調整しない。** 会戦で何かが壊れて見えても、この作業では記録だけして Phase C の報告に書く
7. 以下を見つけたら**実装前に止まって報告する**:
   - `AtkBonus` の一律リセットで意図と違う挙動をする特性（例: 戦闘開始時に `AtkBonus` を積む特性が `OnBattleStart` で二重に積む、等）
   - `StatusKeys` の全消去で壊れる特性（`Armor` を「持ち越せる」前提で設計されたものが無いか）
   - `Revive` が `AtkBonus = 0` にする既存挙動と `OnCarryOver` の整合
   - `MaxTurns` 引き分けが `compare` で実際に起きている編成（T1 の影響範囲）
8. 作業の最後に、§4 Phase C の「報告してほしいこと」4 点に答える形で結果をまとめる。数字と `docs/engage.md` の該当行を引く

---

## 7. 変更一覧（チェックリスト）

- [ ] A: `BattleEngine.Run(IReadOnlyList<UnitState>, …)` 追加、`Materialize` 切り出し、旧 `Run` はラッパー。compare 差分ゼロ
- [ ] B: `Engagement.cs`（`BattleOpening` / `EngagementResult` / `EngagementEngine`）、`Trait.OnCarryOver`、`NecroTrait.OnCarryOver`。compare 差分ゼロ。決定性の確認
- [ ] C: `EnemyCatalog.EngagementColumn`、`engage` モード、`docs/engage.md`、CLAUDE.md / CONTRIBUTING.md のコマンド一覧
- [ ] C: 報告 4 点を README「検証で分かったこと」に追記
- [ ] D: GodotApp が `EngagementResult` を再生。`Openings` から盤面を組む。バナー・進行表示
- [ ] README「調整メモ」に会戦の項、「未解決の課題」に §3.3
- [ ] `design/` に 2 文書、CLAUDE.md の構成に 1 行
