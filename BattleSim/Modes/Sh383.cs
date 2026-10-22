using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S381 = Sh381Diag;
using S382 = Sh382Diag;

// =====================================================================================
// sh383 —— 第383期【軽い期】弔いの楽士のマイナスを直す（版 D2 ／ D1n）／ 死の外での使い道。規定は1つも変えない。
// 指示書は design/PHASE383_DIRGE_ECHO_SPEC.md、報告は design/PHASE383_DIRGE_ECHO.md。
//
//     dotnet run --project BattleSim -c Release 0 sh383 dead      # §3-1: 第382期の4台 × 候補（D2 ／ D1n ／ 素体 ／ 参考 D1）× 8 物差し（1〜3 は並びを探し直す）
//     dotnet run --project BattleSim -c Release 0 sh383 outside   # §3-2: 死の外の6台で1枚を楽士（D2 ／ D1n）に替えた最良（seed 0..19 の4波で選び、最良だけ seed 0..99 × 8 物差し）
//     dotnet run --project BattleSim -c Release 0 sh383 check     # 自己検査
//
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p383/predict.md`）。素体の替え ／ 並びの探索 ／ 死の外の替えた台は器具の中だけ。
// =====================================================================================
static partial class Sh383Diag
{
    internal const string PredictFile = ".tmp/p383/predict.md";
    const string Tag = "第383期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "dead" or "outside" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh383: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "dead": Dead(); return;
            case "outside": Outside(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh383: モードは dead / outside / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long Xv(S377.B b, string k) => b.X.GetValueOrDefault(k);
    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] Key4 = { 0, 1, 2, 5 };

    internal static readonly (string Name, UnitDef D)[] CandOf =
        { ("D2", UnitCatalog.Bard383), ("D1n", UnitCatalog.Bard382n), ("素体", S382.Plain), ("D1（参考）", UnitCatalog.Bard382) };
    internal static readonly string[] OutsideNames = { "試遊・燃焼 ポンの台", "試遊・標 守り型", "試遊・感電 ポンの台", "試遊・毒", "試遊・呪い", "試遊・破片" };
    static string SeatStr(Formation f) => string.Join(" ／ ", Enumerable.Range(0, 5).Select(s => f[s] is { } d ? S307.Short(d) : "—"));

    /// <summary>第382期の計数に、マイナスの燃料（敵の再行動の中で倒れた味方 ／ 儚い駒 ／ 起き上がる死 ／ 積もった層）を足す。</summary>
    internal static void Extra(BattleResult r, List<UnitState> p, List<UnitState> e, S377.B b)
    {
        S382.Extra(r, p, e, b);
        var x = b.X;
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Bard383.Id, out var bt))
        {
            void Add(string k, long v) { if (v != 0) x[k] = x.GetValueOrDefault(k) + v; }
            Add("eKills", bt.R383EchoKills); Add("eEph", bt.R383EchoKillsEph); Add("eRise", bt.R383EchoKillsRise); Add("eLayer", bt.R383EchoLayer);
        }
    }
    static S377.B M(Formation f, I.W[] ws, int seeds) => S377.Measure(f, ws, seeds, Extra);
    static S377.B[] Eight(Formation f) { var res = new S377.B[W8.Length]; Parallel.For(0, W8.Length, wi => res[wi] = M(f, W8[wi].Ws, 100)); return res; }
    static double Mean4(S377.B[] x) => Key4.Average(w => x[w].Win);
    static double Dead4(S377.B[] x) => Key4.Average(w => (double)x[w].FormDeaths / x[w].N);
    static string Got(S377.B b, string prefix) => string.Join("・", b.X.Where(kv => kv.Key.StartsWith(prefix + ":")).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key[(prefix.Length + 1)..]} {Per(kv.Value, b.N)}"));
    static string FellSplit(S377.B b, Formation f)
    {
        var parts = new List<string>();
        foreach ((int _, UnitDef d) in f.Occupied())
        {
            string nm = S307.Short(d);
            long rd = Xv(b, $"rd:{nm}"), ri = Xv(b, $"ri:{nm}"), ri1 = Xv(b, $"ri1:{nm}");
            if (rd == 0 && ri == 0) continue;
            string s = nm;
            if (rd > 0) s += $" 死 {Pct(rd, b.N)}%（T{Per(Xv(b, $"rdT:{nm}"), rd)}）";
            if (ri > 0) s += $" 起 {Per(ri, b.N)}（T{Per(Xv(b, $"ri1T:{nm}"), ri1)}）";
            parts.Add(s);
        }
        return parts.Count == 0 ? "—" : string.Join("・", parts);
    }
    static string Bard(S377.B x) =>
        $"奏で {Per(Xv(x, "songs"), x.N)}・[{Got(x, "enc")}] ／ 敵を控えた {Per(Xv(x, "echoes"), x.N)}・動いた {Per(Xv(x, "echoTurns"), x.N)}・味方への傷 {F0((double)Xv(x, "echoDealt") / x.N)} ／ その中で倒れた味方 {Per(Xv(x, "eKills"), x.N)}（儚い {Per(Xv(x, "eEph"), x.N)}・起き上がる {Per(Xv(x, "eRise"), x.N)}）・積もった層 {Per(Xv(x, "eLayer"), x.N)}";

    static void Signs(List<(string At, S377.B[] X, S377.B[] Base)> rows)
    {
        Console.WriteLine("## 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満の勝ち ／ 30T の打ち切りが比べる台より 5pt 以上増えた ／ 層 20 超 ／ int のあふれ）");
        Console.WriteLine();
        var signs = new List<string>();
        foreach (var (at, x, x0) in rows)
        {
            foreach (int wi in new[] { 1, 2 }) if (x[wi].Win1T * 20 >= x[wi].N && x[wi].Win1T > 0) signs.Add($"{at} × {W8[wi].Name}: 1T 決着 {Pct(x[wi].Win1T, x[wi].N)}%");
            if (x[0].BossFast > 0) signs.Add($"{at}: ボス 4T 未満の勝ち {Pct(x[0].BossFast, x[0].N)}%");
            for (int wi = 0; wi < W8.Length; wi++)
            {
                if (100.0 * x[wi].Cap / x[wi].N - 100.0 * x0[wi].Cap / x0[wi].N >= 5) signs.Add($"{at} × {W8[wi].Name}: 30T の打ち切り {Pct(x0[wi].Cap, x0[wi].N)} → {Pct(x[wi].Cap, x[wi].N)}%");
                if (x[wi].Overflow > 0) signs.Add($"{at} × {W8[wi].Name}: あふれの兆し {x[wi].Overflow} 件");
            }
            foreach (int wi in Key4) if (Xv(x[wi], "peak20") > 0) signs.Add($"{at} × {W8[wi].Name}: 層 20 超 {Pct(Xv(x[wi], "peak20"), x[wi].N)}%（比べる台 {Pct(Xv(x0[wi], "peak20"), x0[wi].N)}%）");
        }
        if (signs.Count == 0) Console.WriteLine("- 無い。");
        foreach (var s in signs) Console.WriteLine("- " + s);
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3-1 死の台
    // ---------------------------------------------------------------------------------
    static void Dead()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int nb = 4, nc = CandOf.Length;
        var boards = new Formation[nb, nc];
        for (int bi = 0; bi < 3; bi++) for (int ci = 0; ci < nc; ci++) boards[bi, ci] = S382.BestSeat(S382.Partners[bi].D, CandOf[ci].D).F;
        for (int ci = 0; ci < nc; ci++) boards[3, ci] = S382.ChainWith(CandOf[ci].D);
        string[] bname = S382.Partners.Select(p => p.Name).Append("死の連鎖（ゾト → 候補）").ToArray();
        var g = new S377.B[nb, nc][];
        for (int bi = 0; bi < nb; bi++) for (int ci = 0; ci < nc; ci++) g[bi, ci] = Eight(boards[bi, ci]);

        Console.WriteLine($"# {Tag} §3-1 死の台 —— 第382期の4台 × 候補（{string.Join(" ／ ", CandOf.Select(c => c.Name))}）× 8 物差し（seed 0..99）");
        Console.WriteLine();
        Console.WriteLine("台 1〜3 は候補ごとに並びを探し直した（第382期と同じ: 120 並び × ボス ／ 近衛 ／ 大隊・seed 0..9）。台 4 は並び固定。セル: 勝率・決着T・倒れた編成の駒（起き上がる死も1回）。");
        Console.WriteLine();
        Console.WriteLine("## 1. 台 × 候補");
        Console.WriteLine();
        Console.WriteLine("| 台 | 候補 | 並び（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4波の平均 | ボスの倒しT ／ 1ターンの最大 |");
        Console.WriteLine("|---|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                var x = g[bi, ci];
                Console.WriteLine($"| {(ci == 0 ? bname[bi] : "")} | {CandOf[ci].Name} | {SeatStr(boards[bi, ci])} | {string.Join(" | ", x.Select(c => c.Cell))} | {F1(Mean4(x))} | {x[0].WinTs}T ／ {F0((double)Xv(x[0], "teamMax") / x[0].N)} |");
            }
        Console.WriteLine();
        for (int ci = 0; ci < nc; ci++)
            Console.WriteLine($"- {CandOf[ci].Name}: 4 波の平均 {F1(Enumerable.Range(0, nb).Average(bi => Mean4(g[bi, ci])))} ／ 強い条件 {F1(Enumerable.Range(0, nb).Average(bi => g[bi, ci][7].Win))} ／ 近衛 {F1(Enumerable.Range(0, nb).Average(bi => g[bi, ci][1].Win))} ／ 大隊 {F1(Enumerable.Range(0, nb).Average(bi => g[bi, ci][2].Win))}（4 台の平均）");
        Console.WriteLine();

        Console.WriteLine("## 2. 楽士（1戦あたり・ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件）");
        Console.WriteLine();
        foreach (int ci in new[] { 0, 1, 3 })
            for (int bi = 0; bi < nb; bi++)
                Console.WriteLine($"- **{bname[bi]} × {CandOf[ci].Name}**: " + string.Join(" ／ ", new[] { 0, 1, 2, 5, 7 }.Select(w => $"{W8[w].Name} {Bard(g[bi, ci][w])}")));
        Console.WriteLine();

        Console.WriteLine("## 3. リィカの層（ボス・T1〜T8・生きていた戦の平均）と 9 層に届いた戦%・T");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"- **{bname[bi]}**: " + string.Join(" ／ ", Enumerable.Range(0, nc).Select(ci => { var x = g[bi, ci][0]; return $"{CandOf[ci].Name} [{x.Layers(8)}]・9 層 {Pct(Xv(x, "l9"), x.N)}%・T{Per(Xv(x, "l9T"), Xv(x, "l9"))}"; })));
        Console.WriteLine();

        Console.WriteLine("## 4. 誰が何Tに倒れたか（D2 ／ D1n ／ 素体・ボス ／ 近衛 ／ 大隊・「死」＝ 本当の死 ／「起」＝ 起き上がり）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            foreach (int ci in new[] { 0, 1, 2 })
                Console.WriteLine($"- **{bname[bi]} × {CandOf[ci].Name}**: " + string.Join(" ／ ", new[] { 0, 1, 2 }.Select(w => $"{W8[w].Name} {FellSplit(g[bi, ci][w], boards[bi, ci])}")));
        Console.WriteLine();

        var rows = new List<(string, S377.B[], S377.B[])>();
        for (int bi = 0; bi < nb; bi++) foreach (int ci in new[] { 0, 1, 3 }) rows.Add(($"{bname[bi]} × {CandOf[ci].Name}", g[bi, ci], g[bi, 2]));
        Signs(rows);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 死の外
    // ---------------------------------------------------------------------------------
    static (double Mean, double Deaths) Quick(Formation f)
    {
        double win = 0; long deaths = 0, n = 0;
        foreach (int wi in Key4)
        {
            int w = 0;
            foreach (var wave in W8[wi].Ws)
                for (int s = 0; s < 20; s++) { var (r, p, _) = I.Raw(f, wave, s, census: false); if (r.PlayerWon) w++; deaths += p.Count(u => u.LastDeathTurn > 0); n++; }
            win += 100.0 * w / (20 * W8[wi].Ws.Length);
        }
        return (win / Key4.Length, (double)deaths / n);
    }
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }

    static void Outside()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3-2 死の外 —— {string.Join(" ／ ", OutsideNames)} で、1枚を楽士（D2 ／ D1n）に替えた台の最良（替える1枚 × 5 通り・seed 0..19 の4波で選び、最良だけ seed 0..99 × 8 物差し）");
        Console.WriteLine();
        Console.WriteLine("最良の選び方: ボス ／ 近衛 ／ 大隊 ／ 本編 第5波の勝率の平均（同点なら倒れた数の少ない方）。セル: 勝率・決着T・倒れた数（括弧は元の台との差 pt）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 楽士 | 替えた1枚（席）| 選びの4波 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 奏でた（ボス ／ 近衛 ／ 大隊 ／ 第5波）|");
        Console.WriteLine("|---|---|---|--:|" + string.Concat(W8.Select(_ => "---|")) + "---|");
        var log = new List<string>(); var fell = new List<string>(); var sigRows = new List<(string, S377.B[], S377.B[])>();
        foreach (var name in OutsideNames)
        {
            var f0 = S381.Named(name);
            var orig = Eight(f0);
            var q0 = Quick(f0);
            Console.WriteLine($"| **{name}**（{S307.Seats(f0)}）| 元の台 | — | {F1(q0.Mean)} | {string.Join(" | ", orig.Select(x => x.Cell))} | — |");
            foreach (int ci in new[] { 0, 1 })
            {
                var cands = f0.Occupied().Select(o => (o.Slot, o.Def, F: Put(f0, o.Slot, CandOf[ci].D))).ToArray();
                var qs = new (double Mean, double Deaths)[cands.Length];
                Parallel.For(0, cands.Length, k => qs[k] = Quick(cands[k].F));
                int best = Enumerable.Range(0, cands.Length).OrderByDescending(k => qs[k].Mean).ThenBy(k => qs[k].Deaths).First();
                var res = Eight(cands[best].F);
                Console.WriteLine($"| | {CandOf[ci].Name} | {S307.Short(cands[best].Def)}（席 {cands[best].Slot}）| {F1(qs[best].Mean)} | {string.Join(" | ", Enumerable.Range(0, W8.Length).Select(wi => $"{res[wi].Cell}（{(res[wi].Win - orig[wi].Win >= 0 ? "+" : "")}{F1(res[wi].Win - orig[wi].Win)}）"))} | {string.Join(" ／ ", Key4.Select(w => Per(Xv(res[w], "songs"), res[w].N)))} |");
                log.Add($"- {name} × {CandOf[ci].Name}: 替える1枚ごとの選びの4波 " + string.Join("・", Enumerable.Range(0, cands.Length).Select(k => $"{S307.Short(cands[k].Def)} {F1(qs[k].Mean)}"))
                    + $"・楽士（ボス）{Bard(res[0])}");
                if (Enumerable.Range(0, W8.Length).Any(wi => res[wi].Win > orig[wi].Win + 0.5))
                    fell.Add($"- **{name} × {CandOf[ci].Name}**（{SeatStr(cands[best].F)}・上がった波: {string.Join("・", Enumerable.Range(0, W8.Length).Where(wi => res[wi].Win > orig[wi].Win + 0.5).Select(wi => W8[wi].Name))}）: "
                        + string.Join(" ／ ", new[] { 0, 1, 2, 7 }.Select(w => $"{W8[w].Name} {FellSplit(res[w], cands[best].F)}")) + $" ——元の台: " + string.Join(" ／ ", new[] { 0, 1, 2, 7 }.Select(w => $"{W8[w].Name} {FellSplit(orig[w], f0)}")));
                sigRows.Add(($"{name} × {CandOf[ci].Name}", res, orig));
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 替える1枚ごとの選びの4波（seed 0..19）と楽士（ボス）");
        Console.WriteLine();
        foreach (var l in log) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine("## 誰が何Tに倒れたか（元の台より上がった波がある台・ボス ／ 近衛 ／ 大隊 ／ 強い条件）");
        Console.WriteLine();
        if (fell.Count == 0) Console.WriteLine("- 無い（どの台も元の台より上がった波が無い）。");
        foreach (var l in fell) Console.WriteLine(l);
        Console.WriteLine();
        Signs(sigRows);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
