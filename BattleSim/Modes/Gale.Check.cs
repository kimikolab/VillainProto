using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// gale check（第229期） —— 自己検査（受け入れ 2〜4・7）
//
// (4) 盤面を直に組んで: 嵐の味方の入れ替えが段の表どおり ／ 追い風は保持者が敵を後ろの行へ動かしたときだけ・4割を切った味方は踏み込まない ／
//     転倒した駒は前列に数えない・標的の介入（庇う・挑発・受け流し）をしない ／ 痺れは今のまま（壁のまま）
// (3) verbose の有無で勝敗・決着T・与ダメが同じ（G1〜G4）・新しい処理は乱数を引かない（追い風の入れ替えは占有者 1 体）
// (2) 転倒が1度も付かない戦は G3 で台本が G0 と一致 ／ G0 の台本 ＝ 前段（`shockdigest g229` を実装の前後で突き合わせる・ここでは回さない）
// (7) 台本: Tailwind の直後に2体の Move（動かしたのは保持者）・きっかけの敵は直前に保持者に後ろへ動かされている・StaggerBreach の数 ＝ 帳簿
// =====================================================================================
static partial class GaleDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("BattleContext.TallyOf が見つからない");
    static readonly FieldInfo DisorderF = typeof(BattleContext).GetField("_disorder", BindingFlags.Instance | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("BattleContext._disorder が見つからない");
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static UnitTally Tal(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static int ok, ng;
    static void Expect(string what, object got, object want)
    {
        bool pass = Equals(got, want);
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }
    static void ExpectTrue(string what, bool cond, string detail = "")
    {
        if (cond) ok++; else ng++;
        Console.WriteLine("- " + (cond ? "○" : "**×**") + " " + what + (detail == "" ? "" : "：" + detail));
    }

    static BattleContext Ctx(Formation pl, Formation en, int seed, bool hole, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(seed, false, shuffler: ShufflerRule.Default with { StaggerHole = hole });
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 100, int atk = 5) => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = Array.Empty<TraitId>() };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第229期 gale check");
        Console.WriteLine();
        Console.WriteLine("## (4) 盤面を直に組んで");
        Console.WriteLine();
        UnitDef a = Plain("a"), b = Plain("b"), c = Plain("c"), d = Plain("d"), x = Plain("x");
        UnitDef e1 = Plain("e1"), e2 = Plain("e2"), e3 = Plain("e3"), e4 = Plain("e4"), e5 = Plain("e5");
        var foes5 = Formation.Build(front1: e1, front3: e3, center: e4, back1: e2, back3: e5);

        // ---- 嵐: 段ごとの味方の入れ替え ----
        foreach (var v in new[] { VerOf("G0"), VerOf("G1") })
            for (int stage = 0; stage < 4; stage++)
            {
                int movedSum = 0, runs = 0;
                for (int seed = 0; seed < 30; seed++)
                {
                    var ctx = Ctx(Formation.Build(front1: a, front3: v.Basa, center: b, back1: c, back3: d), foes5, seed, false, out var p, out var e);
                    ((int[])DisorderF.GetValue(ctx)!)[BattleContext.EnemyTeam] = stage == 0 ? 0 : DisarrayTrait.StageAt[stage - 1];
                    var basa = U(p, "basa");
                    var before = p.Where(u => u != basa).ToDictionary(u => u, u => u.Slot);
                    basa.Traits.First(t => t.Id == TraitId.Shuffler).OnTurnStart(ctx, basa);
                    movedSum += before.Count(kv => kv.Key.Slot != kv.Value);
                    runs++;
                }
                int want = v.Tag == "G1" ? GaleTrait.AllySwaps[stage] : 2;
                Expect($"{v.Tag} 段{stage}: 手番の頭に席が変わった味方（30 試行の平均）", movedSum / runs, want);
            }

        // ---- 追い風 ----
        {
            UnitDef basa2 = VerOf("G2").Basa;
            Formation pl = Formation.Build(front1: a, front3: basa2, center: b, back1: c, back3: d);
            Formation en = Formation.Build(front1: e1, front3: e3, center: e4, back1: e2, back3: e5);
            {
                var ctx = Ctx(pl, en, 0, false, out var p, out var e);
                var bs = U(p, "basa");
                ctx.SwapSlots(U(e, "e1"), 3, bs);   // 前1 → 後1（後ろへ）・後1 の e2 は前1 へ
                Expect("敵を前1 → 後1 へ: 経路0 の最後尾 c が中央へ", FormationRules.SeatNames[U(p, "c").Slot], "中央");
                Expect("  1つ前の b は後1 へ", FormationRules.SeatNames[U(p, "b").Slot], "後1");
                Expect("  先頭の a は動かない", FormationRules.SeatNames[U(p, "a").Slot], "前1");
                Expect("  機会 ／ 踏み込み（前へ出た e2 は機会にならない）", $"{Tal(ctx, bs).TailwindTriggers} / {Tal(ctx, bs).TailwindSteps}", "1 / 1");
            }
            {
                var ctx = Ctx(pl, en, 0, false, out var p, out var e);
                var bs = U(p, "basa"); var cu = U(p, "c");
                cu.Hp = cu.MaxHp * TailwindTrait.HpGatePercent / 100 - 1;
                ctx.SwapSlots(U(e, "e1"), 3, bs);
                Expect("最後尾 c が4割未満: c は動かず、次の b が a の前へ（b 前1・a 中央）",
                       $"{FormationRules.SeatNames[cu.Slot]} / {FormationRules.SeatNames[U(p, "b").Slot]} / {FormationRules.SeatNames[U(p, "a").Slot]}", "後1 / 前1 / 中央");
                Expect("  4割で飛ばした", Tal(ctx, bs).TailwindLowHp, 1L);
            }
            {
                var ctx = Ctx(pl, en, 0, false, out var p, out var e);
                var bs = U(p, "basa");
                foreach (var id in new[] { "b", "c" }) { var u = U(p, id); u.Hp = u.MaxHp * TailwindTrait.HpGatePercent / 100 - 1; }
                var before = p.ToDictionary(u => u, u => u.Slot);
                ctx.SwapSlots(U(e, "e1"), 3, bs);
                Expect("b・c とも4割未満: 誰も動かない", p.Count(u => u.Slot != before[u]), 0);
                Expect("  全員4割未満", Tal(ctx, bs).TailwindAllLow, 1L);
            }
            {
                var ctx = Ctx(pl, en, 0, false, out var p, out var e);
                var bs = U(p, "basa");
                var before = p.ToDictionary(u => u, u => u.Slot);
                ctx.SwapSlots(U(e, "e1"), 1, bs);   // 前1 ⇔ 前3（同じ列）
                Expect("同じ列どうしの入れ替え: 機会にならない", Tal(ctx, bs).TailwindTriggers, 0L);
                ctx.SwapSlots(U(e, "e4"), 4, U(p, "a"));   // 保持者でない駒が敵を後ろへ
                Expect("保持者でない駒が動かした: 機会にならない ／ 味方は動かない", $"{Tal(ctx, bs).TailwindTriggers} / {p.Count(u => u.Slot != before[u])}", "0 / 0");
            }
            {
                var ctx = Ctx(Formation.Build(front1: a, front3: UnitCatalog.BasaG0, center: b, back1: c, back3: d), en, 0, false, out var p, out var e);
                var before = p.ToDictionary(u => u, u => u.Slot);
                ctx.SwapSlots(U(e, "e1"), 3, U(p, "basa"));
                Expect("G0 のバサ（札なし）: 味方は動かない", p.Count(u => u.Slot != before[u]), 0);
            }
            {
                var ctx = Ctx(pl, en, 0, false, out var p, out var e);
                var bs = U(p, "basa");
                ctx.SwapSlots(U(e, "e4"), 4, bs);   // 中央（経路0・1）→ 後3
                Expect("中央の敵を後ろへ: 番号の若い経路0 で c が中央へ", FormationRules.SeatNames[U(p, "c").Slot], "中央");
            }
        }

        // ---- 転倒の穴 ----
        {
            Formation pl = Formation.Build(front1: a, front3: x, center: b, back1: c, back3: d);
            foreach (bool hole in new[] { false, true })
                foreach (string how in new[] { "転倒2", "転倒1", "痺れ2" })
                {
                    var ctx = Ctx(pl, foes5, 0, hole, out var p, out var e);
                    string key = how.StartsWith("痺れ") ? StatusKeys.Stun : StatusKeys.Stagger;
                    U(p, "a").SetCounter(key, 1);
                    if (how.EndsWith("2")) U(p, "x").SetCounter(key, 1);
                    string pool = string.Join(",", ctx.TargetPool(U(e, "e1")).Select(u => u.Def.Id).OrderBy(s => s));
                    string want = hole && how == "転倒2" ? "a,b,x" : "a,x";
                    Expect($"穴 {(hole ? "あり" : "なし")}・前列 {how}: 狙える駒", pool, want);
                }
            {
                var ctx = Ctx(pl, foes5, 0, true, out var p, out var e);
                foreach (var id in new[] { "a", "x", "b" }) U(p, id).SetCounter(StatusKeys.Stagger, 1);
                Expect("穴あり・前列も中列も転倒: 後列まで狙える", string.Join(",", ctx.TargetPool(U(e, "e1")).Select(u => u.Def.Id).OrderBy(s => s)), "a,b,c,d,x");
            }
            // 庇う（ガルド）
            foreach (bool hole in new[] { false, true })
                foreach (bool fallen in new[] { false, true })
                {
                    int gald = 0; long skips = 0;
                    for (int seed = 0; seed < 200; seed++)
                    {
                        var ctx = Ctx(Formation.Build(front1: UnitCatalog.Gald, front3: x, center: b, back1: c, back3: d), foes5, seed, hole, out var p, out var e);
                        var g = U(p, "gald");
                        if (fallen) g.SetCounter(StatusKeys.Stagger, 1);
                        if (ctx.SelectTarget(U(e, "e1")) == g) gald++;
                        skips += Tal(ctx, g).HoleSkips;
                    }
                    bool full = !(hole && fallen);
                    ExpectTrue($"庇う: 穴 {(hole ? "あり" : "なし")}・ガルド {(fallen ? "転倒" : "立つ")} → 主目標がガルド {gald}/200", full ? gald == 200 : gald is > 0 and < 200,
                               full ? "" : $"介入の候補から外れた {skips}");
                }
            // 挑発（セロ）
            foreach (bool fallen in new[] { false, true })
            {
                int sero = 0;
                for (int seed = 0; seed < 200; seed++)
                {
                    var ctx = Ctx(Formation.Build(front1: UnitCatalog.Sero, front3: x, center: b, back1: c, back3: d), foes5, seed, true, out var p, out var e);
                    var s = U(p, "sero");
                    if (fallen) s.SetCounter(StatusKeys.Stagger, 1);
                    if (ctx.SelectTarget(U(e, "e1")) == s) sero++;
                }
                ExpectTrue($"挑発: 穴あり・セロ {(fallen ? "転倒" : "立つ")} → 主目標がセロ {sero}/200", fallen ? sero is > 0 and < 200 : sero == 200);
            }
            // 受け流し（ガルド）
            foreach (bool hole in new[] { false, true })
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.Gald, front3: x, center: b, back1: c, back3: d), foes5, 0, hole, out var p, out var e);
                var g = U(p, "gald");
                g.SetCounter(ParryTrait.StockKey, 2);
                g.SetCounter(StatusKeys.Stagger, 1);
                int hp0 = g.Hp;
                ctx.ApplyDamage(g, 10, U(e, "e1"));
                ExpectTrue($"受け流し: 穴 {(hole ? "あり" : "なし")}・転倒したガルドへ 10 → HP {hp0} → {g.Hp}・在庫 {g.RawCounter(ParryTrait.StockKey)}",
                           hole ? g.Hp < hp0 && g.RawCounter(ParryTrait.StockKey) == 2 : g.Hp == hp0 && g.RawCounter(ParryTrait.StockKey) == 1);
            }
        }
        Console.WriteLine();

        // ---- (3)(2)(7) 実戦 ----
        Console.WriteLine("## (3)(2)(7) 実戦");
        Console.WriteLine();
        var benches = new List<(string, Formation)> { ("M-ハネ 228", MHane228), ("M-ハネ 総当たり G4", Formation.Build(front1: UnitCatalog.Shio, front3: UnitCatalog.BasaG0, center: UnitCatalog.Sero, back1: UnitCatalog.Yomi, back3: UnitCatalog.HaneG0)) };
        benches.AddRange(CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id is "basa" or "hane")).Select(r => ("compare " + r.Name, r.F)));
        int[] waves = { 4, 5, 0, 1, 2, 3 };
        long verbMis = 0, verbN = 0, g3Same = 0, g3N = 0, twEv = 0, twTally = 0, twMoveBad = 0, twCauseBad = 0, twHpBad = 0, brEv = 0, brTally = 0;
        var lk = new object();
        foreach (var (bn, bf) in benches)
            foreach (var v in Versions.Skip(1))
                Parallel.ForEach(waves, w =>
                {
                    long vm = 0, vn = 0, gs = 0, gn = 0, te = 0, tt = 0, mb = 0, cb = 0, hb = 0, be = 0, bt = 0;
                    for (int seed = 0; seed < 20; seed++)
                    {
                        var g = Apply(bf, v);
                        var (r1, p1, e1s, s1) = Fight(g, w, Scales[0].Sc, seed, verbose: true, shuffler: v.Rule);
                        var (r2, _, _, _) = Fight(g, w, Scales[0].Sc, seed, verbose: false, shuffler: v.Rule);
                        vn++;
                        if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns || !r1.DamageByUnit.OrderBy(k => k.Key).SequenceEqual(r2.DamageByUnit.OrderBy(k => k.Key))) vm++;
                        // (7)
                        var byId = p1.Concat(e1s).ToDictionary(u => u.InstanceId);
                        var board = new Board(p1.Concat(e1s), s1);
                        var pendingBack = new List<(int Foe, int? Actor)>();   // 保持者に後ろの行へ動かされて、まだ追い風が出ていない敵
                        var evs = r1.Events;
                        for (int i = 0; i < evs.Count; i++)
                        {
                            var ev = evs[i];
                            if (ev.Kind == BattleEventKind.Tailwind)
                            {
                                te++;
                                // 直後の Move は踏み込む駒（動かしたのは保持者）。後ろへ回る駒の Move は、踏み込んだ駒の移動の読み手
                                // （ヨミの軋み・シオの移り木など）の出来事の後に、同じターンのうちに来る。
                                var next = evs.Skip(i + 1).FirstOrDefault(m => m.Kind == BattleEventKind.Move);
                                bool partnerMoved = evs.Skip(i + 1).TakeWhile(m => m.Kind != BattleEventKind.TurnStart)
                                                       .Any(m => m.Kind == BattleEventKind.Move && m.TargetId == ev.PartnerId && m.ActorId == ev.ActorId);
                                if (next is null || next.TargetId != ev.TargetId || next.ActorId != ev.ActorId || !partnerMoved) mb++;
                                int pi = pendingBack.FindIndex(pb => pb.Foe == ev.SpreadFromId && pb.Actor == ev.ActorId);
                                if (pi < 0) cb++; else pendingBack.RemoveAt(pi);
                                if (ev.TargetId is int st && board.Hp[st] * 100 < board.MaxHp[st] * TailwindTrait.HpGatePercent) hb++;
                            }
                            if (ev.Kind == BattleEventKind.StaggerBreach) be++;
                            if (ev.Kind == BattleEventKind.Move && ev.TargetId is int mt && board.Slot.ContainsKey(mt) && board.Team[mt] == BattleContext.EnemyTeam
                                && FormationRules.DepthOf(FormationRules.RowOf(ev.Slot)) > FormationRules.DepthOf(FormationRules.RowOf(board.Slot[mt])))
                                pendingBack.Add((mt, ev.ActorId));
                            board.Apply(ev);
                        }
                        foreach (var (id, t) in r1.TallyByUnit) { tt += t.TailwindSteps + t.TailwindRefused; bt += t.HoleBreaches; }
                        // (2) G3: 転倒が1度も付かない戦は G0 と台本が同じ
                        if (v.Tag == "G3" && !r1.Events.Any(ev => ev.Kind == BattleEventKind.Stagger))
                        {
                            gn++;
                            var (r0, _, _, _) = Fight(Apply(bf, VerOf("G0")), w, Scales[0].Sc, seed, verbose: true, shuffler: VerOf("G0").Rule);
                            if (Fp(r0) == Fp(r1)) gs++;
                        }
                    }
                    lock (lk) { verbMis += vm; verbN += vn; g3Same += gs; g3N += gn; twEv += te; twTally += tt; twMoveBad += mb; twCauseBad += cb; twHpBad += hb; brEv += be; brTally += bt; }
                });
        Expect($"verbose の有無で勝敗・決着T・与ダメが違う戦（G1〜G4 × {benches.Count} 台 × 6 波 × 20 seed ＝ {verbN} 戦）", verbMis, 0L);
        Expect($"G3: 転倒が1度も付かない戦で台本が G0 と違う（{g3N} 戦）", g3N - g3Same, 0L);
        Expect("Tailwind の件数 ＝ 帳簿（踏み込み＋空振り）", twEv, twTally);
        Expect("Tailwind の直後の Move が踏み込む駒でない、または後ろへ回る駒の Move が続かない（動かしたのは保持者）", twMoveBad, 0L);
        Expect("Tailwind のきっかけの敵が、直前に保持者に後ろの行へ動かされていない", twCauseBad, 0L);
        Expect("Tailwind で踏み込んだ駒が4割を切っていた", twHpBad, 0L);
        Expect("StaggerBreach の件数 ＝ 帳簿（越えた）", brEv, brTally);
        Console.WriteLine($"- 参考: Tailwind {twEv} 件 ／ StaggerBreach {brEv} 件");
        Console.WriteLine();
        Console.WriteLine($"**○ {ok} ／ × {ng}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }

    static string Fp(BattleResult r) => string.Join(";", r.Events.Select(e => $"{e.Kind}|{e.ActorId}|{e.TargetId}|{e.Amount}|{e.Slot}|{e.HpAfter}"));
}
