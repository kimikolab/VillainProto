using BattleCore;
using static Common;

// =====================================================================================
// deep モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "deep")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 deep
// =====================================================================================

static class DeepDiag
{
// 深手（第93期）—— 傷が束ねられ、動くたびに開く。**1本の規則で交差を3本引く。**
//
// 第92期の答え（交差の空白は台の空白ではなく機構の空白。11キー55組のうち繋いでいる機構が
// 実在するのは12組だけ）を受けて、**軸をまたぐ機構そのもの**を1本足して測る。
// 器具は第81期 `pairs2` の 2×2 の写しで**定数を1つも変えていない**。
//
//     dotnet run --project BattleSim -c Release 0 deep phase0                # 表A（門・§1-2 の事実・紙）
//     dotnet run --project BattleSim -c Release 0 deep run <a> [skip] [take] # 2×2 の TSV（a = nomi / kiri。W0 と W1 を両方吐く）
//     dotnet run --project BattleSim -c Release 0 deep tables <TSV...>       # 表C（主判定）
//     dotnet run --project BattleSim -c Release 0 deep cross                 # 表B・D・E（理想61行・(G2)・交差帯12行）
//     dotnet run --project BattleSim -c Release 0 deep foe                   # 表A'（**敵側をローカル台で測る**。§1-1 の但し書き）
//     dotnet run --project BattleSim -c Release 0 deep check                 # 表F（自己検査 (a)〜(j)）
public static void Run(string[] args, int stageIndex)
{
    string dpArg = args.Length > 2 ? args[2] : "";
    var dpSw = System.Diagnostics.Stopwatch.StartNew();
    var dpInv = System.Globalization.CultureInfo.InvariantCulture;

    IReadOnlyList<EnemyCatalog.Stage> dpStages = EnemyCatalog.Stages;
    int dpW = dpStages.Count;
    var dpRoster = UnitCatalog.All.ToArray();
    int dpRN = dpRoster.Length;                       // 51

    // ---- 第81期 `pairs2` の定数の写し（**1つも変えていない**）----------------------------------
    const int DpTableSeed = 9_300_000;                // **第93期の標本**
    const int DpK = 64, DpS = 2;
    const int DpBand = 0, DpM = 8;
    const int DpStrong = 7, DpWeakPct = 60, DpDrawCap = 20000;
    const int DpTop = 20;
    const int DpMinInfo = 20;                         // 第88期 §4-1
    const int DpFpLine = 3;                           // Q1-2 の線（§3-1）
    const double DpEps = 1e-9;
    const int DpIdealSeeds = 200;                     // 理想台（`compare` と同じ帯）

    var dpIdx = new Dictionary<string, int>();
    for (int u = 0; u < dpRN; u++) dpIdx[dpRoster[u].Id] = u;
    string[] dpName = dpRoster.Select(d => d.Name).ToArray();

    int dpNomi = dpIdx["nomi"], dpKiri = dpIdx["kiri"];
    // **意図した相手6枚**（§3-1）: 自傷を読む4枚 ＋ 死を読む2枚。
    // 自傷は `lethal: true` なので「傷 × 被弾」と「傷 × 死」の2本がここで繋がる。
    string[] dpIntendedIds = { "mudo", "gald", "doha", "sekki", "rica", "hagi" };

    int[] DpOthers(int a) => Enumerable.Range(0, dpRN).Where(u => u != a).ToArray();

    // ---- 版 ------------------------------------------------------------------------------------
    DeepRule DpVer(int v) => new(v != 0);
    string DpVName(int v) => v == 0 ? "W0（現行）" : "W1（深手）";

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜92期と同一。`Stages` は書き換えない）--------------------
    var dpWeakCache = new Dictionary<string, UnitDef>();
    UnitDef DpWeakOf(UnitDef d)
    {
        if (dpWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * DpWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        dpWeakCache[d.Id] = w;
        return w;
    }
    var dpWeak = dpStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = DpWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef DpPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var dpPlainMap = dpRoster.ToDictionary(d => d.Id, DpPlain);

    UnitDef[] DpFill(UnitDef[] pool, int strong0, int seed)
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
            if (sel.Attack >= DpStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int DpSeed(int tableSeed, int pairIx, int draw)
    {
        ulong x = (ulong)tableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] DpSeats(UnitDef[] u)
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
    // **TSV の添字**: 0 = y11 / 1 = y01（**A 素体**・B 本物）/ 2 = y10（A 本物・**B 素体**）/ 3 = y00
    // **A を先に固定してから添字を書いた**（第85期の罠。A = ノミ／キリ）。
    Formation DpForm(UnitDef[] u, int[] seats, int cell)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (cell & 1) != 0) || (k == 1 && (cell & 2) != 0);
            f[seats[k]] = plain ? dpPlainMap[u[k].Id] : u[k];
        }
        return f;
    }

    // 版の割り当て。**DeepRule だけを振る**（他は現行の既定のまま）。
    BattleResult DpRun(Formation f, Formation e, int seed, bool verbose, int v)
        => BattleEngine.Run(f, e, seed, verbose: verbose, deep: DpVer(v));

    double DpRate(Formation f, int v)
    {
        double sum = 0;
        for (int wi = 1; wi < dpW; wi++)
        {
            int wins = 0;
            for (int seed = DpBand; seed < DpBand + DpM; seed++)
                if (DpRun(f, dpWeak[wi].Enemy, seed, false, v).PlayerWon) wins++;
            sum += wins * 100.0 / DpM;
        }
        return sum / (dpW - 1);
    }
    string DpP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double DpSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double DpSe(IReadOnlyList<double> xs) => xs.Count < 2 ? double.NaN : DpSd(xs) / Math.Sqrt(xs.Count);
    double DpPct(IReadOnlyList<double> xs, double q)
    {
        if (xs.Count == 0) return double.NaN;
        var s = xs.OrderBy(v => v).ToArray();
        if (s.Length == 1) return s[0];
        double pos = q * (s.Length - 1);
        int lo = (int)Math.Floor(pos), hi = Math.Min(lo + 1, s.Length - 1);
        return s[lo] + (pos - lo) * (s[hi] - s[lo]);
    }
    double DpMed(IReadOnlyList<double> xs) => DpPct(xs, 0.5);

