using System.Globalization;
using BattleCore;
using static Common;

// burnaudit pick / run —— 段1（相方の総当たり）・段2（上位の組を全ての波と倍率で）・表A〜F。
static partial class BurnAuditDiag
{
    /// <summary>段1 の seed の本数（seed 1000..1039・指示書 §4 の「段1 は seed を減らしてよい」）。</summary>
    internal const int PickSeeds = 40;
    internal const int TopB3 = 10, TopB4 = 5;

    internal sealed record Pick(string Tag, string[] Partners, string Best, SeatScore BestS, string Adj, SeatScore AdjS, bool BestIsAdj);

    static string Enc(Formation f) => string.Join(",", Enumerable.Range(0, 5).Select(i => f[i]!.Id));
    static Formation Dec(string s) { var ids = s.Split(','); return Seat(ids.Select(UnitCatalog.ById).ToArray()); }
    static string Name(string id) => UnitCatalog.ById(id).Name;
    static string ShortName(string id) { var n = Name(id); int k = n.LastIndexOf('の'); return k >= 0 && k < n.Length - 1 ? n[(k + 1)..] : n; }

    static string PickPath(string[]? args) => args is { Length: > 3 } ? args[3] : Path.Combine(Path.GetTempPath(), "burnaudit233_pick.tsv");

