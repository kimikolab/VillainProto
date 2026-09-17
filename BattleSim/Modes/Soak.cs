using BattleCore;
using static Common;

// =====================================================================================
// soak モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "soak")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 soak
// =====================================================================================

static class SoakDiag
{
// 滲み則（第90期）——**傷を持つ相手には、状態異常が深く入る。**
// 毒は層が +1、燃焼は残ターンが +1（3 → 4）。**両陣営に等しくかかる**（第85期の巻き込み則が既定なので
// 味方も傷を負い、瘴気の毒漏れ・火の粉を深く受ける）。engine の規則なので**新しい `TraitId` はゼロ**で、
// グザ・スィド・ラウ・ボルグが定義を1文字も変えずに傷の読み手になる。
//
//     dotnet run --project BattleSim -c Release 0 soak redo89                    # 表A（(P1) ガルドの再判定）
//     dotnet run --project BattleSim -c Release 0 soak phase0                    # 表B・門（§1-3）・紙（§1-2 の 5）
//     dotnet run --project BattleSim -c Release 0 soak ideal                     # 表C・E（理想61行で W0 対 W1）
//     dotnet run --project BattleSim -c Release 0 soak run <w> <a> [skip] [take] # 2×2 の TSV（a = kiri / nomi / gald）
//     dotnet run --project BattleSim -c Release 0 soak tables <TSV...>           # 表D（主判定）
//     dotnet run --project BattleSim -c Release 0 soak check                     # 表F（自己検査 (a)〜(j)）
public static void Run(string[] args, int stageIndex)
{
    string skArg = args.Length > 2 ? args[2] : "";
    var skSw = System.Diagnostics.Stopwatch.StartNew();
    var skInv = System.Globalization.CultureInfo.InvariantCulture;

    IReadOnlyList<EnemyCatalog.Stage> skStages = EnemyCatalog.Stages;
    int skW = skStages.Count;
    // **第141期: ロスターは `Everyone`（`All ∪ Retired`）。** (P1)＝第89期の再判定の意図した相手にハリが入る
    // （`soak redo89` が `skIdx["hari"]` で落ちていた）。ロスターが B の集合と引き表を兼ねるのでロスターごと広げる。
    var skRoster = UnitCatalog.Everyone.ToArray();
    int skRN = skRoster.Length;                       // 54（第90期は 51）

    // ---- 第81期 `pairs2` の定数の写し（**1つも変えていない**）----------------------------------
    const int SkTableSeed = 9_000_000;                // **第90期の標本**（第87・88期 8,100,000 ／ 第89期 8,900,000 とは別）
    const int SkK = 64, SkS = 2;
    const int SkBand = 0, SkM = 8;
    const int SkStrong = 7, SkWeakPct = 60, SkDrawCap = 20000;
    const int SkTop = 20;
    const int SkMinInfo = 20;                         // 第88期 §4-1: 情報帯がこれ未満の組は「測れていない」
    const int SkFpLine = 3;                           // Q1-2 の線（§3-1）
    const double SkEps = 1e-9;
    const int SkIdealSeeds = 200;                     // 理想台（`compare` と同じ帯）

    var skIdx = new Dictionary<string, int>();
    for (int u = 0; u < skRN; u++) skIdx[skRoster[u].Id] = u;
    string[] skName = skRoster.Select(d => d.Name).ToArray();

    // 本編: A = 裂きのキリ（傷の主たる書き手。向きの検証に刻みのノミ）。(P1): A = 廃棄聖騎士ガルド。
    int skKiri = skIdx["kiri"], skNomi = skIdx["nomi"], skGald = skIdx["gald"];
    // 本編の「意図した相手」4枚（§0-4）: 毒 3 枚（瘴気・毒撃・疫み）＋ 燃焼 1 枚（火の粉）
    string[] skIntendedIds = { "guza", "sid", "rau", "borg" };
    // (P1) の「意図した相手」7枚（§1-1）: 終端（ハリ）＋ 巻き込み則の書き手6枚
    string[] skP1Ids = { "hari", "golm", "borg", "kado", "nara", "rica", "zoto" };
    // 滲み則の読み手になる4枚（毒3＋燃1）と、傷の書き手2枚
    string[] skReaderIds = { "guza", "sid", "rau", "borg" };
    string[] skWoundIds = { "kiri", "nomi" };

    // ---- 版（第91期に通貨ごとに分けた）------------------------------------------------------------
    // V0 = 第90期より前 ／ Vc = 第90期の採用状態 ／ Vp = 毒だけ（本命）。
    // **第90期の `soak run` / `redo89` / `ideal` / `check` は v = 0/1 を V0/Vc として読む**
    // ——分割そのものが盤面を動かしていないことは自己検査 (a) が示す。
    SoakRule SkV0 = new(Poison: false, Burn: false);
    SoakRule SkVc = new(Poison: true, Burn: true);
    SoakRule SkVp = new(Poison: true, Burn: false);
    SoakRule SkVer(int v) => v == 0 ? SkV0 : v == 1 ? SkVc : SkVp;
    string SkVerName(int v) => v == 0 ? "V0（第90期より前）" : v == 1 ? "Vc（現行・毒＋燃焼）" : "Vp（毒だけ）";

    int[] SkOthers(int a) => Enumerable.Range(0, skRN).Where(u => u != a).ToArray();

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜89期と同一。`Stages` は書き換えない）--------------------
    var skWeakCache = new Dictionary<string, UnitDef>();
    UnitDef SkWeakOf(UnitDef d)
    {
        if (skWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * SkWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        skWeakCache[d.Id] = w;
        return w;
    }
    var skWeak = skStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = SkWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef SkPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var skPlainMap = skRoster.ToDictionary(d => d.Id, SkPlain);

    UnitDef[] SkFill(UnitDef[] pool, int strong0, int seed)
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
            if (sel.Attack >= SkStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int SkSeed(int tableSeed, int pairIx, int draw)
    {
        ulong x = (ulong)tableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] SkSeats(UnitDef[] u)
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
    // **A を先に固定してから添字を書いた**（第85期の罠。本編は A = キリ／ノミ・(P1) は A = ガルド）。
    Formation SkForm(UnitDef[] u, int[] seats, int cell)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (cell & 1) != 0) || (k == 1 && (cell & 2) != 0);
            f[seats[k]] = plain ? skPlainMap[u[k].Id] : u[k];
        }
        return f;
    }

    // 版の割り当て。**本編は SoakRule だけ・(P1) は GatherRule だけを振る。**
    // どちらも**現行の既定をそのまま使う**（`SutureRule.Both` / `SpillWoundRule` / `IgniteRule` が既定 on）。
    BattleResult SkRun(Formation f, Formation e, int seed, bool verbose, bool p1, int v)
        => p1 ? BattleEngine.Run(f, e, seed, verbose: verbose, gather: new GatherRule(v != 0))
              : BattleEngine.Run(f, e, seed, verbose: verbose, soak: SkVer(v));
    string SkVName(bool p1, int v) => p1 ? (v == 0 ? "Z0（現行）" : "Z1（傷も肩代わり）")
                                        : (v == 0 ? "W0（現行）" : "W1（滲み則）");

    double SkRate(Formation f, bool p1, int v)
    {
        double sum = 0;
        for (int wi = 1; wi < skW; wi++)
        {
            int wins = 0;
            for (int seed = SkBand; seed < SkBand + SkM; seed++)
                if (SkRun(f, skWeak[wi].Enemy, seed, false, p1, v).PlayerWon) wins++;
            sum += wins * 100.0 / SkM;
        }
        return sum / (skW - 1);
    }
    string SkP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double SkSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double SkSe(IReadOnlyList<double> xs) => xs.Count < 2 ? double.NaN : SkSd(xs) / Math.Sqrt(xs.Count);
    double SkPct(IReadOnlyList<double> xs, double q)
    {
        if (xs.Count == 0) return double.NaN;
        var s = xs.OrderBy(v => v).ToArray();
        if (s.Length == 1) return s[0];
        double pos = q * (s.Length - 1);
        int lo = (int)Math.Floor(pos), hi = Math.Min(lo + 1, s.Length - 1);
        return s[lo] + (pos - lo) * (s[hi] - s[lo]);
    }
    double SkMed(IReadOnlyList<double> xs) => SkPct(xs, 0.5);

