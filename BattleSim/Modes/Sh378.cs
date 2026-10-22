using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;

// =====================================================================================
// sh378 —— 第378期【軽い期】本体: 目覚めたリィカ（R-積M の上に版）——敵の亡骸を従える（E）／ 層の連撃（L）／ 起こしの上限を外す（U）。
// 指示書は design/PHASE378_DEATH_AWAKEN_SPEC.md ／ 報告は design/PHASE378_DEATH_AWAKEN.md。規定は1つも変えない。
//
//     dotnet run --project BattleSim -c Release 0 sh378 vers      # §3-2: 死の台 5 × 版（規定 ／ 土台 ／ E ／ L ／ EL ／ ELU）× 8 物差し（seed 0..99・verbose）
//     dotnet run --project BattleSim -c Release 0 sh378 mix       # §3-2 の 5: 寄せ集めの台 50（第377期と同じ）× 素体 ／ 規定 ／ 土台 ／ E ／ L ／ EL ／ ELU × 8 物差し（seed 0..19）
//     dotnet run --project BattleSim -c Release 0 sh378 axes      # §3-3: 近衛 ／ 大隊 ／ ボスの1ターンの最大の与ダメ（隊の合計）とボスの倒しT——死の台 × 規定 ／ 土台 ／ ELU と他の軸の試遊の台（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh378 compare <土台|E|L|EL|ELU>   # `compare` 64 行 × 版（リィカ ／ ムグのいる行だけを回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh378 row <行名>  # `compare` の1行 × 版 × 本編 第2〜5波（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh378 check     # 自己検査
//
// 1戦の計数は第377期の器具（`Sh377Diag.One`）＋ この期の計数（`Extra` が `B.X` に書く）。**予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p378/predict.md`）。
// =====================================================================================
static partial class Sh378Diag
{
    internal const string PredictFile = ".tmp/p378/predict.md";
    const string Tag = "第378期";
    const int CompareSeeds = 200, HeroT = 10;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "vers" or "mix" or "axes" or "compare" or "row" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh378: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "vers": Versions(); return;
            case "mix": Mix(); return;
            case "axes": Axes(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "ELU"); return;
            case "row": RowDetail(string.Join(" ", args.Skip(3))); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh378: モードは vers / mix / axes / compare / row / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    // ---------------------------------------------------------------------------------
    // 版
    // ---------------------------------------------------------------------------------
    internal static readonly string[] VerNames = { "規定", "土台", "E", "L", "EL", "ELU" };
    internal static readonly UnitDef[] RicaOf = { UnitCatalog.RicaH378, UnitCatalog.RicaStack, UnitCatalog.RicaE, UnitCatalog.RicaL, UnitCatalog.RicaEL, UnitCatalog.RicaELU };
    internal static readonly UnitDef[] MugOf = { UnitCatalog.Mug, UnitCatalog.MugSproutM, UnitCatalog.MugSproutM, UnitCatalog.MugSproutM, UnitCatalog.MugSproutM, UnitCatalog.MugSproutMU };
    /// <summary>版を当てる（リィカ ／ ムグを版の駒に替える・ムグのいない台はリィカだけ）。土台 ＝ 第377期の R-積M（ムグなしは R-積）。</summary>
    internal static Formation Apply(Formation f, int vi) => vi == 0 ? f : I.Swap(I.Swap(f, "rica", RicaOf[vi]), "mug", MugOf[vi]);

