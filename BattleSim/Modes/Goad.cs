using BattleCore;
using static Common;

// =====================================================================================
// goad モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "goad")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 goad
// =====================================================================================

static class GoadDiag
{
// wave2 モード: 波の関門を1つずつ外して、その波が何に課金しているかを数える（第51期）。
//
// compare は「どの編成が勝つか」しか見ないので、**波の側が何を関門にしているか**が読めない。
// spread（第22期）は波の分離度を測るが、あれも波を1つの箱として扱うだけで、
// 波の中の**どの駒／どの規則**が天井を押さえているかは区別しない。
//
// やっていることは1つだけ——**敵を1体ずつ空席にし、盤面ルールを1つ切って compare を回し、
// 差分を取る**。`Stages` は書き換えず、波の複製をこの中でローカルに組む
// （第46期の `ObRows()` / 第49期の `SgRows()` と同型）。**盤面は1つも動かない。**
//
// **主要な指標は平均ではなく「100.0% の行数」。** 関門を外して 100.0% が増えるなら、
// その関門は天井を押さえている。増えないなら、その関門は既に効いていない。
//
// **「抜く」は空席にする**（別の駒で埋めない）。埋めると2変数が動く。
// **規則は2通りで外す**——(a) 規則フラグ（HushRule / YokeRule の Active）と
// (b) 保持者を**同数値の対照駒**に差し替える。**両者が1セルも違わないことが診断の検算**
// （粛・渇き・軛・殉教はどれも数値・型・速さが素の駒と1つも違わない）。
//
// 波は引数で選べる。第二波以外にも同じ解剖をそのまま回せる。
//
//     dotnet run --project BattleSim -c Release 0 wave2 [波番号 1-5、既定 2]
// 駆り立て（第52期）の診断。**出力は docs/ に置かない**（標準出力で読むだけ）。
// `CompareBuilds()` には**2行足した**が、`Stages` / `Columns` は触っていない。
//
// **「渡した量」と「効き」を分けること。** 第42期は「生成したアーマー」ではなく「実際に吸った量」、
// 第43期は「流した量」ではなく「効き」、第47期は「貫き」ではなく「後列到達」、
// 第49期は「転写」ではなく「転写の効き」、第50期は「焦点」ではなく「撃破順」で判断した。
// **渡した量は成果ではない**——対象が渡した直後に死ぬなら AtkBonus はダメージに変わっていない。
//
// **「効き」と「早逝」を対で見ること。** この駒の本質は「強くなるが死にやすくなる」なので、
// 収支がどちらに振れるかが性格を決める。
//
//     dotnet run --project BattleSim -c Release 0 goad [絞り込み]
//     dotnet run --project BattleSim -c Release 0 goad sweep     # Boost 0/2/4/6 の掃引だけ
//     dotnet run --project BattleSim -c Release 0 goad confirm   # 配置の追試（seed 200..599）
//     dotnet run --project BattleSim -c Release 0 goad alt       # 機構の帰属を別 seed 帯で追試
//     dotnet run --project BattleSim -c Release 0 goad cross     # ソラ・ウツとの干渉（同居版）
public static void Run(string[] args, int stageIndex)
{
    var gdBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> gdStages = EnemyCatalog.Stages;
    const int GdSeeds = 200;   // compare / spread / divert と揃える
    int GdMain = GoadRule.Default.Boost;
    int[] gdBoosts = { 0, 2, 4, 6 };

    string gdMode = args.Length > 2 ? args[2] : "";

    static bool GdHasKari(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Kari));

