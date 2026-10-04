using BattleCore;
using static Common;

// =====================================================================================
// relic p0r ／ rejudge ／ check272 —— 第272期「レリックの再判定（分母の置き直し・火付けの矢の弾数制）」。
// 指示書は design/PHASE272_RELIC_REJUDGE_SPEC.md ／ 報告は design/PHASE272_RELIC_REJUDGE.md。
//
//     dotnet run --project BattleSim -c Release 0 relic p0r       # Phase 0: 帯A（seed 200..399）で素が負けるセル（本編第2〜5波 × 61 行）の数
//     dotnet run --project BattleSim -c Release 0 relic rejudge   # 本判定: 版 G0 ／ G1（弾数 3）／ G2（弾数 5）× 61 行 × 札7枚 × 枠5 × 本編第2〜5波・四つの物差し・弾数制の効き・ボスの付録（G0）
//     dotnet run --project BattleSim -c Release 0 relic check272  # 弾数制の自己検査
//
// 帯A ＝ seed 200..399（選抜と主の値）／ 帯B ＝ seed 400..599（追試）。**第271期の帯 0..199 は使わない**（別標本での再現を見る）。
// 判定の定義（事前に固定・報告書 §1.4 と同じ）:
//   固有の勝者の「件」＝ 帯A で素が負けるセル（行 × 波・勝率 < 100%）× 札 r で、r の最良の枠（帯A）が
//                       帯A・帯B の両方で素に対し片側フィッシャー p < 0.05。行の数は件を持つ行の重複なし
//   混成の成立の「件」＝ G1 の固有の勝者の件のうち、札が持ち込む軸（火付けの矢 → 燃焼 ／ 毒の足跡 → 毒 ／ 弾性 → 移動）が
//                       行の軸タグ（第264期の核リスト）に無いもの
// =====================================================================================
static partial class RelicDiag
{
    const int BandA = 200, BandB = 400;
    static readonly int[] MainWaves = { 1, 2, 3, 4 };

    sealed record Ver272(string Name, string What, Func<TraitId, TraitId>? Map);
    static readonly Ver272[] Vers272 =
    {
        new("G0", "第270期の札のまま（対照）", null),
        new("G1", $"火付けの矢に弾数制 N={RelicFireArrow3Trait.Shots}", r => r == TraitId.RelicFireArrow ? TraitId.RelicFireArrow3 : r),
        new("G2", $"火付けの矢に弾数制 N={RelicFireArrow5Trait.Shots}", r => r == TraitId.RelicFireArrow ? TraitId.RelicFireArrow5 : r),
    };

    /// <summary>札が持ち込む軸の資源（混成の成立）。軋む足は移動を「読む」札で持ち込まない・感電を持ち込む札は無い。</summary>
    static string? BridgeAxis(TraitId r) => r switch
    {
        TraitId.RelicFireArrow => "燃焼",
        TraitId.RelicVenomStep => "毒",
        TraitId.Spring => "移動",
        _ => null,
    };

    sealed record Hit(int Row, int Wave, TraitId Relic, GVar V, Cell A0, Cell A1, Cell B0, Cell B1);

