using BattleCore;
using static Common;

// =====================================================================================
// dull モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "dull")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 dull
// =====================================================================================

static class DullDiag
{
// 弱体を通貨として測る（第42期）。窓口 BattleContext.Dull を通った量を経路別に数え、
// 集約（引き受け・ウケ）がそれをどれだけ横取りしてアーマーに変えたかを出す。
//
// **経路別はログではなく結果（BattleResult.DullByRoute）から取る。** 開戦時1回の3経路
// （呪詛の敵側・呪詛の味方漏れ・萎縮）はログを**1行にまとめて**出すので、
// 文字列からは延べ体数が復元できない（gullet log / yoke log / sever がログを数えたのは、
// あちらが「その行が出たか出なかったか」そのものを見ていたから）。
//
// **「鎧」と「死蔵」を分けて数える。** 生成量だけを見ると第23期の巨躯の吐き戻し
// （経路は通ったが、攻撃力という遅い通貨に変換したので使う前に戦闘が終わる）と
// 同じ穴に落ちる。アーマーも遅い通貨かもしれない。
public static void Run(string[] args, int stageIndex)
{
    var duBuilds = CompareBuilds();
    const int DuSeeds = 200;   // compare / spread / shove と同じ。balance.md と突き合わせる

    string duFilter = args.Length > 2 ? args[2] : "分かち×逆しま,引き受け";
    var duTargets = duBuilds
        .Where(b => duFilter.Length == 0 || duFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    IReadOnlyList<EnemyCatalog.Stage> duStages = EnemyCatalog.Stages;

    // 味方の InstanceId はスロット昇順に 0 から振られる（Materialize → ctx.Add）。
    static int IdOf(Formation f, TraitId t)
    {
        for (int i = 0, k = 0; i < FormationRules.PlayableSlotCount; i++)
            if (f[i] is { } d) { if (d.Traits.Contains(t)) return k; k++; }
        return -1;
    }

    (double Total, double[] Route, double Taken, double Passed, double Armor, double Soaked,
     double UtsuOpen, double UtsuMax, double UtsuLast, double UkeOpen, double UkeLast,
     double Turns, double Win, Dictionary<string, double> From)
    MeasureDull(Formation f, Formation enemy, BearRule rule)
    {
        var route = new double[DullRoutes.Count];
        var from = new Dictionary<string, double>();
        double total = 0, taken = 0, passed = 0, armor = 0, soaked = 0, turns = 0, win = 0;
        double uo = 0, um = 0, ul = 0, useen = 0, ko = 0, kl = 0, kseen = 0;

        int utsuId = IdOf(f, TraitId.Perverse);
        int ukeId = IdOf(f, TraitId.Bear);

        for (int seed = 0; seed < DuSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: true,
                                    null, null, null, null, null, null, rule);
            total += r.DullTotal;
            for (int i = 0; i < route.Length; i++) route[i] += r.DullByRoute[i];
            taken += r.BearTaken; passed += r.BearPassed;
            armor += r.BearArmor; soaked += r.BearSoaked;
            turns += r.Turns; if (r.PlayerWon) win++;
            foreach (var kv in r.BearFrom)
                from[kv.Key] = from.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

            if (utsuId >= 0)
            {
                var snaps = r.Events.Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == utsuId)
                                    .Select(e => e.Amount).ToList();
                if (snaps.Count > 0) { uo += snaps[0]; um += snaps.Max(); ul += snaps[^1]; useen++; }
            }
            if (ukeId >= 0)
            {
                var snaps = r.Events.Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == ukeId)
                                    .Select(e => e.Amount).ToList();
                if (snaps.Count > 0) { ko += snaps[0]; kl += snaps[^1]; kseen++; }
            }
        }

