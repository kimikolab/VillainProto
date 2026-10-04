using BattleCore;
using static Common;
using CW = CheckWaveDiag;

// bosswave venom0 ／ venom ／ check268 ／ log268 —— 第268期「勇者を D-40 で確定・毒の解答『蝕み』」。
// 指示書は design/PHASE268_BOSS_VENOM_TAX_SPEC.md ／ 報告は design/PHASE268_BOSS_VENOM_TAX.md。
// 勇者は第266期の D-40（全体・HP 3,000・攻 12・速 14・天井 +11・動じない・自前の回復 40%）。版は蝕みの N だけ。
static partial class BossWaveDiag
{
    static readonly string PoisonLabel = StatusKeys.LabelOf(StatusKeys.Poison);

    sealed record VVer(string Name, string What, Func<EnemyWave> Make);
    static UnitDef VenomHero(TraitId? tax) => new()
    {
        Id = "bw_indom", Name = "勇者", MaxHp = CW.ChosenBossHp, Attack = 12, Speed = 14, Pattern = AttackPattern.All,
        Traits = tax is TraitId t ? new[] { TraitId.BossRise11, TraitId.BossMend40, TraitId.BossSteadfast, t } : new[] { TraitId.BossRise11, TraitId.BossMend40, TraitId.BossSteadfast },
        PlusText = "D-40（第266期）＋ 蝕み（第268期・bosswave）",
    };
    static VVer[] VVers => new[]
    {
        new VVer("V-半", $"蝕み N={VenomTaxTrait.Half}（机上で回復半減）", () => EnemyWave.Of((2, VenomHero(TraitId.VenomTaxHalf)))),
        new VVer("V-封", $"蝕み N={VenomTaxTrait.Seal}（机上で回復ゼロ）", () => EnemyWave.Of((2, VenomHero(TraitId.VenomTaxSeal)))),
        new VVer("V-0", "対照: D-40（蝕みなし・第266期の D-40 と同じ定義）", () => EnemyWave.Of((2, VenomHero(null)))),
    };

    sealed class VAgg
    {
        public long N, Wins, WinT, Surv, Kill, KillT, NoKillDmg, FirstDeath, FirstDeathT, Alive, Dealt, Healed, HeroHealed, Cut, PoisonBattles;
        public readonly long[] LayerSum = new long[Window + 1], LayerCnt = new long[Window + 1];
        public long LayerAll, LayerAllCnt, LayerMaxSum;

        public void Merge(VAgg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Surv += o.Surv; Kill += o.Kill; KillT += o.KillT; NoKillDmg += o.NoKillDmg; FirstDeath += o.FirstDeath; FirstDeathT += o.FirstDeathT;
            Alive += o.Alive; Dealt += o.Dealt; Healed += o.Healed; HeroHealed += o.HeroHealed; Cut += o.Cut; PoisonBattles += o.PoisonBattles;
            for (int t = 0; t <= Window; t++) { LayerSum[t] += o.LayerSum[t]; LayerCnt[t] += o.LayerCnt[t]; }
            LayerAll += o.LayerAll; LayerAllCnt += o.LayerAllCnt; LayerMaxSum += o.LayerMaxSum;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++;
            var hero = e[0]; int id = hero.InstanceId;
            var mine = p.Select(u => u.InstanceId).ToHashSet();
            bool alive = true, snap = false; int? killT = null, firstT = null; long taken = 0; int turn = 0, layerMax = 0;
            var layerAt = new Dictionary<int, int>();
            foreach (var x in r.Events)
            {
                switch (x.Kind)
                {
                    case BattleEventKind.TurnStart: turn = x.Turn; snap = false; if (x.Turn <= Window && alive) { Alive++; layerAt[turn] = 0; } break;
                    case BattleEventKind.StatusSnapshot when x.TargetId == id && x.Text == PoisonLabel: layerAt[turn] = x.Amount; layerMax = Math.Max(layerMax, x.Amount); break;
                    case BattleEventKind.StatSnapshot: snap = true; break;
                    case BattleEventKind.Heal when x.TargetId == id: HeroHealed += x.Amount; break;
                    case BattleEventKind.Heal when x.TargetId is int h && mine.Contains(h) && x.Turn <= Window: Healed += x.Amount; break;
                    case BattleEventKind.Damage when x.ActorId == id && x.TargetId is int t && mine.Contains(t) && x.Turn <= Window: Dealt += x.Amount; break;
                    case BattleEventKind.Damage when x.TargetId == id && x.Amount > 0 && x.Turn <= Window: taken += x.Amount; break;
                    case BattleEventKind.Death when x.TargetId is int d:
                        if (d == id && alive) { alive = false; killT = x.Turn; }
                        if (mine.Contains(d)) firstT ??= x.Turn;
                        break;
                }
            }
            foreach (var (t, v) in layerAt) { if (t <= Window) { LayerSum[t] += v; LayerCnt[t]++; LayerAll += v; LayerAllCnt++; } }
            LayerMaxSum += layerMax; if (layerMax > 0) PoisonBattles++;
            Cut += hero.RawCounter(VenomTaxTrait.CutKey);
            if (r.PlayerWon) { Wins++; WinT += r.Turns; if (r.PlayerStarterFallen.Count == 0) Surv++; }
            if (killT is int kt) { Kill++; KillT += kt; } else NoKillDmg += taken;
            if (firstT is int ft) { FirstDeath++; FirstDeathT += ft; }
        }

