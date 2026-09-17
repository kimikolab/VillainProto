using BattleCore;
using static Common;

// =====================================================================================
// creak モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "creak")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 creak
// =====================================================================================

static class CreakDiag
{
// 軋みが響く（第66期 → **第67期に条件の出どころを差し替えた**）。
// **駒は作らない。ロスターの最後の1枠は使わない。**
//
// 第66期は `AtkBonus` を読んで単体 → 薙ぎにしたが、閾値 9 はヨミ自身の上昇（軋み 9 / 突き出し 22）
// だけで満たされ、**条件ではなく起動スイッチ**になっていた（到達時点の内訳 軋み 21.8 対 窓口 8.0）。
// 第67期は条件を **`UnitState.WhetReceived`（`Whet` 窓口を通って届いた累計）** に差し替える。
// **軋み自身の上昇は条件に入らない**ので、これで初めて「強化の2枚目の読み手」になる
// （第65期 積み残し1': 供給 16 対 読み手 1）。
//
// 自由度は構造的に消してある（第64期の教訓・自己検査 (e)）:
//   行は4本に固定（ヨミを含む全3行 ＋ 実演行 `軋み×吐き戻し`。**測定前の宣言**）／
//   席は動かさない（`reseat` は帰属の器具ではない）／攻撃型は薙ぎで固定（第66期と同じ）／
//   閾値は `UnitTally.CreakWhetProbes` の格子から3点、採り方は §2-2 で固定
//   （Q1 を満たす版のうち帰属が最大・同点は高い方）。
//
//     dotnet run --project BattleSim -c Release 0 creak         # 主表（A 帯 seed 0..199）
//     dotnet run --project BattleSim -c Release 0 creak phase0  # Phase 0（V0 の `WhetReceived` の分布）
//     dotnet run --project BattleSim -c Release 0 creak alt     # 再現帯（seed 200..599）
public static void Run(string[] args, int stageIndex)
{
    string ckArg = args.Length > 2 ? args[2] : "";
    bool ckAlt = ckArg == "alt";
    int ckSeed0 = ckAlt ? 200 : 0;
    int ckSeedN = ckAlt ? 400 : 200;
    IReadOnlyList<EnemyCatalog.Stage> ckStages = EnemyCatalog.Stages;
    var ckAll = CompareBuilds().ToArray();

    bool CkHasYomi((string Name, Formation F) row)
        => row.F.Occupied().Any(o => o.Item2.Id == UnitCatalog.Yomi.Id);

    var ckRows = ckAll.Where(CkHasYomi).ToArray();
    var ckOther = ckAll.Where(b => !CkHasYomi(b)).ToArray();

    // 実演行（第66期 §1-5）。**第67期は「供給がある台」が主題なので採否にも使う**（指示書 §2-3・測定前の宣言）。
    // 配置は手で固定し `reseat` しない。巨躯は `DepthOf(wall) < DepthOf(target)`＝**ゴルムより後ろ**を庇うので、
    // 吐き戻しを受けるにはヨミが後ろでなければならない（第66期の前提の訂正）。
    var ckDemo = ("軋み×吐き戻し (ヨミ×ゴルム)",
        Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald, center: UnitCatalog.Basa,
                        back1: UnitCatalog.Yomi, back3: UnitCatalog.Sero));

    // Q3ダッシュの器具（指示書 §1-4）。**ゴルムだけを同数値・特性なしの素体に落とす。他の4枚と席は動かさない。**
    // 規則にノブを増やさず駒の側で塞ぐ（第47期・`gradient` / `aim` と同じ扱い）。
    var ckPlainGolm = new UnitDef
    {
        Id = "golm_plain", Name = "素体（ゴルム同数値）",
        MaxHp = 150, Attack = 10, Speed = 3, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = ""
    };
    var ckDemoPlain = ("軋み×吐き戻し（ゴルム素体）",
        Formation.Build(front1: ckPlainGolm, front3: UnitCatalog.Gald, center: UnitCatalog.Basa,
                        back1: UnitCatalog.Yomi, back3: UnitCatalog.Sero));

