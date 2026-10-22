using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S378 = Sh378Diag;

// =====================================================================================
// sh379 —— 第379期【軽い期】段0: 目覚めたリィカ（第378期の ELU のリィカ側）を規定に ／ 本体: ムグが胞子を撒く（版 撒き1 ／ 撒き全 ／ 育つ）／ リィカ＋ソムの台。
// 指示書は design/PHASE379_MUG_SOW_SPEC.md ／ 報告は design/PHASE379_MUG_SOW.md。ムグの規定は変えない。
//
//     dotnet run --project BattleSim -c Release 0 sh379 stage0    # §2-3: `compare` 64 行 旧 → 新（seed 0..199）・死の4台 × 5 波 旧 → 新（seed 0..99）・寄せ集め 50 台 × 8 物差し（seed 0..19）
//     dotnet run --project BattleSim -c Release 0 sh379 vers      # §3-3: 4 台（死の連鎖 ／ 継ぎ当て ／ 死・ソム ／ 死・ゾト抜き）× ムグ（規定 ／ 撒き1 ／ 撒き全 ／ 育つ）× 8 物差し（seed 0..99・verbose）
//     dotnet run --project BattleSim -c Release 0 sh379 compare <撒き1|撒き全|育つ>   # `compare` 64 行 × 版（ムグのいる行だけを回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh379 check     # 自己検査
//
// 1戦の計数は第377期の器具（`Sh377Diag.One`）＋ 第378期の計数（`Sh378Diag.Extra`）＋ この期の計数（`Extra`）。**予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p379/predict.md`）。
// =====================================================================================
static partial class Sh379Diag
{
    internal const string PredictFile = ".tmp/p379/predict.md";
    const string Tag = "第379期";
    const int CompareSeeds = 200, HeroT = 10, LayerT = 8;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "stage0" or "vers" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh379: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "stage0": Stage0(); return;
            case "vers": Versions(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "撒き全"); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh379: モードは stage0 / vers / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long Xv(S377.B b, string k) => b.X.GetValueOrDefault(k);

    // ---------------------------------------------------------------------------------
    // 版 ／ 台
    // ---------------------------------------------------------------------------------
    internal static readonly string[] VerNames = { "ムグ規定", "撒き1", "撒き全", "育つ" };
    internal static readonly UnitDef[] MugOf = { UnitCatalog.Mug, UnitCatalog.MugSow1, UnitCatalog.MugSowAll, UnitCatalog.MugSowGrow };
    internal static Formation Apply(Formation f, int vi) => vi == 0 ? f : I.Swap(f, "mug", MugOf[vi]);
    internal static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;

