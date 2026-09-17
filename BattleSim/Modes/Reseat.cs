using BattleCore;
using static Common;

// =====================================================================================
// reseat モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "reseat")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 reseat
// =====================================================================================

static class ReseatDiag
{
public static void Run(string[] args, int stageIndex)
{
    var all = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> stages = EnemyCatalog.Stages;
    const int ScanSeeds = 50;    // 候補を絞るための粗い探索。layout と揃える
    const int VerifySeeds = 200; // 採否を決める測り直し。compare と揃える
    const int TopOverall = 20;
    const int TopConstrained = 10;

    // 対象は既定では compare の全編成。args[2] にカンマ区切りの部分一致を渡すと絞れる。
    // 固定リストにしていた頃は「いつ作ったリストか」が読めず、盤面や波を変えたあとも
    // 古い顔ぶれのまま回してしまう。絞り込みは呼び出し側で明示する。
    string filter = args.Length > 2 ? args[2] : "";
    var targets = all.Select(b => b.Name)
        .Where(n => filter.Length == 0
                    || filter.Split(',').Any(k => n.Contains(k.Trim())))
        .ToArray();

    // 長時間ジョブは前景で待ち切るしかない（背景に回すと起動元のコマンド終了で刈られる）。
    // 一回の呼び出しに収まる分だけを回せるよう、対象を切り出せるようにしてある。
    int skip = args.Length > 3 && int.TryParse(args[3], out int sk) ? sk : 0;
    int take = args.Length > 4 && int.TryParse(args[4], out int tk) ? tk : targets.Length;
    targets = targets.Skip(skip).Take(take).ToArray();

    Console.WriteLine("# 配置の測り直し");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{ScanSeeds - 1} の全配置探索で候補を絞り、seed 0..{VerifySeeds - 1} で測り直した。");
    Console.WriteLine("`狙`列: ガルドが前列 / セッキが後列 を満たすか（その駒を含む編成のみ）。");

    // === 第140期 —— 戦闘を2つの平坦な並列パスに畳んだ（器具の期・値は1ビットも変えない） ===
    //
    // **`perms` / `order` / `pool` / `verified` の作り方は1文字も変えていない。**
    // 変えたのは「どの順で戦闘を走らせるか」だけで、`BattleEngine.Run` は seed 決定的な純関数
    // （副作用も外部依存もない）なので、どのスレッドで走らせても同じ seed は同じ結果を返す。
    //
    // **各ジョブは自分の添字にしか書かない**（共有の `List` に `Add` しない）ので回収に同期は要らず、
    // **印字は従来どおり直列に、`targets` の順・`pool` の順で行う**——出力はスレッドの
    // スケジューリングに依存しない。**平均も `double[]` の添字順に足す**ので浮動小数も同一。
    //
    // 行ごとに `Parallel.For` を 61 回立てるのではなく**全行ぶんを1つの平坦なジョブ表に畳む**のは、
    // 1行あたり 120 ジョブでは末尾でコアが余るため（`layout` の粗探索と同じ形）。
    var rsPerms = new List<Formation>[targets.Length];
    for (int t = 0; t < targets.Length; t++)
    {
        var members = all.First(b => b.Name == targets[t]).F.Occupied().Select(x => x.Def).ToList();
        var ps = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation();
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            ps.Add(f);
        }
        rsPerms[t] = ps;
    }

