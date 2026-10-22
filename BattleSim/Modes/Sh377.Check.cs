using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// sh377 check —— 第377期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh377Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static UnitTally Tal(BattleContext ctx, UnitState u) => (UnitTally)TallyM.Invoke(ctx, new object[] { u })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate377", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe377", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    /// <summary>直に組む盤面（味方は席 → 駒・敵は中央に的1体）。</summary>
    static (BattleContext Ctx, List<UnitState> P, UnitState E) Board(params (int Slot, UnitDef D)[] seats)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation();
        foreach (var (s, d) in seats) f[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(Formation.Build(center: Foe), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        return (ctx, p, e[0]);
    }
    static void Kill(BattleContext ctx, UnitState u, UnitState by) => ctx.ApplyDamage(u, 9999, by);
    static List<UnitState> Living(BattleContext ctx, string id) => ctx.LivingMembers(BattleContext.PlayerTeam).Where(u => u.Def.Id == id).ToList();
    static Trait TraitOf(TraitId id) => TraitCatalog.Resolve(new[] { id })[0];
    static void TurnStart(BattleContext ctx, UnitState u, int t) { TurnField.SetValue(ctx, t); foreach (var tr in u.Traits) tr.OnTurnStart(ctx, u); }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh377 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 定義 ----
        {
            var w = UnitCatalog.Wight;
            bool wight = w.MaxHp == 24 && w.Attack == 3 && w.Speed == 4 && w.Name == "亡者" && w.Traits.SequenceEqual(new[] { TraitId.Ephemeral, TraitId.Rot })
                && !UnitCatalog.All.Contains(w) && !UnitCatalog.Retired.Contains(w) && !UnitCatalog.Everyone.Any(d => d.Id == w.Id);
            var vers = new[] { UnitCatalog.RicaLink, UnitCatalog.RicaStack, UnitCatalog.MugSproutM };
            bool defs = UnitCatalog.RicaH378.Traits.SequenceEqual(new[] { TraitId.Necro, TraitId.Sacrifice }) && UnitCatalog.MugH379.Traits.SequenceEqual(new[] { TraitId.Splitter })
                && UnitCatalog.All.Contains(UnitCatalog.Rica) && UnitCatalog.All.Contains(UnitCatalog.Mug) && UnitCatalog.All.Count == 52
                && UnitCatalog.RicaLink.Traits.SequenceEqual(UnitCatalog.RicaH378.Traits.Append(TraitId.RicaRaiseDead)) && UnitCatalog.RicaStack.Traits.SequenceEqual(UnitCatalog.RicaH378.Traits.Append(TraitId.RicaRaiseDeadStack))
                && UnitCatalog.MugSproutM.Traits.SequenceEqual(UnitCatalog.MugH379.Traits.Append(TraitId.MugSprout))
                && vers.All(d => !UnitCatalog.All.Contains(d) && !UnitCatalog.Retired.Contains(d))
                && UnitCatalog.RicaLink.PlusText.EndsWith(UnitCatalog.RicaRaiseClause377 + UnitCatalog.RicaLinkTail377) && UnitCatalog.RicaStack.PlusText.EndsWith(UnitCatalog.RicaRaiseClause377 + UnitCatalog.RicaStackTail377)
                && UnitCatalog.MugSproutM.PlusText.EndsWith(UnitCatalog.MugSproutClause377);
            Expect("定義: 亡者（HP 24 ／ 攻 3 ／ 速 4・儚い ＋ 朽ち）は編成に選べない（`All` ／ `Retired` ／ `Everyone` の外）・版の駒 R-繋 ／ R-積（リィカ）／ M（ムグ）＝ 規定の駒 ＋ 札1枚（`All` の外）・規定のリィカ ／ ムグは今どおり（`All` 52 枚）", wight && defs);
        }

        // ---- 朽ち ----
        {
            var (ctx, p, foe) = Board((0, Mate), (4, Mate));
            var wgt = ctx.Summon(UnitCatalog.Wight, BattleContext.PlayerTeam)!;
            var rot = TraitOf(TraitId.Rot);
            var hps = new List<int>();
            for (int t = 2; t <= 4; t++) { TurnField.SetValue(ctx, t); rot.OnTurnStart(ctx, wgt); hps.Add(wgt.Hp); }
            var death = ctx.Events.FirstOrDefault(x => x.Kind == BattleEventKind.Death && x.TargetId == wgt.InstanceId);
            var tal = Tal(ctx, wgt);
            bool ok = hps.SequenceEqual(new[] { 14, 4, 0 }) && !wgt.IsAlive && death is not null && death.ActorId is null && tal.DamageTaken == 0 && tal.R377RotLost == 24 && tal.R377RotDeaths == 1 && wgt.Slot == 5;
            Expect("朽ち: 亡者の HP がターンの頭に 10 ずつ崩れ 24 → 14 → 4 → 0 で倒れる（撃破者なし）・ダメージとして数えない（受けた傷 0）・湧く枠は ○中1", ok, $"HP {string.Join(" → ", hps)}・受けた傷 {tal.DamageTaken}・席 {wgt.Slot}");
        }

        // ---- A ／ B: 3 回まで ／ 儚い駒からは湧かない ----
        foreach (var (name, def, id, key, spawn) in new[] { ("A 亡者起こし", UnitCatalog.RicaLink, "rica", RaiseDeadTrait.CountKey, UnitCatalog.Wight), ("B 亡骸の芽吹き", UnitCatalog.MugSproutM, "mug", MugSproutTrait.CountKey, UnitCatalog.Spore) })
        {
            var (ctx, p, foe) = Board((0, Mate), (1, Mate), (2, Mate), (3, Mate), (4, def));
            var holder = p.First(u => u.Def.Id == id);
            var eph = ctx.Summon(UnitCatalog.Fodder, BattleContext.PlayerTeam)!;   // 儚い駒（背いた召喚獣）
            int before = ctx.Events.Count(x => x.Kind == BattleEventKind.Summon);
            Kill(ctx, eph, foe);
            int afterEph = ctx.Events.Count(x => x.Kind == BattleEventKind.Summon) - before;
            foreach (var m in p.Where(u => u.Def.Id == Mate.Id).ToList()) Kill(ctx, m, foe);
            int spawned = ctx.Events.Count(x => x.Kind == BattleEventKind.Summon && x.Text == spawn.Name && x.ActorId == holder.InstanceId);
            bool ok = afterEph == 0 && spawned == 3 && holder.RawCounter(key) == 3;
            Expect($"{name}: 編成の駒が 4 体倒れても湧くのは 3 体（1戦に 3 回まで）・儚い駒（背いた召喚獣）の死からは湧かない", ok, $"儚い駒の死の後 {afterEph} 体・編成 4 体の死の後 {spawned} 体・回数 {holder.RawCounter(key)}");
        }
        {
            var carry = new UnitState { Def = UnitCatalog.RicaLink, TeamId = 0, Shape = FormationShape.X, Slot = 4, Hp = 55, MaxHp = 55, Traits = TraitCatalog.Resolve(UnitCatalog.RicaLink.Traits) };
            carry.SetCounter(RaiseDeadTrait.CountKey, 3); carry.SetCounter(RaiseDeadTrait.RealDeathKey, 5);
            foreach (var t in carry.Traits) t.OnCarryOver(carry);
            var mug = new UnitState { Def = UnitCatalog.MugSproutM, TeamId = 0, Shape = FormationShape.X, Slot = 2, Hp = 38, MaxHp = 38, Traits = TraitCatalog.Resolve(UnitCatalog.MugSproutM.Traits) };
            mug.SetCounter(MugSproutTrait.CountKey, 3);
            foreach (var t in mug.Traits) t.OnCarryOver(mug);
            Expect("A ／ B: 回数は会戦の境界で 0 に戻る（`OnCarryOver`）", carry.RawCounter(RaiseDeadTrait.CountKey) == 0 && carry.RawCounter(RaiseDeadTrait.RealDeathKey) == 0 && mug.RawCounter(MugSproutTrait.CountKey) == 0);
        }

        // ---- 空きが無ければ回数を使わない ／ 倒れた儚い駒の枠は空き ----
        foreach (var (name, def, id, key) in new[] { ("A", UnitCatalog.RicaLink, "rica", RaiseDeadTrait.CountKey), ("B", UnitCatalog.MugSproutM, "mug", MugSproutTrait.CountKey) })
        {
            var (ctx, p, foe) = Board((0, Mate), (1, Mate), (4, def));
            var holder = p.First(u => u.Def.Id == id);
            var filler = Enumerable.Range(0, 4).Select(_ => ctx.Summon(UnitCatalog.Fodder, BattleContext.PlayerTeam)!).ToList();
            int n0 = ctx.Events.Count(x => x.Kind == BattleEventKind.Summon);
            Kill(ctx, p[0], foe);
            int s1 = ctx.Events.Count(x => x.Kind == BattleEventKind.Summon) - n0, c1 = holder.RawCounter(key);
            long noSeat = id == "rica" ? Tal(ctx, holder).R377NoSeat : Tal(ctx, holder).R377SproutNoSeat;
            Kill(ctx, filler[1], foe);   // ○中3 の儚い駒が倒れる → 新しい札の経路では空き
            Kill(ctx, p[1], foe);
            var last = ctx.Events.Last(x => x.Kind == BattleEventKind.Summon);
            bool ok = s1 == 0 && c1 == 0 && noSeat == 1 && last.ActorId == holder.InstanceId && last.Slot == filler[1].Slot && holder.RawCounter(key) == 1;
            Expect($"{name}: 召喚枠が生きた駒で埋まっていれば湧かず回数も使わない・倒れた儚い駒の枠には湧く", ok, $"埋まっているとき {s1} 体・回数 {c1}・空きなし {noSeat} ／ 儚い駒が倒れた後 席 {last.Slot}（その駒の席 {filler[1].Slot}）・回数 {holder.RawCounter(key)}");
        }
        {
            // 既定の召喚（胞子 ／ 餌）の走査は今どおり「生死を問わず塞ぐ」。倒れた亡者の枠にも、ムグの分裂の胞子は湧かない。
            var (ctx, p, foe) = Board((0, Mate), (2, UnitCatalog.MugH379), (4, Mate));   // 第380期: 第379期までの規定のムグ
            var w0 = ctx.Summon(UnitCatalog.Wight, BattleContext.PlayerTeam)!;   // ○中1
            ctx.LoseHp(w0, 999, TraitId.Rot);
            Kill(ctx, p.First(u => u.Def.Id == "mug"), foe);   // 規定のムグの分裂（胞子2体・既定の走査）
            var spores = ctx.Events.Where(x => x.Kind == BattleEventKind.Summon && x.Text == UnitCatalog.Spore.Name).Select(x => x.Slot).ToList();
            var w1 = ctx.SummonOverFallen(UnitCatalog.Wight, BattleContext.PlayerTeam)!;
            var order = FormationShape.X.SummonSlots.ToArray();
            bool ok = w0.Slot == order[0] && spores.SequenceEqual(new[] { order[1], order[2] }) && w1.Slot == order[0];
            var (ctx2, p2, foe2) = Board((0, Mate), (4, Mate));
            var corpse = new UnitState { Def = Mate, TeamId = BattleContext.PlayerTeam, Shape = FormationShape.X, Slot = order[0], Hp = 50, MaxHp = 50, Traits = TraitCatalog.Resolve(Mate.Traits) };
            AddUnit.Invoke(ctx2, new object[] { corpse });
            Kill(ctx2, corpse, foe2);
            var w2 = ctx2.SummonOverFallen(UnitCatalog.Wight, BattleContext.PlayerTeam)!;
            Expect("湧く枠: ムグの分裂（胞子2体・既定の走査）は倒れた亡者の枠も塞いだまま次の枠へ（規定と同じ）・新しい札の経路は倒れた儚い駒の枠を使い、倒れた儚くない駒の枠は塞ぐ", ok && w2.Slot == order[1],
                $"召喚枠の順 {string.Join(",", order)}・亡者 {w0.Slot} → 倒れた後の分裂の胞子 {string.Join(",", spores)}・新しい札の経路の次の亡者 {w1.Slot}（倒れた亡者の枠）・儚くない死体の枠 {order[0]} のとき亡者 {w2.Slot}");
        }
        {
            // B: ムグが倒れた後は芽吹かない（ムグ自身の死でも芽吹かない・分裂の2体だけ）
            var (ctx, p, foe) = Board((0, Mate), (1, Mate), (2, UnitCatalog.MugSproutM));
            var mug = p.First(u => u.Def.Id == "mug");
            Kill(ctx, mug, foe);
            int afterMug = ctx.Events.Count(x => x.Kind == BattleEventKind.Summon);
            Kill(ctx, p[0], foe); Kill(ctx, p[1], foe);
            int after = ctx.Events.Count(x => x.Kind == BattleEventKind.Summon);
            Expect("B: ムグが倒れた後は芽吹かない（ムグの死は分裂の2体だけ）", afterMug == 2 && after == 2 && Tal(ctx, mug).R377Sprouts == 0, $"ムグの死で {afterMug} 体・その後の編成 2 体の死で +{after - afterMug}");
        }

        // ---- 層: R-繋 は増えず繋ぐ ／ R-積 は 1 増える ----
        {
            var (ctx2, p2, foe2) = Board((0, Mate), (1, Mate), (2, Mate), (4, UnitCatalog.RicaLink));
            var r2 = p2.First(u => u.Def.Id == "rica");
            Kill(ctx2, p2[0], foe2); Kill(ctx2, p2[1], foe2);   // T1: 2 層 ＋ 亡者 2 体
            var ws = Living(ctx2, "wight");
            var seq = new List<int> { r2.RawCounter("necro") };
            TurnStart(ctx2, r2, 2); seq.Add(r2.RawCounter("necro"));            // 2（T1 の死で繋がる）
            ctx2.LoseHp(ws[0], 999, TraitId.Rot); seq.Add(r2.RawCounter("necro"));   // 亡者の死: 2 のまま
            TurnStart(ctx2, r2, 3); seq.Add(r2.RawCounter("necro"));            // 2（亡者の死で繋がる）
            TurnStart(ctx2, r2, 4); seq.Add(r2.RawCounter("necro"));            // 1（T3 に死が無い）
            long saved = Tal(ctx2, r2).R377ChainSaved;
            Expect("R-繋: 亡者の死で層が増えない・亡者が倒れた次のターンに層が薄れない（その次は薄れる）・繋がったターンを数える", seq.SequenceEqual(new[] { 2, 2, 2, 2, 1 }) && saved == 1, $"2 層から: {string.Join(" → ", seq)}・亡者で繋がった {saved}");

            var (ctx3, p3, foe3) = Board((0, Mate), (1, Mate), (4, UnitCatalog.RicaStack));
            var r3 = p3.First(u => u.Def.Id == "rica");
            Kill(ctx3, p3[0], foe3);   // T1: 1 層 ＋ 亡者
            int l1 = r3.RawCounter("necro");
            r3.Hp = 30;
            ctx3.LoseHp(Living(ctx3, "wight").Single(), 999, TraitId.Rot);
            int l2 = r3.RawCounter("necro"), hp2 = r3.Hp;
            Expect("R-積: 亡者の死で層が 1 増える（胞子と同じ・回復 12 も）", l1 == 1 && l2 == 2 && hp2 == 30 + NecroTrait.HealOnDeath && Tal(ctx3, r3).R377ChainSaved == 0, $"層 {l1} → {l2}・HP 30 → {hp2}");
        }

        // ---- 代償 ----
        {
            var (ctx, p, foe) = Board((0, UnitCatalog.Golm), (1, Mate), (3, UnitCatalog.RicaLink));
            var rica = p.First(u => u.Def.Id == "rica"); var golm = p.First(u => u.Def.Id == "golm"); var mate = p.First(u => u.Def.Id == Mate.Id);
            var raise = rica.Traits.OfType<RaiseDeadTrait>().Single();
            mate.Hp = 0;
            rica.Hp = 30; raise.OnAllyDeath(ctx, rica, mate); int a = rica.Hp, ga = golm.Hp;
            rica.Hp = 5; raise.OnAllyDeath(ctx, rica, mate); int b2 = rica.Hp;
            bool ok = a == 22 && ga == golm.MaxHp && b2 == 1 && rica.IsAlive && Tal(ctx, rica).R377CostSelf == 12 && Tal(ctx, rica).DamageTaken == 0;
            Expect("A: 代償はリィカの HP から直接 8（前にゴルムがいても肩代わりされない・ダメージとして数えない）・HP 5 からは 4 で HP 1（倒れない）", ok, $"30 → {a}・ゴルム {ga}/{golm.MaxHp}・5 → {b2}");
        }

        // ---- 台の上で ----
        {
            long battles = 0, wights = 0, overCap = 0, revived = 0, mism = 0, sprouts = 0, sproutOver = 0;
            for (int vi = 1; vi < VerNames.Length; vi++)
                foreach (var name in DeathRows)
                {
                    var f0 = Row(name);
                    if (!Applies(f0, vi)) continue;
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            battles++;
                            var (r, p, _) = I.Raw(Apply(f0, vi), w, sd, verbose: true);
                            var ws = r.Events.Where(x => x.Kind == BattleEventKind.Summon && x.Text == UnitCatalog.Wight.Name).Select(x => x.TargetId!.Value).ToHashSet();
                            wights += ws.Count;
                            if (ws.Count > RaiseDeadTrait.PerBattle) overCap++;
                            revived += r.Events.Count(x => x.Kind == BattleEventKind.Revive && x.TargetId is int ti && ws.Contains(ti));
                            long raises = r.TallyByUnit.TryGetValue("rica", out var rt) ? rt.R377Raises : 0;
                            if (raises != ws.Count) mism++;
                            long sp = r.TallyByUnit.TryGetValue("mug", out var mt) ? mt.R377Sprouts : 0;
                            sprouts += sp; if (sp > MugSproutTrait.PerBattle) sproutOver++;
                        }
                }
            Expect("台の上で: 版の死の台 × 4 版 × 4 波 × seed 0..9 で亡者 ／ 芽吹きは1戦に 3 体以下・亡者は蘇生されない・起こした数の計数と台本の数が一致", overCap == 0 && sproutOver == 0 && revived == 0 && mism == 0 && wights > 0 && sprouts > 0,
                $"{battles} 戦・亡者 {wights} 体・芽吹き {sprouts} 体・3 体超え {overCap} ／ {sproutOver}・蘇生 {revived}・食い違い {mism}");
        }

        // ---- 規定は今どおり ----
        {
            long d = 0, cells = 0;
            var play = Pin378(Presets.Versions.First(r => r.Name == "試遊・死の連鎖").F);   // 第379期: 試遊の台は規定の駒（目覚めたリィカ）になったので、第378期までの規定に固定して比べる
            bool same = play.Occupied().Select(o => o.Def).SequenceEqual(Row("死の連鎖 (リィカ軸)").Occupied().Select(o => o.Def));
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M5 })
                for (int sd = 0; sd < 20; sd++) { cells++; if (!Same(I.Raw(play, w, sd).R, I.Raw(Row("死の連鎖 (リィカ軸)"), w, sd).R)) d++; }
            Expect("試遊・死の連鎖 ＝ `compare` の「死の連鎖 (リィカ軸)」と同じ並び ／ 駒（規定）・5 波 × seed 0..19 で勝敗 ／ T ／ 生存数が同じ", same && d == 0, $"{S307.Seats(play)}・{cells} 戦・差 {d}");
            // `docs/balance.md` のリィカ ／ ムグのいる行（seed 0..199）を規定のまま測り直して一致を見る（全 64 行の一致は `checkup check` の 320 セル）。
            var bal = BalanceH378();   // 第379期: 第378期の `docs/balance.md`（規定のリィカが変わる前）
            int rows = 0, bad = 0;
            foreach (var (name, f) in CompareBuilds().Select(r => (r.Name, F: Pin378(r.F))).Where(r => HasRica(r.F) || HasMug(r.F)))
            {
                rows++;
                for (int wi = 0; wi < EnemyCatalog.Stages.Count; wi++)
                {
                    int wins = 0;
                    for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                    if (!bal.TryGetValue(name, out var cs) || cs[wi] != $"{100.0 * wins / CompareSeeds:F1}%") bad++;
                }
            }
            Expect("規定のリィカ ／ ムグのいる `compare` の行が `docs/balance.md` と一致（seed 0..199・いない行は版の写像で変わらない）", bad == 0 && rows > 0, $"{rows} 行 × {EnemyCatalog.Stages.Count} 波・ずれ {bad}");
        }

        // ---- 素体 ／ 寄せ集めは器具の中だけ ----
        {
            bool ok = !UnitCatalog.Everyone.Any(d => d.Id.EndsWith("_plain", StringComparison.Ordinal))
                && !CompareBuilds().Concat(Presets.Versions).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => o.Def.Id.EndsWith("_plain", StringComparison.Ordinal)));
            var mix = MixBoardsOf(UnitCatalog.RicaH378); var mix2 = MixBoardsOf(UnitCatalog.RicaH378);
            bool mixOk = mix.Length == MixBoards && mix.All(f => f.Occupied().Count() == 5 && f.Occupied().Count(o => o.Def.Id == "rica") == 1 && f.Occupied().Count(o => DeathIds.Contains(o.Def.Id)) == 1 && f.Occupied().Select(o => o.Def.Id).Distinct().Count() == 5)
                && mix.Select(S307.Seats).SequenceEqual(mix2.Select(S307.Seats));
            Expect("§2-3 の素体 ／ 寄せ集めの台は器具の中だけ（カタログ ／ `compare` ／ 試遊の台に素体が無い）・寄せ集めは 50 台ともリィカ ＋ 死の札を持たない4枚（重複なし）・台の乱数は固定（2回作って同じ）", ok && mixOk);
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            // 第378期: 「版・R-繋 死の連鎖」は `Presets.Versions` から外したので、3台は並びの関数から組む（中身は同じ）。
            foreach (var f in new[] { Presets.DeathChain377(UnitCatalog.RicaH378, UnitCatalog.MugH379), Presets.DeathChain377(UnitCatalog.RicaLink, UnitCatalog.MugH379), Presets.DeathChain377(UnitCatalog.RicaStack, UnitCatalog.MugSproutM) })   // 第379期: 規定の台は第378期までの規定のリィカで
            {
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5, I.T9 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（試遊の3台 × 勇者 ／ 近衛 ／ 大隊 ／ 第5波 ／ 的・九・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool ok = new[] { TraitId.RicaRaiseDead, TraitId.RicaRaiseDeadStack, TraitId.Rot, TraitId.MugSprout }.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第377期"));
            Expect("`checkup` の札の分類表に 亡者起こし（R-繋 ／ R-積）／ 朽ち ／ 亡骸の芽吹き が第377期の行として入っている（ソースを読んで確かめる）", ok);
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            // 第378期: 「版・R-繋 死の連鎖」を外した。第379期: 第377 ／ 378期の「版・」の台をすべて外した——頭の 12 台を見て、版の並びは関数から組む。
            bool rows = vr.Take(12).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "試遊・毒 席替え", "試遊・毒 紅", "試遊・ヴィオ 台B", "試遊・死の連鎖" });
            var basis = Row("死の連鎖 (リィカ軸)");
            bool vers = Presets.DeathChain377(UnitCatalog.RicaLink, UnitCatalog.MugH379).Occupied().Select(o => o.Def).SequenceEqual(Apply(basis, 1).Occupied().Select(o => o.Def))
                && Presets.DeathChain377(UnitCatalog.RicaStack, UnitCatalog.MugSproutM).Occupied().Select(o => o.Def).SequenceEqual(Apply(basis, 4).Occupied().Select(o => o.Def));
            Expect("`Presets.Versions` の頭 ＝ 第376期の11台 ＋「試遊・死の連鎖」（第378 ／ 379期に「版・」の死の台を外した・R-繋 ／ R-積M の並びは `DeathChain377`）", rows && vers, string.Join("、", vr));
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 9f6e7b3a:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第376期の後の HEAD（9f6e7b3a）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
