using System.Reflection;
using BattleCore;
using static Common;
using B283 = Boss283Diag;
using D315 = Doha315Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S309 = Som309Diag;

// =====================================================================================
// shiga316 —— 第316期「段0: ドハの規定化（DP-s ＝ DP-b ＋ 殴る駒だけ）／ 本体: シガが手番を失わない版（SF-A 〜 SF-D）」。
// 指示書は design/PHASE316_SHIGA_FEAR_SPEC.md ／ 報告は design/PHASE316_SHIGA_FEAR.md。
//
//   段0（ドハ）
//     dotnet run --project BattleSim -c Release 0 shiga316 dboards           # §2-2 G-b: 代表台（第315期の5台）× 波 × 版（規定 ／ DP-b ／ DQ-b ／ DP-s）・強くした条件 500/300
//     dotnet run --project BattleSim -c Release 0 shiga316 dcompare          # §2-2 G-a: `compare` 64 行 × DP-s（基準は規定のドハ・(G2) つき）
//     dotnet run --project BattleSim -c Release 0 shiga316 dgrid <boss|guard> [ver=ps]   # §2-2 G-c: 第297期の「ドハが要る台」の格子（第315期 `doha315 grid` と同じ作り・基準は規定のドハ）
//   本体（シガ）
//     dotnet run --project BattleSim -c Release 0 shiga316 p0                # §4 Phase 0（規定のシガ・規定のドハ込み）: 手番を失った原因・直前の手番の形・各版で消える見込み・ボス
//     dotnet run --project BattleSim -c Release 0 shiga316 boards [版…]      # §5-2 代表台 × 波 × 版（規定 ／ SF-A ／ SF-B ／ SF-C ／ SF-D ＋ 組み合わせ）と対照（シガ → ドルガ）
//     dotnet run --project BattleSim -c Release 0 shiga316 compare [版…]     # §5-3 `compare` 64 行 × 版（(G2) つき）
//     dotnet run --project BattleSim -c Release 0 shiga316 grid <boss|guard> <版> [<Id>|no<Id>]   # §5-4 シガのいる台の格子（固定枠 シガ ＋ カタ ＋ ソム・探索枠2・席 120・3つ目は組の絞り込み）
//     dotnet run --project BattleSim -c Release 0 shiga316 memo <台の一部> <波> <seed> <版> [log]   # 台本の並び（怖気 ／ 怖くない ／ 痺れ）
//     dotnet run --project BattleSim -c Release 0 shiga316 find [noprev]     # 台本の例を探す
//     dotnet run --project BattleSim -c Release 0 shiga316 check             # 自己検査
//
// 版の名前（ASCII）: 規定 base ／ a ／ b ／ c ／ d ／ 組み合わせは文字を並べる（ad ／ cd ／ acd など）。ドハは base（規定）／ pb ／ qb ／ ps。
// **予測のファイルが無ければ本体の本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p316/predict.md`）。
// =====================================================================================
static class Shiga316Diag
{
    const string PredictFile = ".tmp/p316/predict.md";
    const int Seeds = 200;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"shiga316: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "dboards": DBoards(); return;
            case "dcompare": DCompare(); return;
            case "dgrid": DGrid(A(3, "boss"), A(4, "ps")); return;
            case "p0": P0(); return;
            case "boards": Boards(args.Skip(3).ToArray()); return;
            case "compare": CompareAll(args.Skip(3).ToArray()); return;
            case "grid": Grid(A(3, "boss"), A(4, "d"), A(5, "")); return;
            case "memo": Memo(A(3, "鞭"), A(4, "boss"), int.Parse(A(5, "0")), A(6, "base"), A(7, "") == "log"); return;
            case "find": Find(A(3, "noprev")); return;
            case "check": Check(); return;
            default: Console.WriteLine("shiga316: モードは dboards / dcompare / dgrid / p0 / boards / compare / grid / memo / find / check。"); return;
        }
    }

    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x >= 0 ? $"+{x:F1}" : $"{x:F1}";
    static string Per(long x, long n) => n == 0 ? "—" : F1((double)x / n);
    static string Pct(long x, long n) => n == 0 ? "—" : F1(100.0 * x / n);

    /// <summary>編成の中の <paramref name="id"/> の駒をすべて <paramref name="to"/> に替える（定義の参照ではなく Id で引く——規定が替わっても同じ台を指す）。陣形とレリックは保つ。</summary>
    internal static Formation SwapId(Formation f, string id, UnitDef to)
    {
        var g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied()) if (d.Id == id) g[slot] = to;
        return g;
    }

    // =====================================================================================
    // 段0 —— ドハの規定化（DP-s）
    // =====================================================================================
    internal static readonly (string Tag, UnitDef Def)[] DVers =
    {
        ("規定", UnitCatalog.DohaH316), ("DP-b", UnitCatalog.DohaDPb), ("DQ-b", UnitCatalog.DohaDQb), ("DP-s", UnitCatalog.DohaDPs),
    };
    static UnitDef DVer(string s) => s switch { "pb" => UnitCatalog.DohaDPb, "qb" => UnitCatalog.DohaDQb, "ps" => UnitCatalog.DohaDPs, _ => UnitCatalog.DohaH316 };
    static string DTag(UnitDef d) => DVers.First(v => ReferenceEquals(v.Def, d)).Tag;

    /// <summary>第315期の DQ-b の格子の「ドハが要る台」（`design/PHASE315_DOHA_PUSH2.md` §6・規定 → DQ-b）。G-c の基準。</summary>
    internal const int GateBossDQb = 1757, GateGuardDQb = 5666;

    sealed class DSt
    {
        public long N, Wins, WinT, LossT, DohaTurns, PushTurns, Pushes, PushZero, NoElig, SkipNo, SkipStoic, GiftTurns, GiftDealt, Passes, Passed;
        public readonly Dictionary<string, long> To = new(), Skipped = new();
        public void Add(DSt o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LossT += o.LossT; DohaTurns += o.DohaTurns; PushTurns += o.PushTurns; Pushes += o.Pushes; PushZero += o.PushZero;
            NoElig += o.NoElig; SkipNo += o.SkipNo; SkipStoic += o.SkipStoic; GiftTurns += o.GiftTurns; GiftDealt += o.GiftDealt; Passes += o.Passes; Passed += o.Passed;
            foreach (var (s, d) in new[] { (o.To, To), (o.Skipped, Skipped) }) foreach (var (k, v) in s) d[k] = d.GetValueOrDefault(k) + v;
        }
        public double WinRate => N == 0 ? 0 : 100.0 * Wins / N;
        public string Win => F1(WinRate);
        public string WinTurn => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string Share(Dictionary<string, long> d) { long t = d.Values.Sum(); return t == 0 ? "—" : string.Join("・", d.OrderByDescending(x => x.Value).Take(4).Select(x => $"{x.Key} {100.0 * x.Value / t:F0}%")); }
    }

    static DSt DFight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new DSt { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else a.LossT = r.Turns;
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => Short(g.First().Def));
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;
            if (t.PushGot > 0) a.To[nm] = a.To.GetValueOrDefault(nm) + t.PushGot;
            if (t.PushSkipped > 0) a.Skipped[nm] = a.Skipped.GetValueOrDefault(nm) + t.PushSkipped;
            a.GiftTurns += t.ShareGiftTurns; a.GiftDealt += t.ShareGiftDealt;
            if (id != "doha") continue;
            a.DohaTurns += t.TurnsTaken; a.PushTurns += t.PushTurns; a.Pushes += t.Pushes; a.PushZero += t.PushZero; a.NoElig += t.PushNoEligible;
            a.SkipNo += t.PushSkipNoStrike; a.SkipStoic += t.PushSkipStoic; a.Passes += t.PushPasses; a.Passed += t.PushPassed;
        }
        return a;
    }

    static DSt DMeasure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new DSt[n];
        Parallel.For(0, n, i => parts[i] = DFight(f, w, i));
        var all = new DSt();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    /// <summary>§2-2 G-b と代表台の全体（第315期の5台・規定の倍率 ＋ 守り型の2台は強くした条件 500/300）。</summary>
    static void DBoards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = D315.MakeWaves(100, 100);
        var strong = D315.MakeWaves(D315.StrongHp, D315.StrongAtk).Where(w => w.Name is "ボス" or "近衛" or "大隊" or "本編 第5波").ToArray();
        Console.WriteLine($"# 第316期 段0 —— ドハ DP-s × 代表台（第315期 §4-2 の5台）× 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"版: {string.Join(" ／ ", DVers.Select(v => v.Tag))}（台のドハを Id で差し替える）。強くした条件 ＝ HP {D315.StrongHp}% ／ 攻 {D315.StrongAtk}%（第315期と同じ）。差は規定から。**G-b ＝ 守り型【強】で DP-s と DP-b の差が ±5 以内**。");
        var gb = new List<string>();
        bool gbOk = true;
        foreach (var (bn0, bf, hasStrong) in D315.Boards())
            foreach (bool st in hasStrong ? new[] { false, true } : new[] { false })
            {
                string bn = st ? $"{bn0}【強】" : bn0;
                Console.WriteLine();
                Console.WriteLine($"## {bn}（{Seats(bf)}）");
                Console.WriteLine();
                Console.WriteLine("| 波 | " + string.Join(" | ", DVers.Select(v => v.Tag)) + " | DP-s − DP-b | ドルガ | 倒しT（" + string.Join(" ／ ", DVers.Select(v => v.Tag)) + "） | DP-s: 押した ／ 窓を読んだ手番 /戦 | 相手 | 飛ばした最多（殴らない ／ 支援拒否）/戦・顔ぶれ | 相手なしで殴った /戦 | 送り出された手番の与ダメ /戦 | 渡した上乗せ /戦 |");
                Console.WriteLine("|---|" + string.Concat(DVers.Select(_ => "--:|")) + "--:|--:|---|---|---|---|--:|--:|--:|");
                foreach (var w in st ? strong : waves)
                {
                    var v = DVers.Select(x => DMeasure(SwapId(bf, "doha", x.Def), w)).ToArray();
                    string dolga = v.Any(x => x.WinRate >= 50) ? DMeasure(SwapId(bf, "doha", UnitCatalog.DolgaH395), w).Win : "—";
                    var s = v[3];
                    double dd = s.WinRate - v[1].WinRate;
                    if (st && bn0 == "守り型") { gb.Add($"{w.Name} {Sg(dd)}"); if (Math.Abs(dd) > 5) gbOk = false; }
                    Console.WriteLine($"| {w.Name} | {string.Join(" | ", v.Select((x, i) => i == 0 ? x.Win : $"{x.Win}（{Sg(x.WinRate - v[0].WinRate)}）"))} | {Sg(dd)} | {dolga} | {string.Join(" ／ ", v.Select(x => x.WinTurn))} | "
                        + $"{Per(s.Pushes, s.N)} ／ {Per(s.PushTurns, s.N)} | {s.Share(s.To)} | {Per(s.SkipNo, s.N)} ／ {Per(s.SkipStoic, s.N)}・{s.Share(s.Skipped)} | {Per(s.NoElig, s.N)} | {Per(s.GiftDealt, s.N)} | {Per(s.Passed, s.N)} |");
                }
            }
        Console.WriteLine();
        Console.WriteLine($"**G-b（守り型【強】・DP-s − DP-b が ±5 以内）: {(gbOk ? "越えた" : "越えない")}**（{string.Join("・", gb)}）");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    /// <summary>§2-2 G-a: `compare` 64 行 × DP-s（基準は規定）。</summary>
    static void DCompare()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuildsH316();
        int nw = EnemyCatalog.Stages.Count;
        double[,] Grid(UnitDef d)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = SwapId(rows[ri].F, "doha", d);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }
        var b = Grid(UnitCatalog.DohaH316);
        Console.WriteLine("# 第316期 段0 G-a —— `compare` 64 行 × DP-s（seed 0..199・基準は規定のドハ）");
        foreach (var (tag, d) in new[] { ("DP-b（第313期・参考）", UnitCatalog.DohaDPb), ("DP-s", UnitCatalog.DohaDPs) })
        {
            var g = Grid(d);
            Console.WriteLine();
            Console.WriteLine($"## {tag}");
            Console.WriteLine();
            var (moved, drops) = PrintCompare(rows, b, g, "doha");
            if (tag == "DP-s")
            {
                int ri = Array.FindIndex(rows, r => r.Name == D315.Ret2);
                double d2 = g[ri, 1] - b[ri, 1];
                bool ok = Math.Abs(d2) <= 2 && drops == 0;
                Console.WriteLine();
                Console.WriteLine($"**G-a（{D315.Ret2} の第2波が規定から ±2 以内・−10pt 以上の落ちが 0 行）: {(ok ? "越えた" : "越えない")}**（第2波 {F1(b[ri, 1])} → {F1(g[ri, 1])}（{Sg(d2)}）・落ち {drops} 行）");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>`compare` の差を行ごとに書く（動いた行だけ）。(G2) の分解も書く。返り値は（動いたセル, −10pt 以上落ちた行）。</summary>
    internal static (int Moved, int Drops) PrintCompare((string Name, Formation F)[] rows, double[,] b, double[,] g, string holderId)
    {
        int nw = b.GetLength(1);
        double M(double[,] x, int ri) => Enumerable.Range(1, nw - 1).Average(w => x[ri, w]);
        Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|");
        int movedRows = 0, movedCells = 0, outside = 0;
        var drops = new List<int>();
        for (int ri = 0; ri < rows.Length; ri++)
        {
            bool any = Enumerable.Range(0, nw).Any(w => g[ri, w] != b[ri, w]);
            bool holds = rows[ri].F.Occupied().Any(o => o.Def.Id == holderId);
            if (any && !holds) outside++;
            if (!any) continue;
            movedRows++;
            movedCells += Enumerable.Range(0, nw).Count(w => g[ri, w] != b[ri, w]);
            double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - b[ri, w]);
            if (drop <= -10) drops.Add(ri);
            Console.WriteLine($"| {rows[ri].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => g[ri, w] == b[ri, w] ? F1(g[ri, w]) : $"{F1(g[ri, w])}（{Sg(g[ri, w] - b[ri, w])}）")) + $" | {F1(M(g, ri))} | {Sg(M(g, ri) - M(b, ri))} | {Sg(drop)} |");
        }
        if (movedRows == 0) Console.WriteLine("| （動いた行なし） |" + string.Concat(Enumerable.Range(0, nw + 3).Select(_ => " |")));
        Console.WriteLine();
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        double p5b = pr.Average(ri => b[ri, nw - 1]), p5 = pr.Average(ri => g[ri, nw - 1]);
        double all = Enumerable.Range(0, rows.Length).Average(ri => M(g, ri)), allB = Enumerable.Range(0, rows.Length).Average(ri => M(b, ri));
        Console.WriteLine($"動いた行 {movedRows} ／ セル {movedCells}・持ち主のいない行で動いたセル **{outside}**・全64行の第2〜5波平均 {F1(allB)} → {F1(all)}（{Sg(all - allB)}）・主判定19行の第五波 {F1(p5b)} → {F1(p5)}（歯止め {F1(Baseline.PrimaryFifthFloor)}）");
        if (drops.Count == 0) { Console.WriteLine("(G2): −10.0pt 以上落ちた行なし。"); return (movedCells, 0); }
        foreach (int ri in drops)
        {
            var parts = new List<string>(); bool broken = false;
            foreach (var d in rows[ri].F.Occupied().Select(o => o.Def).Distinct())
            {
                var others = Enumerable.Range(0, rows.Length).Where(rj => rj != ri && rows[rj].F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToList();
                if (others.Count == 0) { parts.Add($"{Short(d)} 他の行 0"); continue; }
                double ch = others.Average(rj => M(g, rj) - M(b, rj));
                if (ch <= -3.0) broken = true;
                parts.Add($"{Short(d)} {Sg(ch)}（{others.Count} 行）");
            }
            Console.WriteLine($"- (G2) {rows[ri].Name}: {string.Join("・", parts)} → **{(broken ? "壊れ" : "編成上の制約")}**");
        }
        return (movedCells, drops.Count);
    }

    /// <summary>§2-2 G-c: 第297期の「ドハが要る台」の格子（第315期 `doha315 grid` と同じ作り・基準は規定）。</summary>
    static void DGrid(string waveKey, string ver)
    {
        var w = D315.MakeWaves(100, 100).First(x => x.Name == (waveKey == "boss" ? "ボス" : "近衛"));
        var dv = DVer(ver);
        var basis = UnitCatalog.DohaH316;
        var pool = UnitCatalog.All.Select(PinDS316).Where(d => d.Id != "doha").ToArray();
        const int seats = 6, seeds = 10;
        int step = Math.Max(1, 120 / seats);
        var seatSet = IntPerms(5).Where((_, i) => i % step == 0).Take(seats).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第316期 段0 G-c 格子 —— {w.Name} × 規定 ／ {DTag(dv)}（固定枠 ドハ ＋ アタッカー1枚 × 探索枠3・席 {seatSet.Length} 通り・seed 0..{seeds - 1}・第315期と同じ作り）");
        Console.WriteLine();
        int Wins(Formation g) { int c = 0; for (int sd = 0; sd < seeds; sd++) if (BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) c++; return c; }
        Formation Seat(UnitDef[] five, int[] perm) { var g = new Formation(); for (int s = 0; s < 5; s++) g[perm[s]] = five[s]; return g; }
        var rows = new System.Collections.Concurrent.ConcurrentBag<(string Atk, double Base, double Ver, double Dolga)>();
        foreach (var atk in D315.Attackers)
        {
            var free = pool.Where(d => !ReferenceEquals(d, atk)).ToArray();
            var combos = new List<UnitDef[]>();
            for (int i = 0; i < free.Length; i++) for (int j = i + 1; j < free.Length; j++) for (int k = j + 1; k < free.Length; k++) combos.Add(new[] { free[i], free[j], free[k] });
            Parallel.ForEach(combos, c =>
            {
                double Best(UnitDef d, out int[] seat)
                {
                    var five = new[] { d, atk, c[0], c[1], c[2] };
                    int best = -1; seat = seatSet[0];
                    foreach (var perm in seatSet) { int x = Wins(Seat(five, perm)); if (x > best) { best = x; seat = perm; } if (best == seeds) break; }
                    return 100.0 * best / seeds;
                }
                double b = Best(basis, out var sb), v = Best(dv, out var sv);
                if (Math.Max(b, v) < 50) return;
                var seat = v >= b ? sv : sb;
                double dolga = 100.0 * Wins(Seat(new[] { UnitCatalog.DolgaH395, atk, c[0], c[1], c[2] }, seat)) / seeds;
                rows.Add((Short(atk), b, v, dolga));
            });
        }
        var all = rows.ToList();
        bool Need(double win, double dolga) => win >= 50 && dolga <= win / 2;
        Console.WriteLine($"台の数（規定か版で 50% 以上）{all.Count}・所要 {sw.Elapsed.TotalSeconds:F0} 秒。**ドハが要る台** ＝ その版で 50% 以上 かつ ドハ → ドルガで半分以下（R383）。");
        Console.WriteLine();
        Console.WriteLine($"| アタッカー | ドハが要る台（規定 ／ {DTag(dv)}） | 版 − 旧 の平均 | 版が +10pt 以上 ／ −10pt 以下 |");
        Console.WriteLine("|---|---|--:|---|");
        foreach (var g in all.GroupBy(r => r.Atk))
            Console.WriteLine($"| {g.Key} | {g.Count(r => Need(r.Base, r.Dolga))} ／ {g.Count(r => Need(r.Ver, r.Dolga))} | {g.Average(r => r.Ver - r.Base):+0.0;-0.0} | {g.Count(r => r.Ver - r.Base >= 10)} ／ {g.Count(r => r.Ver - r.Base <= -10)} |");
        int nb = all.Count(r => Need(r.Base, r.Dolga)), nv = all.Count(r => Need(r.Ver, r.Dolga));
        Console.WriteLine($"| **計** | **{nb:N0} ／ {nv:N0}** | {all.Average(r => r.Ver - r.Base):+0.0;-0.0} | {all.Count(r => r.Ver - r.Base >= 10):N0} ／ {all.Count(r => r.Ver - r.Base <= -10):N0} |");
        Console.WriteLine();
        int gate = waveKey == "boss" ? GateBossDQb : GateGuardDQb;
        Console.WriteLine($"**G-c（{w.Name}・ドハが要る台が第315期の DQ-b {gate:N0} 以上）: {(nv >= gate ? "越えた" : "越えない")}**（{DTag(dv)} {nv:N0}）");
    }

    /// <summary>0..n−1 の並べ替え（第315期 `doha315` の `Perms` と同じ順）。</summary>
    static IEnumerable<int[]> IntPerms(int n)
    {
        var a = Enumerable.Range(0, n).ToArray();
        IEnumerable<int[]> Rec(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var x in Rec(k + 1)) yield return x;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Rec(0);
    }

    // =====================================================================================
    // 本体 —— シガの版
    // =====================================================================================
    internal sealed record SVer(string Tag, UnitDef Def);
    static readonly Dictionary<char, (TraitId Id, string Plus, string? Minus)> Cards = new()
    {
        ['a'] = (TraitId.FearNoStun, "。自分の雷では痺れない", null),
        ['b'] = (TraitId.LiveWireHold, "。手番の鞭でも、身の雷を手放さない", null),
        ['c'] = (TraitId.ScourgeDaunt, "", "感電していないとき、動ける敵を主目標に打つと怖気づく（萎縮した敵は動けない敵とみなす）"),
        ['d'] = (TraitId.ScourgeShockSpare, "", "感電していないとき、感電もしていない動ける敵を主目標に打つと怖気づく"),
    };
    static readonly Dictionary<string, SVer> _combo = new();
    /// <summary>版の名前（base ／ a ／ b ／ c ／ d ／ 組み合わせ ad など）→ 版。単独の版は `UnitCatalog.ShigaSFa` 〜 `ShigaSFd`、組み合わせは `UnitCatalog.ShigaSF` で札を重ねる。</summary>
    internal static SVer SV(string n)
    {
        n = n.ToLowerInvariant().Replace("sf-", "").Replace("-", "");
        if (n is "base" or "規定" or "") return new("規定", UnitCatalog.ShigaSWa);
        if (n == "a") return new("SF-A", UnitCatalog.ShigaSFa);
        if (n == "b") return new("SF-B", UnitCatalog.ShigaSFb);
        if (n == "c") return new("SF-C", UnitCatalog.ShigaSFc);
        if (n == "d") return new("SF-D", UnitCatalog.ShigaSFd);
        lock (_combo)
        {
            if (_combo.TryGetValue(n, out var v)) return v;
            var keys = n.Distinct().OrderBy(c => c).ToArray();
            string plus = UnitCatalog.ShigaSWa.PlusText + string.Concat(keys.Select(c => Cards[c].Plus));
            string minus = keys.Contains('d') ? Cards['d'].Minus! : keys.Contains('c') ? Cards['c'].Minus! : UnitCatalog.ShigaSWa.MinusText;
            if (keys.Contains('c') && keys.Contains('d')) minus = "感電していないとき、感電もしていない動ける敵を主目標に打つと怖気づく（萎縮した敵は動けない敵とみなす）";
            v = new("SF-" + new string(keys).ToUpperInvariant(), UnitCatalog.ShigaSF(keys.Select(c => Cards[c].Id).ToArray(), plus, minus));
            _combo[n] = v;
            return v;
        }
    }
    static SVer[] Single => new[] { SV("base"), SV("a"), SV("b"), SV("c"), SV("d") };

    /// <summary>波: ボス ／ 近衛 ／ 大隊 ／ 九体 新兵 ／ 九体 農兵 ／ 本編の5波（第310期 `som310` と同じ並び）。</summary>
    static S287.Wave[] Waves => Som310Diag.Waves;
    static S287.Wave WaveOf(string n) => n switch { "nine" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };

    static Formation Playtest(string n) => PlaytestH316.First(r => r.Name == n).F;
    static Formation RowF(string n) => CompareBuildsH316().First(r => r.Name == n).F;
    internal const string ThunderBoard = "試遊・感電 雷の型";

    /// <summary>§5-2 の代表台（規定の駒・席は出典のまま）。「光の盾 鞭」は光の盾 の クビ → シガ そのもの（第312期のプリセット）。</summary>
    internal static (string Name, Formation F)[] SBoards() => new (string, Formation)[]
    {
        ("光の盾 鞭（クビ→シガ）", Playtest(Som312Diag.Whip)),
        ("光の盾 重 鞭", Playtest(Som312Diag.WhipHeavy)),
        ("感電 行", RowF("感電 (シガ×カタ×ソム)")),
        ("勝ち台 ツギ→ソム", Som314Diag.Boards().First(b => b.Name == "勝ち台 ツギ→ソム").F),
        ("雷の型", Playtest(ThunderBoard)),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class FSt
    {
        public long N, Wins, WinT, LossT, FirstDeathN, FirstDeathT, Caps, LastT;
        public long Turns, Acted, Lost, Wired, Swings, OutSwings, CowerOut, Cowered, WireSpared, ShockSpared, DauntSpared, StunSkipped, Held, SwFires, SwNoCharge, SwDealt, Dealt, SelfPops, SelfStunned, SelfStunMissed, CowerSpent;
        public long TeamDealt, BossHeal, BattleTurns, ShigaDeathN, ShigaDeathT;
        public long[] Cause = new long[7], PrimHist = new long[16], PrevBits = new long[48], PrevN = new long[3], PrevAtk = new long[3], PrevWired = new long[3], PrevCharge0 = new long[3], Nine = new long[3], NoPrev = new long[3];
        public void Add(FSt o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LossT += o.LossT; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; Caps += o.Caps;
            Turns += o.Turns; OutSwings += o.OutSwings; CowerOut += o.CowerOut; Acted += o.Acted; Lost += o.Lost; Wired += o.Wired; Swings += o.Swings; Cowered += o.Cowered; WireSpared += o.WireSpared; ShockSpared += o.ShockSpared; DauntSpared += o.DauntSpared;
            StunSkipped += o.StunSkipped; Held += o.Held; SwFires += o.SwFires; SwNoCharge += o.SwNoCharge; SwDealt += o.SwDealt; Dealt += o.Dealt; SelfPops += o.SelfPops; SelfStunned += o.SelfStunned; SelfStunMissed += o.SelfStunMissed;
            CowerSpent += o.CowerSpent; TeamDealt += o.TeamDealt; BossHeal += o.BossHeal; BattleTurns += o.BattleTurns; ShigaDeathN += o.ShigaDeathN; ShigaDeathT += o.ShigaDeathT;
            void Sum(long[] a, long[] b) { for (int i = 0; i < a.Length; i++) a[i] += b[i]; }
            Sum(Cause, o.Cause); Sum(PrimHist, o.PrimHist); Sum(PrevBits, o.PrevBits); Sum(PrevN, o.PrevN); Sum(PrevAtk, o.PrevAtk); Sum(PrevWired, o.PrevWired); Sum(PrevCharge0, o.PrevCharge0); Sum(Nine, o.Nine); Sum(NoPrev, o.NoPrev);
        }
        public double WinRate => N == 0 ? 0 : 100.0 * Wins / N;
        public string Win => F1(WinRate);
        public string WinTurn => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string LossTurn => N - Wins == 0 ? "—" : F1((double)LossT / (N - Wins));
        public string Life => FirstDeathN == 0 ? "—" : F1((double)FirstDeathT / FirstDeathN);
        public string PerB(long x) => Per(x, N);
        /// <summary>主目標の種類（優先 動けない → 感電 → 萎縮 → どれでもない）の手番の鞭に占める割合。</summary>
        public string PrimKinds()
        {
            long b = 0, s = 0, d = 0, n = 0;
            for (int i = 0; i < 16; i++) { long c = PrimHist[i]; if ((i & 1) != 0) b += c; else if ((i & 2) != 0) s += c; else if ((i & 4) != 0) d += c; else n += c; }
            long t = b + s + d + n;
            return t == 0 ? "—" : $"{Pct(b, t)} ／ {Pct(s, t)} ／ {Pct(d, t)} ／ {Pct(n, t)}";
        }
        public long LostCower => Cause[0];
        public long LostShock => Cause[1];
        public long LostOther => Cause.Skip(2).Sum();
        public long PrevWith(int k, int bit) { long c = 0; for (int i = 0; i < 16; i++) if ((i & bit) != 0) c += PrevBits[k * 16 + i]; return c; }
    }

    static FSt Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: false);
        var a = new FSt { N = 1, BattleTurns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else { a.LossT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) a.Caps = 1; }
        var dead = p.Where(u => u.LastDeathTurn > 0).Select(u => u.LastDeathTurn).ToList();
        if (dead.Count > 0) { a.FirstDeathN = 1; a.FirstDeathT = dead.Min(); }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit) if (mine.Contains(id)) a.TeamDealt += t.DamageToEnemy;
        if (r.TallyByUnit.TryGetValue("boss_regular", out var bt)) a.BossHeal = bt.Healed;
        var sh = p.FirstOrDefault(u => u.Def.Id == "shiga");
        if (sh is not null && sh.LastDeathTurn > 0) { a.ShigaDeathN = 1; a.ShigaDeathT = sh.LastDeathTurn; }
        if (!r.TallyByUnit.TryGetValue("shiga", out var s)) return a;
        a.Turns = s.TurnsTaken; a.Lost = s.TurnStalls; a.Acted = s.TurnsTaken - s.TurnStalls; a.Wired = s.FearTurnsWired; a.Swings = s.FearSwings; a.OutSwings = s.FearOutSwings; a.CowerOut = s.FearCowerOut;
        a.Cowered = s.WhipCowered; a.WireSpared = s.WhipWiredSpared; a.ShockSpared = s.WhipShockSpared; a.DauntSpared = s.WhipDauntSpared; a.StunSkipped = s.FearStunSkipped; a.Held = s.WireHeld;
        a.SwFires = s.SwFires; a.SwNoCharge = s.SwNoCharge; a.SwDealt = s.SwWhipDealt + s.SwBoltDealt; a.Dealt = s.DamageToEnemy;
        a.SelfPops = s.ShockSpent; a.SelfStunned = s.ShockStunned; a.SelfStunMissed = s.ShockStunMissed; a.CowerSpent = s.FearCowerSpent;
        void Cp(long[]? src, long[] dst) { if (src is null) return; for (int i = 0; i < Math.Min(src.Length, dst.Length); i++) dst[i] = src[i]; }
        Cp(s.FearLostCause, a.Cause); Cp(s.FearPrimHist, a.PrimHist); Cp(s.FearLostPrevBits, a.PrevBits); Cp(s.FearLostPrevN, a.PrevN); Cp(s.FearLostPrevAtk, a.PrevAtk);
        Cp(s.FearLostPrevWired, a.PrevWired); Cp(s.FearLostPrevCharge0, a.PrevCharge0); Cp(s.FearLostNine, a.Nine); Cp(s.FearLostNoPrev, a.NoPrev);
        return a;
    }

    static FSt Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new FSt[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new FSt();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // §4 Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第316期 Phase 0 —— 規定のシガ（SW-a）× 代表台 × 波（seed 0..{Seeds - 1}・ドハは規定）");
        Console.WriteLine();
        Console.WriteLine("数はすべて1戦あたり。**失った手番の原因**: 怖気 ＝ 動ける主目標に怖気づいた次の手番（`Scourge`・痺れに乗せる）／ 感電 ＝ 自分の感電が弾けた痺れ ／ ほか ＝ 敵の痺れ・転倒・組み付き・竦み・そのほか。"
            + "原因は手番の頭の印で決める（怖気づいた印 → 感電の痺れの印 → それ以外）。");
        var tot = new FSt();
        var byBoard = new List<(string Name, FSt S)>();
        var bossFire = new List<(string Name, FSt S)>();
        foreach (var (bn, bf) in SBoards())
        {
            Console.WriteLine();
            Console.WriteLine($"## {bn}（{Seats(bf)}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 手番に来た | 動けた | 失った | 怖気 | 感電の痺れ | ほか（痺れ ／ 転倒 ／ 組み付き ／ 竦み ／ そのほか） | 怖気の割合 | 感電して手番を迎えた | 手番の鞭 | 主目標（動けない ／ 感電 ／ 萎縮 ／ どれでもない %） | 割り込み（打った ／ 蓄電が足りない） | 与ダメ（全体 ／ うち割り込み） |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|--:|--:|--:|---|---|---|");
            var bt = new FSt();
            foreach (var w in Waves)
            {
                var s = Measure(bf, w);
                bt.Add(s); tot.Add(s);
                if (w.Boss) bossFire.Add((bn, s));
                Console.WriteLine($"| {w.Name} | {s.Win} | {s.PerB(s.Turns)} | {s.PerB(s.Acted)} | {s.PerB(s.Lost)} | {s.PerB(s.LostCower)} | {s.PerB(s.LostShock)} | "
                    + $"{string.Join(" ／ ", s.Cause.Skip(2).Select(x => s.PerB(x)))} | {Pct(s.LostCower, s.Lost)}% | {Pct(s.Wired, s.Turns)}% | {s.PerB(s.Swings)} | {s.PrimKinds()} | {s.PerB(s.SwFires)} ／ {s.PerB(s.SwNoCharge)} | {s.PerB(s.Dealt)} ／ {s.PerB(s.SwDealt)} |");
            }
            byBoard.Add((bn, bt));
        }

        Console.WriteLine();
        Console.WriteLine("## §4-1 失った手番の内訳（全台 × 全波の和・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("怖気のうち「手番の外の鞭」＝ 号令（ガン）に叩き起こされた鞭など、割り込み以外の手番の外の一振りで怖気づいた回数（怖気は次の手番を飛ばす）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 戦 | 失った | 怖気（%） | 感電の痺れ（%） | ほか（%） | 怖気づいた ／ うち手番の外の鞭 | 手番の外の鞭（割り込みを除く） |");
        Console.WriteLine("|---|--:|--:|---|---|---|---|--:|");
        foreach (var (bn, s) in byBoard.Append(("**全台**", tot)))
            Console.WriteLine($"| {bn} | {s.N:N0} | {s.PerB(s.Lost)} | {s.PerB(s.LostCower)}（{Pct(s.LostCower, s.Lost)}） | {s.PerB(s.LostShock)}（{Pct(s.LostShock, s.Lost)}） | {s.PerB(s.LostOther)}（{Pct(s.LostOther, s.Lost)}） | {s.PerB(s.Cowered)} ／ {s.PerB(s.CowerOut)} | {s.PerB(s.OutSwings)} |");
        Console.WriteLine();
        Console.WriteLine("### 失った手番の直前のシガの手番の形（全台 × 全波）");
        Console.WriteLine();
        Console.WriteLine("直前の鞭 ＝ その失った手番の前に振った、怖気づきうる鞭（手番の鞭 ／ 号令で叩き起こされた鞭など・割り込みは除く）で、前の手番から今の手番までの間に振ったもの（無ければ「直前が鞭でない」）。名目 ＝ 2倍の前の鞭の打点。**ポンのログの形** ＝ 名目が素の攻撃力（9）かつ蓄電 0。主目標の種類はビット（動けない ／ 感電 ／ 萎縮 ／ ボス・重なりあり）。");
        Console.WriteLine();
        Console.WriteLine("| 原因 | 失った | 直前が鞭 | 直前が鞭でない | 名目の平均 | 振り始めに感電（%） | 蓄電 0（%） | **9 の鞭・蓄電なし**（%） | 主目標 動けない ／ 感電 ／ 萎縮 ／ ボス（%） | どれでもない（%） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|--:|");
        string[] kn = { "怖気", "感電の痺れ", "ほか" };
        long[] lostK = { tot.LostCower, tot.LostShock, tot.LostOther };
        for (int k = 0; k < 3; k++)
        {
            long n = tot.PrevN[k];
            long none = 0; for (int i = 0; i < 16; i++) if ((i & 7) == 0) none += tot.PrevBits[k * 16 + i];
            Console.WriteLine($"| {kn[k]} | {lostK[k]:N0} | {n:N0} | {tot.NoPrev[k]:N0} | {Per(tot.PrevAtk[k], n)} | {Pct(tot.PrevWired[k], n)} | {Pct(tot.PrevCharge0[k], n)} | **{Pct(tot.Nine[k], n)}** | "
                + $"{Pct(tot.PrevWith(k, 1), n)} ／ {Pct(tot.PrevWith(k, 2), n)} ／ {Pct(tot.PrevWith(k, 4), n)} ／ {Pct(tot.PrevWith(k, 8), n)} | {Pct(none, n)} |");
        }
        long nineAll = tot.Nine.Sum();
        Console.WriteLine();
        Console.WriteLine($"**ポンのログの形（直前が 9 の鞭・蓄電なし → 次の手番が飛ぶ）の原因**: 怖気 {Pct(tot.Nine[0], nineAll)}% ／ 感電の痺れ {Pct(tot.Nine[1], nineAll)}% ／ ほか {Pct(tot.Nine[2], nineAll)}%（{nineAll:N0} 手番）。");

        Console.WriteLine();
        Console.WriteLine("## §4-2 シガの手番の分布（台ごと・全波の和・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 手番に来た | 動けた | 失った | 感電して手番を迎えた（%） | 割り込み 打った ／ 蓄電が足りない | 与ダメ 手番 ／ 割り込み | 自分の感電が弾けた ／ そのとき痺れた ／ 痺れを外した（50%） |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|---|---|");
        foreach (var (bn, s) in byBoard.Append(("**全台**", tot)))
            Console.WriteLine($"| {bn} | {s.PerB(s.Turns)} | {s.PerB(s.Acted)} | {s.PerB(s.Lost)} | {Pct(s.Wired, s.Turns)} | {s.PerB(s.SwFires)} ／ {s.PerB(s.SwNoCharge)} | {s.PerB(s.Dealt - s.SwDealt)} ／ {s.PerB(s.SwDealt)} | {s.PerB(s.SelfPops)} ／ {s.PerB(s.SelfStunned)} ／ {s.PerB(s.SelfStunMissed)} |");

        Console.WriteLine();
        Console.WriteLine("## §4-3 各版で消える見込み（判定を当てはめて数えるだけ・挙動は変えていない・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("SF-A ＝ 感電の痺れで失った手番。SF-B ＝ 怖気の手番のうち、自分の感電を電気鞭で使い切ったまま新しく感電していなかった（**上限**——残した感電が先に弾ければ消えない）。"
            + "SF-C ＝ 怖気の手番のうち主目標が萎縮していた。SF-D ＝ 怖気の手番のうち主目標が当たる前に感電していた。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 失った | SF-A | SF-B（上限） | SF-C | SF-D |");
        Console.WriteLine("|---|--:|---|---|---|---|");
        foreach (var (bn, s) in byBoard.Append(("**全台**", tot)))
            Console.WriteLine($"| {bn} | {s.PerB(s.Lost)} | {s.PerB(s.LostShock)}（{Pct(s.LostShock, s.Lost)}%） | {s.PerB(s.CowerSpent)}（{Pct(s.CowerSpent, s.Lost)}%） | {s.PerB(s.PrevWith(0, 4))}（{Pct(s.PrevWith(0, 4), s.Lost)}%） | {s.PerB(s.PrevWith(0, 2))}（{Pct(s.PrevWith(0, 2), s.Lost)}%） |");

        Console.WriteLine();
        Console.WriteLine("## §4-4 ボスでのシガ");
        Console.WriteLine();
        Console.WriteLine("主目標の割合は手番の鞭に占める割合。火力 ＝ 1ターンあたりの敵への与ダメ（全体 ／ うちシガ）と勇者の自前の回復（1ターンあたり）の比。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 倒しT | 負けT | シガの寿命（倒れた戦の割合） | 主目標がボス（%） | うち萎縮（%） | うち感電（%） | シガの与ダメ /戦 | 1ターンの与ダメ 全体 ／ シガ | 勇者の回復 /T | 全体 ÷ 回復 |");
        Console.WriteLine("|---|--:|--:|--:|---|--:|--:|--:|--:|---|--:|--:|");
        var shield = Measure(Playtest("試遊・感電 光の盾"), Waves[0]);
        foreach (var (bn, s) in bossFire.Append(("（参考）光の盾（クビ）", shield)))
        {
            long boss = 0, bd = 0, bs = 0; for (int i = 8; i < 16; i++) { boss += s.PrimHist[i]; if ((i & 4) != 0) bd += s.PrimHist[i]; if ((i & 2) != 0) bs += s.PrimHist[i]; }
            Console.WriteLine($"| {bn} | {s.Win} | {s.WinTurn} | {s.LossTurn} | {Per(s.ShigaDeathT, s.ShigaDeathN)}（{Pct(s.ShigaDeathN, s.N)}%） | {Pct(boss, s.Swings)} | {Pct(bd, boss)} | {Pct(bs, boss)} | {s.PerB(s.Dealt)} | "
                + $"{Per(s.TeamDealt, s.BattleTurns)} ／ {Per(s.Dealt, s.BattleTurns)} | {Per(s.BossHeal, s.BattleTurns)} | {(s.BossHeal == 0 ? "—" : (s.TeamDealt / (double)s.BossHeal).ToString("F2"))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5-2 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void Boards(string[] extra)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Single.Concat(extra.Select(SV)).ToArray();
        Console.WriteLine($"# 第316期 §5-2 代表台 × 波 × 版（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"版: {string.Join(" ／ ", vers.Select(v => v.Tag))}。差は規定から。対照 ＝ シガ → ドルガ（同じ席・どれかの版で 50% 以上のセルだけ）。**組み合わせの門（§3-1）** ＝ 単独の版のうち2つ以上が規定から +5pt 以上（ボスは +3pt）動いたセル。");
        var gate = new List<string>();
        var grid = new List<string>();
        foreach (var (bn, bf) in SBoards())
        {
            Console.WriteLine();
            Console.WriteLine($"## {bn}（{Seats(bf)}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | " + string.Join(" | ", vers.Select(v => v.Tag)) + " | ドルガ | 倒しT | 負けT | 寿命 |");
            Console.WriteLine("|---|" + string.Concat(vers.Select(_ => "--:|")) + "--:|---|---|---|");
            var detail = new List<(S287.Wave W, FSt[] V)>();
            foreach (var w in Waves)
            {
                var v = vers.Select(x => Measure(SwapId(bf, "shiga", x.Def), w)).ToArray();
                detail.Add((w, v));
                string dolga = v.Any(x => x.WinRate >= 50) ? Measure(SwapId(bf, "shiga", UnitCatalog.DolgaH395), w).Win : "—";
                Console.WriteLine($"| {w.Name} | {string.Join(" | ", v.Select((x, i) => i == 0 ? x.Win : $"{x.Win}（{Sg(x.WinRate - v[0].WinRate)}）"))} | {dolga} | "
                    + $"{string.Join(" ／ ", v.Select(x => x.WinTurn))} | {string.Join(" ／ ", v.Select(x => x.LossTurn))} | {string.Join(" ／ ", v.Select(x => x.Life))} |");
                double th = w.Boss ? 3 : 5, gth = w.Boss ? 5 : 10;
                var up = Enumerable.Range(1, Math.Min(4, v.Length - 1)).Where(i => v[i].WinRate - v[0].WinRate >= th).Select(i => vers[i].Tag).ToList();
                if (up.Count >= 2) gate.Add($"{bn} × {w.Name}: {string.Join("・", up)}");
                for (int i = 1; i < v.Length; i++) if (Math.Abs(v[i].WinRate - v[0].WinRate) >= gth) grid.Add($"{vers[i].Tag}: {bn} × {w.Name}（{Sg(v[i].WinRate - v[0].WinRate)}）");
            }
            Console.WriteLine();
            Console.WriteLine("シガ（1戦あたり）: 手番に来た ／ 動けた ／ 失った（怖気 ／ 感電の痺れ ／ ほか）・感電して迎えた %・怖気づかなかった（感電して ／ 感電した敵 ／ 萎縮した敵）・与ダメ（手番 ／ 割り込み）・主目標（動けない ／ 感電 ／ 萎縮 ／ どれでもない %）・自分の感電が弾けた ／ 痺れた");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 来た ／ 動けた ／ 失った | 怖気 ／ 感電 ／ ほか | 感電して迎えた | 怖気づかなかった | 与ダメ 手番 ／ 割り込み | 主目標 | 弾けた ／ 痺れた | 1T の与ダメ 全体 ／ シガ ／ 回復 |");
            Console.WriteLine("|---|---|---|---|--:|---|---|---|---|---|");
            foreach (var (w, v) in detail)
                for (int i = 0; i < v.Length; i++)
                {
                    var s = v[i];
                    Console.WriteLine($"| {w.Name} | {vers[i].Tag} | {s.PerB(s.Turns)} ／ {s.PerB(s.Acted)} ／ {s.PerB(s.Lost)} | {s.PerB(s.LostCower)} ／ {s.PerB(s.LostShock)} ／ {s.PerB(s.LostOther)} | {Pct(s.Wired, s.Turns)}% | "
                        + $"{s.PerB(s.WireSpared)} ／ {s.PerB(s.ShockSpared)} ／ {s.PerB(s.DauntSpared)} | {s.PerB(s.Dealt - s.SwDealt)} ／ {s.PerB(s.SwDealt)} | {s.PrimKinds()} | {s.PerB(s.SelfPops)} ／ {s.PerB(s.SelfStunned)} | "
                        + $"{Per(s.TeamDealt, s.BattleTurns)} ／ {Per(s.Dealt, s.BattleTurns)} ／ {(w.Boss ? Per(s.BossHeal, s.BattleTurns) : "—")} |");
                }
        }
        Console.WriteLine();
        Console.WriteLine("## §3-1 組み合わせの門");
        Console.WriteLine();
        if (gate.Count == 0) Console.WriteLine("**該当なし**。"); else foreach (var g in gate) Console.WriteLine($"- {g}");
        Console.WriteLine();
        Console.WriteLine("## §5-4 格子の門（規定から ±10pt 以上・ボスは ±5pt 以上）");
        Console.WriteLine();
        if (grid.Count == 0) Console.WriteLine("**該当なし**（格子は回さない）。"); else foreach (var g in grid) Console.WriteLine($"- {g}");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5-3 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(string[] extra)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuildsH316();
        int nw = EnemyCatalog.Stages.Count;
        var vers = Single.Concat(extra.Select(SV)).ToArray();
        double[,] Grid(UnitDef d)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = SwapId(rows[ri].F, "shiga", d);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }
        var b = Grid(vers[0].Def);
        Console.WriteLine("# 第316期 §5-3 `compare` 64 行 × 版（seed 0..199・基準は規定のシガ）");
        Console.WriteLine();
        Console.WriteLine($"シガのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "shiga")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Tag}");
            Console.WriteLine();
            PrintCompare(rows, b, Grid(vers[vi].Def), "shiga");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5-4 格子（固定枠 シガ ＋ カタ ＋ ソム・探索枠2・席 120・第293期のボスの格子の作り）
    // ---------------------------------------------------------------------------------
    static void Grid(string waveKey, string ver, string with = "")
    {
        var w = WaveOf(waveKey);
        var v = SV(ver);
        var fixedU = new[] { UnitCatalog.ShigaSWa, UnitCatalog.KataH324, UnitCatalog.SomH317 };
        // 候補 ＝ 第287期の候補（感電の駒 ＋ 第283期の寿命側 ＋ ヒーラー）＋ トウ。固定してあった過去の版はすべていまの規定に替える（Id で引く）。ヒーラーは組に 1 枚まで（ポンの憲法）。
        UnitDef Now(UnitDef d) => UnitCatalog.All.Select(PinDS316).FirstOrDefault(x => x.Id == d.Id) ?? d;
        var heal = S287.Heal.Select(d => d.Id).ToHashSet();
        var pool = S287.Pool.Select(Now).Append(UnitCatalog.TouH324).Where(d => d.Id is not ("shiga" or "kata" or "som")).GroupBy(d => d.Id).Select(g => g.First()).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var combos = new List<UnitDef[]>();
        for (int i = 0; i < pool.Length; i++) for (int j = i + 1; j < pool.Length; j++)
            if (!(heal.Contains(pool[i].Id) && heal.Contains(pool[j].Id))) combos.Add(new[] { pool[i], pool[j] });
        // 絞り込み（参考）: `<Id>` ならその駒を含む組だけ、`no<Id>` なら含まない組だけ
        if (with.Length > 0) combos = combos.Where(c => with.StartsWith("no") ? c.All(d => d.Id != with[2..]) : c.Any(d => d.Id == with)).ToList();
        var boards = new List<UnitDef[]>();
        foreach (var c in combos) boards.AddRange(B283.Perms(fixedU.Concat(c).ToArray()));
        const int cut = 20;
        var res = new (int B, int V, int Db, int Dv)?[boards.Count];
        int Wins(Formation f, int n) { int c = 0; for (int sd = 0; sd < n; sd++) if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) c++; return c; }
        Parallel.For(0, boards.Count, i =>
        {
            var f0 = Pin316(B283.Seat(boards[i]));
            var fv = SwapId(f0, "shiga", v.Def);
            if (Wins(f0, cut) < 5 && Wins(fv, cut) < 5) return;
            int wb = Wins(f0, Seeds), wv = Wins(fv, Seeds);
            int db = wb * 2 >= Seeds ? Wins(SwapId(f0, "shiga", UnitCatalog.DolgaH395), Seeds) : -1;
            int dv = wv * 2 >= Seeds ? (db >= 0 ? db : Wins(SwapId(f0, "shiga", UnitCatalog.DolgaH395), Seeds)) : -1;
            res[i] = (wb, wv, db, dv);
        });
        var all = res.Where(r => r is not null).Select(r => r!.Value).ToList();
        bool Half(int x) => x * 2 >= Seeds;
        bool Need(int x, int d) => Half(x) && d >= 0 && d * 2 <= x;
        Console.WriteLine($"# 第316期 §5-4 格子 —— {w.Name} × 規定 ／ {v.Tag}{(with.Length > 0 ? $"・絞り込み {with}" : "")}（固定枠 シガ ＋ カタ ＋ ソム・探索枠2（候補 {pool.Length} 枚: {string.Join("・", pool.Select(Short))}・ヒーラーは 1 枚まで）{combos.Count} 組・席 120 ＝ {boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{cut - 1}（どちらかで 5 勝以上・{all.Count:N0} 台）→ seed 0..{Seeds - 1}。**シガが要る台** ＝ 50% 以上 かつ シガ → ドルガで半分以下。所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
        Console.WriteLine();
        Console.WriteLine($"| 段 | 規定 | {v.Tag} |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 勝率 ≧ 50% | {all.Count(r => Half(r.B)):N0} | {all.Count(r => Half(r.V)):N0} |");
        Console.WriteLine($"| **シガが要る** | {all.Count(r => Need(r.B, r.Db)):N0} | {all.Count(r => Need(r.V, r.Dv)):N0} |");
        Console.WriteLine($"| 勝率 ≧ 90% | {all.Count(r => r.B * 10 >= Seeds * 9):N0} | {all.Count(r => r.V * 10 >= Seeds * 9):N0} |");
        Console.WriteLine($"| 版 − 規定 の平均（pt） | — | {all.Average(r => 100.0 * (r.V - r.B) / Seeds):+0.0;-0.0} |");
        Console.WriteLine($"| 版が +10pt 以上 ／ −10pt 以下 | — | {all.Count(r => r.V - r.B >= Seeds / 10):N0} ／ {all.Count(r => r.V - r.B <= -Seeds / 10):N0} |");
    }

    // ---------------------------------------------------------------------------------
    // 台本の並び
    // ---------------------------------------------------------------------------------
    static void Memo(string boardPart, string wave, int seed, string ver, bool log = false)
    {
        var (name, f0) = SBoards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var v = SV(ver);
        var f = SwapId(f0, "shiga", v.Def);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        int shiga = p.First(u => u.Def.Id == "shiga").InstanceId;
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# shiga316 memo —— {name} × {v.Tag} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        if (log) { foreach (var l in r.Log.Take(400)) Console.WriteLine(new string(' ', l.Indent * 2) + l.Text); Console.WriteLine("```"); return; }
        int lastT = -1;
        foreach (var x in r.Events)
        {
            bool mine = x.ActorId == shiga || x.TargetId == shiga;
            if (!mine) continue;
            if (x.Kind is not (BattleEventKind.Attack or BattleEventKind.ShockGauge or BattleEventKind.Stun or BattleEventKind.LiveWire or BattleEventKind.ShockSpent or BattleEventKind.StatusGain or BattleEventKind.Death)) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.PartnerId is int pi ? $"  相手 {N(pi)}" : "")}{(x.Reaction ? "  Reaction" : "")}");
        }
        Console.WriteLine("```");
    }

    /// <summary>台本の例を探す（代表台 × 波 × seed 0..199・規定）。noprev ＝ 怖気で失った手番の直前が鞭でなかった戦。</summary>
    static void Find(string what)
    {
        foreach (var (bn, bf) in SBoards())
            foreach (var w in Waves)
                for (int sd = 0; sd < Seeds; sd++)
                {
                    var r = BattleEngine.Run(BattleEngine.Materialize(bf, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    if (!r.TallyByUnit.TryGetValue("shiga", out var t)) continue;
                    bool hit = what == "noprev" ? (t.FearLostNoPrev?[0] ?? 0) > 0 : false;
                    if (!hit) continue;
                    Console.WriteLine($"{bn} × {w.Name} × seed {sd}");
                    return;
                }
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly PropertyInfo TurnProp = typeof(BattleContext).GetProperty("Turn") ?? throw new InvalidOperationException("Turn が見つからない");
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit が見つからない");
    static readonly FieldInfo GiftQueue = typeof(BattleContext).GetField("_giftQueue", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_giftQueue が見つからない");
    static readonly FieldInfo InGift = typeof(BattleContext).GetField("_inGift", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_inGift が見つからない");
    static readonly FieldInfo PushWin = typeof(BattleContext).GetField("_pushWin", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_pushWin が見つからない");
    static readonly FieldInfo Forced = typeof(BattleContext).GetField("_forcedTarget", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_forcedTarget が見つからない");
    static readonly MethodInfo Trigger = typeof(BattleContext).GetMethod("ShockTrigger", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("ShockTrigger が見つからない");
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();
    static int QueueCount(BattleContext ctx) => ((System.Collections.ICollection)GiftQueue.GetValue(ctx)!).Count;
    static UnitState? QueueLastTo(BattleContext ctx)
    {
        UnitState? last = null;
        foreach (var o in (System.Collections.IEnumerable)GiftQueue.GetValue(ctx)!) last = (UnitState)o.GetType().GetField("Item2")!.GetValue(o)!;
        return last;
    }
    /// <summary>ドハの肩代わりの窓に直に書く（窓の帳簿 `_pushWin` ＝ ドハ → 味方 → 実額）。</summary>
    static void Shoulder(BattleContext ctx, UnitState doha, UnitState from, int amt)
    {
        var all = (Dictionary<int, Dictionary<int, int>>)PushWin.GetValue(ctx)!;
        if (!all.TryGetValue(doha.InstanceId, out var w)) all[doha.InstanceId] = w = new Dictionary<int, int>();
        w[from.InstanceId] = w.GetValueOrDefault(from.InstanceId) + amt;
    }

    /// <summary>盤面を直に組む（`Run` を通さない・乱数は seed 0）。敵は <paramref name="foes"/>（倍率なし）。</summary>
    static BattleContext Ctx(Formation pl, Formation foes, out List<UnitState> p, out List<UnitState> e, bool verbose = true, int seed = 0)
    {
        var ctx = new BattleContext(seed, verbose);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(foes, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnProp.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Wall => UnitCatalog.DolgaH395.WithStats(5000, 1);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# shiga316 check —— 第316期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0: DP-s
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Ban, front3: UnitCatalog.Gald, center: UnitCatalog.DohaDPs, back1: UnitCatalog.Zan, back3: UnitCatalog.Sora), Formation.Build(front1: Wall), out var p, out _);
            var doha = p.First(u => u.Def.Id == "doha"); var ban = p.First(u => u.Def.Id == "ban"); var gald = p.First(u => u.Def.Id == "gald"); var zan = p.First(u => u.Def.Id == "zan"); var sora = p.First(u => u.Def.Id == "sora");
            Shoulder(ctx, doha, ban, 50); Shoulder(ctx, doha, gald, 40); Shoulder(ctx, doha, zan, 20); Shoulder(ctx, doha, sora, 10);
            bool ok = ctx.SharePushTurn(doha);
            Expect("DP-s: 窓の最多が殴らない駒（バン 50）・次が支援拒否（ガルド 40）なら飛ばして次に多く肩代わりした殴る味方（ザン 20）へ", ok && QueueLastTo(ctx) == zan && Tal(ctx, "doha").PushSkipNoStrike == 1 && Tal(ctx, "ban").PushSkipped == 1,
                $"相手 {(QueueLastTo(ctx) is { } q ? Short(q.Def) : "—")}・飛ばした（殴らない ／ 支援拒否）{Tal(ctx, "doha").PushSkipNoStrike} ／ {Tal(ctx, "doha").PushSkipStoic}");
            TurnProp.SetValue(ctx, 2);
            Shoulder(ctx, doha, gald, 40); Shoulder(ctx, doha, ban, 30);
            bool ok2 = ctx.SharePushTurn(doha);
            Expect("DP-s: 支援拒否（ガルド）と殴らない駒（バン）しかいなければ送り出さない（殴る＝偽）", !ok2 && QueueCount(ctx) == 1 && Tal(ctx, "doha").PushSkipStoic == 1 && Tal(ctx, "doha").PushNoEligible == 1,
                $"支援拒否 {Tal(ctx, "doha").PushSkipStoic}・相手なし {Tal(ctx, "doha").PushNoEligible}");
            TurnProp.SetValue(ctx, 3);
            bool ok3 = ctx.SharePushTurn(doha);
            Expect("DP-s: 窓が空なら殴る（偽）", !ok3 && Tal(ctx, "doha").PushZero == 2, $"PushZero {Tal(ctx, "doha").PushZero}");
        }
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Zan, center: UnitCatalog.DohaDPs), Formation.Build(front1: Wall), out var p, out _);
            var doha = p.First(u => u.Def.Id == "doha"); var zan = p.First(u => u.Def.Id == "zan");
            ctx.Whet(doha, 30);
            int bon = doha.AtkBonus, z0 = zan.CurrentAttack;
            Shoulder(ctx, doha, zan, 10);
            InGift.SetValue(ctx, true);
            bool inGift = ctx.SharePushTurn(doha);
            InGift.SetValue(ctx, false);
            bool ok = ctx.SharePushTurn(doha);
            Expect("DP-s: 上乗せがすべて移り、ドハは素の攻撃力に戻る", ok && doha.AtkBonus == 0 && doha.CurrentAttack == doha.Def.Attack && zan.CurrentAttack == z0 + bon && bon > 0,
                $"ドハ {doha.CurrentAttack}・ザン {z0} → {zan.CurrentAttack}（上乗せ {bon}）");
            Expect("DP-s: 送り出された手番（ギフト）の中では背を押さない（窓は閉じない）", !inGift && Tal(ctx, "doha").PushInGift == 1, $"ギフト中 {Tal(ctx, "doha").PushInGift}");
        }
        {
            bool same = UnitCatalog.DohaDPs.Traits.SequenceEqual(UnitCatalog.DohaH316.Traits.Concat(new[] { TraitId.SharePush, TraitId.SharePushPower, TraitId.SharePushStrikers }))
                && UnitCatalog.DohaDPs.MaxHp == UnitCatalog.DohaH316.MaxHp && UnitCatalog.DohaDPs.Attack == UnitCatalog.DohaH316.Attack && UnitCatalog.DohaDPs.Speed == UnitCatalog.DohaH316.Speed
                && UnitCatalog.DohaDPs.Actions is { Count: 1 } && UnitCatalog.DohaDPs.Actions[0].Kind == ActionKind.Skill && !UnitCatalog.All.Select(PinDS316).Contains(UnitCatalog.DohaDPs);
            Expect("DP-s の定義（規定 ＋ 背を押す ＋ 上乗せを渡す ＋ 殴る仲間だけ・手番は術・`All` の外）", same, "");
        }

        // ---- 本体: SF-A 〜 SF-D
        // SF-A: 自分の感電が弾けても痺れない（放電はする）。カタ（S3 の痺れの保持者）を置き、シガの感電を弾けさせる。
        {
            foreach (var (tag, sdef, expectStun) in new[] { ("規定", UnitCatalog.ShigaSWa, true), ("SF-A", UnitCatalog.ShigaSFa, false) })
            {
                int stunned = 0, popped = 0, dis = 0, n = 0;
                for (int sd = 0; sd < 40; sd++)
                {
                    var ctx = new BattleContext(sd, false);
                    var p = BattleEngine.Materialize(Formation.Build(front1: sdef, front3: UnitCatalog.KataH324, center: UnitCatalog.GolmH344), BattleContext.PlayerTeam);
                    var e = BattleEngine.Materialize(Formation.Build(front1: Wall), BattleContext.EnemyTeam, EnemyScaleRule.None);
                    foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
                    foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
                    TurnProp.SetValue(ctx, 1);
                    var sh = p.First(u => u.Def.Id == "shiga"); var golm = p.First(u => u.Def.Id == "golm");
                    int g0 = golm.Hp;
                    ctx.MarkShock(sh, e[0]);
                    Trigger.Invoke(ctx, new object?[] { sh, e[0], 0, null });
                    n++;
                    if (sh.RawCounter(StatusKeys.Shock) == 0) popped++;
                    if (sh.RawCounter(StatusKeys.Stun) > 0) stunned++;
                    if (golm.Hp < g0) dis++;
                }
                Expect($"SF-A: {tag} のシガの感電が弾けると {(expectStun ? "半々で痺れる（S3）" : "痺れない")}・放電はする・感電は消える", popped == n && dis == n && (expectStun ? stunned > 0 && stunned < n : stunned == 0),
                    $"{n} 回: 弾けた {popped}・痺れた {stunned}・隣（ゴルム）へ放電 {dis}");
            }
        }
        // SF-B: 手番の鞭の後も自分の感電が残る・移す方は今のまま
        {
            foreach (var (tag, sdef, keep) in new[] { ("規定", UnitCatalog.ShigaSWa, false), ("SF-B", UnitCatalog.ShigaSFb, true) })
            {
                var ctx = Ctx(Formation.Build(front1: sdef), Formation.Build(front1: Wall, front3: Wall), out var p, out var e);
                var sh = p[0];
                ctx.MarkShock(sh, e[0]);
                var prevActor = typeof(BattleContext).GetProperty("TurnActor")!;
                prevActor.SetValue(ctx, sh);
                Forced.SetValue(ctx, e[0]);
                ctx.PerformAttack(sh);
                prevActor.SetValue(ctx, null);
                bool left = sh.RawCounter(StatusKeys.Shock) > 0;
                int marked = e.Count(x => x.RawCounter(StatusKeys.Shock) > 0);
                Expect($"SF-B: {tag} の手番の電気鞭の後、自分の感電が{(keep ? "残る" : "消える")}・当たった敵に感電を移す", left == keep && marked >= 1 && Tal(ctx, "shiga").WiredSwings == 1,
                    $"自分の感電 {(left ? "残" : "消")}・敵の感電 {marked}・残した {Tal(ctx, "shiga").WireHeld}");
            }
        }
        // SF-C: 萎縮した敵を優先し、2倍で入り、主目標でも怖気づかない
        {
            foreach (var (tag, sdef, isC) in new[] { ("規定", UnitCatalog.ShigaSWa, false), ("SF-C", UnitCatalog.ShigaSFc, true) })
            {
                int prefer = 0, n = 0, doubled = 0, cowered = 0, spared = 0;
                for (int sd = 0; sd < 20; sd++)
                {
                    var ctx = Ctx(Formation.Build(front1: sdef), Formation.Build(front1: Wall, front3: Wall), out var p, out var e, verbose: false, seed: sd);
                    var sh = p[0];
                    TurnProp.SetValue(ctx, 1);
                    e[1].SetCounter(StatusKeys.Daunted, 1);
                    typeof(BattleContext).GetProperty("TurnActor")!.SetValue(ctx, sh);
                    int h0 = e[0].Hp, h1 = e[1].Hp;
                    ctx.PerformAttack(sh);
                    typeof(BattleContext).GetProperty("TurnActor")!.SetValue(ctx, null);
                    n++;
                    var t = Tal(ctx, "shiga");
                    if (h1 - e[1].Hp > h0 - e[0].Hp) prefer++;
                    doubled += (int)t.WhipDoubled; cowered += (int)t.WhipCowered; spared += (int)t.WhipDauntSpared;
                    sh.SetCounter(StatusKeys.Stun, 0);
                }
                Expect($"SF-C: {tag}: 萎縮した敵を{(isC ? "優先して主目標にし、2倍で入り、怖気づかない（「怖くない」を数える）" : "動ける敵と同じに扱う（2倍にならない・怖気づくことがある）")}",
                    isC ? prefer == n && doubled == n && cowered == 0 && spared == n : spared == 0 && cowered > 0 && prefer < n,
                    $"{n} 回: 萎縮した敵の方に多く入った {prefer}・2倍 {doubled}・怖気 {cowered}・怖くない {spared}");
            }
        }
        // SF-D: 感電した敵を主目標にしても怖気づかない・見せしめの悲鳴は広げない
        {
            foreach (var (tag, sdef, isD) in new[] { ("規定", UnitCatalog.ShigaSWa, false), ("SF-D", UnitCatalog.ShigaSFd, true) })
            {
                var ctx = Ctx(Formation.Build(front1: sdef), Formation.Build(front1: Wall, front3: Wall, center: Wall), out var p, out var e);
                var sh = p[0];
                ctx.MarkShock(e[0], e[1]);
                typeof(BattleContext).GetProperty("TurnActor")!.SetValue(ctx, sh);
                Forced.SetValue(ctx, e[0]);
                ctx.PerformAttack(sh);
                typeof(BattleContext).GetProperty("TurnActor")!.SetValue(ctx, null);
                var t = Tal(ctx, "shiga");
                bool cowed = e.Any(x => x.RawCounter(StatusKeys.Cowed) > 0);
                bool ev = ctx.Events.Any(x => x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.Unafraid && x.Amount == 2);
                Expect($"SF-D: {tag}: 感電していた主目標を打って{(isD ? "怖気づかない（「怖くない」が台本に出る）" : "怖気づく")}・2倍で入る・悲鳴は上がらない",
                    (isD ? t.WhipCowered == 0 && t.WhipShockSpared == 1 && sh.RawCounter(StatusKeys.Stun) == 0 && ev : t.WhipCowered == 1 && sh.RawCounter(StatusKeys.Stun) > 0 && !ev) && t.WhipDoubledShock >= 1 && !cowed,
                    $"怖気 {t.WhipCowered}・怖くない {t.WhipShockSpared}・2倍（感電）{t.WhipDoubledShock}・竦んだ敵 {(cowed ? "あり" : "なし")}");
            }
        }
        // 計数: 失った手番の原因（怖気 → 次の手番が飛ぶ）
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.ShigaSWa), Formation.Build(front1: Wall), out var p, out var e);
            var sh = p[0];
            ctx.TakeTurn(sh);   // 動ける敵を打って怖気づく
            TurnProp.SetValue(ctx, 2);
            var o = ctx.TakeTurn(sh);   // 失う
            var t = Tal(ctx, "shiga");
            Expect("計数: 動ける敵を打った手番の次が「怖気」で失った手番に数えられ、直前の手番の形（名目 9・蓄電 0・主目標どれでもない）が控えられる",
                o == TurnOutcome.Stalled && t.FearLostCause?[0] == 1 && t.FearLostNine?[0] == 1 && t.FearLostPrevBits?[0] == 1 && t.FearSwings == 1,
                $"原因 {string.Join(",", t.FearLostCause ?? Array.Empty<long>())}・9 の鞭 {t.FearLostNine?[0]}");
        }
        // 規定のシガ ／ 版のない戦は1ビットも動かない: verbose の有無・版の札を持たない戦の compare は `docs/balance.md` と比べる（ここでは verbose の有無）
        {
            int diff = 0, n = 0;
            foreach (var (bn, bf) in SBoards())
                foreach (var v in Single)
                    foreach (var w in Waves.Take(6))
                        for (int sd = 0; sd < 6; sd++)
                        {
                            var f = SwapId(bf, "shiga", v.Def);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns) diff++;
                        }
            Expect("verbose の有無で勝敗 ／ 決着T が同じ（代表台 × 版 × 6 波 × seed 0..5）", diff == 0, $"{n} 戦中 {diff} 戦が違う");
        }
        // PickOne ／ Roll を新たに使っていない（第316期の区画）
        {
            string src = File.ReadAllText("BattleCore/BattleEngine.cs");
            int a = src.IndexOf("// ---- 第316期 —— シガの手番を失った原因", StringComparison.Ordinal);
            int b = a < 0 ? -1 : src.IndexOf("void FearTail", a, StringComparison.Ordinal);
            string region = a < 0 || b < 0 ? "" : src[a..b];
            string tr = File.ReadAllText("BattleCore/Traits.cs");
            int c = tr.IndexOf("// 第316期（SF-D）", StringComparison.Ordinal);
            string reg2 = c < 0 ? "" : tr.Substring(c, 400);
            Expect("第316期の区画（engine の計数 ／ SF-D の判定）に `PickOne` も `Roll` も無い", region.Length > 0 && reg2.Length > 0 && !region.Contains("PickOne") && !region.Contains("Roll(") && !reg2.Contains("Roll("), $"区画 {region.Length} 字");
        }
        // 版の定義
        {
            var vs = new[] { UnitCatalog.ShigaSFa, UnitCatalog.ShigaSFb, UnitCatalog.ShigaSFc, UnitCatalog.ShigaSFd };
            var ids = new[] { TraitId.FearNoStun, TraitId.LiveWireHold, TraitId.ScourgeDaunt, TraitId.ScourgeShockSpare };
            bool ok = vs.Select((d, i) => d.Traits.SequenceEqual(UnitCatalog.ShigaSWa.Traits.Append(ids[i])) && d.MaxHp == 52 && d.Attack == 9 && d.Speed == 3 && !UnitCatalog.All.Select(PinDS316).Contains(d)).All(x => x);
            Expect("SF-A 〜 SF-D は規定のシガ（52 ／ 9 ／ 3）の末尾に札を1枚足しただけ・`All` の外", ok, "");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
