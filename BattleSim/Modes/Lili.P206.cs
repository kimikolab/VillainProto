using BattleCore;
using static Common;

// =====================================================================================
// lili モード（第206期）—— リリの段を三角数に、祝福の儀を「5倍の等分」に
//
// 指示書は design/PHASE206_LILI3_SPEC.md ／ 報告は design/PHASE206_LILI3.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 lili phase206   # Q0-1〜Q0-4（第205期 V4 ＝ W0 の盤面で回す）
// =====================================================================================

static partial class LiliDiag
{
    sealed class L3
    {
        public int N, Wins, Stall, Died, RiteBattles, FinishBattles;
        public double TurnsSum, WinTurnsSum, RiteFirstSum, HealSum, DmgSum, RiteMaxSum;
        public int RiteMaxMax;
        public int[] TierHist = new int[4];
        public double[] TierTurnSum = new double[3]; public int[] TierTurnN = new int[3];
        public double[] CrossTurnSum = new double[4]; public int[] CrossN = new int[4];
        public UnitTally T = new();
    }

    static L3 Collect3(Formation f, int stage, int seeds = Seeds)
    {
        var l = new L3();
        var gate = new object();
        Formation enemy = EnemyCatalog.Stages[stage].Enemy;
        Parallel.For(0, seeds, seed =>
        {
            BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
            if (!r.TallyByUnit.TryGetValue("lili", out UnitTally? me)) return;
            lock (gate)
            {
                l.N++;
                l.TurnsSum += r.Turns;
                if (r.PlayerWon) { l.Wins++; l.WinTurnsSum += r.Turns; } else if (r.Turns >= BattleEngine.MaxTurns) l.Stall++;
                if (me.Deaths > 0) l.Died++;
                l.T.Add(me);
                l.HealSum += me.KissHealed + me.RiteHealed + me.ReturnHealed;
                l.DmgSum += me.DamageToEnemy;
                if (me.RiteFirstTurn > 0) { l.RiteBattles++; l.RiteFirstSum += me.RiteFirstTurn; }
                if (me.RiteFinish > 0) l.FinishBattles++;
                if (me.RiteFires > 0) { l.RiteMaxSum += me.RiteMaxPerFoe; l.RiteMaxMax = Math.Max(l.RiteMaxMax, me.RiteMaxPerFoe); }
                l.TierHist[Math.Min(3, me.KissTierMax)]++;
                if (me.KissTierTurn is { } tt) for (int k = 0; k < 3; k++) if (tt[k] > 0) { l.TierTurnSum[k] += tt[k]; l.TierTurnN[k]++; }
                if (me.KissHealCross is { } hc) for (int k = 0; k < 4; k++) if (hc[k] > 0) { l.CrossTurnSum[k] += hc[k]; l.CrossN[k]++; }
            }
        });
        return l;
    }

    static string Pct(int x, int n) => (100.0 * x / Math.Max(1, n)).ToString("F0") + "%";
    static string Avg(double s, int n) => n == 0 ? "—" : (s / n).ToString("F1");

    static void Phase206()
    {
        Console.WriteLine("# 第206期 `lili phase206` —— Q0-1〜Q0-4（第205期 V4 ＝ W0 の盤面・seed 0..199）");
        Console.WriteLine();
        var rows = LiliRows().ToList();
        var data = new Dictionary<(string, int), L3>();
        foreach (var (band, name, f) in rows) foreach (int st in new[] { 3, 4 }) data[(name, st)] = Collect3(f, st);

        Console.WriteLine("## Q0-1 / Q0-2 儀式の頭に生きていた敵の数・儀式で最後の敵が倒れた戦");
        Console.WriteLine();
        Console.WriteLine("**儀式で決着** ＝ 儀式の吸い取りで敵が全員倒れた戦の割合（分母は全戦）。**勝ちに占める** ＝ 勝った戦のうち儀式で決着した割合。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 勝率 | 儀式/戦 | 儀式のある戦 | 1体 | 2体 | 3体以上 | 儀式で決着 | 勝ちに占める | 1体あたり最大（平均/最大） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (band, name, f) in rows)
        foreach (int st in new[] { 3, 4 })
        {
            L3 l = data[(name, st)]; long[] h = l.T.RiteFoeHist ?? new long[3]; long hs = Math.Max(1, h.Sum());
            Console.WriteLine("| " + (st == 3 ? name : "") + " | " + (st == 3 ? "四" : "五") + " | " + Pct(l.Wins, l.N) + " | "
                              + (l.T.RiteFires / Math.Max(1.0, l.N)).ToString("F2") + " | " + Pct(l.RiteBattles, l.N) + " | "
                              + string.Join(" | ", h.Select(x => (100.0 * x / hs).ToString("F0") + "%")) + " | "
                              + Pct(l.FinishBattles, l.N) + " | " + Pct(l.FinishBattles, l.Wins) + " | "
                              + Avg(l.RiteMaxSum, l.RiteBattles) + " / " + l.RiteMaxMax + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## Q0-3 段の到達（第205期 V4・40 ごと）と、施した累計が 40 / 120 / 240 / 400 を越えたターン（＝三角数の段1〜4）");
        Console.WriteLine();
        Console.WriteLine("**越えた戦** は戦のうちその累計に届いた割合、**T** はその平均ターン（届いた戦だけ）。**決着T** は全戦の平均。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 決着T | 段 0/1/2/3+（40 ごと） | 段1 T | 段2 T | 40 越えた戦 / T | 120 / T | 240 / T | 400 / T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (band, name, f) in rows)
        foreach (int st in new[] { 3, 4 })
        {
            L3 l = data[(name, st)];
            Console.WriteLine("| " + (st == 3 ? name : "") + " | " + (st == 3 ? "四" : "五") + " | " + Avg(l.TurnsSum, l.N) + " | "
                              + string.Join("/", l.TierHist.Select(x => (100.0 * x / Math.Max(1, l.N)).ToString("F0"))) + " | "
                              + Avg(l.TierTurnSum[0], l.TierTurnN[0]) + " | " + Avg(l.TierTurnSum[1], l.TierTurnN[1]) + " | "
                              + string.Join(" | ", Enumerable.Range(0, 4).Select(k => Pct(l.CrossN[k], l.N) + " / " + Avg(l.CrossTurnSum[k], l.CrossN[k]))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## Q0-4 敵が1体だけの戦（`EnemyCatalog` の編成の体数）");
        Console.WriteLine();
        Console.WriteLine("| 集合 | 編成 | 体数 |");
        Console.WriteLine("|---|---|--:|");
        int singles = 0;
        void Scan(string set, string name, Formation f)
        {
            int n = f.Occupied().Count();
            if (n <= 2) singles++;
            Console.WriteLine("| " + set + " | " + name + " | " + n + (n <= 2 ? " **←**" : "") + " |");
        }
        foreach (var s in EnemyCatalog.Stages) Scan("Stages", s.Name, s.Enemy);
        foreach (var s in EnemyCatalog.Vanguards) Scan("Vanguards", s.Name, s.Enemy);
        foreach (var s in EnemyCatalog.Pattern3Copies) Scan("Pattern3Copies", s.Name, s.Enemy);
        foreach (var c in EnemyCatalog.Columns) for (int i = 0; i < c.Squads.Count; i++) Scan("Columns", c.Name + " #" + (i + 1), c.Squads[i]);
        Console.WriteLine();
        Console.WriteLine("体数 2 以下の編成: **" + singles + "**。");
        Console.WriteLine();
    }
}
