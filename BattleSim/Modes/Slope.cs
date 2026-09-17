using BattleCore;
using static Common;

// =====================================================================================
// slope モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "slope")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 slope
// =====================================================================================

static class SlopeDiag
{
// slope モード: 良い規則ならシナジーは報われるか（第72期・調査）
//
// 第71期は「共有キーの数を最大化する規則 S」で終わった。**S は噛み合わせを実際に増やした**
// （共有キーの組数 1.59 → 2.89・理想61行の 2.43 を超える／相方あり率 68.7%・理想 58.4% を超える）
// **のに、第2〜5波の8セルすべてで P に負けた**（S − P = −1.47 / −5.65 pt・両 seed 帯で符号一致）。
//
// 理由は第71期の表E に出ている——**枚数効果の傾きは 被弾 +19.4 から 傷 −15.6 まで 35pt の幅があり、
// シナジーは一様ではなく半分は負の傾き。** S はキーの種類を区別しないので、
// **高く付く2本（傷 −15.6 / 痺 −12.6）を 0.26 枚ずつ買い増し、唯一大きく報われる被弾を 0.13 枚手放していた。**
//
//   **壊れた規則ではなく、良い規則ならシナジーは報われるのか。**
//
//   規則 N（無作為）      提示3枚から乱数で1枚。**下限**（この規則だけ選択にも乱数を使う）
//   規則 P（素朴）        第70期の C/D・第71期の P と同一。攻7以上が2枚未満なら攻撃力最大／以後は最大HP
//   規則 S（シナジー志向） 第71期と同一。すでに選んだ駒と共有するキーの数が最大 → 攻撃力 → Id 辞書順
//   規則 S'（傾き志向）    **提示の駒が持つキーの傾きの合計が最大** → 攻撃力 → Id 辞書順。
//                        **すでに選んだ駒を見ない**——S は「共有」を見て保持者数の多いキーへ引きずられた。
//                        S' は**キーの値段だけ**を見る。
//
// **傾きは第71期 表E（弱い波・規則 Pw・A 帯）の値を定数として焼いてある**（`D4Slope100`）。
// **この期の実測から作らない**——出どころを固定しないと「測った傾きで選んで測り直す」が
// 版ごとに別の傾きを使うことになる。**循環そのものは承知の上で採る。得られるのは上限であって予測ではない。**
// A 帯の実測 Pw 傾きがこの定数を再現することを主表の D-3 で検算する（同じ標本・同じ seed なので一致するはず）。
//
// **4規則 × 2波（既存 / 弱い波＝敵の MaxHp 60%）= 8版。** 配置は規則配置（H）のみ。
// **`Stages` は書き換えない**（弱い波は診断のローカル。`gradient` / `aim` と同じ扱い）。
// **物差しは第2〜5波**（第70期の訂正。第一波は実行して除外・5波版は併記）。
//
//     dotnet run --project BattleSim -c Release 0 slope phase0  # 紙の計算（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 slope         # 主表（表A〜E）・A 帯 seed 0..7
//     dotnet run --project BattleSim -c Release 0 slope alt     # 同じ標本を B 帯（seed 200..207）で
//     dotnet run --project BattleSim -c Release 0 slope check   # 陰性対照（Q6）
public static void Run(string[] args, int stageIndex)
{
    string d4Arg = args.Length > 2 ? args[2] : "";
    IReadOnlyList<EnemyCatalog.Stage> d4Base = EnemyCatalog.Stages;
    int d4W = d4Base.Count;
    var d4Sw = System.Diagnostics.Stopwatch.StartNew();
    var d4Roster = UnitCatalog.All.ToArray();
    int d4RN = d4Roster.Length;                       // 51
    // **第141期: キー表は `Everyone` で引く**（`draft3` と同じ。理想61行にエグが残っている）。
    var d4KeyOf = UnitCatalog.Everyone.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);
    int d4K = UnitTally.CarryKeys.Length;             // 11

    // ---- 測る前に固定した定数 ------------------------------------------------------------
    //
    // **提示の系列は4規則で共有する**（`D4OfferSeed` は第70・71期と同じ 2,000,000）。
    // P は第70期の `D2DraftTeam` / 第71期の `D3Team(1, ·)` と系列も手順も同一なので、
    // **P の編成は1体も違わない**——器具の検算になる（`check` の (2)）。
    // **N の「選ぶ」乱数も同じ系列から引く**（第71期の前提の訂正 1。近い seed の `Random` を
    // 2本並走させると抽選が偏る——攻7以上の期待枚数が超幾何の 2.25 に対して 2.36 になった）。
    // **P / S / S' は1手あたり乱数を3回しか引かない**ので、提示の系列は3規則で完全に同じ。
    const int D4OfferSeed = 2_000_000;
    const int D4BandA = 0, D4BandB = 200;
    const int D4M = 8;                 // 1標本あたりの戦闘 seed 数（第69〜71期と同じ）
    const int D4N = 11000;             // 標本数（第69〜71期と同じ）
    const int D4Strong = 7;            // 規則 P が使う「殴れる駒」の線
    const int D4WeakPct = 60;          // 弱い波: 敵の MaxHp をこの % に（切り捨て）。第70・71期と同一
    const double D4Z = 1.96;
    const int D4R = 4, D4V = 8;        // 規則4 × 波2 = 版8。ver = rule * 2 + wk

    // **傾き（1/100 単位の整数）** = 第71期 表E「Pw（素朴 × 弱い波）・A 帯」の
    // （1枚 − 0枚）と（2枚 − 1枚）の平均。並びは `UnitTally.CarryKeys`。
    // 整数で持つのは**同点判定を浮動小数の誤差に委ねないため**（合計が厳密に一致する）。
    //
    //   強化 (+0.90 − 2.69)/2   弱体 (−8.03 − 4.96)/2   毒 (−4.82 + 8.62)/2   燃 (+2.80 + 10.26)/2
    //   痺 (−9.38 − 15.73)/2    標 (−7.04 + 0.45)/2     破片 (−3.31 − 12.60)/2 傷 (−15.84 − 15.30)/2
    //   手番 (−6.79 − 8.38)/2   被弾 (+20.95 + 17.76)/2 移動 (+9.11 + 10.51)/2
    int[] D4Slope100 = { -90, -650, 190, 653, -1256, -330, -796, -1557, -759, 1936, 981 };

    // 第71期 表E の出どころ（（1枚−0枚）, （2枚−1枚））。**検算と phase0 の表示にだけ使う。**
    double[][] D4Src71 = {
        new[] { 0.90, -2.69 }, new[] { -8.03, -4.96 }, new[] { -4.82, 8.62 }, new[] { 2.80, 10.26 },
        new[] { -9.38, -15.73 }, new[] { -7.04, 0.45 }, new[] { -3.31, -12.60 }, new[] { -15.84, -15.30 },
        new[] { -6.79, -8.38 }, new[] { 20.95, 17.76 }, new[] { 9.11, 10.51 },
    };

    string[] d4RuleName = { "N（無作為）", "P（素朴）", "S（シナジー志向）", "S'（傾き志向）" };
    string[] d4RuleBare = { "無作為", "素朴", "シナジー志向", "傾き志向" };
    string[] d4RuleShort = { "N", "P", "S", "S'" };
    string[] d4WkName = { "既存5波", "弱い波" };
    string D4VerName(int v) => $"{d4RuleShort[v / 2]}{(v % 2 == 0 ? "" : "w")}（{d4RuleBare[v / 2]} × {d4WkName[v % 2]}）";
    string D4VerShort(int v) => d4RuleShort[v / 2] + (v % 2 == 0 ? "" : "w");

    // その駒が持つキーの傾きの合計（キーを持たない駒は 0）。**規則 S' の唯一の判断材料。**
    var d4SlopeOf = d4Roster.ToDictionary(u => u.Id, u => d4KeyOf[u.Id].Sum(k => D4Slope100[k]));

    // ---- 弱い波（第70・71期の写し。**HP だけの1変数**） ------------------------------------
    var d4WeakCache = new Dictionary<string, UnitDef>();
    UnitDef D4WeakOf(UnitDef d)
    {
        if (d4WeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * D4WeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        d4WeakCache[d.Id] = w;
        return w;
    }
    var d4Weak = d4Base.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = D4WeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();
    Formation D4Enemy(int wk, int w) => wk == 0 ? d4Base[w].Enemy : d4Weak[w].Enemy;

    // ---- 素体（第69〜71期と同じ構成。**`Actions` は落とす**） ------------------------------
    var d4Plain = d4Roster.Select(d => new UnitDef
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    }).ToArray();

