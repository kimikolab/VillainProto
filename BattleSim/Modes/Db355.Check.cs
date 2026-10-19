using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// db355 の自己検査（第355期・指示書 §9）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿と台本で確かめる（盤面は動かさない）。
static partial class Db355Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
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
    static UnitDef Plain(string id, int hp = 999, int atk = 10) => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} db355 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            bool def = UnitCatalog.Mudo.Traits.SequenceEqual(UnitCatalog.MudoM12.Traits) && UnitCatalog.Mudo.PlusText == UnitCatalog.MudoM12.PlusText
                && UnitCatalog.Wata.Traits.SequenceEqual(UnitCatalog.WataFeedMudo.Traits) && UnitCatalog.Wata.PlusText == UnitCatalog.WataFeedMudo.PlusText
                && UnitCatalog.MudoH354.Traits.SequenceEqual(new[] { TraitId.Erupt, TraitId.Smear, TraitId.Hex })
                && UnitCatalog.WataH354.Traits.SequenceEqual(new[] { TraitId.Relay, TraitId.RelayWide, TraitId.RelayFeed })
                && UnitCatalog.Mudo.MaxHp == UnitCatalog.MudoH354.MaxHp && UnitCatalog.Wata.MaxHp == UnitCatalog.WataH354.MaxHp;
            Expect("段0: 規定のムド ＝ 第354期の M1＋M2（`MudoM12` と同じ札 ／ 文面）・規定のワタ ＝ 配り（`WataFeedMudo` と同じ）・旧（`MudoH354` ／ `WataH354`）はそのまま", def);
            long rage = 0, fed = 0, oldRage = 0, oldFed = 0;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat }) for (int s = 0; s < 4; s++)
            {
                var (rn, _, _) = I.Raw(Play, w, s); var (ro, _, _) = I.Raw(Pin354(Play), w, s);
                rage += rn.TallyByUnit["mudo"].Db354CurseRage; fed += rn.TallyByUnit["wata"].Db354FedMudo;
                oldRage += ro.TallyByUnit["mudo"].Db354CurseRage; oldFed += ro.TallyByUnit["wata"].Db354FedMudo;
            }
            Expect("段0: 規定のムド ／ ワタ（「試遊・呪い」そのもの）では M1（呪いの怒り）と配り（ワタ → ムド）が働き、旧の定義（`Pin354`）では働かない", rage > 0 && fed > 0 && oldRage == 0 && oldFed == 0,
                $"規定 怒り {rage} ／ ムドへ {fed}・旧 {oldRage} ／ {oldFed}");
            bool pin = ReferenceEquals(PinM354(UnitCatalog.Mudo), UnitCatalog.MudoH354) && ReferenceEquals(PinM354(UnitCatalog.Wata), UnitCatalog.WataH354)
                && ReferenceEquals(PinU353(UnitCatalog.Mudo), UnitCatalog.MudoH354) && ReferenceEquals(PinW351(UnitCatalog.WataH354), UnitCatalog.WataH351) && ReferenceEquals(PinB349(UnitCatalog.Mudo), UnitCatalog.MudoH354);
            Expect("段0: 過去の器具の固定（`PinM354` ／ 古い口の終端 `PinB349` → `PinW351` → `PinU353` → `PinM354`・`PinW351` は `WataH354` も第351期までのワタへ）", pin);
        }

        // ---- 版の定義 ----
        {
            var n = UnitCatalog.NelH355;   // 第356期: 規定のネルが N-100 になったので旧と比べる
            bool body(UnitDef d, UnitDef b) => d.Id == b.Id && d.MaxHp == b.MaxHp && d.Attack == b.Attack && d.Speed == b.Speed && d.Pattern == b.Pattern && d.Actions == b.Actions && d.Advances == b.Advances;
            bool n1 = body(UnitCatalog.NelN100, n) && UnitCatalog.NelN100.Traits.SequenceEqual(n.Traits.Append(TraitId.CurseScar));
            bool n5 = body(UnitCatalog.NelN50, n) && UnitCatalog.NelN50.Traits.SequenceEqual(n.Traits.Append(TraitId.CurseScarHalf));
            bool u = body(UnitCatalog.UtsuU, UnitCatalog.Utsu) && UnitCatalog.UtsuU.Traits.SequenceEqual(UnitCatalog.Utsu.Traits.Append(TraitId.UtsuScar));
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.CurseScarHalf or TraitId.UtsuScar)).Select(d => d.Id).ToArray();
            // 第356期: 規定のネル ＝ N-100（`CurseScar` の保持者はネルだけ）
            bool def356 = UnitCatalog.Nel.Traits.SequenceEqual(UnitCatalog.NelN100.Traits) && UnitCatalog.Nel.PlusText == UnitCatalog.NelN100.PlusText
                && UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Contains(TraitId.CurseScar)).Select(d => d.Id).SequenceEqual(new[] { "nel" });
            Expect("版の定義: N-100 ＝ 旧のネル ＋ `CurseScar` ／ N-50 ＝ 旧のネル ＋ `CurseScarHalf` ／ U ＝ 規定のウツ ＋ `UtsuScar`・体と手番はそのまま・第356期から規定のネル ＝ N-100（`CurseScar` の保持者はネルだけ・ほかの2枚は 0 枚）", n1 && n5 && u && def356 && held.Length == 0, $"n100 {n1}・n50 {n5}・u {u}・規定 {def356}・{string.Join(",", held)}");
            // 第356期 段0: 規定のネル（「試遊・呪い」そのもの）で N-100 が働き、旧の定義（`Pin355`）では働かない
            long now = 0, old = 0;
            var raw = Presets.Versions.First(r => r.Name == PlayRow).F;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat }) for (int s = 0; s < 4; s++)
            {
                var (rn, _, _) = I.Raw(raw, w, s); var (ro, _, _) = I.Raw(Pin355(raw), w, s);
                now += rn.TallyByUnit.Values.Sum(t => t.Cs355ScarTaken); old += ro.TallyByUnit.Values.Sum(t => t.Cs355ScarTaken);
            }
            Expect("第356期 段0: 規定のネル（「試遊・呪い」そのもの）で N-100 が働き（最大HPが削れる）、旧の定義（`Pin355`）では働かない", now > 0 && old == 0, $"規定 {now}・旧 {old}");
        }

        // ---- N-100 ／ N-50 ----
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.NelN100, front3: Plain("ally")), Formation.Build(front1: Plain("cursed"), front3: Plain("clean"), center: Plain("tiny", hp: 10)), out var p, out var e);
            var ally = U(p, "ally"); var cursed = U(e, "cursed"); var clean = U(e, "clean"); var tiny = U(e, "tiny");
            cursed.SetCounter(StatusKeys.Curse, 1); tiny.SetCounter(StatusKeys.Curse, 1); ally.SetCounter(StatusKeys.Curse, 1);
            ctx.ApplyDamage(cursed, 100, ally);
            bool a = cursed.MaxHp == 899 && cursed.Hp == 899;
            ctx.ApplyDamage(clean, 100, ally);
            bool b = clean.MaxHp == 999 && clean.Hp == 899;
            ctx.ApplyDamage(ally, 50, clean);
            bool c = ally.MaxHp == 999 && ally.Hp == 949;
            ctx.ApplyDamage(tiny, 9, ally);
            bool floor = tiny.MaxHp == 1 && tiny.Hp == 1;
            ctx.ApplyDamage(cursed, 20, null, burnTick: true);
            bool tick = cursed.MaxHp == 879;
            Expect("N-100: 呪われている敵だけ、受けた傷（実際に減った HP）と同じだけ最大HPが削れる（999 → 899・今の HP も 899）・呪われていない敵は削れない", a && b, $"呪われた {cursed.MaxHp}・呪われていない {clean.MaxHp}（HP {clean.Hp}）");
            Expect("N-100: 味方は呪われていても削れない（ネルの陣営）・下限 1・刻み（出どころなし）の傷も削れる", c && floor && tick, $"味方 {ally.MaxHp}・小さい敵 {tiny.MaxHp}・刻みの後 {cursed.MaxHp}");
            var c5 = Ctx(Formation.Build(front1: UnitCatalog.NelN50, front3: Plain("ally")), Formation.Build(front1: Plain("cursed")), out var p5, out var e5);
            e5[0].SetCounter(StatusKeys.Curse, 1);
            c5.ApplyDamage(e5[0], 101, U(p5, "ally"));
            Expect("N-50: 削る量は半分（切り捨て・101 → 50）", e5[0].MaxHp == 949 && e5[0].Hp == 898, $"最大HP {e5[0].MaxHp}・HP {e5[0].Hp}");
            var c0 = Ctx(Formation.Build(front1: UnitCatalog.NelH355, front3: Plain("ally")), Formation.Build(front1: Plain("cursed")), out var p0, out var e0);
            e0[0].SetCounter(StatusKeys.Curse, 1);
            c0.ApplyDamage(e0[0], 100, U(p0, "ally"));
            Expect("規定のネル（札なし）では呪われていても削れない（対照）", e0[0].MaxHp == 999, $"{e0[0].MaxHp}");
            // ボスの回復は削れた最大HPを読む（40% ＝ 削れた最大HPの 4 割・満タン ＝ 削れた最大HP）
            var cb = Ctx(Formation.Build(front1: UnitCatalog.NelN100, front3: Plain("ally")), Formation.Build(center: EnemyCatalog.BossRegular), out var pb, out var eb);
            var boss = eb[0];
            boss.SetCounter(StatusKeys.Curse, 1);
            cb.ApplyDamage(boss, 1000, U(pb, "ally"));
            int max = boss.MaxHp;
            boss.Hp = 500;
            var mend = boss.Traits.First(t => t.Id == TraitId.BossMend40);
            mend.OnTurnStart(cb, boss);
            int h1 = boss.Hp;
            mend.OnTurnStart(cb, boss);
            Expect("N-100: ボスの回復は削れた最大HPまでしか戻さない（3,000 → 2,000・500 から 4 割 ＝ 800 癒えて 1,300・次は満タンの 2,000 まで）", max == 2000 && h1 == 1300 && boss.Hp == 2000, $"最大HP {max}・回復 {h1} → {boss.Hp}");
        }

        // ---- U ----
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.UtsuU, front3: Plain("ally")), Formation.Build(front1: Plain("foe"), front3: Plain("foe2")), out var p, out var e);
            var utsu = U(p, "utsu"); var ally = U(p, "ally"); var foe = U(e, "foe"); var foe2 = U(e, "foe2");
            ctx.ApplyDamage(foe, 30, utsu);
            bool a = foe.MaxHp == 969 && foe.Hp == 969;
            ctx.ApplyDamage(foe, 30, ally);
            bool b = foe.MaxHp == 969;
            ctx.ApplyDamage(foe2, 30, utsu, hexShare: true);
            bool c = foe2.MaxHp == 999;
            ctx.ApplyDamage(utsu, 10, foe);
            bool d = utsu.MaxHp == UnitCatalog.Utsu.MaxHp;
            Expect("U: ウツの一撃で減らした HP と同じだけ削れる（呪われているかは問わない）・ウツ以外の一撃 ／ 分け合いで散った分 ／ ウツが受けた傷では削れない", a && b && c && d, $"敵 {foe.MaxHp}・分け合い {foe2.MaxHp}・ウツ {utsu.MaxHp}");
        }

        // ---- 台の上で ----
        {
            // 第356期: 第355期の「版・」の3台は外した（試遊の4台は頭に残る）
            bool vrows = Presets.Versions.Select(r => r.Name).Take(4).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", PlayRow })
                && !Presets.Versions.Any(r => VersionRows.Any(x => x.Row == r.Name));
            Expect("`Presets.Versions` の頭 ＝ 試遊の4台・第355期の「版・」の3台は無い（第356期に外した）", vrows, string.Join("、", Presets.Versions.Select(r => r.Name)));

            long s100 = 0, s50 = 0, su = 0, s0 = 0, suOther = 0;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat }) for (int s = 0; s < 4; s++)
            {
                long Sc(BattleResult r) => r.TallyByUnit.Values.Sum(t => t.Cs355ScarTaken);
                var (r1, _, _) = I.Raw(Apply(Play, V("n100")), w, s); var (r5, _, _) = I.Raw(Apply(Play, V("n50")), w, s);
                var (ru, _, _) = I.Raw(Apply(Play, V("u")), w, s); var (r0, _, _) = I.Raw(Play, w, s);
                s100 += Sc(r1); s50 += Sc(r5); su += Sc(ru); s0 += Sc(r0);
                suOther += ru.TallyByUnit.Where(kv => kv.Key != "utsu").Sum(kv => kv.Value.Cs355ScarDealt);
            }
            Expect("台の上で版が働く（N-100 > N-50 > 0・U > 0 で削り手はウツだけ・規定は 0）", s100 > s50 && s50 > 0 && su > 0 && suOther == 0 && s0 == 0, $"N-100 {s100}・N-50 {s50}・U {su}（ウツ以外 {suOther}）・規定 {s0}");
            var (e1, _, _) = I.Raw(Apply(Play, V("n100")), I.Boss, 0, verbose: true);
            var (eb, _, _) = I.Raw(Play, I.Boss, 0, verbose: true);
            int k1 = e1.Events.Count(x => x.Kind == BattleEventKind.Scar), k0 = eb.Events.Count(x => x.Kind == BattleEventKind.Scar);
            Expect("台本: 最大HPが削れた出来事（`Scar`・第291期の爪痕と同じ）は版の戦にだけ出る（規定の戦には出ない）", k1 > 0 && k0 == 0, $"N-100 {k1}・規定 {k0}");
            bool same = true;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M2 }) for (int s = 0; s < 4; s++)
            {
                var (a, _, _) = I.Raw(Apply(Play, V("base")), w, s); var (z, _, _) = I.Raw(Play, w, s);
                same &= a.PlayerWon == z.PlayerWon && a.Turns == z.Turns && a.PlayerSurvivors == z.PlayerSurvivors;
            }
            Expect("「規定」の版は台そのもの（差し替えても勝敗 ／ T ／ 生存が同じ）", same);
        }

        // ---- verbose ／ PickOne ----
        {
            int diff = 0, n = 0;
            foreach (var v in Vers) foreach (var b in Boards()) foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M5 }) for (int s = 0; s < 3; s++)
            {
                if (!Applies(b, v)) continue;
                var (a, _, _) = I.Raw(Apply(b.F, v), w, s, verbose: true); var (z, _, _) = I.Raw(Apply(b.F, v), w, s, verbose: false);
                n++; if (a.PlayerWon != z.PlayerWon || a.Turns != z.Turns || a.PlayerSurvivors != z.PlayerSurvivors) diff++;
            }
            Expect("verbose の有無で勝敗 ／ T ／ 生存が同じ（版 × 台 × 波 5 × seed 0..2）", diff == 0, $"{n} 戦・差 {diff}");
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 4369c616:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Db355*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第354期（4369c616）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    /// <summary>§7 の試遊の台（名前 ↔ 版）。</summary>
    internal static readonly (string Row, string Ver)[] VersionRows = { ("版・N-100 呪い", "n100"), ("版・N-50 呪い", "n50"), ("版・U 呪い", "u") };
}
