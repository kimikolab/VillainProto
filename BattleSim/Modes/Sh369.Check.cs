using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;

// sh369 の自己検査（第369期・指示書 §7）。
static partial class Sh369Diag
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

    static readonly UnitDef Dummy = new() { Id = "dummy369", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Mate = new() { Id = "mate369", Name = "仲間", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static Formation Five(UnitDef d) => Formation.Build(front1: d, front3: d, center: d, back1: d, back3: d);

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation mine, Formation foe, int seed = 0)
    {
        var ctx = new BattleContext(seed, false);
        var p = BattleEngine.Materialize(mine, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(foe, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        return (ctx, p, e);
    }
    static int Psn(UnitState u) => u.RawCounter(StatusKeys.Poison);
    static void Act(BattleContext ctx, UnitState u, TraitId id) => TraitCatalog.Get(id).OnAction(ctx, u, u.Def.Actions?.FirstOrDefault() ?? new UnitAction(ActionKind.Attack));
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh369 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義 ／ 規定は今どおり ----
        {
            bool def = UnitCatalog.GuzaH371.Traits.SequenceEqual(new[] { TraitId.Miasma, TraitId.ScatterGather }) && UnitCatalog.VioH371.Traits.SequenceEqual(new[] { TraitId.Blightfed, TraitId.BlightCatalyst })
                && UnitCatalog.SidH371.Traits.SequenceEqual(new[] { TraitId.Spew, TraitId.VenomHeavy, TraitId.Numb });
            bool z = UnitCatalog.GuzaZ.Traits.SequenceEqual(UnitCatalog.GuzaH371.Traits.Append(TraitId.MiasmaVent).Append(TraitId.PoisonProof))
                && UnitCatalog.VioZ.Traits.SequenceEqual(new[] { TraitId.CatalystSip, TraitId.PoisonProof }) && UnitCatalog.VioZ.Actions is { Count: 1 } va && va[0].Kind == ActionKind.Skill
                && UnitCatalog.SidZ.Traits.SequenceEqual(new[] { TraitId.PoisonShoulder, TraitId.PoisonProof, TraitId.PoisonCloak, TraitId.CloakRetort, TraitId.Numb }) && UnitCatalog.SidZ.Actions is { Count: 1 } sa && sa[0].Kind == ActionKind.Skill
                && new[] { UnitCatalog.GuzaZ, UnitCatalog.VioZ, UnitCatalog.SidZ }.All(d => !UnitCatalog.All.Contains(d))
                && !UnitCatalog.All.Any(d => !IsW5Default372(d) && d.Traits.Any(t => t is TraitId.MiasmaVent or TraitId.CatalystSip or TraitId.PoisonShoulder or TraitId.CloakRetort or TraitId.PoisonProof));
            Expect("規定のグザ ／ ヴィオ ／ スィドは今どおり（札）・版 Z の札（3枚とも毒に強い・ヴィオ ／ スィドの手番は `Skill`）・`All` に入れない・新しい札の保持者 0 枚", def && z);
        }

        // ---- グザ: 噴き出し ／ 段 ----
        {
            (int[] Foes, int[] Mates, int Stage) Vent(int preFoe)
            {
                var (ctx, p, e) = Board(Formation.Build(front1: Mate, front3: Mate, center: UnitCatalog.GuzaZ, back1: Mate, back3: Mate), Five(Dummy));
                var g = p.First(u => u.Def.Id == "guza");
                if (preFoe > 0) ctx.Poison(e[0], preFoe, p[0], PoisonRoute.Venom);
                var f0 = e.Select(Psn).ToArray(); var m0 = p.Select(Psn).ToArray();
                int st = MiasmaVentTrait.StageOf(ctx, g);
                TraitCatalog.Get(TraitId.MiasmaVent).OnTurnStart(ctx, g);
                return (e.Select((u, i) => Psn(u) - f0[i]).ToArray(), p.Select((u, i) => Psn(u) - m0[i]).ToArray(), st);
            }
            var s0 = Vent(0); var s1 = Vent(30); var s2 = Vent(80); var s3 = Vent(200); var s29 = Vent(29);
            bool ok0 = s0.Stage == 0 && s0.Foes.Count(x => x == 2) == 2 && s0.Foes.Count(x => x == 0) == 3 && s0.Mates.Count(x => x == 1) == 2 && s0.Mates.Count(x => x == 0) == 3;
            bool ok1 = s29.Stage == 0 && s1.Stage == 1 && s1.Foes.Count(x => x == 3) == 3 && s1.Mates.Count(x => x == 1) == 3;
            bool ok2 = s2.Stage == 2 && s2.Foes.Count(x => x == 4) == 4 && s2.Mates.Count(x => x == 2) == 4;
            bool ok3 = s3.Stage == 3 && s3.Foes.All(x => x == 6) && s3.Mates.All(x => x == 3);
            Expect("グザ: 段 0 で敵 2 体に +2 ／ 味方 2 体に +1・累計 30 ／ 80 ／ 200 で段 1 ／ 2 ／ 3（29 は段 0）・段 3 は全員に +6 ／ +3", ok0 && ok1 && ok2 && ok3,
                $"段0 敵 [{string.Join(",", s0.Foes)}] 味方 [{string.Join(",", s0.Mates)}]・段1 [{string.Join(",", s1.Foes)}] ／ [{string.Join(",", s1.Mates)}]・段2 [{string.Join(",", s2.Foes)}]・段3 [{string.Join(",", s3.Foes)}] ／ [{string.Join(",", s3.Mates)}]");
            // 段は下がらない（敵が倒れて累計は減らない）・味方全体の +1（手番の瘴気の漏れ）は起きない
            var (ctx2, p2, e2) = Board(Formation.Build(front1: Mate, center: UnitCatalog.GuzaZ), Formation.Build(center: Dummy, front1: Dummy));
            var g2 = p2.First(u => u.Def.Id == "guza");
            ctx2.Poison(e2[0], 90, p2[0], PoisonRoute.Venom);
            int before = MiasmaVentTrait.StageOf(ctx2, g2);
            ctx2.ApplyDamage(e2[0], 999, p2[0]);
            int after = MiasmaVentTrait.StageOf(ctx2, g2);
            var m0 = p2.Select(Psn).ToArray();
            TraitCatalog.Get(TraitId.Miasma).OnAction(ctx2, g2, new UnitAction(ActionKind.Attack));
            bool noLeak = p2.Select(Psn).SequenceEqual(m0) && Psn(e2[1]) > 0;
            Expect("グザ: 段は下がらない（段 2 の敵を倒しても段 2）・手番の瘴気で味方全体の +1 は起きない（敵には撒く）", before == 2 && after == 2 && noLeak, $"段 {before} → {after}・味方 [{string.Join(",", m0)}] → [{string.Join(",", p2.Select(Psn))}]");
        }

        // ---- グザ: 返り毒 ----
        {
            (int Ally, int Foe) Hit(UnitDef mate, int layers, bool killGuza = false)
            {
                var (ctx, p, e) = Board(Formation.Build(front1: mate, center: UnitCatalog.GuzaZ), Formation.Build(center: Dummy));
                var a = p.First(u => u.Def.Id == mate.Id); var g = p.First(u => u.Def.Id == "guza");
                ctx.Poison(a, layers, e[0], PoisonRoute.Venom);
                if (killGuza) ctx.ApplyDamage(g, 999, null);
                ctx.ApplyDamage(a, 5, e[0]);
                return (Psn(a), Psn(e[0]));
            }
            var h7 = Hit(Mate, 7); var h1 = Hit(Mate, 1); var hc = Hit(UnitCatalog.SidRC, 7); var hd = Hit(Mate, 7, killGuza: true);
            Expect("グザ: 返り毒で半分（切り捨て）が殴った敵へ移る（7 → 4 ／ 敵 +3）・1層は移らない・毒の衣からは移らない・グザが倒れたら移らない",
                h7.Ally == 4 && h7.Foe == 3 && h1.Ally == 1 && h1.Foe == 0 && hc.Ally == 7 + PoisonCloakTrait.SelfStack && hd.Ally == 7 && hd.Foe == 0,   /* 衣の持ち主は半分にならない（衣の +2 だけ増える・敵へ付くのは毒撃の +8） */
                $"7: {h7.Ally} ／ 敵 {h7.Foe}・1: {h1.Ally} ／ {h1.Foe}・衣: スィド {hc.Ally}（敵 {hc.Foe} は毒撃）・グザなし: {hd.Ally} ／ {hd.Foe}");
        }

        // ---- ヴィオ Z ----
        {
            var (ctx, p, e) = Board(Formation.Build(front1: UnitCatalog.SidRC, front3: Mate, center: UnitCatalog.VioZ), Formation.Build(front1: Dummy, front3: Dummy));
            var v = p.First(u => u.Def.Id == "vio"); var s = p.First(u => u.Def.Id == "sid"); var m = p.First(u => u.Def.Id == "mate369");
            ctx.Poison(s, 10, e[0], PoisonRoute.Venom); ctx.Poison(m, 5, e[0], PoisonRoute.Venom); ctx.Poison(v, 4, e[0], PoisonRoute.Venom);
            ctx.Poison(e[0], 3, p[0], PoisonRoute.Venom); ctx.Poison(e[1], 8, p[0], PoisonRoute.Venom);
            foreach (var t in v.Traits) t.OnTurnStart(ctx, v);
            bool noStart = Psn(m) == 5 && Psn(s) == 10;
            Act(ctx, v, TraitId.CatalystSip);
            bool ok = noStart && Psn(m) == 0 && Psn(s) == 10 && Psn(v) == 4 && Psn(e[0]) == 3 && Psn(e[1]) == 8 + 8 + 5;
            Expect("ヴィオ Z: ターンの頭に吸わない・手番で啜って（自分 ／ 衣の持ち主は除く）最も深い敵を倍 ＋ 注ぎ（8 → 21）", ok, $"仲間 {Psn(m)}・スィド {Psn(s)}・自分 {Psn(v)}・敵 [{Psn(e[0])},{Psn(e[1])}]");
        }

        // ---- スィド Z ----
        {
            var (ctx, p, e) = Board(Formation.Build(front1: Mate, front3: Mate, center: UnitCatalog.SidZ), Formation.Build(center: Dummy));
            var s = p.First(u => u.Def.Id == "sid");
            var adj = p.Where(u => u != s && FormationRules.AreAdjacent(s, u)).ToList();
            foreach (var a in adj) ctx.Poison(a, 4, e[0], PoisonRoute.Venom);
            int f0 = Psn(e[0]);
            Act(ctx, s, TraitId.PoisonShoulder);
            bool shoulder = adj.All(a => Psn(a) == 0) && Psn(s) == 4 * adj.Count && Psn(e[0]) == f0;
            int layers = Psn(s);
            ctx.ApplyDamage(s, 10, e[0]);
            bool retort = Psn(s) == layers + 2 && Psn(e[0]) == f0 + layers + 2 && e[0].RawCounter(StatusKeys.Numbed) > 0;
            Expect("スィド Z: 手番で隣の毒を引き受ける（敵には書かない）・殴られると +2 の後に層と同じだけ浴びせる（衣は減らない）・痺れ毒の印", shoulder && retort && adj.Count > 0,
                $"隣 {adj.Count} 体・引き受けた {layers}・殴られた後 自分 {Psn(s)} ／ 敵 {Psn(e[0])}");
        }

        // ---- 決定性 ／ verbose ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in BoardList())
                foreach (var v in Vers.Skip(1))
                    foreach (var w in new[] { I.Boss, I.Guard, S366.BossNoTax })
                        for (int s = 0; s < 5; s++)
                        {
                            cells++;
                            var f = Apply(b.F, v);
                            var r0 = I.Raw(f, w, s).R;
                            if (!Same(r0, I.Raw(f, w, s, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, s).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（5台 × 4版 × 勇者 ／ 近衛 ／ 蝕みなし・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
            bool noTax = !S366.BossNoTax.Make().Any(u => u.HasTrait(TraitId.VenomTaxSeal)) && EnemyCatalog.BossRegular.Traits.Contains(TraitId.VenomTaxSeal);
            Expect("蝕みなしの勇者は器具の中だけ", noTax);
        }

        // ---- 試遊の台 ----
        {
            // 第372期: 第368〜371期の「版・」の台（RCS 吸い分け ／ W4 ／ W5 ／ W2 席替え ／ W5 席替え ほか）はすべて外した（並びはこの器具の中で組む）。検査は「頭の8台」と「版の台が無い」に。
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.Take(8).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒" })
                && !vr.Any(n => n is "版・RCS 吸い分け" or "版・W4 ヴィオ型" or "版・W5 両立" or "版・W2 席替え" or "版・W5 席替え" or "版・W1 返り毒＋ヴィオ" or "版・W2 三人" or "版・W3 上限なし");
            Expect("`Presets.Versions` の頭 ＝ 試遊の8台・第368〜371期の「版・」の台は無い（第372期に外した）", rows, string.Join("、", vr));
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 6477352e:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`PickOne` は増えていない・`Roll(` は Traits.cs で 1 増えた（噴き出しの相手の引き・バサと同じ `ctx.Roll`）・engine は同じ（第368期 6477352e と比べて）", e0 == e1 && t0 == t1 && r1 == r0 + 1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
