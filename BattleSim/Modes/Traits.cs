using BattleCore;
using static Common;

// =====================================================================================
// traits モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "traits")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 traits
// =====================================================================================

static class TraitsDiag
{
// 特性の数はなぜ効くのか —— 在庫率か、単価か（第78期・調査）。
// **新機構ゼロ・駒ゼロ・ロスターの最後の1枠は使わない。`docs/` の差分はゼロ。**
//
// 第76期: 総攻・総HP・総速 を ±5% で揃えても勝率は 30.7pt 散り、
// **上位25%と下位25%を分ける第1の量は「特性の総数 +1.24枚」**だった。
// 第75期（帰属 = 解除率 × 単価）と第77期（帰属 = 在庫率 × 単価）は同じ形の式を出しているので、
// **「特性が多い駒が強いのは入口が多いぶん在庫率が高いから」**という仮説をこの期で測る。
//
// 足したのは診断1本と、**手で作った表2枚**（`TraitHookMap` / `TraitEntryMap`。
// どちらも `TraitKeyMap` と同じ場所＝このファイルの末尾に置いた）。
// **engine も `Traits.cs` も駒も波も1行も触っていない**ので、`compare` は 305 セル 0 件。
//
//     dotnet run --project BattleSim -c Release 0 traits phase0  # 紙の計算（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 traits         # 主表（A 帯: ドラフト seed 0..7 / 理想 0..199）
//     dotnet run --project BattleSim -c Release 0 traits alt     # B 帯（ドラフト 200..207 / 理想 200..399）
public static void Run(string[] args, int stageIndex)
{
    string trArg = args.Length > 2 ? args[2] : "";
    var trSw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> trStages = EnemyCatalog.Stages;
    int trW = trStages.Count;
    var trRoster = UnitCatalog.All.ToArray();
    int trRN = trRoster.Length;
    int trNK = UnitTally.CarryKeys.Length;

    // ---- 第76期の標本をそのまま使う（指示書 §1-5・§2-3。1文字も変えていない）---------------
    const int TrOfferSeed = 2_000_000;
    const int TrN = 11000, TrM = 8;
    const int TrStrong = 7, TrWeakPct = 60;
    const int TrSeedsIdeal = 200;
    int trBand = trArg == "alt" ? 200 : 0;
    string trBandName = trBand == 0 ? "A" : "B";

    var trKeyOf = trRoster.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);
    var trHookOf = trRoster.ToDictionary(u => u.Id, TraitHookMap.HooksOf);
    var trEntOf = trRoster.ToDictionary(u => u.Id, u => TraitEntryMap.EntriesOf(u, withFoe: false));
    var trEntFoeOf = trRoster.ToDictionary(u => u.Id, u => TraitEntryMap.EntriesOf(u, withFoe: true));

    int[] TrKeys(UnitDef d) => trKeyOf.TryGetValue(d.Id, out int[]? k) ? k : TraitKeyMap.KeysOf(d);
    int TrHooks(UnitDef d) => (trHookOf.TryGetValue(d.Id, out string[]? h) ? h : TraitHookMap.HooksOf(d)).Length;
    int TrEnt(UnitDef d) => (trEntOf.TryGetValue(d.Id, out int[]? e) ? e : TraitEntryMap.EntriesOf(d, false)).Length;
    int TrEntFoe(UnitDef d) => (trEntFoeOf.TryGetValue(d.Id, out int[]? e) ? e : TraitEntryMap.EntriesOf(d, true)).Length;

    var trWeakCache = new Dictionary<string, UnitDef>();
    UnitDef TrWeakOf(UnitDef d)
    {
        if (trWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * TrWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        trWeakCache[d.Id] = w;
        return w;
    }
    var trWeak = trStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = TrWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef TrPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };

