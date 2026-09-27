using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// lastdodge check（第227期） —— 自己検査（受け入れ 2〜4・7）
//
// (4) 盤面を直に組んで: 倒れる一撃だけ ／ 軽減（破片）の後で判定 ／ 状態異常と味方からのダメージでは発動しない ／
//     回数が段の表どおり（段0〜1 で1・段2 で2・段3 で3）／ 発動すると通常の回避と同じ連鎖（攻撃力 +3・入れ替え・撃ち返し・段）／
//     会戦の次の戦で回数が戻る
// (3) 逃げ足が一度も発動しない戦は L0 と勝敗・決着ターン・与ダメ・出来事の数まで一致（新しい処理は乱数を引かない）／ verbose の有無
// (7) 台本: `LastDodge` の直後に同じセロの `Evade`・何回目かが 1 から順・数が帳簿と一致
// (2) L0 の台本 ＝ 前段（`shockdigest l227` を実装の前後で突き合わせる・ここでは回さない）
// =====================================================================================
static partial class LastDodgeDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("BattleContext.TallyOf が見つからない");
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static UnitTally Tal(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static int ok, ng;
    static void Expect(string what, object got, object want)
    {
        bool pass = Equals(got, want);
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }

    static BattleContext Ctx(Formation pl, Formation en, int seed, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(seed, false);
        p = BattleEngine.Materialize(OldYomiShio(pl), BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitState U(List<UnitState> p, string id) => p.First(u => u.Def.Id == id);

    /// <summary>
    /// 盤面を組んで <paramref name="act"/> を回す。入口の回避（30%〜）に当たった seed は捨て、
    /// 「入口で避けていない（避けた数 ＝ 逃げ足の数）」seed の結果を返す。
    /// </summary>
    static T Trial<T>(UnitDef seroDef, Func<BattleContext, List<UnitState>, List<UnitState>, T> act, bool noShio = false)
    {
        var recruit = EnemyCatalog.TestStages[0].Enemy.Occupied().Select(o => o.Def).First();
        for (int seed = 0; seed < 500; seed++)
        {
            var ctx = Ctx(Formation.Build(front1: seroDef, front3: UnitCatalog.HaneH0, center: UnitCatalog.Yomi, back1: noShio ? UnitCatalog.Tou : UnitCatalog.Shio, back3: UnitCatalog.BasaG0),
                          Formation.Build(front1: recruit, front3: recruit), seed, out var p, out var e);
            var r = act(ctx, p, e);
            var t = Tal(ctx, U(p, "sero"));
            if (t.Evades == t.LastDodges) return r;
        }
        throw new InvalidOperationException("入口の回避を外す seed が見つからない");
    }

    static partial void CheckImpl()
    {
        ok = ng = 0;
        Console.WriteLine("# 第227期 lastdodge check");
        Console.WriteLine();
        if (Versions.Length < 4) { Console.WriteLine("版の札が見つからない（Phase 0 のコミット）。"); return; }
        UnitDef l1 = VerOf("L1").Sero;

        Console.WriteLine("## (4) 盤面を直に組んで");
        Console.WriteLine();
        // ---- 倒れる一撃だけ ----
        {
            var (hp, used) = Trial(l1, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                ctx.ApplyDamage(s, 10, e[0], pattern: AttackPattern.Single);
                return (s.Hp, Tal(ctx, s).LastDodges);
            });
            Expect("倒れない一撃（10）: 発動しない・HP 42 → 32", $"{hp}/{used}", "32/0");
        }
        {
            var r = Trial(l1, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                int slot0 = s.Slot, moves0 = EvadeTrait.MovesOf(s);
                ctx.ApplyDamage(s, 50, e[0], pattern: AttackPattern.Single);
                var t = Tal(ctx, s);
                return (s.Hp, t.LastDodges, t.Evades, s.RawCounter(EvadeTrait.GainKey), t.EvRipostes, Moved: s.Slot != slot0, Moves: EvadeTrait.MovesOf(s) - moves0, Used: LastDodgeTrait.UsedOf(s));
            });
            Expect("倒れる一撃（50）: かわして HP 42 のまま", r.Hp, 42);
            Expect("倒れる一撃: 逃げ足 1・避けた 1（通常の回避と同じに数える）", $"{r.LastDodges}/{r.Evades}", "1/1");
            Expect("連鎖: 攻撃力 +3", r.Item4, EvadeTrait.Gain);
            Expect("連鎖: 撃ち返し 1", r.EvRipostes, 1L);
            Expect("連鎖: 隣の味方と入れ替わった・移動の段の回数 +1", $"{r.Moved}/{r.Moves}", "True/1");
            Expect("使った回数 1", r.Used, 1);
        }
        // ---- 回数が段の表どおり ----
        foreach (var (moves, want) in new[] { (0, 1), (2, 1), (4, 2), (7, 3) })
        {
            var (dodges, spent, alive) = Trial(l1, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                s.SetCounter(EvadeTrait.MovesKey, moves);
                for (int i = 0; i < 4 && s.IsAlive; i++) { s.Hp = s.MaxHp; ctx.ApplyDamage(s, 999, e[0], pattern: AttackPattern.Single); }
                var t = Tal(ctx, s);
                return (t.LastDodges, t.LastDodgeSpent, s.IsAlive);
            });
            Expect($"回数: 動かされた {moves} 回（身軽さの段 2/4/7）で {want} 回かわして次で倒れる", $"{dodges}/{spent}/{alive}", $"{want}/1/False");
        }
        // ---- 軽減の後で判定（破片） ----
        {
            var r = Trial(l1, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                s.SetCounter(StatusKeys.Armor, 50);
                ctx.ApplyDamage(s, 60, e[0], pattern: AttackPattern.Single);
                return (s.Hp, Tal(ctx, s).LastDodges);
            });
            Expect("破片 50 ＋ 一撃 60（HP に 10）: 倒れないので発動しない・HP 32", $"{r.Hp}/{r.LastDodges}", "32/0");
        }
        {
            var r = Trial(l1, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                s.SetCounter(StatusKeys.Armor, 10);
                ctx.ApplyDamage(s, 45, e[0], pattern: AttackPattern.Single);
                return (s.Hp, Tal(ctx, s).LastDodges);
            }, noShio: true);   // シオがいると HP 7（4 割未満）で緊急退避 → 移り木で +8 される
            Expect("破片 10 ＋ 一撃 45（HP に 35 < 42・シオ抜きの台）: 発動しない・HP 7", $"{r.Hp}/{r.LastDodges}", "7/0");
        }
        {
            var r = Trial(l1, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                s.SetCounter(StatusKeys.Armor, 10);
                ctx.ApplyDamage(s, 60, e[0], pattern: AttackPattern.Single);
                return (s.Hp, Tal(ctx, s).LastDodges, s.RawCounter(StatusKeys.Armor));
            });
            Expect("破片 10 ＋ 一撃 60（HP に 50 ≥ 42）: 発動・HP 42（破片はその一撃で減ったまま 0）", $"{r.Hp}/{r.LastDodges}/{r.Item3}", "42/1/0");
        }
        // ---- 状態異常・味方からのダメージ ----
        {
            var r = Trial(l1, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                ctx.ApplyDamage(s, 60, null, burnTick: true);
                return (s.IsAlive, Tal(ctx, s).LastDodges);
            });
            Expect("燃焼の刻み（出どころなし）で倒れる: 発動しない", $"{r.IsAlive}/{r.LastDodges}", "False/0");
        }
        {
            var r = Trial(l1, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                ctx.ApplyDamage(s, 60, U(p, "hane"), isFriendlyFire: true);
                return (s.IsAlive, Tal(ctx, s).LastDodges);
            });
            Expect("味方の巻き込み（出どころ ＝ 味方）で倒れる: 発動しない", $"{r.IsAlive}/{r.LastDodges}", "False/0");
        }
        {
            var r = Trial(l1, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                ctx.ApplyDamage(s, 60, e[1], isFriendlyFire: true, relayed: true);
                return (s.IsAlive, Tal(ctx, s).LastDodges);
            });
            Expect("肩代わりの中継（relayed）で倒れる: 発動しない", $"{r.IsAlive}/{r.LastDodges}", "False/0");
        }
        {
            var r = Trial(UnitCatalog.SeroL0, (ctx, p, e) =>
            {
                var s = U(p, "sero");
                ctx.ApplyDamage(s, 60, e[0], pattern: AttackPattern.Single);
                return (s.IsAlive, Tal(ctx, s).LastDodges, Tal(ctx, s).LastDodgeSpent);
            });
            Expect("規定のセロ（札なし）: 倒れる一撃で倒れる・帳簿 0", $"{r.IsAlive}/{r.LastDodges}/{r.LastDodgeSpent}", "False/0/0");
        }
        // ---- 会戦の次の戦 ----
        {
            var p = BattleEngine.Materialize(OldYomiShio(Formation.Build(front1: l1)), BattleContext.PlayerTeam);
            p[0].SetCounter(LastDodgeTrait.UsedKey, 2);
            var next = EngagementEngine.CrossBoundary(p);
            Expect("会戦の境界: 使った回数 2 → 0", LastDodgeTrait.UsedOf(next[0]), 0);
        }
        Console.WriteLine();

        // ---- (3) 乱数を引かない ・ verbose ----
        Console.WriteLine("## (3) 逃げ足が発動しない戦は L0 と一致 ／ verbose の有無");
        Console.WriteLine();
        {
            var benches = new[] { MHane225, Formation.Build(front1: UnitCatalog.SeroL0, front3: UnitCatalog.BasaG0, center: UnitCatalog.Yomi, back1: UnitCatalog.HaneH0, back3: UnitCatalog.Shio), Thunder };
            var jobs = new List<(Formation F, int W, EnemyScaleRule Sc, int S)>();
            foreach (var b in benches)
                foreach (int w in new[] { 4, 5, 0, 1, 2, 3 })
                    foreach (var (_, sc) in Scales)
                        for (int s = 0; s < 30; s++) jobs.Add((b, w, sc, s));
            var same = new int[jobs.Count]; var noDodge = new int[jobs.Count]; var vdiff = new int[jobs.Count];
            Parallel.For(0, jobs.Count, i =>
            {
                var (f, w, sc, s) = jobs[i];
                var a = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(Apply(f, VerOf("L0"))), BattleContext.PlayerTeam), WaveOf(w, sc)(), s, verbose: true, shuffler: PreHole);
                foreach (var tag in new[] { "L1", "L2" })
                {
                    var fv = Apply(f, VerOf(tag));
                    var b = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(fv), BattleContext.PlayerTeam), WaveOf(w, sc)(), s, verbose: true, shuffler: PreHole);
                    var c = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(fv), BattleContext.PlayerTeam), WaveOf(w, sc)(), s, verbose: false, shuffler: PreHole);
                    long db = b.TallyByUnit.Values.Sum(t => t.DamageToEnemy), dc = c.TallyByUnit.Values.Sum(t => t.DamageToEnemy);
                    if (b.PlayerWon != c.PlayerWon || b.Turns != c.Turns || db != dc) Interlocked.Increment(ref vdiff[i]);
                    if (tag != "L1") continue;
                    long ld = b.TallyByUnit.TryGetValue("sero", out var t0) ? t0.LastDodges + t0.LastDodgeSpent : 0;
                    if (ld > 0) continue;
                    noDodge[i] = 1;
                    long da = a.TallyByUnit.Values.Sum(t => t.DamageToEnemy);
                    if (a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && da == db && a.Events.Count == b.Events.Count) same[i] = 1;
                }
            });
            Expect($"L1 で逃げ足の判定に1度も来なかった戦（{noDodge.Sum()} / {jobs.Count} 戦）が L0 と勝敗・決着T・与ダメ・出来事の数まで一致", same.Sum(), noDodge.Sum());
            Expect($"verbose の有無の差（L1・L2 × {jobs.Count} 戦）", vdiff.Sum(), 0);
        }
        Console.WriteLine();

        // ---- (7) 台本 ----
        Console.WriteLine("## (7) 台本");
        Console.WriteLine();
        {
            long ev = 0, evOk = 0, ordOk = 0, tally = 0, stageOk = 0;
            var f = Apply(Formation.Build(front1: UnitCatalog.SeroL0, front3: UnitCatalog.BasaG0, center: UnitCatalog.Yomi, back1: UnitCatalog.HaneH0, back3: UnitCatalog.Shio), VerOf("L2"));
            foreach (int w in new[] { 4, 0, 1, 3 })
                for (int s = 0; s < 200; s++)
                {
                    var (r, p, e, _) = DecoyDiag.Fight(f, w, Scales[0].Sc, s);
                    int sero = U(p, "sero").InstanceId;
                    int expectOrd = 1;
                    var evs = r.Events;
                    for (int i = 0; i < evs.Count; i++)
                    {
                        var x = evs[i];
                        if (x.Kind != BattleEventKind.LastDodge) continue;
                        ev++;
                        var next = evs.Skip(i + 1).FirstOrDefault(y => y.Kind == BattleEventKind.Evade);
                        if (next is not null && next.ActorId == sero && x.ActorId == sero && next.TargetId == x.TargetId) evOk++;
                        if (x.Slot == expectOrd) ordOk++;
                        expectOrd++;
                        if (x.StatusRemaining == LastDodgeTrait.Limits[Math.Clamp(x.Amount, 0, 3)] && x.Slot <= x.StatusRemaining) stageOk++;
                    }
                    if (r.TallyByUnit.TryGetValue("sero", out var t)) tally += t.LastDodges;
                }
            Expect("LastDodge の後の最初の Evade は同じセロ・同じ敵", evOk, ev);
            Expect("何回目か（Slot）が 1 から順", ordOk, ev);
            Expect("上限（StatusRemaining）が段の表どおり・何回目か ≤ 上限", stageOk, ev);
            Expect("LastDodge の数 ＝ 帳簿", ev, tally);
            Expect("LastDodge が台本に出る（0 でない）", ev > 0, true);
            Console.WriteLine($"  （LastDodge {ev} 件）");
        }
        Console.WriteLine();
        Console.WriteLine($"**○ {ok} ／ × {ng}**");
    }
}
