using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S388 = Sh388Diag;
using S390 = Sh390Diag;
using S404 = Sh404Diag;

// =====================================================================================
// sh405 —— 第405期【軽い期】段0: ハギの R50 を規定に ／ 本体: ハギの演出のための出来事（`BattleEventKind.Reap`・表示専用）を整える。盤面は変えない。
// 指示書は design/PHASE405_HAGI_SETTLE_SPEC.md、報告は design/PHASE405_HAGI_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 sh405 zero      # §2 段0: `compare` の動き（第404期の HEAD の表と ／ 第404期の R50 の列と）＋ 試遊の 6 台 × 規定 ／ 旧 × 5 物差し
//     dotnet run --project BattleSim -c Release 0 sh405 events    # §4: ハギの試遊の 6 台 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 × seed 0..19 の1戦あたりの出来事の回数 ＋ 単発の例（台 × ボス ／ 近衛 × seed 0）
//     dotnet run --project BattleSim -c Release 0 sh405 log <台の頭> <物差し 0..7> <seed>   # 1戦のログと、ハギの出来事の並び
//     dotnet run --project BattleSim -c Release 0 sh405 check     # 自己検査
//
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p405/predict.md`）。
// =====================================================================================
static partial class Sh405Diag
{
    internal const string PredictFile = ".tmp/p405/predict.md";
    const string Tag = "第405期";
    const int Seeds = 100, CompareSeeds = 200, EvSeeds = 20;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "events" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh405: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "zero": Zero(); return;
            case "events": Events(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh405: モードは zero / events / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    static Formation ColBoard(S388.Board b, UnitDef? d) => d is null ? b.F : Put(b.F, b.Slot, d);

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    const int WBoss = 0, WGuard = 1, WBat = 2, WM5 = 5, WStrong = 7;
    static readonly int[] W5 = { WBoss, WGuard, WBat, WM5, WStrong };
    static readonly (string Name, I.W W)[] EvWaves = { ("ボス", I.Boss), ("近衛", I.Guard), ("大隊", I.Bat), ("本編 第5波", I.M5) };

    /// <summary>§4 の台: ハギの試遊の 6 台（ハギは規定）。席はハギの席。</summary>
    internal static S388.Board[] Boards()
    {
        var v = Presets.Versions;
        Formation Of(string n) => v.First(r => r.Name == n).F;
        return new[]
        {
            new S388.Board("試遊・ハギ 台B", Of("試遊・ハギ 台B"), 3, "ハギ"),
            new S388.Board("試遊・死神と墓守", Of("試遊・死神と墓守"), 0, "ハギ"),
            new S388.Board("試遊・ハギ キャリー", Of("試遊・ハギ キャリー"), 3, "ハギ"),
            new S388.Board("試遊・ハギ 哀歌", Of("試遊・ハギ 哀歌"), 3, "ハギ"),
            new S388.Board("版・移動 ハギ", Of("版・移動 ハギ"), 1, "ハギ"),
            new S388.Board("版・大物 ハギ", Of("版・大物 ハギ"), 1, "ハギ"),
        };
    }

    static double[,] CGrid((string Name, Formation F)[] rows, int seeds)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw, wins = 0;
            for (int s = 0; s < seeds; s++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / seeds;
        });
        return g;
    }

    // ---------------------------------------------------------------------------------
    // 1戦の出来事を数える
    // ---------------------------------------------------------------------------------
    static bool R(BattleEvent x, string label) => x.Kind == BattleEventKind.Reap && x.Text == label;
    /// <summary>数える欄（見出し ／ 1戦の出来事から数える関数）。<c>id</c> は InstanceId → Def.Id。</summary>
    internal static readonly (string Head, Func<BattleEvent, Dictionary<int, string>, bool> Hit)[] Cols =
    {
        ("刈る（振る前）", (x, _) => R(x, ReapLabels.Reap) && x.Slot == ReapLabels.KindTurn),
        ("刈る（追撃の振る前）", (x, _) => R(x, ReapLabels.Reap) && x.Slot == ReapLabels.KindChase),
        ("刈る（当たった後）", (x, _) => R(x, ReapLabels.Reap) && x.Slot == ReapLabels.KindAfter),
        ("刈る（当たった後・追撃）", (x, _) => R(x, ReapLabels.Reap) && x.Slot == ReapLabels.KindAfterChase),
        ("刈る（列ごと）", (x, _) => R(x, ReapLabels.Reap) && x.Slot == ReapLabels.KindColumn),
        ("刈る（二度斬りの 2 発目）", (x, _) => R(x, ReapLabels.Reap) && x.Slot == ReapLabels.KindTwin),
        ("二度斬る（斬撃）", (x, _) => R(x, ReapLabels.Twin) && x.Amount == 0),
        ("二度斬る（刈り）", (x, _) => R(x, ReapLabels.Twin) && x.Amount == 1),
        ("再行動", (x, _) => R(x, ReapLabels.Encore)),
        ("追撃", (x, _) => R(x, ReapLabels.Pursue)),
        ("印", (x, _) => R(x, ReapLabels.Mark)),
        ("印 5 本", (x, _) => R(x, ReapLabels.Mark) && x.Amount >= HagiDepthTrait.MarkCap),
        ("線", (x, _) => R(x, ReapLabels.Line)),
        ("魂 +", (x, _) => R(x, ReapLabels.Soul) && x.Amount > 0),
        ("薄れ", (x, _) => R(x, ReapLabels.Soul) && x.Amount < 0),
        ("段 3", (x, _) => R(x, ReapLabels.Stage) && x.Amount == 1),
        ("段 6", (x, _) => R(x, ReapLabels.Stage) && x.Amount == 2),
        ("段 9", (x, _) => R(x, ReapLabels.Stage) && x.Amount == 3),
        ("段の重み", (x, _) => R(x, ReapLabels.Weight)),
        ("印の重み", (x, _) => R(x, ReapLabels.Heavy)),
        ("巻き込み", (x, id) => x.Kind == BattleEventKind.Damage && x.FriendlyFire && !x.Relayed && x.ActorId is int a && id.GetValueOrDefault(a) == "hagi"),
    };

    internal static (BattleResult R, Dictionary<int, string> Id) One(Formation f, I.W w, int seed) { var (r, id, _) = OneF(f, w, seed); return (r, id); }
    /// <summary>1戦を verbose で回し、InstanceId → Def.Id と、最初に立っていた敵の InstanceId（召喚は含まない）を返す。</summary>
    internal static (BattleResult R, Dictionary<int, string> Id, HashSet<int> Foe) OneF(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var id = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Id);
        return (r, id, e.Select(u => u.InstanceId).ToHashSet());
    }

    // ---------------------------------------------------------------------------------
    // §2 段0
    // ---------------------------------------------------------------------------------
    static void Zero()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §2 段0 —— ハギの R50（斬った直後に刈る ＋ 線の上限 5 割）を規定に（旧は `UnitCatalog.HagiH404`・H123 ＋ B ＋ T1）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var now = CGrid(rows, CompareSeeds);
        var old = CGrid(rows.Select(r => (r.Name, F: Pin404(r.F))).ToArray(), CompareSeeds);
        var rcol = CGrid(rows.Select(r => (r.Name, F: I.Swap(Pin404(r.F), "hagi", UnitCatalog.HagiReapR50))).ToArray(), CompareSeeds);
        var head = S390.BalanceMap(S390.GitShow($"{HeadSha}:docs/balance.md"));
        int nw = EnemyCatalog.Stages.Count, moved = 0, sameR = 0, sameOld = 0, cells = 0, info0 = 0, info1 = 0, over0 = 0, over1 = 0;
        var mv = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
        {
            bool o95 = false, n95 = false;
            for (int w = 0; w < nw; w++)
            {
                cells++;
                string c = head.TryGetValue(rows[ri].Name, out var a) ? a[w].TrimEnd('%') : "?";
                if (c != F1(now[ri, w])) { moved++; mv.Add($"{rows[ri].Name} 第{w + 1}波 {c} → {F1(now[ri, w])}"); }
                if (now[ri, w] == rcol[ri, w]) sameR++;
                if (c == F1(old[ri, w])) sameOld++;
                if (w >= 1) { if (old[ri, w] > 0 && old[ri, w] < 100) info0++; if (now[ri, w] > 0 && now[ri, w] < 100) info1++; }
                if (w == 4) { o95 = old[ri, w] > 95; n95 = now[ri, w] > 95; }
            }
            if (o95) over0++; if (n95) over1++;
        }
        Console.WriteLine($"- 第404期の HEAD（{HeadSha}）の `docs/balance.md` と違うセル **{moved}**{(mv.Count > 0 ? "（" + string.Join("・", mv) + "）" : "")}");
        Console.WriteLine($"- 第404期の R50 の列（ハギを `HagiReapR50` に替えた全行）と同じセル {sameR} ／ {cells}・旧（`HagiH404`）に戻した全行が HEAD の表と同じセル {sameOld} ／ {cells}");
        Console.WriteLine($"- 情報セル（全 {rows.Length} 行・第2〜5波の 0 < x < 100）{info0} → {info1}・第五波 95% 超の行 {over0} → {over1}（(G9) の注意は 2 行以上増えたとき）");
        Console.WriteLine();
        Console.WriteLine($"（seed 0..{Seeds - 1}・セルは「勝率・決着T・倒れた編成の駒」・ボスの T はボスの倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | ボス | 近衛 | 大隊 | 本編 第5波 | 強 第2〜5波 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var b in Boards())
            foreach (var (cn, f) in new[] { ("規定（第405期）", b.F), ("第404期（旧）", Pin404(b.F)) })
            {
                var m = new S377.B[W8.Length];
                Parallel.ForEach(W5, wi => m[wi] = S377.Measure(f, W8[wi].Ws, Seeds));
                Console.WriteLine($"| {(cn.StartsWith("規定") ? $"{b.Name}（{S307.Seats(b.F)}）" : "")} | {cn} | {string.Join(" | ", W5.Select(w => m[w].Cell))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4: 出来事の回数 ＋ 単発の例
    // ---------------------------------------------------------------------------------
    static void Events()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §4 ハギの試遊の台 × 物差し × seed 0..{EvSeeds - 1}——1戦あたりの出来事の回数（`BattleEventKind.Reap`・巻き込みは `Damage`）");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- **{b.Name}**: {S307.Seats(b.F)}");
        Console.WriteLine();
        Console.WriteLine("## 1. 1戦あたりの回数");
        Console.WriteLine();
        Console.WriteLine("| 台 | 物差し | 勝率 | 決着T | " + string.Join(" | ", Cols.Select(c => c.Head)) + " |");
        Console.WriteLine("|---|---|--:|--:|" + string.Concat(Cols.Select(_ => "--:|")));
        foreach (var b in boards)
            foreach (var (wn, w) in EvWaves)
            {
                var sum = new long[Cols.Length]; int wins = 0; long turns = 0;
                var lk = new object();
                Parallel.For(0, EvSeeds, sd =>
                {
                    var (r, id) = One(b.F, w, sd);
                    var c = new long[Cols.Length];
                    foreach (var x in r.Events) for (int k = 0; k < Cols.Length; k++) if (Cols[k].Hit(x, id)) c[k]++;
                    lock (lk) { for (int k = 0; k < Cols.Length; k++) sum[k] += c[k]; if (r.PlayerWon) wins++; turns += r.Turns; }
                });
                Console.WriteLine($"| {b.Name} | {wn} | {F1(100.0 * wins / EvSeeds)} | {F1((double)turns / EvSeeds)} | {string.Join(" | ", sum.Select(v => F1((double)v / EvSeeds)))} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 2. 単発の再生の例（seed 0・ターンごとに「出来事 × 回数」）");
        Console.WriteLine();
        foreach (var b in boards)
            foreach (var (wn, w) in EvWaves.Take(2))
            {
                var (r, id) = One(b.F, w, 0);
                Console.WriteLine($"- **{b.Name} × {wn} × seed 0**（{(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T）");
                foreach (var t in r.Events.Select(x => x.Turn).Distinct().OrderBy(t => t))
                {
                    var parts = Cols.Select((c, k) => (c.Head, N: r.Events.Count(x => x.Turn == t && c.Hit(x, id)))).Where(z => z.N > 0).Select(z => $"{z.Head} {z.N}").ToList();
                    if (parts.Count > 0) Console.WriteLine($"  - T{t}: {string.Join("・", parts)}");
                }
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・ハギ キャリー";
        int wi = args.Length > 4 && int.TryParse(args[4], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 5 && int.TryParse(args[5], out int s0) ? s0 : 0;
        var all = Boards();
        var b = all.FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal)) ?? all[0];
        var (r, id) = One(b.F, W8[wi].Ws[0], seed);
        Console.WriteLine($"# {b.Name} × {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
        Console.WriteLine();
        Console.WriteLine("## ハギの出来事の並び（`Reap` と、ハギの `Attack` ／ `Damage`）");
        foreach (var x in r.Events)
        {
            bool hagi = x.ActorId is int a && id.GetValueOrDefault(a) == "hagi";
            if (x.Kind == BattleEventKind.Reap) Console.WriteLine($"T{x.Turn} Reap {x.Text} → {(x.TargetId is int t ? id.GetValueOrDefault(t) : "—")} Amount {x.Amount} Slot {x.Slot} Remaining {x.StatusRemaining?.ToString() ?? "—"}{(x.PartnerId is int p ? $" Partner {id.GetValueOrDefault(p)}" : "")}");
            else if (hagi && x.Kind is BattleEventKind.Attack or BattleEventKind.Damage) Console.WriteLine($"T{x.Turn} {x.Kind} → {(x.TargetId is int t ? id.GetValueOrDefault(t) : "—")} {x.Amount}{(x.Kind == BattleEventKind.Damage ? $" (HP {x.HpAfter})" : "")}{(x.FriendlyFire ? " 巻き込み" : "")}{(x.Pattern is { } pt ? $" {pt}" : "")}");
        }
    }
}
