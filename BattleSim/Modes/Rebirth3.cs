using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// =====================================================================================
// rebirth3 モード（第183期） —— B群の転生 最後の2枚：継ぎ接ぎのヴェル／疫みのラウ
//
// 指示書は design/PHASE183_REBIRTH_B3_SPEC.md ／ 報告は design/PHASE183_REBIRTH_B3.md。
//
// **数値の線は1本も置かない**（第178・180期から引き継ぐ約束）。
// 回帰確認（2枚のどちらも含まない行が動かないこと）と変化表・帳簿だけで、採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 rebirth3 phase0  # Q0-1〜Q0-7 を実装から引き直す
//     dotnet run --project BattleSim -c Release 0 rebirth3 run     # 1枚ずつの対照・漏れを外した対照・(G2)・勝ち方
//     dotnet run --project BattleSim -c Release 0 rebirth3 ledger  # 2枚の帳簿
//     dotnet run --project BattleSim -c Release 0 rebirth3 check [採用前のbalance.md]  # 自己検査
//
// 旧版の対照は「その駒だけ第182期の姿に戻したローカルの `UnitDef`」で作る（第180期と同じ作法）。
// **`Run` の引数は1本も増やしていない**——版の切り替えは `Traits` / `Actions` の差し替えだけ。
// =====================================================================================

