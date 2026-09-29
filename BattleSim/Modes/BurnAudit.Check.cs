using System.Reflection;
using BattleCore;
using static Common;

// burnaudit check —— 自己検査（受け入れ 2・3 と表F の帳簿の直の検査）。受け入れ 1（`compare` 305 セル）は CLI の差分で見る。
static partial class BurnAuditDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static int ok, ng;
    static void Expect(string what, object got, object want)
    {
        bool pass = Equals(got, want);
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }
    static BattleContext Ctx(Formation pl, Formation en, int turn, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, false);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, turn);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 100) => new() { Id = id, Name = id, MaxHp = hp, Attack = 5, Speed = 1, Traits = Array.Empty<TraitId>() };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第233期 burnaudit check");
        Console.WriteLine();

        // ① 延焼 ／ ③ 火の受け渡しの帳簿（盤面を直に組む）
        {
            // 敵: 前1 e1（燃えている・倒れる）・中央 e2（燃えていない・隣）・前3 e3（燃えている・隣でない＝前1 と前3 は隣接しない）
            var en = Formation.Build(front1: Plain("e1", 10), front3: Plain("e3"), center: Plain("e2"));
            var ctx = Ctx(Formation.Build(front1: Plain("a1"), center: Plain("a2"), back1: Plain("a3")), en, 2, out var p, out var e);
            ctx.Ignite(U(e, "e1")); ctx.Ignite(U(e, "e3")); ctx.Ignite(U(p, "a1"));
            ctx.ApplyDamage(U(e, "e1"), 50, U(p, "a1"));
            var l = ctx.BurnLinkBook;
            bool adj13 = FormationRules.AreAdjacent(0, 1);
            Expect("前1 と前3 は隣接しない（X 字の表）", adj13, false);
            Expect("① 燃えた敵が倒れた（T2 ＝ 序盤）・隣に生きた敵・隣に燃えていない敵・その和", $"{l.FoeDeaths[0]}/{l.FoeBurnDeaths[0]}/{l.FoeBurnDeathNeighbor[0]}/{l.FoeBurnDeathUnburntNeighbor[0]}/{l.FoeBurnDeathUnburntSum[0]}", "1/1/1/1/1");
            Expect("③ 燃えた味方 a1 の撃破・隣に燃えていない味方（a2 中央・a3 後1 は前1 と隣）・その和", $"{l.AllyKills[0]}/{l.AllyBurnKills[0]}/{l.AllyBurnKillUnburntNeighbor[0]}/{l.AllyBurnKillUnburntSum[0]}", "1/1/1/2");
            var ctx2 = Ctx(Formation.Build(front1: Plain("a1")), Formation.Build(front1: Plain("e1", 10), center: Plain("e2")), 5, out var p2, out var e2);
            ctx2.ApplyDamage(U(e2, "e1"), 50, U(p2, "a1"));
            var l2 = ctx2.BurnLinkBook;
            Expect("燃えていない敵の撃破（T5 ＝ 4 以降）は ① にも ③ にも入らない（撃破の数だけ）", $"{l2.FoeDeaths[1]}/{l2.FoeBurnDeaths[1]}/{l2.AllyKills[1]}/{l2.AllyBurnKills[1]}", "1/0/1/0");
        }
        // ② 火を運ぶ貫き
        {
            // 敵は経路0（前1 → 中央 → 後1）の前1 と後1 だけ（経路1 は空なので経路0 を貫く）。中央を空けて後1 まで届かせる。
            var en = Formation.Build(front1: Plain("e1"), back1: Plain("e3"));
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.HotaL0), en, 1, out var p, out var e);
            var hota = U(p, "hota");
            ctx.Ignite(hota);
            ctx.PerformAttack(hota);
            var l = ctx.BurnLinkBook;
            Expect("② 燃えているホタの貫き・抜いた他の敵（後1）・うち燃えていない", $"{l.PyrePierces[0]}/{l.PyreExtraHits[0]}/{l.PyreExtraUnburnt[0]}", "1/1/1");
            var ctx2 = Ctx(Formation.Build(front1: UnitCatalog.HotaL0), en, 1, out var p2, out _);
            ctx2.PerformAttack(U(p2, "hota"));
            Expect("燃えていないホタ（単体）は ② に入らない", ctx2.BurnLinkBook.PyrePierces[0], 0L);
        }

        // 実戦: verbose の有無・帳簿が verbose に依らない・死因の合計 ＝ 倒れた駒
        var boards = new List<Formation> { RefBurn, RefThunder, RefMove,
            Seat(new[] { UnitCatalog.BorgF0, UnitCatalog.HotaL0, UnitCatalog.HiyoF0, UnitCatalog.Beni, UnitCatalog.Golm }),
            Seat(new[] { UnitCatalog.HotaL0, UnitCatalog.BorgF0, UnitCatalog.HiyoF0, UnitCatalog.Sekki, UnitCatalog.Zoto }) };
        long battles = 0, diff = 0, ledgerDiff = 0, deathMismatch = 0, deaths = 0, unknown = 0, fallenGap = 0;
        var lk = new object();
        foreach (var f in boards)
            for (int w = 0; w < WaveNames.Length; w++)
                foreach (var (_, sc) in Scales)
                    Parallel.For(0, 20, s =>
                    {
                        var (r, p, e, slot0) = Fight(f, w, sc, s, true);
                        var (r2, _, _, _) = Fight(f, w, sc, s, false);
                        bool same = r.PlayerWon == r2.PlayerWon && r.Turns == r2.Turns
                                    && string.Join(",", r.PlayerStarterFallen.OrderBy(x => x)) == string.Join(",", r2.PlayerStarterFallen.OrderBy(x => x));
                        var l1 = r.BurnLink!; var l2 = r2.BurnLink!;
                        bool lsame = l1.FoeBurnDeaths.SequenceEqual(l2.FoeBurnDeaths) && l1.PyreExtraHits.SequenceEqual(l2.PyreExtraHits)
                                     && l1.AllyBurnKills.SequenceEqual(l2.AllyBurnKills) && l1.FavorCalls == l2.FavorCalls
                                     && l1.FoeBurnDeathUnburntNeighbor.SequenceEqual(l2.FoeBurnDeathUnburntNeighbor);
                        var a = new Agg(); a.Take(r, p, e, slot0);
                        lock (lk)
                        {
                            battles++; if (!same) diff++; if (!lsame) ledgerDiff++;
                            deaths += a.DeathEvents; unknown += a.Cause[CUnknown];
                            if (a.Cause.Sum() != a.DeathEvents || a.FellSum != a.DeathEvents) deathMismatch++;
                            if (a.DeathEvents != a.StarterFallenSum) fallenGap++;
                        }
                    });
        Expect($"verbose の有無で勝敗・決着T・倒れた駒が違う戦（{battles} 戦）", diff, 0L);
        Expect("表F の帳簿が verbose の有無で違う戦", ledgerDiff, 0L);
        Expect($"死因の合計 ≠ 倒れた駒の数（`Death` の件数）の戦（倒れた {deaths} 件）", deathMismatch, 0L);
        Console.WriteLine($"- （参考）死因が「その他・不明」だった件数 {unknown} ／ `Death` の件数と戦の終わりの `PlayerStarterFallen` が違う戦（蘇生）{fallenGap}");
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
