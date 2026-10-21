using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;

// sh374 の自己検査（第374期・指示書 §7）。
static partial class Sh374Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static UnitTally Tal(BattleContext ctx, UnitState u) => (UnitTally)TallyOfM.Invoke(ctx, new object[] { u })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Mate = new() { Id = "mate374", Name = "仲間", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Spiky = new() { Id = "spiky374", Name = "棘の仲間", MaxHp = 999, Attack = 50, Speed = 1, Traits = new[] { TraitId.Thorns }, PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe374", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Weak = new() { Id = "weak374", Name = "弱い的", MaxHp = 1, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    static int Psn(UnitState u) => u.RawCounter(StatusKeys.Poison);
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation mine)
    {
        var ctx = new BattleContext(0, false);
        var p = BattleEngine.Materialize(mine, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(Formation.Build(center: Foe), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        return (ctx, p, e);
    }
    static void TurnHead(BattleContext ctx, UnitState v) { foreach (var t in v.Traits) t.OnTurnStart(ctx, v); }
    static void Open(BattleContext ctx, UnitState u) { foreach (var t in u.Traits) t.OnBattleStart(ctx, u); }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh374 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義 ／ 規定は今どおり ----
        {
            bool sid = new[] { (UnitCatalog.SidS10, TraitId.SidOpeningCloak10), (UnitCatalog.SidS20, TraitId.SidOpeningCloak20) }
                .All(x => x.Item1.Traits.SequenceEqual(UnitCatalog.SidH374.Traits.Append(x.Item2)) && x.Item1.Id == "sid" && x.Item1.PlusText == "戦いの前から毒をまとっている。" + UnitCatalog.SidH374.PlusText && x.Item1.MinusText == UnitCatalog.SidH374.MinusText);
            bool vio = UnitCatalog.VioC.Traits.SequenceEqual(UnitCatalog.VioH375.Traits.Append(TraitId.VioBloodSip)) && UnitCatalog.VioC.MinusText == "仲間が汚れていないターンは、飢えて隣の仲間の血を啜る" && UnitCatalog.VioC.PlusText == UnitCatalog.VioH375.PlusText;
            bool none = new[] { UnitCatalog.SidS10, UnitCatalog.SidS20, UnitCatalog.VioC }.All(d => !UnitCatalog.All.Contains(d))
                && !UnitCatalog.All.Any(d => d != UnitCatalog.Sid && d != UnitCatalog.Vio && d.Traits.Any(t => t is TraitId.SidOpeningCloak10 or TraitId.SidOpeningCloak20 or TraitId.VioBloodSip))
                && !CompareBuilds().Select(r => (r.Name, F: Pin374(r.F))).Any(r => r.F.Occupied().Any(o => o.Def.Traits.Any(t => t is TraitId.SidOpeningCloak10 or TraitId.SidOpeningCloak20 or TraitId.VioBloodSip)));
            bool def = UnitCatalog.SidH374.Traits.SequenceEqual(UnitCatalog.SidY.Traits)   /* 第375期: 規定は S10・第374期の規定は `SidH374` */ && UnitCatalog.VioH375.Traits.SequenceEqual(UnitCatalog.VioVa.Traits);
            int nr = Enum.GetValues<PoisonRoute>().Length;
            bool route = (int)PoisonRoute.CloakOpening == nr - 1 && BattleContext.SoakBurnRouteIx == nr && BattleContext.SoakRouteCount == nr + 1;
            Expect("版の定義: S10 ／ S20 ＝ 規定のスィド ＋ 開戦の衣（プラスの頭に一文）・C ＝ 規定のヴィオ ＋ 血を啜る（マイナスを差し替え）・`All` ／ `compare` の行には無い・規定のスィド ／ ヴィオは第373期のまま・経路 `CloakOpening` は末尾・燃焼の添字は後ろ",
                sid && vio && none && def && route, $"経路 {nr} 本・`SoakRouteCount` {BattleContext.SoakRouteCount}・燃焼の添字 {BattleContext.SoakBurnRouteIx}");
        }

        // ---- 開戦の衣 ----
        {
            string psn = StatusKeys.LabelOf(StatusKeys.Poison);
            int T1(UnitDef sid)
            {
                var r = BattleEngine.Run(Formation.Build(front1: sid), Formation.Build(center: Foe), 0, verbose: true);
                return r.Events.Where(e => e.Kind == BattleEventKind.StatusSnapshot && e.Turn == 1 && e.Text == psn).Select(e => e.Amount).DefaultIfEmpty(0).Max();
            }
            int a10 = T1(UnitCatalog.SidS10), a20 = T1(UnitCatalog.SidS20), a0 = T1(UnitCatalog.SidH374);
            var r10 = BattleEngine.Run(Formation.Build(front1: UnitCatalog.SidS10), Formation.Build(center: Foe), 0, verbose: true);
            bool noTick = !r10.Events.Any(e => e.Kind == BattleEventKind.Status && e.Text == psn && e.TargetId is int t && r10.Events.Any(o => o.Kind == BattleEventKind.StatusGain && o.PoisonRoute == PoisonRoute.CloakOpening && o.TargetId == t) && e.Amount > 0);
            Expect("開戦の衣: T1 の頭でスィドの層が 10 ／ 20（規定は 0）・毒の刻みでは削られない", a10 == 10 && a20 == 20 && a0 == 0 && noTick, $"S10 {a10}・S20 {a20}・規定 {a0}・刻み {(noTick ? "なし" : "あり")}");

            var (ctx, p, e) = Board(Formation.Build(front1: UnitCatalog.SidS10));
            var s = p[0];
            Open(ctx, s);
            int layers = Psn(s), hp0 = s.Hp, f0 = Psn(e[0]);
            ctx.ApplyDamage(s, 100, e[0]);
            int took = hp0 - s.Hp, ret = Psn(e[0]) - f0;
            Expect("開戦の衣: 衣の軽減と返しがその層を読む（10 層で 100 の一撃を 30% 減らして 70・殴ってきた敵へ +2 の後の 12 層）", layers == 10 && took == 70 && ret == 12, $"層 {layers}・受けた {took}・返し {ret}");

            var eng = EngagementEngine.Run(new[] { Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.SidS10) }, new[] { Formation.Build(center: Weak), Formation.Build(center: Weak) }, 0, verbose: true);
            var t1 = eng.Battles.Select(b => b.Events.Where(x => x.Kind == BattleEventKind.StatusSnapshot && x.Turn == 1 && x.Text == psn).Select(x => x.Amount).DefaultIfEmpty(0).Max()).ToList();
            Expect("開戦の衣: 会戦の次の戦でもまた付く（戦ごとの T1 の頭の層が 10）", eng.Battles.Count >= 2 && t1.All(v => v == 10), $"戦 {eng.Battles.Count}・T1 の層 [{string.Join(",", t1)}]");
        }

        // ---- 血を啜る ----
        int[] nb = Enumerable.Range(0, 5).Where(s => s != 3 && FormationRules.AreAdjacent(3, s)).OrderBy(s => s).ToArray();
        int far = Enumerable.Range(0, 5).First(s => s != 3 && !FormationRules.AreAdjacent(3, s));
        Formation Seat(UnitDef vio, UnitDef? a, UnitDef? b, UnitDef? c)
        {
            var f = new Formation(); f[3] = vio;
            if (a is not null) f[nb[0]] = a; if (b is not null) f[nb[1]] = b; if (c is not null) f[far] = c;
            return f;
        }
        {
            var (ctx, p, e) = Board(Seat(UnitCatalog.VioC, Mate, Mate, Mate));
            var v = p.First(u => u.Def.Id == "vio"); var n0 = p.First(u => u.Slot == nb[0]); var n1 = p.First(u => u.Slot == nb[1]); var fa = p.First(u => u.Slot == far);
            int a0 = v.AtkBonus;
            TurnHead(ctx, v);
            bool sip = v.AtkBonus - a0 == 16 && n0.Hp == n0.MaxHp - 4 && n1.Hp == n1.MaxHp - 4 && fa.Hp == fa.MaxHp && Tal(ctx, v).Z374SipTurns == 1 && Tal(ctx, v).Z374SipHp == 8;
            int g1 = v.AtkBonus;
            ctx.Poison(fa, 1, fa, PoisonRoute.Venom);
            TurnHead(ctx, v);
            bool skip = v.AtkBonus - g1 == 4 && n0.Hp == n0.MaxHp - 4 && Tal(ctx, v).Z374SipTurns == 1;
            Expect("血を啜る: 吸えた毒が 0 のターンに隣2体から HP 4 ずつ・攻撃力 +16・離れた味方からは啜らない・傷（最大HP − 今のHP）として残る ／ 1層でも吸えたら起きない（+4 だけ）", sip && skip,
                $"1回目 攻撃力 +16? {sip}・隣の傷 {n0.MaxHp - n0.Hp} ／ {n1.MaxHp - n1.Hp}・2回目 +{v.AtkBonus - g1}");
        }
        {
            var (ctx, p, e) = Board(Seat(UnitCatalog.VioC, Mate, Mate, null));
            var v = p.First(u => u.Def.Id == "vio"); var n0 = p.First(u => u.Slot == nb[0]); var n1 = p.First(u => u.Slot == nb[1]);
            n0.Hp = 3; n1.Hp = 1;
            int a0 = v.AtkBonus;
            TurnHead(ctx, v);
            Expect("血を啜る: HP 1 未満にしない（HP 3 からは 2・HP 1 からは 0・攻撃力 +4）", n0.Hp == 1 && n1.Hp == 1 && v.AtkBonus - a0 == 4, $"HP {n0.Hp} ／ {n1.Hp}・攻撃力 +{v.AtkBonus - a0}");
        }
        {
            var (ctx, p, e) = Board(Seat(UnitCatalog.VioC, UnitCatalog.Sid, Spiky, null));
            var v = p.First(u => u.Def.Id == "vio"); var sid = p.First(u => u.Def.Id == "sid"); var sp = p.First(u => u.Def.Id == "spiky374");
            ctx.Poison(sid, 6, sid, PoisonRoute.Venom);   // 衣の持ち主の毒は吸わない ＝ 吸えた毒は 0
            int sp0 = Psn(sid), f0 = Psn(e[0]), fh = e[0].Hp, a0 = v.AtkBonus;
            TurnHead(ctx, v);
            bool quiet = Psn(sid) == sp0 && Psn(e[0]) == f0 && e[0].Hp == fh && sid.Hp == sid.MaxHp - 4 && sp.Hp == sp.MaxHp - 4 && v.AtkBonus - a0 == 16;
            Expect("血を啜る: ダメージではない（衣の持ち主から啜っても衣が +2 されず返しも出ない・棘の仲間から啜っても反撃しない）・衣の持ち主の毒は吸わないので啜る", quiet,
                $"スィドの層 {sp0} → {Psn(sid)}・敵の毒 {f0} → {Psn(e[0])}・敵の HP {fh} → {e[0].Hp}");
        }
        {
            var (ctx, p, e) = Board(Seat(UnitCatalog.VioC, null, null, Mate));
            var v = p.First(u => u.Def.Id == "vio"); var fa = p.First(u => u.Slot == far);
            int a0 = v.AtkBonus;
            TurnHead(ctx, v);
            var (ctx2, p2, _) = Board(Seat(UnitCatalog.VioH375, Mate, Mate, null));
            var v2 = p2.First(u => u.Def.Id == "vio");
            int b0 = v2.AtkBonus;
            TurnHead(ctx2, v2);
            Expect("血を啜る: 隣がいなければ起きない ／ 規定のヴィオは啜らない", v.AtkBonus == a0 && fa.Hp == fa.MaxHp && v2.AtkBonus == b0 && p2.All(u => u.Hp == u.MaxHp), $"隣なし +{v.AtkBonus - a0}・規定 +{v2.AtkBonus - b0}");
        }

        // ---- 器具の中だけ ／ verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var bd in BoardList())
                foreach (var v in bd.Vs)
                    foreach (var w in new[] { I.Boss, I.Guard, S366.BossNoTax })
                        for (int sd = 0; sd < 3; sd++)
                        {
                            cells++;
                            var f = Apply(bd.F, v);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（6台 × 版 × 勇者 ／ 近衛 ／ 蝕みなし・seed 0..2）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
            bool noTax = !S366.BossNoTax.Make().Any(u => u.HasTrait(TraitId.VenomTaxSeal)) && EnemyCatalog.BossRegular.Traits.Contains(TraitId.VenomTaxSeal);
            bool wound = S307.Seats(WoundBoard()) == "アカ・スィド・グザ・ヴィオ・ガルド" && FormationRules.AreAdjacent(3, 0) && !FormationRules.AreAdjacent(3, 4);
            Expect("蝕みなしの勇者は器具の中だけ・傷の台（ドルガ → アカ・アカは前1 ＝ 後1 のヴィオの隣・後3 は隣ではないのでガルドを回した）", noTax && wound, S307.Seats(WoundBoard()));
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var missing = new[] { TraitId.SidOpeningCloak10, TraitId.SidOpeningCloak20, TraitId.VioBloodSip }.Where(t => !src.Contains($"[TraitId.{t}]", StringComparison.Ordinal)).ToList();
            Expect("`checkup` の札の表に開戦の衣 ／ 血を啜るの3枚がある（ソースを読んで確かめる）", missing.Count == 0, missing.Count == 0 ? "そろっている" : "無い: " + string.Join("・", missing));
        }

        // ---- 試遊の台 ----
        {
            // 第375期: 第374期の「版・S10 ／ S20 ／ C ／ S10C」は外した（S10 を規定にした・並びはこの器具の中で組む）。検査は「頭の10台」と「第374期の版の台が無い」に。
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.Take(10).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "試遊・毒 席替え", "試遊・毒 紅" })
                && !vr.Any(n => n is "版・S10 開戦の衣" or "版・S20 開戦の衣" or "版・C 血を啜る" or "版・S10C 両方");
            Expect("`Presets.Versions` の頭 ＝ 試遊の8台 ＋「試遊・毒 席替え」＋「試遊・毒 紅」・第374期の版の4台は無い（第375期に外した）", rows, string.Join("、", vr));
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 4cd98cce:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第373期（4cd98cce）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
