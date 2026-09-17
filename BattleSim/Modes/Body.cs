using BattleCore;
using static Common;

// =====================================================================================
// body モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "body")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 body
// =====================================================================================

static class BodyDiag
{
// body モード（第76期・調査）: **ドラフト台で「体」は何を決めているか。**
//
// 第72期に残った未説明の 93%（選択ありドラフトは静的な数値の上で理想編成と見分けが付かないのに
// 勝率が 32.09% 対 67.2%）と、第73・74期の **−7.2**（傷の駒を全部素体にしても残る傾き）を、
// **数えるだけ**の期として解剖する。**新機構ゼロ・駒ゼロ・engine も Traits.cs も 1 行も触らない。**
//
// **台・標本・seed 帯・規則・配置はすべて第69〜75期の写し**（1文字も変えていない）。
//
//     dotnet run --project BattleSim -c Release 0 body phase0        # 紙の計算（**戦闘0回**。抽選だけ回す）
//     dotnet run --project BattleSim -c Release 0 body               # 表B/C/D/F（A 帯 seed 0..7）
//     dotnet run --project BattleSim -c Release 0 body alt           # 同じ表を B 帯（seed 200..207）
//     dotnet run --project BattleSim -c Release 0 body wound [alt]   # 表E（−7.2 の分解）
public static void Run(string[] args, int stageIndex)
{
    string byArg = args.Length > 2 ? args[2] : "";
    string byArg2 = args.Length > 3 ? args[3] : "";
    var bySw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> byStages = EnemyCatalog.Stages;
    int byW = byStages.Count;
    var byRoster = UnitCatalog.All.ToArray();
    int byRN = byRoster.Length;
    var byKeyOf = byRoster.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);
    int byNK = UnitTally.CarryKeys.Length;

    const int ByOfferSeed = 2_000_000;
    const int ByN = 11000, ByM = 8;
    const int ByStrong = 7, ByWeakPct = 60;
    const int BySeedsIdeal = 200;
    int byBand = (byArg == "alt" || byArg2 == "alt") ? 200 : 0;
    string byBandName = byBand == 0 ? "A" : "B";

    // 規則 S' の傾き定数（第72期 `slope` の写し。**1文字も変えていない**）
    int[] BySlope100 = { -90, -650, 190, 653, -1256, -330, -796, -1557, -759, 1936, 981 };
    var bySlopeOf = byRoster.ToDictionary(u => u.Id, u => byKeyOf[u.Id].Sum(k => BySlope100[k]));

    int[] ByKeys(UnitDef d) => byKeyOf.TryGetValue(d.Id, out int[]? k) ? k : TraitKeyMap.KeysOf(d);

    // ---- 弱い波（第70〜75期の写し。**HP だけの1変数**） -------------------------------------
    var byWeakCache = new Dictionary<string, UnitDef>();
    UnitDef ByWeakOf(UnitDef d)
    {
        if (byWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * ByWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        byWeakCache[d.Id] = w;
        return w;
    }
    var byWeak = byStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = ByWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef ByPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };

