using BattleCore;
using static Common;

// =====================================================================================
// pairs モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "pairs")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 pairs
// =====================================================================================

static class PairsDiag
{
// 自己供給と外部供給、ドラフト台ではどちらが強いか（第77期・ヨミ V9 の再測定）。
// **駒は作らない。ロスターの最後の1枠は使わない。`UnitCatalog` / `Stages` / `CompareBuilds()` は1行も動かさない。**
//
// 第66期（V9・条件は `AtkBonus` ＝**自分で作れる値**）は理想台で Q4（情報セル）に落ち、
// 第67期（V67・条件は `WhetReceived` ＝**外から届いた値**）は Q1（薙ぎ化率が2値に潰れた）に落ちた。
// **どちらもドラフト台では一度も測っていない。** ドラフト台は相方が保証されないので、
// **外部供給を前提とする V67 は構造的に不利**なはず——**その差の大きさがこの期の主判定**（指示書 §0-2）。
//
// engine には何も足していない。足したのは
//   (a) `CreakRule` の供給元の選択子 `CreakSource`（Whet / Bonus / Both。**既定は現行のまま**）と
//   (b) **誰も読んで分岐しない**到達ターンの計数 1 本（`UnitTally.CreakBothProbeTurn`）
// だけで、**`Threshold <= 0` で素通りする作法は変えていない**（`compare` 305 セル 0 件が検算）。
//
// 自由度は §1〜§2 で構造的に消してある（第64期の教訓・自己検査 (e)）:
//   台は2つに固定（ドラフト台 Pw ＝ 主判定 ／ 理想台のヨミ3行 ＝ 拒否権）／
//   版は10本に固定（V0 ＋ Bonus 9/18/30 ＝ 第66期の3点 ＋ Whet 2/6/12 ＝ 第67期の3点 ＋ Both 9/18/30）／
//   席は動かさない（`reseat` は帰属の器具ではない・第64期）／
//   採る版の規則は「Q2 を満たす版のうち**ドラフト台の帰属**が最大。同点は高い閾値」。
//
//     dotnet run --project BattleSim -c Release 0 creak3 phase0   # 紙の計算と分布（**盤面は動かない**）
//     dotnet run --project BattleSim -c Release 0 creak3          # 主表（A 帯: ドラフト seed 0..7 / 理想 0..199）
//     dotnet run --project BattleSim -c Release 0 creak3 alt      # B 帯（ドラフト 200..207 / 理想 200..399）
//     dotnet run --project BattleSim -c Release 0 creak3 check    # 陰性対照（Q7）
// 組み合わせの表（第80期・調査）—— 51 体・1,275 組のうち、どの組が「和より大きい」か。
// **新機構ゼロ・駒ゼロ・最後の1枠は使わない。engine も `Traits.cs` も `UnitCatalog` も `Stages` も `CompareBuilds()` も触らない。**
//
// 第69期以降の帰属（素体差し替え）は駒1枚の値段を測る器具で、**組の値は測っていない**。
// 第79期はそれで落ちた（単騎 +1.00 / +1.50 に対し軸あり ±0.00）——組の表があれば作る前に分かった。
// この期は第76〜79期のドラフト台（11,000 標本 × 弱い波 × seed 8本・規則 P・規則配置 H）を**同じ抽選で再生成し**、
// 各標本の「在席した5体」と「勝率（第2〜5波平均）」から 1,275 組の 単独 / 組 / 相乗 を集計する（指示書 §0-2）:
//
//     単独(A)   = A が在席した標本の平均勝率 − A が在席しない標本の平均勝率
//     組(A,B)   = 両方在席の平均 − どちらも不在の平均
//     相乗(A,B) = 組(A,B) − 単独(A) − 単独(B)
//
// **これは在席差の器具で、帰属（素体差し替え）とは別物**（第69期: 1枚抜きと素体差し替えは r = 0.782 の別物）。
// 混ぜない——帰属は §4 の派生表（健康診断の材料）に**並べるだけ**で、相乗の計算には使わない。
// 帳簿は `PairAcc`（このファイルの末尾）。標準誤差は相乗を4群の平均の線形結合として書き、群ごとの分散から合成する。
//
//     dotnet run --project BattleSim -c Release 0 pairs phase0   # 紙の計算（**戦闘0回**。抽選だけ回して組ごとの在席標本数を数える）
//     dotnet run --project BattleSim -c Release 0 pairs          # 主表 A/B 両帯（表A〜E・§2-3・派生表・Q1〜Q5・P1〜P6）
//     dotnet run --project BattleSim -c Release 0 pairs check    # Q6: `compare` 305 セルの突き合わせ
public static void Run(string[] args, int stageIndex)
{
    string prArg = args.Length > 2 ? args[2] : "";
    var prSw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> prStages = EnemyCatalog.Stages;
    int prW = prStages.Count;
    var prRoster = UnitCatalog.All.ToArray();
    int prRN = prRoster.Length;                       // 51
    int prNK = UnitTally.CarryKeys.Length;            // 11
    UnitDef prOno = UnitCatalog.Ono;                  // 第79期の候補駒。`All` には無い。抽選をローカルに組んで測る（P6）
    var prRoster52 = prRoster.Concat(new[] { prOno }).ToArray();
    int prOnoIx = prRN;                               // 52 版の添字（51）

    // ---- ドラフト台は第76〜79期と同一（定数も抽選も規則配置も1文字も変えていない）------------------
    const int PrOfferSeed = 2_000_000;
    const int PrN = 11000, PrM = 8;
    const int PrStrong = 7, PrWeakPct = 60;
    const int PrMinPair = 100;                        // 在席標本がこれ未満の組は「測れなかった」（指示書 §1-2）
    const int PrTop = 30;                             // 表A の上位・下位の行数
    int[] prBands = { 0, 200 };
    string[] prBandName = { "A", "B" };
    const int PrB = 2;

    // 第72期 `D4Slope100`（第71期 表E・弱い波・規則 Pw・A 帯の (1枚−0枚) と (2枚−1枚) の平均・1/100 単位）の写し。
    // 並びは `UnitTally.CarryKeys`。**器具の検算（表C・Q4）にだけ使う。** 既存モードは触らないので写しを持つ。
    int[] PrSlope100 = { -90, -650, 190, 653, -1256, -330, -796, -1557, -759, 1936, 981 };

    var prIdx = new Dictionary<string, int>();
    for (int u = 0; u < prRN; u++) prIdx[prRoster[u].Id] = u;
    prIdx[prOno.Id] = prOnoIx;
    string[] prName = prRoster52.Select(d => d.Name).ToArray();

    var prKeyOf = prRoster52.Select(TraitKeyMap.KeysOf).ToArray();
    var prHookN = prRoster52.Select(d => TraitHookMap.HooksOf(d).Length).ToArray();
    var prEntN = prRoster52.Select(d => TraitEntryMap.EntriesOf(d, withFoe: false).Length).ToArray();
    var prRead = prRoster52.Select(d => d.Traits.SelectMany(t => TraitEntryMap.Reads.TryGetValue(t, out var r) ? r : Array.Empty<(int Key, TraitEntryMap.Where W)>()).Distinct().ToArray()).ToArray();
    var prSup = prRoster52.Select(d => d.Traits.SelectMany(t => TraitEntryMap.Supplies.TryGetValue(t, out var sq) ? sq : Array.Empty<(int Key, TraitEntryMap.Where W)>()).Distinct().ToArray()).ToArray();

    // A の供給 s が B の読み r を満たすか（場所まで見る。A の Self 供給は B から見ると「味方に載っている」）。
    bool PrFeeds((int Key, TraitEntryMap.Where W) su, (int Key, TraitEntryMap.Where W) r)
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
    bool PrSupplies(int a, int b) => prSup[a].Any(su => prRead[b].Any(r => PrFeeds(su, r)));
    // 組の分類（表D）。優先順位は 供給→読み ＞ 読み→読み ＞ 供給→供給 ＞ キーだけ共有 ＞ 共有無し。
    string[] prClassName = { "供給→読み", "読み→読み", "供給→供給", "キーだけ共有", "共有無し" };
    int PrClass(int a, int b)
    {
        if (PrSupplies(a, b) || PrSupplies(b, a)) return 0;
        if (prRead[a].Select(r => r.Key).Intersect(prRead[b].Select(r => r.Key)).Any()) return 1;
        if (prSup[a].Select(su => su.Key).Intersect(prSup[b].Select(su => su.Key)).Any()) return 2;
        if (prKeyOf[a].Intersect(prKeyOf[b]).Any()) return 3;
        return 4;
    }

    var prWeakCache = new Dictionary<string, UnitDef>();
    UnitDef PrWeakOf(UnitDef d)
    {
        if (prWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * PrWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        prWeakCache[d.Id] = w;
        return w;
    }
    var prWeak = prStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = PrWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();
    UnitDef PrPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    // 規則 P（第70〜79期の写し）
    UnitDef[] PrTeam(UnitDef[] roster, int i)
    {
        int rn = roster.Length;
        var rng = new Random(PrOfferSeed + i);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        int remain = rn, strong = 0;
        var picked = new UnitDef[5];
        for (int r = 0; r < 5; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = roster[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= PrStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(roster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    // 規則配置 H（第69期以来の写し）
    int[] PrSeats(UnitDef[] u)
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
    Formation PrForm(UnitDef[] u, int[] seats, int plainIdx)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = k == plainIdx ? PrPlain(u[k]) : u[k];
        return f;
    }
    double PrRate(Formation f, int band)
    {
        double sum = 0;
        for (int wi = 1; wi < prW; wi++)
        {
            int wins = 0;
            for (int seed = band; seed < band + PrM; seed++)
                if (BattleEngine.Run(f, prWeak[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
            sum += wins * 100.0 / PrM;
        }
        return sum / (prW - 1);
    }
    string PrP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    string PrP1(double x) => double.IsNaN(x) ? "—" : (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    double PrSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    string PrPair(int a, int b) => $"{prName[a]} × {prName[b]}";
    long PrC(int n, int k) { long r = 1; for (int i = 1; i <= k; i++) r = r * (n - k + i) / i; return r; }

    var prAllRows = CompareBuilds();
    var prBalance = new Dictionary<string, double[]>();
    if (File.Exists("docs/balance.md"))
        foreach (string line in File.ReadAllLines("docs/balance.md"))
        {
            if (!line.StartsWith("| ")) continue;
            var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
            if (cells.Length != prW + 1 || !cells[1].EndsWith("%")) continue;
            var v = new double[prW];
            bool ok = true;
            for (int w = 0; w < prW; w++)
                if (!double.TryParse(cells[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
            if (ok) prBalance[cells[0]] = v;
        }

    // ---- 抽選（**戦闘0回**）。標本ごとの在席5体と、組ごとの在席標本数はここで確定する ---------------
    var prTeam51 = new UnitDef[PrN][];
    var prTeam52 = new UnitDef[PrN][];
    var prIx51 = new int[PrN][];
    var prIx52 = new int[PrN][];
    Parallel.For(0, PrN, i =>
    {
        prTeam51[i] = PrTeam(prRoster, i);
        prTeam52[i] = PrTeam(prRoster52, i);
        prIx51[i] = prTeam51[i].Select(d => prIdx[d.Id]).ToArray();
        prIx52[i] = prTeam52[i].Select(d => prIdx[d.Id]).ToArray();
    });
    var prCo = new int[prRN, prRN];
    var prIn = new int[prRN];
    for (int i = 0; i < PrN; i++)
    {
        int[] t = prIx51[i];
        for (int a = 0; a < 5; a++)
        {
            prIn[t[a]]++;
            for (int b = a + 1; b < 5; b++) { prCo[t[a], t[b]]++; prCo[t[b], t[a]]++; }
        }
    }
    var prCo52 = new int[prRN + 1, prRN + 1];
    var prIn52 = new int[prRN + 1];
    for (int i = 0; i < PrN; i++)
    {
        int[] t = prIx52[i];
        for (int a = 0; a < 5; a++)
        {
            prIn52[t[a]]++;
            for (int b = a + 1; b < 5; b++) { prCo52[t[a], t[b]]++; prCo52[t[b], t[a]]++; }
        }
    }
    int prPairs = prRN * (prRN - 1) / 2;
    var prAllPairs = new List<(int A, int B)>();
    for (int a = 0; a < prRN; a++) for (int b = a + 1; b < prRN; b++) prAllPairs.Add((a, b));
    var prMeasurable = prAllPairs.Where(p => prCo[p.A, p.B] >= PrMinPair).ToList();
    int PrCountAtLeast(int line) => prAllPairs.Count(p => prCo[p.A, p.B] >= line);

    // 理想61行（既存波・seed 200・`docs/balance.md` の値をそのまま読む＝**戦闘0回**）
    var prIdealTeam = new List<int[]>();
    var prIdealY = new List<double>();
    var prIdealName = new List<string>();
    foreach (var row in prAllRows)
    {
        if (!prBalance.TryGetValue(row.Name, out double[]? cells)) continue;
        var ids = row.F.Occupied().Select(o => o.Def.Id).Where(prIdx.ContainsKey).Select(id => prIdx[id]).Distinct().ToArray();
        prIdealTeam.Add(ids);
        prIdealY.Add(cells.Skip(1).Average());
        prIdealName.Add(row.Name);
    }

    // =====================================================================================
    // phase0: 紙の計算（**戦闘0回**）
    // =====================================================================================
    if (prArg == "phase0")
    {
        Console.WriteLine("# 第80期 Phase 0 —— 組み合わせの表（紙の計算）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `dotnet run --project BattleSim -c Release 0 pairs phase0`");
        Console.WriteLine("抽選（規則 P・seed 2,000,000 + i・11,000 標本）は決定的なので、**組ごとの在席標本数は測る前に確定する。**");
        Console.WriteLine();

        // ---- §1-1 ---------------------------------------------------------------------
        Console.WriteLine("## 1-1. 1組あたりの標本数");
        Console.WriteLine();
        long c51_5 = PrC(prRN, 5), c49_3 = PrC(prRN - 2, 3);
        double pPair = (double)c49_3 / c51_5;
        Console.WriteLine("| 量 | 値 | 指示書 | 判定 |");
        Console.WriteLine("|---|--:|--:|:-:|");
        Console.WriteLine($"| 2枚が同時に在席する確率（超幾何・無作為5枚） | C(49,3)/C(51,5) = {c49_3:N0} / {c51_5:N0} = **{100 * pPair:F3}%** | 約 1.96% | {(Math.Abs(100 * pPair - 1.96) < 0.1 ? "○" : "**×**")} |");
        Console.WriteLine($"| 1組あたりの期待標本（無作為） | {PrN * pPair:F1} | 215 | {(Math.Abs(PrN * pPair - 215) < 10 ? "○" : "**×**")} |");
        Console.WriteLine($"| 1組あたりの平均標本（**規則 P の実測の抽選**） | {PrN * 10.0 / prPairs:F1}（= 11,000 × 10 組 ÷ 1,275） | — | — |");
        Console.WriteLine();
        Console.WriteLine("**指示書 §1-1 の「約 1.96%・期待 215 標本」は誤り。** C(49,3)/C(51,5) = 5·4 / (51·50) = 20 / 2,550 で、"
                          + "**1組あたり 86 標本が上限**（1標本が持つ組は 10 で、それを 1,275 組で分け合う恒等式）。");
        Console.WriteLine();
        // 標準誤差の紙の計算: 相乗 ≈ 0.8·m11 + …。y の標準偏差は第76期の台全体 ≈ 38pt（組の中 30.7pt = 80.5%）を使う。
        double sdGuess = 30.7 / 0.805;
        Console.WriteLine("| 在席標本 n | 相乗の標準誤差の目安（≈ 0.8 × 38 / √n） | 判定線（2 × SE） |");
        Console.WriteLine("|--:|--:|--:|");
        foreach (int n in new[] { 50, 86, 100, 200, 400 })
            Console.WriteLine($"| {n} | {0.8 * sdGuess / Math.Sqrt(n):F2}pt | {2 * 0.8 * sdGuess / Math.Sqrt(n):F2}pt |");
        Console.WriteLine();
        Console.WriteLine($"y の標準偏差は第76期の台全体の値（組の中 30.7pt ＝ 台全体の 80.5% → **{sdGuess:F1}pt**）を置いた。"
                          + "相乗の m11 の係数は 1 − n11/nA − n11/nB ≈ 0.8（在席率 9.6% の駒どうし）。");
        Console.WriteLine("**Q1 の線（|相乗| > 2SE の組が 50 以上）は、雑音だけでも測れる組の 4.6% が越える**"
                          + "——測れる組が 1,000 あれば 46 組は雑音で出る。**Q1 は雑音の期待値と並べて読み、実体の判定は Q2（両帯の再現）に置く。**");
        Console.WriteLine();

        // ---- §1-2 ---------------------------------------------------------------------
        Console.WriteLine("## 1-2. 標本数が足りない組（**測る前に確定**）");
        Console.WriteLine();
        Console.WriteLine("| 線 | 在席標本がその線以上の組 | 割合 | 除外される組 |");
        Console.WriteLine("|--:|--:|--:|--:|");
        foreach (int line in new[] { 30, 50, 100, 200 })
            Console.WriteLine($"| {line}{(line == PrMinPair ? "（**採用**）" : "")} | {PrCountAtLeast(line)} / {prPairs} | {100.0 * PrCountAtLeast(line) / prPairs:F1}% | {prPairs - PrCountAtLeast(line)} |");
        Console.WriteLine();
        var coList = prAllPairs.Select(p => prCo[p.A, p.B]).OrderBy(x => x).ToArray();
        Console.WriteLine($"組ごとの在席標本: 最小 {coList[0]} / 中央値 {coList[coList.Length / 2]} / 最大 {coList[^1]} / 平均 {coList.Average():F1}。"
                          + $"**0 標本の組 {coList.Count(x => x == 0)}**。");
        Console.WriteLine();
        Console.WriteLine("駒ごとの在席標本（少ない順 10 体・多い順 5 体）:");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席 | 測れる組の数（100 以上） |");
        Console.WriteLine("|---|--:|--:|");
        foreach (int u in Enumerable.Range(0, prRN).OrderBy(u => prIn[u]).Take(10)
                                    .Concat(Enumerable.Range(0, prRN).OrderByDescending(u => prIn[u]).Take(5)))
            Console.WriteLine($"| {prName[u]} | {prIn[u]:N0} | {Enumerable.Range(0, prRN).Count(v => v != u && prCo[u, v] >= PrMinPair)} |");
        Console.WriteLine();
        Console.WriteLine("**規則 P（攻撃力 → HP の2段）は攻2〜3・HP の低い駒を原理的に引かない**（第70期）ので、"
                          + "在席の偏りがそのまま組の偏りになる。**測れない組は「弱い駒を含む組」に集中する**（表E で駒ごとに数える）。");
        Console.WriteLine();

        // ---- §1-3 ---------------------------------------------------------------------
        Console.WriteLine("## 1-3. 第76〜79期の標本をそのまま使えるか");
        Console.WriteLine();
        Console.WriteLine("**使えない（保存されていない）。** `body` / `traits` / `lastslot` は標本を毎回再生成しており、ファイルには落としていない。"
                          + "**同じ設定で回し直す**（seed 2,000,000 + i の抽選・規則 P・規則配置 H・弱い波 60%・seed 0..7 / 200..207）。"
                          + "再現の確認は主表の冒頭（入口 0 / ≥ 1 の在席時勝率が第78期の 47.70 / 43.12 と一致すること）。");
        Console.WriteLine();

        // ---- §1-4 ---------------------------------------------------------------------
        Console.WriteLine("## 1-4. 第72期の傾き（キー単位）と、この期の相乗（駒の組単位）の関係");
        Console.WriteLine();
        Console.WriteLine("第72期の傾きは「そのキーを持つ駒の枚数を 0 → 1 → 2 と増やしたときの平均勝率の差」で、"
                          + "**(2枚 − 1枚) − (1枚 − 0枚)** がキー単位の「和より大きいか」に当たる。この期の相乗は駒の組単位なので、"
                          + "**同じキーを共有する2枚の組の相乗を平均すれば、そのキーの (2枚−1枚) − (1枚−0枚) に近づくはず**。");
        Console.WriteLine("ただし第72期の定数（`D4Slope100`）は **(1枚−0枚) と (2枚−1枚) の平均**なので、Q4 の突き合わせは"
                          + "**符号だけ**で行う（指示書 §3 Q4 のとおり）。第71期 表E の2つの数から差も併記する。");
        Console.WriteLine();
        double[][] src71 = {
            new[] { 0.90, -2.69 }, new[] { -8.03, -4.96 }, new[] { -4.82, 8.62 }, new[] { 2.80, 10.26 },
            new[] { -9.38, -15.73 }, new[] { -7.04, 0.45 }, new[] { -3.31, -12.60 }, new[] { -15.84, -15.30 },
            new[] { -6.79, -8.38 }, new[] { 20.95, 17.76 }, new[] { 9.11, 10.51 },
        };
        Console.WriteLine("| キー | 保持駒 | 同キーの組（測れる / 全部） | 第72期の傾き（平均） | (2枚−1枚) − (1枚−0枚) |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int k = 0; k < prNK; k++)
        {
            var hold = Enumerable.Range(0, prRN).Where(u => prKeyOf[u].Contains(k)).ToArray();
            int all = hold.Length * (hold.Length - 1) / 2;
            int meas = prMeasurable.Count(p => prKeyOf[p.A].Contains(k) && prKeyOf[p.B].Contains(k));
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {hold.Length} | {meas} / {all} | {PrP2(PrSlope100[k] / 100.0)} | {PrP2(src71[k][1] - src71[k][0])} |");
        }
        Console.WriteLine();

        // ---- §1-5 / §1-6 ------------------------------------------------------------------
        Console.WriteLine("## 1-5 / 1-6. 床と天井・現在値");
        Console.WriteLine();
        Console.WriteLine("相乗は **0% を含む版**（主）と **0% の標本を除いた版**（自己検査 (a')）の両方で出す。"
                          + "第69期は 78.9% が 0% だったが Pw では改善済み（第76期）。実測は主表の冒頭に出る。");
        Console.WriteLine($"`docs/balance.md` の現在値は **{prAllRows.Length} 行 × {prW} 波 = {prAllRows.Length * prW} セル**"
                          + $"（指示書の 305: {(prAllRows.Length * prW == 305 ? "○" : "**×**")}・読めた行 {prBalance.Count}）。");
        Console.WriteLine();

        // ---- P6 --------------------------------------------------------------------------
        Console.WriteLine("## P6 —— 首刈りのオノは `All` に入れずに測れるか");
        Console.WriteLine();
        Console.WriteLine($"第79期と同じく 52 体のロスターで抽選をローカルに組む（`UnitCatalog.Ono`・`All` は {UnitCatalog.All.Count} のまま）。"
                          + $"オノの在席標本は **{prIn52[prOnoIx]:N0} / {PrN:N0}**（第79期の 846: {(prIn52[prOnoIx] == 846 ? "○" : "**×**")}）。");
        var onoCo = Enumerable.Range(0, prRN).Select(v => prCo52[prOnoIx, v]).OrderByDescending(x => x).ToArray();
        Console.WriteLine($"オノと各駒の在席標本: 最大 {onoCo[0]} / 中央値 {onoCo[onoCo.Length / 2]} / 最小 {onoCo[^1]}。"
                          + $"**{PrMinPair} 以上の組は {onoCo.Count(x => x >= PrMinPair)} / {prRN}**。");
        Console.WriteLine(onoCo.Count(x => x >= PrMinPair) == 0
            ? "**→ 線 100 ではオノの組は1つも測れない**（在席 846 × 4 相方 ÷ 51 ≈ 66 が平均）。主表では線を 30 に緩めた**参考値**として出し、Q5 の判定には使わない。"
            : "→ 測れる組がある。");
        Console.WriteLine();

        Console.WriteLine("## 予測（**測る前に書いた**・指示書 §1）");
        Console.WriteLine();
        Console.WriteLine("| # | 量 | 予測 |");
        Console.WriteLine("|--:|---|---|");
        Console.WriteLine("| P1 | 相乗が正の組の割合 | **半分未満**。第72期の傾きは 11 キー中 2〜3 本しか正でなかった |");
        Console.WriteLine("| P2 | 上位の組の顔ぶれ | **書き手 × 読み手**の組（ボルグ×ホタ、ゾト×ホタ、グザ×ヴィオ、ヒサ×ザン、クグ×シガ） |");
        Console.WriteLine("| P3 | 下位の組 | **傷の5枚どうし**と**消費型どうし** |");
        Console.WriteLine("| P4 | 相乗の大きさ | **単独の帰属より小さい**（±1〜3pt） |");
        Console.WriteLine("| P5 | 器具の検算 | 同キーの組の平均相乗が**第72期の傾きと符号一致** |");
        Console.WriteLine("| P6 | 第79期の駒 | オノと既存51体の相乗が**全部 0 付近** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {prSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check: `compare` 305 セルの突き合わせ（Q6）
    // =====================================================================================
    if (prArg == "check")
    {
        Console.WriteLine("# 第80期 —— 受け入れ基準（Q6）: `compare` 305 セルの突き合わせ");
        Console.WriteLine();
        int mism = 0, cellsN = 0, missing = 0;
        var lines = new List<string>();
        Parallel.For(0, prAllRows.Length, i =>
        {
            var v = new double[prW];
            for (int w = 0; w < prW; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(prAllRows[i].F, prStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                v[w] = wins * 100.0 / 200;
            }
            lock (lines)
            {
                if (!prBalance.TryGetValue(prAllRows[i].Name, out double[]? doc)) { missing++; return; }
                for (int w = 0; w < prW; w++)
                {
                    cellsN++;
                    if (Math.Abs(doc[w] - v[w]) > 0.05) { mism++; lines.Add($"| {prAllRows[i].Name} | 第{w + 1}波 | {doc[w]:F1} | {v[w]:F1} |"); }
                }
            }
        });
        Console.WriteLine($"`CompareBuilds()` {prAllRows.Length} 行 × {prW} 波 × seed 0..199 を回し直して `docs/balance.md` と突き合わせた: "
                          + $"**{cellsN} セル・ずれ {mism} 件**（`docs/balance.md` に無い行 {missing}）。");
        if (lines.Count > 0)
        {
            Console.WriteLine(); Console.WriteLine("| 行 | 波 | docs | 今 |"); Console.WriteLine("|---|---|--:|--:|");
            foreach (string l in lines) Console.WriteLine(l);
        }
        Console.WriteLine();
        Console.WriteLine($"`UnitCatalog.All` は {UnitCatalog.All.Count} 体（オノを含まない: {(UnitCatalog.All.All(d => d.Id != prOno.Id) ? "○" : "**×**")}）。"
                          + " この期は engine も `Traits.cs` も駒も波も触っていないので、ずれは 0 件でなければならない。");
        Console.WriteLine();
        Console.WriteLine($"所要 {prSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }
    if (prArg != "")
    {
        Console.WriteLine("pairs: 引数は phase0 / （無し）/ check。");
        return;
    }

    // =====================================================================================
    // 主表: 両 seed 帯を1回の実行で回す（Q2 が両帯の突き合わせなので分けない）
    // =====================================================================================
    // 版 0 = 現行 / 版 1..5 = k 枚目を素体に（帰属は §4 の派生表にだけ使う）
    var prY = new double[PrB][];
    var prV = new double[PrB][][];
    var prY52 = new double[PrB][];
    for (int bd = 0; bd < PrB; bd++)
    {
        int band = prBands[bd];
        var y = new double[PrN];
        var v = new double[PrN][];
        var y52 = new double[PrN];
        int done = 0;
        Console.Error.Write($"{prBandName[bd]}帯: ");
        Parallel.For(0, PrN, i =>
        {
            UnitDef[] team = prTeam51[i];
            int[] seats = PrSeats(team);
            var w = new double[6];
            for (int ver = 0; ver < 6; ver++) w[ver] = PrRate(PrForm(team, seats, ver - 1), band);
            y[i] = w[0]; v[i] = w;
            UnitDef[] team52 = prTeam52[i];
            y52[i] = PrRate(PrForm(team52, PrSeats(team52), -1), band);
            int c = Interlocked.Increment(ref done);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        prY[bd] = y; prV[bd] = v; prY52[bd] = y52;
    }

    // ---- 帳簿 -------------------------------------------------------------------------------
    var prAcc = new PairAcc[PrB];      // 0% を含む
    var prAccPos = new PairAcc[PrB];   // 0% を除く
    var prAcc52 = new PairAcc[PrB];
    var prCon = new double[PrB][];     // 帰属（在席時平均）
    var prWinIn = new double[PrB][];
    for (int bd = 0; bd < PrB; bd++)
    {
        prAcc[bd] = new PairAcc(prRN); prAccPos[bd] = new PairAcc(prRN); prAcc52[bd] = new PairAcc(prRN + 1);
        var sum = new double[prRN]; var sumW = new double[prRN]; var cnt = new int[prRN];
        for (int i = 0; i < PrN; i++)
        {
            prAcc[bd].Add(prIx51[i], prY[bd][i]);
            if (prY[bd][i] > 0.0) prAccPos[bd].Add(prIx51[i], prY[bd][i]);
            prAcc52[bd].Add(prIx52[i], prY52[bd][i]);
            for (int k = 0; k < 5; k++)
            {
                int u = prIx51[i][k];
                sum[u] += prV[bd][i][0] - prV[bd][i][k + 1]; sumW[u] += prV[bd][i][0]; cnt[u]++;
            }
        }
        prCon[bd] = new double[prRN]; prWinIn[bd] = new double[prRN];
        for (int u = 0; u < prRN; u++)
        {
            prCon[bd][u] = cnt[u] == 0 ? double.NaN : sum[u] / cnt[u];
            prWinIn[bd][u] = cnt[u] == 0 ? double.NaN : sumW[u] / cnt[u];
        }
    }
    var prSt = new PairAcc.Stat[PrB][,];
    var prStPos = new PairAcc.Stat[PrB][,];
    for (int bd = 0; bd < PrB; bd++)
    {
        prSt[bd] = new PairAcc.Stat[prRN, prRN]; prStPos[bd] = new PairAcc.Stat[prRN, prRN];
        foreach ((int a, int b) in prAllPairs)
        {
            prSt[bd][a, b] = prSt[bd][b, a] = prAcc[bd].Of(a, b);
            prStPos[bd][a, b] = prStPos[bd][b, a] = prAccPos[bd].Of(a, b);
        }
    }
    bool PrSig(PairAcc.Stat st) => !double.IsNaN(st.Syn) && st.Se > 0 && Math.Abs(st.Syn) > 2 * st.Se;
    // 「正の相乗」（表B・Q5・派生表）: A 帯で 相乗 > 2SE、かつ B 帯でも 相乗 > 0。**測る前に固定。**
    bool PrPos(int a, int b) => prSt[0][a, b].Syn > 2 * prSt[0][a, b].Se && prSt[1][a, b].Syn > 0;
    bool PrNeg(int a, int b) => prSt[0][a, b].Syn < -2 * prSt[0][a, b].Se && prSt[1][a, b].Syn < 0;

    Console.WriteLine("# 第80期 —— 組み合わせの表（どの2枚が和より大きいか）・A/B 両帯");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 pairs` の出力。**`docs/` には置かない。**");
    Console.WriteLine();
    Console.WriteLine("**器具は在席差**（指示書 §0-2）——帰属（素体差し替え）ではない。派生表の「単独の帰属」だけが素体差し替えで、相乗の計算には使っていない。");
    Console.WriteLine();
    Console.WriteLine($"ドラフト台: 標本 **{PrN:N0}** × 版 6（現行 ＋ 5枚を1枚ずつ素体）× {prW - 1} 波 × seed **{PrM}** 本 × 2 帯"
                      + $"（A: seed 0..7 / B: 200..207・弱い波 {PrWeakPct}%・規則 P・規則配置 H）。"
                      + $"オノ用の 52 体版は現行だけ × 2 帯。");
    Console.WriteLine();
    Console.WriteLine("| 帯 | 標本 | 平均勝率 | 0% の標本 | 100% の標本 | 標準偏差 | 52 体版の平均 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    for (int bd = 0; bd < PrB; bd++)
        Console.WriteLine($"| {prBandName[bd]} | {PrN:N0} | {prY[bd].Average():F2}% "
                          + $"| {prY[bd].Count(x => x <= 0.0):N0} ({100.0 * prY[bd].Count(x => x <= 0.0) / PrN:F1}%) "
                          + $"| {prY[bd].Count(x => x >= 100.0):N0} | {PrSd(prY[bd]):F2} | {prY52[bd].Average():F2}% |");
    Console.WriteLine();

    // ---- §0 再現の確認 -------------------------------------------------------------------
    Console.WriteLine("## §0 —— 再現の確認（**ここが合わなければ以降は読まない**・指示書 §2-1）");
    Console.WriteLine();
    {
        var zero = Enumerable.Range(0, prRN).Where(u => prEntN[u] == 0).ToArray();
        var have = Enumerable.Range(0, prRN).Where(u => prEntN[u] >= 1).ToArray();
        double z0 = zero.Where(u => !double.IsNaN(prWinIn[0][u])).Average(u => prWinIn[0][u]);
        double h0 = have.Where(u => !double.IsNaN(prWinIn[0][u])).Average(u => prWinIn[0][u]);
        double zc = zero.Where(u => !double.IsNaN(prCon[0][u])).Average(u => prCon[0][u]);
        double hc = have.Where(u => !double.IsNaN(prCon[0][u])).Average(u => prCon[0][u]);
        double ono0 = prAcc52[0].MeanIn(prOnoIx);
        Console.WriteLine("| 量 | 今（A 帯） | 第78/79期 | 判定 |");
        Console.WriteLine("|---|--:|--:|:-:|");
        Console.WriteLine($"| 入口 0（{zero.Length} 体）の在席時勝率 | {z0:F2}% | 47.70% | {(Math.Abs(z0 - 47.70) < 0.01 ? "○" : "**×**")} |");
        Console.WriteLine($"| 入口 ≥ 1（{have.Length} 体）の在席時勝率 | {h0:F2}% | 43.12% | {(Math.Abs(h0 - 43.12) < 0.01 ? "○" : "**×**")} |");
        Console.WriteLine($"| 入口 0 / ≥ 1 の帰属の平均 | {PrP2(zc)} / {PrP2(hc)} | +3.43 / +1.32 | {(Math.Abs(zc - 3.43) < 0.01 && Math.Abs(hc - 1.32) < 0.01 ? "○" : "**×**")} |");
        Console.WriteLine($"| オノの在席標本 / 在席時勝率（52 体版） | {prAcc52[0].NA[prOnoIx]:N0} / {ono0:F2}% | 846 / 42.88% | {(prAcc52[0].NA[prOnoIx] == 846 && Math.Abs(ono0 - 42.88) < 0.01 ? "○" : "**×**")} |");
        Console.WriteLine();
    }

    // ---- §1 測れる組 -----------------------------------------------------------------------
    Console.WriteLine("## §1 —— 測れる組（在席標本 ≥ " + PrMinPair + "）");
    Console.WriteLine();
    Console.WriteLine($"1,275 組のうち **{prMeasurable.Count} 組**（{100.0 * prMeasurable.Count / prPairs:F1}%）。"
                      + $"除外 {prPairs - prMeasurable.Count} 組（表E）。**Phase 0 で確定した数と同じ**（抽選は決定的）。");
    Console.WriteLine();
    var prSeList = prMeasurable.Select(p => prSt[0][p.A, p.B].Se).OrderBy(x => x).ToArray();
    Console.WriteLine($"測れる組の標準誤差（A 帯）: 最小 {prSeList[0]:F2} / 中央値 {prSeList[prSeList.Length / 2]:F2} / 最大 {prSeList[^1]:F2}pt。"
                      + $"相乗の絶対値の中央値 {prMeasurable.Select(p => Math.Abs(prSt[0][p.A, p.B].Syn)).OrderBy(x => x).ToArray()[prMeasurable.Count / 2]:F2}pt。");
    Console.WriteLine();

    // ---- 表A --------------------------------------------------------------------------------
    var prOrdA = prMeasurable.OrderByDescending(p => prSt[0][p.A, p.B].Syn).ThenBy(p => p.A).ThenBy(p => p.B).ToList();
    var prOrdB = prMeasurable.OrderByDescending(p => prSt[1][p.A, p.B].Syn).ThenBy(p => p.A).ThenBy(p => p.B).ToList();
    void PrRowA((int A, int B) p, int rank)
    {
        var a = prSt[0][p.A, p.B]; var b = prSt[1][p.A, p.B]; var ap = prStPos[0][p.A, p.B];
        Console.WriteLine($"| {rank} | {PrPair(p.A, p.B)} | {a.N11} | {PrP1(a.SoloA)} / {PrP1(b.SoloA)} | {PrP1(a.SoloB)} / {PrP1(b.SoloB)} "
                          + $"| {PrP1(a.Pair)} / {PrP1(b.Pair)} | **{PrP2(a.Syn)}** | {PrP2(b.Syn)} | {a.Se:F2} "
                          + $"| {(PrSig(a) ? "○" : "—")}{(PrSig(b) ? "○" : "—")} | {PrP2(ap.Syn)} | {prClassName[PrClass(p.A, p.B)]} |");
    }
    Console.WriteLine($"## 表A —— 相乗の上位 {PrTop} 組・下位 {PrTop} 組（A 帯の順・B 帯を併記）");
    Console.WriteLine();
    Console.WriteLine("`有意` は |相乗| > 2SE（A 帯 / B 帯）。`0%除く` は 0% の標本を落とした版の A 帯の相乗（自己検査 (a')）。");
    Console.WriteLine();
    Console.WriteLine("### A-1. 上位（和より大きい）");
    Console.WriteLine();
    Console.WriteLine("| 順位 | 組 | 在席 | 単独(A) A/B | 単独(B) A/B | 組 A/B | **相乗 A** | 相乗 B | SE | 有意 | 0%除く | 分類 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|--:|:-:|--:|---|");
    for (int r = 0; r < Math.Min(PrTop, prOrdA.Count); r++) PrRowA(prOrdA[r], r + 1);
    Console.WriteLine();
    Console.WriteLine("### A-2. 下位（互いの前提を壊している）");
    Console.WriteLine();
    Console.WriteLine("| 順位 | 組 | 在席 | 単独(A) A/B | 単独(B) A/B | 組 A/B | **相乗 A** | 相乗 B | SE | 有意 | 0%除く | 分類 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|--:|:-:|--:|---|");
    for (int r = 0; r < Math.Min(PrTop, prOrdA.Count); r++) PrRowA(prOrdA[prOrdA.Count - 1 - r], prOrdA.Count - r);
    Console.WriteLine();

    // ---- Q1 / Q2 ------------------------------------------------------------------------------
    Console.WriteLine("## Q1 / Q2 —— 相乗は検出できるか・再現するか");
    Console.WriteLine();
    int sigA = prMeasurable.Count(p => PrSig(prSt[0][p.A, p.B]));
    int sigB = prMeasurable.Count(p => PrSig(prSt[1][p.A, p.B]));
    int sigBoth = prMeasurable.Count(p => PrSig(prSt[0][p.A, p.B]) && PrSig(prSt[1][p.A, p.B]) && Math.Sign(prSt[0][p.A, p.B].Syn) == Math.Sign(prSt[1][p.A, p.B].Syn));
    int sigAB = prMeasurable.Count(p => PrSig(prSt[0][p.A, p.B]) && Math.Sign(prSt[0][p.A, p.B].Syn) == Math.Sign(prSt[1][p.A, p.B].Syn));
    int sigPosA = prMeasurable.Count(p => PrSig(prStPos[0][p.A, p.B]));
    int sigPosB = prMeasurable.Count(p => PrSig(prStPos[1][p.A, p.B]));
    double nullExp = 0.0455 * prMeasurable.Count;
    Console.WriteLine("| 版 | 測れる組 | |相乗| > 2SE（A） | （B） | A で有意かつ B で同符号 | 両帯で有意・同符号 | 雑音の期待値（4.55%） |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    Console.WriteLine($"| 0% を含む（主） | {prMeasurable.Count} | **{sigA}** | {sigB} | {sigAB} | {sigBoth} | {nullExp:F1} |");
    Console.WriteLine($"| 0% を除く | {prMeasurable.Count} | {sigPosA} | {sigPosB} | "
                      + $"{prMeasurable.Count(p => PrSig(prStPos[0][p.A, p.B]) && Math.Sign(prStPos[0][p.A, p.B].Syn) == Math.Sign(prStPos[1][p.A, p.B].Syn))} "
                      + $"| {prMeasurable.Count(p => PrSig(prStPos[0][p.A, p.B]) && PrSig(prStPos[1][p.A, p.B]) && Math.Sign(prStPos[0][p.A, p.B].Syn) == Math.Sign(prStPos[1][p.A, p.B].Syn))} | {nullExp:F1} |");
    Console.WriteLine();
    Console.WriteLine($"**Q1（A 帯で 50 組以上）: {sigA} 組 → {(sigA >= 50 ? "○" : "**×**")}**。"
                      + $"ただし雑音だけで {nullExp:F0} 組は越えるので、**Q1 単独では表の実在を言えない**（Phase 0 §1-1）。"
                      + $"雑音の期待値を引いた正味は {sigA - nullExp:F0} 組、両帯で有意・同符号は {sigBoth} 組（雑音の期待値 {0.0455 * 0.0455 * prMeasurable.Count / 2:F1}）。");
    Console.WriteLine();
    var topA = prOrdA.Take(PrTop).ToHashSet();
    var topB = prOrdB.Take(PrTop).ToHashSet();
    var botA = prOrdA.TakeLast(PrTop).ToHashSet();
    var botB = prOrdB.TakeLast(PrTop).ToHashSet();
    int topOv = topA.Count(topB.Contains), botOv = botA.Count(botB.Contains);
    Console.WriteLine("| 集合 | A 帯と B 帯の一致 | 2/3 の線 | 判定 |");
    Console.WriteLine("|---|--:|--:|:-:|");
    Console.WriteLine($"| 上位 {PrTop} 組 | **{topOv} / {PrTop}** | {PrTop * 2 / 3} | {(topOv * 3 >= PrTop * 2 ? "○" : "**×**")} |");
    Console.WriteLine($"| 下位 {PrTop} 組 | **{botOv} / {PrTop}** | {PrTop * 2 / 3} | {(botOv * 3 >= PrTop * 2 ? "○" : "**×**")} |");
    {
        var oA = prMeasurable.OrderByDescending(p => prStPos[0][p.A, p.B].Syn).ToList();
        var oB = prMeasurable.OrderByDescending(p => prStPos[1][p.A, p.B].Syn).ToList();
        int t2 = oA.Take(PrTop).Count(oB.Take(PrTop).ToHashSet().Contains), b2 = oA.TakeLast(PrTop).Count(oB.TakeLast(PrTop).ToHashSet().Contains);
        Console.WriteLine($"| 上位 {PrTop}（0% を除く版） | {t2} / {PrTop} | {PrTop * 2 / 3} | {(t2 * 3 >= PrTop * 2 ? "○" : "**×**")} |");
        Console.WriteLine($"| 下位 {PrTop}（0% を除く版） | {b2} / {PrTop} | {PrTop * 2 / 3} | {(b2 * 3 >= PrTop * 2 ? "○" : "**×**")} |");
    }
    bool q2 = topOv * 3 >= PrTop * 2 && botOv * 3 >= PrTop * 2;
    bool q2h = false; int q2hTop = 0, q2hBot = 0;
    {
        // **参考**（判定には使わない）: 指示書の期待 215 標本が実際は 86 だったので、線を 50 に緩めたときの同じ量を並べる（次期の「標本の増強」の材料）。
        var m50 = prAllPairs.Where(p => prCo[p.A, p.B] >= 50).ToList();
        var o50A = m50.OrderByDescending(p => prSt[0][p.A, p.B].Syn).ThenBy(p => p.A).ThenBy(p => p.B).ToList();
        var o50B = m50.OrderByDescending(p => prSt[1][p.A, p.B].Syn).ThenBy(p => p.A).ThenBy(p => p.B).ToList();
        int s50 = m50.Count(p => PrSig(prSt[0][p.A, p.B]));
        int sb50 = m50.Count(p => PrSig(prSt[0][p.A, p.B]) && PrSig(prSt[1][p.A, p.B]) && Math.Sign(prSt[0][p.A, p.B].Syn) == Math.Sign(prSt[1][p.A, p.B].Syn));
        int t50 = o50A.Take(PrTop).Count(o50B.Take(PrTop).ToHashSet().Contains), b50 = o50A.TakeLast(PrTop).Count(o50B.TakeLast(PrTop).ToHashSet().Contains);
        Console.WriteLine($"| （参考）線 50: 測れる組 {m50.Count} | |相乗| > 2SE（A）{s50}・雑音の期待値 {0.0455 * m50.Count:F0}・両帯で有意・同符号 {sb50} | 上位 {t50} / 下位 {b50} | — |");
    }
    Console.WriteLine();
    {
        var xs = prMeasurable.Select(p => prSt[0][p.A, p.B].Syn).ToArray();
        var ys = prMeasurable.Select(p => prSt[1][p.A, p.B].Syn).ToArray();
        double mx = xs.Average(), my = ys.Average(), sxx = 0, syy = 0, sxy = 0;
        for (int i = 0; i < xs.Length; i++) { sxx += (xs[i] - mx) * (xs[i] - mx); syy += (ys[i] - my) * (ys[i] - my); sxy += (xs[i] - mx) * (ys[i] - my); }
        double r = sxy / Math.Sqrt(sxx * syy);
        int sameSign = prMeasurable.Count(p => Math.Sign(prSt[0][p.A, p.B].Syn) == Math.Sign(prSt[1][p.A, p.B].Syn));
        Console.WriteLine($"**Q2: {(q2 ? "○" : "**×**")}**。測れる組全体の A 帯 × B 帯の相関 r = **{r:F3}**、符号一致 {sameSign} / {prMeasurable.Count}（{100.0 * sameSign / prMeasurable.Count:F1}%）。"
                          + $" 相乗の帯間の差の標準偏差 {PrSd(prMeasurable.Select(p => prSt[0][p.A, p.B].Syn - prSt[1][p.A, p.B].Syn).ToArray()):F2}pt"
                          + $"（SE の中央値 {prSeList[prSeList.Length / 2]:F2} × √2 = {prSeList[prSeList.Length / 2] * Math.Sqrt(2):F2} と比べる）。");
        Console.WriteLine();
        // **Q2 は抽選の揺れを検定していない**——A/B 帯は同じ 11,000 編成を別の戦闘 seed で回しているので、
        // 帯間の差は戦闘 seed の雑音だけで、SE（標本＝編成の揺れを含む）より必ず小さく出る。
        // 抽選の揺れは**標本の偶奇で二分した半割**（第13期 `bench` の作法）で見る。線は Q2 と同じ 2/3。
        var accE = new PairAcc(prRN); var accO = new PairAcc(prRN);
        for (int i = 0; i < PrN; i++) (i % 2 == 0 ? accE : accO).Add(prIx51[i], prY[0][i]);
        var oE = prMeasurable.OrderByDescending(p => accE.Of(p.A, p.B).Syn).ThenBy(p => p.A).ThenBy(p => p.B).ToList();
        var oO = prMeasurable.OrderByDescending(p => accO.Of(p.A, p.B).Syn).ThenBy(p => p.A).ThenBy(p => p.B).ToList();
        int tH = oE.Take(PrTop).Count(oO.Take(PrTop).ToHashSet().Contains), bH = oE.TakeLast(PrTop).Count(oO.TakeLast(PrTop).ToHashSet().Contains);
        var xe = prMeasurable.Select(p => accE.Of(p.A, p.B).Syn).ToArray();
        var xo = prMeasurable.Select(p => accO.Of(p.A, p.B).Syn).ToArray();
        double me = xe.Average(), mo = xo.Average(), see = 0, soo = 0, seo = 0;
        for (int i = 0; i < xe.Length; i++) { see += (xe[i] - me) * (xe[i] - me); soo += (xo[i] - mo) * (xo[i] - mo); seo += (xe[i] - me) * (xo[i] - mo); }
        int sameH = prMeasurable.Count(p => Math.Sign(accE.Of(p.A, p.B).Syn) == Math.Sign(accO.Of(p.A, p.B).Syn));
        int sigH = prMeasurable.Count(p => PrSig(accE.Of(p.A, p.B)) && PrSig(accO.Of(p.A, p.B)) && Math.Sign(accE.Of(p.A, p.B).Syn) == Math.Sign(accO.Of(p.A, p.B).Syn));
        int sigTopBoth = prOrdA.Take(PrTop).Count(p => accE.Of(p.A, p.B).Syn > 0 && accO.Of(p.A, p.B).Syn > 0);
        int sigBotBoth = prOrdA.TakeLast(PrTop).Count(p => accE.Of(p.A, p.B).Syn < 0 && accO.Of(p.A, p.B).Syn < 0);
        Console.WriteLine("### Q2' —— 半割（標本の偶奇で二分・A 帯・**抽選の揺れ**の検定）");
        Console.WriteLine();
        Console.WriteLine("**A/B 帯は同じ 11,000 編成を別の戦闘 seed で回している**ので、Q2 が検定しているのは戦闘 seed の雑音だけで、"
                          + "帯間の差（0.4pt）が SE（2.4pt）より一桁小さいのはそのため。**編成の抽選の揺れは半割で見る**（各半分は 5,500 標本＝組あたりの在席は半分）。");
        Console.WriteLine();
        Console.WriteLine("| 集合 | 偶数半 と 奇数半 の一致 | 2/3 の線 | 判定 |");
        Console.WriteLine("|---|--:|--:|:-:|");
        Console.WriteLine($"| 上位 {PrTop} 組 | **{tH} / {PrTop}** | {PrTop * 2 / 3} | {(tH * 3 >= PrTop * 2 ? "○" : "**×**")} |");
        Console.WriteLine($"| 下位 {PrTop} 組 | **{bH} / {PrTop}** | {PrTop * 2 / 3} | {(bH * 3 >= PrTop * 2 ? "○" : "**×**")} |");
        Console.WriteLine($"| 全体（A 帯）の上位 {PrTop} のうち両半分で正 | {sigTopBoth} / {PrTop} | — | — |");
        Console.WriteLine($"| 全体（A 帯）の下位 {PrTop} のうち両半分で負 | {sigBotBoth} / {PrTop} | — | — |");
        Console.WriteLine();
        Console.WriteLine($"半割の相関 r = **{(see * soo <= 0 ? double.NaN : seo / Math.Sqrt(see * soo)):F3}**（帯間 {r:F3}）、符号一致 {sameH} / {prMeasurable.Count}（{100.0 * sameH / prMeasurable.Count:F1}%）、"
                          + $"両半分で有意・同符号 {sigH} 組（半分ずつなので SE は √2 倍）。**上位・下位の顔ぶれは半割でも 2/3 を{(tH * 3 >= PrTop * 2 && bH * 3 >= PrTop * 2 ? "超える" : "**割る**")}。**");
        Console.WriteLine();
        q2h = tH * 3 >= PrTop * 2 && bH * 3 >= PrTop * 2; q2hTop = tH; q2hBot = bH;
        Console.WriteLine("**両帯で有意・同符号の組**（雑音では作りにくい。**表の実体はここ**）:");
        Console.WriteLine();
        Console.WriteLine("| 組 | 在席 | 相乗 A | 相乗 B | SE A | 分類 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        foreach (var p in prOrdA.Where(p => PrSig(prSt[0][p.A, p.B]) && PrSig(prSt[1][p.A, p.B]) && Math.Sign(prSt[0][p.A, p.B].Syn) == Math.Sign(prSt[1][p.A, p.B].Syn)))
            Console.WriteLine($"| {PrPair(p.A, p.B)} | {prSt[0][p.A, p.B].N11} | **{PrP2(prSt[0][p.A, p.B].Syn)}** | {PrP2(prSt[1][p.A, p.B].Syn)} | {prSt[0][p.A, p.B].Se:F2} | {prClassName[PrClass(p.A, p.B)]} |");
        Console.WriteLine();
    }

    // ---- 表B --------------------------------------------------------------------------------
    Console.WriteLine("## 表B —— 駒ごとの集計（誰が孤立していて、誰が起点か）");
    Console.WriteLine();
    Console.WriteLine("`正(有意)` は「A 帯で 相乗 > 2SE かつ B 帯でも 相乗 > 0」の相手の数（**測る前に固定した定義**・Q5 に使う）。"
                      + "`正(生)` は A 帯の相乗が正の相手の数（雑音を含む。参考）。`最良の相方` は A 帯の相乗が最大の相手。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 在席 | 測れる相手 | **正(有意)** | 負(有意) | 正(生) | 最良の相方 | 相乗 A/B | 最悪の相方 | 相乗 A/B | 相乗の合計(A) | 単独 A/B |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|---|--:|---|--:|--:|--:|");
    var prPartners = Enumerable.Range(0, prRN).Select(u => Enumerable.Range(0, prRN).Where(v => v != u && prCo[u, v] >= PrMinPair).ToArray()).ToArray();
    var prPosN = new int[prRN];
    var prBest = new int[prRN];
    for (int u = 0; u < prRN; u++)
    {
        prPosN[u] = prPartners[u].Count(v => PrPos(u, v));
        prBest[u] = prPartners[u].Length == 0 ? -1 : prPartners[u].OrderByDescending(v => prSt[0][u, v].Syn).First();
    }
    foreach (int u in Enumerable.Range(0, prRN).OrderByDescending(u => prPosN[u]).ThenByDescending(u => prPartners[u].Length == 0 ? double.NegativeInfinity : prPartners[u].Sum(v => prSt[0][u, v].Syn)))
    {
        var ps = prPartners[u];
        if (ps.Length == 0)
        {
            Console.WriteLine($"| {prName[u]} | {prIn[u]:N0} | 0 | — | — | — | — | — | — | — | — | {PrP1(prAcc[0].Solo(u))} / {PrP1(prAcc[1].Solo(u))} |");
            continue;
        }
        int best = prBest[u], worst = ps.OrderBy(v => prSt[0][u, v].Syn).First();
        Console.WriteLine($"| {prName[u]} | {prIn[u]:N0} | {ps.Length} | **{prPosN[u]}** | {ps.Count(v => PrNeg(u, v))} | {ps.Count(v => prSt[0][u, v].Syn > 0)} "
                          + $"| {prName[best]} | {PrP1(prSt[0][u, best].Syn)} / {PrP1(prSt[1][u, best].Syn)} "
                          + $"| {prName[worst]} | {PrP1(prSt[0][u, worst].Syn)} / {PrP1(prSt[1][u, worst].Syn)} "
                          + $"| {PrP1(ps.Sum(v => prSt[0][u, v].Syn))} | {PrP1(prAcc[0].Solo(u))} / {PrP1(prAcc[1].Solo(u))} |");
    }
    Console.WriteLine();

    // ---- 表C --------------------------------------------------------------------------------
    Console.WriteLine("## 表C —— キー単位との照合（**Q4**・器具の検算）");
    Console.WriteLine();
    Console.WriteLine("同じキーを共有する組（`TraitKeyMap.KeysOf` が両方に k を持つ・測れる組）の平均相乗と、第72期の傾き（`D4Slope100`）の符号。"
                      + "**0.5pt 未満の平均は 0 と読む**（第57期「|帰属| < 1.5 は 0」の組版・SE の中央値の半分）。");
    Console.WriteLine();
    // 第71期 表E の出どころ（(1枚−0枚), (2枚−1枚)）。第72期 `D4Src71` の写し。(2枚−1枚) − (1枚−0枚) がキー単位の「和より大きいか」。
    double[][] prSrc71 = {
        new[] { 0.90, -2.69 }, new[] { -8.03, -4.96 }, new[] { -4.82, 8.62 }, new[] { 2.80, 10.26 },
        new[] { -9.38, -15.73 }, new[] { -7.04, 0.45 }, new[] { -3.31, -12.60 }, new[] { -15.84, -15.30 },
        new[] { -6.79, -8.38 }, new[] { 20.95, 17.76 }, new[] { 9.11, 10.51 },
    };
    Console.WriteLine("| キー | 保持駒 | 測れる組 | 平均相乗 A | 平均相乗 B | 有意な組（正 / 負） | 第72期の傾き | 符号 | (2枚−1枚)−(1枚−0枚) | 符号 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|:-:|--:|:-:|");
    int q4Match = 0, q4Keys = 0, q4Match2 = 0;
    for (int k = 0; k < prNK; k++)
    {
        var ps = prMeasurable.Where(p => prKeyOf[p.A].Contains(k) && prKeyOf[p.B].Contains(k)).ToArray();
        int hold = Enumerable.Range(0, prRN).Count(u => prKeyOf[u].Contains(k));
        double slope = PrSlope100[k] / 100.0;
        if (ps.Length == 0)
        {
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {hold} | 0 | — | — | — | {PrP2(slope)} | —（測れる組が無い） | {PrP2(prSrc71[k][1] - prSrc71[k][0])} | — |");
            continue;
        }
        q4Keys++;
        double mA = ps.Average(p => prSt[0][p.A, p.B].Syn), mB = ps.Average(p => prSt[1][p.A, p.B].Syn);
        int sA = Math.Abs(mA) < 0.5 ? 0 : Math.Sign(mA);
        bool match = sA == Math.Sign(slope) && sA != 0;
        if (match) q4Match++;
        double d71 = prSrc71[k][1] - prSrc71[k][0];
        bool match2 = sA == Math.Sign(d71) && sA != 0;
        if (match2) q4Match2++;
        Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {hold} | {ps.Length} | **{PrP2(mA)}** | {PrP2(mB)} "
                          + $"| {ps.Count(p => PrPos(p.A, p.B))} / {ps.Count(p => PrNeg(p.A, p.B))} | {PrP2(slope)} | {(match ? "○" : "**×**")} "
                          + $"| {PrP2(d71)} | {(match2 ? "○" : "**×**")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**Q4: 符号一致 {q4Match} / {prNK}（測れる組があるキー {q4Keys}）→ {(q4Match >= 8 ? "○" : "**×**")}**（線は 8 / 11）。"
                      + $" (2枚−1枚)−(1枚−0枚) と比べると {q4Match2} / {prNK}。");
    Console.WriteLine("**但し書き**: 第72期の傾きは (1枚−0枚) と (2枚−1枚) の平均で「1枚目の値段」を含む。相乗は「2枚目が1枚目の分だけ余分に払うか」なので、"
                      + "厳密には (2枚−1枚) − (1枚−0枚) と比べるべき量（Phase 0 §1-4 に併記）。**両方の符号を見ること。**");
    Console.WriteLine();

    // ---- 表D --------------------------------------------------------------------------------
    Console.WriteLine("## 表D —— 分類との照合（**Q3**・`TraitEntryMap` の `Reads` / `Supplies`）");
    Console.WriteLine();
    Console.WriteLine("| 分類 | 組（全部） | 測れる組 | 平均相乗 A | 平均相乗 B | 正(有意) | 負(有意) | |相乗| の平均 A |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    var prClassMean = new double[prClassName.Length];
    for (int c = 0; c < prClassName.Length; c++)
    {
        int all = prAllPairs.Count(p => PrClass(p.A, p.B) == c);
        var ps = prMeasurable.Where(p => PrClass(p.A, p.B) == c).ToArray();
        prClassMean[c] = ps.Length == 0 ? double.NaN : ps.Average(p => prSt[0][p.A, p.B].Syn);
        Console.WriteLine($"| **{prClassName[c]}** | {all} | {ps.Length} | **{PrP2(prClassMean[c])}** "
                          + $"| {PrP2(ps.Length == 0 ? double.NaN : ps.Average(p => prSt[1][p.A, p.B].Syn))} "
                          + $"| {ps.Count(p => PrPos(p.A, p.B))} | {ps.Count(p => PrNeg(p.A, p.B))} "
                          + $"| {(ps.Length == 0 ? "—" : ps.Average(p => Math.Abs(prSt[0][p.A, p.B].Syn)).ToString("F2"))} |");
    }
    Console.WriteLine();
    bool q3 = !double.IsNaN(prClassMean[0]) && !double.IsNaN(prClassMean[1]) && prClassMean[0] > prClassMean[1];
    Console.WriteLine($"**Q3: （供給→読み）{PrP2(prClassMean[0])} 対（読み→読み）{PrP2(prClassMean[1])} → {(q3 ? "○" : "**×**（第78期の読みが組の側で否定される）")}**。");
    {
        // 床の非線形（自己検査 (a')）: 単独が大きい体（+15 以上）を含む組は、相方が台を床から引き上げるぶんだけ相乗が正に出やすい。
        var big = Enumerable.Range(0, prRN).Where(u => prAcc[0].Solo(u) >= 15.0).ToArray();
        int posAll = prMeasurable.Count(p => PrPos(p.A, p.B)), negAll = prMeasurable.Count(p => PrNeg(p.A, p.B));
        int posBig = prMeasurable.Count(p => PrPos(p.A, p.B) && (big.Contains(p.A) || big.Contains(p.B)));
        int negBig = prMeasurable.Count(p => PrNeg(p.A, p.B) && (big.Contains(p.A) || big.Contains(p.B)));
        int measBig = prMeasurable.Count(p => big.Contains(p.A) || big.Contains(p.B));
        int posBigPos = prMeasurable.Count(p => PrPos(p.A, p.B) && (big.Contains(p.A) || big.Contains(p.B)) && prStPos[0][p.A, p.B].Syn > 2 * prStPos[0][p.A, p.B].Se);
        int posOtherPos = prMeasurable.Count(p => PrPos(p.A, p.B) && !(big.Contains(p.A) || big.Contains(p.B)) && prStPos[0][p.A, p.B].Syn > 2 * prStPos[0][p.A, p.B].Se);
        Console.WriteLine();
        Console.WriteLine($"**床の非線形（自己検査 (a')）**: 単独 +15 以上の体は {big.Length} 体（{string.Join("・", big.Select(u => prName[u]))}）。"
                          + $"測れる組 {prMeasurable.Count} のうちその体を含む組は {measBig}、有意な正 {posAll} のうち {posBig}（{100.0 * posBig / Math.Max(1, posAll):F0}%）・有意な負 {negAll} のうち {negBig}。"
                          + $" 体を含む有意な正 {posBig} 組のうち **0% を除いた版でも 2SE を超えるのは {posBigPos} 組**（含まない側は {posAll - posBig} 組中 {posOtherPos}）"
                          + "——落ちた分は「相方が台を床から引き上げた」ぶんで、機構の噛み合いではない。");
    }
    Console.WriteLine("供給→読み の組の内訳（キーごと・測れる組）:");
    Console.WriteLine();
    Console.WriteLine("| キー | 測れる組 | 平均相乗 A | 平均相乗 B | 最大の組 | 相乗 A |");
    Console.WriteLine("|---|--:|--:|--:|---|--:|");
    for (int k = 0; k < prNK; k++)
    {
        var ps = prMeasurable.Where(p => PrClass(p.A, p.B) == 0
                                         && (prSup[p.A].Any(su => su.Key == k && prRead[p.B].Any(r => PrFeeds(su, r)))
                                             || prSup[p.B].Any(su => su.Key == k && prRead[p.A].Any(r => PrFeeds(su, r))))).ToArray();
        if (ps.Length == 0) continue;
        var best = ps.OrderByDescending(p => prSt[0][p.A, p.B].Syn).First();
        Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {ps.Length} | {PrP2(ps.Average(p => prSt[0][p.A, p.B].Syn))} | {PrP2(ps.Average(p => prSt[1][p.A, p.B].Syn))} "
                          + $"| {PrPair(best.A, best.B)} | {PrP2(prSt[0][best.A, best.B].Syn)} |");
    }
    Console.WriteLine();

    // ---- 表E --------------------------------------------------------------------------------
    Console.WriteLine("## 表E —— 測れなかった組（在席標本 < " + PrMinPair + "）");
    Console.WriteLine();
    int prExcl = prPairs - prMeasurable.Count;
    Console.WriteLine($"**{prExcl} / {prPairs} 組（{100.0 * prExcl / prPairs:F1}%）。** うち 0 標本 {prAllPairs.Count(p => prCo[p.A, p.B] == 0)} 組・50 未満 {prAllPairs.Count(p => prCo[p.A, p.B] < 50)} 組。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 在席 | 攻/HP | 測れない組 / 50 | 測れる組 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (int u in Enumerable.Range(0, prRN).OrderByDescending(u => prRN - 1 - prPartners[u].Length).ThenBy(u => prIn[u]).Take(15))
        Console.WriteLine($"| {prName[u]} | {prIn[u]:N0} | {prRoster[u].Attack}/{prRoster[u].MaxHp} | {prRN - 1 - prPartners[u].Length} | {prPartners[u].Length} |");
    Console.WriteLine();
    Console.WriteLine($"測れる組を1つも持たない駒: **{Enumerable.Range(0, prRN).Count(u => prPartners[u].Length == 0)} 体**"
                      + $"（{string.Join("・", Enumerable.Range(0, prRN).Where(u => prPartners[u].Length == 0).Select(u => prName[u]))}）。"
                      + $" 全 50 組が測れる駒: {Enumerable.Range(0, prRN).Count(u => prPartners[u].Length == prRN - 1)} 体。");
    Console.WriteLine("**偏りは規則 P の側**（攻撃力 → HP の2段しか見ないので攻2〜3・HP の低い駒は引かれない・第70期）——表の空白はロスターの性質ではなく抽選の性質。");
    Console.WriteLine();

    // ---- §2-3 理想台 -------------------------------------------------------------------------
    Console.WriteLine("## §2-3 —— 理想台との照合（**軽く**・`docs/balance.md` の第2〜5波平均をそのまま読む＝戦闘0回）");
    Console.WriteLine();
    {
        var accI = new PairAcc(prRN);
        for (int i = 0; i < prIdealTeam.Count; i++) accI.Add(prIdealTeam[i], prIdealY[i]);
        var both = prMeasurable.Where(p => accI.N11[p.A, p.B] >= 1).ToList();
        Console.WriteLine($"理想 {prIdealTeam.Count} 行のうち、同じ2枚を含む行がある**測れる組**は **{both.Count} 組**"
                          + $"（測れない組も含めれば {prAllPairs.Count(p => accI.N11[p.A, p.B] >= 1)} 組）。");
        if (both.Count < 10)
            Console.WriteLine("**10 組未満なので照合できない。**");
        else
        {
            var xs = both.Select(p => prSt[0][p.A, p.B].Syn).ToArray();
            var ys = both.Select(p => accI.Of(p.A, p.B).Syn).ToArray();
            double mx = xs.Average(), my = ys.Average(), sxx = 0, syy = 0, sxy = 0;
            for (int i = 0; i < xs.Length; i++) { sxx += (xs[i] - mx) * (xs[i] - mx); syy += (ys[i] - my) * (ys[i] - my); sxy += (xs[i] - mx) * (ys[i] - my); }
            int same = both.Count(p => Math.Sign(prSt[0][p.A, p.B].Syn) == Math.Sign(accI.Of(p.A, p.B).Syn));
            Console.WriteLine($"ドラフト台の相乗（A 帯）と理想台の相乗の相関 r = **{(sxx * syy <= 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy)):F3}**、符号一致 {same} / {both.Count}。"
                              + "**理想台の「単独」「組」は 61 行の在席差**で、行が 1〜3 しか無い組が大半（分母の箱・第72期）。**値ではなく符号だけを読むこと。**");
            Console.WriteLine();
            Console.WriteLine("| 組 | ドラフト 相乗 A/B | 理想 行数（両方在席） | 理想 単独(A) | 理想 単独(B) | 理想 組 | **理想 相乗** | 両方在席の行 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
            foreach (var p in both.OrderByDescending(p => prSt[0][p.A, p.B].Syn).Take(12).Concat(both.OrderBy(p => prSt[0][p.A, p.B].Syn).Take(12).Reverse()))
            {
                var si = accI.Of(p.A, p.B);
                var rows = Enumerable.Range(0, prIdealTeam.Count).Where(i => prIdealTeam[i].Contains(p.A) && prIdealTeam[i].Contains(p.B)).Select(i => prIdealName[i]);
                Console.WriteLine($"| {PrPair(p.A, p.B)} | {PrP1(prSt[0][p.A, p.B].Syn)} / {PrP1(prSt[1][p.A, p.B].Syn)} | {si.N11} | {PrP1(si.SoloA)} | {PrP1(si.SoloB)} | {PrP1(si.Pair)} | **{PrP1(si.Syn)}** | {string.Join("・", rows)} |");
            }
            Console.WriteLine();
        }
    }

    // ---- 派生表 -------------------------------------------------------------------------------
    Console.WriteLine("## §4 —— 派生表（**選ばない。並べるだけ**）");
    Console.WriteLine();
    Console.WriteLine("### 4-1. 健康診断の材料（51 体）");
    Console.WriteLine();
    Console.WriteLine("`単独の帰属` は素体差し替え（第69期の標準器具・在席時平均）。`相乗` は在席差。**別の器具なので足し引きしない。**");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 攻/HP/速 | 特性 | **単独の帰属 A/B** | 在席時勝率 | 単独(在席差) A | **最良の相方** | 相乗 A/B | 正(有意) | 入口 | 発火口 | キー |");
    Console.WriteLine("|---|---|---|--:|--:|--:|---|--:|--:|--:|--:|--:|");
    foreach (int u in Enumerable.Range(0, prRN).OrderByDescending(u => double.IsNaN(prCon[0][u]) ? double.NegativeInfinity : prCon[0][u]))
    {
        int best = prBest[u];
        Console.WriteLine($"| {prName[u]} | {prRoster[u].Attack}/{prRoster[u].MaxHp}/{prRoster[u].Speed} | {string.Join("・", prRoster[u].Traits)} "
                          + $"| **{PrP2(prCon[0][u])} / {PrP2(prCon[1][u])}** | {prWinIn[0][u]:F1}% | {PrP1(prAcc[0].Solo(u))} "
                          + $"| {(best < 0 ? "—" : prName[best])} | {(best < 0 ? "—" : $"{PrP1(prSt[0][u, best].Syn)} / {PrP1(prSt[1][u, best].Syn)}")} "
                          + $"| {prPosN[u]} | {prEntN[u]} | {prHookN[u]} | {prKeyOf[u].Length} |");
    }
    Console.WriteLine();
    Console.WriteLine("**「単独では弱いが特定の1枚と組むと化ける駒」＝ 単独の帰属が小さく、最良の相方との相乗が両帯で正の駒。** 表から読むだけで、この期は選ばない。");
    Console.WriteLine();

    Console.WriteLine("### 4-2. 空白の材料（正の相乗を1つも持たない駒）");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 測れる相手 | `Supplies`（キー@場所） | その供給を読む駒（盤面にいるか） | `Reads` |");
    Console.WriteLine("|---|--:|---|---|---|");
    string PrW(TraitEntryMap.Where w) => w switch { TraitEntryMap.Where.Self => "自分", TraitEntryMap.Where.Ally => "味方", TraitEntryMap.Where.Foe => "敵", _ => "任意" };
    foreach (int u in Enumerable.Range(0, prRN).Where(u => prPosN[u] == 0).OrderBy(u => prPartners[u].Length))
    {
        string sup = prSup[u].Length == 0 ? "—" : string.Join("・", prSup[u].Select(su => $"{UnitTally.CarryKeys[su.Key]}@{PrW(su.W)}"));
        var readers = Enumerable.Range(0, prRN).Where(v => v != u && PrSupplies(u, v)).ToArray();
        string rd = prSup[u].Length == 0 ? "—" : readers.Length == 0 ? "**いない**" : string.Join("・", readers.Select(v => prName[v] + (prCo[u, v] >= PrMinPair ? "" : "†")));
        string reads = prRead[u].Length == 0 ? "—" : string.Join("・", prRead[u].Select(r => $"{UnitTally.CarryKeys[r.Key]}@{PrW(r.W)}"));
        Console.WriteLine($"| {prName[u]} | {prPartners[u].Length} | {sup} | {rd} | {reads} |");
    }
    Console.WriteLine();
    Console.WriteLine("† はその組が測れなかった（在席標本 < 100）ことを示す。**「読む駒はいるが測れていない」と「読む駒がいない」は別**——前者は台の問題、後者はロスターの空白。");
    Console.WriteLine();

    // ---- Q5 / P6（オノ）--------------------------------------------------------------------
    Console.WriteLine("## Q5 —— 孤立の検出（正の相乗を持つ相手が 3 体未満の駒）");
    Console.WriteLine();
    {
        var iso = Enumerable.Range(0, prRN).Where(u => prPosN[u] < 3).OrderBy(u => prPosN[u]).ThenBy(u => prPartners[u].Length).ToArray();
        Console.WriteLine($"**{iso.Length} / {prRN} 体**（うち測れる相手が 10 未満の駒 {iso.Count(u => prPartners[u].Length < 10)} 体は「孤立」ではなく「測れていない」）:");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 正(有意) | 測れる相手 | 読み |");
        Console.WriteLine("|---|--:|--:|---|");
        foreach (int u in iso)
            Console.WriteLine($"| {prName[u]} | {prPosN[u]} | {prPartners[u].Length} | {(prPartners[u].Length < 10 ? "測れていない" : prPosN[u] == 0 ? "**孤立**" : "孤立に近い")} |");
        Console.WriteLine();
    }
    Console.WriteLine("### P6 —— 首刈りのオノ（52 体版・**参考値**。線 100 の相手だけで判定し、30 以上の相手も並べる）");
    Console.WriteLine();
    {
        var onoPs = Enumerable.Range(0, prRN).Where(v => prCo52[prOnoIx, v] >= 30).OrderByDescending(v => prAcc52[0].Of(prOnoIx, v).Syn).ToArray();
        Console.WriteLine($"オノの在席 {prIn52[prOnoIx]:N0}・単独(在席差) {PrP2(prAcc52[0].Solo(prOnoIx))} / {PrP2(prAcc52[1].Solo(prOnoIx))}"
                          + $"（第79期の帰属 +1.26 / +1.18 とは器具が違う）。在席標本 30 以上の相手 **{onoPs.Length} / {prRN}**・100 以上 {Enumerable.Range(0, prRN).Count(v => prCo52[prOnoIx, v] >= PrMinPair)}。");
        Console.WriteLine();
        Console.WriteLine("| 相手 | 在席 | 線 100 | 相乗 A | 相乗 B | SE A | 有意 A/B |");
        Console.WriteLine("|---|--:|:-:|--:|--:|--:|:-:|");
        foreach (int v in onoPs)
        {
            var a = prAcc52[0].Of(prOnoIx, v); var b = prAcc52[1].Of(prOnoIx, v);
            Console.WriteLine($"| {prName[v]} | {a.N11} | {(a.N11 >= PrMinPair ? "○" : "—")} | {PrP2(a.Syn)} | {PrP2(b.Syn)} | {a.Se:F2} | {(PrSig(a) ? "○" : "—")}{(PrSig(b) ? "○" : "—")} |");
        }
        var onoPs100 = onoPs.Where(v => prCo52[prOnoIx, v] >= PrMinPair).ToArray();
        int onoPos = onoPs100.Count(v => prAcc52[0].Of(prOnoIx, v).Syn > 2 * prAcc52[0].Of(prOnoIx, v).Se && prAcc52[1].Of(prOnoIx, v).Syn > 0);
        int onoNeg = onoPs100.Count(v => prAcc52[0].Of(prOnoIx, v).Syn < -2 * prAcc52[0].Of(prOnoIx, v).Se && prAcc52[1].Of(prOnoIx, v).Syn < 0);
        double onoMeanAbs = onoPs.Length == 0 ? double.NaN : onoPs.Average(v => Math.Abs(prAcc52[0].Of(prOnoIx, v).Syn));
        Console.WriteLine();
        Console.WriteLine($"線 100 の相手 {onoPs100.Length} 体のうち 正(有意) {onoPos} / 負(有意) {onoNeg}・|相乗| の平均（30 以上の {onoPs.Length} 体）{onoMeanAbs:F2}pt・両帯で符号一致 {onoPs.Count(v => Math.Sign(prAcc52[0].Of(prOnoIx, v).Syn) == Math.Sign(prAcc52[1].Of(prOnoIx, v).Syn))} / {onoPs.Length}。"
                          + $" **P6（全部 0 付近）: {(onoPos == 0 && onoNeg == 0 ? "○（有意な相乗を持つ相手が無い）" : "**×**")}**——ただし SE が 3〜6pt の参考値。"
                          + $" Q5 の定義（正(有意) < 3）ではオノは {(onoPos < 3 ? "**孤立の側に入る**" : "孤立ではない")}。");
        Console.WriteLine();
    }

    // ---- 予測の当否 ---------------------------------------------------------------------------
    Console.WriteLine("## 予測 P1〜P6 の当否");
    Console.WriteLine();
    {
        int posRaw = prMeasurable.Count(p => prSt[0][p.A, p.B].Syn > 0);
        int posSig = prMeasurable.Count(p => PrPos(p.A, p.B)), negSig = prMeasurable.Count(p => PrNeg(p.A, p.B));
        var named = new[] { ("borg", "hota"), ("zoto", "hota"), ("guza", "vio"), ("hisa", "zan"), ("kugu", "shiga") };
        var namedRows = named.Select(n =>
        {
            int a = prIdx[n.Item1], b = prIdx[n.Item2];
            int rank = prOrdA.FindIndex(p => (p.A == Math.Min(a, b) && p.B == Math.Max(a, b)));
            return (Name: PrPair(a, b), Co: prCo[a, b], Rank: rank, Syn: rank < 0 ? double.NaN : prSt[0][a, b].Syn, SynB: rank < 0 ? double.NaN : prSt[1][a, b].Syn);
        }).ToArray();
        int topSupply = prOrdA.Take(PrTop).Count(p => PrClass(p.A, p.B) == 0);
        var wound = new[] { "kiri", "egu", "nomi", "nata", "hari" }.Select(id => prIdx[id]).ToHashSet();
        int botWound = prOrdA.TakeLast(PrTop).Count(p => wound.Contains(p.A) && wound.Contains(p.B));
        int woundMeas = prMeasurable.Count(p => wound.Contains(p.A) && wound.Contains(p.B));
        double woundMean = woundMeas == 0 ? double.NaN : prMeasurable.Where(p => wound.Contains(p.A) && wound.Contains(p.B)).Average(p => prSt[0][p.A, p.B].Syn);
        double absSyn = prMeasurable.Average(p => Math.Abs(prSt[0][p.A, p.B].Syn));
        double absSynSig = sigA == 0 ? double.NaN : prMeasurable.Where(p => PrSig(prSt[0][p.A, p.B])).Average(p => Math.Abs(prSt[0][p.A, p.B].Syn));
        double absCon = Enumerable.Range(0, prRN).Where(u => !double.IsNaN(prCon[0][u])).Average(u => Math.Abs(prCon[0][u]));
        Console.WriteLine("| # | 予測 | 実測 | 当否 |");
        Console.WriteLine("|--:|---|---|:-:|");
        Console.WriteLine($"| P1 | 相乗が正の組は半分未満 | 生の符号で正 {posRaw} / {prMeasurable.Count}（{100.0 * posRaw / prMeasurable.Count:F1}%）・有意な正 {posSig} 対 有意な負 {negSig} | {(posRaw * 2 < prMeasurable.Count ? "○" : "**×**")}（生）/ {(posSig < negSig ? "○" : "**×**")}（有意） |");
        Console.WriteLine($"| P2 | 上位は書き手×読み手 | 上位 {PrTop} 組のうち 供給→読み {topSupply} 組。名指しの5組: "
                          + string.Join("・", namedRows.Select(n => $"{n.Name} {(n.Rank < 0 ? $"測れず（在席 {n.Co}）" : $"{n.Rank + 1} 位 {PrP1(n.Syn)}/{PrP1(n.SynB)}")}"))
                          + $" | {(namedRows.Count(n => n.Rank >= 0 && n.Rank < PrTop) >= 3 ? "○" : "**×**")}（5組中 {namedRows.Count(n => n.Rank >= 0 && n.Rank < PrTop)} 組が上位 {PrTop}） |");
        Console.WriteLine($"| P3 | 下位は傷の5枚どうし・消費型どうし | 傷どうしの組: 測れる {woundMeas} / 10・平均相乗 {PrP2(woundMean)}・下位 {PrTop} に {botWound} 組 | {(woundMeas > 0 && woundMean < 0 ? "○（符号）" : "**×**")} |");
        Console.WriteLine($"| P4 | 相乗は単独の帰属より小さい（±1〜3pt） | |相乗| の平均 {absSyn:F2}（有意な組だけ {absSynSig:F2}）対 |帰属| の平均 {absCon:F2} | {(absSyn < absCon ? "○" : "**×**")}（**ただし相乗の |平均| は SE の大きさを写す**） |");
        Console.WriteLine($"| P5 | 同キーの組の平均相乗が第72期の傾きと符号一致 | {q4Match} / {prNK} | {(q4Match >= 8 ? "○" : "**×**")} |");
        Console.WriteLine($"| P6 | オノとの相乗が全部 0 付近 | 上の P6 の表 | 参考値 |");
        Console.WriteLine();
    }

    // ---- 判定のまとめ ---------------------------------------------------------------------------
    Console.WriteLine("## 判定 Q1〜Q6");
    Console.WriteLine();
    Console.WriteLine("| # | 問い | 実測 | 判定 |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| Q1 | |相乗| > 2SE の組が 50 以上 | A 帯 {sigA}（雑音の期待値 {nullExp:F0}）・両帯で有意・同符号 {sigBoth} | {(sigA >= 50 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q2 | 上位・下位 {PrTop} 組が両帯で 2/3 以上一致 | 上位 {topOv} / 下位 {botOv} | {(q2 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q2' | 同じ集合が半割（標本の偶奇）で 2/3 以上一致（**抽選の揺れ**。指示書に無い自己検査 (f)） | 上位 {q2hTop} / 下位 {q2hBot} | {(q2h ? "○" : "**×**")} |");
    Console.WriteLine($"| Q3 | （供給→読み）＞（読み→読み） | {PrP2(prClassMean[0])} 対 {PrP2(prClassMean[1])} | {(q3 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q4 | 同キーの組と第72期の傾きの符号一致 ≥ 8 / 11 | {q4Match} / {prNK} | {(q4Match >= 8 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q5 | 正の相乗を持つ相手が 3 体未満の駒を名指し | {Enumerable.Range(0, prRN).Count(u => prPosN[u] < 3)} 体（上の表） | 名指した |");
    Console.WriteLine("| Q6 | `compare` 305 セル 0 件・`docs/` 差分 0 | `pairs check` と `docs/` の再生成で別に確かめる | — |");
    Console.WriteLine();
    Console.WriteLine($"所要 {prSw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}
}