    var gdTargets = gdBuilds.Where(b => GdHasKari(b.F)).ToArray();
    if (gdMode.Length > 0 && gdMode != "sweep" && gdMode != "confirm"
        && gdMode != "alt" && gdMode != "cross" && gdMode != "seats")
        gdTargets = gdTargets.Where(b => gdMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // **素体の対照（対照1）。** カリと数値・型・速さが1つも違わず、特性だけを持たない駒。
    // **`GoadRule` を弱める形の対照は使わない**（第47期 `ScaleRule(0)` の失敗）——
    // `Boost = 0` にしても標は付き続けるので、「機構が効いたのか、ただ 62/4/9 の体が
    // 入っただけか」が割れない。
    UnitDef GdPlainDef = new()
    {
        Id = "kari_plain", Name = "素体のカリ", MaxHp = UnitCatalog.Kari.MaxHp,
        Attack = UnitCatalog.Kari.Attack, Speed = UnitCatalog.Kari.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Kari.Pattern
    };
    Formation GdPlain(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Kari) ? GdPlainDef : d;
        return g;
    }
    // カリを外した4体版（対照3）。**第21期の飽和検査**も兼ねる。
    static Formation GdWithoutKari(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Kari)) g[slot] = d;
        return g;
    }

    // カリの席だけを振った5変種（他の4枚は元の相対順のまま詰める）。
    static Formation GdSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Kari))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Kari;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    GdStat MeasureGd(Formation f, Formation enemy, GoadRule rule)
    {
        var z = new GdStat();
        for (int seed = 0; seed < GdSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false, goad: rule);
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Fires += r.GoadFires; z.Idle += r.GoadIdle; z.Given += r.GoadGiven;
            z.Switches += r.GoadSwitches; z.MarkLost += r.GoadMarkLost;
            z.ToPerverse += r.GoadToPerverse;
            foreach ((string k, int v) in r.GoadTargetTo)
                z.TargetTo[k] = z.TargetTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string id, UnitTally t) in r.TallyByUnit)
            {
                if (!z.Dmg.ContainsKey(id))
                { z.Dmg[id] = 0; z.Taken[id] = 0; z.Last[id] = 0; z.Deaths[id] = 0; }
                z.Dmg[id] += t.DamageToEnemy;
                z.Taken[id] += t.DamageTaken;
                z.Last[id] += t.LastActiveTurn;
                z.Deaths[id] += t.Deaths;
            }
        }
        double n = GdSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.Fires /= n; z.Idle /= n; z.Given /= n; z.Switches /= n; z.MarkLost /= n;
        z.ToPerverse /= n;
        foreach (string k in z.TargetTo.Keys.ToList()) z.TargetTo[k] /= n;
        foreach (string k in z.Dmg.Keys.ToList())
        { z.Dmg[k] /= n; z.Taken[k] /= n; z.Last[k] /= n; z.Deaths[k] /= n; }
        return z;
    }

    (double[] Wins, GdStat Z) GdAll(Formation f, GoadRule rule)
    {
        var wins = new double[gdStages.Count];
        var acc = new GdStat();
        for (int w = 0; w < gdStages.Count; w++)
        {
            var z = MeasureGd(f, gdStages[w].Enemy, rule);
            wins[w] = z.Win;
            acc.Turns += z.Turns; acc.Fires += z.Fires; acc.Idle += z.Idle; acc.Given += z.Given;
            acc.Switches += z.Switches; acc.MarkLost += z.MarkLost; acc.ToPerverse += z.ToPerverse;
            foreach ((string k, double v) in z.TargetTo)
                acc.TargetTo[k] = acc.TargetTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.Dmg)
            {
                if (!acc.Dmg.ContainsKey(k))
                { acc.Dmg[k] = 0; acc.Taken[k] = 0; acc.Last[k] = 0; acc.Deaths[k] = 0; }
                acc.Dmg[k] += v; acc.Taken[k] += z.Taken[k];
                acc.Last[k] += z.Last[k]; acc.Deaths[k] += z.Deaths[k];
            }
        }
        double m = gdStages.Count;
        acc.Win = wins.Average();
        acc.Turns /= m; acc.Fires /= m; acc.Idle /= m; acc.Given /= m;
        acc.Switches /= m; acc.MarkLost /= m; acc.ToPerverse /= m;
        foreach (string k in acc.TargetTo.Keys.ToList()) acc.TargetTo[k] /= m;
        foreach (string k in acc.Dmg.Keys.ToList())
        { acc.Dmg[k] /= m; acc.Taken[k] /= m; acc.Last[k] /= m; acc.Deaths[k] /= m; }
        return (wins, acc);
    }

    double[] GdWins(Formation f)
    {
        var v = new double[gdStages.Count];
        for (int w = 0; w < gdStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < GdSeeds; seed++)
                if (BattleEngine.Run(f, gdStages[w].Enemy, seed, false).PlayerWon) wins++;
            v[w] = wins * 100.0 / GdSeeds;
        }
        return v;
    }

    static string GdCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));
    static string GdTop(Dictionary<string, double> d, int n = 3)
    {
        var parts = d.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(n)
            .Select(x => $"{x.Key} {x.Value:0.00}").ToList();
        return parts.Count == 0 ? "—" : string.Join(" / ", parts);
    }
    // 主な渡し先（§1 の「対象」の最頻）の UnitDef.Id。★ の行と各表の ★列 が共有する。
    static string GdStarId(Formation f, GdStat z)
    {
        if (z.TargetTo.Count == 0) return "";
        string name = z.TargetTo.OrderByDescending(x => x.Value).First().Key;
        foreach ((int _, UnitDef d) in f.Occupied())
            if (d.Name == name) return d.Id;
        return "";
    }
    static double GdGet(Dictionary<string, double> d, string k)
        => k.Length > 0 && d.TryGetValue(k, out double v) ? v : 0;

    Console.WriteLine("# 駆り立て（goad）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 goad [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{GdSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（渡す相手の性格で分けた対）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 発火 | 駆り立ての発火回数/戦。**0 になっていないことが受け入れ基準4**（配置探索が機構を無効化していないか） |");
    Console.WriteLine("| 空振り | 隣接に生存味方（支援を受け取る側）がいなくて何もしなかった回数/戦 |");
    Console.WriteLine("| 渡した量 | `AtkBonus` の累積付与量/戦。**成果ではない**（下の「効き」と対で読む） |");
    Console.WriteLine("| 対象 | 渡した相手の内訳・**入替**（対象が前ターンと変わった回数） |");
    Console.WriteLine("| **効き** | **対象が実際に与えたダメージの増加量**（素体との差）。§2 |");
    Console.WriteLine("| **被弾増** | **標が原因で対象が余分に受けた被弾**（素体との差）。§2 |");
    Console.WriteLine("| **早逝** | **対象が最後に盤上にいたターンの差**（素体との差。負なら早く死んでいる）。§2 |");
    Console.WriteLine("| ウツ | 渡した先が逆しま（ウツ）だった回数/戦。**強化が害になる** |");
    Console.WriteLine("| 標消え | 付けた標が次の発火までに剥がされていた回数/戦（**逸らし＝ソラが唯一の経路**） |");
    Console.WriteLine();
    Console.WriteLine("> **「渡した量」と「効き」は別の列。** 渡した量は成果ではない——");
    Console.WriteLine("> 対象が渡した直後に死ぬなら、`AtkBonus` はダメージに変わっていない。");
    Console.WriteLine("> **「効き」と「早逝」の収支（§2）がこの期の主眼。**");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）-----------------------------------------------------------
    if (gdMode.Length == 0)
    {
        Console.WriteLine("## 0. 検算");
        Console.WriteLine();
        var plain = gdBuilds.Where(b => !GdHasKari(b.F)).ToArray();
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < gdStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < GdSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, gdStages[w].Enemy, seed, false,
                            goad: new GoadRule(0, false)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, gdStages[w].Enemy, seed, false,
                            goad: new GoadRule(9, true)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（カリを含まない {plain.Length} 行が `GoadRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {gdStages.Count} 波・`(0, false)` 対 `(9, true)`）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  行を足す前に `compare` の全文で確認済み（**260 セル中 0 件**）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 ------------------------------------------------------------------------------
    if (gdMode.Length == 0 || (gdMode != "sweep" && gdMode != "confirm"
                               && gdMode != "alt" && gdMode != "cross" && gdMode != "seats"))
    {
        Console.WriteLine($"## 1. 主表（`Boost = {GdMain}` と陽性対照）");
        Console.WriteLine();
        Console.WriteLine("`素体` = カリと**数値・型・速さが1つも違わず特性だけを持たない駒**（対照1）。");
        Console.WriteLine("**これが機構の帰属を取る唯一の窓口**——`Boost` を 0 にしても標は付き続ける。");
        Console.WriteLine("`標なし` = `GoadRule.Mark = false`（対照2）。**矛先を集めない版**で、");
        Console.WriteLine("力を渡すだけが残る——**代金の分離**。**差が小さければこの駒は単なるバッファー。**");
        Console.WriteLine("`力なし` = `Boost = 0`（対照2の裏）。**矛先だけを集める版**で、見返りが無い。");
        Console.WriteLine("`4体` = カリを外した4体版（対照3。**第21期の飽和検査**も兼ねる）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in gdTargets)
        {
            var (wins, _) = GdAll(b.F, new GoadRule(GdMain));
            var (nm, _) = GdAll(b.F, new GoadRule(GdMain, false));
            var (nb, _) = GdAll(b.F, new GoadRule(0, true));
            var pw = GdWins(GdPlain(b.F));
            var fw = GdWins(GdWithoutKari(b.F));
            Console.WriteLine($"| {b.Name} | **B{GdMain}** |{GdCells(wins)} {wins.Average():0.0}% |");
            Console.WriteLine($"| | 標なし（矛先を集めない） |{GdCells(nm)} {nm.Average():0.0}% |");
            Console.WriteLine($"| | 力なし（Boost = 0） |{GdCells(nb)} {nb.Average():0.0}% |");
            Console.WriteLine($"| | 素体（特性なし・同数値） |{GdCells(pw)} {pw.Average():0.0}% |");
            Console.WriteLine($"| | 4体（カリ抜き） |{GdCells(fw)} {fw.Average():0.0}% |");
            Console.WriteLine($"| | **機構の帰属（B{GdMain} − 素体）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - pw[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - pw.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **代金の値段（B{GdMain} − 標なし）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - nm[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - nm.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **見返りの値段（B{GdMain} − 力なし）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - nb[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - nb.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | 体の値段（素体 − 4体） | "
                + string.Concat(Enumerable.Range(0, pw.Length).Select(i => $"{pw[i] - fw[i]:+0.0;-0.0;0.0} |"))
                + $" **{pw.Average() - fw.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 機構の計数（`Boost = {GdMain}`・5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | **発火** | 空振り | 渡した量 | 対象 | 入替 | ウツ | 標消え | カリ寿命 |");
        Console.WriteLine("|---|--:|--:|--:|---|--:|--:|--:|--:|");
        foreach (var b in gdTargets)
        {
            var (_, z) = GdAll(b.F, new GoadRule(GdMain));
            Console.WriteLine($"| {b.Name} | **{z.Fires:0.00}** | {z.Idle:0.00} | {z.Given:0.0} "
                + $"| {GdTop(z.TargetTo)} | {z.Switches:0.00} | {z.ToPerverse:0.00} | {z.MarkLost:0.00} "
                + $"| {GdGet(z.Last, "kari"):0.00} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 2. 効きと早逝（本命の指標）--------------------------------------------------------
        Console.WriteLine("## 2. 効きと早逝（**本命の指標**）—— 味方1体ごとの素体との差");
        Console.WriteLine();
        Console.WriteLine("**渡した量は成果ではない。** 対象が渡した直後に死ぬなら `AtkBonus` は");
        Console.WriteLine("ダメージに変わっていない。**`効き` と `早逝` の収支がこの駒の性格を決める。**");
        Console.WriteLine("`★` が駆り立ての主な渡し先（§1 の `対象` の最頻）。5波の平均。");
        Console.WriteLine();
        foreach (var b in gdTargets)
        {
            var (_, z) = GdAll(b.F, new GoadRule(GdMain));
            var (_, pz) = GdAll(GdPlain(b.F), new GoadRule(GdMain));
            string star = GdStarId(b.F, z);
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 味方 | 与ダメ(カリ) | 与ダメ(素体) | **効き** | 被弾(カリ) | 被弾(素体) | **被弾増** | 寿命(カリ) | 寿命(素体) | **早逝** | 死亡(カリ/素体) |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            double sa = 0, sp = 0;
            foreach ((int _, UnitDef d) in b.F.Occupied())
            {
                if (ReferenceEquals(d, UnitCatalog.Kari)) continue;
                string id = d.Id;
                double a = GdGet(z.Dmg, id), p = GdGet(pz.Dmg, id);
                double ta = GdGet(z.Taken, id), tp = GdGet(pz.Taken, id);
                double la = GdGet(z.Last, id), lp = GdGet(pz.Last, id);
                double da = GdGet(z.Deaths, id), dp = GdGet(pz.Deaths, id);
                sa += a; sp += p;
                string mk = id == star ? "★ " : "";
                Console.WriteLine($"| {mk}{d.Name} | {a:0.0} | {p:0.0} | **{a - p:+0.0;-0.0;0.0}** "
                    + $"| {ta:0.0} | {tp:0.0} | **{ta - tp:+0.0;-0.0;0.0}** "
                    + $"| {la:0.00} | {lp:0.00} | **{la - lp:+0.00;-0.00;0.00}** "
                    + $"| {da:0.00} / {dp:0.00} |");
            }
            Console.WriteLine($"| **合計（カリ以外）** | {sa:0.0} | {sp:0.0} | **{sa - sp:+0.0;-0.0;0.0}** | | | | | | | |");
            Console.WriteLine($"| カリ本人 | {GdGet(z.Dmg, "kari"):0.0} | {GdGet(pz.Dmg, "kari_plain"):0.0} "
                + $"| **{GdGet(z.Dmg, "kari") - GdGet(pz.Dmg, "kari_plain"):+0.0;-0.0;0.0}** "
                + $"| {GdGet(z.Taken, "kari"):0.0} | {GdGet(pz.Taken, "kari_plain"):0.0} "
                + $"| **{GdGet(z.Taken, "kari") - GdGet(pz.Taken, "kari_plain"):+0.0;-0.0;0.0}** "
                + $"| {GdGet(z.Last, "kari"):0.00} | {GdGet(pz.Last, "kari_plain"):0.00} "
                + $"| **{GdGet(z.Last, "kari") - GdGet(pz.Last, "kari_plain"):+0.00;-0.00;0.00}** "
                + $"| {GdGet(z.Deaths, "kari"):0.00} / {GdGet(pz.Deaths, "kari_plain"):0.00} |");
            Console.WriteLine();
            Console.Out.Flush();
        }

        // --- 3. 波ごとの内訳 -------------------------------------------------------------------
        Console.WriteLine($"## 3. 波ごとの内訳（`Boost = {GdMain}`）");
        Console.WriteLine();
        foreach (var b in gdTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 発火 | 空振り | 渡した量 | 対象 | 入替 | 標消え | ★効き | ★被弾増 | ★早逝 | カリ寿命 | 決着T |");
            Console.WriteLine("|---|--:|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < gdStages.Count; w++)
            {
                var z = MeasureGd(b.F, gdStages[w].Enemy, new GoadRule(GdMain));
                var pz = MeasureGd(GdPlain(b.F), gdStages[w].Enemy, new GoadRule(GdMain));
                string sid = GdStarId(b.F, z);
                Console.WriteLine($"| {gdStages[w].Name} | {z.Win:0.0}% | {z.Fires:0.00} | {z.Idle:0.00} "
                    + $"| {z.Given:0.0} | {GdTop(z.TargetTo, 2)} | {z.Switches:0.00} | {z.MarkLost:0.00} "
                    + $"| **{GdGet(z.Dmg, sid) - GdGet(pz.Dmg, sid):+0.0;-0.0;0.0}** "
                    + $"| **{GdGet(z.Taken, sid) - GdGet(pz.Taken, sid):+0.0;-0.0;0.0}** "
                    + $"| **{GdGet(z.Last, sid) - GdGet(pz.Last, sid):+0.00;-0.00;0.00}** "
                    + $"| {GdGet(z.Last, "kari"):0.00} | {z.Turns:0.0} |");
                Console.Out.Flush();
            }
            Console.WriteLine();
        }
    }

    // --- 4. 掃引 ------------------------------------------------------------------------------
    if (gdMode.Length == 0 || gdMode == "sweep")
    {
        Console.WriteLine("## 4. 掃引（`Boost` 0 / 2 / 4 / 6）");
        Console.WriteLine();
        Console.WriteLine("**`Boost` は見返りの大きさを切るノブ。標の危険は `Boost` に依存しない**ので、");
        Console.WriteLine("第50期の `TargetCount`（総量と取り分が逆を向く打ち消し型）とは構造が違う。");
        Console.WriteLine("**全幅が小さいときの切り分けは「ノブが機構の計数を動かしたか」で付ける**");
        Console.WriteLine("——(a) ノブが機構を動かさない（第41・47期）／(b) 動かすが出力が小さい（第49期）／");
        Console.WriteLine("(c) 動かすが2つの量が打ち消す（第50期）。`渡した量` と `★効き` の列で判定する。");
        Console.WriteLine();
        Console.WriteLine("| 行 | Boost | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 渡した量 | ★効き | ★早逝 | カリ寿命 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in gdTargets)
        {
            var (_, pz) = GdAll(GdPlain(b.F), new GoadRule(GdMain));
            foreach (int bo in gdBoosts)
            {
                var (wins, z) = GdAll(b.F, new GoadRule(bo));
                string sid = GdStarId(b.F, z);
                Console.WriteLine($"| {b.Name} | {bo} | **{wins.Average():0.0}%** |{GdCells(wins)} "
                    + $"{z.Given:0.0} | **{GdGet(z.Dmg, sid) - GdGet(pz.Dmg, sid):+0.0;-0.0;0.0}** "
                    + $"| **{GdGet(z.Last, sid) - GdGet(pz.Last, sid):+0.00;-0.00;0.00}** "
                    + $"| {GdGet(z.Last, "kari"):0.00} |");
                Console.Out.Flush();
            }
            var pw = GdWins(GdPlain(b.F));
            Console.WriteLine($"| {b.Name} | 素体 | **{pw.Average():0.0}%** |{GdCells(pw)} — | — | — | — |");
        }
        Console.WriteLine();
        foreach (var b in gdTargets)
        {
            var vals = gdBoosts.Select(bo => GdAll(b.F, new GoadRule(bo)).Wins.Average()).ToList();
            Console.WriteLine($"- **{b.Name} の掃引の全幅: {vals.Max() - vals.Min():0.0}pt**"
                + $"（{string.Join(" / ", gdBoosts.Zip(vals, (t, v) => $"B{t} {v:0.0}%"))}）");
        }
        Console.WriteLine();
        if (gdMode == "sweep") return;
    }

    // --- 5. ソラ・ウツとの干渉 ----------------------------------------------------------------
    if (gdMode.Length == 0 || gdMode == "cross")
    {
        Console.WriteLine("## 5. 干渉（ソラ＝逸らし / ウツ＝逆しま との同居）");
        Console.WriteLine();
        Console.WriteLine("**潰さずに測る**（同じ通貨が駒によって逆符号になる実例）。");
        Console.WriteLine("`OnTurnStart` の発火順は **`ctx.AllUnits` の順＝味方はスロット昇順**なので、");
        Console.WriteLine("**ソラの席がカリより後ろなら、カリが付けた標をその手番のうちにソラが剥がす。**");
        Console.WriteLine("`標消え` が 0 でなければ打ち消しが起きている。**席を入れ替えた2版で確かめる。**");
        Console.WriteLine();
        Console.WriteLine("ウツは `AtkBonus` が正だと攻撃力が半減する（`PerverseTrait`）ので、");
        Console.WriteLine("**カリの強化はウツに対して害になる**。ただし `CurrentAttack` で選ぶので");
        Console.WriteLine("**渡した次のターンには選ばれにくくなる**（自己修正）。");
        Console.WriteLine();
        Console.WriteLine("> **`標消え` は2つの席順を区別できない**（測定の限界。報告書に記載）。");
        Console.WriteLine("> ソラは**どちらの席順でも「カリの次の発火」より前に1回走る**ので、");
        Console.WriteLine("> カリから見れば標は毎回消えている。**違いは「その手番のあいだ標が効いたか」**で、");
        Console.WriteLine("> それは `標消え` ではなく**勝率の差**にしか出ない。");
        Console.WriteLine();
        var cross = new (string Name, Formation F)[]
        {
            // ソラが後ろ（後3・席4）: カリ（中央・席2）→ ソラ の順で走るので、
            // カリが付けた標は**その手番のうちに**剥がされる ＝ 代金が消える
            ("ソラ同居・ソラが後（カリ中央 / ソラ後3）",
             Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gan,
                             center: UnitCatalog.Kari, back1: UnitCatalog.Dolga,
                             back3: UnitCatalog.Sora)),
            // ソラが前（前3・席1）: ソラ → カリ（席2）の順なので、
            // カリの標は**その手番のあいだ生きている** ＝ 代金を払う
            ("ソラ同居・ソラが先（ソラ前3 / カリ中央）",
             Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sora,
                             center: UnitCatalog.Kari, back1: UnitCatalog.Dolga,
                             back3: UnitCatalog.Gan)),
            // ウツ同居。**`逆しま (ネル×ウツ)` を土台にした診断のローカル**（`CompareBuilds()` は触らない）。
            // カリを前3（隣接＝中央・後3）に置き、隣接候補を ネル(攻7) と ウツ(攻9) だけにする
            // ——**ウツが初回に選ばれ、+4 された次のターンには CurrentAttack が 4 に落ちて
            // ネル(7) に負ける**（自己修正の実証）。土台を選んだのは、素朴に組んだ版
            // （ガルド・ガン・ザンの顔ぶれ）が 100/0/0/0/0 の床に落ちたため（第21期の飽和検査）。
            ("ウツ同居（カリ前3 / ウツ後3）",
             Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Kari,
                             center: UnitCatalog.Nel, back1: UnitCatalog.Gald,
                             back3: UnitCatalog.Utsu))
        };
        Console.WriteLine("| 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 発火 | 対象 | 標消え | ウツ | ウツ与ダメ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|--:|--:|--:|");
        foreach ((string name, Formation f) in cross)
        {
            var (wins, z) = GdAll(f, new GoadRule(GdMain));
            var (pwn, pz) = GdAll(GdPlain(f), new GoadRule(GdMain));
            Console.WriteLine($"| {name} |{GdCells(wins)} {wins.Average():0.0}% "
                + $"| {z.Fires:0.00} | {GdTop(z.TargetTo, 2)} | {z.MarkLost:0.00} | {z.ToPerverse:0.00} "
                + $"| {GdGet(z.Dmg, "utsu"):0.0} |");
            Console.WriteLine($"| ↳ 素体（同じ顔ぶれ・特性なし） |{GdCells(pwn)} {pwn.Average():0.0}% "
                + $"| — | — | — | — | {GdGet(pz.Dmg, "utsu"):0.0} |");
            Console.WriteLine($"| ↳ **帰属（同居版 − 素体）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - pwn[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - pwn.Average():+0.0;-0.0;0.0}** | | | | | "
                + $"**{GdGet(z.Dmg, "utsu") - GdGet(pz.Dmg, "utsu"):+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        if (gdMode == "cross") return;
    }

    // --- 6. 席の分散 --------------------------------------------------------------------------
    if (gdMode.Length == 0 || gdMode == "seats")
    {
        Console.WriteLine("## 6. 席の分散（`seats2` の写し・受け入れ基準11）");
        Console.WriteLine();
        Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 + 最下位 を seed 0..199 で測り直し。");
        Console.WriteLine("**カリは隣接を読むが「隣に何人いるか」ではなく「隣に誰がいるか」を読む**");
        Console.WriteLine("（隣接する `CurrentAttack` の最大を取り、**その1体に効果を当てる**）ので、");
        Console.WriteLine("第45期の分類ではヒサ（囃し立て）と同じ側——**分散する側に出るかを見る。**");
        Console.WriteLine("比較先は 第45期の 隣接を読む駒 85% / 隣接も列も読まない駒 65% / ヒサ 62%。");
        Console.WriteLine("**列で決まる駒には 2値（前列 / それ以外）も併記する**（第50期の残件）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（3値） | 最頻率 | 2値(前列/以外) | 幅 | 現行の順位 | 1位との差 |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|--:|--:|--:|");
        foreach (var b in gdTargets)
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
                foreach (EnemyCatalog.Stage st in gdStages)
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
                foreach (EnemyCatalog.Stage st in gdStages)
                {
                    int wins = 0;
                    for (int seed = 0; seed < GdSeeds; seed++)
                        if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    avg += wins * 100.0 / GdSeeds;
                }
                return avg / gdStages.Count;
            }

            var verified = pool.Select(i => (Idx: i, Avg: Avg(perms[i]))).OrderByDescending(x => x.Avg).ToList();
            double width = verified[0].Avg - verified[^1].Avg;
            var top5 = verified.Take(5).ToList();
            int curRank = verified.FindIndex(x => x.Idx == curIdx) + 1;
            double curGap = verified[0].Avg - verified.First(x => x.Idx == curIdx).Avg;

            foreach (UnitDef d in members)
            {
                int bestSlot = -1;
                foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                    if (ReferenceEquals(dd, d)) bestSlot = slot;
                int mid = 0, fcorner = 0, bcorner = 0, front = 0;
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
                            if (FormationRules.RowOf(slot) == Row.Front) front++;
                        }
                int bdeg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
                int top = Math.Max(mid, Math.Max(fcorner, bcorner));
                Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                    + $"| 前角{fcorner} / 中央{mid} / 後角{bcorner} | {top * 100 / 5}% "
                    + $"| {front} / {5 - front} | {width:0.0}pt "
                    + $"| {curRank} / {verified.Count} | {curGap:0.0}pt |");
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine("### カリ1枚だけを振った5変種（他の4枚は元の相対順のまま詰める）");
        Console.WriteLine();
        Console.WriteLine("**`発火` と `空振り` が席でどれだけ動くか**を見る——カリは隣接に候補が");
        Console.WriteLine("いなければ何もしないので、**次数がそのまま供給の上限になる**はず。");
        Console.WriteLine();
        Console.WriteLine("| 行 | カリの席 | 次数 | 列 | 発火 | 空振り | 渡した量 | 対象 | 入替 | カリ寿命 | 平均勝率 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|---|--:|--:|--:|");
        foreach (var b in gdTargets)
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                Formation g = GdSeat(b.F, seat);
                int deg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(seat, i)) deg++;
                var (wins, z) = GdAll(g, new GoadRule(GdMain));
                Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {deg} | {FormationRules.RowOf(seat)} "
                    + $"| {z.Fires:0.00} | {z.Idle:0.00} | {z.Given:0.0} | {GdTop(z.TargetTo, 2)} "
                    + $"| {z.Switches:0.00} | {GdGet(z.Last, "kari"):0.00} | {wins.Average():0.0}% |");
                Console.Out.Flush();
            }
        Console.WriteLine();
        if (gdMode == "seats") return;
    }

    // --- 7. 別 seed の追試 --------------------------------------------------------------------
    if (gdMode == "alt")
    {
        const int AltFrom = 200, AltTo = 600;
        Console.WriteLine("## 7. 別 seed の追試（seed 200..599・機構の帰属）");
        Console.WriteLine();
        double[] Cells(Formation f, GoadRule? rule)
        {
            var v = new double[gdStages.Count];
            for (int w = 0; w < gdStages.Count; w++)
            {
                int wins = 0;
                for (int seed = AltFrom; seed < AltTo; seed++)
                    if (BattleEngine.Run(f, gdStages[w].Enemy, seed, false, goad: rule).PlayerWon) wins++;
                v[w] = wins * 100.0 / (AltTo - AltFrom);
            }
            return v;
        }
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in gdTargets)
        {
            var t = Cells(b.F, new GoadRule(GdMain));
            var nm = Cells(b.F, new GoadRule(GdMain, false));
            var pl = Cells(GdPlain(b.F), null);
            Console.WriteLine($"| {b.Name} | B{GdMain} |{GdCells(t)} {t.Average():0.0}% |");
            Console.WriteLine($"| | 標なし |{GdCells(nm)} {nm.Average():0.0}% |");
            Console.WriteLine($"| | 素体 |{GdCells(pl)} {pl.Average():0.0}% |");
            Console.WriteLine($"| | **帰属（追試）** | "
                + string.Concat(Enumerable.Range(0, t.Length).Select(i => $"{t[i] - pl[i]:+0.0;-0.0;0.0} |"))
                + $" **{t.Average() - pl.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **代金（追試）** | "
                + string.Concat(Enumerable.Range(0, t.Length).Select(i => $"{t[i] - nm[i]:+0.0;-0.0;0.0} |"))
                + $" **{t.Average() - nm.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    // --- 8. 配置の追試 ------------------------------------------------------------------------
    if (gdMode == "confirm")
    {
        const int CfFrom = 200, CfTo = 600;
        Console.WriteLine("## 8. 配置の追試（`confirm`・seed 200..599）");
        Console.WriteLine();
        Console.WriteLine("**選定に使っていない seed 帯**で測り直す。採否閾値は **5.0pt**（第46期）。");
        Console.WriteLine("**1位の配置ではなく次数で読む**（第45期の残件 D）。");
        Console.WriteLine("**`発火` と `空振り` の列を必ず見る**——配置探索が機構を無効化する席を");
        Console.WriteLine("選んでいたらそこは採用しない（第49期の業改が引き取り 0.00 回/戦になった件）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | カリの席 | 次数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 現行との差 | 発火 | 空振り |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in gdTargets)
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
                foreach (EnemyCatalog.Stage st in gdStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));

            (double[] Cells, double Fires, double Idle) Measure(Formation f)
            {
                var v = new double[gdStages.Count];
                double fires = 0, idle = 0; int n = 0;
                for (int w = 0; w < gdStages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = CfFrom; seed < CfTo; seed++)
                    {
                        var r = BattleEngine.Run(f, gdStages[w].Enemy, seed, false);
                        if (r.PlayerWon) wins++;
                        fires += r.GoadFires; idle += r.GoadIdle; n++;
                    }
                    v[w] = wins * 100.0 / (CfTo - CfFrom);
                }
                return (v, fires / n, idle / n);
            }
            int Seat(Formation f)
            {
                foreach ((int slot, UnitDef d) in f.Occupied())
                    if (ReferenceEquals(d, UnitCatalog.Kari)) return slot;
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
                + $"|{GdCells(cur.Cells)} **{cur.Cells.Average():0.0}%** | — | {cur.Fires:0.00} | {cur.Idle:0.00} |");
            Console.Out.Flush();
            foreach (int idx in order.Take(5))
            {
                if (idx == curIdx) continue;
                var v = Measure(perms[idx]);
                int seat = Seat(perms[idx]);
                Console.WriteLine($"| {b.Name} | 粗探索 {order.IndexOf(idx) + 1}位 | {FormationRules.SeatNames[seat]} | {Deg(seat)} "
                    + $"|{GdCells(v.Cells)} {v.Cells.Average():0.0}% "
                    + $"| **{v.Cells.Average() - cur.Cells.Average():+0.0;-0.0;0.0}pt** | {v.Fires:0.00} | {v.Idle:0.00} |");
                Console.WriteLine($"|   ↳ 配置 | {string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} | | | | | | | | | | | |");
                Console.Out.Flush();
            }
            Console.WriteLine($"|   ↳ 現行の順位 | **粗探索 {order.IndexOf(curIdx) + 1}位 / {perms.Count}通り** | | | | | | | | | | | |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    return;
}
}
