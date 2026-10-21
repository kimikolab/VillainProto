using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;
using S371 = Sh371Diag;

// sh373 の自己検査（第373期・指示書 §7）。
static partial class Sh373Diag
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

    static readonly UnitDef Mate = new() { Id = "mate373", Name = "仲間", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe373", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    static int Psn(UnitState u) => u.RawCounter(StatusKeys.Poison);
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;

    /// <summary>後1 にヴィオ、<paramref name="seats"/> に味方を置いた盤。返すのは文脈・ヴィオ・席 → 駒。</summary>
    static (BattleContext Ctx, UnitState V, Dictionary<int, UnitState> At) Board(UnitDef vio, params (int Slot, UnitDef D)[] seats)
    {
        var ctx = new BattleContext(0, false);
        var f = new Formation();
        f[3] = vio;
        foreach (var (s, d) in seats) f[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(Formation.Build(center: Foe), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        return (ctx, p.First(u => u.Def.Id == "vio"), p.Where(u => u.Def.Id != "vio").ToDictionary(u => u.Slot));
    }
    static void TurnHead(BattleContext ctx, UnitState v) { foreach (var t in v.Traits) t.OnTurnStart(ctx, v); }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh373 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            var v = UnitCatalog.VioH375; var h = UnitCatalog.VioH372;
            bool cards = v.Traits.SequenceEqual(h.Traits.Append(TraitId.VioFlurryA)) && v.Traits.SequenceEqual(UnitCatalog.VioVa.Traits) && v.PlusText == UnitCatalog.VioVa.PlusText && v.MinusText == UnitCatalog.VioVa.MinusText
                && v.Id == h.Id && v.MaxHp == h.MaxHp && v.Attack == h.Attack && !UnitCatalog.All.Contains(v) && UnitCatalog.All.Contains(UnitCatalog.Vio) && !UnitCatalog.All.Contains(h)   /* 第376期: Va は `VioH375`（`All` の外） */ && UnitCatalog.All.Count == 52
                && h.Traits.SequenceEqual(UnitCatalog.VioW.Traits);
            bool pins = PinV372(v) == h && PinV372(UnitCatalog.Gald) == UnitCatalog.Gald && PinGVH367(h) == UnitCatalog.VioH367 && PinGVH367(v) == UnitCatalog.VioH367 && PinGVS371(h) == UnitCatalog.VioH371 && PinGVS371(v) == UnitCatalog.VioH371
                && Pin372(Presets.PoisonHero367()).Occupied().Select(o => o.Def).SequenceEqual(Presets.PoisonHero367(vio: h, sid: UnitCatalog.SidH374).Occupied().Select(o => o.Def));   // 第375期: `Pin372` はスィドも第374期までの規定へ
            Expect("段0: 規定のヴィオ ＝ 第372期の Va（`VioH372` ＋ 連撃・散らす）・文面は Va の文面・旧の定義 `VioH372`（`All` の外 ＝ 第372期の W5）・固定の口（`Pin372` ／ `PinGVH367` ／ `PinGVS371` が旧へ）", cards && pins);
        }
        {
            long diff = 0, cells = 0;
            var pairs = new (Formation Now, Formation Va)[]
            {
                (Pin375(Presets.Versions.First(r => r.Name == "試遊・毒").F), Presets.PoisonHero367(vio: UnitCatalog.VioVa)),   // 第376期: 第375期までの規定のヴィオに固定
                (Pin375(Presets.Versions.First(r => r.Name == "試遊・毒 席替え").F), Presets.PoisonSeat372(vio: UnitCatalog.VioVa)),
            };
            foreach (var (now, va) in pairs)
                foreach (var w in Waves)
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(now, w, sd).R, I.Raw(va, w, sd).R)) diff++;
                    }
            Expect("段0: 規定の台（試遊・毒 ／ 試遊・毒 席替え）が第372期の Va の台と勝敗 ／ T ／ 生存数が同じ（6 波 × seed 0..9）", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- 吐き戻し ----
        int[] nb = Enumerable.Range(0, 5).Where(s => s != 3 && FormationRules.AreAdjacent(3, s)).OrderBy(s => s).ToArray();
        int far = Enumerable.Range(0, 5).First(s => s != 3 && !FormationRules.AreAdjacent(3, s));
        {
            var (ctx, v, at) = Board(UnitCatalog.VioA1, (far, Mate), (nb[0], Mate), (nb[1], Mate));
            ctx.Poison(at[far], 10, at[far], PoisonRoute.Venom);
            int drawn = Psn(at[far]);
            int a0 = v.AtkBonus;
            TurnHead(ctx, v);
            int gain = v.AtkBonus - a0, pend = v.RawCounter(VioRetchTrait.PendingKey);
            int b0 = Psn(at[nb[0]]), b1 = Psn(at[nb[1]]);
            ctx.VioRetchTurnEnd();
            int g0 = Psn(at[nb[0]]) - b0, g1 = Psn(at[nb[1]]) - b1;
            Expect($"A1: 10 層吸うと攻撃力 +20・残り 5 層をターンの終わりに隣2体へ 3 ／ 2（席番号の小さい方に余り・隣は席 {string.Join(",", nb)}）・離れた味方へは吐かない",
                drawn == 10 && gain == 20 && pend == 5 && g0 == 3 && g1 == 2 && Psn(at[far]) == 0 && v.RawCounter(VioRetchTrait.PendingKey) == 0,
                $"吸った {drawn}・攻撃力 +{gain}・控え {pend}・隣 +{g0} ／ +{g1}・離れた味方 {Psn(at[far])}");
        }
        {
            var (ctx, v, at) = Board(UnitCatalog.VioA2, (far, Mate), (nb[0], Mate), (nb[1], Mate));
            ctx.Poison(at[far], 10, at[far], PoisonRoute.Venom);
            int a0 = v.AtkBonus;
            TurnHead(ctx, v);
            int gain = v.AtkBonus - a0, pend = v.RawCounter(VioRetchTrait.PendingKey);
            ctx.VioRetchTurnEnd();
            Expect("A2: 10 層吸うと 7 層が収まり（攻撃力 +28）3 層を吐く（隣2体に 2 ／ 1）", gain == 28 && pend == 3 && Psn(at[nb[0]]) == 2 && Psn(at[nb[1]]) == 1, $"攻撃力 +{gain}・控え {pend}・隣 {Psn(at[nb[0]])} ／ {Psn(at[nb[1]])}");
        }
        {
            var (ctx, v, at) = Board(UnitCatalog.VioA1, (far, Mate), (nb[0], UnitCatalog.Sid));
            ctx.Poison(at[far], 10, at[far], PoisonRoute.Venom);
            TurnHead(ctx, v);
            int s0 = Psn(at[nb[0]]);
            ctx.VioRetchTurnEnd();
            Expect("A1: 隣が毒の衣の持ち主（スィド）でも吐く（隣1体に 5 層）", Psn(at[nb[0]]) - s0 == 5, $"スィド +{Psn(at[nb[0]]) - s0}");
        }
        {
            var (ctx, v, at) = Board(UnitCatalog.VioA1, (far, Mate));
            ctx.Poison(at[far], 10, at[far], PoisonRoute.Venom);
            TurnHead(ctx, v);
            ctx.VioRetchTurnEnd();
            var t = Tal(ctx, v);
            bool lost = t.Z373RetchLost == 5 && t.Z373RetchOut == 0 && Psn(at[far]) == 0 && v.RawCounter(VioRetchTrait.PendingKey) == 0;
            var (ctx2, v2, at2) = Board(UnitCatalog.VioA1, (far, Mate), (nb[0], Mate));
            ctx2.Poison(at2[far], 10, at2[far], PoisonRoute.Venom);
            TurnHead(ctx2, v2);
            v2.Hp = 0;
            ctx2.VioRetchTurnEnd();
            var t2 = Tal(ctx2, v2);
            bool dead = t2.Z373RetchDead == 5 && Psn(at2[nb[0]]) == 0 && t2.Z373RetchOut == 0;
            Expect("A1: 隣がいなければ吐かない（5 層は消える）・ヴィオが倒れていたら吐かない", lost && dead, $"隣なし: 消えた {t.Z373RetchLost} ／ 倒れた: 吐かなかった {t2.Z373RetchDead}・隣 {Psn(at2[nb[0]])}");
        }
        {
            var (ctx, v, at) = Board(UnitCatalog.VioH375, (far, Mate), (nb[0], Mate));
            ctx.Poison(at[far], 10, at[far], PoisonRoute.Venom);
            int a0 = v.AtkBonus;
            TurnHead(ctx, v);
            ctx.VioRetchTurnEnd();
            Expect("規定のヴィオは今どおり（10 層吸って攻撃力 +40・吐かない）", v.AtkBonus - a0 == 40 && v.RawCounter(VioRetchTrait.PendingKey) == 0 && Psn(at[nb[0]]) == 0, $"攻撃力 +{v.AtkBonus - a0}・隣 {Psn(at[nb[0]])}");
        }

        // ---- 版の定義 ／ 経路 ----
        {
            bool vers = new[] { (UnitCatalog.VioA1, TraitId.VioRetchHalf), (UnitCatalog.VioA2, TraitId.VioRetchThird) }
                .All(x => x.Item1.Traits.SequenceEqual(UnitCatalog.VioH375.Traits.Append(x.Item2)) && x.Item1.Id == UnitCatalog.VioH375.Id && !UnitCatalog.All.Contains(x.Item1) && x.Item1.PlusText == UnitCatalog.VioH375.PlusText && x.Item1.MinusText.Contains("吐き戻す"));
            bool none = !UnitCatalog.All.Any(d => d.Traits.Any(t => t is TraitId.VioRetchHalf or TraitId.VioRetchThird))
                && !CompareBuilds().Any(r => r.F.Occupied().Any(o => o.Def.Traits.Any(t => t is TraitId.VioRetchHalf or TraitId.VioRetchThird)));
            int nr = Enum.GetValues<PoisonRoute>().Length;
            bool route = (int)PoisonRoute.Retch == 18 && BattleContext.SoakBurnRouteIx == nr && BattleContext.SoakRouteCount == nr + 1;   // 第374期: 開戦の衣（19）が後ろに足された
            Expect("版 A1 ／ A2 ＝ 規定のヴィオ ＋ 札1枚（マイナスの文面を差し替え・`All` ／ `compare` の行には無い）・経路 `Retch` は添字 18・燃焼の添字は経路の後ろ", vers && none && route,
                $"経路 {nr} 本・`SoakRouteCount` {BattleContext.SoakRouteCount}・燃焼の添字 {BattleContext.SoakBurnRouteIx}");
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
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（4台 × 3版 × 勇者 ／ 近衛 ／ 蝕みなし・seed 0..2）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
            bool noTax = !S366.BossNoTax.Make().Any(u => u.HasTrait(TraitId.VenomTaxSeal)) && EnemyCatalog.BossRegular.Traits.Contains(TraitId.VenomTaxSeal);
            bool seats = S371.Perms().Count() == 120 && S371.Perms().Select(p => string.Join(",", p)).Distinct().Count() == 120;
            Expect("蝕みなしの勇者 ／ 席の並べ替え（120 通り・重なりなし）は器具の中だけ", noTax && seats);
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var missing = new[] { TraitId.VioRetchHalf, TraitId.VioRetchThird }.Concat(UnitCatalog.VioH375.Traits).Where(t => !src.Contains($"[TraitId.{t}]", StringComparison.Ordinal)).ToList();
            bool va = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.VioFlurryA\][^\n]*第373期から規定");
            Expect("`checkup` の札の表に吐き戻しの2枚と規定のヴィオの札がそろい、連撃・散らすは「第373期から規定」（ソースを読んで確かめる）", missing.Count == 0 && va, missing.Count == 0 ? "そろっている" : "無い: " + string.Join("・", missing));
        }

        // ---- 試遊の台 ----
        {
            // 第374期: 第373期の「版・A1 ／ A2 ／ A1 席替え」は外した（採らない・並びはこの器具の中で組む）。検査は「頭の10台」と「第373期の版の台が無い」に。
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.Take(10).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "試遊・毒 席替え", "試遊・毒 紅" })
                && !vr.Any(n => n is "版・Va 散らす" or "版・Vb 高い段" or "版・Vc 集中" or "版・A1 半分を吐く" or "版・A2 3分の1を吐く" or "版・A1 席替え");
            string guren = S307.Seats(Presets.Versions.First(r => r.Name == "試遊・毒 紅").F);
            Expect("`Presets.Versions` の頭 ＝ 試遊の8台 ＋「試遊・毒 席替え」＋「試遊・毒 紅」（ボルグ・スィド・ベニ・ラウ・グザ）・第372 ／ 373期の版の台は無い（第374期に外した）", rows && guren == "ボルグ・スィド・ベニ・ラウ・グザ", $"{string.Join("、", vr)}・紅 {guren}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show ecf922a9:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第372期（ecf922a9）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