    // ---------------------------------------------------------------------------------
    // この期の計数（`Sh377Diag.One` の `extra`）
    // ---------------------------------------------------------------------------------
    /// <summary>1戦の計数を <see cref="Sh377Diag.B.X"/> に書く: 敵の亡骸からの亡者・6 ／ 9 ／ 15 層・ボスの HP の推移（回復の後）・勇者を倒した T・勇者へ 1,200 超のターン・隊の1ターンの最大の与ダメ。</summary>
    internal static void Extra(BattleResult r, List<UnitState> p, List<UnitState> e, S377.B b)
    {
        var x = b.X;
        if (r.TallyByUnit.TryGetValue("rica", out var rt))
        {
            x["foeRaise"] = rt.R378FoeRaises; x["foeRaiseT"] = rt.R378FoeRaiseTurnSum; x["foeNoSeat"] = rt.R378FoeNoSeat;
            if (rt.R378Layer6Turn > 0) { x["l6"] = 1; x["l6T"] = rt.R378Layer6Turn; }
            if (rt.R378Layer9Turn > 0) { x["l9"] = 1; x["l9T"] = rt.R378Layer9Turn; }
            if (rt.NecroPeak > 15) x["peak15"] = 1;
            if (r.TallyByUnit.TryGetValue("rica", out _) && rt.Z372HitsByTurn is { } hb) x["swings"] = hb.Sum();
        }
        var foe = e.Select(u => u.InstanceId).ToHashSet();
        UnitState? hero = e.Count == 0 ? null : e.MaxBy(u => u.MaxHp);
        long hp = hero?.MaxHp ?? 0; bool open = false;
        var team = new Dictionary<int, long>(); var toHero = new Dictionary<int, long>();
        var hpAt = new long[HeroT + 1]; var seen = new bool[HeroT + 1];
        foreach (var ev in r.Events)
        {
            int t = ev.Turn;
            switch (ev.Kind)
            {
                case BattleEventKind.TurnStart:
                    if (t is >= 1 and <= HeroT) { hpAt[t] = hp; seen[t] = true; }
                    open = true;
                    break;
                case BattleEventKind.Heal when hero is not null && ev.TargetId == hero.InstanceId:
                    hp = ev.HpAfter;
                    if (open && t is >= 1 and <= HeroT) hpAt[t] = hp;
                    break;
                case BattleEventKind.Damage:
                    if (ev.TargetId is int tg && foe.Contains(tg) && !(ev.ActorId is int ac && foe.Contains(ac))) team[t] = team.GetValueOrDefault(t) + ev.Amount;
                    if (hero is not null && ev.TargetId == hero.InstanceId) { hp = ev.HpAfter; open = false; toHero[t] = toHero.GetValueOrDefault(t) + ev.Amount; }
                    break;
                case BattleEventKind.Death when hero is not null && ev.TargetId == hero.InstanceId:
                    hp = 0; x["heroKill"] = 1; x["heroKillT"] = t;
                    break;
            }
        }
        x["teamMax"] = team.Count == 0 ? 0 : team.Values.Max();
        x["over1200"] = toHero.Values.Count(v => v > 1200);
        if (hero is not null)
            for (int t = 1; t <= HeroT; t++)
                if (seen[t] && hpAt[t] > 0) { x[$"heroHp{t}"] = hpAt[t]; x[$"heroHpN{t}"] = 1; }   // 頭のターンが来て、勇者が立っているターンだけ
    }

    static S377.B M(Formation f, I.W[] ws, int seeds) => S377.Measure(f, ws, seeds, Extra);
    static long Xv(S377.B b, string k) => b.X.GetValueOrDefault(k);

    // ---------------------------------------------------------------------------------
    // §3-2 版
    // ---------------------------------------------------------------------------------
    static void Versions()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int seeds = 100;
        var boards = S377.Boards(tsugi: true);
        var waves = S377.VerWaves;
        int nb = boards.Length, nv = VerNames.Length, nw = waves.Length;
        var g = new S377.B[nb, nv, nw];
        var keys = new List<(int B, int V, int W)>();
        for (int bi = 0; bi < nb; bi++) for (int vi = 0; vi < nv; vi++) for (int wi = 0; wi < nw; wi++) keys.Add((bi, vi, wi));
        Parallel.ForEach(keys, k => g[k.B, k.V, k.W] = M(Apply(boards[k.B].F, k.V), waves[k.W].Ws, seeds));

