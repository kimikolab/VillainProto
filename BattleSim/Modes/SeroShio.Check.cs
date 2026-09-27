using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// seroshio check（第224期） —— 自己検査（受け入れ 1〜3）
//
// (3a) 盤面を直に組んで: 段は F0 で 3/6/10・F1〜F3 で 2/4/7 ／ F2 以降は動かされるたび攻撃力 +2（F1 は上がらない）／
//      乱れ撃ちは F3 の段3 で 7 本・段2 と F2 の段3 は 5 本 ／ H1 の移り木は最大HPの 20%（溢れは切る）・H0 は +10 ／
//      H2 は下げた味方にだけ手当て（押し出した駒・シオ自身を下げたときは出ない）
// (3b) verbose の有無で勝敗・決着ターン・与ダメが同じ（F3×H2 ほか × 台 × 波）
// (1)  F0×H0 の台本 ＝ 第223期（`shockdigest f224` を実装の前後で突き合わせる・ここでは回さない）
// =====================================================================================
static partial class SeroShioDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("BattleContext.TallyOf が見つからない");
    static readonly MethodInfo BarrageM = typeof(BattleContext).GetMethod("Barrage", BindingFlags.Instance | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("BattleContext.Barrage が見つからない");
    static UnitTally Tal(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static int ok, ng;
    static void Expect(string what, object got, object want)
    {
        bool pass = Equals(got, want);
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }

    static BattleContext Ctx(Formation pl, Formation en, int seed, out List<UnitState> p, out List<UnitState> e, bool huge = true)
    {
        var ctx = new BattleContext(seed, false);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        if (huge) foreach (var u in e) { u.MaxHp = 99999; u.Hp = 99999; }
        return ctx;
    }

    static UnitState U(List<UnitState> p, string id) => p.First(u => u.Def.Id == id);
    static string Seat(UnitState u) => FormationRules.SeatNames[u.Slot];
    static void Regroup(BattleContext ctx, UnitState shio)
        => TraitCatalog.Get(TraitId.Regroup).OnAction(ctx, shio, new UnitAction(ActionKind.Skill, Label: "隊を組み替えた"));

    static partial void CheckImpl()
    {
        ok = ng = 0;
        Console.WriteLine("# 第224期 seroshio check");
        Console.WriteLine();
        if (F1 is null || F2 is null || F3 is null || H1 is null || H2 is null) { Console.WriteLine("版の札が見つからない（Phase 0 のコミット）。"); return; }
        var knight = EnemyCatalog.Stages[1].Enemy.Occupied().Select(o => o.Def).First(d => d.Traits.Count == 0);
        Formation E5 = Formation.Build(front1: knight, front3: knight, center: knight, back1: knight, back3: knight);

        Console.WriteLine("## (3a) 盤面を直に組んで");
        Console.WriteLine();
        foreach (var (tag, def, want, atkPer) in new[] { ("F0", F0, "0,0,1,1,1,2,2,2,2,3,3", 0), ("F1", F1, "0,1,1,2,2,2,3,3,3,3,3", 0),
                                                        ("F2", F2, "0,1,1,2,2,2,3,3,3,3,3", 2), ("F3", F3, "0,1,1,2,2,2,3,3,3,3,3", 2) })
        {
            // 入れ替えの相手は回避を持たない素のトウ（セロだけが動かされた回数を数える）
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Tou, center: def), E5, 4, out var p, out _);
            UnitState sero = U(p, "sero"), tou = U(p, "tou");
            int atk0 = sero.CurrentAttack;
            var got = new List<int>();
            for (int k = 1; k <= 11; k++) { ctx.SwapSlots(tou, sero.Slot, tou); got.Add(EvadeTrait.StageOf(sero)); }
            Expect($"{tag}: 段（動かされた 1〜11 回）", string.Join(",", got), want);
            Expect($"{tag}: 攻撃力 ＝ 素 ＋ {atkPer} × 動かされた回数（避けていない）", sero.CurrentAttack - atk0, atkPer * 11);
        }
        {
            // 乱れ撃ちの矢の数: 段2 は 5 本・段3 は F3 だけ 7 本
            foreach (var (tag, def, moves, want) in new[] { ("F3 段2", F3, 4, 5), ("F3 段3", F3, 7, 7), ("F2 段3", F2, 7, 5), ("F0 段3", F0, 10, 5), ("F1 段3", F1, 7, 5) })
            {
                var ctx = Ctx(Formation.Build(center: def), E5, 7, out var p, out _);
                UnitState sero = p[0];
                sero.SetCounter(EvadeTrait.MovesKey, moves);
                long a0 = Tal(ctx, sero).EvArrows;
                BarrageM.Invoke(ctx, new object[] { sero });
                Expect($"{tag}（動かされた {moves} 回）の乱れ撃ちの矢", Tal(ctx, sero).EvArrows - a0, (long)want);
            }
        }
        {
            // 移り木: H0 は +10・H1 は受け手の最大HPの 20%（溢れは切る）
            foreach (var (tag, def, deficit, want) in new[] { ("H0", H0, 50, 10), ("H1", H1, 50, 92 * 20 / 100), ("H1 溢れ", H1, 5, 5), ("H2", H2, 50, 92 * 20 / 100) })
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.Yomi, center: def, back3: UnitCatalog.Tou), E5, 8, out var p, out _);
                UnitState yomi = U(p, "yomi"), tou = U(p, "tou");
                yomi.Hp = yomi.MaxHp - deficit;
                int hp0 = yomi.Hp;
                ctx.SwapSlots(tou, yomi.Slot, tou);   // トウがヨミを動かす（シオの隣でなくても移り木は反応する）
                Expect($"{tag}: 動かされたヨミの回復（欠け {deficit}）", yomi.Hp - hp0, want);
                Expect($"{tag}: 手当ては出ない（トウの入れ替え）", Tal(ctx, U(p, "shio")).TendFires, 0L);
            }
        }
        {
            // 手当て: 最も傷ついたヨミを下げる → 中央のシオが前1 へ。ヨミに 移り木 18 ＋ 手当て 18、押し出したシオには何も無い。
            foreach (var (tag, def, tend) in new[] { ("H1", H1, 0), ("H2", H2, 92 * 20 / 100) })
            {
                var f = Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald, center: def, back1: UnitCatalog.Tou, back3: UnitCatalog.Basa);
                var ctx = Ctx(f, E5, 9, out var p, out _);
                UnitState yomi = U(p, "yomi"), shio = U(p, "shio");
                yomi.Hp = yomi.MaxHp / 2; shio.Hp = shio.MaxHp - 30;
                int hy = yomi.Hp, hs = shio.Hp;
                Regroup(ctx, shio);
                Expect($"{tag}: ヨミを中央へ下げた", Seat(yomi), "中央");
                Expect($"{tag}: 下げたヨミの回復（移り木 18 ＋ 手当て {tend}）", yomi.Hp - hy, 92 * 20 / 100 + tend);
                Expect($"{tag}: 押し出したシオ（自分）は癒えない", shio.Hp - hs, 0);
                Expect($"{tag}: 手当ての回数", Tal(ctx, shio).TendFires, tend > 0 ? 1L : 0L);
            }
            {
                // シオ自身が最も傷ついている → シオを下げる。H2 でも手当ては出ない。
                var f = Formation.Build(front1: H2, front3: UnitCatalog.Gald, center: UnitCatalog.Tou, back1: UnitCatalog.Yomi, back3: UnitCatalog.Basa);
                var ctx = Ctx(f, E5, 10, out var p, out _);
                UnitState shio = U(p, "shio");
                shio.Hp = shio.MaxHp / 3;
                int hs = shio.Hp;
                Regroup(ctx, shio);
                Expect("H2: シオ自身を下げた（前1 → 中央）", Seat(shio), "中央");
                Expect("H2: シオ自身を下げたときの手当て", Tal(ctx, shio).TendFires, 0L);
                Expect("H2: シオは癒えない", shio.Hp - hs, 0);
            }
            {
                // 支援拒否（ガルド）を下げても手当ては入らない（`Heal` が弾く）
                var f = Formation.Build(front1: UnitCatalog.Gald, center: H2, back1: UnitCatalog.Tou);
                var ctx = Ctx(f, E5, 11, out var p, out _);
                UnitState gald = U(p, "gald"), shio = U(p, "shio");
                gald.Hp = gald.MaxHp / 2; int hg = gald.Hp;
                Regroup(ctx, shio);
                Expect("H2: 下げたガルド（支援拒否）の HP", gald.Hp - hg, 0);
                Expect("H2: ガルドへの手当ての増分", Tal(ctx, shio).TendGained, 0L);
            }
        }
        Console.WriteLine();

        Console.WriteLine("## (3b) verbose の有無（勝敗・決着ターン・与ダメ）");
        Console.WriteLine();
        {
            long n = 0, diff = 0;
            var combos = new (Formation F, string Tag)[]
            {
                (Apply(S1, F3, H2), "S1 F3×H2"), (Apply(S1, F2, H1), "S1 F2×H1"), (Apply(S2, F3, H2), "S2 F3×H2"), (Apply(S2, F1, H0), "S2 F1×H0"),
                (Apply(K(UnitCatalog.Lili), F3, H0), "K-リリ F3"), (Apply(K(UnitCatalog.Tsugi), F3, H0), "K-ツギ F3"),
            }.Concat(S4Rows().Select(r => (Apply(r.F, F3, H2), "S4 " + r.Name)));
            foreach (var (f, tag) in combos)
                for (int s = 0; s < 2; s++) for (int w = 0; w < WaveNames.Length; w++) for (int seed = 0; seed < 50; seed++)
                {
                    var p1 = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r1 = BattleEngine.Run(p1, WaveOf(w, Scales[s].Sc)(), seed, verbose: true);
                    var p2 = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r2 = BattleEngine.Run(p2, WaveOf(w, Scales[s].Sc)(), seed, verbose: false);
                    n++;
                    long d1 = r1.TallyByUnit.Sum(kv => kv.Value.DamageToEnemy), d2 = r2.TallyByUnit.Sum(kv => kv.Value.DamageToEnemy);
                    if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns || d1 != d2) diff++;
                }
            Expect($"verbose の有無で違った戦（{n:N0} 戦）", diff, 0L);
        }
        {
            // 台本: F3 の乱れ撃ちは 5 本か 7 本で、7 本は段3 に届いた後だけ。H2 の手当ての `Heal` は組み替えの直後で、下げた駒へ。
            int bad = 0, seven = 0, tends = 0, tendBad = 0;
            var f = Apply(S2, F3, H2);
            for (int s = 0; s < 2; s++) for (int w = 0; w < WaveNames.Length; w++) for (int seed = 0; seed < 100; seed++)
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, WaveOf(w, Scales[s].Sc)(), seed, verbose: true);
                int stage = 0;
                foreach (var e in r.Events)
                {
                    if (e.Kind == BattleEventKind.EvadeStage) stage = e.Slot;
                    if (e.Kind == BattleEventKind.Barrage && e.Slot == 1)
                    {
                        if (e.Amount == 7) { seven++; if (stage < 3) bad++; }
                        else if (e.Amount != 5 || stage >= 3) bad++;
                    }
                }
                tends += (int)r.TallyByUnit["shio"].TendFires;
                if (r.TallyByUnit["shio"].TendFires > r.TallyByUnit["shio"].RegroupSwaps) tendBad++;
            }
            Expect("乱れ撃ちの本数が段と合わない手番", bad, 0);
            Console.WriteLine($"  （7 本の乱れ撃ち {seven} 回・手当て {tends} 回）");
            Expect("手当ての回数 ＞ 組み替えの回数になった戦", tendBad, 0);
        }
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**" + (ng == 0 ? " ok=True" : " ok=False"));
    }
}
