using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// fireburst phase0 —— Q0-6（S0 ＝ 規定の駒＝第242期 R3 で、§6 の台を数える）。**盤面は動かさない。**
// 予測に入れる数値: 大技の機会（ギフトの手番の頭で火勢4 のボルグ・ホタ）／ 燃え広がりの上限で捨てる育ち ／ 煽りの相手 ／ 脆さの寄与 ／ ホタとヨミの与ダメ。
static partial class FireBurstDiag
{
    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第244期 Phase 0 —— Q0-6（S0 ＝ 規定の駒・seed 0..199・verbose）");
        Console.WriteLine();
        var boards = new (string Name, Formation F)[]
        {
            ("T3-238", Apply(T3238, VerOf("S0"))), ("T3（242 R3 の1位）", Apply(T3r3, VerOf("S0"))), ("雷＋ボルグ", Apply(ThunderBorg, VerOf("S0"))), ("参考 移動", BA.RefMove),   // 第249期 前段: S0 に固定
        };
        foreach (var (bn, f) in boards) Console.WriteLine($"- {bn}: {BA.SeatsNamed(f)}");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 勝率 | 全員生存 | 決着T | 初回ギフト T1 ／ T2 ／ T3+ % | ギフトの手番/戦 ボルグ（うち火勢4）／ ホタ（うち火勢4） | 燃え広がりの育ち/戦 ／ うち上限で捨てる | ヒヨ自身の育ち（燃え広がり由来）/戦 ／ うち上限で捨てる | 同時に火勢4 T2 ／ T3 ／ T4 | 煽りの相手 ボルグ ／ ホタ ／ 相方 % | 与ダメ/戦 ボルグ ／ ホタ ／ ヨミ | 脆さの分 ÷ 与ダメ（3体 ／ 味方全体）% |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        var cases = new List<(int W, int S)> { (BA.MainWave, 0), (BA.MainWave, 1), (BA.MainWave, 2) };
        for (int w = 0; w < 4; w++) cases.Add((w, 1));
        foreach (var (bn, f) in boards)
            foreach (var (w, s) in cases)
            {
                var parts = new P0Agg[BA.Seeds];
                Parallel.For(0, BA.Seeds, seed =>
                {
                    var (r, p, e) = Fight(f, w, BA.Scales[s].Sc, seed);
                    var a = new P0Agg(); a.Take(r, p, e); parts[seed] = a;
                });
                var st = new P0Agg();
                foreach (var a in parts) st.Merge(a);
                string fg = $"{Pct(st.FirstGift[1], st.N)} ／ {Pct(st.FirstGift[2], st.N)} ／ {Pct(st.FirstGift[3], st.N)}";
                string gt = $"{Per(st.GiftTurn[0], st.N)}（{Per(st.GiftTurn4[0], st.N)}）／ {Per(st.GiftTurn[1], st.N)}（{Per(st.GiftTurn4[1], st.N)}）";
                string sp = $"{Per(st.SpreadGrow, st.N)} ／ {Per(st.SpreadLost, st.N)}";
                string sg = $"{Per(st.SelfGrow, st.N)} ／ {Per(st.SelfLost, st.N)}";
                string sim = string.Join(" ／ ", new[] { 2, 3, 4 }.Select(t => Per(st.Simul4[t], st.SimulN[t])));
                long sk = st.Stoke.Sum();
                string stk = $"{Pct(st.Stoke[0], sk)} ／ {Pct(st.Stoke[1], sk)} ／ {Pct(st.Stoke[3], sk)}";
                string dealt = $"{Per(st.Dealt[0], st.N)} ／ {Per(st.Dealt[1], st.N)} ／ {Per(st.YomiDealt, st.N)}";
                string br = $"{Pct(st.TrioBrittle, st.TrioDealt)} ／ {Pct(st.AllBrittle, st.AllDealt)}";
                Console.WriteLine($"| {bn} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Pct(st.Wins, st.N)} | {Pct(st.AllSurv, st.N)} | {Per(st.Turns, st.N)} | {fg} | {gt} | {sp} | {sg} | {sim} | {stk} | {dealt} | {br} |");
            }
        Console.WriteLine();
        Console.WriteLine("- 「上限で捨てる」＝ 1回の燃え広がりで +2 以上育った分の +1 を超える部分（§3.1 の上限を入れると消える育ち・火勢4 の頭打ちで既に捨てた分は含まない）。");
        Console.WriteLine("- 「脆さの分」＝ 敵への `Damage` の `BrittleExtra`（燃焼の脆さ F1 で足された名目・破片や軛で削られる前）の和 ÷ 敵への `Damage` の量の和。");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    sealed class P0Agg
    {
        public long N, Wins, AllSurv, Turns, SpreadGrow, SpreadLost, SelfGrow, SelfLost, YomiDealt, TrioBrittle, TrioDealt, AllBrittle, AllDealt;
        public readonly long[] FirstGift = new long[4], GiftTurn = new long[4], GiftTurn4 = new long[4], Stoke = new long[4], Dealt = new long[4];
        public readonly long[] Simul4 = new long[8], SimulN = new long[8];
        public void Merge(P0Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; Turns += o.Turns; SpreadGrow += o.SpreadGrow; SpreadLost += o.SpreadLost; SelfGrow += o.SelfGrow; SelfLost += o.SelfLost;
            YomiDealt += o.YomiDealt; TrioBrittle += o.TrioBrittle; TrioDealt += o.TrioDealt; AllBrittle += o.AllBrittle; AllDealt += o.AllDealt;
            for (int i = 0; i < 4; i++) { FirstGift[i] += o.FirstGift[i]; GiftTurn[i] += o.GiftTurn[i]; GiftTurn4[i] += o.GiftTurn4[i]; Stoke[i] += o.Stoke[i]; Dealt[i] += o.Dealt[i]; }
            for (int i = 0; i < 8; i++) { Simul4[i] += o.Simul4[i]; SimulN[i] += o.SimulN[i]; }
        }
        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++; Turns += r.Turns;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var info = new Dictionary<int, (string Id, int Team)>();
            foreach (var u in p.Concat(e)) info[u.InstanceId] = (u.Def.Id, u.TeamId);
            if (r.FireLevels is FireLevelLedger fl)
            {
                if (fl.FirstGiftTurn > 0) FirstGift[Math.Min(3, fl.FirstGiftTurn)]++;
                var simul = new long[8]; var seen = new bool[8];
                foreach (var s in fl.Snaps)
                {
                    if (s.Team != BattleContext.PlayerTeam || s.Turn >= 8) continue;
                    seen[s.Turn] = true; if (s.Level >= 4) simul[s.Turn]++;
                }
                for (int t = 0; t < 8; t++) if (seen[t]) { Simul4[t] += simul[t]; SimulN[t]++; }
            }
            foreach (var x in r.Events)
            {
                if (x.Kind == BattleEventKind.FireLevel && x.TargetId is int tg && info.TryGetValue(tg, out var ti))
                {
                    if (x.Text == FireLevelLabels.GiftTurn) { int ro = RoleOf(ti.Id); GiftTurn[ro]++; if (x.Amount >= 4) GiftTurn4[ro]++; }
                    if (x.Text == FireLevelLabels.Stoke) Stoke[RoleOf(ti.Id)]++;
                    if (x.Text == FireLevelLabels.GrowSpread) { int g = x.Amount - x.Slot; SpreadGrow += g; SpreadLost += Math.Max(0, g - 1); }
                    if (x.Text == FireLevelLabels.GrowSelf && x.ActorId != x.TargetId) { int g = x.Amount - x.Slot; SelfGrow += g; SelfLost += Math.Max(0, g - 1); }
                }
                if (x.Kind == BattleEventKind.Damage && x.ActorId is int a && info.TryGetValue(a, out var ai) && ai.Team == BattleContext.PlayerTeam
                    && x.TargetId is int d && info.TryGetValue(d, out var di) && di.Team != BattleContext.PlayerTeam && !x.Relayed)
                {
                    int ro = RoleOf(ai.Id);
                    AllDealt += x.Amount; AllBrittle += x.BrittleExtra ?? 0;
                    if (ro < 3) { Dealt[ro] += x.Amount; TrioDealt += x.Amount; TrioBrittle += x.BrittleExtra ?? 0; }
                    if (ai.Id == "yomi") YomiDealt += x.Amount;
                }
            }
        }
    }
}
