using BattleCore;
using static Common;

// =====================================================================================
// layout モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "layout")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 layout
// =====================================================================================

static class LayoutDiag
{
public static void Run(string[] args, int stageIndex)
{
    var builds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> stages = EnemyCatalog.Stages;
    const int LayoutSeeds = 50;
    const int TopN = 5;
    const int VerifySeeds = 200;   // 探索で選んだ配置を測り直すときの試行数。compare と揃える

    // 波別最良の一覧を最後にまとめて出すための控え。[編成, 波] → (現行, 最良)
    var bestByStage = new (double Cur, double Best)[builds.Length, stages.Count];

    // ジョブ表は「編成の並び順 → 配置の辞書式昇順」で逐次構築する。
    // 各ジョブは results[自分の添字] にしか書かないので、回収に同期は要らず、
    // 出力はスレッドのスケジューリングに依存しない（同じ引数なら必ず同じ出力になる）。
    var jobs = new List<(int BuildIdx, int PermIdx, Formation F)>();
    for (int b = 0; b < builds.Length; b++)
    {
        var members = builds[b].F.Occupied().Select(x => x.Def).ToList();
        int permIdx = 0;
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation();
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            jobs.Add((b, permIdx++, f));
        }
    }

    // BattleEngine.Run は seed 決定的な純関数（副作用・外部依存なし）なので配置単位の並列は安全。
    var results = new int[jobs.Count][];
    Parallel.For(0, jobs.Count, i =>
    {
        var wins = new int[stages.Count];
        for (int st = 0; st < stages.Count; st++)
            for (int seed = 0; seed < LayoutSeeds; seed++)
                if (BattleEngine.Run(jobs[i].F, stages[st].Enemy, seed, verbose: false).PlayerWon)
                    wins[st]++;
        results[i] = wins;
    });

    // === 第140期 —— 波別最良の測り直しを、印字ループの外の平坦な並列パスに前出しした ===
    //
    // **選び方（`ranked` / `cur` / `pool` / 候補数 8 / 現行を必ず混ぜる）は1文字も変えていない。**
    // 変えたのは「どの順で `Rate` を走らせるか」だけで、`Rate` は seed 決定的な純関数なので値は同一。
    //
    // 従来はここが**印字ループの中で完全に直列**だった——61編成 × 5波 × 最大9候補 × 200 seed。
    // しかも `curRate` は `pool` にも入っているので、**同じ配置・同じ波を毎回2度測っていた**。
    // ここでは (配置, 波) をキーに**重複を潰してから**測る。
    var lyRanked = new List<int>[builds.Length];
    var lyCur = new int[builds.Length];
    for (int b = 0; b < builds.Length; b++)
    {
        int bb = b;
        lyRanked[b] = Enumerable.Range(0, jobs.Count)
            .Where(i => jobs[i].BuildIdx == bb)
            .OrderByDescending(i => results[i].Sum())
            .ThenBy(i => jobs[i].PermIdx)   // 同点は配置の辞書式で若い方（決定的タイブレーク）
            .ToList();
        lyCur[b] = lyRanked[b].FindIndex(i => SameFormation(jobs[i].F, builds[bb].F));
    }

    // 必要な (配置, 波) の組を、印字と同じ規則で先に全部並べる。
    var lyPool = new List<int>[builds.Length, stages.Count];
    var lyNeed = new List<(int JobIdx, int St)>();
    var lySeen = new HashSet<(int, int)>();
    for (int b = 0; b < builds.Length; b++)
        for (int st = 0; st < stages.Count; st++)
        {
            int sx = st;
            const int Candidates = 8;
            var pool = lyRanked[b].OrderByDescending(i => results[i][sx])
                                  .ThenBy(i => jobs[i].PermIdx)
                                  .Take(Candidates)
                                  .Append(lyRanked[b][lyCur[b]])
                                  .Distinct()
                                  .ToList();
            lyPool[b, st] = pool;
            foreach (int i in pool.Append(lyRanked[b][lyCur[b]]))
                if (lySeen.Add((i, sx))) lyNeed.Add((i, sx));
        }

