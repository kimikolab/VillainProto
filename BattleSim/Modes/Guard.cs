using BattleCore;
using static Common;

// =====================================================================================
// guard モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "guard")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 guard
// =====================================================================================

static class GuardDiag
{
public static void Run(string[] args, int stageIndex)
{
    string guardMode = args.Length > 2 ? args[2] : "";
    var gBuilds = CompareBuilds();
    const int GuardSeeds = 200;   // compare / spread と同じ。セルを突き合わせるので変えない
    const int GuardLogSeeds = 50; // 機構の指標だけ verbose で回す本数
    int[] gHps = { 52, 71, 90, 145 };
    int gnb = gBuilds.Length, gnh = gHps.Length;

    // 殉教者の HP だけを差し替えた複製。Id・名前・攻・速・型・特性はカタログのまま。
    UnitDef MartyrWithHp(int hp) => new UnitDef
    {
        Id = EnemyCatalog.Martyr.Id, Name = EnemyCatalog.Martyr.Name,
        MaxHp = hp, Attack = EnemyCatalog.Martyr.Attack, Speed = EnemyCatalog.Martyr.Speed,
        Traits = EnemyCatalog.Martyr.Traits, Pattern = EnemyCatalog.Martyr.Pattern,
        PlusText = EnemyCatalog.Martyr.PlusText, MinusText = EnemyCatalog.Martyr.MinusText,
        Flavor = EnemyCatalog.Martyr.Flavor,
    };

    // **対照: 庇うを外して HP だけ同じにした版。** HP を上げると介入の窓が伸びるが、
    // 同時に**波の総HPも増える**（前列が厚くなる）。この2つを分けないと、
    // 用量反応が「介入が効いた」なのか「ただ硬くなった」なのかが決まらない
    // ——渇き・軛・粛が「規則を無効にした同数値の版」を必ず対照に置いたのと同じ理由。
    UnitDef PlainWithHp(int hp) => new UnitDef
    {
        Id = EnemyCatalog.Axeman2.Id, Name = EnemyCatalog.Axeman2.Name,
        MaxHp = hp, Attack = EnemyCatalog.Axeman2.Attack, Speed = EnemyCatalog.Axeman2.Speed,
        Traits = EnemyCatalog.Axeman2.Traits, Pattern = EnemyCatalog.Axeman2.Pattern,
    };

    // 第五波の残り4枠はカタログのまま。**動く変数は前1の HP と庇うの有無だけ。**
    Formation Wave5WithHp(int hp) => Formation.Build(
        front1: MartyrWithHp(hp), front3: EnemyCatalog.Hero2, center: EnemyCatalog.Knight2,
        back1: EnemyCatalog.Seer, back3: EnemyCatalog.Lancer);

    Formation Wave5PlainHp(int hp) => Formation.Build(
        front1: PlainWithHp(hp), front3: EnemyCatalog.Hero2, center: EnemyCatalog.Knight2,
        back1: EnemyCatalog.Seer, back3: EnemyCatalog.Lancer);

    // ログ行から殉教者の（発火・最終攻・生存T）を取る。gullet log / yoke log / hush と同じ理由
    // ——**発火しなかったことは盤面の値に痕跡を残さない**（庇いは標的を差し替えるだけ）。
    static int TurnOf(string t, int cur)
    {
        if (!t.StartsWith("--- ターン ")) return cur;
        int from = "--- ターン ".Length, to = t.IndexOf(' ', from);
        return to > from && int.TryParse(t.Substring(from, to - from), out int n) ? n : cur;
    }

    static (int Fire, int Atk, int Life) MartyrStats(BattleResult r)
    {
        int turn = 0, death = -1, fire = 0, atk = EnemyCatalog.Martyr.Attack;
        foreach (LogLine line in r.Log)
        {
            string t = line.Text;
            turn = TurnOf(t, turn);
            if (!t.Contains("殉教者")) continue;
            if (t.Contains("を庇った")) fire++;
            else if (t.Contains("殉教者 は倒れた") && death < 0) death = turn;
            else if (t.Contains("誓いを思い出させる"))
            {
                int arrow = t.LastIndexOf('→');
                if (arrow >= 0 && int.TryParse(new string(t.Substring(arrow + 1)
                        .Where(char.IsDigit).ToArray()), out int m)) atk = m;
            }
        }
        return (fire, atk, death < 0 ? r.Turns : death);
    }

    // 勇者候補が落ちたターン（落ちなければ決着ターン）。**庇いの窓を閉じる律速**。
    static int HeroDeathTurn(BattleResult r)
    {
        int turn = 0;
        foreach (LogLine line in r.Log)
        {
            turn = TurnOf(line.Text, turn);
            if (line.Text.Contains("勇者候補 は倒れた")) return turn;
        }
        return r.Turns;
    }

    // ---- 第2〜4波（殉教者がいないので HP を振っても不変）--------------------------------
    var gOther = new double[gnb][];   // gOther[編成][波] 波は 0..4（第1波と第5波は使わない）
    for (int b = 0; b < gnb; b++)
    {
        gOther[b] = new double[EnemyCatalog.Stages.Count];
        for (int w = 1; w <= 3; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < GuardSeeds; seed++)
                if (BattleEngine.Run(gBuilds[b].F, EnemyCatalog.Stages[w].Enemy, seed, verbose: false).PlayerWon)
                    wins++;
            gOther[b][w] = wins * 100.0 / GuardSeeds;
        }
    }
    Console.Error.WriteLine("  第2〜4波 完了");

