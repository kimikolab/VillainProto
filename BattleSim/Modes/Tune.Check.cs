using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// tune check（第231期） —— 自己検査（受け入れ 2〜4・7）
//
// (4) 盤面を直に組んで: 退避の条件が版の表どおり ／ 弾き返しは隣の味方の被弾でも起き、そのときハネはその味方と入れ替わる・回数はハネ自身と共有・粛で止まる ／
//     移動の追撃は段2 以上・自分の回避の入れ替えでは撃たない・1ターン2回まで・粛で止まる・経路は自分と同じ番号 ／
//     挑発の表示は盤面の挑発と同じ判定（盤面が挑発で的を決めた瞬間、表示は必ず「効いている」）
// (3) verbose の有無で勝敗・決着T・倒れた駒が同じ（新しい処理は乱数を引かない・盤面の規則は表示を読まない）
// (7) 台本: SpringGuard の数 ＝ 帳簿・MoveShot の数 ＝ 帳簿・直後に移動の追撃の Attack
// (2) V0 の台本 ＝ 前段（`shockdigest a231` を実装の前後で突き合わせる・ここでは回さない）
// =====================================================================================
static partial class TuneDiag
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

    static BattleContext Ctx(Formation pl, Formation en, int seed, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(seed, false);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 100, int atk = 5, params TraitId[] tr) => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = tr };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static string Seat(UnitState u) => FormationRules.SeatNames[u.Slot];

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第231期 tune check");
        Console.WriteLine();
        Console.WriteLine("## (4) 盤面を直に組んで");
        Console.WriteLine();
        var en = Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("e2"), back1: Plain("e4"), back3: Plain("e5"));
        var hushDef = EnemyCatalog.Stages[1].Enemy.Occupied().Select(o => o.Def).First(d => d.Traits.Contains(TraitId.Hush));

        // ---- 退避の線 ----
        Console.WriteLine("### 緊急退避の線（前1 の x（最大HP 100）が敵の一撃を受ける・後ろ側の隣は中央の y）");
        Console.WriteLine();
        var shioVers = new (string Tag, UnitDef Shio)[] { ("V0", UnitCatalog.ShioA0), ("VA1", ShioHalf), ("VA2", ShioHeavy) };
        // (HP を先に何点削っておくか, 一撃, 版ごとに下げるか V0/VA1/VA2)
        var cases = new (int Pre, int Hit, string Want)[]
        {
            (0, 45, "×/×/○"),    // 100 → 55: 4割・5割の上・一撃 45 ≥ 30
            (0, 55, "×/○/○"),    // 100 → 45: 5割の下
            (0, 65, "○/○/○"),    // 100 → 35: 4割の下
            (0, 20, "×/×/×"),    // 100 → 80
            (40, 20, "×/○/×"),   // 60 → 40: 4割ちょうど（未満ではない）・5割の下・一撃 20 < 30
            (45, 20, "○/○/○"),   // 55 → 35
            (35, 20, "×/○/×"),   // 65 → 45: 5割の下・一撃 20 < 30
        };
        foreach (var (pre, hit, want) in cases)
        {
            var got = new List<string>();
            foreach (var (tag, shio) in shioVers)
            {
                var ctx = Ctx(Formation.Build(front1: Plain("x"), center: Plain("y"), back3: shio), en, 0, out var p, out var e);
                var x = U(p, "x");
                x.Hp -= pre;
                ctx.ApplyDamage(x, hit, U(e, "e1"));
                got.Add(Seat(x) != "前1" ? "○" : "×");
            }
            Expect($"HP {100 - pre} に一撃 {hit}（→ {100 - pre - hit}）で下げたか V0/VA1/VA2", string.Join("/", got), want);
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("x"), center: Plain("y"), back3: ShioHeavy), en, 0, out var p, out var e);
            ctx.ApplyDamage(U(p, "x"), 45, U(e, "e1"));
            Expect("VA2 の一撃3割だけで下げたときの帳簿（線の上で来た ／ 下げた）", $"{Tal(ctx, U(p, "shio")).RetreatHeavyChances}/{Tal(ctx, U(p, "shio")).RetreatHeavyOnly}", "1/1");
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("x"), center: Plain("y"), back3: ShioHalf), en, 0, out var p, out var e);
            ctx.ApplyDamage(U(p, "x"), 10, null, isFriendlyFire: true, burnTick: true);   // 刻み（出どころなし）で 90
            ctx.ApplyDamage(U(p, "x"), 45, null, burnTick: true);                          // 刻みで 45：退避は被弾ごと（出どころを問わない）
            Expect("VA1: 出どころの無い刻みで 5割を切っても下げる（第225期と同じく出どころを問わない）", Seat(U(p, "x")) != "前1", true);
        }
        Console.WriteLine();

        // ---- 弾き返しの隣 ----
        Console.WriteLine("### ハネの弾き返し（後3 のハネ ／ 隣の前3 の味方 z が敵 e1（前1）に殴られる）");
        Console.WriteLine();
        foreach (var (tag, hane, want) in new[] { ("VB", HaneGuard, "中央/前3/後3/1/1"), ("V0", UnitCatalog.Hane, "前1/後3/前3/0/0") })
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), front3: Plain("z"), back3: hane), en, 0, out var p, out var e);
            ctx.ApplyDamage(U(p, "z"), 10, U(e, "e1"));
            var t = Tal(ctx, U(p, "hane"));
            Expect($"{tag}: 殴った敵の席 ／ ハネの席 ／ z の席 ／ 隣で弾いた ／ 隣で入れ替わった", $"{Seat(U(e, "e1"))}/{Seat(U(p, "hane"))}/{Seat(U(p, "z"))}/{t.SpringGuardCount}/{t.SpringGuardSwaps}", want);
            if (tag == "VB") Expect("VB: 弾かれた敵は転ぶ", U(e, "e1").RawCounter(StatusKeys.Stagger), 1);
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("z"), front3: Plain("w"), back3: HaneGuard), en, 0, out var p, out var e);
            ctx.ApplyDamage(U(p, "z"), 10, U(e, "e3"));
            Expect("VB: 隣でない味方（前1 と 後3）が殴られても弾かない", Tal(ctx, U(p, "hane")).SpringGuardChances, 0L);
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), front3: Plain("z"), back3: HaneGuard), en, 0, out var p, out var e);
            ctx.ApplyDamage(U(p, "z"), 10, U(e, "e1"));
            // ハネは前3 へ出た。今度はハネ自身が殴られる（同じターン）→ 回数（1 ＋ 敵の乱れの段）はハネ自身と共有
            int limit = SpringTrait.LimitOf(ctx, U(p, "hane"));
            for (int i = 0; i < 3; i++) ctx.ApplyDamage(U(p, "hane"), 1, U(e, "e3"));
            var t = Tal(ctx, U(p, "hane"));
            Expect($"VB: 隣の分とハネ自身の分は同じ上限を分け合う（1ターン {limit} 回・弾いた計 ＝ 上限）", t.SpringCount, (long)limit);
            ExpectTrue("VB: 上限で止まった回数が数えられている", t.SpringCapped > 0, $"{t.SpringCapped}");
        }
        {
            var ctx = Ctx(Formation.Build(front1: Plain("w"), front3: Plain("z"), back3: HaneGuard),
                Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: hushDef, back1: Plain("e4"), back3: Plain("e5")), 0, out var p, out var e);
            ctx.ApplyDamage(U(p, "z"), 10, U(e, "e1"));
            Expect("VB: 粛の伝令が生きている間は隣の分も弾かない（弾いた ／ 粛で止まった）", $"{Tal(ctx, U(p, "hane")).SpringCount}/{Tal(ctx, U(p, "hane")).SpringHushed}", "0/1");
        }
        Console.WriteLine();

        // ---- 移動の追撃 ----
        Console.WriteLine("### 移動の追撃（セロ ＋ `EvadeMoveShot`・段は動かされた回数を直に書く）");
        Console.WriteLine();
        {
            var ctx = Ctx(Formation.Build(front3: SeroShot, center: Plain("y"), back1: Plain("q")), en, 0, out var p, out var e);
            var sero = U(p, "sero");
            sero.SetCounter(EvadeTrait.MovesKey, EvadeTrait.QuickStageAt[1]);   // 段2
            ctx.SwapSlots(sero, 2, U(p, "y"));
            var t = Tal(ctx, sero);
            Expect("段2: 動かされたら1本（撃った ／ 与ダメ > 0）", $"{t.MoveShots}/{t.EvMoveShotDealt > 0}", "1/True");
            ExpectTrue("経路: 前3 から動かされたセロは中央へ動いた後なので「中央 ＝ 敵が多い経路・同数なら経路0」＝ 前1 の e1 か 前3 の e3 の列", e.Any(u => u.Hp < u.MaxHp));
        }
        {
            var ctx = Ctx(Formation.Build(front3: SeroShot, center: Plain("y"), back3: Plain("q")), en, 0, out var p, out var e);
            var sero = U(p, "sero");
            sero.SetCounter(EvadeTrait.MovesKey, EvadeTrait.QuickStageAt[1]);
            ctx.SwapSlots(sero, 4, U(p, "q"));   // 前3 → 後3（経路1 のまま）
            Expect("経路1 のセロは敵の経路1 の先頭（前3 の e3）を撃つ: e3 ／ e1 が削られたか", $"{U(e, "e3").Hp < 100}/{U(e, "e1").Hp < 100}", "True/False");
            ctx.SwapSlots(sero, 1, U(p, "q"));
            ctx.SwapSlots(sero, 4, U(p, "q"));
            var t = Tal(ctx, sero);
            Expect("1ターンに2回まで（3回動かされて 撃った ／ 上限で止まった）", $"{t.MoveShots}/{t.MoveShotCapped}", "2/1");
            TurnP.SetValue(ctx, 2);
            ctx.SwapSlots(sero, 1, U(p, "q"));
            Expect("次のターンにはまた撃てる", Tal(ctx, sero).MoveShots, 3L);
        }
        {
            var ctx = Ctx(Formation.Build(front3: SeroShot, center: Plain("y"), back3: Plain("q")), en, 0, out var p, out var e);
            var sero = U(p, "sero");
            sero.SetCounter(EvadeTrait.MovesKey, EvadeTrait.QuickStageAt[1] - 1);   // 段1（この移動で段2 に届く）
            ctx.SwapSlots(sero, 4, U(p, "q"));
            Expect("段1 のセロは撃たない（この移動で段2 に届いても）", Tal(ctx, sero).MoveShots, 0L);
            ctx.SwapSlots(sero, 1, U(p, "q"));
            Expect("段2 に届いた次の移動から撃つ", Tal(ctx, sero).MoveShots, 1L);
        }
        {
            var ctx = Ctx(Formation.Build(front3: SeroShot, center: Plain("y"), back3: Plain("q")), en, 0, out var p, out var e);
            var sero = U(p, "sero");
            sero.SetCounter(EvadeTrait.MovesKey, EvadeTrait.QuickStageAt[1]);
            EvadeSwapTrait.Swap(ctx, sero, U(p, "q"));
            var t = Tal(ctx, sero);
            Expect("自分の回避の入れ替えでは撃たない（撃った ／ 自分の入れ替えで数えた）", $"{t.MoveShots}/{t.MoveShotSelfSwap}", "0/1");
            Expect("印は入れ替えの外で 0 に戻る", sero.RawCounter(EvadeMoveShotTrait.SwapKey), 0);
        }
        {
            var ctx = Ctx(Formation.Build(front3: SeroShot, center: Plain("y"), back3: Plain("q")),
                Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: hushDef, back1: Plain("e4"), back3: Plain("e5")), 0, out var p, out var e);
            var sero = U(p, "sero");
            sero.SetCounter(EvadeTrait.MovesKey, EvadeTrait.QuickStageAt[1]);
            ctx.SwapSlots(sero, 4, U(p, "q"));
            Expect("粛の伝令が生きている間は撃たない（撃った ／ 粛で止まった）", $"{Tal(ctx, sero).MoveShots}/{Tal(ctx, sero).MoveShotHushed}", "0/1");
        }
        {
            var ctx = Ctx(Formation.Build(front3: UnitCatalog.SeroC0, center: Plain("y"), back3: Plain("q")), en, 0, out var p, out var e);
            var sero = U(p, "sero");
            sero.SetCounter(EvadeTrait.MovesKey, EvadeTrait.QuickStageAt[1]);
            ctx.SwapSlots(sero, 4, U(p, "q"));
            Expect("札の無い規定のセロは撃たない", Tal(ctx, sero).MoveShots + Tal(ctx, sero).MoveShotChances, 0L);
        }
        Console.WriteLine();

        // ---- 実戦: 挑発の表示・verbose・台本 ----
        Console.WriteLine("## (4)(3)(7) 実戦（M-ハネ 228 の席 × 版 × 6 波 × 倍率3 × seed 0..19）");
        Console.WriteLine();
        long battles = 0, decoyPicks = 0, decoyPickOff = 0, decoyShows = 0, verboseDiff = 0, guardEv = 0, guardTal = 0, shotEv = 0, shotTal = 0, shotNoAttack = 0;
        var lk = new object();
        foreach (var v in Versions)
            for (int w = 0; w < WaveNames.Length; w++)
                foreach (var (_, sc) in Scales)
                    Parallel.For(0, 20, s =>
                    {
                        var f = Apply(MHane228, v);
                        var (r, p, e, _) = Fight(f, w, sc, s, verbose: true);
                        var (r2, _, _, _) = Fight(f, w, sc, s, verbose: false);
                        long dp = 0, dpo = 0, ds = 0, ge = 0, se = 0, sna = 0;
                        int sero = p.FirstOrDefault(u => u.Def.Id == "sero")?.InstanceId ?? -1;
                        bool on = false;
                        var evs = r.Events;
                        for (int i = 0; i < evs.Count; i++)
                        {
                            var ev = evs[i];
                            if (ev.Kind == BattleEventKind.DecoyShow && ev.ActorId == sero) { ds++; on = ev.Slot == 1; }
                            if (ev.Kind == BattleEventKind.Decoy && ev.ActorId == sero) { dp++; if (!on) dpo++; }
                            if (ev.Kind == BattleEventKind.SpringGuard) ge++;
                            if (ev.Kind == BattleEventKind.MoveShot)
                            {
                                se++;
                                var next = evs.Skip(i + 1).FirstOrDefault(x => x.Kind == BattleEventKind.Attack);
                                if (next is null || next.ActorId != ev.ActorId) sna++;
                            }
                        }
                        long gt = 0, st = 0;
                        foreach (var (id, t) in r.TallyByUnit) { gt += t.SpringGuardFires; st += t.MoveShots; }
                        bool same = r.PlayerWon == r2.PlayerWon && r.Turns == r2.Turns
                                    && string.Join(",", r.PlayerStarterFallen.OrderBy(x => x)) == string.Join(",", r2.PlayerStarterFallen.OrderBy(x => x));
                        lock (lk)
                        {
                            battles++; decoyPicks += dp; decoyPickOff += dpo; decoyShows += ds; guardEv += ge; guardTal += gt; shotEv += se; shotTal += st; shotNoAttack += sna;
                            if (!same) verboseDiff++;
                        }
                    });
        Expect("(3) verbose の有無で勝敗・決着T・倒れた駒が違う戦", verboseDiff, 0L);
        ExpectTrue("(4) 挑発が的を決めた瞬間、表示は必ず「効いている」（盤面と同じ判定）", decoyPicks > 0 && decoyPickOff == 0, $"挑発が的を決めた {decoyPicks} 回のうち表示が消えていた {decoyPickOff} 回・表示の切り替わり {decoyShows} 回（{battles} 戦）");
        ExpectTrue("(7) 台本の SpringGuard の数 ＝ 帳簿", guardEv == guardTal && guardEv > 0, $"{guardEv} ／ {guardTal}");
        ExpectTrue("(7) 台本の MoveShot の数 ＝ 帳簿、直後の Attack はセロ", shotEv == shotTal && shotEv > 0 && shotNoAttack == 0, $"{shotEv} ／ {shotTal}・直後がセロの Attack でない {shotNoAttack}");
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
