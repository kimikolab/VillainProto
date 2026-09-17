using BattleCore;
using static Common;

// =====================================================================================
// whet モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "whet")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 whet
// =====================================================================================

static class WhetDiag
{
// pace モード: **勝率以外の物差しで、波から情報を取り出せるか**（第54期・調査）。
//
// `compare` の5波のうち第一波は 56 行すべてが 100.0% で、情報セルが 0 のまま遊んでいる
// （第51期の実測では、3体のうちどれを空席にしても 56 行すべて 100.0% のまま）。
// しかし決着ターンは 2.0〜6.1 と3倍の幅で散っている——**勝敗という物差しでは何も
// 測れていないが、盤面では明確な差が出ている。**
//
// ここでやるのは物差しの比較だけで、**波は1体も触らない**（`Stages` 差分ゼロ）。
// 既存の `chain` / `pulse` も決着T・残存・被ダメを出しているが、どちらも
// **全ステージ通算**（か駒ごと）なので、第一波の値が通算の中に埋もれて取り出せない。
// この診断の要は「**波ごとに**、複数の物差しを、同じ実行の中で並べる」ことにある。
//
// 群の数の定義は第54期の指示書で測る前に固定した（design/PHASE54_PACE.md §1-1）:
//   A 帯（seed 0..199・compare と同じ）の値で昇順に並べ、位置 k に切れ目を入れるのは
//   **B 帯（seed 200..399・選定に使っていない）でも前半と後半が完全に分離する**ときだけ。
//   群 = 切れ目 + 1。隣接ペアだけの比較にすると1組の逆転で群が増減して不安定になる
//   （第45期の「1位は 28/48 行で入れ替わる」と同じ罠）。
//
// 強化の分布（第56期）。**窓口 `BattleContext.Whet` を通った量だけ**を経路別・受け手別に数える。
//
// 第42期に弱体は `ctx.Dull` へ統一されたが、強化側は15箇所が `AtkBonus` を直に叩いていた
// （第52期 Phase 0-1 の持ち越し）。窓口ができたので、**強化の総量と経路別の内訳が初めて測れる。**
// 窓口そのものは挙動不変（`compare` が 280 セル 0 件で一致）なので、**この診断がこの期の成果。**
//
// **通っているのは他者強化の6本だけ。** 自己強化の9本（怒り・庇う／殉教・墓守2本・処刑・棘・
// 澱み喰い・軋み・分かち）は直叩きのまま残してあるので、ここの「総量」は
// **`AtkBonus` の総流量ではなく「他者から受け取った量」**である。収支も同じ意味。
//
// **渡した量は成果ではない**（第42期以降の作法）。`死蔵`（受け取ったのに一度も振らなかった駒への
// 付与量）を必ず分けて出す。`逆しま` は**受け取ったことがそのまま害になった量**。
//
// **陽性対照**（§7）: 既存のノブで経路を1本ずつ殺し、`whet` がその欠落を検出することを確かめる
// ——**合否は「狙った経路が 0 になったか」の1本だけ。** ノブは計数ではなく盤面を切るので、
// 他の経路がわずかに動くのは故障ではない（決着の長さと生死が変わる）
// ——`ColossusRule(Regurgitate: false)` が吐き戻しを、`GoadRule(0)` が駆り立てを 0 にする。
// 「0 件でした」が検出器の故障と区別できないので、**新しいノブは足さず既存のノブでやる。**
//
//     dotnet run --project BattleSim -c Release 0 whet [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    var whBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> whStages = EnemyCatalog.Stages;
    const int WhSeeds = 200;   // compare / spread / dull と同じ帯