    // ---- percent: 介入の密度（RedirectPercent）の掃引（第35期）---------------------------
    //
    // 第34期で「窓の長さ（体）では立たない」が確定した。律速は**勇者候補の生存ターン数**で、
    // 殉教者の体はそこに効かない（生存T 2.1倍に対して発火 1.5倍で頭打ち）。
    // 残る交絡の無いノブは**窓の密度**だけ——**HP を動かさないので波の総HPは1も動かず、
    // 対照は第34期の「庇うなし・HP52」1本で足りる。**
    //
    // 割合は MartyrRule で外から差す（static のノブは置かない。Colossus/Yoke/Hush と同じ）。
    // **p=50 は検算点**で、ここが docs/balance.md の第五波と一致しなければ掃引の台が違う。
    if (guardMode == "percent")
    {
        int[] gPs = { 50, 75, 100 };
        int gnp = gPs.Length;
        Formation gFoe = EnemyCatalog.Stages[4].Enemy;                  // 現行の第五波（殉教者 HP52）
        Formation gCtl = Wave5PlainHp(EnemyCatalog.Martyr.MaxHp);       // 対照: 庇うなし・同HP

        Console.WriteLine("# 介入の密度の掃引（guard percent）");
        Console.WriteLine();
        Console.WriteLine($"代表編成 {gnb} × 第五波 × p {gnp} 点、seed 0..{GuardSeeds - 1}。診断用なので docs/ には置かない。");
        Console.WriteLine();
        Console.WriteLine("動かすのは **割合（`MartyrRule.RedirectPercent`）の1変数のみ**。");
        Console.WriteLine("HP52・攻11・速5・薙ぎ・`DamagePerGain` 2・前1 の席・ガルド側はすべて据え置き。");
        Console.WriteLine("**HP を動かさないので波の総HPは1も動かない**——対照は「庇うなし・HP52」1本で足りる。");
        Console.WriteLine();

        var pRate = new double[gnp][];
        var pFire = new double[gnp][];
        var pAtk = new double[gnp][];
        var pLife = new double[gnp][];
        var pHero = new double[gnp][];   // 勇者候補の生存T（律速の直接観測）
        var ctlRate = new double[gnb];
        var ctlHero = new double[gnb];

        for (int b = 0; b < gnb; b++)
        {
            int w = 0;
            for (int seed = 0; seed < GuardSeeds; seed++)
                if (BattleEngine.Run(gBuilds[b].F, gCtl, seed, verbose: false).PlayerWon) w++;
            ctlRate[b] = w * 100.0 / GuardSeeds;

            long hero = 0;
            for (int seed = 0; seed < GuardLogSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(gBuilds[b].F, gCtl, seed, verbose: true);
                hero += HeroDeathTurn(r);
            }
            ctlHero[b] = hero / (double)GuardLogSeeds;
        }
        Console.Error.WriteLine("  対照（庇うなし・HP52）完了");

        for (int h = 0; h < gnp; h++)
        {
            var rule = new MartyrRule(gPs[h]);
            pRate[h] = new double[gnb]; pFire[h] = new double[gnb]; pAtk[h] = new double[gnb];
            pLife[h] = new double[gnb]; pHero[h] = new double[gnb];

            for (int b = 0; b < gnb; b++)
            {
                int w = 0;
                for (int seed = 0; seed < GuardSeeds; seed++)
                    if (BattleEngine.Run(gBuilds[b].F, gFoe, seed, verbose: false, null, null, null, rule).PlayerWon) w++;
                pRate[h][b] = w * 100.0 / GuardSeeds;

                long fire = 0, atk = 0, life = 0, hero = 0;
                for (int seed = 0; seed < GuardLogSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(gBuilds[b].F, gFoe, seed, verbose: true, null, null, null, rule);
                    (int f, int a, int l) = MartyrStats(r);
                    fire += f; atk += a; life += l;
                    hero += HeroDeathTurn(r);
                }
                pFire[h][b] = fire / (double)GuardLogSeeds;
                pAtk[h][b] = atk / (double)GuardLogSeeds;
                pLife[h][b] = life / (double)GuardLogSeeds;
                pHero[h][b] = hero / (double)GuardLogSeeds;
            }
            Console.Error.WriteLine($"  p {gPs[h]} 完了");
        }

        Console.WriteLine("## 1. 第五波の分布");
        Console.WriteLine();
        Console.WriteLine("| 割合 p | 平均 | 100%の編成 | 0%の編成 | 中間帯(5〜95%) | 標準偏差 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|");
        {
            double m0 = ctlRate.Average();
            double s0 = Math.Sqrt(ctlRate.Select(x => (x - m0) * (x - m0)).Average());
            Console.WriteLine($"| **対照（庇うなし）** | {m0:F1} | {ctlRate.Count(x => x == 100.0)} / {gnb} "
                              + $"| {ctlRate.Count(x => x == 0.0)} | {ctlRate.Count(x => x > 5.0 && x < 95.0)} | {s0:F1} |");
        }
        for (int h = 0; h < gnp; h++)
        {
            double[] v = pRate[h];
            double m = v.Average();
            double sd = Math.Sqrt(v.Select(x => (x - m) * (x - m)).Average());
            Console.WriteLine($"| {gPs[h]} | {m:F1} | {v.Count(x => x == 100.0)} / {gnb} | {v.Count(x => x == 0.0)} "
                              + $"| {v.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} |");
        }

        Console.WriteLine();
        Console.WriteLine("## 2. 固有の敗者の帰属（対照で敗者でない行だけを数える）");
        Console.WriteLine();
        Console.WriteLine("**庇うが作った敗者** = 庇うありで `≤10` かつ**対照では `>10`** かつ 第2〜4波がすべて `>10`。");
        Console.WriteLine("第34期の帰属条件をそのまま使う（見かけの新規と区別する）。");
        Console.WriteLine();
        var ctlLosers = new List<string>();
        for (int b = 0; b < gnb; b++)
            if (ctlRate[b] <= 10.0 && Enumerable.Range(1, 3).All(w => gOther[b][w] > 10.0))
                ctlLosers.Add(gBuilds[b].Name);
        Console.WriteLine($"- **対照（庇うなし）の敗者 {ctlLosers.Count}**: {(ctlLosers.Count == 0 ? "なし" : string.Join(" / ", ctlLosers))}");
        for (int h = 0; h < gnp; h++)
        {
            var all = new List<string>();
            var owned = new List<string>();
            for (int b = 0; b < gnb; b++)
            {
                if (!Enumerable.Range(1, 3).All(w => gOther[b][w] > 10.0)) continue;
                if (pRate[h][b] > 10.0) continue;
                all.Add(gBuilds[b].Name);
                if (ctlRate[b] > 10.0) owned.Add(gBuilds[b].Name);
            }
            Console.WriteLine($"- **p{gPs[h]}**: 敗者 {all.Count} / うち**庇うが作った敗者 {owned.Count}**");
            Console.WriteLine($"    - 敗者: {(all.Count == 0 ? "なし" : string.Join(" / ", all))}");
            Console.WriteLine($"    - **庇うが作った: {(owned.Count == 0 ? "**なし**" : string.Join(" / ", owned))}**");
        }

        Console.WriteLine();
        Console.WriteLine("## 3. 編成 × p の勝率（第五波）");
        Console.WriteLine();
        Console.WriteLine("`介入分` = その p の勝率 − 対照（庇うなし）。**これが庇うの正味の税額。**");
        Console.WriteLine();
        Console.Write("| 編成 | 他波最小 | 対照 |");
        foreach (int p in gPs) Console.Write($" p{p} |");
        foreach (int p in gPs) Console.Write($" 介入分(p{p}) |");
        Console.WriteLine();
        Console.Write("|---|--:|--:|");
        for (int i = 0; i < gnp * 2; i++) Console.Write("--:|");
        Console.WriteLine();
        for (int b = 0; b < gnb; b++)
        {
            string name = gBuilds[b].Name;
            bool canary = name.Contains("毒+ベニ+ラウ") || name.Contains("毒+耐久");
            double omin = Enumerable.Range(1, 3).Min(w => gOther[b][w]);
            Console.Write($"| {(canary ? "**" + name + "**（カナリア）" : name)} | {omin:F1} | {ctlRate[b]:F1} |");
            foreach (var t in pRate) Console.Write($" {t[b]:F1} |");
            foreach (var t in pRate) Console.Write($" {t[b] - ctlRate[b]:+0.0;-0.0;0.0} |");
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine($"## 4. 機構の指標（seed 0..{GuardLogSeeds - 1} の平均・全42編成）");
        Console.WriteLine();
        Console.WriteLine("`勇者候補の生存T` が第34期に確定した律速。**ここが伸びない限り発火は頭打ちになる。**");
        Console.WriteLine();
        Console.WriteLine("| 割合 p | 発火/戦 | 殉教者の最終攻 | 殉教者の生存T | 勇者候補の生存T |");
        Console.WriteLine("|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| **対照（庇うなし）** | 0.00 | — | — | {ctlHero.Average():F2} |");
        for (int h = 0; h < gnp; h++)
            Console.WriteLine($"| {gPs[h]} | {pFire[h].Average():F2} | {pAtk[h].Average():F1} "
                              + $"| {pLife[h].Average():F2} | {pHero[h].Average():F2} |");

        Console.WriteLine();
        Console.WriteLine("## 5. 発火が多い順（上位12行）");
        Console.WriteLine();
        Console.Write("| 編成 |");
        foreach (int p in gPs) Console.Write($" 発火(p{p}) |");
        foreach (int p in gPs) Console.Write($" 最終攻(p{p}) |");
        foreach (int p in gPs) Console.Write($" 殉教者生存T(p{p}) |");
        Console.WriteLine();
        Console.Write("|---|");
        for (int i = 0; i < gnp * 3; i++) Console.Write("--:|");
        Console.WriteLine();
        foreach (int b in Enumerable.Range(0, gnb).OrderByDescending(i => pFire[gnp - 1][i]).Take(12))
        {
            Console.Write($"| {gBuilds[b].Name} |");
            foreach (var t in pFire) Console.Write($" {t[b]:F1} |");
            foreach (var t in pAtk) Console.Write($" {t[b]:F1} |");
            foreach (var t in pLife) Console.Write($" {t[b]:F1} |");
            Console.WriteLine();
        }
        return;
    }


    Console.WriteLine("# 殉教者の体の用量反応（guard）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 {gnb} × 第五波 × HP {gnh} 点、seed 0..{GuardSeeds - 1}。診断用なので docs/ には置かない。");
    Console.WriteLine();
    Console.WriteLine("動かすのは **HP の1変数のみ**（攻11・速5・薙ぎ・Guardian・前1 の席は据え置き）。");
    Console.WriteLine("掃引点は既存の敵の体から借りている: 52=現行 / 71=この波の中央 / 90=この波の前3 / 145=ロスター最重。");
    Console.WriteLine();


    // ---- 各 HP 点で第五波を測る ------------------------------------------------------------
    var gRate = new double[gnh][];        // 勝率(%)・庇うあり
    var gPlain = new double[gnh][];       // 勝率(%)・庇うなし（同HPの対照）
    var gFire = new double[gnh][];        // 肩代わりの発火 /戦
    var gAtk = new double[gnh][];         // 殉教者の最終攻撃力（平均）
    var gLife = new double[gnh][];        // 殉教者の生存ターン（平均。生存し続けたら決着T）

    for (int h = 0; h < gnh; h++)
    {
        Formation foe = Wave5WithHp(gHps[h]);
        Formation plain = Wave5PlainHp(gHps[h]);
        gRate[h] = new double[gnb];
        gPlain[h] = new double[gnb];
        gFire[h] = new double[gnb];
        gAtk[h] = new double[gnb];
        gLife[h] = new double[gnb];

        for (int b = 0; b < gnb; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < GuardSeeds; seed++)
                if (BattleEngine.Run(gBuilds[b].F, foe, seed, verbose: false).PlayerWon) wins++;
            gRate[h][b] = wins * 100.0 / GuardSeeds;

            int pwins = 0;
            for (int seed = 0; seed < GuardSeeds; seed++)
                if (BattleEngine.Run(gBuilds[b].F, plain, seed, verbose: false).PlayerWon) pwins++;
            gPlain[h][b] = pwins * 100.0 / GuardSeeds;

            // 機構の指標は verbose のログ行から。**盤面は1つも動かさない。**
            long fire = 0, atk = 0, life = 0;
            for (int seed = 0; seed < GuardLogSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(gBuilds[b].F, foe, seed, verbose: true);
                int turn = 0, deathTurn = -1, lastAtk = EnemyCatalog.Martyr.Attack;

                foreach (LogLine line in r.Log)
                {
                    string t = line.Text;
                    if (t.StartsWith("--- ターン "))
                    {
                        int from = "--- ターン ".Length, to = t.IndexOf(' ', from);
                        if (to > from && int.TryParse(t.Substring(from, to - from), out int n)) turn = n;
                        continue;
                    }
                    if (!t.Contains("殉教者")) continue;

                    if (t.Contains("を庇った")) fire++;
                    else if (t.Contains("殉教者 は倒れた") && deathTurn < 0) deathTurn = turn;
                    else if (t.Contains("誓いを思い出させる"))
                    {
                        // 「（攻撃 +N → M）」の M を取る。最後に出た値が最終攻撃力。
                        int arrow = t.LastIndexOf('→');
                        if (arrow >= 0)
                        {
                            string tail = new string(t.Substring(arrow + 1).Where(char.IsDigit).ToArray());
                            if (int.TryParse(tail, out int m)) lastAtk = m;
                        }
                    }
                }
                fire += 0;
                atk += lastAtk;
                life += deathTurn < 0 ? r.Turns : deathTurn;   // 落ちなければ決着まで生存
            }
            gFire[h][b] = fire / (double)GuardLogSeeds;
            gAtk[h][b] = atk / (double)GuardLogSeeds;
            gLife[h][b] = life / (double)GuardLogSeeds;
        }
        Console.Error.WriteLine($"  HP {gHps[h]} 完了");
    }

