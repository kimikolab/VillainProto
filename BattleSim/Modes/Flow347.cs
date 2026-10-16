using BattleCore;
using static Common;
using G343 = Golm343Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// flow347 —— 第347期【軽い期】クグの「流す」: 浴びた状態異常を、糸で組み付いた敵へ（FL-a 写す・ダメージ系 ／ FL-b 移す・ダメージ系 ／ FL-c 移す・害のあるもの全部）。
// 指示書は design/PHASE347_KUGU_FLOW_SPEC.md ／ 報告は design/PHASE347_KUGU_FLOW.md。波は `golm343` の5波（ボス ／ 近衛 ／ 大隊 ／ 本編 第2波 ／ 第5波）。
//
//     dotnet run --project BattleSim -c Release 0 flow347 p0                    # §2 Phase 0: クグのいる台すべて × 5 波・seed 0..99（規定のクグが浴びた状態異常の棚卸し）
//     dotnet run --project BattleSim -c Release 0 flow347 boards [版,…]         # §5 代表台 × 5 波 × 版（規定 ／ FL-a ／ FL-b ／ FL-c）・seed 0..99
//     dotnet run --project BattleSim -c Release 0 flow347 compare [版,…]        # §5 `compare` 64 行 × 版（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 flow347 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 flow347 check                 # §8 自己検査
//
// 版の名前（ASCII）: base（規定）／ a（FL-a）／ b（FL-b）／ c（FL-c）。版はクグだけを差し替える（規定の上）。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p347/predict.md`）。
// =====================================================================================
static partial class Flow347Diag
{
    internal const string PredictFile = ".tmp/p347/predict.md";
    const int Seeds = 100;          // 軽い期
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第347期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"flow347: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": Phase0(); return;
            case "log": Log(A(3, "pon"), A(4, "2"), int.Parse(A(5, "0")), V(A(6, "c"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("flow347: モードは p0 / log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    internal sealed record Ver(string Name, string Ascii, UnitDef? Kugu);
    internal static readonly Ver[] Vers =
    {
        new("規定", "base", null),
        new("FL-a 写す・ダメージ系", "a", UnitCatalog.KuguFLa),
        new("FL-b 移す・ダメージ系", "b", UnitCatalog.KuguFLb),
        new("FL-c 移す・全部", "c", UnitCatalog.KuguFLc),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => v.Kugu is null ? f : S316.SwapId(f, "kugu", v.Kugu);
    static S287.Wave[] Waves => G343.Waves;
    static bool IsElite(S287.Wave w) => w.Name is "近衛" or "大隊";

    static readonly string[] WriterNames = { "味方", "敵", "なし" };
    static readonly string[] HeldNames = { "組み付いている", "この一撃でほどけた", "組み付いていない" };
    static string KindName(int ki) => StatusKeys.LabelOf(ThreadFlowRule.Counted[ki]);
    static int Kinds => ThreadFlowRule.Counted.Length;
    static long Cell(long[]? a, int ki, int wi, int hi) => a is null ? 0 : a[(ki * 3 + wi) * 3 + hi];
    static long Sum(long[]? a, Func<int, bool> kind, Func<int, bool>? writer = null, Func<int, bool>? held = null)
    {
        long s = 0;
        for (int ki = 0; ki < Kinds; ki++) if (kind(ki))
            for (int wi = 0; wi < 3; wi++) if (writer is null || writer(wi))
                for (int hi = 0; hi < 3; hi++) if (held is null || held(hi)) s += Cell(a, ki, wi, hi);
        return s;
    }
    static bool Harm(int ki) => ki < ThreadFlowRule.HarmfulCount;
    static bool Dot(int ki) => ki <= 1;

    // ---------------------------------------------------------------------------------
    // 台
    // ---------------------------------------------------------------------------------
    /// <summary>クグのいる台すべて（`compare` の行 ／ 試遊 ／ `Presets.Versions` の「試遊・」）。</summary>
    internal static (string Name, Formation F)[] KuguBoards()
    {
        static bool HasKugu(Formation f) => f.Occupied().Any(o => o.Def.Id == "kugu");
        var rows = CompareBuilds().Where(r => HasKugu(r.F)).Select(r => (r.Name, r.F))
            .Concat(Presets.Playtest.Where(r => HasKugu(r.F)).Select(r => (r.Name, r.F)))
            .Concat(Presets.Versions.Where(r => r.Name.StartsWith("試遊・", StringComparison.Ordinal) && HasKugu(r.F)).Select(r => (r.Name, r.F)));
        var seen = new HashSet<string>();
        return rows.Where(r => seen.Add(r.Name)).ToArray();
    }

    /// <summary>
    /// §2-3 の器具で組む台（決めたこと: 前列の味方の漏れがクグに落ちる並び）。
    /// 毒: `毒 (グザ×ミオ×ラウ)` の前3（ガルド）をクグに。燃焼: `燃焼 (ボルグ×ホタ)` の前1（ガルド）をクグに（ボルグの薙ぎの漏れと横に隣）。
    /// </summary>
    internal static (string Name, Formation F)[] BuiltBoards()
    {
        var poison = CompareBuilds().First(r => r.Name == "毒 (グザ×ミオ×ラウ)").F.Clone();
        poison[1] = UnitCatalog.Kugu;
        var burn = CompareBuilds().First(r => r.Name == "燃焼 (ボルグ×ホタ)").F.Clone();
        burn[0] = UnitCatalog.Kugu;
        return new[] { ("器具・毒 クグ", poison), ("器具・燃焼 クグ", burn) };
    }

    internal const string PonRow = "試遊・感電 ポンの台";

    /// <summary>
    /// §5 の代表台（ポンの台 ／ Phase 0 で浴びる量の多い台 2 ／ §2-3 で組んだ毒の台）。並びは報告書 §2 の答えで決めた（決めたこと）:
    /// 組み付いている間に害のあるものを1戦 1 回以上浴びる台が1台（`縛め非収入型`・ボルグの火）しか無く、毒はどの台にも無いので、器具で組んだ毒の台を足す。
    /// 器具で組んだ燃焼の台は `縛め非収入型` と同じボルグの漏れなので足さない。
    /// </summary>
    internal static (string Name, Formation F)[] Boards()
    {
        var all = KuguBoards().ToDictionary(r => r.Name, r => r.F);
        var b = new List<(string, Formation)> { ("ポンの台", all[PonRow]) };
        foreach (var n in TopRows) b.Add((ShortRow(n), all[n]));
        b.Add(BuiltBoards()[0]);
        return b.ToArray();
    }
    /// <summary>Phase 0 で浴びる量（害のあるもの・組み付いている間）の多い台（`flow347 p0` の §3 の上位2台・報告書 §2）。</summary>
    internal static readonly string[] TopRows = { "縛め非収入型 (クグ×速攻)", "試遊・感電 光の盾 重" };
    static string ShortRow(string n) => n.StartsWith("試遊・感電 ", StringComparison.Ordinal) ? n["試遊・感電 ".Length..] : n.Split(' ')[0];

    static (string Name, Formation F) BoardOf(string n) => n switch
    {
        "pon" => Boards()[0], "quick" => Boards()[1], "heavy" => Boards()[2], "poison" => Boards()[3], "burn" => BuiltBoards()[1],
        _ => Boards().Concat(KuguBoards()).First(b => b.Name == n || ShortRow(b.Name) == n),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, OneT, AllyDeaths, KuguDeaths, KuguWork;
        public UnitTally K = new();
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; OneT += o.OneT; AllyDeaths += o.AllyDeaths; KuguDeaths += o.KuguDeaths; KuguWork += o.KuguWork;
            K.Add(o.K);
        }
    }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new St { N = 1, AllyDeaths = p.Count(u => u.LastDeathTurn > 0), KuguDeaths = p.Count(u => u.Def.Id == "kugu" && u.LastDeathTurn > 0) };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.OneT = 1; }
        if (r.TallyByUnit.TryGetValue("kugu", out var t)) { a.K.Add(t); a.KuguWork = t.DamageToEnemy + t.CreditToEnemy; }
        return a;
    }

    internal static St Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new St();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static void Log(string board, string wave, int seed, Ver v)
    {
        var (name, f0) = BoardOf(board);
        var w = T323.WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, v), BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // §2 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = KuguBoards().Concat(BuiltBoards()).ToArray();
        var res = new Dictionary<(string, string), St>();
        foreach (var (bn, bf) in boards) foreach (var w in Waves) res[(bn, w.Name)] = Measure(bf, w);
        Console.WriteLine($"# {Tag} §2 Phase 0 —— 規定のクグが浴びた状態異常（クグのいる台 {boards.Length - 2} ＋ 器具で組んだ台 2 × {Waves.Length} 波・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("数えるのは `SetCounter` で**増えた分**（1回 ＝ 1件・量 ＝ 増えた値）。電気（感電）・糸・濃さ・手番（まどろみ）・組み付きは除く。"
            + "書き手は第94期の印（味方 ／ 敵 ／ なし）。組み付きは「組み付いている」「この一撃でほどけた」（同じターンにその書き手に殴られてほどけた）「組み付いていない」。"
            + "「器具・」の2台は §2-3 で組んだ台（`毒 (グザ×ミオ×ラウ)` の前3 ／ `燃焼 (ボルグ×ホタ)` の前1 をクグに）。");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. 台 × 波（1戦あたりの回数）");
        Console.WriteLine();
        Console.WriteLine("害 ＝ 害のあるもの13種（うち ダメージ系 ＝ 毒・燃焼）・無害 ＝ 害の無いもの8種。害の「組 ／ ほ ／ 外」＝ 組み付いている ／ この一撃でほどけた ／ 組み付いていない。書き手 ＝ 害のうち 味方 ／ 敵 ／ なし。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 生きていたT ／ 組み付いていたT | 害 | うちダメージ系 | 害の 組 ／ ほ ／ 外 | 害の書き手 味 ／ 敵 ／ なし | 無害 | 多い種類（害・組み付いている間） |");
        Console.WriteLine("|---|---|--:|---|--:|--:|---|---|--:|---|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
            {
                var z = res[(bn, w.Name)]; var c = z.K.Fl347Cnt;
                string top = string.Join("・", Enumerable.Range(0, ThreadFlowRule.HarmfulCount)
                    .Select(ki => (ki, n: Sum(c, k => k == ki, held: h => h == 0))).Where(x => x.n > 0).OrderByDescending(x => x.n).Take(3)
                    .Select(x => $"{KindName(x.ki)} {Per2(x.n, z.N)}"));
                Console.WriteLine($"| {bn} | {w.Name} | {F1(z.Win)} | {Per(z.K.KuguTurns, z.N)} ／ {Per(z.K.KuguHeldTurns, z.N)} | {Per2(Sum(c, Harm), z.N)} | {Per2(Sum(c, Dot), z.N)} | "
                    + $"{Per2(Sum(c, Harm, held: h => h == 0), z.N)} ／ {Per2(Sum(c, Harm, held: h => h == 1), z.N)} ／ {Per2(Sum(c, Harm, held: h => h == 2), z.N)} | "
                    + $"{Per2(Sum(c, Harm, wr => wr == 0), z.N)} ／ {Per2(Sum(c, Harm, wr => wr == 1), z.N)} ／ {Per2(Sum(c, Harm, wr => wr == 2), z.N)} | {Per2(Sum(c, k => !Harm(k)), z.N)} | {(top == "" ? "—" : top)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. 種類 × 書き手 × 組み付き（全台・全波の合計 ／ 1戦あたり）");
        Console.WriteLine();
        var sum = new St();
        foreach (var z in res.Values) sum.Add(z);
        Console.WriteLine($"全 {sum.N} 戦。各セルは「回数 ／ 量」の 1戦あたり。");
        Console.WriteLine();
        Console.WriteLine("| 種類 | 分類 | " + string.Join(" | ", WriterNames.SelectMany(wn => HeldNames.Select(hn => $"{wn}・{hn}"))) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", 9)));
        for (int ki = 0; ki < Kinds; ki++)
        {
            if (Sum(sum.K.Fl347Cnt, k => k == ki) == 0) continue;
            string cls = Dot(ki) ? "ダメージ系" : Harm(ki) ? "そのほか害" : "害の無いもの";
            var cells = new List<string>();
            for (int wi = 0; wi < 3; wi++) for (int hi = 0; hi < 3; hi++)
            {
                long n = Cell(sum.K.Fl347Cnt, ki, wi, hi), a = Cell(sum.K.Fl347Amt, ki, wi, hi);
                cells.Add(n == 0 ? "—" : $"{Per2(n, sum.N)} ／ {Per2(a, sum.N)}");
            }
            Console.WriteLine($"| {KindName(ki)} | {cls} | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 3. 台ごとの合計（5波の平均・1戦あたり）と §2-3 の判定");
        Console.WriteLine();
        Console.WriteLine("| 台 | 害 | 害・組み付いている | ダメージ系・組み付いている | 害・この一撃でほどけた |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        var perBoard = new List<(string Name, double Held)>();
        foreach (var (bn, _) in boards)
        {
            var z = new St();
            foreach (var w in Waves) z.Add(res[(bn, w.Name)]);
            var c = z.K.Fl347Cnt;
            double held = z.N == 0 ? 0 : (double)Sum(c, Harm, held: h => h == 0) / z.N;
            if (!bn.StartsWith("器具・", StringComparison.Ordinal)) perBoard.Add((bn, held));
            Console.WriteLine($"| {bn} | {Per2(Sum(c, Harm), z.N)} | {Per2(Sum(c, Harm, held: h => h == 0), z.N)} | {Per2(Sum(c, Dot, held: h => h == 0), z.N)} | {Per2(Sum(c, Harm, held: h => h == 1), z.N)} |");
        }
        Console.WriteLine();
        var best = perBoard.OrderByDescending(x => x.Held).ToList();
        bool thin = best.All(x => x.Held < 1.0);
        Console.WriteLine($"**§2-3: 害のあるもの（組み付いている間）が 1戦 1 回以上の台 {best.Count(x => x.Held >= 1.0)} ／ {best.Count}**"
            + (thin ? "——全台で 1戦 1 回未満なので、器具で組んだ台（毒 ／ 燃焼）を代表台に足す。" : "。"));
        Console.WriteLine($"上位: {string.Join("・", best.Take(4).Select(x => $"{x.Name} {x.Held:F2}"))}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = new Dictionary<(string, string, string), St>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はクグだけを差し替える（規定の上）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. 流れた量（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("流した回数（種類ごと・敵に増えた量）／ そのうち 味方 ／ 敵 が書いたもの ／ 流した先が倒れた（敵1体につき1回・うち撃破者のいない死）。規定は流さない（—）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 流した（回 ／ 敵に増えた量） | 種類 | 味 ／ 敵 | 敵に付かなかった | 流した先が倒れた |");
        Console.WriteLine("|---|---|---|---|---|---|--:|---|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers.Skip(1))
                {
                    var z = res[(bn, v.Name, w.Name)]; var k = z.K;
                    long nf = k.Fl347Flowed?.Sum() ?? 0, amt = k.Fl347FlowedAmt?.Sum() ?? 0;
                    string kinds = k.Fl347Flowed is null ? "—" : string.Join("・", Enumerable.Range(0, Kinds).Where(i => k.Fl347Flowed[i] > 0)
                        .Select(i => $"{KindName(i)} {Per2(k.Fl347Flowed[i], z.N)} ／ {Per2(k.Fl347FlowedAmt![i], z.N)}"));
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Ascii} | {Per2(nf, z.N)} ／ {Per2(amt, z.N)} | {kinds} | {Per2(k.Fl347FlowedFromAlly, z.N)} ／ {Per2(k.Fl347FlowedFromFoe, z.N)} | "
                        + $"{Per2(k.Fl347FlowNoGain?.Sum() ?? 0, z.N)} | {Per2(k.Fl347ToDied, z.N)}（{Per2(k.Fl347ToDiedNoKiller, z.N)}） |");
                }
        Console.WriteLine();

        Console.WriteLine("## 2. クグの働き（1戦あたり）: 生きていたT ／ 組み付いていたT ／ クグが倒れた ／ クグの与ダメ ＋ 仕掛け(敵)");
        Console.WriteLine();
        Console.WriteLine("仕掛け(敵) に流れた状態異常の削りは**入れていない**（書き手の手柄のまま・決めたこと）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | " + string.Join(" | ", vers.Select(v => v.Ascii)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(vers.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                Console.WriteLine($"| {bn} | {w.Name} | " + string.Join(" | ", vers.Select(v =>
                {
                    var z = res[(bn, v.Name, w.Name)];
                    return $"{Per(z.K.KuguTurns, z.N)} ／ {Per(z.K.KuguHeldTurns, z.N)} ／ {Per2(z.KuguDeaths, z.N)} ／ {Per(z.KuguWork, z.N)}";
                })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 3. 代表台の勝率（規定との差）・決着T（ボスは倒しT）・倒れた味方 ／戦");
        Console.WriteLine();
        foreach (var (bn, _) in boards)
        {
            Console.WriteLine($"### {bn}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
            foreach (var v in vers)
                Console.WriteLine($"| {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var z = res[(bn, v.Name, w.Name)]; double b0 = res[(bn, vers[0].Name, w.Name)].Win;
                    string win = v == vers[0] || z.Win == b0 ? F1(z.Win) : $"{F1(z.Win)}（{Sg(z.Win - b0)}）";
                    return $"{win}・{Per(z.WinT, z.Wins)}T・倒 {Per2(z.AllyDeaths, z.N)}";
                })) + " |");
            Console.WriteLine();
        }

        Console.WriteLine("## 4. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる ／ ボスを 4T 未満で倒す——規定で 4T 以上だった台だけ）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)]; var b = res[(bn, vers[0].Name, w.Name)];
                    if (IsElite(w) && z.N > 0 && 100.0 * z.OneT / z.N >= 5) bad.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.OneT, z.N)}%");
                    if (z.Win - b.Win <= -15) bad.Add($"{bn} × {w.Name} {Sg(z.Win - b.Win)}");
                    if (w.Name == "ボス" && z.Wins > 0 && (double)z.WinT / z.Wins < 4.0 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4.0)) bad.Add($"{bn} × ボス {Per(z.WinT, z.Wins)}T");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5 `compare` × 版
    // ---------------------------------------------------------------------------------
    static double[,] Grid((string Name, Formation F)[] rows, Func<Formation, Formation> map)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = Grid(rows, f => Apply(f, vers[0]));
        Console.WriteLine($"# {Tag} §5 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"規定の列は `docs/balance.md` と同じ帯（seed 0..{CompareSeeds - 1}）。クグのいる行は {rows.Count(r => r.F.Occupied().Any(o => o.Def.Id == "kugu"))} 行（ほかの行は版で動かない）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちたセル | 主判定19行の第五波 | 第五波 95% 超の行（規定 → 版・新たに） |");
        Console.WriteLine("|---|---|--:|--:|---|");
        var grids = vers.Skip(1).Select(v => (v, g: Grid(rows, f => Apply(f, v)))).ToArray();
        int b95 = Enumerable.Range(0, rows.Length).Count(r => b[r, last] > 95);
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        foreach (var (v, g) in grids)
        {
            int upRows = 0, upCells = 0, down = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                bool any = false;
                for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upCells++; any = true; } else if (g[r, w] < b[r, w]) down++; }
                if (any) upRows++;
            }
            int g95 = Enumerable.Range(0, rows.Length).Count(r => g[r, last] > 95), newly = Enumerable.Range(0, rows.Length).Count(r => g[r, last] > 95 && b[r, last] <= 95);
            Console.WriteLine($"| {v.Name} | {upRows} ／ {upCells} | {down} | {F1(pr.Average(ri => b[ri, last]))} → {F1(pr.Average(ri => g[ri, last]))} | {b95} → {g95}（新たに {newly}） |");
        }
        foreach (var (v, g) in grids)
        {
            Console.WriteLine();
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, g, "kugu");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
