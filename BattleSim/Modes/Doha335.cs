using BattleCore;
using static Common;
using D315 = Doha315Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// doha335 —— 第335期【軽い期】標軸の期 その2: 段0 ザンの ZC-a を規定に ／ 本体: ドハのマイナスとゴルムの力の行き先（DM-a 等分 ／ DM-c 溢れる ／ DM-c ＋ ゴルムが飲む ／ GL 力は殴る味方へ）。
// 指示書は design/PHASE335_DOHA_MINUS_GOLM_SPEC.md ／ 報告は design/PHASE335_DOHA_MINUS_GOLM.md。測定の作法は design/MEASURE_LIGHT.md（軽い期）。
//
//     dotnet run --project BattleSim -c Release 0 doha335 p0                      # §3 Phase 0（守り型 × 5 波 ＋ 強くした近衛 ／ 大隊: ドハが倒れる割合 ／ 直に狙われる回数 ／ ゴルムの力の行き先）
//     dotnet run --project BattleSim -c Release 0 doha335 boards [版,…]           # §6 代表台 6 × 5 波（＋ 守り型の強くした近衛 ／ 大隊）× 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 doha335 compare [版,…]          # §6 `compare` 64 行 × 版（seed 0..199・基準は規定 ＝ 段0 入り）
//     dotnet run --project BattleSim -c Release 0 doha335 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 doha335 check                   # §8 自己検査
//
// 版の名前（ASCII）: base（規定）／ a（DM-a）／ c（DM-c）／ cg（DM-c ＋ ゴルム）／ gl（GL）。波: boss ／ guard ／ bat ／ 2 ／ 5 ／ sguard ／ sbat。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p335/predict.md`）。
// =====================================================================================
static partial class Doha335Diag
{
    internal const string PredictFile = ".tmp/p335/predict.md";
    const int Seeds = 100;          // 軽い期（design/MEASURE_LIGHT.md §1）
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第335期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"doha335: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "log": Log(A(3, "guard"), A(4, "bat"), int.Parse(A(5, "0")), V(A(6, "base"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("doha335: モードは p0 / log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef? Doha, UnitDef? Golm, string Holder);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", null, null, "doha"),
        new("DM-a", "a", UnitCatalog.DohaDMa, null, "doha"),
        new("DM-c", "c", UnitCatalog.DohaDMc, null, "doha"),
        new("DM-c＋ゴルム", "cg", UnitCatalog.DohaDMc, UnitCatalog.GolmDrink, "doha"),
        new("GL", "gl", null, UnitCatalog.GolmGL, "golm"),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f;
        if (v.Doha is not null) g = S316.SwapId(g, "doha", v.Doha);
        if (v.Golm is not null) g = S316.SwapId(g, "golm", v.Golm);
        return g;
    }

    internal const string GuardRow = "試遊・標 守り型";
    internal const string NoGolmRow = "試遊・感電 雷の型";   // ドハがいてゴルムがいない台（第313期〜）
    static Formation Playtest(string n) => PlaytestH335.First(r => r.Name == n).F;   // 第336期: ドハを第317〜335期の規定に固定（いまの規定は DM-a）
    static Formation Row(string n) => CompareBuildsH335().First(r => r.Name == n).F;

    /// <summary>§6 の代表台（駒はいまの規定・段0 入り）。守り型を最初に。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("守り型", Playtest(GuardRow)),
        ("ボス台", Playtest("試遊・標 ボス台")),
        ("循環", Playtest("試遊・標 循環")),
        ("三人組", Playtest("試遊・標 三人組")),
        ("標経済", Row("標経済 (ヒサ×ザン×ミサ)")),
        ("雷の型", Playtest(NoGolmRow)),
    };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    /// <summary>強くした条件（第315期と同じ HP 500% ／ 攻 300% を各波の倍率に重ねる）の近衛 ／ 大隊。守り型だけ。</summary>
    internal static readonly S287.Wave[] StrongWaves = D315.MakeWaves(D315.StrongHp, D315.StrongAtk).Where(w => w.Name is "近衛" or "大隊").ToArray();
    static S287.Wave WaveOf(string n) => n switch { "sguard" => StrongWaves[0], "sbat" => StrongWaves[1], _ => T323.WaveOf(n) };
    static string WN(S287.Wave w) => StrongWaves.Contains(w) ? $"強 {w.Name}" : w.Name;

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose なし・帳簿だけから）
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, LossT, AllyDeaths;
        public readonly Dictionary<string, long> DmgBy = new(), RegurgTo = new();
        public readonly HashSet<string> NonStrikers = new();
        // ドハ
        public long DohaN, DohaDeaths, DohaDeathT, LoadAtDeath, Load, Direct, DirectSingle, Walled, Splits, SplitAmt, SplitKills, Overflows, OverflowAmt, OverflowKills, DohaTurns;
        // ゴルム
        public long GolmN, Drinks, Drank, DrankGain, DrinkT, GolmDealt, GolmAfterDrink, GlRedirects, GlAmt, GolmAtkEnd;

        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long DmgAll => DmgBy.Values.Sum();

        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LossT += o.LossT; AllyDeaths += o.AllyDeaths;
            DohaN += o.DohaN; DohaDeaths += o.DohaDeaths; DohaDeathT += o.DohaDeathT; LoadAtDeath += o.LoadAtDeath; Load += o.Load; Direct += o.Direct; DirectSingle += o.DirectSingle; Walled += o.Walled;
            Splits += o.Splits; SplitAmt += o.SplitAmt; SplitKills += o.SplitKills; Overflows += o.Overflows; OverflowAmt += o.OverflowAmt; OverflowKills += o.OverflowKills; DohaTurns += o.DohaTurns;
            GolmN += o.GolmN; Drinks += o.Drinks; Drank += o.Drank; DrankGain += o.DrankGain; DrinkT += o.DrinkT; GolmDealt += o.GolmDealt; GolmAfterDrink += o.GolmAfterDrink;
            GlRedirects += o.GlRedirects; GlAmt += o.GlAmt; GolmAtkEnd += o.GolmAtkEnd;
            foreach (var (src, dst) in new[] { (o.DmgBy, DmgBy), (o.RegurgTo, RegurgTo) })
                foreach (var (k, v) in src) dst[k] = dst.GetValueOrDefault(k) + v;
            NonStrikers.UnionWith(o.NonStrikers);
        }
    }

    static void Bump(Dictionary<string, long> d, string k, long v) { if (v > 0) d[k] = d.GetValueOrDefault(k) + v; }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new St { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else a.LossT = r.Turns;
        a.AllyDeaths = p.Count(u => u.LastDeathTurn > 0);
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => Short(g.First().Def));
        foreach (var u in p) if (!BattleContext.StrikesOnTurn(u.Def)) a.NonStrikers.Add(Short(u.Def));
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;   // 敵側の帳簿は数えない
            Bump(a.DmgBy, nm, t.DamageToEnemy);
            Bump(a.RegurgTo, nm, t.WhetGotRegurg);
            if (id == "doha")
            {
                a.DohaN = 1; a.Load += t.ShareLoad335; a.Direct += t.Dm335Direct; a.DirectSingle += t.Dm335DirectSingle; a.Walled += t.Dm335Walled;
                a.Splits += t.Dm335Splits; a.SplitAmt += t.Dm335SplitAmt; a.SplitKills += t.Dm335SplitKills;
                a.Overflows += t.Dm335Overflows; a.OverflowAmt += t.Dm335OverflowAmt; a.OverflowKills += t.Dm335OverflowKills; a.DohaTurns += t.TurnsTaken;
            }
            if (id == "golm")
            {
                a.GolmN = 1; a.GolmDealt += t.DamageToEnemy; a.GlRedirects += t.Gl335Redirects; a.GlAmt += t.Gl335Amt;
                if (t.Dm335Drank > 0) { a.Drinks = 1; a.Drank += t.Dm335Drank; a.DrankGain += t.Dm335DrankGain; a.DrinkT += t.Dm335DrinkTurn; a.GolmAfterDrink += t.DamageToEnemy - t.Dm335DealtAtDrink; }
            }
        }
        var doha = p.FirstOrDefault(u => u.Def.Id == "doha");
        if (doha is not null && doha.LastDeathTurn > 0) { a.DohaDeaths = 1; a.DohaDeathT = doha.LastDeathTurn; a.LoadAtDeath = r.TallyByUnit.GetValueOrDefault("doha")?.ShareLoad335 ?? 0; }
        var golm = p.FirstOrDefault(u => u.Def.Id == "golm");
        if (golm is not null) a.GolmAtkEnd = golm.CurrentAttack;
        return a;
    }

    internal static St Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, from + i));
        var all = new St();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static void Log(string board, string wave, int seed, Ver v)
    {
        var (name, f0) = board == "guard" ? Boards()[0] : Boards().First(b => b.Name == board);
        var w = WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, v), BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {WN(w)} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }
}
