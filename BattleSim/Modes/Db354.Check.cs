using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// db354 の自己検査（第354期・指示書 §9）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿と台本で確かめる（盤面は動かさない）。
static partial class Db354Diag
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
    static UnitDef Plain(string id, int hp = 200, int atk = 10) => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static UnitTally Tly(BattleContext c, string id) => ((Dictionary<string, UnitTally>)typeof(BattleContext)
        .GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!).GetValueOrDefault(id) ?? new UnitTally();

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} db354 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            bool def = UnitCatalog.Utsu.Traits.SequenceEqual(new[] { TraitId.Perverse, TraitId.PerverseOver }) && UnitCatalog.UtsuH353.Traits.SequenceEqual(new[] { TraitId.Perverse })
                && UnitCatalog.Utsu.PlusText == UnitCatalog.UtsuUTo.PlusText && UnitCatalog.Utsu.MinusText == UnitCatalog.UtsuUTo.MinusText && UnitCatalog.Utsu.MaxHp == UnitCatalog.UtsuH353.MaxHp
                && UnitCatalog.Utsu.Attack == UnitCatalog.UtsuH353.Attack && UnitCatalog.Utsu.Speed == UnitCatalog.UtsuH353.Speed && UnitCatalog.UtsuUTo.Traits.SequenceEqual(UnitCatalog.Utsu.Traits);
            Expect("段0: 規定のウツ ＝ 逆しま ＋ `PerverseOver`（第353期の UT-o と同じ札 ／ 文面 ／ 体）・旧（`UtsuH353`）は逆しまだけ", def);
            var c = Ctx(Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.UtsuH353), Formation.Build(center: Plain("foe", hp: 999)), out var p, out _);
            var now = p[0]; var old = p[1];
            now.AtkBonus = -20; old.AtkBonus = -20;
            bool over = now.CurrentAttack == 9 + 12 + 3 * 8 && old.CurrentAttack == 9 + 20;
            long nowOver = 0, oldOver = 0;
            for (int s = 0; s < 4; s++)
            {
                var (rn, _, _) = I.Raw(Play, I.Boss, s, verbose: true); var (ro, _, _) = I.Raw(Pin353(Play), I.Boss, s, verbose: true);
                nowOver += rn.TallyByUnit["utsu"].DamageToEnemy; oldOver += ro.TallyByUnit["utsu"].DamageToEnemy;
            }
            Expect("段0: 規定のウツで上限超えが働き（下げ幅 20 → 攻 45）、旧の定義では働かない（29）・試遊・呪い × ボスのウツの与ダメは規定 > 旧", over && nowOver > oldOver, $"{now.CurrentAttack} ／ {old.CurrentAttack}・与ダメ {nowOver} ／ {oldOver}（seed 0..3）");
            bool pin = ReferenceEquals(PinU353(UnitCatalog.Utsu), UnitCatalog.UtsuH353) && ReferenceEquals(PinB349(UnitCatalog.Utsu), UnitCatalog.UtsuH353) && ReferenceEquals(PinU353(UnitCatalog.Mudo), UnitCatalog.Mudo);
            Expect("段0: 過去の器具の固定（`PinU353` ／ 古い口の終端 `PinB349` → `PinW351` → `PinU353`）は規定のウツを旧に替える", pin);
        }

        // ---- 版の定義 ----
        {
            var m = UnitCatalog.Mudo;
            bool body(UnitDef d) => d.Id == "mudo" && d.MaxHp == m.MaxHp && d.Attack == m.Attack && d.Speed == m.Speed && d.Pattern == m.Pattern && d.Actions == m.Actions;
            bool m1 = body(UnitCatalog.MudoM1) && UnitCatalog.MudoM1.Traits.SequenceEqual(m.Traits.Append(TraitId.MudoCurseRage));
            bool m2 = body(UnitCatalog.MudoM2) && UnitCatalog.MudoM2.Traits.SequenceEqual(m.Traits.Append(TraitId.MudoCurseHeavy));
            bool m12 = body(UnitCatalog.MudoM12) && UnitCatalog.MudoM12.Traits.SequenceEqual(m.Traits.Append(TraitId.MudoCurseRage).Append(TraitId.MudoCurseHeavy));
            bool f = UnitCatalog.WataFeedMudo.Traits.SequenceEqual(UnitCatalog.Wata.Traits.Append(TraitId.RelayFeedMudo)) && UnitCatalog.WataFeedMudo.Id == "wata";
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.MudoCurseRage or TraitId.MudoCurseHeavy or TraitId.RelayFeedMudo)).Select(d => d.Id).ToArray();
            Expect("版の定義: M1 ＝ ムド ＋ `MudoCurseRage` ／ M2 ＝ ＋ `MudoCurseHeavy`（暴発の後ろ）／ M1＋M2 ／ 配り ＝ 規定のワタ ＋ `RelayFeedMudo`・体はそのまま・保持者 0 枚", m1 && m2 && m12 && f && held.Length == 0,
                $"m1 {m1}・m2 {m2}・m12 {m12}・配り {f}・{string.Join(",", held)}");
        }

        // ---- M1 ----
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.MudoM1), Formation.Build(center: Plain("foe", hp: 9999)), out var p, out _);
            var mudo = p[0];
            ctx.Dull(mudo, 4);
            bool a = EruptTrait.RageOf(mudo) == 1 && mudo.RawCounter(EruptTrait.CurseFracKey) == 1;
            ctx.Dull(mudo, 2);
            bool b = EruptTrait.RageOf(mudo) == 2 && mudo.RawCounter(EruptTrait.CurseFracKey) == 0 && Tly(ctx, "mudo").EruptFires == 0;
            ctx.Dull(mudo, 3);
            var t = Tly(ctx, "mudo");
            bool fire = t.EruptFires == 1 && t.EruptSwings == 3 && EruptTrait.RageOf(mudo) == 0 && t.Db354CurseRage == 3;
            Expect("M1: 下げ幅 3 につき怒り +1・端数は持ち越す（4 → 怒り 1・端数 1 ／ ＋2 → 怒り 2・端数 0）", a && b, $"怒り {EruptTrait.RageOf(mudo)}");
            Expect("M1: 呪いで閾 3 に届いたらその場で暴発する（3 連撃・怒りは 0 に戻る）", fire, $"暴発 {t.EruptFires}・発 {t.EruptSwings}");
            var c0 = Ctx(Formation.Build(front1: UnitCatalog.Mudo), Formation.Build(center: Plain("foe", hp: 999)), out var p0, out _);
            c0.Dull(p0[0], 9);
            Expect("M1: 規定のムドは呪いで怒りが溜まらない（対照）", EruptTrait.RageOf(p0[0]) == 0, $"{EruptTrait.RageOf(p0[0])}");
            var cw = Ctx(Formation.Build(front1: UnitCatalog.MudoM1, back1: UnitCatalog.Wata), Formation.Build(center: Plain("foe", hp: 999)), out var pw, out var ew);
            cw.Dull(U(pw, "mudo"), 4);
            var cd = Ctx(Formation.Build(front1: UnitCatalog.Mudo, back1: UnitCatalog.Wata), Formation.Build(center: Plain("foe", hp: 999)), out var pd, out var ed);
            cd.Dull(U(pd, "mudo"), 4);
            var c2 = Ctx(Formation.Build(front1: UnitCatalog.MudoM2, back1: UnitCatalog.Wata), Formation.Build(center: Plain("foe", hp: 999)), out var p2, out var e2);
            c2.Dull(U(p2, "mudo"), 4);
            Expect("M1 ／ M2: ワタはムドの弱体を拾わない（規定のムドなら拾って敵へ渡す）", U(pw, "mudo").AtkBonus == -4 && ew[0].AtkBonus == 0 && U(pd, "mudo").AtkBonus == 0 && ed[0].AtkBonus == -4 && U(p2, "mudo").AtkBonus == -4 && e2[0].AtkBonus == 0,
                $"M1 ムド {U(pw, "mudo").AtkBonus}・敵 {ew[0].AtkBonus} ／ 規定 ムド {U(pd, "mudo").AtkBonus}・敵 {ed[0].AtkBonus} ／ M2 ムド {U(p2, "mudo").AtkBonus}");
        }

        // ---- M2 ----
        {
            var c = Ctx(Formation.Build(front1: UnitCatalog.MudoM2, front3: UnitCatalog.Mudo), Formation.Build(center: Plain("foe", hp: 999)), out var p, out _);
            var m2 = p[0]; var m0 = p[1];
            m2.AtkBonus = -6; m0.AtkBonus = -6;
            int n2 = m2.CurrentAttack, n0 = m0.CurrentAttack;
            m2.SetCounter(EruptTrait.BurstKey, 1); m0.SetCounter(EruptTrait.BurstKey, 1);
            int b2 = m2.CurrentAttack, b0 = m0.CurrentAttack;
            m2.SetCounter(EruptTrait.BurstKey, 0); m0.SetCounter(EruptTrait.BurstKey, 0);
            int bonus = EruptTrait.HeavySwingBonus;
            Expect("M2: 暴発の1発 ＝ 床（素攻 3）＋ 上乗せ 10 ＋ 下げ幅 6 × 2 ＝ 25（規定は 13）・自分の手番の通常攻撃は変わらない（下げ幅 6 で 0）", b2 == 3 + bonus + 12 && b0 == 3 + bonus && n2 == n0 && n2 == 0,
                $"暴発 {b2} ／ {b0}・通常 {n2} ／ {n0}");
        }

        // ---- 配り ----
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.UtsuH353, front3: Plain("far"), center: UnitCatalog.MudoM12, back1: UnitCatalog.WataFeedMudo, back3: Plain("b3")),
                Formation.Build(center: Plain("foe", hp: 9999)), out var p, out var e);
            var utsu = U(p, "utsu"); var mudo = U(p, "mudo"); var far = U(p, "far"); var foe = e[0];
            ctx.Dull(far, 2);
            bool first = utsu.AtkBonus == -2 && mudo.AtkBonus == 0 && foe.AtkBonus == 0;
            utsu.AtkBonus = -12;
            ctx.Dull(far, 2);
            bool second = mudo.AtkBonus == -2 && foe.AtkBonus == 0;
            mudo.AtkBonus = -11;
            ctx.Dull(far, 2);
            bool third = mudo.AtkBonus == -12 && foe.AtkBonus == -1;
            ctx.Dull(far, 2);
            bool rest = mudo.AtkBonus == -12 && foe.AtkBonus == -3 && Tly(ctx, "wata").Db354FedMudo == 3;
            Expect("配り: ウツ（下げ幅 12 まで）→ ムド（下げ幅 12 まで）→ 最も強い敵 の順に回す", first && second && third && rest, $"ウツ {utsu.AtkBonus}・ムド {mudo.AtkBonus}・敵 {foe.AtkBonus}");
            var c2 = Ctx(Formation.Build(front3: Plain("far"), center: UnitCatalog.MudoM12, back1: UnitCatalog.WataFeedMudo), Formation.Build(center: Plain("foe", hp: 9999)), out var p2, out var e2);
            c2.Dull(U(p2, "far"), 2);
            var c3 = Ctx(Formation.Build(front1: UnitCatalog.UtsuH353, front3: Plain("far"), back1: UnitCatalog.WataFeedMudo), Formation.Build(center: Plain("foe", hp: 9999)), out var p3, out var e3);
            U(p3, "utsu").AtkBonus = -12;
            c3.Dull(U(p3, "far"), 2);
            var c4 = Ctx(Formation.Build(front1: UnitCatalog.UtsuH353, front3: Plain("far"), center: UnitCatalog.MudoM12, back1: UnitCatalog.Wata), Formation.Build(center: Plain("foe", hp: 9999)), out var p4, out var e4);
            U(p4, "utsu").AtkBonus = -12;
            c4.Dull(U(p4, "far"), 2);
            Expect("配り: ウツがいなければムドへ・ムドがいなければ敵へ（飛ばす）・規定のワタ（配りなし）はムドへ回さない", U(p2, "mudo").AtkBonus == -2 && e3[0].AtkBonus == -2 && U(p4, "mudo").AtkBonus == 0 && e4[0].AtkBonus == -2,
                $"ウツなし ムド {U(p2, "mudo").AtkBonus}・ムドなし 敵 {e3[0].AtkBonus}・規定のワタ ムド {U(p4, "mudo").AtkBonus} ／ 敵 {e4[0].AtkBonus}");
        }

        // ---- 台の上で ----
        {
            bool vrows = Presets.Versions.Select(r => r.Name).Take(4).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", PlayRow })
                && Presets.Versions.Skip(4).Select(r => r.Name).SequenceEqual(VersionRows.Select(x => x.Row));
            var f = Presets.Versions.ToDictionary(r => r.Name, r => r.F);
            bool rows = vrows && VersionRows.All(x => Seats(f[x.Row]) == Seats(Play)
                && f[x.Row].Occupied().Any(o => ReferenceEquals(o.Def, V(x.Ver).Mudo)) && f[x.Row].Occupied().Any(o => ReferenceEquals(o.Def, V(x.Ver).Wata))
                && f[x.Row].Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Utsu)));
            Expect("`Presets.Versions` ＝ 試遊の4台 ＋ 「版・M1 呪い」「版・M2 呪い」「版・M1＋M2 呪い」「版・M1＋M2＋配り 呪い」（並びは試遊・呪い・段0 のウツ）", rows, string.Join("、", Presets.Versions.Select(r => r.Name)));
            bool seat = Seats(Play) == "ワタ・ムド・クビ・ネル・ウツ" && Play.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Utsu));
            Expect("「試遊・呪い」＝ ワタ・ムド・クビ・ネル・ウツ（段0 の規定のウツが乗る）", seat, Seats(Play));

            long rage = 0, picked0 = 0, picked1 = 0, fed = 0, heavy = 0, base0 = 0, fastRage = 0;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat }) for (int s = 0; s < 4; s++)
            {
                var (r0, _, _) = I.Raw(Apply(Play, V("base")), w, s); var (r1, _, _) = I.Raw(Apply(Play, V("m1")), w, s);
                var (r2, _, _) = I.Raw(Apply(Play, V("m2")), w, s); var (rf, _, _) = I.Raw(Apply(Play, V("m12f")), w, s);
                var (rq, _, _) = I.Raw(Apply(Row(FastRow), V("m1")), w, s);
                rage += r1.TallyByUnit["mudo"].Db354CurseRage; picked0 += r0.TallyByUnit["wata"].Db354PickedMudo; picked1 += r1.TallyByUnit["wata"].Db354PickedMudo;
                fed += rf.TallyByUnit["wata"].Db354FedMudo; base0 += r0.TallyByUnit["mudo"].Db354CurseRage; fastRage += rq.TallyByUnit["mudo"].Db354CurseRage;
                var t2 = r2.TallyByUnit["mudo"]; var t0 = r0.TallyByUnit["mudo"];
                heavy += t2.EruptSwings == 0 || t0.EruptSwings == 0 ? 0 : (t2.EruptSwingAtk * t0.EruptSwings > t0.EruptSwingAtk * t2.EruptSwings ? 1 : 0);
            }
            Expect("台の上で版が働く（M1 の呪いの怒り ／ 速攻の台でも ／ 配りでムドへ回す > 0・M2 の暴発の1発が規定より重い・規定のワタはムドから拾い M1 では拾わない・規定の呪いの怒りは 0）",
                rage > 0 && fastRage > 0 && fed > 0 && heavy > 0 && picked0 > 0 && picked1 == 0 && base0 == 0,
                $"呪いの怒り {rage}（速攻 {fastRage}）・ムドへ {fed}・重い戦 {heavy}・ムドから拾った 規定 {picked0} ／ M1 {picked1}");

            int Kinds(BattleResult r, string t, int? to = null) => r.Events.Count(e => e.Kind == BattleEventKind.CursePass && e.Text == t && (to is null || e.TargetId == to));
            var (e1, p1, _) = I.Raw(Apply(Play, V("m1")), I.Boss, 0, verbose: true);
            var (ef, pf, _) = I.Raw(Apply(Play, V("m12f")), I.Boss, 0, verbose: true);
            var (eb, _, _) = I.Raw(Play, I.Boss, 0, verbose: true);
            int mudoF = U(pf, "mudo").InstanceId;
            bool marks = Kinds(e1, CursePassLabels.Rage) > 0 && Kinds(ef, CursePassLabels.Feed, mudoF) > 0 && Kinds(eb, CursePassLabels.Rage) == 0 && Kinds(eb, CursePassLabels.Feed, U(p1, "mudo").InstanceId) == 0;
            Expect("台本: `CursePass` の「怒り」は M1 の戦にだけ・ワタ → ムドの「回す」は配りの戦にだけ出る（規定の戦には出ない）", marks, $"怒り {Kinds(e1, CursePassLabels.Rage)}・ムドへ回す {Kinds(ef, CursePassLabels.Feed, mudoF)}");
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
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show e39a713d:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Db354*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第353期（e39a713d）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    /// <summary>§7 の試遊の台（名前 ↔ 版）。</summary>
    internal static readonly (string Row, string Ver)[] VersionRows =
    {
        ("版・M1 呪い", "m1"), ("版・M2 呪い", "m2"), ("版・M1＋M2 呪い", "m12"), ("版・M1＋M2＋配り 呪い", "m12f"),
    };
}
