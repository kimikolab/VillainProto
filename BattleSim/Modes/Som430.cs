using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// =====================================================================================
// som430 —— 第430期【Phase 0・測るだけ】ソムの光の衣の棚卸し（規定は動かさない・版も作らない）。
// 指示書は design/PHASE430_SOM_ROBE_PHASE0_SPEC.md ／ 報告は design/PHASE430_SOM_ROBE_PHASE0.md。
//
//     dotnet run --project BattleSim -c Release 0 som430 robe     # §2: ソムを含む台 × ボス ／ 近衛 ／ 大隊 ／ 本編 第2・4波 × {規定, 衣なし, 上限なし}・seed 0..199
//     dotnet run --project BattleSim -c Release 0 som430 check    # 自己検査
//
// 「衣なし」は規定のソムから光の衣の札（`SparkVeil` ／ `SparkVeilCap`）だけを外した対照（この器具の中だけの定義・`All` の外・保持者 0）。
// 「上限なし」は第320〜332期の規定 `SomH332`（衣に上限がない・既存の定義）。
// =====================================================================================
static class Som430Diag
{
    const string PredictFile = ".tmp/p430/predict.md";
    const int Seeds = 200;
    static readonly I.W[] Waves = { I.Boss, I.Guard, I.Bat, I.M2, I.M4 };

    /// <summary>衣なしの対照（規定のソム − 光の衣の札）。版ではない・`All` ／ `Presets` に入れない。</summary>
    internal static readonly UnitDef SomNoVeil = new()
    {
        Id = UnitCatalog.Som.Id, Name = UnitCatalog.Som.Name, MaxHp = UnitCatalog.Som.MaxHp, Attack = UnitCatalog.Som.Attack, Speed = UnitCatalog.Som.Speed,
        Advances = UnitCatalog.Som.Advances, Pattern = UnitCatalog.Som.Pattern,
        Traits = UnitCatalog.Som.Traits.Where(t => t is not (TraitId.SparkVeil or TraitId.SparkVeilCap)).ToArray(),
        Actions = UnitCatalog.Som.Actions, PlusText = "（衣なしの対照）", MinusText = UnitCatalog.Som.MinusText, Flavor = "",
    };
    static readonly (string Key, string Label, UnitDef D)[] Vers =
        { ("0", "規定", UnitCatalog.Som), ("nv", "衣なし", SomNoVeil), ("nc", "上限なし", UnitCatalog.SomH332) };