    // ---- 1. 分布 -------------------------------------------------------------------------
    Console.WriteLine("## 1. 第五波の分布");
    Console.WriteLine();
    Console.WriteLine("| 殉教者HP | 平均 | 100%の編成 | 0%の編成 | 中間帯(5〜95%) | 標準偏差 |");
    Console.WriteLine("|--:|--:|--:|--:|--:|--:|");
    for (int h = 0; h < gnh; h++)
    {
        double[] v = gRate[h];
        double m = v.Average();
        double sd = Math.Sqrt(v.Select(x => (x - m) * (x - m)).Average());
        Console.WriteLine($"| {gHps[h]} | {m:F1} | {v.Count(x => x == 100.0)} / {gnb} | {v.Count(x => x == 0.0)} "
                          + $"| {v.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} |");
    }

    Console.WriteLine();
    Console.WriteLine("## 1b. 交絡の切り分け —— 庇うを外した同HPの対照");
    Console.WriteLine();
    Console.WriteLine("HP を上げると**介入の窓が伸びる**と同時に**波の総HPが増える**。");
    Console.WriteLine("同じ HP で庇うだけを外した版と比べれば、差がそのまま**介入の効果**になる");
    Console.WriteLine("（渇き・軛・粛が「規則を無効にした同数値の版」を対照に置いたのと同じ形）。");
    Console.WriteLine();
    Console.WriteLine("**HP52 の対照は差し替え前の第五波そのもの**（前1 戦斧兵 52/11/5・薙ぎ）なので、");
    Console.WriteLine("その行が第32期差し戻し後の値と一致することが診断そのものの検算になる。");
    Console.WriteLine();
    Console.WriteLine("| 殉教者HP | 庇うあり 平均 | 庇うなし 平均 | 介入の効果 | 波の総HP | HP による増分 |");
    Console.WriteLine("|--:|--:|--:|--:|--:|--:|");
    double baseAll = gPlain[0].Average();
    for (int h = 0; h < gnh; h++)
    {
        double a = gRate[h].Average(), p = gPlain[h].Average();
        int total = gHps[h] + EnemyCatalog.Hero2.MaxHp + EnemyCatalog.Knight2.MaxHp
                    + EnemyCatalog.Seer.MaxHp + EnemyCatalog.Lancer.MaxHp;
        Console.WriteLine($"| {gHps[h]} | {a:F1} | {p:F1} | **{a - p:+0.0;-0.0;0.0}** | {total} | {p - baseAll:+0.0;-0.0;0.0} |");
    }

