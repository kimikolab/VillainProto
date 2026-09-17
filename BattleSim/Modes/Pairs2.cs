using BattleCore;
using static Common;

// =====================================================================================
// pairs2 モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "pairs2")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 pairs2
// =====================================================================================

static class Pairs2Diag
{
// 第81期 —— 組の標本を狙って積む（2×2 の要因計画に組み直す）
//
// 第80期の器具（**在席差**）には穴が2つあった:
//   (1) 1標本から取れる組は C(5,2) = 10 なので、11,000 標本では1組あたり**上限 86**。線 100 で測れたのは 430 / 1,275 組で、
//       **測れる組を1つも持たない駒が 13 体**（毒6・標2・死軸4・強化供給3 がまるごと表の外）。空白は規則 P の抽選の性質
//   (2) A/B 帯は**同じ 11,000 編成**を別の戦闘 seed で回しているので、帯間の一致は**自分自身との一致**だった（自己検査 (g)）
//
// そこで在席差をやめ、**同じ台（同じ埋め草3枚・同じ席・同じ戦闘 seed）の上で A と B の中身だけを4通りに振る**:
//
//     y11 = 両方が本物 / y10 = A 本物・B 素体 / y01 = A 素体・B 本物 / y00 = 両方が素体
//     相乗(A,B) = y11 − y10 − y01 + y00        ← 2×2 の交互作用
//
// **器具は素体差し替えに統一した**（第69期の標準器具）。在席差は表C（新旧の橋渡し）に**並べるだけ**で、相乗の計算には使わない。
// **既存の `pairs` は1文字も書き換えていない**（新旧の比較に要る）。engine・`Traits.cs`・`UnitCatalog`・`Stages`・`CompareBuilds()` は触らない。
//
//     dotnet run --project BattleSim -c Release 0 pairs2 phase0  # 紙の計算（**戦闘0回**。予算・群の大きさ・台の抽選・2系列の独立）
//     dotnet run --project BattleSim -c Release 0 pairs2         # 主表（表A〜F・Q1〜Q5）
//     dotnet run --project BattleSim -c Release 0 pairs2 check   # Q6: `compare` 305 セルの突き合わせ
public static void Run(string[] args, int stageIndex)
{
    string pgArg = args.Length > 2 ? args[2] : "";
    var pgSw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> pgStages = EnemyCatalog.Stages;
    int pgW = pgStages.Count;
    var pgRoster = UnitCatalog.All.ToArray();
    int pgRN = pgRoster.Length;                       // 51
    int pgNK = UnitTally.CarryKeys.Length;            // 11

    // ---- 台の定数（第76〜80期のドラフト台の写し。抽選・規則配置・波・seed 本数は1文字も変えていない）------
    const int PgOfferSeed = 2_000_000;                // 第80期の在席差を再現するための抽選（表C・第2群の選定）
    const int PgN = 11000;                            // 同上
    const int PgM = 8;                                // 戦闘 seed の本数
    const int PgStrong = 7, PgWeakPct = 60;
    const int PgMinPair = 100;                        // 第80期の「測れる組」の線（表C・表F の突き合わせにだけ使う）
    const int PgTop = 30;                             // 表A の上位・下位の行数
    // ---- 2×2 の定数（この期に決めた。**測る前に固定**）----------------------------------------
    const int PgTableSeed = 8_100_000;                // 台の抽選の起点
    const int PgK = 64;                               // 1組・1系列あたりの台数（§1-4。**予算だけで決めた**。最低 30）
    const int PgS = 2;                                // 独立系列の本数（自己検査 (g)）
    const int PgBand = 0;                             // 戦闘 seed 0..7。**2系列で同じ**——違いは台の抽選だけ
    const int PgDrawCap = 20000;                      // 台の抽選の打ち切り（重複を弾いて 2K 通り集めるまで）
    // この機械で実測した戦闘/秒（`pairs` 4,928,000 戦 / 391.9 秒）。**予算の計算にだけ使う**
    const double PgRatePerSec = 4_928_000.0 / 391.9;

    // 第72期 `D4Slope100` の写し（表E）。並びは `UnitTally.CarryKeys`。**既存モードは触らないので写しを持つ**
    int[] PgSlope100 = { -90, -650, 190, 653, -1256, -330, -796, -1557, -759, 1936, 981 };
    // 第71期 表E の出どころ（(1枚−0枚), (2枚−1枚)）。第72期 `D4Src71` / 第80期 `prSrc71` の写し
    double[][] pgSrc71 = {
        new[] { 0.90, -2.69 }, new[] { -8.03, -4.96 }, new[] { -4.82, 8.62 }, new[] { 2.80, 10.26 },
        new[] { -9.38, -15.73 }, new[] { -7.04, 0.45 }, new[] { -3.31, -12.60 }, new[] { -15.84, -15.30 },
        new[] { -6.79, -8.38 }, new[] { 20.95, 17.76 }, new[] { 9.11, 10.51 },
    };

    var pgIdx = new Dictionary<string, int>();
    for (int u = 0; u < pgRN; u++) pgIdx[pgRoster[u].Id] = u;
    string[] pgName = pgRoster.Select(d => d.Name).ToArray();

    var pgKeyOf = pgRoster.Select(TraitKeyMap.KeysOf).ToArray();
    var pgRead = pgRoster.Select(d => d.Traits.SelectMany(t => TraitEntryMap.Reads.TryGetValue(t, out var r) ? r : Array.Empty<(int Key, TraitEntryMap.Where W)>()).Distinct().ToArray()).ToArray();
    var pgSup = pgRoster.Select(d => d.Traits.SelectMany(t => TraitEntryMap.Supplies.TryGetValue(t, out var sq) ? sq : Array.Empty<(int Key, TraitEntryMap.Where W)>()).Distinct().ToArray()).ToArray();

    // A の供給 s が B の読み r を満たすか（第80期 `PrFeeds` の写し）
    bool PgFeeds((int Key, TraitEntryMap.Where W) su, (int Key, TraitEntryMap.Where W) r)
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
    bool PgSupplies(int a, int b) => pgSup[a].Any(su => pgRead[b].Any(r => PgFeeds(su, r)));
    string[] pgClassName = { "供給→読み", "読み→読み", "供給→供給", "キーだけ共有", "共有無し" };
    int PgClass(int a, int b)
    {
        if (PgSupplies(a, b) || PgSupplies(b, a)) return 0;
        if (pgRead[a].Select(r => r.Key).Intersect(pgRead[b].Select(r => r.Key)).Any()) return 1;
        if (pgSup[a].Select(su => su.Key).Intersect(pgSup[b].Select(su => su.Key)).Any()) return 2;
        if (pgKeyOf[a].Intersect(pgKeyOf[b]).Any()) return 3;
        return 4;
    }

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜80期と同一。`Stages` は書き換えない）------------------
    var pgWeakCache = new Dictionary<string, UnitDef>();
    UnitDef PgWeakOf(UnitDef d)
    {
        if (pgWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * PgWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        pgWeakCache[d.Id] = w;
        return w;
    }
    var pgWeak = pgStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = PgWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    // 素体（同数値・特性なし。**攻・HP・速・攻撃型だけを写し、`Actions` は落とす**——第58期以降の標準器具）
    UnitDef PgPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    // **素体は51体ぶんを1度だけ作って使い回す**（`PgWeakOf` と同じ作法）。
    // 呼ぶたびに `UnitDef` を作ると 65 万回ぶんの割り当てになり、GC のヒープが機械の空きを食い潰す。
    var pgPlainMap = pgRoster.ToDictionary(d => d.Id, PgPlain);

    // 規則 P（第70〜80期の写し・5枚）。**第80期の在席差を再現するためだけに使う**
    UnitDef[] PgTeam5(UnitDef[] roster, int i)
    {
        int rn = roster.Length;
        var rng = new Random(PgOfferSeed + i);
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
            if (sel.Attack >= PgStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(roster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }

    // 埋め草3枚（**残り 49 体から規則 P**）。攻7以上の枚数は **A・B を数えた状態から始める**——
    // 埋め草は「A と B が既に居る台」へ引かれるので、規則 P の条件（攻7以上が2枚未満か）は**台について**読むのが形に忠実。
    // **測る前に固定した選択**（自己検査 (e)）。これで A・B が両方とも殴れる駒のときは埋め草が体へ寄り、床が薄くなる側に倒れる。
    UnitDef[] PgFill(UnitDef[] pool, int strong0, int seed)
    {
        int rn = pool.Length;                          // 49
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
            if (sel.Attack >= PgStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }

    // 台の seed。**近い seed の `Random` を並走させない**（第71期）——(組, 引き番号) を 64bit で混ぜて散らす。
    // 系列は「引いた順に交互へ配る」ので **seed の定数差では分けない**（§1-5 / 自己検査 (g)）。
    int PgSeed(int pairIx, int draw)
    {
        ulong x = (ulong)PgTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }

    // 規則配置 H（第69期以来の写し）。**A と B の席は4版すべてで同じ**（席は本物の5枚から1回だけ決める）
    int[] PgSeats(UnitDef[] u)
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
    // 版 v: bit0 = A（添字0）を素体に / bit1 = B（添字1）を素体に
    Formation PgForm(UnitDef[] u, int[] seats, int v)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (v & 1) != 0) || (k == 1 && (v & 2) != 0);
            f[seats[k]] = plain ? pgPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    Formation PgForm5(UnitDef[] u, int[] seats, int plainIdx)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = k == plainIdx ? pgPlainMap[u[k].Id] : u[k];
        return f;
    }
    double PgRate(Formation f, int band)
    {
        double sum = 0;
        for (int wi = 1; wi < pgW; wi++)
        {
            int wins = 0;
            for (int seed = band; seed < band + PgM; seed++)
                if (BattleEngine.Run(f, pgWeak[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
            sum += wins * 100.0 / PgM;
        }
        return sum / (pgW - 1);
    }
    string PgP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    string PgPair(int a, int b) => $"{pgName[a]} × {pgName[b]}";
    double PgSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double PgMedian(IEnumerable<double> xs)
    {
        var a = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToArray();
        if (a.Length == 0) return double.NaN;
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2.0;
    }
    long PgC(int n, int k) { long r = 1; for (int i = 1; i <= k; i++) r = r * (n - k + i) / i; return r; }

    var pgAllRows = CompareBuilds();
    var pgBalance = new Dictionary<string, double[]>();
    if (File.Exists("docs/balance.md"))
        foreach (string line in File.ReadAllLines("docs/balance.md"))
        {
            if (!line.StartsWith("| ")) continue;
            var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
            if (cells.Length != pgW + 1 || !cells[1].EndsWith("%")) continue;
            var v = new double[pgW];
            bool ok = true;
            for (int w = 0; w < pgW; w++)
                if (!double.TryParse(cells[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
            if (ok) pgBalance[cells[0]] = v;
        }

    var pgAllPairs = new List<(int A, int B)>();
    for (int a = 0; a < pgRN; a++) for (int b = a + 1; b < pgRN; b++) pgAllPairs.Add((a, b));
    var pgPairIxOf = new int[pgRN, pgRN];
    for (int pi = 0; pi < pgAllPairs.Count; pi++) { pgPairIxOf[pgAllPairs[pi].A, pgAllPairs[pi].B] = pi; pgPairIxOf[pgAllPairs[pi].B, pgAllPairs[pi].A] = pi; }

    // ---- 第80期の抽選（**戦闘0回**）。在席標本数と「測れなかった 13 体」はここで確定する -------------
    var pgIx51 = new int[PgN][];
    Parallel.For(0, PgN, i => pgIx51[i] = PgTeam5(pgRoster, i).Select(d => pgIdx[d.Id]).ToArray());
    var pgCo = new int[pgRN, pgRN];
    var pgIn = new int[pgRN];
    for (int i = 0; i < PgN; i++)
    {
        int[] t = pgIx51[i];
        for (int a = 0; a < 5; a++)
        {
            pgIn[t[a]]++;
            for (int b = a + 1; b < 5; b++) { pgCo[t[a], t[b]]++; pgCo[t[b], t[a]]++; }
        }
    }
    var pgOld = pgAllPairs.Where(p => pgCo[p.A, p.B] >= PgMinPair).ToList();      // 第80期に測れた 430 組
    // 第80期 §8-2 の「測れる組を1つも持たない駒」——**名前を焼かずに定義から数える**
    var pgBlind = Enumerable.Range(0, pgRN)
                            .Where(u => Enumerable.Range(0, pgRN).All(v => v == u || pgCo[u, v] < PgMinPair))
                            .ToArray();

    // ---- 群（§1-3。**測る前に固定**。予算が全組を賄うなら群は「落ちた組」を数える器具として残る）------
    //  第1群: 13 体を含む組のうち、供給→読み の関係があるもの（`TraitEntryMap`）
    //  第2群: 第80期に |相乗| > 2SE だった組（在席差・A 帯）
    //  第3群: 残りを `Def.Id` の辞書順
    var pgG1 = pgAllPairs.Where(p => (pgBlind.Contains(p.A) || pgBlind.Contains(p.B)) && PgClass(p.A, p.B) == 0).ToHashSet();

    // =====================================================================================
    // phase0: 紙の計算（**戦闘0回**）
    // =====================================================================================
    if (pgArg == "phase0")
    {
        Console.WriteLine("# 第81期 Phase 0 —— 組の標本を狙って積む（紙の計算）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `dotnet run --project BattleSim -c Release 0 pairs2 phase0`");
        Console.WriteLine();

        // ---- §1-1 予算 ---------------------------------------------------------------------
        Console.WriteLine("## 1-1. 予算（指示書 §1-1）");
        Console.WriteLine();
        Console.WriteLine($"この機械の実測は **{PgRatePerSec:N0} 戦/秒**（第80期の `pairs` を回し直して 4,928,000 戦 / 391.9 秒）。"
                          + "指示書が引く 256 秒は別の機械の値で、**この機械では 1.53 倍かかる**。**予算は実測の側で立てる。**");
        Console.WriteLine();
        Console.WriteLine($"1組・1系列・1台あたりの戦闘数は **4 版 × {pgW - 1} 波 × seed {PgM} 本 = {4 * (pgW - 1) * PgM} 戦**。");
        Console.WriteLine();
        Console.WriteLine("| 組の数 | K（台数/系列） | 系列 | 戦闘数 | 所要 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|");
        foreach (int nPair in new[] { pgAllPairs.Count, 600, 300 })
            foreach (int k in new[] { 30, 64, 100, 128 })
            {
                long b = (long)nPair * 4 * k * PgS * (pgW - 1) * PgM;
                Console.WriteLine($"| {nPair:N0} | {k} | {PgS} | {b:N0} | {(nPair == pgAllPairs.Count && k == PgK ? "**" : "")}{b / PgRatePerSec / 60:F1} 分{(nPair == pgAllPairs.Count && k == PgK ? "**" : "")} |");
            }
        Console.WriteLine();
        long bMain = (long)pgAllPairs.Count * 4 * PgK * PgS * (pgW - 1) * PgM;
        long bOld = (long)PgN * (pgW - 1) * PgM;
        Console.WriteLine($"**採る値: 全 {pgAllPairs.Count:N0} 組 × K = {PgK} × {PgS} 系列 = {bMain:N0} 戦 ＝ {bMain / PgRatePerSec / 60:F1} 分。**"
                          + $" これに表C（新旧の橋渡し）のための第80期の在席差の再現 {bOld:N0} 戦"
                          + $"（{bOld / PgRatePerSec:F0} 秒・**A 帯だけ・版は現行1つだけ**）を足して"
                          + $" **合計 {(bMain + bOld) / PgRatePerSec / 60:F1} 分**。指示書の 30 分に収まる。");
        Console.WriteLine();
        Console.WriteLine("**K は予算だけで決めた**（自由度 (e)）——「30 分に収まる最大の K を 2 の冪で採る」。"
                          + "**組を減らさずに済んだので、§1-3 の群は「どこまで測るか」ではなく「第80期がどこを落としていたか」を数える器具として残る**（表F）。"
                          + "指示書の「30 分を超えるなら組を減らす。台や波や seed を削らない」は発動していない。");
        Console.WriteLine();

        // ---- §1-2 素体 ---------------------------------------------------------------------
        Console.WriteLine("## 1-2. 素体の表（指示書 §1-2）");
        Console.WriteLine();
        int nAct = pgRoster.Count(d => d.Actions is { Count: > 0 });
        int nTr2 = pgRoster.Count(d => d.Traits.Count >= 2);
        Console.WriteLine($"`PgPlain` は **攻・HP・速・攻撃型だけを写す**（`Traits` は空・`Actions` は落とす）。"
                          + $"51 体すべてに機械的に作れる。`Actions` を持つ駒は {nAct} 体・特性2枚以上は {nTr2} 体で、"
                          + "**どちらも素体では消える**（第58期以降の標準器具と同じ）。");
        Console.WriteLine();
        bool okNum = pgRoster.All(d => { var p = PgPlain(d); return p.MaxHp == d.MaxHp && p.Attack == d.Attack && p.Speed == d.Speed && p.Pattern == d.Pattern; });
        bool okTr = pgRoster.All(d => PgPlain(d).Traits.Count == 0);
        bool okId = pgRoster.All(d => !pgIdx.ContainsKey(PgPlain(d).Id));
        bool okAct = pgRoster.All(d => PgPlain(d).Actions is null or { Count: 0 });
        Console.WriteLine("| 検査 | 値 | 判定 |");
        Console.WriteLine("|---|--:|:-:|");
        Console.WriteLine($"| 素体が本物と数値・攻撃型で1つも違わない | {pgRoster.Count(d => { var p = PgPlain(d); return p.MaxHp == d.MaxHp && p.Attack == d.Attack && p.Speed == d.Speed && p.Pattern == d.Pattern; })} / {pgRN} | {(okNum ? "○" : "**×**")} |");
        Console.WriteLine($"| 素体が特性を1つも持たない | {pgRoster.Count(d => PgPlain(d).Traits.Count == 0)} / {pgRN} | {(okTr ? "○" : "**×**")} |");
        Console.WriteLine($"| 素体が `Actions` を持たない | {pgRoster.Count(d => PgPlain(d).Actions is null or { Count: 0 })} / {pgRN} | {(okAct ? "○" : "**×**")} |");
        Console.WriteLine($"| 素体の Id が本物と衝突しない | {pgRoster.Count(d => !pgIdx.ContainsKey(PgPlain(d).Id))} / {pgRN} | {(okId ? "○" : "**×**")} |");
        Console.WriteLine();

        // ---- §1-3 群 -----------------------------------------------------------------------
        Console.WriteLine("## 1-3. 測る組と群（指示書 §1-3）");
        Console.WriteLine();
        Console.WriteLine($"第80期の抽選（規則 P・seed {PgOfferSeed:N0} + i・{PgN:N0} 標本）は決定的なので、"
                          + $"**「線 {PgMinPair} で測れた組」も「測れる組を1つも持たない駒」も戦闘なしで確定する。**");
        Console.WriteLine();
        Console.WriteLine($"- 第80期に測れた組: **{pgOld.Count} / {pgAllPairs.Count}**（{100.0 * pgOld.Count / pgAllPairs.Count:F1}%）");
        Console.WriteLine($"- 測れる組を1つも持たない駒: **{pgBlind.Length} 体** — {string.Join("・", pgBlind.Select(u => $"{pgName[u]}（在席 {pgIn[u]}）"))}");
        Console.WriteLine();
        Console.WriteLine("| 群 | 定義 | 組数 |");
        Console.WriteLine("|---|---|--:|");
        Console.WriteLine($"| 第1群 | {pgBlind.Length} 体を含み、かつ 供給→読み の関係がある | {pgG1.Count} |");
        Console.WriteLine($"| 第2群 | 第80期に \\|相乗\\| > 2SE（在席差・A 帯。**戦闘が要るので主表で確定する**） | 129 前後 |");
        Console.WriteLine($"| 第3群 | 残り（`Def.Id` 辞書順） | 残り |");
        Console.WriteLine($"| 合計 | | **{pgAllPairs.Count}** |");
        Console.WriteLine();
        Console.WriteLine($"**この期は全 {pgAllPairs.Count:N0} 組を測るので、群の途中で打ち切らない**（指示書 §1-3 の但し書きを満たす）。"
                          + $"第1群 {pgG1.Count} 組の内訳:");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席（第80期） | 第80期に測れた相手 | 第1群の相手 | `Supplies` | `Reads` |");
        Console.WriteLine("|---|--:|--:|--:|---|---|");
        foreach (int u in pgBlind.OrderBy(u => pgIn[u]))
            Console.WriteLine($"| {pgName[u]} | {pgIn[u]} | {Enumerable.Range(0, pgRN).Count(v => v != u && pgCo[u, v] >= PgMinPair)} "
                              + $"| {pgG1.Count(p => p.A == u || p.B == u)} "
                              + $"| {(pgSup[u].Length == 0 ? "—" : string.Join("・", pgSup[u].Select(s => $"{UnitTally.CarryKeys[s.Key]}@{s.W}")))} "
                              + $"| {(pgRead[u].Length == 0 ? "—" : string.Join("・", pgRead[u].Select(s => $"{UnitTally.CarryKeys[s.Key]}@{s.W}")))} |");
        Console.WriteLine();

        // ---- §1-4 台と埋め草 ----------------------------------------------------------------
        Console.WriteLine("## 1-4. 台と埋め草（指示書 §1-4）");
        Console.WriteLine();
        Console.WriteLine("台 = 埋め草3枚 ＋ A ＋ B。埋め草は**残り 49 体から規則 P で3枚**、席は**規則配置 H**、"
                          + "**A と B の席は4版すべてで同じ**（席は本物の5枚から1回だけ決めて、素体は同じ席に差し替える）。");
        Console.WriteLine();
        Console.WriteLine("**規則 P の「攻7以上が2枚未満か」は、A と B を数えた状態から始める**（測る前に固定した選択・自由度 (e)）。"
                          + "埋め草は「A と B が既に居る台」に引かれるので、条件は**台について**読むのが規則 P の形に忠実である。"
                          + "これで A・B が両方とも殴れる駒のときは埋め草が体へ寄り、逆のときは攻撃力へ寄る——**床が薄くなる側の選択**（Q5）。");
        Console.WriteLine();
        int pgStrongN = pgRoster.Count(d => d.Attack >= PgStrong);
        Console.WriteLine($"ロスターの攻 {PgStrong} 以上は **{pgStrongN} / {pgRN}**（{100.0 * pgStrongN / pgRN:F1}%）。組の側では"
                          + $" 両方が攻{PgStrong}以上 **{pgAllPairs.Count(p => pgRoster[p.A].Attack >= PgStrong && pgRoster[p.B].Attack >= PgStrong)}** 組・"
                          + $"片方だけ **{pgAllPairs.Count(p => (pgRoster[p.A].Attack >= PgStrong) != (pgRoster[p.B].Attack >= PgStrong))}** 組・"
                          + $"どちらも下 **{pgAllPairs.Count(p => pgRoster[p.A].Attack < PgStrong && pgRoster[p.B].Attack < PgStrong)}** 組"
                          + "——**最後の群だけが埋め草の1・2枚目を攻撃力で引く。**");
        Console.WriteLine();

        // ---- §1-5 2系列の独立 ---------------------------------------------------------------
        Console.WriteLine("## 1-5. 2系列は独立か（指示書 §1-5・**自己検査 (g)**）");
        Console.WriteLine();
        Console.WriteLine("**第80期の A/B 帯は同じ 11,000 編成を共有していたので、帯間の一致は再現の証拠にならなかった。**"
                          + "この期は台の抽選そのものを分ける。**戦闘 seed は2系列で同じ（0..7）にしてある**"
                          + "——違いを「台の抽選」だけに閉じるため。**戦闘 seed の違いを再現と呼ばない。**");
        Console.WriteLine();
        Console.WriteLine($"やり方: 組ごとに引き番号 0, 1, 2, … で埋め草を引き、**埋め草の3枚組が既出なら捨てて**"
                          + $"**{PgS * PgK} 通りの相異なる台**を集め、**引いた順に交互へ配る**（偶数番→系列1・奇数番→系列2）。"
                          + "これで**2系列が1つの編成も共有しないことが構成から保証される**。");
        Console.WriteLine();
        Console.WriteLine("**指示書 §1-5 の「台の抽選 seed を2系列に分ける」からはここだけ外れている**（前提の訂正）。"
                          + "理由は2つ: (1) seed を分ける書き方だと重複しないことが**偶然任せ**になり、"
                          + "指示書自身が要求する「編成が1つも重ならないことを確認してから測る」を構成では保証できない。"
                          + "(2) seed を定数差で2本に分けると**近い seed の `Random` を2本並走させる**ことになり、第71期の相関にそのまま当たる"
                          + "（あの期は超幾何との照合で標準誤差の 32 倍のずれを検出している）。"
                          + "**要求されているのは「独立な2系列」であって「2つの seed」ではない**ので、要求のほうを満たした。");
        Console.WriteLine();
        Console.WriteLine("台の seed そのものも (組, 引き番号) を 64bit で混ぜて作る（SplitMix64）ので、隣り合う引き番号の系列は相関しない。");
        Console.WriteLine();

        // 抽選の性質（**戦闘0回**）: 何回引けば 2K 通り集まるか / 埋め草の分布
        Console.Error.Write("台の抽選を全組ぶん回して性質を数えています…");
        var pgDraws = new int[pgAllPairs.Count];
        var pgDistinct = new int[pgAllPairs.Count];
        var pgFillCnt = new int[pgRN];
        object pgLk = new();
        Parallel.For(0, pgAllPairs.Count, pi =>
        {
            (int a, int b) = pgAllPairs[pi];
            var pool = pgRoster.Where((_, u) => u != a && u != b).ToArray();
            int strong0 = (pgRoster[a].Attack >= PgStrong ? 1 : 0) + (pgRoster[b].Attack >= PgStrong ? 1 : 0);
            var seen = new HashSet<(int, int, int)>();
            var local = new int[pgRN];
            int draw = 0;
            while (seen.Count < PgS * PgK && draw < PgDrawCap)
            {
                var f = PgFill(pool, strong0, PgSeed(pgPairIxOf[a, b], draw));
                var t = f.Select(d => pgIdx[d.Id]).OrderBy(x => x).ToArray();
                if (seen.Add((t[0], t[1], t[2]))) foreach (int u in t) local[u]++;
                draw++;
            }
            pgDraws[pi] = draw; pgDistinct[pi] = seen.Count;
            lock (pgLk) for (int u = 0; u < pgRN; u++) pgFillCnt[u] += local[u];
        });
        Console.Error.WriteLine(" 済");
        int badPair = pgDistinct.Count(d => d < PgS * PgK);
        double medDraw = PgMedian(pgDraws.Select(x => (double)x));
        Console.WriteLine("| 量 | 値 | 判定 |");
        Console.WriteLine("|---|--:|:-:|");
        Console.WriteLine($"| 1組あたり必要な相異なる台 | {PgS * PgK} | — |");
        Console.WriteLine($"| 引いた回数（中央値 / 最大 / 打ち切り） | {medDraw:F0} / {pgDraws.Max()} / {PgDrawCap} | {(pgDraws.Max() < PgDrawCap ? "○" : "**×**")} |");
        Console.WriteLine($"| {PgS * PgK} 通り集まらなかった組 | {badPair} | {(badPair == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| 2系列が共有する編成 | **0**（構成から） | ○ |");
        Console.WriteLine();
        Console.WriteLine($"重複して捨てた割合は 1 − {PgS * PgK} / {medDraw:F0} = **{100.0 * (1 - (double)(PgS * PgK) / medDraw):F1}%**"
                          + "（規則 P は攻撃力・HP の2段しか見ないので同じ3枚が繰り返し出る）。"
                          + $"埋め草の組み合わせは C(49,3) = {PgC(49, 3):N0} 通りあるが、規則 P が実際に到達するのはそのごく一部である。");
        Console.WriteLine();
        Console.WriteLine("埋め草として引かれた回数の上位・下位 8 体（**規則 P の偏りがそのまま出る**。全組 × "
                          + $"{PgS * PgK} 台 × 3 枚 = {(long)pgAllPairs.Count * PgS * PgK * 3:N0} 枚）:");
        Console.WriteLine();
        Console.WriteLine("| 上位 | 回数 | 攻 | HP | | 下位 | 回数 | 攻 | HP |");
        Console.WriteLine("|---|--:|--:|--:|---|---|--:|--:|--:|");
        var pgHi = Enumerable.Range(0, pgRN).OrderByDescending(u => pgFillCnt[u]).Take(8).ToArray();
        var pgLo = Enumerable.Range(0, pgRN).OrderBy(u => pgFillCnt[u]).Take(8).ToArray();
        for (int i = 0; i < 8; i++)
            Console.WriteLine($"| {pgName[pgHi[i]]} | {pgFillCnt[pgHi[i]]:N0} | {pgRoster[pgHi[i]].Attack} | {pgRoster[pgHi[i]].MaxHp} | "
                              + $"| {pgName[pgLo[i]]} | {pgFillCnt[pgLo[i]]:N0} | {pgRoster[pgLo[i]].Attack} | {pgRoster[pgLo[i]].MaxHp} |");
        Console.WriteLine();
        Console.WriteLine($"**埋め草の偏りは第80期の在席の偏りと同じ向き**（規則 P は同じ）。ただし**この期は A と B を抽選で決めない**ので、"
                          + $"**{pgBlind.Length} 体を含む 51 体すべてが必ず {pgRN - 1} 組の主役として測られる**——これが 2×2 に組み直した狙いそのもの。"
                          + "**埋め草の側の偏りは残る**（台の性質であって、測る対象の偏りではない）。");
        Console.WriteLine();

        // ---- §1-6 恒等式 -------------------------------------------------------------------
        Console.WriteLine("## 1-6. 第80期の恒等式（申し送り §6 の確認）");
        Console.WriteLine();
        long c51_5 = PgC(pgRN, 5), c49_3 = PgC(pgRN - 2, 3);
        Console.WriteLine($"- 1標本から取れる組は C(5,2) = {PgC(5, 2)}。{PgN:N0} 標本 × 10 ÷ {pgAllPairs.Count} = "
                          + $"**{(double)PgN * 10 / pgAllPairs.Count:F1}**（1組あたりの上限）。実測の中央値は "
                          + $"**{PgMedian(pgAllPairs.Select(p => (double)pgCo[p.A, p.B])):F0}**、最大 {pgAllPairs.Max(p => pgCo[p.A, p.B])}");
        Console.WriteLine($"- 2枚が同時に在席する確率 = C(49,3)/C(51,5) = {c49_3:N0} / {c51_5:N0} = **{100.0 * c49_3 / c51_5:F3}%**"
                          + "（第80期の指示書の「約 1.96%」は誤り。**申し送り §6 のとおり**）");
        Console.WriteLine($"- 2×2 では 1組あたり **{PgS * PgK} 台**（在席差の上限 86 の {(double)(PgS * PgK) / 86.3:F2} 倍）。"
                          + "**しかも対応のある比較なので、同じ台数でも標準誤差はもっと小さいはず**（P1）");
        Console.WriteLine();
        Console.WriteLine("## 予測（**測る前に書く**）");
        Console.WriteLine();
        Console.WriteLine("| # | 量 | 予測 |");
        Console.WriteLine("|--:|---|---|");
        Console.WriteLine("| P1 | 標準誤差 | 2×2 は在席差より小さい。**同じ標本数なら 1/2 以下** |");
        Console.WriteLine("| P2 | 床 | 台が固定されるので 0%/100% の張り付きが減る。中間帯は規則 P の 57% より**高い** |");
        Console.WriteLine("| P3 | 新旧の橋渡し | 第80期に有意だった 129 組のうち**符号一致 2/3 以上** |");
        Console.WriteLine("| P4 | 13 体 | **全員に測れる組ができる。**そのうち正の相乗を持つ駒が出る |");
        Console.WriteLine("| P5 | 上位の顔ぶれ | **ヨミ×バサ・ボルグ×ホタ・ソラ×トメ**は残る |");
        Console.WriteLine("| P6 | Q4 | 第72期の傾きとの符号一致が **7/11 より改善する** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {pgSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check: `compare` 305 セルの突き合わせ（Q6）
    // =====================================================================================
    if (pgArg == "check")
    {
        Console.WriteLine("# 第81期 —— 受け入れ基準（Q6）: `compare` 305 セルの突き合わせ");
        Console.WriteLine();
        int mism = 0, cellsN = 0, missing = 0;
        var lines = new List<string>();
        Parallel.For(0, pgAllRows.Length, i =>
        {
            var v = new double[pgW];
            for (int w = 0; w < pgW; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(pgAllRows[i].F, pgStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                v[w] = wins * 100.0 / 200;
            }
            lock (lines)
            {
                if (!pgBalance.TryGetValue(pgAllRows[i].Name, out double[]? doc)) { missing++; return; }
                for (int w = 0; w < pgW; w++)
                {
                    cellsN++;
                    if (Math.Abs(doc[w] - v[w]) > 0.05) { mism++; lines.Add($"| {pgAllRows[i].Name} | 第{w + 1}波 | {doc[w]:F1} | {v[w]:F1} |"); }
                }
            }
        });
        Console.WriteLine($"`CompareBuilds()` {pgAllRows.Length} 行 × {pgW} 波 × seed 0..199 を回し直して `docs/balance.md` と突き合わせた: "
                          + $"**{cellsN} セル・ずれ {mism} 件**（`docs/balance.md` に無い行 {missing}）。");
        if (lines.Count > 0)
        {
            Console.WriteLine(); Console.WriteLine("| 行 | 波 | docs | 今 |"); Console.WriteLine("|---|---|--:|--:|");
            foreach (string l in lines) Console.WriteLine(l);
        }
        Console.WriteLine();
        Console.WriteLine($"`UnitCatalog.All` は {UnitCatalog.All.Count} 体。この期は engine も `Traits.cs` も駒も波も触っていないので、ずれは 0 件でなければならない。");
        Console.WriteLine();
        Console.WriteLine($"所要 {pgSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }
    // ---- 1組ぶんの 2×2（`run` と「その場で全部測る」版で共有する）--------------------------------
    (double[][] Syn, double[] Ver, int F00, int F11, int NT) PgMeasure(int pi)
    {
        (int a, int b) = pgAllPairs[pi];
        var pool = pgRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (pgRoster[a].Attack >= PgStrong ? 1 : 0) + (pgRoster[b].Attack >= PgStrong ? 1 : 0);
        // 相異なる台を 2K 通り集めて、引いた順に交互へ配る（§1-5）
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < PgS * PgK && draw < PgDrawCap; draw++)
        {
            var f = PgFill(pool, strong0, PgSeed(pgPairIxOf[a, b], draw));
            var t = f.Select(d => pgIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        var syn = new List<double>[PgS];
        for (int s = 0; s < PgS; s++) syn[s] = new List<double>();
        var vsum = new double[4];
        int f00 = 0, f11 = 0;
        for (int t = 0; t < fills.Count; t++)
        {
            var team = new[] { pgRoster[a], pgRoster[b], fills[t][0], fills[t][1], fills[t][2] };
            int[] seats = PgSeats(team);
            var y = new double[4];
            for (int v = 0; v < 4; v++) y[v] = PgRate(PgForm(team, seats, v), PgBand);
            // y[0] = y11（両方が本物）/ y[1] = y01（A 素体）/ y[2] = y10（B 素体）/ y[3] = y00（両方が素体）
            syn[t % PgS].Add(y[0] - y[2] - y[1] + y[3]);
            for (int v = 0; v < 4; v++) vsum[v] += y[v];
            if (y[3] <= 0.0 || y[3] >= 100.0) f00++;
            if (y[0] <= 0.0 || y[0] >= 100.0) f11++;
        }
        int nT = fills.Count;
        return (syn.Select(l => l.ToArray()).ToArray(),
                vsum.Select(x => nT == 0 ? double.NaN : x / nT).ToArray(), f00, f11, nT);
    }

    // =====================================================================================
    // run: 組の [skip, skip+take) だけを測って TSV で吐く
    // =====================================================================================
    // **長時間ジョブの分割**（`reseat` の skip / take と同じ作法）。全 1,275 組を一度に回すと 28 分かかり、
    // その間ずっと数百万個の `Formation` を作り続けるので、機械の空きメモリ次第で OS に落とされる。
    // シャードは `Def.Id` の順ではなく**添字の順**（`pgAllPairs` の並び）で切る——どこで切っても結果は同じ。
    if (pgArg == "run")
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int skip = args.Length > 3 ? int.Parse(args[3]) : 0;
        int take = args.Length > 4 ? int.Parse(args[4]) : pgAllPairs.Count;
        skip = Math.Clamp(skip, 0, pgAllPairs.Count);
        take = Math.Clamp(take, 0, pgAllPairs.Count - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"pairs2 run {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            var m = PgMeasure(skip + j);
            var sb = new System.Text.StringBuilder();
            sb.Append(pgAllPairs[skip + j].A).Append('	').Append(pgAllPairs[skip + j].B).Append('	')
              .Append(m.NT).Append('	').Append(m.F00).Append('	').Append(m.F11);
            for (int v = 0; v < 4; v++) sb.Append('	').Append(m.Ver[v].ToString("R", inv));
            for (int s = 0; s < PgS; s++) foreach (double x in m.Syn[s]) sb.Append('	').Append(x.ToString("R", inv));
            rows[j] = sb.ToString();
            if (Interlocked.Increment(ref doneR) % 25 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {pgSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    string pgShardPath = "";
    if (pgArg == "tables")
    {
        pgShardPath = args.Length > 3 ? args[3] : "";
        if (pgShardPath == "" || !File.Exists(pgShardPath))
        {
            Console.WriteLine("pairs2 tables <`run` の吐いた TSV を連結したファイル>");
            return;
        }
    }
    else if (pgArg != "")
    {
        Console.WriteLine("pairs2: 引数は phase0 / （無し）/ run <skip> <take> / tables <path> / check。");
        return;
    }

    // =====================================================================================
    // 主表
    // =====================================================================================
    // ---- (1) 第80期の在席差を再現する（表C の左半分・第2群の選定）。**A 帯・版は現行1つだけ** ----------
    Console.Error.Write("在席差（第80期の再現）: ");
    var pgYold = new double[PgN];
    {
        int done = 0;
        Parallel.For(0, PgN, i =>
        {
            UnitDef[] team = PgTeam5(pgRoster, i);
            pgYold[i] = PgRate(PgForm5(team, PgSeats(team), -1), PgBand);
            if (Interlocked.Increment(ref done) % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
    }
    var pgAcc = new PairAcc(pgRN);
    for (int i = 0; i < PgN; i++) pgAcc.Add(pgIx51[i], pgYold[i]);
    var pgOldSt = new PairAcc.Stat[pgRN, pgRN];
    foreach ((int a, int b) in pgAllPairs) pgOldSt[a, b] = pgOldSt[b, a] = pgAcc.Of(a, b);
    // 第2群 = 第80期に |相乗| > 2SE（在席差・A 帯・測れる組）
    var pgG2 = pgOld.Where(p => pgOldSt[p.A, p.B].Se > 0 && Math.Abs(pgOldSt[p.A, p.B].Syn) > 2 * pgOldSt[p.A, p.B].Se).ToList();

    // ---- (2) 2×2 の本測定（その場で全部測るか、`run` の吐いた TSV を読む）------------------------
    int pgNP = pgAllPairs.Count;
    var pgSyn = new double[pgNP][][];      // [組][系列][台]
    var pgVer = new double[pgNP][];        // [組][版] 全 2K 台の平均
    var pgFloor00 = new int[pgNP];         // y00 が 0% か 100% の台数
    var pgFloor11 = new int[pgNP];
    var pgTables = new int[pgNP];
    if (pgShardPath == "")
    {
        int done = 0;
        Console.Error.Write($"2x2 ({pgNP} pairs x K {PgK} x series {PgS}): ");
        Parallel.For(0, pgNP, pi =>
        {
            var m = PgMeasure(pi);
            pgSyn[pi] = m.Syn; pgVer[pi] = m.Ver;
            pgFloor00[pi] = m.F00; pgFloor11[pi] = m.F11; pgTables[pi] = m.NT;
            if (Interlocked.Increment(ref done) % 50 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
    }
    else
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var seenPair = new bool[pgNP];
        int got = 0;
        foreach (string line in File.ReadAllLines(pgShardPath))
        {
            if (line.Length == 0) continue;
            var c = line.Split('	');
            int pi = pgPairIxOf[int.Parse(c[0]), int.Parse(c[1])];
            if (seenPair[pi]) continue;                    // シャードが重なっても取り違えない
            seenPair[pi] = true;
            pgTables[pi] = int.Parse(c[2]); pgFloor00[pi] = int.Parse(c[3]); pgFloor11[pi] = int.Parse(c[4]);
            pgVer[pi] = new double[4];
            for (int v = 0; v < 4; v++) pgVer[pi][v] = double.Parse(c[5 + v], inv);
            pgSyn[pi] = new double[PgS][];
            int at = 9;
            for (int sx = 0; sx < PgS; sx++)
            {
                int n = (pgTables[pi] - sx + PgS - 1) / PgS;
                pgSyn[pi][sx] = new double[n];
                for (int t = 0; t < n; t++) pgSyn[pi][sx][t] = double.Parse(c[at++], inv);
            }
            got++;
        }
        Console.Error.WriteLine($"シャードから {got} 組を読み込んだ");
        if (got != pgNP)
        {
            Console.WriteLine($"**シャードが足りない: {got} / {pgNP} 組。`pairs2 run` の全区間を連結してから `tables` に渡すこと。**");
            return;
        }
    }

    // ---- (3) 統計 ---------------------------------------------------------------------------
    var pgSynAll = new double[pgNP];
    var pgSeAll = new double[pgNP];
    var pgSynS = new double[pgNP][];
    var pgSeS = new double[pgNP][];
    for (int pi = 0; pi < pgNP; pi++)
    {
        var all = pgSyn[pi].SelectMany(x => x).ToArray();
        pgSynAll[pi] = all.Average();
        pgSeAll[pi] = PgSd(all) / Math.Sqrt(all.Length);
        pgSynS[pi] = pgSyn[pi].Select(x => x.Average()).ToArray();
        pgSeS[pi] = pgSyn[pi].Select(x => PgSd(x) / Math.Sqrt(x.Length)).ToArray();
    }
    // **測る前に固定した「有意」の定義**（第80期 `PrPos` の 2×2 版）:
    //   有意 = |相乗（合算 2K 台）| > 2 × SE（合算）、かつ **2系列とも同じ符号**
    bool PgSig(int pi) => Math.Abs(pgSynAll[pi]) > 2 * pgSeAll[pi];
    bool PgPos(int pi) => pgSynAll[pi] > 2 * pgSeAll[pi] && pgSynS[pi].All(x => x > 0);
    bool PgNeg(int pi) => pgSynAll[pi] < -2 * pgSeAll[pi] && pgSynS[pi].All(x => x < 0);

    long pgBattles = (long)pgNP * 4 * PgK * PgS * (pgW - 1) * PgM + (long)PgN * (pgW - 1) * PgM;

    Console.WriteLine("# 第81期 —— 組の標本を狙って積む（2×2 の要因計画）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 pairs2` の出力。**`docs/` には置かない。**");
    Console.WriteLine();
    Console.WriteLine("**器具は素体差し替えに統一した**（第69期の標準器具）。在席差は表C に**並べるだけ**で、相乗の計算には使っていない。");
    Console.WriteLine();
    Console.WriteLine("    y11 = 両方が本物 / y10 = A 本物・B 素体 / y01 = A 素体・B 本物 / y00 = 両方が素体");
    Console.WriteLine("    相乗(A,B) = y11 − y10 − y01 + y00");
    Console.WriteLine();
    Console.WriteLine($"台: 埋め草3枚（残り 49 体・規則 P）＋ A ＋ B・席は規則配置 H（**4版すべて同じ席**）・"
                      + $"弱い波 {PgWeakPct}%・第2〜{pgW}波・戦闘 seed {PgBand}..{PgBand + PgM - 1}。"
                      + $"**全 {pgNP:N0} 組 × K {PgK} 台 × {PgS} 系列**（系列は台の抽選だけが違う。**編成の共有はゼロ**）。");
    Console.WriteLine();
    Console.WriteLine($"回した戦闘は **{pgBattles:N0}**（うち第80期の在席差の再現が {(long)PgN * (pgW - 1) * PgM:N0}）。");
    Console.WriteLine();

    // ---- 4版の全体像 ------------------------------------------------------------------------
    Console.WriteLine("## 0. 4版の全体像");
    Console.WriteLine();
    {
        long tot = pgTables.Sum(x => (long)x);
        string[] vn = { "y11（両方が本物）", "y01（A を素体に）", "y10（B を素体に）", "y00（両方が素体）" };
        Console.WriteLine("| 版 | 平均勝率 | 張り付いた台（0% か 100%） | 中間帯 |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int v = 0; v < 4; v++)
        {
            double m = pgVer.Average(x => x[v]);
            if (v == 0 || v == 3)
            {
                long fl = v == 0 ? pgFloor11.Sum(x => (long)x) : pgFloor00.Sum(x => (long)x);
                Console.WriteLine($"| {vn[v]} | {m:F2}% | {fl:N0} / {tot:N0} | **{100.0 * (tot - fl) / tot:F1}%** |");
            }
            else Console.WriteLine($"| {vn[v]} | {m:F2}% | —（数えていない） | — |");
        }
        Console.WriteLine();
        Console.WriteLine($"全台数 {tot:N0}（{pgNP:N0} 組 × {PgS * PgK} 台）。台が {PgS * PgK} 通り集まらなかった組は "
                          + $"**{pgTables.Count(x => x < PgS * PgK)}**。");
    }
    Console.WriteLine();

    // ---- 表A -------------------------------------------------------------------------------
    var pgOrder = Enumerable.Range(0, pgNP).OrderByDescending(pi => pgSynAll[pi]).ToArray();
    Console.WriteLine($"## 表A —— 相乗の上位 {PgTop}・下位 {PgTop}");
    Console.WriteLine();
    Console.WriteLine("| # | 組 | 台 | y11 | y01 | y10 | y00 | **相乗** | SE | 系列1 | 系列2 | 有意 | 分類 | 第80期（在席差） |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|:-:|---|--:|");
    void PgRow(int rank, int pi)
    {
        (int a, int b) = pgAllPairs[pi];
        var st = pgOldSt[a, b];
        string old = st.N11 >= PgMinPair ? PgP2(st.Syn) : (st.N11 == 0 ? "—" : $"({PgP2(st.Syn)})");
        Console.WriteLine($"| {rank} | {PgPair(a, b)} | {pgTables[pi]} | {pgVer[pi][0]:F1} | {pgVer[pi][1]:F1} | {pgVer[pi][2]:F1} | {pgVer[pi][3]:F1} "
                          + $"| **{PgP2(pgSynAll[pi])}** | {pgSeAll[pi]:F2} | {PgP2(pgSynS[pi][0])} | {PgP2(pgSynS[pi][1])} "
                          + $"| {(PgPos(pi) ? "＋" : PgNeg(pi) ? "−" : "")} | {pgClassName[PgClass(a, b)]} | {old} |");
    }
    for (int i = 0; i < PgTop; i++) PgRow(i + 1, pgOrder[i]);
    Console.WriteLine("| … | | | | | | | | | | | | | |");
    for (int i = PgTop; i >= 1; i--) PgRow(pgNP - i + 1, pgOrder[pgNP - i]);
    Console.WriteLine();
    Console.WriteLine($"括弧つきの第80期の値は**在席標本が線 {PgMinPair} に届かなかった組**（第80期は測れなかった）。「—」は在席 0。");
    Console.WriteLine();

    // ---- Q1 / Q2 ---------------------------------------------------------------------------
    Console.WriteLine("## Q1 / Q2 —— 精度と、独立系列での再現");
    Console.WriteLine();
    double seMed = PgMedian(pgSeAll);
    double seOld = PgMedian(pgOld.Select(p => pgOldSt[p.A, p.B].Se));
    Console.WriteLine("| 量 | 値 |");
    Console.WriteLine("|---|--:|");
    Console.WriteLine($"| 測った組 | {pgNP:N0} / {pgAllPairs.Count:N0}（**全部**） |");
    Console.WriteLine($"| SE の中央値（合算 {PgS * PgK} 台） | **{seMed:F2}pt** |");
    Console.WriteLine($"| SE の四分位（Q1 / Q3） | {PgMedian(pgSeAll.OrderBy(x => x).Take(pgNP / 2)):F2} / {PgMedian(pgSeAll.OrderBy(x => x).Skip(pgNP / 2)):F2} |");
    Console.WriteLine($"| SE の最大 | {pgSeAll.Max():F2}pt |");
    Console.WriteLine($"| 1系列（{PgK} 台）の SE の中央値 | {PgMedian(pgSeS.Select(x => (x[0] + x[1]) / 2)):F2}pt |");
    Console.WriteLine($"| 第80期（在席差・測れた {pgOld.Count} 組）の SE の中央値 | {seOld:F2}pt |");
    Console.WriteLine($"| 相乗の標準偏差（組の間） | {PgSd(pgSynAll):F2}pt |");
    Console.WriteLine($"| 台ごとの相乗の標準偏差（組の中・中央値） | {PgMedian(Enumerable.Range(0, pgNP).Select(pi => PgSd(pgSyn[pi].SelectMany(x => x).ToArray()))):F2}pt |");
    Console.WriteLine();
    Console.WriteLine($"**Q1: SE の中央値 {seMed:F2}pt < 1.5 → {(seMed < 1.5 ? "○" : "**×**")}**");
    Console.WriteLine();
    var o1 = Enumerable.Range(0, pgNP).OrderByDescending(pi => pgSynS[pi][0]).ToArray();
    var o2 = Enumerable.Range(0, pgNP).OrderByDescending(pi => pgSynS[pi][1]).ToArray();
    int topOv = o1.Take(PgTop).Intersect(o2.Take(PgTop)).Count();
    int botOv = o1.TakeLast(PgTop).Intersect(o2.TakeLast(PgTop)).Count();
    double rS;
    {
        double m1 = pgSynS.Average(x => x[0]), m2 = pgSynS.Average(x => x[1]);
        double c = 0, v1 = 0, v2 = 0;
        for (int pi = 0; pi < pgNP; pi++)
        {
            double d1 = pgSynS[pi][0] - m1, d2 = pgSynS[pi][1] - m2;
            c += d1 * d2; v1 += d1 * d1; v2 += d2 * d2;
        }
        rS = c / Math.Sqrt(v1 * v2);
    }
    int signBoth = Enumerable.Range(0, pgNP).Count(pi => Math.Sign(pgSynS[pi][0]) == Math.Sign(pgSynS[pi][1]) && pgSynS[pi][0] != 0);
    int sigTopSign = o1.Take(PgTop).Count(pi => pgSynS[pi][1] > 0);
    int sigBotSign = o1.TakeLast(PgTop).Count(pi => pgSynS[pi][1] < 0);
    Console.WriteLine("| 量 | 値 | 第80期 Q2'（半割） |");
    Console.WriteLine("|---|--:|--:|");
    Console.WriteLine($"| 系列1 と系列2 の相関 r | **{rS:F3}** | 0.601 |");
    Console.WriteLine($"| 上位 {PgTop} の一致 | **{topOv} / {PgTop}** | 19 / 30 |");
    Console.WriteLine($"| 下位 {PgTop} の一致 | **{botOv} / {PgTop}** | 13 / 30 |");
    Console.WriteLine($"| 系列1 の上位 {PgTop} が系列2 でも正 | {sigTopSign} / {PgTop} | 30 / 30 |");
    Console.WriteLine($"| 系列1 の下位 {PgTop} が系列2 でも負 | {sigBotSign} / {PgTop} | 30 / 30 |");
    Console.WriteLine($"| 全組で符号が一致 | {signBoth} / {pgNP}（{100.0 * signBoth / pgNP:F1}%） | — |");
    Console.WriteLine();
    bool q2 = topOv >= 20 && botOv >= 20;
    Console.WriteLine($"**Q2（主判定）: 上位 {topOv} / 下位 {botOv}（線 20 / 30・**編成を1つも共有しない2系列**） → {(q2 ? "○" : "**×**")}**");
    Console.WriteLine();
    Console.WriteLine($"有意（\\|相乗\\| > 2SE）の組は **{Enumerable.Range(0, pgNP).Count(PgSig)}**"
                      + $"（正 {Enumerable.Range(0, pgNP).Count(PgPos)} / 負 {Enumerable.Range(0, pgNP).Count(PgNeg)}。"
                      + "正・負は「2系列とも同じ符号」も要る)。"
                      + $"雑音だけなら {pgNP} × 4.55% ≈ {pgNP * 0.0455:F0} 組。");
    Console.WriteLine();

    // ---- 表B -------------------------------------------------------------------------------
    var pgPosN = new int[pgRN]; var pgNegN = new int[pgRN];
    var pgSumSyn = new double[pgRN]; var pgBest = new int[pgRN]; var pgBestV = new double[pgRN];
    for (int u = 0; u < pgRN; u++) { pgBest[u] = -1; pgBestV[u] = double.NegativeInfinity; }
    for (int pi = 0; pi < pgNP; pi++)
    {
        (int a, int b) = pgAllPairs[pi];
        bool pos = PgPos(pi), neg = PgNeg(pi);
        foreach (int u in new[] { a, b })
        {
            int o = u == a ? b : a;
            if (pos) pgPosN[u]++;
            if (neg) pgNegN[u]++;
            pgSumSyn[u] += pgSynAll[pi];
            if (pgSynAll[pi] > pgBestV[u]) { pgBestV[u] = pgSynAll[pi]; pgBest[u] = o; }
        }
    }
    Console.WriteLine("## 表B —— 駒ごとの集計（51 体・正(有意) の降順）");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 測れた組 | 正(有意) | 負(有意) | 相乗の合計 | 平均 | 最良の相方 | 相乗 | 第80期に測れた相手 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|---|--:|--:|");
    foreach (int u in Enumerable.Range(0, pgRN).OrderByDescending(u => pgPosN[u]).ThenByDescending(u => pgSumSyn[u]))
    {
        int oldN = Enumerable.Range(0, pgRN).Count(v => v != u && pgCo[u, v] >= PgMinPair);
        Console.WriteLine($"| {(pgBlind.Contains(u) ? "**" + pgName[u] + "**" : pgName[u])} | {pgRN - 1} | {pgPosN[u]} | {pgNegN[u]} "
                          + $"| {PgP2(pgSumSyn[u])} | {PgP2(pgSumSyn[u] / (pgRN - 1))} | {pgName[pgBest[u]]} | {PgP2(pgBestV[u])} | {oldN} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**太字は第80期に測れる組を1つも持たなかった {pgBlind.Length} 体**（Q4）。");
    Console.WriteLine();

    // ---- 表C -------------------------------------------------------------------------------
    Console.WriteLine("## 表C —— 新旧の橋渡し（**Q3**）");
    Console.WriteLine();
    Console.WriteLine("第80期の在席差を**同じ抽選・同じ席・同じ戦闘 seed 0..7**で再現してから並べる（器具の検算を兼ねる）。");
    Console.WriteLine();
    Console.WriteLine("| 量 | 第80期の報告 | この期の再現 | 一致 |");
    Console.WriteLine("|---|--:|--:|:-:|");
    Console.WriteLine($"| 線 {PgMinPair} で測れる組 | 430 / 1,275 | {pgOld.Count} / {pgAllPairs.Count} | {(pgOld.Count == 430 ? "○" : "**×**")} |");
    Console.WriteLine($"| 測れる組を1つも持たない駒 | 13 | {pgBlind.Length} | {(pgBlind.Length == 13 ? "○" : "**×**")} |");
    Console.WriteLine($"| \\|相乗\\| > 2SE の組（A 帯） | 129 | **{pgG2.Count}** | {(pgG2.Count == 129 ? "○" : "**×**")} |");
    Console.WriteLine($"| SE の中央値 | 2.36 | {seOld:F2} | {(Math.Abs(seOld - 2.36) < 0.05 ? "○" : "**×**")} |");
    Console.WriteLine($"| A 帯の平均勝率 | — | {pgYold.Average():F2}% | — |");
    Console.WriteLine($"| 0% の標本 | — | {pgYold.Count(x => x <= 0.0):N0}（{100.0 * pgYold.Count(x => x <= 0.0) / PgN:F1}%） | — |");
    Console.WriteLine();
    int q3n, q3m = 0;
    double q3c;
    {
        var xs = new List<double>(); var ys = new List<double>();
        foreach ((int a, int b) in pgG2)
        {
            int pi = pgPairIxOf[a, b];
            double o = pgOldSt[a, b].Syn, n = pgSynAll[pi];
            if (Math.Sign(o) == Math.Sign(n)) q3m++;
            xs.Add(o); ys.Add(n);
        }
        q3n = xs.Count;
        double mx = xs.Average(), my = ys.Average(), c = 0, v1 = 0, v2 = 0;
        for (int i = 0; i < xs.Count; i++) { c += (xs[i] - mx) * (ys[i] - my); v1 += (xs[i] - mx) * (xs[i] - mx); v2 += (ys[i] - my) * (ys[i] - my); }
        q3c = c / Math.Sqrt(v1 * v2);
    }
    Console.WriteLine($"**第80期に有意だった {q3n} 組**について、在席差の相乗と 2×2 の相乗:");
    Console.WriteLine();
    Console.WriteLine("| 量 | 値 |");
    Console.WriteLine("|---|--:|");
    Console.WriteLine($"| 符号の一致 | **{q3m} / {q3n}（{100.0 * q3m / q3n:F1}%）** |");
    Console.WriteLine($"| 相関 r | {q3c:F3} |");
    Console.WriteLine($"| 在席差の平均 | {PgP2(pgG2.Average(p => pgOldSt[p.A, p.B].Syn))} |");
    Console.WriteLine($"| 2×2 の平均 | {PgP2(pgG2.Average(p => pgSynAll[pgPairIxOf[p.A, p.B]]))} |");
    Console.WriteLine($"| \\|在席差\\| の平均 | {pgG2.Average(p => Math.Abs(pgOldSt[p.A, p.B].Syn)):F2} |");
    Console.WriteLine($"| \\|2×2\\| の平均 | {pgG2.Average(p => Math.Abs(pgSynAll[pgPairIxOf[p.A, p.B]])):F2} |");
    Console.WriteLine();
    bool q3ok = q3m * 3 >= q3n * 2;
    Console.WriteLine($"**Q3: 符号一致 {q3m} / {q3n} ≥ 2/3 → {(q3ok ? "○" : "**×**")}**");
    Console.WriteLine();
    // **食い違いがどこに出ているか**（どちらが正しいかは決めない。分布を書くだけ）
    {
        var ok = pgG2.Where(p => Math.Sign(pgOldSt[p.A, p.B].Syn) == Math.Sign(pgSynAll[pgPairIxOf[p.A, p.B]])).ToArray();
        var ng = pgG2.Where(p => Math.Sign(pgOldSt[p.A, p.B].Syn) != Math.Sign(pgSynAll[pgPairIxOf[p.A, p.B]])).ToArray();
        Console.WriteLine("食い違いがどこに出ているか（**どちらが正しいかは決めない。分布を書くだけ**）:");
        Console.WriteLine();
        Console.WriteLine("| 群 | 組 | \\|在席差\\| の平均 | \\|2×2\\| の平均 | 2×2 が有意 | 2×2 が \\|相乗\\| < 2pt |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach ((string nm, (int A, int B)[] g) in new[] { ("符号が一致", ok), ("**符号が食い違った**", ng) })
            Console.WriteLine($"| {nm} | {g.Length} | {(g.Length == 0 ? 0 : g.Average(p => Math.Abs(pgOldSt[p.A, p.B].Syn))):F2} "
                              + $"| {(g.Length == 0 ? 0 : g.Average(p => Math.Abs(pgSynAll[pgPairIxOf[p.A, p.B]]))):F2} "
                              + $"| {g.Count(p => PgSig(pgPairIxOf[p.A, p.B]))} "
                              + $"| {g.Count(p => Math.Abs(pgSynAll[pgPairIxOf[p.A, p.B]]) < 2.0)} |");
        Console.WriteLine();
        Console.WriteLine("在席差の \\|相乗\\| の大きさ別の一致率:");
        Console.WriteLine();
        Console.WriteLine("| \\|在席差\\| | 組 | 符号一致 | 率 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach ((double lo, double hi) in new[] { (0.0, 8.0), (8.0, 12.0), (12.0, 1e9) })
        {
            var g = pgG2.Where(p => Math.Abs(pgOldSt[p.A, p.B].Syn) >= lo && Math.Abs(pgOldSt[p.A, p.B].Syn) < hi).ToArray();
            int mm = g.Count(p => Math.Sign(pgOldSt[p.A, p.B].Syn) == Math.Sign(pgSynAll[pgPairIxOf[p.A, p.B]]));
            Console.WriteLine($"| {lo:F0} 〜 {(hi > 1e8 ? "∞" : hi.ToString("F0"))} | {g.Length} | {mm} | {(g.Length == 0 ? 0 : 100.0 * mm / g.Length):F1}% |");
        }
    }
    Console.WriteLine();
    Console.WriteLine("全 129 組の対応（在席差の降順・上位 20 と下位 20）:");
    Console.WriteLine();
    Console.WriteLine("| 組 | 在席差 | 2×2 | 系列1 | 系列2 | 在席標本 | 分類 | 符号 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|---|:-:|");
    {
        var srt = pgG2.OrderByDescending(p => pgOldSt[p.A, p.B].Syn).ToArray();
        void G2Row((int A, int B) p)
        {
            int pi = pgPairIxOf[p.A, p.B];
            Console.WriteLine($"| {PgPair(p.A, p.B)} | {PgP2(pgOldSt[p.A, p.B].Syn)} | **{PgP2(pgSynAll[pi])}** "
                              + $"| {PgP2(pgSynS[pi][0])} | {PgP2(pgSynS[pi][1])} | {pgCo[p.A, p.B]} | {pgClassName[PgClass(p.A, p.B)]} "
                              + $"| {(Math.Sign(pgOldSt[p.A, p.B].Syn) == Math.Sign(pgSynAll[pi]) ? "○" : "**×**")} |");
        }
        foreach (var p in srt.Take(20)) G2Row(p);
        Console.WriteLine("| … | | | | | | | |");
        foreach (var p in srt.TakeLast(20)) G2Row(p);
    }
    Console.WriteLine();

    // ---- 表D -------------------------------------------------------------------------------
    Console.WriteLine("## 表D —— 分類（`TraitEntryMap` の `Reads` / `Supplies`）");
    Console.WriteLine();
    Console.WriteLine("| 分類 | 組 | 平均相乗 | 系列1 | 系列2 | 正(有意) | 負(有意) | \\|相乗\\| の平均 | 第80期（測れた組のみ） |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    var pgClassMean = new double[pgClassName.Length];
    for (int c = 0; c < pgClassName.Length; c++)
    {
        var ps = Enumerable.Range(0, pgNP).Where(pi => PgClass(pgAllPairs[pi].A, pgAllPairs[pi].B) == c).ToArray();
        pgClassMean[c] = ps.Length == 0 ? double.NaN : ps.Average(pi => pgSynAll[pi]);
        var psOld = pgOld.Where(p => PgClass(p.A, p.B) == c).ToArray();
        Console.WriteLine($"| **{pgClassName[c]}** | {ps.Length} | **{PgP2(pgClassMean[c])}** "
                          + $"| {PgP2(ps.Length == 0 ? double.NaN : ps.Average(pi => pgSynS[pi][0]))} "
                          + $"| {PgP2(ps.Length == 0 ? double.NaN : ps.Average(pi => pgSynS[pi][1]))} "
                          + $"| {ps.Count(PgPos)} | {ps.Count(PgNeg)} "
                          + $"| {(ps.Length == 0 ? "—" : ps.Average(pi => Math.Abs(pgSynAll[pi])).ToString("F2"))} "
                          + $"| {(psOld.Length == 0 ? "—" : PgP2(psOld.Average(p => pgOldSt[p.A, p.B].Syn)) + $"（{psOld.Length}）")} |");
    }
    Console.WriteLine();
    Console.WriteLine("（第80期の報告値は 供給→読み +3.36 / 読み→読み −1.11。**分母が違う**——第80期は在席差で測れた組だけ。）");
    Console.WriteLine();
    bool dOk = pgClassMean[0] > pgClassMean[1];
    Console.WriteLine($"（供給→読み）{PgP2(pgClassMean[0])} 対（読み→読み）{PgP2(pgClassMean[1])} → **{(dOk ? "第80期と同じ向き" : "第80期と逆")}**。");
    Console.WriteLine();
    Console.WriteLine("供給→読み の内訳（キーごと）:");
    Console.WriteLine();
    Console.WriteLine("| キー | 組 | 平均相乗 | 正(有意) | 負(有意) | 最大の組 | 相乗 |");
    Console.WriteLine("|---|--:|--:|--:|--:|---|--:|");
    for (int k = 0; k < pgNK; k++)
    {
        var ps = Enumerable.Range(0, pgNP).Where(pi =>
        {
            (int a, int b) = pgAllPairs[pi];
            return PgClass(a, b) == 0
                   && (pgSup[a].Any(su => su.Key == k && pgRead[b].Any(r => PgFeeds(su, r)))
                       || pgSup[b].Any(su => su.Key == k && pgRead[a].Any(r => PgFeeds(su, r))));
        }).ToArray();
        if (ps.Length == 0) continue;
        int best = ps.OrderByDescending(pi => pgSynAll[pi]).First();
        Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {ps.Length} | {PgP2(ps.Average(pi => pgSynAll[pi]))} "
                          + $"| {ps.Count(PgPos)} | {ps.Count(PgNeg)} | {PgPair(pgAllPairs[best].A, pgAllPairs[best].B)} | {PgP2(pgSynAll[best])} |");
    }
    Console.WriteLine();

    // ---- 表E -------------------------------------------------------------------------------
    Console.WriteLine("## 表E —— 第72期の傾きとの照合（**どちらが壊れているかはこの期でも決めない**）");
    Console.WriteLine();
    Console.WriteLine("| キー | 保持駒 | 組 | 平均相乗 | 系列1 | 系列2 | 第72期の傾き | 符号 | (2枚−1枚)−(1枚−0枚) | 符号 | 第80期 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|:-:|--:|:-:|--:|");
    int q4m = 0, q4m2 = 0, q4k = 0;
    for (int k = 0; k < pgNK; k++)
    {
        var ps = Enumerable.Range(0, pgNP).Where(pi => pgKeyOf[pgAllPairs[pi].A].Contains(k) && pgKeyOf[pgAllPairs[pi].B].Contains(k)).ToArray();
        int hold = Enumerable.Range(0, pgRN).Count(u => pgKeyOf[u].Contains(k));
        double slope = PgSlope100[k] / 100.0;
        double d71 = pgSrc71[k][1] - pgSrc71[k][0];
        if (ps.Length == 0)
        {
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {hold} | 0 | — | — | — | {PgP2(slope)} | — | {PgP2(d71)} | — | — |");
            continue;
        }
        q4k++;
        double m = ps.Average(pi => pgSynAll[pi]);
        int sg = Math.Abs(m) < 0.5 ? 0 : Math.Sign(m);
        bool ok1 = sg == Math.Sign(slope) && sg != 0;
        bool ok2 = sg == Math.Sign(d71) && sg != 0;
        if (ok1) q4m++;
        if (ok2) q4m2++;
        var psOld = pgOld.Where(p => pgKeyOf[p.A].Contains(k) && pgKeyOf[p.B].Contains(k)).ToArray();
        Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {hold} | {ps.Length} | **{PgP2(m)}** "
                          + $"| {PgP2(ps.Average(pi => pgSynS[pi][0]))} | {PgP2(ps.Average(pi => pgSynS[pi][1]))} "
                          + $"| {PgP2(slope)} | {(ok1 ? "○" : "**×**")} | {PgP2(d71)} | {(ok2 ? "○" : "**×**")} "
                          + $"| {(psOld.Length == 0 ? "—" : PgP2(psOld.Average(p => pgOldSt[p.A, p.B].Syn)))} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**符号一致 {q4m} / {pgNK}**（第80期は 7 / 11・線は 8 / 11）→ {(q4m >= 8 ? "○" : "**×**")}。"
                      + $" (2枚−1枚)−(1枚−0枚) と比べると {q4m2} / {pgNK}。"
                      + $"**{q4k} キーすべてで組が測れる**のは 2×2 だから（在席差では測れる組が無いキーがあった）。"
                      + "**0.5pt 未満の平均は 0 と読む**（第80期と同じ線）。");
    Console.WriteLine();

    // ---- Q4 / Q5 ---------------------------------------------------------------------------
    Console.WriteLine($"## Q4 —— 第80期に測れなかった {pgBlind.Length} 体");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 第80期の在席 | 第80期に測れた相手 | この期の組 | 正(有意) | 負(有意) | 最良の相方 | 相乗 | SE の中央値 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|---|--:|--:|");
    foreach (int u in pgBlind.OrderByDescending(u => pgPosN[u]).ThenByDescending(u => pgBestV[u]))
    {
        var mine = Enumerable.Range(0, pgNP).Where(pi => pgAllPairs[pi].A == u || pgAllPairs[pi].B == u).ToArray();
        Console.WriteLine($"| {pgName[u]} | {pgIn[u]} | 0 | {mine.Length} | {pgPosN[u]} | {pgNegN[u]} "
                          + $"| {pgName[pgBest[u]]} | {PgP2(pgBestV[u])} | {PgMedian(mine.Select(pi => pgSeAll[pi])):F2} |");
    }
    Console.WriteLine();
    int q4cov = pgBlind.Count(u => Enumerable.Range(0, pgNP).Any(pi => pgAllPairs[pi].A == u || pgAllPairs[pi].B == u));
    int q4pos = pgBlind.Count(u => pgPosN[u] > 0);
    Console.WriteLine($"**Q4: {q4cov} / {pgBlind.Length} 体に測れる組ができた → {(q4cov == pgBlind.Length ? "○" : "**×**")}**"
                      + $"（うち**正の相乗（有意）を持つ駒は {q4pos} 体**）。");
    Console.WriteLine();

    Console.WriteLine("## Q5 —— 床（「両方が素体」の版が張り付いた台の割合）");
    Console.WriteLine();
    double q5fr;
    {
        long tot = pgTables.Sum(x => (long)x);
        long f00 = pgFloor00.Sum(x => (long)x), f11 = pgFloor11.Sum(x => (long)x);
        q5fr = 100.0 * f00 / tot;
        Console.WriteLine("| 版 | 張り付いた台 | 割合 | 中間帯 |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| y00（両方が素体） | {f00:N0} / {tot:N0} | **{q5fr:F1}%** | {100.0 - q5fr:F1}% |");
        Console.WriteLine($"| y11（両方が本物） | {f11:N0} / {tot:N0} | {100.0 * f11 / tot:F1}% | {100.0 - 100.0 * f11 / tot:F1}% |");
        Console.WriteLine();
        Console.WriteLine($"**Q5: y00 の張り付きは {q5fr:F1}% → {(q5fr > 50 ? "**50% 超。埋め草の規則が悪い**（指示書 §4 の分岐へ）" : "50% 以下。埋め草の規則は保つ")}**");
        Console.WriteLine();
        Console.WriteLine($"参考: 第80期のドラフト台（規則 P・5枚とも本物・この期の再現）の 0% の標本は "
                          + $"**{100.0 * pgYold.Count(x => x <= 0.0) / PgN:F1}%**、中間帯（5 < x < 95）は "
                          + $"**{100.0 * pgYold.Count(x => x > 5.0 && x < 95.0) / PgN:F1}%**。");
    }
    Console.WriteLine();
    Console.WriteLine("床が厚い組・薄い組の上位 8:");
    Console.WriteLine();
    Console.WriteLine("**張り付きは 0% と 100% の合算**（指示書 Q5 の定義）。y00 の平均を併記したので、**この 8 組がどちらの端に寄っているかはこの列で読める**" + "——ただし**全体の 0% と 100% の内訳はこの回では分けていない**（積み残し）。");
    Console.WriteLine();
    Console.WriteLine("| 厚い組 | y00 張り付き | y00 | | 薄い組 | y00 張り付き | y00 |");
    Console.WriteLine("|---|--:|--:|---|---|--:|--:|");
    {
        var hi = Enumerable.Range(0, pgNP).OrderByDescending(pi => pgFloor00[pi]).Take(8).ToArray();
        var lo = Enumerable.Range(0, pgNP).OrderBy(pi => pgFloor00[pi]).Take(8).ToArray();
        for (int i = 0; i < 8; i++)
            Console.WriteLine($"| {PgPair(pgAllPairs[hi[i]].A, pgAllPairs[hi[i]].B)} | {pgFloor00[hi[i]]} / {pgTables[hi[i]]} | {pgVer[hi[i]][3]:F1} | "
                              + $"| {PgPair(pgAllPairs[lo[i]].A, pgAllPairs[lo[i]].B)} | {pgFloor00[lo[i]]} / {pgTables[lo[i]]} | {pgVer[lo[i]][3]:F1} |");
    }
    Console.WriteLine();

    // ---- 表F -------------------------------------------------------------------------------
    Console.WriteLine("## 表F —— 落とした組と、第80期との被覆の比較");
    Console.WriteLine();
    var g2set = pgG2.ToHashSet();
    int g1n = pgG1.Count, g2n = g2set.Count(p => !pgG1.Contains(p));
    Console.WriteLine("| 群 | 定義 | 組 | 測った | 落とした |");
    Console.WriteLine("|---|---|--:|--:|--:|");
    Console.WriteLine($"| 第1群 | {pgBlind.Length} 体 × 供給→読み | {g1n} | {g1n} | 0 |");
    Console.WriteLine($"| 第2群 | 第80期に有意（第1群を除く） | {g2n} | {g2n} | 0 |");
    Console.WriteLine($"| 第3群 | 残り | {pgNP - g1n - g2n} | {pgNP - g1n - g2n} | 0 |");
    Console.WriteLine($"| 合計 | | {pgNP} | **{pgNP}** | **0** |");
    Console.WriteLine();
    Console.WriteLine("**予算が全組を賄ったので、群は「どこまで測るか」ではなく「第80期がどこを落としていたか」を数える器具になった。**");
    Console.WriteLine();
    Console.WriteLine("| 器具 | 測れた組 | 測れた駒 | 測れる組が 0 の駒 |");
    Console.WriteLine("|---|--:|--:|--:|");
    Console.WriteLine($"| 第80期（在席差・線 {PgMinPair}） | {pgOld.Count} / {pgAllPairs.Count}（{100.0 * pgOld.Count / pgAllPairs.Count:F1}%） | {pgRN - pgBlind.Length} / {pgRN} | {pgBlind.Length} |");
    Console.WriteLine($"| この期（2×2・K {PgK} × {PgS} 系列） | {pgNP} / {pgAllPairs.Count}（100.0%） | {pgRN} / {pgRN} | 0 |");
    Console.WriteLine();
    {
        var missed = Enumerable.Range(0, pgNP).Where(pi => pgCo[pgAllPairs[pi].A, pgAllPairs[pi].B] < PgMinPair).ToArray();
        var kept = Enumerable.Range(0, pgNP).Where(pi => pgCo[pgAllPairs[pi].A, pgAllPairs[pi].B] >= PgMinPair).ToArray();
        Console.WriteLine($"- 第80期が落とした組: **{missed.Length}**、うちこの期に有意 **{missed.Count(PgSig)}**（{100.0 * missed.Count(PgSig) / Math.Max(1, missed.Length):F1}%）");
        Console.WriteLine($"- 第80期が測れた組: {kept.Length}、うちこの期に有意 {kept.Count(PgSig)}（{100.0 * kept.Count(PgSig) / Math.Max(1, kept.Length):F1}%）");
        Console.WriteLine();
        Console.WriteLine("落とされていた組の中で \\|相乗\\| が大きい 15 組:");
        Console.WriteLine();
        Console.WriteLine("| 組 | 相乗 | SE | 系列1 | 系列2 | 在席標本 | 分類 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|");
        foreach (int pi in missed.OrderByDescending(pi => Math.Abs(pgSynAll[pi])).Take(15))
        {
            (int a, int b) = pgAllPairs[pi];
            Console.WriteLine($"| {PgPair(a, b)} | **{PgP2(pgSynAll[pi])}** | {pgSeAll[pi]:F2} | {PgP2(pgSynS[pi][0])} | {PgP2(pgSynS[pi][1])} "
                              + $"| {pgCo[a, b]} | {pgClassName[PgClass(a, b)]} |");
        }
    }
    Console.WriteLine();

    // ---- P1〜P6 ----------------------------------------------------------------------------
    Console.WriteLine("## 予測の答え合わせ（P1〜P6）");
    Console.WriteLine();
    Console.WriteLine("| # | 予測 | 実測 | 判定 |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| P1 | SE は在席差の 1/2 以下 | 2×2 {seMed:F2}pt（{PgS * PgK} 台）対 在席差 {seOld:F2}pt（中央 {PgMedian(pgOld.Select(p => (double)pgOldSt[p.A, p.B].N11)):F0} 標本） | {(seMed <= seOld / 2 ? "○" : "**×**")} |");
    {
        long tot = pgTables.Sum(x => (long)x);
        double mid = 100.0 * (tot - pgFloor11.Sum(x => (long)x)) / tot;
        Console.WriteLine($"| P2 | 中間帯が 57% より高い | y11 の中間帯 {mid:F1}% | {(mid > 57 ? "○" : "**×**")} |");
    }
    Console.WriteLine($"| P3 | 129 組の符号一致 2/3 以上 | {q3m} / {q3n}（{100.0 * q3m / q3n:F1}%） | {(q3ok ? "○" : "**×**")} |");
    Console.WriteLine($"| P4 | {pgBlind.Length} 体全員に測れる組・正の相乗を持つ駒が出る | {q4cov} / {pgBlind.Length} 体・正を持つ {q4pos} 体 | {(q4cov == pgBlind.Length && q4pos > 0 ? "○" : "**×**")} |");
    {
        var wantPairs = new[] { ("ヨミ", "バサ"), ("ボルグ", "ホタ"), ("ソラ", "トメ") };
        var res = new List<string>();
        foreach ((string x, string y) in wantPairs)
        {
            int pi = Enumerable.Range(0, pgNP).FirstOrDefault(q =>
                (pgName[pgAllPairs[q].A].Contains(x) && pgName[pgAllPairs[q].B].Contains(y))
                || (pgName[pgAllPairs[q].A].Contains(y) && pgName[pgAllPairs[q].B].Contains(x)), -1);
            if (pi < 0) { res.Add($"{x}×{y} —"); continue; }
            int rank = Array.IndexOf(pgOrder, pi) + 1;
            res.Add($"{x}×{y} {PgP2(pgSynAll[pi])}（{rank} 位{(PgPos(pi) ? "・正(有意)" : "")}）");
        }
        Console.WriteLine($"| P5 | 上位の顔ぶれが残る | {string.Join(" / ", res)} | — |");
    }
    Console.WriteLine($"| P6 | Q4（表E）の符号一致が 7/11 より改善 | {q4m} / {pgNK} | {(q4m > 7 ? "○" : "**×**")} |");
    Console.WriteLine();

    // ---- Q まとめ --------------------------------------------------------------------------
    Console.WriteLine("## 判定");
    Console.WriteLine();
    Console.WriteLine("| # | 問い | 実測 | 判定 |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| Q1 | SE の中央値 < 1.5pt | {seMed:F2}pt | {(seMed < 1.5 ? "○" : "**×**")} |");
    Console.WriteLine($"| **Q2** | **独立系列で上位・下位 30 が 20/30 以上一致** | 上位 {topOv} / 下位 {botOv}（r {rS:F3}） | {(q2 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q3 | 第80期に有意な組で符号一致 2/3 以上 | {q3m} / {q3n} | {(q3ok ? "○" : "**×**")} |");
    Console.WriteLine($"| Q4 | {pgBlind.Length} 体すべてに測れる組 | {q4cov} / {pgBlind.Length} | {(q4cov == pgBlind.Length ? "○" : "**×**")} |");
    Console.WriteLine($"| Q5 | y00 の張り付きが 50% 以下 | {q5fr:F1}% | {(q5fr <= 50 ? "○" : "**×**")} |");
    Console.WriteLine("| Q6 | `compare` 305 セル 0 件・`docs/` 差分 0 | `pairs2 check` と `docs/` の再生成で別に確かめる | — |");
    Console.WriteLine();
    Console.WriteLine($"所要 {pgSw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}
}
