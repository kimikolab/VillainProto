using BattleCore;
using static Common;

// =====================================================================================
// lastdodge —— 第227期「逃亡兵セロの必死の逃げ足と回避盾の測り直し」。
// 指示書は design/PHASE227_LASTDODGE_SPEC.md ／ 報告は design/PHASE227_LASTDODGE.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 lastdodge phase0   # Q0-1 / Q0-2（L0 だけで回る）
//     dotnet run --project BattleSim -c Release 0 lastdodge run      # 表A〜D
//     dotnet run --project BattleSim -c Release 0 lastdodge check    # 自己検査（受け入れ 2〜4・7）
// =====================================================================================
static partial class LastDodgeDiag
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
                Console.WriteLine("lastdodge: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    internal const int Seeds = 200;

    // =================================================================================
    // 版（§3）。**札の差し替え**だけ。名前で引くのは Phase 0 のコミット（札が無い）でも回るように。
    //   L0 前段の規定 ／ L1 必死の逃げ足（`LastDodge`）／ L2 ＋ 回避盾（`Decoy`）／ L3 回避盾だけ（第226期 K4 と同じ）
    // バサ・ハネ・シオは規定のまま（前段でバサ・ハネが敵の乱れ＋突風になった）。
    // =================================================================================

    static TraitId? T(string name) => Enum.TryParse<TraitId>(name, out var t) ? t : null;
    static UnitDef? With(UnitDef d, params string[] names)
    {
        var extra = names.Select(T).ToList();
        if (extra.Any(x => x is null)) return null;
        return DecoyDiag.Copy(d, d.Traits.Concat(extra.Select(x => x!.Value)).ToArray());
    }

    internal sealed record Ver(string Tag, UnitDef Sero);

    internal static Ver[] Versions
    {
        get
        {
            UnitDef s0 = UnitCatalog.SeroL0;
            UnitDef? l1 = With(s0, "LastDodge"), l2 = With(s0, "LastDodge", "Decoy"), l3 = With(s0, "Decoy");
            var list = new List<Ver> { new("L0", s0) };
            if (l1 is not null) list.Add(new("L1", l1));
            if (l2 is not null) list.Add(new("L2", l2));
            if (l3 is not null) list.Add(new("L3", l3));
            return list.ToArray();
        }
    }
    internal static Ver VerOf(string tag) => Versions.First(v => v.Tag == tag);

    /// <summary>編成のセロだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied()) g[slot] = d.Id == "sero" ? v.Sero : d;
        return g;
    }

    // 台（§6.1）
    internal static Formation MHane225 => Formation.Build(front1: UnitCatalog.HaneH0, front3: UnitCatalog.Basa,
        center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.SeroL0);
    internal static Formation Thunder => DecoyDiag.Thunder;

    internal static readonly string[] WaveNames = DriftDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => DriftDiag.WaveOf(w, sc);
    internal const int MainWave = 4;
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales = DecoyDiag.Scales;
    internal static string SeatsNamed(Formation f) => DriftDiag.SeatsNamed(f);
    internal static string F1(double x) => DriftDiag.F1(x);
    internal static string F2(double x) => DriftDiag.F2(x);

    internal static bool SeroFront(Formation f) => f.Occupied().Any(o => o.Def.Id == "sero" && FormationRules.RowOf(o.Slot) == Row.Front);

    // =================================================================================
    // 席（§4）: 版 × 九/新兵 × 200/200 × seed 1000..1199・120 通り。
    // 全員生存 → 落ちた駒の数（少ない方）→ 決着T（勝った戦・短い方）→ 列挙順。
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
                var r = BattleEngine.Run(BattleEngine.Materialize(perms[i], BattleContext.PlayerTeam), WaveOf(MainWave, sc)(), s, verbose: false);
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

    /// <summary>倒れた一撃の種類（Q0-1・Q0-2）。</summary>
    internal static readonly string[] CauseNames = { "単体", "薙ぎ", "貫き", "全体", "反撃・割り込み", "状態異常", "味方", "ほか" };

    internal static int CauseOf(BattleEvent dmg, HashSet<int> enemyIds)
    {
        if (dmg.ActorId is not int a) return 5;
        if (!enemyIds.Contains(a)) return 6;
        if (dmg.Reaction) return 4;
        return dmg.Pattern switch
        {
            AttackPattern.Single => 0, AttackPattern.Sweep => 1, AttackPattern.Pierce => 2, AttackPattern.All => 3, _ => 7,
        };
    }

    /// <summary>後の期に足した帳簿の欄（名前で引く・Phase 0 のコミットでも回るように）。</summary>
    static long Tally(UnitTally t, string name)
        => typeof(UnitTally).GetField(name)?.GetValue(t) is { } v ? Convert.ToInt64(v) : typeof(UnitTally).GetProperty(name)?.GetValue(t) is { } w ? Convert.ToInt64(w) : 0;

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns, FellTotal;
        public readonly Dictionary<string, long> Fell = new(), FellT = new();
        public readonly Dictionary<string, long[]> FellCause = new();
        // セロ
        public long SeroFell, SeroKillHigh;                     // 倒れた一撃の直前の HP が 4 割以上
        public readonly long[] SeroCause = new long[8];
        public long SeroTurns, SeroFront, Drew, Stolen, EvRolls, Evades, Ripostes, Lowered, SeroDealt;
        public long Dodges, DodgeBattles, RefellAfterDodge, LoweredAfterDodge, DodgeSpent;
        public readonly long[] RefellCause = new long[8];
        public readonly long[] DodgeByStage = new long[4];
        public long RefellSpent;                                 // また倒れた戦のうち、その前に回数切れの致命打があった

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns; FellTotal += o.FellTotal;
            foreach (var (k, v) in o.Fell) Fell[k] = Fell.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.FellT) FellT[k] = FellT.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.FellCause) { if (!FellCause.TryGetValue(k, out var a)) FellCause[k] = a = new long[8]; for (int i = 0; i < 8; i++) a[i] += v[i]; }
            SeroFell += o.SeroFell; SeroKillHigh += o.SeroKillHigh;
            for (int i = 0; i < 8; i++) { SeroCause[i] += o.SeroCause[i]; RefellCause[i] += o.RefellCause[i]; }
            for (int i = 0; i < 4; i++) DodgeByStage[i] += o.DodgeByStage[i];
            SeroTurns += o.SeroTurns; SeroFront += o.SeroFront; Drew += o.Drew; Stolen += o.Stolen; EvRolls += o.EvRolls; Evades += o.Evades;
            Ripostes += o.Ripostes; Lowered += o.Lowered; SeroDealt += o.SeroDealt;
            Dodges += o.Dodges; DodgeBattles += o.DodgeBattles; RefellAfterDodge += o.RefellAfterDodge; LoweredAfterDodge += o.LoweredAfterDodge;
            DodgeSpent += o.DodgeSpent; RefellSpent += o.RefellSpent;
        }
        public double Per(long x) => (double)x / Math.Max(1, N);
        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;

        public void Take(BattleResult r, List<UnitState> player, List<UnitState> enemy, Dictionary<int, int> slot0)
        {
            N++; FellTotal += r.PlayerStarterFallen.Count;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var byId = player.ToDictionary(u => u.InstanceId);
            var enemyIds = enemy.Select(u => u.InstanceId).ToHashSet();
            UnitState? sero = player.FirstOrDefault(u => u.Def.Id == "sero");
            bool spent = false;
            if (sero is not null && r.TallyByUnit.TryGetValue("sero", out var t))
            {
                Drew += t.DecoyDrew; Stolen += t.DecoyStolen; EvRolls += t.EvRolls; Evades += t.Evades; Ripostes += t.EvRipostes;
                Lowered += t.RetreatLowered; SeroDealt += t.DamageToEnemy;
                long ld = Tally(t, "LastDodges");
                Dodges += ld; DodgeSpent += Tally(t, "LastDodgeSpent");
                if (ld > 0) DodgeBattles++;
                spent = Tally(t, "LastDodgeSpent") > 0;
            }

            var tr = new DecoyDiag.Tracker(slot0, player);
            var lastDmg = new Dictionary<int, BattleEvent>();
            int seroHp = sero?.MaxHp ?? 0, seroHpBefore = seroHp;
            bool dodged = false;
            foreach (BattleEvent e in r.Events)
            {
                switch (e.Kind)
                {
                    case BattleEventKind.TurnStart:
                        if (sero is not null && tr.Alive.Contains(sero.InstanceId))
                        {
                            SeroTurns++;
                            if (tr.Pool(true).Contains(sero.InstanceId)) SeroFront++;
                        }
                        break;
                    case BattleEventKind.Damage when e.TargetId is int dt:
                        lastDmg[dt] = e;
                        if (sero is not null && dt == sero.InstanceId) { seroHpBefore = seroHp; seroHp = e.HpAfter; }
                        break;
                    case BattleEventKind.Heal when sero is not null && e.TargetId == sero.InstanceId:
                        seroHp = e.HpAfter;
                        break;
                    case var k when k.ToString() == "LastDodge" && sero is not null && e.ActorId == sero.InstanceId:
                        dodged = true;
                        DodgeByStage[Math.Clamp(e.Amount, 0, 3)]++;
                        break;
                    case BattleEventKind.Retreat when sero is not null && e.TargetId == sero.InstanceId && dodged:
                        LoweredAfterDodge++;
                        break;
                    case BattleEventKind.Death when e.TargetId is int d && byId.TryGetValue(d, out var du):
                    {
                        Fell[du.Def.Id] = Fell.GetValueOrDefault(du.Def.Id) + 1;
                        FellT[du.Def.Id] = FellT.GetValueOrDefault(du.Def.Id) + e.Turn;
                        int c = lastDmg.TryGetValue(d, out var kd) ? CauseOf(kd, enemyIds) : 7;
                        if (!FellCause.TryGetValue(du.Def.Id, out var arr)) FellCause[du.Def.Id] = arr = new long[8];
                        arr[c]++;
                        if (du == sero)
                        {
                            SeroFell++; SeroCause[c]++;
                            if (seroHpBefore * 100 >= sero.MaxHp * RetreatTrait.Percent) SeroKillHigh++;
                            if (dodged) { RefellAfterDodge++; RefellCause[c]++; if (spent) RefellSpent++; }
                        }
                        break;
                    }
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
