using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// enemyfire phase0 —— Q0-6（E0 ＝ 規定の駒で、§6 の台を数える）。**盤面は1ビットも動かさない**——台本から「敵の火勢の影」を組み直すだけ。
// 影の規則（指示書 §3.1 の写し）: 燃えていなかった敵に火が点く（`StatusGain` burn）→ 1 ／ 燃え広がりの見出し（`FireLevel` の「燃え広がり」・相手 ＝ 敵）→ +1（上限 4）
// ／ そのターンに育たなかった敵はターンの終わりに −1（1 未満にしない）／ 燃焼の刻みで残りが 0 → 0。
// 刻みの回数・脆さの上昇は「その時点の影の火勢」で名目を足し算するだけ（上限・破片・過剰殺傷を無視した上界）。
static partial class EnemyFireDiag
{
    internal static readonly int[] BrittlePctOf = { 0, 25, 40, 55, 70 };

    internal sealed class ShadowAgg
    {
        public long N, Turns, Wins;
        public readonly long[,] LvHead = new long[8, 5];      // [周回, 火勢] 周回の頭で生きている敵
        public long Grow, GrowFoes, Lit, Wilt, Out, Lv4Deaths, Lv4DeathNb, Lv4DeathNbBurning, BurnDeaths;
        public readonly long[] FirstReach3 = new long[8], FirstReach4 = new long[8];
        public long FoesReached3, FoesReached4, Foes;
        public long TickNominal, TickExtra, BrittleNow, BrittleExtraShadow, EnemyDamage;
        public long HotaDealt, BorgDealt, AllyTicks, AllyTickExtra, AllyTickExtraNoHiyo;
        public long Unleashes, UnleashThenBurn, HotaAfterUnleash, HotaAfterUnleashN, HotaHands, HotaHandDmg;
        public void Merge(ShadowAgg o)
        {
            N += o.N; Turns += o.Turns; Wins += o.Wins;
            for (int t = 0; t < 8; t++) { for (int l = 0; l < 5; l++) LvHead[t, l] += o.LvHead[t, l]; FirstReach3[t] += o.FirstReach3[t]; FirstReach4[t] += o.FirstReach4[t]; }
            Grow += o.Grow; GrowFoes += o.GrowFoes; Lit += o.Lit; Wilt += o.Wilt; Out += o.Out; Lv4Deaths += o.Lv4Deaths; Lv4DeathNb += o.Lv4DeathNb; Lv4DeathNbBurning += o.Lv4DeathNbBurning; BurnDeaths += o.BurnDeaths;
            FoesReached3 += o.FoesReached3; FoesReached4 += o.FoesReached4; Foes += o.Foes;
            TickNominal += o.TickNominal; TickExtra += o.TickExtra; BrittleNow += o.BrittleNow; BrittleExtraShadow += o.BrittleExtraShadow; EnemyDamage += o.EnemyDamage;
            HotaDealt += o.HotaDealt; BorgDealt += o.BorgDealt; AllyTicks += o.AllyTicks; AllyTickExtra += o.AllyTickExtra; AllyTickExtraNoHiyo += o.AllyTickExtraNoHiyo;
            Unleashes += o.Unleashes; UnleashThenBurn += o.UnleashThenBurn; HotaAfterUnleash += o.HotaAfterUnleash; HotaAfterUnleashN += o.HotaAfterUnleashN; HotaHands += o.HotaHands; HotaHandDmg += o.HotaHandDmg;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++; Turns += r.Turns; if (r.PlayerWon) Wins++;
            var foeSlot = e.ToDictionary(u => u.InstanceId, u => u.Slot);
            var foeIds = new HashSet<int>(e.Select(u => u.InstanceId));
            foreach (var x in r.Events)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && (x.Team ?? BattleContext.EnemyTeam) == BattleContext.EnemyTeam && !foeIds.Contains(sid) && !p.Any(u => u.InstanceId == sid))
                { foeIds.Add(sid); if (x.Slot is int ss) foeSlot[sid] = ss; }
            Foes += foeIds.Count;
            int? hota = p.FirstOrDefault(u => u.Def.Id == "hota")?.InstanceId, borg = p.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId;
            bool hiyoIn = p.Any(u => u.Def.Id == "hiyo");
            var burn = new Dictionary<int, int>(); var lv = new Dictionary<int, int>(); var grewT = new Dictionary<int, int>();
            var alive = new HashSet<int>(foeIds); var r3 = new HashSet<int>(); var r4 = new HashSet<int>(); var grownFoes = new HashSet<int>();
            int turn = 0;
            void TurnEnd(int t)
            {
                foreach (int f in alive)
                    if (lv.GetValueOrDefault(f) > 1 && grewT.GetValueOrDefault(f) != t) { lv[f]--; Wilt++; }
            }
            void Head(int t)
            {
                int ti = Math.Min(7, t);
                foreach (int f in alive) LvHead[ti, burn.GetValueOrDefault(f) > 0 ? lv.GetValueOrDefault(f) : 0]++;
            }
            foreach (var x in r.Events)
            {
                if (x.Turn != turn)
                {
                    if (turn > 0) TurnEnd(turn);
                    turn = x.Turn;
                    Head(turn);
                }
                if (x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Burn && x.TargetId is int g && foeIds.Contains(g))
                {
                    if (burn.GetValueOrDefault(g) <= 0) { lv[g] = 1; grewT[g] = 0; Lit++; }
                    burn[g] = x.Amount;
                }
                else if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Spread && x.TargetId is int s && foeIds.Contains(s) && alive.Contains(s))
                {
                    Grow++; grownFoes.Add(s);
                    if (burn.GetValueOrDefault(s) > 0)
                    {
                        lv[s] = Math.Min(4, Math.Max(1, lv.GetValueOrDefault(s)) + 1); grewT[s] = x.Turn;
                        if (lv[s] >= 3 && r3.Add(s)) FirstReach3[Math.Min(7, x.Turn)]++;
                        if (lv[s] >= 4 && r4.Add(s)) FirstReach4[Math.Min(7, x.Turn)]++;
                    }
                }
                else if (x.Kind == BattleEventKind.Status && x.Text == "燃焼" && x.TargetId is int b)
                {
                    if (foeIds.Contains(b))
                    {
                        if (x.TickIndex is null or 1)
                        {
                            int L = Math.Max(1, lv.GetValueOrDefault(b));
                            TickNominal += x.Amount; TickExtra += (long)x.Amount * (L - 1);
                            burn[b] = burn.GetValueOrDefault(b) - 1;
                            if (burn[b] <= 0) { if (lv.GetValueOrDefault(b) > 0) Out++; lv[b] = 0; }
                        }
                    }
                    else if (x.TickIndex is null or 1)
                    {
                        AllyTicks++;
                    }
                }
                else if (x.Kind == BattleEventKind.Damage && x.TargetId is int d && foeIds.Contains(d))
                {
                    EnemyDamage += x.Amount;
                    if (hota is not null && x.ActorId == hota) HotaDealt += x.Amount;
                    if (borg is not null && x.ActorId == borg) BorgDealt += x.Amount;
                    if (x.BrittleExtra is int be && be > 0)
                    {
                        BrittleNow += be;
                        int L = Math.Max(1, lv.GetValueOrDefault(d));
                        BrittleExtraShadow += (long)be * BrittlePctOf[L] / 25 - be;
                    }
                }
                else if (x.Kind == BattleEventKind.Death && x.TargetId is int k && alive.Contains(k))
                {
                    alive.Remove(k);
                    if (burn.GetValueOrDefault(k) > 0) BurnDeaths++;
                    if (burn.GetValueOrDefault(k) > 0 && lv.GetValueOrDefault(k) >= 4 && foeSlot.TryGetValue(k, out int ks))
                    {
                        Lv4Deaths++;
                        foreach (int n in alive)
                            if (foeSlot.TryGetValue(n, out int ns) && FormationRules.AreAdjacent(ks, ns))
                            { Lv4DeathNb++; if (burn.GetValueOrDefault(n) > 0) Lv4DeathNbBurning++; }
                    }
                }
            }
            FoesReached3 += r3.Count; FoesReached4 += r4.Count; GrowFoes += grownFoes.Count;
            // 追記 A の見込み: 放つ（ボルグ）の後の、ホタの最初の手番の与ダメ ／ ホタの手番の平均・放つ → 焼き尽くす の順で撃てた戦
            if (hota is int ht)
            {
                var ev = r.Events;
                var hands = r.Hands.Where(h => h.ActorId == ht).OrderBy(h => h.EventStart).ToList();
                long HandDmg(HandRecord h) { long d = 0; for (int j = h.EventStart; j < h.EventEnd && j < ev.Count; j++) if (ev[j].Kind == BattleEventKind.Damage && ev[j].ActorId == ht && ev[j].TargetId is int tt && foeIds.Contains(tt)) d += ev[j].Amount; return d; }
                foreach (var h in hands) { HotaHands++; HotaHandDmg += HandDmg(h); }
                bool ub = false;
                for (int i = 0; i < ev.Count; i++)
                {
                    if (ev[i] is not { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.Unleash }) continue;
                    Unleashes++;
                    var nx = hands.FirstOrDefault(h => h.EventStart > i);
                    if (nx.EventEnd > 0) { HotaAfterUnleash += HandDmg(nx); HotaAfterUnleashN++; }
                    for (int j = i + 1; j < ev.Count; j++) if (ev[j] is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.Burnout } b && b.ActorId == ht) { ub = true; break; }
                }
                if (ub) UnleashThenBurn++;
            }
            // 味方の刻み（E2 の見込み）: 火勢の写し（周回の頭）で燃えている味方の火勢 L → 追加の刻み L−1 回（ホタは焼かれない）。
            if (r.FireLevels is FireLevelLedger fl)
            {
                var hotaIds = new HashSet<int>(p.Where(u => u.HasTrait(TraitId.Pyre)).Select(u => u.InstanceId));
                foreach (var sn in fl.Snaps)
                    if (sn.Team == BattleContext.PlayerTeam && sn.Burning && sn.Level > 1 && !hotaIds.Contains(sn.Id))
                    {
                        AllyTickExtra += sn.Level - 1;
                        if (!hiyoIn) AllyTickExtraNoHiyo += sn.Level - 1;
                    }
            }
        }
    }

    internal static ShadowAgg MeasureShadow(Formation f, int w, EnemyScaleRule sc, int seeds = BA.Seeds)
    {
        var parts = new ShadowAgg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var (r, p, e) = FB.Fight(f, w, sc, i);
            var a = new ShadowAgg(); a.Take(r, p, e); parts[i] = a;
        });
        var all = new ShadowAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = new (string Name, Formation F)[] { ("T3-244（仮の T3）", E0(T3244)), ("T3-238", E0(T3238)), ("雷＋ボルグ", E0(ThunderBorg)) };   // 第249期 前段: E0 に固定
        Console.WriteLine("# 第245期 Phase 0 —— Q0-6（E0 ＝ 前段の規定の駒・seed 0..199・verbose・台本から敵の火勢の影を組み直す）");
        Console.WriteLine();
        foreach (var (n, f) in boards) Console.WriteLine($"- {n}: {BA.SeatsNamed(f)}");
        Console.WriteLine();
        Console.WriteLine("影の規則: 点く → 1 ／ 燃え広がり（燃えている味方の攻撃が当たる前から燃えていた敵に当たる・1回の攻撃で同じ敵は1回）→ +1（上限 4）／ 育たなかったターンの終わり −1 ／ 燃焼が切れたら 0。");
        Console.WriteLine("「刻みの上乗せ」＝ 影の火勢 L の敵の燃焼の刻みに 6 × (L − 1) を足した名目。「脆さの上乗せ」＝ 今の脆さの分（+25%）を 25/40/55/70% に置き直した名目の差。どちらも上限・破片・過剰殺傷を無視した上界。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 全員勝率 ／ 決着T | 周回の頭の燃えている敵の火勢 1 ／ 2 ／ 3 ／ 4（体/戦） T2 | T3 | T4 | 火勢3 に届いた敵 ／ 4 （/戦・初めて届いた周回 T1 ／ T2 ／ T3 ／ T4+ %） | 燃え広がり/戦（育った敵） | 火勢4 で倒れた敵/戦（そのとき隣の生きた敵 ／ うち燃えている） | 燃えて倒れた敵/戦 | 刻みの名目/戦 → 上乗せ | 脆さの分/戦 → 上乗せ | 敵への与ダメ/戦（ホタ ／ ボルグ） | 味方の刻み/戦 ・ E2 の追加の刻み/戦 | 放つ/戦 ・ 放つ→焼き尽くすの戦 % | 放つの直後のホタの手番の与ダメ ／ ホタの手番の平均 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var (n, f) in boards)
            foreach (int w in new[] { BA.MainWave, 0, 1, 2, 3 })
                foreach (int s in w == BA.MainWave ? new[] { 0, 1 } : new[] { 1 })
                {
                    var a = MeasureShadow(f, w, BA.Scales[s].Sc);
                    string H(int t) => string.Join(" ／ ", Enumerable.Range(1, 4).Select(l => Per(a.LvHead[t, l], a.N)));
                    long f3 = a.FirstReach3.Sum(), f4 = a.FirstReach4.Sum();
                    string first4 = f4 == 0 ? "—" : string.Join(" ／ ", Pct(a.FirstReach4[1], f4), Pct(a.FirstReach4[2], f4), Pct(a.FirstReach4[3], f4), Pct(a.FirstReach4.Skip(4).Sum(), f4));
                    Console.WriteLine($"| {n} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Pct(a.Wins, a.N)} ／ {Per(a.Turns, a.N)} | {H(2)} | {H(3)} | {H(4)} | {Per(a.FoesReached3, a.N)} ／ {Per(a.FoesReached4, a.N)}（{first4}） | {Per(a.Grow, a.N)}（{Per(a.GrowFoes, a.N)}） | {Per(a.Lv4Deaths, a.N)}（{Per(a.Lv4DeathNb, a.N)} ／ {Per(a.Lv4DeathNbBurning, a.N)}） | {Per(a.BurnDeaths, a.N)} | {Per(a.TickNominal, a.N)} → +{Per(a.TickExtra, a.N)} | {Per(a.BrittleNow, a.N)} → +{Per(a.BrittleExtraShadow, a.N)} | {Per(a.EnemyDamage, a.N)}（{Per(a.HotaDealt, a.N)} ／ {Per(a.BorgDealt, a.N)}） | {Per(a.AllyTicks, a.N)} ・ +{Per(a.AllyTickExtra, a.N)} | {Per(a.Unleashes, a.N)} ・ {Pct(a.UnleashThenBurn, a.N)} | {Per(a.HotaAfterUnleash, a.HotaAfterUnleashN)} ／ {Per(a.HotaHandDmg, a.HotaHands)} |");
                }
        Console.WriteLine();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }
}
