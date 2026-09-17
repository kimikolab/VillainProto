using BattleCore;
using static Common;

// =====================================================================================
// relay モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "relay")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 relay
// =====================================================================================

static class RelayModeDiag
{
// 渡し（転嫁）を測る（第43期）。窓口 BattleContext.Dull の中で味方から敵へ移った量を数え、
// **流した量ではなく「味方の被ダメージがいくら減ったか」**で読む。
//
// **「渡し」と「効き」を分けて数えるのが要。** 第42期が「生成したアーマー」ではなく
// 「実際に吸った量」で判断して死蔵率 2.5% を出したのと同じ理由で、流した量は成果ではない
// ——敵の攻撃力を下げても、その敵が既に死んでいたり、もともと殴らない駒だったりすれば
// 効いていない。効きの分母は**敵が味方に与えたダメージ**（敵 tally の DamageToEnemy）で、
// 転嫁を止めた同じ台（RelayRule(0)）との差で取る。
//
// **「自弁率」も必須。** 代金は ApplyDamage を通すので肩代わり5種が割り込む。
// 「横取り量 × 2 を払った」ことにはならない。
public static void Run(string[] args, int stageIndex)
{
    var reBuilds = CompareBuilds();
    const int ReSeeds = 200;   // compare / spread / shove / dull と同じ。balance.md と突き合わせる

    // 第2引数に `kubi` を渡すと変種Cだけを回す（主表と検算は 47行×5波×200seed×2版 で重い）。
    bool reKubiOnly = args.Length > 2 && args[2] == "kubi";
    string reFilter = args.Length > 2 && !reKubiOnly ? args[2] : "渡し";
    var reTargets = reBuilds
        .Where(b => reFilter.Length == 0 || reFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    IReadOnlyList<EnemyCatalog.Stage> reStages = EnemyCatalog.Stages;

    // 敵が味方に与えたダメージ。**敵側の tally から取る**（第13期 Phase DA と同じ理由）。
    // 味方側の DamageTaken から引くと、渡しの代金（source が null の自傷）が混ざる
    // ——あれは TakenFromAlly にも載らない（ApplyDamage は source が null だと
    // 味方由来の印を立てない）ので、味方側からは分離できない。
    static int EnemyOutput(BattleResult r, Formation enemy)
    {
        int sum = 0;
        foreach ((int _, UnitDef d) in enemy.Occupied())
            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) sum += t.DamageToEnemy;
        return sum;
    }

    (double Taken, double Passed, double Sent, double Cost, double SelfPaid,
     double MaxSent, double Zeroed, double Foe, double Death, double Died,
     double Turns, double Win, Dictionary<string, double> From, Dictionary<string, double> To)
    MeasureRelay(Formation f, Formation enemy, RelayRule rule)
    {
        var from = new Dictionary<string, double>();
        var to = new Dictionary<string, double>();
        double taken = 0, passed = 0, sent = 0, cost = 0, self = 0, maxSent = 0, zero = 0;
        double foe = 0, death = 0, died = 0, turns = 0, win = 0;

        for (int seed = 0; seed < ReSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                                    null, null, null, null, null, null, null, rule);
            taken += r.RelayTaken; passed += r.BearPassed; sent += r.RelaySent;
            cost += r.RelayCost; self += r.RelaySelfPaid; zero += r.RelayZeroed;
            if (r.RelayMaxSent > maxSent) maxSent = r.RelayMaxSent;
            foe += EnemyOutput(r, enemy);
            turns += r.Turns; if (r.PlayerWon) win++;
            foreach (var kv in r.RelayFrom)
                from[kv.Key] = from.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in r.RelayTo)
                to[kv.Key] = to.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

            // 早逝: ワタが倒れた試行の、倒れたターン（UnitTally.LastActiveTurn は死亡時に
            // その手番のターンで上書きされる）。倒れなかった試行は分母に入れない。
            if (r.TallyByUnit.TryGetValue(UnitCatalog.Wata.Id, out UnitTally? wt) && wt.Deaths > 0)
            {
                died++; death += wt.LastActiveTurn;
            }
        }