        double n = DuSeeds, mu = Math.Max(1, useen), mk = Math.Max(1, kseen);
        for (int i = 0; i < route.Length; i++) route[i] /= n;
        foreach (string k in from.Keys.ToList()) from[k] /= n;
        return (total / n, route, taken / n, passed / n, armor / n, soaked / n,
                uo / mu, um / mu, ul / mu, ko / mk, kl / mk, turns / n, win * 100.0 / n, from);
    }

    Console.WriteLine("# 弱体の通貨（dull）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 dull [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{DuSeeds - 1}。数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`CompareBuilds()` / `Stages` / `Columns` は触っていない。");
    Console.WriteLine("既定の絞り込みは `分かち×逆しま,引き受け`（引数で上書きできる）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 弱体総量 | 窓口 `Dull` を通った総量/戦（**両陣営**。呪詛の敵側を含む） |");
    Console.WriteLine("| 経路別 | なまり / 呪詛敵 / 呪詛漏れ / 突き返し / 萎縮 の内訳 |");
    Console.WriteLine("| 横取り | 集約役が引き受けた量/戦 |");
    Console.WriteLine("| 素通り | 横取りされずにそのまま入った量/戦（隣に集約役がいない） |");
    Console.WriteLine("| 鎧 | 生成したアーマー量/戦 と、**実際にダメージを吸った量**/戦 |");
    Console.WriteLine("| 死蔵 | 生成したのに使われずに終わった量/戦（= 生成 − 吸った） |");
    Console.WriteLine("| ウツ攻 | 逆しま持ちの `CurrentAttack`（開戦時 / 最大 / 最終T）。`StatSnapshot` から |");
    Console.WriteLine("| ウケ攻 | 集約持ちの `CurrentAttack`（開戦時 / 最終T） |");
    Console.WriteLine();
    Console.WriteLine("**生成量ではなく吸った量で判断する**（第23期の吐き戻しと同じ穴を避けるため）。");
    Console.WriteLine();

    // ---- 検算: ウケ抜きの行は BearRule に対して不変か（受け入れ基準3）-----------------------
    Console.WriteLine("## 0. 検算 —— ウケを含まない行は `BearRule` に対して不変か（受け入れ基準3）");
    Console.WriteLine();
    Console.WriteLine("集約役が盤上にいなければ横取りは1回も走らないので、`ArmorPerDull` を");
    Console.WriteLine("どう振っても勝率は1セルも動かないはず。**分母はセル数**。");
    Console.WriteLine();
    {
        var plain = duBuilds.Where(b => !Enumerable.Range(0, FormationRules.PlayableSlotCount)
                                        .Any(i => b.F[i]?.Id == "uke")).ToArray();
        int cells = 0, diff = 0;
        foreach (var (_, bf) in plain)
            for (int w = 0; w < duStages.Count; w++)
            {
                int a = 0, b3 = 0;
                for (int seed = 0; seed < DuSeeds; seed++)
                {
                    if (BattleEngine.Run(bf, duStages[w].Enemy, seed, false, null, null, null, null,
                                         null, null, new BearRule(0)).PlayerWon) a++;
                    if (BattleEngine.Run(bf, duStages[w].Enemy, seed, false, null, null, null, null,
                                         null, null, new BearRule(3)).PlayerWon) b3++;
                }
                cells++; if (a != b3) diff++;
            }
        Console.WriteLine($"`BearRule(0)` と `BearRule(3)` の突き合わせ: **{cells} セル中 {diff} 件の食い違い**"
            + $"（ウケを含まない {plain.Length} 行 × {duStages.Count} 波）。");
    }
    Console.WriteLine();

    foreach (var (bname, bf) in duTargets)
    {
        Console.WriteLine($"## {bname}");
        Console.WriteLine();

        bool hasUke = Enumerable.Range(0, FormationRules.PlayableSlotCount)
                                .Any(i => bf[i]?.Id == "uke");

        Console.WriteLine($"### 1. 計数（`BearRule.Default` ＝ ArmorPerDull {BearRule.Default.ArmorPerDull}）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 弱体総量 | なまり | 呪詛敵 | 呪詛漏れ | 突き返し | 萎縮 | 横取り | 素通り | 鎧(生成/吸) | 死蔵 | ウツ攻(開/最大/終) | ウケ攻(開/終) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

        var fromAll = new Dictionary<string, double>();
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(bf, duStages[w].Enemy, BearRule.Default);
            foreach (var kv in z.From)
                fromAll[kv.Key] = fromAll.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            Console.WriteLine($"| 第{w + 1}波 | {z.Win:0.0}% | {z.Total:0.00} "
                + $"| {z.Route[(int)DullRoute.Sharer]:0.00} | {z.Route[(int)DullRoute.CurseEnemy]:0.00} "
                + $"| {z.Route[(int)DullRoute.CurseLeak]:0.00} | {z.Route[(int)DullRoute.Shove]:0.00} "
                + $"| {z.Route[(int)DullRoute.Cower]:0.00} "
                + $"| {z.Taken:0.00} | {z.Passed:0.00} | {z.Armor:0.00} / {z.Soaked:0.00} "
                + $"| {z.Armor - z.Soaked:0.00} "
                + $"| {z.UtsuOpen:0.0} / {z.UtsuMax:0.0} / {z.UtsuLast:0.0} "
                + $"| {z.UkeOpen:0.0} / {z.UkeLast:0.0} | {z.Turns:0.0} |");
        }
        Console.WriteLine();

        if (hasUke)
        {
            Console.WriteLine("#### 横取りの相手（量/戦・全波の合計）");
            Console.WriteLine();
            Console.WriteLine("**なまりは「守られた駒」に乗る**ので、ここに出るのは");
            Console.WriteLine("「ウケの隣にいる駒」ではなく「ウケの隣で**殴られた**駒」。");
            Console.WriteLine();
            Console.WriteLine("| 取られた相手 | 量/戦（5波合計） |");
            Console.WriteLine("|---|--:|");
            foreach (var kv in fromAll.OrderByDescending(k => k.Value))
                Console.WriteLine($"| {kv.Key} | {kv.Value:0.00} |");
            Console.WriteLine();
        }

        // ---- 陽性対照 ----------------------------------------------------------------------
        Console.WriteLine("### 2. 陽性対照 `BearRule(0)`（横取りはするが変換しない）");
        Console.WriteLine();
        Console.WriteLine("`ArmorPerDull = 0` は**変換だけを止める**（横取りは走る）。");
        Console.WriteLine("横取り・素通りは既定と同じで、鎧が 0 になるはず。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 横取り | 素通り | 鎧(生成/吸) | ウケ攻(開/終) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(bf, duStages[w].Enemy, new BearRule(0));
            Console.WriteLine($"| 第{w + 1}波 | {z.Win:0.0}% | {z.Taken:0.00} | {z.Passed:0.00} "
                + $"| {z.Armor:0.00} / {z.Soaked:0.00} | {z.UkeOpen:0.0} / {z.UkeLast:0.0} | {z.Turns:0.0} |");
        }
        Console.WriteLine();

        // ---- 掃引 --------------------------------------------------------------------------
        Console.WriteLine("### 3. 掃引（`ArmorPerDull`）");
        Console.WriteLine();
        Console.WriteLine("**見るのは勝率ではなく、生成したアーマーのうち実際に吸われた割合**（`吸率`）。");
        Console.WriteLine("第41期の掃引は「比が全点で 2.30 で動かない＝ノブは強度しか変えず性質を変えない」を");
        Console.WriteLine("示した。全点で吸率が同じなら、`ArmorPerDull` も強度ノブでしかない。");
        Console.WriteLine();
        Console.WriteLine("| ArmorPerDull | 平均勝率 | 横取り/戦 | 鎧 生成/戦 | 鎧 吸/戦 | 死蔵/戦 | 吸率 | ウケ攻(開→終) |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int apd in new[] { 0, 1, 2, 3 })
        {
            double win = 0, tk = 0, ar = 0, so = 0, ko = 0, kl = 0;
            for (int w = 0; w < duStages.Count; w++)
            {
                var z = MeasureDull(bf, duStages[w].Enemy, new BearRule(apd));
                win += z.Win; tk += z.Taken; ar += z.Armor; so += z.Soaked; ko += z.UkeOpen; kl += z.UkeLast;
            }
            int nw = duStages.Count;
            Console.WriteLine($"| {apd} | {win / nw:0.0}% | {tk / nw:0.00} | {ar / nw:0.00} | {so / nw:0.00} "
                + $"| {(ar - so) / nw:0.00} | {(ar > 0.001 ? (so * 100.0 / ar).ToString("0.0") + "%" : "—")} "
                + $"| {ko / nw:0.0} → {kl / nw:0.0} |");
        }
        Console.WriteLine();

        // ---- 波ごとの勝率（崖の確認・受け入れ基準5）------------------------------------------
        Console.WriteLine("### 4. 波ごとの勝率（受け入れ基準5 ＝ 崖になっていないか）");
        Console.WriteLine();
        Console.WriteLine("| ArmorPerDull" + string.Concat(Enumerable.Range(1, duStages.Count).Select(i => $" | 第{i}波")) + " | 平均 |");
        Console.WriteLine("|--:" + string.Concat(duStages.Select(_ => "|--:")) + "|--:|");
        foreach (int apd in new[] { 0, 1, 2, 3 })
        {
            var cells = new List<double>();
            for (int w = 0; w < duStages.Count; w++)
            {
                int win = 0;
                for (int seed = 0; seed < DuSeeds; seed++)
                    if (BattleEngine.Run(bf, duStages[w].Enemy, seed, false, null, null, null, null,
                                         null, null, new BearRule(apd)).PlayerWon) win++;
                cells.Add(win * 100.0 / DuSeeds);
            }
            Console.WriteLine($"| {apd}" + string.Concat(cells.Select(c => $" | {c:0.0}%"))
                + $" | {cells.Average():0.0}% |");
        }
        Console.WriteLine();
    }

    // ---- ウケ抜きの対照（陽性対照その2）------------------------------------------------------
    Console.WriteLine("## 変種（`CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine("採用行の**1枚だけ**を差し替えた版を診断のローカルに組む");
    Console.WriteLine("（`gradient` / `aim` / `route` と同じ扱い）。");
    Console.WriteLine();

    Console.WriteLine("### A. ウケ抜き（4体）—— 陽性対照その2");
    Console.WriteLine();
    Console.WriteLine("**4体版が5体版と同じ値なら、その台は飽和していて測定になっていない**");
    Console.WriteLine("（第21期 `swap` の検査）。ここでは中央を空けたぶんの体の値段も混ざるので、");
    Console.WriteLine("**符号ではなく「動くかどうか」だけを読む**。");
    Console.WriteLine();

    Formation ukeRow = Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
        center: UnitCatalog.Uke, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    Formation ukeGone = Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
        back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);

    Console.WriteLine("| 版 | 平均勝率" + string.Concat(Enumerable.Range(1, duStages.Count).Select(i => $" | 第{i}波")) + " | なまり/戦 | 横取り/戦 |");
    Console.WriteLine("|---|--:" + string.Concat(duStages.Select(_ => "|--:")) + "|--:|--:|");
    foreach (var (vn, vf) in new[] { ("ウケあり（採用行）", ukeRow), ("ウケ抜き（4体・中央 空）", ukeGone) })
    {
        var cells = new List<double>();
        double sh = 0, tk = 0;
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(vf, duStages[w].Enemy, BearRule.Default);
            cells.Add(z.Win); sh += z.Route[(int)DullRoute.Sharer]; tk += z.Taken;
        }
        Console.WriteLine($"| {vn} | {cells.Average():0.0}%" + string.Concat(cells.Select(c => $" | {c:0.0}%"))
            + $" | {sh:0.00} | {tk:0.00} |");
    }
    Console.WriteLine();

    // ---- 排他（受け入れ基準7）----------------------------------------------------------------
    Console.WriteLine("### B. 横取りの排他（受け入れ基準7）");
    Console.WriteLine();
    Console.WriteLine("**ウケの席は固定（前1）で、ウツの席だけを動かす。** 前1の隣接は");
    Console.WriteLine("`{中央, 後1}` なので、ウツを中央に置けば隣接・前3に置けば非隣接になる");
    Console.WriteLine("（角どうし＝前1と前3は隣接していない。`AdjacencyTable` 参照）。");
    Console.WriteLine("**動く変数はウツとガルドの入れ替え1つだけ。**");
    Console.WriteLine();
    Console.WriteLine("ウツの攻撃力の到達点が隣接版で下がれば、**同じ供給を2枚の読み手が配置で分け合う**が実体を持つ。");
    Console.WriteLine();

    Formation adj = Formation.Build(front1: UnitCatalog.Uke, front3: UnitCatalog.Gald,
        center: UnitCatalog.Utsu, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    Formation far = Formation.Build(front1: UnitCatalog.Uke, front3: UnitCatalog.Utsu,
        center: UnitCatalog.Gald, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);

    Console.WriteLine("| 版 | 平均勝率 | なまり/戦 | 横取り/戦 | 素通り/戦 | 鎧(生成/吸) | ウツ攻(開/最大/終) | ウケ攻(開/終) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var (vn, vf) in new[] { ("隣接（ウケ前1 / ウツ中央）", adj), ("非隣接（ウケ前1 / ウツ前3）", far) })
    {
        double win = 0, sh = 0, tk = 0, ps = 0, ar = 0, so = 0, uo = 0, um = 0, ul = 0, ko = 0, kl = 0;
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(vf, duStages[w].Enemy, BearRule.Default);
            win += z.Win; sh += z.Route[(int)DullRoute.Sharer]; tk += z.Taken; ps += z.Passed;
            ar += z.Armor; so += z.Soaked; uo += z.UtsuOpen; um += z.UtsuMax; ul += z.UtsuLast;
            ko += z.UkeOpen; kl += z.UkeLast;
        }
        int nw = duStages.Count;
        Console.WriteLine($"| {vn} | {win / nw:0.0}% | {sh:0.00} | {tk:0.00} | {ps:0.00} "
            + $"| {ar:0.00} / {so:0.00} | {uo / nw:0.0} / {um / nw:0.0} / {ul / nw:0.0} "
            + $"| {ko / nw:0.0} / {kl / nw:0.0} |");
    }
    Console.WriteLine();

    // ---- 移り木との同居（§6-6）---------------------------------------------------------------
    Console.WriteLine("### C. 移り木（シオ）との同居 —— 同じ窓口の逆向き");
    Console.WriteLine();
    Console.WriteLine("第41期の実測では、シオの `+5`（動かされた味方を強化）が突き返しの `−2` を");
    Console.WriteLine("打ち消してウツの `AtkBonus` を正へ振り、逆しまの半減側に落とした。");
    Console.WriteLine("**`Dull` の窓口ができたことで、この干渉は「同じ窓口の逆向き」として初めて計測できる**");
    Console.WriteLine("——強化はまだ窓口を持たないので、読めるのは弱体の側の量だけ。");
    Console.WriteLine();
    Console.WriteLine("台（分かち×逆しま）のノノをシオに差し替える。移動の供給は第五波の曝き（告発人）。");
    Console.WriteLine();

    Formation shioOff = Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald,
        center: UnitCatalog.Nono, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    Formation shioOn = Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald,
        center: UnitCatalog.Shio, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);

    Console.WriteLine("| 版 | 波 | 勝率 | なまり/戦 | ウツ攻(開/最大/終) |");
    Console.WriteLine("|---|---|--:|--:|--:|");
    foreach (var (vn, vf) in new[] { ("シオなし（台）", shioOff), ("シオあり（ノノ→シオ）", shioOn) })
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(vf, duStages[w].Enemy, BearRule.Default);
            Console.WriteLine($"| {vn} | 第{w + 1}波 | {z.Win:0.0}% | {z.Route[(int)DullRoute.Sharer]:0.00} "
                + $"| {z.UtsuOpen:0.0} / {z.UtsuMax:0.0} / {z.UtsuLast:0.0} |");
        }
    Console.WriteLine();
    return;
}
}
