using BattleCore;
using static Common;
using static Hush304Diag;

// =====================================================================================
// hush306 —— 第306期「粛 HCD15（騎士の怒りでひびが入り、15 回で砕ける）の規定化」。
// 指示書は design/PHASE306_HUSH_REGULATE_SPEC.md ／ 報告は design/PHASE306_HUSH_REGULATE.md。
// 規定の第二波（`Stages[1]`）＝ 巡礼騎士 `KnightGR` ×2 ・粛の伝令 `HusherHD15`。旧の第二波は `EnemyCatalog.Wave2H305`（過去の器具は `Common.StagesH305` で固定）。
//
//     dotnet run --project BattleSim -c Release 0 hush306 play [seeds=50]   # §4 試遊の標の行 ＋ 標経済 × 規定の第2波（勝率・決着T・第305期の HCD15 との一致）
//     dotnet run --project BattleSim -c Release 0 hush306 cmp [seeds=200]   # §3 `compare` 64 行の第2波: 旧 → 規定 と、第305期の HCD15 との一致・動いた行・(G2)
//     dotnet run --project BattleSim -c Release 0 hush306 find [seeds=50]   # §5 台本の例（ひび 15 → 砕けた → 割り込みが戻る ／ 騎士を殴って入るひび ／ 砕けた後の斬り返し）が揃った最初の seed
//     dotnet run --project BattleSim -c Release 0 hush306 memo <台の一部> <seed> [最後のT]   # 規定の第2波の台本の並び（Codex 向け）
//     dotnet run --project BattleSim -c Release 0 hush306 check             # 自己検査
// =====================================================================================
static class Hush306Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "play": Play(int.Parse(A(3, "50"))); return;
            case "cmp": Cmp(int.Parse(A(3, "200"))); return;
            case "find": Find(int.Parse(A(3, "50"))); return;
            case "memo": Memo(A(3, "ボス台"), int.Parse(A(4, "0")), int.Parse(A(5, "99"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("hush306: モードは play / find / memo / check。"); return;
        }
    }

    /// <summary>第305期の HCD15（旧の第二波から粛の伝令と騎士を差し替えた版）。規定の第二波と同じ戦になるはず。</summary>
    static readonly V VHCD15 = new("HCD15", "HC ＋ HD15（第305期の版）", EnemyCatalog.HusherHD15, EnemyCatalog.KnightGR);
    static Func<List<UnitState>> Regulated => () => BattleEngine.Materialize(EnemyCatalog.Stages[1].Enemy, BattleContext.EnemyTeam);

    static (string Name, Formation F)[] PlayRows() =>
        Presets.Playtest.Where(r => r.Name.StartsWith("試遊・標", StringComparison.Ordinal)).Select(r => (r.Name, r.F))
            .Append(("標経済 (ヒサ×ザン×ミサ)", CompareBuilds().First(r => r.Name == "標経済 (ヒサ×ザン×ミサ)").F)).ToArray();

    // ---------------------------------------------------------------------------------
    // §4 試遊の確認
    // ---------------------------------------------------------------------------------
    static void Play(int seeds)
    {
        Console.WriteLine($"# 第306期 試遊の確認 —— 規定の第2波（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("試遊・標の行（`Presets.Playtest`）＋ 標経済。勝率（倒しT）／ 粛が砕けた戦（T）／ 粛が倒れた戦（T）。旧 ＝ 第305期までの第2波（`Wave2H305`）・HCD15 ＝ 第305期の版（`hush305`）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 旧 | 規定 | 砕けた | 粛が倒れた | 騎士の斬り返し（出た ／ 止まった） | 第305期の HCD15 | 一致 |");
        Console.WriteLine("|---|---|---|---|---|---|---|:-:|");
        var old = Wave2Of(VS);
        foreach (var (n, f) in PlayRows())
        {
            var o = Many(f, old, seeds); var a = Many(f, Regulated, seeds, verbose: true); var h = Many(f, Wave2Of(VHCD15), seeds);
            bool same = a.Wins == h.Wins && a.WinT == h.WinT && a.Turns == h.Turns;
            Console.WriteLine($"| {n} | {Cell(o)} | {Cell(a)} | {Pct(a.ShatterN, a.N)}（T{Avg(a.ShatterT, a.ShatterN)}） | {Pct(a.HeraldDeadN, a.N)}（T{Avg(a.HeraldDeadT, a.HeraldDeadN)}） | {a.P(a.KnightRip):F2} ／ {a.P(a.KnightHushed):F2} | {Cell(h)} | {(same ? "○" : "**×**")} |");
        }
    }


    // ---------------------------------------------------------------------------------
    // §3 `compare` の第2波（旧 → 規定）と第305期の HCD15 との一致
    // ---------------------------------------------------------------------------------
    static void Cmp(int seeds)
    {
        var rows = CompareBuilds();
        var reg = new V("REG", "規定", EnemyCatalog.HusherHD15, EnemyCatalog.KnightGR);
        double Rate(Formation f)
        {
            var wins = new bool[seeds];
            Parallel.For(0, seeds, s => wins[s] = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Regulated(), s, verbose: false).PlayerWon);
            return 100.0 * wins.Count(x => x) / seeds;
        }
        var o = rows.Select(r => Rate2(r.F, VS, seeds)).ToArray();
        var a = rows.Select(r => Rate(r.F)).ToArray();
        var h = rows.Select(r => Rate2(r.F, VHCD15, seeds)).ToArray();
        Console.WriteLine($"# 第306期 `compare` {rows.Length} 行 × 第2波（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"規定 ＝ 第305期の HCD15 の列と一致: **{Enumerable.Range(0, rows.Length).Count(i => a[i] == h[i])} / {rows.Length} 行**。旧 → 規定 で動いた行:");
        Console.WriteLine();
        Console.WriteLine("| 行 | 旧 | 規定 | 差 | 第305期の HCD15 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int i = 0; i < rows.Length; i++)
            if (a[i] != o[i]) Console.WriteLine($"| {rows[i].Name} | {o[i]:F1} | {a[i]:F1} | {a[i] - o[i]:+0.0;-0.0} | {h[i]:F1} |");
        Console.WriteLine();
        Console.WriteLine($"- 第2波の平均: 旧 {o.Average():F2} → 規定 {a.Average():F2}・20% 未満 {o.Count(x => x < 20)} → {a.Count(x => x < 20)}");
        var drop = Enumerable.Range(0, rows.Length).Where(i => a[i] - o[i] <= -10.0).ToList();
        Console.WriteLine($"- (G2): 第2波で −10.0pt 以上落ちた行 {drop.Count} 行" + (drop.Count == 0 ? "（分解の対象なし）" : "：" + string.Join("・", drop.Select(i => rows[i].Name))));
        foreach (int i in drop)
            foreach (var d in rows[i].F.Occupied().Select(x => x.Def).Distinct())
            {
                var others = Enumerable.Range(0, rows.Length).Where(j => j != i && rows[j].F.Occupied().Any(x => ReferenceEquals(x.Def, d))).ToArray();
                Console.WriteLine($"  - {rows[i].Name} の {Short(d)}: " + (others.Length == 0 ? "他の行 0" : $"{others.Average(j => (a[j] - o[j]) / 5.0):+0.00;-0.00;0.00}pt（{others.Length} 行）"));
            }
    }
    // ---------------------------------------------------------------------------------
    // §5 台本の例
    // ---------------------------------------------------------------------------------
    static void Find(int seeds)
    {
        Console.WriteLine($"# 第306期 台本の例 —— 規定の第2波（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("1 ＝ ひび 15 → 砕けた → 粛が生きている間に味方の割り込み（仇討ち ／ 叫び ／ 羽）が戻る ／ 2 ＝ 騎士の斬り返しが止められて入るひび（`HushState`「ひび」の `TargetId` が騎士）／ 3 ＝ 砕けた後の騎士の斬り返し（騎士が出どころの `Reaction` の `Damage`）。最初に揃った seed（T）と、50 戦のうち揃った戦の数。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 1 砕けて割り込みが戻る | 2 騎士を殴って入るひび | 3 砕けた後の斬り返し |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (n, f) in PlayRows())
        {
            int[] first = { -1, -1, -1 }, ft = new int[3], cnt = new int[3];
            for (int s = 0; s < seeds; s++)
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var e = Regulated();
                var r = BattleEngine.Run(p, e, s, verbose: true);
                var ev = r.Events.ToList();
                var players = p.Select(u => u.InstanceId).ToHashSet();
                var knights = e.Where(u => u.Def.Id == "knight_g").Select(u => u.InstanceId).ToHashSet();
                var h = e.First(u => u.Def.Traits.Contains(TraitId.Hush));
                int di = ev.FindIndex(x => x.Kind == BattleEventKind.Death && x.TargetId == h.InstanceId); if (di < 0) di = ev.Count;
                int si = ev.FindIndex(x => x.Kind == BattleEventKind.HushState && x.Text == HushStateLabels.Shatter);
                void Hit(int k, int turn) { cnt[k]++; if (first[k] < 0) { first[k] = s; ft[k] = turn; } }
                if (si >= 0 && si < di && ev.Skip(si).Take(di - si).Any(x => (x.Kind == BattleEventKind.Damage && x.Reaction && x.ActorId is int a && players.Contains(a)) || x.Kind == BattleEventKind.MarkRally || x.Kind == BattleEventKind.FeatherMark))
                    Hit(0, ev[si].Turn);
                var kc = ev.FirstOrDefault(x => x.Kind == BattleEventKind.HushState && x.Text == HushStateLabels.Crack && x.TargetId is int t && knights.Contains(t));
                if (kc is not null) Hit(1, kc.Turn);
                if (si >= 0)
                {
                    var kr = ev.Skip(si).FirstOrDefault(x => x.Kind == BattleEventKind.Damage && x.Reaction && x.ActorId is int a && knights.Contains(a));
                    if (kr is not null) Hit(2, kr.Turn);
                }
            }
            string C(int k) => first[k] < 0 ? "—" : $"seed {first[k]}（T{ft[k]}）・{cnt[k]} 戦";
            Console.WriteLine($"| {n} | {C(0)} | {C(1)} | {C(2)} |");
        }
    }

    static void Memo(string rowPart, int seed, int lastTurn)
    {
        var (name, f0) = PlayRows().First(b => b.Name.Contains(rowPart, StringComparison.Ordinal));
        var p = BattleEngine.Materialize(f0, BattleContext.PlayerTeam);
        var e = Regulated();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# hush306 memo —— {name} × 規定の第2波 × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Turn > lastTurn) break;
            if (x.Kind is not (BattleEventKind.Damage or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.MarkRally
                or BattleEventKind.Death or BattleEventKind.Attack or BattleEventKind.Cover or BattleEventKind.Sealed or BattleEventKind.HushState)) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static int Count(string path, string needle)
    {
        string s = File.ReadAllText(path);
        int c = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { c++; i += needle.Length; }
        return c;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# hush306 check —— 第306期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var reg = EnemyCatalog.Stages[1].Enemy; var old = EnemyCatalog.Wave2H305.Enemy;
        bool seats = reg.Occupied().Count() == old.Occupied().Count() && reg.Occupied().All(o =>
        {
            var d = old[o.Slot];
            return d is not null && d.Id == o.Def.Id && d.MaxHp == o.Def.MaxHp && d.Attack == o.Def.Attack && d.Speed == o.Def.Speed && d.Pattern == o.Def.Pattern;
        });
        Expect("(a) 規定の第2波 ＝ 巡礼騎士 `KnightGR` ×2 ・粛の伝令 `HusherHD15`・ほかは旧のまま（席・数値・Id・型が旧の第2波と同じ）・名前は旧のまま",
            reg.Occupied().Count(o => o.Def == EnemyCatalog.KnightGR) == 2 && reg.Occupied().Count(o => o.Def == EnemyCatalog.HusherHD15) == 1 && seats
            && !reg.Occupied().Any(o => o.Def == EnemyCatalog.KnightG || o.Def == EnemyCatalog.Husher) && EnemyCatalog.Stages[1].Name == EnemyCatalog.Wave2H305.Name);
        {
            int diff = 0, n = 0;
            foreach (var (_, f) in Boards()) for (int s = 0; s < 20; s++)
                {
                    var a = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Regulated(), s, verbose: true);
                    var b = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Wave2Of(VHCD15)(), s, verbose: true);
                    n++;
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns || a.Events.Count != b.Events.Count) diff++;
                }
            Expect("(b) 規定の第2波 ＝ 第305期の HCD15 の第2波（代表台 × seed 0..19 で勝敗・決着T・台本の出来事の数が同じ）", diff == 0, $"{n} 戦・違い {diff}");
        }
        {
            var dw = Doha297Diag.Waves().First(w => w.Key == "2").Make();
            Expect("(c) 旧の第2波は明示の定義（`Wave2H305`・`Stages` の外）で残り、過去の器具は旧で回る（`StagesH305[1]` ＝ 旧・`doha297` の波 ／ `hush304` の S は旧の粛の伝令と騎士）",
                !EnemyCatalog.Stages.Contains(EnemyCatalog.Wave2H305) && ReferenceEquals(StagesH305[1], EnemyCatalog.Wave2H305)
                && Enumerable.Range(0, EnemyCatalog.Stages.Count).All(i => i == 1 || ReferenceEquals(StagesH305[i], EnemyCatalog.Stages[i]))
                && dw.Any(u => u.Def.Id == "husher" && u.Def.Traits.SequenceEqual(EnemyCatalog.Husher.Traits)) && dw.Count(u => u.Def.Id == "knight_g" && u.Def.Traits.Count == 0) == 2   // 材料化は倍率の写しを差すので Id と札で見る
                && EnemyOf(VS).Occupied().Any(o => o.Def == EnemyCatalog.Husher) && ReferenceEquals(PinWave305(EnemyCatalog.Stages[1].Enemy), old)
                && ReferenceEquals(PinWave305(EnemyCatalog.Stages[2].Enemy), EnemyCatalog.Stages[2].Enemy));
        }
        Expect("(d) 第二波の先遣（作戦マップ 1-1 の `Vanguards[0]`）は規定の第2波から後1を抜いた写し（騎士 `KnightGR` ×2 ・`HusherHD15`）",
            EnemyCatalog.Vanguards[0].Enemy.Occupied().Count(o => o.Def == EnemyCatalog.KnightGR) == 2 && EnemyCatalog.Vanguards[0].Enemy.Occupied().Any(o => o.Def == EnemyCatalog.HusherHD15)
            && EnemyCatalog.Vanguards[0].Enemy[3] is null);
        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(e) `PickOne(` の出現数が第305期と同じ（BattleCore）", pick == 36, $"{pick}");
        {
            int diff = 0, n2 = 0;
            foreach (var (_, f) in PlayRows()) for (int s = 0; s < 20; s++)
                {
                    var a = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Regulated(), s, verbose: false);
                    var b = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Regulated(), s, verbose: true);
                    n2++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                }
            Expect("(f) verbose の有無で勝敗・決着T が同じ（試遊の行 × 規定の第2波 × seed 0..19）", diff == 0, $"{n2} 戦・違い {diff}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