    // ---- 選択規則（第72期 `slope` の D4Team の写し。rule 1 = P / rule 3 = S'）--------------
    UnitDef[] ByTeam(int rule, int i)
    {
        var rng = new Random(ByOfferSeed + i);
        var idx = new int[byRN];
        for (int k = 0; k < byRN; k++) idx[k] = k;
        int remain = byRN, strong = 0;
        var picked = new UnitDef[5];
        for (int r = 0; r < 5; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = byRoster[idx[t]];
            }
            UnitDef sel = rule == 1
                ? (strong < 2
                    ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                    : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First())
                : offer.OrderByDescending(x => bySlopeOf[x.Id])
                       .ThenByDescending(x => x.Attack)
                       .ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= ByStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(byRoster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int[] BySeats(UnitDef[] u)
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
    Formation ByForm(UnitDef[] u, int[] seats, IReadOnlyDictionary<string, UnitDef>? map)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
            f[seats[k]] = (map is not null && map.TryGetValue(u[k].Id, out UnitDef? nd)) ? nd : u[k];
        return f;
    }

    // ---- 説明変数（**測る前に固定。指示書 §2-2 の表から1つも増やしていない**）---------------
    //  群: 0 数値(7) / 1 形(5) / 2 軸(12) / 3 交互作用(1)  —— 合計 25
    const int ByF = 25;
    var byFName = new string[ByF];
    var byFGrp = new int[ByF];
    string[] byGrpName = { "数値", "形", "軸", "交互作用" };
    byFName[0] = "総攻"; byFName[1] = "総HP"; byFName[2] = "総速";
    byFName[3] = "攻の最大値"; byFName[4] = "HP の最小値";
    byFName[5] = "攻の分散"; byFName[6] = "HP の分散";
    byFName[7] = "単体の枚数"; byFName[8] = "薙ぎの枚数"; byFName[9] = "貫きの枚数";
    byFName[10] = "`Actions` を持つ枚数"; byFName[11] = "特性の総数";
    byFName[12] = "相方あり率";
    for (int k = 0; k < byNK; k++) byFName[13 + k] = "キー:" + UnitTally.CarryKeys[k];
    byFName[24] = "総攻 × 総HP";
    for (int c = 0; c < ByF; c++) byFGrp[c] = c <= 6 ? 0 : c <= 11 ? 1 : c <= 23 ? 2 : 3;

    double[] ByFeat(IReadOnlyList<UnitDef> u)
    {
        int n = u.Count;
        var f = new double[ByF];
        double sa = u.Sum(x => (double)x.Attack), sh = u.Sum(x => (double)x.MaxHp), sp = u.Sum(x => (double)x.Speed);
        f[0] = sa; f[1] = sh; f[2] = sp;
        f[3] = u.Max(x => (double)x.Attack); f[4] = u.Min(x => (double)x.MaxHp);
        double ma = sa / n, mh = sh / n;
        f[5] = u.Sum(x => (x.Attack - ma) * (x.Attack - ma)) / n;
        f[6] = u.Sum(x => (x.MaxHp - mh) * (x.MaxHp - mh)) / n;
        f[7] = u.Count(x => x.Pattern == AttackPattern.Single);
        f[8] = u.Count(x => x.Pattern == AttackPattern.Sweep);
        f[9] = u.Count(x => x.Pattern == AttackPattern.Pierce);
        f[10] = u.Count(x => x.Actions is not null);
        f[11] = u.Sum(x => (double)x.Traits.Count);
        int pal = 0;
        for (int a = 0; a < n; a++)
        {
            bool has = false;
            for (int b = 0; b < n && !has; b++)
                if (b != a && ByKeys(u[a]).Any(x => ByKeys(u[b]).Contains(x))) has = true;
            if (has) pal++;
        }
        f[12] = pal * 1.0 / n;
        foreach (UnitDef d in u) foreach (int k in ByKeys(d)) f[13 + k]++;
        f[24] = sa * sh;
        return f;
    }

    // ---- 最小二乗（**関数形は線形。標準化して正規方程式を解くだけ**）------------------------
    // 多重共線性があるので係数は読まない（指示書 §2-2）。**読むのは R² と、群を落としたときの低下だけ。**
    double[] ByBeta(double[][] X, double[] y, IReadOnlyList<int> cols, IReadOnlyList<int> rows,
                    out List<int> use, out List<double> mu, out List<double> sd, out double ym)
    {
        int n = rows.Count;
        use = new List<int>(); mu = new List<double>(); sd = new List<double>();
        foreach (int c in cols)
        {
            double m = 0; for (int i = 0; i < n; i++) m += X[rows[i]][c]; m /= n;
            double v = 0; for (int i = 0; i < n; i++) { double d = X[rows[i]][c] - m; v += d * d; }
            v = Math.Sqrt(v / n);
            if (v > 1e-12) { use.Add(c); mu.Add(m); sd.Add(v); }
        }
        int m2 = use.Count;
        ym = 0; for (int i = 0; i < n; i++) ym += y[rows[i]]; ym /= n;
        if (m2 == 0) return Array.Empty<double>();
        var A = new double[m2][];
        for (int a = 0; a < m2; a++) A[a] = new double[m2 + 1];
        var z = new double[m2];
        for (int i = 0; i < n; i++)
        {
            for (int a = 0; a < m2; a++) z[a] = (X[rows[i]][use[a]] - mu[a]) / sd[a];
            double yc = y[rows[i]] - ym;
            for (int a = 0; a < m2; a++)
            {
                for (int b = a; b < m2; b++) A[a][b] += z[a] * z[b];
                A[a][m2] += z[a] * yc;
            }
        }
        for (int a = 0; a < m2; a++) for (int b = 0; b < a; b++) A[a][b] = A[b][a];
        for (int a = 0; a < m2; a++) A[a][a] += 1e-6 * n;
        for (int p = 0; p < m2; p++)
        {
            int piv = p;
            for (int r2 = p + 1; r2 < m2; r2++) if (Math.Abs(A[r2][p]) > Math.Abs(A[piv][p])) piv = r2;
            (A[p], A[piv]) = (A[piv], A[p]);
            if (Math.Abs(A[p][p]) < 1e-10) A[p][p] = 1e-10;
            for (int r2 = 0; r2 < m2; r2++)
            {
                if (r2 == p) continue;
                double fct = A[r2][p] / A[p][p];
                if (fct == 0) continue;
                for (int c2 = p; c2 <= m2; c2++) A[r2][c2] -= fct * A[p][c2];
            }
        }
        var beta = new double[m2];
        for (int a = 0; a < m2; a++) beta[a] = A[a][m2] / A[a][a];
        return beta;
    }

    double ByR2(double[][] X, double[] y, IReadOnlyList<int> cols, IReadOnlyList<int> rows)
    {
        int n = rows.Count;
        if (n < 10) return double.NaN;
        double[] beta = ByBeta(X, y, cols, rows, out List<int> use, out List<double> mu, out List<double> sd, out double ym);
        double sst = 0; for (int i = 0; i < n; i++) { double d = y[rows[i]] - ym; sst += d * d; }
        if (sst <= 1e-12) return double.NaN;
        if (beta.Length == 0) return 0.0;
        double sse = 0;
        for (int i = 0; i < n; i++)
        {
            double pred = 0;
            for (int a = 0; a < beta.Length; a++) pred += beta[a] * (X[rows[i]][use[a]] - mu[a]) / sd[a];
            double e = (y[rows[i]] - ym) - pred;
            sse += e * e;
        }
        return 1.0 - sse / sst;
    }

    double[] ByResidOf(double[][] X, double[] y, IReadOnlyList<int> cols, IReadOnlyList<int> rows)
    {
        int n = rows.Count;
        double[] beta = ByBeta(X, y, cols, rows, out List<int> use, out List<double> mu, out List<double> sd, out double ym);
        var res = new double[n];
        for (int i = 0; i < n; i++)
        {
            double pred = ym;
            for (int a = 0; a < beta.Length; a++) pred += beta[a] * (X[rows[i]][use[a]] - mu[a]) / sd[a];
            res[i] = y[rows[i]] - pred;
        }
        return res;
    }

    string ByP1(double x) => double.IsNaN(x) ? "—" : (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    string ByP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    string ByR(double x) => double.IsNaN(x) ? "—" : x.ToString("F3");
    double BySd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double ByPctile(IReadOnlyList<int> sorted, int v)
        => 100.0 * sorted.Count(x => x < v) / sorted.Count;

    // 傾き（第73〜75期の写し。**1文字も変えていない**）
    double BySlopeOfBox(IReadOnlyList<double> rate, IReadOnlyList<int> box)
    {
        var bk = new List<double>[4];
        for (int j = 0; j < 4; j++) bk[j] = new List<double>();
        for (int i = 0; i < rate.Count; i++) bk[Math.Min(3, box[i])].Add(rate[i]);
        double d10 = (bk[0].Count > 0 && bk[1].Count > 0) ? bk[1].Average() - bk[0].Average() : double.NaN;
        double d21 = (bk[1].Count > 0 && bk[2].Count > 0) ? bk[2].Average() - bk[1].Average() : double.NaN;
        return double.IsNaN(d10) ? d21 : double.IsNaN(d21) ? d10 : (d10 + d21) / 2;
    }

    // 傷の5枚（第74・75期の写し。順序も同じ）
    var byFive = new[] { UnitCatalog.Kiri, UnitCatalog.Egu, UnitCatalog.Nata, UnitCatalog.Hari, UnitCatalog.Nomi };
    var byFiveIds = byFive.Select(d => d.Id).ToArray();
    // 同じ攻・HP・速を持つ他の素体（**近い数値で代用しない**。無ければ「測れなかった」）
    var bySame = new List<UnitDef>[5];
    for (int k = 0; k < 5; k++)
        bySame[k] = byRoster.Where(x => x.Id != byFive[k].Id
                                     && x.Attack == byFive[k].Attack
                                     && x.MaxHp == byFive[k].MaxHp
                                     && x.Speed == byFive[k].Speed).ToList();

    var byAllRows = CompareBuilds();

    // =====================================================================================
    // phase0: 紙の計算（**戦闘を1回も回さない**。抽選だけ回す）
    // =====================================================================================
    if (byArg == "phase0")
    {
        Console.WriteLine("# 第76期 Phase 0 —— ロスターの分布と、測る前の紙の計算");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `dotnet run --project BattleSim -c Release 0 body phase0`");
        Console.WriteLine();

        Console.WriteLine($"## 0-1. ロスターの分布（**{byRN}体**）");
        Console.WriteLine();
        var atk = byRoster.Select(u => u.Attack).OrderBy(x => x).ToArray();
        var hp = byRoster.Select(u => u.MaxHp).OrderBy(x => x).ToArray();
        var spd = byRoster.Select(u => u.Speed).OrderBy(x => x).ToArray();
        double Med(int[] a) => a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2.0;
        Console.WriteLine("| 量 | 平均 | 中央値 | 最小 | 最大 | 指示書の当たり | 判定 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|:-:|");
        Console.WriteLine($"| 攻 | {atk.Average():F2} | {Med(atk):F1} | {atk[0]} | {atk[^1]} | 中央値 6・平均 7.6 "
                          + $"| {(Math.Abs(Med(atk) - 6) < 0.01 && Math.Abs(atk.Average() - 7.6) < 0.05 ? "○" : "**×**")} |");
        Console.WriteLine($"| HP | {hp.Average():F2} | {Med(hp):F1} | {hp[0]} | {hp[^1]} | 中央値 60 "
                          + $"| {(Math.Abs(Med(hp) - 60) < 0.01 ? "○" : "**×**")} |");
        Console.WriteLine($"| 速 | {spd.Average():F2} | {Med(spd):F1} | {spd[0]} | {spd[^1]} | 中央値 7 "
                          + $"| {(Math.Abs(Med(spd) - 7) < 0.01 ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine($"**攻6以下は {byRoster.Count(u => u.Attack <= 6)} / {byRN}**（指示書の当たり 28/51: "
                          + $"{(byRoster.Count(u => u.Attack <= 6) == 28 ? "○" : "**×**")}）／"
                          + $"**攻{ByStrong}以上は {byRoster.Count(u => u.Attack >= ByStrong)} / {byRN}**"
                          + "（第70期の 23/51 と突き合わせる）。");
        Console.WriteLine();
        Console.WriteLine("### ヒストグラム");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 攻 | 体 | HP | 体 | 速 | 体 |");
        Console.WriteLine("|---|---|--:|---|--:|---|--:|");
        for (int b = 0; b < 8; b++)
        {
            int a0 = b * 5, a1 = a0 + 4;
            int h0 = b * 15, h1 = h0 + 14;
            int s0 = b * 2, s1 = s0 + 1;
            Console.WriteLine($"| {b} | {a0}–{a1} | {atk.Count(x => x >= a0 && x <= a1)} "
                              + $"| {h0}–{h1} | {hp.Count(x => x >= h0 && x <= h1)} "
                              + $"| {s0}–{s1} | {spd.Count(x => x >= s0 && x <= s1)} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 0-2. 傷の5枚の統計上の位置（**百分位**）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 攻 | 百分位 | HP | 百分位 | 速 | 百分位 | 同じ数値の他の素体 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
        for (int k = 0; k < 5; k++)
        {
            UnitDef d = byFive[k];
            Console.WriteLine($"| {d.Name} | {d.Attack} | {ByPctile(atk, d.Attack):F0}% | {d.MaxHp} | {ByPctile(hp, d.MaxHp):F0}% "
                              + $"| {d.Speed} | {ByPctile(spd, d.Speed):F0}% "
                              + $"| {(bySame[k].Count == 0 ? "**無し**" : string.Join("・", bySame[k].Select(x => x.Name)))} |");
        }
        Console.WriteLine();
        Console.WriteLine("**百分位 = その値未満の駒の割合**（分母はロスター全体）。");
        int bySameTotal = bySame.Sum(x => x.Count);
        Console.WriteLine($"**同じ攻・HP・速を持つ他の素体は合計 {bySameTotal} 件**"
                          + (bySameTotal == 0
                             ? "——**§2-4 の後半（同じ数値の他の素体との比較）は測れない。近い数値で代用しない**（指示書 §5-2）。"
                             : "。"));
        Console.WriteLine();
        Console.WriteLine("**参考（判定には使わない）**: 数値が近い素体（|Δ攻| ≤ 2 かつ |ΔHP| ≤ 6 かつ |Δ速| ≤ 2）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 近い素体 |");
        Console.WriteLine("|---|---|");
        for (int k = 0; k < 5; k++)
        {
            UnitDef d = byFive[k];
            var near = byRoster.Where(x => x.Id != d.Id && Math.Abs(x.Attack - d.Attack) <= 2
                                        && Math.Abs(x.MaxHp - d.MaxHp) <= 6 && Math.Abs(x.Speed - d.Speed) <= 2).ToArray();
            Console.WriteLine($"| {d.Name} ({d.Attack}/{d.MaxHp}/{d.Speed}) | "
                              + (near.Length == 0 ? "—" : string.Join("・", near.Select(x => $"{x.Name} ({x.Attack}/{x.MaxHp}/{x.Speed})"))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 0-3. 理想61行とドラフト編成は、数値以外で何が違うか（**列挙は紙で**）");
        Console.WriteLine();
        var byIdealFeat = byAllRows.Select(b => ByFeat(b.F.Occupied().Select(o => o.Def).ToArray())).ToArray();
        var byPFeat = new double[ByN][];
        var bySFeat = new double[ByN][];
        Parallel.For(0, ByN, i => { byPFeat[i] = ByFeat(ByTeam(1, i)); bySFeat[i] = ByFeat(ByTeam(3, i)); });
        Console.WriteLine("| 変数 | 群 | 理想61行 | ドラフト Pw | ドラフト S'w | **Pw − 理想** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int c = 0; c < ByF; c++)
        {
            if (c == 24) continue;
            double a = byIdealFeat.Average(f => f[c]), p = byPFeat.Average(f => f[c]), sv = bySFeat.Average(f => f[c]);
            Console.WriteLine($"| {byFName[c]} | {byGrpName[byFGrp[c]]} | {a:F2} | {p:F2} | {sv:F2} | {ByP2(p - a)} |");
        }
        Console.WriteLine();
        Console.WriteLine("**総攻・総HP は第70期の「静的な数値の上で見分けが付かない」の再現**"
                          + "（規則 P: 総攻 48.9 / 総HP 376 対 理想 48.6 / 372）。");
        Console.WriteLine();

        Console.WriteLine("## 0-4. 回帰の設計（**測る前に固定する。§2-2 から1つも増やしていない**）");
        Console.WriteLine();
        Console.WriteLine("| 群 | 変数 | 本数 |");
        Console.WriteLine("|---|---|--:|");
        for (int g = 0; g < 4; g++)
            Console.WriteLine($"| **{byGrpName[g]}** | "
                              + string.Join(" / ", Enumerable.Range(0, ByF).Where(c => byFGrp[c] == g).Select(c => byFName[c]))
                              + $" | {Enumerable.Range(0, ByF).Count(c => byFGrp[c] == g)} |");
        Console.WriteLine();
        Console.WriteLine("- **目的変数**: 標本ごとの勝率（**第2〜5波の平均**・0〜100）");
        Console.WriteLine("- **関数形**: 線形。交互作用は **総攻 × 総HP の1本だけ**（P4）");
        Console.WriteLine("- **標準化**して正規方程式を解く。対角に 1e-6·n だけ足す（共線性の保険）");
        Console.WriteLine("- **評価指標**: 決定係数 R² と、**群を1つ落としたときの R² の低下**");
        Console.WriteLine("- **係数は読まない**（多重共線性があるので順位だけ）");
        Console.WriteLine("- **3段**: 「数値のみ(7)」→「数値＋形(12)」→「全部(25)」");
        Console.WriteLine("- **0% の標本を含む版と除いた版の両方**を出す（自己検査 (a')）");
        Console.WriteLine();
        Console.WriteLine("**§2-3 の統制した実験の格子も、測る前に固定する**: 総攻・総HP・総速 のそれぞれを");
        Console.WriteLine("`floor(log(x) / log(1.05))` で箱に入れ、3つとも同じ箱に落ちた標本を1つの「組」とする");
        Console.WriteLine("（同じ箱なら最大 ÷ 最小 < 1.05 ＝ **±5% 以内**）。**組は 20 標本以上のものだけ読む。**");
        Console.WriteLine();

        Console.WriteLine("## 0-5. `docs/balance.md` の現在値");
        Console.WriteLine();
        Console.WriteLine($"**{byAllRows.Length} 行 × {byW} 波 = {byAllRows.Length * byW} セル**"
                          + $"（指示書の 61 行 × 5 波 = 305 セル: "
                          + $"{(byAllRows.Length == 61 && byW == 5 ? "○" : "**×**")}）。");
        Console.WriteLine();

        Console.WriteLine("## 0-6. 予測（**測る前に書く**・指示書 §1 の写し）");
        Console.WriteLine();
        Console.WriteLine("| # | 量 | 予測 |");
        Console.WriteLine("|--:|---|---|");
        Console.WriteLine("| P1 | 単変数の効き | **総HP が総攻より強い** |");
        Console.WriteLine("| P2 | 傷の5枚の位置 | **攻は上位・HP は中央以下・速はばらばら**。−7.2 の正体は HP |");
        Console.WriteLine("| P3 | 数値だけの説明力 | **4変数で R² < 0.5** |");
        Console.WriteLine("| P4 | 相互作用 | **攻 × HP が単独の和より大きい** |");
        Console.WriteLine("| P5 | 特性の数 | **効かない** |");
        Console.WriteLine("| P6 | 理想61行 | **R² はさらに低い** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {bySw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // wound: 表E —— −7.2 の分解（傷の5枚を1枚ずつ素体に落とす）
    // =====================================================================================
    if (byArg == "wound")
    {
        // 版: 0 現行 / 1..5 傷の k 枚目を素体に / 6 5枚とも素体に /
        //     7..11 傷の k 枚目を「**同じ攻・HP・速を持つ他の素体**」に差し替え（§2-4 後半）
        //     ——同じ数値の他の素体が存在しない枚は回さない（**近い数値で代用しない**・指示書 §5-2）
        const int ByWV = 12;
        var wvName = new string[ByWV];
        wvName[0] = "**現行**";
        for (int k = 0; k < 5; k++) wvName[1 + k] = $"素体:{byFive[k].Name}";
        wvName[6] = "**5枚とも素体（第73期の −7.2）**";
        for (int k = 0; k < 5; k++)
            wvName[7 + k] = bySame[k].Count == 0
                ? $"同数値の他の素体:{byFive[k].Name}（**無し**）"
                : $"**{byFive[k].Name} → 素体の{bySame[k][0].Name}**（同じ {byFive[k].Attack}/{byFive[k].MaxHp}/{byFive[k].Speed}）";

        var wWin = new int[ByN][];
        var wBox = new int[ByN];
        var wHas = new bool[ByN][];
        int wDone = 0;
        Console.Error.Write($"{byBandName}帯 表E: ");
        Parallel.For(0, ByN, i =>
        {
            UnitDef[] team = ByTeam(1, i);
            int[] seats = BySeats(team);
            var ids = team.Select(x => x.Id).ToHashSet();
            var has = new bool[ByWV];
            has[0] = true;
            for (int k = 0; k < 5; k++) has[1 + k] = ids.Contains(byFiveIds[k]);
            has[6] = byFiveIds.Any(ids.Contains);
            for (int k = 0; k < 5; k++) has[7 + k] = bySame[k].Count > 0 && ids.Contains(byFiveIds[k]);
            var win = new int[ByWV * byW];
            for (int v = 0; v < ByWV; v++)
            {
                if (!has[v]) continue;
                Dictionary<string, UnitDef>? map = null;
                if (v >= 1 && v <= 5) map = new Dictionary<string, UnitDef> { [byFiveIds[v - 1]] = ByPlain(byFive[v - 1]) };
                else if (v == 6)
                {
                    map = new Dictionary<string, UnitDef>();
                    for (int k = 0; k < 5; k++) if (ids.Contains(byFiveIds[k])) map[byFiveIds[k]] = ByPlain(byFive[k]);
                }
                else if (v >= 7) map = new Dictionary<string, UnitDef> { [byFiveIds[v - 7]] = ByPlain(bySame[v - 7][0]) };
                Formation f = ByForm(team, seats, map);
                for (int w = 0; w < byW; w++)
                    for (int seed = byBand; seed < byBand + ByM; seed++)
                        if (BattleEngine.Run(f, byWeak[w].Enemy, seed, verbose: false).PlayerWon)
                            win[v * byW + w]++;
            }
            wWin[i] = win; wHas[i] = has;
            wBox[i] = Math.Min(3, team.Count(x => ByKeys(x).Contains(UnitTally.CarryWound)));
            int c = Interlocked.Increment(ref wDone);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        double WRate(int i, int v, int w) => (wHas[i][v] ? wWin[i][v * byW + w] : wWin[i][w]) * 100.0 / ByM;
        double[] WRates(int v) => Enumerable.Range(0, ByN)
            .Select(i => Enumerable.Range(1, byW - 1).Average(w => WRate(i, v, w))).ToArray();

        Console.WriteLine($"# 第76期 表E —— −7.2 の分解（ドラフト台 Pw・{byBandName} 帯 seed {byBand}..{byBand + ByM - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 body wound{(byBand == 0 ? "" : " alt")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"標本 **{ByN:N0}** × 版 {ByWV} × {byW} 波 × seed **{ByM}** 本（規則 P・弱い波 {ByWeakPct}%）。");
        Console.WriteLine("**箱（傷の枚数）は必ず元の5枚で決める。判定は第2〜5波。**");
        Console.WriteLine();
        Console.WriteLine($"箱: **0枚 {wBox.Count(x => x == 0):N0} / 1枚 {wBox.Count(x => x == 1):N0} / "
                          + $"2枚 {wBox.Count(x => x == 2):N0} / 3+ {wBox.Count(x => x == 3):N0}**"
                          + "（第73期の 5299 / 4706 / 945 / 50 と突き合わせる）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 在席の標本 | 傾き | **Δ（現行比）** | 在席時の勝率（現行） | 在席時の勝率（その版） | **寄与** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        double w0 = BySlopeOfBox(WRates(0), wBox);
        var wSl = new double[ByWV];
        var wCon = new double[ByWV];
        for (int v = 0; v < ByWV; v++) { wSl[v] = double.NaN; wCon[v] = double.NaN; }
        for (int v = 0; v <= 6; v++)
        {
            var rate = WRates(v);
            wSl[v] = BySlopeOfBox(rate, wBox);
            var idx = Enumerable.Range(0, ByN).Where(i => wHas[i][v]).ToArray();
            double a0 = idx.Length == 0 ? double.NaN : idx.Average(i => Enumerable.Range(1, byW - 1).Average(w => WRate(i, 0, w)));
            double a = idx.Length == 0 ? double.NaN : idx.Average(i => Enumerable.Range(1, byW - 1).Average(w => WRate(i, v, w)));
            wCon[v] = a0 - a;
            Console.WriteLine($"| {wvName[v]} | {idx.Length:N0} | {ByP1(wSl[v])} | {(v == 0 ? "—" : ByP2(wSl[v] - w0))} "
                              + $"| {a0:F2}% | {a:F2}% | {(v == 0 ? "—" : ByP2(a0 - a))} |");
        }
        Console.WriteLine();
        Console.WriteLine("**寄与 ＝ 現行 − その駒を素体にした版**（第69期の標準器具）。");
        Console.WriteLine($"**1枚ずつの Δ の和 {ByP2(Enumerable.Range(1, 5).Sum(v => wSl[v] - w0))} 対 5枚まとめての Δ {ByP2(wSl[6] - w0)}**"
                          + "——差が交互作用。");
        Console.WriteLine();
        Console.WriteLine("## §2-4 後半 —— 同じ攻・HP・速を持つ他の素体との比較");
        Console.WriteLine();
        int wSameTotal = bySame.Sum(x => x.Count);
        Console.WriteLine("| 駒 | 攻/HP/速 | 同じ数値の他の素体 | 判定 |");
        Console.WriteLine("|---|---|---|---|");
        for (int k = 0; k < 5; k++)
            Console.WriteLine($"| {byFive[k].Name} | {byFive[k].Attack}/{byFive[k].MaxHp}/{byFive[k].Speed} "
                              + $"| {(bySame[k].Count == 0 ? "**無し**" : string.Join("・", bySame[k].Select(x => x.Name)))} "
                              + $"| {(bySame[k].Count == 0 ? "**測れなかった**" : "**測れる**")} |");
        Console.WriteLine();
        if (wSameTotal == 0)
            Console.WriteLine("**5枚とも同じ数値の他の素体が存在しない。§2-4 の後半は測れなかった**"
                              + "（指示書 §5-2 の字義どおり、近い数値で代用しない）。");
        else
        {
            Console.WriteLine("**同じ数値の他の素体に差し替えた版**（差し替え先も特性なしの素体なので、");
            Console.WriteLine("「その駒を素体にした版」と**盤面の数値が1つも違わない**。違うのは Id だけ）。");
            Console.WriteLine();
            Console.WriteLine("| 版 | 在席の標本 | 傾き | **Δ（現行比）** | 素体版の Δ | **差** | 判定（±1.5pt） |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|:-:|");
            for (int k = 0; k < 5; k++)
            {
                int v = 7 + k;
                if (bySame[k].Count == 0)
                {
                    Console.WriteLine($"| {wvName[v]} | — | — | — | — | — | **測れなかった** |");
                    continue;
                }
                var rate = WRates(v);
                wSl[v] = BySlopeOfBox(rate, wBox);
                var idx = Enumerable.Range(0, ByN).Where(i => wHas[i][v]).ToArray();
                double d = wSl[v] - w0, dp = wSl[1 + k] - w0;
                Console.WriteLine($"| {wvName[v]} | {idx.Length:N0} | {ByP1(wSl[v])} | {ByP2(d)} | {ByP2(dp)} "
                                  + $"| **{ByP2(d - dp)}** | {(Math.Abs(d - dp) <= 1.5 ? "**数値の話**" : "**数値の外**")} |");
            }
            Console.WriteLine();
            Console.WriteLine("**判定（Q3）: 同じ数値の他の素体と傾きが ±1.5pt 以内で一致すれば「数値の話」。**");
        }
        Console.WriteLine();

        // §2-4 の後半は「素体は（攻・HP・速・型）で完全に決まる」ので**恒等的に一致する**。
        // 恒等式ではない形で同じ問いに答えるために、**総攻・総HP・総速 を揃えた組の中で傾きを測り直す**
        // （素体化は攻・HP・速 を1つも動かさないので、**組は全版で同じ**）。
        Console.WriteLine("## §2-4 後半（恒等式でない版）—— 総攻・総HP・総速 を揃えた組の中の傾き");
        Console.WriteLine();
        {
            double lg = Math.Log(1.05);
            var feat = new double[ByN][];
            Parallel.For(0, ByN, i => feat[i] = ByFeat(ByTeam(1, i)));
            var cell = new Dictionary<(int, int, int), List<int>>();
            for (int i = 0; i < ByN; i++)
            {
                var key = ((int)Math.Floor(Math.Log(Math.Max(1, feat[i][0])) / lg),
                           (int)Math.Floor(Math.Log(Math.Max(1, feat[i][1])) / lg),
                           (int)Math.Floor(Math.Log(Math.Max(1, feat[i][2])) / lg));
                if (!cell.TryGetValue(key, out var lst)) cell[key] = lst = new List<int>();
                lst.Add(i);
            }
            var big = cell.Values.Where(v => v.Count >= 20).ToArray();
            var used = big.SelectMany(v => v).ToArray();
            Console.WriteLine($"組は **{big.Length}** 個・被覆 **{used.Length:N0} / {ByN:N0}**"
                              + $"（{100.0 * used.Length / ByN:F1}%）。**素体化は攻・HP・速を1つも動かさないので、組は全版で同じ。**");
            Console.WriteLine();
            Console.WriteLine("| 版 | 素のままの傾き | **組の中の傾き** | 数値で説明が付いた分 |");
            Console.WriteLine("|---|--:|--:|--:|");
            for (int v = 0; v <= 6; v++)
            {
                var rate = WRates(v);
                var adj = new double[ByN];
                foreach (var g in big)
                {
                    double m = g.Average(i => rate[i]);
                    foreach (int i in g) adj[i] = rate[i] - m;
                }
                var bk = new List<double>[4];
                for (int j = 0; j < 4; j++) bk[j] = new List<double>();
                foreach (int i in used) bk[Math.Min(3, wBox[i])].Add(adj[i]);
                double d10 = (bk[0].Count > 0 && bk[1].Count > 0) ? bk[1].Average() - bk[0].Average() : double.NaN;
                double d21 = (bk[1].Count > 0 && bk[2].Count > 0) ? bk[2].Average() - bk[1].Average() : double.NaN;
                double sl = double.IsNaN(d10) ? d21 : double.IsNaN(d21) ? d10 : (d10 + d21) / 2;
                Console.WriteLine($"| {wvName[v]} | {ByP1(wSl[v])} | **{ByP1(sl)}** | {ByP2(sl - wSl[v])} |");
            }
            Console.WriteLine();
            Console.WriteLine("**「5枚とも素体」の行が第73期の −7.2 に当たる。組の中でもそれが残るなら、");
            Console.WriteLine("−7.2 は総攻・総HP・総速 では説明が付かない**——**数値の外**（席・速さの並び・攻撃型）にある。");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {bySw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    if (byArg != "" && byArg != "alt")
    {
        Console.WriteLine("body: 引数は phase0 / （無し）/ alt / wound [alt]。");
        return;
    }

    // =====================================================================================
    // 主表（表B / C / D / F）
    // =====================================================================================
    // 台0 = ドラフト Pw / 台1 = ドラフト S'w / 台2 = 理想61行
    const int ByT = 3;
    string[] byTName = { "ドラフト Pw（素朴 × 弱い波）", "ドラフト S'w（傾き志向 × 弱い波）", "理想61行（既存5波）" };
    var byX = new double[ByT][][];
    var byY = new double[ByT][];

    for (int t = 0; t < 2; t++)
    {
        int rule = t == 0 ? 1 : 3;
        var feats = new double[ByN][];
        var rate = new double[ByN];
        int done = 0;
        Console.Error.Write($"{byBandName}帯 {(t == 0 ? "Pw" : "S'w")}: ");
        Parallel.For(0, ByN, i =>
        {
            UnitDef[] team = ByTeam(rule, i);
            int[] seats = BySeats(team);
            Formation f = ByForm(team, seats, null);
            double sum = 0;
            for (int w = 1; w < byW; w++)
            {
                int win = 0;
                for (int seed = byBand; seed < byBand + ByM; seed++)
                    if (BattleEngine.Run(f, byWeak[w].Enemy, seed, verbose: false).PlayerWon) win++;
                sum += win * 100.0 / ByM;
            }
            feats[i] = ByFeat(team); rate[i] = sum / (byW - 1);
            int c = Interlocked.Increment(ref done);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        byX[t] = feats; byY[t] = rate;
    }
    {
        int n = byAllRows.Length;
        var feats = new double[n][];
        var rate = new double[n];
        Console.Error.Write($"{byBandName}帯 理想61行: ");
        int idealSeed0 = byBand;
        Parallel.For(0, n, i =>
        {
            var team = byAllRows[i].F.Occupied().Select(o => o.Def).ToArray();
            double sum = 0;
            for (int w = 1; w < byW; w++)
            {
                int win = 0;
                for (int seed = idealSeed0; seed < idealSeed0 + BySeedsIdeal; seed++)
                    if (BattleEngine.Run(byAllRows[i].F, byStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                sum += win * 100.0 / BySeedsIdeal;
            }
            feats[i] = ByFeat(team); rate[i] = sum / (byW - 1);
        });
        Console.Error.WriteLine();
        byX[2] = feats; byY[2] = rate;
    }

    Console.WriteLine($"# 第76期 —— ドラフト台で「体」は何を決めているか（{byBandName} 帯）");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 body{(byBand == 0 ? "" : " alt")}` の出力。**`docs/` には置かない。**");
    Console.WriteLine();
    Console.WriteLine($"ドラフト台: 標本 **{ByN:N0}** × {byW - 1} 波 × seed **{ByM}** 本"
                      + $"（seed {byBand}..{byBand + ByM - 1}・弱い波 {ByWeakPct}%・規則配置 H）。");
    Console.WriteLine($"理想台: **{byAllRows.Length} 行** × {byW - 1} 波 × seed **{BySeedsIdeal}** 本"
                      + $"（seed {byBand}..{byBand + BySeedsIdeal - 1}・**既存波**）。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 標本 | 平均勝率 | 0% の標本 | 100% の標本 | 標準偏差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    for (int t = 0; t < ByT; t++)
        Console.WriteLine($"| {byTName[t]} | {byY[t].Length:N0} | {byY[t].Average():F2}% "
                          + $"| {byY[t].Count(x => x <= 0.0):N0} ({100.0 * byY[t].Count(x => x <= 0.0) / byY[t].Length:F1}%) "
                          + $"| {byY[t].Count(x => x >= 100.0):N0} | {BySd(byY[t]):F2} |");
    Console.WriteLine();

    // ---- 表B: 3段の R² -----------------------------------------------------------------
    var byNum = Enumerable.Range(0, ByF).Where(c => byFGrp[c] == 0).ToArray();
    var byNumShape = Enumerable.Range(0, ByF).Where(c => byFGrp[c] <= 1).ToArray();
    var byAll = Enumerable.Range(0, ByF).ToArray();
    var byFour = new[] { 0, 1, 2 };   // P3 の「4変数」——枚数は全台で 5 の定数なので落ちる
    var byRowsAll = new List<int>[ByT];
    var byRowsPos = new List<int>[ByT];
    for (int t = 0; t < ByT; t++)
    {
        byRowsAll[t] = Enumerable.Range(0, byY[t].Length).ToList();
        byRowsPos[t] = Enumerable.Range(0, byY[t].Length).Where(i => byY[t][i] > 0.0).ToList();
    }

    Console.WriteLine("## 表B —— 3段の R²（**主判定 Q1**）");
    Console.WriteLine();
    Console.WriteLine("| 台 | 標本 | 総攻・総HP・総速 のみ(3) | **数値のみ(7)** | 数値＋形(12) | **全部(25)** |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    for (int t = 0; t < ByT; t++)
        for (int q = 0; q < 2; q++)
        {
            var rows = q == 0 ? byRowsAll[t] : byRowsPos[t];
            Console.WriteLine($"| {byTName[t]}{(q == 0 ? "（0% 含む）" : "（**0% 除く**）")} | {rows.Count:N0} "
                              + $"| {ByR(ByR2(byX[t], byY[t], byFour, rows))} "
                              + $"| **{ByR(ByR2(byX[t], byY[t], byNum, rows))}** "
                              + $"| {ByR(ByR2(byX[t], byY[t], byNumShape, rows))} "
                              + $"| **{ByR(ByR2(byX[t], byY[t], byAll, rows))}** |");
        }
    Console.WriteLine();
    Console.WriteLine("**判定線: 「数値のみ」で R² ≥ 0.5 なら未説明 93% は数値の話。< 0.5 なら数値の外。**");
    Console.WriteLine();

    // ---- 単変数（P1）--------------------------------------------------------------------
    Console.WriteLine("## 表B-2 —— 単変数の R²（P1: 総HP が総攻より強いか）");
    Console.WriteLine();
    Console.WriteLine("| 変数 | 群 | Pw | S'w | 理想61行 |");
    Console.WriteLine("|---|---|--:|--:|--:|");
    foreach (int c in Enumerable.Range(0, ByF).OrderByDescending(c => ByR2(byX[0], byY[0], new[] { c }, byRowsAll[0])))
        Console.WriteLine($"| {byFName[c]} | {byGrpName[byFGrp[c]]} "
                          + string.Concat(Enumerable.Range(0, ByT).Select(t => $"| {ByR(ByR2(byX[t], byY[t], new[] { c }, byRowsAll[t]))} ")) + "|");
    Console.WriteLine();
    Console.WriteLine("**Pw の R² の降順に並べてある。**");
    Console.WriteLine();

    // ---- 表C: 群ごとの説明力 -------------------------------------------------------------
    Console.WriteLine("## 表C —— 群ごとの説明力（**Q2**・全部の R² からその群を落とした低下）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 本数 | Pw | 順位 | S'w | 順位 | 理想61行 | 順位 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    var byDrop = new double[ByT][];
    for (int t = 0; t < ByT; t++)
    {
        byDrop[t] = new double[4];
        double full = ByR2(byX[t], byY[t], byAll, byRowsAll[t]);
        for (int g = 0; g < 4; g++)
            byDrop[t][g] = full - ByR2(byX[t], byY[t], byAll.Where(c => byFGrp[c] != g).ToArray(), byRowsAll[t]);
    }
    var byRank = new int[ByT][];
    for (int t = 0; t < ByT; t++)
    {
        byRank[t] = new int[4];
        var ord = Enumerable.Range(0, 4).OrderByDescending(g => byDrop[t][g]).ToArray();
        for (int r = 0; r < 4; r++) byRank[t][ord[r]] = r + 1;
    }
    for (int g = 0; g < 4; g++)
        Console.WriteLine($"| **{byGrpName[g]}** | {Enumerable.Range(0, ByF).Count(c => byFGrp[c] == g)} "
                          + string.Concat(Enumerable.Range(0, ByT).Select(t => $"| {byDrop[t][g]:F4} | {byRank[t][g]} ")) + "|");
    Console.WriteLine();
    Console.WriteLine("**両 seed 帯で順位が一致することが Q2 の判定。**（B 帯は `body alt`）");
    Console.WriteLine();

    // ---- P4: 交互作用 --------------------------------------------------------------------
    Console.WriteLine("## 表C-2 —— P4（総攻 × 総HP は単独の和より大きいか）");
    Console.WriteLine();
    Console.WriteLine("| 台 | 総攻のみ | 総HP のみ | 総攻＋総HP | ＋交互作用 | **交互作用の上積み** |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    for (int t = 0; t < ByT; t++)
    {
        double a = ByR2(byX[t], byY[t], new[] { 0 }, byRowsAll[t]);
        double b = ByR2(byX[t], byY[t], new[] { 1 }, byRowsAll[t]);
        double ab = ByR2(byX[t], byY[t], new[] { 0, 1 }, byRowsAll[t]);
        double abi = ByR2(byX[t], byY[t], new[] { 0, 1, 24 }, byRowsAll[t]);
        Console.WriteLine($"| {byTName[t]} | {ByR(a)} | {ByR(b)} | {ByR(ab)} | {ByR(abi)} | **{abi - ab:F4}** |");
    }
    Console.WriteLine();

    // ---- 表D: 統制した実験 ---------------------------------------------------------------
    Console.WriteLine("## 表D —— 統制した実験（**Q5**・総攻/総HP/総速 を ±5% で揃えた組）");
    Console.WriteLine();
    double byLg = Math.Log(1.05);
    for (int t = 0; t < 2; t++)
    {
        var cell = new Dictionary<(int, int, int), List<int>>();
        for (int i = 0; i < ByN; i++)
        {
            var key = ((int)Math.Floor(Math.Log(Math.Max(1, byX[t][i][0])) / byLg),
                       (int)Math.Floor(Math.Log(Math.Max(1, byX[t][i][1])) / byLg),
                       (int)Math.Floor(Math.Log(Math.Max(1, byX[t][i][2])) / byLg));
            if (!cell.TryGetValue(key, out var lst)) cell[key] = lst = new List<int>();
            lst.Add(i);
        }
        var big = cell.Values.Where(v => v.Count >= 20).ToArray();
        int cov = big.Sum(v => v.Count);
        double wsd = big.Sum(v => v.Count * BySd(v.Select(i => byY[t][i]).ToArray())) / Math.Max(1, cov);
        Console.WriteLine($"**{byTName[t]}**: 20標本以上の組 **{big.Length}** 個・被覆 **{cov:N0} / {ByN:N0}**"
                          + $"（{100.0 * cov / ByN:F1}%）。");
        Console.WriteLine();
        Console.WriteLine("| 量 | 値 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| **組の中の勝率の標準偏差（加重平均）** | **{wsd:F2}pt** |");
        Console.WriteLine($"| 台全体の標準偏差 | {BySd(byY[t]):F2}pt |");
        Console.WriteLine($"| 比（組の中 ÷ 全体） | {100 * wsd / BySd(byY[t]):F1}% |");
        Console.WriteLine();
        var hi = new List<int>(); var lo = new List<int>();
        foreach (var v in big)
        {
            var ord = v.OrderByDescending(i => byY[t][i]).ThenBy(i => i).ToArray();
            int q = Math.Max(1, ord.Length / 4);
            hi.AddRange(ord.Take(q)); lo.AddRange(ord.TakeLast(q));
        }
        Console.WriteLine($"組の中の**上位25%（{hi.Count:N0} 標本）と下位25%（{lo.Count:N0} 標本）**の違い:");
        Console.WriteLine();
        Console.WriteLine("| 変数 | 上位25% | 下位25% | **差** |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| 勝率 | {hi.Average(i => byY[t][i]):F2}% | {lo.Average(i => byY[t][i]):F2}% "
                          + $"| **{ByP2(hi.Average(i => byY[t][i]) - lo.Average(i => byY[t][i]))}** |");
        foreach (int c in Enumerable.Range(0, ByF).Where(c => c != 24)
                          .OrderByDescending(c => Math.Abs(hi.Average(i => byX[t][i][c]) - lo.Average(i => byX[t][i][c]))
                                                  / Math.Max(1e-9, BySd(Enumerable.Range(0, ByN).Select(i => byX[t][i][c]).ToArray()))))
        {
            double h = hi.Average(i => byX[t][i][c]), l = lo.Average(i => byX[t][i][c]);
            Console.WriteLine($"| {byFName[c]} | {h:F2} | {l:F2} | {ByP2(h - l)} |");
        }
        Console.WriteLine();
    }
    Console.WriteLine("**判定: 組の中の標準偏差が 5pt 未満なら数値で決まっている。15pt 以上なら決まっていない。**");
    Console.WriteLine();

    // ---- 表F: 未説明の残り ---------------------------------------------------------------
    Console.WriteLine("## 表F —— 未説明の残り（全変数を入れても説明できない分散）");
    Console.WriteLine();
    Console.WriteLine("| 台 | 全部の R² | 残差の標準偏差 | 目的変数の標準偏差 | 残差 ÷ 全体 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    var byResid = new double[ByT][];
    for (int t = 0; t < ByT; t++)
    {
        byResid[t] = ByResidOf(byX[t], byY[t], byAll, byRowsAll[t]);
        Console.WriteLine($"| {byTName[t]} | {ByR(ByR2(byX[t], byY[t], byAll, byRowsAll[t]))} "
                          + $"| {BySd(byResid[t]):F2}pt | {BySd(byY[t]):F2}pt | {100 * BySd(byResid[t]) / BySd(byY[t]):F1}% |");
    }
    Console.WriteLine();
    for (int t = 0; t < 2; t++)
    {
        var ord = Enumerable.Range(0, ByN).OrderByDescending(i => Math.Abs(byResid[t][i])).ToArray();
        int q = ByN / 20;
        var worst = ord.Take(q).ToArray();
        var rest = ord.Skip(q).ToArray();
        Console.WriteLine($"**{byTName[t]}**: 残差の大きい 5%（{q:N0} 標本）は何が違うか。");
        Console.WriteLine();
        Console.WriteLine("| 変数 | 残差 上位5% | 残り95% | **差** |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| 勝率 | {worst.Average(i => byY[t][i]):F2}% | {rest.Average(i => byY[t][i]):F2}% "
                          + $"| {ByP2(worst.Average(i => byY[t][i]) - rest.Average(i => byY[t][i]))} |");
        Console.WriteLine($"| 残差（符号つき） | {worst.Average(i => byResid[t][i]):F2} | {rest.Average(i => byResid[t][i]):F2} | — |");
        foreach (int c in Enumerable.Range(0, ByF).Where(c => c != 24)
                          .OrderByDescending(c => Math.Abs(worst.Average(i => byX[t][i][c]) - rest.Average(i => byX[t][i][c]))
                                                  / Math.Max(1e-9, BySd(Enumerable.Range(0, ByN).Select(i => byX[t][i][c]).ToArray()))).Take(8))
        {
            double h = worst.Average(i => byX[t][i][c]), l = rest.Average(i => byX[t][i][c]);
            Console.WriteLine($"| {byFName[c]} | {h:F2} | {l:F2} | {ByP2(h - l)} |");
        }
        Console.WriteLine();
    }

    // ---- Q4: 攻撃力を買うと勝つ／攻10〜13を5枚で −7.2 --------------------------------------
    Console.WriteLine("## Q4 —— 「攻撃力を買うと勝つ」と「攻10〜13 を5枚で −7.2」の両立");
    Console.WriteLine();
    {
        int kw = UnitTally.CarryWound;
        var wounded = byRoster.Where(u => byKeyOf[u.Id].Contains(kw)).ToArray();
        Console.WriteLine("| 母集団 | 体数 | 攻 | HP | 速 | 特性数 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| ロスター全体 | {byRN} | {byRoster.Average(u => (double)u.Attack):F2} | {byRoster.Average(u => (double)u.MaxHp):F1} "
                          + $"| {byRoster.Average(u => (double)u.Speed):F2} | {byRoster.Average(u => (double)u.Traits.Count):F2} |");
        Console.WriteLine($"| **傷のキーを持つ駒** | {wounded.Length} | {wounded.Average(u => (double)u.Attack):F2} | {wounded.Average(u => (double)u.MaxHp):F1} "
                          + $"| {wounded.Average(u => (double)u.Speed):F2} | {wounded.Average(u => (double)u.Traits.Count):F2} |");
        var strong = byRoster.Where(u => u.Attack >= ByStrong).ToArray();
        Console.WriteLine($"| 攻{ByStrong}以上 | {strong.Length} | {strong.Average(u => (double)u.Attack):F2} | {strong.Average(u => (double)u.MaxHp):F1} "
                          + $"| {strong.Average(u => (double)u.Speed):F2} | {strong.Average(u => (double)u.Traits.Count):F2} |");
        Console.WriteLine();
        var cellQ4 = new Dictionary<(int, int, int), List<int>>();
        for (int i = 0; i < ByN; i++)
        {
            var key = ((int)Math.Floor(Math.Log(Math.Max(1, byX[0][i][0])) / byLg),
                       (int)Math.Floor(Math.Log(Math.Max(1, byX[0][i][1])) / byLg),
                       (int)Math.Floor(Math.Log(Math.Max(1, byX[0][i][2])) / byLg));
            if (!cellQ4.TryGetValue(key, out var lst)) cellQ4[key] = lst = new List<int>();
            lst.Add(i);
        }
        var bigQ4 = cellQ4.Values.Where(v => v.Count >= 20).ToArray();
        Console.WriteLine("**傷の枚数効果を、総攻・総HP・総速 を揃えた組の中だけで測り直す**");
        Console.WriteLine("（＝「体と枠の機会費用」のうち、この3つで説明が付く分を取り除いた傷の値段）。");
        Console.WriteLine();
        Console.WriteLine("| 測り方 | 0枚 | 1枚 | 2枚 | **傾き** |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        var boxAll = Enumerable.Range(0, ByN).Select(i => (int)Math.Min(3, byX[0][i][13 + kw])).ToArray();
        {
            var bm = new double[3];
            for (int j = 0; j < 3; j++)
            {
                var xs = Enumerable.Range(0, ByN).Where(i => boxAll[i] == j).Select(i => byY[0][i]).ToArray();
                bm[j] = xs.Length > 0 ? xs.Average() : double.NaN;
            }
            Console.WriteLine($"| 素のまま（第73期の器具） | {bm[0]:F2}% | {bm[1]:F2}% | {bm[2]:F2}% "
                              + $"| **{ByP1(BySlopeOfBox(byY[0], boxAll))}** |");
        }
        {
            var adj = new double[ByN];
            var used = new List<int>();
            foreach (var v in bigQ4)
            {
                double m = v.Average(i => byY[0][i]);
                foreach (int i in v) { adj[i] = byY[0][i] - m; used.Add(i); }
            }
            var bm = new double[3];
            for (int j = 0; j < 3; j++)
            {
                var xs = used.Where(i => boxAll[i] == j).Select(i => adj[i]).ToArray();
                bm[j] = xs.Length > 0 ? xs.Average() : double.NaN;
            }
            double d10 = bm[1] - bm[0], d21 = bm[2] - bm[1];
            Console.WriteLine($"| **数値を揃えた組の中**（n={used.Count:N0}） | {ByP2(bm[0])} | {ByP2(bm[1])} | {ByP2(bm[2])} "
                              + $"| **{ByP1((d10 + d21) / 2)}** |");
        }
        Console.WriteLine();
    }
    Console.WriteLine($"所要 {bySw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}
}
