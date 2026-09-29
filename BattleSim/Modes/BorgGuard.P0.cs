using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// borgguard phase0 —— Q0-1 半減の置き場所 ／ Q0-2 焼け残りの判定 ／ Q0-3 G0 の確認 ／ Q0-4 台 ／ Q0-5 過去の実験。
static partial class BorgGuardDiag
{
    /// <summary>`ApplyDamageBody` の段の目印（本文の位置で並べる）。</summary>
    static readonly (string Label, string Marker)[] Stages =
    {
        ("回避（セロ）", "// 回避（第223期"),
        ("逸らし（ソラ）", "// 逸らし（第186期・DeflectTrait"),
        ("棘守りの上限（カド）", "// 棘守り（カド）の肩代わり上限"),
        ("駒の被ダメ修正（ModifyIncomingDamage）", "amount = t.ModifyIncomingDamage(target, amount);"),
        ("惨禍", "// 惨禍は「本人ではなく味方全体」"),
        ("荷", "// 荷（BurdenTrait・第154期）"),
        ("§1 敵の標 +50%", "// 敵の標の被ダメージ増（第184期 §1"),
        ("燃焼の脆さ", "// 燃焼の脆さ（第219期"),
        ("据え", "// 据え: このターン差し出された駒"),
        ("散開", "// 散開: 同じ列に隣り合う味方"),
        ("萎縮", "// 萎縮: 火力と引き換え"),
        ("矢面（ヒサ）", "// 矢面（第184期 §2"),
        ("据えの層（バン）", "// 据えの層（第185期"),
        ("**火の鎧の半減（この期）**", "// 火の鎧（第234期・`FireArmorTrait`）"),
        ("巨躯", "// 巨躯: 自分より前の列に立つ壁"),
        ("分かち", "// 分かち: 型を問わず肩代わり"),
        ("身構え（破片より先・BraceArmored）", "// 第212期（`BraceArmored`・ササ）"),
        ("破片", "if (armor > 0)\n"),
        ("受け流し", "// 受け流し（ParryTrait"),
        ("猶予", "// 猶予（ReprieveTrait"),
        ("身構え（ササ）", "// 身構え（BraceTrait・第143期）"),
        ("軛", "bool yokeBinding = amount > Yoke.Cap"),
        ("**焼け残り（この期）**", "// 焼け残り（第234期・`SmolderTrait`）"),
        ("必死の逃げ足（セロ）", "// 必死の逃げ足（第227期"),
        ("HP を引く", "target.Hp -= amount;"),
    };

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第234期 Phase 0");
        Console.WriteLine();

        // Q0-1 / Q0-2
        string src = File.ReadAllText(Path.Combine("BattleCore", "BattleEngine.cs")).Replace("\r\n", "\n");
        int body = src.IndexOf("void ApplyDamageBody(", StringComparison.Ordinal);
        int end = src.IndexOf("target.Hp -= amount;", body, StringComparison.Ordinal) + 30;
        if (body < 0 || end < body) throw new InvalidOperationException("ApplyDamageBody が見つからない（R034）。");
        var found = new List<(int Pos, string Label)>();
        var missing = new List<string>();
        foreach (var (label, marker) in Stages)
        {
            int i = src.IndexOf(marker, body, StringComparison.Ordinal);
            if (i < 0 || i > end) missing.Add(label); else found.Add((i, label));
        }
        if (missing.Count > 0) throw new InvalidOperationException("段の目印が見つからない: " + string.Join("・", missing) + "（R034）");
        int lineOf(int pos) => src.Take(pos).Count(c => c == '\n') + 1;
        Console.WriteLine("## Q0-1 / Q0-2 `ApplyDamageBody` の段の並び（本文の位置から機械で並べた）");
        Console.WriteLine();
        Console.WriteLine("| 順 | 段 | 行 |");
        Console.WriteLine("|---|---|---|");
        int k = 0;
        foreach (var (pos, label) in found.OrderBy(x => x.Pos)) Console.WriteLine($"| {++k} | {label} | {lineOf(pos)} |");
        Console.WriteLine();
        bool orderOk = found.Select(x => x.Label).SequenceEqual(found.OrderBy(x => x.Pos).Select(x => x.Label));
        Console.WriteLine($"- 並びが表（`Stages`）の順と一致: {(orderOk ? "○" : "**×**")}");
        Console.WriteLine();

