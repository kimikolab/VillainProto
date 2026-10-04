using BattleCore;
using static Common;
using CW = CheckWaveDiag;

// bosswave guard0 ／ guard ／ check267 ／ log267 —— 第267期「動じないに戻し、勇者が癒し手を庇う」。
// 指示書は design/PHASE267_BOSS_GUARDED_HEALER_SPEC.md ／ 報告は design/PHASE267_BOSS_GUARDED_HEALER.md。
// 勇者（中央）: 全体・HP 3,000・攻 12・速 14・天井 +11・動じない（第261期の札）・自前の回復なし・勇者の印（癒し手の回復先の印）・勇者の庇い（75 ／ 100）。
// 癒し手: 第265期の癒し手（城塞の重装兵 145/12/3 ＋ `HeroMend`・ターン頭に勇者を最大HPの 40%）。席は Phase 0 で決める（`guard0`）。
static partial class BossWaveDiag
{
    static UnitDef GuardHero(TraitId shield) => new()
    {
        Id = "bw_guard", Name = "勇者", MaxHp = CW.ChosenBossHp, Attack = 12, Speed = 14, Pattern = AttackPattern.All,
        // 動じない（常時）を勇者の印より前に置く（`ControlBlockedNow` が先頭で抜ける）。勇者の印はここでは「癒し手の回復先」の印として働く。
        Traits = new[] { TraitId.BossRise11, TraitId.BossSteadfast, TraitId.HeroCrest, shield },
        PlusText = "全員を薙ぎ払い、毎ターン攻撃力が 11 上がる。転ばず痺れない。癒し手への単体攻撃を庇う（第267期・bosswave）",
    };
    static readonly string[] SeatLabel = FormationRules.SeatNames;
    /// <summary>規定の席（Phase 0 の `guard0` で決めた・報告書 §1 0-6）。</summary>
    const int HealerSeat = 0;

    sealed record GVer(string Name, string What, Func<EnemyWave> Make);
    static GVer GV(TraitId shield, int seat) => new($"{(shield == TraitId.HeroShield100 ? "G-庇100" : "G-庇75")}", $"癒し手 {SeatLabel[seat]}",
        () => EnemyWave.Of((2, GuardHero(shield)), (seat, EnemyCatalog.BossMender)));
    static GVer[] GVers => new[]
    {
        GV(TraitId.HeroShield75, HealerSeat),
        GV(TraitId.HeroShield100, HealerSeat),
        new GVer("D-40", "対照: 第266期の D-40（動じない・自前の回復 40%・癒し手なし）", () => IVerOf("D-40").Make()),
    };

    static readonly string[] Routes = { "単体", "範囲", "刻み", "その他" };

    sealed class GAgg
    {
        public long N, Wins, WinT, Surv, Kill, KillT, NoKillDmg, FirstDeath, FirstDeathT;
        public long MendKill, MendKillT, Shielded, Leaked, HeroHealed, Alive, Acts, Dealt, Healed, MendFirst, HeroAfterMend;
        public readonly long[] Route = new long[4];

