using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// enemyfire pre —— 前段（指示書 §2）: 煽り・ギフトの相手選びの比べ方を P-（③ を外す）と P′（③′ 倍率の前 × 次の手番の段）で比べる。
// 土台は第244期 S2R（大技・火の雨は乱数・ギフトは G3）。**駒は第244期の規定（`UnitCatalog.*R3`）から組む**（規定化の後も同じ表が出る）。
static partial class EnemyFireDiag
{
    internal static readonly UnitDef PreBorg = With(UnitCatalog.BorgR3, UnitCatalog.BorgR3.Traits.Concat(new[] { TraitId.FireSpreadCap, TraitId.FireUnleash }));
    internal static readonly UnitDef PreHota = With(UnitCatalog.HotaR3, UnitCatalog.HotaR3.Traits.Concat(new[] { TraitId.PyreBurnout, TraitId.PyreEmbers, TraitId.CallFire }));
    internal static readonly UnitDef PreHiyoS2R = With(UnitCatalog.HiyoR3, UnitCatalog.HiyoR3.Traits.Append(TraitId.StokeBaseAtk));
    internal static readonly UnitDef PreHiyoMinus = UnitCatalog.HiyoR3;
    internal static readonly UnitDef PreHiyoPrime = With(UnitCatalog.HiyoR3, UnitCatalog.HiyoR3.Traits.Append(TraitId.StokeStageAtk));

    internal sealed record PreVer(string Name, string What, UnitDef Hiyo);
    internal static readonly PreVer[] PreVersions =
    {
        new("P-", "③ を外す（倍率の後の攻撃力・第242期と同じ）", PreHiyoMinus),
        new("P′", "③′ 倍率の前 × 次の手番の段の倍率", PreHiyoPrime),
        new("S2R", "参考: 第244期 S2R（③ 倍率の前）", PreHiyoS2R),
    };
    internal static readonly (string Name, Func<Formation> F)[] PreSeats = { ("T3-238", () => T3238), ("T3-244", () => T3244) };

    static partial void PreImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new Dictionary<(string B, string V, int W, int S), FB.LAgg>();
        foreach (var (bn, bf) in PreSeats)
            foreach (var v in PreVersions)
            {
                var f = Apply(bf(), PreBorg, PreHota, v.Hiyo);
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < BA.Scales.Length; s++)
                        cells[(bn, v.Name, w, s)] = FB.Measure(f, w, BA.Scales[s].Sc);
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
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }
}
