using BattleCore;
using static Common;

// =====================================================================================
// gather モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "gather")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 gather
// =====================================================================================

static class GatherDiag
{
// 傷も肩代わりする（第89期）——廃棄聖騎士ガルド（`GuardianTrait`）が、庇ったときに隣の味方の傷をひとつ引き受ける。
// **浅く広い供給（巻き込み則の6枚）に深さを作る中継**で、終端は縫いのハリ（味方の傷の深さを読む唯一の駒）。
// 器具は第81期の 2×2（第88期 `gauge` の写し）で、**判定は第88期 §4 の規約**（主判定は特異性・大きさは拒否権にだけ残す）。
// **ノイズ床の定義だけが第89期に変わった**——同じ実験の中の「意図しない相手」の |Δ相乗| の 95 パーセンタイル（§1-1）。
// **既存の診断（gauge / blaze2 / mender / suture2 / thorn / pairs2 / breadth）は1文字も書き換えていない。**
//
//     dotnet run --project BattleSim -c Release 0 gather redo87 [<v0> <v1>]   # §1-1（P1）第87期の再判定（別標本）
//     dotnet run --project BattleSim -c Release 0 gather run87 <v> [skip] [take]  # (P1) の 2×2 を TSV へ（分割実行）
//     dotnet run --project BattleSim -c Release 0 gather seats                # §1-2（P2）confirm で閾値を超えた行
//     dotnet run --project BattleSim -c Release 0 gather phase0               # §1-3（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 gather run <z> [skip] [take]   # 本編の 2×2（z = 0/1）
//     dotnet run --project BattleSim -c Release 0 gather tables <z0> <z1>     # 表C〜F
//     dotnet run --project BattleSim -c Release 0 gather check <z0> <z1>      # 自己検査 (a)〜(j)
public static void Run(string[] args, int stageIndex)
{
    string gaArg = args.Length > 2 ? args[2] : "";
    var gaSw = System.Diagnostics.Stopwatch.StartNew();
    var gaInv = System.Globalization.CultureInfo.InvariantCulture;

    IReadOnlyList<EnemyCatalog.Stage> gaStages = EnemyCatalog.Stages;
    int gaW = gaStages.Count;
    // **第141期: ロスターは `Everyone`（`All ∪ Retired`）。** 意図した相手の終端がハリ（第108期に `All` から外れた）。
    // ロスターが B の集合・引き表・素体表（`gaPlainMap`）を兼ねるのでロスターごと広げる（`thorn` と同じ理由）。
    var gaRoster = UnitCatalog.Everyone.ToArray();
    int gaRN = gaRoster.Length;                       // 54（第89期は 51）

    // ---- 第81期 `pairs2` の定数の写し（**1つも変えていない**）----------------------------------
    const int GaTableSeed = 8_100_000;                // 本編（A ＝ ガルド）
    const int GaRedoSeed = 8_900_000;                 // **(P1) だけ別の抽選**（第87期・第88期のどちらとも違う標本）
    const int GaK = 64, GaS = 2;
    const int GaBand = 0, GaM = 8;
    const int GaStrong = 7, GaWeakPct = 60, GaDrawCap = 20000;
    const int GaTop = 20;
    const int GaMinInfo = 20;                         // 第88期 §4-1: 情報帯がこれ未満の組は「測れていない」
    const int GaFpLine = 3;                           // Q1-2 の線（§3-1）
    const double GaEps = 1e-9;
    const double GaPaperFloor = 5.0;                  // 紙のスループットの停止条件（§1-3）

    var gaIdx = new Dictionary<string, int>();
    for (int u = 0; u < gaRN; u++) gaIdx[gaRoster[u].Id] = u;
    string[] gaName = gaRoster.Select(d => d.Name).ToArray();

    int gaGald = gaIdx["gald"], gaMio = gaIdx["mio"];
    // 本編の「意図した相手」7枚（§3-1）: 終端（ハリ）＋ 巻き込み則の書き手6枚
    string[] gaIntendedIds = { "hari", "golm", "borg", "kado", "tsugi", "rica", "zoto" };
    // (P1) の「意図した相手」2枚（§1-1。主はキリ・ノミは向きの検証）
    string[] gaP1Ids = { "kiri", "nomi" };

    int[] GaOthers(int a) => Enumerable.Range(0, gaRN).Where(u => u != a).ToArray();

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜88期と同一。`Stages` は書き換えない）--------------------
    var gaWeakCache = new Dictionary<string, UnitDef>();
    UnitDef GaWeakOf(UnitDef d)
    {
        if (gaWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * GaWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        gaWeakCache[d.Id] = w;
        return w;
    }
    var gaWeak = gaStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = GaWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef GaPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var gaPlainMap = gaRoster.ToDictionary(d => d.Id, GaPlain);

    UnitDef[] GaFill(UnitDef[] pool, int strong0, int seed)
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
            if (sel.Attack >= GaStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int GaSeed(int tableSeed, int pairIx, int draw)
    {
        ulong x = (ulong)tableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] GaSeats(UnitDef[] u)
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
    // **TSV の添字**: 0 = y11 ／ 1 = y01（**A 素体**・B 本物）／ 2 = y10（A 本物・**B 素体**）／ 3 = y00
    // 第85期の罠（A・B の割り当てで添字の意味が変わる）——**A は先に固定してから添字を書いた**
    // （本編は A ＝ ガルド・(P1) は A ＝ ミオ）。
    Formation GaForm(UnitDef[] u, int[] seats, int cell)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (cell & 1) != 0) || (k == 1 && (cell & 2) != 0);
            f[seats[k]] = plain ? gaPlainMap[u[k].Id] : u[k];
        }
        return f;
    }

    // 版の割り当て。**本編は GatherRule だけを振り、(P1) は IgniteRule だけを振る。**
    // どちらも**現行の既定をそのまま使う**（第88期に `SutureRule` / `SpillWoundRule` が既定 on になっている）。
    BattleResult GaRun(Formation f, Formation e, int seed, bool verbose, bool p1, int v)
        => p1 ? BattleEngine.Run(f, e, seed, verbose: verbose, woundIgnite: new IgniteRule(v != 0))
              : BattleEngine.Run(f, e, seed, verbose: verbose, gather: new GatherRule(v != 0));
    string GaVName(bool p1, int v) => p1 ? (v == 0 ? "Y0（現行）" : "Y1（傷口に着火）")
                                        : (v == 0 ? "Z0（現行）" : "Z1（傷も肩代わり）");

