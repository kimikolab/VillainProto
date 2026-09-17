using BattleCore;
using static Common;

// =====================================================================================
// scale モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "scale")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 scale
// =====================================================================================

static class ScaleDiag
{
// scale モード: アーマー（`StatusKeys.Armor`）に初めての読み手を作る（第47期）。
//
// **出力は docs/ に置かない**（標準出力で読むだけ）。
// `CompareBuilds()` / `Stages` / `Columns` は触らない。
//
//     dotnet run --project BattleSim -c Release 0 scale [絞り込み]
//     dotnet run --project BattleSim -c Release 0 scale phase0   # 実装前の地図（§2 Phase 0）
public static void Run(string[] args, int stageIndex)
{
    var scBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> scStages = EnemyCatalog.Stages;
    const int ScSeeds = 200;   // compare / spread / shove / dull / relay / slander / overbear と揃える
    const int ScMain = 1;      // 主表に使う CostPerAttack（探索段階の初期値）
    int[] scCosts = { 0, 1, 2 };

    string scMode = args.Length > 2 ? args[2] : "";

    // --- Phase 0（§2）: 実装の前に走らせる地図 ------------------------------------------------
    // **盤面を1つも動かさない純粋な記録。** 鱗の実装とは独立で、`ScaleRule` にも触れない。
    if (scMode == "phase0")
    {
        Console.WriteLine("# 鱗 Phase 0 —— 供給の地図（第47期 §2）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 scale phase0` の出力。seed 0..{ScSeeds - 1}。");
        Console.WriteLine("**盤面は1つも動かない。** 実装の前に走らせる測定で、`docs/` には置かない。");
        Console.WriteLine();

        // 0-3. 敵の範囲攻撃を波ごとに数える。
        // **砕け（ShatterTrait）は `source.CurrentPattern != Single` で発火する**ので、
        // 薙ぎ・全体だけでなく**貫きも破片を生む**。ここを単体/範囲の2値で数えると
        // ヒビの供給源を1波ぶん数え落とす。
        Console.WriteLine("## 0-3. 敵の範囲攻撃（＝砕けの供給源）を波ごとに数える");
        Console.WriteLine();
        Console.WriteLine("`ShatterTrait.OnDamaged` は `source.CurrentPattern == AttackPattern.Single` で早期 return するので、");
        Console.WriteLine("**薙ぎ・全体・貫きの3つすべてが破片を生む**（毒・燃焼は `source` が null なので外れる）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 範囲持ちの敵（席・型・溜め） | 範囲枚数 / 5 |");
        Console.WriteLine("|---|---|--:|");
        for (int w = 0; w < scStages.Count; w++)
        {
            var names = new List<string>();
            int cnt = 0;
            foreach ((int slot, UnitDef d) in scStages[w].Enemy.Occupied())
            {
                if (d.Pattern == AttackPattern.Single) continue;
                cnt++;
                string label = d.Pattern switch
                {
                    AttackPattern.Sweep => "薙ぎ",
                    AttackPattern.Pierce => "貫き",
                    AttackPattern.All => "全体",
                    _ => "単体"
                };
                bool charge = d.Actions is not null
                    && d.Actions.Any(a => a.Kind == ActionKind.Charge);
                names.Add($"{FormationRules.SeatNames[slot]}:{d.Name}({label}{(charge ? "・溜め" : "")})");
            }
            Console.WriteLine($"| {scStages[w].Name} | {(names.Count == 0 ? "—" : string.Join(" / ", names))} | {cnt} |");
        }
        Console.WriteLine();

        // 0-6. 味方の攻撃型の分布。**ロスターに常時の貫きは1枚も無い**ことの確認。
        Console.WriteLine("## 0-6. 味方の攻撃型の分布（`Def.Pattern`）と `ModifyPattern` の既存実装");
        Console.WriteLine();
        var pat = new Dictionary<AttackPattern, List<string>>();
        foreach (UnitDef d in UnitCatalog.All)
        {
            if (!pat.TryGetValue(d.Pattern, out var l)) pat[d.Pattern] = l = new List<string>();
            l.Add(d.Name);
        }
        Console.WriteLine("| 型 | 枚数 | 駒 |");
        Console.WriteLine("|---|--:|---|");
        foreach (AttackPattern p in new[] { AttackPattern.Single, AttackPattern.Sweep,
                                            AttackPattern.Pierce, AttackPattern.All })
        {
            var l = pat.TryGetValue(p, out var x) ? x : new List<string>();
            Console.WriteLine($"| {p} | {l.Count} | {(l.Count == 0 ? "—" : (l.Count > 6 ? "（略）" : string.Join("・", l)))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"ロスターは {UnitCatalog.All.Count} 枚。**常時の貫きは 0 枚**で、");
        Console.WriteLine("貫きは `ModifyPattern` を通してしか立たない。");
        Console.WriteLine();

        // 0-4. 味方の1戦あたりの死亡数を波ごとに出す。**ウロの供給条件そのもの。**
        //
        // 蘇生（ヴェル）で戻ってから再度倒れると `Deaths` は2回数えるが、
        // **鱗の供給も `OnAllyDeath` が2回走る**ので、数え方は供給と揃っている。
        // 胞子（ムグ）の子は `Ephemeral` で、これも `OnAllyDeath` を通る（本文で扱う）。
        Console.WriteLine("## 0-4. 味方の死亡数（1戦あたり）を波ごとに出す");
        Console.WriteLine();
        Console.WriteLine("`UnitTally.Deaths` の味方側の合計 ÷ 試行数。**蘇生されて再度倒れると 2 と数える**");
        Console.WriteLine("（鱗の供給も `OnAllyDeath` が2回走るので、数え方は供給と揃っている）。");
        Console.WriteLine("`死軸` は リィカ・ムグ・ゾト・ヴェル・ラウ・ハギ のいずれかを含む行。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 死軸 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        string[] deathAxis = { "rica", "mug", "zoto", "vel", "rau", "hagi" };
        var deathRows = new List<(string Name, bool Axis, double[] D)>();
        foreach (var b in scBuilds)
        {
            var ids = b.F.Occupied().Select(o => o.Def.Id).ToHashSet();
            bool axis = deathAxis.Any(ids.Contains);
            var d = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++)
            {
                int deaths = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                {
                    var r = BattleEngine.Run(b.F, scStages[w].Enemy, seed, verbose: false);
                    foreach ((string id, UnitTally t) in r.TallyByUnit)
                        if (UnitCatalog.All.Any(u => u.Id == id)
                            || id == "spore")   // 胞子は味方側の増援
                            deaths += t.Deaths;
                }
                d[w] = (double)deaths / ScSeeds;
            }
            deathRows.Add((b.Name, axis, d));
            Console.WriteLine($"| {b.Name} | {(axis ? "**●**" : "")} "
                + string.Concat(d.Select(x => $"| {x:0.00} ")) + $"| {d.Average():0.00} |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (label, sel) in new (string, Func<(string, bool, double[]), bool>)[]
                 { ("全 48 行", _ => true), ("死軸", x => x.Item2), ("死軸でない", x => !x.Item2) })
        {
            var g = deathRows.Select(x => (x.Name, x.Axis, x.D)).Where(sel).ToList();
            var avg = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++) avg[w] = g.Average(x => x.Item3[w]);
            Console.WriteLine($"| {label} | {g.Count} "
                + string.Concat(avg.Select(x => $"| {x:0.00} ")) + $"| {avg.Average():0.00} |");
        }
        Console.WriteLine();
        return;
    }

    static bool ScHasUro(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Uro));

    var scTargets = scBuilds.Where(b => ScHasUro(b.F)).ToArray();
    if (scMode.Length > 0 && scMode != "sweep" && scMode != "seats")
        scTargets = scTargets.Where(b => scMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // ウロを外した同じ編成（4体版）。**第21期の飽和検査を兼ねる**
    // ——4体版と5体版が同じ値なら、その台では第5の駒が何であっても結果が変わらない。
    static Formation ScWithoutUro(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Uro)) g[slot] = d;
        return g;
    }

    // **素体の対照。** ウロと数値・型・速さが1つも違わず、特性だけを持たない駒。
    // **`ScaleRule(0)` は消費を止めるだけで供給も発揮も止めない**ので、
    // あれだけでは「鱗が効いたのか、ただ 70/9/7 の体が入ったのか」が割れない
    // ——第41期の「符号を測りたい効果は、その効果だけを 0 にできるノブと対にして作ること」の
    // 実装漏れをここで塞ぐ（規則ではなく駒の側で塞ぐ。`ScaleRule` にノブを増やさない）。
    // カタログには載せない（`gradient` / `aim` / `guard` と同じ、診断のローカルの def）。
    UnitDef ScPlainDef = new()
    {
        Id = "uro_plain", Name = "素体のウロ", MaxHp = UnitCatalog.Uro.MaxHp,
        Attack = UnitCatalog.Uro.Attack, Speed = UnitCatalog.Uro.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Uro.Pattern
    };
    Formation ScPlain(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Uro) ? ScPlainDef : d;
        return g;
    }

