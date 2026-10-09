using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// round300 —— 第300期「ザンの仇巡り（ZM-a）の規定化 ＋ ヒサの矢面の半減をミサの羽にも ＋ 標軸の試遊と演出の台本」。
// 指示書は design/PHASE300_ROUND_GUARD_SPEC.md ／ 報告は design/PHASE300_ROUND_GUARD.md。
//
//     dotnet run --project BattleSim -c Release 0 round300 ref [seeds]     # 段0-2 の参考の数字（段0-1 の後 → 段0-2 の後・誤射で受けた量）
//     dotnet run --project BattleSim -c Release 0 round300 play [seeds]    # §4-1 試遊の目安（試遊・標の5行 × 第1〜5波 ／ 近衛 ／ 大隊 ／ ボス・第296期 → 第300期を1段ずつ）
//     dotnet run --project BattleSim -c Release 0 round300 find [seeds]    # §4-2 台本の例（見せ場ごとの出来事の件数と、最初に出る seed ／ T）
//     dotnet run --project BattleSim -c Release 0 round300 memo <台の一部> <1..5|guard|bat|boss> <seed> [最後のT]   # 台本の並び
//     dotnet run --project BattleSim -c Release 0 round300 check           # 自己検査
// =====================================================================================
static class Round300Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "ref": Ref(int.Parse(A(3, "200"))); return;
            case "play": Play(int.Parse(A(3, "50"))); return;
            case "find": Find(int.Parse(A(3, "50"))); return;
            case "memo": Memo(A(3, "循環"), A(4, "boss"), int.Parse(A(5, "0")), int.Parse(A(6, "99"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("round300: モードは ref / play / find / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 共通
    // ---------------------------------------------------------------------------------
    static Doha297Diag.Wave[] Waves() => Doha297Diag.Waves();
    static Doha297Diag.Wave WaveOf(string k) => Waves().First(w => w.Key == k);
    static Doha297Diag.Wave[] MainWaves() => new[] { "2", "3", "4", "5", "guard", "bat", "boss" }.Select(WaveOf).ToArray();
    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation CompareRow(string n) => CompareBuilds().First(r => r.Name == n).F;
    static Formation Map(Formation f, params (UnitDef From, UnitDef To)[] m)
    {
        var g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied())
            foreach (var (a, b) in m) if (ReferenceEquals(d, a)) { g[slot] = b; break; }
        return g;
    }

    /// <summary>§4-1 の試遊・標の5行（`Presets.Playtest` の順）。</summary>
    static (string Name, Formation F)[] MarkRows() => Presets.Playtest.Where(r => r.Name.StartsWith("試遊・標", StringComparison.Ordinal)).ToArray();

    /// <summary>段0-2 の代表台（§3 の参考の数字）。</summary>
    static (string Name, Formation F)[] RefBoards() => new[]
    {
        ("標経済 (ヒサ×ザン×ミサ)", CompareRow("標経済 (ヒサ×ザン×ミサ)")),
        ("試遊・標 循環", Playtest("試遊・標 循環")),
        ("試遊・標 三人組", Playtest("試遊・標 三人組")),
        ("試遊・標 守り型", Playtest("試遊・標 守り型")),
    };

    /// <summary>
    /// 第296期 → 第300期の規定化を1段ずつ（§4-1）。どの段も「その時点の規定の駒」に差し替える（後ろの段ほど新しい）。
    /// 0 第296期（ドハ `DohaD0`・ミサ `TomeMb`・ザン `ZanZN0`・ヒサ `HisaHKb`）／ 1 ＋ドハ DH-a（第298期）／ 2 ＋ミサ MF-b ・ザン ZN-b（第299期 段0-1）／
    /// 3 ＋ヒサが自分も癒す（第299期 段0-2）／ 4 ＋ザン ZM-a（第300期 段0-1）／ 5 ＋矢面は羽も半分（第300期 段0-2 ＝ 今の規定）。
    /// </summary>
    static readonly string[] StepNames = { "第296期", "＋ドハ DH-a（第298期）", "＋ミサ MF-b ・ザン ZN-b（第299期）", "＋ヒサが自分も癒す（第299期）", "＋ザン ZM-a（第300期）", "＋矢面は羽も半分（第300期）" };
    // 第301期: ソラ ／ ザン ／ ヒサの規定が動いた——どの段も最後に `Pin301` で第300期の規定（ソラ `SoraSRs`・ザン `ZanZMa`・ヒサ `HisaHKf`）に固定する（段5 ＝ 第300期の規定）。
    static Formation Step(Formation f, int k) => Pin301(k switch
    {
        0 => Map(f, (UnitCatalog.Doha, UnitCatalog.DohaD0), (UnitCatalog.Tome, UnitCatalog.TomeMb), (UnitCatalog.Zan, UnitCatalog.ZanZN0), (UnitCatalog.Hisa, UnitCatalog.HisaHKb)),
        1 => Map(f, (UnitCatalog.Tome, UnitCatalog.TomeMb), (UnitCatalog.Zan, UnitCatalog.ZanZN0), (UnitCatalog.Hisa, UnitCatalog.HisaHKb)),
        2 => Map(f, (UnitCatalog.Zan, UnitCatalog.ZanZNb), (UnitCatalog.Hisa, UnitCatalog.HisaHKb)),
        3 => Map(f, (UnitCatalog.Zan, UnitCatalog.ZanZNb), (UnitCatalog.Hisa, UnitCatalog.HisaHKs)),
        4 => Map(f, (UnitCatalog.Hisa, UnitCatalog.HisaHKs)),
        _ => f,
    });

    // ---------------------------------------------------------------------------------
    // 1戦の集計
    // ---------------------------------------------------------------------------------
    sealed class Agg
    {
        public long N, Wins, WinT, Turns, FirstDeathN, FirstDeathT, FfBeckon, FfBeckonHits, FfOther, FfOtherHits, Saved, SavedHits, MfAlly, Framed, Rounds;
        public void Add(Agg o)
        {
            foreach (var f in typeof(Agg).GetFields()) if (f.FieldType == typeof(long)) f.SetValue(this, (long)f.GetValue(this)! + (long)f.GetValue(o)!);
        }
        public double P(long x) => N == 0 ? 0 : (double)x / N;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public double WinTurns => Wins == 0 ? 0 : (double)WinT / Wins;
    }

    static Agg One(Formation f, Func<List<UnitState>> enemy, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, enemy(), seed, verbose: false);
        var a = new Agg { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var dead = p.Where(u => u.LastDeathTurn > 0).Select(u => u.LastDeathTurn).ToList();
        if (dead.Count > 0) { a.FirstDeathN = 1; a.FirstDeathT = dead.Min(); }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!mine.Contains(id)) continue;
            a.FfBeckon += t.FeatherFfBeckonTaken; a.FfBeckonHits += t.FeatherFfBeckonHits; a.FfOther += t.FeatherFfOtherTaken; a.FfOtherHits += t.FeatherFfOtherHits;
            a.Saved += t.BeckonFeatherSaved; a.SavedHits += t.BeckonFeatherHits; a.MfAlly += t.MfShotsAlly; a.Framed += t.FrameVendettas; a.Rounds += t.RoundTurns;
        }
        return a;
    }

    static Agg Many(Formation f, Func<List<UnitState>> enemy, int seeds)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, s => parts[s] = One(f, enemy, s));
        var a = new Agg();
        foreach (var x in parts) a.Add(x);
        return a;
    }

    static string FirstDeath(Agg a) => a.FirstDeathN == 0 ? "—" : $"{(double)a.FirstDeathT / a.FirstDeathN:F1}（{100.0 * a.FirstDeathN / a.N:F0}%）";
    static string Cell(Agg a) => a.Wins == 0 ? $"{a.Win:F0}%" : $"{a.Win:F0}%（{a.WinTurns:F1}）";

    // ---------------------------------------------------------------------------------
    // 段0-2 の参考の数字
    // ---------------------------------------------------------------------------------
    static void Ref(int seeds)
    {
        Console.WriteLine($"# 第300期 段0-2 —— 矢面の半減をミサの羽にも（段0-1 の後 ＝ ヒサ `HisaHKs` → 段0-2 の後 ＝ 規定・seed 0..{seeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("ザンは段0-1 の後の規定（ZM-a）。「誤射で受けた量」はミサの羽（羽の保持者が出どころの同士討ち・徴収 ／ 中継は除く）で味方が受けた実額（括弧は回数）。矢面 ＝ その一撃の時点でヒサの標を持つ味方。「防いだ量」は羽の一撃を矢面で半分にした量。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 前 → 後 | 決着T 前 → 後 | 味方が初めて倒れたT 前 → 後 | 誤射・矢面 前 → 後 | 誤射・ほか 前 → 後 | 防いだ量（回） | 味方への標撃ち | 濡れ衣の仇討ち 前 → 後 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|--:|---|");
        foreach (var (n, f) in RefBoards())
            foreach (var w in MainWaves())
            {
                var a = Many(Step(f, 4), w.Make, seeds);
                var b = Many(Step(f, 5), w.Make, seeds);
                string Bold(string s) => Math.Abs(b.Win - a.Win) >= 10 ? $"**{s}**" : s;
                Console.WriteLine($"| {n} | {w.Name} | {Bold($"{a.Win:F1} → {b.Win:F1}")} | {a.P(a.Turns):F1} → {b.P(b.Turns):F1} | {FirstDeath(a)} → {FirstDeath(b)} | {a.P(a.FfBeckon):F1}（{a.P(a.FfBeckonHits):F2}）→ {b.P(b.FfBeckon):F1}（{b.P(b.FfBeckonHits):F2}） | {a.P(a.FfOther):F1}（{a.P(a.FfOtherHits):F2}）→ {b.P(b.FfOther):F1}（{b.P(b.FfOtherHits):F2}） | {b.P(b.Saved):F1}（{b.P(b.SavedHits):F2}） | {b.P(b.MfAlly):F2} | {a.P(a.Framed):F2} → {b.P(b.Framed):F2} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // §4-1 試遊の目安
    // ---------------------------------------------------------------------------------
    static void Play(int seeds)
    {
        var waves = Enumerable.Range(1, EnemyCatalog.Stages.Count).Select(i => WaveOf(i.ToString())).Concat(new[] { "guard", "bat", "boss" }.Select(WaveOf)).ToArray();
        var rows = MarkRows();
        var res = new Dictionary<(string, int, string), Agg>();
        foreach (var (n, f) in rows) for (int k = 0; k < StepNames.Length; k++) foreach (var w in waves) res[(n, k, w.Key)] = Many(Step(f, k), w.Make, seeds);
        string Head() => "| 行 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |";
        string Rule() => "|---|" + string.Concat(Enumerable.Repeat("--:|", waves.Length));

        Console.WriteLine($"# 第300期 §4-1 試遊の目安（seed 0..{seeds - 1}・括弧は倒しT＝勝った戦の決着T）");
        Console.WriteLine();
        Console.WriteLine("## 今の規定（第300期）");
        Console.WriteLine();
        Console.WriteLine(Head()); Console.WriteLine(Rule());
        foreach (var (n, _) in rows) Console.WriteLine($"| {n} | " + string.Join(" | ", waves.Select(w => Cell(res[(n, StepNames.Length - 1, w.Key)]))) + " |");
        Console.WriteLine();
        Console.WriteLine("## 第296期の規定（ドハ `DohaD0`・ミサ `TomeMb`・ザン `ZanZN0`・ヒサ `HisaHKb`）——第296期 §2-1 の表と同じになるはず");
        Console.WriteLine();
        Console.WriteLine(Head()); Console.WriteLine(Rule());
        foreach (var (n, _) in rows) Console.WriteLine($"| {n} | " + string.Join(" | ", waves.Select(w => Cell(res[(n, 0, w.Key)]))) + " |");
        Console.WriteLine();
        Console.WriteLine("## 1段ずつ（勝率が動いたセルだけ・前の段 → この段・括弧は倒しT）");
        Console.WriteLine();
        for (int k = 1; k < StepNames.Length; k++)
        {
            var moved = new List<string>();
            foreach (var (n, _) in rows)
                foreach (var w in waves)
                {
                    var a = res[(n, k - 1, w.Key)]; var b = res[(n, k, w.Key)];
                    if (a.Wins != b.Wins || Math.Abs(a.WinTurns - b.WinTurns) >= 0.5) moved.Add($"{n.Replace("試遊・標 ", "")} × {w.Name} {Cell(a)} → {Cell(b)}");
                }
            Console.WriteLine($"- **{StepNames[k]}**: " + (moved.Count == 0 ? "動いたセルなし" : string.Join(" ／ ", moved)));
        }
    }

    // ---------------------------------------------------------------------------------
    // §4-2 台本の例
    // ---------------------------------------------------------------------------------
    sealed record Show(string Label, string Row, string Wave, Func<BattleEvent, List<UnitState>, bool> Hit);

    static Show[] Shows() => new Show[]
    {
        new("ドハの力配り（`ShareGive`「力」）", "試遊・標 守り型", "boss", (x, _) => x.Kind == BattleEventKind.ShareGive && x.Text == ShareGiveLabels.Power),
        new("ミサの標撃ち・敵（`FeatherMark`「敵」）", "試遊・標 循環", "guard", (x, _) => x.Kind == BattleEventKind.FeatherMark && x.Text == FeatherMarkLabels.Foe),
        new("ミサの標撃ち・味方（`FeatherMark`「味方」）", "試遊・標 循環", "guard", (x, _) => x.Kind == BattleEventKind.FeatherMark && x.Text == FeatherMarkLabels.Ally),
        new("あいつがやった！（`Framed`「あいつがやった」）", "試遊・標 循環", "guard", (x, _) => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Accuse),
        new("濡れ衣の仇討ち（`Framed`「濡れ衣」）", "試遊・標 循環", "guard", (x, _) => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Vendetta),
        new("仇巡り・ボスの連撃（`VendettaRound`「仇巡り」）", "試遊・標 循環", "boss", (x, _) => x.Kind == BattleEventKind.VendettaRound && x.Text == VendettaRoundLabels.Start),
        new("仇巡り・精鋭の巡り（`VendettaRound`「仇巡り」・2体以上）", "試遊・標 三人組", "bat", (x, _) => x.Kind == BattleEventKind.VendettaRound && x.Text == VendettaRoundLabels.Start && x.Slot >= 2),
        new("ヒサの叫びがヒサ自身を癒す（`MarkRally` の宛先がヒサ）", "試遊・標 三人組", "guard", (x, p) => x.Kind == BattleEventKind.MarkRally && x.TargetId == x.ActorId),
        new("矢面の半減が羽に効いた（`BeckonFeather`）", "標経済 (ヒサ×ザン×ミサ)", "bat", (x, _) => x.Kind == BattleEventKind.BeckonFeather),
    };

    static Formation RowOf(string n) => Pin301(n.StartsWith("試遊", StringComparison.Ordinal) ? Playtest(n) : CompareRow(n));   // 第301期: 第300期の規定に固定

    static void Find(int seeds)
    {
        Console.WriteLine($"# 第300期 §4-2 台本の例（規定の駒・seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 見せ場 | 台 × 波 | 1戦あたりの件数 | 出た戦 | seed 0 の件数（最初の T） | 最初に出る seed（最初の T） |");
        Console.WriteLine("|---|---|--:|--:|---|---|");
        foreach (var s in Shows())
        {
            var f = RowOf(s.Row); var w = WaveOf(s.Wave);
            var cnt = new int[seeds]; var first = new int[seeds];
            Parallel.For(0, seeds, sd =>
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, w.Make(), sd, verbose: true);
                var hits = r.Events.Where(x => s.Hit(x, p)).ToList();
                cnt[sd] = hits.Count; first[sd] = hits.Count > 0 ? hits[0].Turn : -1;
            });
            int fs = Array.FindIndex(cnt, c => c > 0);
            Console.WriteLine($"| {s.Label} | {s.Row} × {w.Name} | {cnt.Average():F2} | {cnt.Count(c => c > 0)} / {seeds} | {cnt[0]}{(cnt[0] > 0 ? $"（T{first[0]}）" : "")} | {(fs < 0 ? "—" : $"seed {fs}（T{first[fs]}）")} |");
        }
    }

    static void Memo(string rowPart, string wave, int seed, int lastTurn)
    {
        var (name, f0) = MarkRows().Concat(CompareBuilds()).First(b => b.Name.Contains(rowPart, StringComparison.Ordinal));
        var f = Pin301(f0);   // 第301期: 第300期の規定に固定
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# round300 memo —— {name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Turn > lastTurn) break;
            if (x.Kind is not (BattleEventKind.Damage or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.Feather or BattleEventKind.MarkRally
                or BattleEventKind.Death or BattleEventKind.Attack or BattleEventKind.VendettaRound or BattleEventKind.ShareGive or BattleEventKind.BeckonFeather
                or BattleEventKind.MarkLayer or BattleEventKind.Heal or BattleEventKind.Whet)) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.PartnerId is int pa ? $"  Partner={N(pa)}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}{(x.Relayed ? "  relayed" : "")}");
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

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly PropertyInfo TurnProp = typeof(BattleContext).GetProperty("Turn") ?? throw new InvalidOperationException("Turn が見つからない");
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit");
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();

    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnProp.SetValue(ctx, 1);
        return ctx;
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
        Console.WriteLine("# round300 check —— 第300期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        var zan = UnitCatalog.ZanZMa; var hisa = UnitCatalog.HisaHKf;   // 第301期: 第300期の規定は `ZanZMa` ／ `HisaHKf`（`Pin301`）
        Expect("(a) 段0-1: 規定のザン ＝ `ZanZNb`（第299期の規定）＋ `VendettaRound`（＝ 第299期の ZM-a）・文面は ZM-a のまま・旧は `All` ／ `Everyone` の外・第299期の版は旧から作る",
            zan.Traits.SequenceEqual(UnitCatalog.ZanZNb.Traits.Append(TraitId.VendettaRound)) && zan.Traits.SequenceEqual(UnitCatalog.ZanZMa.Traits)
            && zan.PlusText == UnitCatalog.ZanZMa.PlusText && zan.PlusText.EndsWith("。手番では、仇を巡って斬る。深く指差された仇ほど、何度も斬る", StringComparison.Ordinal)
            && zan.MinusText == UnitCatalog.ZanZNb.MinusText && zan.Flavor == UnitCatalog.ZanZNb.Flavor && zan.MaxHp == 56 && zan.Attack == 10 && zan.Speed == 5
            && UnitCatalog.All.Contains(UnitCatalog.Zan) && !UnitCatalog.Everyone.Contains(zan) && !UnitCatalog.Everyone.Contains(UnitCatalog.ZanZNb)
            && UnitCatalog.ZanZNb.Traits.SequenceEqual(UnitCatalog.ZanZN0.Traits.Append(TraitId.VendettaFrameAll))
            && UnitCatalog.ZanZM1.Traits.SequenceEqual(UnitCatalog.ZanZNb.Traits.Append(TraitId.VendettaRoundOne)) && VendettaTrait.RoundCap == 8);
        Expect("(b) 段0-2: 規定のヒサ ＝ `HisaHKs`（第299期の規定）＋ `BeckonFeather`・プラスの文面は「（ミサの羽でも）」を足しただけ・数値 ／ 手番は旧のまま・旧は `All` ／ `Everyone` の外",
            hisa.Traits.SequenceEqual(UnitCatalog.HisaHKs.Traits.Append(TraitId.BeckonFeather)) && UnitCatalog.HisaHKs.Traits.SequenceEqual(UnitCatalog.HisaHKb.Traits.Append(TraitId.MarkRallySelf))
            && hisa.PlusText == UnitCatalog.HisaHKs.PlusText.Replace("痛みが半分になる。", "痛みが半分になる（ミサの羽でも）。") && hisa.PlusText != UnitCatalog.HisaHKs.PlusText
            && hisa.MinusText == UnitCatalog.HisaHKs.MinusText && hisa.Flavor == UnitCatalog.HisaHKs.Flavor && hisa.MaxHp == UnitCatalog.HisaHKs.MaxHp && hisa.Speed == UnitCatalog.HisaHKs.Speed
            && hisa.Actions!.SequenceEqual(UnitCatalog.HisaHKs.Actions!) && UnitCatalog.All.Contains(UnitCatalog.Hisa) && !UnitCatalog.Everyone.Contains(hisa) && !UnitCatalog.Everyone.Contains(UnitCatalog.HisaHKs));

        // 盤面を直に組む: 前1 ドルガ（矢面の相手）／ 前3 ゴルム（標だけ・矢面でない）／ 中央 ミサ ／ 後1 ボルグ ／ 後3 ヒサ。敵は ガルド1体。
        // 一撃を直に `ApplyDamage` して HP の減りを旧（`HisaHKs`）と規定で比べる（他の軽減の段は両方に同じだけ掛かる）。
        int Loss(UnitDef h, string shooter, bool beckonTarget)
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Golm, center: UnitCatalog.Tome, back1: UnitCatalog.Borg, back3: h), Formation.Build(front1: UnitCatalog.Gald), out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa");
            var dolga = p.First(u => u.Def.Id == UnitCatalog.Dolga.Id);
            var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id);
            hi.SetCounter(BeckonTrait.TargetKey, dolga.InstanceId + 1);
            dolga.SetCounter(StatusKeys.Marked, 1); golm.SetCounter(StatusKeys.Marked, 1);   // 矢面の守りは標が残っている間だけ（`BeckonGuardOf`）。比べる2体に同じ標を付ける
            var tgt = beckonTarget ? dolga : golm;
            foreach (var u in p) { u.MaxHp = 1000; u.Hp = 1000; }
            UnitState src = shooter switch { "misa" => p.First(u => u.Def.Id == "tome"), "borg" => p.First(u => u.Def.Id == UnitCatalog.Borg.Id), _ => e[0] };
            int before = tgt.Hp;
            ctx.ApplyDamage(tgt, 40, src, isFriendlyFire: src.TeamId == tgt.TeamId, pattern: AttackPattern.Single);
            return before - tgt.Hp;
        }
        int m0 = Loss(UnitCatalog.HisaHKs, "misa", true), m1 = Loss(hisa, "misa", true);
        Expect("(c) ミサの羽が矢面の味方に当たると半分（旧 `HisaHKs` は満額）", m1 * 2 == m0 && m1 > 0, $"旧 {m0} ／ 規定 {m1}");
        int o0 = Loss(UnitCatalog.HisaHKs, "misa", false), o1 = Loss(hisa, "misa", false);
        Expect("(d) 矢面でない味方には満額（旧と同じ）", o0 == o1 && o1 > 0, $"旧 {o0} ／ 規定 {o1}");
        int b0 = Loss(UnitCatalog.HisaHKs, "borg", true), b1 = Loss(hisa, "borg", true);
        Expect("(e) ほかの同士討ち（ボルグが出どころ）は矢面でも満額（旧と同じ）", b0 == b1 && b1 > 0, $"旧 {b0} ／ 規定 {b1}");
        int f0 = Loss(UnitCatalog.HisaHKs, "foe", true), f1 = Loss(hisa, "foe", true), g1 = Loss(hisa, "foe", false);
        Expect("(f) 敵の攻撃の矢面の半減は今のまま（旧 ＝ 規定・矢面でない味方の半分）", f0 == f1 && f1 * 2 == g1, $"旧 {f0} ／ 規定 {f1} ／ 矢面でない {g1}");

        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(g) `PickOne(` の出現数が第299期と同じ（BattleCore）", pick == 36, $"{pick}");

        // (h) verbose の有無で勝敗・決着T が同じ（段0-1 の後 ／ 規定 × 段0-2 の代表台 × 試遊の波 × seed 0..9）
        int diff = 0, n2 = 0;
        foreach (int k in new[] { 4, 5 }) foreach (var (_, f) in RefBoards()) foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf)) for (int s = 0; s < 10; s++)
                    {
                        var g = Step(f, k);
                        var a = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                        var b = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                        n2++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                    }
        Expect("(h) verbose の有無で勝敗・決着T が同じ（段0-1 の後 ／ 規定 × 代表台 4 × 試遊の波 × seed 0..9）", diff == 0, $"{n2} 戦・違い {diff}");

        // (i) 出来事: 規定の標経済 × 大隊で `BeckonFeather` が出る・旧（`HisaHKs`）では 0・規定の循環 × ボスで `VendettaRound` が出る
        long Ev(Formation f, string wave, BattleEventKind k) { long x = 0; for (int s = 0; s < 10; s++) x += BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), WaveOf(wave).Make(), s, verbose: true).Events.Count(e => e.Kind == k); return x; }
        var econR = CompareRow("標経済 (ヒサ×ザン×ミサ)"); var econ = Step(econR, 5);
        long bf0 = Ev(Step(econR, 4), "bat", BattleEventKind.BeckonFeather), bf1 = Ev(econ, "bat", BattleEventKind.BeckonFeather);
        long vr0 = Ev(Step(Playtest("試遊・標 循環"), 3), "boss", BattleEventKind.VendettaRound), vr1 = Ev(Step(Playtest("試遊・標 循環"), 5), "boss", BattleEventKind.VendettaRound);
        Expect("(i) 出来事: 規定の標経済 × 大隊で `BeckonFeather` が出る（旧 0）・規定の循環 × ボスで `VendettaRound` が出る（第299期の規定 0）（seed 0..9）", bf0 == 0 && bf1 > 0 && vr0 == 0 && vr1 > 0, $"羽の半減 {bf0} → {bf1}・仇巡り {vr0} → {vr1}");

        // (j) 防いだ量 ＝ 受け手の防いでもらった量（計数の対）・誤射の計数はヒサの札に依らない（段0-1 の後でも数える）
        var aa = Many(Step(econR, 4), WaveOf("bat").Make, 20); var bb = Many(econ, WaveOf("bat").Make, 20);
        long taken = 0; for (int s = 0; s < 20; s++) { var p = BattleEngine.Materialize(econ, BattleContext.PlayerTeam); var r = BattleEngine.Run(p, WaveOf("bat").Make(), s, verbose: false); var ids = p.Select(u => u.Def.Id).ToHashSet(); taken += r.TallyByUnit.Where(kv => ids.Contains(kv.Key)).Sum(kv => kv.Value.BeckonFeatherTaken); }
        Expect("(j) 計数: ヒサの防いだ量 ＝ 受け手の防いでもらった量・段0-1 の後でも誤射（矢面）を数える", bb.Saved == taken && bb.Saved > 0 && aa.Saved == 0 && aa.FfBeckonHits > 0, $"防いだ {bb.Saved} ／ もらった {taken}・段0-1 の後の誤射（矢面）{aa.FfBeckonHits} 回");

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
