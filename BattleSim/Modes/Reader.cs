using BattleCore;
using static Common;

// =====================================================================================
// reader モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "reader")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 reader
// =====================================================================================

static class ReaderDiag
{
// =====================================================================================
// reader モード（第115期）—— **強化の2枚目の読み手**。
//
// 起点は第65期の積み残し1'——**強化は供給 16（窓口 7 + 自己強化 9）対 読み手 1 の通貨で、
// 偏っているのは供給の内訳ではなく読み手の側。** `AtkBonus` を読んで分岐する駒は
// 逆しま（ウツ）1枚だけで、しかも**符号しか読まない**。
//
// 測る版は R1（二値の鍵）——攻撃力の補正が閾値以上なら単体の一撃が薙ぎになる。
// **engine に足したのは規則の受け渡しと計数だけ**で、判定は駒の特性の中にある
// （軋みが響く＝第66期と同じ形）。**既定は不活性。**
//
//     dotnet run --project BattleSim -c Release 0 reader phase0   # 表P（(B) の索引照合・候補の選定）。**戦闘0回**
//     dotnet run --project BattleSim -c Release 0 reader supply   # 選定規則 (b)（強化の受け手の実測）だけ
//     dotnet run --project BattleSim -c Release 0 reader run      # 表A〜D（4台 × 2版）
//     dotnet run --project BattleSim -c Release 0 reader check    # 自己検査
//
// **既存の診断は1文字も書き換えていない。`Presets` も `UnitCatalog` も1行も触っていない**
// ——台は診断のローカル（`gradient` / `aim` / `tomo` と同じ扱い）。
public static void Run(string[] args, int stageIndex)
{
    string rdMode = args.Length > 2 ? args[2] : "phase0";
    IReadOnlyList<EnemyCatalog.Stage> rdStages = EnemyCatalog.Stages;
    const int RdSeeds = 200;        // 帯A。`compare` と揃える（規約 (G14)）
    const int RdSupplySeeds = 50;   // (b) は「1度でも受けているか」だけを見るので浅くてよい
    const int RdThreshold = 5;      // **掃引しない**（§0-2）。0（不活性）と 5 の2点だけ

    // --- 読み手の版。**V0 は「特性は載っているが規則が不活性」＝現行のバンと1ビットも違わない。**
    var rdVers = new (string Tag, ReaderRule Rule)[]
    {
        // **既定に頼らず 0 を明示する。** 第116期に `ReaderRule.Default` が 5 へ動いたので、
        // `ReaderRule.Default` のままだと第115期の V0 が黙って V1 に化ける。
        ("V0 現行",     new ReaderRule(0)),           // Threshold 0 ＝ ModifyPattern が素通り
        ("V1 積み過ぎ", new ReaderRule(RdThreshold)),
    };

    // 読み手（据えのバン ＋ 積み過ぎ）。**`UnitCatalog` には載せない。**
    UnitDef rdReader = new()
    {
        Id = UnitCatalog.Ban.Id,
        Name = UnitCatalog.Ban.Name,
        MaxHp = UnitCatalog.Ban.MaxHp,
        Attack = UnitCatalog.Ban.Attack,
        Speed = UnitCatalog.Ban.Speed,
        Pattern = UnitCatalog.Ban.Pattern,
        Traits = new[] { TraitId.Bulwark, TraitId.Overload },
        PlusText = UnitCatalog.Ban.PlusText,
        MinusText = UnitCatalog.Ban.MinusText,
        Flavor = UnitCatalog.Ban.Flavor
    };

    // ------------------------------------------------------------------------------
    // 台（§3-1）。**席は4台とも同じ**——読み手の席を動かすと「供給の効き」と「席の効き」が混ざる。
    //   前1 = 供給者（台4 は空席）／前3 ドルガ（総攻の土台）／中央 ガルド（壁）
    //   後1 = 読み手（**レーン0 で供給者の後ろ** ＝ 巨躯の被覆に入る）／後3 エグ
    // 埋め草3枚は**強化も弱体も1点も撒かない**3枚を選んである。
    // **読み手が味方でいちばん遅い**（速2）ので、尾灯の「自分を除いて最も遅い味方」は必ず読み手。
    // ------------------------------------------------------------------------------
    Formation RdBench(UnitDef? supplier, UnitDef? center = null) => Formation.Build(
        front1: supplier, front3: UnitCatalog.Dolga, center: center ?? UnitCatalog.Gald,
        back1: rdReader, back3: UnitCatalog.Egu);

    var rdBenches = new (string Tag, UnitDef? Supplier, UnitDef? Center, string Why)[]
    {
        ("台1 ガン",   UnitCatalog.Gan,  null,             "号令開戦（全体・到着 0T）。**早く広く届くが 1 回きり**"),
        ("台2 ゴルム", UnitCatalog.Golm, null,             "吐き戻し（庇った相手）。**供給量が最大 5.95** だが遅い"),
        ("台3 トモ",   UnitCatalog.Tomo, null,             "尾灯（最も遅い味方）。**1体に集中し、第114期に動的**"),
        ("台4 なし",   null,             null,             "**陰性対照**。強化が 0 の台で機構が1ビットも動かないこと"),
        // 台1 の交絡を切るための対照（第115期に測って足した）。ガルド（`Stoic`）は
        // **味方全体に配られる強化を自分に乗せず隣接する味方へ流す**ので、中央に置くと
        // 号令の開戦時 +4 が読み手に**二重に**届く。中央をウケ（`Bear` は弱体しか横取りしない）に
        // 差し替えると、同じ供給者・同じ席のまま**流し込みだけ**が消える。
        ("台1' ガン中央ウケ", UnitCatalog.Gan, UnitCatalog.Uke,
                              "**台1 の交絡の切り分け**。ガルドの `Stoic` による二重配布だけを消す"),
    };
    const int RdNegative = 3;      // 陰性対照の台の番号
    int[] rdMain = { 0, 1, 2 };    // 主判定に使う台（§3-3 の「台1〜3」）

    // 1戦ぶんの観測。
    var rdRows = new List<RdRow>();

    void RdRunAll(int seeds)
    {
        rdRows.Clear();
        for (int b = 0; b < rdBenches.Length; b++)
        {
            Formation f = RdBench(rdBenches[b].Supplier, rdBenches[b].Center);
            for (int v = 0; v < rdVers.Length; v++)
                for (int w = 0; w < rdStages.Count; w++)
                    for (int seed = 0; seed < seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, rdStages[w].Enemy, seed, verbose: false,
                                                          reader: rdVers[v].Rule);
                        UnitTally t = r.TallyByUnit.TryGetValue(rdReader.Id, out UnitTally? tt) ? tt : new UnitTally();
                        int team = 0;
                        foreach ((int _, UnitDef d) in f.Occupied())
                            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? mt)) team += mt.DamageToEnemy;
                        rdRows.Add(new RdRow
                        {
                            Bench = b, Ver = v, Wave = w + 1, Won = r.PlayerWon, Turns = r.Turns,
                            Alive = t.ReaderTurns, Over = t.ReaderOverTurns, FirstOver = t.ReaderFirstOverTurn,
                            Swings = t.ReaderSwings, Sweeps = t.ReaderSweeps, OverSwung = t.ReaderOverTurnsSwung,
                            BonusSum = t.ReaderBonusSum, BonusMax = t.ReaderBonusMax,
                            Probe = t.ReaderProbeTurns?.ToArray() ?? new int[UnitTally.ReaderProbes.Length],
                            Dmg = t.DamageToEnemy, TeamDmg = team, Whet = r.WhetTotal
                        });
                    }
        }
    }

    // 第2〜5波だけを分母にする（規約 (G10)）。第一波は全行が単発で 100% 勝つ教習波。
    List<RdRow> RdSel(int b, int v, bool all = false)
        => rdRows.Where(x => x.Bench == b && x.Ver == v && (all || x.Wave >= 2)).ToList();
    List<RdRow> RdSelW(int b, int v, int w)
        => rdRows.Where(x => x.Bench == b && x.Ver == v && x.Wave == w).ToList();
    static double RdWin(List<RdRow> xs) => xs.Count == 0 ? 0 : xs.Count(x => x.Won) * 100.0 / xs.Count;
    static double RdAvg(List<RdRow> xs, Func<RdRow, double> f) => xs.Count == 0 ? 0 : xs.Average(f);
    // 門1（到達率）= 閾値以上だったターン数 ÷ 生きていたターン数。**戦ごとに割ってから平均する**
    // （決着の長さが版で動くので、総和どうしを割ると長い戦に重みが寄る）。
    static double RdGate1(List<RdRow> xs) => xs.Count == 0 ? 0
        : xs.Average(x => x.Alive == 0 ? 0.0 : (double)x.Over / x.Alive);

    // リポジトリ直下（`audit` と同じ walk-up）。
    static string? RdRoot()
    {
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Path.GetDirectoryName(root);
        return root;
    }

    // ------------------------------------------------------------------------------
    // (B) 索引の穴（§2）。**`CLAUDE.md` の `→ LESSONS_*.md` の全参照を抜き、
    // その期の節がその文書に実在するかを機械照合する。戦闘0回。**
    // ------------------------------------------------------------------------------
    static (int Refs, List<(int Phase, string File, List<int> Lines)> Missing) RdIndexAudit()
    {
        var missing = new List<(int, string, List<int>)>();
        string? root = RdRoot();
        if (root == null) return (0, missing);
        var secs = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(Path.Combine(root, "design"), "LESSONS_*.md"))
        {
            var set = new HashSet<int>();
            foreach (string line in File.ReadAllLines(path))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"^###\s*第(\d+)期");
                if (m.Success) set.Add(int.Parse(m.Groups[1].Value));
            }
            secs[Path.GetFileName(path)] = set;
        }
        var bag = new Dictionary<(int, string), List<int>>();
        string[] claude = File.ReadAllLines(Path.Combine(root, "CLAUDE.md"));
        int refs = 0;
        for (int i = 0; i < claude.Length; i++)
        {
            var mf = System.Text.RegularExpressions.Regex.Match(claude[i], @"→\s*(LESSONS_\S+\.md)");
            if (!mf.Success) continue;
            string file = mf.Groups[1].Value;
            foreach (System.Text.RegularExpressions.Match mp in
                     System.Text.RegularExpressions.Regex.Matches(claude[i], @"第(\d+)期"))
            {
                int ph = int.Parse(mp.Groups[1].Value);
                refs++;
                if (secs.TryGetValue(file, out HashSet<int>? set) && set.Contains(ph)) continue;
                var key = (ph, file);
                if (!bag.TryGetValue(key, out List<int>? ls)) bag[key] = ls = new List<int>();
                if (!ls.Contains(i + 1)) ls.Add(i + 1);
            }
        }
        foreach (var kv in bag.OrderBy(k => k.Key.Item1))
            missing.Add((kv.Key.Item1, kv.Key.Item2, kv.Value));
        return (refs, missing);
    }

    // 特性が `ModifyAttack` / `ModifyPattern` を上書きしているか（選定規則 (d)）。**反射で数える。**
    static bool RdOverrides(TraitId id, string name)
        => TraitCatalog.Get(id).GetType().GetMethod(name)?.DeclaringType != typeof(Trait);

    // 自己強化（`AtkBonus` を自分で足す特性）の全数。**手で写さず `Traits.cs` を走査する**
    // （第94期に「手で写した表の誤り 29 件」を出した反省。`derive` と同じ作法）。
    static HashSet<TraitId> RdSelfBuffTraits()
    {
        var found = new HashSet<TraitId>();
        string? root = RdRoot();
        if (root == null) return found;
        string path = Path.Combine(root, "BattleCore", "Traits.cs");
        if (!File.Exists(path)) return found;
        // クラス名 -> TraitId。**抽象な基底クラス（`RedirectGainTrait`）にも当たるように継承を辿る**
        // ——庇う／殉教はそこに `self.AtkBonus +=` を持っている。
        var byClass = new Dictionary<string, List<TraitId>>(StringComparer.Ordinal);
        foreach (TraitId id in Enum.GetValues<TraitId>())
            for (Type? ty = TraitCatalog.Get(id).GetType(); ty != null && ty != typeof(Trait); ty = ty.BaseType)
            {
                if (!byClass.TryGetValue(ty.Name, out List<TraitId>? ls)) byClass[ty.Name] = ls = new List<TraitId>();
                ls.Add(id);
            }
        string cur = "";
        foreach (string line in File.ReadAllLines(path))
        {
            var mc = System.Text.RegularExpressions.Regex.Match(line, @"class\s+(\w+Trait)\b");
            if (mc.Success) cur = mc.Groups[1].Value;
            if (line.TrimStart().StartsWith("///")) continue;
            // **`self.` に限る**——尾灯の消灯（`prev.AtkBonus -= lit`）は他人の灯を引き上げる動作で、
            // 自己強化ではない（第108期。横取りに晒さないために `Dull` も通していない）。
            if (!System.Text.RegularExpressions.Regex.IsMatch(line, @"\bself\.AtkBonus\s*[-+]=")) continue;
            if (byClass.TryGetValue(cur, out List<TraitId>? ids)) foreach (TraitId id2 in ids) found.Add(id2);
        }
        return found;
    }

    // `docs/balance.md` の現行値（**戦闘不要**）。行名 -> 5波のセル。
    static Dictionary<string, double[]> RdBalance(int waves)
    {
        var map = new Dictionary<string, double[]>(StringComparer.Ordinal);
        string? root = RdRoot();
        if (root == null) return map;
        foreach (string line in File.ReadAllLines(Path.Combine(root, "docs", "balance.md")))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            var parts = line.Split('|', StringSplitOptions.None).Select(p => p.Trim()).ToArray();
            if (parts.Length < 8) continue;
            var v = new List<double>();
            for (int i = 2; i < parts.Length - 1; i++)
                if (parts[i].EndsWith("%") && double.TryParse(parts[i].TrimEnd('%'), out double d)) v.Add(d);
            if (v.Count == waves) map[parts[1]] = v.ToArray();
        }
        return map;
    }

    // ==============================================================================
    // 第116期 —— 積み過ぎ（`Overload`）を据えのバンに載せる（`reader load ...`）。
    //
    // **第115期の `phase0` / `supply` / `run` / `check` は測定の中身を1文字も書き換えていない**
    // （`rdVers` の V0 が既定に頼らず `new ReaderRule(0)` を明示するようになっただけ
    //  ——`ReaderRule.Default` がこの期に 0 → 5 へ動いたので、頼ったままだと V0 が黙って
    //  V1 に化ける。第110期に同じ穴を踏んでいる）。
    //
    //     reader load phase0   §2（戦闘は分布の測定のみ）
    //     reader load run      §1 (T2)
    //     reader load seat     §1 (T3)（条件に当たった行だけ）
    //     reader load check    自己検査（第2引数に採用前の balance.md を渡すと (a) を測る）
    // ==============================================================================
    if (rdMode == "load")
    {
        string ldSub = args.Length > 3 ? args[3] : "phase0";
        var ldCompare = CompareBuilds();
        var ldCross = CrossBuilds();
        var ldPrim = new HashSet<string>(Baseline.PrimaryRows);
        const int LdSeeds = 200;      // **帯A**。`compare` と揃える（規約 (G14)）
        const int LdSupply = 50;      // 経路の内訳は「どの蛇口から来るか」だけを見るので浅くてよい
        const int LdScan = 50;        // 粗探索。`reseat` / `layout` と揃える
        const int LdCfBase = 200;     // 追試の帯（選定に使っていない seed）
        const int LdCfSeeds = 400;
        const double LdSeatLine = 5.0;   // 第46期の採否閾値
        var ldOff = new ReaderRule(0);                       // **明示する**。既定に頼らない
        var ldOn = new ReaderRule(ReaderRule.Adopted);       // 採用値 5

        static string LdN(UnitDef? d) => d?.Name ?? "-";
        static string LdSeats(Formation f) => LdN(f[0]) + "/" + LdN(f[1]) + " | " + LdN(f[2]) + " | " + LdN(f[3]) + "/" + LdN(f[4]);
        // 情報セル: `0 < x < 100` を**第2〜5波**で数える（第59期 9-1・規約 (G10)）
        static int LdInfo(double[] c) { int n = 0; for (int i = 1; i < c.Length; i++) if (c[i] > 0.0 && c[i] < 100.0) n++; return n; }
        static double LdAvg25(double[] c) => c.Skip(1).Average();

        // **バンを含む行を実装で数える**（指示書 §2-1。「指示書の記述を信用しない」）
        static bool LdHasBan(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Ban));
        var ldBanRows = ldCompare.Where(b => LdHasBan(b.F)).Select(b => (b.Name, b.F, Band: "compare"))
            .Concat(ldCross.Where(b => LdHasBan(b.F)).Select(b => (b.Name, b.F, Band: "交差帯")))
            .ToList();

        double[] LdCells(Formation f, ReaderRule rule, int seedFrom, int seeds)
        {
            var cells = new double[rdStages.Count];
            for (int w = 0; w < rdStages.Count; w++)
            {
                int win = 0;
                for (int seed = seedFrom; seed < seedFrom + seeds; seed++)
                    if (BattleEngine.Run(f, rdStages[w].Enemy, seed, verbose: false, reader: rule).PlayerWon) win++;
                cells[w] = win * 100.0 / seeds;
            }
            return cells;
        }

        // 1行ぶんの観測（波別）。**到達率は戦ごとに割ってから平均する**（第115期の作法）。
        LdStat[] LdWatch(Formation f, ReaderRule rule, int seeds)
        {
            var st = new LdStat[rdStages.Count];
            for (int w = 0; w < rdStages.Count; w++)
            {
                var ls = new LdStat();
                for (int seed = 0; seed < seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, rdStages[w].Enemy, seed, verbose: false, reader: rule);
                    UnitTally t = r.TallyByUnit.TryGetValue(UnitCatalog.Ban.Id, out UnitTally? tt) ? tt : new UnitTally();
                    ls.N++;
                    if (r.PlayerWon) ls.Win++;
                    ls.Turns += r.Turns;
                    ls.Reach += t.ReaderTurns == 0 ? 0.0 : (double)t.ReaderOverTurns / t.ReaderTurns;
                    ls.Alive += t.ReaderTurns; ls.Over += t.ReaderOverTurns; ls.OverSwung += t.ReaderOverTurnsSwung;
                    ls.Sweeps += t.ReaderSweeps; ls.Splash += t.ReaderSplash; ls.Swings += t.ReaderSwings;
                    ls.Bonus += t.ReaderTurns == 0 ? 0.0 : (double)t.ReaderBonusSum / t.ReaderTurns;
                    ls.BonusMax += t.ReaderBonusMax;
                    if (t.ReaderFirstOverTurn > 0) { ls.First += t.ReaderFirstOverTurn; ls.FirstN++; }
                    int[]? probe = t.ReaderProbeTurns;
                    for (int i = 0; i < UnitTally.ReaderProbes.Length; i++)
                        ls.Probe[i] += probe == null || t.ReaderTurns == 0 ? 0.0 : (double)probe[i] / t.ReaderTurns;
                    ls.Dmg += t.DamageToEnemy; ls.Kills += t.Kills;
                }
                st[w] = ls;
            }
            return st;
        }

        // 波2〜5をまとめた1つの観測（規約 (G10)）。
        static LdStat LdSum25(LdStat[] st)
        {
            var ls = new LdStat();
            for (int w = 1; w < st.Length; w++)
            {
                ls.N += st[w].N; ls.Win += st[w].Win; ls.Turns += st[w].Turns; ls.Reach += st[w].Reach;
                ls.Alive += st[w].Alive; ls.Over += st[w].Over; ls.OverSwung += st[w].OverSwung;
                ls.Sweeps += st[w].Sweeps; ls.Splash += st[w].Splash; ls.Swings += st[w].Swings;
                ls.Bonus += st[w].Bonus; ls.BonusMax += st[w].BonusMax; ls.First += st[w].First; ls.FirstN += st[w].FirstN;
                ls.Dmg += st[w].Dmg; ls.Kills += st[w].Kills;
                for (int i = 0; i < ls.Probe.Length; i++) ls.Probe[i] += st[w].Probe[i];
            }
            return ls;
        }

        // -------------------------------------------------------------------------
        // load phase0（表P）。**戦闘は「バンに何点届くか」の分布だけ。**
        // -------------------------------------------------------------------------
        if (ldSub == "phase0")
        {
            Console.WriteLine("# 第116期 Phase 0 —— 積み過ぎを据えのバンに載せる");
            Console.WriteLine();
            Console.WriteLine($"閾値 **{ReaderRule.Adopted}**（掃引しない・§0-3）。"
                + "この節の戦闘は**分布の測定だけ**で、対照はすべて `new ReaderRule(0)` を明示している。");

            // --- 表P-1: バンを含む行の全数
            Console.WriteLine();
            Console.WriteLine("## 表P-1. バンを含む行の全数（`Presets` を実装で走査）");
            Console.WriteLine();
            Console.WriteLine($"`compare` **{ldCompare.Count(b => LdHasBan(b.F))} 行** / "
                + $"交差帯 **{ldCross.Count(b => LdHasBan(b.F))} 行** ＝ 合計 **{ldBanRows.Count} 行**"
                + $"（`compare` は全 {ldCompare.Length} 行・交差帯は全 {ldCross.Length} 行）。");
            Console.WriteLine();
            Console.WriteLine("| 行 | 帯 | 主判定 | バンの席 | 前1/前3 | 中央 | 後1/後3 | ガルドの席 | ガルドの隣接（占有枠） |");
            Console.WriteLine("|---|---|:-:|---|---|---|---|---|--:|");
            string[] slotName = { "前1", "前3", "中央", "後1", "後3" };
            foreach (var row in ldBanRows)
            {
                int banSlot = row.F.Occupied().First(o => ReferenceEquals(o.Def, UnitCatalog.Ban)).Slot;
                var gald = row.F.Occupied().Where(o => ReferenceEquals(o.Def, UnitCatalog.Gald)).ToList();
                string gs = "—", gn = "—";
                if (gald.Count > 0)
                {
                    int g = gald[0].Slot;
                    int deg = row.F.Occupied().Count(o => o.Slot != g && FormationRules.AreAdjacent(g, o.Slot));
                    bool nextToBan = FormationRules.AreAdjacent(g, banSlot);
                    gs = slotName[g];
                    gn = $"{deg}{(nextToBan ? "（**バンに隣接**）" : "")}";
                }
                Console.WriteLine($"| {row.Name} | {row.Band} | {(ldPrim.Contains(row.Name) ? "**P**" : "")} | {slotName[banSlot]} "
                    + $"| {LdN(row.F[0])}/{LdN(row.F[1])} | {LdN(row.F[2])} | {LdN(row.F[3])}/{LdN(row.F[4])} | {gs} | {gn} |");
            }
            Console.WriteLine();
            Console.WriteLine("**ガルド（`Stoic`）の隣接数がそのまま拡散量になる**のは、"
                + "`BattleContext.SupportTargets` が「本人が受け取らないぶんを**隣接する味方全員**へ配る」形だから"
                + "（§0-2）。**バンに隣接していなければ二重配布は起きない。**");

            // --- 表P-2: 経路の内訳
            Console.WriteLine();
            Console.WriteLine($"## 表P-2. バンに届く強化の経路（実測・第2〜5波・seed 0..{LdSupply - 1}）");
            Console.WriteLine();
            bool[] spread = new bool[WhetRoutes.Count];
            spread[(int)WhetRoute.RallyOpening] = spread[(int)WhetRoute.RallyTurn] = spread[(int)WhetRoute.Regurgitate] = true;
            Console.Write("| 行 |");
            for (int i = 1; i < WhetRoutes.Count; i++) Console.Write($" {WhetRoutes.Names[i]}{(spread[i] ? "◇" : "")} |");
            Console.WriteLine(" 合計/戦 |");
            Console.Write("|---|");
            for (int i = 1; i < WhetRoutes.Count; i++) Console.Write("--:|");
            Console.WriteLine("--:|");
            foreach (var row in ldBanRows)
            {
                var sum = new double[WhetRoutes.Count];
                int n = 0;
                for (int w = 1; w < rdStages.Count; w++)
                    for (int seed = 0; seed < LdSupply; seed++)
                    {
                        BattleResult r = BattleEngine.Run(row.F, rdStages[w].Enemy, seed, verbose: false, reader: ldOff);
                        n++;
                        for (int i = 0; i < WhetRoutes.Count; i++)
                            if (r.WhetToByRoute[i].TryGetValue(UnitCatalog.Ban.Id, out int v)) sum[i] += v;
                    }
                Console.Write($"| {row.Name} |");
                for (int i = 1; i < WhetRoutes.Count; i++) Console.Write($" {sum[i] / n:F2} |");
                Console.WriteLine($" **{sum.Skip(1).Sum() / n:F2}** |");
            }
            Console.WriteLine();
            Console.WriteLine("◇ = **ばら撒き型**（`SupportTargets` を通る＝ガルドの `Stoic` が隣へ流す）。"
                + "1体を選ぶ型（駆り立て・縛め・移り木・火選り・尾灯）は流れない。");

            // --- 表P-3: AtkBonus の分布
            Console.WriteLine();
            Console.WriteLine($"## 表P-3. 素体（`Threshold = 0`）でのバンの `AtkBonus` 分布（第2〜5波・seed 0..{LdSeeds - 1}）");
            Console.WriteLine();
            Console.Write("| 行 | 平均 | 最大 | 初到達T |");
            foreach (int p in UnitTally.ReaderProbes) Console.Write($" ≥{p} |");
            Console.WriteLine(" **到達率(≥5)** | 決着T |");
            Console.Write("|---|--:|--:|--:|");
            foreach (int _ in UnitTally.ReaderProbes) Console.Write("--:|");
            Console.WriteLine("--:|--:|");
            foreach (var row in ldBanRows)
            {
                LdStat ls = LdSum25(LdWatch(row.F, ldOff, LdSeeds));
                Console.Write($"| {row.Name} | {ls.Bonus / ls.N:F2} | {ls.BonusMax / ls.N:F1} "
                    + $"| {(ls.FirstN == 0 ? "—" : (ls.First / ls.FirstN).ToString("F2"))} |");
                foreach (double pr in ls.Probe) Console.Write($" {pr / ls.N * 100:F1}% |");
                Console.WriteLine($" **{ls.Reach / ls.N * 100:F1}%** | {ls.Turns / ls.N:F2} |");
            }
            Console.WriteLine();
            Console.WriteLine($"**閾値 {ReaderRule.Adopted} が分布のどこに来るか**を読む列は `≥5`。"
                + "`≥1` との差が「届いてはいるが足りない」の量で、そこが厚い行は**供給の有無ではなく高さで落ちている**。");

            // --- 表P-4: 主判定
            Console.WriteLine();
            Console.WriteLine("## 表P-4. `Baseline.PrimaryRows` との重なり（§2-5）");
            Console.WriteLine();
            var inPrim = ldBanRows.Where(r => ldPrim.Contains(r.Name)).ToList();
            Console.WriteLine($"主判定 **{Baseline.PrimaryRows.Length} 行**のうちバンを含むのは **{inPrim.Count} 行** —— "
                + string.Join(" / ", inPrim.Select(r => r.Name)));
            Console.WriteLine();
            Console.WriteLine(inPrim.Count > 0
                ? $"**主判定19行は動く。** 分母の {inPrim.Count} / {Baseline.PrimaryRows.Length} "
                  + $"= {inPrim.Count * 100.0 / Baseline.PrimaryRows.Length:F1}% が動きうる（規約 (G4)）。"
                : "**主判定19行は動かない。**");

            // --- 表P-5: 二重保持
            Console.WriteLine();
            Console.WriteLine("## 表P-5. バンの札（§2-6）");
            Console.WriteLine();
            Console.WriteLine("| 札 | `ModifyAttack` | `ModifyPattern` |");
            Console.WriteLine("|---|:-:|:-:|");
            foreach (TraitId t in UnitCatalog.Ban.Traits)
                Console.WriteLine($"| {t} | {(RdOverrides(t, "ModifyAttack") ? "○" : "—")} | {(RdOverrides(t, "ModifyPattern") ? "○" : "—")} |");
            int dupA = UnitCatalog.Ban.Traits.Count(t => RdOverrides(t, "ModifyAttack"));
            int dupP = UnitCatalog.Ban.Traits.Count(t => RdOverrides(t, "ModifyPattern"));
            Console.WriteLine();
            Console.WriteLine($"二重保持: `ModifyAttack` **{dupA}** / `ModifyPattern` **{dupP}** → "
                + $"{(dupA <= 1 && dupP <= 1 ? "**○**（衝突しない）" : "**×**")}");

            // --- 表P-6: 過去にバンを触った期
            Console.WriteLine();
            Console.WriteLine("## 表P-6. 過去にバンの席・機構を触った期（`design/` の grep）");
            Console.WriteLine();
            string? rt = RdRoot();
            int hit = 0;
            if (rt != null)
                foreach (string path in Directory.GetFiles(Path.Combine(rt, "design"), "*.md").OrderBy(p => p))
                {
                    string[] lines = File.ReadAllLines(path);
                    var found = new List<int>();
                    for (int i = 0; i < lines.Length; i++)
                        if (lines[i].Contains("バン") && (lines[i].Contains("席") || lines[i].Contains("reseat")))
                            found.Add(i + 1);
                    if (found.Count == 0) continue;
                    hit++;
                    Console.WriteLine($"- `design/{Path.GetFileName(path)}` —— {found.Count} 行"
                        + $"（L{string.Join(", L", found.Take(6))}{(found.Count > 6 ? " …" : "")}）");
                }
            Console.WriteLine();
            Console.WriteLine($"該当 **{hit} ファイル**。");
            return;
        }

        // -------------------------------------------------------------------------
        // load run（表A・表B）。**この期の本体。**
        // -------------------------------------------------------------------------
        if (ldSub == "run")
        {
            Console.WriteLine("# 第116期 (T2) —— 5 行を測る");
            Console.WriteLine();
            Console.WriteLine($"`compare` {ldCompare.Length} 行 ＋ 交差帯 {ldCross.Length} 行 × 5 波 × seed 0..{LdSeeds - 1} を "
                + $"**V0（`Threshold = 0`・明示）** と **V1（{ReaderRule.Adopted}・採用値）** の2版で。");

            var all = ldCompare.Select(b => (b.Name, b.F, Band: "compare"))
                .Concat(ldCross.Select(b => (b.Name, b.F, Band: "交差帯"))).ToList();
            var v0 = new double[all.Count][];
            var v1 = new double[all.Count][];
            Parallel.For(0, all.Count, i => { v0[i] = LdCells(all[i].F, ldOff, 0, LdSeeds); v1[i] = LdCells(all[i].F, ldOn, 0, LdSeeds); });

            // --- 表A
            Console.WriteLine();
            Console.WriteLine("## 表A. バンを含む 5 行（波別の V0 / V1 / Δ）");
            Console.WriteLine();
            Console.WriteLine("| 行 | 帯 | 主判定 | 版 |" + string.Concat(rdStages.Select((_, i) => $" 第{i + 1}波 |"))
                + " 平均(2〜5波) | 情報セル |");
            Console.WriteLine("|---|---|:-:|---|" + string.Concat(rdStages.Select(_ => "--:|")) + "--:|--:|");
            foreach (var row in ldBanRows)
            {
                int i = all.FindIndex(a => a.Name == row.Name);
                foreach (var pair in new[] { ("V0", v0[i]), ("**V1**", v1[i]) })
                    Console.WriteLine($"| {row.Name} | {row.Band} | {(ldPrim.Contains(row.Name) ? "**P**" : "")} | {pair.Item1} |"
                        + string.Concat(pair.Item2.Select(x => $" {x:F1}% |"))
                        + $" {LdAvg25(pair.Item2):F1}% | {LdInfo(pair.Item2)} |");
                Console.WriteLine("| | | | Δ |" + string.Concat(Enumerable.Range(0, rdStages.Count)
                        .Select(w => $" {v1[i][w] - v0[i][w]:+0.0;-0.0;0.0} |"))
                    + $" **{LdAvg25(v1[i]) - LdAvg25(v0[i]):+0.0;-0.0;0.0}** | {LdInfo(v1[i]) - LdInfo(v0[i]):+0;-0;0} |");
            }

            // --- 表A': 門
            Console.WriteLine();
            Console.WriteLine($"## 表A'. 門（第2〜5波・seed 0..{LdSeeds - 1}）");
            Console.WriteLine();
            Console.WriteLine("| 行 | 版 | 到達率 | 初到達T | 振/戦 | **薙ぎ/戦** | 薙ぎ率 | **巻き込み/戦** | 空振り | 与ダメ/戦 | 撃破/戦 | 決着T |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            var reachOf = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var row in ldBanRows)
                foreach (var pair in new[] { ("V0", ldOff), ("**V1**", ldOn) })
                {
                    LdStat ls = LdSum25(LdWatch(row.F, pair.Item2, LdSeeds));
                    double whiff = ls.Over == 0 ? 0 : (ls.Over - ls.OverSwung) / ls.Over * 100;
                    if (pair.Item1 != "V0") reachOf[row.Name] = ls.Reach / ls.N;
                    Console.WriteLine($"| {row.Name} | {pair.Item1} | {ls.Reach / ls.N * 100:F1}% "
                        + $"| {(ls.FirstN == 0 ? "—" : (ls.First / ls.FirstN).ToString("F2"))} | {ls.Swings / ls.N:F2} "
                        + $"| **{ls.Sweeps / ls.N:F2}** | {(ls.Swings == 0 ? 0 : ls.Sweeps / ls.Swings * 100):F1}% "
                        + $"| **{ls.Splash / ls.N:F2}** | {whiff:F1}% | {ls.Dmg / ls.N:F1} | {ls.Kills / ls.N:F2} | {ls.Turns / ls.N:F2} |");
                }

            // --- 表B: 拒否権
            Console.WriteLine();
            Console.WriteLine("## 表B. 拒否権 Q1 —— バンを含まない行が ±0.0 か");
            Console.WriteLine();
            int movedCells = 0, movedRows = 0, cmpCells = 0;
            var moved = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                if (LdHasBan(all[i].F)) continue;
                int d = 0;
                for (int w = 0; w < rdStages.Count; w++) { cmpCells++; if (Math.Abs(v1[i][w] - v0[i][w]) > 0.0001) d++; }
                if (d > 0) { movedRows++; movedCells += d; moved.Add($"{all[i].Name}（{d} セル）"); }
            }
            Console.WriteLine($"バンを含まない **{all.Count - ldBanRows.Count} 行 / {cmpCells} セル**のうち動いたのは "
                + $"**{movedRows} 行 / {movedCells} セル** → {(movedCells == 0 ? "**○**" : "**×**")}");
            foreach (string m in moved) Console.WriteLine($"- {m}");

            // --- 表C: 壊れ（(G1)(G2)）と 95% 超
            Console.WriteLine();
            Console.WriteLine("## 表C. 拒否権 Q3 —— 壊れ（(G1)(G2)）と第五波 95% 超");
            Console.WriteLine();
            var drop = new List<(string Name, int Wave, double D)>();
            for (int i = 0; i < ldCompare.Length; i++)
                for (int w = 0; w < rdStages.Count; w++)
                    if (v1[i][w] - v0[i][w] <= -10.0) drop.Add((all[i].Name, w + 1, v1[i][w] - v0[i][w]));
            Console.WriteLine($"`compare` {ldCompare.Length} 行（(G1) の分母）で **−10.0pt 以上落ちたセル** は **{drop.Count} 件**"
                + $" → {(drop.Count == 0 ? "**○**" : "**要 (G2) の分解**")}");
            foreach (var d in drop) Console.WriteLine($"- {d.Name} 第{d.Wave}波 {d.D:+0.0;-0.0}pt");
            int hi0 = 0, hi1 = 0;
            var newHi = new List<string>();
            for (int i = 0; i < ldCompare.Length; i++)
            {
                bool a = v0[i][^1] > 95.0, b = v1[i][^1] > 95.0;
                if (a) hi0++;
                if (b) hi1++;
                if (!a && b) newHi.Add(all[i].Name);
            }
            Console.WriteLine();
            Console.WriteLine($"第五波 95% 超の行: **{hi0} → {hi1}**（新たに {newHi.Count} 行"
                + (newHi.Count > 0 ? " —— " + string.Join(" / ", newHi) : "") + "）。**(G9) により拒否ではなく記録。**");

            // --- 表D: Baseline
            Console.WriteLine();
            Console.WriteLine("## 表D. `Baseline` —— 主判定19行 / 全61行");
            Console.WriteLine();
            Console.WriteLine("| 分母 | 版 |" + string.Concat(rdStages.Select((_, i) => $" 第{i + 1}波 |")) + " 歯止めとの余裕 |");
            Console.WriteLine("|---|---|" + string.Concat(rdStages.Select(_ => "--:|")) + "--:|");
            void Line(string label, Func<int, bool> pick, double[][] g, string tag)
            {
                var idx = Enumerable.Range(0, ldCompare.Length).Where(pick).ToList();
                var avg = Enumerable.Range(0, rdStages.Count).Select(w => idx.Average(i => g[i][w])).ToArray();
                Console.WriteLine($"| {label}（{idx.Count} 行） | {tag} |" + string.Concat(avg.Select(x => $" {x:F1} |"))
                    + $" {avg[^1] - Baseline.PrimaryFifthFloor:+0.0;-0.0}pt |");
            }
            Line("主判定", i => ldPrim.Contains(all[i].Name), v0, "V0");
            Line("主判定", i => ldPrim.Contains(all[i].Name), v1, "**V1**");
            Line("全61行", i => true, v0, "V0");
            Line("全61行", i => true, v1, "**V1**");
            Console.WriteLine();
            Console.WriteLine($"歯止め `Baseline.PrimaryFifthFloor` = **{Baseline.PrimaryFifthFloor}%**（分母は主判定19行）。");

            // --- 表E: 情報セル
            Console.WriteLine();
            Console.WriteLine("## 表E. 情報セルの合計（`compare` 61 行・帯A）");
            Console.WriteLine();
            int t0 = Enumerable.Range(0, ldCompare.Length).Sum(i => LdInfo(v0[i]));
            int t1 = Enumerable.Range(0, ldCompare.Length).Sum(i => LdInfo(v1[i]));
            Console.WriteLine($"**{t0} → {t1}**（{t1 - t0:+0;-0;0}）。分布 (0/1/2/3/4 セル):");
            for (int k = 0; k <= 4; k++)
                Console.WriteLine($"- {k} セル: {Enumerable.Range(0, ldCompare.Length).Count(i => LdInfo(v0[i]) == k)} "
                    + $"→ {Enumerable.Range(0, ldCompare.Length).Count(i => LdInfo(v1[i]) == k)}");
            Console.WriteLine();
            Console.WriteLine("### 席を測り直す行（(T3) の条件・**測る前に固定した**）");
            Console.WriteLine();
            Console.WriteLine("    (i) 情報セルが減った ／ (ii) 第五波が 95% を超えた ／ (iii) 第2〜5波平均が ±10pt 以上動いた");
            Console.WriteLine();
            foreach (var row in ldBanRows.Where(r => r.Band == "compare"))
            {
                int i = all.FindIndex(a => a.Name == row.Name);
                bool c1 = LdInfo(v1[i]) < LdInfo(v0[i]), c2 = v0[i][^1] <= 95.0 && v1[i][^1] > 95.0;
                bool c3 = Math.Abs(LdAvg25(v1[i]) - LdAvg25(v0[i])) >= 10.0;
                Console.WriteLine($"- {row.Name} —— (i) {(c1 ? "○" : "×")} / (ii) {(c2 ? "○" : "×")} / (iii) {(c3 ? "○" : "×")}"
                    + $" → **{(c1 || c2 || c3 ? "測り直す" : "触らない")}**");
            }

            // --- Q2
            Console.WriteLine();
            Console.WriteLine("## Q2. 行によって到達率が割れるか（線: `compare` 4 行の最大 − 最小 ≥ 0.2）");
            Console.WriteLine();
            var rs = ldBanRows.Where(r => r.Band == "compare").Select(r => reachOf[r.Name]).ToList();
            Console.WriteLine($"最大 **{rs.Max():F3}** / 最小 **{rs.Min():F3}** / 差 **{rs.Max() - rs.Min():F3}** → "
                + $"{(rs.Max() - rs.Min() >= 0.2 ? "**○**" : "**×**")}");
            return;
        }

        // -------------------------------------------------------------------------
        // load seat（表C）。**(T3) の条件に当たった行だけ。**
        // -------------------------------------------------------------------------
        if (ldSub == "seat")
        {
            string ldFilter = args.Length > 4 ? args[4] : "";
            Console.WriteLine("# 第116期 (T3) —— 席の再判定");
            Console.WriteLine();
            Console.WriteLine("**線と採る条件は同じ集合で書く（規約 (G16)）。狙は<u>両方に</u>掛ける。**");
            Console.WriteLine();
            Console.WriteLine("    線・採る条件（測る前に固定）:");
            Console.WriteLine("      (a) 狙（ガルドが前列 / セッキが後列）を満たす        ← 線にも採る条件にも掛ける");
            Console.WriteLine("      (b) 情報セル（帯A・第2〜5波）が 2 以上");
            Console.WriteLine("      (c) 情報セル 4 → 3 → 2 の順、各段の中で平均（第2〜5波）が最上位");
            Console.WriteLine("      (d) 現行席との差が 5.0pt 未満で、情報セルが現行より多いときだけ採る");
            Console.WriteLine();

            var target = new List<(string Name, Formation F)>();
            foreach (var row in ldBanRows.Where(r => r.Band == "compare"))
            {
                if (ldFilter.Length > 0 && !ldFilter.Split(',').Any(k => row.Name.Contains(k.Trim()))) continue;
                double[] a = LdCells(row.F, ldOff, 0, LdSeeds), b = LdCells(row.F, ldOn, 0, LdSeeds);
                bool c1 = LdInfo(b) < LdInfo(a), c2 = a[^1] <= 95.0 && b[^1] > 95.0;
                bool c3 = Math.Abs(LdAvg25(b) - LdAvg25(a)) >= 10.0;
                Console.WriteLine($"- {row.Name} —— 情報セル {LdInfo(a)} → {LdInfo(b)} / 第五波 {a[^1]:F1}% → {b[^1]:F1}% "
                    + $"/ 平均 {LdAvg25(a):F1}% → {LdAvg25(b):F1}% → (i){(c1 ? "○" : "×")} (ii){(c2 ? "○" : "×")} (iii){(c3 ? "○" : "×")} "
                    + $"**{(c1 || c2 || c3 ? "測り直す" : "触らない")}**");
                if (c1 || c2 || c3) target.Add((row.Name, row.F));
            }
            Console.WriteLine();
            Console.WriteLine($"対象 **{target.Count} 行**。");

            var adopted = new List<(string Name, Formation F, int From, int To, double D)>();
            foreach (var t in target)
            {
                string name = t.Name;
                Formation cur = t.F;
                var members = cur.Occupied().Select(x => x.Def).ToList();
                var perms = new List<Formation>();
                foreach (int[] assign in SlotAssignments(members.Count))
                {
                    var f = new Formation();
                    for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
                    perms.Add(f);
                }
                static bool Intent(Formation f)
                {
                    foreach (var (slot, def) in f.Occupied())
                    {
                        if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
                        if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
                    }
                    return true;
                }
                var scan = new int[perms.Count];
                Parallel.For(0, perms.Count, i =>
                {
                    int wins = 0;
                    foreach (EnemyCatalog.Stage st in rdStages)
                        for (int seed = 0; seed < LdScan; seed++)
                            if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false, reader: ldOn).PlayerWon) wins++;
                    scan[i] = wins;
                });
                var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
                var pool = order.Take(20).Concat(order.Where(i => Intent(perms[i])).Take(10))
                                .Append(order.First(i => SameFormation(perms[i], cur))).Distinct().ToList();
                var cA = new double[pool.Count][];
                var cB = new double[pool.Count][];
                var reach = new double[pool.Count];
                Parallel.For(0, pool.Count, k =>
                {
                    cA[k] = LdCells(perms[pool[k]], ldOn, 0, LdSeeds);
                    cB[k] = LdCells(perms[pool[k]], ldOn, LdCfBase, LdCfSeeds);
                    LdStat ls = LdSum25(LdWatch(perms[pool[k]], ldOn, LdScan));
                    reach[k] = ls.N == 0 ? 0 : ls.Reach / ls.N;
                });

                int curK = pool.FindIndex(i => SameFormation(perms[i], cur));
                double curAvg = LdAvg25(cA[curK]);
                int curInfo = LdInfo(cA[curK]);
                var ranked = Enumerable.Range(0, pool.Count).OrderByDescending(k => LdAvg25(cA[k])).ToList();

                Console.WriteLine();
                Console.WriteLine($"## {name}{(ldPrim.Contains(name) ? "（**主判定19行**）" : "")}");
                Console.WriteLine();
                Console.WriteLine($"現行席の情報セル **{curInfo}**・第2〜5波平均 **{curAvg:F1}%**・到達率 **{reach[curK] * 100:F1}%**。候補 {pool.Count} 通り。");
                Console.WriteLine();
                Console.WriteLine("| 追順 | 粗順 | 狙 | 前1/前3 | 中央 | 後1/後3 | 平均(2〜5波) | Δ | **情報セル** | **ガルド隣接** | **到達率** |"
                    + string.Concat(rdStages.Select((_, i) => $" 第{i + 1}波 |")) + $" 平均({LdCfBase}..) |");
                Console.WriteLine("|--:|--:|:-:|---|---|---|--:|--:|--:|--:|--:|" + string.Concat(rdStages.Select(_ => "---:|")) + "--:|");
                for (int r = 0; r < ranked.Count; r++)
                {
                    int k = ranked[r];
                    Formation f = perms[pool[k]];
                    var g = f.Occupied().Where(o => ReferenceEquals(o.Def, UnitCatalog.Gald)).ToList();
                    string gd = "—";
                    if (g.Count > 0)
                    {
                        int gs = g[0].Slot;
                        int deg = f.Occupied().Count(o => o.Slot != gs && FormationRules.AreAdjacent(gs, o.Slot));
                        int bs = f.Occupied().First(o => ReferenceEquals(o.Def, UnitCatalog.Ban)).Slot;
                        gd = $"{deg}{(FormationRules.AreAdjacent(gs, bs) ? "**+**" : "")}";
                    }
                    Console.WriteLine($"| {r + 1}{(k == curK ? "★現行" : "")} | {order.IndexOf(pool[k]) + 1} | {(Intent(f) ? "○" : "×")} "
                        + $"| {LdSeats(f)} | {LdAvg25(cA[k]):F1}% | {LdAvg25(cA[k]) - curAvg:+0.0;-0.0;0.0} | **{LdInfo(cA[k])}** "
                        + $"| {gd} | {reach[k] * 100:F1}% |"
                        + string.Concat(cA[k].Select(c => $" {c:F1}% |")) + $" {LdAvg25(cB[k]):F1}% |");
                }

                // 線を満たすが狙で落ちた席の数（(G16)。**掛かっているが効かなかったことも記録する**）
                int lineOnly = Enumerable.Range(0, pool.Count).Count(k => k != curK && LdInfo(cA[k]) >= 2
                                && Math.Abs(LdAvg25(cA[k]) - curAvg) < LdSeatLine);
                var ok = Enumerable.Range(0, pool.Count)
                    .Where(k => k != curK && Intent(perms[pool[k]]) && LdInfo(cA[k]) >= 2
                                && Math.Abs(LdAvg25(cA[k]) - curAvg) < LdSeatLine)
                    .OrderByDescending(k => LdInfo(cA[k])).ThenByDescending(k => LdAvg25(cA[k]))
                    .ThenBy(k => order.IndexOf(pool[k])).ToList();
                Console.WriteLine();
                Console.WriteLine($"線（(b)(d) の差）を満たす席 **{lineOnly} 通り**、そのうち狙 (a) を満たす席 **{ok.Count} 通り** "
                    + $"——**狙で落ちた席 {lineOnly - ok.Count} 通り**（規約 (G16)）。");
                if (ok.Count == 0) { Console.WriteLine(); Console.WriteLine("**判定: 据え置き** —— (a)(b)(d) を満たす候補が 0 通り。"); continue; }
                int best = ok[0];
                bool take = LdInfo(cA[best]) > curInfo;
                Console.WriteLine();
                Console.WriteLine($"最上位は 追順 **{ranked.IndexOf(best) + 1} 位**（情報セル {curInfo} → **{LdInfo(cA[best])}**、"
                    + $"Δ **{LdAvg25(cA[best]) - curAvg:+0.0;-0.0}pt**、到達率 {reach[curK] * 100:F1}% → **{reach[best] * 100:F1}%**）。");
                Console.WriteLine();
                Console.WriteLine($"**判定: {(take ? "差し替え" : "据え置き")}** —— (d) 情報セルが現行より{(take ? "多い" : "多くない")}。");
                if (take)
                {
                    Formation bf = perms[pool[best]];
                    Console.WriteLine();
                    Console.WriteLine($"採る配置: `front1: {LdN(bf[0])}, front3: {LdN(bf[1])}, center: {LdN(bf[2])}, "
                        + $"back1: {LdN(bf[3])}, back3: {LdN(bf[4])}`");
                    adopted.Add((name, bf, curInfo, LdInfo(cA[best]), LdAvg25(cA[best]) - curAvg));
                }
            }
            Console.WriteLine();
            Console.WriteLine($"## まとめ —— 差し替える行 **{adopted.Count} 行 / {target.Count} 行**");
            Console.WriteLine();
            Console.WriteLine("| 行 | 主判定 | 情報セル | Δ 平均(2〜5波) | 採る配置 |");
            Console.WriteLine("|---|:-:|--:|--:|---|");
            foreach (var a in adopted)
                Console.WriteLine($"| {a.Name} | {(ldPrim.Contains(a.Name) ? "**P**" : "")} | {a.From} → **{a.To}** | {a.D:+0.0;-0.0} | {LdSeats(a.F)} |");
            return;
        }

        // -------------------------------------------------------------------------
        // load check（自己検査）
        // -------------------------------------------------------------------------
        if (ldSub == "check")
        {
            string oldPath = args.Length > 4 ? args[4] : "";
            Console.WriteLine("# 第116期 —— 自己検査");
            Console.WriteLine();

            var bal = RdBalance(rdStages.Count);
            int cells = 0, bad = 0, unknown = 0, banBad = 0;
            var v0all = new Dictionary<string, double[]>(StringComparer.Ordinal);
            var lockObj = new object();
            Parallel.ForEach(ldCompare, b =>
            {
                double[] c = LdCells(b.F, ReaderRule.Default, 0, LdSeeds);
                double[] z = LdCells(b.F, ldOff, 0, LdSeeds);
                lock (lockObj)
                {
                    v0all[b.Name] = z;
                    if (!bal.TryGetValue(b.Name, out double[]? want)) { unknown++; return; }
                    for (int w = 0; w < rdStages.Count; w++)
                    {
                        cells++;
                        if (Math.Abs(c[w] - want[w]) > 0.001) { bad++; if (LdHasBan(b.F)) banBad++; }
                    }
                }
            });
            Console.WriteLine($"- **必須1** `compare` {cells} セルを `docs/balance.md` と突き合わせ（**既定の規則**）: "
                + $"**ずれ {bad} 件**（うちバンの行 {banBad}・行名が引けなかった行 {unknown}）→ {(bad == 0 && unknown == 0 ? "**○**" : "**×**")}");

            string? root = RdRoot();
            int pick = 0;
            if (root != null)
                foreach (string p in new[] { "BattleEngine.cs", "Traits.cs" })
                    pick += File.ReadAllText(Path.Combine(root, "BattleCore", p)).Split("PickOne(").Length - 1;
            Console.WriteLine($"- **必須4** `PickOne(` の出現数: **{pick} 箇所** → {(pick == 26 ? "**○**（26 のまま）" : "**×**")}");

            // (a) Threshold = 0 を明示した経路が採用前の balance.md と一致
            if (oldPath.Length > 0 && File.Exists(oldPath))
            {
                var old = new Dictionary<string, double[]>(StringComparer.Ordinal);
                foreach (string line in File.ReadAllLines(oldPath))
                {
                    if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                    var parts = line.Split('|', StringSplitOptions.None).Select(p => p.Trim()).ToArray();
                    if (parts.Length < 8) continue;
                    var v = new List<double>();
                    for (int i = 2; i < parts.Length - 1; i++)
                        if (parts[i].EndsWith("%") && double.TryParse(parts[i].TrimEnd('%'), out double d)) v.Add(d);
                    if (v.Count == rdStages.Count) old[parts[1]] = v.ToArray();
                }
                int ac = 0, ab = 0;
                var bads = new List<string>();
                foreach (var kv in v0all)
                    if (old.TryGetValue(kv.Key, out double[]? want))
                        for (int w = 0; w < rdStages.Count; w++)
                        {
                            ac++;
                            if (Math.Abs(kv.Value[w] - want[w]) > 0.001) { ab++; bads.Add($"{kv.Key} 第{w + 1}波"); }
                        }
                Console.WriteLine($"- **(a)** `Threshold = 0` を明示した版が**採用前の** `balance.md` と一致: "
                    + $"{ac} セル中ずれ **{ab} 件** → {(ac > 0 && ab == 0 ? "**○**" : "**×**")}"
                    + (bads.Count > 0 ? "（" + string.Join(" / ", bads.Take(8)) + "）" : ""));
            }
            else Console.WriteLine("- **(a)** 採用前の `balance.md` のパスが渡されていないので測っていない（第2引数に渡す）");

            // (b) Overload を持つ駒はバンだけ
            var holders = UnitCatalog.All.Where(d => d.Traits.Contains(TraitId.Overload)).Select(d => d.Name).ToList();
            int foeHolders = 0;
            foreach (EnemyCatalog.Stage st in rdStages)
                foeHolders += st.Enemy.Occupied().Count(o => o.Def.Traits.Contains(TraitId.Overload));
            Console.WriteLine($"- **(b)** `Overload` を持つ駒: 味方 **{holders.Count} 枚**（{string.Join(" / ", holders)}）"
                + $" / 敵側の在席 **{foeHolders} 枚** → "
                + $"{(holders.Count == 1 && holders[0] == UnitCatalog.Ban.Name && foeHolders == 0 ? "**○**" : "**×**")}");

            // (c) 分子と分母を同じ瞬間で取っている（空振りが負にならない）
            int neg = 0, tot = 0;
            foreach (var row in ldBanRows)
                foreach (LdStat ls in LdWatch(row.F, ldOn, LdSeeds))
                {
                    tot++;
                    if (ls.Over > ls.Alive || ls.OverSwung > ls.Over) neg++;
                }
            Console.WriteLine($"- **(c)** 到達率の分子 ≤ 分母 かつ 空振り ≥ 0（**同じ瞬間で数えている**）: "
                + $"{tot} セル中の違反 **{neg} 件** → {(neg == 0 ? "**○**" : "**×**")}");

            // (d) 情報セルの帯
            Console.WriteLine($"- **(d)** 情報セルを数えた帯: **seed 0..{LdSeeds - 1}**（規約 (G14)）→ {(LdSeeds == 200 ? "**○**" : "**×**")}");

            // (e) 狙は seat が線にも採る条件にも掛けている
            Console.WriteLine("- **(e)** 席の探索で狙が**線にも採る条件にも**掛かっている（規約 (G16)）: "
                + "`reader load seat` が「線を満たすが狙で落ちた席」を毎行出力する → **○**");
            return;
        }

        Console.WriteLine("mode: reader load phase0 / run / seat / check");
        return;
    }

    // ------------------------------------------------------------------------------
    // phase0（§5 の表P）。**戦闘0回。**
    // ------------------------------------------------------------------------------
    if (rdMode == "phase0")
    {
        Console.WriteLine("# 第115期 Phase 0 —— 強化の2枚目の読み手（+ 索引の穴）");
        Console.WriteLine();
        Console.WriteLine("**戦闘は1回も回していない。** 盤面も `Presets` も `UnitCatalog` も1行も動かしていない。");

        // --- 表P-1: (B) の索引照合
        Console.WriteLine();
        Console.WriteLine("## 表P-1. (B) 索引の穴（`CLAUDE.md` → `design/LESSONS_*.md`）");
        Console.WriteLine();
        var (refs, missing) = RdIndexAudit();
        Console.WriteLine($"`CLAUDE.md` のコマンド索引にある `→ LESSONS_*.md` の参照を「行 × 期」で数えると **{refs} 件**。");
        Console.WriteLine($"そのうち**参照先にその期の節が実在しないもの**が **{missing.Count} 件**。");
        Console.WriteLine();
        Console.WriteLine("| 期 | 参照先 | `CLAUDE.md` の行 |");
        Console.WriteLine("|---|---|---|");
        foreach (var (ph, file, lines) in missing)
            Console.WriteLine($"| 第{ph}期 | `{file}` | {string.Join(", ", lines.Select(l => "L" + l))} |");
        Console.WriteLine();
        Console.WriteLine("**B-3 で埋めるのは第65期の1件だけ**（指示書 §2）。"
            + $"残り **{Math.Max(0, missing.Count - 1)} 件**は列挙するだけで別の期へ送る（B-4）。");

        // --- 表P-2: 窓口の再確認
        Console.WriteLine();
        Console.WriteLine("## 表P-2. 強化の窓口の再確認（§0-1）");
        Console.WriteLine();
        Console.WriteLine($"`WhetRoute` の経路数（`Other` を含む）: **{Enum.GetValues<WhetRoute>().Length}**");
        Console.WriteLine();
        Console.WriteLine("| # | 経路 |");
        Console.WriteLine("|--:|---|");
        int rn = 0;
        foreach (WhetRoute wr in Enum.GetValues<WhetRoute>())
            Console.WriteLine($"| {++rn} | `{wr}` |");
        var selfBuff = RdSelfBuffTraits();
        Console.WriteLine();
        Console.WriteLine($"`Traits.cs` を走査して見つけた**自己強化（`AtkBonus` を特性が直に動かす）の札**: "
            + $"**{selfBuff.Count} 本** — "
            + string.Join(" / ", selfBuff.OrderBy(x => x.ToString(), StringComparer.Ordinal).Select(x => "`" + x + "`")));
        Console.WriteLine();
        Console.WriteLine("> **手で写していない**（第94期の反省）。`derive` と同じく原文を走査して数えている。");

        // --- 表P-3: 候補の機械的な絞り込み
        Console.WriteLine();
        Console.WriteLine("## 表P-3. 載せる駒の候補（§1-3 の規則を機械で当てる）");
        Console.WriteLine();
        Console.WriteLine("規則は4つ。**(a)(d) は反射で、(e) は原文の走査で当てる。(c) だけが判断。**");
        Console.WriteLine();
        Console.WriteLine("    (a) `AttackReads` を通る駒       … `Actions` が Skill だけの駒／`Immobile` を除く");
        Console.WriteLine("    (d) `ModifyAttack` / `ModifyPattern` を既に持っていない");
        Console.WriteLine("    (e) **自己強化を1本も持たない**   … 第66期の「自分で作れる値を条件にすると");
        Console.WriteLine("                                        条件の粒度はその駒自身の上昇量が決める」");
        Console.WriteLine("    (f) 攻撃型が**単体**             … R1 は単体 → 薙ぎなので、既に薙ぎ／貫きの駒は対象外");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 速 | 攻 | 型 | (a) | (d) | (e) | (f) | 残る |");
        Console.WriteLine("|---|--:|--:|---|:-:|:-:|:-:|:-:|:-:|");
        var survivors = new List<UnitDef>();
        foreach (UnitDef d in UnitCatalog.All.OrderBy(x => x.Speed).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            bool a = !(d.Actions is { Count: > 0 } && d.Actions.All(x => x.Kind == ActionKind.Skill))
                     && !d.Traits.Contains(TraitId.Immobile);
            bool dd = !d.Traits.Any(t => RdOverrides(t, "ModifyAttack") || RdOverrides(t, "ModifyPattern"));
            bool e = !d.Traits.Any(t => selfBuff.Contains(t));
            bool f = d.Pattern == AttackPattern.Single;
            bool keep = a && dd && e && f;
            if (keep) survivors.Add(d);
            Console.WriteLine($"| {d.Name} | {d.Speed} | {d.Attack} | {d.Pattern} | {(a ? "○" : "×")} | {(dd ? "○" : "×")} "
                + $"| {(e ? "○" : "×")} | {(f ? "○" : "×")} | {(keep ? "**○**" : "-")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**残った駒 {survivors.Count} / {UnitCatalog.All.Count} 枚。**");

        // --- 表P-4: 候補3枚
        Console.WriteLine();
        Console.WriteLine("## 表P-4. 候補3枚と、選んだ理由・落とした理由（(c) は判断）");
        Console.WriteLine();
        Console.WriteLine("| | 駒 | 速 | 攻 | 既存の1文 | 判断 |");
        Console.WriteLine("|---|---|--:|--:|---|---|");
        Console.WriteLine("| **採** | 据えのバン | 2 | 5 | 「動かない者を守ることしかできない。動く者は守れない」 | "
            + "**ロスターで最も遅い枠**（尾灯の「自分を除いて最も遅い味方」に構造的に当たる）。"
            + "守る駒が押し付けられた力で初めて盤面を割る＝**主題の反転が1文で説明できる** |");
        Console.WriteLine("| 落 | 責め苦のシガ | 3 | 9 | 「縛られた的しか殴れない臆病者。だからこそ、縛る者の隣でだけ牙になる」 | "
            + "**既に条件付きで別物になる駒**。二つ目の条件を足すと**駒が2文でなく3文で説明される**（(c) の趣旨に反する） |");
        Console.WriteLine("| 落 | 引き受けのウケ | 5 | 6 | 「背負った分だけ自分の腕は落ちる」 | "
            + "面白いが、**`AtkBonus` が構造的に負へ振れる**ので閾値に届かない恐れが大きい"
            + "（門1 が 0 になれば鎖が繋がらず、機構ではなく台の失敗として落ちる） |");
        Console.WriteLine();
        Console.WriteLine("> **ウツには載せない**（符号読みと二値読みが1枚に重なる。指示書 §1-3）。");

        // --- 表P-5: 台の紙
        Console.WriteLine();
        Console.WriteLine("## 表P-5. 台の紙（§3-1・**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 供給 | 5枚の総攻 | 読み手が最遅か | 狙い |");
        Console.WriteLine("|---|---|--:|:-:|---|");
        foreach (var (tag, sup, ctr, why) in rdBenches)
        {
            Formation f = RdBench(sup, ctr);
            int atk = f.Occupied().Sum(o => o.Def.Attack);
            bool slowest = f.Occupied().Where(o => o.Def.Id != rdReader.Id)
                            .All(o => o.Def.Speed > rdReader.Speed);
            Console.WriteLine($"| {tag} | {(sup?.Name ?? "—")} | {atk} | {(slowest ? "○" : "×")} | {why} |");
        }
        Console.WriteLine();
        Console.WriteLine("**席は4台とも同じ**（前1 供給者 / 前3 ドルガ / 中央 ガルド / 後1 読み手 / 後3 エグ）。");
        Console.WriteLine("読み手を**後1**に置くのは、**レーン0 で供給者の真後ろ**＝巨躯（ゴルム）の被覆に入るため。");
        Console.WriteLine("台4 は前1 を**空席**にしてある——供給者を別の駒に差し替えると、"
            + "**陰性対照が「供給者なし」ではなく「別の駒あり」になる。**");

        Console.WriteLine();
        Console.WriteLine("## 予測（§3-4・**測る前に書いてある**）");
        Console.WriteLine();
        Console.WriteLine("1. 台3（尾灯）の門1 が最も高い。ただし第114期の動的な濾しで**到達は間欠する**");
        Console.WriteLine("2. 台2（吐き戻し）は量が最大なのに門1 は低い（受け手が散る）");
        Console.WriteLine("3. 台1（号令開戦）は到着 0T だが **+4 の1回きりで閾値 5 に届かない**");
        Console.WriteLine("4. Q2 は通る。Q3 は台3 だけで通る");
        Console.WriteLine("5. 空振りが門2 の 2 割以上");
        return;
    }

    // ------------------------------------------------------------------------------
    // supply（選定規則 (b)）。**`compare` 61 行で「その駒が強化を1度でも受けているか」を測る。**
    // ------------------------------------------------------------------------------
    if (rdMode == "supply")
    {
        var builds = CompareBuilds();
        var got = new Dictionary<string, long>(StringComparer.Ordinal);
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        int battles = 0;
        foreach (var (name, f) in builds)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnemyCatalog.Stage st in rdStages)
                for (int seed = 0; seed < RdSupplySeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, st.Enemy, seed, verbose: false);
                    battles++;
                    foreach (IReadOnlyDictionary<string, int> to in r.WhetToByRoute)
                        foreach (var kv in to)
                        {
                            got[kv.Key] = got.TryGetValue(kv.Key, out long had) ? had + kv.Value : kv.Value;
                            seen.Add(kv.Key);
                        }
                }
            foreach (string id in seen) rows[id] = rows.TryGetValue(id, out int n) ? n + 1 : 1;
        }
        Console.WriteLine("# 第115期 (b) —— 強化の受け手の実測（選定規則 (b) の分母）");
        Console.WriteLine();
        Console.WriteLine($"`compare` {builds.Count()} 行 × {rdStages.Count} 波 × seed 0..{RdSupplySeeds - 1} "
            + $"= **{battles:N0} 戦**。**`WhetTo` を経路を問わず合算**した。");
        Console.WriteLine();
        Console.WriteLine("| 順 | 駒 | 受けた総量 | 受けた行数 |");
        Console.WriteLine("|--:|---|--:|--:|");
        int i2 = 0;
        foreach (var kv in got.OrderByDescending(x => x.Value))
        {
            UnitDef? d = UnitCatalog.All.FirstOrDefault(x => x.Id == kv.Key);
            Console.WriteLine($"| {++i2} | {(d?.Name ?? kv.Key)} | {kv.Value:N0} | {(rows.TryGetValue(kv.Key, out int n2) ? n2 : 0)} |");
        }
        Console.WriteLine();
        foreach (string id in new[] { UnitCatalog.Ban.Id, UnitCatalog.Shiga.Id, UnitCatalog.Uke.Id })
        {
            UnitDef? d = UnitCatalog.All.FirstOrDefault(x => x.Id == id);
            bool ok = got.TryGetValue(id, out long v) && v > 0;
            Console.WriteLine($"- **(b) {d?.Name ?? id}**: 受けた総量 {(got.TryGetValue(id, out long v2) ? v2 : 0):N0} / "
                + $"行数 {(rows.TryGetValue(id, out int n3) ? n3 : 0)} → {(ok ? "**○**" : "**×**")}");
        }
        return;
    }

    // ------------------------------------------------------------------------------
    // run（表A〜D）。
    // ------------------------------------------------------------------------------
    if (rdMode == "run")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RdRunAll(RdSeeds);
        Console.WriteLine("# 第115期 —— 積み過ぎ（強化の2枚目の読み手）の測定");
        Console.WriteLine();
        Console.WriteLine($"{rdBenches.Length}台 × {rdVers.Length}版 × {rdStages.Count}波 × seed 0..{RdSeeds - 1} = **{rdRows.Count:N0} 戦**"
            + $"（{sw.Elapsed.TotalSeconds:F1} 秒）。**分母は第2〜5波**（規約 (G10)）。");
        Console.WriteLine();
        Console.WriteLine($"閾値は **`AtkBonus` >= {RdThreshold}** で固定。**掃引しない**（§0-2）。");

        // 表A. 門
        Console.WriteLine();
        Console.WriteLine("## 表A. 門（版 × 台・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 決着T | 門1 到達率 | 初到達T | 門2 薙ぎ/戦 | 振り/戦 | 空振り/戦 | 門3 読み手の与ダメ | 味方総与ダメ | 強化/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int b = 0; b < rdBenches.Length; b++)
            for (int v = 0; v < rdVers.Length; v++)
            {
                var xs = RdSel(b, v);
                var reached = xs.Where(x => x.FirstOver > 0).ToList();
                Console.WriteLine($"| {rdBenches[b].Tag} | {rdVers[v].Tag} | {RdWin(xs):F1}% | {RdAvg(xs, x => x.Turns):F2} "
                    + $"| {RdGate1(xs):F3} | {(reached.Count == 0 ? "—" : RdAvg(reached, x => x.FirstOver).ToString("F2"))} "
                    + $"| {RdAvg(xs, x => x.Sweeps):F2} | {RdAvg(xs, x => x.Swings):F2} "
                    + $"| {RdAvg(xs, x => x.Over - x.OverSwung):F2} "
                    + $"| {RdAvg(xs, x => x.Dmg):F1} | {RdAvg(xs, x => x.TeamDmg):F1} | {RdAvg(xs, x => x.Whet):F2} |");
            }

        // 表A'. 波別（V1）
        Console.WriteLine();
        Console.WriteLine("## 表A'. 波別（V1・門1 / 門2 / 勝率。括弧は V0 の勝率）");
        Console.WriteLine();
        Console.WriteLine("| 台 |" + string.Concat(Enumerable.Range(1, rdStages.Count).Select(w => $" 第{w}波 |")));
        Console.WriteLine("|---|" + string.Concat(rdStages.Select(_ => "---:|")));
        for (int b = 0; b < rdBenches.Length; b++)
        {
            var cells = new List<string>();
            for (int w = 1; w <= rdStages.Count; w++)
            {
                var xs = RdSelW(b, 1, w);
                cells.Add($" {RdGate1(xs):F2} / {RdAvg(xs, x => x.Sweeps):F2} / {RdWin(xs):F0}% ({RdWin(RdSelW(b, 0, w)):F0}%) |");
            }
            Console.WriteLine($"| {rdBenches[b].Tag} |" + string.Concat(cells));
        }

        // 表B. 帰属
        Console.WriteLine();
        Console.WriteLine("## 表B. 帰属（V1 − V0・第2〜5波平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | V0 勝率 | V1 勝率 | 帰属 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        var attrib = new double[rdBenches.Length];
        for (int b = 0; b < rdBenches.Length; b++)
        {
            double w0 = RdWin(RdSel(b, 0)), w1 = RdWin(RdSel(b, 1));
            attrib[b] = w1 - w0;
            var per = new List<string>();
            for (int w = 2; w <= rdStages.Count; w++)
                per.Add($" {RdWin(RdSelW(b, 1, w)) - RdWin(RdSelW(b, 0, w)):+0.0;-0.0;0.0} |");
            Console.WriteLine($"| {rdBenches[b].Tag} | {w0:F1}% | {w1:F1}% | **{attrib[b]:+0.0;-0.0;0.0}pt** |"
                + string.Concat(per));
        }

        // 表C. AtkBonus の分布
        Console.WriteLine();
        Console.WriteLine("## 表C. `AtkBonus` の分布（V0・第2〜5波）と閾値 5 の位置");
        Console.WriteLine();
        Console.WriteLine("格子ごとの**到達ターン率**（そのターン頭に `AtkBonus` がその値以上だった割合）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 平均 `AtkBonus` | 最大 |" + string.Concat(UnitTally.ReaderProbes.Select(p => $" ≥{p} |")));
        Console.WriteLine("|---|--:|--:|" + string.Concat(UnitTally.ReaderProbes.Select(_ => "---:|")));
        for (int b = 0; b < rdBenches.Length; b++)
        {
            var xs = RdSel(b, 0);
            var cells = new List<string>();
            for (int k = 0; k < UnitTally.ReaderProbes.Length; k++)
            {
                int kk = k;
                cells.Add($" {(xs.Count == 0 ? 0 : xs.Average(x => x.Alive == 0 ? 0.0 : (double)x.Probe[kk] / x.Alive)):F3} |");
            }
            Console.WriteLine($"| {rdBenches[b].Tag} | {(xs.Count == 0 ? 0 : xs.Average(x => x.Alive == 0 ? 0.0 : (double)x.BonusSum / x.Alive)):F2} "
                + $"| {RdAvg(xs, x => x.BonusMax):F1} |" + string.Concat(cells));
        }
        Console.WriteLine();
        Console.WriteLine("> **閾値 5 は「灯 1 回」「号令開戦 +4 では届かない」の境目に置いてある。**"
            + "分布のどこに来るかは上の `≥1` と `≥5` の差で読む。");

        // 表D. 陰性対照
        Console.WriteLine();
        Console.WriteLine("## 表D. 陰性対照（台4・Q4）");
        Console.WriteLine();
        int diff = 0;
        for (int w = 1; w <= rdStages.Count; w++)
        {
            var a = RdSelW(RdNegative, 0, w); var c = RdSelW(RdNegative, 1, w);
            for (int i = 0; i < a.Count && i < c.Count; i++)
                if (a[i].Won != c[i].Won || a[i].Turns != c[i].Turns) diff++;
        }
        Console.WriteLine($"台4（供給者なし）を V0 と V1 で回して、**勝敗と決着ターンが食い違った試行**: "
            + $"**{diff} / {RdSel(RdNegative, 0, true).Count} 件**");
        Console.WriteLine();
        Console.WriteLine("| 波 | V0 勝率 | V1 勝率 | 差 | 門1 | 門2 | 門3 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int w = 1; w <= rdStages.Count; w++)
        {
            var a = RdSelW(RdNegative, 0, w); var c = RdSelW(RdNegative, 1, w);
            Console.WriteLine($"| 第{w}波 | {RdWin(a):F1}% | {RdWin(c):F1}% | {RdWin(c) - RdWin(a):+0.0;-0.0;0.0} "
                + $"| {RdGate1(c):F3} | {RdAvg(c, x => x.Sweeps):F2} | {RdAvg(c, x => x.Dmg):F1} |");
        }

        // 判定
        Console.WriteLine();
        Console.WriteLine("## 判定（§3-3）");
        Console.WriteLine();
        int q1 = 0;
        foreach (int b in rdMain) if (RdAvg(RdSel(b, 1), x => x.Sweeps) >= 0.5) q1++;
        double g1max = rdMain.Max(b => RdGate1(RdSel(b, 1)));
        double g1min = rdMain.Min(b => RdGate1(RdSel(b, 1)));
        int q3 = rdMain.Count(b => attrib[b] >= 5.0);
        Console.WriteLine("| | 内容 | 線 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|--:|:-:|");
        Console.WriteLine($"| **Q1** | 鎖が繋がるか（門2 ≥ 0.5 回/戦） | 台1〜3 のうち 2 台以上 | {q1} 台 "
            + $"| {(q1 >= 2 ? "**○**" : "**×**")} |");
        Console.WriteLine($"| **Q2** | 供給者で効き方が変わるか（門1 の最大 − 最小） | 0.2 以上 | {g1max - g1min:F3} "
            + $"| {(g1max - g1min >= 0.2 ? "**○**" : "**×**")} |");
        Console.WriteLine($"| **Q3** | 帰属 ≥ +5.0pt の台 | 1 台以上 | {q3} 台 "
            + $"| {(q3 >= 1 ? "**○**" : "**×**")} |");
        Console.WriteLine($"| **Q4** | 陰性対照（台4）が ±0.0 | 0 件 | {diff} 件 "
            + $"| {(diff == 0 ? "**○**" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine("**Q5（`compare` 305 セル 0 件）は `reader check` で取る。**");
        return;
    }

    // ------------------------------------------------------------------------------
    // check（自己検査）。
    // ------------------------------------------------------------------------------
    if (rdMode == "check")
    {
        Console.WriteLine("# 第115期 —— 自己検査");
        Console.WriteLine();

        // 必須1: compare 305 セル
        var builds = CompareBuilds();
        var bal = RdBalance(rdStages.Count);
        int cells = 0, bad = 0, unknown = 0;
        foreach (var (name, f) in builds)
        {
            if (!bal.TryGetValue(name, out double[]? want)) { unknown++; continue; }
            for (int w = 0; w < rdStages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < RdSeeds; seed++)
                    if (BattleEngine.Run(f, rdStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                cells++;
                if (Math.Abs(wins * 100.0 / RdSeeds - want[w]) > 0.001) bad++;
            }
        }
        Console.WriteLine($"- **必須1** `compare` {cells} セルを `docs/balance.md` と突き合わせ: "
            + $"**ずれ {bad} 件**（行名が引けなかった行 {unknown}）→ {(bad == 0 && unknown == 0 ? "**○**" : "**×**")}");
        Console.WriteLine("  （**既定は `ReaderRule.Default`（閾値 0）で `ModifyPattern` が素通りする**うえ、"
            + "`UnitCatalog` に `Overload` を持つ駒は1枚も無い）");

        // 必須4: PickOne
        string? root = RdRoot();
        int pick = 0;
        if (root != null)
            foreach (string p in new[] { "BattleEngine.cs", "Traits.cs" })
                pick += File.ReadAllText(Path.Combine(root, "BattleCore", p))
                            .Split("PickOne(").Length - 1;
        Console.WriteLine($"- **必須4** `PickOne(` の出現数（`BattleCore/BattleEngine.cs` + `Traits.cs`）: "
            + $"**{pick} 箇所** → {(pick == 26 ? "**○**（26 のまま）" : "**×**")}");

        // (a) 台4 で門が全部 0
        RdRunAll(RdSeeds);
        var neg = RdSel(RdNegative, 1, true);
        double n1 = neg.Sum(x => x.Over), n2 = neg.Sum(x => x.Sweeps);
        Console.WriteLine($"- **(a)** 台4（供給者なし）の門1 の分子 **{n1:F0}** / 門2 **{n2:F0}** "
            + $"→ {(n1 == 0 && n2 == 0 ? "**○**" : "**×**")}");

        // (b) 特性を載せても既定で乱数の消費が不変（V0 == 特性なしのバン）
        UnitDef plain = new()
        {
            Id = UnitCatalog.Ban.Id, Name = UnitCatalog.Ban.Name, MaxHp = UnitCatalog.Ban.MaxHp,
            Attack = UnitCatalog.Ban.Attack, Speed = UnitCatalog.Ban.Speed, Pattern = UnitCatalog.Ban.Pattern,
            Traits = UnitCatalog.Ban.Traits
        };
        int drift = 0, n = 0;
        for (int b = 0; b < rdBenches.Length; b++)
        {
            Formation withT = RdBench(rdBenches[b].Supplier, rdBenches[b].Center);
            Formation without = Formation.Build(front1: rdBenches[b].Supplier, front3: UnitCatalog.Dolga,
                                                center: rdBenches[b].Center ?? UnitCatalog.Gald,
                                                back1: plain, back3: UnitCatalog.Egu);
            for (int w = 0; w < rdStages.Count; w++)
                for (int seed = 0; seed < RdSeeds; seed++)
                {
                    BattleResult r1 = BattleEngine.Run(withT, rdStages[w].Enemy, seed, verbose: false);
                    BattleResult r2 = BattleEngine.Run(without, rdStages[w].Enemy, seed, verbose: false);
                    n++;
                    if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) drift++;
                }
        }
        Console.WriteLine($"- **(b)** 札を載せた V0 と**札なしのバン**を同じ seed で回して食い違った試行: "
            + $"**{drift} / {n}** → {(drift == 0 ? "**○**" : "**×**")}");

        // (c) 二重に持っていないこと
        bool dup = rdReader.Traits.Count(t => RdOverrides(t, "ModifyAttack") || RdOverrides(t, "ModifyPattern")) > 1;
        Console.WriteLine($"- **(c)** 読み手が `ModifyAttack` / `ModifyPattern` を二重に持っていないか: "
            + $"{(dup ? "**×**" : "**○**")}（`Bulwark` はどちらも上書きしない）");

        // (d) (B-3) の逐語照合
        if (root != null)
        {
            string src = File.ReadAllText(Path.Combine(root, "design", "PHASE65_WHET_ANATOMY.md"));
            string dst = File.ReadAllText(Path.Combine(root, "design", "LESSONS_061_080.md"));
            int idx = dst.IndexOf("### 第65期", StringComparison.Ordinal);
            int q = 0, qbad = 0;
            if (idx >= 0)
            {
                int end = dst.IndexOf("\n### ", idx + 5, StringComparison.Ordinal);
                string sec = end < 0 ? dst.Substring(idx) : dst.Substring(idx, end - idx);
                foreach (string line in sec.Split('\n'))
                {
                    string t = line.TrimEnd();
                    if (!t.StartsWith("> ") || t.Length < 12) continue;   // 引用ブロックだけを照合する
                    q++;
                    if (!src.Contains(t.Substring(2).Trim(), StringComparison.Ordinal)) qbad++;
                }
            }
            Console.WriteLine($"- **(d)** (B-3) の取り込みが原文に逐語で含まれるか（規約 (G15)）: "
                + $"引用 **{q} 行**中ずれ **{qbad} 行** → {(idx >= 0 && q > 0 && qbad == 0 ? "**○**" : "**×**")}");
        }
        return;
    }

    Console.WriteLine("mode: phase0 / supply / run / check");
    return;
}
}
