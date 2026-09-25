using BattleCore;
using static Common;

// =====================================================================================
// timing モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "timing")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 timing
// =====================================================================================

static class TimingDiag
{
// chain モード: 勝率だけでは見えない「連鎖の深さ」を測る。
// 「2枚で人並みに勝つ」編成と「5枚が畳みかけて無双する」編成は、勝率だけ見ると同じ100%になる。
// MaxEnemyKillsInOneTurn（1ターンで味方が何体倒したかの最大値）と、勝利時の決着ターン数を
// compare と同じ代表編成×全ステージで測って区別する。数値が大きいほど「畳みかけている」。
// timing モード: 味方側の行動パターンの「変種」を測る（第11期 Phase BC）。
//
// 第10期は敵だけがパターンを持ち、味方は毎ターン同じ行動を繰り返していた。相性が
// 片側にしか無いので「大技の前に回復を差す」が**そもそも表現できない**。Phase BB で
// ノノ・ミオを Skill へ移したので、ここでは**特性の数値を一切変えず、パターンだけが
// 違う変種**を並べて、いつ撃つかが結果を動かすかを見る。
//
//   N0 / M0 = [Skill]                （毎ターン。移行直後の形。UnitCatalog はこれ）
//   N1 / M1 = [Skill, Attack]        （隔ターン。手番の半分を攻撃に使う）
//   N2 / M2 = [Skill, Skill, Attack] （3ターン周期。敵の2周期と噛み合わない位相）
//
// 変種は UnitCatalog を書き換えずここでローカルに組む（gradient / aim と同じやり方）。
// 台は2種——チャージ台（bridge の7列目 = ChargeBench。第10期 AB-0）と既存5波。
// 第8期の「136% で測ると何も見えない」が効くので、片方だけでは判定できない。
// 診断用で docs/ には置かない。
//
//     dotnet run --project BattleSim -c Release 0 timing [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    const int TimingSeeds = 200;
    var all = CompareBuilds();

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 変種の中身。ラベルは BB で入れたものをそのまま使う（台本の見た目を変えないため）。
    UnitAction Mend() => new(ActionKind.Skill, Label: "傷を繕っている");
    UnitAction Foul() => new(ActionKind.Skill, Label: "水を濁らせている");
    UnitAction Hit() => new(ActionKind.Attack);

    // V3 は §5-1 の表には無いが、**V1 の結果を読むために要る対照**。
    // V1 は敵の2周期とちょうど逆位相になり、大技ターンとの一致が全編成で 0.0% に
    // ロックされる（下の噛み合わせの表）。つまり V1 の落ち込みには「撃つ回数が半分」と
    // 「大技ターンに一度も乗らない」が混ざっていて、そのままでは分離できない。
    // V3 は**回数を V1 と揃えたまま位相だけ反転**させたもので、この2つを切り分ける。
    var variants = new (string Tag, UnitAction[] Nono, UnitAction[] Mio)[]
    {
        ("V0", new[] { Mend() },                new[] { Foul() }),
        ("V1", new[] { Mend(), Hit() },         new[] { Foul(), Hit() }),
        ("V2", new[] { Mend(), Mend(), Hit() }, new[] { Foul(), Foul(), Hit() }),
        ("V3", new[] { Hit(), Mend() },         new[] { Hit(), Foul() }),
    };

    Formation Swap(Formation f, int v)
    {
        Formation c = f.Clone();
        foreach (var (slot, d) in f.Occupied())
        {
            if (d.Id == "nono") c[slot] = WithActions(d, variants[v].Nono);
            else if (d.Id == "mio") c[slot] = WithActions(d, variants[v].Mio);
        }
        return c;
    }

