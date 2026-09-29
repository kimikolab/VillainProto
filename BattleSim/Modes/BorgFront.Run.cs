using System.Globalization;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BG = BorgGuardDiag;

// borgfront pick / run —— 段1（版ごとに相方と席の総当たり: T3 と 雷＋ボルグ）・段2（上位の台を全ての波と倍率で）・表A〜R。
static partial class BorgFrontDiag
{
    internal sealed record Pick(string Ver, string[] Partners, string Best, BA.SeatScore BestS, string Front, BA.SeatScore FrontS, bool BestIsFront);

    static string Dir => _args is { Length: > 3 } ? _args[3] : Path.GetTempPath();
    static string PickPath(string key, string board) => Path.Combine(Dir, $"borgfront235_pick_{board}_{key}.tsv");

    static readonly HashSet<string> PrimeIds = new() { "shio", "sasa" };
    static bool IsT3P(Pick p) => !p.Partners.Any(PrimeIds.Contains);
    static bool IsT31(Pick p) => p.Partners.All(PrimeIds.Contains);

    /// <summary>段1: 芯 ＋ 相方 k 枚（候補から全ての組）× 席 120 × seed 40。</summary>
    static List<Pick> RunPick(string ver, UnitDef[] core, List<UnitDef> cands, int k, out double seconds)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var jobs = new List<UnitDef[]>();
        if (k == 1) foreach (var c in cands) jobs.Add(new[] { c });
        else for (int i = 0; i < cands.Count; i++) for (int j = i + 1; j < cands.Count; j++) jobs.Add(new[] { cands[i], cands[j] });
        var res = new Pick[jobs.Count];
        for (int n = 0; n < jobs.Count; n++)
        {
            var seats = BA.AllSeats(core.Concat(jobs[n]).ToArray(), BA.PickSeeds);
            var best = BA.Best(seats);
            (Formation F, BA.SeatScore S) front = default; long fk = long.MinValue; bool found = false;
            foreach (var x in seats) if (BG.BorgFront(x.F) && (!found || x.S.Key > fk)) { front = x; fk = x.S.Key; found = true; }
            res[n] = new Pick(ver, jobs[n].Select(p => p.Id).ToArray(), BG.Enc(best.F), best.S, BG.Enc(front.F), front.S, BG.BorgFront(best.F));
            if ((n + 1) % 200 == 0) Console.Error.WriteLine($"  段1 {ver} {n + 1}/{jobs.Count}（{sw.Elapsed.TotalMinutes:F1} 分）");
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

    static List<Pick> PicksOf(string name, string key, UnitDef borg, bool thunder, out double sec)
    {
        string path = PickPath(key, thunder ? "TH" : "T3");
        if (File.Exists(path)) return LoadPick(path, out sec);
        var ps = thunder ? RunPick(name, ThunderCore(borg), ThunderCandidates, 1, out sec) : RunPick(name, BG.CoreOf(borg), BA.Candidates, 2, out sec);
        SavePick(ps, path, sec);
        return ps;
    }

    static partial void PickImpl()
    {
        foreach (var (name, key, _, borg) in Versions)
            foreach (bool th in new[] { false, true })
            {
                string path = PickPath(key, th ? "TH" : "T3");
                if (File.Exists(path)) { Console.WriteLine($"{name} {(th ? "雷＋ボルグ" : "T3")}: {path} は既にある（消せば回し直す）"); continue; }
                var ps = PicksOf(name, key, borg, th, out double sec);
                Console.WriteLine($"{name} {(th ? "雷＋ボルグ" : "T3")}: 段1 を {path} に書いた（{ps.Count} 組・{sec / 60:F1} 分）");
            }
    }

    static List<Pick> Ranked(IEnumerable<Pick> ps) => ps.Select((p, i) => (p, i))
        .OrderByDescending(z => z.p.BestS.Key).ThenBy(z => z.i).Select(z => z.p).ToList();

    static string SN(string id) { var n = UnitCatalog.ById(id).Name; int k = n.LastIndexOf('の'); return k >= 0 && k < n.Length - 1 ? n[(k + 1)..] : n; }
    static string PairName(Pick p) => string.Join("＋", p.Partners.Select(SN));
    static string ScoreText(BA.SeatScore s) => $"{s.Sv}/{BA.PickSeeds}・落 {s.Fell}";
    static string SeatText(string enc) => string.Join(" ／ ", enc.Split(',').Select((id, i) => $"{FormationRules.SeatNames[i]} {SN(id)}"));
    static string BorgSeat(string enc) { int i = Array.IndexOf(enc.Split(','), "borg"); return i < 0 ? "—" : FormationRules.SeatNames[i]; }
    static string BorgSeatF(Formation f) { foreach (var (s, d) in f.Occupied()) if (d.Id == "borg") return FormationRules.SeatNames[s]; return "—"; }
    static string Pct(long a, long b) => b == 0 ? "—" : BA.F1(100.0 * a / b) + "%";

    // ---------------------------------------------------------------------------------
    // 段2 ＋ 表
    // ---------------------------------------------------------------------------------
    sealed record Board(string Ver, string Kind, string Label, Formation F, BA.SeatScore? S);

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var t3 = new Dictionary<string, List<Pick>>();
        var th = new Dictionary<string, List<Pick>>();
        var secs = new Dictionary<string, (double T3, double TH)>();
        foreach (var (name, key, _, borg) in Versions)
        {
            t3[name] = Ranked(PicksOf(name, key, borg, false, out double a));
            th[name] = Ranked(PicksOf(name, key, borg, true, out double b));
            secs[name] = (a, b);
        }
        var roles = BA.ObserveRoles(BA.Candidates);

        Console.WriteLine("# 第235期 段1・段2・表A〜R");
        Console.WriteLine();
        Console.WriteLine($"段1: 九 / 新兵 × 200/200 × seed {BA.PickSeed0}..{BA.PickSeed0 + BA.PickSeeds - 1}（{BA.PickSeeds} 本）・版ごとに T3 1,081 組 ＋ 雷＋ボルグ 44 枚 × 席 120。所要 "
            + string.Join("・", Versions.Select(v => $"{v.Name} {secs[v.Name].T3 / 60:F1}＋{secs[v.Name].TH / 60:F1} 分")) + "。");
        Console.WriteLine("並び: 全員生存 → 落ちた駒の数 → 決着T → 組の列挙順（第233・234期と同じ）。T3′ は T3 の表からシオ・ササを含む組を除いたもの、T3-1 はシオ＋ササの組。段2・表は seed 0..199。");
        Console.WriteLine();

        // 段1 のまとめ
        Console.WriteLine("## 段1 のまとめ（総当たりの1位と、ボルグが前列（前1 ／ 前3）にいる席の最良）");
        Console.WriteLine();
        Console.WriteLine("| 版 | T3 の1位 | ボルグ | T3 上位10 の前列 | T3′ の1位 | ボルグ | T3′ 上位10 の前列 | 雷＋ボルグ の1位 | ボルグ | 雷 上位10 の前列 | T3-1（順位） |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var (name, _, _, _) in Versions)
        {
            var all = t3[name]; var p3 = all.Where(IsT3P).ToList(); var t31 = all.First(IsT31); var h = th[name];
            string One(Pick p) => $"{PairName(p)} {ScoreText(p.BestS)}" + (p.BestIsFront ? "" : $"（前列の最良 {ScoreText(p.FrontS)}・{BorgSeat(p.Front)}）");
            Console.WriteLine($"| {name} | {One(all[0])} | {BorgSeat(all[0].Best)} | {all.Take(10).Count(p => p.BestIsFront)} | {One(p3[0])} | {BorgSeat(p3[0].Best)} | {p3.Take(10).Count(p => p.BestIsFront)} | "
                + $"{One(h[0])} | {BorgSeat(h[0].Best)} | {h.Take(10).Count(p => p.BestIsFront)} | {ScoreText(t31.BestS)}・{BorgSeat(t31.Best)}（{all.IndexOf(t31) + 1} 位） |");
        }
        Console.WriteLine();

        foreach (var (name, _, what, _) in Versions)
        {
            Console.WriteLine($"## 段1 {name}（{what}）");
            Console.WriteLine();
            foreach (var (title, list) in new[] { ("T3", t3[name]), ("T3′（シオ・ササ抜き）", t3[name].Where(IsT3P).ToList()), ("雷＋ボルグ", th[name]) })
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
                Console.WriteLine($"全 {list.Count} 組の 1位の席のうちボルグが前列 {front} 組（{BA.F1(100.0 * front / list.Count)}%）・上位 10 では {list.Take(10).Count(p => p.BestIsFront)}・上位 50 では {list.Take(50).Count(p => p.BestIsFront)}。"
                    + $" ボルグの席（上位 10）: " + string.Join("・", list.Take(10).GroupBy(p => BorgSeat(p.Best)).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")) + "。"
                    + $" 1位と同値（得点が同じ）の組 {list.Count(p => p.BestS.Key == list[0].BestS.Key)}。");
                Console.WriteLine();
            }
        }

        // 表G 役割
        Console.WriteLine("## 表G 相方の役割（上位 10 組のうち、その役割の駒を1枚でも含む組の数・第233期と同じ機械の分け方）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 台 | " + string.Join(" | ", BA.RoleNames) + " | シオを含む | ササを含む |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", BA.RoleNames.Length + 2)));
        foreach (var (name, _, _, _) in Versions)
            foreach (var (tn, list) in new[] { ("T3", t3[name]), ("T3′", t3[name].Where(IsT3P).ToList()), ("雷＋ボルグ", th[name]) })
            {
                var top = list.Take(10).ToList();
                Console.WriteLine($"| {name} | {tn} | " + string.Join(" | ", BA.RoleNames.Select((_, r) => top.Count(p => p.Partners.Any(id => roles[id].Roles[r])).ToString()))
                    + $" | {top.Count(p => p.Partners.Contains("shio"))} | {top.Count(p => p.Partners.Contains("sasa"))} |");
            }
        Console.WriteLine();

