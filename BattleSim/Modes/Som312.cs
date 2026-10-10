using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S309 = Som309Diag;
using S310 = Som310Diag;

// =====================================================================================
// som312 —— 第312期「ソムの重ね（緊急 K-a ＋ 群れ ＋ 萎縮）の規定化 ＋ K-a のベニ対策 ＋ 試遊の台」。
// 指示書は design/PHASE312_SOM_REGULATE2_SPEC.md ／ 報告は design/PHASE312_SOM_REGULATE2.md。
//
//     dotnet run --project BattleSim -c Release 0 som312 beni        # §3: 火の型 ソラ→ソム（ベニのいる台）× 10 波・緊急 ／ 集めた ／ 集めなかった（反転の内側）
//     dotnet run --project BattleSim -c Release 0 som312 playtest    # §4: 試遊の感電の台（7 台）× ボス ／ 近衛 ／ 大隊 ／ 九体 ／ 本編の5波・勝率と倒しT
//     dotnet run --project BattleSim -c Release 0 som312 find        # §5: ベニ対策で集めなかった連鎖のある seed を探す（台本の例）
//     dotnet run --project BattleSim -c Release 0 som312 memo <台の一部> <boss|guard|bat|nine|nine2|1..5> <seed> <T0> <T1>
//     dotnet run --project BattleSim -c Release 0 som312 check       # 自己検査
//
// 段0-1（ベニ対策の前）の数は、ベニ対策の1行だけを外した build で同じモードを回して並べる（報告書 §3）。
// **予測のファイルが無ければ本測定（beni ／ playtest）を走らせない**（R397・`.tmp/p312/predict.md`）。
// =====================================================================================
static class Som312Diag
{
    const string PredictFile = ".tmp/p312/predict.md";
    const int Seeds = S307.Seeds;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "beni" or "playtest" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"som312: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "beni": Beni(); return;
            case "playtest": PlaytestAll(); return;
            case "find": Find(); return;
            case "memo": Memo(A(3, "火の型"), A(4, "guard"), int.Parse(A(5, "0")), int.Parse(A(6, "1")), int.Parse(A(7, "5"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("som312: モードは beni / playtest / find / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 台・波
    // ---------------------------------------------------------------------------------
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);

    /// <summary>ボス ／ 近衛 ／ 大隊 ／ 九体 新兵 ／ 九体 農兵 ／ 本編の第1〜5波（第310期の `som310` と同じ並び）。</summary>
    static readonly S287.Wave[] Waves = S310.Waves;
    static S287.Wave WaveOf(string n) => n switch { "nine" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };

    internal const string Whip = "試遊・感電 光の盾 鞭", WhipHeavy = "試遊・感電 光の盾 重 鞭";
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    /// <summary>§3 の台: 試遊・感電 火の型 の ソラ → ソム（第311期の代表台と同じ・ベニのいる台）。規定の駒。</summary>
    static Formation FireSom() => S309.Swap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, UnitCatalog.Som);
    static (string Name, Formation F)[] ShockRows() => Presets.Playtest.Where(r => r.Name.StartsWith("試遊・感電", StringComparison.Ordinal)).ToArray();

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class St
    {
        public long N, Wins, WinT, Caps, Fired, FocusInverted, FocusHealed, FocusVeil, AllyInverted;
        public void Add(St o) { N += o.N; Wins += o.Wins; WinT += o.WinT; Caps += o.Caps; Fired += o.Fired; FocusInverted += o.FocusInverted; FocusHealed += o.FocusHealed; FocusVeil += o.FocusVeil; AllyInverted += o.AllyInverted; }
        public string Win => F1(N == 0 ? 0 : 100.0 * Wins / N);
        public string WinTurn => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string Per(long x) => F1(N == 0 ? 0 : (double)x / N);
    }

    static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new St { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else if (r.Turns >= BattleEngine.MaxTurns) a.Caps = 1;
        if (r.TallyByUnit.TryGetValue("som", out var t))
        { a.Fired = t.EmergFired; a.FocusInverted = t.SparkFocusInverted; a.FocusHealed = t.EmergFocusHealed; a.FocusVeil = t.EmergFocusVeil; a.AllyInverted = t.InverseLeakBy; }
        return a;
    }

    static St Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new St();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // §3 ベニ対策
    // ---------------------------------------------------------------------------------
    static void Beni()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var f = FireSom();
        Console.WriteLine($"# 第312期 §3 K-a のベニ対策 —— 火の型 ソラ→ソム（{Seats(f)}）× 10 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("規定の駒（この build の規定）。段0-1 の列は、ベニ対策の1行を外した build で同じモードを回した出力を並べる（報告書 §3）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 倒しT | 30T 上限の負け | 緊急 喚んだ /戦 | 集めなかった（反転の内側）/戦 | 集めた HP ／ 衣 /戦 | ソムの回復が反転した傷 /戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|--:|");
        foreach (var w in Waves)
        {
            var a = Measure(f, w);
            Console.WriteLine($"| {w.Name} | {a.Win} | {a.WinTurn} | {a.Caps} | {a.Per(a.Fired)} | {a.Per(a.FocusInverted)} | {a.Per(a.FocusHealed)} ／ {a.Per(a.FocusVeil)} | {a.Per(a.AllyInverted)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §4 試遊の感電の台
    // ---------------------------------------------------------------------------------
    static void PlaytestAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = ShockRows();
        Console.WriteLine($"# 第312期 §4 試遊の感電の台（{rows.Length} 台）× 10 波・seed 0..{Seeds - 1}・段0 の後の規定");
        Console.WriteLine();
        Console.WriteLine("セル ＝ 勝率（倒しT ＝ 勝った戦の平均ターン）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席（前1・前3・中央・後1・後3） | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var (name, f) in rows)
        {
            var cells = Waves.Select(w => Measure(f, w)).ToArray();
            Console.WriteLine($"| {name.Replace("試遊・感電 ", "")} | {Seats(f)} | " + string.Join(" | ", cells.Select(c => $"{c.Win}（{c.WinTurn}）")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5 台本の例
    // ---------------------------------------------------------------------------------
    static void Find()
    {
        var f = FireSom();
        Console.WriteLine("# som312 find —— 火の型 ソラ→ソム で、緊急の光を集めずに全員に降らせた連鎖（`Spark 降る` の `TargetId` なし ＋ `PartnerId`）");
        Console.WriteLine();
        foreach (var wn in new[] { "guard", "bat", "boss" })
        {
            var w = WaveOf(wn);
            for (int sd = 0, found = 0; sd < 40 && found < 5; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                var hits = r.Events.Where(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Release && x.TargetId is null && x.PartnerId is int).Select(x => x.Turn).ToArray();
                if (hits.Length > 0 && ++found > 0) Console.WriteLine($"- {w.Name} × seed {sd}: T{string.Join(", T", hits)}（{(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}）");
            }
        }
    }

    static void Memo(string part, string wave, int seed, int t0, int t1)
    {
        var (name, f) = part.Contains("火の型", StringComparison.Ordinal) ? ("火の型 ソラ→ソム", FireSom()) : ShockRows().First(r => r.Name.Contains(part, StringComparison.Ordinal));
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "背いた獣");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# som312 memo —— {name}（{Seats(f)}）× {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・T{t0}〜T{t1}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        for (int i = 0; i < r.Events.Count; i++)
        {
            var x = r.Events[i];
            if (x.Turn < t0 || x.Turn > t1) continue;
            if (x.Kind is BattleEventKind.StatSnapshot or BattleEventKind.StatusSnapshot) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"#{i,-4} {x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  hp={x.HpAfter}  Slot {x.Slot}{(x.PartnerId is int pp ? $"  partner {N(pp)}" : "")}{(x.StatusRemaining is int sr ? $"  rem {sr}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}");
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

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation f, List<UnitState> enemy)
    {
        var ctx = new BattleContext(0, true);
        var add = typeof(BattleContext).GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        foreach (var u in p.Concat(enemy)) add.Invoke(ctx, new object[] { u });
        foreach (var u in ctx.AllUnits) u.SetCounter(StatusKeys.IdleTurn, 99);
        return (ctx, p, enemy);
    }

    static UnitTally T(BattleContext ctx, UnitState u)
    {
        var d = (Dictionary<string, UnitTally>)typeof(BattleContext).GetProperty("TallyByUnit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ctx)!;
        return d.TryGetValue(u.Def.Id, out var t) ? t : new UnitTally();
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som312 check —— 第312期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef som = UnitCatalog.Som, kata = UnitCatalog.Kata, tou = UnitCatalog.Tou;

        // (a) 段0-1: 規定のソム ／ カタ ／ トウ ＝ 第311期の重ねの駒・旧は H311（`Everyone` の外）
        {
            bool same = som.Traits.SequenceEqual(UnitCatalog.SomSWKa.Traits) && kata.Traits.SequenceEqual(UnitCatalog.KataDT.Traits) && tou.Traits.SequenceEqual(UnitCatalog.TouDT.Traits);
            bool stats = new[] { (som, UnitCatalog.SomSWKa), (kata, UnitCatalog.KataDT), (tou, UnitCatalog.TouDT) }
                .All(x => x.Item1.Id == x.Item2.Id && x.Item1.Name == x.Item2.Name && x.Item1.MaxHp == x.Item2.MaxHp && x.Item1.Attack == x.Item2.Attack && x.Item1.Speed == x.Item2.Speed
                          && x.Item1.Advances == x.Item2.Advances && x.Item1.Pattern == x.Item2.Pattern && ReferenceEquals(x.Item1.Actions, x.Item2.Actions) && x.Item1.Flavor == x.Item2.Flavor && x.Item1.MinusText == x.Item2.MinusText);
            bool old = UnitCatalog.SomH311.Traits.SequenceEqual(UnitCatalog.SomE2.Traits) && UnitCatalog.SomH311.PlusText == UnitCatalog.SomE2.PlusText
                && UnitCatalog.KataH311.Traits.SequenceEqual(UnitCatalog.KataKRb.Traits.Append(TraitId.ThundercloudUncapped)) && ReferenceEquals(UnitCatalog.KataKRinf, UnitCatalog.KataH311)
                && UnitCatalog.TouH311.Traits.SequenceEqual(new[] { TraitId.ChargedPowder, TraitId.ChargedPowderLeak, TraitId.ShockStunHalf, TraitId.ChargedPowderSpread });
            bool roster = UnitCatalog.All.Contains(som) && UnitCatalog.All.Contains(kata) && UnitCatalog.All.Contains(tou) && UnitCatalog.All.Count == 52
                && new[] { UnitCatalog.SomH311, UnitCatalog.KataH311, UnitCatalog.TouH311 }.All(d => !UnitCatalog.Everyone.Contains(d));
            bool text = som.PlusText == UnitCatalog.SomH311.PlusText + UnitCatalog.SomSwarmText && kata.PlusText == UnitCatalog.KataH311.PlusText + UnitCatalog.ShockDauntText
                && tou.PlusText == UnitCatalog.TouH311.PlusText + UnitCatalog.ShockDauntText && som.Name == "背かれのソム";
            Expect("(a) 段0-1: 規定のソム ／ カタ ／ トウ ＝ 第311期の重ねの駒（`SomSWKa` ／ `KataDT` ／ `TouDT` と札・数値・行動が同じ）・旧は `SomH311` ／ `KataH311` ／ `TouH311`（`Everyone` の外）・ロスター 52 枚・文面は叩き台",
                same && stats && old && roster && text, $"札 {same}・数値 {stats}・旧 {old}・ロスター {roster}・文面 {text}");
        }
        // (b) 段0-2: 集める先が反転の内側なら全員に降る・内側でなければ集める（盤を直に組む）
        {
            // 火の型 ソラ→ソムの駒を、ベニの隣でない味方ができる席に並べ替えた盤（シガ・トウ・ガルド・ベニ・ソム——ベニは後1・隣は前1 ／ 中央）。
            // ベニの隣の味方 ／ 隣でない味方 を1体ずつ選び、4 割を切らせて緊急を起こす。
            var f = Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Tou, center: UnitCatalog.Gald, back1: UnitCatalog.Beni, back3: UnitCatalog.Som);
            string Run1(bool inside, out long inv, out long healedOthers, out int? target, out int? partner, out long focusHp)
            {
                var (ctx, p, e) = Board(f, BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None));
                var s = p.First(u => u.Def.Id == "som");
                var beni = p.First(u => u.Def.Id == "beni");
                var ally = p.Where(u => u != s && u != beni && u.Def.Id != "gald").FirstOrDefault(u => ctx.HealInverts(u) == inside)
                           ?? p.Where(u => u != s).First(u => ctx.HealInverts(u) == inside);
                foreach (var u in p) if (u != ally) u.Hp = u.MaxHp / 2;   // ほかの味方も傷ついている（全員に降れば Heal が出る）
                int n0 = ctx.Events.Count;
                ctx.ApplyDamage(ally, ally.Hp - ally.MaxHp * 25 / 100, e[0]);   // 4 割を切らせる
                var ev = ctx.Events.Skip(n0).ToList();
                var rel = ev.FirstOrDefault(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Release);
                target = rel?.TargetId; partner = rel?.PartnerId;
                int ia = ev.IndexOf(rel!);
                healedOthers = ia < 0 ? 0 : ev.Skip(ia).Count(x => x.Kind == BattleEventKind.Heal && x.TargetId != ally.InstanceId);
                var t = T(ctx, s);
                inv = t.SparkFocusInverted; focusHp = t.EmergFocusHealed + t.EmergFocusVeil;
                return $"{Short(ally.Def)}（反転 {(ctx.HealInverts(ally) ? "内" : "外")}）喚んだ {t.EmergFired}";
            }
            string a1 = Run1(true, out long inv1, out long oth1, out int? tg1, out int? pt1, out long fh1);
            string a2 = Run1(false, out long inv2, out long oth2, out int? tg2, out int? pt2, out long fh2);
            bool ok = inv1 == 1 && tg1 is null && pt1 is int && oth1 > 0 && fh1 == 0
                   && inv2 == 0 && tg2 is int && pt2 is null && oth2 == 0 && fh2 > 0;
            Expect("(b) 段0-2: 緊急の光を集める先がベニの結界の内側なら集めずに全員に降る（`Spark 降る` の `TargetId` なし ＋ `PartnerId` ＝ 危なかった味方）・外側なら集める（盤を直に組む）", ok,
                $"内側 {a1}: 集めなかった {inv1}・ほかの味方への Heal {oth1}・集めた {fh1}／外側 {a2}: 集めなかった {inv2}・ほかの味方への Heal {oth2}・集めた {fh2}");
        }
        // (c) 段0-2 は実戦でも効き、ベニのいない台では動かない
        {
            long fireInv = 0, fireFocusedInverted = 0, shieldInv = 0;
            var fire = FireSom();
            for (int sd = 0; sd < 40; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(fire, BattleContext.PlayerTeam), Waves[1].Make(), sd, verbose: true);
                fireInv += r.TallyByUnit["som"].SparkFocusInverted;
                // 集めた降る光（`TargetId` あり）の直後に、その味方への `HealInverted` が出ない
                var ev = r.Events;
                for (int i = 0; i < ev.Count; i++)
                {
                    if (ev[i].Kind != BattleEventKind.Spark || ev[i].Text != SparkLabels.Release || ev[i].TargetId is not int tg) continue;
                    for (int j = i + 1; j < ev.Count && ev[j].Kind is BattleEventKind.Heal or BattleEventKind.Spark or BattleEventKind.HealInverted or BattleEventKind.Damage; j++)
                        if (ev[j].Kind == BattleEventKind.HealInverted && ev[j].TargetId == tg) fireFocusedInverted++;
                }
                foreach (var (_, f) in ShockRows().Where(r2 => !r2.F.Occupied().Any(o => o.Def.Id == "beni")))
                    shieldInv += BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Waves[1].Make(), sd, verbose: false).TallyByUnit.GetValueOrDefault("som")?.SparkFocusInverted ?? 0;
            }
            Expect("(c) 段0-2 は実戦で効く（火の型 ソラ→ソム × 近衛で集めなかった連鎖がある・集めた光は反転しない）・ベニのいない試遊の台では 0", fireInv > 0 && fireFocusedInverted == 0 && shieldInv == 0,
                $"火の型 × 近衛 × seed 0..39: 集めなかった {fireInv}・集めて反転 {fireFocusedInverted}／ベニのいない台: 集めなかった {shieldInv}");
        }
        // (d) 試遊の2台・`compare` の行数
        {
            var pl = Presets.Playtest;
            var shield = Playtest(S309.Shield); var heavy = Playtest(S309.ShieldHeavy);
            bool SameBut(Formation a, Formation b) => Enumerable.Range(0, 5).All(i => ReferenceEquals(a[i], b[i]) || ReferenceEquals(a[i], UnitCatalog.Kubi) && ReferenceEquals(b[i], UnitCatalog.Shiga));
            bool ok = pl.Length == 12 && pl.Take(10).Select(r => r.Name).SequenceEqual(S309.Playtest308.Concat(new[] { S309.Shield, S309.ShieldHeavy }))
                && pl.Skip(10).Select(r => r.Name).SequenceEqual(new[] { Whip, WhipHeavy })
                && SameBut(shield, Playtest(Whip)) && SameBut(heavy, Playtest(WhipHeavy)) && Playtest(Whip).Occupied().Count() == 5 && Playtest(WhipHeavy).Occupied().Count() == 5
                && pl.Skip(10).All(r => r.F.Shape == FormationShape.X && !r.F.HasRelics && r.F.Occupied().All(o => o.Slot < 5 && UnitCatalog.All.Contains(o.Def)) && !r.F.Occupied().Any(o => o.Def.Id == "kubi"))
                && Presets.Compare.Length == 64 && CompareBuilds().Count() == 64 && Presets.Cross.Length == 12 && pl.All(r => !Presets.Compare.Any(c => c.Name == r.Name));
            Expect("(d) 試遊の2台（光の盾 ／ 重 の クビ → シガ・同じ席）が `Presets.Playtest` の末尾・`compare` は 64 行のまま", ok, $"試遊 {pl.Length} 行・末尾 {string.Join(" ／ ", pl.Skip(10).Select(r => Seats(r.F)))}");
        }
        // (e) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var (_, f) in ShockRows().Append(("火の型 ソラ→ソム", FireSom())))
                foreach (var w in Waves)
                    for (int sd = 0; sd < 2; sd++)
                    {
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        n++;
                        if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                    }
            Expect("(e) 決定的・verbose の有無で勝敗と決着T が変わらない（試遊の感電 7 台 ＋ 火の型 ソラ→ソム × 10 波 × seed 0..1）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (f) 乱数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 76fc222:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var pr = System.Diagnostics.Process.Start(psi)!;
                    string o = pr.StandardOutput.ReadToEnd();
                    pr.WaitForExit();
                    return pr.ExitCode == 0 ? o : null;
                }
                catch { return null; }
            }
            var notes = new List<string>(); bool clean = true;
            foreach (var path in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
            {
                string now = File.ReadAllText(path); string? head = Head(path);
                if (head is null) { clean = false; notes.Add($"{path}: 第311期の版を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(f) `PickOne(` ／ `Roll(` を新たに使っていない（第311期の後のコミット 76fc222 以下）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
