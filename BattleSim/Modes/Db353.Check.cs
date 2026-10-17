using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// db353 の自己検査（第353期・指示書 §8）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿と台本で確かめる（盤面は動かさない）。
static partial class Db353Diag
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
    static UnitTally Tly(BattleContext c, string id) => ((Dictionary<string, UnitTally>)typeof(BattleContext)
        .GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!).GetValueOrDefault(id) ?? new UnitTally();
    static int Hits(UnitState u) { int h = 1; foreach (var t in u.Traits) h = t.ModifyHitCount(u, h); return h; }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} db353 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義 ----
        {
            var o = UnitCatalog.UtsuUTo; var c = UnitCatalog.UkeUKc; var u = UnitCatalog.Utsu; var k = UnitCatalog.Uke;
            bool od = o.Traits.SequenceEqual(new[] { TraitId.Perverse, TraitId.PerverseOver }) && o.Id == "utsu" && o.MaxHp == u.MaxHp && o.Attack == u.Attack && o.Speed == u.Speed && o.Pattern == u.Pattern && o.Actions == u.Actions;
            bool cd = c.Traits.SequenceEqual(new[] { TraitId.PlunderCum }) && c.Id == "uke" && c.MaxHp == k.MaxHp && c.Attack == k.Attack && c.Speed == k.Speed && c.Pattern == k.Pattern && c.Actions == k.Actions;
            bool def = u.Traits.SequenceEqual(new[] { TraitId.Perverse }) && k.Traits.SequenceEqual(new[] { TraitId.Bear });
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.PerverseOver or TraitId.PlunderCum)).Select(d => d.Id).ToArray();
            Expect("版の定義: UT-o ＝ 逆しま ＋ `PerverseOver`（逆しまの後ろ・体はウツのまま）／ UK-c ＝ 体はウケのまま・札は `PlunderCum` だけ（背負う札なし）", od && cd, $"o {od}・c {cd}");
            Expect("規定のウツ ／ ウケは触っていない・`PerverseOver` ／ `PlunderCum` の保持者は 0 枚", def && held.Length == 0, string.Join(",", held));
        }

        // ---- UT-o ----
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Utsu), Formation.Build(center: Plain("foe", hp: 999)), out var p, out _);
            var c2 = Ctx(Formation.Build(front1: UnitCatalog.UtsuUTo), Formation.Build(center: Plain("foe", hp: 999)), out var p2, out _);
            var b = p[0]; var o = p2[0];
            var rows = new List<string>();
            bool ok = true;
            foreach (int d in new[] { 0, 3, 6, 11, 12, 13, 20 })
            {
                b.AtkBonus = -d; o.AtkBonus = -d;
                int over = Math.Max(0, d - 12);
                int wantB = UnitCatalog.Utsu.Attack + d, wantO = UnitCatalog.Utsu.Attack + Math.Min(d, 12) + 3 * over;
                ok &= b.CurrentAttack == wantB && o.CurrentAttack == wantO && Hits(b) == Hits(o) && Hits(o) == Math.Min(PerverseTrait.MaxHits, 1 + d / PerverseTrait.HitsPerDull);
                rows.Add($"d{d}: {b.CurrentAttack} ／ {o.CurrentAttack}・{Hits(o)}発");
            }
            b.AtkBonus = 4; o.AtkBonus = 4;
            bool buffed = b.CurrentAttack == o.CurrentAttack && Hits(o) == 1;
            Expect("UT-o: 上限（下げ幅 12）までは下げ幅 1 につき +1（規定と同じ）・超えた分は +3・手数は規定と同じ（5 で止まる）", ok, string.Join("・", rows));
            Expect("UT-o: 強化されている間は規定と同じ（半減・1発）", buffed, $"{b.CurrentAttack} ／ {o.CurrentAttack}");
            bool order = PerverseOverTrait.Threshold == (PerverseTrait.MaxHits - 1) * PerverseTrait.HitsPerDull && PerverseOverTrait.Threshold == 12;
            Expect("UT-o: しきい値 ＝ (5 − 1) × 3 ＝ 12（第351期 WT-u の回し先の量と同じ決まり方）", order, $"{PerverseOverTrait.Threshold}");
        }

        // ---- UK-c ----
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.UkeUKc), Formation.Build(center: Plain("foe", hp: 9999)), out var p, out var e);
            var uke = p[0]; var foe = e[0];
            ctx.Dull(foe, 7);
            ctx.PerformAttack(uke);
            bool g1 = uke.AtkBonus == 3 && ctx.Plundered353(uke, foe) == 6 && ctx.DullReceived353(foe) == 7;
            ctx.PerformAttack(uke);
            bool g2 = uke.AtkBonus == 3 && Tly(ctx, "uke").Db353PlunderDry == 1 && Tly(ctx, "uke").Db353PlunderMiss == 0;
            ctx.Dull(foe, 3);
            ctx.PerformAttack(uke);
            bool g3 = uke.AtkBonus == 5 && ctx.Plundered353(uke, foe) == 10 && Tly(ctx, "uke").Db353PlunderGain == 5;
            Expect("UK-c: 累計（7）のうち奪っていない分の半分（3）だけ上がり、同じ呪いは二度奪わない（残り 1 では上がらず −1 もしない）・新しい呪い 3 で残り 4 → +2", g1 && g2 && g3, $"ウケ {uke.AtkBonus:+0;-0}・奪った累計 {ctx.Plundered353(uke, foe)}");
            var cz = Ctx(Formation.Build(front1: UnitCatalog.UkeUKc), Formation.Build(center: Plain("foe", hp: 9999)), out var pz, out _);
            cz.PerformAttack(pz[0]);
            Expect("UK-c: 累計 0 の敵を殴ると攻撃力 −1（弱体の窓口を通る）", pz[0].AtkBonus == -1 && Tly(cz, "uke").Db353PlunderMiss == 1, $"ウケ {pz[0].AtkBonus}");
            // ボスの伸び（T3 で +22）: 下げ幅（元 − 今）は 0 でも、受けた累計からは奪える
            var cb = Ctx(Formation.Build(front1: UnitCatalog.UkeUKc), Formation.Build(center: EnemyCatalog.BossRegular), out var pb, out var eb);
            var cr = Ctx(Formation.Build(front1: UnitCatalog.UkeUKr), Formation.Build(center: EnemyCatalog.BossRegular), out var pr, out var er);
            TurnP.SetValue(cb, 3); TurnP.SetValue(cr, 3);
            cb.Dull(eb[0], 4); cr.Dull(er[0], 4);
            bool rise = eb[0].CurrentAttack > eb[0].Def.Attack;
            cb.PerformAttack(pb[0]); cr.PerformAttack(pr[0]);
            Expect("UK-c: ボスの伸びで減らない（T3 の勇者に呪い 4 → 今の攻撃力は元より上でも +2・同じ盤面の UK-r は −1）", rise && pb[0].AtkBonus == 2 && pr[0].AtkBonus == -1 && Tly(cb, "uke").Db353PlunderBoss == 2,
                $"勇者 {eb[0].CurrentAttack}（元 {eb[0].Def.Attack}）・UK-c {pb[0].AtkBonus:+0;-0}・UK-r {pr[0].AtkBonus:+0;-0}");
            Expect("UK-c: 背負う札は無い（隣の味方の弱体を横取りしない）", !UnitCatalog.UkeUKc.Traits.Contains(TraitId.Bear), "");
            var cd = Ctx(Formation.Build(front1: UnitCatalog.Uke, front3: Plain("a")), Formation.Build(center: Plain("foe", hp: 999)), out _, out var ed);
            cd.Dull(ed[0], 5);
            Expect("UK-c の保持者がいない戦では累計を数えない（`DullReceived353` が 0）", cd.DullReceived353(ed[0]) == 0, $"{cd.DullReceived353(ed[0])}");
        }

        // ---- 台の上で ----
        {
            var vr = Presets.Versions.Where(r => r.Name.StartsWith("版・", StringComparison.Ordinal)).Select(r => r.Name).ToArray();
            bool vrows = vr.SequenceEqual(new[] { "版・UT-o 呪い", "版・UK-c 呪い", "版・UT-o＋UK-c 呪い" })
                && Presets.Versions.Select(r => r.Name).Take(4).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", PlayRow });
            var f = Presets.Versions.ToDictionary(r => r.Name, r => r.F);
            bool same = vrows && Seats(f["版・UT-o 呪い"]) == Seats(Play) && Seats(f["版・UK-c 呪い"]) == Seats(UkBoard) && Seats(f["版・UT-o＋UK-c 呪い"]) == Seats(UkBoard)
                && f["版・UT-o 呪い"].Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.UtsuUTo)) && f["版・UK-c 呪い"].Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.UkeUKc))
                && f["版・UK-c 呪い"].Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Utsu))
                && f["版・UT-o＋UK-c 呪い"].Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.UtsuUTo)) && f["版・UT-o＋UK-c 呪い"].Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.UkeUKc));
            Expect("`Presets.Versions` ＝ 試遊の4台 ＋ 「版・UT-o 呪い」（試遊・呪いのウツが版）／「版・UK-c 呪い」／「版・UT-o＋UK-c 呪い」（ウケ入りの台）", same, string.Join("、", Presets.Versions.Select(r => r.Name)));
            Expect($"ウケ入りの台 ＝ `ukseats` で選んだ並び（{UkPick}）", Seats(UkBoard) == UkPick, Seats(UkBoard));

            long over = 0, gain = 0, boss = 0, miss = 0, baseOver = 0, baseGain = 0;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat }) for (int s = 0; s < 4; s++)
            {
                var (ro, _, _) = I.Raw(Apply(Play, V("o")), w, s);
                var (rc, _, _) = I.Raw(Apply(UkBoard, V("c")), w, s);
                var (rb, _, _) = I.Raw(Apply(UkBoard, V("base")), w, s);
                over += Math.Max(0, ro.TallyByUnit["utsu"].Db351PerverseMax - 12);
                gain += rc.TallyByUnit["uke"].Db353PlunderGain; boss += rc.TallyByUnit["uke"].Db353PlunderBoss; miss += rc.TallyByUnit["uke"].Db353PlunderMiss;
                baseGain += rb.TallyByUnit["uke"].Db353PlunderGain;
                var (r0, _, _) = I.Raw(Play, w, s);
                baseOver += r0.TallyByUnit["utsu"].Db351PerverseMax;
            }
            Expect("台の上で版が働く（UT-o の台でウツが上限を超える ／ UK-c が奪う・ボスからも奪う・規定のウケは奪わない）。−1 は数えるだけ（ネルが開戦時に敵全員を呪うので、呪いの台では起きない）", over > 0 && gain > 0 && boss > 0 && baseGain == 0,
                $"上限超え {over}・奪った {gain}（ボス {boss}）・−1 {miss} 回・規定 {baseGain}");
            bool eq = true;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M2 }) for (int s = 0; s < 4; s++)
            {
                var (a, _, _) = I.Raw(Apply(Play, V("base")), w, s); var (z, _, _) = I.Raw(Play, w, s);
                var (au, _, _) = I.Raw(Apply(UkBoard, V("base")), w, s); var (zu, _, _) = I.Raw(UkBoard, w, s);
                eq &= a.PlayerWon == z.PlayerWon && a.Turns == z.Turns && a.PlayerSurvivors == z.PlayerSurvivors;
                eq &= au.PlayerWon == zu.PlayerWon && au.Turns == zu.Turns && au.PlayerSurvivors == zu.PlayerSurvivors;
            }
            Expect("「規定」の版は台そのもの（差し替えても勝敗 ／ T ／ 生存が同じ）", eq);

            int Kinds(BattleResult r, string t) => r.Events.Count(e => e.Kind == BattleEventKind.CursePass && e.Text == t);
            var (ec, _, _) = I.Raw(Apply(UkBoard, V("c")), I.Boss, 0, verbose: true);
            var (eo, _, _) = I.Raw(Apply(Play, V("o")), I.Boss, 0, verbose: true);
            var (eb0, _, _) = I.Raw(Play, I.Boss, 0, verbose: true);
            bool marks = Kinds(ec, CursePassLabels.Plunder) > 0 && Kinds(eo, CursePassLabels.Plunder) == 0 && Kinds(eb0, CursePassLabels.Plunder) == 0
                && eo.Events.Select(e => e.Kind).Distinct().All(k => eb0.Events.Any(e => e.Kind == k));
            Expect("台本: UK-c の戦に `CursePass` の奪うが出る・UT-o ／ 規定の戦には出ない・UT-o は新しい出来事の種類を出さない", marks, $"奪う {Kinds(ec, CursePassLabels.Plunder)}");
        }

        // ---- verbose ／ PickOne ----
        {
            int diff = 0, n = 0;
            foreach (var v in Vers) foreach (var b in Boards()) foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M5 }) for (int s = 0; s < 3; s++)
            {
                var (a, _, _) = I.Raw(Apply(b.F, v), w, s, verbose: true); var (z, _, _) = I.Raw(Apply(b.F, v), w, s, verbose: false);
                n++; if (a.PlayerWon != z.PlayerWon || a.Turns != z.Turns || a.PlayerSurvivors != z.PlayerSurvivors) diff++;
            }
            Expect("verbose の有無で勝敗 ／ T ／ 生存が同じ（版 4 × 台 3 × 波 5 × seed 0..2）", diff == 0, $"{n} 戦・差 {diff}");
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show f324acc5:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Db353*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第352期（f324acc5）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