    /// <summary>§3-2 の台（規定のリィカ ＝ 目覚めたリィカ）。死・ゾト抜きはゾトを素体に替えた並び（器具の中だけ）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("死の連鎖 (リィカ軸)", Row("死の連鎖 (リィカ軸)")),
        ("継ぎ当て×死の連鎖（参考）", Row("継ぎ当て×死の連鎖")),
        ("死・ソム", Presets.DeathSom379()),
        ("死・ゾト抜き（比べ用）", I.Swap(Row("死の連鎖 (リィカ軸)"), "zoto", I.Plain(UnitCatalog.Zoto))),
    };

    // ---------------------------------------------------------------------------------
    // この期の計数
    // ---------------------------------------------------------------------------------
    /// <summary>撒いた胞子 ／ 育った胞子 ／ 若いムグが撒いた胞子 ／ 撒けずに殴った手番・層の出どころ（リィカが生きている間に倒れた味方: 編成 ／ 胞子 ／ 若いムグ ／ 亡者）・層 20 超。</summary>
    internal static void Extra(BattleResult r, List<UnitState> p, List<UnitState> e, S377.B b)
    {
        S378.Extra(r, p, e, b);
        var x = b.X;
        if (r.TallyByUnit.TryGetValue("mug", out var mt)) { x["sown"] = mt.R379Sown; x["sownT"] = mt.R379SowTurnSum; x["sowNoSeat"] = mt.R379SowNoSeat; }
        if (r.TallyByUnit.TryGetValue("spore", out var st)) x["grown"] = st.R379Grown;
        if (r.TallyByUnit.TryGetValue(UnitCatalog.YoungMug.Id, out var yt)) { x["ySown"] = yt.R379Sown; x["yNoSeat"] = yt.R379SowNoSeat; }
        if (r.TallyByUnit.TryGetValue("rica", out var rt) && rt.NecroPeak > 20) x["peak20"] = 1;
        var form = p.ToDictionary(u => u.InstanceId);
        var summoned = new Dictionary<int, string>();
        int ricaId = p.FirstOrDefault(u => u.Def.Id == "rica")?.InstanceId ?? -1;
        bool ricaAlive = ricaId >= 0;
        foreach (var ev in r.Events)
        {
            if (ev.Kind == BattleEventKind.Summon && ev.Team == BattleContext.PlayerTeam && ev.TargetId is int sid) summoned[sid] = ev.Text ?? "";
            else if (ev.Kind == BattleEventKind.Revive && ev.TargetId == ricaId) ricaAlive = true;
            else if (ev.Kind == BattleEventKind.Death && ev.TargetId is int dt)
            {
                if (dt == ricaId) { ricaAlive = false; continue; }
                if (!ricaAlive) continue;
                string key = form.ContainsKey(dt) ? "srcForm" : summoned.TryGetValue(dt, out var nm) ? nm == UnitCatalog.Spore.Name ? "srcSpore" : nm == UnitCatalog.YoungMug.Name ? "srcYoung" : nm == UnitCatalog.Wight.Name ? "srcWight" : "" : "";
                if (key != "") x[key] = x.GetValueOrDefault(key) + 1;
            }
        }
    }

    static S377.B M(Formation f, I.W[] ws, int seeds) => S377.Measure(f, ws, seeds, Extra);

    // ---------------------------------------------------------------------------------
    // §2-3 段0
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareTable(string title, (string Name, Formation F)[] baseRows, (string Name, Formation F)[] newRows)
    {
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = CGrid(baseRows); var g = CGrid(newRows);
        var pr = Enumerable.Range(0, baseRows.Length).Where(ri => Baseline.PrimaryRows.Contains(baseRows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, baseRows.Length).Average(r => x[r, w]))));
        int Over95(double[,] x) => Enumerable.Range(0, baseRows.Length).Count(r => x[r, last] > 95.0);
        int Info(double[,] x, IEnumerable<int> rs) => rs.Sum(r => Enumerable.Range(1, nw - 1).Count(w => x[r, w] > 0 && x[r, w] < 100));
        int upR = 0, upC = 0, dnR = 0, dnC = 0;
        var lines = new List<string>();
        for (int r = 0; r < baseRows.Length; r++)
        {
            bool u = false, d = false;
            for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
            if (u) upR++; if (d) dnR++;
            if (u || d) lines.Add($"{baseRows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
        }
        var all = Enumerable.Range(0, baseRows.Length);
        Console.WriteLine(title);
        Console.WriteLine();
        Console.WriteLine("| | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第1〜5波の平均 | 第五波 95% 超の行 | 情報セル（全行 ／ 主判定） |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|");
        string M5(double[,] x) => F1(Enumerable.Range(0, baseRows.Length).Average(r => x[r, 0])) + " ／ " + Means(x);
        Console.WriteLine($"| 前 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {M5(b)} | {Over95(b)} | {Info(b, all)} ／ {Info(b, pr)} |");
        Console.WriteLine($"| 後 | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {M5(g)} | {Over95(g)} | {Info(g, all)} ／ {Info(g, pr)} |");
        Console.WriteLine();
        foreach (var l in lines) Console.WriteLine("- " + l);
        Console.WriteLine();
    }

    static void Stage0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        CompareTable($"# {Tag} §2-3 段0 —— `compare` 64 行: 旧（第378期までの規定のリィカ・`Pin378`）→ 新（目覚めたリィカ）（seed 0..199）",
            rows.Select(r => (r.Name, Pin378(r.F))).ToArray(), rows);

        // 代表台
        var names = new[] { "死の連鎖 (リィカ軸)", "追撃×死 (ハギ×リィカ)", "死の連鎖+後備え", "惨禍×死の連鎖" };
        var waves = new (string Name, I.W[] Ws)[] { ("ボス", new[] { I.Boss }), ("近衛", new[] { I.Guard }), ("大隊", new[] { I.Bat }), ("本編 第5波", new[] { I.M5 }), ("強 第2〜5波", I.StrongWaves) };
        var g = new S377.B[names.Length, 2, waves.Length];
        Parallel.For(0, g.Length, k =>
        {
            int ni = k / (2 * waves.Length), vi = k / waves.Length % 2, wi = k % waves.Length;
            var f = Row(names[ni]);
            g[ni, vi, wi] = M(vi == 0 ? Pin378(f) : f, waves[wi].Ws, 100);
        });
        Console.WriteLine("## 代表台（死の4台・ムグは規定）× 5 波（seed 0..99）——勝率・倒しT・倒れた編成の駒 ／ 誰が何Tに倒れたか");
        Console.WriteLine();
        Console.WriteLine("| 台 | リィカ | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "---|")));
        for (int ni = 0; ni < names.Length; ni++)
            for (int vi = 0; vi < 2; vi++)
                Console.WriteLine($"| {(vi == 0 ? names[ni] : "")} | {(vi == 0 ? "旧" : "新")} | {string.Join(" | ", Enumerable.Range(0, waves.Length).Select(wi => g[ni, vi, wi].Cell))} |");
        Console.WriteLine();
        Console.WriteLine("誰が何Tに倒れたか（新・近衛 ／ 大隊）:");
        Console.WriteLine();
        for (int ni = 0; ni < names.Length; ni++) Console.WriteLine($"- {names[ni]}: 近衛 {g[ni, 1, 1].FellCell} ／ 大隊 {g[ni, 1, 2].FellCell}");
        Console.WriteLine();

        // 寄せ集め
        var vers = new (string Name, UnitDef D)[] { ("素体", I.Plain(UnitCatalog.RicaH378)), ("旧", UnitCatalog.RicaH378), ("新", UnitCatalog.Rica) };
        var mixWaves = S377.VerWaves;
        int nb = S377.MixBoards, nw = mixWaves.Length;
        var boards = vers.Select(v => S377.MixBoardsOf(v.D)).ToArray();
        var mg = new double[vers.Length, nb, nw];
        Parallel.For(0, vers.Length * nb * nw, k =>
        {
            int vi = k / (nb * nw), bi = k / nw % nb, wi = k % nw;
            mg[vi, bi, wi] = mixWaves[wi].Ws.Average(w => S377.WinRate(boards[vi][bi], w, 20));
        });
        Console.WriteLine($"## 寄せ集めの台（第377期と同じ 50 台）× {nw} 物差し（seed 0..19）——50 台の平均勝率（素体との差）／ 素体より 10pt 以上強い台の数");
        Console.WriteLine();
        Console.WriteLine("| リィカ | " + string.Join(" | ", mixWaves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(mixWaves.Select(_ => "---|")));
        for (int vi = 0; vi < vers.Length; vi++)
            Console.WriteLine($"| {vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => { double m = Enumerable.Range(0, nb).Average(bi => mg[vi, bi, wi]); double m0 = Enumerable.Range(0, nb).Average(bi => mg[0, bi, wi]); return vi == 0 ? F1(m) : $"{F1(m)}（{Sg(m - m0)}）・{Enumerable.Range(0, nb).Count(bi => mg[vi, bi, wi] - mg[0, bi, wi] >= 10)}"; }))} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-3 版
    // ---------------------------------------------------------------------------------
    static void Versions()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int seeds = 100;
        var boards = Boards();
        var waves = S377.VerWaves;
        int nb = boards.Length, nv = VerNames.Length, nw = waves.Length;
        var g = new S377.B[nb, nv, nw];
        var keys = new List<(int B, int V, int W)>();
        for (int bi = 0; bi < nb; bi++) for (int vi = 0; vi < nv; vi++) for (int wi = 0; wi < nw; wi++) keys.Add((bi, vi, wi));
        Parallel.ForEach(keys, k => g[k.B, k.V, k.W] = M(Apply(boards[k.B].F, k.V), waves[k.W].Ws, seeds));

        Console.WriteLine($"# {Tag} §3-3 ムグが胞子を撒く —— {nb} 台 × ムグ（{string.Join(" ／ ", VerNames)}）× {nw} 物差し（seed 0..{seeds - 1}・リィカは新しい規定）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。死・ゾト抜きはゾトを素体に替えた並び（器具の中だけ）。");
        Console.WriteLine();
        void Grid(string title, Func<S377.B, string> cell, int[]? only = null)
        {
            var ws = only ?? Enumerable.Range(0, nw).ToArray();
            Console.WriteLine(title);
            Console.WriteLine();
            Console.WriteLine("| 台 | ムグ | " + string.Join(" | ", ws.Select(wi => waves[wi].Name)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")));
            for (int bi = 0; bi < nb; bi++)
                for (int vi = 0; vi < nv; vi++)
                    Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {string.Join(" | ", ws.Select(wi => cell(g[bi, vi, wi])))} |");
            Console.WriteLine();
        }
        int[] key4 = { 0, 1, 2, 5 };
        Grid("## 1. 勝率・倒しT（勝った戦）・倒れた編成の駒（1戦・2度目を含む）", x => x.Cell);
        Grid("## 1'. 誰が何Tに倒れたか（駒 最初に倒れた戦%（T の平均）・早い順）", x => x.FellCell, key4);

        Console.WriteLine("## 2. ボス（リィカの層 T1〜T8 ／ 9 層に届いた戦%・T ／ 1人になった戦%・T ／ リィカが倒れた戦%・T ／ リィカの与ダメ T1〜T8 ／ 勇者の HP（頭の回復の後・T1〜T10）／ 1,200 超のターン ／ 勇者を倒した戦%・T）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                var x = g[bi, vi, 0];
                string hp = string.Join(" ", Enumerable.Range(1, HeroT).Select(t => Xv(x, $"heroHpN{t}") == 0 ? "—" : F0((double)Xv(x, $"heroHp{t}") / Xv(x, $"heroHpN{t}"))));
                Console.WriteLine($"- **{boards[bi].Name} × {VerNames[vi]}**: 層 [{x.Layers(LayerT)}] ／ 9 層 {Pct(Xv(x, "l9"), x.N)}%・T{Per(Xv(x, "l9T"), Xv(x, "l9"))} ／ 1人 {Pct(x.AloneN, x.N)}%・T{Per(x.AloneTSum, x.AloneN)} ／ 倒れた {Pct(x.RicaFellN, x.N)}%・T{Per(x.RicaFellTSum, x.RicaFellN)} ／ 与ダメ [{x.RicaPerTurn(LayerT)}] ／ HP [{hp}] ／ 1,200 超 {Per(Xv(x, "over1200"), x.N)} ／ 倒した {Pct(Xv(x, "heroKill"), x.N)}%・T{Per(Xv(x, "heroKillT"), Xv(x, "heroKill"))}");
            }
        Console.WriteLine();
        Grid("## 3. 湧いた体（1戦: ムグが撒いた胞子・T ／ 育った ／ 若いムグが撒いた ／ ムグの分裂 ＋ 撒いた胞子の合計 ／ 亡者 味方の亡骸 ／ 敵の亡骸 ／ 空きが無くて湧かなかった（撒き ／ 亡者 味方 ／ 敵）／ 胞子の倒れた数）",
            x => $"{Per(Xv(x, "sown"), x.N)}・T{Per(Xv(x, "sownT"), Xv(x, "sown"))} ／ {Per(Xv(x, "grown"), x.N)} ／ {Per(Xv(x, "ySown"), x.N)} ／ {Per(x.SporeSummons, x.N)} ／ {Per(x.Raises, x.N)} ／ {Per(Xv(x, "foeRaise"), x.N)} ／ {Per(Xv(x, "sowNoSeat") + Xv(x, "yNoSeat"), x.N)} ／ {Per(x.NoSeat, x.N)} ／ {Per(Xv(x, "foeNoSeat"), x.N)} ／ {Per(x.SporeDeaths, x.N)}", key4);
        Grid("## 4. 層の出どころ（1戦: リィカが生きている間に倒れた味方 ＝ 層を積んだ死: 編成 ／ 胞子 ／ 若いムグ ／ 亡者）", x => $"{Per(Xv(x, "srcForm"), x.N)} ／ {Per(Xv(x, "srcSpore"), x.N)} ／ {Per(Xv(x, "srcYoung"), x.N)} ／ {Per(Xv(x, "srcWight"), x.N)}", key4);
        Grid("## 4'. リィカ（最大層・届いたT ／ 9 層 %・T ／ 1人になった戦%・T ／ 倒れた戦%・T ／ 1ターンの最大の与ダメ）",
            x => $"{Per(x.PeakSum, x.N)}・T{Per(x.PeakTSum, x.PeakN)} ／ {Pct(Xv(x, "l9"), x.N)}%・T{Per(Xv(x, "l9T"), Xv(x, "l9"))} ／ {Pct(x.AloneN, x.N)}%・T{Per(x.AloneTSum, x.AloneN)} ／ {Pct(x.RicaFellN, x.N)}%・T{Per(x.RicaFellTSum, x.RicaFellN)} ／ {Per(x.RicaMaxTurn, x.N)}", key4);
        Grid("## 5. 隊の1ターンの最大の与ダメ（戦ごとの最大の平均・第378期 §4 の物差し）", x => F0((double)Xv(x, "teamMax") / x.N), new[] { 0, 1, 2 });
        Grid("## 5'. 勝ち方の質（勝った戦の平均生存数（編成の駒）・生存1体で勝った割合%）", x => x.Wins == 0 ? "—" : $"{Per(x.WinSurvSum, x.Wins)}・{Pct(x.Win1Surv, x.Wins)}%");

        Console.WriteLine("## 7. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切りが 5pt 以上増える ／ 層 15 ／ 20 超 ／ int のあふれ）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 1; vi < nv; vi++)
            {
                string at = $"{boards[bi].Name} × {VerNames[vi]}";
                foreach (int wi in new[] { 1, 2 }) { var x = g[bi, vi, wi]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {waves[wi].Name}: 1T 決着 {Pct(x.Win1T, x.N)}%"); }
                if (g[bi, vi, 0].BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {g[bi, vi, 0].BossFast} 戦");
                for (int wi = 0; wi < nw; wi++)
                {
                    var x = g[bi, vi, wi]; var x0 = g[bi, 0, wi];
                    if (100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {waves[wi].Name}: 30T の打ち切り {Pct(x0.Cap, x0.N)} → {Pct(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {waves[wi].Name}: あふれの兆し {x.Overflow} 件");
                }
            }
        Console.WriteLine(signs.Count == 0 ? "- 精鋭の 1T ／ ボス 4T 未満 ／ 30T ／ あふれ: なし" : string.Join("\n", signs.Select(s => "- " + s)));
        Console.WriteLine();
        Console.WriteLine("層 15 ／ 20 を超えた戦%（ボス ／ 近衛 ／ 大隊 ／ 第5波 ／ 九体 ／ 強）:");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                Console.WriteLine($"- {boards[bi].Name} × {VerNames[vi]}: " + string.Join(" ", new[] { 0, 1, 2, 5, 6, 7 }.Select(wi => $"{Pct(Xv(g[bi, vi, wi], "peak15"), g[bi, vi, wi].N)}／{Pct(Xv(g[bi, vi, wi], "peak20"), g[bi, vi, wi].N)}")));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareVer(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int vi = Array.IndexOf(VerNames, ver);
        if (vi < 1) throw new ArgumentException("sh379 compare の版は 撒き1 / 撒き全 / 育つ");
        var rows = CompareBuilds().ToArray();
        var vr = rows.Select(r => (r.Name, F: Apply(r.F, vi))).ToArray();
        Console.WriteLine($"版で回した行: ムグのいる行（{string.Join("、", rows.Where(r => S377.HasMug(r.F)).Select(r => r.Name))}）。ムグのいない行は版の写像で変わらない。");
        Console.WriteLine();
        CompareTable($"# {Tag} `compare` 64 行 × ムグの版 {ver}（seed 0..199・基準は規定）", rows, vr);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
