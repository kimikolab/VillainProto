using BattleCore;
using static Common;

// =====================================================================================
// spring —— 第228期「突き返しのハネ：手番は吹っ飛ばし、殴られたら弾き返し」。
// 指示書は design/PHASE228_SPRING_SPEC.md ／ 報告は design/PHASE228_SPRING.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 spring phase0   # Q0-1〜Q0-5（H0 だけで回る）
//     dotnet run --project BattleSim -c Release 0 spring run      # 表A〜F
//     dotnet run --project BattleSim -c Release 0 spring check    # 自己検査（受け入れ 2〜4・7）
// =====================================================================================
static partial class SpringDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            default:
                Console.WriteLine("spring: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    internal const int Seeds = 200;

    // =================================================================================
    // 版（§5）。**札の差し替え**だけ（今の札は消さない）。名前で引くのは Phase 0 のコミット（札が無い）でも回るように。
    //   H0 前段の規定 ／ H1 吹っ飛ばし（`Blast`）／ H2 弾き返し（`Spring`）／ H3 両方 ／ H3w 両方 ＋ 段2 で両方の経路（`BlastBoth`）
    // =================================================================================

    static TraitId? T(string name) => Enum.TryParse<TraitId>(name, out var t) ? t : null;
    static UnitDef? With(UnitDef d, params string[] names)
    {
        var extra = names.Select(T).ToList();
        if (extra.Any(x => x is null)) return null;
        return DecoyDiag.Copy(d, d.Traits.Concat(extra.Select(x => x!.Value)).ToArray());
    }

    internal sealed record Ver(string Tag, UnitDef Hane);

    internal static Ver[] Versions
    {
        get
        {
            UnitDef h0 = UnitCatalog.HaneH0;
            var list = new List<Ver> { new("H0", h0) };
            foreach (var (tag, names) in new[] { ("H1", new[] { "Blast" }), ("H2", new[] { "Spring" }), ("H3", new[] { "Blast", "Spring" }), ("H3w", new[] { "Blast", "Spring", "BlastBoth" }) })
                if (With(h0, names) is { } d) list.Add(new(tag, d));
            return list.ToArray();
        }
    }
    internal static Ver VerOf(string tag) => Versions.First(v => v.Tag == tag);

    /// <summary>編成のハネだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied()) g[slot] = d.Id == "hane" ? v.Hane : d;
        return g;
    }

    // 台（§8.1）
    /// <summary>第227期 L2 の総当たりの1位（前1 セロ ／ 前3 バサ ／ 中央 ヨミ ／ 後1 ハネ ／ 後3 シオ）。セロは前段で L2 が規定。</summary>
    internal static Formation MHane227 => Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.BasaG0,
        center: UnitCatalog.Yomi, back1: UnitCatalog.HaneH0, back3: UnitCatalog.Shio);
    internal static Formation Thunder => DecoyDiag.Thunder;
    internal static List<(string Name, Formation F)> HaneRows() =>
        CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "hane")).Select(r => (r.Name, OldGale(r.F))).ToList();   // 第230期 前段: バサを第229期の規定に固定

    internal static readonly string[] WaveNames = DriftDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => DriftDiag.WaveOf(w, sc);
    internal const int MainWave = 4;
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales = DecoyDiag.Scales;
    internal static string SeatsNamed(Formation f) => DriftDiag.SeatsNamed(f);
    internal static string F1(double x) => DriftDiag.F1(x);
    internal static string F2(double x) => DriftDiag.F2(x);

    /// <summary>後の期に足した帳簿の欄（名前で引く・Phase 0 のコミットでも回るように）。</summary>
    internal static long Tally(UnitTally t, string name)
        => typeof(UnitTally).GetField(name)?.GetValue(t) is { } v ? Convert.ToInt64(v) : 0;

    // =================================================================================
    // 席（§8.1）: 版 × 九/新兵 × 200/200 × seed 1000..1199・120 通り（全員生存 → 落ちた駒 → 決着T）。
    // =================================================================================

    internal const int PickSeed0 = 1000, PickSeeds = 200;

    internal static List<(Formation F, int W, int Sv, int Fell, long T)> PickSeats(Formation raw, Ver v)
    {
        var sc = Scales[0].Sc;
        var members = Apply(raw, v).Occupied().Select(o => o.Def).ToList();
        var perms = SeroDiag.Permute(members).Select(p => Formation.Build(front1: p[0], front3: p[1], center: p[2], back1: p[3], back3: p[4])).ToList();
        var res = new (Formation F, int W, int Sv, int Fell, long T)[perms.Count];
        Parallel.For(0, perms.Count, i =>
        {
            int w = 0, sv = 0, fell = 0; long t = 0;
            for (int s = PickSeed0; s < PickSeed0 + PickSeeds; s++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(perms[i]), BattleContext.PlayerTeam), WaveOf(MainWave, sc)(), s, verbose: false, shuffler: PreHole);
                fell += r.PlayerStarterFallen.Count;
                if (!r.PlayerWon) continue;
                w++; t += r.Turns; if (r.PlayerStarterFallen.Count == 0) sv++;
            }
            res[i] = (perms[i], w, sv, fell, t);
        });
        return res.Select((x, i) => (x, i)).OrderByDescending(z => z.x.Sv).ThenBy(z => z.x.Fell)
            .ThenBy(z => z.x.W == 0 ? long.MaxValue : z.x.T * 1000 / z.x.W).ThenBy(z => z.i).Select(z => z.x).ToList();
    }

    // =================================================================================
    // 帳簿
    // =================================================================================

    /// <summary>経路（X 字の貫きのレーン）に属する席。○前2（7）・○後2（8）は属さない。</summary>
    internal static bool OnLane(int slot) => slot <= 6;

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns, FellTotal;
        public readonly Dictionary<string, long> Fell = new(), FellT = new();
        // ハネ（表D）
        /// <summary>粛の保持者が倒れた戦の数とそのターンの和（第二波の読み）。</summary>
        public long HushDead, HushDeadTurn, HushBlocked;
        public long HaneTaken, HaneFell, HaneHits, HaneHitsOnLane, HaneHitsBack, HaneHitsReaction, OverrunSwaps;
        // 吹っ飛ばし・弾き返し（表B・C。帳簿の欄は名前で引く）
        public readonly Dictionary<string, long> Hane = new();
        // 敵の乱れ（表E）——第226期の帳簿をそのまま借りる
        public readonly DecoyDiag.Agg D = new();

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns; FellTotal += o.FellTotal;
            foreach (var (k, v) in o.Fell) Fell[k] = Fell.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.FellT) FellT[k] = FellT.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Hane) Hane[k] = Hane.GetValueOrDefault(k) + v;
            HushDead += o.HushDead; HushDeadTurn += o.HushDeadTurn; HushBlocked += o.HushBlocked;
            HaneTaken += o.HaneTaken; HaneFell += o.HaneFell; HaneHits += o.HaneHits; HaneHitsOnLane += o.HaneHitsOnLane;
            HaneHitsBack += o.HaneHitsBack; HaneHitsReaction += o.HaneHitsReaction; OverrunSwaps += o.OverrunSwaps;
            D.Merge(o.D);
        }
        public double Per(long x) => (double)x / Math.Max(1, N);
        public double PerK(string k) => Per(Hane.GetValueOrDefault(k));
        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;

        internal static readonly string[] HaneKeys =
        {
            "BlastCount", "BlastMoved", "BlastForward", "BlastHits", "BlastDealt", "BlastConfused", "BlastKilledA", "BlastSecond", "ReboundNoFront",
            "SpringCount", "SpringForward", "SpringConfused", "SpringCapped", "SpringHushed", "SpringHeld", "SpringNoSeat", "SpringSwaps",
            "ReboundThrusts", "DisarrayPushTwo",
        };

        public void Take(BattleResult r, List<UnitState> player, List<UnitState> enemy, Dictionary<int, int> slot0)
        {
            N++; FellTotal += r.PlayerStarterFallen.Count;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            D.Take(r, player, enemy, slot0);
            var byId = player.ToDictionary(u => u.InstanceId);
            var enemyIds = enemy.Select(u => u.InstanceId).ToHashSet();
            UnitState? hane = player.FirstOrDefault(u => u.Def.Id == "hane");
            if (hane is not null && r.TallyByUnit.TryGetValue("hane", out var t))
            {
                foreach (var k in HaneKeys) Hane[k] = Hane.GetValueOrDefault(k) + Tally(t, k);
                OverrunSwaps += t.OverrunSwaps;
            }
            var hushIds = enemy.Where(u => u.HasTrait(TraitId.Hush)).Select(u => u.InstanceId).ToHashSet();
            var tr = new DecoyDiag.Tracker(slot0, player);
            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind == BattleEventKind.Death && e.TargetId is int hd && hushIds.Contains(hd)) { HushDead++; HushDeadTurn += e.Turn; }
                if (e.Kind == BattleEventKind.Sealed && e.Text == SealedLabels.Hush) HushBlocked++;
                switch (e.Kind)
                {
                    case BattleEventKind.Damage when hane is not null && e.TargetId == hane.InstanceId && e.Amount > 0:
                        HaneTaken += e.Amount;
                        if (e.ActorId is int a && enemyIds.Contains(a) && tr.Alive.Contains(a))
                        {
                            HaneHits++;
                            if (e.Reaction) HaneHitsReaction++;
                            int s = tr.Slot[a];
                            if (OnLane(s)) HaneHitsOnLane++;
                            if (s is 3 or 4) HaneHitsBack++;
                        }
                        break;
                    case BattleEventKind.Death when e.TargetId is int d && byId.TryGetValue(d, out var du):
                        Fell[du.Def.Id] = Fell.GetValueOrDefault(du.Def.Id) + 1;
                        FellT[du.Def.Id] = FellT.GetValueOrDefault(du.Def.Id) + e.Turn;
                        if (du == hane) HaneFell++;
                        break;
                }
                tr.Apply(e);
            }
        }
    }

    internal static Agg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = Seeds)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var a = new Agg();
            var (r, p, e, slot0) = DecoyDiag.Fight(f, w, sc, seed0 + i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }
}