        Console.WriteLine($"# {Tag} §3-2 目覚めたリィカ —— 死の台 {nb} × 版（{string.Join(" ／ ", VerNames)}）× {nw} 物差し（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("土台 ＝ 第377期の R-積M（リィカ `RicaStack` ＋ ムグ `MugSproutM`・ムグのいない台はリィカだけ ＝ R-積）。E ＝ 3 層以上で敵の亡骸から亡者・L ＝ 層 6 ／ 9 で2 ／ 3連撃・U ＝ 起こし ／ 芽吹きの上限なし。継ぎ当て×死の連鎖はツギ入り（参考）。");
        Console.WriteLine();
        void Grid(string title, Func<S377.B, string> cell, int[]? only = null)
        {
            var ws = only ?? Enumerable.Range(0, nw).ToArray();
            Console.WriteLine(title);
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", ws.Select(wi => waves[wi].Name)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")));
            for (int bi = 0; bi < nb; bi++)
                for (int vi = 0; vi < nv; vi++)
                    Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {string.Join(" | ", ws.Select(wi => cell(g[bi, vi, wi])))} |");
            Console.WriteLine();
        }
        int[] key4 = { 0, 1, 2, 5 };
        Grid("## 1. 勝率・倒しT（勝った戦）・倒れた編成の駒（1戦・2度目を含む）", x => x.Cell);
        Grid("## 1'. 誰が何Tに倒れたか（駒 最初に倒れた戦%（T の平均）・早い順）", x => x.FellCell, key4);
        Grid("## 2. リィカ（1人になった戦%・T ／ 倒れた戦%・T ／ 最大層・届いたT ／ 6 層 %・T ／ 9 層 %・T ／ 1ターンの最大の与ダメ）",
            x => $"{Pct(x.AloneN, x.N)}%・T{Per(x.AloneTSum, x.AloneN)} ／ {Pct(x.RicaFellN, x.N)}%・T{Per(x.RicaFellTSum, x.RicaFellN)} ／ {Per(x.PeakSum, x.N)}・T{Per(x.PeakTSum, x.PeakN)} ／ {Pct(Xv(x, "l6"), x.N)}%・T{Per(Xv(x, "l6T"), Xv(x, "l6"))} ／ {Pct(Xv(x, "l9"), x.N)}%・T{Per(Xv(x, "l9T"), Xv(x, "l9"))} ／ {Per(x.RicaMaxTurn, x.N)}", key4);
        Console.WriteLine("## 2'. ボスでのリィカの与ダメ（ターンごと・T1〜T10・生きていた戦の平均）と層");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                var x = g[bi, vi, 0];
                Console.WriteLine($"- {boards[bi].Name} × {VerNames[vi]}: 与ダメ [{x.RicaPerTurn(10)}] ／ 層 [{x.Layers(10)}]");
            }
        Console.WriteLine();
        Grid("## 3. 湧いた体（1戦: 味方の亡骸からの亡者 ／ 敵の亡骸からの亡者・T ／ 芽吹き ／ 亡者の朽ちた %（倒れた亡者のうち）／ 空きが無くて湧かなかった（味方の亡骸 ／ 敵の亡骸 ／ 芽吹き）／ リィカの代償）",
            x => $"{Per(x.Raises, x.N)} ／ {Per(Xv(x, "foeRaise"), x.N)}・T{Per(Xv(x, "foeRaiseT"), Xv(x, "foeRaise"))} ／ {Per(x.Sprouts, x.N)} ／ {Pct(x.WightRot, x.WightDeaths)}% ／ {Per(x.NoSeat, x.N)} ／ {Per(Xv(x, "foeNoSeat"), x.N)} ／ {Per(x.SproutNoSeat, x.N)} ／ {Per(x.CostSelf, x.N)}", key4);

        Console.WriteLine("## 4. ボス（勇者の HP の推移（ターンの頭の回復の後・T1〜T10・まだ立っている戦の平均）／ 勇者を倒した戦%・T ／ 1ターンに勇者へ 1,200 を超えて入ったターン（1戦））");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                var x = g[bi, vi, 0];
                string hpRow = string.Join(" ", Enumerable.Range(1, HeroT).Select(t => Xv(x, $"heroHpN{t}") == 0 ? "—" : F0((double)Xv(x, $"heroHp{t}") / Xv(x, $"heroHpN{t}"))));
                Console.WriteLine($"- {boards[bi].Name} × {VerNames[vi]}: HP [{hpRow}] ／ 倒した {Pct(Xv(x, "heroKill"), x.N)}%・T{Per(Xv(x, "heroKillT"), Xv(x, "heroKill"))} ／ 1,200 超 {Per(Xv(x, "over1200"), x.N)}");
            }
        Console.WriteLine();
        Grid("## 4'. 勝ち方の質（勝った戦の平均生存数（編成の駒）・生存1体で勝った割合%）", x => x.Wins == 0 ? "—" : $"{Per(x.WinSurvSum, x.Wins)}・{Pct(x.Win1Surv, x.Wins)}%");

        Console.WriteLine("## 7. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切りが規定から 5pt 以上増える ／ 層 15 超 ／ int のあふれ ／ 1ターンの与ダメの最大値）");
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
        Console.WriteLine("層が 15 を超えた戦% ／ 隊の1ターンの最大の与ダメ（戦ごとの最大の平均 ／ その台 × 版 × 波の最大）——ボス ／ 近衛 ／ 大隊 ／ 第5波:");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                Console.WriteLine($"- {boards[bi].Name} × {VerNames[vi]}: " + string.Join("・", key4.Select(wi => { var x = g[bi, vi, wi]; return $"{waves[wi].Name} {Pct(Xv(x, "peak15"), x.N)}% ／ {F0((double)Xv(x, "teamMax") / x.N)}"; })));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 の 5 寄せ集め
    // ---------------------------------------------------------------------------------
    static void Mix()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int seeds = 20;
        var vers = new (string Name, UnitDef D)[] { ("素体", I.Plain(UnitCatalog.RicaH378)) }.Concat(VerNames.Select((n, i) => (Name: n == "土台" ? "土台（R-積）" : n, D: RicaOf[i]))).ToArray();
        var boards = vers.Select(v => S377.MixBoardsOf(v.D)).ToArray();
        var waves = S377.VerWaves;
        int nw = waves.Length, nb = S377.MixBoards;
        var g = new double[vers.Length, nb, nw];
        Parallel.For(0, vers.Length * nb * nw, k =>
        {
            int vi = k / (nb * nw), bi = k / nw % nb, wi = k % nw;
            g[vi, bi, wi] = waves[wi].Ws.Average(w => S377.WinRate(boards[vi][bi], w, seeds));
        });
        Console.WriteLine($"# {Tag} §3-2 の 5 寄せ集めの台 —— 第377期と同じ 50 台（リィカ ＋ 死の札を持たない4枚・`new Random({S377.MixSeed})`）× {nw} 物差し（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("ムグのいない台なので、土台 ＝ R-積・ELU の U はリィカの起こしの上限だけ。");
        Console.WriteLine();
        Console.WriteLine("## 1. 50 台の平均勝率（%）と素体との差");
        Console.WriteLine();
        Console.WriteLine("| リィカ | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "--:|")));
        for (int vi = 0; vi < vers.Length; vi++)
            Console.WriteLine($"| {vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => { double m = Enumerable.Range(0, nb).Average(bi => g[vi, bi, wi]); double m0 = Enumerable.Range(0, nb).Average(bi => g[0, bi, wi]); return vi == 0 ? F1(m) : $"{F1(m)}（{Sg(m - m0)}）"; }))} |");
        Console.WriteLine();
        Console.WriteLine("## 2. 素体より 10pt 以上 強い ／ 弱い台の数（50 台のうち）");
        Console.WriteLine();
        Console.WriteLine("| リィカ | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "---|")));
        for (int vi = 1; vi < vers.Length; vi++)
            Console.WriteLine($"| {vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => $"{Enumerable.Range(0, nb).Count(bi => g[vi, bi, wi] - g[0, bi, wi] >= 10)} ／ {Enumerable.Range(0, nb).Count(bi => g[vi, bi, wi] - g[0, bi, wi] <= -10)}"))} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-3 他の軸との火力の比べ
    // ---------------------------------------------------------------------------------
    internal static readonly string[] AxisBoards = { "試遊・燃焼 ポンの台", "試遊・感電 ポンの台", "試遊・毒", "試遊・毒 紅", "試遊・標 守り型", "試遊・破片", "試遊・呪い" };

    static void Axes()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int seeds = 100;
        var all = Presets.Playtest.Concat(Presets.Versions).ToArray();
        var rows = new List<(string Name, Formation F)>();
        foreach (var (n, f) in S377.Boards(tsugi: false))
            foreach (int vi in new[] { 0, 1, 5 }) rows.Add(($"{n} × {VerNames[vi]}", Apply(f, vi)));
        var missing = new List<string>();
        foreach (var n in AxisBoards) { var hit = all.FirstOrDefault(r => r.Name == n); if (hit.F is null) missing.Add(n); else rows.Add((n, hit.F)); }
        var ws = new[] { I.Guard, I.Bat, I.Boss };
        var g = new S377.B[rows.Count, ws.Length];
        Parallel.For(0, g.Length, k => g[k / ws.Length, k % ws.Length] = M(rows[k / ws.Length].F, new[] { ws[k % ws.Length] }, seeds));
        Console.WriteLine($"# {Tag} §3-3 他の軸との火力の比べ —— 近衛 ／ 大隊 ／ ボスの1ターンの最大の与ダメ（隊の合計・戦ごとの最大の平均）とボスの勝率 ／ 倒しT（seed 0..{seeds - 1}・同じ seed）");
        Console.WriteLine();
        Console.WriteLine("隊の与ダメ ＝ 敵に入った `Damage` の合計（撃ち手が敵でないもの・燃焼 ／ 毒の刻みを含む）。" + (missing.Count == 0 ? "" : $"見つからなかった台: {string.Join("・", missing)}。"));
        Console.WriteLine();
        Console.WriteLine("| 台 | 近衛 最大 ／ 勝率 | 大隊 最大 ／ 勝率 | ボス 最大 ／ 勝率・倒しT ／ 1,200 超のターン |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int ri = 0; ri < rows.Count; ri++)
        {
            var c = g[ri, 0]; var b = g[ri, 1]; var h = g[ri, 2];
            Console.WriteLine($"| {rows[ri].Name} | {F0((double)Xv(c, "teamMax") / c.N)} ／ {F1(c.Win)} | {F0((double)Xv(b, "teamMax") / b.N)} ／ {F1(b.Win)} | {F0((double)Xv(h, "teamMax") / h.N)} ／ {F1(h.Win)}・{h.WinTs}T ／ {Per(Xv(h, "over1200"), h.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` の1行 × 版
    // ---------------------------------------------------------------------------------
    static void RowDetail(string name)
    {
        var f = Pin378(CompareBuilds().First(r => r.Name == name).F);   // 第379期: 第378期までの規定のリィカに固定
        var ws = new[] { I.M2, I.M4, I.M5 };
        var g = new S377.B[VerNames.Length, ws.Length];
        Parallel.For(0, g.Length, k => g[k / ws.Length, k % ws.Length] = M(Apply(f, k / ws.Length), new[] { ws[k % ws.Length] }, CompareSeeds));
        Console.WriteLine($"# {Tag} `compare` の1行 × 版 —— {name}（{S307.Seats(f)}・seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 勝率・倒しT・倒 | 誰が何Tに倒れたか | 亡者（味方 ／ 敵の亡骸）| リィカ 最大層 ／ 倒れた% ／ 1ターンの最大 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        for (int vi = 0; vi < VerNames.Length; vi++)
            for (int wi = 0; wi < ws.Length; wi++)
            {
                var x = g[vi, wi];
                Console.WriteLine($"| {VerNames[vi]} | {ws[wi].Name} | {x.Cell} | {x.FellCell} | {Per(x.Raises, x.N)} ／ {Per(Xv(x, "foeRaise"), x.N)} | {Per(x.PeakSum, x.N)} ／ {Pct(x.RicaFellN, x.N)}% ／ {Per(x.RicaMaxTurn, x.N)} |");
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows, Func<Formation, Formation> map, Func<int, bool> only)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            if (!only(ri)) { g[ri, wi] = double.NaN; return; }
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareVer(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int vi = Array.IndexOf(VerNames, ver);
        if (vi < 1) throw new ArgumentException("sh378 compare の版は 土台 / E / L / EL / ELU");
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin378(r.F))).ToArray();   // 第379期: 第378期までの規定のリィカに固定
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        Func<int, bool> only = ri => S377.HasRica(rows[ri].F) || S377.HasMug(rows[ri].F);
        var b = CGrid(rows, f => f, _ => true);
        var g0 = CGrid(rows, f => Apply(f, vi), only);
        var g = new double[rows.Length, nw];
        int touched = 0;
        for (int r = 0; r < rows.Length; r++) { if (only(r)) touched++; for (int w = 0; w < nw; w++) g[r, w] = only(r) ? g0[r, w] : b[r, w]; }
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        int Over95(double[,] x) => Enumerable.Range(0, rows.Length).Count(r => x[r, last] > 95.0);
        Console.WriteLine($"# {Tag} `compare` {rows.Length} 行 × 版 {VerNames[vi]}（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"版で回した行 {touched}（リィカかムグのいる行・{string.Join("、", Enumerable.Range(0, rows.Length).Where(only).Select(ri => rows[ri].Name))}）。それ以外の行は版の写像で1枚も変わらない（回さない）。");
        Console.WriteLine();
        int upR = 0, upC = 0, dnR = 0, dnC = 0;
        var lines = new List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            bool u = false, d = false;
            for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
            if (u) upR++; if (d) dnR++;
            if (u || d) lines.Add($"{rows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
        }
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 | 第五波 95% 超の行 |");
        Console.WriteLine("|---|---|---|--:|---|--:|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} | {Over95(b)} |");
        Console.WriteLine($"| {VerNames[vi]} | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} | {Over95(g)} |");
        Console.WriteLine();
        foreach (var l in lines) Console.WriteLine("- " + l);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
