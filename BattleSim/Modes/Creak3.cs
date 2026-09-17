using BattleCore;
using static Common;

// =====================================================================================
// creak3 モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "creak3")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 creak3
// =====================================================================================

static class Creak3Diag
{
public static void Run(string[] args, int stageIndex)
{
    string c3Arg = args.Length > 2 ? args[2] : "";
    bool c3Alt = c3Arg == "alt";
    var c3Sw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> c3Stages = EnemyCatalog.Stages;
    int c3W = c3Stages.Count;
    var c3Roster = UnitCatalog.All.ToArray();
    int c3RN = c3Roster.Length;
    string c3YomiId = UnitCatalog.Yomi.Id;

    var c3All = CompareBuilds().ToArray();
    bool C3HasYomi((string Name, Formation F) row) => row.F.Occupied().Any(o => o.Def.Id == c3YomiId);
    var c3Rows = c3All.Where(C3HasYomi).ToArray();
    var c3Other = c3All.Where(b => !C3HasYomi(b)).ToArray();

    // 味方側で**ヨミを動かせる**駒は3枚だけ（`SwapSlots` の呼び出し全数から。Phase 0-1）。
    // 逃亡（セロ）／棘守り（カド）／喧噪（バサ）。突き返し（ハネ）と曝き（告発人）は
    // `HaulOutPair(Opponent)` なので**敵陣**を動かす——味方のヨミは動かない。
    var c3Movers = new[] { UnitCatalog.Sero.Id, UnitCatalog.Kado.Id, UnitCatalog.Basa.Id };
    // `Whet` 窓口の供給者6枚（`WhetRoute` の7経路の持ち主。号令は開戦と毎Tの2経路で1枚）。
    var c3Feeders = new[] { UnitCatalog.Kari.Id, UnitCatalog.Gan.Id, UnitCatalog.Kugu.Id,
                            UnitCatalog.Shio.Id, UnitCatalog.Golm.Id, UnitCatalog.Hiyo.Id };

    // ---- 版（**測る前に固定**・指示書 §2-2）------------------------------------------------
    const int C3V = 10;
    var c3Rule = new CreakRule[C3V]
    {
        new(0, CreakSource.Whet),
        new(9, CreakSource.Bonus), new(18, CreakSource.Bonus), new(30, CreakSource.Bonus),
        new(2, CreakSource.Whet),  new(6, CreakSource.Whet),   new(12, CreakSource.Whet),
        new(9, CreakSource.Both),  new(18, CreakSource.Both),  new(30, CreakSource.Both),
    };
    string[] c3Name =
    {
        "V0（無効）",
        "B9 自己 9", "B18 自己 18", "B30 自己 30",
        "W2 外部 2", "W6 外部 6", "W12 外部 12",
        "T9 合計 9", "T18 合計 18", "T30 合計 30",
    };
    // 族: 1 = Bonus（自己供給）/ 2 = Whet（外部供給）/ 3 = Both（合計）
    int C3Fam(int v) => v == 0 ? 0 : v <= 3 ? 1 : v <= 6 ? 2 : 3;
    string[] c3FamName = { "—", "**自己**", "**外部**", "合計" };

    int C3ProbeIdx(int v) => C3Fam(v) switch
    {
        1 or 3 => Array.IndexOf(UnitTally.CreakProbes, c3Rule[v].Threshold),
        2 => Array.IndexOf(UnitTally.CreakWhetProbes, c3Rule[v].Threshold),
        _ => -1,
    };
    int[]? C3ProbeArr(int v, UnitTally t) => C3Fam(v) switch
    {
        1 => t.CreakProbeTurn,
        2 => t.CreakWhetProbeTurn,
        3 => t.CreakBothProbeTurn,
        _ => null,
    };

    // ---- 1版ぶんの計数（**どれも誰も読んで分岐しない**）---------------------------------------
    // 0 戦数 / 1 振（分母）/ 2 薙ぎ（分子）/ 3 軋みの上昇Σ / 4 外から届いたΣ / 5 うち吐き戻し /
    // 6 AtkBonus の最大Σ / 7 WhetReceived の最大Σ / 8 閾値到達の戦数 / 9 その初到達TΣ /
    // 10 一度でも動かされた戦数 / 11 一度でも押された戦数
    const int C3C = 12;
    void C3Acc(BattleResult r, int v, double[] a, int off)
    {
        if (!r.TallyByUnit.TryGetValue(c3YomiId, out UnitTally? t)) return;
        a[off + 0]++;
        a[off + 1] += t.CreakSwings; a[off + 2] += t.CreakSweeps;
        a[off + 3] += t.CreakSelfGain; a[off + 4] += t.CreakWhetGain; a[off + 5] += t.CreakRegurgGain;
        a[off + 6] += t.CreakMaxBonus; a[off + 7] += t.CreakWhetMax;
        if (t.CreakSelfGain > 0) a[off + 10]++;
        if (t.CreakWhetGain > 0) a[off + 11]++;
        int pi = C3ProbeIdx(v);
        int[]? pa = C3ProbeArr(v, t);
        if (pi >= 0 && pa is not null && pa[pi] != 0) { a[off + 8]++; a[off + 9] += pa[pi]; }
    }
    double C3Rate(double[] a, int off) => a[off + 1] > 0 ? a[off + 2] * 100.0 / a[off + 1] : 0;

    string C3P1(double x) => double.IsNaN(x) ? "—" : (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    string C3P2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");

    // ---- 理想台（`compare` と同じ seed 帯）---------------------------------------------------
    const int C3Seeds = 200;
    int c3Seed0 = c3Alt ? 200 : 0;
    (double[] Win, double[] Cnt) C3Ideal(Formation f, int v)
    {
        var win = new double[c3W];
        var cnt = new double[C3C];
        for (int w = 0; w < c3W; w++)
        {
            int wins = 0;
            for (int seed = c3Seed0; seed < c3Seed0 + C3Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, c3Stages[w].Enemy, seed, verbose: false, creak: c3Rule[v]);
                if (r.PlayerWon) wins++;
                C3Acc(r, v, cnt, 0);
            }
            win[w] = wins * 100.0 / C3Seeds;
        }
        return (win, cnt);
    }
    int C3Info(double[] w) => w.Skip(1).Count(x => x > 0.0 && x < 100.0);

    // ---- ドラフト台 Pw（第69〜76期の写し。**1文字も変えていない**）-----------------------------
    const int C3OfferSeed = 2_000_000;
    const int C3N = 11000, C3M = 8;
    const int C3Strong = 7;
    const int C3WeakPct = 60;
    int c3Band = c3Alt ? 200 : 0;

    var c3WeakCache = new Dictionary<string, UnitDef>();
    UnitDef C3WeakOf(UnitDef d)
    {
        if (c3WeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * C3WeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        c3WeakCache[d.Id] = w;
        return w;
    }
    var c3Weak = c3Stages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = C3WeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef[] C3Team(int i)
    {
        var rng = new Random(C3OfferSeed + i);
        var idx = new int[c3RN];
        for (int k = 0; k < c3RN; k++) idx[k] = k;
        int remain = c3RN, strong = 0;
        var picked = new UnitDef[5];
        for (int r = 0; r < 5; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = c3Roster[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= C3Strong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(c3Roster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int[] C3Seats(UnitDef[] u)
    {
        var all5 = new[] { 0, 1, 2, 3, 4 };
        var front = all5.OrderByDescending(k => u[k].MaxHp)
                        .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        var rest = all5.Where(k => k != front[0] && k != front[1]).ToArray();
        var back = rest.OrderByDescending(k => u[k].Attack)
                       .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        int center = rest.Single(k => k != back[0] && k != back[1]);
        var r = new int[5];
        r[front[0]] = 0; r[front[1]] = 1; r[center] = 2; r[back[0]] = 3; r[back[1]] = 4;
        return r;
    }
    Formation C3Form(UnitDef[] u, int[] seats)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = u[k];
        return f;
    }
    var c3KeyOf = c3Roster.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);
    int C3MoveKeys(UnitDef[] team)
        => team.Count(x => c3KeyOf.TryGetValue(x.Id, out int[]? k) && k.Contains(UnitTally.CarryMove));

    double C3Slope(IReadOnlyList<double> rate, IReadOnlyList<int> box)
    {
        var bk = new List<double>[4];
        for (int j = 0; j < 4; j++) bk[j] = new List<double>();
        for (int i = 0; i < rate.Count; i++) bk[Math.Min(3, box[i])].Add(rate[i]);
        double d10 = (bk[0].Count > 0 && bk[1].Count > 0) ? bk[1].Average() - bk[0].Average() : double.NaN;
        double d21 = (bk[1].Count > 0 && bk[2].Count > 0) ? bk[2].Average() - bk[1].Average() : double.NaN;
        return double.IsNaN(d10) ? d21 : double.IsNaN(d21) ? d10 : (d10 + d21) / 2;
    }
    double[] C3BoxMeans(IReadOnlyList<double> rate, IReadOnlyList<int> box)
    {
        var r = new double[4];
        for (int j = 0; j < 4; j++)
        {
            var xs = new List<double>();
            for (int i = 0; i < rate.Count; i++) if (Math.Min(3, box[i]) == j) xs.Add(rate[i]);
            r[j] = xs.Count > 0 ? xs.Average() : double.NaN;
        }
        return r;
    }

    // 1行ぶんの分布（Phase 0-3 用）。**規則は無効のまま回す。**
    int[] c3Grid = { 1, 2, 4, 6, 8, 9, 12, 16, 18, 24, 30, 32 };
    void C3DistRow(string label, Func<int, Formation> form, int rows, bool ideal)
    {
        var bonusMax = new List<int>(); var whetMax = new List<int>();
        var selfSum = new List<int>(); var whetSum = new List<int>(); var swings = new List<int>();
        var tSelf = new double[UnitTally.CreakProbes.Length];
        var nSelf = new double[UnitTally.CreakProbes.Length];
        var tWhet = new double[UnitTally.CreakWhetProbes.Length];
        var nWhet = new double[UnitTally.CreakWhetProbes.Length];
        int lo = ideal ? c3Seed0 : c3Band, hi = ideal ? c3Seed0 + C3Seeds : c3Band + C3M;
        for (int r0 = 0; r0 < rows; r0++)
        {
            Formation f = form(r0);
            for (int w = 0; w < c3W; w++)
                for (int seed = lo; seed < hi; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, ideal ? c3Stages[w].Enemy : c3Weak[w].Enemy,
                                                      seed, verbose: false);
                    if (!r.TallyByUnit.TryGetValue(c3YomiId, out UnitTally? t)) continue;
                    bonusMax.Add(t.CreakMaxBonus); whetMax.Add(t.CreakWhetMax);
                    selfSum.Add(t.CreakSelfGain); whetSum.Add(t.CreakWhetGain); swings.Add(t.CreakSwings);
                    if (t.CreakProbeTurn is not null)
                        for (int k = 0; k < UnitTally.CreakProbes.Length; k++)
                            if (t.CreakProbeTurn[k] != 0) { nSelf[k]++; tSelf[k] += t.CreakProbeTurn[k]; }
                    if (t.CreakWhetProbeTurn is not null)
                        for (int k = 0; k < UnitTally.CreakWhetProbes.Length; k++)
                            if (t.CreakWhetProbeTurn[k] != 0) { nWhet[k]++; tWhet[k] += t.CreakWhetProbeTurn[k]; }
                }
        }
        int n = Math.Max(1, bonusMax.Count);
        Console.Write($"| {label} | `AtkBonus` | {swings.Average():F2} | {selfSum.Average():F1} | {whetSum.Average():F1} | {bonusMax.Average():F1} |");
        foreach (int q in c3Grid) Console.Write($" {100.0 * bonusMax.Count(x => x >= q) / n:F1}% |");
        Console.WriteLine();
        Console.Write("|  | 到達T |  |  |  |  |");
        foreach (int q in c3Grid)
        {
            int i = Array.IndexOf(UnitTally.CreakProbes, q);
            Console.Write(i >= 0 && nSelf[i] > 0 ? $" {tSelf[i] / nSelf[i]:F2} |" : " — |");
        }
        Console.WriteLine();
        Console.Write($"|  | `WhetReceived` |  |  |  | {whetMax.Average():F1} |");
        foreach (int q in c3Grid) Console.Write($" {100.0 * whetMax.Count(x => x >= q) / n:F1}% |");
        Console.WriteLine();
        Console.Write("|  | 到達T |  |  |  |  |");
        foreach (int q in c3Grid)
        {
            int i = Array.IndexOf(UnitTally.CreakWhetProbes, q);
            Console.Write(i >= 0 && nWhet[i] > 0 ? $" {tWhet[i] / nWhet[i]:F2} |" : " — |");
        }
        Console.WriteLine();
    }

    // =====================================================================================
    // phase0: 紙の計算と分布（**`CreakRule` は無効のまま。盤面は1つも動かない**）
    // =====================================================================================
    if (c3Arg == "phase0")
    {
        Console.WriteLine("# 第77期 Phase 0 —— 自己供給と外部供給（**規則は無効のまま測る**）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 creak3 phase0` の出力。**`docs/` には置かない。**");
        Console.WriteLine();

        // ---- 0-1. 選択子の検算 ----
        Console.WriteLine("## 0-1. 選択子の追加が盤面を動かしていないか（**受け入れ基準 1**）");
        Console.WriteLine();
        int c3Diff0 = 0;
        foreach ((string _, Formation f) in c3All)
            for (int w = 0; w < c3W; w++)
                for (int seed = 0; seed < C3Seeds; seed++)
                    if (BattleEngine.Run(f, c3Stages[w].Enemy, seed, verbose: false).PlayerWon
                        != BattleEngine.Run(f, c3Stages[w].Enemy, seed, verbose: false, creak: CreakRule.Default).PlayerWon)
                        c3Diff0++;
        Console.WriteLine($"`creak` を渡さない版 と `CreakRule.Default`（= `(0, Whet)`）を渡した版を、");
        Console.WriteLine($"**{c3All.Length} 行 × {c3W} 波 × {C3Seeds} seed** で1試行ずつ突き合わせた → **ずれ {c3Diff0} 件**。");
        Console.WriteLine();
        Console.WriteLine($"`docs/balance.md` の現在値は **{c3All.Length} 行 × {c3W} 波 = {c3All.Length * c3W} セル**。");
        Console.WriteLine();

        // ---- 0-2. ヨミの在席標本数 ----
        Console.WriteLine("## 0-2. ヨミはドラフト台 Pw に何回入るか（**抽選だけ。戦闘0回**）");
        Console.WriteLine();
        const int C3Sim = 100000;
        int c3LotY = 0, c3LotMove = 0, c3LotFeed = 0, c3LotBoth = 0, c3LotNone = 0;
        var c3LotMoveN = new int[4];
        for (int i = 0; i < C3Sim; i++)
        {
            var ids = C3Team(i).Select(x => x.Id).ToHashSet();
            if (!ids.Contains(c3YomiId)) continue;
            c3LotY++;
            bool mv = c3Movers.Any(ids.Contains);
            bool fd = c3Feeders.Any(ids.Contains);
            if (mv) c3LotMove++;
            if (fd) c3LotFeed++;
            if (mv && fd) c3LotBoth++;
            if (!mv && !fd) c3LotNone++;
            c3LotMoveN[Math.Min(3, c3Movers.Count(ids.Contains))]++;
        }
        Console.WriteLine("| 量 | 抽選 100,000 標本 | 11,000 標本での期待 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| ヨミ在席 | {100.0 * c3LotY / C3Sim:F2}% | **{C3N * c3LotY / C3Sim:N0}** 件 |");
        Console.WriteLine($"| 一様なら（5 / {c3RN}） | {500.0 / c3RN:F2}% | {C3N * 5 / c3RN:N0} 件 |");
        Console.WriteLine();
        {
            double hyp = 1.0;
            for (int k = 0; k < 5; k++) hyp *= (double)(c3RN - 1 - k) / (c3RN - k);
            Console.WriteLine($"**超幾何との照合**: 無作為5枚ならヨミ在席は **{100.0 * (1 - hyp):F2}%**"
                              + $"（規則 P の実測は {100.0 * c3LotY / C3Sim:F2}%。P は攻撃力／HP で選ぶので一様ではない）。");
        }
        Console.WriteLine();
        Console.WriteLine("**ヨミが在席した標本の中で**（分母 = 在席標本）:");
        Console.WriteLine();
        Console.WriteLine("| 相方 | 割合 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| 移動の書き手（セロ / カド / バサ のいずれか） | **{100.0 * c3LotMove / Math.Max(1, c3LotY):F1}%** |");
        Console.WriteLine($"| 強化の供給者（カリ / ガン / クグ / シオ / ゴルム / ヒヨ のいずれか） | **{100.0 * c3LotFeed / Math.Max(1, c3LotY):F1}%** |");
        Console.WriteLine($"| 両方 | {100.0 * c3LotBoth / Math.Max(1, c3LotY):F1}% |");
        Console.WriteLine($"| **どちらも無し** | **{100.0 * c3LotNone / Math.Max(1, c3LotY):F1}%** |");
        Console.WriteLine();
        Console.WriteLine("| 移動の書き手の枚数 | 0 | 1 | 2 | 3 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        Console.WriteLine("| 割合 | " + string.Join(" | ", c3LotMoveN.Select(x => $"{100.0 * x / Math.Max(1, c3LotY):F1}%")) + " |");
        Console.WriteLine();
        Console.WriteLine("> **理想台の3行はすべて移動の書き手を持つ**（隊列崩し＝バサ＋セロ / 突き出し＝セロ / 移動改＝バサ＋セロ）。");
        Console.WriteLine("> **ドラフト台では相方が保証されない**——これが指示書 §0-2 の「構造的に不利」の分母である。");
        Console.WriteLine("> **ただし第五波の告発人（曝き）は敵側から味方を動かす**ので、移動の供給は 0 にはならない。");
        Console.WriteLine();

        // ---- 0-3. 分布 ----
        Console.WriteLine("## 0-3. ヨミの `AtkBonus` と `WhetReceived` の分布（V0・**規則は無効**）");
        Console.WriteLine();
        Console.WriteLine("格子への到達% は戦闘中の最大値から、到達T は計数の初到達ターンから（格子外は `—`）。");
        Console.WriteLine("**第67期の判定（2値 / 連続 / 不発）をそのまま当てる。**");
        Console.WriteLine();
        Console.Write("| 行 | 量 | 振/戦 | 軋み | 押され | 最大値 |");
        foreach (int q in c3Grid) Console.Write($" {q} |");
        Console.WriteLine();
        Console.Write("|---|---|--:|--:|--:|--:|");
        foreach (int _ in c3Grid) Console.Write("--:|");
        Console.WriteLine();
        foreach ((string rn, Formation f) in c3Rows) C3DistRow(rn, _ => f, 1, ideal: true);

        var c3Samp = new List<int>();
        for (int i = 0; i < C3N && c3Samp.Count < 300; i++)
            if (C3Team(i).Any(x => x.Id == c3YomiId)) c3Samp.Add(i);
        C3DistRow($"**ドラフト台 Pw**（在席 {c3Samp.Count} 標本）",
                  k => { UnitDef[] t = C3Team(c3Samp[k]); return C3Form(t, C3Seats(t)); },
                  c3Samp.Count, ideal: false);
        Console.WriteLine();

        // ---- 0-4. 紙で薙ぎ化率を予測する ----
        Console.WriteLine("## 0-4. 紙の予測 —— 閾値 9 は「1回でも動かされたか」と同値");
        Console.WriteLine();
        Console.WriteLine($"軋みの上昇は **{DisplacedTrait.Gain} / {DisplacedTrait.PushedToFrontGain} の2段**なので、");
        Console.WriteLine("`AtkBonus` の閾値 9 は**1回でも動かされれば必ず越える**。");
        Console.WriteLine("18 は「2回（または突き出し1回）」、30 は「4回（または突き出し＋1回）」に相当する。");
        Console.WriteLine();
        Console.WriteLine("**外部供給の側**は、`Whet` の7経路のうち**開戦時一括の号令の鬨が最大**（第56期 3.36 量/戦）で、");
        Console.WriteLine("第67期はそれが 8.0 を一括で届けたために閾値 1〜8 の到達Tが全部 1.00 に潰れた。");
        Console.WriteLine("**ドラフト台では供給者そのものが同席しない標本が大半**なので、");
        Console.WriteLine("**2値に潰れるのではなく不発になる**——それが P3 の予測。");
        Console.WriteLine();

        // ---- 0-5. 理想台のヨミ3行と歯止めの実効レンジ ----
        Console.WriteLine("## 0-5. 理想台のヨミ3行・主判定19行・歯止めの実効レンジ（自己検査 (c)）");
        Console.WriteLine();
        var c3Prim = new HashSet<string>(Baseline.PrimaryRows);
        var c3In = c3Rows.Where(b => c3Prim.Contains(b.Name)).Select(b => b.Name).ToArray();
        Console.WriteLine($"ヨミを含む行は **{c3Rows.Length} 行**、うち主判定 {Baseline.PrimaryRows.Length} 行に **{c3In.Length} 行**"
                          + $"（{string.Join(" / ", c3In)}）。");
        Console.WriteLine();
        var c3PrimIdx0 = Baseline.PrimaryRows.Select(n => Array.FindIndex(c3All, b => b.Name == n))
                                             .Where(i => i >= 0).ToArray();
        double[] c3Fifth0 = c3PrimIdx0.Select(i => C3Ideal(c3All[i].Item2, 0).Win[4]).ToArray();
        double c3Avg0 = c3Fifth0.Average();
        double c3Range = c3In.Select(n => C3Ideal(c3All[Array.FindIndex(c3All, b => b.Name == n)].Item2, 0).Win[4])
                             .Sum() / c3Fifth0.Length;
        double c3Room = c3Avg0 - Baseline.PrimaryFifthFloor;
        Console.WriteLine($"主判定の第五波平均（V0）= **{c3Avg0:F1}%**、歯止め {Baseline.PrimaryFifthFloor:F1}% との余裕 **{c3Room:F1}pt**。");
        Console.WriteLine($"重なる {c3In.Length} 行が第五波で 0.0% まで落ちても平均の低下は最大 **{c3Range:F2}pt**"
                          + $"——**実効レンジがこの幅**（余裕 {c3Room:F1}pt "
                          + $"{(c3Range > c3Room ? "**より大きい ＝ Q5 は拒否権として実効を持つ**" : "**より小さい ＝ Q5 は構造的に発動しない**")}）。");
        Console.WriteLine();
        Console.WriteLine("## 0-6. 陰性対照の分母（Q7）");
        Console.WriteLine();
        Console.WriteLine($"ヨミを含まない **{c3Other.Length} 行** × {c3W} 波 = **{c3Other.Length * c3W} セル** × 版 {C3V - 1} 本。");
        Console.WriteLine();
        Console.WriteLine($"所要 {c3Sw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // ideal66: **第66期 §4-4 の帯（seed 200..599）での再現確認だけ**。
    // 判定基準は1つも変えない——第66期の B 帯は 400 seed で、第73〜76期の器具（200 seed）とは幅が違う。
    // **どちらが正かではなく「Q4 の拒否権がどの帯で発火するか」を並べるための再現**である。
    // =====================================================================================
    if (c3Arg == "ideal66")
    {
        Console.WriteLine("# 第77期 —— 理想台のヨミ3行を**第66期の B 帯**（seed 200..599）で（再現確認）");
        Console.WriteLine();
        Console.WriteLine("**判定基準は変えていない。** 第66期 §4-4 の帯は 400 seed、第73〜76期の器具は 200 seed で幅が違うので、");
        Console.WriteLine("Q4 の拒否権がどの帯で発火するかを並べるためだけに回す。");
        Console.WriteLine();
        (double[] Win, double[] Cnt) C3Ideal66(Formation f, int v)
        {
            var win = new double[c3W];
            var cnt = new double[C3C];
            for (int w = 0; w < c3W; w++)
            {
                int wins = 0;
                for (int seed = 200; seed < 600; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, c3Stages[w].Enemy, seed, verbose: false, creak: c3Rule[v]);
                    if (r.PlayerWon) wins++;
                    C3Acc(r, v, cnt, 0);
                }
                win[w] = wins * 100.0 / 400;
            }
            return (win, cnt);
        }
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 帰属 | 薙ぎ化率 | 情報セル |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach ((string rn, Formation f) in c3Rows)
        {
            double b0 = C3Ideal66(f, 0).Win.Average();
            for (int v = 0; v < C3V; v++)
            {
                var g = C3Ideal66(f, v);
                Console.WriteLine($"| {(v == 0 ? rn : "")} | {c3Name[v]} | " + string.Join(" | ", g.Win.Select(x => $"{x:F1}%"))
                                  + $" | {g.Win.Average():F1}% | {(v == 0 ? "—" : C3P1(g.Win.Average() - b0))} "
                                  + $"| {(v == 0 ? "—" : $"{C3Rate(g.Cnt, 0):F1}%")} | {C3Info(g.Win)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {c3Sw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check: 陰性対照（Q7）
    // =====================================================================================
    if (c3Arg == "check")
    {
        Console.WriteLine("# 第77期 —— 陰性対照（Q7）");
        Console.WriteLine();
        int d1 = 0;
        foreach ((string _, Formation f) in c3All)
            for (int w = 0; w < c3W; w++)
                for (int seed = 0; seed < C3Seeds; seed++)
                    if (BattleEngine.Run(f, c3Stages[w].Enemy, seed, verbose: false).PlayerWon
                        != BattleEngine.Run(f, c3Stages[w].Enemy, seed, verbose: false, creak: CreakRule.Default).PlayerWon)
                        d1++;
        Console.WriteLine($"**(1) 引数なし**: {c3All.Length} 行 × {c3W} 波 × {C3Seeds} seed を"
                          + $" `creak` 無し / `CreakRule.Default` で突き合わせ → **ずれ {d1} 件**"
                          + $"（セル数 {c3All.Length * c3W}）。");
        Console.WriteLine();

        int d2 = 0;
        var d2Rows = new List<string>();
        var c3OtherArr = c3Other.ToArray();
        var d2Each = new int[c3OtherArr.Length];
        Parallel.For(0, c3OtherArr.Length, r0 =>
        {
            Formation f = c3OtherArr[r0].F;
            double[] b0 = C3Ideal(f, 0).Win;
            int bad = 0;
            for (int v = 1; v < C3V; v++)
            {
                double[] bv = C3Ideal(f, v).Win;
                for (int w = 0; w < c3W; w++) if (Math.Abs(b0[w] - bv[w]) > 1e-9) bad++;
            }
            d2Each[r0] = bad;
        });
        for (int r0 = 0; r0 < c3OtherArr.Length; r0++)
        {
            d2 += d2Each[r0];
            if (d2Each[r0] > 0) d2Rows.Add(c3OtherArr[r0].Name);
        }
        Console.WriteLine($"**(2) ヨミを含まない行**: {c3Other.Length} 行 × {c3W} 波 = {c3Other.Length * c3W} セルを"
                          + $"版 {C3V - 1} 本で照合 → **ずれ {d2} 件**"
                          + (d2Rows.Count == 0 ? "。" : $"（{string.Join(" / ", d2Rows)}）。"));
        Console.WriteLine();
        Console.WriteLine($"所要 {c3Sw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // 主表: ドラフト台 Pw（主判定）＋ 理想台のヨミ3行（拒否権）
    // =====================================================================================
    string c3BandName = c3Alt ? "B" : "A";
    Console.WriteLine($"# 第77期 —— 自己供給と外部供給（{c3BandName} 帯: ドラフト seed {c3Band}..{c3Band + C3M - 1} / 理想 seed {c3Seed0}..{c3Seed0 + C3Seeds - 1}）");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 creak3{(c3Alt ? " alt" : "")}` の出力。**`docs/` には置かない。**");
    Console.WriteLine();
    Console.WriteLine($"版 {C3V} 本（V0 ＋ 自己 9/18/30 ＝第66期の3点 ＋ 外部 2/6/12 ＝第67期の3点 ＋ 合計 9/18/30）。");
    Console.WriteLine("**席は動かさない。行も標本も第69〜76期の写し。**");
    Console.WriteLine();

    // ---- ドラフト台の走査 ----
    var c3DWin = new int[C3N][];
    var c3DHas = new bool[C3N];
    var c3DBox = new int[C3N];
    var c3DMove = new bool[C3N];
    var c3DFeed = new bool[C3N];
    var c3DCnt = new double[C3N][];
    int c3Done = 0;
    Console.Error.Write($"{c3BandName}帯 Pw: ");
    Parallel.For(0, C3N, i =>
    {
        UnitDef[] team = C3Team(i);
        int[] seats = C3Seats(team);
        var ids = team.Select(x => x.Id).ToHashSet();
        bool has = ids.Contains(c3YomiId);
        Formation f = C3Form(team, seats);
        var win = new int[C3V * c3W];
        var cnt = has ? new double[C3V * C3C] : null;
        for (int v = 0; v < C3V; v++)
        {
            if (v > 0 && !has) continue;
            for (int w = 0; w < c3W; w++)
                for (int seed = c3Band; seed < c3Band + C3M; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, c3Weak[w].Enemy, seed, verbose: false, creak: c3Rule[v]);
                    if (r.PlayerWon) win[v * c3W + w]++;
                    if (cnt is not null) C3Acc(r, v, cnt, v * C3C);
                }
        }
        c3DWin[i] = win; c3DHas[i] = has; c3DCnt[i] = cnt!;
        c3DBox[i] = Math.Min(3, C3MoveKeys(team));
        c3DMove[i] = c3Movers.Any(ids.Contains);
        c3DFeed[i] = c3Feeders.Any(ids.Contains);
        int c = Interlocked.Increment(ref c3Done);
        if (c % 1000 == 0) Console.Error.Write(".");
    });
    Console.Error.WriteLine();

    double C3DRate(int i, int v, int w)
        => (v == 0 || c3DHas[i] ? c3DWin[i][v * c3W + w] : c3DWin[i][w]) * 100.0 / C3M;
    double[] C3DRates(int v, int wave)
        => Enumerable.Range(0, C3N).Select(i => wave < 0
            ? Enumerable.Range(1, c3W - 1).Average(w => C3DRate(i, v, w))
            : C3DRate(i, v, wave)).ToArray();
    double C3DMean(IEnumerable<int> idx, int v)
        => idx.Average(i => Enumerable.Range(1, c3W - 1).Average(w => C3DRate(i, v, w)));

    var c3Idx = Enumerable.Range(0, C3N).Where(i => c3DHas[i]).ToArray();
    var c3Agg = new double[C3V * C3C];
    foreach (int i in c3Idx) for (int c = 0; c < C3V * C3C; c++) c3Agg[c] += c3DCnt[i][c];

    Console.WriteLine($"**ヨミの在席標本 = {c3Idx.Length:N0} / {C3N:N0}**"
                      + $"（{100.0 * c3Idx.Length / C3N:F2}%。一様なら 5/{c3RN} = {500.0 / c3RN:F2}%）。");
    Console.WriteLine($"うち移動の相方あり **{c3Idx.Count(i => c3DMove[i]):N0}** / 強化の供給者あり **{c3Idx.Count(i => c3DFeed[i]):N0}** / "
                      + $"どちらも無し **{c3Idx.Count(i => !c3DMove[i] && !c3DFeed[i]):N0}**。");
    Console.WriteLine();

    // ---- 表A ----
    Console.WriteLine("## 表A —— ドラフト台 Pw（**在席標本だけ**・第2〜5波）");
    Console.WriteLine();
    Console.WriteLine("| 版 | 族 | 在席時勝率 | **帰属** | **薙ぎ化率** | 到達% | 初到達T | 振/戦 | 軋み | 押され | 動かされた% | 押された% |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var c3DAttr = new double[C3V];
    double c3D0 = C3DMean(c3Idx, 0);
    for (int v = 0; v < C3V; v++)
    {
        int off = v * C3C;
        double a = C3DMean(c3Idx, v);
        c3DAttr[v] = a - c3D0;
        double n = Math.Max(1, c3Agg[off + 0]);
        Console.WriteLine($"| {c3Name[v]} | {c3FamName[C3Fam(v)]} | {a:F2}% "
                          + $"| {(v == 0 ? "—" : $"**{C3P2(c3DAttr[v])}**")} "
                          + $"| {(v == 0 ? "—" : $"**{C3Rate(c3Agg, off):F1}%**")} "
                          + $"| {(v == 0 ? "—" : $"{100.0 * c3Agg[off + 8] / n:F1}%")} "
                          + $"| {(v == 0 || c3Agg[off + 8] == 0 ? "—" : $"{c3Agg[off + 9] / c3Agg[off + 8]:F2}")} "
                          + $"| {c3Agg[off + 1] / n:F2} | {c3Agg[off + 3] / n:F1} | {c3Agg[off + 4] / n:F1} "
                          + $"| {100.0 * c3Agg[off + 10] / n:F1}% | {100.0 * c3Agg[off + 11] / n:F1}% |");
    }
    Console.WriteLine();

    // ---- 表B ----
    Console.WriteLine("## 表B —— 相方の有無で割る（**この期の主題**）");
    Console.WriteLine();
    var c3Grp = new (string Name, int[] Idx)[]
    {
        ("移動の相方あり", c3Idx.Where(i => c3DMove[i]).ToArray()),
        ("移動の相方なし", c3Idx.Where(i => !c3DMove[i]).ToArray()),
        ("強化の供給者あり", c3Idx.Where(i => c3DFeed[i]).ToArray()),
        ("強化の供給者なし", c3Idx.Where(i => !c3DFeed[i]).ToArray()),
        ("**どちらも無し**", c3Idx.Where(i => !c3DMove[i] && !c3DFeed[i]).ToArray()),
    };
    Console.WriteLine("| 群 | 標本 | V0 | " + string.Join(" | ", Enumerable.Range(1, C3V - 1).Select(v => c3Name[v])) + " |");
    Console.WriteLine("|---|--:|--:|" + string.Concat(Enumerable.Repeat("--:|", C3V - 1)));
    foreach ((string gn, int[] gi) in c3Grp)
    {
        if (gi.Length == 0) { Console.WriteLine($"| {gn} | 0 | — |" + string.Concat(Enumerable.Repeat(" — |", C3V - 1))); continue; }
        double b = C3DMean(gi, 0);
        Console.WriteLine($"| {gn} | {gi.Length:N0} | {b:F2}% | "
                          + string.Join(" | ", Enumerable.Range(1, C3V - 1).Select(v => C3P2(C3DMean(gi, v) - b))) + " |");
    }
    Console.WriteLine();
    Console.WriteLine("**セルは帰属（版 − V0）。** 群の分け方は測る前に固定（移動の書き手3枚 / `Whet` の供給者6枚）。");
    Console.WriteLine();

    // ---- 表B-2（波ごと）----
    Console.WriteLine("## 表B-2 —— 波ごとの帰属（自己 B9 / 外部 W2・群別）");
    Console.WriteLine();
    Console.WriteLine("**味方側でヨミを動かせるのは3枚だけ**（セロ / カド / バサ）。**敵側は第五波の告発人（曝き）1枚**"
                      + $"（`ExposeRule.MaxPerBattle` = {ExposeRule.Default.MaxPerBattle}）——");
    Console.WriteLine("移動の相方がいない標本でヨミが動かされる経路はそこだけである。");
    Console.WriteLine();
    Console.WriteLine("| 群 | 版 | 第2波 | 第3波 | 第4波 | 第5波 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|");
    foreach ((string gn, int[] gi) in c3Grp)
    {
        if (gi.Length == 0) continue;
        foreach (int v in new[] { 1, 4 })
            Console.WriteLine($"| {(v == 1 ? gn : "")} | {c3Name[v]} | "
                + string.Join(" | ", Enumerable.Range(1, c3W - 1).Select(w =>
                    C3P2(gi.Average(i => C3DRate(i, v, w)) - gi.Average(i => C3DRate(i, 0, w))))) + " |");
    }
    Console.WriteLine();

    // ---- 表C ----
    Console.WriteLine("## 表C —— 傾き（箱 = 移動キーを持つ駒の枚数・全 11,000 標本）");
    Console.WriteLine();
    Console.WriteLine($"箱: **0枚 {c3DBox.Count(x => x == 0):N0} / 1枚 {c3DBox.Count(x => x == 1):N0} / "
                      + $"2枚 {c3DBox.Count(x => x == 2):N0} / 3+ {c3DBox.Count(x => x == 3):N0}**"
                      + "（ヨミ自身も移動キーの持ち手なので、在席標本は必ず1枚以上の箱に入る）。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 0枚 | 1枚 | 2枚 | 3+ | **傾き** | 傾きの改善 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    double c3Slope0 = double.NaN;
    var c3SlopeV = new double[C3V];
    for (int v = 0; v < C3V; v++)
    {
        var rate = C3DRates(v, -1);
        double[] bm = C3BoxMeans(rate, c3DBox);
        double sl = C3Slope(rate, c3DBox);
        c3SlopeV[v] = sl;
        if (v == 0) c3Slope0 = sl;
        Console.WriteLine($"| {c3Name[v]} | {bm[0]:F2}% | {bm[1]:F2}% | {bm[2]:F2}% | {(double.IsNaN(bm[3]) ? "—" : $"{bm[3]:F2}%")} "
                          + $"| **{C3P1(sl)}** | {(v == 0 ? "—" : C3P2(sl - c3Slope0))} |");
    }
    Console.WriteLine();

    // ---- 理想台 ----
    Console.WriteLine("## 表D —— 理想台のヨミ3行（**拒否権**・波ごとの勝率）");
    Console.WriteLine();
    var c3IWin = new Dictionary<string, double[][]>();
    var c3ICnt = new Dictionary<string, double[][]>();
    foreach ((string rn, Formation f) in c3Rows)
    {
        var ws = new double[C3V][]; var cs = new double[C3V][];
        for (int v = 0; v < C3V; v++) { var g = C3Ideal(f, v); ws[v] = g.Win; cs[v] = g.Cnt; }
        c3IWin[rn] = ws; c3ICnt[rn] = cs;
    }
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | **帰属** | **薙ぎ化率** | 到達% | 初到達T | 情報セル |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach ((string rn, Formation _) in c3Rows)
    {
        double b0 = c3IWin[rn][0].Average();
        for (int v = 0; v < C3V; v++)
        {
            double[] w = c3IWin[rn][v]; double[] c = c3ICnt[rn][v];
            double n = Math.Max(1, c[0]);
            Console.WriteLine($"| {(v == 0 ? rn : "")} | {c3Name[v]} | " + string.Join(" | ", w.Select(x => $"{x:F1}%"))
                              + $" | {w.Average():F1}% | {(v == 0 ? "—" : C3P1(w.Average() - b0))} "
                              + $"| {(v == 0 ? "—" : $"{C3Rate(c, 0):F1}%")} "
                              + $"| {(v == 0 ? "—" : $"{100.0 * c[8] / n:F1}%")} "
                              + $"| {(v == 0 || c[8] == 0 ? "—" : $"{c[9] / c[8]:F2}")} | {C3Info(w)} |");
        }
    }
    Console.WriteLine();

    // ---- Q1 / Q6 ----
    Console.WriteLine("## Q1（**主判定**: 自己供給 対 外部供給）と Q6（外部供給の上積み）");
    Console.WriteLine();
    int C3Best(int fam)
    {
        int best = -1; double ba = double.NegativeInfinity;
        for (int v = 1; v < C3V; v++)
            if (C3Fam(v) == fam && c3DAttr[v] >= ba) { ba = c3DAttr[v]; best = v; }
        return best;
    }
    int c3BB = C3Best(1), c3BW = C3Best(2), c3BT = C3Best(3);
    Console.WriteLine("**最良 = その族でドラフト台の帰属が最大の版**（同点は高い閾値）。");
    Console.WriteLine();
    Console.WriteLine("| 量 | 版 | 値 |");
    Console.WriteLine("|---|---|--:|");
    Console.WriteLine($"| 自己供給の最良 | {c3Name[c3BB]} | {C3P2(c3DAttr[c3BB])} |");
    Console.WriteLine($"| 外部供給の最良 | {c3Name[c3BW]} | {C3P2(c3DAttr[c3BW])} |");
    Console.WriteLine($"| 合計の最良 | {c3Name[c3BT]} | {C3P2(c3DAttr[c3BT])} |");
    Console.WriteLine($"| **Q1 = 自己 − 外部** | {c3Name[c3BB]} − {c3Name[c3BW]} | **{C3P2(c3DAttr[c3BB] - c3DAttr[c3BW])}** |");
    Console.WriteLine($"| **Q6 = 合計 − 自己** | {c3Name[c3BT]} − {c3Name[c3BB]} | **{C3P2(c3DAttr[c3BT] - c3DAttr[c3BB])}** |");
    Console.WriteLine();
    Console.WriteLine("同じ閾値どうしの対（自己 9/18/30 対 合計 9/18/30）でも見る:");
    Console.WriteLine();
    Console.WriteLine("| 閾値 | 自己 | 合計 | 合計 − 自己 |");
    Console.WriteLine("|--:|--:|--:|--:|");
    for (int k = 0; k < 3; k++)
        Console.WriteLine($"| {UnitTally.CreakProbes[k]} | {C3P2(c3DAttr[1 + k])} | {C3P2(c3DAttr[7 + k])} "
                          + $"| **{C3P2(c3DAttr[7 + k] - c3DAttr[1 + k])}** |");
    Console.WriteLine();

    // ---- Q2 と採る版 ----
    Console.WriteLine("## Q2（条件は条件か）と、採る版");
    Console.WriteLine();
    Console.WriteLine("**Q2 = ドラフト台の薙ぎ化率が 10〜90%、かつ理想台のヨミ3行が3行とも 10〜90%**（測る前に固定）。");
    Console.WriteLine();
    Console.WriteLine("| 版 | ドラフト | 帯内 | 理想台3行 | 3行とも帯内 | **Q2** | ドラフトの帰属 |");
    Console.WriteLine("|---|--:|---|---|---|---|--:|");
    var c3Q2 = new bool[C3V];
    for (int v = 1; v < C3V; v++)
    {
        double dr = C3Rate(c3Agg, v * C3C);
        var ir = c3Rows.Select(b => C3Rate(c3ICnt[b.Name][v], 0)).ToArray();
        bool dok = dr >= 10.0 && dr <= 90.0;
        bool iok = ir.All(x => x >= 10.0 && x <= 90.0);
        c3Q2[v] = dok && iok;
        Console.WriteLine($"| {c3Name[v]} | {dr:F1}% | {(dok ? "○" : "×")} | {string.Join(" / ", ir.Select(x => $"{x:F1}%"))} "
                          + $"| {(iok ? "○" : "×")} | {(c3Q2[v] ? "**○**" : "×")} | {C3P2(c3DAttr[v])} |");
    }
    Console.WriteLine();
    int c3Pick = -1; double c3PickA = double.NegativeInfinity;
    for (int v = 1; v < C3V; v++)
        if (c3Q2[v] && c3DAttr[v] >= c3PickA) { c3PickA = c3DAttr[v]; c3Pick = v; }
    if (c3Pick < 0) Console.WriteLine("**Q2 を満たす版が1つも無い → 採らない。**");
    else Console.WriteLine($"→ 採る版の候補は **{c3Name[c3Pick]}**（帰属 {C3P2(c3DAttr[c3Pick])}）。");
    Console.WriteLine();

    // ---- Q3 / Q4 / Q5 ----
    Console.WriteLine("## Q3（帰属）・Q4（情報セル・**拒否権**）・Q5（歯止め）");
    Console.WriteLine();
    Console.WriteLine("| 版 | ドラフトの帰属 | 傾きの改善 | " + string.Join(" | ", c3Rows.Select(b => $"{b.Name}")) + " |");
    Console.WriteLine("|---|--:|--:|" + string.Concat(Enumerable.Repeat("--:|", c3Rows.Length)));
    for (int v = 1; v < C3V; v++)
        Console.WriteLine($"| {c3Name[v]} | {C3P2(c3DAttr[v])} | {C3P2(c3SlopeV[v] - c3Slope0)} | "
                          + string.Join(" | ", c3Rows.Select(b => C3P1(c3IWin[b.Name][v].Average() - c3IWin[b.Name][0].Average()))) + " |");
    Console.WriteLine();
    Console.WriteLine("（理想台の列は**帰属 = 版 − V0 の5波平均**。）");
    Console.WriteLine();
    Console.WriteLine("| 版 | " + string.Join(" | ", c3Rows.Select(b => b.Name)) + " | 情報セル計 | V0 比 | **Q4** |");
    Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", c3Rows.Length)) + "--:|--:|---|");
    int c3Info0 = c3Rows.Sum(b => C3Info(c3IWin[b.Name][0]));
    Console.WriteLine("| V0（無効） | " + string.Join(" | ", c3Rows.Select(b => C3Info(c3IWin[b.Name][0]).ToString()))
                      + $" | {c3Info0} | — | — |");
    var c3Q4 = new bool[C3V];
    for (int v = 1; v < C3V; v++)
    {
        int c3InfoV = c3Rows.Sum(b => C3Info(c3IWin[b.Name][v]));
        c3Q4[v] = c3Rows.All(b => C3Info(c3IWin[b.Name][v]) >= C3Info(c3IWin[b.Name][0]));
        Console.WriteLine($"| {c3Name[v]} | " + string.Join(" | ", c3Rows.Select(b => C3Info(c3IWin[b.Name][v]).ToString()))
                          + $" | {c3InfoV} | {c3InfoV - c3Info0:+0;-0;0} | {(c3Q4[v] ? "○" : "**×**")} |");
    }
    Console.WriteLine();
    Console.WriteLine("**Q4 は行ごとに「情報セルが減っていないこと」**（第66期はここで 96.5〜99.2% を 100.0% に押して落ちた）。");
    Console.WriteLine();

    var c3PrimIdx = Baseline.PrimaryRows.Select(n => Array.FindIndex(c3All, b => b.Name == n))
                                        .Where(i => i >= 0).ToArray();
    Console.WriteLine("| 版 | 主判定19行の第五波平均 | 歯止め 33.2% との余裕 | **Q5** |");
    Console.WriteLine("|---|--:|--:|---|");
    var c3Q5 = new bool[C3V];
    for (int v = 0; v < C3V; v++)
    {
        double avg = c3PrimIdx.Select(i => C3Ideal(c3All[i].Item2, v).Win[4]).Average();
        c3Q5[v] = avg >= Baseline.PrimaryFifthFloor;
        Console.WriteLine($"| {c3Name[v]} | {avg:F1}% | {C3P1(avg - Baseline.PrimaryFifthFloor)} | {(v == 0 ? "—" : c3Q5[v] ? "○" : "**×**")} |");
    }
    Console.WriteLine();

    // ---- 採否 ----
    Console.WriteLine("## 採否（Q2・Q3・Q4・Q5 のすべて）");
    Console.WriteLine();
    Console.WriteLine("| 版 | Q2 | Q3（\\|帰属\\| ≥ 1.5pt） | Q4 | Q5 | **採否** |");
    Console.WriteLine("|---|---|---|---|---|---|");
    for (int v = 1; v < C3V; v++)
    {
        bool q3 = Math.Abs(c3DAttr[v]) >= 1.5;
        bool ok = c3Q2[v] && q3 && c3Q4[v] && c3Q5[v];
        Console.WriteLine($"| {c3Name[v]} | {(c3Q2[v] ? "○" : "×")} | {(q3 ? "○" : "×")}（{C3P2(c3DAttr[v])}） "
                          + $"| {(c3Q4[v] ? "○" : "×")} | {(c3Q5[v] ? "○" : "×")} | {(ok ? "**○**" : "×")} |");
    }
    Console.WriteLine();
    Console.WriteLine("**Q3 の「両 seed 帯で符号一致」は A 帯 / B 帯を別々に回して突き合わせる**（この表は片帯ぶん）。");
    Console.WriteLine();
    Console.WriteLine($"所要 {c3Sw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}
}
