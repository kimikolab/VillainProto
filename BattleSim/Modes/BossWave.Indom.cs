using BattleCore;
using static Common;
using CW = CheckWaveDiag;

// bosswave indom0 ／ indom ／ check266 ／ log266 —— 第266期「勇者1体に戻す・動じないを『不屈』に差し替える」。
// 指示書は design/PHASE266_BOSS_INDOMITABLE_SPEC.md ／ 報告は design/PHASE266_BOSS_INDOMITABLE.md。
// 勇者は1体（中央）・全体・HP 3,000・攻 12・速 14・天井 +11・自前の回復（ターン頭に最大HPの 40%）。版は不屈の N だけ（0 ／ 11 ／ 22）。
// 対照は D-40（動じない＝完全無効・それ以外は F-* と同じ）と、第265期の B3-速14（動じない・毎ターン全快）。倍率なし・seed 0..199。
static partial class BossWaveDiag
{
    static UnitDef IndomHero(TraitId guard, string id) => new()
    {
        Id = id, Name = "勇者", MaxHp = CW.ChosenBossHp, Attack = 12, Speed = 14, Pattern = AttackPattern.All,
        Traits = new[] { TraitId.BossRise11, TraitId.BossMend40, guard },
        PlusText = "全員を薙ぎ払い、毎ターン攻撃力が 11 上がり、ターン頭に最大HPの 40% 癒える（第266期・bosswave）",
    };

    sealed record IVer(string Name, string What, int Step, Func<EnemyWave> Make);
    static readonly IVer[] IVers =
    {
        new("F-0", "不屈 N=0（素通し・制御は無料）", 0, () => EnemyWave.Of((2, IndomHero(TraitId.Indomitable0, "bw_indom")))),
        new("F-11", "不屈 N=11（1回止める ＝ 天井1ターン分）", IndomitableTrait.Mid, () => EnemyWave.Of((2, IndomHero(TraitId.Indomitable11, "bw_indom")))),
        new("F-22", "不屈 N=22（1回止める ＝ 天井2ターン分）", IndomitableTrait.High, () => EnemyWave.Of((2, IndomHero(TraitId.Indomitable22, "bw_indom")))),
        new("D-40", "対照: 動じない（完全無効）・ほかは F-* と同じ（回復 40%）", 0, () => EnemyWave.Of((2, IndomHero(TraitId.BossSteadfast, "bw_indom")))),
        new("B3-速14", "対照: 第265期の B3-速14（動じない・毎ターン全快）", 0, () => EnemyWave.Of((2, B3Speed14))),
    };
    static IVer IVerOf(string n) => IVers.First(v => v.Name == n);

    sealed class IAgg
    {
        public long N, Wins, WinT, Surv, Kill, KillT, NoKillDmg, FirstDeath, FirstDeathT;
        public long Alive, Acts, Dealt, Healed, Ctrl, CtrlBeforeKill, IndomAtk, CeilAtk, PeakAtk;
        public readonly Dictionary<string, long> ByKey = new();

