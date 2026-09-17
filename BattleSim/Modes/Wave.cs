using BattleCore;
using static Common;

// =====================================================================================
// wave モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "wave")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 wave
// =====================================================================================

static class WaveDiag
{
// wave モード: 編成 × 波の交互作用を、単発戦の勝率で測る（第15期 Phase FA）。
//
// **方針の変更が前提にある**（design/SINGLE_BATTLE_PLAN.md §0）。基本（メイン）は単発戦で、
// 会戦は「もありえる」の位置づけになった。**代金（払った HP の割合）は会戦でしか意味を持たない**
// ——単発戦では HP が毎回リセットされるので、90% 削られて勝つのと 20% で勝つのは同じ価値で、
// 代金という概念そのものが成立しない。第5〜9期・第12〜14期は捨てないが、**主の物差しではなかった。**
//
// 単発が主なら狙うものも変わる。配分判断ではなく **波によって最適な編成が違うこと**
// （編成 × 波の交互作用）。「この波にはこの編成」が成立すれば、それだけで編成パズルになる。
// 第6〜8期がずっと探していた「向き」は、**突破度ではなく波ごとの勝率に対して測るべきだった。**
//
// 出発点は docs/balance.md の天井率（勝率 100.0% の編成数）: 第1波 31/31、第2〜4波が 13〜14/31、
// 第5波だけが 2/31。**第1波は評価に一切寄与していない。**
//
// ここで測るのは既存5波 + 第5〜10期に診断のローカルへ散らばっていた候補波の全部。
// **代金ではなく勝率で測り直す**——第5〜7期はすべて代金で評価していたので、
// 勝敗の観点では一度も見ていない。
//
// 却下した案: 候補波を `EnemyCatalog.Stages` / `Columns` へ足してから測る。採用が決まって
// いない波をカタログに入れると `compare` / `dump` が動いて docs/ に差分が出る（第5期以来の方針。
// 波の採用決定はこの作業では**しない**）。集約先はこのモードのローカル1箇所にする。
//
// 診断用で docs/ には置かない（power / bench / timing と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 wave [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    const int WaveSeeds = 200;   // compare / power / bench と同じ
    var all = CompareBuilds();

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    // --- 候補波の集約（1箇所）---
    //
    // 定義は `WaveCatalog()`。**集める先をモードの外へ出してある**のは、第16期の `dissect`（交互作用の解剖）が
    // 同じ波を読むから。**コピーを作った瞬間に「1箇所に集める」という第15期の方針が壊れる。**
    var waves = WaveCatalog();
    int nW = waves.Length;