    // ウロの席だけを振った5変種。他の4枚は元の相対順のまま空いた席へ詰める。
    static Formation ScSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Uro))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Uro;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    // 1つの（編成 × 波 × 規則）についての計測。**盤面は触らない**——`BattleResult` の計数を読むだけ。
    (double Win, double GainDeath, double GainShard, double GainEph, double Worn, double First,
     double Never, double Swings, double Pierce, double Back, double BackDmg,
     double SpentAtk, double SpentHit, double Depleted, double Leftover, double FullSoak, double Turns)
    MeasureScale(Formation f, Formation enemy, ScaleRule rule)
    {
        double win = 0, gd = 0, gs = 0, ge = 0, worn = 0, alive = 0, first = 0, firstN = 0, never = 0;
        double sw = 0, pi = 0, bk = 0, bd = 0, sa = 0, sh = 0, dep = 0, left = 0, soak = 0, turns = 0;
        for (int seed = 0; seed < ScSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                                    null, null, null, null, null, null, null, null, null, null, rule);
            if (r.PlayerWon) win++;
            gd += r.ScaleGainDeath; gs += r.ScaleGainShatter; ge += r.ScaleGainEphemeral;
            worn += r.ScaleWornTurns; alive += r.ScaleAliveTurns;
            if (r.ScaleFirstTurn > 0) { first += r.ScaleFirstTurn; firstN++; } else never++;
            sw += r.ScaleSwings; pi += r.ScalePierceSwings;
            bk += r.ScaleBackHits; bd += r.ScaleBackDamage;
            sa += r.ScaleSpentAttack; sh += r.ScaleSpentHit;
            dep += r.ScaleDepleted; left += r.ScaleLeftover; soak += r.ScaleFullSoaks;
            turns += r.Turns;
        }
        double n = ScSeeds;
        return (win * 100 / n, gd / n, gs / n, ge / n, alive > 0 ? worn * 100 / alive : 0,
                firstN > 0 ? first / firstN : 0, never * 100 / n, sw / n, pi / n, bk / n, bd / n,
                sa / n, sh / n, dep / n, left / n, soak / n, turns / n);
    }

    // 全波を通した集計（機構の量は波で平均する。勝率だけは波ごとに残す）
    (double[] Wins, double GainDeath, double GainShard, double GainEph, double Worn, double First,
     double Never, double Swings, double Pierce, double Back, double BackDmg,
     double SpentAtk, double SpentHit, double Depleted, double Leftover, double FullSoak)
    ScAll(Formation f, ScaleRule rule)
    {
        var wins = new double[scStages.Count];
        double gd = 0, gs = 0, ge = 0, worn = 0, first = 0, firstN = 0, never = 0;
        double sw = 0, pi = 0, bk = 0, bd = 0, sa = 0, sh = 0, dep = 0, left = 0, soak = 0;
        for (int w = 0; w < scStages.Count; w++)
        {
            var z = MeasureScale(f, scStages[w].Enemy, rule);
            wins[w] = z.Win;
            gd += z.GainDeath; gs += z.GainShard; ge += z.GainEph; worn += z.Worn; never += z.Never;
            sw += z.Swings; pi += z.Pierce; bk += z.Back; bd += z.BackDmg;
            sa += z.SpentAtk; sh += z.SpentHit; dep += z.Depleted; left += z.Leftover; soak += z.FullSoak;
            if (z.First > 0) { first += z.First; firstN++; }
        }
        double m = scStages.Count;
        return (wins, gd / m, gs / m, ge / m, worn / m, firstN > 0 ? first / firstN : 0, never / m,
                sw / m, pi / m, bk / m, bd / m, sa / m, sh / m, dep / m, left / m, soak / m);
    }

    static string ScCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));

    Console.WriteLine("# 鱗（scale）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 scale [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{ScSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（供給源を変えた対）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 獲得 | 破片の獲得量/戦。**内訳は 味方の死 / 砕けの破片**（`(儚)` は死のうち胞子ぶんの内数） |");
    Console.WriteLine("| 纏い率 | `Armor > 0` だったターン数 ÷ **ウロが生きてターン頭を迎えた回数** |");
    Console.WriteLine("| 初纏い | 初めて破片を得たターン（得た試行だけの平均）。`未纏` が一度も纏わなかった試行の割合 |");
    Console.WriteLine("| 貫き | 貫きで振った回数/戦（`振り` が総振り回数） |");
    Console.WriteLine("| **後列到達** | **貫きが後列の敵に当たった回数**/戦と、そのとき振り下ろした量（減衰後） |");
    Console.WriteLine("| 支出・攻 / 支出・被 | 攻撃で消費した量/戦 ／ 被弾で吸われた量/戦（**二重支出**） |");
    Console.WriteLine("| 枯渇 / 死蔵 | `Armor` が 0 に戻った回数/戦 ／ 決着時に残っていた量/戦 |");
    Console.WriteLine("| 受切 | 破片が被弾を**受け切った**回数/戦（受け切ると `OnDamaged` が呼ばれない＝§7-1 の干渉） |");
    Console.WriteLine();
    Console.WriteLine("> **「貫き」と「後列到達」は別の列。** 貫いた回数は成果ではない");
    Console.WriteLine("> ——後列に敵がいなければ単体攻撃と同じである。");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）--------------------------------------------------------
    if (scMode != "sweep" && scMode != "seats")
    {
        Console.WriteLine("## 0. 検算 —— 差分は鱗だけに閉じているか（受け入れ基準2）");
        Console.WriteLine();
        var plain = scBuilds.Where(b => !ScHasUro(b.F)).ToArray();   // 現状は既存 48 行
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < scStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, scStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null,
                            new ScaleRule(0)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, scStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null,
                            new ScaleRule(9)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（ウロを含まない {plain.Length} 行が `ScaleRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {scStages.Count} 波・`CostPerAttack` 0 対 9）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  行を足す前に `compare` の全文で確認済み（**240 セル中 0 件**）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 ----------------------------------------------------------------------------
    if (scMode != "sweep" && scMode != "seats")
    {
        Console.WriteLine($"## 1. 主表（`CostPerAttack = {ScMain}` と 陽性対照2本）");
        Console.WriteLine();
        Console.WriteLine("`C0` = `ScaleRule(0)`（**消費しない＝維持型**）。**強度ではなく性質を切るノブ**なので、");
        Console.WriteLine("これは「弱くした版」ではなく「別の駒」。");
        Console.WriteLine("`素体` = ウロと**数値・型・速さが1つも違わず特性だけを持たない駒**に差し替えた版。");
        Console.WriteLine("**これが機構の帰属を取る唯一の窓口**——`C0` は消費を止めるだけで供給も発揮も止めない。");
        Console.WriteLine("`4体` = ウロを外した同じ編成（**第21期の飽和検査**も兼ねる）。");
        Console.WriteLine("`土台` = ウロの席に元の駒（死軸＝ヴェル / ヒビ台＝リィカ）が入っている既存行。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in scTargets)
        {
            var z = ScAll(b.F, new ScaleRule(ScMain));
            var z0 = ScAll(b.F, new ScaleRule(0));
            var four = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                    if (BattleEngine.Run(ScWithoutUro(b.F), scStages[w].Enemy, seed, false).PlayerWon) wins++;
                four[w] = wins * 100.0 / ScSeeds;
            }
            var plainW = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                    if (BattleEngine.Run(ScPlain(b.F), scStages[w].Enemy, seed, false).PlayerWon) wins++;
                plainW[w] = wins * 100.0 / ScSeeds;
            }
            Console.WriteLine($"| {b.Name} | **C{ScMain}** |{ScCells(z.Wins)} {z.Wins.Average():0.0}% |");
            Console.WriteLine($"| | C0（維持型） |{ScCells(z0.Wins)} {z0.Wins.Average():0.0}% |");
            Console.WriteLine($"| | 素体（特性なし・同数値） |{ScCells(plainW)} {plainW.Average():0.0}% |");
            Console.WriteLine($"| | 4体 |{ScCells(four)} {four.Average():0.0}% |");
            Console.Out.Flush();
        }
        // 土台（既存行）を同じ物差しで並べる
        foreach (string baseName in new[] { "死の連鎖 (リィカ軸)", "範囲耐性 (ヒビ×ボルグ)" })
        {
            var bb = scBuilds.FirstOrDefault(x => x.Name == baseName);
            if (bb.F is null) continue;
            var wv = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                    if (BattleEngine.Run(bb.F, scStages[w].Enemy, seed, false).PlayerWon) wins++;
                wv[w] = wins * 100.0 / ScSeeds;
            }
            Console.WriteLine($"| 土台: {baseName} | — |{ScCells(wv)} {wv.Average():0.0}% |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 機構の計数（`CostPerAttack` = {ScMain}・5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 獲得(死/破片) | (儚) | 纏い率 | 初纏い | 未纏 | 貫き/振り | **後列到達(回/量)** | 支出・攻 | 支出・被 | 枯渇 | 死蔵 | 受切 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in scTargets)
        {
            var z = ScAll(b.F, new ScaleRule(ScMain));
            Console.WriteLine($"| {b.Name} | {z.GainDeath:0.0} / {z.GainShard:0.0} | {z.GainEph:0.0} "
                + $"| {z.Worn:0.0}% | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                + $"| {z.Pierce:0.00} / {z.Swings:0.00} | {z.Back:0.00} / {z.BackDmg:0.0} "
                + $"| {z.SpentAtk:0.0} | {z.SpentHit:0.0} | {z.Depleted:0.00} | {z.Leftover:0.0} | {z.FullSoak:0.00} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 2. 波ごとの内訳（供給の時間分布がこの期の設計）------------------------------------
        Console.WriteLine($"## 2. 波ごとの内訳（`CostPerAttack = {ScMain}`）—— 供給の時間分布");
        Console.WriteLine();
        Console.WriteLine("**「初纏い」と「後列到達」を波ごとに読む。** 第一波は敵が3体（前1・前3・中央）で");
        Console.WriteLine("**後列が存在しない**ので、貫いても到達は構造的に 0 になる。");
        Console.WriteLine();
        foreach (var b in scTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 獲得(死/破片) | 纏い率 | 初纏い | 未纏 | 貫き/振り | 後列到達(回/量) | 到達率 | 支出・攻 | 支出・被 | 決着T |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < scStages.Count; w++)
            {
                var z = MeasureScale(b.F, scStages[w].Enemy, new ScaleRule(ScMain));
                Console.WriteLine($"| {scStages[w].Name} | {z.Win:0.0}% | {z.GainDeath:0.0} / {z.GainShard:0.0} "
                    + $"| {z.Worn:0.0}% | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                    + $"| {z.Pierce:0.00} / {z.Swings:0.00} | {z.Back:0.00} / {z.BackDmg:0.0} "
                    + $"| {(z.Pierce > 0 ? $"{z.Back * 100 / z.Pierce:0.0}%" : "—")} "
                    + $"| {z.SpentAtk:0.0} | {z.SpentHit:0.0} | {z.Turns:0.0} |");
                Console.Out.Flush();
            }
            Console.WriteLine();
        }
    }

    // --- 3. 掃引（受け入れ基準9）------------------------------------------------------------
    if (scMode.Length == 0 || scMode == "sweep")
    {
        Console.WriteLine("## 3. 掃引（`CostPerAttack` 0 / 1 / 2）");
        Console.WriteLine();
        Console.WriteLine("**`0` は維持型・`1` 以上は消費型**なので、このノブは強度ではなく**性質**を切っている（はず）。");
        Console.WriteLine("**`0` が明確に強いだけなら、切っているのは性質ではなく強度**（第41期と同じ結末）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | Cost | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 纏い率 | 貫き/振り | 後列到達 | 支出・攻 | 支出・被 | 枯渇 | 死蔵 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in scTargets)
            foreach (int c in scCosts)
            {
                var z = ScAll(b.F, new ScaleRule(c));
                Console.WriteLine($"| {b.Name} | {c} | **{z.Wins.Average():0.0}%** |{ScCells(z.Wins)} "
                    + $"{z.Worn:0.0}% | {z.Pierce:0.00} / {z.Swings:0.00} | {z.Back:0.00} "
                    + $"| {z.SpentAtk:0.0} | {z.SpentHit:0.0} | {z.Depleted:0.00} | {z.Leftover:0.0} |");
                Console.Out.Flush();
            }
        Console.WriteLine();
    }

    // --- 4. 席の分散（seats2 の写し・受け入れ基準10）-----------------------------------------
    if (scMode.Length == 0 || scMode == "seats")
    {
        Console.WriteLine("## 4. 席の分散（`seats2` の写し・受け入れ基準10）");
        Console.WriteLine();
        Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 + 最下位 を seed 0..199 で測り直し。");
        Console.WriteLine("**採否に使うのは1位の配置ではなく次数**（第45期の残件 D）。");
        Console.WriteLine("**鱗は隣接を1つも読まない**ので、第45期の対照（隣接も列も読まない駒＝最頻率 65%）が比較先。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（3値） | 最頻率 | 幅 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|");
        foreach (var b in scTargets)
        {
            var members = b.F.Occupied().Select(x => x.Def).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in SlotAssignments(members.Count))
            {
                var g = new Formation();
                for (int m = 0; m < members.Count; m++) g[assign[m]] = members[m];
                perms.Add(g);
            }
            var scan = new int[perms.Count];
            for (int i = 0; i < perms.Count; i++)
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in scStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));
            var pool = order.Take(20).Append(curIdx).Append(order[^1]).Distinct().ToList();

            double Avg(Formation f)
            {
                double avg = 0;
                foreach (EnemyCatalog.Stage st in scStages)
                {
                    int wins = 0;
                    for (int seed = 0; seed < ScSeeds; seed++)
                        if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    avg += wins * 100.0 / ScSeeds;
                }
                return avg / scStages.Count;
            }

            var verified = pool.Select(i => (Idx: i, Avg: Avg(perms[i]))).OrderByDescending(x => x.Avg).ToList();
            double width = verified[0].Avg - verified[^1].Avg;
            var top5 = verified.Take(5).ToList();

            foreach (UnitDef d in members)
            {
                int bestSlot = -1;
                foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                    if (ReferenceEquals(dd, d)) bestSlot = slot;
                // 第45期の3値（前角 / 中央 / 後角）。**2値（中央/角）だと行と列が混ざる**
                // ——鱗は隣接を読まないが貫きはレーンを走るので、効くとしたら列の側。
                int mid = 0, fcorner = 0, bcorner = 0;
                foreach (var v in top5)
                    foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                        if (ReferenceEquals(dd, d))
                        {
                            int deg2 = 0;
                            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                                if (FormationRules.AreAdjacent(slot, i)) deg2++;
                            if (deg2 == 4) mid++;
                            else if (FormationRules.RowOf(slot) == Row.Front) fcorner++;
                            else bcorner++;
                        }
                int bdeg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
                int top = Math.Max(mid, Math.Max(fcorner, bcorner));
                Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                    + $"| 前角{fcorner} / 中央{mid} / 後角{bcorner} | {top * 100 / 5}% | {width:0.0}pt |");
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine("### ウロ1枚だけを振った5変種（他の4枚は元の相対順のまま詰める）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ウロの席 | 次数 | 纏い率 | 初纏い | 貫き/振り | 後列到達 | 支出・攻 | 支出・被 | 平均勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in scTargets)
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                Formation g = ScSeat(b.F, seat);
                int deg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(seat, i)) deg++;
                var z = ScAll(g, new ScaleRule(ScMain));
                Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {deg} | {z.Worn:0.0}% "
                    + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Pierce:0.00} / {z.Swings:0.00} "
                    + $"| {z.Back:0.00} | {z.SpentAtk:0.0} | {z.SpentHit:0.0} | {z.Wins.Average():0.0}% |");
                Console.Out.Flush();
            }
        Console.WriteLine();
    }

    return;
}
}
