using System.Reflection;
using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// burnaudit phase0 —— Q0-1 仕様の確認 ／ Q0-3 台の駒・相方の役割・所要の見積もり・参考の台の現状。
static partial class BurnAuditDiag
{
    // ---------------------------------------------------------------------------------
    // 相方の役割（§4）: **札から機械的に分ける**——盤面で観測した出来事と、`Traits.cs` の走査だけで決める（手書き 0 行）。
    // ---------------------------------------------------------------------------------
    internal static readonly string[] RoleNames = { "回復", "守り", "火の書き手", "火・脆さの読み手", "状態異常の書き手", "移動", "その他" };

    /// <summary>`Traits.cs` の走査: クラスの本文に燃焼の残りを読む式（`Counter(StatusKeys.Burn)`）がある札。</summary>
    internal static HashSet<TraitId> BurnReaderTraits()
    {
        string path = Path.Combine("BattleCore", "Traits.cs");
        string src = File.ReadAllText(path);
        var res = new HashSet<TraitId>();
        var types = typeof(Trait).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Trait)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null);
        var reader = new Regex(@"Counter\(\s*StatusKeys\.Burn\s*\)");
        foreach (var t in types)
        {
            var m = Regex.Match(src, @"class\s+" + t.Name + @"\b[^{]*\{");
            if (!m.Success) continue;
            int start = m.Index;
            var next = Regex.Match(src.Substring(m.Index + m.Length), @"\n(public|internal)\s+(sealed\s+|abstract\s+|static\s+|readonly\s+)*(class|record|struct|enum)\s");
            string body = next.Success ? src.Substring(start, m.Length + next.Index) : src.Substring(start);
            if (!reader.IsMatch(body)) continue;
            var inst = (Trait)Activator.CreateInstance(t)!;
            res.Add(inst.Id);
        }
        if (res.Count == 0) throw new InvalidOperationException("燃焼を読む札の走査が空（R034）。");
        return res;
    }

    /// <summary>
    /// 役割の観測: 相方を B4 の3枚＋ベニ（ベニ自身なら ヒヨ・ボルグ・ホタ ＋ 素の埋め草）と組み、5 席 × 波6 × 200/200 × seed 0..3 で回して、
    /// その駒が行為者になった出来事を数える（/戦 0.10 以上で役割に入れる）。
    /// </summary>
    internal static Dictionary<string, (bool[] Roles, double[] Rates)> ObserveRoles(IEnumerable<UnitDef> units)
    {
        var readers = BurnReaderTraits();
        var list = units.ToList();
        var res = new Dictionary<string, (bool[], double[])>();
        var plain = new UnitDef { Id = "filler", Name = "埋め草", MaxHp = 60, Attack = 8, Speed = 5, Traits = Array.Empty<TraitId>() };
        var partial = new (string Id, double[] R)[list.Count];
        Parallel.For(0, list.Count, ci =>
        {
            var cand = list[ci];
            var others = cand.Id == "beni" ? new[] { UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo, plain } : new[] { UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo, UnitCatalog.Beni };
            var rates = new double[7];   // 回復・守り・火・読み（反転）・状態異常・移動・破片（量）
            int n = 0;
            for (int seat = 0; seat < 5; seat++)
            {
                var arr = new UnitDef[5]; int k = 0;
                for (int s = 0; s < 5; s++) arr[s] = s == seat ? cand : others[k++];
                var f = Seat(arr);
                for (int w = 0; w < WaveNames.Length; w++)
                    for (int sd = 0; sd < 4; sd++)
                    {
                        var (r, p, e, _) = Fight(f, w, Scales[0].Sc, sd);
                        int me = p.First(u => u.Def.Id == cand.Id).InstanceId;
                        var team = p.Select(u => u.InstanceId).ToHashSet();
                        n++;
                        if (r.TallyByUnit.TryGetValue(cand.Id, out var tl)) rates[6] += tl.ArmorOut;   // 味方（自分を含む）に書いた破片の量
                        foreach (var ev in r.Events)
                        {
                            if (ev.Kind == BattleEventKind.Heal && ev.ActorId == me && ev.TargetId != me && ev.Amount > 0 && ev.TargetId is int ht && team.Contains(ht)) rates[0]++;
                            else if (ev.Kind == BattleEventKind.Intercept && ev.ActorId == me && ev.TargetId != me) rates[1]++;
                            else if (ev.Kind == BattleEventKind.Damage && ev.Relayed && ev.TargetId == me) rates[1]++;
                            else if (ev.Kind == BattleEventKind.StatusGain && ev.ActorId == me && ev.TargetId is int tg)
                            {
                                if (ev.Text == StatusKeys.Burn) rates[2]++;
                                else if (ev.Text == StatusKeys.Armor) { if (team.Contains(tg) && tg != me) rates[1]++; }
                                else if (!team.Contains(tg)) rates[4]++;
                            }
                            else if (ev.Kind == BattleEventKind.Status && ev.InverterId == me && ev.Text == "燃焼") rates[3]++;
                            else if (ev.Kind == BattleEventKind.Move && ev.ActorId == me) rates[5]++;
                        }
                    }
            }
            for (int i = 0; i < 7; i++) rates[i] /= n;
            partial[ci] = (cand.Id, rates);
        });
        foreach (var (id, rates) in partial)
        {
            var d = list.First(u => u.Id == id);
            var roles = new bool[RoleNames.Length];
            for (int i = 0; i < 6; i++) if (rates[i] >= 0.10) roles[i] = true;
            if (rates[6] >= 1.0) roles[1] = true;   // 破片を 1.0/戦 以上配る駒も守り
            if (d.Traits.Any(readers.Contains)) roles[3] = true;
            if (!roles.Take(6).Any(x => x)) roles[6] = true;
            res[id] = (roles, rates);
        }
        return res;
    }

    internal static string RoleText(bool[] roles) => string.Join("・", RoleNames.Where((_, i) => roles[i]));

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第233期 Phase 0（盤面は第232期の規定のまま）");
        Console.WriteLine();

        Console.WriteLine("## Q0-1 仕様の確認（`UnitCatalog` と規則の型から直に引く）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | HP | 攻 | 速 | 型 | 札 | 手番 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var d in Core.Append(UnitCatalog.Beni))
            Console.WriteLine($"| {d.Name} | {d.MaxHp} | {d.Attack} | {d.Speed} | {d.Pattern} | {string.Join(", ", d.Traits)} | "
                + (d.Actions is { Count: > 0 } a ? string.Join(" → ", a.Select(x => $"{x.Kind}「{x.Label}」")) : "攻撃") + " |");
        Console.WriteLine();
        Console.WriteLine($"- 燃焼: 1回 {BurnRules.Damage}・{BurnRules.Turns} ターン（`BurnRules`・再付与は残りターンを戻すだけ）");
        Console.WriteLine($"- 熾火: `PyreTrait.Multiplier` = {PyreTrait.Multiplier}（燃えている間 攻撃 ×{PyreTrait.Multiplier}・型は貫き）");
        Console.WriteLine($"- 贔屓: `FavorRule.Default` = {FavorRule.Default}（燃えている味方＝位置を問わない に +Gain ／ 隣の燃えていない味方に −Loss）");
        Console.WriteLine($"- 熾火の規則: `EmberRule.Default` = {EmberRule.Default}（Fireproof ＝ ホタは焼かれない ／ Brittle ＝ 燃えている**敵**の被ダメ +%）");
        Console.WriteLine($"- 敵の数値の倍率の既定: {EnemyScaleRule.Adopted}（この期の台は §5 の3つの倍率を直に渡す）");
        Console.WriteLine();

        Console.WriteLine("## Q0-3 台の駒");
        Console.WriteLine();
        var cands = Candidates; var cands4 = CandidatesB4;
        int pairs = cands.Count * (cands.Count - 1) / 2;
        Console.WriteLine($"- 相方の候補（B3）: {cands.Count} 枚（`UnitCatalog.All` {UnitCatalog.All.Count} 枚 − ボルグ・ホタ・ヒヨ・ガルド・ツギ。ベニを含む）→ 2枚の組 **{pairs}** 組 × 席 120 通り = {pairs * 120:N0} 編成");
        Console.WriteLine($"- 相方の候補（B4）: {cands4.Count} 枚（ベニを除く）→ {cands4.Count * 120:N0} 編成");
        Console.WriteLine();

        // 所要の見積もり
        var probe = new[] { UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo, UnitCatalog.Beni, cands4[0] };
        var t0 = sw.Elapsed;
        AllSeats(probe, 20);
        double per = (sw.Elapsed - t0).TotalSeconds / (120 * 20);
        Console.WriteLine($"- 所要の見積もり: 120 席 × 20 seed ＝ 2,400 戦 で {(sw.Elapsed - t0).TotalSeconds:F1} 秒（1戦 {per * 1e6:F0} μs・並列）"
            + $" → B3 を seed 20 で {pairs * 120 * 20 * per / 60:F1} 分 ／ seed 40 で {pairs * 120 * 40 * per / 60:F1} 分 ／ seed 200 で {pairs * 120 * 200 * per / 60:F1} 分");
        Console.WriteLine();

        Console.WriteLine("### 相方の役割（機械的に分けた・/戦 0.10 以上で役割に入れる・破片は量 1.0/戦 以上で守り・重なりあり）");
        Console.WriteLine();
        var readers = BurnReaderTraits();
        Console.WriteLine($"`Traits.cs` の走査で燃焼の残りを読む札: {string.Join(", ", readers.OrderBy(x => x.ToString()))}（全札のうち {readers.Count} 本）");
        Console.WriteLine();
        var roles = ObserveRoles(cands);
        Console.WriteLine("| 駒 | 役割 | 回復 | 守り（庇い・肩代わり） | 火を書く | 燃焼を反転 | 状態異常を書く | 動かす | 破片（量） |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var d in cands)
        {
            var (rl, rt) = roles[d.Id];
            Console.WriteLine($"| {d.Name} | {RoleText(rl)} | " + string.Join(" | ", rt.Select(x => F2(x))) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("| 役割 | 枚数 |");
        Console.WriteLine("|---|---|");
        for (int i = 0; i < RoleNames.Length; i++) Console.WriteLine($"| {RoleNames[i]} | {roles.Values.Count(x => x.Roles[i])} |");
        Console.WriteLine();

        Console.WriteLine("## Q0-3' 参考の台の現状（九 / 新兵・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席 | " + string.Join(" | ", Scales.Select(s => s.Name + " 全員生存 ／ 勝率")) + " |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (nm, f) in new[] { ("参考 燃焼", RefBurn), ("参考 雷", RefThunder), ("参考 移動", RefMove) })
            Console.WriteLine($"| {nm} | {SeatsNamed(f)} | " + string.Join(" | ", Scales.Select(s => { var a = Measure(f, MainWave, s.Sc); return $"{F1(a.Surv)} ／ {F1(a.Win)}"; })) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