        public void Merge(GAgg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Surv += o.Surv; Kill += o.Kill; KillT += o.KillT; NoKillDmg += o.NoKillDmg; FirstDeath += o.FirstDeath; FirstDeathT += o.FirstDeathT;
            MendKill += o.MendKill; MendKillT += o.MendKillT; Shielded += o.Shielded; Leaked += o.Leaked; HeroHealed += o.HeroHealed;
            Alive += o.Alive; Acts += o.Acts; Dealt += o.Dealt; Healed += o.Healed; MendFirst += o.MendFirst; HeroAfterMend += o.HeroAfterMend;
            for (int i = 0; i < 4; i++) Route[i] += o.Route[i];
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++;
            int hero = e.First(u => u.Def.Pattern == AttackPattern.All).InstanceId;
            int? mend = e.FirstOrDefault(u => u.HasTrait(TraitId.HeroMend))?.InstanceId;
            var mine = p.Select(u => u.InstanceId).ToHashSet();
            bool heroAlive = true, snap = false; int? killT = null, mendT = null, firstT = null; long taken = 0; int lastRoute = 3;
            foreach (var x in r.Events)
            {
                switch (x.Kind)
                {
                    case BattleEventKind.TurnStart: snap = false; if (x.Turn <= Window && heroAlive) Alive++; break;
                    case BattleEventKind.StatSnapshot: snap = true; break;
                    case BattleEventKind.Intercept when x.ActorId == hero && x.TargetId == mend: Shielded++; break;
                    case BattleEventKind.Attack when x.ActorId == hero && x.Turn <= Window: Acts++; break;
                    case BattleEventKind.Heal when x.TargetId == hero: HeroHealed += x.Amount; break;
                    case BattleEventKind.Heal when x.TargetId is int h && mine.Contains(h) && x.Turn <= Window: Healed += x.Amount; break;
                    case BattleEventKind.Damage when x.ActorId == hero && x.TargetId is int t && mine.Contains(t) && x.Turn <= Window: Dealt += x.Amount; break;
                    case BattleEventKind.Damage when x.TargetId == hero && x.Amount > 0 && x.Turn <= Window: taken += x.Amount; break;
                    case BattleEventKind.Damage when mend is int m && x.TargetId == m && x.Amount > 0:
                        if (x.Pattern == AttackPattern.Single && !x.Relayed) Leaked++;
                        lastRoute = x.Pattern switch
                        {
                            AttackPattern.Single => 0,
                            AttackPattern.Sweep or AttackPattern.Pierce or AttackPattern.All => 1,
                            null when !snap => 2,
                            _ => 3,
                        };
                        break;
                    case BattleEventKind.Death when x.TargetId is int d:
                        if (d == hero && heroAlive) { heroAlive = false; killT = x.Turn; }
                        if (d == mend && mendT is null) mendT = x.Turn;
                        if (mine.Contains(d)) firstT ??= x.Turn;
                        break;
                }
            }
            if (r.PlayerWon) { Wins++; WinT += r.Turns; if (r.PlayerStarterFallen.Count == 0) Surv++; }
            if (killT is int kt) { Kill++; KillT += kt; } else NoKillDmg += taken;
            if (mendT is int mt) { MendKill++; MendKillT += mt; Route[lastRoute]++; if (killT is null || mt <= killT) MendFirst++; if (killT is int k2 && mt <= k2) HeroAfterMend += k2 - mt; }
            if (firstT is int ft) { FirstDeath++; FirstDeathT += ft; }
        }

        public double Win => 100.0 * Wins / N;
        public double KillTAvg => Kill == 0 ? double.PositiveInfinity : (double)KillT / Kill;
        public string RouteText => MendKill == 0 ? "—" : string.Join("・", Enumerable.Range(0, 4).Where(i => Route[i] > 0).Select(i => $"{Routes[i]} {Pct(Route[i], MendKill)}"));
        /// <summary>割った手段の最多（割れなければ「割れない」）。</summary>
        public string ModalRoute => MendKill == 0 ? "割れない" : Routes[Enumerable.Range(0, 4).OrderByDescending(i => Route[i]).First()];
    }

    static GAgg GMeasure(string board, Func<EnemyWave> make)
    {
        var f = CW.BoardOf(board);
        var parts = new GAgg[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var (r, p, e) = CW.Fight(f, () => BattleEngine.MaterializeEnemy(make(), EnemyScaleRule.None), i, verbose: true);
            var a = new GAgg(); a.Take(r, p, e); parts[i] = a;
        });
        var all = new GAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string GKill(GAgg a) => a.Kill == 0 ? $"倒せない（{Per(a.NoKillDmg, a.N)}）" : $"T{Per(a.KillT, a.Kill)}（{Pct(a.Kill, a.N)}%）";
    static string MendT(GAgg a) => a.MendKill == 0 ? "割れない" : $"T{Per(a.MendKillT, a.MendKill)}（{Pct(a.MendKill, a.N)}%）";

