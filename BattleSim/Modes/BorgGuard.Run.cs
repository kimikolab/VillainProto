using System.Globalization;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// borgguard pick / run —— 段1（版ごとに相方と席の総当たり）・段2（上位の台を全ての波と倍率で）・表A〜G。
static partial class BorgGuardDiag
{
    internal sealed record Pick(string Ver, string[] Partners, string Best, BA.SeatScore BestS, string Front, BA.SeatScore FrontS, bool BestIsFront);

    static string PickPath(string ver) => Path.Combine(_args is { Length: > 3 } ? _args[3] : Path.GetTempPath(), $"borgguard234_pick_{ver}.tsv");

    static readonly HashSet<string> PrimeIds = new() { "shio", "sasa" };
    static bool IsT3P(Pick p) => !p.Partners.Any(PrimeIds.Contains);
    static bool IsT31(Pick p) => p.Partners.All(PrimeIds.Contains);

    static List<Pick> RunPick(string ver, UnitDef borg, out double seconds)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cands = BA.Candidates;
        var jobs = new List<UnitDef[]>();
        for (int i = 0; i < cands.Count; i++)
            for (int j = i + 1; j < cands.Count; j++) jobs.Add(new[] { cands[i], cands[j] });
        var res = new Pick[jobs.Count];
        for (int k = 0; k < jobs.Count; k++)
        {
            var seats = BA.AllSeats(CoreOf(borg).Concat(jobs[k]).ToArray(), BA.PickSeeds);
            var best = BA.Best(seats);
            (Formation F, BA.SeatScore S) front = default; long fk = long.MinValue; bool found = false;
            foreach (var x in seats) if (BorgFront(x.F) && (!found || x.S.Key > fk)) { front = x; fk = x.S.Key; found = true; }
            res[k] = new Pick(ver, jobs[k].Select(p => p.Id).ToArray(), Enc(best.F), best.S, Enc(front.F), front.S, BorgFront(best.F));
            if ((k + 1) % 200 == 0) Console.Error.WriteLine($"  段1 {ver} {k + 1}/{jobs.Count}（{sw.Elapsed.TotalMinutes:F1} 分）");
        }
        seconds = sw.Elapsed.TotalSeconds;
        return res.ToList();
    }

    static void SavePick(List<Pick> ps, string path, double seconds)
    {
        using var w = new StreamWriter(path);
        w.WriteLine($"# seconds\t{seconds.ToString("F1", CultureInfo.InvariantCulture)}");
        foreach (var p in ps)
            w.WriteLine(string.Join("\t", p.Ver, string.Join(",", p.Partners), p.Best, p.BestS.Sv, p.BestS.Fell, p.BestS.W, p.BestS.T,
                p.Front, p.FrontS.Sv, p.FrontS.Fell, p.FrontS.W, p.FrontS.T, p.BestIsFront ? 1 : 0));
    }

    static List<Pick> LoadPick(string path, out double seconds)
    {
        seconds = double.NaN;
        var res = new List<Pick>();
        foreach (var line in File.ReadAllLines(path))
        {
            var c = line.Split('\t');
            if (c[0] == "# seconds") { seconds = double.Parse(c[1], CultureInfo.InvariantCulture); continue; }
            res.Add(new Pick(c[0], c[1].Split(','), c[2], new BA.SeatScore(int.Parse(c[3]), int.Parse(c[4]), int.Parse(c[5]), long.Parse(c[6])),
                c[7], new BA.SeatScore(int.Parse(c[8]), int.Parse(c[9]), int.Parse(c[10]), long.Parse(c[11])), c[12] == "1"));
        }
        return res;
    }

    static List<Pick> PicksOf(string ver, UnitDef borg, out double sec)
    {
        string path = PickPath(ver);
        if (File.Exists(path)) return LoadPick(path, out sec);
        var ps = RunPick(ver, borg, out sec);
        SavePick(ps, path, sec);
        return ps;
    }

    static List<Pick> Ranked(IEnumerable<Pick> ps) => ps.Select((p, i) => (p, i))
        .OrderByDescending(z => z.p.BestS.Key).ThenBy(z => z.i).Select(z => z.p).ToList();

    static string SN(string id) { var n = UnitCatalog.ById(id).Name; int k = n.LastIndexOf('の'); return k >= 0 && k < n.Length - 1 ? n[(k + 1)..] : n; }
    static string PairName(Pick p) => string.Join("＋", p.Partners.Select(SN));
    static string ScoreText(BA.SeatScore s) => $"{s.Sv}/{BA.PickSeeds}・落 {s.Fell}";
    static string SeatText(string enc) => string.Join(" ／ ", enc.Split(',').Select((id, i) => $"{FormationRules.SeatNames[i]} {SN(id)}"));
    static string BorgSeat(string enc) => FormationRules.SeatNames[Array.IndexOf(enc.Split(','), "borg")];

    static partial void PickImpl()
    {
        foreach (var (ver, _, borg) in Versions)
        {
            string path = PickPath(ver);
            if (File.Exists(path)) { Console.WriteLine($"{ver}: {path} は既にある（消せば回し直す）"); continue; }
            var ps = RunPick(ver, borg, out double sec);
            SavePick(ps, path, sec);
            Console.WriteLine($"{ver}: 段1 を {path} に書いた（{ps.Count} 組・{sec / 60:F1} 分）");
        }
    }

    // ---------------------------------------------------------------------------------
    // 段2 ＋ 表
    // ---------------------------------------------------------------------------------
    sealed record Board(string Ver, string Label, Formation F, BA.SeatScore? S);

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var picks = new Dictionary<string, List<Pick>>();
        var secs = new Dictionary<string, double>();
        foreach (var (ver, _, borg) in Versions) { picks[ver] = PicksOf(ver, borg, out double s); secs[ver] = s; }
        var roles = BA.ObserveRoles(BA.Candidates);

        Console.WriteLine("# 第234期 段1・段2・表A〜G");
        Console.WriteLine();
        Console.WriteLine($"段1: 九 / 新兵 × 200/200 × seed {BA.PickSeed0}..{BA.PickSeed0 + BA.PickSeeds - 1}（{BA.PickSeeds} 本）・版ごとに 1,081 組 × 席 120。所要 "
            + string.Join("・", Versions.Select(v => $"{v.Name} {secs[v.Name] / 60:F1} 分")) + "。");
        Console.WriteLine("並び: 全員生存 → 落ちた駒の数 → 決着T → 組の列挙順（第233期と同じ）。T3′ は T3 の表からシオ・ササを含む組を除いたもの、T3-1 はシオ＋ササの組。段2・表は seed 0..199。");
        Console.WriteLine();

        // 段1 の表
        foreach (var (ver, what, _) in Versions)
        {
            var all = Ranked(picks[ver]);
            var t3p = all.Where(IsT3P).ToList();
            var t31 = all.First(IsT31);
            Console.WriteLine($"## 段1 {ver}（{what}）");
            Console.WriteLine();
            foreach (var (title, list) in new[] { ("T3", all), ("T3′（シオ・ササ抜き）", t3p) })
            {
                Console.WriteLine($"### {title} の上位 10");
                Console.WriteLine();
                Console.WriteLine("| 順 | 相方 | 役割 | 1位の席 | 1位 | ボルグ | ボルグ前列の最良 |");
                Console.WriteLine("|---|---|---|---|---|---|---|");
                for (int i = 0; i < Math.Min(10, list.Count); i++)
                {
                    var p = list[i];
                    Console.WriteLine($"| {i + 1} | {PairName(p)} | {string.Join(" ／ ", p.Partners.Select(id => BA.RoleText(roles[id].Roles)))} | {SeatText(p.Best)} | {ScoreText(p.BestS)} | "
                        + $"{BorgSeat(p.Best)} | {(p.BestIsFront ? "（1位と同じ）" : $"{ScoreText(p.FrontS)}（{BorgSeat(p.Front)}）")} |");
                }
                Console.WriteLine();
                int front = list.Count(p => p.BestIsFront);
                Console.WriteLine($"全 {list.Count} 組の 1位の席のうち **ボルグが前列 {front} 組（{BA.F1(100.0 * front / list.Count)}%）**・上位 10 では {list.Take(10).Count(p => p.BestIsFront)}・上位 50 では {list.Take(50).Count(p => p.BestIsFront)}。"
                    + $"全員生存 {BA.PickSeeds}/{BA.PickSeeds} の組 {list.Count(p => p.BestS.Sv == BA.PickSeeds)} ／ 0 の組 {list.Count(p => p.BestS.Sv == 0)}。"
                    + $" ボルグの席（上位 10）: " + string.Join("・", list.Take(10).GroupBy(p => BorgSeat(p.Best)).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")) + "。");
                Console.WriteLine();
            }
            Console.WriteLine($"T3-1（シオ＋ササ）: {SeatText(t31.Best)}・{ScoreText(t31.BestS)}・T3 の {all.IndexOf(t31) + 1} 位"
                + (t31.BestIsFront ? "（ボルグ前列）" : $"・ボルグ前列の最良 {ScoreText(t31.FrontS)}（{BorgSeat(t31.Front)}）") + "。");
            Console.WriteLine();
        }

        // 表G 役割
        Console.WriteLine("## 表G 相方の役割（上位 10 組のうち、その役割の駒を1枚でも含む組の数・第233期と同じ機械の分け方）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 台 | " + string.Join(" | ", BA.RoleNames) + " | シオを含む | ササを含む |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", BA.RoleNames.Length + 2)));
        foreach (var (ver, _, _) in Versions)
            foreach (var (tn, list) in new[] { ("T3", Ranked(picks[ver])), ("T3′", Ranked(picks[ver]).Where(IsT3P).ToList()) })
            {
                var top = list.Take(10).ToList();
                Console.WriteLine($"| {ver} | {tn} | " + string.Join(" | ", BA.RoleNames.Select((_, r) => top.Count(p => p.Partners.Any(id => roles[id].Roles[r])).ToString()))
                    + $" | {top.Count(p => p.Partners.Contains("shio"))} | {top.Count(p => p.Partners.Contains("sasa"))} |");
            }
        Console.WriteLine();

        // 段2 の台
        var boards = new List<Board>();
        foreach (var (ver, _, borg) in Versions)
        {
            var all = Ranked(picks[ver]);
            var t3p = all.Where(IsT3P).ToList();
            var t31 = all.First(IsT31);
            void AddB(string label, Pick p, bool front)
            {
                string enc = front ? p.Front : p.Best;
                if (boards.Any(b => b.Ver == ver && Enc(b.F) == enc)) return;
                boards.Add(new Board(ver, label, Dec(enc, borg), front ? p.FrontS : p.BestS));
            }
            for (int i = 0; i < Math.Min(5, all.Count); i++) AddB($"T3-{i + 1} {PairName(all[i])}", all[i], false);
            for (int i = 0; i < Math.Min(5, t3p.Count); i++) AddB($"T3′-{i + 1} {PairName(t3p[i])}", t3p[i], false);
            AddB("T3-1 シオ＋ササ", t31, false);
            if (!all[0].BestIsFront) AddB($"T3-1位 {PairName(all[0])}・ボルグ前列", all[0], true);
            if (!t3p[0].BestIsFront) AddB($"T3′-1位 {PairName(t3p[0])}・ボルグ前列", t3p[0], true);
            if (!t31.BestIsFront) AddB("T3-1 シオ＋ササ・ボルグ前列", t31, true);
        }
        boards.Add(new Board("参考", "雷（ポンの席）", BA.RefThunder, null));
        boards.Add(new Board("参考", "移動（第232期の規定）", BA.RefMove, null));

        var res = new Dictionary<(int B, int W, int S), XAgg>();
        for (int bi = 0; bi < boards.Count; bi++)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    res[(bi, w, s)] = Measure(boards[bi].F, w, BA.Scales[s].Sc);
        XAgg Group(int bi, int s, params int[] ws) { var a = new XAgg(); foreach (int w in ws) a.Merge(res[(bi, w, s)]); return a; }
        int[] Main = { 0, 1, 2, 3 };
        const int MW = BA.MainWave;

        Console.WriteLine("## 段2 の台（席）");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | 席 | 段1 |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
            Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {BA.SeatsNamed(boards[bi].F)} | {(boards[bi].S is { } s ? ScoreText(s) : "—")} |");
        Console.WriteLine();

        // 表A
        Console.WriteLine("## 表A 全員生存 ／ 勝率（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | 九/新兵 200/200 | 九/新兵 400/300 | 九/新兵 115/115 | 九/農兵 200/200 | 本編 第2〜5波 200/200 の平均 | 本編 115/115 の平均 | 決着T（主） | 落ちた駒/戦（主） | 落ちた駒（主） |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            string C(int w, int s) { var a = res[(bi, w, s)].A; return $"{BA.F1(a.Surv)} ／ {BA.F1(a.Win)}"; }
            string M(int s) { var cs = Main.Select(w => res[(bi, w, s)].A).ToList(); return $"{BA.F1(cs.Average(a => a.Surv))} ／ {BA.F1(cs.Average(a => a.Win))}"; }
            var m = res[(bi, MW, 0)].A;
            Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {C(MW, 0)} | {C(MW, 1)} | {C(MW, 2)} | {C(5, 0)} | {M(0)} | {M(2)} | {BA.F2(m.WinT)} | {BA.F2(m.Per(m.FellSum))} | "
                + string.Join("・", m.Fell.OrderByDescending(kv => kv.Value).Select(kv => $"{SN(kv.Key)} {BA.F1(100.0 * kv.Value / m.N)}%／T{BA.F1((double)m.FellT[kv.Key] / kv.Value)}")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("### 表A' 本編の波ごと（200/200 の全員生存 ／ 勝率）");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | " + string.Join(" | ", BA.WaveNames.Take(4)) + " |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
            Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | " + string.Join(" | ", Main.Select(w => { var a = res[(bi, w, 0)].A; return $"{BA.F1(a.Surv)} ／ {BA.F1(a.Win)}"; })) + " |");
        Console.WriteLine();

        // 表B
        Console.WriteLine("## 表B 死因（/戦）と一撃死・検算（死因の合計 ＝ 倒れた駒）");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MW }), ("九 / 新兵 × 400/300", 1, new[] { MW }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 版 | 倒れた/戦 | " + string.Join(" | ", BA.Causes) + " | 一撃死 | 検算 |");
            Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", BA.Causes.Length + 2)));
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var a = Group(bi, s, ws).A;
                string chk = a.Cause.Sum() == a.DeathEvents && a.DeathEvents == a.FellSum ? "一致" : $"**不一致 {a.Cause.Sum()}/{a.DeathEvents}**";
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {BA.F2(a.Per(a.DeathEvents))} | " + string.Join(" | ", a.Cause.Select(x => x == 0 ? "0" : BA.F2(a.Per(x))))
                    + $" | {BA.F2(a.Per(a.CauseOneShot.Sum()))}（{(a.DeathEvents == 0 ? "—" : BA.F1(100.0 * a.CauseOneShot.Sum() / a.DeathEvents) + "%")}） | {chk} |");
            }
            Console.WriteLine();
        }

        // 表C
        Console.WriteLine("## 表C 被ダメの内訳（駒ごと・HP の減り/戦）と、ボルグが受けた割合・半減で切った量");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MW }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 版 | 駒ごと（敵の攻撃 ＋ 巻き込み ＋ 刻み ＋ その他・/戦） | ボルグの割合 | ボルグが受けた敵の一撃/戦 | 半減した一撃/戦 | 半減で切った量/戦 |");
            Console.WriteLine("|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var x = Group(bi, s, ws); var a = x.A;
                long all = a.Taken.Values.Sum(v => v.Sum());
                long borg = a.Taken.GetValueOrDefault(UnitCatalog.Borg.Name)?.Sum() ?? 0;
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | " + string.Join("・", boards[bi].F.Occupied().Select(o => $"{SN(o.Def.Id)} {BA.F1(a.Per(a.Taken.GetValueOrDefault(o.Def.Name)?.Sum() ?? 0))}"))
                    + $" | {Pct(borg, all)} | {BA.F2(a.Per(x.BorgEnemyHits))} | {BA.F2(a.Per(x.GuardHits))} | {BA.F1(a.Per(x.Saved))} |");
            }
            Console.WriteLine();
        }

        // 表D
        Console.WriteLine("## 表D 火");
        Console.WriteLine();
        Console.WriteLine("ボルグの燃焼 ＝ ボルグが生きていたターンのうち、ターン頭の写し（刻みの後）で燃えていた割合。前列 ＝ ターン頭の席が前の列。燃える敵/味方 ＝ ターン頭の平均体数。");
        Console.WriteLine("ヒヨの −2 ＝ 贔屓の代金（隣の燃えていない味方）の量/戦と受け手。");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MW }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 版 | ボルグ 燃えていた | 前列にいた | 燃えて前列 | 鎧の着火 敵 ／ 自分/戦 | 火の粉 敵 ／ 味方/戦 | 燃える敵/T | 燃える味方/T | 脆さ/戦 | ヒヨ→ボルグ +/戦 | ヒヨの −2/戦（受け手） |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var x = Group(bi, s, ws); var a = x.A;
                long cinderFoe = a.IgniteFoeBorg - x.FoeLit, cinderAlly = a.IgniteAllyBorg - x.SelfLit;
                string dull = x.FavorDullTo.Count == 0 ? "—" : $"{BA.F1(a.Per(x.FavorDullTo.Values.Sum()))}（" + string.Join("・", x.FavorDullTo.OrderByDescending(kv => kv.Value).Take(3)
                    .Select(kv => $"{SN(UnitCatalog.Everyone.First(u => u.Name == kv.Key).Id)} {BA.F1(a.Per(kv.Value))}")) + "）";
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {Pct(x.BorgBurnTurns, x.BorgAliveTurns)} | {Pct(x.BorgFrontTurns, x.BorgAliveTurns)} | {Pct(x.BorgBurnFrontTurns, x.BorgAliveTurns)} | "
                    + $"{BA.F2(a.Per(x.FoeLit))} ／ {BA.F2(a.Per(x.SelfLit))} | {BA.F2(a.Per(cinderFoe))} ／ {BA.F2(a.Per(cinderAlly))} | "
                    + $"{BA.F2((double)a.BurnUnitTurns[0] / Math.Max(1, a.BTurns))} | {BA.F2((double)a.BurnUnitTurns[1] / Math.Max(1, a.BTurns))} | {BA.F1(a.Per(a.BrittleExtraFoe))} | "
                    + $"{BA.F1(a.Per(x.HiyoToBorg))} | {dull} |");
            }
            Console.WriteLine();
        }

        // 表E
        Console.WriteLine("## 表E 与ダメ（駒ごと・敵の HP の減り/戦）");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MW }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 版 | 駒ごと | ボルグの薙ぎの一撃 | ホタの割合 |");
            Console.WriteLine("|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var x = Group(bi, s, ws); var a = x.A;
                long all = a.Dealt.Values.Sum();
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | " + string.Join("・", boards[bi].F.Occupied().Select(o => $"{SN(o.Def.Id)} {BA.F1(a.Per(a.Dealt.GetValueOrDefault(o.Def.Name)))}"))
                    + $" | {BA.F1(a.Per(x.BorgDealtSweep))} | {Pct(a.Dealt.GetValueOrDefault(UnitCatalog.Hota.Name), all)} |");
            }
            Console.WriteLine();
        }

        // 表F
        Console.WriteLine("## 表F 焼け残り（G2・G4 の台だけ）");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 条件 | 発火/戦 | 発火した戦でボルグが最後まで残った | 発火の後に倒れた戦 ／ 倒れるまでのターン |");
        Console.WriteLine("|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            if (boards[bi].Ver is not ("G2" or "G4")) continue;
            foreach (var (gname, s, ws) in new[] { ("九/新兵 200/200", 0, new[] { MW }), ("九/新兵 400/300", 1, new[] { MW }), ("本編 200/200", 0, Main) })
            {
                var x = Group(bi, s, ws); var a = x.A;
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {gname} | {BA.F2(a.Per(x.SmolderUsed))} | {Pct(x.SmolderSurvived, x.SmolderUsed)} | "
                    + $"{x.SmolderDied} ／ {(x.SmolderDied == 0 ? "—" : BA.F2((double)x.SmolderDiedTurnsAfter / x.SmolderDied))} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒（段1 を除く）");
    }
}
