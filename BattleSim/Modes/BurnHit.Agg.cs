using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;

// burnhit の集計（1戦を1回だけ回して全部の表に流す）。
static partial class BurnHitDiag
{
    /// <summary>死因（受け入れ 4）。最後に HP を削った出来事で決める。</summary>
    internal static readonly string[] Causes = { "敵の攻撃", "味方の一撃", "燃焼の刻み", "被弾の燃焼", "毒の刻み", "その他" };
    internal const int CFoe = 0, CAlly = 1, CTick = 2, CHit = 3, CPoison = 4, COther = 5;

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, Turns, Fell;
        public readonly long[] Fires = new long[2], Units = new long[2], HitDmg = new long[2], HitHeal = new long[2];
        public readonly long[] TickFires = new long[2], TickDmg = new long[2], TickHeal = new long[2], Kills = new long[2];
        public readonly long[] Repeats = new long[2], Skipped = new long[2], GateOff = new long[2], Chances = new long[2], ChanceLv = new long[2];
        public readonly long[] PerScope = new long[11];
        public readonly Dictionary<string, long[]> ByActor = new(), ByTarget = new(), ChanceBy = new();
        public readonly Dictionary<string, long> FellBy = new();
        public readonly long[] Cause = new long[Causes.Length];
        public long DeathEv, FellRes;
        /// <summary>台本の指紋（表G・受け入れ 1）: 戦ごとの FNV を seed 順に足し合わせる。</summary>
        public ulong Sig;
        /// <summary>的の波（8 ターンまで）: 敵に入った燃焼（刻み ／ 被弾）の HP。</summary>
        public long T8Tick, T8Hit, T8Total;

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; Turns += o.Turns; Fell += o.Fell;
            for (int i = 0; i < 2; i++)
            {
                Fires[i] += o.Fires[i]; Units[i] += o.Units[i]; HitDmg[i] += o.HitDmg[i]; HitHeal[i] += o.HitHeal[i];
                TickFires[i] += o.TickFires[i]; TickDmg[i] += o.TickDmg[i]; TickHeal[i] += o.TickHeal[i]; Kills[i] += o.Kills[i];
                Repeats[i] += o.Repeats[i]; Skipped[i] += o.Skipped[i]; GateOff[i] += o.GateOff[i]; Chances[i] += o.Chances[i]; ChanceLv[i] += o.ChanceLv[i];
            }
            for (int i = 0; i < PerScope.Length; i++) PerScope[i] += o.PerScope[i];
            Add(ByActor, o.ByActor); Add(ByTarget, o.ByTarget); Add(ChanceBy, o.ChanceBy);
            foreach (var (k, v) in o.FellBy) FellBy[k] = FellBy.GetValueOrDefault(k) + v;
            for (int i = 0; i < Cause.Length; i++) Cause[i] += o.Cause[i];
            DeathEv += o.DeathEv; FellRes += o.FellRes; Sig = unchecked(Sig * 1099511628211UL + o.Sig);
            T8Tick += o.T8Tick; T8Hit += o.T8Hit; T8Total += o.T8Total;
        }
        static void Add(Dictionary<string, long[]> a, Dictionary<string, long[]> b)
        {
            foreach (var (k, v) in b)
            {
                if (!a.TryGetValue(k, out var x)) a[k] = x = new long[v.Length];
                for (int i = 0; i < v.Length; i++) x[i] += v[i];
            }
        }
        static void Add(Dictionary<string, long[]> a, IReadOnlyDictionary<string, long[]> b)
        {
            foreach (var (k, v) in b)
            {
                if (!a.TryGetValue(k, out var x)) a[k] = x = new long[v.Length];
                for (int i = 0; i < v.Length; i++) x[i] += v[i];
            }
        }

        public void Take(BattleResult r, List<UnitState> p, int w)
        {
            N++; Turns += r.Turns;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            Fell += r.PlayerStarterFallen.Count; FellRes += r.PlayerStarterFallen.Count;
            foreach (var u in p) if (!u.IsAlive) FellBy[u.Def.Id] = FellBy.GetValueOrDefault(u.Def.Id) + 1;
            if (r.BurnHit is BurnHitLedger b)
            {
                for (int i = 0; i < 2; i++)
                {
                    Fires[i] += b.Fires[i]; Units[i] += b.Units[i]; HitDmg[i] += b.HitDmg[i]; HitHeal[i] += b.HitHeal[i];
                    TickFires[i] += b.TickFires[i]; TickDmg[i] += b.TickDmg[i]; TickHeal[i] += b.TickHeal[i]; Kills[i] += b.Kills[i];
                    Repeats[i] += b.Repeats[i]; Skipped[i] += b.Skipped[i]; GateOff[i] += b.GateOff[i]; Chances[i] += b.Chances[i]; ChanceLv[i] += b.ChanceLv[i];
                }
                for (int i = 0; i < PerScope.Length; i++) PerScope[i] += b.PerScope[i];
                Add(ByActor, b.ByActor); Add(ByTarget, b.ByTarget); Add(ChanceBy, b.ChanceBy);
            }

            // 死因（最後に HP を削った出来事）・台本の指紋
            var starters = p.Select(u => u.InstanceId).ToHashSet();
            var last = new Dictionary<int, int>();
            var lastTick = new Dictionary<int, (string Text, bool Hit)>();
            ulong h = 14695981039346656037UL;
            void Mix(long x) { unchecked { h ^= (ulong)x; h *= 1099511628211UL; } }
            Mix(r.PlayerWon ? 1 : 0); Mix(r.Turns);
            bool tgt = IsTarget(w);
            foreach (var e in r.Events)
            {
                Mix((long)e.Kind); Mix(e.Turn); Mix(e.ActorId ?? -1); Mix(e.TargetId ?? -1); Mix(e.Amount); Mix(e.HpAfter);
                if (e.Kind == BattleEventKind.Status && e.TargetId is int st && (e.Text == "燃焼" || e.Text == "毒"))
                    lastTick[st] = (e.Text!, e.ActorId is not null);
                if (e.Kind == BattleEventKind.Damage && e.TargetId is int dt)
                {
                    if (tgt && e.Turn <= FC.TargetTurns && !starters.Contains(dt))
                    {
                        T8Total += e.Amount;
                        if (e.ActorId is null && lastTick.TryGetValue(dt, out var lt) && lt.Text == "燃焼") { if (lt.Hit) T8Hit += e.Amount; else T8Tick += e.Amount; }
                    }
                    if (starters.Contains(dt))
                    {
                        int c;
                        if (e.ActorId is null)
                            c = lastTick.TryGetValue(dt, out var lt) ? (lt.Text == "毒" ? CPoison : lt.Hit ? CHit : CTick) : COther;
                        else c = starters.Contains(e.ActorId.Value) ? CAlly : CFoe;
                        last[dt] = c;
                    }
                    lastTick.Remove(dt);
                }
                if (e.Kind == BattleEventKind.Death && e.TargetId is int dd && starters.Contains(dd))
                {
                    DeathEv++;
                    Cause[last.TryGetValue(dd, out var c2) ? c2 : COther]++;
                }
            }
            Sig = h;
        }
    }

    /// <summary>1つのセル（台 × 版 × 波 × 倍率）を seed 0..199 で回す。</summary>
    internal static Agg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var xs = new Agg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var (r, p, _) = FC.Fight(f, w, sc, seed0 + i, verbose: true);
            var a = new Agg(); a.Take(r, p, w); xs[i] = a;
        });
        var m = new Agg();
        for (int i = 0; i < seeds; i++) m.Merge(xs[i]);
        return m;
    }
}
