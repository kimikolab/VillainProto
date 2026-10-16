using BattleCore;
using static Common;
using I = Inv348Diag;

// burn349 kindle —— 守るほど燃え上がる（`KindleGuard`・半減で切った量で火勢 +1）がどれだけ効いていたか（指示書 §4 の注）。
static partial class Burn349Diag
{
    static void Kindle()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        I.W[] ws = new[] { I.Boss, I.Guard, I.Bat }.Concat(I.StrongWaves).ToArray();
        var cols = new (string Name, Formation F)[]
        {
            ("規定", Pon),
            ("規定 −守るほど燃え上がる", S316Swap(Pon, "borg", I.Without(UnitCatalog.Borg, TraitId.KindleGuard))),
            ("BN-f", Apply(Pon, V("f"))),
            ("BN-g", Apply(Pon, V("g"))),
        };
        Console.WriteLine($"# {Tag} 守るほど燃え上がる（KindleGuard）の効き —— {PonRow} × 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("発火 ＝ 「守るほどに燃え上がる」（半減で切った量が 30 に届くごとに火勢 +1）の回数 ／戦（台本のログ）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | " + string.Join(" | ", cols.Select(c => $"{c.Name}: 勝率・T・倒れた ／ 発火")) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "---|")));
        foreach (var w in ws)
            Console.WriteLine($"| {w.Name} | " + string.Join(" | ", cols.Select(c =>
            {
                var parts = new (I.St S, int K)[Seeds];
                Parallel.For(0, Seeds, i => { var (r, p, _) = I.Raw(c.F, w, i, verbose: true); parts[i] = (I.Take(r, p, w), r.Log.Count(l => l.ToString().Contains("守るほどに燃え上がる"))); });
                var s = new I.St(); long k = 0;
                foreach (var (x, n) in parts) { s.Add(x); k += n; }
                return $"{F1(s.Win)}・{s.WinTs}T・{Per2(s.AllyFell, s.N)} ／ {Per2(k, s.N)}";
            })) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
    static Formation S316Swap(Formation f, string id, UnitDef to) => Shiga316Diag.SwapId(f, id, to);
}
