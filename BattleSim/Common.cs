using BattleCore;

// =====================================================================================
// 共有ヘルパ（第142期 段1） —— `Prog.Body` の直下にあった `static` ローカル関数を
// **1行も書き換えずに**そのままここへ移しただけ。
//
// **なぜ動かしたか。** `Program.cs` は 70,065 行が `Prog.Body` 1メソッドに畳まれていて、
// Release のビルドに 234 秒・Roslyn のピークに 22.0 GB かかっていた（第140期／第142期 Q0-1）。
// モードを1本ずつ別クラスへ出していくのに、**共有ヘルパを先に外へ出しておかないと
// モードを動かすたびに全モードが同時に触られる**ので、ここだけを先に切り出す。
//
// **`static` ローカル関数は外側のスコープを1つも捕まえられない**（C# の規則）ので、
// 移動で参照が壊れることが原理的に無い。呼び出し側を1箇所も直さずに済むよう、
// `Program.cs` と切り出したモードは `using static Common;` でこれを取り込む。
//
// **再インデントしていない。** 移す前と1バイトも違わないことを機械で照合するため
// （第142期の受け入れ条件）。
// =====================================================================================

static class Common
{
// ヒヨの複製。**移設は第60期に採用したので、`UnitCatalog.Hiyo` の側が V1（手番）である。**
// 診断のローカルに置くのは**移設前の姿**（V0）と素体2種で、`UnitCatalog` は1バイトも触らない。
//
// **V1 が `docs/balance.md` と一致することが、この診断そのものの検算**
// （採用で既定が動いたので、検算の相手は V0 ではなく V1。第36期 `gullet belly4` と同じ形）。
public static UnitDef FvTurnStartDef() => new()
{
    Id = "hiyo_ts", Name = "火選りのヒヨ（ターン頭）", MaxHp = UnitCatalog.Hiyo.MaxHp,
    Attack = UnitCatalog.Hiyo.Attack, Speed = UnitCatalog.Hiyo.Speed,
    Traits = UnitCatalog.Hiyo.Traits, Pattern = UnitCatalog.Hiyo.Pattern
    // Actions を持たない ＝ ActsOnPattern が偽 ＝ FavorTrait.OnTurnStart が従来どおり発火する
};
// V2 = **移設 + 素体**。`Actions` は持つが特性を持たない——移設は出力を捨てるので、
// 攻撃を振る素体を対照にすると V1 との差に「攻5 が消えたぶん」が混ざる（指示書 §2-2）。
public static UnitDef FvPlainActDef() => new()
{
    Id = "hiyo_plain_act", Name = "素体のヒヨ（手番）", MaxHp = UnitCatalog.Hiyo.MaxHp,
    Attack = UnitCatalog.Hiyo.Attack, Speed = UnitCatalog.Hiyo.Speed,
    Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Hiyo.Pattern,
    Actions = new UnitAction[] { new(ActionKind.Skill, Label: "火のそばを見ている") }
};
// V3 = 移設前の素体（第58期の対照そのもの）。V0 − V3 が第58期の機構の帰属。
public static UnitDef FvPlainDef() => new()
{
    Id = "hiyo_plain", Name = "素体のヒヨ", MaxHp = UnitCatalog.Hiyo.MaxHp,
    Attack = UnitCatalog.Hiyo.Attack, Speed = UnitCatalog.Hiyo.Speed,
    Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Hiyo.Pattern
};
public static int FvCountTrait(Formation f, TraitId id) => f.Occupied().Count(o => o.Def.Traits.Contains(id));
public static Formation FvSwap(Formation f, UnitDef from, UnitDef to)
{
    var g = new Formation();
    foreach ((int slot, UnitDef d) in f.Occupied())
        g[slot] = ReferenceEquals(d, from) ? to : d;
    return g;
}
// 上位3件だけを「名前 量」で返す（受け手の内訳。第56期 whet の作法）。
public static string FvTop(Dictionary<string, double> d)
    => d.Count == 0 ? "—"
     : string.Join(" / ", d.OrderByDescending(kv => kv.Value).Take(3)
                           .Select(kv => $"{FvName(kv.Key)} {kv.Value:0.00}"));
public static string FvName(string id)
    => UnitCatalog.All.Concat(EnemyCatalog.Stages.SelectMany(st => st.Enemy.Occupied().Select(o => o.Def)))
                      .FirstOrDefault(d => d.Id == id)?.Name ?? id;
// V1 = 移設（`[Skill]` の1要素）。供給量は変えず、発火の位置だけをターン頭から手番へ降ろす。
public static UnitDef MsActDef() => new()
{
    Id = "guza_act", Name = "瘴気袋のグザ（手番）", MaxHp = UnitCatalog.Guza.MaxHp,
    Attack = UnitCatalog.Guza.Attack, Speed = UnitCatalog.Guza.Speed,
    Traits = UnitCatalog.Guza.Traits, Pattern = UnitCatalog.Guza.Pattern,
    Actions = new UnitAction[] { new(ActionKind.Skill, Label: "瘴気を撒いている") }
};
// V2 = 周期（`[Skill, Attack]`）。**供給が半分になる。** グザは `CanAct` を上書きしないので
// 両方の要素を通れる（`ActionIndex` が進むことを Phase 0 で確認する）。
public static UnitDef MsCycleDef() => new()
{
    Id = "guza_cyc", Name = "瘴気袋のグザ（周期）", MaxHp = UnitCatalog.Guza.MaxHp,
    Attack = UnitCatalog.Guza.Attack, Speed = UnitCatalog.Guza.Speed,
    Traits = UnitCatalog.Guza.Traits, Pattern = UnitCatalog.Guza.Pattern,
    Actions = new UnitAction[] { new(ActionKind.Skill, Label: "瘴気を撒いている"), new(ActionKind.Attack) }
};
// V3 = 移設 + 素体（同数値・特性なし）。V1 − V3 が機構の帰属。
public static UnitDef MsPlainDef() => new()
{
    Id = "guza_plain", Name = "素体のグザ（手番）", MaxHp = UnitCatalog.Guza.MaxHp,
    Attack = UnitCatalog.Guza.Attack, Speed = UnitCatalog.Guza.Speed,
    Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Guza.Pattern,
    Actions = new UnitAction[] { new(ActionKind.Skill, Label: "息を潜めている") }
};
// 席の入れ替え（Q1）。**駒は1枚も差し替えない**——同じ5枚のまま2つの席だけを交換する。
public static Formation MsSwapSeats(Formation f, UnitDef a, UnitDef b)
{
    int sa = -1, sb = -1;
    foreach ((int slot, UnitDef d) in f.Occupied())
    {
        if (ReferenceEquals(d, a)) sa = slot;
        if (ReferenceEquals(d, b)) sb = slot;
    }
    var g = f.Clone();
    if (sa >= 0 && sb >= 0) { g[sa] = b; g[sb] = a; }
    return g;
}
public static bool MsHas(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));
public static string Describe(UnitDef?[] slots)
{
    // 編成枠だけ。SlotsOfRow は召喚枠(5-8)まで返すので、5要素の slots を添字越えで落とす。
    string Row(Row r) => string.Join("/", FormationRules.PlayableSlotsOfRow(r)
        .Select(i => slots[i]?.Name ?? "空"));
    return $"前[{Row(BattleCore.Row.Front)}] 中[{Row(BattleCore.Row.Mid)}] 後[{Row(BattleCore.Row.Back)}]";
}
public static IEnumerable<List<T>> Combinations<T>(IReadOnlyList<T> source, int k)
{
    var idx = new int[k];
    for (int i = 0; i < k; i++) idx[i] = i;
    while (true)
    {
        yield return idx.Select(i => source[i]).ToList();
        int pos = k - 1;
        while (pos >= 0 && idx[pos] == source.Count - k + pos) pos--;
        if (pos < 0) yield break;
        idx[pos]++;
        for (int i = pos + 1; i < k; i++) idx[i] = idx[i - 1] + 1;
    }
}
public static IEnumerable<UnitDef?[]> SlotPermutations(List<UnitDef> members)
{
    int blanks = FormationRules.PlayableSlotCount - members.Count;

    foreach (var order in Permute(members))
        foreach (var empty in Combinations(Enumerable.Range(0, FormationRules.PlayableSlotCount).ToList(), blanks))
        {
            var skip = empty.ToHashSet();
            var slots = new UnitDef?[FormationRules.PlayableSlotCount];
            int m = 0;
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                slots[i] = skip.Contains(i) ? null : order[m++];
            yield return slots;
        }
}
public static IEnumerable<List<T>> Permute<T>(List<T> items)
{
    if (items.Count <= 1) { yield return new List<T>(items); yield break; }
    for (int i = 0; i < items.Count; i++)
    {
        var rest = new List<T>(items);
        T head = rest[i];
        rest.RemoveAt(i);
        foreach (var p in Permute(rest)) { p.Insert(0, head); yield return p; }
    }
}
// 代表編成の定義は **BattleCore/Presets.cs** へ移した（第97期）。
// 表示側（GodotApp）から同じ一覧を引くため——BattleSim は実行ファイルなので参照できず、
// 写しを持つと必ずずれる（第94期の手写しの表で 29 件）。
// **機械的な移動で、行名・順序・中身は1文字も変えていない**（docs/ 10ファイルが差分 0）。
public static (string Name, Formation F)[] CompareBuilds() => Presets.Compare;
// 第92期の交差帯。**CompareBuilds() とは完全に別の入口**で、生成物も docs/crossing.md と分けてある。
// 定義は Presets.Cross（上と同じ理由で移した）。
public static (string Name, Formation F)[] CrossBuilds() => Presets.Cross;
// メンバーを編成スロット 0..4 へ重複なく割り当てる全順列を、
// **召喚枠(5-8)は含めない**——プレイヤーが置けない席に駒を置く配置を数えることになる。
// 割り当てタプルの辞書式昇順で列挙する（各深さでスロットを昇順に試すため）。
// layout モードの決定性（同点タイブレーク＝列挙順の若い方）はこの順序に依存している。
public static IEnumerable<int[]> SlotAssignments(int memberCount)
{
    var assign = new int[memberCount];
    var used = new bool[FormationRules.PlayableSlotCount];
    return Rec(0);

    IEnumerable<int[]> Rec(int depth)
    {
        if (depth == memberCount) { yield return (int[])assign.Clone(); yield break; }
        for (int slot = 0; slot < FormationRules.PlayableSlotCount; slot++)
        {
            if (used[slot]) continue;
            used[slot] = true;
            assign[depth] = slot;
            foreach (int[] a in Rec(depth + 1)) yield return a;
            used[slot] = false;
        }
    }
}
public static bool SameFormation(Formation a, Formation b)
{
    for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
        if (!ReferenceEquals(a[i], b[i])) return false;
    return true;
}
public static string LayoutRow(string rank, Formation f, int[] wins, int seeds)
{
    static string N(UnitDef? d) => d?.Name ?? "−";
    double avg = wins.Sum() * 100.0 / (wins.Length * seeds);
    string cells = string.Concat(wins.Select(w => $" {w * 100.0 / seeds:F1}% |"));
    return $"| {rank} | {N(f[0])}/{N(f[1])} | {N(f[2])} | {N(f[3])}/{N(f[4])} | {avg:F1}% |{cells}";
}
// 1編成 × 1波の単独戦を seed 0..seeds-1 で回し、勝った試行だけの残存を平均する。
// Formation 版の Run ではなく Materialize + UnitState 版を使うのは、戦闘後の残HPを
// 読むため（BattleResult は生存数しか持たず、残HPは持ち越し側の量なので UnitState にある）。
// 残HP割合の分母は編成の定義上の総最大HP（engage の入場戦力と同じ判断）。
// 決着ターン数も勝った試行だけで平均する（chain の `決着T` と同じ判断。負けた試行は
// 打ち切り30ターンに張り付くので混ぜると意味が壊れる）。第6期 aim が媒介変数として使う
// ——代金の表そのものには出さないので cost / gradient の出力は変わらない。
public static (double WinRate, double AvgAlive, double AvgHpPct, int Wins, double AvgTurns) MeasureCost(
    Formation f, Formation enemy, int seeds)
{
    int defTotal = f.Occupied().Sum(x => x.Def.MaxHp);
    int wins = 0;
    double aliveSum = 0, hpPctSum = 0;
    long turnSum = 0;
    for (int seed = 0; seed < seeds; seed++)
    {
        List<UnitState> mine = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        List<UnitState> foes = BattleEngine.Materialize(enemy, BattleContext.EnemyTeam);
        BattleResult r = BattleEngine.Run(mine, foes, seed, verbose: false);
        if (!r.PlayerWon) continue;
        wins++;
        aliveSum += mine.Count(u => u.IsAlive);
        hpPctSum += (double)mine.Where(u => u.IsAlive).Sum(u => u.Hp) / defTotal;
        turnSum += r.Turns;
    }
    return (wins * 100.0 / seeds,
            wins == 0 ? 0 : aliveSum / wins,
            wins == 0 ? 0 : hpPctSum / wins, wins,
            wins == 0 ? 0 : (double)turnSum / wins);
}
// 範囲持ちの判定（gradient と aim が共有する。第5期の +3.1pt と直接比べるには
// 区分が同一である必要があるので、片方だけで定義しない）。
// Def.Pattern が薙ぎ/全体の駒を1体でも含むかという静的な代理指標。ホタ・リィカのように
// 状況で薙ぎ化する駒は数えない——発火が戦況依存で定義からは判定できない。
public static bool HasAoe(Formation f)
    => f.Occupied().Any(x => x.Def.Pattern is AttackPattern.Sweep or AttackPattern.All);
