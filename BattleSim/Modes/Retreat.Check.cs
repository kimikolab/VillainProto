using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// retreat check（第225期） —— 自己検査（受け入れ 3・4・7）
//
// (4) 盤面を直に組んで: 緊急退避は4割を切った味方だけ ／ 回数は段の上限まで（ターンが替われば戻る）／ 後ろ側に相手がいなければ起きない（回数も使わない）／
//     入れ替えで移動の反応が起きる（移り木・セロの段）／ 段は移動の累計で上がる（4/8/14）／ 手当てはシオ自身にも入る（J1）／
//     痺れで止まり転倒では止まらない ／ 反撃の中では出る・割り込みの中では出ない ／ シオが倒れていれば出ない
// (3) verbose の有無で勝敗・決着ターン・与ダメが同じ（J3・J4 × 台 × 波 × 倍率）
// (7) 台本: `Retreat` の欄（下げた駒は4割未満・段・何回目か）と直後の2体の `Move`・`ShioStage` の数が帳簿と一致
// (2) J0 の台本 ＝ 前段（`shockdigest r225` を実装の前後で突き合わせる・ここでは回さない）
// =====================================================================================
static partial class RetreatDiag
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
        foreach (var u in e) { u.MaxHp = 99999; u.Hp = 99999; }
        return ctx;
    }

    static UnitState U(List<UnitState> p, string id) => p.First(u => u.Def.Id == id);
    static string Seat(UnitState u) => FormationRules.SeatNames[u.Slot];
    /// <summary>その駒の HP を「最大HPの pct%」にしてから、4割の線を跨ぐ一撃を1発（敵から）。</summary>
    static void HitTo(BattleContext ctx, UnitState u, UnitState foe, int pctBefore, int pctAfter)
    {
        u.Hp = u.MaxHp * pctBefore / 100;
        int target = u.MaxHp * pctAfter / 100;
        ctx.ApplyDamage(u, u.Hp - target, foe);
    }

    static partial void CheckImpl()
    {
        ok = ng = 0;
        Console.WriteLine("# 第225期 retreat check");
        Console.WriteLine();
        if (J1 is null || J2 is null || J3 is null || J4 is null) { Console.WriteLine("版の札が見つからない（Phase 0 のコミット）。"); return; }
        var knight = EnemyCatalog.Stages[1].Enemy.Occupied().Select(o => o.Def).First(d => d.Traits.Count == 0);
        Formation E1 = Formation.Build(front1: knight);
        // 前1 トウ ／ 前3 ハネ ／ 中央 クビ ／ 後1 シオ ／ 後3 ネル（前1 の後ろ側の隣は 中央（席 2）・前3 も 中央）
        Formation B(UnitDef shio, UnitDef? center = null) => Formation.Build(front1: UnitCatalog.Tou, front3: UnitCatalog.HaneK0,
            center: center ?? UnitCatalog.Kubi, back1: shio, back3: UnitCatalog.Nel);

        Console.WriteLine("## (4) 盤面を直に組んで");
        Console.WriteLine();
        {
            var ctx = Ctx(B(J3), E1, 1, out var p, out var e);
            UnitState tou = U(p, "tou"), hane = U(p, "hane"), shio = U(p, "shio"), nel = U(p, "nel"), foe = e[0];
            SetTurn(ctx, 1);
            HitTo(ctx, tou, foe, 60, 45);
            Expect("J3: 4割を切らない一撃（60% → 45%）では下げない", Seat(tou), "前1");
            HitTo(ctx, tou, foe, 45, 30);
            Expect("J3: 4割を切った一撃（45% → 30%）で下げる（前1 → 中央）", Seat(tou), "中央");
            Expect("J3: 押し出したクビは前1", Seat(U(p, "kubi")), "前1");
            Expect("J3: 下げた回数", Tal(ctx, shio).RetreatSwaps, 1L);
            HitTo(ctx, nel, foe, 50, 20);
            Expect("J3: 後列のネル（後ろ側に相手なし）は下げない", Seat(nel), "後3");
            Expect("J3: 相手なしの回数", Tal(ctx, shio).RetreatNoPartner, 1L);
            HitTo(ctx, hane, foe, 50, 20);
            Expect("J3: 同じターンの2体目（ハネ）は上限 1 で下げない", Seat(hane), "前3");
            Expect("J3: 使い切り", Tal(ctx, shio).RetreatSpent, 1L);
            SetTurn(ctx, 2);
            HitTo(ctx, hane, foe, 30, 20);
            Expect("J3: ターンが替われば下げる（前3 → 中央）", Seat(hane), "中央");
            Expect("J3: 段の札が無いので移動を数えない", ShioStageTrait.MovesOf(shio), 0);
        }
        {
            // 相手なしは回数を使わない: 後列を先に割り、同じターンに前列を割る
            var ctx = Ctx(B(J3), E1, 2, out var p, out var e);
            UnitState tou = U(p, "tou"), nel = U(p, "nel"), foe = e[0];
            SetTurn(ctx, 1);
            HitTo(ctx, nel, foe, 50, 20);
            HitTo(ctx, tou, foe, 50, 20);
            Expect("J3: 相手なしの後でも同じターンに1回下げられる", Seat(tou), "中央");
        }
        {
            // 段: 移動の累計で上がる（1回の緊急退避 ＝ 2 移動）。段1（4 回）で上限 2・移り木 30%。
            var ctx = Ctx(B(J4), E1, 3, out var p, out var e);
            UnitState tou = U(p, "tou"), hane = U(p, "hane"), kubi = U(p, "kubi"), shio = U(p, "shio"), foe = e[0];
            SetTurn(ctx, 1);
            HitTo(ctx, tou, foe, 50, 20);
            Expect("J4: 1回下げた後の移動の累計", ShioStageTrait.MovesOf(shio), 2);
            Expect("J4: 段 0 のまま", ShioStageTrait.StageOf(shio), 0);
            int hk = kubi.Hp;   // 満タン
            ctx.SwapSlots(kubi, hane.Slot, kubi);   // 味方どうしの入れ替え（誰が動かしても数える）
            Expect("J4: 4 回で段 1", ShioStageTrait.StageOf(shio), 1);
            Expect("J4: 段1 に届いたターン", Tal(ctx, shio).ShioStageTurn?[1] ?? 0, 1);
            Expect("J4: 段1 の上限", RetreatTrait.LimitOf(shio), 2);
            Expect("J4: 段1 の移り木（最大HP 56 の 30%）", DrifterTrait.HealOf(shio, hane), 56 * 30 / 100);
            Expect("J4: 段1 の攻撃", DrifterTrait.GainOf(shio), 8);
            // 同じターンにもう1回（段1 の上限 2）
            HitTo(ctx, hane, foe, 50, 20);
            Expect("J4: 段1 は同じターンに2回目も下げる", Tal(ctx, shio).RetreatSwaps, 2L);
            foreach (var (m, st) in new[] { (7, 1), (8, 2), (13, 2), (14, 3), (40, 3) })
            {
                shio.SetCounter(ShioStageTrait.MovesKey, m);
                Expect($"段の条件: 累計 {m} で段", ShioStageTrait.StageOf(shio), st);
            }
            Expect("段3 の上限", RetreatTrait.LimitOf(shio), 4);
            Expect("段3 の移り木（最大HP 56 の 40%）", DrifterTrait.HealOf(shio, hane), 56 * 40 / 100);
            Expect("段3 の攻撃", DrifterTrait.GainOf(shio), 12);
        }
        {
            // 入れ替えで移動の反応: 下げた駒に移り木（J3 ＝ 最大HPの 20%）・前へ出したセロの動かされた回数 +1
            var ctx = Ctx(B(J3, UnitCatalog.SeroL0), E1, 4, out var p, out var e);
            UnitState tou = U(p, "tou"), sero = U(p, "sero"), foe = e[0];
            SetTurn(ctx, 1);
            tou.Hp = tou.MaxHp * 50 / 100;
            int dmg = tou.Hp - tou.MaxHp * 20 / 100;
            ctx.ApplyDamage(tou, dmg, foe);
            int hpAfterHit = tou.MaxHp * 20 / 100;
            Expect("J3: 下げたトウに移り木（最大HPの 20%）", tou.Hp - hpAfterHit, tou.MaxHp * 20 / 100);
            Expect("J3: 前へ出したセロの動かされた回数", EvadeTrait.MovesOf(sero), 1);
            Expect("J3: 移り木の出どころ＝緊急退避（添字 7）の増分", Tal(ctx, U(p, "shio")).DrifterBySrc?[7] ?? 0L, (long)(tou.MaxHp * 20 / 100));
        }
        {
            // 痺れで止まる・転倒では止まらない・倒れていれば出ない
            foreach (var (tag, key, want) in new[] { ("痺れ", StatusKeys.Stun, "前1"), ("転倒", StatusKeys.Stagger, "中央") })
            {
                var ctx = Ctx(B(J3), E1, 5, out var p, out var e);
                UnitState tou = U(p, "tou"), shio = U(p, "shio");
                SetTurn(ctx, 1);
                shio.SetCounter(key, 1);
                HitTo(ctx, tou, e[0], 50, 20);
                Expect($"J3: シオが{tag}のとき", Seat(tou), want);
            }
            {
                var ctx = Ctx(B(J3), E1, 6, out var p, out var e);
                UnitState tou = U(p, "tou"), shio = U(p, "shio");
                SetTurn(ctx, 1);
                shio.Hp = 0;
                HitTo(ctx, tou, e[0], 50, 20);
                Expect("J3: シオが倒れているとき", Seat(tou), "前1");
            }
        }
        {
            // 反撃の中では出る・割り込みの中では出ない
            var ctx = Ctx(B(J3), E1, 7, out var p, out var e);
            UnitState tou = U(p, "tou"), hane = U(p, "hane"), foe = e[0];
            SetTurn(ctx, 1);
            ctx.Reaction(() => HitTo(ctx, tou, foe, 50, 20));
            Expect("J3: 反撃の中の一撃でも下げる", Seat(tou), "中央");
            Expect("J3: 反撃の中で下げた回数", Tal(ctx, U(p, "shio")).RetreatInReaction, 1L);
            SetTurn(ctx, 2);
            ctx.Interrupt(() => HitTo(ctx, hane, foe, 50, 20));
            Expect("J3: 割り込みの中の一撃では下げない", Seat(hane), "前3");
            Expect("J3: 割り込みの中で出せなかった回数", Tal(ctx, U(p, "shio")).RetreatHeld, 1L);
        }
        {
            // 手当て: J1 はシオ自身を下げたときも入る（最大HP 60 の 20% ＝ 12）
            var f = Formation.Build(front1: J1, front3: UnitCatalog.Gald, center: UnitCatalog.Tou, back1: UnitCatalog.Yomi, back3: UnitCatalog.BasaK0);
            var ctx = Ctx(f, E1, 8, out var p, out _);
            UnitState shio = U(p, "shio");
            shio.Hp = shio.MaxHp / 3; int hs = shio.Hp;
            TraitCatalog.Get(TraitId.Regroup).OnAction(ctx, shio, new UnitAction(ActionKind.Skill, Label: "隊を組み替えた"));
            Expect("J1: シオ自身を下げた（前1 → 中央）", Seat(shio), "中央");
            Expect("J1: シオ自身への手当て", shio.Hp - hs, shio.MaxHp * 20 / 100);
        }
        Console.WriteLine();

        Console.WriteLine("## (3) verbose の有無（勝敗・決着ターン・与ダメ）");
        Console.WriteLine();
        var benches = new (string, Formation)[] { ("M-ハネ", PickSeats("M-ハネ", RawHane, false)), ("M-カド", PickSeats("M-カド", RawKado, false)), ("参考", RefGald), ("M-ハネ（仮）", RawHane), ("M-カド（仮）", RawKado) };
        {
            long n = 0, diff = 0;
            foreach (var (_, bf) in benches)
                foreach (var ver in new[] { J3, J4 })
                    for (int s = 0; s < 2; s++) for (int w = 0; w < WaveNames.Length; w++) for (int seed = 0; seed < 50; seed++)
                    {
                        var f = WithShio(bf, ver);
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), WaveOf(w, Scales[s].Sc)(), seed, verbose: true);
                        var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), WaveOf(w, Scales[s].Sc)(), seed, verbose: false);
                        n++;
                        long d1 = r1.TallyByUnit.Sum(kv => kv.Value.DamageToEnemy), d2 = r2.TallyByUnit.Sum(kv => kv.Value.DamageToEnemy);
                        if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns || d1 != d2) diff++;
                    }
            Expect($"verbose の有無で違った戦（{n:N0} 戦）", diff, 0L);
        }
        Console.WriteLine();

        Console.WriteLine("## (7) 台本");
        Console.WriteLine();
        {
            int retreats = 0, badFields = 0, badMoves = 0, badStage = 0, badOrdinal = 0, stageEv = 0, tallyStage = 0;
            long tallySwaps = 0;
            foreach (var (_, bf) in benches.Take(3))
                foreach (var ver in new[] { J3, J4 })
                    for (int s = 0; s < 2; s++) for (int w = 0; w < WaveNames.Length; w++) for (int seed = 0; seed < 50; seed++)
                    {
                        var f = WithShio(bf, ver);
                        var player = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                        var r = BattleEngine.Run(player, WaveOf(w, Scales[s].Sc)(), seed, verbose: true);
                        int shioId = player.First(u => u.Def.Id == "shio").InstanceId;
                        var mine = player.Select(u => u.InstanceId).ToHashSet();
                        bool staged = ver == J4;
                        int stage = 0, lastTurn = -1, ordinal = 0;
                        var ev = r.Events;
                        for (int i = 0; i < ev.Count; i++)
                        {
                            var e = ev[i];
                            if (e.Kind == BattleEventKind.ShioStage) { stageEv++; if (e.Slot <= stage) badStage++; stage = e.Slot; }
                            if (e.Kind != BattleEventKind.Retreat) continue;
                            retreats++;
                            if (e.ActorId != shioId || e.TargetId is null || e.PartnerId is null || e.HpAfter * 100 >= e.Amount * RetreatTrait.Percent) badFields++;
                            if (e.Slot != (staged ? stage : 0)) badStage++;
                            if (e.Turn != lastTurn) { lastTurn = e.Turn; ordinal = 0; }
                            ordinal++;
                            if (e.StatusRemaining != ordinal || ordinal > RetreatTrait.Limits[staged ? stage : 0]) badOrdinal++;
                            var moves = ev.Skip(i + 1).Where(x => x.Kind == BattleEventKind.Move && x.TargetId is int mt && mine.Contains(mt)).Take(2).ToList();
                            if (moves.Count != 2 || moves.Any(m => m.ActorId != shioId) || moves[0].TargetId != e.TargetId || moves[1].TargetId != e.PartnerId) badMoves++;
                        }
                        var t = r.TallyByUnit["shio"];
                        tallySwaps += t.RetreatSwaps;
                        if (t.ShioStageTurn is not null) tallyStage += t.ShioStageTurn.Count(x => x > 0);
                    }
            Expect("`Retreat` の数 ＝ 帳簿の下げた回数", (long)retreats, tallySwaps);
            Expect("`ShioStage` の数 ＝ 帳簿の届いた段の数", stageEv, tallyStage);
            Expect("欄がおかしい `Retreat`（書き手・相手・4割未満）", badFields, 0);
            Expect("段が合わない", badStage, 0);
            Expect("何回目かが合わない／上限を超えた", badOrdinal, 0);
            Expect("直後の2体の `Move` が下げた駒・前へ出した駒でない", badMoves, 0);
            Console.WriteLine($"  （緊急退避 {retreats:N0} 回・段 {stageEv:N0} 回）");
        }
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**" + (ng == 0 ? " ok=True" : " ok=False"));
    }
}
