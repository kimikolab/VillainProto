using BattleCore;
using static Common;

// =====================================================================================
// ablate モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "ablate")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 ablate
// =====================================================================================

static class AblateDiag
{
// ablate モード: 編成からメンバーを1体ずつ抜いたときの勝率低下を測る。
// 「完成した5体 − 重要駒を1体抜いた編成」の差が大きいほど、強さが個々の性能ではなく
// 組み合わせから生まれている証拠になる（README「受け皿を足したら供給役を抜いた対照を必ず測る」の一般化）。
// 差が小さい、あるいはマイナス（抜いたほうが勝率が上がる）なら、そのメンバーは入れ得の疑いがある。
// 対象は既定では compare の全編成。reseat と同じ書式でカンマ区切りの部分一致で絞れる。
public static void Run(string[] args, int stageIndex)
{
    var all = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> abStages = EnemyCatalog.Stages;
    const int AblateSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all.Where(b => filter.Length == 0
                    || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();


    Console.WriteLine("# アブレーション（1体抜いた時の勝率変化）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 ablate > docs/ablation.md` の出力。手で編集しない。");
    Console.WriteLine($"全ステージ通算、seed 0..{AblateSeeds - 1} の {AblateSeeds} 試行。");
    Console.WriteLine("差が大きいほど「そのメンバーが編成の強さの源」。差が0に近い、またはプラス（抜いたほうが勝率が上がる）なら入れ得の疑い。");
    Console.WriteLine();

    // === 第140期 —— 戦闘を1つの平坦な並列パスに畳んだ（器具の期・値は1ビットも変えない） ===
    //
    // **編成の作り方（フル / 1体抜き）も、印字の順序も1文字も変えていない。**
    // `BattleEngine.Run` は seed 決定的な純関数なので、どのスレッドで走らせても値は同じ。
    // 各ジョブは自分の添字にしか書かない（共有の `List` に `Add` しない）。
    //
    // **勝率は整数の勝ち数を持ち帰って、最後に直列で割る**——並列側で平均を取ると
    // 合算の順序で浮動小数が動きうる（規約: カウンタは整数のまま持ち帰る）。
    // **波までジョブに割る**のは、編成単位（= 1,000 戦）だと末尾でコアが余るため。
    var abForms = new List<Formation[]>();       // [行] → [0]=フル, [1..]=1体抜き
    var abMembers = new List<List<(int Slot, UnitDef Def)>>();
    foreach (var (name, full) in targets)
    {
        var members = full.Occupied().ToList();
        var fs = new Formation[members.Count + 1];
        fs[0] = full;
        for (int m = 0; m < members.Count; m++)
        {
            var ablated = new Formation();
            foreach (var (mSlot, mDef) in members)
                if (mSlot != members[m].Slot) ablated[mSlot] = mDef;
            fs[m + 1] = ablated;
        }
        abForms.Add(fs);
        abMembers.Add(members);
    }

    var abWins = new int[abForms.Count][][];     // [行][版][波] の勝ち数
    var abJobs = new List<(int R, int V, int St)>();
    for (int r = 0; r < abForms.Count; r++)
    {
        abWins[r] = new int[abForms[r].Length][];
        for (int v = 0; v < abForms[r].Length; v++)
        {
            abWins[r][v] = new int[abStages.Count];
            for (int st = 0; st < abStages.Count; st++) abJobs.Add((r, v, st));
        }
    }
    Parallel.For(0, abJobs.Count, j =>
    {
        var (r, v, st) = abJobs[j];
        int wins = 0;
        for (int seed = 0; seed < AblateSeeds; seed++)
            if (BattleEngine.Run(abForms[r][v], abStages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
        abWins[r][v][st] = wins;
    });

    double AbRate(int r, int v)
    {
        int wins = 0;
        for (int st = 0; st < abStages.Count; st++) wins += abWins[r][v][st];
        return wins * 100.0 / (abStages.Count * AblateSeeds);
    }

    for (int r = 0; r < targets.Length; r++)
    {
        string name = targets[r].Name;
        double fullRate = AbRate(r, 0);
        var members = abMembers[r];

        Console.WriteLine($"## {name}（フル編成 {fullRate:F1}%）");
        Console.WriteLine();
        Console.WriteLine("| 抜いた駒 | 抜いた後 | 差 |");
        Console.WriteLine("|---|--:|--:|");

        for (int m = 0; m < members.Count; m++)
        {
            double rate = AbRate(r, m + 1);
            string sign = rate - fullRate >= 0 ? "+" : "";
            Console.WriteLine($"| {members[m].Def.Name} | {rate:F1}% | {sign}{rate - fullRate:F1}pt |");
        }
        Console.WriteLine();
    }
    return;
}
}