    static List<Pick> RunPick(out double seconds)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cands = Candidates;
        var jobs = new List<(string Tag, UnitDef[] Partners)>();
        for (int i = 0; i < cands.Count; i++)
            for (int j = i + 1; j < cands.Count; j++) jobs.Add(("B3", new[] { cands[i], cands[j] }));
        foreach (var c in CandidatesB4) jobs.Add(("B4", new[] { UnitCatalog.Beni, c }));
        var res = new Pick[jobs.Count];
        int done = 0;
        for (int k = 0; k < jobs.Count; k++)
        {
            var (tag, ps) = jobs[k];
            var seats = AllSeats(Core.Concat(ps).ToArray(), PickSeeds);
            var best = Best(seats); var adj = Best(seats, adjOnly: true);
            res[k] = new Pick(tag, ps.Select(p => p.Id).ToArray(), Enc(best.F), best.S, Enc(adj.F), adj.S, BorgHotaAdjacent(best.F));
            if (++done % 100 == 0) Console.Error.WriteLine($"  段1 {done}/{jobs.Count}（{sw.Elapsed.TotalMinutes:F1} 分）");
        }
        seconds = sw.Elapsed.TotalSeconds;
        return res.ToList();
    }

    static void SavePick(List<Pick> ps, string path, double seconds)
    {
        using var w = new StreamWriter(path);
        w.WriteLine($"# seconds\t{seconds.ToString("F1", CultureInfo.InvariantCulture)}");
        foreach (var p in ps)
            w.WriteLine(string.Join("\t", p.Tag, string.Join(",", p.Partners), p.Best, p.BestS.Sv, p.BestS.Fell, p.BestS.W, p.BestS.T,
                p.Adj, p.AdjS.Sv, p.AdjS.Fell, p.AdjS.W, p.AdjS.T, p.BestIsAdj ? 1 : 0));
    }

    static List<Pick> LoadPick(string path, out double seconds)
    {
        seconds = double.NaN;
        var res = new List<Pick>();
        foreach (var line in File.ReadAllLines(path))
        {
            var c = line.Split('\t');
            if (c[0] == "# seconds") { seconds = double.Parse(c[1], CultureInfo.InvariantCulture); continue; }
            res.Add(new Pick(c[0], c[1].Split(','), c[2], new SeatScore(int.Parse(c[3]), int.Parse(c[4]), int.Parse(c[5]), long.Parse(c[6])),
                c[7], new SeatScore(int.Parse(c[8]), int.Parse(c[9]), int.Parse(c[10]), long.Parse(c[11])), c[12] == "1"));
        }
        return res;
    }

    /// <summary>段1 の並び: 1位の席の得点（全員生存 → 落ちた駒 → 決着T）。同値は組の列挙順。</summary>
    static List<Pick> Ranked(IEnumerable<Pick> ps, string tag) => ps.Where(p => p.Tag == tag).Select((p, i) => (p, i))
        .OrderByDescending(z => z.p.BestS.Key).ThenBy(z => z.i).Select(z => z.p).ToList();

    static string PairName(Pick p) => string.Join("＋", p.Partners.Where(id => p.Tag == "B3" || id != "beni").Select(ShortName));
    static string ScoreText(SeatScore s) => $"{s.Sv}/{PickSeeds}・落 {s.Fell}・勝 {s.W}";
    static string SeatText(string enc) { var ids = enc.Split(','); return string.Join(" ／ ", ids.Select((id, i) => $"{FormationRules.SeatNames[i]} {ShortName(id)}")); }

    static partial void PickImpl()
    {
        var ps = RunPick(out double sec);
        string path = PickPath(_args);
        SavePick(ps, path, sec);
        Console.WriteLine($"段1 を {path} に書いた（{ps.Count} 組・{sec / 60:F1} 分）");
    }

    // ---------------------------------------------------------------------------------
    // 段2 ＋ 表
    // ---------------------------------------------------------------------------------
    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string path = PickPath(_args);
        double pickSec;
        List<Pick> ps;
        if (File.Exists(path)) ps = LoadPick(path, out pickSec);
        else { ps = RunPick(out pickSec); SavePick(ps, path, pickSec); }
        var roles = ObserveRoles(Candidates);

        Console.WriteLine("# 第233期 段1・段2・表A〜F（盤面は第232期の規定のまま）");
        Console.WriteLine();
        Console.WriteLine($"段1: 九 / 新兵 × 200/200 × seed {PickSeed0}..{PickSeed0 + PickSeeds - 1}（{PickSeeds} 本）・組ごとに席 120 通り。所要 {pickSec / 60:F1} 分（{ps.Count} 組 × 120 × {PickSeeds} ＝ {(long)ps.Count * 120 * PickSeeds:N0} 戦）。");
        Console.WriteLine("並び: 全員生存 → 落ちた駒の数（少ない方）→ 決着T（勝った戦の平均・短い方）→ 組の列挙順。段2・表は seed 0..199（段1 と重ならない）。");
        Console.WriteLine();

        var b3 = Ranked(ps, "B3"); var b4 = Ranked(ps, "B4");
        PrintPick("B3（ボルグ・ホタ・ヒヨ ＋ 相方2枠）", b3, 20, roles);
        PrintPick("B4（ボルグ・ホタ・ヒヨ・ベニ ＋ 相方1枠）", b4, 10, roles);
        PrintRoles(b3, b4, roles);

        // 段2 の台
        var boards = new List<(string Label, Formation F)>();
        foreach (var (tag, list, top) in new[] { ("B3", b3, TopB3), ("B4", b4, TopB4) })
            for (int i = 0; i < Math.Min(top, list.Count); i++)
            {
                var p = list[i];
                boards.Add(($"{tag}-{i + 1} {PairName(p)}・1位{(p.BestIsAdj ? "（隣）" : "")}", Dec(p.Best)));
                if (!p.BestIsAdj) boards.Add(($"{tag}-{i + 1} {PairName(p)}・隣の最良", Dec(p.Adj)));
            }
        boards.Add(("参考 燃焼", RefBurn)); boards.Add(("参考 雷", RefThunder)); boards.Add(("参考 移動", RefMove));

        var res = new Dictionary<(int B, int W, int S), Agg>();
        for (int bi = 0; bi < boards.Count; bi++)
            for (int w = 0; w < WaveNames.Length; w++)
                for (int s = 0; s < Scales.Length; s++)
                    res[(bi, w, s)] = Measure(boards[bi].F, w, Scales[s].Sc);

        Agg Group(int bi, int s, params int[] ws) { var a = new Agg(); foreach (int w in ws) a.Merge(res[(bi, w, s)]); return a; }
        int[] Main = { 0, 1, 2, 3 };

        Console.WriteLine("## 段2 の台（席）");
        Console.WriteLine();
        Console.WriteLine("| # | 台 | 席 | 段1 |");
        Console.WriteLine("|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            var p = FindPick(ps, boards[bi]);
            Console.WriteLine($"| {bi + 1} | {boards[bi].Label} | {SeatsNamed(boards[bi].F)} | {(p is null ? "—" : ScoreText(p.Value))} |");
        }
        Console.WriteLine();

        // 表A
        Console.WriteLine("## 表A 全員生存 ／ 勝率（seed 0..199）");
        Console.WriteLine();
        for (int s = 0; s < Scales.Length; s++)
        {
            Console.WriteLine($"### {Scales[s].Name}");
            Console.WriteLine();
            Console.WriteLine("| # | 台 | " + string.Join(" | ", WaveNames) + " | 本編の平均 |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", WaveNames.Length + 1)));
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var cells = Enumerable.Range(0, WaveNames.Length).Select(w => res[(bi, w, s)]).ToList();
                Console.WriteLine($"| {bi + 1} | {boards[bi].Label} | " + string.Join(" | ", cells.Select(a => $"{F1(a.Surv)} ／ {F1(a.Win)}"))
                    + $" | {F1(cells.Take(4).Average(a => a.Surv))} ／ {F1(cells.Take(4).Average(a => a.Win))} |");
            }
            Console.WriteLine();
        }
        foreach (int s in new[] { 0, 1 })
        {
            Console.WriteLine($"### 表A' 九 / 新兵 × {Scales[s].Name} —— 決着T（勝ち）・落ちた駒（割合 ／ 平均ターン）");
            Console.WriteLine();
            Console.WriteLine("| # | 決着T | 落ちた駒/戦 | 落ちた駒 |");
            Console.WriteLine("|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var a = res[(bi, MainWave, s)];
                Console.WriteLine($"| {bi + 1} | {F2(a.WinT)} | {F2(a.Per(a.FellSum))} | " + string.Join("・", a.Fell.OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{ShortName(kv.Key)} {F1(100.0 * kv.Value / a.N)}%／T{F1((double)a.FellT[kv.Key] / kv.Value)}")) + " |");
            }
            Console.WriteLine();
        }

        // 表B
        Console.WriteLine("## 表B 死因（倒れた一撃の出どころ・/戦）と一撃死");
        Console.WriteLine();
        Console.WriteLine("一撃死 ＝ 倒れた一撃が、直前の手番の頭（最後の `Attack` ／ `TurnStart` の後）からの**1回目の被弾**で、その時点の HP が最大の 5 割以上。");
        Console.WriteLine("**検算**: 死因の合計 ＝ 倒れた駒の数（`Death` の件数）。`PlayerStarterFallen`（戦の終わりに倒れていた駒・蘇生で戻った駒は入らない）との差も出す。");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MainWave }), ("九 / 新兵 × 400/300", 1, new[] { MainWave }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 倒れた/戦 | " + string.Join(" | ", Causes) + " | 一撃死 | 検算 |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Causes.Length + 2)));
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var a = Group(bi, s, ws);
                string chk = a.Cause.Sum() == a.DeathEvents && a.DeathEvents == a.FellSum
                    ? (a.DeathEvents == a.StarterFallenSum ? "一致" : $"一致（戦の終わり {a.StarterFallenSum}）") : $"**不一致 {a.Cause.Sum()}/{a.DeathEvents}**";
                Console.WriteLine($"| {bi + 1} | {F2(a.Per(a.DeathEvents))} | " + string.Join(" | ", a.Cause.Select(x => x == 0 ? "0" : F2(a.Per(x))))
                    + $" | {F2(a.Per(a.CauseOneShot.Sum()))}（{(a.DeathEvents == 0 ? "—" : F1(100.0 * a.CauseOneShot.Sum() / a.DeathEvents) + "%")}） | {chk} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine("### 表B' 九 / 新兵 × 200/200 —— 倒れた駒ごとの死因（/戦・0 は省く）");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Count; bi++)
        {
            var a = res[(bi, MainWave, 0)];
            Console.WriteLine($"- **{bi + 1}** " + string.Join(" ／ ", a.CauseBy.OrderByDescending(kv => kv.Value.Sum()).Select(kv =>
                $"{kv.Key}: " + string.Join("・", Causes.Select((c, i) => (c, v: kv.Value[i])).Where(z => z.v > 0).Select(z => $"{z.c} {F2(a.Per(z.v))}")))));
        }
        Console.WriteLine();

        // 表C
        Console.WriteLine("## 表C 被ダメの内訳（駒ごと・HP の減り/戦・九 / 新兵 × 200/200）");
        Console.WriteLine();
        Console.WriteLine("「燃焼→回復」「毒→回復」はベニの結界で反転した刻みの名目（HP は減っていない）。");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | " + string.Join(" | ", Takes) + " | 合計 | 燃焼→回復 | 毒→回復 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Takes.Length + 3)));
        for (int bi = 0; bi < boards.Count; bi++)
        {
            var a = res[(bi, MainWave, 0)];
            foreach (var (_, d) in boards[bi].F.Occupied())
            {
                var t = a.Taken.GetValueOrDefault(d.Name) ?? new long[Takes.Length];
                Console.WriteLine($"| {bi + 1} | {d.Name} | " + string.Join(" | ", t.Select(x => F1(a.Per(x)))) + $" | {F1(a.Per(t.Sum()))} | "
                    + $"{F1(a.Per(a.BurnInverted.GetValueOrDefault(d.Name)))} | {F1(a.Per(a.PoisonInverted.GetValueOrDefault(d.Name)))} |");
            }
        }
        Console.WriteLine();

        // 表D
        Console.WriteLine("## 表D 火の稼働");
        Console.WriteLine();
        Console.WriteLine("ホタの燃焼中 ＝ 自分の手番の攻撃のうち型が貫き（`Pyre` が燃えている間だけ貫きにする）。ヒヨの空振り ＝ 燃えている味方が 0 体の手番。");
        Console.WriteLine("燃えている駒 ＝ ターン頭（刻みの前）の平均体数（`BrittleLedger`）。「T2 の頭」は 1 ターン目に点いた火が残っていた体数 ÷ 生きていた体数。脆さ ＝ 燃えている敵への上乗せの名目/戦。");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MainWave }), ("九 / 新兵 × 400/300", 1, new[] { MainWave }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | ホタ 燃焼中/手番 | 1T目 | ヒヨ 手番/戦 | 空振り | 配った +/戦 | 受け手（上位） | 燃える味方/T | 燃える敵/T | T2 頭 味方 ／ 敵 | 脆さ/戦 | ボルグの火 敵 ／ 味方/戦 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var a = Group(bi, s, ws);
                bool hasHiyo = boards[bi].F.Occupied().Any(o => o.Def.Id == "hiyo");
                string recv = string.Join("・", a.FavorWhetTo.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{ShortName(UnitCatalog.Everyone.First(u => u.Name == kv.Key).Id)} {F1(a.Per(kv.Value))}"));
                Console.WriteLine($"| {bi + 1} | {Pct(a.HotaPierce, a.HotaAttacks)} | {Pct(a.HotaPierceT1, a.HotaAttacksT1)} | "
                    + (hasHiyo ? $"{F2(a.Per(a.FavorCalls))} | {Pct(a.FavorIdle, a.FavorCalls)} | {F1(a.Per(a.FavorGiven))} | {recv}" : "— | — | — | —")
                    + $" | {F2((double)a.BurnUnitTurns[1] / Math.Max(1, a.BTurns))} | {F2((double)a.BurnUnitTurns[0] / Math.Max(1, a.BTurns))} | "
                    + $"{Pct(a.BurnT2[1], a.AliveT2[1])} ／ {Pct(a.BurnT2[0], a.AliveT2[0])} | {F1(a.Per(a.BrittleExtraFoe))} | {F2(a.Per(a.IgniteFoeBorg))} ／ {F2(a.Per(a.IgniteAllyBorg))} |");
            }
            Console.WriteLine();
        }

        // 表E
        Console.WriteLine("## 表E 与ダメの内訳（駒ごと・敵の HP の減り/戦・九 / 新兵 × 200/200）");
        Console.WriteLine();
        Console.WriteLine("ホタの「燃焼中」は型が貫きの一撃の分、「×4 の上乗せ」はその 3/4（上限・破片で削られる前の比で割った見積もり）。");
        Console.WriteLine();
        Console.WriteLine("| # | 駒ごと（/戦） | ホタ 燃焼中 ／ ×4 の上乗せ |");
        Console.WriteLine("|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            var a = res[(bi, MainWave, 0)];
            Console.WriteLine($"| {bi + 1} | " + string.Join("・", boards[bi].F.Occupied().Select(o => $"{ShortName(o.Def.Id)} {F1(a.Per(a.Dealt.GetValueOrDefault(o.Def.Name)))}"))
                + $" | {F1(a.Per(a.HotaPierceDealt))} ／ {F1(a.Per(a.HotaPierceDealt) * 3 / 4)} |");
        }
        Console.WriteLine();

        // 表F
        Console.WriteLine("## 表F 繋ぎの発火見込み（盤面は変えず数えるだけ・/戦・ターン1〜3 ／ 4以降）");
        Console.WriteLine();
        Console.WriteLine("① 延焼: 燃えている敵が倒れた回数 ／ そのとき隣に生きた敵 ／ 隣に燃えていない生きた敵（＝延焼が新しく火を点けられた回数）。");
        Console.WriteLine("② 火を運ぶ貫き: 燃えているホタの貫きの回数 ／ 抜いた主目標以外の敵 ／ そのうち燃えていなかった数。");
        Console.WriteLine("③ 火の受け渡し: 燃えている味方が敵を倒した回数 ／ そのとき隣に燃えていない生きた味方がいた回数。（参考）敵の撃破 全部。");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MainWave }), ("九 / 新兵 × 400/300", 1, new[] { MainWave }), ("九 / 農兵 × 200/200", 0, new[] { 5 }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 敵の撃破 | ① 燃えて倒れた | ① 隣に生きた敵 | ① 隣に燃えていない敵 | ② 貫き | ② 抜いた他の敵 | ② うち燃えていない | ② 他の敵/貫き | ③ 燃えた味方の撃破 | ③ 隣に燃えていない味方 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var a = Group(bi, s, ws); var l = a.Link;
                string Two(long[] x) => $"{F2(a.Per(x[0]))} ／ {F2(a.Per(x[1]))}";
                Console.WriteLine($"| {bi + 1} | {Two(l.FoeDeaths)} | {Two(l.FoeBurnDeaths)} | {Two(l.FoeBurnDeathNeighbor)} | {Two(l.FoeBurnDeathUnburntNeighbor)} | "
                    + $"{Two(l.PyrePierces)} | {Two(l.PyreExtraHits)} | {Two(l.PyreExtraUnburnt)} | {F2((double)l.PyreExtraHits.Sum() / Math.Max(1, l.PyrePierces.Sum()))} | "
                    + $"{Two(l.AllyBurnKills)} | {Two(l.AllyBurnKillUnburntNeighbor)} |");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒（段1 を除く）");
    }

    static string Pct(long a, long b) => b == 0 ? "—" : F1(100.0 * a / b) + "%";

    static SeatScore? FindPick(List<Pick> ps, (string Label, Formation F) b)
    {
        string enc = Enumerable.Range(0, 5).All(i => b.F[i] is not null) ? Enc(b.F) : "";
        foreach (var p in ps) { if (p.Best == enc) return p.BestS; if (p.Adj == enc) return p.AdjS; }
        return null;
    }

    static void PrintPick(string title, List<Pick> list, int top, Dictionary<string, (bool[] Roles, double[] Rates)> roles)
    {
        Console.WriteLine($"## 段1 {title} の上位 {top}");
        Console.WriteLine();
        Console.WriteLine("| 順 | 相方 | 役割 | 1位の席 | 1位 | ボルグとホタ | 隣の最良 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int i = 0; i < Math.Min(top, list.Count); i++)
        {
            var p = list[i];
            var ids = p.Partners.Where(id => p.Tag == "B3" || id != "beni");
            Console.WriteLine($"| {i + 1} | {PairName(p)} | {string.Join(" ／ ", ids.Select(id => RoleText(roles[id].Roles)))} | {SeatText(p.Best)} | {ScoreText(p.BestS)} | "
                + $"{(p.BestIsAdj ? "隣" : "離れ")} | {(p.BestIsAdj ? "（1位と同じ）" : ScoreText(p.AdjS))} |");
        }
        Console.WriteLine();
        int adj = list.Count(p => p.BestIsAdj);
        Console.WriteLine($"全 {list.Count} 組の 1位の席のうち、ボルグとホタが**隣り合う席は {adj} 組（{F1(100.0 * adj / Math.Max(1, list.Count))}%）**。"
            + $"全員生存 {PickSeeds}/{PickSeeds} の組: {list.Count(p => p.BestS.Sv == PickSeeds)} ／ 0 の組: {list.Count(p => p.BestS.Sv == 0)}。");
        Console.WriteLine();
    }

    static void PrintRoles(List<Pick> b3, List<Pick> b4, Dictionary<string, (bool[] Roles, double[] Rates)> roles)
    {
        Console.WriteLine("## 段1 役割別（B3）");
        Console.WriteLine();
        Console.WriteLine("「上位10 ／ 上位50 に入った組のうち、その役割の駒を1枚でも含む組の数」と「その役割を含む組の最良」。無作為の期待は、その役割の駒を含む組の割合 × 組数。");
        Console.WriteLine();
        Console.WriteLine("| 役割 | 駒の数 | 上位10 | 上位50 | 無作為の期待（10 ／ 50） | 最良の組 | 最良の順位 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        int n = Candidates.Count; long all = (long)n * (n - 1) / 2;
        for (int r = 0; r < RoleNames.Length; r++)
        {
            int k = roles.Values.Count(x => x.Roles[r]);
            double frac = 1.0 - (double)(n - k) * (n - k - 1) / 2 / all;
            bool Has(Pick p) => p.Partners.Any(id => roles[id].Roles[r]);
            int rank = b3.FindIndex(Has);
            Console.WriteLine($"| {RoleNames[r]} | {k} | {b3.Take(10).Count(Has)} | {b3.Take(50).Count(Has)} | {F1(frac * 10)} ／ {F1(frac * 50)} | "
                + (rank < 0 ? "— | —" : $"{PairName(b3[rank])}（{ScoreText(b3[rank].BestS)}） | {rank + 1}") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("### 駒ごと（B3 で、その駒を含む組の最良の順位と 1位の得点・B4 の順位）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 役割 | B3 最良の順位 | B3 最良の組 | B4 の順位 | B4 の得点 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var d in Candidates.OrderBy(d => b3.FindIndex(p => p.Partners.Contains(d.Id))))
        {
            int r3 = b3.FindIndex(p => p.Partners.Contains(d.Id));
            int r4 = b4.FindIndex(p => p.Partners.Contains(d.Id));
            Console.WriteLine($"| {d.Name} | {RoleText(roles[d.Id].Roles)} | {r3 + 1} | {PairName(b3[r3])}（{ScoreText(b3[r3].BestS)}） | "
                + (r4 < 0 ? "— | —" : $"{r4 + 1} | {ScoreText(b4[r4].BestS)}") + " |");
        }
        Console.WriteLine();
    }
}
