using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// firefinish check —— 自己検査（受け入れ 2〜4）。K0 の台本の突き合わせは `firefinish digest` を実装の前のコミットと cmp で見る。
static partial class FireFinishDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
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
    static List<BattleEvent> Since(BattleContext ctx, int n0) => ctx.Events.Skip(n0).ToList();
    static int Lbl(List<BattleEvent> ev, BattleEventKind k, string label) => ev.Count(x => x.Kind == k && x.Text == label);

    /// <summary>規定の駒を席だけ変えて使う（席の番号で駒を呼ぶ）。</summary>
    static UnitDef Re(UnitDef d, string id, int hp = -1) => new()
    {
        Id = id, Name = d.Name, MaxHp = hp > 0 ? hp : d.MaxHp, Attack = d.Attack, Speed = d.Speed, Traits = d.Traits.ToArray(), Pattern = d.Pattern,
        Advances = d.Advances, Actions = d.Actions, PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第249期 firefinish check");
        Console.WriteLine();

        // ---- ③ ホタの火の癒し: 刻みは回復・火の変換と反転より先・二重にしない ----
        foreach (var (hota, name) in new[] { (UnitCatalog.HotaK0, "K0"), (HotaK1, "K1") })
        {
            // ホタ（前1）の隣にベニ（中央）、ヒヨ（後3）——火の変換も反転も効く位置
            var ctx = Ctx(Formation.Build(front1: hota, center: UnitCatalog.Beni, back3: UnitCatalog.HiyoK0), Formation.Build(front1: Plain("e1")), out var p, out _);
            var h = U(p, "hota"); var hy = U(p, "hiyo");
            ctx.Ignite(h); h.Hp = h.MaxHp - 30; int h0 = h.Hp;
            int n0 = ctx.Events.Count;
            ctx.TickStatuses();
            var ev = Since(ctx, n0);
            int conv = Lbl(ev, BattleEventKind.FireArmor, FireArmorLabels.Convert);
            int inv = ev.Count(x => x.Kind == BattleEventKind.Status && x.SourceTrait == TraitId.Inverse && x.TargetId == h.InstanceId);
            Expect($"{name}: ホタの刻み → HP の増え ／ 火の変換 ／ 反転", $"{h.Hp - h0}/{conv}/{inv}", name == "K0" ? "0/0/0" : $"{BurnRules.Damage}/0/0");
        }
        // 燃える巻き込み（ボルグの隣のホタ）
        foreach (var (hota, name) in new[] { (UnitCatalog.HotaK0, "K0"), (HotaK1, "K1") })
        {
            var ctx = Ctx(Formation.Build(front1: hota, center: UnitCatalog.BorgK0), Formation.Build(front1: Plain("e1", hp: 5000)), out var p, out _);
            var h = U(p, "hota"); var b = U(p, "borg");
            ctx.Ignite(h); h.Hp = h.MaxHp - 50; int h0 = h.Hp;
            ctx.FireSplashHit(h, 12, b);
            Expect($"{name}: 燃える巻き込み 12 → ホタの HP の増え", h.Hp - h0, name == "K0" ? 0 : 12);
        }

        // ---- ① 贔屓・火勢: 上乗せは 3 × 相手の火勢・隣の燃えていない味方は −2 ----
        foreach (var (hiyo, name) in new[] { (UnitCatalog.HiyoK0, "K0"), (HiyoK2a, "K2a") })
        {
            for (int lv = 1; lv <= 4; lv++)
            {
                var ctx = Ctx(Formation.Build(front1: hiyo, center: Plain("cold"), back3: HotaK1), Formation.Build(front1: Plain("e1")), out var p, out _);
                var hy = U(p, "hiyo"); var h = U(p, "hota"); var cold = U(p, "cold");
                SetLv(h, lv);
                int a0 = h.AtkBonus, c0 = cold.AtkBonus;
                TraitCatalog.Get(TraitId.Favor).OnAction(ctx, hy, hy.Def.Actions[0]);
                Expect($"{name}: 火勢{lv} の味方への上乗せ ／ 隣の燃えていない味方", $"{h.AtkBonus - a0}/{cold.AtkBonus - c0}", $"{(name == "K0" ? 4 : 3 * lv)}/-2");
            }
        }

        // ---- ② 爆炎: 敵全体 ×3・味方全体（ボルグ以外）に燃焼ダメージ・全員に着火・巻き込みは別に起きない ----
        foreach (var (borg, name) in new[] { (UnitCatalog.BorgK0, "K2a（放つ）"), (BorgK3, "K3（爆炎）") })
        {
            // ボルグ（中央）・ヒヨ（前1・ギフト）・ホタ（前3）・ベニ（後1）・素の味方 a（後3）。ベニの隣は 前1・中央（X 字）
            var ctx = Ctx(Formation.Build(front1: HiyoK2a, front3: HotaK1, center: borg, back1: UnitCatalog.Beni, back3: Plain("a3", hp: 200)),
                          Formation.Build(front1: Plain("e1"), front3: Plain("e3"), center: Plain("ec"), back1: Plain("eb1"), back3: Plain("eb3")), out var p, out var e);
            var bo = U(p, "borg"); var hy = U(p, "hiyo"); var ho = U(p, "hota"); var a3 = U(p, "a3"); var be = U(p, "beni");
            SetLv(bo, 4); SetLv(hy, 3);
            foreach (var x in p) if (x != bo) x.Hp = x.MaxHp - 60;
            var hp0 = p.ToDictionary(x => x, x => x.Hp); var ehp0 = e.ToDictionary(x => x, x => x.Hp);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(hy);
            var ev = Since(ctx, n0);
            int blaze = Lbl(ev, BattleEventKind.FireLevel, FireLevelLabels.Blaze), unl = Lbl(ev, BattleEventKind.FireLevel, FireLevelLabels.Unleash);
            int foesHit = e.Count(x => x.Hp < ehp0[x]);
            int foesBurn = e.Count(x => x.IsAlive && x.RawCounter(StatusKeys.Burn) > 0);
            int allyRows = Lbl(ev, BattleEventKind.FireArmor, FireArmorLabels.BlazeAlly);
            int splash = Lbl(ev, BattleEventKind.FireArmor, FireArmorLabels.Splash);
            var bAtk = ev.Where(x => x.Kind == BattleEventKind.Attack && x.ActorId == bo.InstanceId).Select(x => x.Pattern).FirstOrDefault();
            if (name.StartsWith("K3"))
            {
                Expect("K3: 放つの見出し ／ 爆炎の見出し", $"{unl}/{blaze}", "1/1");
                Expect("K3: ボルグの攻撃の型 ／ 当たった敵（5体中）／ 燃えている敵", $"{bAtk}/{foesHit}/{foesBurn}", $"{AttackPattern.All}/5/5");
                Expect("K3: 味方への燃焼ダメージの見出し（ボルグ以外の4体）／ 燃える巻き込み", $"{allyRows}/{splash}", "4/0");
                Expect("K3: 味方全員が燃えている", p.Where(x => x != bo).All(x => x.RawCounter(StatusKeys.Burn) > 0), true);
                var fb = ctx.FireBook;
                Expect("K3: 行き先（火の癒し ／ 火の変換 ／ 反転 ／ 受けた ／ 焼かれない）の人数", $"{(fb.BlazeNom[0] > 0 ? 1 : 0)}/{fb.BlazeNom[1] / Math.Max(1, fb.BlazeNom.Sum() / 4)}/{fb.BlazeNom[2] / Math.Max(1, fb.BlazeNom.Sum() / 4)}/{fb.BlazeNom[3]}/{fb.BlazeNom[4]}", "1/1/2/0/0");
                Expect("K3: 味方は全員回復（HP が増えた 4体）", p.Count(x => x != bo && x.Hp > hp0[x]), 4);
                int amt = ev.First(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Blaze).Amount;
                Expect("K3: ホタの火の癒しは名目どおり（二重にしない）", ho.Hp - hp0[ho], amt);
            }
            else
            {
                Expect("K2a: 放つ（薙ぎ）・爆炎の見出しなし", $"{unl}/{blaze}/{bAtk}", $"1/0/{AttackPattern.Sweep}");
            }
        }
        // 爆炎で素の味方（ヒヨなし・ベニの外）は焼かれる・倒れる駒は燃焼ダメージで倒れる
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a1", hp: 15), center: BorgK3, back3: Plain("a3", hp: 500)),
                          Formation.Build(front1: Plain("e1")), out var p, out var e);
            var bo = U(p, "borg"); var a1 = U(p, "a1"); var a3 = U(p, "a3");
            SetLv(bo, 4);
            // ギフトの手番を擬す: ヒヨが居ないので `_giftTurnActor` を立てて手番を回す
            var gf = typeof(BattleContext).GetField("_giftTurnActor", BindingFlags.Instance | BindingFlags.NonPublic)!;
            gf.SetValue(ctx, bo);
            int a3h = a3.Hp;
            ctx.TakeTurn(bo);
            gf.SetValue(ctx, null);
            int amt = ctx.Events.First(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Blaze).Amount;
            Expect("ヒヨなし: 素の味方は燃焼ダメージを受ける（a3 の減り ＝ 名目）／ 15 の a1 は倒れる", $"{a3h - a3.Hp}/{a1.IsAlive}", $"{amt}/False");
            Expect("ヒヨなし: 帳簿（受けた の名目 ／ 味方が倒れた）", $"{ctx.FireBook.BlazeNom[3]}/{ctx.FireBook.BlazeAllyKills}", $"{2 * amt}/1");
        }

        // ---- 残り火・連撃: 5発・敵1体なら全部その敵・席の番号順に巡回・乱数を引かない ----
        foreach (int nFoes in new[] { 1, 3, 5 })
        {
            var en = nFoes switch
            {
                1 => Formation.Build(center: Plain("e2")),
                3 => Formation.Build(front1: Plain("e0"), front3: Plain("e1"), back1: Plain("e3")),
                _ => Formation.Build(front1: Plain("e0"), front3: Plain("e1"), center: Plain("e2"), back1: Plain("e3"), back3: Plain("e4")),
            };
            var ctx = Ctx(Formation.Build(front1: HotaK4), en, out var p, out var e);
            var ho = U(p, "hota"); SetLv(ho, 2); ho.SetCounter(FireBurstRule.EmbersKey, 1);
            var cr = new CountingRandom(0); RngF.SetValue(ctx, cr);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(ho);
            var ev = Since(ctx, n0);
            var seq = ev.Where(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.EmbersHit).Select(x => e.First(u => u.InstanceId == x.TargetId).Def.Id).ToList();
            string want = nFoes switch { 1 => "e2,e2,e2,e2,e2", 3 => "e0,e1,e3,e0,e1", _ => "e0,e1,e2,e3,e4" };
            Expect($"連撃（敵 {nFoes} 体）: 当てた順", string.Join(",", seq), want);
            int atks = ev.Count(x => x.Kind == BattleEventKind.Attack && x.ActorId == ho.InstanceId);
            Expect($"連撃（敵 {nFoes} 体）: ホタの攻撃 ／ 全員燃えている ／ 乱数", $"{atks}/{e.All(x => x.RawCounter(StatusKeys.Burn) > 0)}/{cr.N}", "5/True/0");
        }
        {
            var ctx = Ctx(Formation.Build(front1: HotaK1), Formation.Build(front1: Plain("e0"), center: Plain("e2")), out var p, out var e);
            var ho = U(p, "hota"); SetLv(ho, 2); ho.SetCounter(FireBurstRule.EmbersKey, 1);
            int n0 = ctx.Events.Count;
            ctx.TakeTurn(ho);
            var ev = Since(ctx, n0);
            Expect("連撃の札なし（K3）: 残り火は全体 ×2（敵2体以下で追加 1回）＝ 攻撃 2回・連撃の見出し 0",
                $"{ev.Count(x => x.Kind == BattleEventKind.Attack && x.ActorId == ho.InstanceId && x.Pattern == AttackPattern.All)}/{Lbl(ev, BattleEventKind.FireLevel, FireLevelLabels.EmbersHit)}", "2/0");
        }

        // ---- 刻み・一撃: 6 × 火勢 を1回 ----
        foreach (var (borg, name) in new[] { (BorgK3, "K4"), (BorgK4t, "K4t") })
        {
            var ctx = Ctx(Formation.Build(back3: borg), Formation.Build(front1: Plain("e1", hp: 5000)), out var p, out var e);
            var e1 = U(e, "e1"); SetLv(e1, 4); int h0 = e1.Hp;
            int n0 = ctx.Events.Count;
            ctx.TickStatuses();
            var ev = Since(ctx, n0);
            var dmg = ev.Where(x => x.Kind == BattleEventKind.Damage && x.TargetId == e1.InstanceId).Select(x => x.Amount).ToList();
            int per = BurnRules.Damage + (BurnRules.Damage * FoeFireRule.BrittlePercent[4] + 99) / 100;
            int one = 4 * BurnRules.Damage + (4 * BurnRules.Damage * FoeFireRule.BrittlePercent[4] + 99) / 100;
            Expect($"{name}: 火勢4 の敵の刻み（回数 ／ 1回の量）", $"{dmg.Count}/{string.Join(",", dmg.Distinct())}", name == "K4" ? $"4/{per}" : $"1/{one}");
        }
        foreach (var (borg, name) in new[] { (BorgK3, "K4"), (BorgK4t, "K4t") })
        {
            // 味方の刻みも回数（`AllyFireTick`）——ヒヨが居なければ受ける（燃えている味方の被弾の半減はボルグの盾の配り）
            var ctx = Ctx(Formation.Build(back3: borg, front1: Plain("a1")), Formation.Build(front1: Plain("e1", hp: 5000)), out var p, out _);
            var a1 = U(p, "a1"); SetLv(a1, 3);
            int n0 = ctx.Events.Count;
            ctx.TickStatuses();
            var dmg = Since(ctx, n0).Where(x => x.Kind == BattleEventKind.Damage && x.TargetId == a1.InstanceId).Select(x => x.Amount).ToList();
            Expect($"{name}: 火勢3 の味方（ヒヨなし）の刻み（回数 ／ 量の合計）", $"{dmg.Count}/{dmg.Sum()}", name == "K4" ? $"3/{dmg.Sum()}" : $"1/{dmg.Sum()}");
        }

        // ---- 台で回す: verbose の有無・決定性・死因の合計 ＝ 落ちた駒・爆炎と連撃の見出しの並び ----
        long battles = 0, verbDiff = 0, detDiff = 0, causeSum = 0, deathEv = 0, fellSum = 0, fellRes = 0, blazeBad = 0, blazeN = 0, chainBad = 0, chainN = 0, mendBad = 0;
        var lk = new object();
        foreach (var v in Versions)
            foreach (var bf in new Func<Formation>[] { () => FC.T3244, () => FC.T3238, () => FC.ThunderBorg })
                foreach (int w in new[] { 0, 2, 3, BA.MainWave, BA.MainWave + 1, FC.WaveHeavy })
                    for (int s = 0; s < BA.Scales.Length; s++)
                    {
                        var f = Apply(bf(), v); var sc = BA.Scales[s].Sc;
                        Parallel.For(0, 40, seed =>
                        {
                            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam); var en = FC.WaveOf(w, sc)();
                            var slot0 = pl.Concat(en).ToDictionary(u => u, u => u.Slot);
                            var r = BattleEngine.Run(pl, en, seed, verbose: true);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: false);
                            var r3 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, sc)(), seed, verbose: true);
                            var agg = new BA.Agg(); agg.Take(r, pl, en, slot0.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
                            // 爆炎: 見出しの後のボルグの攻撃は全体・同じ手番の燃える巻き込みは 0
                            int bb = 0, bn = 0, cb = 0, cn = 0, mb = 0;
                            var ev = r.Events;
                            int? borg = pl.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId, hota = pl.FirstOrDefault(u => u.Def.Id == "hota")?.InstanceId;
                            for (int i = 0; i < ev.Count; i++)
                            {
                                var x = ev[i];
                                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Blaze && x.ActorId == borg)
                                {
                                    bn++;
                                    var hd = r.Hands.FirstOrDefault(h => h.EventStart <= i && i < h.EventEnd);
                                    int end = hd.EventEnd == 0 ? ev.Count : hd.EventEnd;
                                    var atk = ev.Skip(i).Take(end - i).FirstOrDefault(y => y.Kind == BattleEventKind.Attack && y.ActorId == borg);
                                    int sp = ev.Skip(i).Take(end - i).Count(y => y.Kind == BattleEventKind.FireArmor && y.Text == FireArmorLabels.Splash && y.ActorId == borg);
                                    if (atk is null || atk.Pattern != AttackPattern.All || sp != 0) bb++;
                                }
                                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Embers && x.ActorId == hota && x.Slot == 1 && v.Hota.Traits.Contains(TraitId.EmbersChain))
                                {
                                    cn++;
                                    var hd = r.Hands.FirstOrDefault(h => h.EventStart <= i && i < h.EventEnd);
                                    int end = hd.EventEnd == 0 ? ev.Count : hd.EventEnd;
                                    int hits = ev.Skip(i).Take(end - i).Count(y => y.Kind == BattleEventKind.FireLevel && y.Text == FireLevelLabels.EmbersHit);
                                    // 5発を超えない・1発は撃つ（途中で敵が尽きる・ホタが倒れると5発に満たない）
                                    if (hits > FireFinishRule.EmbersHits || hits == 0) cb++;
                                }
                            }
                            // ホタの火の癒しは「焼かれない」の枝だけ: 火の変換の見出しがホタに出たら二重
                            if (hota is int hi && v.Hota.Traits.Contains(TraitId.PyreMend))
                                mb += ev.Count(y => y.Kind == BattleEventKind.FireArmor && y.Text == FireArmorLabels.Convert && y.TargetId == hi);
                            lock (lk)
                            {
                                battles++;
                                if (r.PlayerWon != r2.PlayerWon || r.Turns != r2.Turns || r.PlayerSurvivors != r2.PlayerSurvivors) verbDiff++;
                                if (r.Events.Count != r3.Events.Count || r.Log.Count != r3.Log.Count || r.Turns != r3.Turns) detDiff++;
                                causeSum += agg.Cause.Sum(); deathEv += agg.DeathEvents; fellSum += agg.FellSum; fellRes += r.PlayerStarterFallen.Count;
                                blazeBad += bb; blazeN += bn; chainBad += cb; chainN += cn; mendBad += mb;
                            }
                        });
                    }
        Expect($"台で回す（{battles} 戦）: 爆炎の手番はボルグの攻撃が全体・燃える巻き込み 0（{blazeN} 回）", blazeBad, 0L);
        Expect($"台で回す: 残り火の連撃は1〜5発（{chainN} 回）", chainBad, 0L);
        Expect("台で回す: ホタに火の変換が出ない（火の癒しが先・二重にしない）", mendBad, 0L);
        Expect("verbose の有無で勝敗・決着T・生存が同じ", verbDiff, 0L);
        Expect("同じ seed を2度回して台本の長さが同じ", detDiff, 0L);
        Expect("死因の合計 ＝ 倒れた出来事の数（受け入れ 4）", causeSum, deathEv);
        Expect("落ちた駒（死因の帳簿）＝ 落ちた駒（結果）", fellSum, fellRes);

        // ---- K0 ＝ 規定（札を1枚も足していない）----
        var k0 = VerOf("K0");
        Expect("K0 の駒 ＝ 規定の駒（参照）", ReferenceEquals(k0.Borg, UnitCatalog.BorgK0) && ReferenceEquals(k0.Hota, UnitCatalog.HotaK0) && ReferenceEquals(k0.Hiyo, UnitCatalog.HiyoK0), true);
        // 第250期 前段: K4 が規定になった——K4 の4枚の保持者は規定のボルグ・ホタ・ヒヨの3枚、刻み・一撃は 0 枚のまま。
        Expect("K4 の札の保持者（`UnitCatalog.All`・第250期 前段から規定の3枚）", UnitCatalog.All.Count(d => d.Traits.Any(t => t is TraitId.PyreMend or TraitId.FavorLevel or TraitId.UnleashBlaze or TraitId.EmbersChain)), 3);
        Expect("刻み・一撃の保持者（`UnitCatalog.All`）", UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.TickOnce)), 0);
        var k4 = VerOf("K4");
        // 第251期: L3 が規定になったので、K4 の突き合わせ先を第250期までの規定（`BorgK4` / `HotaK4`）に移した（ヒヨは第250期から変わっていない）。
        Expect("K4 の札 ＝ 第250期までの規定の札（BorgK4 / HotaK4 / Hiyo）", string.Join(",", k4.Borg.Traits) == string.Join(",", UnitCatalog.BorgK4.Traits) && string.Join(",", k4.Hota.Traits) == string.Join(",", UnitCatalog.HotaK4.Traits)
            && string.Join(",", k4.Hiyo.Traits) == string.Join(",", UnitCatalog.Hiyo.Traits) && k4.Hota.Attack == UnitCatalog.HotaK4.Attack, true);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
        Console.WriteLine($"FIREFINISH_CHECK ok={ok} ng={ng}");
    }
}
