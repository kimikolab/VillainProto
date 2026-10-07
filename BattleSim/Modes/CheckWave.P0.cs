using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;

// checkwave phase0 —— Phase 0 の数え物（Q0-1・Q0-4〜Q0-7）と段1 の棚卸し（6台 × 8波 × seed 0..199）。
static partial class CheckWaveDiag
{
    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第260期 Phase 0 ・ 段1 —— チェック波の下調べと棚卸し");
        Console.WriteLine();
        Console.WriteLine("## 台（駒は今の規定）");
        Console.WriteLine();
        foreach (string b in Boards) Console.WriteLine($"- {b}: {BA.SeatsNamed(BoardOf(b))}");
        Console.WriteLine();

        // ---- Q0-1 ターン頭の順序（台本で確かめる）----
        Console.WriteLine("## Q0-1 ターン頭の順序（T-50後 × 6台 × seed 0..19 の台本で数える）");
        Console.WriteLine();
        var o = OrderCensus(CWave260("T-50後"), 20);
        Console.WriteLine($"- 癒し手の回復（Heal）: {o.Heals} 件。うち同じターンの最初の StatSnapshot（＝刻みの直後）より**前**に出たもの {o.HealBeforeSnap} 件");
        Console.WriteLine($"- ターン頭の刻みで敵が削られたターン: {o.TickTurns}（刻み {o.TickDmg}）。そのうち同じターンに癒し手の回復が後から入ったターン: {o.TickThenHeal}");
        Console.WriteLine();

        // ---- Q0-4 第四波の写し ----
        Console.WriteLine("## Q0-4 第四波の EnemyWave 写し（6台 × seed 0..5 の台本の行一致）");
        Console.WriteLine();
        var (same, lines) = CopyDigest(6);
        Console.WriteLine($"- 一致: {(same ? "○" : "×")}（{lines:N0} 行）");
        Console.WriteLine();

        // ---- Q0-5 軛と回復 ----
        Console.WriteLine("## Q0-5 軛と回復（T-50後 × 6台 × seed 0..19）");
        Console.WriteLine();
        var y = YokeHealCensus(20);
        Console.WriteLine($"- 軛の重装兵が生きている間の癒し手の回復: {y.Heals} 件・うち 1件で 25 を超えた回復 {y.Over25} 件（最大 {y.Max}）——回復は軛に切られない");
        Console.WriteLine();

        // ---- Q0-6 ボスの攻撃力 ----
        Console.WriteLine("## Q0-6 ボスの攻撃力（StatSnapshot の写し・B-全4 ／ B-全8 × 6台 × seed 0..9）");
        Console.WriteLine();
        foreach (var c in new[] { CWave260("B-全4"), CWave260("B-全8") })
        {
            var (ok, n, ex) = RiseCensus(c, 10);
            Console.WriteLine($"- {c.Name}: ターン t の写し ＝ 12 + {(c.Name.EndsWith("8") ? 8 : 4)} × (t − 1) が {ok}/{n} 件（例: {ex}）");
        }
        Console.WriteLine();

