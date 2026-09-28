using BattleCore;
using static Common;

// tune phase0 —— Q0-1〜Q0-4（§6）。V0 ＝ 前段の規定だけで回る（版の札は1つも読まない）。
static partial class TuneDiag
{
    sealed class P0Agg
    {
        public long N, Wins, AllSurv, EnemyHits, Heavy, Into4050, Deaths, TurnsAll;
        public readonly long[] Pre = new long[5];
        public long OneShotFrom40, OneShotFrom50;                    // 倒れた一撃の直前が 4割以上 ／ 5割以上
        public long HaneAlive, HaneFront, HaneHits, AdjHits, AdjHitsHaneFront;
        public long SeroStage2Turns, MovesAfter2, MovesAfter2Self, TurnsWith3Plus, OverCap;
        public readonly Dictionary<string, long> MoveSrc = new();
        public readonly long[] MovesByTurn = new long[7];
        public long DecoyOn, SeroAlive, DecoyEvents;

        public void Merge(P0Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; EnemyHits += o.EnemyHits; Heavy += o.Heavy; Into4050 += o.Into4050; Deaths += o.Deaths; TurnsAll += o.TurnsAll;
            for (int i = 0; i < 5; i++) Pre[i] += o.Pre[i];
            OneShotFrom40 += o.OneShotFrom40; OneShotFrom50 += o.OneShotFrom50;
            HaneAlive += o.HaneAlive; HaneFront += o.HaneFront; HaneHits += o.HaneHits; AdjHits += o.AdjHits; AdjHitsHaneFront += o.AdjHitsHaneFront;
            SeroStage2Turns += o.SeroStage2Turns; MovesAfter2 += o.MovesAfter2; MovesAfter2Self += o.MovesAfter2Self; TurnsWith3Plus += o.TurnsWith3Plus; OverCap += o.OverCap;
            foreach (var (k, v) in o.MoveSrc) MoveSrc[k] = MoveSrc.GetValueOrDefault(k) + v;
            for (int i = 0; i < 7; i++) MovesByTurn[i] += o.MovesByTurn[i];
            DecoyOn += o.DecoyOn; SeroAlive += o.SeroAlive; DecoyEvents += o.DecoyEvents;
        }
        public double Per(long x) => (double)x / Math.Max(1, N);

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            N++; TurnsAll += r.Turns;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var byId = p.Concat(e).ToDictionary(u => u.InstanceId);
            var players = p.Select(u => u.InstanceId).ToHashSet();
            int hane = p.FirstOrDefault(u => u.Def.Id == "hane")?.InstanceId ?? -1;
            UnitState? seroU = p.FirstOrDefault(u => u.Def.Id == "sero");
            int sero = seroU?.InstanceId ?? -1;
            int[] stageAt = seroU is null ? new[] { 99, 99, 99 } : (seroU.Def.Traits.Contains(TraitId.EvadeQuick) ? EvadeTrait.QuickStageAt : EvadeTrait.StageAt);
            int seroMoves = 0, turn = 0, movesThisTurn = 0;
            bool decoyOn = false;
            var b = new GaleDiag.Board(p.Concat(e), slot0);
            void CloseTurn()
            {
                if (movesThisTurn >= 3) TurnsWith3Plus++;
                if (movesThisTurn > EvadeMoveShotTrait.PerTurn) OverCap += movesThisTurn - EvadeMoveShotTrait.PerTurn;
                movesThisTurn = 0;
            }
            foreach (BattleEvent ev in r.Events)
            {
                switch (ev.Kind)
                {
                    case BattleEventKind.TurnStart:
                        CloseTurn(); turn = ev.Turn;
                        if (hane >= 0 && b.Alive.Contains(hane)) { HaneAlive++; if (b.RowOfUnit(hane) == Row.Front) HaneFront++; }
                        if (sero >= 0 && b.Alive.Contains(sero))
                        {
                            SeroAlive++; if (decoyOn) DecoyOn++;
                            if (seroMoves >= stageAt[1]) SeroStage2Turns++;
                        }
                        break;
                    case BattleEventKind.DecoyShow when ev.ActorId == sero:
                        DecoyEvents++; decoyOn = ev.Slot == 1; break;
                    case BattleEventKind.Move when ev.TargetId == sero && sero >= 0:
                        {
                            bool stage2 = seroMoves >= stageAt[1];
                            seroMoves++;
                            if (!stage2) break;
                            if (ev.ActorId == sero) { MovesAfter2Self++; break; }
                            MovesAfter2++; movesThisTurn++;
                            MovesByTurn[Math.Clamp(turn, 0, 6)]++;
                            string src = ev.ActorId is int a && byId.TryGetValue(a, out var au) ? (au.TeamId == BattleContext.PlayerTeam ? au.Def.Id : "敵") : "—";
                            MoveSrc[src] = MoveSrc.GetValueOrDefault(src) + 1;
                            break;
                        }
                    case BattleEventKind.Damage when ev.TargetId is int dt && players.Contains(dt) && ev.ActorId is int da && !players.Contains(da) && ev.Amount > 0:
                        {
                            int before = b.Hp[dt], max = b.MaxHp[dt];
                            EnemyHits++;
                            if (ev.Amount * 100 >= max * RetreatTrait.HeavyPercent) Heavy++;
                            if (ev.HpAfter <= 0)
                            {
                                Deaths++; Pre[PreBin(before, max)]++;
                                if (before * 100 >= max * 40) OneShotFrom40++;
                                if (before * 100 >= max * 50) OneShotFrom50++;
                            }
                            else
                            {
                                int pc = ev.HpAfter * 100 / Math.Max(1, max);
                                if (pc >= 40 && pc < 50) Into4050++;
                                if (dt == hane) HaneHits++;
                                else if (hane >= 0 && b.Alive.Contains(hane) && FormationRules.AreAdjacent(b.Slot[hane], b.Slot[dt]))
                                {
                                    AdjHits++;
                                    if (b.RowOfUnit(hane) == Row.Front) AdjHitsHaneFront++;
                                }
                            }
                            break;
                        }
                }
                b.Apply(ev);
            }
            CloseTurn();
        }
    }

    static P0Agg P0Measure(Formation f, int w, EnemyScaleRule sc)
    {
        var parts = new P0Agg[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var a = new P0Agg();
            var (r, p, e, slot0) = Fight(f, w, sc, i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new P0Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Formation f = MHane228;
        Console.WriteLine("# 第231期 Phase 0（V0 ＝ 前段の規定・台 M-ハネ（228 H3）・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"席: {SeatsNamed(f)}");
        Console.WriteLine();
        var res = new Dictionary<(int, int), P0Agg>();
        for (int s = 0; s < 2; s++) for (int w = 0; w < WaveNames.Length; w++) res[(s, w)] = P0Measure(f, w, Scales[s].Sc);

        Console.WriteLine("## Q0-1 退避の取りこぼし（味方が敵の一撃で倒れたときの、その一撃の直前の HP の割合）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | 勝率 | 全員生存 | 倒れた/戦 | <40 | 40-50 | 50-60 | 60-80 | 80+ | 4割以上から一撃 | 5割以上から一撃 | 一撃3割以上の被弾 | 4〜5割に落ちた被弾/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        for (int s = 0; s < 2; s++) for (int w = 0; w < WaveNames.Length; w++)
            {
                var a = res[(s, w)];
                string pc(long x) => a.Deaths == 0 ? "—" : F1(100.0 * x / a.Deaths) + "%";
                Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {F1(100.0 * a.Wins / a.N)} | {F1(100.0 * a.AllSurv / a.N)} | {F2(a.Per(a.Deaths))} | "
                    + string.Join(" | ", a.Pre.Select(pc)) + $" | {pc(a.OneShotFrom40)} | {pc(a.OneShotFrom50)} | "
                    + $"{(a.EnemyHits == 0 ? "—" : F1(100.0 * a.Heavy / a.EnemyHits) + "%")}（{F2(a.Per(a.Heavy))}/戦）| {F2(a.Per(a.Into4050))} |");
            }
        Console.WriteLine();
        Console.WriteLine("- 区間は倒れた一撃の**直前**の HP ÷ 最大HP。「4割以上から一撃」は今の線（4割）を一度も踏まずに倒れた割合（分母 ＝ 敵の一撃で倒れた味方）。");
        Console.WriteLine("- 「一撃3割以上」は敵の一撃（HP に届いたもの）のうち最大HPの 3割以上を削ったものの割合。「4〜5割に落ちた被弾」は A1 で新しく退避の対象になる被弾の数。");
        Console.WriteLine();

        Console.WriteLine("## Q0-2 ハネの位置（ターン頭に前列にいた割合）と、ハネ ／ ハネの隣の味方が殴られた回数（敵の一撃・HP に届いて生き残ったもの）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | ハネが前列にいたターン | ハネが殴られた/戦 | 隣の味方が殴られた/戦 | うちハネが前列 | 比（隣 ÷ ハネ） |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int s = 0; s < 2; s++) for (int w = 0; w < WaveNames.Length; w++)
            {
                var a = res[(s, w)];
                Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {(a.HaneAlive == 0 ? "—" : F1(100.0 * a.HaneFront / a.HaneAlive) + "%")} | {F2(a.Per(a.HaneHits))} | {F2(a.Per(a.AdjHits))} | {F2(a.Per(a.AdjHitsHaneFront))} | "
                    + $"{(a.HaneHits == 0 ? "—" : F2((double)a.AdjHits / a.HaneHits))} |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-3 セロの移動（段2 に届いた後に動かされた回数・自分の回避の入れ替えを除く）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | 段2 以上のターン/戦 | 動かされた/戦 | 自分の回避の入れ替え/戦 | 1ターン3回以上のターン/戦 | 2回の上限を超えた分/戦 | T1 | T2 | T3 | T4 | T5 | T6+ | 出どころ |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        for (int s = 0; s < 2; s++) for (int w = 0; w < WaveNames.Length; w++)
            {
                var a = res[(s, w)];
                long all = Math.Max(1, a.MovesAfter2);
                string src = string.Join(" ", a.MoveSrc.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {F1(100.0 * kv.Value / all)}%"));
                Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {F2(a.Per(a.SeroStage2Turns))} | {F2(a.Per(a.MovesAfter2))} | {F2(a.Per(a.MovesAfter2Self))} | {F2(a.Per(a.TurnsWith3Plus))} | {F2(a.Per(a.OverCap))} | "
                    + string.Join(" | ", Enumerable.Range(1, 6).Select(t => F2(a.Per(a.MovesByTurn[t])))) + $" | {src} |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-4 挑発の表示（ターン頭に「効いている」だったセロの割合・判定は盤面の挑発と同じ `DecoyTrait.Eligible`）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | 効いていたターン | 切り替わりの出来事/戦 |");
        Console.WriteLine("|---|---|---|---|");
        for (int s = 0; s < 2; s++) for (int w = 0; w < WaveNames.Length; w++)
            {
                var a = res[(s, w)];
                Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {(a.SeroAlive == 0 ? "—" : F1(100.0 * a.DecoyOn / a.SeroAlive) + "%")} | {F2(a.Per(a.DecoyEvents))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
