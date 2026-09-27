using BattleCore;
using static Common;

// cycle —— 版（§5）と帳簿（§8.2）。
static partial class CycleDiag
{
    // =================================================================================
    // 版（§5）。札の差し替えだけ（`Run` の引数は増やさない）。今の札は消さない。
    //   W0 前段の規定 ／ W1 追い風を攻撃力順（バサ・ハネに `TailwindFighter`）／ W2 ＋ ヨミの撃破の衝撃（`KillImpact`）／
    //   W3 ＋ シオの溢れ（`DriftSurge`）／ W4 全部
    // =================================================================================

    internal sealed record Ver(string Tag, UnitDef Basa, UnitDef Hane, UnitDef Yomi, UnitDef Shio);

    static UnitDef Plus(UnitDef d, params TraitId[] extra) => DecoyDiag.Copy(d, d.Traits.Concat(extra).ToArray());

    static readonly UnitDef BasaF = Plus(UnitCatalog.BasaW0, TraitId.TailwindFighter), HaneF = Plus(UnitCatalog.HaneW0, TraitId.TailwindFighter);
    static readonly UnitDef YomiK = Plus(UnitCatalog.YomiW0, TraitId.KillImpact), ShioS = Plus(UnitCatalog.ShioW0, TraitId.DriftSurge);

    internal static readonly Ver[] Versions =
    {
        new("W0", UnitCatalog.BasaW0, UnitCatalog.HaneW0, UnitCatalog.YomiW0, UnitCatalog.ShioW0),
        new("W1", BasaF, HaneF, UnitCatalog.YomiW0, UnitCatalog.ShioW0),
        new("W2", BasaF, HaneF, YomiK, UnitCatalog.ShioW0),
        new("W3", BasaF, HaneF, UnitCatalog.YomiW0, ShioS),
        new("W4", BasaF, HaneF, YomiK, ShioS),
        // 第230期の追記: W4 ＋ ヨミの吹き飛ばしでも追い風（`ImpactTailwind`）＝ 第230期の追記の規定（`UnitCatalog` の今の4枚と同じ札）
        new("W5", BasaF, HaneF, Plus(YomiK, TraitId.ImpactTailwind), ShioS),
    };
    internal static Ver VerOf(string tag) => Versions.First(v => v.Tag == tag);