    // ---- 2. 固有の敗者 --------------------------------------------------------------------
    // 「第五波だけで 10 以下、他のどの波（第一波を除く）でも 10 超」。
    // 第一波は全編成 100% のチュートリアル波なので比較から外す（spread と同じ作法）。
    Console.WriteLine();
    Console.WriteLine("## 2. 第五波の固有の敗者（閾値 90/10・第一波は比較対象外）");
    Console.WriteLine();
    Console.WriteLine("**新規** = HP52（第33期の採用値）では固有の敗者でなかった行。");
    Console.WriteLine();
    var baseLosers = new HashSet<string>();
    for (int h = 0; h < gnh; h++)
    {
        var losers = new List<string>();
        var winners = new List<string>();
        for (int b = 0; b < gnb; b++)
        {
            bool otherAbove = Enumerable.Range(1, 3).All(w => gOther[b][w] > 10.0);
            if (gRate[h][b] <= 10.0 && otherAbove) losers.Add(gBuilds[b].Name);
            bool otherBelow = Enumerable.Range(1, 3).All(w => gOther[b][w] < 90.0);
            if (gRate[h][b] >= 90.0 && otherBelow) winners.Add(gBuilds[b].Name);
        }
        if (h == 0) foreach (string n in losers) baseLosers.Add(n);
        var fresh = losers.Where(n => !baseLosers.Contains(n)).ToList();
        Console.WriteLine($"- **HP {gHps[h]}**: 敗者 {losers.Count} / 勝者 {winners.Count}");
        Console.WriteLine($"    - 敗者: {(losers.Count == 0 ? "なし" : string.Join(" / ", losers))}");
        Console.WriteLine($"    - **新規の敗者: {(fresh.Count == 0 ? "なし" : string.Join(" / ", fresh))}**");
        Console.WriteLine($"    - 勝者: {(winners.Count == 0 ? "なし" : string.Join(" / ", winners))}");
    }