        // 段2 の台
        var boards = new List<Board>();
        foreach (var (name, _, _, borg) in Versions)
        {
            var all = t3[name]; var p3 = all.Where(IsT3P).ToList(); var t31 = all.First(IsT31); var h = th[name];
            void AddB(string kind, string label, Pick p, bool front)
            {
                string enc = front ? p.Front : p.Best;
                if (boards.Any(b => b.Ver == name && BG.Enc(b.F) == enc)) return;
                boards.Add(new Board(name, kind, label, BG.Dec(enc, borg), front ? p.FrontS : p.BestS));
            }
            for (int i = 0; i < Math.Min(5, all.Count); i++) AddB("T3", $"T3-{i + 1} {PairName(all[i])}", all[i], false);
            for (int i = 0; i < Math.Min(5, p3.Count); i++) AddB("T3′", $"T3′-{i + 1} {PairName(p3[i])}", p3[i], false);
            AddB("T3-1", "T3-1 シオ＋ササ", t31, false);
            if (!all[0].BestIsFront) AddB("T3", $"T3-1位 {PairName(all[0])}・前列", all[0], true);
            if (!p3[0].BestIsFront) AddB("T3′", $"T3′-1位 {PairName(p3[0])}・前列", p3[0], true);
            for (int i = 0; i < Math.Min(3, h.Count); i++) AddB("雷", $"雷＋ボルグ-{i + 1} {PairName(h[i])}", h[i], false);
            if (!h[0].BestIsFront) AddB("雷", $"雷＋ボルグ-1位 {PairName(h[0])}・前列", h[0], true);
            boards.Add(new Board(name, "雷置換", "雷 前1 シガ → ボルグ", ThunderSwap(borg, true), null));
            boards.Add(new Board(name, "雷置換", "雷 前3 ツギ → ボルグ", ThunderSwap(borg, false), null));
        }
        boards.Add(new Board("参考", "参考", "雷（ポンの席）", BA.RefThunder, null));
        boards.Add(new Board("参考", "参考", "移動（第232期の規定）", BA.RefMove, null));

