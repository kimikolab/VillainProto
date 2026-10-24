using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;
using S370 = Sh370Diag;

// sh372 の自己検査（第372期・指示書 §6）。
static partial class Sh372Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly MethodInfo Swing = typeof(BattleContext).GetMethod("SwingTurnHits", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Dummy = new() { Id = "dummy372", Name = "的", MaxHp = 9999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Spiky = new() { Id = "spiky372", Name = "棘の的", MaxHp = 9999, Attack = 50, Speed = 1, Traits = new[] { TraitId.Thorns }, PlusText = "" };

    static (BattleContext Ctx, UnitState V, List<UnitState> E) Board(UnitDef vio, Formation foe)
    {
        var ctx = new BattleContext(0, false);
        var p = BattleEngine.Materialize(Formation.Build(back1: vio), BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(foe, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        return (ctx, p[0], e);
    }
    static int Psn(UnitState u) => u.RawCounter(StatusKeys.Poison);
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;

    /// <summary>攻撃力 <paramref name="atk"/> のヴィオに手番を1回振らせる。返すのは敵ごとの毒の増え（席順）と撃った数。</summary>
    static (int[] Got, long Hits, BattleContext Ctx, UnitState V, List<UnitState> E) SwingAt(UnitDef vio, int atk, Formation foe, int vioHp = 0)
    {
        var (ctx, v, e) = Board(vio, foe);
        v.AtkBonus = atk - v.Def.Attack;
        if (vioHp > 0) v.Hp = vioHp;
        int[] f0 = e.Select(Psn).ToArray();
        Swing.Invoke(ctx, new object?[] { v, null });
        long hits = ((UnitTally)TallyOfM.Invoke(ctx, new object[] { v })!).Z372HitsByTurn?.Sum() ?? 0;
        return (e.Select((u, i) => Psn(u) - f0[i]).ToArray(), hits, ctx, v, e);
    }

    static Formation Three => Formation.Build(front1: Dummy, front3: Dummy, center: Dummy);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh372 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0: 規定 ＝ W5 ／ 旧の定義 ／ 固定の口 ----
        {
            bool w5 = UnitCatalog.Guza.Traits.SequenceEqual(UnitCatalog.GuzaW3.Traits) && UnitCatalog.VioH372.Traits.SequenceEqual(UnitCatalog.VioW.Traits) && UnitCatalog.SidH374.Traits.SequenceEqual(UnitCatalog.SidY.Traits)
                && UnitCatalog.VioH372.Actions is null && UnitCatalog.SidH374.Actions is { Count: 1 } sa && sa[0].Kind == ActionKind.Skill
                && new[] { UnitCatalog.Guza, UnitCatalog.Vio, UnitCatalog.Sid }.All(d => UnitCatalog.All.Contains(d)) && !UnitCatalog.All.Contains(UnitCatalog.VioH372) && UnitCatalog.All.Count == 52;   // 第373期: 規定のヴィオは Va・第372期の規定は `VioH372`（`All` の外）
            bool txt = UnitCatalog.VioH372.PlusText.Contains("攻撃力と同じだけ毒") && UnitCatalog.SidH374.PlusText.Contains("まとった毒と同じだけ") && UnitCatalog.Guza.MinusText.Contains("噴き出");
            bool old = UnitCatalog.GuzaH371.Traits.SequenceEqual(new[] { TraitId.Miasma, TraitId.ScatterGather }) && UnitCatalog.VioH371.Traits.SequenceEqual(new[] { TraitId.Blightfed, TraitId.BlightCatalyst })
                && UnitCatalog.SidH371.Traits.SequenceEqual(new[] { TraitId.Spew, TraitId.VenomHeavy, TraitId.Numb })
                && new[] { UnitCatalog.GuzaH371, UnitCatalog.VioH371, UnitCatalog.SidH371 }.All(d => !UnitCatalog.All.Contains(d));
            bool pins = PinGVS371(UnitCatalog.Guza) == UnitCatalog.GuzaH371 && PinGVS371(UnitCatalog.VioH372) == UnitCatalog.VioH371 && PinGVS371(UnitCatalog.SidH374) == UnitCatalog.SidH371
                && PinGVH367(UnitCatalog.Guza) == UnitCatalog.GuzaH367 && PinGVH367(UnitCatalog.GuzaH371) == UnitCatalog.GuzaH367 && PinGVH367(UnitCatalog.VioH372) == UnitCatalog.VioH367 && PinGVH367(UnitCatalog.VioH371) == UnitCatalog.VioH367
                && PinGVH367(UnitCatalog.SidH374) == UnitCatalog.SidH371 && PinGVH367(UnitCatalog.Gald) == UnitCatalog.Gald
                && Pin371(Pin395(Presets.PoisonHero367())).Occupied().Select(o => o.Def).SequenceEqual(Pin395(Presets.PoisonHero367(UnitCatalog.GuzaH371, UnitCatalog.VioH371, UnitCatalog.SidH371)).Occupied().Select(o => o.Def));
            Expect("段0: 規定のグザ ／ ヴィオ ／ スィドの札 ＝ 第371期の W5（グザ W3 ／ ヴィオ W ／ スィド Y）・文面・旧の定義（`*H371`・`All` の外）・固定の口（`Pin371` ／ `PinGVH367` の終端 `PinS371`）", w5 && txt && old && pins);
        }
        {
            long diff = 0, cells = 0;
            var pairs = new (Formation Now, Formation W5)[]
            {
                (Pin372(Presets.Versions.First(r => r.Name == "試遊・毒").F), Pin395(Presets.PoisonHero367(UnitCatalog.GuzaW3, UnitCatalog.VioW, UnitCatalog.SidY))),   // 第373期: 第372期の規定に固定
                (Pin372(Presets.Versions.First(r => r.Name == "試遊・毒 席替え").F), Pin395(Presets.PoisonSeat372(UnitCatalog.GuzaW3, UnitCatalog.VioW, UnitCatalog.SidY))),
            };
            foreach (var (now, w5) in pairs)
                foreach (var w in Waves)
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(now, w, sd).R, I.Raw(w5, w, sd).R)) diff++;
                    }
            Expect("段0: 規定の台（試遊・毒 ／ 試遊・毒 席替え）が第371期の W5 の駒で組んだ台と勝敗 ／ T ／ 生存数が同じ（6 波 × seed 0..9）", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- 連撃: 段 ----
        {
            var cases = new (UnitDef V, int Atk, int Want)[]
            {
                (UnitCatalog.VioVa, 49, 1), (UnitCatalog.VioVa, 50, 2), (UnitCatalog.VioVa, 149, 2), (UnitCatalog.VioVa, 150, 3), (UnitCatalog.VioVa, 400, 3),
                (UnitCatalog.VioVb, 99, 1), (UnitCatalog.VioVb, 100, 2), (UnitCatalog.VioVb, 299, 2), (UnitCatalog.VioVb, 300, 3),
                (UnitCatalog.VioVc, 49, 1), (UnitCatalog.VioVc, 50, 2), (UnitCatalog.VioVc, 150, 3),
                (UnitCatalog.VioH372, 49, 1), (UnitCatalog.VioH372, 150, 1), (UnitCatalog.VioH372, 400, 1),
            };
            var bad = new List<string>();
            foreach (var (v, atk, want) in cases)
            {
                var r = SwingAt(v, atk, Three);
                int strikes = r.Got.Sum() / atk;
                bool isDefault = ReferenceEquals(v, UnitCatalog.VioH372);
                if (strikes != want || (!isDefault && r.Hits != want) || (isDefault && r.Hits != 0) || r.Got.Sum() != want * atk)
                    bad.Add($"{(isDefault ? "規定" : ReferenceEquals(v, UnitCatalog.VioVa) ? "Va" : ReferenceEquals(v, UnitCatalog.VioVb) ? "Vb" : "Vc")} 攻 {atk}: 撃 {strikes}（計数 {r.Hits}）");
            }
            Expect("連撃の段: Va ／ Vc は攻撃力 49 で1発 ／ 50 で2発 ／ 150 で3発・Vb は 99 ／ 100 ／ 300・1発ごとに攻撃力ぶんの毒・規定のヴィオは何を持っても1発（計数も書かない）", bad.Count == 0,
                bad.Count == 0 ? $"{cases.Length} 通り" : string.Join("・", bad));
        }

        // ---- 連撃: 的 ----
        {
            var a = SwingAt(UnitCatalog.VioVa, 150, Three);
            var b = SwingAt(UnitCatalog.VioVb, 300, Three);
            var c = SwingAt(UnitCatalog.VioVc, 150, Three);
            var a1 = SwingAt(UnitCatalog.VioVa, 150, Formation.Build(center: Dummy));
            var a2 = SwingAt(UnitCatalog.VioVa, 150, Formation.Build(front1: Dummy, back3: Dummy));
            bool spread = a.Got.All(x => x == 150) && b.Got.All(x => x == 300);
            bool focus = c.Got.Count(x => x == 450) == 1 && c.Got.Count(x => x == 0) == 2;
            bool one = a1.Got.SequenceEqual(new[] { 450 });
            bool back = a2.Got.OrderBy(x => x).SequenceEqual(new[] { 150, 300 });   // 2体: 1発目 → 別の敵 → 足りないので1発目の的へ戻る
            Expect("連撃の的: Va ／ Vb は1発目の的を除いた敵へ席順（3体に1発ずつ）・敵1体なら同じ敵に3発・2体なら3発目は1発目の的へ戻る・Vc は3発とも同じ敵", spread && focus && one && back,
                $"Va [{string.Join(",", a.Got)}]・Vb [{string.Join(",", b.Got)}]・Vc [{string.Join(",", c.Got)}]・1体 [{string.Join(",", a1.Got)}]・2体 [{string.Join(",", a2.Got)}]");
        }

        // ---- 連撃: 途中で倒れたら残りは撃たない ／ 段は手番の頭 ----
        {
            var r = SwingAt(UnitCatalog.VioVa, 150, Formation.Build(front1: Spiky, front3: Spiky, center: Spiky), vioHp: 1);
            bool died = !r.V.IsAlive && r.Got.Sum() == 150;
            Expect("連撃: 1発目で棘に刺されて倒れたら残り2発は撃たない（段は振る前に1回だけ数える ＝ 計数は 3・毒は 1発ぶん）", died && r.Hits == 3,
                $"生 {r.V.IsAlive}・毒 [{string.Join(",", r.Got)}]・計数 {r.Hits}");
        }

        // ---- 規定のヴィオ ／ 版の定義 ----
        {
            bool vers = new[] { (UnitCatalog.VioVa, TraitId.VioFlurryA), (UnitCatalog.VioVb, TraitId.VioFlurryB), (UnitCatalog.VioVc, TraitId.VioFlurryC) }
                .All(x => x.Item1.Traits.SequenceEqual(UnitCatalog.VioH372.Traits.Append(x.Item2)) && x.Item1.Id == UnitCatalog.VioH372.Id && !UnitCatalog.All.Contains(x.Item1) && x.Item1.PlusText.StartsWith(UnitCatalog.VioH372.PlusText));
            bool none = !UnitCatalog.All.Any(d => d != UnitCatalog.Vio && d.Traits.Any(t => t is TraitId.VioFlurryA or TraitId.VioFlurryB or TraitId.VioFlurryC))   // 第373期: 規定のヴィオは Va（`VioFlurryA`）を持つ
                && !CompareBuilds().Select(r => (r.Name, F: Pin372(r.F))).Any(r => r.F.Occupied().Any(o => o.Def.Traits.Any(t => t is TraitId.VioFlurryA or TraitId.VioFlurryB or TraitId.VioFlurryC)));
            Expect("版 Va ／ Vb ／ Vc ＝ 規定のヴィオ ＋ 札1枚（`All` ／ `compare` の行には無い）", vers && none);
        }

        // ---- 器具の中だけ ／ verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var bd in BoardList())
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, S366.BossNoTax })
                        for (int sd = 0; sd < 3; sd++)
                        {
                            cells++;
                            var f = Apply(bd.F, v);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（5台 × 4版 × 勇者 ／ 近衛 ／ 蝕みなし・seed 0..2）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
            bool noTax = !S366.BossNoTax.Make().Any(u => u.HasTrait(TraitId.VenomTaxSeal)) && EnemyCatalog.BossRegular.Traits.Contains(TraitId.VenomTaxSeal);
            Expect("蝕みなしの勇者は器具の中だけ（規定の勇者は蝕みを持つ）", noTax);
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var want = new[] { TraitId.VioFlurryA, TraitId.VioFlurryB, TraitId.VioFlurryC }.Concat(UnitCatalog.Guza.Traits).Concat(UnitCatalog.VioH372.Traits).Concat(UnitCatalog.SidH374.Traits).Distinct();
            var missing = want.Where(t => !src.Contains($"[TraitId.{t}]", StringComparison.Ordinal)).ToList();
            bool w5 = new[] { TraitId.MiasmaVent, TraitId.MiasmaSplash, TraitId.SplashUncapped, TraitId.StrikePoison, TraitId.PoisonCloak, TraitId.CloakRetort }
                .All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\][^\n]*第372期から規定"));
            Expect("`checkup` の札の表に連撃の3枚と規定の3枚の札がそろい、W5 の札は「第372期から規定」に分け直した（ソースを読んで確かめる）", missing.Count == 0 && w5, missing.Count == 0 ? "そろっている" : "無い: " + string.Join("・", missing));
        }

        // ---- 試遊の台 ----
        {
            // 第373期: 第372期の「版・Va ／ Vb ／ Vc」は外した（Va を規定にした・並びはこの器具の中で組む）。検査は「頭の9台」と「第372期の版の台が無い」に。
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.Take(9).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "試遊・毒 席替え" })
                && !vr.Any(n => n is "版・Va 散らす" or "版・Vb 高い段" or "版・Vc 集中");
            string seat = S307.Seats(Presets.Versions.First(r => r.Name == "試遊・毒 席替え").F);
            Expect("`Presets.Versions` の頭 ＝ 試遊の8台 ＋「試遊・毒 席替え」（ドルガ・ガルド・スィド・グザ・ヴィオ）・第372期の「版・」の3台は無い（第373期に外した）", rows && seat == "ドルガ・ガルド・スィド・グザ・ヴィオ", $"{string.Join("、", vr)}・席替え {seat}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 731d9b7d:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第371期（731d9b7d）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
