using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;

// sh370 の自己検査（第370期・指示書 §7）。
static partial class Sh370Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Dummy = new() { Id = "dummy370", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Mate = new() { Id = "mate370", Name = "仲間", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef YokeHolder = new() { Id = "yoke370", Name = "軛の保持者", MaxHp = 999, Attack = 1, Speed = 1, Traits = new[] { TraitId.Yoke }, PlusText = "" };

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation mine, Formation foe)
    {
        var ctx = new BattleContext(0, false);
        var p = BattleEngine.Materialize(mine, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(foe, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        return (ctx, p, e);
    }
    static int Psn(UnitState u) => u.RawCounter(StatusKeys.Poison);
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh370 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義 ／ 規定は今どおり ----
        {
            bool def = UnitCatalog.Guza.Traits.SequenceEqual(new[] { TraitId.Miasma, TraitId.ScatterGather }) && UnitCatalog.Vio.Traits.SequenceEqual(new[] { TraitId.Blightfed, TraitId.BlightCatalyst })
                && UnitCatalog.Sid.Traits.SequenceEqual(new[] { TraitId.Spew, TraitId.VenomHeavy, TraitId.Numb });
            bool w = UnitCatalog.GuzaW.Traits.SequenceEqual(UnitCatalog.Guza.Traits.Concat(new[] { TraitId.MiasmaVent, TraitId.MiasmaSplash, TraitId.PoisonProof }))
                && UnitCatalog.GuzaW3.Traits.SequenceEqual(UnitCatalog.Guza.Traits.Concat(new[] { TraitId.MiasmaVent, TraitId.MiasmaSplash, TraitId.SplashUncapped, TraitId.PoisonProof }))
                && UnitCatalog.VioW.Traits.SequenceEqual(new[] { TraitId.Blightfed, TraitId.BlightfedSkipCloak, TraitId.StrikePoison, TraitId.PoisonProof }) && UnitCatalog.VioW.Actions is null
                && !UnitCatalog.VioW.Traits.Any(t => t is TraitId.BlightCatalyst or TraitId.Spit or TraitId.CatalystSip)
                && new[] { UnitCatalog.GuzaW, UnitCatalog.GuzaW3, UnitCatalog.VioW }.All(d => !UnitCatalog.All.Contains(d))
                && !UnitCatalog.All.Any(d => d.Traits.Any(t => t is TraitId.MiasmaSplash or TraitId.SplashUncapped or TraitId.StrikePoison or TraitId.VentOff370 or TraitId.BacklashOff370));
            Expect("規定のグザ ／ ヴィオ ／ スィドは今どおり・版 W の札（グザ W ＝ 噴き出し ＋ 向きを戻した返り毒 ＋ 毒に強い・W3 は上限なし・ヴィオ W ＝ 吸い ＋ 吸い分け ＋ 攻撃力ぶんの毒 ＋ 毒に強い・ふつうに殴る・触媒 ／ 吐き戻しなし）・`All` に入れない・新しい札の保持者 0 枚", def && w);
        }

        // ---- 返り毒（向きを戻した） ----
        {
            (int Hit, int Foe, int Other) Splash(UnitDef guza, int foePoison, bool killGuza = false)
            {
                var (ctx, p, e) = Board(Formation.Build(front1: Mate, front3: Mate, center: guza), Formation.Build(center: Dummy));
                var m = p[0]; var o = p[1]; var g = p.First(u => u.Def.Id == "guza");
                if (foePoison > 0) ctx.Poison(e[0], foePoison, m, PoisonRoute.Venom);
                if (killGuza) ctx.ApplyDamage(g, 999, null);
                int f0 = Psn(e[0]);
                ctx.ApplyDamage(m, 5, e[0]);
                return (Psn(m), Psn(e[0]) - f0, Psn(o));
            }
            var s30 = Splash(UnitCatalog.GuzaW, 30); var s80 = Splash(UnitCatalog.GuzaW, 80); var s80u = Splash(UnitCatalog.GuzaW3, 80); var s0 = Splash(UnitCatalog.GuzaW, 0); var sd = Splash(UnitCatalog.GuzaW, 80, killGuza: true);
            Expect("返り毒: 毒 30 の敵が殴ると殴られた味方に +3 ／ 毒 80 で +5（上限）／ W3 で +8 ／ 敵の層は減らない ／ 殴られていない味方には付かない ／ 毒の無い敵では起きない ／ グザが倒れたら起きない",
                s30.Hit == 3 && s80.Hit == 5 && s80u.Hit == 8 && s30.Foe == 0 && s80.Foe == 0 && s80.Other == 0 && s0.Hit == 0 && sd.Hit == 0,
                $"30: +{s30.Hit}・80: +{s80.Hit}・W3 80: +{s80u.Hit}・敵の層の増減 {s30.Foe} ／ {s80.Foe}・毒なし +{s0.Hit}・グザなし +{sd.Hit}");
            // 全体攻撃で当たった味方ごと（同じ敵の全体の一撃 ＝ 味方ごとの ApplyDamage）
            var (ctx2, p2, e2) = Board(Formation.Build(front1: Mate, front3: Mate, back1: Mate, center: UnitCatalog.GuzaW), Formation.Build(center: Dummy));
            ctx2.Poison(e2[0], 40, p2[0], PoisonRoute.Venom);
            foreach (var u in p2) ctx2.ApplyDamage(u, 3, e2[0], pattern: AttackPattern.All);
            Expect("返り毒: 全体攻撃なら当たった味方ごと（4 体 × +4）", p2.All(u => Psn(u) == 4), string.Join(",", p2.Select(Psn)));
        }

        // ---- 段 50 ／ 150 ／ 400 ----
        {
            var (ctx, p, e) = Board(Formation.Build(front1: Mate, center: UnitCatalog.GuzaW), Formation.Build(center: Dummy, front1: Dummy));
            var g = p.First(u => u.Def.Id == "guza");
            int[] st = new int[5];
            st[0] = MiasmaVentTrait.StageOf(ctx, g);
            ctx.Poison(e[0], 49, p[0], PoisonRoute.Venom); st[1] = MiasmaVentTrait.StageOf(ctx, g);
            ctx.Poison(e[0], 1, p[0], PoisonRoute.Venom); st[2] = MiasmaVentTrait.StageOf(ctx, g);
            ctx.Poison(e[1], 100, p[0], PoisonRoute.Venom); st[3] = MiasmaVentTrait.StageOf(ctx, g);
            ctx.Poison(e[1], 250, p[0], PoisonRoute.Venom); ctx.ApplyDamage(e[1], 9999, p[0]); st[4] = MiasmaVentTrait.StageOf(ctx, g);
            Expect("段: 50 ／ 150 ／ 400 で上がり（49 は段 0）、敵が倒れても下がらない・第369期の Z は 30 ／ 80 ／ 200 のまま", st.SequenceEqual(new[] { 0, 0, 1, 2, 3 }) && MiasmaVentTrait.StepsOf(BattleEngine.Materialize(Formation.Build(center: UnitCatalog.GuzaZ), BattleContext.PlayerTeam)[0]).SequenceEqual(MiasmaVentTrait.Steps),
                string.Join(" → ", st));
        }

        // ---- ヴィオ W ----
        {
            var (ctx, p, e) = Board(Formation.Build(front1: UnitCatalog.SidRC, front3: Mate, center: UnitCatalog.VioW), Formation.Build(center: Dummy));
            var v = p.First(u => u.Def.Id == "vio"); var s = p.First(u => u.Def.Id == "sid"); var m = p.First(u => u.Def.Id == "mate370");
            ctx.Poison(s, 10, e[0], PoisonRoute.Venom); ctx.Poison(m, 5, e[0], PoisonRoute.Venom);
            int atk0 = v.Def.Attack + v.AtkBonus;
            foreach (var t in v.Traits) t.OnTurnStart(ctx, v);
            int atk1 = v.Def.Attack + v.AtkBonus;
            bool drain = Psn(m) == 0 && Psn(s) == 10 && atk1 == atk0 + 5 * BlightfedTrait.GainPerStack;
            int f0 = Psn(e[0]);
            TraitCatalog.Get(TraitId.StrikePoison).OnAfterAttack(ctx, v, e[0], 0);
            bool strike = Psn(e[0]) == f0 + atk1;
            Expect("ヴィオ W: ターンの頭に吸って 1層 +4（毒の衣の持ち主からは吸わない）・殴った敵に攻撃力と同じだけ毒", drain && strike, $"攻撃力 {atk0} → {atk1}・仲間 {Psn(m)}・スィド {Psn(s)}・敵 +{Psn(e[0]) - f0}");
            // 軛のある波: 一撃のダメージは 25 で切られるが、乗せる毒は切られない（実際の攻撃で）
            var (ctx2, p2, e2) = Board(Formation.Build(center: UnitCatalog.VioW), Formation.Build(front1: Dummy, center: YokeHolder));
            var v2 = p2[0];
            v2.AtkBonus = 60;
            int hp0 = e2.Sum(u => u.Hp), ps0 = e2.Sum(Psn);
            ctx2.PerformAttack(v2);
            int lost = hp0 - e2.Sum(u => u.Hp), got = e2.Sum(Psn) - ps0;
            Expect("ヴィオ W: 軛のある波でも乗せる毒は切られない（攻撃力 66 で一撃は 25 に切られ、毒は 66）", lost == YokeTrait.Cap && got == v2.Def.Attack + 60, $"一撃 {lost}・毒 +{got}");
        }

        // ---- 器具の中だけ ／ verbose ／ 再現性 ----
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
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（6台 × 4版 × 勇者 ／ 近衛 ／ 蝕みなし・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
            bool noTax = !S366.BossNoTax.Make().Any(u => u.HasTrait(TraitId.VenomTaxSeal)) && EnemyCatalog.BossRegular.Traits.Contains(TraitId.VenomTaxSeal);
            bool cuts = BeniCuts().SelectMany(c => c.F.Occupied()).All(o => !UnitCatalog.All.Contains(o.Def) || o.Def.Traits.All(t => t is not (TraitId.VentOff370 or TraitId.BacklashOff370)))
                && !UnitCatalog.Everyone.Any(d => d.Traits.Any(t => t is TraitId.VentOff370 or TraitId.BacklashOff370)) && !CompareBuilds().Any(r => Has(r.F, "guza") && r.F.Occupied().Any(o => o.Def.Traits.Contains(TraitId.MiasmaVent)));
            Expect("蝕みなしの勇者 ／ ベニの台の切り分けは器具の中だけ（規定の勇者は蝕みを持つ・切り分けの札の保持者は器具の外に 0 枚）", noTax && cuts);
        }

        // ---- checkup の札の表に新しい札がある（第368期の足し漏れを受けて） ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var want = new[] { TraitId.PoisonProof, TraitId.PoisonCloak, TraitId.BlightfedSkipCloak, TraitId.MiasmaVent, TraitId.CatalystSip, TraitId.PoisonShoulder, TraitId.CloakRetort,
                               TraitId.MiasmaSplash, TraitId.SplashUncapped, TraitId.StrikePoison, TraitId.VentOff370, TraitId.BacklashOff370 };
            var missing = want.Where(t => !src.Contains($"[TraitId.{t}]", StringComparison.Ordinal)).ToList();
            Expect("`checkup` の札の表に第368〜370期の札がそろっている（ソースを読んで確かめる）", missing.Count == 0, missing.Count == 0 ? $"{want.Length} 枚" : "無い: " + string.Join("・", missing));
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "版・RCS 吸い分け", "版・W1 返り毒＋ヴィオ", "版・W2 三人", "版・W3 上限なし" });
            Expect("`Presets.Versions` ＝ 試遊の8台 ＋ 「版・RCS 吸い分け」＋ 「版・W1 ／ W2 ／ W3」（「版・Z1 ／ Z2 ／ Z3」は外した）", rows, string.Join("、", vr));
            if (rows)
            {
                Formation V(string n) => Presets.Versions.First(r => r.Name == n).F;
                var play = V("試遊・毒");
                bool ok = new[] { ("版・RCS 吸い分け", Vers[1]), ("版・W1 返り毒＋ヴィオ", Vers[2]), ("版・W2 三人", Vers[3]), ("版・W3 上限なし", Vers[4]) }.All(x =>
                {
                    var f = V(x.Item1); var want = Apply(play, x.Item2);
                    return Enumerable.Range(0, 5).All(i => f[i] == want[i]);
                });
                Expect("「版・」の4台 ＝ 試遊・毒の並びで版の駒だけ替えた台", ok, S307.Seats(play));
            }
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 22a634b8:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第369期（22a634b8）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
