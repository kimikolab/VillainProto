using BattleCore;
using static Common;

// =====================================================================================
// relic p0x ／ expand ／ check273 —— 第273期「レリック採用の正式化と札の増補（橋の多様化）」。
// 指示書は design/PHASE273_RELIC_ADOPT_EXPAND_SPEC.md ／ 報告は design/PHASE273_RELIC_EXPAND.md ／ 物差しは design/RELIC_MEASURES.md。
//
//     dotnet run --project BattleSim -c Release 0 relic p0x       # Phase 0: 新札5枚の読み手の在籍（`compare` 61 行・戦闘0回）
//     dotnet run --project BattleSim -c Release 0 relic check273  # 新札5枚の自己検査
//     dotnet run --project BattleSim -c Release 0 relic logrow <行名> <札> <枠0-4> <波1-4> [seed]   # `compare` の行に札を付けた1戦のログ
//     dotnet run --project BattleSim -c Release 0 relic expand    # 本判定: 61 行 × 札12枚 × 枠5 × 本編第2〜5波・帯A seed 600..799 ／ 帯B 800..999・確定版の物差し
// =====================================================================================
static partial class RelicDiag
{
    const int BandA273 = 600, BandB273 = 800;

    /// <summary>札が持ち込む軸（混成の判定・`RELIC_MEASURES.md` §2.2 の表）。</summary>
    static string? CarryAxis(TraitId r) => r switch
    {
        TraitId.RelicFireArrow => "燃焼",
        TraitId.RelicVenomStep => "毒",
        TraitId.Spring => "移動",
        TraitId.RelicShockStep => "雷",
        TraitId.RelicVenomShove => "移動",
        TraitId.RelicMomentum => "移動",
        _ => null,
    };

    /// <summary>札が橋渡しする資源（第273期の「毒・感電・回復の持ち込み」の数え方・事前に固定）。燃焼・移動以外の資源に触れる札だけ。</summary>
    static string? BridgeResource(TraitId r) => r switch
    {
        TraitId.RelicVenomStep => "毒",
        TraitId.RelicVenomShove => "毒",
        TraitId.RelicShockStep => "感電",
        TraitId.RelicOverflowEdge => "回復",
        _ => null,
    };

    static readonly (TraitId Card, string Reader, string[] Ids)[] Readers273 =
    {
        (TraitId.RelicShockStep, "感電の核（シガ・ツギ・カタ）", new[] { "shiga", "tsugi", "kata" }),
        (TraitId.RelicVenomShove, "毒の書き手（グザ・ミオ・ラウ・スィド・ベニ・ヴィオ）", new[] { "guza", "mio", "rau", "sid", "beni", "vio" }),
        (TraitId.RelicOverflowEdge, "味方を癒す駒（ノノ・シオ・ベニ・ヴェル・リリ・ヒヨ・ハリ・ナラ・ゴルム）", new[] { "nono", "shio", "beni", "vel", "lili", "hiyo", "hari", "nara", "golm" }),
        (TraitId.RelicMomentum, "移動の読み手（バサ・ヨミ・シオ・ハネ・セロ）", new[] { "basa", "yomi", "shio", "hane", "sero" }),
        (TraitId.RelicPoisonMagnet, "ベニ・ヴィオ", new[] { "beni", "vio" }),
    };

    static string FireMark(TraitId id, string holder) => id switch
    {
        TraitId.RelicShockStep => $"{holder} の帯電の足が",
        TraitId.RelicVenomShove => $"{holder} の押し毒が",
        TraitId.RelicOverflowEdge => $"{holder} の溢れの刃（",
        TraitId.RelicMomentum => $"{holder} は勢い余って",
        _ => "\u0000",
    };

