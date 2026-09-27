using BattleCore;
using static Common;

// gale —— 版（§5）と帳簿（§8.2）。
static partial class GaleDiag
{
    // =================================================================================
    // 版（§5）。バサ・ハネの札の差し替え ＋ 転倒の穴の規則（`ShufflerRule.StaggerHole`）。今の札は消さない。
    //   G0 前段の規定 ／ G1 嵐（バサに `Gale`）／ G2 追い風（バサ・ハネに `Tailwind`）／ G3 転倒の穴 ／ G4 全部
    // =================================================================================

    internal sealed record Ver(string Tag, UnitDef Basa, UnitDef Hane, bool Hole)
    {
        public ShufflerRule Rule => ShufflerRule.Default with { StaggerHole = Hole };
    }

    static UnitDef Plus(UnitDef d, params TraitId[] extra) => DecoyDiag.Copy(d, d.Traits.Concat(extra).ToArray());

    internal static readonly Ver[] Versions =
    {
        new("G0", UnitCatalog.BasaG0, UnitCatalog.HaneG0, false),
        new("G1", Plus(UnitCatalog.BasaG0, TraitId.Gale), UnitCatalog.HaneG0, false),
        new("G2", Plus(UnitCatalog.BasaG0, TraitId.Tailwind), Plus(UnitCatalog.HaneG0, TraitId.Tailwind), false),
        new("G3", UnitCatalog.BasaG0, UnitCatalog.HaneG0, true),
        new("G4", Plus(UnitCatalog.BasaG0, TraitId.Gale, TraitId.Tailwind), Plus(UnitCatalog.HaneG0, TraitId.Tailwind), true),
    };
    internal static Ver VerOf(string tag) => Versions.First(v => v.Tag == tag);

