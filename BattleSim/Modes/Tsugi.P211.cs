using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第211期） —— ツギの板を「殴られている前衛」に集める（貼る相手・応急処置・カドの棘・戦績表）
//
// 指示書は design/PHASE211_TSUGI5_SPEC.md ／ 報告は design/PHASE211_TSUGI5.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 tsugi phase211  # Q0-1〜Q0-4（Y0 ＝ 第210期の規定で数える）
//     dotnet run --project BattleSim -c Release 0 tsugi run5      # ツギの在席 3 行 × Y0〜Y3
//     dotnet run --project BattleSim -c Release 0 tsugi swap5     # リリの席にツギ（L ／ Y0〜Y3）
//     dotnet run --project BattleSim -c Release 0 tsugi ledger5   # 板の行き先・前衛の倒れ・応急処置・棘の帳簿
//     dotnet run --project BattleSim -c Release 0 tsugi y3diff [第210期のbalance.md]  # Y3 の全61行の差分（ツギのいない行で動いたセル）
//     dotnet run --project BattleSim -c Release 0 tsugi check5 [第210期のbalance.md]  # 自己検査
//
// 版は札の差し替えだけ: Y0 ＝ 第210期 W3 のツギ ＋ 第210期のカド ／ Y1 ＝ ツギに `PlankNeediest` ／ Y2 ＝ ＋ `FirstAidArmored` ／
// Y3（規定）＝ Y2 のツギ ＋ カドに `ThornsArmored`。**版の定義はこのファイルの中だけで作る**（partial class の静的初期化子の順・第196期）。
// =====================================================================================

static partial class TsugiDiag
{
    static readonly TraitId[] Y0Traits =
    {
        TraitId.Plank, TraitId.PlankScorch, TraitId.Scrap, TraitId.PlankRebound, TraitId.PlankThick,
        TraitId.PlankBase, TraitId.FirstAid, TraitId.PlankSkill,
    };

    static UnitDef YDef(params TraitId[] more) => Clone(UnitCatalog.Tsugi, Y0Traits.Concat(more).ToArray());

    /// <summary>第210期のカド（<c>ThornsArmored</c> を外した札）と、Y3 のカド（末尾に足した札）。</summary>
    static readonly UnitDef Kado0 = Clone(UnitCatalog.Kado, UnitCatalog.Kado.Traits.Where(t => t != TraitId.ThornsArmored).ToArray());
    static readonly UnitDef Kado3 = Clone(UnitCatalog.Kado, UnitCatalog.Kado.Traits.Where(t => t != TraitId.ThornsArmored).Append(TraitId.ThornsArmored).ToArray());

    static readonly (string Tag, UnitDef Tsugi, UnitDef Kado)[] YVersions =
    {
        ("Y0", YDef(), Kado0),
        ("Y1", YDef(TraitId.PlankNeediest), Kado0),
        ("Y2", YDef(TraitId.PlankNeediest, TraitId.FirstAidArmored), Kado0),
        ("Y3", YDef(TraitId.PlankNeediest, TraitId.FirstAidArmored), Kado3),
    };

