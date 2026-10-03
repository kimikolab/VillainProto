using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;
using FK = FireKindleDiag;

// foesurge check —— 自己検査（受け入れ 2〜4）。受け入れ 1（W0 の台本）は `foesurge digest W0` を実装の前と cmp で見る。
static partial class FoeSurgeDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly MethodInfo SurgeM = typeof(BattleContext).GetMethod("BlazeSurge", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo FoeSurgeOfM = typeof(BattleContext).GetMethod("FoeSurgeOf", BindingFlags.Static | BindingFlags.NonPublic)!;
    static readonly MethodInfo SurgeOfM = typeof(BattleContext).GetMethod("SurgeOf", BindingFlags.Static | BindingFlags.NonPublic)!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static readonly FieldInfo RngF = typeof(BattleContext).GetField("_rng", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static int ok, ng;
    static void Expect(string what, object got, object want)
    {
        bool pass = Equals(got, want);
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }
    sealed class CountingRandom : Random
    {
        public int N;
        public CountingRandom(int seed) : base(seed) { }
        public override int Next(int maxValue) { N++; return base.Next(maxValue); }
        public override int Next(int minValue, int maxValue) { N++; return base.Next(minValue, maxValue); }
        public override double NextDouble() { N++; return base.NextDouble(); }
    }
    static UnitDef Plain(string id) => new() { Id = id, Name = id, MaxHp = 1000, Attack = 10, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single };
    static BattleContext Ctx(UnitDef borg, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Hiyo, center: borg, back1: UnitCatalog.Hota, back3: Plain("a1")), BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(Formation.Build(front1: Plain("e1"), front3: Plain("e2")), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static void SetLv(UnitState u, int lv) { u.SetCounter(StatusKeys.Burn, lv > 0 ? 3 : 0); u.SetCounter(FireLevelRule.LvKey, lv); }

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第257期 foesurge check");
        Console.WriteLine();

        // ---- 受け入れ 2（直に）: 敵と味方に渡す量の振り分け ／ 敵上げの見出し ／ 乱数を引かない ----
        int Foe(UnitDef d) => (int)FoeSurgeOfM.Invoke(null, new object[] { BattleEngine.Materialize(Formation.Build(center: d), BattleContext.PlayerTeam)[0] })!;
        int Ally(UnitDef d) => (int)SurgeOfM.Invoke(null, new object[] { BattleEngine.Materialize(Formation.Build(center: d), BattleContext.PlayerTeam)[0] })!;
        Expect("渡す量（敵 ／ 味方）: W0", $"{Foe(UnitCatalog.Borg)}/{Ally(UnitCatalog.Borg)}", "0/0");
        Expect("渡す量（敵 ／ 味方）: W2 敵上げ2", $"{Foe(BorgW2)}/{Ally(BorgW2)}", "2/0");
        Expect("渡す量（敵 ／ 味方）: W4 敵上げ満", $"{Foe(BorgW4)}/{Ally(BorgW4)}", "4/0");
        Expect("渡す量（敵 ／ 味方）: ref（第254期の上げ2）", $"{Foe(BorgRef)}/{Ally(BorgRef)}", "2/2");
        foreach (var (borgDef, name, surge) in new[] { (BorgW2, "敵上げ2", 2), (BorgW4, "敵上げ満", 4) })
        {
            string Go(string id, int lv)
            {
                var ctx = Ctx(borgDef, out var p, out var e);
                var u = p.Concat(e).First(x => x.Def.Id == id); var bo = p.First(x => x.Def.Id == "borg");
                SetLv(bo, 4); SetLv(u, lv);
                var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
                int n0 = ctx.Events.Count;
                SurgeM.Invoke(ctx, new object[] { bo, u, surge });
                var evs = ctx.Events.Skip(n0).Where(x => x.Kind == BattleEventKind.FireLevel && x.TargetId == u.InstanceId).ToList();
                int foeLabel = evs.Count(x => x.Text == FireLevelLabels.BlazeFoeSurge), oldLabel = evs.Count(x => x.Text == FireLevelLabels.BlazeSurge);
                return $"{FireLevelRule.Of(u)}/敵上げ{foeLabel}/上げ{oldLabel}/乱数{cr.N}";
            }
            if (surge == 2)
            {
                Expect($"{name}: 敵 1 → 3（爆炎で点いたばかりの敵）", Go("e1", 1), "3/敵上げ1/上げ0/乱数0");
                Expect($"{name}: 敵 3 → 4", Go("e1", 3), "4/敵上げ1/上げ0/乱数0");
            }
            else
            {
                Expect($"{name}: 敵 1 → 4", Go("e1", 1), "4/敵上げ1/上げ0/乱数0");
                Expect($"{name}: 敵 3 → 4", Go("e1", 3), "4/敵上げ1/上げ0/乱数0");
            }
            Expect($"{name}: 燃えていない敵は上げない", Go("e1", 0), "0/敵上げ0/上げ0/乱数0");
        }

        // ---- 台で回す: 敵だけ ／ 量 ／ 味方の火勢は変わらない ／ 独りでも ／ 爆炎が出ない戦は W0 と一致 ／ 死因の合計 ／ verbose ----
        long battles = 0, noBlaze = 0, sameBad = 0, badTarget = 0, badAmt = 0, soloSurge = 0, foeOutside = 0, oldLabel = 0, allyMoved = 0, ledAlly = 0, ledFoeMismatch = 0,
            verbDiff = 0, causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, surgeEv = 0, gotFour = 0;
        var lk = new object();
        var w0 = VerOf("W0");
        foreach (var vn in new[] { "W2", "W4" })
        {
            var vv = VerOf(vn); int sg = vn == "W2" ? 2 : 4;
            foreach (string bn in Boards)
                foreach (int w in new[] { 0, 1, 2, 3, BA.MainWave, BA.MainWave + 1, FC.WaveHeavy })
                    for (int s = 0; s < BA.Scales.Length; s++)
                    {
                        var f0 = BoardOf(bn, w0); var f1 = BoardOf(bn, vv); var sc = BA.Scales[s].Sc;
                        Parallel.For(0, 30, seed =>
                        {
                            var pl = BattleEngine.Materialize(f1, BattleContext.PlayerTeam); var en = FC.WaveOf(w, sc)();
                            var slotOf = pl.Concat(en).ToDictionary(u => u, u => u.Slot);
                            var r = BattleEngine.Run(pl, en, seed, verbose: true);
                            var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
                            var rq = BattleEngine.Run(BattleEngine.Materialize(f1, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: false);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(f0, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: true);
                            var agg = new BA.Agg(); agg.Take(r, pl, en, slot0);
                            int borg = pl.First(u => u.Def.Id == "borg").InstanceId;
                            var mine = new HashSet<int>(pl.Select(u => u.InstanceId));
                            var ev = r.Events;
                            long bt = 0, ba = 0, so = 0, fo = 0, ol = 0, se = 0, g4 = 0;
                            bool anyBlaze = ev.Any(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Blaze);
                            int blazeAt = -1; bool solo = false; var hitFoes = new HashSet<int>();
                            for (int i = 0; i < ev.Count; i++)
                            {
                                var x = ev[i];
                                if (x.Kind == BattleEventKind.TurnStart) { blazeAt = -1; solo = false; }
                                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.BlazeSolo) solo = true;
                                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Blaze) { blazeAt = i; hitFoes.Clear(); }
                                if (blazeAt >= 0 && x.Kind == BattleEventKind.Damage && x.ActorId == borg && x.TargetId is int t && !mine.Contains(t)) hitFoes.Add(t);
                                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.BlazeSurge) ol++;
                                if (x.Kind != BattleEventKind.FireLevel || x.Text != FireLevelLabels.BlazeFoeSurge) continue;
                                se++;
                                if (blazeAt < 0 || x.ActorId != borg || x.TargetId is not int st || mine.Contains(st)) bt++;
                                else if (!hitFoes.Contains(st)) fo++;
                                int want = sg == 4 ? 4 : Math.Min(4, x.Slot + 2);
                                if (x.Amount != want || x.Slot < 1) ba++;
                                if (x.Amount == 4) g4++;
                                if (solo) so++;
                            }
                            var led = r.FireLevels;
                            long am = led is null ? 0 : led.BlazeAllyLog.Count(a => a.Pre != a.Post);
                            long la = led is null ? 0 : led.SurgeAlly;
                            long lf = led is null ? 0 : Math.Abs(led.SurgeFoe - se);
                            bool same = r.Turns == r0.Turns && r.PlayerWon == r0.PlayerWon && r.Log.Select(l => l.Text).SequenceEqual(r0.Log.Select(l => l.Text));
                            lock (lk)
                            {
                                battles++;
                                if (!anyBlaze) { noBlaze++; if (!same) sameBad++; }
                                badTarget += bt; badAmt += ba; soloSurge += so; foeOutside += fo; oldLabel += ol; surgeEv += se; gotFour += g4;
                                allyMoved += am; ledAlly += la; ledFoeMismatch += lf;
                                if (r.PlayerWon != rq.PlayerWon || r.Turns != rq.Turns || r.PlayerSurvivors != rq.PlayerSurvivors) verbDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                            }
                        });
                    }
        }
        Expect($"台で回す（{battles} 戦・敵上げの見出し {surgeEv} 件・うち 4 に {gotFour}）: 爆炎が1度も出ない戦は W0 と台本（ログ全文）が一致（{noBlaze} 戦）", sameBad, 0L);
        Expect("敵上げの見出しは爆炎の手番の中で・ボルグが書き手・相手は敵", badTarget, 0L);
        Expect("上げた敵は爆炎の一撃が当たった敵", foeOutside, 0L);
        Expect("敵上げ2 は 前 +2（上限 4）・敵上げ満は 4（前は 1 以上）", badAmt, 0L);
        Expect("第254期の「爆炎・上げ」（敵と味方）は1件も出ない", oldLabel, 0L);
        Expect("味方の火勢は変わらない（爆炎の味方の側の帳簿で 前 ＝ 後）", allyMoved, 0L);
        Expect("味方の上げの帳簿（`SurgeAlly`）は 0", ledAlly, 0L);
        Expect("敵の上げの帳簿（`SurgeFoe`）＝ 敵上げの見出しの数", ledFoeMismatch, 0L);
        Expect("爆炎・独りでも上げる（独りの爆炎の手番の敵上げの見出し > 0）", soloSurge > 0, true);
        Expect("verbose の有無で勝敗・決着T・生存が同じ", verbDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 3）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);

        // ---- 版の駒・札 ----
        Expect("W0 の駒 ＝ 規定（Borg / Hota / Hiyo・参照）", ReferenceEquals(w0.Borg, UnitCatalog.Borg) && ReferenceEquals(w0.Hota, UnitCatalog.Hota) && ReferenceEquals(w0.Hiyo, UnitCatalog.Hiyo), true);
        Expect("W2 ／ W4 ／ ref ＝ 規定のボルグ ＋ 札1枚", BorgW2.Traits.SequenceEqual(UnitCatalog.Borg.Traits.Append(TraitId.BlazeFoeSurge2))
            && BorgW4.Traits.SequenceEqual(UnitCatalog.Borg.Traits.Append(TraitId.BlazeFoeSurgeMax)) && BorgRef.Traits.SequenceEqual(UnitCatalog.Borg.Traits.Append(TraitId.BlazeSurge2)), true);
        Expect("敵上げの札の保持者は `UnitCatalog.All` に 0 枚", UnitCatalog.All.Count(u => u.Traits.Contains(TraitId.BlazeFoeSurge2) || u.Traits.Contains(TraitId.BlazeFoeSurgeMax)), 0);
        Expect("敵上げの札は `TraitCatalog` に登録されている", $"{TraitCatalog.Get(TraitId.BlazeFoeSurge2).Id}/{TraitCatalog.Get(TraitId.BlazeFoeSurgeMax).Id}", "BlazeFoeSurge2/BlazeFoeSurgeMax");
        Expect("敵上げの札は火勢の札（`FireLevelRule.Holds`）", FireLevelRule.Holds(BattleEngine.Materialize(Formation.Build(center: Plus(Plain("x"), TraitId.BlazeFoeSurge2)), BattleContext.PlayerTeam)[0]), true);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
        Console.WriteLine($"FOESURGE_CHECK ok={ok} ng={ng}");
    }
}
