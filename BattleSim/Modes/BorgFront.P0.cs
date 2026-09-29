using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BG = BorgGuardDiag;

// borgfront phase0 —— Q0-1 燃焼ダメージの経路 ／ Q0-2 熾火の免除とベニの反転 ／ Q0-3 火の回復の経路 ／ Q0-4 過去の実験 ／ Q0-5 台。
static partial class BorgFrontDiag
{
    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第235期 Phase 0");
        Console.WriteLine();
        string src = File.ReadAllText(Path.Combine("BattleCore", "BattleEngine.cs")).Replace("\r\n", "\n");

        // Q0-1: 単発の一撃を燃焼ダメージとして与える経路があるか（走査）
        Console.WriteLine("## Q0-1 燃焼ダメージの経路（走査）");
        Console.WriteLine();
        int body = src.IndexOf("void BurnTickOnce(", StringComparison.Ordinal);
        int det = src.IndexOf("void DetonateOne(", StringComparison.Ordinal);
        if (body < 0 || det < 0) throw new InvalidOperationException("燃焼の刻みの本体が見つからない（R034）");
        var calls = new List<string>();
        int i = 0;
        while ((i = src.IndexOf("burnTick: true", i + 1, StringComparison.Ordinal)) >= 0)
        {
            int ls = src.LastIndexOf('\n', i) + 1;
            int fn = src.LastIndexOf("\n    void ", i, StringComparison.Ordinal);
            int fn2 = src.LastIndexOf("\n    public ", i, StringComparison.Ordinal);
            int f = Math.Max(fn, fn2);
            string name = src.Substring(f + 1, src.IndexOf('(', f) - f - 1).Trim();
            calls.Add($"{name}: `{src.Substring(ls, src.IndexOf('\n', i) - ls).Trim()}`");
        }
        Console.WriteLine($"`ApplyDamage(…, burnTick: true)` を呼ぶ箇所 {calls.Count} 件:");
        foreach (var c in calls) Console.WriteLine("- " + c);
        Console.WriteLine();
        Console.WriteLine("- **出どころ（source）を持つ一撃を燃焼として与える経路は 0 件**——燃焼の刻み・起爆は出どころ null で、「火に焼かれない」と「ベニの反転」は `ApplyDamage` の**手前**（刻みの本体の中）で分かれる");
        Console.WriteLine("- 足し方: 刻みの本体と同じ順（① 焼かれない駒 ② ベニの反転 `InverseHeal` ③ それ以外）を1本の窓口 `BattleContext.FireSplashHit` に写し、③ は**今までの巻き込みと同じ** `ApplyDamage(ally, spill, borg, isFriendlyFire: true)`");
        Console.WriteLine("  ——したがって脆さ（入口の族・規定 F1 は敵だけ）→ 据え…層 → 火の鎧の半減 → 巨躯 → 分かち → 破片 → 身構え → 軛 の順は巻き込みのまま、被弾で動く札の反応も巻き込みのまま（`burnTick: true` にしない）");
        Console.WriteLine();

