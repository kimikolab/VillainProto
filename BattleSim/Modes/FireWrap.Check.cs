using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;

// firewrap check —— 自己検査（受け入れ 2）: 指名はボルグが火勢4 のときだけ・未満なら印は残る・指名でギフトを受けたら消える。
// 盤面を直に組んでヒヨの手番を1回ずつ回す（第247期の `firetri check` と同じ作法）。
static partial class FireWrapDiag
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
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out CountingRandom cr)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
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
    static UnitDef HotaT() => new()
    {
        Id = "hota", Name = "hota", MaxHp = 1000, Attack = 6, Speed = 1,
        Traits = new[] { TraitId.Pyre, TraitId.PyreStage, TraitId.FireLevel, TraitId.PyreBurnout, TraitId.PyreEmbers, TraitId.CallFire, TraitId.PyreLance, TraitId.PyreCritical },
    };
    static UnitDef BorgT(params TraitId[] tr) => Plain("borg", atk: 18, pat: AttackPattern.Sweep,
        tr: new[] { TraitId.FireLevel, TraitId.FireUnleash, TraitId.FireSpreadCap, TraitId.FoeFireLevel }.Concat(tr).ToArray());
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static void SetLv(UnitState u, int lv, int burn = 3) { u.SetCounter(StatusKeys.Burn, burn); u.SetCounter(FireLevelRule.LvKey, lv); }
    static int FLn(List<BattleEvent> ev, string label) => ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == label);
    static Formation Big => Formation.Build(front1: Plain("e1", hp: 100000), front3: Plain("e3", hp: 100000), center: Plain("ec", hp: 100000));

    static partial void CheckImpl()
    {
        ok = ng = 0;
        Console.WriteLine("# 第248期 firewrap check");
        Console.WriteLine();
        Expect("札 CallFull は Nominated の外では読まれない（印が無ければ Nominated は偽）", FireStokeTrait.Nominated(BattleEngine.Materialize(Formation.Build(front1: BorgT(TraitId.RadiateCall, TraitId.CallFull)), BattleContext.PlayerTeam)[0]), false);

        // ---- 指名の直し: ヒヨ 3（1体）／ 4（2体）× ボルグの火勢 1・2・3・4 × 札（U1 ／ U2）----
        foreach (bool full in new[] { false, true })
            foreach (int hl in new[] { 3, 4 })
                foreach (int bl in new[] { 1, 2, 3, 4 })
                {
                    var borg = full ? BorgT(TraitId.RadiateCall, TraitId.CallFull) : BorgT(TraitId.RadiateCall);
                    // 相方は火勢4 の強い駒（ボルグが指名されなければ相手選びで先に来る）
                    var ctx = Ctx(Formation.Build(front1: HiyoT(), front3: borg, center: HotaT(), back1: Plain("mate", atk: 40, tr: TraitId.FireLevel)), Big, out var p, out var cr);
                    var hy = U(p, "hiyo"); var bo = U(p, "borg");
                    SetLv(hy, hl); SetLv(bo, bl); SetLv(U(p, "hota"), 4); SetLv(U(p, "mate"), 4);
                    bo.SetCounter(FireCycleRule.CallKey, 1);
                    int r0 = cr.N;
                    bool nominated = !full || bl == FireLevelRule.Max;
                    string first = FireStokeTrait.GiftTargets(ctx, hy).First().Def.Id;
                    Expect($"{(full ? "U2" : "U1")}・ヒヨ {hl}・ボルグ {bl}: 並びの先頭（{(nominated ? "ボルグ" : "指名しない")}）・並べても乱数を引かない",
                        $"{(first == "borg")}/{cr.N - r0}", $"{nominated}/0");
                    var n0 = ctx.Events.Count; long held0 = ctx.FireBook.CallHeld, hg0 = ctx.FireBook.CallHeldGift;
                    var recips = FireStokeTrait.GiftTargets(ctx, hy).Take(hl == 4 ? 2 : 1).Select(u => u.Def.Id).ToList();
                    bool borgGets = recips.Contains("borg");
                    ctx.QueueGift(hy, FireStokeTrait.GiftTargets(ctx, hy).Take(hl == 4 ? 2 : 1).ToList(), hl);
                    var ev = ctx.Events.Skip(n0).ToList();
                    // 指名なら印は消える。指名でなければ（ボルグがギフトを受けても受けなくても）印は残る
                    int markAfter = bo.RawCounter(FireCycleRule.CallKey);
                    Expect($"{(full ? "U2" : "U1")}・ヒヨ {hl}・ボルグ {bl}: 見出し「指名」／ 印 ／ 印のまま受けた（ボルグ {(borgGets ? "受けた" : "受けない")}）",
                        $"{FLn(ev, FireLevelLabels.Called)}/{markAfter}/{ctx.FireBook.CallHeldGift - hg0}",
                        nominated ? "1/0/0" : $"0/1/{(borgGets ? 1 : 0)}");
                    // 手番を通しても同じ（ヒヨの OnAction 経由）。指名しなかった手番の計数
                    var ctx2 = Ctx(Formation.Build(front1: HiyoT(), front3: borg, center: HotaT(), back1: Plain("mate", atk: 40, tr: TraitId.FireLevel)), Big, out var p2, out _);
                    SetLv(U(p2, "hiyo"), hl); SetLv(U(p2, "borg"), bl); SetLv(U(p2, "hota"), 4); SetLv(U(p2, "mate"), 4);
                    U(p2, "borg").SetCounter(FireCycleRule.CallKey, 1);
                    int m0 = ctx2.Events.Count;
                    ctx2.TakeTurn(U(p2, "hiyo"));
                    var ev2 = ctx2.Events.Skip(m0).ToList();
                    int iCall = ev2.FindIndex(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Called);
                    bool unl = FLn(ev2, FireLevelLabels.Unleash) > 0;
                    Expect($"{(full ? "U2" : "U1")}・ヒヨ {hl}・ボルグ {bl}（手番）: 指名 ／ 指名しなかった手番の計数 ／ 指名なら放つのは火勢4 のときだけ",
                        $"{(iCall >= 0)}/{ctx2.FireBook.CallHeld}/{(iCall >= 0 ? unl : bl == 4 && unl)}",
                        $"{nominated}/{(nominated ? 0 : 1)}/{bl == 4}");
                }

        // ---- 印のまま（指名でなく）ギフトを受ける: 相方なし・ヒヨ 4（2体）・ホタ 4・ボルグ 3 → 2体目にボルグ ----
        {
            var ctx = Ctx(Formation.Build(front1: HiyoT(), front3: BorgT(TraitId.RadiateCall, TraitId.CallFull), center: HotaT()), Big, out var p, out _);
            SetLv(U(p, "hiyo"), 4); SetLv(U(p, "borg"), 3); SetLv(U(p, "hota"), 4);
            U(p, "borg").SetCounter(FireCycleRule.CallKey, 1);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(U(p, "hiyo"));
            var ev = ctx.Events.Skip(n0).ToList();
            int gifts = ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Gift && x.TargetId == U(p, "borg").InstanceId);
            Expect("U2・ヒヨ 4・ボルグ 3（2体目に受ける）: ボルグへのギフト ／ 指名 ／ 印のまま受けた ／ 受けた後も印が残る（その後の焼き尽くすは重ねない）",
                $"{gifts}/{FLn(ev, FireLevelLabels.Called)}/{ctx.FireBook.CallHeldGift}/{U(p, "borg").RawCounter(FireCycleRule.CallKey)}", "1/0/1/1");
        }

        // ---- 規定の札（規定化の後に意味を持つ）----
        bool adopted = UnitCatalog.Borg.Traits.Contains(TraitId.CallFull);
        Console.WriteLine();
        Console.WriteLine($"規定化: {(adopted ? "済み" : "まだ")}");
        if (adopted)
        {
            Expect("規定のボルグ ＝ U2 のボルグの札", string.Join(",", UnitCatalog.Borg.Traits), string.Join(",", BorgU2.Traits));
            Expect("規定のヒヨ ＝ U1 ／ U2 のヒヨの札", string.Join(",", UnitCatalog.Hiyo.Traits), string.Join(",", HiyoU1.Traits));
            Expect("第247期の器具 T0 ／ 第246期の Q0 ／ 第245期の E0 は旧のボルグ・ヒヨ",
                FireTriDiag.VerOf("T0").Borg == UnitCatalog.BorgU0 && FireTriDiag.VerOf("T0").Hiyo == UnitCatalog.HiyoU0
                && FC.VerOf("Q0").Borg == UnitCatalog.BorgU0 && FC.VerOf("Q0").Hiyo == UnitCatalog.HiyoU0 && EnemyFireDiag.VerOf("E0").Hiyo == UnitCatalog.HiyoU0, true);
        }

        // ---- 戦闘を回して: U2 では指名の火勢は全部 4・印の収支・verbose に依らない ----
        long miss = 0, calls = 0, verboseDiff = 0, n = 0;
        foreach (var (bn, bf) in Boards.Take(2))
            for (int w = 0; w < FC.WaveNames.Length; w++)
                for (int seed = 0; seed < 20; seed++)
                {
                    var f = Apply(bf(), VerOf("U2"));
                    var sc = IsTarget(w) ? EnemyScaleRule.None : BA.Scales[1].Sc;
                    var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, FC.WaveOf(w, sc)(), seed, verbose: true);
                    var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: false);
                    if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns) verboseDiff++;
                    foreach (var x in r.Events)
                        if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Called) { calls++; if (x.Amount < FireLevelRule.Max) miss++; }
                    n++;
                }
        Expect($"U2 の戦闘（{n} 戦）: 指名 {calls} 件のうち火勢4 未満", miss, 0L);
        Expect("U2 の戦闘: verbose の有無で勝敗・決着T が違う戦", verboseDiff, 0L);
        Console.WriteLine();
        Console.WriteLine($"FIREWRAP_CHECK ok={ok} ng={ng}");
    }
}
