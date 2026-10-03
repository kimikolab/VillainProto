using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;
using BH = BurnHitDiag;

// burnadopt check ／ compare —— 第256期の受け入れ 2・3。
static partial class BurnAdoptDiag
{
    static string LogKey(BattleResult r) => $"{r.PlayerWon}|{r.Turns}|{r.Events.Count}|" + string.Join("\n", r.Log.Select(l => l.Text));

    static Formation PinSero(Formation f)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied())
            if (ReferenceEquals(d, UnitCatalog.Sero)) g[slot] = UnitCatalog.SeroS0;
        return g;
    }

    static partial void Check()
    {
        var t0 = DateTime.Now;
        int ok = 0, ng = 0;
        void Ok(bool c, string what) { if (c) ok++; else ng++; Console.WriteLine($"- {(c ? "○" : "×")} {what}"); }
        Console.WriteLine("# 第256期 自己検査（`burnadopt check`）");
        Console.WriteLine();

        // 1. 規則の既定
        Ok(EmberRule.Default.BurnHit && !EmberRule.Pre256.BurnHit, $"`EmberRule.Default.BurnHit` ＝ true ／ `Pre256.BurnHit` ＝ false（Default = {EmberRule.Default}）");
        Ok(EmberRule.Pre256 == (EmberRule.Default with { BurnHit = false }) && EmberRule.Pre256.Brittle == 25, "`Pre256` ＝ 規定から被弾の燃焼だけを外したもの（脆さ 25 は残る）");
        Ok(new[] { "F0", "F1", "F2", "F3", "F4" }.All(t => !EmberRule.Scorched(t).BurnHit), "第219期の版（`Scorched`）は被弾の燃焼を持たない（第219期の器具を動かさない）");

        // 2. 規定の被弾の燃焼 ＝ 第255期の H-分担（札 `BurnHitSplit` ＋ Pre256）。台本（ログの全行・勝敗・決着T・出来事の件数）を突き合わせる。
        var boards = new (string Name, Formation F)[]
        {
            ("T3-244", FC.T3244), ("T3-238", FC.T3238), ("雷＋ボルグ", FC.ThunderBorg), ("毒", BH.PoisonBoard), ("参考 移動", BA.RefMove),
        };
        int n = 0, same = 0, moved = 0, noBurnSame = 0, noBurnN = 0;
        foreach (var (name, f) in boards)
            for (int w = 0; w <= 4; w++)
                for (int seed = 0; seed < 20; seed++)
                {
                    var rd = FightRule(f, w, seed, EmberRule.Default, null);
                    var rs = FightRule(f, w, seed, EmberRule.Pre256, TraitId.BurnHitSplit);
                    var r0 = FightRule(f, w, seed, EmberRule.Pre256, null);
                    n++;
                    if (LogKey(rd) == LogKey(rs)) same++;
                    if (LogKey(rd) != LogKey(r0)) moved++;
                    if (name is "毒" or "参考 移動") { noBurnN++; if (LogKey(rd) == LogKey(r0)) noBurnSame++; }
                }
        Ok(same == n, $"規定（`Default`）の台本 ＝ 第255期の H-分担（`Pre256` ＋ 札 `BurnHitSplit`）: {same} / {n} 戦（5 台 × 九/新兵・本編 第2〜5波 × 400/300 × seed 0..19）");
        Ok(moved > 0, $"規定は第255期までの規定（`Pre256`）と台本が違う戦がある（規則が実際に掛かっている）: {moved} / {n} 戦");
        Ok(noBurnSame == noBurnN, $"燃焼が付かない台（毒・参考 移動）は `Default` と `Pre256` で台本が一致: {noBurnSame} / {noBurnN} 戦");

        // 3. セロ
        Ok(!UnitCatalog.Sero.Traits.Contains(TraitId.StatusArrow), "規定のセロ（`UnitCatalog.Sero`）は状態の矢を持たない");
        Ok(UnitCatalog.SeroS0.Traits.Contains(TraitId.StatusArrow)
           && UnitCatalog.SeroS0.Traits.Where(t => t != TraitId.StatusArrow).SequenceEqual(UnitCatalog.Sero.Traits)
           && UnitCatalog.SeroS0.MaxHp == UnitCatalog.Sero.MaxHp && UnitCatalog.SeroS0.Attack == UnitCatalog.Sero.Attack && UnitCatalog.SeroS0.Speed == UnitCatalog.Sero.Speed,
           "旧（`SeroS0`）＝ 規定 ＋ 状態の矢（ほかの札と数値は同じ）");
        Ok(UnitCatalog.All.Contains(UnitCatalog.Sero) && !UnitCatalog.Everyone.Contains(UnitCatalog.SeroS0), "`All` に規定のセロ・`SeroS0` は `All` にも `Retired` にも入れない");
        var arrowHolders = UnitCatalog.Everyone.Where(u => u.Traits.Contains(TraitId.StatusArrow)).Select(u => u.Name).ToList();
        Ok(arrowHolders.Count == 0, $"`StatusArrow` の保持者は `UnitCatalog.Everyone` に 0 枚（{string.Join("・", arrowHolders)}）");
        Ok(!UnitCatalog.Sero.PlusText.Contains("矢がそれを敵にうつす"), "規定のセロの説明文から状態の矢の1文が消えている");
        // 矢が撃たれるか: ボルグの隣のセロ（火の粉で燃える）。旧は撃ち、規定は撃たない。
        var fa = Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Hota, center: UnitCatalog.Borg, back1: UnitCatalog.Gald, back3: UnitCatalog.Hiyo);
        long arrowsNew = 0, arrowsOld = 0;
        for (int w = 0; w < 4; w++)
            for (int seed = 0; seed < 50; seed++)
            {
                arrowsNew += FightRule(fa, w, seed, EmberRule.Default, null).Events.Count(e => e.Kind == BattleEventKind.StatusArrow);
                arrowsOld += FightRule(PinSero(fa), w, seed, EmberRule.Default, null).Events.Count(e => e.Kind == BattleEventKind.StatusArrow);
            }
        Ok(arrowsNew == 0 && arrowsOld > 0, $"台（セロ前1・ボルグ中央）× 本編 第2〜5波 × seed 0..49: 状態の矢の出来事 規定 {arrowsNew} 件 ／ 旧 {arrowsOld} 件");

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（{(DateTime.Now - t0).TotalSeconds:F0} 秒）");
        Console.WriteLine($"BURNADOPT_CHECK ok={ok} ng={ng}");
    }

    /// <summary>九/新兵（w = 4）・本編 第2〜5波（w = 0..3）× 400/300。</summary>
    static BattleResult FightRule(Formation f, int w, int seed, EmberRule ember, TraitId? card)
        => BattleEngine.Run(BattleEngine.Materialize(BH.Mark(f, card), BattleContext.PlayerTeam), FC.WaveOf(w, BA.Scales[1].Sc)(), seed, verbose: true, ember: ember);

    /// <summary>`compare` 61 行（seed 0..199）を 4 通り（燃焼の規則 × セロ）で回し、動いたセルを原因で分ける。</summary>
    static partial void CompareSplit()
    {
        var t0 = DateTime.Now;
        var rows = CompareBuilds();
        int S = EnemyCatalog.Stages.Count;
        var combos = new (string Name, EmberRule E, bool OldSero)[]
        {
            ("旧（第255期）", EmberRule.Pre256, true), ("燃焼だけ", EmberRule.Default, true), ("セロだけ", EmberRule.Pre256, false), ("新（第256期）", EmberRule.Default, false),
        };
        var wins = new int[combos.Length, rows.Length, S];
        Parallel.For(0, combos.Length * rows.Length * S, k =>
        {
            int c = k / (rows.Length * S), i = k / S % rows.Length, st = k % S;
            var f = combos[c].OldSero ? PinSero(rows[i].F) : rows[i].F;
            int wn = 0;
            for (int seed = 0; seed < 200; seed++) if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, ember: combos[c].E).PlayerWon) wn++;
            wins[c, i, st] = wn;
        });
        string P(int c, int i, int st) => (wins[c, i, st] / 2.0).ToString("F1");
        Console.WriteLine("# 第256期 `compare` 61 行の動いたセル（原因で分ける・seed 0..199）");
        Console.WriteLine();
        foreach (var (label, a, b) in new[] { ("燃焼の規則（H-分担）", 0, 1), ("セロの状態の矢", 0, 2), ("両方（第256期の規定）", 0, 3) })
        {
            Console.WriteLine($"## {label}: {combos[a].Name} → {combos[b].Name}");
            Console.WriteLine();
            int cells = 0, rowsMoved = 0;
            var lines = new List<string>();
            for (int i = 0; i < rows.Length; i++)
            {
                bool any = false;
                var parts = new List<string>();
                for (int st = 0; st < S; st++)
                {
                    bool mv = wins[a, i, st] != wins[b, i, st];
                    if (mv) { cells++; any = true; }
                    parts.Add(mv ? $"**{P(a, i, st)} → {P(b, i, st)}**" : P(b, i, st));
                }
                if (any) { rowsMoved++; lines.Add($"| {rows[i].Name} | {string.Join(" | ", parts)} |"); }
            }
            Console.WriteLine($"動いた: **{rowsMoved} 行 ／ {cells} セル**");
            if (lines.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, S).Select(x => $" 第{x}波 |")));
                Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, S).Select(_ => "--:|")));
                foreach (var l in lines) Console.WriteLine(l);
            }
            Console.WriteLine();
        }
        bool HasSero(Formation f) => f.Occupied().Any(o => o.Def.Id == "sero");
        int seroRows = rows.Count(r => HasSero(r.F));
        Console.WriteLine($"セロのいる行: {seroRows} 行（{string.Join("・", rows.Where(r => HasSero(r.F)).Select(r => r.Name))}）");
        // 旧のセロ（状態の矢あり）で、その12行の中で矢が実際に撃たれた回数（台本の `StatusArrow`・第2〜5波）。
        var seroIdx = Enumerable.Range(0, rows.Length).Where(i => HasSero(rows[i].F)).ToArray();
        var arrows = new long[seroIdx.Length];
        Parallel.For(0, seroIdx.Length, j =>
        {
            for (int st = 1; st < S; st++)
                for (int seed = 0; seed < 200; seed++)
                    arrows[j] += BattleEngine.Run(PinSero(rows[seroIdx[j]].F), EnemyCatalog.Stages[st].Enemy, seed, verbose: true, ember: EmberRule.Default).Events.Count(e => e.Kind == BattleEventKind.StatusArrow);
        });
        Console.WriteLine("旧のセロ（状態の矢あり）× 第256期の燃焼の規則で、矢が撃たれた回数（第2〜5波 × 200 戦 ＝ 800 戦あたり）: "
            + string.Join("・", seroIdx.Select((i, j) => $"{rows[i].Name} {arrows[j]}")));
        Console.WriteLine();
        Console.WriteLine("全61行の第1〜5波の平均: " + string.Join(" ／ ", combos.Select((c, ci) =>
            $"{c.Name} " + string.Join(" / ", Enumerable.Range(0, S).Select(st => (Enumerable.Range(0, rows.Length).Average(i => wins[ci, i, st]) / 2.0).ToString("F1"))))));
        Console.WriteLine();
        Console.WriteLine($"（{(DateTime.Now - t0).TotalSeconds:F0} 秒）");
    }
}
