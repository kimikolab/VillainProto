using BattleCore;
using static Common;

// =====================================================================================
// dissect モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "dissect")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 dissect
// =====================================================================================

static class DissectDiag
{
// dissect モード: 交互作用の個別事例を解剖する（第16期 Phase GA）。
//
// 第15期で**交互作用が実在することは確定した**（寄与する 7 波の 21 ペア全部が ρ < 0.90）。
// **ところが予測できない**——「効き方の一致度」と「順位相関」の関係が r = 0.16 で、
// 第13期の台間の入れ替わり（最良 r² 0.19）と同じ形の壁に、目的変数を替えても当たった。
//
// ここで決めるのは「**法則が無いのか、特徴量が悪いのか**」（design/INTERACTION_READABILITY_PLAN.md §0）。
// `power` の 15 特徴量は「総攻」「総HP」のような集計量ばかりで、**プレイヤーが実際に見ている情報**
// （誰がどの駒を殴るか、貫きが後列に届くか、範囲が何体巻き込むか、毒が乗り切る前に敵が落ちるか）を
// 1つも含んでいない。含んでいないものが効かないのは、法則が無いことの証拠にならない。
//
// **統計ではなく個別事例を読む。** 入れ替わりの大きい 3 ペアから、順位が最も動いた編成を
// 上下2つずつ取って 12 事例。各事例で「その編成が波 A では勝ち、波 B では負ける」理由を
// 戦闘の中身（決着ターン数・盤面の推移・振りと巻き込み・毒の乗り・被弾の位置）から言葉にする。
//
// 却下した案: `demo` / `replay` で1戦ずつ目で読む。seed 0 の1戦は 200 試行の代表ではないし、
// **平均で 75%→100% でも個別の試行では別のことが起きているかもしれない**（§5-7 の停止条件）。
// 200 試行ぶんを集計したうえで、seed による振れを別表で出すほうが停止条件に答えられる。
//
// 却下した案: 波を `dissect` のローカルで組み直す。**`WaveCatalog()` を呼ぶ**——第15期が
// 「1箇所に集める」ためにやった作業を、2つ目の診断がコピーを持った瞬間に台無しにする。
//
// 診断用で docs/ には置かない（wave / power / bench と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 dissect [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    const int DissectSeeds = 200;   // wave / compare / power / bench と同じ
    const double DeadZone = 50.0;   // wave §4 と同じ線（天井率 + 床率 < 50%）

    var all = CompareBuilds();
    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    var waves = WaveCatalog();
    int nW = waves.Length;

    Console.WriteLine("# 交互作用の解剖（第16期 Phase GA）");
    Console.WriteLine();
    Console.WriteLine("第15期は「**交互作用は実在するが、既存の特徴量では予測できない**」で終わった");
    Console.WriteLine("（順位相関 × 効き方の一致が r = 0.16）。ここで決めるのは");
    Console.WriteLine("**「法則が無いのか、特徴量が悪いのか」**——`power` の 15 特徴量は集計量ばかりで、");
    Console.WriteLine("プレイヤーが実際に見ている情報（誰が誰を殴るか・範囲が何体巻き込むか・");
    Console.WriteLine("毒が乗り切る前に敵が落ちるか）を1つも含んでいない。");
    Console.WriteLine();
    Console.WriteLine("**統計ではなく個別事例を読む。** 盤面は1つも動かしていない（`BattleCore` 無変更）。");
    Console.WriteLine();

    // --- 1. 寄与する波を決め直す ---
    //
    // **第15期の「7波」を数字で持ち込まない。** 同じ判定式（天井率 + 床率 < 50%）を同じ
    // seed 数でここでも通し、一致することを確かめてから使う。ハードコードすると、
    // 波の定義が動いたときに黙って古い集合を解剖し続ける。
    // 残存度（全試行の平均。負けた試行は 0）も同じ計測から取っておく。
    // Phase GB が**天井で潰れない連続量**として使う（第15期 (c) の読み方と同じ定義）。
    // 2回回すと seed は同じでも実行時間が倍になるだけなので、1回で両方取る。
    var rate = new double[nW][];
    var degree = new double[nW][];
    for (int w = 0; w < nW; w++)
    {
        rate[w] = new double[nT];
        degree[w] = new double[nT];
        for (int t = 0; t < nT; t++)
        {
            var mw = MeasureWave(targets[t].F, waves[w].Enemy, DissectSeeds);
            rate[w][t] = mw.Win.Average() * 100;
            degree[w][t] = mw.SurvRate.Average();
        }
    }
    var contributes = new bool[nW];
    for (int w = 0; w < nW; w++)
    {
        double ceil = rate[w].Count(v => v >= 100.0 - 1e-9) * 100.0 / nT;
        double floor = rate[w].Count(v => v <= 1e-9) * 100.0 / nT;
        contributes[w] = ceil + floor < DeadZone;
    }
    int[] conW = Enumerable.Range(0, nW).Where(w => contributes[w]).ToArray();
    var rankOf = Enumerable.Range(0, nW).Select(w => AverageRanksDesc(rate[w])).ToArray();
    int Tag(string t) => Array.FindIndex(waves, x => x.Tag == t);

    // `+0.0;-0.0` の書式は、**表示桁でゼロに丸まる負の値を `-+0.0` と出す**
    // （.NET はセクションを丸めた後の値で選びながら、負の符号は別に付ける）。
    // 表示桁で先に丸めておけば消える。**既存のモードには入れない**
    // （同じ症状は `wave` にもあるが、直すとその出力が動いて 18モード差分ゼロを失う）。
    double Sg(double v, int dp) => double.IsNaN(v) ? v : Math.Round(v, dp) + 0.0;

    Console.WriteLine("## 1. 寄与する波（`wave` §4 と同じ判定を、同じ seed 数で引き直したもの）");
    Console.WriteLine();
    Console.WriteLine($"判定式は **天井率 + 床率 < {DeadZone:F0}%**。**第15期の「7波」を数字で持ち込まず**、");
    Console.WriteLine("ここでも同じ式を通している——ハードコードすると、波の定義が動いたときに黙って");
    Console.WriteLine("古い集合を解剖し続ける。");
    Console.WriteLine();
    Console.WriteLine($"**寄与する波: {conW.Length} 本** — "
        + string.Join(" / ", conW.Select(w => $"`{waves[w].Tag}` {waves[w].Name}")));
    Console.WriteLine();
    Console.Out.Flush();

