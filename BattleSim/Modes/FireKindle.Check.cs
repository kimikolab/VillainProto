using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// firekindle check —— 自己検査（受け入れ 1〜3）。M0 の台本の突き合わせは `firekindle digest` を実装の前のコミットと cmp で見る。
static partial class FireKindleDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static readonly FieldInfo RngF = typeof(BattleContext).GetField("_rng", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly FieldInfo GiftF = typeof(BattleContext).GetField("_giftTurnActor", BindingFlags.Instance | BindingFlags.NonPublic)!;
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
        public override int Next(int minValue, int maxValue) { N++; return base.Next(minValue, maxValue); }
        public override double NextDouble() { N++; return base.NextDouble(); }
    }
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e, int turn = 1)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, turn);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 1000, int atk = 10, AttackPattern pat = AttackPattern.Single, params TraitId[] tr)
        => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = tr, Pattern = pat };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static void SetLv(UnitState u, int lv) { u.SetCounter(StatusKeys.Burn, 3); u.SetCounter(FireLevelRule.LvKey, lv); }
    static List<BattleEvent> Since(BattleContext ctx, int n0) => ctx.Events.Skip(n0).ToList();
    static int Lbl(List<BattleEvent> ev, string label) => ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == label);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第252期 firekindle check");
        Console.WriteLine();

        // ---- B1 守るほど燃え上がる: 火の鎧で切った量 30 ごとに +1・盾の配りの分も数える・燃えていない間は数えない ----
        foreach (var (borg, name, card) in new[] { (UnitCatalog.BorgM0, "M0", false), (BorgB1, "M-B1", true) })
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a1"), center: borg), Formation.Build(front1: Plain("e1")), out var p, out var e);
            var bo = U(p, "borg"); var a1 = U(p, "a1"); var e1 = U(e, "e1");
            SetLv(bo, 1);
            var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
            int n0 = ctx.Events.Count;
            ctx.ApplyDamage(bo, 20, e1);                 // 切った 10
            ctx.ApplyDamage(bo, 20, e1);                 // 20
            int lv2 = FireLevelRule.Of(bo);
            SetLv(a1, 1);
            ctx.ApplyDamage(a1, 24, e1);                 // 盾の配りで切った 12 → 32 → +1・残り 2
            int lv3 = FireLevelRule.Of(bo);
            var ev = Since(ctx, n0);
            Expect($"{name}: 鎧で 10・10 の後の火勢 ／ 盾で 12 の後の火勢 ／ 残り ／ 見出し ／ 乱数", $"{lv2}/{lv3}/{bo.RawCounter(FireKindleRule.GuardKey)}/{Lbl(ev, FireLevelLabels.KindleGuard)}/{cr.N}",
                card ? "1/2/2/1/0" : "1/1/0/0/0");
            Expect($"{name}: 帳簿（切った 鎧 ／ 盾・+1・上がった）", $"{ctx.FireBook.GuardSaved[0]}/{ctx.FireBook.GuardSaved[1]}/{ctx.FireBook.GuardSteps}/{ctx.FireBook.GuardRaised}", card ? "20/12/1/1" : "0/0/0/0");
            // 燃えていないボルグ（盾の配りは燃えている味方 a1 に掛かる）: 数えない
            bo.SetCounter(StatusKeys.Burn, 0);
            int acc0 = bo.RawCounter(FireKindleRule.GuardKey);
            for (int i = 0; i < 6; i++) ctx.ApplyDamage(a1, 24, e1);
            Expect($"{name}: 燃えていない間の盾の配りは数えない（残りが動かない ／ 燃えていない間の量）", $"{bo.RawCounter(FireKindleRule.GuardKey) - acc0}/{ctx.FireBook.GuardOff}", card ? "0/72" : "0/0");
        }
        // 1発で 60 以上切ったら +2（while）・火勢4 ならあぶれた火（溜め火）
        {
            var ctx = Ctx(Formation.Build(center: BorgM1), Formation.Build(front1: Plain("e1")), out var p, out var e);
            var bo = U(p, "borg"); var e1 = U(e, "e1"); bo.Hp = 5000;
            SetLv(bo, 3);
            ctx.ApplyDamage(bo, 130, e1);   // 切った 65 → +2（3 → 4・4 であぶれて溜め +1）・残り 5
            Expect("M1: 1発で 65 切る → 火勢 ／ 溜め ／ 残り", $"{FireLevelRule.Of(bo)}/{bo.RawCounter(FireKindleRule.HoardKey)}/{bo.RawCounter(FireKindleRule.GuardKey)}", "4/1/5");
        }

        // ---- B2 開幕の火勢: くすぶりで火勢2・その周回は萎まない ----
        foreach (var (borg, name, card) in new[] { (UnitCatalog.BorgM0, "M0", false), (BorgB2, "M-B2", true) })
        {
            var ctx = Ctx(Formation.Build(center: borg), Formation.Build(front1: Plain("e1")), out var p, out _, turn: 0);
            var bo = U(p, "borg");
            var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
            ctx.SelfKindle(bo);
            int lv0 = FireLevelRule.Of(bo);
            TurnP.SetValue(ctx, 1); ctx.WiltFire();
            int lv1 = FireLevelRule.Of(bo);
            TurnP.SetValue(ctx, 2); ctx.WiltFire();
            Expect($"{name}: くすぶりの後 ／ 周回1 の終わり ／ 周回2 の終わり（育たなければ萎む）／ 見出し ／ 乱数", $"{lv0}/{lv1}/{FireLevelRule.Of(bo)}/{Lbl(ctx.Events.ToList(), FireLevelLabels.KindleOpen)}/{cr.N}", card ? "2/2/1/1/0" : "1/1/1/0/0");
        }

        // ---- B3 放熱で育つ: ホタの焼き尽くすで +1（燃えている間）・印はそのまま ----
        foreach (var (borg, name, card) in new[] { (UnitCatalog.BorgM0, "M0", false), (BorgB3, "M-B3", true) })
            foreach (bool burning in new[] { true, false })
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.Hota, center: borg), Formation.Build(front1: Plain("e1", hp: 50000)), out var p, out _);
                var h = U(p, "hota"); var bo = U(p, "borg");
                SetLv(h, 4); if (burning) SetLv(bo, 2);
                GiftF.SetValue(ctx, h);
                ctx.TakeTurn(h);
                GiftF.SetValue(ctx, null);
                int want = burning ? (card ? 3 : 2) : 0;
                Expect($"{name}・ボルグ{(burning ? "火勢2" : "燃えていない")}: 焼き尽くすの後のボルグの火勢 ／ 放熱の印", $"{FireLevelRule.Of(bo)}/{bo.RawCounter(FireCycleRule.CallKey) > 0}", $"{want}/{burning}");
            }

        // ---- O1 溜め火: 火勢4 の育ちで +1・次の爆炎で ×(3 ＋ 0.5 × 溜め)・味方への燃焼も ×(1 ＋ 0.5 × 溜め)・撃ったら 0 ----
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.HiyoM0, center: BorgM1), Formation.Build(front1: Plain("e1")), out var p, out _);
            var hy = U(p, "hiyo"); var bo = U(p, "borg");
            SetLv(hy, 1); SetLv(bo, 3);
            var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
            ctx.Stoke(hy, bo);                       // 3 → 4（溜めない）
            int h0 = bo.RawCounter(FireKindleRule.HoardKey);
            ctx.Stoke(hy, bo); ctx.Stoke(hy, bo);    // 4 → 溜め 2
            Expect("M1: 3→4 の煽りでは溜めない ／ 火勢4 で2回 → 溜め ／ 見出し ／ 乱数", $"{h0}/{bo.RawCounter(FireKindleRule.HoardKey)}/{Lbl(ctx.Events.ToList(), FireLevelLabels.Hoard)}/{cr.N}", "0/2/2/0");
        }
        {
            var atk = new Dictionary<int, (int Swing, int Ally, int Hoard, string Rel)>();
            foreach (int hoard in new[] { 0, 1, 2, 3 })
            {
                var ctx = Ctx(Formation.Build(front1: Plain("a1", hp: 5000), center: BorgM1), Formation.Build(front1: Plain("e1", hp: 50000), front3: Plain("e2", hp: 50000)), out var p, out _);
                var bo = U(p, "borg");
                SetLv(bo, 4); bo.SetCounter(FireKindleRule.HoardKey, hoard);
                int baseAtk = bo.CurrentAttack;
                GiftF.SetValue(ctx, bo);
                int n0 = ctx.Events.Count;
                ctx.TakeTurn(bo);
                GiftF.SetValue(ctx, null);
                var ev = Since(ctx, n0);
                var sw0 = ev.First(x => x.Kind == BattleEventKind.Attack && x.ActorId == bo.InstanceId).Amount;
                var al = ev.First(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Blaze).Amount;
                var rel = ev.FirstOrDefault(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.HoardRelease);
                atk[hoard] = (sw0, al, bo.RawCounter(FireKindleRule.HoardKey), rel is null ? "-" : $"{rel.Amount}@{rel.Slot}");
                Expect($"M1 溜め{hoard}: 爆炎の1発 ＝ 攻撃力 {baseAtk} × (300 ＋ 50 × {hoard})% ／ 味方への燃焼 ＝ × (100 ＋ 50 × {hoard})% ／ 撃った後の溜め ／ 解き放つの見出し",
                    $"{sw0}/{al}/{bo.RawCounter(FireKindleRule.HoardKey)}/{atk[hoard].Rel}",
                    $"{baseAtk * (300 + 50 * hoard) / 100}/{baseAtk * (100 + 50 * hoard) / 100}/0/{(hoard == 0 ? "-" : $"{hoard}@{300 + 50 * hoard}")}");
            }
        }

        // ---- O2 鎧の火: 火勢4 の育ちで破片 +6・4 未満では付かない ----
        foreach (var (borg, name, card) in new[] { (UnitCatalog.BorgM0, "M0", false), (BorgM2, "M2", true) })
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.HiyoM0, center: borg), Formation.Build(front1: Plain("e1")), out var p, out _);
            var hy = U(p, "hiyo"); var bo = U(p, "borg");
            SetLv(hy, 1); SetLv(bo, 3);
            var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
            ctx.Stoke(hy, bo);
            int a1 = bo.RawCounter(StatusKeys.Armor);
            ctx.Stoke(hy, bo); ctx.Stoke(hy, bo);
            Expect($"{name}: 3→4 で破片 ／ 火勢4 で2回の後の破片 ／ 見出し ／ 乱数", $"{a1}/{bo.RawCounter(StatusKeys.Armor)}/{Lbl(ctx.Events.ToList(), FireLevelLabels.ArmorFlame)}/{cr.N}", card ? "0/12/2/0" : "0/0/0/0");
        }

        // ---- H1 渡す火: ヒヨの火勢4 の育ちで溜め +1（上限 3）・ギフトで相手を溜めの数だけ上げる（上限 4・あぶれた火にならない）・撃ったら 0 ----
        foreach (var (hiyo, name, card) in new[] { (UnitCatalog.HiyoM0, "M0", false), (HiyoH1, "M1", true) })
        {
            var ctx = Ctx(Formation.Build(front1: hiyo, center: BorgM1, back1: UnitCatalog.Hota, back3: Plain("a1")), Formation.Build(front1: Plain("e1")), out var p, out _);
            var hy = U(p, "hiyo"); var bo = U(p, "borg"); var h = U(p, "hota"); var a1 = U(p, "a1");
            SetLv(hy, 4); SetLv(a1, 1);
            var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
            for (int i = 0; i < 4; i++) ctx.Stoke(hy, a1);   // ヒヨ自身が火勢4 で4回育つ → 溜め 3（1回は上限で捨てる）
            int held = hy.RawCounter(FireKindleRule.GiftHoardKey);
            SetLv(bo, 2); SetLv(h, 4);
            int boHoard0 = bo.RawCounter(FireKindleRule.HoardKey);
            int n0 = ctx.Events.Count;
            ctx.QueueGift(hy, new[] { bo, h }, 4);
            var ev = Since(ctx, n0);
            Expect($"{name}: 溜め（上限 3）／ ギフトの後のボルグ（2 → 2＋3 は上限 4）／ ホタ（既に 4）／ 撃った後の溜め ／ 渡す火の見出し ／ ボルグの溜め火は増えない ／ 乱数",
                $"{held}/{FireLevelRule.Of(bo)}/{FireLevelRule.Of(h)}/{hy.RawCounter(FireKindleRule.GiftHoardKey)}/{Lbl(ev, FireLevelLabels.GiftHoard)}/{bo.RawCounter(FireKindleRule.HoardKey) - boHoard0}/{cr.N}",
                card ? "3/4/4/0/1/0/0" : "0/2/4/0/0/0/0");
            Expect($"{name}: 帳簿（溜めた ／ 捨てた ／ 上げた ／ 4 に届いた ／ 既に 4）", $"{ctx.FireBook.GiftHoardAdds}/{ctx.FireBook.GiftHoardCapped}/{ctx.FireBook.GiftHoardRaised}/{ctx.FireBook.GiftHoardTo4}/{ctx.FireBook.GiftHoardAt4}", card ? "3/1/1/1/1" : "0/0/0/0/0");
        }
        // 溜め 1 で火勢3 のボルグ → 4
        {
            var ctx = Ctx(Formation.Build(front1: HiyoH1, center: BorgM1), Formation.Build(front1: Plain("e1")), out var p, out _);
            var hy = U(p, "hiyo"); var bo = U(p, "borg");
            SetLv(hy, 4); SetLv(bo, 3); hy.SetCounter(FireKindleRule.GiftHoardKey, 1);
            ctx.QueueGift(hy, new[] { bo }, 4);
            Expect("M1: 溜め 1 で火勢3 のボルグ → 4（指名・相手選びの後に上げる）", FireLevelRule.Of(bo), 4);
        }

        // ---- H2 癒しの灯: 火勢4 の育ち1回につき燃えている味方全員を 4 回復（燃えていない味方は癒さない・ベニの反転の裏を通らない）----
        foreach (var (hiyo, name, card) in new[] { (UnitCatalog.HiyoM0, "M0", false), (HiyoH2, "M2", true) })
        {
            var ctx = Ctx(Formation.Build(front1: hiyo, front3: Plain("a2"), center: UnitCatalog.Beni, back1: Plain("a1"), back3: Plain("a3")), Formation.Build(front1: Plain("e1")), out var p, out _);
            var hy = U(p, "hiyo"); var a1 = U(p, "a1"); var a2 = U(p, "a2"); var a3 = U(p, "a3");
            SetLv(hy, 4); SetLv(a1, 1); SetLv(a2, 1);
            foreach (var u in new[] { hy, a1, a2, a3 }) u.Hp -= 20;
            var hp0 = new[] { hy.Hp, a1.Hp, a2.Hp, a3.Hp };
            var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
            ctx.Stoke(hy, a1);   // a1 は 1 → 2、ヒヨは火勢4 で育つ → 灯 1回
            var d = new[] { hy.Hp - hp0[0], a1.Hp - hp0[1], a2.Hp - hp0[2], a3.Hp - hp0[3] };
            Expect($"{name}: 灯の後の HP の増え（ヒヨ ／ 燃えている a1（ベニの隣）／ 燃えている a2 ／ 燃えていない a3）／ 見出し ／ 乱数", $"{string.Join(",", d)}/{Lbl(ctx.Events.ToList(), FireLevelLabels.MendGlow)}/{cr.N}",
                card ? "4,4,4,0/1/0" : "0,0,0,0/0/0");
        }

        // ---- 台で回す: verbose の有無・決定性・死因の合計 ＝ 落ちた駒・見出しの数 ＝ 帳簿・札の無い版では 0・B1 の残り・溜め火の倍率 ----
        long battles = 0, verbDiff = 0, detDiff = 0, causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, evBad = 0, guardBad = 0, pctBad = 0, liftBig = 0, openBad = 0;
        var lk = new object();
        foreach (var v in Versions)
            foreach (var bf in new Func<Formation>[] { () => FC.T3244, () => FC.T3238, () => FC.ThunderBorg })
                foreach (int w in new[] { 0, 2, 3, BA.MainWave, BA.MainWave + 1, FC.WaveHeavy })
                    for (int s = 0; s < BA.Scales.Length; s++)
                    {
                        var f = Apply(bf(), v); var sc = BA.Scales[s].Sc;
                        Parallel.For(0, 30, seed =>
                        {
                            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam); var en = FC.WaveOf(w, sc)();
                            var slot0 = pl.Concat(en).ToDictionary(u => u, u => u.Slot);
                            var r = BattleEngine.Run(pl, en, seed, verbose: true);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: false);
                            var r3 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: true);
                            var agg = new BA.Agg(); agg.Take(r, pl, en, slot0.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
                            var fl = r.FireLevels!;
                            int E(string l) => r.Events.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == l);
                            bool Has(TraitId t) => v.Borg.Traits.Contains(t) || v.Hiyo.Traits.Contains(t);
                            int bad = 0;
                            void Eq(long a, long b) { if (a != b) bad++; }
                            Eq(E(FireLevelLabels.KindleGuard), fl.GuardSteps); Eq(E(FireLevelLabels.KindleOpen), fl.OpenLv); Eq(E(FireLevelLabels.RadiateGrow), fl.RadiateGrowN);
                            Eq(E(FireLevelLabels.Hoard), fl.HoardAdds); Eq(E(FireLevelLabels.HoardRelease), fl.HoardBlazes); Eq(E(FireLevelLabels.ArmorFlame), fl.ArmorFlameN);
                            Eq(E(FireLevelLabels.GiftHoardAdd), fl.GiftHoardAdds); Eq(E(FireLevelLabels.GiftHoard), fl.GiftHoardRaised); Eq(E(FireLevelLabels.MendGlow), fl.MendGlowN);
                            Eq(fl.ArmorFlameAmt, fl.ArmorFlameN * FireKindleRule.ArmorFlame);
                            if (!Has(TraitId.KindleGuard)) Eq(fl.GuardSteps + fl.GuardSaved[0] + fl.GuardSaved[1] + fl.GuardOff, 0);
                            if (!Has(TraitId.KindleOpen)) Eq(fl.OpenLv, 0);
                            if (!Has(TraitId.RadiateGrow)) Eq(fl.RadiateGrowN, 0);
                            if (!Has(TraitId.BlazeHoard)) Eq(fl.HoardAdds + fl.HoardLog.Count, 0);
                            if (!Has(TraitId.ArmorFlame)) Eq(fl.ArmorFlameN, 0);
                            if (!Has(TraitId.GiftHoard)) Eq(fl.GiftHoardAdds + fl.GiftHoardRaised + fl.GiftHoardAt4, 0);
                            if (!Has(TraitId.MendGlow)) Eq(fl.MendGlowN, 0);
                            long g = fl.GuardSaved[0] + fl.GuardSaved[1] - FireKindleRule.GuardStep * fl.GuardSteps;
                            int gb = g < 0 || g >= FireKindleRule.GuardStep ? 1 : 0;
                            int pb = fl.HoardLog.Count(x => x.Percent != FireBurstRule.UnleashPercent + x.Hoard * FireKindleRule.HoardPercent);
                            // 開幕の火勢: くすぶりの札を持つ台では、周回1 の頭の写しでボルグが火勢 2 以上（燃えていれば）
                            int ob = 0;
                            if (Has(TraitId.KindleOpen))
                            {
                                int? bo = pl.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId;
                                foreach (var sn in fl.Snaps) if (sn.Id == bo && sn.Turn == 1 && sn.Burning && sn.Level < 2) ob++;
                            }
                            lock (lk)
                            {
                                battles++;
                                if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns || r.PlayerSurvivors != r2.PlayerSurvivors) verbDiff++;
                                if (r.Events.Count != r3.Events.Count || r.Log.Count != r3.Log.Count || r.Turns != r3.Turns) detDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                                evBad += bad; guardBad += gb; pctBad += pb; liftBig += fl.GiftHoardBig; openBad += ob;
                            }
                        });
                    }
        Expect($"台で回す（{battles} 戦）: 見出しの数 ＝ 帳簿・札の無い版では 0", evBad, 0L);
        Expect("台で回す: B1 の切った量 − 30 × (+1 の回数) が 0 以上 30 未満", guardBad, 0L);
        Expect("台で回す: 溜め火の敵への倍率 ＝ 300 ＋ 50 × 溜め", pctBad, 0L);
        Expect("台で回す: 開幕の火勢の台では周回1 の頭でボルグの火勢 ≥ 2", openBad, 0L);
        Console.WriteLine($"  （参考: 渡す火で 4 に届いた相手がそのギフトの手番で大技を撃った {liftBig} 回）");
        Expect("verbose の有無で勝敗・決着T・生存が同じ", verbDiff, 0L);
        Expect("同じ seed を2度回して台本の長さが同じ", detDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 3）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);

        // ---- M0 ＝ 規定の駒（参照）・新しい札の保持者は 0 枚 ----
        var m0 = VerOf("M0");
        // 第253期: 規定のボルグ・ヒヨに B1・B2・溜め火・渡す火が入ったので、M0 は旧の駒（BorgM0 / HiyoM0）に固定し、保持者は規定の2枚になった。
        Expect("M0 の駒 ＝ 第251期の規定（BorgM0 / Hota / HiyoM0・参照）", ReferenceEquals(m0.Borg, UnitCatalog.BorgM0) && ReferenceEquals(m0.Hota, UnitCatalog.Hota) && ReferenceEquals(m0.Hiyo, UnitCatalog.HiyoM0), true);
        Expect("新しい札の保持者（`UnitCatalog.Everyone`・第253期から規定のボルグ・ヒヨの2枚）", string.Join(",", UnitCatalog.Everyone.Where(d => d.Traits.Any(FireKindleRuleHas)).Select(d => d.Id)), "borg,hiyo");
        Expect("新しい札は7枚とも `TraitCatalog` に登録されている", new[] { TraitId.KindleGuard, TraitId.KindleOpen, TraitId.RadiateGrow, TraitId.BlazeHoard, TraitId.ArmorFlame, TraitId.GiftHoard, TraitId.MendGlow }.All(t => TraitCatalog.Get(t).Id == t), true);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
        Console.WriteLine($"FIREKINDLE_CHECK ok={ok} ng={ng}");
    }
    static bool FireKindleRuleHas(TraitId t) => t is TraitId.KindleGuard or TraitId.KindleOpen or TraitId.RadiateGrow or TraitId.BlazeHoard or TraitId.ArmorFlame or TraitId.GiftHoard or TraitId.MendGlow;
}
