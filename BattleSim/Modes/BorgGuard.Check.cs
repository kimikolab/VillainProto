using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// borgguard check —— 自己検査（受け入れ 1〜5）。1 の `compare` 305 セルは CLI の差分でも見る。
static partial class BorgGuardDiag
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
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e, bool verbose = true)
    {
        var ctx = new BattleContext(0, verbose);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 100, int atk = 10, AttackPattern pat = AttackPattern.Single)
        => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = pat };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static UnitTally Tly(BattleContext c) => ((Dictionary<string, UnitTally>)typeof(BattleContext)
        .GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!)["borg"];
    static int Burn(UnitState u) => u.RawCounter(StatusKeys.Burn);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第234期 borgguard check");
        Console.WriteLine();
        var g1 = VerBorg("G1"); var g2 = VerBorg("G2");

        // ① 着火の条件（盤面を直に組む・ボルグは前1 に独り＝敵の単体は必ずボルグを狙う）
        {
            var ctx = Ctx(Formation.Build(front1: g1, back1: Plain("a1")), Formation.Build(front1: Plain("e1")), out var p, out var e);
            var borg = U(p, "borg"); var e1 = U(e, "e1"); var a1 = U(p, "a1");
            ctx.ApplyDamage(borg, 5, a1, isFriendlyFire: true);                 // 味方の刃
            ctx.ApplyDamage(borg, 5, null);                                     // 毒の刻み（出どころ null）
            ctx.ApplyDamage(borg, 5, e1, burnTick: true);                       // 燃焼の刻み
            ctx.ApplyDamage(borg, 5, e1);                                       // 枠の外の敵の直のダメージ（棘の反射など）
            Expect("① 味方の刃・毒・燃焼の刻み・枠の外の敵の直のダメージでは点かない（ボルグ ／ 敵）", $"{Burn(borg)}/{Burn(e1)}", "0/0");
            int hp0 = borg.Hp;
            ctx.PerformAttack(e1);
            Expect("① 敵の攻撃を受けた: 殴った敵と自分に火（燃えていない一撃は半分にならない）", $"{Burn(borg) > 0}/{Burn(e1) > 0}/{hp0 - borg.Hp}", "True/True/10");
            var t = Tly(ctx);
            Expect("帳簿: 敵に点けた ／ 自分に点けた ／ 半減", $"{t.FireArmorFoeLit}/{t.FireArmorSelfLit}/{t.FireArmorGuardHits}", "1/1/0");
            int hp1 = borg.Hp;
            ctx.PerformAttack(e1);
            Expect("② 燃えている間の敵の一撃は半分（10 → 5）", hp1 - borg.Hp, 5);
            int hp2 = borg.Hp;
            ctx.ApplyDamage(borg, 7, a1, isFriendlyFire: true);
            Expect("② 燃えている間は種類を問わず半分・切り上げ（味方の刃 7 → 4）", hp2 - borg.Hp, 4);
        }
        // ① 薙ぎの巻き込みで当たっても、殴った敵1体に1回
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a1"), front3: g1, center: Plain("a2")), Formation.Build(front1: Plain("e1", atk: 6, pat: AttackPattern.Sweep)), out var p, out var e);
            var borg = U(p, "borg"); var e1 = U(e, "e1");
            ctx.PerformAttack(e1);
            var t = Tly(ctx);
            int evs = ctx.Events.Count(x => x.Kind == BattleEventKind.FireArmor && x.Text == FireArmorLabels.Foe);
            Expect("① 薙ぎ（主目標か巻き込み）で当たっても殴った敵に1回・自分に1回", $"{t.FireArmorFoeLit}/{t.FireArmorSelfLit}/{evs}/{Burn(e1) > 0}", "1/1/1/True");
            int attackEv = ctx.Events.ToList().FindIndex(x => x.Kind == BattleEventKind.Attack);
            int fireEv = ctx.Events.ToList().FindIndex(x => x.Kind == BattleEventKind.FireArmor);
            int lastDmg = ctx.Events.ToList().FindLastIndex(x => x.Kind == BattleEventKind.Damage);
            Expect("台本: 火の鎧の着火は攻撃の Damage がすべて出た後", fireEv > lastDmg && lastDmg > attackEv, true);
        }
        // ③ 焼かれない（燃焼の刻み）
        {
            var ctx = Ctx(Formation.Build(front1: g1), Formation.Build(front1: Plain("e1")), out var p, out _);
            var borg = U(p, "borg");
            ctx.Ignite(borg);
            int hp0 = borg.Hp;
            typeof(BattleContext).GetMethod("TickStatuses", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(ctx, Array.Empty<object>());
            Expect("③ 燃焼の刻みで HP が減らない（残りターンは減る）", $"{hp0 - borg.Hp}/{Burn(borg)}", $"0/{BurnRules.Turns - 1}");
            var ctx0 = Ctx(Formation.Build(front1: UnitCatalog.BorgF0), Formation.Build(front1: Plain("e1")), out var p0, out _);
            var b0 = U(p0, "borg"); ctx0.Ignite(b0); int h0 = b0.Hp;
            typeof(BattleContext).GetMethod("TickStatuses", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(ctx0, Array.Empty<object>());
            Expect("（対照）G0 のボルグは刻みで減る", h0 - b0.Hp, BurnRules.Damage);
        }
        // ④ 焼け残り
        {
            var ctx = Ctx(Formation.Build(front1: g2), Formation.Build(front1: Plain("e1", atk: 500)), out var p, out var e);
            var borg = U(p, "borg"); var e1 = U(e, "e1");
            ctx.Ignite(borg);
            ctx.PerformAttack(e1);
            var t = Tly(ctx);
            Expect("④ 燃えている間の倒れる一撃: HP1・火が消える・その攻撃で自分に点けない・殴った敵には点く",
                $"{borg.Hp}/{Burn(borg)}/{t.FireArmorSelfLit}/{t.FireArmorFoeLit}/{Burn(e1) > 0}/{t.SmolderUsed}", "1/0/0/1/True/1");
            ctx.PerformAttack(e1);
            Expect("④ 火が消えているので2発目で倒れる（燃えていなければ働かない）", borg.IsAlive, false);
            var ctx2 = Ctx(Formation.Build(front1: g2), Formation.Build(front1: Plain("e1", atk: 500)), out var p2, out var e2);
            var b2 = U(p2, "borg");
            ctx2.Ignite(b2); ctx2.PerformAttack(U(e2, "e1"));
            ctx2.Ignite(b2); ctx2.PerformAttack(U(e2, "e1"));
            Expect("④ 1戦1回（燃え直しても2回目は働かない）", $"{b2.IsAlive}/{Tly(ctx2).SmolderUsed}", "False/1");
        }

        // 実戦: verbose の有無・死因の合計 ＝ 倒れた駒・台本の件数 ＝ 帳簿・ボルグは刻みで削られない
        var boards = new List<Formation>();
        foreach (var (ver, _, borg) in Versions.Skip(1))
        {
            boards.Add(BA.Seat(new[] { borg, UnitCatalog.Sasa, UnitCatalog.HiyoF0, UnitCatalog.Shio, UnitCatalog.HotaL0 }));
            boards.Add(BA.Seat(new[] { UnitCatalog.HotaL0, borg, UnitCatalog.HiyoF0, UnitCatalog.Kado, UnitCatalog.Nel }));
        }
        long battles = 0, diff = 0, deathMismatch = 0, deaths = 0, evMismatch = 0, borgTick = 0, fireEvents = 0;
        var lk = new object();
        foreach (var f in boards)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                foreach (var (_, sc) in BA.Scales)
                    Parallel.For(0, 10, s =>
                    {
                        var (r, p, e, slot0) = BA.Fight(f, w, sc, s, true);
                        var (r2, _, _, _) = BA.Fight(f, w, sc, s, false);
                        bool same = r.PlayerWon == r2.PlayerWon && r.Turns == r2.Turns
                                    && string.Join(",", r.PlayerStarterFallen.OrderBy(x => x)) == string.Join(",", r2.PlayerStarterFallen.OrderBy(x => x));
                        var t1 = r.TallyByUnit["borg"]; var t2 = r2.TallyByUnit["borg"];
                        same &= t1.FireArmorFoeLit == t2.FireArmorFoeLit && t1.FireArmorSaved == t2.FireArmorSaved && t1.SmolderUsed == t2.SmolderUsed;
                        var x = new XAgg(); x.Take(r, p, e, slot0);
                        int evs = r.Events.Count(ev => ev.Kind == BattleEventKind.FireArmor);
                        long want = t1.FireArmorFoeLit + t1.FireArmorSelfLit + t1.FireArmorGuardHits + t1.SmolderUsed;
                        lock (lk)
                        {
                            battles++; if (!same) diff++;
                            deaths += x.A.DeathEvents;
                            if (x.A.Cause.Sum() != x.A.DeathEvents || x.A.FellSum != x.A.DeathEvents) deathMismatch++;
                            if (evs != want) evMismatch++;
                            fireEvents += evs;
                            borgTick += x.A.Taken.GetValueOrDefault(UnitCatalog.BorgF0.Name)?[3] ?? 0;
                        }
                    });
        Expect($"verbose の有無で勝敗・決着T・倒れた駒・火の鎧の帳簿が違う戦（{battles} 戦）", diff, 0L);
        Expect($"死因の合計 ≠ 倒れた駒の数の戦（倒れた {deaths} 件）", deathMismatch, 0L);
        Expect($"台本の FireArmor の件数 ≠ 帳簿（着火 敵＋自分＋半減＋焼け残り）の戦（台本 {fireEvents} 件）", evMismatch, 0L);
        Expect("ボルグが燃焼の刻みで削られた量（全戦）", borgTick, 0L);

        // 乱数: 新しい処理は Roll / PickOne を呼ばない（本文の走査）
        string src = File.ReadAllText(Path.Combine("BattleCore", "BattleEngine.cs")).Replace("\r\n", "\n");
        var spans = new List<string>();
        // 第250期 前段: 第1の区間（`FireArmorFrame` 〜 `PerformAttackFooting`）には第242〜249期の火勢の処理（火の雨の `Roll`）が後から入ったので、
        // 第234期の本文だけ（枠のクラス 〜 第242期の見出し ／ `PerformAttackFramed` 〜 `PerformAttackFooting`）の2区間に割った。
        foreach (var (a, b) in new[] { ("sealed class FireArmorFrame", "// 第242期 —— 火勢（燃え広がり"), ("void PerformAttackFramed(", "void PerformAttackFooting("), ("// 火の鎧（第234期・`FireArmorTrait`）", "// 巨躯: 自分より前の列"), ("// 焼け残り（第234期・`SmolderTrait`）", "// 必死の逃げ足（第227期") })
        {
            int i = src.IndexOf(a, StringComparison.Ordinal), j = src.IndexOf(b, i, StringComparison.Ordinal);
            if (i < 0 || j < 0) throw new InvalidOperationException("走査の目印が見つからない（R034）: " + a);
            spans.Add(src[i..j]);
        }
        Expect("新しい処理の本文に Roll( / PickOne( が無い（4 区間）", spans.Count(s => s.Contains("Roll(") || s.Contains("PickOne(")), 0);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