    // 変種が実際に効く編成（ノノかミオを含む）。31編成のうち9編成しかないので、
    // **全編成の順位相関は自動的に 1.0 へ引っ張られる。** 第10期の 0.91 と方法を
    // 揃えた全編成の値と、変種が効く編成だけに絞った値の両方を出す。片方だけだと
    // 「動いていない」のか「動く駒が入っていないだけ」なのかが区別できない。
    bool Affected(Formation f) => f.Occupied().Any(x => x.Def.Id is "nono" or "mio");
    var affected = Enumerable.Range(0, targets.Length).Where(t => Affected(targets[t].F)).ToArray();

    Console.WriteLine($"# 行動パターンの変種（seed 0..{TimingSeeds - 1} の {TimingSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("**特性の係数は一切変えていない。** 違うのは行動パターンだけ。");
    Console.WriteLine();
    Console.WriteLine("| 変種 | ノノ | ミオ |");
    Console.WriteLine("|---|---|---|");
    Console.WriteLine("| V0 | `[繕い]` | `[濁し]` |");
    Console.WriteLine("| V1 | `[繕い, 攻撃]` | `[濁し, 攻撃]` |");
    Console.WriteLine("| V2 | `[繕い, 繕い, 攻撃]` | `[濁し, 濁し, 攻撃]` |");
    Console.WriteLine("| V3 | `[攻撃, 繕い]` | `[攻撃, 濁し]` | ← V1 と同じ回数・逆の位相（対照）");
    Console.WriteLine();
    Console.WriteLine($"変種が効く編成（ノノかミオを含む）: **{affected.Length} / {targets.Length}**");
    Console.WriteLine();

    // ---- 台1: チャージ台（会戦・味方1部隊） ------------------------------
    Formation[] bench = ChargeBench();

    var deg = new double[variants.Length][];
    var tot = new double[variants.Length][];
    for (int v = 0; v < variants.Length; v++)
    {
        deg[v] = new double[targets.Length];
        tot[v] = new double[targets.Length];
        for (int t = 0; t < targets.Length; t++)
        {
            Formation[] one = { Swap(targets[t].F, v) };
            for (int seed = 0; seed < TimingSeeds; seed++)
            {
                EngagementResult r = EngagementEngine.Run(one, bench, seed, verbose: false);
                deg[v][t] += BreakthroughDegree(r, bench.Length);
                tot[v][t] += r.Battles.Sum(x => x.Turns);
            }
            deg[v][t] /= TimingSeeds; tot[v][t] /= TimingSeeds;
        }
    }

    Console.WriteLine("## 台1: チャージ台（会戦・味方1部隊）");
    Console.WriteLine();
    Console.WriteLine("bridge の7列目と同じ列（ChargeBench）。突破度は第8期 Phase U と同じ定義。");
    Console.WriteLine("`*` が付いている行が変種の効く編成。他の22編成は3つの変種で完全に同じ値になる");
    Console.WriteLine("（差し替えていないので当然だが、**動いていないことの検算**になる）。");
    Console.WriteLine();
    Console.Write("| 編成 | * |");
    foreach (var (tag, _, _) in variants) Console.Write($" {tag} 突破度 |");
    foreach (var (tag, _, _) in variants.Skip(1)) Console.Write($" {tag}-V0 |");
    foreach (var (tag, _, _) in variants) Console.Write($" {tag} 総T |");
    Console.WriteLine();
    Console.Write("|---|:-:|");
    for (int i = 0; i < variants.Length * 3 - 1; i++) Console.Write("--:|");
    Console.WriteLine();
    for (int t = 0; t < targets.Length; t++)
    {
        Console.Write($"| {targets[t].Name} | {(Affected(targets[t].F) ? "*" : "")} |");
        for (int v = 0; v < variants.Length; v++) Console.Write($" {deg[v][t]:F3} |");
        for (int v = 1; v < variants.Length; v++) Console.Write($" {deg[v][t] - deg[0][t]:+0.000;-0.000} |");
        for (int v = 0; v < variants.Length; v++) Console.Write($" {tot[v][t]:F2} |");
        Console.WriteLine();
    }
    Console.WriteLine();

    // 順位相関の計算方法は第7期から変えない（スピアマン＝平均順位の列にピアソン）。
    double[] Sub(double[] v) => affected.Select(i => v[i]).ToArray();
    var rankAll = variants.Select((_, v) => AverageRanksDesc(deg[v])).ToArray();
    var rankSub = variants.Select((_, v) => AverageRanksDesc(Sub(deg[v]))).ToArray();

    Console.WriteLine($"**順位相関（全{targets.Length}編成）: "
        + string.Join(" / ", variants.Skip(1).Select((x, i) =>
            $"V0-{x.Tag} = {Pearson(rankAll[0], rankAll[i + 1]):F2}")) + "**　値の相関 = "
        + string.Join(" / ", variants.Skip(1).Select((_, i) => $"{Pearson(deg[0], deg[i + 1]):F2}")));
    Console.WriteLine();
    Console.WriteLine($"**順位相関（変種が効く{affected.Length}編成だけ）: "
        + string.Join(" / ", variants.Skip(1).Select((x, i) =>
            $"V0-{x.Tag} = {Pearson(rankSub[0], rankSub[i + 1]):F2}")) + "**　値の相関 = "
        + string.Join(" / ", variants.Skip(1).Select((_, i) =>
            $"{Pearson(Sub(deg[0]), Sub(deg[i + 1])):F2}")));
    Console.WriteLine();
    Console.WriteLine("突破度の平均 "
        + string.Join(" / ", variants.Select((x, v) => $"{x.Tag} {deg[v].Average():F3}"))
        + "、編成間の SD " + string.Join(" / ", variants.Select((_, v) => $"{Sd(deg[v]):F3}")) + "。");
    Console.WriteLine("会戦の総ターン数 "
        + string.Join(" / ", variants.Select((_, v) => $"{tot[v].Average():F2}"))
        + "。**大きく伸びていたら間延び**。");
    Console.WriteLine();

    // ---- 群差（回復持ち / 毒軸。第11期 §5-2 の2番目） --------------------
    // 第10期は 速攻 / 耐久 / 回復持ち で割った。ここでは変種が触るものに合わせて
    // 回復（ノノ）と毒（ミオ）で割る。**排他の2群にする**——第9期 0.131 波・
    // 第10期 0.064 波と並べるには「2つの群の Δ の差」が要るので、
    // 「どれでもない」を混ぜた3群以上にすると群差が定義できない。
    var healer = Enumerable.Range(0, targets.Length)
        .Where(t => targets[t].F.Occupied().Any(x => x.Def.Traits.Any(id =>
            id is TraitId.Mender or TraitId.Devour or TraitId.Drifter or TraitId.Reviver))).ToArray();
    var poison = Enumerable.Range(0, targets.Length)
        .Where(t => targets[t].F.Occupied().Any(x => x.Def.Traits.Any(id =>
            id is TraitId.Venom or TraitId.VenomHeavy or TraitId.Miasma or TraitId.Amplifier or TraitId.Contagion))).ToArray();

    Console.WriteLine("## 群差（回復持ち / 毒軸）");
    Console.WriteLine();
    Console.WriteLine("- `回復持ち`: 味方を癒す／戻す特性（継ぎ当て・毒喰らい・移り木・継ぎ接ぎ）を含む");
    Console.WriteLine("- `毒軸`: 毒を作る／広げる特性（毒撃・瘴気・澱み・疫み）を含む");
    Console.WriteLine();
    Console.WriteLine("`群差` は2群の Δ の差。第9期の自傷率 0.131 波・第10期のチャージ 0.064 波と");
    Console.WriteLine("同じ物差しで、**これを下回るなら9期分で最も弱い軸**ということになる。");
    Console.WriteLine();
    Console.Write("| 群 | 編成数 |");
    foreach (var (tag, _, _) in variants) Console.Write($" {tag} |");
    foreach (var (tag, _, _) in variants.Skip(1)) Console.Write($" Δ({tag}-V0) |");
    Console.WriteLine();
    Console.Write("|---|--:|");
    for (int i = 0; i < variants.Length * 2 - 1; i++) Console.Write("--:|");
    Console.WriteLine();
    double GroupDelta(int[] ix, int v)
        => ix.Length == 0 ? 0 : ix.Average(t => deg[v][t]) - ix.Average(t => deg[0][t]);
    void Row(string name, int[] ix)
    {
        Console.Write($"| {name} | {ix.Length} |");
        for (int v = 0; v < variants.Length; v++)
            Console.Write(ix.Length == 0 ? " — |" : $" {ix.Average(t => deg[v][t]):F3} |");
        for (int v = 1; v < variants.Length; v++)
            Console.Write(ix.Length == 0 ? " — |" : $" {GroupDelta(ix, v):+0.000;-0.000} |");
        Console.WriteLine();
    }
    var nonHealer = Enumerable.Range(0, targets.Length).Where(t => !healer.Contains(t)).ToArray();
    var nonPoison = Enumerable.Range(0, targets.Length).Where(t => !poison.Contains(t)).ToArray();
    Row("回復持ち", healer);
    Row("非回復", nonHealer);
    Row("毒軸", poison);
    Row("非毒軸", nonPoison);
    Row("全編成", Enumerable.Range(0, targets.Length).ToArray());
    Console.WriteLine();
    Console.WriteLine("**群差（回復持ち − 非回復）: " + string.Join(" / ", variants.Skip(1).Select((x, i) =>
        $"{x.Tag} {GroupDelta(healer, i + 1) - GroupDelta(nonHealer, i + 1):+0.000;-0.000} 波")) + "**");
    Console.WriteLine("**群差（毒軸 − 非毒軸）: " + string.Join(" / ", variants.Skip(1).Select((x, i) =>
        $"{x.Tag} {GroupDelta(poison, i + 1) - GroupDelta(nonPoison, i + 1):+0.000;-0.000} 波")) + "**");
    Console.WriteLine();

    // ---- 台2: 既存5波（単独戦） -----------------------------------------
    Console.WriteLine("## 台2: 既存5波（単独戦）");
    Console.WriteLine();
    Console.WriteLine("compare と同じ台。変種が効く編成だけを出す（他は3つとも同じ値になる）。");
    Console.WriteLine($"セルは `{string.Join(" / ", variants.Select(x => x.Tag))}` の勝率。");
    Console.WriteLine();

    var waves = EnemyCatalog.Stages.Select((st, i) => (Name: $"第{i + 1}波", Enemy: st.Enemy)).ToList();
    Console.Write("| 編成 |");
    foreach (var (wn, _) in waves) Console.Write($" {wn} |");
    Console.WriteLine(" 平均T |");
    Console.Write("|---|");
    foreach (var _ in waves) Console.Write("---|");
    Console.WriteLine("--:|");

    var waveWin = new double[variants.Length][];
    for (int v = 0; v < variants.Length; v++) waveWin[v] = new double[waves.Count];

    foreach (int t in affected)
    {
        Console.Write($"| {targets[t].Name} |");
        var tAvg = new double[variants.Length];
        for (int w = 0; w < waves.Count; w++)
        {
            var cell = new double[variants.Length];
            for (int v = 0; v < variants.Length; v++)
            {
                Formation f = Swap(targets[t].F, v);
                int wins = 0; double turnSum = 0;
                for (int seed = 0; seed < TimingSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, waves[w].Enemy, seed, verbose: false);
                    if (r.PlayerWon) wins++;
                    turnSum += r.Turns;
                }
                cell[v] = wins * 100.0 / TimingSeeds;
                waveWin[v][w] += cell[v];
                tAvg[v] += turnSum / TimingSeeds / waves.Count;
            }
            Console.Write(" " + string.Join(" / ", cell.Select(x => x.ToString("F1"))) + " |");
        }
        Console.WriteLine(" " + string.Join(" / ", tAvg.Select(x => x.ToString("F1"))) + " |");
        Console.Out.Flush();
    }
    Console.Write("| **平均** |");
    for (int w = 0; w < waves.Count; w++)
        Console.Write(" " + string.Join(" / ", variants.Select((_, v) =>
            (waveWin[v][w] / affected.Length).ToString("F1"))) + " |");
    Console.WriteLine(" |");
    Console.WriteLine();