    // ---------------------------------------------------------------------------------
    // p0x
    // ---------------------------------------------------------------------------------
    static void P0Expand()
    {
        var rows = CompareBuilds();
        Console.WriteLine("# relic p0x —— 第273期 Phase 0: 新札5枚の読み手の在籍（`compare` 61 行・戦闘0回）");
        Console.WriteLine();
        Console.WriteLine("| 札 | 読み手 | 行数 | 行 |");
        Console.WriteLine("|---|---|--:|---|");
        foreach (var (card, reader, ids) in Readers273)
        {
            var hit = rows.Where(r => r.F.Occupied().Any(o => ids.Contains(o.Def.Id))).Select(r => r.Name).ToList();
            Console.WriteLine($"| {RName(card)} | {reader} | {hit.Count} | {string.Join("・", hit)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"- カタを含む行: {rows.Count(r => HasUnit(r.F, "kata"))}（帯電の足の自己感電の供給は、敵の感電 ／ シガの通電 ／ カタの漏れ）");
        Console.WriteLine($"- ノノを含む行: {rows.Count(r => HasUnit(r.F, "nono"))}");
    }

    // ---------------------------------------------------------------------------------
    // check273
    // ---------------------------------------------------------------------------------
    static void Check273()
    {
        int pass = 0, fail = 0;
        void Ok(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            Console.WriteLine($"- [{(ok ? "○" : "×")}] {name}{(detail.Length > 0 ? $"（{detail}）" : "")}");
        }
        Console.WriteLine("# relic check273 —— 第273期 新札5枚の自己検査");
        Console.WriteLine();
        Ok("(a) `All` ＝ `Initial`（第270期の7枚・並び不変）＋ `Added273`（5枚）",
            RelicCatalog.Initial.Count == 7 && RelicCatalog.Added273.Count == 5 && RelicCatalog.All.Count == 12
            && RelicCatalog.All.Take(7).SequenceEqual(RelicCatalog.Initial)
            && RelicCatalog.Initial.Select(r => r.Id).SequenceEqual(new[] { TraitId.RelicCreak, TraitId.RelicFireArrow, TraitId.RelicVenomStep, TraitId.Spring, TraitId.RelicWilt, TraitId.RelicNumbStep, TraitId.RelicHarden }));
        var newIds = RelicCatalog.Added273.Select(r => r.Id).ToHashSet();
        Ok("(b) 新札5枚の素の保持者 0 枚", UnitCatalog.Everyone.Count(u => u.Traits.Any(newIds.Contains)) == 0);

        // (c) 発火 ／ (d) 発火しなかった戦は素と台本一致（乱数を引かない・盤面に触らない）
        var rows = CompareBuilds();
        string poisonLabel = StatusKeys.Poison;   // StatusGain の Text はキー名（表示名ではない）
        foreach (var r in RelicCatalog.Added273)
        {
            long fires = 0; int quiet = 0, quietSame = 0, n = 0;
            var lk = new object();
            Parallel.For(0, rows.Length, i =>
            {
                var f0 = rows[i].F;
                for (int fr = 0; fr < FormationRules.PlayableSlotCount; fr++)
                {
                    if (f0[fr] is null) continue;
                    var f = f0.WithRelic(fr, r.Id);
                    int holderIx = f0.Occupied().TakeWhile(o => o.Slot != fr).Count();
                    foreach (int w in MainWaves)
                        for (int s = 0; s < 2; s++)
                        {
                            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var res = BattleEngine.Run(p, BattleEngine.Materialize(EnemyCatalog.Stages[w].Enemy, BattleContext.EnemyTeam), s, verbose: true);
                            var h = p[holderIx];
                            long fr1 = r.Id == TraitId.RelicPoisonMagnet
                                ? res.Events.Count(x => x.Kind == BattleEventKind.StatusGain && x.TargetId == h.InstanceId && x.Text == poisonLabel)
                                : res.Log.Count(l => l.Text.Contains(FireMark(r.Id, h.Def.Name)));
                            bool same = false;
                            if (fr1 == 0)
                            {
                                var b = BattleEngine.Run(f0, EnemyCatalog.Stages[w].Enemy, s, verbose: true);
                                same = b.PlayerWon == res.PlayerWon && b.Turns == res.Turns && b.Log.Select(l => l.Text).SequenceEqual(res.Log.Select(l => l.Text));
                            }
                            lock (lk) { n++; fires += fr1; if (fr1 == 0) { quiet++; if (same) quietSame++; } }
                        }
                }
            });
            Ok($"(c) {r.Name}: 発火する（61 行 × 枠5 × 本編4波 × seed 0..1）", fires > 0, $"{n} 戦で {fires} 回");
            Ok($"(d) {r.Name}: 発火しなかった戦は素と台本一致", quiet == quietSame, $"発火 0 の戦 {quiet}・一致 {quietSame}");
        }

        // (e) 毒を招く: 最初に受けた毒の層が素の2倍
        {
            int ok = 0, ng = 0;
            foreach (var (name, f0) in rows.Where(r => r.F.Occupied().Any(o => o.Def.Id is "guza" or "beni" or "rau" or "mio")))
                for (int fr = 0; fr < FormationRules.PlayableSlotCount; fr++)
                    foreach (int w in MainWaves)
                    {
                        int holderIx = f0.Occupied().TakeWhile(o => o.Slot != fr).Count();
                        int? First(Formation f)
                        {
                            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var res = BattleEngine.Run(p, BattleEngine.Materialize(EnemyCatalog.Stages[w].Enemy, BattleContext.EnemyTeam), 0, verbose: true);
                            return res.Events.FirstOrDefault(x => x.Kind == BattleEventKind.StatusGain && x.TargetId == p[holderIx].InstanceId && x.Text == poisonLabel && x.PoisonRoute is not null)?.Amount;
                        }
                        int? a = First(f0), b = First(f0.WithRelic(fr, TraitId.RelicPoisonMagnet));
                        if (a is int x && b is int y) { if (y == x * RelicPoisonMagnetTrait.Factor) ok++; else ng++; }
                    }
            Ok("(e) 毒を招く: 最初に `ctx.Poison` から受けた層が素の 2 倍（毒の書き手のいる行 × 枠 × 本編4波 × seed 0）", ok > 0 && ng == 0, $"一致 {ok}・不一致 {ng}");
        }
        // (f) 帯電の足: 感電の供給（カタの漏れ＝カタの隣の味方に感電）がある検証台では発火する（`compare` の行にカタは居ない）
        {
            var bench = Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Basa, center: UnitCatalog.Kata, back1: UnitCatalog.Sero, back3: UnitCatalog.Hane);
            long fires = 0, marks = 0;
            for (int fr = 0; fr < FormationRules.PlayableSlotCount; fr++)
                foreach (int w in MainWaves)
                    for (int s = 0; s < 10; s++)
                    {
                        var p = BattleEngine.Materialize(bench.WithRelic(fr, TraitId.RelicShockStep), BattleContext.PlayerTeam);
                        var res = BattleEngine.Run(p, BattleEngine.Materialize(EnemyCatalog.Stages[w].Enemy, BattleContext.EnemyTeam), s, verbose: true);
                        string holder = bench[fr]!.Name;
                        fires += res.Log.Count(l => l.Text.Contains($"{holder} の帯電の足が"));
                        marks += res.Log.Count(l => l.Text.Contains($"{holder} の帯電の足が") && l.Text.Contains("感電を移した"));
                    }
            Ok("(f) 帯電の足: カタのいる検証台（ヨミ・バサ・カタ・セロ・ハネ）では発火する（枠5 × 本編4波 × seed 0..9）", fires > 0, $"{fires} 回");
        }
        Console.WriteLine();
        Console.WriteLine($"合計 {pass + fail} 項目: ○ {pass} ／ × {fail}");
    }

    // ---------------------------------------------------------------------------------
    // expand
    // ---------------------------------------------------------------------------------
    static List<Hit> UniqueWinnersOf(GridData g, IReadOnlyList<RelicInfo> cards, int bandB)
    {
        var rows = g.Rows;
        var cand = new List<(int I, int W, TraitId R, GVar V)>();
        for (int i = 0; i < rows.Length; i++)
            foreach (int w in MainWaves)
            {
                Cell a0 = g.C[i][0][w];
                if (a0.Wins >= GridSeeds) continue;
                foreach (var r in cards)
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
            var b0 = MeasureCell(rows[c.I].F, c.W, bandB, GridSeeds);
            var b1 = MeasureCell(Apply(rows[c.I].F, c.V), c.W, bandB, GridSeeds);
            if (FisherGreater(b0.Wins, b0.N, b1.Wins, b1.N) < 0.05)
                lock (hits) hits.Add(new Hit(c.I, c.W, c.R, c.V, g.C[c.I][0][c.W], g.C[c.I][c.V.Ix][c.W], b0, b1));
        });
        hits.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Wave != b.Wave ? a.Wave.CompareTo(b.Wave) : a.Relic.CompareTo(b.Relic));
        return hits;
    }

    static void LogRow(string rowName, string relicName, int frame, int wave, int seed)
    {
        var f0 = CompareBuilds().First(r => r.Name.StartsWith(rowName)).F;
        var f = f0.WithRelic(frame, RelicByName(relicName).Id);
        var res = BattleEngine.Run(f, EnemyCatalog.Stages[wave].Enemy, seed, verbose: true);
        Console.WriteLine($"# {rowName}（{Seats(f)}）× {WaveNames[wave]} × seed {seed} → {(res.PlayerWon ? "勝ち" : "負け")} T{res.Turns}");
        foreach (var l in res.Log) Console.WriteLine(l.Text);
    }

    static bool StrongCell(Cell a, Cell b) => b.Win - a.Win >= 1 || (a.Wins == b.Wins && a.Wins > 0 && b.T <= a.T - 0.1);

    static void Expand()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        var cards = RelicCatalog.All;
        Console.WriteLine($"# relic expand —— 第273期 札の増補（61 行 × 札{cards.Count}枚 × 枠5・部隊に1枚 × 本編第2〜5波・帯A seed {BandA273}..{BandA273 + GridSeeds - 1}・帯B {BandB273}..{BandB273 + GridSeeds - 1}・物差しは design/RELIC_MEASURES.md）");
        Console.WriteLine();
        var g = RunGrid(BandA273, MainWaves, null, cards);
        var hits = UniqueWinnersOf(g, cards, BandB273);
        int losing = Enumerable.Range(0, rows.Length).Sum(i => MainWaves.Count(w => g.C[i][0][w].Wins < GridSeeds));
        Console.WriteLine($"総当たり ＋ 追試 {sw.Elapsed.TotalSeconds:F0} 秒。分母（帯A で素が負けるセル）: **{losing}** ／ 244。");
        Console.WriteLine();

        // ---- 1. 固有の勝者 ----
        Console.WriteLine("## 1. 固有の勝者（札ごと）");
        Console.WriteLine();
        Console.WriteLine("| 札 | 種 | 件 | 行 | 第272期 G0（帯 200..399）の件 |");
        Console.WriteLine("|---|---|--:|--:|---|");
        var prev = new Dictionary<TraitId, string>
        {
            [TraitId.RelicCreak] = "3", [TraitId.RelicFireArrow] = "53", [TraitId.RelicVenomStep] = "2", [TraitId.Spring] = "25",
            [TraitId.RelicWilt] = "3", [TraitId.RelicNumbStep] = "0", [TraitId.RelicHarden] = "31",
        };
        foreach (var r in cards)
        {
            var h = hits.Where(x => x.Relic == r.Id).ToList();
            Console.WriteLine($"| {r.Name} | {r.Kind} | {h.Count} | {h.Select(x => x.Row).Distinct().Count()} | {prev.GetValueOrDefault(r.Id, "（新札）")} |");
        }
        Console.WriteLine();
        int newCardsWithRow = RelicCatalog.Added273.Count(r => hits.Any(h => h.Relic == r.Id));
        Console.WriteLine($"全体: **{hits.Count} 件・{hits.Select(h => h.Row).Distinct().Count()} 行**。**新札で固有の勝者が1行以上ある札: {newCardsWithRow} ／ 5**（下限 ≧ 3）");
        Console.WriteLine();
        Console.WriteLine("### 新札の件");
        Console.WriteLine();
        Console.WriteLine("| 行 | 軸 | 波 | 札 → 枠 | 帯A 素 → 札 | 帯B 素 → 札 | 持ち込む軸 | 混成 | 橋の資源 |");
        Console.WriteLine("|---|---|---|---|--:|--:|---|:-:|---|");
        foreach (var h in hits.Where(h => RelicCatalog.Added273.Any(r => r.Id == h.Relic)))
        {
            string? ax = CarryAxis(h.Relic);
            bool mix = ax is not null && !AxesOf(rows[h.Row].F).Contains(ax);
            Console.WriteLine($"| {rows[h.Row].Name} | {AxisTag(rows[h.Row].F)} | {WaveNames[h.Wave]} | {RName(h.Relic)} → {rows[h.Row].F[h.V.Frame]!.Name} | {h.A0.Win:F1} → {h.A1.Win:F1} | {h.B0.Win:F1} → {h.B1.Win:F1} | {ax ?? "—"} | {(mix ? "○" : "")} | {BridgeResource(h.Relic) ?? "—"} |");
        }
        Console.WriteLine();

        // ---- 2. 混成の内訳 ----
        Console.WriteLine("## 2. 混成の内訳");
        Console.WriteLine();
        int bridged = hits.Count(h => BridgeResource(h.Relic) is not null);
        Console.WriteLine($"**毒・感電・回復を橋渡しした件（主・事前に固定）: {bridged}**（下限 ≧ 5）——" + string.Join("・", new[] { "毒", "感電", "回復" }.Select(k => $"{k} {hits.Count(h => BridgeResource(h.Relic) == k)}")));
        Console.WriteLine();
        Console.WriteLine("参考: 札が行に無い軸を持ち込んだ件（第272期の定義）の内訳:");
        Console.WriteLine();
        foreach (string ax in new[] { "燃焼", "移動", "毒", "雷" })
            Console.WriteLine($"- {ax}: {hits.Count(h => CarryAxis(h.Relic) == ax && !AxesOf(rows[h.Row].F).Contains(ax))} 件（札: {string.Join("・", hits.Where(h => CarryAxis(h.Relic) == ax && !AxesOf(rows[h.Row].F).Contains(ax)).GroupBy(h => RName(h.Relic)).Select(x => $"{x.Key} {x.Count()}"))}）");
        Console.WriteLine();

        // ---- 3. ゴミの成立 ----
        Console.WriteLine("## 3. ゴミの成立");
        Console.WriteLine();
        foreach (var (card, readerIds, readerName) in new[] { (TraitId.RelicPoisonMagnet, new[] { "beni" }, "ベニ"), (TraitId.RelicPoisonMagnet, new[] { "vio" }, "ヴィオ"), (TraitId.RelicWilt, new[] { "utsu" }, "ウツ") })
        {
            int strong = 0, cells = 0, cs = 0, cc = 0;
            var lines = new List<string>();
            for (int i = 0; i < rows.Length; i++)
            {
                bool reader = rows[i].F.Occupied().Any(o => readerIds.Contains(o.Def.Id));
                foreach (var v in g.Vars[i].Where(v => v.Relic == card))
                    foreach (int w in MainWaves)
                    {
                        Cell a = g.C[i][0][w], b = g.C[i][v.Ix][w];
                        bool st = StrongCell(a, b);
                        if (reader) { cells++; if (st) { strong++; lines.Add($"{rows[i].Name}・{WaveNames[w]}・→{rows[i].F[v.Frame]!.Name}: {b.Win - a.Win:+0.0;−0.0;±0.0}pt ／ {(a.Wins > 0 && b.Wins > 0 ? (b.T - a.T).ToString("+0.0;−0.0;±0.0") + "T" : "—")}"); } }
                        else { cc++; if (st) cs++; }
                    }
            }
            Console.WriteLine($"### {readerName} × {RName(card)}");
            Console.WriteLine();
            Console.WriteLine($"- 読み手の行で付けた方が強いセル: **{strong} ／ {cells}**（{100.0 * strong / Math.Max(1, cells):F1}%）・対照（読み手のいない行）: {cs} ／ {cc}（{100.0 * cs / Math.Max(1, cc):F1}%）");
            foreach (var l in lines) Console.WriteLine($"  - {l}");
            Console.WriteLine();
        }

        // ---- 4. 個性の保存（札種別） ----
        Console.WriteLine("## 4. 個性の保存（札種別・確定版）");
        Console.WriteLine();
        var keepTargets = hits.Where(h => RelicCatalog.Info(h.Relic).Kind is "繋ぎ" or "ゴミ").ToList();
        var keep = new string[keepTargets.Count];
        Parallel.For(0, keepTargets.Count, k =>
        {
            var h = keepTargets[k];
            keep[k] = KeepLine($"{rows[h.Row].Name}（{WaveNames[h.Wave]}）", rows[h.Row].F, h.V, h.Wave, BandA273);
        });
        int kept = keep.Count(l => l.EndsWith("| ○ |"));
        Console.WriteLine($"### 繋ぎ・ゴミ: **{kept} ／ {keep.Length}**（全例が線）——機構が個性を消した件 **{keep.Length - kept}**（線 0）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 駒 | 与ダメ 素 → 札 | 回復 素 → 札 | 状態の付与 素 → 札 | 受けたダメ 素 → 札 | 主の指標 | 判定 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|:-:|");
        foreach (var l in keep.Where(l => !l.EndsWith("| ○ |"))) Console.WriteLine(l);
        Console.WriteLine("（○ の行は省略）");
        Console.WriteLine();
        Console.WriteLine("### 変換（身を固める）: 符号反転の成立");
        Console.WriteLine();
        {
            int pos = hits.Count(h => h.Relic == TraitId.RelicHarden);
            var neg = new List<string>();
            for (int i = 0; i < rows.Length; i++)
            {
                int fr = SlotBy(rows[i].F, "攻");
                var v = g.Vars[i].First(x => x.Relic == TraitId.RelicHarden && x.Frame == fr);
                foreach (int w in MainWaves)
                {
                    Cell a = g.C[i][0][w], b = g.C[i][v.Ix][w];
                    if (FisherGreater(b.Wins, b.N, a.Wins, a.N) < 0.05) neg.Add($"{rows[i].Name}・{WaveNames[w]}（→{rows[i].F[fr]!.Name} {a.Win:F1} → {b.Win:F1}）");
                }
            }
            Console.WriteLine($"- 正: 固有の勝者の件 **{pos}**（線 ≧ 1）");
            Console.WriteLine($"- 負: 付け先の規則「攻」で素より有意に下がる行 × 波 **{neg.Count}**（線 ≧ 1）——例: {string.Join("・", neg.Take(5))}");
            Console.WriteLine($"- **符号反転: {(pos >= 1 && neg.Count >= 1 ? "成立" : "不成立")}**");
        }
        Console.WriteLine();

        // ---- 判定 ----
        Console.WriteLine("## 判定（指示書 §2 の下限）");
        Console.WriteLine();
        Console.WriteLine($"- 新札による固有の勝者: {newCardsWithRow} ／ 5 枚（≧ 3）→ **{(newCardsWithRow >= 3 ? "○" : "×")}**");
        Console.WriteLine($"- 混成の内訳（毒・感電・回復の橋）: {bridged} 件（≧ 5）→ **{(bridged >= 5 ? "○" : "×")}**");
        Console.WriteLine($"- 個性の保存（繋ぎ・ゴミ）: {kept} ／ {keep.Length}・機構が消した件 {keep.Length - kept} → **{(keep.Length - kept == 0 ? "○" : "×")}**");
        Console.WriteLine("- ゴミの成立（毒を招く × ベニ）: §3 の「ベニ × 毒を招く」で強いセルが 1 以上なら ○");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