    // 規則 P（第70〜77期の写し）。**この期は S'w を回さない**（指示書 §2-3）。
    UnitDef[] TrTeam(int i)
    {
        var rng = new Random(TrOfferSeed + i);
        var idx = new int[trRN];
        for (int k = 0; k < trRN; k++) idx[k] = k;
        int remain = trRN, strong = 0;
        var picked = new UnitDef[5];
        for (int r = 0; r < 5; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = trRoster[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= TrStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(trRoster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int[] TrSeats(UnitDef[] u)
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
    Formation TrForm(UnitDef[] u, int[] seats, int plainIdx)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = k == plainIdx ? TrPlain(u[k]) : u[k];
        return f;
    }

    // ---- 説明変数（**測る前に固定**）--------------------------------------------------------
    //  群: 0 数値(7・第76期の写し) / 1 特性の総数(1) / 2 発火口(1) / 3 入口(1) / 4 キー(1)
    //  第76期の「形」「軸」「交互作用」は落としてある——この期の問いは
    //  **4つの量の識別力の順位**（Q1）で、群を落とすときに他の群が代理になると順位が読めない。
    //  ただし「数値のみ」との比較のために数値群だけは残す（第76期の R² と突き合わせられる）。
    const int TrF = 12;
    var trFName = new string[TrF];
    var trFGrp = new int[TrF];
    string[] trGrpName = { "数値", "特性の総数", "発火口", "入口", "キー" };
    trFName[0] = "総攻"; trFName[1] = "総HP"; trFName[2] = "総速";
    trFName[3] = "攻の最大値"; trFName[4] = "HP の最小値";
    trFName[5] = "攻の分散"; trFName[6] = "HP の分散";
    trFName[7] = "**特性の総数**"; trFName[8] = "**発火口の総数**";
    trFName[9] = "**入口の総数（味方）**"; trFName[10] = "**キーの総数**";
    trFName[11] = "入口の総数（敵含む）";
    for (int c = 0; c <= 6; c++) trFGrp[c] = 0;
    trFGrp[7] = 1; trFGrp[8] = 2; trFGrp[9] = 3; trFGrp[10] = 4; trFGrp[11] = 3;

    double[] TrFeat(IReadOnlyList<UnitDef> u)
    {
        int n = u.Count;
        var f = new double[TrF];
        double sa = u.Sum(x => (double)x.Attack), sh = u.Sum(x => (double)x.MaxHp), sp = u.Sum(x => (double)x.Speed);
        f[0] = sa; f[1] = sh; f[2] = sp;
        f[3] = u.Max(x => (double)x.Attack); f[4] = u.Min(x => (double)x.MaxHp);
        double ma = sa / n, mh = sh / n;
        f[5] = u.Sum(x => (x.Attack - ma) * (x.Attack - ma)) / n;
        f[6] = u.Sum(x => (x.MaxHp - mh) * (x.MaxHp - mh)) / n;
        f[7] = u.Sum(x => (double)x.Traits.Count);
        f[8] = u.Sum(x => (double)TrHooks(x));
        f[9] = u.Sum(x => (double)TrEnt(x));
        f[10] = u.Sum(x => (double)TrKeys(x).Length);
        f[11] = u.Sum(x => (double)TrEntFoe(x));
        return f;
    }

    // ---- 最小二乗（第76期の写し。**1文字も変えていない**）------------------------------------
    double[] TrBeta(double[][] X, double[] y, IReadOnlyList<int> cols, IReadOnlyList<int> rows,
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
    double TrR2(double[][] X, double[] y, IReadOnlyList<int> cols, IReadOnlyList<int> rows)
    {
        int n = rows.Count;
        if (n < 10) return double.NaN;
        double[] beta = TrBeta(X, y, cols, rows, out List<int> use, out List<double> mu, out List<double> sd, out double ym);
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

    string TrP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    string TrR(double x) => double.IsNaN(x) ? "—" : x.ToString("F3");
    double TrSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double TrCorr(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        int n = a.Count;
        double ma = a.Average(), mb = b.Average(), sa = 0, sb = 0, sab = 0;
        for (int i = 0; i < n; i++)
        {
            double da = a[i] - ma, db = b[i] - mb;
            sa += da * da; sb += db * db; sab += da * db;
        }
        return (sa <= 1e-12 || sb <= 1e-12) ? double.NaN : sab / Math.Sqrt(sa * sb);
    }

    var trAllRows = CompareBuilds();

    // =====================================================================================
    // phase0: 紙の計算（**戦闘を1回も回さない**）
    // =====================================================================================
    if (trArg == "phase0")
    {
        Console.WriteLine("# 第78期 Phase 0 —— 特性の数を3つに分け直す（紙の計算）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `dotnet run --project BattleSim -c Release 0 traits phase0`");
        Console.WriteLine();
        Console.WriteLine("**指示書 §1-4（第76期の識別の再現）は戦闘が要るので、主表の冒頭に置いた**"
                          + "（`traits` の §0）。phase0 は §1-1〜§1-3 と §1-5・§1-6 だけ。");
        Console.WriteLine();

        // ---- §1-1: 特性数の分布 ----------------------------------------------------------
        Console.WriteLine($"## 1-1. ロスターの特性数の分布（**{trRN}体**）");
        Console.WriteLine();
        var tcHist = new Dictionary<int, int>();
        foreach (UnitDef d in trRoster)
        {
            int c = d.Traits.Count;
            tcHist[c] = tcHist.TryGetValue(c, out int v) ? v + 1 : 1;
        }
        double tcMean = trRoster.Average(u => (double)u.Traits.Count);
        Console.WriteLine("| 特性数 | 体数 | 指示書の当たり | 判定 |");
        Console.WriteLine("|--:|--:|---|:-:|");
        var guess = new Dictionary<int, int> { [1] = 38, [2] = 12, [4] = 1 };
        foreach (int c in tcHist.Keys.OrderBy(x => x))
            Console.WriteLine($"| {c} | {tcHist[c]} "
                              + $"| {(guess.TryGetValue(c, out int g) ? g.ToString() : "—")} "
                              + $"| {(guess.TryGetValue(c, out int g2) && g2 == tcHist[c] ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine($"**平均 {tcMean:F2}**（指示書の当たり 1.29: {(Math.Abs(tcMean - 1.29) < 0.005 ? "○" : "**×**")}）。");
        Console.WriteLine($"第76期の「+1.24枚」は**平均の {100 * 1.24 / tcMean:F0}%** に当たる。");
        Console.WriteLine();

        // ---- 表A ------------------------------------------------------------------------
        Console.WriteLine("## 表A —— 駒ごとの4つの量");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 攻/HP/速 | 特性 | 特性数 | 発火口 | **入口(味)** | 入口(敵含) | キー | 入口の中身 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|");
        foreach (UnitDef d in trRoster.OrderByDescending(u => TrEnt(u))
                                      .ThenByDescending(u => u.Traits.Count)
                                      .ThenBy(u => u.Id, StringComparer.Ordinal))
            Console.WriteLine($"| {d.Name} | {d.Attack}/{d.MaxHp}/{d.Speed} "
                              + $"| {string.Join("・", d.Traits.Select(t => t.ToString()))} "
                              + $"| {d.Traits.Count} | {TrHooks(d)} | **{TrEnt(d)}** | {TrEntFoe(d)} | {TrKeys(d).Length} "
                              + $"| {(TrEnt(d) == 0 ? "—" : string.Join("・", trEntOf[d.Id].Select(k => UnitTally.CarryKeys[k])))} |");
        Console.WriteLine();
        Console.WriteLine("**脚注**: 死・撃破は 11 本のキーに無いので、墓守・継ぎ接ぎ・処刑・追い打ちの入口は 0 と数えている。");
        Console.WriteLine("**鱗の自給（味方の死で自分に破片が乗る）は打ち消していない**"
                          + "——供給が「味方の死」という外部の事象に縛られているため。");
        Console.WriteLine("**発火口の `engine` は擬似フック**（札の本体が engine にある場合だけ数える。"
                          + "`OnCarryOver` は単発戦では原理的に走らないので数えない）。");
        Console.WriteLine();

        // 表の網羅性の検査。**手で作った表なので、駒を足したときに静かに 0 と数え始めるのを止める。**
        // `TraitKeyMap` と違って発火口は「載っていない」と「発火口が無い」が区別できないため。
        var missHook = trRoster.SelectMany(u => u.Traits).Distinct()
                               .Where(t => !TraitHookMap.TraitHooks.ContainsKey(t)).ToArray();
        Console.WriteLine($"**`TraitHookMap` の網羅**: ロスターが使う特性 "
                          + $"{trRoster.SelectMany(u => u.Traits).Distinct().Count()} 種のうち、表に無いのは "
                          + $"**{missHook.Length} 種**"
                          + (missHook.Length == 0 ? "（○）。" : $"——**{string.Join("・", missHook)}**（**×**）。"));
        Console.WriteLine("**`TraitEntryMap` は「読みが無い」＝入口 0 が正しい答えなので、同じ検査はできない**"
                          + "（自己検査 (b)）。代わりに**表Aの「入口の中身」列に何が入っているかを目で見ること。**");
        Console.WriteLine();

        Console.WriteLine("### 4つの量の分布");
        Console.WriteLine();
        Console.WriteLine("| 量 | 平均 | 中央値 | 最小 | 最大 | 0 の体数 | 標準偏差 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        var qName = new[] { "特性数", "発火口", "**入口(味)**", "入口(敵含)", "キー" };
        var qVal = new Func<UnitDef, double>[]
        {
            d => d.Traits.Count, d => TrHooks(d), d => TrEnt(d), d => TrEntFoe(d), d => TrKeys(d).Length
        };
        for (int q = 0; q < 5; q++)
        {
            var xs = trRoster.Select(qVal[q]).OrderBy(x => x).ToArray();
            double med = xs.Length % 2 == 1 ? xs[xs.Length / 2] : (xs[xs.Length / 2 - 1] + xs[xs.Length / 2]) / 2.0;
            Console.WriteLine($"| {qName[q]} | {xs.Average():F2} | {med:F1} | {xs[0]:F0} | {xs[^1]:F0} "
                              + $"| {xs.Count(x => x == 0)} | {TrSd(xs):F2} |");
        }
        Console.WriteLine();

        // ---- §1-3: 相関 -----------------------------------------------------------------
        Console.WriteLine("## 1-3. 相関行列（**Q2**・駒を単位に・n = " + trRN + "）");
        Console.WriteLine();
        var cName = new[] { "特性数", "発火口", "入口(味)", "入口(敵含)", "キー", "攻", "HP", "速" };
        var cVal = new Func<UnitDef, double>[]
        {
            d => d.Traits.Count, d => TrHooks(d), d => TrEnt(d), d => TrEntFoe(d), d => TrKeys(d).Length,
            d => d.Attack, d => d.MaxHp, d => d.Speed
        };
        var cols = cVal.Select(f => trRoster.Select(f).ToArray()).ToArray();
        Console.WriteLine("| | " + string.Join(" | ", cName) + " |");
        Console.WriteLine("|---" + string.Concat(cName.Select(_ => "|--:")) + "|");
        for (int a = 0; a < cName.Length; a++)
        {
            Console.Write($"| **{cName[a]}** ");
            for (int b = 0; b < cName.Length; b++)
            {
                double r = a == b ? 1.0 : TrCorr(cols[a], cols[b]);
                bool hot = a != b && Math.Abs(r) >= 0.9;
                Console.Write($"| {(hot ? "**" : "")}{r:F3}{(hot ? "**" : "")} ");
            }
            Console.WriteLine("|");
        }
        Console.WriteLine();
        int hotPairs = 0;
        for (int a = 0; a < 5; a++)
            for (int b = a + 1; b < 5; b++)
                if (Math.Abs(TrCorr(cols[a], cols[b])) >= 0.9) hotPairs++;
        Console.WriteLine($"**4つ（＋入口の敵含み版）の中で |r| ≥ 0.9 の組は {hotPairs} 組。**"
                          + " 0 組なら4つは別の量（Q2 通過）。1組以上あるなら**指示書 §5-2 の分岐**に入る"
                          + "（入口(味)と入口(敵含)は同じ量の2つの数え方なので、この組は数から除いてある）。");
        Console.WriteLine();

        // ---- §1-5 / §1-6 ----------------------------------------------------------------
        Console.WriteLine("## 1-5 / 1-6. 標本と現在値");
        Console.WriteLine();
        Console.WriteLine($"標本は第76期と同じ: **{TrN:N0} 標本 × 弱い波（HP {TrWeakPct}%）× seed {TrM} 本・規則配置 H・規則 P（Pw）**。");
        Console.WriteLine($"理想台は **{trAllRows.Length} 行 × {trW - 1} 波 × seed {TrSeedsIdeal} 本**（既存波）。");
        Console.WriteLine($"`docs/balance.md` の現在値は **{trAllRows.Length} 行 × {trW} 波 = {trAllRows.Length * trW} セル**"
                          + $"（指示書の 305: {(trAllRows.Length * trW == 305 ? "○" : "**×**")}）。");
        Console.WriteLine();

        Console.WriteLine("## 予測（**測る前に書いた**）");
        Console.WriteLine();
        Console.WriteLine("| # | 量 | 予測 |");
        Console.WriteLine("|--:|---|---|");
        Console.WriteLine("| P1 | 特性の総数と入口の数の相関 | **0.7〜0.9**。強いが同じではない |");
        Console.WriteLine("| P2 | 識別力の順位 | **入口 ＞ 特性の総数 ＞ 発火口 ＞ キー** |");
        Console.WriteLine("| P3 | 統制した比較 | **特性の総数を揃えて入口だけ振ると勝率が動く。逆は動かない** |");
        Console.WriteLine("| P4 | 単価 | **特性1つあたりの帰属は、特性の多い駒ほど小さい**（劣加法） |");
        Console.WriteLine("| P5 | 特性4つの1体 | **在席時勝率が最上位付近**（n=1 なので指標として並べるだけ） |");
        Console.WriteLine("| P6 | 理想台 | 同じ分解を理想61行に当てると**順位が変わる** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {trSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    if (trArg != "" && trArg != "alt")
    {
        Console.WriteLine("traits: 引数は phase0 / （無し）/ alt。");
        return;
    }

    // =====================================================================================
    // 主表（§0 の再現ゲート ＋ 表B〜F）
    // =====================================================================================
    // 台0 = ドラフト Pw（主）／台1 = 理想61行（比較）
    const int TrT = 2;
    string[] trTName = { "ドラフト Pw（素朴 × 弱い波）", "理想61行（既存5波）" };
    var trX = new double[TrT][][];
    var trY = new double[TrT][];
    // 帰属: 版 0 = 現行 / 版 1..5 = k 枚目を素体に
    var trWin = new double[TrT][][];
    var trMem = new UnitDef[TrT][][];

    {
        var feats = new double[TrN][];
        var rate = new double[TrN];
        var win = new double[TrN][];
        var mem = new UnitDef[TrN][];
        int done = 0;
        Console.Error.Write($"{trBandName}帯 Pw: ");
        Parallel.For(0, TrN, i =>
        {
            UnitDef[] team = TrTeam(i);
            int[] seats = TrSeats(team);
            var w = new double[6];
            for (int v = 0; v < 6; v++)
            {
                Formation f = TrForm(team, seats, v - 1);
                double sum = 0;
                for (int wi = 1; wi < trW; wi++)
                {
                    int wins = 0;
                    for (int seed = trBand; seed < trBand + TrM; seed++)
                        if (BattleEngine.Run(f, trWeak[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
                    sum += wins * 100.0 / TrM;
                }
                w[v] = sum / (trW - 1);
            }
            feats[i] = TrFeat(team); rate[i] = w[0]; win[i] = w; mem[i] = team;
            int c = Interlocked.Increment(ref done);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        trX[0] = feats; trY[0] = rate; trWin[0] = win; trMem[0] = mem;
    }
    {
        int n = trAllRows.Length;
        var feats = new double[n][];
        var rate = new double[n];
        var win = new double[n][];
        var mem = new UnitDef[n][];
        Console.Error.Write($"{trBandName}帯 理想61行: ");
        Parallel.For(0, n, i =>
        {
            var occ = trAllRows[i].F.Occupied().ToArray();
            var team = occ.Select(o => o.Def).ToArray();
            var w = new double[6];
            for (int v = 0; v < 6; v++)
            {
                if (v - 1 >= team.Length) { w[v] = double.NaN; continue; }
                var f = new Formation();
                for (int k = 0; k < occ.Length; k++)
                    f[occ[k].Slot] = k == v - 1 ? TrPlain(occ[k].Def) : occ[k].Def;
                double sum = 0;
                for (int wi = 1; wi < trW; wi++)
                {
                    int wins = 0;
                    for (int seed = trBand; seed < trBand + TrSeedsIdeal; seed++)
                        if (BattleEngine.Run(f, trStages[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
                    sum += wins * 100.0 / TrSeedsIdeal;
                }
                w[v] = sum / (trW - 1);
            }
            feats[i] = TrFeat(team); rate[i] = w[0]; win[i] = w; mem[i] = team;
        });
        Console.Error.WriteLine();
        trX[1] = feats; trY[1] = rate; trWin[1] = win; trMem[1] = mem;
    }

    Console.WriteLine($"# 第78期 —— 特性の数はなぜ効くのか（在庫率か、単価か）・{trBandName} 帯");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 traits{(trBand == 0 ? "" : " alt")}` の出力。**`docs/` には置かない。**");
    Console.WriteLine();
    Console.WriteLine($"ドラフト台: 標本 **{TrN:N0}** × 版 6（現行 ＋ 5枚を1枚ずつ素体）× {trW - 1} 波 × seed **{TrM}** 本"
                      + $"（seed {trBand}..{trBand + TrM - 1}・弱い波 {TrWeakPct}%・規則配置 H）。");
    Console.WriteLine($"理想台: **{trAllRows.Length} 行** × 版 6 × {trW - 1} 波 × seed **{TrSeedsIdeal}** 本"
                      + $"（seed {trBand}..{trBand + TrSeedsIdeal - 1}・既存波）。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 標本 | 平均勝率 | 0% の標本 | 100% の標本 | 標準偏差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    for (int t = 0; t < TrT; t++)
        Console.WriteLine($"| {trTName[t]} | {trY[t].Length:N0} | {trY[t].Average():F2}% "
                          + $"| {trY[t].Count(x => x <= 0.0):N0} ({100.0 * trY[t].Count(x => x <= 0.0) / trY[t].Length:F1}%) "
                          + $"| {trY[t].Count(x => x >= 100.0):N0} | {TrSd(trY[t]):F2} |");
    Console.WriteLine();

    var trRowsAll = new List<int>[TrT];
    var trRowsPos = new List<int>[TrT];
    for (int t = 0; t < TrT; t++)
    {
        trRowsAll[t] = Enumerable.Range(0, trY[t].Length).ToList();
        trRowsPos[t] = Enumerable.Range(0, trY[t].Length).Where(i => trY[t][i] > 0.0).ToList();
    }

    // ---- §0: 第76期の識別の再現（**器具の検算**。ここが合わなければ以降は読まない）-----------
    Console.WriteLine("## §0 —— 第76期の識別の再現（**器具の検算**）");
    Console.WriteLine();
    double trLg = Math.Log(1.05);
    List<int> trHi = new(), trLo = new();
    {
        var cell = new Dictionary<(int, int, int), List<int>>();
        for (int i = 0; i < TrN; i++)
        {
            var key = ((int)Math.Floor(Math.Log(Math.Max(1, trX[0][i][0])) / trLg),
                       (int)Math.Floor(Math.Log(Math.Max(1, trX[0][i][1])) / trLg),
                       (int)Math.Floor(Math.Log(Math.Max(1, trX[0][i][2])) / trLg));
            if (!cell.TryGetValue(key, out var lst)) cell[key] = lst = new List<int>();
            lst.Add(i);
        }
        var big = cell.Values.Where(v => v.Count >= 20).ToArray();
        int cov = big.Sum(v => v.Count);
        double wsd = big.Sum(v => v.Count * TrSd(v.Select(i => trY[0][i]).ToArray())) / Math.Max(1, cov);
        foreach (var v in big)
        {
            var ord = v.OrderByDescending(i => trY[0][i]).ThenBy(i => i).ToArray();
            int q = Math.Max(1, ord.Length / 4);
            trHi.AddRange(ord.Take(q)); trLo.AddRange(ord.TakeLast(q));
        }
        Console.WriteLine($"総攻・総HP・総速 を ±5% で揃えた組は **{big.Length}** 個・被覆 **{cov:N0} / {TrN:N0}**"
                          + $"（{100.0 * cov / TrN:F1}%）。組の中の勝率の標準偏差 **{wsd:F2}pt**"
                          + $"（台全体 {TrSd(trY[0]):F2}pt ＝ {100 * wsd / TrSd(trY[0]):F1}%）。");
        Console.WriteLine();
        Console.WriteLine("| 量 | 上位25% | 下位25% | **差** | 第76期 | 判定（±0.15） |");
        Console.WriteLine("|---|--:|--:|--:|--:|:-:|");
        double dTc = trHi.Average(i => trX[0][i][7]) - trLo.Average(i => trX[0][i][7]);
        // 第76期の「30.7pt」は**組の中の標準偏差**で、上位25%−下位25%の差は別の量（第76期の「72pt」）。
        // **2つを突き合わせないこと**——標準偏差はこの表の直前の行に出してある。
        double dW = trHi.Average(i => trY[0][i]) - trLo.Average(i => trY[0][i]);
        Console.WriteLine($"| 組の中の標準偏差 | — | — | **{wsd:F2}pt** | 30.7 "
                          + $"| {(Math.Abs(wsd - 30.7) <= 1.0 ? "○" : "**×**")} |");
        Console.WriteLine($"| 勝率（上位25% − 下位25%） | {trHi.Average(i => trY[0][i]):F2}% | {trLo.Average(i => trY[0][i]):F2}% "
                          + $"| **{TrP2(dW)}** | +72 "
                          + $"| {(Math.Abs(dW - 72.0) <= 2.0 ? "○" : "**×**")} |");
        Console.WriteLine($"| **特性の総数** | {trHi.Average(i => trX[0][i][7]):F2} | {trLo.Average(i => trX[0][i][7]):F2} "
                          + $"| **{TrP2(dTc)}** | +1.24 | {(Math.Abs(dTc - 1.24) <= 0.15 ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine("**再現しなければ以降の表は読まない**（指示書 §1-4）。");
        Console.WriteLine();
    }

    // ---- 表B: 回帰 ---------------------------------------------------------------------
    var trNum = Enumerable.Range(0, 7).ToArray();
    var trFourOnly = new[] { 7, 8, 9, 10 };
    var trAllCols = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };   // 入口(敵含)=11 は主モデルに入れない
    Console.WriteLine("## 表B —— 回帰（**主判定 Q1**）");
    Console.WriteLine();
    Console.WriteLine("| 台 | 標本 | 数値のみ(7) | **4つの量のみ(4)** | 数値＋4つ(11) | 数値に4つを足した上積み |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    for (int t = 0; t < TrT; t++)
        for (int q = 0; q < 2; q++)
        {
            var rows = q == 0 ? trRowsAll[t] : trRowsPos[t];
            double a = TrR2(trX[t], trY[t], trNum, rows);
            double b = TrR2(trX[t], trY[t], trFourOnly, rows);
            double c = TrR2(trX[t], trY[t], trAllCols, rows);
            Console.WriteLine($"| {trTName[t]}{(q == 0 ? "（0% 含む）" : "（**0% 除く**）")} | {rows.Count:N0} "
                              + $"| {TrR(a)} | **{TrR(b)}** | {TrR(c)} | {c - a:F4} |");
        }
    Console.WriteLine();
    Console.WriteLine("### 表B-2 —— 群を1つずつ落としたときの R² の低下（**Q1 の本体**）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 本数 | Pw（0% 含む） | 順位 | Pw（0% 除く） | 順位 | 理想61行 | 順位 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    var trDrop = new double[3][];
    var trRank = new int[3][];
    var trCase = new (int T, List<int> Rows, string Name)[]
    {
        (0, trRowsAll[0], "Pw（0% 含む）"), (0, trRowsPos[0], "Pw（0% 除く）"), (1, trRowsAll[1], "理想61行")
    };
    for (int c = 0; c < 3; c++)
    {
        trDrop[c] = new double[5];
        int t = trCase[c].T;
        double full = TrR2(trX[t], trY[t], trAllCols, trCase[c].Rows);
        for (int g = 0; g < 5; g++)
            trDrop[c][g] = full - TrR2(trX[t], trY[t], trAllCols.Where(x => trFGrp[x] != g).ToArray(), trCase[c].Rows);
        trRank[c] = new int[5];
        var ord = Enumerable.Range(0, 5).OrderByDescending(g => trDrop[c][g]).ToArray();
        for (int r = 0; r < 5; r++) trRank[c][ord[r]] = r + 1;
    }
    for (int g = 0; g < 5; g++)
        Console.WriteLine($"| **{trGrpName[g]}** | {trAllCols.Count(x => trFGrp[x] == g)} "
                          + string.Concat(Enumerable.Range(0, 3).Select(c => $"| {trDrop[c][g]:F4} | {trRank[c][g]} ")) + "|");
    Console.WriteLine();
    Console.WriteLine("**Q1: 入口の低下が特性の総数を上回るなら在庫率。下回るなら単価または別の何か。**");
    Console.WriteLine("**両 seed 帯で順位が一致することが条件**（B 帯は `traits alt`）。");
    Console.WriteLine();

    Console.WriteLine("### 表B-3 —— 単変数の R² と、数値群の上に1本だけ足したときの上積み");
    Console.WriteLine();
    Console.WriteLine("| 変数 | 群 | 単変数 Pw | 上積み Pw | 単変数 理想 | 上積み 理想 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|");
    double trBasePw = TrR2(trX[0], trY[0], trNum, trRowsAll[0]);
    double trBaseId = TrR2(trX[1], trY[1], trNum, trRowsAll[1]);
    foreach (int c in new[] { 7, 8, 9, 10, 11 })
        Console.WriteLine($"| {trFName[c]} | {trGrpName[trFGrp[c]]} "
                          + $"| {TrR(TrR2(trX[0], trY[0], new[] { c }, trRowsAll[0]))} "
                          + $"| {TrR2(trX[0], trY[0], trNum.Append(c).ToArray(), trRowsAll[0]) - trBasePw:F4} "
                          + $"| {TrR(TrR2(trX[1], trY[1], new[] { c }, trRowsAll[1]))} "
                          + $"| {TrR2(trX[1], trY[1], trNum.Append(c).ToArray(), trRowsAll[1]) - trBaseId:F4} |");
    Console.WriteLine();
    Console.WriteLine("**編成を単位にした相関行列**（駒ではなく標本。Q2 をこちら側でも見る）:");
    Console.WriteLine();
    {
        var nm = new[] { "特性の総数", "発火口", "入口(味)", "キー", "入口(敵含)" };
        var ix = new[] { 7, 8, 9, 10, 11 };
        Console.WriteLine("| | " + string.Join(" | ", nm) + " |");
        Console.WriteLine("|---" + string.Concat(nm.Select(_ => "|--:")) + "|");
        for (int a = 0; a < ix.Length; a++)
        {
            Console.Write($"| **{nm[a]}** ");
            for (int b = 0; b < ix.Length; b++)
            {
                double r = a == b ? 1.0
                    : TrCorr(Enumerable.Range(0, TrN).Select(i => trX[0][i][ix[a]]).ToArray(),
                             Enumerable.Range(0, TrN).Select(i => trX[0][i][ix[b]]).ToArray());
                bool hot = a != b && Math.Abs(r) >= 0.9;
                Console.Write($"| {(hot ? "**" : "")}{r:F3}{(hot ? "**" : "")} ");
            }
            Console.WriteLine("|");
        }
    }
    Console.WriteLine();

    // ---- 表C: 統制した比較 ---------------------------------------------------------------
    Console.WriteLine("## 表C —— 統制した比較（**Q3**）");
    Console.WriteLine();
    Console.WriteLine("片方の量を**整数値でぴったり揃えた**組の中で、もう片方の量の上半分と下半分の勝率を比べる。");
    Console.WriteLine("**組は「揃える量の値」ごとに作り、その中で振る量の中央値で二分する**"
                      + "（中央値と同値の標本はどちらにも入れない＝自由度を残さないため）。");
    Console.WriteLine();
    for (int dir = 0; dir < 2; dir++)
    {
        int fix = dir == 0 ? 7 : 9;    // 揃える量
        int var2 = dir == 0 ? 9 : 7;   // 振る量
        Console.WriteLine($"### C-{dir + 1}: **{trFName[fix]} を揃えて {trFName[var2]} を振る**");
        Console.WriteLine();
        var groups = Enumerable.Range(0, TrN).GroupBy(i => (int)Math.Round(trX[0][i][fix]))
                               .OrderBy(g => g.Key).ToArray();
        Console.WriteLine($"| {trFName[fix]} | 標本 | 上半分 n | 下半分 n | 上半分の {trFName[var2]} | 下半分 | 上半分の勝率 | 下半分の勝率 | **差** |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        int totHi = 0, totLo = 0;
        double sumHi = 0, sumLo = 0;
        foreach (var g in groups)
        {
            var arr = g.ToArray();
            if (arr.Length < 20) continue;
            var vals = arr.Select(i => trX[0][i][var2]).OrderBy(x => x).ToArray();
            double med = vals[vals.Length / 2];
            var hi = arr.Where(i => trX[0][i][var2] > med).ToArray();
            var lo = arr.Where(i => trX[0][i][var2] < med).ToArray();
            if (hi.Length == 0 || lo.Length == 0) continue;
            totHi += hi.Length; totLo += lo.Length;
            sumHi += hi.Sum(i => trY[0][i]); sumLo += lo.Sum(i => trY[0][i]);
            Console.WriteLine($"| {g.Key} | {arr.Length:N0} | {hi.Length:N0} | {lo.Length:N0} "
                              + $"| {hi.Average(i => trX[0][i][var2]):F2} | {lo.Average(i => trX[0][i][var2]):F2} "
                              + $"| {hi.Average(i => trY[0][i]):F2}% | {lo.Average(i => trY[0][i]):F2}% "
                              + $"| **{TrP2(hi.Average(i => trY[0][i]) - lo.Average(i => trY[0][i]))}** |");
        }
        Console.WriteLine();
        int tot = totHi + totLo;
        double diff = tot == 0 ? double.NaN : sumHi / Math.Max(1, totHi) - sumLo / Math.Max(1, totLo);
        Console.WriteLine($"**合計 {tot:N0} 標本**（上 {totHi:N0} / 下 {totLo:N0}）・**加重した差 {TrP2(diff)}pt**。");
        Console.WriteLine($"判定: 抽出できた組が 100 標本未満なら「測れなかった」——**{(tot >= 100 ? "測れた" : "**測れなかった**")}**。");
        Console.WriteLine($"差が 3pt 以上なら効いている——**{(Math.Abs(diff) >= 3.0 ? "効いている" : "**3pt 未満**")}**。");
        Console.WriteLine();
    }

    // ---- 表D / 表E: 駒ごとの帰属と単価 ----------------------------------------------------
    Console.WriteLine("## 表D —— 単価の分布（**Q4**・入口1本あたりの帰属）");
    Console.WriteLine();
    Console.WriteLine("**帰属 ＝ 現行 − その駒を素体にした版**（第69期の標準器具）。在席した標本だけで平均する。");
    Console.WriteLine();
    var trCon = new Dictionary<string, double>[TrT];
    var trOcc = new Dictionary<string, int>[TrT];
    var trWinIn = new Dictionary<string, double>[TrT];
    for (int t = 0; t < TrT; t++)
    {
        trCon[t] = new Dictionary<string, double>();
        trOcc[t] = new Dictionary<string, int>();
        trWinIn[t] = new Dictionary<string, double>();
        var sum = new Dictionary<string, double>();
        var sumW = new Dictionary<string, double>();
        var cnt = new Dictionary<string, int>();
        for (int i = 0; i < trY[t].Length; i++)
            for (int k = 0; k < trMem[t][i].Length && k < 5; k++)
            {
                string id = trMem[t][i][k].Id;
                if (double.IsNaN(trWin[t][i][k + 1])) continue;
                sum[id] = sum.GetValueOrDefault(id) + (trWin[t][i][0] - trWin[t][i][k + 1]);
                sumW[id] = sumW.GetValueOrDefault(id) + trWin[t][i][0];
                cnt[id] = cnt.GetValueOrDefault(id) + 1;
            }
        foreach (string id in cnt.Keys)
        {
            trCon[t][id] = sum[id] / cnt[id];
            trWinIn[t][id] = sumW[id] / cnt[id];
            trOcc[t][id] = cnt[id];
        }
    }
    Console.WriteLine("| 駒 | 特性数 | 発火口 | 入口 | キー | 在席（Pw） | **帰属（Pw）** | **単価＝帰属÷入口** | 在席時勝率 | 帰属（理想） |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (UnitDef d in trRoster.OrderByDescending(u => trCon[0].GetValueOrDefault(u.Id, double.NaN)))
    {
        int e = TrEnt(d);
        double con = trCon[0].GetValueOrDefault(d.Id, double.NaN);
        Console.WriteLine($"| {d.Name} | {d.Traits.Count} | {TrHooks(d)} | {e} | {TrKeys(d).Length} "
                          + $"| {trOcc[0].GetValueOrDefault(d.Id, 0):N0} | {TrP2(con)} "
                          + $"| {(e == 0 ? "—（分母 0）" : TrP2(con / e))} "
                          + $"| {trWinIn[0].GetValueOrDefault(d.Id, double.NaN):F2}% "
                          + $"| {(trCon[1].ContainsKey(d.Id) ? TrP2(trCon[1][d.Id]) : "—")} |");
    }
    Console.WriteLine();
    {
        var withEnt = trRoster.Where(d => TrEnt(d) >= 1 && trCon[0].ContainsKey(d.Id)).ToArray();
        var unit = withEnt.Select(d => trCon[0][d.Id] / TrEnt(d)).ToArray();
        double m = unit.Average(), sd = TrSd(unit);
        Console.WriteLine("| 量 | 値 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| 入口 ≥ 1 の駒 | **{withEnt.Length}** 体（**これが Q4 の分母**。入口 0 の {trRN - withEnt.Length} 体は定義できない） |");
        Console.WriteLine($"| 単価の平均 | {m:F3} pt/本 |");
        Console.WriteLine($"| 単価の標準偏差 | {sd:F3} |");
        Console.WriteLine($"| **変動係数（|平均| で割る）** | **{Math.Abs(sd / Math.Max(1e-9, Math.Abs(m))):F3}** |");
        Console.WriteLine();
        Console.WriteLine($"**Q4: 変動係数が 0.5 未満なら単価はほぼ一定**（設計で動かせるのは頻度だけ・第75/77期の再現）"
                          + $"——**{(Math.Abs(sd / Math.Max(1e-9, Math.Abs(m))) < 0.5 ? "0.5 未満" : "**0.5 以上（単価も設計対象）**")}**。");
        Console.WriteLine();
        Console.WriteLine("**但し書き（自己検査 (c)）**: 変動係数は平均で割るので、"
                          + $"平均が 0 に近いとき（実測 {m:F3} pt/本・単価は正負にまたがる）**大きさに意味が無い。**");
        Console.WriteLine($"散らばりそのものは **最小 {unit.Min():F2} / 中央 {unit.OrderBy(x => x).ToArray()[unit.Length / 2]:F2} "
                          + $"/ 最大 {unit.Max():F2} pt/本**（幅 {unit.Max() - unit.Min():F2}）で読むこと"
                          + "——**符号がまたがっている時点で「一定」ではない。**");
        Console.WriteLine();
        Console.WriteLine("### P4 —— 特性1つあたりの帰属は、特性が多い駒ほど小さいか（劣加法）");
        Console.WriteLine();
        Console.WriteLine("| 特性数 | 体数 | 帰属の平均 | **特性1つあたり** |");
        Console.WriteLine("|--:|--:|--:|--:|");
        foreach (var g in trRoster.Where(d => trCon[0].ContainsKey(d.Id)).GroupBy(d => d.Traits.Count).OrderBy(g => g.Key))
            Console.WriteLine($"| {g.Key} | {g.Count()} | {TrP2(g.Average(d => trCon[0][d.Id]))} "
                              + $"| **{TrP2(g.Average(d => trCon[0][d.Id]) / g.Key)}** |");
        Console.WriteLine();
        Console.WriteLine("| 相関（駒を単位に） | r |");
        Console.WriteLine("|---|--:|");
        var pool = trRoster.Where(d => trCon[0].ContainsKey(d.Id)).ToArray();
        Console.WriteLine($"| 帰属 × 特性数 | {TrCorr(pool.Select(d => trCon[0][d.Id]).ToArray(), pool.Select(d => (double)d.Traits.Count).ToArray()):F3} |");
        Console.WriteLine($"| 帰属 × 入口 | {TrCorr(pool.Select(d => trCon[0][d.Id]).ToArray(), pool.Select(d => (double)TrEnt(d)).ToArray()):F3} |");
        Console.WriteLine($"| 帰属 × 発火口 | {TrCorr(pool.Select(d => trCon[0][d.Id]).ToArray(), pool.Select(d => (double)TrHooks(d)).ToArray()):F3} |");
        Console.WriteLine($"| 帰属 × キー | {TrCorr(pool.Select(d => trCon[0][d.Id]).ToArray(), pool.Select(d => (double)TrKeys(d).Length).ToArray()):F3} |");
        Console.WriteLine();
    }

    Console.WriteLine("## 表E —— 入口 0 の駒（**Q5**）");
    Console.WriteLine();
    var zero = trRoster.Where(d => TrEnt(d) == 0).ToArray();
    var zeroFoe = trRoster.Where(d => TrEntFoe(d) == 0).ToArray();
    Console.WriteLine($"**入口(味方) 0 は {zero.Length} 体 / {trRN}。入口(敵含む) 0 は {zeroFoe.Length} 体。**");
    Console.WriteLine();
    if (zero.Length == 0)
        Console.WriteLine("**このロスターに自足した駒は存在しない**（指示書 §5-2 の分岐）。");
    else
    {
        Console.WriteLine("| 駒 | 特性 | 発火口 | キー | 敵含めても 0 か | 在席時勝率 | 帰属（Pw） |");
        Console.WriteLine("|---|---|--:|--:|:-:|--:|--:|");
        foreach (UnitDef d in zero.OrderByDescending(u => trWinIn[0].GetValueOrDefault(u.Id, 0.0)))
            Console.WriteLine($"| {d.Name} | {string.Join("・", d.Traits.Select(t => t.ToString()))} "
                              + $"| {TrHooks(d)} | {TrKeys(d).Length} | {(TrEntFoe(d) == 0 ? "○" : "—")} "
                              + $"| {trWinIn[0].GetValueOrDefault(d.Id, double.NaN):F2}% | {TrP2(trCon[0].GetValueOrDefault(d.Id, double.NaN))} |");
        Console.WriteLine();
        var have = trRoster.Where(d => TrEnt(d) >= 1).ToArray();
        Console.WriteLine("| 群 | 体数 | 在席時勝率の平均 | 帰属の平均 |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| **入口 0** | {zero.Length} | {zero.Where(d => trWinIn[0].ContainsKey(d.Id)).Average(d => trWinIn[0][d.Id]):F2}% "
                          + $"| {TrP2(zero.Where(d => trCon[0].ContainsKey(d.Id)).Average(d => trCon[0][d.Id]))} |");
        Console.WriteLine($"| **入口 ≥ 1** | {have.Length} | {have.Where(d => trWinIn[0].ContainsKey(d.Id)).Average(d => trWinIn[0][d.Id]):F2}% "
                          + $"| {TrP2(have.Where(d => trCon[0].ContainsKey(d.Id)).Average(d => trCon[0][d.Id]))} |");
        Console.WriteLine();
    }
    {
        // P5: 特性4つの1体
        var four = trRoster.Where(d => d.Traits.Count >= 4).ToArray();
        var ordWin = trRoster.Where(d => trWinIn[0].ContainsKey(d.Id))
                             .OrderByDescending(d => trWinIn[0][d.Id]).ToArray();
        Console.WriteLine("### P5 —— 特性4つの駒（**n=1 なので証拠にしない。指標として並べるだけ**）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 特性数 | 在席時勝率 | 在席時勝率の順位 | 帰属 | 帰属の順位 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        var ordCon = trRoster.Where(d => trCon[0].ContainsKey(d.Id))
                             .OrderByDescending(d => trCon[0][d.Id]).ToArray();
        foreach (UnitDef d in four)
            Console.WriteLine($"| {d.Name} | {d.Traits.Count} | {trWinIn[0].GetValueOrDefault(d.Id, double.NaN):F2}% "
                              + $"| {Array.FindIndex(ordWin, x => x.Id == d.Id) + 1} / {ordWin.Length} "
                              + $"| {TrP2(trCon[0].GetValueOrDefault(d.Id, double.NaN))} "
                              + $"| {Array.FindIndex(ordCon, x => x.Id == d.Id) + 1} / {ordCon.Length} |");
        Console.WriteLine();
    }

    // ---- 表F: 第76期との接続 ------------------------------------------------------------
    Console.WriteLine("## 表F —— 第76期との接続（「特性の総数 +1.24枚」を4つに分け直す）");
    Console.WriteLine();
    Console.WriteLine("| 量 | 上位25% | 下位25% | **差** | 標準偏差で割った差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (int c in new[] { 7, 8, 9, 10, 11 }
                      .OrderByDescending(c => Math.Abs(trHi.Average(i => trX[0][i][c]) - trLo.Average(i => trX[0][i][c]))
                                              / Math.Max(1e-9, TrSd(Enumerable.Range(0, TrN).Select(i => trX[0][i][c]).ToArray()))))
    {
        double h = trHi.Average(i => trX[0][i][c]), l = trLo.Average(i => trX[0][i][c]);
        double sdc = TrSd(Enumerable.Range(0, TrN).Select(i => trX[0][i][c]).ToArray());
        Console.WriteLine($"| {trFName[c]} | {h:F2} | {l:F2} | **{TrP2(h - l)}** | {TrP2((h - l) / Math.Max(1e-9, sdc))} |");
    }
    Console.WriteLine();
    Console.WriteLine("**標準偏差で割った差の順位が、第76期の「第1識別量」を4つに分け直したもの。**");
    Console.WriteLine();
    Console.WriteLine($"所要 {trSw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}
}
