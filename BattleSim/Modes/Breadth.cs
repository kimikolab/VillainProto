using BattleCore;
using static Common;

// =====================================================================================
// breadth モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "breadth")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 breadth
// =====================================================================================

static class BreadthDiag
{
// 第83期 —— 物差しを引き直す（主判定＝相乗の広さ・単独は床へ）。**調査だけ。設計判断をしない。**
// **新機構ゼロ・駒ゼロ・差し替えの実行ゼロ・`TraitId` ゼロ・engine の変更ゼロ・`docs/` の差分ゼロ。**
//
// 第82期の物差しは**横軸が「単独の帰属」**だったので「1枚で働く駒」が上に来た。設計目標
// （**同じ軸で固めれば最良、というゲームにはしたくない**）と逆を向いているので、**線だけを引き直す**:
//
//     広さ(A)       = |{ B : 相乗(A,B) > 0 かつ |相乗| > 2×SE }|          ← 主判定の素
//     独立の広さ(A) = そのうち KeysOf(A) ∩ KeysOf(B) = ∅ である相手の数   ← **順位はこれで付ける**
//     残す     : 独立の広さ ≥ 3
//     転生     : 独立の広さ < 3 かつ 広さ ≥ 3
//     差し替え : 広さ < 3
//     床       : 群にかかわらず 単独 < −1.5 なら差し替えへ落とす
//
// **器具は第82期（＝第81期の 2×2）の写しで1文字も変えていない**ので、**測定データはそのまま使える**
// ——`checkup run` の吐いた TSV を読むだけ。**追加の戦闘は §2-5（埋め草の偏り）の1本だけ。**
//
//     dotnet run --project BattleSim -c Release 0 breadth phase0 [<checkup run の TSV>]  # 紙の計算（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 breadth run <skip> <take>  # **埋め草を無作為3枚にした版**の 2×2（§2-5・分割実行）
//     dotnet run --project BattleSim -c Release 0 breadth tables <checkup の TSV> [<pairs2 の TSV>] [<breadth run の TSV>]
//     dotnet run --project BattleSim -c Release 0 breadth check   # 受け入れ基準: `compare` 305 セルの突き合わせ
public static void Run(string[] args, int stageIndex)
{
    string bdArg = args.Length > 2 ? args[2] : "";
    var bdSw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> bdStages = EnemyCatalog.Stages;
    int bdW = bdStages.Count;
    var bdRoster = UnitCatalog.All.ToArray();
    int bdRN = bdRoster.Length;                       // 51
    int bdNK = UnitTally.CarryKeys.Length;            // 11

    // ---- 第81期 `pairs2` / 第82期 `checkup` の定数の写し（**1つも変えていない**）--------------------
    const int BdTableSeed = 8_100_000;
    const int BdK = 64;                               // 1組・1系列あたりの台数
    const int BdS = 2;                                // 独立系列の本数（自己検査 (g)）
    const int BdBand = 0, BdM = 8;                    // 戦闘 seed 0..7
    const int BdStrong = 7, BdWeakPct = 60, BdDrawCap = 20000;
    const int BdIdealSeeds = 200;

    // ---- この期に決めた線（**測る前に固定した。数字を見てから動かさない**。指示書 §2）----------------
    const int BdBreadth = 3;          // 広さ・独立の広さ の線（相方が引ける確率 ≒ 10% × 3 枚 = 27%）
    const double BdFloor = -1.5;      // 床（第62期以来の帰属の閾値の符号違い）
    const double BdBreak = 5.0;       // 拒否権1「既存61行を壊す」（第46期の閾値）
    const double BdCeil = 95.0;       // 天井（第82期の写し。**この期では群を作らない。表示だけ**）
    const int BdTop = 30;             // 上位・下位の行数
    // 第82期の3分の線（Q2 の突き合わせに要る。**第82期のコードの写し**）
    const double BdSoloLine82 = 1.5, BdSynLine82 = 5.0, BdCeilShare82 = 50.0;

    var bdIdx = new Dictionary<string, int>();
    for (int u = 0; u < bdRN; u++) bdIdx[bdRoster[u].Id] = u;
    string[] bdName = bdRoster.Select(d => d.Name).ToArray();

    // ---- 第78期の器具（入口 / 発火口 / キー）と第80期の分類 ------------------------------------------
    var bdKeyOf = bdRoster.Select(TraitKeyMap.KeysOf).ToArray();
    var bdHook = bdRoster.Select(TraitHookMap.HooksOf).ToArray();
    var bdEntry = bdRoster.Select(d => TraitEntryMap.EntriesOf(d, withFoe: false)).ToArray();
    var bdRead = bdRoster.Select(d => d.Traits.SelectMany(t => TraitEntryMap.Reads.TryGetValue(t, out var r) ? r : Array.Empty<(int Key, TraitEntryMap.Where W)>()).Distinct().ToArray()).ToArray();
    var bdSup = bdRoster.Select(d => d.Traits.SelectMany(t => TraitEntryMap.Supplies.TryGetValue(t, out var sq) ? sq : Array.Empty<(int Key, TraitEntryMap.Where W)>()).Distinct().ToArray()).ToArray();

    bool BdFeeds((int Key, TraitEntryMap.Where W) su, (int Key, TraitEntryMap.Where W) r)
    {
        if (su.Key != r.Key) return false;
        return r.W switch
        {
            TraitEntryMap.Where.Any => true,
            TraitEntryMap.Where.Self => su.W is TraitEntryMap.Where.Ally or TraitEntryMap.Where.Any,
            TraitEntryMap.Where.Ally => su.W is TraitEntryMap.Where.Self or TraitEntryMap.Where.Ally or TraitEntryMap.Where.Any,
            TraitEntryMap.Where.Foe => su.W is TraitEntryMap.Where.Foe or TraitEntryMap.Where.Any,
            _ => false,
        };
    }
    bool BdSupplies(int a, int b) => bdSup[a].Any(su => bdRead[b].Any(r => BdFeeds(su, r)));
    string[] bdClassName = { "供給→読み", "読み→読み", "供給→供給", "キーだけ共有", "共有無し" };
    int BdClass(int a, int b)
    {
        if (BdSupplies(a, b) || BdSupplies(b, a)) return 0;
        if (bdRead[a].Select(r => r.Key).Intersect(bdRead[b].Select(r => r.Key)).Any()) return 1;
        if (bdSup[a].Select(su => su.Key).Intersect(bdSup[b].Select(su => su.Key)).Any()) return 2;
        if (bdKeyOf[a].Intersect(bdKeyOf[b]).Any()) return 3;
        return 4;
    }
    // **「キーを共有しない」の定義（指示書 §1-2・測る前に固定）**: KeysOf の積集合が空。
    // **キーを1つも持たない駒どうしも「共有しない」に入る**（定義どおり。割合は phase0 が数える）。
    bool BdIndep(int a, int b) => !bdKeyOf[a].Intersect(bdKeyOf[b]).Any();

    // 拒否権3: **その駒が唯一の書き手であるキー**（`TraitEntryMap.Supplies`。**戦闘0回**）
    var bdSupCnt = new int[bdNK];
    for (int u = 0; u < bdRN; u++) foreach (int k in bdSup[u].Select(s => s.Key).Distinct()) bdSupCnt[k]++;
    int[] BdSoleWrite(int u) => bdSup[u].Select(s => s.Key).Distinct().Where(k => bdSupCnt[k] == 1).OrderBy(x => x).ToArray();
    // 表F: **唯一の読み手**（§2-3 の訂正。拒否権には使わない。**判断の材料として併記するだけ**）
    var bdReadCnt = new int[bdNK];
    for (int u = 0; u < bdRN; u++) foreach (int k in bdRead[u].Select(s => s.Key).Distinct()) bdReadCnt[k]++;
    int[] BdSoleRead(int u) => bdRead[u].Select(s => s.Key).Distinct().Where(k => bdReadCnt[k] == 1).OrderBy(x => x).ToArray();

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜82期と同一。`Stages` は書き換えない）------------------------
    var bdWeakCache = new Dictionary<string, UnitDef>();
    UnitDef BdWeakOf(UnitDef d)
    {
        if (bdWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * BdWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        bdWeakCache[d.Id] = w;
        return w;
    }
    var bdWeak = bdStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = BdWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef BdPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var bdPlainMap = bdRoster.ToDictionary(d => d.Id, BdPlain);

    // 埋め草3枚・**規則 P**（第81期 `PgFill` / 第82期 `HcFill` の写し）
    UnitDef[] BdFillP(UnitDef[] pool, int strong0, int seed)
    {
        int rn = pool.Length;
        var rng = new Random(seed);
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
            if (sel.Attack >= BdStrong) strong++;
            int px = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { px = t; break; }
            (idx[px], idx[remain - 1]) = (idx[remain - 1], idx[px]);
            remain--;
        }
        return picked;
    }
    // 埋め草3枚・**規則 R（無作為）**（§2-5。**規則 P は攻2〜3 を引かない**——第80期）
    UnitDef[] BdFillR(UnitDef[] pool, int seed)
    {
        int rn = pool.Length;
        var rng = new Random(seed);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        var picked = new UnitDef[3];
        for (int t = 0; t < 3; t++)
        {
            int j = t + rng.Next(rn - t);
            (idx[t], idx[j]) = (idx[j], idx[t]);
            picked[t] = pool[idx[t]];
        }
        return picked;
    }
    int BdSeed(int pairIx, int draw)
    {
        ulong x = (ulong)BdTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] BdSeats(UnitDef[] u)
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
    Formation BdForm(UnitDef[] u, int[] seats, int v)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (v & 1) != 0) || (k == 1 && (v & 2) != 0);
            f[seats[k]] = plain ? bdPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double BdRate(Formation f, int band)
    {
        double sum = 0;
        for (int wi = 1; wi < bdW; wi++)
        {
            int wins = 0;
            for (int seed = band; seed < band + BdM; seed++)
                if (BattleEngine.Run(f, bdWeak[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
            sum += wins * 100.0 / BdM;
        }
        return sum / (bdW - 1);
    }
    string BdP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double BdSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double BdMedian(IEnumerable<double> xs)
    {
        var a = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToArray();
        if (a.Length == 0) return double.NaN;
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2.0;
    }
    double BdCorr(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        int n = x.Count; if (n < 2) return double.NaN;
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < n; i++) { double a = x[i] - mx, b = y[i] - my; sxy += a * b; sxx += a * a; syy += b * b; }
        return sxx <= 0 || syy <= 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
    }

    var bdAllPairs = new List<(int A, int B)>();
    for (int a = 0; a < bdRN; a++) for (int b = a + 1; b < bdRN; b++) bdAllPairs.Add((a, b));
    int bdNP = bdAllPairs.Count;
    var bdPairIxOf = new int[bdRN, bdRN];
    for (int pi = 0; pi < bdNP; pi++) { bdPairIxOf[bdAllPairs[pi].A, bdAllPairs[pi].B] = pi; bdPairIxOf[bdAllPairs[pi].B, bdAllPairs[pi].A] = pi; }

    // ---- 理想台（`CompareBuilds()` の 61 行）------------------------------------------------------
    var bdAllRows = CompareBuilds();
    var bdPrimary = new HashSet<string>(Baseline.PrimaryRows);
    var bdRowUnits = bdAllRows.Select(r => r.F.Occupied()
                                            .Select(o => bdIdx.TryGetValue(o.Def.Id, out int u) ? u : -1)
                                            .Where(u => u >= 0).Distinct().ToArray()).ToArray();
    var bdInPrimary = new bool[bdRN];
    for (int i = 0; i < bdAllRows.Length; i++)
        if (bdPrimary.Contains(bdAllRows[i].Name)) foreach (int u in bdRowUnits[i]) bdInPrimary[u] = true;
    var bdRowCnt = new int[bdRN];
    for (int i = 0; i < bdAllRows.Length; i++) foreach (int u in bdRowUnits[i]) bdRowCnt[u]++;

    // 理想台の帰属（第82期の写し）＋**行ごとの落ち幅の最大**（この期の拒否権1）
    (double[] Attr, double[] MaxDrop, int[] WorstRow, long Battles) BdIdeal()
    {
        double IdealRate(Formation f)
        {
            double sum = 0;
            for (int wi = 1; wi < bdW; wi++)
            {
                int wins = 0;
                for (int seed = 0; seed < BdIdealSeeds; seed++)
                    if (BattleEngine.Run(f, bdStages[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
                sum += wins * 100.0 / BdIdealSeeds;
            }
            return sum / (bdW - 1);
        }
        var jobs = new List<(int Row, int Slot, int U)>();
        for (int i = 0; i < bdAllRows.Length; i++)
            foreach ((int sl, UnitDef d) in bdAllRows[i].F.Occupied())
                if (bdIdx.TryGetValue(d.Id, out int u)) jobs.Add((i, sl, u));
        var full = new double[bdAllRows.Length];
        Parallel.For(0, bdAllRows.Length, i => full[i] = IdealRate(bdAllRows[i].F));
        var got = new double[jobs.Count];
        Parallel.For(0, jobs.Count, j =>
        {
            var f = new Formation();
            foreach ((int sl, UnitDef d) in bdAllRows[jobs[j].Row].F.Occupied())
                f[sl] = sl == jobs[j].Slot ? bdPlainMap[d.Id] : d;
            got[j] = IdealRate(f);
        });
        var attrSum = new double[bdRN]; var cnt = new int[bdRN];
        var maxDrop = new double[bdRN]; var worst = new int[bdRN];
        for (int u = 0; u < bdRN; u++) { maxDrop[u] = double.NaN; worst[u] = -1; }
        for (int j = 0; j < jobs.Count; j++)
        {
            int u = jobs[j].U;
            double drop = full[jobs[j].Row] - got[j];
            attrSum[u] += drop; cnt[u]++;
            if (double.IsNaN(maxDrop[u]) || drop > maxDrop[u]) { maxDrop[u] = drop; worst[u] = jobs[j].Row; }
        }
        var attr = new double[bdRN];
        for (int u = 0; u < bdRN; u++) attr[u] = cnt[u] == 0 ? double.NaN : attrSum[u] / cnt[u];
        return (attr, maxDrop, worst, (long)(bdAllRows.Length + jobs.Count) * (bdW - 1) * BdIdealSeeds);
    }

    // ---- 1組ぶんの 2×2（**第82期 `HcMeasure` の写し**。埋め草の規則だけ差し替えられる）-------------
    (double[][] Y, int NT) BdMeasure(int pi, bool randomFill)
    {
        (int a, int b) = bdAllPairs[pi];
        var pool = bdRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (bdRoster[a].Attack >= BdStrong ? 1 : 0) + (bdRoster[b].Attack >= BdStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < BdS * BdK && draw < BdDrawCap; draw++)
        {
            int sd = BdSeed(bdPairIxOf[a, b], draw);
            var f = randomFill ? BdFillR(pool, sd) : BdFillP(pool, strong0, sd);
            var t = f.Select(d => bdIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = new[] { bdRoster[a], bdRoster[b], fills[t][0], fills[t][1], fills[t][2] };
            int[] seats = BdSeats(team);
            var y = new double[4];
            for (int v = 0; v < 4; v++) y[v] = BdRate(BdForm(team, seats, v), BdBand);
            ys[t] = y;
        }
        return (ys, fills.Count);
    }

    // =====================================================================================
    // check: `compare` 305 セルの突き合わせ（Q7）
    // =====================================================================================
    if (bdArg == "check")
    {
        var bdDoc = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines("docs/balance.md"))
        {
            if (!line.StartsWith("| ")) continue;
            var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
            if (cells.Length != bdW + 1 || !cells[1].EndsWith("%")) continue;
            var v = new double[bdW];
            bool ok = true;
            for (int w = 0; w < bdW; w++)
                if (!double.TryParse(cells[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
            if (ok) bdDoc[cells[0]] = v;
        }
        int mism = 0, cellsN = 0, missing = 0;
        var bad = new List<string>();
        Parallel.For(0, bdAllRows.Length, i =>
        {
            var v = new double[bdW];
            for (int w = 0; w < bdW; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < BdIdealSeeds; seed++)
                    if (BattleEngine.Run(bdAllRows[i].F, bdStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                v[w] = wins * 100.0 / BdIdealSeeds;
            }
            lock (bad)
            {
                if (!bdDoc.TryGetValue(bdAllRows[i].Name, out double[]? doc)) { missing++; return; }
                for (int w = 0; w < bdW; w++)
                {
                    cellsN++;
                    if (Math.Abs(doc[w] - v[w]) > 0.05) { mism++; bad.Add($"| {bdAllRows[i].Name} | 第{w + 1}波 | {doc[w]:F1} | {v[w]:F1} |"); }
                }
            }
        });
        Console.WriteLine("# 第83期 —— 受け入れ基準（Q7）: `compare` 305 セルの突き合わせ");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` {bdAllRows.Length} 行 × {bdW} 波 × seed 0..{BdIdealSeeds - 1} を回し直して `docs/balance.md` と突き合わせた: "
                          + $"**{cellsN} セル・ずれ {mism} 件**（`docs/balance.md` に無い行 {missing}）。");
        foreach (string l in bad) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"`UnitCatalog.All` は {UnitCatalog.All.Count} 体。この期は engine も `Traits.cs` も駒も波も触っていないので、ずれは 0 件でなければならない。");
        Console.WriteLine();
        Console.WriteLine($"所要 {bdSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // run: **埋め草を無作為3枚にした版**（§2-5）の 2×2 を TSV で吐く。列は `checkup run` と同じ形式
    // =====================================================================================
    if (bdArg == "run")
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int skip = args.Length > 3 ? int.Parse(args[3]) : 0;
        int take = args.Length > 4 ? int.Parse(args[4]) : bdNP;
        skip = Math.Clamp(skip, 0, bdNP);
        take = Math.Clamp(take, 0, bdNP - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"breadth run {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            var (ys, nt) = BdMeasure(skip + j, randomFill: true);
            var vsum = new double[4];
            int f00 = 0, f11 = 0;
            for (int t = 0; t < nt; t++)
            {
                for (int v = 0; v < 4; v++) vsum[v] += ys[t][v];
                if (ys[t][3] <= 0.0 || ys[t][3] >= 100.0) f00++;
                if (ys[t][0] <= 0.0 || ys[t][0] >= 100.0) f11++;
            }
            var sb = new System.Text.StringBuilder();
            sb.Append(bdAllPairs[skip + j].A).Append('\t').Append(bdAllPairs[skip + j].B).Append('\t')
              .Append(nt).Append('\t').Append(f00).Append('\t').Append(f11);
            for (int v = 0; v < 4; v++) sb.Append('\t').Append((nt == 0 ? double.NaN : vsum[v] / nt).ToString("R", inv));
            for (int sx = 0; sx < BdS; sx++)
                for (int t = sx; t < nt; t += BdS)
                    sb.Append('\t').Append((ys[t][0] - ys[t][2] - ys[t][1] + ys[t][3]).ToString("R", inv));
            for (int t = 0; t < nt; t++)
                for (int v = 0; v < 4; v++) sb.Append('\t').Append(ys[t][v].ToString("R", inv));
            rows[j] = sb.ToString();
            if (Interlocked.Increment(ref doneR) % 25 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {bdSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    // ---- TSV の読み込み（`checkup run` / `breadth run` の共通形式）---------------------------------
    (double[][][] Y, int[] NT, int Got) BdLoad(string path)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var y = new double[bdNP][][];
        var nts = new int[bdNP];
        var seen = new bool[bdNP];
        int got = 0;
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int pi = bdPairIxOf[int.Parse(c[0]), int.Parse(c[1])];
            if (seen[pi]) continue;
            seen[pi] = true;
            int nt = int.Parse(c[2]);
            nts[pi] = nt;
            int at = 9 + nt;
            var ys = new double[nt][];
            for (int t = 0; t < nt; t++)
            {
                ys[t] = new double[4];
                for (int v = 0; v < 4; v++) ys[t][v] = double.Parse(c[at++], inv);
            }
            y[pi] = ys;
            got++;
        }
        return (y, nts, got);
    }

    // 組ごとの相乗（合算と系列）／駒ごとの単独・広さ・独立の広さ・3分
    (double[] Syn, double[] Se, double[][] SynS, double[][] SeS, double[][] Serial) BdSyn(double[][][] y, int[] nts)
    {
        var syn = new double[bdNP]; var se = new double[bdNP];
        var synS = new double[bdNP][]; var seS = new double[bdNP][]; var ser = new double[bdNP][];
        for (int pi = 0; pi < bdNP; pi++)
        {
            int nt = nts[pi];
            var all = new double[nt];
            for (int t = 0; t < nt; t++) all[t] = y[pi][t][0] - y[pi][t][2] - y[pi][t][1] + y[pi][t][3];
            syn[pi] = all.Average();
            se[pi] = BdSd(all) / Math.Sqrt(nt);
            synS[pi] = new double[BdS]; seS[pi] = new double[BdS];
            var s = new List<double>();
            for (int sx = 0; sx < BdS; sx++)
            {
                var xs = new List<double>();
                for (int t = sx; t < nt; t += BdS) xs.Add(all[t]);
                synS[pi][sx] = xs.Average();
                seS[pi][sx] = BdSd(xs) / Math.Sqrt(xs.Count);
                s.AddRange(xs);
            }
            ser[pi] = s.ToArray();
        }
        return (syn, se, synS, seS, ser);
    }

    // =====================================================================================
    // phase0: 紙の計算（**戦闘0回**）。TSV を渡すと第82期の測定値から分布だけを出す（**戦闘は回さない**）
    // =====================================================================================
    if (bdArg == "phase0")
    {
        string p0 = args.Length > 3 ? args[3] : "";
        Console.WriteLine("# 第83期 Phase 0 —— 物差しを引き直す前に固定する（紙の計算）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `dotnet run --project BattleSim -c Release 0 breadth phase0 [<checkup run の TSV>]`");
        Console.WriteLine();
        Console.WriteLine("## 1-1. 器具（**第82期の写し。1文字も変えていない**）");
        Console.WriteLine();
        Console.WriteLine("    y11 = 両方が本物 / y01 = A 素体・B 本物 / y10 = A 本物・B 素体 / y00 = 両方が素体");
        Console.WriteLine("    単独(A)   = y11 − y01               ← 第69期の標準器具（**この期では床にしか使わない**）");
        Console.WriteLine("    相乗(A,B) = y11 − y10 − y01 + y00   ← 第81期の 2×2（**この期の主判定の素**）");
        Console.WriteLine();
        Console.WriteLine($"台: 埋め草3枚（残り {bdRN - 2} 体・規則 P）＋ A ＋ B・席は規則配置 H・弱い波 {BdWeakPct}%・"
                          + $"第2〜{bdW}波・戦闘 seed {BdBand}..{BdBand + BdM - 1}・**全 {bdNP:N0} 組 × {BdS * BdK} 台**。"
                          + "**測定データは第82期のものをそのまま使う**（`checkup run` の TSV を読むだけ）。");
        Console.WriteLine();
        Console.WriteLine("## 1-2. 「キーを共有しない」の定義と、その組数（**指示書 §1-2・自己検査 (a)**）");
        Console.WriteLine();
        Console.WriteLine("定義: **`TraitKeyMap.KeysOf(A)` と `KeysOf(B)` の積集合が空**。"
                          + "**キーを1つも持たない駒どうしも「共有しない」に入る**（定義どおり）。");
        Console.WriteLine();
        int nIndep = 0, nBothZero = 0, nOneZero = 0;
        var clsCnt = new int[5];
        var clsIndep = new int[5];
        for (int pi = 0; pi < bdNP; pi++)
        {
            (int a, int b) = bdAllPairs[pi];
            int cl = BdClass(a, b);
            clsCnt[cl]++;
            if (BdIndep(a, b))
            {
                nIndep++; clsIndep[cl]++;
                if (bdKeyOf[a].Length == 0 && bdKeyOf[b].Length == 0) nBothZero++;
                else if (bdKeyOf[a].Length == 0 || bdKeyOf[b].Length == 0) nOneZero++;
            }
        }
        int nKeyZero = Enumerable.Range(0, bdRN).Count(u => bdKeyOf[u].Length == 0);
        Console.WriteLine("| 量 | 組数 | 割合 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 全 1,275 組 | {bdNP} | 100.0% |");
        Console.WriteLine($"| **キーを共有しない** | **{nIndep}** | **{100.0 * nIndep / bdNP:F1}%** |");
        Console.WriteLine($"| うち **両方がキー 0** | {nBothZero} | {100.0 * nBothZero / bdNP:F1}%（共有しない組の {100.0 * nBothZero / nIndep:F1}%） |");
        Console.WriteLine($"| うち **片方がキー 0** | {nOneZero} | {100.0 * nOneZero / bdNP:F1}%（同 {100.0 * nOneZero / nIndep:F1}%） |");
        Console.WriteLine($"| うち **両方がキーを持ち、それでも重ならない** | {nIndep - nBothZero - nOneZero} | {100.0 * (nIndep - nBothZero - nOneZero) / bdNP:F1}%（同 {100.0 * (nIndep - nBothZero - nOneZero) / nIndep:F1}%） |");
        Console.WriteLine();
        Console.WriteLine($"**キーを1つも持たない駒は {nKeyZero} / {bdRN} 体**"
                          + $"（{string.Join("・", Enumerable.Range(0, bdRN).Where(u => bdKeyOf[u].Length == 0).Select(u => bdName[u]))}）。");
        Console.WriteLine();
        Console.WriteLine("第80期の分類との対応（**「共有無し」は分類の第5類とちょうど一致するわけではない**"
                          + "——分類は読み／供給の向きを先に見るので、キーが重なる組でも第4類に落ちる）:");
        Console.WriteLine();
        Console.WriteLine("| 分類 | 組数 | うちキーを共有しない |");
        Console.WriteLine("|---|--:|--:|");
        for (int c = 0; c < 5; c++) Console.WriteLine($"| {bdClassName[c]} | {clsCnt[c]} | {clsIndep[c]} |");
        Console.WriteLine();
        Console.WriteLine("## 1-3. 床の線（指示書 §1-3 / §2-2）");
        Console.WriteLine();
        Console.WriteLine($"**床は −1.5pt**（第62期以来の帰属の閾値の符号違い。**新しい数字を作らない**）。");
        Console.WriteLine();
        if (p0 != "" && File.Exists(p0))
        {
            var (y0, nt0, got0) = BdLoad(p0);
            if (got0 != bdNP) Console.WriteLine($"**TSV が足りない（{got0} / {bdNP} 組）。分布は出せない。**");
            else
            {
                var solo0 = new double[bdRN];
                var cnt0 = new int[bdRN];
                for (int pi = 0; pi < bdNP; pi++)
                {
                    (int a, int b) = bdAllPairs[pi];
                    for (int t = 0; t < nt0[pi]; t++)
                    {
                        solo0[a] += y0[pi][t][0] - y0[pi][t][1]; cnt0[a]++;
                        solo0[b] += y0[pi][t][0] - y0[pi][t][2]; cnt0[b]++;
                    }
                }
                for (int u = 0; u < bdRN; u++) solo0[u] /= cnt0[u];
                int below = Enumerable.Range(0, bdRN).Count(u => solo0[u] < BdFloor);
                Console.WriteLine($"**第82期の測定値（この TSV）で単独の帰属が −1.5 を下回る駒は {below} / {bdRN} 体**: "
                                  + string.Join("・", Enumerable.Range(0, bdRN).Where(u => solo0[u] < BdFloor)
                                        .OrderBy(u => solo0[u]).Select(u => $"{bdName[u]} {BdP2(solo0[u])}")) + "。");
                Console.WriteLine();
                Console.WriteLine("| 帯 | < -10 | -10 〜 -5 | -5 〜 -1.5 | -1.5 〜 0 | 0 〜 +1.5 | +1.5 〜 +5 | +5 以上 |");
                Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
                double[] edges = { -10, -5, -1.5, 0, 1.5, 5 };
                var hist = new int[7];
                foreach (int u in Enumerable.Range(0, bdRN))
                {
                    int i = 0; while (i < edges.Length && solo0[u] >= edges[i]) i++;
                    hist[i]++;
                }
                Console.WriteLine("| 体数 | " + string.Join(" | ", hist) + " |");
            }
        }
        else Console.WriteLine("（TSV が渡されていないので分布は出していない。第82期 表A の値がそのまま使える。）");
        Console.WriteLine();
        Console.WriteLine("## 1-4. 拒否権（指示書 §2-3。**紙で出る2本 ＋ 表F**）");
        Console.WriteLine();
        int nPrim = Enumerable.Range(0, bdRN).Count(u => bdInPrimary[u]);
        var soleW = Enumerable.Range(0, bdRN).Where(u => BdSoleWrite(u).Length > 0).ToArray();
        var soleR = Enumerable.Range(0, bdRN).Where(u => BdSoleRead(u).Length > 0).ToArray();
        Console.WriteLine($"1. **既存 61 行を壊す**: その駒を素体に落とすと**第2〜{bdW}波の平均が {BdBreak:F1}pt 以上落ちる行が1行でもある**"
                          + "（**第82期の拒否権1「理想台では働いている」を廃止して置き換えたもの**。行ごとの落ち幅は理想台の測定と同じ 305 か所から出る）");
        Console.WriteLine($"2. **主判定 {Baseline.PrimaryRows.Length} 行に在席**: {nPrim} / {bdRN} 体（**分母の {100.0 * nPrim / bdRN:F0}%**・自己検査 (a)）");
        Console.WriteLine($"3. **唯一の書き手であるキーがある**: {soleW.Length} / {bdRN} 体");
        Console.WriteLine();
        Console.WriteLine("| キー | " + string.Join(" | ", UnitTally.CarryKeys) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", bdNK)));
        Console.WriteLine("| 書き手 | " + string.Join(" | ", bdSupCnt) + " |");
        Console.WriteLine("| 読み手 | " + string.Join(" | ", bdReadCnt) + " |");
        Console.WriteLine();
        Console.WriteLine($"**唯一の読み手であるキーを持つ駒は {soleR.Length} 体**（§2-3 の訂正。**拒否権には使わない**）: "
                          + string.Join("・", soleR.Select(u => $"{bdName[u]}（{string.Join("・", BdSoleRead(u).Select(k => UnitTally.CarryKeys[k]))}）")) + "。");
        Console.WriteLine();
        Console.WriteLine("## 1-5. 軸ゲーの監視指標（指示書 §2-4。**この期から毎期出す**）");
        Console.WriteLine();
        Console.WriteLine("    同キーの組（KeysOf の積集合が空でない）の相乗の分布  対  共有無しの組の相乗の分布");
        Console.WriteLine("      平均 / 中央値 / 上位 30 に占める割合 / 有意な正の割合");
        Console.WriteLine();
        Console.WriteLine($"**分母は 同キー {bdNP - nIndep} 組 対 共有無し {nIndep} 組**。"
                          + $"一様なら上位 30 のうち共有無しは **{30.0 * nIndep / bdNP:F1} 組**が期待値で、"
                          + "**Q5 の線（5 組以上）はこの期待値より少し下**——**「独占されていない」ことだけを問う線**である。");
        Console.WriteLine();
        Console.WriteLine("## 1-6. 埋め草の偏り（指示書 §2-5）と予算");
        Console.WriteLine();
        long bBudget = (long)bdNP * 4 * BdK * BdS * (bdW - 1) * BdM;
        Console.WriteLine($"**規則 P（攻撃力 → HP の2段）は攻2〜3 を引かない**（第80期）ので、"
                          + $"**埋め草を「残り {bdRN - 2} 体から無作為3枚」に変えた版**（規則 R）で同じ表を作り直す。");
        Console.WriteLine();
        Console.WriteLine($"- 予算: {bdNP:N0} 組 × 4 版 × {BdS * BdK} 台 × {bdW - 1} 波 × seed {BdM} 本 = **{bBudget:N0} 戦**"
                          + "（**この期の追加の戦闘はこれだけ**。第82期の 2×2 と同額）");
        Console.WriteLine("- 台の抽選 seed（`BdSeed`）・席の規則 H・戦闘 seed・4 版の作り方は**すべて規則 P の版と同一**。"
                          + "**変えたのは3枚の選び方の1点だけ**");
        Console.WriteLine($"- 理想台（拒否権1 と 表A の参考列）: ({bdAllRows.Length} 行 + 在席枠) × {bdW - 1} 波 × seed {BdIdealSeeds} 本"
                          + "——**第82期と同じ計算**なので新しい戦闘ではない（同じ値が出ることが検算）");
        Console.WriteLine();
        Console.WriteLine("## 1-7. `docs/balance.md` の現在値（**この期は1バイトも動かさない**）");
        Console.WriteLine();
        {
            int rowsN = 0;
            var waveSum = new double[bdW];
            foreach (string line in File.ReadAllLines("docs/balance.md"))
            {
                if (!line.StartsWith("| ")) continue;
                var cs = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
                if (cs.Length != bdW + 1 || !cs[1].EndsWith("%")) continue;
                bool ok = true;
                var v = new double[bdW];
                for (int w = 0; w < bdW; w++) if (!double.TryParse(cs[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
                if (!ok) continue;
                rowsN++;
                for (int w = 0; w < bdW; w++) waveSum[w] += v[w];
            }
            Console.WriteLine($"**{rowsN} 行 × {bdW} 波 = {rowsN * bdW} セル。** 波ごとの平均: "
                              + string.Join(" / ", waveSum.Select(s => (s / Math.Max(1, rowsN)).ToString("F1"))) + "。");
        }
        Console.WriteLine();
        Console.WriteLine("## 予測（**測る前に書く**・指示書 §1）");
        Console.WriteLine();
        Console.WriteLine("| # | 量 | 予測 |");
        Console.WriteLine("|--:|---|---|");
        Console.WriteLine("| P1 | 群の入れ替わり | **15 体以上**が第82期の3分から別の群へ移る |");
        Console.WriteLine("| P2 | 上がる駒 | **`Supplies` を持つ書き手**（グザ・ボルグ・ゾト・ヒサ・クグ・シオ・トウ・ヒビ・ネル） |");
        Console.WriteLine("| P3 | 下がる駒 | **`Supplies` を持たない純粋な読み手**（第82期の差し替え候補6体のうち4体） |");
        Console.WriteLine("| P4 | ササ | **床では止まらないが、広さで苦しい** |");
        Console.WriteLine("| P5 | 切れる駒 | **1〜5 体** |");
        Console.WriteLine("| P6 | 軸ゲーの指標 | **共有無しの組の相乗の平均は同キーより小さいが、上位には食い込む** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {bdSw.Elapsed.TotalSeconds:F1} 秒（**戦闘 0 回**）。");
        return;
    }

    if (bdArg != "tables")
    {
        Console.WriteLine("breadth: 引数は phase0 [<TSV>] / run <skip> <take> / tables <checkup の TSV> [<pairs2 の TSV>] [<breadth run の TSV>] / check。");
        return;
    }

    // =====================================================================================
    // tables: 表A〜G・Q1〜Q7
    // =====================================================================================
    string bdPath = args.Length > 3 ? args[3] : "";
    string bdRefPath = args.Length > 4 ? args[4] : "";
    string bdFillPath = args.Length > 5 ? args[5] : "";
    if (bdPath == "" || !File.Exists(bdPath))
    {
        Console.WriteLine("breadth tables <`checkup run` の吐いた TSV を連結したファイル> [<`pairs2 run` の TSV>] [<`breadth run` の TSV>]");
        return;
    }
    var bdInv = System.Globalization.CultureInfo.InvariantCulture;

    var (bdY, bdNT, bdGot) = BdLoad(bdPath);
    if (bdGot != bdNP)
    {
        Console.WriteLine($"**シャードが足りない: {bdGot} / {bdNP} 組。`checkup run` の全区間を連結してから `tables` に渡すこと。**");
        return;
    }
    var (bdSynAll, bdSeAll, bdSynS, bdSeS, bdSerial) = BdSyn(bdY, bdNT);

    // ---- 駒ごとの単独（`y11 − y01`）と天井・床 ----------------------------------------------------
    var bdSoloL = new List<double>[bdRN][];
    for (int u = 0; u < bdRN; u++) { bdSoloL[u] = new List<double>[BdS]; for (int sx = 0; sx < BdS; sx++) bdSoloL[u][sx] = new List<double>(); }
    var bdCeilHit = new int[bdRN]; var bdCeilAll = new int[bdRN]; var bdFloorHit = new int[bdRN];
    for (int pi = 0; pi < bdNP; pi++)
    {
        (int a, int b) = bdAllPairs[pi];
        for (int t = 0; t < bdNT[pi]; t++)
        {
            double[] y = bdY[pi][t];
            int sx = t % BdS;
            bdSoloL[a][sx].Add(y[0] - y[1]);
            bdSoloL[b][sx].Add(y[0] - y[2]);
            if (y[3] > BdCeil) { bdCeilHit[a]++; bdCeilHit[b]++; }
            if (y[3] <= 0.0) { bdFloorHit[a]++; bdFloorHit[b]++; }
            bdCeilAll[a]++; bdCeilAll[b]++;
        }
    }
    var bdSolo = new double[bdRN];
    var bdSoloSe = new double[bdRN];
    var bdSoloSv = new double[bdRN][];
    var bdCeilPct = new double[bdRN];
    var bdFloorPct = new double[bdRN];
    for (int u = 0; u < bdRN; u++)
    {
        var all = bdSoloL[u][0].Concat(bdSoloL[u][1]).ToArray();
        bdSolo[u] = all.Average();
        bdSoloSe[u] = BdSd(all) / Math.Sqrt(all.Length);
        bdSoloSv[u] = new[] { bdSoloL[u][0].Average(), bdSoloL[u][1].Average() };
        bdCeilPct[u] = 100.0 * bdCeilHit[u] / bdCeilAll[u];
        bdFloorPct[u] = 100.0 * bdFloorHit[u] / bdCeilAll[u];
    }

    // ---- 広さ・独立の広さ・最良の相乗・3分（**§2-1 / §2-2 の線をそのまま**）------------------------
    string[] bdGroupName = { "残す", "転生", "差し替え" };
    (int[] Br, int[] IBr, int[] Neg, double[] Best, int[] BestIx, int[] Grp) BdAgg(Func<int, bool> pos, Func<int, bool> neg, Func<int, double> syn, double[] solo)
    {
        var br = new int[bdRN]; var ibr = new int[bdRN]; var ng = new int[bdRN];
        var best = new double[bdRN]; var bix = new int[bdRN]; var grp = new int[bdRN];
        for (int u = 0; u < bdRN; u++)
        {
            best[u] = double.NaN; bix[u] = -1;
            for (int v = 0; v < bdRN; v++)
            {
                if (v == u) continue;
                int pi = bdPairIxOf[u, v];
                if (pos(pi))
                {
                    br[u]++;
                    if (BdIndep(u, v)) ibr[u]++;
                    if (double.IsNaN(best[u]) || syn(pi) > best[u]) { best[u] = syn(pi); bix[u] = v; }
                }
                if (neg(pi)) ng[u]++;
            }
            grp[u] = solo[u] < BdFloor ? 2 : ibr[u] >= BdBreadth ? 0 : br[u] >= BdBreadth ? 1 : 2;
        }
        return (br, ibr, ng, best, bix, grp);
    }
    bool BdPosP(int pi) => bdSynAll[pi] > 2 * bdSeAll[pi];
    bool BdNegP(int pi) => bdSynAll[pi] < -2 * bdSeAll[pi];
    var (bdBr, bdIBr, bdNeg, bdBest, bdBestIx, bdGrp) = BdAgg(BdPosP, BdNegP, pi => bdSynAll[pi], bdSolo);

    // 系列ごと（Q3）
    var bdGrpS = new int[bdRN][];
    var bdBrS = new int[bdRN][];
    var bdIBrS = new int[bdRN][];
    var bdBestS = new double[bdRN][];
    for (int u = 0; u < bdRN; u++) { bdGrpS[u] = new int[BdS]; bdBrS[u] = new int[BdS]; bdIBrS[u] = new int[BdS]; bdBestS[u] = new double[BdS]; }
    for (int sx = 0; sx < BdS; sx++)
    {
        int si = sx;
        var soloS = Enumerable.Range(0, bdRN).Select(u => bdSoloSv[u][si]).ToArray();
        var (br, ibr, _, best, _, grp) = BdAgg(pi => bdSynS[pi][si] > 2 * bdSeS[pi][si], pi => bdSynS[pi][si] < -2 * bdSeS[pi][si], pi => bdSynS[pi][si], soloS);
        for (int u = 0; u < bdRN; u++) { bdGrpS[u][si] = grp[u]; bdBrS[u][si] = br[u]; bdIBrS[u][si] = ibr[u]; bdBestS[u][si] = best[u]; }
    }

    // ---- 第82期の3分（Q2 の突き合わせ。**第82期のコードの写し**）----------------------------------
    string[] bd82Name = { "残す", "転生", "差し替え", "別扱い" };
    var bd82 = new int[bdRN];
    for (int u = 0; u < bdRN; u++)
    {
        bool special = bdCeilPct[u] > BdCeilShare82 || bdSoloSv[u][0] * bdSoloSv[u][1] < 0;
        int g = bdSolo[u] >= BdSoloLine82 ? 0 : (!double.IsNaN(bdBest[u]) && bdBest[u] >= BdSynLine82 ? 1 : 2);
        bd82[u] = special ? 3 : g;
    }

    // ---- 理想台（拒否権1・参考列）------------------------------------------------------------------
    Console.Error.Write("理想台の帰属と行ごとの落ち幅: ");
    var (bdIdealAttr, bdMaxDrop, bdWorstRow, bdIdealB) = BdIdeal();
    Console.Error.WriteLine("done");

    // ---- 拒否権（§2-3。**1 を差し替えた**）---------------------------------------------------------
    var bdVeto1 = Enumerable.Range(0, bdRN).Select(u => !double.IsNaN(bdMaxDrop[u]) && bdMaxDrop[u] >= BdBreak).ToArray();
    var bdVeto2 = bdInPrimary;
    var bdVeto3 = Enumerable.Range(0, bdRN).Select(u => BdSoleWrite(u).Length > 0).ToArray();
    bool BdVetoed(int u) => bdVeto1[u] || bdVeto2[u] || bdVeto3[u];
    string BdVetoOf(int u)
    {
        var v = new List<string>();
        if (bdVeto1[u]) v.Add("1壊す");
        if (bdVeto2[u]) v.Add("2主判定");
        if (bdVeto3[u]) v.Add("3唯一");
        return v.Count == 0 ? "—" : string.Join("/", v);
    }
    // 第82期の拒否権1（廃止したもの）: 理想台とドラフト台で「残す」が割れる
    var bd82Veto1 = Enumerable.Range(0, bdRN)
        .Select(u => (!double.IsNaN(bdIdealAttr[u]) && bdIdealAttr[u] >= BdSoloLine82) != (bdSolo[u] >= BdSoloLine82)).ToArray();

    long bdBattles = (long)bdNP * 4 * BdK * BdS * (bdW - 1) * BdM + bdIdealB;

    // =====================================================================================
    Console.WriteLine("# 第83期 —— 物差しを引き直す（主判定＝相乗の広さ・単独は床へ）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 breadth tables <TSV>` の出力。**`docs/` には置かない。**");
    Console.WriteLine();
    Console.WriteLine("**器具は第82期（＝第81期の 2×2）の写しで1文字も変えていない。測定データもそのまま。**"
                      + "**変えたのは線だけ**（指示書 §2）:");
    Console.WriteLine();
    Console.WriteLine("    広さ(A)       = |{ B : 相乗(A,B) > 2×SE }|                        ← 有意な正の相手の数");
    Console.WriteLine("    独立の広さ(A) = そのうち KeysOf(A) ∩ KeysOf(B) = ∅ の相手の数     ← **順位はこれで付ける**");
    Console.WriteLine($"    残す = 独立の広さ ≥ {BdBreadth} ／ 転生 = 広さ ≥ {BdBreadth} ／ 差し替え = それ以外 ／ 床 = 単独 < {BdFloor:F1} なら差し替えへ");
    Console.WriteLine();
    Console.WriteLine($"台: 埋め草3枚（残り {bdRN - 2} 体・規則 P）＋ A ＋ B・席は規則配置 H・弱い波 {BdWeakPct}%・第2〜{bdW}波・"
                      + $"戦闘 seed {BdBand}..{BdBand + BdM - 1}。**全 {bdNP:N0} 組 × {BdS * BdK} 台**（系列 {BdS} × K {BdK}）。"
                      + $"理想台は `CompareBuilds()` {bdAllRows.Length} 行 × seed 0..{BdIdealSeeds - 1}。**合計 {bdBattles:N0} 戦**"
                      + "（**第82期と同じ台・同じ数**——この期は再現の確認のために回し直しただけ）。");
    Console.WriteLine();

    // ---- Q1: 器具の再現 --------------------------------------------------------------------------
    Console.WriteLine("## Q1 —— 器具の再現（第82期のデータがそのまま使えるか）");
    Console.WriteLine();
    int q1Top = -1, q1Bot = -1;
    double q1MaxDiff = double.NaN, q1R = double.NaN;
    if (bdRefPath != "" && File.Exists(bdRefPath))
    {
        var refSyn = new double[bdNP];
        var refSeen = new bool[bdNP];
        double maxDiff = 0;
        int refGot = 0, refCells = 0;
        foreach (string line in File.ReadLines(bdRefPath))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int pi = bdPairIxOf[int.Parse(c[0]), int.Parse(c[1])];
            if (refSeen[pi]) continue;
            refSeen[pi] = true;
            int nt = int.Parse(c[2]);
            double sum = 0;
            for (int t = 0; t < nt; t++)
            {
                double x = double.Parse(c[9 + t], bdInv);
                sum += x;
                if (t < bdSerial[pi].Length) { maxDiff = Math.Max(maxDiff, Math.Abs(x - bdSerial[pi][t])); refCells++; }
            }
            refSyn[pi] = sum / nt;
            refGot++;
        }
        if (refGot == bdNP)
        {
            var mine = Enumerable.Range(0, bdNP).OrderByDescending(pi => bdSynAll[pi]).ToArray();
            var theirs = Enumerable.Range(0, bdNP).OrderByDescending(pi => refSyn[pi]).ToArray();
            q1Top = mine.Take(BdTop).Intersect(theirs.Take(BdTop)).Count();
            q1Bot = mine.TakeLast(BdTop).Intersect(theirs.TakeLast(BdTop)).Count();
            q1MaxDiff = maxDiff;
            q1R = BdCorr(bdSynAll, refSyn);
            Console.WriteLine("`pairs2 run` を別に走らせた TSV（**この期の測定とは別の実行**）と突き合わせた:");
            Console.WriteLine();
            Console.WriteLine("| 量 | 実測 |");
            Console.WriteLine("|---|--:|");
            Console.WriteLine($"| 読めた組 | {refGot} / {bdNP} |");
            Console.WriteLine($"| 台ごとの相乗の**最大の食い違い** | **{maxDiff:F10}pt**（{refCells:N0} セル） |");
            Console.WriteLine($"| 組ごとの相乗の相関 r | {q1R:F6} |");
            Console.WriteLine($"| 上位 {BdTop} の一致 | {q1Top} / {BdTop} |");
            Console.WriteLine($"| 下位 {BdTop} の一致 | {q1Bot} / {BdTop} |");
            Console.WriteLine();
            Console.WriteLine("**第82期の値も併記する**（同じデータから出る量なので、一致しなければ読み方が変わっている）:");
            Console.WriteLine();
            var sigN = Enumerable.Range(0, bdNP).Count(pi => BdPosP(pi) || BdNegP(pi));
            int topPi = Enumerable.Range(0, bdNP).OrderByDescending(pi => bdSynAll[pi]).First();
            int botPi = Enumerable.Range(0, bdNP).OrderBy(pi => bdSynAll[pi]).First();
            Console.WriteLine($"| 量 | この期 | 第82期の報告 |");
            Console.WriteLine("|---|--:|--:|");
            Console.WriteLine($"| 相乗の SE の中央値 | {BdMedian(bdSeAll):F2}pt | 0.58pt |");
            Console.WriteLine($"| 単独の SE の中央値 | {BdMedian(bdSoloSe):F3}pt | 0.157pt |");
            Console.WriteLine($"| 有意な組 | {sigN}（正 {Enumerable.Range(0, bdNP).Count(BdPosP)} / 負 {Enumerable.Range(0, bdNP).Count(BdNegP)}） | 446（正 245 / 負 193） |");
            Console.WriteLine($"| 相乗の 1 位 | {bdName[bdAllPairs[topPi].A]} × {bdName[bdAllPairs[topPi].B]} {BdP2(bdSynAll[topPi])} | グザ × ヴィオ +47.73 |");
            Console.WriteLine($"| 相乗の {bdNP} 位 | {bdName[bdAllPairs[botPi].A]} × {bdName[bdAllPairs[botPi].B]} {BdP2(bdSynAll[botPi])} | ゴルム × カド −25.34 |");
        }
        else Console.WriteLine($"**参照 TSV が足りない（{refGot} / {bdNP} 組）。Q1 は判定できない。**");
    }
    else Console.WriteLine("**参照 TSV（`pairs2 run` の出力）が渡されていない。Q1 は判定できない。**");
    Console.WriteLine();

    // ---- 表A -----------------------------------------------------------------------------------
    Console.WriteLine("## 表A —— 新しい3分（51 体・**独立の広さ → 広さ → 最良の相乗 の降順**）");
    Console.WriteLine();
    Console.WriteLine("| # | 駒 | 群 | 独立の広さ | 広さ | 負 | 最良の相方 | 相乗 | 分類 | 単独 | キー | 第82期 | 理想台 | 最大落ち | 拒否権 |");
    Console.WriteLine("|--:|---|---|--:|--:|--:|---|--:|---|--:|--:|---|--:|--:|---|");
    var bdOrder = Enumerable.Range(0, bdRN)
        .OrderByDescending(u => bdIBr[u]).ThenByDescending(u => bdBr[u])
        .ThenByDescending(u => double.IsNaN(bdBest[u]) ? double.NegativeInfinity : bdBest[u]).ToArray();
    int bdRank = 0;
    foreach (int u in bdOrder)
        Console.WriteLine($"| {++bdRank} | {bdName[u]} | {bdGroupName[bdGrp[u]]} | **{bdIBr[u]}** | {bdBr[u]} | {bdNeg[u]} | "
                          + $"{(bdBestIx[u] < 0 ? "—" : bdName[bdBestIx[u]])} | {BdP2(bdBest[u])} | "
                          + $"{(bdBestIx[u] < 0 ? "—" : bdClassName[BdClass(u, bdBestIx[u])])} | {BdP2(bdSolo[u])} | "
                          + $"{bdKeyOf[u].Length} | {bd82Name[bd82[u]]} | {BdP2(bdIdealAttr[u])} | {BdP2(bdMaxDrop[u])} | {BdVetoOf(u)} |");
    Console.WriteLine();
    int bdNKeep = Enumerable.Range(0, bdRN).Count(u => bdGrp[u] == 0);
    int bdNReb = Enumerable.Range(0, bdRN).Count(u => bdGrp[u] == 1);
    int bdNSwap = Enumerable.Range(0, bdRN).Count(u => bdGrp[u] == 2);
    int bdNFloor = Enumerable.Range(0, bdRN).Count(u => bdSolo[u] < BdFloor);
    Console.WriteLine($"**残す {bdNKeep} / 転生 {bdNReb} / 差し替え {bdNSwap}**（合計 {bdRN}）。"
                      + $"**床（単独 < {BdFloor:F1}）で落ちたのは {bdNFloor} 体**"
                      + $"（うち広さでは残るはずだった駒が {Enumerable.Range(0, bdRN).Count(u => bdSolo[u] < BdFloor && bdIBr[u] >= BdBreadth)} 体・"
                      + $"転生のはずだった駒が {Enumerable.Range(0, bdRN).Count(u => bdSolo[u] < BdFloor && bdIBr[u] < BdBreadth && bdBr[u] >= BdBreadth)} 体）。");
    Console.WriteLine();
    {
        var kz = Enumerable.Range(0, bdRN).Where(u => bdKeyOf[u].Length == 0).ToArray();
        var rankOfU = new Dictionary<int, int>();
        for (int i = 0; i < bdOrder.Length; i++) rankOfU[bdOrder[i]] = i + 1;
        Console.WriteLine($"**キーを1つも持たない {kz.Length} 体は、定義上すべての相手が「共有しない」になるので 独立の広さ = 広さ**"
                          + $"（{string.Join("・", kz.OrderBy(u => rankOfU[u]).Select(u => $"{bdName[u]} {rankOfU[u]}位"))}）。"
                          + $"**上位 10 位のうち {kz.Count(u => rankOfU[u] <= 10)} 体がこれ**——**主判定の軸は「キーを持たないこと」に賞金を出している**（自己検査 (a)）。"
                          + $"キーを持つ 43 体では 独立の広さ ÷ 広さ の中央値は **{BdMedian(Enumerable.Range(0, bdRN).Where(u => bdKeyOf[u].Length > 0 && bdBr[u] > 0).Select(u => (double)bdIBr[u] / bdBr[u])):F2}**。");
        Console.WriteLine();
    }
    Console.WriteLine("「負」は有意な負の相手の数、「最大落ち」は**その駒を素体に落としたときに `CompareBuilds()` の行が落ちる幅の最大**（拒否権1）。"
                      + "「第82期」は同じデータを第82期の線（単独 +1.5 / 相乗 +5.0 / 天井 50%）で切った群。");
    Console.WriteLine();

    // ---- 表B -----------------------------------------------------------------------------------
    Console.WriteLine("## 表B —— 第82期から移動した駒");
    Console.WriteLine();
    var moved = Enumerable.Range(0, bdRN).Where(u => bd82Name[bd82[u]] != bdGroupName[bdGrp[u]]).ToArray();
    Console.WriteLine($"**{moved.Length} / {bdRN} 体が別の群へ移った**"
                      + $"（第82期に別扱いだった {Enumerable.Range(0, bdRN).Count(u => bd82[u] == 3)} 体を除くと {moved.Count(u => bd82[u] != 3)} 体）。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 第82期 | 第83期 | 動いた理由 | 独立の広さ | 広さ | 単独 | 最良の相乗 |");
    Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|");
    foreach (int u in moved.OrderBy(u => bd82[u]).ThenBy(u => bdGrp[u]).ThenByDescending(u => bdIBr[u]))
    {
        string why = bdSolo[u] < BdFloor && bdGrp[u] == 2
            ? (bdIBr[u] >= BdBreadth || bdBr[u] >= BdBreadth ? "**床**（広さでは残る）" : "床（広さでも落ちる）")
            : "広さ";
        Console.WriteLine($"| {bdName[u]} | {bd82Name[bd82[u]]} | {bdGroupName[bdGrp[u]]} | {why} | {bdIBr[u]} | {bdBr[u]} | {BdP2(bdSolo[u])} | {BdP2(bdBest[u])} |");
    }
    Console.WriteLine();
    Console.WriteLine("| 第82期 ＼ 第83期 | 残す | 転生 | 差し替え |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int g = 0; g < 4; g++)
    {
        var line = new List<string>();
        for (int h = 0; h < 3; h++) line.Add(Enumerable.Range(0, bdRN).Count(u => bd82[u] == g && bdGrp[u] == h).ToString());
        Console.WriteLine($"| {bd82Name[g]} | " + string.Join(" | ", line) + " |");
    }
    Console.WriteLine();

    // ---- 表C -----------------------------------------------------------------------------------
    Console.WriteLine("## 表C —— 切れる駒（差し替え候補のうち**拒否権を1つも受けなかった**もの）");
    Console.WriteLine();
    var bdSwaps = Enumerable.Range(0, bdRN).Where(u => bdGrp[u] == 2).OrderBy(u => bdBr[u]).ThenBy(u => bdSolo[u]).ToArray();
    var bdCut = bdSwaps.Where(u => !BdVetoed(u)).ToArray();
    Console.WriteLine($"差し替え候補 **{bdSwaps.Length} 体**のうち、拒否権を1つも受けなかったのは **{bdCut.Length} 体**。");
    Console.WriteLine();
    if (bdCut.Length == 0) Console.WriteLine("**該当なし——切れる駒は 0 体。**");
    else
    {
        Console.WriteLine("| 駒 | 独立の広さ | 広さ | 単独 | 最良の相乗 | 供給するキー | 唯一の書き手 | 唯一の読み手 | compare 行 | 最大落ち | 理想台 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|---|---|--:|--:|--:|");
        foreach (int u in bdCut)
        {
            var sup = bdSup[u].Select(s => s.Key).Distinct().OrderBy(x => x).Select(k => UnitTally.CarryKeys[k]).ToArray();
            var sw = BdSoleWrite(u).Select(k => UnitTally.CarryKeys[k]).ToArray();
            var sr = BdSoleRead(u).Select(k => UnitTally.CarryKeys[k]).ToArray();
            Console.WriteLine($"| {bdName[u]} | {bdIBr[u]} | {bdBr[u]} | {BdP2(bdSolo[u])} | "
                              + $"{BdP2(bdBest[u])}{(bdBestIx[u] < 0 ? "" : "（" + bdName[bdBestIx[u]] + "）")} | "
                              + $"{(sup.Length == 0 ? "**無し**" : string.Join("・", sup))} | {(sw.Length == 0 ? "—" : "**" + string.Join("・", sw) + "**")} | "
                              + $"{(sr.Length == 0 ? "—" : "**" + string.Join("・", sr) + "**")} | {bdRowCnt[u]} | {BdP2(bdMaxDrop[u])} | {BdP2(bdIdealAttr[u])} |");
        }
    }
    Console.WriteLine();
    Console.WriteLine("**差し替え候補の全員**（拒否権の有無を問わず）:");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 独立の広さ | 広さ | 単独 | 最良の相乗 | 供給するキー | compare 行 | 最大落ち | 拒否権 |");
    Console.WriteLine("|---|--:|--:|--:|--:|---|--:|--:|---|");
    foreach (int u in bdSwaps)
    {
        var sup = bdSup[u].Select(s => s.Key).Distinct().OrderBy(x => x).Select(k => UnitTally.CarryKeys[k]).ToArray();
        Console.WriteLine($"| {bdName[u]} | {bdIBr[u]} | {bdBr[u]} | {BdP2(bdSolo[u])} | "
                          + $"{BdP2(bdBest[u])}{(bdBestIx[u] < 0 ? "" : "（" + bdName[bdBestIx[u]] + "）")} | "
                          + $"{(sup.Length == 0 ? "**無し**" : string.Join("・", sup))} | {bdRowCnt[u]} | {BdP2(bdMaxDrop[u])} | {BdVetoOf(u)} |");
    }
    Console.WriteLine();

    // ---- 表D -----------------------------------------------------------------------------------
    Console.WriteLine("## 表D —— 拒否権（§2-3。**1 を「既存 61 行を壊す」に差し替えた**）");
    Console.WriteLine();
    Console.WriteLine("| 拒否権 | 条件 | 差し替え候補で該当 | 51 体で該当 |");
    Console.WriteLine("|---|---|--:|--:|");
    Console.WriteLine($"| 1 | その駒を素体にすると `CompareBuilds()` の行が {BdBreak:F1}pt 以上落ちる | {bdSwaps.Count(u => bdVeto1[u])} | {Enumerable.Range(0, bdRN).Count(u => bdVeto1[u])} |");
    Console.WriteLine($"| 2 | `compare` の主判定 {Baseline.PrimaryRows.Length} 行に含まれる | {bdSwaps.Count(u => bdVeto2[u])} | {Enumerable.Range(0, bdRN).Count(u => bdVeto2[u])} |");
    Console.WriteLine($"| 3 | 唯一の書き手であるキーがある | {bdSwaps.Count(u => bdVeto3[u])} | {Enumerable.Range(0, bdRN).Count(u => bdVeto3[u])} |");
    Console.WriteLine($"| （廃止）旧1 | 理想台とドラフト台で「残す」が割れる | {bdSwaps.Count(u => bd82Veto1[u])} | {Enumerable.Range(0, bdRN).Count(u => bd82Veto1[u])} |");
    Console.WriteLine();
    var bdVetoed = bdSwaps.Where(BdVetoed).ToArray();
    if (bdVetoed.Length > 0)
    {
        Console.WriteLine("**拒否権に当たった差し替え候補:**");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 拒否権 | 内訳 |");
        Console.WriteLine("|---|---|---|");
        foreach (int u in bdVetoed)
        {
            var det = new List<string>();
            if (bdVeto1[u]) det.Add($"`{bdAllRows[bdWorstRow[u]].Name}` が {BdP2(bdMaxDrop[u])} 落ちる");
            if (bdVeto2[u]) det.Add("主判定に在席");
            if (bdVeto3[u]) det.Add("唯一の書き手: " + string.Join("・", BdSoleWrite(u).Select(k => UnitTally.CarryKeys[k])));
            Console.WriteLine($"| {bdName[u]} | {BdVetoOf(u)} | {string.Join(" / ", det)} |");
        }
        Console.WriteLine();
    }
    Console.WriteLine($"**拒否権1 の線は {BdBreak:F1}pt で固定（測る前に決めた・第46期）。ただし分母の割合を数えておく**（自己検査 (a)）"
                      + $"——`最大落ち` の 51 体での中央値は **{BdMedian(bdMaxDrop):F2}pt**:");
    Console.WriteLine();
    Console.WriteLine("| 線 | " + string.Join(" | ", new[] { 2.0, 5.0, 10.0, 20.0, 40.0 }.Select(x => $"{x:F0}pt")) + " |");
    Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", 5)));
    Console.WriteLine("| 51 体で該当 | " + string.Join(" | ", new[] { 2.0, 5.0, 10.0, 20.0, 40.0 }
        .Select(x => Enumerable.Range(0, bdRN).Count(u => !double.IsNaN(bdMaxDrop[u]) && bdMaxDrop[u] >= x))) + " |");
    Console.WriteLine("| 差し替え候補で該当 | " + string.Join(" | ", new[] { 2.0, 5.0, 10.0, 20.0, 40.0 }
        .Select(x => bdSwaps.Count(u => !double.IsNaN(bdMaxDrop[u]) && bdMaxDrop[u] >= x))) + " |");
    Console.WriteLine();
    Console.WriteLine("**廃止した拒否権1（理想台では働いている）で救われていた駒**——"
                      + "第82期の差し替え候補 6 体と、そこで旧1 が立っていたか:");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 第82期の群 | 旧1（台が割れる） | 第83期の群 | 第83期の拒否権 | 切れるか |");
    Console.WriteLine("|---|---|:-:|---|---|:-:|");
    foreach (int u in Enumerable.Range(0, bdRN).Where(u => bd82[u] == 2).OrderBy(u => bdSolo[u]))
        Console.WriteLine($"| {bdName[u]} | {bd82Name[bd82[u]]} | {(bd82Veto1[u] ? "○" : "—")} | {bdGroupName[bdGrp[u]]} | {BdVetoOf(u)} | "
                          + $"{(bdGrp[u] == 2 && !BdVetoed(u) ? "**○**" : "—")} |");
    Console.WriteLine();
    Console.WriteLine($"**旧1 は 51 体中 {Enumerable.Range(0, bdRN).Count(u => bd82Veto1[u])} 体に立つ**のに対し、"
                      + $"**新1 は {Enumerable.Range(0, bdRN).Count(u => bdVeto1[u])} 体**。"
                      + "**指示書 §2-3 は「後者は実際に壊す駒でしか立たない」と書いたが、実測では新1 のほうが広く立つ。**");
    Console.WriteLine();

    // ---- 表E -----------------------------------------------------------------------------------
    Console.WriteLine("## 表E —— 軸ゲーの監視指標（§2-4。**この期から毎期出す**）");
    Console.WriteLine();
    var idxSame = Enumerable.Range(0, bdNP).Where(pi => !BdIndep(bdAllPairs[pi].A, bdAllPairs[pi].B)).ToArray();
    var idxInd = Enumerable.Range(0, bdNP).Where(pi => BdIndep(bdAllPairs[pi].A, bdAllPairs[pi].B)).ToArray();
    var topPairs = Enumerable.Range(0, bdNP).OrderByDescending(pi => bdSynAll[pi]).Take(BdTop).ToArray();
    var botPairs = Enumerable.Range(0, bdNP).OrderBy(pi => bdSynAll[pi]).Take(BdTop).ToArray();
    int topInd = topPairs.Count(pi => BdIndep(bdAllPairs[pi].A, bdAllPairs[pi].B));
    int botInd = botPairs.Count(pi => BdIndep(bdAllPairs[pi].A, bdAllPairs[pi].B));
    Console.WriteLine("| 量 | 同キーの組 | 共有無しの組 |");
    Console.WriteLine("|---|--:|--:|");
    Console.WriteLine($"| 組数 | {idxSame.Length} | {idxInd.Length} |");
    Console.WriteLine($"| 相乗の平均 | {BdP2(idxSame.Average(pi => bdSynAll[pi]))} | {BdP2(idxInd.Average(pi => bdSynAll[pi]))} |");
    Console.WriteLine($"| 相乗の中央値 | {BdP2(BdMedian(idxSame.Select(pi => bdSynAll[pi])))} | {BdP2(BdMedian(idxInd.Select(pi => bdSynAll[pi])))} |");
    Console.WriteLine($"| **上位 {BdTop} に占める数** | **{BdTop - topInd}** | **{topInd}**（期待値 {30.0 * idxInd.Length / bdNP:F1}） |");
    Console.WriteLine($"| 下位 {BdTop} に占める数 | {BdTop - botInd} | {botInd} |");
    Console.WriteLine($"| 有意な正の割合 | {100.0 * idxSame.Count(BdPosP) / idxSame.Length:F1}%（{idxSame.Count(BdPosP)} 組） | {100.0 * idxInd.Count(BdPosP) / idxInd.Length:F1}%（{idxInd.Count(BdPosP)} 組） |");
    Console.WriteLine($"| 有意な負の割合 | {100.0 * idxSame.Count(BdNegP) / idxSame.Length:F1}%（{idxSame.Count(BdNegP)} 組） | {100.0 * idxInd.Count(BdNegP) / idxInd.Length:F1}%（{idxInd.Count(BdNegP)} 組） |");
    Console.WriteLine();
    Console.WriteLine($"**上位 {BdTop} の顔ぶれ**（★ が共有無し）:");
    Console.WriteLine();
    Console.WriteLine("| # | 組 | 相乗 | 分類 | 共有無し |");
    Console.WriteLine("|--:|---|--:|---|:-:|");
    int tk = 0;
    foreach (int pi in topPairs)
    {
        (int a, int b) = bdAllPairs[pi];
        Console.WriteLine($"| {++tk} | {bdName[a]} × {bdName[b]} | {BdP2(bdSynAll[pi])} | {bdClassName[BdClass(a, b)]} | {(BdIndep(a, b) ? "★" : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine("**指示書 §0-1 が挙げた4組**（設計目標の実例）の現在値:");
    Console.WriteLine();
    Console.WriteLine("| 組 | 相乗 | 順位 | 共有無し |");
    Console.WriteLine("|---|--:|--:|:-:|");
    var rankOf = new int[bdNP];
    {
        var ord = Enumerable.Range(0, bdNP).OrderByDescending(pi => bdSynAll[pi]).ToArray();
        for (int i = 0; i < ord.Length; i++) rankOf[ord[i]] = i + 1;
    }
    foreach (var nm in new[] { ("カド", "ハギ"), ("リィカ", "ヴェル"), ("クビ", "ハギ"), ("カド", "キリ") })
    {
        int a = Array.FindIndex(bdRoster, d => d.Name.Contains(nm.Item1));
        int b = Array.FindIndex(bdRoster, d => d.Name.Contains(nm.Item2));
        if (a < 0 || b < 0) continue;
        int pi = bdPairIxOf[a, b];
        Console.WriteLine($"| {bdName[a]} × {bdName[b]} | {BdP2(bdSynAll[pi])} | {rankOf[pi]} | {(BdIndep(a, b) ? "★" : "—")} |");
    }
    Console.WriteLine();

    // ---- 表F -----------------------------------------------------------------------------------
    Console.WriteLine("## 表F —— 唯一の読み手（§2-3 の訂正。**拒否権には使わない。判断の材料として併記する**）");
    Console.WriteLine();
    Console.WriteLine("| キー | " + string.Join(" | ", UnitTally.CarryKeys) + " |");
    Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", bdNK)));
    Console.WriteLine("| 書き手 | " + string.Join(" | ", bdSupCnt) + " |");
    Console.WriteLine("| 読み手 | " + string.Join(" | ", bdReadCnt) + " |");
    Console.WriteLine();
    var soleReaders = Enumerable.Range(0, bdRN).Where(u => BdSoleRead(u).Length > 0).ToArray();
    Console.WriteLine("| 駒 | 唯一の読み手であるキー | 群 | 独立の広さ | 広さ | 単独 | 拒否権 |");
    Console.WriteLine("|---|---|---|--:|--:|--:|---|");
    foreach (int u in soleReaders)
        Console.WriteLine($"| {bdName[u]} | {string.Join("・", BdSoleRead(u).Select(k => UnitTally.CarryKeys[k]))} | {bdGroupName[bdGrp[u]]} | "
                          + $"{bdIBr[u]} | {bdBr[u]} | {BdP2(bdSolo[u])} | {BdVetoOf(u)} |");
    Console.WriteLine();

    // ---- 表G -----------------------------------------------------------------------------------
    Console.WriteLine("## 表G —— 埋め草の偏り（§2-5）");
    Console.WriteLine();
    Console.WriteLine("**まず紙の計算**（戦闘0回）——規則 P と規則 R が実際に引く3枚の中身:");
    Console.WriteLine();
    {
        var statP = new double[3]; var statR = new double[3];
        var usedP = new int[bdRN]; var usedR = new int[bdRN];
        long nP = 0, nR = 0;
        for (int pi = 0; pi < bdNP; pi++)
        {
            (int a, int b) = bdAllPairs[pi];
            var pool = bdRoster.Where((_, u) => u != a && u != b).ToArray();
            int strong0 = (bdRoster[a].Attack >= BdStrong ? 1 : 0) + (bdRoster[b].Attack >= BdStrong ? 1 : 0);
            var seenP = new HashSet<(int, int, int)>();
            var seenR = new HashSet<(int, int, int)>();
            int cP = 0, cR = 0;
            for (int draw = 0; (cP < BdS * BdK || cR < BdS * BdK) && draw < BdDrawCap; draw++)
            {
                int sd = BdSeed(pi, draw);
                if (cP < BdS * BdK)
                {
                    var f = BdFillP(pool, strong0, sd);
                    var t = f.Select(d => bdIdx[d.Id]).OrderBy(x => x).ToArray();
                    if (seenP.Add((t[0], t[1], t[2])))
                    {
                        cP++;
                        foreach (var d in f) { statP[0] += d.Attack; statP[1] += d.MaxHp; if (d.Attack <= 3) statP[2]++; usedP[bdIdx[d.Id]]++; nP++; }
                    }
                }
                if (cR < BdS * BdK)
                {
                    var f = BdFillR(pool, sd);
                    var t = f.Select(d => bdIdx[d.Id]).OrderBy(x => x).ToArray();
                    if (seenR.Add((t[0], t[1], t[2])))
                    {
                        cR++;
                        foreach (var d in f) { statR[0] += d.Attack; statR[1] += d.MaxHp; if (d.Attack <= 3) statR[2]++; usedR[bdIdx[d.Id]]++; nR++; }
                    }
                }
            }
        }
        Console.WriteLine("| 量 | 規則 P（現行） | 規則 R（無作為） |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 埋め草の攻撃力の平均 | {statP[0] / nP:F2} | {statR[0] / nR:F2} |");
        Console.WriteLine($"| 埋め草の最大HP の平均 | {statP[1] / nP:F1} | {statR[1] / nR:F1} |");
        Console.WriteLine($"| **攻 3 以下の割合** | **{100.0 * statP[2] / nP:F1}%** | **{100.0 * statR[2] / nR:F1}%** |");
        Console.WriteLine($"| 一度も引かれない駒 | {Enumerable.Range(0, bdRN).Count(u => usedP[u] == 0)} | {Enumerable.Range(0, bdRN).Count(u => usedR[u] == 0)} |");
        Console.WriteLine($"| 延べ在席の上位 10 体が占める割合 | {100.0 * usedP.OrderByDescending(x => x).Take(10).Sum() / nP:F1}% | {100.0 * usedR.OrderByDescending(x => x).Take(10).Sum() / nR:F1}% |");
        Console.WriteLine();
    }
    int q6Top = -1, q6Bot = -1, q6Grp = -1;
    double q6R = double.NaN;
    if (bdFillPath != "" && File.Exists(bdFillPath))
    {
        var (fY, fNT, fGot) = BdLoad(bdFillPath);
        if (fGot != bdNP) Console.WriteLine($"**規則 R の TSV が足りない（{fGot} / {bdNP} 組）。Q6 は判定できない。**");
        else
        {
            var (fSyn, fSe, fSynS, fSeS, _) = BdSyn(fY, fNT);
            var fSolo = new double[bdRN];
            var fCnt = new int[bdRN];
            var fFloorHit = new int[bdRN];
            for (int pi = 0; pi < bdNP; pi++)
            {
                (int a, int b) = bdAllPairs[pi];
                for (int t = 0; t < fNT[pi]; t++)
                {
                    fSolo[a] += fY[pi][t][0] - fY[pi][t][1]; fCnt[a]++;
                    fSolo[b] += fY[pi][t][0] - fY[pi][t][2]; fCnt[b]++;
                    if (fY[pi][t][3] <= 0.0) { fFloorHit[a]++; fFloorHit[b]++; }
                }
            }
            for (int u = 0; u < bdRN; u++) fSolo[u] /= fCnt[u];
            var (fBr, fIBr, _, fBest, fBestIx, fGrp) = BdAgg(pi => fSyn[pi] > 2 * fSe[pi], pi => fSyn[pi] < -2 * fSe[pi], pi => fSyn[pi], fSolo);
            var mine = Enumerable.Range(0, bdNP).OrderByDescending(pi => bdSynAll[pi]).ToArray();
            var theirs = Enumerable.Range(0, bdNP).OrderByDescending(pi => fSyn[pi]).ToArray();
            q6Top = mine.Take(BdTop).Intersect(theirs.Take(BdTop)).Count();
            q6Bot = mine.TakeLast(BdTop).Intersect(theirs.TakeLast(BdTop)).Count();
            q6R = BdCorr(bdSynAll, fSyn);
            q6Grp = Enumerable.Range(0, bdRN).Count(u => fGrp[u] == bdGrp[u]);
            Console.WriteLine($"**規則 R で同じ表を作り直した**（{bdNP:N0} 組 × {BdS * BdK} 台・"
                              + $"{(long)bdNP * 4 * BdK * BdS * (bdW - 1) * BdM:N0} 戦）:");
            Console.WriteLine();
            Console.WriteLine("| 量 | 実測 | 線 |");
            Console.WriteLine("|---|--:|--:|");
            Console.WriteLine($"| **上位 {BdTop} の一致** | **{q6Top} / {BdTop}** | 20 |");
            Console.WriteLine($"| 下位 {BdTop} の一致 | {q6Bot} / {BdTop} | — |");
            Console.WriteLine($"| 組ごとの相乗の相関 r | {q6R:F3} | — |");
            Console.WriteLine($"| **3分の群が一致する駒** | **{q6Grp} / {bdRN}** | — |");
            Console.WriteLine($"| 相乗の SE の中央値 | {BdMedian(fSe):F2}pt（規則 P は {BdMedian(bdSeAll):F2}） | — |");
            Console.WriteLine($"| 有意な組 | {Enumerable.Range(0, bdNP).Count(pi => Math.Abs(fSyn[pi]) > 2 * fSe[pi])}（規則 P は {Enumerable.Range(0, bdNP).Count(pi => Math.Abs(bdSynAll[pi]) > 2 * bdSeAll[pi])}） | — |");
            Console.WriteLine($"| 床（y00 = 0%）の割合の中央値 | {BdMedian(Enumerable.Range(0, bdRN).Select(u => 100.0 * fFloorHit[u] / fCnt[u])):F1}%（規則 P は {BdMedian(bdFloorPct):F1}%） | — |");
            Console.WriteLine();
            var gBad = Enumerable.Range(0, bdRN).Where(u => fGrp[u] != bdGrp[u]).ToArray();
            if (gBad.Length > 0)
            {
                Console.WriteLine("**群が動いた駒:**");
                Console.WriteLine();
                Console.WriteLine("| 駒 | 規則 P | 規則 R | 独立の広さ（P / R） | 広さ（P / R） | 単独（P / R） |");
                Console.WriteLine("|---|---|---|---|---|---|");
                foreach (int u in gBad.OrderBy(u => bdGrp[u]))
                    Console.WriteLine($"| {bdName[u]} | {bdGroupName[bdGrp[u]]} | {bdGroupName[fGrp[u]]} | {bdIBr[u]} / {fIBr[u]} | "
                                      + $"{bdBr[u]} / {fBr[u]} | {BdP2(bdSolo[u])} / {BdP2(fSolo[u])} |");
                Console.WriteLine();
            }
        }
    }
    else Console.WriteLine("**規則 R の TSV（`breadth run` の出力）が渡されていない。Q6 は判定できない。**");
    Console.WriteLine();

    // ---- Q3 ------------------------------------------------------------------------------------
    Console.WriteLine("## Q3 —— 系列の再現（**主判定**）");
    Console.WriteLine();
    int q3Same = Enumerable.Range(0, bdRN).Count(u => bdGrpS[u][0] == bdGrpS[u][1]);
    Console.WriteLine($"**独立な2系列（{BdK} 台ずつ・台を1つも共有しない）で 3分の群が一致したのは {q3Same} / {bdRN} 体**"
                      + $"（第82期の物差しでは 48 / 51）。**独立の広さ**の系列間相関 r = "
                      + $"**{BdCorr(Enumerable.Range(0, bdRN).Select(u => (double)bdIBrS[u][0]).ToArray(), Enumerable.Range(0, bdRN).Select(u => (double)bdIBrS[u][1]).ToArray()):F3}**、"
                      + $"**広さ**は r = **{BdCorr(Enumerable.Range(0, bdRN).Select(u => (double)bdBrS[u][0]).ToArray(), Enumerable.Range(0, bdRN).Select(u => (double)bdBrS[u][1]).ToArray()):F3}**、"
                      + $"単独は r = **{BdCorr(Enumerable.Range(0, bdRN).Select(u => bdSoloSv[u][0]).ToArray(), Enumerable.Range(0, bdRN).Select(u => bdSoloSv[u][1]).ToArray()):F3}**。");
    Console.WriteLine();
    Console.WriteLine($"**系列は 64 台ずつなので有意の線（2SE）が合算より厳しく、広さそのものが縮む**"
                      + $"——広さの平均は 合算 {Enumerable.Range(0, bdRN).Average(u => (double)bdBr[u]):F1} 対 "
                      + $"系列1 {Enumerable.Range(0, bdRN).Average(u => (double)bdBrS[u][0]):F1} / 系列2 {Enumerable.Range(0, bdRN).Average(u => (double)bdBrS[u][1]):F1}、"
                      + $"独立の広さは 合算 {Enumerable.Range(0, bdRN).Average(u => (double)bdIBr[u]):F1} 対 "
                      + $"{Enumerable.Range(0, bdRN).Average(u => (double)bdIBrS[u][0]):F1} / {Enumerable.Range(0, bdRN).Average(u => (double)bdIBrS[u][1]):F1}。"
                      + "**系列ごとの群は合算の群より厳しい側へずれる**（自己検査 (c)）。");
    Console.WriteLine();
    {
        int agree = 0, either = 0;
        for (int pi = 0; pi < bdNP; pi++)
        {
            bool p1 = bdSynS[pi][0] > 2 * bdSeS[pi][0], p2 = bdSynS[pi][1] > 2 * bdSeS[pi][1];
            if (p1 || p2) { either++; if (p1 && p2) agree++; }
        }
        Console.WriteLine($"**「広さ」は組ごとの有意判定を数え上げた量なので、標本数に依存する。**"
                          + $"2系列で「有意な正」の判定が一致したのは、どちらかで立った {either} 組のうち **{agree} 組（{100.0 * agree / either:F1}%）**、"
                          + $"駒ごとの \\|広さ(1) − 広さ(2)\\| の平均は **{Enumerable.Range(0, bdRN).Average(u => (double)Math.Abs(bdBrS[u][0] - bdBrS[u][1])):F2}**。"
                          + "**第82期の「単独」は標本数に依存しない平均なので、系列に割っても同じ器具のままだった**（r = 1.000）"
                          + "——**Q3 はこの期の軸に対しては構造的に不利な検定である。**");
    }
    Console.WriteLine();
    var q3Bad = Enumerable.Range(0, bdRN).Where(u => bdGrpS[u][0] != bdGrpS[u][1]).ToArray();
    if (q3Bad.Length > 0)
    {
        Console.WriteLine("| 駒 | 合算 | 系列1 | 系列2 | 独立の広さ（合算 / 1 / 2） | 広さ（合算 / 1 / 2） | 単独（合算 / 1 / 2） | 境界 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (int u in q3Bad)
        {
            string edge = bdSoloSv[u].Any(x => x < BdFloor) && !(bdSoloSv[u][0] < BdFloor && bdSoloSv[u][1] < BdFloor)
                ? $"床 {BdFloor:F1}"
                : (bdIBrS[u][0] >= BdBreadth) != (bdIBrS[u][1] >= BdBreadth) ? $"独立の広さ {BdBreadth}" : $"広さ {BdBreadth}";
            Console.WriteLine($"| {bdName[u]} | {bdGroupName[bdGrp[u]]} | {bdGroupName[bdGrpS[u][0]]} | {bdGroupName[bdGrpS[u][1]]} | "
                              + $"{bdIBr[u]} / {bdIBrS[u][0]} / {bdIBrS[u][1]} | {bdBr[u]} / {bdBrS[u][0]} / {bdBrS[u][1]} | "
                              + $"{BdP2(bdSolo[u])} / {BdP2(bdSoloSv[u][0])} / {BdP2(bdSoloSv[u][1])} | {edge} |");
        }
        Console.WriteLine();
    }

    // ---- 予測の答え合わせ -------------------------------------------------------------------------
    Console.WriteLine("## 予測の答え合わせ");
    Console.WriteLine();
    var supHolder = Enumerable.Range(0, bdRN).Where(u => bdSup[u].Length > 0).ToArray();
    var noSup = Enumerable.Range(0, bdRN).Where(u => bdSup[u].Length == 0).ToArray();
    double mIbrSup = supHolder.Average(u => (double)bdIBr[u]), mIbrNo = noSup.Average(u => (double)bdIBr[u]);
    Console.WriteLine("| # | 予測 | 実測 | |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| P1 | 群の入れ替わりが 15 体以上 | {moved.Length} 体 | {(moved.Length >= 15 ? "○" : "**×**")} |");
    Console.WriteLine($"| P2 | `Supplies` を持つ書き手が上がる | 独立の広さの平均 **{mIbrSup:F2}**（{supHolder.Length} 体） 対 **{mIbrNo:F2}**（{noSup.Length} 体） | {(mIbrSup > mIbrNo ? "○" : "**×**")} |");
    {
        var pure = new[] { "エグ", "ノノ", "ハリ", "ナタ" }.Select(n => Array.FindIndex(bdRoster, d => d.Name.Contains(n))).Where(i => i >= 0).ToArray();
        int down = pure.Count(u => bdGrp[u] == 2);
        Console.WriteLine($"| P3 | 供給を持たない純粋な読み手が下がる | {string.Join(" / ", pure.Select(u => bdName[u] + " " + bdGroupName[bdGrp[u]]))} | {(down == pure.Length ? "○" : "**×**")} |");
        int sasa = Array.FindIndex(bdRoster, d => d.Name.Contains("ササ"));
        bool p4 = sasa >= 0 && bdSolo[sasa] >= BdFloor && bdGrp[sasa] != 0;
        Console.WriteLine($"| P4 | ササは床では止まらないが広さで苦しい | {(sasa < 0 ? "—" : $"{bdGroupName[bdGrp[sasa]]}（単独 {BdP2(bdSolo[sasa])} / 独立の広さ {bdIBr[sasa]} / 広さ {bdBr[sasa]}）")} | {(p4 ? "○" : "**×**")} |");
    }
    Console.WriteLine($"| P5 | 切れる駒が 1〜5 体 | {bdCut.Length} 体 | {(bdCut.Length >= 1 && bdCut.Length <= 5 ? "○" : "**×**")} |");
    {
        double mSame = idxSame.Average(pi => bdSynAll[pi]), mInd = idxInd.Average(pi => bdSynAll[pi]);
        bool p6 = mInd < mSame && topInd >= 1;
        Console.WriteLine($"| P6 | 共有無しは平均で小さいが上位に食い込む | 平均 {BdP2(mInd)} 対 {BdP2(mSame)}・上位 {BdTop} に {topInd} 組 | {(p6 ? "○" : "**×**")} |");
    }
    Console.WriteLine();

    // ---- 判定 -----------------------------------------------------------------------------------
    Console.WriteLine("## 判定");
    Console.WriteLine();
    Console.WriteLine("| # | 問い | 実測 | 判定 |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| Q1 | 器具の再現（第82期のデータがそのまま使えるか） | 上位 {(q1Top < 0 ? "—" : q1Top.ToString())} / 下位 {(q1Bot < 0 ? "—" : q1Bot.ToString())}"
                      + $"（台ごとの最大差 {(double.IsNaN(q1MaxDiff) ? "—" : q1MaxDiff.ToString("F10"))}） | {(q1Top >= 20 && q1Bot >= 20 ? "○" : q1Top < 0 ? "—" : "**×**")} |");
    Console.WriteLine($"| **Q2** | **第82期の3分から 15 体以上が別の群へ移る** | **{moved.Length} / {bdRN} 体** | {(moved.Length >= 15 ? "○" : "**×**")} |");
    Console.WriteLine($"| **Q3** | **独立2系列で群が一致する駒が 45/51 以上** | **{q3Same} / {bdRN}** | {(q3Same >= 45 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q4 | 切れる駒（拒否権を通過した差し替え候補） | **{bdCut.Length} 体** / 候補 {bdSwaps.Length} 体 | — |");
    Console.WriteLine($"| **Q5** | **共有無しの組が上位 {BdTop} に 5 組以上** | **{topInd} 組**（期待値 {30.0 * idxInd.Length / bdNP:F1}） | {(topInd >= 5 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q6 | 埋め草を変えても上位 {BdTop} が 20/30 以上一致 | {(q6Top < 0 ? "—" : $"{q6Top} / {BdTop}（群の一致 {q6Grp} / {bdRN}）")} | {(q6Top < 0 ? "—" : q6Top >= 20 ? "○" : "**×**")} |");
    Console.WriteLine("| Q7 | `compare` 305 セル 0 件・`docs/` 差分 0 | `breadth check` と `docs/` の再生成で別に確かめる | — |");
    Console.WriteLine();
    Console.WriteLine($"所要 {bdSw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}
}
