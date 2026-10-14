using BattleCore;
using static Common;
using D315 = Doha315Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// zan334 —— 第334期【軽い期】標軸の期 その1: 段0 ミサの「抉る」（爪痕）をレリックへ ／ 本体: ザンの攻撃回数（ZC-a 中継では出ない ／ ZC-b 1ターン3回まで ／ ZC-c 返り血で倒れる ／ ZC-d 濡れ衣は1ターン1回）。
// 指示書は design/PHASE334_MARK_ZAN_SPEC.md ／ 報告は design/PHASE334_MARK_ZAN.md。測定の作法は design/MEASURE_LIGHT.md（軽い期）。
//
//     dotnet run --project BattleSim -c Release 0 zan334 p0                      # §3 Phase 0（守り型 × 5 波: ザンの攻撃の内訳・駒ごとの与ダメ・ゴルム ／ ドハ ／ ミサ・段0 の前後）
//     dotnet run --project BattleSim -c Release 0 zan334 boards [版,…]           # §6 代表台 5 × 5 波（＋ 守り型の強くした近衛 ／ 大隊）× 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 zan334 compare [版,…]          # §6 `compare` 64 行 × 版（seed 0..199・基準は規定 ＝ 段0 入り）
//     dotnet run --project BattleSim -c Release 0 zan334 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 zan334 check                   # §8 自己検査
//
// 版の名前（ASCII）: base（規定）／ a ／ b ／ c ／ d（ZC-a〜d）。版はザンを差し替える。波: boss ／ guard ／ bat ／ 2 ／ 5（強くした条件は sguard ／ sbat）。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p334/predict.md`）。
// =====================================================================================
static partial class Zan334Diag
{
    internal const string PredictFile = ".tmp/p334/predict.md";
    const int Seeds = 100;          // 軽い期（design/MEASURE_LIGHT.md §1）
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第334期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"zan334: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
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
            default: Console.WriteLine("zan334: モードは p0 / log / boards / compare / check。"); return;
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
    internal sealed record Ver(string Name, string Ascii, UnitDef Zan);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.Zan),
        new("ZC-a", "a", UnitCatalog.ZanZCa),
        new("ZC-b", "b", UnitCatalog.ZanZCb),
        new("ZC-c", "c", UnitCatalog.ZanZCc),
        new("ZC-d", "d", UnitCatalog.ZanZCd),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, "zan", v.Zan);

    internal const string GuardRow = "試遊・標 守り型";
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;

    /// <summary>§6 の代表台（駒はいまの規定・段0 入り）。守り型を最初に。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("守り型", Playtest(GuardRow)),
        ("ボス台", Playtest("試遊・標 ボス台")),
        ("循環", Playtest("試遊・標 循環")),
        ("三人組", Playtest("試遊・標 三人組")),
        ("標経済", Row("標経済 (ヒサ×ザン×ミサ)")),
    };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    /// <summary>強くした条件（第315期と同じ HP 500% ／ 攻 300% を各波の倍率に重ねる）の近衛 ／ 大隊。守り型だけ。</summary>
    internal static readonly S287.Wave[] StrongWaves = D315.MakeWaves(D315.StrongHp, D315.StrongAtk).Where(w => w.Name is "近衛" or "大隊").ToArray();
    static S287.Wave WaveOf(string n) => n switch
    {
        "sguard" => StrongWaves[0], "sbat" => StrongWaves[1], _ => T323.WaveOf(n),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose なし・帳簿だけから）
    // ---------------------------------------------------------------------------------
    internal static readonly string[] KindNames = { "直", "ドハ経由", "ゴルム経由", "ほかの中継", "濡れ衣" };

    internal sealed class St
    {
        public long N, Wins, WinT, LossT, AllyDeaths;
        public readonly Dictionary<string, long> DmgBy = new();
        // ザン
        public long[] Kind = new long[5], KindDealt = new long[5];
        public long RoundMaxSum, RoundPeak, ZanTurns, RoundTurns, RoundSlashes, RoundDealt, NormalDealt, ZanDealt, RelaySkipped, Capped, FrameCapped, RecoilDeaths, RecoilDeathTurn, RecoilTaken, ZanDeaths;
        // ゴルム
        public long Swallowed, Drain, Refunded, GolmDealt, GolmAttacks, GolmTurns;
        public readonly Dictionary<string, long> RegurgTo = new();
        // ドハ
        public long DohaTurns, Pushes, Passed, DohaDealt, DohaAttacks;
        public readonly Dictionary<string, long> PushTo = new();
        // ミサ
        public long Volleys, Chased, Sprayed, MfFoe, MfAlly, MisaDealt, Scar;
        public readonly Dictionary<string, long> FfTo = new();
        // ヒサ
        public long Accuses;

        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long ZanAvenges => Kind.Sum();
        public long ZanAvengeDealt => KindDealt.Sum();
        public long DmgAll => DmgBy.Values.Sum();

        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LossT += o.LossT; AllyDeaths += o.AllyDeaths;
            for (int k = 0; k < 5; k++) { Kind[k] += o.Kind[k]; KindDealt[k] += o.KindDealt[k]; }
            RoundMaxSum += o.RoundMaxSum; RoundPeak = Math.Max(RoundPeak, o.RoundPeak); ZanTurns += o.ZanTurns; RoundTurns += o.RoundTurns; RoundSlashes += o.RoundSlashes;
            RoundDealt += o.RoundDealt; NormalDealt += o.NormalDealt; ZanDealt += o.ZanDealt; RelaySkipped += o.RelaySkipped; Capped += o.Capped; FrameCapped += o.FrameCapped;
            RecoilDeaths += o.RecoilDeaths; RecoilDeathTurn += o.RecoilDeathTurn; RecoilTaken += o.RecoilTaken; ZanDeaths += o.ZanDeaths;
            Swallowed += o.Swallowed; Drain += o.Drain; Refunded += o.Refunded; GolmDealt += o.GolmDealt; GolmAttacks += o.GolmAttacks; GolmTurns += o.GolmTurns;
            DohaTurns += o.DohaTurns; Pushes += o.Pushes; Passed += o.Passed; DohaDealt += o.DohaDealt; DohaAttacks += o.DohaAttacks;
            Volleys += o.Volleys; Chased += o.Chased; Sprayed += o.Sprayed; MfFoe += o.MfFoe; MfAlly += o.MfAlly; MisaDealt += o.MisaDealt; Scar += o.Scar; Accuses += o.Accuses;
            foreach (var (src, dst) in new[] { (o.DmgBy, DmgBy), (o.RegurgTo, RegurgTo), (o.PushTo, PushTo), (o.FfTo, FfTo) })
                foreach (var (k, v) in src) dst[k] = dst.GetValueOrDefault(k) + v;
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
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;   // 敵側の帳簿は数えない
            Bump(a.DmgBy, nm, t.DamageToEnemy);
            Bump(a.RegurgTo, nm, t.WhetGotRegurg);
            Bump(a.PushTo, nm, t.PushGot);
            Bump(a.FfTo, nm, t.FeatherFfBeckonHits + t.FeatherFfOtherHits);
            switch (id)
            {
                case "zan":
                    if (t.Zc334Kind is { } kk) for (int k = 0; k < 5; k++) { a.Kind[k] += kk[k]; a.KindDealt[k] += t.Zc334KindDealt![k]; }
                    a.RoundMaxSum += t.Zc334RoundMax; a.RoundPeak = Math.Max(a.RoundPeak, t.Zc334RoundPeak);
                    a.ZanTurns += t.ZanTurns; a.RoundTurns += t.RoundTurns; a.RoundSlashes += t.RoundSlashes; a.RoundDealt += t.RoundDealt; a.NormalDealt += t.ZanTurnDealt;
                    a.ZanDealt += t.DamageToEnemy; a.RelaySkipped += t.Zc334RelaySkipped; a.Capped += t.Zc334Capped; a.FrameCapped += t.Zc334FrameCapped;
                    a.RecoilDeaths += t.RecoilDeaths; a.RecoilDeathTurn += t.RecoilDeathTurn; a.RecoilTaken += t.RecoilTaken;
                    break;
                case "golm":
                    a.Swallowed += t.Swallowed; a.Drain += t.DamageToAlly; a.Refunded += t.Refunded; a.GolmDealt += t.DamageToEnemy; a.GolmAttacks += t.Attacks; a.GolmTurns += t.TurnsTaken;
                    break;
                case "doha":
                    a.DohaTurns += t.TurnsTaken; a.Pushes += t.Pushes; a.Passed += t.PushPassed; a.DohaDealt += t.DamageToEnemy; a.DohaAttacks += t.Attacks;
                    break;
                case "tome":
                    a.Volleys += t.FeatherVolleys; a.Chased += t.FeatherChased; a.Sprayed += t.FeatherSprayed; a.MfFoe += t.MfShotsFoe; a.MfAlly += t.MfShotsAlly; a.MisaDealt += t.DamageToEnemy; a.Scar += t.RuptureScar;
                    break;
                case "hisa":
                    a.Accuses += t.FrameAccuses;
                    break;
            }
        }
        var zan = p.FirstOrDefault(u => u.Def.Id == "zan");
        if (zan is not null && zan.LastDeathTurn > 0) a.ZanDeaths = 1;
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
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }
}
