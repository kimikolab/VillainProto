using BattleCore;
using static Common;
using D326 = Dense326Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// stun331 —— 第331期【軽い期】感電の痺れで敵が止まりすぎる（ST-a ハメ防止 ／ ST-b 痺れ慣れ ／ ST-c 1ターン1回）。
// 指示書は design/PHASE331_SHOCK_STUN_GUARD_SPEC.md ／ 報告は design/PHASE331_SHOCK_STUN_GUARD.md。測定の作法は design/MEASURE_LIGHT.md（軽い期）。
//
//     dotnet run --project BattleSim -c Release 0 stun331 p0                     # §3 Phase 0（規定・ポンの台: 痺れで失った敵の手番・弾けた回数・破片・味方が受けた攻撃）
//     dotnet run --project BattleSim -c Release 0 stun331 log [波] [seed] [版]   # ポンの台の1戦のログ（読み物）
//     dotnet run --project BattleSim -c Release 0 stun331 boards [版,…]          # §5 代表台 5 × 5 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 stun331 compare [版,…]         # §5 `compare` 64 行 × 版（seed 0..199・基準は規定）
//     dotnet run --project BattleSim -c Release 0 stun331 check                  # §8 自己検査
//
// 版の名前（ASCII）: base（規定）／ a（ST-a）／ b（ST-b）／ c（ST-c）。波: boss ／ guard ／ bat ／ 2 ／ 5。版はトウ ／ カタ（痺れの札の保持者）を差し替える。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p331/predict.md`）。
// =====================================================================================
static partial class Stun331Diag
{
    internal const string PredictFile = ".tmp/p331/predict.md";
    const int Seeds = 100;          // 軽い期（design/MEASURE_LIGHT.md §1）
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第331期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"stun331: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "log": Log(A(3, "guard"), int.Parse(A(4, "10")), V(A(5, "b"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("stun331: モードは p0 / log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long H(long[]? h, int i) => T323.H(h, i);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef Tou, UnitDef Kata);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.TouH331, UnitCatalog.KataH331),   // 第332期: 第330〜331期の規定に固定（いまの規定は ST-c）
        new("ST-a", "a", UnitCatalog.TouSTa, UnitCatalog.KataSTa),
        new("ST-b", "b", UnitCatalog.TouSTb, UnitCatalog.KataSTb),
        new("ST-c", "c", UnitCatalog.TouSTc, UnitCatalog.KataSTc),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(S316.SwapId(f, "tou", v.Tou), "kata", v.Kata);

    /// <summary>ポンの台（第326〜330期と同じ席）。駒はいまの規定。</summary>
    internal static Formation PonBoard() => Formation.Build(front1: UnitCatalog.Kata, front3: UnitCatalog.Shiga, center: UnitCatalog.Tou, back1: UnitCatalog.Kugu, back3: UnitCatalog.Som);
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;

    /// <summary>§5 の代表台（駒はいまの規定）。ポンの台を最初に。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("ポンの台", PonBoard()),
        ("光の盾 鞭", Playtest("試遊・感電 光の盾 鞭")),
        ("光の盾", Playtest("試遊・感電 光の盾")),
        ("粉 隣あり", Playtest("試遊・感電 粉 隣あり")),
        ("感電", CompareBuilds().First(r => r.Name == "感電 (シガ×カタ×ソム)").F),
    };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    static S287.Wave WaveOf(string n) => T323.WaveOf(n);
    static bool IsElite(S287.Wave w) => T323.IsElite(w);

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Z
    {
        public D326.Z D = new();
        /// <summary>敵（波の駒・喚ばれたもの ／ 糸玉を除く）の手番: 回ってきた ／ 動いた（潰れなかった）／ 感電の痺れで失った ／ ほかで潰れた。</summary>
        public long FoeTurns, FoeActed, FoeShockLost, FoeOtherStall, FoeStallCanAct, FoeStallOtherStun;
        /// <summary>敵が続けて手番を失った最長（1戦の最大の和）・1体が1ターンに弾けた回数（弾けた (駒, ターン) の数 ／ 弾けの延べ ／ 最大）。</summary>
        public long LostRunMaxSum, LostRunMax, PopPairs, Pops, PopMax;
        /// <summary>敵の弾けの延べ: 痺れた ／ 50% で外れた ／ ハメ防止で痺れなかった ／ そのうち萎縮した（ST-b）／ 2回目以降で判定しなかった（ST-c）／ 萎縮（動じない）。</summary>
        public long FoeStunned, FoeStunMissed, FoeGuarded, FoeGuardDaunted, FoeOnceSkip, FoeDaunted;
        /// <summary>味方が敵から受けた攻撃（`Attack` の主目標が味方）／ 敵から受けたダメージ。</summary>
        public long AllyAttacked, AllyDmg;
        /// <summary>ソムの破片（光の衣）: 得た ／ 使った（受け止めた）。</summary>
        public long VeilAdded, VeilUsed;
        public UnitTally Shiga = new();
        public long ShigaN;
        public long N => D.N;
        public long Wins => D.Wins;
        public double Win => D.Win;
        public void Merge(Z o)
        {
            D.Merge(o.D);
            FoeTurns += o.FoeTurns; FoeActed += o.FoeActed; FoeShockLost += o.FoeShockLost; FoeOtherStall += o.FoeOtherStall; FoeStallCanAct += o.FoeStallCanAct; FoeStallOtherStun += o.FoeStallOtherStun;
            LostRunMaxSum += o.LostRunMaxSum; LostRunMax = Math.Max(LostRunMax, o.LostRunMax); PopPairs += o.PopPairs; Pops += o.Pops; PopMax = Math.Max(PopMax, o.PopMax);
            FoeStunned += o.FoeStunned; FoeStunMissed += o.FoeStunMissed; FoeGuarded += o.FoeGuarded; FoeGuardDaunted += o.FoeGuardDaunted; FoeOnceSkip += o.FoeOnceSkip; FoeDaunted += o.FoeDaunted;
            AllyAttacked += o.AllyAttacked; AllyDmg += o.AllyDmg; VeilAdded += o.VeilAdded; VeilUsed += o.VeilUsed;
            Shiga.Add(o.Shiga); ShigaN += o.ShigaN;
        }
    }

    internal static Z Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var z = new Z { D = D326.CountZ(p, e, r, w) };
        var pIds = p.Select(u => u.InstanceId).ToHashSet();
        var pDefs = p.Select(u => u.Def.Id).ToHashSet();
        var foeDefs = e.Select(u => u.Def.Id).Where(id => !pDefs.Contains(id)).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (foeDefs.Contains(id))
            {
                z.FoeTurns += t.TurnsTaken; z.FoeActed += t.TurnsTaken - t.TurnStalls; z.FoeShockLost += t.StallShockStun; z.FoeOtherStall += t.TurnStalls - t.StallShockStun; z.FoeStallCanAct += t.StallGrappled + t.StallCowed; z.FoeStallOtherStun += t.StallStun - t.StallShockStun;
                z.FoeStunned += t.ShockStunned; z.FoeStunMissed += t.ShockStunMissed; z.FoeGuarded += t.ShockStunGuarded; z.FoeGuardDaunted += t.St331GuardDaunted; z.FoeOnceSkip += t.St331OnceSkip; z.FoeDaunted += t.ShockDaunted;
            }
            if (pDefs.Contains(id)) z.VeilUsed += t.SparkVeilSoaked;
            if (id == "som") z.VeilAdded += t.SparkVeilAdded;
            if (id == "shiga") { z.Shiga.Add(t); z.ShigaN = 1; }
        }
        var foeIds = e.Where(u => foeDefs.Contains(u.Def.Id)).Select(u => u.InstanceId).ToHashSet();
        var lost = new Dictionary<int, List<int>>();
        var pops = new Dictionary<(int, int), int>();
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Attack when ev.TargetId is int at && pIds.Contains(at) && ev.ActorId is int aa && !pIds.Contains(aa):
                    z.AllyAttacked++; break;
                case BattleEventKind.Damage when ev.TargetId is int dt && pIds.Contains(dt) && ev.ActorId is int da && !pIds.Contains(da):
                    z.AllyDmg += ev.Amount; break;
                case BattleEventKind.Stun when ev.Text == StunLabels.Lost && ev.TargetId is int st && foeIds.Contains(st):
                    (lost.TryGetValue(st, out var l) ? l : lost[st] = new List<int>()).Add(ev.Turn); break;
                case BattleEventKind.ShockSpent when ev.TargetId is int sp && foeIds.Contains(sp):
                    pops[(sp, ev.Turn)] = pops.GetValueOrDefault((sp, ev.Turn)) + 1; break;
            }
        }
        int run = 0;
        foreach (var ts in lost.Values)
        {
            int cur = 1;
            for (int i = 1; i < ts.Count; i++) { cur = ts[i] == ts[i - 1] + 1 ? cur + 1 : ts[i] == ts[i - 1] ? cur : 1; run = Math.Max(run, cur); }
            run = Math.Max(run, ts.Count > 0 ? 1 : 0);
        }
        z.LostRunMaxSum = run; z.LostRunMax = run;
        z.PopPairs = pops.Count; z.Pops = pops.Values.Sum(); z.PopMax = pops.Count == 0 ? 0 : pops.Values.Max();
        return z;
    }

    internal static Z Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var parts = new Z[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, from + i));
        var z = new Z();
        foreach (var c in parts) z.Merge(c);
        return z;
    }

    static void Log(string wave, int seed, Ver v)
    {
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(Apply(PonBoard(), v), BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: true);
        Console.WriteLine($"# ポンの台 × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }
}
