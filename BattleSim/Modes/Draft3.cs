using BattleCore;
using static Common;

// =====================================================================================
// draft3 モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "draft3")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 draft3
// =====================================================================================

static class Draft3Diag
{
// draft3 モード: ドラフトで取り戻せるシナジーは何 pt か（第71期・調査）
//
// 第70期は「**選択ありドラフトの編成は静的な数値の上で理想編成と見分けが付かない**
// （総攻 48.9 対 48.6 / 総HP 376 対 372）**のに、既存5波では 32.09% 対 67.2%**」で終わった。
// **差はまるごとシナジーである。** そして第70期の選択規則は**攻撃力と HP しか見ていない**
// ——シナジーを一切狙わないプレイヤーの値。
//
// **選択規則をシナジー志向にすると、その差の何 pt が戻るか。数えるだけ。設計判断をしない。**
//
//   規則 N（無作為）      提示3枚から乱数で1枚。**下限**（この規則だけ選択にも乱数を使う）
//   規則 P（素朴）        第70期の C/D と同一。攻7以上が2枚未満なら攻撃力最大／以後は最大HP
//   規則 S（シナジー志向） すでに選んだ駒と共有するキーの数が最大 → 同数なら攻撃力最大 → Id 辞書順
//
// **3規則 × 2波（既存 / 弱い波＝敵の MaxHp 60%）= 6版。** 配置は規則配置（H）のみ。
// **`Stages` は書き換えない**（弱い波は診断のローカル。`gradient` / `aim` と同じ扱い）。
//
// **物差しは第2〜5波**（第70期の訂正）。第一波は平均 93〜99% の天井なので、
// 5波の集計に混ぜると「第一波しか勝てない編成」が 1/5 = 20% ＝中間帯の内側に落ちる。
// **実行はするが、判定からは外す**（5波版は併記）。
//
//     dotnet run --project BattleSim -c Release 0 draft3 phase0  # 紙の計算（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 draft3         # 主表（表A〜E）・A 帯 seed 0..7
//     dotnet run --project BattleSim -c Release 0 draft3 alt     # 同じ標本を B 帯（seed 200..207）で
//     dotnet run --project BattleSim -c Release 0 draft3 check   # 陰性対照（Q6）
public static void Run(string[] args, int stageIndex)
{
    string d3Arg = args.Length > 2 ? args[2] : "";
    IReadOnlyList<EnemyCatalog.Stage> d3Base = EnemyCatalog.Stages;
    int d3W = d3Base.Count;
    var d3Sw = System.Diagnostics.Stopwatch.StartNew();
    var d3Roster = UnitCatalog.All.ToArray();
    int d3RN = d3Roster.Length;                       // 51
    // **第141期: キー表は `Everyone` で引く。** 理想61行（`CompareBuilds()`）にはエグが残っているので、
    // `d3Roster`（＝`All`・抽選の母集団）だけで表を作ると `D3Pairs` が `d3KeyOf["egu"]` で落ちる。
    var d3KeyOf = UnitCatalog.Everyone.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);

    // ---- 測る前に固定した定数 ------------------------------------------------------------
    //
    // **提示の系列は3規則で共有する**（`D3OfferSeed`）。P は第70期の `D2DraftTeam` と
    // 系列も手順も同一なので、**P の編成は第70期の C/D と1体も違わない**——器具の検算になる。
    //
    // **N の「選ぶ」乱数も同じ系列から引く**（提示の3回の直後に `Next(3)`）。
    // 最初は別系列（`Random(3_000_000 + i)`）に分けていたが、**`new Random(seed)` は
    // seed が近い2本のあいだで相関する**（.NET は互換性のために旧アルゴリズムを使い続けている）
    // ——実測で N の「攻7以上の期待枚数」が超幾何の 2.25 に対して **2.36** になり、
    // 100,000 標本の標準誤差 0.0034 の **32 倍**ずれた。1本の系列から順に引けば消える。
    // **phase0 の §0-3（超幾何との一致）がこの検算そのもの。**
    // 代償は N の提示が2枚目以降 P/S とずれること（N は対照であって対標本ではないので構わない）。
    const int D3OfferSeed = 2_000_000;
    const int D3BandA = 0, D3BandB = 200;
    const int D3M = 8;                 // 1標本あたりの戦闘 seed 数（第69・70期と同じ）
    const int D3N = 11000;             // 標本数（第69・70期と同じ。据え置きの根拠は phase0 の §0-5）
    const int D3Strong = 7;            // 規則 P が使う「殴れる駒」の線
    const int D3WeakPct = 60;          // 弱い波: 敵の MaxHp をこの % に（切り捨て）。第70期と同一
    const double D3Z = 1.96;
    const int D3R = 3, D3V = 6;        // 規則3 × 波2 = 版6。ver = rule * 2 + wk

    string[] d3RuleName = { "N（無作為）", "P（素朴）", "S（シナジー志向）" };
    string[] d3RuleBare = { "無作為", "素朴", "シナジー志向" };   // 版名の中で規則名が二重にならないように
    string[] d3RuleShort = { "N", "P", "S" };
    string[] d3WkName = { "既存5波", "弱い波" };
    string D3VerName(int v) => $"{d3RuleShort[v / 2]}{(v % 2 == 0 ? "" : "w")}（{d3RuleBare[v / 2]} × {d3WkName[v % 2]}）";
    string D3VerShort(int v) => d3RuleShort[v / 2] + (v % 2 == 0 ? "" : "w");

    // ---- 弱い波（第70期 `draft2` の写し。**HP だけの1変数**） ------------------------------
    var d3WeakCache = new Dictionary<string, UnitDef>();
    UnitDef D3WeakOf(UnitDef d)
    {
        if (d3WeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * D3WeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        d3WeakCache[d.Id] = w;
        return w;
    }
    var d3Weak = d3Base.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = D3WeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();
    Formation D3Enemy(int wk, int w) => wk == 0 ? d3Base[w].Enemy : d3Weak[w].Enemy;

    // ---- 素体（第69・70期と同じ構成。**`Actions` は落とす**） ------------------------------
    var d3Plain = d3Roster.Select(d => new UnitDef
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    }).ToArray();

