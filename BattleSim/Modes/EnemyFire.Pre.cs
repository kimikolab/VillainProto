using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// enemyfire pre —— 前段（指示書 §2）: 煽り・ギフトの相手選びの比べ方を P-（③ を外す）と P′（③′ 倍率の前 × 次の手番の段）で比べる。
// 土台は第244期 S2R（大技・火の雨は乱数・ギフトは G3）。第245期 追記 B（`GiftQuiet`）は P- ／ P′ の両方に入れる。**駒は第244期の規定（`UnitCatalog.*R3`）から組む**（規定化の後も同じ表が出る）。
static partial class EnemyFireDiag
{
    internal static readonly UnitDef PreBorg = With(UnitCatalog.BorgR3, UnitCatalog.BorgR3.Traits.Concat(new[] { TraitId.FireSpreadCap, TraitId.FireUnleash }));
    internal static readonly UnitDef PreHota = With(UnitCatalog.HotaR3, UnitCatalog.HotaR3.Traits.Concat(new[] { TraitId.PyreBurnout, TraitId.PyreEmbers, TraitId.CallFire }));
    internal static readonly UnitDef PreHiyoS2R = With(UnitCatalog.HiyoR3, UnitCatalog.HiyoR3.Traits.Append(TraitId.StokeBaseAtk));
    // 第245期 追記 B: 前段の2択（P- ／ P′）はどちらも「ギフトの手番の燃え広がりではヒヨが育たない」（`GiftQuiet`）を含む。
    internal static readonly UnitDef PreHiyoMinus = With(UnitCatalog.HiyoR3, UnitCatalog.HiyoR3.Traits.Append(TraitId.GiftQuiet));
    internal static readonly UnitDef PreHiyoPrime = With(UnitCatalog.HiyoR3, UnitCatalog.HiyoR3.Traits.Concat(new[] { TraitId.StokeStageAtk, TraitId.GiftQuiet }));
    internal static readonly UnitDef PreHiyoPrimeNoB = With(UnitCatalog.HiyoR3, UnitCatalog.HiyoR3.Traits.Append(TraitId.StokeStageAtk));

    internal sealed record PreVer(string Name, string What, UnitDef Hiyo);
    internal static readonly PreVer[] PreVersions =
    {
        new("P-", "③ を外す（倍率の後の攻撃力・第242期と同じ）", PreHiyoMinus),
        new("P′", "③′ 倍率の前 × 次の手番の段の倍率", PreHiyoPrime),
        new("S2R", "参考: 第244期 S2R（③ 倍率の前・B なし）", PreHiyoS2R),
        new("P′−B", "参考: P′ から B を外す（B の効き）", PreHiyoPrimeNoB),
    };
    internal static readonly (string Name, Func<Formation> F)[] PreSeats = { ("T3-238", () => T3238), ("T3-244", () => T3244) };

