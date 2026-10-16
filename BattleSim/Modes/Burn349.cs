using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// =====================================================================================
// burn349 —— 第349期【軽い期】燃焼を抑える: 半減 ／ 火の変換 ／ 被弾の燃焼（BN-f ／ BN-g ／ BN-a ／ BN-d ／ BN-f＋d・版）。
// 指示書は design/PHASE349_BURN_TRIM_SPEC.md ／ 報告は design/PHASE349_BURN_TRIM.md。波と1戦の計数は `inv348`（第348期）をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 burn349 s0                 # §2 段0: 試遊・燃焼 ポンの台 × ボス × seed 7
//     dotnet run --project BattleSim -c Release 0 burn349 seats [版]         # §3-1 Phase 0: 3枚の席の総当たり 60 × 埋め方 3 × ボス ／ 近衛 ／ 大隊・seed 0..19（版を付けるとその版で）
//     dotnet run --project BattleSim -c Release 0 burn349 p0                 # §3-2 Phase 0: ポンの台の与ダメと守りの出どころ（規定・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 burn349 boards [版,…]      # §6: 代表台 × 波 × 版（seed 0..99）・ポンの台の内訳つき
//     dotnet run --project BattleSim -c Release 0 burn349 seatsall           # §6: 席の総当たり（ボスだけ・seed 0..19）を版ごとに
//     dotnet run --project BattleSim -c Release 0 burn349 axes               # §6-3: 軸の横並び（第348期 §3）の燃焼の行を版ごとに
//     dotnet run --project BattleSim -c Release 0 burn349 compare [版,…]     # §6-4: `compare` 64 行 × 版（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 burn349 log <版> <波> [seed] # 1戦のログ（ポンの台）
//     dotnet run --project BattleSim -c Release 0 burn349 kindle             # 守るほど燃え上がる（KindleGuard）の効き（規定 ／ −KindleGuard ／ BN-f ／ BN-g）
//     dotnet run --project BattleSim -c Release 0 burn349 check              # 自己検査
//
// 版の名前（ASCII）: base（規定）／ f ／ g ／ a ／ d ／ fd。版はボルグ ／ ホタ ／ ヒヨだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ版の本測定（boards ／ seatsall ／ axes ／ compare）を走らせない**（R397・`.tmp/p349/predict.md`）。
// =====================================================================================
static partial class Burn349Diag
{
    internal const string PredictFile = ".tmp/p349/predict.md";
    const string Tag = "第349期";
    const int Seeds = 100, GridSeeds = 20, CompareSeeds = 200;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "seatsall" or "axes" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"burn349: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "s0": Stage0(); return;
            case "seats": SeatsOne(V(A(3, "base"))); return;
            case "p0": Phase0(); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "seatsall": SeatsAll(); return;
            case "axes": AxesAll(); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "boss"), int.Parse(A(5, "7"))); return;
            case "kindle": Kindle(); return;
            case "check": Check(); return;
            default: Console.WriteLine("burn349: モードは s0 / seats / p0 / boards / seatsall / axes / compare / log / check。"); return;
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
    internal sealed record Ver(string Name, string Ascii, UnitDef Borg, UnitDef Hota, UnitDef Hiyo);
    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("BN-f 半減なし＋変換半分", "f", UnitCatalog.BorgBNf, UnitCatalog.Hota, UnitCatalog.HiyoBNhalf),
        new("BN-g 盾の配りなし＋変換半分", "g", UnitCatalog.BorgBNg, UnitCatalog.Hota, UnitCatalog.HiyoBNhalf),
        new("BN-a 被弾の燃焼では癒えない", "a", UnitCatalog.BorgBNa, UnitCatalog.HotaBNa, UnitCatalog.HiyoBNa),
        new("BN-d 被弾の燃焼は1攻撃に1回", "d", UnitCatalog.BorgBNd, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("BN-f＋d", "fd", UnitCatalog.BorgBNfd, UnitCatalog.Hota, UnitCatalog.HiyoBNhalf),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n || v.Name.StartsWith(n, StringComparison.Ordinal));
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(S316.SwapId(S316.SwapId(f, "borg", v.Borg), "hota", v.Hota), "hiyo", v.Hiyo);

    // ---------------------------------------------------------------------------------
    // 台
    // ---------------------------------------------------------------------------------
    internal const string PonRow = "試遊・燃焼 ポンの台";
    internal static Formation Pon => Presets.Versions.First(r => r.Name == PonRow).F;

    /// <summary>席の総当たりの埋め方（指示書 §3-1・3組目は「燃焼と関係の薄い2枚」のドルガ＋セッキ）。2枚は残りの席の番号の若いほうから順に置く（決めたこと）。</summary>
    internal static readonly (string Name, UnitDef A, UnitDef B)[] Fillers =
    {
        ("ムド＋ガルド", UnitCatalog.Mudo, UnitCatalog.Gald),
        ("ガルド＋リリ", UnitCatalog.Gald, UnitCatalog.Lili),
        ("ドルガ＋セッキ", UnitCatalog.Dolga, UnitCatalog.Sekki),
    };
    internal sealed record Seat(string Key, int Borg, int Hota, int Hiyo, int Fill, Formation F);
    static readonly string[] SlotName = { "前1", "前3", "中央", "後1", "後3" };
    internal static Seat[] AllSeats()
    {
        var list = new List<Seat>();
        for (int fi = 0; fi < Fillers.Length; fi++)
            for (int b = 0; b < 5; b++) for (int h = 0; h < 5; h++) for (int y = 0; y < 5; y++)
            {
                if (b == h || b == y || h == y) continue;
                var f = new Formation();
                f[b] = UnitCatalog.Borg; f[h] = UnitCatalog.Hota; f[y] = UnitCatalog.Hiyo;
                var rest = Enumerable.Range(0, 5).Where(i => i != b && i != h && i != y).ToArray();
                f[rest[0]] = Fillers[fi].A; f[rest[1]] = Fillers[fi].B;
                list.Add(new Seat($"{Seats(f)}", b, h, y, fi, f));
            }
        return list.ToArray();
    }
    static string Row(int slot) => slot <= 1 ? "前列" : slot == 2 ? "中央" : "後列";

    /// <summary>§6 の代表台。2台目（Phase 0 の最良の並びのうちポンの台と違うもの）は Phase 0 の答え（報告 §3）で決めた。</summary>
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("燃焼", "ポンの台", Pon),
        new I.Board("燃焼", "最良の並び", BestSeat),
        new I.Board("燃焼", "燃焼 (ボルグ×ホタ)", CompareBuilds().First(r => r.Name == "燃焼 (ボルグ×ホタ)").F),
        new I.Board("移動", "移動 (チェック波の台)", I.MoveCheck),
        new I.Board("標", "試遊・標 守り型", Presets.Playtest.First(r => r.Name == "試遊・標 守り型").F),
        new I.Board("感電", "試遊・感電 ポンの台", Presets.Versions.First(r => r.Name == "試遊・感電 ポンの台").F),
    };
    /// <summary>Phase 0 の最良の並び（ポンの台と違うもの・`burn349 seats` の §3 の1位）。</summary>
    internal static Formation BestSeat => Formation.Build(front1: UnitCatalog.Hota, front3: UnitCatalog.Hiyo, center: UnitCatalog.Borg, back1: UnitCatalog.Mudo, back3: UnitCatalog.Gald);   // Phase 0 の1位（ボス 4.0T・3波とも倒れた 0）

    internal static readonly I.W[] Waves = new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5, I.Nine }.Concat(I.StrongWaves).ToArray();
    static I.W WaveOf(string n) => n switch
    {
        "boss" => I.Boss, "guard" => I.Guard, "bat" => I.Bat, "2" => I.M2, "4" => I.M4, "5" => I.M5, "nine" => I.Nine,
        "s2" => I.StrongWaves[0], "s3" => I.StrongWaves[1], "s4" => I.StrongWaves[2], "s5" => I.StrongWaves[3],
        _ => Waves.First(w => w.Name == n),
    };

    static void Log(Ver v, string wave, int seed)
    {
        var w = WaveOf(wave);
        var (r, _, _) = I.Raw(Apply(Pon, v), w, seed, verbose: true);
        Console.WriteLine($"# ポンの台 × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // §2 段0
    // ---------------------------------------------------------------------------------
    static void Stage0()
    {
        var (r, p, _) = I.Raw(Pon, I.Boss, 7);
        Console.WriteLine($"# {Tag} §2 段0 —— {PonRow}（{Seats(Pon)}）× ボス × seed 7: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・生存 {r.PlayerSurvivors}（ポンの画面: 4ターン・生存5体）");
        Console.WriteLine($"判定: {(r.PlayerWon && r.Turns == 4 && r.PlayerSurvivors == 5 ? "○" : "×")}");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 席の総当たり
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
    /// <summary>倒しT の区分（決めたこと: 勝率 50% 未満は「負け」・ほかは勝った戦の平均の倒しT で）。</summary>
    internal static int Bucket(Cell c) => c.Wins * 2 < c.N ? 3 : (double)c.WinT / c.Wins <= 4.0 ? 0 : (double)c.WinT / c.Wins <= 6.0 ? 1 : 2;
    internal static readonly string[] BucketNames = { "≤4T", "5〜6T", "7T〜", "負け" };

    static Dictionary<Seat, Cell> GridBoss(Ver v, Seat[] seats) => seats.ToDictionary(s => s, s => GridCell(Apply(s.F, v), I.Boss));

    static void SeatsOne(Ver v)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var seats = AllSeats();
        I.W[] ws = { I.Boss, I.Guard, I.Bat };
        var res = new Dictionary<(Seat, string), Cell>();
        foreach (var s in seats) foreach (var w in ws) res[(s, w.Name)] = GridCell(Apply(s.F, v), w);
        Console.WriteLine($"# {Tag} §3-1 席の総当たり —— ボルグ ／ ホタ ／ ヒヨの並び 60 × 埋め方 {Fillers.Length} × ボス ／ 近衛 ／ 大隊・seed 0..{GridSeeds - 1}・{v.Name}");
        Console.WriteLine();
        Console.WriteLine("埋め方の2枚は残りの席の番号（前1 → 前3 → 中央 → 後1 → 後3）の若いほうから順に置いた。区分 ＝ 勝率 50% 未満は「負け」、ほかは勝った戦の平均の倒しT。");
        Console.WriteLine();
        Console.WriteLine("## 1. ボスの倒しT の分布（並びの数）");
        Console.WriteLine();
        Console.WriteLine("| 埋め方 | ヒヨの席 | " + string.Join(" | ", BucketNames) + " | 計 |");
        Console.WriteLine("|---|---|" + string.Concat(BucketNames.Select(_ => "--:|")) + "--:|");
        foreach (var fill in Enumerable.Range(-1, Fillers.Length + 1))
            foreach (var row in new[] { "全部", "前列", "中央", "後列" })
            {
                var xs = seats.Where(s => (fill < 0 || s.Fill == fill) && (row == "全部" || Row(s.Hiyo) == row)).ToArray();
                var cnt = BucketNames.Select((_, b) => xs.Count(s => Bucket(res[(s, I.Boss.Name)]) == b)).ToArray();
                Console.WriteLine($"| {(fill < 0 ? "**全部**" : Fillers[fill].Name)} | {row} | " + string.Join(" | ", cnt) + $" | {xs.Length} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 2. 最良の並び（ボスの勝率 → 倒しT → 近衛 ／ 大隊の勝率 → 3波の倒れた数 → 精鋭の決着T の順）");
        Console.WriteLine();
        Console.WriteLine("| # | 並び（前1・前3・中央・後1・後3） | ボス 勝率・T・倒れた | 近衛 | 大隊 |");
        Console.WriteLine("|--:|---|---|---|---|");
        string WT(Cell c) => $"{Pct(c.Wins, c.N)}・{(c.Wins == 0 ? "—" : F1((double)c.WinT / c.Wins))}T・倒 {Per2(c.Fell, c.N)}";
        var ranked = seats.OrderByDescending(s => res[(s, I.Boss.Name)].Wins).ThenBy(s => res[(s, I.Boss.Name)].Wins == 0 ? 99 : (double)res[(s, I.Boss.Name)].WinT / res[(s, I.Boss.Name)].Wins)
            .ThenByDescending(s => res[(s, I.Guard.Name)].Wins + res[(s, I.Bat.Name)].Wins).ThenBy(s => res[(s, I.Boss.Name)].Fell + res[(s, I.Guard.Name)].Fell + res[(s, I.Bat.Name)].Fell)
            .ThenBy(s => res[(s, I.Guard.Name)].WinT + res[(s, I.Bat.Name)].WinT).ToArray();
        int k = 0;
        foreach (var s in ranked.Take(8))
            Console.WriteLine($"| {++k} | {s.Key}{(Seats(s.F) == Seats(Pon) ? "（ポンの台）" : "")} | {WT(res[(s, I.Boss.Name)])} | {WT(res[(s, I.Guard.Name)])} | {WT(res[(s, I.Bat.Name)])} |");
        Console.WriteLine();
        var pon = seats.First(s => Seats(s.F) == Seats(Pon));
        Console.WriteLine($"ポンの台の順位: {Array.IndexOf(ranked, pon) + 1} ／ {ranked.Length}（{WT(res[(pon, I.Boss.Name)])}）。第348期の代表台 `強化×燃` の並び（ヒヨ・ガルド・ホタ・ムド・ボルグ）: "
            + $"{WT(GridCell(Apply(CrossBuilds().First(r => r.Name == "強化×燃 (ヒヨ×ホタ×ボルグ)").F, v), I.Boss))}（埋め方の2枚の順が総当たりと逆なので別に回した）");
        Console.WriteLine();
        Console.WriteLine("## 3. 並びごと（ボス）—— ボルグ ／ ホタ ／ ヒヨの席別の平均（埋め方は全部）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | " + string.Join(" | ", SlotName) + " |");
        Console.WriteLine("|---|" + string.Concat(SlotName.Select(_ => "---|")));
        foreach (var (nm, sel) in new (string, Func<Seat, int>)[] { ("ボルグ", s => s.Borg), ("ホタ", s => s.Hota), ("ヒヨ", s => s.Hiyo) })
            Console.WriteLine($"| {nm} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(i =>
            {
                var xs = seats.Where(s => sel(s) == i).Select(s => res[(s, I.Boss.Name)]).ToArray();
                long n = xs.Sum(c => c.N), wn = xs.Sum(c => c.Wins), wt = xs.Sum(c => c.WinT);
                return $"{Pct(wn, n)}%・{(wn == 0 ? "—" : F1((double)wt / wn))}T";
            })) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
