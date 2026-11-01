using BattleCore;
using static Common;
using I = Inv348Diag;
using S430 = Som430Diag;

// =====================================================================================
// som434 —— 第434期【規定化】ソムの光の衣に雷の返し ＝ 第433期の RBb-gx を規定に（旧は `UnitCatalog.SomH433`）。
// 指示書は design/PHASE434_SOM_VEIL_SETTLE_SPEC.md ／ 報告は design/PHASE434_SOM_VEIL_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 som434 verify   # §1-8: 第430〜433期の 7 台 × 5 波・seed 0..199 で、規定のソムが第433期の RBb-gx と全戦一致（勝敗 ／ T ／ ソムの計数）・試遊の台も
//     dotnet run --project BattleSim -c Release 0 som434 check    # 自己検査（規定の札と文面・旧 ＝ 第433期までの規定・版の土台・試遊の台・保持者）
// =====================================================================================
static class Som434Diag
{
    const int Seeds = 200;
    const string PlayRow = "試遊・光の衣 ソム";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "verify": Verify(); break;
            case "check": Check(); break;
            default: Console.WriteLine("som434: モードは verify / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static string Sig(BattleResult r)
    {
        var t = r.TallyByUnit.GetValueOrDefault("som") ?? new UnitTally();
        return $"{r.PlayerWon}/{r.Turns}/{t.Vs431Breaks}/{t.Vs431BreakMarked}/{t.Vs431BreakAlready}/{t.Vs431Popped}/{t.Vs433StopOnceAll}/{t.Vs432StopHero}/{t.SparkRainLights}/{t.SparkVeilAdded}";
    }

    static void Verify()
    {
        var boards = S430.Boards();
        Console.WriteLine($"# 第434期 段0 の確かめ（規定のソム ＝ 第433期の RBb-gx・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定のソム 勝率・T | RBb-gx 勝率・T | 全戦一致 |");
        Console.WriteLine("|---|---|---|---|---|");
        bool all = true;
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var w in S430.Waves)
            {
                var a = new string[Seeds]; var g = new string[Seeds];
                var wa = new (bool W, int T)[Seeds]; var wg = new (bool W, int T)[Seeds];
                Parallel.For(0, Seeds, s =>
                {
                    var (r0, _, _) = I.Raw(S430.Swap(b.F, UnitCatalog.Som), w, s);
                    var (r1, _, _) = I.Raw(S430.Swap(b.F, UnitCatalog.SomRBbgx), w, s);
                    a[s] = Sig(r0); g[s] = Sig(r1); wa[s] = (r0.PlayerWon, r0.Turns); wg[s] = (r1.PlayerWon, r1.Turns);
                });
                bool same = a.SequenceEqual(g); all &= same;
                Console.WriteLine($"| {bi + 1} | {w.Name} | {WinT(wa)} | {WinT(wg)} | {(same ? "○" : "×")} |");
            }
        Console.WriteLine();
        // 試遊の台（＝ 台 1 の並び × 規定のソム）
        var play = Presets.Versions.First(r => r.Name == PlayRow).F;
        bool playSame = true;
        foreach (var w in S430.Waves)
            for (int s = 0; s < Seeds; s++)
            {
                var (r0, _, _) = I.Raw(play, w, s);
                var (r1, _, _) = I.Raw(S430.Swap(boards[0].F, UnitCatalog.SomRBbgx), w, s);
                if (Sig(r0) != Sig(r1)) playSame = false;
            }
        all &= playSame;
        Console.WriteLine($"- 試遊の台「{PlayRow}」× 5 波 × seed 0..{Seeds - 1} が 台 1 × RBb-gx と全戦一致: {(playSame ? "○" : "×")}");
        Console.WriteLine(all ? "som434 verify: すべて ○" : "som434 verify: × あり");
        if (!all) Environment.ExitCode = 1;
    }

    static string WinT((bool W, int T)[] x)
    {
        int n = x.Length, w = x.Count(v => v.W);
        return w == 0 ? $"{100.0 * w / n:F1}" : $"{100.0 * w / n:F1}・{x.Where(v => v.W).Average(v => v.T):F1}T";
    }

    static void Check()
    {
        bool ok = true;
        void Req(bool c, string what) { Console.WriteLine($"{(c ? "○" : "×")} {what}"); ok &= c; }
        var som = UnitCatalog.Som; var old = UnitCatalog.SomH433;
        Req(som.Traits.SequenceEqual(old.Traits.Concat(new[] { TraitId.VeilShockBreak, TraitId.VeilShockOnceAll, TraitId.VeilShockSpareHero })), "規定 ＝ 旧（SomH433）＋ VeilShockBreak ＋ VeilShockOnceAll ＋ VeilShockSpareHero");
        Req(som.Traits.SequenceEqual(UnitCatalog.SomRBbgx.Traits) && som.PlusText == UnitCatalog.SomRBbgx.PlusText && som.MinusText == UnitCatalog.SomRBbgx.MinusText, "規定の札と文面は第433期の RBb-gx と同じ");
        Req(som.MaxHp == old.MaxHp && som.Attack == old.Attack && som.Speed == old.Speed && som.Pattern == old.Pattern && som.Id == old.Id && som.Name == old.Name, "HP ／ 攻 ／ 速 ／ パターン ／ Id ／ 名前は変えていない");
        Req(old.Traits.SequenceEqual(UnitCatalog.SomH332.Traits.Append(TraitId.SparkVeilCap)) && !old.Traits.Contains(TraitId.VeilShockBreak), "旧（SomH433）＝ 第333〜433期の規定（SomH332 ＋ SparkVeilCap）");
        Req(UnitCatalog.All.Contains(som) && !UnitCatalog.All.Contains(old) && !UnitCatalog.Retired.Contains(old), "`All` は規定のソム・旧は `All` ／ `Retired` の外");
        Req(UnitCatalog.SomRBbgx.Traits.Take(old.Traits.Count).SequenceEqual(old.Traits) && UnitCatalog.SomRB1.Traits.SequenceEqual(old.Traits.Append(TraitId.VeilShock)), "第431〜433期の版の土台は旧（SomH433）");
        var news = new[] { TraitId.VeilShockBreak, TraitId.VeilShockOnceAll, TraitId.VeilShockSpareHero };
        Req(UnitCatalog.Everyone.Where(d => d.Traits.Any(news.Contains)).SequenceEqual(new[] { som }), "札 3 枚の保持者は `All` ／ `Retired` の中で規定のソムだけ");
        Req(Presets.Versions.Count(r => r.Name == PlayRow) == 1 && !Presets.Versions.Any(r => r.Name == "版・光の衣 ソム") && !VersionsH407().Any(r => r.Name == PlayRow), $"試遊の台「{PlayRow}」は Versions に 1 つ（旧名は無い）・鎖の終端 VersionsH407 が外す");
        var play = Presets.Versions.First(r => r.Name == PlayRow).F;
        var b1 = S430.Boards()[0].F;
        Req(Enumerable.Range(0, 5).All(i => ReferenceEquals(play[i], b1[i])), $"「{PlayRow}」＝ 台 1（打ち返しの行）の並びそのまま（規定のソム）");
        var t = Pin434(b1);
        Req(ReferenceEquals(t[2], old) && ReferenceEquals(PinS433(UnitCatalog.Shiga), UnitCatalog.Shiga), "`Pin434` は規定のソムだけを旧へ（ほかの駒はそのまま）");
        Console.WriteLine(ok ? "som434 check: すべて ○" : "som434 check: × あり");
        if (!ok) Environment.ExitCode = 1;
    }
}