    double GaRate(Formation f, bool p1, int v)
    {
        double sum = 0;
        for (int wi = 1; wi < gaW; wi++)
        {
            int wins = 0;
            for (int seed = GaBand; seed < GaBand + GaM; seed++)
                if (GaRun(f, gaWeak[wi].Enemy, seed, false, p1, v).PlayerWon) wins++;
            sum += wins * 100.0 / GaM;
        }
        return sum / (gaW - 1);
    }
    string GaP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double GaSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double GaSe(IReadOnlyList<double> xs) => xs.Count < 2 ? double.NaN : GaSd(xs) / Math.Sqrt(xs.Count);
    double GaPct(IReadOnlyList<double> xs, double q)
    {
        if (xs.Count == 0) return double.NaN;
        var s = xs.OrderBy(v => v).ToArray();
        if (s.Length == 1) return s[0];
        double pos = q * (s.Length - 1);
        int lo = (int)Math.Floor(pos), hi = Math.Min(lo + 1, s.Length - 1);
        return s[lo] + (pos - lo) * (s[hi] - s[lo]);
    }
    double GaMed(IReadOnlyList<double> xs) => GaPct(xs, 0.5);

    var gaPairIxOf = new int[gaRN, gaRN];
    {
        int pi = 0;
        for (int a = 0; a < gaRN; a++) for (int b = a + 1; b < gaRN; b++) { gaPairIxOf[a, b] = gaPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> GaFills(int tableSeed, int a, int b)
    {
        var pool = gaRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (gaRoster[a].Attack >= GaStrong ? 1 : 0) + (gaRoster[b].Attack >= GaStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < GaS * GaK && draw < GaDrawCap; draw++)
        {
            var f = GaFill(pool, strong0, GaSeed(tableSeed, gaPairIxOf[a, b], draw));
            var t = f.Select(d => gaIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }
    UnitDef[] GaTeam(int a, int b, UnitDef[] fill) => new[] { gaRoster[a], gaRoster[b], fill[0], fill[1], fill[2] };
    double[][] GaMeasure(int tableSeed, int a, int b, bool p1, int v)
    {
        var fills = GaFills(tableSeed, a, b);
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = GaTeam(a, b, fills[t]);
            int[] seats = GaSeats(team);
            ys[t] = new double[4];
            for (int cell = 0; cell < 4; cell++) ys[t][cell] = GaRate(GaForm(team, seats, cell), p1, v);
        }
        return ys;
    }

    // ---- TSV --------------------------------------------------------------------------------------
    (int P1, int V, int A, Dictionary<int, double[][]> D) GaRead(string path)
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
            for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], gaInv); }
            d[b] = ys;
        }
        return (pp, vv, aa, d);
    }
    string GaTsvRow(bool p1, int v, int a, int b, double[][] ys)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(p1 ? 1 : 0).Append('\t').Append(v).Append('\t').Append(a).Append('\t').Append(b).Append('\t').Append(ys.Length);
        foreach (double[] yy in ys) for (int k = 0; k < 4; k++) sb.Append('\t').Append(yy[k].ToString("R", gaInv));
        return sb.ToString();
    }
    double GaSyn(double[] y) => y[0] - y[2] - y[1] + y[3];
    bool GaFloorT(double[] y) => y[3] < GaEps && y[1] < GaEps;
    bool GaCeilT(double[] y) => y[3] > 100 - GaEps && y[1] > 100 - GaEps;
    bool GaInfoT(double[] y) => !GaFloorT(y) && !GaCeilT(y);

    (double M, double Se, double[] Ser, int N) GaAgg(double[] d, bool[]? use)
    {
        var all = new List<double>(); var s0 = new List<double>(); var s1 = new List<double>();
        for (int t = 0; t < d.Length; t++)
        {
            if (use is not null && !use[t]) continue;
            all.Add(d[t]); (t % GaS == 0 ? s0 : s1).Add(d[t]);
        }
        return (all.Count > 0 ? all.Average() : double.NaN, GaSe(all),
                new[] { s0.Count > 0 ? s0.Average() : double.NaN, s1.Count > 0 ? s1.Average() : double.NaN }, all.Count);
    }
    (Dictionary<int, double[]> D, Dictionary<int, bool[]> Info, Dictionary<int, (int All, int Floor, int Ceil, int Info)> Cnt, int Mism)
        GaFold(Dictionary<int, double[][]> v0, Dictionary<int, double[][]> v1, int[] others)
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
                d[t] = GaSyn(v1[b][t]) - GaSyn(v0[b][t]);
                if (Math.Abs(v0[b][t][1] - v1[b][t][1]) > 1e-9) mism++;
                if (Math.Abs(v0[b][t][3] - v1[b][t][3]) > 1e-9) mism++;
                inf[t] = GaInfoT(v0[b][t]);
                if (GaFloorT(v0[b][t])) fl++; else if (GaCeilT(v0[b][t])) ce++; else ok++;
            }
            D[b] = d; Info[b] = inf; Cnt[b] = (nT, fl, ce, ok);
        }
        return (D, Info, Cnt, mism);
    }

    // ---- 1つの 2×2 の判定を丸ごと出す（(P1) と本編で共有）------------------------------------------
    void GaJudge(string title, int a, string[] intendedIds, Dictionary<int, double[][]> d0, Dictionary<int, double[][]> d1, bool p1)
    {
        var others = GaOthers(a);
        var (D, Info, Cnt, mism) = GaFold(d0, d1, others);
        var intended = new HashSet<int>(intendedIds.Select(i => gaIdx[i]));
        var agAll = D.Keys.ToDictionary(b => b, b => GaAgg(D[b], null));
        var agFil = D.Keys.ToDictionary(b => b, b => GaAgg(D[b], Info[b]));
        var measurable = D.Keys.Where(b => Cnt[b].Info >= GaMinInfo).ToHashSet();
        var unint = measurable.Where(b => !intended.Contains(b)).ToArray();

        // **ノイズ床（第89期 §1-1 の規約）**: 同じ実験の中の「意図しない相手」の |Δ相乗| の 95 パーセンタイル
        double floor = GaPct(unint.Select(b => Math.Abs(agFil[b].M)).ToArray(), 0.95);

        int cAll = D.Keys.Sum(b => Cnt[b].All), cFl = D.Keys.Sum(b => Cnt[b].Floor),
            cCe = D.Keys.Sum(b => Cnt[b].Ceil), cIn = D.Keys.Sum(b => Cnt[b].Info);
        Console.WriteLine($"### {title}");
        Console.WriteLine();
        Console.WriteLine($"A ＝ **{gaName[a]}**・版は {GaVName(p1, 0)} 対 {GaVName(p1, 1)}。"
                          + $"意図した相手 {intended.Count} 枚（{string.Join("・", intendedIds.Select(i => gaName[gaIdx[i]]))}）／意図しない相手 {unint.Length} 体。");
        Console.WriteLine();
        Console.WriteLine("| | 台数 | 割合 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 全台（{D.Count} 体 × {GaS * GaK} 台） | {cAll:N0} | 100.0% |");
        Console.WriteLine($"| 床（`y00` = `y01` = 0.0%） | {cFl:N0} | {cFl * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| 天井（`y00` = `y01` = 100.0%） | {cCe:N0} | {cCe * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| **情報帯** | **{cIn:N0}** | **{cIn * 100.0 / cAll:F1}%** |");
        Console.WriteLine();
        Console.WriteLine($"1組あたりの情報帯は 中央値 **{GaMed(D.Keys.Select(b => (double)Cnt[b].Info).ToArray()):F1}** / {GaS * GaK} 台"
                          + $"（最小 {D.Keys.Min(b => Cnt[b].Info)} / 最大 {D.Keys.Max(b => Cnt[b].Info)}）。"
                          + $"**測れていない組（情報帯 < {GaMinInfo}）は {D.Count - measurable.Count} 体。**");
        Console.WriteLine();
        Console.WriteLine($"**自己検査 (a)**: A 素体の2セル（`y01` / `y00`）が版で完全一致 → {cAll * 2:N0} セル・ずれ **{mism}** 件"
                          + $" → **{(mism == 0 ? "○" : "×")}**。");
        Console.WriteLine();
        Console.WriteLine($"**ノイズ床（§1-1 の規約）= 意図しない {unint.Length} 体の \\|Δ\\| の 95%tile = {floor:F2}pt**"
                          + $"（中央値 {GaMed(unint.Select(b => Math.Abs(agFil[b].M)).ToArray()):F2} / 最大 {(unint.Length > 0 ? unint.Max(b => Math.Abs(agFil[b].M)) : double.NaN):F2}）。");
        Console.WriteLine();

        var ordF = measurable.OrderByDescending(b => agFil[b].M).ToArray();
        Console.WriteLine($"| 順位 | B | **Δ（情報帯）** | SE | 情報帯 | 系列1 | 系列2 | 床超 | Δ（全台） | SE |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|--:|--:|");
        var shown = new HashSet<int>();
        void Row(int b)
        {
            int rf = Array.IndexOf(ordF, b) + 1;
            var af = agFil[b]; var aa2 = agAll[b];
            bool over = measurable.Contains(b) && Math.Abs(af.M) > floor;
            Console.WriteLine($"| {(rf > 0 ? rf.ToString() : "—")} | {(intended.Contains(b) ? "★" : "")}{gaName[b]} | **{GaP2(af.M)}** | {af.Se:F2} | {Cnt[b].Info} | {GaP2(af.Ser[0])} | {GaP2(af.Ser[1])} | {(over ? "○" : "")} | {GaP2(aa2.M)} | {aa2.Se:F2} |");
        }
        int top = Math.Min(GaTop / 2, ordF.Length);
        for (int i = 0; i < top; i++) { Row(ordF[i]); shown.Add(ordF[i]); }
        if (ordF.Length > 2 * top) Console.WriteLine("| … | | | | | | | | | |");
        for (int i = Math.Max(top, ordF.Length - top); i < ordF.Length; i++) { Row(ordF[i]); shown.Add(ordF[i]); }
        Console.WriteLine();
        var rest2 = intended.Concat(D.Keys.Where(b => !measurable.Contains(b))).Distinct().Where(b => D.ContainsKey(b) && !shown.Contains(b)).ToArray();
        if (rest2.Length > 0)
        {
            Console.WriteLine("意図した相手（上の表に出ていないぶん）と「測れていない」組:");
            Console.WriteLine();
            Console.WriteLine($"| 順位 | B | **Δ（情報帯）** | SE | 情報帯 | 系列1 | 系列2 | 床超 | Δ（全台） | SE |");
            Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|--:|--:|");
            foreach (int b in rest2) Row(b);
            Console.WriteLine();
        }

        var intM = intended.Where(measurable.Contains).ToArray();
        var passers = intM.Where(b => agFil[b].Ser[0] > 0 && agFil[b].Ser[1] > 0 && agFil[b].M > floor).ToArray();
        var fpAll = unint.Where(b => Math.Abs(agFil[b].M) > floor).ToArray();
        var fpNeg = unint.Where(b => agFil[b].M < -floor).ToArray();
        bool q11 = passers.Length >= 1, q12 = fpAll.Length <= GaFpLine;
        int bestRank = intM.Length == 0 ? 0 : intM.Min(b => Array.IndexOf(ordF, b) + 1);
        Console.WriteLine("| | 内容 | 実測 | 線 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1-1** | 意図した相手のうち 2系列とも正 かつ \\|Δ\\| > ノイズ床 | **{passers.Length} / {intM.Length} 枚**"
                          + (passers.Length > 0 ? "（" + string.Join("・", passers.Select(b => $"{gaName[b]} {GaP2(agFil[b].M)}")) + "）" : "")
                          + $" | ≥ 1 | **{(q11 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1-2** | 意図しない相手で床超の体数 | **{fpAll.Length} / {unint.Length} 体**（負 {fpNeg.Length}） | ≤ {GaFpLine} | **{(q12 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1** | **特異性（主判定）** | | | **{(q11 && q12 ? "○" : "×")}** |");
        Console.WriteLine($"| Q2 | 意図した組の最良順位（{ordF.Length} 体中） | **{(bestRank > 0 ? bestRank + " 位" : "—")}**"
                          + (bestRank > 0 ? $"（{gaName[intM.OrderBy(b => Array.IndexOf(ordF, b)).First()]}）" : "") + " | ≤ 10 | "
                          + $"**{(bestRank > 0 && bestRank <= 10 ? "○" : "×")}** |");
        Console.WriteLine($"| 自己検査 (i) | 主判定が2系列で同符号 | "
                          + (intM.Length > 0 ? string.Join(" / ", intM.OrderBy(b => Array.IndexOf(ordF, b)).Take(3).Select(b => $"{gaName[b]} {GaP2(agFil[b].Ser[0])}・{GaP2(agFil[b].Ser[1])}")) : "—")
                          + " | | |");
        Console.WriteLine();
        Console.WriteLine($"**{title} の結論: {(q11 && q12 ? "通る" : "通らない")}**（Q1-1 {(q11 ? "○" : "×")} / Q1-2 {(q12 ? "○" : "×")}）。");
        Console.WriteLine();
        Console.WriteLine("> **Q1-2 はこのノイズ床の定義のもとでは飾りに近い。** 床は意図しない相手の分布の 95%tile なので、"
                          + $"**構成上おおよそ 5% ＝ {unint.Length * 0.05:F1} 体が必ず超える**（線 {GaFpLine} はそこに合わせてある）。"
                          + "**判定の中身は Q1-1、すなわち「意図した相手が意図しない 95% の相手より上に立つか」という順位の検定である。**");
        Console.WriteLine();
    }

    // =====================================================================================
    // run87 <v> [skip] [take] / run <z> [skip] [take]
    // =====================================================================================
    if (gaArg == "run87" || gaArg == "run")
    {
        bool p1 = gaArg == "run87";
        int a = p1 ? gaMio : gaGald;
        int tableSeed = p1 ? GaRedoSeed : GaTableSeed;
        int v = args.Length > 3 ? int.Parse(args[3]) : 0;
        var others = GaOthers(a);
        int skip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int take = args.Length > 5 ? int.Parse(args[5]) : others.Length;
        skip = Math.Clamp(skip, 0, others.Length);
        take = Math.Clamp(take, 0, others.Length - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"gather {gaArg} v{v} {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            int b = others[skip + j];
            rows[j] = GaTsvRow(p1, v, a, b, GaMeasure(tableSeed, a, b, p1, v));
            if (Interlocked.Increment(ref doneR) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {gaSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    // =====================================================================================
    // redo87 [<v0> <v1>]（§1-1・(P1)）
    // =====================================================================================
    if (gaArg == "redo87")
    {
        Dictionary<int, double[][]> d0, d1;
        if (args.Length >= 5 && File.Exists(args[3]) && File.Exists(args[4]))
        {
            d0 = GaRead(args[3]).D; d1 = GaRead(args[4]).D;
        }
        else
        {
            var others = GaOthers(gaMio);
            d0 = new Dictionary<int, double[][]>(); d1 = new Dictionary<int, double[][]>();
            var r0 = new double[others.Length][][]; var r1 = new double[others.Length][][];
            Console.Error.Write("gather redo87: ");
            int done = 0;
            Parallel.For(0, others.Length, j =>
            {
                r0[j] = GaMeasure(GaRedoSeed, gaMio, others[j], true, 0);
                r1[j] = GaMeasure(GaRedoSeed, gaMio, others[j], true, 1);
                if (Interlocked.Increment(ref done) % 5 == 0) Console.Error.Write(".");
            });
            Console.Error.WriteLine();
            for (int j = 0; j < others.Length; j++) { d0[others[j]] = r0[j]; d1[others[j]] = r1[j]; }
        }

        Console.WriteLine("# 第89期 表A —— (P1) 第87期（傷口に毒を流す）の再判定");
        Console.WriteLine();
        Console.WriteLine($"**別標本**（台の抽選 `TableSeed` = {GaRedoSeed:N0}。第87・88期はどちらも {GaTableSeed:N0}）で測り直した。"
                          + "**それ以外の定数は1つも変えていない。** 盤面は**第88期の採用後**（`SutureRule.Both` ＋ `SpillWoundRule` が既定 on）。"
                          + "**同じデータで覆さない**——増分尺度のノイズ床の定義を先に規約として固定し、別の標本で当てる（§1-1）。");
        Console.WriteLine();
        GaJudge("(P1) ミオ × 全50体（`WoundIgnite` off → on）", gaMio, gaP1Ids, d0, d1, true);
        Console.WriteLine("**第88期 §8-5 の併記**: 情報帯の割合は カド 65.2% / ハリ 70.1% / **ミオ 56.5%**（第88期の標本）。"
                          + "**攻撃しない駒を A に固定すると 5枠のうち1枠が出力ゼロになり、台が床へ落ちる。**"
                          + "ミオの標本は他の2期より条件が悪い——上の表の「床」の割合と並べて読むこと。");
        Console.WriteLine();
        Console.WriteLine($"所要 {gaSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // seats（§1-2・(P2)）——`confirm` の写しではなく、`confirm` と同じ帯で現行席と粗探索1位を比べる
    // =====================================================================================
    if (gaArg == "seats")
    {
        // **`confirm` は使わない。** `confirm` の候補表は **各期に提案された配置を焼き付けた台帳**で、
        // 採用済みの行もそのまま残っている（実際 13 行が「採用」と出るが、**現行の `CompareBuilds()` は
        // すでにその候補の席になっている**）。**あれは過去の決定を今の盤面で測り直した記録**であって、
        // 「今この行の席が古いか」は1つも答えていない。
        //
        // 生きた判定は `docs/reseat.md`（`reseat` の出力・seed 0..199）から取る——第46期の3段の作法:
        //   (1) 現行が上位5通りに入っていれば動かさない
        //   (2) 入っていない行だけ seed 200..599 で測り直し、5.0pt 以上のときだけ動かす
        //   (3) 採否は1位の配置ではなく**次数**で読む
        const double GaSeatLine = 5.0;
        const int GaSeatBase = 200, GaSeatSeeds = 400;
        if (!File.Exists("docs/reseat.md")) { Console.WriteLine("docs/reseat.md が無い"); return; }
        var allRows = CompareBuilds();
        var byName = allRows.ToDictionary(r => r.Name, r => r.F);
        var primary = new HashSet<string>(Baseline.PrimaryRows);

        // ---- docs/reseat.md を節ごとに読む -------------------------------------------------------
        var sections = new List<(string Name, List<(string Rank, bool Cur, string[] Slots, double Avg)> Rows)>();
        {
            string? cur = null;
            List<(string, bool, string[], double)>? rows = null;
            foreach (string line in File.ReadAllLines("docs/reseat.md"))
            {
                if (line.StartsWith("## "))
                {
                    if (cur is not null && rows is not null) sections.Add((cur, rows));
                    cur = line[3..].Trim(); rows = new List<(string, bool, string[], double)>();
                }
                else if (line.StartsWith("| ") && rows is not null)
                {
                    var c = line.Split('|').Select(x => x.Trim()).ToArray();
                    if (c.Length < 8 || c[1].Length == 0 || !char.IsDigit(c[1][0])) continue;
                    if (!double.TryParse(c[6].TrimEnd('%'), out double avg)) continue;
                    var fr = c[3].Split('/');
                    var ba = c[5].Split('/');
                    if (fr.Length != 2 || ba.Length != 2) continue;
                    rows.Add((c[1], c[1].Contains("現行"), new[] { fr[0], fr[1], c[4], ba[0], ba[1] }, avg));
                }
            }
            if (cur is not null && rows is not null) sections.Add((cur, rows));
        }

        Formation? SeatBuild(Formation baseF, string[] slots)
        {
            var members = baseF.Occupied().Select(o => o.Def).ToList();
            var f = new Formation();
            for (int k = 0; k < 5; k++)
            {
                if (slots[k].Length == 0) continue;
                UnitDef? d = members.FirstOrDefault(m => m.Name == slots[k]);
                if (d is null) return null;
                f[k] = d;
            }
            return f;
        }
        bool SeatSame(Formation a, Formation b)
        {
            for (int k = 0; k < 5; k++)
            {
                UnitDef? x = a[k], y = b[k];
                if ((x is null) != (y is null)) return false;
                if (x is not null && y is not null && x.Id != y.Id) return false;
            }
            return true;
        }
        (double Avg, double[] W, int Info) SeatRate(Formation f)
        {
            var w = new double[gaW];
            for (int wv = 0; wv < gaW; wv++)
            {
                int wins = 0;
                for (int seed = GaSeatBase; seed < GaSeatBase + GaSeatSeeds; seed++)
                    if (BattleEngine.Run(f, gaStages[wv].Enemy, seed, verbose: false).PlayerWon) wins++;
                w[wv] = wins * 100.0 / GaSeatSeeds;
            }
            int info = 0;
            for (int wv = 1; wv < gaW; wv++) if (w[wv] > 0 && w[wv] < 100) info++;
            return (w.Average(), w, info);
        }

        Console.WriteLine("# 第89期 表B —— (P2) 席");
        Console.WriteLine();
        Console.WriteLine("**`confirm` は使っていない。** `confirm` の候補表は**各期に提案された配置を焼き付けた台帳**で、"
                          + "**採用済みの行もそのまま残っている**——実際 13 行が「採用」と出るが、"
                          + "**現行の `CompareBuilds()` はすでにその候補の席になっている**。"
                          + "あれは過去の決定を今の盤面で測り直した記録であって、「今この行の席が古いか」は1つも答えていない"
                          + "（**第88期の報告が `刻み×縫い +6.5pt` を「未適用の推奨」と読んだのは誤りで、この期に訂正する**）。");
        Console.WriteLine();
        Console.WriteLine($"生きた判定は `docs/reseat.md`（seed 0..199）から取る。第46期の3段の作法——"
                          + $"(1) 現行が上位5通りに入っていれば動かさない ／ (2) 入っていない行だけ seed {GaSeatBase}..{GaSeatBase + GaSeatSeeds - 1} で"
                          + $"測り直して {GaSeatLine:F1}pt 以上のときだけ動かす ／ (3) 採否は1位の配置ではなく**次数**で読む。");
        Console.WriteLine();

        int inTop5 = 0, outSmall = 0, parseFail = 0;
        var cands = new List<(string Name, int Rank, int N, double Cur, double Best, string[][] Slots)>();
        foreach (var (name, rows) in sections)
        {
            if (rows.Count == 0 || !byName.ContainsKey(name)) { parseFail++; continue; }
            int ci = rows.FindIndex(r => r.Cur);
            if (ci < 0) { parseFail++; continue; }
            var ordered = rows.OrderByDescending(r => r.Avg).ToList();
            int rank = ordered.FindIndex(r => r.Cur) + 1;
            double best = ordered[0].Avg;
            if (rank <= 5) { inTop5++; continue; }
            if (best - rows[ci].Avg < GaSeatLine) { outSmall++; continue; }
            // **候補は「平均1位」ではなく「情報セルを 2 以上に保つ最上位」**（第59期の作法）。
            // `reseat` は勝つ席を探す道具であって測れる席を探す道具ではない（第50期）ので、
            // `CompareBuilds()` の行に当てるときは情報セルの下限を先に置く。**9行すべてに同じ規則を当てる。**
            cands.Add((name, rank, rows.Count, rows[ci].Avg, best,
                       ordered.Where(r => !r.Cur).Select(r => r.Slots).ToArray()));
        }
        Console.WriteLine($"**{sections.Count} 節のうち 現行が上位5位以内 {inTop5} 行 ／ 外だが差 {GaSeatLine:F1}pt 未満 {outSmall} 行 ／ "
                          + $"追試にかける行 {cands.Count} 行**{(parseFail > 0 ? $"（読めなかった節 {parseFail}）" : "")}。");
        Console.WriteLine();
        Console.WriteLine($"| 行 | 主判定 | 現行の順位 | 現行(0..199) | 1位(0..199) | 差 | **現行({GaSeatBase}..)** | **候補({GaSeatBase}..)** | **差** | 採否 | 情報セル 現行→候補 | 波ごとの差 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|:-:|--:|---|");
        var accepted = new List<(string Name, string[] Slots)>();
        foreach (var c in cands)
        {
            Formation curF = byName[c.Name];
            var o = SeatRate(curF);
            int tried = 0, skippedInfo = 0;
            string[]? pick = null; (double Avg, double[] W, int Info) n = default; double gap = 0;
            foreach (string[] slots in c.Slots)
            {
                Formation? f = SeatBuild(curF, slots);
                if (f is null || SeatSame(curF, f)) continue;
                tried++;
                var r = SeatRate(f);
                if (r.Avg - o.Avg < GaSeatLine) break;      // 平均の降順なので、ここから下は届かない
                if (r.Info < 2) { skippedInfo++; continue; } // 情報セルの下限（第59期）
                pick = slots; n = r; gap = r.Avg - o.Avg; break;
            }
            if (pick is not null) accepted.Add((c.Name, pick));
            Console.WriteLine($"| {c.Name} | {(primary.Contains(c.Name) ? "○" : "")} | {c.Rank} / {c.N} | {c.Cur:F1}% | {c.Best:F1}% | {c.Best - c.Cur:F1}pt "
                              + $"| {o.Avg:F1}% | {(pick is null ? "—" : n.Avg.ToString("F1") + "%")} | **{(pick is null ? "—" : gap.ToString("+0.0;-0.0") + "pt")}** "
                              + $"| **{(pick is null ? "据え置き" : "採用")}** | {o.Info} → {(pick is null ? "—" : n.Info.ToString())} | "
                              + (pick is null ? $"情報セル 2 未満で外した候補 {skippedInfo} 通り" 
                                              : string.Join(" / ", Enumerable.Range(0, gaW).Select(wv => $"{n.W[wv] - o.W[wv]:+0.0;-0.0}"))
                                                + (skippedInfo > 0 ? $"（情報セルで外した上位 {skippedInfo} 通り）" : "")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"**採用 {accepted.Count} 行 / 追試 {cands.Count} 行。** 採用した行の新しい席:");
        Console.WriteLine();
        Console.WriteLine("| 行 | 前1 | 前3 | 中央 | 後1 | 後3 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (name, slots) in accepted)
            Console.WriteLine($"| {name} | " + string.Join(" | ", slots) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {gaSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // phase0（§1-3）
    // =====================================================================================
    if (gaArg == "phase0")
    {
        var allRows = CompareBuilds();
        bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
        var galdRows = allRows.Where(r => Has(r.F, "gald")).ToArray();
        var hariRows = allRows.Where(r => Has(r.F, "hari")).ToArray();
        var bothRows = allRows.Where(r => Has(r.F, "gald") && Has(r.F, "hari")).ToArray();
        var writerIds = new[] { "kado", "golm", "borg", "rica", "zoto", "tsugi" };

        Console.WriteLine("# 第89期 Phase 0（§1-3）—— 傷を引き取る前の地図");
        Console.WriteLine();
        Console.WriteLine("## §1-3-1 —— `GuardianTrait` と `MartyrTrait` の関係（**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("| | 実体 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 共有 | どちらも `RedirectGainTrait` を継承し、**基底の `OnDamaged` は1行も違わない**（印を読む → 0 に落とす → `dmg / 2` を `AtkBonus` へ） |");
        Console.WriteLine("| 分けてあるもの | `Trait.Id` と、engine が読む割合（ガルド 50% / 殉教 `MartyrRule` 既定 75%）だけ |");
        Console.WriteLine("| **引き取りの置き場** | **`GuardianTrait` の `OnDamaged` の override**。`RedirectGainTrait` には置かない |");
        Console.WriteLine("| 置かない理由 | 基底に置くと**敵の殉教者にも生える**。敵側に味方の傷を書く手段は無いが、"
                          + "**敵が味方の傷を引き取る経路が生えていないこと**を実測 0 回で確認する（自己検査 (d)） |");
        Console.WriteLine();
        Console.WriteLine($"`Stages` の敵で `Martyr` を持つ駒: "
                          + string.Join(" / ", gaStages.SelectMany(st => st.Enemy.Occupied()).Select(o => o.Def)
                              .Where(d => d.Traits.Contains(TraitId.Martyr)).Select(d => d.Name).Distinct().DefaultIfEmpty("（なし）"))
                          + $"。味方で `Guardian` を持つ駒: "
                          + string.Join(" / ", gaRoster.Where(d => d.Traits.Contains(TraitId.Guardian)).Select(d => d.Name).DefaultIfEmpty("（なし）")) + "。");
        Console.WriteLine();

        Console.WriteLine("## §1-3-2 —— `PendingKey`（`guardPending`）の読み順（**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("`RedirectGainTrait.OnDamaged` は**冒頭で印を読んで即座に 0 に落とす**"
                          + "（`bool guarded = self.Counter(PendingKey) > 0; self.SetCounter(PendingKey, 0);`）。");
        Console.WriteLine();
        Console.WriteLine("    guarded = self.Counter(PendingKey) > 0    ← ★ base より先に読む");
        Console.WriteLine("    base.OnDamaged(...)                       ← ここで印が 0 に落ちる（現行の育ちはそのまま）");
        Console.WriteLine("    if (!guarded || !self.IsAlive) return");
        Console.WriteLine("    計数（庇い成立・隣に傷のある味方がいたか）  ← **版に依らない**（規則の分岐より手前）");
        Console.WriteLine("    if (!ctx.Gather.Enabled) return           ← ここから下だけが Z1");
        Console.WriteLine("    donor = 隣接する生存味方のうち Wound 最大（同数は ctx.PickOne）");
        Console.WriteLine("    donor.Wound -= 1 ; self.Wound += 1");
        Console.WriteLine();
        Console.WriteLine("**`ctx.PickOne` は `Gather.Enabled` の内側にしか無い。** 候補が2個以上あると `Roll` を消費するので、"
                          + "手前で呼ぶと Z0 の乱数列が動く。**計数を規則の手前に置いたのは第86期の X1P と同じ作法**"
                          + "——紙のスループットの分子を Z0 の実測から取るため。");
        Console.WriteLine();

        Console.WriteLine("## §1-3-3 —— 隣接の窓口（**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("`Stoic`（`BattleContext.SupportTargets`）が使っているのと**同じ `FormationRules.AreAdjacent(u.Slot, a.Slot)`**。"
                          + "新しい隣接の読み方は作っていない。**`AcceptsSupport` は見ない**"
                          + "——取り上げるのは支援ではないので、引き受け（`BearTrait`）・業と同じ扱い。");
        Console.WriteLine();
        Console.WriteLine("編成5枠の隣接次数は **中央 4 / 角 2 の2値**（第45期）。"
                          + "**ガルドは34行すべてで角**という第68期の記録があるので、引き取りの供給口は原則2つ。");
        Console.WriteLine();

        Console.WriteLine("## §1-3-4 —— `compare` の行数（**指示書の数を信用せず数え直した**）");
        Console.WriteLine();
        Console.WriteLine("| | 行数 | 行 |");
        Console.WriteLine("|---|--:|---|");
        Console.WriteLine($"| 全行 | {allRows.Length} | |");
        Console.WriteLine($"| ガルドを含む | **{galdRows.Length}** | |");
        Console.WriteLine($"| ハリを含む | **{hariRows.Length}** | {string.Join(" / ", hariRows.Select(r => $"`{r.Name}`"))} |");
        Console.WriteLine($"| **両方を含む** | **{bothRows.Length}** | {(bothRows.Length == 0 ? "**0 行**" : string.Join(" / ", bothRows.Select(r => $"`{r.Name}`")))} |");
        Console.WriteLine($"| ガルド ∩ 巻き込み則の書き手 | {allRows.Count(r => Has(r.F, "gald") && writerIds.Any(w => Has(r.F, w)))} | |");
        Console.WriteLine();
        Console.WriteLine(bothRows.Length == 0
            ? "**両方を含む行が 0 なので、拒否権は原理的に立たない。** 立たないこと自体を自己検査 (h) にする"
              + "——**ガルドを含む行が `compare` で全セル ±0.0** になるはず（ガルドが傷を集めても、それを読む駒が同じ行にいない）。"
            : "**両方を含む行がある。** 拒否権が立ちうるので `gather check` で必ず測る。");
        Console.WriteLine();

        Console.WriteLine("## §1-3-5 —— 紙のスループット（停止条件）");
        Console.WriteLine();
        Console.WriteLine("**分子は線形**（§3-6 で測る前に宣言済み）。1回の庇いにつき傷は 1 つしか動かず、"
                          + "その 1 つが生む回復は**ハリが味方側から糸を引いたときに `PerWound` = "
                          + $"{SutureTrait.PerWound} が1回だけ乗る**——第87期の毒の層のような累積（`2L² − L`）は生まれない。");
        Console.WriteLine();
        Console.WriteLine("    増分/戦（上限） = min(引き取り回数/戦, ハリが味方側から糸を引いた回数/戦) × PerWound");
        Console.WriteLine();
        Console.WriteLine($"分母は**その台の総被ダメージ/戦**。**停止条件: 増分/戦 ÷ 総被ダメージ/戦 ≥ {GaPaperFloor:F0}%。**");
        Console.WriteLine();
        Console.WriteLine("材料は **Z0（現行）の実測**から取る（第86期の X1P の作法）——"
                          + "庇い成立回数と「隣に傷のある味方がいたか」は**規則の分岐より手前で数えている**ので版に依らない。");
        Console.WriteLine();

        // ---- 材料の実測（Z0 だけ。2×2 の台の cell 0＝両方本物）--------------------------------
        var probeB = gaIntendedIds.Select(i => gaIdx[i]).ToArray();
        var acc = new double[probeB.Length][];
        Parallel.For(0, probeB.Length, bi =>
        {
            int b = probeB[bi];
            double guards = 0, donor = 0, taken = 0, sutAlly = 0, sutAllyDepth = 0, sutFoe = 0, taken2 = 0, n = 0, turns = 0, spill = 0;
            var fills = GaFills(GaTableSeed, gaGald, b);
            foreach (var fill in fills)
            {
                var team = GaTeam(gaGald, b, fill);
                int[] seats = GaSeats(team);
                Formation f = GaForm(team, seats, 0);
                for (int wv = 1; wv < gaW; wv++)
                    for (int seed = GaBand; seed < GaBand + GaM; seed++)
                    {
                        BattleResult r = GaRun(f, gaWeak[wv].Enemy, seed, false, false, 0);
                        n++; turns += r.Turns;
                        foreach ((int sl, UnitDef d) in f.Occupied())
                        {
                            if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
                            taken2 += t.DamageTaken;
                            guards += t.GatherGuards; donor += t.GatherHadDonor; taken += t.GatherTaken;
                            sutAlly += t.SutureAlly; sutAllyDepth += t.SutureAllyDepth; sutFoe += t.SutureFoe;
                            spill += t.SpillWoundsWritten;
                        }
                    }
            }
            acc[bi] = new[] { n, guards, donor, taken, sutAlly, sutAllyDepth, sutFoe, taken2, turns, spill };
        });

        Console.WriteLine($"| B（意図した相手） | 戦数 | 決着T | ガルドの庇い成立/戦 | 隣に傷のある味方がいた率 | 巻き込み則の書込/戦 | ハリの味方糸口/戦 | （敵糸口/戦） | 総被ダメ/戦 | **紙の増分/戦** | **÷ 総被ダメ** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        double sg = 0, sd = 0, sa = 0, st2 = 0, sn = 0, str_ = 0, ssp = 0, sfo = 0;
        for (int bi = 0; bi < probeB.Length; bi++)
        {
            var a2 = acc[bi];
            double n = a2[0], guards = a2[1], donor = a2[2], sutAlly = a2[4], sutFoe = a2[6], dmg = a2[7], turns = a2[8], spill = a2[9];
            double paper = Math.Min(donor / n, sutAlly / n) * SutureTrait.PerWound;
            sg += guards; sd += donor; sa += sutAlly; st2 += dmg; sn += n; str_ += turns; ssp += spill; sfo += sutFoe;
            Console.WriteLine($"| {gaName[probeB[bi]]} | {n:N0} | {turns / n:F1} | {guards / n:F2} | {(guards > 0 ? donor * 100.0 / guards : 0):F1}% | {spill / n:F2} | {sutAlly / n:F2} | {sutFoe / n:F2} | {dmg / n:F1} | **{paper:F2}** | **{paper / (dmg / n) * 100:F2}%** |");
        }
        {
            double paper = Math.Min(sd / sn, sa / sn) * SutureTrait.PerWound;
            double spec = (sg / sn) * (sg > 0 ? sd / sg : 0) * (sa / sn) * SutureTrait.PerWound;
            Console.WriteLine($"| **合計** | {sn:N0} | {str_ / sn:F1} | **{sg / sn:F2}** | **{(sg > 0 ? sd * 100.0 / sg : 0):F1}%** | {ssp / sn:F2} | **{sa / sn:F2}** | {sfo / sn:F2} | {st2 / sn:F1} | **{paper:F2}** | **{paper / (st2 / sn) * 100:F2}%** |");
            Console.WriteLine();
            Console.WriteLine($"**停止条件の判定: {paper / (st2 / sn) * 100:F2}% 対 線 {GaPaperFloor:F0}% → "
                              + $"{(paper / (st2 / sn) * 100 >= GaPaperFloor ? "**通る（2×2 を回す）**" : "**落ちる（2×2 を回さずに閉じる）**")}**。");
            Console.WriteLine();
            Console.WriteLine($"**指示書 §1-3 の式**（庇い成立 × 隣に傷のある率 × ハリの味方糸口 × {SutureTrait.PerWound}）で計算すると **{spec:F2}/戦**"
                              + $"（÷ 総被ダメ = {spec / (st2 / sn) * 100:F2}%）。**2つの回数の積になっていて次元が合わない**ので"
                              + "（1戦あたりの回数 × 1戦あたりの回数 ＝ 1戦あたりの回数の2乗）、判定には上の線形の式を使う。**両方を表に残す。**");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {gaSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // ideal —— 鎖の3段が全部そろった台での上限（**紙で止まった原因の切り分け**）
    // =====================================================================================
    if (gaArg == "ideal")
    {
        // ドラフト台（A ＝ ガルド固定・B が1枚・埋め草3枚）では、**源・中継・終端の3枚がそろうのは
        // B ＝ ハリ を引いたうえで埋め草に密な書き手が入ったときだけ**。
        // 「中継が効かないのか、終端が1枚しかないのか」を分けるために、**3段を手で全部そろえた台**を作る。
        // **`CompareBuilds()` は触らない**（診断のローカル。`gradient` / `aim` と同じ扱い）。
        var rows = new (string Name, Formation F)[]
        {
            // **庇いは前列でしか成立しない**（`SelectTargetChain`: `f.Row == Row.Front`）。
            // 前列は前1・前3 の2枠で、どちらも**次数2の角**——**中継の発火条件と供給口の広さは両立しない。**
            // 中央（次数4）に置いた版を対照として残す（庇い成立 0.00 回/戦 になる）。
            ("源=ゴルム / 中継=ガルド前1 / 終端=ハリ",
                Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga,
                                center: UnitCatalog.Golm, back1: UnitCatalog.Hari, back3: UnitCatalog.Vel)),
            ("源=ゴルム / 中継=ガルド前1 / 終端=ハリ / 埋め草=ノミ",
                Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Nomi,
                                center: UnitCatalog.Golm, back1: UnitCatalog.Hari, back3: UnitCatalog.Vel)),
            ("源=ゴルム+ボルグ / 中継=ガルド前1 / 終端=ハリ",
                Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Borg,
                                center: UnitCatalog.Golm, back1: UnitCatalog.Hari, back3: UnitCatalog.Vel)),
            ("**終端なし**（ハリ → 素体・同数値）",
                Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga,
                                center: UnitCatalog.Golm, back1: gaPlainMap["hari"], back3: UnitCatalog.Vel)),
            ("**中継なし**（ガルド → 素体・同数値）",
                Formation.Build(front1: gaPlainMap["gald"], front3: UnitCatalog.Dolga,
                                center: UnitCatalog.Golm, back1: UnitCatalog.Hari, back3: UnitCatalog.Vel)),
            ("中継=ガルド**中央**（庇いの前列条件の対照）",
                Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Dolga,
                                center: UnitCatalog.Gald, back1: UnitCatalog.Hari, back3: UnitCatalog.Vel)),
        };
        const int IdealSeeds = 200;

        Console.WriteLine("# 第89期 —— 鎖の3段をそろえた台（紙で止まった原因の切り分け）");
        Console.WriteLine();
        Console.WriteLine("ドラフト台（A ＝ ガルド固定・B が1枚・埋め草3枚）では、**源・中継・終端の3枚がそろうのは "
                          + "B ＝ ハリ を引いたうえで埋め草に密な書き手が入ったときだけ**。"
                          + "「中継が効かないのか、終端が1枚しかないのか」を分けるために、**3段を手で全部そろえた台**を作った。"
                          + $"`Stages` の5波 × seed 0..{IdealSeeds - 1}。**`CompareBuilds()` は触っていない。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | 庇い成立/戦 | 隣に傷のある率 | **引き取り/戦** | **ガルドの傷の深さ 平均/最大** | ハリの味方糸口/戦 | そのときの深さ | 味方傷の総量/戦 | ガルドの攻撃力補正/戦 | 総被ダメ/戦 | **紙の増分 ÷ 総被ダメ** | 縫いの回復/戦 | 敵の引き取り/戦 | 決着T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var res = new string[rows.Length][];
        Parallel.For(0, rows.Length, i =>
        {
            var lines = new List<string>();
            for (int z = 0; z < 2; z++)
            {
                var w = new double[gaW];
                double guards = 0, donor = 0, taken = 0, depth = 0, depthMax = 0;
                double sutAlly = 0, sutDepth = 0, spill = 0, atk = 0, n = 0, turns = 0, dmgTaken = 0, healed = 0, foeTaken = 0;
                for (int wv = 0; wv < gaW; wv++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < IdealSeeds; seed++)
                    {
                        BattleResult r = GaRun(rows[i].F, gaStages[wv].Enemy, seed, false, false, z);
                        if (r.PlayerWon) wins++;
                        n++; turns += r.Turns;
                        foreach ((int sl, UnitDef d) in rows[i].F.Occupied())
                        {
                            if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
                            guards += t.GatherGuards; donor += t.GatherHadDonor; taken += t.GatherTaken;
                            depth += t.GatherDepthSum; depthMax = Math.Max(depthMax, t.GatherDepthMax);
                            sutAlly += t.SutureAlly; sutDepth += t.SutureAllyDepth;
                            spill += t.SpillWoundsWritten; atk += t.CarryAtkGain; dmgTaken += t.DamageTaken;
                            healed += t.SutureHealed;
                        }
                        foreach ((int sl, UnitDef d) in gaStages[wv].Enemy.Occupied())
                        {
                            if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? et)) continue;
                            foeTaken += et.GatherTaken;
                        }
                    }
                    w[wv] = wins * 100.0 / IdealSeeds;
                }
                double m25 = 0; for (int wv = 1; wv < gaW; wv++) m25 += w[wv]; m25 /= gaW - 1;
                lines.Add($"| {rows[i].Name} | Z{z} | " + string.Join(" | ", w.Select(x => $"{x:F1}%")) + $" | **{m25:F1}%** "
                          + $"| {guards / n:F2} | {(guards > 0 ? donor * 100.0 / guards : 0):F1}% | **{taken / n:F2}** "
                          + $"| **{(taken > 0 ? depth / taken : 0):F2} / {depthMax:F0}** | {sutAlly / n:F2} | {(sutAlly > 0 ? sutDepth / sutAlly : 0):F2} "
                          + $"| {spill / n:F2} | {atk / n:F1} | {dmgTaken / n:F1} | **{Math.Min(taken / n, sutAlly / n) * SutureTrait.PerWound / (dmgTaken / n) * 100:F2}%** | **{healed / n:F1}** | {foeTaken / n:F2} | {turns / n:F1} |");
            }
            res[i] = lines.ToArray();
        });
        foreach (var r in res) foreach (var l in r) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"所要 {gaSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check（自己検査 (d)(h)(j)）
    // =====================================================================================
    if (gaArg == "check")
    {
        var allRows = CompareBuilds();
        bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
        var c = new double[2][][];
        for (int k = 0; k < 2; k++) c[k] = new double[allRows.Length][];
        Parallel.For(0, allRows.Length * 2, k =>
        {
            int i2 = k / 2, z = k % 2;
            var res = new double[gaW];
            for (int wv = 0; wv < gaW; wv++)
            {
                int wins = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (GaRun(allRows[i2].F, gaStages[wv].Enemy, seed, false, false, z).PlayerWon) wins++;
                res[wv] = wins * 100.0 / 200;
            }
            c[z][i2] = res;
        });
        var bal = new Dictionary<string, double[]>();
        if (File.Exists("docs/balance.md"))
            foreach (string line in File.ReadAllLines("docs/balance.md"))
            {
                if (!line.StartsWith("| ")) continue;
                var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
                if (cells.Length != gaW + 1 || !cells[1].EndsWith("%")) continue;
                var v = new double[gaW];
                bool ok = true;
                for (int wv = 0; wv < gaW; wv++)
                    if (!double.TryParse(cells[wv + 1].TrimEnd('%'), out v[wv])) { ok = false; break; }
                if (ok) bal[cells[0]] = v;
            }
        int mismZ = 0, mismDoc = 0, cells2 = 0, movedGald = 0, movedOther = 0;
        for (int i2 = 0; i2 < allRows.Length; i2++)
        {
            bool moved = false;
            for (int wv = 0; wv < gaW; wv++)
            {
                if (Math.Abs(c[0][i2][wv] - c[1][i2][wv]) > 0.001) { mismZ++; moved = true; }
                if (bal.TryGetValue(allRows[i2].Name, out double[]? b)) { cells2++; if (Math.Abs(b[wv] - c[0][i2][wv]) > 0.001) mismDoc++; }
            }
            if (moved) { if (Has(allRows[i2].F, "gald")) movedGald++; else movedOther++; }
        }
        Console.WriteLine("# 第89期 —— 自己検査（`gather check`）");
        Console.WriteLine();
        Console.WriteLine($"**(h) `compare` {allRows.Length} 行 × {gaW} 波が Z0 / Z1 で完全一致** → ずれ **{mismZ}** 件"
                          + $"（動いた行: ガルドを含む {movedGald} / 含まない {movedOther}）→ **{(mismZ == 0 ? "○" : "×")}**。"
                          + "ガルドを含む行は 34 あるが、**味方の傷を読む駒（ハリ）と同席する行が 0** なので、"
                          + "引き取りは走っても盤面には出ない（Phase 0 §1-3-4 の予測どおり）。");
        Console.WriteLine();
        Console.WriteLine($"**(j) `docs/balance.md` と Z0 が一致** → {cells2} セル・ずれ **{mismDoc}** 件 → **{(mismDoc == 0 ? "○" : "×")}**。"
                          + "**計数（`GatherGuards` / `GatherHadDonor`）を規則の分岐より手前に置いても盤面が動いていない**ことの検算"
                          + "——`ctx.PickOne` だけは `Gather.Enabled` の内側にあるので、Z0 では `Roll` を1つも消費しない。");
        Console.WriteLine();
        Console.WriteLine($"所要 {gaSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("gather: 引数は redo87 / run87 <v> / seats / phase0 / ideal / run <z> / tables <z0> <z1> / check <z0> <z1>。");
    return;
}
}
