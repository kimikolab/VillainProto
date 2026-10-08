using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// doha297 —— 第297期「分かちのドハ：痛みをもらって、力を配る」。指示書は design/PHASE297_DOHA_SHARE_SPEC.md ／ 報告は design/PHASE297_DOHA_SHARE.md。
//
//     dotnet run --project BattleSim -c Release 0 doha297 p0            # Phase 0: 仇討ちのきっかけの内訳・肩代わりの量（相手ごと）・ドハの攻撃力の使われ方・配り先が飛ぶ駒
//     dotnet run --project BattleSim -c Release 0 doha297 rates [seeds] # §5: 代表台 × 波（本編 第1〜5波 ／ 近衛 ／ 大隊 ／ ボス規定形）× ドハの版（規定 ／ DH-a ／ DH-b ／ DH-t ／ ドルガ）
//     dotnet run --project BattleSim -c Release 0 doha297 atk [台の一部] # §5-3: 配られた駒の攻撃力の推移（ターンごと）と与ダメ（seed 0..49・verbose）
//     dotnet run --project BattleSim -c Release 0 doha297 memo <台の一部> <boss|guard|bat|1..5> <seed> <規定|a|b|t>   # 台本の並び（ShareGive ／ 仇討ち ／ 中継）
//     dotnet run --project BattleSim -c Release 0 doha297 grid <boss|guard|bat> <a|b|t> [席の数=6] [seed 数=10] [アタッカーの絞り込み]   # §5-2 の格子（固定枠 ドハ ＋ アタッカー1枚 × 探索枠3・規定と版を同じ台で）
//     dotnet run --project BattleSim -c Release 0 doha297 check         # 自己検査
//
// 規定の駒・札・数値・波は1つも変えない。ドハの版は `UnitCatalog.DohaDHa` ／ `DohaDHb` ／ `DohaDHt` を台の規定のドハと差し替えるだけ（`FvSwap`）。
// =====================================================================================
static class Doha297Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "rates";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(); return;
            case "rates": Rates(int.Parse(A(3, "200"))); return;
            case "atk": Atk(A(3, "守り型")); return;
            case "memo": Memo(A(3, "守り型"), A(4, "guard"), int.Parse(A(5, "7")), A(6, "規定")); return;
            case "grid": Grid(A(3, "guard"), A(4, "a"), int.Parse(A(5, "6")), int.Parse(A(6, "10")), A(7, "")); return;
            case "check": Check(); return;
            default: Console.WriteLine("doha297: モードは p0 / rates / atk / memo / grid / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 台・波・版
    // ---------------------------------------------------------------------------------
    internal static readonly (string Tag, UnitDef Def)[] Versions =
    {
        ("規定", UnitCatalog.DohaD0), ("DH-a", UnitCatalog.DohaDHa), ("DH-b", UnitCatalog.DohaDHb), ("DH-t", UnitCatalog.DohaDHt),
    };
    static UnitDef Ver(string s) => s switch { "a" or "DH-a" => UnitCatalog.DohaDHa, "b" or "DH-b" => UnitCatalog.DohaDHb, "t" or "DH-t" => UnitCatalog.DohaDHt, _ => UnitCatalog.DohaD0 };
    static string VerTag(UnitDef d) => Versions.First(v => ReferenceEquals(v.Def, d)).Tag;

    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;

    /// <summary>代表台（§5-2）。守り型 ／ `compare` と交差帯のドハの行すべて ／ 試遊・感電 雷の型 ／ 火の軸の台（燃焼 (ボルグ×ホタ) の ガルド → ドハ）。</summary>
    internal static (string Name, Formation F)[] Boards()
    {
        var l = new List<(string, Formation)> { ("試遊・標 守り型", FvSwap(Playtest("試遊・標 守り型"), UnitCatalog.Doha, UnitCatalog.DohaD0)) };
        foreach (var (n, f) in CompareBuilds().Concat(CrossBuilds()))
            if (f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Doha))) l.Add((n, FvSwap(f, UnitCatalog.Doha, UnitCatalog.DohaD0)));   // 第298期: 旧の規定に固定
        l.Add(("試遊・感電 雷の型", FvSwap(Playtest("試遊・感電 雷の型"), UnitCatalog.Doha, UnitCatalog.DohaD0)));
        var burn = CompareBuilds().First(r => r.Name == "燃焼 (ボルグ×ホタ)").F;
        l.Add(("燃焼＋ドハ（燃焼 (ボルグ×ホタ) の ガルド → ドハ）", FvSwap(burn, UnitCatalog.Gald, UnitCatalog.DohaD0)));
        return l.ToArray();
    }

    internal sealed record Wave(string Name, string Key, Func<List<UnitState>> Make);

    /// <summary>本編 第1〜5波（`compare` と同じ倍率）／ 近衛 ／ 大隊 ／ ボス規定形（試遊の波・波ごとの倍率）。</summary>
    internal static Wave[] Waves()
    {
        var l = new List<Wave>();
        for (int i = 0; i < EnemyCatalog.Stages.Count; i++) { int ii = i; l.Add(new($"第{i + 1}波", (i + 1).ToString(), () => BattleEngine.Materialize(EnemyCatalog.Stages[ii].Enemy, BattleContext.EnemyTeam))); }
        foreach (var (i, k) in new[] { (1, "guard"), (2, "bat"), (0, "boss") }) { var w = EnemyCatalog.PlaytestStages[i]; l.Add(new(w.Name, k, () => BattleEngine.MaterializeEnemy(w.Enemy, w.Scale))); }
        return l.ToArray();
    }
    static Wave WaveOf(string key) => Waves().First(w => w.Key == key);

    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static string Seats(Formation f) => string.Join("・", Enumerable.Range(0, 5).Select(i => f[i] is { } d ? Short(d) : "—"));

    // ---------------------------------------------------------------------------------
    // 1戦の集計
    // ---------------------------------------------------------------------------------
    sealed class Agg
    {
        public long N, Wins, WinT, FirstDeathN, FirstDeathT, DohaDeathN, DohaDeathT, DohaPresent, Vend, Shoulder, ShareHits, Given, Gives, Gifts, GiftTurns, GiftAttacks, GiftCapped, DohaDirect;
        public readonly Dictionary<string, long> Partner = new(), Got = new(), GiftTo = new();
        public void Add(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; DohaDeathN += o.DohaDeathN; DohaDeathT += o.DohaDeathT; DohaPresent += o.DohaPresent;
            Vend += o.Vend; Shoulder += o.Shoulder; ShareHits += o.ShareHits; Given += o.Given; Gives += o.Gives; Gifts += o.Gifts; GiftTurns += o.GiftTurns; GiftAttacks += o.GiftAttacks; GiftCapped += o.GiftCapped; DohaDirect += o.DohaDirect;
            foreach (var (k, v) in o.Partner) Partner[k] = Partner.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Got) Got[k] = Got.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.GiftTo) GiftTo[k] = GiftTo.GetValueOrDefault(k) + v;
        }
        public double P(long x) => N == 0 ? 0 : (double)x / N;
        public string Win => N == 0 ? "—" : $"{100.0 * Wins / N:F1}";
    }

    static Agg One(Formation f, Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new Agg { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var dead = p.Where(u => u.LastDeathTurn > 0).Select(u => u.LastDeathTurn).ToList();
        if (dead.Count > 0) { a.FirstDeathN = 1; a.FirstDeathT = dead.Min(); }
        var doha = p.FirstOrDefault(u => u.Def.Id == "doha");
        if (doha is not null) { a.DohaPresent = 1; if (doha.LastDeathTurn > 0) { a.DohaDeathN = 1; a.DohaDeathT = doha.LastDeathTurn; } }
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => Short(g.First().Def));
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;   // 敵側の帳簿は数えない
            if (id == "zan") a.Vend += t.VendettaFires;
            a.Shoulder += t.SharedAway;
            if (t.SharedAway > 0) a.Partner[nm] = a.Partner.GetValueOrDefault(nm) + t.SharedAway;
            if (t.ShareGot > 0) a.Got[nm] = a.Got.GetValueOrDefault(nm) + t.ShareGot;
            if (t.ShareGiftGot > 0) a.GiftTo[nm] = a.GiftTo.GetValueOrDefault(nm) + t.ShareGiftGot;
            a.Given += t.ShareGiven; a.Gives += t.ShareGives; a.Gifts += t.ShareGifts; a.GiftCapped += t.ShareGiftCapped;
            a.GiftTurns += t.ShareGiftTurns; a.GiftAttacks += t.ShareGiftAttacks; a.ShareHits += t.ShareTakenHits;
            if (id == "doha") a.DohaDirect += t.DamageTaken - t.Shouldered;
        }
        return a;
    }

    static Agg Many(Formation f, Wave w, int seeds)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, s => parts[s] = One(f, w, s));
        var a = new Agg();
        foreach (var x in parts) a.Add(x);
        return a;
    }

    static string Top(Dictionary<string, long> d, long n, int k = 4)
        => d.Count == 0 ? "—" : string.Join("・", d.OrderByDescending(x => x.Value).Take(k).Select(x => $"{x.Key} {(double)x.Value / n:F1}"));

    // ---------------------------------------------------------------------------------
    // Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        Console.WriteLine("# 第297期 Phase 0 —— 分かちのドハ（規定の駒のまま）");
        Console.WriteLine();
        VendettaBreakdown();
        ShareAmounts();
        DohaAttackUse();
        SupportSkips();
    }

    /// <summary>§4-1: ザンの仇討ちのきっかけの内訳（台本で、仇討ちの `Damage` の直前の「敵 → 味方」の `Damage` を引く）。</summary>
    static void VendettaBreakdown()
    {
        Console.WriteLine("## §4-1 ザンの仇討ちのきっかけ（試遊・標 守り型）");
        Console.WriteLine();
        Console.WriteLine("仇討ち ＝ ザンが敵に入れた `Reaction` の `Damage`。きっかけ ＝ その直前（台本を遡って最初）の「敵 → 味方」の `Damage`。"
            + "**中継** ＝ きっかけがドハへの `Relayed`（分かちの分け前）／ **ドハへ直接** ／ **ほかの味方**（標を持つ味方・名前ごと）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | seed | 勝敗 | 仇討ち | 中継（ドハの分け前） | ドハへ直接 | ほかの味方 | きっかけ不明 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|--:|");
        var f = FvSwap(Playtest("試遊・標 守り型"), UnitCatalog.Doha, UnitCatalog.DohaD0);   // 第298期: 旧の規定に固定
        void Line(Wave w, IEnumerable<int> seeds, string label)
        {
            long n = 0, vend = 0, relay = 0, direct = 0, none = 0, wins = 0;
            var other = new Dictionary<string, long>();
            foreach (int s in seeds)
            {
                n++;
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                if (r.PlayerWon) wins++;
                var mine = p.ToDictionary(u => u.InstanceId, u => u);
                int zan = p.First(u => u.Def.Id == "zan").InstanceId, doha = p.First(u => u.Def.Id == "doha").InstanceId;
                for (int i = 0; i < r.Events.Count; i++)
                {
                    var x = r.Events[i];
                    if (x.Kind != BattleEventKind.Damage || x.ActorId != zan || !x.Reaction || x.FriendlyFire || x.TargetId is not int tg || mine.ContainsKey(tg)) continue;
                    vend++;
                    BattleEvent? k = null;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var y = r.Events[j];
                        if (y.Kind == BattleEventKind.Damage && y.TargetId is int yt && mine.ContainsKey(yt) && y.ActorId is int ya && !mine.ContainsKey(ya)) { k = y; break; }
                    }
                    if (k is null) none++;
                    else if (k.TargetId == doha && k.Relayed) relay++;
                    else if (k.TargetId == doha) direct++;
                    else { string nm = Short(mine[k.TargetId!.Value].Def); other[nm] = other.GetValueOrDefault(nm) + 1; }
                }
            }
            double P(long v) => (double)v / n;
            Console.WriteLine($"| {w.Name} | {label} | {wins}/{n} | {P(vend):F1} | {P(relay):F1}（{(vend == 0 ? 0 : 100.0 * relay / vend):F0}%） | {P(direct):F1} | "
                + $"{(other.Count == 0 ? "—" : string.Join("・", other.OrderByDescending(o => o.Value).Select(o => $"{o.Key} {P(o.Value):F1}")))} | {P(none):F1} |");
        }
        var guard = WaveOf("guard");
        Line(guard, new[] { 7 }, "7（ポンの試遊）");
        Line(guard, Enumerable.Range(0, 50), "0..49 の平均");
        Line(WaveOf("bat"), Enumerable.Range(0, 50), "0..49 の平均");
        Line(WaveOf("boss"), Enumerable.Range(0, 50), "0..49 の平均");
        Line(WaveOf("5"), Enumerable.Range(0, 50), "0..49 の平均");
        Console.WriteLine();

        // 戦績の帰属（段0）: 同じ戦（近衛 × seed 7）の回復(与)
        var p7 = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r7 = BattleEngine.Run(p7, guard.Make(), 7, verbose: false);
        string H(string id) { var t = r7.TallyByUnit.GetValueOrDefault(id); return t is null ? "—" : $"{t.HealOutInTurn + t.HealOutOffTurn}"; }
        Console.WriteLine($"戦績の回復(与)（近衛 × seed 7）: ヒサ {H("hisa")} ／ ザン {H("zan")} ／ ドハ {H("doha")} ／ ゴルム {H("golm")} ／ ミサ {H("tome")}"
            + $"（ヒサの叫びの癒えた量 `RallyHealed` {r7.TallyByUnit.GetValueOrDefault("hisa")?.RallyHealed}）・ザンの与ダメの元 ＝ 仇討ち {r7.TallyByUnit.GetValueOrDefault("zan")?.VendettaFires} 回 ／ {r7.TallyByUnit.GetValueOrDefault("zan")?.VendettaDealt}");
        Console.WriteLine();
    }

    /// <summary>§4-2: 肩代わりの量（1戦あたり・相手ごと）・ドハが倒れた T・版の見込み。</summary>
    static void ShareAmounts()
    {
        Console.WriteLine("## §4-2 ドハが肩代わりする量（規定・seed 0..49・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("肩代わり ＝ ドハが中継の一撃で実際に失った HP（`SharedAway` の合計）。直接 ＝ ドハが中継以外で受けた量。倒れたT ＝ ドハが倒れた戦の平均（倒れた割合）。"
            + "**見込み**: DH-a ≈ 肩代わり ÷ 2（相手へ）／ DH-b ≈ （肩代わり ＋ 直接）÷ 2 ／ DH-t ≈ ⌊肩代わり ÷ 52⌋（1ターン1回で頭打ち）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 肩代わり（回） | 相手ごと | 直接 | ドハが倒れたT | DH-a ／ DH-b の見込み | DH-t の見込み |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|---|--:|");
        foreach (var (name, f) in Boards())
            foreach (var w in Waves().Where(w => w.Key is "2" or "5" or "guard" or "bat" or "boss"))
            {
                var a = Many(f, w, 50);
                Console.WriteLine($"| {name} | {w.Name} | {a.Win} | {a.P(a.Shoulder):F1}（{a.P(a.ShareHits):F1}） | {Top(a.Partner, a.N)} | {a.P(a.DohaDirect):F1} | "
                    + $"{(a.DohaDeathN == 0 ? "—" : $"{(double)a.DohaDeathT / a.DohaDeathN:F1}")}（{100.0 * a.DohaDeathN / a.N:F0}%） | {a.P(a.Shoulder) / 2:F1} ／ {(a.P(a.Shoulder) + a.P(a.DohaDirect)) / 2:F1} | {a.P(a.Shoulder) / 52:F2} |");
            }
        Console.WriteLine();
    }

    /// <summary>§4-3: 規定のドハの攻撃力が今どれだけ使われているか（ドハの `Attack` のたびの攻撃力 − 素の 4・与ダメ）。</summary>
    static void DohaAttackUse()
    {
        Console.WriteLine("## §4-3 規定のドハの攻撃力の使われ方（seed 0..49・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("手番 ＝ ドハの `TurnsTaken`・振った ＝ ドハの `Attack`・上がった分 ＝ 振ったときの攻撃力（直前の `StatSnapshot`）− 素の 4 の平均・"
            + "使えた分 ＝ 上がった分 × 振った回数（の合計）・与ダメ ＝ ドハが敵に入れた `Damage`（反撃を含む）。最後の列 ＝ その戦でドハの攻撃力が最も上がったとき（`StatSnapshot` の最大 − 4）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 手番 | 振った | 上がった分（振ったとき） | 使えた分 | 与ダメ | 上がった分の最大 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in Boards().Take(1).Concat(Boards().Where(b => b.Name.Contains("雷の型"))))
            foreach (var w in Waves().Where(w => w.Key is "5" or "guard" or "bat" or "boss"))
            {
                long turns = 0, swings = 0, bonusSum = 0, dealt = 0, peak = 0; int n = 50;
                for (int s = 0; s < n; s++)
                {
                    var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    var doha = p.First(u => u.Def.Id == "doha");
                    var mine = p.Select(u => u.InstanceId).ToHashSet();
                    turns += r.TallyByUnit["doha"].TurnsTaken;
                    int atk = doha.Def.Attack, pk = 0;
                    foreach (var x in r.Events)
                    {
                        if (x.Kind == BattleEventKind.StatSnapshot && x.TargetId == doha.InstanceId) { atk = x.Amount; pk = Math.Max(pk, atk - doha.Def.Attack); }
                        else if (x.Kind == BattleEventKind.Attack && x.ActorId == doha.InstanceId) { swings++; bonusSum += atk - doha.Def.Attack; }
                        else if (x.Kind == BattleEventKind.Damage && x.ActorId == doha.InstanceId && x.TargetId is int t && !mine.Contains(t)) dealt += x.Amount;
                    }
                    peak += pk;
                }
                Console.WriteLine($"| {name} | {w.Name} | {(double)turns / n:F2} | {(double)swings / n:F2} | {(swings == 0 ? 0 : (double)bonusSum / swings):F1} | {(double)bonusSum / n:F1} | {(double)dealt / n:F1} | {(double)peak / n:F1} |");
            }
        Console.WriteLine();
    }

    /// <summary>§4-4: DH-a ／ DH-b の配り先が飛ぶ駒（支援を拒む・強化で弱る・強化を横取りする）。</summary>
    static void SupportSkips()
    {
        Console.WriteLine("## §4-4 配り先が飛ぶ駒（`All` の 52 枚）");
        Console.WriteLine();
        var all = UnitCatalog.All.Select(d => (D: d, U: BattleEngine.Materialize(Formation.Build(front1: d), BattleContext.PlayerTeam)[0])).ToList();
        Console.WriteLine("- 支援を拒む（`AcceptsSupport` が偽・DH-a は配らない ／ DH-b ／ DH-t は次の駒へ）: " + string.Join("・", all.Where(x => !x.U.AcceptsSupport).Select(x => x.D.Name)));
        Console.WriteLine("- 強化で弱る（逆しま `Perverse`・配ると攻撃力が半減する）: " + string.Join("・", all.Where(x => x.D.Traits.Contains(TraitId.Perverse)).Select(x => x.D.Name)));
        Console.WriteLine("- 強化を横取りする（横流し `Funnel`・隣の強化を最も遅い味方へ回す）: " + string.Join("・", all.Where(x => x.D.Traits.Contains(TraitId.Funnel)).Select(x => x.D.Name)));
        Console.WriteLine();
        Console.WriteLine("| 台 | 席 | 支援を拒む | 逆しま | 横流し |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (name, f) in Boards())
        {
            var us = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            string L(Func<UnitState, bool> q) { var m = us.Where(q).Select(u => Short(u.Def)).ToList(); return m.Count == 0 ? "—" : string.Join("・", m); }
            Console.WriteLine($"| {name} | {Seats(f)} | {L(u => !u.AcceptsSupport)} | {L(u => u.HasTrait(TraitId.Perverse))} | {L(u => u.HasTrait(TraitId.Funnel))} |");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §5: 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void Rates(int seeds)
    {
        var waves = Waves();
        Console.WriteLine($"# 第297期 代表台 × 波 × ドハの版（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("勝率（%）。本編は `compare` と同じ倍率（115 ／ 115）、試遊の波（近衛 ／ 大隊 ／ ボス規定形）は波ごとの倍率。ドルガ ＝ 規定の台の ドハ → ドルガ（R383 の対照）。"
            + "**判定の分母は第2〜5波**（第1波は参考）。");
        Console.WriteLine();
        foreach (var (name, f) in Boards())
        {
            Console.WriteLine($"## {name}（{Seats(f)}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 規定 | DH-a | DH-b | DH-t | ドルガ | 最大の差（版 − 規定） |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|---|");
            var detail = new List<string>();
            foreach (var w in waves)
            {
                var res = Versions.Select(v => Many(FvSwap(f, UnitCatalog.DohaD0, v.Def), w, seeds)).ToArray();
                var dolga = Many(FvSwap(f, UnitCatalog.DohaD0, UnitCatalog.Dolga), w, seeds);
                double b = 100.0 * res[0].Wins / seeds;
                int best = Enumerable.Range(1, 3).OrderByDescending(i => Math.Abs(100.0 * res[i].Wins / seeds - b)).First();
                double d = 100.0 * res[best].Wins / seeds - b;
                Console.WriteLine($"| {w.Name} | {res[0].Win} | {res[1].Win} | {res[2].Win} | {res[3].Win} | {dolga.Win} | {(Math.Abs(d) < 0.05 ? "±0" : $"{Versions[best].Tag} {d:+0.0;-0.0}")} |");
                foreach (var (a, i) in res.Select((a, i) => (a, i)))
                {
                    string give = i switch
                    {
                        1 or 2 => $"配った {a.P(a.Given):F1}（{a.P(a.Gives):F1} 回）→ {Top(a.Got, a.N, 3)}",
                        3 => $"手番 {a.P(a.Gifts):F2}（動いた {a.P(a.GiftTurns):F2}・攻撃 {a.P(a.GiftAttacks):F2}・1T1回で止まった {a.P(a.GiftCapped):F2}）→ {Top(a.GiftTo, a.N, 3)}",
                        _ => "—",
                    };
                    detail.Add($"| {w.Name} | {Versions[i].Tag} | {a.Win} | {(a.Wins == 0 ? "—" : $"{(double)a.WinT / a.Wins:F1}")} | "
                        + $"{(a.FirstDeathN == 0 ? "—" : $"{(double)a.FirstDeathT / a.FirstDeathN:F1}")}（{100.0 * a.FirstDeathN / a.N:F0}%） | "
                        + $"{(a.DohaDeathN == 0 ? "—" : $"{(double)a.DohaDeathT / a.DohaDeathN:F1}")}（{100.0 * a.DohaDeathN / a.N:F0}%） | {a.P(a.Shoulder):F1} | {a.P(a.Vend):F1} | {give} |");
                }
            }
            Console.WriteLine();
            Console.WriteLine("<details><summary>版ごとの中身（1戦あたり）</summary>");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 勝率 | 決着T（勝ち） | 味方が初めて倒れたT（倒れた割合） | ドハが倒れたT（割合） | 肩代わり | ザンの仇討ち | 配った ／ 手番 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|");
            foreach (var l in detail) Console.WriteLine(l);
            Console.WriteLine();
            Console.WriteLine("</details>");
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    // §5-3: 攻撃力の推移と与ダメ
    // ---------------------------------------------------------------------------------
    static void Atk(string boardPart)
    {
        var (name, f) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        const int n = 50, maxT = 8;
        Console.WriteLine($"# 第297期 攻撃力の推移 —— {name}（{Seats(f)}）× seed 0..{n - 1}（verbose）");
        Console.WriteLine();
        Console.WriteLine("各ターンの頭の攻撃力（`StatSnapshot`・その時点で生きている戦の平均）と、1戦あたりの与ダメ（敵への `Damage`・中継を除く）。");
        Console.WriteLine();
        foreach (var w in Waves().Where(w => w.Key is "5" or "guard" or "bat" or "boss"))
        {
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | 駒 | " + string.Join(" | ", Enumerable.Range(1, maxT).Select(t => $"T{t}")) + " | 与ダメ |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", maxT + 1)));
            foreach (var v in Versions)
            {
                var g = FvSwap(f, UnitCatalog.DohaD0, v.Def);
                var order = g.Occupied().Select(o => o.Def.Id).ToList();
                var nameOf = g.Occupied().ToDictionary(o => o.Def.Id, o => Short(o.Def));
                var sum = order.ToDictionary(id => id, _ => new double[maxT + 1]);
                var cnt = order.ToDictionary(id => id, _ => new int[maxT + 1]);
                var dmg = order.ToDictionary(id => id, _ => 0L);
                for (int s = 0; s < n; s++)
                {
                    var p = BattleEngine.Materialize(g, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    var byInst = p.ToDictionary(u => u.InstanceId, u => u.Def.Id);
                    foreach (var x in r.Events)
                    {
                        if (x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int t && byInst.TryGetValue(t, out var id) && sum.ContainsKey(id) && x.Turn >= 1 && x.Turn <= maxT) { sum[id][x.Turn] += x.Amount; cnt[id][x.Turn]++; }
                        else if (x.Kind == BattleEventKind.Damage && x.ActorId is int a && byInst.TryGetValue(a, out var aid) && dmg.ContainsKey(aid) && x.TargetId is int tg && !byInst.ContainsKey(tg) && !x.Relayed) dmg[aid] += x.Amount;
                    }
                }
                foreach (var id in order)
                    Console.WriteLine($"| {v.Tag} | {nameOf[id]} | " + string.Join(" | ", Enumerable.Range(1, maxT).Select(t => cnt[id][t] == 0 ? "—" : $"{sum[id][t] / cnt[id][t]:F0}")) + $" | {(double)dmg[id] / n:F0} |");
            }
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    // 台本の並び
    // ---------------------------------------------------------------------------------
    static void Memo(string boardPart, string wave, int seed, string ver)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var f = FvSwap(f0, UnitCatalog.DohaD0, Ver(ver));
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# doha297 memo —— {name} × {VerTag(Ver(ver))} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}（`ShareGive` {r.Events.Count(x => x.Kind == BattleEventKind.ShareGive)} 件）");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Kind is not (BattleEventKind.Damage or BattleEventKind.ShareGive or BattleEventKind.Whet or BattleEventKind.Attack or BattleEventKind.Death or BattleEventKind.MarkRally)) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.Reaction ? "  Reaction" : "")}{(x.Relayed ? "  relayed" : "")}{(x.FriendlyFire ? "  ff" : "")}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // §5-2 の格子（固定枠 ドハ ＋ アタッカー1枚 × 探索枠3 × 席）
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] Attackers = { UnitCatalog.Tome, UnitCatalog.Zan, UnitCatalog.Kata, UnitCatalog.Shiga };

    /// <summary>
    /// §5-2 の格子（R386）。固定枠 ドハ ＋ アタッカー1枚（<see cref="Attackers"/>）× 探索枠3（`All` の残り 50 枚から3枚）× 席。
    /// 席は 5! = 120 通りから等間隔に <paramref name="seats"/> 通り（決定的）。<b>規定と版の同じ台を同じ席の集合で回し</b>、それぞれの最良の席の勝率を取る。
    /// 規定か版のどちらかが 50% 以上の台について、勝った側の最良の席で ドハ → ドルガ（R383）も回す。乱数は seed 0..<paramref name="seeds"/>-1。
    /// </summary>
    static void Grid(string waveKey, string ver, int seats, int seeds = 10, string atkFilter = "")
    {
        var w = WaveOf(waveKey);
        var dv = Ver(ver);
        var pool = UnitCatalog.All.Where(d => !ReferenceEquals(d, UnitCatalog.Doha)).ToArray();
        int step = Math.Max(1, 120 / Math.Max(1, seats));
        var seatSet = Perms(5).Where((_, i) => i % step == 0).Take(seats).ToArray();
        var atks = Attackers.Where(a => atkFilter == "" || Short(a).Contains(atkFilter, StringComparison.Ordinal)).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第297期 格子 —— {w.Name} × 規定 ／ {VerTag(dv)}（固定枠 ドハ ＋ アタッカー1枚 × 探索枠3・席 {seatSet.Length} 通り・seed 0..{seeds - 1}）");
        Console.WriteLine();
        int Wins(Formation g) { int c = 0; for (int sd = 0; sd < seeds; sd++) if (BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) c++; return c; }
        Formation Seat(UnitDef[] five, int[] perm) { var g = new Formation(); for (int s = 0; s < 5; s++) g[perm[s]] = five[s]; return g; }
        var rows = new System.Collections.Concurrent.ConcurrentBag<(string Atk, string[] Free, double Base, double Ver, double Dolga, int[] Seat)>();
        foreach (var atk in atks)
        {
            var free = pool.Where(d => !ReferenceEquals(d, atk)).ToArray();
            var combos = new List<UnitDef[]>();
            for (int i = 0; i < free.Length; i++) for (int j = i + 1; j < free.Length; j++) for (int k = j + 1; k < free.Length; k++) combos.Add(new[] { free[i], free[j], free[k] });
            Parallel.ForEach(combos, c =>
            {
                double Best(UnitDef d, out int[] seat)
                {
                    var five = new[] { d, atk, c[0], c[1], c[2] };
                    int best = -1; seat = seatSet[0];
                    foreach (var perm in seatSet) { int x = Wins(Seat(five, perm)); if (x > best) { best = x; seat = perm; } if (best == seeds) break; }
                    return 100.0 * best / seeds;
                }
                double b = Best(UnitCatalog.DohaD0, out var sb), v = Best(dv, out var sv);
                if (Math.Max(b, v) < 50) return;
                var seat = v >= b ? sv : sb;
                double dolga = 100.0 * Wins(Seat(new[] { UnitCatalog.Dolga, atk, c[0], c[1], c[2] }, seat)) / seeds;
                rows.Add((Short(atk), c.Select(Short).ToArray(), b, v, dolga, seat));
            });
        }
        var all = rows.ToList();
        bool Need(double win, double dolga) => win >= 50 && dolga <= win / 2;
        Console.WriteLine($"台の数（規定か版で 50% 以上）{all.Count}・所要 {sw.Elapsed.TotalSeconds:F0} 秒。**ドハが要る台** ＝ その版で 50% 以上 かつ ドハ → ドルガで半分以下（R383）。");
        Console.WriteLine();
        Console.WriteLine($"| アタッカー | 台（規定 50%以上） | 台（{VerTag(dv)} 50%以上） | ドハが要る台（規定 ／ {VerTag(dv)}） | 版 − 規定 の平均（両方の台） | 版が +10pt 以上 ／ −10pt 以下 | 最高（規定 ／ 版） |");
        Console.WriteLine("|---|--:|--:|---|--:|---|---|");
        foreach (var g in all.GroupBy(r => r.Atk))
            Console.WriteLine($"| {g.Key} | {g.Count(r => r.Base >= 50)} | {g.Count(r => r.Ver >= 50)} | {g.Count(r => Need(r.Base, r.Dolga))} ／ {g.Count(r => Need(r.Ver, r.Dolga))} | {g.Average(r => r.Ver - r.Base):+0.0;-0.0} | "
                + $"{g.Count(r => r.Ver - r.Base >= 10)} ／ {g.Count(r => r.Ver - r.Base <= -10)} | {g.Max(r => r.Base):F0} ／ {g.Max(r => r.Ver):F0} |");
        Console.WriteLine();
        string Kinds(IEnumerable<(string Atk, string[] Free, double Base, double Ver, double Dolga, int[] Seat)> rs)
            => string.Join("・", rs.SelectMany(r => r.Free).GroupBy(x => x).OrderByDescending(x => x.Count()).Take(15).Select(x => $"{x.Key} {x.Count()}"));
        Console.WriteLine($"自由枠に入った駒（ドハが要る台・{VerTag(dv)}）: " + Kinds(all.Where(r => Need(r.Ver, r.Dolga))));
        Console.WriteLine();
        Console.WriteLine($"自由枠に入った駒（ドハが要る台・規定）: " + Kinds(all.Where(r => Need(r.Base, r.Dolga))));
        Console.WriteLine();
        Console.WriteLine($"自由枠に入った駒（版が +10pt 以上の台）: " + Kinds(all.Where(r => r.Ver - r.Base >= 10)));
        Console.WriteLine();
        Console.WriteLine("| 規定 | 版 | ドルガ | アタッカー | 自由枠 | 席（ドハ・アタッカー・自由枠の順の枠番号） |");
        Console.WriteLine("|--:|--:|--:|---|---|---|");
        foreach (var r in all.OrderByDescending(r => r.Ver - r.Base).ThenByDescending(r => r.Ver).Take(15))
            Console.WriteLine($"| {r.Base:F0} | {r.Ver:F0} | {r.Dolga:F0} | {r.Atk} | {string.Join("・", r.Free)} | {string.Join(",", r.Seat)} |");
        Console.WriteLine();
        Console.WriteLine("版で下がった台（下位 5）:");
        Console.WriteLine();
        Console.WriteLine("| 規定 | 版 | ドルガ | アタッカー | 自由枠 |");
        Console.WriteLine("|--:|--:|--:|---|---|");
        foreach (var r in all.OrderBy(r => r.Ver - r.Base).Take(5))
            Console.WriteLine($"| {r.Base:F0} | {r.Ver:F0} | {r.Dolga:F0} | {r.Atk} | {string.Join("・", r.Free)} |");
    }

    static IEnumerable<int[]> Perms(int n)
    {
        var a = Enumerable.Range(0, n).ToArray();
        IEnumerable<int[]> Rec(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var x in Rec(k + 1)) yield return x;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Rec(0);
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
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit が見つからない");
    static UnitTally? Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id);
    static readonly FieldInfo GiftQueue = typeof(BattleContext).GetField("_giftQueue", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_giftQueue が見つからない");

    /// <summary>盤面を直に組む（`Run` を通さない・乱数は seed 0）。</summary>
    static BattleContext Ctx(Formation pl, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Dolga), BattleContext.EnemyTeam, EnemyScaleRule.None);
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
        Console.WriteLine("# doha297 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var d0 = UnitCatalog.DohaD0;   // 第298期: 規定のドハが DH-a（なまりなし）になったので、第297期の「規定」は旧（`DohaD0`）に固定
        var vers = new[] { (UnitCatalog.DohaDHa, TraitId.ShareBack), (UnitCatalog.DohaDHb, TraitId.ShareTop), (UnitCatalog.DohaDHt, TraitId.ShareGift) };
        Expect("(a) 版の札 ＝ 規定のドハ ＋ 版の札1枚・数値と手番は規定のまま・版は `All` ／ `Everyone` の外・規定のドハは版の札を持たない",
            vers.All(v => v.Item1.Traits.SequenceEqual(d0.Traits.Append(v.Item2)) && v.Item1.MaxHp == d0.MaxHp && v.Item1.Attack == d0.Attack && v.Item1.Speed == d0.Speed
                && v.Item1.Pattern == d0.Pattern && v.Item1.Id == d0.Id && !UnitCatalog.Everyone.Contains(v.Item1))
            && UnitCatalog.All.Contains(UnitCatalog.Doha) && !UnitCatalog.Everyone.Contains(d0) && !d0.Traits.Any(t => t is TraitId.ShareBack or TraitId.ShareTop or TraitId.ShareGift));
        Expect("(b) 文面: プラスは肩代わりの文 ＋ 版の一文・マイナスは共通の文・フレーバーは規定のまま",
            UnitCatalog.DohaDHa.PlusText.EndsWith("。引き受けた痛みは、その相手の力に変えて返す", StringComparison.Ordinal)
            && UnitCatalog.DohaDHb.PlusText.EndsWith("。引き受けた痛みは、いちばん腕の立つ仲間の力に変えて渡す", StringComparison.Ordinal)
            && UnitCatalog.DohaDHt.PlusText.EndsWith("。痛みが積もるたび、いちばん腕の立つ仲間を先に行かせる", StringComparison.Ordinal)
            && vers.All(v => v.Item1.PlusText.StartsWith("味方が受けるダメージの4割を肩代わりする（薙ぎでも全体でも効く・味方の破片が受け止めた残りから取る）。", StringComparison.Ordinal)
                && d0.PlusText.StartsWith(v.Item1.PlusText[..v.Item1.PlusText.IndexOf('。')], StringComparison.Ordinal)
                && v.Item1.MinusText == "自分は強くならず、味方が多いほど早く尽きる" && v.Item1.Flavor == d0.Flavor));

        // (c)〜(j) 盤面を直に組んで1発ずつ: 前1 ドルガ（札なし）／ 前3 ミサ ／ 中央 ドハ ／ 後1 カタ ／ 後3 ガルド（支援を拒む）。ゴルム（巨躯・吐き戻し）のような中継と強化を持つ駒は置かない
        Formation Board(UnitDef dv) => Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Tome, center: dv, back1: UnitCatalog.Kata, back3: UnitCatalog.Gald);
        var res = new List<(string Tag, int DohaLoss, int GolmLoss, int DohaAtk, int GolmAtk, int TopAtk, string Top, int Given, int Whet)>();
        foreach (var (tag, dv) in Versions)
        {
            var ctx = Ctx(Board(dv), out var p, out var e);
            var golm = p.First(u => u.Def.Id == "dolga"); var doha = p.First(u => u.Def.Id == "doha");
            var top = ctx.ShareTopAlly(doha)!;
            int g0 = golm.Hp, h0 = doha.Hp, ga0 = golm.AtkBonus, ta0 = top.AtkBonus;
            ctx.ApplyDamage(golm, 30, e[0]);
            res.Add((tag, h0 - doha.Hp, g0 - golm.Hp, doha.AtkBonus, golm.AtkBonus - ga0, top.AtkBonus - ta0, Short(top.Def),
                (int)(Tal(ctx, "doha")?.ShareGiven ?? 0), ctx.WhetByRoute[(int)WhetRoute.Share]));
        }
        var r0 = res[0];
        Expect("(c) 肩代わりの量はどの版も規定と同じ（ドルガへ 30・ドハが失う HP ／ ドルガが失う HP）",
            res.All(x => x.DohaLoss == r0.DohaLoss && x.GolmLoss == r0.GolmLoss) && r0.DohaLoss > 0,
            string.Join(" ／ ", res.Select(x => $"{x.Tag} {x.DohaLoss}・{x.GolmLoss}")));
        Expect("(d) 規定はドハ自身の攻撃力が上がる・版はどれも上がらない（`AtkBonus` 0）",
            r0.DohaAtk > 0 && res.Skip(1).All(x => x.DohaAtk == 0), string.Join(" ／ ", res.Select(x => $"{x.Tag} {x.DohaAtk}")));
        var ra = res[1]; var rb = res[2]; var rt = res[3];
        int gain = Math.Max(1, r0.DohaLoss / SharerTrait.DamagePerGain), dull = (30 * SharerTrait.Percent / 100) / SharerTrait.DullDivisor;
        Expect("(e) DH-a は肩代わりした相手（ドルガ）にだけ配る（ドルガの攻撃力 ＝ +痛み÷2 − なまり）・`Whet`（経路「分かち」）を通る",
            ra.GolmAtk == gain - dull && ra.Given == gain && ra.Whet == gain && r0.GolmAtk == -dull, $"ドルガ {ra.GolmAtk}（+{gain} − {dull}）・`Whet` 分かち {ra.Whet}");
        Expect("(f) DH-b は攻撃力が最も高い味方（ドハ以外・支援を拒むガルドを飛ばす）に配る・`Whet` を通る",
            rb.Top == TopExpected() && rb.TopAtk == gain - (rb.Top == "ドルガ" ? dull : 0) && rb.Whet == gain && rb.Given == gain, $"配り先 {rb.Top} +{rb.TopAtk}（攻撃力: {string.Join("・", BattleEngine.Materialize(Board(d0), BattleContext.PlayerTeam).Select(u => $"{Short(u.Def)} {u.CurrentAttack}"))}）");
        string TopExpected() => Short(BattleEngine.Materialize(Board(d0), BattleContext.PlayerTeam).Where(u => u.Def.Id != "doha" && u.AcceptsSupport).OrderByDescending(u => u.CurrentAttack).ThenBy(u => u.Slot).First().Def);
        {
            // DH-a: 自分への直接の一撃の分は配らない ／ DH-b: 配る
            var ca = Ctx(Board(UnitCatalog.DohaDHa), out var pa, out var ea); ca.ApplyDamage(pa.First(u => u.Def.Id == "doha"), 20, ea[0]);
            var cb = Ctx(Board(UnitCatalog.DohaDHb), out var pb, out var eb); cb.ApplyDamage(pb.First(u => u.Def.Id == "doha"), 20, eb[0]);
            long ga = Tal(ca, "doha")?.ShareGiven ?? 0, gb = Tal(cb, "doha")?.ShareGiven ?? 0;
            Expect("(g) ドハへの直接の一撃: DH-a は配らない ／ DH-b は配る（20 → +10）", ga == 0 && gb == 10, $"{ga} ／ {gb}");
            // 支援を拒む相手には DH-a は配らない（ガルドへの一撃）
            var cg = Ctx(Board(UnitCatalog.DohaDHa), out var pg, out var eg); var gald = pg.First(u => u.Def.Id == "gald"); int gb0 = gald.AtkBonus;
            cg.ApplyDamage(gald, 30, eg[0]);
            Expect("(h) DH-a: 支援を拒む相手（ガルド）を肩代わりしても配らない（`Whet` 分かち 0・ガルドの攻撃力はなまりの分だけ下がる）",
                (Tal(cg, "doha")?.ShareTakenHits ?? 0) > 0 && (Tal(cg, "doha")?.ShareGiven ?? 0) == 0 && cg.WhetByRoute[(int)WhetRoute.Share] == 0 && gald.AtkBonus <= gb0,
                $"肩代わり {Tal(cg, "doha")?.ShareTakenHits} 回・ガルドの攻撃力 {gb0} → {gald.AtkBonus}");
            // DH-b: 支援を拒む駒（ガルド）が攻撃力で最上位でも飛ばして次（ドルガ）へ配る
            var cs = Ctx(Board(UnitCatalog.DohaDHb), out var ps, out var es); var gald2 = ps.First(u => u.Def.Id == "gald"); gald2.AtkBonus = 100;
            var dolga2 = ps.First(u => u.Def.Id == "dolga"); int da0 = dolga2.AtkBonus;
            cs.ApplyDamage(ps.First(u => u.Def.Id == "tome"), 30, es[0]);
            Expect("(h2) DH-b: 支援を拒む駒（ガルド・攻撃力を 109 にした）を飛ばして次に高い駒（ドルガ）へ配る",
                gald2.AtkBonus == 100 && dolga2.AtkBonus > da0 && cs.WhetByRoute[(int)WhetRoute.Share] == dolga2.AtkBonus - da0, $"ドルガ +{dolga2.AtkBonus - da0}");
        }
        {
            // DH-t: 1ターンに1回・端数の持ち越し
            var ct = Ctx(Board(UnitCatalog.DohaDHt), out var pt, out var et);
            var golm = pt.First(u => u.Def.Id == "dolga"); var doha = pt.First(u => u.Def.Id == "doha");
            var q = (System.Collections.ICollection)GiftQueue.GetValue(ct)!;
            int every = SharerTrait.GiftEvery(doha);
            // ドルガへ 100 を2回（4割 ＝ 40 ずつ・ドハの累計 80 → 52 で1回）→ 同じターンにもう 100（累計 28 + 40 = 68 ≥ 52 だが1ターン1回で止まる）。HP は毎回戻す
            ct.ApplyDamage(golm, 100, et[0]); golm.Hp = golm.MaxHp; doha.Hp = doha.MaxHp;
            ct.ApplyDamage(golm, 100, et[0]); golm.Hp = golm.MaxHp; doha.Hp = doha.MaxHp;
            int q1 = q.Count, pool1 = doha.RawCounter(SharerTrait.GiftPoolKey);
            ct.ApplyDamage(golm, 100, et[0]); golm.Hp = golm.MaxHp; doha.Hp = doha.MaxHp;
            int q2 = q.Count, pool2 = doha.RawCounter(SharerTrait.GiftPoolKey);
            long capped = Tal(ct, "doha")?.ShareGiftCapped ?? 0;
            TurnProp.SetValue(ct, 2);
            ct.ApplyDamage(golm, 100, et[0]); golm.Hp = golm.MaxHp; doha.Hp = doha.MaxHp;
            int q3 = q.Count, pool3 = doha.RawCounter(SharerTrait.GiftPoolKey);
            Expect($"(i) DH-t: 累計が {every} に達して1回・同じターンは1回まで（止まった回は累計を減らさない）・次のターンに端数ごと撃つ",
                every == 52 && q1 == 1 && pool1 == 80 - 52 && q2 == 1 && pool2 == 28 + 40 && capped == 1 && q3 == 2 && pool3 == 68 + 40 - 52 && doha.AtkBonus == 0,
                $"控え {q1} → {q2} → {q3}・累計 {pool1} → {pool2} → {pool3}・止まった {capped}");
            Expect("(j) DH-t は攻撃力を配らない（`Whet` 分かち 0）", ct.WhetByRoute[(int)WhetRoute.Share] == 0);
        }
        // (k) `ctx.PickOne` を新たに使っていない（BattleCore の呼び出しの数 ＝ 第296期の数）
        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(k) `PickOne(` の出現数が第296期と同じ（BattleCore）", pick == PickOne296, $"{pick}（第296期 {PickOne296}）");

        // (l) 段0: `MarkRally` の回復(与) がヒサに入る（守り型 × 近衛 × seed 7）・ザンは 0
        var guard = WaveOf("guard");
        var r7 = BattleEngine.Run(BattleEngine.Materialize(FvSwap(Playtest("試遊・標 守り型"), UnitCatalog.Doha, d0), BattleContext.PlayerTeam), guard.Make(), 7, verbose: false);
        var hisa = r7.TallyByUnit["hisa"]; var zan = r7.TallyByUnit["zan"];
        Expect("(l) 段0: 守り型 × 近衛 × seed 7 の回復(与) がヒサに入る（＝ 叫びの癒えた量）・ザンは 0",
            hisa.HealOutInTurn + hisa.HealOutOffTurn == hisa.RallyHealed && hisa.RallyHealed > 0 && zan.HealOutInTurn + zan.HealOutOffTurn == 0,
            $"ヒサ {hisa.HealOutInTurn + hisa.HealOutOffTurn}（叫び {hisa.RallyHealed}）・ザン {zan.HealOutInTurn + zan.HealOutOffTurn}");

        // (m) verbose の有無で勝敗・決着T が同じ（版 × 守り型・雷の型 × 試遊の波 × seed 0..19）
        int diff = 0, n = 0;
        foreach (var (tag, dv) in Versions.Skip(1))
            foreach (var bn in new[] { "試遊・標 守り型", "試遊・感電 雷の型" })
                foreach (var w in Waves().Where(w => w.Key is "guard" or "bat" or "boss"))
                    for (int s = 0; s < 20; s++)
                    {
                        var g = FvSwap(Playtest(bn), UnitCatalog.Doha, dv);
                        var a = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                        var b = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                        n++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                    }
        Expect("(m) verbose の有無で勝敗・決着T が同じ（版3つ × 守り型・雷の型 × 試遊の波 × seed 0..19）", diff == 0, $"{n} 戦・違い {diff}");

        // (n) 表示専用の出来事: 版で `ShareGive` が出る（DH-a ／ DH-b は「力」・DH-t は「手番」と「手番の頭」）・規定では 0
        long Ev(UnitDef dv, string label) { long c = 0; for (int s = 0; s < 10; s++) c += BattleEngine.Run(BattleEngine.Materialize(FvSwap(Playtest("試遊・標 守り型"), UnitCatalog.Doha, dv), BattleContext.PlayerTeam), WaveOf("boss").Make(), s, verbose: true).Events.Count(x => x.Kind == BattleEventKind.ShareGive && x.Text == label); return c; }
        long e0 = Ev(d0, ShareGiveLabels.Power) + Ev(d0, ShareGiveLabels.Gift), ea2 = Ev(UnitCatalog.DohaDHa, ShareGiveLabels.Power), eb2 = Ev(UnitCatalog.DohaDHb, ShareGiveLabels.Power),
             et1 = Ev(UnitCatalog.DohaDHt, ShareGiveLabels.Gift), et2 = Ev(UnitCatalog.DohaDHt, ShareGiveLabels.GiftTurn);
        Expect("(n) `ShareGive`: 規定 0・DH-a ／ DH-b に「力」・DH-t に「手番」と「手番の頭」（守り型 × ボス × seed 0..9）",
            e0 == 0 && ea2 > 0 && eb2 > 0 && et1 > 0 && et2 > 0 && et2 <= et1, $"{e0} ／ {ea2} ／ {eb2} ／ {et1}・{et2}");

        // (o) `compare` の行・交差帯の行の数は第296期のまま（版の駒は `Presets` に入らない）
        Expect("(o) `compare` 64 行・交差帯 12 行・試遊 8 行・どの行にも版のドハがいない",
            Presets.Compare.Length == 64 && Presets.Cross.Length == 12 && Presets.Playtest.Length == 8
            && Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).All(r => r.F.Occupied().All(o => !vers.Any(v => ReferenceEquals(v.Item1, o.Def)))));

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }

    /// <summary>第296期（HEAD 48ba660 〜 7195d67）の BattleCore の `PickOne(` の出現数。</summary>
    const int PickOne296 = 36;
}