    // (a) 粗探索（seed 0..ScanSeeds-1 の全配置）。
    var rsScan = new int[targets.Length][];
    var rsScanJobs = new List<(int T, int I)>();
    for (int t = 0; t < targets.Length; t++)
    {
        rsScan[t] = new int[rsPerms[t].Count];
        for (int i = 0; i < rsPerms[t].Count; i++) rsScanJobs.Add((t, i));
    }
    Parallel.For(0, rsScanJobs.Count, j =>
    {
        var (t, i) = rsScanJobs[j];
        int wins = 0;
        foreach (EnemyCatalog.Stage st in stages)
            for (int seed = 0; seed < ScanSeeds; seed++)
                if (BattleEngine.Run(rsPerms[t][i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
        rsScan[t][i] = wins;
    });

    // (b) 候補の選び方は従来と同一。**戦闘を1回も回さない**ので直列のまま。
    var rsOrder = new List<int>[targets.Length];
    var rsPool = new List<int>[targets.Length];
    for (int t = 0; t < targets.Length; t++)
    {
        var perms = rsPerms[t];
        var scan = rsScan[t];
        var build = all.First(b => b.Name == targets[t]);
        rsOrder[t] = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
        rsPool[t] = rsOrder[t].Take(TopOverall)
            .Concat(rsOrder[t].Where(i => MeetsIntent(perms[i])).Take(TopConstrained))
            .Append(rsOrder[t].First(i => SameFormation(perms[i], build.F)))
            .Distinct().ToList();
    }

    // (c) 測り直し（seed 0..VerifySeeds-1）。**波までジョブに割る**ので粒度が揃う。
    var rsCells = new double[targets.Length][][];
    var rsVerJobs = new List<(int T, int P, int St)>();
    for (int t = 0; t < targets.Length; t++)
    {
        rsCells[t] = new double[rsPool[t].Count][];
        for (int p = 0; p < rsPool[t].Count; p++)
        {
            rsCells[t][p] = new double[stages.Count];
            for (int st = 0; st < stages.Count; st++) rsVerJobs.Add((t, p, st));
        }
    }
    Parallel.For(0, rsVerJobs.Count, j =>
    {
        var (t, p, st) = rsVerJobs[j];
        int wins = 0;
        for (int seed = 0; seed < VerifySeeds; seed++)
            if (BattleEngine.Run(rsPerms[t][rsPool[t][p]], stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
        rsCells[t][p][st] = wins * 100.0 / VerifySeeds;
    });

    // (d) 印字。**従来の foreach の中身をそのまま持ってきてある。**
    for (int t = 0; t < targets.Length; t++)
    {
        string name = targets[t];
        var build = all.First(b => b.Name == name);
        var perms = rsPerms[t];
        var order = rsOrder[t];

        // `pool.Select(...).OrderByDescending(x => x.Avg)` と同じ——LINQ の OrderBy は安定ソートなので、
        // 同値は `pool` の順（＝従来と同じ順）で残る。
        var verified = Enumerable.Range(0, rsPool[t].Count)
            .Select(p => (Idx: rsPool[t][p], Cells: rsCells[t][p], Avg: rsCells[t][p].Average()))
            .OrderByDescending(x => x.Avg).ToList();

        Console.WriteLine();
        Console.WriteLine($"## {name}");
        Console.WriteLine();
        Console.WriteLine("| 粗順 | 狙 | 前1/前3 | 中央 | 後1/後3 | 平均 |"
            + string.Concat(stages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|--:|:-:|---|---|---|--:|" + string.Concat(stages.Select(_ => "---:|")));
        foreach (var v in verified)
        {
            Formation f = perms[v.Idx];
            static string N(UnitDef? d) => d?.Name ?? "−";
            bool isCur = SameFormation(f, build.F);
            string rank = $"{order.IndexOf(v.Idx) + 1}" + (isCur ? "★現行" : "");
            Console.WriteLine($"| {rank} | {(MeetsIntent(f) ? "○" : "×")} | {N(f[0])}/{N(f[1])} | {N(f[2])} "
                + $"| {N(f[3])}/{N(f[4])} | {v.Avg:F1}% |" + string.Concat(v.Cells.Select(c => $" {c:F1}% |")));
        }
        Console.Out.Flush();
    }

    bool MeetsIntent(Formation f)
    {
        foreach (var (slot, def) in f.Occupied())
        {
            if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
            if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
        }
        return true;
    }
    return;
}
}
