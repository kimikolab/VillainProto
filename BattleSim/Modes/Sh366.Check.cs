using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using X = Sh365Diag;

// sh366 の自己検査（第366期・指示書 §6）。
static partial class Sh366Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, new[] { typeof(UnitState) })!;
    static UnitTally TallyOf(BattleContext ctx, UnitState u) => (UnitTally)TallyOfM.Invoke(ctx, new object[] { u })!;
    static readonly MethodInfo PoolAdd = typeof(BattleContext).GetMethod("CommandPoolAdd", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    /// <summary>ヒサ1枚（中央）の盤を作り、溜まりに <paramref name="pool"/> を入れてから <paramref name="over"/> の溢れを足し、ターンの終わりを1回通した後の溜まりを返す。</summary>
    static (int AfterAdd, int AfterEnd, long Dropped) PoolOn(UnitDef hisa, int pool, int over)
    {
        var ctx = new BattleContext(0, false);
        var p = BattleEngine.Materialize(Formation.Build(center: hisa), BattleContext.PlayerTeam);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        var h = p[0];
        h.SetCounter(CommandTrait.PoolKey, pool);
        if (over > 0) PoolAdd.Invoke(ctx, new object[] { h, over, TallyOf(ctx, h) });
        int afterAdd = h.RawCounter(CommandTrait.PoolKey);
        ctx.CommandPoolTurnEnd();
        return (afterAdd, h.RawCounter(CommandTrait.PoolKey), TallyOf(ctx, h).CommandDropped);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh366 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var hisa = UnitCatalog.Hisa;

        // ---- 版の定義 ----
        {
            bool def = hisa.Traits.SequenceEqual(UnitCatalog.HisaH337.Traits.Append(TraitId.MarkInheritSpread)) && UnitCatalog.All.Contains(hisa)
                && new[] { (UnitCatalog.HisaPt, TraitId.CommandPoolTurnDrop), (UnitCatalog.HisaP60, TraitId.CommandPoolCap60), (UnitCatalog.HisaP180, TraitId.CommandPoolCap180) }
                    .All(x => x.Item1.Traits.SequenceEqual(hisa.Traits.Append(x.Item2)) && x.Item1.Id == hisa.Id && x.Item1.MaxHp == hisa.MaxHp && x.Item1.Attack == hisa.Attack
                        && !UnitCatalog.All.Contains(x.Item1) && x.Item1.PlusText.Contains(UnitCatalog.HisaBallClause322 + "（", StringComparison.Ordinal) && x.Item1.MinusText == hisa.MinusText)
                && !hisa.Traits.Any(t => t is TraitId.CommandPoolTurnDrop or TraitId.CommandPoolCap60 or TraitId.CommandPoolCap180)
                && !UnitCatalog.All.Any(d => d.Traits.Any(t => t is TraitId.CommandPoolTurnDrop or TraitId.CommandPoolCap60 or TraitId.CommandPoolCap180));
            Expect("版の定義: 規定のヒサ ＝ 今どおり（`HisaH337` ＋ `MarkInheritSpread`・新しい札を持たない）／ P-t ／ P-60 ／ P-180 は規定 ＋ 札1枚（体 ／ Id は同じ・文面は号令の句の後ろに括弧書き）・`All` に入れない・新しい札の保持者は 0 枚", def);
        }

        // ---- 溜まりの規則（直に） ----
        {
            var d0 = PoolOn(hisa, 50, 0); var dt = PoolOn(UnitCatalog.HisaPt, 50, 0);
            var d0a = PoolOn(hisa, 50, 200); var dta = PoolOn(UnitCatalog.HisaPt, 50, 200);
            Expect("P-t: ターンの終わりに溜まりが 0 になる（捨てた量を数える）／ 規定はそのまま持ち越す", dt.AfterEnd == 0 && dt.Dropped == 50 && d0.AfterEnd == 50 && d0.Dropped == 0 && dta.AfterAdd == 250 && dta.AfterEnd == 0 && d0a.AfterEnd == 250,
                $"規定 50 → {d0.AfterEnd}・P-t 50 → {dt.AfterEnd}（捨てた {dt.Dropped}）・溢れ 200 を足すと 規定 {d0a.AfterAdd} ／ P-t {dta.AfterAdd}（ターンのうちは上限なし）");
            var c60 = PoolOn(UnitCatalog.HisaP60, 50, 30); var c180 = PoolOn(UnitCatalog.HisaP180, 170, 30); var c180b = PoolOn(UnitCatalog.HisaP180, 50, 30);
            Expect("P-60 ／ P-180: 溜まりは上限を超えない・超えた溢れは捨てる（数える）・上限の下では今どおり溜まり、ターンの終わりにも捨てない",
                c60.AfterAdd == 60 && c60.Dropped == 20 && c60.AfterEnd == 60 && c180.AfterAdd == 180 && c180.Dropped == 20 && c180b.AfterAdd == 80 && c180b.Dropped == 0 && c180b.AfterEnd == 80
                && CommandTrait.PoolCapOf(BattleEngine.Materialize(Formation.Build(center: hisa), BattleContext.PlayerTeam)[0]) == 0,
                $"P-60 50＋30 → {c60.AfterAdd}（捨てた {c60.Dropped}）・P-180 170＋30 → {c180.AfterAdd}（捨てた {c180.Dropped}）・50＋30 → {c180b.AfterAdd}・規定の上限 0");
        }

        // ---- 戦の中で ----
        {
            var fB = Presets.HisaBoard366();
            int sameT1 = 0, n = 0; long carryPt = 0, dropPt = 0, maxPool60 = 0, maxPool180 = 0, maxPool0 = 0, vdiff = 0, cdiff = 0;
            for (int seed = 0; seed < 10; seed++)
            {
                n++;
                var a0 = One(fB, I.Boss, seed, true); var at = One(Apply(fB, Vers[1]), I.Boss, seed, true);
                bool same = true;
                foreach (int fld in new[] { BattleContext.Ch365.BurstLayers, BattleContext.Ch365.Balls, BattleContext.Ch365.Rally, BattleContext.Ch365.Overflow })
                    for (int t = 0; t <= 1; t++) if (a0.F(true, fld, t) != at.F(true, fld, t)) same = false;
                if (same) sameT1++;
                for (int t = 0; t < BattleContext.Ch365.Width; t++) { carryPt += at.F(true, BattleContext.Ch365.PoolCarry, t); dropPt += at.F(true, BattleContext.Ch365.PoolDropped, t); }
                foreach (var (v, set) in new[] { (Vers[0], 0), (Vers[2], 60), (Vers[3], 180) })
                {
                    var (r, _, _) = I.Raw(Apply(fB, v), I.Boss, seed, verbose: true);
                    long mx = r.Events.Where(x => x.Kind == BattleEventKind.CommandBall).Select(x => (long)x.Slot).DefaultIfEmpty(0).Max();
                    if (set == 0) maxPool0 = Math.Max(maxPool0, mx); else if (set == 60) maxPool60 = Math.Max(maxPool60, mx); else maxPool180 = Math.Max(maxPool180, mx);
                }
                // verbose の有無 ／ 計数のスイッチの有無で勝敗が同じ（版 3 つ × 台 B ／ 標の守り型 × 勇者 ／ 近衛）。
                foreach (var v in Vers.Skip(1))
                    foreach (var f in new[] { fB, Play("試遊・標 守り型") })
                        foreach (var w in new[] { I.Boss, I.Guard })
                        {
                            var (r0, _, _) = I.Raw(Apply(f, v), w, seed);
                            var (r1, _, _) = I.Raw(Apply(f, v), w, seed, verbose: true);
                            var (r2, _, _) = X.Raw(Apply(f, v), w, seed, 0, true, verbose: false);
                            if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns || r0.PlayerSurvivors != r1.PlayerSurvivors) vdiff++;
                            if (r0.PlayerWon != r2.PlayerWon || r0.Turns != r2.Turns || r0.PlayerSurvivors != r2.PlayerSurvivors) cdiff++;
                        }
            }
            Expect("P-t: 戦の中でも T1 の終わりまでは規定と同じ（号令の層 ／ 玉 ／ 叫び ／ 溢れ・台 B × 勇者・seed 0..9）・持ち越しは 0・捨てた溢れがある", sameT1 == n && carryPt == 0 && dropPt > 0, $"{sameT1} ／ {n} 戦が同じ・持ち越し {carryPt}・捨てた {dropPt}");
            Expect("P-60 ／ P-180: 戦の中で溜まりが上限を超えない（玉の出来事の溜まりの最大・台 B × 勇者・seed 0..9）／ 規定は上限を超えて溜まる", maxPool60 <= 60 && maxPool180 <= 180 && maxPool60 > 0 && maxPool0 > 180,
                $"規定 {maxPool0}・P-60 {maxPool60}・P-180 {maxPool180}");
            Expect("verbose の有無 ／ 計数のスイッチの有無で勝敗 ／ T ／ 生存数が同じ（版 3 つ × 台 B ／ 標の守り型 × 勇者 ／ 近衛・seed 0..9）", vdiff == 0 && cdiff == 0, $"verbose の差 {vdiff}・スイッチの差 {cdiff}");
            Expect("スイッチは戻る（器具の外の戦は数えない）", BattleContext.ChainCut365 == 0 && !BattleContext.ChainCount365);
        }

        // ---- 素体の替えは器具の中だけ ----
        {
            var boards = PoisonBoards();
            var before = boards.Select(b => S307.Seats(b.F)).ToArray();
            int swapped = 0;
            foreach (var b in boards) foreach (var id in PoisonIds.Where(i => Has(b.F, i))) { var g = ToPlain(b.F, id); if (!Has(g, id) && Has(g, id + "_plain")) swapped++; }
            bool ok = swapped > 0 && boards.Select(b => S307.Seats(b.F)).SequenceEqual(before) && PoisonBoards().All(b => !b.F.Occupied().Any(o => o.Def.Id.EndsWith("_plain")))
                && PoisonIds.All(i => UnitCatalog.All.Any(d => d.Id == i && d.Traits.Count > 0)) && !UnitCatalog.All.Any(d => d.Id.EndsWith("_plain"));
            Expect("§3-3 の素体の替えは器具の中だけ（替えた台は写し・`compare` の行 ／ 規定の駒は変わらない）", ok, $"毒の行 {boards.Length}・替えた {swapped} 通り");
            bool noTax = !HeroNoTax.Traits.Contains(TraitId.VenomTaxSeal) && EnemyCatalog.BossRegular.Traits.Contains(TraitId.VenomTaxSeal) && HeroNoTax.Traits.Count == EnemyCatalog.BossRegular.Traits.Count - 1;
            Expect("§3-2 の対照（蝕みなしの勇者）は器具の中だけ（規定の勇者は蝕みを持つ）", noTax);
        }

        // ---- 試遊の台 ----
        {
            // 第367期: 第366期の「版・」の3台は外した（並びは `Presets.HisaBoard366` に固定・版の台はここで組む）。検査は「頭の7台」と「第366期の版の台が無い」に。
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.Take(7).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷" })
                && !vr.Any(n => n is "版・P-t ヒサ" or "版・P-60 ヒサ" or "版・P-180 ヒサ");
            Expect("`Presets.Versions` の頭 ＝ 試遊の7台・第366期の「版・P-t ／ P-60 ／ P-180 ヒサ」は無い（第367期に外した）", rows, string.Join("、", vr));
            {
                bool ok = new[] { UnitCatalog.HisaPt, UnitCatalog.HisaP60, UnitCatalog.HisaP180 }.All(h =>
                {
                    var f = Presets.HisaBoard366(h);
                    return S307.Seats(f) == "ボルグ・ミサ・ヒサ・カド・アカ" && f[2] == h && f.Occupied().Where(o => o.Def.Id != "hisa").All(o => UnitCatalog.All.Contains(o.Def));
                });
                Expect("版の台（`Presets.HisaBoard366`）＝ 台 B（前1 ボルグ ／ 前3 ミサ ／ 中央 ヒサ ／ 後1 カド ／ 後3 アカ）・ヒサだけ版・ほかは規定の駒", ok);
            }
        }

        // ---- PickOne ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show de65f8ec:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh366*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第365期（de65f8ec）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0 && mine == 0,
                $"engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
