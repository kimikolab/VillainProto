using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// =====================================================================================
// db351 —— 第351期【軽い期】デバフ軸の立て直し①: 押し付け ／ 交換 ／ 奪う（WT-a ／ WT-x ／ WT-u ／ UK-r・版）。
// 指示書は design/PHASE351_DEBUFF_PUSH_SPEC.md ／ 報告は design/PHASE351_DEBUFF_PUSH.md。波と1戦の計数は `inv348`（第348期）をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 db351 seats [版]          # §2-1 Phase 0: ネル ／ ワタ ／ ウツの席 60 × 埋め方 3 × ボス ／ 近衛 ／ 大隊・seed 0..19
//     dotnet run --project BattleSim -c Release 0 db351 p0                  # §2-2 Phase 0: 呪いの流れ（代表台 × ボス ／ 大隊 ／ 本編 第2 ／ 第5波・規定・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 db351 flow [版,…]         # §5-1: 呪いの流れを版ごとに（代表台 × ボス ／ 大隊）
//     dotnet run --project BattleSim -c Release 0 db351 boards [版,…]       # §5-2: 代表台 × 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 db351 axes                # §5-3: 軸の横並び（第348期 §3）のデバフの行を版ごとに
//     dotnet run --project BattleSim -c Release 0 db351 compare [版,…]      # §5-4: `compare` 64 行 × 版（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 db351 log <版> <台> <波> [seed]   # 1戦のログ（台 1 ／ 2 ／ uk ／ 逆しま ／ 渡し ／ 弱体）
//     dotnet run --project BattleSim -c Release 0 db351 s352                # 第352期 §3: 規定（WT-u）の「試遊・呪い」× 波と、第351期の WT-u の列の突き合わせ
//     dotnet run --project BattleSim -c Release 0 db351 check               # 自己検査
//
// 版の名前（ASCII）: base（規定）／ a ／ x ／ u ／ r。版はワタ ／ ウケだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ版の本測定（flow ／ boards ／ axes ／ compare）を走らせない**（R397・`.tmp/p351/predict.md`）。
// =====================================================================================
static partial class Db351Diag
{
    internal const string PredictFile = ".tmp/p351/predict.md";
    const string Tag = "第351期";
    const int Seeds = 100, GridSeeds = 20, CompareSeeds = 200;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "flow" or "boards" or "axes" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"db351: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "seats": SeatsOne(V(A(3, "base"))); return;
            case "p0": Phase0(); return;
            case "flow": FlowAll(Pick(A(3, ""))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "axes": AxesAll(); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "1"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "s352": Settle352(); return;
            case "check": Check(); return;
            default: Console.WriteLine("db351: モードは seats / p0 / flow / boards / axes / compare / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    // ---------------------------------------------------------------------------------
    // 版
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef Wata, UnitDef Uke);
    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.WataH351, UnitCatalog.Uke),   // 第352期: 規定のワタが WT-u になったので、第351期の「規定」は旧の定義に固定
        new("WT-a 広く拾う", "a", UnitCatalog.WataWTa, UnitCatalog.Uke),
        new("WT-x 交換", "x", UnitCatalog.WataWTx, UnitCatalog.Uke),
        new("WT-u ウツへ回す", "u", UnitCatalog.WataWTu, UnitCatalog.Uke),
        new("UK-r 奪う", "r", UnitCatalog.WataH351, UnitCatalog.UkeUKr),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n || v.Name.StartsWith(n, StringComparison.Ordinal));
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(S316.SwapId(Pin353(f), "wata", v.Wata), "uke", v.Uke);   // 第354期: ウツは第353期までの規定に固定

    // ---------------------------------------------------------------------------------
    // 呪いの台（§2-1）
    // ---------------------------------------------------------------------------------
    /// <summary>席の総当たりの埋め方（決めたこと: 書き手＋壁 ×2 と 書き手2枚）。2枚は残りの席の番号の若いほうから順に置く（第349期と同じ）。</summary>
    internal static readonly (string Name, UnitDef A, UnitDef B)[] Fillers =
    {
        ("ムド＋ガルド", UnitCatalog.Mudo, UnitCatalog.Gald),
        ("クビ＋ゴルム", UnitCatalog.Kubi, UnitCatalog.Golm),
        ("ムド＋クビ", UnitCatalog.Mudo, UnitCatalog.Kubi),
    };
    internal sealed record Seat(string Key, int Nel, int Wata, int Utsu, int Fill, Formation F);
    static readonly string[] SlotName = { "前1", "前3", "中央", "後1", "後3" };
    internal static Seat[] AllSeats()
    {
        var list = new List<Seat>();
        for (int fi = 0; fi < Fillers.Length; fi++)
            for (int n = 0; n < 5; n++) for (int w = 0; w < 5; w++) for (int u = 0; u < 5; u++)
            {
                if (n == w || n == u || w == u) continue;
                var f = new Formation();
                f[n] = UnitCatalog.Nel; f[w] = UnitCatalog.Wata; f[u] = UnitCatalog.UtsuH353;   // 第354期: 規定のウツが UT-o になったので旧に固定
                var rest = Enumerable.Range(0, 5).Where(i => i != n && i != w && i != u).ToArray();
                f[rest[0]] = Fillers[fi].A; f[rest[1]] = Fillers[fi].B;
                list.Add(new Seat(Seats(f), n, w, u, fi, f));
            }
        return list.ToArray();
    }

    /// <summary>Phase 0 の上位1並び（`db351 seats` の §2 の1位・報告 §2）。「試遊・呪い」もこの並び。</summary>
    internal static Formation Top1 => Pin353(Presets.Versions.First(r => r.Name == PlayRow).F);   // 第354期: ウツは第353期までの規定に固定
    internal const string PlayRow = "試遊・呪い";
    /// <summary>Phase 0 の上位2並び目。</summary>
    internal static Formation Top2 => Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Mudo, center: UnitCatalog.Nel, back1: UnitCatalog.Kubi, back3: UnitCatalog.UtsuH353);   // §2 の2位（第354期: ウツは旧）
    /// <summary>UK-r の台（上位1並びの残り2枚のうち1枚をウケに替えた並び・報告 §1）。</summary>
    internal static Formation UkBoard => S316.SwapId(Top1, UkSwapOut, UnitCatalog.Uke);
    internal const string UkSwapOut = "kubi";   // 決めたこと: 弱体の書き手でないクビを外す（ムドの泥 ／ 呪いの印は残す）

    static Formation Row(string n) => Pin353(CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F);   // 第354期: ウツは旧
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("呪い", "呪いの台 1", Top1),
        new I.Board("呪い", "呪いの台 2", Top2),
        new I.Board("呪い", "呪いの台 ウケ入り", UkBoard),
        new I.Board("呪い", "逆しま (ネル×ウツ)", Row("逆しま (ネル×ウツ)")),
        new I.Board("呪い", "渡し (ワタ×ドハ)", Row("渡し (ワタ×ドハ)")),
        new I.Board("呪い", "弱体×破片 (ウケ×ネル×ウロ)", Row("弱体×破片 (ウケ×ネル×ウロ)")),
    };
    static I.Board BoardOf(string n) => n switch
    {
        "1" => Boards()[0], "2" => Boards()[1], "uk" => Boards()[2],
        _ => Boards().First(b => b.Name.StartsWith(n, StringComparison.Ordinal)),
    };

    internal static readonly I.W[] Waves = new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5 }.Concat(I.StrongWaves).ToArray();
    internal static I.W WaveOf(string n) => n switch
    {
        "boss" => I.Boss, "guard" => I.Guard, "bat" => I.Bat, "2" => I.M2, "4" => I.M4, "5" => I.M5,
        "s2" => I.StrongWaves[0], "s3" => I.StrongWaves[1], "s4" => I.StrongWaves[2], "s5" => I.StrongWaves[3],
        _ => Waves.First(w => w.Name == n),
    };

    static void Log(Ver v, string board, string wave, int seed)
    {
        var b = BoardOf(board); var w = WaveOf(wave);
        var (r, _, _) = I.Raw(Apply(b.F, v), w, seed, verbose: true);
        Console.WriteLine($"# {b.Name}（{Seats(b.F)}）× {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // §2-1 席の総当たり
    // ---------------------------------------------------------------------------------
    internal sealed class Cell { public long N, Wins, WinT, Fell; }
    internal static Cell GridCell(Formation f, I.W w, int n = GridSeeds)
    {
        var parts = new Cell[n];
        Parallel.For(0, n, s =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var r = BattleEngine.Run(p, w.Make(), s, verbose: false, boss: new BossRule(false) { Scale = w.Scale });
            parts[s] = new Cell { N = 1, Wins = r.PlayerWon ? 1 : 0, WinT = r.PlayerWon ? r.Turns : 0, Fell = p.Count(u => u.LastDeathTurn > 0) };
        });
        var c = new Cell();
        foreach (var x in parts) { c.N += x.N; c.Wins += x.Wins; c.WinT += x.WinT; c.Fell += x.Fell; }
        return c;
    }
    static string WT(Cell c) => $"{Pct(c.Wins, c.N)}・{(c.Wins == 0 ? "—" : F1((double)c.WinT / c.Wins))}T・倒 {Per2(c.Fell, c.N)}";

    static void SeatsOne(Ver v)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var seats = AllSeats();
        I.W[] ws = { I.Boss, I.Guard, I.Bat };
        var keys = seats.SelectMany(s => ws.Select(w => (s, w))).ToArray();
        var cells = new Cell[keys.Length];
        for (int k = 0; k < keys.Length; k++) cells[k] = GridCell(Apply(keys[k].s.F, v), keys[k].w);
        var res = new Dictionary<(Seat, string), Cell>();
        for (int k = 0; k < keys.Length; k++) res[(keys[k].s, keys[k].w.Name)] = cells[k];
        Cell C(Seat s, I.W w) => res[(s, w.Name)];
        Console.WriteLine($"# {Tag} §2-1 席の総当たり —— ネル ／ ワタ ／ ウツの並び 60 × 埋め方 {Fillers.Length} × ボス ／ 近衛 ／ 大隊・seed 0..{GridSeeds - 1}・{v.Name}");
        Console.WriteLine();
        Console.WriteLine("埋め方の2枚は残りの席の番号（前1 → 前3 → 中央 → 後1 → 後3）の若いほうから順に置いた。");
        Console.WriteLine();
        Console.WriteLine("## 1. 3波の勝率の分布（並びの数）");
        Console.WriteLine();
        string[] buckets = { "0%", "1〜49%", "50〜99%", "100%" };
        int B(Cell c) => c.Wins == 0 ? 0 : c.Wins * 2 < c.N ? 1 : c.Wins < c.N ? 2 : 3;
        Console.WriteLine("| 波 | 埋め方 | " + string.Join(" | ", buckets) + " | 並びの平均の勝率 |");
        Console.WriteLine("|---|---|" + string.Concat(buckets.Select(_ => "--:|")) + "--:|");
        foreach (var w in ws)
            foreach (var fill in Enumerable.Range(-1, Fillers.Length + 1))
            {
                var xs = seats.Where(s => fill < 0 || s.Fill == fill).ToArray();
                Console.WriteLine($"| {w.Name} | {(fill < 0 ? "**全部**" : Fillers[fill].Name)} | " + string.Join(" | ", buckets.Select((_, b) => xs.Count(s => B(C(s, w)) == b)))
                    + $" | {F1(xs.Average(s => 100.0 * C(s, w).Wins / C(s, w).N))} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 2. 上位の並び（3波の勝率の和 → ボスの勝率 → 3波の倒れた数 → 決着T の順）");
        Console.WriteLine();
        Console.WriteLine("| # | 並び（前1・前3・中央・後1・後3） | 埋め方 | ボス 勝率・T・倒れた | 近衛 | 大隊 |");
        Console.WriteLine("|--:|---|---|---|---|---|");
        var ranked = Rank(seats, C);
        int k2 = 0;
        foreach (var s in ranked.Take(10))
            Console.WriteLine($"| {++k2} | {s.Key} | {Fillers[s.Fill].Name} | {WT(C(s, I.Boss))} | {WT(C(s, I.Guard))} | {WT(C(s, I.Bat))} |");
        Console.WriteLine();
        Console.WriteLine("既存の台（比べる台・同じ帯）:");
        Console.WriteLine();
        foreach (var n in new[] { "逆しま (ネル×ウツ)", "渡し (ワタ×ドハ)", "弱体×破片 (ウケ×ネル×ウロ)" })
        {
            var f = Apply(Row(n), v);
            Console.WriteLine($"- {n}（{Seats(f)}）: ボス {WT(GridCell(f, I.Boss))} ／ 近衛 {WT(GridCell(f, I.Guard))} ／ 大隊 {WT(GridCell(f, I.Bat))}");
        }
        Console.WriteLine();
        Console.WriteLine("## 3. 駒の席ごとの平均（3波の勝率の平均・埋め方は全部）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | " + string.Join(" | ", SlotName) + " |");
        Console.WriteLine("|---|" + string.Concat(SlotName.Select(_ => "--:|")));
        foreach (var (nm, sel) in new (string, Func<Seat, int>)[] { ("ネル", s => s.Nel), ("ワタ", s => s.Wata), ("ウツ", s => s.Utsu) })
            Console.WriteLine($"| {nm} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(i =>
                F1(seats.Where(s => sel(s) == i).Average(s => ws.Average(w => 100.0 * C(s, w).Wins / C(s, w).N))))) + " |");
        Console.WriteLine();
        Console.WriteLine("ワタとウツが隣か（隣 ＝ ネルの漏れ ／ 泥がウツに届く前にワタが拾いうる）:");
        Console.WriteLine();
        foreach (bool adj in new[] { true, false })
        {
            var xs = seats.Where(s => FormationRules.AreAdjacent(s.Wata, s.Utsu) == adj).ToArray();
            Console.WriteLine($"- {(adj ? "隣" : "隣でない")}（{xs.Length} 並び）: " + string.Join(" ／ ", ws.Select(w => $"{w.Name} {F1(xs.Average(s => 100.0 * C(s, w).Wins / C(s, w).N))}")));
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    internal static Seat[] Rank(Seat[] seats, Func<Seat, I.W, Cell> C) => seats
        .OrderByDescending(s => C(s, I.Boss).Wins + C(s, I.Guard).Wins + C(s, I.Bat).Wins)
        .ThenByDescending(s => C(s, I.Boss).Wins)
        .ThenBy(s => C(s, I.Boss).Fell + C(s, I.Guard).Fell + C(s, I.Bat).Fell)
        .ThenBy(s => C(s, I.Boss).WinT + C(s, I.Guard).WinT + C(s, I.Bat).WinT)
        .ThenBy(s => s.Key, StringComparer.Ordinal).ToArray();
}
