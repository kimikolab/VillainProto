using BattleCore;
using static Common;
using CW = CheckWaveDiag;

// bosswave run ／ compare ／ check ／ check269 —— 第269期「ボスの確定（N=37）・compare 別表・まとめ」。
// 指示書は design/PHASE269_BOSS_ADOPT_SPEC.md ／ 報告は design/PHASE269_BOSS_ADOPT.md ／ まとめは design/BOSS_SUMMARY.md。
// 規定形は `EnemyCatalog.BossRegular`（第268期の V-封 と同じ札の並び）。版 V-0（蝕みなし＝D-40）・V-18（第268期の V-半）は対照として並べる。
static partial class BossWaveDiag
{
    /// <summary>正式な指標（第269期・`run` の見出しに出す）。</summary>
    const string Indicators =
        "正式な指標（主は連続量・勝率は副）: **倒しT**（倒せない台は 20 ターンまでの総与ダメ）・**純実入り**（勇者が入れた − 隊の回復・20 ターンまで）・**崩れ始め**（最初の死亡のT）・"
        + "**蝕みで減った回復**（名目・1,200 から引いた量）・**層の推移**（回復が入るときの毒の層・T3 ／ T5 ／ T8）。副: 勝率・全員生存。";

    static VVer[] RegularVers => new[]
    {
        new VVer("規定", $"規定形（`EnemyCatalog.BossRegular`・蝕み N={VenomTaxTrait.Seal}）", () => EnemyCatalog.BossRegularWave),
        new VVer("V-18", $"版: 蝕み N={VenomTaxTrait.Half}（第268期の V-半）", () => EnemyWave.Of((2, VenomHero(TraitId.VenomTaxHalf)))),
        new VVer("V-0", "版: 蝕みなし（第266期の D-40）", () => EnemyWave.Of((2, VenomHero(null)))),
    };

    /// <summary>毒の書き手（第269期の棚卸し・指示書 §1.2 の5体）。軸タグ（行に何枚いるか）に使う。</summary>
    static readonly HashSet<string> PoisonWriters = new() { "guza", "sid", "mio", "rau", "beni" };

