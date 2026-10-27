using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;
using S367 = Sh367Diag;

// sh368 の自己検査（第368期・指示書 §6）。
static partial class Sh368Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, new[] { typeof(UnitState) })!;
    static UnitTally TallyOf(BattleContext ctx, UnitState u) => (UnitTally)TallyOfM.Invoke(ctx, new object[] { u })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Dummy = new() { Id = "dummy368", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation mine, Formation foe)
    {
        var ctx = new BattleContext(0, false);
        var p = BattleEngine.Materialize(mine, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(foe, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        return (ctx, p, e);
    }
    static UnitDef With(UnitDef d, params TraitId[] cards) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances, Pattern = d.Pattern, Actions = d.Actions,
        Traits = d.Traits.Concat(cards).ToArray(), PlusText = d.PlusText,
    };
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh368 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            var (g, v, h) = (UnitCatalog.GuzaH371, UnitCatalog.VioH371, UnitCatalog.Hisa);
            bool def = g.Traits.SequenceEqual(UnitCatalog.GuzaG.Traits) && g.PlusText == UnitCatalog.GuzaG.PlusText && g.MinusText == UnitCatalog.GuzaG.MinusText
                && v.Traits.SequenceEqual(UnitCatalog.VioV.Traits) && v.PlusText == UnitCatalog.VioV.PlusText && v.MinusText == UnitCatalog.VioV.MinusText && v.Actions is { Count: 1 } va && va[0].Kind == ActionKind.Skill
                && h.Traits.SequenceEqual(UnitCatalog.HisaY1.Traits) && h.PlusText == UnitCatalog.HisaY1.PlusText && h.MinusText == UnitCatalog.HisaY1.MinusText
                && !UnitCatalog.All.Contains(g) && !UnitCatalog.All.Contains(v) && UnitCatalog.All.Contains(h) && UnitCatalog.All.Count == 52;   /* 第372期: グザ ／ ヴィオは `*H371`（`All` の外） */
            Expect("段0: 規定のグザ ／ ヴィオ ／ ヒサ ＝ 第367期の版 G ／ V ／ H1（札 ／ 文面 ／ 手番）・`All` は 52 枚のまま", def);
            bool old = UnitCatalog.GuzaH367.Traits.SequenceEqual(new[] { TraitId.Miasma }) && UnitCatalog.VioH367.Traits.SequenceEqual(new[] { TraitId.Blightfed, TraitId.Spit }) && UnitCatalog.VioH367.Actions is null
                && UnitCatalog.HisaH367.Traits.SequenceEqual(UnitCatalog.HisaH337.Traits.Append(TraitId.MarkInheritSpread))
                && new[] { UnitCatalog.GuzaH367, UnitCatalog.VioH367, UnitCatalog.HisaH367 }.All(d => !UnitCatalog.All.Contains(d) && UnitCatalog.Everyone.Contains(d) == false)
                && new[] { (g, UnitCatalog.GuzaH367), (v, UnitCatalog.VioH367), (h, UnitCatalog.HisaH367) }.All(x => x.Item1.Id == x.Item2.Id && x.Item1.MaxHp == x.Item2.MaxHp && x.Item1.Attack == x.Item2.Attack && x.Item1.Speed == x.Item2.Speed);
            Expect("段0: 旧の定義（`GuzaH367` ／ `VioH367` ／ `HisaH367`）＝ 第367期までの規定の札・体は同じ・`All` に入れない", old);
            var pinned = Pin367(Formation.Build(front1: g, front3: v, center: h, back1: UnitCatalog.SidH371, back3: UnitCatalog.Gald));
            bool pin = pinned[0] == UnitCatalog.GuzaH367 && pinned[1] == UnitCatalog.VioH367 && pinned[2] == UnitCatalog.HisaH367 && pinned[3] == UnitCatalog.SidH371
                && PinAG364(g) == UnitCatalog.GuzaH367 && PinH337(UnitCatalog.HisaH367) == UnitCatalog.HisaH337 && IsHisaNow(UnitCatalog.HisaH367) && IsHisaNow(h);
            Expect("段0: 固定の口（`Pin367` ／ 古い口の終端 `PinAG364` → `PinGVH367` ／ `PinH337` ／ `IsHisaNow` が `HisaH367` も受ける）", pin);
            int same = 0, n = 0;
            var play = Pin371(Presets.Versions.First(r => r.Name == "試遊・毒").F);   // 第372期: 第371期までの規定に固定
            var v367 = Pin395(Presets.PoisonHero367(UnitCatalog.GuzaG, UnitCatalog.VioV, UnitCatalog.SidH371));
            var bB = Pin395(Presets.HisaBoard366()); var bB367 = Pin395(Presets.HisaBoard366(UnitCatalog.HisaY1));
            foreach (var w in new[] { I.Boss, I.Guard, I.M5, S366.BossNoTax })
                for (int s = 0; s < 10; s++)
                {
                    n += 2;
                    if (Same(I.Raw(play, w, s).R, I.Raw(v367, w, s).R)) same++;
                    if (Same(I.Raw(bB, w, s).R, I.Raw(bB367, w, s).R)) same++;
                }
            Expect("段0: 規定の台で第367期の版と勝敗 ／ T ／ 生存数が同じ（「試遊・毒」＝ GV の台・台 B ＝ H1 の台 × 勇者 ／ 近衛 ／ 第5波 ／ 蝕みなし・seed 0..9）", same == n, $"{same} ／ {n}");
        }

        // ---- 毒に強い ----
        {
            (int Hp0, int Hp1, int Layers, long Dmg) Tick(UnitDef d, int poison, bool beni = false, int hurt = 0)
            {
                var (ctx, p, e) = Board(beni ? Formation.Build(front1: UnitCatalog.Beni, center: d) : Formation.Build(center: d), Formation.Build(center: Dummy));
                var u = p.First(x => x.Def.Id == d.Id);
                if (hurt > 0) u.Hp -= hurt;
                ctx.Poison(u, poison, e[0], PoisonRoute.Venom);
                int h0 = u.Hp; long d0 = TallyOf(ctx, u).DamageTaken;
                ctx.TickStatuses();
                return (h0, u.Hp, u.RawCounter(StatusKeys.Poison), TallyOf(ctx, u).DamageTaken - d0);
            }
            var g0 = Tick(UnitCatalog.GuzaH371, 10); var gR = Tick(UnitCatalog.GuzaR, 10); var sR = Tick(UnitCatalog.SidR, 10); var vR = Tick(UnitCatalog.VioR, 10);
            Expect("毒に強い: 刻みで HP が減らない ／ 層は残る（グザ ／ スィド ／ ヴィオ・毒 10）・規定のグザは今どおり 10 削られる", gR.Hp0 == gR.Hp1 && sR.Hp0 == sR.Hp1 && vR.Hp0 == vR.Hp1 && gR.Layers == 10 && sR.Layers == 10 && vR.Layers == 10 && g0.Hp0 - g0.Hp1 == 10 && g0.Layers == 10,
                $"規定 {g0.Hp0} → {g0.Hp1}・R グザ {gR.Hp0} → {gR.Hp1}（層 {gR.Layers}）・スィド {sR.Hp0} → {sR.Hp1}・ヴィオ {vR.Hp0} → {vR.Hp1}");
            var bn = Tick(UnitCatalog.GuzaH371, 6, beni: true, hurt: 30); var bR = Tick(UnitCatalog.GuzaR, 6, beni: true, hurt: 30);
            Expect("毒に強い: ベニの隣では今どおり刻みが回復になる（グザ ／ グザ R が同じだけ癒える）", bn.Hp1 > bn.Hp0 && bR.Hp1 - bR.Hp0 == bn.Hp1 - bn.Hp0, $"規定 {bn.Hp0} → {bn.Hp1}・R {bR.Hp0} → {bR.Hp1}");
            var m0 = Tick(UnitCatalog.Mudo, 10); var mR = Tick(With(UnitCatalog.Mudo, TraitId.PoisonProof), 10);
            Expect("毒に強い: 刻みそのものを起こさない（浴びた量を読む札が発火しない・被ダメの帳簿に段が残らない・対照の規定のムドは刻みを浴びる）", m0.Dmg > 0 && mR.Dmg == 0 && mR.Hp0 == mR.Hp1, $"規定のムド 被ダメ {m0.Dmg}・毒に強いムド {mR.Dmg}");
            var (ctx2, p2, e2) = Board(Formation.Build(center: UnitCatalog.GuzaR), Formation.Build(center: Dummy));
            ctx2.Poison(p2[0], 10, e2[0], PoisonRoute.Venom);
            int hd0 = p2[0].Hp; ctx2.Detonate(e2[0], backfire: false);
            Expect("毒に強い: 起爆（刻みではない）は今どおり受ける", p2[0].Hp < hd0, $"{hd0} → {p2[0].Hp}");
        }

        // ---- 毒の衣 ----
        {
            bool pct = PoisonCloakTrait.PercentOf(0) == 0 && PoisonCloakTrait.PercentOf(7) == 21 && PoisonCloakTrait.PercentOf(20) == 60 && PoisonCloakTrait.PercentOf(40) == 60;
            (int Lost, int After) Hit(UnitDef sid, int layers, int dmg)
            {
                var (ctx, p, e) = Board(Formation.Build(center: sid), Formation.Build(center: Dummy));
                var u = p[0];
                if (layers > 0) ctx.Poison(u, layers, e[0], PoisonRoute.Venom);
                int h0 = u.Hp; ctx.ApplyDamage(u, dmg, e[0]);
                return (h0 - u.Hp, u.RawCounter(StatusKeys.Poison));
            }
            var c0 = Hit(UnitCatalog.SidRC, 0, 40); var c20 = Hit(UnitCatalog.SidRC, 20, 50); var c30 = Hit(UnitCatalog.SidRC, 30, 50); var r20 = Hit(UnitCatalog.SidR, 20, 50);
            Expect("毒の衣: 層 0 で軽減 0 ／ 層 20 で 60% ／ 上限を超えない（層 30 でも 60%）／ 殴られると自分に +2 ／ 衣の無い R は軽減しない", pct && c0.Lost == 40 && c0.After == 2 && c20.Lost == 20 && c20.After == 22 && c30.Lost == 20 && r20.Lost == 50 && r20.After == 20,
                $"層 0: 40 → {c0.Lost}（層 {c0.After}）・層 20: 50 → {c20.Lost}（層 {c20.After}）・層 30: {c30.Lost}・R 層 20: {r20.Lost}（層 {r20.After}）");
        }

        // ---- 吸い分け ----
        {
            (int Sid, int Other, int Belly) Drain(UnitDef vio)
            {
                var (ctx, p, e) = Board(Formation.Build(front1: UnitCatalog.SidRC, front3: UnitCatalog.Gald, center: vio), Formation.Build(center: Dummy));
                var s = p.First(x => x.Def.Id == "sid"); var o = p.First(x => x.Def.Id == "gald"); var v = p.First(x => x.Def.Id == "vio");
                ctx.Poison(s, 10, e[0], PoisonRoute.Venom); ctx.Poison(o, 5, e[0], PoisonRoute.Venom);
                TraitCatalog.Get(TraitId.Blightfed).OnTurnStart(ctx, v);
                return (s.RawCounter(StatusKeys.Poison), o.RawCounter(StatusKeys.Poison), SpitTrait.BellyOf(v));
            }
            var cs = Drain(UnitCatalog.VioRCS); var r = Drain(UnitCatalog.VioR);
            Expect("吸い分け: 衣を持つ味方からは吸わない（スィド 10 が残る）／ ほかの味方からは今どおり（ガルド 5 → 0）・R のヴィオは両方吸う", cs.Sid == 10 && cs.Other == 0 && cs.Belly == 5 && r.Sid == 0 && r.Other == 0 && r.Belly == 15,
                $"RCS: スィド {cs.Sid}・ガルド {cs.Other}・腹 {cs.Belly} ／ R: スィド {r.Sid}・ガルド {r.Other}・腹 {r.Belly}");
        }

        // ---- 版の定義 ----
        {
            bool ok = UnitCatalog.SidR.Traits.SequenceEqual(UnitCatalog.SidH371.Traits.Append(TraitId.PoisonProof)) && UnitCatalog.SidRC.Traits.SequenceEqual(UnitCatalog.SidH371.Traits.Append(TraitId.PoisonProof).Append(TraitId.PoisonCloak))
                && UnitCatalog.GuzaR.Traits.SequenceEqual(UnitCatalog.GuzaH371.Traits.Append(TraitId.PoisonProof)) && UnitCatalog.VioR.Traits.SequenceEqual(UnitCatalog.VioH371.Traits.Append(TraitId.PoisonProof))
                && UnitCatalog.VioRCS.Traits.SequenceEqual(UnitCatalog.VioH371.Traits.Append(TraitId.PoisonProof).Append(TraitId.BlightfedSkipCloak))
                && new[] { UnitCatalog.SidR, UnitCatalog.SidRC, UnitCatalog.GuzaR, UnitCatalog.VioR, UnitCatalog.VioRCS }.All(d => !UnitCatalog.All.Contains(d) && d.PlusText.Contains("毒に蝕まれない", StringComparison.Ordinal))
                && !UnitCatalog.All.Any(d => !IsW5Default372(d) && d.Traits.Any(t => t is TraitId.PoisonProof or TraitId.PoisonCloak or TraitId.BlightfedSkipCloak));
            Expect("版の定義: R ／ RC ／ RCS の札（規定の末尾に足す）・`All` に入れない・文面の末尾・新しい札の保持者 0 枚", ok);
        }

        // ---- verbose ／ 器具の中だけ ----
        {
            long diff = 0, cells = 0;
            foreach (var b in BoardList())
                foreach (var v in Vers.Skip(1))
                    foreach (var w in new[] { I.Boss, I.Guard, S366.BossNoTax })
                        for (int s = 0; s < 5; s++)
                        {
                            cells++;
                            var f = Apply(b.F, v);
                            if (!Same(I.Raw(f, w, s).R, I.Raw(f, w, s, verbose: true).R)) diff++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ（6台 × 3版 × 勇者 ／ 近衛 ／ 蝕みなし・seed 0..4）", diff == 0, $"{cells} 戦・差 {diff}");
            bool noTax = !S366.BossNoTax.Make().Any(u => u.HasTrait(TraitId.VenomTaxSeal)) && EnemyCatalog.BossRegular.Traits.Contains(TraitId.VenomTaxSeal);
            Expect("蝕みなしの勇者は器具の中だけ（規定の勇者は蝕みを持つ）", noTax);
        }

        // ---- 試遊の台 ----
        {
            // 第372期: 第368〜371期の「版・」の台（RCS 吸い分け ／ W4 ／ W5 ／ W2 席替え ／ W5 席替え ほか）はすべて外した（並びはこの器具の中で組む）。検査は「頭の8台」と「版の台が無い」に。
            var vr = VersionsH407().Select(r => r.Name).ToArray();
            bool rows = vr.Take(8).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒" })
                && !vr.Any(n => n is "版・RCS 吸い分け" or "版・W4 ヴィオ型" or "版・W5 両立" or "版・W2 席替え" or "版・W5 席替え" or "版・W1 返り毒＋ヴィオ" or "版・W2 三人" or "版・W3 上限なし");
            Expect("`Presets.Versions` の頭 ＝ 試遊の8台・第368〜371期の「版・」の台は無い（第372期に外した）", rows, string.Join("、", vr));
        }

        // ---- PickOne ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show bff6c82e:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh368*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第367期（bff6c82e）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && r0 + RollsAdded369 == r1 /* 第369期: グザ Z の噴き出しの引き（`Common.RollsAdded369`）*/ && re0 == re1 && e0 > 0 && mine == 0,
                $"engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
