using System.Reflection;
using BattleCore;
using static Common;

// spring2 run / check —— 表A〜C（§5）と自己検査（受け入れ 2〜4）。
static partial class Spring2Diag
{
    static readonly string[] Units = TuneDiag.Units;
    static readonly string[] UnitNames = TuneDiag.UnitNames;

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第232期 表A〜C（台 M-ハネ（228 H3）・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"席: {SeatsNamed(MHane228)}");
        Console.WriteLine();
        var res = new Dictionary<(int V, int W, int S), Agg>();
        for (int v = 0; v < Versions.Length; v++)
            for (int w = 0; w < WaveNames.Length; w++)
                for (int s = 0; s < Scales.Length; s++)
                    res[(v, w, s)] = Measure(MHane228, Versions[v], w, Scales[s].Sc);

        Console.WriteLine("## 表A 全員生存 ／ 勝率");
        Console.WriteLine();
        for (int s = 0; s < Scales.Length; s++)
        {
            Console.WriteLine($"### {Scales[s].Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", WaveNames) + " | 本編の平均 | 九の平均 |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", WaveNames.Length + 2)));
            for (int v = 0; v < Versions.Length; v++)
            {
                var cells = Enumerable.Range(0, WaveNames.Length).Select(w => res[(v, w, s)].T).ToList();
                Console.WriteLine($"| {Versions[v].Tag} | " + string.Join(" | ", cells.Select(a => $"{F1(a.Surv)} ／ {F1(a.Win)}"))
                    + $" | {F1(cells.Take(4).Average(a => a.Surv))} | {F1(cells.Skip(4).Average(a => a.Surv))} |");
            }
            Console.WriteLine();
        }

        foreach (int s in new[] { 0, 1 })
            foreach (int w in new[] { TuneDiag.MainWave, 5, 3 })
            {
                Console.WriteLine($"### 表A' {WaveNames[w]} × {Scales[s].Name} —— 決着T・落ちた駒（割合 ／ 平均ターン）");
                Console.WriteLine();
                Console.WriteLine("| 版 | 決着T（勝ち） | " + string.Join(" | ", UnitNames) + " |");
                Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Units.Length)));
                for (int v = 0; v < Versions.Length; v++)
                {
                    var a = res[(v, w, s)].T;
                    Console.WriteLine($"| {Versions[v].Tag} | {F2(a.WinT)} | " + string.Join(" | ", Units.Select(id =>
                        a.Fell.GetValueOrDefault(id) == 0 ? "0.0%" : $"{F1(100.0 * a.Fell[id] / a.N)}% ／ T{F1((double)a.FellT[id] / a.Fell[id])}")) + " |");
                }
                Console.WriteLine();
            }

        Console.WriteLine("## 表B 弾き返し（/戦）: 自分 ／ 隣（判定に来た）・隣で弾けなかった（○席 ／ 最後尾 ／ 上限 ／ 粛 ／ 割り込み）・隣で入れ替わった ／ 入れ替わらなかった・混乱");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | 版 | 自分 | 隣（判定） | ○席 | 最後尾 | 上限 | 粛 | 割り込み | 隣の入れ替わり | 入れ替わらず | 混乱 | ハネが前列にいたターン |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        for (int s = 0; s < 2; s++)
            for (int w = 0; w < WaveNames.Length; w++)
                for (int v = 0; v < Versions.Length; v++)
                {
                    var a = res[(v, w, s)]; var t = a.T;
                    Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {Versions[v].Tag} | {F2(a.Per(t.SpringSelf))} | {F2(a.Per(t.SpringGuard))}（{F2(a.Per(a.GChances))}） | "
                        + $"{F2(a.Per(a.GOffLane))} | {F2(a.Per(a.GTail))} | {F2(a.Per(a.GCapped))} | {F2(a.Per(a.GHushed))} | {F2(a.Per(a.GHeld))} | {F2(a.Per(t.GuardSwaps))} | {F2(a.Per(a.GStay))} | "
                        + $"{F2(a.Per(t.SpringConfused))} | {(t.HaneAliveTurns == 0 ? "—" : F1(100.0 * t.HaneFrontTurns / t.HaneAliveTurns) + "%")} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 表C 緊急退避（/戦）: 退避 ／ 取り合い（隣の弾き返しの後: 同じ味方を下げた ／ 別の駒 ／ 5割未満なのに下げない）・倒れた駒の直前の HP（<40 ／ 40-50 ／ 50-60 ／ 60-80 ／ 80+）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | 版 | 退避 | 同じ味方 | 別の駒 | 下げない | 倒れた（直前の HP） |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        for (int s = 0; s < 2; s++)
            for (int w = 0; w < WaveNames.Length; w++)
                for (int v = 0; v < Versions.Length; v++)
                {
                    var a = res[(v, w, s)]; var t = a.T;
                    Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {Versions[v].Tag} | {F2(a.Per(t.RetreatSwaps))} | {F2(a.Per(a.GuardBoth))} | {F2(a.Per(a.GuardOther))} | {F2(a.Per(a.GuardSkipped))} | "
                        + $"{F2(a.Per(t.DeathPre.Sum()))}: " + string.Join("/", t.DeathPre.Select(x => F2(a.Per(x)))) + " |");
                }
        Console.WriteLine();

        // 追補: 九/新兵 × 400/300 と 200/200 を seed 0..999 で
        Console.WriteLine("## 追補 seed 0..999（九/新兵）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 版 | 全員生存 | 勝率 | 倒れた/戦 | 退避/戦 | 隣の弾き返し/戦 | 混乱/戦 | " + string.Join(" | ", UnitNames) + " |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|" + string.Concat(Enumerable.Repeat("---|", Units.Length)));
        foreach (int s in new[] { 1, 0 })
            foreach (var v in Versions)
            {
                var a = Measure(MHane228, v, TuneDiag.MainWave, Scales[s].Sc, 0, 1000); var t = a.T;
                Console.WriteLine($"| {Scales[s].Name} | {v.Tag} | {F1(t.Surv)} | {F1(t.Win)} | {F2(a.Per(t.Fell.Values.Sum()))} | {F2(a.Per(t.RetreatSwaps))} | {F2(a.Per(t.SpringGuard))} | {F2(a.Per(t.SpringConfused))} | "
                    + string.Join(" | ", Units.Select(id => F1(100.0 * t.Fell.GetValueOrDefault(id) / t.N) + "%")) + " |");
            }
        Console.WriteLine();

        // P1: compare のハネの行（版を当てる）と、ハネのいない行・雷の台
        Console.WriteLine("## P1 compare のハネの行（版を当てた勝率・第1〜5波 × seed 0..199・115/115 ＝ `compare` と同じ条件）");
        Console.WriteLine();
        var rows = CompareBuilds().ToList();
        int noHane = rows.Count(r => !r.F.Occupied().Any(o => o.Def.Id == "hane"));
        Console.WriteLine($"ハネのいない行 {noHane} / {rows.Count}（版はハネの札しか差し替えないので、この行は構造的に 0 セル）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Versions.Select(v => v.Tag)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", Versions.Length)));
        foreach (var r in rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "hane")))
            Console.WriteLine($"| {r.Name} | " + string.Join(" | ", Versions.Select(v =>
            {
                var g = Apply(r.F, v);
                return string.Join(" / ", Enumerable.Range(0, 5).Select(st =>
                    F1(100.0 * Enumerable.Range(0, 200).Count(sd => BattleEngine.Run(g, EnemyCatalog.Stages[st].Enemy, sd, verbose: false).PlayerWon) / 200)));
            })) + " |");
        Console.WriteLine();
        Console.WriteLine("参考 雷（ハネのいない台）: S0 と S2 の全員生存 ／ 勝率（九/新兵）: " + string.Join(" ・ ", Scales.Select(sc =>
        {
            var a = Measure(Thunder, VerOf("S0"), TuneDiag.MainWave, sc.Sc).T; var b = Measure(Thunder, VerOf("S2"), TuneDiag.MainWave, sc.Sc).T;
            return $"{sc.Name} {F1(a.Surv)}／{F1(a.Win)} → {F1(b.Surv)}／{F1(b.Win)}";
        })));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    // ---------------- 自己検査 ----------------
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static UnitTally Tal(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static int ok, ng;
    static void Expect(string what, object got, object want)
    {
        bool pass = Equals(got, want);
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, false);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id) => new() { Id = id, Name = id, MaxHp = 100, Attack = 5, Speed = 1, Traits = Array.Empty<TraitId>() };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static string Seat(UnitState u) => FormationRules.SeatNames[u.Slot];

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第232期 spring2 check");
        Console.WriteLine();
        var en = Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("e2"), back1: Plain("e4"), back3: Plain("e5"));
        foreach (var (tag, hane, want) in new[] { ("S2", HaneS2, "中央/後3/前3/1/0/1"), ("S1", HaneS1, "中央/前3/後3/1/1/0"), ("S0", UnitCatalog.HaneS0, "前1/後3/前3/0/0/0") })
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), front3: Plain("z"), back3: hane), en, out var p, out var e);
            ctx.ApplyDamage(U(p, "z"), 10, U(e, "e1"));
            var t = Tal(ctx, U(p, "hane"));
            Expect($"{tag}: 隣の z が殴られた——殴った敵 ／ ハネ ／ z の席・弾いた ／ 入れ替わった ／ 入れ替わらず", $"{Seat(U(e, "e1"))}/{Seat(U(p, "hane"))}/{Seat(U(p, "z"))}/{t.SpringGuardCount}/{t.SpringGuardSwaps}/{t.SpringGuardStays}", want);
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), front3: Plain("z"), back3: HaneS2), en, out var p, out var e);
            ctx.ApplyDamage(U(p, "z"), 10, U(e, "e1"));
            int limit = SpringTrait.LimitOf(ctx, U(p, "hane"));
            for (int i = 0; i < 3; i++) ctx.ApplyDamage(U(p, "hane"), 1, U(e, "e3"));
            var t = Tal(ctx, U(p, "hane"));
            Expect($"S2: 隣の分とハネ自身の分は同じ上限（1ターン {limit} 回）を分け合う（弾いた計 ／ 上限で止まった）", $"{t.SpringCount}/{t.SpringCapped}", $"{limit}/{4 - limit}");
            Expect("S2: ハネは隣の分で動かず、自分の分は上限で弾かないので後3 のまま", Seat(U(p, "hane")), "後3");
        }
        // 第236期（S3）: 同じ列（後1）の味方 z が殴られた——S3 は弾き、S2 は弾かない（後1 と後3 は隣接しない）。中央（隣接）は両方とも弾く。
        foreach (var (tag, hane, want) in new[] { ("S3", HaneS3, "1/後3"), ("S2", HaneS2, "0/後3") })
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), back1: Plain("z"), back3: hane), en, out var p, out var e);
            ctx.ApplyDamage(U(p, "z"), 10, U(e, "e1"));
            var t = Tal(ctx, U(p, "hane"));
            Expect($"{tag}: 同じ列（後1）の z が殴られた——弾いた ／ ハネの席（後1 と後3 は隣接しない: {FormationRules.AreAdjacent(3, 4)}）", $"{t.SpringGuardCount}/{Seat(U(p, "hane"))}", want);
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), front3: Plain("z"), back3: HaneS3), en, out var p, out var e);
            ctx.ApplyDamage(U(p, "w"), 10, U(e, "e1"));
            Expect("S3: 前1 の w（ハネは後3・別の列で隣接もしない）は弾かない", Tal(ctx, U(p, "hane")).SpringGuardChances, 0L);
        }
        // 実戦: verbose の有無・台本の数
        long battles = 0, diff = 0, guardEv = 0, guardTal = 0, stayMove = 0;
        var lk = new object();
        foreach (var v in Versions)
            for (int w = 0; w < WaveNames.Length; w++)
                foreach (var (_, sc) in Scales)
                    Parallel.For(0, 20, s =>
                    {
                        var f = Apply(MHane228, v);
                        var (r, p, e, _) = Fight(f, w, sc, s, true);
                        var (r2, _, _, _) = Fight(f, w, sc, s, false);
                        bool same = r.PlayerWon == r2.PlayerWon && r.Turns == r2.Turns
                                    && string.Join(",", r.PlayerStarterFallen.OrderBy(x => x)) == string.Join(",", r2.PlayerStarterFallen.OrderBy(x => x));
                        int hane = p.First(u => u.Def.Id == "hane").InstanceId;
                        long ge = 0, sm = 0;
                        var evs = r.Events;
                        for (int i = 0; i < evs.Count; i++)
                        {
                            if (evs[i].Kind != BattleEventKind.SpringGuard) continue;
                            ge++;
                            if (v.Tag is not ("S2" or "S3")) continue;
                            // S2: 見出しの後、次の Damage / Attack / TurnStart までにハネの Move が無い（敵の入れ替えは除く）
                            for (int j = i + 1; j < evs.Count; j++)
                            {
                                var x = evs[j];
                                if (x.Kind is BattleEventKind.Damage or BattleEventKind.Attack or BattleEventKind.TurnStart) break;
                                // ハネと殴られた味方の入れ替え（ハネが動かし、直後にその味方が動く）。追い風でハネが踏み込むのは別（Tailwind が前に立つ）。
                                if (x.Kind == BattleEventKind.Move && x.TargetId == hane && x.ActorId == hane && j + 1 < evs.Count
                                    && evs[j + 1].Kind == BattleEventKind.Move && evs[j + 1].TargetId == evs[i].TargetId) { sm++; break; }
                            }
                        }
                        long gt = r.TallyByUnit.Where(kv => kv.Key == "hane").Sum(kv => kv.Value.SpringGuardFires);
                        lock (lk) { battles++; if (!same) diff++; guardEv += ge; guardTal += gt; stayMove += sm; }
                    });
        Expect($"verbose の有無で勝敗・決着T・倒れた駒が違う戦（{battles} 戦）", diff, 0L);
        Expect("台本の SpringGuard の数 ＝ 帳簿", guardEv == guardTal && guardEv > 0, true);
        Expect("S2・S3: 隣の弾き返しの直後にハネが殴られた味方と入れ替わった回数", stayMove, 0L);
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
