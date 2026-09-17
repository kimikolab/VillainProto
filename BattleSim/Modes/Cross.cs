using BattleCore;
using static Common;

// =====================================================================================
// cross モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "cross")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 cross
// =====================================================================================

static class CrossDiag
{
// =====================================================================================
// cross モード（第92期）—— 交差帯を作る（軸をまたぐ機構を測れる台を並置する）
//
// **`CompareBuilds()` / `Stages` / `Columns` / `Baseline` を1文字も触らない。**
// 交差帯は `CrossBuilds()` という別の入口で、生成物も `docs/crossing.md` と別にする。
//
//     dotnet run --project BattleSim -c Release 0 cross phase0    # §1-1〜§1-3（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 cross quality   # §1-4・§3（> docs/crossing.md）
//     dotnet run --project BattleSim -c Release 0 cross redo <p>  # §4（p = 84 / 86）
//     dotnet run --project BattleSim -c Release 0 cross check     # 自己検査
// =====================================================================================
public static void Run(string[] args, int stageIndex)
{
    string focusId = args.Length > 1 ? args[1] : "";

    string xcArg = args.Length > 2 ? args[2] : "";
    var xcSw = System.Diagnostics.Stopwatch.StartNew();
    IReadOnlyList<EnemyCatalog.Stage> xcStages = EnemyCatalog.Stages;
    int xcW = xcStages.Count;
    var xcRoster = UnitCatalog.All.ToArray();
    int xcRN = xcRoster.Length;
    int xcNK = UnitTally.CarryKeys.Length;          // 11
    const int XcSeeds = 200;                        // **`compare` と同じ**（自己検査 (g)）

    var xcIdx = new Dictionary<string, int>();
    for (int u = 0; u < xcRN; u++) xcIdx[xcRoster[u].Id] = u;

    // ---- 軸の書き手（§1-1）--------------------------------------------------------------------
    // 定義: **その駒の特性が、その通貨を盤面に置く。**`TraitEntryMap.Supplies` をそのまま引き、
    // 洗い直しで出た3件だけを手で直す（**元の表は1文字も触らない**——第78期以降の派生値が動く）。
    var xcSupAdd = new Dictionary<TraitId, int[]>
    {
        [TraitId.Contagion] = new[] { UnitTally.CarryPoison },
        [TraitId.Amplifier] = new[] { UnitTally.CarryPoison },
    };
    var xcSupDrop = new Dictionary<TraitId, int[]>
    {
        [TraitId.Guardian] = new[] { UnitTally.CarryWound },
    };
    HashSet<int> XcWriterKeys(UnitDef d)
    {
        var s = new HashSet<int>();
        foreach (TraitId t in d.Traits)
        {
            if (TraitEntryMap.Supplies.TryGetValue(t, out var sup)) foreach (var x in sup) s.Add(x.Key);
            if (xcSupAdd.TryGetValue(t, out int[]? ad)) foreach (int k in ad) s.Add(k);
        }
        foreach (TraitId t in d.Traits)
            if (xcSupDrop.TryGetValue(t, out int[]? dr)) foreach (int k in dr) s.Remove(k);
        return s;
    }
    HashSet<int> XcReaderKeys(UnitDef d)
    {
        var s = new HashSet<int>();
        foreach (TraitId t in d.Traits)
            if (TraitEntryMap.Reads.TryGetValue(t, out var rd)) foreach (var x in rd) s.Add(x.Key);
        return s;
    }
    var xcWriterOf = xcRoster.ToDictionary(d => d.Id, XcWriterKeys);
    var xcReaderOf = xcRoster.ToDictionary(d => d.Id, XcReaderKeys);
    HashSet<int> XcRowAxes(Formation f)
    {
        var s = new HashSet<int> { UnitTally.CarryHit };   // 被弾は敵が供給するので全行が持つ
        foreach ((_, UnitDef d) in f.Occupied())
            if (xcWriterOf.TryGetValue(d.Id, out var w)) foreach (int k in w) s.Add(k);
        return s;
    }
    string XcK(int k) => UnitTally.CarryKeys[k];

    // ---- 機構が実在する交差（§1-2。**導出する。指示書の一覧を信用しない**）---------------------
    var xcBridge = new Dictionary<(int, int), List<string>>();
    void XcAddBridge(int a, int b, string what)
    {
        if (a == b) return;
        var key = a < b ? (a, b) : (b, a);
        if (!xcBridge.TryGetValue(key, out var l)) xcBridge[key] = l = new List<string>();
        if (!l.Contains(what)) l.Add(what);
    }
    foreach (TraitId t in Enum.GetValues<TraitId>())
    {
        if (!TraitEntryMap.Reads.TryGetValue(t, out var rd)) continue;
        var sup = new List<int>();
        if (TraitEntryMap.Supplies.TryGetValue(t, out var sp)) sup.AddRange(sp.Select(x => x.Key));
        if (xcSupAdd.TryGetValue(t, out int[]? ad2)) sup.AddRange(ad2);
        foreach (int r in rd.Select(x => x.Key).Distinct())
            foreach (int s2 in sup.Distinct())
                XcAddBridge(r, s2, $"`{t}`");
    }
    // engine の盤面規則（**駒の特性には載らない**ので手で足す）
    var xcEngineBridge = new (int A, int B, string Name)[]
    {
        (UnitTally.CarryWound, UnitTally.CarryPoison, "滲み則 `SoakRule.Poison`（採用済み）"),
        (UnitTally.CarryHit,   UnitTally.CarryWound,  "巻き込み則 `SpillWoundRule`（採用済み）"),
        (UnitTally.CarryHit,   UnitTally.CarryWound,  "引き取り `GatherRule`（採用済み）"),
        (UnitTally.CarryHit,   UnitTally.CarryWound,  "棘の傷 `ThornRule`（**残置**・第84期）"),
    };
    foreach (var e in xcEngineBridge) XcAddBridge(e.A, e.B, e.Name);

    if (xcArg == "phase0")
    {
        var xcRows0 = CompareBuilds();
        var xcRowAx = xcRows0.Select(r => XcRowAxes(r.F)).ToArray();

        Console.WriteLine("# 第92期 Phase 0 —— 交差の空白表（**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine($"軸は `TraitKeyMap` の {xcNK} キー（**新しい軸の定義を作らない**）。"
                          + $"55 組について `CompareBuilds()` {xcRows0.Length} 行に何行あるかを数える。");
        Console.WriteLine();

        Console.WriteLine("## 0-1. 軸の書き手（`TraitEntryMap.Supplies` の洗い直し）");
        Console.WriteLine();
        Console.WriteLine("**定義**: その駒の特性が、その通貨を**盤面に置く**。`Supplies` をそのまま引き、"
                          + "洗い直しで出た3件だけを手で直した（**元の表は1文字も触っていない**）。");
        Console.WriteLine();
        Console.WriteLine("| 直し | 特性 | 駒 | キー | 根拠 |");
        Console.WriteLine("|---|---|---|---|---|");
        Console.WriteLine("| **足す** | `Contagion` | 疫みのラウ | 毒 | `ContagionTrait.OnAnyDeath` が敵全体へ毒を撒くのに `Supplies` に無い（`Reads` にだけある） |");
        Console.WriteLine("| **足す** | `Amplifier` | 澱みのミオ | 毒 | 増幅（上書き）と着火（第87期）が毒を置くのに `Supplies` に無い |");
        Console.WriteLine("| **外す** | `Guardian` | 廃棄聖騎士ガルド | 傷 | 引き取り（第90期 `GatherRule`）は**移すだけ**——盤面の総量を増やさない中継 |");
        Console.WriteLine();
        Console.WriteLine("**被弾（`CarryHit`）は書き手 0**（敵が供給・第82期）なので、**全行が持つ**として数える。");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 書き手 | 含む行 / 61 |");
        Console.WriteLine("|---|---|--:|");
        for (int k = 0; k < xcNK; k++)
        {
            var ws = xcRoster.Where(d => xcWriterOf[d.Id].Contains(k)).Select(d => d.Name).ToArray();
            int n = xcRowAx.Count(s => s.Contains(k));
            Console.WriteLine($"| {XcK(k)} | {(ws.Length == 0 ? "**0 枚**（敵が供給）" : string.Join("・", ws))} | {n} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表A —— 交差の空白表（55 組 × 61 行）");
        Console.WriteLine();
        Console.WriteLine("セルは**その組の両方の軸の書き手がいる行数**。`—` は対角。");
        Console.WriteLine();
        Console.Write("| |");
        for (int k = 0; k < xcNK; k++) Console.Write($" {XcK(k)} |");
        Console.WriteLine();
        Console.Write("|---|");
        for (int k = 0; k < xcNK; k++) Console.Write("--:|");
        Console.WriteLine();
        var xcPairRows = new int[xcNK, xcNK];
        for (int a = 0; a < xcNK; a++)
        {
            Console.Write($"| **{XcK(a)}** |");
            for (int b = 0; b < xcNK; b++)
            {
                if (a == b) { Console.Write(" — |"); continue; }
                int n = xcRowAx.Count(s => s.Contains(a) && s.Contains(b));
                xcPairRows[a, b] = n;
                Console.Write(n == 0 ? " **0** |" : $" {n} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine();
        var xcBlanks = new List<(int A, int B)>();
        for (int a = 0; a < xcNK; a++) for (int b = a + 1; b < xcNK; b++) if (xcPairRows[a, b] == 0) xcBlanks.Add((a, b));
        Console.WriteLine($"**空白（0 行）の組は 55 のうち {xcBlanks.Count}**（{xcBlanks.Count * 100.0 / 55:F1}%）。");
        Console.WriteLine();
        Console.WriteLine("| # | 空白の組 | 繋いでいる機構 |");
        Console.WriteLine("|--:|---|---|");
        for (int i = 0; i < xcBlanks.Count; i++)
        {
            var p = xcBlanks[i];
            string br = xcBridge.TryGetValue(p, out var l) ? string.Join(" / ", l) : "**無し**";
            Console.WriteLine($"| {i + 1} | {XcK(p.A)} × {XcK(p.B)} | {br} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表A-2 —— 「2軸以上」の内訳（第91期の 17 / 61 の再現）");
        Console.WriteLine();
        int[] xcSix = { UnitTally.CarryPoison, UnitTally.CarryBurn, UnitTally.CarryWound,
                        UnitTally.CarryMark, UnitTally.CarryStun, UnitTally.CarryArmor };
        var xcDeg = xcRowAx.Select(s => xcSix.Count(s.Contains)).ToArray();
        var xcPrim = new HashSet<string>(Baseline.PrimaryRows);
        Console.WriteLine("第91期は**6軸**（毒・燃焼・傷・標・痺れ・破片＝「撒く」通貨）で数えた。同じ6軸を、洗い直した書き手で数え直す。");
        Console.WriteLine();
        Console.WriteLine("| 含む軸の数 | 行数 / 61 | 割合 | うち主判定19行 |");
        Console.WriteLine("|--:|--:|--:|--:|");
        for (int d = 0; d <= xcSix.Length; d++)
        {
            int n = xcDeg.Count(x => x == d);
            if (n == 0 && d > 3) continue;
            int pn = Enumerable.Range(0, xcRows0.Length).Count(i => xcDeg[i] == d && xcPrim.Contains(xcRows0[i].Name));
            Console.WriteLine($"| {d} | {n} | {n * 100.0 / xcRows0.Length:F1}% | {pn} |");
        }
        int xcMulti = xcDeg.Count(x => x >= 2);
        Console.WriteLine();
        Console.WriteLine($"**2軸以上を含む行は {xcMulti} / {xcRows0.Length}（{xcMulti * 100.0 / xcRows0.Length:F1}%）。**");
        Console.WriteLine();
        Console.WriteLine("| 組 | 行数 | 行 |");
        Console.WriteLine("|---|--:|---|");
        var xcSixPairs = new List<(int A, int B, int N, string Rows)>();
        for (int i = 0; i < xcSix.Length; i++) for (int j = i + 1; j < xcSix.Length; j++)
        {
            var hit = Enumerable.Range(0, xcRows0.Length)
                .Where(r => xcRowAx[r].Contains(xcSix[i]) && xcRowAx[r].Contains(xcSix[j])).ToArray();
            xcSixPairs.Add((xcSix[i], xcSix[j], hit.Length, string.Join(" / ", hit.Select(r => xcRows0[r].Name))));
        }
        foreach (var p in xcSixPairs.OrderByDescending(p => p.N))
            Console.WriteLine($"| {XcK(p.A)} × {XcK(p.B)} | {(p.N == 0 ? "**0**" : p.N.ToString())} | {(p.N == 0 ? "—" : p.Rows)} |");
        Console.WriteLine();

        Console.WriteLine("## 0-2. 1体で交差を持つ駒（`KeysOf` が2キー以上）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | キー | 書き | 読み |");
        Console.WriteLine("|---|---|---|---|");
        int xcMultiKey = 0;
        foreach (UnitDef d in xcRoster)
        {
            int[] ks = TraitKeyMap.KeysOf(d);
            if (ks.Length < 2) continue;
            xcMultiKey++;
            Console.WriteLine($"| {d.Name} | {string.Join("・", ks.Select(XcK))} "
                + $"| {(xcWriterOf[d.Id].Count == 0 ? "—" : string.Join("・", xcWriterOf[d.Id].OrderBy(x => x).Select(XcK)))} "
                + $"| {(xcReaderOf[d.Id].Count == 0 ? "—" : string.Join("・", xcReaderOf[d.Id].OrderBy(x => x).Select(XcK)))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**{xcMultiKey} / {xcRN} 体**が2キー以上を持つ。");
        Console.WriteLine();

        Console.WriteLine("## §1-3 —— 選定規則の適用（**測る前に固定した規則をそのまま順に当てる**）");
        Console.WriteLine();
        var xcCand = new List<(int A, int B)>();
        for (int a = 0; a < xcNK; a++) for (int b = a + 1; b < xcNK; b++) xcCand.Add((a, b));
        int xcRunning = xcCand.Count;
        Console.WriteLine("| 段 | 落とした組 | 残り |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 出発（55 組） | — | {xcRunning} |");
        var xcLost4 = xcCand.Where(p => xcPairRows[p.A, p.B] > 0).ToList();
        xcCand = xcCand.Where(p => xcPairRows[p.A, p.B] == 0).ToList();
        xcRunning -= xcLost4.Count;
        Console.WriteLine($"| 規則4（既に 61 行にある交差） | {xcLost4.Count} | {xcRunning} |");
        var xcLost1 = xcCand.Where(p => !xcBridge.ContainsKey(p)).ToList();
        xcCand = xcCand.Where(p => xcBridge.ContainsKey(p)).ToList();
        xcRunning -= xcLost1.Count;
        Console.WriteLine($"| 規則1（繋いでいる機構が無い） | {xcLost1.Count} | {xcRunning} |");
        Console.WriteLine();
        Console.WriteLine("**残った候補**（規則2・3・5 と上限 12 はここから当てる）:");
        Console.WriteLine();
        Console.WriteLine("| 候補 | 繋いでいる機構 |");
        Console.WriteLine("|---|---|");
        foreach (var p in xcCand)
            Console.WriteLine($"| **{XcK(p.A)} × {XcK(p.B)}** | {string.Join(" / ", xcBridge[p])} |");
        Console.WriteLine();
        Console.WriteLine("> **規則1 と規則4 を額面どおり当てると候補は 1 組しか残らない。**"
                          + $"空白 {xcBlanks.Count} 組のうち **{xcLost1.Count} 組は繋いでいる機構が1本も無い**"
                          + "——**「交差の空白」は同時に「機構の空白」でもある。**"
                          + "機構が実在する交差は、ひとつ（毒 × 傷）を除いて全部すでに 61 行に乗っている。");
        Console.WriteLine();

        // ---- 55 組の全表（行数 × 機構）。規則4 を緩めるときの材料 ----------------------------
        Console.WriteLine("## §1-3 の材料 —— 55 組を「61 行での行数」で並べた全表");
        Console.WriteLine();
        Console.WriteLine("| 組 | 61 行での行数 | 繋いでいる機構 |");
        Console.WriteLine("|---|--:|---|");
        var xcAll55 = new List<(int A, int B)>();
        for (int a = 0; a < xcNK; a++) for (int b = a + 1; b < xcNK; b++) xcAll55.Add((a, b));
        foreach (var p in xcAll55.OrderBy(p => xcPairRows[p.A, p.B]).ThenBy(p => p.A).ThenBy(p => p.B))
            Console.WriteLine($"| {XcK(p.A)} × {XcK(p.B)} | {xcPairRows[p.A, p.B]} "
                + $"| {(xcBridge.TryGetValue(p, out var bl) ? string.Join(" / ", bl) : "**無し**")} |");
        Console.WriteLine();
        Console.WriteLine($"戦闘 0 回・所要 {xcSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // 交差帯の器具（quality / redo / check が共有）
    // =====================================================================================
    var xcRows = CrossBuilds();
    // 行と同じ並びの交差（**行名の頭に書いてあるものと一致する**）。
    var xcAxis = new (int A, int B)[]
    {
        (UnitTally.CarryPoison, UnitTally.CarryWound),   // 毒×傷
        (UnitTally.CarryWound,  UnitTally.CarryHit),     // 傷×被弾
        (UnitTally.CarryDull,   UnitTally.CarryArmor),   // 弱体×破片
        (UnitTally.CarryDull,   UnitTally.CarryBurn),    // 弱体×燃
        (UnitTally.CarryDull,   UnitTally.CarryMove),    // 弱体×移動
        (UnitTally.CarryDull,   UnitTally.CarryHit),     // 弱体×被弾
        (UnitTally.CarryWhet,   UnitTally.CarryBurn),    // 強化×燃
        (UnitTally.CarryWhet,   UnitTally.CarryIdle),    // 強化×手番
        (UnitTally.CarryWhet,   UnitTally.CarryMove),    // 強化×移動
        (UnitTally.CarryWhet,   UnitTally.CarryHit),     // 強化×被弾
        (UnitTally.CarryArmor,  UnitTally.CarryHit),     // 破片×被弾
        (UnitTally.CarryHit,    UnitTally.CarryMove),    // 被弾×移動
    };
    // **空白（規則4 を満たす）組はこの1つだけ。**残り11 は「薄い交差」（61 行に 1〜39 行ある）。
    var xcBlankRow = new[] { true, false, false, false, false, false, false, false, false, false, false, false };

    // 1戦の計測。**交差の「出会い」＝ 同じ駒の上で両方のキーのカウンタが動いた延べ体数。**
    // `UnitTally.CarryCount` は engine の `NoteCarry` が全キーについて等しく積むので、
    // **12 行すべてを同じ器具で測れる**（機構ごとの計数を手で並べる必要が無い）。
    (bool Won, double Turns, double Meet, double CA, double CB) XcOne(Formation f, Formation e, int seed,
        int ka, int kb, ThornRule? th, MendRule? md)
    {
        BattleResult r = BattleEngine.Run(f, e, seed, verbose: false, thorn: th, mend: md);
        int meet = 0; long ca = 0, cb = 0;
        foreach (var kv in r.TallyByUnit)
        {
            if (kv.Value.CarryCount is not int[] cc) continue;
            ca += cc[ka]; cb += cc[kb];
            if (cc[ka] > 0 && cc[kb] > 0) meet++;
        }
        return (r.PlayerWon, r.Turns, meet, ca, cb);
    }

    (double Win, double Turns, double Meet, double CA, double CB) XcCell(Formation f, Formation e,
        int ka, int kb, ThornRule? th = null, MendRule? md = null)
    {
        int wins = 0; double tt = 0, mt = 0, a = 0, b = 0;
        for (int seed = 0; seed < XcSeeds; seed++)
        {
            var o = XcOne(f, e, seed, ka, kb, th, md);
            if (o.Won) wins++;
            tt += o.Turns; mt += o.Meet; a += o.CA; b += o.CB;
        }
        return (wins * 100.0 / XcSeeds, tt / XcSeeds, mt / XcSeeds, a / XcSeeds, b / XcSeeds);
    }

    // =====================================================================================
    // quality（§1-4・§3）—— そのまま `docs/crossing.md` になる
    // =====================================================================================
    if (xcArg == "quality")
    {
        Console.WriteLine("# 交差帯の勝率表と品質");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 cross quality > docs/crossing.md` の出力。手で編集しない。");
        Console.WriteLine($"交差帯 {xcRows.Length} 行 × 全ステージ、seed 0..{XcSeeds - 1} の {XcSeeds} 試行"
                          + "（**`compare` と同じ seed・同じ試行数**・自己検査 (g)）。");
        Console.WriteLine();
        Console.WriteLine("**`docs/balance.md`（61 行）とは別の生成物**。61 行は<b>軸内</b>を測る器具、"
                          + "交差帯は<b>軸間</b>を測る器具で、目的が違う（第92期）。"
                          + "**壊れを見る拒否権の分母は今までどおり `compare` 61 行全体**（第91期 (G1)）。");
        Console.WriteLine();

        var xcWin = new double[xcRows.Length, xcW];
        var xcTurn = new double[xcRows.Length, xcW];
        var xcMeet = new double[xcRows.Length, xcW];
        var xcCa = new double[xcRows.Length, xcW];
        var xcCb = new double[xcRows.Length, xcW];
        Parallel.For(0, xcRows.Length, ri =>
        {
            for (int w = 0; w < xcW; w++)
            {
                var c = XcCell(xcRows[ri].F, xcStages[w].Enemy, xcAxis[ri].A, xcAxis[ri].B);
                xcWin[ri, w] = c.Win; xcTurn[ri, w] = c.Turns; xcMeet[ri, w] = c.Meet;
                xcCa[ri, w] = c.CA; xcCb[ri, w] = c.CB;
            }
        });

        Console.WriteLine("## 勝率表");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(xcStages.Select((st, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|---|" + string.Concat(xcStages.Select(_ => "---:|")));
        for (int ri = 0; ri < xcRows.Length; ri++)
            Console.WriteLine($"| {xcRows[ri].Name} |"
                + string.Concat(Enumerable.Range(0, xcW).Select(w => $" {xcWin[ri, w]:F1}% |")));
        Console.WriteLine();

        // ---- 表C: 品質（Q1〜Q3）--------------------------------------------------------------
        Console.WriteLine("## 表C —— 交差帯の品質（§1-4・§3）");
        Console.WriteLine();
        Console.WriteLine("`情報セル` は `0 < x < 100` の波の数（第59期 9-1 の定義）。"
                          + "`出会い/戦` は**同じ駒の上で交差の両方のキーのカウンタが動いた延べ体数**、"
                          + "`稼働率` は `出会い/戦 ÷ 決着T`（第86期の作法）。"
                          + "`書き/戦`・`読み/戦` はそれぞれのキーが動いた延べ回数。");
        Console.WriteLine();
        Console.WriteLine("| # | 行 | 空白 | 情報セル | 5波平均 | 決着T | 出会い/戦 | 稼働率 | 書き/戦 | 読み/戦 | 判定 |");
        Console.WriteLine("|--:|---|:-:|--:|--:|--:|--:|--:|--:|--:|:-:|");
        int xcQ1n = 0, xcQ3bad = 0;
        double xcRateSum = 0;
        var xcInfoN = new int[xcRows.Length];
        var xcRateOf = new double[xcRows.Length];
        var xcOk3 = new bool[xcRows.Length];
        for (int ri = 0; ri < xcRows.Length; ri++)
        {
            int info = 0;
            for (int w = 0; w < xcW; w++) if (xcWin[ri, w] > 0.0001 && xcWin[ri, w] < 99.9999) info++;
            xcInfoN[ri] = info;
            double avg = Enumerable.Range(0, xcW).Average(w => xcWin[ri, w]);
            double tt = Enumerable.Range(0, xcW).Average(w => xcTurn[ri, w]);
            double mt = Enumerable.Range(0, xcW).Average(w => xcMeet[ri, w]);
            double ca = Enumerable.Range(0, xcW).Average(w => xcCa[ri, w]);
            double cb = Enumerable.Range(0, xcW).Average(w => xcCb[ri, w]);
            double rate = tt > 0 ? mt / tt : 0;
            xcRateSum += rate; xcRateOf[ri] = rate;
            bool ok3 = mt > 0 && ca > 0 && cb > 0;
            xcOk3[ri] = ok3;
            if (info >= 3) xcQ1n++;
            if (!ok3) xcQ3bad++;
            Console.WriteLine($"| {ri + 1} | {xcRows[ri].Name} | {(xcBlankRow[ri] ? "**○**" : "")} "
                + $"| {(info >= 3 ? $"**{info}**" : info.ToString())} / {xcW} | {avg:F1}% | {tt:F1} "
                + $"| {mt:F2} | {rate * 100:F1}% | {ca:F2} | {cb:F2} | {(ok3 ? "○" : "**×**")} |");
        }
        double xcRate = xcRateSum / xcRows.Length;
        // §3 の「Q1〜Q3 が揃わない行は交差帯から外す」を当てた後の帯。
        var xcKeep = Enumerable.Range(0, xcRows.Length).Where(i => xcInfoN[i] >= 3 && xcOk3[i]).ToArray();
        double xcRateKeep = xcKeep.Length == 0 ? double.NaN : xcKeep.Average(i => xcRateOf[i]);
        Console.WriteLine();

        // ---- 61 行を同じ器具で測る（Q2 の比べる相手）------------------------------------------
        // **第86期の「35〜40%」は読み手の発火 ÷ 決着T という別の器具の値**なので、
        // ここでは 61 行を**同じ器具（出会い ÷ 決着T）**で測り直して並べる。
        var xcRows61 = CompareBuilds();
        var xcRowAx61 = xcRows61.Select(r => XcRowAxes(r.F)).ToArray();
        var xcRate61 = new double[xcRows61.Length];
        var xcHas61 = new bool[xcRows61.Length];
        int[] xcSix2 = { UnitTally.CarryPoison, UnitTally.CarryBurn, UnitTally.CarryWound,
                         UnitTally.CarryMark, UnitTally.CarryStun, UnitTally.CarryArmor };
        Parallel.For(0, xcRows61.Length, ri =>
        {
            // その行が実際に持っている「撒く」軸のうち、行数の多い順に2つを交差として測る。
            var ax = xcSix2.Where(k => xcRowAx61[ri].Contains(k)).ToArray();
            if (ax.Length < 2) { xcRate61[ri] = double.NaN; return; }
            xcHas61[ri] = true;
            double best = 0;
            for (int i = 0; i < ax.Length; i++) for (int j = i + 1; j < ax.Length; j++)
            {
                double mt = 0, tt = 0;
                for (int w = 0; w < xcW; w++)
                {
                    var c = XcCell(xcRows61[ri].F, xcStages[w].Enemy, ax[i], ax[j]);
                    mt += c.Meet; tt += c.Turns;
                }
                best = Math.Max(best, tt > 0 ? mt / tt : 0);
            }
            xcRate61[ri] = best;
        });
        var xc61ok = Enumerable.Range(0, xcRows61.Length).Where(i => xcHas61[i]).ToArray();
        double xcRate61Avg = xc61ok.Length == 0 ? double.NaN : xc61ok.Average(i => xcRate61[i]);

        Console.WriteLine("## 判定（§3）");
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 実測 | 線 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1** | 情報セルが5波中3波以上ある行 | **{xcQ1n} / {xcRows.Length} 行** | ≥ 10 | **{(xcQ1n >= 10 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q2** | 交差帯の平均の稼働率 | **{xcRate * 100:F1}%** "
                          + $"| 61 行の同じ器具 {xcRate61Avg * 100:F1}%（{xc61ok.Length} 行） | **{(xcRate > xcRate61Avg ? "○" : "×")}** |");
        Console.WriteLine($"| **Q3** | 交差の両側が発火していない行 | **{xcQ3bad} 行** | 0 | **{(xcQ3bad == 0 ? "○" : "×")}** |");
        Console.WriteLine();
        Console.WriteLine("### §3 の除外規則を当てた後（「Q1〜Q3 が揃わない行は交差帯から外す」）");
        Console.WriteLine();
        var xcOut = Enumerable.Range(0, xcRows.Length).Except(xcKeep).ToArray();
        Console.WriteLine($"**外した行 {xcOut.Length}**: "
            + (xcOut.Length == 0 ? "無し" : string.Join(" / ", xcOut.Select(i => $"{xcRows[i].Name}（情報セル {xcInfoN[i]}/5{(xcOk3[i] ? "" : "・両側の発火 0")}）"))));
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 12 行のまま | 外した後（{0} 行） | 線 | 判定 |".Replace("{0}", xcKeep.Length.ToString()));
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        Console.WriteLine($"| **Q2** | 交差帯の平均の稼働率 | {xcRate * 100:F1}% | **{xcRateKeep * 100:F1}%** "
                          + $"| 61 行 {xcRate61Avg * 100:F1}% | **{(xcRateKeep > xcRate61Avg ? "○" : "×")}** |");
        Console.WriteLine();
        Console.WriteLine("> **この除外は §3 に書いてある規則をそのまま当てたもので、結果を見てから足した規則ではない。**"
                          + "ただし**外した1行だけで Q2 の判定が反転する**ので、両方の値を並べてある（第64期の作法）。");
        Console.WriteLine();
        Console.WriteLine($"> **Q2 の比べる相手について**: 第86期の「35〜40%」は**読み手の発火 ÷ 決着T**という別の器具の値なので、"
                          + "ここでは 61 行を**交差帯とまったく同じ器具**（出会い ÷ 決着T・その行が実際に持つ「撒く」軸の組で最大のもの）で"
                          + $"測り直して並べてある（2軸以上を持つ {xc61ok.Length} 行が分母。持たない行は交差が定義できない）。");
        Console.WriteLine();

        // ---- 席（§1-4。`reseat` の写し）--------------------------------------------------------
        Console.WriteLine("## 席（`reseat` の写し・粗探索 seed 0..49 → 追試 seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("**採否閾値は 5.0pt**（第46期）。作法1（現行が上位5位以内）で終わる行は動かさない。");
        Console.WriteLine();
        Console.WriteLine("**席は「勝つ席」ではなく「測れる席」で選ぶ**（第50期・第59期・第64期）"
                          + "——交差帯は測るための帯なので、`reseat` の1位が情報セルを減らすなら動かさない。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 現行の粗探索順位 | 現行(200) | 情報セル | 1位(200) | 情報セル | 差 | 判定 | 理由 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|:-:|---|");
        const int XcScan = 50, XcTop = 12;
        var xcSeatLine = new string[xcRows.Length];
        Parallel.For(0, xcRows.Length, ri =>
        {
            var members = xcRows[ri].F.Occupied().Select(x => x.Def).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in SlotAssignments(members.Count))
            {
                var f = new Formation();
                for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
                perms.Add(f);
            }
            var scan = new int[perms.Count];
            for (int i = 0; i < perms.Count; i++)
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in xcStages)
                    for (int seed = 0; seed < XcScan; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int cur = order.First(i => SameFormation(perms[i], xcRows[ri].F));
            int curRank = order.IndexOf(cur) + 1;
            var pool = order.Take(XcTop).Append(cur).Distinct().ToList();
            (double A, int Info) Ver(int i)
            {
                double sum = 0; int info = 0;
                foreach (EnemyCatalog.Stage st in xcStages)
                {
                    int wins = 0;
                    for (int seed = 0; seed < XcSeeds; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    double x = wins * 100.0 / XcSeeds;
                    sum += x;
                    if (x > 0.0001 && x < 99.9999) info++;
                }
                return (sum / xcStages.Count, info);
            }
            var ver = pool.Select(i => { var v = Ver(i); return (I: i, v.A, v.Info); }).OrderByDescending(x => x.A).ToList();
            var curV = ver.First(x => x.I == cur);
            var topV = ver[0];
            // **席は「勝つ席」ではなく「測れる席」で選ぶ**（第50期・第59期・第64期）。
            // 交差帯は測るための帯なので、`reseat` の1位が情報セルを減らすなら動かさない。
            bool keep = curRank <= 5 || topV.A - curV.A < 5.0 || curV.Info >= topV.Info;
            string why = curRank <= 5 ? "上位5位以内" : topV.A - curV.A < 5.0 ? "差 < 5.0pt"
                       : curV.Info >= topV.Info ? "**1位は情報セルが減る**" : "—";
            xcSeatLine[ri] = $"| {xcRows[ri].Name} | {curRank} / {perms.Count} | {curV.A:F1}% | {curV.Info} "
                             + $"| {topV.A:F1}% | {topV.Info} | {topV.A - curV.A:F1} "
                             + $"| {(keep ? "据え置き" : "**要差し替え**")} | {why} |";
        });
        foreach (string l in xcSeatLine) Console.WriteLine(l);
        // **所要秒は出さない**（第124期 段0 と同じ理由）。`docs/crossing.md` は生成物なので、
        // 実測の壁時計を末尾に書くと**「再生成して差分なし」が原理的に成り立たない**
        // ——受け入れ条件 A2 が、盤面を1ビットも動かしていなくても毎回落ちる。
        // 所要はコンソールで見れば足りる（`docs/` に置く値ではない）。
        return;
    }

    // =====================================================================================
    // redo <p>（§4）—— 第84期（`ThornRule`）／第86期（`MendRule`）の再判定
    // =====================================================================================
    if (xcArg == "redo")
    {
        int xcP = args.Length > 3 ? int.Parse(args[3]) : 84;
        bool xcIs84 = xcP == 84;
        string xcAid = xcIs84 ? "kado" : "nono";
        int xcA = xcIdx[xcAid];
        string[] xcIntended = xcIs84
            ? new[] { "kiri", "nomi", "egu", "nata", "hari" }
            : new[] { "kado", "golm", "borg", "rica", "zoto", "nara" };
        ThornRule? XcTh(int v) => xcIs84 ? new ThornRule(v == 0 ? ThornWound.None : ThornWound.Foe) : null;
        MendRule? XcMd(int v) => xcIs84 ? null : new MendRule(v == 0 ? MendSide.Plain : MendSide.Wound);
        string XcVN(int v) => xcIs84 ? (v == 0 ? "V0（現行・棘は傷を書かない）" : "V1（棘の傷）")
                                     : (v == 0 ? "X0（現行・繕いは傷を読まない）" : "X1（繕いが傷を読む）");

        Console.WriteLine($"# 第92期 表{(xcIs84 ? "D" : "E")} —— 第{xcP}期の再判定");
        Console.WriteLine();
        Console.WriteLine($"A = **{xcRoster[xcA].Name}**・{XcVN(0)} 対 {XcVN(1)}。"
            + "**対象・意図した相手・判定の手続きは §5 の測定より先にコミットしてある**（自己検査 (d)）。");
        Console.WriteLine();

        // ---- 門（§4-2。鎖が繋がっているか。**紙は出力であって門ではない**・第90期）------------
        Console.WriteLine("## 門 —— 鎖が繋がっているか（交差帯で測る）");
        Console.WriteLine();
        Console.WriteLine("(1) その通貨を持つ相手が存在する ／ (2) その相手に書き手が書く ／ (3) 書いたものが実際に払い出される。");
        Console.WriteLine();
        (double Sup, double Meet, double Pay, double Fires, double Turns) XcGate(int v)
        {
            double sup = 0, meet = 0, pay = 0, fires = 0, turns = 0; int n = 0;
            foreach (var (_, f) in xcRows)
                for (int w = 0; w < xcW; w++)
                {
                    for (int seed = 0; seed < XcSeeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, xcStages[w].Enemy, seed, verbose: false,
                                                          thorn: XcTh(v), mend: XcMd(v));
                        turns += r.Turns;
                        foreach (var kv in r.TallyByUnit)
                        {
                            if (kv.Value.CarryCount is int[] cc) sup += cc[UnitTally.CarryWound];
                            meet += kv.Value.GougeFires + kv.Value.SutureFoe + kv.Value.SutureAlly + kv.Value.MendWoundSeen;
                            pay += kv.Value.GougeOut + kv.Value.SutureHealed + kv.Value.MendHealed;
                            fires += xcIs84 ? kv.Value.GougeFires + kv.Value.SutureFoe + kv.Value.SutureAlly
                                            : kv.Value.MendFires;
                        }
                    }
                    n += XcSeeds;
                }
            return (sup / n, meet / n, pay / n, fires / n, turns / n);
        }
        var g0 = XcGate(0); var g1 = XcGate(1);
        Console.WriteLine("| 段 | 量 | V0 | V1 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| (1)(2) 供給 | 傷が書かれた回数/戦（全駒） | {g0.Sup:F2} | {g1.Sup:F2} | {(g1.Sup > 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (2) 出会い | 傷の読み手が傷に触れた回数/戦 | {g0.Meet:F2} | {g1.Meet:F2} | {(g1.Meet > 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (3) 払い出し | 傷由来の出力/戦（抉り+縫い+繕い） | {g0.Pay:F1} | {g1.Pay:F1} | {(g1.Pay > 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| 参考 | 読み手の発火/戦 | {g0.Fires:F2} | {g1.Fires:F2} | |");
        Console.WriteLine($"| 参考 | 稼働率（発火 ÷ 決着T） | {(g0.Turns > 0 ? g0.Fires / g0.Turns * 100 : 0):F1}% | {(g1.Turns > 0 ? g1.Fires / g1.Turns * 100 : 0):F1}% | |");
        Console.WriteLine();
        Console.WriteLine("**紙は Phase 0 で出すが門にはしない**（第90期の規約）。上の3段がどれも 0 でなければ鎖は繋がっている。");
        Console.WriteLine();

        // ---- 交差帯での `compare` 差分（§4-3 の主判定の片側）------------------------------------
        Console.WriteLine("## 交差帯での差分（分母 = 交差帯 12 行）");
        Console.WriteLine();
        var xcD0 = new double[xcRows.Length, xcW];
        var xcD1 = new double[xcRows.Length, xcW];
        Parallel.For(0, xcRows.Length, ri =>
        {
            for (int w = 0; w < xcW; w++)
            {
                int a0 = 0, a1 = 0;
                for (int seed = 0; seed < XcSeeds; seed++)
                {
                    if (BattleEngine.Run(xcRows[ri].F, xcStages[w].Enemy, seed, verbose: false, thorn: XcTh(0), mend: XcMd(0)).PlayerWon) a0++;
                    if (BattleEngine.Run(xcRows[ri].F, xcStages[w].Enemy, seed, verbose: false, thorn: XcTh(1), mend: XcMd(1)).PlayerWon) a1++;
                }
                xcD0[ri, w] = a0 * 100.0 / XcSeeds; xcD1[ri, w] = a1 * 100.0 / XcSeeds;
            }
        });
        int xcMoved = 0, xcMovedRows = 0;
        Console.WriteLine("| 行 |" + string.Concat(xcStages.Select((st, i) => $" 第{i + 1}波 |")) + " 5波平均 Δ |");
        Console.WriteLine("|---|" + string.Concat(xcStages.Select(_ => "---:|")) + "---:|");
        for (int ri = 0; ri < xcRows.Length; ri++)
        {
            int mv = 0;
            var cells = new List<string>();
            for (int w = 0; w < xcW; w++)
            {
                bool d = Math.Abs(xcD0[ri, w] - xcD1[ri, w]) > 1e-9;
                if (d) mv++;
                cells.Add(d ? $" {xcD0[ri, w]:F1} → **{xcD1[ri, w]:F1}** |" : $" {xcD0[ri, w]:F1} |");
            }
            double dv = Enumerable.Range(0, xcW).Average(w => xcD1[ri, w] - xcD0[ri, w]);
            xcMoved += mv; if (mv > 0) xcMovedRows++;
            Console.WriteLine($"| {xcRows[ri].Name} |" + string.Concat(cells) + $" {dv:+0.0;-0.0;0.0} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**動いたセルは {xcMoved} / {xcRows.Length * xcW}・{xcMovedRows} 行。**");
        Console.WriteLine();

        // ---- 拒否権（分母 = `compare` 61 行全体。第91期 (G1)(G2)）------------------------------
        Console.WriteLine("## 拒否権（分母 = `compare` 61 行全体・第91期 (G1)(G2)）");
        Console.WriteLine();
        var xcC = CompareBuilds();
        var xcE0 = new double[xcC.Length, xcW];
        var xcE1 = new double[xcC.Length, xcW];
        Parallel.For(0, xcC.Length, ri =>
        {
            for (int w = 0; w < xcW; w++)
            {
                int a0 = 0, a1 = 0;
                for (int seed = 0; seed < XcSeeds; seed++)
                {
                    if (BattleEngine.Run(xcC[ri].F, xcStages[w].Enemy, seed, verbose: false, thorn: XcTh(0), mend: XcMd(0)).PlayerWon) a0++;
                    if (BattleEngine.Run(xcC[ri].F, xcStages[w].Enemy, seed, verbose: false, thorn: XcTh(1), mend: XcMd(1)).PlayerWon) a1++;
                }
                xcE0[ri, w] = a0 * 100.0 / XcSeeds; xcE1[ri, w] = a1 * 100.0 / XcSeeds;
            }
        });
        var xcPrim2 = new HashSet<string>(Baseline.PrimaryRows);
        var xcPi = Enumerable.Range(0, xcC.Length).Where(i => xcPrim2.Contains(xcC[i].Name)).ToArray();
        double xcF0 = xcPi.Average(i => xcE0[i, xcW - 1]), xcF1 = xcPi.Average(i => xcE1[i, xcW - 1]);
        int xcCeil0 = Enumerable.Range(0, xcC.Length).Count(i => xcE0[i, xcW - 1] > 95.0);
        int xcCeil1 = Enumerable.Range(0, xcC.Length).Count(i => xcE1[i, xcW - 1] > 95.0);
        var xcDrop = new List<(int Row, int Wave, double A, double B)>();
        for (int ri = 0; ri < xcC.Length; ri++) for (int w = 0; w < xcW; w++)
            if (xcE1[ri, w] - xcE0[ri, w] <= -10.0) xcDrop.Add((ri, w, xcE0[ri, w], xcE1[ri, w]));
        int xcCells = 0; var xcCellRows = new List<int>();
        for (int ri = 0; ri < xcC.Length; ri++)
        {
            int m = 0;
            for (int w = 0; w < xcW; w++) if (Math.Abs(xcE0[ri, w] - xcE1[ri, w]) > 1e-9) m++;
            xcCells += m; if (m > 0) xcCellRows.Add(ri);
        }
        Console.WriteLine($"`compare` {xcC.Length * xcW} セルのうち動いたのは **{xcCells} セル / {xcCellRows.Count} 行**。");
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 実測 | 線 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| 拒否権1 | 主判定19行の第五波平均 | {xcF0:F1} → **{xcF1:F1}** | ≥ {Baseline.PrimaryFifthFloor:F1} | **{(xcF1 >= Baseline.PrimaryFifthFloor ? "○" : "×")}** |");
        Console.WriteLine($"| 拒否権2 | 第五波 95% 超の行 | {xcCeil0} → **{xcCeil1}** | 新たに +2 未満 | **{(xcCeil1 - xcCeil0 < 2 ? "○" : "×")}** |");
        Console.WriteLine($"| 拒否権3 | いずれかの波で −10.0pt 以上落ちた行 | **{xcDrop.Select(d => d.Row).Distinct().Count()} 行** | （下で壊れ／制約に分ける） | |");
        Console.WriteLine();
        if (xcDrop.Count > 0)
        {
            Console.WriteLine("### (G2) 壊れと制約の分解");
            Console.WriteLine();
            Console.WriteLine("落ちた行に含まれる駒それぞれについて、**その駒を含む「他の行」の全波平均の変化**を出す。"
                              + "どの駒も \\|平均変化\\| < 3.0pt なら**組み合わせ固有の制約**（拒否しない）、"
                              + "いずれかの駒で ≤ −3.0pt ならその駒が使えなくなっている＝**壊れ**。");
            Console.WriteLine();
            Console.WriteLine("| 落ちた行 | 波 | V0 → V1 | 駒 | 他の行 | 他の行の全波平均の変化 | 判定 |");
            Console.WriteLine("|---|---|--:|---|--:|--:|:-:|");
            foreach (var d in xcDrop)
            {
                var ids = xcC[d.Row].F.Occupied().Select(o => o.Def.Id).ToArray();
                foreach (string id in ids)
                {
                    var others = Enumerable.Range(0, xcC.Length)
                        .Where(i => i != d.Row && xcC[i].F.Occupied().Any(o => o.Def.Id == id)).ToArray();
                    double dv = others.Length == 0 ? double.NaN
                        : others.Average(i => Enumerable.Range(0, xcW).Average(w => xcE1[i, w] - xcE0[i, w]));
                    string verdict = others.Length == 0 ? "**分解不成立**" : dv <= -3.0 ? "**壊れ**" : "制約";
                    Console.WriteLine($"| {xcC[d.Row].Name} | 第{d.Wave + 1}波 | {d.A:F1} → {d.B:F1} "
                        + $"| {UnitCatalog.ById(id).Name} | {others.Length} "
                        + $"| {(double.IsNaN(dv) ? "—" : dv.ToString("+0.00;-0.00;0.00"))} | {verdict} |");
                }
            }
            Console.WriteLine();
        }
        else Console.WriteLine("**−10.0pt 以上落ちた行は 0**（分解の必要が無い）。");
        Console.WriteLine();

        // ---- 主判定: 2x2（第88期 §4 の特異性・ドラフト台）---------------------------------------
        Console.WriteLine("## 主判定 —— 2x2 の Δ相乗（第88期 §4 の特異性・**ドラフト台**）");
        Console.WriteLine();
        const int XcTableSeed = 9_200_000;              // **第92期の標本**（第90・91期は 9,000,000）
        const int XcK64 = 64, XcS = 2, XcBand = 0, XcM = 8;
        const int XcStrong = 7, XcWeakPct = 60, XcDrawCap = 20000;
        const int XcMinInfo = 20, XcFpLine = 3;
        const double XcEps = 1e-9;
        Console.WriteLine($"器具は第81期 `pairs2` の写しで**定数を1つも変えていない**（K = {XcK64} 台／組／系列・系列 {XcS} 本・"
                          + $"戦闘 seed {XcBand}..{XcBand + XcM - 1}・弱い波 {XcWeakPct}%・第2〜{xcW}波）。"
                          + $"台の抽選は `TableSeed` = {XcTableSeed:N0}（**第92期の標本**）。");
        Console.WriteLine();

        var xcWeakCache = new Dictionary<string, UnitDef>();
        UnitDef XcWeakOf(UnitDef d)
        {
            if (xcWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
            w = new UnitDef { Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * XcWeakPct / 100,
                              Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
                              Pattern = d.Pattern, Actions = d.Actions };
            xcWeakCache[d.Id] = w; return w;
        }
        var xcWeak = xcStages.Select(st =>
        {
            var f = new Formation();
            foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = XcWeakOf(d);
            return new EnemyCatalog.Stage(st.Name, f);
        }).ToArray();
        UnitDef XcPlain(UnitDef d) => new() { Id = d.Id + "_plain", Name = "素体の" + d.Name,
            MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
            Traits = Array.Empty<TraitId>(), Pattern = d.Pattern };
        var xcPlainMap = xcRoster.ToDictionary(d => d.Id, XcPlain);
        UnitDef[] XcFill(UnitDef[] pool, int strong0, int seed)
        {
            int rn = pool.Length; var rng = new Random(seed);
            var idx = new int[rn];
            for (int k = 0; k < rn; k++) idx[k] = k;
            int remain = rn, strong = strong0;
            var picked = new UnitDef[3];
            for (int r = 0; r < 3; r++)
            {
                var offer = new UnitDef[3];
                for (int t = 0; t < 3; t++)
                {
                    int j = t + rng.Next(remain - t);
                    (idx[t], idx[j]) = (idx[j], idx[t]);
                    offer[t] = pool[idx[t]];
                }
                UnitDef sel = strong < 2
                    ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                    : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
                picked[r] = sel;
                if (sel.Attack >= XcStrong) strong++;
                int pi = 0;
                for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
                (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
                remain--;
            }
            return picked;
        }
        int XcSeedOf(int tableSeed, int pairIx, int draw)
        {
            ulong x = (ulong)tableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (int)(x & 0x7FFFFFFFUL);
        }
        int[] XcSeats(UnitDef[] u)
        {
            var all5 = new[] { 0, 1, 2, 3, 4 };
            var front = all5.OrderByDescending(k => u[k].MaxHp).ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
            var rest = all5.Where(k => k != front[0] && k != front[1]).ToArray();
            var back = rest.OrderByDescending(k => u[k].Attack).ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
            int center = rest.Single(k => k != back[0] && k != back[1]);
            var r = new int[5];
            r[front[0]] = 0; r[front[1]] = 1; r[center] = 2; r[back[0]] = 3; r[back[1]] = 4;
            return r;
        }
        // **添字**: 0 = y11 / 1 = y01（**A 素体**・B 本物）/ 2 = y10（A 本物・**B 素体**）/ 3 = y00
        Formation XcForm(UnitDef[] u, int[] seats, int cell)
        {
            var f = new Formation();
            for (int k = 0; k < 5; k++)
            {
                bool plain = (k == 0 && (cell & 1) != 0) || (k == 1 && (cell & 2) != 0);
                f[seats[k]] = plain ? xcPlainMap[u[k].Id] : u[k];
            }
            return f;
        }
        double XcRate2(Formation f, int v)
        {
            double sum = 0;
            for (int wi = 1; wi < xcW; wi++)
            {
                int wins = 0;
                for (int seed = XcBand; seed < XcBand + XcM; seed++)
                    if (BattleEngine.Run(f, xcWeak[wi].Enemy, seed, verbose: false, thorn: XcTh(v), mend: XcMd(v)).PlayerWon) wins++;
                sum += wins * 100.0 / XcM;
            }
            return sum / (xcW - 1);
        }
        var xcPairIx = new int[xcRN, xcRN];
        { int pi = 0; for (int a = 0; a < xcRN; a++) for (int b = a + 1; b < xcRN; b++) { xcPairIx[a, b] = xcPairIx[b, a] = pi; pi++; } }
        double[][] XcMeasure(int a, int b, int v)
        {
            var pool = xcRoster.Where((_, u) => u != a && u != b).ToArray();
            int strong0 = (xcRoster[a].Attack >= XcStrong ? 1 : 0) + (xcRoster[b].Attack >= XcStrong ? 1 : 0);
            var seen = new HashSet<(int, int, int)>();
            var fills = new List<UnitDef[]>();
            for (int draw = 0; fills.Count < XcS * XcK64 && draw < XcDrawCap; draw++)
            {
                var f = XcFill(pool, strong0, XcSeedOf(XcTableSeed, xcPairIx[a, b], draw));
                var t = f.Select(d => xcIdx[d.Id]).OrderBy(x => x).ToArray();
                if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
            }
            var ys = new double[fills.Count][];
            for (int t = 0; t < fills.Count; t++)
            {
                var team = new[] { xcRoster[a], xcRoster[b], fills[t][0], fills[t][1], fills[t][2] };
                int[] seats = XcSeats(team);
                ys[t] = new double[4];
                for (int cell = 0; cell < 4; cell++) ys[t][cell] = XcRate2(XcForm(team, seats, cell), v);
            }
            return ys;
        }
        double XcPct(IReadOnlyList<double> xs, double q)
        {
            if (xs.Count == 0) return double.NaN;
            var s = xs.OrderBy(v => v).ToArray();
            if (s.Length == 1) return s[0];
            double pos = q * (s.Length - 1);
            int lo = (int)Math.Floor(pos), hi = Math.Min(lo + 1, s.Length - 1);
            return s[lo] + (pos - lo) * (s[hi] - s[lo]);
        }
        double XcSd(IReadOnlyList<double> xs)
        {
            int n = xs.Count; if (n < 2) return double.NaN;
            double m = xs.Average();
            return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
        }
        string XcP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");

        var xcOthers = Enumerable.Range(0, xcRN).Where(u => u != xcA).ToArray();
        var xcY0 = new double[xcOthers.Length][][];
        var xcY1 = new double[xcOthers.Length][][];
        Console.Error.Write($"cross redo {xcP}: ");
        int xcDone = 0;
        Parallel.For(0, xcOthers.Length, j =>
        {
            xcY0[j] = XcMeasure(xcA, xcOthers[j], 0);
            xcY1[j] = XcMeasure(xcA, xcOthers[j], 1);
            if (Interlocked.Increment(ref xcDone) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        var xcIntSet = new HashSet<int>(xcIntended.Select(i => xcIdx[i]));
        double XcSyn(double[] y) => y[0] - y[2] - y[1] + y[3];
        bool XcFloor(double[] y) => y[3] < XcEps && y[1] < XcEps;
        bool XcCeil(double[] y) => y[3] > 100 - XcEps && y[1] > 100 - XcEps;
        var xcDelta = new Dictionary<int, double[]>();
        var xcInfo = new Dictionary<int, bool[]>();
        var xcCnt = new Dictionary<int, (int All, int Floor, int Ceil, int Info)>();
        int xcMism = 0;
        for (int j = 0; j < xcOthers.Length; j++)
        {
            int b = xcOthers[j];
            int nT = Math.Min(xcY0[j].Length, xcY1[j].Length);
            var d = new double[nT]; var inf = new bool[nT];
            int fl = 0, ce = 0, ok = 0;
            for (int t = 0; t < nT; t++)
            {
                d[t] = XcSyn(xcY1[j][t]) - XcSyn(xcY0[j][t]);
                if (Math.Abs(xcY0[j][t][1] - xcY1[j][t][1]) > 1e-9) xcMism++;
                if (Math.Abs(xcY0[j][t][3] - xcY1[j][t][3]) > 1e-9) xcMism++;
                inf[t] = !XcFloor(xcY0[j][t]) && !XcCeil(xcY0[j][t]);
                if (XcFloor(xcY0[j][t])) fl++; else if (XcCeil(xcY0[j][t])) ce++; else ok++;
            }
            xcDelta[b] = d; xcInfo[b] = inf; xcCnt[b] = (nT, fl, ce, ok);
        }
        (double M, double[] Ser, int N) XcAgg(int b, bool filtered)
        {
            var all = new List<double>(); var s0 = new List<double>(); var s1 = new List<double>();
            double[] d = xcDelta[b]; bool[] inf = xcInfo[b];
            for (int t = 0; t < d.Length; t++)
            {
                if (filtered && !inf[t]) continue;
                all.Add(d[t]); (t % XcS == 0 ? s0 : s1).Add(d[t]);
            }
            return (all.Count > 0 ? all.Average() : double.NaN,
                    new[] { s0.Count > 0 ? s0.Average() : double.NaN, s1.Count > 0 ? s1.Average() : double.NaN }, all.Count);
        }
        // **(G3)**: `ThornRule` / `MendRule` はどちらも**駒の特性の中**にあるので情報帯フィルタを当てる。
        var xcAgF = xcOthers.ToDictionary(b => b, b => XcAgg(b, true));
        var xcAgA = xcOthers.ToDictionary(b => b, b => XcAgg(b, false));
        var xcMeas = xcOthers.Where(b => xcCnt[b].Info >= XcMinInfo).ToHashSet();
        var xcUnint = xcMeas.Where(b => !xcIntSet.Contains(b)).ToArray();
        double xcFloorN = XcPct(xcUnint.Select(b => Math.Abs(xcAgF[b].M)).ToArray(), 0.95);
        int cAll = xcOthers.Sum(b => xcCnt[b].All), cFl = xcOthers.Sum(b => xcCnt[b].Floor),
            cCe = xcOthers.Sum(b => xcCnt[b].Ceil), cIn = xcOthers.Sum(b => xcCnt[b].Info);
        Console.WriteLine("| | 台数 | 割合 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 全台（{xcOthers.Length} 体 × {XcS * XcK64} 台） | {cAll:N0} | 100.0% |");
        Console.WriteLine($"| 床（`y00` = `y01` = 0.0%） | {cFl:N0} | {cFl * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| 天井（`y00` = `y01` = 100.0%） | {cCe:N0} | {cCe * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| **情報帯** | **{cIn:N0}** | **{cIn * 100.0 / cAll:F1}%** |");
        Console.WriteLine();
        Console.WriteLine($"**自己検査 (a)**: A 素体の2セル（`y01` / `y00`）が版で完全一致 → {cAll * 2:N0} セル・ずれ **{xcMism}** 件"
                          + $" → **{(xcMism == 0 ? "○" : "×")}**。");
        Console.WriteLine();
        Console.WriteLine($"**ノイズ床（第89期 §1-1 の規約）= 意図しない {xcUnint.Length} 体の \\|Δ\\| の 95%tile = {xcFloorN:F2}pt**"
                          + $"（中央値 {XcPct(xcUnint.Select(b => Math.Abs(xcAgF[b].M)).ToArray(), 0.5):F2}）。");
        Console.WriteLine();
        var xcOrd = xcMeas.OrderByDescending(b => xcAgF[b].M).ToArray();
        Console.WriteLine("| 順位 | B | **Δ（情報帯）** | 情報帯 | 系列1 | 系列2 | 床超 | Δ（全台） |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|:-:|--:|");
        var xcShown = new HashSet<int>();
        void XcRow(int b)
        {
            int rf = Array.IndexOf(xcOrd, b) + 1;
            var af = xcAgF[b];
            bool over = xcMeas.Contains(b) && Math.Abs(af.M) > xcFloorN;
            Console.WriteLine($"| {(rf > 0 ? rf.ToString() : "—")} | {(xcIntSet.Contains(b) ? "★" : "")}{xcRoster[b].Name} "
                + $"| **{XcP2(af.M)}** | {xcCnt[b].Info} | {XcP2(af.Ser[0])} | {XcP2(af.Ser[1])} | {(over ? "○" : "")} | {XcP2(xcAgA[b].M)} |");
        }
        int xcTop10 = Math.Min(10, xcOrd.Length);
        for (int i = 0; i < xcTop10; i++) { XcRow(xcOrd[i]); xcShown.Add(xcOrd[i]); }
        if (xcOrd.Length > 2 * xcTop10) Console.WriteLine("| … | | | | | | | |");
        for (int i = Math.Max(xcTop10, xcOrd.Length - xcTop10); i < xcOrd.Length; i++) { XcRow(xcOrd[i]); xcShown.Add(xcOrd[i]); }
        Console.WriteLine();
        var xcRest = xcIntSet.Concat(xcOthers.Where(b => !xcMeas.Contains(b))).Distinct().Where(b => !xcShown.Contains(b)).ToArray();
        if (xcRest.Length > 0)
        {
            Console.WriteLine("意図した相手（上の表に出ていないぶん）と「測れていない」組:");
            Console.WriteLine();
            Console.WriteLine("| 順位 | B | **Δ（情報帯）** | 情報帯 | 系列1 | 系列2 | 床超 | Δ（全台） |");
            Console.WriteLine("|--:|---|--:|--:|--:|--:|:-:|--:|");
            foreach (int b in xcRest) XcRow(b);
            Console.WriteLine();
        }
        var xcIntM = xcIntSet.Where(xcMeas.Contains).ToArray();
        var xcPass = xcIntM.Where(b => xcAgF[b].Ser[0] > 0 && xcAgF[b].Ser[1] > 0 && xcAgF[b].M > xcFloorN).ToArray();
        var xcFp = xcUnint.Where(b => Math.Abs(xcAgF[b].M) > xcFloorN).ToArray();
        bool q11 = xcPass.Length >= 1, q12 = xcFp.Length <= XcFpLine;
        int xcBest = xcIntM.Length == 0 ? 0 : xcIntM.Min(b => Array.IndexOf(xcOrd, b) + 1);
        Console.WriteLine("| | 内容 | 実測 | 線 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1-1** | 意図した相手のうち 2系列とも正 かつ \\|Δ\\| > ノイズ床 | **{xcPass.Length} / {xcIntM.Length} 枚**"
            + (xcPass.Length > 0 ? "（" + string.Join("・", xcPass.Select(b => $"{xcRoster[b].Name} {XcP2(xcAgF[b].M)}")) + "）" : "")
            + $" | ≥ 1 | **{(q11 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1-2** | 意図しない相手で床超の体数 | **{xcFp.Length} / {xcUnint.Length} 体** | ≤ {XcFpLine} | **{(q12 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1** | **特異性（主判定）** | | | **{(q11 && q12 ? "○" : "×")}** |");
        Console.WriteLine($"| Q2 | 意図した組の最良順位（{xcOrd.Length} 体中） | **{(xcBest > 0 ? xcBest + " 位" : "—")}** | ≤ 10 | **{(xcBest > 0 && xcBest <= 10 ? "○" : "×")}** |");
        Console.WriteLine();
        Console.WriteLine($"**第{xcP}期の再判定の結論: {(q11 && q12 ? "通る" : "通らない")}**"
                          + $"（Q1-1 {(q11 ? "○" : "×")} / Q1-2 {(q12 ? "○" : "×")}）。"
                          + "**再判定で通っても、61 行の拒否権で「壊れ」と出たら採らない**（§4-3）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {xcSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check（自己検査）
    // =====================================================================================
    if (xcArg == "check")
    {
        Console.WriteLine("# 第92期 表F —— 自己検査");
        Console.WriteLine();

        // (b) `compare` 305 セルが `docs/balance.md` と 0 件
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Path.GetDirectoryName(root);
        int xcBad = -1, xcCells2 = 0;
        if (root != null)
        {
            var doc = File.ReadAllLines(Path.Combine(root, "docs", "balance.md"));
            var want = new Dictionary<string, double[]>();
            foreach (string line in doc)
            {
                if (!line.StartsWith("| ") || line.StartsWith("|---")) continue;
                var c = line.Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length < 3) continue;
                var vals = new List<double>();
                bool ok = true;
                for (int i = 2; i < c.Length - 1; i++)
                {
                    if (!c[i].EndsWith("%") || !double.TryParse(c[i].TrimEnd('%'), out double v)) { ok = false; break; }
                    vals.Add(v);
                }
                if (ok && vals.Count == xcW) want[c[1]] = vals.ToArray();
            }
            var rows2 = CompareBuilds();
            xcBad = 0;
            var lockO = new object();
            Parallel.For(0, rows2.Length, ri =>
            {
                if (!want.TryGetValue(rows2[ri].Name, out double[]? exp)) { lock (lockO) xcBad += xcW; return; }
                int bad = 0, n = 0;
                for (int w = 0; w < xcW; w++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < XcSeeds; seed++)
                        if (BattleEngine.Run(rows2[ri].F, xcStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                    n++;
                    if (Math.Abs(wins * 100.0 / XcSeeds - exp[w]) > 0.051) bad++;
                }
                lock (lockO) { xcBad += bad; xcCells2 += n; }
            });
        }

        // (f) `ctx.PickOne` を新たに使っていないこと
        int xcPick = 0;
        if (root != null)
        {
            string prog = File.ReadAllText(Path.Combine(root, "BattleSim", "Program.cs"));
            int st = prog.IndexOf("if (focusId == \"cross\")", StringComparison.Ordinal);
            int en = prog.IndexOf("if (focusId == \"ptrace\")", StringComparison.Ordinal);
            // **呼び出しの形だけを数える。**探す文字列は実行時に組み立てる
            // ——ソースに直に書くと、この検査自身の行が引っかかって必ず 1 以上になる（実際に踏んだ）。
            string needle = "." + "PickOne" + "(";
            if (st >= 0 && en > st) xcPick = prog.Substring(st, en - st).Split(needle).Length - 1;
        }

        Console.WriteLine("| | 内容 | 実測 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        Console.WriteLine("| **(a)** | `docs/` の既存8ファイルが差分 0 バイト（`CompareBuilds()` を触っていない証拠） | `git diff --stat docs/` で確認 | — |");
        Console.WriteLine($"| **(b)** | `compare` {xcCells2} セルが `docs/balance.md` と一致 | **ずれ {xcBad} 件** | **{(xcBad == 0 ? "○" : "×")}** |");
        Console.WriteLine("| **(c)** | 交差帯の各行で交差の両側の発火 > 0（Q3） | `cross quality` の表C | — |");
        Console.WriteLine("| **(d)** | §4-2 の事前登録が §5 の測定より先にコミットされている | コミットの順序 | — |");
        Console.WriteLine($"| **(e)** | 再判定で使ったノブ以外が既定のまま | 下の表 | — |");
        Console.WriteLine($"| **(f)** | `ctx.PickOne` を新たに使っていない | **{xcPick} 件** | **{(xcPick == 0 ? "○" : "×")}** |");
        Console.WriteLine($"| **(g)** | 交差帯の seed・試行数が `compare` と同じ | seed 0..{XcSeeds - 1}・{XcSeeds} 試行 | **○** |");
        Console.WriteLine("| **(h)** | 選定規則の各項が何組を落としたか | `cross phase0` の §1-3 | — |");
        Console.WriteLine();
        Console.WriteLine("## (e) 規則の既定（**再判定で振った2本以外は既定のまま**）");
        Console.WriteLine();
        Console.WriteLine("| 規則 | 既定 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine($"| `ThornRule`（第84期・**この期に振った**） | `{ThornRule.Default.Wound}` |");
        Console.WriteLine($"| `MendRule`（第86期・**この期に振った**） | `{MendRule.Default.Side}` |");
        Console.WriteLine($"| `SutureRule` | `{SutureRule.Default.Side}` |");
        Console.WriteLine($"| `SpillWoundRule` | `{SpillWoundRule.Default.Enabled}` / `{SpillWoundRule.Default.Scope}` |");
        Console.WriteLine($"| `GatherRule` | `{GatherRule.Default.Enabled}` |");
        Console.WriteLine($"| `SoakRule` | 毒 `{SoakRule.Default.Poison}` / 燃 `{SoakRule.Default.Burn}` |");
        Console.WriteLine($"| `IgniteRule` | `{IgniteRule.Default.Enabled}` |");
        Console.WriteLine($"| `SeverRule` | `{SeverRule.Default.Wait}` / 閾値 {SeverRule.Default.Threshold} |");
        Console.WriteLine($"| `ThinBladeRule` | `{ThinBladeRule.Default.Cost}` |");
        Console.WriteLine($"| `FunnelRule` | `{FunnelRule.Default.Slowest}` / `{FunnelRule.Default.Both}` |");
        Console.WriteLine();
        Console.WriteLine($"所要 {xcSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("cross: phase0 / quality / redo <84|86> / check");
    return;
}
}
