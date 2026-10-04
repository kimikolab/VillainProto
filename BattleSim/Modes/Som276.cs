using System.Security.Cryptography;
using System.Text;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using CW = CheckWaveDiag;

// =====================================================================================
// som276 —— 第276期「ソムの転生（感電を帯びた裏切りの召喚）」。
// 指示書は design/PHASE276_SOM_REBIRTH_SPEC.md ／ 報告は design/PHASE276_SOM_REBIRTH.md。
//
//     dotnet run --project BattleSim -c Release 0 som276 run        # 検証行 A ／ B × 版 S0 ／ S1 ／ S2 × 本編第2〜5波・ボス規定形・チェック波（B3-桁 ／ W3-割合）× seed 0..199
//     dotnet run --project BattleSim -c Release 0 som276 digest     # 旧ソム（背かれ）を含む戦の台本の指紋（背かれの本体を切り出す前後で一致が門）
//     dotnet run --project BattleSim -c Release 0 som276 bands      # 感電の行（`compare` 62 行目）の第2〜5波を 200 seed の帯ごとに（参考）
//     dotnet run --project BattleSim -c Release 0 som276 log <行 A|B> <版 S0|S1|S2> <波 2..5|ボス|B3|W3> [seed]   # 1戦のログ
// =====================================================================================
static class Som276Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "run";
        switch (mode)
        {
            case "digest": Digest(); return;
            case "run": RunAll(); return;
            case "bands": Bands(); return;
            case "log": LogOne(args.Length > 3 ? args[3] : "A", args.Length > 4 ? args[4] : "S1", args.Length > 5 ? args[5] : "3", args.Length > 6 ? int.Parse(args[6]) : 0); return;
            default: Console.WriteLine("som276: モードは run / digest / log。"); return;
        }
    }

    const int Seeds = 200;

    internal static readonly (string Name, string What, UnitDef Som)[] Vers =
    {
        ("S0", "旧ソム（背かれのみ・対照）", UnitCatalog.SomS0),
        ("S1", "喚ばれた餌が感電を帯びて立つ", UnitCatalog.SomS1),
        ("S1x", "S1 の作り直し: 纏った餌にはカタの雷が落ちない（帯びた敵を選ぶ経路と跳ね先から外す）", UnitCatalog.SomS1x),
        ("S1p", "S1 の作り直し: 纏った餌は雷でも感電が弾ける", UnitCatalog.SomS1p),
        ("S2", "S1 ＋ 餌が立ったとき隣り合う敵すべてにも感電を移す", UnitCatalog.SomS2),
        ("S2′", "対照: 餌は感電せず、立ったとき隣り合う敵すべてに感電を付ける（S2 から餌の感電だけを抜いた）", UnitCatalog.SomS2N),
    };

    static Formation RowOf(string row, UnitDef som) => row == "A" ? RowA(som) : RowB(som);

    /// <summary>波。本編は `compare` と同じ口（倍率は採用値）、ボス規定形とチェック波は倍率なし（各器具と同じ）。</summary>
    static readonly (string Name, string Group, Func<List<UnitState>> Make)[] Waves = BuildWaves();
    static (string, string, Func<List<UnitState>>)[] BuildWaves()
    {
        var l = new List<(string, string, Func<List<UnitState>>)>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
        {
            int ww = w;
            l.Add(($"第{w + 1}波", "本編", () => BattleEngine.Materialize(EnemyCatalog.Stages[ww].Enemy, BattleContext.EnemyTeam)));
        }
        l.Add(("ボス", "ボス", () => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None)));
        l.Add(("B3", "B3", CW.CheckWave(CW.CWaveOf(CW.DefaultBoss))));
        l.Add(("W3", "W3", CW.CheckWave(CW.CWaveOf(CW.DefaultHand))));
        return l.ToArray();
    }

    sealed class Agg
    {
        public long N, Wins, WinT, Surv, Dealt20;
        public long Summoned, FodderHit, FodderPop, FodderDisN, FodderDisDealt, ChainOthers, DisKills, FoeStunned;
        public long Thunder, ThunderFodder, ThunderShocked, FoeKillFodder, ExecGain, MyKillFodder, SomShock;

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Surv += o.Surv; Dealt20 += o.Dealt20;
            Summoned += o.Summoned; FodderHit += o.FodderHit; FodderPop += o.FodderPop; FodderDisN += o.FodderDisN; FodderDisDealt += o.FodderDisDealt;
            ChainOthers += o.ChainOthers; DisKills += o.DisKills; FoeStunned += o.FoeStunned;
            Thunder += o.Thunder; ThunderFodder += o.ThunderFodder; ThunderShocked += o.ThunderShocked;
            FoeKillFodder += o.FoeKillFodder; ExecGain += o.ExecGain; MyKillFodder += o.MyKillFodder; SomShock += o.SomShock;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) Surv++; }
            var fodderIds = r.Events.Where(ev => ev.Kind == BattleEventKind.Summon && ev.Text == UnitCatalog.Fodder.Name && ev.TargetId is not null)
                                    .Select(ev => ev.TargetId!.Value).ToHashSet();
            var foeIds = e.Select(u => u.InstanceId).ToHashSet();
            foreach (var ev in r.Events.Where(ev => ev.Kind == BattleEventKind.Summon && ev.Team == BattleContext.EnemyTeam))
                if (ev.TargetId is int t) foeIds.Add(t);
            foeIds.ExceptWith(fodderIds);
            // 倒しT ＝ 最後の敵（餌を除く）が倒れたターン（`bosswave` の倒しT と同じ読み方・勝った戦だけ）。
            if (r.PlayerWon)
                WinT += r.Events.Where(ev => ev.Kind == BattleEventKind.Death && ev.TargetId is int d && foeIds.Contains(d)).Select(ev => ev.Turn).DefaultIfEmpty(r.Turns).Max();
            string shockLabel = StatusKeys.LabelOf(StatusKeys.Shock);
            foreach (var ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.Damage && ev.Turn <= CW.BossTurns && ev.TargetId is int t && foeIds.Contains(t)) Dealt20 += ev.Amount;
                if (ev.Kind == BattleEventKind.Thunder)
                {
                    Thunder++;
                    if (ev.TargetId is int tt && fodderIds.Contains(tt)) ThunderFodder++;
                    else if ((ev.Text ?? "").Split(',').Contains(shockLabel)) ThunderShocked++;
                }
            }
            Summoned += r.BetraySummoned;
            FodderHit += r.BetrayHits;
            var mine = p.Select(u => u.Def.Id).ToHashSet();
            foreach (var (id, t) in r.TallyByUnit)
            {
                if (id == UnitCatalog.Fodder.Id)
                {
                    FodderPop += t.ShockSpent; FodderDisN += t.DischargeHits; FodderDisDealt += t.DischargeDealt;
                    ChainOthers += t.ChainUnits - t.ChainRoots;
                    continue;
                }
                if (mine.Contains(id)) { MyKillFodder += t.BetrayFodderKills; if (id == UnitCatalog.Som.Id) SomShock += t.ShockOnFoe; continue; }
                DisKills += t.DischargeDeaths; FoeStunned += t.ShockStunned;
                FoeKillFodder += t.BetrayFodderKills;
            }
            foreach (var id in e.Where(u => u.Def.Traits.Contains(TraitId.Executioner)).Select(u => u.Def.Id).Distinct())
                ExecGain += (r.TallyByUnit.TryGetValue(id, out var xt) ? xt.BetrayFodderKills : 0) * ExecutionerTrait.Gain;
        }

        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
    }

    static Agg Measure(Formation f, Func<List<UnitState>> make)
    {
        var parts = new Agg[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var (r, p, e) = CW.Fight(f, make, i, verbose: true);
            var a = new Agg(); a.Take(r, p, e); parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string WinT(Agg a) => a.Wins == 0 ? "—" : ((double)a.WinT / a.Wins).ToString("F2");

    static void RunAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = new List<(string Row, string Ver, Formation F)> { ("雷（ポンの席）", "—", BA.RefThunder) };
        foreach (string row in new[] { "A", "B" }) foreach (var v in Vers) boards.Add((row, v.Name, RowOf(row, v.Som)));
        var res = new Dictionary<(int, string), Agg>();
        for (int bi = 0; bi < boards.Count; bi++) foreach (var w in Waves) res[(bi, w.Name)] = Measure(boards[bi].F, w.Make);

        Console.WriteLine("# 第276期 ソムの転生 —— 検証行 × 版 × 波（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("- 行 A ＝ " + BA.SeatsNamed(RowA(UnitCatalog.SomS0)) + "（ポンの席の中央 ベニ → ソム）");
        Console.WriteLine("- 行 B ＝ " + BA.SeatsNamed(RowB(UnitCatalog.SomS0)) + "（ポンの席の後3 ミオ → ソム）");
        Console.WriteLine("- 参考 ＝ 雷（ポンの席）" + BA.SeatsNamed(BA.RefThunder) + "（ソムなし・版に依らない）");
        foreach (var v in Vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine("- 波: 本編第2〜5波（`compare` と同じ口・倍率 115/115）／ ボス ＝ 規定形（`EnemyCatalog.BossRegularWave`・倍率なし）／ B3 ＝ チェック波 B3-桁 ／ W3 ＝ チェック波 W3-割合（各器具と同じ口）");
        Console.WriteLine();
        string head = "| 行 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " | 第2〜5波 平均 |";
        string sep = "|---|---|" + string.Concat(Waves.Select(_ => "--:|")) + "--:|";

        Console.WriteLine("## 表1 勝率（%）");
        Console.WriteLine();
        Console.WriteLine(head); Console.WriteLine(sep);
        for (int bi = 0; bi < boards.Count; bi++)
        {
            var main = Waves.Where(w => w.Group == "本編").Select(w => res[(bi, w.Name)].Win).Average();
            Console.WriteLine($"| {boards[bi].Row} | {boards[bi].Ver} | " + string.Join(" | ", Waves.Select(w => F1(res[(bi, w.Name)].Win))) + $" | {F1(main)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 表2 倒しT（勝った戦で最後の敵〈餌を除く〉が倒れたターンの平均・`bosswave` の倒しT と同じ読み方）");
        Console.WriteLine();
        Console.WriteLine(head.Replace(" 第2〜5波 平均 |", " 全員生存 %（第2〜5波）|")); Console.WriteLine(sep);
        for (int bi = 0; bi < boards.Count; bi++)
        {
            long s = 0, n = 0;
            foreach (var w in Waves.Where(w => w.Group == "本編")) { s += res[(bi, w.Name)].Surv; n += res[(bi, w.Name)].N; }
            Console.WriteLine($"| {boards[bi].Row} | {boards[bi].Ver} | " + string.Join(" | ", Waves.Select(w => WinT(res[(bi, w.Name)]))) + $" | {F1(100.0 * s / n)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"## 表3 敵（餌を除く）へ入れた HP（1〜{CW.BossTurns} ターン・1戦平均）——倒せない台の連続量");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |"); Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        for (int bi = 0; bi < boards.Count; bi++)
            Console.WriteLine($"| {boards[bi].Row} | {boards[bi].Ver} | " + string.Join(" | ", Waves.Select(w => { var a = res[(bi, w.Name)]; return ((double)a.Dealt20 / a.N).ToString("F0"); })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 表4 機構の実測（1戦平均・群ごと）");
        Console.WriteLine();
        Console.WriteLine("湧 ＝ 湧いた餌 ／ 被 ＝ 味方の攻撃が餌に当たった ／ 弾 ＝ 餌の感電が弾けた ／ 放 ＝ 餌からの放電の本数（削り）／ 連 ＝ 餌が起点の連鎖で弾けた敵（餌を除く）／ ");
        Console.WriteLine("倒 ＝ 放電で倒れた敵 ／ 痺 ＝ 感電で痺れた敵 ／ 雷 ＝ カタの雷が当たった数（うち餌 ／ 餌以外の感電した敵）／ 敵が餌を倒した（処刑の伸び）／ 味方が餌を倒した ／ ソムが付けた感電");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 群 | 湧 | 被 | 弾 | 放（削り） | 連 | 倒 | 痺 | 雷（餌 ／ 感電） | 敵が餌を倒した（伸び） | 味方が餌を倒した | ソムの感電 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 1; bi < boards.Count; bi++)
            foreach (string g in new[] { "本編", "ボス", "B3", "W3" })
            {
                var a = new Agg();
                foreach (var w in Waves.Where(w => w.Group == g)) a.Merge(res[(bi, w.Name)]);
                Console.WriteLine($"| {boards[bi].Row} | {boards[bi].Ver} | {g} | {Per(a.Summoned, a.N)} | {Per(a.FodderHit, a.N)} | {Per(a.FodderPop, a.N)} | {Per(a.FodderDisN, a.N)}（{Per(a.FodderDisDealt, a.N)}）| {Per(a.ChainOthers, a.N)} | {Per(a.DisKills, a.N)} | {Per(a.FoeStunned, a.N)} | {Per(a.Thunder, a.N)}（{Per(a.ThunderFodder, a.N)} ／ {Per(a.ThunderShocked, a.N)}）| {Per(a.FoeKillFodder, a.N)}（{Per(a.ExecGain, a.N)}）| {Per(a.MyKillFodder, a.N)} | {Per(a.SomShock, a.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>`compare` の感電の行（規定のソム）の本編第2〜5波を、200 seed の帯ごとに並べる（帯の間のばらつき・採否には使わない）。</summary>
    static void Bands()
    {
        var f = CompareBuilds().First(r => r.Name.StartsWith("感電 (")).F;
        Console.WriteLine("| 帯 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int b = 0; b < 8; b++)
        {
            var cells = new string[4];
            for (int w = 1; w <= 4; w++)
            {
                int wins = 0;
                Parallel.For(0, Seeds, s => { if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, b * Seeds + s, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                cells[w - 1] = (100.0 * wins / Seeds).ToString("F1");
            }
            Console.WriteLine($"| {b * Seeds}..{b * Seeds + Seeds - 1} | " + string.Join(" | ", cells) + " |");
        }
    }

    static void LogOne(string row, string ver, string wave, int seed)
    {
        var v = Vers.First(x => x.Name == ver);
        var w = Waves.First(x => x.Name == (int.TryParse(wave, out int n) ? $"第{n}波" : wave));
        var f = RowOf(row, v.Som);
        var (r, _, _) = CW.Fight(f, w.Make, seed);
        Console.WriteLine($"# 行 {row}（{BA.SeatsNamed(f)}）× {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    /// <summary>検証・雷ソムA: ポンの席の中央（ベニ）をソムに（攻め寄り）。</summary>
    internal static Formation RowA(UnitDef som) => Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Tsugi,
        center: som, back1: UnitCatalog.Kata, back3: UnitCatalog.Mio);
    /// <summary>検証・雷ソムB: ポンの席の後3（ミオ）をソムに。</summary>
    internal static Formation RowB(UnitDef som) => Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Tsugi,
        center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: som);

    /// <summary>旧ソム（背かれ）を含む戦の台本の指紋。本編の5波 ＋ ボス規定形 ＋ チェック波の規定の組 × seed 0..49。</summary>
    static void Digest()
    {
        var boards = new (string, Formation)[] { ("A", RowA(UnitCatalog.SomS0)), ("B", RowB(UnitCatalog.SomS0)) };
        var waves = new List<(string, Func<List<UnitState>>)>();
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
        {
            int ww = w;
            waves.Add(($"本編{w + 1}", () => BattleEngine.Materialize(EnemyCatalog.Stages[ww].Enemy, BattleContext.EnemyTeam)));
        }
        waves.Add(("ボス規定", () => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None)));
        waves.Add((CW.DefaultBoss, CW.CheckWave(CW.CWaveOf(CW.DefaultBoss))));
        waves.Add((CW.DefaultHand, CW.CheckWave(CW.CWaveOf(CW.DefaultHand))));
        using var sha = SHA256.Create();
        long lines = 0;
        foreach (var (bn, f) in boards)
            foreach (var (wn, mk) in waves)
            {
                var sb = new StringBuilder();
                for (int s = 0; s < 50; s++)
                {
                    var l = CW.Dig(CW.Fight(f, mk, s).R);
                    lines += l.Count;
                    foreach (var x in l) sb.Append(x).Append('\n');
                }
                string h = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
                Console.WriteLine($"{bn} {wn} {h}");
            }
        Console.WriteLine($"行数 {lines}");
    }
}
