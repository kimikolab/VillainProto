using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// ep3 モード（第203期） —— 敵にパターン3（前衛1枚・中衛2・後衛2）を持たせる
//
// 指示書は design/PHASE203_ENEMY_P3_SPEC.md ／ 報告は design/PHASE203_ENEMY_P3.md。
//
// **線は置かない**（採否はポンが遊んで決める）。既存の敵をそのまま使い、陣形だけをパターン3にした
// 「写し」（第三波・第五波）を元の波（X 字）と並べるだけ。
//
//     dotnet run --project BattleSim -c Release 0 ep3 phase0   # Q0-1〜Q0-6 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 ep3 run      # 主表・攻撃の型で分けた読み・帳簿
//     dotnet run --project BattleSim -c Release 0 ep3 check [採用前のbalance.md]  # 自己検査
// =====================================================================================

static partial class EnemyP3Diag
{
    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            default:
                bool handled = false;
                RunMoreImpl(mode, arg, ref handled);
                if (!handled) Console.WriteLine("ep3: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunMoreImpl(string mode, string arg, ref bool handled);

    // =================================================================================
    // 攻撃の型の分類（Q0-4 と §5.2 が共有する）
    // =================================================================================

    /// <summary><c>ModifyPattern</c> で<b>貫き</b>に化ける札のクラス（第203期 Phase 0 に `Traits.cs` から引いた）。</summary>
    static readonly string[] PierceUp = { "SniperTrait", "ScaleTrait", "PyreTrait" };
    /// <summary><c>ModifyPattern</c> で<b>薙ぎ・全体</b>に化ける札のクラス。</summary>
    static readonly string[] AoeUp = { "NecroTrait", "DisplacedTrait", "OverloadTrait", "GradeTrait", "LastStandTrait" };

    /// <summary>
    /// <c>ModifyPattern</c> を上書きしている札（リフレクションで引く）。上の2つの表に無いクラスが出たら
    /// <b>止める</b>（R034: 走査が知らないものを「該当なし」に落とさない）。
    /// </summary>
    static Dictionary<TraitId, string>? PatternTraits()
    {
        var map = new Dictionary<TraitId, string>();
        var unknown = new List<string>();
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            Trait t;
            try { t = TraitCatalog.Get(id); } catch (KeyNotFoundException) { continue; }
            MethodInfo? m = t.GetType().GetMethod("ModifyPattern");
            if (m is null || m.DeclaringType == typeof(Trait)) continue;
            string cls = m.DeclaringType!.Name;
            if (PierceUp.Contains(cls)) map[id] = "P";
            else if (cls == "LastStandTrait") map[id] = "L";   // 剣の段: 最後の1体のときだけ（行の性質ではない）
            else if (AoeUp.Contains(cls)) map[id] = "A";
            else unknown.Add(cls + "(" + id + ")");
        }
        if (unknown.Count > 0)
        {
            Console.WriteLine("**`ModifyPattern` を上書きする札に分類の無いものがある: " + string.Join(", ", unknown.Distinct()) + "。止める。**");
            return null;
        }
        return map;
    }

    internal enum Kind { AlwaysPierce, CondPierce, Aoe, SingleOnly }

    internal static readonly string[] KindNames = { "常時の貫きがいる", "条件付きの貫きだけ", "薙ぎ・全体を持つ", "単体だけ" };

    /// <summary>1駒の型: 常時貫き P ／ 条件貫き p ／ 常時範囲 A ／ 条件範囲 a ／ 単体 S（並べて返す）。</summary>
    static string UnitKinds(UnitDef d, Dictionary<TraitId, string> pt)
    {
        string s = "";
        if (d.Pattern == AttackPattern.Pierce) s += "P";
        if (d.Pattern is AttackPattern.Sweep or AttackPattern.All) s += "A";
        foreach (TraitId t in d.Traits)
            if (pt.TryGetValue(t, out string? k)) s += k.ToLowerInvariant();
        return s == "" ? "S" : s;
    }

