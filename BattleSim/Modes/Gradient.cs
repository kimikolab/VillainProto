using BattleCore;
using static Common;

// =====================================================================================
// gradient モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "gradient")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 gradient
// =====================================================================================

static class GradientDiag
{
// gradient モード: 勾配のある部隊列の候補を測る診断（第5期 Phase N）。
// 3波1列（地点サイズ）の各位置に 2〜3 案の候補波を組み、cost と同じ物差し
// （勝った試行の残HP% → 代金 = 100% − 残HP%）で測る。位置ごとの狙い:
//   第1波 = 安い(20〜30%)・範囲攻撃の編成に安い / 第2波 = 中(35〜50%)・偏らせない /
//   第3波 = 高い(50〜70%)・単体火力の編成に安い
// 3波の合計代金は 110〜150% を狙う（100% 以下だと1部隊で全抜きできて部隊数の判断が消え、
// 200% 超だと2部隊でも抜けず第2期の再来になる。第5期 §3-1）。
//
// 候補波はこのモードのローカル変数で組む。EnemyCatalog.Columns / Stages には足さない
// （採用はポン氏の判断待ち。handoff の接尾列と同じ「公開する列の集合を診断で汚さない」判断）。
// 診断用で docs/ には置かない（seats / handoff / cost と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 gradient [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    var all = CompareBuilds();
    const int GradSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // --- 候補波（位置ごとに 2〜3 案。第5期 §3-2 の制約: 1波6体まで / 貫き1枚 / 全体1枚 /
    //     断罪は入れない）。新 def は 農兵(levy) と 従軍司祭長(chaplain) の2つだけで、
    //     残りは既存 def の再利用（数値は触っていない）。 ---