    /// <summary>台。`compare` ／ 交差帯のソムを含む行（重複する台は1つにまとめる）＋ 試遊の感電の台 ＋ 参考の寄せ集め 2 台（後3 のソム）。</summary>
    static List<(string Name, Formation F)> Boards()
    {
        var list = new List<(string Name, Formation F)>();
        void Add(string n, Formation f)
        {
            if (list.Any(b => Same(b.F, f))) return;
            list.Add((n, f));
        }
        foreach (var r in CompareBuilds().Concat(CrossBuilds()))
            if (r.F.Occupied().Any(o => o.Def.Id == "som")) Add(r.Name, r.F);
        foreach (var n in new[] { "試遊・感電 打ち返し", "試遊・感電 追い打ち", "試遊・感電 瓦礫", "試遊・感電 光の盾", "試遊・感電 ポンの台" })
            Add(n, Presets.Playtest.Concat(Presets.Versions).First(r => r.Name == n).F);
        // 参考: 感電の軸の外（寄せ集め）。ソムを後3（召喚獣を喚ぶだけの席）に置く。
        Add("参考・寄せ集め 耐久 → ソム", Put(CompareBuilds().First(r => r.Name == "耐久 (ガルド×リリ)").F, 4, UnitCatalog.Som));
        Add("参考・寄せ集め 継ぎ当て → ソム", Put(CompareBuilds().First(r => r.Name == "継ぎ当て×被弾強化").F, 4, UnitCatalog.Som));
        return list;
    }
    static bool Same(Formation a, Formation b) =>
        a.Shape == b.Shape && Enumerable.Range(0, 5).All(i => ReferenceEquals(a[i], b[i]));
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    static Formation Swap(Formation f, UnitDef som)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "som") g[o.Slot] = som;
        return g;
    }

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");

    sealed class Fx
    {
        public long N, Wins, WinT, Fell;
        public UnitTally Som = new(), Allies = new();
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Som.Add(o.Som); Allies.Add(o.Allies);
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => Wins == 0 ? F1(Win) : $"{F1(Win)}・{(double)WinT / Wins:0.0}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
    }

    static Fx One(Formation f, I.W w, int seed, bool verbose = false)
    {
        var (r, p, _) = I.Raw(f, w, seed, verbose: verbose);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("som", out var s)) a.Som.Add(s);
        foreach (var id in p.Select(u => u.Def.Id).Distinct())
            if (r.TallyByUnit.TryGetValue(id, out var t)) a.Allies.Add(t);
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode == "robe" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"som430: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "robe": Robe(); break;
            case "check": Check(); break;
            default: Console.WriteLine("som430: モードは robe / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §2: 衣が今の戦いでしていること
    // ---------------------------------------------------------------------------------
    static void Robe()
    {
        var boards = Boards();
        var res = new Dictionary<(int B, int W, string V), Fx>();
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
                foreach (var v in Vers)
                    res[(bi, wi, v.Key)] = Measure(Swap(b.F, v.D), w);

        Console.WriteLine($"# 第430期 §2 —— 光の衣が今の戦いでしていること（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台:");
        foreach (var (b, i) in boards.Select((b, i) => (b, i))) Console.WriteLine($"- {i + 1}. {b.Name}（{S307.Seats(b.F)}）");
        Console.WriteLine();

        Console.WriteLine("## 表1 衣の出入り（規定のソム・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率・倒しT | 張った回数 | 衣になった量 | 上限で捨てた量 | 受け止めた量 | 受け止めた回数 | 砕けた回数 | 衣なしの勝率（差） | 上限なしの勝率（差） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|---|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
            {
                var x = res[(bi, wi, "0")]; var nv = res[(bi, wi, "nv")]; var nc = res[(bi, wi, "nc")];
                Console.WriteLine($"| {bi + 1} | {w.Name} | {x.WT} | {Per1(x.Som.Veil430Puts, x.N)} | {Per1(x.Som.SparkVeilAdded, x.N)} | {Per1(x.Som.SparkVeilCapped, x.N)} | {Per1(x.Allies.SparkVeilSoaked, x.N)} | {Per1(x.Allies.Veil430Hits, x.N)} | {Per(x.Allies.VeilBreaks419, x.N)} | {nv.WT}（{Delta(nv.Win - x.Win)}）| {nc.WT}（{Delta(nc.Win - x.Win)}）|");
            }
        Console.WriteLine();

        Console.WriteLine("## 表2 衣が受け止めた一撃の主（規定のソム・受け止めた量の内訳）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 受け止めた ／戦 | 感電の連鎖 | 敵の単体の刃 | 敵の範囲 | （うち感電した敵から） | 背いた召喚獣 | 味方の巻き込み | 自傷（反動） | 刻み | 中継 ／ 徴収 | 破片の段の外 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
            {
                var t = res[(bi, wi, "0")].Allies; long n = t.SparkVeilSoaked;
                Console.WriteLine($"| {bi + 1} | {w.Name} | {Per1(n, res[(bi, wi, "0")].N)} | {Pc(t.Veil430SoakChain, n)} | {Pc(t.Veil430SoakFoe, n)} | {Pc(t.Veil430SoakFoeAoe, n)} | {Pc(t.Veil430SoakShocked, n)} | {Pc(t.Veil430SoakBeast, n)} | {Pc(t.Veil430SoakAlly, n)} | {Pc(t.Veil430SoakSelf, n)} | {Pc(t.Veil430SoakTick, n)} | {Pc(t.Veil430SoakRelay, n)} | {Pc(t.Veil430SoakOther, n)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表3 倒れた味方（規定 ／ 衣なし）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | 衣なし |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
                Console.WriteLine($"| {bi + 1} | {w.Name} | {res[(bi, wi, "0")].Fall} | {res[(bi, wi, "nv")].Fall} |");
        Console.WriteLine();

        Console.WriteLine("## 表4 背きの雷の落ち先（規定のソム・1戦あたり・§3 の R-a の下見）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 投げた雷 | 帯電した味方へ | 衣をまとった味方へ | うち帯電 | 共鳴返し | 衣が受け止めた回数（主を問わない） | 敵の刃のうち感電した敵から（量） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
            {
                var x = res[(bi, wi, "0")]; var s = x.Som; var t = x.Allies;
                Console.WriteLine($"| {bi + 1} | {w.Name} | {Per1(s.BtThrown, x.N)} | {Per1(s.BtAtCharged, x.N)} | {Per1(s.Veil430ThrowVeiled, x.N)} | {Per1(s.Veil430ThrowVeiledCharged, x.N)} | {Per1(s.CrFired, x.N)} | {Per1(t.Veil430Hits, x.N)} | {Pc(t.Veil430SoakShocked, t.Veil430SoakFoe + t.Veil430SoakFoeAoe)} |");
            }
        Console.WriteLine();

        // 読み (a)(c) の数
        int big = 0, cells = 0; long cap = 0, add = 0;
        foreach (var (k, x) in res.Where(kv => kv.Key.V == "0"))
        {
            cells++;
            if (Math.Abs(res[(k.B, k.W, "nv")].Win - x.Win) >= 10.0) big++;
            cap += x.Som.SparkVeilCapped; add += x.Som.SparkVeilAdded;
        }
        Console.WriteLine($"- (a) 衣なしとの差が ±10pt 以上のセル: {big} ／ {cells}");
        Console.WriteLine($"- (c) 上限で捨てた量 ÷（衣になった量 ＋ 捨てた量）: {Pc(cap, cap + add)}（全セルの合計）");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        bool ok = true;
        void Req(bool c, string what) { Console.WriteLine($"{(c ? "○" : "×")} {what}"); ok &= c; }
        Req(!SomNoVeil.Traits.Contains(TraitId.SparkVeil) && !SomNoVeil.Traits.Contains(TraitId.SparkVeilCap)
            && SomNoVeil.Traits.Count == UnitCatalog.Som.Traits.Count - 2, "衣なしの対照は規定のソムから衣の札 2 枚だけを外した");
        Req(!UnitCatalog.Everyone.Contains(SomNoVeil) && !Presets.Playtest.Concat(Presets.Versions).Any(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, SomNoVeil))), "衣なしの対照は `All` ／ `Retired` ／ `Presets` の外");
        Req(UnitCatalog.Som.Traits.Contains(TraitId.SparkVeilCap) && !UnitCatalog.SomH332.Traits.Contains(TraitId.SparkVeilCap), "上限なしは SomH332（規定は上限あり）");
        var boards = Boards();
        Req(boards.Count >= 6, $"台が {boards.Count} 枚（ソムを含む compare ／ 交差帯の行・試遊の感電の台・参考 2 台）");

        // 計数の枝は盤面を動かさない: verbose の有無・計数の内訳の合計が受け止めた量と一致・衣なしは衣を張らない
        bool same = true, sum = true, none = true;
        foreach (var (b, _) in boards.Select((b, i) => (b, i)))
            foreach (var w in Waves)
                for (int s = 0; s < 10; s++)
                {
                    var (r0, _, _) = I.Raw(b.F, w, s);
                    var (r1, _, _) = I.Raw(b.F, w, s, verbose: true);
                    if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns) same = false;
                    var t = new UnitTally();
                    foreach (var (id, tt) in r0.TallyByUnit) t.Add(tt);
                    long parts = t.Veil430SoakChain + t.Veil430SoakFoe + t.Veil430SoakFoeAoe + t.Veil430SoakBeast + t.Veil430SoakAlly + t.Veil430SoakSelf + t.Veil430SoakTick + t.Veil430SoakRelay + t.Veil430SoakOther;
                    if (parts != t.SparkVeilSoaked || t.Veil430SoakShocked > t.Veil430SoakFoe + t.Veil430SoakFoeAoe) sum = false;
                    var (r2, _, _) = I.Raw(Swap(b.F, SomNoVeil), w, s);
                    long nv = r2.TallyByUnit.Values.Sum(x => x.SparkVeilAdded + x.Veil430Puts + x.SparkVeilSoaked);
                    if (nv != 0) none = false;
                }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（台 × 5 波 × seed 0..9）");
        Req(sum, "受け止めた量の内訳の合計 ＝ 受け止めた量（`SparkVeilSoaked`）・感電した敵からは敵の刃の内側");
        Req(none, "衣なしの対照は衣を張らず、受け止めもしない");
        Console.WriteLine(ok ? "som430 check: すべて ○" : "som430 check: × あり");
        if (!ok) Environment.ExitCode = 1;
    }
}
