using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// enemyfire check —— 自己検査（受け入れ 2〜5）。E0 の台本の突き合わせは `enemyfire digest` を実装の前のコミットと cmp で見る。
static partial class EnemyFireDiag
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
    static void SetLv(UnitState u, int lv, int burn = 3) { u.SetCounter(StatusKeys.Burn, burn); u.SetCounter(FireLevelRule.LvKey, lv); }
    static List<BattleEvent> Since(BattleContext ctx, int n0) => ctx.Events.Skip(n0).ToList();
    static int FL(List<BattleEvent> ev, string label) => ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == label);
    static readonly TraitId[] E1Cards = { TraitId.FireLevel, TraitId.FoeFireLevel, TraitId.FoeFireTick, TraitId.FoeFireBrittle, TraitId.FoeFireSpread };
    static UnitDef Borg(params TraitId[] tr) => Plain("borg", atk: 5, pat: AttackPattern.Sweep, tr: tr);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第245期 enemyfire check");
        Console.WriteLine();

        // ---- 前段: 規定の駒が P′ の版と同じ（札の並びまで）----
        Expect("前段: 規定のボルグ ＝ 第244期 R3 ＋ 上限・放つ", string.Join(",", UnitCatalog.BorgE0.Traits), string.Join(",", UnitCatalog.BorgR3.Traits.Concat(new[] { TraitId.FireSpreadCap, TraitId.FireUnleash })));
        Expect("前段: 規定のホタ ＝ R3 ＋ 焼き尽くす・残り火・呼び火", string.Join(",", UnitCatalog.Hota.Traits), string.Join(",", UnitCatalog.HotaR3.Traits.Concat(new[] { TraitId.PyreBurnout, TraitId.PyreEmbers, TraitId.CallFire })));
        Expect("前段: 規定のヒヨ ＝ R3 ＋ P′ ＋ B", string.Join(",", UnitCatalog.Hiyo.Traits), string.Join(",", UnitCatalog.HiyoR3.Traits.Concat(new[] { TraitId.StokeStageAtk, TraitId.GiftQuiet })));
        // ③′: 段3 のホタ（5 × 8 ＝ 40）はボルグ（18）より先、燃えていないホタは ×1
        {
            var hiyo = new UnitDef { Id = "hiyo", Name = "hiyo", MaxHp = 1000, Attack = 5, Speed = 1, Actions = new UnitAction[] { new(ActionKind.Skill, Label: "火を煽る／火を渡す") },
                Traits = new[] { TraitId.FireStoke, TraitId.TurnGift, TraitId.StokeStageAtk } };
            var hota = new UnitDef { Id = "hota", Name = "hota", MaxHp = 1000, Attack = 5, Speed = 1, Traits = new[] { TraitId.Pyre, TraitId.PyreStage, TraitId.FireLevel, TraitId.PyreEmbers } };
            var ctx = Ctx(Formation.Build(front1: hiyo, front3: Plain("borg", atk: 18, tr: TraitId.FireLevel), center: hota), Formation.Build(front1: Plain("e1")), out var p, out _);
            var hy = U(p, "hiyo"); var bo = U(p, "borg"); var ho = U(p, "hota");
            SetLv(bo, 1); SetLv(ho, 3);
            int m3 = FireStokeTrait.StageMultiplier(ho);
            SetLv(ho, 1); int m1 = FireStokeTrait.StageMultiplier(ho);
            ho.SetCounter(FireBurstRule.EmbersKey, 1); int mE = FireStokeTrait.StageMultiplier(ho); ho.SetCounter(FireBurstRule.EmbersKey, 0);
            ho.SetCounter(StatusKeys.Burn, 0); int m0 = FireStokeTrait.StageMultiplier(ho);
            Expect("③′ 倍率: 段3 のホタ ×8 ／ 段1 ×4 ／ 残り火 ×2 ／ 燃えていない ×1 ／ ボルグ ×1", $"{m3}/{m1}/{mE}/{m0}/{FireStokeTrait.StageMultiplier(bo)}", "8/4/2/1/1");
            SetLv(ho, 1); SetLv(bo, 1); SetLv(hy, 1);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            var st = Since(ctx, n0).First(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Stoke);
            Expect("③′ 煽り: 段1 のホタ（5×4 ＝ 20）＞ ボルグ（18）", st.TargetId == ho.InstanceId ? "hota" : "borg", "hota");
        }

        // ---- 育つ: 燃えている味方の攻撃・当たる前から燃えていた敵・同じ敵は1回まで ----
        {
            var ctx = Ctx(Formation.Build(front1: Borg(E1Cards)), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e);
            var b = U(p, "borg"); SetLv(b, 1);
            SetLv(U(e, "e1"), 1); SetLv(U(e, "e3"), 2);   // ec は燃えていない
            ctx.PerformAttack(b);   // 薙ぎ: 前列の主目標 ＋ 同じ列 ＋ 中列
            var lv = e.Select(x => FireLevelRule.Of(x)).ToArray();
            Expect("育つ: 燃えていた e1・e3 が +1、燃えていなかった ec は 0 のまま（薙ぎの当たった敵）", $"{lv[0]}/{lv[1]}/{lv[2]}", "2/3/0");
            b.SetCounter(StatusKeys.Burn, 0);
            ctx.PerformAttack(b);
            Expect("育つ: 殴った味方が燃えていなければ育たない", $"{FireLevelRule.Of(U(e, "e1"))}/{FireLevelRule.Of(U(e, "e3"))}", "2/3");
        }
        {
            var hota = new UnitDef { Id = "hota", Name = "hota", MaxHp = 1000, Attack = 5, Speed = 1, Traits = new[] { TraitId.Pyre, TraitId.PyreStage, TraitId.FireLevel, TraitId.FoeFireLevel } };
            var ctx = Ctx(Formation.Build(front1: hota), Formation.Build(front1: Plain("e1", hp: 5000)), out var p, out var e);
            var h = U(p, "hota"); SetLv(h, 3); SetLv(U(e, "e1"), 1);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(h);   // 5連撃・同じ敵
            var ev = Since(ctx, n0);
            Expect("育つ: 5連撃（1回の手番）で同じ敵は +1 まで", $"{ev.Count(x => x.Kind == BattleEventKind.Attack)}/{FireLevelRule.Of(U(e, "e1"))}", "5/2");
        }
        // ---- 萎む ----
        {
            var ctx = Ctx(Formation.Build(front1: Borg(E1Cards)), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e);
            var b = U(p, "borg"); SetLv(b, 1);
            SetLv(U(e, "e1"), 2); SetLv(U(e, "e3"), 3); SetLv(U(e, "ec"), 1);
            U(e, "ec").SetCounter(FireLevelRule.GrewKey, 0);
            ctx.PerformAttack(b);   // e1 → 3、e3 → 4（育った印）、ec は燃えていて当たるので 2
            U(e, "ec").SetCounter(FireLevelRule.GrewKey, 0); U(e, "ec").SetCounter(FireLevelRule.LvKey, 3);   // ec だけ「育たなかった 3」に戻す
            ctx.WiltFire();
            Expect("萎む: 育った敵はそのまま（e1 3 ／ e3 4）、育たなかった 3 は 2", $"{FireLevelRule.Of(U(e, "e1"))}/{FireLevelRule.Of(U(e, "e3"))}/{FireLevelRule.Of(U(e, "ec"))}", "3/4/2");
            SetLv(U(e, "e1"), 1); U(e, "e1").SetCounter(FireLevelRule.GrewKey, 0);
            ctx.WiltFire();
            Expect("萎む: 燃えている間は 1 未満にしない", FireLevelRule.Of(U(e, "e1")), 1);
        }
        // ---- 保つ火・消える ----
        {
            var ctx = Ctx(Formation.Build(front1: Borg(E1Cards)), Formation.Build(front1: Plain("e1")), out var p, out var e);
            var x = U(e, "e1");
            ctx.Ignite(x);
            Expect("保つ火: 燃えていなかった敵に点くと 1", x.RawCounter(FireLevelRule.LvKey), 1);
            x.SetCounter(FireLevelRule.LvKey, 3);
            ctx.Ignite(x);
            Expect("保つ火: 点け直しでは上げない（3 のまま）", FireLevelRule.Of(x), 3);
            x.SetCounter(StatusKeys.Burn, 1);
            ctx.TickStatuses();
            Expect("消える: 燃焼が切れたら 0", x.RawCounter(FireLevelRule.LvKey), 0);
        }

        // ---- 刻みの回数・脆さ ----
        foreach (var (name, cards, wantTicks, wantEach) in new[]
                 {
                     ("E0（札なし）", new[] { TraitId.FireLevel }, 1, 8),
                     ("E-刻み", new[] { TraitId.FireLevel, TraitId.FoeFireLevel, TraitId.FoeFireTick }, 3, 8),
                     ("E-脆さ", new[] { TraitId.FireLevel, TraitId.FoeFireLevel, TraitId.FoeFireBrittle }, 1, 10),
                     ("E1", E1Cards, 3, 10),
                 })
        {
            var ctx = Ctx(Formation.Build(front1: Borg(cards)), Formation.Build(front1: Plain("e1")), out _, out var e);
            var x = U(e, "e1"); SetLv(x, 3);
            int n0 = ctx.Events.Count, hp0 = x.Hp;
            ctx.TickStatuses();
            var ev = Since(ctx, n0);
            var dmg = ev.Where(y => y.Kind == BattleEventKind.Damage && y.TargetId == x.InstanceId).Select(y => y.Amount).ToList();
            Expect($"刻み {name}: 火勢3 の敵の燃焼の刻みは {wantTicks} 回・1回 6 ＋ 脆さ（25% ＝ 2 ／ 55% ＝ 4）", $"{dmg.Count}/{string.Join(",", dmg.Distinct())}/{hp0 - x.Hp}", $"{wantTicks}/{wantEach}/{wantTicks * wantEach}");
        }
        {
            var ctx = Ctx(Formation.Build(front1: Borg(E1Cards)), Formation.Build(front1: Plain("e1")), out _, out var e);
            var x = U(e, "e1"); SetLv(x, 4, burn: 1);   // 最後の刻み: 残りターンを 0 にしてから刻む
            int hp0 = x.Hp;
            ctx.TickStatuses();
            Expect("最後の刻み（残り 1）も減らす前の火勢で数える（4 回 × (6 ＋ 70% ＝ 5)）・その後 0", $"{hp0 - x.Hp}/{x.RawCounter(FireLevelRule.LvKey)}", $"{4 * 11}/0");
        }
        foreach (int lv in new[] { 1, 2, 3, 4 })
        {
            var ctx = Ctx(Formation.Build(front1: Plain("atk", atk: 100), center: Borg(E1Cards)), Formation.Build(front1: Plain("e1", hp: 5000)), out var p, out var e);
            var x = U(e, "e1"); SetLv(x, lv);
            int hp0 = x.Hp;
            ctx.ApplyDamage(x, 100, U(p, "atk"));
            Expect($"脆さ: 火勢{lv} の敵への一撃 100 は {100 + FoeFireRule.BrittlePercent[lv]}", hp0 - x.Hp, 100 + FoeFireRule.BrittlePercent[lv]);
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("atk", atk: 100), center: Borg(TraitId.FireLevel, TraitId.FoeFireLevel)), Formation.Build(front1: Plain("e1", hp: 5000)), out var p, out var e);
            var x = U(e, "e1"); SetLv(x, 4);
            int hp0 = x.Hp;
            ctx.ApplyDamage(x, 100, U(p, "atk"));
            Expect("脆さ: 上昇の札が無ければ火勢4 でも 25%", hp0 - x.Hp, 125);
        }

        // ---- 延焼 ----
        {
            // X 字: 前1 の隣は 中央・後1・○中1・○前2（前3 は隣ではない）
            var ctx = Ctx(Formation.Build(front1: Plain("atk", atk: 100), center: Borg(E1Cards)),
                Formation.Build(front1: Plain("e1", hp: 50), front3: Plain("e3"), center: Plain("ec"), back1: Plain("b1")), out var p, out var e);
            var x = U(e, "e1"); SetLv(x, 4);
            SetLv(U(e, "ec"), 2);   // 燃えている隣
            int n0 = ctx.Events.Count;
            ctx.ApplyDamage(x, 999, U(p, "atk"));
            var ev = Since(ctx, n0);
            Expect("延焼: 火勢4 で倒れた → 燃えていた隣（中央）+1 ／ 燃えていなかった隣（後1）は火勢2 ／ 隣でない前3 は 0",
                $"{FireLevelRule.Of(U(e, "ec"))}/{FireLevelRule.Of(U(e, "b1"))}/{FireLevelRule.Of(U(e, "e3"))}", "3/2/0");
            Expect("延焼の見出し（隣の数だけ）", FL(ev, FireLevelLabels.FoeSpread), 2);
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("atk", atk: 100), center: Borg(E1Cards)),
                Formation.Build(front1: Plain("e1", hp: 50), center: Plain("ec")), out var p, out var e);
            var x = U(e, "e1"); SetLv(x, 3);
            ctx.ApplyDamage(x, 999, U(p, "atk"));
            Expect("延焼: 火勢3 で倒れても移らない", FireLevelRule.Of(U(e, "ec")), 0);
        }
        {
            var ctx = Ctx(Formation.Build(center: Borg(E1Cards)), Formation.Build(front1: Plain("e1", hp: 20), center: Plain("ec")), out _, out var e);
            var x = U(e, "e1"); SetLv(x, 4, burn: 1);
            ctx.TickStatuses();   // 最後の刻みで倒れる（燃焼は 0 になってから刻む）
            Expect("延焼: 最後の刻みで倒れても減らす前の火勢 4 で移る", $"{x.IsAlive}/{FireLevelRule.Of(U(e, "ec"))}", "False/2");
        }

        // ---- E2: 味方の刻みも回数（熾火・火の変換・火の癒しは1回ごと）----
        {
            var ally = Plain("ally", hp: 1000);
            var hota = new UnitDef { Id = "hota", Name = "hota", MaxHp = 1000, Attack = 5, Speed = 1, Traits = new[] { TraitId.Pyre } };
            var mend = Plain("mend", hp: 1000, tr: TraitId.FireMend);
            foreach (bool e2 in new[] { false, true })
            {
                var cards = e2 ? E1Cards.Append(TraitId.AllyFireTick).ToArray() : E1Cards;
                var ctx = Ctx(Formation.Build(front1: ally, front3: hota, center: Borg(cards), back1: mend), Formation.Build(front1: Plain("e1")), out var p, out _);
                var a = U(p, "ally"); var h = U(p, "hota"); var m = U(p, "mend");
                SetLv(a, 3); SetLv(h, 3); SetLv(m, 3); m.Hp = 500;
                int a0 = a.Hp, h0 = h.Hp, m0 = m.Hp;
                ctx.TickStatuses();
                Expect($"味方の刻み {(e2 ? "E2" : "E1")}: 火勢3 の味方 ／ 熾火 ／ 火の癒し（HP の差）", $"{a.Hp - a0}/{h.Hp - h0}/{m.Hp - m0}", e2 ? "-18/0/18" : "-6/0/6");
            }
            // 火の変換（ヒヨ）: 1回ごとに回復
            var hiyo = new UnitDef { Id = "hiyo", Name = "hiyo", MaxHp = 1000, Attack = 5, Speed = 1, Traits = new[] { TraitId.FireConvert } };
            {
                var ctx = Ctx(Formation.Build(front1: ally, front3: hiyo, center: Borg(E1Cards.Append(TraitId.AllyFireTick).ToArray())), Formation.Build(front1: Plain("e1")), out var p, out _);
                var a = U(p, "ally"); SetLv(a, 4); a.Hp = 500;
                int n0 = ctx.Events.Count;
                ctx.TickStatuses();
                Expect("味方の刻み E2 ＋ 火の変換: 火勢4 の味方は 4 回とも回復（+24）", a.Hp - 500, 24);
            }
        }

        // ---- 追記 A: 放つで敵の火を育てる ----
        foreach (bool stoke in new[] { false, true })
        {
            var hiyo = new UnitDef { Id = "hiyo", Name = "hiyo", MaxHp = 1000, Attack = 5, Speed = 1, Actions = new UnitAction[] { new(ActionKind.Skill, Label: "火を煽る／火を渡す") },
                Traits = new[] { TraitId.FireStoke, TraitId.TurnGift } };
            var cards = E1Cards.Append(TraitId.FireUnleash);
            if (stoke) cards = cards.Append(TraitId.UnleashStoke);
            var ctx = Ctx(Formation.Build(front1: hiyo, front3: Borg(cards.ToArray())), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e);
            var hy = U(p, "hiyo"); var bo = U(p, "borg");
            SetLv(hy, 3); SetLv(bo, 4);
            SetLv(U(e, "e1"), 1); SetLv(U(e, "e3"), 2);   // ec は燃えていない
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);   // 火勢3 のギフト: 相手はボルグ → 放つ（薙ぎ ×3・当てた敵に火）
            var ev = Since(ctx, n0);
            var lv = e.Select(x => FireLevelRule.Of(x)).ToArray();
            Expect(stoke ? "追記 A: 放つで当たった敵は、燃えていれば +2（1 → 3 ／ 2 → 4）、燃えていなければ着火して 2" : "追記 A の対照（E1）: 放つで当たった敵は、燃えていれば +1、燃えていなければ着火して 1",
                $"{FL(ev, FireLevelLabels.Unleash)}/{lv[0]}/{lv[1]}/{lv[2]}", stoke ? "1/3/4/2" : "1/2/3/1");
        }

        // ---- 追記 B: ギフトの手番の燃え広がりでヒヨは育たない ----
        foreach (bool quiet in new[] { false, true })
        {
            var hiyo = new UnitDef { Id = "hiyo", Name = "hiyo", MaxHp = 1000, Attack = 5, Speed = 1, Actions = new UnitAction[] { new(ActionKind.Skill, Label: "火を煽る／火を渡す") },
                Traits = quiet ? new[] { TraitId.FireStoke, TraitId.TurnGift, TraitId.GiftQuiet } : new[] { TraitId.FireStoke, TraitId.TurnGift } };
            var ctx = Ctx(Formation.Build(front1: hiyo, front3: Borg(TraitId.FireLevel)), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e);
            var hy = U(p, "hiyo"); var bo = U(p, "borg");
            SetLv(hy, 3); SetLv(bo, 2);
            foreach (var x in e) SetLv(x, 1);
            ctx.TakeTurn(hy);   // ギフト（ヒヨ 3 → 1）→ ボルグのギフトの手番（燃えていた敵 3 体に薙ぎ → ボルグ +3 で 4）
            Expect(quiet ? "追記 B: ギフトの手番の燃え広がりでヒヨは育たない（撃って 1 のまま）・ボルグは育つ" : "追記 B の対照: ギフトの手番の燃え広がりでヒヨも育つ",
                $"{FireLevelRule.Of(hy)}/{FireLevelRule.Of(bo)}", quiet ? "1/4" : "4/4");
            SetLv(hy, 1); SetLv(bo, 1);
            foreach (var x in e) if (x.IsAlive) SetLv(x, 1);
            ctx.TakeTurn(bo);   // 通常の手番
            Expect(quiet ? "追記 B: 通常の手番の燃え広がりでは、B があってもヒヨは育つ" : "追記 B の対照: 通常の手番の燃え広がりでヒヨは育つ", FireLevelRule.Of(hy) > 1, true);
        }

        // ---- 乱数を引かない ----
        {
            var cnt = new Dictionary<bool, int>();
            foreach (bool on in new[] { false, true })
            {
                var cards = on ? E1Cards.Append(TraitId.AllyFireTick).Append(TraitId.UnleashStoke).ToArray() : new[] { TraitId.FireLevel };
                var ctx = Ctx(Formation.Build(front1: Borg(cards), front3: Plain("ally")),
                    Formation.Build(front1: Plain("e1", hp: 40), front3: Plain("e3"), center: Plain("ec"), back1: Plain("b1")), out var p, out var e);
                SetLv(U(p, "borg"), 1); SetLv(U(p, "ally"), 3);
                foreach (var x in e) SetLv(x, 4);
                var cr = new CountingRandom(7);
                RngF.SetValue(ctx, cr);
                ctx.PerformAttack(U(p, "borg"));
                ctx.TickStatuses();
                ctx.WiltFire();
                cnt[on] = cr.N;
            }
            Expect("乱数: 敵の火勢の札（育つ・刻み・脆さ・延焼・味方の刻み）の有無で、同じ一振り＋刻み＋萎むの乱数の数が同じ", $"{cnt[true]}", $"{cnt[false]}");
        }

        // ---- 盤面の戦（受け入れ 3〜5） ----
        Console.WriteLine();
        Console.WriteLine("### 盤面の戦（T3-244 と 雷＋ボルグ × 版 E-刻み〜E2 × 波6 × 倍率3 × seed 0..39）");
        Console.WriteLine();
        long battles = 0, snapBad = 0, growBad = 0, growN = 0, spreadBad = 0, spreadN = 0, tickBad = 0, tickN = 0, verbDiff = 0, detDiff = 0;
        long causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0;
        var lk = new object();
        foreach (var v in Versions.Skip(1))
            foreach (var bf in new[] { T3244, ThunderBorg })
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
                            var st = Audit(r, pl, en, v, slot0);
                            lock (lk)
                            {
                                battles++;
                                snapBad += st.SnapBad; growBad += st.GrowBad; growN += st.GrowN; spreadBad += st.SpreadBad; spreadN += st.SpreadN; tickBad += st.TickBad; tickN += st.TickN;
                                if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns || r.PlayerSurvivors != r2.PlayerSurvivors
                                    || r.FireLevels!.FoeGrowth != r2.FireLevels!.FoeGrowth || r.FireLevels.FoeSpreads != r2.FireLevels.FoeSpreads) verbDiff++;
                                if (r.Events.Count != r3.Events.Count || r.Log.Count != r3.Log.Count || r.Turns != r3.Turns) detDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                            }
                        });
            }
        Expect($"写し: 燃えている敵の火勢は 1〜4・燃えていない敵は 0（{battles} 戦）", snapBad, 0L);
        Expect($"育つ・敵（燃え広がり）: 相手は燃えていた敵・1回で +1・同じ手番の同じ殴り手から同じ敵へは1回（{growN} 件）", growBad, 0L);
        Expect($"延焼: 見出しの直前に同じ敵が倒れている・隣・燃えていなければ 2（{spreadN} 件）", spreadBad, 0L);
        Expect($"刻み: 回数の札（全部で何回）と本数が一致・少ないのはその周回に倒れた駒だけ（{tickN} 回の刻み）", tickBad, 0L);
        Expect("verbose の有無で勝敗・決着T・生存・敵の育ち・延焼の数が同じ", verbDiff, 0L);
        Expect("同じ seed を2度回して台本の長さが同じ", detDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 4）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);

        // ---- `compare`: E0 が docs/balance.md と一致・3枚のいない行は 0 セル ----
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
        Expect($"E0 の `compare` が docs/balance.md と一致（{cells} セル）", cellBad, 0);
        int noMoved = 0;
        for (int v = 1; v < Versions.Length; v++)
            for (int i = 0; i < rows.Length; i++)
                if (!rows[i].F.Occupied().Any(o => o.Def.Id == "borg")) for (int stg = 0; stg < 5; stg++) if (rates[v, i, stg] != rates[0, i, stg]) noMoved++;
        Expect("ボルグ（札の持ち主）のいない行は どの版でも 0 セル", noMoved, 0);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    readonly record struct AuditStat(long SnapBad, long GrowBad, long GrowN, long SpreadBad, long SpreadN, long TickBad, long TickN);

    /// <summary>1戦の台本と帳簿を突き合わせる。</summary>
    static AuditStat Audit(BattleResult r, List<UnitState> p, List<UnitState> e, FB.Ver v, Dictionary<int, int> slot0)
    {
        long snapBad = 0, growBad = 0, growN = 0, spreadBad = 0, spreadN = 0, tickBad = 0, tickN = 0;
        foreach (var s in r.FireLevels!.Snaps)
            if (s.Burning ? s.Level is < 1 or > 4 : s.Level != 0) snapBad++;
        var ev = r.Events;
        var foe = new HashSet<int>(e.Select(u => u.InstanceId));
        foreach (var x in ev) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && (x.Team ?? 1) == BattleContext.EnemyTeam) foe.Add(sid);
        var slot = slot0;
        // 手番（枠）ごとの (殴り手, 敵) の育ち
        var hands = r.Hands.OrderBy(h => h.EventStart).ToList();
        int HandIx(int i) { for (int k = hands.Count - 1; k >= 0; k--) if (hands[k].EventStart <= i && i < hands[k].EventEnd) return k; return -1; }
        var seen = new HashSet<(int H, int A, int T)>();
        // 刻み: 同じ駒・同じターンの「燃焼」の Status の数と TickCount
        var tickRuns = new Dictionary<(int T, int Id), (int N, int? Count)>();
        var diedAt = new HashSet<(int, int)>();
        var unleashing = new HashSet<int>();
        for (int i = 0; i < ev.Count; i++)
        {
            var x = ev[i];
            // 追記 A: 放つの見出しの後、同じ駒の燃え広がりの見出しが出るまでの「育つ・敵」は放つの育ち（燃え広がりとは別に数える）
            if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Unleash && x.ActorId is int ua) unleashing.Add(ua);
            if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Spread && x.ActorId is int sa) unleashing.Remove(sa);
            if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.GrowFoe && x.TargetId is int t && foe.Contains(t))
            {
                growN++;
                bool fromSpread = x.ActorId is int a0 && foe.Contains(a0);
                if (!fromSpread && x.ActorId is int au && unleashing.Contains(au)) { if (x.Amount - x.Slot != 1 && !(x.Slot == 1 && x.Amount == 2)) growBad++; continue; }
                if (!fromSpread)
                {
                    if (x.Amount - x.Slot != 1 && !(x.Amount == 4 && x.Slot == 4)) growBad++;
                    int h = HandIx(i);
                    if (!seen.Add((h, x.ActorId ?? -1, t))) growBad++;
                    // 直前に同じ殴り手 → 同じ敵の「燃え広がり」の見出しがある
                    bool head = false;
                    for (int j = i - 1; j >= 0 && j >= i - 40; j--)
                        if (ev[j] is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.Spread } sp && sp.ActorId == x.ActorId && sp.TargetId == t) { head = true; break; }
                    if (!head) growBad++;
                }
            }
            if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.FoeSpread && x.ActorId is int dead && x.TargetId is int nb)
            {
                spreadN++;
                bool died = false;
                for (int j = i - 1; j >= 0 && j >= i - 60; j--) if (ev[j] is { Kind: BattleEventKind.Death } d && d.TargetId == dead) { died = true; break; }
                if (!died || !slot.TryGetValue(dead, out int ds) || !slot.TryGetValue(nb, out int ns) || !FormationRules.AreAdjacent(ds, ns)) { if (!died) spreadBad++; }
                if (x.Slot == 1)
                {
                    // 燃えていなかった隣: 続く「育つ・敵」で 2
                    bool two = false;
                    for (int j = i + 1; j < ev.Count && j <= i + 12; j++) if (ev[j] is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.GrowFoe } g && g.TargetId == nb) { two = g.Amount == FoeFireRule.SpreadLevel; break; }
                    if (!two) spreadBad++;
                }
            }
            if (x.Kind == BattleEventKind.Status && x.Text == "燃焼" && x.TargetId is int bid && foe.Contains(bid) && x.TickCount is int tc)
            {
                var k = (x.Turn, bid);
                var cur = tickRuns.GetValueOrDefault(k);
                tickRuns[k] = (cur.N + 1, tc);
            }
            if (x.Kind == BattleEventKind.Death && x.TargetId is int did) diedAt.Add((x.Turn, did));
        }
        // 刻みの回数: 周回の頭の写し（刻みの後）では減らす前の火勢が取れないので、TickCount（全部で何回）と実際の本数の一致だけを見る。
        // 何回目か／全部で何回かの札が付いた刻み（全部で2回以上）: 本数 ＝ 全部の回数。倒れたらそこで止めるので、その周回に倒れた駒だけ本数が少なくてよい。
        foreach (var ((turn, id), (n, count)) in tickRuns)
        {
            tickN++;
            if (count is not int c || n > c || (n < c && !diedAt.Contains((turn, id)))) tickBad++;
        }
        return new AuditStat(snapBad, growBad, growN, spreadBad, spreadN, tickBad, tickN);
    }

    internal static double[,,] CompareRates()
    {
        var rows = CompareBuilds();
        var w = new double[Versions.Length, rows.Length, 5];
        for (int v = 0; v < Versions.Length; v++)
            for (int i = 0; i < rows.Length; i++)
            {
                var f = Apply(rows[i].F, Versions[v]);
                for (int st = 0; st < 5; st++)
                {
                    var stage = EnemyCatalog.Stages[st].Enemy;
                    var res = new bool[BA.Seeds];
                    Parallel.For(0, BA.Seeds, seed => res[seed] = BattleEngine.Run(f, stage, seed, verbose: false).PlayerWon);
                    w[v, i, st] = 100.0 * res.Count(x => x) / BA.Seeds;
                }
            }
        return w;
    }
}
