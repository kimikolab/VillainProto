using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S379 = Sh379Diag;

// =====================================================================================
// sh380 —— 第380期【軽い期】段0: ムグの「撒き全」を規定に ／ 本体: 死の台の席の探索（リィカ ／ ヴェル ／ ムグ ＋ 2枠）。
// 指示書は design/PHASE380_DEATH_SEATS_SPEC.md（＋ 期の途中のポンの補足: ヴェルはヒーラー・本体の2枠にほかのヒーラーを入れない・ヒーラー2枚の台は参考の表）
// 報告は design/PHASE380_DEATH_SEATS.md。
//
//     dotnet run --project BattleSim -c Release 0 sh380 stage0    # §2-3: `compare` 64 行 旧（`Pin379`）→ 新（撒き全）・ムグのいる代表台 5 × 5 波 旧 → 新（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh380 seats     # §3: 席の探索（段1 → 段2 → 段3）・本体（ヒーラー1枚 ＝ ヴェル）と参考（ヒーラー2枚）
//     dotnet run --project BattleSim -c Release 0 sh380 check     # 自己検査
//
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p380/predict.md`）。探索の台は器具の中だけ（試遊の台に足す1位を除く）。
// =====================================================================================
static partial class Sh380Diag
{
    internal const string PredictFile = ".tmp/p380/predict.md";
    const string Tag = "第380期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "stage0" or "seats" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh380: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "stage0": Stage0(); return;
            case "seats": Seats(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh380: モードは stage0 / seats / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long Xv(S377.B b, string k) => b.X.GetValueOrDefault(k);
    static S377.B M(Formation f, I.W[] ws, int seeds) => S377.Measure(f, ws, seeds, S379.Extra);

    // ---------------------------------------------------------------------------------
    // 母集団
    // ---------------------------------------------------------------------------------
    /// <summary>固定の3枚（規定の駒・段0 の後）。</summary>
    internal static readonly UnitDef[] Fixed = { UnitCatalog.Rica, UnitCatalog.VelH381, UnitCatalog.Mug };   // 第382期: ヴェルは第381期までの規定（`VelH381`）に固定
    static UnitDef[]? _heal;
    /// <summary>ヒーラー（第283期の機械的な定義 ＝ 味方に回復か破片を2回以上書ける駒・`Boss283Diag.HealPool`・1回だけ数えて控える）。</summary>
    internal static UnitDef[] Healers => _heal ??= Boss283Diag.HealPool;
    internal static bool IsHealer(UnitDef d) => Healers.Any(h => h.Id == d.Id);
    /// <summary>2枠の候補: ロスターからリィカ ／ ヴェル ／ ムグとツギ ／ リリを除いた駒。</summary>
    internal static UnitDef[] Cands => UnitCatalog.All.Where(d => !Fixed.Any(x => x.Id == d.Id) && d.Id is not "tsugi" and not "lili").ToArray();
    /// <summary>本体の組（2枠ともヒーラーでない ＝ 台のヒーラーはヴェルの1枚）と、参考の組（2枠のどちらか1枚がヒーラー ＝ 台にヒーラー2枚）。</summary>
    internal static (UnitDef A, UnitDef B)[] Pairs(bool reference)
    {
        var c = Cands;
        var res = new List<(UnitDef, UnitDef)>();
        for (int i = 0; i < c.Length; i++)
            for (int j = i + 1; j < c.Length; j++)
            {
                int h = (IsHealer(c[i]) ? 1 : 0) + (IsHealer(c[j]) ? 1 : 0);
                if (reference ? h == 1 : h == 0) res.Add((c[i], c[j]));
            }
        return res.ToArray();
    }
    static readonly int[][] Perms = Permutations(5).ToArray();
    static IEnumerable<int[]> Permutations(int n)
    {
        if (n == 1) { yield return new[] { 0 }; yield break; }
        foreach (var p in Permutations(n - 1))
            for (int k = 0; k < n; k++) { var q = p.ToList(); q.Insert(k, n - 1); yield return q.ToArray(); }
    }
    /// <summary>5枚を並び <paramref name="perm"/>（駒 i を席 perm[i] へ・席 0 前1 ／ 1 前3 ／ 2 中央 ／ 3 後1 ／ 4 後3）に置く。</summary>
    internal static Formation Board(UnitDef[] five, int[] perm)
    {
        var f = new Formation();
        for (int i = 0; i < 5; i++) f[perm[i]] = five[i];
        return f;
    }
    static string SeatStr(Formation f) => string.Join(" ／ ", Enumerable.Range(0, 5).Select(s => f[s] is { } d ? S307.Short(d) : "—"));

    // ---------------------------------------------------------------------------------
    // 速い計数（勝ち ／ 倒れた編成の駒）
    // ---------------------------------------------------------------------------------
    sealed record Score(double Mean, double Deaths, double[] Win);
    static Score Quick(Formation f, I.W[] ws, int seeds)
    {
        var win = new double[ws.Length]; long deaths = 0, n = 0;
        for (int wi = 0; wi < ws.Length; wi++)
        {
            int w = 0;
            for (int s = 0; s < seeds; s++)
            {
                var (r, p, _) = I.Raw(f, ws[wi], s, census: false);
                if (r.PlayerWon) w++;
                deaths += p.Count(u => u.LastDeathTurn > 0); n++;
            }
            win[wi] = 100.0 * w / seeds;
        }
        return new Score(win.Average(), (double)deaths / n, win);
    }
    static readonly I.W[] W1 = { I.Boss, I.Guard, I.Bat };
    static readonly I.W[] W2 = { I.Boss, I.Guard, I.Bat, I.M5 };
    static IOrderedEnumerable<T> Rank<T>(IEnumerable<T> xs, Func<T, Score> sc) => xs.OrderByDescending(x => sc(x).Mean).ThenBy(x => sc(x).Deaths);

    sealed record Cand(UnitDef A, UnitDef B, int[] Perm, Formation F);

    internal const int S1Seeds = 4, S2Perms = 5;
    /// <summary>
    /// 段1 → 段2 の探索（本体 ／ 参考で共通）。段1: 全組 × 120 並び × ボス ／ 近衛 ／ 大隊 × seed 0..3。段2: **全組** × その組の段1 の上位5並び × seed 0..19 × 4 波。
    /// 指示書の目安（段1 seed 0..1・段2 は段1 の上位 50 組）より広い——目安どおりだと 70 秒で終わり、段1 の 2 seed では上位の組の多くが 100% で並んで「倒れた数」だけで組が切られるため（決めたこと）。
    /// </summary>
    static (List<(Cand C, Score S)> S1Best, List<(Cand C, Score S)> S2) Search((UnitDef A, UnitDef B)[] pairs)
    {
        var cands = pairs.SelectMany(p => Perms.Select(pm => { var five = Fixed.Concat(new[] { p.A, p.B }).ToArray(); return new Cand(p.A, p.B, pm, Board(five, pm)); })).ToArray();
        var s1 = new Score[cands.Length];
        Parallel.For(0, cands.Length, k => s1[k] = Quick(cands[k].F, W1, S1Seeds));
        var byPair = Enumerable.Range(0, cands.Length).GroupBy(k => (cands[k].A.Id, cands[k].B.Id))
            .Select(g => Rank(g, k => s1[k]).Take(S2Perms).ToArray()).ToList();
        var pairBest = Rank(byPair, ks => s1[ks[0]]).ToList();
        var s1Best = pairBest.Select(ks => (cands[ks[0]], s1[ks[0]])).ToList();
        var pick = pairBest.SelectMany(ks => ks).Select(k => cands[k]).ToArray();
        var s2 = new Score[pick.Length];
        Parallel.For(0, pick.Length, k => s2[k] = Quick(pick[k].F, W2, 20));
        var s2List = Rank(Enumerable.Range(0, pick.Length).Select(k => (pick[k], s2[k])), x => x.Item2).ToList();
        return (s1Best, s2List);
    }

    // ---------------------------------------------------------------------------------
    // §2-3 段0
    // ---------------------------------------------------------------------------------
    static void Stage0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin381(r.F))).ToArray();   // 第382期: ヴェルは第381期までの規定に固定
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        double[,] G((string Name, Formation F)[] rs)
        {
            var g = new double[rs.Length, nw];
            Parallel.For(0, rs.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw, wins = 0;
                for (int s = 0; s < 200; s++) if (BattleEngine.Run(rs[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / 200;
            });
            return g;
        }
        var b = G(rows.Select(r => (r.Name, Pin379(r.F))).ToArray()); var g = G(rows);
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        int Over95(double[,] x) => Enumerable.Range(0, rows.Length).Count(r => x[r, last] > 95.0);
        int Info(double[,] x, IEnumerable<int> rs) => rs.Sum(r => Enumerable.Range(1, nw - 1).Count(w => x[r, w] > 0 && x[r, w] < 100));
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        int upC = 0, dnC = 0; var changed = new List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            var cells = Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).ToList();
            upC += cells.Count(w => g[r, w] > b[r, w]); dnC += cells.Count(w => g[r, w] < b[r, w]);
            if (cells.Count > 0) changed.Add($"{rows[r].Name}: " + string.Join("・", cells.Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
        }
        var all = Enumerable.Range(0, rows.Length);
        Console.WriteLine($"# {Tag} §2-3 段0 —— `compare` 64 行: 旧（第379期の規定・ムグは `MugH379`・`Pin379`）→ 新（撒き全）（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| | 動いた行 ／ セル（上がり ／ 落ち）| 主判定19行の第五波 | 全行の第1〜5波の平均 | 第五波 95% 超の行 | 情報セル（全行 ／ 主判定） |");
        Console.WriteLine("|---|---|--:|---|--:|---|");
        Console.WriteLine($"| 前 | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} | {Over95(b)} | {Info(b, all)} ／ {Info(b, pr)} |");
        Console.WriteLine($"| 後 | {changed.Count} ／ {upC + dnC}（{upC} ／ {dnC}）| {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} | {Over95(g)} | {Info(g, all)} ／ {Info(g, pr)} |");
        Console.WriteLine();
        foreach (var l in changed) Console.WriteLine("- " + l);
        Console.WriteLine();

        var boards = new (string Name, Formation F)[]
        {
            ("死の連鎖 (リィカ軸)", Pin381(CompareBuilds().First(r => r.Name == "死の連鎖 (リィカ軸)").F)),
            ("継ぎ当て×死の連鎖", Pin381(CompareBuilds().First(r => r.Name == "継ぎ当て×死の連鎖").F)),
            ("死・ソム", Pin381(Presets.DeathSom379())),
            ("鱗 (ウロ×死軸)", Pin381(CompareBuilds().First(r => r.Name == "鱗 (ウロ×死軸)").F)),
            ("駆り立て改 (カリ×死軸)", Pin381(CompareBuilds().First(r => r.Name == "駆り立て改 (カリ×死軸)").F)),
        };
        var waves = new (string Name, I.W[] Ws)[] { ("ボス", new[] { I.Boss }), ("近衛", new[] { I.Guard }), ("大隊", new[] { I.Bat }), ("本編 第5波", new[] { I.M5 }), ("強 第2〜5波", I.StrongWaves) };
        var rg = new S377.B[boards.Length, 2, waves.Length];
        Parallel.For(0, rg.Length, k =>
        {
            int bi = k / (2 * waves.Length), vi = k / waves.Length % 2, wi = k % waves.Length;
            rg[bi, vi, wi] = M(vi == 0 ? Pin379(boards[bi].F) : boards[bi].F, waves[wi].Ws, 100);
        });
        Console.WriteLine("## ムグのいる代表台 × 5 波（seed 0..99）——勝率・倒しT・倒れた編成の駒");
        Console.WriteLine();
        Console.WriteLine("| 台 | ムグ | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < 2; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {(vi == 0 ? "旧" : "新（撒き全）")} | {string.Join(" | ", Enumerable.Range(0, waves.Length).Select(wi => rg[bi, vi, wi].Cell))} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3 席の探索
    // ---------------------------------------------------------------------------------
    static readonly (string Name, I.W[] Ws)[] W3 = S377.VerWaves;

    /// <summary>段3: 台ごとに 8 物差し × seed 0..99（verbose）。</summary>
    static S377.B[] Stage3(Formation f)
    {
        var res = new S377.B[W3.Length];
        Parallel.For(0, W3.Length, wi => res[wi] = M(f, W3[wi].Ws, 100));
        return res;
    }

    static void PrintTop(string title, List<(Cand C, Score S)> top, S377.B[][] s3, int startRank = 1)
    {
        Console.WriteLine(title);
        Console.WriteLine();
        Console.WriteLine("| # | 2枠 | 並び（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3）| 段2 の平均 | " + string.Join(" | ", W3.Select(w => w.Name)) + " | ボスの1ターンの最大（隊）|");
        Console.WriteLine("|--:|---|---|--:|" + string.Concat(W3.Select(_ => "---|")) + "--:|");
        for (int i = 0; i < top.Count; i++)
        {
            var (c, s) = top[i];
            Console.WriteLine($"| {startRank + i} | {S307.Short(c.A)} ＋ {S307.Short(c.B)} | {SeatStr(c.F)} | {F1(s.Mean)} | {string.Join(" | ", s3[i].Select(x => x.Cell))} | {F0((double)Xv(s3[i][0], "teamMax") / s3[i][0].N)} |");
        }
        Console.WriteLine();
    }

    static void Seats()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var mainPairs = Pairs(reference: false); var refPairs = Pairs(reference: true);
        Console.WriteLine($"# {Tag} §3 死の台の席の探索 —— 固定 リィカ ／ ヴェル ／ ムグ（規定の駒・段0 の後）＋ 2枠");
        Console.WriteLine();
        Console.WriteLine($"ヒーラー（第283期の機械的な定義 ＝ `Boss283Diag.HealPool`）: {string.Join("・", Healers.Select(S307.Short))}。ヴェルはヒーラーに数える（ポンの補足）。");
        Console.WriteLine($"2枠の候補: ロスター 52 枚からリィカ ／ ヴェル ／ ムグとツギ ／ リリを除いた {Cands.Length} 枚。**本体** ＝ 2枠ともヒーラーでない組（{mainPairs.Length} 組・台のヒーラーはヴェルの1枚）／ **参考** ＝ 2枠の1枚がヒーラー（{refPairs.Length} 組・台にヒーラー2枚）。");
        Console.WriteLine($"段1: 全組 × 席の全並び 120 × ボス ／ 近衛 ／ 大隊 × seed 0..{S1Seeds - 1}・段2: **全組** × その組の段1 の上位 {S2Perms} 並び × seed 0..19 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波（組の順位は段2 の最良の並びで決める）・段3: 段2 の上位 10 組（組ごとに最良の並び1つ）× seed 0..99 × 8 物差し。並べる順: 4 波の勝率の平均（同点なら倒れた編成の駒の少ない方・4 波の平均）。");
        Console.WriteLine();

        var (m1, m2) = Search(mainPairs);
        var (r1, r2) = Search(refPairs);
        Console.WriteLine($"段1: 本体 {mainPairs.Length * 120} 並び ／ 参考 {refPairs.Length * 120} 並び・段2: 本体 {m2.Count} 並び ／ 参考 {r2.Count} 並び（{sw.Elapsed.TotalSeconds:F0} 秒）。");
        Console.WriteLine();

        List<(Cand C, Score S)> BestPerPair(List<(Cand C, Score S)> s2, int n) => s2.GroupBy(x => (x.C.A.Id, x.C.B.Id)).Select(g => g.First()).Take(n).ToList();
        var mPairs = BestPerPair(m2, int.MaxValue); var rPairs = BestPerPair(r2, int.MaxValue);
        int PairRank(List<(Cand C, Score S)> ps, Func<Cand, bool> pred) => ps.FindIndex(x => pred(x.C)) + 1;
        bool Has(Cand c, string id) => c.A.Id == id || c.B.Id == id;
        Console.WriteLine($"段2 の組の最良が 4 波とも 100% の組: 本体 {mPairs.Count(x => x.S.Mean == 100.0)} ／ {mPairs.Count}・参考 {rPairs.Count(x => x.S.Mean == 100.0)} ／ {rPairs.Count}（同点は倒れた数で並べる）。");
        Console.WriteLine();
        var top10 = BestPerPair(m2, 10);
        var s3 = top10.Select(x => Stage3(x.C.F)).ToArray();
        // 段3 の並べる順（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波・seed 0..99）で並べ直す
        double Mean3(S377.B[] x) => new[] { 0, 1, 2, 5 }.Average(wi => x[wi].Win);
        double Dead3(S377.B[] x) => new[] { 0, 1, 2, 5 }.Average(wi => (double)x[wi].FormDeaths / x[wi].N);
        var order = Enumerable.Range(0, top10.Count).OrderByDescending(i => Mean3(s3[i])).ThenBy(i => Dead3(s3[i])).ToArray();
        top10 = order.Select(i => top10[i]).ToList(); s3 = order.Select(i => s3[i]).ToArray();
        PrintTop("## 1. 上位 10 台（本体・段3・seed 0..99・各セル: 勝率・倒しT・倒れた編成の駒）——段3 の4波（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波）の平均で並べ直した", top10, s3);
        Console.WriteLine("段3 の4波の平均: " + string.Join("・", Enumerable.Range(0, top10.Count).Select(i => $"{i + 1} 位 {F1(Mean3(s3[i]))}")) + "。");
        Console.WriteLine();

        Console.WriteLine("## 2. 上位 3 台の誰が何Tに倒れたか（ボス ／ 近衛 ／ 大隊）とリィカの層（ボス T1〜T6）");
        Console.WriteLine();
        for (int i = 0; i < Math.Min(3, top10.Count); i++)
        {
            var x = s3[i];
            Console.WriteLine($"- **{i + 1} 位 {S307.Short(top10[i].C.A)} ＋ {S307.Short(top10[i].C.B)}**（{SeatStr(top10[i].C.F)}）: ボス {x[0].FellCell} ／ 近衛 {x[1].FellCell} ／ 大隊 {x[2].FellCell}・層（ボス）[{x[0].Layers(6)}]・ボスの倒しT {x[0].WinTs}・勇者を倒した {Pct(Xv(x[0], "heroKill"), x[0].N)}%");
        }
        Console.WriteLine();

        // 3. 2枠によく入る駒
        var top50Pairs = mPairs.Take(50).Select(x => x.C).ToList();
        var freq = top50Pairs.SelectMany(c => new[] { c.A, c.B }).GroupBy(d => d.Id).Select(g => (D: g.First(), N: g.Count())).OrderByDescending(t => t.N).ToList();
        Console.WriteLine("## 3. 2枠によく入る駒（段2 の上位 50 組に入った回数）");
        Console.WriteLine();
        Console.WriteLine(string.Join("・", freq.Select(t => $"{S307.Short(t.D)} {t.N}")) + "。");
        Console.WriteLine();

        // 4. ゾト ／ ゴルムの位置・今の死の連鎖
        Console.WriteLine("## 4. ゾト ／ ゴルムの位置・今の「死の連鎖 (リィカ軸)」");
        Console.WriteLine();
        var extra = new List<(string Name, Formation F, int R2)>
        {
            ("ゾトを含む最良（段2 の組の最良の並び）", mPairs.First(x => Has(x.C, "zoto")).C.F, PairRank(mPairs, c => Has(c, "zoto"))),
            ("ゴルムを含む最良", mPairs.First(x => Has(x.C, "golm")).C.F, PairRank(mPairs, c => Has(c, "golm"))),
            ("ゾト ＋ ゴルムの組の最良の並び", mPairs.First(x => Has(x.C, "zoto") && Has(x.C, "golm")).C.F, PairRank(mPairs, c => Has(c, "zoto") && Has(c, "golm"))),
            ("今の死の連鎖 (リィカ軸)（ゾト ／ ゴルム・今の席）", Pin381(CompareBuilds().First(r => r.Name == "死の連鎖 (リィカ軸)").F), 0),
        };
        foreach (var (name, f, r2i) in extra)
        {
            var sc = Quick(f, W2, 20);
            int rank2 = mPairs.Count(x => x.S.Mean > sc.Mean || (x.S.Mean == sc.Mean && x.S.Deaths < sc.Deaths)) + 1;
            var x3 = Stage3(f);
            Console.WriteLine($"- **{name}**（{SeatStr(f)}）: {(r2i > 0 ? $"組の順位 {r2i} ／ {mPairs.Count}" : $"組の順位に当てると {rank2} ／ {mPairs.Count} 相当")}・段2 の物差し {F1(sc.Mean)}（4 波 {string.Join(" ／ ", sc.Win.Select(F1))}・倒れた {F1(sc.Deaths)}）・段3: {string.Join(" ／ ", W3.Select((w, wi) => $"{w.Name} {x3[wi].Cell}"))}");
        }
        Console.WriteLine();

        // 5. 参考: ヒーラー2枚
        var ref5 = BestPerPair(r2, 5);
        var rs3 = ref5.Select(x => Stage3(x.C.F)).ToArray();
        var rorder = Enumerable.Range(0, ref5.Count).OrderByDescending(i => Mean3(rs3[i])).ThenBy(i => Dead3(rs3[i])).ToArray();
        ref5 = rorder.Select(i => ref5[i]).ToList(); rs3 = rorder.Select(i => rs3[i]).ToArray();
        PrintTop("## 5. 参考: ヒーラー2枚の台（2枠のどちらかがヒーラー）の上位 5 台（段3・seed 0..99）", ref5, rs3);
        {
            var som = Pin381(Presets.DeathSom379());
            var sc = Quick(som, W2, 20);
            int rank2 = rPairs.Count(x => x.S.Mean > sc.Mean || (x.S.Mean == sc.Mean && x.S.Deaths < sc.Deaths)) + 1;
            int r2i = PairRank(rPairs, c => Has(c, "golm") && Has(c, "som"));
            var somBest = rPairs.First(x => Has(x.C, "som"));
            int somR2 = PairRank(rPairs, c => Has(c, "som"));
            var x3 = Stage3(som); var xb = Stage3(somBest.C.F);
            Console.WriteLine($"- **死・ソム**（{SeatStr(som)}）: 今の席のままで参考の組の順位に当てると {rank2} ／ {rPairs.Count} 相当（ソム ＋ ゴルムの組の最良の並びは {r2i} 位）・段2 の物差し {F1(sc.Mean)}（4 波 {string.Join(" ／ ", sc.Win.Select(F1))}）・段3: {string.Join(" ／ ", W3.Select((w, wi) => $"{w.Name} {x3[wi].Cell}"))}");
            Console.WriteLine($"- **ソムを含む最良**（{S307.Short(somBest.C.A)} ＋ {S307.Short(somBest.C.B)}・{SeatStr(somBest.C.F)}）: 参考の組の順位 {somR2} ／ {rPairs.Count}・段3: {string.Join(" ／ ", W3.Select((w, wi) => $"{w.Name} {xb[wi].Cell}"))}");
            int inTop = ref5.FindIndex(x => x.C.A.Id == "som" || x.C.B.Id == "som");
            Console.WriteLine($"- 参考の上位 5 台にソムを含む台: {(inTop < 0 ? "無い" : $"{inTop + 1} 位")}。");
        }
        Console.WriteLine();

        // 6. 気になった組（並べる順の 11〜40 位の組を段3 に掛け、上位 10 の外で突出したもの）
        var next = mPairs.Skip(10).Take(30).ToList();
        var ns3 = next.Select(x => Stage3(x.C.F)).ToArray();
        double WinT(S377.B b) => b.Wins == 0 ? 99 : (double)b.WinT / b.Wins;
        Console.WriteLine("## 6. 気になった組（組の順位 11〜40 位を段3 に掛けた・上位 10 の外で突出したもの）");
        Console.WriteLine();
        var picks = new (string Why, Func<S377.B[], double> Key, double Top10)[]
        {
            ("強い条件（強 第2〜5波）が最も高い", x => x[7].Win, s3.Max(y => y[7].Win)),
            ("ボスの倒しT が最も早い", x => -WinT(x[0]), s3.Min(y => WinT(y[0]))),
            ("ボスの隊の1ターンの最大が最も大きい", x => (double)Xv(x[0], "teamMax") / x[0].N, s3.Max(y => (double)Xv(y[0], "teamMax") / y[0].N)),
        };
        foreach (var (why, key, top) in picks)
        {
            int bi = Enumerable.Range(0, next.Count).OrderByDescending(i => key(ns3[i])).First();
            var c = next[bi].C; var x = ns3[bi];
            Console.WriteLine($"- **{why}**: {11 + bi} 位 {S307.Short(c.A)} ＋ {S307.Short(c.B)}（{SeatStr(c.F)}）——段3: {string.Join(" ／ ", W3.Select((w, wi) => $"{w.Name} {x[wi].Cell}"))}・ボスの1ターンの最大 {F0((double)Xv(x[0], "teamMax") / x[0].N)}（上位 10 の最良 {F1(top)}）");
        }
        Console.WriteLine();
        Console.WriteLine($"1位の台: {SeatStr(top10[0].C.F)}（Presets の「試遊・死 探索1位」に置く）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
