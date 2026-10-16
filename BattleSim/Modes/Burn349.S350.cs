using BattleCore;
using static Common;
using I = Inv348Diag;

// burn349 s350 —— 第350期の確かめ（指示書 §3）: BN-a を規定にした後の `compare` ／ ポンの台 ／ ボルグのいない燃焼の台を、第349期の BN-a（ボルグの札の門）と比べる。
static partial class Burn349Diag
{
    static void Settle350()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第350期 §3 確かめ —— 規定（BN-a・ボルグ ／ ホタ ／ ヒヨの札の門）と第349期の BN-a（ボルグの札の門）");
        Console.WriteLine();

        // ---- 1. compare ----
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var now = Grid(rows, f => f);
        var bna = Grid(rows, f => Apply(Pin349(f), V("a")));
        int same = 0, cells = 0; var diff = new List<string>();
        for (int r = 0; r < rows.Length; r++)
            for (int w = 0; w < nw; w++)
            {
                cells++;
                if (now[r, w] == bna[r, w]) same++;
                else diff.Add($"{rows[r].Name} 第{w + 1}波 {F1(bna[r, w])} → {F1(now[r, w])}（ボルグ {(rows[r].F.Occupied().Any(o => o.Def.Id == "borg") ? "あり" : "なし")}）");
            }
        Console.WriteLine($"## 1. `compare` {rows.Length} 行 × {nw} 波（seed 0..{CompareSeeds - 1}）: 規定 と 第349期の BN-a の列が一致したセル **{same} ／ {cells}**");
        Console.WriteLine();
        foreach (var d in diff) Console.WriteLine($"- {d}");
        Console.WriteLine();

        // ---- 2. ポンの台 ----
        I.W[] ws = new[] { I.Boss, I.Guard, I.Bat }.Concat(I.StrongWaves).ToArray();
        Console.WriteLine($"## 2. 試遊・燃焼 ポンの台 × 波・seed 0..{Seeds - 1}（規定 ／ 第349期の BN-a ／ 第349期の規定）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 規定（第350期） | 第349期の BN-a | 第349期の規定 |");
        Console.WriteLine("|---|---|---|---|");
        string WT(I.St s) => $"{F1(s.Win)}・{s.WinTs}T・倒 {Per2(s.AllyFell, s.N)}";
        foreach (var w in ws)
            Console.WriteLine($"| {w.Name} | {WT(I.Measure(PonNow, w))} | {WT(I.Measure(Apply(Pon, V("a")), w))} | {WT(I.Measure(Pon, w))} |");
        Console.WriteLine();

        // ---- 3. ボルグのいない燃焼の台 ----
        var noBorg = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions)
            .Where(r => r.F.Occupied().Any(o => o.Def.Id is "hota" or "hiyo") && !r.F.Occupied().Any(o => o.Def.Id == "borg")).ToArray();
        Console.WriteLine($"## 3. ボルグのいない燃焼の台（`compare` ／ 交差帯 ／ 試遊の台のうちホタかヒヨがいてボルグがいない {noBorg.Length} 台）× 同じ波（第349期の規定 → 規定）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 並び | " + string.Join(" | ", ws.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")));
        foreach (var (n, f) in noBorg)
            Console.WriteLine($"| {n} | {Seats(f)} | " + string.Join(" | ", ws.Select(w =>
            {
                var o = I.Measure(Pin349(f), w); var z = I.Measure(f, w);
                return o.Win == z.Win && o.WinT == z.WinT && o.AllyFell == z.AllyFell ? $"{F1(z.Win)}（同じ）" : $"{WT(o)} → **{WT(z)}**";
            })) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
