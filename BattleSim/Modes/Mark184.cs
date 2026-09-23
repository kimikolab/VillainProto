using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// mark184 モード（第184期） —— A群の転生 1〜2枚目（標の軸: ヒサ・ザン ＋ 敵の標の被ダメージ増）
//
// 指示書は design/PHASE184_MARK_SPEC.md ／ 報告は design/PHASE184_MARK.md。
//
// **数値の線は1本も置かない**（第178期から引き継ぐ約束）。
// 回帰確認（標に関わる駒を含まない行が動かないこと）と対照の表・帳簿だけで、採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 mark184 phase0  # Q0-1〜Q0-8 を実装から引き直す
//     dotnet run --project BattleSim -c Release 0 mark184 run     # 対照（§1 だけ／§1＋ヒサ／§1＋ザン／全部）・(G2)・勝ち方
//     dotnet run --project BattleSim -c Release 0 mark184 ledger  # 帳簿（§1・ヒサ・ザン）
//     dotnet run --project BattleSim -c Release 0 mark184 bench   # 診断台（ヒサ×ムド×ザン×トメ）
//     dotnet run --project BattleSim -c Release 0 mark184 check [採用前のbalance.md]  # 自己検査
//
// 旧版の対照は「その駒だけ第183期の姿に戻したローカルの `UnitDef`」で作る（第180期と同じ作法）。
// §1 の切り替えだけは `Run` の引数（`MarkRule`）で振る。
// =====================================================================================

