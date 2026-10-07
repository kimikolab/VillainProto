using BattleCore;
using static Common;

// =====================================================================================
// burst モード（第220期） —— 澱みが爆ぜる（ミオの印を爆弾に）＋ラウを雷の編成へ
//
// 指示書は design/PHASE220_MIRE_BURST_SPEC.md ／ 報告は design/PHASE220_MIRE_BURST.md。
// **線は置かない**（採否はポンが遊んで決める）。台は `Presets` に足さない（この診断のローカル）。
//
//     dotnet run --project BattleSim -c Release 0 burst phase0   # Q0-1〜Q0-6（実装前・盤面は前段＝F1 の規定化の後）
//     dotnet run --project BattleSim -c Release 0 burst run      # 表A〜F（実装後）
//     dotnet run --project BattleSim -c Release 0 burst check    # 自己検査（受け入れ 2〜4）
// =====================================================================================

static partial class BurstDiag
{
    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(arg); return;
            default:
                Console.WriteLine("burst: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunImpl(string arg);
    static partial void CheckImpl(string arg);

    internal const int MeasSeeds = 200;
    internal static readonly int[] Waves25 = { 1, 2, 3, 4 };
    internal static readonly int[] Scales = { 115, 150, 200 };
    internal static EnemyScaleRule Sc(int x) => new(x, x);
    internal static BossRule BossOf(int x) => new(false) { Scale = Sc(x) };

    // =================================================================================
    // 台（指示書 §6.1）
    // =================================================================================

    internal static readonly (string Tag, string Aim, Func<List<UnitDef>> Members)[] Rigs =
    {
        ("R1", "ラウが殴る駒・毒の運び手", () => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, UnitCatalog.Rau, UnitCatalog.Kubi }),
        ("R2", "毒が最も濃い", () => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, UnitCatalog.Rau, UnitCatalog.Guza }),
        ("R3", "前段の基準（ポンの X字の顔ぶれ）", () => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, UnitCatalog.Guza, UnitCatalog.Kubi }),
        ("R4", "毒の少ない雷の台", () => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, UnitCatalog.TouT0, UnitCatalog.Kugu }),
    };

    /// <summary>R3 のポンの X字の席（第216期の台A）。</summary>
    internal static Formation PonX(UnitDef? mio = null) => Formation.Build(
        front1: UnitCatalog.Kubi, front3: UnitCatalog.Guza, center: UnitCatalog.Beni, back1: mio ?? UnitCatalog.Mio, back3: UnitCatalog.Kata);

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    /// <summary>R5 ＝ `compare` でミオのいる行（元の席）。ラウだけの行は版で動かない対照として別に数える。</summary>
    internal static List<(string Name, Formation F)> R5Rows()
        => CompareBuilds().Where(r => Has(r.F, "mio")).Select(r => (r.Name, r.F)).ToList();
    internal static List<(string Name, Formation F)> RauOnlyRows()
        => CompareBuilds().Where(r => Has(r.F, "rau") && !Has(r.F, "mio")).Select(r => (r.Name, r.F)).ToList();

    internal static Formation WithMio(Formation f, UnitDef mio)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "mio" ? mio : d;
        return g;
    }

    /// <summary>
    /// 席の総当たり（指示書 §6.1: <b>倍率 200</b>・版の札を持つミオで・第2〜5波 × seed 1000..1049 の勝ち数）。
    /// <b>同値は 全員生存の数 → 決着ターンの和（少ない方）→ 列挙順</b>（第219期と同じ割り方・R276）。
    /// </summary>
    internal static (Formation Best, int Wins, int Ties, int TiesAfter) PickSeat(List<UnitDef> members, FormationShape sh, int scale)
    {
        var all = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation { Shape = sh };
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            all.Add(f);
        }
        var win = new int[all.Count]; var surv = new int[all.Count]; var turns = new long[all.Count];
        var boss = BossOf(scale);
        Parallel.For(0, all.Count * 4, j =>
        {
            int i = j / 4, st = 1 + j % 4;
            int w = 0, a = 0; long t = 0;
            for (int s = 1000; s < 1050; s++)
            {
                BattleResult r = BattleEngine.Run(all[i], EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss);
                if (r.PlayerWon) { w++; t += r.Turns; if (r.PlayerStarterFallen.Count == 0) a++; } else t += 31;
            }
            Interlocked.Add(ref win[i], w); Interlocked.Add(ref surv[i], a); Interlocked.Add(ref turns[i], t);
        });
        int best = 0;
        for (int i = 1; i < all.Count; i++)
            if (win[i] > win[best] || (win[i] == win[best] && (surv[i] > surv[best] || (surv[i] == surv[best] && turns[i] < turns[best])))) best = i;
        return (all[best], win[best], win.Count(x => x == win[best]),
                Enumerable.Range(0, all.Count).Count(i => win[i] == win[best] && surv[i] == surv[best] && turns[i] == turns[best]));
    }

    // =================================================================================
    // 集計
    // =================================================================================

    internal sealed class BAgg
    {
        public readonly long[] Wins = new long[5], AllSurv = new long[5], N = new long[5];
        public long WinTurns, WinN;
        public readonly BurstLedger B = new();
        public readonly long[] FoeDeathsByWave = new long[5], FoeMarkedByWave = new long[5], FoePoisonByWave = new long[5], FoeMarksByWave = new long[5];
        public readonly Dictionary<string, UnitTally> ByUnit = new();
        public readonly UnitTally P = new(), E = new();
        public long BrittleBurst, BrittleBurstBase;

        public void Take(BattleResult r, int st, HashSet<string> playerIds)
        {
            N[st]++;
            if (r.PlayerWon) { Wins[st]++; WinTurns += r.Turns; WinN++; if (r.PlayerStarterFallen.Count == 0) AllSurv[st]++; }
            foreach (var (id, t) in r.TallyByUnit)
            {
                bool pl = playerIds.Contains(id);
                (pl ? P : E).Add(t);
                if (pl) { if (!ByUnit.TryGetValue(id, out var u)) ByUnit[id] = u = new UnitTally(); u.Add(t); }
            }
            if (r.Burst is BurstLedger b)
            {
                Add(B, b);
                FoeDeathsByWave[st] += b.Deaths[0]; FoeMarkedByWave[st] += b.DeathsMarked[0];
                FoePoisonByWave[st] += b.PoisonSum[0]; FoeMarksByWave[st] += b.MarkSum[0];
            }
            if (r.Brittle is BrittleLedger br && BrittleLedger.Routes.Length > 8)
            {
                BrittleBurst += br.Extra[0, 8] + br.Extra[1, 8];
                BrittleBurstBase += br.Base[0, 8] + br.Base[1, 8];
            }
        }

        public void Merge(BAgg o)
        {
            for (int i = 0; i < 5; i++)
            {
                Wins[i] += o.Wins[i]; AllSurv[i] += o.AllSurv[i]; N[i] += o.N[i];
                FoeDeathsByWave[i] += o.FoeDeathsByWave[i]; FoeMarkedByWave[i] += o.FoeMarkedByWave[i];
                FoePoisonByWave[i] += o.FoePoisonByWave[i]; FoeMarksByWave[i] += o.FoeMarksByWave[i];
            }
            WinTurns += o.WinTurns; WinN += o.WinN;
            Add(B, o.B);
            P.Add(o.P); E.Add(o.E);
            foreach (var (k, v) in o.ByUnit) { if (!ByUnit.TryGetValue(k, out var u)) ByUnit[k] = u = new UnitTally(); u.Add(v); }
            BrittleBurst += o.BrittleBurst; BrittleBurstBase += o.BrittleBurstBase;
        }

        static void AddArr(long[] a, long[] b) { for (int i = 0; i < a.Length; i++) a[i] += b[i]; }
        internal static void Add(BurstLedger a, BurstLedger b)
        {
            AddArr(a.Deaths, b.Deaths); AddArr(a.DeathsMarked, b.DeathsMarked);
            for (int s = 0; s < 2; s++) { AddArr(a.PoisonHist[s], b.PoisonHist[s]); AddArr(a.MarkHist[s], b.MarkHist[s]); AddArr(a.NeighborHist[s], b.NeighborHist[s]); }
            AddArr(a.PoisonSum, b.PoisonSum); AddArr(a.MarkSum, b.MarkSum); AddArr(a.NeighborSum, b.NeighborSum);
            a.HandoffAndContagion += b.HandoffAndContagion;
            AddArr(a.PoisonWrites, b.PoisonWrites); AddArr(a.PoisonAmount, b.PoisonAmount);
            AddArr(a.Bursts, b.Bursts); AddArr(a.BurstsEmpty, b.BurstsEmpty);
            AddArr(a.Hits, b.Hits); AddArr(a.Nominal, b.Nominal); AddArr(a.Removed, b.Removed); AddArr(a.Kills, b.Kills); AddArr(a.ShockPops, b.ShockPops);
            a.InverseHits += b.InverseHits; a.InverseNominal += b.InverseNominal; a.InverseHealed += b.InverseHealed;
            AddArr(a.StageBursts, b.StageBursts); AddArr(a.StageNominal, b.StageNominal); AddArr(a.StageHits, b.StageHits);
            AddArr(a.ChainLenHist, b.ChainLenHist);
            AddArr(a.ChainIdxChains, b.ChainIdxChains); AddArr(a.ChainIdxBursts, b.ChainIdxBursts); AddArr(a.ChainIdxNominal, b.ChainIdxNominal);
            a.RootByDischarge += b.RootByDischarge; a.RootByThunder += b.RootByThunder;
        }

        public long Battles => N.Sum();
        public double WinPct(int st) => N[st] == 0 ? double.NaN : 100.0 * Wins[st] / N[st];
        public double Mean25 => Waves25.Average(WinPct);
        public double AllSurvPct => 100.0 * AllSurv.Sum() / Math.Max(1, N.Sum());
        public double MeanWinT => WinN == 0 ? double.NaN : (double)WinTurns / WinN;
        public double Per(long x) => (double)x / Math.Max(1, Battles);
    }

    internal static BAgg Measure(Formation f, int scale, int seed0 = 0, int seeds = MeasSeeds, IReadOnlyList<int>? waves = null)
    {
        waves ??= Waves25;
        var playerIds = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        var boss = BossOf(scale);
        var total = new BAgg();
        var gate = new object();
        Parallel.For(0, waves.Count * seeds, () => new BAgg(), (j, _, local) =>
        {
            int st = waves[j / seeds], s = seed0 + j % seeds;
            local.Take(BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss), st, playerIds);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    internal static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    internal static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");
    internal static string P1(long a, long b) => b == 0 ? "—" : (100.0 * a / b).ToString("F1");
    internal static string D1(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;±0.0");
    internal static string Short(UnitDef d) => d.Name.Split('の').Last();
    internal static string ShapeName(FormationShape s) => s == FormationShape.X ? "X字" : "P2";
    internal static string SeatsNamed(Formation f) => string.Join(" ／ ", f.Occupied().Select(o => f.Shape.FrameNames[o.Slot] + ":" + Short(o.Def)));

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第220期 Phase 0 —— 澱みが爆ぜる（盤面は前段＝燃焼の脆さ F1 の規定化の後・爆発は未実装）");
        Console.WriteLine();
        Console.WriteLine("- `EmberRule.Default` ＝ " + EmberRule.Default);
        Console.WriteLine("- ミオの札（B0・爆発の札を抜いた駒）: " + string.Join(", ", MioBase().Traits) + " ／ フレーバー「" + UnitCatalog.Mio.Flavor + "」");
        Console.WriteLine("- ラウの札: " + string.Join(", ", UnitCatalog.Rau.Traits));
        Console.WriteLine();

        // ---- Q0-3（静的）----
        Console.WriteLine("## Q0-3 敵の隣接（静的・全員が生きているとき）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 陣形 | 駒ごとの隣の敵の数 | 平均 |");
        Console.WriteLine("|---|---|---|--:|");
        var waves = EnemyCatalog.Stages.Select((s, i) => (Name: "第" + (i + 1) + "波", F: s.Enemy)).Skip(1)
            .Concat(EnemyCatalog.Pattern3Copies.Select(s => (Name: s.Name + "（P3 の写し）", F: s.Enemy)));
        foreach (var (name, f) in waves)
        {
            var units = BattleEngine.Materialize(f, BattleContext.EnemyTeam);
            var counts = units.Select(u => units.Count(v => v != u && FormationRules.AreAdjacent(u, v))).ToList();
            Console.WriteLine("| " + name + " | " + (f.Shape == FormationShape.X ? "X字" : "P3") + " | " + string.Join(" ", units.Zip(counts, (u, c) => u.Def.Name.Split('の').Last() + c)) + " | " + F2(counts.Average()) + " |");
        }
        Console.WriteLine();

        // ---- 仮の席 ----
        Console.WriteLine("## Q0-6 台と仮の席（B0 × 倍率 200 × 第2〜5波 × seed 1000..1049・同値は 全員生存 → 決着T → 列挙順）");
        Console.WriteLine();
        Console.WriteLine("本番の席は実装の後に **B2 × 倍率 200** で選び直す（指示書 §6.1）。ここは在庫を読むための B0 の仮の席。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 席 | 勝ち数/200 | 同値 | 3段で同値 | 勝率 115 | 150 | 200 | 全員生存 200 | 決着T 200 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        var benches = new List<(string, Formation)>();
        foreach (var (tag, _, mem) in Rigs)
            foreach (var sh in new[] { FormationShape.X, FormationShape.Diamond })
            {
                var (best, w, ties, ta) = PickSeat(mem().Select(d => d.Id == "mio" ? MioBase() : d).ToList(), sh, 200);   // 第220期の追記: B0 に固定
                benches.Add((tag + " " + ShapeName(sh), best));
                var a = Scales.Select(x => Measure(best, x)).ToArray();
                Console.WriteLine("| " + tag + " | " + ShapeName(sh) + " | " + SeatsNamed(best) + " | " + w + " | " + ties + " | " + ta + " | "
                                  + string.Join(" | ", a.Select(x => F1(x.Mean25))) + " | " + F1(a[2].AllSurvPct) + " | " + F2(a[2].MeanWinT) + " |");
            }
        {
            benches.Add(("R3 ポンの X字", PonX(MioBase())));
            var a = Scales.Select(x => Measure(PonX(MioBase()), x)).ToArray();
            Console.WriteLine("| R3 | ポンの X字 | " + SeatsNamed(PonX()) + " | — | — | — | " + string.Join(" | ", a.Select(x => F1(x.Mean25))) + " | " + F1(a[2].AllSurvPct) + " | " + F2(a[2].MeanWinT) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("R5（`compare` でミオのいる行）: " + string.Join(" ／ ", R5Rows().Select(r => r.Name)));
        Console.WriteLine("ラウだけの行（版で動かないはずの対照）: " + string.Join(" ／ ", RauOnlyRows().Select(r => r.Name)));
        Console.WriteLine();
        foreach (var (n, f) in R5Rows()) benches.Add(("R5 " + n, WithMio(f, MioBase())));

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1 倒れた瞬間の在庫（敵・第2〜5波 × seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 倒れた敵 /戦 | うち印あり % | 印ありの毒の層 平均 | 印の数 平均 | 毒の層の段（0 / 1〜3 / 4〜7 / 8〜15 / 16〜31 / 32〜 %） | 印の数（1/2/3/4/5〜 %） | B1 の1発（名目） | B2 の1発（名目） | 隣の生きている敵 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|--:|--:|--:|");
        var aggs = new Dictionary<(string, int), BAgg>();
        foreach (var (n, f) in benches)
            foreach (int sc in Scales)
            {
                var a = Measure(f, sc);
                aggs[(n, sc)] = a;
                var b = a.B;
                long m = b.DeathsMarked[0];
                double pAvg = m == 0 ? double.NaN : (double)b.PoisonSum[0] / m, kAvg = m == 0 ? double.NaN : (double)b.MarkSum[0] / m;
                Console.WriteLine("| " + n + " | " + sc + " | " + F2(a.Per(b.Deaths[0])) + " | " + P1(m, b.Deaths[0]) + " | " + F1(pAvg) + " | " + F2(kAvg) + " | "
                                  + string.Join(" / ", b.PoisonHist[0].Select(x => P1(x, m))) + " | " + string.Join(" / ", b.MarkHist[0].Skip(1).Select(x => P1(x, m))) + " | "
                                  + F1(pAvg / 2) + " | " + F1(pAvg * (1 + kAvg) / 4) + " | " + F2((double)b.NeighborSum[0] / Math.Max(1, b.Deaths[0])) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("### 波ごと（倍率 150）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 第2波 印あり % ／ 毒 ／ 印 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (n, _) in benches)
        {
            var a = aggs[(n, 150)];
            Console.WriteLine("| " + n + " | " + string.Join(" | ", Waves25.Select(st =>
                P1(a.FoeMarkedByWave[st], a.FoeDeathsByWave[st]) + " ／ " + F1(a.FoeMarkedByWave[st] == 0 ? double.NaN : (double)a.FoePoisonByWave[st] / a.FoeMarkedByWave[st])
                + " ／ " + F2(a.FoeMarkedByWave[st] == 0 ? double.NaN : (double)a.FoeMarksByWave[st] / a.FoeMarkedByWave[st]))) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("### 倒れた瞬間の隣の生きている敵（倍率 150・分布 0/1/2/3/4/5〜 %）");
        Console.WriteLine();
        foreach (var (n, _) in benches)
        {
            var b = aggs[(n, 150)].B;
            Console.WriteLine("- " + n + ": " + string.Join(" / ", b.NeighborHist[0].Select(x => P1(x, b.Deaths[0]))));
        }
        Console.WriteLine();
        Console.WriteLine("### 味方側（B2x の燃料・倍率 150）: 倒れた味方 /戦 ・ うち印あり % ・ 印ありの毒の層 ・ 印の数");
        Console.WriteLine();
        foreach (var (n, _) in benches)
        {
            var a = aggs[(n, 150)]; var b = a.B;
            long m = b.DeathsMarked[1];
            Console.WriteLine("- " + n + ": " + F2(a.Per(b.Deaths[1])) + " ・ " + P1(m, b.Deaths[1]) + "% ・ " + F1(m == 0 ? double.NaN : (double)b.PoisonSum[1] / m) + " ・ " + F2(m == 0 ? double.NaN : (double)b.MarkSum[1] / m));
        }
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4 ラウ（倍率 150・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | ラウが振った | ラウが弾いた感電 | うつした（回 ／ 層） | 死骸から飛んだ（回 ／ 層） | うつしの漏れ（回 ／ 層） | 同じ死で印の移りと重なった |");
        Console.WriteLine("|---|--:|--:|---|---|---|--:|");
        foreach (var (n, f) in benches.Where(x => Has(x.Item2, "rau")))
        {
            var a = aggs[(n, 150)]; var b = a.B;
            var rt = a.ByUnit.GetValueOrDefault("rau") ?? new UnitTally();
            int T = (int)PoisonRoute.Touch, C = (int)PoisonRoute.Contagion, L = (int)PoisonRoute.TouchLeak;
            Console.WriteLine("| " + n + " | " + F2(a.Per(rt.Attacks)) + " | " + F2(a.Per(rt.ShockTriggered)) + " | " + F2(a.Per(b.PoisonWrites[T])) + " ／ " + F1(a.Per(b.PoisonAmount[T])) + " | "
                              + F2(a.Per(b.PoisonWrites[C])) + " ／ " + F1(a.Per(b.PoisonAmount[C])) + " | " + F2(a.Per(b.PoisonWrites[L])) + " ／ " + F1(a.Per(b.PoisonAmount[L])) + " | "
                              + F2(a.Per(b.HandoffAndContagion)) + " |");
        }
        Console.WriteLine();
    }
}
