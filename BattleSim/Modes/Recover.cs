using BattleCore;
using static Common;

// =====================================================================================
// recover モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "recover")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 recover
// =====================================================================================

static class RecoverDiag
{
// recover モード（第101期）—— 境界に回復を置く（会戦を測れる帯へ持っていく）
//
// 起点は第100期。**順路(5波) 突破率 0.00% / 地点(3波) 0.97%** で 72/73 行が 0% に並び、
// **突破波数の 37% が同値塊**になって順位が付かなかった。落ちた試行の **66.2% が第2波**で、
// **会戦の第2波クリア率 33.8% 対 単発 66.7%**——**持ち越しがちょうど半分にしている。**
//
// 触るのは**会戦の境界の規則1つ**（`RecoverRule`）だけ。駒・特性・数値・engine の判定・
// `Presets` / `CompareBuilds()` / `CrossBuilds()` / `Stages` / `Columns` は 1 行も触っていない。
//
// **この期は測って並べるところまで。既定は R0（`RecoverRule.Default`）のまま。採否はポン。**
//
//     dotnet run --project BattleSim -c Release 0 recover phase0   # 表A（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 recover tables   # 表A〜H を通しで（既定）
public static void Run(string[] args, int stageIndex)
{
    string rcSub = args.Length > 2 ? args[2] : "tables";
    const int RcSeeds = 200;

    // ---- 判定の線（**測る前に固定する**。ここを結果を見てから緩めない・第64期） ----
    const double Q1MedianLo = 5.0, Q1MedianHi = 50.0;   // 73行の突破率の中央値がこの帯に入ること
    const int Q1ZeroMax = 37;                            // 突破率 0% の行がこれを**下回る**こと
    const double Q3RhoLine = 0.95;                       // これ以上なら「単発と一致した」と読む
    const double Q3GapLine = 0.05;                       // かつ RFull の ρ より この幅 下回ること

    var rcRows = CompareBuilds().Concat(CrossBuilds()).ToArray();

    // 列は**実装から引く**（第100期と同じ作法。名前で決め打ちしない）。
    EnemyCatalog.Column rcCol5 = EnemyCatalog.Columns
        .First(c => ReferenceEquals(c.Squads, EnemyCatalog.EngagementColumn));
    EnemyCatalog.Column rcCol3 = EnemyCatalog.Columns
        .Where(c => c.Squads.Count == 3).OrderBy(c => c.Name).First();

    var rcVers = new (string Tag, RecoverRule Rule, string Note)[]
    {
        ("R0",    new RecoverRule(0,   false), "現行（`RecoverRule.Default`）"),
        ("R25",   new RecoverRule(25,  false), ""),
        ("R50",   new RecoverRule(50,  false), ""),
        ("R100",  new RecoverRule(100, false), "**代金を HP から駒へ移す**（HP は戻るが死者は戻らない）"),
        ("RFull", new RecoverRule(100, true),  "**対照。** 各波が単発と同じ条件に近づく"),
    };

    // ---- 1行ぶんの会戦（第100期 `run` の RunEngage の写し ＋ 版の引数） ----
    (int Win, int[] FellAt, double[] AliveSum, double[] HpSum, int[] Reached,
     double SurvSum, int Blow, int Narrow, int Party)
        RcEngage(Formation f, EnemyCatalog.Column col, RecoverRule rule)
    {
        int n = col.Squads.Count;
        var playerColumn = new[] { f };
        // HP割合の分母は**編成全体の定義上総最大HP**（不変値）。engage / run と同じ判断。
        int defTotal = f.Occupied().Sum(x => x.Def.MaxHp);
        int party = f.Occupied().Count();

        int win = 0, blow = 0, narrow = 0;
        double survSum = 0;
        var fellAt = new int[n + 2];
        var aliveSum = new double[n];
        var hpSum = new double[n];
        var reached = new int[n];

        for (int seed = 0; seed < RcSeeds; seed++)
        {
            EngagementResult r = EngagementEngine.Run(playerColumn, col.Squads, seed,
                                                      verbose: false, recover: rule);
            int battles = r.Battles.Count;
            if (r.PlayerWon)
            {
                win++;
                BattleResult last = r.Battles[battles - 1];
                survSum += last.PlayerSurvivors;
                if (last.PlayerSurvivors >= 4) blow++;
                if (last.PlayerSurvivors <= 1) narrow++;
            }
            else fellAt[Math.Min(battles, n + 1)]++;

            for (int b = 0; b < r.PlayerEntries.Count && b < n; b++)
            {
                aliveSum[b] += r.PlayerEntries[b].Alive;
                hpSum[b] += (double)r.PlayerEntries[b].HpSum / defTotal;
                reached[b]++;
            }
        }
        return (win, fellAt, aliveSum, hpSum, reached, survSum, blow, narrow, party);
    }

    // ---- 単発の波ごとの勝率（連勝率の材料。境界を1度も通らない） ----
    double[] RcSolo(Formation f, int waves)
    {
        var rate = new double[waves];
        for (int w = 0; w < waves; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < RcSeeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
            rate[w] = wins * 100.0 / RcSeeds;
        }
        return rate;
    }

    // ---- スピアマンの順位相関（同値は平均順位） ----
    static double[] RcRankAvg(double[] v)
    {
        int n = v.Length;
        var idx = Enumerable.Range(0, n).OrderBy(i => v[i]).ToArray();
        var rank = new double[n];
        for (int i = 0; i < n;)
        {
            int j = i;
            while (j + 1 < n && v[idx[j + 1]] == v[idx[i]]) j++;
            double r = (i + j) / 2.0 + 1;
            for (int k = i; k <= j; k++) rank[idx[k]] = r;
            i = j + 1;
        }
        return rank;
    }
    static double RcSpearman(double[] a, double[] b)
    {
        double[] ra = RcRankAvg(a), rb = RcRankAvg(b);
        double ma = ra.Average(), mb = rb.Average();
        double num = 0, da = 0, db = 0;
        for (int i = 0; i < ra.Length; i++)
        { num += (ra[i] - ma) * (rb[i] - mb); da += (ra[i] - ma) * (ra[i] - ma); db += (rb[i] - mb) * (rb[i] - mb); }
        return da == 0 || db == 0 ? 0 : num / Math.Sqrt(da * db);
    }
    static double RcPearson(double[] a, double[] b)
    {
        double ma = a.Average(), mb = b.Average();
        double num = 0, da = 0, db = 0;
        for (int i = 0; i < a.Length; i++)
        { num += (a[i] - ma) * (b[i] - mb); da += (a[i] - ma) * (a[i] - ma); db += (b[i] - mb) * (b[i] - mb); }
        return da == 0 || db == 0 ? 0 : num / Math.Sqrt(da * db);
    }

    // ---------------- 表A（Phase 0・戦闘0回） ----------------
    void RcEmitA()
    {
        Console.WriteLine("## 表A —— Phase 0（**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("### (1) ノブの定義（実装から引いた）");
        Console.WriteLine();
        Console.WriteLine("| 版 | `HpPercent` | `ReviveDead` | `Active` | 意味 |");
        Console.WriteLine("|---|--:|:-:|:-:|---|");
        foreach (var v in rcVers)
            Console.WriteLine($"| **{v.Tag}** | {v.Rule.HpPercent} | {(v.Rule.ReviveDead ? "○" : "×")} "
                + $"| {(v.Rule.Active ? "○" : "**×（第100期と同一）**")} | {v.Note} |");
        Console.WriteLine();
        Console.WriteLine($"既定 `RecoverRule.Default` = `({RecoverRule.Default.HpPercent}, "
            + $"{RecoverRule.Default.ReviveDead.ToString().ToLowerInvariant()})` "
            + $"→ `Active` = **{RecoverRule.Default.Active}**。**測定中も既定は R0 のまま動かしていない。**");
        Console.WriteLine();
        Console.WriteLine("### (2) 境界の窓口（`EngagementEngine.CarryOver`）");
        Console.WriteLine();
        Console.WriteLine("既存の段（`StatusKeys.All` の削除・`ResetAtkBonus`・`WhetReceived = 0`・");
        Console.WriteLine("`ActionIndex = 0`・`OnCarryOver`）には**1文字も触っていない**。回復は**その後ろ**に足した。");
        Console.WriteLine("`OrderBy(u => u.Slot)` の並べ替えも動かしていない（次の `Run` の `Add` 順＝`InstanceId` の");
        Console.WriteLine("振り順を決定的に保つため）。");
        Console.WriteLine();
        Console.WriteLine("- **`ctx.Heal` は通していない。** 境界は戦闘の外で、渇き（第三波の波ルール）も");
        Console.WriteLine("  `AcceptsSupport`（支援拒否）も「戦闘中に味方が配るもの」に対する規則である。");
        Console.WriteLine("  境界の手当ては誰かが配っているのではないので、**`Hp` を直接足す**（理由はコード側にも書いた）。");
        Console.WriteLine("- **`ReviveDead` の対象はその戦闘へ投入した駒だけ**（`current`）。");
        Console.WriteLine("  **戦闘中に湧いた駒（胞子・亡者）はこのリストに入らないので復帰しない**（儚い駒は持ち越さない）。");
        Console.WriteLine("- **復帰する駒のスロットは元のまま。** 境界で再配置しないのが現行の規則。");
        Console.WriteLine("- **敵側には一切適用しない。** 味方が敵部隊を抜くたび敵は");
        Console.WriteLine("  `BattleEngine.Materialize` で**新品**が投入されるので元から全快である");
        Console.WriteLine("  （`CarryOver(aliveE)` は規則を渡していない＝既定のまま。コードで確認できる）。");
        Console.WriteLine();
        Console.WriteLine("### (3) 測る列（実装から引いた。名前で決め打ちしない）");
        Console.WriteLine();
        bool prefix = rcCol3.Squads.Count <= rcCol5.Squads.Count
            && rcCol3.Squads.Select((sq, i) => ReferenceEquals(sq, rcCol5.Squads[i])).All(x => x);
        Console.WriteLine($"**3波の列 = 「{rcCol3.Name}」**（{rcCol3.Squads.Count}波）で回す。"
            + $"5波の列「{rcCol5.Name}」の**先頭3つと参照同一**か: **{(prefix ? "○" : "×")}**"
            + "——第100期の「5波の列は3波の列に『誰も届かない2波』を足しただけ」の前提。");
        Console.WriteLine();
        int dupes = rcRows.GroupBy(r => r.Name).Count(g => g.Count() > 1);
        Console.WriteLine($"行は `Presets.Compare` {CompareBuilds().Length} ＋ `Presets.Cross` {CrossBuilds().Length} "
            + $"= **{rcRows.Length}行**（重複 {dupes}件）× seed 0..{RcSeeds - 1} × **5版** "
            + $"= **{rcRows.Length * RcSeeds * rcVers.Length:N0} 試行**。");
        Console.WriteLine();
        Console.WriteLine("### (4) 規約 (G10)（**測定より先にコミットしてある**）");
        Console.WriteLine();
        Console.WriteLine("> **会戦・単発とも、判定に使う分母は第2〜5波（地点なら第2〜3波）とする。第一波を分母に入れない。**");
        Console.WriteLine();
        Console.WriteLine("地点の会戦では**第一波で落ちる試行が 0**（第100期の実測）なので、");
        Console.WriteLine("`突破率` は定義上「第2〜3波を抜けた割合」であり、そのまま (G10) を満たす。");
        Console.WriteLine("単発の連勝率も**第一波が全行 100.0%** なら 第1〜3波の積 ＝ 第2〜3波の積 で一致する");
        Console.WriteLine("——**一致するかは実測で確かめる**（表F の脚注）。");
        Console.WriteLine();
    }

    if (rcSub == "phase0")
    {
        Console.WriteLine("# 第101期 —— 境界に回復を置く（`recover phase0`）");
        Console.WriteLine();
        RcEmitA();
        return;
    }

    // ---------------- 測定 ----------------
    Console.WriteLine("# 第101期 —— 境界に回復を置く（会戦を測れる帯へ持っていく）");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 recover {rcSub}` の出力。");
    Console.WriteLine("**触ったのは会戦の境界の規則 1 つ（`RecoverRule`）だけ。** 駒・特性・数値・engine の判定・");
    Console.WriteLine("`Presets` / `CompareBuilds()` / `CrossBuilds()` / `Stages` / `Columns` は 1 行も触っていない。");
    Console.WriteLine("**既定は R0 のまま。この期は測って並べるところまでで、採否は決めない。**");
    Console.WriteLine("**`docs/` には置かない。**");
    Console.WriteLine();
    RcEmitA();
    Console.Out.Flush();

    int nw3 = rcCol3.Squads.Count;
    int rcCells = rcRows.Length * RcSeeds;

    // 単発（境界を通らない）。連勝率の材料。
    var soloRate = new double[rcRows.Length][];
    for (int i = 0; i < rcRows.Length; i++) soloRate[i] = RcSolo(rcRows[i].F, nw3);
    var chainRate = new double[rcRows.Length];      // 第1〜3波の積
    var chainTail = new double[rcRows.Length];      // 第2〜3波の積（(G10)）
    for (int i = 0; i < rcRows.Length; i++)
    {
        double p1 = 1, p2 = 1;
        for (int w = 0; w < nw3; w++) { p1 *= soloRate[i][w] / 100.0; if (w > 0) p2 *= soloRate[i][w] / 100.0; }
        chainRate[i] = p1 * 100; chainTail[i] = p2 * 100;
    }

    // 版ごとの計測
    var rate = new double[rcVers.Length][];          // 行ごとの突破率
    var fell = new int[rcVers.Length][];
    var entAlive = new double[rcVers.Length][];
    var entHp = new double[rcVers.Length][];
    var entReach = new int[rcVers.Length][];
    var vWins = new int[rcVers.Length];
    var vSurv = new double[rcVers.Length];
    var vBlow = new int[rcVers.Length];
    var vNarrow = new int[rcVers.Length];
    var vBroke = new int[rcVers.Length];             // 1試行でも抜けた行
    var rowSurv = new double[rcVers.Length][];       // 行ごとの残存（抜けた試行のみ）

    for (int v = 0; v < rcVers.Length; v++)
    {
        rate[v] = new double[rcRows.Length];
        rowSurv[v] = new double[rcRows.Length];
        fell[v] = new int[nw3 + 2];
        entAlive[v] = new double[nw3];
        entHp[v] = new double[nw3];
        entReach[v] = new int[nw3];
        for (int i = 0; i < rcRows.Length; i++)
        {
            var m = RcEngage(rcRows[i].F, rcCol3, rcVers[v].Rule);
            rate[v][i] = m.Win * 100.0 / RcSeeds;
            rowSurv[v][i] = m.Win > 0 ? m.SurvSum / m.Win : 0;
            if (m.Win > 0) vBroke[v]++;
            for (int k = 0; k < fell[v].Length && k < m.FellAt.Length; k++) fell[v][k] += m.FellAt[k];
            for (int b = 0; b < nw3; b++)
            { entAlive[v][b] += m.AliveSum[b]; entHp[v][b] += m.HpSum[b]; entReach[v][b] += m.Reached[b]; }
            vWins[v] += m.Win; vSurv[v] += m.SurvSum; vBlow[v] += m.Blow; vNarrow[v] += m.Narrow;
        }
        Console.Error.WriteLine($"[{rcVers[v].Tag}] done");
    }

    static double RcMedian(double[] v)
    {
        var s = v.OrderBy(x => x).ToArray();
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2.0;
    }

    // ---------------- 表B ----------------
    Console.WriteLine($"## 表B —— 版ごとの集計（「{rcCol3.Name}」{nw3}波 × 73行 × seed 0..{RcSeeds - 1}）");
    Console.WriteLine();
    Console.WriteLine("`突破率` はその列を最後まで抜けた試行の割合（全 14,600 試行）。");
    Console.WriteLine("`中央値` は**行ごとの突破率**の中央値（73行）。`0%の行` は 1 試行も抜けなかった行の数。");
    Console.WriteLine("`第2波クリア率` は「第2波を抜けた試行 ÷ 全試行」——**第1波で落ちる試行は 0** なので分母は全試行。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 突破率 | 中央値 | 0%の行 | 100%の行 | 抜けた行 | 落ちた波(第1/第2/第3) | 第2波クリア率 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|---|--:|");
    for (int v = 0; v < rcVers.Length; v++)
    {
        int zero = rate[v].Count(x => x == 0), full = rate[v].Count(x => x >= 100);
        double clear2 = (rcCells - fell[v][1] - fell[v][2]) * 100.0 / rcCells;
        Console.WriteLine($"| **{rcVers[v].Tag}** | {vWins[v] * 100.0 / rcCells:F2}% | {RcMedian(rate[v]):F1}% "
            + $"| {zero} | {full} | {vBroke[v]} | "
            + string.Join(" / ", Enumerable.Range(1, nw3).Select(i => $"{fell[v][i]}")) + $" | {clear2:F1}% |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表C ----------------
    Console.WriteLine("## 表C —— 行ごとの突破率（73行 × 5版）");
    Console.WriteLine();
    Console.WriteLine("`単発 連勝率` は**境界を1度も通らない**単発戦の第1〜3波の勝率の積（(G10) の第2〜3波の積も併記）。");
    Console.WriteLine("`RFull` はその連勝率に一致するはずの対照（受け入れ3）。並びは R100 の降順。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 単発 連勝率(1-3) | 連勝率(2-3) | " + string.Join(" | ", rcVers.Select(x => x.Tag)) + " |");
    Console.WriteLine("|---|--:|--:|" + string.Concat(rcVers.Select(_ => "--:|")));
    var ordC = Enumerable.Range(0, rcRows.Length).OrderByDescending(i => rate[3][i]).ThenByDescending(i => rate[2][i]).ToArray();
    foreach (int i in ordC)
        Console.WriteLine($"| {rcRows[i].Name} | {chainRate[i]:F1}% | {chainTail[i]:F1}% | "
            + string.Join(" | ", Enumerable.Range(0, rcVers.Length).Select(v => $"{rate[v][i]:F1}%")) + " |");
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表D ----------------
    Console.WriteLine("## 表D —— 入場戦力（版ごと・全73行の平均）");
    Console.WriteLine();
    Console.WriteLine("各波の開始時点の生存数と HP 合計の割合（**分母は編成全体の定義上総最大HP**・不変値）。");
    Console.WriteLine("到達しなかった試行は分母から外し、到達率を括弧で併記する。");
    Console.WriteLine();
    Console.WriteLine("| 版 |" + string.Concat(Enumerable.Range(0, nw3).Select(b => $" 第{b + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw3).Select(_ => "---|")));
    for (int v = 0; v < rcVers.Length; v++)
        Console.WriteLine($"| **{rcVers[v].Tag}** |" + string.Concat(Enumerable.Range(0, nw3).Select(b =>
            entReach[v][b] == 0 ? " — |"
            : $" {entAlive[v][b] / entReach[v][b]:F2}体 {entHp[v][b] * 100 / entReach[v][b]:F0}%"
              + $" (到達 {entReach[v][b] * 100.0 / rcCells:F0}%) |")));
    Console.WriteLine();

    // ---------------- 表E ----------------
    Console.WriteLine("## 表E —— 勝ち方の質（抜けた試行の上でだけ定義される）");
    Console.WriteLine();
    Console.WriteLine("**最後の波（第3波）を勝ったときの**生存数で測る（第100期 `run` の定義の写し）。");
    Console.WriteLine("`圧勝率` = 生存4体以上 ／ `全滅勝ち` = 生存1体以下。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 抜けた試行 | 残存 | 圧勝率 | 全滅勝ち |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int v = 0; v < rcVers.Length; v++)
        Console.WriteLine($"| **{rcVers[v].Tag}** | {vWins[v]} | "
            + (vWins[v] == 0 ? "— | — | — |"
               : $"{vSurv[v] / vWins[v]:F2} | {vBlow[v] * 100.0 / vWins[v]:F0}% | {vNarrow[v] * 100.0 / vWins[v]:F0}% |"));
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表F（判定） ----------------
    double rhoFull = RcSpearman(rate[rcVers.Length - 1], chainRate);
    Console.WriteLine("## 表F —— 判定 Q1〜Q3（**線は測る前に固定してある**）");
    Console.WriteLine();
    Console.WriteLine($"- **Q1（帯）**: 73行の突破率の**中央値が {Q1MedianLo:F0}〜{Q1MedianHi:F0}%** に入り、"
        + $"かつ**突破率 0% の行が {Q1ZeroMax} 行を下回る**こと");
    Console.WriteLine("- **Q2（泥仕合）**: **圧勝率が R0 より下がらない**こと");
    Console.WriteLine($"- **Q3（独自性）**: 行の順位が単発の連勝率と**一致しない**こと"
        + $"——スピアマン **ρ < {Q3RhoLine:F2}** かつ **RFull の ρ より {Q3GapLine:F2} 以上低い**こと");
    Console.WriteLine();
    Console.WriteLine("| 版 | Q1 中央値 | Q1 0%の行 | Q1 | Q2 圧勝率 | Q2 | Q3 ρ(単発連勝率) | Q3 | **総合** |");
    Console.WriteLine("|---|--:|--:|:-:|--:|:-:|--:|:-:|:-:|");
    double blow0 = vWins[0] == 0 ? 0 : vBlow[0] * 100.0 / vWins[0];
    var pass = new bool[rcVers.Length];
    for (int v = 0; v < rcVers.Length; v++)
    {
        double med = RcMedian(rate[v]);
        int zero = rate[v].Count(x => x == 0);
        bool q1 = med >= Q1MedianLo && med <= Q1MedianHi && zero < Q1ZeroMax;
        double blow = vWins[v] == 0 ? 0 : vBlow[v] * 100.0 / vWins[v];
        bool q2 = blow >= blow0;
        double rho = RcSpearman(rate[v], chainRate);
        bool q3 = rho < Q3RhoLine && rho <= rhoFull - Q3GapLine;
        pass[v] = q1 && q2 && q3;
        Console.WriteLine($"| **{rcVers[v].Tag}** | {med:F1}% | {zero} | {(q1 ? "○" : "×")} "
            + $"| {(vWins[v] == 0 ? "—" : blow.ToString("F0") + "%")} | {(q2 ? "○" : "×")} "
            + $"| {rho:F3} | {(q3 ? "○" : "×")} | {(pass[v] ? "**○**" : "×")} |");
    }
    Console.WriteLine();
    var winners = Enumerable.Range(0, rcVers.Length).Where(v => pass[v] && rcVers[v].Tag != "RFull").ToArray();
    Console.WriteLine(winners.Length == 0
        ? "**Q1〜Q3 を同時に満たす版は無い。**（`RFull` は対照であって候補ではないので、通っても推奨にしない。）"
        : "**推奨（Q1〜Q3 を同時に満たす版）: " + string.Join(" / ", winners.Select(v => rcVers[v].Tag))
          + "**（`RFull` は対照なので候補から外してある）");
    Console.WriteLine();
    Console.WriteLine("**脚注（(G10)）**: 単発の第一波の勝率が全行 100.0% なら 第1〜3波の積 ＝ 第2〜3波の積。"
        + $"実測で **第一波が 100.0% でない行 {Enumerable.Range(0, rcRows.Length).Count(i => soloRate[i][0] < 100):D} / {rcRows.Length}**、"
        + $"2つの積の ρ = **{RcSpearman(chainRate, chainTail):F3}**。");
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表G（§3-2 あわせて見るもの・判定ではない） ----------------
    Console.WriteLine("## 表G —— あわせて見るもの（**判定ではない**）");
    Console.WriteLine();
    Console.WriteLine("### G-1. R100 の突破率は「勝率」と「残存」のどちらと相関するか");
    Console.WriteLine();
    Console.WriteLine("**代金が HP から駒へ移っているなら、残存との相関が強く出るはず。**");
    Console.WriteLine("`単発 残存(2-3波)` は単発戦の第2〜3波を勝った試行の平均生存数（(G10)）。");
    Console.WriteLine();
    // 単発の残存（第2〜3波）
    var soloSurv = new double[rcRows.Length];
    for (int i = 0; i < rcRows.Length; i++)
    {
        int w2 = 0; double s2 = 0;
        for (int w = 1; w < nw3; w++)
            for (int seed = 0; seed < RcSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(rcRows[i].F, EnemyCatalog.Stages[w].Enemy, seed, verbose: false);
                if (!r.PlayerWon) continue;
                w2++; s2 += r.PlayerSurvivors;
            }
        soloSurv[i] = w2 > 0 ? s2 / w2 : 0;
    }
    Console.WriteLine("| 版 | ρ(突破率, 単発連勝率) | ρ(突破率, 単発残存(2-3波)) | r(ピアソン・連勝率) | r(ピアソン・残存) |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int v = 0; v < rcVers.Length; v++)
        Console.WriteLine($"| **{rcVers[v].Tag}** | {RcSpearman(rate[v], chainRate):F3} | {RcSpearman(rate[v], soloSurv):F3} "
            + $"| {RcPearson(rate[v], chainRate):F3} | {RcPearson(rate[v], soloSurv):F3} |");
    Console.WriteLine();

    Console.WriteLine("### G-2. 第2波クリア率は単発の 66.7% に届くか");
    Console.WriteLine();
    double solo2 = Enumerable.Range(0, rcRows.Length).Average(i => soloRate[i][1]);
    Console.WriteLine($"**単発（フルHP）の第2波の勝率は 73行平均で {solo2:F1}%。**");
    Console.WriteLine();
    Console.WriteLine("| 版 | 会戦の第2波クリア率 | 単発との差 |");
    Console.WriteLine("|---|--:|--:|");
    for (int v = 0; v < rcVers.Length; v++)
    {
        double clear2 = (rcCells - fell[v][1] - fell[v][2]) * 100.0 / rcCells;
        Console.WriteLine($"| **{rcVers[v].Tag}** | {clear2:F1}% | {clear2 - solo2:+0.0;-0.0}pt |");
    }
    Console.WriteLine();
    Console.WriteLine("**第2波は会戦の初戦（第1波）を経てから入る波なので、`R0` の差がそのまま「持ち越しの代金」。**");
    Console.WriteLine();

    Console.WriteLine("### G-3. 育つ駒は会戦で二重に不利のままか");
    Console.WriteLine();
    Console.WriteLine("**`AtkBonus` は境界で全消しのまま（触っていない）。** 回復を入れてもここは解消しないはず。");
    Console.WriteLine("対象は**駒で引く**（行名ではない）: 泥人形ムド（被弾強化）・棘鎧のカド（棘）・");
    Console.WriteLine("軋みのヨミ（移動で育つ）・墓守のリィカ（層）。");
    Console.WriteLine();
    var growIds = new[] { UnitCatalog.Mudo.Id, UnitCatalog.Kado.Id, UnitCatalog.Yomi.Id, UnitCatalog.Rica.Id };
    var isGrow = rcRows.Select(r => r.F.Occupied().Any(u => growIds.Contains(u.Def.Id))).ToArray();
    int ng = isGrow.Count(x => x);
    Console.WriteLine($"育つ駒を含む行 **{ng} / {rcRows.Length}**。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 含む行の突破率 | 含まない行の突破率 | 差 | 含む行の単発連勝率 | 含まない行 | 差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    double gc = Enumerable.Range(0, rcRows.Length).Where(i => isGrow[i]).Average(i => chainRate[i]);
    double oc = Enumerable.Range(0, rcRows.Length).Where(i => !isGrow[i]).Average(i => chainRate[i]);
    for (int v = 0; v < rcVers.Length; v++)
    {
        double g = Enumerable.Range(0, rcRows.Length).Where(i => isGrow[i]).Average(i => rate[v][i]);
        double o = Enumerable.Range(0, rcRows.Length).Where(i => !isGrow[i]).Average(i => rate[v][i]);
        Console.WriteLine($"| **{rcVers[v].Tag}** | {g:F1}% | {o:F1}% | {g - o:+0.0;-0.0}pt "
            + $"| {gc:F1}% | {oc:F1}% | {gc - oc:+0.0;-0.0}pt |");
    }
    Console.WriteLine();
    Console.WriteLine("**単発の連勝率の差（右3列・版に依らない定数）と会戦の差を比べる**"
        + "——会戦の差のほうが負に大きければ「会戦で二重に不利」が残っている。");
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表H（自己検査） ----------------
    Console.WriteLine("## 表H —— 受け入れ（自己検査）");
    Console.WriteLine();

    // 受け入れ2: R0 が第100期の実測と一致するか（値は第100期の報告から）
    const int P100Fell2 = 9669, P100Fell3 = 4790, P100Broke = 141, P100Rows = 1;
    bool acc2 = fell[0][2] == P100Fell2 && fell[0][3] == P100Fell3
        && vWins[0] == P100Broke && vBroke[0] == P100Rows;
    Console.WriteLine("### 2. `R0` が第100期の実測を再現するか");
    Console.WriteLine();
    Console.WriteLine("**既知の値を再現できて初めて器具として使える**（第94期の第一版は欠落 105 件のうち 78 件が器具自身の誤り）。");
    Console.WriteLine();
    Console.WriteLine("| 量 | 第100期 | この期の `R0` | 一致 |");
    Console.WriteLine("|---|--:|--:|:-:|");
    Console.WriteLine($"| 突破した試行 | {P100Broke} | {vWins[0]} | {(vWins[0] == P100Broke ? "○" : "×")} |");
    Console.WriteLine($"| 突破率 | 0.97% | {vWins[0] * 100.0 / rcCells:F2}% | {(vWins[0] == P100Broke ? "○" : "×")} |");
    Console.WriteLine($"| 1試行でも抜けた行 | {P100Rows} | {vBroke[0]} | {(vBroke[0] == P100Rows ? "○" : "×")} |");
    Console.WriteLine($"| 第2波で落ちた試行 | {P100Fell2} | {fell[0][2]} | {(fell[0][2] == P100Fell2 ? "○" : "×")} |");
    Console.WriteLine($"| 第3波で落ちた試行 | {P100Fell3} | {fell[0][3]} | {(fell[0][3] == P100Fell3 ? "○" : "×")} |");
    Console.WriteLine();
    Console.WriteLine($"→ **{(acc2 ? "○ 再現した" : "× 再現しない")}**"
        + "（`RecoverRule.Default` は `Active` = false なので `CarryOver` は第100期と同一の経路を通る）。");
    Console.WriteLine();

    // 受け入れ3: RFull が単発の連勝率と一致するか
    int nv = rcVers.Length - 1;
    var diff = Enumerable.Range(0, rcRows.Length).Select(i => rate[nv][i] - chainRate[i]).ToArray();
    int near = diff.Count(d => Math.Abs(d) <= 5.0);
    Console.WriteLine("### 3. `RFull` の突破率が単発の連勝率と一致するか");
    Console.WriteLine();
    Console.WriteLine($"ρ(RFull, 単発連勝率) = **{rhoFull:F3}** ／ ピアソン r = **{RcPearson(rate[nv], chainRate):F3}** ／ "
        + $"平均差 **{diff.Average():+0.00;-0.00}pt** ／ 最大差 **{diff.Max(Math.Abs):F1}pt** ／ "
        + $"|差| ≤ 5.0pt の行 **{near} / {rcRows.Length}**");
    Console.WriteLine();
    Console.WriteLine("**厳密に一致するはずが無い構造上の理由**（切り分けのために先に列挙する。どれも境界の既存の段で、");
    Console.WriteLine("この期は 1 文字も触っていない）:");
    Console.WriteLine();
    Console.WriteLine("1. **スロットが持ち越される**（境界で再配置しない）——戦闘中に動いた駒は次の波を別の席で始める");
    Console.WriteLine("2. **`HasFallenBack` が持ち越される**（判断 D6）——後衛特化が第2波の開幕から立つ");
    Console.WriteLine("3. **`MaxHp` の損耗が持ち越される**（継ぎ接ぎのヴェルは 46 → 11 まで縮む）——`RFull` は縮んだ `MaxHp` まで戻す");
    Console.WriteLine("4. **`Trait.OnCarryOver` が恒久分を再構成する**（墓守の層など）");
    Console.WriteLine("5. **蘇生回数などの特性私有カウンタが持ち越される**（`Counters` は境界で `StatusKeys` しか消さない）");
    Console.WriteLine();
    var driverIds = new[] { UnitCatalog.Sero.Id, UnitCatalog.Kado.Id, UnitCatalog.Basa.Id,
                            UnitCatalog.Vel.Id, UnitCatalog.Rica.Id };
    var hasDriver = rcRows.Select(r => r.F.Occupied().Any(u => driverIds.Contains(u.Def.Id))).ToArray();
    var big = Enumerable.Range(0, rcRows.Length).Where(i => Math.Abs(diff[i]) > 5.0).ToArray();
    Console.WriteLine($"|差| > 5.0pt の行 **{big.Length}**。うち上の 1〜5 の駆動因（移動＝逃亡セロ・棘守りカド・喧噪バサ／");
    Console.WriteLine($"継ぎ接ぎヴェル／墓守リィカ）を含む行 **{big.Count(i => hasDriver[i])}**"
        + $"（全73行では {hasDriver.Count(x => x)} 行）。");
    if (big.Length > 0)
    {
        Console.WriteLine();
        Console.WriteLine("| 編成 | 単発連勝率 | RFull | 差 | 駆動因 |");
        Console.WriteLine("|---|--:|--:|--:|:-:|");
        foreach (int i in big.OrderByDescending(i => Math.Abs(diff[i])).Take(20))
            Console.WriteLine($"| {rcRows[i].Name} | {chainRate[i]:F1}% | {rate[nv][i]:F1}% | {diff[i]:+0.0;-0.0}pt "
                + $"| {(hasDriver[i] ? "○" : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine("**切り分け**: `RFull` と単発の差が上の 5 つで説明でき、かつ `R0` が第100期を再現している（受け入れ2）なら、");
    Console.WriteLine("**ノブも器具も間違っていない**（規則は境界の後ろに足しただけで、既存の段は 1 文字も動いていない）。");
    Console.WriteLine();
    Console.WriteLine("### 1・4・5・6（この実行の外で確かめるもの）");
    Console.WriteLine();
    Console.WriteLine("| # | 受け入れ | 確かめ方 |");
    Console.WriteLine("|--:|---|---|");
    Console.WriteLine("| 1 | `compare` 305 セルが `docs/balance.md` と 0 件 | **単発は境界を1度も通らない**"
        + "（`BattleEngine.Run` は `EngagementEngine` を呼ばない）ので原理的に立たない。実測でも確認する |");
    Console.WriteLine("| 4 | `docs/` を再生成して差分を報告 | `docs/engage.md` は既定を変えたときだけ動く。**既定は R0 のまま**なので差分0のはず |");
    Console.WriteLine("| 5 | `ctx.PickOne` が 8 箇所 | `grep -c` で数える（第89期 (h)。この期は 1 つも足していない） |");
    Console.WriteLine("| 6 | 境界で `ctx.Heal` を呼んでいない | `Engagement.cs` に `Heal` が 0 箇所（表A (2)） |");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}
}