    /// <summary>Phase 0-6: 癒し手の席（前1 ／ 後1 ／ ○後2）ごとに、単体攻撃が癒し手を狙った回数（庇われた ＋ 漏れた）と、割ったT・手段・勝率。</summary>
    static void Guard0()
    {
        Console.WriteLine("# 第267期 Phase 0 —— 癒し手の席（前1 ／ 後1 ／ ○後2）× 庇い 75 ／ 100（7台・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("「庇った」＝ 勇者が差し替えた単体攻撃の数、「漏れた」＝ 癒し手に入った単体攻撃の数（1戦あたり）。席の候補は前列（単体の第一候補）・後1（第265期の席）・○後2（貫きが届かない）。");
        Console.WriteLine();
        Console.WriteLine("| 席 | 版 | 台 | 庇った ／ 漏れた | 癒し手を割ったT | 割った手段 | 勇者の倒しT | 勝率 |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|--:|");
        foreach (int seat in new[] { 0, 3, 8 })
            foreach (var shield in new[] { TraitId.HeroShield75, TraitId.HeroShield100 })
            {
                var v = GV(shield, seat);
                foreach (string b in Boards)
                {
                    var a = GMeasure(b, v.Make);
                    Console.WriteLine($"| {SeatLabel[seat]} | {v.Name} | {b} | {Per(a.Shielded, a.N)} ／ {Per(a.Leaked, a.N)} | {MendT(a)} | {a.RouteText} | {GKill(a)} | {F1(a.Win)} |");
                }
            }
    }

    static void GuardRun()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = GVers;
        Console.WriteLine($"# 第267期 —— 勇者が癒し手を庇う（癒し手 {SeatLabel[HealerSeat]}・庇い 75 ／ 100）＋ 対照 D-40（7台・倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"勝ち ＝ 敵全滅（engine の打ち切り {BattleEngine.MaxTurns} ターン）。純実入り・振れた手番は {Window} ターンまで。割った手段は癒し手への最後の一撃の型（単体 ／ 範囲 ＝ 薙ぎ・貫き・全体 ／ 刻み ＝ ターン頭の型なし ／ その他 ＝ 手番中の型なし〈反撃・放電など〉）。");
        Console.WriteLine();
        foreach (var v in vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        var res = new Dictionary<(string, string), GAgg>();
        foreach (string b in Boards) foreach (var v in vers) res[(b, v.Name)] = GMeasure(b, v.Make);

        Console.WriteLine("## 表1 勝率 ／ 全員生存（%）・勇者の倒しT");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", vers.Select(v => "勝率 " + v.Name)) + " | " + string.Join(" | ", vers.Select(v => "倒しT " + v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(vers.Select(_ => "--:|")) + string.Concat(vers.Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", vers.Select(v => $"{F1(res[(b, v.Name)].Win)} ／ {F1(100.0 * res[(b, v.Name)].Surv / res[(b, v.Name)].N)}")) + " | " + string.Join(" | ", vers.Select(v => GKill(res[(b, v.Name)]))) + " |");
        Console.WriteLine();
        Console.WriteLine("## 表2 癒し手 —— 庇った ／ 漏れた単体・割ったT・割った手段・勇者が受けた回復（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 庇った ／ 漏れた | 癒し手を割ったT | 割った手段 | 勇者より先に割った | 割ってから勇者が倒れるまで | 勇者が受けた回復 |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|");
        foreach (string b in Boards)
            foreach (var v in vers.Take(2))
            {
                var a = res[(b, v.Name)];
                Console.WriteLine($"| {b} | {v.Name} | {Per(a.Shielded, a.N)} ／ {Per(a.Leaked, a.N)} | {MendT(a)} | {a.RouteText} | {Pct(a.MendFirst, a.N)}% | {(a.MendFirst == 0 ? "—" : Per(a.HeroAfterMend, a.MendFirst) + " T")} | {Per(a.HeroHealed, a.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 表3 純実入り（20 ターンまで）・崩れ始め・勇者が振れた手番");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", vers.Select(v => "純実入り " + v.Name)) + " | " + string.Join(" | ", vers.Select(v => "崩れ始め " + v.Name)) + " | " + string.Join(" | ", vers.Select(v => "振れた手番 " + v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, vers.Length * 3).Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", vers.Select(v => Per(res[(b, v.Name)].Dealt - res[(b, v.Name)].Healed, res[(b, v.Name)].N)))
                + " | " + string.Join(" | ", vers.Select(v => $"{T(res[(b, v.Name)].FirstDeathT, res[(b, v.Name)].FirstDeath)}（{Pct(res[(b, v.Name)].FirstDeath, res[(b, v.Name)].N)}%）"))
                + " | " + string.Join(" | ", vers.Select(v => $"{Pct(res[(b, v.Name)].Acts, res[(b, v.Name)].Alive)}%")) + " |");
        Console.WriteLine();

