using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// cycle check（第230期） —— 自己検査（受け入れ 2〜4・7）
//
// (4) 盤面を直に組んで: 追い風は「前にいない」味方のうち攻撃力の最も高い駒（同値は後ろの行）・4割未満は除外 ／
//     撃破の衝撃は後ろに敵がいれば吹き飛ばし（席が無ければ転倒だけ）・いなければ勢い余って入れ替え・入れ替えは1ターン2回 ／
//     溢れの半分が攻撃力に・1体1戦 +15 まで
// (3) verbose の有無で勝敗・決着T・倒れた駒が同じ ／ 版の札が働かない戦は台本が1つ前の版と一致（乱数の列が変わらない）
// (2) W0 の台本 ＝ 前段（`shockdigest w230` を実装の前後で突き合わせる・ここでは回さない）
// (7) 台本: KillImpact の数 ＝ 帳簿（吹き飛ばし＋転倒だけ＋勢い余って＋空振り）・勢い余っての直後にヨミの Move ・Overflow の数と量 ＝ 帳簿
// =====================================================================================
static partial class CycleDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("BattleContext.TallyOf が見つからない");
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

    /// <summary>盤面を直に組む。<paramref name="enemySlots"/> を渡せば敵の席を上書きする（○前2 など）。</summary>
    static BattleContext Ctx(Formation pl, Formation en, int seed, out List<UnitState> p, out List<UnitState> e, Dictionary<string, int>? enemySlots = null)
    {
        var ctx = new BattleContext(seed, false);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        if (enemySlots is not null) foreach (var u in e) if (enemySlots.TryGetValue(u.Def.Id, out int s)) u.Slot = s;
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 100, int atk = 5) => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = Array.Empty<TraitId>() };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static string Seat(UnitState u) => FormationRules.SeatNames[u.Slot];

    static string Digest(BattleResult r)
        => string.Join(";", r.Events.Select(e => $"{e.Kind}|{e.Turn}|{e.ActorId}|{e.TargetId}|{e.Amount}|{e.Slot}|{e.HpAfter}|{e.Text}"));

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第230期 cycle check");
        Console.WriteLine();
        Console.WriteLine("## (4) 盤面を直に組んで");
        Console.WriteLine();

        // ---- 追い風の踏み込み先 ----
        {
            UnitDef a = Plain("a", atk: 5), b = Plain("b", atk: 9), c = Plain("c", atk: 3), d = Plain("d");
            UnitDef e1 = Plain("e1"), e2 = Plain("e2"), e3 = Plain("e3"), e4 = Plain("e4"), e5 = Plain("e5");
            var en = Formation.Build(front1: e1, front3: e3, center: e4, back1: e2, back3: e5);
            foreach (var (tag, basa, wantB, wantA, wantC) in new[]
            {
                ("W1", BasaF, "前1", "中央", "後1"),
                ("W0", UnitCatalog.BasaW0, "後1", "前1", "中央"),
            })
            {
                var ctx = Ctx(Formation.Build(front1: a, front3: basa, center: b, back1: c, back3: d), en, 0, out var p, out var e);
                ctx.SwapSlots(U(e, "e1"), 3, U(p, "basa"));   // 敵を前1 → 後1（経路0）
                Expect($"{tag}: 経路0（a 攻5 前1 ／ b 攻9 中央 ／ c 攻3 後1）で b ／ a ／ c の席", $"{Seat(U(p, "b"))} / {Seat(U(p, "a"))} / {Seat(U(p, "c"))}", $"{wantB} / {wantA} / {wantC}");
            }
            {
                var ctx = Ctx(Formation.Build(front1: a, front3: BasaF, center: b, back1: c, back3: d), en, 0, out var p, out var e);
                var bu = U(p, "b"); bu.Hp = bu.MaxHp * TailwindTrait.HpGatePercent / 100 - 1;
                ctx.SwapSlots(U(e, "e1"), 3, U(p, "basa"));
                Expect("W1: 攻撃力最大の b が4割未満 → 次に高い c が b と入れ替わる（c 中央・b 後1）", $"{Seat(U(p, "c"))} / {Seat(bu)}", "中央 / 後1");
            }
            {
                UnitDef b5 = Plain("b", atk: 5), c5 = Plain("c", atk: 5);
                var ctx = Ctx(Formation.Build(front1: a, front3: BasaF, center: b5, back1: c5, back3: d), en, 0, out var p, out var e);
                ctx.SwapSlots(U(e, "e1"), 3, U(p, "basa"));
                Expect("W1: b・c が同じ攻撃力 → 後ろの行の c が踏み込む（c 中央・b 後1）", $"{Seat(U(p, "c"))} / {Seat(U(p, "b"))}", "中央 / 後1");
            }
            {
                var ctx = Ctx(Formation.Build(front1: a, front3: BasaF, center: b, back1: c, back3: d), en, 0, out var p, out var e);
                foreach (var id in new[] { "b", "c" }) { var u = U(p, id); u.Hp = u.MaxHp * TailwindTrait.HpGatePercent / 100 - 1; }
                var before = p.ToDictionary(u => u, u => u.Slot);
                ctx.SwapSlots(U(e, "e1"), 3, U(p, "basa"));
                Expect("W1: 前にいない b・c とも4割未満 → 誰も動かない", p.Count(u => u.Slot != before[u]), 0);
            }
        }

        // ---- 撃破の衝撃 ----
        {
            UnitDef a = Plain("a"), c = Plain("c"), d = Plain("d"), f = Plain("f");
            UnitDef e1 = Plain("e1", hp: 1), e3 = Plain("e3", hp: 1), e7 = Plain("e7", hp: 1), e4 = Plain("e4"), e2 = Plain("e2");
            var pl = Formation.Build(front1: a, front3: c, center: YomiK, back1: d, back3: f);
            {
                var ctx = Ctx(pl, Formation.Build(front1: e1, center: e4, back1: e2), 0, out var p, out var e);
                var y = U(p, "yomi");
                ctx.PerformAttack(y);
                Expect("前1 を倒す（中央に敵）: 中央の敵が ○中1 へ吹き飛ぶ ／ 転ぶ", $"{Seat(U(e, "e4"))} / {U(e, "e4").RawCounter(StatusKeys.Stagger)}", "○中1 / 1");
                Expect("  ヨミは動かない ／ 帳簿（吹き飛ばし・勢い余って）", $"{Seat(y)} / {Tal(ctx, y).ImpactBlow} / {Tal(ctx, y).ImpactTumble}", "中央 / 1 / 0");
            }
            {
                var ctx = Ctx(pl, Formation.Build(front1: e1, back1: e2), 0, out var p, out var e);
                var y = U(p, "yomi");
                ctx.PerformAttack(y);
                Expect("前1 を倒す（後1 だけに敵）: 後1 は席が無いので動かず転ぶ（転倒だけ）", $"{Seat(U(e, "e2"))} / {U(e, "e2").RawCounter(StatusKeys.Stagger)} / {Tal(ctx, y).ImpactStumble}", "後1 / 1 / 1");
            }
            {
                var ctx = Ctx(pl, Formation.Build(front1: e1, back3: e2), 0, out var p, out var e);
                var y = U(p, "yomi");
                ctx.PerformAttack(y);
                Expect("前1 を倒す（経路0 の後ろに敵なし）: 勢い余ってヨミが隣の味方と入れ替わる", $"{Seat(y) != "中央"} / {Tal(ctx, y).ImpactTumble}", "True / 1");
                Expect("  後3 の敵は動かず転ばない", $"{Seat(U(e, "e2"))} / {U(e, "e2").RawCounter(StatusKeys.Stagger)}", "後3 / 0");
            }
            {
                // 3体とも経路の後ろに敵がいない（前1 ・前3 ・○前2）。同じターンに3体倒すと、勢い余っては2回まで。
                var ctx = Ctx(pl, Formation.Build(front1: e1, front3: e3, center: e7), 3, out var p, out var e, new() { ["e7"] = 7 });
                var y = U(p, "yomi");
                int manual = 0;
                for (int i = 0; i < 3 && e.Any(u => u.IsAlive); i++) { ctx.PerformAttack(y); manual++; }
                var t = Tal(ctx, y);
                Expect("同じターンに3体（後ろに敵なし）: 勢い余って ／ 上限で止まった", $"{t.ImpactTumble} / {t.ImpactCapped}", "2 / 1");
                Expect("  ヨミの振り ＝ 手で振った回数 ＋ 1（1回目の入れ替えで軋みの割り込み・2回目の入れ替えは割り込みの中なので再入禁止）", $"{t.CreakSwings} / {manual}", $"{manual + 1} / {manual}");
                Expect("  ○前2 の敵（経路外）も勢い余っての側", e.All(u => !u.IsAlive), true);
            }
            {
                // 札の無いヨミ: 何も起きない
                var ctx = Ctx(Formation.Build(front1: a, front3: c, center: UnitCatalog.YomiW0, back1: d, back3: f), Formation.Build(front1: e1, center: e4, back1: e2), 0, out var p, out var e);
                ctx.PerformAttack(U(p, "yomi"));
                Expect("札の無いヨミ（W0）: 中央の敵は動かない", Seat(U(e, "e4")), "中央");
            }
        }

        // ---- 溢れ ----
        {
            UnitDef x = Plain("x"), a = Plain("a"), c = Plain("c"), d = Plain("d");
            var en = Formation.Build(front1: Plain("e1"), back1: Plain("e2"));
            // 段（味方が動かされた累計）で移り木の量が変わらないよう、段の札を外したシオで確かめる。
            UnitDef NoStage(UnitDef s) => DecoyDiag.Copy(s, s.Traits.Where(t => t != TraitId.ShioStage).ToArray());
            foreach (var (tag, shio) in new[] { ("W3", NoStage(ShioS)), ("W0", NoStage(UnitCatalog.ShioW0)) })
            {
                var ctx = Ctx(Formation.Build(front1: x, front3: a, center: shio, back1: c, back3: d), en, 0, out var p, out var e);
                var xu = U(p, "x"); var au = U(p, "a");
                int b0 = xu.AtkBonus;
                ctx.SwapSlots(xu, au.Slot, au);   // x（満タン）が動かされる → 移り木 20（段0）が全部溢れる
                int b1 = xu.AtkBonus - b0;
                ctx.SwapSlots(xu, au.Slot, au);
                int b2 = xu.AtkBonus - b0;
                ctx.SwapSlots(xu, au.Slot, au);
                int b3 = xu.AtkBonus - b0;
                int g = DrifterTrait.Gain;
                if (tag == "W3")
                {
                    Expect("W3: 満タンの x が動かされる（溢れ 20）→ 攻撃 +移り木 5 ＋ 溢れ 10", b1, g + 10);
                    Expect("  2回目（溢れ 20・上限まで 5）→ 累計 +移り木 10 ＋ 15", b2, 2 * g + 15);
                    Expect("  3回目（上限）→ 移り木の分だけ", b3, 3 * g + 15);
                    Expect("  帳簿（攻撃力にした ／ 上限で捨てた）", $"{Tal(ctx, xu).ShioOverflowGain} / {Tal(ctx, xu).ShioOverflowCapped}", "15 / 15");
                    // 半分だけ溢れる: HP 90 に 20 → 増えた 10・溢れ 10 → +5
                    var cu = U(p, "c"); cu.Hp = 90; int cb = cu.AtkBonus;
                    ctx.SwapSlots(cu, U(p, "d").Slot, U(p, "d"));
                    Expect("  HP 90/100 の c（溢れ 10）→ 攻撃 +移り木 5 ＋ 5", cu.AtkBonus - cb, g + 5);
                }
                else Expect("W0: 溢れは攻撃力にならない（移り木の分だけ）", b3, 3 * g);
            }
        }

        // ---- (3)(2)(7) 実戦 ----
        Console.WriteLine();
        Console.WriteLine("## (3)(7) 実戦（台 × 版 × 波 × seed 0..19）");
        Console.WriteLine();
        var benches = new[] { MHane228, MHane229 };
        int verboseDiff = 0, n = 0, sameW1W2 = 0, sameW1W2N = 0, sameW1W3 = 0, sameW1W3N = 0, sameW0W1 = 0, sameW0W1N = 0;
        long evImpact = 0, tlImpact = 0, evOver = 0, tlOverN = 0, evOverSum = 0, tlOverSum = 0, tumbleMoveOk = 0, tumbleEv = 0;
        foreach (var bf in benches)
            foreach (int w in new[] { 4, 5, 0, 1, 2, 3 })
                for (int seed = 0; seed < 20; seed++)
                {
                    var rs = new Dictionary<string, BattleResult>();
                    foreach (var v in Versions)
                    {
                        var g = Apply(bf, v);
                        var (r1, p1, _, _) = Fight(g, w, Scales[0].Sc, seed, verbose: true);
                        var (r2, _, _, _) = Fight(g, w, Scales[0].Sc, seed, verbose: false);
                        n++;
                        if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns || r1.PlayerStarterFallen.Count != r2.PlayerStarterFallen.Count) verboseDiff++;
                        rs[v.Tag] = r1;
                        var ty = r1.TallyByUnit.GetValueOrDefault("yomi");
                        if (ty is not null) tlImpact += ty.ImpactBlow + ty.ImpactStumble + ty.ImpactTumble + ty.ImpactRefused;
                        evImpact += r1.Events.Count(x => x.Kind == BattleEventKind.KillImpact);
                        foreach (var (id, t) in r1.TallyByUnit) if (p1.Any(u => u.Def.Id == id)) { tlOverSum += t.ShioOverflowGain; }
                        var ovs = r1.Events.Where(x => x.Kind == BattleEventKind.Overflow).ToList();
                        evOver += ovs.Count; evOverSum += ovs.Sum(x => x.Amount);
                        for (int i = 0; i < r1.Events.Count; i++)
                        {
                            var ev = r1.Events[i];
                            if (ev.Kind != BattleEventKind.KillImpact || ev.Text != ImpactLabels.Tumble) continue;
                            tumbleEv++;
                            var next = r1.Events.Skip(i + 1).FirstOrDefault(x => x.Kind == BattleEventKind.Move);
                            if (next is not null && next.TargetId == ev.ActorId) tumbleMoveOk++;
                        }
                    }
                    var yw2 = rs["W2"].TallyByUnit.GetValueOrDefault("yomi");
                    if (yw2 is null || yw2.ImpactKills == 0) { sameW1W2N++; if (Digest(rs["W1"]) == Digest(rs["W2"])) sameW1W2++; }
                    if (!rs["W3"].TallyByUnit.Values.Any(t => t.ShioOverflowGain > 0)) { sameW1W3N++; if (Digest(rs["W1"]) == Digest(rs["W3"])) sameW1W3++; }
                    if (!rs["W1"].Events.Any(x => x.Kind == BattleEventKind.Tailwind)) { sameW0W1N++; if (Digest(rs["W0"]) == Digest(rs["W1"])) sameW0W1++; }
                }
        Expect($"verbose の有無で勝敗・決着T・倒れた数が違った戦（{n} 戦）", verboseDiff, 0);
        Expect($"W2 でヨミが攻撃の中で1体も倒さなかった戦は台本が W1 と一致（{sameW1W2N} 戦）", sameW1W2, sameW1W2N);
        Expect($"W3 で溢れが攻撃力に1度もならなかった戦は台本が W1 と一致（{sameW1W3N} 戦）", sameW1W3, sameW1W3N);
        Expect($"W1 で追い風が1度も起きなかった戦は台本が W0 と一致（{sameW0W1N} 戦）", sameW0W1, sameW0W1N);
        Expect("台本の KillImpact ＝ 帳簿（吹き飛ばし＋転倒だけ＋勢い余って＋空振り）", evImpact, tlImpact);
        Expect("勢い余っての直後の Move はヨミ", tumbleMoveOk, tumbleEv);
        Expect("台本の Overflow の量 ＝ 帳簿（攻撃力にした量）", evOverSum, tlOverSum);
        Console.WriteLine();
        Console.WriteLine($"**○ {ok} ／ × {ng}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