    var lyRateVal = new double[lyNeed.Count];
    Parallel.For(0, lyNeed.Count, k =>
    {
        var (i, sx) = lyNeed[k];
        int wins = 0;
        for (int seed = 0; seed < VerifySeeds; seed++)
            if (BattleEngine.Run(jobs[i].F, stages[sx].Enemy, seed, verbose: false).PlayerWon) wins++;
        lyRateVal[k] = wins * 100.0 / VerifySeeds;
    });
    var lyRate = new Dictionary<(int, int), double>(lyNeed.Count);
    for (int k = 0; k < lyNeed.Count; k++) lyRate[lyNeed[k]] = lyRateVal[k];

    Console.WriteLine("# 配置探索");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 layout` の出力。");
    Console.WriteLine($"compare の各編成をメンバー固定で全配置（5体=120通り / 4体=120通り）に展開し、");
    Console.WriteLine($"全{stages.Count}ステージ × seed 0..{LayoutSeeds - 1} の平均勝率で並べた上位{TopN}件と現行配置。");
    Console.WriteLine($"検証した配置: {jobs.Count:N0} 通り（{(long)jobs.Count * stages.Count * LayoutSeeds:N0} 戦）");

    for (int b = 0; b < builds.Length; b++)
    {
        int bb = b;
        var ranked = lyRanked[b];

        Console.WriteLine();
        Console.WriteLine($"## {builds[b].Name}");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 前1/前3 | 中央 | 後1/後3 | 平均 |"
            + string.Concat(stages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|--:|---|---|---|--:|" + string.Concat(stages.Select(_ => "---:|")));
        for (int rank = 0; rank < TopN && rank < ranked.Count; rank++)
            Console.WriteLine(LayoutRow($"{rank + 1}", jobs[ranked[rank]].F, results[ranked[rank]], LayoutSeeds));

        int cur = lyCur[b];
        Console.WriteLine(LayoutRow($"現行({cur + 1}位)", jobs[ranked[cur]].F, results[ranked[cur]], LayoutSeeds));

        // 波別最良。上の表は全ステージ平均を最大化する「一つの配置」を選ぶが、
        // 実プレイは波ごとに組み替えられる。この差を出さないと、平均最良の配置が
        // たまたま苦手な波で出した勝率を「その編成の限界」と読み違える（§2-10）。
        Console.WriteLine();
        Console.WriteLine("| 波 | 前1/前3 | 中央 | 後1/後3 | 現行 | 波別最良 |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        for (int st = 0; st < stages.Count; st++)
        {
            int sx = st;

            // seed 50 の探索は 720 通りの最大を取るので、上位は運で入れ替わる。
            // 1位だけを測り直すと「波別最良が現行より低い」という原理的にありえない行が出る
            // （実測で最大 8pt の逆転が出た）。候補を上位数件に広げ、現行も必ず混ぜて、
            // seed 200 で測り直した中の最良を採る。これで表は必ず単調になる。
            // **候補の作り方は上の前計算と同一**（第140期に `lyPool` へ移しただけ）。
            var pool = lyPool[b, st];

            double curRate = lyRate[(ranked[cur], sx)];
            int best = ranked[cur];
            double bestRate = curRate;
            foreach (int i in pool)
            {
                double r = lyRate[(i, sx)];
                if (r > bestRate) { bestRate = r; best = i; }
            }
            bestByStage[bb, sx] = (curRate, bestRate);

            Formation bf = jobs[best].F;
            Console.WriteLine($"| 第{sx + 1}波 | {NameOf(bf[0])}/{NameOf(bf[1])} | {NameOf(bf[2])} "
                + $"| {NameOf(bf[3])}/{NameOf(bf[4])} | {curRate:F1}% | {bestRate:F1}% |");
        }
    }

    // 一覧。docs/balance.md（現行配置で固定）と並べて読むためのもの。
    Console.WriteLine();
    Console.WriteLine("## 波別最良の一覧");
    Console.WriteLine();
    Console.WriteLine($"各セルは「現行配置 → その波だけの最良配置」。どちらも seed 0..{VerifySeeds - 1} で測り直した値。");
    Console.WriteLine("勝率表（`compare`）は現行配置に固定した値なので、左の数字がそちらと対応する。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(stages.Select((_, i) => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(stages.Select(_ => "---:|")));
    for (int b = 0; b < builds.Length; b++)
    {
        var cells = Enumerable.Range(0, stages.Count)
            .Select(st => $" {bestByStage[b, st].Cur:F1} → {bestByStage[b, st].Best:F1} |");
        Console.WriteLine($"| {builds[b].Name} |" + string.Concat(cells));
    }
    return;

    static string NameOf(UnitDef? d) => d?.Name ?? "−";
}
}
