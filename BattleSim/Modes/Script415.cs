using BattleCore;
using static Common;
using I = Inv348Diag;
using K8 = Kari408Diag;

// =====================================================================================
// script415 —— 第415期【小さな期・表示専用】デバフ軸の演出に要る台本の出来事（カーズの印 ／ ウツの手数 ／ ワタの引き受け元）の自己検査。
// 指示書は design/PHASE415_SCRIPT_EVENTS_SPEC.md ／ 報告は design/PHASE415_SCRIPT_EVENTS.md。
//
//     dotnet run --project BattleSim -c Release 0 script415 check
//
// 台は「試遊・カーズ 呪い」と「試遊・呪い」・波は `kari408` の 4 波（ボス ／ 近衛 ／ 大隊 ／ 強 第4波）。
// =====================================================================================
static class Script415Diag
{
    const string KarzRow = "試遊・カーズ 呪い";
    const string CurseRow = "試遊・呪い";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode != "check") { Console.WriteLine("script415: モードは check。"); return; }
        Check();
    }

    static int _fail;
    static void Expect(string what, bool ok, string detail = "")
    {
        Console.WriteLine($"- [{(ok ? "OK" : "NG")}] {what}{(detail == "" ? "" : $"（{detail}）")}");
        if (!ok) _fail++;
    }

    static int HitsOf(UnitState u)
    {
        int hits = 1;
        foreach (Trait t in u.Traits) hits = t.ModifyHitCount(u, hits);
        return Math.Max(1, hits);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# 第415期 script415 check —— 台本の出来事 3 つの自己検査");
        Console.WriteLine();
        var karz = Presets.Versions.First(r => r.Name == KarzRow).F;
        var curse = Presets.Versions.First(r => r.Name == CurseRow).F;
        var boss = K8.Waves[0];

        // 1. 「試遊・カーズ 呪い」× ボス・seed 0
        var (r, p, _) = I.Raw(karz, boss, 0, verbose: true);
        var ev = r.Events;
        int marks = ev.Count(e => e.Kind == BattleEventKind.StatusGain && e.Text == BattleContext.KarzMarkText);
        long tally = r.TallyByUnit["kari"].K409Marks;
        Console.WriteLine($"「{KarzRow}」× {boss.Name}・seed 0: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T・出来事 {ev.Count} 件");
        Console.WriteLine();
        Expect("karz_mark の件数 ＝ カーズが印を付けた回数（`K409Marks`）", marks == tally && marks > 0, $"出来事 {marks} ／ 計数 {tally}");
        int karzId = p.First(u => u.Def.Id == "kari").InstanceId;
        bool skillAim = true;
        for (int i = 0; i < ev.Count; i++)
        {
            if (ev[i].Kind != BattleEventKind.StatusGain || ev[i].Text != BattleContext.KarzMarkText) continue;
            var sk = ev.Take(i).LastOrDefault(e => e.Kind == BattleEventKind.Skill && e.ActorId == karzId);
            if (sk is null || sk.Turn != ev[i].Turn || sk.TargetId != ev[i].TargetId || ev[i].ActorId != karzId) skillAim = false;
        }
        Expect("印の出来事ごとに、同じターンのカーズの `Skill`（呪いを刻む）の `TargetId` ＝ 印の敵・書き手 ＝ カーズ", skillAim);
        var utsu = p.First(u => u.Def.Id == "utsu");
        var hitsEv = ev.Where(e => e.Kind == BattleEventKind.StatusGain && e.Text == BattleContext.UtsuHitsText && e.TargetId == utsu.InstanceId).ToList();
        int endHits = HitsOf(utsu);
        Expect("utsu_hits ≥ 1 件・最初はターン 0（開戦）・量は 1〜5・続く2件は値が違う",
            hitsEv.Count >= 1 && hitsEv[0].Turn == 0 && hitsEv.All(e => e.Amount is >= 1 and <= PerverseTrait.MaxHits)
            && hitsEv.Zip(hitsEv.Skip(1)).All(z => z.First.Amount != z.Second.Amount),
            $"{hitsEv.Count} 件: {string.Join(" → ", hitsEv.Select(e => $"T{e.Turn}:{e.Amount}"))}");
        Expect("最後の utsu_hits の量 ＝ 決着時のウツの手数" + (utsu.IsAlive ? "" : "（ウツは倒れている——倒れた時点の手数）"),
            hitsEv.Count > 0 && hitsEv[^1].Amount == endHits, $"出来事 {hitsEv.LastOrDefault()?.Amount} ／ 決着時 {endHits}");
        Console.WriteLine();

        // 2. ワタの引き受け元（「試遊・呪い」× 4 波・seed 0..9）
        int takes = 0, noFrom = 0, sumBad = 0, wataBoards = 0;
        foreach (var w in K8.Waves)
            for (int s = 0; s < 10; s++)
            {
                var (x, xp, _) = I.Raw(curse, w, s, verbose: true);
                var tk = x.Events.Where(e => e.Kind == BattleEventKind.StatusGain && e.Text == BattleContext.WataTakeText).ToList();
                int wataId = xp.First(u => u.Def.Id == "wata").InstanceId;
                takes += tk.Count;
                noFrom += tk.Count(e => e.FromId is null || e.TargetId != wataId || e.FromId == wataId);
                if (tk.Sum(e => e.Amount) != x.TallyByUnit["wata"].Db351Picked) sumBad++;
                wataBoards++;
            }
        Expect($"「{CurseRow}」× 4 波 × seed 0..9: ワタの引き受け（wata_take）すべてに `FromId`（受け手 ＝ ワタ・元の受け手 ≠ ワタ）", takes > 0 && noFrom == 0, $"{takes} 件・欠け {noFrom}");
        Expect("引き受けの量の和 ＝ ワタが拾った量（`Db351Picked`）", sumBad == 0, $"{wataBoards} 戦のうち違い {sumBad}");
        int other = 0;
        foreach (var e in I.Raw(curse, boss, 0, verbose: true).R.Events)
            if (e.FromId is not null && !(e.Kind == BattleEventKind.StatusGain && e.Text == BattleContext.WataTakeText)) other++;
        Expect("`FromId` は wata_take 以外の出来事に入らない", other == 0, $"{other} 件");
        Console.WriteLine();

        // 3. verbose の有無で勝敗 ／ T ／ 生存が変わらない
        int diff = 0, n = 0;
        foreach (var f in new[] { karz, curse })
            foreach (var w in K8.Waves)
                for (int s = 0; s < 20; s++)
                {
                    var a = I.Raw(f, w, s, verbose: true).R; var b = I.Raw(f, w, s, verbose: false).R;
                    n++;
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns || a.PlayerSurvivors != b.PlayerSurvivors) diff++;
                }
        Expect("verbose の有無で勝敗 ／ T ／ 生存が変わらない（2 台 × 4 波 × seed 0..19）", diff == 0, $"{n} 戦のうち違い {diff}");
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて OK**" : $"**NG {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