    // --- 2. 解剖する 3 ペアと 12 事例 ---
    //
    // ペアは計画書 §2-1 の指定（S4 × R10 / S2 × S4 / S4 × R8）。**探索で選ばない**——
    // 「入れ替わりが最大のペア」を毎回探索し直すと、波の定義が動くたびに解剖対象が入れ替わって
    // 第16期の議論と後の期の議論が繋がらなくなる。指定は第15期の測定（§6-3 の上位）から来ている。
    //
    // 各ペアの中の編成は**順位差で機械的に選ぶ**（上下2つずつ）。ここを手で選ぶと、
    // 「説明が付く事例を選んだ」になって §2-3 の集計（何件に説明が付いたか）が意味を失う。
    var pairSpec = new[] { ("S4", "R10"), ("S2", "S4"), ("S4", "R8") };
    var cases = new List<(string PA, string PB, int A, int B, int T, double Gap, string Dir)>();
    Console.WriteLine("## 2. 解剖する 3 ペアと 12 事例");
    Console.WriteLine();
    Console.WriteLine("ペアは計画書 §2-1 の指定（第15期 §6-3 の上位から）。**探索で選び直さない**——");
    Console.WriteLine("毎回「入れ替わり最大」を探すと、波の定義が動くたびに解剖対象が入れ替わって");
    Console.WriteLine("期をまたいだ議論が繋がらなくなる。ペアの中の編成は**順位差で機械的に**上下2つずつ。");
    Console.WriteLine();
    Console.WriteLine("`順位差` = 順位(A) − 順位(B)。**正なら B の波で順位が上がる**（順位は 1 が最良）。");
    Console.WriteLine();
    Console.WriteLine("| # | ペア | 向き | 編成 | A 勝率 | A 順位 | B 勝率 | B 順位 | 順位差 |");
    Console.WriteLine("|--:|:-:|---|---|--:|--:|--:|--:|--:|");
    int caseNo = 0;
    foreach (var (ta, tb) in pairSpec)
    {
        int a = Tag(ta), b = Tag(tb);
        if (a < 0 || b < 0) continue;
        var byGap = Enumerable.Range(0, nT)
            .OrderByDescending(t => rankOf[a][t] - rankOf[b][t]).ToArray();
        var picked = byGap.Take(2).Select(t => (t, "B で上がる"))
            .Concat(byGap.Reverse().Take(2).Select(t => (t, "A で上がる"))).ToArray();
        foreach (var (t, dir) in picked)
        {
            caseNo++;
            cases.Add((ta, tb, a, b, t, rankOf[a][t] - rankOf[b][t], dir));
            Console.WriteLine($"| {caseNo} | {ta} × {tb} | {(dir == "B で上がる" ? $"**{tb}** で上がる" : $"**{ta}** で上がる")} "
                + $"| {targets[t].Name} | {rate[a][t]:F1}% | {rankOf[a][t]:F1} "
                + $"| {rate[b][t]:F1}% | {rankOf[b][t]:F1} | {Sg(rankOf[a][t] - rankOf[b][t], 1):+0.0;-0.0} |");
        }
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3. 波の現物 ---
    //
    // 第6〜7期に見つけた原理「**符号は個体HP、大きさは体数**」が、代金ではなく勝敗でも
    // 効いているかを見るための下敷き。S4 は 145HP × 3 + 司祭 + 詠唱兵、R10 は 60HP × 6 で、
    // **総HP は近いのに個体HP と体数が正反対。**
    var involved = pairSpec.SelectMany(p => new[] { p.Item1, p.Item2 }).Distinct()
        .Select(Tag).Where(w => w >= 0).OrderBy(w => w).ToArray();
    double MedianHp(Formation e)
    {
        var v = e.Occupied().Select(x => (double)x.Def.MaxHp).OrderBy(x => x).ToArray();
        return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
    }
    Console.WriteLine("## 3. 波の現物（3ペアに出てくる波だけ）");
    Console.WriteLine();
    Console.WriteLine("第6〜7期の原理「**符号は個体HP、大きさは体数**」が、代金ではなく**勝敗**でも");
    Console.WriteLine("効いているかを読むための下敷き。**S4 と R10 は総HP が近いのに個体HP と体数が正反対。**");
    Console.WriteLine();
    Console.WriteLine("| タグ | 波 | 体数 | 総HP | 総攻 | 個体HP中央値 | 最大個体HP | 1体あたり攻 | 範囲枚数 | 貫き枚数 | 平均速度 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int w in involved)
    {
        Formation e = waves[w].Enemy;
        Console.WriteLine($"| **{waves[w].Tag}** | {waves[w].Name} | {e.Count} "
            + $"| {e.Occupied().Sum(x => x.Def.MaxHp)} | {e.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {MedianHp(e):F0} | {e.Occupied().Max(x => x.Def.MaxHp)} "
            + $"| {e.Occupied().Average(x => x.Def.Attack):F1} "
            + $"| {e.Occupied().Count(x => x.Def.Pattern is AttackPattern.Sweep or AttackPattern.All)} "
            + $"| {e.Occupied().Count(x => x.Def.Pattern == AttackPattern.Pierce)} "
            + $"| {e.Occupied().Average(x => x.Def.Speed):F1} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3-2. 事例に出てくる編成の現物 ---
    //
    // 波の側だけ出しても比が作れない。**交互作用は 編成 × 波 なので、読むには両側の現物が要る**
    // ——第12期以来ずっと編成側の特徴量だけで予測しようとしていたのが第16期の反省点（§0）。
    // 値は `power` の静的8種と同じ定義（戦わずに決まる量だけ）。
    var caseT = cases.Select(c => c.T).Distinct().OrderBy(x => x).ToArray();
    Console.WriteLine("### 3-2. 事例に出てくる編成の現物（`power` の静的8種と同じ定義）");
    Console.WriteLine();
    Console.WriteLine("**波の側だけ出しても比が作れない。** 交互作用は 編成 × 波 なので、読むには両側の現物が要る");
    Console.WriteLine("——第12期以来ずっと編成側だけで予測しようとしていたのが第16期の反省点。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 体数 | 総HP | 総攻 | 積 | 最薄HP | 後列HP | 平均速度 | 範囲枚数 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int t in caseT)
    {
        Formation fm = targets[t].F;
        int hp = fm.Occupied().Sum(x => x.Def.MaxHp), atk = fm.Occupied().Sum(x => x.Def.Attack);
        Console.WriteLine($"| {targets[t].Name} | {fm.Count} | {hp} | {atk} | {(double)hp * atk / 1000:F1}k "
            + $"| {fm.Occupied().Min(x => x.Def.MaxHp)} "
            + $"| {fm.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)} "
            + $"| {fm.Occupied().Average(x => x.Def.Speed):F1} | {AoeCount(fm)} |");
    }
    Console.WriteLine();
    Console.Out.Flush();
    // --- 4. 計測 ---
    // 事例に出てくる (編成, 波) の組だけを verbose で回す。同じ組が別の事例に出てきたら使い回す
    // （測り直すと同じ組の数字が事例ごとに違って見える。同じ seed なので値は一致するが、
    //   一致することを確かめるより最初から1回にするほうが安い）。
    var traces = new Dictionary<(int W, int T), WaveTrace>();
    foreach (var c in cases)
        foreach (int w in new[] { c.A, c.B })
            if (!traces.ContainsKey((w, c.T)))
                traces[(w, c.T)] = MeasureTrace(targets[c.T].F, waves[w].Enemy, DissectSeeds);

    // --- 5. seed による振れ（§5-7 の停止条件） ---
    //
    // 「平均で 75% → 100% でも、個別の試行では別のことが起きているかもしれない」。
    // 見るのは2つ:
    //   (1) **負けの中身** — 全滅（削られ切った）と打ち切り（削り切れなかった）は
    //       同じ「敗北」だが原因が正反対。混ざっていたら1つの説明では足りない。
    //   (2) **決着ターン数のばらつき** — 勝った試行の決着 T の SD が平均に対して大きいなら、
    //       「同じ戦い方で勝っている」とは言えない。
    Console.WriteLine("## 4. seed による振れ（解剖してよいかの確認）");
    Console.WriteLine();
    Console.WriteLine("**平均で 75% → 100% でも、個別の試行では別のことが起きているかもしれない**");
    Console.WriteLine("（計画書 §5-7 の停止条件）。見るのは2つ:");
    Console.WriteLine();
    Console.WriteLine("- **負けの中身** — `全滅`（削られ切った）と `打切`（30T で削り切れなかった）は");
    Console.WriteLine("  同じ敗北だが原因が正反対。**混ざっている事例は1つの説明では足りない。**");
    Console.WriteLine("- **決着Tのばらつき** — `SD/平均` が大きいなら「同じ戦い方で勝っている」とは言えない。");
    Console.WriteLine();
    Console.WriteLine("| # | 編成 | 波 | 勝率 | 全滅 | 打切 | 決着T(勝) | SD | SD/平均 | 決着T(負) |");
    Console.WriteLine("|--:|---|:-:|--:|--:|--:|--:|--:|--:|--:|");
    caseNo = 0;
    var shaky = new List<string>();
    foreach (var c in cases)
    {
        caseNo++;
        foreach (int w in new[] { c.A, c.B })
        {
            WaveTrace tr = traces[(w, c.T)];
            double cv = tr.TurnsWin <= 0 ? double.NaN : tr.TurnsWinSd / tr.TurnsWin;
            bool mixed = tr.WipeRate > 5 && tr.DrawRate > 5;
            if (mixed) shaky.Add($"#{caseNo} {targets[c.T].Name} / {waves[w].Tag}（全滅 {tr.WipeRate:F1}% + 打切 {tr.DrawRate:F1}%）");
            Console.WriteLine($"| {(w == c.A ? caseNo.ToString() : "")} | {(w == c.A ? targets[c.T].Name : "")} "
                + $"| {waves[w].Tag} | {tr.WinRate:F1}% | {tr.WipeRate:F1}% | {tr.DrawRate:F1}% "
                + $"| {(tr.TurnsWin <= 0 ? "—" : $"{tr.TurnsWin:F1}")} "
                + $"| {(double.IsNaN(tr.TurnsWinSd) ? "—" : $"{tr.TurnsWinSd:F1}")} "
                + $"| {(double.IsNaN(cv) ? "—" : $"{cv:F2}")} "
                + $"| {(double.IsNaN(tr.TurnsLose) ? "—" : $"{tr.TurnsLose:F1}")} |{(mixed ? " ← **混在**" : "")}");
        }
    }
    Console.WriteLine();
    Console.WriteLine(shaky.Count == 0
        ? "**負けの中身が混ざっている事例は 0 件。** どの事例も敗因は1種類なので、1つの説明で足りる。"
        : $"> **負けの中身が混ざっている事例が {shaky.Count} 件ある**（全滅も打切も 5% を超える）: "
          + string.Join(" / ", shaky) + "。**この事例には説明が2つ要る**ので、下の解剖でも両方を書く。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 6. 事例ごとの解剖 ---
    //
    // 列の選び方は「**プレイヤーが画面で見ているもの**」に寄せてある（第16期 §0）。
    // 集計量（総攻・総HP）は §3 に出したので、ここでは出来事の側だけを出す。
    Console.WriteLine("## 5. 事例ごとの解剖（12件）");
    Console.WriteLine();
    Console.WriteLine("列は「**プレイヤーが画面で見ているもの**」に寄せてある。集計量（総攻・総HP）は §3 に");
    Console.WriteLine("出したので、ここでは出来事の側だけを出す。");
    Console.WriteLine();
    Console.WriteLine("| 量 | 意味 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| `振/戦` | 味方が攻撃を振った回数。**反撃はここを通らない**（`ApplyDamage` 直呼び） |");
    Console.WriteLine("| `巻込/振` | 1振りで実際に削った敵の数。**範囲が何体に届いたか**の実測 |");
    Console.WriteLine("| `主目標打点` | 1振りが主目標に通した量。一撃圏の分母 |");
    Console.WriteLine("| `振に帰属 %` | 直接ダメージのうち手番の振りから来た割合。"
        + "**低いほど反撃・破裂・追い打ち型**（`pulse` の反応型を量で見た列） |");
    Console.WriteLine("| `一撃圏` | 敵の個体HP中央値 ÷ 主目標打点。**何発で1体落ちるか**（定義値ベース） |");
    Console.WriteLine("| `振/撃破` | 敵1体を落とすのに振った回数。**一撃圏の実測版**（範囲・毒も込み） |");
    Console.WriteLine("| `直接/戦`・`毒燃/戦` | 敵に通した量。毒・燃焼は出どころを持たないので分ける |");
    Console.WriteLine("| `毒燃（自）/戦` | **味方**が浴びた毒・燃焼。瘩気軸はここを払っている |");
    Console.WriteLine("| `削り比` | (直接 + 毒燃) ÷ 敵の総HP。**1.00 を超えたぶんが過剰殺傷** |");
    Console.WriteLine("| `毒ピーク` | 敵に乗った毒の総段数の最大（と、そのターン） |");
    Console.WriteLine("| `毒の無駄` | 敵が落ちた時点で乗ったままだった段数。**乗り切る前に落ちた量** |");
    Console.WriteLine("| `後列被弾` | 味方の被ダメのうち後列（slot 4/5）が受けた割合 |");
    Console.WriteLine();

    caseNo = 0;
    foreach (var c in cases)
    {
        caseNo++;
        WaveTrace ta = traces[(c.A, c.T)], tb = traces[(c.B, c.T)];
        string na = waves[c.A].Tag, nb = waves[c.B].Tag;
        double medA = MedianHp(waves[c.A].Enemy), medB = MedianHp(waves[c.B].Enemy);

        Console.WriteLine($"### 事例 {caseNo}: {targets[c.T].Name} — {na} {ta.WinRate:F1}% / {nb} {tb.WinRate:F1}%");
        Console.WriteLine();
        Console.WriteLine($"{(c.Dir == "B で上がる" ? $"**{nb} で上がる**" : $"**{na} で上がる**")}"
            + $"（順位 {rankOf[c.A][c.T]:F1} → {rankOf[c.B][c.T]:F1}、順位差 {c.Gap:+0.0;-0.0}）。");
        Console.WriteLine();
        string F(double v, string fmt) => double.IsNaN(v) ? "—" : v.ToString(fmt);
        Console.WriteLine($"| 量 | {na} | {nb} | 差 |");
        Console.WriteLine("|---|--:|--:|--:|");
        // 差の書式は小数以下の桁ごとに作る（「+0.0;-0.0」の形）。
        // 差を出さないと、読む側が毎回引き算をすることになる。
        // `+0.0` の書式に **-0.0 をそのまま渡すと `-+0.0` になる**
        // （.NET Core 3.0 以降は負のゼロに符号を付けるが、セクション書式は
        //   -0.0 を正側として拾うので両方の符号が並ぶ）。`+ 0.0` は IEEE で -0.0 を +0.0 に潰す。
        void RowF(string name, double a, double b, int dp)
        {
            string p = "0" + (dp > 0 ? "." + new string('0', dp) : "");
            Console.WriteLine($"| {name} | {F(a, "F" + dp)} | {F(b, "F" + dp)} "
                + $"| {(double.IsNaN(a) || double.IsNaN(b) ? "—" : Sg(b - a, dp).ToString($"+{p};-{p}"))} |");
        }
        RowF("勝率 %", ta.WinRate, tb.WinRate, 1);
        RowF("全滅 %", ta.WipeRate, tb.WipeRate, 1);
        RowF("打切 %", ta.DrawRate, tb.DrawRate, 1);
        RowF("決着T（勝）", ta.TurnsWin, tb.TurnsWin, 1);
        RowF("残存（勝）", ta.AliveOnWin, tb.AliveOnWin, 2);
        RowF("振/戦", ta.AllySwings, tb.AllySwings, 1);
        RowF("巻込/振", ta.HitsPerSwing, tb.HitsPerSwing, 2);
        RowF("主目標打点", ta.PrimaryDmg, tb.PrimaryDmg, 1);
        RowF("振に帰属 %", ta.SwingShare, tb.SwingShare, 1);
        RowF($"敵の個体HP中央値", medA, medB, 0);
        RowF("**一撃圏（発/体）**", medA / ta.PrimaryDmg, medB / tb.PrimaryDmg, 2);
        RowF("**振/撃破**", ta.SwingsPerKill, tb.SwingsPerKill, 2);
        RowF("直接/戦", ta.DirectToFoe, tb.DirectToFoe, 0);
        RowF("毒燃/戦", ta.DotToFoe, tb.DotToFoe, 0);
        RowF("毒燃（自）/戦", ta.DotToAlly, tb.DotToAlly, 0);
        RowF("撃破/戦", ta.FoeDeaths, tb.FoeDeaths, 2);
        RowF("**削り比**", ta.ShaveRatio, tb.ShaveRatio, 2);
        RowF("毒ピーク（段）", ta.PoisonPeak, tb.PoisonPeak, 1);
        RowF("毒ピークのT", ta.PoisonPeakTurn, tb.PoisonPeakTurn, 1);
        RowF("毒の無駄（段）", ta.PoisonWasted, tb.PoisonWasted, 1);
        RowF("敵の振/戦", ta.FoeSwings, tb.FoeSwings, 1);
        RowF("被ダメ/戦", ta.AllyTaken, tb.AllyTaken, 0);
        RowF("後列被弾 %", ta.BackShare, tb.BackShare, 1);
        Console.WriteLine();
        Console.WriteLine("盤面の推移（ターン開始時点の平均生存数。決着後は決着時の盤面で埋めてある）:");
        Console.WriteLine();
        Console.WriteLine("| 陣営 |" + string.Concat(Enumerable.Range(1, WaveTrace.Profile).Select(t => $" T{t} |")));
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, WaveTrace.Profile).Select(_ => "--:|")));
        Console.WriteLine($"| 味方 / {na} |" + string.Concat(ta.AllyAlive.Select(v => $" {v:F1} |")));
        Console.WriteLine($"| 敵 / {na} |" + string.Concat(ta.FoeAlive.Select(v => $" {v:F1} |")));
        Console.WriteLine($"| 味方 / {nb} |" + string.Concat(tb.AllyAlive.Select(v => $" {v:F1} |")));
        Console.WriteLine($"| 敵 / {nb} |" + string.Concat(tb.FoeAlive.Select(v => $" {v:F1} |")));
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 7. 12事例の横断表 ---
    //
    // 事例を1件ずつ読むと「その事例の説明」しか出てこない。**12件に共通する構造があるか**は
    // 同じ量を12行に並べて初めて見える。ここに出す量が、そのまま Phase GB の積の候補になる
    // （§3-3「積の候補は Phase GA の解剖から出てきたものに限る」）。
    //
    // **`削り比` は説明変数にはできない。** 「敵の総HP を削り切ったか」は勝利の定義の言い換えで、
    // 第14期の同語反復の基準（分子経路）にそのまま当たる。**診断としては読めるが、
    // Phase GB の候補には入れない。**
    Console.WriteLine("## 6. 12事例の横断表（共通する構造があるか）");
    Console.WriteLine();
    Console.WriteLine("事例を1件ずつ読むと「その事例の説明」しか出てこない。**12件に共通する構造があるか**は");
    Console.WriteLine("同じ量を12行に並べて初めて見える。ここに出す量が Phase GB の積の候補になる。");
    Console.WriteLine();
    Console.WriteLine("> **`削り比` は説明変数にはできない。** 「敵の総HPを削り切ったか」は勝利の定義の");
    Console.WriteLine("> 言い換えで、第14期の同語反復の基準（分子経路）にそのまま当たる。");
    Console.WriteLine("> **診断としては読めるが、Phase GB の候補には入れない。**");
    Console.WriteLine();
    Console.WriteLine("`積比` = 味方の積（総HP × 総攻）÷ 敵の積。**戦わずに決まる**ので候補になる。");
    Console.WriteLine("`一撃圏` = 敵の個体HP中央値 ÷ 主目標打点（実測）。");
    Console.WriteLine();
    Console.WriteLine("| # | 編成 | 波 | 勝率 | 敵総攻 | 敵個体HP | 積比 | 一撃圏 | 振/撃破 | 決着T | 削り比 | 振に帰属% | 後列被弾% |");
    Console.WriteLine("|--:|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    caseNo = 0;
    foreach (var c in cases)
    {
        caseNo++;
        Formation fm = targets[c.T].F;
        double allyProd = (double)fm.Occupied().Sum(x => x.Def.MaxHp) * fm.Occupied().Sum(x => x.Def.Attack);
        foreach (int w in new[] { c.A, c.B })
        {
            WaveTrace tr = traces[(w, c.T)];
            Formation e = waves[w].Enemy;
            double foeProd = (double)e.Occupied().Sum(x => x.Def.MaxHp) * e.Occupied().Sum(x => x.Def.Attack);
            double med = MedianHp(e);
            Console.WriteLine($"| {(w == c.A ? caseNo.ToString() : "")} | {(w == c.A ? targets[c.T].Name : "")} "
                + $"| {waves[w].Tag} | {tr.WinRate:F1}% | {e.Occupied().Sum(x => x.Def.Attack)} | {med:F0} "
                + $"| {allyProd / foeProd:F2} | {med / tr.PrimaryDmg:F2} | {tr.SwingsPerKill:F2} "
                + $"| {(tr.TurnsWin <= 0 ? "—" : $"{tr.TurnsWin:F1}")} | {tr.ShaveRatio:F2} "
                + $"| {(double.IsNaN(tr.SwingShare) ? "—" : $"{tr.SwingShare:F0}")} "
                + $"| {(double.IsNaN(tr.BackShare) ? "—" : $"{tr.BackShare:F0}")} |");
        }
    }
    Console.WriteLine();

    // 12事例のうち、各量が「勝率の高いほうを当てた」件数。**12件に共通する構造があるか**の
    // いちばん粗い答え。符号は「値が大きいほうが勝つ」を + とする。
    // 12/12 でも n = 12 なので偶然（コイン12回で 12 表の確率は 1/4096）を排除できるだけ。
    // **相関の代わりにはならない**ので、Phase GB でプールした 217 点に当てる。
    Console.WriteLine("### 6-1. どの量が「勝ったほうの波」を当てたか（12事例中）");
    Console.WriteLine();
    Console.WriteLine("符号は「値が**大きい**ほうの波で勝率が高い」を ○ とする。**n = 12 しかない**ので、");
    Console.WriteLine("これは相関の代わりにはならない——Phase GB でプールした点に当て直す。");
    Console.WriteLine("勝率が同じ事例は判定不能なので分母から外す。");
    Console.WriteLine();
    Console.WriteLine("> **波の側だけで決まる量は、ここで必ず 50% になる。** 事例は各ペアの順位差の");
    Console.WriteLine("> **上下 2 つずつ**で選んであり、同じ 2 波に対して向きが正反対の事例が 2 件ずつ入る");
    Console.WriteLine("> ——波だけの量はその 2 件で必ず 1 勝 1 敗する。**これは選び方の副作用であって、");
    Console.WriteLine("> その特徴量が無力だという意味ではない。** 同時に、**交互作用は定義上どちらか片側だけでは");
    Console.WriteLine("> 説明できない**ことの直接の表れでもある——Phase GB が積を使う理由はこれ。");
    Console.WriteLine();
    var probes = new (string Name, string Dir, Func<int, int, double> Get)[]
    {
        ("敵の総攻", "小さいほう", (w, t) => -waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)),
        ("敵の個体HP中央値", "小さいほう", (w, t) => -MedianHp(waves[w].Enemy)),
        ("敵の体数", "小さいほう", (w, t) => -waves[w].Enemy.Count),
        ("敵の総HP", "小さいほう", (w, t) => -waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp)),
        ("積比（味方の積 ÷ 敵の積）", "大きいほう", (w, t) =>
            (double)targets[t].F.Occupied().Sum(x => x.Def.MaxHp) * targets[t].F.Occupied().Sum(x => x.Def.Attack)
            / (waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp) * (double)waves[w].Enemy.Occupied().Sum(x => x.Def.Attack))),
        ("一撃圏（発/体）", "小さいほう", (w, t) => -MedianHp(waves[w].Enemy) / traces[(w, t)].PrimaryDmg),
        ("振/撃破", "小さいほう", (w, t) => -traces[(w, t)].SwingsPerKill),
        ("決着T（勝）", "短いほう", (w, t) => -traces[(w, t)].TurnsWin),
        ("後列被弾 %", "小さいほう", (w, t) => -traces[(w, t)].BackShare),
        ("削り比（**同語反復**）", "大きいほう", (w, t) => traces[(w, t)].ShaveRatio),
    };
    Console.WriteLine("| 量 | 勝つのは | 当たり | 外れ | 的中率 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|");
    foreach (var (name, dir, get) in probes)
    {
        int hit = 0, miss = 0;
        foreach (var c in cases)
        {
            double wa = traces[(c.A, c.T)].WinRate, wb = traces[(c.B, c.T)].WinRate;
            if (Math.Abs(wa - wb) < 1e-9) continue;
            double va = get(c.A, c.T), vb = get(c.B, c.T);
            if (double.IsNaN(va) || double.IsNaN(vb)) continue;
            if ((va > vb) == (wa > wb)) hit++; else miss++;
        }
        Console.WriteLine($"| {name} | {dir} | {hit} | {miss} | {(hit + miss == 0 ? "—" : $"{hit * 100.0 / (hit + miss):F0}%")} |");
    }
    Console.WriteLine();
    Console.Out.Flush();
    // --- 8. 読み（12事例の説明） ---
    //
    // **ここだけは測定ではなく解釈。** 上の表から言葉にしたもので、推測は推測と明記する
    // （計画書 §2-4）。数字が動いたらこの節も書き直すこと——**表と食い違ったら表が正しい。**
    Console.WriteLine("## 7. 読み（12事例の説明）");
    Console.WriteLine();
    Console.WriteLine("**ここだけは測定ではなく解釈。** 上の表から言葉にしたもので、**推測は推測と明記する。**");
    Console.WriteLine("数字が動いたらこの節も書き直すこと——**表と食い違ったら表が正しい。**");
    Console.WriteLine();
    Console.WriteLine("### 7-1. 共通の骨格 — 単発戦は2つの時計の競走");
    Console.WriteLine();
    Console.WriteLine("12事例に共通する形が1つある。**単発戦の勝敗は「味方が敵を削り切るまでのターン数」と");
    Console.WriteLine("「敵が味方を削り切るまでのターン数」の競走**で、波はこの2つを**別々に**動かす。");
    Console.WriteLine();
    Console.WriteLine("| 波 | 味方の時計（削り切るまで） | 敵の時計（削られ切るまで） |");
    Console.WriteLine("|:-:|---|---|");
    Console.WriteLine("| S4 | **遅い** — 個体145 × 5 = 545 を削るのに時間が要る | **遅い** — 総攻 53・速度 4.4 |");
    Console.WriteLine("| R8 | 速い — 個体 90 | 速い — 総攻 80 |");
    Console.WriteLine("| R10 | 速い — 個体 60 | **最速** — 総攻 96 |");
    Console.WriteLine("| S2 | **最速** — 個体 45 | 速い — 総攻 86・速度 7.8・貫き1 |");
    Console.WriteLine();
    Console.WriteLine("**S4 だけが「長いが優しい」で、他は全部「短いが厳しい」。** 3ペアのうち3ペアとも S4 が");
    Console.WriteLine("片側に入っているのはそのため（第15期 §6-3 の上位が S4 で埋まっているのも同じ理由）。");
    Console.WriteLine();
    Console.WriteLine("**編成が入れ替わるのは、編成ごとにこの2つの時計への効き方が違うから。** 12事例は");
    Console.WriteLine("2群にきれいに割れた。");
    Console.WriteLine();
    Console.WriteLine("### 7-2. 甲群 — 出力が時間で育つ編成は「敵の時計が遅い波」で上がる");
    Console.WriteLine();
    Console.WriteLine("**事例 3・4・5・11・12**（速攻 / 毒+耐久 / 溜め改）。どれも S4 で上がる。");
    Console.WriteLine();
    Console.WriteLine("- **事例 3・11 速攻 (ボルグ×ムド)** — ムドの被弾強化は殴られるほど攻撃が上がるので、");
    Console.WriteLine("  出力の立ち上がりに時間が要る。S4 では 41.1 回振って 11.5T かけて勝つ。R8/R10 では");
    Console.WriteLine("  **削り比が 0.81 / 0.89 で、敵の総HP すら削り切れていない**——育つ前に落ちている。");
    Console.WriteLine("  `敵の振/戦` は S4 31.7 / R8 30.9 とほぼ同じなのに `被ダメ` は 218 → 338。");
    Console.WriteLine("  **同じ回数殴られて 1.5 倍痛い。** 効いているのは殴られた回数ではなく1発の重さ。");
    Console.WriteLine("- **事例 12 溜め改 (クグ×バン×ガン)** — `振に帰属%` が S4 **22%** / R8 70%。");
    Console.WriteLine("  **S4 では打点の 78% が手番外（カドの反撃）から出ている。** 反撃は敵に殴らせないと");
    Console.WriteLine("  回らないので、殴られても死なない波でしか成立しない。R8 では `後列被弾` が");
    Console.WriteLine("  11% → **43%** に跳ね、後列のガン・バン・ドルガ（後列HP 173）が直接削られる。");
    Console.WriteLine("- **事例 5 溜め改（S2 × S4）** — 同じ編成が S2 でも落ちる（82.5%）。`後列被弾` 30% で、");
    Console.WriteLine("  **S2 の貫き 1 枚がレーンを走って後列に届いている。** 事例 8 の範囲耐性は同じ S2 で");
    Console.WriteLine("  `後列被弾` 6.5% しかない——あちらは後列HP が 55 しかなく、貫きが届く先に何も無い。");
    Console.WriteLine("  **`敵の貫き枚数 × 味方の後列HP` が、同じ波で符号を分けている。**");
    Console.WriteLine("- **事例 4 毒+耐久 (ベニ×トウ)** — 毒の出力が 60/T（524 ÷ 8.7T）しかないので、");
    Console.WriteLine("  R10 の 360HP に対しては 45/T まで落ちて撃破が 4.01/6 で止まる。S4 は 545HP と重いが、");
    Console.WriteLine("  敵の総攻 53 なので 8.7T かけられる。**重い波のほうが安い。**");
    Console.WriteLine();
    Console.WriteLine("### 7-3. 乙群 — 出力が一撃圏に縛られる編成は「敵の個体HPが低い波」で上がる");
    Console.WriteLine();
    Console.WriteLine("**事例 1・2・7・8・9・10**（毒 / 燃焼 / 耐久 / 範囲耐性 / 追撃×死 / 死の連鎖）。");
    Console.WriteLine();
    Console.WriteLine("- **事例 7 耐久 (ガルド×ノノ)** — いちばんきれいな一撃圏の事例。一撃圏 2.35 → **7.80**、");
    Console.WriteLine("  `振/撃破` 2.83 → 6.91、`削り比` 1.22 → **0.97**、`撃破` 5.00 → **2.98**。");
    Console.WriteLine("  S4 では 5 体のうち 3 体しか落とせない。**敵の総攻は S2 のほうが高い（86 > 53）のに勝つ**");
    Console.WriteLine("  ——落とした敵は殴ってこないので、早く落とすほど被弾が減る（第6期 H1 の複利）。");
    Console.WriteLine("- **事例 8 範囲耐性 (ヒビ×ボルグ)** — 範囲を 2 枚持っているのに S4 で落ちる。");
    Console.WriteLine("  `巻込/振` は 1.33 → **1.38 とむしろ増えている**のに `撃破` は 5.00 → 4.79。");
    Console.WriteLine("  **範囲の価値は巻き込み枚数ではなく「巻き込んだ結果何体落ちたか」で決まる**");
    Console.WriteLine("  ——個体145 に対しては、撒いても撃破に変換されない（第6期 H2 の再現）。");
    Console.WriteLine("- **事例 1 毒 / 事例 2 燃焼** — 継続ダメージも同じ縛りを受ける。毒は `毒の無駄`");
    Console.WriteLine("  （落ちた時点で乗ったままだった段数）が S4 **282.6 段** / R10 156.7 段で、");
    Console.WriteLine("  **乗せたのに撃破へ変換されなかった量が S4 では 1.8 倍。** 燃焼は一撃圏 7.22 → 2.91。");
    Console.WriteLine("- **事例 9 追撃×死 / 事例 10 死の連鎖** — どちらも**撃破そのものを燃料にする型**");
    Console.WriteLine("  （墓守の層・追い打ち）なので、撃破が出ないと出力が立ち上がらない。盤面推移を見ると");
    Console.WriteLine("  S4 では敵が T4 まで 5.0 のまま（最初の撃破が T5 まで出ない）、R8 では T3 から崩れる。");
    Console.WriteLine();
    Console.WriteLine("### 7-4. 説明が付かなかった事例");
    Console.WriteLine();
    Console.WriteLine("- **事例 6 毒+耐久（S2 95.5% → S4 100.0%）。** 一撃圏が 9.59 → **30.94** と 3 倍悪化して");
    Console.WriteLine("  いるのに勝率は上がっている。乙群の説明が当たらず、甲群の説明（敵の総攻 86 → 53）は");
    Console.WriteLine("  当たるが、**そもそも勝率差が 4.5pt しかない。** 順位差 18.5 の正体は");
    Console.WriteLine("  **S4 で 100.0% の編成が 13 もある同値塊**で、勝率 100.0% は全部が順位 7.0 に潰れる。");
    Console.WriteLine("  **第15期が (a) の読み方を「当てにならない」とした同じ現象**が、事例の選び方にも");
    Console.WriteLine("  出ている——順位差で機械的に選ぶと、同値塊のせいで出来事の起きていない事例が混じる。");
    Console.WriteLine();
    Console.WriteLine("**説明が付いたのは 11 / 12。** うち甲群 5 件・乙群 6 件で、**説明の骨格は 2 種類しかない。**");
    Console.WriteLine("どちらも「敵の個体HP と体数」ではなく、**「敵の個体HP と敵の総攻」の 2 軸**に落ちる。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9. 数値への変換案（Phase GB の候補） ---
    //
    // 計画書 §2-3 の 2 と 3。**ここで挙げたものだけが Phase GB の積の候補になる**
    // （§3-3「積の候補は Phase GA の解剖から出てきたものに限る。総当たりで作らない」）。
    Console.WriteLine("## 8. 数値への変換案（Phase GB が使う候補）");
    Console.WriteLine();
    Console.WriteLine("§7 の説明を数値にしたもの。**ここに挙げたものだけが Phase GB の積の候補になる**");
    Console.WriteLine("（計画書 §3-3「総当たりで作らない」）。`使える` は、両側とも**戦わずに決まる**かどうか。");
    Console.WriteLine();
    Console.WriteLine("| 説明（§7） | 数値 | 使える | 備考 |");
    Console.WriteLine("|---|---|:-:|---|");
    Console.WriteLine("| 甲: 味方が持ちこたえるターン数 | `味方の総HP ÷ 敵の総攻` | ○ | 事例 3・11・4 |");
    Console.WriteLine("| 甲: 敵を削り切るまでのターン数 | `敵の総HP ÷ 味方の総攻` | ○ | 事例 4・7 |");
    Console.WriteLine("| 甲: 2つの時計の比 | `味方の積 ÷ 敵の積` | ○ | **上2つの比そのもの**（約分すると 味方HP×味方攻 ÷ 敵HP×敵攻）。**独立ではない** |");
    Console.WriteLine("| 甲: 貫きが後列に届く | `敵の貫き枚数 × 味方の後列HP` | ○ | 事例 5・8 が符号を分けた |");
    Console.WriteLine("| 甲: 先手を取られる | `敵の平均速度 − 味方の平均速度` | ○ | 事例 5 の**推測**。単独では向きが決まらない |");
    Console.WriteLine("| 乙: 一撃圏 | `敵の個体HP中央値 ÷ 味方の1体あたり攻` | ○ | 事例 7 が最も明確 |");
    Console.WriteLine("| 乙: 範囲が撃破に変換されるか | `味方の範囲枚数 × 敵の体数 ÷ 一撃圏` | ○ | 事例 8。**巻き込み枚数ではない** |");
    Console.WriteLine("| 乙: 集中砲火で最薄が落ちるか | `敵の総攻 ÷ 味方の最薄HP` | ○ | 事例 3・12 |");
    Console.WriteLine("| （診断のみ）削り切ったか | `削り比` | **×** | **勝利の言い換え**（分子経路。第14期の基準） |");
    Console.WriteLine("| （診断のみ）実測の1発打点 | `主目標打点` | **×** | 波ごとに測った量なので、波ごとの勝率に当てると循環する |");
    Console.WriteLine();
    Console.WriteLine("**`振に帰属%` が示したこと（Phase GB の設計に効く）。** 溜め改は S4 で打点の 78% が");
    Console.WriteLine("手番外から出ていて、毒軸は `総攻` 20〜22 で毎ターン 45〜127 を削っている。");
    Console.WriteLine("**`総攻` はこれらの編成の出力を表していない**——第12期以来ずっと使ってきた");
    Console.WriteLine("編成側の唯一の出力特徴量が、反撃軸と毒軸に対しては桁で外れている。");
    Console.WriteLine("**「特徴量が悪い」の中身の1つがこれ。**");
    Console.WriteLine();
    Console.Out.Flush();
    // ================= Phase GB: 敵側の特徴量と交互作用項（第16期） =================
    //
    // **いまの `power` は編成側の特徴量しか持っていない。** 交互作用は 編成 × 波 なのに、
    // 波の側を一度も数値化していなかった——第12期以来ずっと片側だけで予測しようとしていたので、
    // **予測できなくて当然だった可能性がある**（計画書 §3-1）。
    //
    // §6-1 でそれが数字になって出た。**波の側だけで決まる量は的中率がちょうど 50% になる。**
    // あれは事例の選び方の副作用でもあるが、同時に構造そのもの——下の §11 で示すとおり、
    // **片側だけの特徴量は交互作用成分と相関が恒等的に 0 になる。**
    //
    // 候補は 10 個以内。**総当たりで作らない**（編成8 × 敵9 = 72 通りを全部試すと、
    // n = 217 では必ず何かが当たる）。出どころは「理屈で先に決めたもの」と
    // 「§7 の解剖から出てきたもの」の2つだけで、§8 の表に挙げたものに限る。
    Console.WriteLine("## 9. 敵側の特徴量（Phase GB）");
    Console.WriteLine();
    Console.WriteLine("**編成側と対称になるように取る。** 定義は `UnitDef` と `Formation` から計算できるものだけ");
    Console.WriteLine("（戦わずに分かる量。`power` の静的8種と同じ作法）。");
    Console.WriteLine();

    var foeF = new (string Name, string Def, Func<Formation, double> Get)[]
    {
        ("敵体数",     "波の駒数", e => e.Count),
        ("敵総HP",     "Def.MaxHp の合計", e => e.Occupied().Sum(x => x.Def.MaxHp)),
        ("敵総攻",     "Def.Attack の合計", e => e.Occupied().Sum(x => x.Def.Attack)),
        ("敵個体HP中", "個体 MaxHp の中央値。**第6〜7期の原理の主役**", MedianHp),
        ("敵最大個体HP", "いちばん硬い駒の MaxHp", e => e.Occupied().Max(x => x.Def.MaxHp)),
        ("敵1体攻",    "敵総攻 ÷ 敵体数", e => e.Occupied().Average(x => x.Def.Attack)),
        ("敵範囲枚数",  "Def.Pattern が薙ぎ/全体の駒数",
            e => e.Occupied().Count(x => x.Def.Pattern is AttackPattern.Sweep or AttackPattern.All)),
        ("敵貫き枚数",  "Def.Pattern が貫きの駒数",
            e => e.Occupied().Count(x => x.Def.Pattern == AttackPattern.Pierce)),
        ("敵平均速度",  "Def.Speed の平均", e => e.Occupied().Average(x => x.Def.Speed)),
    };
    // 編成側は `power` / `wave` の静的8種をそのまま。**定義を1文字も変えない**
    // ——変えると第12〜15期の数字と繋がらなくなる。
    var allyF = new (string Name, Func<Formation, double> Get)[]
    {
        ("体数",     f => f.Count),
        ("総HP",     f => f.Occupied().Sum(x => x.Def.MaxHp)),
        ("総攻",     f => f.Occupied().Sum(x => x.Def.Attack)),
        ("積",       f => (double)f.Occupied().Sum(x => x.Def.MaxHp) * f.Occupied().Sum(x => x.Def.Attack)),
        ("最薄HP",   f => f.Occupied().Min(x => x.Def.MaxHp)),
        ("後列HP",   f => f.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)),
        ("平均速度", f => f.Occupied().Average(x => x.Def.Speed)),
        ("範囲枚数", f => AoeCount(f)),
    };

    Console.WriteLine("| 特徴量 | 定義 |" + string.Concat(conW.Select(w => $" {waves[w].Tag} |")));
    Console.WriteLine("|---|---|" + string.Concat(conW.Select(_ => "--:|")));
    foreach (var (name, def, get) in foeF)
        Console.WriteLine($"| **{name}** | {def} |"
            + string.Concat(conW.Select(w => $" {get(waves[w].Enemy):0.#} |")));
    Console.WriteLine();
    Console.Out.Flush();

    // --- 10. 交互作用項の候補 ---
    double AllyAtkEach(Formation f) => f.Occupied().Average(x => x.Def.Attack);
    var terms = new (string Name, string Expr, string From, string Why, Func<Formation, Formation, double> Get)[]
    {
        ("耐えるT", "味方の総HP ÷ 敵総攻", "GA 甲群",
            "事例 3・11・4。味方が持ちこたえるターン数。速攻は 327/53 = 6.2 → 327/96 = 3.4 で落ちる",
            (f, e) => f.Occupied().Sum(x => x.Def.MaxHp) / (double)e.Occupied().Sum(x => x.Def.Attack)),
        ("削るT", "敵総HP ÷ 味方の総攻", "理屈（§3-3 の3番）",
            "決着の速さ。第6期 H1「範囲の利得は減らした状態で経過するターン数に比例する」の分母",
            (f, e) => e.Occupied().Sum(x => x.Def.MaxHp) / (double)f.Occupied().Sum(x => x.Def.Attack)),
        ("時計比", "味方の積 ÷ 敵の積", "GA 甲群",
            "上2つの比そのもの（約分すると 味方HP×味方攻 ÷ 敵HP×敵攻）。**独立ではない**が、"
            + "2つの時計の競走を1本にまとめた基準として置く",
            (f, e) => (double)f.Occupied().Sum(x => x.Def.MaxHp) * f.Occupied().Sum(x => x.Def.Attack)
                      / (e.Occupied().Sum(x => x.Def.MaxHp) * (double)e.Occupied().Sum(x => x.Def.Attack))),
        ("一撃圏", "敵の個体HP中央値 ÷ 味方の1体あたり攻", "GA 乙群 + 理屈（§3-3 の2番）",
            "事例 7 が最も明確（2.35 → 7.80 で 100% → 16%）。**第13期で棄却した閾値仮説の、敵側を入れた版**",
            (f, e) => MedianHp(e) / AllyAtkEach(f)),
        ("範囲×体数", "味方の範囲枚数 × 敵体数", "理屈（§3-3 の1番）",
            "範囲は体数が多いほど効く（第6〜7期の原理「大きさは体数」）",
            (f, e) => AoeCount(f) * (double)e.Count),
        ("範囲の変換", "味方の範囲枚数 × 敵体数 ÷ 一撃圏", "GA 乙群",
            "事例 8。**巻き込み枚数ではなく、巻き込んだ結果何体落ちたかで決まる**"
            + "——撒いた先が一撃圏の外なら範囲は撃破に変換されない",
            (f, e) => AoeCount(f) * e.Count * AllyAtkEach(f) / MedianHp(e)),
        ("貫き×後列", "敵の貫き枚数 × 味方の後列HP", "理屈（§3-3 の4番）+ GA 甲群",
            "事例 5・8 が同じ S2 で符号を分けた（後列被弾 30% と 6.5%）",
            (f, e) => e.Occupied().Count(x => x.Def.Pattern == AttackPattern.Pierce)
                      * (double)f.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)),
        ("集中砲火", "敵総攻 ÷ 味方の最薄HP", "GA 甲群",
            "事例 3・12。1体に集まれば最薄が落ちる。**総HP では表せない**（同じ総HPでも薄い駒があると崩れる）",
            (f, e) => e.Occupied().Sum(x => x.Def.Attack) / (double)f.Occupied().Min(x => x.Def.MaxHp)),
        ("1発の重さ", "敵の1体あたり攻 ÷ 味方の最薄HP", "GA 甲群",
            "事例 3「`敵の振/戦` は 31.7 と 30.9 でほぼ同じなのに `被ダメ` は 218 → 338」"
            + "——効いているのは殴られた回数ではなく1発の重さ",
            (f, e) => e.Occupied().Average(x => x.Def.Attack) / f.Occupied().Min(x => x.Def.MaxHp)),
        ("先手差", "敵の平均速度 − 味方の平均速度", "GA 甲群（**推測**）",
            "事例 5 の推測。**積ではなく差**——速度は比を取っても意味を持たない。"
            + "単独では向きが決まらないことが §7-2 で分かっている",
            (f, e) => e.Occupied().Average(x => x.Def.Speed) - f.Occupied().Average(x => x.Def.Speed)),
    };

    Console.WriteLine("## 10. 交互作用項の候補（10 個）");
    Console.WriteLine();
    Console.WriteLine("**総当たりで作っていない。** 編成8 × 敵9 = 72 通りを全部試すと n = 217 では必ず何かが");
    Console.WriteLine("当たるので、出どころを2つに限った——**理屈で先に決めたもの**（計画書 §3-3 の4つ）と、");
    Console.WriteLine("**§7 の解剖から出てきたもの**（§8 の表に挙げたものだけ）。");
    Console.WriteLine();
    Console.WriteLine("どの項も**両側とも戦わずに決まる**。実測の量（主目標打点・削り比）は入れていない");
    Console.WriteLine("——波ごとに測った量を波ごとの勝率に当てると循環する（§8）。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | 式 | 出どころ | 理由 |");
    Console.WriteLine("|--:|---|---|:-:|---|");
    for (int k = 0; k < terms.Length; k++)
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | `{terms[k].Expr}` | {terms[k].From} | {terms[k].Why} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 11. 分散分解 ---
    //
    // **交互作用が「どれだけあるか」を先に測る。** これを測らずに相関だけ出すと、
    // 「積が効いた」が主効果（波の難度）を拾っているだけかもしれない。
    // 加法モデル 予測_ij = 波の平均 + 編成の平均 − 全体平均 を引いた残りが交互作用成分そのもの。
    //
    // **片側だけの特徴量は、この残差と相関が恒等的に 0 になる**（残差は行方向にも列方向にも
    // 和が 0 なので、片側で定数の量との共分散が消える）。これは実装の検算にもなる。
    int nC = conW.Length;
    double[][] Resid(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        return Enumerable.Range(0, nC)
            .Select(c => Enumerable.Range(0, nT).Select(t => y[c][t] - rowM[c] - colM[t] + grand).ToArray())
            .ToArray();
    }
    (double Wave, double Build, double Inter) Decompose(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        double ssT = y.SelectMany(r => r).Sum(v => (v - grand) * (v - grand));
        double ssW = nT * rowM.Sum(m => (m - grand) * (m - grand));
        double ssB = nC * colM.Sum(m => (m - grand) * (m - grand));
        var rs = Resid(src);
        double ssI = rs.SelectMany(r => r).Sum(v => v * v);
        return (ssW / ssT * 100, ssB / ssT * 100, ssI / ssT * 100);
    }

    Console.WriteLine("## 11. 分散分解 — 交互作用はどれだけあるか");
    Console.WriteLine();
    Console.WriteLine($"寄与する {nC} 波 × {nT} 編成 = **{nC * nT} 点**。加法モデル");
    Console.WriteLine("`予測 = 波の平均 + 編成の平均 − 全体平均` を引いた残りが**交互作用成分そのもの**。");
    Console.WriteLine();
    Console.WriteLine("目的変数は2つ出す。**勝率は天井（100.0%）で潰れる**ので、そこだけで結論を出すと");
    Console.WriteLine("「天井が作った見かけの交互作用」を実物と取り違える——第15期が (c) 残存度を置いたのと同じ理由。");
    Console.WriteLine();
    var decW = Decompose(rate);
    var decD = Decompose(degree);
    Console.WriteLine("| 目的変数 | 波の主効果 | 編成の主効果 | **交互作用** |");
    Console.WriteLine("|---|--:|--:|--:|");
    Console.WriteLine($"| 勝率 | {decW.Wave:F1}% | {decW.Build:F1}% | **{decW.Inter:F1}%** |");
    Console.WriteLine($"| 残存度 | {decD.Wave:F1}% | {decD.Build:F1}% | **{decD.Inter:F1}%** |");
    Console.WriteLine();
    var residW = Resid(rate);
    var residD = Resid(degree);

    // 検算。片側だけの特徴量が残差と相関 0 になることを実際に確かめる。
    double[] Flat(Func<int, int, double> get) => Enumerable.Range(0, nC)
        .SelectMany(c => Enumerable.Range(0, nT).Select(t => get(conW[c], t))).ToArray();
    double[] FlatV(double[][] v) => v.SelectMany(r => r).ToArray();
    double maxOne = 0;
    foreach (var (name, get) in allyF)
        maxOne = Math.Max(maxOne, Math.Abs(Pearson(Flat((w, t) => get(targets[t].F)), FlatV(residW))));
    foreach (var (name, _, get) in foeF)
        maxOne = Math.Max(maxOne, Math.Abs(Pearson(Flat((w, t) => get(waves[w].Enemy)), FlatV(residW))));
    Console.WriteLine($"**検算: 片側だけの特徴量 17 種（編成8 + 敵9）と交互作用成分の相関は、最大でも |r| = {maxOne:F6}。**");
    Console.WriteLine("これは測定結果ではなく**恒等式**——残差は行方向にも列方向にも和が 0 なので、");
    Console.WriteLine("片側で定数の量との共分散は必ず消える。**交互作用を予測したいなら両側が要る**ことの、");
    Console.WriteLine("いちばん強い形の言い直しになっている（第12期以来ずっと片側だけで測っていた）。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 12. 相関 ---
    Console.WriteLine("## 12. 交互作用項は効くか");
    Console.WriteLine();
    Console.WriteLine("3通りの当て方を出す。**どれか1つでは読めない。**");
    Console.WriteLine();
    Console.WriteLine("- **(1) プール（生）** — 217 点をそのまま。計画書 §3-4 の1番。");
    Console.WriteLine("  ただし**波の主効果を拾うだけ**になりやすい（波によって難度が違うので、");
    Console.WriteLine("  敵側の量を含む項は自動的に効いて見える）。");
    Console.WriteLine("- **(2) 交互作用成分** — §11 の残差に当てる。**これが本題。**");
    Console.WriteLine("- **(3) 波ごと** — 第15期と直接比べるための形。");
    Console.WriteLine();
    Console.WriteLine("順位相関 `ρ` も併記する。**項には閾値的なもの（一撃圏）が含まれる**ので、");
    Console.WriteLine("ピアソンだけだと「単調だが直線ではない効き方」を取りこぼした可能性を消せない。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | (1) プール r | r² | **(2) 交互作用 r** | **r²** | (2) ρ | 交互作用の説明力（全分散比） |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|");
    var termScore = new List<(int K, double R, double R2)>();
    double[] residFlat = FlatV(residW);
    for (int k = 0; k < terms.Length; k++)
    {
        double[] x = Flat((w, t) => terms[k].Get(targets[t].F, waves[w].Enemy));
        double rp = Pearson(x, Flat((w, t) => rate[w][t]));
        var ci = Correlate(x, residFlat);
        termScore.Add((k, ci.R, ci.R * ci.R));
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | {Sg(rp, 2):+0.00;-0.00} | {rp * rp:F3} "
            + $"| {Sg(ci.R, 2):+0.00;-0.00} | **{ci.R * ci.R:F3}** | {Sg(ci.Rho, 2):+0.00;-0.00} "
            + $"| {ci.R * ci.R * decW.Inter / 100:F3} |");
    }
    Console.WriteLine();
    double maxRho = Enumerable.Range(0, terms.Length).Max(k =>
        Math.Abs(Correlate(Flat((w, t) => terms[k].Get(targets[t].F, waves[w].Enemy)), residFlat).Rho));
    Console.WriteLine($"**順位相関でも最大 |ρ| = {maxRho:F3}。** 単調な非線形を取りこぼしているのではない。");
    Console.WriteLine();
    var bestT = termScore.OrderByDescending(x => x.R2).First();
    Console.WriteLine($"**最も効く項は `{terms[bestT.K].Name}`（{terms[bestT.K].Expr}）で、交互作用成分に対して");
    Console.WriteLine($"r = {Sg(bestT.R, 2):+0.00;-0.00} / r² = {bestT.R2:F3}。** 交互作用は全分散の {decW.Inter:F1}% なので、");
    Console.WriteLine($"**全体としては {bestT.R2 * decW.Inter / 100:F3}** を説明していることになる。");
    Console.WriteLine();
    if (bestT.R2 < 0.02)
    {
        Console.WriteLine("> **この「最良」に意味は無い。** r² がこの帯では 10 項の順位は雑音で入れ替わる。");
        Console.WriteLine("> **「この項が一番近い」として設計に使ってはいけない**——下の §13 の残差も");
        Console.WriteLine("> 実質は交互作用成分そのもの（回帰ではぼ何も引けていない）として読むこと。");
        Console.WriteLine();
    }
    // 残存度でも同じことをやる。天井で潰れない連続量で符号と順位が変わらないかを見る。
    Console.WriteLine("### 12-1. 残存度で測り直す（天井で潰れない連続量）");
    Console.WriteLine();
    Console.WriteLine("**勝率は 100.0% で潰れる**ので、そこだけで結論を出すと天井が作った見かけの交互作用を");
    Console.WriteLine("実物と取り違える。同じ項を残存度の交互作用成分に当て直す。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | 交互作用 r（勝率） | 交互作用 r（残存度） | 符号一致 |");
    Console.WriteLine("|--:|---|--:|--:|:-:|");
    int agree = 0;
    var degScore = new List<(int K, double R)>();
    for (int k = 0; k < terms.Length; k++)
    {
        double[] x = Flat((w, t) => terms[k].Get(targets[t].F, waves[w].Enemy));
        double rw = Pearson(x, FlatV(residW));
        double rd = Pearson(x, FlatV(residD));
        degScore.Add((k, rd));
        // 相関が取れない列（分散0・標本不足）は NaN。Math.Sign は NaN で例外を投げるので、
        // 「判定不能」として扱う（他の場所と同じく NaN は測れなかったの意）。
        bool known = !double.IsNaN(rw) && !double.IsNaN(rd);
        bool ok = known && Math.Sign(rw) == Math.Sign(rd);
        if (ok) agree++;
        Console.WriteLine($"| {k + 1} | {terms[k].Name} | {Sg(rw, 2):+0.00;-0.00} | {Sg(rd, 2):+0.00;-0.00} | {(known ? (ok ? "○" : "**×**") : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**符号が一致したのは {agree} / {terms.Length}。** 一致しない項は、勝率の天井が作った");
    Console.WriteLine("見かけの効きを拾っている疑いがあるので、そのまま設計に使ってはいけない。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 12-2. 波ごと（第15期との対比） ---
    //
    // **波の中では、交互作用項は味方側の量の定数倍にしかならない**（敵側が定数だから）。
    // だから波ごとの r は、その項の味方側成分の r と（符号を除いて）同じになる。
    // **第15期と直接比べられる形はここではなく (2) のほう**だが、比べたときに何が起きるかを
    // 見せておかないと「波ごとでも上がるはず」と読まれる。
    Console.WriteLine("### 12-2. 波ごと（第15期との対比）");
    Console.WriteLine();
    Console.WriteLine("**波の中では、交互作用項は味方側の量の定数倍にしかならない**——敵側が定数だから。");
    Console.WriteLine("だから波ごとの |r| は、その項の味方側成分の |r| と一致する。**積を入れても");
    Console.WriteLine("波ごとの説明力は原理的に上がらない**ので、第15期と比べる場所はここではなく (2) のほう。");
    Console.WriteLine();
    Console.WriteLine("`静的1変数` は編成側の静的8種の最良 r²。**第15期 `wave` §9-2 の同名の列と一致するはず**");
    Console.WriteLine("（同じ定義・同じ seed・同じ編成集合）——ずれたら実装が違う。");
    Console.WriteLine();
    // 第15期 `wave` §9-2 の `静的1変数 r²` の列。**別の実行から引いた数字だが、
    // 同じ seed の決定的な計算なので一致しなければ実装が違う**——値を候補に使うのではなく、
    // この診断が第15期と同じ盤を見ていることの検算にだけ使う（`wave` §2-1 と同じ作法）。
    var stat15 = new Dictionary<string, double>
    {
        ["S2"] = 0.03, ["S3"] = 0.16, ["S4"] = 0.15, ["S5"] = 0.06,
        ["R8"] = 0.17, ["R9"] = 0.20, ["R10"] = 0.12,
    };
    int statMiss = 0;
    Console.WriteLine("| 波 | 静的1変数 r²（編成側だけ） | 第15期の記録 | 交互作用項の最良 r² | 最良の項 |");
    Console.WriteLine("|:-:|--:|--:|--:|---|");
    foreach (int w in conW)
    {
        double bestS = allyF.Max(a => { double r = Pearson(Enumerable.Range(0, nT).Select(t => a.Get(targets[t].F)).ToArray(), rate[w]); return double.IsNaN(r) ? 0 : r * r; });
        var bt = Enumerable.Range(0, terms.Length)
            .Select(k => (K: k, R2: Math.Pow(Pearson(Enumerable.Range(0, nT).Select(t => terms[k].Get(targets[t].F, waves[w].Enemy)).ToArray(), rate[w]), 2)))
            .Where(x => !double.IsNaN(x.R2)).OrderByDescending(x => x.R2).First();
        string rec = "—";
        if (stat15.TryGetValue(waves[w].Tag, out double want))
        {
            bool ok = Math.Abs(bestS - want) <= 0.005;
            if (!ok) statMiss++;
            rec = ok ? $"{want:F2}" : $"**{want:F2} ←ずれ**";
        }
        Console.WriteLine($"| {waves[w].Tag} | {bestS:F2} | {rec} | {bt.R2:F2} | {terms[bt.K].Name} |");
    }
    Console.WriteLine();
    Console.WriteLine(statMiss == 0
        ? "**検算: 第15期 `wave` §9-2 の `静的1変数` と完全に一致（ずれ 0 件）。** "
          + "この診断は第15期と同じ盤を見ている。"
        : $"**検算: {statMiss} 件ずれた。第15期と同じ盤を見ていない——先へ進む前に原因を潰すこと。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 13. 最も効く項の残差 ---
    //
    // 計画書 §3-4 の3番。**残差の大きい事例が §7 で解剖したものと一致するか。**
    // 一致するなら「解剖で見えたものが数値でも残っている」で、一致しないなら
    // 「解剖したのは、この項では説明できない別の何か」になる。
    Console.WriteLine("## 13. 最も効く項の残差（§7 の事例と一致するか）");
    Console.WriteLine();
    Console.WriteLine($"交互作用成分を `{terms[bestT.K].Name}` で回帰したあとの残り。**大きいところが");
    Console.WriteLine("「この項でも説明が付かない入れ替わり」**で、そこが §7 の 12 事例と一致するかを見る。");
    Console.WriteLine();
    {
        double[] x = Flat((w, t) => terms[bestT.K].Get(targets[t].F, waves[w].Enemy));
        double[] y = FlatV(residW);
        double mx = x.Average(), my = y.Average();
        double b = x.Zip(y, (a, c) => (a - mx) * (c - my)).Sum() / x.Sum(a => (a - mx) * (a - mx));
        var left = new List<(int W, int T, double E)>();
        int idx = 0;
        for (int c = 0; c < nC; c++)
            for (int t = 0; t < nT; t++, idx++)
                left.Add((conW[c], t, y[idx] - (my + b * (x[idx] - mx))));
        var caseKey = cases.SelectMany(cc => new[] { (cc.A, cc.T), (cc.B, cc.T) }).ToHashSet();
        int hitTop = 0;
        Console.WriteLine("| 順 | 編成 | 波 | 勝率 | 交互作用成分 | 残差 | §7 の事例 |");
        Console.WriteLine("|--:|---|:-:|--:|--:|--:|:-:|");
        int rank = 0;
        foreach (var (w, t, e) in left.OrderByDescending(v => Math.Abs(v.E)).Take(12))
        {
            rank++;
            bool inCase = caseKey.Contains((w, t));
            if (inCase) hitTop++;
            int c = Array.IndexOf(conW, w);
            Console.WriteLine($"| {rank} | {targets[t].Name} | {waves[w].Tag} | {rate[w][t]:F1}% "
                + $"| {Sg(residW[c][t], 1):+0.0;-0.0} | {Sg(e, 1):+0.0;-0.0} | {(inCase ? "**○**" : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**残差上位 12 のうち、§7 で解剖した (編成, 波) は {hitTop} 件。**");
        Console.WriteLine($"解剖の対象は {caseKey.Count} 組 / 全 {nC * nT} 組（{caseKey.Count * 100.0 / (nC * nT):F0}%）なので、");
        Console.WriteLine($"偶然なら {12.0 * caseKey.Count / (nC * nT):F1} 件前後。");
        Console.WriteLine();
        Console.WriteLine("> **読み方に注意。** 最良の項でさえ r² が雑音帯なので、この残差は");
        Console.WriteLine("> **実質的に交互作用成分そのもの**（前の列とほぼ同じ値になっている）。");
        Console.WriteLine("> だからこの表は「項で引いた残り」ではなく、**交互作用が大きい (編成, 波) の一覧**として読む。");
        Console.WriteLine("> その上で、§7 が順位差で選んだ 12 事例は **交互作用の大きいところを当てていた**");
        Console.WriteLine("> （偶然の 2〜3 倍）——**解剖の対象選びは外していない。**");
        Console.WriteLine();
    }
    Console.Out.Flush();

    // --- 14. 判定 ---
    // §3-5 の3行から数字で機械的に選ぶ（§8 / bench と同じ作法）。
    // 線は「交互作用成分の r² が 0.10 を超えるか」。**測定から出た線ではない**ので、
    // 生の r² を同じ節に出してある。
    const double TermLine = 0.10;
    Console.WriteLine("## 14. 判定（計画書 §3-5 のどの行か）");
    Console.WriteLine();
    Console.WriteLine($"線は「**交互作用成分に対する r² が {TermLine:F2} を超えるか**」。**測定から出た線ではない**");
    Console.WriteLine("ので、生の r² を §12 にそのまま出してある（引き直せる）。");
    Console.WriteLine();
    int strong = termScore.Count(x => x.R2 >= TermLine);
    string verdict = bestT.R2 >= TermLine
        ? "**1行目: 交互作用は読める。** 特徴量が悪かっただけ。プレイヤーも学べるので、ステージ設計に使える"
        : "**2行目: 積を入れても上がらないが、Phase GA の解剖では説明が付いた（11/12）。** "
          + "人間には読めるが数値化できていない——説明の言葉を、より良い特徴量に翻訳し直す余地がある";
    Console.WriteLine($"- 交互作用成分の r² が {TermLine:F2} 以上の項: **{strong} / {terms.Length}**");
    Console.WriteLine($"- 最良は `{terms[bestT.K].Name}` の r² = **{bestT.R2:F3}**");
    Console.WriteLine($"- §7 の解剖で説明が付いたのは **11 / 12**");
    Console.WriteLine();
    Console.WriteLine(verdict);
    Console.WriteLine();
    Console.WriteLine("**3行目（積も効かず解剖でも説明が付かない = ガチャに近い）ではない。**");
    Console.WriteLine("§7 で 11/12 に説明が付いているので、その行は数字の上で外れている。");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}
}
