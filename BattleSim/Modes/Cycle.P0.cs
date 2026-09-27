using BattleCore;
using static Common;

// cycle phase0 —— Q0-1〜Q0-6（§6）。W0 ＝ 前段の規定だけで回る（盤面を動かす札は1つも読まない）。
static partial class CycleDiag
{
    static readonly string[] Ids = { "basa", "sero", "yomi", "shio", "hane" };

    internal sealed class P0Agg
    {
        public long N, AllSurv, Wins;
        // Q0-1 追い風で踏み込んだ駒（今の規則）と、攻撃力順にしたときの見込み（台本の盤面 ＋ ターン頭の攻撃力で引き直す）
        public readonly Dictionary<string, long> StepNow = new(), StepAtk = new();
        public long TailEvents, SameAsNow;
        // Q0-2 ヨミの撃破（ターン別: 1..5 と 6+）× 後ろに敵がいた ／ いない ／ 経路外
        public readonly long[] KillBehind = new long[7], KillNone = new long[7], KillOff = new long[7];
        // Q0-4 溢れ（受け手別）
        public readonly Dictionary<string, long> Over = new(), Half = new(), OverEv = new(), Capped = new();
        public long DrifterNominal, DrifterGained, TendNominal, TendGained;

        public void Merge(P0Agg o)
        {
            N += o.N; AllSurv += o.AllSurv; Wins += o.Wins; TailEvents += o.TailEvents; SameAsNow += o.SameAsNow;
            foreach (var (k, v) in o.StepNow) StepNow[k] = StepNow.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.StepAtk) StepAtk[k] = StepAtk.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Over) Over[k] = Over.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Half) Half[k] = Half.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.OverEv) OverEv[k] = OverEv.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Capped) Capped[k] = Capped.GetValueOrDefault(k) + v;
            for (int i = 0; i < 7; i++) { KillBehind[i] += o.KillBehind[i]; KillNone[i] += o.KillNone[i]; KillOff[i] += o.KillOff[i]; }
            DrifterNominal += o.DrifterNominal; DrifterGained += o.DrifterGained; TendNominal += o.TendNominal; TendGained += o.TendGained;
        }
        public double Per(long x) => (double)x / Math.Max(1, N);

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            N++;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var pIds = p.Select(u => u.Def.Id).ToHashSet();
            foreach (var (id, t) in r.TallyByUnit)
            {
                if (!pIds.Contains(id)) continue;
                if (id == "shio") { DrifterNominal += t.DrifterNominal + t.TendNominal; DrifterGained += t.DrifterGained + t.TendGained; }
                long o = Tally(t, "ShioOverflowRecv"), h = Tally(t, "ShioOverflowHalf"), n = Tally(t, "ShioOverflowEvents");
                if (n == 0) continue;
                Over[id] = Over.GetValueOrDefault(id) + o; Half[id] = Half.GetValueOrDefault(id) + h; OverEv[id] = OverEv.GetValueOrDefault(id) + n;
                if (h >= 15) Capped[id] = Capped.GetValueOrDefault(id) + 1;
            }
            var byId = p.Concat(e).ToDictionary(u => u.InstanceId);
            var b = new GaleDiag.Board(p.Concat(e), slot0);
            var atk = byId.ToDictionary(kv => kv.Key, kv => kv.Value.Def.Attack);
            int yomi = p.FirstOrDefault(u => u.Def.Id == "yomi")?.InstanceId ?? -1;
            foreach (BattleEvent ev in r.Events)
            {
                switch (ev.Kind)
                {
                    case BattleEventKind.StatSnapshot when ev.TargetId is int su:
                        atk[su] = ev.Amount; break;
                    case BattleEventKind.Tailwind when ev.TargetId is int st && byId.TryGetValue(st, out var su2):
                        {
                            TailEvents++;
                            StepNow[su2.Def.Id] = StepNow.GetValueOrDefault(su2.Def.Id) + 1;
                            int lane = ev.Slot;
                            var line = b.LaneAllies(BattleContext.PlayerTeam, lane);
                            int best = -1;
                            for (int i = 1; i < line.Count; i++)
                            {
                                int u = line[i];
                                if (b.Hp[u] * 100 < b.MaxHp[u] * TailwindTrait.HpGatePercent) continue;
                                if (best < 0) { best = u; continue; }
                                int a = atk.GetValueOrDefault(u), ab = atk.GetValueOrDefault(best);
                                if (a > ab) best = u;
                                else if (a == ab && line.IndexOf(u) > line.IndexOf(best)) best = u;
                            }
                            if (best >= 0)
                            {
                                string bid = byId[best].Def.Id;
                                StepAtk[bid] = StepAtk.GetValueOrDefault(bid) + 1;
                                if (best == st) SameAsNow++;
                            }
                            break;
                        }
                    case BattleEventKind.Death when ev.TargetId is int d && yomi >= 0 && ev.ActorId == yomi
                                                   && byId.TryGetValue(d, out var dd) && dd.TeamId != BattleContext.PlayerTeam:
                        {
                            int tb = Math.Clamp(ev.Turn, 1, 6);
                            int slot = b.Slot[d];
                            var lanes = FormationRules.LanesOf(slot);
                            if (lanes.Count == 0) { KillOff[tb]++; break; }
                            bool behind = false;
                            foreach (int lane in lanes)
                            {
                                var line = b.LaneAllies(dd.TeamId, lane);
                                int k = line.IndexOf(d);
                                if (k >= 0 && k < line.Count - 1) { behind = true; break; }
                            }
                            if (behind) KillBehind[tb]++; else KillNone[tb]++;
                            break;
                        }
                }
                b.Apply(ev);
            }
        }
    }

    internal static P0Agg MeasureP0(Formation f, int w, EnemyScaleRule sc)
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
        Console.WriteLine("# 第230期 cycle phase0（W0 ＝ 前段の規定・seed 0..199）");
        Console.WriteLine();
        var benches = new[] { ("228 H3 の席", MHane228), ("229 G4 の1位", MHane229) };
        foreach (var (n, f) in benches) Console.WriteLine($"- {n}: {SeatsNamed(f)}");
        Console.WriteLine();
        var cells = new List<(int S, int W)> { (0, 4), (0, 5), (0, 0), (0, 1), (0, 2), (0, 3), (1, 4), (2, 4) };
        var res = new Dictionary<(int B, int S, int W), P0Agg>();
        for (int bi = 0; bi < benches.Length; bi++)
            foreach (var (s, w) in cells) res[(bi, s, w)] = MeasureP0(benches[bi].Item2, w, Scales[s].Sc);

        Console.WriteLine("## Q0-1 追い風で踏み込んだ駒（1戦あたり）と、攻撃力順にしたときの見込み");
        Console.WriteLine();
        Console.WriteLine("見込み ＝ 追い風の瞬間の経路（台本の盤面）で、先頭以外・HP 4割以上の駒のうち**ターン頭の攻撃力**（`StatSnapshot`）が最も高い駒（同値は後ろの行）。ターン中の上昇（軋み・移り木）は入らない。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 全員生存 | 追い風 | 今: " + string.Join(" ／ ", Ids) + " | 攻撃力順: " + string.Join(" ／ ", Ids) + " | 同じ駒 |");
        Console.WriteLine("|---|---|--:|--:|---|---|--:|");
        for (int bi = 0; bi < benches.Length; bi++)
            foreach (var (s, w) in cells)
            {
                var a = res[(bi, s, w)];
                Console.WriteLine($"| {benches[bi].Item1} | {Scales[s].Name} {WaveNames[w]} | {F1(100.0 * a.AllSurv / a.N)} | {F2(a.Per(a.TailEvents))} | "
                    + string.Join(" ／ ", Ids.Select(id => F2(a.Per(a.StepNow.GetValueOrDefault(id))))) + " | "
                    + string.Join(" ／ ", Ids.Select(id => F2(a.Per(a.StepAtk.GetValueOrDefault(id))))) + $" | {F1(100.0 * a.SameAsNow / Math.Max(1, a.TailEvents))}% |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-2 ヨミの撃破（1戦あたり・ターン別）と、そのとき同じ経路の後ろに敵がいたか");
        Console.WriteLine();
        Console.WriteLine("「後ろに敵」＝ 倒れた敵の経路（中央は2本のどちらか）で、それより後ろの席に生きている敵がいた ／ 「いない」＝ 経路の最後尾だった ／ 「経路外」＝ ○前2・○後2（9体の波）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 撃破 | 後ろに敵 ／ いない ／ 経路外 | T1 | T2 | T3 | T4 | T5 | T6+ |");
        Console.WriteLine("|---|---|--:|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < benches.Length; bi++)
            foreach (var (s, w) in cells)
            {
                var a = res[(bi, s, w)];
                long all = a.KillBehind.Sum() + a.KillNone.Sum() + a.KillOff.Sum();
                string T(int t) { long x = a.KillBehind[t] + a.KillNone[t] + a.KillOff[t]; return x == 0 ? "—" : $"{F2(a.Per(x))}（後 {100.0 * a.KillBehind[t] / x:F0}%）"; }
                Console.WriteLine($"| {benches[bi].Item1} | {Scales[s].Name} {WaveNames[w]} | {F2(a.Per(all))} | {F2(a.Per(a.KillBehind.Sum()))} ／ {F2(a.Per(a.KillNone.Sum()))} ／ {F2(a.Per(a.KillOff.Sum()))} | "
                    + string.Join(" | ", Enumerable.Range(1, 6).Select(T)) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-4 移り木・手当ての溢れ（受け手別・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("溢れ ＝ 名目の回復 − 実際に増えた HP（`Heal` が増やした／満タンで増えなかったときだけ。渇き・支援拒否・反転は数えない）。半分 ＝ 1回ごとの切り捨ての和（W3 の攻撃力の上乗せの素・上限前）。上限 ＝ 1戦で半分の和が 15 以上になった戦の割合。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | シオの名目 ／ 増えた | " + string.Join(" | ", Ids.Select(id => id + " 溢れ ／ 半分 ／ 回数 ／ 上限")) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(Ids.Select(_ => "---|")));
        for (int bi = 0; bi < benches.Length; bi++)
            foreach (var (s, w) in cells)
            {
                var a = res[(bi, s, w)];
                Console.WriteLine($"| {benches[bi].Item1} | {Scales[s].Name} {WaveNames[w]} | {F1(a.Per(a.DrifterNominal))} ／ {F1(a.Per(a.DrifterGained))} | "
                    + string.Join(" | ", Ids.Select(id => $"{F1(a.Per(a.Over.GetValueOrDefault(id)))} ／ {F1(a.Per(a.Half.GetValueOrDefault(id)))} ／ {F2(a.Per(a.OverEv.GetValueOrDefault(id)))} ／ {100.0 * a.Capped.GetValueOrDefault(id) / a.N:F0}%")) + " |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
