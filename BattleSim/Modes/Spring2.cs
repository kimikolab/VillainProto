using BattleCore;
using static Common;

// =====================================================================================
// spring2 —— 第232期「ハネの弾き返しを入れ替わらない形で測り直す」。
// 指示書は design/PHASE232_SPRING2_SPEC.md ／ 報告は design/PHASE232_SPRING2.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 spring2 phase0   # Q0-1 取り合い ／ Q0-2 弾けなかった内訳（S0・S1 だけで回る）
//     dotnet run --project BattleSim -c Release 0 spring2 run      # 表A〜C
//     dotnet run --project BattleSim -c Release 0 spring2 check    # 自己検査
// =====================================================================================
static partial class Spring2Diag
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
                Console.WriteLine("spring2: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    internal const int Seeds = 200;

    /// <summary>第228期 H3 の1位の席（前1 バサ ／ 前3 セロ ／ 中央 ヨミ ／ 後1 シオ ／ 後3 ハネ）。駒は前段の規定（シオ 退避5割・セロ 移動の追撃）。</summary>
    internal static Formation MHane228 => Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Sero,
        center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Hane);
    internal static Formation Thunder => GaleDiag.Thunder;

    internal static readonly string[] WaveNames = TuneDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => TuneDiag.WaveOf(w, sc);
    /// <summary>敵の倍率（§5）: 200/200（主判定）・400/300（負荷試験）・115/115。</summary>
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales =
    {
        ("200/200", new EnemyScaleRule(200, 200)),
        ("400/300", new EnemyScaleRule(400, 300)),
        ("115/115", new EnemyScaleRule(115, 115)),
    };
    internal static string SeatsNamed(Formation f) => GaleDiag.SeatsNamed(f);
    internal static string F1(double x) => GaleDiag.F1(x);
    internal static string F2(double x) => GaleDiag.F2(x);

    // 版（§2）。札の差し替えだけ。
    internal sealed record Ver(string Tag, UnitDef Hane);
    static UnitDef Plus(UnitDef d, params TraitId[] extra) => DecoyDiag.Copy(d, d.Traits.Concat(extra).ToArray());
    internal static readonly UnitDef HaneS1 = Plus(UnitCatalog.Hane, TraitId.SpringGuard);
    internal static readonly UnitDef HaneS2 = Plus(UnitCatalog.Hane, TraitId.SpringGuard, TraitId.SpringStay);
    internal static Ver[] Versions => _versions ??= new[] { new Ver("S0", UnitCatalog.Hane), new Ver("S1", HaneS1), new Ver("S2", HaneS2) };
    static Ver[]? _versions;
    internal static Ver VerOf(string tag) => Versions.First(v => v.Tag == tag);

    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied()) g[slot] = d.Id == "hane" ? v.Hane : d;
        return g;
    }

    internal static (BattleResult R, List<UnitState> P, List<UnitState> E, Dictionary<int, int> Slot0) Fight(
        Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = WaveOf(w, sc)();
        var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        return (r, p, e, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
    }

    /// <summary>表の1セル（第231期の `TuneDiag.Agg` ＋ 取り合いと弾けなかった内訳）。</summary>
    internal sealed class Agg
    {
        public readonly TuneDiag.Agg T = new();
        // Q0-1 取り合い: 隣の弾き返し1回ごとに、その被弾の後（次の Damage / Attack / TurnStart まで）に緊急退避が
        // 殴られた味方を下げた（両方）／ 別の駒を下げた ／ 殴られた味方が5割未満なのに下げなかった ／ 5割以上で退避は要らなかった
        public long GuardBoth, GuardOther, GuardSkipped, GuardNoNeed;
        // S0: ハネの隣の味方が殴られて緊急退避が起きた回数（S1 なら取り合いになりうる被弾）
        public long AdjRetreat;
        // Q0-2 弾けなかった内訳（隣の分）
        public long GChances, GFires, GOffLane, GTail, GCapped, GHushed, GHeld, GStay;

        public void Merge(Agg o)
        {
            T.Merge(o.T);
            GuardBoth += o.GuardBoth; GuardOther += o.GuardOther; GuardSkipped += o.GuardSkipped; GuardNoNeed += o.GuardNoNeed; AdjRetreat += o.AdjRetreat;
            GChances += o.GChances; GFires += o.GFires; GOffLane += o.GOffLane; GTail += o.GTail; GCapped += o.GCapped; GHushed += o.GHushed; GHeld += o.GHeld; GStay += o.GStay;
        }
        public double Per(long x) => T.Per(x);

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            T.Take(r, p, e, slot0);
            foreach (var (id, t) in r.TallyByUnit)
            {
                if (id != "hane" || !p.Any(u => u.Def.Id == "hane")) continue;
                GChances += t.SpringGuardChances; GFires += t.SpringGuardFires; GOffLane += t.SpringGuardOffLane; GTail += t.SpringGuardTail;
                GCapped += t.SpringGuardCapped; GHushed += t.SpringGuardHushed; GHeld += t.SpringGuardHeld; GStay += t.SpringGuardStays;
            }
            var players = p.Select(u => u.InstanceId).ToHashSet();
            int hane = p.FirstOrDefault(u => u.Def.Id == "hane")?.InstanceId ?? -1;
            var b = new GaleDiag.Board(p.Concat(e), slot0);
            var evs = r.Events;
            for (int i = 0; i < evs.Count; i++)
            {
                var ev = evs[i];
                if (ev.Kind == BattleEventKind.SpringGuard && ev.TargetId is int ally)
                {
                    bool need = b.Hp[ally] * 100 < b.MaxHp[ally] * RetreatTrait.HalfPercent;
                    int kind = 0;   // 0 なし ／ 1 同じ味方 ／ 2 別の駒
                    for (int j = i + 1; j < evs.Count; j++)
                    {
                        var x = evs[j];
                        if (x.Kind is BattleEventKind.Damage or BattleEventKind.Attack or BattleEventKind.TurnStart) break;
                        if (x.Kind == BattleEventKind.Retreat) { kind = x.TargetId == ally ? 1 : 2; break; }
                    }
                    if (kind == 1) GuardBoth++; else if (kind == 2) GuardOther++; else if (need) GuardSkipped++; else GuardNoNeed++;
                }
                if (ev.Kind == BattleEventKind.Damage && ev.TargetId is int dt && players.Contains(dt) && dt != hane && hane >= 0
                    && ev.ActorId is int da && !players.Contains(da) && ev.HpAfter > 0 && b.Alive.Contains(hane)
                    && FormationRules.AreAdjacent(b.Slot[hane], b.Slot[dt]))
                {
                    for (int j = i + 1; j < evs.Count; j++)
                    {
                        var x = evs[j];
                        if (x.Kind is BattleEventKind.Damage or BattleEventKind.Attack or BattleEventKind.TurnStart) break;
                        if (x.Kind == BattleEventKind.Retreat && x.TargetId == dt) { AdjRetreat++; break; }
                    }
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
