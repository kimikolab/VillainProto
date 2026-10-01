using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// firetri check —— 自己検査（受け入れ 2〜5）。T0 の台本の突き合わせは `firetri digest` を前段の規定化だけのコードと cmp で見る。
static partial class FireTriDiag
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
    /// <summary>規定のヒヨの札（贔屓なし）＋ 版の札。</summary>
    static UnitDef HiyoT(params TraitId[] tr) => new()
    {
        Id = "hiyo", Name = "hiyo", MaxHp = 1000, Attack = 5, Speed = 1, Actions = new UnitAction[] { new(ActionKind.Skill, Label: "火を煽る／火を渡す") },
        Traits = new[] { TraitId.FireStoke, TraitId.TurnGift, TraitId.StokeStageAtk, TraitId.GiftQuiet }.Concat(tr).ToArray(),
    };
    static UnitDef HotaT(params TraitId[] tr) => new()
    {
        Id = "hota", Name = "hota", MaxHp = 1000, Attack = 6, Speed = 1,
        Traits = new[] { TraitId.Pyre, TraitId.PyreStage, TraitId.FireLevel, TraitId.PyreBurnout, TraitId.PyreEmbers, TraitId.CallFire, TraitId.PyreLance, TraitId.PyreCritical }.Concat(tr).ToArray(),
    };
    static UnitDef BorgT(params TraitId[] tr) => Plain("borg", atk: 18, pat: AttackPattern.Sweep,
        tr: new[] { TraitId.FireLevel, TraitId.FireUnleash, TraitId.FireSpreadCap, TraitId.FoeFireLevel }.Concat(tr).ToArray());
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static void SetLv(UnitState u, int lv, int burn = 3) { u.SetCounter(StatusKeys.Burn, burn); u.SetCounter(FireLevelRule.LvKey, lv); }
    static List<BattleEvent> Since(BattleContext ctx, int n0) => ctx.Events.Skip(n0).ToList();
    static int FLn(List<BattleEvent> ev, string label) => ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == label);
    static string Ids(List<UnitState> p, IEnumerable<BattleEvent> ev, string label)
        => string.Join(",", ev.Where(x => x.Kind == BattleEventKind.FireLevel && x.Text == label).Select(x => p.First(u => u.InstanceId == x.TargetId).Def.Id));
    static Formation Big => Formation.Build(front1: Plain("e1", hp: 100000), front3: Plain("e3", hp: 100000), center: Plain("ec", hp: 100000));

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第247期 firetri check");
        Console.WriteLine();

        // ---- 前段 ----
        // 第249期 前段: 規定が後の期に札を足しても崩れないよう「先頭が一致」で見る（器具の版は `FC.HotaHP` に固定した）。
        Expect("前段: 規定のホタの札の先頭 ＝ 第246期の規定 ＋ 大火槍・臨界", string.Join(",", UnitCatalog.Hota.Traits.Take(UnitCatalog.HotaQ0.Traits.Count + 2)),
            string.Join(",", UnitCatalog.HotaQ0.Traits.Concat(new[] { TraitId.PyreLance, TraitId.PyreCritical })));
        Expect("前段: 第246期の器具の Q0 ／ 第245期の器具の E0 は旧のホタ", FC.VerOf("Q0").Hota == UnitCatalog.HotaQ0 && EnemyFireDiag.VerOf("E0").Hota == UnitCatalog.HotaQ0, true);
        Expect("前段: 第246期の Q2-HP のホタの札 ＝ 器具の版のホタ（T0）の札", string.Join(",", FC.VerOf("Q2-HP").Hota.Traits), string.Join(",", VerOf("T0").Hota.Traits));
        // 第248期: 放熱（ボルグ）と火の粉（ヒヨ）は規定になった（保持者 1 枚）。(b) は保持者 0 枚のまま。
        foreach (TraitId t in new[] { TraitId.RadiateCall, TraitId.SparkCatch, TraitId.SparkUnleash, TraitId.GiftPair })
            Expect($"札 {t} の保持者は `All` に {(t == TraitId.GiftPair ? 0 : 1)} 枚（第248期に規定）", UnitCatalog.All.Count(u => u.Traits.Contains(t)), t == TraitId.GiftPair ? 0 : 1);
        // 大火槍の見出しは段3 だけ
        foreach (int lv in new[] { 2, 3, 4 })
        {
            var ctx = Ctx(Formation.Build(front1: HotaT()), Formation.Build(front1: Plain("e1", hp: 100000), center: Plain("ec", hp: 100000)), out var p, out _, out _);
            SetLv(U(p, "hota"), lv);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(U(p, "hota"));
            var ev = Since(ctx, n0);
            string want = lv switch { 2 => "0/0", 3 => "1/0", _ => "0/1" };
            Expect($"段{lv} の手番: 見出し「大火槍」／「臨界」", $"{FLn(ev, FireLevelLabels.Lance)}/{FLn(ev, FireLevelLabels.Critical)}", want);
        }

        // ---- 放熱の印: 焼き尽くすで付く・燃えていなければ付かない・重ねない・乱数を引かない ----
        foreach (bool call in new[] { false, true })
        {
            var ctx = Ctx(Formation.Build(front1: HiyoT(), front3: call ? BorgT(TraitId.RadiateCall) : BorgT(), center: HotaT()), Big, out var p, out _, out var cr);
            var hy = U(p, "hiyo"); var bo = U(p, "borg"); var ho = U(p, "hota");
            SetLv(hy, 3); SetLv(bo, 2); SetLv(ho, 4);
            int n0 = ctx.Events.Count, r0 = cr.N;
            ctx.TakeTurn(hy);   // 火勢3・1体 → ホタ（火勢4）→ 焼き尽くす
            var ev = Since(ctx, n0);
            int rolls = cr.N - r0;
            Expect($"焼き尽くす（放熱の札 {(call ? "あり" : "なし")}）: 焼き尽くす 1 ／ 印 {(call ? 1 : 0)} ／ 見出し「放熱の印」", $"{FLn(ev, FireLevelLabels.Burnout)}/{bo.RawCounter(FireCycleRule.CallKey)}/{FLn(ev, FireLevelLabels.CallMark)}", call ? "1/1/1" : "1/0/0");
            if (!call) { _rollsNoCall = rolls; continue; }
            Expect("乱数: 放熱の札の有無で同じ手番の乱数の数が同じ", rolls, _rollsNoCall);
            // 重ねない: ホタの手番をもう一度渡す（ヒヨを通さず、控えに直に積む）
            SetLv(ho, 4);
            long st0 = ctx.FireBook.CallStacked;
            ctx.QueueGift(hy, new[] { ho }, 3);
            ctx.TakeTurn(U(p, "hiyo"));   // ヒヨの手番（ボルグを指名して渡す）の前に、控えのホタが焼き尽くす
            Expect("重ねない（印のあるまま2度目の焼き尽くす → 印は 1・捨てた +1）", ctx.FireBook.CallStacked - st0 >= 1, true);
        }
        {
            var ctx = Ctx(Formation.Build(front1: HiyoT(), front3: BorgT(TraitId.RadiateCall), center: HotaT()), Big, out var p, out _, out _);
            SetLv(U(p, "hiyo"), 3); SetLv(U(p, "hota"), 4); U(p, "borg").SetCounter(StatusKeys.Burn, 0);
            ctx.TakeTurn(U(p, "hiyo"));
            Expect("燃えていないボルグには印が付かない", U(p, "borg").RawCounter(FireCycleRule.CallKey), 0);
        }

        // ---- 指名: 印を持つボルグは火勢に関わらず最優先・指名で印が消える ----
        foreach (var (hyTr, tag) in new[] { (Array.Empty<TraitId>(), "旧育ち（P′）"), (new[] { TraitId.StokePick, TraitId.HiyoSpark }, "新育ち（準備を優先）") })
            foreach (int hl in new[] { 3, 4 })
                foreach (int bl in new[] { 1, 3, 4 })
                {
                    var ctx = Ctx(Formation.Build(front1: HiyoT(hyTr), front3: BorgT(TraitId.RadiateCall), center: HotaT(), back1: Plain("mate", atk: 40, tr: TraitId.FireLevel)), Big, out var p, out _, out var cr);
                    var hy = U(p, "hiyo"); var bo = U(p, "borg");
                    SetLv(hy, hl); SetLv(bo, bl); SetLv(U(p, "hota"), 4); SetLv(U(p, "mate"), 4);
                    bo.SetCounter(FireCycleRule.CallKey, 1);
                    int r0 = cr.N;
                    var order = FireStokeTrait.GiftTargets(ctx, hy).Take(hl == 3 ? 1 : 2).Select(u => u.Def.Id).ToList();
                    Expect($"指名（{tag}・ヒヨ {hl}・ボルグ {bl}）: 相手の並びの先頭はボルグ・並べても乱数を引かない", $"{order[0]}/{cr.N - r0}", "borg/0");
                    int n0 = ctx.Events.Count;
                    ctx.TakeTurn(hy);
                    var ev = Since(ctx, n0);
                    var called = ev.FirstOrDefault(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Called);
                    bool unl = FLn(ev, FireLevelLabels.Unleash) > 0;
                    // ヒヨ 4 は2体に渡すので、2体目のホタ（火勢4）が焼き尽くして印が灯り直す——灯り直しは指名の後
                    int iCall = ev.FindIndex(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Called);
                    int reMark = ev.Skip(iCall + 1).Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.CallMark);
                    Expect($"指名（{tag}・ヒヨ {hl}・ボルグ {bl}）: 見出し「指名」1件（火勢 {bl}・1体目）・指名で印が消える（ヒヨ 4 は2体目の焼き尽くすで灯り直す）・放つのは火勢4 のときだけ",
                        $"{FLn(ev, FireLevelLabels.Called)}/{called?.Amount}/{called?.Slot}/{bo.RawCounter(FireCycleRule.CallKey)}:{reMark}/{unl}", $"1/{bl}/1/{(hl == 4 ? "1:1" : "0:0")}/{bl == 4}");
                }
        {
            // 印が無ければ指名は出ず、並びは印の札が無いときと同じ
            var ctx = Ctx(Formation.Build(front1: HiyoT(), front3: BorgT(TraitId.RadiateCall), center: HotaT()), Big, out var p, out _, out _);
            SetLv(U(p, "hiyo"), 3); SetLv(U(p, "borg"), 4); SetLv(U(p, "hota"), 4);
            string a = string.Join(",", FireStokeTrait.GiftTargets(ctx, U(p, "hiyo")).Select(u => u.Def.Id));
            var ctx2 = Ctx(Formation.Build(front1: HiyoT(), front3: BorgT(), center: HotaT()), Big, out var p2, out _, out _);
            SetLv(U(p2, "hiyo"), 3); SetLv(U(p2, "borg"), 4); SetLv(U(p2, "hota"), 4);
            string b = string.Join(",", FireStokeTrait.GiftTargets(ctx2, U(p2, "hiyo")).Select(u => u.Def.Id));
            Expect("印が無ければ相手の並びは放熱の札が無いときと同じ", a, b);
        }
        {
            // 燃えていなくなったら印は消える（刻みで燃焼が 0 に）
            var ctx = Ctx(Formation.Build(front1: BorgT(TraitId.RadiateCall)), Formation.Build(front1: Plain("e1")), out var p, out _, out _);
            var bo = U(p, "borg");
            SetLv(bo, 2, burn: 1); bo.SetCounter(FireCycleRule.CallKey, 1);
            int n0 = ctx.Events.Count;
            ctx.TickStatuses();
            Expect("燃焼が切れたら印は消える（見出し「放熱の印・消える」1件）", $"{bo.RawCounter(StatusKeys.Burn)}/{bo.RawCounter(FireCycleRule.CallKey)}/{FLn(Since(ctx, n0), FireLevelLabels.CallLost)}", "0/0/1");
        }

        // ---- 火の粉: 焼き尽くす・放つの1回につき +1（札ごと）----
        foreach (var (tr, tag, wantB, wantU) in new[]
        {
            (Array.Empty<TraitId>(), "札なし（T0）", 0, 0),
            (new[] { TraitId.SparkCatch }, "焼き尽くすだけ（T1-放粉）", 1, 0),
            (new[] { TraitId.SparkCatch, TraitId.SparkUnleash }, "両方（T1）", 1, 1),
            (new[] { TraitId.StokePick, TraitId.HiyoSpark, TraitId.SparkUnleash }, "新育ち ＋ 放つ（T2）", 1, 1),
        })
        {
            // 焼き尽くす: ホタだけに渡す（ボルグは燃えていない＝相手にならない）
            var ctx = Ctx(Formation.Build(front1: HiyoT(tr), front3: BorgT(), center: HotaT()), Big, out var p, out _, out _);
            var hy = U(p, "hiyo"); var bo = U(p, "borg"); var ho = U(p, "hota");
            SetLv(hy, 2); SetLv(ho, 4); bo.SetCounter(StatusKeys.Burn, 0);
            ctx.QueueGift(hy, new[] { ho }, 3);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(bo);   // 控えを流す（燃えていないボルグの手番の後に）
            var ev = Since(ctx, n0);
            int gb = ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.GrowSpark && x.TargetId == hy.InstanceId && x.ActorId == ho.InstanceId);
            // 放つ: ボルグだけに渡す
            var ctx2 = Ctx(Formation.Build(front1: HiyoT(tr), front3: BorgT(), center: HotaT()), Big, out var p2, out _, out _);
            var hy2 = U(p2, "hiyo"); var bo2 = U(p2, "borg");
            SetLv(hy2, 2); SetLv(bo2, 4); U(p2, "hota").SetCounter(StatusKeys.Burn, 0);
            ctx2.QueueGift(hy2, new[] { bo2 }, 3);
            int m0 = ctx2.Events.Count;
            ctx2.TakeTurn(U(p2, "hota"));
            var ev2 = Since(ctx2, m0);
            int gu = ev2.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.GrowSpark && x.TargetId == hy2.InstanceId && x.ActorId == bo2.InstanceId);
            Expect($"火の粉（{tag}）: 焼き尽くす 1回で +{wantB} ／ 放つ 1回で +{wantU}（大技がそれぞれ1回）",
                $"{FLn(ev, FireLevelLabels.Burnout)}:{gb}/{FLn(ev2, FireLevelLabels.Unleash)}:{gu}", $"1:{wantB}/1:{wantU}");
        }
        {
            // 燃えていないヒヨは火の粉で育たない
            var ctx = Ctx(Formation.Build(front1: HiyoT(TraitId.SparkCatch, TraitId.SparkUnleash), front3: BorgT(), center: HotaT()), Big, out var p, out _, out _);
            var hy = U(p, "hiyo"); SetLv(U(p, "hota"), 4); hy.SetCounter(StatusKeys.Burn, 0); U(p, "borg").SetCounter(StatusKeys.Burn, 0);
            ctx.QueueGift(hy, new[] { U(p, "hota") }, 3);
            ctx.TakeTurn(U(p, "borg"));
            Expect("燃えていないヒヨは火の粉で育たない", FireLevelRule.Of(hy), 0);
        }

        // ---- (b): 火勢3 で準備の味方が2体いれば2体に渡す（札があるときだけ・2体のときだけ）----
        foreach (bool pair in new[] { false, true })
            foreach (int ready in new[] { 1, 2 })
            {
                var tr = pair ? new[] { TraitId.GiftPair } : Array.Empty<TraitId>();
                var ctx = Ctx(Formation.Build(front1: HiyoT(tr), front3: BorgT(), center: HotaT(), back1: Plain("mate", atk: 40, tr: TraitId.FireLevel)), Big, out var p, out _, out var cr);
                SetLv(U(p, "hiyo"), 3); SetLv(U(p, "borg"), ready == 2 ? 4 : 3); SetLv(U(p, "hota"), 4); SetLv(U(p, "mate"), 4);
                int n0 = ctx.Events.Count;
                ctx.TakeTurn(U(p, "hiyo"));
                var ev = Since(ctx, n0);
                int want = pair && ready == 2 ? 2 : 1;
                Expect($"(b)（札 {(pair ? "あり" : "なし")}・準備の味方 {ready} 体・火勢4 の相方は準備でない）: 渡した数 ／ 見出し「二体に渡す」",
                    $"{FLn(ev, FireLevelLabels.Gift)}/{FLn(ev, FireLevelLabels.GiftPair)}", $"{want}/{(want == 2 ? 1 : 0)}");
            }

        // ---- 盤面: 版ごとに 3台 × 9波 × 倍率（的は 1）× seed 40 ----
        long battles = 0, verbDiff = 0, detDiff = 0, causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, markBad = 0, calledNot1 = 0, calledNoMark = 0, sparkBad = 0, t0Bad = 0;
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
                            var en = FC.WaveOf(w, sc)();
                            var slotOf = pl.Concat(en).ToDictionary(u => u, u => u.Slot);
                            var r = BattleEngine.Run(pl, en, seed, verbose: true);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: false);
                            var r3 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: true);
                            var agg = new BA.Agg(); agg.Take(r, pl, en, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
                            var fl = r.FireLevels!;
                            // 印の収支: 付いた ＝ 指名 ＋ 燃えず消えた ＋ 戦の終わりに残った（0 か 1）
                            long rest = fl.CallMarks - fl.Called.Sum() - fl.CallLost;
                            bool mb = rest is < 0 or > 1;
                            // 指名は必ず 1体目・指名の直前に印があった
                            int c1 = 0, cnm = 0, sb = 0; bool on = false;
                            int? borg = pl.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId, hiyo = pl.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId;
                            foreach (var x in r.Events)
                            {
                                if (x.Kind != BattleEventKind.FireLevel) continue;
                                if (x.Text == FireLevelLabels.CallMark && x.TargetId == borg) on = true;
                                if (x.Text == FireLevelLabels.CallLost && x.TargetId == borg) on = false;
                                if (x.Text == FireLevelLabels.Called) { if (x.Slot != 1) c1++; if (!on) cnm++; on = false; }
                                // 火の粉は札のある版だけ・焼き尽くすか放つの直後
                                if (x.Text == FireLevelLabels.GrowSpark && x.TargetId == hiyo && !(v.Hiyo.Traits.Contains(TraitId.SparkCatch) || v.Hiyo.Traits.Contains(TraitId.HiyoSpark) || v.Hiyo.Traits.Contains(TraitId.SparkUnleash))) sb++;
                                if (x.Text == FireLevelLabels.GrowSpark && x.TargetId == hiyo && x.ActorId == borg && !v.Hiyo.Traits.Contains(TraitId.SparkUnleash)) sb++;
                            }
                            bool t0b = v.Name == "T0" && (fl.CallMarks + fl.Called.Sum() + fl.CallLost + fl.GiftPairs + fl.SparkBy.Sum()) != 0;
                            lock (lk)
                            {
                                battles++;
                                if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns || r.PlayerSurvivors != r2.PlayerSurvivors
                                    || fl.CallMarks != r2.FireLevels!.CallMarks || fl.Called.Sum() != r2.FireLevels.Called.Sum() || fl.SparkBy.Sum() != r2.FireLevels.SparkBy.Sum()) verbDiff++;
                                if (r.Events.Count != r3.Events.Count || r.Log.Count != r3.Log.Count || r.Turns != r3.Turns) detDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                                if (mb) markBad++;
                                calledNot1 += c1; calledNoMark += cnm; sparkBad += sb; if (t0b) t0Bad++;
                            }
                        });
            }
        Expect($"印の収支（付いた ＝ 指名 ＋ 燃えず消えた ＋ 戦の終わりに残った 0〜1・{battles} 戦）が合わない戦", markBad, 0L);
        Expect("指名は必ず 1体目", calledNot1, 0L);
        Expect("指名の直前に印があった（印の無い指名 0）", calledNoMark, 0L);
        Expect("火の粉は札のある版だけ（放つの火の粉は `SparkUnleash` だけ）", sparkBad, 0L);
        Expect("T0 では印・指名・(b)・火の粉が1度も起きない", t0Bad, 0L);
        Expect("verbose の有無で勝敗・決着T・生存・印・指名・火の粉の数が同じ", verbDiff, 0L);
        Expect("同じ seed を2度回して台本の長さが同じ", detDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 4）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);

        // ---- `compare`: T0 が docs/balance.md と一致 ----
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
        Expect($"T0 の `compare`（{cells} セル）が docs/balance.md と一致", cellBad, 0);
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }
    static int _rollsNoCall;
}