    /// <summary>版を当てる: <paramref name="seat"/> の駒をツギに、カドを版のカドに（どちらも居なければそのまま）。</summary>
    static Formation ApplyY(Formation f, string seat, string tag)
    {
        var v = YVersions.First(x => x.Tag == tag);
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef u) in f.Occupied())
            g[slot] = u.Id == seat ? v.Tsugi : u.Id == "kado" ? v.Kado : u;
        return g;
    }

    static IEnumerable<(string Band, string Name, Func<int, Agg> At)> YRows(string tag)
    {
        foreach (var (name, f) in TsugiRows())
            yield return ("compare", name, st => Measure(name + "|" + tag, ApplyY(f, "tsugi", tag), st));
        foreach (var (name, f) in LiliRows())
            yield return ("差し替え", name, st => Measure(name + "|S" + tag, ApplyY(f, "lili", tag), st));
    }

    static readonly string[] YTags = { "Y0", "Y1", "Y2", "Y3" };

    /// <summary>破片の書き手（Q0-2・<b>手で持つ表</b>。実測は `ThornArmorMuted` が数える）。</summary>
    static readonly (TraitId Id, string What)[] ArmorWriterTraits =
    {
        (TraitId.Plank, "板（ツギ）"), (TraitId.Shatter, "砕け（ヒビ）"), (TraitId.Brace, "身構え（ササ）"),
        (TraitId.Bear, "引き受け（ウケ・自分）"), (TraitId.Scale, "鱗（ウロ・自分）"),
    };

    static partial void RunMore211(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "phase211": Phase211(); handled = true; return;
            default: RunMore211b(mode, arg, ref handled); return;
        }
    }

    static partial void RunMore211b(string mode, string arg, ref bool handled);

    static string RowName(int r) => r switch { 0 => "前", 1 => "中", _ => "後" };

    // =================================================================================
    // phase211 —— Q0-1〜Q0-4（Y0 ＝ 第210期の規定）
    // =================================================================================

    static void Phase211()
    {
        Console.WriteLine("# 第211期 `tsugi phase211` —— Q0-1〜Q0-4（Y0 ＝ 第210期の規定・seed 0..199・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**盤面は第210期のまま**（足したのは計数と、誰も持っていない札3枚だけ・`compare` 305 セル 0 件差分）。");
        Console.WriteLine();

        // ---------------------------------------------------------------------------
        Console.WriteLine("## Q0-1 カドの棘が鳴る条件");
        Console.WriteLine();
        Console.WriteLine("- 棘（`ThornsTrait.OnDamaged`）は **HP が減った一撃の通知**で鳴る。`ApplyDamage` は破片の段で一撃を全部吸うと "
                          + "`return` し（「破片で受け切ったなら何も起きなかったと扱う」）、`OnDamaged` も `OnAllyDamaged` も呼ばない。");
        Console.WriteLine("- 破片が一部だけ吸った一撃は、残りが HP に届くので**今でも鳴る**（量は `dmg` を読まず自分の攻撃力 × 2）。鳴らないのは**受け切った一撃だけ**。");
        Console.WriteLine("- 拾える位置は、その `return` の直前（破片の段・軛より前）。出どころ・刻みか・反撃の中かはそこで全部分かる。"
                          + "第211期はそこに `ArmorOnlyHit` を置き、`ThornsArmored` を持つ棘の保持者だけ棘の本体（`ThornsTrait.Riposte` に切り出した）を呼ぶ。"
                          + "門は棘と同じ（敵陣営の出どころ・刻みでない・反撃の中でない・`CanActOutOfTurn`＝粛で止まる）。");
        Console.WriteLine("- 棘守り（`ThornGuard` の入れ替え）は `OnDamaged` のままなので、破片で受け切った一撃では入れ替えない（指示書 §2.3 は棘だけ）。");
        Console.WriteLine();

        // ---------------------------------------------------------------------------
        Console.WriteLine("## Q0-2 カドが破片で一撃を受け切る `compare` 行（Y3 で動きうる行の予告）");
        Console.WriteLine();
        Console.WriteLine("`compare` 61 行のうちカドを含む行を、第210期の盤面のまま回した。**受け切り/戦** ＝ カドが敵の一撃を破片で受け切って棘が鳴らなかった回数 ／ "
                          + "**棘/戦** ＝ 実際に鳴った棘の回数 ／ **比** ＝ 受け切り ÷ (棘 ＋ 受け切り)。**破片の書き手** は同じ行の駒の札から引く。");
        Console.WriteLine();
        Console.WriteLine("| 行 | ツギ | 破片の書き手 | 受け切り/戦 第2 / 3 / 4 / 5 | 棘/戦 第2 / 3 / 4 / 5 | 比 |");
        Console.WriteLine("|---|:-:|---|---|---|--:|");
        var predicted = new List<string>();
        foreach (var (name, f) in CompareBuilds())
        {
            if (!f.Occupied().Any(o => o.Def.Id == "kado")) continue;
            var ag = Waves.Select(st => Measure(name + "|P0", ApplyY(f, "tsugi", "Y0"), st)).ToArray();
            var mu = ag.Select(a => a.Per(a.Of("kado").ThornArmorMuted)).ToArray();
            var th = ag.Select(a => a.Per(a.Of("kado").ThornRipostes)).ToArray();
            string writers = string.Join("・", f.Occupied().SelectMany(o => ArmorWriterTraits.Where(w => o.Def.Traits.Contains(w.Id)).Select(w => w.What)).Distinct());
            double m = mu.Sum(), t = th.Sum();
            if (m > 0) predicted.Add(name);
            Console.WriteLine("| " + name + " | " + (HasTsugi(f) ? "○" : "") + " | " + (writers == "" ? "—" : writers) + " | "
                              + string.Join(" / ", mu.Select(x => x.ToString("F2"))) + " | " + string.Join(" / ", th.Select(x => x.ToString("F2"))) + " | "
                              + (m + t == 0 ? "—" : (100 * m / (m + t)).ToString("F1") + "%") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**Y3 で動きうる `compare` 行（受け切りが 1 回でもある行）**: " + (predicted.Count == 0 ? "なし" : string.Join(" ／ ", predicted)) + "。");
        Console.WriteLine();
        Console.WriteLine("差し替え（リリの席にツギ）でカドを含む行も同じ表で:");
        Console.WriteLine();
        Console.WriteLine("| 行 | 受け切り/戦 第2 / 3 / 4 / 5 | 棘/戦 第2 / 3 / 4 / 5 |");
        Console.WriteLine("|---|---|---|");
        foreach (var r in YRows("Y0").Where(r => r.Band == "差し替え"))
        {
            var ag = Waves.Select(r.At).ToArray();
            if (!ag.Any(a => a.T.ContainsKey("kado"))) continue;
            Console.WriteLine("| " + r.Name + " | " + string.Join(" / ", ag.Select(a => a.Per(a.Of("kado").ThornArmorMuted).ToString("F2"))) + " | "
                              + string.Join(" / ", ag.Select(a => a.Per(a.Of("kado").ThornRipostes).ToString("F2"))) + " |");
        }
        Console.WriteLine();

        // ---------------------------------------------------------------------------
        Console.WriteLine("## Q0-3 手番の板・応急処置の対象の内訳（Y0）");
        Console.WriteLine();
        Console.WriteLine("列は貼った瞬間の相手の席の列（前・中・後）。**自分** ＝ ツギ自身に貼った割合（列にも含まれる）。"
                          + "**候補と一致** ＝ 実際に貼った相手が「(HP＋破片)÷最大HP が最小」の候補に入っていた割合 ＝ Y1 でも同じ相手に貼る見当。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 波 | 板/戦 | 前 / 中 / 後 | 自分 | 候補と一致 | 候補がツギだけ | 応急/戦 | 前 / 中 / 後 | 自分 |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|--:|--:|---|--:|");
        foreach (var band in new[] { "compare", "差し替え" })
            foreach (int st in Waves.Append(-1))
            {
                var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => YRows("Y0").Where(r => r.Band == band).Select(r => r.At(w))).ToList();
                SumAll(ags, out int n, out var ts);
                long[] pr = ts.PlankToRow ?? new long[3], fr = ts.FirstAidToRow ?? new long[3];
                Console.WriteLine("| " + band + " | " + WaveLab(st) + " | " + ((double)ts.PlankPastes / Math.Max(1, n)).ToString("F2") + " | "
                                  + string.Join(" / ", pr.Select(x => Pct(x, ts.PlankPastes))) + " | " + Pct(ts.PlankSelf, ts.PlankPastes) + " | "
                                  + Pct(ts.PlankHypSame, ts.PlankHypPicks) + " | " + Pct(ts.PlankHypSelf, ts.PlankHypPicks) + " | "
                                  + ((double)ts.FirstAidFired / Math.Max(1, n)).ToString("F2") + " | "
                                  + string.Join(" / ", fr.Select(x => Pct(x, ts.FirstAidFired))) + " | " + Pct(ts.FirstAidSelf, ts.FirstAidFired) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("### 行ごと（第2〜5波の合算）");
        Console.WriteLine();
        Console.WriteLine("**板を受けた** ＝ 手番の板を受けた回数/戦（駒ごと・多い順）／ **候補になった** ＝ 「実質の残り体力が最小」の候補に入った回数/戦（Y1 で板が行く先の見当）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 板を受けた（Y0） | 候補になった（Y1 の見当） |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var r in YRows("Y0"))
        {
            var ags = Waves.Select(r.At).ToList();
            int n = ags.Sum(a => a.N);
            var by = new Dictionary<string, (long Recv, long Hyp)>();
            foreach (var a in ags) foreach (var (id, t) in a.T) { var o = by.GetValueOrDefault(id); by[id] = (o.Recv + t.PlankReceived, o.Hyp + t.PlankHypChosen); }
            string Nm(string id) => UnitCatalog.Everyone.FirstOrDefault(u => u.Id == id)?.Name ?? id;
            Console.WriteLine("| " + r.Band + " | " + r.Name + " | "
                              + string.Join(" ／ ", by.Where(p => p.Value.Recv > 0).OrderByDescending(p => p.Value.Recv).Select(p => Nm(p.Key) + " " + ((double)p.Value.Recv / n).ToString("F2"))) + " | "
                              + string.Join(" ／ ", by.Where(p => p.Value.Hyp > 0).OrderByDescending(p => p.Value.Hyp).Select(p => Nm(p.Key) + " " + ((double)p.Value.Hyp / n).ToString("F2"))) + " |");
        }
        Console.WriteLine();

        // ---------------------------------------------------------------------------
        Console.WriteLine("## Q0-4 「(HP＋破片)÷最大HP」で選ぶ偏り（Y0 の盤面で、手番ごとの候補を数える）");
        Console.WriteLine();
        Console.WriteLine("**偏り** ＝ 1戦の中で最も多く候補になった駒が候補の延べ数に占める割合の平均（候補が出た戦だけ）／ **何体** ＝ 1戦で候補になった駒の数の平均 ／ "
                          + "**候補の破片** ＝ 候補（1体目）が持っていた破片の平均 ／ **破片あり** は候補が破片を持っていた割合の見当（破片の合計 > 0 の手番）。"
                          + "**Y0 の盤面で数えている**ので、Y1 で板を貼った後の変化（貼られた駒が候補から外れる）は入らない——実測は `ledger5`。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 波 | 偏り | 何体 | 候補の破片 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var band in new[] { "compare", "差し替え" })
            foreach (int st in Waves.Append(-1))
            {
                var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => YRows("Y0").Where(r => r.Band == band).Select(r => r.At(w))).ToList();
                SumAll(ags, out int n, out var ts);
                int hb = ags.Sum(a => a.HypBattles);
                Console.WriteLine("| " + band + " | " + WaveLab(st) + " | " + (hb == 0 ? "—" : (100 * ags.Sum(a => a.HypTopShareSum) / hb).ToString("F1") + "%") + " | "
                                  + (hb == 0 ? "—" : (ags.Sum(a => a.HypDistinctSum) / hb).ToString("F2")) + " | "
                                  + (ts.PlankHypPicks == 0 ? "—" : ((double)ts.PlankHypArmor / ts.PlankHypPicks).ToString("F1")) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("### 前衛が倒れた戦と、倒れた一撃の直前の破片（Y0）");
        Console.WriteLine();
        Console.WriteLine("**前衛倒れ** ＝ 前列で始めた駒が1体以上倒れた戦の割合 ／ **倒れた直前の破片** ＝ 倒れた一撃を受ける直前に持っていた破片の平均（倒れた回数）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 前衛倒れ 第2 / 3 / 4 / 5 | 倒れた直前の破片（回数） |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var r in YRows("Y0"))
        {
            var ags = Waves.Select(r.At).ToList();
            long dc = ags.Sum(a => a.T.Values.Sum(t => t.DiedCount)), da = ags.Sum(a => a.T.Values.Sum(t => t.DiedArmorBefore));
            Console.WriteLine("| " + r.Band + " | " + r.Name + " | " + string.Join(" / ", ags.Select(a => F1(100.0 * a.FrontFell / Math.Max(1, a.N)))) + " | "
                              + (dc == 0 ? "—" : ((double)da / dc).ToString("F1")) + "（" + dc + "） |");
        }
        Console.WriteLine();
    }
}