    // ---- 3. カナリア + 全行の勝率 ----------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 3. 編成 × HP の勝率（第五波）");
    Console.WriteLine();
    Console.WriteLine("`カナリア` = 指示書が指定した課税過剰の検出行（毒+ベニ+ラウ / 毒+耐久）。");
    Console.WriteLine("`他波最小` = 第2〜4波の最小値（10 以下なら固有の敗者になれない）。");
    Console.WriteLine();
    Console.Write("| 編成 | 他波最小 |");
    foreach (int hp in gHps) Console.Write($" HP{hp} |");
    Console.Write(" 52→145 |");
    foreach (int hp in gHps) Console.Write($" 介入分(HP{hp}) |");
    Console.WriteLine();
    Console.Write("|---|--:|");
    for (int i = 0; i < gnh * 2 + 1; i++) Console.Write("--:|");
    Console.WriteLine();
    for (int b = 0; b < gnb; b++)
    {
        string name = gBuilds[b].Name;
        bool canary = name.Contains("毒+ベニ+ラウ") || name.Contains("毒+耐久");
        double omin = Enumerable.Range(1, 3).Min(w => gOther[b][w]);
        Console.Write($"| {(canary ? "**" + name + "**（カナリア）" : name)} | {omin:F1} |");
        foreach (var t in gRate) Console.Write($" {t[b]:F1} |");
        Console.Write($" {gRate[gnh - 1][b] - gRate[0][b]:+0.0;-0.0;0.0} |");
        for (int h = 0; h < gnh; h++) Console.Write($" {gRate[h][b] - gPlain[h][b]:+0.0;-0.0;0.0} |");
        Console.WriteLine();
    }

