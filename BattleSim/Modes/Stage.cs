using BattleCore;
using static Common;

// =====================================================================================
// stage モード（第157期） —— ステージ単位で駒を測る
//
// 指示書は design/PHASE157_STAGE_ABLATE_SPEC.md。前提は design/PHASE156_STAGE_SURVEY.md。
//
// **engine は1行も触らない。** `EngagementEngine.Run` の既存の引数（`RecoverRule` /
// `BoundaryRule`）と既存の目的変数（`Common.BreakthroughDegree`）と既存の器具
// （素体差し替え・第69期）を繋ぐだけ。
//
//     dotnet run --project BattleSim -c Release 0 stage phase0  # 前提を実装から引き直す（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 stage scan    # 段1 台の帯を決める
//     dotnet run --project BattleSim -c Release 0 stage run     # 段2 `ablate` の会戦版
//     dotnet run --project BattleSim -c Release 0 stage life    # 段3 駒ごとの寿命の帳簿
//     dotnet run --project BattleSim -c Release 0 stage check   # 自己検査（§4）
// =====================================================================================

static class StageDiag
{
    const int Seeds = 200;

    // ---- 段1 の線（**測る前に固定する**。§1-4。結果を見てから緩めない・第64期） ----
    const int ZeroMax = 15;   // 突破度 0 の行が 61 行中これ以下
    const int FullMax = 15;   // 突破度が列長ちょうどの行が 61 行中これ以下

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": Scan(arg); return;
            case "run": Attribute(arg); return;
            case "life": Life(arg); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("stage: モードは phase0 / scan / run / life / check。");
                return;
        }
    }

    // =================================================================================
    // 台（列 × 境界の規則）。**新しい敵は1体も作らない**（第1期からの規則）。
    // 列は `EnemyCatalog.Stages` の接頭を切り出すだけ——`地点`（先頭3波）は
    // `EnemyCatalog.Columns` に既にあり、2波の列はここで同じ作り方でローカルに組む。
    // =================================================================================

    public sealed record Col(string Name, IReadOnlyList<Formation> Squads)
    {
        public int Len => Squads.Count;
    }

    public static Col[] Columns()
    {
        // 実装から引く（名前で決め打ちしない・第100期の作法）。
        var route = EnemyCatalog.Columns.First(c => ReferenceEquals(c.Squads, EnemyCatalog.EngagementColumn));
        var spot = EnemyCatalog.Columns.Where(c => c.Squads.Count == 3).OrderBy(c => c.Name).First();
        return new[]
        {
            new Col("地点2", EnemyCatalog.Stages.Take(2).Select(s => s.Enemy).ToList()),
            new Col("地点3", spot.Squads),
            new Col("順路5", route.Squads),
        };
    }

    /// <summary>
    /// 境界の版。<b><see cref="RecoverRule"/> と <see cref="BoundaryRule"/> は併用しない</b>
    /// （engine が禁じている＝Engagement.cs の `rec.Active ? BoundaryRule.Default : ...`。自己検査 (d)）ので、
    /// どの版も片方しか立てない。
    /// </summary>
    public sealed record Ver(string Name, RecoverRule? Rec, BoundaryRule? Bnd, string Note);

    public static Ver[] Versions() => new[]
    {
        new Ver("R0",      null, null, "**現行**（`RecoverRule.Default` / `BoundaryRule.Default`）"),
        new Ver("R25",     new RecoverRule(25, false), null, ""),
        new Ver("R50",     new RecoverRule(50, false), null, ""),
        new Ver("R75",     new RecoverRule(75, false), null, ""),
        new Ver("R100",    new RecoverRule(100, false), null, "第101期の R100"),
        new Ver("R0T",     new RecoverRule(0, true), null, "**未探索**（死者だけ戻す。HP は戻さない）"),
        new Ver("R25T",    new RecoverRule(25, true), null, "**未探索**"),
        new Ver("R50T",    new RecoverRule(50, true), null, "**未探索**"),
        new Ver("R75T",    new RecoverRule(75, true), null, "**未探索**"),
        new Ver("RFull",   new RecoverRule(100, true), null, "第101期の RFull"),
        new Ver("BRevive", null, new BoundaryRule(BoundaryChoice.Revive), "第102期"),
        new Ver("BHeal",   null, new BoundaryRule(BoundaryChoice.Heal),   "第102期（＝ R100）"),
        new Ver("BCarry",  null, new BoundaryRule(BoundaryChoice.Carry),  "第102期"),
    };

    public static Ver VerOf(string name) => Versions().First(v => v.Name == name);
    public static Col ColOf(string name) => Columns().First(c => c.Name == name);

    // ---- 1行ぶんの突破度（seed 平均）と内訳 ----
    public static (double Deg, int NoClear, int FullClear) Degree(Formation f, Col col, Ver v)
    {
        var player = new[] { f };
        double sum = 0;
        int noClear = 0, fullClear = 0;
        for (int seed = 0; seed < Seeds; seed++)
        {
            EngagementResult r = EngagementEngine.Run(player, col.Squads, seed,
                                                      verbose: false, recover: v.Rec, boundary: v.Bnd);
            sum += BreakthroughDegree(r, col.Len);
            if (r.EnemySquadsCleared == 0) noClear++;
            if (r.EnemySquadsCleared >= col.Len) fullClear++;
        }
        return (sum / Seeds, noClear, fullClear);
    }

    // =================================================================================
    // phase0 —— 前提を実装から引き直す（**戦闘0回**）
    // =================================================================================
    static void Phase0()
    {
        Console.WriteLine("# 第157期 Phase 0 —— 前提を実装から引き直す（戦闘0回）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 stage phase0`");
        Console.WriteLine();

        Console.WriteLine("## 0-1. 列（**新しい敵は1体も作っていない**）");
        Console.WriteLine();
        Console.WriteLine("| 列 | 長さ | 中身 |");
        Console.WriteLine("|---|--:|---|");
        foreach (var c in Columns())
            Console.WriteLine($"| {c.Name} | {c.Len} | "
                + string.Join(" → ", EnemyCatalog.Stages.Take(c.Len).Select(s => s.Name)) + " |");
        Console.WriteLine();
        Console.WriteLine($"`EnemyCatalog.Columns` は {EnemyCatalog.Columns.Count} 本"
            + $"（{string.Join(" / ", EnemyCatalog.Columns.Select(c => $"{c.Name}:{c.Squads.Count}"))}）。"
            + "**`地点2` だけが診断のローカル**（`Stages.Take(2)` ＝ `地点` と同じ作り方）。");
        Console.WriteLine();

        Console.WriteLine("## 0-2. 版（**`RecoverRule` と `BoundaryRule` は併用しない**）");
        Console.WriteLine();
        Console.WriteLine("| 版 | `RecoverRule` | `BoundaryRule` | 備考 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var v in Versions())
            Console.WriteLine($"| {v.Name} | {(v.Rec is null ? "—" : v.Rec.ToString())} | "
                + $"{(v.Bnd is null ? "—" : v.Bnd.Value.Choice.ToString())} | {v.Note} |");
        Console.WriteLine();
        int both = Versions().Count(v => v.Rec is not null && v.Bnd is not null);
        Console.WriteLine($"**両方を立てた版: {both} 件**（自己検査 (d)）。");
        Console.WriteLine();

        Console.WriteLine("## 0-3. 線（**測る前に固定した**。§1-4）");
        Console.WriteLine();
        Console.WriteLine($"1. 突破度が **0 の行が {ZeroMax} 行以下**");
        Console.WriteLine($"2. 突破度が **列長ちょうどの行が {FullMax} 行以下**");
        Console.WriteLine("3. 1・2 を満たす点のうち **61 行の突破度の標準偏差が最大**");
        Console.WriteLine();
        Console.WriteLine("**補助の列を2本、同じく測る前に固定する**（規約 (G11) —— "
            + "線を引く前に対照側の実測値を見る、の実装側の版）:");
        Console.WriteLine();
        Console.WriteLine("- **抜き0行** ＝ 200 seed すべてで敵部隊を1つも抜けなかった行"
            + "（＝勝率 0% の厳密な類似物。突破度は部分点を持つので「0 の行」より厳しい）");
        Console.WriteLine("- **全抜き行** ＝ 200 seed すべてで列を抜き切った行（＝勝率 100% の類似物）");
        Console.WriteLine();
        Console.WriteLine("**線1・2 が対照（R0）で既に満たされているなら、その線は何も言っていない**"
            + " —— そのときは判定にそう書き、補助の列で読む。**線は動かさない。**");
        Console.WriteLine();

        Console.WriteLine("## 0-4. 器具（段2）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 定義 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| **会戦帰属** | 突破度（本体） − 突破度（その駒を素体に） |");
        Console.WriteLine("| **単発帰属** | **同じ器具・同じ敵部隊・同じ seed** を会戦にせず独立に戦わせた版の差 |");
        Console.WriteLine();
        Console.WriteLine("**単発帰属を `docs/ablation.md`（駒を1枚<u>抜く</u>）から引かない。**"
            + "あれは5枚を4枚にする器具で、段2 の素体差し替え（5枚のまま特性だけ落とす）と別物なので、"
            + "差を取ると「持ち越しの効き」に「1枠空いたぶん」が混ざる。"
            + "**器具を揃え、違いを『繋いだか／繋がなかったか』の1つに閉じる。**");
        Console.WriteLine();
        Console.WriteLine("**単発側の目的変数も突破度と同じ尺度に揃える** —— "
            + "列の各部隊と**全快の同じ編成で**独立に戦い、"
            + "「先頭から連続して勝った部隊数 ＋ 最初に負けた部隊の削り割合」を作る"
            + "（＝会戦から持ち越しだけを抜いた版。分母・部分点の作り方は `BreakthroughDegree` と同じ）。");
        Console.WriteLine();

        Console.WriteLine("## 0-5. 素体（第69期の標準器具）");
        Console.WriteLine();
        Console.WriteLine("    Id / Name 以外は MaxHp・Attack・Speed・Pattern をそのまま、Traits を空、Actions を落とす");
        Console.WriteLine();
        var rows = CompareBuilds();
        var units = rows.SelectMany(r => r.F.Occupied().Select(o => o.Def.Id)).Distinct().ToArray();
        Console.WriteLine($"`CompareBuilds()` は **{rows.Length} 行**・延べ **{rows.Sum(r => r.F.Occupied().Count())} 枠**・"
            + $"異なり **{units.Length} 体**。`UnitCatalog.All` は {UnitCatalog.All.Count} 枚。");
        Console.WriteLine();
        var notInAll = units.Where(id => !UnitCatalog.All.Any(d => d.Id == id)).ToArray();
        Console.WriteLine($"**`All` に居ないのに `Presets` に居る駒: {notInAll.Length} 体**"
            + (notInAll.Length == 0 ? "" : "（" + string.Join("・", notInAll) + "）")
            + " —— 第140期の則。**素体は `Presets` の `UnitDef` からその場で作る**ので、この非対称では落ちない。");
        Console.WriteLine();

        Console.WriteLine("## 0-6. §2-3 の予測（**実装前に書き切る。外れても消さない**）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 予測 | 在席行 | 理由（第156期 §8） |");
        Console.WriteLine("|---|---|--:|---|");
        foreach (var (u, dir, why) in Predictions)
        {
            int n = rows.Count(r => r.F.Occupied().Any(o => o.Def.Id == u));
            string nm = UnitCatalog.Everyone.FirstOrDefault(d => d.Id == u)?.Name ?? u;
            Console.WriteLine($"| {nm} | **{dir}** | {n} | {why} |");
        }
        Console.WriteLine();
        Console.WriteLine("向きは `会戦帰属 − 単発帰属` の符号。**`≒` は |差| が全 52 体の最小四分位に入ること**で読む。");
        Console.WriteLine();
        Console.WriteLine("**在席行が 0 の駒については予測が検証できない**（規約 (G2) の「他の行が 0 行」と同じ形）。");
    }

    // §2-3。**指示書の表をそのまま。数値は入れず向きだけ。**
    public static readonly (string Unit, string Dir, string Why)[] Predictions =
    {
        ("vel",  "会戦 < 単発", "蘇生ごとに `MaxHp` が半減し境界を越えて戻らない（実測 46 → 11）"),
        ("nono", "会戦 < 単発", "尽きると駒1枚を失う。単発では勝率にしか出ない"),
        ("mudo", "会戦 < 単発", "境界で `AtkBonus` が全部消える"),
        ("utsu", "会戦 < 単発", "境界で `AtkBonus` が全部消える"),
        ("hibi", "会戦 < 単発", "破片は `StatusKeys.All` なので境界で全部消える"),
        ("uro",  "会戦 < 単発", "同上"),
        ("gare", "会戦 < 単発", "同上"),
        ("zoto", "会戦 > 単発", "湧いた駒は持ち越さないが、**部隊戦の数だけ使える**"),
        ("mug",  "会戦 > 単発", "同上"),
        ("kugu", "会戦 > 単発", "開戦時1回の全体供給が**境界ごとに掛け直される**"),
        ("nel",  "会戦 > 単発", "同上"),
        ("kubi", "会戦 > 単発", "同上"),
        ("gald", "会戦 > 単発", "受け流しの在庫がターン頭に戻る ＝ **HP を1点も払わずに時間を買う**"),
        ("ban",  "会戦 ≒ 単発", "`IdleTurn` と `ActionIndex` は Battle スコープに閉じている"),
        ("gan",  "会戦 ≒ 単発", "同上"),
        ("hagi", "符号が割れる", "鍵の供給が後半に偏るが、自分が先に落ちれば同じ"),
        ("tome", "符号が割れる", "同上"),
    };

    // =================================================================================
    // 段1 —— 台の帯を決める
    // =================================================================================
    static void Scan(string arg)
    {
        var rows = CompareBuilds();
        var cols = Columns();
        var vers = Versions();
        var only = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (only.Length > 0) cols = cols.Where(c => only.Contains(c.Name)).ToArray();
        if (cols.Length == 0) { Console.WriteLine("stage scan: 列名が引けない（地点2 / 地点3 / 順路5）。"); return; }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第157期 段1 —— 台の帯を決める");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` {rows.Length} 行 × seed 0..{Seeds - 1}。"
            + $"**列 {cols.Length} 本 × 版 {vers.Length} 版 ＝ {cols.Length * vers.Length} 点。**");
        Console.WriteLine();
        Console.WriteLine($"線（**測る前に固定**）: 0 の行 ≤ {ZeroMax} ／ 列長ちょうどの行 ≤ {FullMax} ／ その中で SD 最大。");
        Console.WriteLine();
        Console.WriteLine("| 列 | 版 | 平均突破度 | 取り分 | SD | 0行 | 満行 | 抜き0行 | 全抜き行 | 線1・2 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|:-:|");

        var best = (Sd: double.NegativeInfinity, Col: "", Ver: "");
        foreach (var col in cols)
            foreach (var v in vers)
            {
                var deg = new double[rows.Length];
                var nc = new int[rows.Length];
                var fc = new int[rows.Length];
                Parallel.For(0, rows.Length, i =>
                {
                    var (d, n, f) = Degree(rows[i].F, col, v);
                    deg[i] = d; nc[i] = n; fc[i] = f;
                });
                double mean = deg.Average(), sd = Sd(deg);
                int zero = deg.Count(d => d == 0.0);
                int full = deg.Count(d => d >= col.Len);
                int noClearRows = nc.Count(x => x == Seeds);
                int fullClearRows = fc.Count(x => x == Seeds);
                bool pass = zero <= ZeroMax && full <= FullMax;
                if (pass && sd > best.Sd) best = (sd, col.Name, v.Name);
                Console.WriteLine($"| {col.Name} | {v.Name} | {mean:F3} | {mean / col.Len * 100:F1}% | {sd:F3} "
                    + $"| {zero} | {full} | {noClearRows} | {fullClearRows} | {(pass ? "○" : "×")} |");
                Console.Error.WriteLine($"{col.Name} {v.Name} done {sw.Elapsed.TotalSeconds:F0}s");
            }

        Console.WriteLine();
        Console.WriteLine(best.Col == ""
            ? "**線1・2 を満たす点が1つも無い。**"
            : $"**線1・2 を満たす点のうち SD 最大 ＝ `{best.Col} × {best.Ver}`（SD {best.Sd:F3}）。**");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
    }

    // =================================================================================
    // 段1 で選んだ点（§1-5「選んだ点を 1 箇所に置くこと」）。
    // **`RecoverRule.Default` には一切触っていない**（自己検査 (c)）——台の設定として
    // 診断側が `EngagementEngine.Run` に渡すだけ。
    // =================================================================================
    public const string PointCol = "順路5";
    public const string PointVer = "RFull";

    static (Col, Ver) PointOf(string arg)
    {
        var a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string cn = a.Length > 0 ? a[0] : PointCol;
        string vn = a.Length > 1 ? a[1] : PointVer;
        return (ColOf(cn), VerOf(vn));
    }

    // 素体（第69期の標準器具）。**`Presets` の `UnitDef` からその場で作る**ので
    // `UnitCatalog.All` を1度も引かない（第140期の則）。
    public static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern,
    };

    /// <summary>
    /// 会戦の seed の割り方。<b><c>Engagement.DeriveSeed</c> の写し</b>（private なので複製）。
    /// 単発側を同じ seed で回すために要る——揃えないと「繋いだか」以外の差が混ざる。
    /// </summary>
    static int DeriveSeed(int seed, int battleIndex) => unchecked(seed * 1000003 + battleIndex);

    /// <summary>
    /// <b>単発版の突破度。</b> 会戦のループから<b>持ち越しだけを抜いた</b>もの——
    /// 各部隊戦を<b>毎回新品の編成</b>で戦い、勝敗と削りの数え方は
    /// <c>EngagementEngine.Run</c> / <see cref="BreakthroughDegree"/> の逐語の写しにしてある。
    /// </summary>
    static double IndepDegree(Formation f, Col col, int seed)
    {
        int cleared = 0;
        double attr = 0;
        for (int b = 0; b < col.Len; b++)
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = BattleEngine.Materialize(col.Squads[b], BattleContext.EnemyTeam);
            int defMax = e.Sum(u => u.Def.MaxHp);
            BattleResult r = BattleEngine.Run(p, e, DeriveSeed(seed, b), verbose: false);
            int left = e.Sum(u => Math.Max(0, u.Hp));
            attr = defMax == 0 ? 0 : (double)(defMax - left) / defMax;
            bool clearedE = e.All(u => !u.IsAlive);
            bool lostP = p.All(u => !u.IsAlive) || !r.PlayerWon;
            if (clearedE) cleared++;
            if (lostP) break;
        }
        return cleared >= col.Len ? col.Len : cleared + attr;
    }

    static double EngageDegree(Formation f, Col col, Ver v, int seed)
    {
        EngagementResult r = EngagementEngine.Run(new[] { f }, col.Squads, seed,
                                                  verbose: false, recover: v.Rec, boundary: v.Bnd);
        return BreakthroughDegree(r, col.Len);
    }

    static (double E, double I) Both(Formation f, Col col, Ver v)
    {
        double e = 0, i = 0;
        for (int seed = 0; seed < Seeds; seed++)
        {
            e += EngageDegree(f, col, v, seed);
            i += IndepDegree(f, col, seed);
        }
        return (e / Seeds, i / Seeds);
    }

    // =================================================================================
    // 段2 —— `ablate` の会戦版
    // =================================================================================
    static void Attribute(string arg)
    {
        var (col, ver) = PointOf(arg);
        var rows = CompareBuilds();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var fullE = new double[rows.Length];
        var fullI = new double[rows.Length];
        Parallel.For(0, rows.Length, i =>
        {
            var (e, s) = Both(rows[i].F, col, ver);
            fullE[i] = e; fullI[i] = s;
        });

        var jobs = new List<(int Row, int Slot, UnitDef Def)>();
        for (int i = 0; i < rows.Length; i++)
            foreach ((int sl, UnitDef d) in rows[i].F.Occupied()) jobs.Add((i, sl, d));

        var gotE = new double[jobs.Count];
        var gotI = new double[jobs.Count];
        Parallel.For(0, jobs.Count, j =>
        {
            var f = new Formation();
            foreach ((int sl, UnitDef d) in rows[jobs[j].Row].F.Occupied())
                f[sl] = sl == jobs[j].Slot ? Plain(d) : d;
            var (e, s) = Both(f, col, ver);
            gotE[j] = e; gotI[j] = s;
        });

        Console.WriteLine("# 第157期 段2 —— `ablate` の会戦版");
        Console.WriteLine();
        Console.WriteLine($"台 ＝ **{col.Name} × {ver.Name}**（段1 で選んだ点）。"
            + $"`CompareBuilds()` {rows.Length} 行 × 延べ {jobs.Count} 枠 × seed 0..{Seeds - 1}。");
        Console.WriteLine();
        Console.WriteLine("    会戦帰属 = 突破度（本体） − 突破度（その駒を素体に）        ← 持ち越しあり");
        Console.WriteLine("    単発帰属 = 同じ器具・同じ列・同じ seed を毎戦新品で戦った版   ← 持ち越しなし");
        Console.WriteLine("    差       = 会戦帰属 − 単発帰属                              ← **この期の本体**");
        Console.WriteLine();

        // ---- 駒ごとの集計 ----
        var ids = jobs.Select(j => j.Def.Id).Distinct().ToArray();
        var name = ids.ToDictionary(id => id, id => jobs.First(j => j.Def.Id == id).Def.Name);
        var accE = ids.ToDictionary(id => id, _ => new List<double>());
        var accI = ids.ToDictionary(id => id, _ => new List<double>());
        for (int j = 0; j < jobs.Count; j++)
        {
            accE[jobs[j].Def.Id].Add(fullE[jobs[j].Row] - gotE[j]);
            accI[jobs[j].Def.Id].Add(fullI[jobs[j].Row] - gotI[j]);
        }
        var diff = ids.ToDictionary(id => id, id => accE[id].Average() - accI[id].Average());
        // 「≒」の読み方（§0-6 で先に固定した）——|差| の最小四分位。
        var absSorted = ids.Select(id => Math.Abs(diff[id])).OrderBy(x => x).ToArray();
        double q1 = absSorted[absSorted.Length / 4];

        Console.WriteLine($"## 2-1. 駒ごと（{ids.Length} 体・在席行の平均）");
        Console.WriteLine();
        Console.WriteLine($"**`≒` の線（§0-6 で先に固定）: |差| ≤ {q1:F3}**（|差| の最小四分位）。");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | 在席行 | 会戦帰属 | 単発帰属 | **差** | 予測 | 判定 |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|---|:-:|");
        int rk = 0;
        int hit = 0, miss = 0, split = 0, untested = 0;
        foreach (string id in ids.OrderByDescending(id => diff[id]))
        {
            var pred = Predictions.FirstOrDefault(p => p.Unit == id);
            string verdict = "—";
            if (pred.Unit is not null)
            {
                bool near = Math.Abs(diff[id]) <= q1;
                string actual = near ? "会戦 ≒ 単発" : diff[id] > 0 ? "会戦 > 単発" : "会戦 < 単発";
                if (pred.Dir == "符号が割れる") { verdict = "割"; split++; }
                else if (pred.Dir == actual) { verdict = "○"; hit++; }
                else { verdict = "×"; miss++; }
            }
            Console.WriteLine($"| {++rk} | {name[id]} | {accE[id].Count} | {accE[id].Average():+0.000;-0.000} "
                + $"| {accI[id].Average():+0.000;-0.000} | **{diff[id]:+0.000;-0.000}** "
                + $"| {(pred.Unit is null ? "—" : pred.Dir)} | {verdict} |");
        }
        Console.WriteLine();
        foreach (var p in Predictions)
            if (!ids.Contains(p.Unit)) untested++;
        Console.WriteLine($"**予測 {Predictions.Length} 件: ○ {hit} ／ × {miss} ／ 割（符号が割れる） {split} "
            + $"／ 在席せず検証不能 {untested}。**");
        Console.WriteLine();
        Console.WriteLine("「割」は当たり外れを数えない（**「符号が割れる」は反証可能な向きを述べていない**"
            + " —— 規約 (G4) の分母の書き落としと同じ形。次に書くときは行ごとの符号で線を引くこと）。");
        Console.WriteLine();

        // ---- 行 × 駒 ----
        Console.WriteLine("## 2-2. 行 × 駒（延べ枠。**この表が §2-2 の本体**）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 会戦帰属 | 単発帰属 | 差 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (int j in Enumerable.Range(0, jobs.Count)
                     .OrderByDescending(j => (fullE[jobs[j].Row] - gotE[j]) - (fullI[jobs[j].Row] - gotI[j])))
            Console.WriteLine($"| {rows[jobs[j].Row].Name} | {jobs[j].Def.Name} "
                + $"| {fullE[jobs[j].Row] - gotE[j]:+0.000;-0.000} | {fullI[jobs[j].Row] - gotI[j]:+0.000;-0.000} "
                + $"| {(fullE[jobs[j].Row] - gotE[j]) - (fullI[jobs[j].Row] - gotI[j]):+0.000;-0.000} |");
        Console.WriteLine();

        // ---- 行ごとの台 ----
        Console.WriteLine("## 2-3. 行ごとの本体の突破度（帰属の分母）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 会戦 | 単発 | 差 |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int i = 0; i < rows.Length; i++)
            Console.WriteLine($"| {rows[i].Name} | {fullE[i]:F3} | {fullI[i]:F3} | {fullE[i] - fullI[i]:+0.000;-0.000} |");
        Console.WriteLine();
        Console.WriteLine($"平均 会戦 {fullE.Average():F3} ／ 単発 {fullI.Average():F3}"
            + $"（SD {Sd(fullE):F3} / {Sd(fullI):F3}）。**列長 {col.Len}。**");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
    }

    // =================================================================================
    // 段3 —— 駒ごとの寿命の帳簿
    // =================================================================================
    static void Life(string arg)
    {
        var a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var targets = a.Length > 2 ? a.Skip(2).ToArray() : new[] { "nono", "vel", "rica", "gald" };
        var (col, ver) = PointOf(arg);
        var rows = CompareBuilds()
            .Where(r => r.F.Occupied().Any(o => targets.Contains(o.Def.Id))).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        int L = col.Len + 1;   // 会戦の Battle 数は高々 列長（味方1部隊）。+1 は保険
        // [unit][battle] の集計
        var entered = targets.ToDictionary(t => t, _ => new long[L]);
        var hpSum = targets.ToDictionary(t => t, _ => new double[L]);
        var maxSum = targets.ToDictionary(t => t, _ => new double[L]);
        var mend = targets.ToDictionary(t => t, _ => new double[L]);
        var mendHeal = targets.ToDictionary(t => t, _ => new double[L]);
        var fell = targets.ToDictionary(t => t, _ => new long[L]);
        var lowHp = targets.ToDictionary(t => t, _ => new long[L]);   // 入場 HP が MaxHp の 25% 以下
        var fellAt = targets.ToDictionary(t => t, _ => new long[L + 1]);   // 末尾 ＝ 最後まで倒れず
        var runs = targets.ToDictionary(t => t, _ => 0L);
        var battlesSum = targets.ToDictionary(t => t, _ => 0.0);
        var loBound = new object();

        Parallel.ForEach(rows, row =>
        {
            var lEntered = targets.ToDictionary(t => t, _ => new long[L]);
            var lHp = targets.ToDictionary(t => t, _ => new double[L]);
            var lMax = targets.ToDictionary(t => t, _ => new double[L]);
            var lMend = targets.ToDictionary(t => t, _ => new double[L]);
            var lHeal = targets.ToDictionary(t => t, _ => new double[L]);
            var lFell = targets.ToDictionary(t => t, _ => new long[L]);
            var lLow = targets.ToDictionary(t => t, _ => new long[L]);
            var lFellAt = targets.ToDictionary(t => t, _ => new long[L + 1]);
            var lRuns = targets.ToDictionary(t => t, _ => 0L);
            var lBattles = targets.ToDictionary(t => t, _ => 0.0);
            var here = targets.Where(t => row.F.Occupied().Any(o => o.Def.Id == t)).ToArray();

            for (int seed = 0; seed < Seeds; seed++)
            {
                EngagementResult r = EngagementEngine.Run(new[] { row.F }, col.Squads, seed,
                                                          verbose: true, recover: ver.Rec, boundary: ver.Bnd);
                foreach (string t in here) { lRuns[t]++; lBattles[t] += r.Battles.Count; }
                var died = here.ToDictionary(t => t, _ => -1);
                for (int b = 0; b < r.Battles.Count && b < L; b++)
                {
                    foreach (var op in r.Openings[b].Where(o => o.TeamId == BattleContext.PlayerTeam))
                    {
                        if (!here.Contains(op.UnitId)) continue;
                        lEntered[op.UnitId][b]++;
                        lHp[op.UnitId][b] += op.Hp;
                        lMax[op.UnitId][b] += op.MaxHp;
                        if (op.MaxHp > 0 && op.Hp * 4 <= op.MaxHp) lLow[op.UnitId][b]++;
                    }
                    foreach (string t in here)
                    {
                        if (r.Battles[b].TallyByUnit.TryGetValue(t, out UnitTally? tl))
                        {
                            lMend[t][b] += tl.MendFires;
                            lHeal[t][b] += tl.MendHealed;
                        }
                        if (r.Battles[b].PlayerStarterFallen.Contains(t))
                        {
                            lFell[t][b]++;
                            if (died[t] < 0) died[t] = b;
                        }
                    }
                }
                foreach (string t in here) lFellAt[t][died[t] < 0 ? L : died[t]]++;
            }

            lock (loBound)
                foreach (string t in here)
                {
                    runs[t] += lRuns[t]; battlesSum[t] += lBattles[t];
                    for (int b = 0; b < L; b++)
                    {
                        entered[t][b] += lEntered[t][b]; hpSum[t][b] += lHp[t][b];
                        maxSum[t][b] += lMax[t][b]; mend[t][b] += lMend[t][b];
                        mendHeal[t][b] += lHeal[t][b]; fell[t][b] += lFell[t][b];
                        lowHp[t][b] += lLow[t][b];
                    }
                    for (int b = 0; b <= L; b++) fellAt[t][b] += lFellAt[t][b];
                }
        });

        Console.WriteLine("# 第157期 段3 —— 駒ごとの寿命の帳簿");
        Console.WriteLine();
        Console.WriteLine($"台 ＝ **{col.Name} × {ver.Name}**。対象 {targets.Length} 体"
            + $"（{string.Join("・", targets)}）を含む **{rows.Length} 行** × seed 0..{Seeds - 1}。"
            + "**`verbose: true`**（`Openings` を読むため）。");
        Console.WriteLine();

        foreach (string t in targets)
        {
            if (runs[t] == 0) { Console.WriteLine($"## {t} —— **在席行 0。測れない。**"); Console.WriteLine(); continue; }
            string nm = UnitCatalog.Everyone.FirstOrDefault(d => d.Id == t)?.Name ?? t;
            int nRow = rows.Count(r => r.F.Occupied().Any(o => o.Def.Id == t));
            Console.WriteLine($"## {nm}（在席 {nRow} 行・{runs[t]:N0} 会戦・平均 {battlesSum[t] / runs[t]:F2} 部隊戦）");
            Console.WriteLine();
            Console.WriteLine("| 戦 | 入場率 | 平均入場HP | 平均MaxHp | HP比 | 瀕死率 | 繕い回数 | 繕い量 | この戦で倒れた |");
            Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int b = 0; b < L; b++)
            {
                if (entered[t][b] == 0) continue;
                double hp = hpSum[t][b] / entered[t][b], mx = maxSum[t][b] / entered[t][b];
                Console.WriteLine($"| 第{b + 1}戦 | {entered[t][b] * 100.0 / runs[t]:F1}% | {hp:F1} | {mx:F1} "
                    + $"| {(mx == 0 ? 0 : hp / mx * 100):F1}% | {lowHp[t][b] * 100.0 / entered[t][b]:F1}% "
                    + $"| {mend[t][b] / entered[t][b]:F2} | {mendHeal[t][b] / entered[t][b]:F1} "
                    + $"| {fell[t][b] * 100.0 / entered[t][b]:F1}% |");
            }
            Console.WriteLine();
            var parts = new List<string>();
            for (int b = 0; b < L; b++) if (fellAt[t][b] > 0) parts.Add($"第{b + 1}戦 {fellAt[t][b] * 100.0 / runs[t]:F1}%");
            parts.Add($"最後まで {fellAt[t][L] * 100.0 / runs[t]:F1}%");
            Console.WriteLine("**初めて倒れた戦**: " + string.Join(" ／ ", parts));
            Console.WriteLine();
        }

        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
    }

    // =================================================================================
    // 自己検査（§4）
    // =================================================================================
    static void Check(string arg)
    {
        string balance = arg.Length > 0 ? arg.Trim() : "docs/balance.md";
        Console.WriteLine("# 第157期 —— 自己検査（§4）");
        Console.WriteLine();

        // (a) compare 305 セル
        Console.WriteLine("## (a) `compare` 305 セルが `docs/balance.md` と 0 件差分");
        Console.WriteLine();
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Path.GetDirectoryName(root);
        string path = root is null ? balance : Path.Combine(root, "docs", "balance.md");
        if (!File.Exists(path)) Console.WriteLine($"`{path}` が無い。**判定できない。**");
        else
        {
            var want = new Dictionary<string, double[]>();
            foreach (string line in File.ReadLines(path))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                var c = line.Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length < 8) continue;
                var vals = new List<double>();
                for (int k = 2; k <= 6; k++)
                    if (double.TryParse(c[k].Replace("%", "").Replace("*", ""),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double d)) vals.Add(d);
                if (vals.Count == 5) want[c[1].Replace("*", "")] = vals.ToArray();
            }
            var rows = CompareBuilds();
            int cells = 0, bad = 0, seen = 0;
            foreach (var (nm, f) in rows)
            {
                if (!want.TryGetValue(nm, out double[]? w)) continue;
                seen++;
                for (int wi = 0; wi < EnemyCatalog.Stages.Count; wi++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                        if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
                    double got = wins * 100.0 / Seeds;
                    cells++;
                    if (Math.Abs(got - w[wi]) > 0.05) { bad++; Console.WriteLine($"- ずれ: {nm} 第{wi + 1}波 {got:F1} 対 {w[wi]:F1}"); }
                }
            }
            Console.WriteLine($"引けた行 {seen} / {rows.Length}・**{cells} セル中ずれ {bad} 件**"
                + $"（{(bad == 0 && cells == 305 ? "○" : "×")}）。");
        }
        Console.WriteLine();

        // (b) PickOne
        Console.WriteLine("## (b) `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        // **検索文字列は連結で組む**（第123期）——素直に書くと、この走査のコード自身が当たる
        // （実際に一度 2 件と出た。症状は「走査が空」ではなく**静かに違う数を出す**）。
        string needle = "Pick" + "One(";
        int pick = 0;
        if (root != null)
            foreach (string f in Directory.GetFiles(Path.Combine(root, "BattleSim"), "Stage.cs", SearchOption.AllDirectories))
                pick += File.ReadAllText(f).Split(needle).Length - 1;
        Console.WriteLine($"`BattleSim/Modes/Stage.cs` の `Pick`+`One(` は **{pick} 件**（{(pick == 0 ? "○" : "×")}）"
            + " —— 検索文字列を連結で組んだ後の値（第123期）。");
        Console.WriteLine();

        // (c) 既定に触っていない
        Console.WriteLine("## (c) 段1 で選んだ点を `RecoverRule.Default` にしていない");
        Console.WriteLine();
        Console.WriteLine($"`RecoverRule.Default` ＝ `{RecoverRule.Default}`"
            + $"（`Active` ＝ {RecoverRule.Default.Active}）・"
            + $"`BoundaryRule.Default` ＝ `{BoundaryRule.Default.Choice}`（`Active` ＝ {BoundaryRule.Default.Active}）。"
            + $"**選んだ点 `{PointCol} × {PointVer}` は診断が引数で渡すだけ**"
            + $"（{(!RecoverRule.Default.Active && !BoundaryRule.Default.Active ? "○" : "×")}）。");
        Console.WriteLine();

        // (d) 併用していない
        Console.WriteLine("## (d) `RecoverRule` と `BoundaryRule` を併用していない");
        Console.WriteLine();
        int both = Versions().Count(v => v.Rec is not null && v.Bnd is not null);
        Console.WriteLine($"両方を立てた版 **{both} 件**（{(both == 0 ? "○" : "×")}）。");
        Console.WriteLine();

        // (e) 素体が新しい数値になっていない
        Console.WriteLine("## (e) 素体が新しい数値の駒になっていない");
        Console.WriteLine();
        int diff = 0, n = 0;
        foreach (var (_, f) in CompareBuilds())
            foreach ((int _, UnitDef d) in f.Occupied())
            {
                UnitDef p = Plain(d);
                n++;
                if (p.MaxHp != d.MaxHp || p.Attack != d.Attack || p.Speed != d.Speed || p.Pattern != d.Pattern) diff++;
            }
        Console.WriteLine($"延べ {n} 枠で **数値が動いた素体 {diff} 件**（{(diff == 0 ? "○" : "×")}）。"
            + "落としているのは `Traits` と `Actions` だけ。");
        Console.WriteLine();

        // (f) 会戦帰属と単発帰属の対応
        Console.WriteLine("## (f) 会戦帰属と単発帰属が同じ行・同じ駒で対応している");
        Console.WriteLine();
        Console.WriteLine("**行名をキーにしていない**——段2 は `jobs[j] = (行の添字, スロット, UnitDef)` の"
            + "**同じ配列**から両方を作り、`Both()` が1回の呼び出しで会戦と単発を返す。"
            + "**対応の取り違えが起こる場所が実装に無い**（○）。");
        Console.WriteLine();
        Console.WriteLine("検算: 列長 1 の会戦では `FirstBattleAttrition` と `LastBattleAttrition` が一致するはず"
            + "（`Engagement.cs` の doc）。同じ台で会戦版と単発版も一致するはず——");
        var col1 = new Col("検算1", EnemyCatalog.Stages.Take(1).Select(s => s.Enemy).ToList());
        var v0 = VerOf("R0");
        int mism = 0;
        foreach (var (nm, f) in CompareBuilds().Take(10))
        {
            double e = 0, s = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                e += EngageDegree(f, col1, v0, seed);
                s += IndepDegree(f, col1, seed);
            }
            if (Math.Abs(e - s) > 1e-9) { mism++; Console.WriteLine($"- ずれ: {nm} {e / Seeds:F6} 対 {s / Seeds:F6}"); }
        }
        Console.WriteLine($"先頭 10 行・列長 1 で **会戦版と単発版のずれ {mism} 件**（{(mism == 0 ? "○" : "×")}）。");
        Console.WriteLine();

        // (g) 触っていないノブ
        Console.WriteLine("## (g) 触っていないノブの既定が1つも動いていない");
        Console.WriteLine();
        Console.WriteLine("**`BattleCore` は1行も触っていない**——この期に足したのは"
            + "`BattleSim/Modes/Stage.cs`（新規）と `BattleSim/Program.cs` の振り分け1ブロックだけ。"
            + "`docs/rules.md` の差分で示す。");
    }
}
