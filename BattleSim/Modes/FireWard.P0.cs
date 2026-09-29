using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// fireward phase0 —— Q0-1 半減の置き場所 ／ Q0-2 V の経路 ／ Q0-3 過去の実験 ／ Q0-4 台（予測の数値の材料）。
static partial class FireWardDiag
{
    /// <summary>Phase 0 と予測に使う仮の席（第235期の報告に出てくる形を手で写した。段1 の結果ではない）。</summary>
    internal static (string Name, string Enc)[] ProbeSeats =
    {
        ("T3 ササ＋ガレ（ボルグ中央）", "sasa,hota,borg,gare,hiyo"),
        ("T3′ ゴルム＋アカ（ボルグ前1）", "borg,hota,golm,susu,hiyo"),
        ("T3′ ヒサ＋ベニ（ボルグ前1）", "borg,hota,beni,hisa,hiyo"),
    };

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第238期 Phase 0");
        Console.WriteLine();
        string src = File.ReadAllText(Path.Combine("BattleCore", "BattleEngine.cs")).Replace("\r\n", "\n");
        int Line(int pos) => src.Take(pos).Count(c => c == '\n') + 1;

        // Q0-1
        Console.WriteLine("## Q0-1 半減の置き場所（`ApplyDamageBody` の本文の位置）");
        Console.WriteLine();
        int body = src.IndexOf("void ApplyDamageBody(", StringComparison.Ordinal);
        if (body < 0) throw new InvalidOperationException("ApplyDamageBody が見つからない（R034）");
        var marks = new (string Name, string Mark)[]
        {
            ("据えの層（第185期）", "        // 据えの層（第185期"),
            ("火の鎧の半減（第234期）", "        // 火の鎧（第234期"),
            ("**盾の配り（第238期・D）**", "        // 盾の配り（第238期"),
            ("巨躯（肩代わり）", "        // 巨躯:"),
            ("分かち（肩代わり）", "        // 分かち:"),
            ("破片", "        // 破片（アーマー）"),
            ("軛（上限）", "        // 軛（YokeTrait）"),
            ("身構え（上限・既定の位置）", "        // 身構え（BraceTrait"),
        };
        Console.WriteLine("| 段 | 行 |");
        Console.WriteLine("|---|---|");
        int prev = -1; bool ordered = true;
        foreach (var (nm, mk) in marks)
        {
            int i = src.IndexOf(mk, body, StringComparison.Ordinal);
            if (i < 0) throw new InvalidOperationException("目印が見つからない（R034）: " + mk);
            if (i < prev) ordered = false;
            prev = i;
            Console.WriteLine($"| {nm} | {Line(i)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"- 並びは上から{(ordered ? "この順" : "**この順ではない**")}。D は火の鎧の半減と同じ族（軽減の族の最後）の直後に置いた——**巨躯・分かちの中継より前**なので、D で半分にしてから肩代わりされる（中継の段は `relayed` なので D はもう掛からない）。破片・身構え・軛は D の後ろ");
        Console.WriteLine("- ササの身構え（既定 Z2）は破片の後ろの上限なので、D で半分になった一撃がさらに身構えで切られる（重ねる）。ボルグ自身は D の対象外（火の鎧の半減だけ）");
        Console.WriteLine("- D の条件: 相手陣営の出どころ・刻み／徴収／中継／呪いの共有ではない（＝敵の攻撃。回避・§1 と同じ「攻撃」の条件）・受け手が燃えている・保持者が生きていて受け手が保持者でない（D1 は隣）");
        Console.WriteLine();

        // Q0-2
        Console.WriteLine("## Q0-2 V の経路（燃焼ダメージの3つの入口）");
        Console.WriteLine();
        foreach (var (nm, head, inv, conv) in new[]
        {
            ("燃焼の刻み `BurnTickOnce`", "void BurnTickOnce(", "UnitState? inverterB = InvertsTick(u);", "FireConvertHolder(u) is UnitState hiyoB"),
            ("起爆 `DetonateOne`（味方の側）", "void DetonateOne(", "InvertsTick(u)) is UnitState inverterB", "FireConvertHolder(u) is UnitState hiyoD"),
            ("燃える巻き込み `FireSplashHit`", "public void FireSplashHit(", "InvertsTick(ally) is UnitState beni", "FireConvertHolder(ally) is UnitState hiyo"),
        })
        {
            int h = src.IndexOf(head, StringComparison.Ordinal);
            if (h < 0) throw new InvalidOperationException("目印が見つからない（R034）: " + head);
            int py = src.IndexOf("HasTrait(TraitId.Pyre)", h, StringComparison.Ordinal);
            int iv = src.IndexOf(inv, h, StringComparison.Ordinal);
            int cv = src.IndexOf(conv, h, StringComparison.Ordinal);
            if (py < 0 || iv < 0 || cv < 0) throw new InvalidOperationException("目印が見つからない（R034）: " + nm);
            Console.WriteLine($"- {nm}: ① 焼かれない／火の癒し（{Line(py)} 行）→ ② ベニの反転（{Line(iv)} 行）→ ③ 火の変換（{Line(cv)} 行）→ それ以外は今までどおり。並びが{(py < iv && iv < cv ? "§3 の優先順位どおり" : "**§3 と違う**")}");
        }
        Console.WriteLine("- ① ② は `return`（または `else if`）で抜けるので、V は ① ② が働いた一撃には何もしない（二重にならない）。優先順位の内訳は V の保持者の帳簿（計数のみ）に付ける");
        Console.WriteLine("- 燃える巻き込みの V は**受け手が燃えているときだけ**（巻き込みは火の粉より前に当たるので、1ターン目の最初の振りでは多くの隣がまだ燃えていない）。刻み・起爆は燃焼そのものなので条件なし");
        Console.WriteLine("- V の回復は `Heal(…, inverted: true, fireHeal: …)`＝ベニの反転の裏は通らず、渇きは素通り（a）／ `FireConvertDry` で封じられる（b）。支援拒否と上限は `Heal` のまま。V の回復の出どころ（`by`）はヒヨ");
        Console.WriteLine("- 毒の刻み・敵の攻撃・味方の刃（物理の巻き込み）は V の入口を通らない");
        Console.WriteLine();

        // Q0-3
        Console.WriteLine("## Q0-3 過去の実験（`design/*.md` の grep）");
        Console.WriteLine();
        var files = Directory.GetFiles("design", "*.md").Where(f => !f.Contains("PHASE236_FIRE_WARD") && !f.Contains("PHASE238_FIRE_WARD")).ToArray();
        foreach (var kw in new[] { "盾の配り", "火の変換", "火の衣", "燃えている味方の被ダメ", "燃えている味方は受けるダメージ", "TickHeal" })
        {
            var hit = files.Where(f => File.ReadAllText(f).Contains(kw)).Select(Path.GetFileName).ToList();
            Console.WriteLine($"- 「{kw}」: {(hit.Count == 0 ? "0 件" : $"{hit.Count} 件（{string.Join("・", hit.Take(6))}）")}");
        }
        Console.WriteLine("- **燃えている味方への被ダメ軽減を位置を問わず配る案・燃焼の味方側の反転を位置を問わず配る案は、どちらも測っていない**。");
        Console.WriteLine("  近いのは: ベニの反転（第190期・隣だけ・毒と燃焼の両方）／火の鎧（第234期・ボルグ自身だけ）／`EmberRule.TickHeal`（第178期・ホタだけ・既定 0 のまま）／");
        Console.WriteLine("  第233期の候補A「火の衣」（ヒヨが贔屓した味方の次の被弾を減らす）と候補C（巻き込みを燃えている味方には薬に）は**書いただけで実装していない**。第130期の熾火の配布は火を配る案（被ダメではない）");
        Console.WriteLine();

        // Q0-4
        Console.WriteLine("## Q0-4 台と予測の材料（A ＝ 第235期「全部」・仮の席 × 九/新兵 × seed 0..199）");
        Console.WriteLine();
        var cands = BA.Candidates;
        Console.WriteLine($"- T3: 相方の候補 {cands.Count} 枚 → {cands.Count * (cands.Count - 1) / 2} 組 × 席 120 × seed {BA.PickSeeds}（版 {Versions.Length}）。T3′（シオ・ササ抜き）は T3 の表から引く");
        Console.WriteLine();
        Console.WriteLine("| 仮の席 | 倍率 | 全員生存 ／ 勝率 | 燃える味方/T（ボルグ除く） | うちボルグの隣 | 隣の割合 | 燃える巻き込み 名目 ／ 普通に受けた/戦 | 燃焼の刻みで受けた（ボルグ以外）/戦 | 敵の攻撃で受けた（ボルグ以外）/戦 | 倒れた ボルグ／ホタ／ヒヨ／相方 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
        var vA = VerOf("A");
        foreach (var (nm, enc) in ProbeSeats)
            foreach (int s in new[] { 0, 1 })
            {
                var z = Measure(Dec(enc, vA), BA.MainWave, BA.Scales[s].Sc);
                var a = z.A;
                long burnTick = a.Taken.Where(kv => kv.Key != UnitCatalog.Borg.Name).Sum(kv => kv.Value[3]);
                long foeAtk = a.Taken.Where(kv => kv.Key != UnitCatalog.Borg.Name).Sum(kv => kv.Value[0]);
                Console.WriteLine($"| {nm} | {BA.Scales[s].Name} | {BA.F1(a.Surv)} ／ {BA.F1(a.Win)} | {BA.F2((double)z.BurnAllies / Math.Max(1, z.CensusTurns))} | {BA.F2((double)z.BurnAdj / Math.Max(1, z.CensusTurns))} | "
                    + $"{(z.BurnAllies == 0 ? "—" : BA.F1(100.0 * z.BurnAdj / z.BurnAllies) + "%")} | {BA.F1(a.Per(z.Y.SplashNominal))} ／ {BA.F1(a.Per(z.Y.SplashTaken))} | {BA.F1(a.Per(burnTick))} | {BA.F1(a.Per(foeAtk))} | "
                    + string.Join("／", ZAgg.Roles.Select(r => BA.F2(a.Per(z.FellBy.GetValueOrDefault(r))))) + " |");
            }
        Console.WriteLine();
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var probe = BA.AllSeats(CoreOf(VerOf("A+D1+V1")).Concat(new[] { UnitCatalog.Golm, UnitCatalog.Susu }).ToArray(), BA.PickSeeds);
        double us = sw2.Elapsed.TotalMilliseconds * 1000 / (120.0 * BA.PickSeeds);
        Console.WriteLine($"- 所要の見積もり（A+D1+V1・ゴルム＋アカの 120 席 × seed {BA.PickSeeds} で {sw2.Elapsed.TotalSeconds:F1} 秒）: T3 1 版 ≒ {us * 1081 * 120 * BA.PickSeeds / 1e6 / 60:F1} 分 ＋ 400/300 の同値の分（組ごとに 200/200 の最良と同値の席だけ）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
