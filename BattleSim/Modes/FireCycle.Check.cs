using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// firecycle check —— 自己検査（受け入れ 2〜5・追記 D）。Q0 の台本の突き合わせは `firecycle digest` を前段の規定化だけのコードと cmp で見る。
static partial class FireCycleDiag
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
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e, out CountingRandom cr)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        cr = new CountingRandom(7); RngF.SetValue(ctx, cr);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 1000, int atk = 10, AttackPattern pat = AttackPattern.Single, params TraitId[] tr)
        => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = tr, Pattern = pat };
    static UnitDef HiyoT(params TraitId[] tr) => new()
    {
        Id = "hiyo", Name = "hiyo", MaxHp = 1000, Attack = 5, Speed = 1, Actions = new UnitAction[] { new(ActionKind.Skill, Label: "火を煽る／火を渡す") },
        Traits = new[] { TraitId.FireStoke, TraitId.TurnGift, TraitId.StokeStageAtk, TraitId.GiftQuiet }.Concat(tr).ToArray(),
    };
    static UnitDef HotaT(params TraitId[] tr) => new()
    {
        Id = "hota", Name = "hota", MaxHp = 1000, Attack = 6, Speed = 1,
        Traits = new[] { TraitId.Pyre, TraitId.PyreStage, TraitId.FireLevel, TraitId.PyreBurnout, TraitId.PyreEmbers, TraitId.CallFire }.Concat(tr).ToArray(),
    };
    static UnitDef BorgT(params TraitId[] tr) => Plain("borg", atk: 18, pat: AttackPattern.Sweep,
        tr: new[] { TraitId.FireLevel, TraitId.FireUnleash, TraitId.FoeFireLevel }.Concat(tr).ToArray());
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static void SetLv(UnitState u, int lv, int burn = 3) { u.SetCounter(StatusKeys.Burn, burn); u.SetCounter(FireLevelRule.LvKey, lv); }
    static List<BattleEvent> Since(BattleContext ctx, int n0) => ctx.Events.Skip(n0).ToList();
    static int FLn(List<BattleEvent> ev, string label) => ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == label);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第246期 firecycle check");
        Console.WriteLine();

        // ---- 前段 ----
        Expect("前段: 規定のボルグ ＝ 第245期の規定 ＋ E2 の5枚", string.Join(",", UnitCatalog.BorgU0.Traits),
            string.Join(",", UnitCatalog.BorgE0.Traits.Concat(new[] { TraitId.FoeFireLevel, TraitId.FoeFireTick, TraitId.FoeFireBrittle, TraitId.FoeFireSpread, TraitId.AllyFireTick })));
        Expect("前段: 第245期の器具の E0 は旧のボルグ", EnemyFireDiag.VerOf("E0").Borg == UnitCatalog.BorgE0, true);
        foreach (TraitId t in new[] { TraitId.StokePick, TraitId.HiyoSpark, TraitId.BorgRadiate })   // 第247期 前段: 臨界・大火槍は規定のホタが持つようになった
            Expect($"札 {t} の保持者は `All` に 0 枚", UnitCatalog.All.Count(u => u.Traits.Contains(t)), 0);

        // ---- 煽りの相手: 型が変わる味方を優先 ----
        foreach (bool pick in new[] { false, true })
        {
            var tr = pick ? new[] { TraitId.StokePick } : Array.Empty<TraitId>();
            var ctx = Ctx(Formation.Build(front1: HiyoT(tr), front3: BorgT(), center: Plain("mate", atk: 40, tr: TraitId.FireLevel)), Formation.Build(front1: Plain("e1")), out var p, out _, out var cr);
            SetLv(U(p, "hiyo"), 1); SetLv(U(p, "borg"), 3); SetLv(U(p, "mate"), 2);
            int n0 = ctx.Events.Count, r0 = cr.N;
            ctx.TakeTurn(U(p, "hiyo"));
            var ev = Since(ctx, n0);
            var st = ev.First(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Stoke);
            string to = st.TargetId == U(p, "borg").InstanceId ? "borg" : "mate";
            Expect($"煽り（相手選び {(pick ? "あり" : "なし")}）: ボルグ 火勢3（+1 で大技の準備）対 相方 火勢2・攻40（型は変わらない）", to, pick ? "borg" : "mate");
            if (pick) Expect("煽り: 理由の見出し「型が変わる」が1件・乱数を引かない", $"{FLn(ev, FireLevelLabels.StokeForm)}/{cr.N - r0}", "1/0");
        }
        // 型が変わるか（表）
        {
            var ctx = Ctx(Formation.Build(front1: HotaT(), front3: HotaT(TraitId.PyreLance), center: HotaT(TraitId.PyreLance, TraitId.PyreCritical), back1: BorgT()), Formation.Build(front1: Plain("e1")), out var p, out _, out _);
            var h5 = p[0]; var hl = p[1]; var hp = p[2]; var bo = U(p, "borg");
            string Row(UnitState u) => string.Join("", Enumerable.Range(1, 3).Select(l => FormChangesAt(u, l) ? "○" : "×"));
            Expect("型が変わる（火勢1・2・3 から +1）: H5 ／ 大火槍だけ ／ HP ／ ボルグ", $"{Row(h5)} {Row(hl)} {Row(hp)} {Row(bo)}", "○○○ ○○○ ○○○ ××○");
            Expect("型の札（火勢1〜4）: H5 ／ HP", $"{string.Join("", Enumerable.Range(1, 4).Select(l => PyreStageTrait.FormAt(h5, l)))} {string.Join("", Enumerable.Range(1, 4).Select(l => PyreStageTrait.FormAt(hp, l)))}", "1233 1245");
        }

        // ---- ギフトの相手: 大技の準備ができた味方を優先 ----
        foreach (bool pick in new[] { false, true })
            foreach (int hl in new[] { 3, 4 })
            {
                var tr = pick ? new[] { TraitId.StokePick } : Array.Empty<TraitId>();
                var ctx = Ctx(Formation.Build(front1: HiyoT(tr), front3: BorgT(), center: Plain("mate", atk: 40, tr: TraitId.FireLevel), back1: HotaT()), Formation.Build(front1: Plain("e1", hp: 100000)), out var p, out _, out _);
                SetLv(U(p, "hiyo"), hl); SetLv(U(p, "borg"), 4); SetLv(U(p, "mate"), 4); SetLv(U(p, "hota"), 2);
                int n0 = ctx.Events.Count;
                ctx.TakeTurn(U(p, "hiyo"));
                var gifts = Since(ctx, n0).Where(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Gift).Select(x => p.First(u => u.InstanceId == x.TargetId).Def.Id);
                string want = (pick, hl) switch { (false, 3) => "mate", (false, 4) => "mate,borg", (true, 3) => "borg", _ => "borg,mate" };
                Expect($"ギフト（相手選び {(pick ? "あり" : "なし")}・ヒヨの火勢 {hl}）: ボルグ 4（準備）・相方 4 攻40（準備でない）・ホタ 2", string.Join(",", gifts), want);
            }

        // ---- ヒヨは煽りと火の粉でだけ育つ（燃え広がりでは育たない）----
        foreach (bool spark in new[] { false, true })
        {
            var ctx = Ctx(Formation.Build(front1: spark ? HiyoT(TraitId.HiyoSpark) : HiyoT(), front3: BorgT()), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e, out _);
            SetLv(U(p, "hiyo"), 1); SetLv(U(p, "borg"), 1);
            foreach (var f in e) SetLv(f, 1);
            ctx.TakeTurn(U(p, "borg"));
            Expect($"燃え広がり（上限なしのボルグの薙ぎが燃えている敵3体に当たる）でヒヨ（火の粉の札 {(spark ? "あり" : "なし")}）の火勢", FireLevelRule.Of(U(p, "hiyo")), spark ? 1 : 4);
        }
        // 焼き尽くす → 火の粉（ヒヨ +1）・放熱（ボルグに1つ・重ねない）・次の通常の手番で +1・ギフトの手番では使わない
        {
            var ctx = Ctx(Formation.Build(front1: HiyoT(TraitId.StokePick, TraitId.HiyoSpark), front3: BorgT(TraitId.BorgRadiate), center: HotaT()), Formation.Build(front1: Plain("e1", hp: 100000), front3: Plain("e3", hp: 100000)), out var p, out _, out var cr);
            var hy = U(p, "hiyo"); var bo = U(p, "borg"); var ho = U(p, "hota");
            SetLv(hy, 3); SetLv(bo, 2); SetLv(ho, 4);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);   // ギフト（火勢3・1体）→ ホタ（準備）→ 焼き尽くす
            var ev = Since(ctx, n0);
            Expect("焼き尽くす: ヒヨのギフトはホタへ・焼き尽くすが1回", $"{FLn(ev, FireLevelLabels.GiftReady)}/{FLn(ev, FireLevelLabels.Burnout)}", "1/1");
            Expect("火の粉: ヒヨは撃って 1 → 火の粉で 2", FireLevelRule.Of(hy), 2);
            Expect("放熱: ボルグに印 1・見出し1件", $"{bo.RawCounter(FireCycleRule.RadiateKey)}/{FLn(ev, FireLevelLabels.Radiate)}", "1/1");
            // 重ねない: もう一度焼き尽くす
            SetLv(hy, 3); SetLv(ho, 4);
            long stacked0 = ctx.FireBook.RadiateStacked;
            ctx.TakeTurn(hy);
            Expect("放熱は重ねない（2度目の焼き尽くすで印は 1 のまま・捨てた回数 +1）", $"{bo.RawCounter(FireCycleRule.RadiateKey)}/{ctx.FireBook.RadiateStacked - stacked0}", "1/1");
            // ギフトの手番では使わない: ボルグを準備（4）にしてギフトを受けさせる（ホタは 1）
            SetLv(bo, 4); SetLv(ho, 1); SetLv(hy, 3);
            int n1 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            var ev1 = Since(ctx, n1);
            Expect("ギフトの手番（ボルグ）では放熱を使わない（放つは撃つ・印は残る）", $"{FLn(ev1, FireLevelLabels.Unleash)}/{FLn(ev1, FireLevelLabels.RadiateUse)}/{bo.RawCounter(FireCycleRule.RadiateKey)}", "1/0/1");
            // 次の通常の手番の頭で +1（放つで 1 に戻っているので 1 → 2・その後の攻撃の燃え広がりは敵が燃えていないので無し）
            int lvB = FireLevelRule.Of(bo), r0 = cr.N;
            int n2 = ctx.Events.Count;
            ctx.TakeTurn(bo);
            var ev2 = Since(ctx, n2);
            int iUse = ev2.FindIndex(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.RadiateUse);
            int iAtk = ev2.FindIndex(x => x.Kind == BattleEventKind.Attack && x.ActorId == bo.InstanceId);
            Expect("通常の手番: 頭で放熱を使って +1（攻撃より前）・印は 0", $"{lvB}→{ev2.First(x => x.Text == FireLevelLabels.GrowRadiate).Amount}/{(iUse >= 0 && iUse < iAtk)}/{bo.RawCounter(FireCycleRule.RadiateKey)}", $"{lvB}→{lvB + 1}/True/0");
            // 乱数: 放熱の有無で同じ手番の乱数の数が同じ
            bo.SetCounter(FireCycleRule.RadiateKey, 1); SetLv(bo, 1);
            int ra = cr.N; ctx.TakeTurn(bo); int da = cr.N - ra;
            SetLv(bo, 1); int rb = cr.N; ctx.TakeTurn(bo); int db = cr.N - rb;
            Expect("乱数: 放熱を使う手番と使わない手番で乱数の数が同じ", da, db);
        }
        // 燃えていないボルグは蓄えない
        {
            var ctx = Ctx(Formation.Build(front1: HiyoT(TraitId.StokePick), front3: BorgT(TraitId.BorgRadiate), center: HotaT()), Formation.Build(front1: Plain("e1", hp: 100000)), out var p, out _, out _);
            SetLv(U(p, "hiyo"), 3); SetLv(U(p, "hota"), 4); U(p, "borg").SetCounter(StatusKeys.Burn, 0);
            ctx.TakeTurn(U(p, "hiyo"));
            Expect("燃えていないボルグは放熱を蓄えない", U(p, "borg").RawCounter(FireCycleRule.RadiateKey), 0);
        }

        // ---- 臨界: 自分の手番の火勢4 だけ・当たった敵 +1（燃え広がりと合わせて +2）----
        {
            var ctx = Ctx(Formation.Build(front1: HotaT(TraitId.PyreCritical), front3: BorgT()), Formation.Build(front1: Plain("e1", hp: 100000), center: Plain("ec", hp: 100000), back1: Plain("eb", hp: 100000)), out var p, out var e, out var cr);
            var ho = U(p, "hota");
            SetLv(ho, 4); SetLv(U(e, "e1"), 1);   // e1 は燃えている（1）・ec は燃えていない
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(ho);
            var ev = Since(ctx, n0);
            var atk = ev.First(x => x.Kind == BattleEventKind.Attack && x.ActorId == ho.InstanceId);
            var d1 = ev.First(x => x.Kind == BattleEventKind.Damage && x.ActorId == ho.InstanceId);
            Expect("臨界（自分の手番・火勢4）: 見出し1件・貫き・1発目 ＝ 攻6 × 7（脆さ込み）", $"{FLn(ev, FireLevelLabels.Critical)}/{atk.Pattern}/{d1.Amount - (d1.BrittleExtra ?? 0)}", $"1/{AttackPattern.Pierce}/42");
            var hit = e.Where(f => ev.Any(x => x.Kind == BattleEventKind.Damage && x.TargetId == f.InstanceId && x.ActorId == ho.InstanceId)).ToList();
            string lv = string.Join(",", hit.Select(f => $"{f.Def.Id}{FireLevelRule.Of(f)}"));
            Expect("臨界: 当たった敵の火勢——燃えていた e1 は 1 → 3（臨界 +1 ＋ 燃え広がり +1）・燃えていなかった敵は着火 1 → 2", lv, string.Join(",", hit.Select(f => f.Def.Id == "e1" ? "e13" : $"{f.Def.Id}2")));
            // ギフトの手番の火勢4 は焼き尽くす（臨界にならない）
            var ctx2 = Ctx(Formation.Build(front1: HiyoT(TraitId.StokePick), front3: HotaT(TraitId.PyreCritical)), Formation.Build(front1: Plain("e1", hp: 100000)), out var p2, out _, out _);
            SetLv(U(p2, "hiyo"), 3); SetLv(U(p2, "hota"), 4);
            int m0 = ctx2.Events.Count;
            ctx2.TakeTurn(U(p2, "hiyo"));
            var ev2 = Since(ctx2, m0);
            Expect("ギフトの手番の火勢4 は焼き尽くす・臨界は出ない", $"{FLn(ev2, FireLevelLabels.Burnout)}/{FLn(ev2, FireLevelLabels.Critical)}", "1/0");
            // 火勢3 は臨界にならない（H5 は5連撃）
            SetLv(ho, 3);
            int n3 = ctx.Events.Count;
            ctx.TakeTurn(ho);
            var ev3 = Since(ctx, n3);
            Expect("H5 の火勢3 は5連撃（攻撃 5 回・臨界なし）", $"{ev3.Count(x => x.Kind == BattleEventKind.Attack && x.ActorId == ho.InstanceId)}/{FLn(ev3, FireLevelLabels.Critical)}", "5/0");
        }
        // HP（大火槍）: 火勢3 は貫き ×7 の1発
        {
            var ctx = Ctx(Formation.Build(front1: HotaT(TraitId.PyreLance, TraitId.PyreCritical)), Formation.Build(front1: Plain("e1", hp: 100000), center: Plain("ec", hp: 100000)), out var p, out _, out var cr);
            var ho = U(p, "hota"); SetLv(ho, 3);
            int n0 = ctx.Events.Count, r0 = cr.N;
            ctx.TakeTurn(ho);
            var ev = Since(ctx, n0);
            var d1 = ev.First(x => x.Kind == BattleEventKind.Damage && x.ActorId == ho.InstanceId);
            Expect("HP の火勢3: 攻撃1回・貫き・1発目 ＝ 攻6 × 7（脆さの分を除く）・臨界なし", $"{ev.Count(x => x.Kind == BattleEventKind.Attack && x.ActorId == ho.InstanceId)}/{ev.First(x => x.Kind == BattleEventKind.Attack).Pattern}/{d1.Amount - (d1.BrittleExtra ?? 0)}/{FLn(ev, FireLevelLabels.Critical)}", $"1/{AttackPattern.Pierce}/42/0");
            // 乱数: 臨界（HP の火勢4）と大火槍（HP の火勢3）で同じ貫き1発の乱数の数
            int da = cr.N - r0;
            SetLv(ho, 4); int rb = cr.N; ctx.TakeTurn(ho); int db = cr.N - rb;
            Expect("乱数: 大火槍の手番と臨界の手番で乱数の数が同じ（臨界の +1 は乱数を引かない）", da, db);
        }

        // ---- 盤面: 版ごとに 3台 × 9波 × 倍率（的は 1）× seed 40 ----
        long battles = 0, verbDiff = 0, detDiff = 0, causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, critOutside = 0, radiateGift = 0, sparkBad = 0, targetWins = 0, targetTurns = 0, targetN = 0;
        var lk = new object();
        foreach (var v in Versions)
            foreach (var bf in new[] { T3244, T3238, ThunderBorg })
            {
                var f = Apply(bf, v);
                for (int w = 0; w < WaveNames.Length; w++)
                    for (int s = 0; s < (IsTarget(w) ? 1 : 3); s++)
                        Parallel.For(0, 40, seed =>
                        {
                            var sc = IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc;
                            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var en = WaveOf(w, sc)();
                            var slotOf = pl.Concat(en).ToDictionary(u => u, u => u.Slot);
                            var r = BattleEngine.Run(pl, en, seed, verbose: true);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), WaveOf(w, sc)(), seed, verbose: false);
                            var r3 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), WaveOf(w, sc)(), seed, verbose: true);
                            var agg = new BA.Agg(); agg.Take(r, pl, en, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
                            // 臨界の見出しは「ギフトの手番の外」だけ・放熱を使うのは「ギフトの手番の外」だけ
                            int co = 0, rg = 0, sb = 0;
                            var giftHands = new List<(int S, int E)>();
                            for (int i = 0; i < r.Events.Count; i++)
                                if (r.Events[i] is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.GiftTurn } gt)
                                {
                                    var hd = r.Hands.Where(h => h.ActorId == gt.TargetId && h.EventStart > i).OrderBy(h => h.EventStart).FirstOrDefault();
                                    if (hd.EventEnd > 0) giftHands.Add((hd.EventStart, hd.EventEnd));
                                }
                            bool InGift(int i) => giftHands.Any(g => i >= g.S && i < g.E);
                            for (int i = 0; i < r.Events.Count; i++)
                            {
                                var x = r.Events[i];
                                if (x.Kind != BattleEventKind.FireLevel) continue;
                                if (x.Text == FireLevelLabels.Critical && InGift(i)) co++;
                                if (x.Text == FireLevelLabels.RadiateUse && InGift(i)) rg++;
                                // ヒヨ（火の粉の札）の「育つ」は煽り・火の粉だけ
                                if (v.Hiyo.Traits.Contains(TraitId.HiyoSpark) && x.Text == FireLevelLabels.GrowSelf && x.TargetId is int ht && pl.Any(u => u.InstanceId == ht && u.Def.Id == "hiyo") && x.ActorId != ht) sb++;
                            }
                            lock (lk)
                            {
                                battles++;
                                if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns || r.PlayerSurvivors != r2.PlayerSurvivors
                                    || (r.FireLevels?.Criticals ?? 0) != (r2.FireLevels?.Criticals ?? 0) || (r.FireLevels?.RadiateUsed ?? 0) != (r2.FireLevels?.RadiateUsed ?? 0)) verbDiff++;
                                if (r.Events.Count != r3.Events.Count || r.Log.Count != r3.Log.Count || r.Turns != r3.Turns) detDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                                critOutside += co; radiateGift += rg; sparkBad += sb;
                                if (IsTarget(w)) { targetN++; targetTurns += r.Turns; var c = new CAgg(); c.Take(r, pl, en, true); targetWins += c.Wins + c.AllSurv + c.Turns; }
                            }
                        });
            }
        Expect($"臨界の見出しはギフトの手番の中に出ない（{battles} 戦）", critOutside, 0L);
        Expect("放熱を使うのはギフトの手番の中ではない", radiateGift, 0L);
        Expect("火の粉の版: ヒヨの「育つ・ヒヨ」はヒヨ自身の煽りから（味方の燃え広がりからは 0）", sparkBad, 0L);
        Expect("verbose の有無で勝敗・決着T・生存・臨界・放熱の数が同じ", verbDiff, 0L);
        Expect("同じ seed を2度回して台本の長さが同じ", detDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 4）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);
        Expect("的の波: 集計は勝率・全員生存・決着T を数えない（8 ターンの打ち切りは集計だけ）", targetWins, 0L);
        Expect("的の波: 戦そのものは 8 ターンで止めていない（決着T の平均が 8 を超える）", targetN > 0 && (double)targetTurns / targetN > TargetTurns, true);

        // ---- `compare`: Q0 が docs/balance.md と一致 ----
        var rates = CompareRates();
        var rows = CompareBuilds();
        // 第251期: Q0 はその期の駒なので、燃焼の規定化のたびにボルグ・ホタ・ヒヨのいる行が docs/balance.md と合わなくなる（第250期 前段で `死軸×ヒヨ` 第五波が 1 セル動いてから落ちていた）。
        // 突き合わせは3体のいない行だけにした（第250期 前段の `firelevel check` と同じ直し・いる行は「どの版でも 0 セル」の側で見る）。
        bool HasFire3(Formation f) => f.Occupied().Any(o => o.Def.Id is "borg" or "hota" or "hiyo");
        var bal = File.ReadAllLines("docs/balance.md", System.Text.Encoding.UTF8).Where(l => l.StartsWith("| ") && l.Contains('%')).ToList();
        int cellBad = 0, cells = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            if (HasFire3(rows[i].F)) continue;
            var line = bal.FirstOrDefault(l => l.StartsWith("| " + rows[i].Name + " |"));
            if (line is null) { cellBad += 5; continue; }
            var c = line.Split('|').Select(x => x.Trim()).Where(x => x.EndsWith('%')).ToArray();
            for (int stg = 0; stg < 5; stg++) { cells++; if (Math.Abs(double.Parse(c[stg].TrimEnd('%')) - rates[0, i, stg]) > 0.01) cellBad++; }
        }
        Expect($"Q0 の `compare`（{cells} セル）が docs/balance.md と一致", cellBad, 0);
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }
}
