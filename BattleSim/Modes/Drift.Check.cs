using System.Reflection;
using BattleCore;
using static Common;

// drift check —— 自己検査（第222期・受け入れ 2・3・6）。
static partial class DriftDiag
{
    static int ok, ng;
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
        ?? throw new InvalidOperationException("BattleContext.Add が見つからない");

    static void Expect<T>(string what, T got, T want)
    {
        bool pass = EqualityComparer<T>.Default.Equals(got, want);
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }

    static BattleContext Ctx(Formation pl, out List<UnitState> p)
    {
        var ctx = new BattleContext(0, false);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        var knight = EnemyCatalog.KnightG;   // 第306期: 第二波の騎士に斬り返しが付いたので、特性の無い巡礼騎士を名指しで引く（第305期までの `Stages[1]` の特性なしの1体目と同じ物）
        var e = BattleEngine.Materialize(Formation.Build(front1: knight, front3: knight), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        return ctx;
    }

    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("BattleContext.TallyOf が見つからない");
    static UnitTally Tally(BattleContext ctx, UnitState u) => (UnitTally)TallyOfM.Invoke(ctx, new object[] { u })!;

    static UnitState U(List<UnitState> p, string id) => p.First(u => u.Def.Id == id);
    static string Seat(UnitState u) => FormationRules.SeatNames[u.Slot];
    static void Act(BattleContext ctx, UnitState shio)
        => TraitCatalog.Get(TraitId.Regroup).OnAction(ctx, shio, new UnitAction(ActionKind.Skill, Label: RegroupLabel));

    static partial void CheckImpl()
    {
        ok = ng = 0;
        Console.WriteLine("# 第222期 drift check");
        Console.WriteLine();

        // ---- (3) シオの入れ替え ----
        Console.WriteLine("## (3) シオ: 盤面を直に組んで1手番");
        Console.WriteLine();
        {
            var f = Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald, center: ShioReg, back1: UnitCatalog.SeroOld, back3: UnitCatalog.BasaK0);
            var ctx = Ctx(f, out var p);
            var slots0 = string.Join(",", p.Select(u => u.Slot));
            Act(ctx, U(p, "shio"));
            Expect("全員満タンなら何もしない（席）", string.Join(",", p.Select(u => u.Slot)), slots0);
            Expect("全員満タンなら何もしない（計数・全員満タン）", Tally(ctx, U(p, "shio")).RegroupAllFull, 1L);
        }
        {
            // 前1 ヨミ 50% ／ 前3 ガルド 90%。最も傷ついたのはヨミ。前1 の後ろ側の隣は 中央 → 後1 の順 → 中央のシオ。
            var f = Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald, center: ShioReg, back1: UnitCatalog.SeroOld, back3: UnitCatalog.BasaK0);
            var ctx = Ctx(f, out var p);
            var yomi = U(p, "yomi"); var gald = U(p, "gald"); var shio = U(p, "shio");
            yomi.Hp = yomi.MaxHp / 2; gald.Hp = gald.MaxHp * 9 / 10;
            int hp0 = yomi.Hp;
            Act(ctx, shio);
            Expect("最も傷ついた（割合）ヨミを下げる → 中央", Seat(yomi), "中央");
            Expect("相手は後ろ側の隣で席番号の順（中央のシオ）→ 前1", Seat(shio), "前1");
            Expect("下げたヨミに軋みが反応（+9）と移り木（+5）", yomi.AtkBonus, DisplacedTrait.Gain + DrifterTrait.Gain);
            Expect("下げたヨミに移り木の回復（+10）", yomi.Hp - hp0, DrifterTrait.Heal);
            Expect("ガルドは動かない", Seat(gald), "前3");
        }
        {
            // 前へ押し出された側の反応: 前1 セロ（傷）／ 中央 ヨミ（満タン）→ ヨミが前へ突き出される（+22）。
            var f = Formation.Build(front1: UnitCatalog.SeroOld, front3: UnitCatalog.Gald, center: UnitCatalog.Yomi, back1: ShioReg, back3: UnitCatalog.BasaK0);
            var ctx = Ctx(f, out var p);
            var yomi = U(p, "yomi"); var sero = U(p, "sero");
            sero.Hp = sero.MaxHp / 3;
            int yhp = yomi.Hp;
            Act(ctx, U(p, "shio"));
            Expect("押し出したヨミ → 前1", Seat(yomi), "前1");
            Expect("押し出したヨミは突き出し（+22）と移り木（+5）", yomi.AtkBonus, DisplacedTrait.PushedToFrontGain + DrifterTrait.Gain);
            Expect("無傷で押し出されたヨミは回復で増えない（満タン）", yomi.Hp, yhp);
            Expect("下げたセロに移り木（+5）", sero.AtkBonus, DrifterTrait.Gain);
            Expect("計数: 押し出された ／ 下げられた", (Tally(ctx, yomi).RegroupPushed, Tally(ctx, sero).RegroupLowered), (1L, 1L));
        }
        {
            // 最も傷ついたのが後列（後ろ側の隣が無い）なら、次に傷ついた駒で探す。
            var f = Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald, center: ShioReg, back1: UnitCatalog.SeroOld, back3: UnitCatalog.BasaK0);
            var ctx = Ctx(f, out var p);
            var basa = U(p, "basa"); var gald = U(p, "gald");
            basa.Hp = basa.MaxHp / 10; gald.Hp = gald.MaxHp / 2;
            Act(ctx, U(p, "shio"));
            Expect("後列のバサ（最も傷ついた）は動かない", Seat(basa), "後3");
            Expect("次に傷ついたガルドを下げる（前3 の後ろ側の隣は 中央 → 後3 → 中央）", Seat(gald), "中央");
        }
        {
            // 後列だけが傷ついていれば何もしない。
            var f = Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald, center: ShioReg, back1: UnitCatalog.SeroOld, back3: UnitCatalog.BasaK0);
            var ctx = Ctx(f, out var p);
            U(p, "basa").Hp = 10; U(p, "sero").Hp = 10;
            var slots0 = string.Join(",", p.Select(u => u.Slot));
            Act(ctx, U(p, "shio"));
            Expect("後列だけが傷ついていれば何もしない（席）", string.Join(",", p.Select(u => u.Slot)), slots0);
            Expect("同（計数・相手なし）", Tally(ctx, U(p, "shio")).RegroupStuck, 1L);
        }
        {
            // シオ自身が最も傷ついていればシオ自身を下げる。
            var f = Formation.Build(front1: ShioReg, front3: UnitCatalog.Gald, center: UnitCatalog.Yomi, back1: UnitCatalog.SeroOld, back3: UnitCatalog.BasaK0);
            var ctx = Ctx(f, out var p);
            var shio = U(p, "shio");
            shio.Hp = 5;
            Act(ctx, shio);
            Expect("シオ自身を下げる → 中央", Seat(shio), "中央");
            Expect("計数: 自分を下げた", Tally(ctx, shio).RegroupSelf, 1L);
        }
        {
            // パターン2: 前衛 D（○前2）が傷 → 後ろ側の隣は中衛（席番号の順）。
            var f = Formation.BuildDiamond(a: UnitCatalog.SeroOld, b: UnitCatalog.BasaK0, c: ShioReg, d: UnitCatalog.Yomi, e: UnitCatalog.Gald);
            var ctx = Ctx(f, out var p);
            var yomi = U(p, "yomi");
            string from = Seat(yomi);
            yomi.Hp = 10;
            Act(ctx, U(p, "shio"));
            var partner = p.First(u => u != yomi && FormationRules.SeatNames[u.Slot] == from);
            Expect("パターン2: 前衛のヨミが下がった（前の席に入ったのは隣でより後ろの行の駒）", FormationRules.AreAdjacent(yomi, partner) && FormationRules.DepthOf(yomi.Row) > FormationRules.DepthOf(partner.Row), true);
        }
        Console.WriteLine();

        // ---- (3') ヨミの薙ぎ ----
        Console.WriteLine("## (3') ヨミ: 閾値未満は単体・以上は薙ぎ");
        Console.WriteLine();
        foreach (var (def, th) in new[] { (YomiS30, 30), (YomiS20, 20) })
        {
            var u = BattleEngine.Materialize(Formation.Build(front1: def), BattleContext.PlayerTeam)[0];
            u.AtkBonus = th - def.Attack - 1;
            Expect($"閾値 {th}: 攻 {u.CurrentAttack} は単体", u.CurrentPattern, AttackPattern.Single);
            u.AtkBonus = th - def.Attack;
            Expect($"閾値 {th}: 攻 {u.CurrentAttack} は薙ぎ", u.CurrentPattern, AttackPattern.Sweep);
            u.AtkBonus = th - def.Attack - 5;
            Expect($"閾値 {th}: 弱体で攻 {u.CurrentAttack} に下がれば単体に戻る", u.CurrentPattern, AttackPattern.Single);
        }
        {
            var u = BattleEngine.Materialize(Formation.Build(front1: YomiV0), BattleContext.PlayerTeam)[0];
            u.AtkBonus = 100;
            Expect("V0 のヨミは攻 106 でも単体", u.CurrentPattern, AttackPattern.Single);
        }
        Console.WriteLine();

        // ---- (2)(6) 全戦で ----
        Console.WriteLine("## (2) verbose の有無で勝敗・決着ターンが同じ ／ (6) 台本の入れ替え");
        Console.WriteLine();
        long mismatch = 0, battles = 0, regroups = 0, badHurt = 0, badMove = 0, badDepth = 0, sweepsBelow = 0, singlesAbove = 0, yomiAtk = 0;
        var gate = new object();
        var benches = new List<Formation> { BenchD1, PickD4(print: false) };
        benches.AddRange(CompareRowsWith().Select(r => r.F));
        foreach (var bf in benches)
            foreach (var (_, sc) in Scales)
                for (int w = 0; w < WaveNames.Length; w++)
                    foreach (var (tag, shioD, yomiD) in Versions)
                    {
                        var f = Apply(bf, shioD, yomiD);
                        int th = yomiD == YomiS20 ? 20 : yomiD == YomiS30 ? 30 : int.MaxValue;
                        var enemy = WaveOf(w, sc);
                        Parallel.For(0, 100, s =>
                        {
                            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var start = pl.Select(u => u.Slot).ToList();
                            var rv = BattleEngine.Run(pl, enemy(), s, verbose: true);
                            var rq = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), enemy(), s, verbose: false);
                            long mm = rv.PlayerWon != rq.PlayerWon || rv.Turns != rq.Turns ? 1 : 0;
                            var slot = new Dictionary<int, int>();
                            for (int i = 0; i < pl.Count; i++) slot[pl[i].InstanceId] = start[i];
                            int? yid = pl.FirstOrDefault(u => u.Def.Id == "yomi")?.InstanceId;
                            long rg = 0, bh = 0, bm = 0, bd = 0, sb = 0, sa = 0, ya = 0;
                            var ev = rv.Events;
                            for (int i = 0; i < ev.Count; i++)
                            {
                                var e = ev[i];
                                if (e.Kind == BattleEventKind.Move && e.TargetId is int t && slot.ContainsKey(t)) slot[t] = e.Slot;
                                if (e.Kind == BattleEventKind.Attack && yid is int y && e.ActorId == y)
                                {
                                    ya++;
                                    if (e.Pattern == AttackPattern.Sweep && e.Amount < th) sb++;
                                    if (e.Pattern == AttackPattern.Single && e.Amount >= th) sa++;
                                }
                                if (e.Kind != BattleEventKind.Regroup) continue;
                                rg++;
                                if (!(e.HpAfter < e.Amount)) bh++;
                                int low = e.TargetId!.Value, par = e.PartnerId!.Value;
                                int lowFrom = slot[low], parFrom = slot[par];
                                // 直後の2件が2体の Move（ActorId ＝ シオ）であること
                                var m1 = i + 1 < ev.Count ? ev[i + 1] : null;
                                var m2 = i + 2 < ev.Count ? ev[i + 2] : null;
                                if (m1 is null || m2 is null || m1.Kind != BattleEventKind.Move || m1.TargetId != low || m1.ActorId != e.ActorId
                                    || !ev.Skip(i + 1).Any(x => x.Kind == BattleEventKind.Move && x.TargetId == par && x.ActorId == e.ActorId)) bm++;
                                // 下げた駒の元の席と相手の元の席: 隣接かつ相手がより後ろの行
                                if (!(f.Shape.AreAdjacent(lowFrom, parFrom) && FormationRules.DepthOf(FormationRules.RowOf(parFrom)) > FormationRules.DepthOf(FormationRules.RowOf(lowFrom)))) bd++;
                            }
                            lock (gate)
                            {
                                battles++; mismatch += mm; regroups += rg; badHurt += bh; badMove += bm; badDepth += bd;
                                sweepsBelow += sb; singlesAbove += sa; yomiAtk += ya;
                            }
                        });
                    }
        Console.WriteLine($"- 戦 {battles}・入れ替え {regroups}・ヨミの一撃 {yomiAtk}");
        Expect("(2) verbose の有無で勝敗・決着ターンが違った戦", mismatch, 0L);
        Expect("(3) 下げた駒が無傷だった入れ替え", badHurt, 0L);
        Expect("(3) 相手が「隣接かつより後ろの行」でなかった入れ替え", badDepth, 0L);
        Expect("(6) Regroup の直後に2体の Move（書き手 ＝ シオ）が続かなかった", badMove, 0L);
        Expect("(3') 閾値未満で薙いだヨミの一撃（割り込みを含む）", sweepsBelow, 0L);
        Expect("(3') 閾値以上で単体だったヨミの一撃", singlesAbove, 0L);
        Console.WriteLine();
        Console.WriteLine($"**合格 {ok} ／ 不合格 {ng}**");
        Console.WriteLine($"DRIFT_CHECK ok={ng == 0}");
    }
}
