using System.Text.RegularExpressions;
using BattleCore;

// =====================================================================================
// wound2 モード（第120期） —— 傷という通貨の棚卸し。**測るだけ。機構は1つも作らない**
//
// 起点は第119期: 見直し対象の実質5体のうち3体（ノノ +0.28／ナタ +0.59／エグ +0.66）が傷軸で、
// 第82期でも差し替え候補6体のうち3体が傷の読み手だった。**37期越しに同じ場所が出ている。**
// 第73〜75期が3期かけて代金を直しても値段は動いていない。
//
// > **したがって問題は駒ではなく通貨の側にある。ただし在庫がどこにあるかを知らない。**
//
// **engine に規則は1本も足していない。** 足したのは (1) 規則の受け渡し（`WoundRule`）と
// (2) 計数の3種類（在庫の走査／消滅の帳簿／盤面から実際に減った HP）だけで、
// **既定では 1 ビットも動かない**（`compare` 305 セル 0 件が受け入れ基準）。
//
//     dotnet run --project BattleSim -c Release 0 wound2 phase0   # 表P（実装の列挙・戦闘0回）
//     dotnet run --project BattleSim -c Release 0 wound2 run      # 表A〜E（W1〜W4 ＋ 案Aの材料）
//     dotnet run --project BattleSim -c Release 0 wound2 check    # 自己検査
//
// **`Program.cs` ではなく別ファイルにしてある**（`Tank.cs` と同じ判断——あちらは 67,000 行が
// 全部で1つのメソッドで、Release のビルドに 4 分半かかる）。クロージャを1つも作らない形で書く。
// =====================================================================================
static class Wound2Diag
{
    // --- 定数（測る前に固定する）--------------------------------------------------------
    const int Seeds = 200;                            // 帯A。`compare` と揃える（規約 (G14)）
    static readonly int[] Waves = { 1, 2, 3, 4 };     // 第2〜5波（規約 (G10)。第一波は分母に入れない）
    const double AttrLine = 1.5;                      // Q4 の線（いずれかの波で −1.5pt 以上）

    static readonly string[] RouteName =
        { "裂き(キリ)", "刻み(ノミ)", "巻き込み則(engine)", "棘の傷", "棘の巻き込み", "引き取り(中継)" };
    static readonly string[] LossName =
        { "断ちが消費", "繕いの塞ぎ", "縫いの塞ぎ", "引き取り(donor)", "深手に束ね", "会戦の掃除", "読まれず駒が死亡", "読まれず決着" };
    static readonly string[] ReaderName =
        { "抉り(エグ)", "刻みのなぞり(ノミ)", "断ち(ナタ)", "縫い(ハリ)", "繕い(ノノ)", "滲み則(engine)" };
    static readonly string[] GuardName =
        { "庇う(ガルド)", "後備え(ドハ)", "棘守り(セッキ)", "巨躯(ゴルム)", "殉教(敵側)" };

    static string? _root;

