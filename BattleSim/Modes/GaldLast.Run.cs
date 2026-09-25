using BattleCore;
using static Common;

// =====================================================================================
// galdlast run / ledger / check —— 版 × ガルドの在席行・剣の段の帳簿・自己検査
// 第198期に作り、第199期に版を差し替えた（第198期の「剣」「返しなし」は札だけ残置。表からは外した）。
// =====================================================================================

static partial class GaldLastDiag
{
    static partial void RunMoreImpl(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run": RunVersions(); handled = true; return;
            case "ledger": Ledger(); handled = true; return;
            case "check": Check(arg); handled = true; return;
        }
    }

    // =================================================================================
    // 版
    // =================================================================================

    static UnitDef Clone(UnitDef d, TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
        Advances = d.Advances, Actions = d.Actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    static UnitDef GaldWith(TraitId? last)
        => Clone(UnitCatalog.Gald, last is TraitId id
            ? new[] { TraitId.Guardian, TraitId.Stoic, TraitId.Parry, id }
            : new[] { TraitId.Guardian, TraitId.Stoic, TraitId.Parry });

    // 添字（表の列の意味が添字に依るので名前で持つ）
    const int VNone = 0, V198 = 1, V199 = 2, VOldScar = 3, VNoStock = 4, VMutualLoss = 5, VShield = 6;

    /// <summary>版の並び。規定は 199 ＝ <c>UnitCatalog.Gald</c> そのもの。盾剣は第198期の参考として残す。</summary>
    static readonly (string Name, UnitDef Def)[] Versions =
    {
        ("無", GaldWith(null)),
        ("198", GaldWith(TraitId.LastStandScar)),
        ("199", UnitCatalog.Gald),
        ("199・傷は旧", GaldWith(TraitId.LastStandHoldOldScar)),
        ("199・在庫なし", GaldWith(TraitId.LastStandHoldNoStock)),
        ("199・相打ち負け", GaldWith(TraitId.LastStandHoldMutualLoss)),
        ("盾剣（198 参考）", GaldWith(TraitId.LastStandShield)),
    };

    static Formation WithGald(Formation f, UnitDef g) => FvSwap(f, UnitCatalog.Gald, g);

    // =================================================================================
    // 1版 × 1行 の集計
    // =================================================================================

    sealed class Acc
    {
        public int N, Wins, Timeouts;
        public int[] WaveWins = new int[5], WaveN = new int[5];
        public double TurnsSum;
        // 剣の段（無の版は「最後の1体」で数える）
        public int InN, InWins, InTimeouts, InDied, InMutual, MutualWins;
        public double InTurnSum, InRemainSum, InHpPct, InFoes, InDeathAfterSum, InTurnsSum;
        public double Sweep, Riposte, Dying, RiposteN, DyingN, Blocked, Skipped, InReaction, ParriedAfter, RefillBlocked, StockDropped;
        public double ScarTaken, ParriedTaken, ScarAtk, StockAtDraw;
        public int ScarAtkPos;
    }

    static void Measure(Acc a, Formation f, int st, int seed)
    {
        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
        a.N++; a.WaveN[st]++;
        a.TurnsSum += r.Turns;
        if (r.PlayerWon) { a.Wins++; a.WaveWins[st]++; }
        bool to = !r.PlayerWon && r.Turns >= BattleEngine.MaxTurns;
        if (to) a.Timeouts++;
        if (!r.TallyByUnit.TryGetValue("gald", out UnitTally? g)) return;
        int entry = g.LastStandTurn > 0 ? g.LastStandTurn : g.AloneTurn;
        if (entry <= 0) return;
        a.InN++;
        if (r.PlayerWon) a.InWins++;
        if (to) a.InTimeouts++;
        a.InTurnSum += entry;
        a.InTurnsSum += r.Turns;
        a.InRemainSum += r.Turns - entry;
        int hp = g.LastStandTurn > 0 ? g.LastStandHp : g.AloneHp, mx = g.LastStandTurn > 0 ? g.LastStandMaxHp : g.AloneMaxHp;
        a.InHpPct += mx == 0 ? 0 : 100.0 * hp / mx;
        a.InFoes += g.LastStandTurn > 0 ? g.LastStandFoes : g.AloneFoes;
        if (g.Deaths > 0) { a.InDied++; a.InDeathAfterSum += g.LastActiveTurn - entry; }
        if (g.LastStandDyingRipostes > 0) a.InMutual++;
        if (g.LastStandMutualWins > 0) a.MutualWins++;
        if (g.LastStandTurn > 0)
        {
            double inStage = g.DamageToEnemy - g.LastStandBaseDealt;
            a.Riposte += g.LastStandRiposteDealt; a.Dying += g.LastStandDyingDealt;
            a.Sweep += inStage - g.LastStandRiposteDealt - g.LastStandDyingDealt;
            a.RiposteN += g.LastStandRipostes; a.DyingN += g.LastStandDyingRipostes;
            a.Blocked += g.LastStandRiposteBlocked; a.Skipped += g.LastStandRiposteSkipped; a.InReaction += g.LastStandRiposteInReaction;
            a.ParriedAfter += g.LastStandParried; a.RefillBlocked += g.LastStandRefillBlocked; a.StockDropped += g.LastStandStockDropped;
            a.ScarTaken += g.LastStandScarTaken; a.ParriedTaken += g.LastStandParriedTaken; a.ScarAtk += g.LastStandScarAtk;
            a.StockAtDraw += g.LastStandStockAtDraw;
            if (g.LastStandScarAtk > 0) a.ScarAtkPos++;
        }
    }

    static Acc[,] MeasureAll(List<(string Name, Formation F)> rows)
    {
        var acc = new Acc[rows.Count, Versions.Length];
        Parallel.For(0, rows.Count * Versions.Length, k =>
        {
            int i = k / Versions.Length, v = k % Versions.Length;
            var a = new Acc();
            Formation f = WithGald(rows[i].F, Versions[v].Def);
            for (int st = 1; st <= 4; st++)
                for (int seed = 0; seed < Seeds; seed++) Measure(a, f, st, seed);
            acc[i, v] = a;
        });
        return acc;
    }

    static Acc Sum(IEnumerable<Acc> l)
    {
        var s = new Acc();
        foreach (Acc a in l)
        {
            s.N += a.N; s.Wins += a.Wins; s.Timeouts += a.Timeouts; s.TurnsSum += a.TurnsSum;
            for (int w = 0; w < 5; w++) { s.WaveWins[w] += a.WaveWins[w]; s.WaveN[w] += a.WaveN[w]; }
            s.InN += a.InN; s.InWins += a.InWins; s.InTimeouts += a.InTimeouts; s.InDied += a.InDied; s.InMutual += a.InMutual; s.MutualWins += a.MutualWins;
            s.InTurnSum += a.InTurnSum; s.InRemainSum += a.InRemainSum; s.InHpPct += a.InHpPct; s.InFoes += a.InFoes;
            s.InDeathAfterSum += a.InDeathAfterSum; s.InTurnsSum += a.InTurnsSum;
            s.Sweep += a.Sweep; s.Riposte += a.Riposte; s.Dying += a.Dying; s.RiposteN += a.RiposteN; s.DyingN += a.DyingN;
            s.Blocked += a.Blocked; s.Skipped += a.Skipped; s.InReaction += a.InReaction; s.ParriedAfter += a.ParriedAfter;
            s.RefillBlocked += a.RefillBlocked; s.StockDropped += a.StockDropped;
            s.ScarTaken += a.ScarTaken; s.ParriedTaken += a.ParriedTaken; s.ScarAtk += a.ScarAtk; s.StockAtDraw += a.StockAtDraw; s.ScarAtkPos += a.ScarAtkPos;
        }
        return s;
    }

    static string Pc(int a, int n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
    static string Dl(int a0, int n0, int a1, int n1)
        => n0 == 0 || n1 == 0 ? "—" : (100.0 * a1 / n1 - 100.0 * a0 / n0).ToString("+0.0;-0.0;±0.0");

    // =================================================================================
    // run —— 版 × ガルドの在席行
    // =================================================================================

    static void RunVersions()
    {
        var rows = GaldRows().ToList();
        Acc[,] acc = MeasureAll(rows);
        Console.WriteLine("# 第199期 `galdlast run` —— 版 × ガルドの在席行（`compare` 35 行・第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("版: **無**（剣の段なし＝第197期）／ **198**（第198期の規定・剣＋傷）／ **199**（規定）／ 199・傷は旧 ／ 199・在庫なし ／ 199・相打ち負け ／ 盾剣（第198期の参考）。"
                          + "`入った` ＝ 剣の段に入った戦（無の版は「最後の1体」になった戦）。");
        Console.WriteLine();
        Console.WriteLine("## 表A 行ごとの勝率（第2〜5波の平均・%）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 入った | " + string.Join(" | ", Versions.Select(v => v.Name)) + " | 199 − 198 |");
        Console.WriteLine("|---|--:|" + string.Concat(Versions.Select(_ => "--:|")) + "--:|");
        for (int i = 0; i < rows.Count; i++)
            Console.WriteLine("| " + rows[i].Name + " | " + Pc(acc[i, V199].InN, acc[i, V199].N) + " | "
                              + string.Join(" | ", Enumerable.Range(0, Versions.Length).Select(v => Pc(acc[i, v].Wins, acc[i, v].N))) + " | "
                              + Dl(acc[i, V198].Wins, acc[i, V198].N, acc[i, V199].Wins, acc[i, V199].N) + " |");
        var tot = Enumerable.Range(0, Versions.Length).Select(v => Sum(Enumerable.Range(0, rows.Count).Select(i => acc[i, v]))).ToArray();
        Console.WriteLine("| **計** | " + Pc(tot[V199].InN, tot[V199].N) + " | " + string.Join(" | ", tot.Select(a => Pc(a.Wins, a.N))) + " | "
                          + Dl(tot[V198].Wins, tot[V198].N, tot[V199].Wins, tot[V199].N) + " |");
        Console.WriteLine();

        Console.WriteLine("## 表B 波ごと（在席行の合計・勝率 %）");
        Console.WriteLine();
        Console.WriteLine("| 波 | " + string.Join(" | ", Versions.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Versions.Select(_ => "--:|")));
        for (int st = 1; st <= 4; st++)
            Console.WriteLine("| 第" + (st + 1) + "波 | " + string.Join(" | ", tot.Select(a => Pc(a.WaveWins[st], a.WaveN[st]))) + " |");
        Console.WriteLine();

        Console.WriteLine("## 表C 剣の段に入った戦（在席行の合計）");
        Console.WriteLine();
        Console.WriteLine("`残りT` ＝ 決着T − 入ったT。`倒れるまで` ＝ 入ってからガルドが倒れるまでのターン（倒れた戦だけ）。"
                          + "`相打ち` ＝ 倒れる一撃に斬り返した戦。`相打ち勝ち` ＝ その相打ちで最後の敵を倒して勝った戦（2.3）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 入った戦 | 入ったT | HP | 敵の数 | 勝率 | 打ち切り | 倒れた | 相打ち | 相打ち勝ち | 残りT | 倒れるまで | 全体の打ち切り | 全体の決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int v = 0; v < Versions.Length; v++)
        {
            Acc a = tot[v];
            Console.WriteLine("| " + Versions[v].Name + " | " + a.InN + "（" + Pc(a.InN, a.N) + "%） | " + Avg(a.InTurnSum, a.InN, "F2") + " | "
                              + (a.InN == 0 ? "—" : (a.InHpPct / a.InN).ToString("F0") + "%") + " | " + Avg(a.InFoes, a.InN) + " | "
                              + Pc(a.InWins, a.InN) + " | " + Pc(a.InTimeouts, a.InN) + " | " + Pc(a.InDied, a.InN) + " | " + Pc(a.InMutual, a.InN) + " | "
                              + Pc(a.MutualWins, a.InN) + " | "
                              + Avg(a.InRemainSum, a.InN, "F1") + " | " + Avg(a.InDeathAfterSum, a.InDied, "F1") + " | "
                              + Pc(a.Timeouts, a.N) + " | " + Avg(a.TurnsSum, a.N) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 表D 剣の段の上乗せと受け流し・与えたダメージ（入った戦1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("`身に受けた傷` / `受け流した刃` ＝ 抜いたときの累計（版に依らず同じ）。`上乗せ` ＝ 加えた攻撃力（×2 の前）。"
                          + "`在庫` ＝ 抜いたときの受け流しの在庫。`剣で受け流し` ＝ 抜いた後に受け流した回数。"
                          + "`手番` ＝ 剣の段の `DamageToEnemy` の増分 − 斬り返し − 相打ち。`止められた` ＝ 粛・痺れ・組み付きで返せなかった被弾。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 身に受けた傷 | 受け流した刃 | 上乗せ | 在庫 | 剣で受け流し | 手番 | 斬り返し | 相打ち | 斬り返しの割合 | 返した回数 | 相打ちの回数 | 止められた |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int v = 1; v < Versions.Length; v++)
        {
            Acc a = tot[v];
            double all = a.Sweep + a.Riposte + a.Dying;
            Console.WriteLine("| " + Versions[v].Name + " | " + Avg(a.ScarTaken, a.InN, "F1") + " | " + Avg(a.ParriedTaken, a.InN, "F1") + " | "
                              + Avg(a.ScarAtk, a.InN) + " | " + Avg(a.StockAtDraw, a.InN) + " | " + Avg(a.ParriedAfter, a.InN) + " | "
                              + Avg(a.Sweep, a.InN, "F1") + " | " + Avg(a.Riposte, a.InN, "F1") + " | " + Avg(a.Dying, a.InN, "F1") + " | "
                              + (all == 0 ? "—" : (100 * (a.Riposte + a.Dying) / all).ToString("F0") + "%") + " | "
                              + Avg(a.RiposteN, a.InN) + " | " + Avg(a.DyingN, a.InN) + " | " + Avg(a.Blocked, a.InN) + " |");
        }
    }

    // =================================================================================
    // ledger —— 波ごと・行ごとの帳簿（199 と 198）
    // =================================================================================

    static void Ledger()
    {
        var rows = GaldRows().ToList();
        Console.WriteLine("# 第199期 `galdlast ledger` —— 剣の段の帳簿（199 と 198・波ごと／行ごと）");
        Console.WriteLine();
        foreach (int vi in new[] { V199, V198 })
        {
            Console.WriteLine("## " + Versions[vi].Name + " —— 波ごと");
            Console.WriteLine();
            Console.WriteLine("| 波 | 入った戦 | 入ったT | 勝率 | 相打ち勝ち | 打ち切り | 倒れるまで | 上乗せ | 在庫 | 剣で受け流し | 手番 | 斬り返し | 相打ち | 止められた |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            var perWave = new Acc[5];
            Parallel.For(1, 5, st =>
            {
                var a = new Acc();
                foreach (var (_, f0) in rows)
                {
                    Formation f = WithGald(f0, Versions[vi].Def);
                    for (int seed = 0; seed < Seeds; seed++) Measure(a, f, st, seed);
                }
                perWave[st] = a;
            });
            for (int st = 1; st <= 4; st++)
            {
                Acc a = perWave[st];
                Console.WriteLine("| 第" + (st + 1) + "波 | " + Pc(a.InN, a.N) + "% | " + Avg(a.InTurnSum, a.InN, "F1") + " | " + Pc(a.InWins, a.InN) + " | "
                                  + Pc(a.MutualWins, a.InN) + " | " + Pc(a.InTimeouts, a.InN) + " | " + Avg(a.InDeathAfterSum, a.InDied, "F1") + " | "
                                  + Avg(a.ScarAtk, a.InN) + " | " + Avg(a.StockAtDraw, a.InN) + " | " + Avg(a.ParriedAfter, a.InN) + " | "
                                  + Avg(a.Sweep, a.InN, "F1") + " | " + Avg(a.Riposte, a.InN, "F1") + " | " + Avg(a.Dying, a.InN, "F1") + " | "
                                  + Avg(a.Blocked, a.InN) + " |");
            }
            Console.WriteLine();
        }
        Console.WriteLine("## 199 —— 行ごと（入った戦だけ）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 入った | 198 の勝率 | 199 の勝率 | 相打ち勝ち | 倒れるまで 198 → 199 | 上乗せ 198 → 199 | 在庫 | 剣で受け流し | 斬り返しの割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        Acc[,] acc = MeasureAll(rows);
        for (int i = 0; i < rows.Count; i++)
        {
            Acc b = acc[i, V198], a = acc[i, V199];
            double all = a.Sweep + a.Riposte + a.Dying;
            Console.WriteLine("| " + rows[i].Name + " | " + Pc(a.InN, a.N) + "% | " + Pc(b.InWins, b.InN) + " | " + Pc(a.InWins, a.InN) + " | "
                              + Pc(a.MutualWins, a.InN) + " | "
                              + Avg(b.InDeathAfterSum, b.InDied, "F1") + " → " + Avg(a.InDeathAfterSum, a.InDied, "F1") + " | "
                              + Avg(b.ScarAtk, b.InN) + " → " + Avg(a.ScarAtk, a.InN) + " | " + Avg(a.StockAtDraw, a.InN) + " | "
                              + Avg(a.ParriedAfter, a.InN) + " | "
                              + (all == 0 ? "—" : (100 * (a.Riposte + a.Dying) / all).ToString("F0") + "%") + " |");
        }
    }

    // =================================================================================
    // check —— 自己検査（第199期 §6）
    // =================================================================================

    static void Check(string balancePath)
    {
        if (string.IsNullOrWhiteSpace(balancePath)) balancePath = Path.Combine("docs", "balance.md");
        Console.WriteLine("# 第199期 `galdlast check` —— 自己検査");
        Console.WriteLine();
        bool ok = true;

        // (a) 198 の版（全 61 行のガルドを第198期の札へ）が第198期の balance.md と一致 ／ ガルドを含まない行は規定でも一致
        var before = ParseBalance(balancePath);
        int cells = 0, diff198 = 0, diffNonGald = 0, nonGaldCells = 0;
        UnitDef v198 = Versions[V198].Def;
        foreach (var (name, f) in CompareBuilds())
        {
            if (!before.TryGetValue(name, out double[]? b)) { Console.WriteLine("- 行名が引けない: " + name); ok = false; continue; }
            bool hasGald = f.Occupied().Any(o => o.Def.Id == "gald");
            Formation f0 = WithGald(f, v198);
            for (int st = 0; st < 5; st++)
            {
                int w0 = 0, w1 = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    if (BattleEngine.Run(f0, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) w0++;
                    if (!hasGald && BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) w1++;
                }
                cells++;
                if (Math.Abs(100.0 * w0 / Seeds - b[st]) > 0.05) diff198++;
                if (!hasGald)
                {
                    nonGaldCells++;
                    if (Math.Abs(100.0 * w1 / Seeds - b[st]) > 0.05) diffNonGald++;
                }
            }
        }
        Console.WriteLine($"- (a) 198 の版 × 61 行 × 5 波 = {cells} セル中、`{balancePath}` と違うセル: **{diff198}**");
        Console.WriteLine($"- (a') ガルドを含まない行（規定）{nonGaldCells} セル中、違うセル: **{diffNonGald}**");
        if (diff198 != 0 || diffNonGald != 0) ok = false;

        // (b)(c)(d) 版をまたいだ入る瞬間・構え直し・斬り返し・相打ち勝ちの経路
        int entryMismatch = 0, sword = 0, stanceAfter = 0, refillAfter = 0, riposteBad = 0, riposteEv = 0, mutualEv = 0;
        int zeroWins = 0, zeroWinsBad = 0, victoryEv = 0, victoryBad = 0, lossVersionZeroWins = 0, parriedAfter = 0;
        foreach (var (_, f) in GaldRows())
        {
            Formation fNone = WithGald(f, Versions[VNone].Def);
            for (int st = 1; st <= 4; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult r0 = BattleEngine.Run(fNone, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    int alone = r0.TallyByUnit.TryGetValue("gald", out UnitTally? g0) ? g0.AloneTurn : 0;
                    for (int v = 1; v < Versions.Length; v++)
                    {
                        bool verbose = v == V199;
                        BattleResult r = BattleEngine.Run(WithGald(f, Versions[v].Def), EnemyCatalog.Stages[st].Enemy, seed, verbose: verbose);
                        UnitTally g = r.TallyByUnit["gald"];
                        if (g.LastStandTurn != alone) entryMismatch++;
                        bool swordVer = v != VShield;
                        if (g.LastStandTurn > 0 && swordVer)
                        {
                            sword++;
                            if (g.ParryStances != g.LastStandStancesAtDraw) stanceAfter++;
                            if (g.ParryRefillGuard != g.LastStandRefillGuardAtDraw) refillAfter++;
                            parriedAfter += (int)g.LastStandParried;
                        }
                        // 生存 0 の勝ちは相打ち勝ち（印）だけ
                        if (r.PlayerWon && r.PlayerSurvivors == 0)
                        {
                            zeroWins++;
                            if (g.LastStandMutualWins == 0) zeroWinsBad++;
                            if (v == VMutualLoss || v == V198) lossVersionZeroWins++;
                        }
                        if (!verbose) continue;
                        var ev = r.Events;
                        for (int k = 0; k < ev.Count; k++)
                        {
                            if (ev[k].Kind == BattleEventKind.LastStandVictory)
                            {
                                victoryEv++;
                                // 直前の相打ちの斬り返しが同じ敵に向いているか
                                BattleEvent? rip = null;
                                for (int j = k - 1; j >= 0; j--) if (ev[j].Kind == BattleEventKind.LastStandRiposte) { rip = ev[j]; break; }
                                if (rip is null || rip.Slot != 1 || rip.TargetId != ev[k].TargetId || !r.PlayerWon) victoryBad++;
                            }
                            if (ev[k].Kind != BattleEventKind.LastStandRiposte) continue;
                            riposteEv++;
                            if (ev[k].Slot == 1) mutualEv++;
                            // 直前の「ガルドに向いた Damage か Parry」が、その敵の・巻き込みでも中継でもない Damage であること
                            BattleEvent? hit = null;
                            for (int j = k - 1; j >= 0; j--)
                                if ((ev[j].Kind == BattleEventKind.Damage || ev[j].Kind == BattleEventKind.Parry) && ev[j].TargetId == ev[k].ActorId) { hit = ev[j]; break; }
                            if (hit is null || hit.Kind != BattleEventKind.Damage || hit.ActorId != ev[k].TargetId || hit.FriendlyFire || hit.Relayed) riposteBad++;
                        }
                    }
                }
        }
        Console.WriteLine($"- (b) 剣の段に入ったターンが「無」の版の「最後の1体になったターン」と違う戦: **{entryMismatch}**（6 版 × 在席 35 行 × 第2〜5波 × seed 0..49）");
        Console.WriteLine($"- (c) 盾を捨てる版で剣の段に入った {sword} 戦のうち、抜いた後に構え直した戦: **{stanceAfter}** ／ 庇いで在庫が戻った戦: **{refillAfter}**"
                          + $"（抜いた後に受け流した回数は {parriedAfter}＝残った在庫のぶん）");
        Console.WriteLine($"- (c') 台本（199）: 斬り返し {riposteEv} 件（うち相打ち {mutualEv}）。直前のガルドへの一撃が「その敵の・受け流されていない・巻き込みでも中継でもない `Damage`」でない斬り返し: **{riposteBad}**");
        Console.WriteLine($"- (d) 生存 0 の勝ち {zeroWins} 戦のうち、相打ち勝ちの印が立っていない戦: **{zeroWinsBad}** ／ 198・相打ち負けの版で生存 0 の勝ち: **{lossVersionZeroWins}**");
        Console.WriteLine($"- (d') 台本（199）: `LastStandVictory` {victoryEv} 件のうち、直前が同じ敵への相打ちの斬り返しでない／勝ちでない: **{victoryBad}**");
        if (entryMismatch != 0 || stanceAfter != 0 || refillAfter != 0 || riposteBad != 0 || zeroWinsBad != 0 || lossVersionZeroWins != 0 || victoryBad != 0) ok = false;

        // (e) 会戦で生存 0 の勝ちが落ちない（全在席行 × seed 0..99・規定の版）
        int eng = 0, engZeroWin = 0;
        foreach (var (_, f) in GaldRows())
            for (int seed = 0; seed < 100; seed++)
            {
                EngagementResult e = EngagementEngine.Run(new[] { f }, EnemyCatalog.EngagementColumn, seed, verbose: false);
                eng++;
                foreach (BattleResult br in e.Battles) if (br.PlayerWon && br.PlayerSurvivors == 0) engZeroWin++;
            }
        Console.WriteLine($"- (e) 会戦（`EngagementColumn`・在席 35 行 × seed 0..99 ＝ {eng} 会戦）が例外なく回った。うち生存 0 の勝ちを含む戦 {engZeroWin}");

        // (f) 乱数: 新しい処理は PickOne / Roll を呼ばない（ソースの走査）
        string src = File.ReadAllText(Path.Combine("BattleCore", "Traits.cs"));
        int a0 = src.IndexOf("public class LastStandTrait", StringComparison.Ordinal);
        int a1 = src.IndexOf("public sealed class LastStandShieldTrait", StringComparison.Ordinal);
        string body = a0 >= 0 && a1 > a0 ? src.Substring(a0, a1 - a0) : "";
        string eng2 = File.ReadAllText(Path.Combine("BattleCore", "BattleEngine.cs"));
        int b0 = eng2.IndexOf("public bool MarkLastStandVictory", StringComparison.Ordinal);
        string body2 = b0 >= 0 ? eng2.Substring(b0, Math.Min(1200, eng2.Length - b0)) : "";
        if (body.Length == 0 || body2.Length == 0) { Console.WriteLine("- (f) 走査が空（止める）"); ok = false; }
        else
        {
            bool rng = (body + body2).Contains("PickOne" + "(") || (body + body2).Contains(".Roll" + "(") || (body + body2).Contains("Shuffled" + "(");
            Console.WriteLine($"- (f) `LastStandTrait` 系と `MarkLastStandVictory` の本文に乱数の呼び出し: **{(rng ? "あり" : "なし")}**");
            if (rng) ok = false;
        }

        Console.WriteLine();
        Console.WriteLine(ok ? "**合格**" : "**不合格**");
    }

    static Dictionary<string, double[]> ParseBalance(string path)
    {
        var d = new Dictionary<string, double[]>();
        foreach (string raw in File.ReadAllLines(path))
        {
            string l = raw.Trim();
            if (!l.StartsWith("| ") || !l.Contains('%')) continue;
            string[] c = l.Trim('|').Split('|').Select(x => x.Trim()).ToArray();
            if (c.Length < 6) continue;
            var v = new double[5];
            bool okp = true;
            for (int i = 0; i < 5; i++) okp &= double.TryParse(c[i + 1].TrimEnd('%'), out v[i]);
            if (okp) d[c[0]] = v;
        }
        return d;
    }
}
