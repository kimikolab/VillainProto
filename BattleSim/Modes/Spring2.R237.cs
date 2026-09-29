using BattleCore;
using static Common;

// spring2 run237 / check237 —— 第237期「着地の反動（②）」。S2 ・①（同じ列・第236期の S3）・②（着地の反動）・①＋② を第232期と同じ台で並べる。**線は置かない。**
static partial class Spring2Diag
{
    internal static readonly UnitDef HaneL = Plus(UnitCatalog.HaneS0, TraitId.SpringGuard, TraitId.SpringStay, TraitId.Landing);
    internal static readonly UnitDef HaneRL = Plus(UnitCatalog.HaneS0, TraitId.SpringGuard, TraitId.SpringStay, TraitId.SpringRow, TraitId.Landing);
    static Ver[] V237 => _v237 ??= new[] { new Ver("S2", HaneS2), new Ver("①", HaneS3), new Ver("②", HaneL), new Ver("①＋②", HaneRL) };
    static Ver[]? _v237;

    static partial void Run237()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第237期 着地の反動（台 M-ハネ（228 H3）・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"席: {SeatsNamed(MHane228)}");
        Console.WriteLine();
        foreach (int s in new[] { 0, 1 })
        {
            Console.WriteLine($"## {Scales[s].Name}");
            Console.WriteLine();
            Console.WriteLine("全員生存 ／ 勝率。下の行は /戦: 弾＝弾き返し（自分＋隣）・移＝味方の移動（味方が動かされた件数）・着＝着地の反動（入れ替え ／ 上限 ／ 候補なし）・退＝シオの退避");
            Console.WriteLine();
            Console.WriteLine("| 波 | " + string.Join(" | ", V237.Select(v => v.Tag)) + " |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", V237.Length)));
            var main = V237.Select(_ => new List<double>()).ToArray();
            for (int w = 0; w < WaveNames.Length; w++)
            {
                var cells = V237.Select(v => Measure(MHane228, v, w, Scales[s].Sc)).ToList();
                for (int k = 0; k < cells.Count; k++) if (w < 4) main[k].Add(cells[k].T.Surv);
                Console.WriteLine($"| {WaveNames[w]} | " + string.Join(" | ", cells.Select(a =>
                    $"**{F1(a.T.Surv)}** ／ {F1(a.T.Win)}<br>弾 {F2(a.Per(a.T.SpringSelf + a.T.SpringGuard))}・移 {F2(a.Per(a.AllyMoves))}・着 {F2(a.Per(a.LandSwaps))}/{F2(a.Per(a.LandCapped))}/{F2(a.Per(a.LandNoPair))}・退 {F2(a.Per(a.T.RetreatSwaps))}")) + " |");
            }
            Console.WriteLine("| 本編の平均（全員生存） | " + string.Join(" | ", main.Select(m => F1(m.Average()))) + " |");
            Console.WriteLine();
        }

        Console.WriteLine("## 九/新兵 × seed 0..999");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 版 | 全員生存 | 勝率 | 倒れた/戦 | 弾き返し/戦（うち隣） | 味方の移動/戦 | 着地の反動/戦 | シオの退避/戦 | " + string.Join(" | ", UnitNames) + " |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|" + string.Concat(Enumerable.Repeat("---|", Units.Length)));
        foreach (int s in new[] { 1, 0 })
            foreach (var v in V237)
            {
                var a = Measure(MHane228, v, TuneDiag.MainWave, Scales[s].Sc, 0, 1000); var t = a.T;
                Console.WriteLine($"| {Scales[s].Name} | {v.Tag} | {F1(t.Surv)} | {F1(t.Win)} | {F2(a.Per(t.Fell.Values.Sum()))} | {F2(a.Per(t.SpringSelf + t.SpringGuard))}（{F2(a.Per(t.SpringGuard))}） | "
                    + $"{F2(a.Per(a.AllyMoves))} | {F2(a.Per(a.LandSwaps))} | {F2(a.Per(t.RetreatSwaps))} | "
                    + string.Join(" | ", Units.Select(id => F1(100.0 * t.Fell.GetValueOrDefault(id) / t.N) + "%")) + " |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    static partial void Check237()
    {
        ok = ng = 0;
        Console.WriteLine("# 第237期 spring2 check237");
        Console.WriteLine();
        var en = Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("e2"), back1: Plain("e4"), back3: Plain("e5"));
        // 検査用のハネは着地の反動だけを持つ（本物は突き崩し・追い風が先に味方を動かすので、1回の入れ替えを切り分けられない）。上限は 1。
        var bare = new UnitDef { Id = "hane", Name = "hane", MaxHp = 56, Attack = 5, Speed = 1, Traits = new[] { TraitId.Landing } };
        // ハネ後3・中央 c・前3 z・前1 w・後1 y。ハネを後1 と入れ替える（ハネが動かされる）→ 移った先の隣の味方1体が、その隣の別の味方と入れ替わる。
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), front3: Plain("z"), center: Plain("c"), back1: Plain("y"), back3: bare), en, out var p, out var e);
            var before = p.ToDictionary(u => u.Def.Id, u => u.Slot);
            before["y"] = before["hane"];   // ハネと入れ替わった y は後3 へ
            ctx.SwapSlots(U(p, "hane"), U(p, "y").Slot, U(e, "e1"));
            var t = Tal(ctx, U(p, "hane"));
            int movedOthers = p.Count(u => u.Def.Id != "hane" && u.Slot != before[u.Def.Id]);
            Expect("ハネが動かされた——着地の反動の入れ替え ／ ハネの入れ替えの後で席が変わった味方", $"{t.LandingSwaps}/{movedOthers}", "1/2");
            Expect("ハネは後1 のまま（自分は動かない）", Seat(U(p, "hane")), "後1");
            ctx.SwapSlots(U(p, "hane"), 4, U(e, "e1"));
            int lim = SpringTrait.LimitOf(ctx, U(p, "hane"));
            Expect($"同じターンの2回目は上限（{lim} 回）で止まる（入れ替え ／ 上限）", $"{t.LandingSwaps}/{t.LandingCapped}", lim == 1 ? "1/1" : "2/0");
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), front3: Plain("z"), center: Plain("c"), back1: Plain("y"), back3: bare), en, out var p, out var e);
            foreach (var id in new[] { "w", "c" }) U(p, id).Hp = U(p, id).MaxHp / 2 - 1;
            ctx.SwapSlots(U(p, "hane"), U(p, "y").Slot, U(e, "e1"));
            var t = Tal(ctx, U(p, "hane"));
            Expect("移った先の隣の味方がすべて HP5割未満——入れ替えない（候補なし ／ 入れ替え）", $"{t.LandingNoPair}/{t.LandingSwaps}", "1/0");
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), back3: bare), en, out var p, out var e);
            ctx.SwapSlots(U(p, "hane"), 3, U(e, "e1"));   // 後3 → 後1（空席）
            Expect("隣が1体でその隣に別の味方がいない——候補なし", Tal(ctx, U(p, "hane")).LandingNoPair, 1L);
        }
        long battles = 0, diff = 0, s2land = 0, land = 0;
        var lk = new object();
        foreach (var v in V237)
            for (int w = 0; w < WaveNames.Length; w++)
                foreach (var (_, sc) in Scales)
                    Parallel.For(0, 20, s =>
                    {
                        var f = Apply(MHane228, v);
                        var (r, _, _, _) = Fight(f, w, sc, s, true);
                        var (r2, _, _, _) = Fight(f, w, sc, s, false);
                        bool same = r.PlayerWon == r2.PlayerWon && r.Turns == r2.Turns
                                    && string.Join(",", r.PlayerStarterFallen.OrderBy(x => x)) == string.Join(",", r2.PlayerStarterFallen.OrderBy(x => x));
                        long l = r.TallyByUnit.Where(kv => kv.Key == "hane").Sum(kv => kv.Value.LandingSwaps);
                        lock (lk) { battles++; if (!same) diff++; if (v.Tag is "S2" or "①") s2land += l; else land += l; }
                    });
        Expect($"verbose の有無で勝敗・決着T・倒れた駒が違う戦（{battles} 戦）", diff, 0L);
        Expect("札の無い版（S2・①）で着地の反動が起きた回数", s2land, 0L);
        Expect("札のある版（②・①＋②）で着地の反動が起きた", land > 0, true);
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**");
    }
}
