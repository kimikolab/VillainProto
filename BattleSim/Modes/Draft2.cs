using BattleCore;
using static Common;

// =====================================================================================
// draft2 モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "draft2")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 draft2
// =====================================================================================

static class Draft2Diag
{
// ============================================================================
// draft2 モード: ドラフト台の床は誰のせいか（第70期）
//
// 第69期のドラフト台は**平均 6.58%・標本の 78.9% がちょうど 0%**（中間帯 17.9%・Q1 の線は 60%）。
// **台としては使えない。** ただしこの床には2つの原因が混ざっている:
//
//   ロスターの側 —— 攻撃力6以下が 28/51。無作為5枚では殴る駒が足りない
//   波の側       —— 既存5波は**理想編成に勝てるよう調整された敵**で、ドラフト編成には強すぎる
//
// **2×2 で切り分ける。**
//
//        |  既存5波  |  弱い波（敵の MaxHp を一律 60%）
//   ---- + --------- + --------------------------------
//   無作為5枚         |    A     |    B
//   3枚提示から1枚×5回 |    C     |    D
//
// **この期は切り分けるだけ。設計判断をしない。採否も無い。**
// **`Stages` は書き換えない**（弱い波は診断のローカルに組む。`gradient` / `aim` と同じ扱い）。
// **配置は規則配置（H）のみ**——第69期で「78.9% が 0% の台では置き方を変えても 0 のまま」で
// 配置の価値は**測定不能**だった。台が直ってから測り直す。
// **第一波を除外しない**（第69期の訂正のとおり、この台では第一波も分別に効く）。
//
//     dotnet run --project BattleSim -c Release 0 draft2 phase0  # 紙の計算（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 draft2         # 主表（表A〜E）・A 帯 seed 0..7
//     dotnet run --project BattleSim -c Release 0 draft2 alt     # 同じ標本を B 帯（seed 200..207）で
//     dotnet run --project BattleSim -c Release 0 draft2 check   # 陰性対照（Q6）
public static void Run(string[] args, int stageIndex)
{
    string d2Arg = args.Length > 2 ? args[2] : "";
    IReadOnlyList<EnemyCatalog.Stage> d2Base = EnemyCatalog.Stages;
    int d2W = d2Base.Count;
    var d2Sw = System.Diagnostics.Stopwatch.StartNew();
    var d2Roster = UnitCatalog.All.ToArray();
    int d2RN = d2Roster.Length;                       // 51
    var d2KeyOf = d2Roster.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);

    // ---- 測る前に固定した定数 ------------------------------------------------------------
    //
    // 標本の乱数は測定の乱数と分ける（第69期と同じ作法）。抽選用の系列は 1000000.. と 2000000.. で、
    // 戦闘 seed の帯（0.. / 200..）とは重ならない。
    // **無作為5枚の系列は第69期の `draft` と同じ 1000000..**
    // ——A 版は第69期の H 版の波ごとの値を**そのまま再現するはず**で、それが器具の検算になる。
    const int D2RandSeed = 1_000_000;
    const int D2DraftSeed = 2_000_000;
    const int D2BandA = 0, D2BandB = 200;
    const int D2M = 8;                 // 1標本あたりの戦闘 seed 数（第69期と同じ）
    const int D2N = 11000;             // 標本数。決め方は phase0 の §0-4
    const int D2Strong = 7;            // 「殴れる駒」の線（攻撃力7以上）。§2-2 の選択規則が使う
    const int D2WeakPct = 60;          // 弱い波: 敵の MaxHp をこの % に（切り捨て）
    const double D2Z = 1.96;

    string[] d2VerName = { "A（無作為 × 既存5波）", "B（無作為 × 弱い波）",
                           "C（選択 × 既存5波）",   "D（選択 × 弱い波）" };
    string[] d2VerShort = { "A", "B", "C", "D" };
    const int D2V = 4;

    // ---- 弱い波（§2-3。**HP だけの1変数**） ----------------------------------------------
    //
    // 攻撃力・速さ・特性・席・体数は一切変えない。第34期の教訓
    // （1変数を振るときは、それが他に何を一緒に動かすかを先に数える）——攻撃力を下げると
    // 「味方が落ちない → 手数が増える」で決着ターンまで動くが、**HP だけなら動くのは
    // 「敵を倒すのに要る打点」だけ**。
    // **敵側に召喚・分裂・蘇生を持つ駒は1体もいない**ので、戦闘中に等倍の敵が湧くことはない
    // （`EnemyCatalog` を grep して 0 件）。
    var d2WeakCache = new Dictionary<string, UnitDef>();
    UnitDef D2WeakOf(UnitDef d)
    {
        if (d2WeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * D2WeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        d2WeakCache[d.Id] = w;
        return w;
    }
    var d2Weak = d2Base.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = D2WeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();
    Formation D2Enemy(int ver, int w) => (ver == 1 || ver == 3) ? d2Weak[w].Enemy : d2Base[w].Enemy;

    // ---- 素体（第69期と同じ構成。**`Actions` は落とす**） --------------------------------
    var d2Plain = d2Roster.Select(d => new UnitDef
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    }).ToArray();

    // ---- 編成の作り方 ---------------------------------------------------------------------
    //
    // 無作為5枚（A / B）: 第69期 `draft` の抽選部分の**写し**。同じ系列・同じ手順なので
    // 標本は1体も違わない。
    UnitDef[] D2RandomTeam(int i)
    {
        var rng = new Random(D2RandSeed + i);
        var idx = new int[d2RN];
        for (int k = 0; k < d2RN; k++) idx[k] = k;
        for (int k = 0; k < 5; k++)
        {
            int j = k + rng.Next(d2RN - k);
            (idx[k], idx[j]) = (idx[j], idx[k]);
        }
        var u = new UnitDef[5];
        for (int k = 0; k < 5; k++) u[k] = d2Roster[idx[k]];
        return u;
    }

    // 選択ありドラフト（C / D）: §2-2 の規則。**測る前に固定。3行から増やしていない。**
    //
    //   1. まだ選ばれていない駒から無作為に3枚提示する
    //   2. すでに選んだ駒に攻撃力7以上が 2 枚未満なら、提示の中で攻撃力が最大の駒
    //      そうでなければ、提示の中で最大HPの駒
    //   3. 同値は `Def.Id` の辞書順
    //
    // **乱数は提示にしか使わない**（選択は決定的）。
    UnitDef[] D2DraftTeam(int i)
    {
        var rng = new Random(D2DraftSeed + i);
        var idx = new int[d2RN];
        for (int k = 0; k < d2RN; k++) idx[k] = k;
        int remain = d2RN, strong = 0;
        var picked = new UnitDef[5];
        for (int r = 0; r < 5; r++)
        {
            // 残り `remain` 枚から無作為に3枚（部分 Fisher-Yates を3つぶん進める）
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = d2Roster[idx[t]];
            }
            UnitDef pick = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = pick;
            if (pick.Attack >= D2Strong) strong++;
            // 選んだ1枚だけを候補から外す（提示して選ばなかった2枚は次回も候補に残る）
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(d2Roster[idx[t]], pick)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }

    // 規則配置 H（第69期 §1-3 の写し。席0/1・3/4 の割り当てまで同じ）
    int[] D2Seats(UnitDef[] u)
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
    Formation D2Form(UnitDef[] u, int[] seats, int replace, UnitDef? with)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = (k == replace) ? with : u[k];
        return f;
    }

    // ---- 統計の道具 -----------------------------------------------------------------------
    string D2P1(double x) => (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    string D2P2(double x) => (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    (double Mean, double Sd, int N) D2Stats(IReadOnlyList<double> xs)
    {
        int n = xs.Count;
        if (n == 0) return (0, 0, 0);
        double m = xs.Average();
        if (n < 2) return (m, 0, n);
        return (m, Math.Sqrt(xs.Sum(x => (x - m) * (x - m)) / (n - 1)), n);
    }
    double D2Corr(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        int n = a.Count;
        if (n < 2) return double.NaN;
        double ma = a.Average(), mb = b.Average();
        double sa = 0, sb = 0, sab = 0;
        for (int i = 0; i < n; i++) { double x = a[i] - ma, y = b[i] - mb; sa += x * x; sb += y * y; sab += x * y; }
        return (sa <= 0 || sb <= 0) ? double.NaN : sab / Math.Sqrt(sa * sb);
    }
    // 中間帯（5〜95%）の割合
    double D2Mid(IEnumerable<double> xs) { var a = xs.ToArray(); return a.Count(x => x >= 5 && x <= 95) * 100.0 / a.Length; }

    // ======================================================================================
    // phase0: 紙の計算（**戦闘を1回も回さない**）
    // ======================================================================================
    if (d2Arg == "phase0")
    {
        Console.WriteLine("# 第70期 Phase 0 —— 測る前に紙で計算する（ドラフト台の 2×2）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** 第69期の P1（30〜50%）は当てずっぽうで、");
        Console.WriteLine("超幾何を使えば指示書の時点で出せた——同じことをしないための節。");
        Console.WriteLine();

        // ---- 0-1. ロスターの分布 ----
        Console.WriteLine("## 0-1. ロスターの分布（`UnitCatalog.All` 51 体）");
        Console.WriteLine();
        void D2Hist(string label, Func<UnitDef, int> f, int[] edges)
        {
            Console.Write($"| {label} |");
            for (int e = 0; e < edges.Length; e++)
            {
                int lo = edges[e], hi = e + 1 < edges.Length ? edges[e + 1] - 1 : int.MaxValue;
                int n = d2Roster.Count(u => f(u) >= lo && f(u) <= hi);
                Console.Write($" {n} |");
            }
            Console.WriteLine($" {d2Roster.Average(f):F1} | {d2Roster.Min(f)}〜{d2Roster.Max(f)} |");
        }
        Console.WriteLine("| 攻撃力 | 0-2 | 3-4 | 5-6 | 7-9 | 10-14 | 15-19 | 20+ | 平均 | 範囲 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        D2Hist("体数", u => u.Attack, new[] { 0, 3, 5, 7, 10, 15, 20 });
        Console.WriteLine();
        Console.WriteLine("| HP | 0-39 | 40-59 | 60-79 | 80-99 | 100+ | 平均 | 範囲 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
        D2Hist("体数", u => u.MaxHp, new[] { 0, 40, 60, 80, 100 });
        Console.WriteLine();
        Console.WriteLine("| 速さ | 0-3 | 4-5 | 6-7 | 8-9 | 10+ | 平均 | 範囲 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
        D2Hist("体数", u => u.Speed, new[] { 0, 4, 6, 8, 10 });
        Console.WriteLine();
        int nStrong = d2Roster.Count(u => u.Attack >= D2Strong);
        Console.WriteLine($"**攻撃力 {D2Strong} 以上（＝「殴れる駒」）は {nStrong} / {d2RN} 体（{nStrong * 100.0 / d2RN:F1}%）。**");
        Console.WriteLine($"**攻撃力 6 以下は {d2RN - nStrong} 体（{(d2RN - nStrong) * 100.0 / d2RN:F1}%）**——第69期 §11 の数え直し。");
        Console.WriteLine();
        Console.WriteLine("| 攻撃力 7 以上の駒 |");
        Console.WriteLine("|---|");
        Console.WriteLine("| " + string.Join(" / ", d2Roster.Where(u => u.Attack >= D2Strong)
                          .OrderByDescending(u => u.Attack).Select(u => $"{u.Name} {u.Attack}")) + " |");
        Console.WriteLine();

        // ---- 0-2. A / B の紙の計算（超幾何） ----
        Console.WriteLine("## 0-2. P1（A / B）—— 無作為5枚の「殴れる枚数」（超幾何・厳密）");
        Console.WriteLine();
        double Comb(int n, int r)
        {
            if (r < 0 || r > n) return 0;
            double v = 1;
            for (int k = 0; k < r; k++) v = v * (n - k) / (k + 1);
            return v;
        }
        Console.WriteLine("| 攻7以上の枚数 | 0 | 1 | 2 | 3 | 4 | 5 | 期待値 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        Console.Write("| 確率 |");
        double ev = 0, ge3 = 0, eq0 = 0;
        for (int k = 0; k <= 5; k++)
        {
            double pk = Comb(nStrong, k) * Comb(d2RN - nStrong, 5 - k) / Comb(d2RN, 5);
            ev += k * pk;
            if (k >= 3) ge3 += pk;
            if (k == 0) eq0 = pk;
            Console.Write($" {pk * 100:F1}% |");
        }
        Console.WriteLine($" **{ev:F2}** |");
        Console.WriteLine();
        Console.WriteLine($"**攻7以上が 0 枚 = {eq0 * 100:F1}% / 3 枚以上 = {ge3 * 100:F1}%。**");
        Console.WriteLine("第69期 §11 が事後に計算した「攻6以下が3枚以上 = 59.5%」は、**同じ分布の裏返し**"
                          + $"（攻{D2Strong}以上が 2 枚以下 = {(1 - ge3) * 100:F1}%）。"); 
        Console.WriteLine();

        // ---- 0-3. C / D の紙の計算（10 万回の抽選シミュレーション・戦闘0回） ----
        const int D2Sim = 100000;
        Console.WriteLine($"## 0-3. P1（C / D）—— 選択ありドラフトの「殴れる枚数」（抽選 {D2Sim:N0} 回・**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("§2-2 の規則（3枚提示 → 攻7以上が2枚未満なら攻撃力最大／以後は最大HP・同値は Id 辞書順）。");
        Console.WriteLine();
        var simRand = new int[6];
        var simDraft = new int[6];
        double atkR = 0, hpR = 0, atkD = 0, hpD = 0;
        for (int i = 0; i < D2Sim; i++)
        {
            var a = D2RandomTeam(i);
            var b = D2DraftTeam(i);
            simRand[a.Count(u => u.Attack >= D2Strong)]++;
            simDraft[b.Count(u => u.Attack >= D2Strong)]++;
            atkR += a.Sum(u => u.Attack); hpR += a.Sum(u => u.MaxHp);
            atkD += b.Sum(u => u.Attack); hpD += b.Sum(u => u.MaxHp);
        }
        Console.WriteLine("| 版 | 0 枚 | 1 | 2 | 3 | 4 | 5 | 期待値 | 総攻の平均 | 総HPの平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        Console.Write("| 無作為（A / B・シム） |");
        for (int k = 0; k <= 5; k++) Console.Write($" {simRand[k] * 100.0 / D2Sim:F1}% |");
        Console.WriteLine($" **{simRand.Select((c, k) => (double)c * k).Sum() / D2Sim:F2}** | {atkR / D2Sim:F1} | {hpR / D2Sim:F0} |");
        Console.Write("| 選択（C / D・シム） |");
        for (int k = 0; k <= 5; k++) Console.Write($" {simDraft[k] * 100.0 / D2Sim:F1}% |");
        Console.WriteLine($" **{simDraft.Select((c, k) => (double)c * k).Sum() / D2Sim:F2}** | {atkD / D2Sim:F1} | {hpD / D2Sim:F0} |");
        Console.WriteLine();
        Console.WriteLine("**上の行が 0-2 の超幾何と一致することが、抽選の実装の検算になる。**");
        Console.WriteLine();

        // ---- 0-4. 弱い波の倍率の根拠 ----
        Console.WriteLine("## 0-4. 弱い波の倍率の根拠（§2-3）");
        Console.WriteLine();
        var idealRows0 = CompareBuilds();
        double idAtk = idealRows0.Average(b => b.F.Occupied().Sum(o => o.Def.Attack));
        double idHp = idealRows0.Average(b => b.F.Occupied().Sum(o => o.Def.MaxHp));
        Console.WriteLine($"理想編成 {idealRows0.Length} 行の 総攻の平均 **{idAtk:F1}** / 総HPの平均 **{idHp:F0}**。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 総攻の平均 | 理想比 | 総HPの平均 | 理想比 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        Console.WriteLine($"| 理想編成 {idealRows0.Length} 行 | {idAtk:F1} | 1.00 | {idHp:F0} | 1.00 |");
        Console.WriteLine($"| A / B（無作為5枚） | {atkR / D2Sim:F1} | **{(atkR / D2Sim) / idAtk:F2}** | {hpR / D2Sim:F0} | {(hpR / D2Sim) / idHp:F2} |");
        Console.WriteLine($"| C / D（選択5枚） | {atkD / D2Sim:F1} | **{(atkD / D2Sim) / idAtk:F2}** | {hpD / D2Sim:F0} | {(hpD / D2Sim) / idHp:F2} |");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵の総HP | 総HP × 0.6 | 敵の総攻 | 体数 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int w = 0; w < d2W; w++)
        {
            int hp = d2Base[w].Enemy.Occupied().Sum(o => o.Def.MaxHp);
            int at = d2Base[w].Enemy.Occupied().Sum(o => o.Def.Attack);
            Console.WriteLine($"| {d2Base[w].Name} | {hp} | {d2Weak[w].Enemy.Occupied().Sum(o => o.Def.MaxHp)} | {at} | {d2Base[w].Enemy.Count} |");
        }
        Console.WriteLine();
        Console.WriteLine("**倍率の決め方（測る前に固定）**。勝敗の第一近似は「敵を削り切るのが先か、");
        Console.WriteLine("自分が削り切られるのが先か」なので:");
        Console.WriteLine();
        Console.WriteLine("    敵を倒すまで  T_kill = 敵の総HP ÷ 味方の総攻");
        Console.WriteLine("    味方が落ちるまで T_die  = 味方の総HP ÷ 敵の総攻");
        Console.WriteLine("    勝つ条件 T_kill < T_die  ⇔  敵の総HP < (味方の総攻 × 味方の総HP) ÷ 敵の総攻");
        Console.WriteLine();
        Console.WriteLine("**余裕は「味方の総攻 × 味方の総HP」に比例する**ので、理想編成と同じ余裕を与える倍率は");
        Console.WriteLine("**総攻の理想比 × 総HPの理想比**になる（総攻だけで決めると耐久の不足を見落とす）:");
        Console.WriteLine();
        double eqR = (atkR / D2Sim) / idAtk * ((hpR / D2Sim) / idHp);
        double eqD = (atkD / D2Sim) / idAtk * ((hpD / D2Sim) / idHp);
        Console.WriteLine($"    無作為（A / B）: {(atkR / D2Sim) / idAtk:F2} × {(hpR / D2Sim) / idHp:F2} = **{eqR:F2}**");
        Console.WriteLine($"    選択  （C / D）: {(atkD / D2Sim) / idAtk:F2} × {(hpD / D2Sim) / idHp:F2} = **{eqD:F2}**");
        Console.WriteLine();
        Console.WriteLine($"**採用する倍率 = {D2WeakPct}%**（コードに固定してある）。**等価点 {eqR:F2} より一段下**に置いた——");
        Console.WriteLine("理由は**総攻が打点の代理でしかない**こと。毒・反撃・墓守・破裂は総攻に1点も現れないが、");
        Console.WriteLine("理想編成 61 行はそこで出力を稼いでいる（第57期の燃焼軸・第13期の受け手側集計）。");
        Console.WriteLine($"**同じ総攻でも理想編成のほうが実際の打点は大きいので、真の等価点は {eqR:F2} より下にある。**");
        Console.WriteLine();
        Console.WriteLine("**倍率は1つだけ。版ごとに変えない**——変えると B−A と D−C が別の量になり、");
        Console.WriteLine("2×2 の分解が成り立たなくなる。**A / C は等倍のまま**なので、");
        Console.WriteLine($"選択ありの等価点が {eqD:F2}（＝ほぼ 1.00）であることは「C は理想編成と同じ静的条件で戦う」を意味する。");
        Console.WriteLine();
        Console.WriteLine("**この倍率が直さないもの（測る前に書いておく）**: 敵の攻撃力・速さ・特性はそのままなので、");
        Console.WriteLine("**HP を下げても縮むのは T_kill だけで、T_die は1ターンも伸びない。**");
        Console.WriteLine("「削り切る前に全滅する」側の床——**特に第二波（敵の総攻 84・味方の総HP 328）**——は残る。");
        Console.WriteLine();
        Console.WriteLine("**静的な数値の上では、選択ありの編成は理想編成と見分けが付かない**");
        Console.WriteLine($"（総攻 {atkD / D2Sim:F1} 対 {idAtk:F1} / 総HP {hpD / D2Sim:F0} 対 {idHp:F0}）。");
        Console.WriteLine("**C の中間帯が低ければ、その差は数値ではなく噛み合わせと配置にある**——これは測る前に言える。");
        Console.WriteLine();

        // ---- 0-5. N と M ----
        Console.WriteLine("## 0-5. N と M の決め方（**どちらの条件で決めたかを明記する**）");
        Console.WriteLine();
        Console.WriteLine("この期の主目的は**中間帯の割合**（Q1・Q2・Q3）なので、指示書 §1-4 の**緩いほうの条件**で足りる:");
        Console.WriteLine();
        Console.WriteLine("    割合の 95% 信頼区間の半幅 = 1.96 × √(p(1−p)/N) ≤ 2.0%");
        Console.WriteLine("    最悪の p = 0.5 で  N ≥ 1.96² × 0.25 / 0.02² = 2401");
        Console.WriteLine();
        Console.WriteLine("**それでも N = 11,000 を採る。** 理由は3つで、どれも精度ではなく比較可能性の話:");
        Console.WriteLine();
        Console.WriteLine("1. **A 版が第69期の H 版とまったく同じ標本になる**（系列も N も M も同じ）ので、");
        Console.WriteLine("   波ごとの値が再現することが器具の検算になる");
        Console.WriteLine("2. Q4（駒ごとの値）と Q5（枚数効果）は**駒ごと・キーごとに分母が割れる**");
        Console.WriteLine($"   ——1駒あたりの在席標本数は N × 5 / {d2RN} なので、N = 2401 だと 235 標本しか無い");
        Console.WriteLine("3. 戦闘が足りている（下の見積もり）");
        Console.WriteLine();
        Console.WriteLine($"    中間帯の半幅（N = {D2N}・最悪の p = 0.5）: ±{D2Z * Math.Sqrt(0.25 / D2N) * 100:F2}%");
        Console.WriteLine($"    1駒あたりの在席標本数:                 {D2N * 5.0 / d2RN:F0}");
        Console.WriteLine();
        long mainB = (long)D2N * D2V * d2W * D2M;
        Console.WriteLine($"主表1帯の戦闘数: **{mainB:N0}**（{D2N:N0} 標本 × {D2V} 版 × {d2W} 波 × seed {D2M} 本）。");
        Console.WriteLine($"表D（素体差し替え）を1版ぶん足すと **+{(long)D2N * 5 * d2W * D2M:N0}**。");
        Console.WriteLine();
        Console.WriteLine($"所要 {d2Sw.Elapsed.TotalSeconds:F1} 秒（**戦闘 0 回**）。");
        return;
    }

    // ======================================================================================
    // check: 陰性対照（Q6）
    // ======================================================================================
    if (d2Arg == "check")
    {
        Console.WriteLine("# 第70期 —— 陰性対照（Q6）");
        Console.WriteLine();
        Console.WriteLine("## (1) `Stages` を書き換えていないこと");
        Console.WriteLine();
        Console.WriteLine("弱い波は診断のローカルの複製で、`EnemyCatalog.Stages` は読むだけ。");
        Console.WriteLine("**複製の側だけが HP を落とし、他の列は1つも動いていない**ことを直に照合する。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 体数 | HP（既存→弱） | 攻の一致 | 速の一致 | 特性の一致 | 型の一致 | 席の一致 |");
        Console.WriteLine("|---|--:|---|:-:|:-:|:-:|:-:|:-:|");
        for (int w = 0; w < d2W; w++)
        {
            var a = d2Base[w].Enemy.Occupied().ToArray();
            var b = d2Weak[w].Enemy.Occupied().ToArray();
            bool atk = a.Length == b.Length && a.Zip(b).All(z => z.First.Def.Attack == z.Second.Def.Attack);
            bool spd = a.Zip(b).All(z => z.First.Def.Speed == z.Second.Def.Speed);
            bool tr = a.Zip(b).All(z => z.First.Def.Traits.SequenceEqual(z.Second.Def.Traits));
            bool pt = a.Zip(b).All(z => z.First.Def.Pattern == z.Second.Def.Pattern);
            bool sl = a.Zip(b).All(z => z.First.Slot == z.Second.Slot);
            Console.WriteLine($"| {d2Base[w].Name} | {a.Length} | {a.Sum(x => x.Def.MaxHp)} → {b.Sum(x => x.Def.MaxHp)} "
                              + $"| {(atk ? "○" : "×")} | {(spd ? "○" : "×")} | {(tr ? "○" : "×")} "
                              + $"| {(pt ? "○" : "×")} | {(sl ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine("**敵側に召喚・分裂・蘇生を持つ駒は0体**なので、戦闘中に等倍の敵が湧く経路は無い:");
        var summonish = new[] { TraitId.Splitter, TraitId.Reviver, TraitId.Necro };
        int sm = d2Base.SelectMany(st => st.Enemy.Occupied()).Count(o => o.Def.Traits.Any(t => summonish.Contains(t)));
        Console.WriteLine($"- `Splitter` / `Reviver` / `Necro` を持つ敵: **{sm} 体**");
        Console.WriteLine();

        Console.WriteLine("## (2) 抽選が決定的で、規則配置が5席に割れること");
        Console.WriteLine();
        Console.WriteLine("無作為5枚の抽選も規則配置も第69期 `draft` の写しで、系列も `Random(1000000 + 標本番号)` で同じ。");
        Console.WriteLine("**本当の検算は表A の A 版の波ごとの値が第69期 表A の H 版と一致すること**で、それは主表で見る");
        Console.WriteLine("（第69期 H: 92.16 / 3.41 / 10.18 / 10.08 / 4.69%）。ここでは決定性と席の妥当性だけを数える。");
        Console.WriteLine();
        int mism = 0, seatMism = 0;
        for (int i = 0; i < 2000; i++)
        {
            var u = D2RandomTeam(i);
            var rng = new Random(1_000_000 + i);
            var idx = new int[d2RN];
            for (int k = 0; k < d2RN; k++) idx[k] = k;
            for (int k = 0; k < 5; k++) { int j = k + rng.Next(d2RN - k); (idx[k], idx[j]) = (idx[j], idx[k]); }
            if (!Enumerable.Range(0, 5).All(k => ReferenceEquals(u[k], d2Roster[idx[k]]))) mism++;
            var s2 = D2Seats(u);
            if (s2.Distinct().Count() != 5) seatMism++;
        }
        Console.WriteLine($"- 標本の食い違い **{mism} 件 / 2000** / 席が5つに割れなかった標本 **{seatMism} 件**");
        Console.WriteLine();

        Console.WriteLine("## (3) 選択ありドラフトが規則どおりに動くこと");
        Console.WriteLine();
        Console.WriteLine("**重複なし**（5枚がすべて別の駒）と、**規則の前半が実際に効いていること**");
        Console.WriteLine($"（攻{D2Strong}以上が 2 枚に届くまでは攻撃力最大を採る）を 5000 標本で数える。");
        Console.WriteLine();
        int dup = 0; var strongDist = new int[6];
        for (int i = 0; i < 5000; i++)
        {
            var u = D2DraftTeam(i);
            if (u.Select(x => x.Id).Distinct().Count() != 5) dup++;
            strongDist[u.Count(x => x.Attack >= D2Strong)]++;
        }
        Console.WriteLine($"- 重複のある標本 **{dup} 件 / 5000**");
        Console.WriteLine($"- 攻{D2Strong}以上が **0枚 {strongDist[0]} / 1枚 {strongDist[1]} / 2枚 {strongDist[2]} "
                          + $"/ 3枚 {strongDist[3]} / 4枚 {strongDist[4]} / 5枚 {strongDist[5]}**");
        Console.WriteLine($"- **2枚未満の標本は {strongDist[0] + strongDist[1]} 件**"
                          + "（提示3枚が全部攻6以下だと規則を守っても2枚に届かない。**規則の上限がここに出る**）");
        Console.WriteLine();

        Console.WriteLine("## (4) 盤面");
        Console.WriteLine();
        Console.WriteLine("`draft2` は `BattleEngine.Run` を読むだけで、`Traits.cs` / `UnitCatalog` / `Stages` /");
        Console.WriteLine("`CompareBuilds()` を1行も動かしていない。`compare` / `dump` / `engage` の再生成と");
        Console.WriteLine("`docs/` の diff は別に取る（報告書 §1-4）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {d2Sw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ======================================================================================
    // 主表（表A〜E）
    // ======================================================================================
    {
        bool alt = d2Arg == "alt";
        int band = alt ? D2BandB : D2BandA;
        string bandName = alt ? "B" : "A";

        // ---- 走査 ----
        var d2Full = new int[D2N][];      // [標本][版 * 波数 + 波] 勝数
        var d2Mem = new int[D2N][];       // [標本][版種 * 5 + k] 抽選された駒（0=無作為 / 1=選択）
        int done = 0;
        Console.Error.Write($"{bandName}帯 主表: ");
        Parallel.For(0, D2N, i =>
        {
            var fw = new int[D2V * d2W];
            var mm = new int[10];
            var teams = new[] { D2RandomTeam(i), D2DraftTeam(i) };
            var seats = new[] { D2Seats(teams[0]), D2Seats(teams[1]) };
            for (int t = 0; t < 2; t++)
                for (int k = 0; k < 5; k++) mm[t * 5 + k] = Array.IndexOf(d2Roster, teams[t][k]);
            for (int ver = 0; ver < D2V; ver++)
            {
                int tk = ver < 2 ? 0 : 1;
                Formation f = D2Form(teams[tk], seats[tk], -1, null);
                for (int w = 0; w < d2W; w++)
                    for (int seed = band; seed < band + D2M; seed++)
                        if (BattleEngine.Run(f, D2Enemy(ver, w), seed, verbose: false).PlayerWon)
                            fw[ver * d2W + w]++;
            }
            d2Full[i] = fw; d2Mem[i] = mm;
            int c = Interlocked.Increment(ref done);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        // 標本の勝率（**5波すべて**）
        double D2Rate(int i, int ver)
        {
            int s2 = 0;
            for (int w = 0; w < d2W; w++) s2 += d2Full[i][ver * d2W + w];
            return s2 * 100.0 / (d2W * D2M);
        }
        var rate = new double[D2V][];
        for (int v = 0; v < D2V; v++) rate[v] = Enumerable.Range(0, D2N).Select(i => D2Rate(i, v)).ToArray();

        // **第2〜5波だけの集計**（第69期と同じ切り方）。**判定には使わない**——指示書 §2-1 は
        // 「第一波を除外しない」と書いており、Q1〜Q3 はそちらで判定する。
        // 併記するのは、**第一波が天井（平均 93〜99%）なので 5波の集計に混ぜると
        // 「第一波しか勝てない編成」が 1/5 = 20% ちょうど＝中間帯の内側に落ちる**ため
        // ——同じ戦闘を別の切り方で見た値を並べないと、その効きが読めない。
        double D2Rate25(int i, int ver)
        {
            int s3 = 0;
            for (int w = 1; w < d2W; w++) s3 += d2Full[i][ver * d2W + w];
            return s3 * 100.0 / ((d2W - 1) * D2M);
        }
        var rate25 = new double[D2V][];
        for (int v = 0; v < D2V; v++) rate25[v] = Enumerable.Range(0, D2N).Select(i => D2Rate25(i, v)).ToArray();

        Console.WriteLine($"# 第70期 —— ドラフト台の 2×2（{bandName} 帯 seed {band}..{band + D2M - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 draft2{(alt ? " alt" : "")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"標本 **{D2N:N0}** × 版 **{D2V}** × {d2W} 波 × seed **{D2M}** 本 = **{(long)D2N * D2V * d2W * D2M:N0} 戦**。");
        Console.WriteLine($"配置は**規則配置（H）のみ**。**第一波を含む5波すべてを集計**（標本の勝率は {d2W * D2M} 戦）。");
        Console.WriteLine($"弱い波は敵の `MaxHp` を **{D2WeakPct}%**（切り捨て）にした複製で、**他の列は1つも動かしていない**。");
        Console.WriteLine();

        // ---- 表A ----
        Console.WriteLine("## 表A —— 4版 × 波 の分布");
        Console.WriteLine();
        Console.WriteLine("| 版 | 対象 | 平均 | =0% | =100% | **中間帯 5〜95%** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int v = 0; v < D2V; v++)
        {
            for (int w = 0; w < d2W; w++)
            {
                var xs = Enumerable.Range(0, D2N).Select(i => d2Full[i][v * d2W + w] * 100.0 / D2M).ToArray();
                Console.WriteLine($"| {d2VerName[v]} | 第{w + 1}波 | {xs.Average():F2}% "
                                  + $"| {xs.Count(x => x == 0) * 100.0 / D2N:F1}% | {xs.Count(x => x == 100) * 100.0 / D2N:F1}% "
                                  + $"| {D2Mid(xs):F1}% |");
            }
            Console.WriteLine($"| {d2VerName[v]} | **全体（5波・判定に使う）** | **{rate[v].Average():F2}%** "
                              + $"| **{rate[v].Count(x => x == 0) * 100.0 / D2N:F1}%** | **{rate[v].Count(x => x == 100) * 100.0 / D2N:F1}%** "
                              + $"| **{D2Mid(rate[v]):F1}%** |");
            Console.WriteLine($"| {d2VerName[v]} | 第2〜5波（第69期の切り方・**併記**） | {rate25[v].Average():F2}% "
                              + $"| {rate25[v].Count(x => x == 0) * 100.0 / D2N:F1}% | {rate25[v].Count(x => x == 100) * 100.0 / D2N:F1}% "
                              + $"| {D2Mid(rate25[v]):F1}% |");
        }
        Console.WriteLine();
        var mid = Enumerable.Range(0, D2V).Select(v => D2Mid(rate[v])).ToArray();
        double half = D2Z * Math.Sqrt(0.25 / D2N) * 100;
        int best = Array.IndexOf(mid, mid.Max());
        bool anyPass = mid.Any(x => x >= 60);
        Console.WriteLine($"**Q1**: 中間帯が 60% 以上の版は **{mid.Count(x => x >= 60)} 個**"
                          + $"（最大は {d2VerShort[best]} の **{mid[best]:F1}%**・割合の 95% 信頼区間は ±{half:F2}%）"
                          + $" → **{(anyPass ? d2VerShort[best] + " を採る" : "×（使える台は無い）")}**");
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B —— 2×2 の分解（中間帯の割合）");
        Console.WriteLine();
        Console.WriteLine("| | 既存5波 | 弱い波 | 波の寄与（弱 − 既存） |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| 無作為5枚 | A **{mid[0]:F1}%** | B **{mid[1]:F1}%** | {D2P2(mid[1] - mid[0])} |");
        Console.WriteLine($"| 選択あり | C **{mid[2]:F1}%** | D **{mid[3]:F1}%** | {D2P2(mid[3] - mid[2])} |");
        Console.WriteLine($"| **編成の作り方の寄与（選択 − 無作為）** | {D2P2(mid[2] - mid[0])} | {D2P2(mid[3] - mid[1])} | |");
        Console.WriteLine();
        Console.WriteLine("| 量 | 値 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| **B − A**（波の寄与） | **{D2P2(mid[1] - mid[0])} ポイント** |");
        Console.WriteLine($"| **C − A**（編成の作り方の寄与） | **{D2P2(mid[2] - mid[0])} ポイント** |");
        Console.WriteLine($"| **D − A**（両方） | **{D2P2(mid[3] - mid[0])} ポイント** |");
        Console.WriteLine($"| (B−A) + (C−A)（独立なら D−A と一致する） | {D2P2((mid[1] - mid[0]) + (mid[2] - mid[0]))} |");
        Console.WriteLine($"| **交互作用 = (D−A) − (B−A) − (C−A)** | **{D2P2((mid[3] - mid[0]) - (mid[1] - mid[0]) - (mid[2] - mid[0]))}** |");
        Console.WriteLine();
        string d2Cause = Math.Abs(mid[1] - mid[0]) > Math.Abs(mid[2] - mid[0]) ? "波" : "編成の作り方";
        Console.WriteLine($"**Q2**: |B−A| = {Math.Abs(mid[1] - mid[0]):F1} 対 |C−A| = {Math.Abs(mid[2] - mid[0]):F1} "
                          + $"→ **主因は{d2Cause}**");
        Console.WriteLine($"**Q3**: C − A = {D2P2(mid[2] - mid[0])} ポイント（判定は +10 以上）"
                          + $" → **{(mid[2] - mid[0] >= 10 ? "○" : "×")}**");
        Console.WriteLine();
        Console.WriteLine("### 第2〜5波だけで数えた同じ分解（**併記**。判定は上の5波の側）");
        Console.WriteLine();
        Console.WriteLine("第一波は平均 93〜99% の天井なので、5波の集計に混ぜると");
        Console.WriteLine("**「第一波しか勝てない編成」がちょうど 1/5 = 20% ＝中間帯の内側**に落ちる。");
        Console.WriteLine("同じ戦闘を第69期と同じ切り方でも数えて並べる。");
        Console.WriteLine();
        var mid25 = Enumerable.Range(0, D2V).Select(v => D2Mid(rate25[v])).ToArray();
        Console.WriteLine("| | 既存5波 | 弱い波 | 波の寄与 |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| 無作為5枚 | A **{mid25[0]:F1}%** | B **{mid25[1]:F1}%** | {D2P2(mid25[1] - mid25[0])} |");
        Console.WriteLine($"| 選択あり | C **{mid25[2]:F1}%** | D **{mid25[3]:F1}%** | {D2P2(mid25[3] - mid25[2])} |");
        Console.WriteLine($"| **編成の作り方の寄与** | {D2P2(mid25[2] - mid25[0])} | {D2P2(mid25[3] - mid25[1])} | |");
        Console.WriteLine();
        Console.WriteLine($"**A の {mid25[0]:F1}% は第69期 表A の H 版の中間帯 18.3% と一致するはず**（同じ標本・同じ切り方）。");
        Console.WriteLine();
        Console.WriteLine($"| 量 | 5波（判定） | 第2〜5波（併記） |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| B − A | {D2P2(mid[1] - mid[0])} | **{D2P2(mid25[1] - mid25[0])}** |");
        Console.WriteLine($"| C − A | {D2P2(mid[2] - mid[0])} | **{D2P2(mid25[2] - mid25[0])}** |");
        Console.WriteLine($"| D − A | {D2P2(mid[3] - mid[0])} | **{D2P2(mid25[3] - mid25[0])}** |");
        Console.WriteLine($"| 交互作用 | {D2P2((mid[3] - mid[0]) - (mid[1] - mid[0]) - (mid[2] - mid[0]))} "
                          + $"| **{D2P2((mid25[3] - mid25[0]) - (mid25[1] - mid25[0]) - (mid25[2] - mid25[0]))}** |");
        Console.WriteLine($"| 60% 以上の版 | {mid.Count(x => x >= 60)} | **{mid25.Count(x => x >= 60)}** |");
        Console.WriteLine();

        Console.WriteLine("### 平均勝率でも同じ分解を出す（中間帯は分布の形、平均は水準）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 中間帯（ポイント） | 平均勝率（pt） |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| B − A | {D2P2(mid[1] - mid[0])} | {D2P2(rate[1].Average() - rate[0].Average())} |");
        Console.WriteLine($"| C − A | {D2P2(mid[2] - mid[0])} | {D2P2(rate[2].Average() - rate[0].Average())} |");
        Console.WriteLine($"| D − A | {D2P2(mid[3] - mid[0])} | {D2P2(rate[3].Average() - rate[0].Average())} |");
        Console.WriteLine($"| 交互作用 | {D2P2((mid[3] - mid[0]) - (mid[1] - mid[0]) - (mid[2] - mid[0]))} "
                          + $"| {D2P2((rate[3].Average() - rate[0].Average()) - (rate[1].Average() - rate[0].Average()) - (rate[2].Average() - rate[0].Average()))} |");
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C —— 編成の中身の分布（Phase 0 の紙の計算との照合）");
        Console.WriteLine();
        Console.WriteLine("**A / B は同じ編成・C / D も同じ編成**（違うのは波だけ）なので、行は2つで足りる。");
        Console.WriteLine();
        Console.WriteLine($"| 編成の作り方 | 攻{D2Strong}以上 0枚 | 1 | 2 | 3 | 4 | 5 | 期待値 | 総攻 | 総HP | 相方を持つ駒の割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int t = 0; t < 2; t++)
        {
            var dist = new int[6];
            double atk = 0, hp = 0, pal = 0;
            for (int i = 0; i < D2N; i++)
            {
                var mem = Enumerable.Range(0, 5).Select(k => d2Roster[d2Mem[i][t * 5 + k]]).ToArray();
                dist[mem.Count(u => u.Attack >= D2Strong)]++;
                atk += mem.Sum(u => u.Attack);
                hp += mem.Sum(u => u.MaxHp);
                for (int k = 0; k < 5; k++)
                {
                    int[] mk = d2KeyOf[mem[k].Id];
                    bool has = false;
                    for (int j = 0; j < 5 && !has; j++)
                        if (j != k && d2KeyOf[mem[j].Id].Any(x => mk.Contains(x))) has = true;
                    if (has) pal++;
                }
            }
            Console.Write($"| {(t == 0 ? "無作為5枚（A / B）" : "選択あり（C / D）")} |");
            for (int k = 0; k <= 5; k++) Console.Write($" {dist[k] * 100.0 / D2N:F1}% |");
            Console.WriteLine($" **{dist.Select((c, k) => (double)c * k).Sum() / D2N:F2}** "
                              + $"| {atk / D2N:F1} | {hp / D2N:F0} | {pal * 100.0 / (D2N * 5):F1}% |");
        }
        Console.WriteLine();

        // ---- 表E（枚数効果・全4版） ----
        Console.WriteLine("## 表E —— 枚数効果（キーごと・全4版）");
        Console.WriteLine();
        Console.WriteLine("その標本にそのキーを持つ駒が何枚いたかで分けた平均勝率。");
        Console.WriteLine("**第69期の「正なのは移動と被弾の2本だけ」が再現するか**を見る（Q5）。");
        Console.WriteLine();
        for (int v = 0; v < D2V; v++)
        {
            int tk = v < 2 ? 0 : 1;
            Console.WriteLine($"### {d2VerName[v]}");
            Console.WriteLine();
            Console.WriteLine("| キー | 0枚 | n | 1枚 | n | 2枚 | n | 3枚以上 | n | 2枚 − 1枚 | 1枚 − 0枚 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int k = 0; k < UnitTally.CarryKeys.Length; k++)
            {
                var bk = new List<double>[4];
                for (int b = 0; b < 4; b++) bk[b] = new List<double>();
                for (int i = 0; i < D2N; i++)
                {
                    int c = 0;
                    for (int q = 0; q < 5; q++) if (d2KeyOf[d2Roster[d2Mem[i][tk * 5 + q]].Id].Contains(k)) c++;
                    bk[Math.Min(c, 3)].Add(rate[v][i]);
                }
                string Cell(int b) => bk[b].Count == 0 ? "—" : $"{bk[b].Average():F2}%";
                string Del(int a2, int b2) => (bk[a2].Count == 0 || bk[b2].Count == 0) ? "—"
                    : D2P2(bk[a2].Average() - bk[b2].Average());
                Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {Cell(0)} | {bk[0].Count} | {Cell(1)} | {bk[1].Count} "
                                  + $"| {Cell(2)} | {bk[2].Count} | {Cell(3)} | {bk[3].Count} "
                                  + $"| **{Del(2, 1)}** | {Del(1, 0)} |");
            }
            Console.WriteLine();
        }

        // ---- 表D（Q1 を満たした版。無ければ中間帯が最大の版を「参考」で1つだけ） ----
        //
        // **どの版で出すかの規則は測る前に固定してある**——Q1 を満たした版すべて、
        // 満たす版が1つも無ければ**中間帯が最大の版を1つだけ**「参考」として出す
        // （Q4 は「判定不能」と書く。結果を見てから対象を選ばない）。
        var showD = anyPass
            ? Enumerable.Range(0, D2V).Where(v => mid[v] >= 60).ToArray()
            : new[] { best };

        Console.WriteLine("## 表D —— 駒ごとの在席時勝率と寄与");
        Console.WriteLine();
        Console.WriteLine($"出すのは **{string.Join(" / ", showD.Select(v => d2VerShort[v]))}**"
                          + $"（{(anyPass ? "Q1 を満たした版" : "**Q1 を満たす版が無いので、中間帯が最大の版を参考として1つだけ**")}）。");
        Console.WriteLine();
        Console.WriteLine("**寄与 = 標本の勝率 − その駒を素体（同数値・特性なし）に差し替えた版の勝率。**");
        Console.WriteLine("**器具は素体差し替えに統一してある**（第69期の発見。1枚抜きとは別物なので混ぜない）。");
        Console.WriteLine("理想61行の側も**同じ器具・同じ集計（5波すべて・seed 0..199）**で測り直した。");
        Console.WriteLine();

        // 理想61行（同じ実行の中で測り直す）
        var idealRows = CompareBuilds();
        var idFull = new double[idealRows.Length];
        Parallel.For(0, idealRows.Length, r =>
        {
            int win = 0;
            for (int w = 0; w < d2W; w++)
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(idealRows[r].F, d2Base[w].Enemy, seed, verbose: false).PlayerWon) win++;
            idFull[r] = win * 100.0 / (d2W * 200);
        });
        var idJobs = new List<(int Row, int Unit, int Slot)>();
        for (int r = 0; r < idealRows.Length; r++)
            foreach ((int slot, UnitDef d) in idealRows[r].F.Occupied())
            {
                int ui0 = Array.FindIndex(d2Roster, x => x.Id == d.Id);
                if (ui0 >= 0) idJobs.Add((r, ui0, slot));   // `All` に無い駒は素体を作れないので外す
            }
        var idPlain = new double[idJobs.Count];
        Parallel.For(0, idJobs.Count, j =>
        {
            (int r, int ui, int slot) = idJobs[j];
            var pf = idealRows[r].F.Clone();
            pf[slot] = d2Plain[ui];
            int win = 0;
            for (int w = 0; w < d2W; w++)
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(pf, d2Base[w].Enemy, seed, verbose: false).PlayerWon) win++;
            idPlain[j] = win * 100.0 / (d2W * 200);
        });
        var idSeat = new List<double>[d2RN];
        var idCon = new List<double>[d2RN];
        for (int u = 0; u < d2RN; u++) { idSeat[u] = new List<double>(); idCon[u] = new List<double>(); }
        for (int j = 0; j < idJobs.Count; j++)
        {
            (int r, int ui, _) = idJobs[j];
            if (ui < 0) continue;
            idSeat[ui].Add(idFull[r]);
            idCon[ui].Add(idFull[r] - idPlain[j]);
        }

        foreach (int v in showD)
        {
            int tk = v < 2 ? 0 : 1;
            // 素体差し替えの走査（その版だけ）
            var plainWin = new int[D2N][];
            int dn = 0;
            Console.Error.Write($"{bandName}帯 表D({d2VerShort[v]}): ");
            Parallel.For(0, D2N, i =>
            {
                var team = tk == 0 ? D2RandomTeam(i) : D2DraftTeam(i);
                var seats = D2Seats(team);
                var pw = new int[5];
                for (int k = 0; k < 5; k++)
                {
                    var g = D2Form(team, seats, k, d2Plain[d2Mem[i][tk * 5 + k]]);
                    for (int w = 0; w < d2W; w++)
                        for (int seed = band; seed < band + D2M; seed++)
                            if (BattleEngine.Run(g, D2Enemy(v, w), seed, verbose: false).PlayerWon) pw[k]++;
                }
                plainWin[i] = pw;
                int c = Interlocked.Increment(ref dn);
                if (c % 1000 == 0) Console.Error.Write(".");
            });
            Console.Error.WriteLine();

            var seatRate = new List<double>[d2RN];
            var conRate = new List<double>[d2RN];
            for (int u = 0; u < d2RN; u++) { seatRate[u] = new List<double>(); conRate[u] = new List<double>(); }
            for (int i = 0; i < D2N; i++)
                for (int k = 0; k < 5; k++)
                {
                    int u = d2Mem[i][tk * 5 + k];
                    seatRate[u].Add(rate[v][i]);
                    conRate[u].Add(rate[v][i] - plainWin[i][k] * 100.0 / (d2W * D2M));
                }

            Console.WriteLine($"### {d2VerName[v]}");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 攻 | キー | 在席 | 在席時勝率 | ±95% | 寄与 | ±95% | 理想·在席時勝率 | 理想·寄与 | 行数 |");
            Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|");
            var xs1 = new List<double>(); var ys1 = new List<double>();
            var xs2 = new List<double>(); var ys2 = new List<double>();
            foreach (int u in Enumerable.Range(0, d2RN).OrderByDescending(u => D2Stats(conRate[u]).Mean))
            {
                var a = D2Stats(seatRate[u]); var b = D2Stats(conRate[u]);
                string keys = string.Join("・", d2KeyOf[d2Roster[u].Id].Select(k => UnitTally.CarryKeys[k]));
                string idS = idSeat[u].Count > 0 ? $"{idSeat[u].Average():F1}%" : "—";
                string idC = idCon[u].Count > 0 ? D2P1(idCon[u].Average()) : "—";
                if (idSeat[u].Count > 0)
                {
                    xs1.Add(a.Mean); ys1.Add(idSeat[u].Average());
                    xs2.Add(b.Mean); ys2.Add(idCon[u].Average());
                }
                Console.WriteLine($"| {d2Roster[u].Name} | {d2Roster[u].Attack} | {(keys.Length == 0 ? "—" : keys)} | {a.N} "
                                  + $"| {a.Mean:F2}% | ±{D2Z * a.Sd / Math.Sqrt(Math.Max(1, a.N)):F2} "
                                  + $"| **{D2P1(b.Mean)}** | ±{D2Z * b.Sd / Math.Sqrt(Math.Max(1, b.N)):F2} "
                                  + $"| {idS} | {idC} | {idSeat[u].Count} |");
            }
            Console.WriteLine();
            Console.WriteLine("| 散布（理想61行との相関） | r | n |");
            Console.WriteLine("|---|--:|--:|");
            Console.WriteLine($"| **在席時勝率どうし**（指示書 Q4 の字義） | **{D2Corr(xs1, ys1):F3}** | {xs1.Count} |");
            Console.WriteLine($"| **寄与どうし**（第69期の 0.354 と比べられる形） | **{D2Corr(xs2, ys2):F3}** | {xs2.Count} |");
            Console.WriteLine();
            double r4 = D2Corr(xs1, ys1);
            Console.WriteLine($"**Q4（{d2VerShort[v]}）**: 在席時勝率どうしの r = **{r4:F3}**"
                              + $" → **{(r4 < 0.7 ? "台が違う（r < 0.7）" : "台が同じ側（r ≥ 0.7）")}**"
                              + $"{(anyPass ? "" : "。**ただし Q1 を満たしていない版なので参考値**")}");
            Console.WriteLine();
        }

        Console.WriteLine($"所要 {d2Sw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }
}
}
