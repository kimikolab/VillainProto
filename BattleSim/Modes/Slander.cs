using BattleCore;
using static Common;

// =====================================================================================
// slander モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "slander")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 slander
// =====================================================================================

static class SlanderDiag
{
// 誹り（第44期）を測る。**敵から味方へ弱体を撒く初めての経路**で、窓口は第42期の
// BattleContext.Dull。撒いた量そのものではなく、**そのうち何割が読み手（ウケ＝アーマー化 /
// ワタ＝敵へ転嫁）に届いたか**で読む——第42期が「生成したアーマー」ではなく「実際に吸った量」で、
// 第43期が「流した量」ではなく「効き」で判断したのと同じ理由。**撒いた量は成果ではない。**
//
// **「早逝」が必須。** 保持者は前列（第二波・前3）なので早く落ちる。供給の総量は
// 保持者の生存ターン数で決まるので、これを見ないと「弱すぎる」と「早く死んでいる」が切り分かない。
//
// **陽性対照は SlanderRule(0)。** 誹りが 0.00 になること自体が計数の検算で、
// 同時にその行の「差し替え前の値」（＝Phase 0 の基準）になる。
public static void Run(string[] args, int stageIndex)
{
    var slBuilds = CompareBuilds();
    const int SlSeeds = 200;   // compare / spread / shove / dull / relay と同じ。balance.md と突き合わせる
    const int SlWave = 1;      // 第二波（0 起算）。誹りの保持者はここにしかいない

    string slFilter = args.Length > 2 ? args[2] : "";
    var slTargets = slBuilds
        .Where(b => slFilter.Length == 0 || slFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // **波は診断のローカルに組む**（gradient / aim / guard と同じ扱い）。
    // 誹りは測って採用しなかったので `Stages` には載っていない——載せ替えずに測れるようにしてある。
    //
    // 差し替え版（前3 = Slanderer）と、差し替え前（前3 = KnightG）。
    // **2体は数値・型・速さが1つも違わない**ので、差分は誹り1つに閉じる。
    Formation SlSlanderWave2() => Formation.Build(
        front1: EnemyCatalog.KnightG, front3: EnemyCatalog.Slanderer, center: EnemyCatalog.Husher,
        back1: EnemyCatalog.Almoner, back3: EnemyCatalog.ArcherG);

    Formation SlPlainWave2() => Formation.Build(
        front1: EnemyCatalog.KnightG, front3: EnemyCatalog.KnightG, center: EnemyCatalog.Husher,
        back1: EnemyCatalog.Almoner, back3: EnemyCatalog.ArcherG);

    Formation slWave2 = SlSlanderWave2();

    // 弱体の読み手（ウツ＝逆しま / ウケ＝集約 / ワタ＝渡し）を1枚でも含む行か。
    static bool SlHasReader(Formation f) => f.Occupied().Any(o =>
        o.Def.Traits.Contains(TraitId.Perverse) || o.Def.Traits.Contains(TraitId.Bear)
        || o.Def.Traits.Contains(TraitId.Relay));

    int slRouteIdx = (int)DullRoute.Slander;

    (double Win, double Fired, double Total, double Taken, double Passed, double Zeroed,
     double Death, double Died, double Turns, double Foe, double Swings,
     Dictionary<string, double> To, Dictionary<string, double> Zero, Dictionary<string, double> RelayTo)
    MeasureSlander(Formation f, Formation enemy, SlanderRule rule, int seed0 = 0, int seedN = SlSeeds)
    {
        var to = new Dictionary<string, double>();
        var zero = new Dictionary<string, double>();
        var rto = new Dictionary<string, double>();
        double win = 0, fired = 0, total = 0, taken = 0, passed = 0, zeroed = 0;
        double death = 0, died = 0, turns = 0, foe = 0, swings = 0;

        for (int seed = seed0; seed < seed0 + seedN; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                                    null, null, null, null, null, null, null, null, rule);
            if (r.PlayerWon) win++;
            fired += r.SlanderFired; total += r.SlanderTotal;
            taken += r.DullTakenByRoute[slRouteIdx];
            passed += r.DullByRoute[slRouteIdx] - r.DullTakenByRoute[slRouteIdx];
            zeroed += r.DullZeroed; turns += r.Turns;

            foreach (var kv in r.SlanderTo)
                to[kv.Key] = to.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in r.DullZeroedWho)
                zero[kv.Key] = zero.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in r.RelayTo)
                rto[kv.Key] = rto.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

            // 敵が味方に与えたダメージと、敵が振った回数（第二波の攻撃回数＝誹りの発火の母数）
            foreach ((int _, UnitDef d) in enemy.Occupied())
                if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t))
                { foe += t.DamageToEnemy; swings += t.Attacks; }

            // 早逝: 誹りの保持者が倒れた試行の、倒れたターン（UnitTally.LastActiveTurn は
            // 死亡時にその手番のターンで上書きされる）。倒れなかった試行は分母に入れない。
            if (r.TallyByUnit.TryGetValue(EnemyCatalog.Slanderer.Id, out UnitTally? st) && st.Deaths > 0)
            { died++; death += st.LastActiveTurn; }
        }

        double n = seedN, md = Math.Max(1, died);
        foreach (string k in to.Keys.ToList()) to[k] /= n;
        foreach (string k in zero.Keys.ToList()) zero[k] /= n;
        foreach (string k in rto.Keys.ToList()) rto[k] /= n;
        return (win * 100 / n, fired / n, total / n, taken / n, passed / n, zeroed / n,
                death / md, died / n, turns / n, foe / n, swings / n, to, zero, rto);
    }

    // 逆しま持ちの CurrentAttack（StatSnapshot。ターン頭の値）。**verbose が要るので別立て**。
    (double Open, double Max, double Last, double Seen) MeasureUtsu(Formation f, Formation enemy, SlanderRule rule)
    {
        int readerId = -1;
        for (int i = 0, k = 0; i < FormationRules.PlayableSlotCount; i++)
            if (f[i] is { } d) { if (d.Traits.Contains(TraitId.Perverse)) readerId = k; k++; }
        if (readerId < 0) return (0, 0, 0, 0);

        double open = 0, max = 0, last = 0, seen = 0;
        for (int seed = 0; seed < SlSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: true,
                                    null, null, null, null, null, null, null, null, rule);
            var snaps = r.Events
                .Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == readerId)
                .Select(e => e.Amount).ToList();
            if (snaps.Count > 0) { open += snaps[0]; max += snaps.Max(); last += snaps[^1]; seen++; }
        }
        double m = Math.Max(1, seen);
        return (open / m, max / m, last / m, seen);
    }

    Console.WriteLine("# 誹り（slander）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 slander [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{SlSeeds - 1}。数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`CompareBuilds()` / `Stages` / `Columns` は触っていない。");
    Console.WriteLine("**測るのは第二波だけ**——誹りの保持者（誹りの巡礼騎士・前3）はそこにしかいない。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 誹り | 発火回数/戦（`SlanderFired`）と撒いた総量/戦（`SlanderTotal`） |");
    Console.WriteLine("| 横取り | ウケ／ワタが横取りした量/戦（`DullTakenByRoute[Slander]`）＝**敵の供給が資産に変わった量** |");
    Console.WriteLine("| 素通り | 読み手に届かずただの損になった量/戦（`DullByRoute[Slander]` − 横取り） |");
    Console.WriteLine("| 攻ゼロ | 味方の `CurrentAttack` が 0 になった回数/戦（**崖の検算**・全経路） |");
    Console.WriteLine("| 早逝 | 誹りの保持者が倒れたターン（倒れた試行の平均）と、倒れた試行の割合 |");
    Console.WriteLine("| 敵振り | 敵5体が攻撃を振った回数/戦（`UnitTally.Attacks` の和）＝**誹りの発火の母数** |");
    Console.WriteLine();
    Console.WriteLine("**撒いた量は成果ではない。** 採否は「横取り ÷（横取り + 素通り）」で読む。");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）--------------------------------------------------------
    Console.WriteLine("## 0. 検算 —— 差分は規則だけに閉じているか");
    Console.WriteLine();
    {
        Formation plain = SlPlainWave2();
        int cells = 0, diffA = 0, diffB = 0;
        foreach (var b in slBuilds)
        {
            int a = 0, c = 0, d = 0;
            for (int seed = 0; seed < SlSeeds; seed++)
            {
                // A: 誹りの保持者を置いた波 × SlanderRule(0)
                if (BattleEngine.Run(b.F, slWave2, seed, false,
                        null, null, null, null, null, null, null, null, new SlanderRule(0)).PlayerWon) a++;
                // B: 前3 を KnightG に戻した波 ×
                //    SlanderRule(0)（＝出荷している Stages[1] そのもの）
                if (BattleEngine.Run(b.F, EnemyCatalog.Stages[SlWave].Enemy, seed, false,
                        null, null, null, null, null, null, null, null, new SlanderRule(0)).PlayerWon) c++;
                // C: 前3 を KnightG に戻した波 × SlanderRule(3)（規則を有効にしても保持者がいない）
                if (BattleEngine.Run(b.F, plain, seed, false,
                        null, null, null, null, null, null, null, null, new SlanderRule(3)).PlayerWon) d++;
            }
            cells++;
            if (a != c) diffA++;
            if (c != d) diffB++;
        }
        Console.WriteLine($"- **基準1**（保持者を置いた波 × `SlanderRule(0)` ⇔ 出荷している `Stages[1]`）: "
            + $"**{cells} セル中 {diffA} 件の食い違い**（{cells} 行 × 第二波）");
        Console.WriteLine($"- **基準2**（`KnightG` に戻すと規則を有効にしても元へ戻る）: **{cells} セル中 {diffB} 件の食い違い**");
        Console.WriteLine();
        Console.Out.Flush();
    }

    int[] slPens = { 0, 1, 2, 3 };
    const int SlMain = 2;   // 主表に使う Penalty

    // --- 1. 陽性対照と主表 ------------------------------------------------------------------
    Console.WriteLine($"## 1. 全 {slBuilds.Length} 行 × 第二波（陽性対照 `SlanderRule(0)` と 主表 `Penalty = {SlMain}`）");
    Console.WriteLine();
    Console.WriteLine("`読` = 弱体の読み手（ウツ／ウケ／ワタ）を持つ行。");
    Console.WriteLine();
    Console.WriteLine($"| 行 | 読 | 勝率(P0) | 勝率(P{SlMain}) | 差 | 誹り | 総量 | 横取り | 素通り | 攻ゼロ(P0→P{SlMain}) | 早逝(T/率) | 敵振り | 決着T(P0→P{SlMain}) |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

    var slAggTo = new Dictionary<string, double>();
    var slAggZero = new Dictionary<string, double>();
    var slAggZero0 = new Dictionary<string, double>();
    double slPassReader = 0, slPassPlain = 0; int slNReader = 0, slNPlain = 0;
    double slDeltaReader = 0, slDeltaPlain = 0;

    foreach (var b in slBuilds)
    {
        var z0 = MeasureSlander(b.F, slWave2, new SlanderRule(0));
        var z = MeasureSlander(b.F, slWave2, new SlanderRule(SlMain));
        bool reader = SlHasReader(b.F);

        foreach (var kv in z.To)
            slAggTo[kv.Key] = slAggTo.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
        foreach (var kv in z.Zero)
            slAggZero[kv.Key] = slAggZero.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

        foreach (var kv in z0.Zero)
            slAggZero0[kv.Key] = slAggZero0.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

        if (reader) { slPassReader += z.Passed; slNReader++; slDeltaReader += z.Win - z0.Win; }
        else { slPassPlain += z.Passed; slNPlain++; slDeltaPlain += z.Win - z0.Win; }

        Console.WriteLine($"| {b.Name} | {(reader ? "○" : "")} | {z0.Win:0.0}% | {z.Win:0.0}% "
            + $"| {z.Win - z0.Win:+0.0;-0.0;±0.0} | {z.Fired:0.00} | {z.Total:0.0} | {z.Taken:0.00} "
            + $"| {z.Passed:0.00} | {z0.Zeroed:0.00} → {z.Zeroed:0.00} | {z.Death:0.0} / {z.Died * 100:0.0}% "
            + $"| {z0.Swings:0.0} | {z0.Turns:0.0} → {z.Turns:0.0} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine($"読み手あり **{slNReader} 行**: 平均 {slDeltaReader / Math.Max(1, slNReader):+0.00;-0.00;±0.00} pt"
        + $" / 素通り {slPassReader / Math.Max(1, slNReader):0.00} per 戦");
    Console.WriteLine($"読み手なし **{slNPlain} 行**: 平均 {slDeltaPlain / Math.Max(1, slNPlain):+0.00;-0.00;±0.00} pt"
        + $" / **目減り（素通り）{slPassPlain / Math.Max(1, slNPlain):0.00} per 戦**");
    Console.WriteLine();

    // --- 2. 誰が誹られたか ------------------------------------------------------------------
    Console.WriteLine($"## 2. 誰が誹られたか（`Penalty = {SlMain}` ・全 {slBuilds.Length} 行の合計 / 戦）");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 量/戦（全行合計） |");
    Console.WriteLine("|---|--:|");
    foreach (var kv in slAggTo.OrderByDescending(k => k.Value).Take(12))
        Console.WriteLine($"| {kv.Key} | {kv.Value:0.00} |");
    Console.WriteLine();

    Console.WriteLine("### 攻ゼロの内訳（崖の検算・受け入れ基準4）");
    Console.WriteLine();
    Console.WriteLine("**帰属は P0 との差で取る**——`攻ゼロ` は窓口を通る全経路を数えるので、");
    Console.WriteLine("呪詛・萎縮・なまりのぶんが P0 の側に既に載っている。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 素の攻 | P0 | P" + SlMain + " | 差（＝誹りに帰属） |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    if (slAggZero.Count == 0) Console.WriteLine("| （0 件） | — | 0.00 | 0.00 | ±0.00 |");
    foreach (var kv in slAggZero.OrderByDescending(k => k.Value - (slAggZero0.TryGetValue(k.Key, out double q) ? q : 0)).Take(15))
    {
        var def = UnitCatalog.All.FirstOrDefault(u => u.Name == kv.Key);
        double b0 = slAggZero0.TryGetValue(kv.Key, out double p) ? p : 0;
        Console.WriteLine($"| {kv.Key} | {(def is null ? "—" : def.Attack.ToString())} "
            + $"| {b0:0.00} | {kv.Value:0.00} | {kv.Value - b0:+0.00;-0.00;±0.00} |");
    }
    Console.WriteLine();

    // --- 3. 読み手を持つ行の詳細（ウツ攻 / ワタの流し先）-------------------------------------
    Console.WriteLine("## 3. 弱体の読み手を持つ行の詳細");
    Console.WriteLine();
    Console.WriteLine("`ウツ攻` は逆しま持ちの `CurrentAttack`（開戦時 / 最大 / 最終T）。`StatSnapshot` から。");
    Console.WriteLine("**開戦時 < 最大 が「敵の供給が読み手に届いた」の直接の証拠。**");
    Console.WriteLine();
    Console.WriteLine($"| 行 | 勝率(P0→P{SlMain}) | 誹り | 横取り | 素通り | 横取り率 | ウツ攻(P0) | ウツ攻(P{SlMain}) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    var slRelayNotes = new List<string>();
    foreach (var b in slBuilds.Where(x => SlHasReader(x.F)))
    {
        var z0 = MeasureSlander(b.F, slWave2, new SlanderRule(0));
        var z = MeasureSlander(b.F, slWave2, new SlanderRule(SlMain));
        var u0 = MeasureUtsu(b.F, slWave2, new SlanderRule(0));
        var u1 = MeasureUtsu(b.F, slWave2, new SlanderRule(SlMain));
        double got = z.Taken + z.Passed;
        Console.WriteLine($"| {b.Name} | {z0.Win:0.0}% → {z.Win:0.0}% | {z.Fired:0.00} | {z.Taken:0.00} "
            + $"| {z.Passed:0.00} | {(got > 0 ? $"{z.Taken * 100 / got:0.0}%" : "—")} "
            + $"| {(u0.Seen > 0 ? $"{u0.Open:0.0} / {u0.Max:0.0} / {u0.Last:0.0}" : "—")} "
            + $"| {(u1.Seen > 0 ? $"{u1.Open:0.0} / {u1.Max:0.0} / {u1.Last:0.0}" : "—")} |");
        Console.Out.Flush();

        if (z.RelayTo.Count > 0 || z0.RelayTo.Count > 0)
        {
            slRelayNotes.Add($"- **{b.Name}** P0 の流し先: "
                + (z0.RelayTo.Count == 0 ? "（なし）" : string.Join(" / ", z0.RelayTo.OrderByDescending(k => k.Value)
                    .Select(k => $"{k.Key} {k.Value:0.00}"))));
            slRelayNotes.Add($"- **{b.Name}** P{SlMain} の流し先: "
                + (z.RelayTo.Count == 0 ? "（なし）" : string.Join(" / ", z.RelayTo.OrderByDescending(k => k.Value)
                    .Select(k => $"{k.Key} {k.Value:0.00}"))));
        }
    }
    Console.WriteLine();
    if (slRelayNotes.Count > 0)
    {
        Console.WriteLine("### ワタの流し先（**撒いた呪いが本人に返るか**）");
        Console.WriteLine();
        Console.WriteLine("第二波の最高攻撃力は `KnightG` / `Slanderer` の 24 なので、");
        Console.WriteLine("**誹りの保持者自身に返る可能性がある**（設計としては望ましい）。");
        Console.WriteLine();
        foreach (string note in slRelayNotes) Console.WriteLine(note);
        Console.WriteLine();
    }

    // --- 4. 掃引 ----------------------------------------------------------------------------
    Console.WriteLine("## 4. 掃引（`Penalty` 0 / 1 / 2 / 3）");
    Console.WriteLine();
    Console.WriteLine("**見るのは勝率ではなく「横取り／素通り」の比と、第二波の情報セル。**");
    Console.WriteLine("第41期の掃引は「比が全点で 2.30 で動かない＝ノブは強度しか変えず性質を変えない」を示した。");
    Console.WriteLine("同じことが起きるかを先に疑う。");
    Console.WriteLine();
    Console.WriteLine("| Penalty | 平均(全行) | 主判定17行 | 情報セル | 100%行 | 0%行 | 誹り | 総量 | 横取り | 素通り | 横取り率 | 攻ゼロ |");
    Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

    // 主判定17行（design/HANDOFF_NEXT_SESSION.md §4-4）。行集合を固定した歯止めの分母。
    string[] slMainKeys =
    {
        "隊列崩し (バサ×ヨミ×セロ)", "燃焼 (ボルグ×ホタ)", "縛め収入型 (クグ×バン×ガン)",
        "仇討ち×砕け (ヒビ×ザン)", "刻み×抉り (ノミ×エグ)", "裂き (キリ×エグ)",
        "耐久 (ガルド×リリ)", "溜め改 (クグ×バン×ガン)", "逆しま (ネル×ウツ)",
        "追撃×据え (ハギ×バン)", "継ぎ当て×分散回復", "毒+耐久 (ベニ×トウ)",
        "速攻 (ボルグ×ムド)", "反撃改2 (ガン×カド)", "惨禍×死の連鎖",
        "後衛特化+後備え", "突き出し (セロ×ヨミ)"
    };

    var slSweep = new Dictionary<int, Dictionary<string, double>>();
    foreach (int pen in slPens)
    {
        var wins = new Dictionary<string, double>();
        double fired = 0, total = 0, taken = 0, passed = 0, zeroed = 0;
        foreach (var b in slBuilds)
        {
            var z = MeasureSlander(b.F, slWave2, new SlanderRule(pen));
            wins[b.Name] = z.Win;
            fired += z.Fired; total += z.Total; taken += z.Taken; passed += z.Passed; zeroed += z.Zeroed;
        }
        slSweep[pen] = wins;
        double n = slBuilds.Length;
        var col = wins.Values.ToList();
        double got = taken + passed;
        Console.WriteLine($"| {pen} | {col.Average():0.00} "
            + $"| {slMainKeys.Average(k => wins[k]):0.00} "
            + $"| {col.Count(x => x > 0 && x < 100)} | {col.Count(x => x >= 100)} | {col.Count(x => x <= 0)} "
            + $"| {fired / n:0.00} | {total / n:0.0} | {taken / n:0.00} | {passed / n:0.00} "
            + $"| {(got > 0 ? $"{taken * 100 / got:0.0}%" : "—")} | {zeroed / n:0.00} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine("**歯止め**: 主判定17行の第二波は `Penalty = 0` の値から 10pt 以上落ちてはいけない。");
    Console.WriteLine();

    // --- 5. 符号反転（受け入れ基準6）--------------------------------------------------------
    Console.WriteLine("## 5. 符号反転（受け入れ基準6・**この期の主眼**）");
    Console.WriteLine();
    Console.WriteLine($"`Penalty = {SlMain}` で第二波の勝率が動いた行。**上がる行と下がる行が両方あること**が条件。");
    Console.WriteLine();
    Console.WriteLine($"| 行 | 読 | P0 | P{SlMain} | 差 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|");
    var slMoved = slBuilds
        .Select(b => (b.Name, Reader: SlHasReader(b.F), A: slSweep[0][b.Name], B: slSweep[SlMain][b.Name]))
        .Where(x => Math.Abs(x.B - x.A) > 0.001)
        .OrderByDescending(x => x.B - x.A).ToList();
    foreach (var x in slMoved)
        Console.WriteLine($"| {x.Name} | {(x.Reader ? "○" : "")} | {x.A:0.0}% | {x.B:0.0}% | {x.B - x.A:+0.0;-0.0;±0.0} |");
    Console.WriteLine();
    Console.WriteLine($"上がった行 **{slMoved.Count(x => x.B > x.A)}** / 下がった行 **{slMoved.Count(x => x.B < x.A)}**"
        + $" / 動かなかった行 **{slBuilds.Length - slMoved.Count}**。");
    Console.WriteLine();


    // --- 6. 別 seed での追試（confirm）------------------------------------------------------
    // **選定に使っていない seed に当てて採否を決める**（CLAUDE.md の reseat → confirm と同じ作法）。
    // 対象は「読み手を持つ7行」と「主表で 1.0pt 以上動いた行」。+1.0pt が 200 試行のうち2件でしかない
    // 以上、seed 帯を変えても符号が保つかを見ないと符号反転を主張できない。
    Console.WriteLine("## 6. 別 seed での追試（seed 200..599 の 400 試行）");
    Console.WriteLine();
    Console.WriteLine("**選定に使っていない seed 帯**。主表（seed 0..199）で1セルでも動いた行と、");
    Console.WriteLine("読み手を持つ7行が対象。**符号が保つかどうかだけを見る。**");
    Console.WriteLine();
    Console.WriteLine($"| 行 | 読 | 主表 P0→P{SlMain} | 主表 差 | 追試 P0→P{SlMain} | 追試 差 | 符号 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|:-:|");
    foreach (var b in slBuilds)
    {
        bool reader = SlHasReader(b.F);
        double a = slSweep[0][b.Name], c = slSweep[SlMain][b.Name];
        if (!reader && Math.Abs(c - a) < 0.001) continue;
        var q0 = MeasureSlander(b.F, slWave2, new SlanderRule(0), 200, 400);
        var q1 = MeasureSlander(b.F, slWave2, new SlanderRule(SlMain), 200, 400);
        double d1 = c - a, d2 = q1.Win - q0.Win;
        string sign = Math.Abs(d1) < 0.001 && Math.Abs(d2) < 0.001 ? "—"
            : (d1 > 0) == (d2 > 0) && Math.Abs(d2) > 0.001 ? "保つ" : "**割れる**";
        Console.WriteLine($"| {b.Name} | {(reader ? "○" : "")} | {a:0.0}% → {c:0.0}% | {d1:+0.0;-0.0;±0.0} "
            + $"| {q0.Win:0.0}% → {q1.Win:0.0}% | {d2:+0.0;-0.0;±0.0} | {sign} |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    if (slTargets.Length > 0 && slFilter.Length > 0)
    {
        Console.WriteLine($"## 7. 絞り込み `{slFilter}` の掃引（第二波）");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", slPens.Select(p => $"P{p}")) + " |");
        Console.WriteLine("|---|" + string.Join("|", slPens.Select(_ => "--:")) + "|");
        foreach (var b in slTargets)
            Console.WriteLine($"| {b.Name} | " + string.Join(" | ", slPens.Select(p => $"{slSweep[p][b.Name]:0.0}%")) + " |");
        Console.WriteLine();
    }
    return;
}
}