    /// <summary>
    /// 行の分類（§5.2）。優先は 常時貫き ＞ 条件付き貫き ＞ 薙ぎ・全体 ＞ 単体だけ。
    /// <b>ガルドの剣の段（`l`）は数えない</b>——味方が最後の1体になった戦でしか立たないので、行の性質ではない。
    /// </summary>
    static Kind RowKind(Formation f, Dictionary<TraitId, string> pt)
    {
        var ks = f.Occupied().Select(o => UnitKinds(o.Def, pt)).ToList();
        if (ks.Any(k => k.Contains('P'))) return Kind.AlwaysPierce;
        if (ks.Any(k => k.Contains('p'))) return Kind.CondPierce;
        if (ks.Any(k => k.Contains('A') || k.Contains('a'))) return Kind.Aoe;
        return Kind.SingleOnly;
    }

    static bool HasTrait(Formation f, TraitId id) => f.Occupied().Any(o => o.Def.Traits.Contains(id));

    // =================================================================================
    // Phase 0 —— 戦闘0回
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第203期 `ep3 phase0` —— Q0-1〜Q0-6（戦闘0回）");
        Console.WriteLine();
        if (!ParryScan.Init()) return;
        string root = ParryScan.Root!;
        string traitsSrc = File.ReadAllText(Path.Combine(root, "BattleCore", "Traits.cs"));
        string engineSrc = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 敵の隊に `FormationShape` を持たせる入口");
        Console.WriteLine();
        bool matShape = engineSrc.Contains("Shape = formation.Shape,") && engineSrc.Contains("Slot = formation.Shape.PlayableSlots[slot]");
        Console.WriteLine("- `BattleEngine.Materialize(Formation, team)` は**陣営を問わず** `formation.Shape` を駒へ写し、枠 i を `Shape.PlayableSlots[i]` の席に立てる: "
                          + (matShape ? "**確認**" : "**見つからない（止める）**"));
        Console.WriteLine("- 敵の編成も同じ `Formation` なので、**第200期の味方用の口がそのまま使える**。敵の倍率（`EnemyScaleRule`）も同じ `Materialize` の中で掛かる");
        Console.WriteLine("- 敵が呼んだ召喚は `ShapeOfTeam(teamId)`（その陣営の最初の駒の陣形）を引くので、敵がパターン3なら召喚もパターン3の表に立つ");
        Console.WriteLine("- 貫きの経路は**撃たれる側の陣形**（`target.Shape` / `foes[0].Shape`）で引き、どちらの2レーンかは**撃つ側の席の格子のレーン**（`FormationShape.GridLane(attacker.Slot)`）で選ぶ（第202期の `PierceRule.Facing`）"
                          + "——撃つ側の陣形は読まないので、X 字の味方もパターン2の味方も席の番号から格子のレーンが決まる");
        Console.WriteLine();

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 敵側の札が席・列・レーン・隣接を読んでいるか");
        Console.WriteLine();
        Formation w3 = EnemyCatalog.Stages[2].Enemy, w5 = EnemyCatalog.Stages[4].Enemy;
        string[] geo = { "AreAdjacent", "SweepTargets", "LanesOf", "LanePath", "IsSummonSlot", "PlayableSlots", ".Row", "RowOf", "DepthOf", ".Slot" };
        Console.WriteLine("| 波 | 席 | 駒 | 型 | 札 | 札の本文が読む幾何 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (wn, f) in new[] { ("三", w3), ("五", w5) })
            foreach ((int slot, UnitDef d) in f.Occupied())
            {
                var reads = d.Traits.Select(t =>
                {
                    string cls = TraitCatalog.Get(t).GetType().Name;
                    string body = ClassBody(traitsSrc, cls);
                    var hit = geo.Where(g => body.Contains(g)).ToList();
                    return t + ":" + (body.Length == 0 ? "**本文が引けない**" : hit.Count == 0 ? "なし" : string.Join(",", hit));
                }).ToList();
                Console.WriteLine("| " + wn + " | " + FormationRules.SeatNames[slot] + " | " + d.Name + " (`" + d.Id + "`) | " + d.Pattern
                                  + (d.Actions is null ? "" : "・溜め") + " | " + (d.Traits.Count == 0 ? "—" : string.Join("・", d.Traits)) + " | "
                                  + (reads.Count == 0 ? "—" : string.Join(" ／ ", reads)) + " |");
            }
        Console.WriteLine();
        bool martyrFront = engineSrc.Contains("f.HasTrait(TraitId.Martyr) && f.Row == Row.Front && f != target");
        Console.WriteLine("- **殉教（`Martyr`）の判定は engine 側**（`SelectTargetChain`）で、条件は `f.Row == Row.Front && f != target`: " + (martyrFront ? "**確認**" : "**見つからない**"));
        Console.WriteLine("  - パターン3の殉教者は C（○前2 ＝ 前列）に立つ。**C が生きている間、単体・薙ぎ・全体の主目標は C だけ**（`PoolOf` は前列の生存者）なので、"
                          + "**主目標が殉教者自身になり `f != target` で外れる**。庇いが働くのは主目標が C 以外になる経路だけ（標の引き寄せ・止め・執着・断ちは `pool` の中だけ）");
        Console.WriteLine("  - **予測: P3-五 の庇いの発動はほぼ 0 になる**（「唯一の前衛が庇い持ち」は、庇う相手がいない形）");
        Console.WriteLine("- 曝き（告発人 `Expose`）は**味方の陣を動かす**（`HaulOutPair(味方)`）ので、敵の陣形は読まない。味方の陣形（X 字）の列で働く");
        Console.WriteLine("- 処刑（勇者候補 `Executioner`）・断罪（`Condemn`）は撃破と反撃を読むだけで席を読まない。渇き（`Drought`）は盤面ルールで保持者の席を読まない");
        Console.WriteLine("- 狙撃手（`Actions` の溜め）と槍騎兵の貫き・戦斧兵の薙ぎは**味方の陣形**の表を引く（撃つ側の陣形は読まない）");
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 敵の召喚・分裂");
        Console.WriteLine();
        var summoners = w3.Occupied().Concat(w5.Occupied()).Where(o => o.Def.Traits.Any(t => ClassBody(traitsSrc, TraitCatalog.Get(t).GetType().Name).Contains("ctx.Summon("))).ToList();
        Console.WriteLine("- 第三波・第五波の駒で、札の本文に `ctx.Summon(` を持つもの: **" + summoners.Count + " 体**" + (summoners.Count == 0 ? "" : "（" + string.Join("・", summoners.Select(o => o.Def.Name)) + "）"));
        Console.WriteLine("- 敵陣に湧くのは**味方の**背かれ（ソム）の餌だけ。餌の席は `BetrayedTrait.FodderSlot` = " + BetrayedTrait.FodderSlot + "（○前2）の決め打ちで、"
                          + "**パターン3ではそこに C が立っている**ので `Summon` は陣形の召喚枠の走査へ落ちる（`IsSummonSlot(7)` が偽）");
        Console.WriteLine("- **決め: パターン3の召喚枠の走査は 前1 → 前3 → 中央 → ○後2**（9マスのうち編成に使っていない4つ・前から）。"
                          + "X 字の餌が ○前2（前列）に立つのと同じく、餌は前列に立つ＝「敵の前列が埋まる」という背かれの設計のまま");
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 味方の貫き・薙ぎ・全体の持ち主と `compare` 61 行");
        Console.WriteLine();
        Dictionary<TraitId, string>? pt = PatternTraits();
        if (pt is null) return;
        Console.WriteLine("`ModifyPattern` を上書きする札（リフレクション）: 貫きへ " + string.Join("・", pt.Where(p => p.Value == "P").Select(p => p.Key))
                          + " ／ 薙ぎ・全体へ " + string.Join("・", pt.Where(p => p.Value == "A").Select(p => p.Key))
                          + " ／ 剣の段（薙ぎ）" + pt.Count(p => p.Value == "L") + " 枚");
        Console.WriteLine();
        var rows = CompareBuilds();
        var units = rows.SelectMany(r => r.F.Occupied().Select(o => o.Def)).GroupBy(d => d.Id).Select(g => g.First()).ToList();
        Console.WriteLine("| 型 | 駒（`compare` に居るもの） | 在席行 |");
        Console.WriteLine("|---|---|--:|");
        foreach (var (label, pred) in new (string, Func<string, bool>)[]
        {
            ("常時の貫き", k => k.Contains('P')),
            ("条件付きの貫き", k => k.Contains('p')),
            ("常時の薙ぎ・全体", k => k.Contains('A')),
            ("条件付きの薙ぎ・全体", k => k.Contains('a')),
            ("剣の段（最後の1体だけ・群分けに使わない）", k => k.Contains('l')),
        })
        {
            var us = units.Where(d => pred(UnitKinds(d, pt))).ToList();
            int n = rows.Count(r => r.F.Occupied().Any(o => pred(UnitKinds(o.Def, pt))));
            Console.WriteLine("| " + label + " | " + string.Join("・", us.Select(d => d.Name + (d.Traits.Any(pt.ContainsKey) ? "(" + string.Join(",", d.Traits.Where(pt.ContainsKey)) + ")" : "")))
                              + " | " + n + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**§5.2 の行の分類**（優先: 常時貫き ＞ 条件付き貫き ＞ 薙ぎ・全体 ＞ 単体だけ）:");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | 行 |");
        Console.WriteLine("|---|--:|---|");
        foreach (Kind k in Enum.GetValues<Kind>())
        {
            var names = rows.Where(r => RowKind(r.F, pt) == k).Select(r => r.Name).ToList();
            Console.WriteLine("| " + KindNames[(int)k] + " | " + names.Count + " | " + string.Join(" ／ ", names) + " |");
        }
        Console.WriteLine();

        // ---------------- Q0-5 ----------------
        Console.WriteLine("## Q0-5 敵の席を動かす味方の札");
        Console.WriteLine();
        Console.WriteLine("| 札 | 読む席 | パターン3で | `compare` の在席行 |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var (id, what, p3) in new[]
        {
            (TraitId.Rebound, "敵の前1・前3（0/1 の決め打ち）", "**前1・前3 が空席なので毎手番「相手がいない」**（C の ○前2 は対象外）。壊れはしないが働かない"),
            (TraitId.Betrayed, "敵陣 ○前2（7 の決め打ち）", "C が立っているので陣形の召喚枠（前1 から）へ落ちる。餌は前列に立つ"),
            (TraitId.Shuffler, "敵の編成の駒どうしの入れ替え（召喚枠を除く・`IsSummonSlot(u)` は陣形を引く）", "5枚のどれとも入れ替わる。C と中衛・後衛が入れ替われば C が後ろへ下がる"),
            (TraitId.Shove, "`HaulOutPair`（後列 ⇔ 前列・列は幾何）", "後衛（A・D）⇔ C。変わらない"),
            (TraitId.Expose, "（敵側の札・味方を動かす）", "味方の陣形で働く"),
        })
        {
            int n = rows.Count(r => HasTrait(r.F, id));
            Console.WriteLine("| " + id + " | " + what + " | " + p3 + " | " + n + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- ハネの突き返し（`Rebound`）はパターン3の敵に対して**1回も働かない**（予測）。帳簿で `ReboundNoFront` を数える");
        Console.WriteLine();

        // ---------------- Q0-6 ----------------
        Console.WriteLine("## Q0-6 DemoApp の再生");
        Console.WriteLine();
        string view = File.ReadAllText(Path.Combine(root, "DemoApp", "BattlefieldView3D.cs"));
        int pp = view.IndexOf("Vector3 " + "PawnPosition(int team, int slot)", StringComparison.Ordinal);
        bool nine = pp >= 0 && Enumerable.Range(0, 9).All(s => view.IndexOf(s + " => (", pp, StringComparison.Ordinal) > 0);
        Console.WriteLine("- `BattlefieldView3D.PawnPosition(team, slot)` は**陣営を問わず 0〜8 の9席すべてに座標を持つ**: " + (nine ? "**確認**" : "**確認できない**"));
        Console.WriteLine("- 敵の x は `depth` を正へ（味方は負へ）写すだけなので、敵のパターン3（後1・○中1・○前2・後3・○中3）も今の座標でそのまま立つ見込み。"
                          + "**描き足しは要らない**。要るのは「写しの波で戦を始める口」（`--demo-enemy-p3`）だけ");
        Console.WriteLine();
    }

    /// <summary>`class <cls>` から、次に列 0 で始まる `public ` / `internal ` / `/// ` の行までの本文。</summary>
    static string ClassBody(string src, string cls)
    {
        int i = src.IndexOf("class " + cls + " ", StringComparison.Ordinal);
        if (i < 0) i = src.IndexOf("class " + cls + "\n", StringComparison.Ordinal);
        if (i < 0) i = src.IndexOf("class " + cls + "\r", StringComparison.Ordinal);
        if (i < 0) return "";
        int end = src.Length;
        foreach (string tok in new[] { "\npublic ", "\ninternal ", "\n/// ", "\r\npublic ", "\r\ninternal ", "\r\n/// " })
        {
            int j = src.IndexOf(tok, i + 1, StringComparison.Ordinal);
            if (j > 0 && j < end) end = j;
        }
        return src[i..end];
    }
}
