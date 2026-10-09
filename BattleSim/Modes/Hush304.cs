using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// hush304 —— 第304期「庇い HC-s の規定化 ＋ 粛の作り直しの版（HB 叩けば破れる ／ HD 抑えきれず砕ける ／ HC 諸刃 ／ HCD）」。
// 指示書は design/PHASE304_HUSH_REWORK_SPEC.md ／ 報告は design/PHASE304_HUSH_REWORK.md。
// 版は**第2波の敵だけ**を差し替える（粛の伝令 → `HusherHB` ／ `HusherHD10` ／ `HusherHD20`・巡礼騎士 → `KnightGR`）。味方の駒は規定のまま。
//
//     dotnet run --project BattleSim -c Release 0 hush304 p0 [seeds]       # §4 Phase 0（粛が止めた数のターンごと ／ 保持者が受けた傷 ／ 騎士の斬り返しの見込み）
//     dotnet run --project BattleSim -c Release 0 hush304 ver [seeds]      # §5-2 代表台 × 本編 第1〜5波 × 版
//     dotnet run --project BattleSim -c Release 0 hush304 wave2 [seeds]    # §5-3 標軸の行 × 第2波（S → HB → HD10 → HD20 → HC → HCD）と版ごとの中身
//     dotnet run --project BattleSim -c Release 0 hush304 cmp [seeds]      # §5-2 `compare` 64 行 × 第2波 × 版・分布・(G2)・§7 の得点
//     dotnet run --project BattleSim -c Release 0 hush304 find [seeds]     # Codex 向け: 「ひび → 砕ける → 割り込みが戻る」が揃った最初の seed
//     dotnet run --project BattleSim -c Release 0 hush304 memo <台の一部> <seed> [S|HB|HD10|HD20|HC|HCD] [最後のT]
//     dotnet run --project BattleSim -c Release 0 hush304 check            # 自己検査
// =====================================================================================
static class Hush304Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": Phase0(int.Parse(A(3, "200"))); return;
            case "ver": Ver(int.Parse(A(3, "200"))); return;
            case "wave2": Wave2(int.Parse(A(3, "200"))); return;
            case "cmp": Cmp(int.Parse(A(3, "200"))); return;
            case "find": Find(int.Parse(A(3, "50"))); return;
            case "memo": Memo(A(3, "三人組"), int.Parse(A(4, "0")), A(5, "HD10"), int.Parse(A(6, "99"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("hush304: モードは p0 / ver / wave2 / cmp / find / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版（第2波の敵）と台
    // ---------------------------------------------------------------------------------
    internal sealed record V(string Key, string Name, UnitDef Herald, UnitDef Knight);
    internal static readonly V VS = new("S", "規定（段0 の後）", EnemyCatalog.Husher, EnemyCatalog.KnightG);
    internal static readonly V VHB = new("HB", "叩けば破れる", EnemyCatalog.HusherHB, EnemyCatalog.KnightG);
    internal static readonly V VHD10 = new("HD10", "抑えきれず砕ける（10）", EnemyCatalog.HusherHD10, EnemyCatalog.KnightG);
    internal static readonly V VHD20 = new("HD20", "抑えきれず砕ける（20）", EnemyCatalog.HusherHD20, EnemyCatalog.KnightG);
    internal static readonly V VHC = new("HC", "諸刃（騎士の斬り返し）", EnemyCatalog.Husher, EnemyCatalog.KnightGR);

    /// <summary>
    /// HCD の HD は §7 の得点で選ぶ（**測る前に決めた線**・報告書 §1）: `compare` 64 行の第2波で 20% 未満の行が少ない方。
    /// 同数なら、範囲 ／ 貫きのある行の第2波の平均の上げが小さい方。それも同じなら HD20（緩い方を採らない）。
    /// </summary>
    static V? _hcd;
    static V HCD
    {
        get
        {
            if (_hcd is not null) return _hcd;
            var pick = PickHd(200);
            _hcd = new("HCD", $"HC ＋ {pick.Key}", pick.Herald, EnemyCatalog.KnightGR);
            return _hcd;
        }
    }
    static V[] Vers => new[] { VS, VHB, VHD10, VHD20, VHC, HCD };

    internal static Formation EnemyOf(V v)
    {
        var g = StagesH305[1].Enemy.Clone();
        foreach ((int slot, UnitDef d) in StagesH305[1].Enemy.Occupied())
            g[slot] = ReferenceEquals(d, EnemyCatalog.Husher) ? v.Herald : ReferenceEquals(d, EnemyCatalog.KnightG) ? v.Knight : d;
        return g;
    }
    internal static Func<List<UnitState>> Wave2Of(V v) { var f = EnemyOf(v); return () => BattleEngine.Materialize(f, BattleContext.EnemyTeam); }

    static Doha297Diag.Wave[] Waves() => Doha297Diag.Waves();
    internal static Doha297Diag.Wave WaveOf(string k) => Waves().First(w => w.Key == k);
    internal static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static Formation CompareRow(string n) => CompareBuilds().First(r => r.Name == n).F;
    const string Econ = "標経済 (ヒサ×ザン×ミサ)";
    internal static (string Name, Formation F)[] Boards() =>
        Presets.Playtest.Where(r => r.Name.StartsWith("試遊・標", StringComparison.Ordinal) && r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hisa))).Select(r => (r.Name, r.F))
            .Append((Econ, CompareRow(Econ)))
            .Append((Hisa301Diag.NoZanName, Hisa301Diag.NoZan)).ToArray();
    internal static (string Name, Formation F)[] HisaRows() => CompareBuilds().Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hisa))).ToArray();
    internal static (string Name, Formation F)[] MarkRows() => Boards().Concat(HisaRows().Where(r => r.Name != Econ)).ToArray();

    /// <summary>範囲 ／ 貫きの駒（手番の攻撃型が単体でない駒）がいる編成か。§7 の「楽すぎる台」の候補。</summary>
    internal static bool HasArea(Formation f) => BattleEngine.Materialize(f, BattleContext.PlayerTeam).Any(u => u.CurrentPattern != AttackPattern.Single);

    // ---------------------------------------------------------------------------------
    // 1戦の集計
    // ---------------------------------------------------------------------------------
    internal sealed class Agg
    {
        public long N, Wins, WinT, Turns, HeraldDeadN, HeraldDeadT, HeraldAliveTurns,
            Breaks, OpenTurns, OpenPassOpp, OpenPassOwn, ShatterN, ShatterT, ShatterPassOpp, ShatterPassOwn, PassKnight,
            KnightAsked, KnightRip, KnightHushed, KnightHeld, KnightDealt,
            BlockOpp, BlockOwn, HeraldHits, HeraldHitTurns, KnightChance, KnightChanceHushed;
        public readonly long[] BlocksByTurn = new long[12], ShatterByTurn = new long[12];
        public readonly long[][] ByRoute = Enumerable.Range(0, OutOfTurnRoutes.Count).Select(_ => new long[2]).ToArray();
        public readonly Dictionary<string, long> RipTaken = new();
        public readonly List<int> Est10 = new(), Est20 = new(), Est15 = new();   // Est15 は第305期
        public void Add(Agg o)
        {
            foreach (var f in typeof(Agg).GetFields())
                if (f.FieldType == typeof(long)) f.SetValue(this, (long)f.GetValue(this)! + (long)f.GetValue(o)!);
            for (int i = 0; i < BlocksByTurn.Length; i++) { BlocksByTurn[i] += o.BlocksByTurn[i]; ShatterByTurn[i] += o.ShatterByTurn[i]; }
            for (int i = 0; i < ByRoute.Length; i++) { ByRoute[i][0] += o.ByRoute[i][0]; ByRoute[i][1] += o.ByRoute[i][1]; }
            foreach (var (k, v) in o.RipTaken) RipTaken[k] = RipTaken.GetValueOrDefault(k) + v;
            Est10.AddRange(o.Est10); Est20.AddRange(o.Est20); Est15.AddRange(o.Est15);
        }
        public double P(long x) => N == 0 ? 0 : (double)x / N;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public double WinTurns => Wins == 0 ? 0 : (double)WinT / Wins;
    }

    internal static Agg One(Formation f, Func<List<UnitState>> enemy, int seed, bool verbose)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = enemy();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        var a = new Agg { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var hb = r.BoardRules.HushBlocked;
        a.BlockOpp = hb[1]; a.BlockOwn = hb[0];
        for (int i = 0; i < a.ByRoute.Length; i++) { a.ByRoute[i][0] = r.BoardRules.HushByRoute[i][0]; a.ByRoute[i][1] = r.BoardRules.HushByRoute[i][1]; }
        if (r.TallyByUnit.TryGetValue("husher", out var ht))
        {
            a.Breaks = ht.HushBreaks; a.OpenPassOpp = ht.HushOpenPassOpp; a.OpenPassOwn = ht.HushOpenPassOwn;
            a.ShatterPassOpp = ht.HushShatterPassOpp; a.ShatterPassOwn = ht.HushShatterPassOwn; a.PassKnight = ht.HushPassKnight;
            if (ht.HushShatterTurn > 0) { a.ShatterN = 1; a.ShatterT = ht.HushShatterTurn; a.ShatterByTurn[Math.Min(11, ht.HushShatterTurn)]++; }
        }
        if (r.TallyByUnit.TryGetValue("knight_g", out var kt))
        {
            a.KnightAsked = kt.KnightAsked; a.KnightRip = kt.KnightRipostes; a.KnightHushed = kt.KnightHushed; a.KnightHeld = kt.KnightHeld; a.KnightDealt = kt.KnightDealt;
        }
        var herald = e.FirstOrDefault(u => u.Def.Traits.Contains(TraitId.Hush));
        if (herald is null) return a;
        int deadT = herald.LastDeathTurn;
        if (deadT > 0) { a.HeraldDeadN = 1; a.HeraldDeadT = deadT; }
        a.HeraldAliveTurns = deadT > 0 ? deadT : r.Turns;
        if (!verbose) return a;

        // 台本から: 粛が単独の原因で止めた数（ターンごと）・保持者が受けた傷・騎士の斬り返しの機会（粛が無ければ出た数）・沈黙が破れていたターン
        var players = p.Select(u => u.InstanceId).ToHashSet();
        var knights = e.Where(u => u.Def.Id == "knight_g").Select(u => u.InstanceId).ToHashSet();
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Id);
        bool died = false; int cum = 0, est10 = 0, est20 = 0, est15 = 0;
        var hitTurns = new HashSet<int>(); var chance = new HashSet<(int, int)>(); var openTurns = new HashSet<int>(); int openFrom = -1;
        int? lastRipKnight = null;
        foreach (var x in r.Events)
        {
            if (x.Kind == BattleEventKind.Sealed && x.Text == SealedLabels.Hush)
            {
                a.BlocksByTurn[Math.Min(11, x.Turn)]++;
                cum++;
                if (cum == HushVariantTrait.Shatter10) est10 = x.Turn;
                if (cum == HushVariantTrait.Shatter20) est20 = x.Turn;
                if (cum == HushVariantTrait.Shatter15) est15 = x.Turn;
            }
            if (x.Kind == BattleEventKind.HushState && x.Text == HushStateLabels.Break) openFrom = x.Turn;
            if (x.Kind == BattleEventKind.HushState && x.Text == HushStateLabels.Close && openFrom > 0) { for (int t = openFrom; t <= x.Turn; t++) openTurns.Add(t); openFrom = -1; }
            if (!died && x.Kind == BattleEventKind.Damage && x.TargetId == herald.InstanceId && x.Amount > 0) { a.HeraldHits++; hitTurns.Add(x.Turn); }
            if (x.Kind == BattleEventKind.Death && x.TargetId == herald.InstanceId) died = true;
            if (x.Kind == BattleEventKind.Damage && x.TargetId is int kid && knights.Contains(kid) && x.ActorId is int ai && players.Contains(ai) && !x.Reaction && x.Amount > 0 && x.HpAfter > 0)
            {
                if (chance.Add((kid, x.Turn)) && !died) a.KnightChanceHushed++;
            }
            // 斬り返しで受けた量（駒別）: 騎士が出どころの Reaction の Damage
            if (x.Kind == BattleEventKind.Damage && x.Reaction && x.ActorId is int ri && knights.Contains(ri) && x.TargetId is int vi && players.Contains(vi))
            {
                string k = names[vi];
                a.RipTaken[k] = a.RipTaken.GetValueOrDefault(k) + x.Amount;
                lastRipKnight = ri;
            }
        }
        if (openFrom > 0) for (int t = openFrom; t <= (deadT > 0 ? deadT : r.Turns); t++) openTurns.Add(t);
        a.OpenTurns = openTurns.Count(t => t <= a.HeraldAliveTurns);
        a.HeraldHitTurns = hitTurns.Count;
        a.KnightChance = chance.Count;
        a.Est10.Add(est10); a.Est20.Add(est20); a.Est15.Add(est15);
        return a;
    }

    internal static Agg Many(Formation f, Func<List<UnitState>> enemy, int seeds, bool verbose = false, int from = 0)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, s => parts[s] = One(f, enemy, from + s, verbose));
        var a = new Agg();
        foreach (var x in parts) a.Add(x);
        return a;
    }

    internal static string Pct(long x, long n) => n == 0 ? "—" : $"{100.0 * x / n:F0}%";
    internal static string Avg(long s, long n) => n == 0 ? "—" : $"{(double)s / n:F1}";
    internal static string Cell(Agg a) => a.Wins == 0 ? $"{a.Win:F1}" : $"{a.Win:F1}（{a.WinTurns:F1}）";
    internal static string Est(List<int> l) { var hit = l.Where(t => t > 0).OrderBy(t => t).ToList(); return hit.Count == 0 ? $"—（届かない {100:F0}%）" : $"中央 T{hit[hit.Count / 2]}・平均 T{hit.Average():F1}（届かない {100.0 * (l.Count - hit.Count) / l.Count:F0}%）"; }

    // ---------------------------------------------------------------------------------
    // §4 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0(int seeds)
    {
        var w2 = Wave2Of(VS);
        Console.WriteLine($"# 第304期 Phase 0（seed 0..{seeds - 1}・規定 S ＝ 段0 の後・第2波）");
        Console.WriteLine();
        // `compare` の第2波の上位の行 ＝ 粛が単独の原因で止めた数（1戦）の多い順に 10 行
        var cmpRows = CompareBuilds().Select(r => (r.Name, r.F, A: Many(r.F, w2, seeds, verbose: true))).ToList();
        var top = cmpRows.OrderByDescending(x => x.A.BlockOpp + x.A.BlockOwn).Take(10).ToList();
        var rows = Boards().Select(b => (b.Name, b.F, A: Many(b.F, w2, seeds, verbose: true))).Concat(top).ToList();

        Console.WriteLine("## 1. 粛が単独の原因で止めた行動（1戦・ターンごと）と、HD10 ／ HD20 で砕けるT の見込み");
        Console.WriteLine();
        Console.WriteLine("代表台 ＋ `compare` の行のうち止めた数（1戦）の多い 10 行。見込みは「止めた数の累計が 10 ／ 20 に達したT」（粛が生きている間だけ数える・届かない ＝ 粛が倒れるか決着が先）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 勝率（倒しT） | 止めた（1戦・味方 ／ 敵） | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8+ | HD10 の見込み | HD20 の見込み | 粛が倒れた戦（T） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|---|---|");
        foreach (var (n, _, a) in rows)
        {
            var bt = Enumerable.Range(1, 7).Select(t => $"{a.P(a.BlocksByTurn[t]):F2}").Append($"{a.P(a.BlocksByTurn.Skip(8).Sum()):F2}");
            Console.WriteLine($"| {n} | {Cell(a)} | {a.P(a.BlockOpp + a.BlockOwn):F2}（{a.P(a.BlockOpp):F2} ／ {a.P(a.BlockOwn):F2}） | " + string.Join(" | ", bt) + $" | {Est(a.Est10)} | {Est(a.Est20)} | {Pct(a.HeraldDeadN, a.N)}（{Avg(a.HeraldDeadT, a.HeraldDeadN)}） |");
        }
        Console.WriteLine();
        Console.WriteLine("経路の内訳（1戦・味方の行動 ／ 敵の行動・0 の経路は省く）:");
        Console.WriteLine();
        foreach (var (n, _, a) in rows)
        {
            var parts = Enumerable.Range(0, OutOfTurnRoutes.Count).Where(i => a.ByRoute[i][0] + a.ByRoute[i][1] > 0)
                .Select(i => $"{OutOfTurnRoutes.Names[i]} {a.P(a.ByRoute[i][1]):F2} ／ {a.P(a.ByRoute[i][0]):F2}");
            Console.WriteLine($"- {n}: " + (parts.Any() ? string.Join("・", parts) : "—"));
        }
        Console.WriteLine();
        Console.WriteLine("## 2. 粛の保持者が受けた傷（HB で沈黙が破れているターンの見込み）");
        Console.WriteLine();
        Console.WriteLine("受けた傷 ＝ 粛の伝令の HP が減った一撃の数（1戦・倒れるまで）。傷を受けたターン ＝ そのうち別のターンの数。割合 ＝ 傷を受けたターン ÷ 粛が生きていたターン（HB では、傷を受けたターンから保持者の次の手番までが破れる——下限の見込み）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 受けた傷（1戦） | 傷を受けたターン（1戦） | 粛が生きていたターン（1戦） | 割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var (n, _, a) in rows)
            Console.WriteLine($"| {n} | {a.P(a.HeraldHits):F2} | {a.P(a.HeraldHitTurns):F2} | {a.P(a.HeraldAliveTurns):F2} | {(a.HeraldAliveTurns == 0 ? "—" : $"{100.0 * a.HeraldHitTurns / a.HeraldAliveTurns:F0}%")} |");
        Console.WriteLine();
        Console.WriteLine("## 3. HC の見込み（騎士の斬り返しの機会）");
        Console.WriteLine();
        Console.WriteLine("機会 ＝ 騎士が味方の手番の攻撃（反撃でない）で傷を受け、生き残ったターンの数（騎士ごと・1ターン1回・1戦）。粛がいなければ全部斬り返す。止められる ＝ そのうち粛が生きていたターン。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 機会（1戦） | うち粛の下（止められる） | 粛が倒れた後（出る） |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var (n, _, a) in rows)
            Console.WriteLine($"| {n} | {a.P(a.KnightChance):F2} | {a.P(a.KnightChanceHushed):F2} | {a.P(a.KnightChance - a.KnightChanceHushed):F2} |");
        Console.WriteLine();
        Console.WriteLine("## 4. `compare` 64 行の第2波（S）の分布と、止めた数の分布");
        Console.WriteLine();
        Console.WriteLine($"- 勝率: {Bins(cmpRows.Select(x => x.A.Win))}");
        Console.WriteLine($"- 止めた数（1戦）: 0 の行 {cmpRows.Count(x => x.A.BlockOpp + x.A.BlockOwn == 0)}・0〜5 {cmpRows.Count(x => { double b = x.A.P(x.A.BlockOpp + x.A.BlockOwn); return b > 0 && b < 5; })}・5〜10 {cmpRows.Count(x => { double b = x.A.P(x.A.BlockOpp + x.A.BlockOwn); return b >= 5 && b < 10; })}・10〜20 {cmpRows.Count(x => { double b = x.A.P(x.A.BlockOpp + x.A.BlockOwn); return b >= 10 && b < 20; })}・20 以上 {cmpRows.Count(x => x.A.P(x.A.BlockOpp + x.A.BlockOwn) >= 20)}");
        Console.WriteLine($"- 20% 未満の行: " + string.Join("・", cmpRows.Where(x => x.A.Win < 20).Select(x => $"{x.Name} {x.A.Win:F1}（止めた {x.A.P(x.A.BlockOpp + x.A.BlockOwn):F1}・HD10 {Est(x.A.Est10)}）")));
        Console.WriteLine($"- 範囲 ／ 貫きのある行: {cmpRows.Count(x => HasArea(x.F))} 行（第2波の平均 {cmpRows.Where(x => HasArea(x.F)).Average(x => x.A.Win):F1}）・ない行: {cmpRows.Count(x => !HasArea(x.F))} 行（{cmpRows.Where(x => !HasArea(x.F)).Average(x => x.A.Win):F1}）");
    }

    internal static string Bins(IEnumerable<double> xs)
    {
        var l = xs.ToList();
        return $"0〜20 {l.Count(x => x < 20)} ／ 20〜50 {l.Count(x => x >= 20 && x < 50)} ／ 50〜80 {l.Count(x => x >= 50 && x < 80)} ／ 80〜100 {l.Count(x => x >= 80)}（行の数・平均 {l.Average():F1}）";
    }

    // ---------------------------------------------------------------------------------
    // §5-2 代表台 × 本編 第1〜5波 × 版
    // ---------------------------------------------------------------------------------
    static void Ver(int seeds)
    {
        var vers = Vers;
        Console.WriteLine($"# 第304期 代表台 × 本編 第1〜5波 × 版（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("版は第2波の敵だけを差し替える（ほかの波は S と同じ敵）。S から ±10pt 以上動いたセルは太字。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var (n, f) in Boards())
        {
            var s0 = Enumerable.Range(0, StagesH305.Count).Select(i => Many(f, WaveOf((i + 1).ToString()).Make, seeds)).ToArray();
            foreach (var v in vers)
            {
                var a = Enumerable.Range(0, StagesH305.Count).Select(i => i == 1 ? Many(f, Wave2Of(v), seeds) : v == VS ? s0[i] : Many(f, WaveOf((i + 1).ToString()).Make, seeds)).ToArray();
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", a.Select((x, i) => v != VS && Math.Abs(x.Win - s0[i].Win) >= 10 ? $"**{Cell(x)}**" : Cell(x))) + " |");
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // §5-3 第2波の表と版ごとの中身
    // ---------------------------------------------------------------------------------
    static void Wave2(int seeds)
    {
        var vers = Vers;
        Console.WriteLine($"# 第304期 標軸の行 × 第2波（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"HCD の HD は {HCD.Name}（§7 の得点・報告書 §1）。勝率（倒しT）／ 粛が倒れた戦。");
        Console.WriteLine();
        var res = new Dictionary<(string, string), Agg>();
        var rows = MarkRows();
        foreach (var (n, f) in rows) foreach (var v in vers) res[(n, v.Key)] = Many(f, Wave2Of(v), seeds, verbose: true);
        Console.WriteLine("| 行 | " + string.Join(" | ", vers.Select(v => v.Key)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", vers.Length)));
        foreach (var (n, _) in rows)
            Console.WriteLine($"| {n} | " + string.Join(" | ", vers.Select(v => { var a = res[(n, v.Key)]; var c = $"{Cell(a)} ／ {Pct(a.HeraldDeadN, a.N)}"; return v != VS && Math.Abs(a.Win - res[(n, "S")].Win) >= 10 ? $"**{c}**" : c; })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 粛が倒れたT（倒れなかった戦の割合）");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", vers.Select(v => v.Key)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", vers.Length)));
        foreach (var (n, _) in rows)
            Console.WriteLine($"| {n} | " + string.Join(" | ", vers.Select(v => { var a = res[(n, v.Key)]; return $"T{Avg(a.HeraldDeadT, a.HeraldDeadN)}（{Pct(a.N - a.HeraldDeadN, a.N)}）"; })) + " |");
        Console.WriteLine();
        Console.WriteLine("## HB —— 破れていたターン・破れている間に出た割り込み（1戦）");
        Console.WriteLine();
        Console.WriteLine("破れた ＝ 沈黙が破れた回数 ／ 割合 ＝ 破れていたターン ÷ 粛が生きていたターン ／ 通った ＝ 破れている間に通ったターン外の行動（味方 ／ 敵）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 勝率 S → HB | 破れた | 割合 | 通った（味方 ／ 敵） | 止まった（S → HB） |");
        Console.WriteLine("|---|---|--:|--:|---|---|");
        foreach (var (n, _) in rows)
        {
            var a = res[(n, "HB")]; var s = res[(n, "S")];
            Console.WriteLine($"| {n} | {s.Win:F1} → {a.Win:F1} | {a.P(a.Breaks):F2} | {(a.HeraldAliveTurns == 0 ? "—" : $"{100.0 * a.OpenTurns / a.HeraldAliveTurns:F0}%")} | {a.P(a.OpenPassOpp):F2} ／ {a.P(a.OpenPassOwn):F2} | {s.P(s.BlockOpp + s.BlockOwn):F2} → {a.P(a.BlockOpp + a.BlockOwn):F2} |");
        }
        foreach (var hk in new[] { "HD10", "HD20", "HCD" })
        {
            Console.WriteLine();
            Console.WriteLine($"## {hk} —— 砕けたT（砕けなかった戦）・砕けるまでに止めた行動の内訳（1戦）");
            Console.WriteLine();
            Console.WriteLine("内訳 ＝ 粛が単独の原因で止めた行動（味方 ／ 敵）と経路（味方の経路・敵の経路）。砕けた後に通った ＝ 粛が生きている間に通ったターン外の行動（味方 ／ 敵）。");
            Console.WriteLine();
            Console.WriteLine("| 行 | 勝率 S → 版 | 砕けた戦 | 砕けたT | 止めた（味方 ／ 敵） | 経路 | 砕けた後に通った（味方 ／ 敵） |");
            Console.WriteLine("|---|---|--:|---|---|---|---|");
            foreach (var (n, _) in rows)
            {
                var a = res[(n, hk)]; var s = res[(n, "S")];
                var parts = Enumerable.Range(0, OutOfTurnRoutes.Count).Where(i => a.ByRoute[i][0] + a.ByRoute[i][1] > 0)
                    .Select(i => $"{OutOfTurnRoutes.Names[i]} {a.P(a.ByRoute[i][1] + a.ByRoute[i][0]):F1}");
                Console.WriteLine($"| {n} | {s.Win:F1} → {a.Win:F1} | {Pct(a.ShatterN, a.N)} | T{Avg(a.ShatterT, a.ShatterN)}（{Pct(a.N - a.ShatterN, a.N)}） | {a.P(a.BlockOpp):F2} ／ {a.P(a.BlockOwn):F2} | {(parts.Any() ? string.Join("・", parts) : "—")} | {a.P(a.ShatterPassOpp):F2} ／ {a.P(a.ShatterPassOwn):F2} |");
            }
        }
        foreach (var hk in new[] { "HC", "HCD" })
        {
            Console.WriteLine();
            Console.WriteLine($"## {hk} —— 騎士の斬り返し（1戦）");
            Console.WriteLine();
            Console.WriteLine("機会 ＝ 斬り返しの機会（騎士ごと1ターン1回）／ 出た ／ 粛で止まった ／ 痺れほかで止まった ／ 減らした HP ＝ 斬り返した相手（攻撃してきた駒）の HP の減り ／ 受けた駒 ＝ 斬り返しの `Damage` を受けた駒（巨躯 ／ 分かち ／ 庇いの肩代わりを含む・1戦の量）。");
            Console.WriteLine();
            Console.WriteLine("| 行 | 勝率 S → 版 | 機会 | 出た | 粛で止まった | 痺れほか | 減らした HP | 受けた駒（1戦の量） |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|");
            foreach (var (n, _) in rows)
            {
                var a = res[(n, hk)]; var s = res[(n, "S")];
                string taken = a.RipTaken.Count == 0 ? "—" : string.Join(" ／ ", a.RipTaken.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{FvName(kv.Key)} {a.P(kv.Value):F1}"));
                Console.WriteLine($"| {n} | {s.Win:F1} → {a.Win:F1} | {a.P(a.KnightAsked):F2} | {a.P(a.KnightRip):F2} | {a.P(a.KnightHushed):F2} | {a.P(a.KnightHeld):F2} | {a.P(a.KnightDealt):F1} | {taken} |");
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // §5-2 `compare` 64 行 × 第2波 × 版・(G2)・§7 の得点
    // ---------------------------------------------------------------------------------
    internal static double Rate2(Formation f, V v, int seeds)
    {
        var mk = Wave2Of(v);
        var wins = new bool[seeds];
        Parallel.For(0, seeds, s => wins[s] = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), mk(), s, verbose: false).PlayerWon);
        return 100.0 * wins.Count(x => x) / seeds;
    }

    static V PickHd(int seeds)
    {
        var rows = CompareBuilds();
        var area = rows.Select(r => HasArea(r.F)).ToArray();
        var s0 = rows.Select(r => Rate2(r.F, VS, seeds)).ToArray();
        (int Low, double AreaGain) Score(V v)
        {
            var a = rows.Select(r => Rate2(r.F, v, seeds)).ToArray();
            return (a.Count(x => x < 20), Enumerable.Range(0, rows.Length).Where(i => area[i]).Average(i => a[i] - s0[i]));
        }
        var d10 = Score(VHD10); var d20 = Score(VHD20);
        if (d10.Low != d20.Low) return d10.Low < d20.Low ? VHD10 : VHD20;
        if (Math.Abs(d10.AreaGain - d20.AreaGain) > 1e-9) return d10.AreaGain < d20.AreaGain ? VHD10 : VHD20;
        return VHD20;
    }

    static void Cmp(int seeds)
    {
        var rows = CompareBuilds();
        var vers = Vers;
        var area = rows.Select(r => HasArea(r.F)).ToArray();
        var all = vers.ToDictionary(v => v.Key, v => rows.Select(r => Rate2(r.F, v, seeds)).ToArray());
        var s0 = all["S"];
        Console.WriteLine($"# 第304期 `compare` {rows.Length} 行 × 第2波 × 版（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"HCD の HD は {HCD.Name}。版は第2波の敵だけを差し替えるので、第1・3〜5波は S と全セル同じ。S から −10.0pt 以上落ちたセルは太字・+10.0pt 以上上がったセルは斜体。範囲 ／ 貫き ＝ 手番の攻撃型が単体でない駒がいる行（{area.Count(x => x)} 行）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 範囲 ／ 貫き | " + string.Join(" | ", vers.Select(v => v.Key)) + " |");
        Console.WriteLine("|---|:-:|" + string.Concat(Enumerable.Repeat("--:|", vers.Length)));
        for (int i = 0; i < rows.Length; i++)
            Console.WriteLine($"| {rows[i].Name} | {(area[i] ? "○" : "")} | " + string.Join(" | ", vers.Select(v =>
            {
                double x = all[v.Key][i], d = x - s0[i];
                return v != VS && d <= -10.0 ? $"**{x:F1}**" : v != VS && d >= 10.0 ? $"*{x:F1}*" : $"{x:F1}";
            })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 分布と §7 の得点");
        Console.WriteLine();
        Console.WriteLine("| 版 | 0〜20 | 20〜50 | 50〜80 | 80〜100 | 64 行の平均 | 範囲 ／ 貫きの行の平均（S からの上げ） | ない行の平均（同） | 20% 未満の行 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|---|---|");
        foreach (var v in vers)
        {
            var a = all[v.Key];
            double ar = Enumerable.Range(0, rows.Length).Where(i => area[i]).Average(i => a[i]), arg = Enumerable.Range(0, rows.Length).Where(i => area[i]).Average(i => a[i] - s0[i]);
            double nr = Enumerable.Range(0, rows.Length).Where(i => !area[i]).Average(i => a[i]), nrg = Enumerable.Range(0, rows.Length).Where(i => !area[i]).Average(i => a[i] - s0[i]);
            var low = Enumerable.Range(0, rows.Length).Where(i => a[i] < 20).Select(i => rows[i].Name);
            Console.WriteLine($"| {v.Key} | {a.Count(x => x < 20)} | {a.Count(x => x >= 20 && x < 50)} | {a.Count(x => x >= 50 && x < 80)} | {a.Count(x => x >= 80)} | {a.Average():F1} | {ar:F1}（{arg:+0.0;-0.0;0.0}） | {nr:F1}（{nrg:+0.0;-0.0;0.0}） | {(low.Any() ? string.Join("・", low) : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine("## (G2)（第2波で −10.0pt 以上落ちた行）");
        Console.WriteLine();
        Console.WriteLine("「その駒を含む他の行」の全波平均の変化 ＝ 第2波の変化 ÷ 5（ほかの波は動かない）。");
        Console.WriteLine();
        bool any = false;
        foreach (var v in vers.Where(v => v != VS))
            for (int i = 0; i < rows.Length; i++)
            {
                if (all[v.Key][i] - s0[i] > -10.0) continue;
                any = true;
                var parts = new List<string>(); bool broken = false;
                foreach (var d in rows[i].F.Occupied().Select(o => o.Def).Distinct())
                {
                    var others = Enumerable.Range(0, rows.Length).Where(j => j != i && rows[j].F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToArray();
                    if (others.Length == 0) { parts.Add($"{Short(d)}: 他の行 0（分解が成立しない）"); continue; }
                    double delta = others.Average(j => (all[v.Key][j] - s0[j]) / 5.0);
                    if (delta <= -3.0) broken = true;
                    parts.Add($"{Short(d)} {delta:+0.0;-0.0;0.0}pt（{others.Length} 行）");
                }
                Console.WriteLine($"- {v.Key} {rows[i].Name}（{s0[i]:F1} → {all[v.Key][i]:F1}）: " + string.Join(" ／ ", parts) + (broken ? " → **壊れ**" : " → 編成上の制約"));
            }
        if (!any) Console.WriteLine("−10.0pt 以上落ちた行は無い（(G2) の分解の対象なし）。");
    }

    // ---------------------------------------------------------------------------------
    // 台本の例（Codex 向け）
    // ---------------------------------------------------------------------------------
    static void Find(int seeds)
    {
        Console.WriteLine($"# 第304期 台本の例（seed 0..{seeds - 1}・第2波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 台 | 「ひび → 砕けた → 割り込みが戻る（粛が生きている間）」が揃った最初の seed（砕けたT） | 「破れた → 割り込み → 戻った」が揃った最初の seed（T） |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var v in new[] { VHD10, VHB })
            foreach (var (n, f) in Boards())
            {
                int first = -1, ft = 0;
                for (int s = 0; s < seeds && first < 0; s++)
                {
                    var e = Wave2Of(v)();
                    var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), e, s, verbose: true);
                    var ev = r.Events.ToList();
                    var h = e.First(u => u.Def.Traits.Contains(TraitId.Hush));
                    int di = ev.FindIndex(x => x.Kind == BattleEventKind.Death && x.TargetId == h.InstanceId); if (di < 0) di = ev.Count;
                    string open = v == VHD10 ? HushStateLabels.Shatter : HushStateLabels.Break;
                    int si = ev.FindIndex(x => x.Kind == BattleEventKind.HushState && x.Text == open);
                    if (si < 0 || si > di) continue;
                    int ci = v == VHB ? ev.FindIndex(si, x => x.Kind == BattleEventKind.HushState && x.Text == HushStateLabels.Close) : di;
                    if (ci < 0) ci = di;
                    bool back = ev.Skip(si).Take(ci - si).Any(x => (x.Kind == BattleEventKind.Damage && x.Reaction) || x.Kind == BattleEventKind.FeatherMark || x.Kind == BattleEventKind.MarkRally || x.Kind == BattleEventKind.Cover);
                    if (back) { first = s; ft = ev[si].Turn; }
                }
                Console.WriteLine($"| {v.Key} | {n} | {(v == VHD10 ? (first < 0 ? "—" : $"seed {first}（T{ft}）") : "")} | {(v == VHB ? (first < 0 ? "—" : $"seed {first}（T{ft}）") : "")} |");
            }
    }

    static void Memo(string rowPart, int seed, string ver, int lastTurn)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(rowPart, StringComparison.Ordinal));
        var v = Vers.First(b => b.Key == ver);
        var p = BattleEngine.Materialize(f0, BattleContext.PlayerTeam);
        var e = Wave2Of(v)();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# hush304 memo —— {name} × 第2波 × seed {seed} × {v.Key} {v.Name} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Turn > lastTurn) break;
            if (x.Kind is not (BattleEventKind.Damage or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.Feather or BattleEventKind.MarkRally
                or BattleEventKind.Death or BattleEventKind.Attack or BattleEventKind.VendettaRound or BattleEventKind.Cover or BattleEventKind.Sealed or BattleEventKind.HushState)) continue;
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

    internal static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    internal static readonly PropertyInfo TurnProp = typeof(BattleContext).GetProperty("Turn") ?? throw new InvalidOperationException("Turn が見つからない");
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit");
    internal static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();

    internal static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in p.Concat(e)) foreach (var t in u.Traits) t.OnBattleStart(ctx, u);
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
        Console.WriteLine("# hush304 check —— 第304期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var hisa = UnitCatalog.Hisa; var old = UnitCatalog.HisaH303;
        Expect("(a) 段0 の定義: 規定のヒサ ＝ `HisaH303` ＋ `CoverSkipLethal`（＝ 第303期の HC-s と同じ札）・旧は `All` ／ `Everyone` の外・数値は旧のまま・旧の文面は第303期の規定のまま・第303期の版は旧から作る",
            hisa.Traits.SequenceEqual(old.Traits.Append(TraitId.CoverSkipLethal)) && hisa.Traits.SequenceEqual(UnitCatalog.HisaHCs.Traits) && hisa.MinusText == old.MinusText
            && hisa.PlusText.Contains("自分まで倒れる一撃なら立たない") && !old.PlusText.Contains("倒れる一撃なら") && old.PlusText == UnitCatalog.HisaH302.PlusText
            && hisa.MaxHp == old.MaxHp && hisa.Speed == old.Speed && UnitCatalog.All.Contains(hisa) && !UnitCatalog.Everyone.Contains(old)
            && UnitCatalog.HisaQ1.Traits.Contains(TraitId.HushGesture1) && !UnitCatalog.HisaQ1.Traits.Contains(TraitId.CoverSkipLethal) && UnitCatalog.HisaHCd.Traits.SequenceEqual(old.Traits.Append(TraitId.CoverSkipShoulder)));

        // (b) 段0: 自分が倒れる一撃では庇わない ／ 倒れない一撃は庇う ／ 見送りは1戦1度を使わない
        {
            (bool Covered, long SkipL, int AllyHp, int HisaHp) Cov(BattleContext ctx, List<UnitState> p, List<UnitState> e, int hisaHp)
            {
                var hi = p.First(u => u.Def.Id == "hisa"); var a = p.First(u => u.Def.Id == UnitCatalog.Dolga.Id);
                a.Hp = 20; hi.Hp = hisaHp;
                long before = Tal(ctx, "hisa").CoverFires;
                ctx.ApplyDamage(a, 40, e[0], pattern: AttackPattern.Single);
                return (Tal(ctx, "hisa").CoverFires > before, Tal(ctx, "hisa").CoverSkipLethal, a.Hp, hi.Hp);
            }
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, back3: hisa), Formation.Build(front1: UnitCatalog.Gald), out var p, out var e);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            var c1 = Cov(ctx, p, e, 30);                 // 倒れる一撃 → 見送る
            var c2 = Cov(ctx, p, e, 100);                // 同じ戦で、倒れない一撃 → 庇う（見送りは1戦1度を使っていない）
            var c3 = Cov(ctx, p, e, 100);                // 1戦1度を使った後 → 庇わない
            var ctxO = Ctx(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, back3: old), Formation.Build(front1: UnitCatalog.Gald), out var po, out var eo);
            foreach (var u in po.Concat(eo)) { u.MaxHp = 1000; u.Hp = 1000; }
            var o1 = Cov(ctxO, po, eo, 30);              // 旧は倒れる一撃でも庇う
            Expect("(b) 段0: 規定のヒサは自分が倒れる一撃では庇わない ／ 同じ戦の次の倒れない一撃は庇う（見送りは1戦1度を使わない）／ 庇った後は庇わない ／ 旧 `HisaH303` は倒れる一撃でも庇う",
                !c1.Covered && c1.SkipL == 1 && c2.Covered && !c3.Covered && o1.Covered,
                $"倒れる {c1.Covered}（見送り {c1.SkipL}）・次 {c2.Covered}・3回目 {c3.Covered}・旧 {o1.Covered}（ヒサ HP {o1.HisaHp}）");
        }

        // (c) 版の定義
        Expect("(c) 版: 粛の伝令の版は札を1枚足しただけ（数値・Id は既定のまま）・騎士の版も同じ・どれも `Stages` に無い・第2波の差し替えは粛の伝令と巡礼騎士の席だけ",
            EnemyCatalog.HusherHB.Traits.SequenceEqual(new[] { TraitId.Hush, TraitId.HushBreak }) && EnemyCatalog.HusherHD10.Traits.SequenceEqual(new[] { TraitId.Hush, TraitId.HushShatter10 })
            && EnemyCatalog.HusherHD20.Traits.SequenceEqual(new[] { TraitId.Hush, TraitId.HushShatter20 }) && EnemyCatalog.KnightGR.Traits.SequenceEqual(new[] { TraitId.KnightRiposte })
            && new[] { EnemyCatalog.HusherHB, EnemyCatalog.HusherHD10, EnemyCatalog.HusherHD20 }.All(d => d.Id == EnemyCatalog.Husher.Id && d.MaxHp == EnemyCatalog.Husher.MaxHp && d.Attack == EnemyCatalog.Husher.Attack && d.Speed == EnemyCatalog.Husher.Speed)
            && EnemyCatalog.KnightGR.Id == EnemyCatalog.KnightG.Id && EnemyCatalog.KnightGR.MaxHp == EnemyCatalog.KnightG.MaxHp && EnemyCatalog.KnightGR.Attack == EnemyCatalog.KnightG.Attack
            && !StagesH305.Any(s => s.Enemy.Occupied().Any(o => o.Def == EnemyCatalog.HusherHB || o.Def == EnemyCatalog.HusherHD10 || o.Def == EnemyCatalog.HusherHD20 || o.Def == EnemyCatalog.KnightGR))
            && EnemyOf(VHC).Occupied().Count(o => o.Def == EnemyCatalog.KnightGR) == 2 && EnemyOf(VHB).Occupied().Count(o => o.Def == EnemyCatalog.HusherHB) == 1
            && EnemyOf(VHB).Occupied().Count() == StagesH305[1].Enemy.Occupied().Count());

        // (d) S の第2波 ＝ `Stages[1]` そのまま（器具の組み立てが既定の波と同じ戦を回す）
        {
            int diff = 0;
            foreach (var (_, f) in Boards()) for (int s = 0; s < 20; s++)
                {
                    var a = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Wave2Of(VS)(), s, verbose: false);
                    var b = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.Materialize(StagesH305[1].Enemy, BattleContext.EnemyTeam), s, verbose: false);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                }
            Expect("(d) S の第2波は `Stages[1]` と同じ戦（代表台 × seed 0..19）", diff == 0, $"違い {diff}");
        }

        // (e) HB: 傷を受けた後、保持者の次の手番の始まりまで通り、その後また止まる
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Borg), Formation.Build(front1: UnitCatalog.Gald, center: EnemyCatalog.HusherHB), out var p, out var e);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            var h = e.First(u => u.Def.Id == "husher"); var g = p[0];
            bool b0 = ctx.CanActOutOfTurn(g, OutOfTurnRoute.Avenge);
            ctx.ApplyDamage(h, 5, g, pattern: AttackPattern.Single);
            bool b1 = ctx.CanActOutOfTurn(g, OutOfTurnRoute.Avenge), b1e = ctx.CanActOutOfTurn(e[0], OutOfTurnRoute.Avenge);
            ctx.ApplyDamage(h, 5, g, pattern: AttackPattern.Single);   // 破れている間の傷は何もしない
            long br = Tal(ctx, "husher").HushBreaks;
            ctx.TakeTurn(p[1]);                                       // ほかの駒の手番では戻らない
            bool b2 = ctx.CanActOutOfTurn(g, OutOfTurnRoute.Avenge);
            ctx.TakeTurn(h);                                          // 保持者の手番の始まりで戻る
            bool b3 = ctx.CanActOutOfTurn(g, OutOfTurnRoute.Avenge);
            var ctx0 = Ctx(Formation.Build(front1: UnitCatalog.Golm), Formation.Build(front1: UnitCatalog.Gald, center: EnemyCatalog.Husher), out var p0, out var e0);
            foreach (var u in p0.Concat(e0)) { u.MaxHp = 1000; u.Hp = 1000; }
            ctx0.ApplyDamage(e0.First(u => u.Def.Id == "husher"), 5, p0[0], pattern: AttackPattern.Single);
            bool s1 = ctx0.CanActOutOfTurn(p0[0], OutOfTurnRoute.Avenge);
            Expect("(e) HB: 傷の前は止まる ／ 傷の後は両陣営とも通る ／ 破れている間の傷は数えない ／ ほかの駒の手番では戻らない ／ 保持者の手番の始まりで止まる ／ 既定の粛は傷でも止まったまま",
                !b0 && b1 && b1e && br == 1 && b2 && !b3 && !s1, $"前 {b0}・後 {b1} ／ 敵 {b1e}・破れた {br}・ほかの手番の後 {b2}・保持者の手番の後 {b3}・既定 {s1}");
        }

        // (f) HD: 9 回では止まり、10 回目の後は通る ／ 戻らない ／ 保持者は生きている
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm), Formation.Build(front1: UnitCatalog.Gald, center: EnemyCatalog.HusherHD10), out var p, out var e);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            var g = p[0]; var h = e.First(u => u.Def.Id == "husher");
            int blocked9 = 0; for (int i = 0; i < 9; i++) if (!ctx.CanActOutOfTurn(i % 2 == 0 ? g : e[0], OutOfTurnRoute.Avenge)) blocked9++;
            bool before = ctx.HushShattered;
            bool tenth = ctx.CanActOutOfTurn(g, OutOfTurnRoute.Thorns);
            bool after = ctx.CanActOutOfTurn(g, OutOfTurnRoute.Avenge), afterE = ctx.CanActOutOfTurn(e[0], OutOfTurnRoute.Avenge);
            TurnProp.SetValue(ctx, 5); ctx.TakeTurn(h);
            bool later = ctx.CanActOutOfTurn(g, OutOfTurnRoute.Avenge);
            // 痺れで落ちた問い合わせは数えない（単独の原因ではない）
            var ctx2 = Ctx(Formation.Build(front1: UnitCatalog.Golm), Formation.Build(front1: UnitCatalog.Gald, center: EnemyCatalog.HusherHD10), out var p2, out var e2);
            p2[0].SetCounter(StatusKeys.Stun, 1);
            for (int i = 0; i < 15; i++) ctx2.CanActOutOfTurn(p2[0], OutOfTurnRoute.Avenge);
            var ctx20 = Ctx(Formation.Build(front1: UnitCatalog.Golm), Formation.Build(front1: UnitCatalog.Gald, center: EnemyCatalog.HusherHD20), out var p20, out var e20);
            for (int i = 0; i < 19; i++) ctx20.CanActOutOfTurn(p20[0], OutOfTurnRoute.Avenge);
            bool at19 = ctx20.CanActOutOfTurn(p20[0], OutOfTurnRoute.Avenge), at20 = ctx20.CanActOutOfTurn(p20[0], OutOfTurnRoute.Avenge);
            Expect("(f) HD: 9 回は止まる ／ 10 回目も止まり、その後は両陣営とも通る ／ 保持者の手番の後も戻らない ／ 保持者は生きている ／ 痺れの問い合わせは数えない ／ HD20 は 20 回目の後",
                blocked9 == 9 && !before && !tenth && ctx.HushShattered && after && afterE && later && h.IsAlive && ctx.HushCracks == 10 && ctx2.HushCracks == 0 && !ctx2.HushShattered && !at19 && at20,
                $"9 回 {blocked9}・10 回目 {tenth}・後 {after} ／ 敵 {afterE}・保持者の手番の後 {later}・生きている {h.IsAlive}・ひび {ctx.HushCracks}・痺れ {ctx2.HushCracks}・HD20 19 回目の後 {at19} ／ 20 回目の後 {at20}");
        }

        // (g) HC: 粛の下では騎士の斬り返しが止まる ／ 粛が倒れた後 ／ 砕けた後は出る
        {
            (long Rip, long Hushed, int AttackerHp) Hit(UnitDef herald, Action<BattleContext, List<UnitState>, List<UnitState>>? prep)
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm), Formation.Build(front1: EnemyCatalog.KnightGR, center: herald), out var p, out var e);
                foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
                prep?.Invoke(ctx, p, e);
                var k = e.First(u => u.Def.Id == "knight_g");
                ctx.ApplyDamage(k, 5, p[0], pattern: AttackPattern.Single);
                ctx.ApplyDamage(k, 5, p[0], pattern: AttackPattern.Single);   // 同じターンの2発目は機会にならない
                var t = Tal(ctx, "knight_g");
                return (t.KnightRipostes, t.KnightHushed, p[0].Hp);
            }
            var h0 = Hit(EnemyCatalog.Husher, null);
            var h1 = Hit(EnemyCatalog.Husher, (ctx, p, e) => ctx.ApplyDamage(e.First(u => u.Def.Id == "husher"), 5000, p[0], pattern: AttackPattern.Single));
            var h2 = Hit(EnemyCatalog.HusherHD10, (ctx, p, e) => { for (int i = 0; i < 10; i++) ctx.CanActOutOfTurn(p[0], OutOfTurnRoute.Avenge); });
            var h3 = Hit(EnemyCatalog.HusherHD10, null);
            Expect("(g) HC: 粛の下では騎士の斬り返しが止まる（1ターン1回）／ 粛が倒れた後は出る ／ HD で砕けた後は出る ／ 止められた斬り返しもひびに入る",
                h0.Rip == 0 && h0.Hushed == 1 && h1.Rip == 1 && h1.AttackerHp < 1000 && h2.Rip == 1 && h3.Rip == 0 && h3.Hushed == 1,
                $"粛の下 出た {h0.Rip} ／ 止まった {h0.Hushed}・倒れた後 {h1.Rip}（攻撃した駒 HP {h1.AttackerHp}）・砕けた後 {h2.Rip}・HD の下 {h3.Rip}");
            var ctx4 = Ctx(Formation.Build(front1: UnitCatalog.Golm), Formation.Build(front1: EnemyCatalog.KnightGR, center: EnemyCatalog.HusherHD10), out var p4, out var e4);
            foreach (var u in p4.Concat(e4)) { u.MaxHp = 1000; u.Hp = 1000; }
            ctx4.ApplyDamage(e4.First(u => u.Def.Id == "knight_g"), 5, p4[0], pattern: AttackPattern.Single);
            Expect("(g2) HCD: 騎士の斬り返しが止められると、ひびが1つ入る", ctx4.HushCracks == 1, $"ひび {ctx4.HushCracks}");
        }

        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(h) `PickOne(` の出現数が第303期と同じ（BattleCore）", pick == 36, $"{pick}");
        {
            int diff = 0, n2 = 0;
            foreach (var v in new[] { VS, VHB, VHD10, VHD20, VHC })
                foreach (var (_, f) in Boards()) for (int s = 0; s < 10; s++)
                    {
                        var a = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Wave2Of(v)(), s, verbose: false);
                        var b = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Wave2Of(v)(), s, verbose: true);
                        n2++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                    }
            Expect("(i) verbose の有無で勝敗・決着T が同じ（S ／ HB ／ HD10 ／ HD20 ／ HC × 代表台 × 第2波 × seed 0..9）", diff == 0, $"{n2} 戦・違い {diff}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
