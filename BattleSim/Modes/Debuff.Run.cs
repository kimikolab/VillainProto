using BattleCore;
using static Common;

// =====================================================================================
// debuff モード（第189期）の本体 —— 版の対照・帳簿・診断台・自己検査
//
//     dotnet run --project BattleSim -c Release 0 debuff run      # 版の対照 × 3枚の在席行（compare ＋ 交差帯）・(G2)・勝ち方
//     dotnet run --project BattleSim -c Release 0 debuff ledger   # 帳簿（1戦あたり・第2〜5波）
//     dotnet run --project BattleSim -c Release 0 debuff bench    # 診断台3つ × 旧／新／マイナスなし／素体
//     dotnet run --project BattleSim -c Release 0 debuff check [採用前のbalance.md]  # 自己検査
//
// **旧の駒はこの診断のローカルに写してある**（`UnitCatalog` には新しい駒しか居ない）。
// =====================================================================================

static partial class DebuffDiag
{
    static partial void RunMoreImpl(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run": RunVersions(); handled = true; return;
            case "ledger": RunLedger(); handled = true; return;
            case "bench": RunBenches(); handled = true; return;
            case "check": Check(arg); handled = true; return;
            case "rows": Rows(); handled = true; return;
        }
    }

    // =================================================================================
    // 旧の駒・マイナスなしの駒（この診断のローカル。Id は本物と同じ＝帳簿を同じキーで引く）
    // =================================================================================

    static UnitDef Clone(UnitDef d, TraitId[] traits, IReadOnlyList<UnitAction>? actions, bool keepActions = true) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
        Advances = d.Advances, Actions = keepActions ? actions : null, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    /// <summary>旧ネル（第188期まで）: 開戦時の呪詛だけ・毎手番殴る。</summary>
    static readonly UnitDef NelOld = Clone(UnitCatalog.Nel, new[] { TraitId.Curse }, null, keepActions: false);
    /// <summary>旧クビ（第188期まで）: 萎縮（被ダメ −30% と味方全体の攻撃 −9）・毎手番殴る。</summary>
    static readonly UnitDef KubiOld = Clone(UnitCatalog.Kubi, new[] { TraitId.Cower }, null, keepActions: false);
    /// <summary>旧ハネ（第188期まで）: 突き返し（効果A＋隣のよろけ）・毎手番殴る。</summary>
    static readonly UnitDef HaneOld = Clone(UnitCatalog.Hane, new[] { TraitId.Shove }, null, keepActions: false);

    static readonly UnitDef NelYP = Clone(UnitCatalog.Nel, new[] { TraitId.Curse, TraitId.Hexer }, UnitCatalog.Nel.Actions);
    static readonly UnitDef KubiYP = Clone(UnitCatalog.Kubi, new[] { TraitId.Huddle, TraitId.Daunt }, UnitCatalog.Kubi.Actions);
    static readonly UnitDef HaneYP = Clone(UnitCatalog.Hane, new[] { TraitId.Rebound }, UnitCatalog.Hane.Actions);

    /// <summary>1枚ずつの版。(ネル, クビ, ハネ) をどの定義にするか。</summary>
    readonly record struct Ver(string Tag, UnitDef Nel, UnitDef Kubi, UnitDef Hane);

    static readonly Ver VOld = new("旧", NelOld, KubiOld, HaneOld);
    static readonly Ver VNew = new("新", UnitCatalog.Nel, UnitCatalog.Kubi, UnitCatalog.Hane);
    static readonly Ver[] Versions =
    {
        VOld,
        new("ネルだけ新", UnitCatalog.Nel, KubiOld, HaneOld),
        new("クビだけ新", NelOld, UnitCatalog.Kubi, HaneOld),
        new("ハネだけ新", NelOld, KubiOld, UnitCatalog.Hane),
        VNew,
        new("新・ネル漏れなし", NelYP, UnitCatalog.Kubi, UnitCatalog.Hane),
        new("新・クビ漏れなし", UnitCatalog.Nel, KubiYP, UnitCatalog.Hane),
        new("新・ハネ入れ替えなし", UnitCatalog.Nel, UnitCatalog.Kubi, HaneYP),
    };

    static Formation Apply(Formation f, Ver v)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = d.Id switch { "nel" => v.Nel, "kubi" => v.Kubi, "hane" => v.Hane, _ => d };
        return g;
    }

    static bool HasThree(Formation f) => f.Occupied().Any(o => Three.Contains(o.Def.Id));

    // =================================================================================
    // 勝率（seed 0..199・波ごと）。seed の並列は決定的（戦闘は独立）
    // =================================================================================

    static double[] Rates(Formation f, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            Parallel.For(0, seeds, seed =>
            {
                if (BattleEngine.Run(f, enemy, seed, verbose: false).PlayerWon) Interlocked.Increment(ref wins);
            });
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells(double[] w) => string.Join(" / ", w.Skip(1).Select(x => x.ToString("F1")));
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");

    static IEnumerable<(string Band, string Name, Formation F)> ThreeRows()
    {
        foreach (var (n, f) in CompareBuilds()) if (HasThree(f)) yield return ("compare", n, f);
        foreach (var (n, f) in CrossBuilds()) if (HasThree(f)) yield return ("交差帯", n, f);
    }

    // =================================================================================
    // run —— 版の対照
    // =================================================================================

    static void RunVersions()
    {
        Console.WriteLine("# 第189期 `debuff run` —— 版の対照（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("seed 0..199。平均は第2〜5波（規約 (G10)）。**旧** ＝ 第188期までの3枚（この診断のローカルに写した）。"
                          + "**マイナスなし** ＝ その駒の代金の札（`HexLeak` / `DauntLeak` / `Overrun`）だけを外した `yP`。"
                          + "**代金 ＝ 新 − マイナスなし**（新しいプラスの上で測る・R289）。");
        Console.WriteLine();

        Console.WriteLine("## 表A. 3枚の在席行 × 版（第2〜5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | " + string.Join(" | ", Versions.Select(v => v.Tag)) + " | 新 − 旧 | ネル代金 | クビ代金 | ハネ代金 |");
        Console.WriteLine("|---|---|" + string.Concat(Versions.Select(_ => "--:|")) + "--:|--:|--:|--:|");
        var perWave = new List<(string Name, double[] Old, double[] New)>();
        foreach (var (band, name, f) in ThreeRows())
        {
            var m = Versions.Select(v => Rates(Apply(f, v))).ToArray();
            double[] mean = m.Select(Mean25).ToArray();
            perWave.Add((name, m[0], m[4]));
            var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
            string cost(int yp, string id) => ids.Contains(id) ? D(mean[4] - mean[yp]) : "—";
            Console.WriteLine("| " + band + " | " + name + " | " + string.Join(" | ", mean.Select(x => x.ToString("F1")))
                              + " | **" + D(mean[4] - mean[0]) + "** | " + cost(5, "nel") + " | " + cost(6, "kubi") + " | " + cost(7, "hane") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表B. 波ごと（旧 → 新・第2 / 3 / 4 / 5 波）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 旧 | 新 | 最大の低下 |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var (name, o, n) in perWave)
            Console.WriteLine("| " + name + " | " + Cells(o) + " | " + Cells(n) + " | "
                              + D(Enumerable.Range(1, 4).Min(i => n[i] - o[i])) + " |");
        Console.WriteLine();

        G2();
        Chain();
        Totals();
    }

    /// <summary>(G2) の分解（61 行分母・報告のみ）。いずれかの波で −10pt 以上落ちた行の駒ごとに「他の行」の平均変化。</summary>
    static void G2()
    {
        Console.WriteLine("## 表C. (G2) の分解（`compare` 61 行分母・**報告のみ**）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var old = new Dictionary<string, double[]>();
        var neu = new Dictionary<string, double[]>();
        foreach (var (n, f) in rows)
        {
            neu[n] = Rates(f);
            old[n] = HasThree(f) ? Rates(Apply(f, VOld)) : neu[n];
        }
        foreach (var (n, f) in rows)
        {
            double worst = Enumerable.Range(1, 4).Min(i => neu[n][i] - old[n][i]);
            if (worst > -10.0) continue;
            Console.WriteLine("- **" + n + "**（最大の低下 " + D(worst) + "）");
            foreach (UnitDef d in f.Occupied().Select(o => o.Def).DistinctBy(d => d.Id))
            {
                var others = rows.Where(r => r.Name != n && r.F.Occupied().Any(o => o.Def.Id == d.Id)).ToList();
                if (others.Count == 0) { Console.WriteLine("  - " + d.Name + ": 他の行 0 行（分解が成立しない）"); continue; }
                double avg = others.Average(r => Mean25(neu[r.Name]) - Mean25(old[r.Name]));
                Console.WriteLine("  - " + d.Name + ": 他の行 " + others.Count + " 行・平均変化 " + D(avg)
                                  + (avg <= -3.0 ? " → **壊れ**" : Math.Abs(avg) < 3.0 ? "（制約）" : ""));
            }
        }
        Console.WriteLine();
    }

    /// <summary>`chain` と同じ定義の残存・全滅勝ち（ただし分母は第2〜5波）。</summary>
    static void Chain()
    {
        Console.WriteLine("## 表D. 勝ち方（旧 → 新・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`残存` は勝った試行の生存数、`全滅勝ち` は勝った試行のうち生存1体の割合、`決着T` は勝った試行のターン数。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 残存 旧 | 残存 新 | 全滅勝ち 旧 | 全滅勝ち 新 | 決着T 旧 | 決着T 新 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (_, name, f) in ThreeRows())
        {
            Qual o = QualOf(Apply(f, VOld)), n = QualOf(f);
            Console.WriteLine("| " + name + " | " + o.Surv.ToString("F2") + " | " + n.Surv.ToString("F2") + " | "
                              + o.Wipe.ToString("F1") + "% | " + n.Wipe.ToString("F1") + "% | " + o.WinT.ToString("F2") + " | " + n.WinT.ToString("F2") + " |");
        }
        Console.WriteLine();
    }

    readonly record struct Qual(double Surv, double Wipe, double WinT);

    static Qual QualOf(Formation f)
    {
        double surv = 0, wipe = 0, t = 0; int wins = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                if (!r.PlayerWon) continue;
                wins++; surv += r.PlayerSurvivors; t += r.Turns;
                if (r.PlayerSurvivors == 1) wipe++;
            }
        return wins == 0 ? new Qual(0, 0, 0) : new Qual(surv / wins, 100.0 * wipe / wins, t / wins);
    }

    /// <summary>全体の指標（61 行 × 波の平均・主判定19行の第五波・情報セル）。</summary>
    static void Totals()
    {
        Console.WriteLine("## 表E. 全体の指標（`compare` 61 行・旧 → 新）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var o = new List<double[]>(); var n = new List<double[]>();
        var primary = Baseline.PrimaryRows.ToHashSet();
        double po5 = 0, pn5 = 0; int pc = 0;
        var pO = new List<double[]>(); var pN = new List<double[]>();
        int infoO = 0, infoN = 0, hiO = 0, hiN = 0;
        foreach (var (name, f) in rows)
        {
            double[] nw = Rates(f);
            double[] ow = HasThree(f) ? Rates(Apply(f, VOld)) : nw;
            o.Add(ow); n.Add(nw);
            if (primary.Contains(name)) { po5 += ow[4]; pn5 += nw[4]; pc++; pO.Add(ow); pN.Add(nw); }
            for (int i = 1; i < 5; i++) { if (ow[i] > 0 && ow[i] < 100) infoO++; if (nw[i] > 0 && nw[i] < 100) infoN++; }
            if (ow[4] > 95) hiO++;
            if (nw[4] > 95) hiN++;
        }
        string waves(List<double[]> x) => string.Join(" / ", Enumerable.Range(0, 5).Select(i => x.Average(w => w[i]).ToString("F1")));
        Console.WriteLine("- 全61行の波平均: 旧 " + waves(o) + " → 新 " + waves(n));
        Console.WriteLine("- 主判定19行の波平均: 旧 " + waves(pO) + " → 新 " + waves(pN));
        Console.WriteLine("- 主判定19行の第五波: 旧 " + (po5 / pc).ToString("F1") + " → 新 " + (pn5 / pc).ToString("F1")
                          + "（歯止め 33.2%・行が引けた数 " + pc + "）");
        Console.WriteLine("- 情報セル（`0 < x < 100`・第2〜5波）: 旧 " + infoO + " → 新 " + infoN);
        Console.WriteLine("- 第五波 95% 超の行: 旧 " + hiO + " → 新 " + hiN);
        Console.WriteLine();
    }

    // =================================================================================
    // ledger —— 帳簿（1戦あたり・第2〜5波）
    // =================================================================================

    readonly record struct Led(
        double HexActs, double HexMarks, double CursedMax, double HexDull, double Share, double ShareToPlayer, double Leak,
        double DauntActs, double DauntFoes, double DauntAllies, double FoeSwings, double FoeCut, double AllySwings, double AllyCut,
        double Thrusts, double Staggers, double FoeStallStagger, double NoFront, double OvSwaps, double OvRefused, double OvNoAlly,
        double RdDisplaced, double RdDrifter, double RdSniper);

    static Led LedgerOf(Formation f, int stFrom = 1, int stTo = 4)
    {
        var mine = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        double[] a = new double[24]; int n = 0;
        for (int st = stFrom; st <= stTo; st++)
        {
            var foes = EnemyCatalog.Stages[st].Enemy.Occupied().Select(o => o.Def.Id).ToHashSet();
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                n++;
                a[4] += r.HexShareDamage; a[5] += r.HexShareDamageToPlayer;
                foreach (var (id, t) in r.TallyByUnit)
                {
                    if (id == "nel") { a[0] += t.HexerActs; a[1] += t.HexerMarks; a[2] += t.HexerCursedMax; a[3] += t.HexerDullTotal; a[6] += t.HexLeakTotal; }
                    if (id == "kubi") { a[7] += t.DauntActs; a[8] += t.DauntFoes; a[9] += t.DauntAllies; }
                    if (id == "hane")
                    {
                        a[14] += t.ReboundThrusts; a[15] += t.ReboundStaggers; a[17] += t.ReboundNoFront;
                        a[18] += t.OverrunSwaps; a[19] += t.OverrunRefused; a[20] += t.OverrunNoAlly;
                        a[21] += t.OverrunReaderDisplaced; a[22] += t.OverrunReaderDrifter; a[23] += t.OverrunReaderSniper;
                    }
                    if (mine.Contains(id)) { a[12] += t.DauntedSwings; a[13] += t.DauntedCut; }
                    else if (foes.Contains(id)) { a[10] += t.DauntedSwings; a[11] += t.DauntedCut; a[16] += t.StallStagger; }
                }
            }
        }
        double[] m = a.Select(x => x / n).ToArray();
        return new Led(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13],
                       m[14], m[15], m[16], m[17], m[18], m[19], m[20], m[21], m[22], m[23]);
    }

    static void RunLedger()
    {
        Console.WriteLine("# 第189期 `debuff ledger` —— 帳簿（1戦あたり・第2〜5波・seed 0..199）");
        Console.WriteLine();
        var rows = ThreeRows().ToList();
        var nelRows = rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "nel")).ToList();
        var kubiRows = rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "kubi")).ToList();
        var haneRows = rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "hane")).ToList();
        var benchRows = Benches.Select(b => ("診断台", b.Name, b.F)).ToList();

        Console.WriteLine("## ネル（呪いを広げる）");
        Console.WriteLine();
        Console.WriteLine("`呪い持ちの最大` は1戦の中の最大値の平均。`攻撃ダウン` は呪い持ちへ引いた名目の総量。"
                          + "`共有` は呪いの共有で渡った総量（**ムドの呪いの分も含む**・うち味方へ）。`漏れ` は隣の味方へ引いた名目の総量。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 手番 | 呪った | 呪い持ちの最大 | 攻撃ダウン | 共有（うち味方） | 漏れ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (_, name, f) in nelRows.Concat(benchRows.Where(b => b.Item3.Occupied().Any(o => o.Def.Id == "nel"))))
        {
            Led l = LedgerOf(f);
            Console.WriteLine("| " + name + " | " + l.HexActs.ToString("F2") + " | " + l.HexMarks.ToString("F2") + " | " + l.CursedMax.ToString("F2")
                              + " | " + l.HexDull.ToString("F1") + " | " + l.Share.ToString("F1") + "（" + l.ShareToPlayer.ToString("F1") + "） | " + l.Leak.ToString("F1") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## クビ（萎縮）");
        Console.WriteLine();
        Console.WriteLine("`萎縮させた敵` は新しく萎縮させた敵の延べ数。`防いだ` は萎縮した敵の一撃で削った打点の合計（名目。実際に入る量はこの後の段で決まる）、"
                          + "`失った` は萎縮した味方の一撃で削った打点の合計。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 手番 | 萎縮させた敵 | 隣の味方 | 敵の半減した一撃 | 防いだ | 味方の半減した一撃 | 失った |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (_, name, f) in kubiRows.Concat(benchRows.Where(b => b.Item3.Occupied().Any(o => o.Def.Id == "kubi"))))
        {
            Led l = LedgerOf(f);
            Console.WriteLine("| " + name + " | " + l.DauntActs.ToString("F2") + " | " + l.DauntFoes.ToString("F2") + " | " + l.DauntAllies.ToString("F2")
                              + " | " + l.FoeSwings.ToString("F2") + " | " + l.FoeCut.ToString("F1") + " | " + l.AllySwings.ToString("F2") + " | " + l.AllyCut.ToString("F1") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## ハネ（バネ）");
        Console.WriteLine();
        Console.WriteLine("`奪った手番` は敵が転倒で失った手番の合計（盤上で敵を転ばせるのはハネだけ）。"
                          + "`読み手` はハネの入れ替えの通知が届いた延べ数（ヨミが動かされた／シオに味方の移動が届いた／セロが後ろへ下がった）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 突き返し | 転ばせた | 奪った手番 | 前列に敵なし | 入れ替え | 空振り | 隣に味方なし | 読み手 ヨミ／シオ／セロ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (_, name, f) in haneRows.Concat(benchRows.Where(b => b.Item3.Occupied().Any(o => o.Def.Id == "hane"))))
        {
            Led l = LedgerOf(f);
            Console.WriteLine("| " + name + " | " + l.Thrusts.ToString("F2") + " | " + l.Staggers.ToString("F2") + " | " + l.FoeStallStagger.ToString("F2")
                              + " | " + l.NoFront.ToString("F2") + " | " + l.OvSwaps.ToString("F2") + " | " + l.OvRefused.ToString("F2") + " | " + l.OvNoAlly.ToString("F2")
                              + " | " + l.RdDisplaced.ToString("F2") + "／" + l.RdDrifter.ToString("F2") + "／" + l.RdSniper.ToString("F2") + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // bench —— 診断台（Phase 0 で測る前に固定した3つ・`Presets` には足さない）
    // =================================================================================

    static readonly (string Name, Formation F)[] Benches =
    {
        ("台N（ネル×ウツ×ムド）", Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Gald, center: UnitCatalog.Nel,
                                                  back1: UnitCatalog.Utsu, back3: UnitCatalog.Borg)),
        ("台K（クビ×毒）", Formation.Build(front1: UnitCatalog.Guza, front3: UnitCatalog.Rau, center: UnitCatalog.Kubi,
                                         back1: UnitCatalog.KataOld, back3: UnitCatalog.Sid)),
        ("台H（ハネ×移動）", Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Hane,
                                           back1: UnitCatalog.Yomi, back3: UnitCatalog.Shio)),
    };

    /// <summary>同じ数値・特性なしの素体（第69期からの器具）。`Actions` を持たないので毎手番殴る。</summary>
    static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Pattern = d.Pattern, Traits = Array.Empty<TraitId>()
    };

    static void RunBenches()
    {
        Console.WriteLine("# 第189期 `debuff bench` —— 診断台3つ × 旧／新／マイナスなし／素体（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("seed 0..199。**素体** ＝ 新しい駒と同じ数値・特性なし（毎手番殴る）。平均は第2〜5波。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | Δ（素体比） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in Benches)
        {
            UnitDef who = f.Occupied().Select(o => o.Def).First(d => Three.Contains(d.Id));
            UnitDef old = who.Id switch { "nel" => NelOld, "kubi" => KubiOld, _ => HaneOld };
            UnitDef yp = who.Id switch { "nel" => NelYP, "kubi" => KubiYP, _ => HaneYP };
            var vs = new (string Tag, UnitDef D)[] { ("素体", Plain(who)), ("旧", old), ("**新**", who), ("マイナスなし", yp) };
            double basis = 0; bool first = true;
            foreach (var (tag, d) in vs)
            {
                var g = new Formation();
                foreach ((int s, UnitDef x) in f.Occupied()) g[s] = x.Id == who.Id ? d : x;
                double[] w = Rates(g);
                if (first) basis = Mean25(w);
                Console.WriteLine("| " + (first ? name : "") + " | " + tag + " | " + string.Join(" | ", w.Select(x => x.ToString("F1")))
                                  + " | " + Mean25(w).ToString("F1") + " | " + (first ? "—" : D(Mean25(w) - basis)) + " |");
                first = false;
            }
        }
        Console.WriteLine();
        Console.WriteLine("**代金 ＝ 新 − マイナスなし**。帳簿は `debuff ledger` の診断台の行。");
        Console.WriteLine();
    }

    // =================================================================================
    // rows —— 表F（**事後に足した**・予測の判定には使わない）
    // =================================================================================

    /// <summary>
    /// 台K が床（全版 0.0%）に張り付いた（R146 の再発）ので、<b>床でない台</b>として足した（第188期の表E と同じ作法）。
    /// クビは「毒の書き手（グザ・スィド・ミオ・ラウ・ヴィオ）を含む `compare` 行」へ、
    /// ハネは「移動の読み手（ヨミ・シオ）を含む `compare` 行」へ、それぞれ<b>軸の駒以外</b>のうち
    /// 素体差し替えの帰属が最も低い1枚と替える。
    /// </summary>
    static void Rows()
    {
        Console.WriteLine("# 第189期 `debuff rows` —— 表F（**事後の追加・予測の判定に使わない**）");
        Console.WriteLine();
        Console.WriteLine("替える駒 ＝ その行で**軸の駒以外のうち**素体差し替えの帰属が最も低い1枚（第188期 `kata rows` と同じ規則）。"
                          + "勝率は第2〜5波の平均・seed 0..199。**Δ素体** ＝ 新 − 同じ席の素体（機構だけの値）／ **代金** ＝ 新 − マイナスなし。");
        Console.WriteLine();
        RowsFor("クビ × 毒の行", UnitCatalog.Kubi, KubiOld, KubiYP, new[] { "guza", "sid", "mio", "rau", "vio" });
        RowsFor("ハネ × 移動の行", UnitCatalog.Hane, HaneOld, HaneYP, new[] { "yomi", "shio" });
    }

    static void RowsFor(string title, UnitDef neu, UnitDef old, UnitDef yp, string[] axis)
    {
        Console.WriteLine("## " + title);
        Console.WriteLine();
        Console.WriteLine("| 行 | 替えた駒（帰属） | 元 | 素体 | 旧 | **新** | マイナスなし | Δ元 | **Δ素体** | 代金 | 新 − 旧 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var dm = new List<double>(); var dc = new List<double>(); var dn = new List<double>(); var dold = new List<double>();
        foreach (var (name, f) in CompareBuilds())
        {
            if (!f.Occupied().Any(o => axis.Contains(o.Def.Id))) continue;
            if (f.Occupied().Any(o => o.Def.Id == neu.Id)) continue;
            double baseW = Mean25(Rates(f));
            var cand = f.Occupied().Where(o => !axis.Contains(o.Def.Id) && !Three.Contains(o.Def.Id))
                                   .Select(o => (o.Def, A: baseW - Mean25(Rates(SwapTo(f, o.Def, Plain(o.Def)))))).ToList();
            if (cand.Count == 0) continue;
            var low = cand.OrderBy(x => x.A).First();
            double pl = Mean25(Rates(SwapTo(f, low.Def, Plain(neu))));
            double ol = Mean25(Rates(SwapTo(f, low.Def, old)));
            double nw = Mean25(Rates(SwapTo(f, low.Def, neu)));
            double y = Mean25(Rates(SwapTo(f, low.Def, yp)));
            dm.Add(nw - pl); dc.Add(nw - y); dn.Add(nw - baseW); dold.Add(nw - ol);
            Console.WriteLine("| " + name + " | " + low.Def.Name + "（" + D(low.A) + "） | " + baseW.ToString("F1") + " | " + pl.ToString("F1")
                              + " | " + ol.ToString("F1") + " | **" + nw.ToString("F1") + "** | " + y.ToString("F1") + " | " + D(nw - baseW)
                              + " | **" + D(nw - pl) + "** | " + D(nw - y) + " | " + D(nw - ol) + " |");
        }
        Console.WriteLine();
        if (dm.Count > 0)
            Console.WriteLine("- " + dm.Count + " 行 ／ Δ元 の平均 " + D(dn.Average()) + " ／ **Δ素体 の平均 " + D(dm.Average()) + "**"
                              + "（正 " + dm.Count(x => x > 0.05) + " ／ 負 " + dm.Count(x => x < -0.05) + "）／ 代金の平均 " + D(dc.Average())
                              + "（負 " + dc.Count(x => x < -0.05) + "）／ 新 − 旧 の平均 " + D(dold.Average()));
        Console.WriteLine();
    }

    static Formation SwapTo(Formation f, UnitDef from, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = ReferenceEquals(d, from) ? to : d;
        return g;
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第189期 `debuff check` —— 自己検査");
        Console.WriteLine();

        // (a)(b) compare のセル: 3枚を含まない行は採用前と一致／旧に戻すと採用前と一致
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        if (File.Exists(path))
        {
            var want = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(path))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                string[] c = line.Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length < 8) continue;
                want[c[1]] = c.Skip(2).Take(5).Select(x => double.Parse(x.TrimEnd('%'))).ToArray();
            }
            int diffA = 0, seenA = 0, diffB = 0, seenB = 0;
            foreach (var (name, f) in CompareBuilds())
            {
                if (!want.TryGetValue(name, out double[]? w)) { diffB += 5; continue; }
                double[] r = HasThree(f) ? Rates(Apply(f, VOld)) : Rates(f);
                for (int i = 0; i < 5; i++)
                {
                    bool bad = Math.Abs(r[i] - w[i]) > 0.001;
                    seenB++; if (bad) diffB++;
                    if (!HasThree(f)) { seenA++; if (bad) diffA++; }
                }
            }
            Console.WriteLine("- (a) 3枚を含まない行のセルが `" + path + "` とずれた数: **" + diffA + " / " + seenA + "**（0 が正）");
            Console.WriteLine("- (b) 3枚を旧に戻したときに `" + path + "` とずれたセル: **" + diffB + " / " + seenB + "**（0 が正）");
        }

        // (c) 乱数を引かない（新しい札の本体の走査）
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            int i = traits.IndexOf("class " + "HexerTrait"), j = traits.IndexOf("class " + "OverrunTrait");
            int k = j < 0 ? -1 : traits.IndexOf("/// <summary>", j);
            string b = i < 0 || j < 0 ? "" : traits.Substring(i, (k < 0 ? traits.Length : k) - i);
            string pick = "Pick" + "One", roll = "Roll" + "(", shuf = "Shuffl" + "ed";
            int Count(string x) => (b.Length - b.Replace(x, "").Length) / x.Length;
            Console.WriteLine("- (c) 新しい札の本体が `" + pick + "` / `Roll` / `" + shuf + "` を呼ぶ回数: **"
                              + (Count(pick) + Count(roll) + Count(shuf)) + "**（0 が正・走査した本体 " + b.Length + " 字。空なら止める）");
            string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
            int pe = (engine.Length - engine.Replace(pick + "(", "").Length) / (pick.Length + 1);
            int pt = (traits.Length - traits.Replace(pick + "(", "").Length) / (pick.Length + 1);
            Console.WriteLine("- (c') `" + pick + "(` の出現: engine " + pe + " ／ traits " + pt + "（採用前のコミットと比べる）");
        }

        // (d) 台本（verbose）に呪い・萎縮・転倒が載るか
        foreach (var (name, f) in Benches)
        {
            int gain = 0, stagger = 0, skill = 0;
            for (int seed = 0; seed < 5; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[1].Enemy, seed, verbose: true);
                skill += r.Events.Count(e => e.Kind == BattleEventKind.Skill);
                gain += r.Events.Count(e => e.Kind == BattleEventKind.StatusGain
                                          && (e.Text == StatusKeys.Curse || e.Text == StatusKeys.Daunted));
                stagger += r.Events.Count(e => e.Kind == BattleEventKind.Stagger);
            }
            Console.WriteLine("- (d) " + name + " × 第二波 × seed 0..4 の台本: `Skill` " + skill + " ／ 呪い・萎縮の `StatusGain` " + gain
                              + " ／ `Stagger` " + stagger);
        }

        // (e) 旧の札（Cower / Shove）の保持者は All に 0 枚
        int cower = UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Cower));
        int shove = UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Shove));
        Console.WriteLine("- (e) `All` の旧の札の保持者: `Cower` " + cower + " ／ `Shove` " + shove + "（0 が正）・`All` " + UnitCatalog.All.Count + " 枚");
        Console.WriteLine();
    }
}
