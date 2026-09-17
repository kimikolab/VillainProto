using BattleCore;
using static Common;

// =====================================================================================
// pace モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "pace")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 pace
// =====================================================================================

static class PaceDiag
{
//     dotnet run --project BattleSim -c Release 0 pace
public static void Run(string[] args, int stageIndex)
{
    var pcBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> pcStages = EnemyCatalog.Stages;
    const int PcSeedsA = 200;   // 0..199。compare と同じ帯（勝率が balance.md と一致することの検算に使う）
    const int PcSeedsB = 200;   // 200..399。選定に使っていない帯（群の定義がここを使う）
    int pcNb = pcBuilds.Length, pcNw = pcStages.Count;

    string[] pcMeasure = { "決着T", "残存数", "被ダメ総量", "与ダメ総量" };
    const int PcNm = 4;

    // pcVal[帯][物差し][波][編成] = その帯の平均値。
    var pcVal = new double[2][][][];
    for (int band = 0; band < 2; band++)
    {
        pcVal[band] = new double[PcNm][][];
        for (int m = 0; m < PcNm; m++)
        {
            pcVal[band][m] = new double[pcNw][];
            for (int w = 0; w < pcNw; w++) pcVal[band][m][w] = new double[pcNb];
        }
    }
    // 勝率は A 帯だけ（compare と同じ計算なので docs/balance.md と一致する）。
    var pcRate = new double[pcNw][];
    for (int w = 0; w < pcNw; w++) pcRate[w] = new double[pcNb];

    // 味方／敵の切り分けは def.Id でやる（TallyByUnit は両陣営を Def.Id で混ぜて持つ）。
    // **同じ Id が両陣営に出ると集計が壊れる**ので、衝突を数えて検算に出す。
    int pcCollisions = 0;

    var pcSw = System.Diagnostics.Stopwatch.StartNew();
    for (int w = 0; w < pcNw; w++)
    {
        var pcEnemyIds = new HashSet<string>(pcStages[w].Enemy.Occupied().Select(o => o.Item2.Id));
        for (int b = 0; b < pcNb; b++)
        {
            foreach ((int _, UnitDef def) in pcBuilds[b].F.Occupied())
                if (pcEnemyIds.Contains(def.Id)) pcCollisions++;

            for (int band = 0; band < 2; band++)
            {
                int from = band == 0 ? 0 : PcSeedsA;
                int n = band == 0 ? PcSeedsA : PcSeedsB;
                double turns = 0, alive = 0, taken = 0, dealt = 0;
                int wins = 0;
                for (int seed = from; seed < from + n; seed++)
                {
                    BattleResult r = BattleEngine.Run(pcBuilds[b].F, pcStages[w].Enemy, seed, verbose: false);
                    if (r.PlayerWon) wins++;
                    turns += r.Turns;
                    alive += r.PlayerSurvivors;
                    // 受け手側から数える（第13期 Phase DA）。毒・燃焼は source が null なので
                    // 出どころ側からは数えられないが、受け手の DamageTaken には必ず載る。
                    foreach ((string id, UnitTally t) in r.TallyByUnit)
                    {
                        if (pcEnemyIds.Contains(id)) dealt += t.DamageTaken;
                        else taken += t.DamageTaken;   // 味方の召喚（胞子）もこちらに入る
                    }
                }
                pcVal[band][0][w][b] = turns / n;
                pcVal[band][1][w][b] = alive / n;
                pcVal[band][2][w][b] = taken / n;
                pcVal[band][3][w][b] = dealt / n;
                if (band == 0) pcRate[w][b] = wins * 100.0 / n;
            }
        }
    }
    pcSw.Stop();

    Console.WriteLine("# 物差しの比較（pace・第54期）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 {pcNb} × 全 {pcNw} 波 × 2帯（A: seed 0..{PcSeedsA - 1} / B: seed {PcSeedsA}..{PcSeedsA + PcSeedsB - 1}）。");
    Console.WriteLine($"計 {pcNb * pcNw * (PcSeedsA + PcSeedsB):N0} 戦 / 所要 {pcSw.Elapsed.TotalSeconds:F1} 秒。");
    Console.WriteLine();
    Console.WriteLine("**盤面は1つも動かない。** `Stages` も `CompareBuilds()` も読むだけ。");
    Console.WriteLine("A 帯の勝率は `compare` と同じ計算なので `docs/balance.md` と一致する（下の検算）。");
    Console.WriteLine();
    Console.WriteLine($"- **検算1**: 味方と敵で Def.Id が衝突した件数 = **{pcCollisions}**（0 でなければ味方/敵の切り分けが壊れている）");
    Console.WriteLine($"- **検算2**: A 帯・第一波で 100.0% の行数 = **{pcRate[0].Count(x => x >= 100.0)} / {pcNb}**");
    Console.Write("- **検算3**: A 帯の波ごとの平均勝率 = ");
    Console.WriteLine(string.Join(" / ", Enumerable.Range(0, pcNw).Select(w => $"{pcRate[w].Average():F1}")));
    Console.WriteLine();

    // ---- 表A: 第一波の分離度 ----------------------------------------------------------
    Console.WriteLine("## 表A: 物差しごとの分離度（第一波）");
    Console.WriteLine();
    Console.WriteLine("`群` は §1-1 の定義（A 帯で昇順に並べ、B 帯でも前後が完全分離する切れ目の数 + 1）。");
    Console.WriteLine("**第一波の勝率は全行 100.0% で分散 0 なので、相関は第2〜5波の勝率に対して取る。**");
    Console.WriteLine("第一波の物差しが後ろの波の勝率を予測するなら、それは既存の情報の焼き直しである。");
    Console.WriteLine();
    Console.Write("| 物差し | 最小 | 最大 | 中央値 | 標準偏差 | **群** |");
    for (int w = 1; w < pcNw; w++) Console.Write($" r(第{w + 1}波) |");
    Console.WriteLine(" **&#124;r&#124;最大** |");
    Console.Write("|---|--:|--:|--:|--:|--:|");
    for (int w = 1; w < pcNw; w++) Console.Write("--:|");
    Console.WriteLine("--:|");
    for (int m = 0; m < PcNm; m++)
    {
        double[] pa = pcVal[0][m][0], pb = pcVal[1][m][0];
        double mean = pa.Average();
        double sd = Math.Sqrt(pa.Select(x => (x - mean) * (x - mean)).Sum() / pa.Length);
        Console.Write($"| {pcMeasure[m]} | {pa.Min():F2} | {pa.Max():F2} | {PcMedian(pa):F2} | {sd:F2} | **{PcGroups(pa, pb)}** |");
        double rmax = 0;
        for (int w = 1; w < pcNw; w++)
        {
            double r = PcCorr(pa, pcRate[w]);
            rmax = Math.Max(rmax, double.IsNaN(r) ? 0 : Math.Abs(r));
            Console.Write(double.IsNaN(r) ? " — |" : $" {r:+0.00;-0.00} |");
        }
        Console.WriteLine($" **{rmax:F2}** |");
    }
    Console.WriteLine();

    // ---- 表B: 全波の分離度 ------------------------------------------------------------
    Console.WriteLine("## 表B: 全波での分離度");
    Console.WriteLine();
    Console.WriteLine("`勝率群` は勝率そのものを同じ定義で群に割ったもの（比較の基準線）。");
    Console.WriteLine("`分離行` = **勝率が完全に同値の塊（サイズ2以上）が、その物差しの群では2つ以上に割れている**とき、");
    Console.WriteLine("その塊に属する行数。**勝率が拾えていない情報の量**を行数で数えている。");
    Console.WriteLine();
    Console.Write("| 波 | 勝率平均 | 勝率群 |");
    for (int m = 0; m < PcNm; m++) Console.Write($" {pcMeasure[m]}(群/分離行) |");
    Console.WriteLine();
    Console.Write("|---|--:|--:|");
    for (int m = 0; m < PcNm; m++) Console.Write("--:|");
    Console.WriteLine();
    for (int w = 0; w < pcNw; w++)
    {
        Console.Write($"| 第{w + 1}波 | {pcRate[w].Average():F1} | {PcGroups(pcRate[w], pcRate[w])} |");
        for (int m = 0; m < PcNm; m++)
            Console.Write($" {PcGroups(pcVal[0][m][w], pcVal[1][m][w])} / **{PcSplitRows(pcRate[w], pcVal[0][m][w], pcVal[1][m][w])}** |");
        Console.WriteLine();
    }
    Console.WriteLine();
    Console.WriteLine("> `勝率群` の B 帯は A 帯と同じ列を渡している（勝率は同じ帯で測った値どうしを");
    Console.WriteLine("> 比べても意味がないため）。**基準線であって、他の列と同じ厳しさでは無い**");
    Console.WriteLine("> ——勝率群は「A 帯で値が違えば必ず切れる」ので上振れする。");
    Console.WriteLine();

    // ---- 表C: 第一波の決着ターンの両端 --------------------------------------------------
    Console.WriteLine("## 表C: 第一波で決着ターンが最も速い5行と最も遅い5行");
    Console.WriteLine();
    Console.WriteLine("`総攻` は編成の素の攻撃力合計、`範囲` は単体型でない駒の数、`最速` は速さの最大値。");
    Console.WriteLine("敵は 前1 討伐隊の新兵(45/11/6) / 前3 戦斧兵(55/12/5・薙ぎ) / 中央 討伐隊の新兵(45/11/6)。総HP 145。");
    Console.WriteLine();
    // 何が速さを決めているか。静的特徴量（第12期 power の写し。新しい量は作らない）との単相関。
    double[] pcAtk = pcBuilds.Select(x => (double)x.F.Occupied().Sum(o => o.Item2.Attack)).ToArray();
    double[] pcArea = pcBuilds.Select(x => (double)x.F.Occupied().Count(o => o.Item2.Pattern != AttackPattern.Single)).ToArray();
    double[] pcSpd = pcBuilds.Select(x => (double)x.F.Occupied().Max(o => o.Item2.Speed)).ToArray();
    double[] pcHp = pcBuilds.Select(x => (double)x.F.Occupied().Sum(o => o.Item2.MaxHp)).ToArray();
    Console.WriteLine("**第一波の決着Tと静的特徴量の単相関（56行）:** "
        + $"総攻 {PcCorr(pcVal[0][0][0], pcAtk):+0.00;-0.00} / 範囲枚数 {PcCorr(pcVal[0][0][0], pcArea):+0.00;-0.00} / "
        + $"最速 {PcCorr(pcVal[0][0][0], pcSpd):+0.00;-0.00} / 総HP {PcCorr(pcVal[0][0][0], pcHp):+0.00;-0.00}");
    Console.WriteLine();
    Console.WriteLine("**物差しどうしの相関（第一波・56行）:**");
    Console.WriteLine();
    Console.Write("| |");
    for (int m = 0; m < PcNm; m++) Console.Write($" {pcMeasure[m]} |");
    Console.WriteLine();
    Console.Write("|---|");
    for (int m = 0; m < PcNm; m++) Console.Write("--:|");
    Console.WriteLine();
    for (int m = 0; m < PcNm; m++)
    {
        Console.Write($"| {pcMeasure[m]} |");
        for (int m2 = 0; m2 < PcNm; m2++) Console.Write($" {PcCorr(pcVal[0][m][0], pcVal[0][m2][0]):+0.00;-0.00} |");
        Console.WriteLine();
    }
    Console.WriteLine();

    var pcOrder = Enumerable.Range(0, pcNb).OrderBy(b => pcVal[0][0][0][b]).ToArray();
    Console.WriteLine("| | 編成 | 決着T | 残存 | 被ダメ | 与ダメ | 総攻 | 範囲 | 最速 | 中身 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|---|");
    for (int i = 0; i < 5; i++) PcRow("**速**", pcOrder[i]);
    Console.WriteLine("| … | | | | | | | | | |");
    for (int i = 5; i >= 1; i--) PcRow("**遅**", pcOrder[pcNb - i]);
    Console.WriteLine();

    void PcRow(string tag, int b)
    {
        UnitDef[] occ = pcBuilds[b].F.Occupied().Select(o => o.Item2).ToArray();
        int atk = occ.Sum(d => d.Attack);
        int area = occ.Count(d => d.Pattern != AttackPattern.Single);
        int spd = occ.Length == 0 ? 0 : occ.Max(d => d.Speed);
        Console.WriteLine($"| {tag} | {pcBuilds[b].Name} | {pcVal[0][0][0][b]:F2} | {pcVal[0][1][0][b]:F2} "
            + $"| {pcVal[0][2][0][b]:F0} | {pcVal[0][3][0][b]:F0} | {atk} | {area} | {spd} "
            + $"| {string.Join(" ", occ.Select(d => $"{d.Name}({d.Attack}/{d.Speed})"))} |");
    }
    return;

    // 中央値。
    static double PcMedian(double[] v)
    {
        double[] s = v.OrderBy(x => x).ToArray();
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2.0;
    }

    // 群の数（§1-1 の定義）。A で昇順に並べ、B でも前半と後半が完全に分離する位置だけを切れ目にする。
    static int PcGroups(double[] a, double[] b)
    {
        int n = a.Length;
        if (n == 0) return 0;
        int[] perm = Enumerable.Range(0, n).OrderBy(i => a[i]).ThenBy(i => i).ToArray();
        // 接尾辞の最小値を先に作る（O(n)）。
        var sufMin = new double[n + 1];
        sufMin[n] = double.PositiveInfinity;
        for (int k = n - 1; k >= 0; k--) sufMin[k] = Math.Min(sufMin[k + 1], b[perm[k]]);
        int cuts = 0;
        double preMax = double.NegativeInfinity;
        for (int k = 0; k < n - 1; k++)
        {
            preMax = Math.Max(preMax, b[perm[k]]);
            if (preMax < sufMin[k + 1]) cuts++;
        }
        return cuts + 1;
    }

    // 「勝率では同値だが物差しでは分かれる行」の数（§1-2 の定義）。
    static int PcSplitRows(double[] rate, double[] a, double[] b)
    {
        int n = rate.Length;
        // 物差しの群番号を各行に振る（PcGroups と同じ切り方）。
        int[] perm = Enumerable.Range(0, n).OrderBy(i => a[i]).ThenBy(i => i).ToArray();
        var sufMin = new double[n + 1];
        sufMin[n] = double.PositiveInfinity;
        for (int k = n - 1; k >= 0; k--) sufMin[k] = Math.Min(sufMin[k + 1], b[perm[k]]);
        var gid = new int[n];
        int g = 0;
        double preMax = double.NegativeInfinity;
        for (int k = 0; k < n; k++)
        {
            gid[perm[k]] = g;
            preMax = Math.Max(preMax, b[perm[k]]);
            if (k < n - 1 && preMax < sufMin[k + 1]) g++;
        }
        int rows = 0;
        foreach (IGrouping<double, int> cls in Enumerable.Range(0, n).GroupBy(i => rate[i]))
        {
            int[] idx = cls.ToArray();
            if (idx.Length < 2) continue;
            if (idx.Select(i => gid[i]).Distinct().Count() >= 2) rows += idx.Length;
        }
        return rows;
    }

    // ピアソン相関。片方の分散が 0 なら定義できないので NaN。
    static double PcCorr(double[] a, double[] b)
    {
        double ma = a.Average(), mb = b.Average();
        double num = 0, da = 0, db = 0;
        for (int i = 0; i < a.Length; i++)
        {
            num += (a[i] - ma) * (b[i] - mb);
            da += (a[i] - ma) * (a[i] - ma);
            db += (b[i] - mb) * (b[i] - mb);
        }
        return da == 0 || db == 0 ? double.NaN : num / Math.Sqrt(da * db);
    }
}
}
