using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// db351 の自己検査（第351期・指示書 §9）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿と台本で確かめる（盤面は動かさない）。
static partial class Db351Diag
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

    /// <summary>直に組む盤面: 前1 ウツ ／ 前3 遠い味方 ／ 中央 中の味方 ／ 後1 ワタ（版）／ 後3 後の味方。敵は1体（攻 10・強化 <paramref name="foeBuff"/>）。</summary>
    static BattleContext Board(UnitDef wata, int foeBuff, out List<UnitState> p, out UnitState foe, bool utsu = true)
    {
        var ctx = Ctx(Formation.Build(front1: utsu ? UnitCatalog.Utsu : Plain("x1"), front3: Plain("far"), center: Plain("mid"), back1: wata, back3: Plain("b3")),
            Formation.Build(center: Plain("foe", hp: 999)), out p, out var e);
        foe = e[0];
        foe.AtkBonus = foeBuff;   // 検査の下ごしらえ（強化の窓口の外で置く・盤面の規則ではない）
        return ctx;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} db351 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義 ----
        {
            bool a = UnitCatalog.WataWTa.Traits.SequenceEqual(new[] { TraitId.Relay, TraitId.RelayWide });
            bool x = UnitCatalog.WataWTx.Traits.SequenceEqual(new[] { TraitId.Relay, TraitId.RelayWide, TraitId.RelaySwap });
            bool u = UnitCatalog.WataWTu.Traits.SequenceEqual(new[] { TraitId.Relay, TraitId.RelayWide, TraitId.RelayFeed });
            bool r = UnitCatalog.UkeUKr.Traits.SequenceEqual(new[] { TraitId.Plunder }) && UnitCatalog.UkeUKr.MaxHp == UnitCatalog.Uke.MaxHp && UnitCatalog.UkeUKr.Attack == UnitCatalog.Uke.Attack
                && UnitCatalog.UkeUKr.Speed == UnitCatalog.Uke.Speed && UnitCatalog.UkeUKr.Id == "uke" && UnitCatalog.UkeUKr.Actions == UnitCatalog.Uke.Actions;
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.RelayWide or TraitId.RelaySwap or TraitId.RelayFeed or TraitId.Plunder)).Select(d => d.Id).ToArray();
            // 第352期: 規定のワタ ＝ WT-u（`RelayWide` ＋ `RelayFeed`）・旧（`WataH351`）は渡しだけ・`RelaySwap` ／ `Plunder` の保持者は 0 枚
            bool def = UnitCatalog.Wata.Traits.SequenceEqual(UnitCatalog.WataWTu.Traits) && UnitCatalog.WataH351.Traits.SequenceEqual(new[] { TraitId.Relay })
                && UnitCatalog.Uke.Traits.SequenceEqual(new[] { TraitId.Bear })
                && UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.RelayWide or TraitId.RelayFeed)).Select(d => d.Id).SequenceEqual(new[] { "wata" });
            held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.RelaySwap or TraitId.Plunder)).Select(d => d.Id).ToArray();
            Expect("版の定義: WT-a ＝ 渡し ＋ `RelayWide` ／ WT-x ＝ ＋ `RelaySwap` ／ WT-u ＝ ＋ `RelayFeed` ／ UK-r ＝ 体はウケのまま・札は `Plunder` だけ（背負う札なし）", a && x && u && r, $"a {a}・x {x}・u {u}・r {r}");
            Expect("第352期: 規定のワタ ＝ WT-u（`RelayWide` ＋ `RelayFeed`・保持者はワタだけ）・旧（`WataH351`）は渡しだけ・ウケは触っていない・`RelaySwap` ／ `Plunder` の保持者は 0 枚", def && held.Length == 0, held.Length == 0 ? "" : string.Join(",", held));
        }

        // ---- WT-a ----
        {
            bool farOk = !FormationRules.AreAdjacent(1, 3), utsuAdj = FormationRules.AreAdjacent(0, 3) || FormationRules.AreAdjacent(0, 2);
            var ctx = Board(UnitCatalog.WataWTa, 0, out var p, out var foe);
            var wata = U(p, "wata"); var far = U(p, "far"); var utsu = U(p, "utsu");
            int hp0 = wata.Hp;
            ctx.Dull(far, 4);
            bool pick = far.AtkBonus == 0 && foe.AtkBonus == -4 && wata.Hp == hp0 - 4 * RelayTrait.HpCostPerDull && Tly(ctx, "wata").Db351PickedFar == 4;
            ctx.Dull(utsu, 4);
            bool skipUtsu = utsu.AtkBonus == -4 && foe.AtkBonus == -4;
            ctx.Dull(wata, 3);
            bool skipSelf = wata.AtkBonus == -3 && foe.AtkBonus == -4;
            Expect("WT-a: 隣でない味方（前3・後1 のワタと隣でない）の弱体も拾って敵へ渡し、渡した分 ×2 だけワタが削れる", farOk && pick, $"遠い味方 {far.AtkBonus}・敵 {foe.AtkBonus}・ワタ HP {hp0} → {wata.Hp}");
            Expect("WT-a: ウツ ／ ワタ自身の弱体は拾わない", skipUtsu && skipSelf, $"ウツ {utsu.AtkBonus}・ワタ {wata.AtkBonus}");
            var c0 = Board(UnitCatalog.WataH351, 0, out var p0, out var f0);
            c0.Dull(U(p0, "far"), 4);
            Expect("旧の規定のワタ（`WataH351`）は隣でない味方の弱体を拾わない（対照）", U(p0, "far").AtkBonus == -4 && f0.AtkBonus == 0, $"遠い味方 {U(p0, "far").AtkBonus}・敵 {f0.AtkBonus}");
        }

        // ---- WT-x ----
        {
            var ctx = Board(UnitCatalog.WataWTx, 6, out var p, out var foe);
            var far = U(p, "far");
            ctx.Dull(far, 4);
            bool took = far.AtkBonus == 4 && foe.AtkBonus == 6 - 4 - 4 && Tly(ctx, "wata").Db351Swapped == 4;
            Expect("WT-x: 渡した量（4）まで敵の強化（+6）を取り、拾った元の味方に攻撃力として付く", took, $"元の味方 {far.AtkBonus:+0;-0}・敵 {foe.AtkBonus:+0;-0}");
            var c2 = Board(UnitCatalog.WataWTx, 2, out var p2, out var f2);
            c2.Dull(U(p2, "far"), 4);
            Expect("WT-x: 強化が渡した量より少なければ強化の分だけ（+2 → 2）", U(p2, "far").AtkBonus == 2 && f2.AtkBonus == 2 - 4 - 2, $"元の味方 {U(p2, "far").AtkBonus:+0;-0}・敵 {f2.AtkBonus:+0;-0}");
            var c3 = Board(UnitCatalog.WataWTx, 0, out var p3, out var f3);
            c3.Dull(U(p3, "far"), 4);
            Expect("WT-x: 敵に強化が無ければ渡すだけ", U(p3, "far").AtkBonus == 0 && f3.AtkBonus == -4 && Tly(c3, "wata").Db351Swapped == 0, $"元の味方 {U(p3, "far").AtkBonus}・敵 {f3.AtkBonus}");
            // ボスの毎ターンの伸び（`BossRise11`・T3 で +22）は「今 − 元」に入る ＝ 強化として取れる
            var c4 = Ctx(Formation.Build(front3: Plain("far"), back1: UnitCatalog.WataWTx), Formation.Build(center: EnemyCatalog.BossRegular), out var p4, out var e4);
            TurnP.SetValue(c4, 3);
            var boss = e4[0];
            int rise = boss.CurrentAttack - boss.Def.Attack;
            c4.Dull(U(p4, "far"), 4);
            Expect("WT-x: 強化 ＝ 今の攻撃力 − 元（ボスの毎ターンの伸びも入る・T3 の勇者 +22 から 4 を取る）", rise == 22 && U(p4, "far").AtkBonus == 4 && boss.AtkBonus == -8, $"伸び {rise}・元の味方 {U(p4, "far").AtkBonus:+0;-0}・勇者 {boss.AtkBonus:+0;-0}");
        }

        // ---- WT-u ----
        {
            var ctx = Board(UnitCatalog.WataWTu, 0, out var p, out var foe);
            var utsu = U(p, "utsu"); var far = U(p, "far");
            ctx.Dull(far, 4);
            bool first = utsu.AtkBonus == -4 && foe.AtkBonus == 0 && far.AtkBonus == 0;
            utsu.AtkBonus = -10;
            ctx.Dull(far, 4);
            bool rest = utsu.AtkBonus == -12 && foe.AtkBonus == -2;
            ctx.Dull(far, 4);
            bool full = utsu.AtkBonus == -12 && foe.AtkBonus == -6 && utsu.Traits.Any(t => t.ModifyHitCount(utsu, 1) == PerverseTrait.MaxHits);
            Expect("WT-u: 拾った弱体はまずウツへ（下げ幅 12 ＝ 手数が上限 5 発に届くまで）", first, $"ウツ {utsu.AtkBonus}・敵 {foe.AtkBonus}");
            Expect("WT-u: 届く分を越えた余りは敵へ（ウツ −10 に 4 → ウツへ 2・敵へ 2）・届いた後はすべて敵へ", rest && full, $"ウツ {utsu.AtkBonus}・敵 {foe.AtkBonus}");
            var c2 = Board(UnitCatalog.WataWTu, 0, out var p2, out var f2, utsu: false);
            var ca = Board(UnitCatalog.WataWTa, 0, out var pa, out var fa, utsu: false);
            c2.Dull(U(p2, "far"), 4); ca.Dull(U(pa, "far"), 4);
            var c3 = Board(UnitCatalog.WataWTu, 0, out var p3, out var f3);
            U(p3, "utsu").Hp = 0;
            c3.Dull(U(p3, "far"), 4);
            Expect("WT-u: ウツがいなければ（倒れていれば）WT-a と同じ", f2.AtkBonus == fa.AtkBonus && f2.AtkBonus == -4 && f3.AtkBonus == -4 && U(p2, "wata").Hp == U(pa, "wata").Hp, $"敵 {f2.AtkBonus} ／ WT-a {fa.AtkBonus} ／ ウツ倒れ {f3.AtkBonus}");
        }

        // ---- UK-r ----
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.UkeUKr), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
            var uke = p[0]; var foe = e[0];
            foe.AtkBonus = -7;   // 下げ幅 7（元 10 → 今 3）
            ctx.PerformAttack(uke);
            bool gain = uke.AtkBonus == 3 && Tly(ctx, "uke").Db351PlunderGain == 3;
            foe.AtkBonus = 0;
            ctx.PerformAttack(uke);
            bool miss = uke.AtkBonus == 2 && Tly(ctx, "uke").Db351PlunderMiss == 1;
            foe.AtkBonus = -1;   // 下げ幅 1 → 半分は 0（上がらないし、鈍りもしない）
            ctx.PerformAttack(uke);
            bool one = uke.AtkBonus == 2;
            Expect("UK-r: 下げ幅（7）の半分（切り捨て 3）だけ攻撃力が上がる（累積）", gain, $"ウケ {uke.AtkBonus:+0;-0}");
            Expect("UK-r: 下げ幅 0 の敵を殴ると攻撃力 −1・下げ幅 1 は上がりも下がりもしない", miss && one, $"ウケ {uke.AtkBonus:+0;-0}");
            Expect("UK-r: 背負う札は無い（隣の味方の弱体を横取りしない）", !UnitCatalog.UkeUKr.Traits.Contains(TraitId.Bear), "");
        }

        // ---- 台（呪いの台）の上で ----
        {
            var top = Top1;
            bool row = Seats(top) == "ワタ・ムド・クビ・ネル・ウツ" && top.Occupied().All(o => ReferenceEquals(o.Def, UnitCatalog.Wata) || ReferenceEquals(o.Def, UnitCatalog.Mudo)
                || ReferenceEquals(o.Def, UnitCatalog.Kubi) || ReferenceEquals(o.Def, UnitCatalog.Nel) || ReferenceEquals(o.Def, UnitCatalog.Utsu))
                && !CompareBuilds().Any(c => c.Name == PlayRow) && !Presets.Playtest.Any(c => c.Name == PlayRow);
            Expect("「試遊・呪い」＝ Phase 0 の1位（ワタ・ムド・クビ・ネル・ウツ）・規定の駒・`Presets.Versions` だけに置く", row, Seats(top));
            // 第352期: 規定化で第351期の「版・」の4台を外した（試遊の4台だけ）。第353期: 「版・」の台は期ごとに入れ替わるので、頭の4台（試遊）と、第351期の版の台が無いことだけを見る
            var vr = Presets.Versions.Where(r => r.Name.StartsWith("版・", StringComparison.Ordinal)).ToArray();
            bool vrows = !vr.Any(r => r.Name is "版・WT-a 呪い" or "版・WT-x 呪い" or "版・WT-u 呪い" or "版・UK-r 呪い")
                && Presets.Versions.Select(r => r.Name).Take(4).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", PlayRow })
                && Presets.Versions.Skip(4).All(r => r.Name.StartsWith("版・", StringComparison.Ordinal));
            Expect("`Presets.Versions` の頭 ＝ 試遊の4台（感電 ／ ミサ ／ 燃焼 ／ 呪い）・第351期の「版・」の4台は無い（第352期に外した）", vrows, string.Join("、", Presets.Versions.Select(r => r.Name)));

            long far = 0, sw = 0, fed = 0, pl = 0, plMiss = 0, base0 = 0, onPerv = 0;
            foreach (var w in new[] { I.Boss, I.Bat }) for (int s = 0; s < 4; s++)
            {
                var (ra, _, _) = I.Raw(Apply(Top2, V("a")), w, s);
                var (rx, _, _) = I.Raw(Apply(Top1, V("x")), w, s);
                var (ru, _, _) = I.Raw(Apply(Top1, V("u")), w, s);
                var (rr, _, _) = I.Raw(Apply(UkBoard, V("r")), w, s);
                var (rb, _, _) = I.Raw(Apply(Top2, V("base")), w, s);
                far += ra.TallyByUnit["wata"].Db351PickedFar; sw += rx.TallyByUnit["wata"].Db351Swapped; onPerv += rx.TallyByUnit["wata"].Db351SwapOnPerverse; fed += ru.TallyByUnit["wata"].Db351Fed;
                pl += rr.TallyByUnit["uke"].Db351PlunderGain; plMiss += rr.TallyByUnit["uke"].Db351PlunderMiss; base0 += rb.TallyByUnit["wata"].Db351PickedFar;
            }
            Expect("台の上で版が働く（WT-a 隣でない拾い ／ WT-x 交換 ／ WT-u ウツへ ／ UK-r 奪う・弱っていない敵 がどれも > 0・規定のワタの隣でない拾いは 0）",
                far > 0 && sw > 0 && fed > 0 && pl > 0 && plMiss > 0 && base0 == 0, $"隣でない {far}・交換 {sw}（ウツに付いた {onPerv} 回）・ウツへ {fed}・奪う {pl} ／ 鈍った {plMiss} 回・規定 {base0}");

            // 台本の印（表示専用）: 版の戦にだけ出て、規定の戦には出ない
            int Kinds(BattleResult r, string t) => r.Events.Count(e => e.Kind == BattleEventKind.CursePass && e.Text == t);
            var (ex, _, _) = I.Raw(Apply(Top1, V("x")), I.Boss, 0, verbose: true);
            var (eu, _, _) = I.Raw(Apply(Top1, V("u")), I.Boss, 0, verbose: true);
            var (er, _, _) = I.Raw(Apply(UkBoard, V("r")), I.Boss, 0, verbose: true);
            var (eb, _, _) = I.Raw(Apply(Top1, V("base")), I.Boss, 0, verbose: true);
            bool marks = Kinds(ex, CursePassLabels.Push) > 0 && Kinds(ex, CursePassLabels.Swap) > 0 && Kinds(eu, CursePassLabels.Feed) > 0 && Kinds(er, CursePassLabels.Plunder) > 0
                && eb.Events.All(e => e.Kind != BattleEventKind.CursePass);
            Expect("台本: `CursePass`（押し付け ／ 交換 ／ 回す ／ 奪う）は版の戦にだけ出る（規定の呪いの台には出ない）", marks,
                $"押し付け {Kinds(ex, CursePassLabels.Push)}・交換 {Kinds(ex, CursePassLabels.Swap)}・回す {Kinds(eu, CursePassLabels.Feed)}・奪う {Kinds(er, CursePassLabels.Plunder)}");
        }

        // ---- 第352期: 規定化 ----
        {
            long nowFar = 0, nowFed = 0, oldFar = 0, oldFed = 0;
            foreach (var w in new[] { I.Boss, I.Bat }) for (int s = 0; s < 4; s++)
            {
                var (rn, _, _) = I.Raw(Top1, w, s); var (ro, _, _) = I.Raw(Pin351(Top1), w, s);
                nowFar += rn.TallyByUnit["wata"].Db351PickedFar; nowFed += rn.TallyByUnit["wata"].Db351Fed;
                oldFar += ro.TallyByUnit["wata"].Db351PickedFar; oldFed += ro.TallyByUnit["wata"].Db351Fed;
            }
            Expect("第352期: 規定のワタ（「試遊・呪い」そのもの）では WT-u が働き（隣でない拾い ／ ウツへ回す > 0）、旧の定義（`Pin351`）では働かない（0）",
                nowFar > 0 && nowFed > 0 && oldFar == 0 && oldFed == 0, $"規定 {nowFar} ／ {nowFed}・旧 {oldFar} ／ {oldFed}");
            var (en, _, _) = I.Raw(Top1, I.Boss, 0, verbose: true);
            int Kn(string t) => en.Events.Count(e => e.Kind == BattleEventKind.CursePass && e.Text == t);
            Expect("第352期: 規定の台本に `CursePass` の押し付け ／ 回すが出る（交換 ／ 奪うは出ない）", Kn(CursePassLabels.Push) > 0 && Kn(CursePassLabels.Feed) > 0 && Kn(CursePassLabels.Swap) == 0 && Kn(CursePassLabels.Plunder) == 0,
                $"押し付け {Kn(CursePassLabels.Push)}・回す {Kn(CursePassLabels.Feed)}");
            bool same = true;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat }) for (int s = 0; s < 4; s++)
            {
                var (rn, _, _) = I.Raw(Top1, w, s); var (ru, _, _) = I.Raw(Apply(Top1, V("u")), w, s);
                same &= rn.PlayerWon == ru.PlayerWon && rn.Turns == ru.Turns && rn.PlayerSurvivors == ru.PlayerSurvivors;
            }
            Expect("第352期: 規定のワタの台は第351期の WT-u の台と勝敗 ／ T ／ 生存が同じ（ボス ／ 近衛 ／ 大隊 × seed 0..3）", same);
        }

        // ---- verbose ／ PickOne ----
        {
            int diff = 0, n = 0;
            foreach (var v in Vers) foreach (var b in Boards().Take(3)) foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M5 }) for (int s = 0; s < 3; s++)
            {
                var (a, _, _) = I.Raw(Apply(b.F, v), w, s, verbose: true); var (z, _, _) = I.Raw(Apply(b.F, v), w, s, verbose: false);
                n++; if (a.PlayerWon != z.PlayerWon || a.Turns != z.Turns || a.PlayerSurvivors != z.PlayerSurvivors) diff++;
            }
            Expect("verbose の有無で勝敗 ／ T ／ 生存が同じ（版 5 × 呪いの台 3 × 波 5 × seed 0..2）", diff == 0, $"{n} 戦・差 {diff}");
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 69fa9321:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Db351*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第350期の後（69fa9321）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