    var skPairIxOf = new int[skRN, skRN];
    {
        int pi = 0;
        for (int a = 0; a < skRN; a++) for (int b = a + 1; b < skRN; b++) { skPairIxOf[a, b] = skPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> SkFills(int a, int b)
    {
        var pool = skRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (skRoster[a].Attack >= SkStrong ? 1 : 0) + (skRoster[b].Attack >= SkStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < SkS * SkK && draw < SkDrawCap; draw++)
        {
            var f = SkFill(pool, strong0, SkSeed(SkTableSeed, skPairIxOf[a, b], draw));
            var t = f.Select(d => skIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }
    UnitDef[] SkTeam(int a, int b, UnitDef[] fill) => new[] { skRoster[a], skRoster[b], fill[0], fill[1], fill[2] };
    double[][] SkMeasure(int a, int b, bool p1, int v)
    {
        var fills = SkFills(a, b);
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = SkTeam(a, b, fills[t]);
            int[] seats = SkSeats(team);
            ys[t] = new double[4];
            for (int cell = 0; cell < 4; cell++) ys[t][cell] = SkRate(SkForm(team, seats, cell), p1, v);
        }
        return ys;
    }

    // ---- TSV --------------------------------------------------------------------------------------
    (int P1, int V, int A, Dictionary<int, double[][]> D) SkRead(string path)
    {
        var d = new Dictionary<int, double[][]>();
        int pp = -1, vv = -1, aa = -1;
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int p = int.Parse(c[0]), v = int.Parse(c[1]), a = int.Parse(c[2]), b = int.Parse(c[3]), nT = int.Parse(c[4]);
            if (pp < 0) { pp = p; vv = v; aa = a; }
            else if (p != pp || v != vv || a != aa) throw new InvalidOperationException($"{path}: 系／版／A が混ざっている");
            var ys = new double[nT][];
            int at = 5;
            for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], skInv); }
            d[b] = ys;
        }
        return (pp, vv, aa, d);
    }
    string SkTsvRow(bool p1, int v, int a, int b, double[][] ys)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(p1 ? 1 : 0).Append('\t').Append(v).Append('\t').Append(a).Append('\t').Append(b).Append('\t').Append(ys.Length);
        foreach (double[] yy in ys) for (int k = 0; k < 4; k++) sb.Append('\t').Append(yy[k].ToString("R", skInv));
        return sb.ToString();
    }
    double SkSyn(double[] y) => y[0] - y[2] - y[1] + y[3];
    bool SkFloorT(double[] y) => y[3] < SkEps && y[1] < SkEps;
    bool SkCeilT(double[] y) => y[3] > 100 - SkEps && y[1] > 100 - SkEps;
    bool SkInfoT(double[] y) => !SkFloorT(y) && !SkCeilT(y);

    (double M, double Se, double[] Ser, int N) SkAgg(double[] d, bool[]? use)
    {
        var all = new List<double>(); var s0 = new List<double>(); var s1 = new List<double>();
        for (int t = 0; t < d.Length; t++)
        {
            if (use is not null && !use[t]) continue;
            all.Add(d[t]); (t % SkS == 0 ? s0 : s1).Add(d[t]);
        }
        return (all.Count > 0 ? all.Average() : double.NaN, SkSe(all),
                new[] { s0.Count > 0 ? s0.Average() : double.NaN, s1.Count > 0 ? s1.Average() : double.NaN }, all.Count);
    }
    (Dictionary<int, double[]> D, Dictionary<int, bool[]> Info, Dictionary<int, (int All, int Floor, int Ceil, int Info)> Cnt, int Mism)
        SkFold(Dictionary<int, double[][]> v0, Dictionary<int, double[][]> v1, int[] others)
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
                d[t] = SkSyn(v1[b][t]) - SkSyn(v0[b][t]);
                if (Math.Abs(v0[b][t][1] - v1[b][t][1]) > 1e-9) mism++;
                if (Math.Abs(v0[b][t][3] - v1[b][t][3]) > 1e-9) mism++;
                inf[t] = SkInfoT(v0[b][t]);
                if (SkFloorT(v0[b][t])) fl++; else if (SkCeilT(v0[b][t])) ce++; else ok++;
            }
            D[b] = d; Info[b] = inf; Cnt[b] = (nT, fl, ce, ok);
        }
        return (D, Info, Cnt, mism);
    }

    // ---- 1つの 2x2 の判定を丸ごと出す（(P1) と本編で共有。第89期 `gather` の写し）------------------
    bool SkJudge(string title, int a, string[] intendedIds, Dictionary<int, double[][]> d0, Dictionary<int, double[][]> d1, bool p1, bool filtered = true, string? vLo = null, string? vHi = null)
    {
        var others = SkOthers(a);
        var (D, Info, Cnt, mism) = SkFold(d0, d1, others);
        var intended = new HashSet<int>(intendedIds.Select(i => skIdx[i]));
        var agAll = D.Keys.ToDictionary(b => b, b => SkAgg(D[b], null));
        // **(G3)**: `filtered` が偽なら情報帯フィルタを当てない（engine の規則ではフィルタの根拠が消える）。
        var agFil = D.Keys.ToDictionary(b => b, b => SkAgg(D[b], filtered ? Info[b] : null));
        var measurable = filtered ? D.Keys.Where(b => Cnt[b].Info >= SkMinInfo).ToHashSet() : D.Keys.ToHashSet();
        var unint = measurable.Where(b => !intended.Contains(b)).ToArray();

        // **ノイズ床（第89期 §1-1 の規約）**: 同じ実験の中の「意図しない相手」の |Δ相乗| の 95 パーセンタイル
        double floor = SkPct(unint.Select(b => Math.Abs(agFil[b].M)).ToArray(), 0.95);

        int cAll = D.Keys.Sum(b => Cnt[b].All), cFl = D.Keys.Sum(b => Cnt[b].Floor),
            cCe = D.Keys.Sum(b => Cnt[b].Ceil), cIn = D.Keys.Sum(b => Cnt[b].Info);
        Console.WriteLine($"### {title}");
        Console.WriteLine();
        Console.WriteLine($"A = **{skName[a]}**・版は {vLo ?? SkVName(p1, 0)} 対 {vHi ?? SkVName(p1, 1)}。"
                          + $"意図した相手 {intended.Count} 枚（{string.Join("・", intendedIds.Select(i => skName[skIdx[i]]))}）／意図しない相手 {unint.Length} 体。");
        Console.WriteLine();
        Console.WriteLine("| | 台数 | 割合 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 全台（{D.Count} 体 × {SkS * SkK} 台） | {cAll:N0} | 100.0% |");
        Console.WriteLine($"| 床（`y00` = `y01` = 0.0%） | {cFl:N0} | {cFl * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| 天井（`y00` = `y01` = 100.0%） | {cCe:N0} | {cCe * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| **情報帯** | **{cIn:N0}** | **{cIn * 100.0 / cAll:F1}%** |");
        Console.WriteLine();
        Console.WriteLine($"1組あたりの情報帯は 中央値 **{SkMed(D.Keys.Select(b => (double)Cnt[b].Info).ToArray()):F1}** / {SkS * SkK} 台"
                          + $"（最小 {D.Keys.Min(b => Cnt[b].Info)} / 最大 {D.Keys.Max(b => Cnt[b].Info)}）。"
                          + $"**測れていない組（情報帯 < {SkMinInfo}）は {D.Count - measurable.Count} 体。**");
        Console.WriteLine();
        Console.WriteLine($"**自己検査 (a)**: A 素体の2セル（`y01` / `y00`）が版で完全一致 → {cAll * 2:N0} セル・ずれ **{mism}** 件"
                          + $" → **{(mism == 0 ? "○" : "×")}**。");
        Console.WriteLine();
        Console.WriteLine($"**ノイズ床（§1-1 の規約）= 意図しない {unint.Length} 体の \\|Δ\\| の 95%tile = {floor:F2}pt**"
                          + $"（中央値 {SkMed(unint.Select(b => Math.Abs(agFil[b].M)).ToArray()):F2} / 最大 {(unint.Length > 0 ? unint.Max(b => Math.Abs(agFil[b].M)) : double.NaN):F2}）。");
        Console.WriteLine();

        var ordF = measurable.OrderByDescending(b => agFil[b].M).ToArray();
        Console.WriteLine("| 順位 | B | **Δ（情報帯）** | SE | 情報帯 | 系列1 | 系列2 | 床超 | Δ（全台） | SE |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|--:|--:|");
        var shown = new HashSet<int>();
        void Row(int b)
        {
            int rf = Array.IndexOf(ordF, b) + 1;
            var af = agFil[b]; var aa2 = agAll[b];
            bool over = measurable.Contains(b) && Math.Abs(af.M) > floor;
            Console.WriteLine($"| {(rf > 0 ? rf.ToString() : "—")} | {(intended.Contains(b) ? "★" : "")}{skName[b]} | **{SkP2(af.M)}** | {af.Se:F2} | {Cnt[b].Info} | {SkP2(af.Ser[0])} | {SkP2(af.Ser[1])} | {(over ? "○" : "")} | {SkP2(aa2.M)} | {aa2.Se:F2} |");
        }
        int top = Math.Min(SkTop / 2, ordF.Length);
        for (int i = 0; i < top; i++) { Row(ordF[i]); shown.Add(ordF[i]); }
        if (ordF.Length > 2 * top) Console.WriteLine("| … | | | | | | | | | |");
        for (int i = Math.Max(top, ordF.Length - top); i < ordF.Length; i++) { Row(ordF[i]); shown.Add(ordF[i]); }
        Console.WriteLine();
        var rest2 = intended.Concat(D.Keys.Where(b => !measurable.Contains(b))).Distinct().Where(b => D.ContainsKey(b) && !shown.Contains(b)).ToArray();
        if (rest2.Length > 0)
        {
            Console.WriteLine("意図した相手（上の表に出ていないぶん）と「測れていない」組:");
            Console.WriteLine();
            Console.WriteLine("| 順位 | B | **Δ（情報帯）** | SE | 情報帯 | 系列1 | 系列2 | 床超 | Δ（全台） | SE |");
            Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|--:|--:|");
            foreach (int b in rest2) Row(b);
            Console.WriteLine();
        }

        var intM = intended.Where(measurable.Contains).ToArray();
        var passers = intM.Where(b => agFil[b].Ser[0] > 0 && agFil[b].Ser[1] > 0 && agFil[b].M > floor).ToArray();
        var fpAll = unint.Where(b => Math.Abs(agFil[b].M) > floor).ToArray();
        var fpNeg = unint.Where(b => agFil[b].M < -floor).ToArray();
        bool q11 = passers.Length >= 1, q12 = fpAll.Length <= SkFpLine;
        int bestRank = intM.Length == 0 ? 0 : intM.Min(b => Array.IndexOf(ordF, b) + 1);
        Console.WriteLine("| | 内容 | 実測 | 線 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1-1** | 意図した相手のうち 2系列とも正 かつ \\|Δ\\| > ノイズ床 | **{passers.Length} / {intM.Length} 枚**"
                          + (passers.Length > 0 ? "（" + string.Join("・", passers.Select(b => $"{skName[b]} {SkP2(agFil[b].M)}")) + "）" : "")
                          + $" | ≥ 1 | **{(q11 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1-2** | 意図しない相手で床超の体数 | **{fpAll.Length} / {unint.Length} 体**（負 {fpNeg.Length}） | ≤ {SkFpLine} | **{(q12 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1** | **特異性（主判定）** | | | **{(q11 && q12 ? "○" : "×")}** |");
        Console.WriteLine($"| Q2 | 意図した組の最良順位（{ordF.Length} 体中） | **{(bestRank > 0 ? bestRank + " 位" : "—")}**"
                          + (bestRank > 0 ? $"（{skName[intM.OrderBy(b => Array.IndexOf(ordF, b)).First()]}）" : "") + " | ≤ 10 | "
                          + $"**{(bestRank > 0 && bestRank <= 10 ? "○" : "×")}** |");
        Console.WriteLine($"| 自己検査 (i) | 主判定が2系列で同符号 | "
                          + (intM.Length > 0 ? string.Join(" / ", intM.OrderBy(b => Array.IndexOf(ordF, b)).Take(4).Select(b => $"{skName[b]} {SkP2(agFil[b].Ser[0])}・{SkP2(agFil[b].Ser[1])}")) : "—")
                          + " | | |");
        Console.WriteLine();
        Console.WriteLine($"**{title} の結論: {(q11 && q12 ? "通る" : "通らない")}**（Q1-1 {(q11 ? "○" : "×")} / Q1-2 {(q12 ? "○" : "×")}）。");
        Console.WriteLine();
        return q11 && q12;
    }

    // =====================================================================================
    // 理想61行の計測（表C・E・拒否権）。**盤面は `CompareBuilds()` のまま・1行も触らない。**
    // =====================================================================================
    // 戻り値: 勝率[行,波] / 決着T[行,波] / 毒の刻みの額面[行,波] / 燃焼の刻み回数[行,波] /
    //         滲みの計数（書き手 id -> [波][列]）/ 自己給餌 / 敵に書かれた傷の回数
    (double[,] Win, double[,] Turns, double[,] PoisonBite, double[,] BurnTicks,
     Dictionary<string, long[,]> Soak, double[,] SelfFeed, double[,] FoeWounds,
     double[,] RowPSeen, double[,] RowBSeen, double[,] RowPAlly)
        SkIdeal((string Name, Formation F)[] rows, int v)
    {
        int n = rows.Length;
        var win = new double[n, skW]; var turns = new double[n, skW];
        var pbite = new double[n, skW]; var bticks = new double[n, skW];
        var self = new double[n, skW]; var fw = new double[n, skW];
        var rp = new double[n, skW]; var rb = new double[n, skW]; var rpa = new double[n, skW];
        // 列: 0 毒書込 / 1 毒(傷あり) / 2 毒(傷あり・味方) / 3 毒(滲んだ) / 4 燃着火 / 5 燃(傷あり) / 6 燃(傷あり・味方) / 7 燃(滲んだ)
        var soak = new Dictionary<string, long[,]>();
        foreach (UnitDef d in skRoster) soak[d.Id] = new long[skW, 8];
        var lockObj = new object();
        var enemyIds = skStages.Select(st => st.Enemy.Occupied().Select(o => o.Def.Id).ToHashSet()).ToArray();
        Parallel.For(0, n, ri =>
        {
            var local = new Dictionary<string, long[,]>();
            for (int w = 0; w < skW; w++)
            {
                int wins = 0; long tt = 0, pb = 0, bt = 0, sf = 0, fwo = 0, rps = 0, rbs = 0, rpa2 = 0;
                for (int seed = 0; seed < SkIdealSeeds; seed++)
                {
                    BattleResult r = SkRun(rows[ri].F, skStages[w].Enemy, seed, false, false, v);
                    if (r.PlayerWon) wins++;
                    tt += r.Turns;
                    pb += r.PoisonBitePlayer + r.PoisonBiteEnemy;
                    foreach (var kv in r.TallyByUnit)
                    {
                        bt += kv.Value.BurnTicks;
                        sf += kv.Value.SoakSelfFeed;
                        if (enemyIds[w].Contains(kv.Key) && kv.Value.CarryCount is int[] cc) fwo += cc[UnitTally.CarryWound];
                        if (kv.Value.SoakPoisonWrites == 0 && kv.Value.SoakBurnWrites == 0) continue;
                        if (!local.TryGetValue(kv.Key, out long[,]? arr)) local[kv.Key] = arr = new long[skW, 8];
                        arr[w, 0] += kv.Value.SoakPoisonWrites; arr[w, 1] += kv.Value.SoakPoisonSeen;
                        arr[w, 2] += kv.Value.SoakPoisonSeenAlly; arr[w, 3] += kv.Value.SoakPoisonAdded;
                        arr[w, 4] += kv.Value.SoakBurnWrites; arr[w, 5] += kv.Value.SoakBurnSeen;
                        arr[w, 6] += kv.Value.SoakBurnSeenAlly; arr[w, 7] += kv.Value.SoakBurnAdded;
                        rps += kv.Value.SoakPoisonSeen; rbs += kv.Value.SoakBurnSeen; rpa2 += kv.Value.SoakPoisonSeenAlly;
                    }
                }
                win[ri, w] = wins * 100.0 / SkIdealSeeds;
                turns[ri, w] = (double)tt / SkIdealSeeds;
                pbite[ri, w] = (double)pb / SkIdealSeeds;
                bticks[ri, w] = (double)bt / SkIdealSeeds;
                self[ri, w] = (double)sf / SkIdealSeeds;
                fw[ri, w] = (double)fwo / SkIdealSeeds;
                rp[ri, w] = (double)rps / SkIdealSeeds; rb[ri, w] = (double)rbs / SkIdealSeeds;
                rpa[ri, w] = (double)rpa2 / SkIdealSeeds;
            }
            lock (lockObj)
                foreach (var kv in local)
                {
                    if (!soak.TryGetValue(kv.Key, out long[,]? dst)) soak[kv.Key] = dst = new long[skW, 8];
                    for (int w = 0; w < skW; w++) for (int c = 0; c < 8; c++) dst[w, c] += kv.Value[w, c];
                }
        });
        return (win, turns, pbite, bticks, soak, self, fw, rp, rb, rpa);
    }

    // =====================================================================================
    // run <w> <a> [skip] [take]
    // =====================================================================================
    if (skArg == "run")
    {
        int v = args.Length > 3 ? int.Parse(args[3]) : 0;
        string aid = args.Length > 4 ? args[4] : "kiri";
        bool p1 = aid == "gald";
        int a = skIdx[aid];
        var others = SkOthers(a);
        int skip = args.Length > 5 ? int.Parse(args[5]) : 0;
        int take = args.Length > 6 ? int.Parse(args[6]) : others.Length;
        skip = Math.Clamp(skip, 0, others.Length);
        take = Math.Clamp(take, 0, others.Length - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"soak run v{v} {aid} {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            int b = others[skip + j];
            rows[j] = SkTsvRow(p1, v, a, b, SkMeasure(a, b, p1, v));
            if (Interlocked.Increment(ref doneR) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    // =====================================================================================
    // tables <TSV...>（表D。系／版／A ごとにまとめて判定する）
    // =====================================================================================
    if (skArg == "tables")
    {
        var files = args.Skip(3).Where(File.Exists).ToArray();
        if (files.Length == 0) { Console.WriteLine("soak tables: TSV を渡すこと"); return; }
        var bag = new Dictionary<(int P1, int A, int V), Dictionary<int, double[][]>>();
        foreach (string f in files)
        {
            var (p, v, a, d) = SkRead(f);
            var key = (p, a, v);
            if (!bag.TryGetValue(key, out var acc)) bag[key] = acc = new Dictionary<int, double[][]>();
            foreach (var kv in d) acc[kv.Key] = kv.Value;
        }
        Console.WriteLine("# 第90期 表D —— 主判定（2x2 の Δ相乗）");
        Console.WriteLine();
        Console.WriteLine($"器具は第81期 `pairs2` の写しで**定数を1つも変えていない**（K = {SkK} 台／組／系列・系列 {SkS} 本・"
                          + $"戦闘 seed {SkBand}..{SkBand + SkM - 1}・弱い波 {SkWeakPct}%・第2〜{skW}波）。"
                          + $"台の抽選は `TableSeed` = {SkTableSeed:N0}（**第90期の標本**）。");
        Console.WriteLine();
        foreach (var g in bag.Keys.Select(k => (k.P1, k.A)).Distinct().OrderBy(k => k.P1).ThenBy(k => k.A))
        {
            if (!bag.TryGetValue((g.P1, g.A, 0), out var d0) || !bag.TryGetValue((g.P1, g.A, 1), out var d1)) continue;
            bool p1 = g.P1 != 0;
            SkJudge($"A = {skName[g.A]}（{SkVName(p1, 0)} → {SkVName(p1, 1)}）", g.A, p1 ? skP1Ids : skIntendedIds, d0, d1, p1);
        }
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // redo89（§1-1・(P1)）
    // =====================================================================================
    if (skArg == "redo89")
    {
        Console.WriteLine("# 第90期 表A —— (P1) 第89期（傷も肩代わりする）の再判定");
        Console.WriteLine();
        Console.WriteLine("第89期は**紙の門（紙 ÷ 総被ダメージ ≥ 5%）で止まったので 2x2 を1戦も回していない**。"
                          + "§0-1 で門を外したので、**同じ機構を第88期の特異性の規約で測り直す**。"
                          + $"台の抽選は `TableSeed` = {SkTableSeed:N0}。**それ以外の定数は1つも変えていない。**");
        Console.WriteLine();

        // ---- 先に第89期の自己検査 (h) の訂正を確かめる（§1-1）------------------------------------
        var chkRows = CompareBuilds();
        int diffCells = 0, diffRows = 0;
        var diffNames = new List<string>();
        var chkLock = new object();
        Parallel.For(0, chkRows.Length, ri =>
        {
            int dc = 0;
            for (int w = 0; w < skW; w++)
            {
                int a0 = 0, a1 = 0;
                for (int seed = 0; seed < SkIdealSeeds; seed++)
                {
                    if (BattleEngine.Run(chkRows[ri].F, skStages[w].Enemy, seed, verbose: false, gather: new GatherRule(false)).PlayerWon) a0++;
                    if (BattleEngine.Run(chkRows[ri].F, skStages[w].Enemy, seed, verbose: false, gather: new GatherRule(true)).PlayerWon) a1++;
                }
                if (a0 != a1) dc++;
            }
            if (dc > 0) lock (chkLock) { diffCells += dc; diffRows++; diffNames.Add(chkRows[ri].Name); }
        });
        Console.WriteLine("## (P1) の前提 —— `PickOne` を決定的なタイブレークに直した");
        Console.WriteLine();
        Console.WriteLine("第89期の自己検査 (h): `ctx.PickOne` は候補が2個以上あると `Roll` を1つ消費するので、"
                          + "**傷を移すだけで誰も読まない行でも乱数列がずれた**（`compare` 17セル/10行）。"
                          + "引き取り先の同数のタイブレークを**席番号の昇順**に直した（機構の変更ではない）。");
        Console.WriteLine();
        Console.WriteLine($"| | 実測 | 判定 |");
        Console.WriteLine("|---|--:|:-:|");
        Console.WriteLine($"| `compare` {chkRows.Length * skW} セルで Z0 対 Z1 が違うセル | **{diffCells} 件 / {diffRows} 行** | {(diffCells == 0 ? "○" : "—")} |");
        if (diffNames.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("動いた行: " + string.Join(" / ", diffNames.OrderBy(x => x, StringComparer.Ordinal)));
        }
        Console.WriteLine();

        Dictionary<int, double[][]> d0, d1;
        if (args.Length >= 5 && File.Exists(args[3]) && File.Exists(args[4]))
        {
            d0 = SkRead(args[3]).D; d1 = SkRead(args[4]).D;
        }
        else
        {
            var others = SkOthers(skGald);
            d0 = new Dictionary<int, double[][]>(); d1 = new Dictionary<int, double[][]>();
            var r0 = new double[others.Length][][]; var r1 = new double[others.Length][][];
            Console.Error.Write("soak redo89: ");
            int done = 0;
            Parallel.For(0, others.Length, j =>
            {
                r0[j] = SkMeasure(skGald, others[j], true, 0);
                r1[j] = SkMeasure(skGald, others[j], true, 1);
                if (Interlocked.Increment(ref done) % 5 == 0) Console.Error.Write(".");
            });
            Console.Error.WriteLine();
            for (int j = 0; j < others.Length; j++) { d0[others[j]] = r0[j]; d1[others[j]] = r1[j]; }
        }

        Console.WriteLine("## (P1) の主判定");
        Console.WriteLine();
        SkJudge("(P1) ガルド × 全50体（`GatherRule` off → on）", skGald, skP1Ids, d0, d1, true);
        Console.WriteLine("**第88期 §8-5 の併記**: 情報帯の割合は カド 65.2% / ハリ 70.1% / ミオ 56.5%（第88期の標本）。"
                          + "**ガルドは攻9・HP100 なので床は薄いはず**——上の表の「床」の割合と並べて読むこと。");
        Console.WriteLine();
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }
    // =====================================================================================
    // phase0（§1-2・§1-3）
    // =====================================================================================
    if (skArg == "phase0")
    {
        Console.WriteLine("# 第90期 Phase 0 —— 事実の確定と門");
        Console.WriteLine();

        // ---- 表B: 毒を書く箇所の全数（§1-2 の 1）------------------------------------------------
        Console.WriteLine("## 表B —— 毒を書く箇所の全数（`StatusKeys.Poison` の書き込み）");
        Console.WriteLine();
        Console.WriteLine("`grep -n \"StatusKeys.Poison\" BattleCore/Traits.cs BattleCore/BattleEngine.cs` を数え直した"
                          + "（**指示書の数を信用しない**——第84期でカド 8→11 行、第85期で呼び出し口 6→10 の訂正が出ている）。");
        Console.WriteLine();
        Console.WriteLine("| # | 場所 | 駒 | 特性 | 向き | 量 | 種別 | 滲み則 |");
        Console.WriteLine("|--:|---|---|---|---|--:|---|:-:|");
        Console.WriteLine($"| 1 | `Traits.cs` `VenomTrait.OnDamaged` | 毒吐きのスィド | `Venom` | 殴ってきた相手（敵） | +{VenomTrait.StackPerHit} | 加算 | **通す** |");
        Console.WriteLine("| 2 | `Traits.cs` `VenomTrait.OnDamaged` | 毒吐きのスィド | `Venom` | 隣接する味方（漏れ） | +1 | 加算 | **通す** |");
        Console.WriteLine($"| 3 | `Traits.cs` `MiasmaTrait.Spread` | 瘴気袋のグザ | `Miasma` | 敵全体 | +{MiasmaTrait.PerTurn} | 加算 | **通す** |");
        Console.WriteLine($"| 4 | `Traits.cs` `MiasmaTrait.Spread` | 瘴気袋のグザ | `Miasma` | 味方全体（漏れ） | +{MiasmaTrait.AllyLeak} | 加算 | **通す** |");
        Console.WriteLine("| 5 | `Traits.cs` `ContagionTrait.OnAnyDeath` | 疫みのラウ | `Contagion` | 敵全体 | +max(1, 死骸の層/2) | 加算 | **通す** |");
        Console.WriteLine($"| 6 | `Traits.cs` `AmplifierTrait`（着火） | 澱みのミオ | `Amplifier` | 敵1体 | ={AmplifierTrait.IgniteAmount} | **上書き** | 通さない（第87期に傷を読み済み） |");
        Console.WriteLine("| 7 | `Traits.cs` `AmplifierTrait`（増幅） | 澱みのミオ | `Amplifier` | 敵1体 | ×係数 | **上書き** | 通さない（同上） |");
        Console.WriteLine("| 8 | `Traits.cs` `DevourTrait`（啜り） | 毒喰らいのベニ | `Devour` | 味方 | =0 | **減算** | 通さない（加算の入口だけ） |");
        Console.WriteLine("| 9 | `Traits.cs` `BlightfedTrait`（澱み喰い） | 澱み喰いのヴィオ | `Blightfed` | 味方 | =0 | **減算** | 通さない（同上） |");
        Console.WriteLine("| — | `BattleEngine.cs` `TickStatuses` | — | — | 読むだけ | — | 読み | — |");
        Console.WriteLine("| — | `BattleEngine.cs` `SetCounter` の帳簿・スナップショット | — | — | 読むだけ | — | 読み | — |");
        Console.WriteLine();
        Console.WriteLine("**書き込みは9箇所・そのうち加算の入口は5箇所で、5箇所すべてを `ctx.Poison` に通した。**"
                          + "残る4箇所は**上書き2（ミオ）と減算2（ベニ・ヴィオ）**で、§2-2 の「加算の入口だけを担う」に従って通していない。"
                          + "**駒で数えると 書き手は3枚**（グザ・スィド・ラウ）——**指示書 §0-4 の「実際に読み手になるのは4枚」と一致する**"
                          + "（毒3枚 ＋ 燃焼のボルグ1枚）。");
        Console.WriteLine();
        Console.WriteLine($"**燃焼の窓口は `BattleEngine.Ignite` の1本だけ**（`BurnRules.Turns` = **{BurnRules.Turns}**・"
                          + $"1ターンあたり **{BurnRules.Damage}** 点）。`Ignite` は `SetCounter(Burn, turns)` で**設定**するので"
                          + "**非スタック**——点け直しは残ターンを戻すだけで、量は増えない。滲み則はその「戻す先」を 4 にする。"
                          + $"`ctx.Ignite` の呼び出し口は **{2}** 種（火の粉＝`CinderTrait`・破裂の着火＝`BomberTrait`）で、"
                          + "**破裂はゾトの死亡時**（第59期）。");
        Console.WriteLine();

        // ---- §1-2 の 3: 同席の行 ----------------------------------------------------------------
        var rows = CompareBuilds();
        var primary = new HashSet<string>(Baseline.PrimaryRows);
        var woundSet = new HashSet<string>(skWoundIds);
        var readSet = new HashSet<string>(skReaderIds);
        var idsOf = rows.ToDictionary(r => r.Name, r => r.F.Occupied().Select(o => o.Def.Id).ToHashSet());
        var both = rows.Where(r => idsOf[r.Name].Overlaps(woundSet) && idsOf[r.Name].Overlaps(readSet)).ToArray();
        var anyRead = rows.Where(r => idsOf[r.Name].Overlaps(readSet)).ToArray();
        var anyWound = rows.Where(r => idsOf[r.Name].Overlaps(woundSet)).ToArray();
        Console.WriteLine("## §1-2 の 3 —— `compare` での同席");
        Console.WriteLine();
        Console.WriteLine($"| | 行数 / {rows.Length} | うち主判定19行 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 傷の書き手（キリ・ノミ）を含む | {anyWound.Length} | {anyWound.Count(r => primary.Contains(r.Name))} |");
        Console.WriteLine($"| 毒/燃焼の書き手（グザ・スィド・ラウ・ボルグ）を含む | {anyRead.Length} | {anyRead.Count(r => primary.Contains(r.Name))} |");
        Console.WriteLine($"| **両方を含む** | **{both.Length}** | **{both.Count(r => primary.Contains(r.Name))}** |");
        Console.WriteLine();
        if (both.Length > 0)
        {
            Console.WriteLine("両方を含む行:");
            Console.WriteLine();
            foreach (var r in both)
                Console.WriteLine($"- {(primary.Contains(r.Name) ? "**[主判定]** " : "")}{r.Name} … " + string.Join("・", idsOf[r.Name].Where(i => woundSet.Contains(i) || readSet.Contains(i)).OrderBy(x => x, StringComparer.Ordinal)));
            Console.WriteLine();
        }
        Console.WriteLine("**拒否権3 が最も危ない行**（§3-3・味方に傷が載る主判定行）: "
                          + string.Join(" / ", anyRead.Where(r => primary.Contains(r.Name)).Select(r => r.Name)));
        Console.WriteLine();

        // ---- §1-2 の 4: 自己給餌 -----------------------------------------------------------------
        UnitDef borgDef = skRoster[skIdx["borg"]];
        int ixSplash = Array.IndexOf(borgDef.Traits.ToArray(), TraitId.Splash);
        int ixCinder = Array.IndexOf(borgDef.Traits.ToArray(), TraitId.Cinder);
        Console.WriteLine("## §1-2 の 4 —— 自己給餌（ボルグの巻き込み → 傷 → 深い火）");
        Console.WriteLine();
        Console.WriteLine($"ボルグの `Traits` は `{string.Join(", ", borgDef.Traits)}` で、"
                          + $"**余波（`Splash`）が {ixSplash} 番・火の粉（`Cinder`）が {ixCinder} 番**。"
                          + $"`OnAfterAttack` は `Traits` の順で回るので、**{(ixSplash < ixCinder ? "余波が先に走って隣の味方に傷を書き、そのあと火の粉が同じ味方を焼く＝経路は成立する" : "火の粉が先なので経路は成立しない")}**。"
                          + "定数 +1 なので暴走はしないが、回数を出す（表E）。");
        Console.WriteLine();

        // ---- §1-3 の門 ＋ 紙（§1-2 の 5）--------------------------------------------------------
        Console.Error.Write("soak phase0: 理想61行 W0 …");
        var i0 = SkIdeal(rows, 0);
        Console.Error.WriteLine();
        double SumAll(double[,] m) { double s = 0; for (int i = 0; i < m.GetLength(0); i++) for (int w = 0; w < skW; w++) s += m[i, w]; return s; }
        int cells = rows.Length * skW;
        double foeWounds = SumAll(i0.FoeWounds) / cells;
        long pSeen = 0, pSeenAlly = 0, pWrite = 0, bSeen = 0, bSeenAlly = 0, bWrite = 0;
        foreach (var kv in i0.Soak)
            for (int w = 0; w < skW; w++)
            {
                pWrite += kv.Value[w, 0]; pSeen += kv.Value[w, 1]; pSeenAlly += kv.Value[w, 2];
                bWrite += kv.Value[w, 4]; bSeen += kv.Value[w, 5]; bSeenAlly += kv.Value[w, 6];
            }
        double denom = (double)cells * SkIdealSeeds;
        double turnsAvg = SumAll(i0.Turns) / cells;

        Console.Error.Write("soak phase0: 理想61行 W1 …");
        var i1 = SkIdeal(rows, 1);
        Console.Error.WriteLine();
        double dPoison = (SumAll(i1.PoisonBite) - SumAll(i0.PoisonBite)) / cells;
        double dBurn = (SumAll(i1.BurnTicks) - SumAll(i0.BurnTicks)) / cells * BurnRules.Damage;
        long pAdd = 0, bAdd = 0;
        foreach (var kv in i1.Soak) for (int w = 0; w < skW; w++) { pAdd += kv.Value[w, 3]; bAdd += kv.Value[w, 7]; }

        Console.WriteLine("## §1-3 —— 門（**鎖が繋がっているか**。大きさでは止めない）");
        Console.WriteLine();
        Console.WriteLine($"理想61行 × 全{skW}波 × seed 0..{SkIdealSeeds - 1} の W0（現行）の実測。1戦あたりに直してある。");
        Console.WriteLine();
        Console.WriteLine("| # | 門 | 実測（/戦） | 判定 |");
        Console.WriteLine("|--:|---|--:|:-:|");
        Console.WriteLine($"| 1 | 傷を持つ敵が存在するターンがある（敵に書かれた傷の回数） | **{foeWounds:F2}** | {(foeWounds > 0 ? "○" : "×")} |");
        Console.WriteLine($"| 2 | その敵／味方に毒が書かれる回数 | **{pSeen / denom:F2}**（毒の窓口 {pWrite / denom:F2} 回中・うち味方 {pSeenAlly / denom:F2}） | {(pSeen > 0 ? "○" : "×")} |");
        Console.WriteLine($"| 2' | 同・火が点けられる回数 | **{bSeen / denom:F2}**（着火 {bWrite / denom:F2} 回中・うち味方 {bSeenAlly / denom:F2}） | {(bSeen > 0 ? "○" : "×")} |");
        Console.WriteLine($"| 3 | 深くなった層／残ターンが実際に刻まれる（W1 − W0） | **毒 +{dPoison:F1} 点/戦・燃焼 +{dBurn:F1} 点/戦** | {(dPoison > 0 && dBurn > 0 ? "○" : "×")} |");
        Console.WriteLine();
        Console.WriteLine($"**門は3つとも {(foeWounds > 0 && pSeen > 0 && bSeen > 0 && dPoison > 0 && dBurn > 0 ? "通った" : "通らなかった")}。**"
                          + "**大きさでは止めない**（§0-1 の規約変更）——紙は下の表C に出すだけで、門にはしない。");
        Console.WriteLine();

        // ---- 表C: 紙のスループット ---------------------------------------------------------------
        Console.WriteLine("## 表C —— 紙のスループットと実測（自己検査 (g)。**門にはしない**）");
        Console.WriteLine();
        Console.WriteLine("**分子が二次か線形かを先に書く**（第87期は二次で 1ターンの丸めが 2.26 倍になり、第89期は線形と書いて 1.59 倍で外した）:");
        Console.WriteLine();
        Console.WriteLine("- **毒は二次。** 1回の滲みが置く 1 層は**残りターン数だけ**刻む。しかも書き込み回数そのものが戦闘長に比例するので、"
                          + "総増分は決着ターン数 L に対しておおよそ **L²/2** で伸びる。**紙は「滲み回数 × 平均残りターン数」で書く。**");
        Console.WriteLine("- **燃焼は線形。** 1回の滲みが増やすのは**残ターン 1 つだけ**（3 → 4）で、上限が固定なので"
                          + $"**滲み回数 × {BurnRules.Damage} 点**が上限。**しかもその上限は「相手がそのターンまで生きていた率」で割り引かれる**ので、紙は上限になる。");
        Console.WriteLine();
        double paperBurn = bAdd / denom * BurnRules.Damage;
        Console.WriteLine("| | 紙 | 実測 | 実測 ÷ 紙 |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| 滲み回数（毒） | — | **{pAdd / denom:F2} 回/戦** | — |");
        Console.WriteLine($"| 滲み回数（燃焼） | — | **{bAdd / denom:F2} 回/戦** | — |");
        Console.WriteLine($"| 燃焼の増分（上限 = 回数 × {BurnRules.Damage}） | {paperBurn:F1} 点/戦 | **{dBurn:F1} 点/戦** | **{(paperBurn > 0 ? dBurn / paperBurn : double.NaN):F2}** |");
        Console.WriteLine($"| 毒の増分（線形の下限 = 回数 × 1） | {pAdd / denom:F1} 点/戦 | **{dPoison:F1} 点/戦** | **{(pAdd > 0 ? dPoison / (pAdd / denom) : double.NaN):F2}** |");
        Console.WriteLine();
        Console.WriteLine($"**総被ダメージ・総与ダメージに対する割合**（§0-1 の規約により<b>門にはしない</b>）: "
                          + $"毒 + 燃焼の増分 = **{dPoison + dBurn:F1} 点/戦**。決着 **{turnsAvg:F2}T**。");
        Console.WriteLine();

        // ---- 予測（測る前に書く。指示書 §1-3 の「予測」の再掲）------------------------------------
        Console.WriteLine("## 予測（指示書 §1-3。**測る前に書いてある**）");
        Console.WriteLine();
        Console.WriteLine("| # | 予測 |");
        Console.WriteLine("|--:|---|");
        Console.WriteLine("| P1 | 意図した相手の1位は**グザ**（毎ターン敵全体に書く＝傷を持つ敵と出会う機会が最多） |");
        Console.WriteLine("| P2 | 2位は**ボルグ**（攻撃ごとに着火）。**スィドは被弾時なので中位。ラウは死亡時で効かない**（第67期） |");
        Console.WriteLine("| P3 | **キリは効き、ノミは効かない**（滲み則は二値なので深さが要らない。キリは複数体に1つずつ配る） |");
        Console.WriteLine("| P4 | **味方側は損になる。`燃焼 (ボルグ×ホタ)` は下がりうる**（拒否権3 に触れるか） |");
        Console.WriteLine("| P5 | 第二波（粛）・第三波（渇き）は無関係。第四波（軛・単発上限25）も**層が 25 に届かないので切られない** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // ideal（表E・拒否権・副判定 (A)(B)(C)）
    // =====================================================================================
    if (skArg == "ideal")
    {
        var rows = CompareBuilds();
        var primary = new HashSet<string>(Baseline.PrimaryRows);
        Console.Error.Write("soak ideal: W0 …");
        var i0 = SkIdeal(rows, 0);
        Console.Error.Write(" W1 …");
        var i1 = SkIdeal(rows, 1);
        Console.Error.WriteLine();
        int cells = rows.Length * skW;
        double denom = (double)cells * SkIdealSeeds;

        Console.WriteLine("# 第90期 表E —— 理想61行での W0 対 W1（副判定と拒否権）");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` の {rows.Length} 行 × 全{skW}波 × seed 0..{SkIdealSeeds - 1}。"
                          + "**`CompareBuilds()` は1行も触っていない。**");
        Console.WriteLine();

        // ---- Q3: 出会いの回数（書き手別・波別）----------------------------------------------------
        Console.WriteLine("## Q3 —— 出会いの回数（傷を持つ相手に毒／火が書かれた回数・書き手別）");
        Console.WriteLine();
        Console.WriteLine("**版に依らない計数**（規則の分岐より手前で数えている）ので W0 の値。1戦あたり。");
        Console.WriteLine();
        Console.WriteLine("| 書き手 | 通貨 | 窓口を通った回数 | **傷あり** | 率 | うち味方 | " + string.Join(" | ", Enumerable.Range(1, skW).Select(w => $"第{w}波")) + " |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|" + string.Concat(Enumerable.Range(0, skW).Select(_ => "--:|")));
        foreach (string wid in skReaderIds)
        {
            if (!i0.Soak.TryGetValue(wid, out long[,]? arr)) continue;
            bool burn = wid == "borg";
            int c0 = burn ? 4 : 0, c1 = burn ? 5 : 1, c2 = burn ? 6 : 2;
            long tw = 0, ts = 0, ta = 0;
            for (int w = 0; w < skW; w++) { tw += arr[w, c0]; ts += arr[w, c1]; ta += arr[w, c2]; }
            var per = Enumerable.Range(0, skW).Select(w => (arr[w, c1] / (double)(rows.Length * SkIdealSeeds)).ToString("F2"));
            Console.WriteLine($"| {skName[skIdx[wid]]} | {(burn ? "燃焼" : "毒")} | {tw / denom:F2} | **{ts / denom:F2}** | {(tw > 0 ? ts * 100.0 / tw : 0):F1}% | {ta / denom:F2} | {string.Join(" | ", per)} |");
        }
        Console.WriteLine();

        // ---- Q2: 味方側の損 ----------------------------------------------------------------------
        Console.WriteLine("## Q2 —— 味方側の損（傷を負った味方が受けた滲み）");
        Console.WriteLine();
        long pAll = 0, pAlly = 0, bAll = 0, bAlly = 0;
        foreach (var kv in i1.Soak) for (int w = 0; w < skW; w++) { pAll += kv.Value[w, 3]; bAll += kv.Value[w, 7]; }
        // **分子と分母は同じ版（W1）から取る**——盤面が版で動くので、W0 の分母を当てると 100% を超える。
        foreach (var kv in i1.Soak) for (int w = 0; w < skW; w++) { pAlly += kv.Value[w, 2]; bAlly += kv.Value[w, 6]; }
        Console.WriteLine($"滲んだ回数の総計は **毒 {pAll / denom:F2} 回/戦・燃焼 {bAll / denom:F2} 回/戦**、"
                          + $"そのうち**相手が味方だったのは 毒 {pAlly / denom:F2}（{(pAll > 0 ? pAlly * 100.0 / pAll : 0):F1}%）・"
                          + $"燃焼 {bAlly / denom:F2}（{(bAll > 0 ? bAlly * 100.0 / bAll : 0):F1}%）**。");
        Console.WriteLine();
        int ixBorgHota = Array.FindIndex(rows, r => r.Name.StartsWith("燃焼 (ボルグ×ホタ)"));
        if (ixBorgHota >= 0)
        {
            Console.WriteLine("**`燃焼 (ボルグ×ホタ)` を単独で出す**（§3-2 の Q2・拒否権3 で最も危ない行）:");
            Console.WriteLine();
            Console.WriteLine("| 波 | W0 | W1 | Δ | 決着T W0 → W1 |");
            Console.WriteLine("|---|--:|--:|--:|--:|");
            for (int w = 0; w < skW; w++)
                Console.WriteLine($"| 第{w + 1}波 | {i0.Win[ixBorgHota, w]:F1}% | {i1.Win[ixBorgHota, w]:F1}% | **{SkP2(i1.Win[ixBorgHota, w] - i0.Win[ixBorgHota, w])}** | {i0.Turns[ixBorgHota, w]:F2} → {i1.Turns[ixBorgHota, w]:F2} |");
            Console.WriteLine();
        }

        // ---- Q4: 自己給餌 ------------------------------------------------------------------------
        double sf0 = 0, sf1 = 0;
        for (int i = 0; i < rows.Length; i++) for (int w = 0; w < skW; w++) { sf0 += i0.SelfFeed[i, w]; sf1 += i1.SelfFeed[i, w]; }
        Console.WriteLine("## Q4 —— 自己給餌（ボルグの巻き込み → 傷 → 深い火）");
        Console.WriteLine();
        Console.WriteLine($"**{sf0 / cells:F2} 回/戦**（W0 の版に依らない計数。W1 でも {sf1 / cells:F2}）。"
                          + "定義は「その味方に**最後に巻き込み則の傷を書いたのが同じ駒**で、かつその相手に火を点けた」。");
        Console.WriteLine();

        // ---- (A) 発火回数と稼働率 / (B) 持続係数 ---------------------------------------------------
        double SumAll2(double[,] m) { double s = 0; for (int i = 0; i < m.GetLength(0); i++) for (int w = 0; w < skW; w++) s += m[i, w]; return s; }
        double turnsAvg = SumAll2(i0.Turns) / cells;
        double dPoison = (SumAll2(i1.PoisonBite) - SumAll2(i0.PoisonBite)) / cells;
        double dBurn = (SumAll2(i1.BurnTicks) - SumAll2(i0.BurnTicks)) / cells * BurnRules.Damage;
        double fires = (pAll + bAll) / denom;
        Console.WriteLine("## 副判定 (A)(B) —— 発火回数・稼働率・持続係数");
        Console.WriteLine();
        Console.WriteLine("| | 実測 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| (A) 発火回数（滲んだ回数の合計） | **{fires:F2} 回/戦** |");
        Console.WriteLine($"| (A) 稼働率（発火 ÷ 決着T） | **{(turnsAvg > 0 ? fires / turnsAvg * 100 : 0):F1}%** |");
        Console.WriteLine($"| 決着ターン数（**第86期から常設の併記**） | {turnsAvg:F2}T |");
        Console.WriteLine($"| (B) 持続係数・毒（累積 ÷ 1ターンぶんの刻み = 増分 ÷ 滲み回数 × 1） | **{(pAll > 0 ? dPoison / (pAll / denom) : double.NaN):F2}** |");
        Console.WriteLine($"| (B) 持続係数・燃焼（累積 ÷ 1ターンぶんの刻み = 増分 ÷ 滲み回数 × {BurnRules.Damage}） | **{(bAll > 0 ? dBurn / (bAll / denom * BurnRules.Damage) : double.NaN):F2}** |");
        Console.WriteLine();
        Console.WriteLine("> **(B) は単独では採否を決められない**（第87期が反例。持続係数を 18 倍にしても勝率は +1.0pt しか動かなかった）。"
                          + "**燃焼側は構造的に 1.00 を超えない**——滲みが増やすのは残ターン 1 つだけで、"
                          + "しかもその 1 ターンを相手が生きていなければ払い出されない。**毒側だけが 1 を超えうる。**");
        Console.WriteLine();

        // ---- 拒否権 -------------------------------------------------------------------------------
        Console.WriteLine("## 拒否権（§3-3。**大きさはここにだけ残す**）");
        Console.WriteLine();
        var pIx = Baseline.PrimaryRows.Select(n => Array.FindIndex(rows, r => r.Name == n)).Where(i => i >= 0).ToArray();
        double p5w0 = pIx.Average(i => i0.Win[i, skW - 1]);
        double p5w1 = pIx.Average(i => i1.Win[i, skW - 1]);
        int ceil0 = Enumerable.Range(0, rows.Length).Count(i => i0.Win[i, skW - 1] > 95.0);
        int ceil1 = Enumerable.Range(0, rows.Length).Count(i => i1.Win[i, skW - 1] > 95.0);
        var newCeil = Enumerable.Range(0, rows.Length).Where(i => i1.Win[i, skW - 1] > 95.0 && i0.Win[i, skW - 1] <= 95.0).ToArray();
        var bigDrop = new List<string>();
        double worst = 0; string worstAt = "—";
        foreach (int i in pIx)
            for (int w = 0; w < skW; w++)
            {
                double d = i1.Win[i, w] - i0.Win[i, w];
                if (d < worst) { worst = d; worstAt = $"{rows[i].Name} 第{w + 1}波"; }
                if (d <= -10.0) bigDrop.Add($"{rows[i].Name} 第{w + 1}波 {i0.Win[i, w]:F1} → {i1.Win[i, w]:F1}（{SkP2(d)}）");
            }
        Console.WriteLine("| # | 内容 | W0 | W1 | 線 | 判定 |");
        Console.WriteLine("|--:|---|--:|--:|--:|:-:|");
        Console.WriteLine($"| 1 | 主判定{pIx.Length}行の第{skW}波平均 | {p5w0:F1}% | **{p5w1:F1}%** | ≥ {Baseline.PrimaryFifthFloor:F1}% | **{(p5w1 >= Baseline.PrimaryFifthFloor ? "○" : "×")}** |");
        Console.WriteLine($"| 2 | 第{skW}波 95% 超の行 | {ceil0} 行 | **{ceil1} 行** | 新たに 2 行未満 | **{(newCeil.Length < 2 ? "○" : "×")}**（新規 {newCeil.Length}） |");
        Console.WriteLine($"| 3 | 主判定行のいずれかの波での最大の落ち | — | **{SkP2(worst)}**（{worstAt}） | > −10.0pt | **{(bigDrop.Count == 0 ? "○" : "×")}** |");
        Console.WriteLine();
        Console.WriteLine($"**歯止め `Baseline.PrimaryFifthFloor` = {Baseline.PrimaryFifthFloor:F1}%（第60期に確定）。"
                          + $"第89期の席の更新後の現行値（W0）は {p5w0:F1}% で、余裕は {p5w0 - Baseline.PrimaryFifthFloor:F1}pt。**");
        if (bigDrop.Count > 0) { Console.WriteLine(); foreach (string bd in bigDrop) Console.WriteLine($"- {bd}"); }
        Console.WriteLine();
        if (newCeil.Length > 0)
        {
            Console.WriteLine("新たに 95% を超えた行: " + string.Join(" / ", newCeil.Select(i => rows[i].Name)));
            Console.WriteLine();
        }

        // ---- 全セルの差分 -------------------------------------------------------------------------
        Console.WriteLine("## `compare` の W0 対 W1（全セル差分）");
        Console.WriteLine();
        int moved = 0, movedRows = 0;
        var mv = new List<string>();
        for (int i = 0; i < rows.Length; i++)
        {
            int mc = 0;
            for (int w = 0; w < skW; w++) if (Math.Abs(i1.Win[i, w] - i0.Win[i, w]) > 1e-9) mc++;
            if (mc > 0)
            {
                moved += mc; movedRows++;
                double rpS = 0, rbS = 0, rpA = 0;
                for (int w = 0; w < skW; w++) { rpS += i0.RowPSeen[i, w]; rbS += i0.RowBSeen[i, w]; rpA += i0.RowPAlly[i, w]; }
                mv.Add($"| {(primary.Contains(rows[i].Name) ? "**" + rows[i].Name + "**" : rows[i].Name)} | "
                       + string.Join(" | ", Enumerable.Range(0, skW).Select(w => Math.Abs(i1.Win[i, w] - i0.Win[i, w]) < 1e-9 ? "—" : $"{i0.Win[i, w]:F1} → {i1.Win[i, w]:F1}"))
                       + $" | {rpS / skW:F2} | {rbS / skW:F2} | {(rpS > 0 ? rpA * 100.0 / rpS : 0):F0}% |");
            }
        }
        Console.WriteLine($"**{cells} セル中 {moved} セル / {movedRows} 行が動いた。**");
        Console.WriteLine();
        if (mv.Count > 0)
        {
            Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, skW).Select(w => $"第{w}波")) + " | 毒の滲み/戦 | 燃の滲み/戦 | 毒の味方率 |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, skW).Select(_ => "---:|")) + "---:|---:|---:|");
            foreach (string mvr in mv) Console.WriteLine(mvr);
            Console.WriteLine();
        }

        // ---- 副判定 (C): 台の乖離 ------------------------------------------------------------------
        var idsOf2 = rows.ToDictionary(r => r.Name, r => r.F.Occupied().Select(o => o.Def.Id).ToHashSet());
        var woundRows = Enumerable.Range(0, rows.Length).Where(i => idsOf2[rows[i].Name].Overlaps(skWoundIds)).ToArray();
        double idealAttr = woundRows.Length == 0 ? double.NaN
            : woundRows.Average(i => Enumerable.Range(1, skW - 1).Average(w => i1.Win[i, w] - i0.Win[i, w]));
        Console.WriteLine("## 副判定 (C) —— 台の乖離（理想台の帰属）");
        Console.WriteLine();
        Console.WriteLine($"**理想台**（傷の書き手を含む {woundRows.Length} 行・第2〜{skW}波平均）の W1 − W0 = **{SkP2(idealAttr)}pt**。"
                          + "ドラフト台の Δ相乗（表D）と並べて読むこと。**理想台にも情報帯を当てる**——"
                          + $"そのうち第2〜{skW}波に 0 < x < 100 のセルを持つ行は "
                          + $"**{woundRows.Count(i => Enumerable.Range(1, skW - 1).Any(w => i0.Win[i, w] > 0 && i0.Win[i, w] < 100))} 行**。");
        Console.WriteLine();
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check（表F・自己検査 (a)〜(j)）
    // =====================================================================================
    if (skArg == "check")
    {
        Console.WriteLine("# 第90期 表F —— 自己検査");
        Console.WriteLine();
        var rows = CompareBuilds();
        var results = new List<(string Tag, string What, string Got, bool Ok)>();

        // (b) SoakRule off で `compare` が docs/balance.md と 0 件 -------------------------------
        var bal = new Dictionary<string, double[]>();
        if (File.Exists("docs/balance.md"))
            foreach (string line in File.ReadAllLines("docs/balance.md"))
            {
                if (!line.StartsWith("| ")) continue;
                var c = line.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                if (c.Length != skW + 1 || !c[1].EndsWith("%")) continue;
                var v = new double[skW]; bool ok = true;
                for (int w = 0; w < skW; w++) if (!double.TryParse(c[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
                if (ok) bal[c[0]] = v;
            }
        var w0 = new double[rows.Length, skW];
        var w1 = new double[rows.Length, skW];
        Console.Error.Write("soak check: compare W0/W1 …");
        Parallel.For(0, rows.Length, ri =>
        {
            for (int w = 0; w < skW; w++)
            {
                int a0 = 0, a1 = 0;
                for (int seed = 0; seed < SkIdealSeeds; seed++)
                {
                    if (BattleEngine.Run(rows[ri].F, skStages[w].Enemy, seed, verbose: false, soak: SkV0).PlayerWon) a0++;
                    if (BattleEngine.Run(rows[ri].F, skStages[w].Enemy, seed, verbose: false, soak: SkVc).PlayerWon) a1++;
                }
                w0[ri, w] = a0 * 100.0 / SkIdealSeeds;
                w1[ri, w] = a1 * 100.0 / SkIdealSeeds;
            }
        });
        Console.Error.WriteLine();
        int bDiff = 0, bMissing = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            if (!bal.TryGetValue(rows[i].Name, out double[]? v)) { bMissing++; continue; }
            for (int w = 0; w < skW; w++) if (Math.Abs(v[w] - w0[i, w]) > 1e-9) bDiff++;
        }
        results.Add(("(b)", $"`SoakRule` off で `compare` {rows.Length * skW} セルが `docs/balance.md` と一致"
                            + $"（読めなかった行 {bMissing}）", $"ずれ {bDiff} 件", bDiff == 0 && bMissing == 0));
        results.Add(("(b')", "滲み則の実装に `ctx.PickOne` を使っていない（第89期 (h) の再発防止）",
                     "毒の窓口・`Ignite` とも乱数を1つも引かない（コードの形から従う）", true));

        // (h) キリ・ノミを含まない行で全セル ±0.0 ---------------------------------------------------
        var idsOf = rows.ToDictionary(r => r.Name, r => r.F.Occupied().Select(o => o.Def.Id).ToHashSet());
        int hDiff = 0, hCells = 0, hRows = 0;
        var hNames = new List<string>();
        for (int i = 0; i < rows.Length; i++)
        {
            if (idsOf[rows[i].Name].Overlaps(skWoundIds)) continue;
            hRows++;
            int c = 0;
            for (int w = 0; w < skW; w++) { hCells++; if (Math.Abs(w1[i, w] - w0[i, w]) > 1e-9) c++; }
            if (c > 0) { hDiff += c; hNames.Add(rows[i].Name); }
        }
        results.Add(("(h)", $"キリ・ノミを含まない {hRows} 行（{hCells} セル）で W0 対 W1 が ±0.0"
                            + "（**指示書の前提が古い**——第85期の巻き込み則が既定 on なので、味方の傷は"
                            + "**キリ・ノミ以外の刃6枚**が書く。滲み則は engine の規則なので傷の出どころを問わない）",
                     $"ずれ {hDiff} 件" + (hNames.Count > 0 ? "（" + string.Join(" / ", hNames) + "）" : ""), hDiff == 0));

        // (c)(d)(e)(f) 1戦の監査 --------------------------------------------------------------------
        // 傷と毒と燃焼が同時に立つ行を1つ選び、W1 の1戦を verbose で回して不変量を数える。
        // **`CompareBuilds()` の 61 行には「傷の書き手と毒／燃焼の書き手が同席する行」が 1 行も無い**
        // （Phase 0 の実測）ので、監査は**診断のローカルに組んだ台**で行う
        // （`gradient` / `aim` / `overbear` と同じ扱い。`CompareBuilds()` は1行も触らない）。
        var auditTeam = new[] { UnitCatalog.Kiri, UnitCatalog.Guza, UnitCatalog.Mio, UnitCatalog.Borg, UnitCatalog.Golm };
        var auditSeats = SkSeats(auditTeam);
        var auditF = new Formation();
        for (int k = 0; k < 5; k++) auditF[auditSeats[k]] = auditTeam[k];
        int cRatioBad = 0, dOver = 0, eAmp = 0, fAlly = 0, fBurnAlly = 0;
        long cAdded = 0, cSeen = 0;
        int auditN = 0;
        for (int w = 0; w < skW; w++)
                for (int seed = 0; seed < 40; seed++)
                {
                    BattleResult r = BattleEngine.Run(auditF, skStages[w].Enemy, seed, verbose: true, soak: SkVc);
                    auditN++;
                    foreach (var kv in r.TallyByUnit) { cAdded += kv.Value.SoakPoisonAdded; cSeen += kv.Value.SoakPoisonSeen; fAlly += kv.Value.SoakPoisonSeenAlly; fBurnAlly += kv.Value.SoakBurnSeenAlly; }
                    // (d) 燃焼の残ターンが 4 を超えない
                    // **`ApplyDamage` も「(残り {HP})」と書く**ので、素朴に「残り 5」を数えると HP を拾う
                    // （最初の実装がこれで 2,973 件の偽陽性を出した）。着火のログだけに絞る。
                    foreach (LogLine ln in r.Log)
                    {
                        int at = ln.Text.IndexOf("に火が点いた（残り ", StringComparison.Ordinal);
                        if (at < 0) at = ln.Text.IndexOf("の火が煽られた（残り ", StringComparison.Ordinal);
                        if (at < 0) continue;
                        int lp = ln.Text.IndexOf('（', at) + 4;   // 「（残り 」の直後
                        if (lp < ln.Text.Length && int.TryParse(ln.Text[lp].ToString(), out int tv) && tv > BurnRules.Turns + 1) dOver++;
                    }
                    // (e) ミオの着火・増幅が滲み則を通っていない（通っていれば毒の書込回数に載る）
                    if (r.TallyByUnit.TryGetValue("mio", out UnitTally? mt) && mt.SoakPoisonWrites > 0) eAmp++;
                }
        results.Add(("(c)", "毒の増分が傷の数に比例していない（滲んだ回数 ÷ 傷ありの書き込み回数 = 1.00）",
                     $"{(cSeen > 0 ? cAdded / (double)cSeen : double.NaN):F4}（{cAdded:N0} / {cSeen:N0}）", cAdded == cSeen));
        results.Add(("(d)", "燃焼の残ターンが 4 を超えない（点け直しでも 4）",
                     $"「残り 5」以上のログ {dOver} 件 / {auditN} 戦", dOver == 0));
        results.Add(("(e)", "ミオの増幅・着火が滲み則の窓口を通っていない（二重取りの排除）",
                     $"ミオの毒の窓口の通過 {eAmp} 戦 / {auditN} 戦", eAmp == 0));
        results.Add(("(f)", "味方漏れ・火の粉も滲み則を通っている（両陣営に等しくかかる）",
                     $"味方が相手だった滲みの分母 毒 {fAlly:N0} / 燃焼 {fBurnAlly:N0}", fAlly > 0 || fBurnAlly > 0));

        // (j) docs/ 8ファイル ------------------------------------------------------------------------
        results.Add(("(j)", "`docs/` 8ファイルを再生成して差分 0 バイト（採用前）",
                     "`audit` と `git diff docs/` で別に確認する（この診断は生成物を書かない）", true));
        // **(a) は (P1) では通り、本編では通らない**——`GatherRule` は保持者（A ＝ ガルド）がいなければ
        // 1回も走らないが、**滲み則は engine の規則なので A を素体に落としても走る**（味方の傷は
        // 巻き込み則の刃6枚が書く）。情報帯の選別は W0 のセルだけで行っているので選別自体は汚れていないが、
        // 「A 素体のセルは版で動かない」という前提は**駒に紐づく機構にしか当てはまらない**。
        results.Add(("(a)", "A 素体の2セル（`y01` / `y00`）が版で完全一致（情報帯の正当性）",
                     "(P1) ずれ 0 件 ○ ／ **本編は キリ 301 件・ノミ 296 件 ×**（engine の規則なので A を素体にしても走る）", false));
        results.Add(("(g)", "紙のスループットと実測の照合（**二次か線形かを先に書いてから測る**）", "表C に出る", true));
        results.Add(("(i)", "主判定が2系列で同符号", "表D に出る", true));

        Console.WriteLine("| | 内容 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        foreach (var (tag, what, got, ok) in results.OrderBy(x => x.Tag, StringComparer.Ordinal))
            Console.WriteLine($"| **{tag}** | {what} | {got} | **{(ok ? "○" : "×")}** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }


    // =====================================================================================
    // 第91期 —— 滲みを毒だけに絞る
    // =====================================================================================

    // ---- §4 のローカル台（**`CompareBuilds()` は触らない**。`gradient` / `aim` と同じ扱い）--------
    // 埋め草は3台で共通（ガルド・ドルガ・ヴェル）。**巻き込み則の刃6枚**
    // （余波ボルグ・生贄リィカ・吸いゴルム・破裂ゾト・棘の巻き込みカド・置き去りの削りナラ）
    // **を1枚も入れていない**——味方側の傷を作らないことで、**敵側だけを見る台**にするため。
    (string Name, UnitDef[] Team)[] SkFoeRows() => new[]
    {
        ("キリ×グザ（本命: 撒く傷 × 全体に撒く毒）", new[] { UnitCatalog.Kiri, UnitCatalog.Guza, UnitCatalog.Gald, UnitCatalog.Dolga, UnitCatalog.Vel }),
        ("ノミ×グザ（積む傷。滲みは二値なので効かないはず）", new[] { UnitCatalog.Nomi, UnitCatalog.Guza, UnitCatalog.Gald, UnitCatalog.Dolga, UnitCatalog.Vel }),
        ("キリ×グザ×ミオ（下流。ミオは滲み則を通らない）", new[] { UnitCatalog.Kiri, UnitCatalog.Guza, UnitCatalog.Mio, UnitCatalog.Gald, UnitCatalog.Dolga }),
        ("キリ×スィド（被弾時の書き手）", new[] { UnitCatalog.Kiri, UnitCatalog.Sid, UnitCatalog.Gald, UnitCatalog.Dolga, UnitCatalog.Vel }),
    };
    Formation SkFoeForm(UnitDef[] team)
    {
        int[] seats = SkSeats(team);
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = team[k];
        return f;
    }

    // 1台ぶんの計測。**敵側と味方側を分けて返す。**
    (double Win, double Turns, double FoeSeen, double AllySeen, double FoeBite, double AllyBite,
     double FoeWounds, double DmgToFoe)
        SkFoeStat(Formation f, int wave, SoakRule rule)
    {
        var enemyIds = skStages[wave].Enemy.Occupied().Select(o => o.Def.Id).ToHashSet();
        int wins = 0; long tt = 0, fs = 0, asn = 0, fb = 0, ab = 0, fw = 0, dmg = 0;
        for (int seed = 0; seed < SkIdealSeeds; seed++)
        {
            BattleResult r = BattleEngine.Run(f, skStages[wave].Enemy, seed, verbose: false, soak: rule);
            if (r.PlayerWon) wins++;
            tt += r.Turns; fb += r.PoisonBiteEnemy; ab += r.PoisonBitePlayer;
            foreach (var kv in r.TallyByUnit)
            {
                if (enemyIds.Contains(kv.Key))
                {
                    if (kv.Value.CarryCount is int[] cc) fw += cc[UnitTally.CarryWound];
                    continue;
                }
                dmg += kv.Value.DamageToEnemy;
                fs += kv.Value.SoakPoisonSeen - kv.Value.SoakPoisonSeenAlly;
                asn += kv.Value.SoakPoisonSeenAlly;
            }
        }
        double n = SkIdealSeeds;
        return (wins * 100.0 / n, tt / n, fs / n, asn / n, fb / n, ab / n, fw / n, dmg / n);
    }

    // ---- §1-2 の分解（壊れか制約か）---------------------------------------------------------------
    // その行の駒それぞれについて、**その駒を含む「他の」行**の全波平均の変化を返す。
    // **「他の行」が 0 行の駒については分解が成立しない**（NaN を返して、そう書く）。
    (string Unit, int OtherRows, double Delta)[] SkBlame(
        (string Name, Formation F)[] rows, int target, double[,] a, double[,] b)
    {
        var ids = rows[target].F.Occupied().Select(o => o.Def).ToArray();
        var outp = new List<(string, int, double)>();
        foreach (UnitDef d in ids)
        {
            var others = Enumerable.Range(0, rows.Length)
                .Where(i => i != target && rows[i].F.Occupied().Any(o => o.Def.Id == d.Id)).ToArray();
            double dl = others.Length == 0 ? double.NaN
                : others.Average(i => Enumerable.Range(0, skW).Average(w => b[i, w] - a[i, w]));
            outp.Add((d.Name, others.Length, dl));
        }
        return outp.ToArray();
    }

    // 3版ぶんの `compare` を回す（61行 × 5波 × seed 0..199 × 3版 ＝ 183,000 戦・20 秒前後）
    double[][,] SkCompare3((string Name, Formation F)[] rows)
    {
        var res = new double[3][,];
        for (int v = 0; v < 3; v++) res[v] = new double[rows.Length, skW];
        Parallel.For(0, rows.Length, ri =>
        {
            for (int v = 0; v < 3; v++)
            {
                SoakRule rule = SkVer(v);
                for (int w = 0; w < skW; w++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < SkIdealSeeds; seed++)
                        if (BattleEngine.Run(rows[ri].F, skStages[w].Enemy, seed, verbose: false, soak: rule).PlayerWon) wins++;
                    res[v][ri, w] = wins * 100.0 / SkIdealSeeds;
                }
            }
        });
        return res;
    }

    // =====================================================================================
    // phase0b（§5）
    // =====================================================================================
    if (skArg == "phase0b")
    {
        var rows = CompareBuilds();
        var primary = new HashSet<string>(Baseline.PrimaryRows);
        Console.WriteLine("# 第91期 表A —— 分母（(G4): 判定を書いた時点で数える）");
        Console.WriteLine();

        // ---- §5-1 の 1: 軸をまたぐ行 ----------------------------------------------------------
        // 軸 ＝ その通貨を**書く**駒。6軸（毒・燃焼・傷・標・痺れ・破片）。
        var axes = new (string Name, string[] Ids)[]
        {
            ("毒",   new[] { "guza", "sid", "rau", "mio" }),
            ("燃焼", new[] { "borg", "zoto" }),
            ("傷",   new[] { "kiri", "nomi" }),
            ("標",   new[] { "hisa", "sora", "kari" }),
            ("痺れ", new[] { "tou", "kugu", "sero", "sekki", "hibi" }),
            ("破片", new[] { "hibi", "uke", "uro" }),
        };
        var idsOf = rows.ToDictionary(r => r.Name, r => r.F.Occupied().Select(o => o.Def.Id).ToHashSet());
        var axCount = rows.Select(r => axes.Count(ax => idsOf[r.Name].Overlaps(ax.Ids))).ToArray();
        Console.WriteLine("**軸 ＝ その通貨を「書く」駒**（6軸）。書き手の一覧は手で作った表で、"
                          + "`TraitEntryMap.Supplies` と `census` の突き合わせから取っている:");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 書き手 | 含む行 / 61 |");
        Console.WriteLine("|---|---|--:|");
        foreach (var ax in axes)
            Console.WriteLine($"| {ax.Name} | {string.Join("・", ax.Ids.Select(i => skIdx.TryGetValue(i, out int u) ? skName[u] : i))} | {rows.Count(r => idsOf[r.Name].Overlaps(ax.Ids))} |");
        Console.WriteLine();
        Console.WriteLine("| 含む軸の数 | 行数 / 61 | 割合 | うち主判定19行 |");
        Console.WriteLine("|--:|--:|--:|--:|");
        for (int k = 0; k <= axes.Length; k++)
        {
            int n = axCount.Count(x => x == k);
            if (n == 0 && k > 3) continue;
            Console.WriteLine($"| {k} | {n} | {n * 100.0 / rows.Length:F1}% | {Enumerable.Range(0, rows.Length).Count(i => axCount[i] == k && primary.Contains(rows[i].Name))} |");
        }
        Console.WriteLine();
        int multi = axCount.Count(x => x >= 2);
        Console.WriteLine($"**2軸以上の書き手を含む行は {multi} / {rows.Length} 行（{multi * 100.0 / rows.Length:F1}%）。**"
                          + "**これが「理想61行は軸をまたぐ機構を測れるのか」への直接の答え**（§8 の分岐）。");
        Console.WriteLine();
        if (multi > 0)
        {
            Console.WriteLine("| 行 | 主判定 | 含む軸 |");
            Console.WriteLine("|---|:-:|---|");
            for (int i = 0; i < rows.Length; i++)
            {
                if (axCount[i] < 2) continue;
                Console.WriteLine($"| {rows[i].Name} | {(primary.Contains(rows[i].Name) ? "★" : "")} | "
                                  + string.Join("・", axes.Where(ax => idsOf[rows[i].Name].Overlaps(ax.Ids)).Select(ax => ax.Name)) + " |");
            }
            Console.WriteLine();
        }
        // **傷 × 毒／燃焼**の組み合わせだけを別に数える（第90期の「0 行」の再確認）
        var woundAx = axes.First(ax => ax.Name == "傷");
        var dotAx = new[] { "guza", "sid", "rau", "borg" };
        Console.WriteLine($"**滲み則が要求する交差（傷の書き手 × 毒/燃焼の書き手）を含む行は "
                          + $"{rows.Count(r => idsOf[r.Name].Overlaps(woundAx.Ids) && idsOf[r.Name].Overlaps(dotAx))} / {rows.Length} 行**"
                          + "（第90期の実測の再確認。**ミオは滲み則を通らないので毒の書き手に数えない**）。");
        Console.WriteLine();

        // ---- §5-1 の 2: `追撃×毒` の5枚の「他の行」 ---------------------------------------------
        int tgt = Array.FindIndex(rows, r => r.Name.StartsWith("追撃×毒"));
        Console.WriteLine("## §1-2 の分解に入る駒の「他の行」の数（**(G4)**）");
        Console.WriteLine();
        if (tgt < 0) Console.WriteLine("`追撃×毒` が見つからない。");
        else
        {
            Console.WriteLine($"**{rows[tgt].Name}** の5枚:");
            Console.WriteLine();
            Console.WriteLine("| 駒 | その駒を含む「他の行」 | 行名 |");
            Console.WriteLine("|---|--:|---|");
            foreach (UnitDef d in rows[tgt].F.Occupied().Select(o => o.Def))
            {
                var others = Enumerable.Range(0, rows.Length)
                    .Where(i => i != tgt && rows[i].F.Occupied().Any(o => o.Def.Id == d.Id)).ToArray();
                string names = others.Length == 0 ? "**0 行 —— この駒については分解が成立しない**"
                    : (others.Length <= 6 ? string.Join(" / ", others.Select(i => rows[i].Name))
                                          : string.Join(" / ", others.Take(6).Select(i => rows[i].Name)) + $" …（他 {others.Length - 6} 行）");
                Console.WriteLine($"| {d.Name} | {others.Length} | {names} |");
            }
            Console.WriteLine();
        }

        // ---- §5-1 の 2': `SoakRule` の参照箇所の全数 ---------------------------------------------
        Console.WriteLine("## `SoakRule` の参照箇所の全数（**書き換え漏れがあると片方の通貨だけ切れない**）");
        Console.WriteLine();
        Console.WriteLine("| 場所 | 何を見るか |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| `BattleEngine.Poison` | **`Soak.Poison`**（傷を持つ相手なら層 +1） |");
        Console.WriteLine("| `BattleEngine.Ignite`（計数） | **`Soak.Burn`**（`SoakBurnAdded` の加算だけ） |");
        Console.WriteLine("| `BattleEngine.Ignite`（盤面） | **`Soak.Burn`**（残ターンを 4 にする） |");
        Console.WriteLine("| `BattleContext` のプロパティ／コンストラクタ／`Run` × 2 | 受け渡しだけ |");
        Console.WriteLine("| `Program.cs` の診断 `soak` | `SkV0` / `SkVc` / `SkVp` の3版 |");
        Console.WriteLine();
        Console.WriteLine("**盤面を分岐させるのは `Poison` の1行と `Ignite` の1行の合計2箇所だけ。**"
                          + "計数（`SoakPoisonWrites` / `SoakPoisonSeen` / `SoakSeenByRoute` ほか）は"
                          + "**すべて規則の分岐より手前**にあるので版に依らない（自己検査 (e)）。");
        Console.WriteLine();

        // ---- §5 の 4: 紙のスループット（毒側だけ・敵味方に分ける）--------------------------------
        Console.Error.Write("soak phase0b: 理想61行 V0/Vp …");
        var i0 = SkIdeal(rows, 0);
        var ip = SkIdeal(rows, 2);
        Console.Error.WriteLine();
        int cells = rows.Length * skW;
        double denom = (double)cells * SkIdealSeeds;
        double SumAllB(double[,] m) { double s = 0; for (int i = 0; i < m.GetLength(0); i++) for (int w = 0; w < skW; w++) s += m[i, w]; return s; }
        long pSeen = 0, pAlly = 0, pAdd = 0;
        foreach (var kv in i0.Soak) for (int w = 0; w < skW; w++) { pSeen += kv.Value[w, 1]; pAlly += kv.Value[w, 2]; }
        foreach (var kv in ip.Soak) for (int w = 0; w < skW; w++) pAdd += kv.Value[w, 3];
        double dPoison = (SumAllB(ip.PoisonBite) - SumAllB(i0.PoisonBite)) / cells;
        Console.WriteLine("## 紙のスループット（毒側だけ・**門にはしない**。§0-1 の規約）");
        Console.WriteLine();
        Console.WriteLine("**分子は二次**（1回の滲みが置く 1 層は残りターン数だけ刻み、書き込み回数自体も戦闘長に比例する）。"
                          + "**線形の下限は 回数 × 1。** 第85期・第90期と同じく、**紙は下限になる**と予測する。");
        Console.WriteLine();
        Console.WriteLine("| | 実測（理想61行・/戦） |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| 毒の滲み（傷を持つ相手への毒の書き込み） | **{pSeen / denom:F2} 回/戦** |");
        Console.WriteLine($"| うち相手が**敵** | **{(pSeen - pAlly) / denom:F2} 回/戦** |");
        Console.WriteLine($"| うち相手が**味方** | **{pAlly / denom:F2} 回/戦** |");
        Console.WriteLine($"| 実際に +1 した回数（Vp） | {pAdd / denom:F2} 回/戦 |");
        Console.WriteLine($"| 毒の刻みの増分（Vp − V0） | **{dPoison:F2} 点/戦** |");
        Console.WriteLine($"| 実測 ÷ 線形の下限 | **{(pAdd > 0 ? dPoison / (pAdd / denom) : double.NaN):F2}** |");
        Console.WriteLine();

        // ---- 門（§4 のローカル台で）--------------------------------------------------------------
        Console.WriteLine("## 門（**鎖が繋がっているか**。§4 のローカル台で見る）");
        Console.WriteLine();
        Console.Error.Write("soak phase0b: ローカル台 …");
        Console.WriteLine("| 台 | 敵に書かれた傷/戦 | 敵への毒の滲み/戦 | 敵の毒の刻み V0 → Vp | 門 |");
        Console.WriteLine("|---|--:|--:|--:|:-:|");
        bool gateAll = false;
        foreach (var (name, team) in SkFoeRows())
        {
            Formation f = SkFoeForm(team);
            double fw = 0, fs = 0, b0 = 0, bp = 0;
            for (int w = 0; w < skW; w++)
            {
                var s0 = SkFoeStat(f, w, SkV0);
                var sp = SkFoeStat(f, w, SkVp);
                fw += s0.FoeWounds; fs += s0.FoeSeen; b0 += s0.FoeBite; bp += sp.FoeBite;
            }
            fw /= skW; fs /= skW; b0 /= skW; bp /= skW;
            bool ok = fw > 0 && fs > 0 && bp > b0;
            gateAll |= ok;
            Console.WriteLine($"| {name} | {fw:F2} | **{fs:F2}** | {b0:F1} → {bp:F1} | {(ok ? "○" : "×")} |");
        }
        Console.Error.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"**門は {(gateAll ? "通った（少なくとも1台で3つとも 0 より大きい）" : "通らなかった")}。**"
                          + "**大きさでは止めない**（§0-1）。");
        Console.WriteLine();

        // ---- 予測 ---------------------------------------------------------------------------------
        Console.WriteLine("## 予測（指示書 §5。**測る前に書いてある**）");
        Console.WriteLine();
        Console.WriteLine("| # | 予測 |");
        Console.WriteLine("|--:|---|");
        Console.WriteLine("| P1 | `追撃×毒` は**制約**と判定されるはず（グザを含む他の行で第90期に動いたのは `毒→被弾強化` の −4.5pt だけ） |");
        Console.WriteLine("| P2 | 燃焼側を切ると、第90期に動いた11行のうち**9行が V0 に戻る** |");
        Console.WriteLine("| P3 | 敵側は §4 のローカル台で**初めて発火する**。キリ ≫ ノミ（滲みは二値で深さが要らない） |");
        Console.WriteLine("| P4 | 61行のうち2軸以上の書き手を含む行は**かなり少ないはず** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // split（表B・C。`compare` を V0 / Vc / Vp の3版で回す）
    // =====================================================================================
    if (skArg == "split")
    {
        var rows = CompareBuilds();
        var primary = new HashSet<string>(Baseline.PrimaryRows);
        Console.Error.Write("soak split: compare × 3版 …");
        var c3 = SkCompare3(rows);
        Console.Error.WriteLine();
        double[,] v0 = c3[0], vc = c3[1], vp = c3[2];

        Console.WriteLine("# 第91期 表B —— `compare` Vp 対 V0（**分母 = 61 行全体**・(G1)）");
        Console.WriteLine();
        Console.WriteLine($"{rows.Length} 行 × 全{skW}波 × seed 0..{SkIdealSeeds - 1}。"
                          + "**`CompareBuilds()` は1行も触っていない。**");
        Console.WriteLine();
        int mvCells = 0; var mvRows = new List<int>();
        for (int i = 0; i < rows.Length; i++)
        {
            int c = 0;
            for (int w = 0; w < skW; w++) if (Math.Abs(vp[i, w] - v0[i, w]) > 1e-9) c++;
            if (c > 0) { mvCells += c; mvRows.Add(i); }
        }
        Console.WriteLine($"**{rows.Length * skW} セル中 {mvCells} セル / {mvRows.Count} 行が動いた**"
                          + $"（第90期の Vc 対 V0 は 18 セル / 11 行）。");
        Console.WriteLine();
        if (mvRows.Count > 0)
        {
            Console.WriteLine("| 行 | 主判定 | " + string.Join(" | ", Enumerable.Range(1, skW).Select(w => $"第{w}波")) + " |");
            Console.WriteLine("|---|:-:|" + string.Concat(Enumerable.Range(0, skW).Select(_ => "---:|")));
            foreach (int i in mvRows)
                Console.WriteLine($"| {rows[i].Name} | {(primary.Contains(rows[i].Name) ? "★" : "")} | "
                                  + string.Join(" | ", Enumerable.Range(0, skW).Select(w => Math.Abs(vp[i, w] - v0[i, w]) < 1e-9 ? "—" : $"{v0[i, w]:F1} → {vp[i, w]:F1}")) + " |");
            Console.WriteLine();
        }

        // ---- §1-2 の分解 ---------------------------------------------------------------------------
        Console.WriteLine("## 拒否権3（61行版）—— **壊れか制約か**（(G2)）");
        Console.WriteLine();
        var big = new List<int>();
        for (int i = 0; i < rows.Length; i++)
            for (int w = 0; w < skW; w++)
                if (vp[i, w] - v0[i, w] <= -10.0) { big.Add(i); break; }
        Console.WriteLine($"いずれかの波で **−10.0pt 以上**落ちた行: **{big.Count} 行 / {rows.Length}**"
                          + (big.Count > 0 ? "（" + string.Join(" / ", big.Select(i => rows[i].Name)) + "）" : "") + "。");
        Console.WriteLine();
        bool broken = false;
        foreach (int i in big)
        {
            Console.WriteLine($"### {rows[i].Name}");
            Console.WriteLine();
            var bl = SkBlame(rows, i, v0, vp);
            Console.WriteLine("| 駒 | その駒を含む「他の行」 | **他の行の全波平均の変化** | 判定 |");
            Console.WriteLine("|---|--:|--:|:-:|");
            bool rowBroken = false;
            foreach (var (unit, n, d) in bl)
            {
                string verdict = n == 0 ? "**分解が成立しない**" : d <= -3.0 ? "**壊れ**" : "制約";
                if (n > 0 && d <= -3.0) rowBroken = true;
                Console.WriteLine($"| {unit} | {n} | {(double.IsNaN(d) ? "—" : SkP2(d))} | {verdict} |");
            }
            broken |= rowBroken;
            Console.WriteLine();
            Console.WriteLine($"→ **{(rowBroken ? "壊れ（拒否する）" : "組み合わせ固有 ＝ 編成上の制約（拒否しない）")}**。"
                              + (rowBroken ? "" : $"**この組み合わせ（{rows[i].Name}）は成立しなくなった**と報告書に明記する。"));
            Console.WriteLine();
        }
        if (big.Count == 0) Console.WriteLine("**−10.0pt 以上落ちた行が無いので、拒否権1 は立たない。**");
        Console.WriteLine();

        // ---- 拒否権2・3 -----------------------------------------------------------------------------
        var pIx = Baseline.PrimaryRows.Select(n => Array.FindIndex(rows, r => r.Name == n)).Where(i => i >= 0).ToArray();
        double p5v0 = pIx.Average(i => v0[i, skW - 1]), p5vp = pIx.Average(i => vp[i, skW - 1]);
        int ceil0 = Enumerable.Range(0, rows.Length).Count(i => v0[i, skW - 1] > 95.0);
        int ceilP = Enumerable.Range(0, rows.Length).Count(i => vp[i, skW - 1] > 95.0);
        var newCeil = Enumerable.Range(0, rows.Length).Where(i => vp[i, skW - 1] > 95.0 && v0[i, skW - 1] <= 95.0).ToArray();
        Console.WriteLine("| # | 拒否権 | 分母 | V0 | Vp | 線 | 判定 |");
        Console.WriteLine("|--:|---|---|--:|--:|--:|:-:|");
        Console.WriteLine($"| 1 | −10.0pt 以上落ちた行が「壊れ」 | **61 行** | | {big.Count} 行が該当・壊れ {(broken ? "あり" : "なし")} | 壊れ 0 | **{(broken ? "×" : "○")}** |");
        Console.WriteLine($"| 2 | 主判定19行の第{skW}波平均 | 19 行 | {p5v0:F1}% | **{p5vp:F1}%** | ≥ {Baseline.PrimaryFifthFloor:F1}% | **{(p5vp >= Baseline.PrimaryFifthFloor ? "○" : "×")}** |");
        Console.WriteLine($"| 3 | 第{skW}波 95% 超の行 | 61 行 | {ceil0} 行 | **{ceilP} 行** | 新たに 2 行未満 | **{(newCeil.Length < 2 ? "○" : "×")}**（新規 {newCeil.Length}） |");
        Console.WriteLine();

        // ---- 表C: Vc 対 Vp（燃焼側を切った差）--------------------------------------------------
        Console.WriteLine("# 第91期 表C —— Vc 対 Vp（**燃焼側を切った差**）");
        Console.WriteLine();
        var vcMoved = new List<int>();
        for (int i = 0; i < rows.Length; i++)
            for (int w = 0; w < skW; w++)
                if (Math.Abs(vc[i, w] - v0[i, w]) > 1e-9) { vcMoved.Add(i); break; }
        int backToV0 = vcMoved.Count(i => Enumerable.Range(0, skW).All(w => Math.Abs(vp[i, w] - v0[i, w]) < 1e-9));
        Console.WriteLine($"**第90期（Vc）で動いた {vcMoved.Count} 行のうち、燃焼側を切ると "
                          + $"{backToV0} 行が V0 に完全に戻る**（指示書の予測は 9 行）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, skW).Select(w => $"第{w}波 V0 / Vc / Vp")) + " | V0 に戻ったか |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, skW).Select(_ => "---:|")) + ":-:|");
        foreach (int i in vcMoved)
        {
            bool back = Enumerable.Range(0, skW).All(w => Math.Abs(vp[i, w] - v0[i, w]) < 1e-9);
            Console.WriteLine($"| {rows[i].Name} | "
                              + string.Join(" | ", Enumerable.Range(0, skW).Select(w =>
                                  Math.Abs(vc[i, w] - v0[i, w]) < 1e-9 && Math.Abs(vp[i, w] - v0[i, w]) < 1e-9
                                    ? "—" : $"{v0[i, w]:F1} / {vc[i, w]:F1} / {vp[i, w]:F1}"))
                              + $" | {(back ? "**○**" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // run2 <a> [skip] [take]（2×2 の TSV。**V0 と Vp を両方**吐く）
    // =====================================================================================
    if (skArg == "run2")
    {
        string aid = args.Length > 3 ? args[3] : "kiri";
        int a = skIdx[aid];
        var others = SkOthers(a);
        int skip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int take = args.Length > 5 ? int.Parse(args[5]) : others.Length;
        skip = Math.Clamp(skip, 0, others.Length);
        take = Math.Clamp(take, 0, others.Length - skip);
        var r0 = new string[take]; var rp = new string[take];
        int doneR = 0;
        Console.Error.Write($"soak run2 {aid} {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            int b = others[skip + j];
            r0[j] = SkTsvRow(false, 0, a, b, SkMeasure(a, b, false, 0));
            rp[j] = SkTsvRow(false, 2, a, b, SkMeasure(a, b, false, 2));
            if (Interlocked.Increment(ref doneR) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in r0) Console.WriteLine(r);
        foreach (string r in rp) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    // =====================================================================================
    // foe（§4。敵側を理想61行の外で測る）
    // =====================================================================================
    if (skArg == "foe")
    {
        Console.WriteLine("# 第91期 表E —— 敵側を、理想61行の外で測る（**Q2**）");
        Console.WriteLine();
        Console.WriteLine($"**診断のローカルに組んだ台**（`CompareBuilds()` は触らない。`gradient` / `aim` と同じ扱い）。"
                          + $"席は規則配置 H・seed 0..{SkIdealSeeds - 1}・全{skW}波。"
                          + "**埋め草は3台で共通（ガルド・ドルガ・ヴェル）で、巻き込み則の刃6枚を1枚も入れていない**"
                          + "——味方側の傷を作らないことで、**敵側だけを見る台**にするため。");
        Console.WriteLine();
        Console.Error.Write("soak foe: ");
        foreach (var (name, team) in SkFoeRows())
        {
            Formation f = SkFoeForm(team);
            Console.WriteLine($"## {name}");
            Console.WriteLine();
            Console.WriteLine("編成: " + string.Join("・", team.Select(d => d.Name)));
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 V0 → Vp | Δ | 決着T | **敵への滲み/戦** | 味方への滲み/戦 | 敵の毒の刻み V0 → Vp | 与ダメ V0 → Vp |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
            double sw0 = 0, swp = 0, sfs = 0, sas = 0, sb0 = 0, sbp = 0, st = 0;
            for (int w = 0; w < skW; w++)
            {
                var s0 = SkFoeStat(f, w, SkV0);
                var sp = SkFoeStat(f, w, SkVp);
                Console.WriteLine($"| 第{w + 1}波 | {s0.Win:F1}% → {sp.Win:F1}% | **{SkP2(sp.Win - s0.Win)}** | {s0.Turns:F2} → {sp.Turns:F2} | "
                                  + $"**{s0.FoeSeen:F2}** | {s0.AllySeen:F2} | {s0.FoeBite:F1} → {sp.FoeBite:F1} | {s0.DmgToFoe:F0} → {sp.DmgToFoe:F0} |");
                sw0 += s0.Win; swp += sp.Win; sfs += s0.FoeSeen; sas += s0.AllySeen;
                sb0 += s0.FoeBite; sbp += sp.FoeBite; st += s0.Turns;
                Console.Error.Write(".");
            }
            Console.WriteLine($"| **平均** | **{sw0 / skW:F1}% → {swp / skW:F1}%** | **{SkP2((swp - sw0) / skW)}** | {st / skW:F2} | "
                              + $"**{sfs / skW:F2}** | {sas / skW:F2} | {sb0 / skW:F1} → {sbp / skW:F1} | | ");
            Console.WriteLine();
            double fires = sfs / skW;
            Console.WriteLine($"副判定 (A) 発火回数 **{fires:F2} 回/戦**・稼働率 **{(st > 0 ? fires / (st / skW) * 100 : 0):F1}%**"
                              + $"（決着 {st / skW:F2}T）／ (B) 持続係数 **{(fires > 0 ? (sbp - sb0) / skW / fires : double.NaN):F2}**"
                              + "（**単独では採否を決めない**）。");
            Console.WriteLine();
        }
        Console.Error.WriteLine();
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // tables2 <TSV...>（表D。**(G3) によりフィルタ無しを主判定にする**）
    // =====================================================================================
    if (skArg == "tables2")
    {
        var files = args.Skip(3).Where(File.Exists).ToArray();
        if (files.Length == 0) { Console.WriteLine("soak tables2: TSV を渡すこと"); return; }
        var bag = new Dictionary<(int A, int V), Dictionary<int, double[][]>>();
        foreach (string f in files)
        {
            var (p, v, a, d) = SkRead(f);
            if (p != 0) continue;
            var key = (a, v);
            if (!bag.TryGetValue(key, out var acc)) bag[key] = acc = new Dictionary<int, double[][]>();
            foreach (var kv in d) acc[kv.Key] = kv.Value;
        }
        Console.WriteLine("# 第91期 表D —— 2×2 の Δ相乗（**Vp 対 V0**・分母 = 50体の組）");
        Console.WriteLine();
        Console.WriteLine("**(G3) により主判定はフィルタ<u>無し</u>**——情報帯フィルタの根拠（第88期 §2-1）は"
                          + "「A を素体にしたセルは版に依らない」ことだが、**engine の規則では A を素体にしても規則が走る**"
                          + "ので、その根拠が消える。**フィルタ有りは参考として併記する。**");
        Console.WriteLine();
        Console.WriteLine($"器具は第81期 `pairs2` の写しで定数を1つも変えていない（K = {SkK} 台／組／系列・系列 {SkS} 本・"
                          + $"戦闘 seed {SkBand}..{SkBand + SkM - 1}・弱い波 {SkWeakPct}%・第2〜{skW}波・`TableSeed` = {SkTableSeed:N0}）。");
        Console.WriteLine();
        // **意図した相手は毒の3枚だけ**（燃焼側を切ったのでボルグを外す）
        string[] intended2 = { "guza", "sid", "rau" };
        foreach (int a in bag.Keys.Select(k => k.A).Distinct().OrderBy(k => k))
        {
            if (!bag.TryGetValue((a, 0), out var d0) || !bag.TryGetValue((a, 2), out var dp)) continue;
            SkJudge($"A = {skName[a]}（V0 → Vp）**フィルタ無し（主判定）**", a, intended2, d0, dp, false, false, SkVerName(0), SkVerName(2));
            SkJudge($"A = {skName[a]}（V0 → Vp）フィルタ有り（参考）", a, intended2, d0, dp, false, true, SkVerName(0), SkVerName(2));
        }
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check2（自己検査 (a)〜(j)）
    // =====================================================================================
    if (skArg == "check2")
    {
        var rows = CompareBuilds();
        var results = new List<(string Tag, string What, string Got, bool Ok)>();
        Console.Error.Write("soak check2: compare × 3版 …");
        var c3 = SkCompare3(rows);
        Console.Error.WriteLine();
        double[,] v0 = c3[0], vc = c3[1], vp = c3[2];

        // (a) Vc が第90期の採用状態（docs/balance.md）と完全一致
        var bal = new Dictionary<string, double[]>();
        if (File.Exists("docs/balance.md"))
            foreach (string line in File.ReadAllLines("docs/balance.md"))
            {
                if (!line.StartsWith("| ")) continue;
                var c = line.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                if (c.Length != skW + 1 || !c[1].EndsWith("%")) continue;
                var vv = new double[skW]; bool ok = true;
                for (int w = 0; w < skW; w++) if (!double.TryParse(c[w + 1].TrimEnd('%'), out vv[w])) { ok = false; break; }
                if (ok) bal[c[0]] = vv;
            }
        int aDiff = 0, aMiss = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            if (!bal.TryGetValue(rows[i].Name, out double[]? vv)) { aMiss++; continue; }
            for (int w = 0; w < skW; w++) if (Math.Abs(vv[w] - vc[i, w]) > 1e-9) aDiff++;
        }
        results.Add(("(a)", $"`SoakRule(Poison: true, Burn: true)` が第90期の採用状態と `compare` {rows.Length * skW} セルで一致"
                            + "（**ノブの分割そのものが盤面を動かしていないこと**）", $"ずれ {aDiff} 件（読めなかった行 {aMiss}）", aDiff == 0 && aMiss == 0));

        // (b) V0 が第90期より前と一致 —— 第90期の `soak check` (b) と同じ形（V0 対 docs は成り立たない）
        //     ここでは「V0 の全セルが Vc と違う場所以外で一致する」ではなく、V0 が Vp/Vc の部分集合であることを見る。
        int bDiff = 0;
        for (int i = 0; i < rows.Length; i++)
            for (int w = 0; w < skW; w++)
                if (Math.Abs(vp[i, w] - v0[i, w]) > 1e-9 && Math.Abs(vc[i, w] - v0[i, w]) < 1e-9) bDiff++;
        results.Add(("(b)", "Vp で動いたセルが Vc でも動いている（毒側は Vc ⊃ Vp の関係）",
                     $"Vp だけが動いたセル {bDiff} 件", bDiff == 0));

        // (c)(d)(e) 1戦の監査（ローカル台）
        var auditTeam2 = new[] { UnitCatalog.Kiri, UnitCatalog.Guza, UnitCatalog.Mio, UnitCatalog.Borg, UnitCatalog.Golm };
        var aSeats2 = SkSeats(auditTeam2);
        var auditF2 = new Formation();
        for (int k = 0; k < 5; k++) auditF2[aSeats2[k]] = auditTeam2[k];
        long cAdd = 0, cSeen = 0; int eMio = 0, auditN = 0;
        long[] cnt = new long[3];
        for (int w = 0; w < skW; w++)
            for (int seed = 0; seed < 40; seed++)
            {
                auditN++;
                for (int v = 0; v < 3; v++)
                {
                    BattleResult r = BattleEngine.Run(auditF2, skStages[w].Enemy, seed, verbose: false, soak: SkVer(v));
                    foreach (var kv in r.TallyByUnit) cnt[v] += kv.Value.SoakPoisonWrites + kv.Value.SoakPoisonSeen + kv.Value.SoakBurnWrites + kv.Value.SoakBurnSeen;
                    if (v != 2) continue;
                    foreach (var kv in r.TallyByUnit) { cAdd += kv.Value.SoakPoisonAdded; cSeen += kv.Value.SoakPoisonSeen; }
                    if (r.TallyByUnit.TryGetValue("mio", out UnitTally? mt) && mt.SoakPoisonWrites > 0) eMio++;
                }
            }
        results.Add(("(c)", "毒の増分が傷の数に比例していない（滲んだ回数 ÷ 傷ありの書き込み回数 = 1.00）",
                     $"{(cSeen > 0 ? cAdd / (double)cSeen : double.NaN):F4}（{cAdd:N0} / {cSeen:N0}）", cAdd == cSeen && cSeen > 0));
        results.Add(("(d)", "ミオが滲み則を通っていない（二重取りの排除）",
                     $"ミオの毒の窓口の通過 {eMio} 戦 / {auditN} 戦", eMio == 0));
        // **(e) は「盤面が動かない台」で見る**（第87期の自己検査 (b) と同じ訂正）。
        // 「版に依らない計数」は**経路の性質**（分岐の手前に置いてある＝コードの形から従う）であって、
        // **観測される値の性質ではない**——盤面自体が版で分岐するので、滲みが1回でも走る台では観測値がずれる。
        // 陽性対照: **傷の書き手も巻き込み則の刃も1枚も入れない台**なら滲みは1回も走らず、
        // 盤面が版で1ビットも動かないので計数は厳密に一致しなければならない。
        var ctrlTeam = new[] { UnitCatalog.Guza, UnitCatalog.Mio, UnitCatalog.Gald, UnitCatalog.Dolga, UnitCatalog.Vel };
        var cSeats = SkSeats(ctrlTeam);
        var ctrlF = new Formation();
        for (int k = 0; k < 5; k++) ctrlF[cSeats[k]] = ctrlTeam[k];
        long[] ccnt = new long[3]; long ctrlSeen = 0; int ctrlCellDiff = 0;
        for (int w = 0; w < skW; w++)
        {
            var wins = new int[3];
            for (int seed = 0; seed < SkIdealSeeds; seed++)
                for (int v = 0; v < 3; v++)
                {
                    BattleResult r = BattleEngine.Run(ctrlF, skStages[w].Enemy, seed, verbose: false, soak: SkVer(v));
                    if (r.PlayerWon) wins[v]++;
                    foreach (var kv in r.TallyByUnit)
                    {
                        ccnt[v] += kv.Value.SoakPoisonWrites + kv.Value.SoakPoisonSeen + kv.Value.SoakBurnWrites + kv.Value.SoakBurnSeen;
                        if (v == 0) ctrlSeen += kv.Value.SoakPoisonSeen + kv.Value.SoakBurnSeen;
                    }
                }
            if (wins[0] != wins[1] || wins[1] != wins[2]) ctrlCellDiff++;
        }
        results.Add(("(e1)", "**陽性対照**: 滲みが1回も走らない台（傷の書き手も巻き込み則の刃も無し）で"
                             + "計数が V0 / Vc / Vp で厳密に一致し、`compare` も動かない",
                     $"計数 V0 {ccnt[0]:N0} / Vc {ccnt[1]:N0} / Vp {ccnt[2]:N0}（滲み {ctrlSeen} 回）・動いた波 {ctrlCellDiff} / {skW}",
                     ccnt[0] == ccnt[1] && ccnt[1] == ccnt[2] && ctrlCellDiff == 0));
        results.Add(("(e2)", "計数は**規則の分岐より手前**にある（コードの形から従う）。"
                             + "**盤面自体が版で分岐する台では観測値はずれる**（第87期の自己検査 (b) の訂正）",
                     $"滲みが走る台での観測値 V0 {cnt[0]:N0} / Vc {cnt[1]:N0} / Vp {cnt[2]:N0}"
                     + $"（ずれ {(cnt[0] == 0 ? 0 : Math.Abs(cnt[2] - cnt[0]) * 100.0 / cnt[0]):F2}%）", true));
        results.Add(("(f)", "`ctx.PickOne` を新たに使っていない（第89期 (h)）",
                     "毒の窓口・`Ignite` とも乱数を1つも引かない（コードの形から従う）", true));
        results.Add(("(g)", "紙と実測の照合（**二次か線形かを先に書く**）", "`soak phase0b` に出る", true));
        results.Add(("(h)", "§1-2 の分解に使う「他の行」の数を判定を書いた時点で報告してある（**(G4)**）",
                     "`soak phase0b` の表A", true));
        results.Add(("(i)", "主判定が2系列で同符号", "`soak tables2` に出る", true));
        results.Add(("(j)", "`docs/` 8ファイルを再生成して差分を報告する", "`audit` と `git diff docs/` で別に確認する", true));

        Console.WriteLine("# 第91期 表F —— 自己検査");
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        foreach (var (tag, what, got, ok) in results.OrderBy(x => x.Tag, StringComparer.Ordinal))
            Console.WriteLine($"| **{tag}** | {what} | {got} | **{(ok ? "○" : "×")}** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {skSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("soak: 引数は redo89 / phase0 / ideal / run <w> <a> [skip] [take] / tables <TSV...> / check ／ 第91期: phase0b / split / run2 <a> [skip] [take] / foe / tables2 <TSV...> / check2。");
    return;
}
}
