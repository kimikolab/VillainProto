using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;
using FK = FireKindleDiag;

// blazesurge check —— 自己検査（受け入れ 1〜4）。V0 の台本の突き合わせは `blazesurge digest V0` を実装の前の `giftorder digest N2` と cmp で見る。
static partial class BlazeSurgeDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly MethodInfo SurgeM = typeof(BattleContext).GetMethod("BlazeSurge", BindingFlags.Instance | BindingFlags.NonPublic)!;
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
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static void SetLv(UnitState u, int lv) { u.SetCounter(StatusKeys.Burn, lv > 0 ? 3 : 0); u.SetCounter(FireLevelRule.LvKey, lv); }

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第254期 blazesurge check");
        Console.WriteLine();

        // ---- 受け入れ 2: 上げ2 は +2（+1 の育ち2回）・上げ満は 4 に ／ 上限を超えた分はあぶれた火（ホタ・ヒヨ）／ ボルグ・燃えていない駒は対象外 ／ 乱数を引かない ----
        foreach (var (borgDef, name, surge) in new[] { (BorgV2, "上げ2", 2), (BorgV4, "上げ満", 4) })
        {
            string Go(string id, int lv)
            {
                var ctx = Ctx(borgDef, out var p, out var e);
                var all = p.Concat(e).ToList();
                var u = U(all, id); var bo = U(p, "borg");
                SetLv(bo, 4); SetLv(u, lv);
                int atk0 = u.AtkBonus, hoard0 = u.RawCounter(FireKindleRule.GiftHoardKey);
                var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
                int n0 = ctx.Events.Count;
                SurgeM.Invoke(ctx, new object[] { bo, u, surge });
                int ev = ctx.Events.Skip(n0).Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.BlazeSurge && x.TargetId == u.InstanceId);
                return $"{FireLevelRule.Of(u)}/+{u.AtkBonus - atk0}/溜め+{u.RawCounter(FireKindleRule.GiftHoardKey) - hoard0}/見出し{ev}/乱数{cr.N}";
            }
            if (surge == 2)
            {
                Expect($"{name}: ホタ 1 → 3（あぶれた火なし）", Go("hota", 1), "3/+0/溜め+0/見出し1/乱数0");
                Expect($"{name}: ホタ 3 → 4 ＋ あぶれた火1回（攻撃力 +4）", Go("hota", 3), "4/+4/溜め+0/見出し1/乱数0");
                Expect($"{name}: ホタ 4 → 4 ＋ あぶれた火2回（攻撃力 +8）", Go("hota", 4), "4/+8/溜め+0/見出し1/乱数0");
                Expect($"{name}: ヒヨ 3 → 4 ＋ 渡す火の溜め +1", Go("hiyo", 3), "4/+0/溜め+1/見出し1/乱数0");
                Expect($"{name}: ヒヨ 4 → 4 ＋ 渡す火の溜め +2", Go("hiyo", 4), "4/+0/溜め+2/見出し1/乱数0");
                Expect($"{name}: 敵 1 → 3", Go("e1", 1), "3/+0/溜め+0/見出し1/乱数0");
                Expect($"{name}: 味方（札なし）2 → 4", Go("a1", 2), "4/+0/溜め+0/見出し1/乱数0");
            }
            else
            {
                Expect($"{name}: ホタ 1 → 4（あぶれた火なし）", Go("hota", 1), "4/+0/溜め+0/見出し1/乱数0");
                Expect($"{name}: ホタ 4 → 4 ＋ あぶれた火1回（攻撃力 +4）", Go("hota", 4), "4/+4/溜め+0/見出し1/乱数0");
                Expect($"{name}: ヒヨ 4 → 4 ＋ 渡す火の溜め +1", Go("hiyo", 4), "4/+0/溜め+1/見出し1/乱数0");
                Expect($"{name}: 敵 2 → 4", Go("e1", 2), "4/+0/溜め+0/見出し1/乱数0");
            }
            Expect($"{name}: 燃えていない駒は上げない", Go("a1", 0), "0/+0/溜め+0/見出し0/乱数0");
            Expect($"{name}: ボルグ自身は上げない", Go("borg", 1), "1/+0/溜め+0/見出し0/乱数0");
        }
        // 渡す火の溜めは上限 3 のまま（上限で捨てる）
        {
            var ctx = Ctx(BorgV2, out var p, out _);
            var hy = U(p, "hiyo"); var bo = U(p, "borg");
            SetLv(bo, 4); SetLv(hy, 4); hy.SetCounter(FireKindleRule.GiftHoardKey, 2);
            SurgeM.Invoke(ctx, new object[] { bo, hy, 2 });
            Expect("上げ2: ヒヨ 4・溜め 2 → 溜め 3（1回は上限で捨てる）", $"{hy.RawCounter(FireKindleRule.GiftHoardKey)}/{ctx.FireBook.GiftHoardCapped}", "3/1");
        }

        // ---- 台で回す: 対象 ／ 量 ／ 爆炎が1度も出ない戦は V0 と台本一致 ／ 独りでも上げる ／ 死因の合計 ／ verbose の有無 ----
        long battles = 0, noBlaze = 0, sameBad = 0, badTarget = 0, badAmt = 0, soloSurge = 0, foeOutside = 0, allyOutside = 0, verbDiff = 0, causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, surgeEv = 0;
        var lk = new object();
        var v0 = VerOf("V0");
        foreach (var vn in new[] { "V2", "V4" })
        {
            var vv = VerOf(vn); int sg = vn == "V2" ? 2 : 4;
            foreach (var bf in new Func<Formation>[] { () => FC.T3244, () => FC.T3238, () => FC.ThunderBorg })
                foreach (int w in new[] { 0, 1, 2, 3, BA.MainWave, BA.MainWave + 1, FC.WaveHeavy })
                    for (int s = 0; s < BA.Scales.Length; s++)
                    {
                        var f0 = FK.Apply(bf(), v0); var f1 = FK.Apply(bf(), vv); var sc = BA.Scales[s].Sc;
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
                            long bt = 0, ba = 0, so = 0, fo = 0, ao = 0, se = 0;
                            bool anyBlaze = ev.Any(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Blaze);
                            int blazeAt = -1; bool solo = false; var hitFoes = new HashSet<int>(); var allyHit = new HashSet<int>();
                            for (int i = 0; i < ev.Count; i++)
                            {
                                var x = ev[i];
                                if (x.Kind == BattleEventKind.TurnStart) { blazeAt = -1; solo = false; }
                                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.BlazeSolo) solo = true;
                                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Blaze) { blazeAt = i; hitFoes.Clear(); allyHit.Clear(); }
                                if (blazeAt >= 0 && x.Kind == BattleEventKind.Damage && x.ActorId == borg && x.TargetId is int t && !mine.Contains(t)) hitFoes.Add(t);
                                if (blazeAt >= 0 && x.Kind == BattleEventKind.FireArmor && x.Text == FireArmorLabels.BlazeAlly && x.TargetId is int at) allyHit.Add(at);
                                if (x.Kind != BattleEventKind.FireLevel || x.Text != FireLevelLabels.BlazeSurge) continue;
                                se++;
                                if (blazeAt < 0 || x.ActorId != borg || x.TargetId == borg) bt++;
                                if (x.TargetId is int st)
                                {
                                    if (mine.Contains(st)) { if (!allyHit.Contains(st)) ao++; }
                                    else if (!hitFoes.Contains(st)) fo++;
                                }
                                int want = sg == 4 ? 4 : Math.Min(4, x.Slot + 2);
                                if (x.Amount != want || x.Slot < 1) ba++;
                                if (solo) so++;
                            }
                            bool same = r.Turns == r0.Turns && r.PlayerWon == r0.PlayerWon && r.Log.Select(l => l.Text).SequenceEqual(r0.Log.Select(l => l.Text));
                            lock (lk)
                            {
                                battles++;
                                if (!anyBlaze) { noBlaze++; if (!same) sameBad++; }
                                badTarget += bt; badAmt += ba; soloSurge += so; foeOutside += fo; allyOutside += ao; surgeEv += se;
                                if (r.PlayerWon != rq.PlayerWon || r.Turns != rq.Turns || r.PlayerSurvivors != rq.PlayerSurvivors) verbDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                            }
                        });
                    }
        }
        Expect($"台で回す（{battles} 戦・上げの見出し {surgeEv} 件）: 爆炎が1度も出ない戦は V0 と台本（ログ全文）が一致（{noBlaze} 戦）", sameBad, 0L);
        Expect("上げの見出しは爆炎の手番の中で・ボルグが書き手・ボルグ以外", badTarget, 0L);
        Expect("上げた敵は爆炎の一撃が当たった敵", foeOutside, 0L);
        Expect("上げた味方は爆炎の味方の側（「爆炎・味方」）の相手", allyOutside, 0L);
        Expect("上げ2 は 前 +2（上限 4）・上げ満は 4（前は 1 以上）", badAmt, 0L);
        Expect("爆炎・独りでも上げる（独りの爆炎の手番の上げの見出し > 0）", soloSurge > 0, true);
        Expect("verbose の有無で勝敗・決着T・生存が同じ", verbDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 3）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);

        // ---- 版の駒・札 ----
        Expect("V0 の駒 ＝ 第253期の規定（Borg / Hota / Hiyo・参照）", ReferenceEquals(v0.Borg, UnitCatalog.Borg) && ReferenceEquals(v0.Hota, UnitCatalog.Hota) && ReferenceEquals(v0.Hiyo, UnitCatalog.Hiyo), true);
        Expect("V2 ／ V4 ＝ 規定のボルグ ＋ 上げの札1枚", BorgV2.Traits.SequenceEqual(UnitCatalog.Borg.Traits.Append(TraitId.BlazeSurge2)) && BorgV4.Traits.SequenceEqual(UnitCatalog.Borg.Traits.Append(TraitId.BlazeSurgeMax)), true);
        Expect("上げの札の保持者は `UnitCatalog.All` に 0 枚", UnitCatalog.All.Count(u => u.Traits.Contains(TraitId.BlazeSurge2) || u.Traits.Contains(TraitId.BlazeSurgeMax)), 0);
        Expect("上げの札は `TraitCatalog` に登録されている", $"{TraitCatalog.Get(TraitId.BlazeSurge2).Id}/{TraitCatalog.Get(TraitId.BlazeSurgeMax).Id}", "BlazeSurge2/BlazeSurgeMax");

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
        Console.WriteLine($"BLAZESURGE_CHECK ok={ok} ng={ng}");
    }
}