        public void Merge(IAgg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Surv += o.Surv; Kill += o.Kill; KillT += o.KillT; NoKillDmg += o.NoKillDmg; FirstDeath += o.FirstDeath; FirstDeathT += o.FirstDeathT;
            Alive += o.Alive; Acts += o.Acts; Dealt += o.Dealt; Healed += o.Healed; Ctrl += o.Ctrl; CtrlBeforeKill += o.CtrlBeforeKill; IndomAtk += o.IndomAtk; CeilAtk += o.CeilAtk; PeakAtk += o.PeakAtk;
            foreach (var (k, v) in o.ByKey) ByKey[k] = ByKey.GetValueOrDefault(k) + v;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, int step)
        {
            N++;
            var hero = e[0];
            int id = hero.InstanceId;
            var mine = p.Select(u => u.InstanceId).ToHashSet();
            bool alive = true; int? killT = null, firstT = null; long taken = 0; int peak = 0;
            foreach (var x in r.Events)
            {
                switch (x.Kind)
                {
                    case BattleEventKind.TurnStart when x.Turn <= Window && alive: Alive++; break;
                    case BattleEventKind.StatSnapshot when x.TargetId == id: peak = Math.Max(peak, x.Amount); break;
                    case BattleEventKind.Attack when x.ActorId == id && x.Turn <= Window: Acts++; break;
                    case BattleEventKind.Damage when x.ActorId == id && x.TargetId is int t && mine.Contains(t) && x.Turn <= Window: Dealt += x.Amount; break;
                    case BattleEventKind.Damage when x.TargetId == id && x.Amount > 0 && x.Turn <= Window: taken += x.Amount; break;
                    case BattleEventKind.Heal when x.TargetId is int h && mine.Contains(h) && x.Turn <= Window: Healed += x.Amount; break;
                    case BattleEventKind.Death when x.TargetId is int d:
                        if (d == id && alive) { alive = false; killT = x.Turn; }
                        if (mine.Contains(d)) firstT ??= x.Turn;
                        break;
                }
            }
            int c = hero.RawCounter(IndomitableTrait.CountKey);
            Ctrl += c;
            foreach (string k in StatusKeys.Control) { int n = hero.RawCounter(IndomitableTrait.CountKey + ":" + k); if (n > 0) ByKey[k] = ByKey.GetValueOrDefault(k) + n; }
            IndomAtk += (long)step * c;
            int endT = killT ?? r.Turns;
            CeilAtk += BossRiseTrait.Calc * Math.Max(0, endT - 1);
            PeakAtk += peak;
            if (r.PlayerWon) { Wins++; WinT += r.Turns; if (r.PlayerStarterFallen.Count == 0) Surv++; }
            if (killT is int kt) { Kill++; KillT += kt; } else NoKillDmg += taken;
            if (firstT is int ft) { FirstDeath++; FirstDeathT += ft; }
        }

        public double Win => 100.0 * Wins / N;
        public double KillTAvg => Kill == 0 ? double.PositiveInfinity : (double)KillT / Kill;
    }