    // ---- 敵の大技ターンとの噛み合わせ（第11期 §5-2 の3番目） -------------
    //
    // 「詠唱兵・狙撃手が大技を撃つターンに、ノノの回復が乗っているか」を台本から数える。
    // 大技ターンの同定は Charge イベントを使う——溜めた駒が**次に攻撃したターン**が
    // 大技のターン。倍率を見て判定しないのは、イベントに載っているのが実ダメージで、
    // 攻撃力の変動と区別できないため。溜めた次の手番に痺れて飛ばされても正しく追える。
    //
    // 術のターンは Skill イベント。ChargeBench の敵は誰も Skill を持たないので、
    // Skill イベント = 移行した味方（ノノかミオ）の手番で確定する。
    //
    // ここだけ verbose:true で回す（台本が要る）。編成を9つに絞ってあるので許容範囲。
    Console.WriteLine("## 敵の大技ターンとの噛み合わせ");
    Console.WriteLine();
    Console.WriteLine("チャージ台で台本を取り、部隊戦ごとに数えた。");
    Console.WriteLine("`大技T/戦` は1部隊戦あたりの大技ターン数、`一致` はそのうち術も撃ったターンの割合、");
    Console.WriteLine("`素の術率` は全ターンのうち術を撃ったターンの割合（比較の基準線）。");
    Console.WriteLine();
    Console.WriteLine("**`一致` が `素の術率` から大きく離れるなら、位相が噛んでいる（または外れている）。**");
    Console.WriteLine("V1 は隔ターン・敵は2周期なので、ここが割れるなら一番大きい観測になる（第11期 §5-3）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 変種 | 大技T/戦 | 一致 | 素の術率 | 術T/戦 | 総T/戦 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|");

