using BattleCore;
using static Common;

// =====================================================================================
// favor モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "favor")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 favor
// =====================================================================================

static class FavorDiag
{
// 火選り（第58期）の診断。**Phase 0 は実装より先に走らせる**（指示書 §5-4）。
//
// 第57期が出した「燃焼は盤面のどこにも繋がっていない閉じた2枚組」（表E の 18 セルが全部 0）を受けて、
// **盤上に既にある「誰も読んでいない味方側の燃焼」（着火 味 2.40 対 敵 2.21）を
// 強化・弱体へ変換する駒**を作る期。
//
// **Phase 0-1（乗算監査）が最優先。** `UnitState.CurrentAttack` は `Def.Attack + AtkBonus` を
// 作ってから `ModifyAttack` を通すので、**熾火（ホタ）に配った強化1点は燃えている間 4 点になる。**
// ロスター唯一の乗算フラグで、「強化の配布経路を触るときに必ず監査する」が積み残しだった。
//
//     dotnet run --project BattleSim -c Release 0 favor phase0   # 実装前の地図（乗算監査・在庫・試験行）
public static void Phase0(string[] args, int stageIndex)
{
    var kpBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> kpStages = EnemyCatalog.Stages;
    const int KpSeeds = 200;   // compare / spread / whet / burn と同じ帯

    Console.WriteLine("# 火選りの地図（第58期 Phase 0）");
    Console.WriteLine();

    // ---- 0-1. 乗算監査 ---------------------------------------------------------------
    Console.WriteLine("## 0-1. 熾火の乗算監査（**最優先**・指示書 §1-1）");
    Console.WriteLine();
    Console.WriteLine("`UnitState.CurrentAttack` は `Def.Attack + AtkBonus` を作ってから");
    Console.WriteLine("`Trait.ModifyAttack` を通す。`PyreTrait.ModifyAttack` は燃えている間");
    Console.WriteLine($"`atk * {PyreTrait.Multiplier}` を返すので、**強化は素の攻撃力と一緒に掛けられる**。");
    Console.WriteLine();

    // (a) 盤面を1つも動かさない算術。UnitState を1つ作って CurrentAttack を読むだけ。
    static UnitState KpMake(UnitDef d, int bonus, bool burning)
    {
        var u = new UnitState
        {
            Def = d, Hp = d.MaxHp, MaxHp = d.MaxHp, TeamId = BattleContext.PlayerTeam,
            Traits = TraitCatalog.Resolve(d.Traits), AtkBonus = bonus
        };
        if (burning) u.SetCounter(StatusKeys.Burn, BurnRules.Turns);
        return u;
    }
    Console.WriteLine("### (a) 算術（戦闘0回）");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 素の攻 | AtkBonus | 燃えていない | 燃えている | 1点の実効（冷 / 熱） |");
    Console.WriteLine("|---|--:|--:|--:|--:|---|");
    foreach (UnitDef d in new[] { UnitCatalog.Hota, UnitCatalog.Mudo, UnitCatalog.Dolga })
    {
        int cold0 = KpMake(d, 0, false).CurrentAttack;
        int hot0 = KpMake(d, 0, true).CurrentAttack;
        foreach (int bonus in new[] { 0, 1, 2, 4 })
        {
            int cold = KpMake(d, bonus, false).CurrentAttack;
            int hot = KpMake(d, bonus, true).CurrentAttack;
            string eff = bonus == 0 ? "—"
                : $"{(cold - cold0) / (double)bonus:0.0} / **{(hot - hot0) / (double)bonus:0.0}**";
            Console.WriteLine($"| {d.Name} | {d.Attack} | +{bonus} | {cold} | {hot} | {eff} |");
        }
    }
    Console.WriteLine();
    Console.WriteLine($"**熾火だけが熱 {PyreTrait.Multiplier}.0。** 他の2枚は燃えていても 1.0（`ModifyAttack` を持たない）。");
    Console.WriteLine();

    // (b) 1戦のログ。StatSnapshot はターン頭の CurrentAttack を写す（BattleEngine.cs）。
    //     注入は Materialize → AtkBonus → UnitState 版 Run（第24期 `yield` と同じ経路）。
    //     InstanceId は ctx.Add が味方リストの順（= Materialize のスロット昇順）に 0 から振る。
    var kpRow = kpBuilds.First(b => b.Name.Contains("燃焼 (ボルグ×ホタ)"));
    Console.WriteLine("### (b) 1戦のログ（`燃焼 (ボルグ×ホタ)` × 第4波 × seed 0）");
    Console.WriteLine();
    Console.WriteLine("**ホタの `AtkBonus` に開戦時から +0 / +2 を注入した2版**を並べる。");
    Console.WriteLine("`CurrentAttack` はターン頭のスナップショット、`与ダメ` はそのターンにホタが通した量。");
    Console.WriteLine();
    Console.WriteLine("| 版 | ターン | ホタの `CurrentAttack` | 燃(残) | そのターンのホタの与ダメ |");
    Console.WriteLine("|---|--:|--:|:-:|--:|");
    foreach (int inject in new[] { 0, 2 })
    {
        var pl = BattleEngine.Materialize(kpRow.F, BattleContext.PlayerTeam);
        int hotaIid = pl.FindIndex(u => u.Def.Id == "hota");   // Add が 0 から順に振る
        foreach (UnitState u in pl.Where(u => u.Def.Id == "hota")) u.AtkBonus += inject;
        var fo = BattleEngine.Materialize(kpStages[3].Enemy, BattleContext.EnemyTeam);
        BattleResult rr = BattleEngine.Run(pl, fo, 0, verbose: true);
        for (int t = 1; t <= rr.Turns; t++)
        {
            int atk = -1, burn = 0, dmg = 0;
            foreach (BattleEvent e in rr.Events)
            {
                if (e.Turn != t) continue;
                if (e.Kind == BattleEventKind.StatSnapshot && e.TargetId == hotaIid) atk = e.Amount;
                if (e.Kind == BattleEventKind.StatusSnapshot && e.TargetId == hotaIid && e.Text == "燃") burn = e.Amount;
                if (e.Kind == BattleEventKind.Damage && e.ActorId == hotaIid && !e.FriendlyFire) dmg += e.Amount;
            }
            if (atk < 0) continue;
            Console.WriteLine($"| +{inject} | {t} | **{atk}** | {(burn > 0 ? burn.ToString() : "—")} | {dmg} |");
        }
    }
    Console.WriteLine();

    // (c) 全波の集計。ホタと非乗算の駒に同じ量を注入して、実効倍率を測る。
    Console.WriteLine("### (c) 全波の集計（`燃焼 (ボルグ×ホタ)` × 全5波 × seed 0..199）");
    Console.WriteLine();
    Console.WriteLine("**注入した1点が、その駒の与ダメを何点動かしたか。** 分母は注入量。");
    Console.WriteLine("`振り` は `PerformAttack` を通った回数（**貫きは1体貫くごとに25%減衰する**ので、");
    Console.WriteLine("与ダメの伸びは攻撃力の伸びより緩む）。");
    Console.WriteLine();
    Console.WriteLine("| 注入先 | 注入量 | その駒の与ダメ/戦 | 振り/戦 | 1点あたりの与ダメ増 | 平均勝率 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    foreach (string who in new[] { "hota", "mudo" })
    {
        double baseDmg = 0;
        foreach (int inject in new[] { 0, 1, 2, 4 })
        {
            double dmg = 0, atk = 0, win = 0;
            int n = 0;
            for (int w = 0; w < kpStages.Count; w++)
                for (int seed = 0; seed < KpSeeds; seed++)
                {
                    var pl = BattleEngine.Materialize(kpRow.F, BattleContext.PlayerTeam);
                    foreach (UnitState u in pl.Where(u => u.Def.Id == who)) u.AtkBonus += inject;
                    var fo = BattleEngine.Materialize(kpStages[w].Enemy, BattleContext.EnemyTeam);
                    BattleResult r = BattleEngine.Run(pl, fo, seed, verbose: false);
                    if (r.PlayerWon) win++;
                    if (r.TallyByUnit.TryGetValue(who, out UnitTally? t))
                    { dmg += t.DamageToEnemy; atk += t.Attacks; }
                    n++;
                }
            dmg /= n; atk /= n; win = win * 100.0 / n;
            if (inject == 0) baseDmg = dmg;
            string per = inject == 0 ? "—" : $"**{(dmg - baseDmg) / inject:+0.00;-0.00;0.00}**";
            Console.WriteLine($"| {(who == "hota" ? "熾のホタ（乗算あり）" : "泥人形ムド（乗算なし）")} | +{inject} "
                + $"| {dmg:0.0} | {atk:0.00} | {per} | {win:0.0}% |");
            Console.Out.Flush();
        }
    }
    Console.WriteLine();

    // ---- 0-2. 在庫 -------------------------------------------------------------------
    Console.WriteLine("## 0-2. 在庫");
    Console.WriteLine();
    Console.WriteLine($"- `CompareBuilds()` の行数: **{kpBuilds.Length}**（× {kpStages.Count} 波 = **{kpBuilds.Length * kpStages.Count} セル**）");
    Console.WriteLine($"- `UnitCatalog.All`: **{UnitCatalog.All.Count}** 体（上限 52 に対して残り **{52 - UnitCatalog.All.Count}**）");
    Console.WriteLine($"- `WhetRoutes.Count` = **{WhetRoutes.Count}** ／ `DullRoutes.Count` = **{DullRoutes.Count}**（末尾に足す）");
    int kpBorg = kpBuilds.Count(b => b.F.Occupied().Any(o => o.Def.Id == "borg"));
    Console.WriteLine($"- ボルグを含む行: **{kpBorg}**");
    Console.WriteLine();

    // ---- 0-3. 試験行の候補 -----------------------------------------------------------
    Console.WriteLine("## 0-3. 試験行の候補（ボルグを含む行 × 1枠を素体へ差し替え）");
    Console.WriteLine();
    Console.WriteLine("**`ablate` の値ではなく、その行がまだ動くか**を見る（第29期「台が死んでいる」の検査）。");
    Console.WriteLine("`素体5` = 5枠のうち1つを **HP70/攻5/速6・特性なし**（＝火選りの体）へ差し替えた版の平均勝率。");
    Console.WriteLine("**20% 台に潰れる席は、何を入れても測定にならない**（`reseat` の 120 通りが並ぶのと同じ合図）。");
    Console.WriteLine("火種（ボルグ）の枠は差し替えない。");
    Console.WriteLine();
    UnitDef KpBody = new()
    {
        Id = "hiyo_plain", Name = "素体（HP70/攻5/速6）",
        MaxHp = 70, Attack = 5, Speed = 6,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single
    };
    double[] KpWins(Formation f)
    {
        var v = new double[kpStages.Count];
        for (int w = 0; w < kpStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < KpSeeds; seed++)
                if (BattleEngine.Run(f, kpStages[w].Enemy, seed, false).PlayerWon) wins++;
            v[w] = wins * 100.0 / KpSeeds;
        }
        return v;
    }
    Console.WriteLine("| 行 | 現行 | 差し替えた枠 | 席 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 素体5 | 差 |");
    Console.WriteLine("|---|--:|---|:-:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var b in kpBuilds.Where(b => b.F.Occupied().Any(o => o.Def.Id == "borg")))
    {
        double cur = KpWins(b.F).Average();
        foreach ((int slot, UnitDef d) in b.F.Occupied().ToList())
        {
            if (d.Id == "borg") continue;   // 火種は残す
            var g = new Formation();
            foreach ((int s2, UnitDef d2) in b.F.Occupied()) g[s2] = s2 == slot ? KpBody : d2;
            double[] v = KpWins(g);
            Console.WriteLine($"| {b.Name} | {cur:0.0}% | {d.Name} | {FormationRules.SeatNames[slot]} "
                + string.Concat(v.Select(x => $"| {x:0.0}% "))
                + $"| **{v.Average():0.0}%** | {v.Average() - cur:+0.0;-0.0;0.0} |");
            Console.Out.Flush();
        }
    }
    Console.WriteLine();
    return;
}

// ---- ここから先は本体（phase0 は上で return 済み）---------------------------------------
public static void Run(string[] args, int stageIndex)
{
    var kdBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> kdStages = EnemyCatalog.Stages;
    const int KdSeeds = 200;   // compare / spread / whet / burn / goad と同じ帯
    FavorRule KdMain = FavorRule.Default;
    var kdSweep = new (int G, int L)[] { (0, 0), (2, 2), (4, 2), (2, 4), (4, 4) };

    string kdMode = args.Length > 2 ? args[2] : "";

    static bool KdHasHiyo(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hiyo));

    var kdTargets = kdBuilds.Where(b => KdHasHiyo(b.F)).ToArray();
    if (kdMode.Length > 0 && kdMode != "sweep" && kdMode != "confirm"
        && kdMode != "alt" && kdMode != "seats")
        kdTargets = kdTargets.Where(b => kdMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // **素体の対照。** ヒヨと数値・型・速さが1つも違わず、特性だけを持たない駒。
    // `FavorRule(0, 0)` でも機構は完全に止まる（Whet / Dull が amount <= 0 で即 return し、
    // FavorTrait は乱数を1つも引かない）ので、**この2つは1セルも違わないはず**——§0 で検算する。
    UnitDef KdPlainDef = new()
    {
        Id = "hiyo_plain", Name = "素体のヒヨ", MaxHp = UnitCatalog.Hiyo.MaxHp,
        Attack = UnitCatalog.Hiyo.Attack, Speed = UnitCatalog.Hiyo.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Hiyo.Pattern
    };
    // 火の粉を落としたボルグ（第57期の `BorgNoCinder` の写し）。**`Splash` は残す**
    // ——一緒に落とすと「巻き込みが消えたから勝った」と混ざる。Q2 の帰属はこれで取る。
    UnitDef KdBorgNoCinder = new()
    {
        Id = "borg_nc", Name = "ボルグ（火の粉なし）",
        MaxHp = 60, Attack = 18, Speed = 8,
        Traits = new[] { TraitId.Splash }, Pattern = AttackPattern.Sweep
    };
    Formation KdSwap(Formation f, UnitDef from, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, from) ? to : d;
        return g;
    }
    Formation KdPlain(Formation f) => KdSwap(f, UnitCatalog.Hiyo, KdPlainDef);
    Formation KdNoCinder(Formation f) => KdSwap(f, UnitCatalog.Borg, KdBorgNoCinder);
    // ヒヨを外した4体版。**第21期の飽和検査**も兼ねる。
    static Formation KdWithoutHiyo(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Hiyo)) g[slot] = d;
        return g;
    }
    // ヒヨの席だけを振った5変種（他の4枚は元の相対順のまま詰める）。
    static Formation KdSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Hiyo))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Hiyo;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    KdStat MeasureKd(Formation f, Formation enemy, FavorRule? rule)
    {
        var z = new KdStat();
        for (int seed = 0; seed < KdSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false, favor: rule);
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Fires += r.FavorFires; z.Idle += r.FavorIdle;
            z.Whetted += r.FavorWhetted; z.Dulled += r.FavorDulled;
            z.Given += r.FavorGiven; z.Taken += r.FavorTaken; z.ToPyre += r.FavorToPyre;
            z.WhetTotal += r.WhetTotal; z.DullTotal += r.DullTotal;
            foreach ((string k, int v) in r.FavorWhetTo)
                z.WhetTo[k] = z.WhetTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.FavorDullTo)
                z.DullTo[k] = z.DullTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string id, UnitTally t) in r.TallyByUnit)
            {
                if (!z.Dmg.ContainsKey(id))
                { z.Dmg[id] = 0; z.Taken2[id] = 0; z.Last[id] = 0; z.Atk[id] = 0; z.Got[id] = 0; }
                z.Dmg[id] += t.DamageToEnemy;
                z.Taken2[id] += t.DamageTaken;
                z.Last[id] += t.LastActiveTurn;
                z.Atk[id] += t.Attacks;
                z.Got[id] += t.Whetted;
            }
            // 死蔵: 強化を受けたのに一度も `PerformAttack` を通らなかった駒への付与量。
            foreach ((string id, UnitTally t) in r.TallyByUnit)
                if (t.Whetted > 0 && t.Attacks == 0) z.Hoard += t.Whetted;
        }
        double n = KdSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.Fires /= n; z.Idle /= n; z.Whetted /= n; z.Dulled /= n;
        z.Given /= n; z.Taken /= n; z.ToPyre /= n; z.Hoard /= n;
        z.WhetTotal /= n; z.DullTotal /= n;
        foreach (string k in z.WhetTo.Keys.ToList()) z.WhetTo[k] /= n;
        foreach (string k in z.DullTo.Keys.ToList()) z.DullTo[k] /= n;
        foreach (string k in z.Dmg.Keys.ToList())
        { z.Dmg[k] /= n; z.Taken2[k] /= n; z.Last[k] /= n; z.Atk[k] /= n; z.Got[k] /= n; }
        return z;
    }

    (double[] Wins, KdStat Z) KdAll(Formation f, FavorRule? rule)
    {
        var wins = new double[kdStages.Count];
        var acc = new KdStat();
        for (int w = 0; w < kdStages.Count; w++)
        {
            var z = MeasureKd(f, kdStages[w].Enemy, rule);
            wins[w] = z.Win;
            acc.Turns += z.Turns; acc.Fires += z.Fires; acc.Idle += z.Idle;
            acc.Whetted += z.Whetted; acc.Dulled += z.Dulled;
            acc.Given += z.Given; acc.Taken += z.Taken; acc.ToPyre += z.ToPyre; acc.Hoard += z.Hoard;
            acc.WhetTotal += z.WhetTotal; acc.DullTotal += z.DullTotal;
            foreach ((string k, double v) in z.WhetTo)
                acc.WhetTo[k] = acc.WhetTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.DullTo)
                acc.DullTo[k] = acc.DullTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.Dmg)
            {
                if (!acc.Dmg.ContainsKey(k))
                { acc.Dmg[k] = 0; acc.Taken2[k] = 0; acc.Last[k] = 0; acc.Atk[k] = 0; acc.Got[k] = 0; }
                acc.Dmg[k] += v; acc.Taken2[k] += z.Taken2[k];
                acc.Last[k] += z.Last[k]; acc.Atk[k] += z.Atk[k]; acc.Got[k] += z.Got[k];
            }
        }
        double m = kdStages.Count;
        acc.Win = wins.Average();
        acc.Turns /= m; acc.Fires /= m; acc.Idle /= m; acc.Whetted /= m; acc.Dulled /= m;
        acc.Given /= m; acc.Taken /= m; acc.ToPyre /= m; acc.Hoard /= m;
        acc.WhetTotal /= m; acc.DullTotal /= m;
        foreach (string k in acc.WhetTo.Keys.ToList()) acc.WhetTo[k] /= m;
        foreach (string k in acc.DullTo.Keys.ToList()) acc.DullTo[k] /= m;
        foreach (string k in acc.Dmg.Keys.ToList())
        { acc.Dmg[k] /= m; acc.Taken2[k] /= m; acc.Last[k] /= m; acc.Atk[k] /= m; acc.Got[k] /= m; }
        return (wins, acc);
    }

    double[] KdWins(Formation f, FavorRule? rule = null)
    {
        var v = new double[kdStages.Count];
        for (int w = 0; w < kdStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < KdSeeds; seed++)
                if (BattleEngine.Run(f, kdStages[w].Enemy, seed, false, favor: rule).PlayerWon) wins++;
            v[w] = wins * 100.0 / KdSeeds;
        }
        return v;
    }

    static string KdCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));
    static string KdTop(Dictionary<string, double> d, int n = 3)
    {
        var parts = d.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(n)
            .Select(x => $"{x.Key} {x.Value:0.00}").ToList();
        return parts.Count == 0 ? "—" : string.Join(" / ", parts);
    }
    static double KdGet(Dictionary<string, double> d, string k)
        => k.Length > 0 && d.TryGetValue(k, out double v) ? v : 0;

    Console.WriteLine("# 火選り（favor）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 favor [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{KdSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` は触っていない。`CompareBuilds()` には**3行足した**");
    Console.WriteLine("（火の粉の帰属の符号で分けた3種＝資産 / 代金 / ゼロ）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 発火 | 強化か弱体を1体でも配った手番の数/戦。**0 になっていないことが受け入れ基準** |");
    Console.WriteLine("| 空振り | 盤上に燃えている味方が1体もいなかった手番/戦。**第1ターンは構造的にここへ落ちる** |");
    Console.WriteLine("| 燃体/戦 | 強化した延べ体数（＝燃えている味方の延べ数）。**ノブでは動かない量** |");
    Console.WriteLine("| 非燃体/戦 | 鈍らせた延べ体数（＝隣接する非燃焼の味方の延べ数）。同上 |");
    Console.WriteLine("| 配った / 撒いた | 強化・弱体の**量**。**ノブで動く量**（切り分けは第49期の作法） |");
    Console.WriteLine("| **熾火へ** | 配った強化のうち熾火（乗算持ち）へ落ちた量。**Q4 の分子** |");
    Console.WriteLine("| **死蔵** | 強化を受けたのに `PerformAttack` を1回も通らなかった駒への付与量。**Q5** |");
    Console.WriteLine();

    // --- 0. 検算 ------------------------------------------------------------------------------
    if (kdMode.Length == 0)
    {
        Console.WriteLine("## 0. 検算");
        Console.WriteLine();
        var plain = kdBuilds.Where(b => !KdHasHiyo(b.F)).ToArray();
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < kdStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < KdSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, kdStages[w].Enemy, seed, false,
                            favor: new FavorRule(0, 0)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, kdStages[w].Enemy, seed, false,
                            favor: new FavorRule(9, 9)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（ヒヨを含まない {plain.Length} 行が `FavorRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {kdStages.Count} 波・`(0,0)` 対 `(9,9)`）");

        // **FavorRule(0,0) と素体が1セルも違わない**ことの検算（§2-1 の主張そのもの）。
        int c2 = 0, d2 = 0;
        foreach (var b in kdTargets)
            for (int w = 0; w < kdStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < KdSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, kdStages[w].Enemy, seed, false,
                            favor: new FavorRule(0, 0)).PlayerWon) a++;
                    if (BattleEngine.Run(KdPlain(b.F), kdStages[w].Enemy, seed, false).PlayerWon) c++;
                }
                c2++;
                if (a != c) d2++;
            }
        Console.WriteLine($"- **`FavorRule(0, 0)` が素体と一致**（機構は乱数を1つも引かない）: "
            + $"**{c2} セル中 {d2} 件の食い違い**（{kdTargets.Length} 行 × {kdStages.Count} 波）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  行を足す前に `compare` の全文で確認済み（**280 セル中 0 件・バイト単位で一致**）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 ------------------------------------------------------------------------------
    if (kdMode.Length == 0 || (kdMode != "sweep" && kdMode != "confirm"
                               && kdMode != "alt" && kdMode != "seats"))
    {
        Console.WriteLine($"## 1. 主表（`Gain = {KdMain.Gain}` / `Loss = {KdMain.Loss}` と対照）");
        Console.WriteLine();
        Console.WriteLine("`素体` = ヒヨと**数値・型・速さが1つも違わず特性だけを持たない駒**。");
        Console.WriteLine("**これが機構の帰属を取る唯一の窓口**（第47期 `ScaleRule(0)` の失敗を避ける形）。");
        Console.WriteLine("`鈍りなし` = `Loss = 0`（マイナスを止めた版）。**代金の分離。**");
        Console.WriteLine("`強化なし` = `Gain = 0`（プラスを止めた版）。**見返りの分離。**");
        Console.WriteLine("`4体` = ヒヨを外した4体版（**第21期の飽和検査**も兼ねる）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in kdTargets)
        {
            var (wins, _) = KdAll(b.F, KdMain);
            var nl = KdWins(b.F, new FavorRule(KdMain.Gain, 0));
            var ng = KdWins(b.F, new FavorRule(0, KdMain.Loss));
            var pw = KdWins(KdPlain(b.F));
            var fw = KdWins(KdWithoutHiyo(b.F));
            Console.WriteLine($"| {b.Name} | **G{KdMain.Gain}/L{KdMain.Loss}** |{KdCells(wins)} {wins.Average():0.0}% |");
            Console.WriteLine($"| | 鈍りなし（Loss = 0） |{KdCells(nl)} {nl.Average():0.0}% |");
            Console.WriteLine($"| | 強化なし（Gain = 0） |{KdCells(ng)} {ng.Average():0.0}% |");
            Console.WriteLine($"| | 素体（特性なし・同数値） |{KdCells(pw)} {pw.Average():0.0}% |");
            Console.WriteLine($"| | 4体（ヒヨ抜き） |{KdCells(fw)} {fw.Average():0.0}% |");
            Console.WriteLine($"| | **機構の帰属（G{KdMain.Gain}/L{KdMain.Loss} − 素体）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - pw[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - pw.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **代金（G{KdMain.Gain}/L{KdMain.Loss} − 鈍りなし）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - nl[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - nl.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 2. 機構の計数 --------------------------------------------------------------------
        Console.WriteLine("## 2. 機構の計数（`発火` が 0 でないことの確認・Q4 / Q5）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 発火 | 空振り | 燃体/戦 | 非燃体/戦 | 配った | 撒いた | **熾火へ** | 熾火の取り分 | **死蔵** | 死蔵率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in kdTargets)
        {
            var (_, z) = KdAll(b.F, KdMain);
            var (_, pz) = KdAll(KdPlain(b.F), KdMain);
            double hoard = z.Hoard - pz.Hoard;   // ヒヨが増やした死蔵ぶん
            Console.WriteLine($"| {b.Name} | {z.Fires:0.00} | {z.Idle:0.00} | {z.Whetted:0.00} | {z.Dulled:0.00} "
                + $"| {z.Given:0.0} | {z.Taken:0.0} | **{z.ToPyre:0.0}** "
                + $"| {(z.Given > 0 ? z.ToPyre * 100.0 / z.Given : 0):0.0}% "
                + $"| {z.Hoard:0.00} | {(z.WhetTotal > 0 ? z.Hoard * 100.0 / z.WhetTotal : 0):0.0}% |");
            Console.WriteLine($"| ↳ 素体（比較） | — | — | — | — | — | — | — | — | {pz.Hoard:0.00} "
                + $"| {(pz.WhetTotal > 0 ? pz.Hoard * 100.0 / pz.WhetTotal : 0):0.0}% |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine("### 受け手の内訳");
        Console.WriteLine();
        Console.WriteLine("| 行 | 強化の受け手（量/戦） | 弱体の受け手（量/戦） |");
        Console.WriteLine("|---|---|---|");
        foreach (var b in kdTargets)
        {
            var (_, z) = KdAll(b.F, KdMain);
            Console.WriteLine($"| {b.Name} | {KdTop(z.WhetTo, 4)} | {KdTop(z.DullTo, 4)} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 3. 火の粉の帰属の反転（Q2）-------------------------------------------------------
        Console.WriteLine("## 3. 火の粉の帰属（**Q2**・代金は資産に反転したか）");
        Console.WriteLine();
        Console.WriteLine("第57期と同じ取り方——**ボルグから `Cinder` だけを落とした版との差**");
        Console.WriteLine("（`Splash` は残す。一緒に落とすと「巻き込みが消えたから勝った」と混ざる）。");
        Console.WriteLine("**閾値は |帰属| ≥ 1.5pt**（第57期で確立。それ未満は別 seed 帯で符号が反転する）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | **火の粉の帰属** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in kdTargets)
        {
            // (a) ヒヨが素体の版での火の粉の帰属（＝この台のもともとの符号）
            var p1 = KdWins(KdPlain(b.F));
            var p0 = KdWins(KdNoCinder(KdPlain(b.F)));
            // (b) ヒヨが働いている版での火の粉の帰属
            var k1 = KdWins(b.F, KdMain);
            var k0 = KdWins(KdNoCinder(b.F), KdMain);
            Console.WriteLine($"| {b.Name} | 素体 + 火の粉あり |{KdCells(p1)} {p1.Average():0.0}% | |");
            Console.WriteLine($"| | 素体 + 火の粉なし |{KdCells(p0)} {p0.Average():0.0}% | "
                + $"**{p1.Average() - p0.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **火選り** + 火の粉あり |{KdCells(k1)} {k1.Average():0.0}% | |");
            Console.WriteLine($"| | **火選り** + 火の粉なし |{KdCells(k0)} {k0.Average():0.0}% | "
                + $"**{k1.Average() - k0.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        if (kdMode.Length > 0) return;
    }

    // --- 4. 掃引 ------------------------------------------------------------------------------
    if (kdMode.Length == 0 || kdMode == "sweep")
    {
        Console.WriteLine("## 4. 掃引（V0 `0/0` ／ V1 `2/2` ／ V2 `4/2` ／ V3 `2/4` ／ V4 `4/4`）");
        Console.WriteLine();
        Console.WriteLine("**各点に「規則を無効にした同数値の対照」を置く**（指示書 §4・第34期の作法）。");
        Console.WriteLine("ここでの対照は**素体**（HP70/攻5/速6・特性なし）1本で足りる");
        Console.WriteLine("——`Gain` / `Loss` は波の総HP も編成の顔ぶれも動かさないので、");
        Console.WriteLine("第34期の交絡（HP を振ったら波の総HP も動いた）は構造的に起きない。");
        Console.WriteLine();
        Console.WriteLine("**全幅が小さいときの切り分けは「ノブが機構の計数を動かしたか」で付ける**");
        Console.WriteLine("——`燃体/戦`・`非燃体/戦`（体数）は `Gain` / `Loss` で動かず、");
        Console.WriteLine("`配った`・`撒いた`（量）だけが動く。**体数が動いていたら測定が壊れている。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | Gain | Loss | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 燃体 | 非燃体 | 配った | 撒いた | 熾火へ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in kdTargets)
        {
            for (int i = 0; i < kdSweep.Length; i++)
            {
                (int g, int l) = kdSweep[i];
                var (wins, z) = KdAll(b.F, new FavorRule(g, l));
                Console.WriteLine($"| {b.Name} | V{i} | {g} | {l} | **{wins.Average():0.0}%** |{KdCells(wins)} "
                    + $"{z.Whetted:0.00} | {z.Dulled:0.00} | {z.Given:0.0} | {z.Taken:0.0} | {z.ToPyre:0.0} |");
                Console.Out.Flush();
            }
            var pw = KdWins(KdPlain(b.F));
            Console.WriteLine($"| {b.Name} | **素体** | — | — | **{pw.Average():0.0}%** |{KdCells(pw)} — | — | — | — | — |");
        }
        Console.WriteLine();
        foreach (var b in kdTargets)
        {
            var vals = kdSweep.Select(t => KdAll(b.F, new FavorRule(t.G, t.L)).Wins.Average()).ToList();
            double plain = KdWins(KdPlain(b.F)).Average();
            Console.WriteLine($"- **{b.Name} の掃引の全幅: {vals.Max() - vals.Min():0.0}pt**"
                + $"（{string.Join(" / ", kdSweep.Zip(vals, (t, v) => $"{t.G}/{t.L} {v:0.0}%"))}）"
                + $" ／ 素体 {plain:0.0}%");
        }
        Console.WriteLine();
        if (kdMode == "sweep") return;
    }

    // --- 5. 席の分散 --------------------------------------------------------------------------
    if (kdMode.Length == 0 || kdMode == "seats")
    {
        Console.WriteLine("## 5. 席の分散（`seats2` の写し）");
        Console.WriteLine();
        Console.WriteLine("**3値（前角 / 中央 / 後角）と 2値（前列 / それ以外）を併記する**");
        Console.WriteLine("——第50期の残件（列で決まる駒は3値だと過小評価される）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 最良席 | 次数 | 上位5の内訳 | 3値の最頻 | 前列/以外 | 幅 | 現行の順位 |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|--:|---|");
        var kdBands = new Dictionary<string, double[]>();
        foreach (var b in kdTargets)
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
            Parallel.For(0, perms.Count, i =>
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in kdStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            });
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            var top5 = order.Take(5).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));
            double width = (scan[order[0]] - scan[order[^1]]) * 100.0 / (kdStages.Count * 50);
            kdBands[b.Name] = scan.Select(v => v * 100.0 / (kdStages.Count * 50)).ToArray();
            // **素体の帯（第63期に追加・第62期 §11-1 の訂正）。** 帯の峰は機構ではなく
            // **台の配置感度**を測っている疑いがあるので、同じ5枚のヒヨだけを素体に落とした版で
            // 同じ帯を出して並べる。**第58期 Q6 の読み直し**（採否は動かさない）。
            {
                Formation pf = KdPlain(b.F);
                var pmembers = pf.Occupied().Select(x => x.Def).ToList();
                var pperms = new List<Formation>();
                foreach (int[] assign in SlotAssignments(pmembers.Count))
                {
                    var g = new Formation();
                    for (int m = 0; m < pmembers.Count; m++) g[assign[m]] = pmembers[m];
                    pperms.Add(g);
                }
                var pscan = new int[pperms.Count];
                Parallel.For(0, pperms.Count, i =>
                {
                    int wins = 0;
                    foreach (EnemyCatalog.Stage st in kdStages)
                        for (int sd = 0; sd < 50; sd++)
                            if (BattleEngine.Run(pperms[i], st.Enemy, sd, verbose: false).PlayerWon) wins++;
                    pscan[i] = wins;
                });
                kdBands[b.Name + " ←素体"] = pscan.Select(v => v * 100.0 / (kdStages.Count * 50)).ToArray();
            }
            foreach (UnitDef d in members)
            {
                int bestSlot = -1;
                foreach ((int slot, UnitDef dd) in perms[top5[0]].Occupied())
                    if (ReferenceEquals(dd, d)) bestSlot = slot;
                int mid = 0, fcorner = 0, bcorner = 0, front = 0;
                foreach (int v in top5)
                    foreach ((int slot, UnitDef dd) in perms[v].Occupied())
                        if (ReferenceEquals(dd, d))
                        {
                            int deg2 = 0;
                            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                                if (FormationRules.AreAdjacent(slot, i)) deg2++;
                            if (deg2 == 4) mid++;
                            else if (FormationRules.RowOf(slot) == Row.Front) fcorner++;
                            else bcorner++;
                            if (FormationRules.RowOf(slot) == Row.Front) front++;
                        }
                int bdeg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
                int top = Math.Max(mid, Math.Max(fcorner, bcorner));
                Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                    + $"| 前角{fcorner} / 中央{mid} / 後角{bcorner} | {top * 100 / 5}% "
                    + $"| {front} / {5 - front} | {width:0.0}pt "
                    + $"| {order.IndexOf(curIdx) + 1} / {perms.Count} |");
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 帯の形（Q6）。**単峰なら配置の判断が立っていない**（第29期のキリの反証と同じ読み方）。
        Console.WriteLine("### `reseat` 120通りの帯の形（**Q6**）");
        Console.WriteLine();
        Console.WriteLine("粗探索（seed 0..49 × 全5波）の平均勝率を 10pt 刻みで数える。");
        Console.WriteLine("**二峰なら「良い置き方」と「やってはいけない置き方」が分かれている**＝配置の判断が立っている。");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(0, 10).Select(i => $"{i * 10}〜")) + " | 峰 |");
        Console.WriteLine("|---" + string.Concat(Enumerable.Repeat("|--:", 11)) + "|");
        foreach ((string bn, double[] band) in kdBands)
        {
            var hist = new int[10];
            foreach (double v in band) hist[Math.Min(9, (int)(v / 10))]++;
            // 峰の数: 左右の隣より大きい非ゼロの箱（両端は片側だけ見る）
            int peaks = 0;
            for (int i = 0; i < 10; i++)
            {
                if (hist[i] == 0) continue;
                bool l = i == 0 || hist[i] > hist[i - 1];
                bool r = i == 9 || hist[i] > hist[i + 1];
                if (l && r) peaks++;
            }
            Console.WriteLine($"| {bn} | " + string.Join(" | ", hist.Select(h => h.ToString()))
                + $" | **{peaks}** |");
        }
        Console.WriteLine();

        Console.WriteLine("### ヒヨ1枚だけを振った5変種（他の4枚は元の相対順のまま詰める）");
        Console.WriteLine();
        Console.WriteLine("**プラスは位置を問わず・マイナスは位置で決まる**ので、");
        Console.WriteLine("`燃体` は席で動かず `非燃体` だけが次数で動くはず。**そこが設計の主張の実測。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | ヒヨの席 | 次数 | 列 | 発火 | 空振り | 燃体 | 非燃体 | 配った | 撒いた | ヒヨ寿命 | 平均勝率 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in kdTargets)
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                Formation g = KdSeat(b.F, seat);
                int deg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(seat, i)) deg++;
                var (wins, z) = KdAll(g, KdMain);
                Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {deg} | {FormationRules.RowOf(seat)} "
                    + $"| {z.Fires:0.00} | {z.Idle:0.00} | {z.Whetted:0.00} | {z.Dulled:0.00} "
                    + $"| {z.Given:0.0} | {z.Taken:0.0} | {KdGet(z.Last, "hiyo"):0.00} | {wins.Average():0.0}% |");
                Console.Out.Flush();
            }
        Console.WriteLine();
        if (kdMode == "seats") return;
    }

    // --- 6. 別 seed の追試 --------------------------------------------------------------------
    if (kdMode == "alt")
    {
        const int AltFrom = 200, AltTo = 600;
        Console.WriteLine("## 6. 別 seed の追試（seed 200..599・機構の帰属と火の粉の符号）");
        Console.WriteLine();
        double[] Cells(Formation f, FavorRule? rule)
        {
            var v = new double[kdStages.Count];
            for (int w = 0; w < kdStages.Count; w++)
            {
                int wins = 0;
                for (int seed = AltFrom; seed < AltTo; seed++)
                    if (BattleEngine.Run(f, kdStages[w].Enemy, seed, false, favor: rule).PlayerWon) wins++;
                v[w] = wins * 100.0 / (AltTo - AltFrom);
            }
            return v;
        }
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in kdTargets)
        {
            var t = Cells(b.F, KdMain);
            var pl = Cells(KdPlain(b.F), null);
            var k0 = Cells(KdNoCinder(b.F), KdMain);
            var p0 = Cells(KdNoCinder(KdPlain(b.F)), null);
            Console.WriteLine($"| {b.Name} | 火選り |{KdCells(t)} {t.Average():0.0}% |");
            Console.WriteLine($"| | 素体 |{KdCells(pl)} {pl.Average():0.0}% |");
            Console.WriteLine($"| | **機構の帰属（追試）** | "
                + string.Concat(Enumerable.Range(0, t.Length).Select(i => $"{t[i] - pl[i]:+0.0;-0.0;0.0} |"))
                + $" **{t.Average() - pl.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **火の粉の帰属・素体（追試）** | "
                + string.Concat(Enumerable.Range(0, t.Length).Select(i => $"{pl[i] - p0[i]:+0.0;-0.0;0.0} |"))
                + $" **{pl.Average() - p0.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **火の粉の帰属・火選り（追試）** | "
                + string.Concat(Enumerable.Range(0, t.Length).Select(i => $"{t[i] - k0[i]:+0.0;-0.0;0.0} |"))
                + $" **{t.Average() - k0.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    // --- 7. 配置の追試 ------------------------------------------------------------------------
    if (kdMode == "confirm")
    {
        const int CfFrom = 200, CfTo = 600;
        Console.WriteLine("## 7. 配置の追試（`confirm`・seed 200..599）");
        Console.WriteLine();
        Console.WriteLine("**選定に使っていない seed 帯**で測り直す。採否閾値は **5.0pt**（第46期）。");
        Console.WriteLine("**1位の配置ではなく次数で読む**（第45期の残件 D）。");
        Console.WriteLine("**`発火` と `空振り` の列を必ず見る**——配置探索が機構を無効化する席を");
        Console.WriteLine("選んでいたらそこは採用しない（第49期の業改が引き取り 0.00 回/戦になった件）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | ヒヨの席 | 次数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 現行との差 | 発火 | 空振り | 燃体 | 非燃体 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in kdTargets)
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
            Parallel.For(0, perms.Count, i =>
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in kdStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            });
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));

            (double[] Cells, double Fires, double Idle, double Whetted, double Dulled) Measure(Formation f)
            {
                var v = new double[kdStages.Count];
                double fires = 0, idle = 0, wh = 0, du = 0; int n = 0;
                for (int w = 0; w < kdStages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = CfFrom; seed < CfTo; seed++)
                    {
                        var r = BattleEngine.Run(f, kdStages[w].Enemy, seed, false);
                        if (r.PlayerWon) wins++;
                        fires += r.FavorFires; idle += r.FavorIdle;
                        wh += r.FavorWhetted; du += r.FavorDulled; n++;
                    }
                    v[w] = wins * 100.0 / (CfTo - CfFrom);
                }
                return (v, fires / n, idle / n, wh / n, du / n);
            }
            int Seat(Formation f)
            {
                foreach ((int slot, UnitDef d) in f.Occupied())
                    if (ReferenceEquals(d, UnitCatalog.Hiyo)) return slot;
                return -1;
            }
            int Deg(int slot)
            {
                int n = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(slot, i)) n++;
                return n;
            }

            var cur = Measure(b.F);
            Console.WriteLine($"| {b.Name} | **現行** | {FormationRules.SeatNames[Seat(b.F)]} | {Deg(Seat(b.F))} "
                + $"|{KdCells(cur.Cells)} **{cur.Cells.Average():0.0}%** | — | {cur.Fires:0.00} | {cur.Idle:0.00} "
                + $"| {cur.Whetted:0.00} | {cur.Dulled:0.00} |");
            Console.Out.Flush();
            foreach (int idx in order.Take(5))
            {
                if (idx == curIdx) continue;
                var v = Measure(perms[idx]);
                int seat = Seat(perms[idx]);
                Console.WriteLine($"| {b.Name} | 粗探索 {order.IndexOf(idx) + 1}位 | {FormationRules.SeatNames[seat]} | {Deg(seat)} "
                    + $"|{KdCells(v.Cells)} {v.Cells.Average():0.0}% "
                    + $"| **{v.Cells.Average() - cur.Cells.Average():+0.0;-0.0;0.0}pt** | {v.Fires:0.00} | {v.Idle:0.00} "
                    + $"| {v.Whetted:0.00} | {v.Dulled:0.00} |");
                Console.WriteLine($"|   ↳ 配置 | {string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} | | | | | | | | | | | | | |");
                Console.Out.Flush();
            }
            Console.WriteLine($"|   ↳ 現行の順位 | **粗探索 {order.IndexOf(curIdx) + 1}位 / {perms.Count}通り** | | | | | | | | | | | | | |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    return;
}
}