    // ---- 3つの選択規則（**測る前に固定。§2-1 から1行も増やしていない**） -------------------
    //
    //   共通: まだ選ばれていない駒から無作為に3枚提示 → 1枚選ぶ。これを5回。
    //         **提示して選ばなかった2枚は次回も候補に残る**（第70期と同じ）。
    //
    //   N: 提示の中から乱数で1枚。
    //      **これは「無作為5枚」と分布が完全に一致する**——残りから一様に3枚出して
    //      その中から一様に1枚選ぶのは、残りから一様に1枚選ぶのと同じ（対称性）。
    //      第70期 A/B の分布が再現することが検算になる（表C）。
    //   P: すでに選んだ駒に攻 `D3Strong` 以上が2枚未満なら提示の最大攻撃力、以後は最大HP。
    //   S: すでに選んだ駒**全体**のキーの和集合と共有するキーの数が最大の駒
    //      → 同数なら攻撃力が最大 → 同値は `Def.Id` の辞書順。
    //      **1枚目は共有キーが必ず 0 なので攻撃力で決まる**（指示書 §2-1 の但し書き。それでよい）。
    UnitDef[] D3Team(int rule, int i)
    {
        var rng = new Random(D3OfferSeed + i);
        var idx = new int[d3RN];
        for (int k = 0; k < d3RN; k++) idx[k] = k;
        int remain = d3RN, strong = 0;
        var picked = new UnitDef[5];
        var keysSoFar = new HashSet<int>();
        for (int r = 0; r < 5; r++)
        {
            // 残り `remain` 枚から無作為に3枚（部分 Fisher-Yates を3つぶん進める）
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = d3Roster[idx[t]];
            }
            UnitDef sel;
            if (rule == 0)
                sel = offer[rng.Next(3)];
            else if (rule == 1)
                sel = strong < 2
                    ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                    : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            else
                sel = offer.OrderByDescending(x => d3KeyOf[x.Id].Count(k => keysSoFar.Contains(k)))
                           .ThenByDescending(x => x.Attack)
                           .ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= D3Strong) strong++;
            foreach (int k in d3KeyOf[sel.Id]) keysSoFar.Add(k);
            // 選んだ1枚だけを候補から外す
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(d3Roster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }

    // 規則配置 H（第69期 §1-3 の写し。第70期と同一）
    int[] D3Seats(UnitDef[] u)
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
    Formation D3Form(UnitDef[] u, int[] seats, int replace, UnitDef? with)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = (k == replace) ? with : u[k];
        return f;
    }

    // ---- 噛み合わせの指標（**測る前に固定**） ----------------------------------------------
    //
    // **共有キーの組数** = 5枚から2枚を選ぶ 10 組のうち、キーを1つ以上共有する組の数（0〜10）。
    // **相方を持つ駒の割合** は第70期 表C の写し（比較のために両方出す）。
    int D3Pairs(IReadOnlyList<UnitDef> u)
    {
        int c = 0;
        for (int a = 0; a < 5; a++)
            for (int b = a + 1; b < 5; b++)
                if (d3KeyOf[u[a].Id].Any(x => d3KeyOf[u[b].Id].Contains(x))) c++;
        return c;
    }
    int D3HasPal(IReadOnlyList<UnitDef> u)
    {
        int c = 0;
        for (int a = 0; a < 5; a++)
        {
            bool has = false;
            for (int b = 0; b < 5 && !has; b++)
                if (b != a && d3KeyOf[u[a].Id].Any(x => d3KeyOf[u[b].Id].Contains(x))) has = true;
            if (has) c++;
        }
        return c;
    }

    // ---- 統計の道具（第70期の写し） ---------------------------------------------------------
    string D3P1(double x) => (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    string D3P2(double x) => (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    (double Mean, double Sd, int N) D3Stats(IReadOnlyList<double> xs)
    {
        int n = xs.Count;
        if (n == 0) return (0, 0, 0);
        double m = xs.Average();
        if (n < 2) return (m, 0, n);
        return (m, Math.Sqrt(xs.Sum(x => (x - m) * (x - m)) / (n - 1)), n);
    }
    double D3Corr(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        int n = a.Count;
        if (n < 2) return double.NaN;
        double ma = a.Average(), mb = b.Average();
        double sa = 0, sb = 0, sab = 0;
        for (int i = 0; i < n; i++) { double x = a[i] - ma, y = b[i] - mb; sa += x * x; sb += y * y; sab += x * y; }
        return (sa <= 0 || sb <= 0) ? double.NaN : sab / Math.Sqrt(sa * sb);
    }
    double D3Mid(IEnumerable<double> xs) { var a = xs.ToArray(); return a.Count(x => x >= 5 && x <= 95) * 100.0 / a.Length; }

    // ======================================================================================
    // phase0: 紙の計算（**戦闘を1回も回さない**）
    // ======================================================================================
    if (d3Arg == "phase0")
    {
        const int D3Sim = 100000;
        Console.WriteLine("# 第71期 Phase 0 —— 測る前に紙で計算する（3つの選択規則）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** 第70期の紙の計算は実測と 0.5 ポイント以内で一致した");
        Console.WriteLine("（編成の中身は紙で出せる。勝率は出せない）——同じやり方で先に出す。");
        Console.WriteLine();

        // ---- 0-1. ロスターのキー保有 ----
        Console.WriteLine("## 0-1. ロスターのキー保有（`UnitCatalog.All` 51 体・`TraitKeyMap.KeysOf`）");
        Console.WriteLine();
        int noKey = d3Roster.Count(u => d3KeyOf[u.Id].Length == 0);
        Console.WriteLine($"- **キーを1つも持たない駒: {noKey} / {d3RN} 体**（規則 S から見ると「噛まない駒」）");
        Console.WriteLine($"- 1駒あたりのキー数: 平均 **{d3Roster.Average(u => d3KeyOf[u.Id].Length):F2}** "
                          + $"/ 最大 {d3Roster.Max(u => d3KeyOf[u.Id].Length)}");
        int nStrong0 = d3Roster.Count(u => u.Attack >= D3Strong);
        Console.WriteLine($"- 攻撃力 {D3Strong} 以上: **{nStrong0} / {d3RN} 体（{nStrong0 * 100.0 / d3RN:F1}%）**（規則 P の分母）");
        Console.WriteLine();
        Console.WriteLine("| キー | 保持する駒の数 | ロスター比 | 無作為5枚での期待枚数 |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int k = 0; k < UnitTally.CarryKeys.Length; k++)
        {
            int n = d3Roster.Count(u => d3KeyOf[u.Id].Contains(k));
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {n} | {n * 100.0 / d3RN:F1}% | {n * 5.0 / d3RN:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine("**無作為5枚で「同じキーの2枚目」が来る確率は、上の期待枚数がどれも 1 前後であること**");
        Console.WriteLine("**からして高くない**——S が伸ばせる上限はロスターの側で決まっている。");
        Console.WriteLine();

        // ---- 0-2. 3規則の抽選（10 万回・戦闘0回） ----
        Console.WriteLine($"## 0-2. P1 —— 3規則の編成の中身（抽選 {D3Sim:N0} 回・**戦闘0回**）");
        Console.WriteLine();
        var dist = new int[D3R][];
        var sAtk = new double[D3R]; var sHp = new double[D3R];
        var sPair = new double[D3R]; var sPal = new double[D3R]; var sNoKey = new double[D3R];
        for (int r = 0; r < D3R; r++) dist[r] = new int[6];
        for (int i = 0; i < D3Sim; i++)
            for (int r = 0; r < D3R; r++)
            {
                var u = D3Team(r, i);
                dist[r][u.Count(x => x.Attack >= D3Strong)]++;
                sAtk[r] += u.Sum(x => x.Attack); sHp[r] += u.Sum(x => x.MaxHp);
                sPair[r] += D3Pairs(u); sPal[r] += D3HasPal(u);
                sNoKey[r] += u.Count(x => d3KeyOf[x.Id].Length == 0);
            }
        Console.WriteLine($"| 規則 | 攻{D3Strong}以上 0枚 | 1 | 2 | 3 | 4 | 5 | 期待値 | 総攻 | 総HP | **共有キーの組数** | 相方あり率 | キー無し/編成 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int r = 0; r < D3R; r++)
        {
            Console.Write($"| {d3RuleName[r]} |");
            for (int k = 0; k <= 5; k++) Console.Write($" {dist[r][k] * 100.0 / D3Sim:F1}% |");
            Console.WriteLine($" **{dist[r].Select((c, k) => (double)c * k).Sum() / D3Sim:F2}** "
                              + $"| {sAtk[r] / D3Sim:F1} | {sHp[r] / D3Sim:F0} | **{sPair[r] / D3Sim:F2}** "
                              + $"| {sPal[r] / (D3Sim * 5) * 100:F1}% | {sNoKey[r] / D3Sim:F2} |");
        }
        Console.WriteLine();

        // ---- 0-3. 超幾何との照合（N の検算） ----
        double Comb(int n, int r2)
        {
            if (r2 < 0 || r2 > n) return 0;
            double v = 1;
            for (int k = 0; k < r2; k++) v = v * (n - k) / (k + 1);
            return v;
        }
        Console.WriteLine("## 0-3. 規則 N は「無作為5枚」と分布が一致する（器具の検算）");
        Console.WriteLine();
        Console.WriteLine("残りから一様に3枚出して、その中から一様に1枚選ぶのは、**残りから一様に1枚選ぶのと同じ**");
        Console.WriteLine("（対称性。選ばなかった2枚は候補に戻る）。よって N の分布は超幾何と一致するはず:");
        Console.WriteLine();
        Console.WriteLine($"| 攻{D3Strong}以上の枚数 | 0 | 1 | 2 | 3 | 4 | 5 | 期待値 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        Console.Write("| 超幾何（厳密） |");
        double ev = 0;
        for (int k = 0; k <= 5; k++)
        {
            double pk = Comb(nStrong0, k) * Comb(d3RN - nStrong0, 5 - k) / Comb(d3RN, 5);
            ev += k * pk;
            Console.Write($" {pk * 100:F1}% |");
        }
        Console.WriteLine($" **{ev:F2}** |");
        Console.Write($"| 規則 N（シム {D3Sim:N0} 回） |");
        for (int k = 0; k <= 5; k++) Console.Write($" {dist[0][k] * 100.0 / D3Sim:F1}% |");
        Console.WriteLine($" **{dist[0].Select((c, k) => (double)c * k).Sum() / D3Sim:F2}** |");
        Console.WriteLine();
        Console.WriteLine("**規則 P の行が第70期 Phase 0 の「選択（C / D・シム）」と一致することも検算になる**");
        Console.WriteLine("（提示の系列も手順も同一にしてある。第70期の値は 期待値 3.07 / 総攻 48.9 / 総HP 376）。");
        Console.WriteLine();

        // ---- 0-4. 理想61行の同じ指標（分母） ----
        var idealRows0 = CompareBuilds();
        double idAtk = idealRows0.Average(b => b.F.Occupied().Sum(o => o.Def.Attack));
        double idHp = idealRows0.Average(b => b.F.Occupied().Sum(o => o.Def.MaxHp));
        var idealTeams = idealRows0.Select(b => b.F.Occupied().Select(o => o.Def).ToArray()).ToArray();
        var idealFive = idealTeams.Where(t => t.Length == 5).ToArray();
        double idPair = idealFive.Average(D3Pairs);
        double idPal = idealFive.Average(t => D3HasPal(t) / 5.0) * 100;
        Console.WriteLine("## 0-4. 分母 —— 理想編成 61 行の同じ指標");
        Console.WriteLine();
        Console.WriteLine("**今回の分母は『共有キーの組数』**（第70期に総攻・総HP は理想と同じと分かったため）。");
        Console.WriteLine($"5枠が埋まっている行は {idealFive.Length} / {idealRows0.Length}（`D3Pairs` / `D3HasPal` はその行だけで平均）。");
        Console.WriteLine();
        Console.WriteLine("| 母集団 | 総攻 | 総HP | **共有キーの組数** | 相方あり率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        Console.WriteLine($"| **理想編成 {idealRows0.Length} 行** | **{idAtk:F1}** | **{idHp:F0}** | **{idPair:F2}** | **{idPal:F1}%** |");
        for (int r = 0; r < D3R; r++)
            Console.WriteLine($"| 規則 {d3RuleShort[r]} | {sAtk[r] / D3Sim:F1} | {sHp[r] / D3Sim:F0} "
                              + $"| {sPair[r] / D3Sim:F2} | {sPal[r] / (D3Sim * 5) * 100:F1}% |");
        Console.WriteLine();
        Console.WriteLine("| 量 | N | P | S | 理想 61 行 | **S が埋めた割合（S−P）÷（理想−P）** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        double den = idPair - sPair[1] / D3Sim;
        Console.WriteLine($"| 共有キーの組数 | {sPair[0] / D3Sim:F2} | {sPair[1] / D3Sim:F2} | {sPair[2] / D3Sim:F2} | {idPair:F2} "
                          + $"| {(Math.Abs(den) < 1e-9 ? "—" : ((sPair[2] - sPair[1]) / D3Sim / den * 100).ToString("F0") + "%")} |");
        Console.WriteLine();
        Console.WriteLine("**これは編成の中身の話で、勝率の話ではない**（第70期 §8）。");
        Console.WriteLine("**噛み合わせの枚数がどこまで戻るかは紙で出せるが、それが何 pt になるかは戦闘でしか出ない。**");
        Console.WriteLine();

        // ---- 0-5. N と M ----
        Console.WriteLine("## 0-5. N と M（**第70期と同じ条件・同じ標本数を据え置く**）");
        Console.WriteLine();
        Console.WriteLine("指示書 §1-4 の条件は「中間帯の割合の推定が ±2% 未満」:");
        Console.WriteLine();
        Console.WriteLine("    半幅 = 1.96 × √(p(1−p)/N) ≤ 2.0%  →  最悪の p = 0.5 で N ≥ 2401");
        Console.WriteLine();
        Console.WriteLine($"**N = {D3N:N0} / M = {D3M} を据え置く**（第69・70期と同じ）。理由は精度ではなく比較可能性:");
        Console.WriteLine();
        Console.WriteLine("1. **規則 P は第70期の C/D と1標本も違わない**ので、表A の P 版が第70期 表A の C/D と");
        Console.WriteLine("   一致することが器具の検算になる（N も M も系列も同じでないと成り立たない）");
        Console.WriteLine("2. 表D・表E は駒ごと・キーごとに分母が割れる");
        Console.WriteLine($"3. 中間帯の半幅（N = {D3N:N0}・最悪の p = 0.5）: **±{D3Z * Math.Sqrt(0.25 / D3N) * 100:F2}%**");
        Console.WriteLine();
        long mainB = (long)D3N * D3V * d3W * D3M;
        Console.WriteLine($"主表1帯の戦闘数: **{mainB:N0}**（{D3N:N0} 標本 × {D3V} 版 × {d3W} 波 × seed {D3M} 本）。");
        Console.WriteLine($"表D は1版ぶんで **+{(long)D3N * 5 * (d3W - 1) * D3M:N0}**、"
                          + $"理想61行の測り直しが **+{(long)(idealRows0.Length * d3W + 305 * (d3W - 1)) * 200:N0}**。");
        Console.WriteLine();

        // ---- 0-6. docs/balance.md の現在値 ----
        Console.WriteLine("## 0-6. `docs/balance.md` の現在値（Q6 の控え）");
        Console.WriteLine();
        string bpath = System.IO.File.Exists("docs/balance.md") ? "docs/balance.md" : "../docs/balance.md";
        if (System.IO.File.Exists(bpath))
        {
            var lines = System.IO.File.ReadAllLines(bpath)
                .Where(l => l.StartsWith("| ") && !l.StartsWith("| 編成") && !l.StartsWith("|---")).ToArray();
            var sums = new double[d3W]; int rows = 0;
            foreach (string l in lines)
            {
                var c = l.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                if (c.Length < d3W + 1) continue;
                bool ok = true;
                var vals = new double[d3W];
                for (int w = 0; w < d3W; w++)
                    if (!double.TryParse(c[w + 1].TrimEnd('%'), out vals[w])) { ok = false; break; }
                if (!ok) continue;
                for (int w = 0; w < d3W; w++) sums[w] += vals[w];
                rows++;
            }
            Console.WriteLine("| 行数 | セル | " + string.Join(" | ", Enumerable.Range(1, d3W).Select(w => $"第{w}波の平均")) + " | 第2〜5波の平均 |");
            Console.WriteLine("|--:|--:|" + string.Concat(Enumerable.Repeat("--:|", d3W + 1)));
            Console.WriteLine($"| **{rows}** | **{rows * d3W}** | "
                              + string.Join(" | ", Enumerable.Range(0, d3W).Select(w => $"{sums[w] / Math.Max(1, rows):F1}%"))
                              + $" | **{sums.Skip(1).Sum() / Math.Max(1, rows * (d3W - 1)):F1}%** |");
        }
        else Console.WriteLine("（`docs/balance.md` が見つからない。作業ディレクトリを確認すること）");
        Console.WriteLine();
        Console.WriteLine("**第2〜5波の理想編成の平均が、回収量の分母である**（第70期の 35pt は5波の値）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {d3Sw.Elapsed.TotalSeconds:F1} 秒（**戦闘 0 回**）。");
        return;
    }

    // ======================================================================================
    // check: 陰性対照（Q6）
    // ======================================================================================
    if (d3Arg == "check")
    {
        Console.WriteLine("# 第71期 —— 陰性対照（Q6）");
        Console.WriteLine();
        Console.WriteLine("## (1) `Stages` を書き換えていないこと");
        Console.WriteLine();
        Console.WriteLine("弱い波は診断のローカルの複製で、`EnemyCatalog.Stages` は読むだけ。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 体数 | HP（既存→弱） | 攻の一致 | 速の一致 | 特性の一致 | 型の一致 | 席の一致 |");
        Console.WriteLine("|---|--:|---|:-:|:-:|:-:|:-:|:-:|");
        for (int w = 0; w < d3W; w++)
        {
            var a = d3Base[w].Enemy.Occupied().ToArray();
            var b = d3Weak[w].Enemy.Occupied().ToArray();
            bool atk = a.Length == b.Length && a.Zip(b).All(z => z.First.Def.Attack == z.Second.Def.Attack);
            bool spd = a.Zip(b).All(z => z.First.Def.Speed == z.Second.Def.Speed);
            bool tr = a.Zip(b).All(z => z.First.Def.Traits.SequenceEqual(z.Second.Def.Traits));
            bool pt = a.Zip(b).All(z => z.First.Def.Pattern == z.Second.Def.Pattern);
            bool sl = a.Zip(b).All(z => z.First.Slot == z.Second.Slot);
            Console.WriteLine($"| {d3Base[w].Name} | {a.Length} | {a.Sum(x => x.Def.MaxHp)} → {b.Sum(x => x.Def.MaxHp)} "
                              + $"| {(atk ? "○" : "×")} | {(spd ? "○" : "×")} | {(tr ? "○" : "×")} "
                              + $"| {(pt ? "○" : "×")} | {(sl ? "○" : "×")} |");
        }
        Console.WriteLine();

        Console.WriteLine("## (2) 規則 P が第70期の C/D と1標本も違わないこと");
        Console.WriteLine();
        Console.WriteLine("第70期 `draft2` の `D2DraftTeam` を**この節にだけ書き写して**突き合わせる");
        Console.WriteLine("（`draft2` の関数はあちらのブロックの中にあるので呼べない。**照合のための写しであり、");
        Console.WriteLine("測定には使わない**）。1件でも食い違えば、表A の P 版を第70期と比べられない。");
        Console.WriteLine();
        UnitDef[] D3P70Team(int i)
        {
            var rng = new Random(2_000_000 + i);
            var idx = new int[d3RN];
            for (int k = 0; k < d3RN; k++) idx[k] = k;
            int remain = d3RN, strong = 0;
            var picked = new UnitDef[5];
            for (int r = 0; r < 5; r++)
            {
                var offer = new UnitDef[3];
                for (int t = 0; t < 3; t++)
                {
                    int j = t + rng.Next(remain - t);
                    (idx[t], idx[j]) = (idx[j], idx[t]);
                    offer[t] = d3Roster[idx[t]];
                }
                UnitDef pick = strong < 2
                    ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                    : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
                picked[r] = pick;
                if (pick.Attack >= D3Strong) strong++;
                int pi = 0;
                for (int t = 0; t < 3; t++) if (ReferenceEquals(d3Roster[idx[t]], pick)) { pi = t; break; }
                (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
                remain--;
            }
            return picked;
        }
        int pMis = 0;
        for (int i = 0; i < 5000; i++)
        {
            var a = D3Team(1, i); var b = D3P70Team(i);
            if (!Enumerable.Range(0, 5).All(k => ReferenceEquals(a[k], b[k]))) pMis++;
        }
        Console.WriteLine($"- **P と第70期の写しの食い違い: {pMis} 件 / 5000**");
        Console.WriteLine();

        Console.WriteLine("## (3) 3規則が規則どおりに動くこと");
        Console.WriteLine();
        Console.WriteLine("**重複なし**（5枚がすべて別の駒）／**S が提示の中で共有キー最大の駒を選んでいること**／");
        Console.WriteLine("**S の1枚目が共有キー 0 で攻撃力最大の駒であること**（指示書 §2-1 の但し書き）を数える。");
        Console.WriteLine();
        int dup = 0, sViol = 0, sFirstAtk = 0, nDet = 0;
        var pairDist = new int[D3R][];
        for (int r = 0; r < D3R; r++) pairDist[r] = new int[11];
        for (int i = 0; i < 5000; i++)
        {
            for (int r = 0; r < D3R; r++)
            {
                var u = D3Team(r, i);
                if (u.Select(x => x.Id).Distinct().Count() != 5) dup++;
                pairDist[r][D3Pairs(u)]++;
            }
            // S の選択が規則どおりか（提示を再現して照合する）
            {
                var rng = new Random(D3OfferSeed + i);
                var idx = new int[d3RN];
                for (int k = 0; k < d3RN; k++) idx[k] = k;
                int remain = d3RN;
                var keys = new HashSet<int>();
                var team = D3Team(2, i);
                for (int r = 0; r < 5; r++)
                {
                    var offer = new UnitDef[3];
                    for (int t = 0; t < 3; t++)
                    {
                        int j = t + rng.Next(remain - t);
                        (idx[t], idx[j]) = (idx[j], idx[t]);
                        offer[t] = d3Roster[idx[t]];
                    }
                    int best0 = offer.Max(x => d3KeyOf[x.Id].Count(k => keys.Contains(k)));
                    if (d3KeyOf[team[r].Id].Count(k => keys.Contains(k)) != best0) sViol++;
                    if (r == 0 && team[0].Attack != offer.Max(x => x.Attack)) sFirstAtk++;
                    foreach (int k in d3KeyOf[team[r].Id]) keys.Add(k);
                    int pi = 0;
                    for (int t = 0; t < 3; t++) if (ReferenceEquals(d3Roster[idx[t]], team[r])) { pi = t; break; }
                    (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
                    remain--;
                }
            }
            // 決定性（N は乱数を使うが、同じ標本番号なら同じ編成になること）
            {
                var a = D3Team(0, i); var b = D3Team(0, i);
                if (!Enumerable.Range(0, 5).All(k => ReferenceEquals(a[k], b[k]))) nDet++;
            }
        }
        Console.WriteLine($"- 重複のある標本 **{dup} 件 / {5000 * D3R}**");
        Console.WriteLine($"- **S が共有キー最大以外を選んだ手 {sViol} 件 / {5000 * 5}**");
        Console.WriteLine($"- **S の1枚目が提示の最大攻撃力でなかった標本 {sFirstAtk} 件 / 5000**（1枚目は共有キーが必ず 0）");
        Console.WriteLine($"- 同じ標本番号で編成が変わった件数 **{nDet} 件 / 5000**（決定性）");
        Console.WriteLine();
        Console.WriteLine("| 共有キーの組数 | " + string.Join(" | ", Enumerable.Range(0, 11)) + " | 平均 |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", 12)));
        for (int r = 0; r < D3R; r++)
            Console.WriteLine($"| 規則 {d3RuleShort[r]} | "
                              + string.Join(" | ", pairDist[r].Select(c => $"{c * 100.0 / 5000:F1}%"))
                              + $" | **{pairDist[r].Select((c, k) => (double)c * k).Sum() / 5000:F2}** |");
        Console.WriteLine();

        Console.WriteLine("## (4) 盤面");
        Console.WriteLine();
        Console.WriteLine("`draft3` は `BattleEngine.Run` を読むだけで、`Traits.cs` / `UnitCatalog` / `Stages` /");
        Console.WriteLine("`CompareBuilds()` を1行も動かしていない。`compare` / `dump` / `engage` の再生成と");
        Console.WriteLine("`docs/` の diff は別に取る。");
        Console.WriteLine();
        Console.WriteLine($"所要 {d3Sw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ======================================================================================
    // 主表（表A〜E）
    // ======================================================================================
    {
        bool alt = d3Arg == "alt";
        int band = alt ? D3BandB : D3BandA;
        string bandName = alt ? "B" : "A";

        var d3Full = new int[D3N][];     // [標本][版 * 波数 + 波] 勝数
        var d3Mem = new int[D3N][];      // [標本][規則 * 5 + k] 抽選された駒の添字
        int done = 0;
        Console.Error.Write($"{bandName}帯 主表: ");
        Parallel.For(0, D3N, i =>
        {
            var fw = new int[D3V * d3W];
            var mm = new int[D3R * 5];
            var teams = new UnitDef[D3R][];
            var seats = new int[D3R][];
            for (int r = 0; r < D3R; r++)
            {
                teams[r] = D3Team(r, i);
                seats[r] = D3Seats(teams[r]);
                for (int k = 0; k < 5; k++) mm[r * 5 + k] = Array.IndexOf(d3Roster, teams[r][k]);
            }
            for (int r = 0; r < D3R; r++)
            {
                Formation f = D3Form(teams[r], seats[r], -1, null);
                for (int wk = 0; wk < 2; wk++)
                {
                    int ver = r * 2 + wk;
                    for (int w = 0; w < d3W; w++)
                        for (int seed = band; seed < band + D3M; seed++)
                            if (BattleEngine.Run(f, D3Enemy(wk, w), seed, verbose: false).PlayerWon)
                                fw[ver * d3W + w]++;
                }
            }
            d3Full[i] = fw; d3Mem[i] = mm;
            int c = Interlocked.Increment(ref done);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        // **主の物差しは第2〜5波**（指示書 §0-3）。5波版は併記のみ。
        double D3Rate25(int i, int ver)
        {
            int s = 0;
            for (int w = 1; w < d3W; w++) s += d3Full[i][ver * d3W + w];
            return s * 100.0 / ((d3W - 1) * D3M);
        }
        double D3Rate5(int i, int ver)
        {
            int s = 0;
            for (int w = 0; w < d3W; w++) s += d3Full[i][ver * d3W + w];
            return s * 100.0 / (d3W * D3M);
        }
        var rate = new double[D3V][];    // 第2〜5波（判定）
        var rate5 = new double[D3V][];   // 5波（併記）
        for (int v = 0; v < D3V; v++)
        {
            rate[v] = Enumerable.Range(0, D3N).Select(i => D3Rate25(i, v)).ToArray();
            rate5[v] = Enumerable.Range(0, D3N).Select(i => D3Rate5(i, v)).ToArray();
        }

        Console.WriteLine($"# 第71期 —— ドラフトで取り戻せるシナジー（{bandName} 帯 seed {band}..{band + D3M - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 draft3{(alt ? " alt" : "")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"標本 **{D3N:N0}** × 版 **{D3V}**（3規則 × 2波）× {d3W} 波 × seed **{D3M}** 本 "
                          + $"= **{(long)D3N * D3V * d3W * D3M:N0} 戦**。");
        Console.WriteLine("配置は**規則配置（H）のみ**。**判定は第2〜5波**（第一波は実行して除外・5波版は併記）。");
        Console.WriteLine($"弱い波は敵の `MaxHp` を **{D3WeakPct}%**（切り捨て）にした複製で、**他の列は1つも動かしていない**。");
        Console.WriteLine();

        // ---- 表A ----
        Console.WriteLine("## 表A —— 6版 × 波 の分布");
        Console.WriteLine();
        Console.WriteLine("| 版 | 対象 | 平均 | =0% | =100% | **中間帯 5〜95%** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int v = 0; v < D3V; v++)
        {
            for (int w = 0; w < d3W; w++)
            {
                var xs = Enumerable.Range(0, D3N).Select(i => d3Full[i][v * d3W + w] * 100.0 / D3M).ToArray();
                Console.WriteLine($"| {(w == 0 ? D3VerName(v) : D3VerShort(v))} | 第{w + 1}波 | {xs.Average():F2}% "
                                  + $"| {xs.Count(x => x == 0) * 100.0 / D3N:F1}% | {xs.Count(x => x == 100) * 100.0 / D3N:F1}% "
                                  + $"| {D3Mid(xs):F1}% |");
            }
            Console.WriteLine($"| {D3VerShort(v)} | **第2〜5波（判定に使う）** | **{rate[v].Average():F2}%** "
                              + $"| **{rate[v].Count(x => x == 0) * 100.0 / D3N:F1}%** | **{rate[v].Count(x => x == 100) * 100.0 / D3N:F1}%** "
                              + $"| **{D3Mid(rate[v]):F1}%** |");
            Console.WriteLine($"| {D3VerShort(v)} | 5波（**併記**・判定に使わない） | {rate5[v].Average():F2}% "
                              + $"| {rate5[v].Count(x => x == 0) * 100.0 / D3N:F1}% | {rate5[v].Count(x => x == 100) * 100.0 / D3N:F1}% "
                              + $"| {D3Mid(rate5[v]):F1}% |");
        }
        Console.WriteLine();
        var mid = Enumerable.Range(0, D3V).Select(v => D3Mid(rate[v])).ToArray();
        var mid5 = Enumerable.Range(0, D3V).Select(v => D3Mid(rate5[v])).ToArray();
        double half = D3Z * Math.Sqrt(0.25 / D3N) * 100;
        int best = Array.IndexOf(mid, mid.Max());
        bool anyPass = mid.Any(x => x >= 60);
        Console.WriteLine($"**Q1**: 第2〜5波の中間帯が 60% 以上の版は **{mid.Count(x => x >= 60)} 個**"
                          + $"（最大は {D3VerShort(best)} の **{mid[best]:F1}%**・割合の 95% 信頼区間は ±{half:F2}%）"
                          + $" → **{(anyPass ? D3VerShort(best) + " を採る" : "×（使える台は無い）")}**");
        Console.WriteLine();
        Console.WriteLine("**(a') 自己検査**: 判定に使う第2〜5波の各波の平均は "
                          + string.Join(" / ", Enumerable.Range(0, D3V).Select(v =>
                              $"{D3VerShort(v)} " + string.Join(",", Enumerable.Range(1, d3W - 1).Select(w =>
                                  (Enumerable.Range(0, D3N).Average(i => d3Full[i][v * d3W + w] * 100.0 / D3M)).ToString("F0")))))
                          + " ——**飽和した波（第一波）は集計に入っていない**。");
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B —— 回収量（主判定 Q2）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 規則 | 中間帯（第2〜5波） | 平均勝率（第2〜5波） | 中間帯（5波・併記） | 平均勝率（5波・併記） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int wk = 0; wk < 2; wk++)
            for (int r = 0; r < D3R; r++)
            {
                int v = r * 2 + wk;
                Console.WriteLine($"| {d3WkName[wk]} | {d3RuleShort[r]} | **{mid[v]:F1}%** | **{rate[v].Average():F2}%** "
                                  + $"| {mid5[v]:F1}% | {rate5[v].Average():F2}% |");
            }
        Console.WriteLine();
        Console.WriteLine("| 波 | 量 | 中間帯（第2〜5波） | 平均勝率（第2〜5波） | 中間帯（5波） | 平均勝率（5波） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int wk = 0; wk < 2; wk++)
        {
            int vn = 0 * 2 + wk, vp = 1 * 2 + wk, vs = 2 * 2 + wk;
            Console.WriteLine($"| {d3WkName[wk]} | **P − N**（攻撃力を数える価値） | {D3P2(mid[vp] - mid[vn])} "
                              + $"| {D3P2(rate[vp].Average() - rate[vn].Average())} "
                              + $"| {D3P2(mid5[vp] - mid5[vn])} | {D3P2(rate5[vp].Average() - rate5[vn].Average())} |");
            Console.WriteLine($"| {d3WkName[wk]} | **S − P（回収量・主判定）** | **{D3P2(mid[vs] - mid[vp])}** "
                              + $"| **{D3P2(rate[vs].Average() - rate[vp].Average())}** "
                              + $"| {D3P2(mid5[vs] - mid5[vp])} | {D3P2(rate5[vs].Average() - rate5[vp].Average())} |");
            Console.WriteLine($"| {d3WkName[wk]} | S − N（両方の合計） | {D3P2(mid[vs] - mid[vn])} "
                              + $"| {D3P2(rate[vs].Average() - rate[vn].Average())} "
                              + $"| {D3P2(mid5[vs] - mid5[vn])} | {D3P2(rate5[vs].Average() - rate5[vn].Average())} |");
        }
        Console.WriteLine();

        // 理想61行（同じ実行の中で測り直す。**回収量の分母**）
        var idealRows = CompareBuilds();
        var idWinW = new int[idealRows.Length][];
        Parallel.For(0, idealRows.Length, r =>
        {
            var ww = new int[d3W];
            for (int w = 0; w < d3W; w++)
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(idealRows[r].F, d3Base[w].Enemy, seed, verbose: false).PlayerWon) ww[w]++;
            idWinW[r] = ww;
        });
        double id25 = idWinW.Average(ww => ww.Skip(1).Sum() * 100.0 / ((d3W - 1) * 200));
        double id5 = idWinW.Average(ww => ww.Sum() * 100.0 / (d3W * 200));
        Console.WriteLine("### 回収量を「理想編成との差」の割合で読む");
        Console.WriteLine();
        Console.WriteLine($"理想編成 {idealRows.Length} 行の平均勝率（既存5波・seed 0..199）: "
                          + $"**第2〜5波 {id25:F2}% / 5波 {id5:F2}%**。");
        Console.WriteLine();
        Console.WriteLine("| 物差し | P（素朴） | 理想 | 差（＝シナジーの総額） | **S − P** | **埋めた割合** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        {
            double gap25 = id25 - rate[2].Average(), rec25 = rate[4].Average() - rate[2].Average();
            double gap5 = id5 - rate5[2].Average(), rec5 = rate5[4].Average() - rate5[2].Average();
            Console.WriteLine($"| 平均勝率・第2〜5波（既存波） | {rate[2].Average():F2}% | {id25:F2}% | {gap25:F2} pt "
                              + $"| **{D3P2(rec25)} pt** | **{rec25 / gap25 * 100:F1}%** |");
            Console.WriteLine($"| 平均勝率・5波（既存波・併記） | {rate5[2].Average():F2}% | {id5:F2}% | {gap5:F2} pt "
                              + $"| {D3P2(rec5)} pt | {rec5 / gap5 * 100:F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine("**分母は既存波でしか意味を持たない**（理想61行は既存波でしか測っていない）ので、");
        Console.WriteLine("弱い波の S − P はこの表に入れない。");
        Console.WriteLine();

        // 波ごとの S − P / P − N
        Console.WriteLine("### 波ごとの内訳（平均勝率）");
        Console.WriteLine();
        Console.WriteLine("| 波の版 | 対象 | N | P | S | P − N | **S − P** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        for (int wk = 0; wk < 2; wk++)
            for (int w = 0; w < d3W; w++)
            {
                double[] m = Enumerable.Range(0, D3R)
                    .Select(r => Enumerable.Range(0, D3N).Average(i => d3Full[i][(r * 2 + wk) * d3W + w] * 100.0 / D3M)).ToArray();
                Console.WriteLine($"| {d3WkName[wk]} | 第{w + 1}波 | {m[0]:F2}% | {m[1]:F2}% | {m[2]:F2}% "
                                  + $"| {D3P2(m[1] - m[0])} | **{D3P2(m[2] - m[1])}** |");
            }
        Console.WriteLine();
        {
            double sp = rate[4].Average() - rate[2].Average();
            double pn = rate[2].Average() - rate[0].Average();
            Console.WriteLine($"**Q2（既存波・平均勝率・第2〜5波）**: S − P = **{D3P2(sp)} pt**");
            Console.WriteLine($"**Q3（既存波）**: P − N = {D3P2(pn)} 対 S − P = {D3P2(sp)} → "
                              + $"**{(sp < pn ? "S − P のほうが小さい（シナジーを狙うことは攻撃力を数えることより価値が低い）" : "S − P のほうが大きい")}**");
            double sp2 = rate[5].Average() - rate[3].Average();
            double pn2 = rate[3].Average() - rate[1].Average();
            Console.WriteLine($"**Q2（弱い波・平均勝率・第2〜5波）**: S − P = **{D3P2(sp2)} pt** / P − N = {D3P2(pn2)}");
        }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C —— 編成の中身（Phase 0 の紙との照合・Q4）");
        Console.WriteLine();
        Console.WriteLine("**波は編成を変えないので行は3つ**（規則ごと）。");
        Console.WriteLine();
        Console.WriteLine($"| 規則 | 攻{D3Strong}以上 0枚 | 1 | 2 | 3 | 4 | 5 | 期待値 | 総攻 | 総HP | **共有キーの組数** | 相方あり率 | キー無し/編成 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var pairMean = new double[D3R];
        for (int r = 0; r < D3R; r++)
        {
            var dist = new int[6];
            double atk = 0, hp = 0, pal = 0, pr = 0, nk = 0;
            for (int i = 0; i < D3N; i++)
            {
                var mem = Enumerable.Range(0, 5).Select(k => d3Roster[d3Mem[i][r * 5 + k]]).ToArray();
                dist[mem.Count(u => u.Attack >= D3Strong)]++;
                atk += mem.Sum(u => u.Attack); hp += mem.Sum(u => u.MaxHp);
                pr += D3Pairs(mem); pal += D3HasPal(mem);
                nk += mem.Count(u => d3KeyOf[u.Id].Length == 0);
            }
            pairMean[r] = pr / D3N;
            Console.Write($"| {d3RuleName[r]} |");
            for (int k = 0; k <= 5; k++) Console.Write($" {dist[k] * 100.0 / D3N:F1}% |");
            Console.WriteLine($" **{dist.Select((c, k) => (double)c * k).Sum() / D3N:F2}** | {atk / D3N:F1} | {hp / D3N:F0} "
                              + $"| **{pr / D3N:F2}** | {pal * 100.0 / (D3N * 5):F1}% | {nk / D3N:F2} |");
        }
        var idealTeams2 = idealRows.Select(b => b.F.Occupied().Select(o => o.Def).ToArray()).Where(t => t.Length == 5).ToArray();
        Console.WriteLine($"| **（参考）理想編成 {idealRows.Length} 行**（5枠 {idealTeams2.Length} 行） | — | — | — | — | — | — | — "
                          + $"| {idealRows.Average(b => b.F.Occupied().Sum(o => o.Def.Attack)):F1} "
                          + $"| {idealRows.Average(b => b.F.Occupied().Sum(o => o.Def.MaxHp)):F0} "
                          + $"| **{idealTeams2.Average(D3Pairs):F2}** | {idealTeams2.Average(t => D3HasPal(t) / 5.0) * 100:F1}% "
                          + $"| {idealTeams2.Average(t => t.Count(u => !d3KeyOf.ContainsKey(u.Id) || d3KeyOf[u.Id].Length == 0)):F2} |");
        Console.WriteLine();
        Console.WriteLine("**Q4**（Phase 0 の紙 10 万回 と 実測 11,000 標本 の共有キーの組数が ±0.5 以内）は報告書側で照合する"
                          + $"（実測: N {pairMean[0]:F2} / P {pairMean[1]:F2} / S {pairMean[2]:F2}）。");
        Console.WriteLine($"**N < P < S の順に増えているか: {(pairMean[0] < pairMean[1] && pairMean[1] < pairMean[2] ? "○" : "×")}**");
        Console.WriteLine();

        // ---- 表E（枚数効果・全6版。**戦闘を1回も足さない集計だけ**） ----
        Console.WriteLine("## 表E —— 枚数効果（キーごと・全6版）");
        Console.WriteLine();
        Console.WriteLine("その標本にそのキーを持つ駒が何枚いたかで分けた平均勝率（**第2〜5波**）。");
        Console.WriteLine("**第70期の「弱い波では燃が3本目になる」が再現するか**を見る。");
        Console.WriteLine("**Q1 を満たした版が判定の対象**だが、集計は戦闘を1回も足さないので全6版を出す。");
        Console.WriteLine();
        for (int v = 0; v < D3V; v++)
        {
            int r0 = v / 2;
            Console.WriteLine($"### {D3VerName(v)}");
            Console.WriteLine();
            Console.WriteLine("| キー | 0枚 | n | 1枚 | n | 2枚 | n | 3枚以上 | n | 2枚 − 1枚 | 1枚 − 0枚 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int k = 0; k < UnitTally.CarryKeys.Length; k++)
            {
                var bk = new List<double>[4];
                for (int b = 0; b < 4; b++) bk[b] = new List<double>();
                for (int i = 0; i < D3N; i++)
                {
                    int c = 0;
                    for (int q = 0; q < 5; q++) if (d3KeyOf[d3Roster[d3Mem[i][r0 * 5 + q]].Id].Contains(k)) c++;
                    bk[Math.Min(c, 3)].Add(rate[v][i]);
                }
                string Cell(int b) => bk[b].Count == 0 ? "—" : $"{bk[b].Average():F2}%";
                string Del(int a2, int b2) => (bk[a2].Count == 0 || bk[b2].Count == 0) ? "—"
                    : D3P2(bk[a2].Average() - bk[b2].Average());
                Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {Cell(0)} | {bk[0].Count} | {Cell(1)} | {bk[1].Count} "
                                  + $"| {Cell(2)} | {bk[2].Count} | {Cell(3)} | {bk[3].Count} "
                                  + $"| **{Del(2, 1)}** | {Del(1, 0)} |");
            }
            Console.WriteLine();
        }

        // ---- 表D ----
        //
        // **どの版で出すかの規則は測る前に固定してある**（自由度 (e) を消すため）:
        //   Q1 を満たした版があれば**その版の波設定**、無ければ**中間帯が最大の版の波設定**を採り、
        //   **その波設定の3規則すべて**を出す。3規則を揃えるのは Q5（S で r が上がるか）が
        //   1版だけでは答えられないため——**結果を見てから対象を選ばない。**
        int showWk = best % 2;
        var showD = Enumerable.Range(0, D3R).Select(r => r * 2 + showWk).ToArray();

        Console.WriteLine("## 表D —— 駒ごとの在席時勝率と寄与");
        Console.WriteLine();
        Console.WriteLine($"出すのは **{string.Join(" / ", showD.Select(D3VerShort))}**"
                          + $"（{(anyPass ? "Q1 を満たした版" : "**Q1 を満たす版が無いので、中間帯が最大の版**")}の波設定＝**{d3WkName[showWk]}**の3規則すべて）。");
        Console.WriteLine("**3規則を揃えて出すのは Q5（S で r が上がるか）が1版だけでは答えられないため**（測る前に固定した規則）。");
        Console.WriteLine();
        Console.WriteLine("**寄与 = 標本の勝率 − その駒を素体（同数値・特性なし）に差し替えた版の勝率。**");
        Console.WriteLine("**器具は素体差し替えに統一**（第69期）。理想61行の側も**同じ器具・同じ集計（第2〜5波・seed 0..199）**で測り直した。");
        Console.WriteLine();

        var idJobs = new List<(int Row, int Unit, int Slot)>();
        for (int r = 0; r < idealRows.Length; r++)
            foreach ((int slot, UnitDef d) in idealRows[r].F.Occupied())
            {
                int ui0 = Array.FindIndex(d3Roster, x => x.Id == d.Id);
                if (ui0 >= 0) idJobs.Add((r, ui0, slot));
            }
        var idPlain25 = new double[idJobs.Count];
        Parallel.For(0, idJobs.Count, j =>
        {
            (int r, int ui, int slot) = idJobs[j];
            var pf = idealRows[r].F.Clone();
            pf[slot] = d3Plain[ui];
            int win = 0;
            for (int w = 1; w < d3W; w++)
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(pf, d3Base[w].Enemy, seed, verbose: false).PlayerWon) win++;
            idPlain25[j] = win * 100.0 / ((d3W - 1) * 200);
        });
        var idSeat = new List<double>[d3RN];
        var idCon = new List<double>[d3RN];
        for (int u = 0; u < d3RN; u++) { idSeat[u] = new List<double>(); idCon[u] = new List<double>(); }
        for (int j = 0; j < idJobs.Count; j++)
        {
            (int r, int ui, _) = idJobs[j];
            double full25 = idWinW[r].Skip(1).Sum() * 100.0 / ((d3W - 1) * 200);
            idSeat[ui].Add(full25);
            idCon[ui].Add(full25 - idPlain25[j]);
        }

        var q5r = new double[D3R];
        foreach (int v in showD)
        {
            int r0 = v / 2;
            var plainWin = new int[D3N][];
            int dn = 0;
            Console.Error.Write($"{bandName}帯 表D({D3VerShort(v)}): ");
            Parallel.For(0, D3N, i =>
            {
                var team = D3Team(r0, i);
                var seats = D3Seats(team);
                var pw = new int[5];
                for (int k = 0; k < 5; k++)
                {
                    var g = D3Form(team, seats, k, d3Plain[d3Mem[i][r0 * 5 + k]]);
                    for (int w = 1; w < d3W; w++)
                        for (int seed = band; seed < band + D3M; seed++)
                            if (BattleEngine.Run(g, D3Enemy(showWk, w), seed, verbose: false).PlayerWon) pw[k]++;
                }
                plainWin[i] = pw;
                int c = Interlocked.Increment(ref dn);
                if (c % 1000 == 0) Console.Error.Write(".");
            });
            Console.Error.WriteLine();

            var seatRate = new List<double>[d3RN];
            var conRate = new List<double>[d3RN];
            for (int u = 0; u < d3RN; u++) { seatRate[u] = new List<double>(); conRate[u] = new List<double>(); }
            for (int i = 0; i < D3N; i++)
                for (int k = 0; k < 5; k++)
                {
                    int u = d3Mem[i][r0 * 5 + k];
                    seatRate[u].Add(rate[v][i]);
                    conRate[u].Add(rate[v][i] - plainWin[i][k] * 100.0 / ((d3W - 1) * D3M));
                }

            Console.WriteLine($"### {D3VerName(v)}");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 攻 | キー | 在席 | 在席時勝率 | ±95% | 寄与 | ±95% | 理想·在席時勝率 | 理想·寄与 | 行数 |");
            Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|");
            var xs1 = new List<double>(); var ys1 = new List<double>();
            var xs2 = new List<double>(); var ys2 = new List<double>();
            foreach (int u in Enumerable.Range(0, d3RN).OrderByDescending(u => D3Stats(conRate[u]).Mean))
            {
                var a = D3Stats(seatRate[u]); var b = D3Stats(conRate[u]);
                string keys = string.Join("・", d3KeyOf[d3Roster[u].Id].Select(k => UnitTally.CarryKeys[k]));
                string idS = idSeat[u].Count > 0 ? $"{idSeat[u].Average():F1}%" : "—";
                string idC = idCon[u].Count > 0 ? D3P1(idCon[u].Average()) : "—";
                if (idSeat[u].Count > 0)
                {
                    xs1.Add(a.Mean); ys1.Add(idSeat[u].Average());
                    xs2.Add(b.Mean); ys2.Add(idCon[u].Average());
                }
                Console.WriteLine($"| {d3Roster[u].Name} | {d3Roster[u].Attack} | {(keys.Length == 0 ? "—" : keys)} | {a.N} "
                                  + $"| {a.Mean:F2}% | ±{D3Z * a.Sd / Math.Sqrt(Math.Max(1, a.N)):F2} "
                                  + $"| **{D3P1(b.Mean)}** | ±{D3Z * b.Sd / Math.Sqrt(Math.Max(1, b.N)):F2} "
                                  + $"| {idS} | {idC} | {idSeat[u].Count} |");
            }
            Console.WriteLine();
            double rSeat = D3Corr(xs1, ys1), rCon = D3Corr(xs2, ys2);
            q5r[r0] = rSeat;
            Console.WriteLine("| 散布（理想61行との相関） | r | n |");
            Console.WriteLine("|---|--:|--:|");
            Console.WriteLine($"| **在席時勝率どうし**（Q5 の字義） | **{rSeat:F3}** | {xs1.Count} |");
            Console.WriteLine($"| 寄与どうし（第69期の 0.354 と比べられる形） | **{rCon:F3}** | {xs2.Count} |");
            Console.WriteLine();
        }
        Console.WriteLine($"**Q5**: 在席時勝率どうしの r は N {q5r[0]:F3} / P {q5r[1]:F3} / S {q5r[2]:F3} → "
                          + $"**{(q5r[2] > q5r[1] ? "S で上がった（S はシナジーを選べている）" : "S で上がらなかった（S はシナジーを選べていない）")}**"
                          + $"{(anyPass ? "" : "。**ただし Q1 を満たす版が無いので参考値**")}");
        Console.WriteLine();

        Console.WriteLine($"所要 {d3Sw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }
}
}