        Console.WriteLine("## 表4 採否（指示書 §4）");
        Console.WriteLine();
        Console.WriteLine("### 4-1 毒2台の勝率 > 0 か・勝ち筋が「刻みで癒し手を割る」経由か");
        Console.WriteLine();
        Console.WriteLine("| 版 | 台 | 勝率 | 癒し手を割ったT | 割った手段 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|---|:-:|");
        foreach (var v in vers.Take(2))
            foreach (string b in PoisonBoards.OrderBy(x => x, StringComparer.Ordinal))
            {
                var a = res[(b, v.Name)];
                bool ok = a.Wins > 0 && a.ModalRoute == "刻み";
                Console.WriteLine($"| {v.Name} | {b} | {F1(a.Win)} | {MendT(a)} | {a.RouteText} | {(ok ? "○" : "×")} |");
            }
        Console.WriteLine();
        Console.WriteLine("### 4-2 割った手段が軸で分かれるか（台ごとの最多の手段）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 台ごとの最多の手段 | 異なる手段の数 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        foreach (var v in vers.Take(2))
        {
            var m = Boards.Select(b => (b, R: res[(b, v.Name)].ModalRoute)).ToList();
            int kinds = m.Select(x => x.R).Distinct().Count();
            Console.WriteLine($"| {v.Name} | {string.Join("・", m.Select(x => $"{x.b} {x.R}"))} | {kinds} | {(kinds >= 2 ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 4-3 毒以外の台が D-40 から大きく歪まないか（勝率の差・倒しT の差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 G-庇75 − D-40 | 勝率 G-庇100 − D-40 | 倒しT G-庇75 − D-40 | 倒しT G-庇100 − D-40 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        string D(double x) => double.IsNaN(x) || double.IsInfinity(x) ? "—" : x.ToString("+0.0;−0.0;0.0");
        foreach (string b in Boards.Where(x => !PoisonBoards.Contains(x)))
        {
            var d = res[(b, "D-40")];
            Console.WriteLine($"| {b} | {D(res[(b, "G-庇75")].Win - d.Win)} | {D(res[(b, "G-庇100")].Win - d.Win)} | {D(res[(b, "G-庇75")].KillTAvg - d.KillTAvg)} | {D(res[(b, "G-庇100")].KillTAvg - d.KillTAvg)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>参考（採否には使わない）: 癒し手の HP だけを ×k にした G-庇75（前1）。指示書 §2-3 の「数値ノブ（癒し手の HP）の出番」の見積もり。</summary>
    static void GuardBody()
    {
        Console.WriteLine("# 第267期 参考 —— 癒し手の HP だけを厚くした G-庇75（前1・×1 ／ ×2 ／ ×4 ／ ×8・7台・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"癒し手の HP を {EnemyCatalog.Warden.MaxHp} × k にする（攻・速・型・札・席はそのまま）。**採否には使わない**（指示書 §1-2 の「既存の敵と同数値」を外れる）。D-40 の倒しT は 燃焼 T3.0 ／ T2.5・移動 T5.3・雷 T5.7・混ぜ T6.2。");
        Console.WriteLine();
        Console.WriteLine("| k | 台 | 癒し手を割ったT | 割った手段 | 勇者の倒しT | 勝率 ／ 全員生存 |");
        Console.WriteLine("|--:|---|--:|---|--:|--:|");
        foreach (int k in new[] { 1, 2, 4, 8 })
        {
            var b0 = EnemyCatalog.BossMender;
            UnitDef fat = new() { Id = b0.Id, Name = b0.Name, MaxHp = b0.MaxHp * k, Attack = b0.Attack, Speed = b0.Speed, Pattern = b0.Pattern, Advances = b0.Advances, Actions = b0.Actions, Traits = b0.Traits, PlusText = b0.PlusText };
            foreach (string b in Boards)
            {
                var a = GMeasure(b, () => EnemyWave.Of((2, GuardHero(TraitId.HeroShield75)), (HealerSeat, fat)));
                Console.WriteLine($"| {k} | {b} | {MendT(a)} | {a.RouteText} | {GKill(a)} | {F1(a.Win)} ／ {F1(100.0 * a.Surv / a.N)} |");
            }
        }
    }

    static void Log267(string board, string ver, int seed)
    {
        var v = GVers.First(x => x.Name == ver);
        var (r, p, e) = CW.Fight(CW.BoardOf(board), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), seed);
        Console.WriteLine($"# {board} × {v.Name}（{v.What}）× seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    static void Check267()
    {
        int pass = 0, fail = 0;
        void Ok(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            Console.WriteLine($"- [{(ok ? "○" : "×")}] {name}{(detail.Length > 0 ? $"（{detail}）" : "")}");
        }
        Console.WriteLine("# bosswave check267 —— 第267期 自己検査");
        Console.WriteLine();
        Ok("(a) 癒し手は第265期の癒し手そのもの（城塞の重装兵 ＋ `HeroMend` 1枚）", EnemyCatalog.BossMender.MaxHp == EnemyCatalog.Warden.MaxHp && EnemyCatalog.BossMender.Traits.Count == 1 && EnemyCatalog.BossMender.Traits[0] == TraitId.HeroMend);
        {
            var h = GuardHero(TraitId.HeroShield75);
            Ok("(b) 勇者 ＝ HP 3,000・攻 12・速 14・全体・{天井 +11, 動じない, 勇者の印, 庇い}・自前の回復なし",
                h.MaxHp == 3000 && h.Attack == 12 && h.Speed == 14 && h.Pattern == AttackPattern.All && h.Traits.Count == 4
                && !h.Traits.Contains(TraitId.BossMend40) && !h.Traits.Contains(TraitId.BossMendFull));
        }
        // (c) 庇いは単体だけを差し替える。100% は常に、75% は概ね 3/4。癒し手以外への単体は差し替えない
        {
            int n100 = 0, n75 = 0, other = 0, total = 400;
            for (int s = 0; s < total; s++)
            {
                var (ctx, e) = Board(GV(TraitId.HeroShield100, 0).Make());
                var att = ctx.LivingMembers(BattleContext.PlayerTeam).First();
                // 単体で癒し手を狙わせる: 前列の癒し手が pool の唯一の候補
                var t = ctx.SelectTarget(att, AttackPattern.Single);
                if (t is not null && t.HasTrait(TraitId.HeroShield100)) n100++;
            }
            for (int s = 0; s < total; s++)
            {
                var ctx = new BattleContext(s, false);
                var p = BattleEngine.Materialize(CW.BoardOf("雷"), BattleContext.PlayerTeam);
                var e = BattleEngine.MaterializeEnemy(GV(TraitId.HeroShield75, 0).Make(), EnemyScaleRule.None);
                foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
                var t = ctx.SelectTarget(p[0], AttackPattern.Single);
                if (t is not null && HeroShieldTrait.Holds(t)) n75++;
            }
            {
                // 癒し手のいない波（勇者だけ）では差し替えの段に入らない（主目標は勇者そのもの）
                var (ctx, e) = Board(EnemyWave.Of((2, GuardHero(TraitId.HeroShield100))));
                var t = ctx.SelectTarget(ctx.LivingMembers(BattleContext.PlayerTeam).First(), AttackPattern.Single);
                if (t == e[0]) other++;
            }
            Ok("(c-1) 庇い 100: 前列の癒し手を狙った単体は全部勇者へ", n100 == total, $"{n100} ／ {total}");
            Ok("(c-2) 庇い 75: 概ね 3/4（65〜85%）", n75 >= total * 65 / 100 && n75 <= total * 85 / 100, $"{n75} ／ {total}");
            Ok("(c-3) 癒し手がいなければ主目標は勇者のまま", other == 1);
            var (ctx2, e2) = Board(GV(TraitId.HeroShield100, 0).Make());
            var sw = ctx2.SelectTarget(ctx2.LivingMembers(BattleContext.PlayerTeam).First(), AttackPattern.Sweep);
            Ok("(c-4) 薙ぎの主目標は差し替えない（前列の癒し手のまま）", sw is not null && sw.HasTrait(TraitId.HeroMend), sw?.Name ?? "null");
        }
        // (d) 刻みは庇いを通らない: 癒し手の毒は癒し手から引かれる
        {
            var (ctx, e) = Board(GV(TraitId.HeroShield100, 0).Make());
            var mend = e.First(u => u.HasTrait(TraitId.HeroMend)); var hero = e.First(u => HeroShieldTrait.Holds(u));
            mend.SetCounter(StatusKeys.Poison, 10);
            int mh = mend.Hp, hh = hero.Hp;
            ctx.TickStatuses();
            Ok("(d) 毒の刻みは癒し手に入り、勇者は減らない", mend.Hp < mh && hero.Hp == hh, $"癒し手 {mh} → {mend.Hp}・勇者 {hh} → {hero.Hp}");
        }
        // (e) 保持者 0・乱数
        {
            TraitId[] mine = { TraitId.HeroShield75, TraitId.HeroShield100 };
            var enemyDefs = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def))
                .Concat(EnemyCatalog.TestStages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)))
                .Concat(EnemyCatalog.BossStages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)));
            int holders = UnitCatalog.Everyone.Concat(enemyDefs).Count(u => u.Traits.Any(mine.Contains));
            Ok("(e) 庇いの札 2 枚の保持者がロスターと本編・検証・第265期の敵に 0 枚", holders == 0, $"{holders} 枚");
        }
        {
            bool same = true; long lines = 0;
            foreach (var v in GVers.Take(2))
                foreach (string b in Boards)
                    for (int s = 0; s < 4; s++)
                    {
                        var r1 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), s).R;
                        var r2 = CW.Fight(CW.BoardOf(b), () => BattleEngine.MaterializeEnemy(v.Make(), EnemyScaleRule.None), s).R;
                        same &= r1.Log.Select(l => l.Text).SequenceEqual(r2.Log.Select(l => l.Text)) && r1.Turns == r2.Turns;
                        lines += r1.Log.Count;
                    }
            Ok("(f) 同じ seed の2回が台本一致（2版 × 7台 × seed 0..3）", same, $"{lines:N0} 行");
        }
        Console.WriteLine();
        Console.WriteLine($"合計 {pass + fail} 項目: ○ {pass} ／ × {fail}");
    }
}