    /// <summary>編成のバサ・ハネ・ヨミ・シオだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied())
            g[slot] = d.Id switch { "basa" => v.Basa, "hane" => v.Hane, "yomi" => v.Yomi, "shio" => v.Shio, _ => d };
        return g;
    }

    // 席（§8.1）: W4 × 九/新兵 × 200/200 × seed 1000..1199・120 通り（全員生存 → 落ちた駒 → 決着T）。
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

    internal static readonly string[] Units = { "basa", "sero", "yomi", "shio", "hane" };
    internal static readonly string[] UnitNames = { "バサ", "セロ", "ヨミ", "シオ", "ハネ" };

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns, FellTotal, TurnsAll;
        public readonly Dictionary<string, long> Fell = new(), FellT = new();
        // 表B 追い風
        public long TailImpact, TailEvents, YomiFwd, YomiFwdTw, ShioFwd, ShioFwdTw, AllyMoves;
        public readonly Dictionary<string, long> StepBy = new();
        // 表C 撃破の衝撃（ヨミの帳簿）
        public long Kills, Blow, Stumble, Tumble, Capped, NoAlly, Outside, Refused, ImpactMoves, CreakSwings, AllKills;
        public readonly long[] BlowT = new long[7], TumbleT = new long[7];
        // 表D 溢れ
        public readonly Dictionary<string, long> Over = new(), Gain = new(), CapHit = new();
        // 表E 火力
        public readonly Dictionary<string, long> Dealt = new();
        public readonly Dictionary<string, long[]> AtkSum = new(), AtkN = new();

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns; FellTotal += o.FellTotal; TurnsAll += o.TurnsAll;
            foreach (var (k, v) in o.Fell) Fell[k] = Fell.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.FellT) FellT[k] = FellT.GetValueOrDefault(k) + v;
            TailImpact += o.TailImpact; TailEvents += o.TailEvents; YomiFwd += o.YomiFwd; YomiFwdTw += o.YomiFwdTw; ShioFwd += o.ShioFwd; ShioFwdTw += o.ShioFwdTw; AllyMoves += o.AllyMoves;
            foreach (var (k, v) in o.StepBy) StepBy[k] = StepBy.GetValueOrDefault(k) + v;
            Kills += o.Kills; Blow += o.Blow; Stumble += o.Stumble; Tumble += o.Tumble; Capped += o.Capped; NoAlly += o.NoAlly; Outside += o.Outside;
            Refused += o.Refused; ImpactMoves += o.ImpactMoves; CreakSwings += o.CreakSwings; AllKills += o.AllKills;
            for (int i = 0; i < 7; i++) { BlowT[i] += o.BlowT[i]; TumbleT[i] += o.TumbleT[i]; }
            foreach (var (k, v) in o.Over) Over[k] = Over.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Gain) Gain[k] = Gain.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.CapHit) CapHit[k] = CapHit.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Dealt) Dealt[k] = Dealt.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.AtkSum) { var a = AtkSum.TryGetValue(k, out var x) ? x : AtkSum[k] = new long[7]; for (int i = 0; i < 7; i++) a[i] += v[i]; }
            foreach (var (k, v) in o.AtkN) { var a = AtkN.TryGetValue(k, out var x) ? x : AtkN[k] = new long[7]; for (int i = 0; i < 7; i++) a[i] += v[i]; }
        }
        public double Per(long x) => (double)x / Math.Max(1, N);
        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;
        public double AtkAt(string id, int t) => AtkN.TryGetValue(id, out var n) && n[t] > 0 ? (double)AtkSum[id][t] / n[t] : double.NaN;

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            N++; FellTotal += r.PlayerStarterFallen.Count; TurnsAll += r.Turns;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var pIds = p.Select(u => u.Def.Id).ToHashSet();
            foreach (var (id, t) in r.TallyByUnit)
            {
                if (!pIds.Contains(id)) continue;
                if (id == "yomi")
                {
                    Kills += t.ImpactKills; Blow += t.ImpactBlow; Stumble += t.ImpactStumble; Tumble += t.ImpactTumble; Capped += t.ImpactCapped;
                    NoAlly += t.ImpactNoAlly; Outside += t.ImpactOutside; Refused += t.ImpactRefused; CreakSwings += t.CreakSwings;
                    if (t.ImpactBlowByTurn is { } bt) for (int i = 0; i < 7; i++) BlowT[i] += bt[i];
                    if (t.ImpactTumbleByTurn is { } tt) for (int i = 0; i < 7; i++) TumbleT[i] += tt[i];
                }
                if (id is "basa" or "hane") TailImpact += t.TailwindFromImpact;
                Over[id] = Over.GetValueOrDefault(id) + t.ShioOverflowRecv;
                Gain[id] = Gain.GetValueOrDefault(id) + t.ShioOverflowGain;
                if (t.ShioOverflowGain >= DriftSurgeTrait.CapPerUnit) CapHit[id] = CapHit.GetValueOrDefault(id) + 1;
            }
            var byId = p.Concat(e).ToDictionary(u => u.InstanceId);
            var players = p.Select(u => u.InstanceId).ToHashSet();
            int yomi = p.FirstOrDefault(u => u.Def.Id == "yomi")?.InstanceId ?? -1;
            var b = new GaleDiag.Board(p.Concat(e), slot0);
            var tailPending = new List<(int Unit, int? Actor)>();
            foreach (BattleEvent ev in r.Events)
            {
                switch (ev.Kind)
                {
                    case BattleEventKind.Tailwind:
                        TailEvents++;
                        if (ev.TargetId is int t1) { tailPending.Add((t1, ev.ActorId)); if (byId.TryGetValue(t1, out var su)) StepBy[su.Def.Id] = StepBy.GetValueOrDefault(su.Def.Id) + 1; }
                        if (ev.PartnerId is int t2) tailPending.Add((t2, ev.ActorId));
                        break;
                    case BattleEventKind.Move when ev.TargetId is int m:
                        {
                            if (ev.ActorId == yomi && yomi >= 0) ImpactMoves++;
                            if (!players.Contains(m)) break;
                            AllyMoves++;
                            int ti = tailPending.FindIndex(x => x.Unit == m && x.Actor == ev.ActorId);
                            if (ti >= 0) tailPending.RemoveAt(ti);
                            bool fwd = b.Slot.ContainsKey(m) && FormationRules.DepthOf(FormationRules.RowOf(ev.Slot)) < FormationRules.DepthOf(FormationRules.RowOf(b.Slot[m]));
                            if (fwd && byId[m].Def.Id == "yomi") { YomiFwd++; if (ti >= 0) YomiFwdTw++; }
                            if (fwd && byId[m].Def.Id == "shio") { ShioFwd++; if (ti >= 0) ShioFwdTw++; }
                            break;
                        }
                    case BattleEventKind.Damage when ev.ActorId is int da && players.Contains(da) && ev.TargetId is int dt && !players.Contains(dt):
                        {
                            string id = byId[da].Def.Id;
                            Dealt[id] = Dealt.GetValueOrDefault(id) + ev.Amount;
                            break;
                        }
                    case BattleEventKind.StatSnapshot when ev.TargetId is int su2 && players.Contains(su2) && ev.Turn is >= 1 and <= 6:
                        {
                            string id = byId[su2].Def.Id;
                            var s = AtkSum.TryGetValue(id, out var x) ? x : AtkSum[id] = new long[7];
                            var n = AtkN.TryGetValue(id, out var y) ? y : AtkN[id] = new long[7];
                            s[ev.Turn] += ev.Amount; n[ev.Turn]++;
                            break;
                        }
                    case BattleEventKind.Death when ev.TargetId is int d && players.Contains(d) && byId.TryGetValue(d, out var dd):
                        Fell[dd.Def.Id] = Fell.GetValueOrDefault(dd.Def.Id) + 1;
                        FellT[dd.Def.Id] = FellT.GetValueOrDefault(dd.Def.Id) + ev.Turn;
                        break;
                    case BattleEventKind.Death when ev.TargetId is int d2 && !players.Contains(d2) && ev.ActorId == yomi && yomi >= 0:
                        AllKills++; break;
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