static class Rebirth3Diag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": StageRun(); return;
            case "ledger": Ledger(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("rebirth3: モードは phase0 / run / ledger / check。");
                return;
        }
    }

    // =================================================================================
    // 旧版の駒（第182期の姿）。**`UnitCatalog` は触らず、診断のローカルで作る**
    // =================================================================================

    static UnitDef Remake(UnitDef d, TraitId[] traits, IReadOnlyList<UnitAction>? actions,
                          string? plus = null, string? minus = null) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Pattern = d.Pattern, Advances = d.Advances, Actions = actions,
        Traits = traits, PlusText = plus ?? d.PlusText, MinusText = minus ?? d.MinusText, Flavor = d.Flavor
    };

    /// <summary>第182期のヴェル（蘇生だけ・手番は素の攻撃）。</summary>
    static readonly UnitDef VelOld = Remake(UnitCatalog.Vel, new[] { TraitId.Reviver }, null,
        "倒れた味方を戦線に戻す（2回まで）", "1回縫うごとに自分の最大HPが半分になる");

    /// <summary>第182期のラウ（疫みだけ）。</summary>
    static readonly UnitDef RauOld = Remake(UnitCatalog.Rau, new[] { TraitId.Contagion }, null,
        "毒に侵された駒が倒れると、残りの敵へ毒が飛ぶ（味方の死骸からも飛ぶ）",
        "自分では毒を与えられない。撒いた毒は傷を負った相手には深く入る");

    /// <summary>触れてうつすは載せるが漏れだけ外した版（<c>LeakPerTouch = 0</c> の対照・`yP`）。</summary>
    static readonly UnitDef RauNoLeak = Remake(UnitCatalog.Rau, new[] { TraitId.Contagion, TraitId.Touch }, null);

    static Formation Swap(Formation f, string id, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == id ? to : d;
        return g;
    }

    /// <summary>2枚とも第182期の姿に戻す（＝採用前の `docs/balance.md` を再現する版）。</summary>
    static Formation Old(Formation f) => Swap(Swap(f, "vel", VelOld), "rau", RauOld);

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static bool Mine(Formation f) => Has(f, "vel") || Has(f, "rau");
    static string Who(Formation f) => Has(f, "vel") && Has(f, "rau") ? "両方" : Has(f, "vel") ? "ヴェル" : Has(f, "rau") ? "ラウ" : "";

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第183期 `rebirth3 phase0` —— 前提を実装から引き直す");
        Console.WriteLine();
        if (!ParryScan.Init()) return;
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
        string models = ParryScan.Read(Path.Combine("BattleCore", "Models.cs"));

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1. 在席と同席（`Presets.Compare` 61 行 ＋ 交差帯 12 行）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | ヴェル | ラウ | ヴィオ | ムド | リィカ | ゾト | ベニ | ミオ | トモ | ヴェルの隣 | ラウの隣 |");
        Console.WriteLine("|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|---|---|");
        int nVel = 0, nRau = 0, nBoth = 0, nNone = 0;
        foreach ((string band, string name, Formation f) in Bands())
        {
            if (!Mine(f)) { if (band == "compare") nNone++; continue; }
            if (band == "compare") { if (Has(f, "vel")) nVel++; if (Has(f, "rau")) nRau++; if (Has(f, "vel") && Has(f, "rau")) nBoth++; }
            string M(string id) => Has(f, id) ? "●" : "";
            Console.WriteLine("| " + name + " | " + band + " | " + M("vel") + " | " + M("rau") + " | " + M("vio") + " | "
                              + M("mudo") + " | " + M("rica") + " | " + M("zoto") + " | " + M("beni") + " | " + M("mio")
                              + " | " + M("tomo") + " | " + Neigh(f, "vel") + " | " + Neigh(f, "rau") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- `compare` 61 行: ヴェル在席 **" + nVel + "** ／ ラウ在席 **" + nRau + "** ／ **両方 " + nBoth + " 行**"
                          + "（両方の行は1枚ずつの切り分けから外し、「両方」として別に書く）／ **どちらも含まない " + nNone + " 行**（回帰の分母）");
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2. 手番で撃つ既存の作法と、`Actions = [Skill]` にしたときに動く読み手");
        Console.WriteLine();
        Console.WriteLine("| 駒 | `Actions` | 札 |");
        Console.WriteLine("|---|---|---|");
        foreach (UnitDef d in UnitCatalog.Everyone)
            if (d.Actions is { Count: > 0 } acts && acts.Any(a => a.Kind == ActionKind.Skill))
                Console.WriteLine("| " + d.Name + " | " + string.Join(" → ", acts.Select(a => a.Kind.ToString())) + " | "
                                  + string.Join(", ", d.Traits) + " |");
        Console.WriteLine();
        Console.WriteLine("`UnitDef.Actions` を読む箇所（`BattleCore`・コメント以外）:");
        Console.WriteLine();
        foreach ((string file, string txt) in new[] { ("Traits.cs", traits), ("BattleEngine.cs", engine), ("Models.cs", models) })
        {
            string[] ls = txt.Replace("\r", "").Split('\n');
            for (int i = 0; i < ls.Length; i++)
            {
                string t = ls[i].TrimStart();
                if (t.StartsWith("//")) continue;
                if (ls[i].Contains("Def.Actions") || ls[i].Contains("d.Actions"))
                    Console.WriteLine("- `" + file + ":" + (i + 1) + "` —— `" + t + "`");
            }
        }
        Console.WriteLine();
        Console.WriteLine("**蘇生との干渉**: 蘇生（`ReviverTrait.OnAllyDeath`）は手番を1度も通らない（死亡通知の順序で走る）。"
                          + "縫い合わせは `OnAction`（手番の `Skill`）だけ。**2本は別の入口**で、`charges` / `sewn` のキーも共有しない。"
                          + "**ただし `Actions` を持つと尾灯の静的な濾し（`TaillightTrait.AttacksInOwnTurn`）が偽を返す**"
                          + "——トモとヴェルが同席する行があれば、トモの灯はヴェルを飛ばすようになる（上の表の「トモ」列）。");
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3. 最大HPを書き換える既存の経路");
        Console.WriteLine();
        foreach ((string file, string txt) in new[] { ("Traits.cs", traits), ("BattleEngine.cs", engine) })
        {
            string[] ls = txt.Replace("\r", "").Split('\n');
            for (int i = 0; i < ls.Length; i++)
            {
                string t = ls[i].TrimStart();
                if (t.StartsWith("//")) continue;
                if (Regex.IsMatch(ls[i], @"\.MaxHp\s*(=|-=|\+=)[^=]"))
                    Console.WriteLine("- `" + file + ":" + (i + 1) + "` —— `" + t + "`");
            }
        }
        Console.WriteLine();
        Console.WriteLine("**既存の経路は「`MaxHp` を直に書き、`Hp` を新しい最大値で頭打ちにする」形だけ**（ヴェル自身の半減）。"
                          + "縫い跡も同じ書き方に揃えた。**蘇生の 40% は `dead.MaxHp * ReviveHpPercent / 100` なので、"
                          + "縫われた後に倒れた駒は減った最大HPから計算される**（指示書どおり、それで良い）。");
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4. 敵陣でも `AreAdjacent` は同じ表で引けるか");
        Console.WriteLine();
        Console.WriteLine("`FormationRules.AreAdjacent(int a, int b)` は**スロット番号しか受け取らない**（陣営を見ない）"
                          + "——敵陣も同じ 9 席の X 字なので、標的の席から隣を引けばそのまま敵の隣になる。"
                          + "標的の隣の席の数（編成枠 0〜4）: "
                          + string.Join(" ／ ", Enumerable.Range(0, 5).Select(s => s + "→" + Enumerable.Range(0, 9).Count(t => FormationRules.AreAdjacent(s, t)))) + "。");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5. ラウの現行のマイナス「撒いた毒は傷を負った相手には深く入る」の所在");
        Console.WriteLine();
        int iSoak = engine.IndexOf("if (Soak.Poison && wounded)");
        Console.WriteLine("- **ラウの特性（`ContagionTrait`）の中には1行も無い。** 実体は毒の窓口 `BattleContext.Poison` の"
                          + "滲み則（`SoakRule.Poison`・第90期）で、" + (iSoak >= 0 ? "**全経路に等しくかかる**" : "**（見つからない——要確認）**")
                          + "——瘴気（グザ）・毒撃（スィド）・吐き戻し（ヴィオ）の毒も同じように深く入る。");
        Console.WriteLine("- **説明文から外すだけで挙動は1ビットも変わらない**（規則は engine にあり、ラウ固有ではない）。"
                          + "傷の供給は第122期に巻き込み則を止めてからほぼ 0 なので（第139期の帳簿で 0.646/戦）、実害も薄い。"
                          + "**だから規則は触らず、説明文だけ新しいマイナス（漏れ）に差し替えた。**");
        Console.WriteLine("- 新しい毒の経路を2本足したので `SoakRouteCount` を 7 → 9、燃焼の添字を 6 → 8 にずらした"
                          + "（ずらさないと新しい経路の添字が燃焼と重なる）。");
        Console.WriteLine();

        // ---- Q0-6 ----
        Console.WriteLine("## Q0-6. 漏れ（手番中）と澱み喰いの吸い上げ（`OnTurnStart`）の順序");
        Console.WriteLine();
        int iTick = engine.IndexOf("ctx.TickStatuses();");
        int iStart = engine.IndexOf(".OnTurnStart(ctx, u)");
        Console.WriteLine("- ターンの順序は `TickStatuses` → `OnTurnStart` → 行動順ループ（"
                          + (iTick >= 0 && iStart >= 0 && iTick < iStart ? "実装で確認" : "**要確認**") + "）。");
        Console.WriteLine("- 漏れは行動順ループの中（ラウの手番の `OnAfterAttack`）で付く → **次のターン頭にまず1度刻み**（1層なら 1 点）"
                          + " → **その直後の `OnTurnStart` でヴィオが吸う** → ヴィオの腹に入り、ヴィオの手番の命中で吐き戻す。");
        Console.WriteLine("- **漏れた毒を吸えるのはヴィオが盤上に生きているときだけ**で、ヴィオは**隣接を問わず味方全員**から吸う。");
        Console.WriteLine();

        // ---- Q0-7 ----
        Console.WriteLine("## Q0-7. 過去の類似測定（`design/` の grep）");
        Console.WriteLine();
        string root = ParryScan.Root!;
        foreach ((string label, string pat) in new[]
                 {
                     ("手番で回復", "手番で(回復|繕|癒)"),
                     ("最大HPを削る", "最大HP(が|を)(減|削|半分)"),
                     ("隣接へ毒を移す", "隣接(する)?(味方|敵)に毒を移す|隣の敵(に|へ).{0,6}毒"),
                     ("疫みの拡散先の却下案", "拡散先を敵味方"),
                 })
        {
            var hits = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(root, "design"), "*.md"))
            {
                string txt = File.ReadAllText(file);
                int c = Regex.Matches(txt, pat).Count;
                if (c > 0) hits.Add(Path.GetFileName(file) + "(" + c + ")");
            }
            Console.WriteLine("- **" + label + "**（`" + pat + "`）: " + (hits.Count == 0 ? "0 件" : string.Join(" ", hits.Take(12)) + (hits.Count > 12 ? " ほか " + (hits.Count - 12) + " 件" : "")));
        }
        Console.WriteLine("- `ContagionTrait` の doc に**却下案が2つ**残っている——「拡散先を敵味方の区別なしにする」と"
                          + "「隣接する味方に毒を移す（毎ターン+1 / 拡散時に同量）」。後者は**この期の漏れと同じ向き**で、"
                          + "当時は「量が大きすぎ、ヴィオを入れるかどうかの二択になる」で落ちた（ヴィオ在席で +7pt）。"
                          + "**今回の漏れは『うつしたときだけ・1層』なので量は当時より小さい。**");
        Console.WriteLine();
    }

    static IEnumerable<(string Band, string Name, Formation F)> Bands()
    {
        foreach ((string n, Formation f) in CompareBuilds()) yield return ("compare", n, f);
        foreach ((string n, Formation f) in CrossBuilds()) yield return ("交差帯", n, f);
    }

    static string Neigh(Formation f, string id)
    {
        var me = f.Occupied().FirstOrDefault(o => o.Def.Id == id);
        if (me.Def is null) return "";
        return string.Join("・", f.Occupied().Where(o => FormationRules.AreAdjacent(me.Slot, o.Slot)).Select(o => Short(o.Def)));
    }

    static string Short(UnitDef d)
    {
        int i = d.Name.LastIndexOf('の');
        return i >= 0 && i + 1 < d.Name.Length ? d.Name[(i + 1)..] : d.Name;
    }

    // =================================================================================
    // run
    // =================================================================================

    static void StageRun()
    {
        Console.WriteLine("# 第183期 `rebirth3 run` —— 1枚ずつの対照（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**旧** ＝ 2枚とも第182期の姿（採用前の `docs/balance.md` を再現する版）。"
                          + "**ヴェルのみ** ＝ ヴェルだけ新・ラウは旧 ／ **ラウのみ** ＝ ラウだけ新・ヴェルは旧 ／ **新** ＝ この期の既定。"
                          + "第2〜5波・seed 0..199。");
        Console.WriteLine();

        Console.WriteLine("## 表A. 2枚のどちらかを含む行（`compare` 61 行）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 主 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | Δ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        var oldW = new Dictionary<string, double[]>();
        var newW = new Dictionary<string, double[]>();
        int still = 0, rows = 0;
        double so = 0, sn = 0;
        foreach ((string name, Formation f) in CompareBuilds())
        {
            double[] a = Rates(Old(f)), b = Rates(f);
            oldW[name] = a; newW[name] = b;
            if (!Mine(f)) { if (Enumerable.Range(0, 5).Any(i => Math.Abs(a[i] - b[i]) > 0.001)) still++; continue; }
            rows++; so += Mean25(a); sn += Mean25(b);
            Console.WriteLine("| " + name + " | " + Who(f) + " | 旧 | " + Cells(a) + " | " + Mean25(a).ToString("F1") + " | — |");
            Console.WriteLine("| | | **新** | " + Cells(b) + " | " + Mean25(b).ToString("F1") + " | **" + D(Mean25(b) - Mean25(a)) + "** |");
        }
        Console.WriteLine();
        Console.WriteLine("- 2枚のどちらかを含む行: **" + rows + " 行**（旧の平均 " + (so / rows).ToString("F1") + "% → 新 " + (sn / rows).ToString("F1") + "%）");
        Console.WriteLine("- **2枚のどちらも含まないのに動いた行: " + still + " 行**（0 が正）");
        Console.WriteLine();

        Console.WriteLine("## 表B. 1枚ずつの対照と、漏れを外した対照（第2〜5波の平均）");
        Console.WriteLine();
        Console.WriteLine("**漏れの代金は新しいプラスの上で測る**（R289）——`代金 ＝ 新 − 漏れなし`（どちらも触れてうつすを持つ）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 主 | 旧 | ヴェルのみ | ラウのみ | 漏れなし（ラウ） | 新 | ヴェルの寄与 | ラウの寄与 | 漏れの代金 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds().Concat(CrossBuilds().Select(c => (c.Name + "（交差帯）", c.F))))
        {
            if (!Mine(f)) continue;
            double o = Avg25(Old(f));
            double v = Avg25(Swap(f, "rau", RauOld));
            double r = Avg25(Swap(f, "vel", VelOld));
            double nl = Has(f, "rau") ? Avg25(Swap(f, "rau", RauNoLeak)) : double.NaN;
            double n = Avg25(f);
            Console.WriteLine("| " + name + " | " + Who(f) + " | " + o.ToString("F1") + " | "
                              + (Has(f, "vel") ? v.ToString("F1") : "—") + " | " + (Has(f, "rau") ? r.ToString("F1") : "—") + " | "
                              + (double.IsNaN(nl) ? "—" : nl.ToString("F1")) + " | " + n.ToString("F1") + " | "
                              + (Has(f, "vel") ? D(v - o) : "—") + " | " + (Has(f, "rau") ? D(r - o) : "—") + " | "
                              + (double.IsNaN(nl) ? "—" : D(n - nl)) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表C. 交差帯 12 行");
        Console.WriteLine();
        int cStill = 0, cMine = 0;
        foreach ((string name, Formation f) in CrossBuilds())
        {
            double[] a = Rates(Old(f)), b = Rates(f);
            bool diff = Enumerable.Range(0, 5).Any(i => Math.Abs(a[i] - b[i]) > 0.001);
            if (!Mine(f)) { if (diff) cStill++; continue; }
            cMine++;
            Console.WriteLine("- " + name + "（" + Who(f) + "）: 旧 " + Cells(a) + " → 新 " + Cells(b) + "（平均 " + D(Mean25(b) - Mean25(a)) + "）");
        }
        Console.WriteLine();
        Console.WriteLine("- 交差帯で2枚のどちらかを含む行: **" + cMine + " 行** ／ **含まないのに動いた行: " + cStill + " 行**（0 が正）");
        Console.WriteLine();

        // ---- (G2) ----
        Console.WriteLine("## 表D. (G2) の「壊れ／制約」の分解（`compare` 61 行の分母・**報告のみ**）");
        Console.WriteLine();
        var dropped = new List<(string Name, int Wave, double Delta)>();
        foreach ((string name, Formation f) in CompareBuilds())
            for (int w = 1; w < 5; w++)
                if (newW[name][w] - oldW[name][w] <= -10.0) dropped.Add((name, w, newW[name][w] - oldW[name][w]));
        if (dropped.Count == 0) Console.WriteLine("**−10.0pt 以上落ちたセルは 0 件。**");
        else
        {
            Console.WriteLine("| 行 | 波 | Δ |");
            Console.WriteLine("|---|--:|--:|");
            foreach (var d in dropped) Console.WriteLine("| " + d.Name + " | 第" + (d.Wave + 1) + "波 | " + D(d.Delta) + " |");
            Console.WriteLine();
            Console.WriteLine("落ちた行に含まれる駒それぞれの「その駒を含む**他の**行」の第2〜5波平均の変化:");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 他の行の数 | 平均変化 | 判定 |");
            Console.WriteLine("|---|--:|--:|---|");
            var units = new SortedSet<string>();
            var builds = CompareBuilds().ToList();
            foreach (var d in dropped.Select(x => x.Name).Distinct())
                foreach ((int _, UnitDef u) in builds.First(b => b.Name == d).F.Occupied()) units.Add(u.Id);
            foreach (string id in units)
            {
                var others = builds.Where(b => Has(b.F, id) && !dropped.Any(x => x.Name == b.Name)).ToList();
                if (others.Count == 0) { Console.WriteLine("| " + UnitCatalog.ById(id).Name + " | 0 | — | 分解が成立しない |"); continue; }
                double avg = others.Average(b => Mean25(newW[b.Name]) - Mean25(oldW[b.Name]));
                string verdict = avg <= -3.0 ? "**壊れ**" : Math.Abs(avg) < 3.0 ? "制約" : "（上振れ）";
                Console.WriteLine("| " + UnitCatalog.ById(id).Name + " | " + others.Count + " | " + D(avg) + " | " + verdict + " |");
            }
        }
        Console.WriteLine();

        // ---- 勝ち方 ----
        Console.WriteLine("## 表E. 勝ち方（第2〜5波・勝った試行だけ）");
        Console.WriteLine();
        Console.WriteLine("`残存` ＝ 勝った試行の生存数の平均 ／ `全滅勝ち` ＝ 勝った試行のうち生存1体の割合（`docs/chain.md` と同じ定義。**分母は第2〜5波**）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 主 | 残存 旧 | 残存 新 | 全滅勝ち 旧 | 全滅勝ち 新 | 決着T 旧 | 決着T 新 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Mine(f)) continue;
            var a = Quality(Old(f)); var b = Quality(f);
            Console.WriteLine("| " + name + " | " + Who(f) + " | " + a.Surv.ToString("F2") + " | " + b.Surv.ToString("F2") + " | "
                              + a.Edge.ToString("F1") + "% | " + b.Edge.ToString("F1") + "% | " + a.T.ToString("F2") + " | " + b.T.ToString("F2") + " |");
        }
        Console.WriteLine();

        // ---- 指標 ----
        Console.WriteLine("## 表F. 全体の指標");
        Console.WriteLine();
        var primary = Baseline.PrimaryRows;
        double[] all = new double[5], prim = new double[5];
        foreach ((string name, Formation _) in CompareBuilds())
            for (int w = 0; w < 5; w++) all[w] += newW[name][w] / 61.0;
        int pc = 0;
        foreach (string p in primary) if (newW.TryGetValue(p, out double[]? pw)) { pc++; for (int w = 0; w < 5; w++) prim[w] += pw[w]; }
        for (int w = 0; w < 5; w++) prim[w] /= Math.Max(1, pc);
        Console.WriteLine("- 全61行: " + string.Join(" / ", all.Select(x => x.ToString("F1"))));
        Console.WriteLine("- 主判定" + pc + "行: " + string.Join(" / ", prim.Select(x => x.ToString("F1")))
                          + "（歯止め 33.2% との余裕 " + D(prim[4] - 33.2) + "pt）");
        int infoOld = 0, infoNew = 0, hiOld = 0, hiNew = 0;
        foreach ((string name, Formation _) in CompareBuilds())
        {
            for (int w = 1; w < 5; w++) { if (oldW[name][w] > 0 && oldW[name][w] < 100) infoOld++; if (newW[name][w] > 0 && newW[name][w] < 100) infoNew++; }
            if (oldW[name][4] > 95) hiOld++; if (newW[name][4] > 95) hiNew++;
        }
        Console.WriteLine("- 情報セル（`0 < x < 100`・第2〜5波）: " + infoOld + " → **" + infoNew + "** ／ 第五波 95% 超: " + hiOld + " → **" + hiNew + "**");
        Console.WriteLine();
    }

    static (double Surv, double Edge, double T) Quality(Formation f)
    {
        long wins = 0, surv = 0, edge = 0, turns = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                if (!r.PlayerWon) continue;
                wins++; surv += r.PlayerSurvivors; turns += r.Turns;
                if (r.PlayerSurvivors == 1) edge++;
            }
        return wins == 0 ? (0, 0, 0) : ((double)surv / wins, 100.0 * edge / wins, (double)turns / wins);
    }

    // =================================================================================
    // ledger
    // =================================================================================

    static void Ledger()
    {
        Console.WriteLine("# 第183期 `rebirth3 ledger` —— 2枚の帳簿（第2〜5波の平均・1戦あたり）");
        Console.WriteLine();

        Console.WriteLine("## 表G. 縫い合わせ（ヴェル在席の行）");
        Console.WriteLine();
        Console.WriteLine("`縫い` 縫った回数 ／ `封じ` 渇きに封じられた回数 ／ `殴り` 傷ついた隣人がいなくて殴った回数 ／"
                          + " `回復` 実際に増えた HP ／ `縫い跡` 削った最大HP ／ `蘇生` は `Revive` を呼んだ回数（旧 → 新）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 縫い | 封じ | 殴り | 回復 | 縫い跡 | 蘇生 旧 | 蘇生 新 | ヴェルの生存T 旧 → 新 | 縫われた駒（回/戦・失った最大HP/戦） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        var stitchedBy = new Dictionary<string, (double N, double Scar)>();
        double gF = 0, gH = 0, gS = 0; int gRows = 0;
        foreach ((string name, Formation f) in CompareBuilds().Concat(CrossBuilds().Select(c => (c.Name + "（交差帯）", c.F))))
        {
            if (!Has(f, "vel")) continue;
            double fires = 0, sealedN = 0, swings = 0, healed = 0, scar = 0, revNew = 0, lifeNew = 0; int n = 0;
            var local = new Dictionary<string, (double N, double Scar)>();
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    n++;
                    if (r.TallyByUnit.TryGetValue("vel", out UnitTally? t))
                    {
                        fires += t.StitchFires; sealedN += t.StitchSealed; swings += t.StitchSwings;
                        healed += t.StitchHealed; scar += t.StitchScarDealt; revNew += t.RevivesGiven;
                        lifeNew += t.LastActiveTurn;
                    }
                    foreach ((int _, UnitDef d) in f.Occupied())
                        if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? u) && u.StitchedTimes > 0)
                        {
                            local.TryGetValue(d.Id, out var c); local[d.Id] = (c.N + u.StitchedTimes, c.Scar + u.StitchScarTaken);
                        }
                }
            double revOld = 0, lifeOld = 0;
            Formation of = Old(f);
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(of, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    if (r.TallyByUnit.TryGetValue("vel", out UnitTally? t)) { revOld += t.RevivesGiven; lifeOld += t.LastActiveTurn; }
                }
            foreach (var kv in local)
            {
                stitchedBy.TryGetValue(kv.Key, out var c); stitchedBy[kv.Key] = (c.N + kv.Value.N / n, c.Scar + kv.Value.Scar / n);
            }
            gF += fires / n; gH += healed / n; gS += scar / n; gRows++;
            string who = string.Join(" ／ ", local.OrderByDescending(kv => kv.Value.N)
                .Select(kv => Short(UnitCatalog.ById(kv.Key)) + " " + (kv.Value.N / n).ToString("F2") + "・" + (kv.Value.Scar / n).ToString("F1")));
            Console.WriteLine("| " + name + " | " + (fires / n).ToString("F2") + " | " + (sealedN / n).ToString("F2") + " | "
                              + (swings / n).ToString("F2") + " | " + (healed / n).ToString("F1") + " | " + (scar / n).ToString("F1") + " | "
                              + (revOld / n).ToString("F2") + " | " + (revNew / n).ToString("F2") + " | "
                              + (lifeOld / n).ToString("F2") + " → " + (lifeNew / n).ToString("F2") + " | " + (who.Length == 0 ? "—" : who) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- ヴェル在席 " + gRows + " 行の平均: 縫い **" + (gF / gRows).ToString("F2") + " 回/戦** ／ 回復 **"
                          + (gH / gRows).ToString("F1") + "** ／ 縫い跡 **" + (gS / gRows).ToString("F1") + "**");
        Console.WriteLine("- 縫われた駒の内訳（行をまたいだ合計・回/戦）: " + string.Join(" ／ ", stitchedBy.OrderByDescending(kv => kv.Value.N)
            .Select(kv => UnitCatalog.ById(kv.Key).Name + " " + kv.Value.N.ToString("F2") + "（最大HP −" + kv.Value.Scar.ToString("F1") + "）")));
        Console.WriteLine();

        Console.WriteLine("## 表H. 触れてうつす・漏れ（ラウ在席の行）");
        Console.WriteLine();
        Console.WriteLine("`うつし` 発火した回数 ／ `延べ` 写した体数 ／ `層` 写した総量 ／ `空振り` 毒はあったが隣に敵がいなかった ／"
                          + " `漏れ` 隣の味方へ付けた総量 ／ `吸い(漏れ)` ヴィオが吸った層のうち漏れ由来の上限 ／"
                          + " `敵の毒` ＝ 敵が毒の刻みで受けた総量（旧 → 新）／ `味方の毒` 同じく味方。");
        Console.WriteLine();
        Console.WriteLine("| 行 | うつし | 延べ | 層 | 空振り | 漏れ | 吸い(漏れ) | 敵の毒 旧 → 新 | 味方の毒 旧 → 新 | 漏れの受け手（層/戦） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach ((string name, Formation f) in CompareBuilds().Concat(CrossBuilds().Select(c => (c.Name + "（交差帯）", c.F))))
        {
            if (!Has(f, "rau")) continue;
            double fires = 0, tg = 0, ly = 0, miss = 0, leak = 0, drawn = 0, fe = 0, fp = 0; int n = 0;
            var recv = new Dictionary<string, double>();
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    n++; fe += r.PoisonBiteEnemy; fp += r.PoisonBitePlayer;
                    if (r.TallyByUnit.TryGetValue("rau", out UnitTally? t))
                    { fires += t.TouchFires; tg += t.TouchTargets; ly += t.TouchLayers; miss += t.TouchMisses; leak += t.TouchLeakOut; }
                    if (r.TallyByUnit.TryGetValue("vio", out UnitTally? v)) drawn += v.TouchLeakDrawn;
                    foreach ((int _, UnitDef d) in f.Occupied())
                        if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? u) && u.TouchLeakIn > 0)
                        { recv.TryGetValue(d.Id, out double c); recv[d.Id] = c + u.TouchLeakIn; }
                }
            double oe = 0, op = 0;
            Formation of = Old(f);
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(of, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    oe += r.PoisonBiteEnemy; op += r.PoisonBitePlayer;
                }
            string who = string.Join(" ／ ", recv.OrderByDescending(kv => kv.Value).Select(kv => Short(UnitCatalog.ById(kv.Key)) + " " + (kv.Value / n).ToString("F2")));
            Console.WriteLine("| " + name + " | " + (fires / n).ToString("F2") + " | " + (tg / n).ToString("F2") + " | " + (ly / n).ToString("F1") + " | "
                              + (miss / n).ToString("F2") + " | " + (leak / n).ToString("F2") + " | " + (Has(f, "vio") ? (drawn / n).ToString("F2") : "—") + " | "
                              + (oe / n).ToString("F1") + " → " + (fe / n).ToString("F1") + " | " + (op / n).ToString("F1") + " → " + (fp / n).ToString("F1") + " | "
                              + (who.Length == 0 ? "—" : who) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**ベニ・ミオが漏れ・伝染を「読んだ回数」は数えていない**——ベニは「毒に侵された敵の数」を、ミオは「毒が積まれた敵」を"
                          + "ターン頭に一括で読むので、どの層が伝染由来かは区別できない。**効きは表B の版の差（ラウのみ − 旧）で読む。**");
        Console.WriteLine();
    }

    // =================================================================================
    // check
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第183期 `rebirth3 check` —— 自己検査");
        Console.WriteLine();

        // (a) 旧へ戻した 61 行が採用前の balance.md と一致する
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        if (File.Exists(path))
        {
            var want = ReadBalance(path);
            int cells = 0, miss = 0, rowsSeen = 0;
            foreach ((string name, Formation f) in CompareBuilds())
            {
                if (!want.TryGetValue(name, out double[]? w)) { miss++; continue; }
                rowsSeen++;
                double[] a = Rates(Old(f));
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - w[i]) > 0.001) cells++;
            }
            Console.WriteLine("- (a) 2枚を第182期の姿へ戻した " + rowsSeen + " 行 × 5波 と `" + path + "` の差: **" + cells + " セル**（0 が正）"
                              + (miss > 0 ? " ／ 行名が引けなかった行 " + miss : ""));
        }
        else Console.WriteLine("- (a) `" + path + "` が無いので飛ばした");

        // (b) 2枚を含まない行は旧と新で1セルも動かない（compare ＋ 交差帯）
        {
            int cells = 0, rows = 0;
            foreach ((string _, string _, Formation f) in Bands())
            {
                if (Mine(f)) continue;
                rows++;
                double[] a = Rates(Old(f)), b = Rates(f);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - b[i]) > 0.001) cells++;
            }
            Console.WriteLine("- (b) 2枚のどちらも含まない " + rows + " 行（compare ＋ 交差帯）で動いたセル: **" + cells + " / " + rows * 5 + "**（0 が正）");
        }

        // (c) 縫い合わせの収支
        {
            long fires = 0, recv = 0, scarOut = 0, scarIn = 0, stoic = 0;
            foreach ((string _, string _, Formation f) in Bands())
            {
                if (!Has(f, "vel")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        foreach (var kv in r.TallyByUnit)
                        {
                            fires += kv.Value.StitchFires; recv += kv.Value.StitchedTimes;
                            scarOut += kv.Value.StitchScarDealt; scarIn += kv.Value.StitchScarTaken;
                            if (kv.Key == "gald") stoic += kv.Value.StitchedTimes;
                        }
                    }
            }
            Console.WriteLine("- (c) 縫い: 縫った " + fires + " ／ 縫われた " + recv + "（**一致** " + (fires == recv ? "○" : "**×**") + "）"
                              + " ／ 縫い跡 出 " + scarOut + " ／ 入 " + scarIn + "（" + (scarOut == scarIn ? "○" : "**×**") + "）"
                              + " ／ 支援拒否（ガルド）が縫われた回数 **" + stoic + "**（0 が正）");
        }

        // (d) 触れてうつすは標的の毒を減らさない・漏れの収支
        {
            long outL = 0, inL = 0, fires = 0;
            foreach ((string _, string _, Formation f) in Bands())
            {
                if (!Has(f, "rau")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        foreach (var kv in r.TallyByUnit) { outL += kv.Value.TouchLeakOut; inL += kv.Value.TouchLeakIn; fires += kv.Value.TouchFires; }
                    }
            }
            Console.WriteLine("- (d) 漏れ: 出 " + outL + " ／ 入 " + inL + "（" + (outL == inL ? "○" : "**×**") + "）／ うつした回数 " + fires + "（> 0 が正）");
        }

        // (e) 漏れなし版は、漏れの計数が 0
        {
            long leak = 0, fires = 0;
            foreach ((string _, string _, Formation f) in Bands())
            {
                if (!Has(f, "rau")) continue;
                Formation g = Swap(f, "rau", RauNoLeak);
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = BattleEngine.Run(g, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        foreach (var kv in r.TallyByUnit) { leak += kv.Value.TouchLeakOut; fires += kv.Value.TouchFires; }
                    }
            }
            Console.WriteLine("- (e) 漏れなし版（`yP`）: うつした " + fires + " ／ 漏れ **" + leak + "**（0 が正）");
        }

        // (f) PickOne を新しく使っていない
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string pick = "ctx.Pick" + "One(";
            int total = (traits.Length - traits.Replace(pick, "").Length) / pick.Length;
            int mine = 0;
            foreach (string cls in new[] { "Stitch" + "Trait", "Touch" + "Trait", "TouchLeak" + "Trait" })
            {
                int i = traits.IndexOf("class " + cls + " ");
                if (i < 0) continue;
                int j = traits.IndexOf("\npublic ", i + 10);
                string body = traits.Substring(i, (j < 0 ? traits.Length : j) - i);
                mine += (body.Length - body.Replace(pick, "").Length) / pick.Length;
            }
            Console.WriteLine("- (f) 第183期の3枚が `" + pick + "` を呼ぶ回数: **" + mine + "**（0 が正）／ `Traits.cs` 全体は " + total + " 件");
        }

        // (g) 経路とキー
        Console.WriteLine("- (g) `PoisonRoute` の本数: **" + Enum.GetValues<PoisonRoute>().Length + "**（6 → 8）／ `SoakRouteCount` **"
                          + BattleContext.SoakRouteCount + "** ／ 燃焼の添字 **" + BattleContext.SoakBurnRouteIx + "**（毒の経路と重ならないこと: "
                          + (BattleContext.SoakBurnRouteIx >= Enum.GetValues<PoisonRoute>().Length ? "○" : "**×**") + "）"
                          + " ／ `StatusKeys.All` の本数 **" + StatusKeys.All.Length + "**（第182期から動かない）");
        Console.WriteLine();
    }

    static Dictionary<string, double[]> ReadBalance(string path)
    {
        var d = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            string[] c = line.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            if (c.Length < 6) continue;
            var w = new double[5];
            bool ok = true;
            for (int i = 0; i < 5; i++)
                ok &= double.TryParse(c[1 + i].TrimEnd('%'), System.Globalization.NumberStyles.Float,
                                      System.Globalization.CultureInfo.InvariantCulture, out w[i]);
            if (ok) d[c[0]] = w;
        }
        return d;
    }

    // =================================================================================
    // ヘルパ
    // =================================================================================

    static double[] Rates(Formation f, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static double Avg25(Formation f) => Mean25(Rates(f));
    static string Cells(double[] w) => string.Join(" | ", Enumerable.Range(1, 4).Select(i => w[i].ToString("F1")));
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");
}
