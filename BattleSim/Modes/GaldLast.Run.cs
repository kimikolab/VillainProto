using BattleCore;
using static Common;

// =====================================================================================
// galdlast run / ledger / check（第198期） —— 版 × ガルドの在席行・剣の段の帳簿・自己検査
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

    /// <summary>版の並び（先頭が対照）。規定は 剣＋傷 ＝ <c>UnitCatalog.Gald</c> そのもの。</summary>
    static readonly (string Name, UnitDef Def)[] Versions =
    {
        ("無", GaldWith(null)),
        ("剣＋傷", UnitCatalog.Gald),
        ("剣", GaldWith(TraitId.LastStand)),
        ("返しなし", GaldWith(TraitId.LastStandPlain)),
        ("盾剣", GaldWith(TraitId.LastStandShield)),
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
        public int InN, InWins, InTimeouts, InDied, InMutual;
        public double InTurnSum, InRemainSum, InHpPct, InFoes, InDeathAfterSum, InTurnsSum;
        public double Sweep, Riposte, Dying, RiposteN, DyingN, Blocked, Skipped, InReaction, Parried, RefillBlocked, StockDropped;
        public double ScarTaken, ScarAtk;
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
        if (g.LastStandTurn > 0)
        {
            double inStage = g.DamageToEnemy - g.LastStandBaseDealt;
            a.Riposte += g.LastStandRiposteDealt; a.Dying += g.LastStandDyingDealt;
            a.Sweep += inStage - g.LastStandRiposteDealt - g.LastStandDyingDealt;
            a.RiposteN += g.LastStandRipostes; a.DyingN += g.LastStandDyingRipostes;
            a.Blocked += g.LastStandRiposteBlocked; a.Skipped += g.LastStandRiposteSkipped; a.InReaction += g.LastStandRiposteInReaction;
            a.Parried += g.LastStandParried; a.RefillBlocked += g.LastStandRefillBlocked; a.StockDropped += g.LastStandStockDropped;
            a.ScarTaken += g.LastStandScarTaken; a.ScarAtk += g.LastStandScarAtk;
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
            s.InN += a.InN; s.InWins += a.InWins; s.InTimeouts += a.InTimeouts; s.InDied += a.InDied; s.InMutual += a.InMutual;
            s.InTurnSum += a.InTurnSum; s.InRemainSum += a.InRemainSum; s.InHpPct += a.InHpPct; s.InFoes += a.InFoes;
            s.InDeathAfterSum += a.InDeathAfterSum; s.InTurnsSum += a.InTurnsSum;
            s.Sweep += a.Sweep; s.Riposte += a.Riposte; s.Dying += a.Dying; s.RiposteN += a.RiposteN; s.DyingN += a.DyingN;
            s.Blocked += a.Blocked; s.Skipped += a.Skipped; s.InReaction += a.InReaction; s.Parried += a.Parried;
            s.RefillBlocked += a.RefillBlocked; s.StockDropped += a.StockDropped;
            s.ScarTaken += a.ScarTaken; s.ScarAtk += a.ScarAtk; s.ScarAtkPos += a.ScarAtkPos;
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
        Console.WriteLine("# 第198期 `galdlast run` —— 版 × ガルドの在席行（`compare` 35 行・第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("版: **無**（剣の段なし＝第197期）／ **剣＋傷**（規定）／ 剣 ／ 返しなし（剣から斬り返しを外す）／ 盾剣（受け流しは今のまま・×1・単体）。"
                          + "`入った` ＝ 剣の段に入った戦（無の版は「最後の1体」になった戦）。");
        Console.WriteLine();
        Console.WriteLine("## 表A 行ごとの勝率（第2〜5波の平均・%）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 入った | " + string.Join(" | ", Versions.Select(v => v.Name)) + " | 剣＋傷 − 無 | 剣 − 無 | 返しなし − 無 | 盾剣 − 無 |");
        Console.WriteLine("|---|--:|" + string.Concat(Versions.Select(_ => "--:|")) + "--:|--:|--:|--:|");
        for (int i = 0; i < rows.Count; i++)
        {
            Acc b = acc[i, 0];
            Console.WriteLine("| " + rows[i].Name + " | " + Pc(acc[i, 1].InN, acc[i, 1].N) + " | "
                              + string.Join(" | ", Enumerable.Range(0, Versions.Length).Select(v => Pc(acc[i, v].Wins, acc[i, v].N))) + " | "
                              + string.Join(" | ", Enumerable.Range(1, Versions.Length - 1).Select(v => Dl(b.Wins, b.N, acc[i, v].Wins, acc[i, v].N))) + " |");
        }
        var tot = Enumerable.Range(0, Versions.Length).Select(v => Sum(Enumerable.Range(0, rows.Count).Select(i => acc[i, v]))).ToArray();
        Console.WriteLine("| **計** | " + Pc(tot[1].InN, tot[1].N) + " | " + string.Join(" | ", tot.Select(a => Pc(a.Wins, a.N))) + " | "
                          + string.Join(" | ", Enumerable.Range(1, Versions.Length - 1).Select(v => Dl(tot[0].Wins, tot[0].N, tot[v].Wins, tot[v].N))) + " |");
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
        Console.WriteLine("`入った後の残りT` ＝ 決着T − 入ったT。`倒れるまで` ＝ 入ってからガルドが倒れるまでのターン（倒れた戦だけ）。`相打ち` ＝ 倒れる一撃に斬り返した戦。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 入った戦 | 入ったT | HP | 敵の数 | 勝率 | 打ち切り | 倒れた | 相打ち | 残りT | 倒れるまで | 全体の打ち切り | 全体の決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int v = 0; v < Versions.Length; v++)
        {
            Acc a = tot[v];
            Console.WriteLine("| " + Versions[v].Name + " | " + a.InN + "（" + Pc(a.InN, a.N) + "%） | " + Avg(a.InTurnSum, a.InN, "F2") + " | "
                              + (a.InN == 0 ? "—" : (a.InHpPct / a.InN).ToString("F0") + "%") + " | " + Avg(a.InFoes, a.InN) + " | "
                              + Pc(a.InWins, a.InN) + " | " + Pc(a.InTimeouts, a.InN) + " | " + Pc(a.InDied, a.InN) + " | " + Pc(a.InMutual, a.InN) + " | "
                              + Avg(a.InRemainSum, a.InN, "F1") + " | " + Avg(a.InDeathAfterSum, a.InDied, "F1") + " | "
                              + Pc(a.Timeouts, a.N) + " | " + Avg(a.TurnsSum, a.N) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 表D 剣の段で与えたダメージ（入った戦1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("`手番` ＝ 剣の段の `DamageToEnemy` の増分 − 斬り返し − 相打ち（剣の版は薙ぎ・盾剣は単体）。`止められた` ＝ 粛・痺れ・組み付きで返せなかった被弾。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 手番 | 斬り返し | 相打ち | 斬り返しの割合 | 返した回数 | 相打ちの回数 | 止められた | 巻き込み等で返さず | 反撃の中 | 傷の累計 | 加えた攻撃力 | 加えた戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int v = 1; v < Versions.Length; v++)
        {
            Acc a = tot[v];
            double all = a.Sweep + a.Riposte + a.Dying;
            Console.WriteLine("| " + Versions[v].Name + " | " + Avg(a.Sweep, a.InN, "F1") + " | " + Avg(a.Riposte, a.InN, "F1") + " | " + Avg(a.Dying, a.InN, "F1") + " | "
                              + (all == 0 ? "—" : (100 * (a.Riposte + a.Dying) / all).ToString("F0") + "%") + " | "
                              + Avg(a.RiposteN, a.InN) + " | " + Avg(a.DyingN, a.InN) + " | " + Avg(a.Blocked, a.InN) + " | " + Avg(a.Skipped, a.InN) + " | "
                              + Avg(a.InReaction, a.InN) + " | " + Avg(a.ScarTaken, a.InN, "F1") + " | " + Avg(a.ScarAtk, a.InN) + " | " + Pc(a.ScarAtkPos, a.InN) + "% |");
        }
    }

    // =================================================================================
    // ledger —— 波ごと・行ごとの帳簿（剣＋傷と剣）
    // =================================================================================

    static void Ledger()
    {
        var rows = GaldRows().ToList();
        Console.WriteLine("# 第198期 `galdlast ledger` —— 剣の段の帳簿（剣＋傷・波ごと／行ごと）");
        Console.WriteLine();
        foreach (int vi in new[] { 1, 2 })
        {
            Console.WriteLine("## " + Versions[vi].Name + " —— 波ごと");
            Console.WriteLine();
            Console.WriteLine("| 波 | 入った戦 | 入ったT | 勝率 | 打ち切り | 残りT | 手番 | 斬り返し | 相打ち | 返した回数 | 止められた | 傷の累計 | 加えた攻撃力 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
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
                                  + Pc(a.InTimeouts, a.InN) + " | " + Avg(a.InRemainSum, a.InN, "F1") + " | " + Avg(a.Sweep, a.InN, "F1") + " | "
                                  + Avg(a.Riposte, a.InN, "F1") + " | " + Avg(a.Dying, a.InN, "F1") + " | " + Avg(a.RiposteN, a.InN) + " | "
                                  + Avg(a.Blocked, a.InN) + " | " + Avg(a.ScarTaken, a.InN, "F1") + " | " + Avg(a.ScarAtk, a.InN) + " |");
            }
            Console.WriteLine();
        }
        Console.WriteLine("## 剣＋傷 —— 行ごと（入った戦だけ）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 入った | 無の勝率(入った戦) | 剣＋傷の勝率 | 無の打ち切り | 剣＋傷の打ち切り | 残りT 無 → 剣＋傷 | 傷の累計 | 加えた攻撃力 | 斬り返しの割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        Acc[,] acc = MeasureAll(rows);
        for (int i = 0; i < rows.Count; i++)
        {
            Acc b = acc[i, 0], a = acc[i, 1];
            double all = a.Sweep + a.Riposte + a.Dying;
            Console.WriteLine("| " + rows[i].Name + " | " + Pc(a.InN, a.N) + "% | " + Pc(b.InWins, b.InN) + " | " + Pc(a.InWins, a.InN) + " | "
                              + Pc(b.InTimeouts, b.InN) + " | " + Pc(a.InTimeouts, a.InN) + " | "
                              + Avg(b.InRemainSum, b.InN, "F1") + " → " + Avg(a.InRemainSum, a.InN, "F1") + " | "
                              + Avg(a.ScarTaken, a.InN, "F1") + " | " + Avg(a.ScarAtk, a.InN) + " | "
                              + (all == 0 ? "—" : (100 * (a.Riposte + a.Dying) / all).ToString("F0") + "%") + " |");
        }
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string balancePath)
    {
        if (string.IsNullOrWhiteSpace(balancePath)) balancePath = Path.Combine("docs", "balance.md");
        Console.WriteLine("# 第198期 `galdlast check` —— 自己検査");
        Console.WriteLine();
        bool ok = true;

        // (a) 無の版（全 61 行のガルドを剣の段なしへ）が採用前の balance.md と一致 ／ ガルドを含まない行は規定でも一致
        var before = ParseBalance(balancePath);
        int cells = 0, diffNo = 0, diffNonGald = 0, nonGaldCells = 0;
        UnitDef none = Versions[0].Def;
        foreach (var (name, f) in CompareBuilds())
        {
            if (!before.TryGetValue(name, out double[]? b)) { Console.WriteLine("- 行名が引けない: " + name); ok = false; continue; }
            bool hasGald = f.Occupied().Any(o => o.Def.Id == "gald");
            Formation f0 = WithGald(f, none);
            for (int st = 0; st < 5; st++)
            {
                int w0 = 0, w1 = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    if (BattleEngine.Run(f0, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) w0++;
                    if (!hasGald && BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) w1++;
                }
                cells++;
                if (Math.Abs(100.0 * w0 / Seeds - b[st]) > 0.05) diffNo++;
                if (!hasGald)
                {
                    nonGaldCells++;
                    if (Math.Abs(100.0 * w1 / Seeds - b[st]) > 0.05) diffNonGald++;
                }
            }
        }
        Console.WriteLine($"- (a) 無の版 × 61 行 × 5 波 = {cells} セル中、`{balancePath}` と違うセル: **{diffNo}**");
        Console.WriteLine($"- (a') ガルドを含まない行（規定）{nonGaldCells} セル中、違うセル: **{diffNonGald}**");
        if (diffNo != 0 || diffNonGald != 0) ok = false;

        // (b)(c) 剣の段に入る瞬間は版に依らない ／ 剣の版は入った後に受け流さない ／ 斬り返しは敵の攻撃にだけ
        int entryMismatch = 0, sword = 0, parriedAfter = 0, riposteBad = 0, riposteEv = 0, mutualEv = 0, drawEv = 0, drawBad = 0;
        foreach (var (_, f) in GaldRows())
        {
            Formation f0 = WithGald(f, none);
            for (int st = 1; st <= 4; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult r0 = BattleEngine.Run(f0, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    int alone = r0.TallyByUnit.TryGetValue("gald", out UnitTally? g0) ? g0.AloneTurn : 0;
                    for (int v = 1; v < Versions.Length; v++)
                    {
                        bool verbose = v == 1;
                        BattleResult r = BattleEngine.Run(WithGald(f, Versions[v].Def), EnemyCatalog.Stages[st].Enemy, seed, verbose: verbose);
                        UnitTally g = r.TallyByUnit["gald"];
                        if (g.LastStandTurn != alone) entryMismatch++;
                        if (g.LastStandTurn > 0 && v <= 3) { sword++; parriedAfter += (int)g.LastStandParried; }
                        if (!verbose) continue;
                        var ev = r.Events;
                        for (int k = 0; k < ev.Count; k++)
                        {
                            if (ev[k].Kind == BattleEventKind.LastStand) { drawEv++; if (g.LastStandTurn == 0) drawBad++; }
                            if (ev[k].Kind != BattleEventKind.LastStandRiposte) continue;
                            riposteEv++;
                            if (ev[k].Slot == 1) mutualEv++;
                            // 直前の、ガルドへの Damage を探す
                            BattleEvent? hit = null;
                            for (int j = k - 1; j >= 0; j--)
                                if (ev[j].Kind == BattleEventKind.Damage && ev[j].TargetId == ev[k].ActorId) { hit = ev[j]; break; }
                            if (hit is null || hit.ActorId != ev[k].TargetId || hit.FriendlyFire || hit.Relayed) riposteBad++;
                        }
                    }
                }
        }
        Console.WriteLine($"- (b) 剣の段に入ったターンが「無」の版の「最後の1体になったターン」と違う戦: **{entryMismatch}**（4 版 × 在席 35 行 × 第2〜5波 × seed 0..49）");
        Console.WriteLine($"- (c) 剣の版（剣＋傷・剣・返しなし）で剣の段に入った {sword} 戦のうち、入った後に受け流した回数: **{parriedAfter}**");
        Console.WriteLine($"- (c') 台本（剣＋傷）: 剣の段の印 {drawEv} 件（帳簿と食い違い {drawBad}）・斬り返し {riposteEv} 件（うち相打ち {mutualEv}）。"
                          + $"直前のガルドへの `Damage` が「その敵の・巻き込みでも中継でもない一撃」でない斬り返し: **{riposteBad}**");
        if (entryMismatch != 0 || parriedAfter != 0 || riposteBad != 0 || drawBad != 0) ok = false;

        // (d) 会戦で次の戦に持ち越さない（最後の1体のまま次の戦に入ったら開戦時に抜き直す）
        int nextBattles = 0, nextDrawnAtStart = 0, nextStartAlone = 0, nextParriedBefore = 0;
        foreach (var (_, f) in GaldRows())
            for (int seed = 0; seed < 50; seed++)
            {
                EngagementResult e = EngagementEngine.Run(new[] { f }, EnemyCatalog.EngagementColumn, seed, verbose: false);
                for (int b = 1; b < e.Battles.Count; b++)
                {
                    if (!e.Battles[b - 1].TallyByUnit.TryGetValue("gald", out UnitTally? prev) || prev.LastStandTurn == 0 || prev.Deaths > 0) continue;
                    if (!e.Battles[b].TallyByUnit.TryGetValue("gald", out UnitTally? cur)) continue;
                    nextBattles++;
                    if (cur.LastStandTurn == 1) nextDrawnAtStart++;
                    if (e.PlayerEntries.Count > b && e.PlayerEntries[b].Alive == 1) nextStartAlone++;
                    if (cur.ParryFires > 0) nextParriedBefore++;
                }
            }
        Console.WriteLine($"- (d) 会戦（`EngagementColumn`・在席 35 行 × seed 0..49）: 前の戦で剣を抜いて生き残ったガルドの次の戦 {nextBattles} 戦。"
                          + $"そのうち第1ターンに抜いていた戦 {nextDrawnAtStart}（最後の1体のまま入った戦 {nextStartAlone}）、受け流しが働いた戦 {nextParriedBefore}");

        // (e) 乱数: 新しい処理は PickOne / Roll を呼ばない（ソースの走査）
        string src = File.ReadAllText(Path.Combine("BattleCore", "Traits.cs"));
        int a0 = src.IndexOf("public class LastStandTrait", StringComparison.Ordinal);
        int a1 = src.IndexOf("public sealed class LastStandShieldTrait", StringComparison.Ordinal);
        string body = a0 >= 0 && a1 > a0 ? src.Substring(a0, a1 - a0) : "";
        if (body.Length == 0) { Console.WriteLine("- (e) 走査が空（止める）"); ok = false; }
        else
        {
            bool rng = body.Contains("PickOne" + "(") || body.Contains(".Roll" + "(") || body.Contains("Shuffled" + "(");
            Console.WriteLine($"- (e) `LastStandTrait` の本文に乱数の呼び出し: **{(rng ? "あり" : "なし")}**");
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