        double n = ReSeeds, md = Math.Max(1, died);
        foreach (string k in from.Keys.ToList()) from[k] /= n;
        foreach (string k in to.Keys.ToList()) to[k] /= n;
        return (taken / n, passed / n, sent / n, cost / n, self / n, maxSent, zero / n,
                foe / n, death / md, died / n, turns / n, win * 100.0 / n, from, to);
    }

    Console.WriteLine("# 転嫁（relay）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 relay [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{ReSeeds - 1}。数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`CompareBuilds()` / `Stages` / `Columns` は触っていない。");
    Console.WriteLine("既定の絞り込みは `渡し`（引数で上書きできる）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 横取り | ワタが引き受けた量/戦（`RelayTaken`） |");
    Console.WriteLine("| 素通り | 隣接に横取り役がいなくて素通りした量/戦（`BearPassed`） |");
    Console.WriteLine("| 渡し | 敵へ流した量/戦（`RelaySent`） |");
    Console.WriteLine("| 最大流入 | **1回の `Dull` で流した最大量**（全 seed の最大。崖の検算） |");
    Console.WriteLine("| 攻ゼロ | 転嫁で敵の `CurrentAttack` が 0 になった回数/戦（**崖の検算**） |");
    Console.WriteLine("| 代金 | `ApplyDamage` へ渡した総量/戦（= 横取り × 2） |");
    Console.WriteLine("| 自弁 | そのうち**ワタ自身の身に落ちた量**/戦。`自弁率` = 自弁 ÷ 代金 |");
    Console.WriteLine("| 敵与ダメ | **敵が味方に与えたダメージ**/戦（敵 tally の `DamageToEnemy` の和） |");
    Console.WriteLine("| 早逝 | ワタが倒れたターン（倒れた試行の平均）と、倒れた試行の割合 |");
    Console.WriteLine();
    Console.WriteLine("**流した量は成果ではない。** 採否は「敵与ダメ」が対照よりいくら減ったか（＝効き）で読む。");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準2）------------------------------------------------------
    if (reKubiOnly) { reTargets = Array.Empty<(string Name, Formation F)>(); goto reVariants; }
    Console.WriteLine("## 0. 検算 —— ワタを含まない行は `RelayRule` に対して不変か（受け入れ基準2）");
    Console.WriteLine();
    Console.WriteLine("渡し役が盤上にいなければ横取りは1回も走らないので、`TransferPercent` を");
    Console.WriteLine("どう振っても勝率は1セルも動かないはず。**分母はセル数**。");
    Console.WriteLine();
    {
        int cells = 0, diff = 0, rows = 0;
        foreach (var b in reBuilds)
        {
            if (b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Wata.Id)) continue;
            rows++;
            for (int w = 0; w < reStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < ReSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, reStages[w].Enemy, seed, false,
                                         null, null, null, null, null, null, null, new RelayRule(0)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, reStages[w].Enemy, seed, false,
                                         null, null, null, null, null, null, null, new RelayRule(100)).PlayerWon) c++;
                }
                cells++; if (a != c) diff++;
            }
        }
        Console.WriteLine($"`RelayRule(0)` と `RelayRule(100)` の突き合わせ: **{cells} セル中 {diff} 件の食い違い**"
            + $"（ワタを含まない {rows} 行 × {reStages.Count} 波）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    foreach (var b in reTargets)
    {
        Console.WriteLine($"## {b.Name}");
        Console.WriteLine();
        Console.WriteLine("### 1. 計数（`RelayRule.Default` ＝ TransferPercent 100）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 横取り | 素通り | 渡し | 最大流入 | 攻ゼロ | 代金 | 自弁 | 自弁率 | 敵与ダメ | 早逝(T/率) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

        var full = new (double Taken, double Passed, double Sent, double Cost, double SelfPaid,
                        double MaxSent, double Zeroed, double Foe, double Death, double Died,
                        double Turns, double Win, Dictionary<string, double> From,
                        Dictionary<string, double> To)[reStages.Count];

        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(b.F, reStages[w].Enemy, RelayRule.Default);
            full[w] = z;
            Console.WriteLine($"| 第{w + 1}波 | {z.Win:0.0}% | {z.Taken:0.00} | {z.Passed:0.00} | {z.Sent:0.00} "
                + $"| {z.MaxSent:0} | {z.Zeroed:0.00} | {z.Cost:0.00} | {z.SelfPaid:0.00} "
                + $"| {(z.Cost > 0 ? $"{z.SelfPaid * 100 / z.Cost:0.0}%" : "—")} | {z.Foe:0.0} "
                + $"| {z.Death:0.0} / {z.Died * 100:0.0}% | {z.Turns:0.0} |");
            Console.Out.Flush();
        }

        Console.WriteLine();
        Console.WriteLine("#### 横取りの相手（量/戦・全波の合計）");
        Console.WriteLine();
        Console.WriteLine("**なまりは「守られた駒」に乗る**ので、ここに出るのは");
        Console.WriteLine("「ワタの隣にいる駒」ではなく「ワタの隣で**殴られた**駒」。");
        Console.WriteLine();
        Console.WriteLine("| 取られた相手 | 量/戦（5波合計） |");
        Console.WriteLine("|---|--:|");
        var fromAll = new Dictionary<string, double>();
        foreach (var z in full)
            foreach (var kv in z.From)
                fromAll[kv.Key] = fromAll.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
        foreach (var kv in fromAll.OrderByDescending(k => k.Value))
            Console.WriteLine($"| {kv.Key} | {kv.Value:0.00} |");

        Console.WriteLine();
        Console.WriteLine("#### 流し先（量/戦・波ごと）");
        Console.WriteLine();
        Console.WriteLine("**最高攻撃力の生存駒を決定的に選ぶ**ので、上から均されて対象が移る。");
        Console.WriteLine("1体に集中していたら自己分散が働いていない（＝崖の予兆）。");
        Console.WriteLine();
        for (int w = 0; w < reStages.Count; w++)
        {
            if (full[w].To.Count == 0) { Console.WriteLine($"- **第{w + 1}波**: （転嫁なし）"); continue; }
            Console.WriteLine($"- **第{w + 1}波**: "
                + string.Join(" / ", full[w].To.OrderByDescending(k => k.Value)
                                              .Select(kv => $"{kv.Key} {kv.Value:0.0}")));
        }
        Console.WriteLine();

        // --- 2. 陽性対照 -------------------------------------------------------------
        Console.WriteLine("### 2. 陽性対照 `RelayRule(0)`（横取りするが流さない＝除去役）");
        Console.WriteLine();
        Console.WriteLine("`TransferPercent = 0` は**転嫁だけを止める**（横取りも代金もそのまま走る）。");
        Console.WriteLine("**弱体はそこで消滅する**ので、これは除去役そのもの。");
        Console.WriteLine("`効き` は同じ台の転嫁ありとの敵与ダメの差（**正なら転嫁が敵の出力を削っている**）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 横取り | 渡し | 代金 | 自弁率 | 敵与ダメ | **効き** | 早逝(T/率) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(b.F, reStages[w].Enemy, new RelayRule(0));
            Console.WriteLine($"| 第{w + 1}波 | {z.Win:0.0}% | {z.Taken:0.00} | {z.Sent:0.00} | {z.Cost:0.00} "
                + $"| {(z.Cost > 0 ? $"{z.SelfPaid * 100 / z.Cost:0.0}%" : "—")} | {z.Foe:0.0} "
                + $"| **{z.Foe - full[w].Foe:+0.0;-0.0}** | {z.Death:0.0} / {z.Died * 100:0.0}% | {z.Turns:0.0} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 3. 掃引 -----------------------------------------------------------------
        Console.WriteLine("### 3. 掃引（`TransferPercent` 0 / 50 / 100）");
        Console.WriteLine();
        Console.WriteLine("**0（除去）が 100（転嫁）と同等以上なら、転嫁という機構は要らない**（受け入れ基準10）。");
        Console.WriteLine("代金は3点とも同じなので、差は「流した先で何が起きたか」だけ。");
        Console.WriteLine();
        Console.WriteLine("| TransferPercent | 平均勝率 | 横取り/戦 | 渡し/戦 | 代金/戦 | 自弁率 | 攻ゼロ/戦 | 敵与ダメ/戦 | 早逝率 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        int[] tps = { 0, 50, 100 };
        var sweep = new double[tps.Length][];
        for (int i = 0; i < tps.Length; i++)
        {
            double win = 0, tk = 0, sn = 0, cs = 0, sp = 0, zr = 0, fo = 0, dd = 0;
            var per = new double[reStages.Count];
            for (int w = 0; w < reStages.Count; w++)
            {
                var z = MeasureRelay(b.F, reStages[w].Enemy, new RelayRule(tps[i]));
                per[w] = z.Win;
                win += z.Win; tk += z.Taken; sn += z.Sent; cs += z.Cost; sp += z.SelfPaid;
                zr += z.Zeroed; fo += z.Foe; dd += z.Died;
            }
            sweep[i] = per;
            int n = reStages.Count;
            Console.WriteLine($"| {tps[i]} | {win / n:0.0}% | {tk / n:0.00} | {sn / n:0.00} | {cs / n:0.00} "
                + $"| {(cs > 0 ? $"{sp * 100 / cs:0.0}%" : "—")} | {zr / n:0.00} | {fo / n:0.0} | {dd * 100 / n:0.0}% |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine("### 4. 波ごとの勝率（受け入れ基準5 ＝ 崖になっていないか）");
        Console.WriteLine();
        Console.WriteLine("| TransferPercent | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < tps.Length; i++)
            Console.WriteLine($"| {tps[i]} | " + string.Join(" | ", sweep[i].Select(v => $"{v:0.0}%"))
                + $" | {sweep[i].Average():0.0}% |");
        Console.WriteLine();
    }

    // --- 5. 変種 ---------------------------------------------------------------------
reVariants:
    Console.WriteLine("## 変種（`CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine("採用行の**席か1枚だけ**を差し替えた版を診断のローカルに組む");
    Console.WriteLine("（`gradient` / `aim` / `route` / `dull` と同じ扱い）。");
    Console.WriteLine();

    if (reKubiOnly) goto reKubi;
    Console.WriteLine("### A. ワタ抜き（4体）—— 陽性対照その2");
    Console.WriteLine();
    Console.WriteLine("**4体版が5体版と同じ値なら、その台は飽和していて測定になっていない**");
    Console.WriteLine("（第21期 `swap` の検査）。中央を空けたぶんの体の値段も混ざるので、");
    Console.WriteLine("**符号ではなく「動くかどうか」だけを読む**。");
    Console.WriteLine();

    var baseF = Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
                                center: UnitCatalog.Wata, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    var noWata = Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
                                 back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);

    Console.WriteLine("| 版 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 横取り/戦 | 渡し/戦 | 敵与ダメ/戦 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var (vn, vf) in new[] { ("ワタあり（採用行）", baseF), ("ワタ抜き（4体・中央 空）", noWata) })
    {
        double tk = 0, sn = 0, fo = 0;
        var per = new double[reStages.Count];
        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(vf, reStages[w].Enemy, RelayRule.Default);
            per[w] = z.Win; tk += z.Taken; sn += z.Sent; fo += z.Foe;
        }
        int n = reStages.Count;
        Console.WriteLine($"| {vn} | {per.Average():0.0}% | " + string.Join(" | ", per.Select(v => $"{v:0.0}%"))
            + $" | {tk / n:0.00} | {sn / n:0.00} | {fo / n:0.0} |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    // --- B. 隣接が値段として機能するか（受け入れ基準8）--------------------------------
    Console.WriteLine("### B. 名指しした駒（ドルガ）を隣に置くか（受け入れ基準8）");
    Console.WriteLine();
    Console.WriteLine("**ワタの席は固定（前1）で、ドルガの席だけを動かす。** 前1の隣接は");
    Console.WriteLine("`{中央, 後1}` なので、ドルガを後1に置けば隣接・後3に置けば非隣接になる");
    Console.WriteLine("（`AdjacencyTable` 参照）。**動く変数はドルガとドハの入れ替え1つだけ。**");
    Console.WriteLine();
    Console.WriteLine("ドルガはロスター最高攻撃力（38）で、しかも**2ターンに1回しか動けない**");
    Console.WriteLine("＝1回の振りの価値が2倍。**「隣に置く価値のある駒」の条件に最も近い**");
    Console.WriteLine("——第42期の集約はこれを先に決めていなかったので、隣接がコストにしかならなかった。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 横取り/戦 | 渡し/戦 | 敵与ダメ/戦 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var adj = Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Gald,
                              center: UnitCatalog.Nono, back1: UnitCatalog.Dolga, back3: UnitCatalog.Doha);
    var far = Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Gald,
                              center: UnitCatalog.Nono, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    foreach (var (vn, vf) in new[] { ("隣接（ワタ前1 / ドルガ後1）", adj), ("非隣接（ワタ前1 / ドルガ後3）", far) })
    {
        double tk = 0, sn = 0, fo = 0;
        var per = new double[reStages.Count];
        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(vf, reStages[w].Enemy, RelayRule.Default);
            per[w] = z.Win; tk += z.Taken; sn += z.Sent; fo += z.Foe;
        }
        int n = reStages.Count;
        Console.WriteLine($"| {vn} | {per.Average():0.0}% | " + string.Join(" | ", per.Select(v => $"{v:0.0}%"))
            + $" | {tk / n:0.00} | {sn / n:0.00} | {fo / n:0.0} |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    // --- C. 萎縮（クビ）との同居（予測4）---------------------------------------------
reKubi:
    Console.WriteLine("### C. 萎縮（クビ）との同居 —— 開戦時1回・1体につき 9（予測4）");
    Console.WriteLine();
    Console.WriteLine("採用行のノノをクビに差し替える。萎縮は**開戦時1回・味方1体につき 9**なので、");
    Console.WriteLine("中央（隣接次数4）のワタは1ターン目に 4体ぶん = 36 を横取りし、代金は 72。");
    Console.WriteLine("**HP84 に対して 86%。** 角（次数2）なら 18 / 代金 36。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 横取り/戦 | 代金/戦 | 自弁率 | 渡し/戦 | 攻ゼロ/戦 | 早逝(T/率) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var kubiMid = Formation.Build(front1: UnitCatalog.Kubi, front3: UnitCatalog.Gald,
                                  center: UnitCatalog.Wata, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    var kubiCorner = Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Gald,
                                     center: UnitCatalog.Kubi, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    foreach (var (vn, vf) in new[] { ("クビ同居（ワタ中央・次数4）", kubiMid), ("クビ同居（ワタ前1・次数2）", kubiCorner) })
    {
        double tk = 0, cs = 0, sp = 0, sn = 0, zr = 0, de = 0, dd = 0;
        var per = new double[reStages.Count];
        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(vf, reStages[w].Enemy, RelayRule.Default);
            per[w] = z.Win; tk += z.Taken; cs += z.Cost; sp += z.SelfPaid;
            sn += z.Sent; zr += z.Zeroed; de += z.Death; dd += z.Died;
        }
        int n = reStages.Count;
        Console.WriteLine($"| {vn} | {per.Average():0.0}% | " + string.Join(" | ", per.Select(v => $"{v:0.0}%"))
            + $" | {tk / n:0.00} | {cs / n:0.00} "
            + $"| {(cs > 0 ? $"{sp * 100 / cs:0.0}%" : "—")} | {sn / n:0.00} | {zr / n:0.00} "
            + $"| {de / n:0.0} / {dd * 100 / n:0.0}% |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}
}
