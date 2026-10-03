using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;

// burnhit phase0 —— Q0-5（H0 ＋ 計数の札で、被弾の燃焼が起きうる一撃を数える）と、手数の表（表B の「手数の多い駒」の定義）。
static partial class BurnHitDiag
{
    internal static readonly (string Name, (int W, int S)[] Cells)[] Groups =
    {
        ("九/新兵 400/300", new[] { (BA.MainWave, 1) }),
        ("九/新兵 200/200", new[] { (BA.MainWave, 0) }),
        ("本編 400/300", new[] { (0, 1), (1, 1), (2, 1), (3, 1) }),
        ("本編 200/200", new[] { (0, 0), (1, 0), (2, 0), (3, 0) }),
        ("重い波 400/300", new[] { (FC.WaveHeavy, 1) }),
        ("的・一（8T）", new[] { (FC.WaveTarget1, 0) }),
        ("的・九（8T）", new[] { (FC.WaveTarget9, 0) }),
    };

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第255期 Phase 0 —— H0（＝ 規定）＋ 計数の札で、被弾の燃焼が起きうる一撃を数える（盤面は H0 と同じ）");
        Console.WriteLine();
        var boards = FixedBoards.Concat(RefBoards).ToArray();
        foreach (string bn in boards) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BaseOf(bn, null))}");
        Console.WriteLine();

        // Q0-5: 台ごとの機会
        Console.WriteLine("## Q0-5 —— 被弾の燃焼の機会（H0 の盤面で・/戦）");
        Console.WriteLine();
        Console.WriteLine("機会 ＝「その一撃より前から燃えていた駒が、出どころのある一撃で HP か破片を減らした」（刻み・徴収・中継・呪いの共有・逸らしの受け渡し・放電・澱みの爆発を除く）。");
        Console.WriteLine("見込み ＝ 機会の瞬間の火勢 × 6（H-足す ／ H-分担 で入る被弾の燃焼の名目・軛と脆さの前）。刻み ＝ ターン頭の燃焼の刻みで実際に動いた HP（削った ／ 回復）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 決着T | 刻みの回数 味方 ／ 敵 | 刻みで削った HP 味方 ／ 敵 | 刻みが回復になった HP 味方 | 機会 味方 ／ 敵 | 機会の平均火勢 味方 ／ 敵 | 見込み 味方 ／ 敵 | 機会の多い殴り手（敵が燃えている側・上位4） |");
        Console.WriteLine("|---|---|--:|---|---|--:|---|---|---|---|");
        foreach (string bn in boards)
        {
            var f = Mark(BaseOf(bn, null), Probe.Card);
            foreach (var (gn, cells) in Groups)
            {
                var m = new Agg();
                foreach (var (w, s) in cells) m.Merge(Measure(f, w, ScOf(w, s)));
                string top = string.Join(" ", m.ChanceBy.Where(kv => kv.Key.StartsWith("0:")).OrderByDescending(kv => kv.Value[0]).Take(4)
                    .Select(kv => $"{NameOf(kv.Key)[2..]} {Per(kv.Value[0], m.N)}"));
                Console.WriteLine($"| {bn} | {gn} | {Per(m.Turns, m.N)} | {Per(m.TickFires[0], m.N)} ／ {Per(m.TickFires[1], m.N)} | {Per(m.TickDmg[0], m.N)} ／ {Per(m.TickDmg[1], m.N)} | {Per(m.TickHeal[0], m.N)} | "
                    + $"{Per(m.Chances[0], m.N)} ／ {Per(m.Chances[1], m.N)} | {Per(m.ChanceLv[0], m.Chances[0])} ／ {Per(m.ChanceLv[1], m.Chances[1])} | {Per(6 * m.ChanceLv[0], m.N)} ／ {Per(6 * m.ChanceLv[1], m.N)} | {top} |");
            }
        }
        Console.WriteLine();
        // 盤面が H0 と同じこと（計数の札が盤面を動かさない）
        int same = 0, cells2 = 0;
        foreach (string bn in boards)
            foreach (var (w, s) in new[] { (BA.MainWave, 1), (0, 1), (3, 1), (FC.WaveTarget9, 0) })
            {
                cells2++;
                var a = Measure(BaseOf(bn, null), w, ScOf(w, s)).Sig;
                var b = Measure(Mark(BaseOf(bn, null), Probe.Card), w, ScOf(w, s)).Sig;
                if (a == b) same++;
            }
        Console.WriteLine($"計数の札が盤面を動かさない: {same} / {cells2} セルで台本の指紋が H0 と一致");
        Console.WriteLine();

        HandsTable();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    /// <summary>手数の表: `compare` 61 行 × 第2〜5波 × seed 0..49（H0）で、駒ごとの「敵への命中 ÷ 手番」。上位を表B の「手数の多い駒」とする。</summary>
    internal static List<(string Id, string Name, double PerTurn, long Hits, long Turns)> HandRates()
    {
        var rows = CompareBuilds();
        var hits = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();
        var turns = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();
        Parallel.For(0, rows.Length * 4 * 50, k =>
        {
            int ri = k / 200, w = (k / 50) % 4, seed = k % 50;
            var (r, p, _) = FC.Fight(rows[ri].F, w, BA.Scales[2].Sc, seed, verbose: true);
            var idOf = p.ToDictionary(u => u.InstanceId, u => u.Def.Id);
            var mine = new Dictionary<string, long>();
            foreach (var e in r.Events)
                if (e.Kind == BattleEventKind.Damage && e.ActorId is int a && idOf.TryGetValue(a, out var id) && e.TargetId is int t && !idOf.ContainsKey(t) && !e.Relayed)
                    mine[id] = mine.GetValueOrDefault(id) + 1;
            foreach (var u in p.Select(u => u.Def.Id).Distinct())
            {
                hits.AddOrUpdate(u, mine.GetValueOrDefault(u), (_, v) => v + mine.GetValueOrDefault(u));
                long tt = r.TallyByUnit.TryGetValue(u, out var ut) ? ut.TurnsTaken : 0;
                turns.AddOrUpdate(u, tt, (_, v) => v + tt);
            }
        });
        return hits.Keys.Select(id => (id, UnitCatalog.ById(id).Name, turns[id] == 0 ? 0.0 : (double)hits[id] / turns[id], hits[id], turns[id]))
            .OrderByDescending(x => x.Item3).ToList();
    }
    internal static readonly string[] MoveAxis = { "basa", "hane", "yomi", "shio", "sero" };
    static List<(string Id, string Name, double PerTurn, long Hits, long Turns)>? _hands;
    internal static List<(string Id, string Name, double PerTurn, long Hits, long Turns)> Hands => _hands ??= HandRates();
    /// <summary>手数の多い駒 ＝ 命中 ÷ 手番 の上位 10 枚（ボルグ・ホタ・ヒヨを除く）。</summary>
    internal static HashSet<string> ManyHands => Hands.Where(x => x.Id is not ("borg" or "hota" or "hiyo")).Take(10).Select(x => x.Id).ToHashSet();

    static void HandsTable()
    {
        Console.WriteLine("## 手数の表 —— `compare` 61 行 × 第2〜5波 × 115/115 × seed 0..49（H0）の「敵への命中 ÷ 手番」");
        Console.WriteLine();
        Console.WriteLine("命中 ＝ その駒が出どころの、敵への `Damage` の出来事（中継を除く・薙ぎや貫きの巻き込みも1件ずつ）。手番 ＝ `TurnsTaken`。**上位 10 枚（ボルグ・ホタ・ヒヨを除く）を表B の「手数の多い駒」とする**（Phase 0 で固定）。");
        Console.WriteLine();
        Console.WriteLine("| 順 | 駒 | 命中/手番 | 命中 | 手番 | 移動軸 |");
        Console.WriteLine("|--:|---|--:|--:|--:|---|");
        int i = 0;
        foreach (var x in Hands)
        {
            i++;
            Console.WriteLine($"| {i} | {x.Name} | {x.PerTurn:F2} | {x.Hits} | {x.Turns} | {(MoveAxis.Contains(x.Id) ? "○" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"手数の多い駒（上位 10）: {string.Join("・", ManyHands.Select(id => UnitCatalog.ById(id).Name))}");
        Console.WriteLine($"移動軸: {string.Join("・", MoveAxis.Select(id => UnitCatalog.ById(id).Name))}");
        Console.WriteLine();
    }
}
