using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S381 = Sh381Diag;

// =====================================================================================
// sh382 —— 第382期【軽い期】段0: 魂を分けるヴェル（第381期の S2）を規定に ／ 本体: 弔いの楽士（版 D1 ／ D1n）をトモ ／ ドハ ／ ガン ／ 素体と同じ枠で比べる。
// 指示書は design/PHASE382_DIRGE_SPEC.md、報告は design/PHASE382_DIRGE.md。
//
//     dotnet run --project BattleSim -c Release 0 sh382 stage0    # §2-3: `compare` 64 行 旧（`Pin381`）→ 新（S2）・第381期の S2 の列と同じか・死の台の代表 5 × 5 波 旧 → 新（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh382 cmp       # §3: 4 台 × 候補 6（D1 ／ D1n ／ トモ ／ ドハ ／ ガン ／ 素体）× 8 物差し（1〜3 は並びを探す）
//     dotnet run --project BattleSim -c Release 0 sh382 check     # 自己検査
//
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p382/predict.md`）。素体の替え ／ 並びの探索の台は器具の中だけ（試遊の台に足す「版・楽士 死」を除く）。
// =====================================================================================
static partial class Sh382Diag
{
    internal const string PredictFile = ".tmp/p382/predict.md";
    const string Tag = "第382期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "stage0" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh382: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "stage0": Stage0(); return;
            case "cmp": Cmp(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh382: モードは stage0 / cmp / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long Xv(S377.B b, string k) => b.X.GetValueOrDefault(k);
    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] Key4 = { 0, 1, 2, 5 };

    // ---------------------------------------------------------------------------------
    // 候補と台
    // ---------------------------------------------------------------------------------
    /// <summary>素体（器具の中だけ・楽士と同じ体 HP 40 ／ 攻 3 ／ 速 9 ／ 据置・札なし・ふつうに殴る）。</summary>
    internal static readonly UnitDef Plain = new()
    {
        Id = "plain382", Name = "素体", MaxHp = UnitCatalog.Bard382.MaxHp, Attack = UnitCatalog.Bard382.Attack, Speed = UnitCatalog.Bard382.Speed, Advances = false,
        Traits = Array.Empty<TraitId>(), PlusText = "", MinusText = "",
    };
    internal static readonly (string Name, UnitDef D)[] CandOf =
    {
        ("D1", UnitCatalog.Bard382), ("D1n", UnitCatalog.Bard382n), ("トモ", UnitCatalog.Tomo), ("ドハ", UnitCatalog.Doha), ("ガン", UnitCatalog.Gan), ("素体", Plain),
    };
    /// <summary>台 1〜3 の固定の1枚（芯 リィカ ／ ヴェル ／ ムグ ＋ この1枚 ＋ 候補）。</summary>
    internal static readonly (string Name, UnitDef D)[] Partners = { ("スィド ＋ 候補", UnitCatalog.Sid), ("ゴルム ＋ 候補", UnitCatalog.Golm), ("ボルグ ＋ 候補", UnitCatalog.Borg) };
    internal static readonly UnitDef[] Core = { UnitCatalog.Rica, UnitCatalog.Vel, UnitCatalog.Mug };
    /// <summary>台 4: ゾトの席を候補に替えた死の連鎖（前1 候補 ／ 前3 ゴルム ／ 中央 ムグ ／ 後1 ヴェル ／ 後3 リィカ・並びは固定）。</summary>
    internal static Formation ChainWith(UnitDef c) => Formation.Build(front1: c, front3: UnitCatalog.Golm, center: UnitCatalog.Mug, back1: UnitCatalog.Vel, back3: UnitCatalog.Rica);

    static readonly int[][] Perms = Permutations(5).ToArray();
    static IEnumerable<int[]> Permutations(int n)
    {
        if (n == 1) { yield return new[] { 0 }; yield break; }
        foreach (var p in Permutations(n - 1))
            for (int k = 0; k < n; k++) { var q = p.ToList(); q.Insert(k, n - 1); yield return q.ToArray(); }
    }
    internal static Formation Board(UnitDef[] five, int[] perm) { var f = new Formation(); for (int i = 0; i < 5; i++) f[perm[i]] = five[i]; return f; }
    static string SeatStr(Formation f) => string.Join(" ／ ", Enumerable.Range(0, 5).Select(s => f[s] is { } d ? S307.Short(d) : "—"));

    /// <summary>並びの探索（指示書 §3-3: 120 並び × ボス ／ 近衛 ／ 大隊・seed 0..9）。並べる順: 3 波の勝率の平均 → 倒れた編成の駒の少ない方 → 並びの番号。</summary>
    internal static (Formation F, double Mean) BestSeat(UnitDef partner, UnitDef cand)
    {
        var five = Core.Concat(new[] { partner, cand }).ToArray();
        var sc = new (double Mean, double Deaths)[Perms.Length];
        Parallel.For(0, Perms.Length, k =>
        {
            var f = Board(five, Perms[k]); double w = 0; long d = 0, n = 0;
            foreach (var wave in new[] { I.Boss, I.Guard, I.Bat })
                for (int s = 0; s < 10; s++) { var (r, p, _) = I.Raw(f, wave, s, census: false); if (r.PlayerWon) w++; d += p.Count(u => u.LastDeathTurn > 0); n++; }
            sc[k] = (100.0 * w / n, (double)d / n);
        });
        int best = Enumerable.Range(0, Perms.Length).OrderByDescending(k => sc[k].Mean).ThenBy(k => sc[k].Deaths).First();
        return (Board(five, Perms[best]), sc[best].Mean);
    }

    // ---------------------------------------------------------------------------------
    // この期の計数
    // ---------------------------------------------------------------------------------
    /// <summary>第381期の計数（起き上がり・層の出どころ・勇者の HP ほか）に、楽士 ／ トモ ／ ドハ ／ ガンの補助を足す（受け手は編成の駒の短い名前ごと）。</summary>
    internal static void Extra(BattleResult r, List<UnitState> p, List<UnitState> e, S377.B b)
    {
        S381.Extra(r, p, e, b);
        var x = b.X;
        void Add(string k, long v) { if (v != 0) x[k] = x.GetValueOrDefault(k) + v; }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Bard382.Id, out var bt))
        { Add("songs", bt.R382Songs); Add("noDeath", bt.R382NoDeath); Add("noOne", bt.R382NoOne); Add("inGift", bt.R382InGift); Add("echoes", bt.R382Echoes); Add("skipped", bt.R382Skipped); }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Tomo.Id, out var tt)) { Add("tlFires", tt.TaillightFires); Add("tlYields", tt.TaillightYields); Add("tlYieldDmg", tt.TaillightYieldDamage); }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Doha.Id, out var dt)) { Add("pushes", dt.Pushes); Add("pushPassed", dt.PushPassed); }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Gan.Id, out var gt)) { Add("revFires", gt.ReveilleFires); Add("revMiss", gt.ReveilleMisses); }
        foreach (var id in p.Select(u => u.Def.Id).Distinct())
        {
            if (!r.TallyByUnit.TryGetValue(id, out var t)) continue;
            string nm = S307.Short(p.First(u => u.Def.Id == id).Def);
            Add($"enc:{nm}", t.R382EncoreTurns); Add($"encD:{nm}", t.R382EncoreDealt);
            Add($"lit:{nm}", t.TaillightLitReceived); Add($"pushGot:{nm}", t.PushGot); Add($"giftD:{nm}", t.ShareGiftDealt); Add($"woken:{nm}", t.ReveilleWoken);
        }
        foreach (var id in e.Select(u => u.Def.Id).Distinct())
            if (r.TallyByUnit.TryGetValue(id, out var t)) { Add("echoTurns", t.R382EchoTurns); Add("echoDealt", t.R382EchoDealt); }
    }
    static S377.B M(Formation f, I.W[] ws, int seeds) => S377.Measure(f, ws, seeds, Extra);
    static S377.B[] Eight(Formation f) { var res = new S377.B[W8.Length]; Parallel.For(0, W8.Length, wi => res[wi] = M(f, W8[wi].Ws, 100)); return res; }

    /// <summary>補助を受けた駒（キー接頭辞ごとに 1戦あたり）。</summary>
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

    // ---------------------------------------------------------------------------------
    // §2-3 段0
    // ---------------------------------------------------------------------------------
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

    internal static (string Name, Formation F)[] DeathReps() => new[]
    {
        ("試遊・死 探索1位", Presets.DeathSeek380()), ("試遊・死の連鎖", Presets.DeathChain377()), ("試遊・死 ソム", Presets.DeathSom379()),
        ("第380期 2 位（スィド＋ドハ）", S381.Seat(S381.Top10[1].Seats)), ("第380期 3 位（ゴルム＋スィド）", S381.Seat(S381.Top10[2].Seats)),
    };

    static void Stage0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = CGrid(rows.Select(r => (r.Name, Pin381(r.F))).ToArray()); var g = CGrid(rows);
        var s2 = CGrid(rows.Select(r => (r.Name, I.Swap(Pin381(r.F), "vel", UnitCatalog.VelS2))).ToArray());
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        var all = Enumerable.Range(0, rows.Length);
        int Over95(double[,] x) => all.Count(r => x[r, last] > 95.0);
        int Info(double[,] x, IEnumerable<int> rs) => rs.Sum(r => Enumerable.Range(1, nw - 1).Count(w => x[r, w] > 0 && x[r, w] < 100));
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(all.Average(r => x[r, w]))));
        int up = 0, dn = 0, sameS2 = 0; var changed = new List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            var cells = Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).ToList();
            up += cells.Count(w => g[r, w] > b[r, w]); dn += cells.Count(w => g[r, w] < b[r, w]);
            if (cells.Count > 0) changed.Add($"{rows[r].Name}: " + string.Join("・", cells.Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
            for (int w = 0; w < nw; w++) if (g[r, w] != s2[r, w]) sameS2++;
        }
        Console.WriteLine($"# {Tag} §2-3 段0 —— `compare` 64 行: 旧（第381期の規定・ヴェルは `VelH381`・`Pin381`）→ 新（魂を分けるヴェル）（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| | 動いた行 ／ セル（上がり ／ 落ち）| 主判定19行の第五波 | 全行の第1〜5波の平均 | 第五波 95% 超の行 | 情報セル（全行 ／ 主判定） |");
        Console.WriteLine("|---|---|--:|---|--:|---|");
        Console.WriteLine($"| 前 | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} | {Over95(b)} | {Info(b, all)} ／ {Info(b, pr)} |");
        Console.WriteLine($"| 後 | {changed.Count} ／ {up + dn}（{up} ／ {dn}）| {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} | {Over95(g)} | {Info(g, all)} ／ {Info(g, pr)} |");
        Console.WriteLine();
        Console.WriteLine($"第381期の S2 の列（旧の行のヴェルを `VelS2` に替えた 64 行）と新の 64 行の違うセル: **{sameS2}**（320 セル中）。");
        Console.WriteLine();
        foreach (var l in changed) Console.WriteLine("- " + l);
        Console.WriteLine();

        var reps = DeathReps();
        var waves = new (string Name, I.W[] Ws)[] { ("ボス", new[] { I.Boss }), ("近衛", new[] { I.Guard }), ("大隊", new[] { I.Bat }), ("本編 第5波", new[] { I.M5 }), ("強 第2〜5波", I.StrongWaves) };
        var rg = new S377.B[reps.Length, 2, waves.Length];
        Parallel.For(0, rg.Length, k =>
        {
            int bi = k / (2 * waves.Length), vi = k / waves.Length % 2, wi = k % waves.Length;
            rg[bi, vi, wi] = M(vi == 0 ? Pin381(reps[bi].F) : reps[bi].F, waves[wi].Ws, 100);
        });
        Console.WriteLine("## 死の台の代表 × 5 波（seed 0..99）——勝率・決着T・倒れた編成の駒（起き上がる死も1回）");
        Console.WriteLine();
        Console.WriteLine("| 台 | ヴェル | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "---|")));
        for (int bi = 0; bi < reps.Length; bi++)
            for (int vi = 0; vi < 2; vi++)
                Console.WriteLine($"| {(vi == 0 ? reps[bi].Name : "")} | {(vi == 0 ? "旧" : "新（S2）")} | {string.Join(" | ", Enumerable.Range(0, waves.Length).Select(wi => rg[bi, vi, wi].Cell))} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3 比べ
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int nb = 4, nc = CandOf.Length;
        var boards = new Formation[nb, nc]; var searchMean = new double[nb, nc];
        for (int bi = 0; bi < 3; bi++)
            for (int ci = 0; ci < nc; ci++) (boards[bi, ci], searchMean[bi, ci]) = BestSeat(Partners[bi].D, CandOf[ci].D);
        for (int ci = 0; ci < nc; ci++) { boards[3, ci] = ChainWith(CandOf[ci].D); searchMean[3, ci] = double.NaN; }
        string[] bname = Partners.Select(p => p.Name).Append("死の連鎖（ゾト → 候補）").ToArray();
        var g = new S377.B[nb, nc][];
        for (int bi = 0; bi < nb; bi++) for (int ci = 0; ci < nc; ci++) g[bi, ci] = Eight(boards[bi, ci]);
        double Mean4(S377.B[] x) => Key4.Average(w => x[w].Win);
        double Dead4(S377.B[] x) => Key4.Average(w => (double)x[w].FormDeaths / x[w].N);

        Console.WriteLine($"# {Tag} §3 弔いの楽士（D1 ／ D1n）× トモ ／ ドハ ／ ガン ／ 素体 —— 芯 リィカ ／ ヴェル ／ ムグ（規定・ヴェルは段0 の後）");
        Console.WriteLine();
        Console.WriteLine("台 1〜3 は候補ごとに並びを探した（120 並び × ボス ／ 近衛 ／ 大隊・seed 0..9・3 波の平均 → 倒れた数）。台 4 は並び固定。測定は seed 0..99 × 8 物差し（verbose）。");
        Console.WriteLine("セル: 勝率・決着T・倒れた編成の駒（起き上がる死も1回と数える・本当の死は §5）。");
        Console.WriteLine();
        Console.WriteLine("## 1. 台 × 候補（選ばれた並び・8 物差し）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 候補 | 並び（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3）| 探索 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4波の平均 |");
        Console.WriteLine("|---|---|---|--:|" + string.Concat(W8.Select(_ => "---|")) + "--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? bname[bi] : "")} | {CandOf[ci].Name} | {SeatStr(boards[bi, ci])} | {(double.IsNaN(searchMean[bi, ci]) ? "—" : F1(searchMean[bi, ci]))} | {string.Join(" | ", g[bi, ci].Select(x => x.Cell))} | {F1(Mean4(g[bi, ci]))} |");
        Console.WriteLine();
        Console.WriteLine("強い条件の平均（4 台）: " + string.Join(" ／ ", Enumerable.Range(0, nc).Select(ci => $"{CandOf[ci].Name} {F1(Enumerable.Range(0, nb).Average(bi => g[bi, ci][7].Win))}")) + "。");
        Console.WriteLine();
        Console.WriteLine("4 波の平均（4 台）: " + string.Join(" ／ ", Enumerable.Range(0, nc).Select(ci => $"{CandOf[ci].Name} {F1(Enumerable.Range(0, nb).Average(bi => Mean4(g[bi, ci])))}")) + "。");
        Console.WriteLine();

        Console.WriteLine("## 2. ボスの倒しT ／ 隊の1ターンの最大の与ダメ（戦ごとの最大の平均）——近衛 ／ 大隊の1ターンの最大も");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", CandOf.Select(c => c.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(CandOf.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {bname[bi]} | {string.Join(" | ", Enumerable.Range(0, nc).Select(ci => { var x = g[bi, ci]; return $"{x[0].WinTs}T ／ {F0((double)Xv(x[0], "teamMax") / x[0].N)}（近衛 {F0((double)Xv(x[1], "teamMax") / x[1].N)}・大隊 {F0((double)Xv(x[2], "teamMax") / x[2].N)}）"; }))} |");
        Console.WriteLine();

        Console.WriteLine("## 3. 楽士（1戦あたり: 奏でた ／ 倒れていなくて殴った ／ 敵を動かした（マイナス）・動かされた駒 ／ 再行動で入った与ダメ ／ マイナスの敵の手番で味方が受けた傷）");
        Console.WriteLine();
        foreach (int ci in new[] { 0, 1 })
            for (int bi = 0; bi < nb; bi++)
                Console.WriteLine($"- **{bname[bi]} × {CandOf[ci].Name}**: " + string.Join(" ／ ", Key4.Select(w =>
                {
                    var x = g[bi, ci][w];
                    long encD = x.X.Where(kv => kv.Key.StartsWith("encD:")).Sum(kv => kv.Value);
                    return $"{W8[w].Name} {Per(Xv(x, "songs"), x.N)} ／ {Per(Xv(x, "noDeath"), x.N)} ／ {Per(Xv(x, "echoes"), x.N)}・[{Got(x, "enc")}]・与ダメ {F0((double)encD / x.N)}・敵の手番 {Per(Xv(x, "echoTurns"), x.N)} で {F0((double)Xv(x, "echoDealt") / x.N)}";
                })));
        Console.WriteLine();

        Console.WriteLine("## 4. トモ ／ ドハ ／ ガンの補助（1戦あたり・受けた駒）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            Console.WriteLine($"- **{bname[bi]} × トモ**（灯した ／ 手番を譲った ／ 譲った手番の与ダメ・灯を受けた駒）: " + string.Join(" ／ ", Key4.Select(w => { var x = g[bi, 2][w]; return $"{W8[w].Name} {Per(Xv(x, "tlFires"), x.N)} ／ {Per(Xv(x, "tlYields"), x.N)} ／ {F0((double)Xv(x, "tlYieldDmg") / x.N)}・[{Got(x, "lit")}]"; })));
            Console.WriteLine($"- **{bname[bi]} × ドハ**（背を押した ／ 渡した力・押された駒 ／ その手番の与ダメ）: " + string.Join(" ／ ", Key4.Select(w => { var x = g[bi, 3][w]; return $"{W8[w].Name} {Per(Xv(x, "pushes"), x.N)} ／ {Per(Xv(x, "pushPassed"), x.N)}・[{Got(x, "pushGot")}] ／ [{Got(x, "giftD")}]"; })));
            Console.WriteLine($"- **{bname[bi]} × ガン**（叩き起こした ／ 起こせなかった・起こされた駒）: " + string.Join(" ／ ", Key4.Select(w => { var x = g[bi, 4][w]; return $"{W8[w].Name} {Per(Xv(x, "revFires"), x.N)} ／ {Per(Xv(x, "revMiss"), x.N)}・[{Got(x, "woken")}]"; })));
        }
        Console.WriteLine();

        Console.WriteLine("## 5. 誰が何Tに倒れたか（各台の最良の候補（4 波の平均 → 倒れた数）と素体・ボス ／ 近衛 ／ 大隊・「死」＝ 本当の死 ／「起」＝ 起き上がり）");
        Console.WriteLine();
        var bestOf = new int[nb];
        for (int bi = 0; bi < nb; bi++)
        {
            bestOf[bi] = Enumerable.Range(0, nc - 1).OrderByDescending(ci => Mean4(g[bi, ci])).ThenBy(ci => Dead4(g[bi, ci])).First();
            foreach (int ci in new[] { bestOf[bi], nc - 1 }.Distinct())
                Console.WriteLine($"- **{bname[bi]} × {CandOf[ci].Name}**{(ci == bestOf[bi] ? "（最良）" : "")}: " + string.Join(" ／ ", new[] { 0, 1, 2 }.Select(w => $"{W8[w].Name} {FellSplit(g[bi, ci][w], boards[bi, ci])}")));
        }
        Console.WriteLine();

        Console.WriteLine("## 6. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満の勝ち ／ 30T の打ち切りが素体より 5pt 以上増えた ／ 層 20 超 ／ int のあふれ）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                var x = g[bi, ci]; var x0 = g[bi, nc - 1]; string at = $"{bname[bi]} × {CandOf[ci].Name}";
                foreach (int wi in new[] { 1, 2 }) if (x[wi].Win1T * 20 >= x[wi].N && x[wi].Win1T > 0) signs.Add($"{at} × {W8[wi].Name}: 1T 決着 {Pct(x[wi].Win1T, x[wi].N)}%");
                if (x[0].BossFast > 0) signs.Add($"{at}: ボス 4T 未満の勝ち {Pct(x[0].BossFast, x[0].N)}%");
                for (int wi = 0; wi < W8.Length; wi++)
                {
                    if (100.0 * x[wi].Cap / x[wi].N - 100.0 * x0[wi].Cap / x0[wi].N >= 5) signs.Add($"{at} × {W8[wi].Name}: 30T の打ち切り {Pct(x0[wi].Cap, x0[wi].N)} → {Pct(x[wi].Cap, x[wi].N)}%");
                    if (x[wi].Overflow > 0) signs.Add($"{at} × {W8[wi].Name}: あふれの兆し {x[wi].Overflow} 件");
                }
                foreach (int wi in Key4) if (Xv(x[wi], "peak20") > 0) signs.Add($"{at} × {W8[wi].Name}: 層 20 超 {Pct(Xv(x[wi], "peak20"), x[wi].N)}%（素体 {Pct(Xv(x0[wi], "peak20"), x0[wi].N)}%）");
            }
        if (signs.Count == 0) Console.WriteLine("- 無い。");
        foreach (var s in signs) Console.WriteLine("- " + s);
        Console.WriteLine();

        int bestD1 = Enumerable.Range(0, 3).OrderByDescending(bi => Mean4(g[bi, 0])).ThenBy(bi => Dead4(g[bi, 0])).ThenByDescending(bi => g[bi, 0][7].Win).First();
        Console.WriteLine($"D1 がいちばん良かった台（1〜3・4 波の平均 → 倒れた数 → 強い条件）: {bname[bestD1]}（{SeatStr(boards[bestD1, 0])}）——Presets の「版・楽士 死」に置く。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