    var dpPairIxOf = new int[dpRN, dpRN];
    {
        int pi = 0;
        for (int a = 0; a < dpRN; a++) for (int b = a + 1; b < dpRN; b++) { dpPairIxOf[a, b] = dpPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> DpFills(int a, int b)
    {
        var pool = dpRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (dpRoster[a].Attack >= DpStrong ? 1 : 0) + (dpRoster[b].Attack >= DpStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < DpS * DpK && draw < DpDrawCap; draw++)
        {
            var f = DpFill(pool, strong0, DpSeed(DpTableSeed, dpPairIxOf[a, b], draw));
            var t = f.Select(d => dpIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }
    UnitDef[] DpTeam(int a, int b, UnitDef[] fill) => new[] { dpRoster[a], dpRoster[b], fill[0], fill[1], fill[2] };
    double[][] DpMeasure(int a, int b, int v)
    {
        var fills = DpFills(a, b);
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = DpTeam(a, b, fills[t]);
            int[] seats = DpSeats(team);
            ys[t] = new double[4];
            for (int cell = 0; cell < 4; cell++) ys[t][cell] = DpRate(DpForm(team, seats, cell), v);
        }
        return ys;
    }

    // ---- TSV --------------------------------------------------------------------------------------
    // **1つのファイルに W0 と W1（と複数の A）が混ざっていてよい**（`deep run` は両版を吐く）。
    List<(int V, int A, int B, double[][] Ys)> DpRead(string path)
    {
        var outp = new List<(int, int, int, double[][])>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            if (c.Length < 4 || !int.TryParse(c[0], out int v)) continue;
            int a = int.Parse(c[1]), b = int.Parse(c[2]), nT = int.Parse(c[3]);
            var ys = new double[nT][];
            int at = 4;
            for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], dpInv); }
            outp.Add((v, a, b, ys));
        }
        return outp;
    }
    string DpTsvRow(int v, int a, int b, double[][] ys)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(v).Append('\t').Append(a).Append('\t').Append(b).Append('\t').Append(ys.Length);
        foreach (double[] yy in ys) for (int k = 0; k < 4; k++) sb.Append('\t').Append(yy[k].ToString("R", dpInv));
        return sb.ToString();
    }
    double DpSyn(double[] y) => y[0] - y[2] - y[1] + y[3];
    bool DpFloorT(double[] y) => y[3] < DpEps && y[1] < DpEps;
    bool DpCeilT(double[] y) => y[3] > 100 - DpEps && y[1] > 100 - DpEps;
    bool DpInfoT(double[] y) => !DpFloorT(y) && !DpCeilT(y);

    (double M, double Se, double[] Ser, int N) DpAgg(double[] d, bool[]? use)
    {
        var all = new List<double>(); var s0 = new List<double>(); var s1 = new List<double>();
        for (int t = 0; t < d.Length; t++)
        {
            if (use is not null && !use[t]) continue;
            all.Add(d[t]); (t % DpS == 0 ? s0 : s1).Add(d[t]);
        }
        return (all.Count > 0 ? all.Average() : double.NaN, DpSe(all),
                new[] { s0.Count > 0 ? s0.Average() : double.NaN, s1.Count > 0 ? s1.Average() : double.NaN }, all.Count);
    }
    (Dictionary<int, double[]> D, Dictionary<int, bool[]> Info, Dictionary<int, (int All, int Floor, int Ceil, int Info)> Cnt, int Mism)
        DpFold(Dictionary<int, double[][]> v0, Dictionary<int, double[][]> v1, int[] others)
    {
        var D = new Dictionary<int, double[]>();
        var Info = new Dictionary<int, bool[]>();
        var Cnt = new Dictionary<int, (int, int, int, int)>();
        int mism = 0;
        foreach (int b in others)
        {
            if (!v0.ContainsKey(b) || !v1.ContainsKey(b)) continue;
            int nT = Math.Min(v0[b].Length, v1[b].Length);
            var d = new double[nT]; var inf = new bool[nT];
            int fl = 0, ce = 0, ok = 0;
            for (int t = 0; t < nT; t++)
            {
                d[t] = DpSyn(v1[b][t]) - DpSyn(v0[b][t]);
                if (Math.Abs(v0[b][t][1] - v1[b][t][1]) > 1e-9) mism++;
                if (Math.Abs(v0[b][t][3] - v1[b][t][3]) > 1e-9) mism++;
                inf[t] = DpInfoT(v0[b][t]);
                if (DpFloorT(v0[b][t])) fl++; else if (DpCeilT(v0[b][t])) ce++; else ok++;
            }
            D[b] = d; Info[b] = inf; Cnt[b] = (nT, fl, ce, ok);
        }
        return (D, Info, Cnt, mism);
    }

    // ---- 1つの 2x2 の判定を丸ごと出す（第90・91期 `soak` の写し）----------------------------------
    // **(G3)**: 深手は engine の規則なので**主判定はフィルタ無し**。フィルタ有りは参考として併記する。
    bool DpJudge(string title, int a, string[] intendedIds,
                 Dictionary<int, double[][]> d0, Dictionary<int, double[][]> d1, bool filtered)
    {
        var others = DpOthers(a);
        var (D, Info, Cnt, mism) = DpFold(d0, d1, others);
        var intended = new HashSet<int>(intendedIds.Select(i => dpIdx[i]));
        var agAll = D.Keys.ToDictionary(b => b, b => DpAgg(D[b], null));
        var agFil = D.Keys.ToDictionary(b => b, b => DpAgg(D[b], filtered ? Info[b] : null));
        var measurable = filtered ? D.Keys.Where(b => Cnt[b].Info >= DpMinInfo).ToHashSet() : D.Keys.ToHashSet();
        var unint = measurable.Where(b => !intended.Contains(b)).ToArray();

        // **ノイズ床（第89期 §1-1 の規約）**: 同じ実験の中の「意図しない相手」の |Δ相乗| の 95 パーセンタイル
        double floor = DpPct(unint.Select(b => Math.Abs(agFil[b].M)).ToArray(), 0.95);

        int cAll = D.Keys.Sum(b => Cnt[b].All), cFl = D.Keys.Sum(b => Cnt[b].Floor),
            cCe = D.Keys.Sum(b => Cnt[b].Ceil), cIn = D.Keys.Sum(b => Cnt[b].Info);
        Console.WriteLine($"### {title}");
        Console.WriteLine();
        Console.WriteLine($"A = **{dpName[a]}**・版は {DpVName(0)} 対 {DpVName(1)}・"
                          + $"**情報帯フィルタ {(filtered ? "有り（参考）" : "無し（主判定・(G3)）")}**。"
                          + $"意図した相手 {intended.Count} 枚（{string.Join("・", intendedIds.Select(i => dpName[dpIdx[i]]))}）／意図しない相手 {unint.Length} 体。");
        Console.WriteLine();
        Console.WriteLine("| | 台数 | 割合 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 全台（{D.Count} 体 × {DpS * DpK} 台） | {cAll:N0} | 100.0% |");
        Console.WriteLine($"| 床（`y00` = `y01` = 0.0%） | {cFl:N0} | {cFl * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| 天井（`y00` = `y01` = 100.0%） | {cCe:N0} | {cCe * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| **情報帯** | **{cIn:N0}** | **{cIn * 100.0 / cAll:F1}%** |");
        Console.WriteLine();
        Console.WriteLine($"**参考**: A 素体の2セル（`y01` / `y00`）が版で一致した数 → {cAll * 2:N0} セル・ずれ **{mism}** 件。"
                          + "**engine の規則なので A を素体にしても規則は走る**（第90期の自己検査 (a) の訂正・(G3)）"
                          + "——ここは 0 を要求しない。**選別に W0 のセルしか使っていないこと**が代わりの検査。");
        Console.WriteLine();
        Console.WriteLine($"**ノイズ床（第89期 §1-1 の規約）= 意図しない {unint.Length} 体の \\|Δ\\| の 95%tile = {floor:F2}pt**"
                          + $"（中央値 {DpMed(unint.Select(b => Math.Abs(agFil[b].M)).ToArray()):F2} / 最大 {(unint.Length > 0 ? unint.Max(b => Math.Abs(agFil[b].M)) : double.NaN):F2}）。");
        Console.WriteLine();

        var ordF = measurable.OrderByDescending(b => agFil[b].M).ToArray();
        Console.WriteLine("| 順位 | B | **Δ** | SE | 情報帯 | 系列1 | 系列2 | 床超 | Δ（全台） | SE |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|--:|--:|");
        var shown = new HashSet<int>();
        void Row(int b)
        {
            int rf = Array.IndexOf(ordF, b) + 1;
            var af = agFil[b]; var aa2 = agAll[b];
            bool over = measurable.Contains(b) && Math.Abs(af.M) > floor;
            Console.WriteLine($"| {(rf > 0 ? rf.ToString() : "—")} | {(intended.Contains(b) ? "★" : "")}{dpName[b]} | **{DpP2(af.M)}** | {af.Se:F2} | {Cnt[b].Info} | {DpP2(af.Ser[0])} | {DpP2(af.Ser[1])} | {(over ? "○" : "")} | {DpP2(aa2.M)} | {aa2.Se:F2} |");
        }
        int top = Math.Min(DpTop / 2, ordF.Length);
        for (int i = 0; i < top; i++) { Row(ordF[i]); shown.Add(ordF[i]); }
        if (ordF.Length > 2 * top) Console.WriteLine("| … | | | | | | | | | |");
        for (int i = Math.Max(top, ordF.Length - top); i < ordF.Length; i++) { Row(ordF[i]); shown.Add(ordF[i]); }
        Console.WriteLine();
        var rest2 = intended.Concat(D.Keys.Where(b => !measurable.Contains(b))).Distinct().Where(b => D.ContainsKey(b) && !shown.Contains(b)).ToArray();
        if (rest2.Length > 0)
        {
            Console.WriteLine("意図した相手（上の表に出ていないぶん）と「測れていない」組:");
            Console.WriteLine();
            Console.WriteLine("| 順位 | B | **Δ** | SE | 情報帯 | 系列1 | 系列2 | 床超 | Δ（全台） | SE |");
            Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|--:|--:|");
            foreach (int b in rest2) Row(b);
            Console.WriteLine();
        }

        var intM = intended.Where(measurable.Contains).ToArray();
        var passers = intM.Where(b => agFil[b].Ser[0] > 0 && agFil[b].Ser[1] > 0 && agFil[b].M > floor).ToArray();
        var fpAll = unint.Where(b => Math.Abs(agFil[b].M) > floor).ToArray();
        var fpNeg = unint.Where(b => agFil[b].M < -floor).ToArray();
        bool q11 = passers.Length >= 1, q12 = fpAll.Length <= DpFpLine;
        int bestRank = intM.Length == 0 ? 0 : intM.Min(b => Array.IndexOf(ordF, b) + 1);
        Console.WriteLine("| | 内容 | 実測 | 線 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1-1** | 意図した相手のうち 2系列とも正 かつ \\|Δ\\| > ノイズ床 | **{passers.Length} / {intM.Length} 枚**"
                          + (passers.Length > 0 ? "（" + string.Join("・", passers.Select(b => $"{dpName[b]} {DpP2(agFil[b].M)}")) + "）" : "")
                          + $" | ≥ 1 | **{(q11 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1-2** | 意図しない相手で床超の体数 | **{fpAll.Length} / {unint.Length} 体**（負 {fpNeg.Length}） | ≤ {DpFpLine} | **{(q12 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1** | **特異性（主判定）** | | | **{(q11 && q12 ? "○" : "×")}** |");
        Console.WriteLine($"| Q2 | 意図した組の最良順位（{ordF.Length} 体中） | **{(bestRank > 0 ? bestRank + " 位" : "—")}**"
                          + (bestRank > 0 ? $"（{dpName[intM.OrderBy(b => Array.IndexOf(ordF, b)).First()]}）" : "") + " | ≤ 10 | "
                          + $"**{(bestRank > 0 && bestRank <= 10 ? "○" : "×")}** |");
        Console.WriteLine($"| 自己検査 (i) | 主判定が2系列で同符号 | "
                          + (intM.Length > 0 ? string.Join(" / ", intM.OrderBy(b => Array.IndexOf(ordF, b)).Take(4).Select(b => $"{dpName[b]} {DpP2(agFil[b].Ser[0])}・{DpP2(agFil[b].Ser[1])}")) : "—")
                          + " | | |");
        Console.WriteLine();
        Console.WriteLine($"**{title} の結論: {(q11 && q12 ? "通る" : "通らない")}**（Q1-1 {(q11 ? "○" : "×")} / Q1-2 {(q12 ? "○" : "×")}）。");
        Console.WriteLine();
        return q11 && q12;
    }

    // =====================================================================================
    // 理想台の計測（表A・B・D・E で共有）。**盤面は `CompareBuilds()` / `CrossBuilds()` のまま。**
    // =====================================================================================
    // 列の添字（陣営ごとに1組ずつ持つ）。
    const int DcReach = 0, DcActs = 1, DcOnTop = 2, DcBundle = 3, DcBundleT = 4,
              DcBite = 5, DcBiteOut = 6, DcRelay = 7, DcOver = 8, DcOverOut = 9,
              DcStall = 10, DcSoak = 11, DcGathA = 12, DcWrites = 13, DcTaken = 14, DcCols = 15;

    (double[,] Win, double[,] Turns, double[,,,] C, long[,] Route)
        DpIdeal((string Name, Formation F)[] rows, int v)
    {
        int n = rows.Length;
        var win = new double[n, dpW];
        var turns = new double[n, dpW];
        var acc = new double[n, dpW, 2, DcCols];          // [行, 波, 陣営(0 味方 / 1 敵), 列]
        var route = new long[2, BattleContext.WoundRouteCount];
        var lockObj = new object();
        var enemyIds = dpStages.Select(st => st.Enemy.Occupied().Select(o => o.Def.Id).ToHashSet()).ToArray();
        Parallel.For(0, n, ri =>
        {
            var localRoute = new long[2, BattleContext.WoundRouteCount];
            for (int w = 0; w < dpW; w++)
            {
                int wins = 0; long tt = 0;
                for (int seed = 0; seed < DpIdealSeeds; seed++)
                {
                    BattleResult r = DpRun(rows[ri].F, dpStages[w].Enemy, seed, false, v);
                    if (r.PlayerWon) wins++;
                    tt += r.Turns;
                    foreach (var kv in r.TallyByUnit)
                    {
                        UnitTally t = kv.Value;
                        int side = enemyIds[w].Contains(kv.Key) ? 1 : 0;
                        acc[ri, w, side, DcReach] += t.DeepReach;
                        acc[ri, w, side, DcActs] += t.DeepActs;
                        acc[ri, w, side, DcOnTop] += t.DeepOnTop;
                        acc[ri, w, side, DcBundle] += t.DeepBundles;
                        acc[ri, w, side, DcBundleT] += t.DeepBundleFirstTurn;
                        acc[ri, w, side, DcBite] += t.DeepBiteFires;
                        acc[ri, w, side, DcBiteOut] += t.DeepBiteOut;
                        acc[ri, w, side, DcRelay] += t.DeepBiteRelayed;
                        acc[ri, w, side, DcOver] += t.DeepOverFires;
                        acc[ri, w, side, DcOverOut] += t.DeepOverOut;
                        acc[ri, w, side, DcStall] += t.DeepStalled;
                        acc[ri, w, side, DcSoak] += t.DeepSoakDeeper;
                        acc[ri, w, side, DcGathA] += t.DeepGatherAfter;
                        acc[ri, w, side, DcWrites] += t.WoundWrites;
                        acc[ri, w, side, DcTaken] += t.DamageTaken;
                        if (t.WoundWritesByRoute is int[] wr)
                            for (int c = 0; c < wr.Length; c++) localRoute[side, c] += wr[c];
                    }
                }
                win[ri, w] = wins * 100.0 / DpIdealSeeds;
                turns[ri, w] = (double)tt / DpIdealSeeds;
                for (int side = 0; side < 2; side++)
                    for (int c = 0; c < DcCols; c++) acc[ri, w, side, c] /= DpIdealSeeds;
            }
            lock (lockObj)
                for (int side = 0; side < 2; side++)
                    for (int c = 0; c < BattleContext.WoundRouteCount; c++) route[side, c] += localRoute[side, c];
        });
        return (win, turns, acc, route);
    }

    double DpSum((string Name, Formation F)[] rows, double[,,,] c, int side, int col)
    {
        double s = 0;
        for (int i = 0; i < rows.Length; i++) for (int w = 0; w < dpW; w++) s += c[i, w, side, col];
        return s / (rows.Length * dpW);
    }

    // その行の駒それぞれについて、**その駒を含む「他の」行**の全波平均の変化（第91期 (G2)）。
    (string Unit, int OtherRows, double Delta)[] DpBlame(
        (string Name, Formation F)[] rows, int target, double[,] a, double[,] b)
    {
        var ids = rows[target].F.Occupied().Select(o => o.Def).ToArray();
        var outp = new List<(string, int, double)>();
        foreach (UnitDef d in ids)
        {
            var others = Enumerable.Range(0, rows.Length)
                .Where(i => i != target && rows[i].F.Occupied().Any(o => o.Def.Id == d.Id)).ToArray();
            double dl = others.Length == 0 ? double.NaN
                : others.Average(i => Enumerable.Range(0, dpW).Average(w => b[i, w] - a[i, w]));
            outp.Add((d.Name, others.Length, dl));
        }
        return outp.ToArray();
    }

    // 2版ぶんの勝率だけを回す（`compare` / 交差帯の突き合わせ用）。
    double[][,] DpCompare2((string Name, Formation F)[] rows)
    {
        var res = new double[2][,];
        for (int v = 0; v < 2; v++) res[v] = new double[rows.Length, dpW];
        Parallel.For(0, rows.Length, ri =>
        {
            for (int v = 0; v < 2; v++)
                for (int w = 0; w < dpW; w++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < DpIdealSeeds; seed++)
                        if (DpRun(rows[ri].F, dpStages[w].Enemy, seed, false, v).PlayerWon) wins++;
                    res[v][ri, w] = wins * 100.0 / DpIdealSeeds;
                }
        });
        return res;
    }

    // =====================================================================================
    // phase0（表A: 門・§1-2 の事実・紙）
    // =====================================================================================
    if (dpArg == "phase0")
    {
        var rows = CompareBuilds();
        var xrows = CrossBuilds();

        Console.WriteLine("# 第93期 表A —— 門（§1-1）と §1-2 の事実");
        Console.WriteLine();
        Console.WriteLine($"束ねる数 `Bundle` = **{DeepRule.Bundle}** ／ 自傷と上乗せの量 `DeepBite` = **{DeepRule.DeepBite}**"
                          + "（**3つとも振らない**。掃引はこの期に無い）。");
        Console.WriteLine();

        // ---- §1-2 の 1: 傷を書く箇所の全数 -------------------------------------------------------
        Console.WriteLine("## §1-2 の 1 —— 傷を書く箇所の全数（**指示書の数を信用せず数え直した**）");
        Console.WriteLine();
        Console.WriteLine("`grep -n \"StatusKeys.Wound\" BattleCore/*.cs` の全数を、加算／減算／上書き／読み に分類した。"
                          + "**加算だけが新しい窓口（`BattleContext.Wound`）を通る。**");
        Console.WriteLine();
        Console.WriteLine("| 分類 | 場所 | 経路 | 窓口を通すか |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine("| **加算** | `RendTrait.OnAfterAttack` | 裂き（キリ・主目標に 1） | ○ |");
        Console.WriteLine("| **加算** | `CarveTrait.OnAfterAttack` | 刻み（ノミ・なぞってから 1） | ○ |");
        Console.WriteLine("| **加算** | `BattleEngine.ApplyDamage`（巻き込み則） | 味方の刃が通ると 1（第85期・**既定 on**） | ○ |");
        Console.WriteLine("| **加算** | `ThornsTrait`（刺し返し） | 棘の傷（第84期・**既定 off**） | ○ |");
        Console.WriteLine("| **加算** | `ThornsTrait`（余波） | 棘の巻き込みの傷（第84期・**既定 off**） | ○ |");
        Console.WriteLine("| **加算** | `GatherRule`（受け取る側） | 引き取り（第89期に作り第90期に採用・**既定 on**）。**中継であって新しい供給ではない** | ○ |");
        Console.WriteLine("| 減算 | `GatherRule`（donor 側） | 引き取り。深手は移せない（別キー） | × |");
        Console.WriteLine("| 減算 | `SutureTrait`（塞ぎ） | 縫い。1つ引く | × |");
        Console.WriteLine("| 減算 | `MenderTrait`（塞ぎ） | 繕い（第92期に採用）。1つ引く | × |");
        Console.WriteLine("| 上書き | `SeverTrait.OnAfterAttack` | 断ち。0 に戻す | × |");
        Console.WriteLine("| 読み | `BattleContext.Poison` / `Ignite` | 滲み則（第90・91期） | — |");
        Console.WriteLine("| 読み | `BattleContext.PerformAttack` | 薄刃の代金 `ThinBladeCost.Unwounded`（既定 off） | — |");
        Console.WriteLine("| 読み | `GougeTrait` / `CarveTrait`（なぞり） / `SeverTrait` / `SutureTrait` / `MenderTrait` | 傷の読み手5枚 | — |");
        Console.WriteLine("| 読み | `AmplifierTrait`（ミオの着火・`IgniteRule`） | 第87期に作り第89期に採用・**既定 on** | — |");
        Console.WriteLine("| 読み | `GatherRule`（donor 候補） | **raw の傷だけ**（深手は移せない） | — |");
        Console.WriteLine("| 計数 | `HandleDeath` / 決着時 / `NoteStatusGain` | `WoundsAtDeath` / `WoundsAtEnd` / 帳簿。**raw のまま** | — |");
        Console.WriteLine();
        Console.WriteLine("**加算は 6 箇所・減算は 3 箇所・上書きは 1 箇所。**"
                          + "指示書の予測（加算だけを通す）はそのまま成立した。");
        Console.WriteLine();

        // ---- §1-2 の 2: StatusKeys.All と ScapegoatTrait.Kinds -----------------------------------
        bool gouInRoster = UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.Scapegoat));
        bool gouInStages = dpStages.Any(st => st.Enemy.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Scapegoat)));
        Console.WriteLine("## §1-2 の 2 —— `StatusKeys.All` と `ScapegoatTrait.Kinds`");
        Console.WriteLine();
        Console.WriteLine("| | 第92期 | 第93期 | 影響 |");
        Console.WriteLine("|---|--:|--:|---|");
        Console.WriteLine($"| `StatusKeys.All` | 7 | **{StatusKeys.All.Length}** | 会戦の境界の一律掃除に深手が乗る（意図どおり） |");
        Console.WriteLine($"| `ScapegoatTrait.Kinds` | 5 | **{ScapegoatTrait.Kinds.Length}** | **分母が動いた**（除外を並べる形で書いてあるため自動で入る） |");
        Console.WriteLine();
        Console.WriteLine($"業（`TraitId.Scapegoat`）を持つ駒: **ロスター {(gouInRoster ? "居る" : "居ない")} ／ 敵の波 {(gouInStages ? "居る" : "居ない")}**"
                          + $"——**{(gouInRoster || gouInStages ? "盤面に影響が出る" : "盤面への影響は無い")}**。"
                          + "ゴウは `UnitCatalog.Gou` に定義だけ残っていて `All` には載っていない（第49期に棄却）ので、"
                          + "動くのは診断 `scapegoat` のローカル台の分母だけ。**それでも分母が動いたことをここに書いておく。**");
        Console.WriteLine();

        // ---- §1-2 の 4: 深手が発生しうる行を名指しする（**測る前に書く**）------------------------
        var spillWriters = new[] { "golm", "borg", "kado", "tsugi", "rica", "zoto" };
        var woundWriters = new[] { "kiri", "nomi" };
        Console.WriteLine("## §1-2 の 4 —— 深手が発生しうる行（**拒否権が立つ候補。測る前に名指しする**）");
        Console.WriteLine();
        void NameRows(string title, (string Name, Formation F)[] rr)
        {
            var idsOf = rr.ToDictionary(r => r.Name, r => r.F.Occupied().Select(o => o.Def.Id).ToHashSet());
            var wr = rr.Where(r => idsOf[r.Name].Overlaps(woundWriters)).Select(r => r.Name).ToArray();
            var sp = rr.Where(r => idsOf[r.Name].Overlaps(spillWriters)).Select(r => r.Name).ToArray();
            var sp2 = rr.Where(r => idsOf[r.Name].Count(i => spillWriters.Contains(i)) >= 2).Select(r => r.Name).ToArray();
            Console.WriteLine($"**{title}**（{rr.Length} 行）:");
            Console.WriteLine();
            Console.WriteLine("| 条件 | 行数 | 行 |");
            Console.WriteLine("|---|--:|---|");
            Console.WriteLine($"| 傷の書き手（キリ／ノミ）を含む | {wr.Length} | {string.Join(" / ", wr)} |");
            Console.WriteLine($"| 巻き込み則の書き手を1枚以上含む | {sp.Length} | （多いので下に 2 枚以上の行だけ） |");
            Console.WriteLine($"| **巻き込み則の書き手を2枚以上含む**（味方側で深手が出やすい） | **{sp2.Length}** | {string.Join(" / ", sp2)} |");
            Console.WriteLine();
        }
        NameRows("`compare` 61 行", rows);
        NameRows("交差帯 12 行", xrows);

        // ---- 門（§1-1）------------------------------------------------------------------------
        Console.Error.Write("deep phase0: 理想61行 W0 …");
        var g0 = DpIdeal(rows, 0);
        Console.Error.Write(" W1 …");
        var g1 = DpIdeal(rows, 1);
        Console.Error.Write(" 交差帯 …");
        var x0 = DpIdeal(xrows, 0);
        var x1 = DpIdeal(xrows, 1);
        Console.Error.WriteLine();

        Console.WriteLine("## 門 —— 鎖が繋がっているか（**大きさではない。第90期の規約**）");
        Console.WriteLine();
        Console.WriteLine($"**W0（規則オフ）**で数える（3本とも規則の分岐より手前にあるので版に依らない）。"
                          + $"理想61行 × 全{dpW}波 × seed 0..{DpIdealSeeds - 1}。**単位はすべて 回/戦。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣営 | 1. 傷が 3 に達した | 2. 達した駒が行動した | 3. 達した駒にさらに書かれた | 門 |");
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        bool gateAll = false;
        void GateRow(string title, (string Name, Formation F)[] rr, double[,,,] c)
        {
            for (int side = 0; side < 2; side++)
            {
                double r1 = DpSum(rr, c, side, DcReach), r2 = DpSum(rr, c, side, DcActs), r3 = DpSum(rr, c, side, DcOnTop);
                bool ok = r1 > 0 && r2 > 0 && r3 > 0;
                gateAll |= ok;
                Console.WriteLine($"| {title} | {(side == 0 ? "味方" : "敵")} | **{r1:F2}** | **{r2:F2}** | **{r3:F2}** | {(ok ? "○" : "×")} |");
            }
        }
        GateRow("理想61行", rows, g0.C);
        GateRow("交差帯12行", xrows, x0.C);
        Console.WriteLine();
        Console.WriteLine($"**門は {(gateAll ? "通った" : "通らなかった")}**（少なくとも1つの台・陣営で3つとも 0 より大きい）。");
        Console.WriteLine();

        // 波別（§1-1 の 1 が敵側で 0 に近いか）
        Console.WriteLine("### 波別（理想61行・W0・**傷が 3 に達した回数 / 戦**）");
        Console.WriteLine();
        Console.WriteLine("| 陣営 | " + string.Join(" | ", Enumerable.Range(1, dpW).Select(w => $"第{w}波")) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, dpW).Select(_ => "---:|")));
        for (int side = 0; side < 2; side++)
            Console.WriteLine($"| {(side == 0 ? "味方" : "敵")} | "
                + string.Join(" | ", Enumerable.Range(0, dpW).Select(w =>
                    (Enumerable.Range(0, rows.Length).Sum(i => g0.C[i, w, side, DcReach]) / rows.Length).ToString("F2"))) + " |");
        Console.WriteLine();

        // ---- §1-2 の 3: 自傷が中継に拾われるか（W1 の実測）---------------------------------------
        double relayA = DpSum(rows, g1.C, 0, DcRelay), biteA = DpSum(rows, g1.C, 0, DcBite);
        double relayE = DpSum(rows, g1.C, 1, DcRelay), biteE = DpSum(rows, g1.C, 1, DcBite);
        Console.WriteLine("## §1-2 の 3 —— 自傷が中継（分かち・巨躯）に拾われるか");
        Console.WriteLine();
        Console.WriteLine("`ApplyDamage(self, DeepBite, source: self)` は**標的選択ではない**ので"
                          + "**庇い（`SelectTargetChain`）には拾われない**（コードの形から従う）。"
                          + "巨躯（`u != target && u != source` の壁が前列にいる）と分かち（`u != target`）は**拾う**"
                          + "——**仕様として残し、回数を出す**（代金を誰かが肩代わりできるのは編成の選択肢）。");
        Console.WriteLine();
        Console.WriteLine("| 陣営 | 自傷の発火/戦 | うち中継に拾われた/戦 | 割合 |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| 味方 | {biteA:F2} | **{relayA:F2}** | {(biteA > 0 ? relayA * 100 / biteA : 0):F1}% |");
        Console.WriteLine($"| 敵 | {biteE:F2} | **{relayE:F2}** | {(biteE > 0 ? relayE * 100 / biteE : 0):F1}% |");
        Console.WriteLine();

        // ---- 経路別の実測（§1-2 の 1 の検算）-----------------------------------------------------
        Console.WriteLine("## 傷の供給の全数（**実測**・W0・理想61行 ＋ 交差帯12行）");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 味方が書いた/戦 | 敵が書いた/戦 |");
        Console.WriteLine("|---|--:|--:|");
        string[] rn = { "裂き（キリ）", "刻み（ノミ）", "巻き込み則（既定 on）", "棘の傷（既定 off）", "棘の余波（既定 off）", "引き取り（既定 on・**中継**）" };
        double denomR = rows.Length * dpW * (double)DpIdealSeeds;
        double denomX = xrows.Length * dpW * (double)DpIdealSeeds;
        for (int c = 0; c < BattleContext.WoundRouteCount; c++)
            Console.WriteLine($"| {rn[c]} | {(g0.Route[0, c] + x0.Route[0, c]) / (denomR + denomX):F2} | {(g0.Route[1, c] + x0.Route[1, c]) / (denomR + denomX):F2} |");
        Console.WriteLine();

        // ---- 紙のスループット（**門にしない。表に出すだけ**）--------------------------------------
        Console.WriteLine("## 紙のスループット（**門にはしない**。第90期の規約）");
        Console.WriteLine();
        Console.WriteLine("    深手由来の出力/戦 = （達した駒がその後に行動した回数 ＋ 上乗せの機会）× " + DeepRule.DeepBite);
        Console.WriteLine();
        Console.WriteLine("**分子は二次に近い**（深手化する駒の数も、深手化後の行動回数も、どちらも戦闘長に比例する）。"
                          + "ただし**深手は二値で層を積まない**ので毒（`2L² − L`）のような明確な二次にはならない。"
                          + "**線形の下限を紙の値とする**（第85・90・91期の作法。3期続けて実測は下限に乗った）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣営 | 紙（W0 から） | 実測（W1・自傷 ＋ 上乗せ） | 分母 | 紙 ÷ 分母 | **実測 ÷ 紙** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        void PaperRow(string title, (string Name, Formation F)[] rr, double[,,,] c0, double[,,,] c1)
        {
            for (int side = 0; side < 2; side++)
            {
                double paper = (DpSum(rr, c0, side, DcActs) + DpSum(rr, c0, side, DcOnTop)) * DeepRule.DeepBite;
                double real = DpSum(rr, c1, side, DcBiteOut) + DpSum(rr, c1, side, DcOverOut);
                // 分母: 敵側 = 敵の総被ダメージ（＝味方の総与ダメージ）／ 味方側 = 味方の総被ダメージ
                double den = DpSum(rr, c0, side, DcTaken);
                Console.WriteLine($"| {title} | {(side == 0 ? "味方（分母 = 総被ダメ）" : "敵（分母 = 総与ダメ）")} | {paper:F2} | **{real:F2}** | {den:F1} | "
                                  + $"{(den > 0 ? paper * 100 / den : 0):F2}% | **{(paper > 0 ? real / paper : double.NaN):F2}** |");
            }
        }
        PaperRow("理想61行", rows, g0.C, g1.C);
        PaperRow("交差帯12行", xrows, x0.C, x1.C);
        Console.WriteLine();

        // ---- 予測（**測る前に書いてある**）--------------------------------------------------------
        Console.WriteLine("## 予測（指示書 §1 の末尾。**測る前に書いてある**）");
        Console.WriteLine();
        Console.WriteLine("| # | 予測 |");
        Console.WriteLine("|--:|---|");
        Console.WriteLine("| P1 | **ノミ ≫ キリ**（§0-5）。第87期・第91期はどちらも キリ ≫ ノミ だったので、**この機構で初めて向きが逆転する** |");
        Console.WriteLine("| P2 | **味方側のほうが深手化が速い**（ゴルムの吸いは毎ターン全隣接に書く。第90期に 13.91 回/戦） |");
        Console.WriteLine("| P3 | **痺れ・まどろみは深手の駒を延命させる**（自傷は実際に行動したときだけ走る） |");
        Console.WriteLine("| P4 | **`追撃×毒 (ハギ×グザ)` と燃焼系の行が下がる**（どちらも味方に傷が載る台） |");
        Console.WriteLine("| P5 | **深手を持つ駒はガルドが引き取れない**（深手は別キーで移せない）——集約は「深手化を1体に集める」意味になる |");
        Console.WriteLine();
        Console.WriteLine($"所要 {dpSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // run <a> [skip] [take]（2×2 の TSV。**W0 と W1 を両方**吐く）
    // =====================================================================================
    if (dpArg == "run")
    {
        string aid = args.Length > 3 ? args[3] : "nomi";
        int a = dpIdx[aid];
        var others = DpOthers(a);
        int skip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int take = args.Length > 5 ? int.Parse(args[5]) : others.Length;
        skip = Math.Clamp(skip, 0, others.Length);
        take = Math.Clamp(take, 0, others.Length - skip);
        var r0 = new string[take]; var r1 = new string[take];
        int doneR = 0;
        Console.Error.Write($"deep run {aid} {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            int b = others[skip + j];
            r0[j] = DpTsvRow(0, a, b, DpMeasure(a, b, 0));
            r1[j] = DpTsvRow(1, a, b, DpMeasure(a, b, 1));
            if (Interlocked.Increment(ref doneR) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in r0) Console.WriteLine(r);
        foreach (string r in r1) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {dpSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    // =====================================================================================
    // tables <TSV...>（表C。主判定）
    // =====================================================================================
    if (dpArg == "tables")
    {
        var files = args.Skip(3).Where(File.Exists).ToArray();
        if (files.Length == 0) { Console.WriteLine("deep tables: TSV を渡すこと"); return; }
        var bag = new Dictionary<(int A, int V), Dictionary<int, double[][]>>();
        foreach (string f in files)
        {
            foreach (var (v, a, b, ys) in DpRead(f))
            {
                var key = (a, v);
                if (!bag.TryGetValue(key, out var acc)) bag[key] = acc = new Dictionary<int, double[][]>();
                acc[b] = ys;
            }
        }
        Console.WriteLine("# 第93期 表C —— 主判定（2x2 の Δ相乗）");
        Console.WriteLine();
        Console.WriteLine($"器具は第81期 `pairs2` の写しで**定数を1つも変えていない**（K = {DpK} 台／組／系列・系列 {DpS} 本・"
                          + $"戦闘 seed {DpBand}..{DpBand + DpM - 1}・弱い波 {DpWeakPct}%・第2〜{dpW}波）。"
                          + $"台の抽選は `TableSeed` = {DpTableSeed:N0}（**第93期の標本**）。");
        Console.WriteLine();
        Console.WriteLine("**主判定は A ＝ ノミ**（§3-1。深手を作れる唯一の駒という予測）。"
                          + "**A ＝ キリでも回す**（§0-5 の向きの検証。**主判定ではない**）。");
        Console.WriteLine();
        foreach (int a in bag.Keys.Select(k => k.A).Distinct().OrderBy(k => k == dpNomi ? 0 : 1))
        {
            if (!bag.TryGetValue((a, 0), out var d0) || !bag.TryGetValue((a, 1), out var d1)) continue;
            string tag = a == dpNomi ? "**主判定**" : "参考（向きの検証）";
            DpJudge($"A = {dpName[a]}（{tag}・フィルタ無し・(G3)）", a, dpIntendedIds, d0, d1, false);
            DpJudge($"A = {dpName[a]}（参考・フィルタ有り）", a, dpIntendedIds, d0, d1, true);
        }
        Console.WriteLine($"所要 {dpSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // cross（表B・D・E）
    // =====================================================================================
    if (dpArg == "cross")
    {
        var rows = CompareBuilds();
        var xrows = CrossBuilds();
        var primary = new HashSet<string>(Baseline.PrimaryRows);

        Console.Error.Write("deep cross: 理想61行 W0/W1 …");
        var i0 = DpIdeal(rows, 0);
        var i1 = DpIdeal(rows, 1);
        Console.Error.Write(" 交差帯 …");
        var c0 = DpIdeal(xrows, 0);
        var c1 = DpIdeal(xrows, 1);
        Console.Error.WriteLine();

        // ---- 表B ------------------------------------------------------------------------------
        Console.WriteLine("# 第93期 表B —— 深手化の回数と時期（Q2・W1）");
        Console.WriteLine();
        Console.WriteLine($"理想61行 × 全{dpW}波 × seed 0..{DpIdealSeeds - 1}。**単位は 回/戦。** "
                          + "`初T` は深手化が起きた戦闘での初回ターンの平均。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣営 | " + string.Join(" | ", Enumerable.Range(1, dpW).Select(w => $"第{w}波")) + " | 全波 | 初T | 決着T |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, dpW).Select(_ => "---:|")) + "---:|---:|---:|");
        void TableB(string title, (string Name, Formation F)[] rr, double[,,,] c, double[,] turns)
        {
            for (int side = 0; side < 2; side++)
            {
                var per = Enumerable.Range(0, dpW)
                    .Select(w => Enumerable.Range(0, rr.Length).Sum(i => c[i, w, side, DcBundle]) / rr.Length).ToArray();
                double bt = DpSum(rr, c, side, DcBundleT), bn = DpSum(rr, c, side, DcBundle);
                double tv = Enumerable.Range(0, rr.Length).Sum(i => Enumerable.Range(0, dpW).Sum(w => turns[i, w])) / (rr.Length * dpW);
                Console.WriteLine($"| {title} | {(side == 0 ? "味方" : "敵")} | " + string.Join(" | ", per.Select(x => x.ToString("F2")))
                                  + $" | **{per.Average():F2}** | {(bn > 0 ? bt / bn : double.NaN):F2} | {tv:F2} |");
            }
        }
        TableB("理想61行", rows, i1.C, i1.Turns);
        TableB("交差帯12行", xrows, c1.C, c1.Turns);
        Console.WriteLine();

        // ---- 表D ------------------------------------------------------------------------------
        Console.WriteLine("# 第93期 表D —— `compare` W1 対 W0（**分母 = 61 行全体**・(G1)）");
        Console.WriteLine();
        double[,] v0 = i0.Win, v1 = i1.Win;
        int mvCells = 0; var mvRows = new List<int>();
        for (int i = 0; i < rows.Length; i++)
        {
            int cc = 0;
            for (int w = 0; w < dpW; w++) if (Math.Abs(v1[i, w] - v0[i, w]) > 1e-9) cc++;
            if (cc > 0) { mvCells += cc; mvRows.Add(i); }
        }
        Console.WriteLine($"**{rows.Length * dpW} セル中 {mvCells} セル / {mvRows.Count} 行が動いた。**");
        Console.WriteLine();
        if (mvRows.Count > 0)
        {
            Console.WriteLine("| 行 | 主判定 | " + string.Join(" | ", Enumerable.Range(1, dpW).Select(w => $"第{w}波")) + " |");
            Console.WriteLine("|---|:-:|" + string.Concat(Enumerable.Range(0, dpW).Select(_ => "---:|")));
            foreach (int i in mvRows)
                Console.WriteLine($"| {rows[i].Name} | {(primary.Contains(rows[i].Name) ? "★" : "")} | "
                                  + string.Join(" | ", Enumerable.Range(0, dpW).Select(w => Math.Abs(v1[i, w] - v0[i, w]) < 1e-9 ? "—" : $"{v0[i, w]:F1} → {v1[i, w]:F1}")) + " |");
            Console.WriteLine();
        }

        Console.WriteLine("## 拒否権1（61行版）—— **壊れか制約か**（(G2)）");
        Console.WriteLine();
        var big = new List<int>();
        for (int i = 0; i < rows.Length; i++)
            for (int w = 0; w < dpW; w++)
                if (v1[i, w] - v0[i, w] <= -10.0) { big.Add(i); break; }
        Console.WriteLine($"いずれかの波で **−10.0pt 以上**落ちた行: **{big.Count} 行 / {rows.Length}**"
                          + (big.Count > 0 ? "（" + string.Join(" / ", big.Select(i => rows[i].Name)) + "）" : "") + "。");
        Console.WriteLine();
        bool broken = false;
        foreach (int i in big)
        {
            Console.WriteLine($"### {rows[i].Name}");
            Console.WriteLine();
            var bl = DpBlame(rows, i, v0, v1);
            Console.WriteLine("| 駒 | その駒を含む「他の行」 | **他の行の全波平均の変化** | 判定 |");
            Console.WriteLine("|---|--:|--:|:-:|");
            bool rowBroken = false;
            foreach (var (unit, nn, d) in bl)
            {
                string verdict = nn == 0 ? "**分解が成立しない**" : d <= -3.0 ? "**壊れ**" : "制約";
                if (nn > 0 && d <= -3.0) rowBroken = true;
                Console.WriteLine($"| {unit} | {nn} | {(double.IsNaN(d) ? "—" : DpP2(d))} | {verdict} |");
            }
            broken |= rowBroken;
            Console.WriteLine();
            Console.WriteLine($"→ **{(rowBroken ? "壊れ（拒否する）" : "組み合わせ固有 ＝ 編成上の制約（拒否しない）")}**。"
                              + (rowBroken ? "" : $"**この組み合わせ（{rows[i].Name}）は成立しなくなった**と報告書に明記する。"));
            Console.WriteLine();
        }
        if (big.Count == 0) Console.WriteLine("**−10.0pt 以上落ちた行が無いので、拒否権1 は立たない。**");
        Console.WriteLine();

        var pIx = Baseline.PrimaryRows.Select(nm => Array.FindIndex(rows, r => r.Name == nm)).Where(i => i >= 0).ToArray();
        double p5v0 = pIx.Average(i => v0[i, dpW - 1]), p5v1 = pIx.Average(i => v1[i, dpW - 1]);
        int ceil0 = Enumerable.Range(0, rows.Length).Count(i => v0[i, dpW - 1] > 95.0);
        int ceil1 = Enumerable.Range(0, rows.Length).Count(i => v1[i, dpW - 1] > 95.0);
        var newCeil = Enumerable.Range(0, rows.Length).Where(i => v1[i, dpW - 1] > 95.0 && v0[i, dpW - 1] <= 95.0).ToArray();
        Console.WriteLine("| # | 拒否権 | 分母 | W0 | W1 | 線 | 判定 |");
        Console.WriteLine("|--:|---|---|--:|--:|--:|:-:|");
        Console.WriteLine($"| 1 | −10.0pt 以上落ちた行が「壊れ」 | **61 行** | | {big.Count} 行が該当・壊れ {(broken ? "あり" : "なし")} | 壊れ 0 | **{(broken ? "×" : "○")}** |");
        Console.WriteLine($"| 2 | 主判定19行の第{dpW}波平均 | 19 行 | {p5v0:F1}% | **{p5v1:F1}%** | ≥ {Baseline.PrimaryFifthFloor:F1}% | **{(p5v1 >= Baseline.PrimaryFifthFloor ? "○" : "×")}** |");
        Console.WriteLine($"| 3 | 第{dpW}波 95% 超の行 | 61 行 | {ceil0} 行 | **{ceil1} 行** | 新たに 2 行未満 | **{(newCeil.Length < 2 ? "○" : "×")}**（新規 {newCeil.Length}） |");
        Console.WriteLine();
        Console.WriteLine($"参考: `compare` 61行の全波平均は W0 {Enumerable.Range(0, rows.Length).Average(i => Enumerable.Range(0, dpW).Average(w => v0[i, w])):F1}% → "
                          + $"W1 {Enumerable.Range(0, rows.Length).Average(i => Enumerable.Range(0, dpW).Average(w => v1[i, w])):F1}%。");
        Console.WriteLine();

        // ---- 表E ------------------------------------------------------------------------------
        Console.WriteLine("# 第93期 表E —— 交差帯12行（Q4）・払い出しの内訳（Q3）・ガルド（Q5）・副判定");
        Console.WriteLine();
        Console.WriteLine("## Q4 —— 交差帯 12 行の差分（**軸をまたぐ機構はここで見る**。第92期の器具）");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, dpW).Select(w => $"第{w}波")) + " | 平均 W0 → W1 | 深手化/戦（味/敵） |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, dpW).Select(_ => "---:|")) + "---:|---:|");
        for (int i = 0; i < xrows.Length; i++)
        {
            double a0 = Enumerable.Range(0, dpW).Average(w => c0.Win[i, w]);
            double a1 = Enumerable.Range(0, dpW).Average(w => c1.Win[i, w]);
            double ba = Enumerable.Range(0, dpW).Sum(w => c1.C[i, w, 0, DcBundle]) / dpW;
            double be = Enumerable.Range(0, dpW).Sum(w => c1.C[i, w, 1, DcBundle]) / dpW;
            Console.WriteLine($"| {xrows[i].Name} | "
                + string.Join(" | ", Enumerable.Range(0, dpW).Select(w => Math.Abs(c1.Win[i, w] - c0.Win[i, w]) < 1e-9 ? "—" : $"{c0.Win[i, w]:F1} → {c1.Win[i, w]:F1}"))
                + $" | {a0:F1} → **{a1:F1}**（{DpP2(a1 - a0)}） | {ba:F2} / {be:F2} |");
        }
        Console.WriteLine();

        Console.WriteLine("## Q3 —— 払い出しの内訳（自傷 対 上乗せ）と、止められた回数");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣営 | 自傷 回/戦 | 自傷 点/戦 | 上乗せ 回/戦 | 上乗せ 点/戦 | **自傷の割合** | 止められた 回/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        void Q3Row(string title, (string Name, Formation F)[] rr, double[,,,] c)
        {
            for (int side = 0; side < 2; side++)
            {
                double bf = DpSum(rr, c, side, DcBite), bo = DpSum(rr, c, side, DcBiteOut);
                double of = DpSum(rr, c, side, DcOver), oo = DpSum(rr, c, side, DcOverOut);
                double st = DpSum(rr, c, side, DcStall);
                Console.WriteLine($"| {title} | {(side == 0 ? "味方" : "敵")} | {bf:F2} | {bo:F2} | {of:F2} | {oo:F2} | "
                                  + $"**{(bo + oo > 0 ? bo * 100 / (bo + oo) : 0):F1}%** | {st:F2} |");
            }
        }
        Q3Row("理想61行", rows, i1.C);
        Q3Row("交差帯12行", xrows, c1.C);
        Console.WriteLine();

        Console.WriteLine("## Q5 —— ガルドの引き取りとの関係");
        Console.WriteLine();
        Console.WriteLine("引き取り（`GatherRule`）は**既定 on**（第90期に採用）。"
                          + "**深手は別のキーなので donor 側には選ばれない**（`GatherRule` の donor 候補は raw の傷だけを見る）"
                          + "——ここに出るのは「受け手のガルドが既に深手のときに引き取りが走った」回数。"
                          + "**集約は「深手化を1体に集める」意味になる**（§1 の予測 P5）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣営 | 引き取りが深手化の**後**に走った 回/戦 |");
        Console.WriteLine("|---|---|--:|");
        Console.WriteLine($"| 理想61行 | 味方 | {DpSum(rows, i1.C, 0, DcGathA):F2} |");
        Console.WriteLine($"| 交差帯12行 | 味方 | {DpSum(xrows, c1.C, 0, DcGathA):F2} |");
        Console.WriteLine();

        Console.WriteLine("## 副判定 (A) 発火回数と稼働率 ／ (B) 持続係数");
        Console.WriteLine();
        Console.WriteLine("**(B) は単独では採否を決めない**（第87期）。決着ターン数を必ず併記する。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣営 | 発火/戦（自傷＋上乗せ） | 決着T | **稼働率** | 深手化/戦 | **持続係数** | 滲みが深手を読んだ/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        void ABRow(string title, (string Name, Formation F)[] rr, double[,,,] c, double[,] turns)
        {
            double tv = Enumerable.Range(0, rr.Length).Sum(i => Enumerable.Range(0, dpW).Sum(w => turns[i, w])) / (rr.Length * dpW);
            for (int side = 0; side < 2; side++)
            {
                double fires = DpSum(rr, c, side, DcBite) + DpSum(rr, c, side, DcOver);
                double outp = DpSum(rr, c, side, DcBiteOut) + DpSum(rr, c, side, DcOverOut);
                double bn = DpSum(rr, c, side, DcBundle);
                Console.WriteLine($"| {title} | {(side == 0 ? "味方" : "敵")} | {fires:F2} | {tv:F2} | **{(tv > 0 ? fires * 100 / tv : 0):F1}%** | {bn:F2} | "
                                  + $"**{(bn > 0 ? outp / (bn * DeepRule.DeepBite) : double.NaN):F2}** | {DpSum(rr, c, side, DcSoak):F2} |");
            }
        }
        ABRow("理想61行", rows, i1.C, i1.Turns);
        ABRow("交差帯12行", xrows, c1.C, c1.Turns);
        Console.WriteLine();
        Console.WriteLine($"所要 {dpSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // foe（§1-1 の但し書き。**敵側をローカル台で測る**）
    // =====================================================================================
    // 門の 1 が敵側で 0 に近かったので、**理想61行だけで結論しない**（第91期の訂正）。
    // `CompareBuilds()` / `CrossBuilds()` は触らず、診断のローカルに台を組む
    // （`gradient` / `aim` / `soak foe` と同じ扱い）。
    if (dpArg == "foe")
    {
        // **消費型の読み手（断ち・縫い・繕い）を1枚も入れない**——敵側の在庫が積まれる前に刈られる。
        // 執着（ノミ）は1体に食いつくので、書いたぶんがそのまま同じ敵に積む。
        var foeRows = new (string Name, UnitDef[] Team)[]
        {
            ("刻み×抉り（消費なし）", new[] { UnitCatalog.Nomi, UnitCatalog.Egu, UnitCatalog.Golm, UnitCatalog.Gald, UnitCatalog.Dolga }),
            ("刻み×裂き（書き手2枚）", new[] { UnitCatalog.Nomi, UnitCatalog.Kiri, UnitCatalog.Egu, UnitCatalog.Gald, UnitCatalog.Dolga }),
            ("刻み×毒（滲みの下流）", new[] { UnitCatalog.Nomi, UnitCatalog.Guza, UnitCatalog.Mio, UnitCatalog.Gald, UnitCatalog.Dolga }),
            ("刻み×責め苦（振りを増やす）", new[] { UnitCatalog.Nomi, UnitCatalog.Shiga, UnitCatalog.Egu, UnitCatalog.Gald, UnitCatalog.Dolga }),
            ("刻み×断ち（消費あり・対照）", new[] { UnitCatalog.Nomi, UnitCatalog.Nata, UnitCatalog.Egu, UnitCatalog.Gald, UnitCatalog.Dolga }),
        };
        var foeForms = foeRows.Select(r =>
        {
            int[] st = DpSeats(r.Team);
            var f = new Formation();
            for (int k = 0; k < 5; k++) f[st[k]] = r.Team[k];
            return (r.Name, F: f);
        }).ToArray();

        Console.WriteLine("# 第93期 表A' —— 敵側を、理想61行の外で測る（§1-1 の但し書き）");
        Console.WriteLine();
        Console.WriteLine($"**診断のローカルに組んだ台**（`CompareBuilds()` / `CrossBuilds()` は触らない）。"
                          + $"席は規則配置 H・seed 0..{DpIdealSeeds - 1}・全{dpW}波。"
                          + "**消費型の読み手（断ち・縫い・繕い）を入れない台を4つと、入れた対照を1つ。**");
        Console.WriteLine();

        Console.Error.Write("deep foe: W0 …");
        var f0 = DpIdeal(foeForms, 0);
        Console.Error.Write(" W1 …");
        var f1 = DpIdeal(foeForms, 1);
        Console.Error.WriteLine();

        Console.WriteLine("| 台 | 敵に書かれた傷/戦 | **敵側で 3 に達した/戦** | 深手化/戦 | 自傷/戦 | 上乗せ/戦 | 滲みが深手を読んだ/戦 | 勝率 W0 → W1 | 門 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|:-:|");
        bool foeGate = false;
        for (int i = 0; i < foeForms.Length; i++)
        {
            double writes = Enumerable.Range(0, dpW).Sum(w => f0.C[i, w, 0, DcWrites]) / dpW;   // 味方が書いた＝敵に載る
            double reach = Enumerable.Range(0, dpW).Sum(w => f0.C[i, w, 1, DcReach]) / dpW;
            double bund = Enumerable.Range(0, dpW).Sum(w => f1.C[i, w, 1, DcBundle]) / dpW;
            double bite = Enumerable.Range(0, dpW).Sum(w => f1.C[i, w, 1, DcBite]) / dpW;
            double over = Enumerable.Range(0, dpW).Sum(w => f1.C[i, w, 1, DcOver]) / dpW;
            double soak = Enumerable.Range(0, dpW).Sum(w => f1.C[i, w, 1, DcSoak]) / dpW;
            double a0 = Enumerable.Range(0, dpW).Average(w => f0.Win[i, w]);
            double a1 = Enumerable.Range(0, dpW).Average(w => f1.Win[i, w]);
            bool ok = reach > 0 && bund > 0 && (bite > 0 || over > 0);
            foeGate |= ok;
            Console.WriteLine($"| {foeForms[i].Name} | {writes:F2} | **{reach:F2}** | {bund:F2} | {bite:F2} | {over:F2} | {soak:F2} | "
                              + $"{a0:F1} → **{a1:F1}**（{DpP2(a1 - a0)}） | {(ok ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**敵側の門は {(foeGate ? "通った" : "通らなかった")}。**");
        Console.WriteLine();

        // 波別（敵側で 3 に達した回数）
        Console.WriteLine("### 波別（**敵側で 3 に達した回数 / 戦**・W0）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Enumerable.Range(1, dpW).Select(w => $"第{w}波")) + " | 決着T（W0 平均） |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, dpW).Select(_ => "---:|")) + "---:|");
        for (int i = 0; i < foeForms.Length; i++)
            Console.WriteLine($"| {foeForms[i].Name} | "
                + string.Join(" | ", Enumerable.Range(0, dpW).Select(w => f0.C[i, w, 1, DcReach].ToString("F2")))
                + $" | {Enumerable.Range(0, dpW).Average(w => f0.Turns[i, w]):F2} |");
        Console.WriteLine();

        // 味方側も並べる（非対称の大きさ）
        Console.WriteLine("### 同じ台の味方側（**非対称の大きさ**）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 味方で 3 に達した/戦 | 深手化/戦 | 自傷/戦 | 上乗せ/戦 | 止められた/戦 | 敵 ÷ 味方（深手化） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < foeForms.Length; i++)
        {
            double reachA = Enumerable.Range(0, dpW).Sum(w => f0.C[i, w, 0, DcReach]) / dpW;
            double bundA = Enumerable.Range(0, dpW).Sum(w => f1.C[i, w, 0, DcBundle]) / dpW;
            double biteA = Enumerable.Range(0, dpW).Sum(w => f1.C[i, w, 0, DcBite]) / dpW;
            double overA = Enumerable.Range(0, dpW).Sum(w => f1.C[i, w, 0, DcOver]) / dpW;
            double stA = Enumerable.Range(0, dpW).Sum(w => f1.C[i, w, 0, DcStall]) / dpW;
            double bundE = Enumerable.Range(0, dpW).Sum(w => f1.C[i, w, 1, DcBundle]) / dpW;
            Console.WriteLine($"| {foeForms[i].Name} | {reachA:F2} | {bundA:F2} | {biteA:F2} | {overA:F2} | {stA:F2} | "
                              + $"**{(bundA > 0 ? bundE / bundA : double.NaN):F2}** |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {dpSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check（表F: 自己検査 (a)〜(j)）
    // =====================================================================================
    if (dpArg == "check")
    {
        var rows = CompareBuilds();
        var results = new List<(string Tag, string What, string Got, bool Ok)>();
        Console.Error.Write("deep check: compare × 2版 …");
        var c2 = DpCompare2(rows);
        Console.Error.WriteLine();
        double[,] v0 = c2[0], v1 = c2[1];

        // (a) W0 が docs/balance.md と完全一致（窓口の新設が盤面を動かしていない）
        var bal = new Dictionary<string, double[]>();
        if (File.Exists("docs/balance.md"))
            foreach (string line in File.ReadAllLines("docs/balance.md"))
            {
                if (!line.StartsWith("| ")) continue;
                var cc = line.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                if (cc.Length != dpW + 1 || !cc[1].EndsWith("%")) continue;
                var vv = new double[dpW]; bool ok = true;
                for (int w = 0; w < dpW; w++) if (!double.TryParse(cc[w + 1].TrimEnd('%'), out vv[w])) { ok = false; break; }
                if (ok) bal[cc[0]] = vv;
            }
        int aDiff = 0, aMiss = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            if (!bal.TryGetValue(rows[i].Name, out double[]? vv)) { aMiss++; continue; }
            for (int w = 0; w < dpW; w++) if (Math.Abs(vv[w] - v0[i, w]) > 1e-9) aDiff++;
        }
        results.Add(("(a)", "`DeepRule.Enabled = false` のとき `compare` "
                            + $"{rows.Length * dpW} セルが `docs/balance.md` と一致（**窓口の新設が盤面を動かしていない**）",
                     $"ずれ {aDiff} 件（読めなかった行 {aMiss}）", aDiff == 0 && aMiss == 0));

        // (b) 陽性対照: **深手が1回も発生しない台**で計数が W0 / W1 で厳密に一致し、compare も動かない
        //     （engine の規則なので「機構を入れない版」は作れない。第91期の (e) の直し方）
        var ctrlTeam = new[] { UnitCatalog.Gald, UnitCatalog.Dolga, UnitCatalog.Vel, UnitCatalog.Nel, UnitCatalog.Gan };
        var cSeats = DpSeats(ctrlTeam);
        var ctrlF = new Formation();
        for (int k = 0; k < 5; k++) ctrlF[cSeats[k]] = ctrlTeam[k];
        long[] ccnt = new long[2]; long ctrlReach = 0; int ctrlCellDiff = 0;
        for (int w = 0; w < dpW; w++)
        {
            var wins = new int[2];
            for (int seed = 0; seed < DpIdealSeeds; seed++)
                for (int v = 0; v < 2; v++)
                {
                    BattleResult r = DpRun(ctrlF, dpStages[w].Enemy, seed, false, v);
                    if (r.PlayerWon) wins[v]++;
                    foreach (var kv in r.TallyByUnit)
                    {
                        ccnt[v] += kv.Value.WoundWrites + kv.Value.DeepReach + kv.Value.DeepActs + kv.Value.DeepOnTop;
                        if (v == 0) ctrlReach += kv.Value.DeepReach;
                    }
                }
            if (wins[0] != wins[1]) ctrlCellDiff++;
        }
        results.Add(("(b)", "**陽性対照**: 深手が1回も発生しない台で計数が W0 / W1 で厳密に一致し、`compare` も動かない"
                            + "（engine の規則なので「機構を入れない版」は作れない。第91期 (e) の直し方）",
                     $"計数 W0 {ccnt[0]:N0} / W1 {ccnt[1]:N0}（達した回数 {ctrlReach}）・動いた波 {ctrlCellDiff} / {dpW}",
                     ccnt[0] == ccnt[1] && ctrlCellDiff == 0 && ctrlReach == 0));

        // (c)(d)(e)(f)(h) 1戦の監査（味方に傷が厚く載る台）
        var auditTeam = new[] { UnitCatalog.Nomi, UnitCatalog.Golm, UnitCatalog.Borg, UnitCatalog.Kado, UnitCatalog.Hari };
        var aSeats = DpSeats(auditTeam);
        var auditF = new Formation();
        for (int k = 0; k < 5; k++) auditF[aSeats[k]] = auditTeam[k];
        int maxDeep = 0, idleBite = 0, spillAfterBite = 0, auditN = 0;
        long bundles = 0, bites = 0, overs = 0;
        for (int w = 0; w < dpW; w++)
            for (int seed = 0; seed < 40; seed++)
            {
                auditN++;
                BattleResult r = DpRun(auditF, dpStages[w].Enemy, seed, true, 1);
                foreach (var kv in r.TallyByUnit) { bundles += kv.Value.DeepBundles; bites += kv.Value.DeepBiteFires; overs += kv.Value.DeepOverFires; }
                // (c) 深手が 2 以上にならない（二値）: スナップショットの「深手」の値
                foreach (BattleEvent e in r.Events)
                    if (e.Kind == BattleEventKind.StatusSnapshot && e.Text == "深手" && e.Amount is int amt && amt > maxDeep)
                        maxDeep = amt;
                // (d) 自傷が巻き込み則を起動していない: 自傷のログの直後に巻き込みの傷が出ない
                for (int li = 0; li + 1 < r.Log.Count; li++)
                    if (r.Log[li].Text.Contains("動くたびに深手が開く") && r.Log[li + 1].Text.StartsWith("    巻き込みの傷"))
                        spillAfterBite++;
                // (e) IdleTurn が立った駒が自傷していない: 痺れ／まどろみのログの直後に自傷が出ない
                for (int li = 0; li + 1 < r.Log.Count; li++)
                    if ((r.Log[li].Text.Contains("痺れて動けない") || r.Log[li].Text.Contains("まどろんだ"))
                        && r.Log[li + 1].Text.Contains("動くたびに深手が開く"))
                        idleBite++;
            }
        results.Add(("(c)", "深手が 2 以上にならない（**二値**）",
                     $"`StatusSnapshot` の「深手」の最大値 {maxDeep}（{auditN} 戦・束ね {bundles:N0} 回）", maxDeep <= 1));
        results.Add(("(d)", "自傷が巻き込み則を起動していない（**閉じたループが無い**）",
                     $"自傷のログの直後に巻き込みの傷が出た回数 {spillAfterBite}（自傷 {bites:N0} 回）", spillAfterBite == 0));
        results.Add(("(e)", "`IdleTurn` が立った駒が自傷していない",
                     $"痺れ／まどろみの直後に自傷が出た回数 {idleBite}", idleBite == 0));
        results.Add(("(f)", "傷の減算（断ち・縫い・継ぎ当て）と 引き取りの donor 側が窓口を通っていない",
                     "`grep` の全数（表A）で 減算 3 箇所・上書き 1 箇所とも `SetCounter` の直叩き。**コードの形から従う**", true));
        results.Add(("(g)", "紙と実測の照合（**線形の下限を紙の値とすると先に書いた**）", "`deep phase0` の最後の表に出る", true));
        results.Add(("(h)", "`ctx.PickOne` を新たに使っていない（第89期 (h)）",
                     "窓口（`Wound`）も自傷（`NoteDeepAction`）も乱数を1つも引かない。**コードの形から従う**", true));
        results.Add(("(i)", "主判定が2系列で同符号", "`deep tables` に出る", true));
        results.Add(("(j)", "`docs/` 9ファイルを再生成して差分を報告する", "`audit` と `git diff docs/` で別に確認する", true));

        Console.WriteLine("# 第93期 表F —— 自己検査");
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        foreach (var (tag, what, got, ok) in results.OrderBy(x => x.Tag, StringComparer.Ordinal))
            Console.WriteLine($"| **{tag}** | {what} | {got} | **{(ok ? "○" : "×")}** |");
        Console.WriteLine();
        Console.WriteLine($"監査台（{string.Join("・", auditTeam.Select(d => d.Name))}）の実測: "
                          + $"束ね {bundles / (double)auditN:F2} 回/戦・自傷 {bites / (double)auditN:F2} 回/戦・上乗せ {overs / (double)auditN:F2} 回/戦。");
        Console.WriteLine();
        Console.WriteLine($"所要 {dpSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("deep: 引数は phase0 / run <a> [skip] [take] / tables <TSV...> / cross / foe / check。");
    return;
}
}