    static partial void PreImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new Dictionary<(string B, string V, int W, int S), FB.LAgg>();
        var hcells = new Dictionary<(string B, string V, int W, int S), HAgg>();
        foreach (var (bn, bf) in PreSeats)
            foreach (var v in PreVersions)
            {
                var f = Apply(bf(), PreBorg, PreHota, v.Hiyo);
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < BA.Scales.Length; s++)
                        (cells[(bn, v.Name, w, s)], hcells[(bn, v.Name, w, s)]) = MeasureH(f, w, BA.Scales[s].Sc);
            }
        Console.WriteLine("# 第245期 前段 —— 煽りの比べ方（P- ／ P′）");
        Console.WriteLine();
        Console.WriteLine("土台は第244期 S2R（ボルグ ＋ 上限・放つ ／ ホタ ＋ 焼き尽くす・残り火・呼び火 ／ ヒヨ G3）。seed 0..199・verbose。");
        Console.WriteLine();
        foreach (var (bn, bf) in PreSeats) Console.WriteLine($"- {bn}: {BA.SeatsNamed(bf())}");
        Console.WriteLine();
        Console.WriteLine("## 線（指示書 §2.2）: 本編 第2〜5波 × 400/300 の勝率の平均（2席の平均）→ 九/新兵 × 400/300 の全員生存。差 1.0pt 未満なら P′");
        Console.WriteLine();
        Console.WriteLine("| 版 | 本編 400/300 勝率 T3-238 ／ T3-244 ／ **2席の平均** | 九/新兵 400/300 全員生存 T3-238 ／ T3-244 ／ **平均** |");
        Console.WriteLine("|---|---|---|");
        var main = new Dictionary<string, double>(); var sub = new Dictionary<string, double>();
        foreach (var v in PreVersions)
        {
            double W(string b) => Enumerable.Range(0, 4).Average(w => 100.0 * cells[(b, v.Name, w, 1)].Wins / cells[(b, v.Name, w, 1)].N);
            double S(string b) { var a = cells[(b, v.Name, BA.MainWave, 1)]; return 100.0 * a.AllSurv / a.N; }
            double wa = W("T3-238"), wb = W("T3-244"), sa = S("T3-238"), sb = S("T3-244");
            main[v.Name] = (wa + wb) / 2; sub[v.Name] = (sa + sb) / 2;
            Console.WriteLine($"| {v.Name} | {wa:F1} ／ {wb:F1} ／ **{main[v.Name]:F2}** | {sa:F1} ／ {sb:F1} ／ **{sub[v.Name]:F2}** |");
        }
        Console.WriteLine();
        double d = main["P′"] - main["P-"];
        // 指示書 §2.2: 主判定の差が 1.0pt 未満なら P′（全員生存の差は記録する）。1.0pt 以上なら主判定の高い方。
        string pick = Math.Abs(d) < 1.0 ? "P′（主判定の差 1.0pt 未満）" : d > 0 ? "P′" : "P-";
        Console.WriteLine($"主判定の差 P′ − P- ＝ **{d:+0.00;-0.00}pt** ／ 全員生存の差 {sub["P′"] - sub["P-"]:+0.00;-0.00}pt → **選ぶ版: {pick}**");
        Console.WriteLine();

        Console.WriteLine("## 表G —— 煽り・ギフトの相手と成績");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 全員生存 ／ 勝率 ／ 決着T | 煽りの相手 ボルグ ／ ホタ ／ 相方 % | 煽り/戦 | ギフトの相手 ボルグ ／ ホタ ／ 相方 % | 焼き尽くす ／ 放つ /戦 | ホタ ／ ボルグ の与ダメ/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|---|---|---|");
        foreach (var (bn, _) in PreSeats)
            foreach (var v in PreVersions)
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 2; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var a = cells[(bn, v.Name, w, s)];
                        long sr = a.StokeRole.Sum(), rr = a.RecipRole.Sum();
                        string ho = a.OutSum.TryGetValue("hota", out var h) ? Per(h[0], a.N) : "—";
                        string bo = a.OutSum.TryGetValue("borg", out var b) ? Per(b[0], a.N) : "—";
                        Console.WriteLine($"| {bn} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Pct(a.AllSurv, a.N)} ／ {Pct(a.Wins, a.N)} ／ {Per(a.Turns, a.N)} | {Pct(a.StokeRole[0], sr)} ／ {Pct(a.StokeRole[1], sr)} ／ {Pct(a.StokeRole[3], sr)} | {Per(a.Stokes, a.N)} | {Pct(a.RecipRole[0], rr)} ／ {Pct(a.RecipRole[1], rr)} ／ {Pct(a.RecipRole[3], rr)} | {Per(a.Burnout, a.N)} ／ {Per(a.Unleash, a.N)} | {ho} ／ {bo} |");
                    }
        Console.WriteLine();
        Console.WriteLine("## 表G′ —— ヒヨの火勢（追記 B）");
        Console.WriteLine();
        Console.WriteLine("周回の頭（刻みの後）のヒヨの火勢の平均と火勢4 の割合、ギフトを撃った次の周回の頭のヒヨの火勢の分布、ギフトの間隔（2回以上撃った戦のうち、続けて撃たなかった周回がある戦の割合・間隔の平均）、B で育たなかったヒヨの育ち（のべ/戦）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 平均 T1 ／ T2 ／ T3 ／ T4 ／ T5 | 火勢4 % T1 ／ T2 ／ T3 ／ T4 ／ T5 | ギフトの次の周回 1 ／ 2 ／ 3 ／ 4 % | ギフト/戦 | 2回以上撃った戦 ／ うち間が空いた % | 間隔の平均 | B で育たなかった/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|---|--:|--:|");
        foreach (var (bn, _) in PreSeats)
            foreach (var v in PreVersions)
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 2; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var h = hcells[(bn, v.Name, w, s)];
                        string avg = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Per(h.LvSum[t], h.LvCnt[t])));
                        string p4 = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Pct(h.Lv4[t], h.LvCnt[t])));
                        long nx = h.NextLv.Sum();
                        string nl = string.Join(" ／ ", Enumerable.Range(1, 4).Select(l => Pct(h.NextLv[l], nx)));
                        Console.WriteLine($"| {bn} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {avg} | {p4} | {nl} | {Per(h.Gifts, h.N)} | {Pct(h.Multi, h.N)} ／ {Pct(h.Gapped, h.Multi)} | {Per(h.GapSum, h.GapN)} | {Per(h.Quiet, h.N)} |");
                    }
        Console.WriteLine();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    /// <summary>ヒヨの火勢の集計（追記 B）。</summary>
    internal sealed class HAgg
    {
        public long N, Gifts, Multi, Gapped, GapSum, GapN, Quiet;
        public readonly long[] LvSum = new long[8], LvCnt = new long[8], Lv4 = new long[8], NextLv = new long[5];
        public void Merge(HAgg o)
        {
            N += o.N; Gifts += o.Gifts; Multi += o.Multi; Gapped += o.Gapped; GapSum += o.GapSum; GapN += o.GapN; Quiet += o.Quiet;
            for (int t = 0; t < 8; t++) { LvSum[t] += o.LvSum[t]; LvCnt[t] += o.LvCnt[t]; Lv4[t] += o.Lv4[t]; }
            for (int l = 0; l < 5; l++) NextLv[l] += o.NextLv[l];
        }
        public void Take(BattleResult r, List<UnitState> p)
        {
            N++;
            int? hiyo = p.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId;
            if (hiyo is not int hy || r.FireLevels is not FireLevelLedger fl) return;
            Quiet += fl.GiftQuietSkipped;
            var lvAt = new Dictionary<int, int>();
            foreach (var sn in fl.Snaps)
                if (sn.Id == hy) { int t = Math.Min(7, sn.Turn); LvSum[t] += sn.Level; LvCnt[t]++; if (sn.Level >= 4) Lv4[t]++; lvAt[sn.Turn] = sn.Level; }
            var giftTurns = r.Events.Where(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Gift && x.ActorId == hy && x.Slot == 1).Select(x => x.Turn).ToList();
            Gifts += giftTurns.Count;
            foreach (int t in giftTurns) if (lvAt.TryGetValue(t + 1, out int l)) NextLv[Math.Clamp(l, 0, 4)]++;
            if (giftTurns.Count >= 2)
            {
                Multi++;
                bool gap = false;
                for (int i = 1; i < giftTurns.Count; i++) { int g = giftTurns[i] - giftTurns[i - 1]; GapSum += g; GapN++; if (g >= 2) gap = true; }
                if (gap) Gapped++;
            }
        }
    }

    internal static (FB.LAgg L, HAgg H) MeasureH(Formation f, int w, EnemyScaleRule sc, int seeds = BA.Seeds)
    {
        var ls = new FB.LAgg[seeds]; var hs = new HAgg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = BA.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
            var r = BattleEngine.Run(p, e, i, verbose: true);
            var a = new FB.LAgg(); a.Take(r, p, e, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value)); ls[i] = a;
            var b = new HAgg(); b.Take(r, p); hs[i] = b;
        });
        var la = new FB.LAgg(); var ha = new HAgg();
        for (int i = 0; i < seeds; i++) { la.Merge(ls[i]); ha.Merge(hs[i]); }
        return (la, ha);
    }
}