    // ==================================================================================
    public static void Run(string mode, string sub, string sub2 = "")
    {
        if (!Init()) return;
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunTables(); return;
            case "check": Check(); return;
            // 第121期 —— 巻き込み則の版を並べる。**既存3モードの呼び出しは1文字も変わらない。**
            case "spill": SpillRun(sub, sub2); return;
            default: Console.WriteLine("wound2: モードは phase0 / run / check / spill のいずれか。"); return;
        }
    }

    static bool Init()
    {
        _root = Directory.GetCurrentDirectory();
        while (_root != null && !File.Exists(Path.Combine(_root, "docs", "balance.md")))
            _root = Path.GetDirectoryName(_root);
        if (_root is null)
        {
            Console.WriteLine("wound2: リポジトリ直下から実行すること（`docs/balance.md` が見つからない）。"
                + " **走査が空なら止める**（第117期の器具の事故）。");
            return false;
        }
        return true;
    }

    static string Src(string file) => File.ReadAllText(Path.Combine(_root!, "BattleCore", file));

    static WoundRule V0 => new(true, true);      // 現行（在庫の走査あり）
    static WoundRule VOff => new(false, false);  // 傷を丸ごと外した対照（**設計案ではない**）

    // 行（`compare` 61 ＋ 交差帯 12）。
    static (string Name, Formation F, bool Cross)[] Rows()
        => Presets.Compare.Select(b => (b.Name, b.F, false))
            .Concat(Presets.Cross.Select(b => (b.Name, b.F, true))).ToArray();

    // ==================================================================================
    // 集計器。**1戦ぶんの `WoundLedger` を足し込むだけ。**
    // ==================================================================================
    sealed class Agg
    {
        public readonly long[] WAlly = new long[BattleContext.WoundRouteCount];
        public readonly long[] WFoe = new long[BattleContext.WoundRouteCount];
        public readonly long[] Loss = new long[BattleContext.WoundLossCount];
        public readonly long[] LossAlly = new long[BattleContext.WoundLossCount];
        public long StockTurns, StockAlly, StockFoe, TurnsAllyAny, TurnsFoeAny;
        public int AllyMax, FoeMax;
        public readonly long[] DepthAlly = new long[3];
        public readonly long[] DepthFoe = new long[3];
        public long LagSum, LagCount, HpRemoved;
        public readonly long[] RFires = new long[6];
        public readonly long[] RWounds = new long[6];
        public readonly long[] RNom = new long[6];
        public readonly long[] REff = new long[6];
        public readonly long[] GFires = new long[5];
        public readonly long[] GTgt = new long[5];
        public readonly long[] GAllySum = new long[5];
        public readonly long[] GAny = new long[5];
        public long Battles, Turns, Wins;

        public void Add(BattleResult r)
        {
            WoundLedger w = r.Wounds;
            for (int i = 0; i < WAlly.Length; i++) { WAlly[i] += w.WriteAlly[i]; WFoe[i] += w.WriteFoe[i]; }
            for (int i = 0; i < Loss.Length; i++) { Loss[i] += w.LossAll[i]; LossAlly[i] += w.LossAlly[i]; }
            StockTurns += w.StockTurns; StockAlly += w.StockAllySum; StockFoe += w.StockFoeSum;
            TurnsAllyAny += w.TurnsAllyAny; TurnsFoeAny += w.TurnsFoeAny;
            if (w.StockAllyMax > AllyMax) AllyMax = w.StockAllyMax;
            if (w.StockFoeMax > FoeMax) FoeMax = w.StockFoeMax;
            for (int i = 0; i < 3; i++) { DepthAlly[i] += w.DepthAlly[i]; DepthFoe[i] += w.DepthFoe[i]; }
            LagSum += w.LagSum; LagCount += w.LagCount; HpRemoved += w.HpRemoved;
            for (int i = 0; i < 6; i++)
            {
                RFires[i] += w.ReadFires[i]; RWounds[i] += w.ReadWounds[i];
                RNom[i] += w.ReadNominal[i]; REff[i] += w.ReadEffective[i];
            }
            for (int i = 0; i < 5; i++)
            {
                GFires[i] += w.GuardFires[i]; GTgt[i] += w.GuardTargetWounded[i];
                GAllySum[i] += w.GuardWoundedAllySum[i]; GAny[i] += w.GuardAnyWoundedAlly[i];
            }
            Battles++; Turns += r.Turns; if (r.PlayerWon) Wins++;
        }

        public long Written { get { long t = 0; for (int i = 0; i < WAlly.Length; i++) t += WAlly[i] + WFoe[i]; return t; } }
        public long WrittenAlly { get { long t = 0; foreach (long v in WAlly) t += v; return t; } }
        public long Accounted { get { long t = 0; foreach (long v in Loss) t += v; return t; } }
        /// <summary>読まれないまま落ちた傷（駒の死 ＋ 決着）。</summary>
        public long Unread => Loss[(int)WoundLoss.Death] + Loss[(int)WoundLoss.End];
        public long ReadWoundsTotal { get { long t = 0; foreach (long v in RWounds) t += v; return t; } }
    }

    // ==================================================================================
    // Phase 0 —— §1。**戦闘0回**（情報帯の片側は `docs/balance.md` から引く）。
    // ==================================================================================
    static void Phase0()
    {
        Console.WriteLine("# 第120期 Phase 0 —— 傷に関わる実装の全列挙（表P・**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("**enum のコメントではなく実装の分岐から引いている**（第119期・68 枚中 31 枚で");
        Console.WriteLine("コメントと実装が食い違っていた）。**走査件数を出力し、0 件なら止める**（第117・118期）。");
        Console.WriteLine();

        string traits = Src("Traits.cs"), engine = Src("BattleEngine.cs"), models = Src("Models.cs");
        string engagement = Src("Engagement.cs");

        // --- P-1 書き手 --------------------------------------------------------------------
        var writes = new List<(string Where, string Route, string Line)>();
        foreach ((string file, string src) in new[] { ("Traits.cs", traits), ("BattleEngine.cs", engine) })
            foreach (Match m in Regex.Matches(src, @"^(?!\s*///).*\bWound\([^;]*?WoundRoute\.(?<r>\w+)\).*$",
                                              RegexOptions.Multiline))
                writes.Add((file, m.Groups["r"].Value, m.Value.Trim()));

        Console.WriteLine($"## P-1 書き手（`BattleContext.Wound` の呼び出し）—— **{writes.Count} 件**");
        Console.WriteLine();
        if (writes.Count == 0) { Console.WriteLine("**走査が空。止める。**"); return; }
        Console.WriteLine("| # | ファイル | 経路 | 行 |");
        Console.WriteLine("|--:|---|---|---|");
        for (int i = 0; i < writes.Count; i++)
            Console.WriteLine($"| {i + 1} | `{writes[i].Where}` | `{writes[i].Route}` | `{Trim(writes[i].Line)}` |");
        Console.WriteLine();

        string[] routes = Enum.GetNames(typeof(WoundRoute));
        var used = writes.Select(w => w.Route).ToHashSet(StringComparer.Ordinal);
        Console.WriteLine($"**§1 の 3 —— `WoundRoute` の全経路が計数されているか**"
            + $"（enum {routes.Length} 本／書き手のある経路 {used.Count} 本）:");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 書き手 | 既定で走るか |");
        Console.WriteLine("|---|:-:|---|");
        string[] defOn =
        {
            "○（キリ）", "○（ノミ）", "○（`SpillWoundRule { Enabled = True }`・第85期）",
            "×（`ThornRule { Wound = None }`・第84期の残置）", "×（同上）",
            "○（`GatherRule { Enabled = True }`・第89期）。**中継**"
        };
        for (int i = 0; i < routes.Length; i++)
            Console.WriteLine($"| `{routes[i]}` | {(used.Contains(routes[i]) ? "○" : "×")} | {defOn[i]} |");
        Console.WriteLine();
        Console.WriteLine("**抜けている経路は無い**——加算の窓口は1本だけなので、"
            + "`WoundRoute` の全数がそのまま供給の全数になる（第93期の設計）。");
        Console.WriteLine();

        // --- P-2 消す側 --------------------------------------------------------------------
        // **前後1行を窓にして帳簿の札を引く**——`NoteWoundLoss` は行の直前・直後どちらにも置かれている
        // （束ねは直前・断ちは直後）。**行だけを見ると「札が無い」と誤って読む。**
        var drops = new List<(string Where, string Line, string Tag)>();
        foreach ((string file, string src) in new[] { ("Traits.cs", traits), ("BattleEngine.cs", engine), ("Engagement.cs", engagement) })
        {
            string[] ls = src.Split((char)10).Select(x => x.TrimEnd((char)13)).ToArray();
            for (int i = 0; i < ls.Length; i++)
            {
                string t = ls[i].TrimStart();
                if (t.StartsWith("///")) continue;
                bool hit = ls[i].Contains("SetCounter(StatusKeys.Wound")
                        || ls[i].Contains("Counters.Remove(key)");
                if (!hit) continue;
                string win = string.Join(" ", ls.Skip(Math.Max(0, i - 1)).Take(3));
                string tag = win.Contains("WoundLoss.") ? "`" + Regex.Match(win, @"WoundLoss\.\w+").Value + "`"
                           : ls[i].Contains("Wound, w)") ? "**加算**（`WoundRoute`）"
                           : ls[i].Contains("Counters.Remove") ? "`WoundLoss.Carry`（会戦のみ・単発では 0）"
                           : "**—（帳簿に載っていない）**";
                drops.Add((file, ls[i].Trim(), tag));
            }
        }

        Console.WriteLine($"## P-2 傷の counter を書き換える箇所（**減算には窓口が無い**）—— **{drops.Count} 件**");
        Console.WriteLine();
        if (drops.Count == 0) { Console.WriteLine("**走査が空。止める。**"); return; }
        Console.WriteLine("| # | ファイル | 帳簿 | 行 |");
        Console.WriteLine("|--:|---|---|---|");
        for (int i = 0; i < drops.Count; i++)
            Console.WriteLine($"| {i + 1} | `{drops[i].Where}` | {drops[i].Tag} | `{Trim(drops[i].Line)}` |");
        Console.WriteLine();
        Console.WriteLine("**加算1・減算5・境界の掃除1。** 第120期の帳簿はこの全数の上に置いてある"
            + "（自己検査 (a) が閉じることがその証明）。");
        Console.WriteLine();

        // --- P-3 読み手 --------------------------------------------------------------------
        var reads = new List<(string Where, string Line)>();
        foreach ((string file, string src) in new[] { ("Traits.cs", traits), ("BattleEngine.cs", engine) })
            foreach (Match m in Regex.Matches(src,
                @"^(?!\s*//)(?!.*NoteWoundRead)(?!.*SetCounter)(?!\s*public (?:int|bool) ).*(?:WoundDepthOf\(|IsWounded\(|Counter\(StatusKeys\.Wound\)).*$",
                RegexOptions.Multiline))
                reads.Add((file, m.Value.Trim()));

        Console.WriteLine($"## P-3 傷を読む行の全数 —— **{reads.Count} 件**");
        Console.WriteLine();
        if (reads.Count == 0) { Console.WriteLine("**走査が空。止める。**"); return; }
        Console.WriteLine("| # | ファイル | 行 |");
        Console.WriteLine("|--:|---|---|");
        for (int i = 0; i < reads.Count; i++)
            Console.WriteLine($"| {i + 1} | `{reads[i].Where}` | `{Trim(reads[i].Line)}` |");
        Console.WriteLine();
        Console.WriteLine("**この中には計数だけの行も入っている**（`WoundsAtDeath` / `WoundsAtEnd` / 滲み則の観測）。");
        Console.WriteLine("**盤面を分岐させる読み手は6枚**——抉り（エグ）／刻みのなぞり（ノミ）／断ち（ナタ）／");
        Console.WriteLine("縫い（ハリ）／繕い（ノノ・第92期に採用）／滲み則（engine・第90期）。");
        Console.WriteLine("薄刃（`ThinBladeCost.Unwounded`）と深手（`DeepRule`）は**既定では読まない**。");
        Console.WriteLine();

        // --- P-4 記録 ----------------------------------------------------------------------
        // **宣言行を除く**（`private static void NoteWoundWriter(` は呼び出しではない）。
        int noteCalls = Regex.Matches(engine, @"(?<!void )NoteWoundWriter\(").Count;
        int declared = Regex.Matches(models, @"WoundWriters").Count;
        int refs = Regex.Matches(engine, @"\.WoundWriters\b").Count + Regex.Matches(engagement, @"\.WoundWriters\b").Count;
        Console.WriteLine("## P-4 記録（`UnitState.WoundWriters`・第104期）—— **今も取られているか**");
        Console.WriteLine();
        Console.WriteLine($"- 宣言: `Models.cs` に {declared} 件");
        Console.WriteLine($"- 書き込み: `NoteWoundWriter(` **{noteCalls} 箇所**（`BattleContext.Wound` の中・**版に依らない**）");
        Console.WriteLine($"- 参照: `.WoundWriters` {refs} 箇所");
        Console.WriteLine();
        Console.WriteLine(noteCalls > 0
            ? "**取られている。** したがって §5 の落とし穴の対策 (2)「自分で書いた傷は読まない」は"
              + "**実装可能**——`WoundWriters` に自陣の駒しかいない傷を弾けばよい。"
            : "**取られていない。** 対策 (2) は記録を作るところから始まる。");
        Console.WriteLine();

        // --- P-5 情報帯の screen（片側）-----------------------------------------------------
        Console.WriteLine("## P-5 判定に使うセルが情報帯に入るか（§1 の 4・第118期の7例目）");
        Console.WriteLine();
        ScreenFromBalance();

        // --- P-6 過去に測った期 --------------------------------------------------------------
        Console.WriteLine("## P-6 過去に傷の在庫を測った期（`design/` の grep）");
        Console.WriteLine();
        var hits = new List<(string F, int N)>();
        foreach (string f in Directory.GetFiles(Path.Combine(_root!, "design"), "*.md")
                                      .OrderBy(x => x, StringComparer.Ordinal))
        {
            int n = Regex.Matches(File.ReadAllText(f), @"傷.{0,8}在庫|在庫.{0,8}傷").Count;
            if (n > 0) hits.Add((Path.GetFileName(f), n));
        }
        Console.WriteLine("| ファイル | 「傷 × 在庫」の共起 |");
        Console.WriteLine("|---|--:|");
        foreach (var h in hits.OrderByDescending(x => x.N)) Console.WriteLine($"| `design/{h.F}` | {h.N} |");
        Console.WriteLine();
        Console.WriteLine($"**{hits.Count} ファイル。** 第73期（`wound`）が「在庫の収支」を出しているが、"
            + "**台は理想台とドラフト台で `Presets` ではない**——編成に依る在庫の分布は測っていない。");
        Console.WriteLine("第107期は「傷の 90.5% は味方側」を出しているが、**その後に第108期（ハリ→トモ）と");
        Console.WriteLine("第111期（交差帯の終端をノミへ）が入っている**ので、定義を実装から取り直して測り直す（§2-2）。");
    }

    static string Trim(string s) => s.Length <= 100 ? s : s.Substring(0, 98) + "…";

    /// <summary>`docs/balance.md` の現行のセルの床・天井を数える（**戦闘0回**）。</summary>
    static void ScreenFromBalance()
    {
        int rows = 0, cells = 0, floor = 0, ceil = 0, info = 0;
        foreach (double[] v in BalanceCells().Values)
        {
            rows++;
            for (int w = 1; w < 5; w++)   // 第2〜5波（(G10)）
            {
                cells++;
                if (v[w] <= 0.0) floor++;
                else if (v[w] >= 100.0) ceil++;
                else info++;
            }
        }
        Console.WriteLine($"`docs/balance.md` の {rows} 行 × 第2〜5波 ＝ **{cells} セル**:");
        Console.WriteLine();
        Console.WriteLine("| | セル | 割合 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 床（0.0%） | {floor} | {floor * 100.0 / cells:F1}% |");
        Console.WriteLine($"| 天井（100.0%） | {ceil} | {ceil * 100.0 / cells:F1}% |");
        Console.WriteLine($"| **情報帯（0 < x < 100）** | **{info}** | **{info * 100.0 / cells:F1}%** |");
        Console.WriteLine();
        Console.WriteLine("**Q4（傷を無効化したときの帰属）は差を読むので、現行が床でも天井でも対照が動けば差は出る。**");
        Console.WriteLine("原理的に 0.0pt にしかならないのは**現行と対照の両方が床／両方が天井**のセルだけで、");
        Console.WriteLine("**そのふるいは対照を回してからでないと当てられない**（`wound2 run` の表D-2 が出す）。");
        Console.WriteLine("ここで出せるのは片側だけである——**第118期の穴は「片方の顔が見えない」ことだった。**");
        Console.WriteLine();
    }

    static Dictionary<string, double[]> BalanceCells()
    {
        var want = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (string ln in File.ReadAllLines(Path.Combine(_root!, "docs", "balance.md")))
        {
            if (!ln.StartsWith("| ") || ln.Contains("---") || ln.Contains("第1波 |")) continue;
            string[] c = ln.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            if (c.Length < 6) continue;
            var v = new List<double>();
            for (int i = 1; i < c.Length; i++)
                if (double.TryParse(c[i].TrimEnd('%'), out double d)) v.Add(d);
            if (v.Count >= 5) want[c[0]] = v.ToArray();
        }
        return want;
    }

    // ==================================================================================
    // run —— 表A〜E
    // ==================================================================================
    static void RunTables()
    {
        var rows = Rows();
        var all = new Agg();
        var perRow = new Agg[rows.Length];
        var w0 = new double[rows.Length][];
        var w1 = new double[rows.Length][];

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int r = 0; r < rows.Length; r++)
        {
            perRow[r] = new Agg();
            w0[r] = new double[Waves.Length];
            w1[r] = new double[Waves.Length];
            for (int wi = 0; wi < Waves.Length; wi++)
            {
                Formation foe = EnemyCatalog.Stages[Waves[wi]].Enemy;
                int a = 0, b = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r0 = BattleEngine.Run(rows[r].F, foe, seed, verbose: false, wound: V0);
                    if (r0.PlayerWon) a++;
                    all.Add(r0); perRow[r].Add(r0);
                    if (BattleEngine.Run(rows[r].F, foe, seed, verbose: false, wound: VOff).PlayerWon) b++;
                }
                w0[r][wi] = a * 100.0 / Seeds;
                w1[r][wi] = b * 100.0 / Seeds;
            }
        }
        sw.Stop();

        Console.WriteLine("# 第120期 —— 傷という通貨の棚卸し（表A〜E）");
        Console.WriteLine();
        Console.WriteLine($"台: `compare` {Presets.Compare.Length} 行 ＋ 交差帯 {Presets.Cross.Length} 行 ＝ **{rows.Length} 行** "
            + $"× 第2〜5波 × seed 0..{Seeds - 1}（規約 (G10)・(G14)）。");
        Console.WriteLine($"**{all.Battles:N0} 戦**（現行）＋ 同数の対照（傷を無効）。所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
        Console.WriteLine();
        Console.WriteLine("**`Presets` をそのまま使う**——在庫は編成に依るので、ローカル台では在庫の分布が測れない。");
        Console.WriteLine("**傷を含まない行も分母に入れる**（規約 (G4)）。「何行で傷が書かれるか」自体が結果である。");
        Console.WriteLine();

        TableA(all, rows, perRow);
        TableB(all);
        TableC(all);
        TableD(rows, perRow, w0, w1);
        TableE(all);
    }

    // --- 表A W1 在庫 -------------------------------------------------------------------
    static void TableA(Agg a, (string Name, Formation F, bool Cross)[] rows, Agg[] per)
    {
        double n = a.Battles;
        Console.WriteLine("## 表A —— W1 在庫（傷はどちら側に・誰に・何個 書かれるか）");
        Console.WriteLine();
        Console.WriteLine("### A-1 書かれた傷（経路別 × 陣営別・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("**陣営は「書かれた側」で割る**（書き手の帰属は `UnitTally.WoundWritesByRoute` の側にある）。");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 味方側の駒へ | 敵側の駒へ | 計 | 味方比 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int i = 0; i < RouteName.Length; i++)
        {
            long t = a.WAlly[i] + a.WFoe[i];
            Console.WriteLine($"| {RouteName[i]} | {a.WAlly[i] / n:F3} | {a.WFoe[i] / n:F3} | {t / n:F3} | "
                + (t == 0 ? "—（既定で無効／保持者なし）" : $"{a.WAlly[i] * 100.0 / t:F1}%") + " |");
        }
        long tot = a.Written, ally = a.WrittenAlly;
        Console.WriteLine($"| **計** | **{ally / n:F3}** | **{(tot - ally) / n:F3}** | **{tot / n:F3}** | "
            + $"**{(tot == 0 ? 0 : ally * 100.0 / tot):F1}%** |");
        Console.WriteLine();
        Console.WriteLine($"> **第107期の 90.5%（味方側の比率）に対して、今は {(tot == 0 ? 0 : ally * 100.0 / tot):F1}%。**");
        Console.WriteLine();

        Console.WriteLine("### A-2 在庫の厚み（ターン頭の走査）");
        Console.WriteLine();
        double st = Math.Max(1, a.StockTurns);
        Console.WriteLine("| | 味方 | 敵 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 傷を持つ駒の同時存在数（平均/T） | {a.StockAlly / st:F3} | {a.StockFoe / st:F3} |");
        Console.WriteLine($"| 同・最大 | {a.AllyMax} | {a.FoeMax} |");
        Console.WriteLine($"| 傷持ちが1体でもいたターンの割合 | {a.TurnsAllyAny * 100.0 / st:F1}% | {a.TurnsFoeAny * 100.0 / st:F1}% |");
        Console.WriteLine();
        Console.WriteLine($"走査したターンの延べ = {a.StockTurns:N0}（決着 {a.Turns / n:F2} T/戦）。");
        Console.WriteLine();

        Console.WriteLine("### A-3 深さの分布（走査の延べ・傷持ちの駒だけが分母）");
        Console.WriteLine();
        long da = a.DepthAlly.Sum(), df = a.DepthFoe.Sum();
        string[] dn = { "1", "2", "3 以上" };
        Console.WriteLine("| 深さ | 味方 | 敵 |");
        Console.WriteLine("|---|--:|--:|");
        for (int i = 0; i < 3; i++)
            Console.WriteLine($"| {dn[i]} | {(da == 0 ? 0 : a.DepthAlly[i] * 100.0 / da):F1}% | "
                + $"{(df == 0 ? 0 : a.DepthFoe[i] * 100.0 / df):F1}% |");
        Console.WriteLine();
        Console.WriteLine("**断ちの閾値は 2**（`SeverRule { Threshold = 2 }`）——深さ 1 の割合がそのまま"
            + "「断ちが下りない在庫」の割合になる。");
        Console.WriteLine();

        Console.WriteLine("### A-4 行ごとの分散（同じ駒が編成で別物になっているか・第115期 Q2 と同じ問い）");
        Console.WriteLine();
        var vals = new List<(string Name, double V)>();
        for (int r = 0; r < rows.Length; r++)
            vals.Add((rows[r].Name, per[r].Written / (double)Math.Max(1, per[r].Battles)));
        vals.Sort((x, y) => y.V.CompareTo(x.V));
        int wrote = vals.Count(v => v.V > 0.0005);
        Console.WriteLine($"**傷が1個でも書かれた行: {wrote} / {rows.Length}**（書かれなかった行 {rows.Length - wrote}）。");
        Console.WriteLine();
        Console.WriteLine("| 順 | 行 | 書かれた傷/戦 |");
        Console.WriteLine("|--:|---|--:|");
        for (int i = 0; i < Math.Min(12, vals.Count); i++)
            Console.WriteLine($"| {i + 1} | {vals[i].Name} | {vals[i].V:F2} |");
        Console.WriteLine("| … | | |");
        var low = vals.Where(v => v.V > 0.0005).ToArray();
        for (int i = Math.Max(0, low.Length - 4); i < low.Length; i++)
            Console.WriteLine($"| {vals.FindIndex(x => x.Name == low[i].Name) + 1} | {low[i].Name} | {low[i].V:F2} |");
        Console.WriteLine();
    }

    // --- 表B W2 消滅 -------------------------------------------------------------------
    static void TableB(Agg a)
    {
        double n = a.Battles;
        Console.WriteLine("## 表B —— W2 消滅の内訳（**帳簿が閉じることが Q1**）");
        Console.WriteLine();
        Console.WriteLine("| 経路 | /戦 | 割合 | うち味方側の駒から |");
        Console.WriteLine("|---|--:|--:|--:|");
        long acc = a.Accounted;
        for (int i = 0; i < LossName.Length; i++)
            Console.WriteLine($"| {LossName[i]} | {a.Loss[i] / n:F3} | {(acc == 0 ? 0 : a.Loss[i] * 100.0 / acc):F1}% | {a.LossAlly[i] / n:F3} |");
        Console.WriteLine($"| **計** | **{acc / n:F3}** | **100.0%** | **{a.LossAlly.Sum() / n:F3}** |");
        Console.WriteLine();
        Console.WriteLine($"**書かれた傷 {a.Written:N0}（{a.Written / n:F3}/戦）／帳簿の右辺 {acc:N0}（{acc / n:F3}/戦）"
            + $"—— 差 {a.Written - acc}。**");
        Console.WriteLine();
        Console.WriteLine(a.Written == acc
            ? "> **Q1 ○。完全一致。** 加算は窓口1本・減算は全数当たってあるので、経路の抜けは無い。"
            : "> **Q1 ×。一致しない。経路の抜けがある。**");
        Console.WriteLine();
        double unread = acc == 0 ? 0 : a.Unread * 100.0 / acc;
        Console.WriteLine($"> **読まれずに消えた割合 = {unread:F1}%**"
            + $"（駒の死 {(acc == 0 ? 0 : a.Loss[(int)WoundLoss.Death] * 100.0 / acc):F1}%"
            + $" ＋ 決着 {(acc == 0 ? 0 : a.Loss[(int)WoundLoss.End] * 100.0 / acc):F1}%）。");
        Console.WriteLine("> **これが高いなら、値段ではなく到達の問題である。**");
        Console.WriteLine();
        Console.WriteLine("**注意: 「消えた」と「読まれた」は別の量。** 抉り（エグ）と刻みのなぞり（ノミ）は"
            + "**傷を消費しない読み手**なので、読まれた傷はこの表のどこにも現れない");
        Console.WriteLine("——読まれたうえで最後まで残り、`読まれず決着` / `読まれず駒が死亡` に落ちる。"
            + "**読まれた回数は表C の側にある。**");
        Console.WriteLine();
        if (a.LagCount > 0)
            Console.WriteLine($"**傷が書かれてから読まれるまで: 平均 {a.LagSum / (double)a.LagCount:F2} ターン**"
                + $"（読まれた延べ {a.LagCount:N0} 回）。");
        else Console.WriteLine("**傷は1度も読まれていない**（齢の分母が 0）。");
        Console.WriteLine();
        Console.WriteLine("**`会戦の掃除` は単発の台では構造的に 0**（境界を1度も跨がない）。"
            + "会戦での掃除は `UnitTally.CarryWound` の側にあり、この期の分母ではない。");
        Console.WriteLine();
    }

    // --- 表C W3 実効単価 ---------------------------------------------------------------
    static void TableC(Agg a)
    {
        double n = a.Battles;
        Console.WriteLine("## 表C —— W3 実効単価（傷1つが実際に何点に化けているか）");
        Console.WriteLine();
        Console.WriteLine("**名目 = 定数 × 読んだ傷の数。実効 = 盤面から実際に減った HP（回復側は実際に癒えた量）。**");
        Console.WriteLine("差はオーバーキル・肩代わり・満タン・渇きで消えたぶんである。");
        Console.WriteLine();
        int[] konst = { GougeTrait.PerWound, CarveTrait.PerWound, SeverTrait.PerWound,
                        SutureTrait.PerWound, MenderTrait.PerWound, 1 };
        Console.WriteLine("| 読み手 | 定数 | 発火/戦 | 読んだ傷/戦 | 名目/戦 | 実効/戦 | 実効÷名目 | 実効÷読んだ傷 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < 6; i++)
        {
            bool soak = i == 5;
            Console.WriteLine($"| {ReaderName[i]} | +{konst[i]}{(soak ? "（毒T）" : "")} | {a.RFires[i] / n:F3} | {a.RWounds[i] / n:F3} | {a.RNom[i] / n:F3} | "
                + (soak ? "—" : $"{a.REff[i] / n:F3}") + " | "
                + (soak || a.RNom[i] == 0 ? "—" : $"{a.REff[i] * 100.0 / a.RNom[i]:F1}%") + " | "
                + (soak || a.RWounds[i] == 0 ? "—" : $"{a.REff[i] / (double)a.RWounds[i]:F2}") + " |");
        }
        Console.WriteLine();
        long nomD = a.RNom[0] + a.RNom[1] + a.RNom[2];
        long effD = a.REff[0] + a.REff[1] + a.REff[2];
        long wD = a.RWounds[0] + a.RWounds[1] + a.RWounds[2];
        long nomH = a.RNom[3] + a.RNom[4], effH = a.REff[3] + a.REff[4];
        long wH = a.RWounds[3] + a.RWounds[4];
        Console.WriteLine("**与ダメ側（抉り・なぞり・断ち）と回復側（縫い・繕い）は単位が違うので分けて読む。**");
        Console.WriteLine("**滲み則だけは出力が毒の残ターン**なので実効の列に載せていない（単価の分子から外す）。");
        Console.WriteLine();
        Console.WriteLine("| | 名目/戦 | 実効/戦 | 読んだ傷/戦 | 実効÷読んだ傷 | **実効÷書かれた傷** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| 与ダメ側 | {nomD / n:F3} | {effD / n:F3} | {wD / n:F3} | "
            + (wD == 0 ? "—" : $"{effD / (double)wD:F2}") + " | "
            + (a.Written == 0 ? "—" : $"**{effD / (double)a.Written:F2}**") + " |");
        Console.WriteLine($"| 回復側 | {nomH / n:F3} | {effH / n:F3} | {wH / n:F3} | "
            + (wH == 0 ? "—" : $"{effH / (double)wH:F2}") + " | "
            + (a.Written == 0 ? "—" : $"{effH / (double)a.Written:F2}") + " |");
        Console.WriteLine();
        Console.WriteLine($"> **傷1つの実効単価（与ダメ側）= {(a.Written == 0 ? 0 : effD / (double)a.Written):F2} 点。**"
            + $" 定数は抉り +{GougeTrait.PerWound} / なぞり +{CarveTrait.PerWound} / 断ち +{SeverTrait.PerWound}。");
        Console.WriteLine("> **書かれた傷を分母に取ると、読まれなかったぶんがそのまま単価を割る。**");
        Console.WriteLine();
        Console.WriteLine($"参考: 盤面から実際に減った HP の総量 = {a.HpRemoved / n:F1}/戦。"
            + $"**傷由来の与ダメはそのうち {(a.HpRemoved == 0 ? 0 : effD * 100.0 / a.HpRemoved):F2}%。**");
        Console.WriteLine();

        HariBench();
    }

    /// <summary>ハリ（縫い）は `Presets` に不在（第108期にロスターから外れた）ので、参考台を1つ組む。</summary>
    static void HariBench()
    {
        Console.WriteLine("### C-2 参考: ハリ（縫い）の台を1つ組む（**`Presets` に不在**）");
        Console.WriteLine();
        Console.WriteLine("交差帯の `傷×被弾 (カド×ノミ×ノノ)` の**終端の1枚だけ**をハリに戻した版。");
        Console.WriteLine("**席も他の4枚も1つも動かしていない**（第111期の差し替えを1枚だけ戻した形）。");
        Console.WriteLine("**`Presets` は1行も触っていない。台は診断のローカル。**");
        Console.WriteLine();
        Formation nono = Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Kado,
            center: UnitCatalog.Nono, back1: UnitCatalog.Nomi, back3: UnitCatalog.Egu);
        Formation hari = Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Kado,
            center: UnitCatalog.Hari, back1: UnitCatalog.Nomi, back3: UnitCatalog.Egu);
        Console.WriteLine("| 版 | 勝率（第2〜5波） | 書かれた傷/戦 | 読まれず/戦 | 縫い 敵から | 縫い 味方から | 塞ぎ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach ((string nm, Formation f) in new[] { ("終端 ノノ（＝現行の交差帯）", nono), ("終端 ハリ（参考）", hari) })
        {
            var g = new Agg();
            double wins = 0; long sFoe = 0, sAlly = 0;
            foreach (int wv in Waves)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[wv].Enemy, seed, verbose: false, wound: V0);
                    g.Add(r); if (r.PlayerWon) wins++;
                    foreach (UnitTally t in r.TallyByUnit.Values) { sFoe += t.SutureFoe; sAlly += t.SutureAlly; }
                }
            double m = g.Battles;
            Console.WriteLine($"| {nm} | {wins * 100.0 / m:F1}% | {g.Written / m:F2} | {g.Unread / m:F2} | "
                + $"{sFoe / m:F2} | {sAlly / m:F2} | "
                + $"{(g.Loss[(int)WoundLoss.SutureSeal] + g.Loss[(int)WoundLoss.MendSeal]) / m:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **第107期の「敵から糸を引く回数 0.53 → 0.00」はこの `縫い 敵から` の列。**");
        Console.WriteLine("> ハリは `compare` 61 行にも交差帯 12 行にも1行も無いので、**主表の分母では常に 0.00**。");
        Console.WriteLine();
    }

    // --- 表D W4 判断 -------------------------------------------------------------------
    static void TableD((string Name, Formation F, bool Cross)[] rows, Agg[] per, double[][] w0, double[][] w1)
    {
        Console.WriteLine("## 表D —— W4 鎖が繋がっている行と、傷を丸ごと外したときの帰属");
        Console.WriteLine();

        string[] writerIds = { UnitCatalog.Kiri.Id, UnitCatalog.Nomi.Id };
        string[] readerIds =
        {
            UnitCatalog.Egu.Id, UnitCatalog.Nomi.Id, UnitCatalog.Nata.Id,
            UnitCatalog.Hari.Id, UnitCatalog.Nono.Id, UnitCatalog.Mio.Id
        };
        int hasW = 0, hasR = 0, hasBoth = 0, wrote = 0, read = 0, chain = 0;
        var chainRows = new List<(string Name, double W, double R)>();
        var attr = new List<(string Name, double Avg, double Best, int BestWave, double Worst, int WorstWave, bool Screened, bool Chain)>();

        for (int r = 0; r < rows.Length; r++)
        {
            var ids = rows[r].F.Occupied().Select(u => u.Def.Id).ToHashSet(StringComparer.Ordinal);
            bool defW = writerIds.Any(ids.Contains), defR = readerIds.Any(ids.Contains);
            if (defW) hasW++;
            if (defR) hasR++;
            if (defW && defR) hasBoth++;

            double bat = Math.Max(1, per[r].Battles);
            long wr = per[r].Written, rd = per[r].ReadWoundsTotal;
            if (wr > 0) wrote++;
            if (rd > 0) read++;
            bool ch = wr > 0 && rd > 0;
            if (ch) { chain++; chainRows.Add((rows[r].Name, wr / bat, rd / bat)); }

            // **帰属 = 現行 − 無効**（正なら「傷があるほうが強い」）。
            // Q4 の線は「**無効にすると落ちる**」なので、**帰属が正の側**を見る（符号を取り違えない）。
            double sum = 0, best = 0, worst = 0; int bw = 0, ww = 0; bool screened = true;
            for (int i = 0; i < Waves.Length; i++)
            {
                double d = w0[r][i] - w1[r][i];
                sum += d;
                if (d > best) { best = d; bw = Waves[i] + 1; }
                if (d < worst) { worst = d; ww = Waves[i] + 1; }
                bool bothFloor = w0[r][i] <= 0.0 && w1[r][i] <= 0.0;
                bool bothCeil = w0[r][i] >= 100.0 && w1[r][i] >= 100.0;
                if (!bothFloor && !bothCeil) screened = false;
            }
            attr.Add((rows[r].Name, sum / Waves.Length, best, bw, worst, ww, screened, ch));
        }

        Console.WriteLine("### D-1 鎖が繋がっている行（Q3）");
        Console.WriteLine();
        Console.WriteLine($"| | 行数 | / {rows.Length} |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 傷の書き手（キリ・ノミ）が**在席** | {hasW} | {hasW * 100.0 / rows.Length:F1}% |");
        Console.WriteLine($"| 傷の読み手（エグ・ノミ・ナタ・ハリ・ノノ・ミオ）が**在席** | {hasR} | {hasR * 100.0 / rows.Length:F1}% |");
        Console.WriteLine($"| 両方が在席 | {hasBoth} | {hasBoth * 100.0 / rows.Length:F1}% |");
        Console.WriteLine($"| **傷が1個でも書かれた**（実測） | **{wrote}** | {wrote * 100.0 / rows.Length:F1}% |");
        Console.WriteLine($"| **傷が1個でも読まれた**（実測） | **{read}** | {read * 100.0 / rows.Length:F1}% |");
        Console.WriteLine($"| **両方 ＝ 鎖が繋がっている** | **{chain}** | **{chain * 100.0 / rows.Length:F1}%** |");
        Console.WriteLine();
        Console.WriteLine("**在席で数えると読み手6枚の顔ぶれしか見えない。実測で数えると巻き込み則（engine 側）が入る**");
        Console.WriteLine("——だから2つを並べてある（第39期「駒の説明文から数えると必ず抜ける」の実装版）。");
        Console.WriteLine();
        Console.WriteLine("**`読÷書` が 100% を超える行がある**——抉りとなぞりは**消費しない読み手**なので、");
        Console.WriteLine("同じ傷を何度も読む。**「読まれた傷」は延べであって在庫ではない。**");
        Console.WriteLine();
        Console.WriteLine("| # | 行 | 書/戦 | 読/戦 | 読÷書 |");
        Console.WriteLine("|--:|---|--:|--:|--:|");
        int k = 0;
        foreach (var c in chainRows.OrderByDescending(x => x.R))
            Console.WriteLine($"| {++k} | {c.Name} | {c.W:F2} | {c.R:F2} | {(c.W == 0 ? 0 : c.R * 100.0 / c.W):F1}% |");
        Console.WriteLine();

        Console.WriteLine("### D-2 傷を丸ごと外したときの帰属（Q4・`WoundRule(Enabled: false)`）");
        Console.WriteLine();
        Console.WriteLine("**帰属 = 現行 − 無効。正なら「傷があるほうが強い」＝ 外すと落ちる。**");
        Console.WriteLine();
        int screenedN = attr.Count(x => x.Screened);
        Console.WriteLine($"**情報帯の screen**: 現行と対照が**両方とも床／両方とも天井**の行 = **{screenedN} 行**"
            + $"（判定の分母 **{rows.Length - screenedN} 行**）。第118期の7例目——**片方の顔が見えない行を分母に入れない。**");
        Console.WriteLine();
        var drops = attr.Where(x => !x.Screened && x.Best >= AttrLine).OrderByDescending(x => x.Best).ToArray();
        Console.WriteLine($"**Q4 —— 傷を外すといずれかの波で {AttrLine:F1}pt 以上落ちる行: {drops.Length} 行**");
        Console.WriteLine();
        if (drops.Length > 0)
        {
            Console.WriteLine("| 行 | 最大の落差 | その波 | 第2〜5波の平均 | 鎖 |");
            Console.WriteLine("|---|--:|--:|--:|:-:|");
            foreach (var d in drops)
                Console.WriteLine($"| {d.Name} | −{d.Best:F1} pt | 第{d.BestWave}波 | {d.Avg:+0.0;-0.0} pt | {(d.Chain ? "○" : "×")} |");
        }
        else Console.WriteLine("**1行も無い。**");
        Console.WriteLine();
        var gains = attr.Where(x => !x.Screened && x.Worst <= -AttrLine).OrderBy(x => x.Worst).ToArray();
        Console.WriteLine($"**逆側 —— 傷を外すといずれかの波で {AttrLine:F1}pt 以上<u>上がる</u>行: {gains.Length} 行**");
        Console.WriteLine("（＝**傷が正味の損になっている行**。巻き込み則が味方に 8 個/戦 書くので、これは驚きではない）");
        Console.WriteLine();
        if (gains.Length > 0)
        {
            Console.WriteLine("| 行 | 最大の上げ幅 | その波 | 第2〜5波の平均 | 鎖 |");
            Console.WriteLine("|---|--:|--:|--:|:-:|");
            foreach (var g in gains)
                Console.WriteLine($"| {g.Name} | +{-g.Worst:F1} pt | 第{g.WorstWave}波 | {g.Avg:+0.0;-0.0} pt | {(g.Chain ? "○" : "×")} |");
        }
        else Console.WriteLine("**1行も無い。**");
        Console.WriteLine();
        var live = attr.Where(x => !x.Screened).ToArray();
        Console.WriteLine($"**分母 {live.Length} 行の帰属の平均 = {live.Average(x => x.Avg):+0.00;-0.00} pt**"
            + $"（鎖のある {live.Count(x => x.Chain)} 行 {(live.Any(x => x.Chain) ? live.Where(x => x.Chain).Average(x => x.Avg) : 0):+0.00;-0.00} pt ／ "
            + $"鎖の無い {live.Count(x => !x.Chain)} 行 {(live.Any(x => !x.Chain) ? live.Where(x => !x.Chain).Average(x => x.Avg) : 0):+0.00;-0.00} pt）。");
        Console.WriteLine();
        Console.WriteLine("**動いた行の数**（|平均| が 0.05pt を超える行）= "
            + $"**{live.Count(x => Math.Abs(x.Avg) > 0.05)} / {live.Length}**。");
        Console.WriteLine();
    }

    // --- 表E 案Aの材料 -----------------------------------------------------------------
    static void TableE(Agg a)
    {
        double n = a.Battles;
        Console.WriteLine("## 表E —— 案A（庇いが傷を読む）の材料（**実装しない。数えるだけ**）");
        Console.WriteLine();
        Console.WriteLine("| 介入 | 発火/戦 | その瞬間の傷持ちの味方（平均） | 傷持ちの味方が1体以上 | **守った相手が傷持ちだった割合** |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int i = 0; i < 5; i++)
        {
            long f = a.GFires[i];
            Console.WriteLine($"| {GuardName[i]} | {f / n:F3} | " + (f == 0 ? "—" : $"{a.GAllySum[i] / (double)f:F2}")
                + " | " + (f == 0 ? "—" : $"{a.GAny[i] * 100.0 / f:F1}%")
                + " | " + (f == 0 ? "—" : $"**{a.GTgt[i] * 100.0 / f:F1}%**") + " |");
        }
        long tf = a.GFires.Sum(), tt = a.GTgt.Sum(), ta = a.GAny.Sum();
        Console.WriteLine($"| **計** | **{tf / n:F3}** | " + (tf == 0 ? "—" : $"{a.GAllySum.Sum() / (double)tf:F2}")
            + " | " + (tf == 0 ? "—" : $"{ta * 100.0 / tf:F1}%")
            + " | " + (tf == 0 ? "—" : $"**{tt * 100.0 / tf:F1}%**") + " |");
        Console.WriteLine();
        Console.WriteLine("> **3つ目（偶然の一致率）が既に高いなら、案Aを入れても盤面はほとんど変わらない。**");
        Console.WriteLine("> **2つ目が 0 なら、対象選択を変える余地がそもそも無い。**");
        Console.WriteLine();
        Console.WriteLine("**殉教は敵側の介入**なので 0.000（味方陣の介入だけを数えている）——"
            + "案Aは味方側の配置の話なので分母に入れない。**「数えた結果 0」であることを示すために列は残してある。**");
        Console.WriteLine();
    }

    // ==================================================================================
    // check —— 自己検査
    // ==================================================================================
    static void Check()
    {
        Console.WriteLine("# 第120期 —— 自己検査");
        Console.WriteLine();

        // --- 必須1 --------------------------------------------------------------------
        Console.WriteLine("## 必須1 —— `compare` 305 セルが `docs/balance.md` と 0 件");
        Console.WriteLine();
        var want = BalanceCells();
        int cells = 0, diff = 0;
        foreach (var (name, f) in Presets.Compare)
        {
            if (!want.TryGetValue(name, out double[]? exp)) { Console.WriteLine($"- **行名が引けない: {name}**"); continue; }
            for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
            {
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
                double got = wins * 100.0 / Seeds;
                cells++;
                if (Math.Abs(got - exp[st]) > 0.049)
                {
                    diff++;
                    Console.WriteLine($"- **差分: {name} 第{st + 1}波 {exp[st]:F1}% → {got:F1}%**");
                }
            }
        }
        Console.WriteLine($"- {cells} セル中 **{diff} 件**の差分。{(diff == 0 ? "**○**" : "**×**")}");
        Console.WriteLine();

        // --- (a)(b) 帳簿と対照 ------------------------------------------------------------
        Console.WriteLine("## (a) 書かれた傷 ＝ 消えた傷 ＋ 決着時に残っていた傷 ／ (b) 無効版で傷が 0");
        Console.WriteLine();
        long off = 0, on = 0; int bad = 0, seen = 0;
        const int CheckSeeds = 20;
        foreach (var (_, f) in Presets.Compare.Concat(Presets.Cross))
            foreach (int wv in Waves)
                for (int seed = 0; seed < CheckSeeds; seed++)
                {
                    WoundLedger a = BattleEngine.Run(f, EnemyCatalog.Stages[wv].Enemy, seed, verbose: false, wound: V0).Wounds;
                    WoundLedger b = BattleEngine.Run(f, EnemyCatalog.Stages[wv].Enemy, seed, verbose: false, wound: VOff).Wounds;
                    on += a.Written; off += b.Written;
                    seen++;
                    if (a.Written != a.Accounted) bad++;
                }
        Console.WriteLine($"- (a) {seen:N0} 戦のうち帳簿が閉じなかったのは **{bad} 戦**。{(bad == 0 ? "**○**" : "**×**")}");
        Console.WriteLine($"- (b) 無効版の傷の総数 = **{off}**（現行 {on:N0}）。{(off == 0 ? "**○**" : "**×**")}");
        Console.WriteLine();

        // --- 走査あり／なしで盤面が動かないこと ---------------------------------------------
        Console.WriteLine("## (f) `Census` を入れても盤面が1ビットも動かない");
        Console.WriteLine();
        int cdiff = 0, ccells = 0;
        foreach (var (_, f) in Presets.Compare.Concat(Presets.Cross))
            foreach (int wv in Waves)
            {
                int x = 0, y = 0;
                for (int seed = 0; seed < CheckSeeds; seed++)
                {
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[wv].Enemy, seed, verbose: false, wound: V0).PlayerWon) x++;
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[wv].Enemy, seed, verbose: false).PlayerWon) y++;
                }
                ccells++;
                if (x != y) cdiff++;
            }
        Console.WriteLine($"- {ccells} セル中 **{cdiff} 件**の差分（走査あり 対 既定）。{(cdiff == 0 ? "**○**" : "**×**")}");
        Console.WriteLine();

        // --- (c)(d) --------------------------------------------------------------------
        Console.WriteLine("## (c)(d) 走査件数と、実装の分岐からの列挙");
        Console.WriteLine();
        Console.WriteLine("`wound2 phase0` の表P が出す（走査が 0 件なら `phase0` がその場で止まる）。");
        Console.WriteLine();

        // --- 必須3 --------------------------------------------------------------------
        Console.WriteLine("## 必須3 —— 触っていないノブの既定が動いていない");
        Console.WriteLine();
        Console.WriteLine($"- `WoundRule.Default` = `{WoundRule.Default}` **＝ 現行**（書かれる・走査しない）。");
        Console.WriteLine("- 残りは `docs/rules.md` の差分で示す（`derive rules` を再生成して `git diff docs/`）。");
        Console.WriteLine("  **`wound` が 42 本目として1行増えるだけ**であることが合格条件。");
        Console.WriteLine();

        // --- 必須4 --------------------------------------------------------------------
        int pick = Regex.Matches(Src("Traits.cs"), @"(?<!///.{0,200})\bPickOne\(").Count
                 + Regex.Matches(Src("BattleEngine.cs"), @"\bPickOne\(").Count;
        Console.WriteLine("## 必須4 —— `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        Console.WriteLine($"- `BattleCore` の `PickOne(` の出現 = **{pick} 箇所**（doc コメントを含む粗い数え）。");
        Console.WriteLine("  **第120期に足したコードは `PickOne` を1度も呼んでいない**（計数だけ・`git diff` で確認できる）。");
        Console.WriteLine();

        Console.WriteLine("## (e) 判定に使ったセルの screen");
        Console.WriteLine();
        Console.WriteLine("`wound2 run` の表D-2 が「両方とも床／両方とも天井」の行数を出す。");
        Console.WriteLine();
    }

    // =====================================================================================
    // wound2 spill モード（第121期） —— 巻き込み則の版を並べる。**測るだけ。機構は1つも作らない**
    //
    // 起点は第120期: 傷の供給の 89.4% は engine の巻き込み則（`SpillWoundRule`）で、
    // 駒（キリ・ノミ）は合わせて 6.9% しか書かない。**書かれた傷の 94.7% は一度も読まれずに消える。**
    //
    // > **巻き込み則は第88期に、縫いの両側読み（ハリ）と対で採用された。その対の片側は第108期に
    // > ロスターから外れ、供給の側だけが残って味方に 8.211/戦 を書き続けている。**
    //
    // **engine にもノブにも1行も足していない**——`SpillWoundRule(Enabled, Scope)` は既存で、
    // 計数は第120期の `WoundRule(Census)` をそのまま使う。**新しい enum 値も足さない。**
    //
    //     dotnet run --project BattleSim -c Release 0 wound2 spill phase0   # 表P（§1・**戦闘0回**）
    //     dotnet run --project BattleSim -c Release 0 wound2 spill run      # 表A〜C（3版 × 73行）
    //     dotnet run --project BattleSim -c Release 0 wound2 spill check    # 自己検査
    //
    // **既存3モード（phase0 / run / check）は1文字も変えていない。** 同じファイルに置いてあるのは
    // `derive rules` の走査がクラス宣言（`static class`）でファイルを結ぶため——`partial` に割ると
    // **索引から `wound2` が静かに消える**（第121期に実際に踏んだ。第117・119期の「実装から引く表」の系）。
    // =====================================================================================
    // --- 版（測る前に固定する）----------------------------------------------------------
    static readonly (string Tag, string Label, SpillWoundRule R)[] SpillVers =
    {
        ("S0", "現行（`Enabled: true, Scope: All`・味方の刃6枚が書く）", new SpillWoundRule(true, SpillScope.All)),
        ("S1", "絞り（`Scope: Dense`・**吸いと余波だけ**が書く）",        new SpillWoundRule(true, SpillScope.Dense)),
        ("S2", "停止（`Enabled: false`・**第88期以前の状態**）",          new SpillWoundRule(false)),
    };

    // --- 第120期の実測（`design/PHASE120_WOUND_STOCK.md`）。**器具の再現の的（Q1）** ------
    const double P120Spill = 8.211, P120Written = 9.184, P120AllyRatio = 93.1, P120EngineShare = 89.4,
                 P120Unread = 94.7, P120Depth3Ally = 40.5, P120EffPerRead = 2.72, P120EffPerWritten = 0.15;
    const int P120Chain = 18, P120Moved = 18, P120Wrote = 54, P120Exact = 55;

    // --- Q3 の分母（第120期 §5-2 の「外すと落ちる」上位6行。**全部ノミかソラを含む**）------
    static readonly string[] CarveRows =
    {
        "刻み×断ち (ノミ×ナタ)", "刻み (ノミ単騎)", "刻み×抉り (ノミ×エグ)",
        "止め (トメ×ソラ)", "刻み×澱み (ノミ×ミオ)", "逸らし改 (ソラ×ノミ)"
    };
    const string PoisonRow = "追撃×毒 (ハギ×グザ)";

    const double Q3Line = 3.0;     // Q3: 刻み系が「動かない」線（第2〜5波平均・±）
    const double Q4Line = 20.0;    // Q4: `追撃×毒` 第2波が「戻る」線
    const double VetoLine = 10.0;  // Q5: 拒否権3（いずれかの波で −10.0pt 以上）
    const double MoveLine = 0.05;  // 「動いた」の線（第2〜5波平均）

    public static void SpillRun(string sub, string sub2 = "")
    {
        switch (sub)
        {
            // 第122期 —— `phase0 adopt` だけが別の表を出す。**`phase0` 単独の出力は1文字も変わらない。**
            case "phase0": if (sub2 == "adopt") AdoptPhase0(); else SpillPhase0(); return;
            case "run": SpillTables(); return;
            case "check": SpillCheck(); Console.WriteLine(); AdoptCheck(); return;
            // --- 第122期（採用の期）------------------------------------------------------
            case "adopt": AdoptTables(); return;
            case "seat": AdoptSeat(); return;
            default: Console.WriteLine("wound2 spill: モードは phase0 / run / check / adopt / seat のいずれか。"); return;
        }
    }

    /// <summary>走査した位置から囲みのクラス名を引く（**引けなければ「—」を返して表に出す**・第117/119期）。</summary>
    static string EnclosingClass(string src, int index)
    {
        MatchCollection ms = Regex.Matches(src.Substring(0, index),
            @"^\s*(?:public |internal |)(?:sealed |static |abstract |partial )*class (?<n>\w+)", RegexOptions.Multiline);
        return ms.Count == 0 ? "—" : ms[ms.Count - 1].Groups["n"].Value;
    }

    // ==================================================================================
    // Phase 0 —— §1。**戦闘0回**
    // ==================================================================================
    static void SpillPhase0()
    {
        Console.WriteLine("# 第121期 Phase 0 —— 巻き込み則の版を並べる（表P・**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("**enum のコメントではなく実装の分岐から引く**（第119期）。**走査件数を出力し、0 件なら止める**（第117期）。");
        Console.WriteLine();

        string traits = Src("Traits.cs"), engine = Src("BattleEngine.cs");

        // --- P-1 版 -------------------------------------------------------------------
        Console.WriteLine("## P-1 版（**既存のノブ。新しい enum 値も足していない**）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 設定 | 意味 |");
        Console.WriteLine("|---|---|---|");
        foreach (var v in SpillVers)
            Console.WriteLine($"| **{v.Tag}** | `{v.R}` | {v.Label} |");
        Console.WriteLine();
        Console.WriteLine($"- `SpillWoundRule.Default` = `{SpillWoundRule.Default}`（**測る間は現行のまま**）");
        Console.WriteLine($"- `SpillScope` の列挙子 = {string.Join(" / ", Enum.GetNames(typeof(SpillScope)).Select(x => "`" + x + "`"))}");
        Match writes = Regex.Match(traits, @"public bool Writes\(UnitState source\) =>\s*(?<b>[^;]+);");
        if (!writes.Success) { Console.WriteLine("- **`Writes` の実装が引けない。止める。**"); return; }
        Console.WriteLine($"- 絞りの判定（実装）: `{Regex.Replace(writes.Groups["b"].Value.Trim(), @"\s+", " ")}`");
        Console.WriteLine();

        // --- P-2 味方の刃の全数 ---------------------------------------------------------
        Console.WriteLine("## P-2 味方の刃の全数（`isFriendlyFire: true` の呼び出し）");
        Console.WriteLine();
        var ff = new List<(string File, string Cls, string Src3, bool Relayed, string Line)>();
        foreach ((string file, string src) in new[] { ("Traits.cs", traits), ("BattleEngine.cs", engine) })
            foreach (Match m in Regex.Matches(src,
                @"^(?!\s*///).*ApplyDamage\(\s*(?<a>[^,]+),\s*(?<b>[^,]+),\s*(?<c>[^,]+),\s*isFriendlyFire: true.*$",
                RegexOptions.Multiline))
                ff.Add((file, EnclosingClass(src, m.Index), m.Groups["c"].Value.Trim(),
                        m.Value.Contains("relayed: true"), m.Value.Trim()));

        Console.WriteLine($"**走査 {ff.Count} 件。**");
        if (ff.Count == 0) { Console.WriteLine("**走査が空。止める。**"); return; }
        Console.WriteLine();
        Console.WriteLine("| # | ファイル | 囲みのクラス | `source` | 中継札 | 巻き込み則を通るか |");
        Console.WriteLine("|--:|---|---|---|:-:|---|");
        var writers = new List<string>();
        for (int i = 0; i < ff.Count; i++)
        {
            bool nul = ff[i].Src3 == "null";
            string verdict = ff[i].Relayed ? "×（`relayed` の札＝肩代わりの中継）"
                           : nul ? "×（`source` が `null`）"
                           : "**○ 書き手**";
            if (!ff[i].Relayed && !nul) writers.Add(ff[i].Cls);
            Console.WriteLine($"| {i + 1} | `{ff[i].File}` | `{ff[i].Cls}` | `{ff[i].Src3}` | "
                + $"{(ff[i].Relayed ? "○" : "")} | {verdict} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**書き手 {writers.Count} 枚**（第120期の報告の 6 枚と照合する）。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | `TraitId` | 保持者（`UnitCatalog.All`） | **S1（Dense）で書くか** |");
        Console.WriteLine("|---|---|---|:-:|");
        int denseWriters = 0, unresolved = 0;
        foreach (string cls in writers)
        {
            string idName = cls.EndsWith("Trait") ? cls.Substring(0, cls.Length - 5) : cls;
            if (!Enum.TryParse(idName, out TraitId tid))
            {
                unresolved++;
                Console.WriteLine($"| `{cls}` | **引けない** | — | — |");
                continue;
            }
            string holders = string.Join(" / ", UnitCatalog.All.Where(u => u.Traits.Contains(tid)).Select(u => u.Name));
            bool dense = tid == TraitId.Drain || tid == TraitId.Splash;
            if (dense) denseWriters++;
            Console.WriteLine($"| `{cls}` | `{tid}` | {(holders.Length == 0 ? "**0 枚**" : holders)} | {(dense ? "**○**" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**S1 で残る書き手 = {denseWriters} 枚**（`Writes` の条件が `Drain` / `Splash` なので実装と一対一）。"
            + (unresolved > 0 ? $" **`TraitId` に落とせなかったクラスが {unresolved} 件。**" : ""));
        Console.WriteLine();

        // --- P-3 傷を読む行と陣営 --------------------------------------------------------
        Console.WriteLine("## P-3 傷を読む行の全数と、**どちらの陣営の傷を読むか**（§1 の 2）");
        Console.WriteLine();
        Console.WriteLine("**分類は囲みのクラスで引き、引けなかったものは「未分類」として全部表に出す**");
        Console.WriteLine("（第119期——**引けたことと使えることは別**）。");
        Console.WriteLine();
        var reads = new List<(string File, string Cls)>();
        foreach ((string file, string src) in new[] { ("Traits.cs", traits), ("BattleEngine.cs", engine) })
            foreach (Match m in Regex.Matches(src,
                @"^(?!\s*//)(?!.*NoteWoundRead)(?!.*SetCounter)(?!\s*public (?:int|bool) ).*(?:WoundDepthOf\(|IsWounded\(|Counter\(StatusKeys\.Wound\)).*$",
                RegexOptions.Multiline))
                reads.Add((file, EnclosingClass(src, m.Index)));

        Console.WriteLine($"**走査 {reads.Count} 件。**");
        if (reads.Count == 0) { Console.WriteLine("**走査が空。止める。**"); return; }
        Console.WriteLine();
        Console.WriteLine("| クラス | 件数 | 駒 | 読む陣営 | 根拠（実装） |");
        Console.WriteLine("|---|--:|---|---|---|");
        int unclassified = 0;
        foreach (var g in reads.GroupBy(x => x.Cls).OrderByDescending(g => g.Count()))
        {
            (string who, string side, string why) = ClassifyReader(g.Key);
            if (side.Contains("未分類")) unclassified++;
            Console.WriteLine($"| `{g.Key}` | {g.Count()} | {who} | {side} | {why} |");
        }
        Console.WriteLine();
        Console.WriteLine(unclassified == 0 ? "**未分類 0。**"
            : $"**未分類 {unclassified} 件。分類表に足すまで判定に使わない。**");
        Console.WriteLine();
        Console.WriteLine("> **味方の傷を読むのは 繕い（ノノ）／引き取り（ガルド）／縫い（ハリ・`Presets` に不在）／");
        Console.WriteLine("> 滲み則（engine・毒を持つ駒の傷を読むので陣営を見ていない）の 4 本。**");
        Console.WriteLine("> **攻めの3枚（抉り・なぞり・断ち）は敵の傷しか読まない**ので、S1 / S2 で損をしない。");
        Console.WriteLine();

        // --- P-4 代金が出る行の予告 -------------------------------------------------------
        Console.WriteLine("## P-4 味方の傷の読み手を含む行（**代金が出る行の予告**・§1 の 3）");
        Console.WriteLine();
        var rows = Rows();
        string[] payers = { UnitCatalog.Nono.Id, UnitCatalog.Gald.Id, UnitCatalog.Hari.Id };
        string[] payerNames = { "繕い（ノノ）", "引き取り（ガルド）", "縫い（ハリ）" };
        var hit = new List<(string Row, string Who)>();
        for (int r = 0; r < rows.Length; r++)
        {
            var ids = rows[r].F.Occupied().Select(u => u.Def.Id).ToHashSet(StringComparer.Ordinal);
            var who = new List<string>();
            for (int i = 0; i < payers.Length; i++) if (ids.Contains(payers[i])) who.Add(payerNames[i]);
            if (who.Count > 0) hit.Add((rows[r].Name + (rows[r].Cross ? "（交差帯）" : ""), string.Join(" / ", who)));
        }
        Console.WriteLine($"**{hit.Count} 行 / {rows.Length}**（`compare` {Presets.Compare.Length} ＋ 交差帯 {Presets.Cross.Length}）。");
        Console.WriteLine();
        Console.WriteLine("| # | 行 | 味方の傷の読み手 |");
        Console.WriteLine("|--:|---|---|");
        for (int i = 0; i < hit.Count; i++) Console.WriteLine($"| {i + 1} | {hit[i].Row} | {hit[i].Who} |");
        Console.WriteLine();
        Console.WriteLine("**ハリは 0 行のはず**（第108期にロスターから外れた）"
            + "——**対の片側が消えているというのが、この期の起点そのもの。**");
        Console.WriteLine();

        // --- P-5 滲み則の往復 ------------------------------------------------------------
        Console.WriteLine($"## P-5 滲み則が味方側の傷を読む経路（`{PoisonRow}` の往復・§1 の 4）");
        Console.WriteLine();
        Console.WriteLine($"- `SoakRule.Default` = `{SoakRule.Default}`（第90期に採用・**毒の側だけ**）");
        Console.WriteLine("- 実装: 毒の刻みが `wounded` なら 1 層ぶん重くなる（`BattleEngine.cs` の毒の段）。");
        Console.WriteLine("  **傷を持つ駒が敵味方どちらかを見ていない**ので、**敵の毒が味方の傷を読む**。");
        Console.WriteLine();
        var prow = rows.FirstOrDefault(x => x.Name == PoisonRow);
        if (prow.Name is null) { Console.WriteLine($"**`{PoisonRow}` が引けない。止める。**"); return; }
        Console.WriteLine($"`{PoisonRow}` の5枚: " + string.Join(" / ", prow.F.Occupied().Select(u => u.Def.Name)));
        Console.WriteLine();
        Console.WriteLine("> **第90期の事故（第2波 87.5 → 29.0 ＝ −58.5pt）は規約 (G1) がこの行・この波を名指ししている。**");
        Console.WriteLine("> 第120期は**傷を丸ごと外すとそっくり戻る**ことを示した（+58.5pt）。");
        Console.WriteLine("> **S2 で戻るなら、戻したのは「傷」ではなく「巻き込み則が味方に書いた傷」である**（Q4）。");
        Console.WriteLine();

        // --- P-6 第85 / 86 / 88期 ---------------------------------------------------------
        Console.WriteLine("## P-6 巻き込み則を採否した期の実測（§1 の 5）");
        Console.WriteLine();
        foreach (string f in new[] { "PHASE85_SUTURE2.md", "PHASE86_MENDER.md", "PHASE88_GAUGE.md" })
        {
            string path = Path.Combine(_root!, "design", f);
            Console.WriteLine(File.Exists(path)
                ? $"- `design/{f}` — あり（{File.ReadAllLines(path).Length} 行）"
                : $"- `design/{f}` — **無い**");
        }
        Console.WriteLine();
        Console.WriteLine("| 期 | 何を測ったか | 当時の線 | 結果 |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine("| 第85期 | 縫いの両側読み（W1）と巻き込み則（W2） | **Δ相乗 ≥ +3.0pt**（水準の分布から引いた線） | 落ちた |");
        Console.WriteLine("| 第86期 | 繕いの傷読み | 紙 ÷ 総被ダメ ≥ 5%（**大きさの門**） | 2×2 を1戦も回さず落ちた |");
        Console.WriteLine("| 第88期 | 物差しの引き直し（特異性・情報帯・ノイズ床） | 主判定は**特異性**・拒否権は大きさ | **両側読み ＋ 巻き込み則で +5.74 → 採用** |");
        Console.WriteLine();
        Console.WriteLine("> **当時の線と今の線の違い**: 第85期は「大きさ」で落ち、第88期は「特異性」で通った。");
        Console.WriteLine("> **どちらの線も『対で測る』ことを前提にしている**——その対の片側（ハリ）は今 `Presets` に 0 行。");
        Console.WriteLine("> **この期が問うのは大きさでも特異性でもなく、「対が壊れた後も供給だけ残す理由があるか」である。**");
        Console.WriteLine();

        // --- P-7 情報帯の片側 -------------------------------------------------------------
        Console.WriteLine("## P-7 判定に使うセルが情報帯に入るか（§1 の 6・片側）");
        Console.WriteLine();
        ScreenFromBalance();

        // --- P-8 再現の的 -----------------------------------------------------------------
        Console.WriteLine("## P-8 器具の再現の的（Q1・第120期の実測）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 第120期 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| 巻き込み則が書く傷/戦 | {P120Spill:F3} |");
        Console.WriteLine($"| 書かれた傷/戦（全経路） | {P120Written:F3} |");
        Console.WriteLine($"| **供給に占める巻き込み則** | **{P120EngineShare:F1}%** |");
        Console.WriteLine($"| **味方比** | **{P120AllyRatio:F1}%** |");
        Console.WriteLine($"| **読まれずに消えた割合** | **{P120Unread:F1}%** |");
        Console.WriteLine($"| 味方側の深さ3以上 | {P120Depth3Ally:F1}% |");
        Console.WriteLine($"| 実効÷読まれた傷 ／ 実効÷書かれた傷 | {P120EffPerRead:F2} ／ {P120EffPerWritten:F2} |");
        Console.WriteLine($"| 鎖が繋がっている行 ／ 傷が書かれた行 | {P120Chain} ／ {P120Wrote}（/ 73） |");
        Console.WriteLine($"| 傷を外すと動いた行 ／ ちょうど ±0.00pt | {P120Moved} ／ {P120Exact} |");
        Console.WriteLine();
        Console.WriteLine("**この表と `wound2 spill run` の S0 の列が一致することが Q1。**");
        Console.WriteLine("**新しい器具は既知の値を再現できて初めて使える。**");
        Console.WriteLine();
    }

    /// <summary>傷を読むクラスの分類（**実装から引けるのはクラス名まで。陣営は根拠を1行ずつ書く**）。</summary>
    static (string Who, string Side, string Why) ClassifyReader(string cls) => cls switch
    {
        "GougeTrait" => ("抉り（エグ）", "敵", "`OnAfterAttack` の主目標 ＝ 攻撃対象"),
        "CarveTrait" => ("刻みのなぞり（ノミ）", "敵", "同上（なぞってから刻む）"),
        "SeverTrait" => ("断ち（ナタ）", "敵", "`TargetPool`（標的候補 ＝ 敵陣）から選ぶ"),
        "SutureTrait" => ("縫い（ハリ・**不在**）", "**両側**", "`SutureRule.Both`（第90期）。`Presets` に 0 行"),
        "MenderTrait" => ("繕い（ノノ）", "**味方**", "`ctx.MostHurtAlly(self)` の患者"),
        "GuardianTrait" => ("引き取り（ガルド）", "**味方**", "`ctx.LivingMembers(self.TeamId)` の隣接"),
        "ThinBladeTrait" => ("薄刃（キリ）", "敵", "`ThinBladeCost.Unwounded`。**既定では読まない**"),
        "AmplifierTrait" => ("澱み（ミオ）", "敵", "傷口の着火（`IgniteRule`）。標的は敵"),
        "BattleContext" => ("滲み則 ＋ 帳簿（engine）", "**両側**", "毒／燃焼を持つ駒の傷を読む（陣営を見ていない）＋ 計数"),
        // `BattleEngine`（`BattleContext` ではない）に1件だけある——決着時に残っていた傷を帳簿へ落とす行。
        // **盤面を分岐させない計数専用**なので、版に依らず走る。
        "BattleEngine" => ("決着時の帳簿（engine）", "**両側**", "`WoundsAtEnd`。**計数専用で盤面を分岐させない**"),
        _ => ("—", "**未分類**", "—")
    };

    // ==================================================================================
    // run —— 表A〜C（3版 ＋ 第120期の再現用の対照）
    // ==================================================================================
    static void SpillTables()
    {
        var rows = Rows();
        int V = SpillVers.Length;
        var agg = new Agg[V];
        var perRow = new Agg[V][];
        var win = new double[V][][];
        var spillBy = new Dictionary<string, long>[V];
        for (int v = 0; v < V; v++)
        {
            agg[v] = new Agg();
            perRow[v] = new Agg[rows.Length];
            win[v] = new double[rows.Length][];
            spillBy[v] = new Dictionary<string, long>(StringComparer.Ordinal);
        }
        var winOff = new double[rows.Length][];

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int r = 0; r < rows.Length; r++)
        {
            for (int v = 0; v < V; v++) { win[v][r] = new double[Waves.Length]; perRow[v][r] = new Agg(); }
            winOff[r] = new double[Waves.Length];
            for (int wi = 0; wi < Waves.Length; wi++)
            {
                Formation foe = EnemyCatalog.Stages[Waves[wi]].Enemy;
                var hits = new int[V];
                int hoff = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    for (int v = 0; v < V; v++)
                    {
                        BattleResult res = BattleEngine.Run(rows[r].F, foe, seed, verbose: false,
                                                            spillWound: SpillVers[v].R, wound: V0);
                        if (res.PlayerWon) hits[v]++;
                        agg[v].Add(res);
                        perRow[v][r].Add(res);
                        foreach (var kv in res.TallyByUnit)
                            if (kv.Value.SpillWoundsWritten > 0)
                                spillBy[v][kv.Key] = spillBy[v].GetValueOrDefault(kv.Key) + kv.Value.SpillWoundsWritten;
                    }
                    // 第120期の再現用（傷を丸ごと外す・巻き込み則は現行のまま）。
                    if (BattleEngine.Run(rows[r].F, foe, seed, verbose: false, wound: VOff).PlayerWon) hoff++;
                }
                for (int v = 0; v < V; v++) win[v][r][wi] = hits[v] * 100.0 / Seeds;
                winOff[r][wi] = hoff * 100.0 / Seeds;
            }
        }
        sw.Stop();

        Console.WriteLine("# 第121期 —— 巻き込み則の版を並べる（表A〜C）");
        Console.WriteLine();
        Console.WriteLine($"台: `compare` {Presets.Compare.Length} 行 ＋ 交差帯 {Presets.Cross.Length} 行 ＝ **{rows.Length} 行** "
            + $"× 第2〜5波 × seed 0..{Seeds - 1}（規約 (G10)・(G14)）。");
        Console.WriteLine($"**3 版 × {agg[0].Battles:N0} 戦 ＋ 第120期の再現用の対照（傷を無効）。** 所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
        Console.WriteLine();
        Console.WriteLine("**`Presets` は1行も触っていない。**（版は `BattleEngine.Run` の引数だけで振る）");
        Console.WriteLine();

        SpillTableQ1(agg[0], rows, perRow[0], win[0], winOff);
        SpillTableA(agg, spillBy);
        SpillTableB(rows, win);
        SpillTableC(rows, win);
    }

    // --- Q1 器具の再現 ------------------------------------------------------------------
    static void SpillTableQ1(Agg a, (string Name, Formation F, bool Cross)[] rows, Agg[] per,
                             double[][] w0, double[][] off)
    {
        double n = a.Battles;
        double written = a.Written / n;
        double spill = a.WAlly[(int)WoundRoute.Spill] / n;
        double allyRatio = a.WrittenAlly * 100.0 / Math.Max(1, a.Written);
        double share = a.WAlly[(int)WoundRoute.Spill] * 100.0 / Math.Max(1, a.Written);
        double unread = a.Unread * 100.0 / Math.Max(1, a.Written);
        double d3 = a.DepthAlly[2] * 100.0 / Math.Max(1, a.DepthAlly.Sum());
        long dmgRead = a.RWounds[0] + a.RWounds[1] + a.RWounds[2];
        long dmgEff = a.REff[0] + a.REff[1] + a.REff[2];

        int chain = 0, wrote = 0, moved = 0, exact = 0;
        for (int r = 0; r < rows.Length; r++)
        {
            long wr = per[r].Written, rd = per[r].ReadWoundsTotal;
            if (wr > 0) wrote++;
            if (wr > 0 && rd > 0) chain++;
            double avg = 0;
            for (int i = 0; i < Waves.Length; i++) avg += w0[r][i] - off[r][i];
            avg /= Waves.Length;
            if (Math.Abs(avg) > MoveLine) moved++;
            if (avg == 0.0) exact++;
        }

        Console.WriteLine("## Q1 —— 器具の再現（S0 が第120期と一致するか）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 第120期 | S0（この期） | 一致 |");
        Console.WriteLine("|---|--:|--:|:-:|");
        Console.WriteLine(Cmp("巻き込み則が書く傷/戦", P120Spill, spill, 0.005, "F3"));
        Console.WriteLine(Cmp("書かれた傷/戦（全経路）", P120Written, written, 0.005, "F3"));
        Console.WriteLine(Cmp("**供給に占める巻き込み則**", P120EngineShare, share, 0.05, "F1"));
        Console.WriteLine(Cmp("**味方比**", P120AllyRatio, allyRatio, 0.05, "F1"));
        Console.WriteLine(Cmp("**読まれずに消えた割合**", P120Unread, unread, 0.05, "F1"));
        Console.WriteLine(Cmp("味方側の深さ3以上", P120Depth3Ally, d3, 0.05, "F1"));
        Console.WriteLine(Cmp("実効÷読まれた傷", P120EffPerRead, dmgRead == 0 ? 0 : dmgEff / (double)dmgRead, 0.005, "F2"));
        Console.WriteLine(Cmp("実効÷書かれた傷", P120EffPerWritten, a.Written == 0 ? 0 : dmgEff / (double)a.Written, 0.005, "F2"));
        Console.WriteLine(Cmp("鎖が繋がっている行", P120Chain, chain, 0.5, "F0"));
        Console.WriteLine(Cmp("傷が書かれた行", P120Wrote, wrote, 0.5, "F0"));
        Console.WriteLine(Cmp("傷を外すと動いた行", P120Moved, moved, 0.5, "F0"));
        Console.WriteLine(Cmp("ちょうど ±0.00pt の行", P120Exact, exact, 0.5, "F0"));
        Console.WriteLine();
    }

    static string Cmp(string name, double want, double got, double tol, string fmt)
        => $"| {name} | {want.ToString(fmt)} | {got.ToString(fmt)} | {(Math.Abs(want - got) <= tol ? "**○**" : "**×**")} |";

    // --- 表A 在庫 3 版 -------------------------------------------------------------------
    static void SpillTableA(Agg[] agg, Dictionary<string, long>[] spillBy)
    {
        int V = SpillVers.Length;
        Console.WriteLine("## 表A —— 在庫（§2-1・3 版）");
        Console.WriteLine();
        Console.WriteLine("### A-1 書かれた傷（経路別 × 陣営別・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 陣営 | " + string.Join(" | ", SpillVers.Select(v => v.Tag)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", V)));
        for (int i = 0; i < RouteName.Length; i++)
        {
            long tot = 0;
            for (int v = 0; v < V; v++) tot += agg[v].WAlly[i] + agg[v].WFoe[i];
            if (tot == 0) continue;
            int route = i;
            Console.WriteLine($"| {RouteName[route]} | 味方へ | " + string.Join(" | ",
                Enumerable.Range(0, V).Select(v => (agg[v].WAlly[route] / (double)agg[v].Battles).ToString("F3"))) + " |");
            Console.WriteLine($"| {RouteName[route]} | 敵へ | " + string.Join(" | ",
                Enumerable.Range(0, V).Select(v => (agg[v].WFoe[route] / (double)agg[v].Battles).ToString("F3"))) + " |");
        }
        Console.WriteLine("| **計** | | " + string.Join(" | ",
            Enumerable.Range(0, V).Select(v => (agg[v].Written / (double)agg[v].Battles).ToString("F3"))) + " |");
        Console.WriteLine("| **味方比** | | " + string.Join(" | ", Enumerable.Range(0, V).Select(v =>
            "**" + (agg[v].Written == 0 ? 0 : agg[v].WrittenAlly * 100.0 / agg[v].Written).ToString("F1") + "%**")) + " |");
        Console.WriteLine();

        Console.WriteLine("### A-2 在庫・深さ・消滅・単価");
        Console.WriteLine();
        Console.WriteLine("| 量 | " + string.Join(" | ", SpillVers.Select(v => v.Tag)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", V)));
        ARow("傷を持つ味方の同時存在数（平均/T）", v => (agg[v].StockAlly / (double)Math.Max(1, agg[v].StockTurns)).ToString("F3"));
        ARow("傷持ちの味方が1体でもいたターン", v => (agg[v].TurnsAllyAny * 100.0 / Math.Max(1, agg[v].StockTurns)).ToString("F1") + "%");
        ARow("傷持ちの敵が1体でもいたターン", v => (agg[v].TurnsFoeAny * 100.0 / Math.Max(1, agg[v].StockTurns)).ToString("F1") + "%");
        ARow("**味方側の深さ3以上**", v => (agg[v].DepthAlly[2] * 100.0 / Math.Max(1, agg[v].DepthAlly.Sum())).ToString("F1") + "%");
        ARow("敵側の深さ1", v => (agg[v].DepthFoe[0] * 100.0 / Math.Max(1, agg[v].DepthFoe.Sum())).ToString("F1") + "%");
        ARow("**読まれずに消えた割合**", v => "**" + (agg[v].Unread * 100.0 / Math.Max(1, agg[v].Written)).ToString("F1") + "%**");
        ARow("読まれた傷/戦（延べ）", v => (agg[v].ReadWoundsTotal / (double)agg[v].Battles).ToString("F3"));
        ARow("**実効÷読まれた傷**", v =>
        {
            long rd = agg[v].RWounds[0] + agg[v].RWounds[1] + agg[v].RWounds[2];
            long ef = agg[v].REff[0] + agg[v].REff[1] + agg[v].REff[2];
            return rd == 0 ? "—" : (ef / (double)rd).ToString("F2");
        });
        ARow("**実効÷書かれた傷**", v =>
        {
            long ef = agg[v].REff[0] + agg[v].REff[1] + agg[v].REff[2];
            return agg[v].Written == 0 ? "—" : "**" + (ef / (double)agg[v].Written).ToString("F2") + "**";
        });
        ARow("決着ターン（規約 (G6)）", v => (agg[v].Turns / (double)agg[v].Battles).ToString("F2"));
        ARow("勝率（全 73 行 × 第2〜5波）", v => (agg[v].Wins * 100.0 / agg[v].Battles).ToString("F1") + "%");
        Console.WriteLine();

        Console.WriteLine("### A-3 巻き込み則の書き手の内訳（`UnitTally.SpillWoundsWritten`・自己検査 (b)）");
        Console.WriteLine();
        var ids = new List<string>();
        foreach (var d in spillBy) foreach (string k in d.Keys) if (!ids.Contains(k)) ids.Add(k);
        ids.Sort((x, y) => spillBy[0].GetValueOrDefault(y).CompareTo(spillBy[0].GetValueOrDefault(x)));
        Console.WriteLine("| 書き手 | " + string.Join(" | ", SpillVers.Select(v => v.Tag)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", V)));
        foreach (string id in ids)
        {
            string nm = UnitCatalog.All.FirstOrDefault(u => u.Id == id)?.Name ?? id;
            string cur = id;
            Console.WriteLine($"| {nm} | " + string.Join(" | ", Enumerable.Range(0, V).Select(v =>
                (spillBy[v].GetValueOrDefault(cur) / (double)agg[v].Battles).ToString("F3"))) + " |");
        }
        Console.WriteLine();

        void ARow(string name, Func<int, string> f)
            => Console.WriteLine($"| {name} | " + string.Join(" | ", Enumerable.Range(0, V).Select(f)) + " |");
    }

    // --- 表B 帰属 -----------------------------------------------------------------------
    static void SpillTableB((string Name, Formation F, bool Cross)[] rows, double[][][] win)
    {
        Console.WriteLine("## 表B —— 帰属（S1 − S0 と S2 − S0・§2-2）");
        Console.WriteLine();
        Console.WriteLine("**符号は「版 − 現行」**——正なら**その版のほうが強い**（＝巻き込み則を絞る／止めると上がる）。");
        Console.WriteLine();

        // B-1 刻み系（Q3）
        Console.WriteLine("### B-1 刻み系 6 行（Q3・**動かないこと**が芯）");
        Console.WriteLine();
        Console.WriteLine("| 行 | S1−S0 平均 | S2−S0 平均 | S2−S0 の最大 | その波 | 線 ±3.0pt |");
        Console.WriteLine("|---|--:|--:|--:|--:|:-:|");
        double worst = 0;
        int missing = 0;
        foreach (string name in CarveRows)
        {
            int r = Array.FindIndex(rows, x => x.Name == name);
            if (r < 0) { missing++; Console.WriteLine($"| **{name}（引けない）** | — | — | — | — | **×** |"); continue; }
            double a1 = 0, a2 = 0, mx = 0;
            int mw = 0;
            for (int i = 0; i < Waves.Length; i++)
            {
                a1 += win[1][r][i] - win[0][r][i];
                double d = win[2][r][i] - win[0][r][i];
                a2 += d;
                if (Math.Abs(d) > Math.Abs(mx)) { mx = d; mw = Waves[i] + 1; }
            }
            a1 /= Waves.Length; a2 /= Waves.Length;
            if (Math.Abs(a2) > Math.Abs(worst)) worst = a2;
            Console.WriteLine($"| {name} | {a1:+0.0;-0.0;0.0} | **{a2:+0.0;-0.0;0.0}** | {mx:+0.0;-0.0;0.0} | 第{mw}波 | "
                + (Math.Abs(a2) <= Q3Line ? "**○**" : "**×**") + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q3 ＝ {(missing == 0 && Math.Abs(worst) <= Q3Line ? "○" : "×")}**"
            + $"（最大の変動 {worst:+0.0;-0.0;0.0}pt・線 ±{Q3Line:F1}pt）。");
        Console.WriteLine();

        // B-2 追撃×毒（Q4）
        Console.WriteLine($"### B-2 `{PoisonRow}`（Q4・第2波が **+{Q4Line:F0}pt 以上戻る**か）");
        Console.WriteLine();
        int pr = Array.FindIndex(rows, x => x.Name == PoisonRow);
        if (pr < 0) Console.WriteLine("**行が引けない。止める。**");
        else
        {
            double q4 = 0;
            Console.WriteLine("| 波 | S0 | S1 | S2 | S1−S0 | **S2−S0** |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|");
            for (int i = 0; i < Waves.Length; i++)
            {
                double d2 = win[2][pr][i] - win[0][pr][i];
                if (Waves[i] == 1) q4 = d2;
                Console.WriteLine($"| 第{Waves[i] + 1}波 | {win[0][pr][i]:F1} | {win[1][pr][i]:F1} | {win[2][pr][i]:F1} | "
                    + $"{win[1][pr][i] - win[0][pr][i]:+0.0;-0.0;0.0} | **{d2:+0.0;-0.0;0.0}** |");
            }
            Console.WriteLine();
            Console.WriteLine($"**Q4 ＝ {(q4 >= Q4Line ? "○" : "×")}**（第2波 {q4:+0.0;-0.0;0.0}pt・線 +{Q4Line:F1}pt）。");
        }
        Console.WriteLine();

        // B-3 代金（Q5）
        Console.WriteLine($"### B-3 代金（Q5・いずれかの波で **−{VetoLine:F1}pt 以上**落ちる行）");
        Console.WriteLine();
        for (int v = 1; v <= 2; v++)
        {
            var bad = new List<(string Name, double Worst, int Wave, double Avg)>();
            for (int r = 0; r < rows.Length; r++)
            {
                double w = 0, avg = 0;
                int ww = 0;
                for (int i = 0; i < Waves.Length; i++)
                {
                    double d = win[v][r][i] - win[0][r][i];
                    avg += d;
                    if (d < w) { w = d; ww = Waves[i] + 1; }
                }
                if (w <= -VetoLine) bad.Add((rows[r].Name, w, ww, avg / Waves.Length));
            }
            Console.WriteLine($"**{SpillVers[v].Tag}: {bad.Count} 行**"
                + (bad.Count >= 3 ? $"（**3 行以上 ＝ {SpillVers[v].Tag} は採らない**）" : "（線は 3 行）") + "。");
            Console.WriteLine();
            if (bad.Count == 0) continue;
            Console.WriteLine("| 行 | 最大の落差 | その波 | 第2〜5波の平均 |");
            Console.WriteLine("|---|--:|--:|--:|");
            foreach (var b in bad.OrderBy(x => x.Worst))
                Console.WriteLine($"| {b.Name} | {b.Worst:F1} pt | 第{b.Wave}波 | {b.Avg:+0.0;-0.0;0.0} pt |");
            Console.WriteLine();
            if (v == 2) BreakOrConstraint(rows, win, bad.Select(x => x.Name).ToArray());
        }
    }

    /// <summary>規約 (G2) —— 落ちた行を「壊れ」と「制約」に分ける（分母は 73 行）。</summary>
    static void BreakOrConstraint((string Name, Formation F, bool Cross)[] rows, double[][][] win, string[] bad)
    {
        Console.WriteLine("**(G2) 壊れか制約か**——落ちた行に含まれる駒それぞれについて、"
            + "**その駒を含む他の行**の第2〜5波平均の変化（S2−S0）を出す。");
        Console.WriteLine();
        Console.WriteLine("| 落ちた行 | 駒 | その駒を含む他の行 | 平均変化 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|---|");
        foreach (string name in bad)
        {
            int br = Array.FindIndex(rows, x => x.Name == name);
            if (br < 0) continue;
            foreach (var u in rows[br].F.Occupied())
            {
                string id = u.Def.Id;
                double sum = 0;
                int cnt = 0;
                for (int r = 0; r < rows.Length; r++)
                {
                    if (r == br) continue;
                    if (!rows[r].F.Occupied().Any(x => x.Def.Id == id)) continue;
                    double avg = 0;
                    for (int i = 0; i < Waves.Length; i++) avg += win[2][r][i] - win[0][r][i];
                    sum += avg / Waves.Length;
                    cnt++;
                }
                string verdict = cnt == 0 ? "**他の行が 0 行 ＝ この分解が成立しない**"
                               : sum / cnt <= -3.0 ? "**壊れ（その駒が使えなくなっている）**"
                               : "制約（組み合わせ固有）";
                Console.WriteLine($"| {name} | {u.Def.Name} | {cnt} | "
                    + (cnt == 0 ? "—" : $"{sum / cnt:+0.00;-0.00;0.00}") + $" | {verdict} |");
            }
        }
        Console.WriteLine();
    }

    // --- 表C 動いた行の全列挙 -------------------------------------------------------------
    static void SpillTableC((string Name, Formation F, bool Cross)[] rows, double[][][] win)
    {
        Console.WriteLine($"## 表C —— 動いた行の全列挙（|第2〜5波平均| > {MoveLine:F2}pt）");
        Console.WriteLine();
        for (int v = 1; v <= 2; v++)
        {
            var moved = new List<(string Name, double Avg, double Worst, double Best, int Info)>();
            int screened = 0;
            double all = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                double avg = 0, w = 0, b = 0;
                bool blind = true;
                int info = 0;
                for (int i = 0; i < Waves.Length; i++)
                {
                    double d = win[v][r][i] - win[0][r][i];
                    avg += d;
                    if (d < w) w = d;
                    if (d > b) b = d;
                    bool bothFloor = win[0][r][i] <= 0.0 && win[v][r][i] <= 0.0;
                    bool bothCeil = win[0][r][i] >= 100.0 && win[v][r][i] >= 100.0;
                    if (!bothFloor && !bothCeil) blind = false;
                    if (win[0][r][i] > 0.0 && win[0][r][i] < 100.0) info++;
                }
                if (blind) screened++;
                avg /= Waves.Length;
                all += avg;
                if (Math.Abs(avg) > MoveLine) moved.Add((rows[r].Name, avg, w, b, info));
            }
            Console.WriteLine($"### {SpillVers[v].Tag} —— **{moved.Count} / {rows.Length} 行**が動いた"
                + $"（現行と両方とも床／両方とも天井の行 = {screened}・自己検査 (e)）");
            Console.WriteLine();
            Console.WriteLine("| 行 | 第2〜5波平均 | 最大の下げ | 最大の上げ | S0 の情報セル |");
            Console.WriteLine("|---|--:|--:|--:|--:|");
            foreach (var m in moved.OrderByDescending(x => x.Avg))
                Console.WriteLine($"| {m.Name} | **{m.Avg:+0.0;-0.0;0.0}** | {m.Worst:F1} | +{m.Best:F1} | {m.Info} |");
            Console.WriteLine();
            Console.WriteLine($"**{rows.Length} 行の平均 = {all / rows.Length:+0.00;-0.00;0.00} pt。**");
            Console.WriteLine();

            // 拒否権1（(G9) の残る2本のうちの1本）。
            double f0 = 0, fv = 0;
            int fn = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                if (!Baseline.PrimaryRows.Contains(rows[r].Name)) continue;
                f0 += win[0][r][Waves.Length - 1];
                fv += win[v][r][Waves.Length - 1];
                fn++;
            }
            Console.WriteLine($"**主判定 {fn} 行の第五波平均: S0 {f0 / Math.Max(1, fn):F1}% → {SpillVers[v].Tag} {fv / Math.Max(1, fn):F1}%**"
                + $"（歯止め {Baseline.PrimaryFifthFloor:F1}%・拒否権1 ＝ "
                + (fv / Math.Max(1, fn) >= Baseline.PrimaryFifthFloor ? "**○**" : "**×**") + "）。");
            Console.WriteLine();
        }
    }

    // ==================================================================================
    // check —— 自己検査
    // ==================================================================================
    static void SpillCheck()
    {
        Console.WriteLine("# 第121期 —— 自己検査");
        Console.WriteLine();

        // --- 必須1 ---------------------------------------------------------------------
        Console.WriteLine("## 必須1 —— `compare` 305 セルが `docs/balance.md` と 0 件（**既定経路**）");
        Console.WriteLine();
        var want = BalanceCells();
        int cells = 0, diff = 0;
        foreach (var (name, f) in Presets.Compare)
        {
            if (!want.TryGetValue(name, out double[]? exp)) { Console.WriteLine($"- **行名が引けない: {name}**"); continue; }
            for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
            {
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
                double got = wins * 100.0 / Seeds;
                cells++;
                if (Math.Abs(got - exp[st]) > 0.049)
                {
                    diff++;
                    Console.WriteLine($"- **差分: {name} 第{st + 1}波 {exp[st]:F1}% → {got:F1}%**");
                }
            }
        }
        Console.WriteLine($"- {cells} セル中 **{diff} 件**の差分。{(diff == 0 ? "**○**" : "**×**")}");
        Console.WriteLine();

        const int CheckSeeds = 20;

        // --- (a') 既定と一致する版はどれか --------------------------------------------------
        //
        // **第122期に的を作り直した**（第60期「採用で既定が動いた診断は、検算の相手が V0 から V1 へ移る」）。
        // 第121期は「S0 が既定と一致する」でよかったが、**第122期に既定が S2 へ動いた**ので、
        // その文のままだと採用したこと自体が「×」として出る。数えるのは
        // **「どの版が既定と一致するか」**で、**一致した版が `SpillWoundRule.Default` と等しい**ことが合格条件。
        Console.WriteLine("## (a') 既定と一致する版はどれか（**明示的に渡すこと自体が盤面を動かしていないか**）");
        Console.WriteLine();
        int vN = SpillVers.Length;
        var vDiff = new int[vN];
        int vCells = 0;
        foreach (var (_, f) in Presets.Compare.Concat(Presets.Cross))
            foreach (int wv in Waves)
            {
                int y = 0;
                for (int seed = 0; seed < CheckSeeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[wv].Enemy, seed, verbose: false).PlayerWon) y++;
                for (int v = 0; v < vN; v++)
                {
                    int x = 0;
                    for (int seed = 0; seed < CheckSeeds; seed++)
                        if (BattleEngine.Run(f, EnemyCatalog.Stages[wv].Enemy, seed, verbose: false,
                                             spillWound: SpillVers[v].R, wound: V0).PlayerWon) x++;
                    if (x != y) vDiff[v]++;
                }
                vCells++;
            }
        Console.WriteLine($"| 版 | 設定 | {vCells} セル中の差分 | 既定と同じ設定か |");
        Console.WriteLine("|---|---|--:|:-:|");
        for (int v = 0; v < vN; v++)
            Console.WriteLine($"| {SpillVers[v].Tag} | `{SpillVers[v].R}` | {vDiff[v]} "
                + $"| {(SpillVers[v].R.Equals(SpillWoundRule.Default) ? "**○**" : "")} |");
        Console.WriteLine();
        int match = Array.FindIndex(vDiff, d => d == 0);
        bool aOk = match >= 0 && SpillVers[match].R.Equals(SpillWoundRule.Default)
                              && vDiff.Count(d => d == 0) == 1;
        Console.WriteLine($"- 差分 0 の版 = **{(match < 0 ? "無し" : SpillVers[match].Tag)}**、"
            + $"`SpillWoundRule.Default` = `{SpillWoundRule.Default}`。{(aOk ? "**○**" : "**×**")}");
        Console.WriteLine();

        // --- (b)(c) -----------------------------------------------------------------------
        Console.WriteLine("## (b) S2 で engine 経路が 0 ／ S1 で吸い・余波以外が 0 ／ (c) 帳簿が閉じる（3 版とも）");
        Console.WriteLine();
        int V = SpillVers.Length;
        var spill = new long[V];
        var gather = new long[V];
        var other = new long[V];
        var badLedger = new int[V];
        int seen = 0;
        string[] denseIds = UnitCatalog.All
            .Where(u => u.Traits.Contains(TraitId.Drain) || u.Traits.Contains(TraitId.Splash))
            .Select(u => u.Id).ToArray();
        foreach (var (_, f) in Presets.Compare.Concat(Presets.Cross))
            foreach (int wv in Waves)
                for (int seed = 0; seed < CheckSeeds; seed++)
                {
                    seen++;
                    for (int v = 0; v < V; v++)
                    {
                        BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[wv].Enemy, seed, verbose: false,
                                                            spillWound: SpillVers[v].R, wound: V0);
                        WoundLedger w = res.Wounds;
                        spill[v] += w.WriteAlly[(int)WoundRoute.Spill] + w.WriteFoe[(int)WoundRoute.Spill];
                        gather[v] += w.WriteAlly[(int)WoundRoute.Gather] + w.WriteFoe[(int)WoundRoute.Gather];
                        if (w.Written != w.Accounted) badLedger[v]++;
                        foreach (var kv in res.TallyByUnit)
                            if (kv.Value.SpillWoundsWritten > 0 && !denseIds.Contains(kv.Key))
                                other[v] += kv.Value.SpillWoundsWritten;
                    }
                }
        Console.WriteLine($"**{seen:N0} 戦 × 3 版。** 密度の高い2枚 = {string.Join(" / ", denseIds)}。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 巻き込み則の傷 | 引き取り（中継） | **吸い・余波以外が書いた巻き込み** | 帳簿が閉じない戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int v = 0; v < V; v++)
            Console.WriteLine($"| {SpillVers[v].Tag} | {spill[v]:N0} | {gather[v]:N0} | {other[v]:N0} | {badLedger[v]} |");
        Console.WriteLine();
        Console.WriteLine($"- (b) S2 の巻き込み則 = {spill[2]}。{(spill[2] == 0 ? "**○**" : "**×**")}"
            + $" ／ S1 の「吸い・余波以外」= {other[1]}。{(other[1] == 0 ? "**○**" : "**×**")}");
        Console.WriteLine($"- (c) 帳簿が閉じなかった戦 = {badLedger.Sum()}。{(badLedger.Sum() == 0 ? "**○**" : "**×**")}");
        Console.WriteLine();

        // --- 必須3 ------------------------------------------------------------------------
        Console.WriteLine("## 必須3 —— 触っていないノブの既定が動いていない");
        Console.WriteLine();
        Console.WriteLine($"- `SpillWoundRule.Default` = `{SpillWoundRule.Default}`"
            + "（**第121期は不変。第122期に `false` へ変えた**——上の値が現在の既定）。");
        Console.WriteLine($"- `WoundRule.Default` = `{WoundRule.Default}` ／ `GatherRule.Default` = `{GatherRule.Default}` ／ "
            + $"`MendRule.Default` = `{MendRule.Default}` ／ `SoakRule.Default` = `{SoakRule.Default}`。");
        Console.WriteLine("- 残りは `docs/rules.md` の差分で示す（`derive rules` を再生成して `git diff docs/`）。"
            + "**第121期は 0 バイト差が合格条件・第122期は動くのが 3 本だけであることが合格条件**（下の 122 の必須3）。");
        Console.WriteLine();

        // --- 必須4 ------------------------------------------------------------------------
        int pick = Regex.Matches(Src("Traits.cs"), @"(?<!///.{0,200})\bPickOne\(").Count
                 + Regex.Matches(Src("BattleEngine.cs"), @"\bPickOne\(").Count;
        Console.WriteLine("## 必須4 —— `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        Console.WriteLine($"- `BattleCore` の `PickOne(` の出現 = **{pick} 箇所**（第120期と同数なら ○）。");
        Console.WriteLine("- **第121期は `BattleCore` を1文字も触っていない**"
            + "（**第122期は既定値 3 行と1文 3 行だけ**——下の 122 の必須4）。");
        Console.WriteLine();

        // --- (d)(e) -----------------------------------------------------------------------
        Console.WriteLine("## (d) 走査件数 ／ (e) 判定に使ったセルの screen");
        Console.WriteLine();
        Console.WriteLine("- (d) `wound2 spill phase0` が全部の走査件数を出し、**0 件ならその場で止まる**。");
        Console.WriteLine("- (e) `wound2 spill run` の表C が「両方とも床／両方とも天井」の行数を版ごとに出す。");
        Console.WriteLine();
    }

    // =====================================================================================
    // wound2 spill adopt モード（第122期） —— **採用の期。新機構ゼロ。動かすのは既定値3つだけ。**
    //
    // 第121期の S2（巻き込み則を止める）を採るにあたり、**同じコミットで対の読み手2枚も降ろす**。
    // 供給だけ止めると、第88期に「対」で採った機構の残り半分が**入力ゼロのまま盤面に残る**
    // ——第108期にハリ（縫い）が消えて供給だけが残ったのと、まったく同じ形をもう一度作ることになる。
    //
    //     `SpillWoundRule.Enabled`  true  → **false**   （第88期に採用・供給側）
    //     `MendRule.Side`           Wound → **Plain**   （第92期に採用・ノノの読み手）
    //     `GatherRule.Enabled`      true  → **false**   （第90期 (P1) に採用・ガルドの中継）
    //
    // **3つとも残置**（ノブは残る）。**engine も駒も1文字も触らない**——動くのは既定値と、
    // それに伴う `PlusText`（`docs/units.md` が動く）だけ。
    //
    //     dotnet run --project BattleSim -c Release 0 wound2 spill phase0 adopt   # 表P
    //     dotnet run --project BattleSim -c Release 0 wound2 spill adopt          # 表A・B
    //     dotnet run --project BattleSim -c Release 0 wound2 spill seat           # 表C
    //     dotnet run --project BattleSim -c Release 0 wound2 spill check          # 自己検査（121 ＋ 122）
    // =====================================================================================
    static readonly (string Tag, string Label, SpillWoundRule S, MendRule M, GatherRule G)[] AdoptVers =
    {
        ("A0", "現行（3つとも現行）",
            new SpillWoundRule(true, SpillScope.All), new MendRule(MendSide.Wound), new GatherRule(true)),
        ("A1", "供給だけ止める（＝第121期 S2）",
            new SpillWoundRule(false), new MendRule(MendSide.Wound), new GatherRule(true)),
        ("A3", "**採用候補**（供給 ＋ 読み手2枚）",
            new SpillWoundRule(false), new MendRule(MendSide.Plain), new GatherRule(false)),
    };

    const double Q2Line = 0.5;      // Q2: A3 − A1 が「動かない」線（第2〜5波平均・±）
    const int Q2Rows = 70;          // Q2: その線に収まる行数の下限（/ 73）
    const double Q6Margin = 5.0;    // Q6: 歯止めとの余裕の下限
    const double SeatLine = 5.0;    // 席（第46期の採否閾値・(G16) で線と採る条件に同じ値を使う）
    const int SeatScan = 50;        // 席の粗探索の seed 数（`reseat` の写し）
    const int SeatBandB = 200, SeatBandBSeeds = 400;   // 帯B（安定の確認だけ。情報セルは帯A で数える・(G14)）

    /// <summary>採用の版で1戦回す（**`wound: V0` は第121期と揃える**＝走査を有効にするだけで盤面は動かない）。</summary>
    static BattleResult AdoptBattle(Formation f, Formation foe, int seed, int v)
        => BattleEngine.Run(f, foe, seed, verbose: false,
                            spillWound: AdoptVers[v].S, mend: AdoptVers[v].M, gather: AdoptVers[v].G, wound: V0);

    static double[] AdoptCells(Formation f, int v, int seedBase, int seeds)
    {
        var cells = new double[Waves.Length];
        for (int i = 0; i < Waves.Length; i++)
        {
            Formation foe = EnemyCatalog.Stages[Waves[i]].Enemy;
            int w = 0;
            for (int s = seedBase; s < seedBase + seeds; s++) if (AdoptBattle(f, foe, s, v).PlayerWon) w++;
            cells[i] = w * 100.0 / seeds;
        }
        return cells;
    }

    static double Avg25(double[] c) => c.Average();
    static int InfoCells(double[] c) => c.Count(x => x > 0.0 && x < 100.0);

    /// <summary>git を叩いて生の出力を返す（**引けなければ null。呼び出し側はそこで止める**・第117期）。</summary>
    static string? Git(params string[] args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = _root!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (string a in args) psi.ArgumentList.Add(a);
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return null;
            string outp = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 ? outp : null;
        }
        catch { return null; }
    }

    /// <summary>ある版の `UnitCatalog.cs` から、駒1枚の `PlusText` / `MinusText` を引く。</summary>
    static (string? Plus, string? Minus) TextsAt(string rev, string unitId)
    {
        string? src = Git("show", rev + ":BattleCore/UnitCatalog.cs");
        if (src is null) return (null, null);
        int at = src.IndexOf("Id = \"" + unitId + "\"", StringComparison.Ordinal);
        if (at < 0) return (null, null);
        int end = src.IndexOf("};", at, StringComparison.Ordinal);
        string block = end < 0 ? src.Substring(at) : src.Substring(at, end - at);
        Match p = Regex.Match(block, "PlusText = \"(?<t>[^\"]*)\"");
        Match m = Regex.Match(block, "MinusText = \"(?<t>[^\"]*)\"");
        return (p.Success ? p.Groups["t"].Value : null, m.Success ? m.Groups["t"].Value : null);
    }

    /// <summary>いま載っている文字列を導入したコミットの**親**を引く（(G15)・行番号ではなく文字列で切る）。</summary>
    static string? ParentOfIntroducing(string text)
    {
        string? log = Git("log", "--format=%H", "-S" + text, "--", "BattleCore/UnitCatalog.cs");
        if (string.IsNullOrWhiteSpace(log)) return null;
        string head = log.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        return head.Length == 0 ? null : head + "^";
    }

    /// <summary>味方に傷を書く刃（`isFriendlyFire: true`）の `TraitId`。**走査件数も返す**。</summary>
    static (List<TraitId> Ids, int Scanned) SpillWriterTraits(string traits, string engine)
    {
        var ids = new List<TraitId>();
        int scanned = 0;
        foreach (string src in new[] { traits, engine })
            foreach (Match m in Regex.Matches(src,
                @"^(?!\s*///).*ApplyDamage\(\s*(?<a>[^,]+),\s*(?<b>[^,]+),\s*(?<c>[^,]+),\s*isFriendlyFire: true.*$",
                RegexOptions.Multiline))
            {
                scanned++;
                if (m.Value.Contains("relayed: true") || m.Groups["c"].Value.Trim() == "null") continue;
                string cls = EnclosingClass(src, m.Index);
                string idName = cls.EndsWith("Trait") ? cls.Substring(0, cls.Length - 5) : cls;
                if (Enum.TryParse(idName, out TraitId tid) && !ids.Contains(tid)) ids.Add(tid);
            }
        return (ids, scanned);
    }

    // ==================================================================================
    // Phase 0 —— 表P（§1 の 1〜6）。**戦闘0回**
    // ==================================================================================
    static void AdoptPhase0()
    {
        Console.WriteLine("# 第122期 Phase 0 —— 巻き込み則を止め、対の読み手2枚も降ろす（表P・**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("**走査件数を必ず出し、0 件ならその場で止める**（第117期）。"
            + "**実装から引いた分類は食い違いを表に出す**（第119期）。");
        Console.WriteLine();

        string traits = Src("Traits.cs"), engine = Src("BattleEngine.cs");

        // --- P-1 版 --------------------------------------------------------------------
        Console.WriteLine("## P-1 版（§2 の3版。**ノブは3本とも既存。新しい enum 値も型も足していない**）");
        Console.WriteLine();
        Console.WriteLine("| 版 | `SpillWoundRule` | `MendRule` | `GatherRule` | 内容 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var v in AdoptVers)
            Console.WriteLine($"| **{v.Tag}** | `{v.S}` | `{v.M}` | `{v.G}` | {v.Label} |");
        Console.WriteLine();
        Console.WriteLine($"- 現在の既定: `SpillWoundRule.Default` = `{SpillWoundRule.Default}` ／ "
            + $"`MendRule.Default` = `{MendRule.Default}` ／ `GatherRule.Default` = `{GatherRule.Default}`");
        Console.WriteLine();

        // --- P-2 A1 ≡ S2 ---------------------------------------------------------------
        Console.WriteLine("## P-2 A1 が第121期の S2 と同一か（Q1 の的・§1 の 1）");
        Console.WriteLine();
        bool same = AdoptVers[1].S.Equals(SpillVers[2].R)
                 && AdoptVers[1].M.Equals(MendRule.Default) && AdoptVers[1].G.Equals(GatherRule.Default);
        Console.WriteLine($"- 第121期 S2 = `{SpillVers[2].R}`（`mend` / `gather` は渡していない ＝ 当時の既定）");
        Console.WriteLine($"- 第122期 A1 = `{AdoptVers[1].S}` ＋ `{AdoptVers[1].M}` ＋ `{AdoptVers[1].G}`");
        Console.WriteLine($"- **構成として同一か: {(same ? "**○**" : "**×**")}**"
            + "（実測での全セル一致は `wound2 spill adopt` の Q1 が出す）");
        Console.WriteLine();
        Console.WriteLine("> **注意**: この比較が成り立つのは `MendRule.Default` / `GatherRule.Default` が"
            + "**まだ現行**のあいだだけである。**既定を変えた後は A1 が「明示的に第121期の姿を渡した版」になる**"
            + "——第60期「採用で既定が動いた診断は、検算の相手が V0 から V1 へ移る」。");
        Console.WriteLine();

        // --- P-3 動く行の予告 -----------------------------------------------------------
        Console.WriteLine("## P-3 A3 で動く行の予告（§1 の 2）");
        Console.WriteLine();
        Console.WriteLine("### P-3a 味方に傷を書く刃（`isFriendlyFire: true` の全数）");
        Console.WriteLine();
        var (writerIds, scanned) = SpillWriterTraits(traits, engine);
        Console.WriteLine($"**走査 {scanned} 件。**");
        if (scanned == 0) { Console.WriteLine("**走査が空。止める。**"); return; }
        Console.WriteLine($"**書き手 {writerIds.Count} 枚** = " + string.Join(" / ", writerIds.Select(t => $"`{t}`")) + "。");
        if (writerIds.Count == 0) { Console.WriteLine("**書き手が 0 枚。止める。**"); return; }
        Console.WriteLine();
        Console.WriteLine("| `TraitId` | 保持者（`UnitCatalog.All`） |");
        Console.WriteLine("|---|---|");
        foreach (TraitId t in writerIds)
        {
            string holders = string.Join(" / ", UnitCatalog.All.Where(u => u.Traits.Contains(t)).Select(u => u.Name));
            Console.WriteLine($"| `{t}` | {(holders.Length == 0 ? "**0 枚**" : holders)} |");
        }
        Console.WriteLine();

        // --- P-3b 読み手 -----------------------------------------------------------------
        Console.WriteLine("### P-3b 味方の傷の読み手（**この期に降ろす2枚**）");
        Console.WriteLine();
        Console.WriteLine("| 読み手 | 駒 | ノブ | 現行 | この期 |");
        Console.WriteLine("|---|---|---|---|---|");
        Console.WriteLine($"| 繕い（`MenderTrait`） | {UnitCatalog.Nono.Name} | `MendRule.Side` | `Wound` | **`Plain`** |");
        Console.WriteLine($"| 引き取り（`GuardianTrait`） | {UnitCatalog.Gald.Name} | `GatherRule.Enabled` | `true` | **`false`** |");
        Console.WriteLine($"| 縫い（`SutureTrait`） | {UnitCatalog.Hari.Name} | — | **`Presets` に 0 行**（第108期に外した） | — |");
        Console.WriteLine();

        // --- P-3c 行 ---------------------------------------------------------------------
        var rows = Rows();
        string[] readerIds = { UnitCatalog.Nono.Id, UnitCatalog.Gald.Id, UnitCatalog.Hari.Id };
        var predicted = new List<(string Row, bool Prim, bool Cross, string Why)>();
        foreach (var row in rows)
        {
            var defs = row.F.Occupied().Select(u => u.Def).ToList();
            var why = new List<string>();
            var w = defs.Where(d => d.Traits.Any(t => writerIds.Contains(t))).Select(d => d.Name).ToList();
            if (w.Count > 0) why.Add("刃: " + string.Join("・", w));
            var rd = defs.Where(d => readerIds.Contains(d.Id)).Select(d => d.Name).ToList();
            if (rd.Count > 0) why.Add("**読み手: " + string.Join("・", rd) + "**");
            // 滲み則は毒／燃焼を持つ駒の傷を**陣営を問わず**読む（第90期）。味方に傷が載らなくなれば往復が消える。
            bool soak = defs.Any(d => d.Traits.Contains(TraitId.Venom) || d.Traits.Contains(TraitId.Contagion)
                                   || d.Traits.Contains(TraitId.Amplifier) || d.Traits.Contains(TraitId.Miasma));
            if (soak && w.Count > 0) why.Add("**滲み則の往復**");
            if (why.Count > 0)
                predicted.Add((row.Name, Baseline.PrimaryRows.Contains(row.Name), row.Cross, string.Join(" ／ ", why)));
        }
        Console.WriteLine("### P-3c 予告した行");
        Console.WriteLine();
        Console.WriteLine($"**予告 {predicted.Count} 行 / {rows.Length}**"
            + $"（`compare` {Presets.Compare.Length} ＋ 交差帯 {Presets.Cross.Length}）。");
        if (predicted.Count == 0) { Console.WriteLine("**予告が 0 行。止める。**"); return; }
        Console.WriteLine();
        Console.WriteLine("| # | 行 | 主判定 | 交差帯 | 理由 |");
        Console.WriteLine("|--:|---|:-:|:-:|---|");
        for (int i = 0; i < predicted.Count; i++)
            Console.WriteLine($"| {i + 1} | {predicted[i].Row} | {(predicted[i].Prim ? "**P**" : "")} "
                + $"| {(predicted[i].Cross ? "○" : "")} | {predicted[i].Why} |");
        Console.WriteLine();
        Console.WriteLine("> **予告は上限であって予想ではない**——刃を持つ行でも、味方に当たらなければ動かない。"
            + "実測は `wound2 spill adopt` の表A。");
        Console.WriteLine();

        // --- P-4 主判定19行 -------------------------------------------------------------
        int prim = predicted.Count(p => p.Prim);
        Console.WriteLine("## P-4 予告した行のうち主判定19行に入るもの（§1 の 3・規約 (G4)）");
        Console.WriteLine();
        Console.WriteLine($"**{prim} 行 / {Baseline.PrimaryRows.Length} 行 = {prim * 100.0 / Baseline.PrimaryRows.Length:F1}%。**"
            + (prim > 0 ? " **主判定が動きうる。歯止めとの余裕を必ず書く**（§5 の分岐）。"
                        : " **主判定は動かない**。"));
        Console.WriteLine();
        foreach (var p in predicted.Where(x => x.Prim)) Console.WriteLine($"- {p.Row}");
        Console.WriteLine();
        Console.WriteLine($"`{PoisonRow}`（Q4 の行）は主判定19行に"
            + (Baseline.PrimaryRows.Contains(PoisonRow) ? "**入っている**" : "**入っていない**") + "。");
        Console.WriteLine();

        // --- P-5 採用前の実装と同一か -----------------------------------------------------
        Console.WriteLine("## P-5 `MendSide.Plain` / `GatherRule(false)` が採用前と同一か（§1 の 4）");
        Console.WriteLine();
        Console.WriteLine("**分岐そのものを実装から引く**（コメントではなく式）。");
        Console.WriteLine();
        Match mendBranch = Regex.Match(traits, @"int w = ctx\.Mend\.Side == MendSide\.Wound \? [^;]+;");
        Match gatherBranch = Regex.Match(traits, @"if \(!ctx\.Gather\.Enabled\) return;");
        if (!mendBranch.Success || !gatherBranch.Success) { Console.WriteLine("**分岐が引けない。止める。**"); return; }
        Console.WriteLine("| ノブ | 分岐（実装） | 分岐より後ろに残るもの |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine($"| `MendRule.Side` | `{mendBranch.Value}` | "
            + "`w == 0` なので `seal` も偽・`NoteWoundRead` も走らない（**繕い量は定額 `Amount` に戻る**） |");
        Console.WriteLine($"| `GatherRule.Enabled` | `{gatherBranch.Value}` | "
            + "**計数（`GatherGuards` / `GatherHadDonor`）だけ。`SetCounter` も `ctx.Wound` も走らない** |");
        Console.WriteLine();
        Console.WriteLine("**残る差は1つだけ**——ノノの札 `TraitId.Seal` は第92期に採用の作業として足したもので、"
            + "**`MendSide.Plain` では `w == 0` なので `seal` が常に偽**（`HasTrait(Seal)` は読まれるが分岐しない）。");
        Console.WriteLine($"- ノノの `Traits` = {string.Join(" / ", UnitCatalog.Nono.Traits.Select(t => "`" + t + "`"))}");
        Console.WriteLine("- **札は残す**（機構は残置。降ろすのは既定値だけ、という §0-1 の形に揃える）。");
        Console.WriteLine();

        // --- P-6 旧文 ---------------------------------------------------------------------
        Console.WriteLine("## P-6 ノノ・ガルドの `PlusText` の旧文（§1 の 5・**`git log` から引く。手で書き直さない**）");
        Console.WriteLine();
        var targets = new (UnitDef U, string Label)[] { (UnitCatalog.Gald, "第90期 (P1)"), (UnitCatalog.Nono, "第92期") };
        var old = new Dictionary<string, (string P, string M)>(StringComparer.Ordinal);
        Console.WriteLine("| 駒 | 採用の期 | 導入コミットの親 | 旧 `PlusText` | 旧 `MinusText` |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (u, label) in targets)
        {
            string? rev = ParentOfIntroducing(u.PlusText);
            if (rev is null) { Console.WriteLine($"| {u.Name} | {label} | **引けない** | — | — |"); continue; }
            var (p, m) = TextsAt(rev, u.Id);
            if (p is null || m is null) { Console.WriteLine($"| {u.Name} | {label} | `{rev}` | **引けない** | — |"); continue; }
            old[u.Id] = (p, m);
            Console.WriteLine($"| {u.Name} | {label} | `{rev}` | {p} | {m} |");
        }
        Console.WriteLine();
        if (old.Count < 2) { Console.WriteLine("**2枚とも引けていない。止める**（手で書き直さない）。"); return; }

        Console.WriteLine("**現行の文**:");
        Console.WriteLine();
        foreach (var (u, _) in targets)
            Console.WriteLine($"- {u.Name}: `{u.PlusText}` ／ `{u.MinusText}`");
        Console.WriteLine();
        Console.WriteLine("> **旧文をそのまま書き戻せない欄が1つある。**");
        Console.WriteLine("> ノノの `MinusText` は**第106期（繕いの代金半額）が同じ行の別の箇所を変えている**ので、");
        Console.WriteLine("> 逐語で戻すと第106期の採用を巻き戻すことになる。**戻すのは第92期が足した節だけ。**");
        Console.WriteLine();
        string nonoMinusNew = UnitCatalog.Nono.MinusText.Replace("繕うとその傷はひとつ塞がる。", "");
        Console.WriteLine("| 駒 | 欄 | この期に書く文 | 逐語の旧文と一致 |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine($"| {UnitCatalog.Gald.Name} | Plus | {old[UnitCatalog.Gald.Id].P} | ○ |");
        Console.WriteLine($"| {UnitCatalog.Nono.Name} | Plus | {old[UnitCatalog.Nono.Id].P} | ○ |");
        Console.WriteLine($"| {UnitCatalog.Nono.Name} | Minus | {nonoMinusNew} | "
            + (nonoMinusNew == old[UnitCatalog.Nono.Id].M ? "○" : "**×（第106期の「半分」を保つ）**") + " |");
        Console.WriteLine();
        Console.WriteLine($"逐語の旧 `MinusText`: `{old[UnitCatalog.Nono.Id].M}`");
        Console.WriteLine();
        Console.WriteLine($"ガルドの `MinusText` は3期とも触っていない（`{UnitCatalog.Gald.MinusText}`）。");
        Console.WriteLine();

        // --- P-7 情報帯 -------------------------------------------------------------------
        Console.WriteLine("## P-7 判定に使うセルが情報帯に入るか（§1 の 6・片側）");
        Console.WriteLine();
        ScreenFromBalance();
    }

    // ==================================================================================
    // adopt —— 表A・B（§2。A0 / A1 / A3 × 73 行 × 第2〜5波 × seed 0..199）
    // ==================================================================================
    static void AdoptTables()
    {
        var rows = Rows();
        int V = AdoptVers.Length;
        var win = new double[V][][];
        for (int v = 0; v < V; v++) win[v] = new double[rows.Length][];
        var s121 = new double[rows.Length][];          // 第121期の S2 の経路（Q1 の的）
        var agg = new Agg[V];
        for (int v = 0; v < V; v++) agg[v] = new Agg();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int r = 0; r < rows.Length; r++)
        {
            for (int v = 0; v < V; v++) win[v][r] = new double[Waves.Length];
            s121[r] = new double[Waves.Length];
            for (int wi = 0; wi < Waves.Length; wi++)
            {
                Formation foe = EnemyCatalog.Stages[Waves[wi]].Enemy;
                var hits = new int[V];
                int h121 = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    for (int v = 0; v < V; v++)
                    {
                        BattleResult res = AdoptBattle(rows[r].F, foe, seed, v);
                        if (res.PlayerWon) hits[v]++;
                        agg[v].Add(res);
                    }
                    // 第121期の S2 とまったく同じ呼び方（`mend` / `gather` を渡さない）。
                    if (BattleEngine.Run(rows[r].F, foe, seed, verbose: false,
                                         spillWound: SpillVers[2].R, wound: V0).PlayerWon) h121++;
                }
                for (int v = 0; v < V; v++) win[v][r][wi] = hits[v] * 100.0 / Seeds;
                s121[r][wi] = h121 * 100.0 / Seeds;
            }
        }
        sw.Stop();

        Console.WriteLine("# 第122期 —— 巻き込み則を止め、対の読み手2枚も降ろす（表A・B）");
        Console.WriteLine();
        Console.WriteLine($"台: `compare` {Presets.Compare.Length} 行 ＋ 交差帯 {Presets.Cross.Length} 行 ＝ **{rows.Length} 行** "
            + $"× 第2〜5波 × seed 0..{Seeds - 1}（規約 (G10)・(G14)）。");
        Console.WriteLine($"**3 版 ＋ 第121期 S2 の再現 × {agg[0].Battles:N0} 戦。** 所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
        Console.WriteLine();
        Console.WriteLine("**`Presets` も engine も1行も触っていない**（版は `BattleEngine.Run` の引数だけで振る）。");
        Console.WriteLine();

        // --- Q1 ------------------------------------------------------------------------
        int q1bad = 0;
        for (int r = 0; r < rows.Length; r++)
            for (int i = 0; i < Waves.Length; i++)
                if (Math.Abs(win[1][r][i] - s121[r][i]) > 1e-9) q1bad++;
        Console.WriteLine("## Q1 —— 器具の再現（A1 が第121期 S2 と全セル一致するか）");
        Console.WriteLine();
        Console.WriteLine($"{rows.Length} 行 × {Waves.Length} 波 ＝ **{rows.Length * Waves.Length} セル**中、"
            + $"ずれ **{q1bad} 件**。**Q1 ＝ {(q1bad == 0 ? "○" : "×")}**");
        Console.WriteLine();
        Console.WriteLine("（A1 は `mend` / `gather` を**現行の既定と同じ値で明示的に渡した**版なので、"
            + "一致しなければ「明示的に渡すこと自体が盤面を動かしている」＝器具の欠陥である。）");
        Console.WriteLine();

        // --- 表A -----------------------------------------------------------------------
        Console.WriteLine("## 表A —— A0 / A1 / A3 × 73 行（**A3 − A1 を別列で**）");
        Console.WriteLine();
        Console.WriteLine("符号は「版 − A0」。**A3 − A1 が読み手2枚を降ろしたぶん**（Q2）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 主 | 交 | A0 | A1 | A3 | A1−A0 | **A3−A0** | **A3−A1** | 情報セル A0→A3 |");
        Console.WriteLine("|---|:-:|:-:|--:|--:|--:|--:|--:|--:|--:|");
        var order = Enumerable.Range(0, rows.Length)
            .OrderByDescending(r => Avg25(win[2][r]) - Avg25(win[0][r])).ToList();
        int q2ok = 0, moved = 0, screened = 0;
        foreach (int r in order)
        {
            double a0 = Avg25(win[0][r]), a1 = Avg25(win[1][r]), a3 = Avg25(win[2][r]);
            if (Math.Abs(a3 - a1) <= Q2Line) q2ok++;
            if (Math.Abs(a3 - a0) > MoveLine) moved++;
            bool blind = true;
            for (int i = 0; i < Waves.Length; i++)
            {
                bool bothFloor = win[0][r][i] <= 0.0 && win[2][r][i] <= 0.0;
                bool bothCeil = win[0][r][i] >= 100.0 && win[2][r][i] >= 100.0;
                if (!bothFloor && !bothCeil) blind = false;
            }
            if (blind) screened++;
            Console.WriteLine($"| {rows[r].Name} | {(Baseline.PrimaryRows.Contains(rows[r].Name) ? "**P**" : "")} "
                + $"| {(rows[r].Cross ? "○" : "")} | {a0:F1} | {a1:F1} | {a3:F1} "
                + $"| {a1 - a0:+0.0;-0.0;0.0} | **{a3 - a0:+0.0;-0.0;0.0}** | {a3 - a1:+0.00;-0.00;0.00} "
                + $"| {InfoCells(win[0][r])} → {InfoCells(win[2][r])} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**動いた行（|A3−A0| > {MoveLine:F2}pt）= {moved} / {rows.Length}。**"
            + $" 現行と A3 の**両方とも床／両方とも天井**の行 = {screened}（自己検査 (f)）。");
        Console.WriteLine();
        Console.WriteLine($"**Q2 ＝ {(q2ok >= Q2Rows ? "○" : "×")}**"
            + $"（|A3−A1| ≤ {Q2Line:F1}pt の行が **{q2ok} / {rows.Length}**・線 {Q2Rows} 行）。");
        Console.WriteLine();
        var big = order.Where(r => Math.Abs(Avg25(win[2][r]) - Avg25(win[1][r])) > Q2Line).ToList();
        if (big.Count > 0)
        {
            Console.WriteLine("**線を超えた行**（読み手2枚を降ろしたことで動いた行）:");
            Console.WriteLine();
            Console.WriteLine("| 行 | A3−A1 | 読み手 |");
            Console.WriteLine("|---|--:|---|");
            foreach (int r in big)
            {
                var who = rows[r].F.Occupied().Select(u => u.Def)
                    .Where(d => d.Id == UnitCatalog.Nono.Id || d.Id == UnitCatalog.Gald.Id)
                    .Select(d => d.Name).ToList();
                Console.WriteLine($"| {rows[r].Name} | {Avg25(win[2][r]) - Avg25(win[1][r]):+0.0;-0.0;0.0} "
                    + $"| {(who.Count == 0 ? "**いない（残りの書き手を名指しすること）**" : string.Join(" / ", who))} |");
            }
            Console.WriteLine();
        }

        // --- 表B-1 刻み系（Q3）----------------------------------------------------------
        Console.WriteLine("## 表B —— 芯になる行（§2-1 の Q3・Q4・Q5）");
        Console.WriteLine();
        Console.WriteLine($"### B-1 刻み系 6 行（Q3・**動かないこと**が芯。線 ±{Q3Line:F1}pt）");
        Console.WriteLine();
        Console.WriteLine("| 行 | A1−A0 | **A3−A0** | A3−A0 の最大 | その波 | 線 |");
        Console.WriteLine("|---|--:|--:|--:|--:|:-:|");
        double worst = 0;
        int missing = 0;
        foreach (string name in CarveRows)
        {
            int r = Array.FindIndex(rows, x => x.Name == name);
            if (r < 0) { missing++; Console.WriteLine($"| **{name}（引けない）** | — | — | — | — | **×** |"); continue; }
            double a1 = Avg25(win[1][r]) - Avg25(win[0][r]), a3 = Avg25(win[2][r]) - Avg25(win[0][r]), mx = 0;
            int mw = 0;
            for (int i = 0; i < Waves.Length; i++)
            {
                double d = win[2][r][i] - win[0][r][i];
                if (Math.Abs(d) > Math.Abs(mx)) { mx = d; mw = Waves[i] + 1; }
            }
            if (Math.Abs(a3) > Math.Abs(worst)) worst = a3;
            Console.WriteLine($"| {name} | {a1:+0.0;-0.0;0.0} | **{a3:+0.0;-0.0;0.0}** | {mx:+0.0;-0.0;0.0} | 第{mw}波 | "
                + (Math.Abs(a3) <= Q3Line ? "**○**" : "**×**") + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q3 ＝ {(missing == 0 && Math.Abs(worst) <= Q3Line ? "○" : "×")}**"
            + $"（最大の変動 {worst:+0.0;-0.0;0.0}pt・線 ±{Q3Line:F1}pt）。");
        Console.WriteLine();

        // --- 表B-2 追撃×毒（Q4）---------------------------------------------------------
        Console.WriteLine($"### B-2 `{PoisonRow}`（Q4・第2波が **+{Q4Line:F0}pt 以上戻る**か）");
        Console.WriteLine();
        int pr = Array.FindIndex(rows, x => x.Name == PoisonRow);
        double q4 = 0;
        if (pr < 0) Console.WriteLine("**行が引けない。止める。**");
        else
        {
            Console.WriteLine("| 波 | A0 | A1 | A3 | A1−A0 | **A3−A0** |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|");
            for (int i = 0; i < Waves.Length; i++)
            {
                double d = win[2][pr][i] - win[0][pr][i];
                if (Waves[i] == 1) q4 = d;
                Console.WriteLine($"| 第{Waves[i] + 1}波 | {win[0][pr][i]:F1} | {win[1][pr][i]:F1} | {win[2][pr][i]:F1} "
                    + $"| {win[1][pr][i] - win[0][pr][i]:+0.0;-0.0;0.0} | **{d:+0.0;-0.0;0.0}** |");
            }
            Console.WriteLine();
            Console.WriteLine($"**Q4 ＝ {(q4 >= Q4Line ? "○" : "×")}**（第2波 {q4:+0.0;-0.0;0.0}pt・線 +{Q4Line:F1}pt）。"
                + "**規約 (G1) が第90期の事故として名指ししている行・波である。**");
        }
        Console.WriteLine();

        // --- 表B-3 代金（Q5）------------------------------------------------------------
        Console.WriteLine($"### B-3 代金（Q5・いずれかの波で **−{VetoLine:F1}pt 以上**落ちる行。線 **0 行**）");
        Console.WriteLine();
        var bad = new List<(string Name, double Worst, int Wave, double Avg)>();
        for (int r = 0; r < rows.Length; r++)
        {
            double w = 0, avg = 0;
            int ww = 0;
            for (int i = 0; i < Waves.Length; i++)
            {
                double d = win[2][r][i] - win[0][r][i];
                avg += d;
                if (d < w) { w = d; ww = Waves[i] + 1; }
            }
            if (w <= -VetoLine) bad.Add((rows[r].Name, w, ww, avg / Waves.Length));
        }
        Console.WriteLine($"**{bad.Count} 行。Q5 ＝ {(bad.Count == 0 ? "○" : "×")}**");
        Console.WriteLine();
        if (bad.Count > 0)
        {
            Console.WriteLine("| 行 | 最大の落差 | その波 | 第2〜5波の平均 |");
            Console.WriteLine("|---|--:|--:|--:|");
            foreach (var b in bad.OrderBy(x => x.Worst))
                Console.WriteLine($"| {b.Name} | {b.Worst:F1} pt | 第{b.Wave}波 | {b.Avg:+0.0;-0.0;0.0} pt |");
            Console.WriteLine();
            AdoptBreakOrConstraint(rows, win, bad.Select(x => x.Name).ToArray());
        }

        // --- 表B-4 小さい代金（記録）------------------------------------------------------
        var small = new List<(string Name, double Worst, int Wave, double Avg)>();
        for (int r = 0; r < rows.Length; r++)
        {
            double w = 0, avg = 0;
            int ww = 0;
            for (int i = 0; i < Waves.Length; i++)
            {
                double d = win[2][r][i] - win[0][r][i];
                avg += d;
                if (d < w) { w = d; ww = Waves[i] + 1; }
            }
            if (w < 0 && w > -VetoLine && Math.Abs(avg / Waves.Length) > MoveLine)
                small.Add((rows[r].Name, w, ww, avg / Waves.Length));
        }
        Console.WriteLine($"### B-4 拒否権に届かない代金（**記録するだけ**・{small.Count} 行）");
        Console.WriteLine();
        if (small.Count > 0)
        {
            Console.WriteLine("| 行 | 最大の落差 | その波 | 第2〜5波の平均 |");
            Console.WriteLine("|---|--:|--:|--:|");
            foreach (var b in small.OrderBy(x => x.Worst))
                Console.WriteLine($"| {b.Name} | {b.Worst:F1} pt | 第{b.Wave}波 | {b.Avg:+0.0;-0.0;0.0} pt |");
            Console.WriteLine();
        }

        // --- Q6 `Baseline` ---------------------------------------------------------------
        Console.WriteLine("## Q6 —— `Baseline`（主判定19行の第五波と歯止め）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 主判定19行の第五波 | 全73行の第五波 | 歯止め | 余裕 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        double margin = 0;
        for (int v = 0; v < V; v++)
        {
            double f = 0, all = 0;
            int fn = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                all += win[v][r][Waves.Length - 1];
                if (!Baseline.PrimaryRows.Contains(rows[r].Name)) continue;
                f += win[v][r][Waves.Length - 1];
                fn++;
            }
            double p = f / Math.Max(1, fn);
            if (v == 2) margin = p - Baseline.PrimaryFifthFloor;
            Console.WriteLine($"| {AdoptVers[v].Tag} | **{p:F1}%**（{fn} 行） | {all / rows.Length:F1}% "
                + $"| {Baseline.PrimaryFifthFloor:F1}% | **{p - Baseline.PrimaryFifthFloor:+0.0;-0.0;0.0}pt** |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q6 ＝ {(margin >= Q6Margin ? "○" : "×")}**（余裕 {margin:+0.0;-0.0;0.0}pt・線 +{Q6Margin:F1}pt）。");
        Console.WriteLine();
        Console.WriteLine("**主判定19行の各波**:");
        Console.WriteLine();
        Console.WriteLine("| 版 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int v = 0; v < V; v++)
        {
            var cells = new double[Waves.Length];
            int fn = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                if (!Baseline.PrimaryRows.Contains(rows[r].Name)) continue;
                for (int i = 0; i < Waves.Length; i++) cells[i] += win[v][r][i];
                fn++;
            }
            Console.WriteLine($"| {AdoptVers[v].Tag} | " + string.Join(" | ", cells.Select(c => $"{c / fn:F1}")) + " |");
        }
        Console.WriteLine();

        // --- Q7 情報セル ------------------------------------------------------------------
        Console.WriteLine("## Q7 —— 情報セル（`compare` 61 行 × 第2〜5波・帯A・(G14)）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 情報セル合計 | 平均/行 | 0 | 1 | 2 | 3 | 4 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        var infoSum = new int[V];
        for (int v = 0; v < V; v++)
        {
            var dist = new int[5];
            int n = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Cross) continue;
                int c = InfoCells(win[v][r]);
                infoSum[v] += c;
                dist[c]++;
                n++;
            }
            Console.WriteLine($"| {AdoptVers[v].Tag} | **{infoSum[v]}** | {infoSum[v] / (double)n:F2} | "
                + string.Join(" | ", dist) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q7（席の再判定の前）: {infoSum[0]} → {infoSum[2]} = {infoSum[2] - infoSum[0]:+0;-0;0}。**"
            + " **席を測り直した後の値で判定する**（`wound2 spill seat`）。");
        Console.WriteLine();

        // --- 席の再判定に当たる行 -----------------------------------------------------------
        Console.WriteLine("## §2-2 の条件に当たる行（**席の再判定の対象**・測る前に固定した条件）");
        Console.WriteLine();
        Console.WriteLine("    (i)   情報セルが減った行");
        Console.WriteLine("    (ii)  第五波が **新たに** 95% を超えた行");
        Console.WriteLine("    (iii) 第2〜5波平均が **±10pt 以上**動いた行");
        Console.WriteLine();
        Console.WriteLine("| 行 | (i) | (ii) | (iii) | 情報セル | 第五波 A0→A3 | 平均の変化 |");
        Console.WriteLine("|---|:-:|:-:|:-:|--:|--:|--:|");
        int hit = 0;
        for (int r = 0; r < rows.Length; r++)
        {
            int i0 = InfoCells(win[0][r]), i3 = InfoCells(win[2][r]);
            double f0 = win[0][r][Waves.Length - 1], f3 = win[2][r][Waves.Length - 1];
            double d = Avg25(win[2][r]) - Avg25(win[0][r]);
            bool ci = i3 < i0, cii = f3 > 95.0 && f0 <= 95.0, ciii = Math.Abs(d) >= 10.0;
            if (!ci && !cii && !ciii) continue;
            hit++;
            Console.WriteLine($"| {rows[r].Name} | {(ci ? "○" : "")} | {(cii ? "○" : "")} | {(ciii ? "○" : "")} "
                + $"| {i0} → {i3} | {f0:F1} → {f3:F1} | {d:+0.0;-0.0;0.0} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**{hit} 行。** `wound2 spill seat` が同じ条件で選び直して席を測る。");
        Console.WriteLine();
    }

    /// <summary>規約 (G2) —— 落ちた行を「壊れ」と「制約」に分ける（分母は 73 行・A3 対 A0）。</summary>
    static void AdoptBreakOrConstraint((string Name, Formation F, bool Cross)[] rows, double[][][] win, string[] bad)
    {
        Console.WriteLine("**(G2) 壊れか制約か**——落ちた行に含まれる駒それぞれについて、"
            + "**その駒を含む他の行**の第2〜5波平均の変化（A3−A0）を出す。");
        Console.WriteLine();
        Console.WriteLine("| 落ちた行 | 駒 | その駒を含む他の行 | 平均変化 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|---|");
        foreach (string name in bad)
        {
            int br = Array.FindIndex(rows, x => x.Name == name);
            if (br < 0) continue;
            foreach (var u in rows[br].F.Occupied())
            {
                string id = u.Def.Id;
                double sum = 0;
                int cnt = 0;
                for (int r = 0; r < rows.Length; r++)
                {
                    if (r == br) continue;
                    if (!rows[r].F.Occupied().Any(x => x.Def.Id == id)) continue;
                    sum += Avg25(win[2][r]) - Avg25(win[0][r]);
                    cnt++;
                }
                string verdict = cnt == 0 ? "**他の行が 0 行 ＝ この分解が成立しない**"
                               : sum / cnt <= -3.0 ? "**壊れ（その駒が使えなくなっている）**"
                               : "制約（組み合わせ固有）";
                Console.WriteLine($"| {name} | {u.Def.Name} | {cnt} | "
                    + (cnt == 0 ? "—" : $"{sum / cnt:+0.00;-0.00;0.00}") + $" | {verdict} |");
            }
        }
        Console.WriteLine();
    }

    // ==================================================================================
    // seat —— 表C（§2-2。**条件に当たった行だけ**。採用版 A3 で測る）
    // ==================================================================================
    static void AdoptSeat()
    {
        var rows = Rows();
        Console.WriteLine("# 第122期 —— 席の再判定（表C・採用版 A3）");
        Console.WriteLine();
        Console.WriteLine("**採用と席は対**（第107期「機構が変わった行の席は寿命が切れる」）。**ただし全行はやらない。**");
        Console.WriteLine();
        Console.WriteLine("**対象の条件（測る前に固定）**: (i) 情報セルが減った行 ／ "
            + "(ii) 第五波が新たに 95% を超えた行 ／ (iii) 第2〜5波平均が ±10pt 以上動いた行。");
        Console.WriteLine();
        Console.WriteLine("**線と採る条件は同じ集合**（規約 (G16)）:");
        Console.WriteLine();
        Console.WriteLine("    狙（ガルドが前列 / セッキが後列）○ かつ 情報セル 2 以上");
        Console.WriteLine("    その中で 情報セル 4 → 3 → 2 の順、各段で第2〜5波平均が最上位");
        Console.WriteLine($"    段どうしの差が {SeatLine:F1}pt 未満なら情報の多い側");
        Console.WriteLine($"    現行から動かすのは 情報セルが増える か 平均が +{SeatLine:F1}pt 以上のときだけ");
        Console.WriteLine();
        Console.WriteLine($"情報セルは**帯A**（seed 0..{Seeds - 1}）で数える（規約 (G14)）。"
            + $"帯B（{SeatBandB}..{SeatBandB + SeatBandBSeeds - 1}）は安定の確認として併記するだけ。");
        Console.WriteLine();

        // --- 対象を選ぶ（A0 と A3 を測り直す）------------------------------------------------
        var target = new List<int>();
        var a0 = new double[rows.Length][];
        var a3 = new double[rows.Length][];
        var swSel = System.Diagnostics.Stopwatch.StartNew();
        Parallel.For(0, rows.Length, r =>
        {
            a0[r] = AdoptCells(rows[r].F, 0, 0, Seeds);
            a3[r] = AdoptCells(rows[r].F, 2, 0, Seeds);
        });
        swSel.Stop();
        for (int r = 0; r < rows.Length; r++)
        {
            int i0 = InfoCells(a0[r]), i3 = InfoCells(a3[r]);
            bool ci = i3 < i0;
            bool cii = a3[r][Waves.Length - 1] > 95.0 && a0[r][Waves.Length - 1] <= 95.0;
            bool ciii = Math.Abs(Avg25(a3[r]) - Avg25(a0[r])) >= 10.0;
            if (ci || cii || ciii) target.Add(r);
        }
        Console.WriteLine($"対象 **{target.Count} 行 / {rows.Length}**（選定に {swSel.Elapsed.TotalSeconds:F1} 秒）。");
        Console.WriteLine();
        if (target.Count == 0) { Console.WriteLine("**条件に当たる行が 0 行。席は動かさない。**"); return; }

        var adopted = new List<(string Name, Formation F, int From, int To, double D, double DB)>();
        foreach (int r in target)
        {
            var members = rows[r].F.Occupied().Select(x => x.Def).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in AdoptSlotAssignments(members.Count))
            {
                var f = new Formation();
                for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
                perms.Add(f);
            }
            var scan = new int[perms.Count];
            Parallel.For(0, perms.Count, i =>
            {
                int wins = 0;
                foreach (int wv in Waves)
                    for (int seed = 0; seed < SeatScan; seed++)
                        if (AdoptBattle(perms[i], EnemyCatalog.Stages[wv].Enemy, seed, 2).PlayerWon) wins++;
                scan[i] = wins;
            });
            var ord = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            var pool = ord.Take(20).Concat(ord.Where(i => AdoptIntent(perms[i])).Take(10))
                          .Append(ord.First(i => AdoptSameFormation(perms[i], rows[r].F))).Distinct().ToList();
            var cA = new double[pool.Count][];
            var cB = new double[pool.Count][];
            Parallel.For(0, pool.Count, k => cA[k] = AdoptCells(perms[pool[k]], 2, 0, Seeds));
            Parallel.For(0, pool.Count, k => cB[k] = AdoptCells(perms[pool[k]], 2, SeatBandB, SeatBandBSeeds));

            int curK = pool.FindIndex(i => AdoptSameFormation(perms[i], rows[r].F));
            double curAvg = Avg25(cA[curK]);
            int curInfo = InfoCells(cA[curK]);
            var ranked = Enumerable.Range(0, pool.Count).OrderByDescending(k => Avg25(cA[k])).ToList();

            Console.WriteLine($"## {rows[r].Name}"
                + (Baseline.PrimaryRows.Contains(rows[r].Name) ? "（**主判定19行**）" : "")
                + (rows[r].Cross ? "（交差帯）" : ""));
            Console.WriteLine();
            Console.WriteLine($"現行席の情報セル **{curInfo}**・第2〜5波平均 **{curAvg:F1}%**。候補 {pool.Count} 通り。");
            Console.WriteLine();
            Console.WriteLine("| 追順 | 粗順 | 狙 | 前1/前3 | 中央 | 後1/後3 | 平均(2〜5波) | Δ | **情報セル** | "
                + string.Join(" | ", Waves.Select(w => $"第{w + 1}波")) + $" | 平均({SeatBandB}..) | 情報セル({SeatBandB}..) |");
            Console.WriteLine("|--:|--:|:-:|---|---|---|--:|--:|--:|" + string.Concat(Waves.Select(_ => "--:|")) + "--:|--:|");
            for (int i = 0; i < ranked.Count; i++)
            {
                int k = ranked[i];
                Formation f = perms[pool[k]];
                Console.WriteLine($"| {i + 1}{(k == curK ? "★現行" : "")} | {ord.IndexOf(pool[k]) + 1} "
                    + $"| {(AdoptIntent(f) ? "○" : "×")} | {AdoptSeats(f)} | {Avg25(cA[k]):F1}% "
                    + $"| {Avg25(cA[k]) - curAvg:+0.0;-0.0;0.0} | **{InfoCells(cA[k])}** | "
                    + string.Join(" | ", cA[k].Select(c => $"{c:F1}%")) + $" | {Avg25(cB[k]):F1}% | {InfoCells(cB[k])} |");
            }
            Console.WriteLine();

            // (G16) 線と採る条件は同じ集合。**線を満たすが狙で落ちた席の数も出す。**
            var lineOnly = Enumerable.Range(0, pool.Count).Where(k => InfoCells(cA[k]) >= 2).ToList();
            var eligible = lineOnly.Where(k => AdoptIntent(perms[pool[k]])).ToList();
            Console.WriteLine($"情報セル 2 以上 = **{lineOnly.Count} 通り**、そのうち狙 ○ = **{eligible.Count} 通り**"
                + $"（**狙で落ちた席 {lineOnly.Count - eligible.Count} 通り**・(G16) の記録）。");
            Console.WriteLine();
            if (eligible.Count == 0) { Console.WriteLine("**判定: 据え置き** —— 条件を満たす候補が 0 通り。"); Console.WriteLine(); continue; }

            int best = -1;
            double gmax = eligible.Max(k => Avg25(cA[k]));
            for (int tier = 4; tier >= 2; tier--)
            {
                var inTier = eligible.Where(k => InfoCells(cA[k]) == tier).ToList();
                if (inTier.Count == 0) continue;
                int b = inTier.OrderByDescending(k => Avg25(cA[k])).ThenBy(k => ord.IndexOf(pool[k])).First();
                if (Avg25(cA[b]) > gmax - SeatLine) { best = b; break; }
            }
            if (best < 0) best = eligible.OrderByDescending(k => Avg25(cA[k])).First();

            bool take = best != curK
                     && (InfoCells(cA[best]) > curInfo || Avg25(cA[best]) - curAvg >= SeatLine);
            Console.WriteLine($"最上位は 追順 **{ranked.IndexOf(best) + 1} 位**"
                + $"（情報セル {curInfo} → **{InfoCells(cA[best])}**・Δ **{Avg25(cA[best]) - curAvg:+0.0;-0.0}pt**"
                + $"・帯B では Δ {Avg25(cB[best]) - Avg25(cB[curK]):+0.0;-0.0}pt / 情報セル {InfoCells(cB[best])}）。");
            Console.WriteLine();
            Console.WriteLine($"**判定: {(take ? "差し替え" : "据え置き")}**"
                + (take ? "" : " —— 情報セルが増えず、平均も線に届かない。"));
            if (take)
            {
                Formation bf = perms[pool[best]];
                Console.WriteLine();
                Console.WriteLine($"採る配置: `front1: {AdoptN(bf[0])}, front3: {AdoptN(bf[1])}, center: {AdoptN(bf[2])}, "
                    + $"back1: {AdoptN(bf[3])}, back3: {AdoptN(bf[4])}`");
                adopted.Add((rows[r].Name, bf, curInfo, InfoCells(cA[best]),
                             Avg25(cA[best]) - curAvg, Avg25(cB[best]) - Avg25(cB[curK])));
            }
            Console.WriteLine();
        }

        Console.WriteLine($"## まとめ —— 差し替える行 **{adopted.Count} 行 / {target.Count} 行**");
        Console.WriteLine();
        if (adopted.Count == 0) { Console.WriteLine("**席は1行も動かさない。**"); return; }
        Console.WriteLine("| 行 | 主判定 | 情報セル | Δ 平均(2〜5波) | Δ 帯B | 採る配置 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|---|");
        foreach (var a in adopted)
            Console.WriteLine($"| {a.Name} | {(Baseline.PrimaryRows.Contains(a.Name) ? "**P**" : "")} "
                + $"| {a.From} → **{a.To}** | {a.D:+0.0;-0.0} | {a.DB:+0.0;-0.0} | {AdoptSeats(a.F)} |");
        Console.WriteLine();
        Console.WriteLine($"情報セルの総和は **{adopted.Sum(a => a.To - a.From):+0;-0;0}** 増える見込み"
            + "（`compare` を測り直して確かめる）。");
        Console.WriteLine();
    }

    static string AdoptN(UnitDef? d) => d?.Name ?? "-";
    static string AdoptSeats(Formation f)
        => AdoptN(f[0]) + "/" + AdoptN(f[1]) + " | " + AdoptN(f[2]) + " | " + AdoptN(f[3]) + "/" + AdoptN(f[4]);

    /// <summary>狙（ガルドが前列 / セッキが後列）。**既存の作法。結果を見て足した条件ではない**。</summary>
    static bool AdoptIntent(Formation f)
    {
        foreach (var (slot, def) in f.Occupied())
        {
            if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
            if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
        }
        return true;
    }

    static bool AdoptSameFormation(Formation a, Formation b)
    {
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++) if (!ReferenceEquals(a[i], b[i])) return false;
        return true;
    }

    /// <summary>編成枠 0..4 への全順列（`Program.cs` の `SlotAssignments` の写し。召喚枠は含めない）。</summary>
    static IEnumerable<int[]> AdoptSlotAssignments(int memberCount)
    {
        var assign = new int[memberCount];
        var used = new bool[FormationRules.PlayableSlotCount];
        return Rec(0);

        IEnumerable<int[]> Rec(int depth)
        {
            if (depth == memberCount) { yield return (int[])assign.Clone(); yield break; }
            for (int slot = 0; slot < FormationRules.PlayableSlotCount; slot++)
            {
                if (used[slot]) continue;
                used[slot] = true;
                assign[depth] = slot;
                foreach (int[] a in Rec(depth + 1)) yield return a;
                used[slot] = false;
            }
        }
    }

    // ==================================================================================
    // check —— 第122期の自己検査（必須4項目 ＋ (a)〜(f)）
    // ==================================================================================
    static void AdoptCheck()
    {
        Console.WriteLine("# 第122期 —— 自己検査");
        Console.WriteLine();
        const int CheckSeeds = 20;

        // --- 必須1 -----------------------------------------------------------------------
        Console.WriteLine("## 必須1 —— `compare` 305 セルが `docs/balance.md` と 0 件（**既定経路**）");
        Console.WriteLine();
        var want = BalanceCells();
        int cells = 0, diff = 0;
        foreach (var (name, f) in Presets.Compare)
        {
            if (!want.TryGetValue(name, out double[]? exp)) { Console.WriteLine($"- **行名が引けない: {name}**"); continue; }
            for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
            {
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
                double got = wins * 100.0 / Seeds;
                cells++;
                if (Math.Abs(got - exp[st]) > 0.049)
                {
                    diff++;
                    Console.WriteLine($"- **差分: {name} 第{st + 1}波 {exp[st]:F1}% → {got:F1}%**");
                }
            }
        }
        Console.WriteLine($"- {cells} セル中 **{diff} 件**の差分。{(diff == 0 ? "**○**" : "**×**")}");
        Console.WriteLine("- **既定を変えたコミットの直後は、動いた行のセルだけが差分に出る**"
            + "（`docs/` を再生成すれば 0 件に戻る。§3 のコミット 5）。");
        Console.WriteLine();

        // --- (a) A1 == 第121期 S2 ----------------------------------------------------------
        Console.WriteLine("## (a) A1 が第121期 S2 と一致する（Q1 の抜き取り）");
        Console.WriteLine();
        int aCells = 0, aDiff = 0;
        foreach (var (_, f) in Presets.Compare.Concat(Presets.Cross))
            foreach (int wv in Waves)
            {
                int x = 0, y = 0;
                for (int seed = 0; seed < CheckSeeds; seed++)
                {
                    if (AdoptBattle(f, EnemyCatalog.Stages[wv].Enemy, seed, 1).PlayerWon) x++;
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[wv].Enemy, seed, verbose: false,
                                         spillWound: SpillVers[2].R, wound: V0).PlayerWon) y++;
                }
                aCells++;
                if (x != y) aDiff++;
            }
        Console.WriteLine($"- {aCells} セル中 **{aDiff} 件**の差分。{(aDiff == 0 ? "**○**" : "**×**")}");
        Console.WriteLine();

        // --- (b)(c) 帳簿 --------------------------------------------------------------------
        Console.WriteLine("## (b) A3 で味方側に傷が 1 つも書かれない ／ (c) 読み手2枚が 0 回");
        Console.WriteLine();
        var ally = new long[BattleContext.WoundRouteCount];
        var foeW = new long[BattleContext.WoundRouteCount];
        long mendRead = 0, gatherWrite = 0, a0Ally = 0;
        int seen = 0;
        foreach (var (_, f) in Presets.Compare.Concat(Presets.Cross))
            foreach (int wv in Waves)
                for (int seed = 0; seed < CheckSeeds; seed++)
                {
                    seen++;
                    WoundLedger w = AdoptBattle(f, EnemyCatalog.Stages[wv].Enemy, seed, 2).Wounds;
                    for (int i = 0; i < ally.Length; i++) { ally[i] += w.WriteAlly[i]; foeW[i] += w.WriteFoe[i]; }
                    mendRead += w.ReadWounds[(int)WoundReader.Mend];
                    gatherWrite += w.WriteAlly[(int)WoundRoute.Gather] + w.WriteFoe[(int)WoundRoute.Gather];
                    WoundLedger w0 = AdoptBattle(f, EnemyCatalog.Stages[wv].Enemy, seed, 0).Wounds;
                    a0Ally += w0.WriteAlly.Sum();
                }
        Console.WriteLine($"**{seen:N0} 戦。**");
        Console.WriteLine();
        Console.WriteLine("| 経路 | A3 味方へ | A3 敵へ |");
        Console.WriteLine("|---|--:|--:|");
        for (int i = 0; i < ally.Length; i++)
            if (ally[i] + foeW[i] > 0 || i < RouteName.Length)
                Console.WriteLine($"| {(i < RouteName.Length ? RouteName[i] : i.ToString())} | {ally[i]:N0} | {foeW[i]:N0} |");
        Console.WriteLine();
        long allyAll = ally.Sum();
        Console.WriteLine($"- (b) A3 の味方側の傷 = **{allyAll:N0}**（A0 では {a0Ally:N0}）。{(allyAll == 0 ? "**○**" : "**×**")}");
        if (allyAll != 0)
            Console.WriteLine("  - **書き手を名指しする**: "
                + string.Join(" / ", Enumerable.Range(0, ally.Length).Where(i => ally[i] > 0)
                    .Select(i => $"{(i < RouteName.Length ? RouteName[i] : i.ToString())} {ally[i]:N0}")));
        Console.WriteLine($"- (c) 繕いが読んだ傷 = **{mendRead:N0}**（線 0）／ 引き取りが書いた傷 = **{gatherWrite:N0}**（線 0）。"
            + (mendRead == 0 && gatherWrite == 0 ? "**○**" : "**×**"));
        Console.WriteLine();

        // --- (d) 旧文 ------------------------------------------------------------------------
        Console.WriteLine("## (d) ノノ・ガルドの1文が `git log` から引いた旧文と一致する（**手で書き直していない**）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 欄 | 現在の実装 | git から引いた旧文 | 一致 |");
        Console.WriteLine("|---|---|---|---|:-:|");
        int dOk = 0, dTot = 0;
        foreach (var (u, plusIntro, minusIntro) in new (UnitDef, string, string?)[]
                 {
                     (UnitCatalog.Gald, "味方への攻撃も傷も肩代わりし、その傷のぶん強くなる", null),
                     (UnitCatalog.Nono, "その味方に傷があれば、傷1つにつき繕いが増える", "繕うとその傷はひとつ塞がる。"),
                 })
        {
            string? rev = ParentOfIntroducing(plusIntro);
            if (rev is null) { Console.WriteLine($"| {u.Name} | Plus | {u.PlusText} | **引けない** | **×** |"); dTot++; continue; }
            var (p, m) = TextsAt(rev, u.Id);
            dTot++;
            bool ok = p is not null && p == u.PlusText;
            if (ok) dOk++;
            Console.WriteLine($"| {u.Name} | Plus | {u.PlusText} | {p ?? "**引けない**"} | {(ok ? "**○**" : "**×**")} |");
            if (minusIntro is null)
            {
                dTot++;
                bool okm = m is not null && m == u.MinusText;
                if (okm) dOk++;
                Console.WriteLine($"| {u.Name} | Minus | {u.MinusText} | {m ?? "**引けない**"} | {(okm ? "**○**" : "**×**")} |");
            }
            else
            {
                // 第106期が同じ行の別の箇所を変えているので、**逐語では一致しない**のが正しい。
                dTot++;
                bool okm = m is not null && !u.MinusText.Contains(minusIntro)
                           && m.Replace("繕った分だけ", "繕った量の半分だけ") == u.MinusText;
                if (okm) dOk++;
                Console.WriteLine($"| {u.Name} | Minus | {u.MinusText} | {m ?? "**引けない**"} "
                    + $"| {(okm ? "**○**（第92期の節だけを外し、第106期の「半分」は保つ）" : "**×**")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"- **{dOk} / {dTot} 一致。**{(dOk == dTot ? "**○**" : "**×**")}"
            + "（既定を変える前は「旧文と一致しない」が正しい ＝ この検査はコミット 3 の後に通る）");
        Console.WriteLine();

        // --- (e) 走査と索引 --------------------------------------------------------------------
        Console.WriteLine($"## (e) 走査件数を出して 0 件で止める ／ `{"par" + "tial"} class` を使っていない（第121期の索引の事故）");
        Console.WriteLine();
        string self = File.ReadAllText(Path.Combine(_root!, "BattleSim", "Wound2.cs"));
        // **探す文字列は実行時に組み立てる**——リテラルで書くと、この検査の行そのものを数える
        // （第92期の自己検査 (f) が最初 3 件と出したのと同じ穴）。
        bool hasPartial = Regex.IsMatch(self, "par" + "tial" + @"\s+class");
        bool hasClass = self.Contains("static class Wound2Diag");
        Console.WriteLine($"- `BattleSim/Wound2.cs` に `{"par" + "tial"} class` = {(hasPartial ? "**あり（×）**" : "無し（**○**）")}"
            + $" ／ `static class Wound2Diag` = {(hasClass ? "あり（**○**）" : "**無し（×）**")}");
        Console.WriteLine("- `wound2 spill phase0 adopt` は走査件数を出し、0 件ならその場で `return` する。");
        Console.WriteLine();

        // --- (f) 情報帯 -------------------------------------------------------------------------
        Console.WriteLine("## (f) 判定に使ったセルが情報帯に入っている");
        Console.WriteLine();
        Console.WriteLine("`wound2 spill adopt` の表A が「A0 と A3 の両方とも床／両方とも天井」の行数を出す。");
        Console.WriteLine("`docs/balance.md` 側の分布は下のとおり:");
        Console.WriteLine();
        ScreenFromBalance();

        // --- 必須3 ---------------------------------------------------------------------------
        Console.WriteLine("## 必須3 —— 触っていないノブの既定が動いていない（**動くのは 3 本だけ**）");
        Console.WriteLine();
        Console.WriteLine("| ノブ | 現在の既定 | 第122期の予定 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine($"| `SpillWoundRule.Default` | `{SpillWoundRule.Default}` | `SpillWoundRule {{ Enabled = False, Scope = All }}` |");
        Console.WriteLine($"| `MendRule.Default` | `{MendRule.Default}` | `MendRule {{ Side = Plain }}` |");
        Console.WriteLine($"| `GatherRule.Default` | `{GatherRule.Default}` | `GatherRule {{ Enabled = False }}` |");
        Console.WriteLine($"| `WoundRule.Default` | `{WoundRule.Default}` | **不変** |");
        Console.WriteLine($"| `SoakRule.Default` | `{SoakRule.Default}` | **不変** |");
        Console.WriteLine($"| `MenderCostRule.Default` | `{MenderCostRule.Default}` | **不変**（第106期） |");
        Console.WriteLine($"| `SutureRule.Default` | `{SutureRule.Default}` | **不変** |");
        Console.WriteLine($"| `ThornRule.Default` | `{ThornRule.Default}` | **不変** |");
        Console.WriteLine();
        Console.WriteLine("- 残り全部は `docs/rules.md` の差分で示す（`derive rules` を再生成して `git diff docs/`）。");
        Console.WriteLine();

        // --- 必須4 ---------------------------------------------------------------------------
        int pick = Regex.Matches(Src("Traits.cs"), @"(?<!///.{0,200})\bPickOne\(").Count
                 + Regex.Matches(Src("BattleEngine.cs"), @"\bPickOne\(").Count;
        Console.WriteLine("## 必須4 —— `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        Console.WriteLine($"- `BattleCore` の `PickOne(` の出現 = **{pick} 箇所**（第120・121期と同数なら ○）。");
        Console.WriteLine("- **第122期が `BattleCore` で触るのは既定値 3 行と `PlusText` / `MinusText` の 3 行だけ**"
            + "（`git diff BattleCore/` がその 6 行に収まる）。");
        Console.WriteLine();
    }
}