    // 回数と位相を分けて集計しておく（下のまとめで使う）。
    var castRate = new double[variants.Length][];
    var matchRate = new double[variants.Length][];
    for (int v = 0; v < variants.Length; v++)
    {
        castRate[v] = new double[targets.Length];
        matchRate[v] = new double[targets.Length];
    }

    foreach (int t in affected)
    {
        for (int v = 0; v < variants.Length; v++)
        {
            Formation[] one = { Swap(targets[t].F, v) };
            long bigT = 0, skillT = 0, allT = 0, hit = 0, battles = 0;
            for (int seed = 0; seed < TimingSeeds; seed++)
            {
                EngagementResult r = EngagementEngine.Run(one, bench, seed, verbose: true);
                foreach (BattleResult b in r.Battles)
                {
                    battles++;
                    var big = new HashSet<int>();
                    var skill = new HashSet<int>();
                    var turnsSeen = new HashSet<int>();
                    var charged = new HashSet<int>();   // 溜めた直後の駒
                    foreach (BattleEvent e in b.Events)
                    {
                        if (e.Turn > 0) turnsSeen.Add(e.Turn);
                        switch (e.Kind)
                        {
                            case BattleEventKind.Charge when e.ActorId is { } c:
                                charged.Add(c); break;
                            case BattleEventKind.Attack when e.ActorId is { } a && charged.Remove(a):
                                big.Add(e.Turn); break;
                            case BattleEventKind.Skill:
                                skill.Add(e.Turn); break;
                        }
                    }
                    bigT += big.Count; skillT += skill.Count; allT += turnsSeen.Count;
                    hit += big.Count(x => skill.Contains(x));
                }
            }
            castRate[v][t] = (double)skillT / Math.Max(1, battles);
            matchRate[v][t] = bigT == 0 ? 0 : hit * 100.0 / bigT;

            Console.WriteLine($"| {(v == 0 ? targets[t].Name : "")} | {variants[v].Tag} "
                + $"| {(double)bigT / Math.Max(1, battles):F2} "
                + $"| {(bigT == 0 ? "—" : $"{hit * 100.0 / bigT:F1}%")} "
                + $"| {(allT == 0 ? "—" : $"{skillT * 100.0 / allT:F1}%")} "
                + $"| {(double)skillT / Math.Max(1, battles):F2} "
                + $"| {(double)allT / Math.Max(1, battles):F2} |");
            Console.Out.Flush();
        }
    }
    Console.WriteLine();

