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
            case "perm": Perm(arg); return;
            case "permsd": PermSd(arg); return;
            case "obj": ObjRun(arg); return;
            case "rho": Rho(arg); return;
            case "rhocarry": RhoCarry(arg); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("stage: モードは phase0 / scan / perm / permsd / run / life / obj / rho / rhocarry / check。");
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
        // 第158期: 逆順列。**既存の `EnemyCatalog.Columns` にあるもの**を引くだけで、
        // 新しい敵も新しい並びも作らない（順路と同じ長さで、順路ではない列）。
        var rev = EnemyCatalog.Columns.First(c => c.Squads.Count == route.Squads.Count
                                                  && !ReferenceEquals(c.Squads, EnemyCatalog.EngagementColumn));
        return new[]
        {
            new Col("地点2", EnemyCatalog.Stages.Take(2).Select(s => s.Enemy).ToList()),
            new Col("地点3", spot.Squads),
            new Col("順路5", route.Squads),
            new Col("逆順路5", rev.Squads),
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

    /// <summary>
    /// 列を名前で引く。<b>第159期</b>: <c>P12345</c> の形の名前は「既存5波の並べ替え」として
    /// その場で組む——<b>新しい敵も新しい編成も1体も作らない</b>（<c>EnemyCatalog.Stages</c> を
    /// 並べ替えるだけ）。<c>P12345</c> は <c>順路5</c>・<c>P54321</c> は <c>逆順路5</c> と
    /// <b>同じ <see cref="Formation"/> の参照</b>になるので、対照の再現がそのまま検算になる。
    /// </summary>
    public static Col ColOf(string name)
    {
        var hit = Columns().FirstOrDefault(c => c.Name == name);
        if (hit is not null) return hit;
        int[]? p = ParsePerm(name);
        if (p is not null) return PermCol(p);
        throw new ArgumentException($"列 `{name}` が引けない（地点2 / 地点3 / 順路5 / 逆順路5 / P12345 形式）。");
    }

    /// <summary>`P12345` を 0 始まりの添字列へ。順列でなければ null。</summary>
    public static int[]? ParsePerm(string name)
    {
        int n = EnemyCatalog.Stages.Count;
        if (name.Length != n + 1 || name[0] != 'P') return null;
        var p = new int[n];
        for (int i = 0; i < n; i++)
        {
            int d = name[i + 1] - '1';
            if (d < 0 || d >= n) return null;
            p[i] = d;
        }
        return p.Distinct().Count() == n ? p : null;
    }

    /// <summary>並べ替えた列。**敵は1体も作らない**——`Stages` の `Enemy` をそのまま引く。</summary>
    public static Col PermCol(int[] p) =>
        new("P" + string.Concat(p.Select(i => (char)('1' + i))),
            p.Select(i => EnemyCatalog.Stages[i].Enemy).ToList());

    /// <summary>0..n-1 の全順列（辞書順）。</summary>
    public static IEnumerable<int[]> AllPerms(int n)
    {
        var cur = Enumerable.Range(0, n).ToArray();
        while (true)
        {
            yield return (int[])cur.Clone();
            int i = n - 2;
            while (i >= 0 && cur[i] >= cur[i + 1]) i--;
            if (i < 0) yield break;
            int j = n - 1;
            while (cur[j] <= cur[i]) j--;
            (cur[i], cur[j]) = (cur[j], cur[i]);
            Array.Reverse(cur, i + 1, n - i - 1);
        }
    }

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
    // 第159期 段A —— 列の並べ方（`stage perm`）
    //
    // **敵の総量は1ビットも変えない。順序だけを変える。**
    // 5波の全 120 順列 × 版。ここでは rho を測らない（安い走査）——出すのは台の水準と
    // **何戦目で決着したか**の分布だけ（指示書 Q0-3。SD が落ちる理由が床か天井かを割る）。
    // =================================================================================

    // §7-4。**実装前に書き切り、外れても消さない**（規約・第64期）。
    public static readonly (string Key, string Text)[] PermPredictions =
    {
        ("PA1", "**逆順で SD が落ちる理由は (a) 床への収束**"
              + "——第1戦で決着する会戦の割合が順路より高く、突破度 0 の行が増える"),
        ("PA2", "**台SD÷列長 が最大になるのは「難度の山が後ろにある」順列**"
              + "（＝第五波が終盤・第一波が序盤。順路5 に近い並び）"),
        ("PA3", "**本丸は ×**——順列の軸でも rho と SD は同じ向きに動く"
              + "（第158期 §3-3 の r = +0.726 が順列でも効く。rho を下げる並びは SD も落とす）"),
        ("PA4", "**段C の顔ぶれは第158期と同じ**——`BCarry` を含む点では"
              + "強化を配る駒（移り木のシオ・尾灯のトモ）が上位に出る"),
    };

    /// <summary>1行ぶんの突破度に<b>「何戦目で決着したか」</b>を足したもの（段A 専用）。</summary>
    static (double Deg, int NoClear, int FullClear, int[] Battles) DegreeB(Formation f, Col col, Ver v)
    {
        var player = new[] { f };
        double sum = 0;
        int noClear = 0, fullClear = 0;
        var hist = new int[col.Len + 1];
        for (int seed = 0; seed < Seeds; seed++)
        {
            EngagementResult r = EngagementEngine.Run(player, col.Squads, seed,
                                                      verbose: false, recover: v.Rec, boundary: v.Bnd);
            sum += BreakthroughDegree(r, col.Len);
            if (r.EnemySquadsCleared == 0) noClear++;
            if (r.EnemySquadsCleared >= col.Len) fullClear++;
            hist[Math.Min(r.Battles.Count, col.Len)]++;
        }
        return (sum / Seeds, noClear, fullClear, hist);
    }

    static void Perm(string arg)
    {
        var rows = CompareBuilds();
        var a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] verNames = a.Length > 0
            ? a[0].Split(',', StringSplitOptions.RemoveEmptyEntries)
            : new[] { "R0", "BCarry" };
        var vers = verNames.Select(VerOf).ToArray();
        int n = EnemyCatalog.Stages.Count;
        var perms = AllPerms(n).Select(PermCol).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Console.WriteLine("# 第159期 段A —— 列の並べ方（安い走査。rho は測らない）");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` {rows.Length} 行 × seed 0..{Seeds - 1} × "
            + $"**順列 {perms.Length} 通り × 版 {vers.Length} 版 ＝ {perms.Length * vers.Length} 点**。");
        Console.WriteLine();
        Console.WriteLine("**敵の総量は1ビットも変えていない**——`EnemyCatalog.Stages` の5波を並べ替えるだけで、"
            + "新しい敵も新しい編成も1体も作っていない。`P12345` ＝ `順路5`・`P54321` ＝ `逆順路5`。");
        Console.WriteLine();
        foreach (var (k, t) in PermPredictions) Console.WriteLine($"{k}. {t}  ");
        Console.WriteLine();

        var recs = new List<(string P, string V, double Mean, double Sd, int Zero, int Full,
                             int NoClearRows, int FullClearRows, double[] Battles, double AvgB)>();
        foreach (var col in perms)
            foreach (var v in vers)
            {
                var deg = new double[rows.Length];
                var nc = new int[rows.Length];
                var fc = new int[rows.Length];
                var hist = new int[rows.Length][];
                Parallel.For(0, rows.Length, i =>
                {
                    var (d, z, f, h) = DegreeB(rows[i].F, col, v);
                    deg[i] = d; nc[i] = z; fc[i] = f; hist[i] = h;
                });
                var tot = new double[col.Len + 1];
                for (int b = 0; b <= col.Len; b++)
                    tot[b] = hist.Sum(h => h[b]) / (double)(rows.Length * Seeds);
                double avgB = Enumerable.Range(0, col.Len + 1).Sum(b => b * tot[b]);
                recs.Add((col.Name, v.Name, deg.Average(), Sd(deg),
                          deg.Count(d => d == 0.0), deg.Count(d => d >= col.Len),
                          nc.Count(x => x == Seeds), fc.Count(x => x == Seeds), tot, avgB));
                Console.Error.WriteLine($"{col.Name} {v.Name} done {sw.Elapsed.TotalSeconds:F0}s");
            }

        Console.WriteLine("## A-1. 台SD÷列長 の上位20点（版ごとの上位は A-2）");
        Console.WriteLine();
        void Head()
        {
            Console.WriteLine("| 順列 | 版 | 五波の位置 | 平均突破度 | 取り分 | **台SD÷列長** | 0行 | 満行 | 抜き0行 | 全抜き行 | 平均戦数 | 1戦で決着 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        }
        int PosOfLast(string pn) => pn.IndexOf((char)('1' + n - 1));   // 1 始まり（先頭の 'P' のぶん）
        void Line((string P, string V, double Mean, double Sd, int Zero, int Full,
                   int NoClearRows, int FullClearRows, double[] Battles, double AvgB) r)
            => Console.WriteLine($"| {r.P} | {r.V} | {PosOfLast(r.P)} | {r.Mean:F3} | {r.Mean / n * 100:F1}% "
                + $"| **{r.Sd / n:F3}** | {r.Zero} | {r.Full} | {r.NoClearRows} | {r.FullClearRows} "
                + $"| {r.AvgB:F2} | {r.Battles[1]:P1} |");
        Head();
        foreach (var r in recs.OrderByDescending(x => x.Sd).Take(20)) Line(r);
        Console.WriteLine();
        Console.WriteLine("`五波の位置` は第五波（異端審問団）が何戦目か（1 始まり）。");
        Console.WriteLine();

        Console.WriteLine("## A-2. 版ごとの上位4点（**段B で rho を測る候補**）＋ 対照2点");
        Console.WriteLine();
        Head();
        var picks = new List<(string P, string V)>();
        foreach (var v in vers)
            foreach (var r in recs.Where(x => x.V == v.Name).OrderByDescending(x => x.Sd).Take(4))
            { Line(r); picks.Add((r.P, r.V)); }
        foreach (var c in new[] { ("P12345", "R0"), ("P54321", "BCarry") })
        {
            var r = recs.FirstOrDefault(x => x.P == c.Item1 && x.V == c.Item2);
            if (r.P is not null) { Line(r); if (!picks.Contains(c)) picks.Add(c); }
        }
        Console.WriteLine();
        Console.WriteLine("**段B の点**: " + string.Join(" / ", picks.Select(x => $"`{x.P} {x.V}`")));
        Console.WriteLine();
        Console.WriteLine("    dotnet run --project BattleSim -c Release 0 stage rho \"" 
            + string.Join(",", picks.Select(x => $"{x.P} {x.V}")) + "\"");
        Console.WriteLine();

        Console.WriteLine("## A-3. 決着した戦数の分布（指示書 Q0-3。**床か天井か**）");
        Console.WriteLine();
        Console.WriteLine("| 順列 | 版 | " + string.Join(" | ", Enumerable.Range(1, n).Select(b => $"{b}戦")) 
            + " | 平均戦数 | 0行 | 満行 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, n + 3).Select(_ => "--:|")));
        foreach (var r in recs.Where(x => x.P == "P12345" || x.P == "P54321")
                              .OrderBy(x => x.P).ThenBy(x => x.V))
            Console.WriteLine($"| {r.P} | {r.V} | "
                + string.Join(" | ", Enumerable.Range(1, n).Select(b => $"{r.Battles[b]:P1}"))
                + $" | {r.AvgB:F2} | {r.Zero} | {r.Full} |");
        Console.WriteLine();

        Console.WriteLine("## A-4. 全 " + recs.Count + " 点");
        Console.WriteLine();
        Head();
        foreach (var r in recs.OrderBy(x => x.V).ThenByDescending(x => x.Sd)) Line(r);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
    }

    // =================================================================================
    // 第159期 段A' —— **帰属SD そのもの**で 120 順列を走査（`stage permsd`）
    //
    // 段A の並べ替えは台SD（61 行の突破度のばらつき）で候補を選ぶが、**線2 が読むのは帰属SD**
    // （駒 52 体のばらつき）で、実測ではこの2つが食い違う（P41325 は台SD 0.198 に対し帰属SD 0.093）。
    // **rho は測らない**——単発側（列ごとに 366 回の `IndepAvg`）を省くと1点 4 秒で済む。
    // **線は1つも動かしていない。** 候補の選び方を代理から本番の量へ替えただけ。
    // =================================================================================
    static void PermSd(string arg)
    {
        var rows = CompareBuilds();
        var a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var vers = (a.Length > 0 ? a[0].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 : new[] { "R0", "BCarry" }).Select(VerOf).ToArray();
        int n = EnemyCatalog.Stages.Count;
        var perms = AllPerms(n).Select(PermCol).ToArray();

        var jobs = new List<(int Row, int Slot, UnitDef Def)>();
        for (int i = 0; i < rows.Length; i++)
            foreach ((int sl, UnitDef d) in rows[i].F.Occupied()) jobs.Add((i, sl, d));
        var ids = jobs.Select(j => j.Def.Id).Distinct().ToArray();
        var slotsOf = ids.ToDictionary(id => id,
            id => Enumerable.Range(0, jobs.Count).Where(j => jobs[j].Def.Id == id).ToArray());
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Console.WriteLine("# 第159期 段A' —— 帰属SD で 120 順列を走査（rho は測らない）");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` {rows.Length} 行 × 延べ {jobs.Count} 枠 × 駒 {ids.Length} 体 × "
            + $"seed 0..{Seeds - 1} × **順列 {perms.Length} 通り × 版 {vers.Length} 版**。");
        Console.WriteLine();
        Console.WriteLine("**線2 が読むのは帰属SD（駒 52 体）**で、段A が候補選びに使った台SD（61 行）ではない。"
            + "**線は1つも動かしていない**——候補の選び方を代理から本番の量へ替えただけ。");
        Console.WriteLine();

        var recs = new List<(string P, string V, double SdU, double SdS, double TaiSd, double Mean)>();
        foreach (var col in perms)
            foreach (var v in vers)
            {
                var fE = new double[rows.Length];
                Parallel.For(0, rows.Length, i => fE[i] = EngageAvg(rows[i].F, col, v));
                var gE = new double[jobs.Count];
                Parallel.For(0, jobs.Count, j => gE[j] = EngageAvg(SwapOne(rows[jobs[j].Row].F, jobs[j].Slot), col, v));
                var sE = new double[jobs.Count];
                for (int j = 0; j < jobs.Count; j++) sE[j] = fE[jobs[j].Row] - gE[j];
                var uE = ids.Select(id => slotsOf[id].Average(j => sE[j])).ToArray();
                recs.Add((col.Name, v.Name, Sd(uE), Sd(sE), Sd(fE), uE.Average()));
                Console.Error.WriteLine($"{col.Name} {v.Name} done {sw.Elapsed.TotalSeconds:F0}s");
            }

        void Head()
        {
            Console.WriteLine("| 順列 | 版 | **帰属SD÷列長** | 対照比 | 枠SD÷列長 | 台SD÷列長 | 会戦帰属の平均 | 線2 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|:-:|");
        }
        double ctrl = recs.FirstOrDefault(x => x.P == "P12345" && x.V == "R0").SdU / n;
        void Line((string P, string V, double SdU, double SdS, double TaiSd, double Mean) r)
            => Console.WriteLine($"| {r.P} | {r.V} | **{r.SdU / n:F3}** | {(ctrl > 0 ? (r.SdU / n / ctrl).ToString("F2") : "—")} 倍 "
                + $"| {r.SdS / n:F3} | {r.TaiSd / n:F3} | {r.Mean:+0.000;-0.000} | {(r.SdU / n >= SdLine ? "○" : "×")} |");

        Console.WriteLine($"## A'-1. 帰属SD÷列長 の上位20点（線2 ＝ {SdLine:F3}）");
        Console.WriteLine();
        Head();
        foreach (var r in recs.OrderByDescending(x => x.SdU).Take(20)) Line(r);
        Console.WriteLine();
        Console.WriteLine($"**線2 を満たす点: {recs.Count(x => x.SdU / n >= SdLine)} / {recs.Count}。**"
            + $" 最大は `{recs.OrderByDescending(x => x.SdU).First().P} × {recs.OrderByDescending(x => x.SdU).First().V}` "
            + $"の {recs.Max(x => x.SdU) / n:F3}（対照 {ctrl:F3} の {recs.Max(x => x.SdU) / n / ctrl:F2} 倍）。");
        Console.WriteLine();

        Console.WriteLine("## A'-2. 対照");
        Console.WriteLine();
        Head();
        foreach (var c in new[] { ("P12345", "R0"), ("P12345", "BCarry"), ("P54321", "R0"), ("P54321", "BCarry") })
        {
            var r = recs.FirstOrDefault(x => x.P == c.Item1 && x.V == c.Item2);
            if (r.P is not null) Line(r);
        }
        Console.WriteLine();

        Console.WriteLine("## A'-3. 全 " + recs.Count + " 点");
        Console.WriteLine();
        Head();
        foreach (var r in recs.OrderBy(x => x.V).ThenByDescending(x => x.SdU)) Line(r);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
    }

    // =================================================================================
    // 第158期 —— 目的変数を ρ に変える（`stage rho`）
    //
    // **単発帰属は版に依らない**（`IndepDegree` は `Ver` を受け取らない）ので、列ごとに1度だけ
    // 測って使い回す。同じ列の点どうしは分母（単発側）が1ビットも違わない——
    // だから「絞った ρ」のフィルタを**単発側**に掛けると、点をまたいで同じ枠集合になる。
    // **処置後の側（会戦帰属）で選別しない**のは第88期の情報帯と同じ理由。
    // =================================================================================

    public sealed record Pt(string ColName, string VerName, string Note);

    public static Pt[] RhoPoints() => new[]
    {
        new Pt("順路5",   "R0",      "**対照**（現行の境界。第157期 rho 0.857 / 帰属SD 0.318）"),
        new Pt("順路5",   "RFull",   "**対照**（第157期 rho 0.884 / 帰属SD 0.808）"),
        new Pt("順路5",   "BCarry",  "**本命**（`StatusKeys` / `AtkBonus` / `WhetReceived` を持ち越す）"),
        new Pt("順路5",   "BRevive", "`BoundaryChoice` のもう一方（体だけ返す）"),
        new Pt("逆順路5", "R0",      "列の順序"),
        new Pt("逆順路5", "BCarry",  "列の順序 × 本命"),
        new Pt("地点3",   "R0",      "短い列"),
        new Pt("地点3",   "BCarry",  "短い列 × 本命"),
        new Pt("地点2",   "R0",      "一番短い列（**rho が 1 に近づくはず** ＝ 予測4）"),
    };

    // §4。**実装前に書き切り、外れても消さない**（規約・第64期）。
    public static readonly (string Key, string Text)[] RhoPredictions =
    {
        ("P1", "**`BCarry` は rho を下げる**（順路5 × BCarry の rho < 対照 R0 の 0.857）"),
        ("P2", "**`BCarry` で上がる駒**：ヒビ・ウロ・ガレ（破片）／ムド・ウツ（`AtkBonus`）。R0 で負だった差が反転する"),
        ("P3", "**`BCarry` で下がる駒**：グザ・ミオ・スィド（毒。**負も持ち越す**）"),
        ("P4", "**`地点2 × R0` の rho が全点で最大**（列が短く単発に近い）"),
        ("P5", "**逆順列は rho を下げるが SD は上げない**（順序が変わるだけで通貨は増えない）"),
        ("P6", "**`RFull` の rho が高い**（持ち越しの経路を消しているので単発に似る）"),
    };

    // ---- 線（**測る前に固定する**。§2-1） ----
    const double RhoLine = 0.75;     // 線1: 序列が動いた
    const double SdLine = 0.162;     // 線2（第158/159期の版・**第161期に参考へ降ろした**）: 帰属SD ÷ 列長
    // 線2（**第161期に引き直した版**）: 会戦の帰属SD ÷ **単発の帰属SD**。
    // **列長で割らない**（比なので無次元）し、**どの対照で出た値かも自明**（同じ走行の単発側）。
    // 旧 0.162 の出どころは第157期 §1-2 の**台の水準**の表で、**駒の水準の線として設定されたことが一度も無い**。
    const double RatioLine = 0.80;

    static double IndepAvg(Formation f, Col col)
    {
        double s = 0;
        for (int seed = 0; seed < Seeds; seed++) s += IndepDegree(f, col, seed);
        return s / Seeds;
    }

    static double EngageAvg(Formation f, Col col, Ver v)
    {
        double s = 0;
        for (int seed = 0; seed < Seeds; seed++) s += EngageDegree(f, col, v, seed);
        return s / Seeds;
    }

    static Formation SwapOne(Formation src, int slot)
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in src.Occupied()) f[sl] = sl == slot ? Plain(d) : d;
        return f;
    }

    sealed record RhoRes(Pt P, Col C, string[] Ids,
                         double[] UE, double[] UI, double[] SE, double[] SI, double[] FE);

    static double RhoQ1(double[] v)
    {
        var a = v.Select(Math.Abs).OrderBy(x => x).ToArray();
        return a[a.Length / 4];
    }

    static (double Rho, int N) RhoOf(double[] e, double[] i, double thr)
    {
        var xs = new List<double>(); var ys = new List<double>();
        for (int k = 0; k < e.Length; k++) if (Math.Abs(i[k]) > thr) { xs.Add(e[k]); ys.Add(i[k]); }
        if (xs.Count < 3) return (double.NaN, xs.Count);
        var c = Correlate(xs.ToArray(), ys.ToArray());
        return (c.Rho, c.N);
    }

    // 同値塊（規約 (G13)）。最大の同値グループの割合と、厳密に 0 の数。
    static (double Tie, int Zero) RhoTies(double[] v)
    {
        var g = v.GroupBy(x => Math.Round(x, 6)).OrderByDescending(x => x.Count()).First();
        return ((double)g.Count() / v.Length, v.Count(x => Math.Abs(x) < 1e-9));
    }

    static void Rho(string arg)
    {
        var rows = CompareBuilds();
        var jobs = new List<(int Row, int Slot, UnitDef Def)>();
        for (int i = 0; i < rows.Length; i++)
            foreach ((int sl, UnitDef d) in rows[i].F.Occupied()) jobs.Add((i, sl, d));
        var ids = jobs.Select(j => j.Def.Id).Distinct().ToArray();
        var uname = ids.ToDictionary(id => id, id => jobs.First(j => j.Def.Id == id).Def.Name);
        var slotsOf = ids.ToDictionary(id => id,
            id => Enumerable.Range(0, jobs.Count).Where(j => jobs[j].Def.Id == id).ToArray());

        var pts = RhoPoints();
        if (arg.Trim().Length > 0)
        {
            var want = arg.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            // **完全一致**で絞る。`Contains` だと「順路5 R0」が「逆順路5 R0」を拾う
            // （第123期「静かに違うものを測る」の引数の側。実際に1度拾った）。
            // 第159期: 表に無い点（`P12345 R0` のような順列）は**その場で組む**——
            // 組めるかどうかは `ColOf` / `VerOf` が決める（引けなければ例外で止まる。第117期）。
            var built = new List<Pt>();
            foreach (string w in want)
            {
                var hit = pts.FirstOrDefault(p => $"{p.ColName} {p.VerName}" == w);
                if (hit is not null) { built.Add(hit); continue; }
                var t = w.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (t.Length != 2) { Console.WriteLine($"stage rho: 点 `{w}` が読めない（「列 版」）。"); return; }
                _ = ColOf(t[0]); _ = VerOf(t[1]);            // 引けることを先に確かめる
                built.Add(new Pt(t[0], t[1], "第159期 段B"));
            }
            pts = built.ToArray();
            if (pts.Length == 0) { Console.WriteLine("stage rho: 該当する点が無い。"); return; }
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // ---- 単発側は列ごとに1度だけ（版に依らない） ----
        var indepFull = new Dictionary<string, double[]>();
        var indepGot = new Dictionary<string, double[]>();
        foreach (string cn in pts.Select(p => p.ColName).Distinct())
        {
            var col = ColOf(cn);
            var fI = new double[rows.Length];
            Parallel.For(0, rows.Length, i => fI[i] = IndepAvg(rows[i].F, col));
            var gI = new double[jobs.Count];
            Parallel.For(0, jobs.Count, j => gI[j] = IndepAvg(SwapOne(rows[jobs[j].Row].F, jobs[j].Slot), col));
            indepFull[cn] = fI; indepGot[cn] = gI;
        }

        var res = new List<RhoRes>();
        foreach (var p in pts)
        {
            var col = ColOf(p.ColName); var ver = VerOf(p.VerName);
            var fE = new double[rows.Length];
            Parallel.For(0, rows.Length, i => fE[i] = EngageAvg(rows[i].F, col, ver));
            var gE = new double[jobs.Count];
            Parallel.For(0, jobs.Count, j => gE[j] = EngageAvg(SwapOne(rows[jobs[j].Row].F, jobs[j].Slot), col, ver));
            double[] fI = indepFull[p.ColName], gI = indepGot[p.ColName];
            var sE = new double[jobs.Count]; var sI = new double[jobs.Count];
            for (int j = 0; j < jobs.Count; j++) { sE[j] = fE[jobs[j].Row] - gE[j]; sI[j] = fI[jobs[j].Row] - gI[j]; }
            var uE = ids.Select(id => slotsOf[id].Average(j => sE[j])).ToArray();
            var uI = ids.Select(id => slotsOf[id].Average(j => sI[j])).ToArray();
            res.Add(new RhoRes(p, col, ids, uE, uI, sE, sI, fE));
        }

        Console.WriteLine("# 第158期 段B —— 目的変数を rho に変える");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` {rows.Length} 行 × 延べ {jobs.Count} 枠 × 駒 {ids.Length} 体 × seed 0..{Seeds - 1}。");
        Console.WriteLine();
        Console.WriteLine("    会戦帰属 = 突破度（本体） − 突破度（その駒を素体に）   ← 持ち越しあり");
        Console.WriteLine("    単発帰属 = 同じ器具・同じ列・同じ seed を毎戦新品で    ← 持ち越しなし。**版に依らない**");
        Console.WriteLine("    rho      = 会戦帰属 と 単発帰属 の順位相関             ← **この期の目的変数**");
        Console.WriteLine();
        Console.WriteLine("**絞った rho のフィルタは単発側**（`|単発帰属| > q1`）。処置後の側で選別しない（第88期の情報帯）。");
        Console.WriteLine("**単発側は版に依らない**ので、同じ列の点はどれも同じ枠集合で絞られる。");
        Console.WriteLine();

        Console.WriteLine("## B-1. 点ごとの rho と SD（**駒 52 体**の水準。第157期 §2-4 と同じ）");
        Console.WriteLine();
        Console.WriteLine("| 点 | 列長 | **rho(全)** | **rho(絞)** | 絞りの分母 | r(全) | 会戦の帰属SD | **単発の帰属SD** | **比** | SD÷列長 | 台SD | 台SD÷列長 | 会戦帰属の平均 | 単発帰属の平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in res)
        {
            double q = RhoQ1(r.UI);
            var (rhoAll, nAll) = RhoOf(r.UE, r.UI, -1);
            var (rhoCut, nCut) = RhoOf(r.UE, r.UI, q);
            double pear = Correlate(r.UE, r.UI).R;
            double sd = Sd(r.UE), sdI = Sd(r.UI);
            Console.WriteLine($"| {r.P.ColName} × {r.P.VerName} | {r.C.Len} | **{rhoAll:F3}** | **{rhoCut:F3}** "
                + $"| {nCut} / {nAll} | {pear:F3} | {sd:F3} | **{sdI:F3}** | **{(sdI > 0 ? sd / sdI : double.NaN):F3}** "
                + $"| {sd / r.C.Len:F3} | {Sd(r.FE):F3} | {Sd(r.FE) / r.C.Len:F3} "
                + $"| {r.UE.Average():+0.000;-0.000} | {r.UI.Average():+0.000;-0.000} |");
        }
        Console.WriteLine();
        Console.WriteLine("絞りの閾値 q1（|単発帰属| の最小四分位）は列ごとに1つ: "
            + string.Join(" / ", res.Select(r => r.P.ColName).Distinct()
                .Select(cn => $"{cn} {RhoQ1(res.First(r => r.P.ColName == cn).UI):F3}")) + "。");
        Console.WriteLine();

        Console.WriteLine("## B-2. 点ごとの rho（**枠**の水準。§2-2 の字義どおり）");
        Console.WriteLine();
        Console.WriteLine("| 点 | rho(全) | rho(絞) | 絞りの分母 | 帰属SD | SD÷列長 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var r in res)
        {
            double q = RhoQ1(r.SI);
            var (rhoAll, nAll) = RhoOf(r.SE, r.SI, -1);
            var (rhoCut, nCut) = RhoOf(r.SE, r.SI, q);
            Console.WriteLine($"| {r.P.ColName} × {r.P.VerName} | {rhoAll:F3} | {rhoCut:F3} | {nCut} / {nAll} "
                + $"| {Sd(r.SE):F3} | {Sd(r.SE) / r.C.Len:F3} |");
        }
        Console.WriteLine();

        Console.WriteLine("## B-3. 同値塊（規約 (G13)。**同値塊がある分布に順位相関を当てない**）");
        Console.WriteLine();
        Console.WriteLine("| 点 | 会戦帰属の最大同値塊 | 厳密に 0 の駒 | 単発帰属の最大同値塊 | 厳密に 0 の駒 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var r in res)
        {
            var (tE, zE) = RhoTies(r.UE); var (tI, zI) = RhoTies(r.UI);
            Console.WriteLine($"| {r.P.ColName} × {r.P.VerName} | {tE:P1} | {zE} / {r.Ids.Length} | {tI:P1} | {zI} / {r.Ids.Length} |");
        }
        Console.WriteLine();

        Console.WriteLine("## B-4. 判定（**線は §2-1 で測る前に固定した**）");
        Console.WriteLine();
        Console.WriteLine($"線1 序列が動いた: **rho(絞) <= {RhoLine:F2}**（**第161期に rho(絞) で揃えた**。rho(全) は併記するが判定に使わない）  ");
        Console.WriteLine($"線2 駒の差が立っている: **会戦の帰属SD ÷ 単発の帰属SD >= {RatioLine:F2}**"
            + $"（**第161期に引き直した**。旧 `帰属SD ÷ 列長 >= {SdLine:F3}` は参考へ降ろした——"
            + "あの 0.162 は第157期 §1-2 の**台の水準**の値で、駒の水準の線として設定されたことが一度も無い）  ");
        Console.WriteLine("線3 本丸: **線1 と線2 を同時に満たす点が1つ以上**");
        Console.WriteLine();
        Console.WriteLine("| 点 | rho(全) | **rho(絞)** | 線1 | **比** | 線2 | **本丸** | 旧SD÷列長 | 旧線2 |");
        Console.WriteLine("|---|--:|--:|:-:|--:|:-:|:-:|--:|:-:|");
        int win = 0;
        foreach (var r in res)
        {
            double q = RhoQ1(r.UI);
            double a = RhoOf(r.UE, r.UI, -1).Rho, b = RhoOf(r.UE, r.UI, q).Rho;
            double sd = Sd(r.UE), sdI = Sd(r.UI), sdn = sd / r.C.Len;
            double ratio = sdI > 0 ? sd / sdI : double.NaN;
            bool l1 = b <= RhoLine, l2 = ratio >= RatioLine;
            if (l1 && l2) win++;
            Console.WriteLine($"| {r.P.ColName} × {r.P.VerName} | {a:F3} | **{b:F3}** | {(l1 ? "○" : "×")} "
                + $"| **{ratio:F3}** | {(l2 ? "○" : "×")} | {(l1 && l2 ? "**○**" : "×")} "
                + $"| {sdn:F3} | {(sdn >= SdLine ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**線1 と線2 を同時に満たす点: {win} / {res.Count}。本丸は {(win > 0 ? "○" : "×")}。**");
        Console.WriteLine();
        var near = res.OrderByDescending(r => Sd(r.UE) / Sd(r.UI)).First();
        double nr = Sd(near.UE) / Sd(near.UI);
        Console.WriteLine($"比が最大の点は `{near.P.ColName} × {near.P.VerName}` の **{nr:F3}**。");
        // 線2 を1点も通らなかったときだけ「あと何倍要るか」を出す（通っているのに出すと嘘になる）。
        var miss = res.Where(r => Sd(r.UE) / Sd(r.UI) < RatioLine)
                      .OrderByDescending(r => Sd(r.UE) / Sd(r.UI)).ToArray();
        if (miss.Length == res.Count)
        {
            double mr = Sd(miss[0].UE) / Sd(miss[0].UI);
            Console.WriteLine($"**線2 は 0 / {res.Count}。** いちばん近い `{miss[0].P.ColName} × {miss[0].P.VerName}` で "
                + $"**{mr:F3}**——線 {RatioLine:F2} に届くには会戦の帰属SD が **{RatioLine / mr:F2} 倍**要る。");
        }
        else if (miss.Length > 0)
        {
            double mr = Sd(miss[0].UE) / Sd(miss[0].UI);
            Console.WriteLine($"線2 を落とした点のうち最良は `{miss[0].P.ColName} × {miss[0].P.VerName}` の **{mr:F3}**"
                + $"（あと **{RatioLine / mr:F2} 倍**）。");
        }
        Console.WriteLine();

        Console.WriteLine("## B-5. 予測（**実装前に書いた。外れても消さない**）");
        Console.WriteLine();
        foreach (var (k, t) in RhoPredictions) Console.WriteLine($"{k}. {t}");
        Console.WriteLine();

        Console.WriteLine("## B-6. 動いた駒（単発と会戦で順位が入れ替わった上位10体）");
        Console.WriteLine();
        foreach (var r in res)
        {
            var rE = AverageRanksDesc(r.UE);
            var rI = AverageRanksDesc(r.UI);
            var ord = Enumerable.Range(0, r.Ids.Length)
                .OrderByDescending(k => Math.Abs(rI[k] - rE[k])).Take(10).ToArray();
            Console.WriteLine($"### {r.P.ColName} × {r.P.VerName}");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 在席枠 | 単発順位 | 会戦順位 | **Δ順位** | 単発帰属 | 会戦帰属 | 差 | 札 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|");
            foreach (int k in ord)
            {
                var def = jobs.First(j => j.Def.Id == r.Ids[k]).Def;
                string traits = def.Traits.Count == 0 ? "—" : string.Join(" / ", def.Traits);
                Console.WriteLine($"| {uname[r.Ids[k]]} | {slotsOf[r.Ids[k]].Length} | {rI[k]:F1} | {rE[k]:F1} "
                    + $"| **{rI[k] - rE[k]:+0.0;-0.0}** | {r.UI[k]:+0.000;-0.000} | {r.UE[k]:+0.000;-0.000} "
                    + $"| {r.UE[k] - r.UI[k]:+0.000;-0.000} | {traits} |");
            }
            Console.WriteLine();
        }

        Console.WriteLine("## B-7. 予測 P2 / P3 の名指し（`差 = 会戦帰属 − 単発帰属` の符号）");
        Console.WriteLine();
        string[] up = { "hibi", "uro", "gare", "mudo", "utsu" };
        string[] down = { "guza", "mio", "sid" };
        Console.WriteLine("| 駒 | 予測 | " + string.Join(" | ", res.Select(r => $"{r.P.ColName}×{r.P.VerName}")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(res.Select(_ => "--:|")));
        foreach (string id in up.Concat(down))
        {
            int k = Array.IndexOf(ids, id);
            if (k < 0) { Console.WriteLine($"| {id} | — | 在席せず |"); continue; }
            Console.WriteLine($"| {uname[id]} | {(up.Contains(id) ? "P2 上がる" : "P3 下がる")} | "
                + string.Join(" | ", res.Select(r => $"{r.UE[k] - r.UI[k]:+0.000;-0.000}")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
    }

    // =================================================================================
    // 第158期 —— 境界をまたいで運ばれた「入場時の実効攻撃力の上乗せ」（`stage rhocarry`）
    //
    // `BattleOpening.Attack` は **`UnitState.CurrentAttack`**（`Def.Attack + AtkBonus` を
    // `Trait.ModifyAttack` に通した後）で、`BaseAttack` は `Def.Attack`。
    // **だから引き算は純粋な `AtkBonus` ではない**——薄刃（キリ −11）や逆しまの半減が混ざる
    // （第121期「判定の線は計数の名前ではなく実装が数えている出来事に当てる」）。
    // **engine には1行も足さない。** 状態異常のカウンタは台本に載らない（第125期）ので出せない。
    // =================================================================================
    static void RhoCarry(string arg)
    {
        var a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var col = ColOf(a.Length > 0 ? a[0] : "順路5");
        var vers = (a.Length > 1 ? a[1] : "R0,BCarry").Split(',').Select(VerOf).ToArray();
        var rows = CompareBuilds();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Console.WriteLine("# 第158期 段C —— 入場時の実効攻撃力の上乗せ（実測）");
        Console.WriteLine();
        Console.WriteLine($"列 ＝ {col.Name}。`BattleOpening.Attack − BaseAttack` を**第2戦以降の入場時**に読む。");
        Console.WriteLine();
        Console.WriteLine("**これは純粋な `AtkBonus` ではない**——`Attack` は `CurrentAttack`（`ModifyAttack` を通った後）なので、");
        Console.WriteLine("薄刃（裂きのキリ −11）や逆しまの半減が混ざる。**`AtkBonus` そのものを読む窓口は台本に無い。**");
        Console.WriteLine("**状態異常のカウンタは台本に載らないので出せない**（第125期）。");
        Console.WriteLine();

        var per = new Dictionary<string, double[]>();
        var pos = new Dictionary<string, double[]>();
        var cnt = new Dictionary<string, long[]>();
        var lk = new object();
        for (int vi = 0; vi < vers.Length; vi++)
        {
            int vidx = vi;
            Parallel.ForEach(rows, row =>
            {
                var lSum = new Dictionary<string, double>();
                var lPos = new Dictionary<string, long>();
                var lCnt = new Dictionary<string, long>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    EngagementResult r = EngagementEngine.Run(new[] { row.F }, col.Squads, seed,
                        verbose: true, recover: vers[vidx].Rec, boundary: vers[vidx].Bnd);
                    for (int b = 1; b < r.Openings.Count; b++)
                        foreach (var op in r.Openings[b].Where(o => o.TeamId == BattleContext.PlayerTeam))
                        {
                            int bonus = op.Attack - op.BaseAttack;
                            lSum[op.UnitId] = lSum.GetValueOrDefault(op.UnitId) + bonus;
                            if (bonus > 0) lPos[op.UnitId] = lPos.GetValueOrDefault(op.UnitId) + 1;
                            lCnt[op.UnitId] = lCnt.GetValueOrDefault(op.UnitId) + 1;
                        }
                }
                lock (lk)
                    foreach (string id in lCnt.Keys)
                    {
                        if (!per.ContainsKey(id))
                        {
                            per[id] = new double[vers.Length];
                            pos[id] = new double[vers.Length];
                            cnt[id] = new long[vers.Length];
                        }
                        per[id][vidx] += lSum.GetValueOrDefault(id);
                        pos[id][vidx] += lPos.GetValueOrDefault(id);
                        cnt[id][vidx] += lCnt[id];
                    }
            });
        }

        Console.WriteLine("| 駒 | " + string.Join(" | ", vers.Select(v => $"{v.Name} 平均 | {v.Name} 正の率")) + " |");
        Console.WriteLine("|---|" + string.Concat(vers.Select(_ => "--:|--:|")));
        int last = vers.Length - 1;
        foreach (string id in per.Keys.OrderByDescending(id => cnt[id][last] == 0 ? 0 : per[id][last] / cnt[id][last]))
        {
            string nm = UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id)?.Name ?? id;
            var cells = new List<string>();
            for (int v = 0; v < vers.Length; v++)
            {
                long c = cnt[id][v];
                cells.Add(c == 0 ? "—" : $"{per[id][v] / c:F2}");
                cells.Add(c == 0 ? "—" : $"{(double)pos[id][v] / c:P1}");
            }
            Console.WriteLine($"| {nm} | " + string.Join(" | ", cells) + " |");
        }
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

    // =================================================================================
    // 第162期 —— 目的変数を替える（`stage obj`）
    //
    // 指示書は design/PHASE162_OBJECTIVE_SPEC.md。前提は design/PHASE161_RELINE.md。
    //
    // **engine は1行も触らない。** `EngagementResult` が **`verbose: false` でも積んでいる**
    // `PlayerExits`（`SquadEntry`）と `Battles[i].TallyByUnit` を読むだけ——
    // 4つの目的変数はすべて**1回の走行から同時に**取れるので、戦闘の回数は第161期と変わらない。
    //
    // **列長の交絡を切るため、判定は `順路5`（列長5）と `地点3`（列長3）の両方で要求する**（§1-2）。
    // =================================================================================

    /// <summary>目的変数（第162期）。<b>どれも「戦った部隊戦ごとの項の和」</b>で、戦わなかった地点は 0。</summary>
    public enum Obj { Break = 0, Alive = 1, Hp = 2, Harm = 3 }

    public const int ObjCount = 4;

    public static readonly (Obj O, string Name, string Def)[] ObjDefs =
    {
        (Obj.Break, "突破度",
            "`EnemySquadsCleared + LastBattleAttrition`（列長で頭打ち）＝**いまの目的変数・対照**"),
        (Obj.Alive, "生存枚数",
            "Σ_戦 `PlayerExits[b].Alive` ÷ **出撃枚数**（1戦あたり 0..1）"),
        (Obj.Hp, "残HP割合",
            "Σ_戦 `max(0, PlayerExits[b].HpSum)` ÷ **編成の定義上総MaxHp**（1戦あたり 0..1）"),
        (Obj.Harm, "与えた総害",
            "Σ_戦 （その戦で味方が敵へ与えたダメージ）÷ **その敵部隊の定義上総MaxHp**"),
    };

    /// <summary>
    /// 行（編成）ごとに1度だけ作る定数。<b>目的変数の分母はここで固定する</b>——
    /// 「戦ごとに動く分母」を使うと部分点の意味が Battle ごとに変わる（<c>Engagement.cs</c> の
    /// <c>LastBattleAttrition</c> の doc と同じ理由）。
    /// </summary>
    sealed record ObjCtx(HashSet<string> Ids, int Deployed, int DefMaxHp, int[] FoeMax);

    static ObjCtx CtxOf(Formation f, Col col) => new(
        f.Occupied().Select(o => o.Def.Id).ToHashSet(),
        f.Occupied().Count(),
        f.Occupied().Sum(o => o.Def.MaxHp),
        col.Squads.Select(s => s.Occupied().Sum(o => o.Def.MaxHp)).ToArray());

    /// <summary>その部隊戦で味方（<b>出撃した駒</b>）が敵へ与えたダメージ。</summary>
    static int HarmOf(BattleResult r, ObjCtx c)
    {
        int d = 0;
        foreach ((string id, UnitTally t) in r.TallyByUnit) if (c.Ids.Contains(id)) d += t.DamageToEnemy;
        return d;
    }

    /// <summary><b>会戦版</b>の4つの目的変数を1回の走行から同時に取る。</summary>
    static double[] EngageObjs(Formation f, Col col, Ver v, int seed, ObjCtx c)
    {
        EngagementResult r = EngagementEngine.Run(new[] { f }, col.Squads, seed,
                                                  verbose: false, recover: v.Rec, boundary: v.Bnd);
        var o = new double[ObjCount];
        o[(int)Obj.Break] = BreakthroughDegree(r, col.Len);
        for (int b = 0; b < r.Battles.Count; b++)
        {
            o[(int)Obj.Alive] += c.Deployed == 0 ? 0 : (double)r.PlayerExits[b].Alive / c.Deployed;
            o[(int)Obj.Hp] += c.DefMaxHp == 0 ? 0 : Math.Max(0, r.PlayerExits[b].HpSum) / (double)c.DefMaxHp;
            int foeMax = c.FoeMax[r.Pairings[b].EnemySquad];
            o[(int)Obj.Harm] += foeMax == 0 ? 0 : HarmOf(r.Battles[b], c) / (double)foeMax;
        }
        return o;
    }

    /// <summary>
    /// <b>単発版</b>の4つ。<see cref="IndepDegree"/> の逐語の写しに、
    /// 同じ位置で同じ式の3つを足しただけ（打ち切り <c>break</c> も同じ場所）。
    /// </summary>
    static double[] IndepObjs(Formation f, Col col, int seed, ObjCtx c)
    {
        int cleared = 0;
        double attr = 0;
        var o = new double[ObjCount];
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
            o[(int)Obj.Alive] += c.Deployed == 0 ? 0 : (double)p.Count(u => u.IsAlive) / c.Deployed;
            o[(int)Obj.Hp] += c.DefMaxHp == 0 ? 0 : Math.Max(0, p.Sum(u => u.Hp)) / (double)c.DefMaxHp;
            o[(int)Obj.Harm] += defMax == 0 ? 0 : HarmOf(r, c) / (double)defMax;
            if (clearedE) cleared++;
            if (lostP) break;
        }
        o[(int)Obj.Break] = cleared >= col.Len ? col.Len : cleared + attr;
        return o;
    }

    static double[] EngageObjAvg(Formation f, Col col, Ver v)
    {
        var c = CtxOf(f, col);
        var s = new double[ObjCount];
        for (int seed = 0; seed < Seeds; seed++)
        {
            var o = EngageObjs(f, col, v, seed, c);
            for (int k = 0; k < ObjCount; k++) s[k] += o[k];
        }
        for (int k = 0; k < ObjCount; k++) s[k] /= Seeds;
        return s;
    }

    static double[] IndepObjAvg(Formation f, Col col)
    {
        var c = CtxOf(f, col);
        var s = new double[ObjCount];
        for (int seed = 0; seed < Seeds; seed++)
        {
            var o = IndepObjs(f, col, seed, c);
            for (int k = 0; k < ObjCount; k++) s[k] += o[k];
        }
        for (int k = 0; k < ObjCount; k++) s[k] /= Seeds;
        return s;
    }

    // ---- §4 の線（**測る前に固定した**。線1 / 線2 の高さは第161期と同じ） ----
    //  線3 **`順路5` と `地点3` の両方で 1 と 2**（列長の交絡を切る）／ 線4 本丸 = 線3 を満たす目的変数が1つ以上

    // §4。**実装前に書き切り、外れても消さない**（規約・第64期）。
    public static readonly (string Key, string Text)[] ObjPredictions =
    {
        ("P1", "**`生存枚数` は床に張り付く**（突破度 0 の行がそのまま 0 になる）——Q0-4 で落ちる"),
        ("P2", "**`与えた総害` の分子（会戦の帰属SD）が突破度より大きい**（負けた地点の削りが乗るので天井が外れる）"),
        ("P3", "**`与えた総害` は分母（単発の帰属SD）も一緒に大きくなる**ので、比は思ったほど伸びない"),
        ("P4", "**`残HP割合` は rho を下げる**（消耗そのものを測るので持ち越しの効きが直接出る）"),
        ("P5", "**線3（両方の列）を通る目的変数は 0 個**——`地点3` で通っても `順路5` で落ちる"),
    };

    static void ObjRun(string arg)
    {
        string mode = arg.Trim();
        if (mode.StartsWith("phase0")) { ObjPhase0(); return; }
        if (mode.StartsWith("floor")) { ObjFloor(); return; }

        var rows = CompareBuilds();
        var jobs = new List<(int Row, int Slot, UnitDef Def)>();
        for (int i = 0; i < rows.Length; i++)
            foreach ((int sl, UnitDef d) in rows[i].F.Occupied()) jobs.Add((i, sl, d));
        var ids = jobs.Select(j => j.Def.Id).Distinct().ToArray();
        var uname = ids.ToDictionary(id => id, id => jobs.First(j => j.Def.Id == id).Def.Name);
        var slotsOf = ids.ToDictionary(id => id,
            id => Enumerable.Range(0, jobs.Count).Where(j => jobs[j].Def.Id == id).ToArray());

        // 点は「列 版」の完全一致（第161期 (e)）。既定は §4 の4点。
        string[] want = mode.Length > 0
            ? mode.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray()
            : new[] { "順路5 R0", "順路5 BCarry", "地点3 R0", "地点3 BCarry" };
        var pts = new List<(string Cn, string Vn)>();
        foreach (string w in want)
        {
            var t = w.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (t.Length != 2) { Console.WriteLine($"stage obj: 点 `{w}` が読めない（「列 版」）。"); return; }
            _ = ColOf(t[0]); _ = VerOf(t[1]);
            pts.Add((t[0], t[1]));
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // ---- 単発側は列ごとに1度だけ（版に依らない） ----
        var iFull = new Dictionary<string, double[][]>();   // [行][目的変数]
        var iGot = new Dictionary<string, double[][]>();    // [枠][目的変数]
        foreach (string cn in pts.Select(p => p.Cn).Distinct())
        {
            var col = ColOf(cn);
            var fI = new double[rows.Length][];
            Parallel.For(0, rows.Length, i => fI[i] = IndepObjAvg(rows[i].F, col));
            var gI = new double[jobs.Count][];
            Parallel.For(0, jobs.Count, j => gI[j] = IndepObjAvg(SwapOne(rows[jobs[j].Row].F, jobs[j].Slot), col));
            iFull[cn] = fI; iGot[cn] = gI;
        }

        // ---- 会戦側（点ごと） ----
        var recs = new List<(string Cn, string Vn, Col C, double[][] UE, double[][] UI)>();
        foreach (var (cn, vn) in pts)
        {
            var col = ColOf(cn); var ver = VerOf(vn);
            var fE = new double[rows.Length][];
            Parallel.For(0, rows.Length, i => fE[i] = EngageObjAvg(rows[i].F, col, ver));
            var gE = new double[jobs.Count][];
            Parallel.For(0, jobs.Count, j => gE[j] = EngageObjAvg(SwapOne(rows[jobs[j].Row].F, jobs[j].Slot), col, ver));
            double[][] fI = iFull[cn], gI = iGot[cn];
            // 駒ごとの帰属（在席枠の平均）を目的変数ごとに
            var uE = new double[ObjCount][]; var uI = new double[ObjCount][];
            for (int k = 0; k < ObjCount; k++)
            {
                var sE = new double[jobs.Count]; var sI = new double[jobs.Count];
                for (int j = 0; j < jobs.Count; j++)
                {
                    sE[j] = fE[jobs[j].Row][k] - gE[j][k];
                    sI[j] = fI[jobs[j].Row][k] - gI[j][k];
                }
                uE[k] = ids.Select(id => slotsOf[id].Average(j => sE[j])).ToArray();
                uI[k] = ids.Select(id => slotsOf[id].Average(j => sI[j])).ToArray();
            }
            recs.Add((cn, vn, col, uE, uI));
        }

        Console.WriteLine("# 第162期 段A/B —— 目的変数を替える");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` {rows.Length} 行 × 延べ {jobs.Count} 枠 × 駒 {ids.Length} 体 × seed 0..{Seeds - 1}。");
        Console.WriteLine("**4つの目的変数は1回の走行から同時に取る**（戦闘の回数は第161期の1点ぶんと同じ）。");
        Console.WriteLine();
        foreach (var (o, nm, df) in ObjDefs) Console.WriteLine($"- **{nm}**: {df}");
        Console.WriteLine();

        Console.WriteLine("## B-1. 目的変数 × 点（**駒の水準**）");
        Console.WriteLine();
        Console.WriteLine("| 目的変数 | 点 | 列長 | rho(全) | **rho(絞)** | 絞りの分母 | 線1 | 会戦の帰属SD | 単発の帰属SD | **比** | 線2 | 会戦帰属の平均 | 単発帰属の平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|:-:|--:|--:|--:|:-:|--:|--:|");
        var pass = new Dictionary<(int K, string P), bool>();
        foreach (var (o, nm, _) in ObjDefs)
        {
            int k = (int)o;
            foreach (var r in recs)
            {
                double q = RhoQ1(r.UI[k]);
                var (rhoAll, nAll) = RhoOf(r.UE[k], r.UI[k], -1);
                var (rhoCut, nCut) = RhoOf(r.UE[k], r.UI[k], q);
                double sd = Sd(r.UE[k]), sdI = Sd(r.UI[k]);
                double ratio = sdI > 0 ? sd / sdI : double.NaN;
                bool l1 = rhoCut <= RhoLine, l2 = ratio >= RatioLine;
                pass[(k, r.Cn + " " + r.Vn)] = l1 && l2;
                Console.WriteLine($"| {nm} | {r.Cn} × {r.Vn} | {r.C.Len} | {rhoAll:F3} | **{rhoCut:F3}** | {nCut} / {nAll} "
                    + $"| {(l1 ? "○" : "×")} | {sd:F3} | {sdI:F3} | **{ratio:F3}** | {(l2 ? "○" : "×")} "
                    + $"| {r.UE[k].Average():+0.000;-0.000} | {r.UI[k].Average():+0.000;-0.000} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("絞りの閾値 q1（|単発帰属| の最小四分位）は**列 × 目的変数**ごとに1つ。");
        Console.WriteLine();

        Console.WriteLine("## B-2. 同値塊（規約 (G13)）");
        Console.WriteLine();
        Console.WriteLine("| 目的変数 | 点 | 会戦帰属の最大同値塊 | 厳密に 0 の駒 | 単発帰属の最大同値塊 | 厳密に 0 の駒 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var (o, nm, _) in ObjDefs)
        {
            int k = (int)o;
            foreach (var r in recs)
            {
                var (tE, zE) = RhoTies(r.UE[k]); var (tI, zI) = RhoTies(r.UI[k]);
                Console.WriteLine($"| {nm} | {r.Cn} × {r.Vn} | {tE:P1} | {zE} / {ids.Length} | {tI:P1} | {zI} / {ids.Length} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## B-3. 判定（**線は §4 で測る前に固定した**）");
        Console.WriteLine();
        Console.WriteLine($"線1 **rho(絞) <= {RhoLine:F2}** ／ 線2 **会戦の帰属SD ÷ 単発の帰属SD >= {RatioLine:F2}**  ");
        Console.WriteLine("線3 **`順路5` と `地点3` の両方で 1 と 2**（列長の交絡を切る・§1-2）  ");
        Console.WriteLine("線4 **本丸 = 線3 を満たす目的変数が1つ以上**");
        Console.WriteLine();
        var vers = recs.Select(r => r.Vn).Distinct().ToArray();
        Console.WriteLine("| 目的変数 | 版 | 順路5（列長5） | 地点3（列長3） | **線3** |");
        Console.WriteLine("|---|---|:-:|:-:|:-:|");
        int win = 0;
        foreach (var (o, nm, _) in ObjDefs)
        {
            int k = (int)o;
            foreach (string vn in vers)
            {
                bool a = pass.TryGetValue((k, "順路5 " + vn), out bool pa) && pa;
                bool b = pass.TryGetValue((k, "地点3 " + vn), out bool pb) && pb;
                bool got = a && b;
                if (got) win++;
                Console.WriteLine($"| {nm} | {vn} | {(a ? "○" : "×")} | {(b ? "○" : "×")} | {(got ? "**○**" : "×")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"**線3 を通った（目的変数 × 版）: {win}。本丸は {(win > 0 ? "○" : "×")}。**");
        Console.WriteLine();

        Console.WriteLine("## B-4. §6-1 —— 分子（会戦の帰属SD）は突破度からどれだけ伸びたか");
        Console.WriteLine();
        Console.WriteLine("| 点 | 突破度 | 生存枚数 | 残HP割合 | 与えた総害 | 最大の伸び |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var r in recs)
        {
            double b0 = Sd(r.UE[0]);
            var row = Enumerable.Range(0, ObjCount).Select(k => Sd(r.UE[k])).ToArray();
            Console.WriteLine($"| {r.Cn} × {r.Vn} | {row[0]:F3} | {row[1]:F3} | {row[2]:F3} | {row[3]:F3} "
                + $"| **×{(b0 > 0 ? row.Skip(1).Max() / b0 : double.NaN):F2}** |");
        }
        Console.WriteLine();

        Console.WriteLine("## B-5. 予測（**実装前に書いた。外れても消さない**）");
        Console.WriteLine();
        foreach (var (k, t) in ObjPredictions) Console.WriteLine($"{k}. {t}");
        Console.WriteLine();

        Console.WriteLine("## B-6. 段C —— 動いた駒（順位が入れ替わった上位10体）");
        Console.WriteLine();
        foreach (var r in recs)
        {
            foreach (var (o, nm, _) in ObjDefs)
            {
                int k = (int)o;
                var rE = AverageRanksDesc(r.UE[k]);
                var rI = AverageRanksDesc(r.UI[k]);
                var ord = Enumerable.Range(0, ids.Length)
                    .OrderByDescending(x => Math.Abs(rI[x] - rE[x])).Take(10).ToArray();
                Console.WriteLine($"### {r.Cn} × {r.Vn} / {nm}");
                Console.WriteLine();
                Console.WriteLine("| 駒 | 在席枠 | 単発順位 | 会戦順位 | **Δ順位** | 単発帰属 | 会戦帰属 | 差 | 札 |");
                Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|");
                foreach (int x in ord)
                {
                    var def = jobs.First(j => j.Def.Id == ids[x]).Def;
                    string traits = def.Traits.Count == 0 ? "—" : string.Join(" / ", def.Traits);
                    Console.WriteLine($"| {uname[ids[x]]} | {slotsOf[ids[x]].Length} | {rI[x]:F1} | {rE[x]:F1} "
                        + $"| **{rI[x] - rE[x]:+0.0;-0.0}** | {r.UI[k][x]:+0.000;-0.000} | {r.UE[k][x]:+0.000;-0.000} "
                        + $"| {r.UE[k][x] - r.UI[k][x]:+0.000;-0.000} | {traits} |");
                }
                Console.WriteLine();
            }
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
    }

    /// <summary>
    /// Q0-4（**`ablate` を回す前に数える**）。61 行の目的変数の分布——
    /// <b>最小値ちょうどの行数</b>と<b>最大値ちょうどの行数</b>が 15 を超えたら、
    /// その候補はそこで落とす（§2-1 の条件2）。
    /// </summary>
    static void ObjFloor()
    {
        var rows = CompareBuilds();
        var pts = new[] { ("順路5", "R0"), ("地点3", "R0") };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Console.WriteLine("# 第162期 段0 Q0-4 —— 床と天井（**`ablate` を回す前**）");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` {rows.Length} 行 × seed 0..{Seeds - 1}。**素体差し替えは1回も回していない**"
            + $"（{rows.Length} 行ぶんの走行だけ）。線は **最小値ちょうど {ZeroMax} 行以下 かつ 最大値ちょうど {FullMax} 行以下**。");
        Console.WriteLine();
        Console.WriteLine("| 点 | 目的変数 | 最小値 | **=最小の行** | 最大値 | **=最大の行** | 中央値 | 平均 | 行のSD | 条件2 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|:-:|");
        foreach (var (cn, vn) in pts)
        {
            var col = ColOf(cn); var ver = VerOf(vn);
            var vals = new double[rows.Length][];
            Parallel.For(0, rows.Length, i => vals[i] = EngageObjAvg(rows[i].F, col, ver));
            foreach (var (o, nm, _) in ObjDefs)
            {
                int k = (int)o;
                var v = vals.Select(x => x[k]).ToArray();
                double mn = v.Min(), mx = v.Max();
                int nmn = v.Count(x => Math.Abs(x - mn) < 1e-9), nmx = v.Count(x => Math.Abs(x - mx) < 1e-9);
                var srt = v.OrderBy(x => x).ToArray();
                bool ok = nmn <= ZeroMax && nmx <= FullMax;
                Console.WriteLine($"| {cn} × {vn} | {nm} | {mn:F3} | **{nmn}** | {mx:F3} | **{nmx}** "
                    + $"| {srt[srt.Length / 2]:F3} | {v.Average():F3} | {Sd(v):F3} | {(ok ? "○" : "**×**")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
    }

    /// <summary>Q0-2 / Q0-3 / Q0-5（**戦闘0回**）。<c>EngagementResult</c> から取れる量を実装から引き直す。</summary>
    static void ObjPhase0()
    {
        Console.WriteLine("# 第162期 段0 —— Phase 0（**戦闘0回**）");
        Console.WriteLine();

        Console.WriteLine("## Q0-2. `EngagementResult` から取れる量");
        Console.WriteLine();
        Console.WriteLine("| フィールド | `verbose: false` で積むか | 出どころ |");
        Console.WriteLine("|---|:-:|---|");
        foreach (var p in typeof(EngagementResult).GetProperties())
        {
            bool vb = p.Name is "Openings";
            string note = p.Name switch
            {
                "Openings" => "**`verbose` 時のみ**（各要素が空になる）",
                "Battles" => "各部隊戦の `BattleResult`（`Log` / `Events` だけが verbose 依存）",
                "PlayerEntries" or "EnemyEntries" => "入場戦力。doc に「verbose に関係なく積む」",
                "PlayerExits" => "**退場戦力**。`Run` の直後に `Snapshot(current)`",
                _ => "—",
            };
            Console.WriteLine($"| `{p.Name}` | {(vb ? "×" : "○")} | {note} |");
        }
        Console.WriteLine();
        Console.WriteLine("`SquadEntry` のフィールド: "
            + string.Join(" / ", typeof(SquadEntry).GetProperties().Select(p => $"`{p.Name}`")) + "。");
        Console.WriteLine();
        Console.WriteLine("**注意（実装を読んで判明）**: `Snapshot` は `squad.Sum(u => u.Hp)` で、"
            + "`BattleEngine` は `target.Hp -= amount` の後に **0 で切っていない**。"
            + "したがって `HpSum` には**倒れた駒の負の HP** が入りうる。"
            + "**会戦側と単発側で同じ式（合計を取ってから 0 で切る）にしてある**ので、比の両側で同じ扱いになる。");
        Console.WriteLine();

        Console.WriteLine("## Q0-3. 候補ごとに、engine を触らずに集計できるか");
        Console.WriteLine();
        Console.WriteLine("| 候補 | 窓口 | engine を触るか | 条件1（負けた後も定義される） |");
        Console.WriteLine("|---|---|:-:|---|");
        Console.WriteLine("| 突破度（対照） | `EnemySquadsCleared` / `LastBattleAttrition` | 不要 | **×**（列長で頭打ち・部分点は最後の1地点だけ） |");
        Console.WriteLine("| 生存枚数 | `PlayerExits[b].Alive` | 不要 | △（負けた戦は 0 だが、それまでの戦の分は残る） |");
        Console.WriteLine("| 残HP割合 | `PlayerExits[b].HpSum` | 不要 | ○ |");
        Console.WriteLine("| 与えた総害 | `Battles[b].TallyByUnit[id].DamageToEnemy` | 不要 | **○**（負けた地点で削った分がそのまま乗る） |");
        Console.WriteLine();
        Console.WriteLine("**4つとも engine を触らずに取れる**（`TallyByUnit` も `verbose` に依存しない）。");
        Console.WriteLine();

        Console.WriteLine("### 与えた総害の分子の作り方（**`Def.Id` の衝突と召喚の漏れ**）");
        Console.WriteLine();
        var pIds = CompareBuilds().SelectMany(r => r.F.Occupied().Select(o => o.Def.Id)).Distinct().ToHashSet();
        var eIds = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def.Id)).Distinct().ToHashSet();
        var clash = pIds.Intersect(eIds).ToArray();
        Console.WriteLine($"`CompareBuilds()` の味方 `Def.Id` {pIds.Count} 種 と 5波の敵 `Def.Id` {eIds.Count} 種 の"
            + $"**重なりは {clash.Length} 件**（{(clash.Length == 0 ? "○ 衝突なし" : string.Join(" / ", clash))}）。");
        Console.WriteLine();
        Console.WriteLine("**分子は「出撃した5枚の `Def.Id`」に限る**ので、**戦闘中に湧いた味方（胞子・餌・亡者）の"
            + "与ダメージは数に入らない**。これは会戦側と単発側で同じ扱いなので比には効かないが、"
            + "**召喚を持つ駒の帰属を過小に見る**（記録しておく）。");
        Console.WriteLine();

        Console.WriteLine("## Q0-5. 単発側の定義");
        Console.WriteLine();
        Console.WriteLine("`IndepObjs` は `IndepDegree`（第157期）の**逐語の写し**に、"
            + "**同じ位置で同じ式**の3つを足しただけ。**打ち切り（`if (lostP) break;`）も同じ場所**にある。");
        Console.WriteLine();
        Console.WriteLine("| 目的変数 | 会戦側 | 単発側 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 生存枚数 | `PlayerExits[b].Alive` ÷ 出撃枚数 | `p.Count(u => u.IsAlive)` ÷ 出撃枚数 |");
        Console.WriteLine("| 残HP割合 | `max(0, PlayerExits[b].HpSum)` ÷ 定義上総MaxHp | `max(0, p.Sum(u => u.Hp))` ÷ 同 |");
        Console.WriteLine("| 与えた総害 | `Battles[b].TallyByUnit` | 同じ `BattleResult` の同じ辞書 |");
        Console.WriteLine();
        Console.WriteLine("**打ち切りは分母にも乗る**（第159期 R239）——`IndepDegree` が"
            + "「最初に負けた時点で止まる」ので、**単発側にも同じ天井がある**。"
            + "**打ち切りを外さなかった**のは、外すと**会戦側（負けたら会戦が終わる）と定義が食い違う**ため"
            + "——比の両側は**同じ打ち切り**で揃えてある。");
        Console.WriteLine();

        Console.WriteLine("## 予測（**測る前に書いた**）");
        Console.WriteLine();
        foreach (var (k, t) in ObjPredictions) Console.WriteLine($"{k}. {t}");
    }

}
