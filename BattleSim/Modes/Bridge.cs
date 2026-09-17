using BattleCore;
using static Common;

// =====================================================================================
// bridge モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "bridge")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 bridge
// =====================================================================================

static class BridgeDiag
{
// bridge モード: 代金の向きは編成の序列を動かすか（第7期 Phase S）。
//
// 代金に向きが出ても、突破数の序列が動くとは限らない。**第4期の逆順列がまさにそれ**で、
// 指標は大きく動いたのに実体は「第五波の独立勝率の測り直し」だった。同じ轍を踏まないために、
// 向きのある波を実際に列として組み、engage と同じ物差し（期待突破数・突破率、1部隊・2部隊）で
// 全編成を測って**順位が入れ替わるか**を見る。
//
// | 列 | 第1波 | 第2波 | 第3波 | 性格 |
// |---|---|---|---|---|
// | 平坦列 | 1b 農兵5（+3.1pt） | 2b 騎士混成 | 3a 精鋭3（-0.5pt） | 向きがほぼ無い（第5期の推奨列） |
// | 反転列 | H2a 裸5（+8.7pt） | 2b 騎士混成 | R11 従卒5（-8.4pt） | 列の中で符号が反転する |
//
// 第2波は両列で同じもの（2b 固定）にして交絡を減らす。反転列は2本測る——主表は指示書
// どおり H2a（+8.7pt・第6期の最大値）だが、H2a の代金は 29.8% で 1b の 27.3% より
// 2.5pt 高い。列全体の難度がずれると順位相関に床/天井の効きが混ざるので、代金が 1b と
// ほぼ同額（27.2%）で向きだけが違う H2d（+7.4pt）を**難度をそろえた対照**として並べる。
//
// 順位相関はスピアマン（同順位は平均順位で処理し、順位列のピアソン相関を取る）。
// 判定（第7期 §3-3）: 0.7 未満かつ範囲持ちが反転列で上がっていれば橋が架かった。
// 0.9 以上なら「代金には出たが結果には出ていない」＝第4期の逆順列と同じ形。
//
// 診断用で docs/ には置かない（seats / handoff / cost / gradient / aim / flip と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 bridge [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    var all = CompareBuilds();
    const int BridgeSeeds = 200;   // gradient / aim / flip と同じ。平坦列の検算が成立する条件

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 波はすべて gradient / aim / flip のローカル定義をそのまま写したもの（同じ並び・同じ配置）。
    // 値が動いたら測り方が変わった証拠なので、平坦列の期待突破数(1) = 2.05 / 突破率(1) = 6.4%
    // （第5期の推奨列）と突き合わせて止まる。
    Formation W1Levy5 = Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy);
    Formation W1ZealotBare5 = Formation.Build(front1: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare, center: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare, back3: EnemyCatalog.ZealotBare);
    Formation W1ZealotLeather4 = Formation.Build(front1: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather, center: EnemyCatalog.ZealotLeather, back1: EnemyCatalog.ZealotLeather);
    Formation W2Mixed = Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman);
    Formation W3Elite3 = Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Warden);
    Formation W3Squire5 = Formation.Build(front1: EnemyCatalog.ZealotSquire, front3: EnemyCatalog.ZealotSquire, center: EnemyCatalog.ZealotSquire, back1: EnemyCatalog.ZealotSquire, back3: EnemyCatalog.ZealotSquire);
    // 第8期 Phase V。第3波の代金だけを振る（体数・個体HP は R11 と同じで攻撃だけが違う）。
    Formation W3Porter5 = Formation.Build(front1: EnemyCatalog.ZealotPorter, front3: EnemyCatalog.ZealotPorter, center: EnemyCatalog.ZealotPorter, back1: EnemyCatalog.ZealotPorter, back3: EnemyCatalog.ZealotPorter);
    Formation W3Pilgrim5 = Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front3: EnemyCatalog.ZealotPilgrim, center: EnemyCatalog.ZealotPilgrim, back1: EnemyCatalog.ZealotPilgrim, back3: EnemyCatalog.ZealotPilgrim);

    var columns = new (string Name, string Note, Formation[] Squads)[]
    {
        ("平坦列", "1b 農兵5(+3.1pt) / 2b 騎士混成 / 3a 精鋭3(-0.5pt)。第5期の推奨列",
            new[] { W1Levy5, W2Mixed, W3Elite3 }),
        ("反転列", "H2a 裸5(+8.7pt) / 2b 騎士混成 / R11 従卒5(-8.4pt)。列の中で符号が反転する",
            new[] { W1ZealotBare5, W2Mixed, W3Squire5 }),
        ("反転列(難度そろえ)", "H2d 革4(+7.4pt・代金 27.2% は 1b とほぼ同額) / 2b / R11 従卒5",
            new[] { W1ZealotLeather4, W2Mixed, W3Squire5 }),
        // 第8期 Phase V。反転列と第1波・第2波は同じで、第3波の代金だけが違う3点。
        ("反転列(中)", "H2a 裸5 / 2b / 荷駄5(90/攻7・代金 54%)。合計を下げて境目に近づける",
            new[] { W1ZealotBare5, W2Mixed, W3Porter5 }),
        ("反転列(低)", "H2a 裸5 / 2b / 巡礼5(90/攻4・代金 42%)。**向きは -2.2pt しか無い**",
            new[] { W1ZealotBare5, W2Mixed, W3Pilgrim5 }),
        // 低の群差の対照。第1波だけを向きの無い 1b に戻した以外は反転列(低)と同じ。
        // 反転列(低) で範囲持ちの Δ が開いたとき、それが「列の向き（第1波 +8.7pt）が
        // 境目で結果に出た」のか「安い波は範囲持ちに有利なだけ」なのかを分ける。
        ("平坦列(低)", "1b 農兵5(+3.1pt) / 2b / 巡礼5。反転列(低) の群差の対照",
            new[] { W1Levy5, W2Mixed, W3Pilgrim5 }),
        // 第10期 Phase AB-0。上の6列には全体持ちも貫き持ちも1体もいないので、
        // チャージ化の前後でこの表が1つも動かない。測定台の骨格を保ったまま
        // 貫き1枚・全体1枚だけ入れ替えた列を足す（中身は ChargeBench() に寄せてある）。
        // この列の 単体−範囲 は -1.9pt しかないので**向きの判定には使えない**。
        // 測るのは時間軸（チャージ化の前後）で、第6〜8期の軸ではない。
        ("チャージ台", "H2a 裸5 / 2b の斧→狙撃手(貫き) / 巡礼3+詠唱兵(全体)・合計 116.6%。チャージ化の前後を測る台",
            ChargeBench()),
    };

    Console.WriteLine($"# 向きは序列を動かすか（seed 0..{BridgeSeeds - 1} の {BridgeSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("向きのある波を列として組み、engage と同じ物差し（期待突破数・突破率、投入部隊数 1〜2）で");
    Console.WriteLine("全編成を測ったもの。**見たいのは値の大小ではなく順位の入れ替わり**——第4期の逆順列は");
    Console.WriteLine("指標が大きく動いたのに中身が「第五波の独立勝率の測り直し」だった。");
    Console.WriteLine();
    Console.WriteLine("**検算: 平坦列の 期待突破数(1) = 2.05 / 突破率(1) = 6.4%**（第5期の推奨列 1b/2b/3a）。");
    Console.WriteLine("一致しなければこの表は読めない。");
    Console.WriteLine();
    foreach (var (name, note, _) in columns) Console.WriteLine($"- **{name}**: {note}");
    Console.WriteLine();

    // --- 計測 ---
    int nCol = columns.Length, nT = targets.Length;
    var exp1 = new double[nCol, nT];
    var full1 = new double[nCol, nT];
    var exp2 = new double[nCol, nT];
    var full2 = new double[nCol, nT];
    var deg1 = new double[nCol, nT];   // 突破度（第8期 Phase U）
    var deg2 = new double[nCol, nT];
    for (int c = 0; c < nCol; c++)
        for (int t = 0; t < nT; t++)
        {
            Formation[] one = { targets[t].F };
            Formation[] two = { targets[t].F, targets[t].F };
            double e1 = 0, e2 = 0, d1 = 0, d2 = 0;
            int w1 = 0, w2 = 0;
            for (int seed = 0; seed < BridgeSeeds; seed++)
            {
                EngagementResult r1 = EngagementEngine.Run(one, columns[c].Squads, seed, verbose: false);
                e1 += r1.EnemySquadsCleared;
                d1 += BreakthroughDegree(r1, columns[c].Squads.Length);
                if (r1.PlayerWon) w1++;
                EngagementResult r2 = EngagementEngine.Run(two, columns[c].Squads, seed, verbose: false);
                e2 += r2.EnemySquadsCleared;
                d2 += BreakthroughDegree(r2, columns[c].Squads.Length);
                if (r2.PlayerWon) w2++;
            }
            exp1[c, t] = e1 / BridgeSeeds;
            full1[c, t] = w1 * 100.0 / BridgeSeeds;
            exp2[c, t] = e2 / BridgeSeeds;
            full2[c, t] = w2 * 100.0 / BridgeSeeds;
            deg1[c, t] = d1 / BridgeSeeds;
            deg2[c, t] = d2 / BridgeSeeds;
        }

    // --- 列ごとの合計代金と、第3波の向き（第8期 Phase V §3-1・§3-3） ---
    // 波の代金は cost / gradient / aim / flip と同じ物差し（勝った試行の残HP% → 代金 =
    // 100% − 残HP%、区分は HasAoe）で、ここで測り直す。列の合計代金と突破度を同じ
    // 実行の中で並べないと、「境目に近づけたら相関が動いたか」を突き合わせられない。
    // **向きの無い列で橋を測っても判定にならない**ので、単体−範囲 を必ず併記する（§5-7）。
    Console.WriteLine("### 列の合計代金と、第3波の向き（同じ実行で測り直したもの）");
    Console.WriteLine();
    Console.WriteLine("代金は cost / aim / flip と同じ物差し（勝った試行の残HP% → 代金 = 100% − 残HP%）。");
    Console.WriteLine("`合計代金` は3波の代金の和で、部隊の容量（約 100%）と比べて読む。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 第1波 | 第2波 | 第3波 | 合計代金 | 第3波の 単体−範囲 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    foreach (var (name, _, squads) in columns)
    {
        var costs = squads.Select(w => WaveCost(targets, w, BridgeSeeds)).ToArray();
        Console.WriteLine($"| {name} | {costs[0].Mean:F1}% | {costs[1].Mean:F1}% | {costs[2].Mean:F1}% "
            + $"| {costs.Sum(c => c.Mean):F1}% | {costs[2].Split:+0.0;-0.0}pt |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine("`単体−範囲` がマイナスなら範囲持ちに高くつく（反転している）。**-4pt を下回ると");
    Console.WriteLine("向きが編成間のばらつき（SD 13pt 前後）に埋もれる**ので、その列は向きの判定に使えない。");
    Console.WriteLine();

    // --- 突破度の検算（列長1では最終戦＝初戦なので両者が一致するはず。第8期 §2-3） ---
    // 一致しなければ分母か更新位置がずれている。表を読む前にここで止まれるよう先に出す。
    double maxGap = 0;
    for (int t = 0; t < nT; t++)
        for (int seed = 0; seed < 20; seed++)
        {
            EngagementResult r = EngagementEngine.Run(new[] { targets[t].F }, new[] { W3Elite3 }, seed, verbose: false);
            maxGap = Math.Max(maxGap, Math.Abs(r.LastBattleAttrition - r.FirstBattleAttrition));
        }
    Console.WriteLine($"**検算（列長1）: |LastBattleAttrition − FirstBattleAttrition| の最大 = {maxGap:F6}**"
        + $"（{nT} 編成 × seed 0..19。0 でなければ突破度の分母がずれている）");
    Console.WriteLine();

    Console.WriteLine("### 列ごとの全編成平均（検算はこの表の平坦列を見る）");
    Console.WriteLine();
    Console.WriteLine("| 列 | 期待突破数(1) | 突破度(1) | 突破率(1) | 期待突破数(2) | 突破度(2) | 突破率(2) | 非線形 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    for (int c = 0; c < nCol; c++)
    {
        double a1 = Avg(c, exp1), a2 = Avg(c, exp2);
        Console.WriteLine($"| {columns[c].Name} | {a1:F2} | {Avg(c, deg1):F2} | {Avg(c, full1):F1}% "
            + $"| {a2:F2} | {Avg(c, deg2):F2} | {Avg(c, full2):F1}% "
            + $"| {(a1 == 0 ? "—" : $"{a2 / (2 * a1):F2}")} |");
    }
    double Avg(int c, double[,] m) => Enumerable.Range(0, nT).Average(t => m[c, t]);

    // --- 順位 ---
    // 期待突破数(1) の降順で順位を付ける（同値は平均順位）。1部隊で見るのは、2部隊だと
    // 突破率が 92〜100% に飽和して序列が潰れるため（第5期の持ち越し論点(2)）。
    var rank1 = new double[nCol][];
    var rank2 = new double[nCol][];
    var rankD1 = new double[nCol][];   // 突破度での順位（第8期 Phase U）
    var rankD2 = new double[nCol][];
    for (int c = 0; c < nCol; c++)
    {
        rank1[c] = AverageRanksDesc(Enumerable.Range(0, nT).Select(t => exp1[c, t]).ToArray());
        rank2[c] = AverageRanksDesc(Enumerable.Range(0, nT).Select(t => exp2[c, t]).ToArray());
        rankD1[c] = AverageRanksDesc(Enumerable.Range(0, nT).Select(t => deg1[c, t]).ToArray());
        rankD2[c] = AverageRanksDesc(Enumerable.Range(0, nT).Select(t => deg2[c, t]).ToArray());
    }

    Console.WriteLine();
    Console.WriteLine("### 編成別の期待突破数と順位（1部隊。順位は期待突破数(1) の降順・同値は平均順位）");
    Console.WriteLine();
    Console.WriteLine("`範` は Def.Pattern に薙ぎ/全体を含む編成（HasAoe。cost 以来ずっと同じ区分）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 範 | 平坦 期待 | 平坦 順位 | 反転 期待 | 反転 順位 | 順位差 "
        + "| 平坦 突破度 | 反転 突破度 | 突破度の順位差 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderBy(t => rank1[0][t]))
        Console.WriteLine($"| {targets[t].Name} | {(HasAoe(targets[t].F) ? "○" : "")} "
            + $"| {exp1[0, t]:F2} | {rank1[0][t]:F1} | {exp1[1, t]:F2} | {rank1[1][t]:F1} "
            + $"| {rank1[0][t] - rank1[1][t]:+0.0;-0.0} "
            + $"| {deg1[0, t]:F3} | {deg1[1, t]:F3} | {rankD1[0][t] - rankD1[1][t]:+0.0;-0.0} |");
    Console.WriteLine();
    Console.WriteLine("順位差はプラスが「反転列で上がった」（順位の数字が小さくなった）。");
    Console.WriteLine("突破度は**突破した部隊数 + 最後に負けた部隊戦での削り割合**（全抜きは列長ちょうど）。");

    // --- 順位相関 ---
    Console.WriteLine();
    Console.WriteLine("### 順位相関（スピアマン。同順位は平均順位、順位列のピアソン相関）");
    Console.WriteLine();
    Console.WriteLine("| 比較 | 期待突破数 1部隊 | 期待突破数 2部隊 | **突破度 1部隊** | **突破度 2部隊** "
        + "| 順位差の絶対値の平均(期待・1部隊) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    for (int c = 1; c < nCol; c++)
        Console.WriteLine($"| 平坦列 × {columns[c].Name} | {Pearson(rank1[0], rank1[c]):F2} | {Pearson(rank2[0], rank2[c]):F2} "
            + $"| {Pearson(rankD1[0], rankD1[c]):F2} | {Pearson(rankD2[0], rankD2[c]):F2} "
            + $"| {Enumerable.Range(0, nT).Average(t => Math.Abs(rank1[0][t] - rank1[c][t])):F1} |");
    Console.WriteLine();
    Console.WriteLine("判定の目安（第7期 §3-3）: 0.7 未満かつ範囲持ちが反転列で上がっていれば**橋が架かった**。");
    Console.WriteLine("0.9 以上なら**代金には出たが結果には出ていない**（第4期の逆順列と同じ形）。");

    // --- 同値塊の大きさ（順位相関を額面で読んではいけない理由） ---
    // 期待突破数は「ちょうど2波抜けて3波目で尽きる」に張り付く編成が多い。同値塊の中の
    // 並びは平均順位で潰してあるが、塊の外に1編成出入りするだけで塊全体の順位が動くので、
    // **順位相関は塊の大きさに引きずられる**。塊の頭数と値の幅を必ず併記する。
    Console.WriteLine();
    Console.WriteLine("### 指標の分解能（順位相関を額面で読まないための注記）");
    Console.WriteLine();
    Console.WriteLine("| 列 | 期待突破数がちょうど 2.00 の編成数 | 期待の同値編成数 | 最小 | 最大 | 幅 "
        + "| 突破度が整数の編成数 | 突破度の同値編成数 | 突破度 最小 | 最大 | 幅 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int c = 0; c < nCol; c++)
    {
        var v = Enumerable.Range(0, nT).Select(t => exp1[c, t]).ToArray();
        var d = Enumerable.Range(0, nT).Select(t => deg1[c, t]).ToArray();
        Console.WriteLine($"| {columns[c].Name} | {v.Count(x => x == 2.0)} | {nT - v.Distinct().Count()} "
            + $"| {v.Min():F2} | {v.Max():F2} | {v.Max() - v.Min():F2} "
            + $"| {d.Count(x => x == Math.Floor(x))} | {nT - d.Distinct().Count()} "
            + $"| {d.Min():F3} | {d.Max():F3} | {d.Max() - d.Min():F3} |");
    }
    Console.WriteLine();
    Console.WriteLine("`同値編成数` は「他の編成と値がぴったり並んでいる編成の数」（編成数 − 相異なる値の数）。");
    Console.WriteLine("順位相関はこの塊の大きさに引きずられるので、突破度で塊が消えているかをここで見る。");

    // --- 値そのものの相関（同値塊の影響を受けない側） ---
    // 順位は塊で暴れるが、期待突破数の値は暴れない。順位相関が低くて値相関が高いなら、
    // 「序列が入れ替わった」のではなく「分解能が無い指標を順位に潰した」だけ。
    Console.WriteLine();
    Console.WriteLine("| 比較 | 期待突破数(1) の値の相関 | 期待突破数(2) の値の相関 |");
    Console.WriteLine("|---|--:|--:|");
    for (int c = 1; c < nCol; c++)
    {
        var a1 = Enumerable.Range(0, nT).Select(t => exp1[0, t]).ToArray();
        var b1 = Enumerable.Range(0, nT).Select(t => exp1[c, t]).ToArray();
        var a2 = Enumerable.Range(0, nT).Select(t => exp2[0, t]).ToArray();
        var b2 = Enumerable.Range(0, nT).Select(t => exp2[c, t]).ToArray();
        Console.WriteLine($"| 平坦列 × {columns[c].Name} | {Pearson(a1, b1):F2} | {Pearson(a2, b2):F2} |");
    }

    // --- 動いた編成 上位5つ ---
    Console.WriteLine();
    Console.WriteLine("### 順位が最も動いた編成 上位5つ（平坦列 → 反転列）");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 範 | 平坦 順位 | 反転 順位 | 動き |");
    Console.WriteLine("|---|:-:|--:|--:|---|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => Math.Abs(rank1[0][t] - rank1[1][t])).Take(5))
    {
        double d = rank1[0][t] - rank1[1][t];
        Console.WriteLine($"| {targets[t].Name} | {(HasAoe(targets[t].F) ? "○" : "")} "
            + $"| {rank1[0][t]:F1} | {rank1[1][t]:F1} | {(d > 0 ? "↑" : "↓")}{Math.Abs(d):F1} |");
    }

    // --- 範囲持ちが反転列で上がっているか（狙いどおりの向きに動いたか） ---
    // 順位が動いても、動いたのが範囲/単体と無関係なら「向きではない別の要因」（地力・
    // 自傷の固定費など）が動かしている。第7期 §3-3 の3行目を判別するための表。
    Console.WriteLine();
    Console.WriteLine("### 範囲持ち / 単体のみ の平均順位（狙いどおりの向きに動いたか）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成数 | 平坦 平均順位 | 反転 平均順位 | 差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (bool aoe in new[] { true, false })
    {
        var grp = Enumerable.Range(0, nT).Where(t => HasAoe(targets[t].F) == aoe).ToArray();
        double a = grp.Average(t => rank1[0][t]), b = grp.Average(t => rank1[1][t]);
        Console.WriteLine($"| {(aoe ? "範囲持ち" : "単体のみ")} | {grp.Length} | {a:F1} | {b:F1} | {a - b:+0.0;-0.0} |");
    }
    Console.WriteLine();
    Console.WriteLine("差がプラスなら反転列で順位が上がっている（順位の数字が小さくなった）。");

    // 値そのもので同じことを見る。順位は同値塊で暴れるが、期待突破数の差は暴れない。
    // 「範囲持ちだけが得をした」なら群間で Δ に差が出るはずで、出ないなら動かしたのは
    // 向きではない（第7期 §3-3 の3行目）。
    Console.WriteLine();
    Console.WriteLine("### 期待突破数の変化 Δ（反転列 − 平坦列。値なので同値塊の影響を受けない）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成数 |" + string.Concat(Enumerable.Range(1, nCol - 1).Select(c => $" Δ {columns[c].Name} |")));
    Console.WriteLine("|---|--:|" + string.Concat(Enumerable.Range(1, nCol - 1).Select(_ => "--:|")));
    foreach (bool aoe in new[] { true, false })
    {
        var grp = Enumerable.Range(0, nT).Where(t => HasAoe(targets[t].F) == aoe).ToArray();
        Console.WriteLine($"| {(aoe ? "範囲持ち" : "単体のみ")} | {grp.Length} |"
            + string.Concat(Enumerable.Range(1, nCol - 1)
                .Select(c => $" {grp.Average(t => exp1[c, t] - exp1[0, t]):+0.000;-0.000} |")));
    }

    Console.WriteLine();
    Console.WriteLine("### 突破度の変化 Δ（同じ表を突破度で。整数に潰れない分だけ細かく出る）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成数 |" + string.Concat(Enumerable.Range(1, nCol - 1).Select(c => $" Δ {columns[c].Name} |")));
    Console.WriteLine("|---|--:|" + string.Concat(Enumerable.Range(1, nCol - 1).Select(_ => "--:|")));
    foreach (bool aoe in new[] { true, false })
    {
        var grp = Enumerable.Range(0, nT).Where(t => HasAoe(targets[t].F) == aoe).ToArray();
        Console.WriteLine($"| {(aoe ? "範囲持ち" : "単体のみ")} | {grp.Length} |"
            + string.Concat(Enumerable.Range(1, nCol - 1)
                .Select(c => $" {grp.Average(t => deg1[c, t] - deg1[0, t]):+0.000;-0.000} |")));
    }
    Console.WriteLine();
    Console.WriteLine("範囲持ちの Δ が単体のみの Δ より**大きい**（＝損が小さい）なら、向きが結果に届いている。");

    // --- 群を「範囲/単体」から「自傷率」へ入れ替える（第9期 Phase Y） ---
    //
    // 第6〜8期で、攻撃パターンによる代金の向きは結果に届かないと分かった（代金では ±8pt
    // 開くのに突破度の順位相関は 0.83〜0.94、群差は境目でだけ +0.125 波）。**列も物差しも
    // 変えず、群の定義だけを差し替える**——比較可能性を保つため、ここから上の出力は1行も動かさない。
    //
    // 群分けの連続量は自傷率（= 自傷分 ÷ (敵由来 + 自傷分)。第9期 Phase X の bill と同じ
    // 計算で、測定台 113% の上で測る）。攻撃パターンと違って**編成の内部構造に課金する軸**で、
    // 第5期に目視で見えた「自傷の固定費」がその正体かどうかをここで確かめる。
    Formation[] bench = BenchColumn113();
    bool benchOk = bench.Length == columns[4].Squads.Length
        && Enumerable.Range(0, bench.Length).All(i => SameFormation(bench[i], columns[4].Squads[i]));

    Console.WriteLine();
    Console.WriteLine("## 自傷率で群分けし直す（第9期 Phase Y）");
    Console.WriteLine();
    Console.WriteLine("**列も物差しも第8期のまま。群の定義だけを 範囲/単体 → 自傷率 に差し替えたもの。**");
    Console.WriteLine("自傷率 = 自傷分 ÷ (敵由来 + 自傷分)。測定台 113%（反転列(低)）で味方1部隊の会戦を");
    Console.WriteLine($"seed 0..{BridgeSeeds - 1} 回して測る（`bill` と同じ計算）。");
    Console.WriteLine();
    Console.WriteLine(benchOk
        ? "**検算（測定台）: 自傷率を測った列は 反転列(低) と完全に同一。**"
        : "**測定台が 反転列(低) と違う。自傷率と Δ が別の列の上で測られているので読んではいけない。**");
    Console.WriteLine();

    var selfHarm = Enumerable.Range(0, nT)
        .Select(t => MeasureBill(targets[t].F, bench, BridgeSeeds).SelfHarmRate * 100)
        .ToArray();

    // 地力: 平坦列(低) の突破度(1)。自傷率と地力が交絡していると
    // 「自傷型が弱い」のが自傷のせいなのかたまたま弱い編成なのかを分けられない（§3-3）。
    var grit = Enumerable.Range(0, nT).Select(t => deg1[5, t]).ToArray();

    var sorted = Enumerable.Range(0, nT).OrderByDescending(t => selfHarm[t]).ToArray();
    int third = nT / 3;
    var tier = new int[nT];                       // 0=高自傷 / 1=中 / 2=低
    for (int k = 0; k < nT; k++) tier[sorted[k]] = k < third ? 0 : k < nT - third ? 1 : 2;

    Console.WriteLine("### 自傷率の分布");
    Console.WriteLine();
    var shSorted = selfHarm.OrderBy(x => x).ToArray();
    double shMean = shSorted.Average();
    double shSd = Math.Sqrt(shSorted.Average(x => (x - shMean) * (x - shMean)));
    Console.WriteLine($"編成数 {nT} / 最小 {shSorted[0]:F1}% / 中央 {shSorted[nT / 2]:F1}% / 最大 {shSorted[^1]:F1}% "
        + $"/ 平均 {shMean:F1}% / 標準偏差 {shSd:F1}pt / ちょうど 0% の編成 {selfHarm.Count(x => x == 0)}");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 範 | 自傷率 | 三分位 | 地力（平坦列(低) 突破度） | Δ突破度 反転列(低) |");
    Console.WriteLine("|---|:-:|--:|:-:|--:|--:|");
    foreach (int t in sorted)
        Console.WriteLine($"| {targets[t].Name} | {(HasAoe(targets[t].F) ? "○" : "")} | {selfHarm[t]:F1}% "
            + $"| {(tier[t] == 0 ? "高" : tier[t] == 1 ? "中" : "低")} | {grit[t]:F3} "
            + $"| {deg1[4, t] - deg1[0, t]:+0.000;-0.000} |");
    Console.WriteLine();
    Console.WriteLine("**一様（標準偏差が小さい）なら群分けは意味を持たない。**三分位は自傷率の降順で");
    Console.WriteLine($"上から {third} / {nT - 2 * third} / {third} 編成に切る（自傷率 0% が同数以上あると下位群は同値塊になる）。");

    // --- 三分位の群差（第8期の 範囲/単体 の表と同じ形。直接並べて読めるようにする） ---
    Console.WriteLine();
    Console.WriteLine("### 突破度の変化 Δ（自傷率の三分位。上の 範囲持ち/単体のみ の表と同じ形）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成数 | 自傷率 平均 |"
        + string.Concat(Enumerable.Range(1, nCol - 1).Select(c => $" Δ {columns[c].Name} |")));
    Console.WriteLine("|---|--:|--:|" + string.Concat(Enumerable.Range(1, nCol - 1).Select(_ => "--:|")));
    for (int g = 0; g < 3; g++)
    {
        var grp = Enumerable.Range(0, nT).Where(t => tier[t] == g).ToArray();
        Console.WriteLine($"| {(g == 0 ? "高自傷" : g == 1 ? "中" : "低自傷")} | {grp.Length} "
            + $"| {grp.Average(t => selfHarm[t]):F1}% |"
            + string.Concat(Enumerable.Range(1, nCol - 1)
                .Select(c => $" {grp.Average(t => deg1[c, t] - deg1[0, t]):+0.000;-0.000} |")));
    }
    Console.WriteLine();
    var hi = Enumerable.Range(0, nT).Where(t => tier[t] == 0).ToArray();
    var lo = Enumerable.Range(0, nT).Where(t => tier[t] == 2).ToArray();
    double gap113 = hi.Average(t => deg1[4, t] - deg1[0, t]) - lo.Average(t => deg1[4, t] - deg1[0, t]);
    Console.WriteLine($"**測定台 113%（反転列(低)）での 高自傷 − 低自傷 = {gap113:+0.000;-0.000} 波。**");
    Console.WriteLine("第8期の 範囲持ち − 単体のみ は同じ列で **+0.125 波**（0.237 − 0.112）。この2つを並べて読む。");

    // --- 連続量どうしの相関（群平均より情報量が多い） ---
    Console.WriteLine();
    Console.WriteLine("### 自傷率と Δ突破度の相関（値のピアソン相関。群に潰さない）");
    Console.WriteLine();
    Console.WriteLine("| 列 | 自傷率 × Δ突破度(1部隊) | 自傷率 × Δ突破度(2部隊) | 自傷率 × その列の突破度(1) |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int c = 1; c < nCol; c++)
    {
        var d1 = Enumerable.Range(0, nT).Select(t => deg1[c, t] - deg1[0, t]).ToArray();
        var d2 = Enumerable.Range(0, nT).Select(t => deg2[c, t] - deg2[0, t]).ToArray();
        var lv = Enumerable.Range(0, nT).Select(t => deg1[c, t]).ToArray();
        Console.WriteLine($"| {columns[c].Name} | {Pearson(selfHarm, d1):F2} | {Pearson(selfHarm, d2):F2} "
            + $"| {Pearson(selfHarm, lv):F2} |");
    }

    // --- 交絡（§3-3）。自傷型が弱いのは自傷のせいか、たまたま弱い編成が自傷型なだけか ---
    // Δ を使っているのは地力を引くためだが、引き切れているかは偏相関で確かめる。
    Console.WriteLine();
    Console.WriteLine("### 自傷率と地力の交絡");
    Console.WriteLine();
    var gritFlat = Enumerable.Range(0, nT).Select(t => deg1[0, t]).ToArray();
    Console.WriteLine($"- 自傷率 × 地力（平坦列(低) の突破度(1)）: **{Pearson(selfHarm, grit):F2}**");
    Console.WriteLine($"- 自傷率 × 地力（平坦列 の突破度(1)。Δ の基準側）: {Pearson(selfHarm, gritFlat):F2}");
    Console.WriteLine();
    Console.WriteLine("| 列 | 自傷率 × Δ突破度 | 地力 × Δ突破度 | 地力を制御した偏相関（自傷率 × Δ） |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int c = 1; c < nCol; c++)
    {
        var d = Enumerable.Range(0, nT).Select(t => deg1[c, t] - deg1[0, t]).ToArray();
        double rSG = Pearson(selfHarm, grit), rSD = Pearson(selfHarm, d), rGD = Pearson(grit, d);
        double denom = (1 - rSG * rSG) * (1 - rGD * rGD);
        double partial = denom <= 0 ? double.NaN : (rSD - rSG * rGD) / Math.Sqrt(denom);
        Console.WriteLine($"| {columns[c].Name} | {rSD:F2} | {rGD:F2} | **{partial:F2}** |");
    }
    Console.WriteLine();
    Console.WriteLine("自傷率 × 地力 の絶対値が大きいなら、群差は自傷の効果ではなく地力の差を見ている");
    Console.WriteLine("かもしれない。Δ は地力を引くための差分だが、引き切れているかは偏相関で確かめる。");
    return;
}
}