    // 版と閾値（§2-2・**測る前に固定**）。Phase 0-3 の分布から `CreakWhetProbes` の格子上で3点を採る。
    int[] ckVer = { 0, 2, 6, 12 };
    string[] ckVerName = { "V0", "Va", "Vb", "Vc" };

    // 1行ぶんの器。**勝率と計数を同じ走査で取る。**
    (double[] Win, double Sweeps, double Swings, double WhetMax,
     double SelfGain, double WhetGain, double RegurgGain,
     double[] ProbeT, double[] ProbeN)
    CkRun(Formation f, int threshold)
    {
        int np = UnitTally.CreakWhetProbes.Length;
        var win = new double[ckStages.Count];
        double sweeps = 0, swings = 0, wmax = 0, sg = 0, wg = 0, rg = 0;
        var pt = new double[np]; var pn = new double[np];

        for (int w = 0; w < ckStages.Count; w++)
        {
            int wins = 0;
            for (int seed = ckSeed0; seed < ckSeed0 + ckSeedN; seed++)
            {
                BattleResult r = BattleEngine.Run(f, ckStages[w].Enemy, seed, verbose: false,
                                                  creak: new CreakRule(threshold));
                if (r.PlayerWon) wins++;
                if (!r.TallyByUnit.TryGetValue(UnitCatalog.Yomi.Id, out UnitTally? t)) continue;
                sweeps += t.CreakSweeps; swings += t.CreakSwings; wmax += t.CreakWhetMax;
                sg += t.CreakSelfGain; wg += t.CreakWhetGain; rg += t.CreakRegurgGain;
                if (t.CreakWhetProbeTurn is null) continue;
                for (int i = 0; i < np; i++)
                {
                    if (t.CreakWhetProbeTurn[i] == 0) continue;
                    pn[i]++; pt[i] += t.CreakWhetProbeTurn[i];
                }
            }
            win[w] = wins * 100.0 / ckSeedN;
        }
        int n = ckStages.Count * ckSeedN;
        return (win, sweeps / n, swings / n, wmax / n, sg / n, wg / n, rg / n, pt, pn);
    }

    var ckSw = System.Diagnostics.Stopwatch.StartNew();

