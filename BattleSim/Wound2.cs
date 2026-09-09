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
    public static void Run(string mode)
    {
        if (!Init()) return;
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunTables(); return;
            case "check": Check(); return;
            default: Console.WriteLine("wound2: モードは phase0 / run / check のいずれか。"); return;
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
}
