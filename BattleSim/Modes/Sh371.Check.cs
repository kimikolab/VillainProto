using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;
using S370 = Sh370Diag;

// sh371 の自己検査（第371期・指示書 §6）。
static partial class Sh371Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Dummy = new() { Id = "dummy371", Name = "的", MaxHp = 999, Attack = 50, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Mate = new() { Id = "mate371", Name = "仲間", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

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
        Console.WriteLine($"# {Tag} sh371 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義 ／ 規定は今どおり ----
        {
            bool def = UnitCatalog.Guza.Traits.SequenceEqual(new[] { TraitId.Miasma, TraitId.ScatterGather }) && UnitCatalog.Vio.Traits.SequenceEqual(new[] { TraitId.Blightfed, TraitId.BlightCatalyst })
                && UnitCatalog.Sid.Traits.SequenceEqual(new[] { TraitId.Spew, TraitId.VenomHeavy, TraitId.Numb });
            bool y = UnitCatalog.SidY.Traits.SequenceEqual(new[] { TraitId.Spew, TraitId.PoisonProof, TraitId.PoisonCloak, TraitId.CloakRetort, TraitId.Numb })
                && UnitCatalog.SidY.Actions is { Count: 1 } a && a[0].Kind == ActionKind.Skill && UnitCatalog.SidY.Id == UnitCatalog.Sid.Id && UnitCatalog.SidY.MaxHp == UnitCatalog.Sid.MaxHp
                && !UnitCatalog.SidY.Traits.Any(t => t is TraitId.PoisonShoulder or TraitId.VenomHeavy) && !UnitCatalog.All.Contains(UnitCatalog.SidY);
            bool w = Vers[3].Guza == UnitCatalog.GuzaW3 && Vers[3].Vio == UnitCatalog.VioW && Vers[3].Sid == UnitCatalog.SidR && Vers[4].Guza == UnitCatalog.GuzaW3 && Vers[4].Sid == UnitCatalog.SidY;
            Expect("規定のグザ ／ ヴィオ ／ スィドは今どおり・スィド Y の札（吐く ＋ 毒に強い ＋ 衣 ＋ 返し ＋ 痺れ毒・引き受け ／ 毒撃 +8 なし・手番は `Skill`）・W4 ＝ グザ W ＋ 上限なし ＋ ヴィオ W ＋ スィド R・W5 ＝ W4 のスィドを Y に", def && y && w);
        }

        // ---- W4: 上限なし ----
        {
            var (ctx, p, e) = Board(Formation.Build(front1: Mate, center: UnitCatalog.GuzaW3), Formation.Build(center: Dummy));
            ctx.Poison(e[0], 200, p[0], PoisonRoute.Venom);
            int f0 = Psn(e[0]);
            ctx.ApplyDamage(p[0], 5, e[0]);
            Expect("W4: 返り毒に上限が無い（毒 200 の敵で +20・敵の層は減らない）", Psn(p[0]) == 20 && Psn(e[0]) == f0, $"+{Psn(p[0])}・敵 {f0} → {Psn(e[0])}");
        }

        // ---- スィド Y ----
        {
            var (ctx, p, e) = Board(Formation.Build(front1: Mate, front3: Mate, center: UnitCatalog.SidY), Formation.Build(front1: Dummy, front3: Dummy));
            var s = p.First(u => u.Def.Id == "sid");
            var adj = p.Where(u => u != s && FormationRules.AreAdjacent(s, u)).ToList();
            foreach (var m in adj) ctx.Poison(m, 4, e[0], PoisonRoute.Venom);
            int[] f0 = e.Select(Psn).ToArray();
            foreach (var t in s.Traits) t.OnAction(ctx, s, s.Def.Actions![0]);
            int[] f1 = e.Select(Psn).ToArray();
            bool spew = f1.Zip(f0, (x, y) => x - y).Sum() == SpewTrait.SpitStack && adj.All(m => Psn(m) == 4) && Psn(s) == 0;
            ctx.Poison(s, 10, e[0], PoisonRoute.Venom);
            int src0 = Psn(e[1]);
            ctx.ApplyDamage(s, 10, e[1]);
            bool retort = Psn(s) == 12 && Psn(e[1]) == src0 + 12;
            Expect("スィド Y: 手番で吐く（敵に +6）・隣の毒を引き受けない・殴られると +2 の後に層と同じだけ浴びせる（衣は減らない）・毒撃 +8 は起きない", spew && retort && adj.Count > 0,
                $"吐いた +{f1.Zip(f0, (x, y) => x - y).Sum()}・隣 [{string.Join(",", adj.Select(Psn))}]・殴られた後 自分 {Psn(s)} ／ 殴った敵 +{Psn(e[1]) - src0}");
        }

        // ---- 器具の中だけ ／ verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in BoardList())
                foreach (var v in Vers.Skip(3))
                    foreach (var w in new[] { I.Boss, I.Guard, S366.BossNoTax })
                        for (int sd = 0; sd < 5; sd++)
                        {
                            cells++;
                            var f = Apply(b.F, v);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（5台 × W4 ／ W5 × 勇者 ／ 近衛 ／ 蝕みなし・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
            bool noTax = !S366.BossNoTax.Make().Any(u => u.HasTrait(TraitId.VenomTaxSeal)) && EnemyCatalog.BossRegular.Traits.Contains(TraitId.VenomTaxSeal);
            bool seats = Perms().Count() == 120 && Perms().Select(p => string.Join(",", p)).Distinct().Count() == 120 && !CompareBuilds().Any(r => r.F.Occupied().Any(o => o.Def == UnitCatalog.SidY));
            Expect("蝕みなしの勇者 ／ 席の並べ替え（120 通り・重なりなし）は器具の中だけ", noTax && seats);
        }

        // ---- checkup の札の表（新しい札はこの期は無い・スィド Y の札がそろっている） ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var missing = UnitCatalog.SidY.Traits.Concat(new[] { TraitId.MiasmaSplash, TraitId.SplashUncapped, TraitId.StrikePoison }).Where(t => !src.Contains($"[TraitId.{t}]", StringComparison.Ordinal)).ToList();
            Expect("`checkup` の札の表に W4 ／ W5 の駒の札がそろっている（この期に足した札は無い・ソースを読んで確かめる）", missing.Count == 0, missing.Count == 0 ? "そろっている" : "無い: " + string.Join("・", missing));
        }

