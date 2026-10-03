using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;
using FK = FireKindleDiag;

// giftorder check —— 自己検査（受け入れ 1・2・4）。N0 の台本の突き合わせは `giftorder digest N0` を実装の前の `firekindle digest` と cmp で見る。
static partial class GiftOrderDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static readonly FieldInfo RngF = typeof(BattleContext).GetField("_rng", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly FieldInfo QueueF = typeof(BattleContext).GetField("_giftQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;
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
    static BattleContext Ctx(Formation pl, out List<UnitState> p)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(Formation.Build(front1: Plain("e1")), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id) => new() { Id = id, Name = id, MaxHp = 1000, Attack = 10, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static void SetLv(UnitState u, int lv) { u.SetCounter(StatusKeys.Burn, 3); u.SetCounter(FireLevelRule.LvKey, lv); }
    /// <summary>控えた相手の並び（駒の Id）。</summary>
    static string Queue(BattleContext ctx)
    {
        var q = (System.Collections.IEnumerable)QueueF.GetValue(ctx)!;
        var ids = new List<string>();
        foreach (var item in q) ids.Add(((UnitState)item.GetType().GetField("Item2")!.GetValue(item)!).Def.Id);
        return string.Join(",", ids);
    }

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第253期 giftorder check");
        Console.WriteLine();

        // ---- 受け入れ 2: 渡す順は2体のときだけ・放つの持ち主（ボルグ）が含まれるときだけボルグ先 ／ 1体のときは変わらない ----
        foreach (var (hiyo, name, card) in new[] { (HiyoN1, "N1", false), (HiyoN2, "N2", true) })
        {
            (string Q, int Lbl, long Sw, long Pairs, long Already, int Rng) Go(Func<List<UnitState>, UnitState[]> to)
            {
                var ctx = Ctx(Formation.Build(front1: hiyo, center: BorgN1, back1: UnitCatalog.Hota, back3: Plain("a1")), out var p);
                var hy = U(p, "hiyo");
                foreach (var u in p) SetLv(u, 3);
                SetLv(hy, 4);
                var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
                int n0 = ctx.Events.Count;
                ctx.QueueGift(hy, to(p), 4);
                int lbl = ctx.Events.Skip(n0).Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.GiftOrder);
                return (Queue(ctx), lbl, ctx.FireBook.OrderSwapped, ctx.FireBook.OrderPairs, ctx.FireBook.OrderAlready, cr.N);
            }
            var a = Go(p => new[] { U(p, "hota"), U(p, "borg") });
            Expect($"{name}: 2体（ホタ, ボルグ）→ 控えの並び ／ 見出し ／ 並べ替え ／ 機会 ／ 元から ／ 乱数", $"{a.Q}/{a.Lbl}/{a.Sw}/{a.Pairs}/{a.Already}/{a.Rng}", card ? "borg,hota/1/1/1/0/0" : "hota,borg/0/0/1/0/0");
            var b = Go(p => new[] { U(p, "borg"), U(p, "hota") });
            Expect($"{name}: 2体（ボルグ, ホタ）→ そのまま", $"{b.Q}/{b.Lbl}/{b.Sw}/{b.Pairs}/{b.Already}", "borg,hota/0/0/1/1");
            var c = Go(p => new[] { U(p, "hota"), U(p, "a1") });
            Expect($"{name}: 2体（ホタ, ボルグ以外）→ そのまま・機会に数えない", $"{c.Q}/{c.Lbl}/{c.Sw}/{c.Pairs}", "hota,a1/0/0/0");
            var d = Go(p => new[] { U(p, "hota") });
            Expect($"{name}: 1体（ホタ）→ そのまま", $"{d.Q}/{d.Lbl}/{d.Sw}/{d.Pairs}", "hota/0/0/0");
            var e = Go(p => new[] { U(p, "borg") });
            Expect($"{name}: 1体（ボルグ）→ そのまま", $"{e.Q}/{e.Lbl}/{e.Sw}/{e.Pairs}", "borg/0/0/0");
        }
        // 渡す火の上げは両方の相手に・手番の前（控えた時点で両方 4）
        {
            var ctx = Ctx(Formation.Build(front1: HiyoN2, center: BorgN1, back1: UnitCatalog.Hota), out var p);
            var hy = U(p, "hiyo"); var bo = U(p, "borg"); var h = U(p, "hota");
            SetLv(hy, 4); SetLv(bo, 3); SetLv(h, 2); hy.SetCounter(FireKindleRule.GiftHoardKey, 2);
            ctx.QueueGift(hy, new[] { h, bo }, 4);
            Expect("N2: 渡す火（溜め 2）は並べ替えの後も両方に・控えた時点で（ボルグ 3→4 ／ ホタ 2→4 ／ 並び）", $"{FireLevelRule.Of(bo)}/{FireLevelRule.Of(h)}/{Queue(ctx)}", "4/4/borg,hota");
        }

        // ---- 台で回す: N1 と N2 の突き合わせ（並べ替えが1度も起きない戦は台本ごと一致）・ボルグ先・死因の合計（受け入れ 4）・verbose の有無 ----
        long battles = 0, noSwap = 0, sameBad = 0, swapped = 0, firstBad = 0, verbDiff = 0, causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, swapLbl = 0;
        var lk = new object();
        var n1 = VerOf("N1"); var n2 = VerOf("N2");
        foreach (var bf in new Func<Formation>[] { () => FC.T3244, () => FC.T3238, () => FC.ThunderBorg })
            foreach (int w in new[] { 0, 1, 2, 3, BA.MainWave, BA.MainWave + 1, FC.WaveHeavy })
                for (int s = 0; s < BA.Scales.Length; s++)
                {
                    var f1 = FK.Apply(bf(), n1); var f2 = FK.Apply(bf(), n2); var sc = BA.Scales[s].Sc;
                    Parallel.For(0, 40, seed =>
                    {
                        var pl = BattleEngine.Materialize(f2, BattleContext.PlayerTeam); var en = FC.WaveOf(w, sc)();
                        var slotOf = pl.Concat(en).ToDictionary(u => u, u => u.Slot);
                        var r = BattleEngine.Run(pl, en, seed, verbose: true);
                        var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
                        var rq = BattleEngine.Run(BattleEngine.Materialize(f2, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: false);
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f1, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: true);
                        var agg = new BA.Agg(); agg.Take(r, pl, en, slot0);
                        var fl = r.FireLevels!;
                        int lbl = r.Events.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.GiftOrder);
                        // ボルグを含む2体のギフトでは、1体目の「ターンギフト」がボルグ（ボルグが生きていれば）
                        int fb = 0;
                        var o = new OAgg(); o.Take(r, pl, en, false);
                        fb = (int)(o.Gifts2Borg - o.BorgFirst);
                        bool same = r.Turns == r1.Turns && r.PlayerWon == r1.PlayerWon && r.Events.Count == r1.Events.Count && r.Log.Count == r1.Log.Count
                                    && r.Log.Select(l => l.Text).SequenceEqual(r1.Log.Select(l => l.Text));
                        lock (lk)
                        {
                            battles++;
                            if (fl.OrderSwapped == 0) { noSwap++; if (!same) sameBad++; } else swapped++;
                            firstBad += fb;
                            swapLbl += lbl - fl.OrderSwapped;
                            if (r.PlayerWon != rq.PlayerWon || r.Turns != rq.Turns || r.PlayerSurvivors != rq.PlayerSurvivors) verbDiff++;
                            causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                        }
                    });
                }
        Expect($"台で回す（{battles} 戦・並べ替えが起きた戦 {swapped}）: 並べ替えが1度も起きない N2 の戦は N1 と台本（ログ全文）が一致（{noSwap} 戦）", sameBad, 0L);
        Expect("台で回す: ボルグを含む2体のギフトで、ボルグが1体目でなかった回", firstBad, 0L);
        Expect("台で回す: 「先に渡す」の見出しの数 ＝ 帳簿の並べ替え", swapLbl, 0L);
        Expect("verbose の有無で勝敗・決着T・生存が同じ", verbDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 4）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);

        // ---- 版の駒・札 ----
        var n0 = VerOf("N0");
        Expect("N0 の駒 ＝ 第251期の規定（BorgM0 / Hota / HiyoM0・参照）", ReferenceEquals(n0.Borg, UnitCatalog.BorgM0) && ReferenceEquals(n0.Hota, UnitCatalog.Hota) && ReferenceEquals(n0.Hiyo, UnitCatalog.HiyoM0), true);
        Expect("N1 ＝ ref から放熱で育つ（B3）を抜いた札（並びは問わない）",
            BorgN1.Traits.OrderBy(t => t).SequenceEqual(BorgRef.Traits.Where(t => t != TraitId.RadiateGrow).OrderBy(t => t)) && HiyoN1.Traits.SequenceEqual(FK.HiyoH1.Traits), true);
        Expect("渡す順の札は `TraitCatalog` に登録されている", TraitCatalog.Get(TraitId.GiftOrder).Id, TraitId.GiftOrder);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
        Console.WriteLine($"GIFTORDER_CHECK ok={ok} ng={ng}");
    }
}