    static IAgg IMeasure(string board, IVer v)
    {
        var f = CW.BoardOf(board);
        var parts = new IAgg[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var (r, p, e) = CW.Fight(f, () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), i, verbose: true);
            var a = new IAgg(); a.Take(r, p, e, v.Step); parts[i] = a;
        });
        var all = new IAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string IKill(IAgg a) => a.Kill == 0 ? $"倒せない（{Per(a.NoKillDmg, a.N)}）" : $"T{Per(a.KillT, a.Kill)}（{Pct(a.Kill, a.N)}%）";
    static string Keys(IAgg a) => a.ByKey.Count == 0 ? "—" : string.Join("・", StatusKeys.Control.Where(a.ByKey.ContainsKey).Select(k => $"{StatusKeys.LabelOf(k)} {Per(a.ByKey[k], a.N)}"));

    /// <summary>Phase 0-2: F-0（素通し）で各台が1戦に手番を奪う回数と、N 候補での不屈の積みの見積もり。</summary>
    static void Indom0()
    {
        Console.WriteLine("# 第266期 Phase 0 —— F-0（不屈 N=0・素通し）で手番を奪う回数と、N ごとの積みの見積もり（7台・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("「回数」は勇者の手番を奪う状態の値が**実際に上がった**回数（`IndomitableTrait.CountKey`）。見積もりは「F-0 の回数 × N」で、N を上げたときに台の振る舞いが変わる分（止めるのを控える・早く倒れる）は入っていない。");
        Console.WriteLine();
        var f0 = IVerOf("F-0"); var d40 = IVerOf("D-40");
        Console.WriteLine("| 台 | 回数 ／ 戦（内訳）| 勇者が振れた手番 F-0 ／ D-40 | 倒しT F-0 ／ D-40 | 勝率 F-0 ／ D-40 | 天井の積み（倒れたT）| 見積もり 不屈 N=11 ／ N=22 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (string b in Boards)
        {
            var a = IMeasure(b, f0); var d = IMeasure(b, d40);
            Console.WriteLine($"| {b} | {Per(a.Ctrl, a.N)}（{Keys(a)}）| {Pct(a.Acts, a.Alive)}% ／ {Pct(d.Acts, d.Alive)}% | {IKill(a)} ／ {IKill(d)} | {F1(a.Win)} ／ {F1(d.Win)} | +{Per(a.CeilAtk, a.N)} | +{Per(a.Ctrl * 11, a.N)} ／ +{Per(a.Ctrl * 22, a.N)} |");
        }
    }

    static void IndomRun()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第266期 —— 不屈（勇者1体・N = 0 ／ 11 ／ 22）＋ 対照2つ（7台・倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"勝ち ＝ 勇者を倒す（engine の打ち切り {BattleEngine.MaxTurns} ターン）。純実入り・振れた手番は {Window} ターンまで。「不屈の積み」は戦の終わりの回数 × N、「天井の積み」は 11 ×（勇者が倒れた／戦が終わったT − 1）。");
        Console.WriteLine();
        foreach (var v in IVers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        var res = new Dictionary<(string, string), IAgg>();
        foreach (string b in Boards) foreach (var v in IVers) res[(b, v.Name)] = IMeasure(b, v);

        Console.WriteLine("## 表1 倒しT（倒した戦の平均・割合。倒せない台は 20 ターンまでの総与ダメ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", IVers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(IVers.Select(_ => "--:|")));
        foreach (string b in Boards) Console.WriteLine($"| {b} | " + string.Join(" | ", IVers.Select(v => IKill(res[(b, v.Name)]))) + " |");
        Console.WriteLine();
        Console.WriteLine("## 表2 勝率 ／ 全員生存（%）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", IVers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(IVers.Select(_ => "--:|")));
        foreach (string b in Boards) Console.WriteLine($"| {b} | " + string.Join(" | ", IVers.Select(v => $"{F1(res[(b, v.Name)].Win)} ／ {F1(100.0 * res[(b, v.Name)].Surv / res[(b, v.Name)].N)}")) + " |");
        Console.WriteLine();
        Console.WriteLine("## 表3 手番を奪った回数・不屈の積み・天井の積み・勇者が振れた手番（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 手番を奪った回数（内訳）| 不屈の積み | 天井の積み | 攻撃力の最大（平均）| 勇者が振れた手番 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (string b in Boards)
            foreach (var v in IVers.Take(4))
            {
                var a = res[(b, v.Name)];
                Console.WriteLine($"| {b} | {v.Name} | {Per(a.Ctrl, a.N)}（{Keys(a)}）| +{Per(a.IndomAtk, a.N)} | +{Per(a.CeilAtk, a.N)} | {Per(a.PeakAtk, a.N)} | {a.Acts} ／ {a.Alive}（{Pct(a.Acts, a.Alive)}%）|");
            }
        Console.WriteLine();
        Console.WriteLine("## 表4 純実入り（勇者が入れた − 隊の回復・20 ターンまで）・崩れ始め（最初の死亡のT・割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", IVers.Select(v => "純実入り " + v.Name)) + " | " + string.Join(" | ", IVers.Select(v => "崩れ始め " + v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(IVers.Select(_ => "--:|")) + string.Concat(IVers.Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", IVers.Select(v => Per(res[(b, v.Name)].Dealt - res[(b, v.Name)].Healed, res[(b, v.Name)].N)))
                + " | " + string.Join(" | ", IVers.Select(v => $"{T(res[(b, v.Name)].FirstDeathT, res[(b, v.Name)].FirstDeath)}（{Pct(res[(b, v.Name)].FirstDeath, res[(b, v.Name)].N)}%）")) + " |");
        Console.WriteLine();

        // ---- 採否 ----
        Console.WriteLine("## 表5 採否（指示書 §4）");
        Console.WriteLine();
        // 制御の台 ＝ F-0 で1戦に手番を奪う回数が 1 回以上の台（予め固定した名前ではなく、計数で決める）
        var ctrl = Boards.Where(b => (double)res[(b, "F-0")].Ctrl / Seeds >= 1.0).ToList();
        var quiet = Boards.Where(b => res[(b, "F-0")].Ctrl == 0).ToList();
        Console.WriteLine($"制御の台（F-0 で手番を奪う回数 ≥ 1 ／ 戦）: {(ctrl.Count == 0 ? "—" : string.Join("・", ctrl))}。回数 0 の台: {(quiet.Count == 0 ? "—" : string.Join("・", quiet))}。");
        Console.WriteLine();
        Console.WriteLine("### 5-1 N にまたがる勾配（倒しT は伸び、勝率は下がる向きで単調か）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倒しT F-0 → F-11 → F-22 | 勝率 F-0 → F-11 → F-22 | 倒しT 単調 | 勝率 単調 | 動いた |");
        Console.WriteLine("|---|---|---|:-:|:-:|:-:|");
        int grad = 0;
        foreach (string b in ctrl)
        {
            var k = new[] { "F-0", "F-11", "F-22" }.Select(n => res[(b, n)].KillTAvg).ToArray();
            var w = new[] { "F-0", "F-11", "F-22" }.Select(n => res[(b, n)].Win).ToArray();
            bool kt = k[0] <= k[1] + 1e-9 && k[1] <= k[2] + 1e-9, wt = w[0] >= w[1] - 1e-9 && w[1] >= w[2] - 1e-9;
            bool moved = k[2] - k[0] >= 0.05 || w[0] - w[2] >= 0.5;
            if (kt && wt && moved) grad++;
            Console.WriteLine($"| {b} | {string.Join(" → ", new[] { "F-0", "F-11", "F-22" }.Select(n => IKill(res[(b, n)])))} | {string.Join(" → ", w.Select(F1))} | {(kt ? "○" : "×")} | {(wt ? "○" : "×")} | {(moved ? "○" : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"単調で動いた制御の台: **{grad} ／ {ctrl.Count}**。");
        Console.WriteLine();
        Console.WriteLine("### 5-2 完全無効（D-40）と F-11 ／ F-22 の間に中間帯があるか（倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 台 | F-0 | F-11 | F-22 | D-40（完全無効）| F-11 が F-0 と D-40 の間 | F-22 が F-0 と D-40 の間 |");
        Console.WriteLine("|---|--:|--:|--:|--:|:-:|:-:|");
        foreach (string b in ctrl)
        {
            double f0 = res[(b, "F-0")].KillTAvg, d = res[(b, "D-40")].KillTAvg;
            string Between(string n)
            {
                double x = res[(b, n)].KillTAvg; double lo = Math.Min(f0, d), hi = Math.Max(f0, d);
                return x > lo + 0.05 && x < hi - 0.05 ? "○" : (x >= hi - 0.05 ? "無効側以上" : "素通し側");
            }
            Console.WriteLine($"| {b} | {IKill(res[(b, "F-0")])} | {IKill(res[(b, "F-11")])} | {IKill(res[(b, "F-22")])} | {IKill(res[(b, "D-40")])} | {Between("F-11")} | {Between("F-22")} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 5-3 回数 0 の台は3版で動かないか（計数の混入の検査）・全軸が勝てるか");
        Console.WriteLine();
        Console.WriteLine("| 台 | F-0 ＝ F-11 ＝ F-22（倒しT と勝率）| 勝率 0% の版 |");
        Console.WriteLine("|---|:-:|---|");
        foreach (string b in Boards)
        {
            bool same = quiet.Contains(b)
                ? new[] { "F-11", "F-22" }.All(n => res[(b, n)].KillT == res[(b, "F-0")].KillT && res[(b, n)].Kill == res[(b, "F-0")].Kill && res[(b, n)].Wins == res[(b, "F-0")].Wins)
                : false;
            var zero = new[] { "F-0", "F-11", "F-22" }.Where(n => res[(b, n)].Wins == 0).ToList();
            Console.WriteLine($"| {b} | {(quiet.Contains(b) ? (same ? "○" : "×（混入を疑う）") : "—（制御の台）")} | {(zero.Count == 0 ? "—" : string.Join("・", zero))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log266(string board, string ver, int seed)
    {
        var v = IVerOf(ver);
        var (r, p, e) = CW.Fight(CW.BoardOf(board), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), seed);
        Console.WriteLine($"# {board} × {v.Name}（{v.What}）× seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・手番を奪った回数 {e[0].RawCounter(IndomitableTrait.CountKey)}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    static void Check266()
    {
        int pass = 0, fail = 0;
        void Ok(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            Console.WriteLine($"- [{(ok ? "○" : "×")}] {name}{(detail.Length > 0 ? $"（{detail}）" : "")}");
        }
        Console.WriteLine("# bosswave check266 —— 第266期 自己検査");
        Console.WriteLine();

        // (a) 不屈: 5種とも通り、上がるたびに数える。値が変わらない書き込み・下げる書き込み・手番を奪わない状態は数えない
        {
            var (ctx, e) = Board(IVerOf("F-11").Make());
            var h = e[0];
            bool pass5 = StatusKeys.Control.All(k => { h.SetCounter(k, 1); return h.RawCounter(k) == 1; });
            Ok("(a-1) 手番を奪う状態 5 種がすべて付く", pass5);
            Ok("(a-2) 5 種を1回ずつで 5 回・攻撃力 +55", h.RawCounter(IndomitableTrait.CountKey) == 5 && h.CurrentAttack == 12 + 55, $"{h.RawCounter(IndomitableTrait.CountKey)} 回・攻 {h.CurrentAttack}");
            h.SetCounter(StatusKeys.Stun, 1);
            h.SetCounter(StatusKeys.Stun, 0);
            Ok("(a-3) 同じ値の書き込み・下げる書き込みは数えない", h.RawCounter(IndomitableTrait.CountKey) == 5);
            h.SetCounter(StatusKeys.Stun, 2);
            Ok("(a-4) 0 → 2 は1回（量ではなく回数）", h.RawCounter(IndomitableTrait.CountKey) == 6);
            h.SetCounter(StatusKeys.Poison, 5); h.SetCounter(StatusKeys.Daunted, 1); h.SetCounter(StatusKeys.Numbed, 1); h.SetCounter(StatusKeys.Burn, 2);
            Ok("(a-5) 毒・萎縮・鈍り・燃焼は数えない", h.RawCounter(IndomitableTrait.CountKey) == 6);
            Ok("(a-6) 内訳の和 ＝ 回数", StatusKeys.Control.Sum(k => h.RawCounter(IndomitableTrait.CountKey + ":" + k)) == 6);
        }
        {
            var (ctx, e) = Board(IVerOf("F-0").Make());
            var h = e[0];
            foreach (string k in StatusKeys.Control) h.SetCounter(k, 1);
            Ok("(b-1) F-0: 5 回数えるが攻撃力は 12 のまま", h.RawCounter(IndomitableTrait.CountKey) == 5 && h.CurrentAttack == 12, $"攻 {h.CurrentAttack}");
            var (ctx2, e2) = Board(IVerOf("F-22").Make());
            e2[0].SetCounter(StatusKeys.Stagger, 1);
            Ok("(b-2) F-22: 1回で攻撃力 +22", e2[0].CurrentAttack == 12 + 22, $"攻 {e2[0].CurrentAttack}");
            var (ctx3, e3) = Board(IVerOf("D-40").Make());
            e3[0].SetCounter(StatusKeys.Stun, 1);
            Ok("(b-3) D-40: 動じないは痺れを塞ぎ、数えない", e3[0].RawCounter(StatusKeys.Stun) == 0 && e3[0].RawCounter(IndomitableTrait.CountKey) == 0);
        }
        // (c) 天井と不屈は別の箱（天井はターン数から・不屈は回数から）。AtkBonus に積まない
        {
            var (ctx, e) = Board(IVerOf("F-11").Make());
            var h = e[0];
            h.SetCounter(StatusKeys.Stun, 1);
            Ok("(c) 不屈は AtkBonus に積まない（攻撃力 +11 は ModifyAttack の側）", h.AtkBonus == 0 && h.CurrentAttack == 23, $"AtkBonus {h.AtkBonus}・攻 {h.CurrentAttack}");
        }
        // (d) 自前の回復 40%
        {
            var (ctx, e) = Board(IVerOf("F-11").Make());
            var h = e[0]; h.Hp = 100;
            TraitCatalog.Resolve(new[] { TraitId.BossMend40 })[0].OnTurnStart(ctx, h);
            Ok("(d) 自前の回復: HP 100 → 1,300", h.Hp == 1300, $"{h.Hp}");
        }
        // (e) 保持者 0
        {
            TraitId[] mine = { TraitId.BossMend40, TraitId.Indomitable0, TraitId.Indomitable11, TraitId.Indomitable22 };
            var enemyDefs = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def))
                .Concat(EnemyCatalog.TestStages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)))
                .Concat(EnemyCatalog.BossStages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)));
            int holders = UnitCatalog.Everyone.Concat(enemyDefs).Count(u => u.Traits.Any(mine.Contains));
            Ok("(e) 新しい札 4 枚の保持者がロスターと本編・検証・第265期の敵に 0 枚", holders == 0, $"{holders} 枚");
        }
        // (f) 乱数を引かない
        {
            string src = File.ReadAllText(Path.Combine("BattleCore", "Traits.cs"));
            int from = src.IndexOf("public sealed class IndomitableTrait", StringComparison.Ordinal);
            int to = src.IndexOf("public sealed class BossRiseTrait", StringComparison.Ordinal);
            string body = from >= 0 && to > from ? src[from..to] : "";
            Ok("(f) 不屈の本文に `PickOne` ／ `Roll` が 0", body.Length > 0 && !body.Contains("PickOne") && !body.Contains("Roll"), $"{body.Length} 字");
        }
        // (g) 決定性
        {
            bool same = true; long lines = 0;
            foreach (var v in IVers.Take(3))
                foreach (string b in Boards)
                    for (int s = 0; s < 4; s++)
                    {
                        var r1 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), s).R;
                        var r2 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), s).R;
                        same &= r1.Log.Select(l => l.Text).SequenceEqual(r2.Log.Select(l => l.Text)) && r1.Turns == r2.Turns;
                        lines += r1.Log.Count;
                    }
            Ok("(g) 同じ seed の2回が台本一致（3版 × 7台 × seed 0..3）", same, $"{lines:N0} 行");
        }
        // (h) F-0 と「不屈の札なし」は台本が一致する（数えるだけで盤面を動かさない・ログの不屈の行を除く）
        {
            UnitDef bare = new()
            {
                Id = "bw_indom", Name = "勇者", MaxHp = CW.ChosenBossHp, Attack = 12, Speed = 14, Pattern = AttackPattern.All,
                Traits = new[] { TraitId.BossRise11, TraitId.BossMend40 },
            };
            bool same = true; int n = 0;
            foreach (string b in Boards)
                for (int s = 0; s < 8; s++)
                {
                    var r1 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(IVerOf("F-0").Make(), EnemyScaleRule.None), s).R;
                    var r2 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(EnemyWave.Of((2, bare)), EnemyScaleRule.None), s).R;
                    same &= r1.Log.Select(l => l.Text).Where(t => !t.Contains("は屈しない")).SequenceEqual(r2.Log.Select(l => l.Text)) && r1.Turns == r2.Turns && r1.PlayerWon == r2.PlayerWon;
                    n++;
                }
            Ok("(h) F-0 ＝ 不屈の札なし（7台 × seed 0..7・「屈しない」の行を除いて台本一致）", same, $"{n} 戦");
        }
        Console.WriteLine();
        Console.WriteLine($"合計 {pass + fail} 項目: ○ {pass} ／ × {fail}");
    }
}