    // ---- 4. 機構の指標 ---------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine($"## 4. 殉教者の側の指標（seed 0..{GuardLogSeeds - 1} の平均）");
    Console.WriteLine();
    Console.WriteLine("`発火` = 肩代わりが成立した回数/戦。`最終攻` = 戦闘終了時の攻撃力（素は 11）。");
    Console.WriteLine("`生存T` = 殉教者が落ちたターン（落ちなければ決着ターン）。");
    Console.WriteLine();
    Console.Write("| 編成 |");
    foreach (int hp in gHps) Console.Write($" 発火(HP{hp}) |");
    foreach (int hp in gHps) Console.Write($" 最終攻(HP{hp}) |");
    foreach (int hp in gHps) Console.Write($" 生存T(HP{hp}) |");
    Console.WriteLine();
    Console.Write("|---|");
    for (int i = 0; i < gnh * 3; i++) Console.Write("--:|");
    Console.WriteLine();
    foreach (int b in Enumerable.Range(0, gnb).OrderByDescending(i => gFire[gnh - 1][i]))
    {
        Console.Write($"| {gBuilds[b].Name} |");
        foreach (var t in gFire) Console.Write($" {t[b]:F1} |");
        foreach (var t in gAtk) Console.Write($" {t[b]:F1} |");
        foreach (var t in gLife) Console.Write($" {t[b]:F1} |");
        Console.WriteLine();
    }

    // ---- 5. 平均の要約 ----------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 5. 全編成の平均（機構の指標）");
    Console.WriteLine();
    Console.WriteLine("| 殉教者HP | 発火/戦 | 最終攻 | 生存T |");
    Console.WriteLine("|--:|--:|--:|--:|");
    for (int h = 0; h < gnh; h++)
        Console.WriteLine($"| {gHps[h]} | {gFire[h].Average():F2} | {gAtk[h].Average():F1} | {gLife[h].Average():F2} |");

    return;
}
}