    /// <summary>編成のバサ・ハネだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied()) g[slot] = d.Id == "basa" ? v.Basa : d.Id == "hane" ? v.Hane : d;
        return g;
    }

    // =================================================================================
    // 席（§8.1）: G4 × 九/新兵 × 200/200 × seed 1000..1199・120 通り（全員生存 → 落ちた駒 → 決着T）。
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
                var r = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(perms[i]), BattleContext.PlayerTeam), WaveOf(MainWave, sc)(), s, verbose: false, shuffler: v.Rule);
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

    /// <summary>味方が動かされた出どころ（表B）。</summary>
    internal static readonly string[] MoveSources = { "バサの入れ替え", "追い風", "ハネ（勢い余って）", "シオ", "セロ", "敵", "その他" };

    internal static readonly string[] TallyKeys =
    {
        "GaleStirs", "GaleAllyMoved", "TailwindTriggers", "TailwindFromShuffle", "TailwindFromBlast", "TailwindFromSpring", "TailwindFromOther",
        "TailwindSteps", "TailwindNoPair", "TailwindLowHp", "TailwindAllLow", "TailwindRefused", "TailwindNested",
        "ShuffleAllySwaps", "ShuffleFoeSwaps", "ShuffleConfuses", "GustFell",
    };

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns, FellTotal;
        public readonly Dictionary<string, long> Fell = new(), FellT = new();
        public readonly long[] AllyMoves = new long[MoveSources.Length];
        public long AllyForward;
        public readonly Dictionary<string, long> T = new();                // 味方（バサ・ハネ）の帳簿
        public long StepYomi, StepSero, StepShio;                            // 追い風で前へ出た
        public long StepRetreated;                                           // 追い風で前へ出たのが、同じターンに緊急退避で下げた駒（P6）
        public long HoleBreachP, HoleBreachE, HoleSkipP, HoleSkipE;          // 転倒の穴（P ＝ 味方の側 ／ E ＝ 敵の側）
        public long StaggerFoe;                                              // 敵に付いた転倒
        public long ConfusedStrikes;                                         // 混乱した敵の同士討ち
        // 段に届いたターン（届いた戦の数 ／ ターンの和）: シオ段1〜3・セロ段1〜3・バサの敵の乱れ段1〜3・ヨミの初めての薙ぎ
        public readonly long[] ShioN = new long[4], ShioT = new long[4], SeroN = new long[4], SeroT = new long[4], BasaN = new long[4], BasaT = new long[4];
        public long YomiSweepN, YomiSweepT;

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns; FellTotal += o.FellTotal;
            foreach (var (k, v) in o.Fell) Fell[k] = Fell.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.FellT) FellT[k] = FellT.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.T) T[k] = T.GetValueOrDefault(k) + v;
            for (int i = 0; i < AllyMoves.Length; i++) AllyMoves[i] += o.AllyMoves[i];
            AllyForward += o.AllyForward;
            StepYomi += o.StepYomi; StepSero += o.StepSero; StepShio += o.StepShio; StepRetreated += o.StepRetreated;
            HoleBreachP += o.HoleBreachP; HoleBreachE += o.HoleBreachE; HoleSkipP += o.HoleSkipP; HoleSkipE += o.HoleSkipE;
            StaggerFoe += o.StaggerFoe; ConfusedStrikes += o.ConfusedStrikes;
            for (int i = 0; i < 4; i++) { ShioN[i] += o.ShioN[i]; ShioT[i] += o.ShioT[i]; SeroN[i] += o.SeroN[i]; SeroT[i] += o.SeroT[i]; BasaN[i] += o.BasaN[i]; BasaT[i] += o.BasaT[i]; }
            YomiSweepN += o.YomiSweepN; YomiSweepT += o.YomiSweepT;
        }
        public double Per(long x) => (double)x / Math.Max(1, N);
        public double PerK(string k) => Per(T.GetValueOrDefault(k));
        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;
        public static string StageT(long[] n, long[] t, int st, long battles)
            => n[st] == 0 ? "—" : $"{100.0 * n[st] / Math.Max(1, battles):F0}% T{(double)t[st] / n[st]:F1}";

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            N++; FellTotal += r.PlayerStarterFallen.Count;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var pIds = p.Select(u => u.Def.Id).ToHashSet();
            foreach (var (id, t) in r.TallyByUnit)
            {
                bool mine = pIds.Contains(id);
                if (mine && id is "basa" or "hane")
                    foreach (var k in TallyKeys) T[k] = T.GetValueOrDefault(k) + Tally(t, k);
                if (mine) { HoleBreachP += t.HoleBreaches; HoleSkipP += t.HoleSkips; }
                else { HoleBreachE += t.HoleBreaches; HoleSkipE += t.HoleSkips; }
            }
            var byId = p.Concat(e).ToDictionary(u => u.InstanceId);
            var players = p.Select(u => u.InstanceId).ToHashSet();
            var b = new Board(p.Concat(e), slot0);
            var tailPending = new List<(int Unit, int? Actor)>();   // 追い風の入れ替えでまだ Move が出ていない2体
            bool yomiSwept = false;
            var shioSeen = new bool[4]; var seroSeen = new bool[4]; var basaSeen = new bool[4];
            var retreatTurn = new Dictionary<int, int>();
            foreach (BattleEvent ev in r.Events)
            {
                switch (ev.Kind)
                {
                    case BattleEventKind.Retreat when ev.TargetId is int rt:
                        retreatTurn[rt] = ev.Turn; break;
                    case BattleEventKind.Tailwind:
                        if (ev.TargetId is int rs && retreatTurn.TryGetValue(rs, out int rtn) && rtn == ev.Turn) StepRetreated++;
                        if (ev.TargetId is int t1) tailPending.Add((t1, ev.ActorId));
                        if (ev.PartnerId is int t2) tailPending.Add((t2, ev.ActorId));
                        if (ev.TargetId is int st && byId.TryGetValue(st, out var su))
                        {
                            if (su.Def.Id == "yomi") StepYomi++; else if (su.Def.Id == "sero") StepSero++; else if (su.Def.Id == "shio") StepShio++;
                        }
                        break;
                    case BattleEventKind.Move when ev.TargetId is int m && players.Contains(m):
                        {
                            string? aid = ev.ActorId is int a && byId.TryGetValue(a, out var au) ? au.Def.Id : null;
                            bool foeAct = ev.ActorId is int a2 && byId.TryGetValue(a2, out var au2) && au2.TeamId != BattleContext.PlayerTeam;
                            int ti = tailPending.FindIndex(x => x.Unit == m && x.Actor == ev.ActorId);
                            int src = ti >= 0 ? 1 : foeAct ? 5 : aid == "basa" ? 0 : aid == "hane" ? 2 : aid == "shio" ? 3 : aid == "sero" ? 4 : 6;
                            if (ti >= 0) tailPending.RemoveAt(ti);
                            AllyMoves[src]++;
                            if (b.Slot.ContainsKey(m) && FormationRules.DepthOf(FormationRules.RowOf(ev.Slot)) < FormationRules.DepthOf(FormationRules.RowOf(b.Slot[m]))) AllyForward++;
                            break;
                        }
                    case BattleEventKind.Stagger when ev.Text == StaggerLabels.Fell && ev.TargetId is int sf && !players.Contains(sf):
                        StaggerFoe++; break;
                    case BattleEventKind.Confused when ev.Text == ConfusedLabels.Struck:
                        ConfusedStrikes++; break;
                    case BattleEventKind.ShioStage when ev.Slot is >= 1 and <= 3 && !shioSeen[ev.Slot]:
                        shioSeen[ev.Slot] = true; ShioN[ev.Slot]++; ShioT[ev.Slot] += ev.Turn; break;
                    case BattleEventKind.EvadeStage when ev.Slot is >= 1 and <= 3 && !seroSeen[ev.Slot]:
                        seroSeen[ev.Slot] = true; SeroN[ev.Slot]++; SeroT[ev.Slot] += ev.Turn; break;
                    case BattleEventKind.DisarrayStage when ev.ActorId is int da && byId.TryGetValue(da, out var du) && du.Def.Id == "basa"
                                                          && ev.Slot is >= 1 and <= 3 && !basaSeen[ev.Slot]:
                        basaSeen[ev.Slot] = true; BasaN[ev.Slot]++; BasaT[ev.Slot] += ev.Turn; break;
                    case BattleEventKind.Attack when !yomiSwept && ev.Pattern == AttackPattern.Sweep && ev.ActorId is int ya && byId.TryGetValue(ya, out var yu) && yu.Def.Id == "yomi":
                        yomiSwept = true; YomiSweepN++; YomiSweepT += ev.Turn; break;
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
            var (r, p, e, slot0) = Fight(g, w, sc, seed0 + i, shuffler: v.Rule);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }
}