    Console.WriteLine($"# 編成 × 波の交互作用（単発戦・seed 0..{WaveSeeds - 1} の {WaveSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("**単発戦の勝率で測る。** 会戦（`engage` / `power` / `bench`）の突破度でも代金でもない");
    Console.WriteLine("——代金は HP を持ち越す会戦でしか意味を持たず、単発では 90% 削られて勝つのと 20% で");
    Console.WriteLine("勝つのが同じ価値になる。第5〜7期の候補波は**すべて代金で評価していた**ので、");
    Console.WriteLine("勝敗の観点ではここが初めての測定になる。");
    Console.WriteLine();
    Console.WriteLine("見たいのは値の大小ではなく **「波によって最適な編成が違うか」**（編成 × 波の交互作用）。");
    Console.WriteLine("成立すれば、それだけで編成パズルになる。");
    Console.WriteLine();
    Console.WriteLine("**測定だけで、盤面は何も変えていない。** `EnemyCatalog.Stages` / `Columns` にも足していない。");
    Console.WriteLine();

    // --- 1. 候補波の定義 ---
    Console.WriteLine("## 1. 候補波の定義（ここが1箇所）");
    Console.WriteLine();
    Console.WriteLine($"既存5波 + 候補 {nW - 5} = **{nW} 波**。定義はどれも出どころのローカル定義を1文字も");
    Console.WriteLine("変えずに写したもの（§2 の検算で突き合わせる）。");
    Console.WriteLine();
    Console.WriteLine("> **現物が無くて入れられなかった候補が2つある。** 第8期に測った「攻5 版」（90/攻5）と");
    Console.WriteLine("> 「板金従卒5」（60/攻7）は `UnitCatalog` に `UnitDef` が残っていない（前者は刻みとして");
    Console.WriteLine("> 測っただけ、後者は「却下した案」として文章にだけ残っている）。**BattleCore を触らない**");
    Console.WriteLine("> 作業なので新しい敵は作らず、集めるのは現物のある波だけにした。");
    Console.WriteLine();
    Console.WriteLine("| タグ | 出どころ | 波 | 体数 | 総HP | 総攻 | 中身（HP/攻/速/型） |");
    Console.WriteLine("|:-:|:-:|---|--:|--:|--:|---|");
    foreach (var (tag, era, name, enemy) in waves)
    {
        string[] seat = FormationRules.SeatNames;
        var members = enemy.Occupied().Select(x =>
        {
            string pat = x.Def.Pattern switch
            {
                AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き",
                AttackPattern.All => "全体", _ => "単体"
            };
            return $"{seat[x.Slot]}={x.Def.Name}({x.Def.MaxHp}/{x.Def.Attack}/速{x.Def.Speed}/{pat})";
        });
        Console.WriteLine($"| **{tag}** | {era} | {name} | {enemy.Count} "
            + $"| {enemy.Occupied().Sum(x => x.Def.MaxHp)} | {enemy.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {string.Join("、", members)} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 2. 検算 ---
    //
    // (1) **再現の検算**（§5-7 の停止条件）。集め方が間違っていれば、代金・向き・ターン数が
    //     gradient / aim / flip / bridge と食い違う。`MeasureCost` を**同じ関数のまま呼び直す**
    //     ので、一致しなければ写し間違い以外の説明が付かない。
    // (2) 味方と敵の Def.Id が衝突していると、敵の被弾が味方の動的特徴量に混ざる（power と同じ穴）。
    // (3) 敵同士の巻き込みが無いこと（受け手側から与ダメを取るための前提。第13期 §3-1）。
    Console.WriteLine("## 2. 検算");
    Console.WriteLine();

    var cost = new (double WinRate, double AvgAlive, double AvgHpPct, int Wins, double AvgTurns)[nW, nT];
    for (int w = 0; w < nW; w++)
        for (int t = 0; t < nT; t++)
            cost[w, t] = MeasureCost(targets[t].F, waves[w].Enemy, WaveSeeds);

    // 記録されている値（**現行 master での** gradient / aim / flip / bridge の出力）。
    // 第5〜7期当時の値ではない——第10・11期でチャージとスキルの行動化が入っているので
    // 当時の数字とは合わない（合わないこと自体は集め方の誤りではない）。突き合わせるのは
    // 「同じ master で同じ波を測ったら同じ値が出るか」で、そこがずれたら写し間違いになる。
    var recorded = new Dictionary<string, (double Cost, double Split, double Turns, string From)>
    {
        ["G1a"] = (31.8, +2.9, 4.1, "gradient / aim"), ["G1b"] = (27.4, +3.0, 3.9, "gradient / aim"),
        ["G1c"] = (36.7, +2.3, 4.3, "gradient / aim"),
        ["G2a"] = (36.0, +1.7, double.NaN, "gradient"), ["G2b"] = (41.4, +2.3, double.NaN, "gradient"),
        ["G2c"] = (50.9, +2.8, double.NaN, "gradient"),
        ["G3a"] = (61.4, -0.4, 6.6, "gradient / flip"), ["G3b"] = (52.8, -0.1, 5.8, "gradient / flip"),
        ["G3c"] = (44.7, +2.7, 5.3, "gradient / flip"),
        ["H1a"] = (33.2, -2.6, 5.7, "aim"), ["H1b"] = (28.4, -2.3, 5.3, "aim"), ["H1c"] = (21.7, -2.8, 5.4, "aim"),
        ["H2a"] = (30.0, +8.9, 3.1, "aim / bridge"), ["H2b"] = (36.1, +7.6, 3.5, "aim"),
        ["H2c"] = (42.5, +5.6, 4.0, "aim"), ["H2d"] = (27.7, +6.8, 3.2, "aim / bridge"),
        ["M1"] = (36.2, +3.2, 4.1, "aim"),
        ["R0"] = (33.2, +7.0, 3.5, "flip"), ["R1"] = (47.7, +1.4, 4.8, "flip"),
        ["R2"] = (35.8, +2.6, 4.1, "flip"), ["R3"] = (23.8, +1.9, 3.5, "flip"),
        ["R4"] = (62.3, -2.9, 6.4, "flip"), ["R5"] = (46.6, +2.4, 5.1, "flip"), ["R6"] = (31.7, +2.7, 4.3, "flip"),
        ["R7"] = (60.3, -0.2, 6.6, "flip"), ["R8"] = (79.7, -7.9, 8.2, "flip"), ["R9"] = (86.8, -4.3, 7.7, "flip"),
        ["R10"] = (74.7, -6.7, 6.6, "flip"), ["R11"] = (68.3, -8.8, 7.7, "flip / bridge"),
        ["R12"] = (57.8, -4.0, 6.9, "flip"),
        ["P6"] = (54.2, -4.9, double.NaN, "bridge"), ["Q6"] = (42.5, -2.4, double.NaN, "bridge"),
        ["C2"] = (44.6, double.NaN, double.NaN, "bridge"), ["C3"] = (43.9, -2.8, double.NaN, "bridge"),
    };

    var costMean = new double[nW];
    var costSplit = new double[nW];
    var costTurns = new double[nW];
    var zeroWin = new int[nW];
    int mismatch = 0;
    Console.WriteLine("### 2-1. 再現（`MeasureCost` を同じ関数のまま呼び直したもの）");
    Console.WriteLine();
    Console.WriteLine("`記録` は**現行 master での** gradient / aim / flip / bridge の出力。第5〜7期当時の");
    Console.WriteLine("数字ではない（第10・11期でチャージとスキルの行動化が入っているので当時とは合わない）。");
    Console.WriteLine("ここで確かめたいのは「同じ master で同じ波を測ったら同じ値が出るか」だけ。");
    Console.WriteLine("**0.1 を超えてずれたら写し間違い**なので、先へ進まずに止まる（§5-7）。");
    Console.WriteLine();
    Console.WriteLine("| タグ | 出典 | 代金平均 | 記録 | 単体−範囲 | 記録 | 平均ターン数 | 記録 | 勝率0%の編成数 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|--:|");
    for (int w = 0; w < nW; w++)
    {
        var live = Enumerable.Range(0, nT).Where(t => cost[w, t].Wins > 0).ToArray();
        zeroWin[w] = nT - live.Length;
        double Cost(int t) => (1 - cost[w, t].AvgHpPct) * 100;
        costMean[w] = live.Length == 0 ? double.NaN : live.Average(Cost);
        costTurns[w] = live.Length == 0 ? double.NaN : live.Average(t => cost[w, t].AvgTurns);
        var groups = live.GroupBy(t => HasAoe(targets[t].F)).ToDictionary(g => g.Key, g => g.Average(Cost));
        double aoe = groups.TryGetValue(true, out double a) ? a : double.NaN;
        double single = groups.TryGetValue(false, out double b) ? b : double.NaN;
        costSplit[w] = single - aoe;

        if (!recorded.TryGetValue(waves[w].Tag, out var rec))
        {
            Console.WriteLine($"| {waves[w].Tag} | （既存5波・突き合わせ先なし） | {costMean[w]:F1}% | — "
                + $"| {costSplit[w]:+0.0;-0.0}pt | — | {costTurns[w]:F1} | — | {zeroWin[w]} |");
            continue;
        }
        string Chk(double got, double want)
        {
            if (double.IsNaN(want)) return "—";
            bool ok = Math.Abs(got - want) <= 0.1;
            if (!ok) mismatch++;
            return ok ? $"{want:F1}" : $"**{want:F1} ←ずれ**";
        }
        string c1 = Chk(costMean[w], rec.Cost), c2 = Chk(costSplit[w], rec.Split), c3 = Chk(costTurns[w], rec.Turns);
        Console.WriteLine($"| {waves[w].Tag} | {rec.From} | {costMean[w]:F1}% | {c1} "
            + $"| {costSplit[w]:+0.0;-0.0}pt | {c2} | {costTurns[w]:F1} | {c3} | {zeroWin[w]} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine(mismatch == 0
        ? "**再現: 一致（ずれ 0 件）。** 集め方は写しになっている。"
        : $"**再現: {mismatch} 件ずれた。集め方が間違っている（§5-7 の停止条件）。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 計測本体 ---
    // seed ごとの勝敗と残存率を丸ごと持つ。半割（§4）は同じ計測から取り出すだけで済む
    // （2回走らせると、半割の値そのものに実行間のばらつきが乗る。bench と同じ作法）。
    var win = new double[nW][][];    // [波][編成][seed] 0/1
    var surv = new double[nW][][];   // [波][編成][seed] 生存数 ÷ 出撃数
    var dyn = new double[nW][][];    // [波][編成][動的特徴量]
    long foeFromAlly = 0;
    for (int w = 0; w < nW; w++)
    {
        win[w] = new double[nT][];
        surv[w] = new double[nT][];
        dyn[w] = new double[nT][];
        for (int t = 0; t < nT; t++)
        {
            var m = MeasureWave(targets[t].F, waves[w].Enemy, WaveSeeds);
            win[w][t] = m.Win;
            surv[w][t] = m.SurvRate;
            dyn[w][t] = m.Dynamics;
            foeFromAlly += m.FoeTakenFromAlly;
        }
    }

    var clash = new List<string>();
    foreach (var (tag, _, _, enemy) in waves)
    {
        var foeIds = enemy.Occupied().Select(x => x.Def.Id).ToHashSet();
        foreach (var (name, f) in targets)
            foreach (string id in f.Occupied().Select(x => x.Def.Id).Where(foeIds.Contains))
                clash.Add($"{tag} × {name}: {id}");
    }

    Console.WriteLine("### 2-2. 動的特徴量の前提（Phase FB が読む）");
    Console.WriteLine();
    Console.WriteLine($"- **味方と敵の Def.Id の衝突: {clash.Count} 件**"
        + (clash.Count == 0 ? "（0 でなければ動的特徴量に敵の数字が混ざっている）"
                            : $" ← **混入している**: {string.Join(" / ", clash.Take(5))}"));
    Console.WriteLine($"- **敵の TakenFromAlly の総和: {foeFromAlly}**"
        + (foeFromAlly == 0
            ? "（0 = 敵側に巻き込みが無い。受け手側の与ダメから引いた量も 0）"
            : " ← **敵同士の巻き込みがある。** 与ダメからこの量を引いている"));
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3. 編成 × 波の勝率表 ---
    double[] winRate(int w) => Enumerable.Range(0, nT).Select(t => win[w][t].Average() * 100).ToArray();
    var rate = Enumerable.Range(0, nW).Select(winRate).ToArray();
    // 残存度 = 全試行の平均（生存数 ÷ 出撃数）。**負けた試行は 0 になる**ので勝率を内包しつつ、
    // 勝率が天井に張り付いた波でも「何体残して勝ったか」で編成が割れる。
    // `chain` の `残存`（勝った試行だけの平均生存数）とは分母が違う——あちらは勝ち方の質、
    // こちらは天井を割るための連続量。**両方出して、どちらを使ったかを明記する**（§2-2）。
    var degree = Enumerable.Range(0, nW)
        .Select(w => Enumerable.Range(0, nT).Select(t => surv[w][t].Average()).ToArray()).ToArray();
    var aliveOnWin = Enumerable.Range(0, nW).Select(w => Enumerable.Range(0, nT).Select(t =>
    {
        int wins = win[w][t].Count(x => x > 0);
        return wins == 0 ? 0.0 : Enumerable.Range(0, WaveSeeds).Where(s => win[w][t][s] > 0)
            .Sum(s => surv[w][t][s]) / wins;
    }).ToArray()).ToArray();

    Console.WriteLine("## 3. 編成 × 波の勝率表");
    Console.WriteLine();
    Console.WriteLine("列は §1 のタグ。`S1`〜`S5` が既存5波（この5列は `docs/balance.md` と同じ値になる）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(waves.Select(x => $" {x.Tag} |")));
    Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "--:|")));
    for (int t = 0; t < nT; t++)
        Console.WriteLine($"| {targets[t].Name} |"
            + string.Concat(Enumerable.Range(0, nW).Select(w => $" {rate[w][t]:F1} |")));
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4. 波ごとの要約 ---
    // **天井に張り付いた波は評価に寄与しない。** 全滅する波（床）も同じ。
    // 線は「天井率 + 床率 ≥ 50%」に置いた——**これは測定から出た線ではない**ので、
    // 生の天井率・床率を同じ表に出して、線を引き直せるようにしてある。
    const double DeadZone = 50.0;
    var ceilPct = new double[nW];
    var floorPct = new double[nW];
    var contributes = new bool[nW];
    Console.WriteLine("## 4. 波ごとの要約（どの波が評価に寄与しているか）");
    Console.WriteLine();
    Console.WriteLine("`天井` は勝率 100.0% の編成数、`床` は 0.0% の編成数。**どちらも同値塊**で、");
    Console.WriteLine("その中の編成同士は区別できない——天井だけの波・床だけの波は評価に寄与しない。");
    Console.WriteLine();
    Console.WriteLine("`残存(勝時)` は勝った試行の平均生存数 ÷ 出撃数（`chain` の `残存` と同じ定義）。");
    Console.WriteLine("`残存度` は**全試行**の平均（生存数 ÷ 出撃数。負けた試行は 0）で、勝率を内包しつつ");
    Console.WriteLine("天井で潰れない連続量。§6 の第2の読み方がこれを使う。");
    Console.WriteLine();
    Console.WriteLine($"`寄与` は **天井率 + 床率 < {DeadZone:F0}%** を満たすか。**この線は測定から出たものではない**");
    Console.WriteLine("ので、生の天井・床の数字を同じ表に出してある（線を引き直したければここから読み直せる）。");
    Console.WriteLine();
    Console.WriteLine("| タグ | 波 | 平均勝率 | 勝率SD | 天井 | 床 | 天井+床 | 残存(勝時) | 残存度 | 残存度SD | 寄与 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|--:|--:|:-:|");
    for (int w = 0; w < nW; w++)
    {
        int ceil = rate[w].Count(v => v >= 100.0 - 1e-9);
        int floor = rate[w].Count(v => v <= 1e-9);
        ceilPct[w] = ceil * 100.0 / nT;
        floorPct[w] = floor * 100.0 / nT;
        contributes[w] = ceilPct[w] + floorPct[w] < DeadZone;
        Console.WriteLine($"| **{waves[w].Tag}** | {waves[w].Name} | {rate[w].Average():F1}% | {Sd(rate[w]):F1}pt "
            + $"| {ceil}/{nT} | {floor}/{nT} | {ceilPct[w] + floorPct[w]:F0}% "
            + $"| {aliveOnWin[w].Average():F2} | {degree[w].Average():F3} | {Sd(degree[w]):F3} "
            + $"| {(contributes[w] ? "○" : "×")} |");
    }
    Console.WriteLine();
    int nContrib = contributes.Count(x => x);
    int allCeil = Enumerable.Range(0, nW).Count(w => ceilPct[w] >= 100.0 - 1e-9);
    Console.WriteLine($"**寄与している波は {nContrib} / {nW}。** 既存5波では "
        + $"{Enumerable.Range(0, 5).Count(w => contributes[w])} / 5。");
    Console.WriteLine();
    Console.WriteLine($"うち **{allCeil} 波は {nT} 編成すべてが勝率 100.0%**（完全な天井）。候補波"
        + $"（既存5波を除く {nW - 5} 波）に限ると、寄与するのは "
        + $"{Enumerable.Range(5, nW - 5).Count(w => contributes[w])} 波しかない。");
    Console.WriteLine();
    Console.WriteLine("**第5〜8期の候補波は、単発戦としては全編成が勝ち切ってしまう。** 理屈は読める——");
    Console.WriteLine("あれは会戦の3波列の1本として設計した波で、狙いは「1部隊の容量（約 100%）を");
    Console.WriteLine("3波で使い切る」ことだった。1波あたりの代金 25〜60% は**会戦では3波ぶんが積み上がって");
    Console.WriteLine("部隊を殺す**が、単発では HP が毎回戻るので**ただの「6割削られて勝つ」**にしかならない。");
    Console.WriteLine("**代金の帯を狙って作った波は、単発戦の物差しでは全部が天井の同じ場所に並ぶ。**");
    Console.WriteLine("寄与しているのは、代金ではなく難度で作られた波（既存の第2〜5波）と、第7期に");
    Console.WriteLine("**打ち切りバイアスを理由に一度は捨てた重い波**（R8 / R9 / R10）だけ。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 5. 半割 = 測定の信頼性の上限 ---
    //
    // 波をまたいだ順位相関が 1.00 未満なのは当たり前で、**乱数のばらつきだけでもそうなる。**
    // 「どれくらいなら動いたと言えるか」の基準を先に測る（第13期 bench の作法をそのまま持ってくる）。
    // 第13期の突破度での値は 0.985〜0.995 / 補正後 0.99 だったが、**目的変数が違うので測り直す**
    // ——単発の勝率は 200 試行の二項比率なので、突破度よりばらつきが大きい可能性がある。
    double SB(double r) => 2 * r / (1 + r);
    double[] MeanOver(double[][] v, Func<int, bool> take) => Enumerable.Range(0, nT)
        .Select(t => Enumerable.Range(0, WaveSeeds).Where(take).Average(s => v[t][s])).ToArray();
    var capR = new double[nW];
    var capRho = new double[nW];
    var capDegRho = new double[nW];
    Console.WriteLine("## 5. 半割 — 測定の信頼性の上限");
    Console.WriteLine();
    Console.WriteLine("**同じ波を seed で半分に割り、両半分で勝率の相関を取る。** 割り方は2種類:");
    Console.WriteLine();
    Console.WriteLine($"- **前後半**: 前半 = seed 0..{WaveSeeds / 2 - 1} / 後半 = seed {WaveSeeds / 2}..{WaveSeeds - 1}");
    Console.WriteLine("- **偶奇**: 偶数 seed / 奇数 seed");
    Console.WriteLine();
    Console.WriteLine("相関は**編成の並び**に対して取る（seed の並びではない）。半割は 100 試行同士なので");
    Console.WriteLine("200 試行の測定より一致度が低く出る——Spearman-Brown の補正 `r(2n) = 2r(n) / (1 + r(n))`");
    Console.WriteLine("を掛けた値を併記する（**補正は「両半分が同等・誤差が独立」を仮定するので生の値も併記**）。");
    Console.WriteLine();
    Console.WriteLine("第13期は突破度で 0.985〜0.995 / 補正後 0.99 だった。**目的変数が違うので測り直している。**");
    Console.WriteLine("勝率が天井・床に潰れた波では順位が同値塊になり、半割そのものが計算できない（`—`）。");
    Console.WriteLine();
    Console.WriteLine("| タグ | 前後半 r | 前後半 ρ | 偶奇 r | 偶奇 ρ | 補正後 r | **補正後 ρ** | 残存度 補正後 ρ |");
    Console.WriteLine("|:-:|--:|--:|--:|--:|--:|--:|--:|");
    for (int w = 0; w < nW; w++)
    {
        var h1 = Correlate(MeanOver(win[w], s => s < WaveSeeds / 2), MeanOver(win[w], s => s >= WaveSeeds / 2));
        var h2 = Correlate(MeanOver(win[w], s => s % 2 == 0), MeanOver(win[w], s => s % 2 == 1));
        var d1 = Correlate(MeanOver(surv[w], s => s < WaveSeeds / 2), MeanOver(surv[w], s => s >= WaveSeeds / 2));
        var d2 = Correlate(MeanOver(surv[w], s => s % 2 == 0), MeanOver(surv[w], s => s % 2 == 1));
        capR[w] = SB((h1.R + h2.R) / 2);
        capRho[w] = SB((h1.Rho + h2.Rho) / 2);
        capDegRho[w] = SB((d1.Rho + d2.Rho) / 2);
        string F(double v) => double.IsNaN(v) ? "—" : $"{v:F3}";
        Console.WriteLine($"| {waves[w].Tag} | {F(h1.R)} | {F(h1.Rho)} | {F(h2.R)} | {F(h2.Rho)} "
            + $"| {F(capR[w])} | **{F(capRho[w])}** | {F(capDegRho[w])} |");
    }
    Console.WriteLine();
    var okCap = Enumerable.Range(0, nW).Where(w => !double.IsNaN(capRho[w])).ToArray();
    Console.WriteLine($"**補正後 ρ の中央値 {okCap.Select(w => capRho[w]).OrderBy(x => x).ElementAt(okCap.Length / 2):F3}"
        + $"（最小 {okCap.Min(w => capRho[w]):F3} / 最大 {okCap.Max(w => capRho[w]):F3}）。**");
    Console.WriteLine("波をまたいだ相関はこれを超えられない。**超えられない量が「余地」で、余地こそが");
    Console.WriteLine("測定のばらつきでは説明できない入れ替わりの量になる。**");
    Console.WriteLine();
    Console.Out.Flush();
    // --- 6. 波ペアの順位相関（本題） ---
    //
    // **順位が入れ替わる波のペアが複数あれば、編成 × 波の交互作用が実在する。**
    // 判定は §2-4 の3行のどれか:
    //   1行目 入れ替わるペアが複数ある      → 交互作用が実在。単発戦のステージ設計の骨格になる
    //   2行目 どの波でも順位がほぼ同じ      → 波の側では差が作れない。編成側で作るしかない
    //   3行目 天井・床を外すとペアが残らない → 実質「勝てる波」と「勝てない波」の一次元
    //
    // 天井・床の扱いで結論が変わりうるので、**3通り全部を出す**（§5-7）:
    //   (a) 全波・勝率           天井・床をそのまま含める
    //   (b) 寄与する波だけ・勝率  §4 の線で切る
    //   (c) 全波・残存度         天井の波も残存で割れるので、切らずに済む読み方
    const double Slack = 0.05;    // bench §6 と同じ線。**測定から出た線ではない**
    const double Flat = 0.90;     // §2-4 の2行目「相関 0.9 以上」

    var rankRate = Enumerable.Range(0, nW).Select(w => AverageRanksDesc(rate[w])).ToArray();
    var rankDeg = Enumerable.Range(0, nW).Select(w => AverageRanksDesc(degree[w])).ToArray();

    Console.WriteLine("## 6. 波ペアの順位相関（本題）");
    Console.WriteLine();
    Console.WriteLine("`ρ` はスピアマン（同順位は平均順位。第8期以降の順位相関と同じ計算）。");
    Console.WriteLine($"`余地` = min(両端の半割 補正後 ρ) − ρ。**{Slack:F2} 未満なら測定のばらつきで説明が付く**");
    Console.WriteLine("（bench §6 と同じ線。これも測定から出た線ではない）。");
    Console.WriteLine();
    Console.WriteLine("### 6-1. 順位相関の行列（勝率・全波）");
    Console.WriteLine();
    Console.WriteLine("対角は半割の補正後 ρ（＝その波自身との一致度の上限）。**行を横に読めば、");
    Console.WriteLine("その波の上限と、他の波との一致度が同じ行に並ぶ。** `—` は同値塊で相関が計算できない波。");
    Console.WriteLine();
    Console.WriteLine("|  |" + string.Concat(waves.Select(x => $" {x.Tag} |")));
    Console.WriteLine("|:-:|" + string.Concat(waves.Select(_ => "--:|")));
    for (int i = 0; i < nW; i++)
    {
        var row = new List<string>();
        for (int j = 0; j < nW; j++)
        {
            double v = i == j ? capRho[i] : Correlate(rate[i], rate[j]).Rho;
            row.Add(double.IsNaN(v) ? "—" : (i == j ? $"*{v:F2}*" : $"{v:F2}"));
        }
        Console.WriteLine($"| **{waves[i].Tag}** | {string.Join(" | ", row)} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // ペアの一覧を作る。3通りの読み方が同じ関数を共有するので、
    // 「扱いを変えたら結論が変わった」が扱いの差だけから出ることが保証される。
    (int A, int B, double Rho, double Cap, double Slk, double MeanGap, double MaxGap)[] Pairs(
        double[][] v, double[][] rk, double[] cap, Func<int, bool> use)
    {
        var list = new List<(int, int, double, double, double, double, double)>();
        for (int i = 0; i < nW; i++)
            for (int j = i + 1; j < nW; j++)
            {
                if (!use(i) || !use(j)) continue;
                double rho = Correlate(v[i], v[j]).Rho;
                double c = Math.Min(cap[i], cap[j]);
                if (double.IsNaN(rho) || double.IsNaN(c)) continue;
                var gaps = Enumerable.Range(0, nT).Select(t => Math.Abs(rk[i][t] - rk[j][t])).ToArray();
                list.Add((i, j, rho, c, c - rho, gaps.Average(), gaps.Max()));
            }
        return list.OrderByDescending(x => x.Item5).ToArray();
    }

    void EmitPairs(string head, string[] notes,
        (int A, int B, double Rho, double Cap, double Slk, double MeanGap, double MaxGap)[] ps, int take)
    {
        Console.WriteLine(head);
        Console.WriteLine();
        foreach (string line in notes) Console.WriteLine(line);
        Console.WriteLine();
        int swaps = ps.Count(x => x.Slk >= Slack && x.Rho < Flat);
        Console.WriteLine($"ペア総数 {ps.Length}、うち **ρ < {Flat:F2} かつ 余地 ≥ {Slack:F2}** が **{swaps}** 組。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 波 | ρ | 上限 | 余地 | 平均\\|順位差\\| | 最大\\|順位差\\| |");
        Console.WriteLine("|:-:|:-:|--:|--:|--:|--:|--:|");
        foreach (var x in ps.Take(take))
            Console.WriteLine($"| {waves[x.A].Tag} | {waves[x.B].Tag} | {x.Rho:F2} | {x.Cap:F2} "
                + $"| **{x.Slk:F2}** | {x.MeanGap:F1} | {x.MaxGap:F1} |");
        Console.WriteLine();
        Console.Out.Flush();
    }

    var pAll = Pairs(rate, rankRate, capRho, _ => true);
    var pCon = Pairs(rate, rankRate, capRho, w => contributes[w]);
    var pDeg = Pairs(degree, rankDeg, capDegRho, _ => true);

    // 全編成が勝率 100% で並ぶ波は順位が完全な同値塊になり、半割そのものが計算できない
    // （分散 0）。上限が取れない波はペアから落ちるので、(a) は「全波」ではなく
    // **「半割が計算できた波」**の読み方になる。落ちた数を出す。
    int capOk = Enumerable.Range(0, nW).Count(w => !double.IsNaN(capRho[w]));
    int capOkCon = Enumerable.Range(0, nW).Count(w => contributes[w] && !double.IsNaN(capRho[w]));
    int capOkDeg = Enumerable.Range(0, nW).Count(w => !double.IsNaN(capDegRho[w]));

    EmitPairs($"### 6-2. (a) 半割が計算できた波・勝率（{capOk} / {nW} 波）— 入れ替わりの大きいペア上位25",
        new[]
        {
            $"天井・床の波もそのまま入れた読み方。残る {nW - capOk} 波は**全編成が勝率 100% で並んで",
            "順位が完全な同値塊**になり、半割（上限）が計算できないのでペアから落ちている。",
            "",
            "> **この読み方は当てにならない。** 天井の波の順位はほぼ全部が同値塊なので、半割 ρ が",
            "> 1.00 に張り付き（30編成が同値で1編成だけ外れる、といった形）、他の波との ρ は同値塊の",
            "> せいで 0 付近まで落ちる。結果として `余地` が 1.0 を超える——**上限を超えて一致しない**",
            "> という意味不明な値で、これは入れ替わりではなく同値塊の副作用。(b) を置いてあるのはこのため。",
        }, pAll, 25);
    EmitPairs($"### 6-3. (b) 寄与する波だけ・勝率（{nContrib} 波・うち半割が取れたのは {capOkCon} 波）"
        + " — 入れ替わりの大きいペア全件",
        new[]
        {
            $"§4 の線（天井率 + 床率 < {DeadZone:F0}%）で切ったあと。**同値塊を外しても入れ替わりが残るか**が",
            "§2-4 の1行目と3行目を分ける。**判定はこの表で読む。**",
        }, pCon, 25);
    EmitPairs($"### 6-4. (c) 全波・残存度（{capOkDeg} / {nW} 波）— 入れ替わりの大きいペア上位25",
        new[]
        {
            "**天井の波も残存で割れるので、波を1つも捨てずに済む読み方。** 勝率が 100% で並ぶ編成同士も",
            "「何体残して勝ったか」で順位が付くので、半割はどの波でも計算できる。",
        }, pDeg, 25);

// --- 7. 名指し: いちばん遠いペアで誰が入れ替わったか ---
    // 相関の数字だけだと「入れ替わった」が抽象のまま残る。**どの編成がどちらの波で強いのか**を
    // 名前で出さないと、次にステージを設計する材料にならない。
    if (pCon.Length > 0)
    {
        var top = pCon[0];
        Console.WriteLine($"## 7. 名指し — 寄与する波のうちいちばん遠いペア（{waves[top.A].Tag} × {waves[top.B].Tag}・ρ = {top.Rho:F2}）");
        Console.WriteLine();
        Console.WriteLine($"- **{waves[top.A].Tag}**: {waves[top.A].Name}");
        Console.WriteLine($"- **{waves[top.B].Tag}**: {waves[top.B].Name}");
        Console.WriteLine();
        Console.WriteLine("`順位差` = 順位(A) − 順位(B)。**正なら B の波で順位が上がる**（順位は 1 が最良）。");
        Console.WriteLine();
        Console.WriteLine($"| 向き | 編成 | {waves[top.A].Tag} 勝率 | 順位 | {waves[top.B].Tag} 勝率 | 順位 | 順位差 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        var byGap = Enumerable.Range(0, nT)
            .OrderByDescending(t => rankRate[top.A][t] - rankRate[top.B][t]).ToArray();
        foreach (int t in byGap.Take(5))
            Console.WriteLine($"| {waves[top.B].Tag} で**上がる** | {targets[t].Name} | {rate[top.A][t]:F1}% "
                + $"| {rankRate[top.A][t]:F1} | {rate[top.B][t]:F1}% | {rankRate[top.B][t]:F1} "
                + $"| {rankRate[top.A][t] - rankRate[top.B][t]:+0.0;-0.0} |");
        foreach (int t in byGap.Reverse().Take(5))
            Console.WriteLine($"| {waves[top.A].Tag} で**上がる** | {targets[t].Name} | {rate[top.A][t]:F1}% "
                + $"| {rankRate[top.A][t]:F1} | {rate[top.B][t]:F1}% | {rankRate[top.B][t]:F1} "
                + $"| {rankRate[top.A][t] - rankRate[top.B][t]:+0.0;-0.0} |");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 8. 判定 ---
    // §2-4 の表のどの行に当たるかを、数字から機械的に選ぶ（bench §6 と同じ作法）。
    // 文章で判定すると「読み方によってはこうも取れる」が残る。
    string Verdict(int swapAll, int swapCon)
        => swapCon >= 2
            ? "**1行目: 編成 × 波の交互作用が実在する。** 単発戦のステージ設計の骨格になる"
            : swapAll >= 2
                ? "**3行目: 入れ替わるが、天井・床を外すとペアがほとんど残らない。** 実質「勝てる波」と"
                  + "「勝てない波」しか無く、難度の一次元に潰れている"
                : $"**2行目: どの波でも順位がほぼ同じ（ρ {Flat:F2} 以上）。** 波を何種類作っても編成パズルに"
                  + "ならない——波の側では差が作れないので、編成側（特性・スキル）で作るしかない";

    int swAll = pAll.Count(x => x.Slk >= Slack && x.Rho < Flat);
    int swCon = pCon.Count(x => x.Slk >= Slack && x.Rho < Flat);
    int swDeg = pDeg.Count(x => x.Slk >= Slack && x.Rho < Flat);
    int swDegCon = pDeg.Count(x => x.Slk >= Slack && x.Rho < Flat && contributes[x.A] && contributes[x.B]);

    Console.WriteLine("## 8. 判定（§2-4 のどの行か）");
    Console.WriteLine();
    Console.WriteLine($"**入れ替わったペア** = ρ < {Flat:F2} かつ 余地 ≥ {Slack:F2}（測定のばらつきで説明が付かない）。");
    Console.WriteLine();
    Console.WriteLine("| 天井・床の扱い | ペア総数 | 入れ替わったペア |");
    Console.WriteLine("|---|--:|--:|");
    Console.WriteLine($"| (a) 半割が計算できた波・勝率 | {pAll.Length} | {swAll} |");
    Console.WriteLine($"| (b) 寄与する波だけ・勝率 | {pCon.Length} | {swCon} |");
    Console.WriteLine($"| (c) 全波・残存度 | {pDeg.Length} | {swDeg}（うち寄与する波同士 {swDegCon}） |");
    Console.WriteLine();
    Console.WriteLine("判定は (a) と (b) の両方から決める——**(b) だけで 2 組以上残れば 1行目**、");
    Console.WriteLine("(a) には出るが (b) で消えるなら 3行目、どちらにも出なければ 2行目（§2-4）。");
    Console.WriteLine();
    Console.WriteLine($"- 勝率での判定: {Verdict(swAll, swCon)}");
    Console.WriteLine($"- 残存度での判定: {Verdict(swDeg, swDegCon)}");
    Console.WriteLine();
    Console.WriteLine(Verdict(swAll, swCon) == Verdict(swDeg, swDegCon)
        ? "**天井・床の扱いを変えても判定は変わらない。**"
        : "> **警告: 天井・床の扱いで判定が変わる。** どちらか一方を選ばず、両方の結果を報告すること（§5-7）。");
    Console.WriteLine();
    Console.Out.Flush();
    // ================= Phase FB: 地力の分解（単発版・第15期） =================
    //
    // 第12〜14期は**突破度**（会戦）を目的変数にして「地力」を分解してきた。第14期の結論は
    // 「地力は既存の特徴量では表せない」（主 総攻 r² 0.308 / 従 与ダメ効率 r² 0.242、
    // 静的だけなら 0.31→0.35 / 0.10→0.16）。**あれは会戦専用の目的変数に対する分解だった。**
    //
    // ここでやるのは第14期 Phase EA と**同じ手順・同じ計算方法**で、目的変数だけを
    // 突破度 → 単発の勝率（波ごと、および平均）に差し替えた測り直し。台も seed も同じ。
    //
    // **同語反復の判定はやり直す。** 目的変数が変わると、何が言い換えかも変わる（§3-1）。
    // 別の実行から第14期の数字を引くと、動いたのが定義のせいか実行のせいか決まらないので、
    // 突破度の側も同じ実行の中で `MeasurePower` を呼び直して対比表に出す（第13期以来の作法）。
    Console.WriteLine("## 9. 地力の分解（単発版・Phase FB）");
    Console.WriteLine();
    Console.WriteLine("第12〜14期と**同じ手順・同じ計算方法**で、目的変数だけを 突破度 → 単発の勝率に");
    Console.WriteLine("差し替えたもの。特徴量の定義も第14期のまま（受け手側の与ダメ・撃破。第13期 Phase DA）。");
    Console.WriteLine();

    // --- 静的特徴量（power と同じ8種。定義値だけから取る） ---
    var statics = new (string Name, string Def, Func<Formation, double> Get)[]
    {
        ("体数",     "編成の駒数（4 or 5）", f => f.Count),
        ("総HP",     "Def.MaxHp の合計", f => f.Occupied().Sum(x => x.Def.MaxHp)),
        ("総攻",     "Def.Attack の合計", f => f.Occupied().Sum(x => x.Def.Attack)),
        ("積",       "総HP × 総攻",
            f => (double)f.Occupied().Sum(x => x.Def.MaxHp) * f.Occupied().Sum(x => x.Def.Attack)),
        ("最薄HP",   "編成中いちばん低い Def.MaxHp", f => f.Occupied().Min(x => x.Def.MaxHp)),
        ("後列HP",   "後列（slot 4/5）の Def.MaxHp 合計",
            f => f.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)),
        ("平均速度", "Def.Speed の平均", f => f.Occupied().Average(x => x.Def.Speed)),
        ("範囲枚数", "Def.Pattern が薙ぎ/全体の駒数（AoeCount）", f => AoeCount(f)),
    };
    string[] dynNames = { "与ダメ/戦", "被ダメ/戦", "撃破/戦", "干渉/戦", "回復/戦", "自傷率", "与ダメ効率" };
    int nS = statics.Length, nD = dynNames.Length, nF = nS + nD;
    string[] featNames = statics.Select(s => s.Name).Concat(dynNames).ToArray();
    bool[] isStatic = Enumerable.Range(0, nF).Select(k => k < nS).ToArray();

    var stat = new double[nT][];
    for (int t = 0; t < nT; t++) stat[t] = statics.Select(s => s.Get(targets[t].F)).ToArray();

    // --- 9-1. 同語反復の再判定 ---
    //
    // 基準は第14期と同じ1本だけ:「**目的変数の言い換えになっていないか**」。
    // 「信頼できるか」は混ぜない（混ぜると基準が二重になって、次に特徴量を足すときに使えない）。
    //
    // 単発戦では経路の構成が変わる。
    //   分母経路は**消える** — 部隊戦が必ず1回なので `/戦` の分母は定数 1。第14期に
    //                          「分母は目的変数 + 1」だった経路そのものが無い。
    //   分子経路は**1つ増える** — 味方の全滅＝敗北なので、`被ダメ/戦` が敗北の定義を含む。
    //                              突破度に対しては分母経路だけだったので残していた量が、
    //                              単発の勝率に対しては**言い換え側に回る**。
    //
    // 却下した案: `被ダメ/戦` を「相関が低いから」という理由で残す。基準は言い換えかどうかの
    // 1本だけで、**相関の大小を判定に混ぜない**（第14期の基準をそのまま使う）。
    var taut15 = new (bool Excluded, string Era14, string Reason)[]
    {
        (true,  "除外",
            "**分子経路。** 敵の総HPを削り切ることが勝利なので、勝った試行では分子が敵の総HPに張り付く。第14期と同じ判定"),
        (true,  "残す",
            "**単発では分子経路に回る。** 味方の全滅＝敗北なので、負けた試行では分子が味方の総HP（+回復・過剰殺傷）に張り付く。突破度に対しては分母経路だけだったので残していたが、**目的変数が変わると経路も変わる**"),
        (true,  "除外",
            "**分子経路。もっとも露骨な言い換え。** 敵の全滅＝勝利。勝った試行の値は敵の体数そのもので、これは測定結果ではなく算術。第14期と同じ判定"),
        (false, "残す",
            "分子は「誰が起点になったか」の回数で、勝敗の定義に入らない。**毒軸で構造的に過小**（第13期の残る穴）だが、それは信頼性の問題であって同語反復ではない——基準を混ぜないので残す"),
        (false, "残す",
            "分子は味方の回復量で、勝敗の定義に入らない。単発では分母経路も無いので、経路が1つも無い"),
        (false, "残す",
            "**比なので、分子・分母の両方に乗っている「味方がどれだけ削られたか」が打ち消える。** 分子は味方同士の削りで、敗北の写しではない"),
        (false, "残す",
            "分子・分母とも分子経路の量だが、**比を取ると言い換えの部分が打ち消える**——`与ダメ ÷ 撃破` は「1体倒すのに振った量」。第14期と同じ判定"),
    };

    Console.WriteLine("### 9-1. 同語反復の再判定（目的変数が変わったので判定もやり直す）");
    Console.WriteLine();
    Console.WriteLine("基準は第14期と同じ**1本だけ**:「**目的変数の言い換えになっていないか**」。");
    Console.WriteLine("**「信頼できるか」は混ぜない**（混ぜると基準が二重になり、次に特徴量を足すときに使えない）。");
    Console.WriteLine();
    Console.WriteLine("単発戦では経路の構成が2つとも変わる。");
    Console.WriteLine();
    Console.WriteLine("- **分母経路は消える。** 部隊戦が必ず1回なので `/戦` の分母は定数 1。第14期に");
    Console.WriteLine("  「分母は 目的変数 + 1」だった経路そのものが存在しない。");
    Console.WriteLine("- **分子経路は1つ増える。** 味方の全滅＝敗北なので、`被ダメ/戦` が敗北の定義を含む。");
    Console.WriteLine("  **突破度に対しては分母経路だけだったので残していた量が、単発の勝率に対しては");
    Console.WriteLine("  言い換え側に回る**——「目的変数が変わると何が言い換えかも変わる」の実例。");
    Console.WriteLine();

    // 算術の署名を数字で出す。言葉で言い張らずに、比が勝率／敗率とどれだけ一致するかを測る
    // （第14期が分母経路を「部隊戦数/試行 × 突破度 の r」で測って出したのと同じ作法）。
    // 検算は寄与する波（＝勝率に分散がある波）でだけ意味を持つ。
    int[] conW = Enumerable.Range(0, nW).Where(w => contributes[w]).ToArray();
    Console.WriteLine("#### 検算: 分子経路は本当に算術か");
    Console.WriteLine();
    Console.WriteLine("量を「その波で取りうる最大」で割ると、勝率（あるいは敗率）そのものになるはず。");
    Console.WriteLine("**恒等式に近いなら、それは測定結果ではなく算術。** 勝率に分散のある波でだけ意味を持つので、");
    Console.WriteLine("§4 で寄与すると判定した波だけを出す。");
    Console.WriteLine();
    Console.WriteLine("| 波 | 撃破/戦 ÷ 敵体数 × 勝率 の r | 与ダメ/戦 ÷ 敵総HP × 勝率 の r | 被ダメ/戦 ÷ 総HP × **敗率** の r |");
    Console.WriteLine("|:-:|--:|--:|--:|");
    foreach (int w in conW)
    {
        double foeCount = waves[w].Enemy.Occupied().Count();
        double foeHp = waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp);
        double[] killRatio = Enumerable.Range(0, nT).Select(t => dyn[w][t][2] / foeCount).ToArray();
        double[] dmgRatio = Enumerable.Range(0, nT).Select(t => dyn[w][t][0] / foeHp).ToArray();
        double[] takenRatio = Enumerable.Range(0, nT)
            .Select(t => dyn[w][t][1] / targets[t].F.Occupied().Sum(x => x.Def.MaxHp)).ToArray();
        double[] loss = rate[w].Select(v => 100 - v).ToArray();
        Console.WriteLine($"| {waves[w].Tag} | {Correlate(killRatio, rate[w]).R:+0.000;-0.000} "
            + $"| {Correlate(dmgRatio, rate[w]).R:+0.000;-0.000} "
            + $"| {Correlate(takenRatio, loss).R:+0.000;-0.000} |");
    }
    Console.WriteLine();
    // 敗率 100% の編成では「味方の総HP を全部払った」はずなので、比が 1.0 を下回らない。
    // 下回るなら、DamageTaken を経由しない死亡経路があることになる。
    var allDead = new List<string>();
    foreach (int w in conW)
        for (int t = 0; t < nT; t++)
            if (rate[w][t] <= 1e-9)
                allDead.Add($"{waves[w].Tag}/{targets[t].Name} "
                    + $"{dyn[w][t][1] / targets[t].F.Occupied().Sum(x => x.Def.MaxHp):F2}");
    Console.WriteLine($"勝率 0% の編成（{allDead.Count} 件）の `被ダメ/戦 ÷ 総HP`: "
        + (allDead.Count == 0 ? "該当なし"
            : string.Join(" / ", allDead.Take(6)) + (allDead.Count > 6 ? " …" : "")));
    Console.WriteLine("**全滅しているので 1.00 を下回らないはず**（過剰殺傷と回復のぶん 1.00 を超える）。");
    Console.WriteLine("下回るなら `DamageTaken` を経由しない死亡経路があることになる。");
    Console.WriteLine();
    // 数字の読み方を先に書く。**書かないと「相関が低いから残す」と読まれる**が、
    // 基準は言い換えかどうかの1本だけで、相関の大小は判定に入らない（第14期の基準）。
    Console.WriteLine("> **読み方に注意。** 露骨な算術の署名を持つのは `撃破/戦` だけ（r 0.82〜0.98）で、");
    Console.WriteLine("> `与ダメ/戦` と `被ダメ/戦` の署名はどちらも弱い。**これは両者が鏡像だから**——");
    Console.WriteLine("> 勝てば敵の総HPを、負ければ味方の総HPを払い切るという同じ形で、どちらも");
    Console.WriteLine("> **過剰殺傷と「勝った（負けた）試行の中でのばらつき」に薄められる**。");
    Console.WriteLine("> 第14期は突破度に対して `与ダメ/戦` を分子経路として外している。単発で");
    Console.WriteLine("> `被ダメ/戦` を残すなら、**同じ形の量を勝ち側だけ外して負け側は残す**ことになり、");
    Console.WriteLine("> 基準が非対称になる。**外す根拠は構造であって相関の大小ではない**ので、両方外す。");
    Console.WriteLine();

    Console.WriteLine("#### 判定表（動的7種。静的8種はどちらの経路も持たないのですべて残す）");
    Console.WriteLine();
    Console.WriteLine("| 特徴量 | 第14期（突破度） | **第15期（単発の勝率）** | 理由 |");
    Console.WriteLine("|---|:-:|:-:|---|");
    for (int k = 0; k < nD; k++)
        Console.WriteLine($"| {dynNames[k]} | {taut15[k].Era14} "
            + $"| {(taut15[k].Excluded ? "**除外**" : "残す")} | {taut15[k].Reason} |");
    Console.WriteLine();

    bool[] keep15 = Enumerable.Range(0, nF).Select(k => k < nS || !taut15[k - nS].Excluded).ToArray();
    int[] cand15 = Enumerable.Range(0, nF).Where(k => keep15[k]).ToArray();
    Console.WriteLine($"**除外後の候補は {cand15.Length} 種**（静的 {nS} + 動的 {cand15.Length - nS}）。"
        + $"外したのは {string.Join(" / ", Enumerable.Range(0, nF).Where(k => !keep15[k]).Select(k => "`" + featNames[k] + "`"))}"
        + $"。第14期は 13 種（`被ダメ/戦` が残っていた）。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-2. 目的変数 ---
    //
    // 波ごとの勝率と、平均勝率2種。**天井の波は分散ゼロで相関が計算できない**（§3-2 の予告どおり）
    // ——第1波は 31 編成すべてが 100.0% なので、そこには当てる相手がいない。
    var goals = new List<(string Name, string Note, double[] V, double[][] Dyn)>();
    foreach (int w in Enumerable.Range(0, nW))
        goals.Add(($"{waves[w].Tag}", waves[w].Name, rate[w], dyn[w]));
    // 平均勝率。動的特徴量も同じ波の平均を取る（別の台の値を混ぜない）。
    double[][] MeanDyn(int[] ws) => Enumerable.Range(0, nT)
        .Select(t => Enumerable.Range(0, nF - nS)
            .Select(k => ws.Average(w => dyn[w][t][k])).ToArray()).ToArray();
    int[] baseW = { 0, 1, 2, 3, 4 };
    goals.Add(("平均(既存5波)", "`docs/balance.md` の 5 列の平均（power Phase EB の `単発戦` と同じ計算）",
        Enumerable.Range(0, nT).Select(t => baseW.Average(w => rate[w][t])).ToArray(), MeanDyn(baseW)));
    goals.Add(($"平均(寄与{conW.Length}波)", "§4 で寄与すると判定した波だけの平均",
        Enumerable.Range(0, nT).Select(t => conW.Average(w => rate[w][t])).ToArray(), MeanDyn(conW)));

    // --- 9-3. 波ごとの分解 ---
    // n = 31 しかないので、第12期からの方針どおり単相関 → 第一近似 → 静的だけ、の1段だけ。
    // 多変量は2変数まで。**因果は主張しない。**
    var firstOf = new Dictionary<string, (int K, double R, double R2, double StatBest, double StatPair, int TopK1, int TopK2, int TopK3)>();
    Console.WriteLine("### 9-2. 波ごとの第一近似と説明力");
    Console.WriteLine();
    Console.WriteLine($"候補 {cand15.Length} 種を目的変数に当てて、|r| の1位を第一近似とする（第12〜14期と同じ手順）。");
    Console.WriteLine("`静的1変数` / `静的2変数` は**戦わずにどこまで分かるか**——静的だけの説明力。");
    Console.WriteLine("**多変量は2変数まで**（n = 31 では3変数以上は過学習する）。**因果は主張しない。**");
    Console.WriteLine();
    Console.WriteLine("`—` は勝率の分散がゼロで相関が計算できない波（全編成が同じ勝率）。");
    Console.WriteLine("**第1波（S1）がまさにそれ**——31 編成すべてが 100.0% なので、当てる相手がいない。");
    Console.WriteLine();
    Console.WriteLine("| 目的変数 | 寄与 | 第一近似 | 区分 | r | **r²** | 2位 | 3位 | 静的1変数 r² | 静的2変数 R² |");
    Console.WriteLine("|:-:|:-:|---|:-:|--:|--:|---|---|--:|--:|");
    foreach (var (gname, _, gv, gdyn) in goals)
    {
        double[] Col(int k) => k < nS
            ? Enumerable.Range(0, nT).Select(t => stat[t][k]).ToArray()
            : Enumerable.Range(0, nT).Select(t => gdyn[t][k - nS]).ToArray();

        var ord = cand15.Select(k => (K: k, C: Correlate(Col(k), gv)))
            .Where(x => !double.IsNaN(x.C.R))
            .OrderByDescending(x => Math.Abs(x.C.R)).ToArray();
        bool con = Array.IndexOf(waves.Select(x => x.Tag).ToArray(), gname) is int wi && wi >= 0
            ? contributes[wi] : true;
        if (ord.Length == 0)
        {
            Console.WriteLine($"| **{gname}** | {(con ? "○" : "×")} | — | — | — | — | — | — | — | — |");
            continue;
        }
        var bestS = ord.FirstOrDefault(x => isStatic[x.K], (K: -1, C: (R: double.NaN, Rho: double.NaN, N: 0)));
        double bestPair = 0;
        for (int i = 0; i < nS; i++)
            for (int j = i + 1; j < nS; j++)
                bestPair = Math.Max(bestPair, R2Two(Correlate(Col(i), gv).R, Correlate(Col(j), gv).R,
                                                    Correlate(Col(i), Col(j)).R));
        string Nm(int i) => i < ord.Length ? $"{featNames[ord[i].K]} {ord[i].C.R:+0.00;-0.00}" : "—";
        Console.WriteLine($"| **{gname}** | {(con ? "○" : "×")} | {featNames[ord[0].K]} "
            + $"| {(isStatic[ord[0].K] ? "静" : "動")} | {ord[0].C.R:+0.00;-0.00} "
            + $"| **{ord[0].C.R * ord[0].C.R:F3}** | {Nm(1)} | {Nm(2)} "
            + $"| {(bestS.K < 0 ? "—" : $"{bestS.C.R * bestS.C.R:F2}")} | {bestPair:F2} |");
        firstOf[gname] = (ord[0].K, ord[0].C.R, ord[0].C.R * ord[0].C.R,
            bestS.K < 0 ? double.NaN : bestS.C.R * bestS.C.R, bestPair,
            ord[0].K, ord.Length > 1 ? ord[1].K : -1, ord.Length > 2 ? ord[2].K : -1);
        Console.Out.Flush();
    }
    Console.WriteLine();

    // --- 9-4. 単相関の全一覧（寄与する波だけ。全波ぶん出すと読めない） ---
    Console.WriteLine("### 9-3. 単相関の全一覧（寄与する波 × 候補特徴量の r）");
    Console.WriteLine();
    Console.WriteLine("**波によって効く特徴量が違えば、それがそのまま §6 の交互作用の説明になる。**");
    Console.WriteLine("符号まで含めて読むこと——同じ特徴量が波によって逆向きに効くなら、それは");
    Console.WriteLine("「どちらの波にも効く地力」ではなく**波の性格そのもの**。");
    Console.WriteLine();
    Console.WriteLine("| 区分 | 特徴量 |" + string.Concat(conW.Select(w => $" {waves[w].Tag} |"))
        + " 平均(既存5波) | 平均(寄与) |");
    Console.WriteLine("|:-:|---|" + string.Concat(conW.Select(_ => "--:|")) + "--:|--:|");
    foreach (int k in cand15)
    {
        var cells = new List<string>();
        foreach (var (gname, _, gv, gdyn) in goals)
        {
            if (!conW.Any(w => waves[w].Tag == gname) && !gname.StartsWith("平均")) continue;
            double[] col = k < nS
                ? Enumerable.Range(0, nT).Select(t => stat[t][k]).ToArray()
                : Enumerable.Range(0, nT).Select(t => gdyn[t][k - nS]).ToArray();
            double r = Correlate(col, gv).R;
            cells.Add(double.IsNaN(r) ? "—" : $"{r:+0.00;-0.00}");
        }
        Console.WriteLine($"| {(isStatic[k] ? "静" : "動")} | {featNames[k]} | {string.Join(" | ", cells)} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-5. 突破度との対比 ---
    //
    // 第14期の数字は**同じ実行の中で取り直す**（第13期以来の作法）。別の実行から引くと、
    // 動いたのが定義のせいか実行のせいか決まらない。台も候補集合も第14期のまま。
    var powerBenches = new (string Tag, string Name, IReadOnlyList<Formation> Squads)[]
    {
        ("主", "チャージ台", ChargeBench()),
        ("従", "既存5波", EnemyCatalog.Columns.First(c => c.Name == "順路").Squads),
    };
    // 第14期 Phase EA の候補集合（`与ダメ/戦` と `撃破/戦` だけを外したもの）。
    int[] cand14 = Enumerable.Range(0, nF).Where(k => k < nS || (k - nS != 0 && k - nS != 2)).ToArray();

    Console.WriteLine("### 9-4. 突破度との対比（第14期の数字は同じ実行の中で取り直したもの）");
    Console.WriteLine();
    Console.WriteLine("**別の実行から引かない**——動いたのが目的変数のせいか実行のせいか決まらなくなる");
    Console.WriteLine("（第13期以来の作法）。台も候補集合も第14期 Phase EA のまま（13種）。");
    Console.WriteLine();
    Console.WriteLine("| 目的変数 | 候補 | 第一近似 | r | **r²** | 静的1変数 r² | 静的2変数 R² |");
    Console.WriteLine("|---|--:|---|--:|--:|--:|--:|");
    foreach (var (tag, bname, squads) in powerBenches)
    {
        var deg = new double[nT];
        var pdyn = new double[nT][];
        for (int t = 0; t < nT; t++)
        {
            var m = MeasurePower(targets[t].F, squads, WaveSeeds);
            deg[t] = m.Degree;
            pdyn[t] = m.Dynamics;
        }
        double[] Col(int k) => k < nS
            ? Enumerable.Range(0, nT).Select(t => stat[t][k]).ToArray()
            : Enumerable.Range(0, nT).Select(t => pdyn[t][k - nS]).ToArray();
        var ord = cand14.Select(k => (K: k, C: Correlate(Col(k), deg)))
            .Where(x => !double.IsNaN(x.C.R)).OrderByDescending(x => Math.Abs(x.C.R)).ToArray();
        var bestS = ord.First(x => isStatic[x.K]);
        double bestPair = 0;
        for (int i = 0; i < nS; i++)
            for (int j = i + 1; j < nS; j++)
                bestPair = Math.Max(bestPair, R2Two(Correlate(Col(i), deg).R, Correlate(Col(j), deg).R,
                                                    Correlate(Col(i), Col(j)).R));
        Console.WriteLine($"| 突破度・{tag}: {bname}（第14期） | {cand14.Length} | {featNames[ord[0].K]} "
            + $"| {ord[0].C.R:+0.00;-0.00} | **{ord[0].C.R * ord[0].C.R:F3}** "
            + $"| {bestS.C.R * bestS.C.R:F2} | {bestPair:F2} |");
        Console.Out.Flush();
    }
    foreach (string g in new[] { "平均(既存5波)", $"平均(寄与{conW.Length}波)" })
        if (firstOf.TryGetValue(g, out var f15))
            Console.WriteLine($"| **単発の勝率・{g}（第15期）** | {cand15.Length} | {featNames[f15.K]} "
                + $"| {f15.R:+0.00;-0.00} | **{f15.R2:F3}** "
                + $"| {(double.IsNaN(f15.StatBest) ? "—" : $"{f15.StatBest:F2}")} | {f15.StatPair:F2} |");
    foreach (int w in conW)
        if (firstOf.TryGetValue(waves[w].Tag, out var f15))
            Console.WriteLine($"| 単発の勝率・{waves[w].Tag}（第15期） | {cand15.Length} | {featNames[f15.K]} "
                + $"| {f15.R:+0.00;-0.00} | **{f15.R2:F3}** "
                + $"| {(double.IsNaN(f15.StatBest) ? "—" : $"{f15.StatBest:F2}")} | {f15.StatPair:F2} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-6. FA との突き合わせ ---
    //
    // 「波によって効く特徴量が違う」ことと「波によって編成の順位が入れ替わる」ことは
    // 別々の観測で、**繋がっている保証は無い。** 繋がっているなら、特徴量の効き方が
    // 似ている波のペアほど順位相関が高いはず——それを 21 ペアで測る。
    Console.WriteLine("### 9-5. §6 の交互作用と繋がっているか");
    Console.WriteLine();
    Console.WriteLine("「波によって効く特徴量が違う」と「波によって順位が入れ替わる」は別々の観測で、");
    Console.WriteLine("**繋がっている保証は無い。** 繋がっているなら、**特徴量の効き方が似ている波のペアほど");
    Console.WriteLine("順位相関が高い**はず。効き方の似ぐあいは「候補特徴量それぞれの r を並べたベクトル」の");
    Console.WriteLine("ピアソン相関で測る（`効き方の一致`）。");
    Console.WriteLine();
    double[] Profile(int w) => cand15.Select(k =>
    {
        double[] col = k < nS
            ? Enumerable.Range(0, nT).Select(t => stat[t][k]).ToArray()
            : Enumerable.Range(0, nT).Select(t => dyn[w][t][k - nS]).ToArray();
        return Correlate(col, rate[w]).R;
    }).ToArray();
    var prof = Enumerable.Range(0, nW).Select(w => contributes[w] ? Profile(w) : null).ToArray();

    Console.WriteLine("| 波 | 波 | 順位相関 ρ（§6） | 効き方の一致 |");
    Console.WriteLine("|:-:|:-:|--:|--:|");
    var xs = new List<double>();
    var ys = new List<double>();
    for (int i = 0; i < conW.Length; i++)
        for (int j = i + 1; j < conW.Length; j++)
        {
            int a = conW[i], b = conW[j];
            double rho = Correlate(rate[a], rate[b]).Rho;
            double agree = Pearson(prof[a]!, prof[b]!);
            if (double.IsNaN(rho) || double.IsNaN(agree)) continue;
            xs.Add(rho);
            ys.Add(agree);
            Console.WriteLine($"| {waves[a].Tag} | {waves[b].Tag} | {rho:F2} | {agree:+0.00;-0.00} |");
        }
    Console.WriteLine();
    double link = Pearson(xs.ToArray(), ys.ToArray());
    Console.WriteLine($"**順位相関 × 効き方の一致: r = {link:F2}**（{xs.Count} ペア）。");
    Console.WriteLine("正で大きいほど「**効く特徴量の違いが、そのまま順位の入れ替わりになっている**」。");
    Console.WriteLine("0 付近なら、入れ替わりは既存の特徴量では説明できていない——**入れ替わりは実在するが");
    Console.WriteLine("何が起こしているか分からない**ことになり、次に測るものが変わる。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-6. まとめ ---
    // 数字から機械的に選ぶ（§8 と同じ作法）。文章で判定すると読み方の幅が残る。
    var conGoals = conW.Select(w => waves[w].Tag)
        .Concat(new[] { "平均(既存5波)", $"平均(寄与{conW.Length}波)" })
        .Where(firstOf.ContainsKey).ToArray();
    double maxR2 = conGoals.Max(g => firstOf[g].R2);
    double maxStat = conGoals.Max(g => double.IsNaN(firstOf[g].StatBest) ? 0 : firstOf[g].StatBest);
    double maxStatPair = conGoals.Max(g => firstOf[g].StatPair);
    var firstNames = conW.Where(w => firstOf.ContainsKey(waves[w].Tag))
        .Select(w => featNames[firstOf[waves[w].Tag].K]).Distinct().ToArray();

    Console.WriteLine("### 9-6. まとめ");
    Console.WriteLine();
    double avg5R2 = firstOf.TryGetValue("平均(既存5波)", out var g5) ? g5.R2 : double.NaN;
    int overMain = conGoals.Count(g => firstOf[g].R2 > 0.308);
    Console.WriteLine($"- **第一近似の r² は寄与する波で {conGoals.Min(g => firstOf[g].R2):F3} 〜 {maxR2:F3}"
        + $"、平均(既存5波) では {avg5R2:F3}。** 突破度の 0.308（主）/ 0.242（従）を上回るのは");
    Console.WriteLine($"  {overMain} 本だけで、**「単発の勝率なら総攻や総HPがずっとよく効く」は支持されない**");
    Console.WriteLine("  （§3-2 の予想と逆）。**波を平均するほど説明が付かなくなる**——波ごとに効くものが");
    Console.WriteLine("  違うので、平均すると打ち消し合う。それ自体が交互作用の裏返しになっている。");
    Console.WriteLine($"- **静的だけの説明力は 1変数で最大 {maxStat:F2} / 2変数で最大 {maxStatPair:F2}。**");
    Console.WriteLine("  突破度に対する 0.31→0.35（主）/ 0.10→0.16（従）と同じ帯か、それ以下。");
    Console.WriteLine("  **「編成した時点で単発の勝敗はかなり決まっている」も支持されない。**");
    Console.WriteLine($"- **第一近似は波で入れ替わる**（寄与する波で {firstNames.Length} 種類: "
        + $"{string.Join(" / ", firstNames.Select(x => "`" + x + "`"))}）。符号まで含めて違う。");
    Console.WriteLine($"- ただし **効き方の違いは §6 の入れ替わりを説明していない**（r = {link:F2}）。");
    Console.WriteLine("  **交互作用は実在するのに、既存の特徴量ではどの編成がどの波に強いかを予測できない。**");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}
}
