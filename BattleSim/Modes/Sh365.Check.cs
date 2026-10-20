using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// sh365 の自己検査（第365期・指示書 §6）。
static partial class Sh365Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static UnitDef Foe(string id) => new() { Id = id, Name = id, MaxHp = 999, Attack = 0, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single };

    /// <summary>撃ち手1枚（中央）× 敵 <paramref name="n"/> 体。アカは傷 20、ガレは礫 10 で1回撃たせ、最初の敵の減りを返す。</summary>
    static int ShotOn(UnitDef shooter, int n, out int unit)
    {
        var ctx = new BattleContext(0, false);
        var p = BattleEngine.Materialize(Formation.Build(center: shooter), BattleContext.PlayerTeam);
        var e = BattleEngine.MaterializeEnemy(EnemyWave.Of(Enumerable.Range(0, n).Select(k => (k, Foe($"f{k}"))).ToArray()), EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        var me = p[0]; var f0 = e.OrderBy(u => u.Slot).First();
        int before = f0.Hp;
        if (shooter.Id == "susu")
        {
            me.Hp = me.MaxHp - 20; unit = 20;
            var id = shooter.Traits.Contains(TraitId.BloodRead) ? TraitId.BloodRead : TraitId.Ash;
            if (id == TraitId.Ash) { me.SetCounter(StatusKeys.Ash, 20); me.SetCounter(AshTrait.CycleKey, 1); }   // 灰 20・投げる番
            TraitCatalog.Get(id).OnAction(ctx, me, new UnitAction(ActionKind.Skill));
        }
        else
        {
            me.SetCounter(PebbleTrait.Key, 10);
            unit = 10 * ctx.Shrapnel.Multiplier + me.CurrentAttack;
            TraitCatalog.Get(TraitId.PebbleGather).OnAction(ctx, me, new UnitAction(ActionKind.Skill));
        }
        return before - f0.Hp;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh365 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            var s = UnitCatalog.Susu; var g = UnitCatalog.Gare;
            bool def = s.Traits.SequenceEqual(UnitCatalog.SusuAKnA.Traits) && s.PlusText == UnitCatalog.SusuAKnA.PlusText && s.MinusText == UnitCatalog.SusuAKnA.MinusText
                && g.Traits.SequenceEqual(UnitCatalog.GareGRA.Traits) && g.PlusText == UnitCatalog.GareGRA.PlusText && g.MinusText == UnitCatalog.GareGRA.MinusText
                && UnitCatalog.SusuH364.Traits.SequenceEqual(new[] { TraitId.Ash }) && UnitCatalog.GareH364.Traits.SequenceEqual(new[] { TraitId.PebbleGather })
                && UnitCatalog.SusuAKn.Traits.SequenceEqual(new[] { TraitId.BloodRead }) && UnitCatalog.GareGRB.Traits.SequenceEqual(new[] { TraitId.PebbleGather, TraitId.ScatterShot })
                && UnitCatalog.All.Contains(s) && UnitCatalog.All.Contains(g) && !UnitCatalog.All.Contains(UnitCatalog.SusuH364) && !UnitCatalog.All.Contains(UnitCatalog.GareH364)
                && s.Id == "susu" && g.Id == "gare" && s.MaxHp == UnitCatalog.SusuH364.MaxHp && g.Attack == UnitCatalog.GareH364.Attack
                && ReferenceEquals(PinAG364(s), UnitCatalog.SusuH364) && ReferenceEquals(PinAG364(g), UnitCatalog.GareH364) && ReferenceEquals(PinKd362(s), UnitCatalog.SusuH364)
                && ReferenceEquals(PinSa360(g), UnitCatalog.GareH364) && ReferenceEquals(PinR358(g), UnitCatalog.GareH358) && ReferenceEquals(PinR358(UnitCatalog.GareH364), UnitCatalog.GareH358);
            Expect("段0: 規定のアカ ＝ AKn-A（札 ／ 文面が `SusuAKnA` と同じ）・規定のガレ ＝ GR-A（`GareGRA` と同じ）・旧は `SusuH364`（灰）／ `GareH364`（GR-r）・版は旧から・`All` は規定だけ・古い口の終端からアカ ／ ガレも旧へ（`PinR358` は旧のガレも `GareH358` へ）", def);
            int a1 = ShotOn(UnitCatalog.Susu, 1, out int au), a5 = ShotOn(UnitCatalog.Susu, 5, out _), o1 = ShotOn(UnitCatalog.SusuH364, 1, out _);
            int g1 = ShotOn(UnitCatalog.Gare, 1, out int gu), g5 = ShotOn(UnitCatalog.Gare, 5, out _), h1 = ShotOn(UnitCatalog.GareH364, 1, out _);
            Expect($"段0: 規定のアカ ／ ガレで集まる全体が働く（敵 1 体で 5 倍・5 体で今と同じ）／ 旧の定義では働かない（敵 1 体でも1倍）", a1 == au * 5 && a5 == au && o1 == 20 && g1 == gu * 5 && g5 == gu && h1 == gu,
                $"アカ 1体 {a1} ／ 5体 {a5} ／ 旧 {o1}・ガレ 1体 {g1} ／ 5体 {g5} ／ 旧 {h1}");
        }

        // ---- 計測のスイッチ ----
        {
            int diff = 0, n = 0, vdiff = 0;
            foreach (var f in new[] { BoardB(), BoardIdle(), Presets.Versions.First(r => r.Name == "試遊・自傷").F })
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat })
                    for (int seed = 0; seed < 6; seed++)
                    {
                        var (r0, _, _) = I.Raw(f, w, seed);
                        var (r1, _, _) = Raw(f, w, seed, 0, true, verbose: false);
                        var (r2, _, _) = Raw(f, w, seed, 0, true, verbose: true);
                        n++;
                        if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns || r0.PlayerSurvivors != r1.PlayerSurvivors) diff++;
                        if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) vdiff++;
                    }
            Expect("切らない C0 ＋ 数え上げを立てた戦は、規定（スイッチなし）と勝敗 ／ T ／ 生存数が同じ（台 B ／ 何もしない素体 ／ 試遊・自傷 × 勇者 ／ 近衛 ／ 大隊・seed 0..5）", diff == 0, $"{n} 戦・差 {diff}");
            Expect("verbose の有無で勝敗 ／ T が同じ（同じ組）", vdiff == 0, $"差 {vdiff}");
            Expect("スイッチは戻る（器具の外の戦は切らない・数えない）", BattleContext.ChainCut365 == 0 && !BattleContext.ChainCount365);
            // 切りが効いていることの確かめ（台 B × 勇者・seed 0..9）。
            int W = BattleContext.Ch365.Width;
            long F(Agg a, bool hisa, int field) { long s = 0; for (int t = 0; t < W; t++) s += (hisa ? a.Hisa : a.Misa)[field * W + t]; return s; }
            var c0 = Measure(BoardB(), I.Boss, 0, 10);
            var c1 = Measure(BoardB(), I.Boss, 1, 10); var c2 = Measure(BoardB(), I.Boss, 2, 10); var c3 = Measure(BoardB(), I.Boss, 3, 10);
            var c4 = Measure(BoardB(), I.Boss, 4, 10); var c5 = Measure(BoardB(), I.Boss, 5, 10); var c6 = Measure(BoardB(), I.Boss, 6, 10);
            bool c4ok = Enumerable.Range(0, W).All(t => (c4.Misa[BattleContext.Ch365.GainAccuse * W + t] + c4.Misa[BattleContext.Ch365.GainBurst * W + t] + c4.Misa[BattleContext.Ch365.GainOther * W + t]) <= BattleContext.ChainCap365 * c4.N);
            bool ok = F(c0, true, BattleContext.Ch365.RallyFeather) > 0 && F(c1, true, BattleContext.Ch365.RallyFeather) == 0 && F(c1, true, BattleContext.Ch365.RallyCut) > 0
                && F(c0, false, BattleContext.Ch365.GainBurst) > 0 && F(c2, false, BattleContext.Ch365.GainBurst) == 0
                && F(c0, false, BattleContext.Ch365.GainAccuse) > 0 && F(c3, false, BattleContext.Ch365.GainAccuse) == 0
                && c4ok && F(c4, false, BattleContext.Ch365.GainCut) > 0
                && F(c0, false, BattleContext.Ch365.MfQueuedLayer) > 0 && F(c5, false, BattleContext.Ch365.MfQueuedLayer) == 0 && F(c5, false, BattleContext.Ch365.MfCut) > 0
                && F(c0, true, BattleContext.Ch365.Balls) > 0 && F(c6, true, BattleContext.Ch365.Balls) == 0;
            Expect("切りが効く（C1 羽のまとまりの叫び 0 ／ C2 号令の羽 0 ／ C3 『あいつがやった！』の羽 0 ／ C4 1ターンの羽 ≦ 3 ／ C5 層の追加の標撃ち 0 ／ C6 玉 0・台 B × 勇者・seed 0..9）", ok,
                $"C0 羽の叫び {F(c0, true, BattleContext.Ch365.RallyFeather)}・号令の羽 {F(c0, false, BattleContext.Ch365.GainBurst)}・指差しの羽 {F(c0, false, BattleContext.Ch365.GainAccuse)}・層の標撃ち {F(c0, false, BattleContext.Ch365.MfQueuedLayer)}・玉 {F(c0, true, BattleContext.Ch365.Balls)}");
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷" });
            Expect("`Presets.Versions` ＝ 試遊の7台（「試遊・自傷」を足した・第364期の「版・」の4台は外した）", rows, string.Join("、", vr));
            if (rows)
            {
                var f = Presets.Versions.First(r => r.Name == "試遊・自傷").F; var sh = Presets.Versions.First(r => r.Name == "試遊・破片").F;
                bool ok = S307.Seats(f) == "カド・リリ・ムド・ボルグ・アカ" && f.Occupied().All(o => UnitCatalog.All.Contains(o.Def)) && sh.Occupied().Any(o => o.Def == UnitCatalog.Gare);
                Expect("「試遊・自傷」＝ 前1 カド ／ 前3 リリ ／ 中央 ムド ／ 後1 ボルグ ／ 後3 アカ（規定の駒）・「試遊・破片」のガレは規定（GR-A）", ok, S307.Seats(f));
            }
        }

        // ---- PickOne ----
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 29fa9782:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs"), r1 = File.ReadAllText("BattleCore/Traits.cs").Split("Roll(").Length - 1, re1 = File.ReadAllText("BattleCore/BattleEngine.cs").Split("Roll(").Length - 1;
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh365*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第364期（29fa9782）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0 && mine == 0,
                $"engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