    string whFilter = args.Length > 2 ? args[2] : "";
    var whTargets = whBuilds
        .Where(b => whFilter.Length == 0 || whFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 駒名の引き当て。TallyByUnit は両陣営を Def.Id で混ぜて持つので、敵側の名前も要る
    // （弱体は呪詛・転嫁で敵にも載る）。召喚（胞子）は Occupied に出ないので、
    // 見つからなければ Id をそのまま出す（**握り潰さない**）。
    var whName = new Dictionary<string, string>();
    foreach (UnitDef d in UnitCatalog.All) whName[d.Id] = d.Name;
    var whEnemyIds = new HashSet<string>();
    foreach (EnemyCatalog.Stage st in whStages)
        foreach ((int _, UnitDef d) in st.Enemy.Occupied()) { whName[d.Id] = d.Name; whEnemyIds.Add(d.Id); }
    string WhLabel(string id) => whName.TryGetValue(id, out string? n) ? n : id;

    // 1戦ぶんを足し込む器。**すべて「延べ量 ÷ 戦数」で割って出す**（量/戦の作法）。
    var whRoute = new double[WhetRoutes.Count];
    double whTotal = 0, whDull = 0, whPerv = 0, whFlip = 0;
    double whHoard = 0;                                    // 死蔵(旧): 受け取ったが Attacks == 0 の駒への付与量
    double whHoardInt = 0;                                 // 参考: Interventions == 0 の駒への付与量
    double whHoardNew = 0;                                 // **死蔵(新)**: AttackReads == 0 の駒への付与量
    var whHoardNewWho = new Dictionary<string, double>();  // 新定義の内訳
    var whGot = new Dictionary<string, double>();          // 受け手ごとの Whetted
    var whLost = new Dictionary<string, double>();         // 受け手ごとの Dulled
    var whHoardWho = new Dictionary<string, double>();     // 死蔵の内訳
    var whRowRoute = new Dictionary<string, double[]>();   // 行ごとの経路別（供給源の地図）
    int whBattles = 0;

    var whSw = System.Diagnostics.Stopwatch.StartNew();
    foreach ((string whRowName, Formation whF) in whTargets)
    {
        var rowRoute = new double[WhetRoutes.Count];
        foreach (EnemyCatalog.Stage st in whStages)
        {
            for (int seed = 0; seed < WhSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(whF, st.Enemy, seed, verbose: false);
                whBattles++;

                whTotal += r.WhetTotal;
                whDull += r.DullTotal;
                whPerv += r.WhetToPerverse;
                whFlip += r.WhetPerverseFlips;
                for (int i = 0; i < WhetRoutes.Count; i++)
                {
                    whRoute[i] += r.WhetByRoute[i];
                    rowRoute[i] += r.WhetByRoute[i];
                }

                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (t.Whetted > 0)
                    {
                        whGot[id] = whGot.TryGetValue(id, out double g) ? g + t.Whetted : t.Whetted;
                        // 死蔵。**振った回数が 0 の駒**への付与量。反撃（棘）は Attacks を通らないので
                        // 「反応型」も死蔵に数えてしまう——内訳を出して読む側で切り分ける。
                        if (t.Attacks == 0)
                        {
                            whHoard += t.Whetted;
                            whHoardWho[id] = whHoardWho.TryGetValue(id, out double h) ? h + t.Whetted : t.Whetted;
                        }
                        // **死蔵の新定義（第64期）。** `Attacks` は `PerformAttack` を通った回数なので、
                        // 棘（カド）のように **`PerformAttack` を通らずに自分の `CurrentAttack` を
                        // 打点に変える駒**を「死蔵」に数えてしまう（第63期 §11-2 で符号を逆に読んだ）。
                        // `AttackReads` は攻撃力を出力に変換した回数そのものなので、そこが塞がる。
                        if (t.Interventions == 0) whHoardInt += t.Whetted;
                        if (t.AttackReads == 0)
                        {
                            whHoardNew += t.Whetted;
                            whHoardNewWho[id] = whHoardNewWho.TryGetValue(id, out double h2)
                                ? h2 + t.Whetted : t.Whetted;
                        }
                    }
                    if (t.Dulled > 0)
                        whLost[id] = whLost.TryGetValue(id, out double d) ? d + t.Dulled : t.Dulled;
                }
            }
        }
        whRowRoute[whRowName] = rowRoute;
    }
    whSw.Stop();

    double whN = Math.Max(1, whBattles);

    Console.WriteLine("# 強化の分布 —— 窓口 `Whet`（第56期）");
    Console.WriteLine();
    Console.WriteLine($"対象 **{whTargets.Length} 編成** × 全 {whStages.Count} 波 × seed 0..{WhSeeds - 1} = **{whBattles} 戦**"
                      + $"（{whSw.Elapsed.TotalSeconds:F1} 秒）。");
    Console.WriteLine();
    Console.WriteLine("**通っているのは他者強化の6経路だけ。** 自己強化の9本（怒り・庇う／殉教・墓守2本・処刑・");
    Console.WriteLine("棘・澱み喰い・軋み・分かち）は `AtkBonus` を直に叩いたまま残してあるので、");
    Console.WriteLine("下の「総量」は `AtkBonus` の総流量ではなく **「他者から受け取った量」** である。");
    Console.WriteLine();

