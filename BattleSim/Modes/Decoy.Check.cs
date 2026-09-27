using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// decoy check（第226期） —— 自己検査（受け入れ 3・4・7）
//
// (4) 盤面を直に組んで: 挑発は単体攻撃にだけ・セロが狙える列のときだけ・相手陣営の攻撃だけ ／ 既存の介入（庇う・標・棘守り）が挑発より優先 ／
//     回避率が段の表どおり（40/45/50/60）／ バサがいる間はどの理由でも前に出た敵が混乱（上限まで・バサが倒れたら立たない）／
//     転倒が動けない敵に数えられる（札の保持者がいる戦だけ）／ 敵の乱れの段が累計で上がる（4/8/14）／ バサの入れ替えが段で増える ／
//     ハネが段2 から2体を突き返す ／ 突風は1ターン2回まで（ターンが替われば戻る）／ シオの遅い段は 8/16/26
// (3) verbose の有無で勝敗・決着ターン・与ダメが同じ（K1〜K4s × 台 × 波 × 倍率）
// (7) 台本: `Decoy` の直後の Attack の主目標はセロ・`Disarray` の相手は前へ出て混乱・`DisarrayStage` と `Squall` の数が帳簿と一致
// (2) K0 の台本 ＝ 前段（`shockdigest k226` を実装の前後で突き合わせる・ここでは回さない）
// =====================================================================================
static partial class DecoyDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("BattleContext.TallyOf が見つからない");
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static UnitTally Tal(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static void SetTurn(BattleContext c, int t) => TurnP.SetValue(c, t);
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
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        return ctx;
    }
    static UnitState U(List<UnitState> p, string id) => p.First(u => u.Def.Id == id);
    static string Seat(UnitState u) => FormationRules.SeatNames[u.Slot];

    static partial void CheckImpl()
    {
        ok = ng = 0;
        Console.WriteLine("# 第226期 decoy check");
        Console.WriteLine();
        if (Versions.Length < 7) { Console.WriteLine("版の札が見つからない（Phase 0 のコミット）。"); return; }
        Ver k1 = VerOf("K1"), k2 = VerOf("K2"), k4 = VerOf("K4"), k3s = VerOf("K3s");
        var recruit = EnemyCatalog.TestStages[0].Enemy.Occupied().Select(o => o.Def).First();
        var sweeper = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)).First(d => d.Pattern == AttackPattern.Sweep);

        Console.WriteLine("## (4) 盤面を直に組んで");
        Console.WriteLine();
        // ---- 挑発 ----
        {
            // 前1 セロ（回避盾）／ 前3 ハネ ／ 中央 ヨミ ／ 後1 シオ ／ 後3 バサ
            var ctx = Ctx(Formation.Build(front1: k1.Sero, front3: UnitCatalog.Hane, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Basa),
                          Formation.Build(front1: recruit, front3: sweeper), 3, out var p, out var e);
            UnitState sero = U(p, "sero"), foe = e[0], sw = e[1];
            int hits = 0;
            for (int i = 0; i < 50; i++) if (ctx.SelectTarget(foe) == sero) hits++;
            Expect("挑発: セロが前列にいれば敵の単体攻撃は 50 回とも セロへ", hits, 50);
            int swHits = 0;
            for (int i = 0; i < 200; i++) if (ctx.SelectTarget(sw) == sero) swHits++;
            Expect("挑発: 薙ぎの主目標は変えない（200 回のうちセロ以外も出る）", swHits < 200, true);
            Expect("挑発: 味方（混乱していない）の攻撃の的は敵のまま", ctx.SelectTarget(U(p, "hane"))?.TeamId, BattleContext.EnemyTeam);
            // セロを後列へ（前列にハネが残る）→ 挑発は働かない
            ctx.SwapSlots(sero, 3);
            int back = 0;
            for (int i = 0; i < 50; i++) if (ctx.SelectTarget(foe) == sero) back++;
            Expect("挑発: セロが後列（前列に味方が残る）なら的にならない", back, 0);
            Expect("挑発: 引きつけた回数（前列の 50 回 ＋ 薙ぎは数えない）", Tal(ctx, sero).DecoyDrew, 50L);
        }
        {
            // 既存の介入が優先: 前1 セロ ／ 前3 ガルド（庇う・前列）→ 挑発した一撃を庇いが引き剥がす
            var ctx = Ctx(Formation.Build(front1: k1.Sero, front3: UnitCatalog.Gald, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Basa),
                          Formation.Build(front1: recruit), 5, out var p, out var e);
            UnitState sero = U(p, "sero"), gald = U(p, "gald");
            int toGald = 0;
            for (int i = 0; i < 50; i++) if (ctx.SelectTarget(e[0]) == gald) toGald++;
            Expect("介入の優先: 庇う（ガルド・100%）が挑発より先 → 50 回ともガルド", toGald, 50);
            Expect("介入の優先: 引き剥がされた回数", Tal(ctx, sero).DecoyStolen, 50L);
            Expect("介入の優先: 引きつけた回数 0", Tal(ctx, sero).DecoyDrew, 0L);
        }
        {
            // 標（味方の標・75%）が挑発より先: ハネに標を付ける
            var ctx = Ctx(Formation.Build(front1: k1.Sero, front3: UnitCatalog.Hane, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Basa),
                          Formation.Build(front1: recruit), 7, out var p, out var e);
            UnitState sero = U(p, "sero"), hane = U(p, "hane");
            hane.SetCounter(StatusKeys.Marked, 1);
            int toHane = 0;
            for (int i = 0; i < 400; i++) if (ctx.SelectTarget(e[0]) == hane) toHane++;
            Expect("介入の優先: 標の味方が挑発を上書きする（400 回のうち 75% 前後 ＝ 250〜350）", toHane is >= 250 and <= 350, true);
        }
        // ---- 回避率 ----
        {
            var ctx = Ctx(Formation.Build(front1: k1.Sero), Formation.Build(front1: recruit), 1, out var p, out _);
            UnitState sero = U(p, "sero");
            var got = new List<int>();
            foreach (int m in new[] { 0, 2, 4, 7 }) { sero.SetCounter(EvadeTrait.MovesKey, m); got.Add(EvadeTrait.PercentOf(sero)); }
            Expect("回避率: 段 0/1/2/3 で 40/45/50/60", string.Join("/", got), "40/45/50/60");
            var ctx0 = Ctx(Formation.Build(front1: UnitCatalog.Sero), Formation.Build(front1: recruit), 1, out var p0, out _);
            UnitState s0 = U(p0, "sero");
            var got0 = new List<int>();
            foreach (int m in new[] { 0, 2, 4, 7 }) { s0.SetCounter(EvadeTrait.MovesKey, m); got0.Add(EvadeTrait.PercentOf(s0)); }
            Expect("回避率: 規定のセロは 30/30/30/45 のまま", string.Join("/", got0), "30/30/30/45");
        }
        // ---- 敵の乱れ ----
        {
            // 味方: 前1 ハネ（乱れ）／ 前3 セロ ／ 中央 ヨミ ／ 後1 シオ ／ 後3 バサ（乱れ）。敵: 5 体（前1・前3・中央・後1・後3）
            var ctx = Ctx(Formation.Build(front1: k2.Hane, front3: UnitCatalog.Sero, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: k2.Basa),
                          Formation.Build(front1: recruit, front3: recruit, center: recruit, back1: recruit, back3: recruit), 11, out var p, out var e);
            UnitState basa = U(p, "basa"), hane = U(p, "hane");
            SetTurn(ctx, 1);
            UnitState f0 = e.First(u => u.Slot == 0), b3 = e.First(u => u.Slot == 3);
            // セロ（敵の回避の入れ替えを模して）ではなく「どの理由でも」: 味方ではない出どころ null の入れ替えで後1 の敵を前1 へ
            ctx.SwapSlots(b3, 0, null);
            Expect("混乱: 出どころ null の入れ替えで前へ出た敵（後1 → 前1）が混乱", b3.RawCounter(StatusKeys.Confused), 1);
            Expect("混乱: 後ろへ下がった敵は混乱しない", f0.RawCounter(StatusKeys.Confused), 0);
            Expect("段: 敵が動かされた累計 2 → 段0", DisarrayTrait.StageOf(ctx, basa), 0);
            Expect("混乱: バサ以外で前へ（帳簿）", Tal(ctx, basa).DisarrayConfusesOther, 1L);
            // 上限: 段0 は3回まで
            for (int i = 0; i < 6; i++)
            {
                var back = e.Where(u => u.IsAlive && FormationRules.RowOf(u.Slot) == Row.Back && u.RawCounter(StatusKeys.Confused) == 0).ToList();
                var front = e.Where(u => u.IsAlive && FormationRules.RowOf(u.Slot) == Row.Front).ToList();
                if (back.Count == 0 || front.Count == 0) break;
                ctx.SwapSlots(back[0], front[0].Slot, hane);
            }
            long conf = Tal(ctx, basa).DisarrayConfuses, capped = Tal(ctx, basa).DisarrayCapped;
            int stage = DisarrayTrait.StageOf(ctx, basa);
            int cap = DisarrayTrait.ConfuseCap[stage];
            Expect($"上限: 段 {stage} の上限 {cap}（なし ＝ 0）を超えない", cap == 0 || conf <= cap, true);
            Expect("段: 累計 4 で段1・8 で段2・14 で段3", string.Join("/", new[] { 3, 4, 8, 13, 14 }.Select(DisarrayTrait.StageOfCount)), "0/1/2/2/3");
            Expect("段: ハネにもバサと同じ段（相手陣営の累計）", DisarrayTrait.StageOf(ctx, hane), DisarrayTrait.StageOf(ctx, basa));
            Expect("段: 保持者に届いたターン（段1）", Tal(ctx, basa).DisarrayStageTurn?[1] ?? 0, 1);
            // バサを倒す → 前へ出ても混乱しない
            basa.Hp = 0;
            var e2 = e.Where(u => u.IsAlive && u.RawCounter(StatusKeys.Confused) == 0).ToList();
            var bk = e2.FirstOrDefault(u => FormationRules.RowOf(u.Slot) != Row.Front);
            var fr = e.FirstOrDefault(u => u.IsAlive && FormationRules.RowOf(u.Slot) == Row.Front && u != bk);
            if (bk is not null && fr is not null)
            {
                long before = Tal(ctx, basa).DisarrayConfuses;
                ctx.SwapSlots(bk, fr.Slot, hane);
                Expect("混乱: バサが倒れていれば前へ出ても混乱しない", Tal(ctx, basa).DisarrayConfuses, before);
            }
            // 転倒 → 動けない敵
            UnitState t = e.First(u => u.IsAlive);
            t.SetCounter(StatusKeys.Stagger, 1);
            Expect("動けない敵: 乱れの札の保持者がいる戦では転倒も数える", TormentTrait.IsBound(ctx, t), true);
            var ctx0 = Ctx(Formation.Build(front1: UnitCatalog.Hane, back3: UnitCatalog.Basa), Formation.Build(front1: recruit), 1, out _, out var e0);
            SetTurn(ctx0, 1);   // ターン 0 のままだと IdleTurn の既定値 0 が「今のターン」に一致してしまう
            e0[0].SetCounter(StatusKeys.Stagger, 1);
            Expect("動けない敵: 札が無ければ転倒は数えない（今のまま）", TormentTrait.IsBound(ctx0, e0[0]), false);
        }
        {
            // バサの入れ替えの体数（段で 2/3/3/4）とハネの2体: 段を直に上げて1手番
            var ctx = Ctx(Formation.Build(front1: k2.Hane, front3: UnitCatalog.Sero, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: k2.Basa),
                          Formation.Build(front1: recruit, front3: recruit, center: recruit, back1: recruit, back3: recruit), 13, out var p, out var e);
            UnitState basa = U(p, "basa"), hane = U(p, "hane");
            SetTurn(ctx, 1);
            var disorder = typeof(BattleContext).GetField("_disorder", BindingFlags.Instance | BindingFlags.NonPublic)!;
            int[] arr = (int[])disorder.GetValue(ctx)!;
            arr[BattleContext.EnemyTeam] = 14;
            var slots0 = e.ToDictionary(u => u, u => u.Slot);
            long swaps0 = Tal(ctx, basa).ShuffleFoeSwaps;
            TraitCatalog.Get(TraitId.Shuffler).OnTurnStart(ctx, basa);
            int movedFoes = e.Count(u => u.Slot != slots0[u]);
            Expect("入れ替え: 段3 はバサが敵を4体動かす", movedFoes, 4);
            arr[BattleContext.EnemyTeam] = 8;
            long pushes0 = Tal(ctx, hane).ReboundThrusts;
            TraitCatalog.Get(TraitId.Rebound).OnAction(ctx, hane, new UnitAction(ActionKind.Skill, Label: "突き返す"));
            Expect("突き返し: 段2 のハネは2体を突き返す", Tal(ctx, hane).ReboundThrusts - pushes0, 2L);
            Expect("突き返し: 2体目の帳簿", Tal(ctx, hane).DisarrayPushTwo, 1L);
        }
        // ---- 突風 ----
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Hane, front3: UnitCatalog.Sero, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: k4.Basa),
                          Formation.Build(front1: recruit, front3: recruit, center: recruit), 17, out var p, out var e);
            foreach (var u in e) { u.MaxHp = 99999; u.Hp = 99999; }
            UnitState basa = U(p, "basa"), sero = U(p, "sero");
            SetTurn(ctx, 1);
            for (int i = 0; i < 4; i++) ctx.SwapSlots(basa, i % 2 == 0 ? 1 : 4, sero);
            Expect("突風: 1ターンに4回動かされても2回まで", Tal(ctx, basa).SquallFires, 2L);
            Expect("突風: 使い切った回数", Tal(ctx, basa).SquallCapped, 2L);
            SetTurn(ctx, 2);
            ctx.SwapSlots(basa, 1, sero);
            Expect("突風: ターンが替われば戻る", Tal(ctx, basa).SquallFires, 3L);
        }
        // ---- シオの遅い段 ----
        {
            var ctx = Ctx(Formation.Build(front1: k3s.Shio), Formation.Build(front1: recruit), 1, out var p, out _);
            UnitState shio = U(p, "shio");
            var got = new List<int>();
            foreach (int m in new[] { 7, 8, 15, 16, 25, 26 }) { shio.SetCounter(ShioStageTrait.MovesKey, m); got.Add(ShioStageTrait.StageOf(shio)); }
            Expect("シオの遅い段: 7/8/15/16/25/26 回で 0/1/1/2/2/3", string.Join("/", got), "0/1/1/2/2/3");
        }
        Console.WriteLine();

        // ---- (3) verbose の有無 ----
        Console.WriteLine("## (3) verbose の有無で勝敗・決着ターン・与ダメが同じ");
        Console.WriteLine();
        {
            var benches = new[] { MHane225, Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Basa, center: UnitCatalog.Hane, back1: UnitCatalog.Yomi, back3: UnitCatalog.Shio) };
            int n = 0, diff = 0;
            var jobs = new List<(Formation F, int W, EnemyScaleRule Sc, int S)>();
            foreach (var v in Versions.Skip(1))
                foreach (var b in benches)
                    foreach (int w in new[] { 4, 5, 0, 1, 2, 3 })
                        foreach (var (_, sc) in Scales)
                            for (int s = 0; s < 20; s++) jobs.Add((Apply(b, v), w, sc, s));
            var res = new bool[jobs.Count];
            Parallel.For(0, jobs.Count, i =>
            {
                var (f, w, sc, s) = jobs[i];
                var a = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), WaveOf(w, sc)(), s, verbose: true);
                var b = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), WaveOf(w, sc)(), s, verbose: false);
                long da = a.TallyByUnit.Values.Sum(t => t.DamageToEnemy), db = b.TallyByUnit.Values.Sum(t => t.DamageToEnemy);
                res[i] = a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && da == db;
            });
            n = jobs.Count; diff = res.Count(x => !x);
            Expect($"verbose の有無の差（{n} 戦）", diff, 0);
        }
        Console.WriteLine();

        // ---- (7) 台本 ----
        Console.WriteLine("## (7) 台本");
        Console.WriteLine();
        {
            long decoyEv = 0, decoyOk = 0, drew = 0, disEv = 0, disOk = 0, disTally = 0, stEv = 0, stTally = 0, sqEv = 0, sqTally = 0, disOther = 0;
            var f = Apply(Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Basa, center: UnitCatalog.Hane, back1: UnitCatalog.Yomi, back3: UnitCatalog.Shio), k4);
            foreach (int w in new[] { 4, 0, 3 })
                for (int s = 0; s < 100; s++)
                {
                    var (r, p, e, slot0) = Fight(f, w, Scales[0].Sc, s);
                    int sero = U(p, "sero").InstanceId, basa = U(p, "basa").InstanceId;
                    var tr = new Tracker(slot0, p);
                    var lastFrom = new Dictionary<int, int>();
                    var evs = r.Events;
                    for (int i = 0; i < evs.Count; i++)
                    {
                        var ev = evs[i];
                        if (ev.Kind == BattleEventKind.Decoy)
                        {
                            decoyEv++;
                            var next = evs.Skip(i + 1).FirstOrDefault(x => x.Kind == BattleEventKind.Attack);
                            if (next is not null && next.ActorId == ev.TargetId && next.TargetId == sero && next.Pattern == AttackPattern.Single && ev.ActorId == sero) decoyOk++;
                        }
                        if (ev.Kind == BattleEventKind.Disarray)
                        {
                            disEv++;
                            // 直前の Move（同じ駒）で行が前に変わっている（Tracker は出来事の後に進めるので、ここでは Move の後の席）
                            int id = ev.TargetId!.Value;
                            if (lastFrom.TryGetValue(id, out int from) && ev.PartnerId == basa
                                && FormationRules.DepthOf(FormationRules.RowOf(tr.Slot[id])) < FormationRules.DepthOf(FormationRules.RowOf(from))) disOk++;
                            if (ev.ActorId != basa) disOther++;
                        }
                        if (ev.Kind == BattleEventKind.DisarrayStage) stEv++;
                        if (ev.Kind == BattleEventKind.Squall) sqEv++;
                        if (ev.Kind == BattleEventKind.Move && ev.TargetId is int mv) lastFrom[mv] = tr.Slot[mv];
                        tr.Apply(ev);
                    }
                    foreach (var (id, t) in r.TallyByUnit)
                    {
                        if (id == "sero") drew += t.DecoyDrew;
                        if (id == "basa") { disTally += t.DisarrayConfuses; sqTally += t.SquallFires; }
                        if (id is "basa" or "hane" && t.DisarrayStageTurn is not null) stTally += t.DisarrayStageTurn.Count(x => x > 0);
                    }
                }
            Expect("Decoy の直後の Attack はその敵の単体・主目標はセロ", decoyOk, decoyEv);
            Expect("Decoy の数 ＝ 帳簿（引きつけた）", decoyEv, drew);
            Expect("Disarray の相手は直前の Move で前へ出た・PartnerId はバサ", disOk, disEv);
            Expect("Disarray の数 ＝ 帳簿（混乱）", disEv, disTally);
            Expect("Disarray のうちバサ以外が動かしたものがある", disOther > 0, true);
            Expect("DisarrayStage の数 ＝ 帳簿（段に届いた数・バサとハネ）", stEv, stTally);
            Expect("Squall の数 ＝ 帳簿（突風）", sqEv, sqTally);
            Console.WriteLine($"  （Decoy {decoyEv} ／ Disarray {disEv}（うちバサ以外 {disOther}）／ DisarrayStage {stEv} ／ Squall {sqEv}）");
        }
        Console.WriteLine();
        Console.WriteLine($"**○ {ok} ／ × {ng}**");
    }

}
