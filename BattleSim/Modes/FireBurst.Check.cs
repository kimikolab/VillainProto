using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// fireburst check —— 自己検査（受け入れ 2〜5）。S0 の台本の突き合わせは `fireburst digest` を実装の前のコミットと cmp で見る。
static partial class FireBurstDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static readonly FieldInfo RngF = typeof(BattleContext).GetField("_rng", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static int ok, ng;
    static void Expect(string what, object got, object want)
    {
        bool pass = Equals(got, want);
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }
    sealed class CountingRandom : Random
    {
        public int N;
        public CountingRandom(int seed) : base(seed) { }
        public override int Next(int maxValue) { N++; return base.Next(maxValue); }
    }
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 1000, int atk = 10, AttackPattern pat = AttackPattern.Single, params TraitId[] tr)
        => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = tr, Pattern = pat };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static void SetLv(UnitState u, int lv) { u.SetCounter(StatusKeys.Burn, 3); u.SetCounter(FireLevelRule.LvKey, lv); }
    static readonly UnitAction[] HiyoAct = { new(ActionKind.Skill, Label: "火を煽る／火を渡す") };
    static UnitDef HiyoD(params TraitId[] extra) => new()
    {
        Id = "hiyo", Name = "hiyo", MaxHp = 1000, Attack = 5, Speed = 1, Actions = HiyoAct,
        Traits = new[] { TraitId.FireStoke, TraitId.TurnGift }.Concat(extra).ToArray(),
    };
    static UnitDef HotaD(params TraitId[] extra) => new()
    {
        Id = "hota", Name = "hota", MaxHp = 1000, Attack = 5, Speed = 1,
        Traits = new[] { TraitId.Pyre, TraitId.PyreStage, TraitId.FireLevel }.Concat(extra).ToArray(),
    };
    static List<BattleEvent> Since(BattleContext ctx, int n0) => ctx.Events.Skip(n0).ToList();
    static int FL(List<BattleEvent> ev, string label) => ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == label);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第244期 fireburst check");
        Console.WriteLine();

        // ---- ② 燃え広がりの上限 ----
        foreach (bool cap in new[] { false, true })
        {
            var borg = Plain("borg", atk: 5, pat: AttackPattern.Sweep, tr: cap ? new[] { TraitId.FireLevel, TraitId.FireSpreadCap } : new[] { TraitId.FireLevel });
            var ctx = Ctx(Formation.Build(front1: borg, front3: HiyoD()), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e);
            var bu = U(p, "borg"); var hy = U(p, "hiyo");
            SetLv(bu, 1); SetLv(hy, 1);
            U(e, "e1").SetCounter(StatusKeys.Burn, 3); U(e, "e3").SetCounter(StatusKeys.Burn, 3);
            ctx.PerformAttack(bu);
            Expect(cap ? "② 上限あり: 燃えていた敵2体に当てても +1（本人 ／ ヒヨ）" : "② 上限なし（S0）: 燃えていた敵2体で +2（本人 ／ ヒヨ）",
                $"{FireLevelRule.Of(bu)}/{FireLevelRule.Of(hy)}", cap ? "2/2" : "3/3");
            if (cap) Expect("② 上限で捨てた育ち（帳簿: 本人 ／ ヒヨ）", $"{ctx.FireBook.SpreadCapped}/{ctx.FireBook.SelfCapped}", "1/1");
        }
        // 5連撃（段3）の手番も1回の攻撃の枠で +1
        {
            var ctx = Ctx(Formation.Build(front1: HotaD(TraitId.FireSpreadCap)), Formation.Build(front1: Plain("e1"), front3: Plain("e3")), out var p, out var e);
            var h = U(p, "hota"); SetLv(h, 3);
            U(e, "e1").SetCounter(StatusKeys.Burn, 3); U(e, "e3").SetCounter(StatusKeys.Burn, 3);
            ctx.TakeTurn(h);
            Expect("② 5連撃の手番（燃えていた敵にだけ当てる）: +1 まで", FireLevelRule.Of(h), 4);
        }

        // ---- ③ 攻撃力を倍率の前で比べる ----
        foreach (bool baseAtk in new[] { false, true })
        {
            var hiyo = baseAtk ? HiyoD(TraitId.StokeBaseAtk) : HiyoD();
            var ctx = Ctx(Formation.Build(front1: hiyo, front3: Plain("borg", atk: 18, tr: TraitId.FireLevel), center: HotaD()), Formation.Build(front1: Plain("e1")), out var p, out _);
            var hy = U(p, "hiyo"); var bo = U(p, "borg"); var ho = U(p, "hota");
            SetLv(hy, 1); SetLv(bo, 1); SetLv(ho, 1);   // ホタは燃えて段1: 倍率の後 5×4 ＝ 20 ＞ 18、倍率の前 5 ＜ 18
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            var st = Since(ctx, n0).First(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Stoke);
            Expect(baseAtk ? "③ 倍率の前: 煽りはボルグ（18 ＞ 5）" : "③ 倍率の後（S0）: 煽りはホタ（20 ＞ 18）", st.TargetId == bo.InstanceId ? "borg" : "hota", baseAtk ? "borg" : "hota");
            SetLv(hy, 3); SetLv(bo, 2); SetLv(ho, 2);
            n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            var g = Since(ctx, n0).First(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Gift);
            Expect(baseAtk ? "③ 倍率の前: 同じ火勢のギフトはボルグが先" : "③ 倍率の後（S0）: 同じ火勢のギフトは段2（×4）のホタが先", g.TargetId == bo.InstanceId ? "borg" : "hota", baseAtk ? "borg" : "hota");
        }

        // ---- ① 大技: ギフトの手番で火勢4 のときだけ ----
        {
            var hota = HotaD(TraitId.PyreBurnout, TraitId.PyreEmbers, TraitId.CallFire, TraitId.FireSpreadCap);
            var ctx = Ctx(Formation.Build(front1: HiyoD(), front3: hota), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e);
            var hy = U(p, "hiyo"); var h = U(p, "hota");
            SetLv(h, 4);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(h);   // 自分の手番
            var ev = Since(ctx, n0);
            Expect("① 自分の手番では火勢4 でも撃たない（段3 以上の 5連撃）", $"{FL(ev, FireLevelLabels.Burnout)}/{ev.Count(x => x.Kind == BattleEventKind.Attack)}", "0/5");
            // ギフトの手番・火勢3 → 撃たない
            SetLv(h, 2); SetLv(hy, 3);
            n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            ev = Since(ctx, n0);
            Expect("① ギフトの手番でも火勢4 未満（呼び火で 2 → 3）なら撃たない", $"{FL(ev, FireLevelLabels.GiftTurn)}/{FL(ev, FireLevelLabels.Burnout)}", "1/0");
            Expect("呼び火: ヒヨのギフトでホタ +1（見出し ／ 育つ）", $"{FL(ev, FireLevelLabels.CallFire)}/{FL(ev, FireLevelLabels.GrowCall)}", "1/1");
            // ギフトの手番・火勢4 → 焼き尽くす
            foreach (var x in e) { x.Hp = 1000; x.SetCounter(StatusKeys.Burn, 0); }
            SetLv(h, 3); SetLv(hy, 3);   // 呼び火で 4 になってからギフトの手番
            n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            ev = Since(ctx, n0);
            var atks = ev.Where(x => x.Kind == BattleEventKind.Attack && x.ActorId == h.InstanceId).ToList();
            Expect("① ギフトの手番・火勢4: 焼き尽くす（見出し1・撃った後 1）", $"{FL(ev, FireLevelLabels.Burnout)}/{ev.First(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Spent && x.TargetId == h.InstanceId).Amount}", "1/1");
            Expect("① 焼き尽くす: 全体1発（×4 ＝ 20）＋ 火の雨 10 発（×1.5 ＝ 7）", $"{atks.Count}/{atks[0].Pattern}/{atks[0].Amount}/{atks[1].Pattern}/{atks[1].Amount}", "11/All/20/Single/7");
            Expect("① 焼き尽くす: 当てた敵がみな燃えている（保つ火）", e.All(x => x.RawCounter(StatusKeys.Burn) > 0), true);
            Expect("① 撃った後の火勢は 1〜2（燃え広がりは枠の出口で +1 まで・当たる前に燃えていた敵が無ければ 1）", FireLevelRule.Of(h), 1);
            Expect("残り火の印が立つ", h.RawCounter(FireBurstRule.EmbersKey), 1);
            // 残り火: ギフトの手番では消費しない（火勢3 のギフトの手番）
            SetLv(h, 2); SetLv(hy, 3);
            n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            ev = Since(ctx, n0);
            Expect("残り火: ギフトの手番では撃たず、印は残る", $"{FL(ev, FireLevelLabels.Embers)}/{h.RawCounter(FireBurstRule.EmbersKey)}", "0/1");
            // 次の自分の手番で残り火（全体 ×2 ＝ 10）
            SetLv(h, 3);
            n0 = ctx.Events.Count;
            ctx.TakeTurn(h);
            ev = Since(ctx, n0);
            var ea = ev.Where(x => x.Kind == BattleEventKind.Attack && x.ActorId == h.InstanceId).ToList();
            Expect("残り火: 次の自分の手番で段の代わりに全体 ×2（1回・印は消える）", $"{FL(ev, FireLevelLabels.Embers)}/{ea.Count}/{ea[0].Pattern}/{ea[0].Amount}/{h.RawCounter(FireBurstRule.EmbersKey)}", "1/1/All/10/0");
            n0 = ctx.Events.Count;
            ctx.TakeTurn(h);
            ev = Since(ctx, n0);
            Expect("残り火: その次の自分の手番は段のまま", $"{FL(ev, FireLevelLabels.Embers)}/{FL(ev, FireLevelLabels.Stage)}", "0/1");
        }
        // 残り火の追加（敵が2体以下）
        {
            var ctx = Ctx(Formation.Build(front1: HotaD(TraitId.PyreBurnout, TraitId.PyreEmbers)), Formation.Build(front1: Plain("e1"), front3: Plain("e3")), out var p, out _);
            var h = U(p, "hota"); SetLv(h, 2); h.SetCounter(FireBurstRule.EmbersKey, 1);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(h);
            var ev = Since(ctx, n0);
            Expect("残り火: 敵が2体以下なら追加で全体 ×2（全体が2回）", ev.Count(x => x.Kind == BattleEventKind.Attack && x.Pattern == AttackPattern.All), 2);
        }
        // 放つ・呼び火（ボルグ）
        {
            var borg = Plain("borg", atk: 10, pat: AttackPattern.Sweep, tr: new[] { TraitId.FireLevel, TraitId.FireUnleash, TraitId.FireSpreadCap });
            var ctx = Ctx(Formation.Build(front1: HiyoD(), front3: borg, center: HotaD(TraitId.CallFire)), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e);
            var hy = U(p, "hiyo"); var bo = U(p, "borg"); var ho = U(p, "hota");
            SetLv(hy, 3); SetLv(bo, 4); SetLv(ho, 1);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);   // 火勢3 のギフト: 相手はボルグ（火勢の高い順）
            var ev = Since(ctx, n0);
            var ua = ev.Where(x => x.Kind == BattleEventKind.Attack && x.ActorId == bo.InstanceId).ToList();
            Expect("① 放つ: ギフトの手番・火勢4 で薙ぎ ×3（攻 10 → 30・1回）", $"{FL(ev, FireLevelLabels.Unleash)}/{ua.Count}/{ua[0].Pattern}/{ua[0].Amount}", "1/1/Sweep/30");
            var hitIds = ev.Where(x => x.Kind == BattleEventKind.Damage && x.ActorId == bo.InstanceId).Select(x => x.TargetId).Distinct().ToList();
            Expect("① 放つ: 当たった敵がみな燃えている", hitIds.All(id => e.First(x => x.InstanceId == id).RawCounter(StatusKeys.Burn) > 0) && hitIds.Count > 0, true);
            Expect("呼び火: ヒヨのギフトで +1、ボルグの放つで +1（ホタ 1 → 3）", $"{FireLevelRule.Of(ho)}/{FL(ev, FireLevelLabels.CallFire)}", "3/2");
            Expect("① 放った後のボルグは 1（当たる前に燃えていた敵が無い）", FireLevelRule.Of(bo), 1);
            ho.SetCounter(StatusKeys.Burn, 0); SetLv(bo, 4); SetLv(hy, 3);
            n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            ev = Since(ctx, n0);
            Expect("呼び火: 燃えていないホタは育たない", $"{FL(ev, FireLevelLabels.CallFire)}/{FireLevelRule.Of(ho)}", "0/0");
        }
        // 火の雨: D は HP の多い順・乱数なし ／ R は 1 発ごとに乱数。ホタの手番だけを数えるため、ギフトの手番の印を直に立てて `TakeTurn(ホタ)` を呼ぶ。
        var giftF = typeof(BattleContext).GetField("_giftTurnActor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        // 基準: 同じ敵 3 体への全体攻撃1発（残り火・大技でない全体と同じ）が引く乱数（全体の当たる順の並べ替え）
        int baseRolls;
        {
            var ctx = Ctx(Formation.Build(front1: HotaD(TraitId.PyreEmbers)),
                Formation.Build(front1: Plain("e1", hp: 1000), front3: Plain("e3", hp: 900), center: Plain("ec", hp: 800)), out var p, out _);
            var h = U(p, "hota"); SetLv(h, 2); h.SetCounter(FireBurstRule.EmbersKey, 1);
            var cr = new CountingRandom(7);
            RngF.SetValue(ctx, cr);
            ctx.TakeTurn(h);
            baseRolls = cr.N;
            Console.WriteLine($"  （基準: 敵 3 体への全体1発の手番（残り火）が引く乱数 {baseRolls}）");
        }
        foreach (bool ordered in new[] { true, false })
        {
            var hota = ordered ? HotaD(TraitId.PyreBurnout, TraitId.FireRainOrdered) : HotaD(TraitId.PyreBurnout);
            var ctx = Ctx(Formation.Build(front1: hota),
                Formation.Build(front1: Plain("e1", hp: 1000), front3: Plain("e3", hp: 900), center: Plain("ec", hp: 800)), out var p, out var e);
            var h = U(p, "hota");
            SetLv(h, 4);
            var cr = new CountingRandom(7);
            RngF.SetValue(ctx, cr);
            giftF.SetValue(ctx, h);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(h);
            var ev = Since(ctx, n0);
            var rain = ev.Where(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Rain).ToList();
            if (ordered)
            {
                Expect("火の雨 D: 10 発とも「その瞬間の HP が最も多い敵」（全体の後 980 ／ 880 ／ 780 → 10 発では 880 を割らない）", rain.Count(x => x.TargetId == U(e, "e1").InstanceId), 10);
                Expect("火の雨 D: 焼き尽くすの手番の乱数 ＝ 全体1発の基準（雨では1つも引かない）", cr.N, baseRolls);
            }
            else Expect("火の雨 R: 焼き尽くすの手番の乱数 ＝ 基準 ＋ 雨 1 発に1つ（敵 3 体 × 10 発）", cr.N, baseRolls + FireBurstRule.RainDrops);
        }
        // 放つも乱数を引かない（薙ぎの主目標は前列の1体・的の選び方で引く分は通常の攻撃と同じ）: 放つ札の有無で同じ一振りの乱数が同じ
        {
            var cnt = new Dictionary<bool, int>();
            foreach (bool un in new[] { false, true })
            {
                var borg = Plain("borg", atk: 5, pat: AttackPattern.Sweep, tr: un ? new[] { TraitId.FireLevel, TraitId.FireUnleash } : new[] { TraitId.FireLevel });
                var ctx = Ctx(Formation.Build(front1: borg), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out _);
                var b = U(p, "borg"); SetLv(b, 4);
                var cr = new CountingRandom(7);
                RngF.SetValue(ctx, cr);
                giftF.SetValue(ctx, b);
                ctx.TakeTurn(b);
                cnt[un] = cr.N;
            }
            Expect("放つ: 放つ札の有無で、ギフトの手番の乱数の数が同じ", $"{cnt[true]}", $"{cnt[false]}");
        }
        // S1 は乱数を引かない: 上限あり／なしの同じ一振りで乱数の数が同じ
        {
            var cnt = new Dictionary<bool, int>();
            foreach (bool cap in new[] { false, true })
            {
                var borg = Plain("borg", atk: 5, pat: AttackPattern.Sweep, tr: cap ? new[] { TraitId.FireLevel, TraitId.FireSpreadCap } : new[] { TraitId.FireLevel });
                var ctx = Ctx(Formation.Build(front1: borg, front3: cap ? HiyoD(TraitId.StokeBaseAtk) : HiyoD()), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e);
                foreach (var u in p) SetLv(u, 1);
                foreach (var x in e) x.SetCounter(StatusKeys.Burn, 3);
                var cr = new CountingRandom(7);
                RngF.SetValue(ctx, cr);
                ctx.PerformAttack(U(p, "borg"));
                SetLv(U(p, "hiyo"), 1); SetLv(U(p, "borg"), 1);   // 育ちの差でヒヨがギフトを撃つかどうかが変わらないよう、ヒヨの手番の前に揃える（煽りの手番）
                ctx.TakeTurn(U(p, "hiyo"));
                cnt[cap] = cr.N;
            }
            Expect("S1: 上限・倍率の前 の有無で、同じ一振り＋ヒヨの煽りの手番の乱数の数が同じ", $"{cnt[true]}", $"{cnt[false]}");
        }

        // ---- 盤面の戦（受け入れ 3〜5） ----
        Console.WriteLine();
        Console.WriteLine("### 盤面の戦（T3-238 と 雷＋ボルグ × 版 S1〜S2R-g4 × 波6 × 倍率3 × seed 0..39）");
        Console.WriteLine();
        long battles = 0, snapBad = 0, capBad = 0, capN = 0, moveBad = 0, moveN = 0, spentBad = 0, embersBad = 0, embersN = 0, verbDiff = 0, detDiff = 0;
        long causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, rainBad = 0, rainN = 0;
        var moveKinds = new long[4];
        var lk = new object();
        foreach (var v in Versions.Skip(1))
            foreach (var bf in new[] { T3238, ThunderBorg })
            {
                var f = Apply(bf, v);
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                        Parallel.For(0, 40, seed =>
                        {
                            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var en = BA.WaveOf(w, BA.Scales[s].Sc)();
                            var slotOf = pl.Concat(en).ToDictionary(u => u, u => u.Slot);
                            var r = BattleEngine.Run(pl, en, seed, verbose: true);
                            var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BA.WaveOf(w, BA.Scales[s].Sc)(), seed, verbose: false);
                            var r3 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BA.WaveOf(w, BA.Scales[s].Sc)(), seed, verbose: true);
                            var agg = new BA.Agg(); agg.Take(r, pl, en, slot0);
                            var st = Audit(r, pl, v);
                            lock (lk)
                            {
                                battles++;
                                snapBad += st.SnapBad; capBad += st.CapBad; capN += st.CapN; moveBad += st.MoveBad; moveN += st.MoveN; spentBad += st.SpentBad;
                                embersBad += st.EmbersBad; embersN += st.EmbersN; rainBad += st.RainBad; rainN += st.RainN;
                                foreach (var m in r.FireLevels!.Moves) moveKinds[m.Kind]++;
                                if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns || r.PlayerSurvivors != r2.PlayerSurvivors
                                    || r.FireLevels!.Gifts != r2.FireLevels!.Gifts || r.FireLevels.Moves.Count != r2.FireLevels.Moves.Count || r.FireLevels.RainDrops != r2.FireLevels.RainDrops) verbDiff++;
                                if (r.Events.Count != r3.Events.Count || r.Log.Count != r3.Log.Count || r.Turns != r3.Turns) detDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                            }
                        });
            }
        Console.WriteLine($"（大技: 放つ {moveKinds[1]} ／ 焼き尽くす {moveKinds[2]} ／ 残り火 {moveKinds[3]}）");
        Console.WriteLine();
        Expect($"写し: 燃えている駒の火勢は 1〜4・燃えていない駒は 0（{battles} 戦）", snapBad, 0L);
        Expect($"② 燃え広がり・ヒヨ自身の育ちは1回で +1 まで（{capN} 件）", capBad, 0L);
        Expect($"① 放つ・焼き尽くすはギフトの手番で火勢4 のときだけ（{moveN} 回）", moveBad, 0L);
        Expect("① 撃った直後に 1（見出しの次の火勢の出来事が「撃った」→ 1）", spentBad, 0L);
        Expect($"残り火は焼き尽くすの後の、ギフトでない次の手番だけ（{embersN} 回）", embersBad, 0L);
        Console.WriteLine($"（火の雨 {rainN} 発。D の落ち先は上の単体の検査で見る）");
        Expect("verbose の有無で勝敗・決着T・生存・ギフト・大技・雨の数が同じ", verbDiff, 0L);
        Expect("同じ seed を2度回して台本の長さが同じ", detDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 4）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);

        // ---- `compare`: S0 が docs/balance.md と一致・3枚のいない行は 0 セル ----
        var rates = CompareRates();
        var rows = CompareBuilds();
        var bal = File.ReadAllLines("docs/balance.md", System.Text.Encoding.UTF8).Where(l => l.StartsWith("| ") && l.Contains('%')).ToList();
        int cellBad = 0, cells = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            var line = bal.FirstOrDefault(l => l.StartsWith("| " + rows[i].Name + " |"));
            if (line is null) { cellBad += 5; continue; }
            var c = line.Split('|').Select(x => x.Trim()).Where(x => x.EndsWith('%')).ToArray();
            for (int stg = 0; stg < 5; stg++) { cells++; if (Math.Abs(double.Parse(c[stg].TrimEnd('%')) - rates[0, i, stg]) > 0.01) cellBad++; }
        }
        Expect($"S0 の `compare` が docs/balance.md と一致（{cells} セル）", cellBad, 0);
        bool Has(Formation f) => f.Occupied().Any(o => o.Def.Id is "borg" or "hota" or "hiyo");
        int noMoved = 0;
        for (int v = 1; v < Versions.Length; v++)
            for (int i = 0; i < rows.Length; i++)
                if (!Has(rows[i].F)) for (int stg = 0; stg < 5; stg++) if (rates[v, i, stg] != rates[0, i, stg]) noMoved++;
        Expect("ボルグ・ホタ・ヒヨのいない行は どの版でも 0 セル", noMoved, 0);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    readonly record struct AuditStat(long SnapBad, long CapBad, long CapN, long MoveBad, long MoveN, long SpentBad, long EmbersBad, long EmbersN, long RainBad, long RainN);

    /// <summary>1戦の台本と帳簿を突き合わせる。</summary>
    static AuditStat Audit(BattleResult r, List<UnitState> p, Ver v)
    {
        long snapBad = 0, capBad = 0, capN = 0, moveBad = 0, moveN = 0, spentBad = 0, embersBad = 0, embersN = 0, rainN = 0;
        foreach (var s in r.FireLevels!.Snaps)
            if (s.Burning ? s.Level is < 1 or > 4 : s.Level != 0) snapBad++;
        var ev = r.Events;
        bool cap = v.Borg.Traits.Contains(TraitId.FireSpreadCap);
        // 手番の枠: ギフトの手番は、枠の直前の出来事が「ギフトの手番」の見出し
        // （見出しの直後に「火を受け取って動く」の見せ場 `Highlight` が1件挟まる）
        bool IsGiftHand(HandRecord h)
        {
            for (int j = h.EventStart - 1; j >= 0 && j >= h.EventStart - 3; j--)
            {
                var g = ev[j];
                if (g.Kind == BattleEventKind.Highlight) continue;
                return g is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.GiftTurn } && g.TargetId == h.ActorId;
            }
            return false;
        }
        var hands = r.Hands.OrderBy(h => h.EventStart).ToList();
        HandRecord? HandAt(int actor, int i) => hands.Where(h => h.ActorId == actor && h.EventStart <= i && i < h.EventEnd).Select(h => (HandRecord?)h).LastOrDefault();
        int? pendingSpent = null;
        for (int i = 0; i < ev.Count; i++)
        {
            var x = ev[i];
            if (x.Kind != BattleEventKind.FireLevel || x.TargetId is not int t) continue;
            if (pendingSpent == t && x.Text is not (FireLevelLabels.Unleash or FireLevelLabels.Burnout))
            {
                if (x.Text != FireLevelLabels.Spent || x.Amount != 1) spentBad++;
                pendingSpent = null;
            }
            switch (x.Text)
            {
                case FireLevelLabels.GrowSpread: case FireLevelLabels.GrowSelf:
                    if (cap && (x.Text == FireLevelLabels.GrowSpread || x.ActorId != x.TargetId)) { capN++; if (x.Amount - x.Slot > 1) capBad++; }
                    break;
                case FireLevelLabels.Unleash: case FireLevelLabels.Burnout:
                {
                    moveN++;
                    var h = HandAt(t, i);
                    if (x.Amount != 4 || h is null || !IsGiftHand(h.Value)) moveBad++;
                    pendingSpent = t;
                    if (x.Text == FireLevelLabels.Burnout)
                    {
                        // 次の「ギフトでない」手番: 攻撃したなら残り火がその中に1つ、動けなかったなら無い
                        var nx = hands.Where(y => y.ActorId == t && y.EventStart > i && !IsGiftHand(y)).Select(y => (HandRecord?)y).FirstOrDefault();
                        if (nx is HandRecord next && p.First(u => u.InstanceId == t).Def.Traits.Contains(TraitId.PyreEmbers))
                        {
                            int inNext = 0;
                            for (int j = next.EventStart; j < next.EventEnd && j < ev.Count; j++)
                                if (ev[j] is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.Embers, Slot: 1 } em && em.TargetId == t) inNext++;
                            if (next.Outcome == TurnOutcome.Attack ? inNext != 1 : inNext != 0) embersBad++;
                        }
                    }
                    break;
                }
                case FireLevelLabels.Embers:
                {
                    if (x.Slot != 1) break;
                    embersN++;
                    var h = HandAt(t, i);
                    // 残り火の手番はギフトでなく、その前に同じ駒の焼き尽くすがある
                    int lastBo = -1;
                    for (int j = i - 1; j >= 0; j--) if (ev[j] is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.Burnout } b && b.TargetId == t) { lastBo = j; break; }
                    if (h is not HandRecord hh || IsGiftHand(hh) || lastBo < 0) embersBad++;
                    else if (hands.Any(y => y.ActorId == t && y.EventStart > lastBo && y.EventStart < hh.EventStart && !IsGiftHand(y))) embersBad++;   // 間に別の自分の手番
                    break;
                }
                case FireLevelLabels.Rain: rainN++; break;
            }
        }
        return new AuditStat(snapBad, capBad, capN, moveBad, moveN, spentBad, embersBad, embersN, 0, rainN);
    }
}