    // ---- 回数 と 位相 のどちらが効いているか -----------------------------
    //
    // V1 と V3 は**周期が同じで位相だけ逆**（V1 は大技ターンと一致 0%、V3 は乗る側）。
    // ここが分離の要で、V1 と V3 の差はすべて位相のもの……ではない。戦闘が 3〜6 ターンで
    // 終わるので、V3 は初撃が T2 になるぶん **1戦あたりの発火回数そのものが減る**。
    // だから「術T/戦（回数）」と「一致（位相）」の両方を突破度に当てて、
    // どちらが順位を説明しているかを見る。
    //
    // **編成ごとに中心化してから相関を取る。** 生のまま 36点をまとめると、編成間の
    // 地力の差（突破度 1.8〜3.0）が変種の差（0.0〜0.3）を覆い隠して相関が 0 に潰れる。
    // 見たいのは「同じ編成の中で変種を替えたときに何が動くか」なので、各編成の平均を
    // 引いた偏差同士で当てる（第9期の群平均を引く扱いと同じ考え方）。
    double[] Centered(Func<int, int, double> pick) => affected.SelectMany(t =>
    {
        double m = Enumerable.Range(0, variants.Length).Average(v => pick(v, t));
        return Enumerable.Range(0, variants.Length).Select(v => pick(v, t) - m);
    }).ToArray();