// 範囲持ちの「枚数」。HasAoe の二値区分は薙ぎ1枚でも範囲側に入れてしまうので、
// 向きが出た候補について枚数で単調に下がるかを見る（第6期 §2-4）。
public static int AoeCount(Formation f)
    => f.Occupied().Count(x => x.Def.Pattern is AttackPattern.Sweep or AttackPattern.All);
// 1波の代金（勝った試行の残HP% → 代金 = 100% − 残HP%）と、その向き（単体のみ − 範囲持ち）。
// flip の候補まとめが出しているものと同じ計算で、bridge が列の合計代金を出すために使う
// （第8期 Phase V）。勝率 0% の編成は代金が定義できないので両群とも集計から外す
// ——外した編成が偏ると打ち切りバイアスが乗るので、使う側は勝率 0% の数も見ること。
public static (double Mean, double Split) WaveCost((string Name, Formation F)[] targets, Formation wave, int seeds)
{
    var live = new List<(bool Aoe, double Cost)>();
    foreach (var (_, f) in targets)
    {
        var m = MeasureCost(f, wave, seeds);
        if (m.Wins > 0) live.Add((HasAoe(f), (1 - m.AvgHpPct) * 100));
    }
    if (live.Count == 0) return (double.NaN, double.NaN);
    var byGroup = live.GroupBy(x => x.Aoe).ToDictionary(g => g.Key, g => g.Average(x => x.Cost));
    double aoe = byGroup.TryGetValue(true, out double a) ? a : double.NaN;
    double single = byGroup.TryGetValue(false, out double b) ? b : double.NaN;
    return (live.Average(x => x.Cost), single - aoe);
}
// 測定台（第9期 §0）。**合計代金 113% が、結果が敏感になる唯一の帯**——136% で測ると
// 全編成が突破 0% に潰れて何も見えない（第6〜8期の結論）。中身は第8期 bridge の
// 反転列(低) と同一（H2a 裸5 / 2b 騎士混成 / 巡礼5）で、bill（代金の分解）と
// bridge（自傷率での群分け）が同じ台の上で測るために1箇所へ寄せてある。
// bridge 側は列の定義を自分で持ったまま、この関数と一致することを検算する
// （列の定義を bridge から取り上げると第8期の出力との突き合わせが読めなくなる）。
public static Formation[] BenchColumn113() => new[]
{
    Formation.Build(front1: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare, center: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare, back3: EnemyCatalog.ZealotBare),
    Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman),
    Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front3: EnemyCatalog.ZealotPilgrim, center: EnemyCatalog.ZealotPilgrim, back1: EnemyCatalog.ZealotPilgrim, back3: EnemyCatalog.ZealotPilgrim),
};
// チャージ台（第10期 Phase AB-0）。**測定台 113% には全体持ちも貫き持ちも1体もいない**
// （裸5 / 新兵・騎士・戦斧兵 / 巡礼5）。第9期までは敵の攻撃型が測定の交絡になるので
// わざと外してあったが、第10期はその2種にチャージを付ける期なので、あの台の上で測ると
// チャージ化の前後で数字が1つも動かない。
//
// そこで測定台の骨格（第1波 裸5 / 第3波 巡礼者・合計代金 113% 帯）を保ったまま、
// 貫きを1枚（第2波の戦斧兵→狙撃手）、全体を1枚（第3波の巡礼者1体→詠唱兵）だけ入れ替えた列を
// 別に作る。**入れ替えであって追加ではない**ので、ステージ設計の「貫き1枚まで／全体1枚まで」
// （UnitCatalog.cs の第三波・第四波のコメント）を跨がない。
//
// 入れ替えで代金が上がった（巡礼5のまま詠唱兵を入れると合計 128.7%）ので、第3波の体数を
// 6→4 に削って 116.6% に戻してある。**攻撃値は触らない**（第8期 Phase V の作法。攻撃を
// 振ると「安さの効果」と「一撃圏を跨いだ効果」が混ざる）。実測 116.6% は 113% 帯の
// 上端だが、突破率(1) = 39.1%・同値塊 3 と、測定台 113%（25.9% / 同値塊 8）より
// 分解能が高い。チャージは火力を塊にするぶん突破率を下げる方向に効くので、
// 前が 39% なのは帯の中へ降りてくる余地としてちょうどよい。
//
// **この列で「向き」（単体−範囲）は測れない**（-1.9pt しかなく、bridge 自身の注記の
// -4pt 基準を下回る）。第6〜8期の軸ではなく時間軸を測るための台なので、それでよい。
//
// BenchColumn113 は触らない。あちらを据え置くことで、第9期の bill / bridge の数字が
// そのまま比較対象として残り、かつ「チャージ化で動いてはいけない列」が検出器になる。
public static Formation[] ChargeBench() => new[]
{
    Formation.Build(front1: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare, center: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare, back3: EnemyCatalog.ZealotBare),
    // 2b 騎士混成 の戦斧兵（薙ぎ）を狙撃手（貫き）に。スロットはそのまま中衛。
    Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Archer),
    // 巡礼者を詠唱兵（全体）入りに。既存の第四波と同じくレーン1の最深部（後2）に置く。
    // 体数 4 は合計代金を 113% 帯へ戻すための刻み（上のコメント参照）。
    Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front3: EnemyCatalog.ZealotPilgrim, center: EnemyCatalog.ZealotPilgrim, back1: EnemyCatalog.Chanter),
};
// 代金の分解（第9期 Phase X）。味方1部隊で列を1回走らせ、失った HP を
//   失ったHP = 敵由来 + 自傷分 − 回復 + 残差
// に割る。返す値はすべて**定義上の総最大HP に対する割合（%）の seed 平均**。
//
// 勝敗で試行を絞らない（cost は勝った試行だけを見るが、あれは「勝ったのにいくら
// 残ったか」の物差し。ここで見たいのは払った HP の内訳なので、負けた試行——自傷型が
// いちばん払っている場面——を落とすと分解が偏る）。
//
// tally は Def.Id で引くので、味方の Def.Id だけを拾う（胞子のような湧いた駒は
// 定義上の総最大HP に入っていないので、分子からも外れるのが正しい）。
// 敵と Def.Id が衝突していると敵の被弾が混ざるが、それは呼び出し側が検算する。
public static (double Lost, double Enemy, double Ally, double Heal, double Residual, double SelfHarmRate,
        double[] AllyByBattle, double[] EnemyByBattle, int[] Reached, int WonFirst, double FirstWinCost)
    MeasureBill(Formation f, IReadOnlyList<Formation> column, int seeds)
{
    int defTotal = f.Occupied().Sum(x => x.Def.MaxHp);
    var ids = f.Occupied().Select(x => x.Def.Id).ToHashSet();
    int n = column.Count;

    double lost = 0, enemy = 0, ally = 0, heal = 0;
    var allyB = new double[n];
    var enemyB = new double[n];
    var reached = new int[n];
    int wonFirst = 0;
    double firstWinCost = 0;

    for (int seed = 0; seed < seeds; seed++)
    {
        EngagementResult r = EngagementEngine.Run(new[] { f }, column, seed, verbose: false);

        // 失った HP は会戦を終えた時点の残 HP から取る（PlayerExits の最後）。
        // 入場側だけだと最終戦の後が読めない。
        lost += defTotal - r.PlayerExits[^1].HpSum;

        for (int b = 0; b < r.Battles.Count; b++)
        {
            double e = 0, a = 0, h = 0;
            foreach ((string id, UnitTally t) in r.Battles[b].TallyByUnit)
            {
                if (!ids.Contains(id)) continue;
                e += t.DamageTaken - t.TakenFromAlly;
                a += t.TakenFromAlly;
                h += t.Healed;
            }
            enemy += e; ally += a; heal += h;
            if (b < n) { reached[b]++; allyB[b] += a; enemyB[b] += e; }
        }

        // 第1戦に勝った試行だけの第1戦の代金。cost（単独戦・勝った試行だけ）と
        // 突き合わせるための値で、seed は揃わないので一致ではなく近似で読む。
        if (r.Battles[0].PlayerWon)
        {
            wonFirst++;
            firstWinCost += (defTotal - r.PlayerExits[0].HpSum) * 100.0 / defTotal;
        }
    }

    double Pct(double v) => v * 100.0 / (seeds * (double)defTotal);
    double lostPct = Pct(lost), enemyPct = Pct(enemy), allyPct = Pct(ally), healPct = Pct(heal);
    return (lostPct, enemyPct, allyPct, healPct,
            lostPct - (enemyPct + allyPct - healPct),
            enemy + ally == 0 ? 0 : ally / (enemy + ally),
            Enumerable.Range(0, n).Select(b => reached[b] == 0 ? 0 : allyB[b] * 100.0 / (reached[b] * (double)defTotal)).ToArray(),
            Enumerable.Range(0, n).Select(b => reached[b] == 0 ? 0 : enemyB[b] * 100.0 / (reached[b] * (double)defTotal)).ToArray(),
            reached, wonFirst, wonFirst == 0 ? 0 : firstWinCost / wonFirst);
}
// 編成の動的特徴量（第12期 Phase CA。第13期 Phase DA で与ダメ・撃破の出どころを差し替え）。
// 味方1部隊で列を1回走らせ、突破度と UnitTally の集計を返す。
// **新しい tally フィールドは足していない**（第12期 §3-2 / 第13期 §3-1）。
//
// **与ダメと撃破は受け手側＝敵の tally から取る。** TickStatuses は
// `ApplyDamage(u, poison, null)` と source を渡さずに呼ぶので、毒・燃焼の削りは
// 出どころの駒の `DamageToEnemy` にも `Kills` にも載らない。味方側から合計すると
// 毒軸の編成の出力が構造的に過小に出る（第12期で見つけた穴）。どの経路で削っても
// 敵の `DamageTaken` には必ず載るので、敵側から数えれば帰属を持たない削りも拾える。
//
//     与ダメ = 敵全員の DamageTaken − 敵の TakenFromAlly
//     撃破   = 敵の死亡数（誰が仕留めたかを問わない）
//
// `TakenFromAlly` を引くのは敵同士の巻き込みを編成の手柄にしないため。**引いた量は
// 呼び出し側に返して報告する**（0 なら敵側に巻き込みが無いことの確認になる）。
//
// **どちらも振り切った量・回数を数える**（過剰殺傷を含む）。HP は 0 で止まるが tally は
// 超過分も数える（第9期 bill の残差分析で確認済み）。味方側の `DamageToEnemy` も
// `ApplyDamage` の同じ行で同じ `amount` を足しているので、**過剰分の扱いは新旧で変わらない**
// ——`与ダメ効率 = 与ダメ ÷ 撃破数` はオーバーキルの指標なので、過剰分が入っているほうが正しい。
//
// 却下した案: 毒の出どころを `UnitState` / `StatusKeys` に持たせて駒単位の帰属を復元する。
// 「部隊で協力して撒いて拡散して濃くする」がコンセプトなので駒単位の帰属は粒度が細かすぎるうえ、
// `BattleCore` に触ることになる（第13期 §2「やらないこと」）。編成単位で足りる。
//
// 味方側の値（旧定義）も同時に返す。第12期の数字と**同じ seed・同じ実行の中で**
// 並べられるようにするため（別の実行から引いてくると、動いたのが定義のせいか実行のせいかが
// 決まらない）。
//
// tally は Def.Id で引く。味方は編成の Def.Id、敵は列の Def.Id で拾う——胞子のような
// 湧いた駒はどちらの集合にも無いので、味方側の集計からは自然に外れる。ただし
// **胞子が敵に通した削りは敵の `DamageTaken` に載るので、受け手側の与ダメには入る。**
// 編成が盤面に出した出力の総量としてはそのほうが正しい（新旧で意味が変わる点）。
// 味方と敵の Def.Id 衝突は呼び出し側が検算する——power はそれを実際に数えて出す。
//
// 分母は seed 数ではなく**部隊戦の数**。会戦は深く抜いた編成ほど Battle が増えるので、
// seed 数で割ると「長く戦った」ぶんだけ値が膨らみ、突破度と機械的に相関してしまう
// （地力の第一近似を探す診断でそれをやると、測りたいものの写しが特徴量側に入る）。
//
// 突破度は seed ごとの生値も返す。第13期 Phase DB の半割（seed を2つに分けて同じ台を
// 2回測る）が、同じ計測を2度走らせずに済むようにするため。
//
// 分母そのもの（`部隊戦数 ÷ 試行`）も返す。味方1部隊では **部隊戦数 = 突破数 + 1**
// （全抜き時だけ = 突破数）なので、`/戦` の量はすべて「目的変数 + 1」で割っている。
// 第14期 Phase EA の同語反復の判定は、この分母が実際にどれだけ突破度を運んでいるかを
// 測って根拠にする（言葉で言い張らずに数字で出す）。
public static (double Degree, double[] PerSeed, double[] Dynamics, double[] Legacy,
        long FoeTakenFromAlly, int DeathGaps, double BattlesPerSeed)
    MeasurePower(Formation f, IReadOnlyList<Formation> column, int seeds)
{
    var ids = f.Occupied().Select(x => x.Def.Id).ToHashSet();
    var foeIds = column.SelectMany(s => s.Occupied()).Select(x => x.Def.Id).ToHashSet();
    var perSeed = new double[seeds];
    long battles = 0;
    long dmgOut = 0, dmgIn = 0, fromAlly = 0, kills = 0, acts = 0, heal = 0;
    long foeTaken = 0, foeFromAlly = 0, foeDeaths = 0;
    int deathGaps = 0;

    for (int seed = 0; seed < seeds; seed++)
    {
        EngagementResult r = EngagementEngine.Run(new[] { f }, column, seed, verbose: false);
        perSeed[seed] = BreakthroughDegree(r, column.Count);
        for (int i = 0; i < r.Battles.Count; i++)
        {
            BattleResult b = r.Battles[i];
            battles++;
            long deathsHere = 0;
            foreach ((string id, UnitTally t) in b.TallyByUnit)
            {
                if (ids.Contains(id))
                {
                    dmgOut += t.DamageToEnemy;
                    dmgIn += t.DamageTaken;
                    fromAlly += t.TakenFromAlly;
                    kills += t.Kills;
                    acts += t.Interventions;
                    heal += t.Healed;
                }
                else if (foeIds.Contains(id))
                {
                    foeTaken += t.DamageTaken;
                    foeFromAlly += t.TakenFromAlly;
                    foeDeaths += t.Deaths;
                    deathsHere += t.Deaths;
                }
            }
            // 勝った部隊戦では敵は全滅している。死亡数が投入した敵駒の数と合わなければ、
            // **`DamageTaken` を経由せずに死ぬ経路がある**ことになる（第13期 §6 の停止条件）。
            // 味方1部隊なので負けた時点で会戦が終わり、勝った戦の敵部隊は必ず新品
            // （敵の持ち越しは起きない）——期待値は部隊の定義上の駒数でよい。
            if (b.PlayerWon && deathsHere != column[r.Pairings[i].EnemySquad].Occupied().Count())
                deathGaps++;
        }
    }

    double Per(long v) => battles == 0 ? 0 : (double)v / battles;
    long foeDmg = foeTaken - foeFromAlly;
    return (perSeed.Average(), perSeed, new[]
    {
        Per(foeDmg), Per(dmgIn), Per(foeDeaths), Per(acts), Per(heal),
        dmgIn == 0 ? 0 : (double)fromAlly / dmgIn,
        // 撃破 0 の編成では与ダメ効率が定義できない。0 で埋めると「無駄が無い」側に
        // 化けて相関を汚すので NaN を返し、相関の側で点ごと落とす。
        foeDeaths == 0 ? double.NaN : (double)foeDmg / foeDeaths,
    }, new[]
    {
        // 旧定義（味方側）。対比表だけが読む。並びは 与ダメ/戦・撃破/戦・与ダメ効率。
        Per(dmgOut), Per(kills),
        kills == 0 ? double.NaN : (double)dmgOut / kills,
    }, foeFromAlly, deathGaps, (double)battles / seeds);
}
// 単発戦1波ぶんを seed 0..seeds-1 で回し、勝敗・生存率・動的特徴量をまとめて返す（第15期 Phase FA）。
//
// MeasurePower（会戦）との違いは**分母**。単発では部隊戦が必ず1回なので、動的特徴量の分母
// 「戦」は seed 数そのものになる——第14期の分母経路（味方1部隊では 部隊戦数 = 突破数 + 1）は
// ここには存在しない。**分母が定数なので `/戦` は「平均を取る」以上の意味を持たない。**
//
// 与ダメ・撃破・与ダメ効率は**受け手側（敵の tally）から取る**（第13期 Phase DA と同じ理由。
// TickStatuses は ApplyDamage(u, poison, null) と source を渡さないので、毒・燃焼の削りは
// 出どころの駒の DamageToEnemy にも Kills にも載らない）。`干渉/戦` だけは味方側のまま
// ——毒は出どころを持たないので受け手側に対応物が無い。
//
// 生存率の分母は**編成の定義上の駒数**。r.PlayerSurvivors は胞子のように湧いた駒も数えるので
// 1.0 を超えることがある（chain の `残存` と同じ定義。あちらも同じ性質を持つ）。
//
// 残HP（代金）はここでは測らない。MeasureCost が測る量をこの関数でも定義すると、
// gradient / aim / flip との再現の検算がこの関数の正しさにも依存してしまう。
public static (double[] Win, double[] SurvRate, double[] Dynamics, long FoeTakenFromAlly)
    MeasureWave(Formation f, Formation enemy, int seeds)
{
    var ids = f.Occupied().Select(x => x.Def.Id).ToHashSet();
    var foeIds = enemy.Occupied().Select(x => x.Def.Id).ToHashSet();
    int party = f.Occupied().Count();

    var win = new double[seeds];
    var surv = new double[seeds];
    long dmgIn = 0, fromAlly = 0, acts = 0, heal = 0;
    long foeTaken = 0, foeFromAlly = 0, foeDeaths = 0;

    for (int seed = 0; seed < seeds; seed++)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
        win[seed] = r.PlayerWon ? 1 : 0;
        surv[seed] = (double)r.PlayerSurvivors / party;
        foreach ((string id, UnitTally t) in r.TallyByUnit)
        {
            if (ids.Contains(id))
            {
                dmgIn += t.DamageTaken;
                fromAlly += t.TakenFromAlly;
                acts += t.Interventions;
                heal += t.Healed;
            }
            else if (foeIds.Contains(id))
            {
                foeTaken += t.DamageTaken;
                foeFromAlly += t.TakenFromAlly;
                foeDeaths += t.Deaths;
            }
        }
    }

    double Per(long v) => (double)v / seeds;
    long foeDmg = foeTaken - foeFromAlly;
    return (win, surv, new[]
    {
        Per(foeDmg), Per(dmgIn), Per(foeDeaths), Per(acts), Per(heal),
        dmgIn == 0 ? 0 : (double)fromAlly / dmgIn,
        // 撃破 0 の編成では与ダメ効率が定義できない。0 で埋めると「無駄が無い」側に化けて
        // 相関を汚すので NaN を返し、相関の側で点ごと落とす（MeasurePower と同じ判断）。
        foeDeaths == 0 ? double.NaN : (double)foeDmg / foeDeaths,
    }, foeFromAlly);
}
// 1事例（編成 × 波）を verbose で回して、解剖に要る材料をまとめて返す（第16期 Phase GA）。
//
// **BattleCore は触らない。** 要るものは全部 `BattleResult.Events` から読める——
// Attack / Damage / Status / StatusSnapshot / Death / Move / Summon が時間順に並んでいるので、
// 「誰が誰を殴ったか」「範囲が何体巻き込んだか」「毒が何段乗ったか」はここで組み直せる。
// **文字列（Log）は解析しない**（`ptrace` は解析しているが、あれは構造化イベントが入る前の道具）。
//
// InstanceId は Deploy の順（味方 → 敵、スロット昇順）で振られるので、同じ順で数えれば
// 陣営が引ける（`replay` の roster と同じ組み立て）。後から湧いた駒（胞子・増援）は
// Summon イベントが Team を持っているので、そこで台帳に足す。
//
// 振りの範囲は「Attack イベントから、同じ手番の同じ actor が出した Damage まで」で切る。
// 反撃・破裂・毒は ActorId が違う（毒は null）ので自然に外れる——**追加のフラグは要らない。**
//
// verbose=true は Events を確保するぶん遅いが、seed 数は `wave` に揃える（200）。
// 別の seed 集合で測ると、§1 の勝率・順位と解剖の数字が別の試行を見ることになる。
public static WaveTrace MeasureTrace(Formation f, Formation enemy, int seeds)
{
    int party = f.Count;
    int foes = enemy.Count;
    int foeHpTotal = enemy.Occupied().Sum(x => x.Def.MaxHp);
    int allyHpTotal = f.Occupied().Sum(x => x.Def.MaxHp);

    int wins = 0, draws = 0, wipes = 0;
    double turnWinSum = 0, turnWinSq = 0, turnLoseSum = 0, aliveWinSum = 0;
    long allySwings = 0, swingHits = 0, swingDmg = 0, primaryDmg = 0, primaryN = 0;
    long directToFoe = 0, dotToFoe = 0, foeDeaths = 0;
    long foeSwings = 0, allyTaken = 0, backTaken = 0, dotToAlly = 0;
    double poisonPeakSum = 0, poisonPeakTurnSum = 0;
    long poisonWasted = 0;
    var allyAlive = new double[WaveTrace.Profile];
    var foeAlive = new double[WaveTrace.Profile];

    for (int seed = 0; seed < seeds; seed++)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true);

        // 陣営とスロットの台帳。スロットは Move で動くので追いかける（後列被弾率が要る）。
        var team = new Dictionary<int, int>();
        var slot = new Dictionary<int, int>();
        int id = 0;
        foreach (var (tm, fm) in new[] { (BattleContext.PlayerTeam, f), (BattleContext.EnemyTeam, enemy) })
            foreach (var (sl, _) in fm.Occupied()) { team[id] = tm; slot[id] = sl; id++; }
        var alive = new HashSet<int>(team.Keys);
        var stack = new Dictionary<int, int>();   // 敵に乗っている毒の段数（直近のスナップショット）

        int curActor = -1, curTurn = -1, curTarget = -1;
        bool curAlly = false, gotPrimary = false;
        int snapTurn = -1, snapSum = 0;
        double peak = 0, peakTurn = 0;
        var profA = new double[WaveTrace.Profile];
        var profF = new double[WaveTrace.Profile];
        int lastTurn = 0;

        void RecordTurn(int t)
        {
            lastTurn = t;
            if (t < 1 || t > WaveTrace.Profile) return;
            profA[t - 1] = alive.Count(x => team[x] == BattleContext.PlayerTeam);
            profF[t - 1] = alive.Count(x => team[x] == BattleContext.EnemyTeam);
        }

        foreach (BattleEvent e in r.Events)
        {
            switch (e.Kind)
            {
                case BattleEventKind.TurnStart:
                    curActor = -1;
                    // 前のターンのスナップショット合計を締める（毒の総段数のピークを取る）
                    if (snapTurn > 0 && snapSum > peak) { peak = snapSum; peakTurn = snapTurn; }
                    snapTurn = e.Turn; snapSum = 0;
                    RecordTurn(e.Turn);
                    break;

                case BattleEventKind.StatusSnapshot:
                    // 敵に乗っている毒だけを見る。**ターン開始の TickStatuses 直後の残量**なので、
                    // そのターン中に積まれたぶんは次のターンの頭まで出ない（BattleEventKind の注記）。
                    if (e.Text == "毒" && e.TargetId is int sid && team.TryGetValue(sid, out int stm)
                        && stm == BattleContext.EnemyTeam)
                    {
                        snapSum += e.Amount;
                        stack[sid] = e.Amount;
                    }
                    break;

                case BattleEventKind.Attack:
                    curActor = e.ActorId ?? -1;
                    curTurn = e.Turn;
                    curTarget = e.TargetId ?? -1;
                    curAlly = curActor >= 0 && team.TryGetValue(curActor, out int atm)
                              && atm == BattleContext.PlayerTeam;
                    gotPrimary = false;
                    if (curAlly) allySwings++;
                    else if (curActor >= 0) foeSwings++;
                    break;

                case BattleEventKind.Damage:
                {
                    int tgt = e.TargetId ?? -1;
                    if (tgt < 0 || !team.TryGetValue(tgt, out int ttm)) break;

                    if (ttm == BattleContext.EnemyTeam)
                    {
                        // 敵が受けた削り。**出どころ無し（毒・燃焼）は Status 側で数える**ので
                        // ここでは足さない（足すと二重計上になる）。敵同士の巻き込みも外す。
                        if (e.ActorId is not null && !e.FriendlyFire) directToFoe += e.Amount;
                    }
                    else
                    {
                        allyTaken += e.Amount;
                        if (slot.TryGetValue(tgt, out int sl) && FormationRules.RowOf(sl) == Row.Back)
                            backTaken += e.Amount;
                    }

                    // 振りへの帰属。**同じ手番の同じ actor** に限るので、反撃（actor が違う）も
                    // 毒（actor が null）も自然に外れる。
                    if (curAlly && e.ActorId == curActor && e.Turn == curTurn
                        && ttm == BattleContext.EnemyTeam && !e.FriendlyFire)
                    {
                        swingHits++;
                        swingDmg += e.Amount;
                        if (!gotPrimary && tgt == curTarget)
                        {
                            primaryDmg += e.Amount; primaryN++; gotPrimary = true;
                        }
                    }
                    break;
                }

                case BattleEventKind.Status:
                    if (e.TargetId is int dtg && team.TryGetValue(dtg, out int dtm))
                    {
                        if (dtm == BattleContext.EnemyTeam) dotToFoe += e.Amount;
                        else dotToAlly += e.Amount;
                    }
                    break;

                case BattleEventKind.Death:
                    if (e.TargetId is int did)
                    {
                        alive.Remove(did);
                        if (team.TryGetValue(did, out int dem) && dem == BattleContext.EnemyTeam)
                        {
                            foeDeaths++;
                            // 乗ったまま死んだ毒。**乗り切る前に落ちた量**の代理指標になる。
                            if (stack.TryGetValue(did, out int left))
                            {
                                poisonWasted += left; stack.Remove(did);
                            }
                        }
                    }
                    break;

                case BattleEventKind.Revive:
                    if (e.TargetId is int rid) alive.Add(rid);
                    break;

                case BattleEventKind.Summon:
                    if (e.TargetId is int nid && e.Team is int ntm)
                    {
                        team[nid] = ntm; slot[nid] = e.Slot; alive.Add(nid);
                    }
                    break;

                case BattleEventKind.Move:
                    if (e.TargetId is int mid) slot[mid] = e.Slot;
                    break;
            }
        }
        if (snapTurn > 0 && snapSum > peak) { peak = snapSum; peakTurn = snapTurn; }

        // 決着より後のターンは「決着時の盤面が続いた」として埋める。空欄にすると
        // 平均の分母が波ごとに変わり、推移の比較ができなくなる。
        for (int t = Math.Max(lastTurn, 1); t < WaveTrace.Profile; t++)
        {
            profA[t] = alive.Count(x => team[x] == BattleContext.PlayerTeam);
            profF[t] = alive.Count(x => team[x] == BattleContext.EnemyTeam);
        }
        for (int t = 0; t < WaveTrace.Profile; t++) { allyAlive[t] += profA[t]; foeAlive[t] += profF[t]; }

        poisonPeakSum += peak;
        poisonPeakTurnSum += peakTurn;

        if (r.PlayerWon)
        {
            wins++;
            turnWinSum += r.Turns; turnWinSq += (double)r.Turns * r.Turns;
            aliveWinSum += (double)r.PlayerSurvivors / party;
        }
        else
        {
            // 負けは2種類ある。**全滅と打ち切り（30T 引き分け）は同じ「敗北」だが中身が違う。**
            // 打ち切りは「削り切れなかった」で、全滅は「削られ切った」。
            turnLoseSum += r.Turns;
            if (r.PlayerSurvivors > 0) draws++; else wipes++;
        }
    }

    int losses = seeds - wins;
    double mW = wins == 0 ? 0 : turnWinSum / wins;
    return new WaveTrace
    {
        Seeds = seeds,
        Party = party,
        Foes = foes,
        FoeHpTotal = foeHpTotal,
        AllyHpTotal = allyHpTotal,
        WinRate = wins * 100.0 / seeds,
        DrawRate = draws * 100.0 / seeds,
        WipeRate = wipes * 100.0 / seeds,
        TurnsWin = mW,
        TurnsWinSd = wins == 0 ? double.NaN : Math.Sqrt(Math.Max(0, turnWinSq / wins - mW * mW)),
        TurnsLose = losses == 0 ? double.NaN : turnLoseSum / losses,
        AliveOnWin = wins == 0 ? double.NaN : aliveWinSum / wins,
        AllySwings = (double)allySwings / seeds,
        HitsPerSwing = allySwings == 0 ? double.NaN : (double)swingHits / allySwings,
        PrimaryDmg = primaryN == 0 ? double.NaN : (double)primaryDmg / primaryN,
        SwingShare = directToFoe == 0 ? double.NaN : swingDmg * 100.0 / directToFoe,
        DirectToFoe = (double)directToFoe / seeds,
        DotToFoe = (double)dotToFoe / seeds,
        FoeDeaths = (double)foeDeaths / seeds,
        SwingsPerKill = foeDeaths == 0 ? double.NaN : (double)allySwings / foeDeaths,
        ShaveRatio = (directToFoe + dotToFoe) / (double)foeHpTotal / seeds,
        FoeSwings = (double)foeSwings / seeds,
        AllyTaken = (double)allyTaken / seeds,
        BackShare = allyTaken == 0 ? double.NaN : backTaken * 100.0 / allyTaken,
        DotToAlly = (double)dotToAlly / seeds,
        PoisonPeak = poisonPeakSum / seeds,
        PoisonPeakTurn = poisonPeakSum <= 0 ? double.NaN : poisonPeakTurnSum / seeds,
        PoisonWasted = (double)poisonWasted / seeds,
        AllyAlive = allyAlive.Select(x => x / seeds).ToArray(),
        FoeAlive = foeAlive.Select(x => x / seeds).ToArray(),
    };
}
// 参照台1つ × 編成1つの出力を測る（第17期 Phase HA）。
//
// **目的変数から独立に編成の出力量を取るための関数。** 波ごとの勝率と同じ戦闘から与ダメを
// 取ると第14期の同語反復（分子経路）にそのまま当たるので、固定の的で1回だけ測る。
//
// **BattleCore は触らない。** 材料は `BattleResult.Events`（verbose: true）で、
// 振りへの帰属の切り方は `MeasureTrace`（第16期 Phase GA）と同一
// ——「Attack イベントから、同じ手番の同じ actor が出した Damage まで」。
// 反撃（actor が違う）も毒（actor が null）も追加のフラグ無しで外れる。
//
// **打点は `Damage` イベントから取る。`Status` からは取らない。** `Status` が持つのは
// 適用**前**の量なので、破片で吸われたぶん・非致死で丸めたぶんが実際の削りとずれる
// （`dissect` の `毒燃/戦` は `Status` から取っていて、そこだけ流儀が違う）。
// 一致は呼び出し側が敵の tally（第13期の受け手側測定）と突き合わせて検算する。
//
// seed ごとの値を配列で返すのは、**半割（測定の信頼性の上限）を同じ計測から取り出す**ため。
// 2回走らせると半割の値そのものに実行間のばらつきが乗る（`bench` 第13期と同じ作法）。
public static OutputTrace MeasureOutput(Formation f, Formation bench, int seeds)
{
    var foeIds = bench.Occupied().Select(x => x.Def.Id).ToHashSet();

    var damage = new double[seeds];
    var turns = new double[seeds];
    var cum = new double[seeds][];
    double swing = 0, direct = 0, dot = 0, tally = 0, over = 0;
    long foeFromAlly = 0, kills = 0;
    int shortRun = 0, allyWipe = 0, foeWipe = 0;

    for (int seed = 0; seed < seeds; seed++)
    {
        BattleResult r = BattleEngine.Run(f, bench, seed, verbose: true);

        // 陣営の台帳。InstanceId は Deploy の順（味方 → 敵、スロット昇順）で振られる
        // （`MeasureTrace` / `replay` の roster と同じ組み立て）。
        var team = new Dictionary<int, int>();
        // 敵の最大HP（InstanceId 別）。**オーバーキルを数えるために要る**——`ApplyDamage` は
        // 残HPで切り詰めないので `Damage` イベントの `Amount` は素の量で、超過分は
        // 「1体に通した合計 − 最大HP」でしか取れない（`HpAfter` は 0 止まりなので使えない）。
        var foeMaxHp = new Dictionary<int, int>();
        int id = 0;
        foreach (var (tm, fm) in new[] { (BattleContext.PlayerTeam, f), (BattleContext.EnemyTeam, bench) })
            foreach (var (_, def) in fm.Occupied())
            {
                team[id] = tm;
                if (tm == BattleContext.EnemyTeam) foeMaxHp[id] = def.MaxHp;
                id++;
            }

        // 敵1体ごとの被弾合計。参照台は回復も破片も持たないので、合計が最大HPを超えたぶんが
        // そのままオーバーキルになる。
        var dealtTo = new Dictionary<int, double>();

        var perTurn = new double[OutputTrace.Ramp];
        double total = 0;
        int curActor = -1, curTurn = -1;
        bool curAlly = false;

        foreach (BattleEvent e in r.Events)
        {
            switch (e.Kind)
            {
                case BattleEventKind.TurnStart:
                    curActor = -1;
                    break;

                case BattleEventKind.Summon:
                    // 胞子のように湧いた駒。台帳に足さないと、その駒の打点が陣営不明で落ちる。
                    if (e.TargetId is int nid && e.Team is int ntm) team[nid] = ntm;
                    break;

                case BattleEventKind.Attack:
                    curActor = e.ActorId ?? -1;
                    curTurn = e.Turn;
                    curAlly = curActor >= 0 && team.TryGetValue(curActor, out int atm)
                              && atm == BattleContext.PlayerTeam;
                    break;

                case BattleEventKind.Damage:
                {
                    int tgt = e.TargetId ?? -1;
                    if (tgt < 0 || !team.TryGetValue(tgt, out int ttm)
                        || ttm != BattleContext.EnemyTeam) break;
                    // 敵同士の巻き込みは外す（受け手側測定の前提。第13期 §3-1）。
                    if (e.ActorId is int src
                        && (e.FriendlyFire
                            || (team.TryGetValue(src, out int stm) && stm == BattleContext.EnemyTeam)))
                        break;

                    total += e.Amount;
                    dealtTo.TryGetValue(tgt, out double had);
                    dealtTo[tgt] = had + e.Amount;
                    if (e.Turn >= 1 && e.Turn <= OutputTrace.Ramp) perTurn[e.Turn - 1] += e.Amount;
                    if (e.ActorId is null)
                    {
                        dot += e.Amount;   // 毒・燃焼。ApplyDamage が source を渡さないので actor が無い
                    }
                    else
                    {
                        direct += e.Amount;
                        // 同じ手番の同じ actor だけを「振り」に帰属させる。反撃は actor が違う。
                        if (curAlly && e.ActorId == curActor && e.Turn == curTurn) swing += e.Amount;
                    }
                    break;
                }
            }
        }

        // 敵の tally からも同じ量を出す（第13期の受け手側測定）。呼び出し側が突き合わせる。
        // 撃破も受け手側から数える——毒・燃焼の削りは `ApplyDamage(u, poison, null)` で
        // source を持たないので、味方側の `Kills` には載らない（第13期 Phase DA）。
        foreach ((string tid, UnitTally t) in r.TallyByUnit)
            if (foeIds.Contains(tid))
            {
                tally += t.DamageTaken - t.TakenFromAlly; foeFromAlly += t.TakenFromAlly;
                kills += t.Deaths;
            }

        // オーバーキル（第18期 Phase IA）。**1体ごとに数える。**
        // 合計打点 − 敵の総HP では、生き残った駒のぶんまで引いてしまう。
        foreach ((int fid, int mhp) in foeMaxHp)
            if (dealtTo.TryGetValue(fid, out double got) && got > mhp) over += got - mhp;

        damage[seed] = total;
        turns[seed] = r.Turns;
        var c = new double[OutputTrace.Ramp];
        double acc = 0;
        for (int t = 0; t < OutputTrace.Ramp; t++) { acc += perTurn[t]; c[t] = acc; }
        cum[seed] = c;

        if (r.Turns < OutputTrace.Ramp) shortRun++;
        if (r.PlayerWon) foeWipe++;
        else if (r.PlayerSurvivors == 0) allyWipe++;
    }

    return new OutputTrace
    {
        Seeds = seeds,
        Damage = damage,
        Turns = turns,
        Cum = cum,
        Swing = swing,
        Direct = direct,
        Dot = dot,
        Short = shortRun,
        AllyWipe = allyWipe,
        FoeWipe = foeWipe,
        TallyDamage = tally,
        FoeFromAlly = foeFromAlly,
        Kills = kills,
        Overkill = over,
    };
}
// Actions だけを剥がした複製。charge 診断が「溜めない同じ敵」を同じ実行の中で
// 作るために使う（git を戻して測り直すと、前後の数字が別の実行から来ることになる）。
// Id も含めて他は全て同じ。前後を別々の会戦で回すので tally は混ざらない。
public static UnitDef StripActions(UnitDef d) => d.Actions is null ? d : new UnitDef
{
    Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
    Traits = d.Traits, Pattern = d.Pattern,
    PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
};
// Actions だけを差し替えた複製。timing 診断が変種をローカルに組むために使う
// （UnitCatalog は N0 / M0 のまま。gradient / aim が候補波をローカルで組んだのと同じ）。
// Id も含めて他は全て同じ。変種ごとに別の会戦を回すので tally は混ざらない。
public static UnitDef WithActions(UnitDef d, IReadOnlyList<UnitAction> acts) => new UnitDef
{
    Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
    Traits = d.Traits, Pattern = d.Pattern, Actions = acts,
    PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
};
// 母標準偏差。cost の代金のばらつきと同じ物差し。
public static double Sd(IReadOnlyList<double> v)
{
    if (v.Count == 0) return 0;
    double m = v.Average();
    return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / v.Count);
}
// 突破度 = 突破した部隊数 + 最後に負けた部隊戦での削り割合（0.0 〜 列長。第8期 Phase U）。
// 期待突破数は整数の平均なので、「あと一歩まで削った」と「初戦で溶けた」が同じ 2.00 に潰れる。
// 部分点を足して連続量にすると、代金の向きが結果に届いているかを順位で見られるようになる。
// 全抜き（列を抜き切った）試行は最終戦にも勝っていて LastBattleAttrition が 1.0 になるので、
// そのまま足すと列長を超える。列長ちょうどに揃える。
public static double BreakthroughDegree(EngagementResult r, int columnLength)
    => r.EnemySquadsCleared >= columnLength
        ? columnLength
        : r.EnemySquadsCleared + r.LastBattleAttrition;