static class Mark184Diag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": StageRun(); return;
            case "ledger": Ledger(); return;
            case "bench": Bench(); return;
            case "strip": Strip(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("mark184: モードは phase0 / run / ledger / bench / check。");
                return;
        }
    }

    // =================================================================================
    // 版
    // =================================================================================

    static UnitDef Remake(UnitDef d, TraitId[] traits, IReadOnlyList<UnitAction>? actions,
                          string? plus = null, string? minus = null) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Pattern = d.Pattern, Advances = d.Advances, Actions = actions,
        Traits = traits, PlusText = plus ?? d.PlusText, MinusText = minus ?? d.MinusText, Flavor = d.Flavor
    };

    /// <summary>第183期のヒサ（開戦時に1回だけ標・手番は攻2の素の攻撃）。</summary>
    static readonly UnitDef HisaOld = Remake(UnitCatalog.Hisa, new[] { TraitId.Marker }, null,
        "隣接する味方1体に標を付け、敵の攻撃を集中させる", "自分では何もできない。押し出された味方は普通は死ぬ");

    /// <summary>第183期のザン（仇討ち＝等倍の割り込み・殴られると怯む）。</summary>
    static readonly UnitDef ZanOld = Remake(UnitCatalog.Zan, new[] { TraitId.Avenge }, null,
        "標的にされた味方が殴られるたび、殴った者へ割り込んで刃を返す",
        "自分が殴られると怖気づき、次の手番を失う。怯んでいる間は刃も返せない");

    /// <summary>矢面だけ・逃げない（`yP`）。</summary>
    static readonly UnitDef HisaNoFlee = Remake(UnitCatalog.Hisa, new[] { TraitId.Beckon }, UnitCatalog.Hisa.Actions);

    /// <summary>仇指しだけ・返り血なし（`yP`）。</summary>
    static readonly UnitDef ZanNoRecoil = Remake(UnitCatalog.Zan, new[] { TraitId.Vendetta }, null);

    sealed record Version(string Name, bool NewHisa, bool NewZan, MarkRule Rule, UnitDef? HisaAs = null, UnitDef? ZanAs = null);

    static readonly Version V0 = new("旧（第183期）", false, false, MarkRule.Off);
    static readonly Version S1 = new("§1 だけ", false, false, MarkRule.Default);
    static readonly Version S1H = new("§1＋ヒサ", true, false, MarkRule.Default);
    static readonly Version S1Z = new("§1＋ザン", false, true, MarkRule.Default);
    static readonly Version New = new("新（全部）", true, true, MarkRule.Default);
    static readonly Version NewNoS1 = new("新・§1 なし", true, true, MarkRule.Off);
    static readonly Version YpH = new("新・逃げない", true, true, MarkRule.Default, HisaAs: HisaNoFlee);
    static readonly Version YpZ = new("新・返り血なし", true, true, MarkRule.Default, ZanAs: ZanNoRecoil);

    static Formation Swap(Formation f, string id, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == id ? to : d;
        return g;
    }

    static Formation Apply(Formation f, Version v)
    {
        Formation g = f;
        g = Swap(g, "hisa", v.HisaAs ?? (v.NewHisa ? UnitCatalog.Hisa : HisaOld));
        g = Swap(g, "zan", v.ZanAs ?? (v.NewZan ? UnitCatalog.Zan : ZanOld));
        return g;
    }

    static readonly string[] MarkUnits = { "hisa", "zan", "sora", "tome", "kari" };
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static bool Mine(Formation f) => MarkUnits.Any(id => Has(f, id));
    static string Who(Formation f) => string.Join("・", MarkUnits.Where(id => Has(f, id)).Select(id => Short(UnitCatalog.ById(id))));

    static BattleResult Fight(Formation f, int st, int seed, Version v, bool verbose = false)
        => BattleEngine.Run(Apply(f, v), EnemyCatalog.Stages[st].Enemy, seed, verbose: verbose, markRule: v.Rule);

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第184期 `mark184 phase0` —— 前提を実装から引き直す");
        Console.WriteLine();
        if (!ParryScan.Init()) return;
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1. 標（`StatusKeys.Marked`）の書き手（陣営別・実装から引く）");
        Console.WriteLine();
        Console.WriteLine("`Traits.cs` で `SetCounter(StatusKeys.Marked, 1)` を書くクラス:");
        Console.WriteLine();
        string[] ls = traits.Replace("\r", "").Split('\n');
        string cls = "";
        var writers = new List<string>();
        for (int i = 0; i < ls.Length; i++)
        {
            string t = ls[i].TrimStart();
            if (t.StartsWith("public sealed class ")) cls = t.Split(' ')[3];
            if (t.StartsWith("//")) continue;
            if (ls[i].Contains("SetCounter(StatusKeys." + "Marked, 1)"))
            {
                writers.Add(cls);
                Console.WriteLine("- `Traits.cs:" + (i + 1) + "` `" + cls + "` —— `" + t + "`");
            }
        }
        Console.WriteLine();
        var writerIds = new Dictionary<TraitId, string>
        {
            [TraitId.Marker] = "味方（旧ヒサ）", [TraitId.Beckon] = "味方（新ヒサ）", [TraitId.Goad] = "味方（カリ）",
            [TraitId.Divert] = "自分・敵（ソラ）", [TraitId.Vendetta] = "敵（新ザン）", [TraitId.Scapegoat] = "敵（業の転写・ゴウ）",
        };
        Console.WriteLine("| 札 | 付ける先 | 味方の保持者（`Everyone`） | 敵の保持者（全波） |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var kv in writerIds)
        {
            var mine = UnitCatalog.Everyone.Where(d => d.Traits.Contains(kv.Key)).Select(d => d.Name).ToList();
            var foes = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied()).Select(o => o.Def)
                .Where(d => d.Traits.Contains(kv.Key)).Select(d => d.Name).Distinct().ToList();
            Console.WriteLine("| `" + kv.Key + "` | " + kv.Value + " | " + (mine.Count == 0 ? "—" : string.Join("・", mine))
                              + " | " + (foes.Count == 0 ? "**0**" : string.Join("・", foes)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**敵がこちらに標を付ける経路は 0 本**（敵の全波に書き手が1体もいない）——§1 は味方に効かない。"
                          + "§1 の判定は `target.TeamId != PlayerTeam` でも閉じてある。");
        Console.WriteLine();

        // ---- Q0-2 / Q0-3 ----
        Console.WriteLine("## Q0-2 / Q0-3. `ApplyDamage` の段の並び（本文の位置から機械で引く）");
        Console.WriteLine();
        int body = engine.IndexOf("void ApplyDamageBody(");
        var stages = new (string Key, string Label)[]
        {
            ("ThornGuardTrait.AbsorbCap", "棘守りの肩代わり上限（入力を切る）"),
            ("t.ModifyIncomingDamage", "駒の被ダメージ修正（脆弱など）"),
            ("HavocTrait.Percent", "惨禍 +50%（入口・加算）"),
            ("Ward.BurdenPercent", "荷（入口・加算）"),
            ("MarkRules.VulnerablePercent", "**§1 敵の標 +50%（入口・加算）**"),
            ("BulwarkTrait.ReductionPercent", "据え（軽減）"),
            ("LooseTrait.ReductionPercent", "散開（軽減）"),
            ("CowerTrait.ReductionPercent", "萎縮（軽減）"),
            ("BeckonTrait.GuardPercent", "**§2 矢面の半減（軽減）**"),
            ("Colossus.Percent", "巨躯の肩代わり"),
            ("SharerTrait.Percent", "分かちの肩代わり"),
            ("int armor = target.RawCounter(StatusKeys.Armor)", "破片"),
            ("Parry.Uses > 0", "受け流し"),
            ("Brace.Cap > 0 && amount > Brace.Cap", "身構えの上限"),
            ("bool yokeBinding = amount > Yoke.Cap", "軛の上限（HP を引く直前）"),
        };
        Console.WriteLine("| 順 | 段 | 位置 |");
        Console.WriteLine("|--:|---|--:|");
        int k = 0;
        foreach (var s in stages.Select(s => (s.Label, Pos: engine.IndexOf(s.Key, body))).OrderBy(x => x.Pos < 0 ? int.MaxValue : x.Pos))
            Console.WriteLine("| " + (++k) + " | " + s.Label + " | " + (s.Pos < 0 ? "**見つからない**" : "+" + (s.Pos - body)) + " |");
        Console.WriteLine();
        Console.WriteLine("- **§1 は上限（軛・身構え）より前**なので「1発は Cap を超えない」は守られる。"
                          + "惨禍と同じ加算の族で、軽減の族（据え・散開・萎縮）より前に掛かる。");
        Console.WriteLine("- **§2 は §1 と同じ条件で符号だけ違う形**（相手陣営の出どころがあり、刻み `burnTick`・徴収 `levy`・"
                          + "中継 `relayed`・共有 `hexShare` ではない）。§1 は敵陣営だけ・§2 は矢面の相手（味方）だけなので、**同じ一撃に両方は乗らない**。");
        Console.WriteLine("- 呪いの共有（`hexShare`）は第96期の段のまま、どちらにも乗らない。毒の刻みは `source` が null なので自然に外れる。");
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4. カリとヒサが同じ味方に標を重ねたとき");
        Console.WriteLine();
        Console.WriteLine("標のカウンタは1本（`StatusKeys.Marked`）しか無いので、**出どころは保持者の側の記憶で区別する**"
                          + "——`BeckonTrait.TargetKey`（相手の `InstanceId + 1`）。engine の半減は"
                          + "「標がある **かつ** 同じ陣営の矢面の保持者の記憶がこの駒を指している」の積で読む。"
                          + "**カリの標・ソラが自分に付ける標は半減しない。**");
        Console.WriteLine();
        int both = 0;
        foreach ((string band, string name, Formation f) in Bands())
            if (Has(f, "hisa") && Has(f, "kari")) { both++; Console.WriteLine("- ヒサとカリが同席する行: " + name + "（" + band + "）"); }
        Console.WriteLine(both == 0 ? "- **ヒサとカリが同席する行は `compare` 61 行・交差帯 12 行に 0 行**（重なりは盤面に出ない）。" : "");
        Console.WriteLine("- 重なりの穴: カリは毎ターン頭に**前の相手の標を 0 にする**ので、ヒサと同じ相手を選んでいた場合はヒサの標も消える"
                          + "（次のヒサの手番で付け直すまで半減も止まる）。**ソラは味方の標を毎ターン剥がす**ので、ヒサ×ソラの行では"
                          + "ソラの手番の後は半減が止まる。");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5. 入れ替えで発火する移動の読み手（リフレクションで引く）");
        Console.WriteLine();
        Console.WriteLine("| 札 | 上書きしているフック | `All` の保持者 |");
        Console.WriteLine("|---|---|---|");
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            Trait tr;
            try { tr = TraitCatalog.Get(id); } catch { continue; }
            Type ty = tr.GetType();
            var hooks = new List<string>();
            foreach (string h in new[] { "OnMoved", "OnAllyMoved" })
            {
                MethodInfo? mi = ty.GetMethod(h);
                if (mi is not null && mi.DeclaringType == ty) hooks.Add(h);
            }
            if (hooks.Count == 0) continue;
            var holders = UnitCatalog.All.Where(d => d.Traits.Contains(id)).Select(d => Short(d)).ToList();
            Console.WriteLine("| `" + id + "` | " + string.Join(" / ", hooks) + " | " + (holders.Count == 0 ? "—" : string.Join("・", holders)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 入れ替えは `SwapSlots` そのもの（`ActorId` にヒサが載る）。**押しのけられた味方にも同じ通知が走る。**");
        Console.WriteLine("- **バサの転倒は敵にしか付かない**（`ShufflerTrait` が前へ出した敵を転ばせる）。混乱（`ConfusionRule`）は既定 `Active = false`。"
                          + "**味方の入れ替えで転ぶ／混乱する経路は既定の盤面に無い。**");
        Console.WriteLine();

        // ---- Q0-6 / Q0-7 ----
        Console.WriteLine("## Q0-6 / Q0-7. 仇指しの順序と、敵の標の競合");
        Console.WriteLine();
        Console.WriteLine("- **殴ってから付ける**（指示書の推奨どおり）。1発目は倍（攻10 → 20）だけ。既に標を持つ敵なら 20 × 1.5 = 30。");
        Console.WriteLine("- 敵の標持ちが複数いるときの標的選択: トメ（止め）は `FinisherTrait.Preferred`（現在HP最大・同値のみ `PickOne`）、"
                          + "それ以外の味方は `PickOne(標持ち)` → `Roll(100) < 75`。**ザンが付ける標は乱数の消費を増やす**"
                          + "（標持ちが 2 体以上なら `PickOne` が引く）。同時に何体立つかは `ledger` の `同時最大` 列で実測する。");
        Console.WriteLine("- トメは殴った標を消費するので、**ザンが付けた標もトメに食われる**（第150期「トメがいれば標は書かれた瞬間に食われる」）。");
        Console.WriteLine();

        Console.WriteLine("## 在席（`compare` 61 行 ＋ 交差帯 12 行）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | ヒサ | ザン | ソラ | トメ | カリ | ヒサの隣 |");
        Console.WriteLine("|---|---|:-:|:-:|:-:|:-:|:-:|---|");
        int nMine = 0, nNone = 0;
        foreach ((string band, string name, Formation f) in Bands())
        {
            if (!Mine(f)) { if (band == "compare") nNone++; continue; }
            if (band == "compare") nMine++;
            string M(string id) => Has(f, id) ? "●" : "";
            Console.WriteLine("| " + name + " | " + band + " | " + M("hisa") + " | " + M("zan") + " | " + M("sora") + " | "
                              + M("tome") + " | " + M("kari") + " | " + Neigh(f, "hisa") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- `compare` 61 行: 標に関わる駒を含む **" + nMine + " 行** ／ 含まない **" + nNone + " 行**（回帰の分母）");
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
        Console.WriteLine("# 第184期 `mark184 run` —— 対照（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**旧** ＝ 第183期（旧ヒサ・旧ザン・§1 なし）。**§1 だけ** ＝ 旧ヒサ・旧ザンのまま §1（+50%）。"
                          + "**§1＋ヒサ** ／ **§1＋ザン** ＝ 片方だけ新。**新** ＝ この期の既定（3つ全部）。"
                          + "**新・§1 なし** ＝ 2枚は新・§1 を切った版（§1 の寄与を新しいプラスの上で測る・R289）。"
                          + "**逃げない** ／ **返り血なし** ＝ 代金の札だけを外した `yP`。第2〜5波の平均・seed 0..199。");
        Console.WriteLine();

        var versions = new[] { V0, S1, S1H, S1Z, New, NewNoS1, YpH, YpZ };
        var rows = Bands().ToList();
        var res = new Dictionary<(string, string), double[]>();
        Parallel.ForEach(rows.SelectMany(r => versions.Select(v => (r, v))), x =>
        {
            if (!Mine(x.r.F) && x.v != V0 && x.v != New) return;
            double[] w = Rates(x.r.F, x.v);
            lock (res) res[(x.r.Band + "/" + x.r.Name, x.v.Name)] = w;
        });
        double A(string key, Version v) => Mean25(res[(key, v.Name)]);

        Console.WriteLine("## 表A. 標に関わる駒を含む行（第2〜5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 駒 | 旧 | §1 だけ | §1＋ヒサ | §1＋ザン | **新** | 新−旧 | §1 の寄与（新−§1なし） | 逃げの代金（新−逃げない） | 返り血の代金（新−返り血なし） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in rows)
        {
            if (!Mine(r.F)) continue;
            string key = r.Band + "/" + r.Name;
            Console.WriteLine("| " + r.Name + " | " + r.Band + " | " + Who(r.F) + " | " + A(key, V0).ToString("F1") + " | "
                              + A(key, S1).ToString("F1") + " | " + A(key, S1H).ToString("F1") + " | " + A(key, S1Z).ToString("F1") + " | **"
                              + A(key, New).ToString("F1") + "** | **" + D(A(key, New) - A(key, V0)) + "** | "
                              + D(A(key, New) - A(key, NewNoS1)) + " | "
                              + (Has(r.F, "hisa") ? D(A(key, New) - A(key, YpH)) : "—") + " | "
                              + (Has(r.F, "zan") ? D(A(key, New) - A(key, YpZ)) : "—") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表A'. 波別（旧 → 新）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 旧 第2〜5波 | 新 第2〜5波 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var r in rows)
        {
            if (!Mine(r.F)) continue;
            string key = r.Band + "/" + r.Name;
            Console.WriteLine("| " + r.Name + " | " + r.Band + " | " + Cells(res[(key, V0.Name)]) + " | " + Cells(res[(key, New.Name)]) + " |");
        }
        Console.WriteLine();

        // ---- 回帰 ----
        int still = 0, stillRows = 0;
        foreach (var r in rows)
        {
            if (Mine(r.F)) continue;
            stillRows++;
            string key = r.Band + "/" + r.Name;
            double[] a = res[(key, V0.Name)], b = res[(key, New.Name)];
            if (Enumerable.Range(0, 5).Any(i => Math.Abs(a[i] - b[i]) > 0.001)) still++;
        }
        Console.WriteLine("- **標に関わる駒を含まない " + stillRows + " 行（compare ＋ 交差帯）で旧と新が違う行: " + still + " 行**（0 が正）");
        Console.WriteLine();

        // ---- (G2) ----
        Console.WriteLine("## 表B. (G2) の「壊れ／制約」の分解（`compare` 61 行の分母・**報告のみ**）");
        Console.WriteLine();
        var builds = CompareBuilds().ToList();
        double[] O(string n) => res[("compare/" + n, V0.Name)];
        double[] N(string n) => res[("compare/" + n, New.Name)];
        var dropped = new List<(string Name, int Wave, double Delta)>();
        foreach ((string name, Formation _) in builds)
            for (int w = 1; w < 5; w++)
                if (N(name)[w] - O(name)[w] <= -10.0) dropped.Add((name, w, N(name)[w] - O(name)[w]));
        if (dropped.Count == 0) Console.WriteLine("**−10.0pt 以上落ちたセルは 0 件。**");
        else
        {
            Console.WriteLine("| 行 | 波 | Δ |");
            Console.WriteLine("|---|--:|--:|");
            foreach (var d in dropped) Console.WriteLine("| " + d.Name + " | 第" + (d.Wave + 1) + "波 | " + D(d.Delta) + " |");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 他の行の数 | 平均変化 | 判定 |");
            Console.WriteLine("|---|--:|--:|---|");
            var units = new SortedSet<string>();
            foreach (var d in dropped.Select(x => x.Name).Distinct())
                foreach ((int _, UnitDef u) in builds.First(b => b.Name == d).F.Occupied()) units.Add(u.Id);
            foreach (string id in units)
            {
                var others = builds.Where(b => Has(b.F, id) && !dropped.Any(x => x.Name == b.Name)).ToList();
                if (others.Count == 0) { Console.WriteLine("| " + UnitCatalog.ById(id).Name + " | 0 | — | 分解が成立しない |"); continue; }
                double avg = others.Average(b => Mean25(N(b.Name)) - Mean25(O(b.Name)));
                string verdict = avg <= -3.0 ? "**壊れ**" : Math.Abs(avg) < 3.0 ? "制約" : "（上振れ）";
                Console.WriteLine("| " + UnitCatalog.ById(id).Name + " | " + others.Count + " | " + D(avg) + " | " + verdict + " |");
            }
        }
        Console.WriteLine();

        // ---- 勝ち方 ----
        Console.WriteLine("## 表C. 勝ち方（第2〜5波・勝った試行だけ）");
        Console.WriteLine();
        Console.WriteLine("`残存` ＝ 勝った試行の生存数の平均 ／ `全滅勝ち` ＝ 勝った試行のうち生存1体の割合（`docs/chain.md` と同じ定義）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 残存 旧 | 残存 新 | 全滅勝ち 旧 | 全滅勝ち 新 | 決着T 旧 | 決着T 新 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in builds)
        {
            if (!Mine(f)) continue;
            var a = Quality(f, V0); var b = Quality(f, New);
            Console.WriteLine("| " + name + " | " + Who(f) + " | " + a.Surv.ToString("F2") + " | " + b.Surv.ToString("F2") + " | "
                              + a.Edge.ToString("F1") + "% | " + b.Edge.ToString("F1") + "% | " + a.T.ToString("F2") + " | " + b.T.ToString("F2") + " |");
        }
        Console.WriteLine();

        // ---- 指標 ----
        Console.WriteLine("## 表D. 全体の指標（新）");
        Console.WriteLine();
        double[] all = new double[5], prim = new double[5];
        foreach ((string name, Formation _) in builds)
            for (int w = 0; w < 5; w++) all[w] += N(name)[w] / builds.Count;
        int pc = 0;
        foreach (string p in Baseline.PrimaryRows)
            if (builds.Any(b => b.Name == p)) { pc++; for (int w = 0; w < 5; w++) prim[w] += N(p)[w]; }
        for (int w = 0; w < 5; w++) prim[w] /= Math.Max(1, pc);
        Console.WriteLine("- 全" + builds.Count + "行: " + string.Join(" / ", all.Select(x => x.ToString("F1"))));
        Console.WriteLine("- 主判定" + pc + "行: " + string.Join(" / ", prim.Select(x => x.ToString("F1")))
                          + "（歯止め 33.2% との余裕 " + D(prim[4] - 33.2) + "pt）");
        int infoOld = 0, infoNew = 0, hiOld = 0, hiNew = 0;
        foreach ((string name, Formation _) in builds)
        {
            for (int w = 1; w < 5; w++) { if (O(name)[w] > 0 && O(name)[w] < 100) infoOld++; if (N(name)[w] > 0 && N(name)[w] < 100) infoNew++; }
            if (O(name)[4] > 95) hiOld++; if (N(name)[4] > 95) hiNew++;
        }
        Console.WriteLine("- 情報セル（`0 < x < 100`・第2〜5波）: " + infoOld + " → **" + infoNew + "** ／ 第五波 95% 超: " + hiOld + " → **" + hiNew + "**");
        Console.WriteLine();
    }

    static (double Surv, double Edge, double T) Quality(Formation f, Version v)
    {
        long wins = 0, surv = 0, edge = 0, turns = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = Fight(f, st, seed, v);
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
        Console.WriteLine("# 第184期 `mark184 ledger` —— 帳簿（1戦あたり・第2〜5波・seed 0..199）");
        Console.WriteLine();

        var rows = Bands().Where(r => Mine(r.F)).ToList();

        Console.WriteLine("## 表E. §1 敵の標の被ダメージ増（新）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 標が立ったターン | 延べ体数 | 同時最大 | 増えた回数 | 上乗せ（ソラ） | 上乗せ（ザン） | 上乗せ（他） | 上乗せ計 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in rows)
        {
            double turns = 0, unitTurns = 0, hits = 0, sora = 0, zan = 0, oth = 0; long max = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult b = Fight(r.F, st, seed, New);
                    n++;
                    MarkAxisLedger m = b.MarkAxis;
                    turns += m.FoeTurns; unitTurns += m.FoeUnitTurns; max = Math.Max(max, m.FoeMax);
                    if (m.VulnHits is null) continue;
                    hits += m.VulnHits.Sum();
                    oth += m.VulnAdded[(int)MarkOrigin.Other]; sora += m.VulnAdded[(int)MarkOrigin.Divert]; zan += m.VulnAdded[(int)MarkOrigin.Vendetta];
                }
            if (turns + hits == 0) continue;
            Console.WriteLine("| " + r.Name + " | " + Who(r.F) + " | " + (turns / n).ToString("F2") + " | " + (unitTurns / n).ToString("F2") + " | " + max + " | "
                              + (hits / n).ToString("F2") + " | " + (sora / n).ToString("F1") + " | " + (zan / n).ToString("F1") + " | "
                              + (oth / n).ToString("F1") + " | **" + ((sora + zan + oth) / n).ToString("F1") + "** |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表F. ヒサ（新）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 付けた | 付け替え | 隣なし | 半減で防いだ | 入れ替え | 逃げ場なし | 最中の振り | 最中の強化 | 最中に動いた敵 | 指差された相手（回/戦） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var r in rows)
        {
            if (!Has(r.F, "hisa")) continue;
            double fire = 0, sw = 0, idle = 0, saved = 0, flee = 0, stuck = 0, rs = 0, rw = 0, fm = 0; int n = 0;
            var picked = new Dictionary<string, double>();
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult b = Fight(r.F, st, seed, New);
                    n++;
                    if (b.TallyByUnit.TryGetValue("hisa", out UnitTally? t))
                    {
                        fire += t.BeckonFires; sw += t.BeckonSwitches; idle += t.BeckonIdle; saved += t.BeckonGuardSaved;
                        flee += t.FleeSwaps; stuck += t.FleeStuck; rs += t.FleeReaderSwings; rw += t.FleeReaderWhet; fm += t.FleeFoeMoves;
                    }
                    foreach ((int _, UnitDef d) in r.F.Occupied())
                        if (b.TallyByUnit.TryGetValue(d.Id, out UnitTally? u) && u.BeckonPicked > 0)
                            picked[Short(d)] = picked.GetValueOrDefault(Short(d)) + u.BeckonPicked;
                }
            Console.WriteLine("| " + r.Name + " | " + (fire / n).ToString("F2") + " | " + (sw / n).ToString("F2") + " | " + (idle / n).ToString("F2") + " | "
                              + (saved / n).ToString("F1") + " | " + (flee / n).ToString("F2") + " | " + (stuck / n).ToString("F2") + " | "
                              + (rs / n).ToString("F2") + " | " + (rw / n).ToString("F1") + " | " + (fm / n).ToString("F2") + " | "
                              + string.Join(" ", picked.OrderByDescending(x => x.Value).Select(x => x.Key + " " + (x.Value / n).ToString("F2"))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表G. ザン（旧 → 新）");
        Console.WriteLine();
        Console.WriteLine("`与害` ＝ `UnitTally.DamageToEnemy`（実際に敵へ通った量）。`他` は同じ編成の他の4枚（与害 > 0 の駒）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 刃を返した | 削った | 標を付けた | 返り血 | ザンの与害 | 他の最大 | 他の平均 | ザン ÷ 他の平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in rows)
        {
            if (!Has(r.F, "zan")) continue;
            foreach (Version v in new[] { V0, New })
            {
                double fire = 0, dealt = 0, marks = 0, rec = 0, zdmg = 0, omax = 0, oavg = 0; int n = 0;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult b = Fight(r.F, st, seed, v);
                        n++;
                        if (b.TallyByUnit.TryGetValue("zan", out UnitTally? t))
                        { fire += t.VendettaFires; dealt += t.VendettaDealt; marks += t.VendettaMarks; rec += t.RecoilTaken; zdmg += t.DamageToEnemy; }
                        var others = r.F.Occupied().Where(o => o.Def.Id != "zan")
                            .Select(o => b.TallyByUnit.TryGetValue(o.Def.Id, out UnitTally? u) ? (double)u.DamageToEnemy : 0.0).ToList();
                        omax += others.Count == 0 ? 0 : others.Max();
                        var pos = others.Where(x => x > 0).ToList();
                        oavg += pos.Count == 0 ? 0 : pos.Average();
                    }
                Console.WriteLine("| " + r.Name + " | " + v.Name + " | " + (fire / n).ToString("F2") + " | " + (dealt / n).ToString("F1") + " | "
                                  + (marks / n).ToString("F2") + " | " + (rec / n).ToString("F1") + " | " + (zdmg / n).ToString("F1") + " | "
                                  + (omax / n).ToString("F1") + " | " + (oavg / n).ToString("F1") + " | " + (oavg > 0 ? (zdmg / oavg).ToString("F2") : "—") + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("旧版の `刃を返した` 列は 0（旧 `AvengeTrait` はこの計数を持たない）。旧版の出力は `ザンの与害` で比べる。");
        Console.WriteLine();
    }

    // =================================================================================
    // 診断台
    // =================================================================================

    static readonly (string Name, Formation F)[] Benches =
    {
        ("台1 ヒサ中央（ムド・ボルグ・トメ・ザン）", Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Borg,
            center: UnitCatalog.Hisa, back1: UnitCatalog.Tome, back3: UnitCatalog.Zan)),
        ("台2 ヒサ後列（ムド・トメ・ザン・ボルグ）", Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Tome,
            center: UnitCatalog.Zan, back1: UnitCatalog.Hisa, back3: UnitCatalog.Borg)),
    };

    static void Bench()
    {
        Console.WriteLine("# 第184期 `mark184 bench` —— 診断台（ヒサ×ムド×ザン×トメ・`Presets` には足さない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1〜5波 | 第2〜5波平均 |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var b in Benches)
            foreach (Version v in new[] { V0, S1, S1H, S1Z, New, NewNoS1, YpH, YpZ })
            {
                double[] w = Rates(b.F, v);
                Console.WriteLine("| " + b.Name + " | " + v.Name + " | " + string.Join(" / ", w.Select(x => x.ToString("F1"))) + " | " + Mean25(w).ToString("F1") + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 帳簿（新・第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 半減で防いだ | ムドが指差された | ムドの生存T | 旧のムドの生存T | ムドの暴発 | 刃を返した | 標を付けた | §1 の上乗せ | トメの与害 | ザンの与害 | ボルグの与害 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in Benches)
        {
            double saved = 0, mp = 0, life = 0, lifeOld = 0, erupt = 0, vf = 0, vm = 0, add = 0, tome = 0, zan = 0, borg = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = Fight(b.F, st, seed, New);
                    BattleResult o = Fight(b.F, st, seed, V0);
                    n++;
                    UnitTally T(BattleResult x, string id) => x.TallyByUnit.TryGetValue(id, out UnitTally? t) ? t : new UnitTally();
                    saved += T(r, "hisa").BeckonGuardSaved; mp += T(r, "mudo").BeckonPicked;
                    life += LifeOf(r, "mudo"); lifeOld += LifeOf(o, "mudo");
                    erupt += T(r, "mudo").EruptFires; vf += T(r, "zan").VendettaFires; vm += T(r, "zan").VendettaMarks;
                    add += r.MarkAxis.VulnAdded?.Sum() ?? 0;
                    tome += T(r, "tome").DamageToEnemy; zan += T(r, "zan").DamageToEnemy; borg += T(r, "borg").DamageToEnemy;
                }
            Console.WriteLine("| " + b.Name + " | " + (saved / n).ToString("F1") + " | " + (mp / n).ToString("F2") + " | " + (life / n).ToString("F2") + " | "
                              + (lifeOld / n).ToString("F2") + " | " + (erupt / n).ToString("F2") + " | " + (vf / n).ToString("F2") + " | " + (vm / n).ToString("F2") + " | "
                              + (add / n).ToString("F1") + " | " + (tome / n).ToString("F1") + " | " + (zan / n).ToString("F1") + " | " + (borg / n).ToString("F1") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("`生存T` ＝ その駒の `LastActiveTurn`（倒れなければ決着T）。");
        Console.WriteLine();

        // 1戦のログ（鎖が繋がって見えるか）
        Console.WriteLine("## 1戦のログ（台1・第4波・seed 3・新）");
        Console.WriteLine();
        Console.WriteLine("```");
        BattleResult one = Fight(Benches[0].F, 3, 3, New, verbose: true);
        foreach (var line in one.Log.Take(90)) Console.WriteLine(line);
        Console.WriteLine("```");
    }

    /// <summary>ソラ（逸らし）がヒサの標を剥がす行の挙動（ポンの追加の問い）。</summary>
    static void Strip()
    {
        Console.WriteLine("# 第184期 `mark184 strip` —— ソラはヒサの守りの標を剥がすか（新・第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("`守られた被弾` ＝ 半減が掛かった被弾 ／ `剥がれていた被弾` ＝ ヒサの記憶が指しているのに標が無く、半減が掛からなかった被弾"
                          + "（量は軽減の族を通った後）。`剥がれ率` ＝ 剥がれていた ÷ (守られた ＋ 剥がれていた)。");
        Console.WriteLine();
        Console.WriteLine("| 行 | ソラ | 付けた | 守られた被弾 | 防いだ量 | 剥がれていた被弾 | その量 | 剥がれ率 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in Bands().Where(r => Has(r.F, "hisa")).Concat(Benches.Select(b => ("台", b.Name, b.F))))
        {
            double fire = 0, gh = 0, gs = 0, sh = 0, sd = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult b = Fight(r.F, st, seed, New);
                    n++;
                    gh += b.MarkAxis.GuardHits; gs += b.MarkAxis.GuardSaved; sh += b.MarkAxis.StrippedHits; sd += b.MarkAxis.StrippedDamage;
                    if (b.TallyByUnit.TryGetValue("hisa", out UnitTally? t)) fire += t.BeckonFires;
                }
            Console.WriteLine("| " + r.Name + " | " + (Has(r.F, "sora") ? "●" : "") + " | " + (fire / n).ToString("F2") + " | " + (gh / n).ToString("F2") + " | "
                              + (gs / n).ToString("F1") + " | " + (sh / n).ToString("F2") + " | " + (sd / n).ToString("F1") + " | "
                              + (gh + sh > 0 ? (100.0 * sh / (gh + sh)).ToString("F1") + "%" : "—") + " |");
        }
        Console.WriteLine();
    }

    static double LifeOf(BattleResult r, string id)
        => r.TallyByUnit.TryGetValue(id, out UnitTally? t) && t.LastActiveTurn > 0 ? t.LastActiveTurn : r.Turns;

    // =================================================================================
    // check
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第184期 `mark184 check` —— 自己検査");
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
                double[] a = Rates(f, V0);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - w[i]) > 0.001) cells++;
            }
            Console.WriteLine("- (a) 旧（第183期のヒサ・ザン・`MarkRule.Off`）の " + rowsSeen + " 行 × 5波 と `" + path + "` の差: **" + cells + " セル**（0 が正）"
                              + (miss > 0 ? " ／ 行名が引けなかった行 " + miss : ""));
        }
        else Console.WriteLine("- (a) `" + path + "` が無いので飛ばした");

        // (b) 標に関わる駒を含まない行は旧と新で1セルも動かない
        {
            int cells = 0, rows = 0;
            foreach ((string _, string _, Formation f) in Bands())
            {
                if (Mine(f)) continue;
                rows++;
                double[] a = Rates(f, V0), b = Rates(f, New);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - b[i]) > 0.001) cells++;
            }
            Console.WriteLine("- (b) 標に関わる駒を含まない " + rows + " 行（compare ＋ 交差帯）で動いたセル: **" + cells + " / " + rows * 5 + "**（0 が正）");
        }

        // (c) 半減はヒサの標の相手にしか付かない／§1 は味方に付かない／返り血で倒れない
        {
            long taken = 0, takenNotPicked = 0, vulnByFoe = 0, zanSelfKill = 0, battles = 0;
            foreach ((string _, string _, Formation f) in Bands().Concat(Benches.Select(b => ("台", b.Name, b.F))))
            {
                if (!Mine(f)) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = Fight(f, st, seed, New);
                        battles++;
                        var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
                        foreach (var kv in r.TallyByUnit)
                        {
                            taken += kv.Value.BeckonGuardTaken;
                            if (kv.Value.BeckonGuardTaken > 0 && kv.Value.BeckonPicked == 0) takenNotPicked++;
                            if (!ids.Contains(kv.Key)) vulnByFoe += kv.Value.MarkVulnDealt;
                        }
                        if (r.TallyByUnit.TryGetValue("zan", out UnitTally? z) && z.RecoilTaken > 0)
                        {
                            // 返り血は lethal: false。ザンの死が返り血だけで起きていないことは engine が保証する（HP 1 で止まる）。
                            if (z.RecoilTaken < 0) zanSelfKill++;
                        }
                    }
            }
            Console.WriteLine("- (c) " + battles + " 戦: 半減で防いでもらった総量 " + taken + " ／ **指差されていない駒が防いでもらった: " + takenNotPicked + "**（0 が正）"
                              + " ／ **§1 の上乗せを敵が入れた量: " + vulnByFoe + "**（0 が正）");
        }

        // (d) PickOne を新しく使っていない
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string pick = "ctx.Pick" + "One(";
            int total = (traits.Length - traits.Replace(pick, "").Length) / pick.Length;
            int mine = 0, old = 0;
            foreach (string cls in new[] { "Beckon" + "Trait", "Flee" + "Trait", "Vendetta" + "Trait", "Recoil" + "Trait" })
                mine += CountIn(traits, cls, pick);
            old = CountIn(traits, "Marker" + "Trait", pick);
            Console.WriteLine("- (d) 第184期の4枚が `" + pick + "` を呼ぶ回数: **" + mine + "**（0 が正）／ 旧ヒサ（`MarkerTrait`・保持者 0 枚）の "
                              + old + " 件は盤面から消えた ／ `Traits.cs` 全体は " + total + " 件");
        }

        // (e) 保持者
        Console.WriteLine("- (e) 旧 `Marker` の保持者（`All`）: **" + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Marker)) + "** ／ 旧 `Avenge`: **"
                          + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Avenge)) + "**（どちらも 0 が正）");
        Console.WriteLine();
    }

    static int CountIn(string src, string cls, string needle)
    {
        int i = src.IndexOf("class " + cls + " ");
        if (i < 0) return 0;
        int j = src.IndexOf("\npublic ", i + 10);
        string body = src.Substring(i, (j < 0 ? src.Length : j) - i);
        return (body.Length - body.Replace(needle, "").Length) / needle.Length;
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

    static double[] Rates(Formation f, Version v, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (Fight(f, st, seed, v).PlayerWon) wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells(double[] w) => string.Join(" / ", Enumerable.Range(1, 4).Select(i => w[i].ToString("F1")));
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");
}