    // ---- phase0: V0 の `WhetReceived` の分布と、判定式の実効レンジ ----------------
    if (ckArg == "phase0")
    {
        Console.WriteLine("# 軋みが響く（第67期） —— Phase 0（**規則は無効のまま測る**）");
        Console.WriteLine();
        Console.WriteLine($"seed {ckSeed0}..{ckSeed0 + ckSeedN - 1} × 5 波。**盤面は1つも動かない**"
                          + "（`CreakRule` 無効＝`ModifyPattern` が素通り）。");
        Console.WriteLine();
        Console.WriteLine("## 0-3. ヨミの `WhetReceived` の分布（現行・V0）");
        Console.WriteLine();
        Console.WriteLine("`押され` は1戦あたり窓口経由で届いた累計の平均、`うち吐` はそのうち吐き戻し、");
        Console.WriteLine("`軋み` は自前の上昇（**条件に入らない**）。各列は 5 波 × 試行のうちその点へ届いた割合で、");
        Console.WriteLine("次の行が届いた試行だけの平均ターン。**閾値の候補はこの格子から採る。**");
        Console.WriteLine();
        Console.Write("| 行 | 振/戦 | 押され | うち吐 | 軋み |");
        foreach (int q in UnitTally.CreakWhetProbes) Console.Write($" {q} |");
        Console.WriteLine();
        Console.Write("|---|--:|--:|--:|--:|");
        foreach (int _ in UnitTally.CreakWhetProbes) Console.Write("--:|");
        Console.WriteLine();
        double ckDen = ckStages.Count * ckSeedN;
        foreach ((string rn, Formation f) in ckRows.Append(ckDemo).Append(ckDemoPlain))
        {
            var g = CkRun(f, 0);
            Console.Write($"| {rn} | {g.Swings:F2} | {g.WhetGain:F1} | {g.RegurgGain:F1} | {g.SelfGain:F1} |");
            for (int i = 0; i < UnitTally.CreakWhetProbes.Length; i++)
                Console.Write($" {g.ProbeN[i] * 100.0 / ckDen:F1}% |");
            Console.WriteLine();
            Console.Write("|  |  |  |  | 到達T |");
            for (int i = 0; i < UnitTally.CreakWhetProbes.Length; i++)
                Console.Write(g.ProbeN[i] > 0 ? $" {g.ProbeT[i] / g.ProbeN[i]:F2} |" : " — |");
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine("## 0-5. 主判定19行との重なりと、Q6 の実効レンジ（自己検査 (c)）");
        Console.WriteLine();
        var ckPrimary0 = new HashSet<string>(Baseline.PrimaryRows);
        var ckIn0 = ckRows.Where(b => ckPrimary0.Contains(b.Name)).Select(b => b.Name).ToArray();
        Console.WriteLine($"ヨミを含む行は **{ckRows.Length} 行**、うち主判定 {Baseline.PrimaryRows.Length} 行に "
                          + $"**{ckIn0.Length} 行**（{string.Join(" / ", ckIn0)}）。");
        Console.WriteLine();
        double[] ckFifth0 = Baseline.PrimaryRows
            .Select(n => Array.FindIndex(ckAll, b => b.Name == n)).Where(i => i >= 0)
            .Select(i => CkRun(ckAll[i].Item2, 0).Win[4]).ToArray();
        double ckAvg0 = ckFifth0.Average();
        double ckSwing0 = ckIn0.Select(n => CkRun(ckAll[Array.FindIndex(ckAll, b => b.Name == n)].Item2, 0).Win[4]).Sum()
                          / ckFifth0.Length;
        Console.WriteLine($"主判定の第五波平均（現行）= **{ckAvg0:F1}%**、歯止め {Baseline.PrimaryFifthFloor:F1}% との余裕 "
                          + $"**{ckAvg0 - Baseline.PrimaryFifthFloor:F1}pt**。");
        Console.WriteLine($"重なる {ckIn0.Length} 行が第五波で 0.0% まで落ちても平均の低下は最大 **{ckSwing0:F2}pt**"
                          + "——**実効レンジがこの幅**。");
        Console.WriteLine();
        Console.WriteLine("## 0-6. 陰性対照の分母");
        Console.WriteLine();
        Console.WriteLine($"ヨミを含まない **{ckOther.Length} 行**（{ckOther.Length} × 5 波 = **{ckOther.Length * 5} セル**）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {ckSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ---- 主表 -------------------------------------------------------------------
    Console.WriteLine($"# 軋みが響く（第67期） —— 主表{(ckAlt ? "・再現帯" : "")}");
    Console.WriteLine();
    Console.WriteLine($"seed {ckSeed0}..{ckSeed0 + ckSeedN - 1} × 5 波。帰属 = V_n − V0。");
    Console.WriteLine("**席は現行のまま（`reseat` しない）／行は4本に固定（測定前の宣言）。**");
    Console.WriteLine($"閾値は `WhetReceived`（窓口経由の累計）で **{ckVer[1]} / {ckVer[2]} / {ckVer[3]}**。");
    Console.WriteLine();

    var ckMain = ckRows.Append(ckDemo).ToArray();
    var ckBase = new Dictionary<string, double[]>();
    foreach ((string rn, Formation f) in ckMain.Append(ckDemoPlain)) ckBase[rn] = CkRun(f, 0).Win;

    Console.WriteLine("## 1. 行 × 版（波ごとの勝率・平均・帰属・薙ぎ化率）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 帰属 | 薙ぎ化率 | 振/戦 | 押され | 初到達T |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var ckAttr = new Dictionary<string, double[]>();
    var ckRate = new Dictionary<string, double[]>();
    foreach ((string rn, Formation f) in ckMain)
    {
        var attr = new double[ckVer.Length];
        var rate = new double[ckVer.Length];
        for (int v = 0; v < ckVer.Length; v++)
        {
            var g = CkRun(f, ckVer[v]);
            double avg = g.Win.Average();
            attr[v] = avg - ckBase[rn].Average();
            rate[v] = g.Swings > 0 ? g.Sweeps * 100.0 / g.Swings : 0;
            int pi = Array.IndexOf(UnitTally.CreakWhetProbes, ckVer[v]);
            string ft = pi >= 0 && g.ProbeN[pi] > 0 ? (g.ProbeT[pi] / g.ProbeN[pi]).ToString("F2") : "—";
            Console.WriteLine($"| {(v == 0 ? rn : "")} | {ckVerName[v]} | "
                              + string.Join(" | ", g.Win.Select(x => $"{x:F1}%"))
                              + $" | {avg:F1}% | {(v == 0 ? "—" : $"{attr[v]:+0.0;-0.0}")} "
                              + $"| {rate[v]:F1}% | {g.Swings:F2} | {g.WhetGain:F1} | {ft} |");
        }
        ckAttr[rn] = attr; ckRate[rn] = rate;
    }
    Console.WriteLine();

    // ---- Q1 → 閾値の採用 ---------------------------------------------------------
    Console.WriteLine("## 2. Q1（条件は条件か）と閾値の採用");
    Console.WriteLine();
    Console.WriteLine("**採る版は「Q1 を満たす版のうち帰属が最大のもの。同点は高い閾値」**（§2-2・測る前に固定）。");
    Console.WriteLine("Q1 = 採る版で、**4行中3行**の薙ぎ化率が **10% 以上 90% 以下**。**90% 超が1行でもあれば×。**");
    Console.WriteLine();
    Console.WriteLine("| 版 | 閾値 | 薙ぎ化率（4行） | 帯内 | 90%超 | Q1 | 帰属（4行平均） |");
    Console.WriteLine("|---|--:|---|--:|--:|---|--:|");
    int ckPick = -1; double ckPickAttr = double.NegativeInfinity;
    for (int v = 1; v < ckVer.Length; v++)
    {
        var rr = ckMain.Select(b => ckRate[b.Item1][v]).ToArray();
        int inBand = rr.Count(x => x >= 10.0 && x <= 90.0);
        int over = rr.Count(x => x > 90.0);
        bool q1 = inBand >= 3 && over == 0;
        double aa = ckMain.Select(b => ckAttr[b.Item1][v]).Average();
        Console.WriteLine($"| {ckVerName[v]} | {ckVer[v]} | {string.Join(" / ", rr.Select(x => $"{x:F1}%"))} "
                          + $"| {inBand} | {over} | {(q1 ? "○" : "×")} | {aa:+0.0;-0.0} |");
        if (q1 && aa >= ckPickAttr) { ckPickAttr = aa; ckPick = v; }
    }
    Console.WriteLine();
    if (ckPick < 0) Console.WriteLine("**Q1 を満たす版が1つも無い → 採らない（残置）。**");
    else Console.WriteLine($"→ 採る版は **{ckVerName[ckPick]}（Threshold = {ckVer[ckPick]}）**。");
    Console.WriteLine();

    // ---- Q3ダッシュ（主判定・読み手か） ---------------------------------------------
    Console.WriteLine("## 3. Q3'（**主判定**: 外の供給を読んでいるか）");
    Console.WriteLine();
    Console.WriteLine("`軋み×吐き戻し` のゴルムだけを同数値・特性なしの素体に落とす（他の4枚と席は動かさない）。");
    Console.WriteLine("**薙ぎ化率が 1/5 以下に落ちること。**落ちなければ条件は外の供給を読んでいない。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 閾値 | 現行の薙ぎ化率 | 素体の薙ぎ化率 | 比 | 押され（現行→素体） | 1/5 以下 |");
    Console.WriteLine("|---|--:|--:|--:|--:|---|---|");
    for (int v = 1; v < ckVer.Length; v++)
    {
        var gn = CkRun(ckDemo.Item2, ckVer[v]);
        var gp = CkRun(ckDemoPlain.Item2, ckVer[v]);
        double rn2 = gn.Swings > 0 ? gn.Sweeps * 100.0 / gn.Swings : 0;
        double rp = gp.Swings > 0 ? gp.Sweeps * 100.0 / gp.Swings : 0;
        double ratio = rn2 > 0 ? rp / rn2 : 0;
        Console.WriteLine($"| {ckVerName[v]} | {ckVer[v]} | {rn2:F1}% | {rp:F1}% | {ratio:F2} "
                          + $"| {gn.WhetGain:F1} → {gp.WhetGain:F1} | {(rn2 > 0 && ratio <= 0.2 ? "○" : "×")} |");
    }
    Console.WriteLine();

    // ---- Q2 ------------------------------------------------------------------------
    if (ckPick >= 0)
    {
        Console.WriteLine("## 4. Q2（帰属）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帰属 | ≥ +1.5pt | ≥ −3.0pt |");
        Console.WriteLine("|---|--:|---|---|");
        foreach ((string rn, Formation _) in ckMain)
        {
            double a = ckAttr[rn][ckPick];
            Console.WriteLine($"| {rn} | {a:+0.0;-0.0} | {(a >= 1.5 ? "○" : "×")} | {(a >= -3.0 ? "○" : "**×**")} |");
        }
        int okN = ckMain.Count(b => ckAttr[b.Item1][ckPick] >= 1.5);
        bool floorOk = ckMain.All(b => ckAttr[b.Item1][ckPick] >= -3.0);
        Console.WriteLine();
        Console.WriteLine($"**Q2 = {(okN >= 2 && floorOk ? "○" : "×")}**（+1.5pt 以上が {okN} / 4 行"
                          + $"・下限を割った行 {ckMain.Count(b => ckAttr[b.Item1][ckPick] < -3.0)}）。");
        Console.WriteLine();
    }

    // ---- Q4 / Q5 / Q6 / Q7 ---------------------------------------------------------
    Console.WriteLine("## 5. Q4（情報セル）・Q5（陰性対照）・Q6（歯止め）・Q7（絵）");
    Console.WriteLine();
    int InfoCells(double[] w) => w.Skip(1).Count(x => x > 0.0 && x < 100.0);
    Console.WriteLine("| 行 | 情報セル V0 | " + string.Join(" | ", ckVerName.Skip(1).Select(n => $"情報セル {n}")) + " |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach ((string rn, Formation f) in ckMain)
        Console.WriteLine($"| {rn} | {InfoCells(ckBase[rn])} | "
                          + string.Join(" | ", ckVer.Skip(1).Select(t => InfoCells(CkRun(f, t).Win).ToString())) + " |");
    Console.WriteLine();

    int ckDiff = 0;
    foreach ((string rn, Formation f) in ckOther)
    {
        double[] b0 = CkRun(f, 0).Win;
        foreach (int t in ckVer.Skip(1))
        {
            double[] bt = CkRun(f, t).Win;
            for (int w = 0; w < b0.Length; w++) if (Math.Abs(b0[w] - bt[w]) > 1e-9) ckDiff++;
        }
    }
    Console.WriteLine($"**Q5**: ヨミを含まない {ckOther.Length} 行 × 5 波 = {ckOther.Length * 5} セルを"
                      + $"3つの閾値で照合 → **ずれ {ckDiff} 件**。");
    Console.WriteLine();

    var ckPrimIdx = Baseline.PrimaryRows.Select(n => Array.FindIndex(ckAll, b => b.Name == n))
                                        .Where(i => i >= 0).ToArray();
    foreach (int v in (ckPick < 0 ? new[] { 0 } : new[] { 0, ckPick }))
    {
        double avg = ckPrimIdx.Select(i => CkRun(ckAll[i].Item2, ckVer[v]).Win[4]).Average();
        Console.WriteLine($"**Q6**: 主判定 {ckPrimIdx.Length} 行の第五波平均（{ckVerName[v]}）= **{avg:F1}%**"
                          + $"、歯止め {Baseline.PrimaryFifthFloor:F1}% との余裕 **{avg - Baseline.PrimaryFifthFloor:F1}pt**。");
    }
    Console.WriteLine();
    Console.WriteLine("**Q7**（絵）: 初到達Tは §1 の最右列。第66期の 1.55〜2.55 より遅いことが条件。");
    Console.WriteLine();
    Console.WriteLine($"所要 {ckSw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}
}
