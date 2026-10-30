using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;

// =====================================================================================
// lili420 —— 第420期 段0: リリ「受け取られなかった駆け寄りは差し引かない」（規定の札の挙動の直し・旧は `LiliH419`）。
// 指示書は design/PHASE420_SEKKI_DIGEST_SPEC.md §0 ／ 報告は design/PHASE420_SEKKI_DIGEST.md §1。
//
//     dotnet run --project BattleSim -c Release 0 lili420 settle   # 規定（直した）× 旧（LiliH419）× 試遊・リリ 呪い ／ 紅 ＋ elite の分かち×逆しま × 4 波・seed 0..99（ほかは 0..199）
//     dotnet run --project BattleSim -c Release 0 lili420 check    # 自己検査
// =====================================================================================
static class Lili420Diag
{
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static double Win(L.Fx a) => a.N == 0 ? double.NaN : 100.0 * a.Wins / a.N;
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");
    static Formation Row(string name) => Presets.Versions.First(r => r.Name == name).F;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "settle": Settle(); break;
            case "check": Check(); break;
            default: Console.WriteLine("lili420: モードは settle / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Settle()
    {
        Console.WriteLine("# 第420期 段0 —— 規定のリリ（受け取られた駆け寄りだけ差し引く）と旧（`LiliH419`・受け取られなくても引く）");
        Console.WriteLine();
        Console.WriteLine("差し引かなかった ＝ 注ぎが受け取られず、差し引きを飛ばした駆け寄り（回 ／戦・一撃の量 ／戦）。痛み ／手番 ＝ 手番の頭の痛み。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 旧 | 規定 | 差 | 差し引かなかった ／戦（回 ／ 量）| 差し引いた ／戦 旧 → 規定 | 痛み ／手番 旧 → 規定 | 手番の回復 ／戦 旧 → 規定 |");
        Console.WriteLine("|---|---|---|---|--:|---|---|---|---|");
        var boards = new (string Name, Func<Formation> F, int Seeds)[]
        {
            ("試遊・リリ 呪い", () => Row("試遊・リリ 呪い"), 100),
            ("試遊・リリ 紅", () => Row("試遊・リリ 紅"), 100),
            ("分かち×逆しま (ドハ×ウツ)", () => CompareBuilds().First(r => r.Name == "分かち×逆しま (ドハ×ウツ)").F, 200),
        };
        foreach (var (name, mk, n) in boards)
            foreach (var w in L.Hard)
            {
                var o = L.Measure(L.Swap(mk(), UnitCatalog.LiliH419), w, n);
                var a = L.Measure(mk(), w, n);
                Console.WriteLine($"| {name} | {w.Name} | {o.WT} | {a.WT} | {Delta(Win(a) - Win(o))} | {Per(a.Rush.DeductSkipped, a.N)} ／ {Per1(a.Rush.DeductSkippedAmt, a.N)} | "
                    + $"{Per1(o.Rush.PainCut, o.N)} → {Per1(a.Rush.PainCut, a.N)} | {Per1(o.KissPain, o.KissActs)} → {Per1(a.KissPain, a.KissActs)} | {Per1(o.HealTurn, o.N)} → {Per1(a.HealTurn, a.N)} |");
            }
    }

    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第420期 lili420 check");
        Console.WriteLine();
        var lili = UnitCatalog.Lili; var old = UnitCatalog.LiliH419;
        Req(lili.Traits.SequenceEqual(UnitCatalog.LiliH417.Traits.Concat(new[] { TraitId.KissRush, TraitId.KissSpillRushOnly, TraitId.KissRushDeduct, TraitId.KissRushNoStigma })), "規定のリリの札は第418期のまま（札は増やさない）");
        Req(old.Traits.SequenceEqual(lili.Traits.Select(t => t == TraitId.KissRushDeduct ? TraitId.KissRushDeductAny : t)) && old.PlusText == lili.PlusText && old.MinusText == lili.MinusText, "旧 `LiliH419` ＝ 規定の差し引きの札だけを `KissRushDeductAny` に替えた写し（文面は同じ）");
        Req(!UnitCatalog.All.Contains(old) && !UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.KissRushDeductAny)), "旧は `All` の外・`KissRushDeductAny` の保持者は `All` に 0 枚");
        Req(ReferenceEquals(PinL420(lili), old) && ReferenceEquals(PinL420(UnitCatalog.Mudo), UnitCatalog.Mudo), "`PinL420` は規定のリリだけを旧へ");
        // 差し引き後の重なりが 0（規定・旧とも）・規定は受け取られなかった駆け寄りを引かない
        var sum = new L.Fx(); var sumOld = new L.Fx();
        foreach (var w in new[] { I.Boss, I.Guard })
        {
            var f = CompareBuilds().First(r => r.Name == "分かち×逆しま (ドハ×ウツ)").F;
            sum.Add(L.Measure(f, w, 40)); sumOld.Add(L.Measure(L.Swap(f, old), w, 40));
        }
        Req(sum.Rush.PainOverlap == 0 && sumOld.Rush.PainOverlap == 0, "差し引き後の重なりは規定 ／ 旧とも 0");
        Req(sum.Rush.DeductSkipped > 0 && sumOld.Rush.DeductSkipped == 0, $"規定は受け取られなかった駆け寄りを差し引かない（{sum.Rush.DeductSkipped} 回）・旧は数えない（0）");
        bool same = true;
        foreach (var w in new[] { I.Guard, I.Boss })
            for (int s = 0; s < 6; s++)
            {
                var f = Row("試遊・リリ 呪い");
                var (r1, _, _) = I.Raw(f, w, s, verbose: false);
                var (r2, _, _) = I.Raw(f, w, s, verbose: true);
                if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
            }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（試遊・リリ 呪い × 近衛 ／ ボス × 6 seed）");
        string src = File.ReadAllText("BattleCore/Traits.cs");
        int i0 = src.IndexOf("public sealed class KissRushTrait", StringComparison.Ordinal);
        int i1 = src.IndexOf("public sealed class KissSpillTrait", StringComparison.Ordinal);
        Req(i0 > 0 && i1 > i0 && !src[i0..i1].Contains("PickOne") && !src[i0..i1].Contains("Roll("), "`KissRushTrait` は `PickOne` ／ `Roll` を使わない");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