    static VAgg VMeasureF(Formation f, Func<EnemyWave> make)
    {
        var parts = new VAgg[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var (r, p, e) = CW.Fight(f, () => BattleEngine.MaterializeEnemy(make(), EnemyScaleRule.None), i, verbose: true);
            var a = new VAgg(); a.Take(r, p, e); parts[i] = a;
        });
        var all = new VAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static void RegularRun()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = RegularVers;
        Console.WriteLine($"# ボスの規定形（第269期）—— 勇者1体・全体・HP 3,000・攻 12・速 14・天井 +{BossRiseTrait.Calc}・動じない・自前の回復 40%・蝕み N={VenomTaxTrait.Seal}（7台・倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine(Indicators);
        Console.WriteLine();
        Console.WriteLine($"勝ち ＝ 勇者を倒す（engine の打ち切り {BattleEngine.MaxTurns} ターン）。まとめは design/BOSS_SUMMARY.md。");
        Console.WriteLine();
        foreach (var v in vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        var res = new Dictionary<(string, string), VAgg>();
        foreach (string b in Boards) foreach (var v in vers) res[(b, v.Name)] = VMeasure(b, v.Make);

        Console.WriteLine("## 表1 連続量（規定形）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倒しT | 純実入り | 崩れ始め | 蝕みで減った回復 | 勇者が受けた回復 | 層 T3 ／ T5 ／ T8 | 勝率 ／ 全員生存 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string b in Boards)
        {
            var a = res[(b, "規定")];
            Console.WriteLine($"| {b} | {VKill(a)} | {Per(a.Dealt - a.Healed, a.N)} | {T(a.FirstDeathT, a.FirstDeath)}（{Pct(a.FirstDeath, a.N)}%）| {Per(a.Cut, a.N)} | {Per(a.HeroHealed, a.N)} | {a.LayerAt(3)} ／ {a.LayerAt(5)} ／ {a.LayerAt(8)} | {F1(a.Win)} ／ {F1(100.0 * a.Surv / a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 表2 版との比較（倒しT ／ 勝率）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", vers.Select(v => "倒しT " + v.Name)) + " | " + string.Join(" | ", vers.Select(v => "勝率 " + v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, vers.Length * 2).Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", vers.Select(v => VKill(res[(b, v.Name)]))) + " | " + string.Join(" | ", vers.Select(v => F1(res[(b, v.Name)].Win))) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>`compare` の全行を規定形のボスに当てる別表（第269期 §1.2）。`compare` 本体には列を足さない。seed は `compare` と同じ 0..199。</summary>
    static void RegularCompare()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        Console.WriteLine($"# ボス別表（第269期）—— `compare` の全 {rows.Length} 行 × ボスの規定形（倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("採否の判定ではなく現況の把握（棚卸し）。`compare` 本体（`docs/balance.md`）には列を足していない。書き手 ＝ 毒の書き手（グザ・スィド・ミオ・ラウ・ベニ）が行に何枚いるか。");
        Console.WriteLine();
        var res = new (string Name, int W, VAgg A)[rows.Length];
        Parallel.For(0, rows.Length, i =>
        {
            var (name, f) = rows[i];
            int w = f.Occupied().Count(o => PoisonWriters.Contains(o.Def.Id));
            res[i] = (name, w, VMeasureF(f, () => EnemyCatalog.BossRegularWave));
        });
        Console.WriteLine("| 行 | 書き手 | 勝率 | 全員生存 | 倒しT | 崩れ始め | 層の平均 | 蝕みで減った回復 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, w, a) in res)
            Console.WriteLine($"| {name} | {w} | {F1(a.Win)} | {F1(100.0 * a.Surv / a.N)} | {VKill(a)} | {T(a.FirstDeathT, a.FirstDeath)}（{Pct(a.FirstDeath, a.N)}%）| {a.LayerAvg:F1} | {Per(a.Cut, a.N)} |");
        Console.WriteLine();

        Console.WriteLine("## 集計");
        Console.WriteLine();
        int under95 = res.Count(x => x.A.Win < 95), zero = res.Count(x => x.A.Wins == 0), full = res.Count(x => x.A.Wins == x.A.N);
        Console.WriteLine($"- 勝率 100% の行: **{full} ／ {rows.Length}**・95% 未満: **{under95}**・0%: **{zero}**");
        Console.WriteLine($"- 全行の平均勝率: {res.Average(x => x.A.Win):F1}%");
        Console.WriteLine();
        Console.WriteLine("| 書き手の枚数 | 行数 | 平均勝率 | 95% 未満の行 | 倒しT（倒した戦の平均）|");
        Console.WriteLine("|--:|--:|--:|--:|--:|");
        foreach (var g in res.GroupBy(x => x.W).OrderBy(g => g.Key))
        {
            long kt = g.Sum(x => x.A.KillT), kn = g.Sum(x => x.A.Kill);
            Console.WriteLine($"| {g.Key} | {g.Count()} | {g.Average(x => x.A.Win):F1} | {g.Count(x => x.A.Win < 95)} | {(kn == 0 ? "—" : "T" + ((double)kt / kn).ToString("F1"))} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 95% 未満の行");
        Console.WriteLine();
        var low = res.Where(x => x.A.Win < 95).OrderBy(x => x.A.Win).ToList();
        if (low.Count == 0) Console.WriteLine("（なし）");
        foreach (var (name, w, a) in low) Console.WriteLine($"- {name}（書き手 {w}）: {F1(a.Win)}%・{VKill(a)}・崩れ始め {T(a.FirstDeathT, a.FirstDeath)}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>
    /// 参考（第269期・採否には使わない・ノブは動かさない）: `compare` の全行に、規定形からノブを1つだけ緩めた3版を当てた集計だけ。
    /// 指示書 §1.2 の「倍率ノブの候補（HP・天井・回復率）を報告書に書く」ための材料。
    /// </summary>
    static void RegularKnobs()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        UnitDef Var(int hp, TraitId rise, bool mend) => new()
        {
            Id = "boss_regular", Name = "勇者", MaxHp = hp, Attack = 12, Speed = 14, Pattern = AttackPattern.All,
            Traits = mend ? new[] { rise, TraitId.BossMend40, TraitId.BossSteadfast, TraitId.VenomTaxSeal } : new[] { rise, TraitId.BossSteadfast },
        };
        var vars = new (string Name, UnitDef D)[]
        {
            ("規定", EnemyCatalog.BossRegular),
            ("HP 1,500", Var(1500, TraitId.BossRise11, true)),
            ("天井 +4", Var(3000, TraitId.BossRise4, true)),
            ("回復なし", Var(3000, TraitId.BossRise11, false)),
        };
        Console.WriteLine($"# ボス別表の参考（第269期）—— 規定形からノブを1つだけ緩めた3版 × `compare` の全 {rows.Length} 行（集計だけ・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("**採否には使わない・ノブは動かさない**（指示書 §3）。「回復なし」は回復 40% と蝕みを外した版（蝕みは回復が無ければ意味を持たない）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 100% の行 | 95% 未満 | 0% の行 | 平均勝率 | 5〜95% の行 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var (name, d) in vars)
        {
            var w = new double[rows.Length];
            Parallel.For(0, rows.Length, i => w[i] = VMeasureF(rows[i].F, () => EnemyWave.Of((2, d))).Win);
            Console.WriteLine($"| {name} | {w.Count(x => x >= 100)} | {w.Count(x => x < 95)} | {w.Count(x => x <= 0)} | {w.Average():F1} | {w.Count(x => x > 5 && x < 95)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Check269()
    {
        int pass = 0, fail = 0;
        void Ok(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            Console.WriteLine($"- [{(ok ? "○" : "×")}] {name}{(detail.Length > 0 ? $"（{detail}）" : "")}");
        }
        Console.WriteLine("# bosswave check269 —— 第269期 自己検査");
        Console.WriteLine();
        var h = EnemyCatalog.BossRegular;
        Ok("(a) 規定形 ＝ HP 3,000・攻 12・速 14・全体・{天井 +11, 自前の回復 40%, 動じない, 蝕み N=37}",
            h.MaxHp == 3000 && h.Attack == 12 && h.Speed == 14 && h.Pattern == AttackPattern.All
            && h.Traits.SequenceEqual(new[] { TraitId.BossRise11, TraitId.BossMend40, TraitId.BossSteadfast, TraitId.VenomTaxSeal }) && VenomTaxTrait.Seal == 37);
        {
            bool same = true; int n = 0;
            foreach (string b in Boards)
                for (int s = 0; s < 8; s++)
                {
                    var r1 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None), s).R;
                    var r2 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(EnemyWave.Of((2, VenomHero(TraitId.VenomTaxSeal))), EnemyScaleRule.None), s).R;
                    same &= r1.Log.Select(l => l.Text).SequenceEqual(r2.Log.Select(l => l.Text)) && r1.PlayerWon == r2.PlayerWon; n++;
                }
            Ok("(b) 規定形 ＝ 第268期の V-封（7台 × seed 0..7 の台本一致）", same, $"{n} 戦");
        }
        {
            var enemyDefs = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def))
                .Concat(EnemyCatalog.TestStages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)));
            Ok("(c) 規定形は `Stages` ／ 検証波に載っていない（`Stages` は 5 波のまま）", EnemyCatalog.Stages.Count == 5 && !enemyDefs.Contains(h));
            TraitId[] mine = { TraitId.VenomTaxSeal, TraitId.BossMend40, TraitId.BossSteadfast, TraitId.BossRise11 };
            int holders = UnitCatalog.Everyone.Count(u => u.Traits.Any(mine.Contains));
            Ok("(d) 規定形の札の保持者がロスター（`Everyone`）に 0 枚", holders == 0, $"{holders} 枚");
        }
        {
            var ids = PoisonWriters.ToList();
            bool all = ids.All(id => UnitCatalog.Everyone.Any(u => u.Id == id));
            Ok("(e) 書き手の5体（グザ・スィド・ミオ・ラウ・ベニ）がロスターに居る", all);
        }
        Console.WriteLine();
        Console.WriteLine($"合計 {pass + fail} 項目: ○ {pass} ／ × {fail}");
    }

    /// <summary>自己検査を全部（第265〜269期）。</summary>
    static void CheckAll()
    {
        Check(); Console.WriteLine();
        Check266(); Console.WriteLine();
        Check267(); Console.WriteLine();
        Check268(); Console.WriteLine();
        Check269();
    }
}
