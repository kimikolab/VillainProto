using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using CW = CheckWaveDiag;

// =====================================================================================
// tou279 —— 第279期「トウの転生（帯電の粉）」。
// 指示書は design/PHASE279_TOU_REBIRTH_SPEC.md ／ 報告は design/PHASE279_TOU_REBIRTH.md。
//
//     dotnet run --project BattleSim -c Release 0 tou279 run        # トウ在席の `compare` 2 行・感電の行の2形（E1 ／ E2）・移動の台2つ（M1 ／ M2）・感電の行そのまま × 版 T0 ／ T1 ／ T2 × 本編第2〜5波・ボス規定形・チェック波（B3 ／ W3）× seed 0..199
//     dotnet run --project BattleSim -c Release 0 tou279 bandb      # 帯B（seed 200..599）の追試: 7 台 × 版 × 本編第2〜5波の勝率
//     dotnet run --project BattleSim -c Release 0 tou279 relic      # 帯電の足の再測: 移動の台（M0 ／ M1 ／ M2）と感電の行の2形 × 版 × 札の枠5 × 本編第2〜5波・帯A 1400..1599 ／ 帯B 1600..1799・固有の勝者と発火
//     dotnet run --project BattleSim -c Release 0 tou279 check      # 自己検査（T0 の写しが規定と台本一致・粉の付与と漏れ・痺れの出どころ・乱数・verbose）
//     dotnet run --project BattleSim -c Release 0 tou279 log <台 0..6> <版 T0|T1|T2|T3|T3n> <波 2..5|ボス|B3|W3> [seed]   # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 tou279 compare    # 第286期: `compare` 64 行 × 5 波 × 版（トウのいない行のずれ・主判定・歯止め）
//     dotnet run --project BattleSim -c Release 0 tou279 elite      # 第286期: 精鋭（近衛 ／ 大隊）× トウ在席の行 × 版（勝率と機構）
//     dotnet run --project BattleSim -c Release 0 tou279 p0         # 第286期 Phase 0: 敵の盤面（X 字 ／ 9 席 ／ ボス）で主目標の席ごとの「隣の敵」の数・感電の書き手
//
// **規定のトウは動かさない**（T0 のまま）。T1 ／ T2 は `UnitCatalog.TouT1` ／ `TouT2`（`All` ／ `Retired` ／ `Presets` に入れない）。採否はポン。
// 第286期: 対称の粉 T3 ／ T3n（`UnitCatalog.TouT3` ／ `TouT3n`）を版に足した（指示書 design/PHASE286_TOU_SPREAD_SPEC.md）。
// =====================================================================================
static class Tou279Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "run";
        switch (mode)
        {
            case "run": RunAll(); return;
            case "check": Check(); return;
            case "bandb": BandB(); return;
            case "relic": Relic(); return;
            case "p0": P0(); return;
            case "compare": CompareAll(); return;
            case "elite": EliteRows(); return;
            case "log": LogOne(args.Length > 3 ? int.Parse(args[3]) : 1, args.Length > 4 ? args[4] : "T1", args.Length > 5 ? args[5] : "5", args.Length > 6 ? int.Parse(args[6]) : 0); return;
            default: Console.WriteLine("tou279: モードは run / bandb / relic / check / log。"); return;
        }
    }

    const int Seeds = 200;

    internal static readonly (string Name, string What, UnitDef Tou)[] Vers =
    {
        ("T0", "旧トウ（痺れ粉・第278〜286期の規定・対照）", UnitCatalog.TouT0),
        ("T1", "帯電の粉（敵へ感電 ＋ 隣の味方へ漏れ）＋ S3", UnitCatalog.TouT1),
        ("T2", "帯電の粉（敵へ感電のみ・漏れの対照）＋ S3", UnitCatalog.TouT2),
        ("T3", "対称の粉（主目標 ＋ その隣の敵へ感電 ＋ 隣の味方へ漏れ）＋ S3（第286期・第287期の規定）", UnitCatalog.TouT3),
        ("T3n", "対称の粉・漏れなし（漏れの代金の対照）＋ S3（第286期）", UnitCatalog.TouT3n),
    };

    static Formation Row(string prefix) => FvSwap(FvSwap(FvSwap(CompareBuilds().First(r => r.Name.StartsWith(prefix)).F, UnitCatalog.Shiga, UnitCatalog.ShigaG3K), UnitCatalog.Kata, UnitCatalog.KataS3), UnitCatalog.Kugu, UnitCatalog.KuguKG0);   // 第291期: クグも旧の規定へ・第289期: シガを旧の規定（G3K）に固定・第290期: カタも旧の規定（S3）に
    static Formation ShockRow => Pin308(Row("感電 (シガ×カタ×ソム)"));   // 第308期: ソムは旧の規定（`SomH307`）に
    /// <summary>第273期の帯電の足の検証台（`relic check273` (f)）。</summary>
    static Formation Bench273(UnitDef center) => Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Basa, center: center, back1: UnitCatalog.Sero, back3: UnitCatalog.Hane);

    /// <summary>台（規定のトウ＝T0 で定義し、版はトウだけを差し替える）。群 ＝ compare ／ 検証 ／ 参照（トウがいない・版に依らない）。</summary>
    static readonly (string Name, string Group, Func<UnitDef, Formation> Make)[] Boards =
    {
        ("毒+耐久 (ベニ×トウ)", "compare", d => FvSwap(Row("毒+耐久 (ベニ×トウ)"), UnitCatalog.Tou, d)),
        ("責め苦 (トウ×シガ)", "compare", d => FvSwap(Row("責め苦 (トウ×シガ)"), UnitCatalog.Tou, d)),
        ("E1 感電の行の後3 ミオ → トウ", "検証", d => FvSwap(ShockRow, UnitCatalog.Mio, d)),
        ("E2 感電の行の中央 ソム → トウ", "検証", d => FvSwap(ShockRow, UnitCatalog.SomH307, d)),
        ("M1 隊列崩しの中央 ガルド → トウ", "検証", d => FvSwap(Row("隊列崩し (バサ×ヨミ×セロ)"), UnitCatalog.Gald, d)),
        ("M2 第273期の検証台の中央 カタ → トウ", "検証", d => Bench273(d)),
        ("感電の行（ソムのまま・参照）", "参照", _ => ShockRow),
    };

    static readonly (string Name, string Group, Func<List<UnitState>> Make)[] Waves = BuildWaves();
    static (string, string, Func<List<UnitState>>)[] BuildWaves()
    {
        var l = new List<(string, string, Func<List<UnitState>>)>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
        {
            int ww = w;
            l.Add(($"第{w + 1}波", "本編", () => BattleEngine.Materialize(EnemyCatalog.Stages[ww].Enemy, BattleContext.EnemyTeam)));
        }
        l.Add(("ボス", "ボス", () => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None)));
        l.Add(("B3", "B3", CW.CheckWave(CW.CWaveOf(CW.DefaultBoss))));
        l.Add(("W3", "W3", CW.CheckWave(CW.CWaveOf(CW.DefaultHand))));
        return l.ToArray();
    }

    /// <summary>1戦平均を取る量。側（味方 ／ 敵）の量は駒ごとの計数を `Def.Id` の側で足す。</summary>
    sealed class Agg
    {
        public long N, Wins, WinT, Surv, Dealt20, Turns;
        public long TouAttacks, PowderFoe, PowderAlly, OldStuns;          // トウ: 手番の一撃 ／ 粉で新しく感電させた敵・味方 ／ 旧の痺れ粉が止めた数
        public long FoePops, AllyPops, FoeStunned, FoeStunEarly, AllyStunned; // 起爆（弾けた駒）と感電の痺れ（側ごと）
        public long FoeStall, FoeStallShock, AllyStall, AllyStallShock;  // 痺れで潰れた手番（うち感電の痺れ）
        public long FoeDischarge, AllyDischarge;                          // 放電で削れた HP（受けた側）
        public long Wired, Cowered, ShigaSwings, RelicMoves;              // シガの電気鞭 ／ 怖気づき ／ 鞭の一振り ／ 帯電の足が感電を移した数
        public long BaitPops, TouRoots;                                   // 弾けた敵のうちソムの餌（背いた獣）／ トウの一撃が起こした連鎖（起点の数）
        public long PowderMain, PowderSpread;                             // 第286期: 粉で新しく感電した敵のうち主目標 ／ 主目標の隣
        public long FoeChains, FoeChainUnits, FoeChains2;                 // 第286期: 敵側の起爆（根の数）／ 弾けた敵の延べ ／ 2体以上が弾けた起爆

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Surv += o.Surv; Dealt20 += o.Dealt20; Turns += o.Turns;
            TouAttacks += o.TouAttacks; PowderFoe += o.PowderFoe; PowderAlly += o.PowderAlly; OldStuns += o.OldStuns;
            FoePops += o.FoePops; AllyPops += o.AllyPops; FoeStunned += o.FoeStunned; FoeStunEarly += o.FoeStunEarly; AllyStunned += o.AllyStunned;
            FoeStall += o.FoeStall; FoeStallShock += o.FoeStallShock; AllyStall += o.AllyStall; AllyStallShock += o.AllyStallShock;
            FoeDischarge += o.FoeDischarge; AllyDischarge += o.AllyDischarge;
            Wired += o.Wired; Cowered += o.Cowered; ShigaSwings += o.ShigaSwings; RelicMoves += o.RelicMoves;
            BaitPops += o.BaitPops; TouRoots += o.TouRoots;
            PowderMain += o.PowderMain; PowderSpread += o.PowderSpread; FoeChains += o.FoeChains; FoeChainUnits += o.FoeChainUnits; FoeChains2 += o.FoeChains2;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++;
            Turns += r.Turns;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) Surv++; }
            var foeIds = e.Select(u => u.InstanceId).ToHashSet();
            foreach (var ev in r.Events.Where(ev => ev.Kind == BattleEventKind.Summon && ev.Team == BattleContext.EnemyTeam))
                if (ev.TargetId is int t) foeIds.Add(t);
            if (r.PlayerWon)
                WinT += r.Events.Where(ev => ev.Kind == BattleEventKind.Death && ev.TargetId is int d && foeIds.Contains(d)).Select(ev => ev.Turn).DefaultIfEmpty(r.Turns).Max();
            foreach (var ev in r.Events)
                if (ev.Kind == BattleEventKind.Damage && ev.TargetId is int t && foeIds.Contains(t) && ev.Turn <= CW.BossTurns) Dealt20 += ev.Amount;

            var mine = p.Select(u => u.Def.Id).ToHashSet();
            foreach (var (id, t) in r.TallyByUnit)
            {
                bool ally = mine.Contains(id);
                if (ally) { AllyPops += t.ShockSpent; AllyStunned += t.ShockStunned; AllyStall += t.StallStun; AllyStallShock += t.StallShockStun; AllyDischarge += t.DischargeTaken; }
                else
                {
                    FoePops += t.ShockSpent; FoeStunned += t.ShockStunned; FoeStunEarly += t.ShockStunnedEarly; FoeStall += t.StallStun; FoeStallShock += t.StallShockStun; FoeDischarge += t.DischargeTaken;
                    FoeChains += t.ChainRoots; FoeChainUnits += t.ChainUnits;
                    if (t.ChainSizeHist is { } ch) for (int k = 2; k < ch.Length; k++) FoeChains2 += ch[k];
                }
            }
            if (r.TallyByUnit.TryGetValue(UnitCatalog.Tou.Id, out var tt)) { TouAttacks += tt.Attacks; PowderFoe += tt.ShockOnFoe; PowderAlly += tt.ShockOnAlly; TouRoots += tt.ShockTriggered; PowderMain += tt.PowderMain; PowderSpread += tt.PowderSpread; }
            if (r.TallyByUnit.TryGetValue(UnitCatalog.Fodder.Id, out var ft)) BaitPops += ft.ShockSpent;
            if (r.TallyByUnit.TryGetValue(UnitCatalog.ShigaG3K.Id, out var st)) { Wired += st.WiredSwings; Cowered += st.WhipCowered; ShigaSwings += st.Attacks; }
            foreach (var l in r.Log)
            {
                if (l.Text.Contains("の体が痺れて動かない", StringComparison.Ordinal)) OldStuns++;
                else if (l.Text.Contains("の帯電の足が", StringComparison.Ordinal)) RelicMoves++;
            }
        }

        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
    }

    static Agg Measure(Formation f, Func<List<UnitState>> make, int from = 0, int n = Seeds)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i =>
        {
            var (r, p, e) = CW.Fight(f, make, from + i, verbose: true);
            var a = new Agg(); a.Take(r, p, e); parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string WinT(Agg a) => a.Wins == 0 ? "—" : ((double)a.WinT / a.Wins).ToString("F2");

    static void RunAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = new Dictionary<(int, string, string), Agg>();
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers)
            {
                var f = Boards[bi].Make(v.Tou);
                foreach (var w in Waves) res[(bi, v.Name, w.Name)] = Measure(f, w.Make);
            }

        Console.WriteLine("# 第279期 トウの転生 —— トウ在席の行 × 版 × 波（seed 0..199）");
        Console.WriteLine();
        for (int bi = 0; bi < Boards.Length; bi++)
            Console.WriteLine($"- 台{bi}（{Boards[bi].Group}）{Boards[bi].Name} ＝ " + BA.SeatsNamed(Boards[bi].Make(UnitCatalog.Tou)));
        foreach (var v in Vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine("- 波: 本編第2〜5波（`compare` と同じ口・倍率 115/115）／ ボス ＝ 規定形（倍率なし）／ B3 ＝ チェック波 B3-桁 ／ W3 ＝ チェック波 W3-割合");
        Console.WriteLine();
        string head = "| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " | 第2〜5波 平均 | T0 差 |";
        string sep = "|---|---|" + string.Concat(Waves.Select(_ => "--:|")) + "--:|--:|";

        Console.WriteLine("## 表1 勝率（%）");
        Console.WriteLine();
        Console.WriteLine(head); Console.WriteLine(sep);
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            double m0 = Waves.Where(w => w.Group == "本編").Select(w => res[(bi, "T0", w.Name)].Win).Average();
            foreach (var v in Vers)
            {
                double main = Waves.Where(w => w.Group == "本編").Select(w => res[(bi, v.Name, w.Name)].Win).Average();
                string d = v.Name == "T0" ? "" : (main - m0).ToString("+0.0;-0.0;0.0");
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", Waves.Select(w => F1(res[(bi, v.Name, w.Name)].Win))) + $" | {F1(main)} | {d} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 表2 倒しT（勝った戦で最後の敵が倒れたターンの平均）と 敵へ入れた HP（1〜20 ターン・全員の合計・1戦平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name + " 倒しT")) + " | ボス HP | B3 HP | W3 HP |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")) + "--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers)
            {
                string hp(string w) { var a = res[(bi, v.Name, w)]; return ((double)a.Dealt20 / a.N).ToString("F0"); }
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", Waves.Select(w => WinT(res[(bi, v.Name, w.Name)]))) + $" | {hp("ボス")} | {hp("B3")} | {hp("W3")} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 表3 機構の実測（1戦平均・群ごと）");
        Console.WriteLine();
        Console.WriteLine("攻 ＝ トウの手番の一撃 ／ 粉敵・粉味 ＝ トウの粉で新しく感電した敵・味方 ／ 旧痺 ＝ 旧の痺れ粉が止めた数（T0）／ 弾敵・弾味 ＝ 感電が弾けた駒（敵・味方・トウ以外の書き手の分も含む）／ ");
        Console.WriteLine("痺敵（先）＝ 感電で痺れた敵（そのターンにまだ動いていなかった数）／ 痺味 ＝ 感電で痺れた味方 ／ 潰敵（感）＝ 痺れで潰れた敵の手番（うち感電の痺れ）／ 潰味（感）＝ 同・味方 ／ ");
        Console.WriteLine("放敵・放味 ＝ 放電で削れた HP（受けた側）／ 鞭 ＝ シガの一振り（うち電気鞭・怖気づき）／ 餌 ＝ 弾敵のうちソムの餌（背いた獣）／ ト起 ＝ トウの一撃が起こした連鎖の数 ／ T ＝ 決着T");
        Console.WriteLine();
        Console.WriteLine("第286期に足した列: 粉敵（主・隣）＝ 粉で新しく感電した敵のうち主目標 ／ 主目標の隣 ／ 連鎖 ＝ 敵側の1回の起爆で弾けた敵の平均（2体以上が弾けた起爆の数／戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 群 | 攻 | 粉敵（主・隣） | 粉味 | 旧痺 | 弾敵 | 弾味 | 連鎖（2体以上） | 痺敵（先） | 痺味 | 潰敵（感） | 潰味（感） | 放敵 | 放味 | 鞭（電・怖） | 餌 | ト起 | T |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers)
                foreach (string g in new[] { "本編", "ボス", "B3", "W3" })
                {
                    var a = new Agg();
                    foreach (var w in Waves.Where(w => w.Group == g)) a.Merge(res[(bi, v.Name, w.Name)]);
                    Console.WriteLine($"| {bi} | {v.Name} | {g} | {Per(a.TouAttacks, a.N)} | {Per(a.PowderFoe, a.N)}（{Per(a.PowderMain, a.N)}・{Per(a.PowderSpread, a.N)}）| {Per(a.PowderAlly, a.N)} | {Per(a.OldStuns, a.N)} | {Per(a.FoePops, a.N)} | {Per(a.AllyPops, a.N)} | {(a.FoeChains == 0 ? "—" : ((double)a.FoeChainUnits / a.FoeChains).ToString("F2"))}（{Per(a.FoeChains2, a.N)}）| {Per(a.FoeStunned, a.N)}（{Per(a.FoeStunEarly, a.N)}）| {Per(a.AllyStunned, a.N)} | {Per(a.FoeStall, a.N)}（{Per(a.FoeStallShock, a.N)}）| {Per(a.AllyStall, a.N)}（{Per(a.AllyStallShock, a.N)}）| {Per(a.FoeDischarge, a.N)} | {Per(a.AllyDischarge, a.N)} | {Per(a.ShigaSwings, a.N)}（{Per(a.Wired, a.N)}・{Per(a.Cowered, a.N)}）| {Per(a.BaitPops, a.N)} | {Per(a.TouRoots, a.N)} | {Per(a.Turns, a.N)} |");
                }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>帯B（seed 200..599）の追試: 台 × 版 × 本編第2〜5波の勝率（帯A の T0 差がノイズでないかを見る・verbose なし）。</summary>
    static void BandB()
    {
        Console.WriteLine("| 台 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | T0 差 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            double m0 = 0;
            foreach (var v in Vers)
            {
                var f = Boards[bi].Make(v.Tou);
                var cells = new double[4];
                for (int w = 1; w <= 4; w++)
                {
                    int wins = 0;
                    Parallel.For(200, 600, s => { if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, s, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                    cells[w - 1] = 100.0 * wins / 400;
                }
                double m = cells.Average();
                if (v.Name == "T0") m0 = m;
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", cells.Select(F1)) + $" | {F1(m)} | {(v.Name == "T0" ? "" : (m - m0).ToString("+0.0;-0.0;0.0"))} |");
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // 帯電の足の再測（指示書 §2.5・`design/RELIC_MEASURES.md` §2.1 の手続きを検証台に当てる）
    // ---------------------------------------------------------------------------------

    /// <summary>第279期の帯（使ったことの無い帯: 第271〜273期 0..999 ／ 第276期 1000..1399）。</summary>
    const int RelicA = 1400, RelicB = 1600;

    /// <summary>片側フィッシャー（b の勝ちが a より多い向き）。`RelicDiag.FisherGreater` と同じ式。</summary>
    static double FisherGreater(int winsA, int nA, int winsB, int nB)
    {
        int k = winsA + winsB, n = nA + nB;
        double LogC(int nn, int kk) => LogFact(nn) - LogFact(kk) - LogFact(nn - kk);
        double denom = LogC(n, k), p = 0;
        for (int x = winsB; x <= Math.Min(k, nB); x++)
            p += Math.Exp(LogC(nB, x) + LogC(nA, k - x) - denom);
        return Math.Min(1, p);
    }
    static double LogFact(int n) { double s = 0; for (int i = 2; i <= n; i++) s += Math.Log(i); return s; }

    static void Relic()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var benches = new (string Name, Func<UnitDef, Formation> Make)[]
        {
            ("M0 第273期の検証台（中央 カタ・トウなし・版に依らない）", _ => Bench273(UnitCatalog.KataS3)),
            ("M1 隊列崩しの中央 ガルド → トウ", Boards[4].Make),
            ("M2 第273期の検証台の中央 カタ → トウ", Boards[5].Make),
            ("E1 感電の行の後3 ミオ → トウ", Boards[2].Make),
            ("E2 感電の行の中央 ソム → トウ", Boards[3].Make),
            ("責め苦 (トウ×シガ)", Boards[1].Make),
        };
        string[] frame = { "前1", "前3", "中央", "後1", "後3" };
        Console.WriteLine($"# 第279期 帯電の足の再測 —— 検証台 × 版 × 札の枠5（部隊に1枚）× 本編第2〜5波・帯A seed {RelicA}..{RelicA + Seeds - 1} ／ 帯B {RelicB}..{RelicB + Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("手続きは `design/RELIC_MEASURES.md` §2.1: 分母は帯A で素（札なし・同じ版）が負けるセル。最良の枠（帯A・勝率 → 倒しT）が帯A と帯B の両方で素に対し片側フィッシャー p < 0.05 なら件。発火 ＝ 「帯電の足が … 感電を移した」のログ行（1戦平均・本編4波）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 素（帯A） | 最良の枠 → 勝率（帯A・p） | 帯B 素 → 札（p） | 件 | 発火／戦（枠ごと: 前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3） |");
        Console.WriteLine("|---|---|---|--:|---|---|:-:|---|");
        int hits = 0;
        var hitRows = new List<string>();
        foreach (var (bname, make) in benches)
            foreach (var v in Vers)
            {
                if (bname.StartsWith("M0") && v.Name != "T0") continue;   // M0 は版に依らない
                var f = make(v.Tou);
                for (int w = 1; w <= 4; w++)
                {
                    int ww = w;
                    Func<List<UnitState>> wave = () => BattleEngine.Materialize(EnemyCatalog.Stages[ww].Enemy, BattleContext.EnemyTeam);
                    var a0 = Measure(f, wave, RelicA);
                    var per = new Agg[5];
                    for (int fr = 0; fr < 5; fr++) per[fr] = Measure(f.WithRelic(fr, TraitId.RelicShockStep), wave, RelicA);
                    string fires = string.Join(" ／ ", per.Select(a => Per(a.RelicMoves, a.N)));
                    if (a0.Wins >= Seeds)
                    {
                        Console.WriteLine($"| {bname} | {v.Name} | 第{w + 1}波 | {F1(a0.Win)} | （素が全勝・分母外） | — | | {fires} |");
                        continue;
                    }
                    int best = Enumerable.Range(0, 5).OrderByDescending(fr => per[fr].Wins).ThenBy(fr => per[fr].Wins == 0 ? double.MaxValue : (double)per[fr].WinT / per[fr].Wins).First();
                    double pA = FisherGreater((int)a0.Wins, Seeds, (int)per[best].Wins, Seeds);
                    string bcol = "—"; bool hit = false;
                    if (pA < 0.05)
                    {
                        var b0 = Measure(f, wave, RelicB);
                        var b1 = Measure(f.WithRelic(best, TraitId.RelicShockStep), wave, RelicB);
                        double pB = FisherGreater((int)b0.Wins, Seeds, (int)b1.Wins, Seeds);
                        hit = pB < 0.05;
                        bcol = $"{F1(b0.Win)} → {F1(b1.Win)}（{pB:F3}）";
                    }
                    if (hit) { hits++; hitRows.Add($"{bname} × {v.Name} × 第{w + 1}波 × {frame[best]}（{f[best]!.Name}）"); }
                    Console.WriteLine($"| {bname} | {v.Name} | 第{w + 1}波 | {F1(a0.Win)} | {frame[best]}（{f[best]!.Name}）→ {F1(per[best].Win)}（{pA:F3}） | {bcol} | {(hit ? "○" : "")} | {fires} |");
                }
            }
        Console.WriteLine();
        Console.WriteLine($"**固有の勝者 {hits} 件**" + (hitRows.Count > 0 ? ": " + string.Join(" ／ ", hitRows) : "") + "。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Check()
    {
        int bad = 0;
        void Ok(string what, bool ok) { Console.WriteLine($"{(ok ? "○" : "×")} {what}"); if (!ok) bad++; }

        // 第287期に T3 を規定にしたので、(a)(b)(b2)(c) は「規定 ＝ T3・旧の規定は `TouT0`」の形に直した（第279〜286期の版は「規定 ＝ T0」を確かめていた）。
        Ok("(a) 帯電の粉 ／ 粉の漏れ ／ 舞う粉の保持者は `UnitCatalog.Everyone` で規定のトウ1枚だけ",
           UnitCatalog.Everyone.Where(d => d.Traits.Contains(TraitId.ChargedPowder) || d.Traits.Contains(TraitId.ChargedPowderLeak) || d.Traits.Contains(TraitId.ChargedPowderSpread))
               .All(d => ReferenceEquals(d, UnitCatalog.Tou)));
        Ok("(b) 規定のトウは T3（`TouT3` と同じ物）・`TouT0` は旧の規定（痺れ粉）を明示的に持ち、体は同じ",
           ReferenceEquals(UnitCatalog.TouT3, UnitCatalog.Tou) && UnitCatalog.TouT0.Traits.SequenceEqual(new[] { TraitId.Paralyze })
           && UnitCatalog.TouT0.MaxHp == UnitCatalog.Tou.MaxHp && UnitCatalog.TouT0.Attack == UnitCatalog.Tou.Attack && UnitCatalog.TouT0.Speed == UnitCatalog.Tou.Speed
           && UnitCatalog.TouT0.Pattern == UnitCatalog.Tou.Pattern && UnitCatalog.TouT0.Advances == UnitCatalog.Tou.Advances);
        Ok("(b2) T1 ／ T2 ／ T3 ／ T3n の体・型は T0 と同じで、違うのは札だけ（T2 ＝ T1 − 漏れ ／ T3 ＝ T1 ＋ 舞う ／ T3n ＝ T2 ＋ 舞う）",
           new[] { UnitCatalog.TouT1, UnitCatalog.TouT2, UnitCatalog.TouT3, UnitCatalog.TouT3n }.All(d => d.MaxHp == 46 && d.Attack == 3 && d.Speed == 11 && d.Pattern == UnitCatalog.Tou.Pattern && d.Advances == UnitCatalog.Tou.Advances)
           && UnitCatalog.TouT1.Traits.Except(new[] { TraitId.ChargedPowderLeak }).SequenceEqual(UnitCatalog.TouT2.Traits)
           && UnitCatalog.TouT3.Traits.SequenceEqual(UnitCatalog.TouT1.Traits.Append(TraitId.ChargedPowderSpread))
           && UnitCatalog.TouT3n.Traits.SequenceEqual(UnitCatalog.TouT2.Traits.Append(TraitId.ChargedPowderSpread)));

        // (c) 規定のトウで組んだ台と `TouT3` の台の台本が一致（トウ在席の6台 × 本編第2〜5波 × seed 0..49）。
        bool same = true;
        foreach (var b in Boards.Where(b => b.Group != "参照"))
            foreach (var w in Waves.Where(w => w.Group == "本編"))
                for (int s = 0; s < 50 && same; s++)
                    same &= CW.Dig(CW.Fight(b.Make(UnitCatalog.Tou), w.Make, s).R).SequenceEqual(CW.Dig(CW.Fight(b.Make(UnitCatalog.TouT3), w.Make, s).R));
        Ok("(c) 規定のトウの台本が `TouT3` と一致（6 台 × 本編第2〜5波 × seed 0..49）", same);

        // (d)〜(g) 粉の付与・漏れ・痺れの出どころ（6 台 × 本編4波 × seed 0..19）。
        var sum = Vers.ToDictionary(v => v.Name, _ => new Agg());
        foreach (var v in Vers)
            foreach (var b in Boards.Where(b => b.Group != "参照"))
                foreach (var w in Waves.Where(w => w.Group == "本編"))
                    sum[v.Name].Merge(Measure(b.Make(v.Tou), w.Make, 0, 20));
        Ok($"(d) T1 ／ T2 の粉が敵に感電を付ける（{sum["T1"].PowderFoe} ／ {sum["T2"].PowderFoe}）・T0 は 0（{sum["T0"].PowderFoe}）",
           sum["T1"].PowderFoe > 0 && sum["T2"].PowderFoe > 0 && sum["T0"].PowderFoe == 0);
        Ok($"(e) 漏れ: T1 だけが味方に感電を付ける（T1 {sum["T1"].PowderAlly} ／ T2 {sum["T2"].PowderAlly} ／ T0 {sum["T0"].PowderAlly}）",
           sum["T1"].PowderAlly > 0 && sum["T2"].PowderAlly == 0 && sum["T0"].PowderAlly == 0);
        Ok($"(f) 旧の痺れ粉は T0 にだけ出る（T0 {sum["T0"].OldStuns} ／ T1 {sum["T1"].OldStuns} ／ T2 {sum["T2"].OldStuns}）",
           sum["T0"].OldStuns > 0 && sum["T1"].OldStuns == 0 && sum["T2"].OldStuns == 0);

        // (g) カタのいない台（責め苦）でも、T1 では感電の起爆で敵が痺れる（トウの S3）。T0 では感電の痺れは 0。
        var noKata = Boards[1];
        long Stunned(UnitDef d) { long n = 0; foreach (var w in Waves.Where(w => w.Group == "本編")) n += Measure(noKata.Make(d), w.Make, 0, 20).FoeStunned; return n; }
        long s0 = Stunned(UnitCatalog.TouT0), s1 = Stunned(UnitCatalog.TouT1);
        Ok($"(g) カタのいない台（責め苦）: 感電で痺れた敵 T1 {s1} ＞ 0 ・ T0 {s0} ＝ 0（S3 はトウの札が立てる）", s1 > 0 && s0 == 0);

        // (j)(k) 第286期: 舞う粉は T3 ／ T3n にだけ出る・T3n は味方に漏れない・粉敵 ＝ 主 ＋ 隣
        Ok($"(j) 主目標の隣への粉は T3 ／ T3n にだけ出る（T3 {sum["T3"].PowderSpread} ／ T3n {sum["T3n"].PowderSpread} ／ T1 {sum["T1"].PowderSpread} ／ T2 {sum["T2"].PowderSpread}）",
           sum["T3"].PowderSpread > 0 && sum["T3n"].PowderSpread > 0 && sum["T1"].PowderSpread == 0 && sum["T2"].PowderSpread == 0);
        Ok($"(k) 漏れ: T3n は味方に感電を付けない（{sum["T3n"].PowderAlly}）・T3 は付ける（{sum["T3"].PowderAlly}）・粉敵 ＝ 主 ＋ 隣（T3 {sum["T3"].PowderFoe} ＝ {sum["T3"].PowderMain} ＋ {sum["T3"].PowderSpread}）",
           sum["T3n"].PowderAlly == 0 && sum["T3"].PowderAlly > 0
           && Vers.Skip(1).All(v => sum[v.Name].PowderFoe == sum[v.Name].PowderMain + sum[v.Name].PowderSpread));

        // (h) 決定性: 同じ seed の T1 を2度回して台本が一致。
        bool det = true;
        foreach (var b in Boards.Take(4))
            for (int s = 0; s < 20; s++)
                foreach (var d in new[] { UnitCatalog.TouT1, UnitCatalog.TouT3 })
                    det &= CW.Dig(CW.Fight(b.Make(d), Waves[2].Make, s).R).SequenceEqual(CW.Dig(CW.Fight(b.Make(d), Waves[2].Make, s).R));
        Ok("(h) T1 ／ T3 は seed 決定的（同じ seed の2戦の台本が一致）", det);

        // (i) verbose の有無で結果が変わらない（イベントを積む処理が盤面を変えていない）。
        bool vb = true;
        foreach (var b in Boards)
            foreach (var v in Vers.Skip(1))
                for (int s = 0; s < 20; s++)
                {
                    var a = CW.Fight(b.Make(v.Tou), Waves[1].Make, s, verbose: true).R;
                    var c = CW.Fight(b.Make(v.Tou), Waves[1].Make, s, verbose: false).R;
                    vb &= a.PlayerWon == c.PlayerWon && a.Turns == c.Turns;
                }
        Ok("(i) verbose の有無で勝敗と決着T が一致（T1 ／ T2 ／ T3 ／ T3n × 7 台 × 第3波 × seed 0..19）", vb);

        Console.WriteLine(bad == 0 ? "自己検査: すべて ○" : $"自己検査: × が {bad} 件");
        Environment.ExitCode = bad == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------------------------
    // 第286期 Phase 0: 主目標の席ごとの「隣の敵」の数（敵の盤面ごと）と感電の書き手
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        Console.WriteLine("# 第286期 Phase 0 —— 敵の盤面で主目標の席ごとの「隣の敵」の数（`FormationRules.AreAdjacent(target, n)`・全員生存のとき）");
        Console.WriteLine();
        var boards = new List<(string Name, List<UnitState> E)>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++) boards.Add(($"本編 第{w + 1}波", BattleEngine.Materialize(EnemyCatalog.Stages[w].Enemy, BattleContext.EnemyTeam)));
        boards.Add(("近衛（X 字 5 席）", BattleEngine.MaterializeEnemy(EliteDiag.Five, EliteDiag.Elite)));
        boards.Add(("大隊（9 席）", BattleEngine.MaterializeEnemy(EliteDiag.Nine, EliteDiag.Elite)));
        boards.Add(("ボス（規定形）", BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None)));
        Console.WriteLine("| 盤面 | 体数 | 主目標の席 → 隣の敵の数 | 最大 | 平均 |");
        Console.WriteLine("|---|--:|---|--:|--:|");
        foreach (var (name, e) in boards)
        {
            var cnt = e.Select(u => (u, n: e.Count(x => x != u && FormationRules.AreAdjacent(u, x)))).OrderBy(t => t.u.Slot).ToList();
            string cells = string.Join(" ／ ", cnt.Select(t => $"{t.u.Slot}:{t.u.Name} → {t.n}"));
            Console.WriteLine($"| {name} | {e.Count} | {cells} | {cnt.Max(t => t.n)} | {cnt.Average(t => t.n):F2} |");
        }
        Console.WriteLine();
        string root = Directory.GetCurrentDirectory();
        var writes = Directory.GetFiles(Path.Combine(root, "BattleCore"), "*.cs")
            .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (File: Path.GetFileName(f), Line: i + 1, Text: l.Trim())))
            .Where(x => x.Text.Contains("SetCounter(StatusKeys.Shock, 1)", StringComparison.Ordinal) && !x.Text.StartsWith("//"))
            .ToList();
        Console.WriteLine($"## 感電を付ける書き込み（`SetCounter(StatusKeys.Shock, 1)`）: {writes.Count} 箇所");
        Console.WriteLine();
        foreach (var w in writes) Console.WriteLine($"- `{w.File}:{w.Line}` `{w.Text}`");
    }

    // ---------------------------------------------------------------------------------
    // 第286期: `compare` 64 行 × 版 ／ 精鋭 × トウ在席の行 × 版
    // ---------------------------------------------------------------------------------
    static bool HasTou(Formation f) => f.Occupied().Any(o => o.Def.Id == UnitCatalog.Tou.Id);

    static double[,] Grid(UnitDef d)
    {
        var rows = CompareBuilds();
        var g = new double[rows.Length, EnemyCatalog.Stages.Count];
        Parallel.For(0, rows.Length * EnemyCatalog.Stages.Count, k =>
        {
            int ri = k / EnemyCatalog.Stages.Count, wi = k % EnemyCatalog.Stages.Count;
            var f = FvSwap(rows[ri].F, UnitCatalog.Tou, d);
            int wins = 0;
            for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / Seeds;
        });
        return g;
    }

    static void CompareAll()
    {
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.ToDictionary(v => v.Name, v => Grid(v.Tou));
        var basis = grids["T0"];
        Console.WriteLine("# 第286期 `compare` 64 行 × トウの版（seed 0..199・トウ在席の行だけ `UnitCatalog.Tou` を版に差し替える）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | T0 差 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|");
        for (int ri = 0; ri < rows.Length; ri++)
        {
            if (!HasTou(rows[ri].F)) continue;
            double m0 = Enumerable.Range(1, nw - 1).Average(w => basis[ri, w]);
            foreach (var v in Vers)
            {
                var g = grids[v.Name];
                double m = Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
                Console.WriteLine($"| {rows[ri].Name} | {v.Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w]))) + $" | {F1(m)} | {(v.Name == "T0" ? "" : (m - m0).ToString("+0.0;-0.0;0.0"))} |");
            }
        }
        Console.WriteLine();
        foreach (var v in Vers.Skip(1))
        {
            int bad = 0, cells = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (HasTou(rows[ri].F)) continue;
                for (int w = 0; w < nw; w++) { cells++; if (grids[v.Name][ri, w] != basis[ri, w]) bad++; }
            }
            Console.WriteLine($"- トウのいない行のずれ（{v.Name} − T0）: {cells} セル中 **{bad} 件**");
        }
        var prim = Baseline.PrimaryRows.Select(n => Array.FindIndex(rows, r => r.Name == n)).ToArray();
        Console.WriteLine();
        Console.WriteLine($"| 版 | 全64行 第1〜5波 | 主判定19行 第1〜5波 | 主判定の第五波 − 歯止め（{Baseline.PrimaryFifthFloor}%） |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var v in Vers)
        {
            var g = grids[v.Name];
            Console.WriteLine($"| {v.Name} | " + string.Join(" / ", Enumerable.Range(0, nw).Select(w => F1(Enumerable.Range(0, rows.Length).Average(ri => g[ri, w]))))
                + " | " + string.Join(" / ", Enumerable.Range(0, nw).Select(w => F1(prim.Average(ri => g[ri, w]))))
                + $" | {(prim.Average(ri => g[ri, nw - 1]) - Baseline.PrimaryFifthFloor):+0.0;-0.0} |");
        }
    }

    static void EliteRows()
    {
        var rows = CompareBuilds().Where(r => HasTou(r.F)).ToArray();
        var waves = new (string Name, Func<List<UnitState>> Make)[]
        {
            ("近衛", () => BattleEngine.MaterializeEnemy(EliteDiag.Five, EliteDiag.Elite)),
            ("大隊", () => BattleEngine.MaterializeEnemy(EliteDiag.Nine, EliteDiag.Elite)),
        };
        Console.WriteLine($"# 第286期 精鋭（近衛 ／ 大隊・HP {EliteDiag.Elite.HpPercent}% ／ 攻 {EliteDiag.Elite.AtkPercent}%）× トウ在席の行 × 版（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 版 | 勝率 | 倒しT | 粉敵（主・隣） | 粉味 | 弾敵 | 弾味 | 連鎖（2体以上） | 潰敵（感） | 潰味（感） | 放敵 | 放味 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in rows)
            foreach (var (wn, mk) in waves)
                foreach (var v in Vers)
                {
                    var a = Measure(FvSwap(f, UnitCatalog.Tou, v.Tou), mk);
                    Console.WriteLine($"| {name} | {wn} | {v.Name} | {F1(a.Win)} | {WinT(a)} | {Per(a.PowderFoe, a.N)}（{Per(a.PowderMain, a.N)}・{Per(a.PowderSpread, a.N)}）| {Per(a.PowderAlly, a.N)} | {Per(a.FoePops, a.N)} | {Per(a.AllyPops, a.N)} | "
                        + $"{(a.FoeChains == 0 ? "—" : ((double)a.FoeChainUnits / a.FoeChains).ToString("F2"))}（{Per(a.FoeChains2, a.N)}）| {Per(a.FoeStall, a.N)}（{Per(a.FoeStallShock, a.N)}）| {Per(a.AllyStall, a.N)}（{Per(a.AllyStallShock, a.N)}）| {Per(a.FoeDischarge, a.N)} | {Per(a.AllyDischarge, a.N)} |");
                }
    }

    static void LogOne(int board, string ver, string wave, int seed)
    {
        var v = Vers.First(x => x.Name == ver);
        var w = Waves.First(x => x.Name == (int.TryParse(wave, out int n) ? $"第{n}波" : wave));
        var f = Boards[board].Make(v.Tou);
        var (r, _, _) = CW.Fight(f, w.Make, seed);
        Console.WriteLine($"# 台{board} {Boards[board].Name}（{BA.SeatsNamed(f)}）× {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
