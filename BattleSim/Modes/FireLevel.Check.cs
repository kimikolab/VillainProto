using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FS = FireScaleDiag;

// firelevel check —— 自己検査（受け入れ 1・3・4）。台本の突き合わせ（R0 ＝ 第241期）は `firelevel digest` で見る。
static partial class FireLevelDiag
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
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 1000, int atk = 10, AttackPattern pat = AttackPattern.Single, params TraitId[] tr)
        => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = tr, Pattern = pat };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static void SetLv(UnitState u, int lv) { u.SetCounter(StatusKeys.Burn, 3); u.SetCounter(FireLevelRule.LvKey, lv); }

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第242期 firelevel check");
        Console.WriteLine();

        // ---- 保つ火 ----
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a", tr: TraitId.FireLevel), front3: Plain("b")), Formation.Build(front1: Plain("e1")), out var p, out var e);
            var a = U(p, "a"); var b = U(p, "b"); var en = U(e, "e1");
            ctx.Ignite(b, friendly: true, source: a);
            Expect("保つ火: 燃えていなかった駒に点くと 1", FireLevelRule.Of(b), 1);
            SetLv(a, 3);
            ctx.Ignite(a, friendly: true, source: a);
            Expect("保つ火: 火勢3 の駒に点け直しても 3 のまま", FireLevelRule.Of(a), 3);
            ctx.Ignite(en, source: a);
            Expect("敵は火勢を持たない（点いても私有キーは 0）", en.RawCounter(FireLevelRule.LvKey), 0);
            a.SetCounter(StatusKeys.Burn, 1); a.SetCounter(FireLevelRule.LvKey, 3);
            typeof(BattleContext).GetMethod("TickStatuses")!.Invoke(ctx, Array.Empty<object>());
            Expect("消える: 燃焼が切れたら 0", $"{FireLevelRule.Of(a)}/{a.RawCounter(FireLevelRule.LvKey)}", "0/0");
        }

        // ---- 燃え広がり（薙ぎ: 前列2体が燃えている・中央は燃えていない → +2） ----
        {
            var borg = Plain("borg", atk: 5, pat: AttackPattern.Sweep, tr: TraitId.FireLevel);
            var ctx = Ctx(Formation.Build(front1: borg), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec")), out var p, out var e);
            var bu = U(p, "borg");
            SetLv(bu, 1);
            U(e, "e1").SetCounter(StatusKeys.Burn, 3); U(e, "e3").SetCounter(StatusKeys.Burn, 3);
            ctx.PerformAttack(bu);
            Expect("燃え広がり: 当たる前から燃えていた敵2体 → +2", FireLevelRule.Of(bu), 3);
            Expect("燃え広がり: 帳簿（相手の数）", ctx.FireBook.SpreadHits, 2L);
            // 本人が燃えていなければ育たない
            var ctx2 = Ctx(Formation.Build(front1: borg), Formation.Build(front1: Plain("e1"), front3: Plain("e3")), out var p2, out var e2);
            U(e2, "e1").SetCounter(StatusKeys.Burn, 3); U(e2, "e3").SetCounter(StatusKeys.Burn, 3);
            ctx2.PerformAttack(U(p2, "borg"));
            Expect("燃え広がり: 本人が燃えていなければ 0（点いてもいない）", $"{FireLevelRule.Of(U(p2, "borg"))}/{ctx2.FireBook.SpreadHits}", "0/0");
        }

        // ---- ホタの段3: 同じ敵に 5 回・燃え広がりは同じ敵で1回・「当たる前から」 ----
        {
            var hota = new UnitDef { Id = "hota", Name = "hota", MaxHp = 1000, Attack = 5, Speed = 1, Traits = new[] { TraitId.Pyre, TraitId.PyreStage, TraitId.FireLevel } };
            var ctx = Ctx(Formation.Build(front1: hota), Formation.Build(front1: Plain("e1"), front3: Plain("e3")), out var p, out var e);
            var h = U(p, "hota"); var e1 = U(e, "e1"); var e3 = U(e, "e3");
            SetLv(h, 3);
            e1.SetCounter(StatusKeys.Burn, 3);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(h);
            var atks = ctx.Events.Skip(n0).Where(x => x.Kind == BattleEventKind.Attack && x.ActorId == h.InstanceId).ToList();
            var dmg = ctx.Events.Skip(n0).Where(x => x.Kind == BattleEventKind.Damage && x.ActorId == h.InstanceId).Select(x => x.TargetId).Distinct().Count();
            Expect("段3: 1手番に5回振る", atks.Count, 5);
            Expect("段3: 5回とも同じ敵（倒れていない）", dmg, 1);
            Expect("段3: 1回 ×1.6（攻 5 ×1.6 ＝ 8）", atks[0].Amount, 8);
            var hitFoe = e1.Hp < 1000 ? e1 : e3;
            Expect("段3: 燃え広がりは同じ敵で1回まで（当たる前から燃えていた敵なら +1、でなければ +0）",
                FireLevelRule.Of(h), hitFoe == e1 ? 4 : 3);
            Expect("段3: 当てた敵に着火（保つ火）", hitFoe.RawCounter(StatusKeys.Burn) > 0, true);
            // 段2: 貫き・当てた敵全員に着火
            var ctx2 = Ctx(Formation.Build(front1: hota), Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec"), back1: Plain("eb"), back3: Plain("eb3")), out var p2, out var e2);
            var h2 = U(p2, "hota"); SetLv(h2, 2);
            ctx2.TakeTurn(h2);
            var hit2 = e2.Where(x => x.Hp < 1000).ToList();
            Expect("段2: 貫きで当てた敵が全員燃える（当てた数 ＝ 燃えた数）", $"{hit2.Count(x => x.RawCounter(StatusKeys.Burn) > 0)}/{hit2.Count}", "3/3");
            Expect("段2: 振るのは1回", ctx2.Events.Count(x => x.Kind == BattleEventKind.Attack && x.ActorId == h2.InstanceId), 1);
            Expect("段2: 攻撃型は貫き", ctx2.Events.First(x => x.Kind == BattleEventKind.Attack).Pattern, (AttackPattern?)AttackPattern.Pierce);
        }

        // ---- 萎む ----
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a", tr: TraitId.FireLevel), front3: Plain("b"), center: Plain("c")), Formation.Build(front1: Plain("e1")), out var p, out var e);
            var a = U(p, "a"); var b = U(p, "b"); var c = U(p, "c");
            SetLv(a, 3); SetLv(b, 3); SetLv(c, 1);
            b.SetCounter(FireLevelRule.GrewKey, 1);   // このターンに育った
            ctx.WiltFire();
            Expect("萎む: 育たなかった駒は −1 ／ 育った駒はそのまま ／ 1 の駒は 1 未満にならない", $"{FireLevelRule.Of(a)}/{FireLevelRule.Of(b)}/{FireLevelRule.Of(c)}", "2/3/1");
        }

        // ---- ギフト（直に）: 火勢3 で1体・並び・撃った後 1 ／ 粛の窓口を通らない ----
        {
            var hiyo = new UnitDef { Id = "hiyo", Name = "hiyo", MaxHp = 1000, Attack = 5, Speed = 1, Traits = new[] { TraitId.FireStoke, TraitId.TurnGift },
                Actions = new UnitAction[] { new(ActionKind.Skill, Label: "火のそばを見ている") } };
            var ctx = Ctx(Formation.Build(front1: hiyo, front3: Plain("x", atk: 50), center: Plain("y", atk: 10), back1: Plain("z", atk: 99)),
                Formation.Build(front1: Plain("e1"), front3: Plain("hush", tr: TraitId.Hush)), out var p, out var e);
            var hy = U(p, "hiyo"); var x = U(p, "x"); var y = U(p, "y"); var z = U(p, "z");
            SetLv(hy, 3); SetLv(x, 2); SetLv(y, 3); SetLv(z, 2);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            var gifts = ctx.Events.Skip(n0).Where(ev => ev.Kind == BattleEventKind.FireLevel && ev.Text == FireLevelLabels.Gift).ToList();
            Expect("ギフト: 火勢3 で1体・相手は火勢の高い順（攻撃力より先）", string.Join(",", gifts.Select(g => g.TargetId == y.InstanceId ? "y" : g.TargetId == x.InstanceId ? "x" : "z")), "y");
            Expect("ギフト: 撃った後ヒヨは 1", FireLevelRule.Of(hy), 1);
            Expect("ギフト: 相手はヒヨの手番の後に通常の手番を1回（粛の保持者が生きていても動く）", $"{ctx.FireBook.GiftTurns}/{ctx.FireBook.GiftHushTurns}/{ctx.FireBook.GiftHushAttacks}", "1/1/1");
            Expect("ギフト: 相手の手番は攻撃（ターン外の行動ではない）",
                ctx.Events.Skip(n0).Any(ev => ev.Kind == BattleEventKind.Attack && ev.ActorId == y.InstanceId), true);
            // 火勢4: 2体・同じ火勢は攻撃力順
            SetLv(hy, 4); SetLv(x, 3); SetLv(y, 2); SetLv(z, 3);
            n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            gifts = ctx.Events.Skip(n0).Where(ev => ev.Kind == BattleEventKind.FireLevel && ev.Text == FireLevelLabels.Gift).ToList();
            Expect("ギフト: 火勢4 で2体・同じ火勢は攻撃力の高い順", string.Join(",", gifts.Select(g => g.TargetId == y.InstanceId ? "y" : g.TargetId == x.InstanceId ? "x" : "z")), "z,x");
            // 火勢2: 煽る（攻撃力最大・火勢4 は除く）＋ 自分 +1
            SetLv(hy, 2); SetLv(x, 4); SetLv(y, 1); SetLv(z, 2);
            ctx.TakeTurn(hy);
            Expect("煽り: 火勢4 の駒は除き、攻撃力最大（z）を +1・ヒヨも +1", $"{FireLevelRule.Of(z)}/{FireLevelRule.Of(x)}/{FireLevelRule.Of(hy)}", "3/4/3");
        }
        {
            var hiyo = new UnitDef { Id = "hiyo", Name = "hiyo", MaxHp = 1000, Attack = 5, Speed = 1, Traits = new[] { TraitId.FireStoke, TraitId.TurnGiftWait },
                Actions = new UnitAction[] { new(ActionKind.Skill, Label: "火のそばを見ている") } };
            var ctx = Ctx(Formation.Build(front1: hiyo, front3: Plain("x", atk: 50)), Formation.Build(front1: Plain("e1")), out var p, out var e);
            var hy = U(p, "hiyo"); var x = U(p, "x");
            SetLv(hy, 3); SetLv(x, 2);
            ctx.TakeTurn(hy);
            Expect("G4: 火勢3 では撃たずに煽る", $"{ctx.FireBook.Gifts}/{FireLevelRule.Of(x)}/{FireLevelRule.Of(hy)}", "0/3/4");
        }

        // ---- 盤面の戦（R3・台本と帳簿の突き合わせ）----
        Console.WriteLine();
        Console.WriteLine("### 盤面の戦（T3-238 と 雷＋ボルグ × 版 R1〜R3g4 × 波6 × 倍率3 × seed 0..39）");
        Console.WriteLine();
        long battles = 0, snapBad = 0, stageBad = 0, stageN = 0, giftBad = 0, giftN = 0, spentBad = 0, wiltBad = 0, wiltN = 0, verbDiff = 0, detDiff = 0;
        long causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, hushGiftAtk = 0, hushGiftTurns = 0;
        var lk = new object();
        foreach (var v in Versions.Skip(1))
            foreach (var bf in new[] { FS.T3, FS.ThunderBorg })
            {
                var f = Apply(bf, v);
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                        Parallel.For(0, 40, seed =>
                        {
                            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var en = BA.WaveOf(w, BA.Scales[s].Sc)();
                            var slotOf = pl.Concat(en).ToDictionary(u => u, u => u.Slot);
                            var r = BattleEngine.Run(pl, en, seed, verbose: true);
                            var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BA.WaveOf(w, BA.Scales[s].Sc)(), seed, verbose: false);
                            var r3 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BA.WaveOf(w, BA.Scales[s].Sc)(), seed, verbose: true);
                            var agg = new BA.Agg(); agg.Take(r, pl, en, slot0);
                            var st = Audit(r, pl, v);
                            lock (lk)
                            {
                                battles++;
                                snapBad += st.SnapBad; stageBad += st.StageBad; stageN += st.StageN; giftBad += st.GiftBad; giftN += st.GiftN; spentBad += st.SpentBad; wiltBad += st.WiltBad; wiltN += st.WiltN;
                                if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns || r.PlayerSurvivors != r2.PlayerSurvivors
                                    || r.FireLevels!.Gifts != r2.FireLevels!.Gifts || r.FireLevels.SpreadHits != r2.FireLevels.SpreadHits || r.FireLevels.Wilts != r2.FireLevels.Wilts) verbDiff++;
                                if (r.Events.Count != r3.Events.Count || r.Log.Count != r3.Log.Count || r.Turns != r3.Turns) detDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                                hushGiftTurns += r.FireLevels.GiftHushTurns; hushGiftAtk += r.FireLevels.GiftHushAttacks;
                            }
                        });
            }
        Expect($"写し: 燃えている駒の火勢は 1〜4・燃えていない駒は 0（{battles} 戦）", snapBad, 0L);
        Expect($"段: 手番の時点の火勢どおりの型（段1 単体1回 ／ 段2 貫き1回 ／ 段3 以上 単体・5回まで）（{stageN} 手番）", stageBad, 0L);
        Expect($"ギフト: ヒヨの火勢 3 以上（G4 は 4）・相手の火勢は相手でない燃えている味方以上（{giftN} 件）", giftBad, 0L);
        Expect("ギフト: 撃った後ヒヨは 1（次の火勢の出来事が「撃った」→ 1）", spentBad, 0L);
        Expect($"萎む: そのターンに育った駒は萎まない・萎んだ後も 1 以上（{wiltN} 件）", wiltBad, 0L);
        Expect("verbose の有無で勝敗・決着T・生存・ギフト・燃え広がり・萎むが同じ", verbDiff, 0L);
        Expect("同じ seed を2度回して台本の長さが同じ（乱数の口を足していない）", detDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 4）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);
        Expect($"粛の下のギフトの手番が攻撃になった（粛の下の手番 {hushGiftTurns}）", hushGiftAtk > 0, true);

        // ---- `compare`: R0 が docs/balance.md と一致 ----
        var rates = CompareRates();
        var rows = CompareBuilds();
        var bal = File.ReadAllLines("docs/balance.md", System.Text.Encoding.UTF8).Where(l => l.StartsWith("| ") && l.Contains('%')).ToList();
        int cellBad = 0, cells = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            var line = bal.FirstOrDefault(l => l.StartsWith("| " + rows[i].Name + " |"));
            if (line is null) { cellBad += 5; continue; }
            var c = line.Split('|').Select(x => x.Trim()).Where(x => x.EndsWith('%')).ToArray();
            for (int stg = 0; stg < 5; stg++) { cells++; if (Math.Abs(double.Parse(c[stg].TrimEnd('%')) - rates[0, i, stg]) > 0.01) cellBad++; }
        }
        Expect($"R0 の `compare` が docs/balance.md と一致（{cells} セル）", cellBad, 0);
        bool Has(Formation f) => f.Occupied().Any(o => o.Def.Id is "borg" or "hota" or "hiyo");
        int noMoved = 0;
        for (int v = 1; v < Versions.Length; v++)
            for (int i = 0; i < rows.Length; i++)
                if (!Has(rows[i].F)) for (int stg = 0; stg < 5; stg++) if (rates[v, i, stg] != rates[0, i, stg]) noMoved++;
        Expect("ボルグ・ホタ・ヒヨのいない行は どの版でも 0 セル（受け入れ 1）", noMoved, 0);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    readonly record struct AuditStat(long SnapBad, long StageBad, long StageN, long GiftBad, long GiftN, long SpentBad, long WiltBad, long WiltN);

    /// <summary>1戦の台本と帳簿を突き合わせる（火勢は台本の出来事から組み直す）。</summary>
    static AuditStat Audit(BattleResult r, List<UnitState> p, Ver v)
    {
        long snapBad = 0, stageBad = 0, stageN = 0, giftBad = 0, giftN = 0, spentBad = 0, wiltBad = 0, wiltN = 0;
        foreach (var s in r.FireLevels!.Snaps)
            if (s.Burning ? s.Level is < 1 or > 4 : s.Level != 0) snapBad++;
        var ev = r.Events;
        var lv = new Dictionary<int, int>();
        var grewTurn = new Dictionary<int, int>();
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        int hiyo = p.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId ?? -1;
        bool wait = v.Hiyo.Traits.Contains(TraitId.TurnGiftWait);
        int? pendingSpent = null;
        // 手番の枠: ホタの段の手番の攻撃
        var handOf = r.Hands.Where(h => mine.Contains(h.ActorId)).ToList();
        for (int i = 0; i < ev.Count; i++)
        {
            var x = ev[i];
            if (x.Kind != BattleEventKind.FireLevel || x.TargetId is not int t) continue;
            switch (x.Text)
            {
                case FireLevelLabels.Lit: case FireLevelLabels.Out: case FireLevelLabels.Wilt: case FireLevelLabels.Spent:
                case FireLevelLabels.GrowSpread: case FireLevelLabels.GrowStoke: case FireLevelLabels.GrowSelf:
                    if (x.Text is FireLevelLabels.GrowSpread or FireLevelLabels.GrowStoke or FireLevelLabels.GrowSelf) grewTurn[t] = x.Turn;
                    if (x.Text == FireLevelLabels.Wilt)
                    {
                        wiltN++;
                        if (grewTurn.GetValueOrDefault(t) == x.Turn || x.Amount < 1) wiltBad++;
                    }
                    if (pendingSpent == t) { if (x.Text != FireLevelLabels.Spent || x.Amount != 1) spentBad++; pendingSpent = null; }
                    lv[t] = x.Amount;
                    break;
                case FireLevelLabels.Gift:
                    giftN++;
                    int hl = x.Amount;
                    if (hl < (wait ? 4 : 3) || (x.Slot == 2 && hl < 4)) giftBad++;
                    int tl = lv.GetValueOrDefault(t);
                    foreach (var (id, l) in lv) if (mine.Contains(id) && id != hiyo && id != t && l > tl && x.Slot == 1) giftBad++;
                    if (x.Slot == 1) pendingSpent = hiyo;
                    break;
            }
        }
        // 段（手番の頭の出来事 → その手番の攻撃）
        foreach (var h in handOf)
        {
            var st = Enumerable.Range(h.EventStart, Math.Max(0, Math.Min(h.EventEnd, ev.Count) - h.EventStart)).Select(j => ev[j])
                .FirstOrDefault(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Stage && x.ActorId == h.ActorId);
            if (st is null) continue;
            stageN++;
            var atks = Enumerable.Range(h.EventStart, Math.Min(h.EventEnd, ev.Count) - h.EventStart).Select(j => ev[j])
                .Where(x => x.Kind == BattleEventKind.Attack && x.ActorId == h.ActorId && !x.Reaction).ToList();
            if (atks.Count == 0) continue;
            bool good = st.Amount switch
            {
                0 or 1 => atks.Count == 1 && atks[0].Pattern == AttackPattern.Single,
                2 => atks.Count == 1 && atks[0].Pattern == AttackPattern.Pierce,
                _ => atks.Count <= PyreStageTrait.BurstHits && atks.All(a => a.Pattern == AttackPattern.Single),
            };
            if (!good) stageBad++;
        }
        return new AuditStat(snapBad, stageBad, stageN, giftBad, giftN, spentBad, wiltBad, wiltN);
    }
}