    // ---- 1. 経路別 -------------------------------------------------------
    Console.WriteLine("## 1. 経路別の強化総量");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 量/戦 | 占有率 | 出た編成 |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int i = 0; i < WhetRoutes.Count; i++)
    {
        int rows = whRowRoute.Count(kv => kv.Value[i] > 0);
        Console.WriteLine($"| {WhetRoutes.Names[i]} | {whRoute[i] / whN:F2} | "
                          + $"{(whTotal > 0 ? whRoute[i] * 100.0 / whTotal : 0):F1}% | {rows} |");
    }
    Console.WriteLine($"| **合計** | **{whTotal / whN:F2}** | 100.0% | {whRowRoute.Count(kv => kv.Value.Sum() > 0)} |");
    Console.WriteLine();
    Console.WriteLine($"参考: 弱体総量（`Dull`）は **{whDull / whN:F2}** /戦。"
                      + $"強化 ÷ 弱体 = **{(whDull > 0 ? whTotal / whDull : 0):F2} 倍**。");
    Console.WriteLine();
    Console.WriteLine("> `その他` が 0 でないなら、札を付け忘れた `Whet` の呼び出しがある（受け入れ基準・指示書 §6-5）。");
    Console.WriteLine();

    // ---- 2. 受け手 -------------------------------------------------------
    Console.WriteLine("## 2. 受け手（強化を受け取った駒・上位10）");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 受けた強化/戦 | 占有率 | 陣営 |");
    Console.WriteLine("|---|--:|--:|:-:|");
    foreach ((string id, double v) in whGot.OrderByDescending(kv => kv.Value).Take(10))
        Console.WriteLine($"| {WhLabel(id)} | {v / whN:F2} | {(whTotal > 0 ? v * 100.0 / whTotal : 0):F1}% | "
                          + (whEnemyIds.Contains(id) ? "敵" : "味方") + " |");
    Console.WriteLine();
    Console.WriteLine($"受け手の延べ種類数 **{whGot.Count}**。");
    Console.WriteLine();

    // ---- 3. 収支 ---------------------------------------------------------
    Console.WriteLine("## 3. 収支（`Whet - Dull` の正味）");
    Console.WriteLine();
    Console.WriteLine("**他者から受け取った正味**であって `AtkBonus` の総収支ではない（自己強化9本は窓口を通らない）。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 強化/戦 | 弱体/戦 | 正味 | 陣営 |");
    Console.WriteLine("|---|--:|--:|--:|:-:|");
    var whAll = new HashSet<string>(whGot.Keys);
    whAll.UnionWith(whLost.Keys);
    foreach (string id in whAll
                 .OrderByDescending(x => Math.Abs((whGot.TryGetValue(x, out double a) ? a : 0)
                                                  - (whLost.TryGetValue(x, out double b) ? b : 0)))
                 .Take(15))
    {
        double g = whGot.TryGetValue(id, out double gv) ? gv : 0;
        double d = whLost.TryGetValue(id, out double dv) ? dv : 0;
        Console.WriteLine($"| {WhLabel(id)} | {g / whN:F2} | {d / whN:F2} | **{(g - d) / whN:+0.00;-0.00;0.00}** | "
                          + (whEnemyIds.Contains(id) ? "敵" : "味方") + " |");
    }
    Console.WriteLine();

    // ---- 4. 死蔵 ---------------------------------------------------------
    Console.WriteLine("## 4. 死蔵（配った強化が出力にならなかった量）");
    Console.WriteLine();
    Console.WriteLine("**第64期に定義を直した**（第63期 §11-2）。3つの数え方を併記する。");
    Console.WriteLine();
    Console.WriteLine("| 定義 | 数え方 | 死蔵量/戦 | 強化総量に占める割合 |");
    Console.WriteLine("|---|---|--:|--:|");
    Console.WriteLine($"| 旧（第56期） | `Attacks == 0`（`PerformAttack` を通らなかった） | {whHoard / whN:F2} | "
                      + $"**{(whTotal > 0 ? whHoard * 100.0 / whTotal : 0):F1}%** |");
    Console.WriteLine($"| 参考 | `Interventions == 0`（ダメージの出どころにならなかった） | {whHoardInt / whN:F2} | "
                      + $"{(whTotal > 0 ? whHoardInt * 100.0 / whTotal : 0):F1}% |");
    Console.WriteLine($"| **新（第64期）** | **`AttackReads == 0`（攻撃力を出力に1度も変換しなかった）** | "
                      + $"**{whHoardNew / whN:F2}** | **{(whTotal > 0 ? whHoardNew * 100.0 / whTotal : 0):F1}%** |");
    Console.WriteLine();
    Console.WriteLine("> **旧定義は広すぎ、`Interventions` は狭すぎる。** 棘（カド）は `PerformAttack` を1度も");
    Console.WriteLine("> 通らないが反撃量を自分の `CurrentAttack` で決めるので**強化は満額効く**（旧定義は死蔵に数える）。");
    Console.WriteLine("> 逆に破裂・生贄・吸いは**固定量**で攻撃力を1ビットも読まないのに `Interventions` は立つ。");
    Console.WriteLine("> **`AttackReads` は攻撃力を出力量に変換した回数そのもの**で、加算箇所は4つだけ");
    Console.WriteLine("> （`PerformAttack` / 棘 / 仇討ち / 責め苦の追撃）。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 死蔵(旧)/戦 | 死蔵(新)/戦 | その駒が受けた総量 | 新定義の割合 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    var whHoardAll = new HashSet<string>(whHoardWho.Keys);
    whHoardAll.UnionWith(whHoardNewWho.Keys);
    foreach (string id in whHoardAll
                 .OrderByDescending(x => Math.Max(whHoardWho.TryGetValue(x, out double a) ? a : 0,
                                                  whHoardNewWho.TryGetValue(x, out double b) ? b : 0))
                 .Take(12))
    {
        double o = whHoardWho.TryGetValue(id, out double ov) ? ov : 0;
        double nv = whHoardNewWho.TryGetValue(id, out double nvv) ? nvv : 0;
        double got = whGot.TryGetValue(id, out double g) ? g : 0;
        Console.WriteLine($"| {WhLabel(id)} | {o / whN:F2} | **{nv / whN:F2}** | {got / whN:F2} | "
                          + $"{(got > 0 ? nv * 100.0 / got : 0):F1}% |");
    }
    Console.WriteLine();

    // ---- 5. 逆しま -------------------------------------------------------
    Console.WriteLine("## 5. 逆しま（強化が害になった量）");
    Console.WriteLine();
    Console.WriteLine($"ウツが受けた強化 **{whPerv / whN:F2}** /戦 ＝ 強化総量の **{(whTotal > 0 ? whPerv * 100.0 / whTotal : 0):F1}%**。");
    Console.WriteLine($"そのうち **符号が正へ渡った（半減側へ落ちた）回数 {whFlip / whN:F3} 回/戦**。");
    Console.WriteLine();
    Console.WriteLine("> `AtkBonus` が正だと攻撃力が半減し、負だと下げ幅の3倍になる（`PerverseTrait`）。");
    Console.WriteLine("> **量ではなく符号が読まれる**ので、「落ちた回数」のほうが本体。");
    Console.WriteLine();

    // ---- 6. 行ごとの供給源 -----------------------------------------------
    Console.WriteLine("## 6. 行ごとの供給源（強化を持つ編成だけ）");
    Console.WriteLine();
    Console.WriteLine("| 編成 | " + string.Join(" | ", WhetRoutes.Names.Skip(1)) + " | 合計/戦 |");
    Console.WriteLine("|---" + string.Concat(Enumerable.Repeat("|--:", WhetRoutes.Count)) + "|");
    double whPerRow = whStages.Count * WhSeeds;
    foreach ((string rn, double[] rr) in whRowRoute.Where(kv => kv.Value.Sum() > 0)
                                                   .OrderByDescending(kv => kv.Value.Sum()))
        Console.WriteLine($"| {rn} | "
                          + string.Join(" | ", rr.Skip(1).Select(v => $"{v / whPerRow:F1}"))
                          + $" | **{rr.Sum() / whPerRow:F1}** |");
    Console.WriteLine();
    Console.WriteLine($"強化を1点でも通す行は **{whRowRoute.Count(kv => kv.Value.Sum() > 0)} / {whTargets.Length}**。");
    Console.WriteLine();

    // ---- 7. 陽性対照 -----------------------------------------------------
    // **「0 件でした」は検出器の故障と区別が付かない**（第37期の作法）。
    // 既存のノブで経路を1本ずつ殺し、その経路だけが 0 になり他が動かないことを確かめる。
    // **新しいノブは足さない。**
    Console.WriteLine("## 7. 陽性対照（経路を1本ずつ窓口から外す）");
    Console.WriteLine();
    Console.WriteLine("既存のノブで経路を殺し、**狙った経路が 0 になる**ことを確かめる（合否はこの1本）。");
    Console.WriteLine("`ColossusRule(Regurgitate: false)` が吐き戻しを、`GoadRule(0)` が駆り立てを 0 にする。");
    Console.WriteLine();

    double[] WhMeasure((string Name, Formation F)[] rows, ColossusRule? col, GoadRule? goad)
    {
        var acc = new double[WhetRoutes.Count];
        int n = 0;
        foreach ((string _, Formation f) in rows)
            foreach (EnemyCatalog.Stage st in whStages)
                for (int seed = 0; seed < WhSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, st.Enemy, seed, verbose: false,
                                                      colossus: col, goad: goad);
                    n++;
                    for (int i = 0; i < WhetRoutes.Count; i++) acc[i] += r.WhetByRoute[i];
                }
        for (int i = 0; i < WhetRoutes.Count; i++) acc[i] /= Math.Max(1, n);
        return acc;
    }

    var whProbes = new (string Label, int Route, TraitId Need, ColossusRule? Col, GoadRule? Goad)[]
    {
        ("吐き戻しを切る (`Regurgitate: false`)", (int)WhetRoute.Regurgitate, TraitId.Colossus,
         ColossusRule.Default with { Regurgitate = false }, null),
        ("駆り立てを切る (`GoadRule(0)`)", (int)WhetRoute.Goad, TraitId.Goad,
         null, new GoadRule(0)),
    };

    foreach ((string label, int route, TraitId need, ColossusRule? col, GoadRule? goad) in whProbes)
    {
        // 対照の台は「その経路を実際に持っている行」だけに絞る（持たない行を混ぜると分母で薄まる）。
        var rows = whTargets.Where(t => t.F.Occupied().Any(o => o.Item2.Traits.Contains(need))).ToArray();
        Console.WriteLine($"### {label}");
        Console.WriteLine();
        if (rows.Length == 0)
        {
            Console.WriteLine("**対象の行が絞り込みに入っていない**（対照が立たない）。絞り込みを外して回すこと。");
            Console.WriteLine();
            continue;
        }
        double[] before = WhMeasure(rows, null, null);
        double[] after = WhMeasure(rows, col, goad);

        Console.WriteLine($"対象 {rows.Length} 行。");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 切る前 | 切った後 | 差 |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int i = 0; i < WhetRoutes.Count; i++)
        {
            if (before[i] == 0 && after[i] == 0) continue;
            Console.WriteLine($"| {WhetRoutes.Names[i]}{(i == route ? " ←狙い" : "")} | "
                              + $"{before[i]:F2} | {after[i]:F2} | {after[i] - before[i]:+0.00;-0.00;0.00} |");
        }
        // **合否は「狙った経路が 0 になったか」の1本だけ。**
        // 他の経路のずれは検出器の故障ではない——ノブは計数ではなく**盤面**を切るので
        // （`Regurgitate: false` は攻撃力が返らない盤面・`GoadRule(0)` は押し出しの無い盤面）、
        // 決着ターン数と生死が変わり、ターンあたりで走る他の経路の回数も動く。
        // **「他が動かないこと」を合格条件にすると、盤面を動かさないノブしか対照に使えなくなる。**
        bool killed = before[route] > 0 && after[route] == 0;
        double drift = Enumerable.Range(0, WhetRoutes.Count)
                                 .Where(i => i != route)
                                 .Sum(i => Math.Abs(after[i] - before[i]));
        Console.WriteLine();
        Console.WriteLine($"狙った経路: {before[route]:F2} → **{after[route]:F2}**（切る前が 0 でないこと・切った後が 0 であること）");
        Console.WriteLine($"→ 検出器は **{(killed ? "機能している" : "疑わしい（読む前に原因を潰すこと）")}**");
        Console.WriteLine();
        double rest = before.Sum() - before[route];
        double driftPct = rest > 0 ? drift * 100.0 / rest : 0;
        Console.WriteLine($"参考: 他経路の総ずれ **{drift:F2}**/戦（他経路の合計の {driftPct:F1}%）。"
                          + "**これは故障ではない**——ノブは計数ではなく盤面を切るので、");
        Console.WriteLine("決着の長さと生死が変わり、他の経路が走る回数も動く。合否は狙った経路の1本だけで読む。");
        Console.WriteLine();
    }

    return;
}
}
