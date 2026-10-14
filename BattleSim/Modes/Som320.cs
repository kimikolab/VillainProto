using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S309 = Som309Diag;
using S312 = Som312Diag;
using S319 = Som319Diag;

// =====================================================================================
// som320 —— 第320期「ソム WS-b の規定化 ＋ 試遊の台と手引き」（測定の期ではない）。
// 指示書は design/PHASE320_SOM_SETTLE_SPEC.md ／ 報告は design/PHASE320_SOM_SETTLE.md ／ 手引きは design/PLAYTEST_GUIDE_SHOCK.md。
//
//     dotnet run --project BattleSim -c Release 0 som320 cand        # §3: 瓦礫の台の候補 4 × 10 波（獣の弾けから拾った瓦礫 ／ 勝率）——どれを試遊に載せるかを決める
//     dotnet run --project BattleSim -c Release 0 som320 boards      # §3: 足した試遊の台 3 × 10 波・勝率と狙いの出来事（打ち返し ／ 追い打ち ／ 瓦礫）/戦（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 som320 guide       # §4: 既存の試遊の感電の台 × 10 波（手引きの数字の出どころ）
//     dotnet run --project BattleSim -c Release 0 som320 census      # §4: 試遊の感電の台 × 10 波で、画面に出る出来事の名前（`Text`）を数える（/戦）
//     dotnet run --project BattleSim -c Release 0 som320 memo <台の一部> <boss|guard|bat|nine|nine2|1..5> <seed>   # 狙いの出来事の台本（1戦）
//     dotnet run --project BattleSim -c Release 0 som320 check       # 自己検査（規定化・固定・粛で止まったときの暴発・試遊の台）
//
// 駒・札・数値・波は段0（規定のソム ＝ WS-b）のほかは1つも変えない（器具だけ）。台は `Presets.Playtest` を引くだけで写しを持たない（`cand` の候補を除く）。
// =====================================================================================
static class Som320Diag
{
    const int Seeds = S307.Seeds;
    internal const string Swing = "試遊・感電 打ち返し", Pursuit = "試遊・感電 追い打ち", Scrap = "試遊・感電 瓦礫";
    internal static readonly string[] Added = { Swing, Pursuit, Scrap };

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "boards";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "cand": Table("§3 瓦礫の台の候補", Candidates()); return;
            case "boards": Table("§3 足した試遊の台", Added.Select(n => (n, Playtest(n))).ToArray()); return;
            case "guide": Table("§4 既存の試遊の感電の台", PlaytestH321.Where(r => r.Name.StartsWith("試遊・感電", StringComparison.Ordinal) && !Added.Contains(r.Name)).ToArray()); return;
            case "census": Census(); return;
            case "memo": Memo(A(3, "打ち返し"), A(4, "boss"), int.Parse(A(5, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("som320: モードは cand / boards / guide / census / memo / check。"); return;
        }
    }

    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string Per1(long a, long n) => S307.Per1(a, n);
    static string Pct(long a, long n) => S307.Pct(a, n);
    static Formation Playtest(string n) => Pin321(Presets.Playtest.First(r => r.Name == n).F);   // 第322期: シガ ／ ヒサを第321期の規定に固定
    static readonly S287.Wave[] Waves = S319.Waves;
    static S287.Wave WaveOf(string n) => n switch { "nine" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };

    /// <summary>§3 瓦礫の台の候補（規定の駒）。</summary>
    static (string Name, Formation F)[] Candidates() => new (string, Formation)[]
    {
        ("光の盾 クビ→ツギ", S309.Swap(Playtest(S309.Shield), UnitCatalog.Kubi, UnitCatalog.Tsugi)),
        ("光の盾 重 クビ→ツギ", S309.Swap(Playtest(S309.ShieldHeavy), UnitCatalog.Kubi, UnitCatalog.Tsugi)),
        ("ツギの勝ち台 ゴルム→ソム", S309.Swap(B283.Seat(S307.Order(S307.WinBoard).Select(d => ReferenceEquals(d, UnitCatalog.ShigaSWa) ? UnitCatalog.ShigaH321 : d).ToArray()), UnitCatalog.Golm, UnitCatalog.Som)),
        ("感電 行", Pin321(Presets.Compare.First(r => r.Name == "感電 (シガ×カタ×ソム)").F)),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（台本）
    // ---------------------------------------------------------------------------------
    sealed class M
    {
        public long N, Wins, WinT, SomDied, Stood, Res, Swing, Backfire, Pursuit, PursuitBeast, Scrap, ScrapBeast, AllyPops, AllyStuns;
        public void Add(M o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; SomDied += o.SomDied; Stood += o.Stood; Res += o.Res; Swing += o.Swing; Backfire += o.Backfire;
            Pursuit += o.Pursuit; PursuitBeast += o.PursuitBeast; Scrap += o.Scrap; ScrapBeast += o.ScrapBeast; AllyPops += o.AllyPops; AllyStuns += o.AllyStuns;
        }
    }

    static M Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: true);
        var m = new M { N = 1 };
        if (r.PlayerWon) { m.Wins = 1; m.WinT = r.Turns; }
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), hagi = p.FirstOrDefault(u => u.Def.Id == "hagi"), tsugi = p.FirstOrDefault(u => u.Def.Id == "tsugi");
        if (som is not null && !som.IsAlive) m.SomDied = 1;
        var beasts = new HashSet<int>();
        bool lastBeast = false;   // 直前の「倒れた ／ 弾けた」が獣だったか（ハギの追い打ちの起こり）
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Summon when som is not null && ev.ActorId == som.InstanceId && ev.TargetId is int sid: beasts.Add(sid); break;
                case BattleEventKind.BeastThrow when ev.Text == BeastThrowLabels.Hit || ev.Text == BeastThrowLabels.Thread || ev.Text == BeastThrowLabels.Resonance || ev.Text == BeastThrowLabels.Hushed || ev.Text == BeastThrowLabels.Rod || ev.Text == BeastThrowLabels.Backswing:
                    m.Stood++;
                    if (ev.Text == BeastThrowLabels.Resonance) m.Res++;
                    if (ev.Text == BeastThrowLabels.Backswing) m.Swing++;
                    break;
                case BattleEventKind.BeastBurst:
                    lastBeast = true;
                    if (ev.Text == BeastBurstLabels.Backfire) m.Backfire++;
                    break;
                case BattleEventKind.Death: lastBeast = ev.TargetId is int dt && beasts.Contains(dt); break;
                case BattleEventKind.Highlight when hagi is not null && ev.ActorId == hagi.InstanceId:
                    m.Pursuit++; if (lastBeast) m.PursuitBeast++; break;
                case BattleEventKind.Plank when tsugi is not null && ev.ActorId == tsugi.InstanceId && ev.Text == PlankLabels.Scrap && ev.Slot == 1:
                    m.Scrap++; if (ev.TargetId is int st && beasts.Contains(st)) m.ScrapBeast++; break;
            }
        }
        foreach (var id in p.Select(u => u.Def.Id).Distinct())
            if (r.TallyByUnit.TryGetValue(id, out var ut)) { m.AllyPops += ut.ShockSpent; m.AllyStuns += ut.ShockStunned; }
        return m;
    }

    static M Measure(Formation f, S287.Wave w)
    {
        var parts = new M[Seeds];
        Parallel.For(0, Seeds, i => parts[i] = Fight(f, w, i));
        var all = new M();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // §3 ／ §4 の表
    // ---------------------------------------------------------------------------------
    static void Table(string title, (string Name, Formation F)[] boards)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new M[boards.Length, Waves.Length];
        for (int b = 0; b < boards.Length; b++) for (int w = 0; w < Waves.Length; w++) cells[b, w] = Measure(boards[b].F, Waves[w]);
        Console.WriteLine($"# 第320期 {title}（規定の駒 ＝ ソム WS-b ＋ シガ SG・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席（前1・前3・中央・後1・後3） |");
        Console.WriteLine("|---|---|");
        foreach (var (n, f) in boards) Console.WriteLine($"| {n} | {Seats(f)} |");
        Console.WriteLine();
        Console.WriteLine("## 勝率（括弧は勝った戦の決着T）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "--:|")));
        for (int b = 0; b < boards.Length; b++)
            Console.WriteLine($"| {boards[b].Name} | " + string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(w => $"{Pct(cells[b, w].Wins, cells[b, w].N)}（{Per1(cells[b, w].WinT, cells[b, w].Wins)}）")) + " |");
        Console.WriteLine();
        Console.WriteLine("## 狙いの出来事 /戦（打ち返し ・ 追い打ち（うち獣の弾けの後）・ 瓦礫（うち獣））");
        Console.WriteLine();
        Console.WriteLine("「打ち返し」＝ `BeastThrow 打ち返し`（シガ）／「追い打ち」＝ ハギの `Highlight`（倒れた隙に踏み込む）・直前に倒れた ／ 弾けたのが獣なら「獣の後」／「瓦礫」＝ ツギの `Plank 瓦礫` の倒れた分（1体 5）・拾った先が獣なら「獣」。");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int b = 0; b < boards.Length; b++)
            Console.WriteLine($"| {boards[b].Name} | " + string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(w =>
            {
                var c = cells[b, w];
                var parts = new List<string>();
                if (c.Swing > 0) parts.Add($"打 {Per1(c.Swing, c.N)}");
                if (c.Pursuit > 0) parts.Add($"追 {Per1(c.Pursuit, c.N)}（{Per1(c.PursuitBeast, c.N)}）");
                if (c.Scrap > 0) parts.Add($"瓦 {Per1(c.Scrap, c.N)}（{Per1(c.ScrapBeast, c.N)}）");
                return parts.Count == 0 ? "—" : string.Join(" ・ ", parts);
            })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 獣と味方 /戦（立った ・ 跳ね返り ・ 投げて暴発 ／ ソムが倒れた ／ 味方の感電 弾けた ・ 痺れた）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int b = 0; b < boards.Length; b++)
            Console.WriteLine($"| {boards[b].Name} | " + string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(w =>
            {
                var c = cells[b, w];
                return $"{Per1(c.Stood, c.N)} ・ {Per1(c.Res, c.N)} ・ {Per1(c.Backfire, c.N)} ／ {Pct(c.SomDied, c.N)} ／ {Per1(c.AllyPops, c.N)} ・ {Per1(c.AllyStuns, c.N)}";
            })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 10 波の和 /戦");
        Console.WriteLine();
        Console.WriteLine("| 台 | 打ち返し | 追い打ち（獣の後） | 瓦礫（獣） | 立った ・ 跳ね返り ・ 暴発 |");
        Console.WriteLine("|---|--:|---|---|---|");
        for (int b = 0; b < boards.Length; b++)
        {
            var s = new M(); for (int w = 0; w < Waves.Length; w++) s.Add(cells[b, w]);
            long n = Seeds;
            Console.WriteLine($"| {boards[b].Name} | {Per1(s.Swing, n)} | {Per1(s.Pursuit, n)}（{Per1(s.PursuitBeast, n)}） | {Per1(s.Scrap, n)}（{Per1(s.ScrapBeast, n)}） | {Per1(s.Stood, n)} ・ {Per1(s.Res, n)} ・ {Per1(s.Backfire, n)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4 出来事の名前の数（手引きの「画面で見える出来事」の出どころ）
    // ---------------------------------------------------------------------------------
    static void Census()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = PlaytestH321.Where(r => r.Name.StartsWith("試遊・感電", StringComparison.Ordinal)).ToArray();
        static string Key(BattleEvent ev) => ev.Kind switch
        {
            BattleEventKind.BeastThrow => $"BeastThrow {ev.Text}",
            BattleEventKind.BeastBurst => $"BeastBurst {ev.Text}",
            BattleEventKind.ShockGauge when ev.Text == ShockGaugeLabels.Unafraid => $"ShockGauge {ev.Text}",
            BattleEventKind.Skill when ev.Text == "仲間の背中を見た" => $"Skill {ev.Text}",
            BattleEventKind.ShareGive when ev.Text is ShareGiveLabels.GiftTurn or ShareGiveLabels.Pass => $"ShareGive {ev.Text}",
            _ => "",
        };
        Console.WriteLine($"# 第320期 §4 試遊の感電の台の出来事の名前 /戦（規定の駒・seed 0..{Seeds - 1}・ボス ／ 近衛 ／ 大隊 ／ 本編 第2波 ／ 第3波）");
        var waves = new[] { Waves[0], Waves[1], Waves[2], Waves[6], Waves[7] };
        foreach (var (name, f) in boards)
        {
            Console.WriteLine();
            Console.WriteLine($"## {name}（{Seats(f)}）");
            Console.WriteLine();
            var counts = waves.Select(w =>
            {
                var parts = new Dictionary<string, long>[Seeds];
                Parallel.For(0, Seeds, i =>
                {
                    var d = new Dictionary<string, long>();
                    var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), i, verbose: true);
                    foreach (var ev in r.Events) { string k = Key(ev); if (k.Length > 0) d[k] = d.GetValueOrDefault(k) + 1; }
                    parts[i] = d;
                });
                var all = new Dictionary<string, long>();
                foreach (var d in parts) foreach (var (k, v) in d) all[k] = all.GetValueOrDefault(k) + v;
                return all;
            }).ToArray();
            var keys = counts.SelectMany(c => c.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (keys.Count == 0) { Console.WriteLine("（出ない）"); continue; }
            Console.WriteLine("| 出来事 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "--:|")));
            foreach (var k in keys) Console.WriteLine($"| `{k}` | " + string.Join(" | ", counts.Select(c => Per1(c.GetValueOrDefault(k), Seeds))) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 台本（1戦）
    // ---------------------------------------------------------------------------------
    static void Memo(string board, string wave, int seed)
    {
        var (name, f) = PlaytestH321.First(r => r.Name.Contains(board, StringComparison.Ordinal));
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), hagi = p.FirstOrDefault(u => u.Def.Id == "hagi"), tsugi = p.FirstOrDefault(u => u.Def.Id == "tsugi");
        var beasts = new HashSet<int>();
        string N(int? id) => id is int i ? (beasts.Contains(i) ? $"獣#{i}" : names.GetValueOrDefault(i, $"#{i}")) : "—";
        Console.WriteLine($"# som320 memo —— {name} × {w.Name} × seed {seed}（{(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}）・{Seats(f)}");
        Console.WriteLine();
        foreach (var ev in r.Events)
        {
            string? line = ev.Kind switch
            {
                BattleEventKind.Summon when som is not null && ev.ActorId == som.InstanceId && ev.TargetId is int sid && beasts.Add(sid) => $"ソムが喚んだ → {N(sid)}",
                BattleEventKind.BeastThrow => $"`BeastThrow {ev.Text}` {N(ev.ActorId)} → {N(ev.TargetId)}" + (ev.PartnerId is int pp ? $"（{N(pp)}）" : ""),
                BattleEventKind.BeastBurst => $"`BeastBurst {ev.Text}` {N(ev.TargetId)}（起こし手 {N(ev.ActorId)}）",
                BattleEventKind.Highlight when hagi is not null && ev.ActorId == hagi.InstanceId => "ハギが倒れた隙に踏み込む（追い打ち）",
                BattleEventKind.Plank when tsugi is not null && ev.ActorId == tsugi.InstanceId && ev.Text == PlankLabels.Scrap && ev.Slot == 1 => $"ツギが瓦礫を拾った（{N(ev.TargetId)}・在庫 {ev.StatusRemaining}）",
                BattleEventKind.Death when ev.TargetId is int dt && p.Any(u => u.InstanceId == dt) => $"**{N(dt)} が倒れた**",
                BattleEventKind.Highlight when ev.ActorId is int ha && names.ContainsKey(ha) && p.Any(u => u.InstanceId == ha) => $"`Highlight` {N(ha)}: {ev.Text?.Trim()}",
                _ => null,
            };
            if (line is not null) Console.WriteLine($"- T{ev.Turn}: {line}");
        }
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

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som320 check —— 第320期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef som = UnitCatalog.SomH332, h = UnitCatalog.SomH319;   // 第333期: 第320〜332期の規定のソム

        // (a) 段0: 規定のソム ＝ WS-b（第319期の規定 ＋ `BeastThrowBackfire`）・旧の定義 `SomH319` ＝ 第319期の規定（CR-b）・第319期の版は旧の上
        {
            bool d = som.Traits.SequenceEqual(h.Traits.Append(TraitId.BeastThrowBackfire)) && som.Traits.SequenceEqual(UnitCatalog.SomWSb.Traits)
                && h.Traits.SequenceEqual(UnitCatalog.SomCRb.Traits) && h.Traits.SequenceEqual(UnitCatalog.SomH318.Traits.Concat(new[] { TraitId.BeastStand, TraitId.BeastBetray, TraitId.BeastResonance, TraitId.BeastResonanceArc }))
                && som.MinusText == h.MinusText + "。受け止める者がいなければ、召喚獣は投げた雷の残りで暴発する" && som.MinusText == UnitCatalog.SomWSb.MinusText && som.PlusText == h.PlusText
                && som.MaxHp == h.MaxHp && som.Attack == h.Attack && som.Speed == h.Speed && ReferenceEquals(som.Actions, h.Actions) && som.Id == h.Id && som.Name == h.Name
                && UnitCatalog.SomWSa.Traits.SequenceEqual(h.Traits.Append(TraitId.BeastStandLate)) && UnitCatalog.SomWSa3.Traits.SequenceEqual(h.Traits.Append(TraitId.BeastStandLate3));
            bool roster = UnitCatalog.All.Contains(UnitCatalog.Som) && !UnitCatalog.Everyone.Contains(som) && UnitCatalog.All.Count == 52   // 第333期: `som` は第320〜332期の規定（`SomH332`） && !UnitCatalog.Everyone.Contains(h) && !UnitCatalog.Everyone.Contains(UnitCatalog.SomWSb)
                && UnitCatalog.Everyone.Where(x => x.Traits.Contains(TraitId.BeastThrowBackfire)).SequenceEqual(new[] { UnitCatalog.Som });
            bool pin = ReferenceEquals(PinS319(som), h) && ReferenceEquals(PinS319(UnitCatalog.Shiga), UnitCatalog.ShigaH321) && ReferenceEquals(PinSS318(som), UnitCatalog.SomH318) && ReferenceEquals(PinSS318(h), UnitCatalog.SomH318)
                && ReferenceEquals(PinS317(som), UnitCatalog.SomH317) && ReferenceEquals(PinS317(h), UnitCatalog.SomH317) && ReferenceEquals(PinDS316(som), UnitCatalog.SomH317) && IsSomNow(som) && IsSomNow(h)
                && CompareBuildsH319().First(r => r.Name == "感電 (シガ×カタ×ソム)").F.Occupied().Any(o => ReferenceEquals(o.Def, h))
                && Pin318(Presets.Playtest.First(r => r.Name == S309.Shield).F).Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.SomH318));
            Expect("(a) 段0: 規定のソム ＝ WS-b（第319期の規定 ＋ `BeastThrowBackfire`・札は第319期の `SomWSb` と同じ・MinusText の末尾「受け止める者がいなければ、獣は投げた雷の残りで暴発する」）・旧の定義 `SomH319` ＝ CR-b が `Everyone` の外・第319期の版は旧の上・`Pin319` が旧へ、`PinSS318` ／ `PinS317` ／ `PinDS316` がそれより前の旧へ替える",
                d && roster && pin, $"定義 {d}・ロスター {roster}・固定 {pin}");
        }
        // (b) 規定の挙動: 受け止められなかった獣は暴発・跳ね返り ／ 打ち返しは暴発しない・**粛で止まった共鳴返しも暴発する**（指示書 §2 の確かめ）・旧（CR-b）は残る
        {
            var (c1, s1, g1, _) = S319.One(UnitCatalog.Golm, som, charged: false);
            int e1 = c1.Events.Count; S319.TurnStart(c1, s1);
            var ev1 = S319.AfterEv(c1, e1);
            bool hit = S319.T(c1, s1).WsBackfire == 1 && S319.Stood(c1, s1).Count == 0 && g1.RawCounter(StatusKeys.Shock) > 0
                && ev1.FindIndex(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Hit) < ev1.FindIndex(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Backfire);
            var (c2, s2, _, _) = S319.One(UnitCatalog.Golm, som, charged: true);
            S319.TurnStart(c2, s2);
            bool res = S319.T(c2, s2).CrFired == 1 && S319.T(c2, s2).WsBackfire == 0 && S319.Stood(c2, s2).Count == 0;
            var (c3, s3, _, _) = S319.One(UnitCatalog.ShigaH321, som, charged: false);
            S319.TurnStart(c3, s3);
            bool sg = S319.T(c3, s3).SgFired == 1 && S319.T(c3, s3).WsBackfire == 0 && S319.Stood(c3, s3).Count == 0;
            var (c4, s4, _, _) = S319.One(UnitCatalog.Golm, som, charged: true, wave: 1);
            bool hushOn = c4.HushBindingNow;
            int e4 = c4.Events.Count; S319.TurnStart(c4, s4);
            var ev4 = S319.AfterEv(c4, e4);
            int iHushed = ev4.FindIndex(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Hushed), iBf = ev4.FindIndex(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Backfire);
            bool hush = hushOn && S319.T(c4, s4).CrHushed == 1 && S319.T(c4, s4).WsBackfire == 1 && S319.Stood(c4, s4).Count == 0 && iHushed >= 0 && iHushed < iBf;
            var (c5, s5, _, _) = S319.One(UnitCatalog.Golm, h, charged: true, wave: 1);
            S319.TurnStart(c5, s5);
            bool old = S319.T(c5, s5).CrHushed == 1 && S319.T(c5, s5).WsBackfire == 0 && S319.Stood(c5, s5).Count == 1;
            Expect("(b) 規定のソム: 帯電していない味方に雷が付いた獣は `BeastBurst 投げて暴発`・跳ね返り ／ 打ち返しの獣は暴発しない・**粛で共鳴返しが止まった獣は `共鳴が止まった` の後に暴発する**（旧 CR-b では立ったまま残る）",
                hit && res && sg && hush && old, $"暴発 {hit}・跳ね返り {res}・打ち返し {sg}・粛 {hush}（粛が効いていた {hushOn}）・旧は残る {old}");
        }
        // (c) 試遊の台: 既存の12行は名前も並びもそのまま・末尾に3行・規定の駒・`compare` ／ 交差帯の行数はそのまま・DemoApp の部分一致で自分に当たる
        {
            var pl = Presets.Playtest.Take(15).ToArray();   // 第325期: 試遊の末尾に2行（粉 隣あり ／ 隣なし）を足した——第324期までの15行で見る
            var old12 = S309.Playtest308.Concat(new[] { S309.Shield, S309.ShieldHeavy, S312.Whip, S312.WhipHeavy }).ToArray();
            bool shape = pl.Length == 15 && pl.Take(12).Select(r => r.Name).SequenceEqual(old12) && pl.Skip(12).Select(r => r.Name).SequenceEqual(Added)
                && pl.Skip(12).All(r => r.F.Shape == FormationShape.X && !r.F.HasRelics && r.F.Occupied().Count() == 5 && r.F.Occupied().All(o => o.Slot < 5 && UnitCatalog.All.Contains(o.Def)) && r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Som)))
                && Presets.Compare.Length == 64 && Presets.Cross.Length == 12 && pl.Select(r => r.Name).Distinct().Count() == pl.Length && pl.All(r => !Presets.Compare.Any(c => c.Name == r.Name));
            var demoRows = Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).ToArray();
            bool demo = Added.All(n => demoRows.First(r => r.Name.Contains(n, StringComparison.Ordinal)).Name == n);
            Expect("(c) 試遊の台: `Presets.Playtest` の既存12行は名前も並びもそのまま・末尾に3行（打ち返し ／ 追い打ち ／ 瓦礫）・規定の駒だけ・`compare` 64 行 ／ 交差帯 12 行のまま・DemoApp の `--demo-preset=` の部分一致で自分に当たる",
                shape && demo, $"試遊 {pl.Length} 行・部分一致 {demo}");
        }
        // (d) 足した3台の中身（下敷きどおり）
        {
            Formation sw = Playtest(Swing), pu = Playtest(Pursuit), sc = Playtest(Scrap);
            var row = Pin321(Presets.Compare.First(r => r.Name == "感電 (シガ×カタ×ソム)").F);
            bool Same(Formation a, Formation b) { var x = a.Occupied().ToList(); var y = b.Occupied().ToList(); return a.Shape == b.Shape && x.Count == y.Count && x.Zip(y).All(q => q.First.Slot == q.Second.Slot && ReferenceEquals(q.First.Def, q.Second.Def)); }
            bool swOk = Same(sw, row) && sw.Occupied().Any(o => o.Slot == 0 && ReferenceEquals(o.Def, UnitCatalog.ShigaH321));
            bool puOk = Same(pu, S309.Swap(Playtest(S309.Shield), UnitCatalog.Kubi, UnitCatalog.Hagi));
            bool scOk = Same(sc, Candidates().First(c => c.Name == ScrapSource).F) && sc.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Tsugi));
            Expect($"(d) 打ち返し ＝ `compare` の `感電 (シガ×カタ×ソム)` と同じ駒と席（シガは前1）・追い打ち ＝ 光の盾のクビ → ハギ（席はそのまま）・瓦礫 ＝ {ScrapSource}",
                swOk && puOk && scOk, $"打ち返し {Seats(sw)}・追い打ち {Seats(pu)}・瓦礫 {Seats(sc)}");
        }
        // (e) 狙いの出来事が実戦で出る（seed 0..19・ボス ／ 近衛 ／ 本編 第3波）
        {
            M Sum(string n) { var s = new M(); foreach (var w in new[] { Waves[0], Waves[1], Waves[7] }) for (int sd = 0; sd < 20; sd++) s.Add(Fight(Playtest(n), w, sd)); return s; }
            var a = Sum(Swing); var b = Sum(Pursuit); var c = Sum(Scrap);
            Expect("(e) 狙いの出来事が実戦で出る（ボス ／ 近衛 ／ 本編 第3波 × seed 0..19）: 打ち返しの台でシガの打ち返し・追い打ちの台で獣の後のハギの追い打ち・瓦礫の台で獣から拾ったツギの瓦礫",
                a.Swing > 0 && b.PursuitBeast > 0 && c.ScrapBeast > 0, $"打ち返し {a.Swing}・追い打ち（獣の後）{b.PursuitBeast}・瓦礫（獣）{c.ScrapBeast}（{a.N} 戦ずつ）");
        }
        // (f) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var name in Added)
                foreach (var w in Waves)
                    for (int sd = 0; sd < 3; sd++)
                    {
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(Playtest(name), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var r2 = BattleEngine.Run(BattleEngine.Materialize(Playtest(name), BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        n++;
                        if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                    }
            Expect("(f) 決定的・verbose の有無で勝敗と決着T が変わらない（足した3台 × 10 波 × seed 0..2）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (g) 乱数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show fe75803:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var pr = System.Diagnostics.Process.Start(psi)!;
                    string o = pr.StandardOutput.ReadToEnd();
                    pr.WaitForExit();
                    return pr.ExitCode == 0 ? o : null;
                }
                catch { return null; }
            }
            var notes = new List<string>(); bool clean = true;
            foreach (var path in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs", "BattleCore/UnitCatalog.cs", "BattleCore/Presets.cs" })
            {
                string now = File.ReadAllText(path); string? head = Head(path);
                if (head is null) { clean = false; notes.Add($"{path}: 第319期の版を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(g) `PickOne(` ／ `Roll(` を新たに使っていない（第319期のコミット fe75803 と比べる）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    /// <summary>§3 の瓦礫の台の出どころ（`cand` の候補の名前）。</summary>
    internal const string ScrapSource = "光の盾 クビ→ツギ";
}