    static List<Hit> UniqueWinners(GridData g, Func<TraitId, TraitId>? map)
    {
        var rows = g.Rows;
        var cand = new List<(int I, int W, TraitId R, GVar V)>();
        for (int i = 0; i < rows.Length; i++)
            foreach (int w in MainWaves)
            {
                Cell a0 = g.C[i][0][w];
                if (a0.Wins >= GridSeeds) continue;
                foreach (var r in RelicCatalog.Initial)
                {
                    GVar? best = null;
                    foreach (var v in g.Vars[i].Where(v => v.Relic == r.Id))
                        if (best is null || Cell.Better(g.C[i][v.Ix][w], g.C[i][best.Ix][w]) > 0) best = v;
                    if (best is null) continue;
                    if (FisherGreater(a0.Wins, a0.N, g.C[i][best.Ix][w].Wins, g.C[i][best.Ix][w].N) < 0.05) cand.Add((i, w, r.Id, best));
                }
            }
        var hits = new List<Hit>();
        Parallel.ForEach(cand, c =>
        {
            var b0 = MeasureCell(rows[c.I].F, c.W, BandB, GridSeeds);
            var b1 = MeasureCell(Apply(rows[c.I].F, c.V, map), c.W, BandB, GridSeeds);
            if (FisherGreater(b0.Wins, b0.N, b1.Wins, b1.N) < 0.05)
                lock (hits) hits.Add(new Hit(c.I, c.W, c.R, c.V, g.C[c.I][0][c.W], g.C[c.I][c.V.Ix][c.W], b0, b1));
        });
        hits.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Wave != b.Wave ? a.Wave.CompareTo(b.Wave) : a.Relic.CompareTo(b.Relic));
        return hits;
    }

    static string RName(TraitId r) => RelicCatalog.Info(r).Name;

    // ---------------------------------------------------------------------------------
    // p0r
    // ---------------------------------------------------------------------------------
    static void P0Rejudge()
    {
        var rows = CompareBuilds();
        Console.WriteLine($"# relic p0r —— 第272期 Phase 0（帯A seed {BandA}..{BandA + GridSeeds - 1}・素の版だけ）");
        Console.WriteLine();
        var lose = new int[rows.Length, WaveNames.Length];
        Parallel.For(0, rows.Length, i => { foreach (int w in MainWaves) lose[i, w] = MeasureCell(rows[i].F, w, BandA, GridSeeds).Wins; });
        Console.WriteLine("| 波 | 素が負けるセル（勝率 < 100%） | うち 0% | うち 95% 以上 |");
        Console.WriteLine("|---|--:|--:|--:|");
        int tot = 0;
        foreach (int w in MainWaves)
        {
            var ws = Enumerable.Range(0, rows.Length).Select(i => lose[i, w]).ToList();
            int n = ws.Count(x => x < GridSeeds); tot += n;
            Console.WriteLine($"| {WaveNames[w]} | {n} | {ws.Count(x => x == 0)} | {ws.Count(x => x < GridSeeds && x >= GridSeeds * 95 / 100)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**固有の勝者の分母: 素が負けるセル {tot}**（61 行 × 4 波 ＝ 244 セルのうち）・行で数えると {Enumerable.Range(0, rows.Length).Count(i => MainWaves.Any(w => lose[i, w] < GridSeeds))} 行");
        Console.WriteLine();
        Console.WriteLine("素が負けるセルの行（軸タグ・波ごとの勝率）:");
        Console.WriteLine();
        Console.WriteLine("| 行 | 軸 | " + string.Join(" | ", MainWaves.Select(w => WaveNames[w])) + " |");
        Console.WriteLine("|---|---|" + string.Concat(MainWaves.Select(_ => "--:|")));
        for (int i = 0; i < rows.Length; i++)
            if (MainWaves.Any(w => lose[i, w] < GridSeeds))
                Console.WriteLine($"| {rows[i].Name} | {AxisTag(rows[i].F)} | " + string.Join(" | ", MainWaves.Select(w => $"{100.0 * lose[i, w] / GridSeeds:F1}"))+ " |");
    }

    // ---------------------------------------------------------------------------------
    // check272
    // ---------------------------------------------------------------------------------
    static void Check272()
    {
        int pass = 0, fail = 0;
        void Ok(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            Console.WriteLine($"- [{(ok ? "○" : "×")}] {name}{(detail.Length > 0 ? $"（{detail}）" : "")}");
        }
        Console.WriteLine("# relic check272 —— 第272期 弾数制の自己検査");
        Console.WriteLine();
        Ok("(a) 版の札は `Versions` にあり `All` には無い（第270・271期の器具の7枚は不変）",
            RelicCatalog.Initial.Count == 7 && RelicCatalog.Versions.Count == 2 && RelicCatalog.Initial.All(r => !RelicCatalog.Versions.Contains(r))
            && RelicCatalog.IsRelic(TraitId.RelicFireArrow3) && RelicCatalog.IsRelic(TraitId.RelicFireArrow5));
        Ok("(b) 版の札の素の保持者 0 枚", UnitCatalog.Everyone.Count(u => u.Traits.Contains(TraitId.RelicFireArrow3) || u.Traits.Contains(TraitId.RelicFireArrow5)) == 0);
        {
            // (c) 着火の回数が上限を超えない・(d) 上限に届かない戦は G0 と台本一致・(e) 上限に届いた戦がある（弾数制が実際に効いている）
            var rows = CompareBuilds();
            int over = 0, same = 0, diffUnder = 0, capped = 0, n = 0;
            foreach (var (name, f0) in rows.Take(20))
                for (int s = 0; s < 5; s++)
                    foreach (int w in MainWaves)
                    {
                        int fr = SlotBy(f0, "攻");
                        string holder = f0[fr]!.Name;
                        var r0 = BattleEngine.Run(f0.WithRelic(fr, TraitId.RelicFireArrow), EnemyCatalog.Stages[w].Enemy, s, verbose: true);
                        var r1 = BattleEngine.Run(f0.WithRelic(fr, TraitId.RelicFireArrow3), EnemyCatalog.Stages[w].Enemy, s, verbose: true);
                        int s0 = r0.Log.Count(l => l.Text.Contains($"{holder} の火付けの矢が"));
                        int s1 = r1.Log.Count(l => l.Text.Contains($"{holder} の火付けの矢が"));
                        n++;
                        if (s1 > RelicFireArrow3Trait.Shots) over++;
                        if (s0 <= RelicFireArrow3Trait.Shots)
                        {
                            bool eq = r0.PlayerWon == r1.PlayerWon && r0.Turns == r1.Turns
                                && r0.Log.Select(l => Strip(l.Text)).SequenceEqual(r1.Log.Select(l => Strip(l.Text)));
                            if (eq) same++; else diffUnder++;
                        }
                        else capped++;
                    }
            Ok("(c) G1 の着火は1戦 3 回を超えない", over == 0, $"{n} 戦・超えた戦 {over}");
            Ok("(d) G0 の着火が 3 回以下の戦は、G1 と台本一致（「残り n」の表記を除く）", diffUnder == 0, $"一致 {same}・不一致 {diffUnder}");
            Ok("(e) G0 で 4 回以上着火する戦がある（弾数制が効く場面が存在する）", capped > 0, $"{capped} 戦");
        }
        {
            // (f) 会戦の境界で弾数が戻る（同じ駒で2戦して、2戦目にも着火する）
            var f = Formation.Build(front1: UnitCatalog.Dolga).WithRelic(0, TraitId.RelicFireArrow3);   // 殴る駒（ガルドは殴らないので 0 発になる）
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            Func<List<UnitState>> weak = () => BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gald, center: UnitCatalog.Gald), BattleContext.EnemyTeam, EnemyScaleRule.None);
            var r1 = BattleEngine.Run(p, weak(), 0, verbose: true);
            int used1 = p[0].RawCounter(RelicFireArrowCappedTrait.ShotsKey);
            foreach (var u in p) foreach (var t in u.Traits) t.OnCarryOver(u);
            int after = p[0].RawCounter(RelicFireArrowCappedTrait.ShotsKey);
            Ok("(f) `OnCarryOver` で弾数が 0 に戻る（会戦の次の戦にまた3発）", used1 > 0 && after == 0, $"1戦目 {used1} 発 → 境界の後 {after}");
        }
        Console.WriteLine();
        Console.WriteLine($"合計 {pass + fail} 項目: ○ {pass} ／ × {fail}");

        static string Strip(string t) => System.Text.RegularExpressions.Regex.Replace(t, "（残り \\d+）", "");
    }

    // ---------------------------------------------------------------------------------
    // rejudge
    // ---------------------------------------------------------------------------------
    static void Rejudge()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        Console.WriteLine($"# relic rejudge —— 第272期 再判定（版 G0 ／ G1 ／ G2 × 61 行 × 札7枚 × 枠5・部隊に1枚 × 本編第2〜5波・帯A seed {BandA}..{BandA + GridSeeds - 1}・帯B {BandB}..{BandB + GridSeeds - 1}）");
        Console.WriteLine();
        foreach (var v in Vers272) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();

        var grids = new Dictionary<string, GridData>();
        var hits = new Dictionary<string, List<Hit>>();
        foreach (var v in Vers272)
        {
            grids[v.Name] = RunGrid(BandA, MainWaves, v.Map);
            hits[v.Name] = UniqueWinners(grids[v.Name], v.Map);
        }
        Console.WriteLine($"総当たり ＋ 追試 {sw.Elapsed.TotalSeconds:F0} 秒。");
        Console.WriteLine();
        var g0 = grids["G0"];
        int losing = Enumerable.Range(0, rows.Length).Sum(i => MainWaves.Count(w => g0.C[i][0][w].Wins < GridSeeds));
        Console.WriteLine($"固有の勝者の分母: 帯A で素が負けるセル **{losing}** ／ 244。");
        Console.WriteLine();

        // ---- 1. 固有の勝者 ----
        Console.WriteLine("## 1. 固有の勝者（件 ＝ 素が負けるセル × 札・最良の枠が帯A・帯B とも片側フィッシャー p < 0.05）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 件 | 行 | 火付けの矢以外の札の件 | 火付けの矢以外の札の行 | 札ごとの件 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        foreach (var v in Vers272)
        {
            var h = hits[v.Name];
            var nf = h.Where(x => x.Relic != TraitId.RelicFireArrow).ToList();
            Console.WriteLine($"| {v.Name} | {h.Count} | {h.Select(x => x.Row).Distinct().Count()} | {nf.Count} | {nf.Select(x => x.Row).Distinct().Count()} | "
                + string.Join("・", RelicCatalog.Initial.Select(r => $"{r.Name} {h.Count(x => x.Relic == r.Id)}")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("### G1 の件（本判定）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 軸 | 波 | 札 → 枠 | 帯A 素 → 札 | 帯B 素 → 札 | 持ち込む軸 | 混成 |");
        Console.WriteLine("|---|---|---|---|--:|--:|---|:-:|");
        var g1 = hits["G1"];
        int mixedHits = 0;
        foreach (var h in g1)
        {
            string? ax = BridgeAxis(h.Relic);
            bool mix = ax is not null && !AxesOf(rows[h.Row].F).Contains(ax);
            if (mix) mixedHits++;
            string label = h.Relic == TraitId.RelicFireArrow ? RName(TraitId.RelicFireArrow3) : RName(h.Relic);
            Console.WriteLine($"| {rows[h.Row].Name} | {AxisTag(rows[h.Row].F)} | {WaveNames[h.Wave]} | {label} → {rows[h.Row].F[h.V.Frame]!.Name} | {h.A0.Win:F1} → {h.A1.Win:F1} | {h.B0.Win:F1} → {h.B1.Win:F1} | {ax ?? "—"} | {(mix ? "○" : "")} |");
        }
        Console.WriteLine();
        int g1Rows = g1.Select(x => x.Row).Distinct().Count();
        int g1NonFireRows = g1.Where(x => x.Relic != TraitId.RelicFireArrow).Select(x => x.Row).Distinct().Count();
        Console.WriteLine($"**固有の勝者（G1）: {g1Rows} 行**（下限 ≧ 3）・**火付けの矢以外の札: {g1NonFireRows} 行**（下限 ≧ 1）");
        Console.WriteLine();
        Console.WriteLine($"## 2. 混成の成立（G1 の件のうち、札が行に無い軸の資源を持ち込んだもの）: **{mixedHits} 件**（下限 ≧ 2）");
        Console.WriteLine();
        foreach (var v in Vers272.Where(v => v.Name != "G1"))
        {
            int m = hits[v.Name].Count(h => BridgeAxis(h.Relic) is string ax && !AxesOf(rows[h.Row].F).Contains(ax));
            Console.WriteLine($"- 参考 {v.Name}: {m} 件");
        }
        Console.WriteLine();

        // ---- 3. 個性の保存 ----
        Console.WriteLine("## 3. 個性の保存（G1 の固有の勝者の全件・帯A・20 ターンまで）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 駒 | 与ダメ 素 → 札 | 回復 素 → 札 | 状態の付与 素 → 札 | 受けたダメ 素 → 札 | 主の指標 | 判定 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|:-:|");
        var keepLines = new string[g1.Count];
        var g1Map = Vers272[1].Map;
        Parallel.For(0, g1.Count, k =>
        {
            var h = g1[k];
            keepLines[k] = KeepLine($"{rows[h.Row].Name}（{WaveNames[h.Wave]}）", rows[h.Row].F, h.V, h.Wave, BandA, g1Map)
                .Replace(RName(TraitId.RelicFireArrow) + "→", RName(TraitId.RelicFireArrow3) + "→");
        });
        foreach (var l in keepLines) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"**保たれた件: {keepLines.Count(l => l.EndsWith("| ○ |"))} ／ {keepLines.Length}**");
        Console.WriteLine();

        // ---- 4. ゴミの成立の再現（G0・帯A） ----
        Console.WriteLine("## 4. ゴミの成立の再現（ウツ × 萎える心・帯A・本編第2〜5波）");
        Console.WriteLine();
        {
            int strong = 0, cells = 0, self = 0, cs = 0, cc = 0;
            for (int i = 0; i < rows.Length; i++)
                foreach (var v in g0.Vars[i].Where(v => v.Relic == TraitId.RelicWilt))
                    foreach (int w in MainWaves)
                    {
                        Cell a = g0.C[i][0][w], b = g0.C[i][v.Ix][w];
                        bool st = b.Win - a.Win >= 1 || (a.Wins == b.Wins && a.Wins > 0 && b.T <= a.T - 0.1);
                        if (HasUnit(rows[i].F, "utsu")) { cells++; if (st) { strong++; if (rows[i].F[v.Frame]!.Id == "utsu") self++; } }
                        else { cc++; if (st) cs++; }
                    }
            double rr = 100.0 * strong / cells, rc = 100.0 * cs / Math.Max(1, cc);
            bool rep = rr > 5 * rc && self * 3 >= strong * 2;
            Console.WriteLine($"- 読み手の行で付けた方が強いセル: **{strong} ／ {cells}（{rr:F1}%）**・うちウツ本人に付けたセル **{self}**");
            Console.WriteLine($"- 対照（ウツのいない行）: {cs} ／ {cc}（{rc:F1}%）");
            Console.WriteLine($"- 再現の線（事前に固定）: 読み手の行の割合が対照の 5 倍を超え、かつ強いセルの 2/3 以上がウツ本人 → **{(rep ? "再現した" : "再現しなかった")}**");
            Console.WriteLine();
            Console.WriteLine("| 行 | " + string.Join(" | ", MainWaves.Select(w => WaveNames[w])) + " |");
            Console.WriteLine("|---|" + string.Concat(MainWaves.Select(_ => "--:|")));
            for (int i = 0; i < rows.Length; i++)
            {
                if (!HasUnit(rows[i].F, "utsu")) continue;
                var v = g0.Vars[i].First(v => v.Relic == TraitId.RelicWilt && rows[i].F[v.Frame]!.Id == "utsu");
                Console.WriteLine($"| {rows[i].Name}〔萎える心→ウツ〕 | " + string.Join(" | ", MainWaves.Select(w =>
                {
                    Cell a = g0.C[i][0][w], b = g0.C[i][v.Ix][w];
                    return $"{b.Win - a.Win:+0.0;−0.0;±0.0} ／ {(a.Wins > 0 && b.Wins > 0 ? (b.T - a.T).ToString("+0.0;−0.0;±0.0") : "—")}";
                })) + " |");
            }
            Console.WriteLine();
        }

        // ---- 5. 弾数制の効き ----
        Console.WriteLine("## 5. 弾数制の効き（火付けの矢のセル・帯A）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 最大の上げ（行・波・枠） | 素 < 100% のセルで +5pt 以上の版の数 | 固有の勝者の件（火付けの矢） |");
        Console.WriteLine("|---|---|--:|--:|");
        foreach (var v in Vers272)
        {
            var g = grids[v.Name];
            (double D, int I, int W, GVar V) up = (double.MinValue, 0, 0, g.Vars[0][0]);
            int lift = 0;
            for (int i = 0; i < rows.Length; i++)
                foreach (var gv in g.Vars[i].Where(x => x.Relic == TraitId.RelicFireArrow))
                    foreach (int w in MainWaves)
                    {
                        double d = g.C[i][gv.Ix][w].Win - g.C[i][0][w].Win;
                        if (d > up.D) up = (d, i, w, gv);
                        if (g.C[i][0][w].Wins < GridSeeds && d >= 5) lift++;
                    }
            Console.WriteLine($"| {v.Name} | {up.D:+0.0;−0.0;±0.0}pt（{rows[up.I].Name}・{WaveNames[up.W]}・→{rows[up.I].F[up.V.Frame]!.Name}） | {lift} | {hits[v.Name].Count(h => h.Relic == TraitId.RelicFireArrow)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 燃焼の核を持つ行（ボルグ・ホタ・ヒヨ入り）で G0 と弾数制の差（火付けの矢の全セル・行 × 波 × 枠）");
        Console.WriteLine();
        Console.WriteLine("| 比較 | セル数 | |差| の最大 | |差| の平均 | |差| > 5pt のセル |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var (a, b) in new[] { ("G0", "G1"), ("G0", "G2") })
        {
            var ds = new List<double>();
            for (int i = 0; i < rows.Length; i++)
            {
                if (!AxesOf(rows[i].F).Contains("燃焼")) continue;
                foreach (var gv in g0.Vars[i].Where(x => x.Relic == TraitId.RelicFireArrow))
                    foreach (int w in MainWaves) ds.Add(Math.Abs(grids[a].C[i][gv.Ix][w].Win - grids[b].C[i][gv.Ix][w].Win));
            }
            Console.WriteLine($"| {a} − {b} | {ds.Count} | {ds.Max():F1} | {ds.Average():F2} | {ds.Count(x => x > 5)} |");
        }
        Console.WriteLine();
        Console.WriteLine("参考: 燃焼の核を持たない行の同じ比較");
        Console.WriteLine();
        Console.WriteLine("| 比較 | セル数 | |差| の最大 | |差| の平均 | |差| > 5pt のセル |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var (a, b) in new[] { ("G0", "G1"), ("G0", "G2") })
        {
            var ds = new List<double>();
            for (int i = 0; i < rows.Length; i++)
            {
                if (AxesOf(rows[i].F).Contains("燃焼")) continue;
                foreach (var gv in g0.Vars[i].Where(x => x.Relic == TraitId.RelicFireArrow))
                    foreach (int w in MainWaves) ds.Add(Math.Abs(grids[a].C[i][gv.Ix][w].Win - grids[b].C[i][gv.Ix][w].Win));
            }
            Console.WriteLine($"| {a} − {b} | {ds.Count} | {ds.Max():F1} | {ds.Average():F2} | {ds.Count(x => x > 5)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 固有の勝者の件の推移（G0 → G1 → G2）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 札 | G0 | G1 | G2 |");
        Console.WriteLine("|---|---|---|:-:|:-:|:-:|");
        var keys = hits.Values.SelectMany(h => h).Select(h => (h.Row, h.Wave, h.Relic)).Distinct().OrderBy(k => k.Row).ThenBy(k => k.Wave).ThenBy(k => k.Relic);
        foreach (var k in keys)
            Console.WriteLine($"| {rows[k.Row].Name} | {WaveNames[k.Wave]} | {RName(k.Relic)} | " + string.Join(" | ", Vers272.Select(v => hits[v.Name].Any(h => (h.Row, h.Wave, h.Relic) == k) ? "○" : "")) + " |");
        Console.WriteLine();

        // ---- 6. ボスの付録（G0・採否に使わない） ----
        Console.WriteLine("## 6. 付録: ボス（G0・帯A・採否に使わない）——札1枚で 0% の壁が割れないこと");
        Console.WriteLine();
        {
            var gb = RunGrid(BandA, new[] { 0 }, null);
            var zero = Enumerable.Range(0, rows.Length).Where(i => gb.C[i][0][0].Wins == 0).ToList();
            int reach = zero.Count(i => gb.Vars[i].Any(v => v.Relic is not null && gb.C[i][v.Ix][0].Wins >= MinWinsA));
            int maxWins = zero.Count == 0 ? 0 : zero.Max(i => gb.Vars[i].Where(v => v.Relic is not null).Max(v => gb.C[i][v.Ix][0].Wins));
            var broken = new List<string>();
            foreach (int i in zero)
            {
                var (bv, bc) = BestVar(gb, i, 0, relicOnly: true);
                if (bc.Wins < MinWinsA) continue;
                var b0 = MeasureCell(rows[i].F, 0, BandB, GridSeeds);
                var b1 = MeasureCell(Apply(rows[i].F, bv), 0, BandB, GridSeeds);
                if (FisherGreater(b0.Wins, b0.N, b1.Wins, b1.N) < 0.05) broken.Add($"{rows[i].Name}〔{VarName(rows[i].F, bv)}〕{bc.Win:F1}%");
            }
            Console.WriteLine($"- ボスで素 0% の行: **{zero.Count}** ／ 61・札1枚で帯A 5 勝以上に届いた行: **{reach}**（最良の版の最多 {maxWins} 勝）・割れた行（帯B でも有意）: **{broken.Count}**{(broken.Count > 0 ? "——" + string.Join("・", broken) : "")}");
            Console.WriteLine($"- **{(broken.Count == 0 ? "割れなかった（設計どおり）" : "割れた")}**");
        }
        Console.WriteLine();

        // ---- 判定 ----
        Console.WriteLine("## 判定（事前に固定した下限）");
        Console.WriteLine();
        Console.WriteLine($"- 固有の勝者（G1）: {g1Rows} 行（≧ 3）・火付けの矢以外 {g1NonFireRows} 行（≧ 1）→ **{(g1Rows >= 3 && g1NonFireRows >= 1 ? "○" : "×")}**");
        Console.WriteLine($"- 混成の成立（G1）: {mixedHits} 件（≧ 2）→ **{(mixedHits >= 2 ? "○" : "×")}**");
        Console.WriteLine($"- 個性の保存（G1）: {keepLines.Count(l => l.EndsWith("| ○ |"))} ／ {keepLines.Length}（全例）→ **{(keepLines.Length > 0 && keepLines.All(l => l.EndsWith("| ○ |")) ? "○" : keepLines.Length == 0 ? "該当なし" : "×")}**");
        int g1FireRows = hits["G1"].Where(h => h.Relic == TraitId.RelicFireArrow).Select(h => h.Row).Distinct().Count();
        Console.WriteLine($"- 弾数の選択（指示書 §2.2）: G1 の固有の勝者 {g1Rows} 行（うち火付けの矢 {g1FireRows} 行）・G2 {hits["G2"].Select(h => h.Row).Distinct().Count()} 行 → 「保てるなら N=3」");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
