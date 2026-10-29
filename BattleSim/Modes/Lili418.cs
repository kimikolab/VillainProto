using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// lili418 —— 第418期 段0: リリ LK-1m-s の規定化 ／ 本体【軽い期・版1つ】上限2（LK-2m-s）。
// 指示書は design/PHASE418_LILI_SETTLE_SPEC.md ／ 報告は design/PHASE418_LILI_SETTLE.md。1戦の計数は lili416 の `Fx` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 lili418 settle   # 段0 の確かめ: 「試遊・リリ 呪い」「試遊・リリ 紅」× 4 波・seed 0..99 が第417期の LK-1m-s（`LiliLk1ms`）と同じ
//     dotnet run --project BattleSim -c Release 0 lili418 check    # 自己検査
// =====================================================================================
static class Lili418Diag
{
    const int Seeds = 100;
    static readonly string[] PlayRows = { "試遊・リリ 呪い", "試遊・リリ 紅" };

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static double Win(L.Fx a) => a.N == 0 ? double.NaN : 100.0 * a.Wins / a.N;
    static Formation Row(string name) => Presets.Versions.First(r => r.Name == name).F;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "settle": Settle(); break;
            case "check": Check(); break;
            default: Console.WriteLine("lili418: モードは settle / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 段0 の確かめ
    // ---------------------------------------------------------------------------------
    static void Settle()
    {
        Console.WriteLine($"# 第418期 段0 の確かめ —— 試遊の台（規定のリリ）× {string.Join(" ／ ", L.Hard.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("規定 ＝ 段0 の規定のリリ。LK-1m-s ＝ 第417期の版 `LiliLk1ms`（同じ札の別の定義）。勝率・倒しT・決着T の和・駆け寄りの回数が一致すれば同じ。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | LK-1m-s | 一致 |");
        Console.WriteLine("|---|---|---|---|:-:|");
        foreach (var name in PlayRows)
            foreach (var w in L.Hard)
            {
                var a = L.Measure(Row(name), w, Seeds);
                var b = L.Measure(L.Swap(Row(name), UnitCatalog.LiliLk1ms), w, Seeds);
                bool same = a.Wins == b.Wins && a.WinT == b.WinT && a.Turns == b.Turns && a.Rush.Fired == b.Rush.Fired;
                Console.WriteLine($"| {name} | {w.Name} | {a.WT} | {b.WT} | {(same ? "○" : "**×**")} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第418期 lili418 check");
        Console.WriteLine();
        var lili = UnitCatalog.Lili; var old = UnitCatalog.LiliH417;
        Req(lili.Traits.SequenceEqual(old.Traits.Concat(new[] { TraitId.KissRush, TraitId.KissSpillRushOnly, TraitId.KissRushDeduct, TraitId.KissRushNoStigma })), "規定のリリ ＝ 旧の8枚 ＋ KissRush ＋ KissSpillRushOnly ＋ KissRushDeduct ＋ KissRushNoStigma");
        Req(lili.Traits.SequenceEqual(UnitCatalog.LiliLk1ms.Traits), "規定のリリの札 ＝ 第417期の LK-1m-s（`LiliLk1ms`）と同じ並び");
        Req(lili.MaxHp == 78 && lili.Attack == 3 && lili.Speed == 6 && lili.Id == "lili" && lili.Actions.Count == 1 && lili.Actions[0].Kind == ActionKind.Skill, "HP 78 ／ 攻 3 ／ 速 6 ／ Id ／ `[Skill]` は旧のまま");
        Req(lili.Flavor == old.Flavor && lili.PlusText.StartsWith(old.PlusText) && lili.PlusText.EndsWith("（1ターン1回）。駆け寄った敵に聖痕は残らない"), "文面: プラスは旧 ＋ 駆け寄りの節・フレーバーは旧のまま");
        Req(lili.MinusText == "駆け寄って吸った敵の状態（毒・火・呪い・痺れなど）と攻撃力の上げ下げは、殴られた仲間が背負う", "文面: マイナスは最終案");
        Req(UnitCatalog.All.Contains(lili) && !UnitCatalog.All.Contains(old) && UnitCatalog.All.Count(d => d.Id == "lili") == 1, "`All` のリリは規定（旧は入らない）");
        Req(UnitCatalog.LiliLk1.Traits.Take(8).SequenceEqual(old.Traits) && UnitCatalog.LiliLk1m.Traits.Take(8).SequenceEqual(old.Traits), "第416〜417期の版は旧から作ったまま");
        Req(ReferenceEquals(PinL418(lili), old) && ReferenceEquals(PinL418(UnitCatalog.Mudo), UnitCatalog.Mudo), "`PinL418` は規定のリリだけを旧へ");
        Req(PlayRows.All(n => Presets.Versions.Any(r => r.Name == n)), "試遊の台に「試遊・リリ 呪い」「試遊・リリ 紅」がある");
        Req(!Presets.Versions.Any(r => r.Name.StartsWith("版・LK-")), "「版・LK-…」の台は無い");
        Req(!VersionsH407().Any(r => VersionRows418.Contains(r.Name)), "鎖の終端（`VersionsH407`）が第418期の台を外す");
        var c = Row("試遊・リリ 呪い");
        Req(c[0]?.Id == "utsu" && c[1]?.Id == "mudo" && c[2]?.Id == "nel" && c[3]?.Id == "wata" && ReferenceEquals(c[4], lili), "「試遊・リリ 呪い」＝ 前1 ウツ ／ 前3 ムド ／ 中央 ネル ／ 後1 ワタ ／ 後3 リリ（規定）");
        var g = Row("試遊・リリ 紅");
        Req(g[0]?.Id == "borg" && g[1]?.Id == "sid" && g[2]?.Id == "beni" && ReferenceEquals(g[3], lili) && g[4]?.Id == "guza", "「試遊・リリ 紅」＝「試遊・毒 紅」のラウ（後1）をリリ（規定）に");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