        // ---- Q0-7 机上計算 ----
        Console.WriteLine("## Q0-7 机上計算 —— ボスの一撃で落ちるターン（攻 12 + X × (t − 1) ≥ 最大HP・軽減と回復を無視）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 駒（最大HP の低い順）| X = 4 | X = 8 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (string b in Boards)
        {
            var us = BoardOf(b).Occupied().Select(x => x.Def).OrderBy(d => d.MaxHp).ToList();
            static int T(int hp, int x) => hp <= 12 ? 1 : (hp - 12 + x - 1) / x + 1;
            Console.WriteLine($"| {b} | {string.Join("・", us.Select(d => $"{d.Name} {d.MaxHp}"))} | {string.Join("・", us.Select(d => $"T{T(d.MaxHp, 4)}"))} | {string.Join("・", us.Select(d => $"T{T(d.MaxHp, 8)}"))} |");
        }
        Console.WriteLine();

        // ---- 段1 棚卸し ----
        Console.WriteLine("## 段1 棚卸し（倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("窓 ＝ ターン t の手番 ＋ ターン t+1 の頭の刻み（回復される前に削れる量）。的の波は 1〜20 ターン目の窓だけ（勝率は数えない）。");
        Console.WriteLine("窓の最大・命中の最大は「その戦の最大」の 200 戦平均 ／ 200 戦の中の最大。");
        Console.WriteLine();
        var inv = new Dictionary<(string, int), Agg>();
        foreach (string b in Boards)
            foreach (int w in InvWaves)
                inv[(b, w)] = Measure(BoardOf(b), InvWave(w), FC.IsTarget(w) ? Kind.Target : Kind.Normal, 0);
        _inv = inv;
        Console.WriteLine("| 台 | 波 | 勝率 | 全員生存 | 決着T | 窓の与ダメの最大（平均 ／ 最大）| 命中の最大（平均 ／ 最大）| 命中 ／ 窓 | 与ダメ ／ 窓 | 1発 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string b in Boards)
            foreach (int w in InvWaves)
            {
                var a = inv[(b, w)];
                bool t = FC.IsTarget(w);
                Console.WriteLine($"| {b} | {InvName(w)} | {(t ? "—" : F1(a.Win))} | {(t ? "—" : F1(a.SurvPct))} | {(t ? "—" : Per(a.Turns, a.N))} | {Per(a.MaxWinSum, a.N)} ／ {a.MaxWinMax} | {Per(a.MaxHitSum, a.N)} ／ {a.MaxHitMax} | {Per(a.WinHits, a.WinCnt)} | {Per(a.WinDmg, a.WinCnt)} | {Per(a.WinDmg, a.WinHits)} |");
            }
        Console.WriteLine();
        Console.WriteLine("### 段1-2 火力の形（本編第2〜5波の平均 ／ 的・一）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 本編 命中 ／ 窓 | 本編 1発 | 本編 窓の最大 | 的・一 命中 ／ 窓 | 的・一 1発 | 的・一 窓の最大（平均 ／ 最大）|");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (string b in Boards)
        {
            var m = new Agg(); foreach (int w in new[] { 0, 1, 2, 3 }) m.Merge(inv[(b, w)]);
            var t1 = inv[(b, FC.WaveTarget1)];
            Console.WriteLine($"| {b} | {Per(m.WinHits, m.WinCnt)} | {Per(m.WinDmg, m.WinHits)} | {Per(m.MaxWinSum, m.N)} | {Per(t1.WinHits, t1.WinCnt)} | {Per(t1.WinDmg, t1.WinHits)} | {Per(t1.MaxWinSum, t1.N)} ／ {t1.MaxWinMax} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 段1-3 的・一 —— その戦の窓の最大が閾値以上だった戦の割合（ボスの HP を決める表）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Thresholds.Select(x => $"≥ {x}")) + " |");
        Console.WriteLine("|---|" + string.Concat(Thresholds.Select(_ => "--:|")));
        foreach (string b in Boards)
        {
            var a = inv[(b, FC.WaveTarget1)];
            Console.WriteLine($"| {b} | " + string.Join(" | ", a.OverTh.Select(v => Pct(v, a.N))) + " |");
        }
        Console.WriteLine();
        foreach (int w in new[] { FC.WaveTarget1, FC.WaveTarget9 })
        {
            Console.WriteLine($"### 段1-4 {InvName(w)} —— ターンごとの窓の与ダメ（200 戦平均・t の手番 ＋ t+1 の頭の刻み）");
            Console.WriteLine();
            int[] ts = { 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 15, 20 };
            Console.WriteLine("| 台 | " + string.Join(" | ", ts.Select(t => $"T{t}")) + " |");
            Console.WriteLine("|---|" + string.Concat(ts.Select(_ => "--:|")));
            foreach (string b in Boards)
            {
                var a = inv[(b, w)];
                Console.WriteLine($"| {b} | " + string.Join(" | ", ts.Select(t => Per(a.TurnDmg[t], a.TurnCnt[t]))) + " |");
            }
            Console.WriteLine();
        }
        Console.WriteLine("### 段1-5 的・一 —— 窓が閾値に初めて届いたターン（届いた戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Thresholds.Select(x => $"≥ {x}")) + " |");
        Console.WriteLine("|---|" + string.Concat(Thresholds.Select(_ => "--:|")));
        foreach (string b in Boards)
        {
            var a = inv[(b, FC.WaveTarget1)];
            Console.WriteLine($"| {b} | " + string.Join(" | ", Thresholds.Select((_, i) => a.FirstOverN[i] == 0 ? "—" : $"T{Per(a.FirstOver[i], a.FirstOverN[i])}")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
    internal static Dictionary<(string, int), Agg>? _inv;

    // ---------------------------------------------------------------------------------
    // Phase 0 の数え物（自己検査と共有）
    // ---------------------------------------------------------------------------------
    internal record struct OrderStats(long Heals, long HealBeforeSnap, long TickTurns, long TickDmg, long TickThenHeal);

    internal static OrderStats OrderCensus(CWave c, int seeds)
    {
        long heals = 0, before = 0, tickTurns = 0, tickDmg = 0, tickThen = 0;
        foreach (string b in Boards)
            for (int sd = 0; sd < seeds; sd++)
            {
                var (r, p, e) = Fight(BoardOf(b), CheckWave(c), sd);
                var foes = e.Select(u => u.InstanceId).ToHashSet();
                int healer = e.First(u => u.Def.Id == "cw_healer").InstanceId;
                bool snap = false; long headTick = 0; bool healed = false;
                void Close() { if (headTick > 0) { tickTurns++; tickDmg += headTick; if (healed) tickThen++; } }
                foreach (var x in r.Events)
                {
                    if (x.Kind == BattleEventKind.TurnStart) { Close(); snap = false; headTick = 0; healed = false; continue; }
                    if (x.Kind == BattleEventKind.StatSnapshot) { snap = true; continue; }
                    if (x.Kind == BattleEventKind.Heal && x.ActorId == healer)
                    {
                        heals++; if (!snap) before++;
                        healed = true;
                        continue;
                    }
                    if (x.Kind == BattleEventKind.Damage && !snap && x.TargetId is int t && foes.Contains(t)) headTick += x.Amount;
                }
                Close();
            }
        return new OrderStats(heals, before, tickTurns, tickDmg, tickThen);
    }

    /// <summary>元の第四波（`Formation`）と写し（`EnemyWave`）の台本の一致（ログ ＋ 出来事）。</summary>
    internal static (bool Same, long Lines) CopyDigest(int seeds)
    {
        bool same = true; long n = 0;
        foreach (string b in Boards)
            for (int sd = 0; sd < seeds; sd++)
            {
                var a = Dig(Fight(BoardOf(b), InvWave(Wave4), sd).R);
                var c = Dig(Fight(BoardOf(b), CheckWave(Copy4), sd).R);
                n += a.Count;
                if (!a.SequenceEqual(c)) same = false;
            }
        return (same, n);
    }
    internal static List<string> Dig(BattleResult r)
    {
        var l = new List<string> { $"won={r.PlayerWon} T={r.Turns}" };
        l.AddRange(r.Log.Select(x => x.Text));
        // 第291期: 表示専用の4種（`ShockGauge` ／ `Feather` ／ `Scar` ／ `MarkLayer`）は外す——前の期の台本の指紋と並べるため（盤面は読むだけの種類）。
        l.AddRange(r.Events.Where(ev => ev.Kind is not (BattleEventKind.ShockGauge or BattleEventKind.Feather or BattleEventKind.Scar or BattleEventKind.MarkLayer)).Select(ev => $"E {ev.Kind} {ev.Turn} {ev.ActorId} {ev.TargetId} {ev.Amount} {ev.HpAfter} {ev.Slot} {ev.Text} {ev.TickIndex} {ev.TickCount} {ev.BrittleExtra}"));
        return l;
    }

    internal static (long Heals, long Over25, long Max) YokeHealCensus(int seeds)
    {
        long heals = 0, over = 0, mx = 0;
        foreach (string b in Boards)
            for (int sd = 0; sd < seeds; sd++)
            {
                var (r, _, e) = Fight(BoardOf(b), CheckWave(CWave260("T-50後")), sd);
                int healer = e.First(u => u.Def.Id == "cw_healer").InstanceId, yoker = e.First(u => u.Def.Id == "yoker").InstanceId;
                bool yokeAlive = true;
                foreach (var x in r.Events)
                {
                    if (x.Kind == BattleEventKind.Death && x.TargetId == yoker) yokeAlive = false;
                    if (x.Kind == BattleEventKind.Heal && x.ActorId == healer && yokeAlive)
                    { heals++; if (x.Amount > 25) over++; mx = Math.Max(mx, x.Amount); }
                }
            }
        return (heals, over, mx);
    }

    internal static (long Ok, long N, string Ex) RiseCensus(CWave c, int seeds)
    {
        long ok = 0, n = 0; string ex = "";
        int step = c.Name.EndsWith("8") ? BossRiseTrait.High : BossRiseTrait.Low;
        foreach (string b in Boards)
            for (int sd = 0; sd < seeds; sd++)
            {
                var (r, _, e) = Fight(BoardOf(b), CheckWave(c), sd);
                int boss = e[0].InstanceId;
                foreach (var x in r.Events)
                    if (x.Kind == BattleEventKind.StatSnapshot && x.TargetId == boss)
                    {
                        n++;
                        int want = 12 + step * (x.Turn - 1);
                        if (x.Amount == want) ok++;
                        if (ex.Length < 40 && x.Turn is 1 or 2 or 5) ex += $"T{x.Turn}={x.Amount} ";
                    }
            }
        return (ok, n, ex.Trim());
    }
}
