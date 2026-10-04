using System.Reflection;
using BattleCore;
using static Common;
using CW = CheckWaveDiag;

// =====================================================================================
// bosswave —— 第265期「本編ボス波『勇者パーティー』（第六波）」。
// 指示書は design/PHASE265_BOSS_WAVE_SPEC.md ／ 報告は design/PHASE265_BOSS_WAVE.md。
// 波は `EnemyCatalog.BossStages`（H-制御税 ／ H-回復税）。台はチェック波の7台（`CheckWaveDiag.Boards7`）。倍率なし。
// **`Stages` には載せていない**（Phase 0-1・報告書 §1）ので、`compare` ほか `Stages` を数える器具は1つも動かない。
//
//     dotnet run --project BattleSim -c Release 0 bosswave run            # 7台 × 2版 ＋ 対照2つ（B3-桁 ／ B3-速14）× seed 0..199
//     dotnet run --project BattleSim -c Release 0 bosswave check          # 自己検査
//     dotnet run --project BattleSim -c Release 0 bosswave body           # 参考: 取り巻きの HP だけを ×1 ／ ×2 ／ ×4 ／ ×8 にしたとき、割る順序が軸で分かれ始めるか（採否には使わない）
//     dotnet run --project BattleSim -c Release 0 bosswave log <台> <版> [seed]   # 1戦のログ（版は H-制御税 ／ H-回復税 ／ B3-桁 ／ B3-速14）
// =====================================================================================
static class BossWaveDiag
{
    const int Seeds = 200;
    /// <summary>チェック波と同じ 20 ターンの窓（純実入り・勇者の手番を数える窓）。勝ち負けは engine の打ち切り（<see cref="BattleEngine.MaxTurns"/>）のまま。</summary>
    const int Window = CW.BossTurns;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "run";
        switch (mode)
        {
            case "run": RunAll(); return;
            case "check": Check(); return;
            case "body": Body(); return;
            case "log": LogOne(args.Length > 3 ? args[3] : "雷", args.Length > 4 ? args[4] : "H-制御税", args.Length > 5 ? int.Parse(args[5]) : 0); return;
            default: Console.WriteLine("bosswave: モードは run / check / log。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版。H-* は本編の波（`EnemyCatalog.BossStages`）。対照の B3-桁 はチェック波の規定のボスそのもの（速 5・自前で全快・常に動じない）、
    // B3-速14 はそれを速 14 にしただけ（勇者と速さを揃え、取り巻きの効き目と速さの効き目を分けるための対照）。
    // ---------------------------------------------------------------------------------
    sealed record Ver(string Name, string What, Func<EnemyWave> Make, bool Retinue);
    static UnitDef B3Speed14 => new()
    {
        Id = "cw_boss", Name = "ボス", MaxHp = CW.ChosenBossHp, Attack = 12, Speed = 14, Pattern = AttackPattern.All,
        Traits = new[] { TraitId.BossMendFull, TraitId.BossRise11, TraitId.BossSteadfast }, PlusText = "B3-桁 を速 14 にした対照（第265期）",
    };
    static readonly Ver[] Vers =
    {
        new("H-制御税", EnemyCatalog.BossStages[0].Tax, () => EnemyCatalog.BossStages[0].Enemy, true),
        new("H-回復税", EnemyCatalog.BossStages[1].Tax, () => EnemyCatalog.BossStages[1].Enemy, true),
        new("B3-桁", "対照: チェック波の規定のボス（速 5・自前で全快・常に動じない・取り巻きなし）", () => CW.CWaveOf(CW.DefaultBoss).Make(), false),
        new("B3-速14", "対照: B3-桁 を速 14 にしただけ", () => EnemyWave.Of((2, B3Speed14)), false),
    };
    static Ver VerOf(string n) => Vers.First(v => v.Name == n);
    static readonly string[] Boards = CW.Boards7;
    static readonly HashSet<string> PoisonBoards = new() { "毒", CW.Poison2 };

    /// <summary>役（勇 ／ 支 ／ 癒）。対照のボスは勇。</summary>
    static char RoleOf(UnitDef d) => d.Id switch
    {
        "boss_hero" or "cw_boss" => '勇',
        "boss_ward" => '支',
        "boss_mend" => '癒',
        _ => '?',
    };

    // ---------------------------------------------------------------------------------
    // 集計。盤面は1ビットも動かさない（台本を読むだけ）。
    // ---------------------------------------------------------------------------------
    sealed class Agg
    {
        public long N, Wins, Win20, Surv, WinT;
        public long HeroKill, HeroKillT, WardKill, WardKillT, MendKill, MendKillT;
        public long FirstDeath, FirstDeathT;
        public long HeroAlive, HeroActs, HeroDealt, MineHealed, HeroHealed, HeroDmgNoKill;
        public readonly Dictionary<string, long> Orders = new();

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; Win20 += o.Win20; Surv += o.Surv; WinT += o.WinT;
            HeroKill += o.HeroKill; HeroKillT += o.HeroKillT; WardKill += o.WardKill; WardKillT += o.WardKillT; MendKill += o.MendKill; MendKillT += o.MendKillT;
            FirstDeath += o.FirstDeath; FirstDeathT += o.FirstDeathT;
            HeroAlive += o.HeroAlive; HeroActs += o.HeroActs; HeroDealt += o.HeroDealt; MineHealed += o.MineHealed; HeroHealed += o.HeroHealed; HeroDmgNoKill += o.HeroDmgNoKill;
            foreach (var (k, v) in o.Orders) Orders[k] = Orders.GetValueOrDefault(k) + v;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++;
            var role = e.ToDictionary(u => u.InstanceId, u => RoleOf(u.Def));
            int hero = e.First(u => role[u.InstanceId] == '勇').InstanceId;
            var mine = p.Select(u => u.InstanceId).ToHashSet();
            var alive = new HashSet<int>(role.Keys);
            var order = new List<char>();
            int? heroT = null, wardT = null, mendT = null, firstT = null;
            long heroTaken = 0;
            foreach (var x in r.Events)
            {
                switch (x.Kind)
                {
                    case BattleEventKind.TurnStart when x.Turn <= Window && alive.Contains(hero): HeroAlive++; break;
                    case BattleEventKind.Attack when x.ActorId == hero && x.Turn <= Window: HeroActs++; break;
                    case BattleEventKind.Damage when x.ActorId == hero && x.TargetId is int t && mine.Contains(t) && x.Turn <= Window: HeroDealt += x.Amount; break;
                    case BattleEventKind.Damage when x.TargetId == hero && x.Amount > 0 && x.Turn <= Window: heroTaken += x.Amount; break;
                    case BattleEventKind.Heal when x.TargetId is int h && mine.Contains(h) && x.Turn <= Window: MineHealed += x.Amount; break;
                    case BattleEventKind.Heal when x.TargetId == hero && x.Turn <= Window: HeroHealed += x.Amount; break;
                    case BattleEventKind.Death when x.TargetId is int d:
                        if (role.TryGetValue(d, out char c) && alive.Remove(d))
                        {
                            order.Add(c);
                            if (c == '勇') heroT ??= x.Turn;
                            if (c == '支') wardT ??= x.Turn;
                            if (c == '癒') mendT ??= x.Turn;
                        }
                        if (mine.Contains(d)) firstT ??= x.Turn;
                        break;
                }
            }
            if (r.PlayerWon) { Wins++; WinT += r.Turns; if (r.Turns <= Window) Win20++; if (r.PlayerStarterFallen.Count == 0) Surv++; }
            if (heroT is int ht) { HeroKill++; HeroKillT += ht; } else HeroDmgNoKill += heroTaken;
            if (wardT is int wt) { WardKill++; WardKillT += wt; }
            if (mendT is int mt) { MendKill++; MendKillT += mt; }
            if (firstT is int ft) { FirstDeath++; FirstDeathT += ft; }
            string key = order.Count == 0 ? "（誰も割れない）" : string.Join("→", order);
            Orders[key] = Orders.GetValueOrDefault(key) + 1;
        }

        public double Win => 100.0 * Wins / N;
        public double KillTAvg => HeroKill == 0 ? double.PositiveInfinity : (double)HeroKillT / HeroKill;
        /// <summary>最も多い割った順（同数は文字列順）。</summary>
        public (string Order, long Count) Modal => Orders.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (kv.Key, kv.Value)).First();
        /// <summary>最初に割った役の内訳。</summary>
        public long FirstIs(char c) => Orders.Where(kv => kv.Key.Length > 0 && kv.Key[0] == c).Sum(kv => kv.Value);
    }

