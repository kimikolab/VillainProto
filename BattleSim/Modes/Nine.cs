using BattleCore;
using static Common;

// =====================================================================================
// nine モード（第221期） —— 9体の検証波（敵専用の9枠の入口）
//
// 指示書は design/PHASE221_NINE_WAVE_SPEC.md ／ 報告は design/PHASE221_NINE_WAVE.md。
// **線は置かない。** 本編の5波（`EnemyCatalog.Stages`）は触らない。検証の波は `EnemyCatalog.TestStages`。
//
//     dotnet run --project BattleSim -c Release 0 nine phase0   # 盤面の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 nine run      # 表A〜D（61 行 × 検証の3波 ／ 雷の台）
//     dotnet run --project BattleSim -c Release 0 nine check    # 自己検査（受け入れ 2〜4）
// =====================================================================================

static partial class NineDiag
{
    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(); return;
            default:
                Console.WriteLine("nine: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunImpl(string arg);
    static partial void CheckImpl();

    internal const int Seeds = 200;
    internal static readonly HashSet<string> FoeIds = new() { "recruit", "levy" };

    // ---- 行の群（指示書 §4 P2・**測る前に固定**）----
    internal static bool HasPattern(Formation f, params AttackPattern[] ps) => f.Occupied().Any(o => ps.Contains(o.Def.Pattern));
    internal static bool HasId(Formation f, params string[] ids) => f.Occupied().Any(o => ids.Contains(o.Def.Id));
    internal static readonly (string Name, Func<Formation, bool> In)[] Groups =
    {
        ("薙ぎ・全体の駒がいる", f => HasPattern(f, AttackPattern.Sweep, AttackPattern.All)),
        ("貫きの駒がいる", f => HasPattern(f, AttackPattern.Pierce)),
        ("範囲の駒が1枚もいない", f => !HasPattern(f, AttackPattern.Sweep, AttackPattern.All, AttackPattern.Pierce)),
        ("撃破の読み手（ハギ・エグ・リィカ・ゾト）", f => HasId(f, "hagi", "egu", "rica", "zoto")),
        ("毒撒き（グザ）", f => HasId(f, "guza")),
        ("単体高倍率（ドルガ）", f => HasId(f, "dolga")),
        ("被弾で稼ぐ耐久（棘のカド）", f => HasId(f, "kado")),   // ガルド・ゴルムは 61 行中 58 行にいて群にならない（Phase 0）
    };

    // =================================================================================
    // Phase 0（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第221期 Phase 0 —— 9体の検証波（戦闘0回）");
        Console.WriteLine();
        Console.WriteLine("## 検証の波（`EnemyCatalog.TestStages`）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 体数 | 席 | 総HP（素） | 総攻/T（素） | 総HP（×115） | 総攻/T（×115） |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|");
        foreach (var t in EnemyCatalog.TestStages)
        {
            var u = BattleEngine.MaterializeEnemy(t.Enemy);
            var raw = t.Enemy.Occupied().ToList();
            Console.WriteLine($"| {t.Name} | {raw.Count} | {string.Join(" ", raw.Select(o => FormationRules.SeatNames[o.Seat]))} | {raw.Sum(o => o.Def.MaxHp)} | {raw.Sum(o => o.Def.Attack)} | {u.Sum(x => x.MaxHp)} | {u.Sum(x => x.Def.Attack)} |");
        }
        var w1 = BattleEngine.Materialize(EnemyCatalog.Stages[0].Enemy, BattleContext.EnemyTeam);
        Console.WriteLine($"| （参考）{EnemyCatalog.Stages[0].Name} | {w1.Count} | — | {EnemyCatalog.Stages[0].Enemy.Occupied().Sum(o => o.Def.MaxHp)} | {EnemyCatalog.Stages[0].Enemy.Occupied().Sum(o => o.Def.Attack)} | {w1.Sum(x => x.MaxHp)} | {w1.Sum(x => x.Def.Attack)} |");
        Console.WriteLine();

        Console.WriteLine("## 9マスの表（X 字・盤面の表をそのまま引く）");
        Console.WriteLine();
        Console.WriteLine("| 席 | slot | 行 | 貫きの経路 | 隣接（9体のとき） | 薙ぎの巻き込み |");
        Console.WriteLine("|---|--:|---|---|---|---|");
        for (int s = 0; s < FormationRules.TotalSlots; s++)
        {
            var lanes = FormationRules.LanesOf(s);
            var adj = Enumerable.Range(0, 9).Where(b => FormationRules.AreAdjacent(s, b)).ToList();
            Console.WriteLine($"| {FormationRules.SeatNames[s]} | {s} | {FormationRules.RowOf(s)} | {(lanes.Count == 0 ? "なし" : string.Join(" と ", lanes.Select(l => l == 0 ? "1" : "3")))} | {adj.Count}: {string.Join(" ", adj.Select(b => FormationRules.SeatNames[b]))} | {string.Join(" ", FormationRules.SweepTargets(s).Select(b => FormationRules.SeatNames[b]))} |");
        }
        int edges9 = 0, edges5 = 0;
        for (int a = 0; a < 9; a++) for (int b = a + 1; b < 9; b++)
            if (FormationRules.AreAdjacent(a, b)) { edges9++; if (a < 5 && b < 5) edges5++; }
        Console.WriteLine();
        Console.WriteLine($"隣接の辺: 5体（席 0〜4）{edges5} 本・平均次数 {2.0 * edges5 / 5:F2} ／ 9体 {edges9} 本・平均次数 {2.0 * edges9 / 9:F2}");
        Console.WriteLine();

        Console.WriteLine("## 行の群（61 行・指示書 §4 P2・測る前に固定）");
        Console.WriteLine();
        var rows = CompareBuilds();
        Console.WriteLine("| 群 | 行数 |");
        Console.WriteLine("|---|--:|");
        foreach (var (n, inG) in Groups) Console.WriteLine($"| {n} | {rows.Count(r => inG(r.F))} |");
        Console.WriteLine();
    }

