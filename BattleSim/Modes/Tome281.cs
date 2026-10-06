using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using CW = CheckWaveDiag;

// =====================================================================================
// tome281 —— 第281期「トメの転生（炸裂・爪痕・乱射と、敵側の標の層）」。
// 指示書は design/PHASE281_TOME_REBIRTH_SPEC.md ／ 報告は design/PHASE281_TOME_REBIRTH.md。
//
//     dotnet run --project BattleSim -c Release 0 tome281 run        # 台4つ × 版 T0 ／ T1 ／ T2 ／ T1-s ／ T1-c × 本編第2〜5波・ボス規定形 × seed 0..199
//     dotnet run --project BattleSim -c Release 0 tome281 bandb      # 帯B（seed 200..599）の追試: 台 × 版 × 本編第2〜5波の勝率
//     dotnet run --project BattleSim -c Release 0 tome281 boss       # ボスの到達度（指示書 §5 の実測版）: 台 × 版のターン別の推移と律速の分解
//     dotnet run --project BattleSim -c Release 0 tome281 mult       # 倍率の掃引（FinisherRule.Multiplier 1..4 ／ T1）× 台 × 本編第2〜5波・ボス
//     dotnet run --project BattleSim -c Release 0 tome281 seat       # 新行（ヒサ×ザン×トメ×ムド×ボルグ）の席: 120 通り × T1 × 帯A → 上位8を帯B で追試
//     dotnet run --project BattleSim -c Release 0 tome281 check      # 自己検査
//     dotnet run --project BattleSim -c Release 0 tome281 log <台 0..3> <版> <波 2..5|ボス> [seed]   # 1戦のログ
//
// **規定のトメは動かさない**（T0 のまま）。T1 ／ T2 ／ T1-s は `UnitCatalog.TomeT1` ／ `TomeT2` ／ `TomeT1s`（`All` ／ `Retired` ／ `Presets` に入れない）。
// T1-c は T1 に `FinisherRule(2, Consume: false)` を渡した対照（ノブではない）。採否はポン。
// =====================================================================================
static class Tome281Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "run";
        switch (mode)
        {
            case "run": RunAll(); return;
            case "bandb": BandB(); return;
            case "boss": Boss(); return;
            case "mult": Mult(); return;
            case "seat": Seat(args.Length > 3 ? args[3] : "b"); return;
            case "check": Check(); return;
            case "log": LogOne(args.Length > 3 ? int.Parse(args[3]) : 2, args.Length > 4 ? args[4] : "T1", args.Length > 5 ? args[5] : "5", args.Length > 6 ? int.Parse(args[6]) : 0); return;
            default: Console.WriteLine("tome281: モードは run / bandb / boss / mult / seat / check / log。"); return;
        }
    }

    const int Seeds = 200;

    internal sealed record Ver(string Name, string What, UnitDef Tome, FinisherRule Rule);
    internal static readonly Ver[] Vers =
    {
        new("T0", "旧トメ（止め・規定のまま・対照）", UnitCatalog.TomeT0, FinisherRule.Default),
        new("T1", "炸裂 ＋ 爪痕 ＋ 乱射（規定候補）", UnitCatalog.TomeT1, FinisherRule.Default),
        new("T2", "T1 ＋ 周期 [溜め, 溜め, 術]", UnitCatalog.TomeT2, FinisherRule.Default),
        new("T1-s", "T1 から爪痕を抜く（爪痕の寄与の分離）", UnitCatalog.TomeT1s, FinisherRule.Default),
        new("T1-c", "T1 で層を消さない（`Consume=false`・消費サイクルの寄与の分離）", UnitCatalog.TomeT1, new FinisherRule(2, false)),
    };
    static Ver VerOf(string n) => Vers.First(v => v.Name == n);

    static Formation Row(string prefix) => CompareBuilds().First(r => r.Name.StartsWith(prefix)).F;

    /// <summary>新行（`compare` 64 行目）。席は `seat` で選んだもの（`Presets` の定義をそのまま引く）。</summary>
    static Formation MarkEconomyRow => Row("標経済 (ヒサ×ザン×トメ)");

    /// <summary>台（規定のトメ＝T0 で定義し、版はトメだけを差し替える）。</summary>
    internal static readonly (string Name, string Group, Func<UnitDef, Formation> Make)[] Boards =
    {
        ("止め (トメ×ソラ)", "compare", d => FvSwap(Row("止め (トメ×ソラ)"), UnitCatalog.Tome, d)),
        ("止め改 (トメ×薙ぎ)", "compare", d => FvSwap(Row("止め改 (トメ×薙ぎ)"), UnitCatalog.Tome, d)),
        ("標経済 (ヒサ×ザン×トメ)", "compare", d => FvSwap(MarkEconomyRow, UnitCatalog.Tome, d)),
        ("標台S 止めの中央 ノミ → ザン", "ボス台", d => FvSwap(FvSwap(Row("止め (トメ×ソラ)"), UnitCatalog.Nomi, UnitCatalog.Zan), UnitCatalog.Tome, d)),
        ("読み手台 毒→被弾強化の後3 セロ → トメ", "検証", d => FvSwap(Row("毒→被弾強化 (グザ×ムド)"), UnitCatalog.Sero, d)),
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

    const int TurnCap = 20;

    /// <summary>1戦平均を取る量。</summary>
    sealed class Agg
    {
        public long N, Wins, WinT, Surv, Turns, Dealt20, FirstDeath, FirstDeathT, TomeDied, TomeDeathT;
        public long RFires, RCross, RLayerSum, RLayerMax, RDealt, RScar, RKills, RConsumed, OldFires;
        public long STurns, SFoe, SAlly, SPulled, SFoeDealt, SAllyDealt, SAllyKills, LayerAdds, HexOnTome, Charges, Erupts;
        public long BossHeal, BossMaxEnd, BossHpEnd;
        public readonly long[] Hist = new long[10];
        public readonly long[] ScarByT = new long[TurnCap + 1], TomeByT = new long[TurnCap + 1], TeamByT = new long[TurnCap + 1], HealByT = new long[TurnCap + 1], AliveByT = new long[TurnCap + 1];
        public readonly Dictionary<string, long> AllyHitBy = new();

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Surv += o.Surv; Turns += o.Turns; Dealt20 += o.Dealt20;
            FirstDeath += o.FirstDeath; FirstDeathT += o.FirstDeathT; TomeDied += o.TomeDied; TomeDeathT += o.TomeDeathT;
            RFires += o.RFires; RCross += o.RCross; RLayerSum += o.RLayerSum; RLayerMax = Math.Max(RLayerMax, o.RLayerMax);
            RDealt += o.RDealt; RScar += o.RScar; RKills += o.RKills; RConsumed += o.RConsumed; OldFires += o.OldFires;
            STurns += o.STurns; SFoe += o.SFoe; SAlly += o.SAlly; SPulled += o.SPulled; SFoeDealt += o.SFoeDealt; SAllyDealt += o.SAllyDealt;
            SAllyKills += o.SAllyKills; LayerAdds += o.LayerAdds; HexOnTome += o.HexOnTome; Charges += o.Charges; Erupts += o.Erupts;
            BossHeal += o.BossHeal; BossMaxEnd += o.BossMaxEnd; BossHpEnd += o.BossHpEnd;
            for (int i = 0; i < Hist.Length; i++) Hist[i] += o.Hist[i];
            for (int i = 0; i <= TurnCap; i++) { ScarByT[i] += o.ScarByT[i]; TomeByT[i] += o.TomeByT[i]; TeamByT[i] += o.TeamByT[i]; HealByT[i] += o.HealByT[i]; AliveByT[i] += o.AliveByT[i]; }
            foreach (var (k, v) in o.AllyHitBy) AllyHitBy[k] = AllyHitBy.GetValueOrDefault(k) + v;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++;
            Turns += r.Turns;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) Surv++; }
            var foeIds = e.Select(u => u.InstanceId).ToHashSet();
            foreach (var ev in r.Events.Where(ev => ev.Kind == BattleEventKind.Summon && ev.Team == BattleContext.EnemyTeam))
                if (ev.TargetId is int t) foeIds.Add(t);
            var mine = p.ToDictionary(u => u.InstanceId, u => u.Def.Name);
            UnitState? tome = p.FirstOrDefault(u => u.Def.Id == UnitCatalog.Tome.Id);
            if (r.PlayerWon)
                WinT += r.Events.Where(ev => ev.Kind == BattleEventKind.Death && ev.TargetId is int d && foeIds.Contains(d)).Select(ev => ev.Turn).DefaultIfEmpty(r.Turns).Max();
            int firstDeath = 0, tomeDeath = 0;
            var deadAt = new Dictionary<int, int>();
            foreach (var ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.Damage && ev.TargetId is int t)
                {
                    int tt = Math.Clamp(ev.Turn, 0, TurnCap);
                    if (foeIds.Contains(t))
                    {
                        if (ev.Turn <= TurnCap) { Dealt20 += ev.Amount; TeamByT[tt] += ev.Amount; }
                        if (tome is not null && ev.ActorId == tome.InstanceId && ev.Turn <= TurnCap) TomeByT[tt] += ev.Amount;
                    }
                    else if (tome is not null && ev.ActorId == tome.InstanceId && mine.TryGetValue(t, out string? nm) && t != tome.InstanceId)
                        AllyHitBy[nm] = AllyHitBy.GetValueOrDefault(nm) + 1;
                }
                else if (ev.Kind == BattleEventKind.Heal && ev.TargetId is int h && foeIds.Contains(h))
                {
                    BossHeal += ev.Amount;
                    if (ev.Turn <= TurnCap) HealByT[Math.Clamp(ev.Turn, 0, TurnCap)] += ev.Amount;
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
                STurns += tm.SprayTurns; SFoe += tm.SprayFoe; SAlly += tm.SprayAlly; SPulled += tm.SprayPulled;
                SFoeDealt += tm.SprayFoeDealt; SAllyDealt += tm.SprayAllyDealt; SAllyKills += tm.SprayAllyKills; Charges += tm.Charges;
                if (tm.RuptureLayerHist is { } hh) for (int i = 0; i < Hist.Length; i++) Hist[i] += hh[i];
                if (tm.RuptureScarByTurn is { } sb) for (int i = 0; i <= TurnCap; i++) ScarByT[i] += sb[i];
            }
            OldFires += r.FinisherFires;
            if (tome is not null)
                foreach (var l in r.Log)
                {
                    if (l.Text.Contains("の泥が " + tome.Name + " に絡みつく", StringComparison.Ordinal)) HexOnTome++;
                    else if (l.Text.Contains("が溜め込んだ泥を撒き散らす", StringComparison.Ordinal)) Erupts++;
                }
            var boss = e.FirstOrDefault(u => u.Def.Id == EnemyCatalog.BossRegular.Id);
            if (boss is not null) { BossMaxEnd += boss.MaxHp; BossHpEnd += Math.Max(0, boss.Hp); }
        }

        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
    }

    static (BattleResult R, List<UnitState> P, List<UnitState> E) Fight(Formation f, Func<List<UnitState>> wave, int seed, FinisherRule rule, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = wave();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose, finisher: rule);
        return (r, p, e);
    }

    static Agg Measure(Formation f, Func<List<UnitState>> make, FinisherRule rule, int from = 0, int n = Seeds)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i =>
        {
            var (r, p, e) = Fight(f, make, from + i, rule);
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

    static Dictionary<(int, string, string), Agg> MeasureAll(Ver[] vers)
    {
        var res = new Dictionary<(int, string, string), Agg>();
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in vers)
            {
                var f = Boards[bi].Make(v.Tome);
                foreach (var w in Waves) res[(bi, v.Name, w.Name)] = Measure(f, w.Make, v.Rule);
            }
        return res;
    }

    static void RunAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = MeasureAll(Vers);
        Console.WriteLine("# 第281期 トメの転生 —— 台 × 版 × 波（seed 0..199）");
        Console.WriteLine();
        for (int bi = 0; bi < Boards.Length; bi++)
            Console.WriteLine($"- 台{bi}（{Boards[bi].Group}）{Boards[bi].Name} ＝ " + BA.SeatsNamed(Boards[bi].Make(UnitCatalog.Tome)));
        foreach (var v in Vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine("- 波: 本編第2〜5波（`compare` と同じ口・倍率 115/115）／ ボス ＝ 規定形（倍率なし）");
        Console.WriteLine();
        Console.WriteLine("## 表1 勝率（%）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " | 第2〜5波 平均 | T0 差 |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")) + "--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            double m0 = Waves.Where(w => w.Group == "本編").Select(w => res[(bi, "T0", w.Name)].Win).Average();
            foreach (var v in Vers)
            {
                double main = Waves.Where(w => w.Group == "本編").Select(w => res[(bi, v.Name, w.Name)].Win).Average();
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", Waves.Select(w => F1(res[(bi, v.Name, w.Name)].Win))) + $" | {F1(main)} | {(v.Name == "T0" ? "" : Sgn(main - m0))} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 表2 倒しT（勝った戦）／ 崩れ始め（味方の最初の死亡のT・起きた戦の割合）／ トメの死亡T");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers)
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var a = res[(bi, v.Name, w.Name)];
                    string fd = a.FirstDeath == 0 ? "—" : $"{(double)a.FirstDeathT / a.FirstDeath:F1}（{100.0 * a.FirstDeath / a.N:F0}%）";
                    string td = a.TomeDied == 0 ? "—" : $"{(double)a.TomeDeathT / a.TomeDied:F1}";
                    return $"{WinT(a)} ／ {fd} ／ {td}";
                })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 表3 機構の実測（1戦平均・本編第2〜5波 ／ ボス）");
        Console.WriteLine();
        Console.WriteLine("炸 ＝ 炸裂（列越え）／ 層 ＝ 炸裂の平均層（最大）／ 炸害 ＝ 炸裂で減らした HP ／ 爪 ＝ 爪痕で削った最大HP ／ 炸殺 ＝ 炸裂で倒した数 ／ 旧 ＝ 旧トメの倍打ち（`FinisherFires`）／ ");
        Console.WriteLine("乱 ＝ 乱射の手番 ／ 乱敵・乱味 ＝ 敵・味方に当たった発（減らした HP）／ 引 ＝ 標に引かれた発 ／ 味殺 ＝ 乱射で倒れた味方 ／ 層足 ＝ 既に標のある敵に層を足した回数（書き手の合計）／ 呪 ＝ ムドの呪いがトメに付いた回数 ／ 暴 ＝ ムドの暴発（味方側の全数）／ 溜 ＝ 溜めの手番 ／ T ＝ 決着T");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 群 | 炸（越） | 層（最大） | 炸害 | 爪 | 炸殺 | 旧 | 乱 | 乱敵（害） | 乱味（害） | 引 | 味殺 | 層足 | 呪 | 暴 | 溜 | T |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers)
                foreach (string g in new[] { "本編", "ボス" })
                {
                    var a = new Agg();
                    foreach (var w in Waves.Where(w => w.Group == g)) a.Merge(res[(bi, v.Name, w.Name)]);
                    string layer = a.RFires == 0 ? "—" : $"{(double)a.RLayerSum / a.RFires:F2}（{a.RLayerMax}）";
                    Console.WriteLine($"| {bi} | {v.Name} | {g} | {Per(a.RFires, a.N)}（{Per(a.RCross, a.N)}）| {layer} | {Per1(a.RDealt, a.N)} | {Per1(a.RScar, a.N)} | {Per(a.RKills, a.N)} | {Per(a.OldFires, a.N)} | {Per(a.STurns, a.N)} | {Per(a.SFoe, a.N)}（{Per1(a.SFoeDealt, a.N)}）| {Per(a.SAlly, a.N)}（{Per1(a.SAllyDealt, a.N)}）| {Per(a.SPulled, a.N)} | {Per(a.SAllyKills, a.N)} | {Per(a.LayerAdds, a.N)} | {Per(a.HexOnTome, a.N)} | {Per(a.Erupts, a.N)} | {Per(a.Charges, a.N)} | {Per(a.Turns, a.N)} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 表4 層の深さの分布（炸裂の回数・本編第2〜5波の合計 ／ ボス）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 群 | 層1 | 層2 | 層3 | 層4 | 層5 | 層6〜8 | 層9 以上 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers.Where(v => v.Name != "T0"))
                foreach (string g in new[] { "本編", "ボス" })
                {
                    var a = new Agg();
                    foreach (var w in Waves.Where(w => w.Group == g)) a.Merge(res[(bi, v.Name, w.Name)]);
                    Console.WriteLine($"| {bi} | {v.Name} | {g} | {a.Hist[1]} | {a.Hist[2]} | {a.Hist[3]} | {a.Hist[4]} | {a.Hist[5]} | {a.Hist[6] + a.Hist[7] + a.Hist[8]} | {a.Hist[9]} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 表5 乱射の味方撃ちの宛先（発の数・本編第2〜5波 ＋ ボスの合計・T1）");
        Console.WriteLine();
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            var a = new Agg();
            foreach (var w in Waves) a.Merge(res[(bi, "T1", w.Name)]);
            Console.WriteLine($"- 台{bi}: " + (a.AllyHitBy.Count == 0 ? "なし" : string.Join(" ／ ", a.AllyHitBy.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}"))));
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>帯B（seed 200..599）の追試: 台 × 版 × 本編第2〜5波の勝率（verbose なし）。</summary>
    static void BandB()
    {
        Console.WriteLine("# 第281期 帯B（seed 200..599）の追試 —— 本編第2〜5波の勝率");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | T0 差 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            double m0 = 0;
            foreach (var v in Vers)
            {
                var f = Boards[bi].Make(v.Tome);
                var cells = new double[4];
                for (int w = 1; w <= 4; w++)
                {
                    int wins = 0, ww = w;
                    Parallel.For(200, 600, s => { if (BattleEngine.Run(f, EnemyCatalog.Stages[ww].Enemy, s, verbose: false, finisher: v.Rule).PlayerWon) Interlocked.Increment(ref wins); });
                    cells[w - 1] = 100.0 * wins / 400;
                }
                double m = cells.Average();
                if (v.Name == "T0") m0 = m;
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", cells.Select(F1)) + $" | {F1(m)} | {(v.Name == "T0" ? "" : Sgn(m - m0))} |");
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // ボスの到達度（指示書 §5 の実測版）
    // ---------------------------------------------------------------------------------
    static void Boss()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boss = Waves.First(w => w.Name == "ボス");
        Console.WriteLine($"# 第281期 ボスの到達度 —— 規定形（HP {EnemyCatalog.BossRegular.MaxHp}・回復は最大HPの 40%）× 台 × 版（seed 0..199・倍率なし）");
        Console.WriteLine();
        Console.WriteLine("ターン別は**その手番まで生きていた戦も死んでいた戦も含めた 1戦平均**（決着した戦はその後 0）。爪痕 ＝ そのターンに削った最大HP、回復 ＝ 勇者が受けた回復、トメ ＝ トメが勇者に入れた HP、隊 ＝ 隊全体が勇者に入れた HP、生存 ＝ 味方の生存数。");
        Console.WriteLine();
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            Console.WriteLine($"## 台{bi} {Boards[bi].Name} ＝ {BA.SeatsNamed(Boards[bi].Make(UnitCatalog.Tome))}");
            Console.WriteLine();
            Console.WriteLine("| 版 | 勝率 | 崩れ始め | トメの死亡T | 炸裂（層） | 爪痕累計 | 最後の最大HP | 勇者の回復（総） | 隊の与ダメ（20T） | トメの与ダメ |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            var agg = new Dictionary<string, Agg>();
            foreach (var v in Vers)
            {
                var a = Measure(Boards[bi].Make(v.Tome), boss.Make, v.Rule);
                agg[v.Name] = a;
                string layer = a.RFires == 0 ? "—" : $"{(double)a.RFires / a.N:F2}（{(double)a.RLayerSum / a.RFires:F2}）";
                Console.WriteLine($"| {v.Name} | {F1(a.Win)} | {(a.FirstDeath == 0 ? "—" : ((double)a.FirstDeathT / a.FirstDeath).ToString("F1"))} | {(a.TomeDied == 0 ? "—" : ((double)a.TomeDeathT / a.TomeDied).ToString("F1"))} | {layer} | {Per1(a.RScar, a.N)} | {Per1(a.BossMaxEnd, a.N)} | {Per1(a.BossHeal, a.N)} | {Per1(a.Dealt20, a.N)} | {Per1(a.TomeByT.Sum(), a.N)} |");
            }
            Console.WriteLine();
            foreach (string vn in new[] { "T1", "T2" })
            {
                var a = agg[vn];
                Console.WriteLine($"### {vn} のターン別（1戦平均）");
                Console.WriteLine();
                Console.WriteLine("| T | " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => $"T{t}")) + " |");
                Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, 8).Select(_ => "--:|")));
                Console.WriteLine("| 爪痕 | " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => Per1(a.ScarByT[t], a.N))) + " |");
                Console.WriteLine("| 回復 | " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => Per1(a.HealByT[t], a.N))) + " |");
                Console.WriteLine("| トメ | " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => Per1(a.TomeByT[t], a.N))) + " |");
                Console.WriteLine("| 隊 | " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => Per1(a.TeamByT[t], a.N))) + " |");
                Console.WriteLine("| 生存 | " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => Per(a.AliveByT[t], a.N))) + " |");
                Console.WriteLine();
            }
        }
        // 律速の分解: T1 を基準に、倍率（＝供給 s を掛けたのと同じ打点）と寿命（隊の全員の最大HP × k）の格子。
        Console.WriteLine("## 律速の分解（T1・倍率 × 寿命の格子）");
        Console.WriteLine();
        Console.WriteLine("倍率 ＝ `FinisherRule.Multiplier`（2 が規定。供給 s を同じ比で増やしたのと同じ打点）／ 寿命 ＝ 隊の全員の最大HP × k（崩れを遅らせる）。");
        Console.WriteLine("セル ＝ 勝率（倒しT）／ 爪痕累計。**診断の仮想の版で、採否の候補ではない。**");
        Console.WriteLine();
        int[] mults = { 2, 4, 8, 16 }, hps = { 1, 2, 3, 5, 10 };
        foreach (int bi in new[] { 3, 0, 2 })
        {
            Console.WriteLine($"### 台{bi} {Boards[bi].Name}");
            Console.WriteLine();
            Console.WriteLine("| 倍率 ＼ 寿命 | " + string.Join(" | ", hps.Select(h => $"×{h}")) + " |");
            Console.WriteLine("|---|" + string.Concat(hps.Select(_ => "--:|")));
            foreach (int m in mults)
            {
                var cells = new List<string>();
                foreach (int h in hps)
                {
                    var f = Boards[bi].Make(UnitCatalog.TomeT1);
                    if (h != 1) f = Tough(f, h);
                    var a = Measure(f, boss.Make, new FinisherRule(m));
                    cells.Add($"{F1(a.Win)}（{WinT(a)}）／ {Per1(a.RScar, a.N)}");
                }
                Console.WriteLine($"| {m} | " + string.Join(" | ", cells) + " |");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>隊の全員の最大HPを <paramref name="x"/> 倍にした写し（律速の分解の仮想の版・Id は同じ）。</summary>
    static Formation Tough(Formation f, int x)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.WithStats(d.MaxHp * x, d.Attack);
        return g;
    }

    // ---------------------------------------------------------------------------------
    // 倍率の掃引（指示書 §0: 掃引は `FinisherRule.Multiplier` 系の1本だけ）
    // ---------------------------------------------------------------------------------
    static void Mult()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第281期 倍率の掃引 —— T1 × `FinisherRule.Multiplier` 1..4（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("壊れ ＝ 第2〜5波が全部 100%。**T0（旧トメ）でも全部 100% の台は「既に張り付き」**と書き、新たに張り付いた台だけを壊れに数える（第277期 N2 と同型の判定）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " | 第2〜5波 平均 | 炸裂の平均打点（本編） | 壊れ（第2〜5波が全部 100%） |");
        Console.WriteLine("|---|--:|" + string.Concat(Waves.Select(_ => "--:|")) + "--:|--:|---|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            bool pinned0 = Waves.Where(w => w.Group == "本編").All(w => Measure(Boards[bi].Make(UnitCatalog.TomeT0), w.Make, FinisherRule.Default).Win >= 100.0);
            for (int m = 1; m <= 4; m++)
            {
                var f = Boards[bi].Make(UnitCatalog.TomeT1);
                var cells = Waves.Select(w => (w, a: Measure(f, w.Make, new FinisherRule(m)))).ToList();
                var main = cells.Where(c => c.w.Group == "本編").ToList();
                double mean = main.Average(c => c.a.Win);
                var mm = new Agg(); foreach (var c in main) mm.Merge(c.a);
                string per = mm.RFires == 0 ? "—" : ((double)mm.RDealt / mm.RFires).ToString("F1");
                bool broken = main.All(c => c.a.Win >= 100.0);
                Console.WriteLine($"| {bi} | {m} | " + string.Join(" | ", cells.Select(c => F1(c.a.Win))) + $" | {F1(mean)} | {per} | {(broken ? (pinned0 ? "既に張り付き（T0 も）" : "**新たな張り付き**") : "")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 新行の席（指示書 §7: 仮置きから layout で振り直し、採否前に帯で検証）
    // ---------------------------------------------------------------------------------
    /// <summary>新行の顔ぶれの候補。a ＝ 第184期の診断台1（ムド・ボルグ）／ b ＝ `仇討ち (ヒサ×ザン)` のドルガ → トメ（1枚だけ違う）／ c ＝ ヒサ・ザン・トメ・ガルド・ムド。</summary>
    static readonly Dictionary<string, (string What, UnitDef[] M)> SeatSets = new()
    {
        ["a"] = ("第184期の診断台1（ヒサ・ザン・トメ・ムド・ボルグ）", new[] { UnitCatalog.Hisa, UnitCatalog.Zan, UnitCatalog.TomeT1, UnitCatalog.Mudo, UnitCatalog.Borg }),
        ["b"] = ("`仇討ち (ヒサ×ザン)` のドルガ → トメ（ガン・ヒサ・ガルド・トメ・ザン）", new[] { UnitCatalog.Gan, UnitCatalog.Hisa, UnitCatalog.Gald, UnitCatalog.TomeT1, UnitCatalog.Zan }),
        ["c"] = ("ヒサ・ザン・トメ・ガルド・ムド", new[] { UnitCatalog.Hisa, UnitCatalog.Zan, UnitCatalog.TomeT1, UnitCatalog.Gald, UnitCatalog.Mudo }),
    };

    static void Seat(string key)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var members = SeatSets[key].M;
        var cands = new List<(Formation F, double[] A)>();
        foreach (int[] asg in SlotAssignments(members.Length))
        {
            var f = new Formation();
            for (int i = 0; i < members.Length; i++) f[asg[i]] = members[i];
            var cells = new double[4];
            for (int w = 1; w <= 4; w++)
            {
                int wins = 0, ww = w;
                Parallel.For(0, Seeds, s => { if (BattleEngine.Run(f, EnemyCatalog.Stages[ww].Enemy, s, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                cells[w - 1] = 100.0 * wins / Seeds;
            }
            cands.Add((f, cells));
        }
        var ranked = cands.OrderByDescending(c => c.A.Average()).ToList();
        Console.WriteLine($"# 第281期 新行の席 —— {key}: {SeatSets[key].What}（トメは T1）× 120 通り × 本編第2〜5波（帯A ＝ seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("情報セル ＝ 第2〜5波で 0 < x < 100 のセル数（(G14)・帯A）。狙 ＝ ヒサの隣に、ヒサ以外で最大HPが最も大きい駒（矢面を受ける壁）。");
        Console.WriteLine();
        Console.WriteLine("| 順 | 席 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 情報セル | 狙 |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|---|");
        for (int i = 0; i < ranked.Count; i++)
        {
            if (i >= 20 && i < ranked.Count - 3) continue;
            var c = ranked[i];
            Console.WriteLine($"| {i + 1} | {BA.SeatsNamed(c.F)} | " + string.Join(" | ", c.A.Select(F1)) + $" | {F1(c.A.Average())} | {c.A.Count(x => x > 0 && x < 100)} | {(Aim(c.F) ? "○" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 上位8の追試（帯B ＝ seed 200..599）・T1 と T0（同じ席でトメだけ差し替え）");
        Console.WriteLine();
        Console.WriteLine("| 順 | 席 | T1 帯A | T1 帯B | T0 帯B | T1 − T0（帯B） | 情報セル（帯A） | 狙 |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|---|");
        for (int i = 0; i < Math.Min(8, ranked.Count); i++)
        {
            var c = ranked[i];
            double b1 = BandBMean(c.F), b0 = BandBMean(FvSwap(c.F, UnitCatalog.TomeT1, UnitCatalog.TomeT0));
            Console.WriteLine($"| {i + 1} | {BA.SeatsNamed(c.F)} | {F1(c.A.Average())} | {F1(b1)} | {F1(b0)} | {Sgn(b1 - b0)} | {c.A.Count(x => x > 0 && x < 100)} | {(Aim(c.F) ? "○" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");

        static bool Aim(Formation f)
        {
            var occ = f.Occupied().ToList();
            int hs = occ.First(o => o.Def.Id == UnitCatalog.Hisa.Id).Slot;
            var wall = occ.Where(o => o.Def.Id != UnitCatalog.Hisa.Id).OrderByDescending(o => o.Def.MaxHp).First();
            return FormationRules.AreAdjacent(hs, wall.Slot);
        }
    }

    static double BandBMean(Formation f)
    {
        var cells = new double[4];
        for (int w = 1; w <= 4; w++)
        {
            int wins = 0, ww = w;
            Parallel.For(200, 600, s => { if (BattleEngine.Run(f, EnemyCatalog.Stages[ww].Enemy, s, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
            cells[w - 1] = 100.0 * wins / 400;
        }
        return cells.Average();
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
        Console.WriteLine("# 第281期 tome281 check —— 自己検査");
        Console.WriteLine();

        // (a) 新札の保持者は選べる駒・退役・プリセットに 0 枚
        var news = new[] { TraitId.Rupture, TraitId.RuptureScar, TraitId.Spray };
        int holders = UnitCatalog.Everyone.Count(d => d.Traits.Any(news.Contains));
        int presetHolders = CompareBuilds().Concat(CrossBuilds()).Sum(r => r.F.Occupied().Count(o => o.Def.Traits.Any(news.Contains)));
        Expect("(a) 炸裂・爪痕・乱射の保持者は `UnitCatalog.Everyone` と `compare` ／ 交差帯の行に 0 枚", holders == 0 && presetHolders == 0, $"{holders} ／ {presetHolders}");

        // (b) T0 の定義は規定のトメと同じ
        Expect("(b) T0 の定義（札・数値・文）は規定のトメと同じ",
            UnitCatalog.TomeT0.Traits.SequenceEqual(UnitCatalog.Tome.Traits) && UnitCatalog.TomeT0.MaxHp == UnitCatalog.Tome.MaxHp
            && UnitCatalog.TomeT0.Attack == UnitCatalog.Tome.Attack && UnitCatalog.TomeT0.Speed == UnitCatalog.Tome.Speed
            && UnitCatalog.TomeT0.PlusText == UnitCatalog.Tome.PlusText && UnitCatalog.TomeT0.Actions is null);

        // (c) T0 の写しの台本が規定のトメと一致（台 × 本編第2〜5波 × seed 0..49）
        int diff = 0, total = 0;
        for (int bi = 0; bi < Boards.Length; bi++)
            for (int w = 1; w <= 4; w++)
                for (int s = 0; s < 50; s++)
                {
                    int ww = w;
                    Func<List<UnitState>> mk = () => BattleEngine.Materialize(EnemyCatalog.Stages[ww].Enemy, BattleContext.EnemyTeam);
                    var a = Fight(Boards[bi].Make(UnitCatalog.Tome), mk, s, FinisherRule.Default).R;
                    var b = Fight(Boards[bi].Make(UnitCatalog.TomeT0), mk, s, FinisherRule.Default).R;
                    total++;
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns || !a.Log.Select(l => l.Text).SequenceEqual(b.Log.Select(l => l.Text))) diff++;
                }
        Expect("(c) T0 の写しの台本は規定のトメと一致", diff == 0, $"{total} 戦中 {diff} 件ずれ");

        // (d) T0 の勝率が `docs/balance.md` の止めの2行と同じ口で出る（Formation 版の Run と UnitState 版の Run が同じ）
        int dd = 0;
        for (int bi = 0; bi < 2; bi++)
            for (int w = 1; w <= 4; w++)
            {
                int ww = w, wa = 0, wb = 0;
                var f = Boards[bi].Make(UnitCatalog.Tome);
                for (int s = 0; s < Seeds; s++)
                {
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[ww].Enemy, s, verbose: false).PlayerWon) wa++;
                    if (Fight(f, () => BattleEngine.Materialize(EnemyCatalog.Stages[ww].Enemy, BattleContext.EnemyTeam), s, FinisherRule.Default, false).R.PlayerWon) wb++;
                }
                if (wa != wb) dd++;
            }
        Expect("(d) 診断の口（Materialize ＋ UnitState 版の Run）は `compare` の口（Formation 版）と勝ち数が一致（止めの2行 × 第2〜5波）", dd == 0, $"{dd} セルずれ");

        // (e) 炸裂の計数: 撃った層の分布の和 ＝ 撃った回数 ／ 列越え ≦ 炸裂 ／ 消費 ＝ 炸裂（T1・Consume）
        var bossW = Waves.First(w => w.Name == "ボス");
        var t1 = new Agg(); var t1c = new Agg(); var t2 = new Agg();
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var w in Waves)
            {
                t1.Merge(Measure(Boards[bi].Make(UnitCatalog.TomeT1), w.Make, FinisherRule.Default, 0, 50));
                t1c.Merge(Measure(Boards[bi].Make(UnitCatalog.TomeT1), w.Make, new FinisherRule(2, false), 0, 50));
                t2.Merge(Measure(Boards[bi].Make(UnitCatalog.TomeT2), w.Make, FinisherRule.Default, 0, 50));
            }
        Expect("(e) T1: 層の分布の和 ＝ 炸裂の回数 ／ 列越え ≦ 炸裂 ／ 消費 ≦ 炸裂", t1.Hist.Sum() == t1.RFires && t1.RCross <= t1.RFires && t1.RConsumed <= t1.RFires && t1.RFires > 0,
            $"分布 {t1.Hist.Sum()} ／ 炸裂 {t1.RFires} ／ 越 {t1.RCross} ／ 消費 {t1.RConsumed}");
        Expect("(f) T1-c は層を消さない（消費 0）・層が T1 より深い", t1c.RConsumed == 0 && t1c.RLayerSum * t1.RFires > t1.RLayerSum * t1c.RFires,
            $"T1 平均層 {(double)t1.RLayerSum / Math.Max(1, t1.RFires):F2} ／ T1-c {(double)t1c.RLayerSum / Math.Max(1, t1c.RFires):F2}");
        Expect("(g) 層 2 以上の炸裂が出る（層化が効いている）", t1.Hist.Skip(2).Sum() > 0, $"層2以上 {t1.Hist.Skip(2).Sum()} ／ {t1.RFires}");
        Expect("(h) 乱射は敵味方の両方に当たる・旧の倍打ち（`FinisherFires`）は新トメでは 0", t1.SFoe > 0 && t1.SAlly > 0 && t1.OldFires == 0, $"敵 {t1.SFoe} ／ 味 {t1.SAlly} ／ 旧 {t1.OldFires}");
        Expect("(i) T2 は溜めの手番を持つ（溜め ≈ 2 × 術）", t2.Charges > 0, $"溜め {t2.Charges} ／ 炸裂 {t2.RFires} ／ 乱射 {t2.STurns}");

        // (j) 爪痕: ボスの最後の最大HP ＝ 3000 − 爪痕（T1・ボスの台）
        int scarBad = 0, scarN = 0;
        for (int bi = 0; bi < Boards.Length; bi++)
            for (int s = 0; s < 50; s++)
            {
                var (r, p, e) = Fight(Boards[bi].Make(UnitCatalog.TomeT1), bossW.Make, s, FinisherRule.Default);
                var b = e.First(u => u.Def.Id == EnemyCatalog.BossRegular.Id);
                int scar = r.TallyByUnit.TryGetValue(UnitCatalog.Tome.Id, out var tm) ? tm.RuptureScar : 0;
                scarN++;
                if (b.MaxHp != EnemyCatalog.BossRegular.MaxHp - scar) scarBad++;
            }
        Expect("(j) 爪痕: ボスの最後の最大HP ＝ 3,000 − 爪痕の計数（T1・4台 × seed 0..49）", scarBad == 0, $"{scarN} 戦中 {scarBad} 件ずれ");

        // (k) seed 決定的・verbose の有無で勝敗と決着T が一致（T1 ／ T2・全台 × 本編第2〜5波 ＋ ボス × seed 0..19）
        int nd = 0, nv = 0;
        foreach (string vn in new[] { "T1", "T2" })
        {
            var v = VerOf(vn);
            for (int bi = 0; bi < Boards.Length; bi++)
                foreach (var w in Waves)
                    for (int s = 0; s < 20; s++)
                    {
                        var a = Fight(Boards[bi].Make(v.Tome), w.Make, s, v.Rule).R;
                        var b = Fight(Boards[bi].Make(v.Tome), w.Make, s, v.Rule).R;
                        var c = Fight(Boards[bi].Make(v.Tome), w.Make, s, v.Rule, verbose: false).R;
                        if (!a.Log.Select(l => l.Text).SequenceEqual(b.Log.Select(l => l.Text))) nd++;
                        if (a.PlayerWon != c.PlayerWon || a.Turns != c.Turns) nv++;
                    }
        }
        Expect("(k) seed 決定的（同じ seed の台本が一致）", nd == 0, $"{nd} 件");
        Expect("(l) verbose の有無で勝敗と決着T が一致", nv == 0, $"{nv} 件");

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "すべて ○。" : $"× が {fail} 件。");
    }

    static void LogOne(int board, string ver, string wave, int seed)
    {
        var v = VerOf(ver);
        var w = Waves.First(x => x.Name == (int.TryParse(wave, out int n) ? $"第{n}波" : wave));
        var f = Boards[board].Make(v.Tome);
        var (r, _, e) = Fight(f, w.Make, seed, v.Rule);
        Console.WriteLine($"# 台{board} {Boards[board].Name}（{BA.SeatsNamed(f)}）× {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
        var boss = e.FirstOrDefault(u => u.Def.Id == EnemyCatalog.BossRegular.Id);
        if (boss is not null) Console.WriteLine($"（勇者の最後: HP {boss.Hp} ／ 最大HP {boss.MaxHp}）");
    }
}
