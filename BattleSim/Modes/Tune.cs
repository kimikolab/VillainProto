using BattleCore;
using static Common;

// =====================================================================================
// tune —— 第231期「400/300 で見えた穴：緊急退避の線・ハネの弾き返しの範囲・セロの移動の追撃」。
// 指示書は design/PHASE231_TUNE_SPEC.md ／ 報告は design/PHASE231_TUNE.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 tune phase0   # Q0-1〜Q0-4（V0 ＝ 前段の規定だけで回る）
//     dotnet run --project BattleSim -c Release 0 tune run      # 表A〜D
//     dotnet run --project BattleSim -c Release 0 tune check    # 自己検査（受け入れ 2〜4・7）
// =====================================================================================
static partial class TuneDiag
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
                Console.WriteLine("tune: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    internal const int Seeds = 200;

    /// <summary>前段の規定の1位の席（第228期 H3 の1位の席・前1 バサ ／ 前3 セロ ／ 中央 ヨミ ／ 後1 シオ ／ 後3 ハネ）。駒は今の規定。</summary>
    internal static Formation MHane228 => Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.SeroC0,
        center: UnitCatalog.Yomi, back1: UnitCatalog.ShioA0, back3: UnitCatalog.Hane);
    /// <summary>参考: ポンの席（前1 シガ ／ 前3 ツギ ／ 中央 ベニ ／ 後1 カタ ／ 後3 ミオ）。</summary>
    internal static Formation Thunder => GaleDiag.Thunder;

    /// <summary>波（§8.1）: 本編の第2〜5波・九/新兵・九/農兵（`DriftDiag` と同じ並び）。</summary>
    internal static readonly string[] WaveNames = DriftDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => DriftDiag.WaveOf(w, sc);
    internal const int MainWave = 4;
    /// <summary>敵の倍率（§8.1）: 400/300（ポンが遊んだ設定）・200/200（これまでの主判定）・115/115。</summary>
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales =
    {
        ("400/300", new EnemyScaleRule(400, 300)),
        ("200/200", new EnemyScaleRule(200, 200)),
        ("115/115", new EnemyScaleRule(115, 115)),
    };
    internal static string SeatsNamed(Formation f) => GaleDiag.SeatsNamed(f);
    internal static string F1(double x) => GaleDiag.F1(x);
    internal static string F2(double x) => GaleDiag.F2(x);

    // =================================================================================
    // 版（§5）。札の差し替えだけ（`Run` の引数は増やさない）。挑発の表示は全版に入っている（盤面は動かない）。
    // =================================================================================
    internal sealed record Ver(string Tag, UnitDef Shio, UnitDef Hane, UnitDef Sero);

    static UnitDef Plus(UnitDef d, params TraitId[] extra) => DecoyDiag.Copy(d, d.Traits.Concat(extra).ToArray());

    static readonly UnitDef ShioHalf = Plus(UnitCatalog.ShioA0, TraitId.RetreatHalf), ShioHeavy = Plus(UnitCatalog.ShioA0, TraitId.RetreatHeavy);
    static readonly UnitDef HaneGuard = Plus(UnitCatalog.Hane, TraitId.SpringGuard), SeroShot = Plus(UnitCatalog.SeroC0, TraitId.EvadeMoveShot);

    internal static readonly Ver[] Versions =
    {
        new("V0", UnitCatalog.ShioA0, UnitCatalog.Hane, UnitCatalog.SeroC0),
        new("VA1", ShioHalf, UnitCatalog.Hane, UnitCatalog.SeroC0),
        new("VA2", ShioHeavy, UnitCatalog.Hane, UnitCatalog.SeroC0),
        new("VB", UnitCatalog.ShioA0, HaneGuard, UnitCatalog.SeroC0),
        new("VC", UnitCatalog.ShioA0, UnitCatalog.Hane, SeroShot),
        new("VX1", ShioHalf, HaneGuard, SeroShot),
        new("VX2", ShioHeavy, HaneGuard, SeroShot),
    };
    internal static Ver VerOf(string tag) => Versions.First(v => v.Tag == tag);

    /// <summary>編成のシオ・ハネ・セロだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied())
            g[slot] = d.Id switch { "shio" => v.Shio, "hane" => v.Hane, "sero" => v.Sero, _ => d };
        return g;
    }

    /// <summary>1戦を回す（台本つき・今の既定の規則）。</summary>
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E, Dictionary<int, int> Slot0) Fight(
        Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = WaveOf(w, sc)();
        var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
        return (r, p, e, slot0);
    }

    // 席（§8.1）: VX2 × 九/新兵 × 400/300 × seed 1000..1199・120 通り（全員生存 → 落ちた駒 → 勝ち → 決着T）。
    internal const int PickSeed0 = 1000, PickSeeds = 200;

    internal static List<(Formation F, int W, int Sv, int Fell, long T)> PickSeats(Formation raw, Ver v, EnemyScaleRule sc)
    {
        var members = Apply(raw, v).Occupied().Select(o => o.Def).ToList();
        var perms = SeroDiag.Permute(members).Select(p => Formation.Build(front1: p[0], front3: p[1], center: p[2], back1: p[3], back3: p[4])).ToList();
        var res = new (Formation F, int W, int Sv, int Fell, long T)[perms.Count];
        Parallel.For(0, perms.Count, i =>
        {
            int w = 0, sv = 0, fell = 0; long t = 0;
            for (int s = PickSeed0; s < PickSeed0 + PickSeeds; s++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(perms[i], BattleContext.PlayerTeam), WaveOf(MainWave, sc)(), s, verbose: false);
                fell += r.PlayerStarterFallen.Count;
                if (!r.PlayerWon) continue;
                w++; t += r.Turns; if (r.PlayerStarterFallen.Count == 0) sv++;
            }
            res[i] = (perms[i], w, sv, fell, t);
        });
        return res.Select((x, i) => (x, i)).OrderByDescending(z => z.x.Sv).ThenBy(z => z.x.Fell).ThenByDescending(z => z.x.W)
            .ThenBy(z => z.x.W == 0 ? long.MaxValue : z.x.T * 1000 / z.x.W).ThenBy(z => z.i).Select(z => z.x).ToList();
    }

    internal static readonly string[] Units = { "basa", "sero", "yomi", "shio", "hane" };
    internal static readonly string[] UnitNames = { "バサ", "セロ", "ヨミ", "シオ", "ハネ" };

    /// <summary>倒れた一撃の直前の HP の割合の区間（%）。</summary>
    internal static readonly string[] PreBins = { "<40", "40-50", "50-60", "60-80", "80+" };
    internal static int PreBin(int hp, int max)
    {
        int pc = hp * 100 / Math.Max(1, max);
        return pc < 40 ? 0 : pc < 50 ? 1 : pc < 60 ? 2 : pc < 80 ? 3 : 4;
    }

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns, TurnsAll;
        public readonly Dictionary<string, long> Fell = new(), FellT = new();
        // 表B 緊急退避
        public long RetreatSwaps, RetreatHeavy, RetreatChances, RetreatHeavyChances, RetreatSpent, RetreatNoPartner, RetreatHushed;
        public readonly long[] DeathPre = new long[5];
        public long HeavyHits, EnemyHits, HitsInto4050;
        // 表C ハネ
        public long SpringSelf, SpringGuard, GuardSwaps, GuardRefused, GuardChances, SpringSwapsSelf, SpringConfused, SpringCapped, HaneFrontTurns, HaneAliveTurns, AdjHits, HaneHits;
        // 表D セロ
        public long MoveShots, MoveCapped, MoveChances, MoveSelfSwap, MoveHushed, SeroDealt, Riposte, Barrage, MoveDealt, DecoyOnTurns, SeroAliveTurns;
        public readonly long[] MoveByTurn = new long[7];

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns; TurnsAll += o.TurnsAll;
            foreach (var (k, v) in o.Fell) Fell[k] = Fell.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.FellT) FellT[k] = FellT.GetValueOrDefault(k) + v;
            RetreatSwaps += o.RetreatSwaps; RetreatHeavy += o.RetreatHeavy; RetreatChances += o.RetreatChances; RetreatHeavyChances += o.RetreatHeavyChances;
            RetreatSpent += o.RetreatSpent; RetreatNoPartner += o.RetreatNoPartner; RetreatHushed += o.RetreatHushed;
            for (int i = 0; i < 5; i++) DeathPre[i] += o.DeathPre[i];
            HeavyHits += o.HeavyHits; EnemyHits += o.EnemyHits; HitsInto4050 += o.HitsInto4050;
            SpringSelf += o.SpringSelf; SpringGuard += o.SpringGuard; GuardSwaps += o.GuardSwaps; GuardRefused += o.GuardRefused; GuardChances += o.GuardChances;
            SpringSwapsSelf += o.SpringSwapsSelf; SpringConfused += o.SpringConfused; SpringCapped += o.SpringCapped;
            HaneFrontTurns += o.HaneFrontTurns; HaneAliveTurns += o.HaneAliveTurns; AdjHits += o.AdjHits; HaneHits += o.HaneHits;
            MoveShots += o.MoveShots; MoveCapped += o.MoveCapped; MoveChances += o.MoveChances; MoveSelfSwap += o.MoveSelfSwap; MoveHushed += o.MoveHushed;
            SeroDealt += o.SeroDealt; Riposte += o.Riposte; Barrage += o.Barrage; MoveDealt += o.MoveDealt; DecoyOnTurns += o.DecoyOnTurns; SeroAliveTurns += o.SeroAliveTurns;
            for (int i = 0; i < 7; i++) MoveByTurn[i] += o.MoveByTurn[i];
        }
        public double Per(long x) => (double)x / Math.Max(1, N);
        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            N++; TurnsAll += r.Turns;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var pIds = p.Select(u => u.Def.Id).ToHashSet();
            foreach (var (id, t) in r.TallyByUnit)
            {
                if (!pIds.Contains(id)) continue;
                if (id == "shio")
                {
                    RetreatSwaps += t.RetreatSwaps; RetreatHeavy += t.RetreatHeavyOnly; RetreatChances += t.RetreatChances; RetreatHeavyChances += t.RetreatHeavyChances;
                    RetreatSpent += t.RetreatSpent; RetreatNoPartner += t.RetreatNoPartner; RetreatHushed += t.RetreatHushed;
                }
                if (id == "hane")
                {
                    SpringSelf += t.SpringCount - t.SpringGuardCount; SpringGuard += t.SpringGuardCount; GuardSwaps += t.SpringGuardSwaps; GuardRefused += t.SpringGuardRefused;
                    GuardChances += t.SpringGuardChances; SpringSwapsSelf += t.SpringSwaps; SpringConfused += t.SpringConfused; SpringCapped += t.SpringCapped;
                }
                if (id == "sero")
                {
                    MoveShots += t.MoveShots; MoveCapped += t.MoveShotCapped; MoveChances += t.MoveShotChances; MoveSelfSwap += t.MoveShotSelfSwap; MoveHushed += t.MoveShotHushed;
                    SeroDealt += t.DamageToEnemy; Riposte += t.EvRiposteDealt; Barrage += t.EvBarrageDealt; MoveDealt += t.EvMoveShotDealt;
                    if (t.MoveShotByTurn is { } mb) for (int i = 0; i < 7; i++) MoveByTurn[i] += mb[i];
                }
            }
            var byId = p.Concat(e).ToDictionary(u => u.InstanceId);
            var players = p.Select(u => u.InstanceId).ToHashSet();
            int hane = p.FirstOrDefault(u => u.Def.Id == "hane")?.InstanceId ?? -1;
            int sero = p.FirstOrDefault(u => u.Def.Id == "sero")?.InstanceId ?? -1;
            bool decoyOn = false;
            var b = new GaleDiag.Board(p.Concat(e), slot0);
            foreach (BattleEvent ev in r.Events)
            {
                switch (ev.Kind)
                {
                    case BattleEventKind.TurnStart:
                        if (hane >= 0 && b.Alive.Contains(hane)) { HaneAliveTurns++; if (b.RowOfUnit(hane) == Row.Front) HaneFrontTurns++; }
                        if (sero >= 0 && b.Alive.Contains(sero)) { SeroAliveTurns++; if (decoyOn) DecoyOnTurns++; }
                        break;
                    case BattleEventKind.DecoyShow when ev.ActorId == sero:
                        decoyOn = ev.Slot == 1; break;
                    case BattleEventKind.Damage when ev.TargetId is int dt && players.Contains(dt) && ev.ActorId is int da && !players.Contains(da) && ev.Amount > 0:
                        {
                            int before = b.Hp[dt], max = b.MaxHp[dt];
                            EnemyHits++;
                            if (ev.Amount * 100 >= max * RetreatTrait.HeavyPercent) HeavyHits++;
                            if (ev.HpAfter > 0)
                            {
                                int pc = ev.HpAfter * 100 / Math.Max(1, max);
                                if (pc >= 40 && pc < 50) HitsInto4050++;
                                if (dt == hane) HaneHits++;
                                else if (hane >= 0 && b.Alive.Contains(hane) && FormationRules.AreAdjacent(b.Slot[hane], b.Slot[dt])) AdjHits++;
                            }
                            else DeathPre[PreBin(before, max)]++;
                            break;
                        }
                    case BattleEventKind.Death when ev.TargetId is int d && players.Contains(d) && byId.TryGetValue(d, out var dd):
                        Fell[dd.Def.Id] = Fell.GetValueOrDefault(dd.Def.Id) + 1;
                        FellT[dd.Def.Id] = FellT.GetValueOrDefault(dd.Def.Id) + ev.Turn;
                        break;
                }
                b.Apply(ev);
            }
        }
    }

    internal static Agg Measure(Formation f, Ver v, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = Seeds)
    {
        var parts = new Agg[seeds];
        var g = Apply(f, v);
        Parallel.For(0, seeds, i =>
        {
            var a = new Agg();
            var (r, p, e, slot0) = Fight(g, w, sc, seed0 + i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }
}