    // ---- 4つの選択規則（**測る前に固定。指示書 §2-1 から1行も増やしていない**） -------------
    UnitDef[] D4Team(int rule, int i)
    {
        var rng = new Random(D4OfferSeed + i);
        var idx = new int[d4RN];
        for (int k = 0; k < d4RN; k++) idx[k] = k;
        int remain = d4RN, strong = 0;
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
                offer[t] = d4Roster[idx[t]];
            }
            UnitDef sel;
            if (rule == 0)
                sel = offer[rng.Next(3)];
            else if (rule == 1)
                sel = strong < 2
                    ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                    : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            else if (rule == 2)
                sel = offer.OrderByDescending(x => d4KeyOf[x.Id].Count(k => keysSoFar.Contains(k)))
                           .ThenByDescending(x => x.Attack)
                           .ThenBy(x => x.Id, StringComparer.Ordinal).First();
            else
                // S': **すでに選んだ駒を見ない。** キーの傾きの合計だけで決める。
                sel = offer.OrderByDescending(x => d4SlopeOf[x.Id])
                           .ThenByDescending(x => x.Attack)
                           .ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= D4Strong) strong++;
            foreach (int k in d4KeyOf[sel.Id]) keysSoFar.Add(k);
            // 選んだ1枚だけを候補から外す
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(d4Roster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }

    // 規則配置 H（第69期 §1-3 の写し。第70・71期と同一）
    int[] D4Seats(UnitDef[] u)
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
    Formation D4Form(UnitDef[] u, int[] seats, int replace, UnitDef? with)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = (k == replace) ? with : u[k];
        return f;
    }

    // ---- 噛み合わせの指標（第71期の写し。**S' が壊れている兆候の判定に使う**） --------------
    int D4Pairs(IReadOnlyList<UnitDef> u)
    {
        int c = 0;
        for (int a = 0; a < u.Count; a++)
            for (int b = a + 1; b < u.Count; b++)
                if (d4KeyOf[u[a].Id].Any(x => d4KeyOf[u[b].Id].Contains(x))) c++;
        return c;
    }
    int D4HasPal(IReadOnlyList<UnitDef> u)
    {
        int c = 0;
        for (int a = 0; a < u.Count; a++)
        {
            bool has = false;
            for (int b = 0; b < u.Count && !has; b++)
                if (b != a && d4KeyOf[u[a].Id].Any(x => d4KeyOf[u[b].Id].Contains(x))) has = true;
            if (has) c++;
        }
        return c;
    }

    // ---- 統計の道具（第70・71期の写し） -----------------------------------------------------
    string D4P1(double x) => double.IsNaN(x) ? "—" : (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    string D4P2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    (double Mean, double Sd, int N) D4Stats(IReadOnlyList<double> xs)
    {
        int n = xs.Count;
        if (n == 0) return (0, 0, 0);
        double m = xs.Average();
        if (n < 2) return (m, 0, n);
        return (m, Math.Sqrt(xs.Sum(x => (x - m) * (x - m)) / (n - 1)), n);
    }
    double D4Corr(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        int n = a.Count;
        if (n < 2) return double.NaN;
        double ma = a.Average(), mb = b.Average();
        double sa = 0, sb = 0, sab = 0;
        for (int i = 0; i < n; i++) { double x = a[i] - ma, y = b[i] - mb; sa += x * x; sb += y * y; sab += x * y; }
        return (sa <= 0 || sb <= 0) ? double.NaN : sab / Math.Sqrt(sa * sb);
    }
    double D4Mid(IEnumerable<double> xs) { var a = xs.ToArray(); return a.Count(x => x >= 5 && x <= 95) * 100.0 / a.Length; }

    // 傾き = （1枚 − 0枚）と（2枚 − 1枚）の平均。**第71期の表E と同じ定義。**
    // 片方の箱が空なら もう片方だけ、両方空なら NaN。
    double D4SlopeOf4(IReadOnlyList<double>[] bk)
    {
        double d10 = (bk[0].Count > 0 && bk[1].Count > 0) ? bk[1].Average() - bk[0].Average() : double.NaN;
        double d21 = (bk[1].Count > 0 && bk[2].Count > 0) ? bk[2].Average() - bk[1].Average() : double.NaN;
        return double.IsNaN(d10) ? d21 : double.IsNaN(d21) ? d10 : (d10 + d21) / 2;
    }

    // ======================================================================================
    // phase0: 紙の計算（**戦闘を1回も回さない**）
    // ======================================================================================
    if (d4Arg == "phase0")
    {
        const int D4Sim = 100000;
        Console.WriteLine("# 第72期 Phase 0 —— 測る前に紙で計算する（規則 S' を足す）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** 第70・71期の紙の計算は実測と ±0.01〜0.5 で一致した");
        Console.WriteLine("（編成の中身は紙で出せる。勝率は出せない）——同じ手順を置く。");
        Console.WriteLine();

        // ---- 0-1. 傾きの出どころ（**測る前に固定**） ----
        Console.WriteLine("## 0-1. 傾きの出どころ（指示書 §2-1・**測る前に固定**）");
        Console.WriteLine();
        Console.WriteLine("    傾き(キー) = 第71期 表E の「1枚 − 0枚」と「2枚 − 1枚」の平均");
        Console.WriteLine("    出どころ  = **弱い波・規則 Pw・A 帯**（`design/PHASE71_DRAFT3.md` §6 の表）");
        Console.WriteLine();
        Console.WriteLine("**この期の実測から作らない。** 定数 `D4Slope100` に焼いてあり、");
        Console.WriteLine("A 帯の実測がこれを再現することは主表の D-3 で検算する（同じ標本・同じ seed）。");
        Console.WriteLine();
        Console.WriteLine("| キー | 1枚 − 0枚 | 2枚 − 1枚 | **傾き（S' が使う値）** | 保持する駒 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (int k in Enumerable.Range(0, d4K).OrderByDescending(k => D4Slope100[k]))
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {D4P2(D4Src71[k][0])} | {D4P2(D4Src71[k][1])} "
                              + $"| **{D4P2(D4Slope100[k] / 100.0)}** | {d4Roster.Count(u => d4KeyOf[u.Id].Contains(k))} |");
        Console.WriteLine();
        Console.WriteLine($"**正のキーは {Enumerable.Range(0, d4K).Count(k => D4Slope100[k] > 0)} 本"
                          + $"（{string.Join("・", Enumerable.Range(0, d4K).Where(k => D4Slope100[k] > 0).Select(k => UnitTally.CarryKeys[k]))}）**、"
                          + $"負は {Enumerable.Range(0, d4K).Count(k => D4Slope100[k] < 0)} 本。");
        Console.WriteLine();

        // ---- 0-2. 駒ごとの傾きの合計（S' の選好は机上で全部読める） ----
        Console.WriteLine("## 0-2. 駒ごとの傾きの合計（**S' の判断材料はこれ1本**）");
        Console.WriteLine();
        Console.WriteLine("S' は提示の3枚をこの値で並べ、同数なら攻撃力、同値なら `Def.Id` の辞書順で選ぶ。");
        Console.WriteLine("**すでに選んだ駒を見ないので、選好はロスターの側で完全に決まっている**——");
        Console.WriteLine("**S' の選好は戦闘を1回も回さずに全部読める。**");
        Console.WriteLine();
        Console.WriteLine("| 順 | 駒 | 攻 | キー | **傾きの合計** |");
        Console.WriteLine("|--:|---|--:|---|--:|");
        int rank = 0;
        foreach (var u in d4Roster.OrderByDescending(x => d4SlopeOf[x.Id]).ThenByDescending(x => x.Attack)
                                  .ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            rank++;
            if (rank > 12 && rank <= d4RN - 8) continue;
            if (rank == d4RN - 7) Console.WriteLine("| … | …（中略） | | | |");
            string keys = string.Join("・", d4KeyOf[u.Id].Select(k => UnitTally.CarryKeys[k]));
            Console.WriteLine($"| {rank} | {u.Name} | {u.Attack} | {(keys.Length == 0 ? "—" : keys)} "
                              + $"| **{D4P2(d4SlopeOf[u.Id] / 100.0)}** |");
        }
        Console.WriteLine();
        int nZero = d4Roster.Count(u => d4KeyOf[u.Id].Length == 0);
        int nPos = d4Roster.Count(u => d4SlopeOf[u.Id] > 0);
        Console.WriteLine($"- **傾きの合計が正の駒: {nPos} / {d4RN} 体**（S' はこの集合を優先して買う）");
        Console.WriteLine($"- キーを1つも持たない駒（合計 0）: **{nZero} / {d4RN} 体**"
                          + $"——**負のキーだけを持つ {d4RN - nPos - nZero} 体より上に来る**");
        Console.WriteLine();

        // ---- 0-3. ロスターのキー保有 ----
        Console.WriteLine("## 0-3. ロスターのキー保有（`UnitCatalog.All` 51 体・`TraitKeyMap.KeysOf`）");
        Console.WriteLine();
        int nStrong0 = d4Roster.Count(u => u.Attack >= D4Strong);
        Console.WriteLine($"- 1駒あたりのキー数: 平均 **{d4Roster.Average(u => d4KeyOf[u.Id].Length):F2}** / 最大 {d4Roster.Max(u => d4KeyOf[u.Id].Length)}");
        Console.WriteLine($"- 攻撃力 {D4Strong} 以上: **{nStrong0} / {d4RN} 体（{nStrong0 * 100.0 / d4RN:F1}%）**（規則 P の分母）");
        Console.WriteLine();
        Console.WriteLine("| キー | 保持する駒 | ロスター比 | 無作為5枚での期待枚数 | 傾き |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int k = 0; k < d4K; k++)
        {
            int n = d4Roster.Count(u => d4KeyOf[u.Id].Contains(k));
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {n} | {n * 100.0 / d4RN:F1}% | {n * 5.0 / d4RN:F2} "
                              + $"| {D4P2(D4Slope100[k] / 100.0)} |");
        }
        Console.WriteLine();

        // ---- 0-4. 4規則の抽選（10 万回・戦闘0回） ----
        Console.WriteLine($"## 0-4. P1 —— 4規則の編成の中身（抽選 {D4Sim:N0} 回・**戦闘0回**）");
        Console.WriteLine();
        var dist = new int[D4R][];
        var sAtk = new double[D4R]; var sHp = new double[D4R];
        var sPair = new double[D4R]; var sPal = new double[D4R]; var sNoKey = new double[D4R];
        var sKey = new double[D4R][];
        for (int r = 0; r < D4R; r++) { dist[r] = new int[6]; sKey[r] = new double[d4K]; }
        for (int i = 0; i < D4Sim; i++)
            for (int r = 0; r < D4R; r++)
            {
                var u = D4Team(r, i);
                dist[r][u.Count(x => x.Attack >= D4Strong)]++;
                sAtk[r] += u.Sum(x => x.Attack); sHp[r] += u.Sum(x => x.MaxHp);
                sPair[r] += D4Pairs(u); sPal[r] += D4HasPal(u);
                sNoKey[r] += u.Count(x => d4KeyOf[x.Id].Length == 0);
                for (int k = 0; k < d4K; k++) sKey[r][k] += u.Count(x => d4KeyOf[x.Id].Contains(k));
            }
        Console.WriteLine($"| 規則 | 攻{D4Strong}以上 0枚 | 1 | 2 | 3 | 4 | 5 | 期待値 | 総攻 | 総HP | **共有キーの組数** | 相方あり率 | キー無し/編成 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int r = 0; r < D4R; r++)
        {
            Console.Write($"| {d4RuleName[r]} |");
            for (int k = 0; k <= 5; k++) Console.Write($" {dist[r][k] * 100.0 / D4Sim:F1}% |");
            Console.WriteLine($" **{dist[r].Select((c, k) => (double)c * k).Sum() / D4Sim:F2}** "
                              + $"| {sAtk[r] / D4Sim:F1} | {sHp[r] / D4Sim:F0} | **{sPair[r] / D4Sim:F2}** "
                              + $"| {sPal[r] / (D4Sim * 5) * 100:F1}% | {sNoKey[r] / D4Sim:F2} |");
        }
        Console.WriteLine();

        Console.WriteLine("### キーごとの保持枚数（**Q3 の予測。これを実測と ±0.5 で照合する**）");
        Console.WriteLine();
        Console.WriteLine("| キー | 傾き | N | P | S | **S'** | **S' − P** | **S' − S** | S − P（第71期） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int k in Enumerable.Range(0, d4K).OrderByDescending(k => D4Slope100[k]))
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {D4P2(D4Slope100[k] / 100.0)} "
                              + $"| {sKey[0][k] / D4Sim:F2} | {sKey[1][k] / D4Sim:F2} | {sKey[2][k] / D4Sim:F2} "
                              + $"| **{sKey[3][k] / D4Sim:F2}** | **{D4P2((sKey[3][k] - sKey[1][k]) / D4Sim)}** "
                              + $"| {D4P2((sKey[3][k] - sKey[2][k]) / D4Sim)} | {D4P2((sKey[2][k] - sKey[1][k]) / D4Sim)} |");
        Console.WriteLine();
        Console.WriteLine("**第71期の S は 傷・痺 を 0.26 枚ずつ買い増し、被弾を 0.13 枚手放していた**（S − P の列）。");
        Console.WriteLine("**S' がその逆を向いているか**（傷・痺 が S より少なく、被弾が多いか）を上の表で読む。");
        Console.WriteLine();
        {
            double addSp = 0, addS = 0;
            for (int k = 0; k < d4K; k++)
            {
                addSp += (sKey[3][k] - sKey[1][k]) / D4Sim * (D4Slope100[k] / 100.0);
                addS += (sKey[2][k] - sKey[1][k]) / D4Sim * (D4Slope100[k] / 100.0);
            }
            Console.WriteLine($"**加法の目安（Σ 枚数差 × 傾き）: S' − P {D4P2(addSp)} pt / S − P {D4P2(addS)} pt**");
            Console.WriteLine("——第71期は S − P で −8.2 と出し、弱い波の実測は −5.65 だった");
            Console.WriteLine("（キーが同じ駒に重なるので二重計上を含む。**符号と桁の予測にだけ使う**）。");
        }
        Console.WriteLine();

        // ---- 0-5. 超幾何との照合（乱数の系列の検算） ----
        double Comb(int n, int r2)
        {
            if (r2 < 0 || r2 > n) return 0;
            double v = 1;
            for (int k = 0; k < r2; k++) v = v * (n - k) / (k + 1);
            return v;
        }
        Console.WriteLine("## 0-5. 乱数の系列の検算（指示書 §1-1・**第71期の欠陥の再発防止**）");
        Console.WriteLine();
        Console.WriteLine("提示も N の選択も**1本の系列**（`Random(2,000,000 + i)`）から順に引いている。");
        Console.WriteLine("残りから一様に3枚出してその中から一様に1枚選ぶのは**残りから一様に1枚選ぶのと同じ**なので、");
        Console.WriteLine("N の分布は超幾何と一致するはず——**ここがずれたら系列が壊れている。**");
        Console.WriteLine();
        Console.WriteLine($"| 攻{D4Strong}以上の枚数 | 0 | 1 | 2 | 3 | 4 | 5 | 期待値 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        Console.Write("| 超幾何（厳密） |");
        double ev = 0;
        for (int k = 0; k <= 5; k++)
        {
            double pk = Comb(nStrong0, k) * Comb(d4RN - nStrong0, 5 - k) / Comb(d4RN, 5);
            ev += k * pk;
            Console.Write($" {pk * 100:F1}% |");
        }
        Console.WriteLine($" **{ev:F2}** |");
        Console.Write($"| 規則 N（シム {D4Sim:N0} 回） |");
        for (int k = 0; k <= 5; k++) Console.Write($" {dist[0][k] * 100.0 / D4Sim:F1}% |");
        double evN = dist[0].Select((c, k) => (double)c * k).Sum() / D4Sim;
        Console.WriteLine($" **{evN:F2}** |");
        Console.WriteLine();
        double seN = Math.Sqrt(5 * (nStrong0 / (double)d4RN) * (1 - nStrong0 / (double)d4RN) / D4Sim);
        Console.WriteLine($"ずれ **{Math.Abs(evN - ev):F4}**（標準誤差の目安 {seN:F4} の **{Math.Abs(evN - ev) / seN:F1} 倍**）"
                          + $" → **{(Math.Abs(evN - ev) < 4 * seN ? "○（系列は健全）" : "×（系列が壊れている。測定前に直すこと）")}**");
        Console.WriteLine();
        Console.WriteLine("**規則 P の行が第70期 Phase 0 の「選択（C / D・シム）」と一致することも検算になる**");
        Console.WriteLine("（第70期の値は 期待値 3.07 / 総攻 48.9 / 総HP 376。第71期も同じ値を出した）。");
        Console.WriteLine();

        // ---- 0-6. 理想61行の同じ指標（分母） ----
        var idealRows0 = CompareBuilds();
        var idealTeams = idealRows0.Select(b => b.F.Occupied().Select(o => o.Def).ToArray()).ToArray();
        var idealFive = idealTeams.Where(t => t.Length == 5).ToArray();
        Console.WriteLine("## 0-6. 分母 —— 理想編成 61 行の同じ指標");
        Console.WriteLine();
        Console.WriteLine("| 母集団 | 総攻 | 総HP | **共有キーの組数** | 相方あり率 | キー無し/編成 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| **理想編成 {idealRows0.Length} 行**（5枠 {idealFive.Length} 行） "
                          + $"| **{idealRows0.Average(b => b.F.Occupied().Sum(o => o.Def.Attack)):F1}** "
                          + $"| **{idealRows0.Average(b => b.F.Occupied().Sum(o => o.Def.MaxHp)):F0}** "
                          + $"| **{idealFive.Average(D4Pairs):F2}** | **{idealFive.Average(t => D4HasPal(t) / 5.0) * 100:F1}%** "
                          + $"| **{idealFive.Average(t => t.Count(u => !d4KeyOf.ContainsKey(u.Id) || d4KeyOf[u.Id].Length == 0)):F2}** |");
        for (int r = 0; r < D4R; r++)
            Console.WriteLine($"| 規則 {d4RuleShort[r]} | {sAtk[r] / D4Sim:F1} | {sHp[r] / D4Sim:F0} "
                              + $"| {sPair[r] / D4Sim:F2} | {sPal[r] / (D4Sim * 5) * 100:F1}% | {sNoKey[r] / D4Sim:F2} |");
        Console.WriteLine();
        Console.WriteLine($"**S' の共有キーの組数（{sPair[3] / D4Sim:F2}）が S（{sPair[2] / D4Sim:F2}）より大きければ、");
        Console.WriteLine("S' は「結局集まりやすいキーを買っている」**（指示書 §5 の「S' が壊れている兆候」の片方）");
        Console.WriteLine($"→ **紙の段階の判定: {(sPair[3] > sPair[2] ? "**該当する**" : "該当しない")}**");
        Console.WriteLine();
        Console.WriteLine("| 母集団 | キーごとの保持枚数（傾きの高い順） |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 理想61行 | " + string.Join(" / ", Enumerable.Range(0, d4K)
            .OrderByDescending(k => D4Slope100[k])
            .Select(k => $"{UnitTally.CarryKeys[k]} {idealTeams.Average(t => t.Count(u => d4KeyOf.ContainsKey(u.Id) && d4KeyOf[u.Id].Contains(k))):F2}")) + " |");
        Console.WriteLine("| 規則 S' | " + string.Join(" / ", Enumerable.Range(0, d4K)
            .OrderByDescending(k => D4Slope100[k])
            .Select(k => $"{UnitTally.CarryKeys[k]} {sKey[3][k] / D4Sim:F2}")) + " |");
        Console.WriteLine();

        // ---- 0-7. N と M ----
        Console.WriteLine("## 0-7. N と M（**第69〜71期と同じ条件・同じ標本数を据え置く**）");
        Console.WriteLine();
        Console.WriteLine("指示書 §1-5 の条件は「中間帯の推定が ±2% 未満」:");
        Console.WriteLine();
        Console.WriteLine("    半幅 = 1.96 × √(p(1−p)/N) ≤ 2.0%  →  最悪の p = 0.5 で N ≥ 2401");
        Console.WriteLine();
        Console.WriteLine($"**N = {D4N:N0} / M = {D4M} を据え置く**。理由は精度ではなく比較可能性——");
        Console.WriteLine("**規則 P / S は第71期と1標本も違わない**ので、表A の P / S 版が第71期 表A と一致することが");
        Console.WriteLine($"器具の検算になる。中間帯の半幅（最悪の p = 0.5）: **±{D4Z * Math.Sqrt(0.25 / D4N) * 100:F2}%**。");
        Console.WriteLine();
        long mainB = (long)D4N * D4V * d4W * D4M;
        Console.WriteLine($"主表1帯の戦闘数: **{mainB:N0}**（{D4N:N0} 標本 × {D4V} 版 × {d4W} 波 × seed {D4M} 本）。");
        Console.WriteLine($"表E は1版ぶんで **+{(long)D4N * 5 * (d4W - 1) * D4M:N0}**（2版）、"
                          + $"理想61行の測り直しが **+{(long)(idealRows0.Length * d4W + 305 * (d4W - 1)) * 200:N0}**。");
        Console.WriteLine();

        // ---- 0-8. docs/balance.md の現在値 ----
        Console.WriteLine("## 0-8. `docs/balance.md` の現在値（Q6 の控え）");
        Console.WriteLine();
        string bpath = System.IO.File.Exists("docs/balance.md") ? "docs/balance.md" : "../docs/balance.md";
        if (System.IO.File.Exists(bpath))
        {
            var lines = System.IO.File.ReadAllLines(bpath)
                .Where(l => l.StartsWith("| ") && !l.StartsWith("| 編成") && !l.StartsWith("|---")).ToArray();
            var sums = new double[d4W]; int rows = 0;
            foreach (string l in lines)
            {
                var c = l.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                if (c.Length < d4W + 1) continue;
                bool ok = true;
                var vals = new double[d4W];
                for (int w = 0; w < d4W; w++)
                    if (!double.TryParse(c[w + 1].TrimEnd('%'), out vals[w])) { ok = false; break; }
                if (!ok) continue;
                for (int w = 0; w < d4W; w++) sums[w] += vals[w];
                rows++;
            }
            Console.WriteLine("| 行数 | セル | " + string.Join(" | ", Enumerable.Range(1, d4W).Select(w => $"第{w}波の平均")) + " | 第2〜5波の平均 |");
            Console.WriteLine("|--:|--:|" + string.Concat(Enumerable.Repeat("--:|", d4W + 1)));
            Console.WriteLine($"| **{rows}** | **{rows * d4W}** | "
                              + string.Join(" | ", Enumerable.Range(0, d4W).Select(w => $"{sums[w] / Math.Max(1, rows):F1}%"))
                              + $" | **{sums.Skip(1).Sum() / Math.Max(1, rows * (d4W - 1)):F1}%** |");
        }
        else Console.WriteLine("（`docs/balance.md` が見つからない。作業ディレクトリを確認すること）");
        Console.WriteLine();
        Console.WriteLine($"所要 {d4Sw.Elapsed.TotalSeconds:F1} 秒（**戦闘 0 回**）。");
        return;
    }

    // ======================================================================================
    // check: 陰性対照（Q6）
    // ======================================================================================
    if (d4Arg == "check")
    {
        Console.WriteLine("# 第72期 —— 陰性対照（Q6）");
        Console.WriteLine();
        Console.WriteLine("## (1) `Stages` を書き換えていないこと");
        Console.WriteLine();
        Console.WriteLine("弱い波は診断のローカルの複製で、`EnemyCatalog.Stages` は読むだけ。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 体数 | HP（既存→弱） | 攻の一致 | 速の一致 | 特性の一致 | 型の一致 | 席の一致 |");
        Console.WriteLine("|---|--:|---|:-:|:-:|:-:|:-:|:-:|");
        for (int w = 0; w < d4W; w++)
        {
            var a = d4Base[w].Enemy.Occupied().ToArray();
            var b = d4Weak[w].Enemy.Occupied().ToArray();
            bool atk = a.Length == b.Length && a.Zip(b).All(z => z.First.Def.Attack == z.Second.Def.Attack);
            bool spd = a.Zip(b).All(z => z.First.Def.Speed == z.Second.Def.Speed);
            bool tr = a.Zip(b).All(z => z.First.Def.Traits.SequenceEqual(z.Second.Def.Traits));
            bool pt = a.Zip(b).All(z => z.First.Def.Pattern == z.Second.Def.Pattern);
            bool sl = a.Zip(b).All(z => z.First.Slot == z.Second.Slot);
            Console.WriteLine($"| {d4Base[w].Name} | {a.Length} | {a.Sum(x => x.Def.MaxHp)} → {b.Sum(x => x.Def.MaxHp)} "
                              + $"| {(atk ? "○" : "×")} | {(spd ? "○" : "×")} | {(tr ? "○" : "×")} "
                              + $"| {(pt ? "○" : "×")} | {(sl ? "○" : "×")} |");
        }
        Console.WriteLine();

        Console.WriteLine("## (2) 規則 P が第70期の C/D と1標本も違わないこと");
        Console.WriteLine();
        Console.WriteLine("第70期 `draft2` の `D2DraftTeam` を**この節にだけ書き写して**突き合わせる");
        Console.WriteLine("（第71期 `check` の (2) と同じ写し。**照合のためであり、測定には使わない**）。");
        Console.WriteLine();
        UnitDef[] D4P70Team(int i)
        {
            var rng = new Random(2_000_000 + i);
            var idx = new int[d4RN];
            for (int k = 0; k < d4RN; k++) idx[k] = k;
            int remain = d4RN, strong = 0;
            var picked = new UnitDef[5];
            for (int r = 0; r < 5; r++)
            {
                var offer = new UnitDef[3];
                for (int t = 0; t < 3; t++)
                {
                    int j = t + rng.Next(remain - t);
                    (idx[t], idx[j]) = (idx[j], idx[t]);
                    offer[t] = d4Roster[idx[t]];
                }
                UnitDef pick = strong < 2
                    ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                    : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
                picked[r] = pick;
                if (pick.Attack >= D4Strong) strong++;
                int pi = 0;
                for (int t = 0; t < 3; t++) if (ReferenceEquals(d4Roster[idx[t]], pick)) { pi = t; break; }
                (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
                remain--;
            }
            return picked;
        }
        int pMis = 0;
        for (int i = 0; i < 5000; i++)
        {
            var a = D4Team(1, i); var b = D4P70Team(i);
            if (!Enumerable.Range(0, 5).All(k => ReferenceEquals(a[k], b[k]))) pMis++;
        }
        Console.WriteLine($"- **P と第70期の写しの食い違い: {pMis} 件 / 5000**");
        Console.WriteLine();

        Console.WriteLine("## (3) 4規則が規則どおりに動くこと");
        Console.WriteLine();
        Console.WriteLine("**重複なし**／**S が提示の中で共有キー最大**／**S' が提示の中で傾きの合計が最大**／");
        Console.WriteLine("**S' が「すでに選んだ駒」を見ていないこと**（提示だけから選択を再現できる）を数える。");
        Console.WriteLine();
        int dup = 0, sViol = 0, spViol = 0, spCtx = 0, nDet = 0;
        var pairDist = new int[D4R][];
        for (int r = 0; r < D4R; r++) pairDist[r] = new int[11];
        for (int i = 0; i < 5000; i++)
        {
            for (int r = 0; r < D4R; r++)
            {
                var u = D4Team(r, i);
                if (u.Select(x => x.Id).Distinct().Count() != 5) dup++;
                pairDist[r][D4Pairs(u)]++;
            }
            foreach (int rule in new[] { 2, 3 })
            {
                var rng = new Random(D4OfferSeed + i);
                var idx = new int[d4RN];
                for (int k = 0; k < d4RN; k++) idx[k] = k;
                int remain = d4RN;
                var keys = new HashSet<int>();
                var team = D4Team(rule, i);
                for (int r = 0; r < 5; r++)
                {
                    var offer = new UnitDef[3];
                    for (int t = 0; t < 3; t++)
                    {
                        int j = t + rng.Next(remain - t);
                        (idx[t], idx[j]) = (idx[j], idx[t]);
                        offer[t] = d4Roster[idx[t]];
                    }
                    if (rule == 2)
                    {
                        int best0 = offer.Max(x => d4KeyOf[x.Id].Count(k => keys.Contains(k)));
                        if (d4KeyOf[team[r].Id].Count(k => keys.Contains(k)) != best0) sViol++;
                    }
                    else
                    {
                        // 傾きの合計が最大か
                        if (d4SlopeOf[team[r].Id] != offer.Max(x => d4SlopeOf[x.Id])) spViol++;
                        // **提示だけから再現できるか**（すでに選んだ駒を見ていない）
                        var alone = offer.OrderByDescending(x => d4SlopeOf[x.Id]).ThenByDescending(x => x.Attack)
                                         .ThenBy(x => x.Id, StringComparer.Ordinal).First();
                        if (!ReferenceEquals(alone, team[r])) spCtx++;
                    }
                    foreach (int k in d4KeyOf[team[r].Id]) keys.Add(k);
                    int pi = 0;
                    for (int t = 0; t < 3; t++) if (ReferenceEquals(d4Roster[idx[t]], team[r])) { pi = t; break; }
                    (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
                    remain--;
                }
            }
            {
                var a = D4Team(0, i); var b = D4Team(0, i);
                if (!Enumerable.Range(0, 5).All(k => ReferenceEquals(a[k], b[k]))) nDet++;
            }
        }
        Console.WriteLine($"- 重複のある標本 **{dup} 件 / {5000 * D4R}**");
        Console.WriteLine($"- S が共有キー最大以外を選んだ手 **{sViol} 件 / {5000 * 5}**");
        Console.WriteLine($"- **S' が傾きの合計の最大以外を選んだ手 {spViol} 件 / {5000 * 5}**");
        Console.WriteLine($"- **S' の選択が提示だけから再現できなかった手 {spCtx} 件 / {5000 * 5}**（＝すでに選んだ駒を見ていない）");
        Console.WriteLine($"- 同じ標本番号で編成が変わった件数 **{nDet} 件 / 5000**（決定性）");
        Console.WriteLine();
        Console.WriteLine("| 共有キーの組数 | " + string.Join(" | ", Enumerable.Range(0, 11)) + " | 平均 |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", 12)));
        for (int r = 0; r < D4R; r++)
            Console.WriteLine($"| 規則 {d4RuleShort[r]} | "
                              + string.Join(" | ", pairDist[r].Select(c => $"{c * 100.0 / 5000:F1}%"))
                              + $" | **{pairDist[r].Select((c, k) => (double)c * k).Sum() / 5000:F2}** |");
        Console.WriteLine();

        Console.WriteLine("## (4) 傾きの定数が第71期 表E の写しであること");
        Console.WriteLine();
        Console.WriteLine("**定数は 1/100 単位の整数**なので、平均がちょうど 0.005 の位に落ちるキー");
        Console.WriteLine("（強化 −0.895 / 弱体 −6.495 / 痺 −12.555 / 標 −3.295）は丸めのぶんだけずれる。");
        Console.WriteLine("**線は 0.006 未満**——丸め1つぶんより大きい食い違いだけを拾う。");
        Console.WriteLine();
        Console.WriteLine("| キー | `D4Slope100` ÷ 100 | 第71期 §6 の（1枚−0枚, 2枚−1枚）の平均 | 差 | 一致 |");
        Console.WriteLine("|---|--:|--:|--:|:-:|");
        int mis = 0;
        for (int k = 0; k < d4K; k++)
        {
            double avg = (D4Src71[k][0] + D4Src71[k][1]) / 2;
            double gap = Math.Abs(avg - D4Slope100[k] / 100.0);
            bool ok = gap < 0.006;
            if (!ok) mis++;
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {D4Slope100[k] / 100.0:F3} | {avg:F3} | {gap:F3} | {(ok ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"- **食い違い {mis} 件 / {d4K}**（線 0.006）");
        Console.WriteLine();

        Console.WriteLine("## (5) 盤面");
        Console.WriteLine();
        Console.WriteLine("`slope` は `BattleEngine.Run` を読むだけで、`Traits.cs` / `UnitCatalog` / `Stages` /");
        Console.WriteLine("`CompareBuilds()` を1行も動かしていない。`compare` / `dump` の再生成と `docs/` の diff は別に取る。");
        Console.WriteLine();
        Console.WriteLine($"所要 {d4Sw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ======================================================================================
    // 主表（表A〜E）
    // ======================================================================================
    {
        bool alt = d4Arg == "alt";
        int band = alt ? D4BandB : D4BandA;
        string bandName = alt ? "B" : "A";

        var d4Full = new int[D4N][];     // [標本][版 * 波数 + 波] 勝数
        var d4Mem = new int[D4N][];      // [標本][規則 * 5 + k] 抽選された駒の添字
        int done = 0;
        Console.Error.Write($"{bandName}帯 主表: ");
        Parallel.For(0, D4N, i =>
        {
            var fw = new int[D4V * d4W];
            var mm = new int[D4R * 5];
            var teams = new UnitDef[D4R][];
            var seats = new int[D4R][];
            for (int r = 0; r < D4R; r++)
            {
                teams[r] = D4Team(r, i);
                seats[r] = D4Seats(teams[r]);
                for (int k = 0; k < 5; k++) mm[r * 5 + k] = Array.IndexOf(d4Roster, teams[r][k]);
            }
            for (int r = 0; r < D4R; r++)
            {
                Formation f = D4Form(teams[r], seats[r], -1, null);
                for (int wk = 0; wk < 2; wk++)
                {
                    int ver = r * 2 + wk;
                    for (int w = 0; w < d4W; w++)
                        for (int seed = band; seed < band + D4M; seed++)
                            if (BattleEngine.Run(f, D4Enemy(wk, w), seed, verbose: false).PlayerWon)
                                fw[ver * d4W + w]++;
                }
            }
            d4Full[i] = fw; d4Mem[i] = mm;
            int c = Interlocked.Increment(ref done);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        // **主の物差しは第2〜5波**（第70期の訂正）。5波版は併記のみ。
        double D4Rate25(int i, int ver)
        {
            int s = 0;
            for (int w = 1; w < d4W; w++) s += d4Full[i][ver * d4W + w];
            return s * 100.0 / ((d4W - 1) * D4M);
        }
        double D4Rate5(int i, int ver)
        {
            int s = 0;
            for (int w = 0; w < d4W; w++) s += d4Full[i][ver * d4W + w];
            return s * 100.0 / (d4W * D4M);
        }
        var rate = new double[D4V][];    // 第2〜5波（判定）
        var rate5 = new double[D4V][];   // 5波（併記）
        for (int v = 0; v < D4V; v++)
        {
            rate[v] = Enumerable.Range(0, D4N).Select(i => D4Rate25(i, v)).ToArray();
            rate5[v] = Enumerable.Range(0, D4N).Select(i => D4Rate5(i, v)).ToArray();
        }

        Console.WriteLine($"# 第72期 —— 良い規則ならシナジーは報われるか（{bandName} 帯 seed {band}..{band + D4M - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 slope{(alt ? " alt" : "")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"標本 **{D4N:N0}** × 版 **{D4V}**（4規則 × 2波）× {d4W} 波 × seed **{D4M}** 本 "
                          + $"= **{(long)D4N * D4V * d4W * D4M:N0} 戦**。");
        Console.WriteLine("配置は**規則配置（H）のみ**。**判定は第2〜5波**（第一波は実行して除外・5波版は併記）。");
        Console.WriteLine($"弱い波は敵の `MaxHp` を **{D4WeakPct}%**（切り捨て）にした複製で、**他の列は1つも動かしていない**。");
        Console.WriteLine();
        Console.WriteLine("**S' が使う傾きは第71期 表E（Pw・A 帯）の定数**（`D4Slope100`）で、この期の実測から作っていない。");
        Console.WriteLine("**循環は承知の上——得られるのは上限であって予測ではない。**");
        Console.WriteLine();

        // ---- 表A ----
        Console.WriteLine("## 表A —— 8版 × 波 の分布");
        Console.WriteLine();
        Console.WriteLine("| 版 | 対象 | 平均 | =0% | =100% | **中間帯 5〜95%** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int v = 0; v < D4V; v++)
        {
            for (int w = 0; w < d4W; w++)
            {
                var xs = Enumerable.Range(0, D4N).Select(i => d4Full[i][v * d4W + w] * 100.0 / D4M).ToArray();
                Console.WriteLine($"| {(w == 0 ? D4VerName(v) : D4VerShort(v))} | 第{w + 1}波 | {xs.Average():F2}% "
                                  + $"| {xs.Count(x => x == 0) * 100.0 / D4N:F1}% | {xs.Count(x => x == 100) * 100.0 / D4N:F1}% "
                                  + $"| {D4Mid(xs):F1}% |");
            }
            Console.WriteLine($"| {D4VerShort(v)} | **第2〜5波（判定に使う）** | **{rate[v].Average():F2}%** "
                              + $"| **{rate[v].Count(x => x == 0) * 100.0 / D4N:F1}%** | **{rate[v].Count(x => x == 100) * 100.0 / D4N:F1}%** "
                              + $"| **{D4Mid(rate[v]):F1}%** |");
            Console.WriteLine($"| {D4VerShort(v)} | 5波（**併記**・判定に使わない） | {rate5[v].Average():F2}% "
                              + $"| {rate5[v].Count(x => x == 0) * 100.0 / D4N:F1}% | {rate5[v].Count(x => x == 100) * 100.0 / D4N:F1}% "
                              + $"| {D4Mid(rate5[v]):F1}% |");
        }
        Console.WriteLine();
        var mid = Enumerable.Range(0, D4V).Select(v => D4Mid(rate[v])).ToArray();
        var mid5 = Enumerable.Range(0, D4V).Select(v => D4Mid(rate5[v])).ToArray();
        double half = D4Z * Math.Sqrt(0.25 / D4N) * 100;
        int best = Array.IndexOf(mid, mid.Max());
        bool anyPass = mid.Any(x => x >= 60);
        Console.WriteLine($"**Q5（台は使えるようになったか）**: S' の中間帯は "
                          + $"**S' {mid[6]:F1}% / S'w {mid[7]:F1}%**（線は 60%・割合の 95% 信頼区間 ±{half:F2}%）→ "
                          + $"**{(Math.Max(mid[6], mid[7]) >= 60 ? "○" : "×")}**。"
                          + $"8版で 60% 以上は **{mid.Count(x => x >= 60)} 個**、最大は {D4VerShort(best)} の **{mid[best]:F1}%**"
                          + $"（第71期の最大は Pw 57.1 / 57.7%）。");
        Console.WriteLine();
        Console.WriteLine("**(a') 自己検査**: 判定に使う第2〜5波の各波の平均は "
                          + string.Join(" / ", Enumerable.Range(0, D4V).Select(v =>
                              $"{D4VerShort(v)} " + string.Join(",", Enumerable.Range(1, d4W - 1).Select(w =>
                                  (Enumerable.Range(0, D4N).Average(i => d4Full[i][v * d4W + w] * 100.0 / D4M)).ToString("F0")))))
                          + " ——**飽和した波（第一波）は集計に入っていない**。");
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B —— 規則の序列（主判定 Q1・Q2）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 規則 | 中間帯（第2〜5波） | 平均勝率（第2〜5波） | 中間帯（5波・併記） | 平均勝率（5波・併記） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int wk = 0; wk < 2; wk++)
            for (int r = 0; r < D4R; r++)
            {
                int v = r * 2 + wk;
                Console.WriteLine($"| {d4WkName[wk]} | {d4RuleShort[r]} | **{mid[v]:F1}%** | **{rate[v].Average():F2}%** "
                                  + $"| {mid5[v]:F1}% | {rate5[v].Average():F2}% |");
            }
        Console.WriteLine();
        Console.WriteLine("| 波 | 量 | 中間帯（第2〜5波） | 平均勝率（第2〜5波） | 中間帯（5波） | 平均勝率（5波） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int wk = 0; wk < 2; wk++)
        {
            int vn = wk, vp = 2 + wk, vs = 4 + wk, vq = 6 + wk;
            void Row(string name, int a, int b) =>
                Console.WriteLine($"| {d4WkName[wk]} | {name} | {D4P2(mid[a] - mid[b])} "
                                  + $"| {D4P2(rate[a].Average() - rate[b].Average())} "
                                  + $"| {D4P2(mid5[a] - mid5[b])} | {D4P2(rate5[a].Average() - rate5[b].Average())} |");
            Row("**P − N**（攻撃力を数える価値）", vp, vn);
            Row("S − P（第71期の再現）", vs, vp);
            Row("**S' − P（上限・主判定 Q1）**", vq, vp);
            Row("S' − S（規則を直した価値）", vq, vs);
            Row("S' − N（合計）", vq, vn);
        }
        Console.WriteLine();
        {
            bool ord0 = rate[6].Average() > rate[2].Average() && rate[2].Average() > rate[4].Average()
                        && rate[4].Average() > rate[0].Average();
            bool ord1 = rate[7].Average() > rate[3].Average() && rate[3].Average() > rate[5].Average()
                        && rate[5].Average() > rate[1].Average();
            Console.WriteLine($"**Q2（序列 S' > P > S > N か・平均勝率・第2〜5波）**: "
                              + $"既存波 **{(ord0 ? "○" : "×")}**（S' {rate[6].Average():F2} / P {rate[2].Average():F2} / S {rate[4].Average():F2} / N {rate[0].Average():F2}）"
                              + $" ・ 弱い波 **{(ord1 ? "○" : "×")}**（S' {rate[7].Average():F2} / P {rate[3].Average():F2} / S {rate[5].Average():F2} / N {rate[1].Average():F2}）");
            Console.WriteLine();
            Console.WriteLine($"**Q1（上限・S' − P）**: 既存波 中間帯 {D4P2(mid[6] - mid[2])} / 平均勝率 **{D4P2(rate[6].Average() - rate[2].Average())} pt**"
                              + $" ・ 弱い波 中間帯 {D4P2(mid[7] - mid[3])} / 平均勝率 **{D4P2(rate[7].Average() - rate[3].Average())} pt**");
        }
        Console.WriteLine();

        // 理想61行（同じ実行の中で測り直す。**上限の分母**）
        var idealRows = CompareBuilds();
        var idWinW = new int[idealRows.Length][];
        Parallel.For(0, idealRows.Length, r =>
        {
            var ww = new int[d4W];
            for (int w = 0; w < d4W; w++)
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(idealRows[r].F, d4Base[w].Enemy, seed, verbose: false).PlayerWon) ww[w]++;
            idWinW[r] = ww;
        });
        double id25 = idWinW.Average(ww => ww.Skip(1).Sum() * 100.0 / ((d4W - 1) * 200));
        double id5 = idWinW.Average(ww => ww.Sum() * 100.0 / (d4W * 200));
        Console.WriteLine("### 上限を「理想編成との差」の割合で読む");
        Console.WriteLine();
        Console.WriteLine($"理想編成 {idealRows.Length} 行の平均勝率（既存5波・seed 0..199）: "
                          + $"**第2〜5波 {id25:F2}% / 5波 {id5:F2}%**。");
        Console.WriteLine();
        Console.WriteLine("| 物差し | P（素朴） | 理想 | 差（＝シナジーの総額） | **S' − P** | **埋めた割合** | （参考）S − P |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        {
            double gap25 = id25 - rate[2].Average();
            double rec25 = rate[6].Average() - rate[2].Average(), old25 = rate[4].Average() - rate[2].Average();
            double gap5 = id5 - rate5[2].Average();
            double rec5 = rate5[6].Average() - rate5[2].Average(), old5 = rate5[4].Average() - rate5[2].Average();
            Console.WriteLine($"| 平均勝率・第2〜5波（既存波） | {rate[2].Average():F2}% | {id25:F2}% | {gap25:F2} pt "
                              + $"| **{D4P2(rec25)} pt** | **{rec25 / gap25 * 100:F1}%** | {D4P2(old25)} pt |");
            Console.WriteLine($"| 平均勝率・5波（既存波・併記） | {rate5[2].Average():F2}% | {id5:F2}% | {gap5:F2} pt "
                              + $"| {D4P2(rec5)} pt | {rec5 / gap5 * 100:F1}% | {D4P2(old5)} pt |");
        }
        Console.WriteLine();
        Console.WriteLine("**分母は既存波でしか意味を持たない**（理想61行は既存波でしか測っていない）ので、");
        Console.WriteLine("弱い波の S' − P はこの表に入れない。");
        Console.WriteLine();

        // 波ごとの内訳
        Console.WriteLine("### 波ごとの内訳（平均勝率）");
        Console.WriteLine();
        Console.WriteLine("| 波の版 | 対象 | N | P | S | S' | P − N | S − P | **S' − P** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int wk = 0; wk < 2; wk++)
            for (int w = 0; w < d4W; w++)
            {
                double[] m = Enumerable.Range(0, D4R)
                    .Select(r => Enumerable.Range(0, D4N).Average(i => d4Full[i][(r * 2 + wk) * d4W + w] * 100.0 / D4M)).ToArray();
                Console.WriteLine($"| {d4WkName[wk]} | 第{w + 1}波 | {m[0]:F2}% | {m[1]:F2}% | {m[2]:F2}% | {m[3]:F2}% "
                                  + $"| {D4P2(m[1] - m[0])} | {D4P2(m[2] - m[1])} | **{D4P2(m[3] - m[1])}** |");
            }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C —— 編成の中身（Phase 0 の紙との照合・Q3）");
        Console.WriteLine();
        Console.WriteLine("**波は編成を変えないので行は4つ**（規則ごと）。");
        Console.WriteLine();
        Console.WriteLine($"| 規則 | 攻{D4Strong}以上 0枚 | 1 | 2 | 3 | 4 | 5 | 期待値 | 総攻 | 総HP | **共有キーの組数** | 相方あり率 | キー無し/編成 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var pairMean = new double[D4R];
        var keyMean = new double[D4R][];
        for (int r = 0; r < D4R; r++)
        {
            var dist = new int[6];
            double atk = 0, hp = 0, pal = 0, pr = 0, nk = 0;
            keyMean[r] = new double[d4K];
            for (int i = 0; i < D4N; i++)
            {
                var mem = Enumerable.Range(0, 5).Select(k => d4Roster[d4Mem[i][r * 5 + k]]).ToArray();
                dist[mem.Count(u => u.Attack >= D4Strong)]++;
                atk += mem.Sum(u => u.Attack); hp += mem.Sum(u => u.MaxHp);
                pr += D4Pairs(mem); pal += D4HasPal(mem);
                nk += mem.Count(u => d4KeyOf[u.Id].Length == 0);
                for (int k = 0; k < d4K; k++) keyMean[r][k] += mem.Count(u => d4KeyOf[u.Id].Contains(k));
            }
            pairMean[r] = pr / D4N;
            for (int k = 0; k < d4K; k++) keyMean[r][k] /= D4N;
            Console.Write($"| {d4RuleName[r]} |");
            for (int k = 0; k <= 5; k++) Console.Write($" {dist[k] * 100.0 / D4N:F1}% |");
            Console.WriteLine($" **{dist.Select((c, k) => (double)c * k).Sum() / D4N:F2}** | {atk / D4N:F1} | {hp / D4N:F0} "
                              + $"| **{pr / D4N:F2}** | {pal * 100.0 / (D4N * 5):F1}% | {nk / D4N:F2} |");
        }
        var idealTeamsAll = idealRows.Select(b => b.F.Occupied().Select(o => o.Def).ToArray()).ToArray();
        var idealTeams5 = idealTeamsAll.Where(t => t.Length == 5).ToArray();
        Console.WriteLine($"| **（参考）理想編成 {idealRows.Length} 行**（5枠 {idealTeams5.Length} 行） | — | — | — | — | — | — | — "
                          + $"| {idealRows.Average(b => b.F.Occupied().Sum(o => o.Def.Attack)):F1} "
                          + $"| {idealRows.Average(b => b.F.Occupied().Sum(o => o.Def.MaxHp)):F0} "
                          + $"| **{idealTeams5.Average(D4Pairs):F2}** | {idealTeams5.Average(t => D4HasPal(t) / 5.0) * 100:F1}% "
                          + $"| {idealTeams5.Average(t => t.Count(u => !d4KeyOf.ContainsKey(u.Id) || d4KeyOf[u.Id].Length == 0)):F2} |");
        Console.WriteLine();
        Console.WriteLine("### キーごとの保持枚数（**Q3 の本体**）");
        Console.WriteLine();
        Console.WriteLine("| キー | 傾き | N | P | S | **S'** | **S' − P** | S − P | 理想61行 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int k in Enumerable.Range(0, d4K).OrderByDescending(k => D4Slope100[k]))
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {D4P2(D4Slope100[k] / 100.0)} "
                              + $"| {keyMean[0][k]:F2} | {keyMean[1][k]:F2} | {keyMean[2][k]:F2} | **{keyMean[3][k]:F2}** "
                              + $"| **{D4P2(keyMean[3][k] - keyMean[1][k])}** | {D4P2(keyMean[2][k] - keyMean[1][k])} "
                              + $"| {idealTeamsAll.Average(t => t.Count(u => d4KeyOf.ContainsKey(u.Id) && d4KeyOf[u.Id].Contains(k))):F2} |");
        Console.WriteLine();
        Console.WriteLine($"**共有キーの組数: N {pairMean[0]:F2} / P {pairMean[1]:F2} / S {pairMean[2]:F2} / S' {pairMean[3]:F2}**"
                          + $"（理想61行 {idealTeams5.Average(D4Pairs):F2}）");
        Console.WriteLine($"→ **S' が壊れている兆候（S' の組数 > S の組数）: "
                          + $"{(pairMean[3] > pairMean[2] ? "該当する" : "該当しない")}**");
        {
            bool wound = keyMean[3][UnitTally.CarryWound] < keyMean[2][UnitTally.CarryWound];
            bool stun = keyMean[3][UnitTally.CarryStun] < keyMean[2][UnitTally.CarryStun];
            bool hit = keyMean[3][UnitTally.CarryHit] > keyMean[2][UnitTally.CarryHit];
            Console.WriteLine($"→ **Q3 の符号（傷・痺 が S より少なく、被弾が S より多い）: "
                              + $"傷 {(wound ? "○" : "×")} / 痺 {(stun ? "○" : "×")} / 被弾 {(hit ? "○" : "×")}**");
        }
        Console.WriteLine();

        // ---- 表D: 傾きの地図（**この期の第一級の出力**） ----
        //
        // **規則は P 固定**（S / S' は選好が偏るので地図の分母にしない）。
        // 条件は **4波（第2〜5波）× 2つの波の強さ = 8**。指示書 §3 Q4 は「4条件」と書くが、
        // §2-2 の定義（波 × 波の強さ）は 8 通りになる——**8条件で数え、集計2条件と理想61行を併記する。**
        Console.WriteLine("## 表D —— 傾きの地図（**この期の第一級の出力**・規則 P 固定）");
        Console.WriteLine();
        Console.WriteLine("キーを持つ駒が編成に何枚いたかで標本を分け、**（1枚 − 0枚）と（2枚 − 1枚）の平均**を傾きとする");
        Console.WriteLine("（第71期 表E と同じ定義）。**規則は P 固定**——S / S' は選好が偏るので地図の分母にしない。");
        Console.WriteLine();

        var slopeCell = new double[d4K, 2 * d4W];    // [キー][wk * d4W + w]（w = 0 は第一波・参考）
        var slopeAgg = new double[d4K, 2];           // [キー][wk] 第2〜5波の集計
        var boxN = new int[d4K][];
        for (int k = 0; k < d4K; k++)
        {
            var cnt = new int[D4N];
            boxN[k] = new int[4];
            for (int i = 0; i < D4N; i++)
            {
                int c = 0;
                for (int q = 0; q < 5; q++) if (d4KeyOf[d4Roster[d4Mem[i][1 * 5 + q]].Id].Contains(k)) c++;
                cnt[i] = Math.Min(c, 3);
                boxN[k][cnt[i]]++;
            }
            for (int wk = 0; wk < 2; wk++)
            {
                int v = 1 * 2 + wk;
                for (int w = 0; w < d4W; w++)
                {
                    var bk = new List<double>[4];
                    for (int b = 0; b < 4; b++) bk[b] = new List<double>();
                    for (int i = 0; i < D4N; i++) bk[cnt[i]].Add(d4Full[i][v * d4W + w] * 100.0 / D4M);
                    slopeCell[k, wk * d4W + w] = D4SlopeOf4(bk);
                }
                {
                    var bk = new List<double>[4];
                    for (int b = 0; b < 4; b++) bk[b] = new List<double>();
                    for (int i = 0; i < D4N; i++) bk[cnt[i]].Add(rate[v][i]);
                    slopeAgg[k, wk] = D4SlopeOf4(bk);
                }
            }
        }

        // 理想61行の同じ表
        var idSlopeCell = new double[d4K, d4W];
        var idSlopeAgg = new double[d4K];
        var idBoxes = new int[d4K][];
        for (int k = 0; k < d4K; k++)
        {
            var cnt = idealTeamsAll.Select(t => Math.Min(t.Count(u => d4KeyOf.ContainsKey(u.Id) && d4KeyOf[u.Id].Contains(k)), 3)).ToArray();
            idBoxes[k] = new int[4];
            for (int r = 0; r < cnt.Length; r++) idBoxes[k][cnt[r]]++;
            for (int w = 0; w < d4W; w++)
            {
                var bk = new List<double>[4];
                for (int b = 0; b < 4; b++) bk[b] = new List<double>();
                for (int r = 0; r < idealRows.Length; r++) bk[cnt[r]].Add(idWinW[r][w] * 100.0 / 200);
                idSlopeCell[k, w] = D4SlopeOf4(bk);
            }
            {
                var bk = new List<double>[4];
                for (int b = 0; b < 4; b++) bk[b] = new List<double>();
                for (int r = 0; r < idealRows.Length; r++) bk[cnt[r]].Add(idWinW[r].Skip(1).Sum() * 100.0 / ((d4W - 1) * 200));
                idSlopeAgg[k] = D4SlopeOf4(bk);
            }
        }

        Console.WriteLine("### D-1. ドラフト台（規則 P）—— 8条件（4波 × 2つの波の強さ）");
        Console.WriteLine();
        Console.WriteLine("| キー | 保持駒 | 0枚 | 1枚 | 2枚 | 3+ | 既存2 | 既存3 | 既存4 | 既存5 | 弱2 | 弱3 | 弱4 | 弱5 | **既存·集計** | **弱·集計** | 符号一致 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|:-:|");
        int allSame = 0, split = 0, allSameLoose = 0;
        foreach (int k in Enumerable.Range(0, d4K).OrderByDescending(k => D4Slope100[k]))
        {
            var cells = new List<double>();
            for (int wk = 0; wk < 2; wk++) for (int w = 1; w < d4W; w++) cells.Add(slopeCell[k, wk * d4W + w]);
            var valid = cells.Where(x => !double.IsNaN(x)).ToArray();
            bool same = valid.Length > 0 && (valid.All(x => x > 0) || valid.All(x => x < 0));
            var loose = valid.Where(x => Math.Abs(x) >= 1.0).ToArray();
            bool sameLoose = loose.Length == 0 || loose.All(x => x > 0) || loose.All(x => x < 0);
            if (same) allSame++; else split++;
            if (sameLoose) allSameLoose++;
            Console.Write($"| {UnitTally.CarryKeys[k]} | {d4Roster.Count(u => d4KeyOf[u.Id].Contains(k))} "
                          + $"| {boxN[k][0]} | {boxN[k][1]} | {boxN[k][2]} | {boxN[k][3]} |");
            foreach (double x in cells) Console.Write($" {D4P1(x)} |");
            Console.WriteLine($" **{D4P1(slopeAgg[k, 0])}** | **{D4P1(slopeAgg[k, 1])}** | {(same ? "○" : "**×**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q4（8条件で符号が一致するキー）: {allSame} / {d4K}**（割れるキー {split}）。"
                          + $"**|傾き| < 1.0 のセルを 0 と読む緩い版では {allSameLoose} / {d4K}。**");
        Console.WriteLine("**一致するキーは「ロスターの性質」・割れるキーは「台に固有」**（指示書 Q4）。");
        Console.WriteLine();

        Console.WriteLine("### D-2. 理想61行（同じ器具・既存5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| キー | 0枚 | 1枚 | 2枚 | 3+ | 第2波 | 第3波 | 第4波 | 第5波 | **集計** | ドラフト台·既存 | 符号 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|:-:|");
        int idAgree = 0, idCmp = 0;
        foreach (int k in Enumerable.Range(0, d4K).OrderByDescending(k => D4Slope100[k]))
        {
            Console.Write($"| {UnitTally.CarryKeys[k]} | {idBoxes[k][0]} | {idBoxes[k][1]} | {idBoxes[k][2]} | {idBoxes[k][3]} |");
            for (int w = 1; w < d4W; w++) Console.Write($" {D4P1(idSlopeCell[k, w])} |");
            bool cmp = !double.IsNaN(idSlopeAgg[k]) && !double.IsNaN(slopeAgg[k, 0]);
            bool agree = cmp && Math.Sign(idSlopeAgg[k]) == Math.Sign(slopeAgg[k, 0]);
            if (cmp) { idCmp++; if (agree) idAgree++; }
            Console.WriteLine($" **{D4P1(idSlopeAgg[k])}** | {D4P1(slopeAgg[k, 0])} "
                              + $"| {(!cmp ? "—" : agree ? "○" : "**×**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**理想61行とドラフト台（既存波）で符号が一致するキー: {idAgree} / {idCmp}**。");
        Console.WriteLine("**第71期 §9-4 の未確認（傷・痺 の −15 はドラフト台に固有か）はここで答える**"
                          + "——ただし理想61行は n = 61 で箱が小さく、0枚 / 3枚以上 の箱が空になるキーがある（左の4列）。");
        Console.WriteLine();

        Console.WriteLine("### D-3. 検算 —— A 帯の実測 Pw 傾きが `D4Slope100`（第71期）を再現するか");
        Console.WriteLine();
        Console.WriteLine("**S' が使った定数は第71期 表E（Pw・A 帯）の値。同じ標本・同じ seed 帯なので、");
        Console.WriteLine("A 帯ではこの表が一致するはず**（B 帯では seed が違うのでずれてよい）。");
        Console.WriteLine();
        Console.WriteLine("| キー | S' が使った定数 | この期の実測（Pw・集計） | 差 |");
        Console.WriteLine("|---|--:|--:|--:|");
        double maxGap = 0;
        foreach (int k in Enumerable.Range(0, d4K).OrderByDescending(k => D4Slope100[k]))
        {
            double g = slopeAgg[k, 1] - D4Slope100[k] / 100.0;
            if (!double.IsNaN(g)) maxGap = Math.Max(maxGap, Math.Abs(g));
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {D4P2(D4Slope100[k] / 100.0)} | {D4P2(slopeAgg[k, 1])} | {D4P2(g)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"- **最大差 {maxGap:F2} pt**（{bandName} 帯）");
        Console.WriteLine();

        // ---- 表E: 駒ごとの在席時勝率と寄与 ----
        //
        // **どの版で出すかの規則は測る前に固定**（自由度 (e) を消すため）:
        //   Q5 を満たした版があれば**その版の波設定**、無ければ**中間帯が最大の版の波設定**を採り、
        //   **S' と P の2つ**を出す。P を並べるのは**第71期の r（Pw 0.150 / 0.149）が再現することが
        //   器具の検算**になるため（同じ標本・同じ seed・同じ器具）。
        int showWk = best % 2;
        var showV = new[] { 6 + showWk, 2 + showWk };

        Console.WriteLine("## 表E —— 駒ごとの在席時勝率と寄与");
        Console.WriteLine();
        Console.WriteLine($"出すのは **{string.Join(" / ", showV.Select(D4VerShort))}**"
                          + $"（{(anyPass ? "Q5 を満たした版" : "**Q5 を満たす版が無いので、中間帯が最大の版**")}の波設定＝**{d4WkName[showWk]}**）。");
        Console.WriteLine("**P を並べるのは第71期の r（Pw 0.150 / 0.149）が再現することが器具の検算になるため**（測る前に固定した規則）。");
        Console.WriteLine();
        Console.WriteLine("**寄与 = 標本の勝率 − その駒を素体（同数値・特性なし）に差し替えた版の勝率。**");
        Console.WriteLine("**器具は素体差し替えに統一**（第69期）。理想61行の側も**同じ器具・同じ集計（第2〜5波・seed 0..199）**で測り直した。");
        Console.WriteLine();

        var idJobs = new List<(int Row, int Unit, int Slot)>();
        for (int r = 0; r < idealRows.Length; r++)
            foreach ((int slot, UnitDef d) in idealRows[r].F.Occupied())
            {
                int ui0 = Array.FindIndex(d4Roster, x => x.Id == d.Id);
                if (ui0 >= 0) idJobs.Add((r, ui0, slot));
            }
        var idPlain25 = new double[idJobs.Count];
        Parallel.For(0, idJobs.Count, j =>
        {
            (int r, int ui, int slot) = idJobs[j];
            var pf = idealRows[r].F.Clone();
            pf[slot] = d4Plain[ui];
            int win = 0;
            for (int w = 1; w < d4W; w++)
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(pf, d4Base[w].Enemy, seed, verbose: false).PlayerWon) win++;
            idPlain25[j] = win * 100.0 / ((d4W - 1) * 200);
        });
        var idSeat = new List<double>[d4RN];
        var idCon = new List<double>[d4RN];
        for (int u = 0; u < d4RN; u++) { idSeat[u] = new List<double>(); idCon[u] = new List<double>(); }
        for (int j = 0; j < idJobs.Count; j++)
        {
            (int r, int ui, _) = idJobs[j];
            double full25 = idWinW[r].Skip(1).Sum() * 100.0 / ((d4W - 1) * 200);
            idSeat[ui].Add(full25);
            idCon[ui].Add(full25 - idPlain25[j]);
        }

        var rSeatOf = new Dictionary<int, double>();
        var rConOf = new Dictionary<int, double>();
        foreach (int v in showV)
        {
            int r0 = v / 2;
            var plainWin = new int[D4N][];
            int dn = 0;
            Console.Error.Write($"{bandName}帯 表E({D4VerShort(v)}): ");
            Parallel.For(0, D4N, i =>
            {
                var team = D4Team(r0, i);
                var seats = D4Seats(team);
                var pw = new int[5];
                for (int k = 0; k < 5; k++)
                {
                    var g = D4Form(team, seats, k, d4Plain[d4Mem[i][r0 * 5 + k]]);
                    for (int w = 1; w < d4W; w++)
                        for (int seed = band; seed < band + D4M; seed++)
                            if (BattleEngine.Run(g, D4Enemy(showWk, w), seed, verbose: false).PlayerWon) pw[k]++;
                }
                plainWin[i] = pw;
                int c = Interlocked.Increment(ref dn);
                if (c % 1000 == 0) Console.Error.Write(".");
            });
            Console.Error.WriteLine();

            var seatRate = new List<double>[d4RN];
            var conRate = new List<double>[d4RN];
            for (int u = 0; u < d4RN; u++) { seatRate[u] = new List<double>(); conRate[u] = new List<double>(); }
            for (int i = 0; i < D4N; i++)
                for (int k = 0; k < 5; k++)
                {
                    int u = d4Mem[i][r0 * 5 + k];
                    seatRate[u].Add(rate[v][i]);
                    conRate[u].Add(rate[v][i] - plainWin[i][k] * 100.0 / ((d4W - 1) * D4M));
                }

            Console.WriteLine($"### {D4VerName(v)}");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 攻 | キー | 傾き計 | 在席 | 在席時勝率 | ±95% | 寄与 | ±95% | 理想·在席時勝率 | 理想·寄与 | 行数 |");
            Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            var xs1 = new List<double>(); var ys1 = new List<double>();
            var xs2 = new List<double>(); var ys2 = new List<double>();
            foreach (int u in Enumerable.Range(0, d4RN).OrderByDescending(u => D4Stats(conRate[u]).Mean))
            {
                var a = D4Stats(seatRate[u]); var b = D4Stats(conRate[u]);
                string keys = string.Join("・", d4KeyOf[d4Roster[u].Id].Select(k => UnitTally.CarryKeys[k]));
                string idS = idSeat[u].Count > 0 ? $"{idSeat[u].Average():F1}%" : "—";
                string idC = idCon[u].Count > 0 ? D4P1(idCon[u].Average()) : "—";
                if (idSeat[u].Count > 0)
                {
                    xs1.Add(a.Mean); ys1.Add(idSeat[u].Average());
                    xs2.Add(b.Mean); ys2.Add(idCon[u].Average());
                }
                Console.WriteLine($"| {d4Roster[u].Name} | {d4Roster[u].Attack} | {(keys.Length == 0 ? "—" : keys)} "
                                  + $"| {D4P1(d4SlopeOf[d4Roster[u].Id] / 100.0)} | {a.N} "
                                  + $"| {a.Mean:F2}% | ±{D4Z * a.Sd / Math.Sqrt(Math.Max(1, a.N)):F2} "
                                  + $"| **{D4P1(b.Mean)}** | ±{D4Z * b.Sd / Math.Sqrt(Math.Max(1, b.N)):F2} "
                                  + $"| {idS} | {idC} | {idSeat[u].Count} |");
            }
            Console.WriteLine();
            double rSeat = D4Corr(xs1, ys1), rCon = D4Corr(xs2, ys2);
            rSeatOf[v] = rSeat; rConOf[v] = rCon;
            Console.WriteLine("| 散布（理想61行との相関） | r | n |");
            Console.WriteLine("|---|--:|--:|");
            Console.WriteLine($"| **在席時勝率どうし** | **{rSeat:F3}** | {xs1.Count} |");
            Console.WriteLine($"| 寄与どうし | **{rCon:F3}** | {xs2.Count} |");
            Console.WriteLine();
        }
        Console.WriteLine($"**理想61行との r（在席時勝率どうし）: P {rSeatOf[2 + showWk]:F3} / S' {rSeatOf[6 + showWk]:F3}**"
                          + $"（第71期は N 0.168 / P 0.150 / S 0.205）。"
                          + $"**P が第71期を再現していれば器具の検算になる。**"
                          + $" 寄与どうしは P {rConOf[2 + showWk]:F3} / S' {rConOf[6 + showWk]:F3}（第71期 Pw 0.521 / Sw 0.588）。");
        Console.WriteLine();

        Console.WriteLine($"所要 {d4Sw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }
}
}
