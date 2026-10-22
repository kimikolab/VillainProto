using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S378 = Sh378Diag;
using S379 = Sh379Diag;
using S381 = Sh381Diag;

// =====================================================================================
// sh385 —— 第385期【軽い期】段0: 哀歌のエレ（楽士 G1）をロスターに入れる（ウケの籍）／ 本体: 死軸のまとめ（design/DEATH_AXIS_SUMMARY.md）。
// 指示書は design/PHASE385_ERE_SPEC.md、報告は design/PHASE385_ERE.md。
//
//     dotnet run --project BattleSim -c Release 0 sh385 stage0    # §2-5: `compare` 64 → 66 行（足した2行・ウケの行）・エレの台 × 5 波 旧（版の楽士 G1）→ 新（エレ）
//     dotnet run --project BattleSim -c Release 0 sh385 axes      # §3-1 の 4: 死の台 ＋ 他の軸の試遊の台 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件・倒しT・隊の1ターンの最大（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh385 mix       # §3-1 の 5: 寄せ集めの台 50（第377期と同じ台）× 素体 ／ 今のリィカ（seed 0..19）
//     dotnet run --project BattleSim -c Release 0 sh385 check     # 自己検査
//
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p385/predict.md`）。
// =====================================================================================
static partial class Sh385Diag
{
    internal const string PredictFile = ".tmp/p385/predict.md";
    const string Tag = "第385期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "stage0" or "axes" or "mix" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh385: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "stage0": Stage0(); return;
            case "axes": Axes(); return;
            case "mix": Mix(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh385: モードは stage0 / axes / mix / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static long Xv(S377.B b, string k) => b.X.GetValueOrDefault(k);
    static string Sg(double d) => (d >= 0 ? "+" : "") + F1(d);
    static S377.B M(Formation f, I.W[] ws, int seeds) => S377.Measure(f, ws, seeds, S379.Extra);
    static readonly (string Name, I.W[] Ws)[] W5 = { ("ボス", new[] { I.Boss }), ("近衛", new[] { I.Guard }), ("大隊", new[] { I.Bat }), ("本編 第5波", new[] { I.M5 }), ("強 第2〜5波", I.StrongWaves) };

    static double[,] CGrid((string Name, Formation F)[] rows)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw, wins = 0;
            for (int s = 0; s < 200; s++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / 200;
        });
        return g;
    }

    // ---------------------------------------------------------------------------------
    // §2-5 段0
    // ---------------------------------------------------------------------------------
    static void Stage0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var oldRows = CompareBuildsH384(); var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = CGrid(oldRows); var g = CGrid(rows);
        int Over95(double[,] x, int n) => Enumerable.Range(0, n).Count(r => x[r, last] > 95.0);
        int Info(double[,] x, IEnumerable<int> rs) => rs.Sum(r => Enumerable.Range(1, nw - 1).Count(w => x[r, w] > 0 && x[r, w] < 100));
        string Means(double[,] x, int n) => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(Enumerable.Range(0, n).Average(r => x[r, w]))));
        var prOld = Enumerable.Range(0, oldRows.Length).Where(ri => Baseline.PrimaryRows.Contains(oldRows[ri].Name)).ToList();
        var prNew = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        int moved = 0; var movedRows = new List<string>();
        for (int r = 0; r < oldRows.Length; r++)
        {
            int ni = Array.FindIndex(rows, x => x.Name == oldRows[r].Name);
            for (int w = 0; w < nw; w++) if (g[ni, w] != b[r, w]) { moved++; movedRows.Add(oldRows[r].Name); }
        }
        var ukeRows = rows.Concat(CrossBuilds()).Where(r => r.F.Occupied().Any(o => o.Def.Id == "uke")).Select(r => r.Name).ToArray();   // `compare` ＋ 交差帯
        Console.WriteLine($"# {Tag} §2-5 段0 —— `compare` 64 → {rows.Length} 行（哀歌のエレの2行を足した・ウケは `Retired` へ・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| | 行 | 動いたセル（もとの 64 行）| 主判定19行の第五波 | 全行の第1〜5波の平均 | 第五波 95% 超の行 | 情報セル（全行 ／ 主判定） |");
        Console.WriteLine("|---|--:|--:|--:|---|--:|---|");
        Console.WriteLine($"| 前 | {oldRows.Length} | — | {F1(prOld.Average(ri => b[ri, last]))} | {Means(b, oldRows.Length)} | {Over95(b, oldRows.Length)} | {Info(b, Enumerable.Range(0, oldRows.Length))} ／ {Info(b, prOld)} |");
        Console.WriteLine($"| 後 | {rows.Length} | {moved} | {F1(prNew.Average(ri => g[ri, last]))} | {Means(g, rows.Length)} | {Over95(g, rows.Length)} | {Info(g, Enumerable.Range(0, rows.Length))} ／ {Info(g, prNew)} |");
        Console.WriteLine();
        Console.WriteLine($"ウケのいる行（`compare` ／ 交差帯・残した・前例 ナタ → カタ）: {string.Join("、", ukeRows)}。主判定19行に足した2行は入っていない: {(CompareRows385.All(n => !Baseline.PrimaryRows.Contains(n)) ? "○" : "×")}。");
        Console.WriteLine();
        Console.WriteLine("## 足した2行（第1〜5波・seed 0..199）");
        Console.WriteLine();
        foreach (var n in CompareRows385)
        {
            int ri = Array.FindIndex(rows, x => x.Name == n);
            Console.WriteLine($"- {n}（{S307.Seats(rows[ri].F)}）: " + string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => $"第{w + 1}波 {F1(g[ri, w])}")));
        }
        Console.WriteLine();

        var boards = new (string Name, Formation F)[] { ("試遊・死 エレ", Presets.DeathBard382(UnitCatalog.Ere)), ("試遊・燃焼 エレ", Presets.BurnBard384(UnitCatalog.Ere)) };
        var rg = new S377.B[boards.Length, 2, W5.Length];
        Parallel.For(0, rg.Length, k =>
        {
            int bi = k / (2 * W5.Length), vi = k / W5.Length % 2, wi = k % W5.Length;
            rg[bi, vi, wi] = M(vi == 0 ? Pin385(boards[bi].F) : boards[bi].F, W5[wi].Ws, 100);
        });
        Console.WriteLine("## エレのいる台 × 5 波（seed 0..99）——旧 ＝ 第384期の版の楽士 G1（`Bard384G1`）／ 新 ＝ 哀歌のエレ");
        Console.WriteLine();
        Console.WriteLine("| 台 | 楽士 | " + string.Join(" | ", W5.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(W5.Select(_ => "---|")));
        int diff = 0;
        for (int bi = 0; bi < boards.Length; bi++)
        {
            for (int vi = 0; vi < 2; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {(vi == 0 ? "旧（G1）" : "新（エレ）")} | {string.Join(" | ", Enumerable.Range(0, W5.Length).Select(wi => rg[bi, vi, wi].Cell))} |");
            for (int wi = 0; wi < W5.Length; wi++) if (rg[bi, 0, wi].Cell != rg[bi, 1, wi].Cell) diff++;
        }
        Console.WriteLine();
        Console.WriteLine($"旧と新で違うセル: **{diff}**（{boards.Length * W5.Length} セル中）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 4 死の台 ＋ 他の軸
    // ---------------------------------------------------------------------------------
    internal static (string Name, string Axis, Formation F)[] AxisRows()
    {
        var all = Presets.Playtest.Concat(Presets.Versions).ToArray();
        Formation Named(string n) => all.First(r => r.Name == n).F;
        var rows = new List<(string, string, Formation)>
        {
            ("試遊・死の連鎖", "死", Named("試遊・死の連鎖")), ("試遊・死 探索1位", "死", Named("試遊・死 探索1位")), ("試遊・死 ソム", "死", Named("試遊・死 ソム")),
            ("試遊・死 エレ", "死", Named("試遊・死 エレ")), ("第380期 2 位（スィド＋ドハ）", "死", S381.Seat(S381.Top10[1].Seats)), ("第380期 3 位（ゴルム＋スィド）", "死", S381.Seat(S381.Top10[2].Seats)),
        };
        foreach (var n in S378.AxisBoards) rows.Add((n, n.Contains("燃焼") ? "燃焼" : n.Contains("感電") ? "感電" : n.Contains("毒") ? "毒" : n.Contains("標") ? "標" : n.Contains("破片") ? "破片" : "呪い", Named(n)));
        rows.Add(("試遊・燃焼 エレ", "燃焼", Named("試遊・燃焼 エレ")));
        return rows.ToArray();
    }

    static void Axes()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = AxisRows();
        var g = new S377.B[rows.Length, W5.Length];
        Parallel.For(0, g.Length, k => g[k / W5.Length, k % W5.Length] = M(rows[k / W5.Length].F, W5[k % W5.Length].Ws, 100));
        Console.WriteLine($"# {Tag} §3-1 の 4 今の数字 —— 死の台 ＋ 他の軸の試遊の台 × 5 波（seed 0..99・この期の規定）・ボスの倒しT ／ 隊の1ターンの最大の与ダメ（第378期 §4 の物差し）");
        Console.WriteLine();
        Console.WriteLine("セル: 勝率・決着T・倒れた編成の駒（起き上がる死も1回）。隊の1ターンの最大 ＝ 敵に入った `Damage` の合計（撃ち手が敵でないもの・燃焼 ／ 毒の刻みを含む）のターンごとの最大を戦ごとに取った平均。");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 台 | 並び | " + string.Join(" | ", W5.Select(w => w.Name)) + " | ボスの倒しT | 1ターンの最大（ボス ／ 近衛 ／ 大隊）|");
        Console.WriteLine("|---|---|---|" + string.Concat(W5.Select(_ => "---|")) + "--:|---|");
        for (int ri = 0; ri < rows.Length; ri++)
            Console.WriteLine($"| {rows[ri].Axis} | {rows[ri].Name} | {S307.Seats(rows[ri].F)} | {string.Join(" | ", Enumerable.Range(0, W5.Length).Select(wi => g[ri, wi].Cell))} | {g[ri, 0].WinTs} | {string.Join(" ／ ", new[] { 0, 1, 2 }.Select(wi => F0((double)Xv(g[ri, wi], "teamMax") / g[ri, wi].N)))} |");
        Console.WriteLine();
        var dead = Enumerable.Range(0, rows.Length).Where(ri => rows[ri].Axis == "死").ToArray(); var oth = Enumerable.Range(0, rows.Length).Where(ri => rows[ri].Axis != "死").ToArray();
        string Avg(int[] rs, int wi) => F1(rs.Average(ri => g[ri, wi].Win));
        Console.WriteLine($"死の台 {dead.Length} 台の平均: " + string.Join(" ／ ", Enumerable.Range(0, W5.Length).Select(wi => $"{W5[wi].Name} {Avg(dead, wi)}")) + $"・ボスの1ターンの最大 {F0(dead.Average(ri => (double)Xv(g[ri, 0], "teamMax") / g[ri, 0].N))}。");
        Console.WriteLine($"他の軸 {oth.Length} 台の平均: " + string.Join(" ／ ", Enumerable.Range(0, W5.Length).Select(wi => $"{W5[wi].Name} {Avg(oth, wi)}")) + $"・ボスの1ターンの最大 {F0(oth.Average(ri => (double)Xv(g[ri, 0], "teamMax") / g[ri, 0].N))}。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 5 寄せ集め
    // ---------------------------------------------------------------------------------
    /// <summary>第377期 §4-5 の寄せ集めの台（`sh377 mix`・素体 ／ 第377期の規定のリィカ）の数字（50 台の平均・seed 0..19・8 物差し）。</summary>
    static readonly double[] Mix377Plain = { 4.8, 13.9, 10.5, 67.3, 87.6, 74.6, 82.1, 4.4 }, Mix377Rica = { 2.3, 14.7, 12.2, 73.2, 89.6, 77.4, 83.7, 4.5 };

    static void Mix()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int seeds = 20;
        var waves = S377.VerWaves; int nw = waves.Length, nb = S377.MixBoards;
        var vers = new (string Name, UnitDef D)[] { ("素体", I.Plain(UnitCatalog.Rica)), ("今のリィカ", UnitCatalog.Rica) };
        var boards = vers.Select(v => S377.MixBoardsOf(v.D)).ToArray();   // 第377期と同じ 50 台（第385期: 駒の池はウケのいる `AllH384`）
        var g = new double[vers.Length, nb, nw];
        Parallel.For(0, vers.Length * nb * nw, k =>
        {
            int vi = k / (nb * nw), bi = k / nw % nb, wi = k % nw;
            int w = 0, n = 0;
            foreach (var wave in waves[wi].Ws) for (int s = 0; s < seeds; s++) { n++; if (I.Raw(boards[vi][bi], wave, s, census: false).R.PlayerWon) w++; }
            g[vi, bi, wi] = 100.0 * w / n;
        });
        Console.WriteLine($"# {Tag} §3-1 の 5 寄せ集めの台 —— 第377期と同じ 50 台（リィカ ＋ 死の札を持たない4枚・席もランダム・`new Random({S377.MixSeed})`）× 8 物差し（seed 0..{seeds - 1}・この期の規定）");
        Console.WriteLine();
        Console.WriteLine("| | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "--:|")));
        Console.WriteLine($"| 第377期 素体 | {string.Join(" | ", Mix377Plain.Select(F1))} |");
        Console.WriteLine($"| 第377期 規定のリィカ（素体との差）| {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => $"{F1(Mix377Rica[wi])}（{Sg(Mix377Rica[wi] - Mix377Plain[wi])}）"))} |");
        double Avg(int vi, int wi) => Enumerable.Range(0, nb).Average(bi => g[vi, bi, wi]);
        Console.WriteLine($"| 今 素体 | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => F1(Avg(0, wi))))} |");
        Console.WriteLine($"| 今のリィカ（素体との差）| {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => $"{F1(Avg(1, wi))}（{Sg(Avg(1, wi) - Avg(0, wi))}）"))} |");
        Console.WriteLine();
        Console.WriteLine("素体より 10pt 以上 強い ／ 弱い台（50 台のうち）: " + string.Join("・", Enumerable.Range(0, nw).Select(wi => $"{waves[wi].Name} {Enumerable.Range(0, nb).Count(bi => g[1, bi, wi] - g[0, bi, wi] >= 10)} ／ {Enumerable.Range(0, nb).Count(bi => g[1, bi, wi] - g[0, bi, wi] <= -10)}")) + "。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