// 降順の平均順位（1 が最良）。同値は平均順位にする——編成の期待突破数は 0.00 や 3.00 で
// 並ぶことがあり、入力順で順位を割ると順位相関が入力順の産物になる（第7期 Phase S）。
public static double[] AverageRanksDesc(double[] v)
{
    int n = v.Length;
    var idx = Enumerable.Range(0, n).OrderByDescending(i => v[i]).ToArray();
    var r = new double[n];
    for (int k = 0; k < n;)
    {
        int j = k;
        while (j + 1 < n && v[idx[j + 1]] == v[idx[k]]) j++;
        double avg = (k + j) / 2.0 + 1;   // 0 始まりの位置の平均 → 1 始まりの順位
        for (int m = k; m <= j; m++) r[idx[m]] = avg;
        k = j + 1;
    }
    return r;
}
// ピアソン相関。順位列に当てるとスピアマンの順位相関になる（同順位は平均順位で処理済み）。
public static double Pearson(double[] a, double[] b)
{
    // 標本が無い／1点しかないときは NaN。呼び出し側はどこも NaN を「測れなかった」として
    // 扱っているので、ここで落とさない。**天井と床の波しか残らないと実際に空になる**
    // （寄与する波が2本未満だと、波どうしのペアが1つも作れない）。
    if (a.Length < 2 || b.Length < 2) return double.NaN;
    double ma = a.Average(), mb = b.Average();
    double cov = 0, va = 0, vb = 0;
    for (int i = 0; i < a.Length; i++)
    {
        double da = a[i] - ma, db = b[i] - mb;
        cov += da * db; va += da * da; vb += db * db;
    }
    return va == 0 || vb == 0 ? double.NaN : cov / Math.Sqrt(va * vb);
}
// 特徴量と目的変数の相関（第12期 Phase CB）。ピアソンとスピアマンを両方返す。
// 目的変数（突破度）は連続量なのでピアソンが素直だが、第8期以降の測定はすべて
// スピアマンの順位相関で報告されているので、突き合わせられるよう両方出す（§4-3）。
//
// 片方が NaN の点は落とす。撃破 0 の編成では与ダメ効率が定義できず、0 で埋めると
// 「無駄が無い」側に化けて相関を汚す。落とした結果は N で分かるようにする。
public static (double R, double Rho, int N) Correlate(double[] x, double[] y)
{
    var px = new List<double>();
    var py = new List<double>();
    for (int i = 0; i < x.Length; i++)
        if (!double.IsNaN(x[i]) && !double.IsNaN(y[i])) { px.Add(x[i]); py.Add(y[i]); }
    if (px.Count < 3) return (double.NaN, double.NaN, px.Count);
    double[] ax = px.ToArray(), ay = py.ToArray();
    return (Pearson(ax, ay), Pearson(AverageRanksDesc(ax), AverageRanksDesc(ay)), px.Count);
}
// 単回帰の予測値（第12期 Phase CB）。残差 = 実測 − これ。
// x に NaN が混じる点は予測できないので NaN を返す（残差の順位付けから落ちる）。
public static double[] LinearFit(double[] x, double[] y)
{
    var idx = Enumerable.Range(0, x.Length).Where(i => !double.IsNaN(x[i]) && !double.IsNaN(y[i])).ToArray();
    if (idx.Length < 2) return x.Select(_ => double.NaN).ToArray();
    double mx = idx.Average(i => x[i]), my = idx.Average(i => y[i]);
    double sxy = idx.Sum(i => (x[i] - mx) * (y[i] - my));
    double sxx = idx.Sum(i => (x[i] - mx) * (x[i] - mx));
    double slope = sxx == 0 ? 0 : sxy / sxx;
    return x.Select(v => double.IsNaN(v) ? double.NaN : my + slope * (v - mx)).ToArray();
}
// 2変数の重相関の二乗（第12期 Phase CB）。r1 / r2 は各説明変数と目的変数の相関、
// r12 は説明変数同士の相関。**2変数で止めるのは n = 31 だから**——3変数以上は
// 過学習して「説明できた」という数字だけが残る（§4-1）。
//
// 説明変数同士がほぼ同一（|r12| ≒ 1）だと分母が 0 に落ちて発散するので、
// その場合は単変量の良い方を返す（2本目に情報が無いので、それが正しい答えでもある）。
public static double R2Two(double r1, double r2, double r12)
{
    if (double.IsNaN(r1) || double.IsNaN(r2) || double.IsNaN(r12)) return double.NaN;
    double denom = 1 - r12 * r12;
    if (denom < 1e-9) return Math.Max(r1 * r1, r2 * r2);
    return Math.Min(1.0, (r1 * r1 + r2 * r2 - 2 * r1 * r2 * r12) / denom);
}
// 代金の表（編成 × 波）と、波ごとのばらつきの表を吐く。計測値は呼び出し側にも返す
// （gradient が候補選定の集計に使う）。
public static (double WinRate, double AvgAlive, double AvgHpPct, int Wins, double AvgTurns)[,] EmitCostTables(
    (string Name, Formation F)[] targets,
    IReadOnlyList<(string Name, Formation Enemy)> waves, int seeds)
{
    Console.WriteLine("### 代金の表（勝った試行だけの集計）");
    Console.WriteLine();
    Console.WriteLine("セルは `勝率 / 残体数 残HP%`。勝率 0% の波は残存が定義できないので `—`。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(waves.Select(w => $" {w.Name} |")));
    Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "---|")));

    var cells = new (double WinRate, double AvgAlive, double AvgHpPct, int Wins, double AvgTurns)[targets.Length, waves.Count];
    for (int t = 0; t < targets.Length; t++)
    {
        var row = new List<string>();
        for (int w = 0; w < waves.Count; w++)
        {
            cells[t, w] = MeasureCost(targets[t].F, waves[w].Enemy, seeds);
            row.Add(cells[t, w].Wins == 0
                ? $" {cells[t, w].WinRate:F0}% / — |"
                : $" {cells[t, w].WinRate:F0}% / 残{cells[t, w].AvgAlive:F1} {cells[t, w].AvgHpPct * 100:F0}% |");
        }
        Console.WriteLine($"| {targets[t].Name} |" + string.Concat(row));
        Console.Out.Flush();
    }

    // ばらつきの表。波間の差は平均で、編成間の差は標準偏差で見る（第5期 §2-1）。
    // 代金は勝率 > 0% の編成だけで集計する（負けた試行しか無い編成は代金が定義できない）。
    Console.WriteLine();
    Console.WriteLine("### 代金のばらつき");
    Console.WriteLine();
    Console.WriteLine("| 波 | 勝率の中央値 | 代金の平均 | 代金の最小（編成名） | 代金の最大（編成名） | 代金の標準偏差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    for (int w = 0; w < waves.Count; w++)
    {
        var rates = Enumerable.Range(0, targets.Length)
            .Select(t => cells[t, w].WinRate).OrderBy(x => x).ToArray();
        double median = rates.Length % 2 == 1
            ? rates[rates.Length / 2]
            : (rates[rates.Length / 2 - 1] + rates[rates.Length / 2]) / 2;

        var costs = Enumerable.Range(0, targets.Length)
            .Where(t => cells[t, w].Wins > 0)
            .Select(t => (targets[t].Name, Cost: (1 - cells[t, w].AvgHpPct) * 100))
            .ToList();
        if (costs.Count == 0)
        {
            Console.WriteLine($"| {waves[w].Name} | {median:F1}% | — | — | — | — |");
            continue;
        }
        double mean = costs.Average(c => c.Cost);
        double sd = Math.Sqrt(costs.Average(c => (c.Cost - mean) * (c.Cost - mean)));
        var lo = costs.OrderBy(c => c.Cost).First();
        var hi = costs.OrderByDescending(c => c.Cost).First();
        Console.WriteLine($"| {waves[w].Name} | {median:F1}% | {mean:F1}% | {lo.Cost:F1}%（{lo.Name}） "
            + $"| {hi.Cost:F1}%（{hi.Name}） | {sd:F1}pt |");
    }
    Console.WriteLine();
    Console.WriteLine("代金の集計対象（勝率 > 0% の編成数）: "
        + string.Join(" / ", Enumerable.Range(0, waves.Count).Select(w =>
            $"{waves[w].Name} {Enumerable.Range(0, targets.Length).Count(t => cells[t, w].Wins > 0)}/{targets.Length}")));
    return cells;
}
// 候補波の集約先（第15期 Phase FA）。**ここが唯一の1箇所**で、`wave`（第15期）と
// `dissect`（第16期）の両方がこの関数を呼ぶ。モードのローカルに置いたままだと
// 2つ目の診断がコピーを持つことになり、**集め直した意味がその時点で消える。**
//
// --- 候補波の集約（1箇所） ---
//
// 出どころは6つ。**どれも定義を1文字も変えずに写している**——値が動いたら集め方が
// 間違っている証拠なので、下の「再現の検算」で 代金・向き・ターン数 を突き合わせる
// （§5-7 の停止条件）。
//
//   既存5波   EnemyCatalog.Stages（compare / chain / pulse が測っている盤面そのもの）
//   第5期     gradient の w1 / w2 / w3
//   第6期     aim の H1 系・H2 系・M1（1a/1b/1c は gradient と同一なので重複させない）
//   第7期     flip の R0〜R12（3a/3b/3c は gradient と同一なので重複させない）
//   第8期     bridge の 荷駄5 / 巡礼5（第3波の代金だけを振った点。攻10 は R11 と同じ）
//   第10期    ChargeBench の第2波・第3波（第1波は H2a と同一なので重複させない）
//
// **現物が無くて入れられなかった候補が2つある。** 第8期に測った「攻5 版」（90/攻5）と
// 「板金従卒5」（60/攻7）は `UnitCatalog` に `UnitDef` が残っていない（攻5 は刻みとして
// 測っただけ、板金従卒は「却下した案」として文章にだけ残っている）。BattleCore を触らない
// 作業なので**新しい敵は作らない**——集めるのは現物のある波だけにして、無い2つは出力に明記する。
public static (string Tag, string Era, string Name, Formation Enemy)[] WaveCatalog()
    => new (string, string, string, Formation)[]
    {
        ("S1",  "既存",   "第一波 / 物見の兵",      EnemyCatalog.Stages[0].Enemy),
        ("S2",  "既存",   "第二波 / 巡礼騎士団",    EnemyCatalog.Stages[1].Enemy),
        ("S3",  "既存",   "第三波 / 討伐隊本隊",    EnemyCatalog.Stages[2].Enemy),
        ("S4",  "既存",   "第四波 / 城塞守備隊",    EnemyCatalog.Stages[3].Enemy),
        ("S5",  "既存",   "第五波 / 異端審問団",    EnemyCatalog.Stages[4].Enemy),

        // 第5期 gradient の w1 / w2 / w3。
        ("G1a", "第5期",  "1a 農兵5",
            Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("G1b", "第5期",  "1b 農兵5",
            Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("G1c", "第5期",  "1c 農兵5+斧",
            Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Axeman, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("G2a", "第5期",  "2a 新兵3+斧",
            Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Recruit, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman)),
        ("G2b", "第5期",  "2b 騎士混成",
            Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman)),
        ("G2c", "第5期",  "2c 騎士2+狙撃",
            Formation.Build(front1: EnemyCatalog.Knight, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Archer)),
        ("G3a", "第5期",  "3a 精鋭3",
            Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Warden)),
        ("G3b", "第5期",  "3b 精鋭+司祭長",
            Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Chaplain)),
        ("G3c", "第5期",  "3c 精鋭2",
            Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion)),

        // 第6期 aim。H1 系（高HP低攻）・H2 系（低HP高攻）・M1（中間点）。
        ("H1a", "第6期",  "H1a 人足5",
            Formation.Build(front1: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer, center: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer, back3: EnemyCatalog.Laborer)),
        ("H1b", "第6期",  "H1b 人足5",
            Formation.Build(front1: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer, center: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer, back3: EnemyCatalog.Laborer)),
        ("H1c", "第6期",  "H1c 人足4",
            Formation.Build(front1: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer, center: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer)),
        ("H2a", "第6期",  "H2a 裸5(16)",
            Formation.Build(front1: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare, center: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare, back3: EnemyCatalog.ZealotBare)),
        ("H2b", "第6期",  "H2b 革5(24)",
            Formation.Build(front1: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather, center: EnemyCatalog.ZealotLeather, back1: EnemyCatalog.ZealotLeather, back3: EnemyCatalog.ZealotLeather)),
        ("H2c", "第6期",  "H2c 鎖5(32)",
            Formation.Build(front1: EnemyCatalog.ZealotMail, front3: EnemyCatalog.ZealotMail, center: EnemyCatalog.ZealotMail, back1: EnemyCatalog.ZealotMail, back3: EnemyCatalog.ZealotMail)),
        ("H2d", "第6期",  "H2d 革4(24)",
            Formation.Build(front1: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather, center: EnemyCatalog.ZealotLeather, back1: EnemyCatalog.ZealotLeather)),
        ("M1",  "第6期",  "M1 傭兵5",
            Formation.Build(front1: EnemyCatalog.Drifter, front3: EnemyCatalog.Drifter, center: EnemyCatalog.Drifter, back1: EnemyCatalog.Drifter, back3: EnemyCatalog.Drifter)),

        // 第7期 flip。R0〜R6・R8〜R12 は 体数 × 個体HP の格子、R7 は処刑なしの対照。
        ("R0",  "第7期",  "R0 鎖4(32)",
            Formation.Build(front1: EnemyCatalog.ZealotMail, front3: EnemyCatalog.ZealotMail, center: EnemyCatalog.ZealotMail, back1: EnemyCatalog.ZealotMail)),
        ("R1",  "第7期",  "R1 板金4(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate, center: EnemyCatalog.ZealotPlate, back1: EnemyCatalog.ZealotPlate)),
        ("R2",  "第7期",  "R2 板金3(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate, center: EnemyCatalog.ZealotPlate)),
        ("R3",  "第7期",  "R3 板金2(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate)),
        ("R4",  "第7期",  "R4 重甲4(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat, back1: EnemyCatalog.ZealotGreat)),
        ("R5",  "第7期",  "R5 重甲3(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat)),
        ("R6",  "第7期",  "R6 重甲2(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat)),
        ("R7",  "第7期",  "R7 精鋭3・処刑なし（3a と数値は同じ）",
            Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.ChampionPlain, center: EnemyCatalog.Warden)),
        ("R8",  "第7期",  "R8 重甲5(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat, back1: EnemyCatalog.ZealotGreat, back3: EnemyCatalog.ZealotGreat)),
        ("R9",  "第7期",  "R9 重甲5(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat, back1: EnemyCatalog.ZealotGreat, back3: EnemyCatalog.ZealotGreat)),
        ("R10", "第7期",  "R10 板金5(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate, center: EnemyCatalog.ZealotPlate, back1: EnemyCatalog.ZealotPlate, back3: EnemyCatalog.ZealotPlate)),
        ("R11", "第7期",  "R11 従卒5(90/攻10)",
            Formation.Build(front1: EnemyCatalog.ZealotSquire, front3: EnemyCatalog.ZealotSquire, center: EnemyCatalog.ZealotSquire, back1: EnemyCatalog.ZealotSquire, back3: EnemyCatalog.ZealotSquire)),
        ("R12", "第7期",  "R12 従卒5(90/攻10)",
            Formation.Build(front1: EnemyCatalog.ZealotSquire, front3: EnemyCatalog.ZealotSquire, center: EnemyCatalog.ZealotSquire, back1: EnemyCatalog.ZealotSquire, back3: EnemyCatalog.ZealotSquire)),

        // 第8期 bridge。R11 と体数・個体HP は同じで攻撃だけが違う（代金を振った軸）。
        ("P6",  "第8期",  "荷駄5(90/攻7)",
            Formation.Build(front1: EnemyCatalog.ZealotPorter, front3: EnemyCatalog.ZealotPorter, center: EnemyCatalog.ZealotPorter, back1: EnemyCatalog.ZealotPorter, back3: EnemyCatalog.ZealotPorter)),
        ("Q6",  "第8期",  "巡礼5(90/攻4)",
            Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front3: EnemyCatalog.ZealotPilgrim, center: EnemyCatalog.ZealotPilgrim, back1: EnemyCatalog.ZealotPilgrim, back3: EnemyCatalog.ZealotPilgrim)),

        // 第10期 ChargeBench の第2波・第3波。**候補波の中で貫き・全体を持つのはここだけ**
        // （第6期以降の候補は「敵の攻撃型は測定の交絡になる」として単体で揃えてある）。
        ("C2",  "第10期", "チャージ台2波 新兵2+騎士+狙撃(貫き)", ChargeBench()[1]),
        ("C3",  "第10期", "チャージ台3波 巡礼3+詠唱兵(全体)",   ChargeBench()[2]),
    };
}
