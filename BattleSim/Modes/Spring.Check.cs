using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// spring check（第228期） —— 自己検査（受け入れ 2〜4・7）
//
// (4) 盤面を直に組んで: 吹っ飛ばしの後も経路の駒のいる席の並びが変わらない／A が経路の最後尾／軌跡の敵すべてにダメージ／
//     行が前に変わった敵だけが混乱（バサの対）／弾き返しは敵の攻撃の被弾だけ・1ターンの回数の上限どおり・粛で止まる／経路に属さない席の敵は弾かない
// (3) ハネのいない戦は H0 と一致 ／ verbose の有無で勝敗・決着T・与ダメが同じ（H1〜H3w）
// (7) 台本: Blast の後に経路の全員への Damage と並べ替えの Move・Spring の後に Move と転倒
// (2) H0 の台本 ＝ 前段（`shockdigest h228` を実装の前後で突き合わせる・ここでは回さない）
// =====================================================================================
static partial class SpringDiag
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

    /// <summary>味方と、席を直接指定した敵（9 枠）を置いた盤面。</summary>
    static BattleContext Ctx9(Formation pl, (int Slot, UnitDef Def)[] foes, int seed, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(seed, false);
        p = BattleEngine.Materialize(OldYomiShio(pl), BattleContext.PlayerTeam);
        var wave = EnemyWave.Of(foes.Select(f => (f.Slot, f.Def)).ToArray());
        e = BattleEngine.MaterializeEnemy(wave, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitState U(List<UnitState> p, string id) => p.First(u => u.Def.Id == id);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        Console.WriteLine("# 第228期 spring check");
        Console.WriteLine();
        if (Versions.Length < 5) { Console.WriteLine("版の札が見つからない（Phase 0 のコミット）。"); return; }
        UnitDef h1 = VerOf("H1").Hane, h2 = VerOf("H2").Hane;
        var recruit = EnemyCatalog.TestStages[0].Enemy.Occupied().Select(o => o.Def).First();

        UnitDef strong = new() { Id = "strongfoe", Name = "強い敵", MaxHp = 500, Attack = 30, Speed = 1, Traits = Array.Empty<TraitId>() };
        UnitDef tough = new() { Id = "toughfoe", Name = "硬い敵", MaxHp = 500, Attack = 5, Speed = 1, Traits = Array.Empty<TraitId>() };

        Console.WriteLine("## (4) 盤面を直に組んで");
        Console.WriteLine();
        // ---- 吹っ飛ばし（9体・前3 の A） ----
        {
            var foes = new (int, UnitDef)[] { (0, tough), (1, strong), (2, tough), (3, tough), (4, tough), (5, tough), (6, tough), (7, tough), (8, tough) };
            // 突風（バサが動かされると吹く）と軋み（ヨミ）の割り込みがダメージを混ぜないように、バサは突風の札を抜き、中央はトウにする
            var basaNoSquall = DecoyDiag.Copy(UnitCatalog.BasaG0, UnitCatalog.BasaG0.Traits.Where(x => x != TraitId.Squall).ToArray());
            var ctx = Ctx9(Formation.Build(front1: h1, front3: basaNoSquall, center: UnitCatalog.Tou, back1: UnitCatalog.Shio, back3: UnitCatalog.Sero), foes, 1, out var p, out var e);
            UnitState hane = U(p, "hane");
            var laneSeats = FormationRules.LanePath(1);   // 前3 → 中央 → ○中3 → 後3
            var before = laneSeats.Select(s => e.First(u => u.Slot == s)).ToList();
            var hp0 = before.ToDictionary(u => u, u => u.Hp);
            var rows0 = before.ToDictionary(u => u, u => u.Row);
            BlastTrait.Act(ctx, hane);
            var after = laneSeats.Select(s => e.FirstOrDefault(u => u.IsAlive && u.Slot == s)).ToList();
            Expect("吹っ飛ばし: A（前3・攻30）が経路の最後尾（後3）へ", after[3] == before[0], true);
            Expect("吹っ飛ばし: 残りは1つずつ前へ詰める（中央 → 前3・○中3 → 中央・後3 → ○中3）", after[0] == before[1] && after[1] == before[2] && after[2] == before[3], true);
            Expect("吹っ飛ばし: 経路の席に駒がいる並びは変わらない（4 席とも埋まっている）", after.All(u => u is not null), true);
            Expect("吹っ飛ばし: 軌跡の敵すべてにダメージ（4 体）", before.Count(u => u.Hp < hp0[u]), 4);
            Expect("吹っ飛ばし: ダメージは貫きの減衰どおり（先頭 ≥ 2番目 ≥ 3番目 ≥ 4番目）",
                   Enumerable.Range(0, 3).All(k => hp0[before[k]] - before[k].Hp >= hp0[before[k + 1]] - before[k + 1].Hp), true);
            Expect("吹っ飛ばし: 経路の外（前1・後1・○前2・○後2・○中1）は動かず無傷", e.Where(u => !before.Contains(u)).All(u => u.Hp == u.MaxHp && !laneSeats.Contains(u.Slot)), true);
            Expect("吹っ飛ばし: A は転ぶ", before[0].RawCounter(StatusKeys.Stagger), 1);
            var fwd = before.Where(u => FormationRules.DepthOf(u.Row) < FormationRules.DepthOf(rows0[u])).ToHashSet();
            var conf = e.Where(u => u.RawCounter(StatusKeys.Confused) > 0).ToHashSet();
            Expect("混乱: 行が前に変わった敵（中央 → 前3・後3 → ○中3 の2体）だけ", conf.SetEquals(fwd) && fwd.Count == 2, true);
            Expect("混乱: 中央 ← ○中3（同じ中列）は混乱しない", before[2].RawCounter(StatusKeys.Confused), 0);
            var t = Tal(ctx, hane);
            Expect("帳簿: 吹っ飛ばし 1・動いた 4・前へ 2・当たった 4・混乱 2", $"{t.BlastCount}/{t.BlastMoved}/{t.BlastForward}/{t.BlastHits}/{t.BlastConfused}", "1/4/2/4/2");
            Expect("代金: ハネは隣の味方と入れ替わった", t.OverrunSwaps, 1L);
        }
        // ---- 吹っ飛ばし（空席のある経路・A の後ろに誰もいない経路） ----
        {
            var foes = new (int, UnitDef)[] { (0, strong), (2, tough), (3, tough), (1, tough) };
            var ctx = Ctx9(Formation.Build(front1: h1, front3: UnitCatalog.BasaG0, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Sero), foes, 2, out var p, out var e);
            UnitState a = e.First(u => u.Slot == 0), b = e.First(u => u.Slot == 2), c = e.First(u => u.Slot == 3);
            BlastTrait.Act(ctx, U(p, "hane"));
            Expect("空席: ○中1 は空のまま・B 前1・C 中央・A 後1", $"{b.Slot}/{c.Slot}/{a.Slot}/{e.Any(u => u.Slot == 5)}", "0/2/3/False");
        }
        {
            var foes = new (int, UnitDef)[] { (0, strong) };
            var ctx = Ctx9(Formation.Build(front1: h1, back3: UnitCatalog.Sero), foes, 3, out var p, out var e);
            BlastTrait.Act(ctx, U(p, "hane"));
            var t = Tal(ctx, U(p, "hane"));
            Expect("後ろに誰もいない: A は前1 のまま・転ぶ・動いた 0", $"{e[0].Slot}/{e[0].RawCounter(StatusKeys.Stagger)}/{t.BlastMoved}", "0/1/0");
        }
        // ---- 弾き返し ----
        UnitState Setup(out BattleContext ctx, out List<UnitState> e, int foeSlot, bool basaHush = false, int seed = 5)
        {
            var foes = new List<(int, UnitDef)> { (foeSlot, strong) };
            foreach (int s in new[] { 0, 1, 2, 5, 6, 3, 4, 7, 8 }) if (s != foeSlot && foes.Count < 6) foes.Add((s, tough));
            ctx = Ctx9(Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.BasaG0, center: UnitCatalog.Yomi, back1: h2, back3: UnitCatalog.Shio), foes.ToArray(), seed, out var p, out e);
            return U(p, "hane");
        }
        {
            var hane = Setup(out var ctx, out var e, 0);
            UnitState foe = e.First(u => u.Slot == 0), mid = e.First(u => u.Slot == 2);
            ctx.ApplyDamage(hane, 5, foe, pattern: AttackPattern.Single);
            var t = Tal(ctx, hane);
            Expect("弾き返し: 前1 の敵に殴られる → 中央へ弾く・中央の敵が前1 へ", $"{foe.Slot}/{mid.Slot}/{t.SpringCount}", "2/0/1");
            Expect("弾き返し: 弾いた敵は転ぶ", foe.RawCounter(StatusKeys.Stagger), 1);
            Expect("弾き返し: 前へ出た敵はバサの対で混乱", mid.RawCounter(StatusKeys.Confused), 1);
            Expect("弾き返し: ハネは隣の味方と入れ替わる", t.SpringSwaps, 1L);
            // 同じターンの2回目（段0 なら上限 1）
            ctx.ApplyDamage(hane, 5, mid, pattern: AttackPattern.Single);
            Expect("回数: 段0 は1ターン1回（2回目は上限で止まる）", $"{Tal(ctx, hane).SpringCount}/{Tal(ctx, hane).SpringCapped}", "1/1");
            TurnP.SetValue(ctx, 2);
            ctx.ApplyDamage(hane, 5, mid, pattern: AttackPattern.Single);
            Expect("回数: ターンが替われば戻る", Tal(ctx, hane).SpringCount, 2L);
        }
        {
            var hane = Setup(out var ctx, out var e, 0);
            // 敵の乱れの段3（累計 14）: 上限 4（弾くたびに累計が増えて段が上がるので、段が上がりきった所で見る）
            typeof(BattleContext).GetField("_disorder", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ctx, new[] { 0, 14 });
            int stage = DisarrayTrait.StageOf(ctx, hane);
            UnitState foe = e.First(u => u.Slot == 0);
            for (int i = 0; i < 7; i++) { var f = e.FirstOrDefault(u => u.IsAlive && u.Slot is 0 or 1) ?? foe; ctx.ApplyDamage(hane, 1, f, pattern: AttackPattern.Single); }
            Expect($"回数: 段 {stage} なら1ターン {1 + stage} 回まで", Tal(ctx, hane).SpringCount, (long)(1 + stage));
        }
        {
            var hane = Setup(out var ctx, out var e, 0);
            UnitState foe = e.First(u => u.Slot == 0);
            ctx.ApplyDamage(hane, 5, null, burnTick: true);
            ctx.ApplyDamage(hane, 5, U(ctx.LivingMembers(BattleContext.PlayerTeam).ToList(), "yomi"), isFriendlyFire: true);
            ctx.ApplyDamage(hane, 5, foe, isFriendlyFire: true, relayed: true);
            Expect("弾き返し: 刻み・味方の巻き込み・中継では弾かない", Tal(ctx, hane).SpringCount, 0L);
        }
        foreach (var (slot, name) in new[] { (7, "○前2"), (8, "○後2"), (3, "後1"), (4, "後3") })
        {
            var hane = Setup(out var ctx, out var e, slot);
            UnitState foe = e.First(u => u.Slot == slot);
            ctx.ApplyDamage(hane, 5, foe, pattern: AttackPattern.Single);
            var t = Tal(ctx, hane);
            Expect($"弾けない席（{name}）: 弾かない・回数を使わない", $"{t.SpringCount}/{t.SpringNoSeat}/{foe.Slot}", $"0/1/{slot}");
        }
        {
            // 粛: 敵に粛の保持者（伝令）を置く
            var hushDef = EnemyCatalog.Stages[1].Enemy.Occupied().Select(o => o.Def).First(d => d.Traits.Contains(TraitId.Hush));
            var foes = new (int, UnitDef)[] { (0, strong), (2, hushDef), (1, tough) };
            var ctx = Ctx9(Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.BasaG0, center: UnitCatalog.Yomi, back1: h2, back3: UnitCatalog.Shio), foes, 6, out var p, out var e);
            UnitState hane = U(p, "hane");
            ctx.ApplyDamage(hane, 5, e.First(u => u.Slot == 0), pattern: AttackPattern.Single);
            Expect("粛: 伝令が生きている間は弾かない（粛で止まった 1）", $"{Tal(ctx, hane).SpringCount}/{Tal(ctx, hane).SpringHushed}", "0/1");
        }
        Console.WriteLine();

        // ---- (3) ----
        Console.WriteLine("## (3) ハネのいない戦は H0 と一致 ／ verbose の有無");
        Console.WriteLine();
        {
            var benches = new[] { MHane227, Formation.Build(front1: UnitCatalog.BasaG0, front3: UnitCatalog.Sero, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.HaneH0), HaneRows()[0].F };
            var jobs = new List<(Formation F, int W, EnemyScaleRule Sc, int S)>();
            foreach (var b in benches)
                foreach (int w in new[] { 4, 5, 0, 1, 2, 3 })
                    foreach (var (_, sc) in Scales)
                        for (int s = 0; s < 20; s++) jobs.Add((b, w, sc, s));
            var vdiff = new int[jobs.Count];
            Parallel.For(0, jobs.Count, i =>
            {
                var (f, w, sc, s) = jobs[i];
                foreach (var v in Versions.Skip(1))
                {
                    var fv = Apply(f, v);
                    var a = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(fv), BattleContext.PlayerTeam), WaveOf(w, sc)(), s, verbose: true, shuffler: PreHole);
                    var c = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(fv), BattleContext.PlayerTeam), WaveOf(w, sc)(), s, verbose: false, shuffler: PreHole);
                    long da = a.TallyByUnit.Values.Sum(t => t.DamageToEnemy), dc = c.TallyByUnit.Values.Sum(t => t.DamageToEnemy);
                    if (a.PlayerWon != c.PlayerWon || a.Turns != c.Turns || da != dc) Interlocked.Increment(ref vdiff[i]);
                }
            });
            Expect($"verbose の有無の差（H1〜H3w × {jobs.Count} 戦）", vdiff.Sum(), 0);
            // ハネのいない台（雷）は版に依らない（Apply がハネを見つけない）
            int same = 0, n = 0;
            foreach (int w in new[] { 4, 0, 1 })
                for (int s = 0; s < 20; s++)
                {
                    var a = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(Apply(Thunder, VerOf("H0"))), BattleContext.PlayerTeam), WaveOf(w, Scales[0].Sc)(), s, verbose: true, shuffler: PreHole);
                    var b = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(Apply(Thunder, VerOf("H3w"))), BattleContext.PlayerTeam), WaveOf(w, Scales[0].Sc)(), s, verbose: true, shuffler: PreHole);
                    n++; if (a.Events.Count == b.Events.Count && a.Turns == b.Turns && a.PlayerWon == b.PlayerWon) same++;
                }
            Expect("ハネのいない台（雷）は H0 と H3w で出来事の数・決着T・勝敗まで一致", same, n);
        }
        Console.WriteLine();

        // ---- (7) 台本 ----
        Console.WriteLine("## (7) 台本");
        Console.WriteLine();
        {
            long bl = 0, blOk = 0, blTally = 0, sp = 0, spOk = 0, spTally = 0;
            var f = Apply(Formation.Build(front1: UnitCatalog.BasaG0, front3: UnitCatalog.Sero, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.HaneH0), VerOf("H3"));
            foreach (int w in new[] { 4, 5, 1, 3 })
                for (int s = 0; s < 150; s++)
                {
                    var (r, p, e, slot0) = DecoyDiag.Fight(f, w, Scales[0].Sc, s);
                    int hane = U(p, "hane").InstanceId;
                    var enemyIds = e.Select(u => u.InstanceId).ToHashSet();
                    var evs = r.Events;
                    for (int i = 0; i < evs.Count; i++)
                    {
                        var x = evs[i];
                        if (x.Kind == BattleEventKind.Blast)
                        {
                            bl++;
                            // 次の Blast / TurnStart までに、ハネの貫きの Damage が経路の数（Amount）だけ、または A が倒れていれば Move 無し
                            var win = evs.Skip(i + 1).TakeWhile(y => y.Kind is not (BattleEventKind.Blast or BattleEventKind.TurnStart)).ToList();
                            int dmg = win.Count(y => y.Kind == BattleEventKind.Damage && y.ActorId == hane && y.Pattern == AttackPattern.Pierce && y.TargetId is int tt && enemyIds.Contains(tt));
                            bool aDied = win.Any(y => y.Kind == BattleEventKind.Death && y.TargetId == x.TargetId);
                            bool moved = win.Any(y => y.Kind == BattleEventKind.Move && y.ActorId == hane && y.TargetId == x.TargetId);
                            int othersDied = win.Count(y => y.Kind == BattleEventKind.Death && y.TargetId is int dd && enemyIds.Contains(dd) && dd != x.TargetId);
                            if (x.ActorId == hane && dmg >= 1 && dmg <= x.Amount && (aDied || x.Amount == 1 || moved || othersDied >= x.Amount - 1)) blOk++;
                            else Console.WriteLine($"  （Blast の例外: 波 {WaveNames[w]} seed {s} T{x.Turn} 経路の数 {x.Amount} 貫きの Damage {dmg} A 倒れ {aDied} A 動いた {moved}）");
                        }
                        if (x.Kind == BattleEventKind.Spring)
                        {
                            sp++;
                            var next = evs.Skip(i + 1).FirstOrDefault(y => y.Kind == BattleEventKind.Move);
                            var stag = evs.Skip(i + 1).FirstOrDefault(y => y.Kind == BattleEventKind.Stagger);
                            if (x.ActorId == hane && next is not null && next.TargetId == x.TargetId && next.Slot == x.Slot && next.ActorId == hane
                                && stag is not null && stag.TargetId == x.TargetId) spOk++;
                        }
                    }
                    if (r.TallyByUnit.TryGetValue("hane", out var t)) { blTally += t.BlastCount; spTally += t.SpringCount + t.SpringRefused; }
                }
            Expect("Blast の後にハネの貫きの Damage（1〜経路の数）と A の Move（A が倒れた・後ろが全員倒れた・1体だけのときを除く）", blOk, bl);
            Expect("Blast の数 ＝ 帳簿", bl, blTally);
            Expect("Spring の後の最初の Move は弾いた敵・弾いた先の席・動かしたのはハネ、続いて転倒", spOk, sp);
            Expect("Spring の数 ＝ 帳簿", sp, spTally);
            Expect("どちらも台本に出る", bl > 0 && sp > 0, true);
            Console.WriteLine($"  （Blast {bl} ／ Spring {sp}）");
        }
        Console.WriteLine();
        Console.WriteLine($"**○ {ok} ／ × {ng}**");
    }
}