        public double Win => 100.0 * Wins / N;
        public double KillTAvg => Kill == 0 ? double.PositiveInfinity : (double)KillT / Kill;
        public double LayerAvg => LayerAllCnt == 0 ? 0 : (double)LayerAll / LayerAllCnt;
        public string LayerAt(int t) => LayerCnt[t] == 0 ? "—" : ((double)LayerSum[t] / LayerCnt[t]).ToString("F1");
    }

    static VAgg VMeasure(string board, Func<EnemyWave> make)
    {
        var f = CW.BoardOf(board);
        var parts = new VAgg[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var (r, p, e) = CW.Fight(f, () => BattleEngine.MaterializeEnemy(make(), EnemyScaleRule.None), i, verbose: true);
            var a = new VAgg(); a.Take(r, p, e); parts[i] = a;
        });
        var all = new VAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string VKill(VAgg a) => a.Kill == 0 ? $"倒せない（{Per(a.NoKillDmg, a.N)}）" : $"T{Per(a.KillT, a.Kill)}（{Pct(a.Kill, a.N)}%）";

    /// <summary>Phase 0: D-40 で勇者に積まれる毒の層（ターン頭の刻みの後＝回復が入るときの値）。</summary>
    static void Venom0()
    {
        Console.WriteLine("# 第268期 Phase 0 —— D-40 で勇者に積まれる毒の層（回復が入るときの値・7台・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("層は「ターン頭の刻みの後の残量」（`StatusSnapshot`）。毒の層は刻みで減らず、上限も無い。平均は勇者が生きていたターン頭（20 ターンまで）で取る。");
        Console.WriteLine();
        int[] ts = { 1, 2, 3, 4, 5, 6, 8, 10, 12, 15 };
        Console.WriteLine("| 台 | 層が積まれた戦 | 層の平均 | 層の最大（戦の平均）| " + string.Join(" | ", ts.Select(t => $"T{t}")) + " | 倒しT | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|" + string.Concat(ts.Select(_ => "--:|")) + "--:|--:|");
        foreach (string b in Boards)
        {
            var a = VMeasure(b, () => EnemyWave.Of((2, VenomHero(null))));
            Console.WriteLine($"| {b} | {Pct(a.PoisonBattles, a.N)}% | {a.LayerAvg:F1} | {Per(a.LayerMaxSum, a.N)} | " + string.Join(" | ", ts.Select(a.LayerAt)) + $" | {VKill(a)} | {F1(a.Win)} |");
        }
    }

    static void VenomRun()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = VVers;
        Console.WriteLine($"# 第268期 —— 蝕み（N = {VenomTaxTrait.Half} ／ {VenomTaxTrait.Seal}）＋ 対照 V-0（D-40）（7台・倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"勝ち ＝ 勇者を倒す（engine の打ち切り {BattleEngine.MaxTurns} ターン）。純実入りは {Window} ターンまで。蝕みで減った回復量は名目（最大HPの 40% ＝ 1,200 から引いた量・戦の終わりまで）。");
        Console.WriteLine();
        foreach (var v in vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        var res = new Dictionary<(string, string), VAgg>();
        foreach (string b in Boards) foreach (var v in vers) res[(b, v.Name)] = VMeasure(b, v.Make);

        Console.WriteLine("## 表1 勝率 ／ 全員生存（%）・倒しT");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", vers.Select(v => "勝率 " + v.Name)) + " | " + string.Join(" | ", vers.Select(v => "倒しT " + v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(vers.Select(_ => "--:|")) + string.Concat(vers.Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", vers.Select(v => $"{F1(res[(b, v.Name)].Win)} ／ {F1(100.0 * res[(b, v.Name)].Surv / res[(b, v.Name)].N)}")) + " | " + string.Join(" | ", vers.Select(v => VKill(res[(b, v.Name)]))) + " |");
        Console.WriteLine();
        Console.WriteLine("## 表2 蝕みで減った回復量・勇者が受けた回復・層（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 蝕みで減った回復（名目）| 勇者が受けた回復 | 層の平均 | T3 ／ T5 ／ T8 の層 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (string b in Boards)
            foreach (var v in vers)
            {
                var a = res[(b, v.Name)];
                Console.WriteLine($"| {b} | {v.Name} | {Per(a.Cut, a.N)} | {Per(a.HeroHealed, a.N)} | {a.LayerAvg:F1} | {a.LayerAt(3)} ／ {a.LayerAt(5)} ／ {a.LayerAt(8)} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 表3 純実入り（20 ターンまで）・崩れ始め");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", vers.Select(v => "純実入り " + v.Name)) + " | " + string.Join(" | ", vers.Select(v => "崩れ始め " + v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, vers.Length * 2).Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", vers.Select(v => Per(res[(b, v.Name)].Dealt - res[(b, v.Name)].Healed, res[(b, v.Name)].N)))
                + " | " + string.Join(" | ", vers.Select(v => $"{T(res[(b, v.Name)].FirstDeathT, res[(b, v.Name)].FirstDeath)}（{Pct(res[(b, v.Name)].FirstDeath, res[(b, v.Name)].N)}%）")) + " |");
        Console.WriteLine();

        Console.WriteLine("## 表4 採否（指示書 §4）");
        Console.WriteLine();
        var writers = Boards.Where(b => res[(b, "V-0")].PoisonBattles > 0).ToList();
        var clean = Boards.Where(b => res[(b, "V-0")].PoisonBattles == 0).ToList();
        Console.WriteLine($"毒の書き手がいる台（V-0 で勇者に層が積まれた戦がある）: {string.Join("・", writers)}。いない台: {(clean.Count == 0 ? "—" : string.Join("・", clean))}。");
        Console.WriteLine();
        Console.WriteLine("| 条件 | 中身 | 判定 |");
        Console.WriteLine("|---|---|:-:|");
        {
            var pz = PoisonBoards.OrderBy(x => x, StringComparer.Ordinal).ToList();
            bool ok = pz.All(b => res[(b, "V-封")].Wins > 0);
            Console.WriteLine($"| 毒2台の勝率 > 0（V-封）| {string.Join("・", pz.Select(b => $"{b} {F1(res[(b, "V-封")].Win)}%（V-半 {F1(res[(b, "V-半")].Win)}%）"))} | {(ok ? "○" : "×")} |");
        }
        {
            bool ok = clean.All(b => vers.Take(2).All(v => res[(b, v.Name)].Wins == res[(b, "V-0")].Wins && res[(b, v.Name)].KillT == res[(b, "V-0")].KillT && res[(b, v.Name)].Kill == res[(b, "V-0")].Kill && res[(b, v.Name)].Cut == 0));
            Console.WriteLine($"| 書き手のいない台が V-0 から動かない | {string.Join("・", clean)}：勝率・倒しT・蝕みの量が3版で同一か | {(ok ? "○" : "×")} |");
        }
        foreach (string b in writers.Where(x => !PoisonBoards.Contains(x)))
        {
            double k0 = res[(b, "V-0")].KillTAvg, kh = res[(b, "V-半")].KillTAvg, ks = res[(b, "V-封")].KillTAvg;
            bool grad = kh <= k0 + 1e-9 && ks <= kh + 1e-9 && ks < k0 - 0.05;
            Console.WriteLine($"| {b} が中間の恩恵（倒しT V-0 ≥ V-半 ≥ V-封・縮む）| T{k0:F2} → T{kh:F2} → T{ks:F2} | {(grad ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log268(string board, string ver, int seed)
    {
        var v = VVers.First(x => x.Name == ver);
        var (r, p, e) = CW.Fight(CW.BoardOf(board), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), seed);
        Console.WriteLine($"# {board} × {v.Name}（{v.What}）× seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・蝕みで減った回復 {e[0].RawCounter(VenomTaxTrait.CutKey)}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    static void Check268()
    {
        int pass = 0, fail = 0;
        void Ok(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            Console.WriteLine($"- [{(ok ? "○" : "×")}] {name}{(detail.Length > 0 ? $"（{detail}）" : "")}");
        }
        Console.WriteLine("# bosswave check268 —— 第268期 自己検査");
        Console.WriteLine();
        var mend = TraitCatalog.Resolve(new[] { TraitId.BossMend40 })[0];
        {
            var (ctx, e) = Board(EnemyWave.Of((2, VenomHero(TraitId.VenomTaxSeal))));
            var h = e[0]; h.Hp = 100; h.SetCounter(StatusKeys.Poison, 3);
            mend.OnTurnStart(ctx, h);
            int want = Math.Max(0, 1200 - 3 * VenomTaxTrait.Seal);
            Ok($"(a-1) V-封: 層 3 で回復 1,200 − 3 × {VenomTaxTrait.Seal} ＝ {want}", h.Hp == 100 + want && h.RawCounter(VenomTaxTrait.CutKey) == 1200 - want, $"HP {h.Hp}・減った {h.RawCounter(VenomTaxTrait.CutKey)}");
            Ok("(a-2) 毒の層は消費しない", h.RawCounter(StatusKeys.Poison) == 3);
            h.Hp = 100; h.SetCounter(StatusKeys.Poison, 999);
            mend.OnTurnStart(ctx, h);
            Ok("(a-3) 下限 0（層が多くても HP は減らない）", h.Hp == 100, $"HP {h.Hp}");
        }
        {
            var (ctx, e) = Board(EnemyWave.Of((2, VenomHero(TraitId.VenomTaxHalf))));
            var h = e[0]; h.Hp = 100; h.SetCounter(StatusKeys.Poison, 2);
            mend.OnTurnStart(ctx, h);
            Ok($"(b) V-半: 層 2 で回復 1,200 − 2 × {VenomTaxTrait.Half}", h.Hp == 100 + Math.Max(0, 1200 - 2 * VenomTaxTrait.Half), $"HP {h.Hp}");
        }
        {
            var (ctx, e) = Board(EnemyWave.Of((2, VenomHero(null))));
            var h = e[0]; h.Hp = 100; h.SetCounter(StatusKeys.Poison, 5);
            mend.OnTurnStart(ctx, h);
            Ok("(c) 蝕みの札が無ければ層があっても 1,200 回復（D-40 のまま）", h.Hp == 1300 && h.RawCounter(VenomTaxTrait.CutKey) == 0, $"HP {h.Hp}");
        }
        {
            // V-0 は第266期の D-40 と台本が一致する（同じ札の並び）
            bool same = true; int n = 0;
            foreach (string b in Boards)
                for (int s = 0; s < 4; s++)
                {
                    var r1 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(EnemyWave.Of((2, VenomHero(null))), EnemyScaleRule.None), s).R;
                    var r2 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(IVerOf("D-40").Make(), EnemyScaleRule.None), s).R;
                    same &= r1.Log.Select(l => l.Text).SequenceEqual(r2.Log.Select(l => l.Text)); n++;
                }
            Ok("(d) V-0 ＝ 第266期の D-40（7台 × seed 0..3 の台本一致）", same, $"{n} 戦");
        }
        {
            TraitId[] mine = { TraitId.VenomTaxHalf, TraitId.VenomTaxSeal };
            var enemyDefs = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def))
                .Concat(EnemyCatalog.TestStages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)))
                .Concat(EnemyCatalog.BossStages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)));
            int holders = UnitCatalog.Everyone.Concat(enemyDefs).Count(u => u.Traits.Any(mine.Contains));
            Ok("(e) 蝕みの札 2 枚の保持者がロスターと本編・検証・第265期の敵に 0 枚", holders == 0, $"{holders} 枚");
        }
        {
            bool same = true;
            foreach (var v in VVers.Take(2))
                foreach (string b in Boards)
                    for (int s = 0; s < 4; s++)
                    {
                        var r1 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), s).R;
                        var r2 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), s).R;
                        same &= r1.Log.Select(l => l.Text).SequenceEqual(r2.Log.Select(l => l.Text));
                    }
            Ok("(f) 同じ seed の2回が台本一致（2版 × 7台 × seed 0..3）", same);
        }
        Console.WriteLine();
        Console.WriteLine($"合計 {pass + fail} 項目: ○ {pass} ／ × {fail}");
    }
}