    double[] cDeg = Centered((v, t) => deg[v][t]);
    double[] cCast = Centered((v, t) => castRate[v][t]);
    double[] cMatch = Centered((v, t) => matchRate[v][t]);

    Console.WriteLine("## 回数か、位相か");
    Console.WriteLine();
    Console.WriteLine($"変種が効く{affected.Length}編成 × {variants.Length}変種 = {cDeg.Length}点。");
    Console.WriteLine("**編成ごとに平均を引いた偏差で当てている**——生のままだと編成間の地力の差");
    Console.WriteLine("（突破度 1.8〜3.0）が変種の差（0.0〜0.3）を覆い隠して相関が 0 に潰れる。");
    Console.WriteLine();
    Console.WriteLine($"- 突破度 × 術T/戦（回数）: **{Pearson(cDeg, cCast):F2}**");
    Console.WriteLine($"- 突破度 × 一致（位相）: **{Pearson(cDeg, cMatch):F2}**");
    Console.WriteLine();
    Console.WriteLine("**V1 と V3 は周期が同じ（隔ターン）で位相だけ逆。**");
    Console.WriteLine($"一致は V1 {affected.Average(t => matchRate[1][t]):F1}% → "
        + $"V3 {affected.Average(t => matchRate[3][t]):F1}% と大きく開くのに、");
    Console.WriteLine($"突破度は V1 {affected.Average(t => deg[1][t]):F3} → V3 {affected.Average(t => deg[3][t]):F3}。");
    Console.WriteLine($"術T/戦は V1 {affected.Average(t => castRate[1][t]):F2} → V3 {affected.Average(t => castRate[3][t]):F2}。");
    Console.WriteLine();
    Console.WriteLine("**位相を大技ターンに乗せたほうが弱いなら、効いているのは回数のほう。**");
    Console.WriteLine("戦闘が短いので、周期を後ろにずらすと初撃が遅れて発火回数そのものが減る。");
    Console.WriteLine();
    return;
}
}