        // Q0-2: 熾火の免除は反転より前か（本文の位置）
        Console.WriteLine("## Q0-2 熾火の免除とベニの反転（本文の位置）");
        Console.WriteLine();
        int pyre = src.IndexOf("u.HasTrait(TraitId.Pyre)", body, StringComparison.Ordinal);
        int inv = src.IndexOf("InvertsTick(u)", body, StringComparison.Ordinal);
        Console.WriteLine($"- 燃焼の刻み: 焼かれない枝（{src.Take(pyre).Count(c => c == '\n') + 1} 行）が反転の枝（{src.Take(inv).Count(c => c == '\n') + 1} 行）より{(pyre < inv ? "前" : "後")}");
        int pyre2 = src.IndexOf("u.HasTrait(TraitId.Pyre)", det, StringComparison.Ordinal);
        int inv2 = src.IndexOf("InvertsTick(u)) is UnitState inverterB", det, StringComparison.Ordinal);
        Console.WriteLine($"- 起爆: 焼かれない枝が反転の枝より{(pyre2 < inv2 ? "前" : "後")}");
        // 実測: ホタがベニの隣で燃えたとき、反転の回復が届いているか
        var f1 = BA.Seat(new[] { UnitCatalog.BorgF0, UnitCatalog.HiyoF0, UnitCatalog.Hota, UnitCatalog.Beni, UnitCatalog.Mio });
        long hotaBurnTicks = 0, hotaInverted = 0, hotaBurning = 0, adj = 0;
        for (int w = 0; w < BA.WaveNames.Length; w++)
            for (int s = 0; s < 50; s++)
            {
                var (r, p, _, _) = BA.Fight(f1, w, BA.Scales[0].Sc, s);
                int hota = p.First(u => u.Def.Id == "hota").InstanceId;
                hotaInverted += r.Events.Count(e => e.Kind == BattleEventKind.Status && e.TargetId == hota && e.InverterId is not null && e.Text == "燃焼");
                hotaBurning += r.Events.Count(e => e.Kind == BattleEventKind.StatusSnapshot && e.TargetId == hota && e.Text == StatusKeys.LabelOf(StatusKeys.Burn));
                hotaBurnTicks += r.Log.Count(l => l.Text.Contains("熾のホタ は燃えているが焼かれない"));
            }
        adj = FormationRules.AreAdjacent(2, 3) ? 1 : 0;
        Console.WriteLine($"- 実測（{BA.SeatsNamed(f1)}・ホタとベニは{(adj == 1 ? "隣" : "隣でない")}・6 波 × seed 50）: ホタが燃えていたターン {hotaBurning}・焼かれなかった刻み {hotaBurnTicks}・**反転の回復が届いた刻み {hotaInverted}**");
        Console.WriteLine("  ——焼かれない枝が先に `return` するので、**ベニの結界の内側でもホタには反転の回復が届いていない**（直さない。事実だけ）");
        Console.WriteLine();

        // Q0-3
        Console.WriteLine("## Q0-3 火の回復の経路");
        Console.WriteLine();
        Console.WriteLine("- `ctx.Heal` の入口の順: 支援拒否（`Stoic`）→ **渇き**（`DroughtBinding`）→ **反転の裏**（`InverseLeak`・`inverted` なら素通り）→ HP を足す（上限で切る）");
        Console.WriteLine("- ベニの反転の回復（`InverseHeal`）は `Heal(…, inverted: true)`——**渇きには封じられ、反転の裏は通らない**");
        Console.WriteLine("- 火の回復（`BattleContext.FireHeal`）は `Heal(…, inverted: true, fireHeal: …)`: 反転の裏は通らない（H1・H2 とも）／ `fireHeal` が真なら渇きを素通り（H1a）・`FireMendDry` を持てば偽（H1b）");
        Console.WriteLine("- 火の癒しは燃焼の刻みの「焼かれない」枝の中で `return` するので、**ベニの反転（刻み → 回復）とは二重にならない**（刻み1回に回復1回）");
        Console.WriteLine();

        // Q0-5
        Console.WriteLine("## Q0-5 台");
        Console.WriteLine();
        var c3 = BA.Candidates; var cth = ThunderCandidates;
        Console.WriteLine($"- T3: 相方の候補 {c3.Count} 枚 → {c3.Count * (c3.Count - 1) / 2} 組 × 席 120 × seed {BA.PickSeeds}（版 {Versions.Length}）");
        Console.WriteLine($"- T3′: シオ・ササを除く {c3.Count(u => u.Id is not ("shio" or "sasa"))} 枚 → {(c3.Count - 2) * (c3.Count - 3) / 2} 組（T3 の表から抜き出す）");
        Console.WriteLine($"- 雷＋ボルグ: 候補 {cth.Count} 枚（T3 の 47 枚からベニ・ミオ・カタを除く）× 席 120 × seed {BA.PickSeeds}");
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var probe = BA.AllSeats(BG.CoreOf(VerBorg("全部")).Concat(new[] { UnitCatalog.Golm, UnitCatalog.Susu }).ToArray(), BA.PickSeeds);
        double us = sw2.Elapsed.TotalMilliseconds * 1000 / (120.0 * BA.PickSeeds);
        Console.WriteLine($"- 所要の見積もり（全部・ゴルム＋アカの 120 席で {sw2.Elapsed.TotalSeconds:F1} 秒）: T3 1 版 ≒ {us * 1081 * 120 * BA.PickSeeds / 1e6 / 60:F1} 分・雷＋ボルグ 1 版 ≒ {us * cth.Count * 120 * BA.PickSeeds / 1e6 / 60:F1} 分");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