    // =================================================================================
    // 集計
    // =================================================================================

    internal sealed class NAgg
    {
        public long N, Wins, AllSurv, WinTurns;
        public long FoeTurns, FoeAttacks, PlayerTaken, FoeTaken, Turns;
        public long PierceDead;
        public long EChainRoots, EChainUnits, EChainBig;   // 敵を根とする感電の連鎖（根の側）・3体以上
        public long PChainRoots, PChainUnits;
        public readonly long[] BurstLen = new long[11];
        public long FoeDeaths, FoeNeighborSum, FoeBursts, FoeBurstHits;

        public void Take(BattleResult r, HashSet<string> playerIds)
        {
            N++; Turns += r.Turns;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            PierceDead += r.PierceDeadEnds;
            foreach (var (id, t) in r.TallyByUnit)
            {
                if (FoeIds.Contains(id))
                {
                    FoeTurns += t.TurnsTaken; FoeAttacks += t.Attacks; FoeTaken += t.DamageTaken;
                    EChainRoots += t.ChainRoots; EChainUnits += t.ChainUnits;
                    if (t.ChainSizeHist is { } h) for (int i = 3; i < h.Length; i++) EChainBig += h[i];
                }
                else if (playerIds.Contains(id))
                {
                    PlayerTaken += t.DamageTaken;
                    PChainRoots += t.ChainRoots; PChainUnits += t.ChainUnits;
                }
            }
            if (r.Burst is BurstLedger b)
            {
                for (int i = 0; i < 11; i++) BurstLen[i] += b.ChainLenHist[i];
                FoeDeaths += b.Deaths[0]; FoeNeighborSum += b.NeighborSum[0];
                FoeBursts += b.Bursts[0]; FoeBurstHits += b.Hits[0];
            }
        }

        public void Merge(NAgg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns;
            FoeTurns += o.FoeTurns; FoeAttacks += o.FoeAttacks; PlayerTaken += o.PlayerTaken; FoeTaken += o.FoeTaken; Turns += o.Turns;
            PierceDead += o.PierceDead;
            EChainRoots += o.EChainRoots; EChainUnits += o.EChainUnits; EChainBig += o.EChainBig;
            PChainRoots += o.PChainRoots; PChainUnits += o.PChainUnits;
            for (int i = 0; i < 11; i++) BurstLen[i] += o.BurstLen[i];
            FoeDeaths += o.FoeDeaths; FoeNeighborSum += o.FoeNeighborSum; FoeBursts += o.FoeBursts; FoeBurstHits += o.FoeBurstHits;
        }

        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;
        public double Per(long x) => (double)x / Math.Max(1, N);
        public double ChainMean => EChainRoots == 0 ? double.NaN : (double)EChainUnits / EChainRoots;
        public double ChainBigPct => EChainRoots == 0 ? double.NaN : 100.0 * EChainBig / EChainRoots;
        public long BurstChains => BurstLen.Sum();
        public double BurstMultiPct => BurstChains == 0 ? double.NaN : 100.0 * BurstLen.Skip(2).Sum() / BurstChains;
        public double BurstLenMean => BurstChains == 0 ? double.NaN : (double)Enumerable.Range(0, 11).Sum(i => (long)i * BurstLen[i]) / BurstChains;
        public double NeighborMean => FoeDeaths == 0 ? double.NaN : (double)FoeNeighborSum / FoeDeaths;
    }

    /// <summary>敵の作り方（seed に依らない）。</summary>
    internal static Func<List<UnitState>> Wave(EnemyWave w, EnemyScaleRule sc) => () => BattleEngine.MaterializeEnemy(w, sc);
    internal static Func<List<UnitState>> Stage(Formation f, EnemyScaleRule sc) => () => BattleEngine.Materialize(f, BattleContext.EnemyTeam, sc);

    internal static NAgg Measure(Formation f, Func<List<UnitState>> enemy, int seed0 = 0, int seeds = Seeds)
    {
        var playerIds = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        var total = new NAgg();
        var gate = new object();
        Parallel.For(0, seeds, () => new NAgg(), (j, _, local) =>
        {
            local.Take(BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), enemy(), seed0 + j, verbose: false), playerIds);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    internal static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    internal static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");
    internal static string D1(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;±0.0");
}