        // ---- 試遊の台（席替えの台は席の確かめの1位） ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "版・RCS 吸い分け", "版・W4 ヴィオ型", "版・W5 両立", "版・W2 席替え", "版・W5 席替え" });
            Expect("`Presets.Versions` ＝ 試遊の8台 ＋ 「版・RCS 吸い分け」＋ 「版・W4 ／ W5 ／ W2 席替え ／ W5 席替え」（「版・W1 ／ W2 ／ W3」は外した）", rows, string.Join("、", vr));
            if (rows)
            {
                Formation V(string n) => Presets.Versions.First(r => r.Name == n).F;
                var play = V("試遊・毒");
                bool same(Formation a, Formation b) => Enumerable.Range(0, 5).All(i => a[i] == b[i]);
                bool ok = same(V("版・W4 ヴィオ型"), Apply(play, Vers[3])) && same(V("版・W5 両立"), Apply(play, Vers[4]));
                // 席替えの台 ＝ 席の確かめの1位（蝕みなし → 倒しT → ボス → 倒しT の順・seed 0..19）を組み直して比べる
                var perms = Perms().ToArray();
                double TOf(S370.Agg a) => a.Wins == 0 ? 99 : (double)a.WinT / a.Wins;
                int Top(S370.Ver v)
                {
                    var r = new S370.Agg[perms.Length, 2];
                    Parallel.For(0, perms.Length * 2, k => r[k / 2, k % 2] = S370.Measure(Seat(perms[k / 2], v), k % 2 == 0 ? I.Boss : S366.BossNoTax, SeatSeeds));
                    return Enumerable.Range(0, perms.Length).OrderByDescending(pi => r[pi, 1].Win).ThenBy(pi => TOf(r[pi, 1])).ThenByDescending(pi => r[pi, 0].Win).ThenBy(pi => TOf(r[pi, 0])).First();
                }
                int t2 = Top(Vers[2]), t5 = Top(Vers[4]);
                bool seat = same(V("版・W2 席替え"), Seat(perms[t2], Vers[2])) && same(V("版・W5 席替え"), Seat(perms[t5], Vers[4]));
                Expect("「版・W4 ／ W5」＝ 試遊・毒の並びで版の駒だけ替えた台・「版・W2 ／ W5 席替え」＝ 席の確かめの1位（組み直して同じ）", ok && seat,
                    $"W2 の1位 {S307.Seats(Seat(perms[t2], Vers[0]))}・W5 の1位 {S307.Seats(Seat(perms[t5], Vers[0]))}");
            }
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show f039b16d:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第370期（f039b16d）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