    static Agg Measure(string board, Ver v)
    {
        var f = CW.BoardOf(board);
        var parts = new Agg[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var (r, p, e) = CW.Fight(f, () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), i, verbose: true);
            var a = new Agg(); a.Take(r, p, e); parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
    static string KillT(Agg a) => a.HeroKill == 0 ? $"倒せない（{Per(a.HeroDmgNoKill, a.N)}）" : $"T{Per(a.HeroKillT, a.HeroKill)}（{Pct(a.HeroKill, a.N)}%）";
    static string T(long sum, long n) => n == 0 ? "—" : "T" + Per(sum, n);

    static void RunAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第265期 —— 本編ボス波「勇者パーティー」（7台 × 2版 ＋ 対照2つ・倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"勝ち ＝ 敵全滅（engine の打ち切り {BattleEngine.MaxTurns} ターンのまま）。純実入り・勇者の手番は {Window} ターンまでの窓で数える（チェック波と同じ）。");
        Console.WriteLine();
        foreach (var v in Vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();

        var res = new Dictionary<(string, string), Agg>();
        foreach (string b in Boards)
            foreach (var v in Vers) res[(b, v.Name)] = Measure(b, v);

        // ---- 表1 倒しT・決着・勝率 ----
        Console.WriteLine("## 表1 勇者の倒しT（倒した戦の平均・倒した割合。倒せない台は 20 ターンまでの総与ダメ）／ 勝率");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Vers.Select(v => v.Name)) + " | " + string.Join(" | ", Vers.Select(v => "勝率 " + v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "--:|")) + string.Concat(Vers.Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", Vers.Select(v => KillT(res[(b, v.Name)]))) + " | " + string.Join(" | ", Vers.Select(v => F1(res[(b, v.Name)].Win))) + " |");
        Console.WriteLine();

        // ---- 表2 割った順 ----
        var hv = Vers.Where(v => v.Retinue).ToArray();
        Console.WriteLine("## 表2 割った順（勇 ＝ 勇者・支 ＝ 支え・癒 ＝ 癒し手。死んだ順。上位3つと割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 最初に割った 勇 ／ 支 ／ 癒（%）| 割った順（上位3）|");
        Console.WriteLine("|---|---|--:|---|");
        foreach (string b in Boards)
            foreach (var v in hv)
            {
                var a = res[(b, v.Name)];
                string top = string.Join("・", a.Orders.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(3).Select(kv => $"{kv.Key} {Pct(kv.Value, a.N)}"));
                Console.WriteLine($"| {b} | {v.Name} | {Pct(a.FirstIs('勇'), a.N)} ／ {Pct(a.FirstIs('支'), a.N)} ／ {Pct(a.FirstIs('癒'), a.N)} | {top} |");
            }
        Console.WriteLine();

        // ---- 表3 取り巻き・勇者の手番 ----
        Console.WriteLine("## 表3 取り巻きを割ったT・勇者の手番・勇者が受けた回復（1戦あたり・20 ターンまで）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 支えを割ったT（%）| 癒し手を割ったT（%）| 勇者の倒しT | 決着T（勝ち）| 勇者が振れた手番 | 勇者が受けた回復 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (string b in Boards)
            foreach (var v in Vers)
            {
                var a = res[(b, v.Name)];
                Console.WriteLine($"| {b} | {v.Name} | {(v.Retinue ? $"{T(a.WardKillT, a.WardKill)}（{Pct(a.WardKill, a.N)}）" : "—")} | {(v.Retinue ? $"{T(a.MendKillT, a.MendKill)}（{Pct(a.MendKill, a.N)}）" : "—")} | "
                    + $"{KillT(a)} | {T(a.WinT, a.Wins)} | {a.HeroActs} ／ {a.HeroAlive}（{Pct(a.HeroActs, a.HeroAlive)}%）| {Per(a.HeroHealed, a.N)} |");
            }
        Console.WriteLine();

        // ---- 表4 純実入り・崩れ始め ----
        Console.WriteLine("## 表4 純実入り（勇者が入れた − 隊の回復・1戦あたり・20 ターンまで）・崩れ始め（最初の死亡）・全員生存");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Vers.Select(v => "純実入り " + v.Name)) + " | " + string.Join(" | ", Vers.Select(v => "崩れ始め " + v.Name)) + " | " + string.Join(" | ", Vers.Select(v => "全員生存 " + v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "--:|")) + string.Concat(Vers.Select(_ => "--:|")) + string.Concat(Vers.Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", Vers.Select(v => Per(res[(b, v.Name)].HeroDealt - res[(b, v.Name)].MineHealed, res[(b, v.Name)].N)))
                + " | " + string.Join(" | ", Vers.Select(v => $"{T(res[(b, v.Name)].FirstDeathT, res[(b, v.Name)].FirstDeath)}（{Pct(res[(b, v.Name)].FirstDeath, res[(b, v.Name)].N)}%）"))
                + " | " + string.Join(" | ", Vers.Select(v => F1(100.0 * res[(b, v.Name)].Surv / res[(b, v.Name)].N))) + " |");
        Console.WriteLine();

        // ---- 表5 採否 ----
        Console.WriteLine("## 表5 採否（指示書 §4）");
        Console.WriteLine();
        Console.WriteLine("### 5-1 割る順序が軸で分かれるか（台ごとの最多の順。全台が同じなら取り巻きは飾り＝棄却）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 台ごとの最多の順 | 異なる順の数 | 最初に割る役の異なる数 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        var split = new Dictionary<string, bool>();
        foreach (var v in hv)
        {
            var modal = Boards.Select(b => (b, M: res[(b, v.Name)].Modal)).ToList();
            int kinds = modal.Select(x => x.M.Order).Distinct().Count();
            int firsts = modal.Select(x => x.M.Order[0]).Distinct().Count();
            split[v.Name] = kinds >= 2;
            Console.WriteLine($"| {v.Name} | {string.Join("・", modal.Select(x => $"{x.b} {x.M.Order}（{Pct(x.M.Count, Seeds)}）"))} | {kinds} | {firsts} | {(kinds >= 2 ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 5-2 2版で倒しT の順位が入れ替わる台（倒せない台は最下位で同順）");
        Console.WriteLine();
        int RankOf(string b, string v)
        {
            double t = res[(b, v)].KillTAvg;
            return 1 + Boards.Count(x => res[(x, v)].KillTAvg < t - 1e-9);
        }
        Console.WriteLine("| 台 | 順位 H-制御税 | 順位 H-回復税 | 倒しT H-制御税 | 倒しT H-回復税 | 入れ替わり |");
        Console.WriteLine("|---|--:|--:|--:|--:|:-:|");
        int swaps = 0;
        foreach (string b in Boards)
        {
            int r0 = RankOf(b, hv[0].Name), r1 = RankOf(b, hv[1].Name);
            if (r0 != r1) swaps++;
            Console.WriteLine($"| {b} | {r0} | {r1} | {KillT(res[(b, hv[0].Name)])} | {KillT(res[(b, hv[1].Name)])} | {(r0 != r1 ? "○" : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"入れ替わった台: **{swaps}**（0 なら席は課税として効いていない）。");
        Console.WriteLine();
        Console.WriteLine("### 5-3 全軸が勝てるか（勝率 0% の台が毒以外に出ないこと）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 0% の台 | 毒以外 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        foreach (var v in hv)
        {
            var zero = Boards.Where(b => res[(b, v.Name)].Wins == 0).ToList();
            int nonPoison = zero.Count(b => !PoisonBoards.Contains(b));
            Console.WriteLine($"| {v.Name} | {(zero.Count == 0 ? "—" : string.Join("・", zero))} | {nonPoison} | {(nonPoison == 0 ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>
    /// 参考（採否には使わない）: 取り巻き2体の HP だけを ×<paramref name="k"/> にした2版。攻・速・型・札は同じ。
    /// 第265期の 5-1 が × だった理由が「席（構造）」か「体の薄さ（数値）」かを分けるための表。
    /// </summary>
    static Ver[] Thick(int k)
    {
        UnitDef Fat(UnitDef d) => new()
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * k, Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern, Advances = d.Advances, Actions = d.Actions,
            Traits = d.Traits, PlusText = d.PlusText,
        };
        var ward = Fat(EnemyCatalog.BossWard); var mend = Fat(EnemyCatalog.BossMender);
        return new[]
        {
            new Ver($"H-制御税 ×{k}", "", () => EnemyWave.Of((2, EnemyCatalog.BossHero), (3, mend), (8, ward)), true),
            new Ver($"H-回復税 ×{k}", "", () => EnemyWave.Of((2, EnemyCatalog.BossHero), (3, ward), (8, mend)), true),
        };
    }

    static void Body()
    {
        Console.WriteLine("# 第265期 参考 —— 取り巻きの HP だけを厚くしたとき（7台 × 2版 × ×1 ／ ×2 ／ ×4 ／ ×8・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"取り巻き（支え ／ 癒し手）の HP を {EnemyCatalog.Warden.MaxHp} × k にする。攻・速・型・札・席はそのまま。**採否には使わない**（指示書 §1-2 の「既存の敵と同数値」を外れる）。");
        Console.WriteLine();
        Console.WriteLine("| k | 版 | 台ごとの最多の順（割合）| 異なる順 | 最初に割る役の異なる数 | 倒しT（台ごと）| 勝率 0% の台 |");
        Console.WriteLine("|--:|---|---|--:|--:|---|---|");
        foreach (int k in new[] { 1, 2, 4, 8 })
            foreach (var v in Thick(k))
            {
                var res = Boards.ToDictionary(b => b, b => Measure(b, v));
                var modal = Boards.Select(b => (b, M: res[b].Modal)).ToList();
                var zero = Boards.Where(b => res[b].Wins == 0).ToList();
                Console.WriteLine($"| {k} | {v.Name} | {string.Join("・", modal.Select(x => $"{x.b} {x.M.Order}（{Pct(x.M.Count, Seeds)}）"))} | {modal.Select(x => x.M.Order).Distinct().Count()} | {modal.Select(x => x.M.Order[0]).Distinct().Count()} | "
                    + $"{string.Join("・", Boards.Select(b => $"{b} {(res[b].HeroKill == 0 ? "—" : "T" + Per(res[b].HeroKillT, res[b].HeroKill))}"))} | {(zero.Count == 0 ? "—" : string.Join("・", zero))} |");
            }
    }

    static void LogOne(string board, string ver, int seed)
    {
        var v = VerOf(ver);
        var (r, p, e) = CW.Fight(CW.BoardOf(board), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), seed);
        Console.WriteLine($"# {board} × {v.Name}（{v.What}）× seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
        ?? throw new InvalidOperationException("BattleContext.Add が見つからない");

    static void Check()
    {
        int pass = 0, fail = 0;
        void Ok(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            Console.WriteLine($"- [{(ok ? "○" : "×")}] {name}{(detail.Length > 0 ? $"（{detail}）" : "")}");
        }
        Console.WriteLine("# bosswave check —— 第265期 自己検査");
        Console.WriteLine();

        // (a) 取り巻きは城塞の重装兵と数値・型・行動・踏み込みが同一で、差分は札1枚
        var wd = EnemyCatalog.Warden;
        foreach (var d in new[] { EnemyCatalog.BossWard, EnemyCatalog.BossMender })
        {
            bool eq = d.MaxHp == wd.MaxHp && d.Attack == wd.Attack && d.Speed == wd.Speed && d.Pattern == wd.Pattern && d.Advances == wd.Advances
                      && ReferenceEquals(d.Actions, wd.Actions) && wd.Traits.Count == 0 && d.Traits.Count == 1;
            Ok($"(a) {d.Name}（{d.Traits[0]}）＝ 城塞の重装兵 {wd.MaxHp}/{wd.Attack}/{wd.Speed}・{wd.Pattern} ＋ 札1枚", eq);
        }

        // (b) 2版は同じ定義を使い、差分は席だけ（勇者は中央・取り巻きは 後1 と ○後2 を入れ替えただけ）
        {
            var a = EnemyCatalog.BossStages[0].Enemy; var b = EnemyCatalog.BossStages[1].Enemy;
            bool same = a.Count == 3 && b.Count == 3 && ReferenceEquals(a[2], b[2]) && ReferenceEquals(a[3], b[8]) && ReferenceEquals(a[8], b[3])
                        && ReferenceEquals(a[2], EnemyCatalog.BossHero) && ReferenceEquals(a[8], EnemyCatalog.BossWard) && ReferenceEquals(b[8], EnemyCatalog.BossMender);
            Ok("(b) H-制御税（支え ○後2・癒し手 後1）と H-回復税（入れ替え）は同じ3体で、席だけが違う", same);
        }

        // (c) 勇者 ＝ B3-桁 の本体（HP 3,000・攻 12・全体・天井 +11）を速 14 にし、回復と常時の動じないを外したもの
        {
            var h = EnemyCatalog.BossHero;
            bool ok = h.MaxHp == CW.ChosenBossHp && h.Attack == 12 && h.Speed == 14 && h.Pattern == AttackPattern.All
                      && h.Traits.Count == 2 && h.Traits.Contains(TraitId.BossRise11) && h.Traits.Contains(TraitId.HeroCrest);
            Ok("(c) 勇者 ＝ HP 3,000・攻 12・速 14・全体・{天井 +11, 勇者の印}（全快・常時の動じないを持たない）", ok);
        }

        // (d) 勇者の印: 支えが生きている間は痺れ・転倒・組み付き・竦み・混乱が付かず、支えが倒れたその場から付く
        {
            var (ctx, e) = Board(EnemyCatalog.BossStages[0].Enemy);
            var hero = e.First(u => u.Def == EnemyCatalog.BossHero); var ward = e.First(u => u.Def == EnemyCatalog.BossWard);
            bool blocked = StatusKeys.Control.All(k => { hero.SetCounter(k, 1); return hero.RawCounter(k) == 0; });
            ward.Hp = 0;
            bool open = StatusKeys.Control.All(k => { hero.SetCounter(k, 1); return hero.RawCounter(k) == 1; });
            hero.SetCounter(StatusKeys.Poison, 5);
            Ok("(d-1) 支えが生きている間は手番を奪う状態 5 種が付かない", blocked);
            Ok("(d-2) 支えが倒れたその場から 5 種とも付く", open);
            Ok("(d-3) 毒は支えの生死に関わらず付く", hero.RawCounter(StatusKeys.Poison) == 5);
        }
        {
            // 癒し手が倒れても動じないは残る（支えだけが源）
            var (ctx, e) = Board(EnemyCatalog.BossStages[0].Enemy);
            var hero = e.First(u => u.Def == EnemyCatalog.BossHero); var mend = e.First(u => u.Def == EnemyCatalog.BossMender);
            mend.Hp = 0;
            hero.SetCounter(StatusKeys.Stun, 1);
            Ok("(d-4) 癒し手が倒れても、支えが生きていれば痺れは付かない", hero.RawCounter(StatusKeys.Stun) == 0);
        }
        {
            // 第261期の動じない（常時）は今まで通り塞ぐ
            var (ctx, e) = Board(EnemyWave.Of((2, B3Speed14)));
            e[0].SetCounter(StatusKeys.Stun, 1);
            Ok("(d-5) 第261期の動じない（BossSteadfast・常時）はそのまま痺れを塞ぐ", e[0].RawCounter(StatusKeys.Stun) == 0);
        }

        // (e) 癒し手: ターン頭に勇者だけを最大HPの 40% 回復（取り巻きは癒さない）
        {
            var (ctx, e) = Board(EnemyCatalog.BossStages[1].Enemy);
            var hero = e.First(u => u.Def == EnemyCatalog.BossHero); var ward = e.First(u => u.Def == EnemyCatalog.BossWard); var mend = e.First(u => u.Def == EnemyCatalog.BossMender);
            hero.Hp = 100; ward.Hp = 50; mend.Hp = 50;
            TraitCatalog.Resolve(new[] { TraitId.HeroMend })[0].OnTurnStart(ctx, mend);
            Ok("(e-1) 勇者 HP 100 → 1,300（+40% ＝ 1,200）・支え ／ 癒し手は 50 のまま", hero.Hp == 1300 && ward.Hp == 50 && mend.Hp == 50, $"勇者 {hero.Hp}・支え {ward.Hp}・癒し手 {mend.Hp}");
            hero.Hp = 2900;
            TraitCatalog.Resolve(new[] { TraitId.HeroMend })[0].OnTurnStart(ctx, mend);
            Ok("(e-2) 最大HPを超えない（2,900 → 3,000）", hero.Hp == 3000, $"{hero.Hp}");
        }

        // (f) 新しい札3枚の保持者はロスター（`Everyone`）と本編・検証・先遣・パターン3 の敵に 0 枚。`Stages` の敵は1体も変わっていない
        {
            TraitId[] mine = { TraitId.HeroCrest, TraitId.HeroWard, TraitId.HeroMend };
            var enemyDefs = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def))
                .Concat(EnemyCatalog.TestStages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)))
                .Concat(EnemyCatalog.Vanguards.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)))
                .Concat(EnemyCatalog.Pattern3Copies.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)));
            int holders = UnitCatalog.Everyone.Concat(enemyDefs).Count(u => u.Traits.Any(mine.Contains));
            Ok("(f-1) 新しい札 3 枚の保持者がロスターと本編・検証・先遣・パターン3 の敵に 0 枚", holders == 0, $"{holders} 枚");
            Ok("(f-2) `Stages` は 5 波のまま", EnemyCatalog.Stages.Count == 5, $"{EnemyCatalog.Stages.Count}");
            int idReads = Directory.GetFiles("BattleCore", "*.cs").Sum(f => File.ReadAllText(f).Split('\n').Count(l => l.Contains("\"boss_hero\"") || l.Contains("\"boss_ward\"") || l.Contains("\"boss_mend\"")));
            Ok("(f-3) BattleCore の規則が勇者パーティーの Id を読まない（定義の行だけ・3 行）", idReads == 3, $"{idReads} 行");
        }

        // (g) 新しい札は乱数を引かない（`PickOne` ／ `Roll` を本文に持たない）
        {
            string src = File.ReadAllText(Path.Combine("BattleCore", "Traits.cs"));
            int from = src.IndexOf("public sealed class HeroCrestTrait", StringComparison.Ordinal);
            int to = src.IndexOf("public sealed class BossRiseTrait", StringComparison.Ordinal);
            string body = from >= 0 && to > from ? src[from..to] : "";
            Ok("(g) 勇者の印・支え・癒し手の本文に `PickOne` ／ `Roll` が 0", body.Length > 0 && !body.Contains("PickOne") && !body.Contains("Roll"), $"{body.Length} 字");
        }

        // (h) 決定性: 同じ seed で2回回すと台本が一致する（2版 × 7台 × seed 0..3）
        {
            bool same = true; long lines = 0;
            foreach (var v in Vers.Where(v => v.Retinue))
                foreach (string b in Boards)
                    for (int s = 0; s < 4; s++)
                    {
                        var r1 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), s).R;
                        var r2 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), s).R;
                        same &= r1.Log.Select(l => l.Text).SequenceEqual(r2.Log.Select(l => l.Text)) && r1.PlayerWon == r2.PlayerWon && r1.Turns == r2.Turns;
                        lines += r1.Log.Count;
                    }
            Ok("(h) 同じ seed の2回が台本一致（2版 × 7台 × seed 0..3）", same, $"{lines:N0} 行");
        }

        Console.WriteLine();
        Console.WriteLine($"合計 {pass + fail} 項目: ○ {pass} ／ × {fail}");
    }

    static (BattleContext Ctx, List<UnitState> E) Board(EnemyWave w)
    {
        var ctx = new BattleContext(0, false);
        var p = BattleEngine.Materialize(CW.BoardOf("雷"), BattleContext.PlayerTeam);
        var e = BattleEngine.MaterializeEnemy(w, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        return (ctx, e);
    }
}
