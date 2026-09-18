using BattleCore;
using static Common;

// =====================================================================================
// haste モード（第149期） —— 「行動順」という通貨にいくらの値が付くかを測る
//
// 指示書は design/PHASE149_HASTE_SPEC.md ／ 報告は design/PHASE149_HASTE.md。
//
// **駒も代金も作らない。**「毎ターン味方1体を行動順の先頭に出す」だけを掛けて、
// **行動順という通貨の値段**を測る。値が付かなければそこで終わる。
//
//     dotnet run --project BattleSim -c Release 0 haste phase0   # 前提を実装から引き直す（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 haste run      # 段B/C（V0 / Slowest / Strongest × compare 61行）
//     dotnet run --project BattleSim -c Release 0 haste check    # 自己検査（必須1・必須4 ＋ (a)〜(e)）
//
// **台を作らない。** 通貨の値段を測るので、台を選ぶこと自体が答えを作る（第143期の埋め草の則）。
// 線は帯全体（`compare` 61 行）で引く。
// =====================================================================================

static class HasteDiag
{
    const int Seeds = 200;

    /// <summary>「遅いから外された層」（指示書 Q0-6）。この駒を含む行と含まない行で分けて集計する。</summary>
    static readonly string[] SlowLayer =
    {
        UnitCatalog.Dolga.Id, UnitCatalog.Golm.Id, UnitCatalog.Gald.Id,
        UnitCatalog.Ban.Id, UnitCatalog.Sekki.Id, UnitCatalog.Kado.Id,
    };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": Body(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("haste: モードは phase0 / run / check。");
                return;
        }
    }

    // =================================================================================
    // phase0 —— 実装から引き直す（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第149期 `haste phase0` —— 前提を実装から引き直す（戦闘0回）");
        Console.WriteLine();

        // Q0-5. 逆位の保持者が盤上にいるか
        int invStages = 0;
        var invNames = new List<string>();
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
            foreach (var o in EnemyCatalog.Stages[st].Enemy.Occupied())
                if (o.Def.Traits.Contains(TraitId.Inversion))
                { invStages++; invNames.Add("第" + (st + 1) + "波 " + o.Def.Name); }
        int invRoster = UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Inversion));

        Console.WriteLine("## Q0-5. 逆位（`TraitId.Inversion`）の保持者");
        Console.WriteLine();
        Console.WriteLine("| | 枚数 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine("| `UnitCatalog.All`（編成に選べる52枚） | " + invRoster + " |");
        Console.WriteLine("| `EnemyCatalog.Stages` の5波 | " + invStages + " |");
        Console.WriteLine();
        Console.WriteLine(invStages + invRoster == 0
            ? "**盤上に 0 枚。`inverted` は常に偽で、この期の測定は逆位の影響を受けない。**"
            : "**保持者がいる: " + string.Join(" / ", invNames) + "。変数が2つになるので測り方を見直すこと。**");
        Console.WriteLine();

        // Q0-4. 味方ロスターの速さ分布
        Console.WriteLine("## Q0-4. 味方ロスター 52 枚の速さ分布（`Slowest` が誰を拾うか）");
        Console.WriteLine();
        Console.WriteLine("| 速さ | 枚数 | 駒 |");
        Console.WriteLine("|--:|--:|---|");
        foreach (var g in UnitCatalog.All.GroupBy(d => d.Speed).OrderBy(g => g.Key))
            Console.WriteLine("| " + g.Key + " | " + g.Count() + " | "
                              + string.Join("・", g.Select(d => d.Name)) + " |");
        Console.WriteLine();

        // 敵側の速さ（先頭へ出すと誰を追い越すか）
        Console.WriteLine("## Q0-3. 敵の速さ（先頭へ出すと敵全員より先に動く）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵の速さ（降順） |");
        Console.WriteLine("|---|---|");
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
            Console.WriteLine("| 第" + (st + 1) + "波 | " + string.Join(" / ", EnemyCatalog.Stages[st].Enemy
                .Occupied().OrderByDescending(o => o.Def.Speed)
                .Select(o => o.Def.Name + " " + o.Def.Speed)) + " |");
        Console.WriteLine();

        // Q0-6. 遅い層を含む行の数（分母を先に数える。規約 (G4)）
        var rows = CompareBuilds();
        int with = rows.Count(r => HasSlow(r.F));
        Console.WriteLine("## Q0-6. 「遅い層」を含む行の分母（規約 (G4)）");
        Console.WriteLine();
        Console.WriteLine("- 遅い層 ＝ " + string.Join("・", SlowLayer.Select(id =>
                              UnitCatalog.Everyone.First(d => d.Id == id).Name + " 速"
                              + UnitCatalog.Everyone.First(d => d.Id == id).Speed)));
        Console.WriteLine("- 含む行: **" + with + " / " + rows.Length + "**"
                          + "（含まない行: " + (rows.Length - with) + "）");
        int priWith = rows.Count(r => Baseline.PrimaryRows.Contains(r.Name) && HasSlow(r.F));
        int pri = rows.Count(r => Baseline.PrimaryRows.Contains(r.Name));
        Console.WriteLine("- 主判定行のうち含む行: **" + priWith + " / " + pri + "**");
        Console.WriteLine();
        Console.WriteLine("**分母のほぼ全部を選ぶ条件は何も言っていない**（第122期）ので、"
                          + "含まない行が " + (rows.Length - with) + " 行しか無ければ予測2 は原理的に読めない。");
        Console.WriteLine();

        // 編成の中でいちばん遅い駒は誰か（Slowest が実際に拾う駒）
        Console.WriteLine("## Q0-4'. 各行で `Slowest` が拾う駒（開戦時の盤面・同値は席順）");
        Console.WriteLine();
        var picked = new Dictionary<string, int>();
        foreach (var (_, f) in rows)
        {
            var o = f.Occupied().OrderBy(x => x.Def.Speed).ThenBy(x => x.Slot).First();
            picked[o.Def.Name] = picked.GetValueOrDefault(o.Def.Name) + 1;
        }
        Console.WriteLine("| 駒 | 拾われる行数 |");
        Console.WriteLine("|---|--:|");
        foreach (var kv in picked.OrderByDescending(k => k.Value).ThenBy(k => k.Key))
            Console.WriteLine("| " + kv.Key + " | " + kv.Value + " |");
        Console.WriteLine();
    }

    static bool HasSlow(Formation f) => f.Occupied().Any(o => SlowLayer.Contains(o.Def.Id));

    // =================================================================================
    // run —— 段B（Slowest）と段C（Strongest）。**同じ帯・同じ seed**
    // =================================================================================

    static void Body()
    {
        Console.WriteLine("# 第149期 `haste run` —— 行動順という通貨の値段");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ 現行の既定（`HastePick.None`）／ S ＝ `Slowest` ／ A ＝ `Strongest`。**");
        Console.WriteLine("`compare` 61 行 × 5 波 × seed 0.." + (Seeds - 1)
                          + "。**判定の分母は第2〜5波**（規約 (G10)）。");
        Console.WriteLine();

        var rows = CompareBuilds();
        var v0 = new Res[rows.Length];
        var vs = new Res[rows.Length];
        var va = new Res[rows.Length];
        Parallel.For(0, rows.Length, i =>
        {
            v0[i] = Measure(rows[i].F, HasteRule.Default);
            vs[i] = Measure(rows[i].F, HasteRule.Slowest);
            va[i] = Measure(rows[i].F, HasteRule.Strongest);
        });

        // ---- 表A. 全体の帰属 ----
        Console.WriteLine("## 表A. `compare` 61 行の帰属（波ごと）");
        Console.WriteLine();
        Console.WriteLine("| 波 | V0 | S 遅い | Δ(S) | A 強い | Δ(A) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        for (int st = 0; st < 5; st++)
        {
            double a = v0.Average(r => r.Win[st]), b = vs.Average(r => r.Win[st]), c = va.Average(r => r.Win[st]);
            Console.WriteLine("| 第" + (st + 1) + "波" + (st == 0 ? "（参考・分母外）" : "") + " | "
                              + F1(a) + " | " + F1(b) + " | " + Sg(b - a) + " | " + F1(c) + " | " + Sg(c - a) + " |");
        }
        double A0 = Mean25(v0), AS = Mean25(vs), AA = Mean25(va);
        Console.WriteLine("| **第2〜5波** | **" + F1(A0) + "** | **" + F1(AS) + "** | **" + Sg(AS - A0)
                          + "** | **" + F1(AA) + "** | **" + Sg(AA - A0) + "** |");
        Console.WriteLine();

        // ---- 表B. 遅い層を含む行／含まない行 ----
        Console.WriteLine("## 表B. 「遅い層」を含む行／含まない行（予測2）");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | V0 | Δ(S) | Δ(A) |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (bool want in new[] { true, false })
        {
            var idx = Enumerable.Range(0, rows.Length).Where(i => HasSlow(rows[i].F) == want).ToArray();
            if (idx.Length == 0) continue;
            double a = idx.Average(i => Mean25(v0[i])), b = idx.Average(i => Mean25(vs[i])), c = idx.Average(i => Mean25(va[i]));
            Console.WriteLine("| " + (want ? "含む" : "含まない") + " | " + idx.Length + " | "
                              + F1(a) + " | " + Sg(b - a) + " | " + Sg(c - a) + " |");
        }
        Console.WriteLine();

        // ---- 表C. 行ごと（動いた行を大きい順に） ----
        Console.WriteLine("## 表C. 行ごとの帰属（第2〜5波平均・|Δ(S)| の大きい順に上位20）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 遅い層 | V0 | Δ(S) | Δ(A) | 前倒し/戦 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|");
        var order = Enumerable.Range(0, rows.Length)
            .OrderByDescending(i => Math.Abs(Mean25(vs[i]) - Mean25(v0[i]))).Take(20);
        foreach (int i in order)
            Console.WriteLine("| " + rows[i].Name + " | " + (HasSlow(rows[i].F) ? "○" : "—") + " | "
                              + F1(Mean25(v0[i])) + " | " + Sg(Mean25(vs[i]) - Mean25(v0[i])) + " | "
                              + Sg(Mean25(va[i]) - Mean25(v0[i])) + " | " + vs[i].Moves.ToString("F2") + " |");
        Console.WriteLine();

        // ---- 表D. 拒否権と情報セル ----
        var priIdx = Enumerable.Range(0, rows.Length).Where(i => Baseline.PrimaryRows.Contains(rows[i].Name)).ToArray();
        Console.WriteLine("## 表D. 拒否権（**この期は採用しないので情報として記録するだけ**）");
        Console.WriteLine();
        Console.WriteLine("| | V0 | S | A |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine("| 主判定" + priIdx.Length + "行・第五波（歯止め "
                          + Baseline.PrimaryFifthFloor.ToString("F1") + "） | "
                          + F1(priIdx.Average(i => v0[i].Win[4])) + " | "
                          + F1(priIdx.Average(i => vs[i].Win[4])) + " | "
                          + F1(priIdx.Average(i => va[i].Win[4])) + " |");
        Console.WriteLine("| 情報セル（全61行・第2〜5波） | " + Info(v0) + " | " + Info(vs) + " | " + Info(va) + " |");
        Console.WriteLine("| 情報セル（主判定" + priIdx.Length + "行） | "
                          + Info(priIdx.Select(i => v0[i])) + " | " + Info(priIdx.Select(i => vs[i]))
                          + " | " + Info(priIdx.Select(i => va[i])) + " |");
        Console.WriteLine("| 第五波 95% 超の行 | " + v0.Count(r => r.Win[4] > 95) + " | "
                          + vs.Count(r => r.Win[4] > 95) + " | " + va.Count(r => r.Win[4] > 95) + " |");
        Console.WriteLine();
        foreach (var (tag, v) in new[] { ("S", vs), ("A", va) })
        {
            int veto = 0, vetoPri = 0;
            for (int i = 0; i < rows.Length; i++)
                for (int st = 0; st < 5; st++)
                    if (v[i].Win[st] - v0[i].Win[st] <= -10.0)
                    { veto++; if (Baseline.PrimaryRows.Contains(rows[i].Name)) vetoPri++; }
            Console.WriteLine("- **" + tag + "**: −10.0pt 以上のセル **" + veto + "**（うち主判定 " + vetoPri + "）");
        }
        Console.WriteLine();

        // ---- 表E. 検算 ----
        Console.WriteLine("## 表E. 検算");
        Console.WriteLine();
        long t0 = v0.Sum(r => r.Turns), ts = vs.Sum(r => r.Turns), ta = va.Sum(r => r.Turns);
        Console.WriteLine("| | V0 | S | A |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine("| `TurnLoopCalls` の総和 | " + t0 + " | " + ts + " | " + ta + " |");
        Console.WriteLine("| 前倒し/戦（`HasteMoves`） | " + v0.Average(r => r.Moves).ToString("F2") + " | "
                          + vs.Average(r => r.Moves).ToString("F2") + " | "
                          + va.Average(r => r.Moves).ToString("F2") + " |");
        Console.WriteLine("| 決着T（第2〜5波） | " + v0.Average(r => r.Settle).ToString("F2") + " | "
                          + vs.Average(r => r.Settle).ToString("F2") + " | "
                          + va.Average(r => r.Settle).ToString("F2") + " |");
        Console.WriteLine();
        Console.WriteLine("**予測5（`TurnLoopCalls` が版間で一致）: "
                          + (t0 == ts && t0 == ta ? "○" : "×（延べ手番数が動いている）") + "**");
        Console.WriteLine();

        // ---- 判定 ----
        double best = Math.Max(AS - A0, AA - A0);
        Console.WriteLine("## 判定（指示書 §4 —— 採否ではなく「次に進むか」の線）");
        Console.WriteLine();
        Console.WriteLine("- 帰属の最大（`compare` 全体・第2〜5波）: **" + Sg(best) + "pt**"
                          + "（S " + Sg(AS - A0) + " / A " + Sg(AA - A0) + "）");
        Console.WriteLine("- 区分: **" + (best < 6.0 ? "+6pt 未満 → ここで終わる（行動順は安い通貨）"
                              : best < 15.0 ? "+6〜+15pt → 条件付き（どの行で効いたかを見て載せる先を次期に決める）"
                              : "+15pt 以上 → 通貨として成立（次期に「誰に、どう払わせるか」の設計へ）") + "**");
        Console.WriteLine();
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第149期 自己検査 —— `haste check`");
        Console.WriteLine();

        string bal = arg.Length > 0 ? arg : "docs/balance.md";
        Console.WriteLine("## (a) 必須1: 既定（`Pick = None`）の `compare` 305 セルが `" + bal + "` と 0 件");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**比較先が見つからない。**");
        else
        {
            var want = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(bal))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                string[] c = line.Split('|', StringSplitOptions.TrimEntries);
                if (c.Length < 8) continue;
                var v = new double[5];
                bool ok = true;
                for (int i = 0; i < 5; i++)
                    if (!double.TryParse(c[i + 2].Replace("%", ""), out v[i])) { ok = false; break; }
                if (ok) want[c[1]] = v;
            }
            int bad = 0, cells = 0;
            foreach (var (name, f) in CompareBuilds())
            {
                if (!want.TryGetValue(name, out double[]? w)) { Console.WriteLine("- 行が引けない: " + name); continue; }
                double[] got = Measure(f, HasteRule.Default).Win;
                for (int st = 0; st < 5; st++) { cells++; if (Math.Abs(got[st] - w[st]) > 0.001) bad++; }
            }
            Console.WriteLine("- 突き合わせ **" + cells + " セル / 食い違い " + bad + " 件** → "
                              + (bad == 0 ? "**○**" : "**×**"));
        }
        Console.WriteLine();

        Console.WriteLine("## (b) 必須4: `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        string eng = ReadSrc("BattleCore/BattleEngine.cs");
        int pickOne = Count(eng, "PickOne(") + Count(ReadSrc("BattleCore/Traits.cs"), "PickOne(");
        Console.WriteLine("- `PickOne(` の出現数（`BattleEngine.cs` ＋ `Traits.cs`）: **" + pickOne + "**"
                          + "（第148期 HEAD と同数なら ○）");
        Console.WriteLine();

        Console.WriteLine("## (c) 触った箇所が `order` の確定後の1箇所だけか");
        Console.WriteLine();
        Console.WriteLine("- `ctx.Haste.` を読む箇所: **" + Count(eng, "ctx.Haste.")
                          + "**（2 ＝ 同じ1ブロックの中の門と選び方だけなら ○）");
        Console.WriteLine("- `ctx.Shuffle(` の呼び出し: **" + Count(eng, "ctx.Shuffle(") + "**（第148期と同数なら ○）");
        Console.WriteLine();

        Console.WriteLine("## (d) 延べ手番数の検算");
        Console.WriteLine();
        Console.WriteLine("**指示書 §5-4 の「`TurnLoopCalls` が段A/B/C で一致」は原理的に成り立たない**"
                          + "——順序は盤面を動かすので決着ターン数が動き、"
                          + "**分母がターン数である量はそれに引きずられる**（第113期）。"
                          + "成り立つのは「**その<u>ターンの</u> `order` の要素数が変わらない**」のほうなので、"
                          + "engine に `HasteCountMismatch` を置いて直接数える。");
        Console.WriteLine();
        long[] t = new long[3];
        long mismatch = 0;
        double[] settle = new double[3];
        int battles = 0;
        var vers = new[] { HasteRule.Default, HasteRule.Slowest, HasteRule.Strongest };
        foreach (var (_, f) in CompareBuilds())
            for (int st = 0; st < 5; st++)
                for (int seed = 0; seed < 20; seed++)
                {
                    for (int v = 0; v < 3; v++)
                    {
                        BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                            verbose: false, haste: vers[v]);
                        t[v] += res.TurnLoopCalls;
                        settle[v] += res.Turns;
                        mismatch += res.HasteCountMismatch;
                    }
                    battles++;
                }
        Console.WriteLine("| | V0 | S | A |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine("| `TurnLoopCalls` の総和（seed 0..19） | " + t[0] + " | " + t[1] + " | " + t[2] + " |");
        Console.WriteLine("| 決着T | " + (settle[0] / battles).ToString("F2") + " | "
                          + (settle[1] / battles).ToString("F2") + " | "
                          + (settle[2] / battles).ToString("F2") + " |");
        Console.WriteLine();
        Console.WriteLine("- 指示書どおりの線（3版で一致）: **"
                          + (t[0] == t[1] && t[0] == t[2] ? "○" : "×") + "**（線は動かさずに記録する・第64期）");
        Console.WriteLine("- `order` の要素数が変わった回数（`HasteCountMismatch`）: **" + mismatch
                          + "**（0 なら ○ ＝ 延べ手番数はターンの中では変わっていない）");
        Console.WriteLine();

        Console.WriteLine("## (e) 既定では前倒しが1回も走らない");
        Console.WriteLine();
        long moves = 0;
        foreach (var (_, f) in CompareBuilds())
            for (int st = 0; st < 5; st++)
                for (int seed = 0; seed < 20; seed++)
                    moves += BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).HasteMoves;
        Console.WriteLine("- 既定の `HasteMoves` 総和: **" + moves + "**（0 なら ○）");
        Console.WriteLine();
    }

    // =================================================================================
    // 器具
    // =================================================================================

    readonly record struct Res(double[] Win, double Moves, long Turns, double Settle);

    static Res Measure(Formation f, HasteRule r)
    {
        var w = new double[5];
        long turns = 0;
        double moves = 0, settle = 0;
        int battles = 0, battles25 = 0;
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, haste: r);
                if (res.PlayerWon) wins++;
                turns += res.TurnLoopCalls;
                moves += res.HasteMoves;
                battles++;
                if (st >= 1) { settle += res.Turns; battles25++; }
            }
            w[st] = 100.0 * wins / Seeds;
        }
        return new Res(w, moves / battles, turns, settle / battles25);
    }

    static double Mean25(Res r) => r.Win.Skip(1).Average();
    static double Mean25(IEnumerable<Res> rs) => rs.Average(Mean25);
    static int Info(IEnumerable<Res> rs) => rs.Sum(r => r.Win.Skip(1).Count(x => x > 0 && x < 100));
    static string F1(double v) => v.ToString("F1");
    static string Sg(double v) => v.ToString("+0.0;-0.0;0.0");

    static int Count(string s, string needle)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    static string ReadSrc(string rel) => File.Exists(rel) ? File.ReadAllText(rel) : "";
}