    // 第1波: 農兵(30/8)の頭数だけで作る。1a/1b は体数の差。1c は「群れに斧1本」——
    // 敵側に薙ぎが1枚入ると味方の受け方（庇う・標的が効かない攻撃）が変わるので、
    // 範囲/単体の割れが「体数」由来か「敵の攻撃パターン」由来かを切り分ける対照。
    var w1 = new (string Name, Formation Enemy)[]
    {
        ("1a 農兵5", Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("1b 農兵5", Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("1c 農兵5+斧", Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Axeman, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
    };

    // 第2波: 既存 def の再利用だけで第一波と第二波の中間を作る（中間に新造の個性は要らない）。
    // 2a→2c の順に重くなる。2c の狙撃手は貫き1枚の上限内。
    var w2 = new (string Name, Formation Enemy)[]
    {
        ("2a 新兵3+斧", Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Recruit, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman)),
        ("2b 騎士混成", Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman)),
        ("2c 騎士2+狙撃", Formation.Build(front1: EnemyCatalog.Knight, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Archer)),
    };

    // 第3波: 少数高HP の精鋭。聖騎士長（第六波以降の素材・処刑持ち）と重装兵が素体。
    // 回復役の有無で性格が大きく変わるはずなので、司祭長入り(3b)となし(3a)の両方を測る。
    // 3c は2体の下限案（体数が減るほど範囲攻撃の意味が消え、単体火力有利が立つはず）。
    var w3 = new (string Name, Formation Enemy)[]
    {
        ("3a 精鋭3", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Warden)),
        ("3b 精鋭+司祭長", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Chaplain)),
        ("3c 精鋭2", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion)),
    };

    var cand = w1.Concat(w2).Concat(w3).ToList();

    Console.WriteLine($"# 勾配列の候補診断（seed 0..{GradSeeds - 1} の {GradSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("候補波の中身（HP/攻/速/型/配置）:");
    Console.WriteLine();
    foreach (var (name, enemy) in cand)
    {
        var members = enemy.Occupied().Select(x =>
        {
            string pat = x.Def.Pattern switch
            {
                AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き",
                AttackPattern.All => "全体", _ => "単体"
            };
            string[] seat = FormationRules.SeatNames;
            return $"{seat[x.Slot]}={x.Def.Name}({x.Def.MaxHp}/{x.Def.Attack}/速{x.Def.Speed}/{pat})";
        });
        Console.WriteLine($"- **{name}**: {string.Join("、", members)}");
    }
    Console.WriteLine();

    var cells = EmitCostTables(targets, cand, GradSeeds);

    // --- 範囲持ち / 単体のみ の割れ（第1波候補の成功条件。第5期 §3-3） ---
    // 判定は HasAoe（ファイル末尾の共有ヘルパ）。個別の食い違いは代金の表の側で読む。

    Console.WriteLine();
    Console.WriteLine("### 範囲持ちと単体のみの代金（編成の Def.Pattern に薙ぎ/全体を含むか）");
    Console.WriteLine();
    Console.WriteLine("| 候補 | 範囲持ちの代金平均 | 単体のみの代金平均 | 差（単体 − 範囲） |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int w = 0; w < cand.Count; w++)
    {
        var groups = Enumerable.Range(0, targets.Length)
            .Where(t => cells[t, w].Wins > 0)
            .GroupBy(t => HasAoe(targets[t].F))
            .ToDictionary(g => g.Key, g => g.Average(t => (1 - cells[t, w].AvgHpPct) * 100));
        double aoe = groups.TryGetValue(true, out double a) ? a : double.NaN;
        double single = groups.TryGetValue(false, out double b) ? b : double.NaN;
        Console.WriteLine($"| {cand[w].Name} | {aoe:F1}% | {single:F1}% | {single - aoe:+0.0;-0.0}pt |");
    }
    int nAoe = targets.Count(t => HasAoe(t.F));
    Console.WriteLine();
    Console.WriteLine($"範囲持ち {nAoe} 編成 / 単体のみ {targets.Length - nAoe} 編成"
        + "（代金は各群とも勝率 > 0% の編成だけで平均）");

    // --- 組み合わせ列（全27通り × 投入部隊数 1〜2） ---
    // 合計代金は各候補波の代金平均の単純和（単独戦の値）。連戦の実際の消耗は期待突破数で見る。
    // 期待突破数・突破率は全編成の平均。非線形 = 期待(2) ÷ (期待(1)×2)（engage と同じ定義）。
    double[] candMean = Enumerable.Range(0, cand.Count).Select(w =>
        Enumerable.Range(0, targets.Length).Where(t => cells[t, w].Wins > 0)
            .Average(t => (1 - cells[t, w].AvgHpPct) * 100)).ToArray();

    Console.WriteLine();
    Console.WriteLine("### 組み合わせ列（第1波×第2波×第3波 の27通り × 投入部隊数1〜2・全編成平均）");
    Console.WriteLine();
    Console.WriteLine("| 列 | 合計代金 | 期待突破数(1) | 突破率(1) | 期待突破数(2) | 突破率(2) | 非線形 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    for (int i = 0; i < w1.Length; i++)
        for (int j = 0; j < w2.Length; j++)
            for (int k = 0; k < w3.Length; k++)
            {
                var column = new[] { w1[i].Enemy, w2[j].Enemy, w3[k].Enemy };
                long trials = (long)targets.Length * GradSeeds;
                double e1 = 0, e2 = 0;
                int full1 = 0, full2 = 0;
                foreach (var (_, f) in targets)
                {
                    Formation[] one = { f };
                    Formation[] two = { f, f };
                    for (int seed = 0; seed < GradSeeds; seed++)
                    {
                        EngagementResult r1 = EngagementEngine.Run(one, column, seed, verbose: false);
                        e1 += r1.EnemySquadsCleared;
                        if (r1.PlayerWon) full1++;
                        EngagementResult r2 = EngagementEngine.Run(two, column, seed, verbose: false);
                        e2 += r2.EnemySquadsCleared;
                        if (r2.PlayerWon) full2++;
                    }
                }
                double total = candMean[i] + candMean[3 + j] + candMean[6 + k];
                double exp1 = e1 / trials, exp2 = e2 / trials;
                Console.WriteLine($"| {w1[i].Name[..2]}/{w2[j].Name[..2]}/{w3[k].Name[..2]} | {total:F0}% "
                    + $"| {exp1:F2} | {full1 * 100.0 / trials:F1}% | {exp2:F2} | {full2 * 100.0 / trials:F1}% "
                    + $"| {(exp1 == 0 ? "—" : $"{exp2 / (2 * exp1):F2}")} |");
                Console.Out.Flush();
            }

    // --- 推奨列の編成別内訳 ---
    // 組み合わせ表を読んでから spotlight を差し替えて再実行する二段運用（診断モードなので
    // 出力は使うときにその場で吐く。docs/ に置かない）。
    // 1本目は推奨列（1b/2b/3a: 全3波が §3-1 の狙い帯に入り、波間の勾配 27→41→61 が
    // いちばん単調に開く）。2本目は第3波を司祭長入りに替えた対照（回復役で編成別の
    // 序列がどう動くかを見る）。
    var spotlight = new (string Name, Formation[] Column)[]
    {
        ("1b/2b/3a（推奨）", new[] { w1[1].Enemy, w2[1].Enemy, w3[0].Enemy }),
        ("1b/2b/3b（司祭長対照）", new[] { w1[1].Enemy, w2[1].Enemy, w3[1].Enemy }),
    };
    foreach (var (colName, column) in spotlight)
    {
        Console.WriteLine();
        Console.WriteLine($"### 編成別内訳: {colName}");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 期待突破数(1) | 突破率(1) | 期待突破数(2) | 突破率(2) |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var (name, f) in targets)
        {
            Formation[] one = { f };
            Formation[] two = { f, f };
            double e1 = 0, e2 = 0;
            int full1 = 0, full2 = 0;
            for (int seed = 0; seed < GradSeeds; seed++)
            {
                EngagementResult r1 = EngagementEngine.Run(one, column, seed, verbose: false);
                e1 += r1.EnemySquadsCleared;
                if (r1.PlayerWon) full1++;
                EngagementResult r2 = EngagementEngine.Run(two, column, seed, verbose: false);
                e2 += r2.EnemySquadsCleared;
                if (r2.PlayerWon) full2++;
            }
            Console.WriteLine($"| {name} | {e1 / GradSeeds:F2} | {full1 * 100.0 / GradSeeds:F1}% "
                + $"| {e2 / GradSeeds:F2} | {full2 * 100.0 / GradSeeds:F1}% |");
            Console.Out.Flush();
        }
    }
    return;
}
}