        var res = new Dictionary<(int B, int W, int S), YAgg>();
        for (int bi = 0; bi < boards.Count; bi++)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    res[(bi, w, s)] = Measure(boards[bi].F, w, BA.Scales[s].Sc);
        YAgg Group(int bi, int s, params int[] ws) { var a = new YAgg(); foreach (int w in ws) a.Merge(res[(bi, w, s)]); return a; }
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
        Console.WriteLine("## 表A 全員生存 ／ 勝率（seed 0..199）・ボルグの席と前列にいたターン");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | ボルグ | 前列にいた（主） | 九/新兵 200/200 | 九/新兵 400/300 | 九/新兵 115/115 | 九/農兵 200/200 | 本編 第2〜5波 200/200 の平均 | 本編 115/115 の平均 | 決着T（主） | 落ちた駒/戦（主） | 落ちた駒（主） |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            string C(int w, int s) { var a = res[(bi, w, s)].X.A; return $"{BA.F1(a.Surv)} ／ {BA.F1(a.Win)}"; }
            string M(int s) { var cs = Main.Select(w => res[(bi, w, s)].X.A).ToList(); return $"{BA.F1(cs.Average(a => a.Surv))} ／ {BA.F1(cs.Average(a => a.Win))}"; }
            var y = res[(bi, MW, 0)]; var m = y.X.A;
            Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {BorgSeatF(boards[bi].F)} | {Pct(y.X.BorgFrontTurns, y.X.BorgAliveTurns)} | {C(MW, 0)} | {C(MW, 1)} | {C(MW, 2)} | {C(5, 0)} | {M(0)} | {M(2)} | {BA.F2(m.WinT)} | {BA.F2(m.Per(m.FellSum))} | "
                + string.Join("・", m.Fell.OrderByDescending(kv => kv.Value).Select(kv => $"{SN(kv.Key)} {BA.F1(100.0 * kv.Value / m.N)}%")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("### 表A' 本編の波ごと（200/200 の全員生存 ／ 勝率）");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | " + string.Join(" | ", BA.WaveNames.Take(4)) + " |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
            Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | " + string.Join(" | ", Main.Select(w => { var a = res[(bi, w, 0)].X.A; return $"{BA.F1(a.Surv)} ／ {BA.F1(a.Win)}"; })) + " |");
        Console.WriteLine();

        // 表A'' 版ごとの最良（段2 の値）
        Console.WriteLine("### 表A'' 版ごとの最良（段2・九/新兵 200/200 の全員生存。括弧はその台の 400/300 ／ 本編 200/200 の平均）");
        Console.WriteLine();
        Console.WriteLine("| 版 | T3 の最良 | T3′ の最良 | 雷＋ボルグ の最良 | 雷置換 前1 | 雷置換 前3 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (name, _, _, _) in Versions)
        {
            string BestOf(Func<Board, bool> pred)
            {
                var ix = Enumerable.Range(0, boards.Count).Where(i => boards[i].Ver == name && pred(boards[i])).ToList();
                if (ix.Count == 0) return "—";
                int b = ix.OrderByDescending(i => res[(i, MW, 0)].X.A.Surv).ThenBy(i => i).First();
                var a = res[(b, MW, 0)].X.A;
                return $"{BA.F1(a.Surv)}（{BA.F1(res[(b, MW, 1)].X.A.Surv)} ／ {BA.F1(Main.Average(w => res[(b, w, 0)].X.A.Surv))}）#{b + 1}・ボルグ{BorgSeatF(boards[b].F)}";
            }
            Console.WriteLine($"| {name} | {BestOf(b => b.Kind is "T3" or "T3-1")} | {BestOf(b => b.Kind == "T3′")} | {BestOf(b => b.Kind == "雷")} | {BestOf(b => b.Label.Contains("前1 シガ"))} | {BestOf(b => b.Label.Contains("前3 ツギ"))} |");
        }
        Console.WriteLine();

        // 表B
        Console.WriteLine("## 表B 死因（/戦）・巻き込みは 物理 ／ 燃焼 に分ける・検算（死因の合計 ＝ 倒れた駒）");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MW }), ("九 / 新兵 × 400/300", 1, new[] { MW }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 版 | 台 | 倒れた/戦 | 敵の攻撃 | 巻き込み 物理 | 巻き込み 燃焼 | 味方のその他 | 燃焼の刻み | 毒の刻み | その他・不明 | 一撃死 | 検算 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var y = Group(bi, s, ws); var a = y.X.A;
                bool splitOk = y.SplashPhysDeaths + y.SplashBurnDeaths == a.Cause[BA.CBorgSplash];
                string chk = a.Cause.Sum() == a.DeathEvents && a.DeathEvents == a.FellSum && splitOk ? "一致" : $"**不一致 {a.Cause.Sum()}/{a.DeathEvents}/{(splitOk ? "" : "巻き込み")}**";
                string P(long x) => x == 0 ? "0" : BA.F2(a.Per(x));
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {BA.F2(a.Per(a.DeathEvents))} | {P(a.Cause.Take(5).Sum())} | {P(y.SplashPhysDeaths)} | {P(y.SplashBurnDeaths)} | "
                    + $"{P(a.Cause[6])} | {P(a.Cause[BA.CBurnTick])} | {P(a.Cause[BA.CPoisonTick])} | {P(a.Cause[BA.CUnknown])} | {BA.F2(a.Per(a.CauseOneShot.Sum()))} | {chk} |");
            }
            Console.WriteLine();
        }

        // 表C
        Console.WriteLine("## 表C 被ダメと回復（/戦）: ボルグの被ダメ・半減・燃える巻き込みの行き先・火の癒し・焼き返し");
        Console.WriteLine();
        Console.WriteLine("燃える巻き込みの行き先 ＝ 名目 → 焼かれない（ホタ・火の鎧）で消えた ／ ベニの反転に回った（うち癒えた）／ 普通に受けた HP の減り。火の癒し・焼き返しは 名目 → 癒えた（渇きで止まった回数）。");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MW }), ("九 / 新兵 × 400/300", 1, new[] { MW }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 版 | 台 | ボルグの被ダメ | 味方全体に占める | 半減で切った | 燃える巻き込み 名目 | 焼かれない | 反転（癒えた） | 普通に受けた | 物理の巻き込み（S なし） | 火の癒し 名目 → 癒えた | 焼き返し 回 ／ 名目 → 癒えた | ボルグの回復 計 | ボルグが倒れた |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var y = Group(bi, s, ws); var a = y.X.A;
                long all = a.Taken.Values.Sum(v => v.Sum());
                long physSplash = a.Taken.Values.Sum(v => v[1]) - y.SplashTaken;
                string F(long x) => BA.F1(a.Per(x));
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {F(y.BorgTaken)} | {Pct(y.BorgTaken, all)} | {F(y.X.Saved)} | {F(y.SplashNominal)} | {F(y.SplashImmune)} | {F(y.SplashInverted)}（{F(y.SplashInvHealed)}） | {F(y.SplashTaken)} | {F(physSplash)} | "
                    + $"{F(y.MendNominal)} → {F(y.MendHealed)}{(y.MendDry > 0 ? $"（渇き {F(y.MendDry)}）" : "")} | {BA.F2(a.Per(y.FeedFires))} ／ {F(y.FeedNominal)} → {F(y.FeedHealed)}{(y.FeedDry > 0 ? $"（渇き {F(y.FeedDry)}）" : "")} | {F(y.BorgHealed)} | {Pct(y.BorgFell, a.N)} |");
            }
            Console.WriteLine();
        }

        // 表D
        Console.WriteLine("## 表D 火");
        Console.WriteLine();
        Console.WriteLine("1ターン目に燃えていた ＝ 1ターン目の刻みの後の写しで燃えていた戦の割合。燃えていた ＝ 生きていたターンのうち燃えていた割合（第234期と同じ）。1T の被ダメ ＝ 1ターン目にボルグが受けた HP の減り/戦（敵の一撃の数）。");
        Console.WriteLine("鎧の着火 敵 ／ 自分・くすぶり・焼き返しの自火 は回/戦。燃える敵/味方 ＝ ターン頭の平均体数。");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MW }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 版 | 台 | 1Tに燃えていた | 1T の被ダメ（一撃） | 燃えていた | 前列 | 燃えて前列 | 鎧の着火 敵 ／ 自分 | くすぶり | 焼き返しの自火 | 燃える敵/T | 燃える味方/T | ヒヨ→ボルグ +/戦 | ヒヨの −2/戦 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var y = Group(bi, s, ws); var x = y.X; var a = x.A;
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {Pct(y.BorgT1Burn, y.BorgT1Alive)} | {BA.F1(a.Per(y.BorgT1Taken))}（{BA.F2(a.Per(y.BorgT1Hits))}） | {Pct(x.BorgBurnTurns, x.BorgAliveTurns)} | {Pct(x.BorgFrontTurns, x.BorgAliveTurns)} | {Pct(x.BorgBurnFrontTurns, x.BorgAliveTurns)} | "
                    + $"{BA.F2(a.Per(x.FoeLit))} ／ {BA.F2(a.Per(x.SelfLit))} | {BA.F2(a.Per(y.KindleLit))} | {BA.F2(a.Per(y.FeedFires))} | "
                    + $"{BA.F2((double)a.BurnUnitTurns[0] / Math.Max(1, a.BTurns))} | {BA.F2((double)a.BurnUnitTurns[1] / Math.Max(1, a.BTurns))} | {BA.F1(a.Per(x.HiyoToBorg))} | {BA.F1(a.Per(x.FavorDullTo.Values.Sum()))} |");
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
            Console.WriteLine("| # | 版 | 台 | 駒ごと | ボルグの薙ぎの一撃 | ボルグの割合 | ホタの割合 |");
            Console.WriteLine("|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                var y = Group(bi, s, ws); var a = y.X.A;
                long all = a.Dealt.Values.Sum();
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | " + string.Join("・", boards[bi].F.Occupied().Select(o => $"{SN(o.Def.Id)} {BA.F1(a.Per(a.Dealt.GetValueOrDefault(o.Def.Name)))}"))
                    + $" | {BA.F1(a.Per(y.X.BorgDealtSweep))} | {Pct(a.Dealt.GetValueOrDefault(UnitCatalog.Borg.Name), all)} | {Pct(a.Dealt.GetValueOrDefault(UnitCatalog.Hota.Name), all)} |");
            }
            Console.WriteLine();
        }

        // 表R
        Console.WriteLine("## 表R 雷＋ボルグ: 今の雷の組との比較・ボルグの被弾と回復・カタの雷の重さ");
        Console.WriteLine();
        Console.WriteLine("雷の重さ ＝ 当たった雷1発ごとの「帯びた種類の数」の平均（1発 ＝ 攻 ×(1＋種類)）。");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | 九/新兵 200/200 | 400/300 | 本編 200/200 の平均 | ボルグの被ダメ/戦（主） | ボルグの回復/戦（主） | ボルグが倒れた（主） | 雷 回/戦 ／ 当たった | 雷の重さ（種類） | 雷で削った/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            if (boards[bi].Kind is not ("雷" or "雷置換") && boards[bi].Label != "雷（ポンの席）") continue;
            var y = res[(bi, MW, 0)]; var a = y.X.A;
            Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {BA.F1(a.Surv)} ／ {BA.F1(a.Win)} | {BA.F1(res[(bi, MW, 1)].X.A.Surv)} ／ {BA.F1(res[(bi, MW, 1)].X.A.Win)} | "
                + $"{BA.F1(Main.Average(w => res[(bi, w, 0)].X.A.Surv))} ／ {BA.F1(Main.Average(w => res[(bi, w, 0)].X.A.Win))} | {BA.F1(a.Per(y.BorgTaken))} | {BA.F1(a.Per(y.BorgHealed))} | {Pct(y.BorgFell, a.N)} | "
                + $"{BA.F2(a.Per(y.KataCasts))} ／ {BA.F2(a.Per(y.KataHits))} | {(y.KindsN == 0 ? "—" : BA.F2((double)y.KindsSum / y.KindsN))} | {BA.F1(a.Per(y.KataDealt))} |");
        }
        Console.WriteLine();

        // 表H: H1a ／ H1b（第三波＝渇き）
        Console.WriteLine("## 表H 火の回復と渇き（全部の台 × 本編の第三波・H1a ＝ 渇きを素通り ／ H1b ＝ 渇きに封じられる）");
        Console.WriteLine();
        Console.WriteLine("| # | 台 | 倍率 | H1a 全員生存 ／ 勝率 | H1b 全員生存 ／ 勝率 | H1a 火の回復（癒えた/戦） | H1b 火の回復（癒えた ／ 渇きで止まった回/戦） | ボルグが倒れた H1a ／ H1b |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            if (boards[bi].Ver != "全部") continue;
            var fb = BG.Dec(BG.Enc(boards[bi].F), H1b);
            foreach (int s in new[] { 0, 2 })
            {
                var ya = res[(bi, 1, s)]; var yb = Measure(fb, 1, BA.Scales[s].Sc);
                Console.WriteLine($"| {bi + 1} | {boards[bi].Label} | {BA.Scales[s].Name} | {BA.F1(ya.X.A.Surv)} ／ {BA.F1(ya.X.A.Win)} | {BA.F1(yb.X.A.Surv)} ／ {BA.F1(yb.X.A.Win)} | "
                    + $"{BA.F1(ya.X.A.Per(ya.MendHealed + ya.FeedHealed))} | {BA.F1(yb.X.A.Per(yb.MendHealed + yb.FeedHealed))} ／ {BA.F2(yb.X.A.Per(yb.MendDry + yb.FeedDry))} | {Pct(ya.BorgFell, ya.X.A.N)} ／ {Pct(yb.BorgFell, yb.X.A.N)} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒（段1 を除く）");
    }
}
