using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// fireatk check —— 自己検査（受け入れ 2〜5）。L0 の台本の突き合わせは `fireatk digest` を前段だけのコードと cmp で見る。
static partial class FireAtkDiag
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
    static List<BattleEvent> Since(BattleContext ctx, int n0) => ctx.Events.Skip(n0).ToList();
    static int Lbl(List<BattleEvent> ev, BattleEventKind k, string label) => ev.Count(x => x.Kind == k && x.Text == label);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第250期 fireatk check");
        Console.WriteLine();

        // ---- A1 あぶれた火: 火勢4 のホタへの育ちだけ・1回 +4・火勢は 4 のまま・乱数を引かない ----
        foreach (var (hota, name, card) in new[] { (UnitCatalog.HotaK4, "L0", false), (HotaA1, "L-A1", true), (HotaA2, "L-A2", false) })
            foreach (int lv in new[] { 3, 4 })
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.Hiyo, back3: hota), Formation.Build(front1: Plain("e1")), out var p, out _);
                var hy = U(p, "hiyo"); var h = U(p, "hota");
                SetLv(hy, 1); SetLv(h, lv);
                var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
                int a0 = h.AtkBonus, n0 = ctx.Events.Count;
                ctx.Stoke(hy, h);
                var ev = Since(ctx, n0);
                int want = card && lv == 4 ? FireFeedRule.OverflowAtk : 0;
                Expect($"{name}: 火勢{lv} のホタを煽る → 攻撃力の増え ／ 火勢 ／ あぶれた火の見出し ／ 乱数", $"{h.AtkBonus - a0}/{FireLevelRule.Of(h)}/{Lbl(ev, BattleEventKind.FireLevel, FireLevelLabels.Overflow)}/{cr.N}", $"{want}/4/{(want > 0 ? 1 : 0)}/0");
            }
        // 燃えていないホタ・ヒヨ（段の札を持たない）への育ちでは起きない
        {
            var ctx = Ctx(Formation.Build(front1: Plus(UnitCatalog.Hiyo, TraitId.PyreOverflow), back3: HotaA1), Formation.Build(front1: Plain("e1")), out var p, out _);
            var hy = U(p, "hiyo"); var h = U(p, "hota");
            SetLv(hy, 4); SetLv(h, 2);
            int ha0 = hy.AtkBonus;
            ctx.Stoke(hy, h);   // 煽った本人（ヒヨ・火勢4）も育つ
            Expect("A1: 段の札を持たない駒（札だけ持たせたヒヨ）は火勢4 で育っても攻撃力が上がらない", hy.AtkBonus - ha0, 0);
        }

        // ---- A2 くべられる火: 燃えていたホタにホタ以外の味方が点けたときだけ・1回 +2・火勢は上げない ----
        foreach (var (hota, name, card) in new[] { (UnitCatalog.HotaK4, "L0", false), (HotaA2, "L-A2", true), (HotaA1, "L-A1", false) })
        {
            var ctx = Ctx(Formation.Build(front1: hota, center: UnitCatalog.BorgK4, back3: UnitCatalog.Beni), Formation.Build(front1: Plain("e1")), out var p, out var e);
            var h = U(p, "hota"); var bo = U(p, "borg"); var be = U(p, "beni"); var e1 = U(e, "e1");
            var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
            int a0 = h.AtkBonus;
            ctx.Ignite(h, friendly: true, source: bo);                    // 燃えていない → 点く（くべられる火ではない）
            int afterLit = h.AtkBonus - a0;
            h.SetCounter(FireLevelRule.LvKey, 2);
            ctx.Ignite(h, friendly: true, source: bo);                    // 燃えている・ボルグ → +2
            ctx.Ignite(h, friendly: true, source: be);                    // 燃えている・ベニ → +2
            int afterAllies = h.AtkBonus - a0;
            ctx.Ignite(h, friendly: true, source: h);                     // 自分 → 0
            ctx.Ignite(h, source: e1);                                    // 敵 → 0
            ctx.Ignite(h, friendly: true);                                // 出どころなし → 0
            int w = card ? FireFeedRule.FedAtk : 0;
            Expect($"{name}: 点く ／ 味方2回の点け直し ／ 自分・敵・出どころなし の後 の攻撃力の増え ／ 火勢 ／ 乱数", $"{afterLit}/{afterAllies}/{h.AtkBonus - a0}/{FireLevelRule.Of(h)}/{cr.N}", $"0/{2 * w}/{2 * w}/2/0");
            Expect($"{name}: くべられる火の見出し ／ 帳簿", $"{ctx.Events.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Fed)}/{ctx.FireBook.FedN}", card ? "2/2" : "0/0");
        }

        // ---- 上乗せは大技で火勢が 1 に戻っても残る（焼き尽くすの前後）・焼き尽くす・重は全体の1発 ×7 ----
        foreach (var (hota, name) in new[] { (UnitCatalog.HotaK4, "L0"), (HotaL1, "L1"), (HotaL2, "L2") })
        {
            var ctx = Ctx(Formation.Build(front1: hota), Formation.Build(front1: Plain("e1", hp: 50000), front3: Plain("e2", hp: 50000)), out var p, out _);
            var h = U(p, "hota"); SetLv(h, 4);
            h.AtkBonus += 10;   // あぶれた火・くべられる火で積んだ分の代わり
            int bonus0 = h.AtkBonus;
            GiftF.SetValue(ctx, h);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(h);
            GiftF.SetValue(ctx, null);
            var ev = Since(ctx, n0);
            var blast = ev.First(x => x.Kind == BattleEventKind.Attack && x.ActorId == h.InstanceId);
            int mult = name == "L2" ? FireCycleRule.HeavyMultiplier : 4;
            Expect($"{name}: 焼き尽くすの全体の1発（攻撃力 (6+10) × {mult}）／ 撃った後の火勢 ≤ 2 ／ 上乗せは残る（減らない）",
                $"{blast.Amount}/{FireLevelRule.Of(h) <= 2}/{h.AtkBonus >= bonus0}", $"{(6 + 10) * mult}/True/True");
            Expect($"{name}: 火の雨は 10 発", ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Rain), 10);
        }
        // 上乗せの分だけ火の雨の1発も上がる（×1.5 は攻撃力に掛かる）
        {
            var ctx = Ctx(Formation.Build(front1: HotaL2), Formation.Build(front1: Plain("e1", hp: 50000)), out var p, out _);
            var h = U(p, "hota"); SetLv(h, 4); h.AtkBonus += 10;
            GiftF.SetValue(ctx, h);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(h);
            var atks = Since(ctx, n0).Where(x => x.Kind == BattleEventKind.Attack && x.ActorId == h.InstanceId).Select(x => x.Amount).ToList();
            Expect("L2: 全体 ／ 火の雨の1発（(6+10) × 7 ／ × 1.5）", $"{atks[0]}/{atks[1]}", $"{16 * 7}/{16 * 3 / 2}");
        }

        // ---- 爆炎・独り: ヒヨが盤面にいないときだけ・自分の手番で火勢4 ----
        foreach (var (borg, name, hiyoState) in new[] { (UnitCatalog.BorgK4, "L0・ヒヨなし", 0), (BorgL3, "L3・ヒヨなし", 0), (BorgL3, "L3・ヒヨが生きている", 1), (BorgL3, "L3・ヒヨが倒れた", 2) })
            foreach (int lv in new[] { 3, 4 })
            {
                var pl = hiyoState == 0 ? Formation.Build(front1: Plain("a1", hp: 500), center: borg, back3: UnitCatalog.HotaK4)
                                        : Formation.Build(front1: Plain("a1", hp: 500), center: borg, back3: UnitCatalog.HotaK4, back1: UnitCatalog.Hiyo);
                var ctx = Ctx(pl, Formation.Build(front1: Plain("e1", hp: 5000), front3: Plain("e2", hp: 5000)), out var p, out _);
                var bo = U(p, "borg"); var a1 = U(p, "a1");
                if (hiyoState == 2) U(p, "hiyo").Hp = 0;
                SetLv(bo, lv);
                var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
                int ah = a1.Hp, n0 = ctx.Events.Count;
                ctx.TakeTurn(bo);
                var ev = Since(ctx, n0);
                bool fire = name.StartsWith("L3") && hiyoState != 1 && lv == 4;
                int solo = Lbl(ev, BattleEventKind.FireLevel, FireLevelLabels.BlazeSolo), blaze = Lbl(ev, BattleEventKind.FireLevel, FireLevelLabels.Blaze);
                var bAtk = ev.FirstOrDefault(x => x.Kind == BattleEventKind.Attack && x.ActorId == bo.InstanceId);
                Expect($"{name}・火勢{lv}: 独りの見出し ／ 爆炎の見出し ／ ボルグの攻撃の型 ／ 素の味方が焼かれた", $"{solo}/{blaze}/{bAtk?.Pattern}/{a1.Hp < ah}",
                    fire ? $"1/1/{AttackPattern.All}/True" : $"0/0/{AttackPattern.Sweep}/{(a1.Hp < ah)}");
                if (fire)
                {
                    // 乱数: 同じ盤でギフトの手番の爆炎（L0 の札・`_giftTurnActor` を立てる）と同じ回数＝札の口は乱数を足していない
                    var ctx2 = Ctx(pl, Formation.Build(front1: Plain("e1", hp: 5000), front3: Plain("e2", hp: 5000)), out var p2, out _);
                    var bo2 = U(p2, "borg");
                    if (hiyoState == 2) U(p2, "hiyo").Hp = 0;
                    SetLv(bo2, lv);
                    var cr2 = new CountingRandom(0); RngF.SetValue(ctx2, cr2);
                    GiftF.SetValue(ctx2, bo2);
                    ctx2.TakeTurn(bo2);
                    GiftF.SetValue(ctx2, null);
                    Expect($"{name}: 独りの爆炎の帳簿（回 ／ 受けた の名目 ＝ ボルグの攻撃力 ×1）／ 乱数 ＝ ギフトの手番の爆炎の乱数",
                        $"{ctx.FireBook.BlazeSolos}/{ctx.FireBook.BlazeSoloNom[3] == ev.First(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Blaze).Amount}/{cr.N}", $"1/True/{cr2.N}");
                    Expect($"{name}: ギフトの手番の爆炎は独りに数えない", ctx2.FireBook.BlazeSolos, 0L);
                }
            }

        // ---- 台で回す: verbose の有無・決定性・死因の合計 ＝ 落ちた駒・見出しの数 ＝ 帳簿 ----
        long battles = 0, verbDiff = 0, detDiff = 0, causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, evBad = 0, overN = 0, fedN = 0, soloN = 0, soloBad = 0;
        var lk = new object();
        foreach (var v in Versions)
            foreach (var bf in new Func<Formation>[] { () => FC.T3244, () => FC.T3238, () => FC.ThunderBorg })
                foreach (int w in new[] { 0, 2, 3, BA.MainWave, BA.MainWave + 1, FC.WaveHeavy })
                    for (int s = 0; s < BA.Scales.Length; s++)
                    {
                        var f = Apply(bf(), v); var sc = BA.Scales[s].Sc;
                        Parallel.For(0, 40, seed =>
                        {
                            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam); var en = FC.WaveOf(w, sc)();
                            var slot0 = pl.Concat(en).ToDictionary(u => u, u => u.Slot);
                            var r = BattleEngine.Run(pl, en, seed, verbose: true);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: false);
                            var r3 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: true);
                            var agg = new BA.Agg(); agg.Take(r, pl, en, slot0.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
                            var fl = r.FireLevels!;
                            int eo = r.Events.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Overflow);
                            int ef = r.Events.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Fed);
                            int es = r.Events.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.BlazeSolo);
                            int bad = (eo != fl.OverflowN ? 1 : 0) + (ef != fl.FedN ? 1 : 0) + (es != fl.BlazeSolos ? 1 : 0)
                                + (fl.OverflowAtk != fl.OverflowN * FireFeedRule.OverflowAtk ? 1 : 0) + (fl.FedAtk != fl.FedN * FireFeedRule.FedAtk ? 1 : 0)
                                + (!v.Hota.Traits.Contains(TraitId.PyreOverflow) && fl.OverflowN > 0 ? 1 : 0) + (!v.Hota.Traits.Contains(TraitId.PyreFed) && fl.FedN > 0 ? 1 : 0)
                                + (!v.Borg.Traits.Contains(TraitId.BlazeSolo) && fl.BlazeSolos > 0 ? 1 : 0);
                            // 独りの爆炎はヒヨが倒れた後（またはいない台）だけ
                            int? hiyo = pl.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId;
                            int sb = 0;
                            if (hiyo is int hi)
                            {
                                int dead = r.Events.Select((x, i) => (x, i)).FirstOrDefault(t => t.x.Kind == BattleEventKind.Death && t.x.TargetId == hi).i;
                                bool died = r.Events.Any(x => x.Kind == BattleEventKind.Death && x.TargetId == hi);
                                for (int i = 0; i < r.Events.Count; i++)
                                    if (r.Events[i].Kind == BattleEventKind.FireLevel && r.Events[i].Text == FireLevelLabels.BlazeSolo && (!died || i < dead)) sb++;
                            }
                            lock (lk)
                            {
                                battles++;
                                if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns || r.PlayerSurvivors != r2.PlayerSurvivors) verbDiff++;
                                if (r.Events.Count != r3.Events.Count || r.Log.Count != r3.Log.Count || r.Turns != r3.Turns) detDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                                evBad += bad; overN += eo; fedN += ef; soloN += es; soloBad += sb;
                            }
                        });
                    }
        Expect($"台で回す（{battles} 戦）: 見出しの数 ＝ 帳簿・上乗せ ＝ 回 × 4 ／ × 2・札の無い版では 0（あぶれた火 {overN}・くべられる火 {fedN}・独り {soloN}）", evBad, 0L);
        Expect("台で回す: 独りの爆炎はヒヨが生きている間は出ない", soloBad, 0L);
        Expect("verbose の有無で勝敗・決着T・生存が同じ", verbDiff, 0L);
        Expect("同じ seed を2度回して台本の長さが同じ", detDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 4）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);

        // ---- L3 と L2: ヒヨが落ちなかった戦は台本が一致（独りの爆炎はヒヨが倒れた後にしか起きない）----
        {
            long same = 0, diff = 0, fell = 0, soloInFell = 0;
            var l2 = VerOf("L2"); var l3 = VerOf("L3");
            foreach (var bf in new Func<Formation>[] { () => FC.T3244, () => FC.T3238 })
                foreach (int w in new[] { 0, 1, 2, 3, BA.MainWave, BA.MainWave + 1, FC.WaveHeavy })
                    for (int s = 0; s < BA.Scales.Length; s++)
                        for (int seed = 0; seed < 40; seed++)
                        {
                            var (ra, _, _) = FC.Fight(Apply(bf(), l2), w, BA.Scales[s].Sc, seed);
                            var (rb, _, _) = FC.Fight(Apply(bf(), l3), w, BA.Scales[s].Sc, seed);
                            if (ra.PlayerStarterFallen.Contains("hiyo")) { fell++; soloInFell += rb.FireLevels!.BlazeSolos; continue; }
                            bool eq = ra.Events.Count == rb.Events.Count && ra.Log.Select(l => l.Text).SequenceEqual(rb.Log.Select(l => l.Text));
                            if (eq) same++; else diff++;
                        }
            Expect($"L3 と L2: ヒヨが落ちなかった戦の台本が一致（一致 {same} 戦・ヒヨが落ちた {fell} 戦でのうち独りの爆炎 {soloInFell} 回）", diff, 0L);
        }

        // ---- L0 ＝ 第250期までの規定（第251期に L3 を規定にしたので旧の駒に固定）・L3 ＝ 今の規定 ----
        var l0 = VerOf("L0");
        Expect("L0 の駒 ＝ 第250期までの規定の駒（BorgK4 / HotaK4 / Hiyo・参照）", ReferenceEquals(l0.Borg, UnitCatalog.BorgK4) && ReferenceEquals(l0.Hota, UnitCatalog.HotaK4) && ReferenceEquals(l0.Hiyo, UnitCatalog.Hiyo), true);
        Expect("旧の駒は第249期 K4（札の数: ボルグ ／ ホタ ／ ヒヨ）", $"{UnitCatalog.BorgK4.Traits.Count}/{UnitCatalog.HotaK4.Traits.Count}/{UnitCatalog.Hiyo.Traits.Count}",
            $"{UnitCatalog.BorgK0.Traits.Count + 1}/{UnitCatalog.HotaK0.Traits.Count + 2}/{UnitCatalog.HiyoK0.Traits.Count + 1}");
        var l3v = VerOf("L3");
        Expect("第251期: 規定の札 ＝ L3 の札（並びまで・ボルグ ／ ホタ ／ ヒヨ）", string.Join(",", UnitCatalog.Borg.Traits) == string.Join(",", l3v.Borg.Traits)
            && string.Join(",", UnitCatalog.Hota.Traits) == string.Join(",", l3v.Hota.Traits) && ReferenceEquals(l3v.Hiyo, UnitCatalog.Hiyo), true);
        Expect("新しい札の保持者（`UnitCatalog.All`・規定のボルグ ／ ホタの2枚だけ）", string.Join(",", UnitCatalog.All.Where(d => d.Traits.Any(t => t is TraitId.PyreOverflow or TraitId.PyreFed or TraitId.BurnoutHeavy or TraitId.BlazeSolo)).Select(d => d.Id)), "borg,hota");

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
        Console.WriteLine($"FIREATK_CHECK ok={ok} ng={ng}");
    }
}
