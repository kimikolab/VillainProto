using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// sero check（第223期） —— 自己検査（受け入れ 2〜4・7）
//
// (4a) 盤面を直に組んで: 状態異常のダメージ（毒・燃焼の刻み）・味方からのダメージ（放電・爆発・刃）・中継は回避しない／
//      敵の攻撃は回避の判定を振る（率は 30% の近く）／避けた一撃で破片が減らない／回避のたび入れ替わる（隣がいれば）・隣がいなければ入れ替わらない／
//      段は動かされた回数で上がる（3・6・10）／段3 で回避率 45%／E2 でセロの状態が減らない・矢で毒 +2・燃焼・感電が付く
// (3)  verbose の有無で勝敗・決着ターンが同じ（E1・E2 × 台 × 波）
// (4b) 台本: 乱れ撃ちは5本（戦が終わらない限り）／避けた → 入れ替えの2つの Move（出どころ ＝ セロ）→ 追い撃ち → その矢の Attack（的 ＝ 攻撃してきた敵）／
//      段が上がる出来事は 1・2・3 の順／状態の矢の直後に StatusGain
// =====================================================================================

static partial class SeroDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("BattleContext.TallyOf が見つからない");
    static UnitTally Tal(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static int ok, ng;
    static void Expect(string what, long got, long want)
    {
        bool pass = got == want;
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }
    static void ExpectIn(string what, long got, long lo, long hi)
    {
        bool pass = got >= lo && got <= hi;
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + $"（{lo}〜{hi}）");
    }

    static BattleContext Ctx(Formation pl, Formation en, int seed, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(seed, false);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in p.Concat(e)) { u.MaxHp = 99999; u.Hp = 99999; }
        return ctx;
    }

    static partial void CheckImpl()
    {
        ok = ng = 0;
        Console.WriteLine("# 第223期 sero check");
        Console.WriteLine();
        Console.WriteLine("## (4a) 盤面を直に組んで");
        Console.WriteLine();
        var knight = EnemyCatalog.Stages[1].Enemy.Occupied().Select(o => o.Def).First(d => d.Traits.Count == 0);
        Formation E5 = Formation.Build(front1: knight, front3: knight, center: knight, back1: knight, back3: knight);
        const int N = 1000;

        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.TouT0, center: E1, back1: UnitCatalog.Gald), E5, 1, out var p, out var e);
            UnitState sero = p.First(u => u.Def.Id == "sero"), ally = p.First(u => u.Def.Id == "tou");
            UnitTally t = Tal(ctx, sero);
            int hp0 = sero.Hp;
            for (int i = 0; i < N; i++) ctx.ApplyDamage(sero, 5, null);                              // 毒の刻み・起爆（出どころ null）
            for (int i = 0; i < N; i++) ctx.ApplyDamage(sero, 5, null, burnTick: true);              // 燃焼の刻み
            for (int i = 0; i < N; i++) ctx.ApplyDamage(sero, 5, ally, isFriendlyFire: true);        // 放電・爆発・味方の刃（同じ陣営）
            for (int i = 0; i < N; i++) ctx.ApplyDamage(sero, 5, e[0], relayed: true);               // 肩代わりの中継
            for (int i = 0; i < N; i++) ctx.ApplyDamage(sero, 5, e[0], hexShare: true);              // 呪いの共有
            Expect("状態異常・味方・中継・共有の 5,000 回で回避の判定を振った回数", t.EvRolls, 0);
            Expect("同じく HP の減り", hp0 - sero.Hp, 5 * 5 * N);
        }
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.TouT0, center: E1, back1: UnitCatalog.Gald), E5, 2, out var p, out var e);
            UnitState sero = p.First(u => u.Def.Id == "sero");
            UnitTally t = Tal(ctx, sero);
            sero.SetCounter(StatusKeys.Armor, 99999);
            int armor0 = sero.RawCounter(StatusKeys.Armor), hp0 = sero.Hp;
            for (int i = 0; i < N; i++) { ctx.ApplyDamage(sero, 5, e[i % 5], singleHit: true, pattern: AttackPattern.Single); foreach (var x in e) x.Hp = x.MaxHp; }
            Expect("敵の攻撃 1,000 回で判定を振った回数", t.EvRolls, N);
            // 入れ替えるたび段が上がる（隣のトウと 10 回入れ替わると段3 ＝ 45%）ので率は 30〜45% のあいだ
            ExpectIn("避けた回数（入れ替えで段が上がる・30%〜45% の ±4σ）", t.Evades, 300 - 58, 450 + 63);
            Expect("破片の減り ＝ 5 ×（当たった回数）", armor0 - sero.RawCounter(StatusKeys.Armor), 5 * (N - t.Evades));
            Expect("HP は減らない（破片が受け切る）", hp0 - sero.Hp, 0);
            Expect("攻撃力 ＝ 11 ＋ 3 × 避けた回数", sero.CurrentAttack, 11 + 3 * t.Evades);
            Expect("入れ替えた回数 ＝ 避けた回数（隣がいる）", t.EvSwaps + t.EvSwapRefused, t.Evades);
            Expect("段が上がって追い撃ちが2本になっても本数 ≧ 避けた回数（反撃の中の回避を除く）", t.EvRipostes + t.EvRiposteInReaction + t.EvRiposteHushed >= t.Evades ? 1 : 0, 1);
        }
        {
            var ctx = Ctx(Formation.Build(center: E1), E5, 3, out var p, out var e);
            UnitState sero = p[0];
            UnitTally t = Tal(ctx, sero);
            for (int i = 0; i < 300; i++) ctx.ApplyDamage(sero, 5, e[0], singleHit: true, pattern: AttackPattern.Single);
            Expect("隣に誰もいないとき入れ替わった回数", t.EvSwaps, 0);
            Expect("隣に誰もいなかった回数 ＝ 避けた回数", t.EvSwapNone, t.Evades);
            Expect("動かされた回数（入れ替わっていないので 0）", EvadeTrait.MovesOf(sero), 0);
        }
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.TouT0, center: E1NoSwap), E5, 6, out var p, out var e);
            UnitState sero = p.First(u => u.Def.Id == "sero");
            for (int i = 0; i < N; i++) { ctx.ApplyDamage(sero, 5, e[i % 5], singleHit: true, pattern: AttackPattern.Single); foreach (var x in e) x.Hp = x.MaxHp; }
            ExpectIn("入れ替えなし（段0 のまま）で避けた回数（30%・±4σ）", Tal(ctx, sero).Evades, 300 - 58, 300 + 58);
            Expect("入れ替えなしの追い撃ち ＝ 避けた回数（段0・1本・単体）", Tal(ctx, sero).EvRipostes, Tal(ctx, sero).Evades);
            Expect("段0 の追い撃ちに貫きは無い", Tal(ctx, sero).EvRipostePierce, 0);
        }
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.TouT0, center: E1NoSwap), E5, 4, out var p, out var e);
            UnitState sero = p.First(u => u.Def.Id == "sero"), tou = p.First(u => u.Def.Id == "tou");
            var got = new List<int>();
            for (int k = 1; k <= 11; k++)
            {
                ctx.SwapSlots(tou, sero.Slot, tou);   // 2体が入れ替わる → セロが1回動かされる
                got.Add(EvadeTrait.StageOf(sero));
            }
            Expect("段（動かされた 1〜11 回）が 0,0,1,1,1,2,2,2,2,3,3", string.Join(",", got) == "0,0,1,1,1,2,2,2,2,3,3" ? 1 : 0, 1);
            Expect("段3 の回避率", EvadeTrait.PercentOf(sero), 45);
            UnitTally t = Tal(ctx, sero);
            for (int i = 0; i < N; i++) { ctx.ApplyDamage(sero, 5, e[0], singleHit: true, pattern: AttackPattern.Single); foreach (var x in e) x.Hp = x.MaxHp; }
            ExpectIn("段3 で避けた回数（45%・±4σ）", t.Evades, 450 - 63, 450 + 63);
            Expect("段3 の追い撃ちの本数 ＝ 2 × 撃てた回避（的が生きている間）", t.EvRipostes, 2 * (t.Evades - t.EvRiposteInReaction - t.EvRiposteHushed));
            Expect("段1 以上の追い撃ちは全部貫き", t.EvRipostePierce, t.EvRipostes);
        }
        {
            var ctx = Ctx(Formation.Build(center: E2), E5, 5, out var p, out var e);
            UnitState sero = p[0];
            ctx.Poison(sero, 3, e[0], PoisonRoute.Venom);
            ctx.Ignite(sero, source: e[0]);
            ctx.MarkShock(sero, e[0]);
            int sp = sero.RawCounter(StatusKeys.Poison), sb = sero.RawCounter(StatusKeys.Burn), ss = sero.RawCounter(StatusKeys.Shock);
            UnitState foe = e[4];
            ctx.EvadeShot(sero, foe, false, 1);
            ctx.EvadeShot(sero, foe, false, 1);
            Expect("E2: 2本撃った後のセロの毒", sero.RawCounter(StatusKeys.Poison), sp);
            Expect("E2: セロの燃焼", sero.RawCounter(StatusKeys.Burn), sb);
            Expect("E2: セロの感電", sero.RawCounter(StatusKeys.Shock), ss);
            Expect("E2: 敵の毒（2本 × 2 層）", foe.RawCounter(StatusKeys.Poison), 4);
            Expect("E2: 敵の燃焼（残り 3）", foe.RawCounter(StatusKeys.Burn), BurnRules.Turns);
            Expect("E2: 敵の感電（2本目の矢で弾けて付き直す）", foe.RawCounter(StatusKeys.Shock), 1);
            Expect("E2: 矢で付けた数（毒・燃焼・感電 × 2本）", Tal(ctx, sero).ArrowPoison + Tal(ctx, sero).ArrowBurn + Tal(ctx, sero).ArrowShock, 6);
            Expect("追い撃ちの的は指定した敵（前列の規則・庇いを通らない）: 後3 の敵の HP が減った", foe.Hp < foe.MaxHp ? 1 : 0, 1);
        }
        Console.WriteLine();

        // ---------------- (3)・(4b) 全戦 ----------------
        Console.WriteLine("## (3)(4b) 戦で（E1・E2 × S1 ポン・S2・S3・S4 の12行 × 5波 × 115/115・150/115 × seed 0..49）");
        Console.WriteLine();
        var benches = new List<Formation> { BenchS1Pon, BenchS2Raw, BenchS3Raw };
        benches.AddRange(CompareRowsWithSero().Select(r => r.F));
        long battles = 0, verboseDiff = 0, barrageTurns = 0, barrageShort = 0, evadeEv = 0, evadeBadMove = 0, ripBad = 0, stageBad = 0, arrowBad = 0, arrowEv = 0, ripEv = 0;
        foreach (var f0 in benches)
            foreach (var ver in new[] { E1, E2 })
                for (int s = 0; s < Scales.Length; s++)
                    for (int w = 0; w < WaveNames.Length; w++)
                        for (int seed = 0; seed < 50; seed++)
                        {
                            var f = WithSero(f0, ver);
                            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var r = BattleEngine.Run(pl, WaveOf(w, Scales[s].Sc)(), seed, verbose: true);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), WaveOf(w, Scales[s].Sc)(), seed, verbose: false);
                            battles++;
                            if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns) verboseDiff++;
                            int sero = pl.First(u => u.Def.Id == "sero").InstanceId;
                            var ev = r.Events;
                            int lastStage = 0;
                            int bTurn = -1, bLen = 0;
                            int lastTurn = ev.Count == 0 ? 0 : ev.Max(y => y.Turn);   // 敵が全滅した手番（`r.Turns` は次のターンを数えていることがある）
                            int seroDeath = ev.Where(y => y.Kind == BattleEventKind.Death && y.TargetId == sero).Select(y => y.Turn).DefaultIfEmpty(-1).First();
                            void Close()
                            {
                                if (bLen > 0 && bLen < 5 && bTurn != lastTurn && bTurn != seroDeath)
                                {
                                    barrageShort++;
                                    if (Environment.GetEnvironmentVariable("SERO_DEBUG") == "1" && barrageShort == 1)
                                        foreach (var y in ev.Where(y => y.Turn == bTurn))
                                            Console.WriteLine($"    dbg T{y.Turn} {y.Kind} a={y.ActorId} t={y.TargetId} slot={y.Slot} amt={y.Amount} (sero={sero}, turns={r.Turns})");
                                }
                                bLen = 0;
                            }
                            for (int i = 0; i < ev.Count; i++)
                            {
                                var x = ev[i];
                                if (x.Kind == BattleEventKind.Barrage)
                                {
                                    if (x.Slot == 1) { Close(); bTurn = x.Turn; bLen = 1; barrageTurns++; }
                                    else if (x.Slot == bLen + 1) bLen++;
                                    else barrageShort++;
                                }
                                else if (x.Kind == BattleEventKind.Evade)
                                {
                                    evadeEv++;
                                    if (x.PartnerId is not null)
                                    {
                                        // 直後の2件が Move（出どころ ＝ セロ）で、対象がセロと相手
                                        if (i + 2 >= ev.Count || ev[i + 1].Kind != BattleEventKind.Move || ev[i + 1].ActorId != sero) evadeBadMove++;
                                    }
                                }
                                else if (x.Kind == BattleEventKind.EvadeRiposte)
                                {
                                    ripEv++;
                                    var at = ev.Skip(i + 1).FirstOrDefault(y => y.Kind == BattleEventKind.Attack);
                                    if (at is null || at.ActorId != sero || at.TargetId != x.TargetId || at.Reaction != true) ripBad++;
                                }
                                else if (x.Kind == BattleEventKind.EvadeStage)
                                {
                                    if (x.Slot != lastStage + 1 && !(x.Slot > lastStage)) stageBad++;
                                    lastStage = x.Slot;
                                }
                                else if (x.Kind == BattleEventKind.StatusArrow)
                                {
                                    arrowEv++;
                                    if (i + 1 >= ev.Count || ev[i + 1].Kind != BattleEventKind.StatusGain) arrowBad++;
                                }
                            }
                            Close();
                        }
        Expect($"verbose の有無で勝敗・決着ターンが違った戦（{battles} 戦）", verboseDiff, 0);
        Expect($"乱れ撃ちが5本に届かずに終わった手番（敵が全滅した・セロが倒れたターンを除く・{barrageTurns} 手番）", barrageShort, 0);
        Expect($"避けて入れ替わった直後が セロの Move でない（{evadeEv} 件）", evadeBadMove, 0);
        Expect($"追い撃ちの次の Attack がセロ・同じ的・ターン外でない（{ripEv} 件）", ripBad, 0);
        Expect("段が上がる出来事が昇順でない", stageBad, 0);
        Expect($"状態の矢の直後が StatusGain でない（{arrowEv} 件）", arrowBad, 0);
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ng}**（○ / ×）");
        Console.WriteLine($"SERO_CHECK_COMPLETE ok={ng == 0}");
    }
}