        // Q0-3 G0 の確認
        Console.WriteLine("## Q0-3 G0（第233期の規定）の確認（九 / 新兵 × 200/200・seed 0..199）");
        Console.WriteLine();
        var g0 = UnitCatalog.Borg;
        var boards = new (string Name, Formation F)[]
        {
            ("233 B3-1（シオ＋ササ・1位）", BA.Seat(new[] { UnitCatalog.Hota, UnitCatalog.Sasa, UnitCatalog.Hiyo, UnitCatalog.Shio, g0 })),
            ("233 B3-2（シオ＋ハネ・1位）", BA.Seat(new[] { UnitCatalog.Hiyo, UnitCatalog.Hota, UnitCatalog.Shio, UnitCatalog.HaneR0, g0 })),
            ("233 B3-1 のボルグとホタを入れ替え（ボルグ前1）", BA.Seat(new[] { g0, UnitCatalog.Sasa, UnitCatalog.Hiyo, UnitCatalog.Shio, UnitCatalog.Hota })),
            ("参考 燃焼（`compare` の行・ガルド入り）", BA.RefBurn),
        };
        Console.WriteLine("| 台 | 全員生存 ／ 勝率 | ボルグ 生きていたT/戦 | 燃えていた | 前列にいた | 受けた敵の一撃/戦 | 受けた被ダメ/戦（敵 ／ 巻き込み ／ 刻み） | ボルグが倒れた | ボルグの被ダメ ÷ 味方全体 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var (nm, f) in boards)
        {
            var x = Measure(f, BA.MainWave, BA.Scales[0].Sc);
            var a = x.A;
            var t = a.Taken.GetValueOrDefault(g0.Name) ?? new long[BA.Takes.Length];
            long all = a.Taken.Values.Sum(v => v.Sum());
            Console.WriteLine($"| {nm} | {BA.F1(a.Surv)} ／ {BA.F1(a.Win)} | {BA.F2(a.Per(x.BorgAliveTurns))} | {Pct(x.BorgBurnTurns, x.BorgAliveTurns)} | {Pct(x.BorgFrontTurns, x.BorgAliveTurns)} | "
                + $"{BA.F2(a.Per(x.BorgEnemyHits))} | {BA.F1(a.Per(t[0]))} ／ {BA.F1(a.Per(t[1]))} ／ {BA.F1(a.Per(t[3]))} | {Pct(a.Fell.GetValueOrDefault("borg"), a.N)} | {Pct(t.Sum(), all)} |");
        }
        Console.WriteLine();
        Console.WriteLine("「燃えていた」＝ ボルグが生きていたターンのうち、ターン頭の写し（刻みの後）で燃焼が残っていたターン。「前列」＝ ターン頭の席が前の列。");
        Console.WriteLine();

        // Q0-4 台
        Console.WriteLine("## Q0-4 台の駒");
        Console.WriteLine();
        var c = BA.Candidates;
        var cp = c.Where(u => u.Id is not ("shio" or "sasa")).ToList();
        int pairs = c.Count * (c.Count - 1) / 2, pairsP = cp.Count * (cp.Count - 1) / 2;
        Console.WriteLine($"- T3 の相方の候補: {c.Count} 枚（第233期と同じ）→ {pairs} 組 × 席 120");
        Console.WriteLine($"- T3′ の候補: {cp.Count} 枚（シオ・ササを除く）→ {pairs - pairsP} 組を除いた **{pairsP} 組**——**T3 の部分集合なので段1 を回し直さない**（T3 の表から抜き出す）");
        Console.WriteLine("- T3-1: シオ＋ササの 1 組（T3 の表から引く）");
        var probe = CoreOf(Versions[1].Borg).Concat(new[] { UnitCatalog.Shio, UnitCatalog.Sasa }).ToArray();
        var t0 = sw.Elapsed;
        BA.AllSeats(probe, 20);
        double per = (sw.Elapsed - t0).TotalSeconds / (120 * 20);
        Console.WriteLine($"- 所要の見積もり（G1 の 120 席 × 20 seed）: 1戦 {per * 1e6:F0} μs → 1 版 {pairs * 120 * BA.PickSeeds * per / 60:F1} 分 × 5 版 ＝ {5 * pairs * 120 * BA.PickSeeds * per / 60:F1} 分（seed {BA.PickSeeds} 本）");
        Console.WriteLine();

        // Q0-5 過去の実験
        Console.WriteLine("## Q0-5 過去の実験（`design/*.md` の grep）");
        Console.WriteLine();
        var files = Directory.GetFiles("design", "*.md").Where(p => !Path.GetFileName(p).StartsWith("PHASE234")).ToArray();
        foreach (var kw in new[] { "殴ってきた", "殴った敵", "攻撃者に", "燃えている間", "燃えている味方", "被ダメが半分", "被ダメージが半分", "半分になる", "火の鎧", "焼け残", "HP1" })
        {
            var hits = files.Where(p => File.ReadAllText(p).Contains(kw)).Select(Path.GetFileNameWithoutExtension).ToList();
            Console.WriteLine($"- 「{kw}」: {hits.Count} 件" + (hits.Count > 0 ? "（" + string.Join("・", hits.Take(12)) + (hits.Count > 12 ? " ほか" : "") + "）" : ""));
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    internal static string Pct(long a, long b) => b == 0 ? "—" : BA.F1(100.0 * a / b) + "%";
}
