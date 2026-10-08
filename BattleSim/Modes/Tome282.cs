using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// =====================================================================================
// tome282 —— 第282期「トメ A 確定の反映 ＋ 消費の廃止（T1n）＋ ボスの標台の組み直し」。
// 指示書は design/PHASE282_TOME_NO_CONSUME_SPEC.md ／ 報告は design/PHASE282_TOME_NO_CONSUME.md。
//
//     dotnet run --project BattleSim -c Release 0 tome282 run     # 段1: トメ在席の `compare` 3 行 ＋ 標台S × 版 T0 ／ T1（規定）／ T1n × 本編第2〜5波・ボス × seed 0..199
//     dotnet run --project BattleSim -c Release 0 tome282 bandb   # 段1: 帯B（seed 200..599）の追試
//     dotnet run --project BattleSim -c Release 0 tome282 wall    # 段2 の Phase 0: 標経済の前3 ガルド → 壁の候補 × ボス・本編（ザンの仇指しが立つか）
//     dotnet run --project BattleSim -c Release 0 tome282 boss    # 段2: ボスの到達度 2×2（{旧台 ガルド壁, 新台} × {T1, T1n}）＋ 標台S
//     dotnet run --project BattleSim -c Release 0 tome282 feed    # 段3: 乱射の餌化（ムド・ドハ・ウツ同席・ソラの供給が途中で断たれる台・帯B）
//     dotnet run --project BattleSim -c Release 0 tome282 check   # 自己検査
//     dotnet run --project BattleSim -c Release 0 tome282 log <台> <版> <波 2..5|ボス> [seed]
// =====================================================================================
static class Tome282Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "run";
        switch (mode)
        {
            case "run": RunAll(); return;
            case "bandb": BandB(); return;
            case "wall": Wall(); return;
            case "boss": Boss(); return;
            case "feed": Feed(); return;
            case "check": Check(); return;
            case "log": LogOne(args.Length > 3 ? int.Parse(args[3]) : 2, args.Length > 4 ? args[4] : "T1n", args.Length > 5 ? args[5] : "ボス", args.Length > 6 ? int.Parse(args[6]) : 0); return;
            default: Console.WriteLine("tome282: モードは run / bandb / wall / boss / feed / check / log。"); return;
        }
    }

    const int Seeds = 200;
    const int TurnCap = 20;

    sealed record Ver(string Name, string What, UnitDef Tome);
    static readonly Ver[] Vers =
    {
        new("T0", "旧トメ（止め・対照）", UnitCatalog.TomeT0),
        new("T1", "炸裂 ＋ 爪痕 ＋ 乱射（第282期の規定・第283期は対照）", UnitCatalog.TomeT1),
        new("T1n", "T1 ＋ 層を残す（消費の廃止・第283期の規定）", UnitCatalog.TomeT1n),
    };
    static Ver VerOf(string n) => Vers.First(v => v.Name == n);

    static Formation Row(string prefix) => FvSwap(FvSwap(FvSwap(FvSwap(CompareBuilds().First(r => r.Name.StartsWith(prefix)).F, UnitCatalog.Sora, UnitCatalog.SoraSR0), UnitCatalog.Hisa, UnitCatalog.HisaHK0), UnitCatalog.Tome, UnitCatalog.TomeMb), UnitCatalog.Zan, UnitCatalog.ZanZN0);   // 第295期: ソラを旧の規定（SR0）に固定・第296期: ヒサも（HK0）・第299期: ミサ ／ ザンも（`TomeMb` ／ `ZanZN0`）

    /// <summary>新台の壁（`wall` で選んだもの・指示書 §3-1）。標経済の前3 ガルドをこの駒に替える。</summary>
    internal static UnitDef NewWall => UnitCatalog.DohaD0;

    static Formation EconomyWith(UnitDef wall) => FvSwap(Row("標経済 (ヒサ×ザン×ミサ)"), UnitCatalog.Gald, wall);

    /// <summary>台（規定のトメ＝T1 で定義し、版はトメだけを差し替える）。</summary>
    static readonly (string Name, string Group, Func<UnitDef, Formation> Make)[] Boards =
    {
        ("見境 (ミサ×ソラ)", "compare", d => FvSwap(Row("見境 (ミサ×ソラ)"), UnitCatalog.TomeMb, d)),
        ("見境改 (ミサ×薙ぎ)", "compare", d => FvSwap(Row("見境改 (ミサ×薙ぎ)"), UnitCatalog.TomeMb, d)),
        ("標経済 (ヒサ×ザン×ミサ)", "compare", d => FvSwap(Row("標経済 (ヒサ×ザン×ミサ)"), UnitCatalog.TomeMb, d)),
        ("標台S 止めの中央 ノミ → ザン", "ボス台", d => FvSwap(FvSwap(Row("見境 (ミサ×ソラ)"), UnitCatalog.Nomi, UnitCatalog.ZanZN0), UnitCatalog.TomeMb, d)),
        ("新台 標経済の前3 ガルド → 壁", "ボス台", d => FvSwap(EconomyWith(NewWall), UnitCatalog.TomeMb, d)),
    };

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
        return l.ToArray();
    }
    static (string Name, string Group, Func<List<UnitState>> Make) BossWave => Waves.First(w => w.Name == "ボス");

    sealed class Agg
    {
        public long N, Wins, WinT, Turns, Dealt20, FirstDeath, FirstDeathT, TomeDied, TomeDeathT;
        public long RFires, RCross, RLayerSum, RLayerMax, RDealt, RScar, RKills, RConsumed;
        public long STurns, SFoe, SAlly, SPulled, SAllyKills, SAllyDealt, LayerAdds, Vend, VendMarks;
        public long BossHeal, BossMaxEnd;
        public long Erupts, EruptFuel, EruptFuelAlly, DohaGains, SprayOnMudo, SprayOnDoha, UtsuExtra;
        public readonly long[] Hist = new long[10];
        public readonly long[] ScarByT = new long[TurnCap + 1], HealByT = new long[TurnCap + 1], TomeByT = new long[TurnCap + 1], TeamByT = new long[TurnCap + 1], AliveByT = new long[TurnCap + 1];

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Turns += o.Turns; Dealt20 += o.Dealt20;
            FirstDeath += o.FirstDeath; FirstDeathT += o.FirstDeathT; TomeDied += o.TomeDied; TomeDeathT += o.TomeDeathT;
            RFires += o.RFires; RCross += o.RCross; RLayerSum += o.RLayerSum; RLayerMax = Math.Max(RLayerMax, o.RLayerMax);
            RDealt += o.RDealt; RScar += o.RScar; RKills += o.RKills; RConsumed += o.RConsumed;
            STurns += o.STurns; SFoe += o.SFoe; SAlly += o.SAlly; SPulled += o.SPulled; SAllyKills += o.SAllyKills; SAllyDealt += o.SAllyDealt;
            LayerAdds += o.LayerAdds; Vend += o.Vend; VendMarks += o.VendMarks; BossHeal += o.BossHeal; BossMaxEnd += o.BossMaxEnd;
            Erupts += o.Erupts; EruptFuel += o.EruptFuel; EruptFuelAlly += o.EruptFuelAlly; DohaGains += o.DohaGains;
            SprayOnMudo += o.SprayOnMudo; SprayOnDoha += o.SprayOnDoha; UtsuExtra += o.UtsuExtra;
            for (int i = 0; i < Hist.Length; i++) Hist[i] += o.Hist[i];
            for (int i = 0; i <= TurnCap; i++) { ScarByT[i] += o.ScarByT[i]; HealByT[i] += o.HealByT[i]; TomeByT[i] += o.TomeByT[i]; TeamByT[i] += o.TeamByT[i]; AliveByT[i] += o.AliveByT[i]; }
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++;
            Turns += r.Turns;
            if (r.PlayerWon) Wins++;
            var foeIds = e.Select(u => u.InstanceId).ToHashSet();
            foreach (var ev in r.Events.Where(ev => ev.Kind == BattleEventKind.Summon && ev.Team == BattleContext.EnemyTeam))
                if (ev.TargetId is int t) foeIds.Add(t);
            var mine = p.ToDictionary(u => u.InstanceId, u => u.Def.Id);
            UnitState? tome = p.FirstOrDefault(u => u.Def.Id == UnitCatalog.Tome.Id);
            if (r.PlayerWon)
                WinT += r.Events.Where(ev => ev.Kind == BattleEventKind.Death && ev.TargetId is int d && foeIds.Contains(d)).Select(ev => ev.Turn).DefaultIfEmpty(r.Turns).Max();
            int firstDeath = 0, tomeDeath = 0;
            var deadAt = new Dictionary<int, int>();
            foreach (var ev in r.Events)
            {
                int tt = Math.Clamp(ev.Turn, 0, TurnCap);
                if (ev.Kind == BattleEventKind.Damage && ev.TargetId is int t)
                {
                    if (foeIds.Contains(t))
                    {
                        if (ev.Turn <= TurnCap) { Dealt20 += ev.Amount; TeamByT[tt] += ev.Amount; }
                        if (tome is not null && ev.ActorId == tome.InstanceId && ev.Turn <= TurnCap) TomeByT[tt] += ev.Amount;
                    }
                    else if (tome is not null && ev.ActorId == tome.InstanceId && mine.TryGetValue(t, out string? id))
                    {
                        if (id == UnitCatalog.Mudo.Id) SprayOnMudo++;
                        else if (id == UnitCatalog.DohaD0.Id) SprayOnDoha++;
                    }
                }
                else if (ev.Kind == BattleEventKind.Heal && ev.TargetId is int h && foeIds.Contains(h))
                {
                    BossHeal += ev.Amount;
                    if (ev.Turn <= TurnCap) HealByT[tt] += ev.Amount;
                }
                else if (ev.Kind == BattleEventKind.Death && ev.TargetId is int d && mine.ContainsKey(d))
                {
                    if (firstDeath == 0) firstDeath = ev.Turn;
                    if (tome is not null && d == tome.InstanceId && tomeDeath == 0) tomeDeath = ev.Turn;
                    deadAt.TryAdd(d, ev.Turn);
                }
            }
            if (firstDeath > 0) { FirstDeath++; FirstDeathT += firstDeath; }
            if (tomeDeath > 0) { TomeDied++; TomeDeathT += tomeDeath; }
            for (int t = 1; t <= TurnCap; t++)
                if (t <= r.Turns) AliveByT[t] += p.Count(u => !deadAt.TryGetValue(u.InstanceId, out int dt) || dt > t);

            foreach (var (id, t) in r.TallyByUnit) LayerAdds += t.MarkLayerAdds;
            if (r.TallyByUnit.TryGetValue(UnitCatalog.Tome.Id, out var tm))
            {
                RFires += tm.RuptureFires; RCross += tm.RuptureCross; RLayerSum += tm.RuptureLayerSum; RLayerMax = Math.Max(RLayerMax, tm.RuptureLayerMax);
                RDealt += tm.RuptureDealt; RScar += tm.RuptureScar; RKills += tm.RuptureKills; RConsumed += tm.RuptureConsumed;
                STurns += tm.SprayTurns; SFoe += tm.SprayFoe; SAlly += tm.SprayAlly; SPulled += tm.SprayPulled; SAllyKills += tm.SprayAllyKills; SAllyDealt += tm.SprayAllyDealt;
                if (tm.RuptureLayerHist is { } hh) for (int i = 0; i < Hist.Length; i++) Hist[i] += hh[i];
                if (tm.RuptureScarByTurn is { } sb) for (int i = 0; i <= TurnCap; i++) ScarByT[i] += sb[i];
            }
            if (p.Any(u => u.Def.Id == UnitCatalog.Zan.Id) && r.TallyByUnit.TryGetValue(UnitCatalog.Zan.Id, out var zt)) { Vend += zt.VendettaFires; VendMarks += zt.VendettaMarks; }
            if (p.Any(u => u.Def.Id == UnitCatalog.Mudo.Id) && r.TallyByUnit.TryGetValue(UnitCatalog.Mudo.Id, out var mt)) { Erupts += mt.EruptFires; EruptFuel += mt.EruptFuel; EruptFuelAlly += mt.EruptFuelFromAlly; }
            if (p.Any(u => u.Def.Id == UnitCatalog.Utsu.Id) && r.TallyByUnit.TryGetValue(UnitCatalog.Utsu.Id, out var ut)) UtsuExtra += ut.ExtraSwings;
            if (p.Any(u => u.Def.Id == UnitCatalog.DohaD0.Id))
                foreach (var l in r.Log) if (l.Text.Contains("が痛みを飲み込んだ", StringComparison.Ordinal)) DohaGains++;
            var boss = e.FirstOrDefault(u => u.Def.Id == EnemyCatalog.BossRegular.Id);
            if (boss is not null) BossMaxEnd += boss.MaxHp;
        }

        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
    }

    static (BattleResult R, List<UnitState> P, List<UnitState> E) Fight(Formation f, Func<List<UnitState>> wave, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = wave();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        return (r, p, e);
    }

    static Agg Measure(Formation f, Func<List<UnitState>> make, int from = 0, int n = Seeds)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i =>
        {
            var (r, p, e) = Fight(f, make, from + i);
            var a = new Agg(); a.Take(r, p, e); parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string WinT(Agg a) => a.Wins == 0 ? "—" : ((double)a.WinT / a.Wins).ToString("F2");
    static string Sgn(double x) => x.ToString("+0.0;-0.0;0.0");
    static string AvgT(long sum, long n) => n == 0 ? "—" : ((double)sum / n).ToString("F1");
    static string Layer(Agg a) => a.RFires == 0 ? "—" : $"{(double)a.RLayerSum / a.RFires:F2}（{a.RLayerMax}）";

    // ---------------------------------------------------------------------------------
    // 段1: 本編
    // ---------------------------------------------------------------------------------
    static void RunAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = new Dictionary<(int, string, string), Agg>();
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers)
                foreach (var w in Waves) res[(bi, v.Name, w.Name)] = Measure(Boards[bi].Make(v.Tome), w.Make);

        Console.WriteLine("# 第282期 段1 —— 消費の廃止（T1n）× 台 × 波（seed 0..199）");
        Console.WriteLine();
        for (int bi = 0; bi < Boards.Length; bi++)
            Console.WriteLine($"- 台{bi}（{Boards[bi].Group}）{Boards[bi].Name} ＝ " + BA.SeatsNamed(Boards[bi].Make(UnitCatalog.TomeMb)));
        foreach (var v in Vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        Console.WriteLine("## 表1 勝率（%）と差");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " | 第2〜5波 平均 | T1 差 | 張り付き |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")) + "--:|--:|---|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            double m1 = Waves.Where(w => w.Group == "本編").Select(w => res[(bi, "T1", w.Name)].Win).Average();
            bool pinT1 = Waves.Where(w => w.Group == "本編").All(w => res[(bi, "T1", w.Name)].Win >= 100.0);
            foreach (var v in Vers)
            {
                var main = Waves.Where(w => w.Group == "本編").Select(w => res[(bi, v.Name, w.Name)].Win).ToList();
                bool pin = main.All(x => x >= 100.0);
                string pinText = v.Name == "T1n" && pin ? (pinT1 ? "既に（T1 も）" : "**新たな張り付き**") : "";
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", Waves.Select(w => F1(res[(bi, v.Name, w.Name)].Win))) + $" | {F1(main.Average())} | {(v.Name == "T1" ? "" : Sgn(main.Average() - m1))} | {pinText} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 表2 T1n − T1（波別・pt）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "--:|")));
        for (int bi = 0; bi < Boards.Length; bi++)
            Console.WriteLine($"| {bi} | " + string.Join(" | ", Waves.Select(w => Sgn(res[(bi, "T1n", w.Name)].Win - res[(bi, "T1", w.Name)].Win))) + " |");
        Console.WriteLine();
        Console.WriteLine("## 表3 機構（1戦平均・波ごと）");
        Console.WriteLine();
        Console.WriteLine("炸 ＝ 炸裂（列越え）／ 層 ＝ 炸裂の平均層（最大）／ 爪 ＝ 爪痕 ／ 消 ＝ 層を消した数 ／ 乱 ＝ 乱射の手番 ／ 乱味 ＝ 味方に当たった発（倒した味方）／ 層足 ＝ 既に標のある敵に層を足した回数 ／ 仇 ＝ ザンの仇指し ／ 倒しT ＝ 勝った戦の決着T");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 炸（越） | 層（最大） | 爪 | 消 | 乱 | 乱味（倒） | 層足 | 仇 | 倒しT |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers.Where(v => v.Name != "T0"))
                foreach (var w in Waves)
                {
                    var a = res[(bi, v.Name, w.Name)];
                    Console.WriteLine($"| {bi} | {v.Name} | {w.Name} | {Per(a.RFires, a.N)}（{Per(a.RCross, a.N)}）| {Layer(a)} | {Per1(a.RScar, a.N)} | {Per(a.RConsumed, a.N)} | {Per(a.STurns, a.N)} | {Per(a.SAlly, a.N)}（{Per(a.SAllyKills, a.N)}）| {Per(a.LayerAdds, a.N)} | {Per(a.Vend, a.N)} | {WinT(a)} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 表4 層の深さの分布（炸裂の回数・本編第2〜5波の合計）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 層1 | 層2 | 層3 | 層4 | 層5 | 層6〜8 | 層9 以上 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers.Where(v => v.Name != "T0"))
            {
                var a = new Agg(); foreach (var w in Waves.Where(w => w.Group == "本編")) a.Merge(res[(bi, v.Name, w.Name)]);
                Console.WriteLine($"| {bi} | {v.Name} | {a.Hist[1]} | {a.Hist[2]} | {a.Hist[3]} | {a.Hist[4]} | {a.Hist[5]} | {a.Hist[6] + a.Hist[7] + a.Hist[8]} | {a.Hist[9]} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void BandB()
    {
        Console.WriteLine("# 第282期 帯B（seed 200..599）—— 本編第2〜5波の勝率");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | T1 差 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            double m1 = 0;
            foreach (var v in new[] { VerOf("T1"), VerOf("T1n"), VerOf("T0") })
            {
                var f = Boards[bi].Make(v.Tome);
                var cells = new double[4];
                for (int w = 1; w <= 4; w++)
                {
                    int wins = 0, ww = w;
                    Parallel.For(200, 600, s => { if (BattleEngine.Run(f, EnemyCatalog.Stages[ww].Enemy, s, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                    cells[w - 1] = 100.0 * wins / 400;
                }
                double m = cells.Average();
                if (v.Name == "T1") m1 = m;
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", cells.Select(F1)) + $" | {F1(m)} | {(v.Name == "T1" ? "" : Sgn(m - m1))} |");
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // 段2 の Phase 0: 壁の選別（指示書 §3-1）
    // ---------------------------------------------------------------------------------
    static readonly UnitDef[] WallCands = { UnitCatalog.Gald, UnitCatalog.Golm, UnitCatalog.Ban, UnitCatalog.DohaD0, UnitCatalog.Uke, UnitCatalog.Kado, UnitCatalog.Sasa, UnitCatalog.Dolga };

    static void Wall()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第282期 段2 Phase 0 —— 標経済の前3（ヒサが標を付ける壁）の候補 × ボス・本編（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("採る条件（測る前に固定）: **ボス（T1）でザンの仇指しが1戦 1 回以上立つ**（＝標持ち本人に被弾が届く）候補のうち、ボス（T1n）の爪痕累計が最大のもの。");
        Console.WriteLine("仇 ＝ ザンの仇指し ／ 層足 ＝ 既に標のある敵に層を足した回数 ／ 崩れ ＝ 味方の最初の死亡のT ／ ト死 ＝ トメの死亡T ／ 爪 ＝ 爪痕累計 ／ 本編 ＝ 第2〜5波の平均勝率（T1）。");
        Console.WriteLine();
        Console.WriteLine("| 壁 | 最大HP | ボス T1 仇 | 層足 | 崩れ | ト死 | 爪（T1） | 爪（T1n） | 最後の最大HP（T1n） | 本編 T1 | 本編 T1n |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var wall in WallCands)
        {
            var b1 = Measure(FvSwap(EconomyWith(wall), UnitCatalog.TomeMb, UnitCatalog.TomeT1), BossWave.Make);
            var bn = Measure(FvSwap(EconomyWith(wall), UnitCatalog.TomeMb, UnitCatalog.TomeT1n), BossWave.Make);
            double main1 = Waves.Where(w => w.Group == "本編").Average(w => Measure(FvSwap(EconomyWith(wall), UnitCatalog.TomeMb, UnitCatalog.TomeT1), w.Make).Win);
            double mainN = Waves.Where(w => w.Group == "本編").Average(w => Measure(FvSwap(EconomyWith(wall), UnitCatalog.TomeMb, UnitCatalog.TomeT1n), w.Make).Win);
            Console.WriteLine($"| {wall.Name} | {wall.MaxHp} | {Per(b1.Vend, b1.N)} | {Per(b1.LayerAdds, b1.N)} | {AvgT(b1.FirstDeathT, b1.FirstDeath)} | {AvgT(b1.TomeDeathT, b1.TomeDied)} | {Per1(b1.RScar, b1.N)} | {Per1(bn.RScar, bn.N)} | {Per1(bn.BossMaxEnd, bn.N)} | {F1(main1)} | {F1(mainN)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 段2: ボスの到達度 2×2
    // ---------------------------------------------------------------------------------
    static void Boss()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new (string Board, string Ver, Formation F)[]
        {
            ("旧台（ガルド壁）", "T1", Boards[2].Make(UnitCatalog.TomeT1)),
            ("旧台（ガルド壁）", "T1n", Boards[2].Make(UnitCatalog.TomeT1n)),
            ($"新台（{NewWall.Name}）", "T1", Boards[4].Make(UnitCatalog.TomeT1)),
            ($"新台（{NewWall.Name}）", "T1n", Boards[4].Make(UnitCatalog.TomeT1n)),
            ("標台S（参考）", "T1", Boards[3].Make(UnitCatalog.TomeT1)),
            ("標台S（参考）", "T1n", Boards[3].Make(UnitCatalog.TomeT1n)),
        };
        Console.WriteLine($"# 第282期 段2 —— ボスの到達度（規定形・HP {EnemyCatalog.BossRegular.MaxHp}・回復は最大HPの 40%・seed 0..199・倍率なし）");
        Console.WriteLine();
        Console.WriteLine($"- 新台 ＝ {BA.SeatsNamed(Boards[4].Make(UnitCatalog.TomeMb))}");
        Console.WriteLine($"- 旧台 ＝ {BA.SeatsNamed(Boards[2].Make(UnitCatalog.TomeMb))}");
        Console.WriteLine($"- 標台S ＝ {BA.SeatsNamed(Boards[3].Make(UnitCatalog.TomeMb))}");
        Console.WriteLine();
        Console.WriteLine("s ＝ 炸裂1回あたりの平均層（T1）／ 1ターンあたりの層の供給（層足 ＋ 新しく付いた標・T1n は炸裂の層の伸びで読む）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 倒しT | 崩れ | トメの死亡T | 仇／戦 | 炸裂（層・最大） | 爪痕累計 | 最後の最大HP | 勇者の回復 | 隊の与ダメ（20T） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var aggs = new List<(string, string, Agg)>();
        foreach (var c in cells)
        {
            var a = Measure(c.F, BossWave.Make);
            aggs.Add((c.Board, c.Ver, a));
            Console.WriteLine($"| {c.Board} | {c.Ver} | {F1(a.Win)} | {WinT(a)} | {AvgT(a.FirstDeathT, a.FirstDeath)} | {AvgT(a.TomeDeathT, a.TomeDied)} | {Per(a.Vend, a.N)} | {Per(a.RFires, a.N)}（{Layer(a)}）| {Per1(a.RScar, a.N)} | {Per1(a.BossMaxEnd, a.N)} | {Per1(a.BossHeal, a.N)} | {Per1(a.Dealt20, a.N)} |");
        }
        Console.WriteLine();
        foreach (var (b, v, a) in aggs)
        {
            Console.WriteLine($"### {b} × {v} のターン別（1戦平均）");
            Console.WriteLine();
            Console.WriteLine("| T | " + string.Join(" | ", Enumerable.Range(1, 10).Select(t => $"T{t}")) + " |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, 10).Select(_ => "--:|")));
            Console.WriteLine("| 爪痕 | " + string.Join(" | ", Enumerable.Range(1, 10).Select(t => Per1(a.ScarByT[t], a.N))) + " |");
            Console.WriteLine("| 回復 | " + string.Join(" | ", Enumerable.Range(1, 10).Select(t => Per1(a.HealByT[t], a.N))) + " |");
            Console.WriteLine("| トメ | " + string.Join(" | ", Enumerable.Range(1, 10).Select(t => Per1(a.TomeByT[t], a.N))) + " |");
            Console.WriteLine("| 隊 | " + string.Join(" | ", Enumerable.Range(1, 10).Select(t => Per1(a.TeamByT[t], a.N))) + " |");
            Console.WriteLine("| 生存 | " + string.Join(" | ", Enumerable.Range(1, 10).Select(t => Per(a.AliveByT[t], a.N))) + " |");
            Console.WriteLine();
        }
        // 残る律速: 寿命（隊の全員の最大HP × k）だけを伸ばした仮想の版（第281期の格子の続き・採否の候補ではない）
        Console.WriteLine("## 寿命だけを伸ばした仮想の版（隊の全員の最大HP × k・セル ＝ 勝率（倒しT）／ 爪痕累計（仇指し・炸裂・平均層・トメの死亡T））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ×1 | ×1.5 | ×2 | ×3 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var (bn, bi) in new[] { ($"新台（{NewWall.Name}）", 4), ("標台S", 3), ("旧台（ガルド壁）", 2) })
            foreach (var v in new[] { VerOf("T1"), VerOf("T1n") })
            {
                var cellsK = new List<string>();
                foreach (int k10 in new[] { 10, 15, 20, 30 })
                {
                    var f = Boards[bi].Make(v.Tome);
                    if (k10 != 10) f = Tough(f, k10);
                    var a = Measure(f, BossWave.Make);
                    cellsK.Add($"{F1(a.Win)}（{WinT(a)}）／ {Per1(a.RScar, a.N)}（仇 {Per1(a.Vend, a.N)}・炸 {Per1(a.RFires, a.N)}・層 {(a.RFires == 0 ? "—" : ((double)a.RLayerSum / a.RFires).ToString("F1"))}・ト死 {AvgT(a.TomeDeathT, a.TomeDied)}）");
                }
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", cellsK) + " |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static Formation Tough(Formation f, int x10)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.WithStats(d.MaxHp * x10 / 10, d.Attack);
        return g;
    }

    // ---------------------------------------------------------------------------------
    // 段3: 乱射の餌化（帯B）
    // ---------------------------------------------------------------------------------
    /// <summary>餌台: 前1 ムド ／ 前3 ドハ ／ 中央 ウツ ／ 後1 トメ ／ 後3 ソラ。標の供給はソラの焦点だけで、ソラが倒れると断たれる（以後トメは乱射）。</summary>
    static Formation FeedBoard(UnitDef tome) => Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.DohaD0, center: UnitCatalog.Utsu, back1: tome, back3: UnitCatalog.Sora);

    static void Feed()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第282期 段3 —— 乱射の餌化（餌台 × 版 × 本編第2〜5波・帯B ＝ seed 200..599）");
        Console.WriteLine();
        Console.WriteLine($"- 餌台 ＝ {BA.SeatsNamed(FeedBoard(UnitCatalog.Tome))}（`compare` には足さない）");
        Console.WriteLine("- 暴発 ＝ ムドの暴発 ／ 燃料（味方）＝ 暴発の燃料になった被弾（うち味方から）／ 乱→ム ＝ 乱射がムドに当たった発 ／ 痛み ＝ ドハが痛みを飲み込んだ回数 ／ 乱→ド ＝ 乱射がドハに当たった発 ／ ウ追 ＝ ウツの手番の追加の振り");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 勝率 | 乱射の手番 | 暴発 | 燃料（味方） | 乱→ム | 痛み | 乱→ド | ウ追 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var v in Vers)
        {
            var all = new Agg();
            foreach (var w in Waves.Where(w => w.Group == "本編"))
            {
                var a = Measure(FeedBoard(v.Tome), w.Make, 200, 400);
                all.Merge(a);
                Console.WriteLine($"| {v.Name} | {w.Name} | {F1(a.Win)} | {Per(a.STurns, a.N)} | {Per(a.Erupts, a.N)} | {Per(a.EruptFuel, a.N)}（{Per(a.EruptFuelAlly, a.N)}）| {Per(a.SprayOnMudo, a.N)} | {Per(a.DohaGains, a.N)} | {Per(a.SprayOnDoha, a.N)} | {Per(a.UtsuExtra, a.N)} |");
            }
            Console.WriteLine($"| **{v.Name}** | **計** | {F1(all.Win)} | {Per(all.STurns, all.N)} | {Per(all.Erupts, all.N)} | {Per(all.EruptFuel, all.N)}（{Per(all.EruptFuelAlly, all.N)}）| {Per(all.SprayOnMudo, all.N)} | {Per(all.DohaGains, all.N)} | {Per(all.SprayOnDoha, all.N)} | {Per(all.UtsuExtra, all.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int fail = 0;
        void Expect(string what, bool ok, string detail = "")
        {
            Console.WriteLine($"- {(ok ? "○" : "×")} {what}{(detail.Length > 0 ? "（" + detail + "）" : "")}");
            if (!ok) fail++;
        }
        Console.WriteLine("# 第282期 tome282 check —— 自己検査");
        Console.WriteLine();
        // 第283期に T1n を規定にしたので、(a)〜(c) は「規定 ＝ T1n」の形に直した（第282期の版は「規定 ＝ T1」を確かめていた）。
        // 第286期に M-b を規定にしたので、(a) は「`TomeT1n` が T1n の定義を明示的に持ち、規定（M-b）とは別の物」の形に直した。
        Expect("(a) `TomeT1n` は T1n（炸裂・爪痕・乱射・層を残す）を明示的に持つ・規定（第286期から M-b）とも `TomeT1` とも別の物",
            UnitCatalog.TomeT1n.Traits.SequenceEqual(new[] { TraitId.Rupture, TraitId.RuptureScar, TraitId.Spray, TraitId.RuptureKeep })
            && !ReferenceEquals(UnitCatalog.TomeT1n, UnitCatalog.Tome) && !ReferenceEquals(UnitCatalog.TomeT1, UnitCatalog.Tome)
            && UnitCatalog.Tome.Traits.SequenceEqual(UnitCatalog.TomeMb.Traits.Append(TraitId.FeatherMarkLayer)));   // 第299期: 規定は M-b ＋ MF-b（`TomeMb` は M-b の明示の定義）
        Expect("(b) T0 は旧トメ（止め）・T1n は T1 ＋ 層を残す の1札だけが違う",
            UnitCatalog.TomeT0.Traits.SequenceEqual(new[] { TraitId.Finisher })
            && UnitCatalog.TomeT1n.Traits.Except(UnitCatalog.TomeT1.Traits).SequenceEqual(new[] { TraitId.RuptureKeep })
            && !UnitCatalog.TomeT1.Traits.Except(UnitCatalog.TomeT1n.Traits).Any());
        int keepHolders = UnitCatalog.Everyone.Count(d => d.Traits.Contains(TraitId.RuptureKeep));
        int keepRows = CompareBuilds().Concat(CrossBuilds()).Sum(r => r.F.Occupied().Count(o => o.Def.Traits.Contains(TraitId.RuptureKeep)));
        int tomeRows = CompareBuilds().Concat(CrossBuilds()).Sum(r => r.F.Occupied().Count(o => ReferenceEquals(o.Def, UnitCatalog.Tome)));
        Expect("(c) 層を残す札の保持者は `Everyone` で規定のトメ1枚・行の保持者はトメの在席数と同じ", keepHolders == 1 && keepRows == tomeRows, $"{keepHolders} ／ {keepRows} ／ トメ {tomeRows}");

        // (d) T1n ＝ T1-c（`FinisherRule(2, Consume: false)`）の台本一致（4 台 × 本編第2〜5波 ＋ ボス × seed 0..29）
        int diff = 0, tot = 0;
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var w in Waves)
                for (int s = 0; s < 30; s++)
                {
                    var p1 = BattleEngine.Materialize(Boards[bi].Make(UnitCatalog.TomeT1n), BattleContext.PlayerTeam);
                    var a = BattleEngine.Run(p1, w.Make(), s, verbose: true);
                    var p2 = BattleEngine.Materialize(Boards[bi].Make(UnitCatalog.TomeT1), BattleContext.PlayerTeam);
                    var b = BattleEngine.Run(p2, w.Make(), s, verbose: true, finisher: new FinisherRule(2, false));
                    tot++;
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns || !a.Log.Select(l => l.Text).SequenceEqual(b.Log.Select(l => l.Text))) diff++;
                }
        Expect("(d) T1n の台本は T1-c（第281期の対照・`Consume=false`）と一致", diff == 0, $"{tot} 戦中 {diff} 件ずれ");

        // (e) T1n は層を一度も消さない・層の分布の和 ＝ 炸裂
        var n = new Agg();
        for (int bi = 0; bi < Boards.Length; bi++) foreach (var w in Waves) n.Merge(Measure(Boards[bi].Make(UnitCatalog.TomeT1n), w.Make, 0, 30));
        Expect("(e) T1n: 消費 0・層の分布の和 ＝ 炸裂の回数", n.RConsumed == 0 && n.Hist.Sum() == n.RFires && n.RFires > 0, $"消費 {n.RConsumed} ／ 分布 {n.Hist.Sum()} ／ 炸裂 {n.RFires}");

        // (f) 層化のゲート: トメのいない台では層を足した回数 0（仇討ち (ヒサ×ザン)・逸らし (ソラ×カド) × 本編第2〜5波）
        long adds = 0;
        foreach (string row in new[] { "仇討ち (ヒサ×ザン)", "逸らし (ソラ×カド)", "逸らし改 (ソラ×ノミ)" })
            foreach (var w in Waves) adds += Measure(Row(row), w.Make, 0, 30).LayerAdds;
        Expect("(f) トメのいない台では敵の標は層にならない（層を足した回数 0）", adds == 0, $"{adds}");

        // (g) seed 決定的・verbose の有無で勝敗と決着T が一致（T1n）
        int nd = 0, nv = 0;
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var w in Waves)
                for (int s = 0; s < 15; s++)
                {
                    var a = Fight(Boards[bi].Make(UnitCatalog.TomeT1n), w.Make, s).R;
                    var b = Fight(Boards[bi].Make(UnitCatalog.TomeT1n), w.Make, s).R;
                    var c = Fight(Boards[bi].Make(UnitCatalog.TomeT1n), w.Make, s, verbose: false).R;
                    if (!a.Log.Select(l => l.Text).SequenceEqual(b.Log.Select(l => l.Text))) nd++;
                    if (a.PlayerWon != c.PlayerWon || a.Turns != c.Turns) nv++;
                }
        Expect("(g) T1n: seed 決定的", nd == 0, $"{nd} 件");
        Expect("(h) T1n: verbose の有無で勝敗と決着T が一致", nv == 0, $"{nv} 件");

        // (i) 器具の固定: `CompareT0` は第281期までの行（規定のトメ → T0）と同じ顔ぶれ
        bool same = CompareT0.Length == CompareBuilds().Length && CompareT0.Zip(CompareBuilds()).All(z =>
            z.First.Name == z.Second.Name && z.First.F.Occupied().Select(o => o.Def.Id).SequenceEqual(z.Second.F.Occupied().Select(o => o.Def.Id))
            && !z.First.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Tome)));
        Expect("(i) 旧トメに固定した器具が読む行（`CompareT0`）は規定のトメを1枚も含まず、顔ぶれ（Id）は `compare` と同じ", same);

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "すべて ○。" : $"× が {fail} 件。");
    }

    static void LogOne(int board, string ver, string wave, int seed)
    {
        var v = VerOf(ver);
        var w = Waves.First(x => x.Name == (int.TryParse(wave, out int nn) ? $"第{nn}波" : wave));
        var f = Boards[board].Make(v.Tome);
        var (r, _, e) = Fight(f, w.Make, seed);
        Console.WriteLine($"# 台{board} {Boards[board].Name}（{BA.SeatsNamed(f)}）× {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
        var boss = e.FirstOrDefault(u => u.Def.Id == EnemyCatalog.BossRegular.Id);
        if (boss is not null) Console.WriteLine($"（勇者の最後: HP {boss.Hp} ／ 最大HP {boss.MaxHp}）");
    }
}
