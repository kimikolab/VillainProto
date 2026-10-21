using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;
using X = Sh365Diag;

// sh367 の自己検査（第367期・指示書 §8）。
static partial class Sh367Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    /// <summary>器具の中だけの的（体だけ大きい・札なし）と軛の保持者。</summary>
    static readonly UnitDef Dummy = new() { Id = "dummy367", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef YokeHolder = new() { Id = "yoke367", Name = "軛の保持者", MaxHp = 999, Attack = 1, Speed = 1, Traits = new[] { TraitId.Yoke }, PlusText = "" };

    /// <summary>盤を作る（味方 ／ 敵の写しを reflection で `Add` する・戦は回さない）。</summary>
    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation mine, Formation foe, YokeRule? yoke = null)
    {
        var ctx = new BattleContext(0, false, yoke: yoke);
        var p = BattleEngine.Materialize(mine, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(foe, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        return (ctx, p, e);
    }
    static void Act(BattleContext ctx, UnitState u, TraitId id) => TraitCatalog.Get(id).OnAction(ctx, u, u.Def.Actions?.FirstOrDefault() ?? new UnitAction(ActionKind.Attack));
    static int Psn(UnitState u) => u.RawCounter(StatusKeys.Poison);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh367 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        // 第368期: この期の「規定」＝ 第367期までの規定（`GuzaH367` ／ `VioH367` ／ `HisaH367`）。G ／ V ／ H1 は第368期に規定になった。
        var guza = UnitCatalog.GuzaH367; var vio = UnitCatalog.VioH367; var hisa = UnitCatalog.HisaH367;
        var newCards = new[] { TraitId.BlightCatalyst, TraitId.RallyFeatherQuiet };

        // ---- 版の定義 ----
        {
            bool g = UnitCatalog.GuzaG.Traits.SequenceEqual(guza.Traits.Append(TraitId.ScatterGather)) && UnitCatalog.GuzaG.Id == guza.Id && UnitCatalog.GuzaG.MaxHp == guza.MaxHp && UnitCatalog.GuzaG.Attack == guza.Attack && UnitCatalog.GuzaG.Speed == guza.Speed;
            bool v = UnitCatalog.VioV.Traits.SequenceEqual(new[] { TraitId.Blightfed, TraitId.BlightCatalyst }) && UnitCatalog.VioV.Id == vio.Id && UnitCatalog.VioV.MaxHp == vio.MaxHp && UnitCatalog.VioV.Attack == vio.Attack && UnitCatalog.VioV.Speed == vio.Speed
                && UnitCatalog.VioV.Actions is { Count: 1 } va && va[0].Kind == ActionKind.Skill;
            bool h = UnitCatalog.HisaY1.Traits.SequenceEqual(hisa.Traits.Append(TraitId.RallyFeatherQuiet)) && UnitCatalog.HisaY1.Id == hisa.Id && UnitCatalog.HisaY1.MaxHp == hisa.MaxHp
                && UnitCatalog.HisaY1.PlusText.Split("（羽の一撃では叫ばない）").Length == 2 && UnitCatalog.HisaY1.PlusText.Contains(UnitCatalog.HisaShoutClause367 + "（羽の一撃では叫ばない）", StringComparison.Ordinal);
            // 第368期: 3つとも規定になった——規定の駒（`All`）は版と同じ札 ／ 文面、版の定義そのものは `All` に入れないまま。
            bool notAll = new[] { UnitCatalog.GuzaG, UnitCatalog.VioV, UnitCatalog.HisaY1 }.All(d => !UnitCatalog.All.Contains(d))
                && new[] { (UnitCatalog.Guza, UnitCatalog.GuzaG), (UnitCatalog.Vio, UnitCatalog.VioV), (UnitCatalog.Hisa, UnitCatalog.HisaY1) }.All(x => UnitCatalog.All.Contains(x.Item1) && x.Item1.Traits.SequenceEqual(x.Item2.Traits) && x.Item1.PlusText == x.Item2.PlusText && x.Item1.MinusText == x.Item2.MinusText)
                && UnitCatalog.All.Where(d => d.Traits.Any(t => newCards.Contains(t))).Select(d => d.Id).OrderBy(x => x).SequenceEqual(new[] { "hisa", "vio" });
            Expect("版の定義: G ＝ 第367期までのグザ ＋ `ScatterGather` ／ V ＝ 吸い ＋ 触媒（吐き戻しを外す・手番は `Skill` 1つ）／ H1 ＝ 第367期までのヒサ ＋ `RallyFeatherQuiet`（文面は叫びの句の後に括弧書き1つ）・体 ／ Id は同じ・版の定義は `All` に入れない（第368期: 規定の駒が版と同じ札 ／ 文面・新しい札の保持者はヴィオ ／ ヒサ）", g && v && h && notAll);
        }

        // ---- 規定は今どおり ----
        {
            bool d = guza.Traits.SequenceEqual(new[] { TraitId.Miasma }) && vio.Traits.SequenceEqual(new[] { TraitId.Blightfed, TraitId.Spit }) && vio.Actions is null
                && hisa.Traits.SequenceEqual(UnitCatalog.HisaH337.Traits.Append(TraitId.MarkInheritSpread)) && !UnitCatalog.All.Contains(guza) && !UnitCatalog.All.Contains(vio) && !UnitCatalog.All.Contains(hisa);
            bool y = !YokeRule.Default.SparesTicks && YokeRule.Default.Cap == YokeTrait.Cap && YokeRule.Default.Active && YokeRule.Default.ToString() == "YokeRule { Cap = 25, Active = True }"
                && YokeY.ToString() == "YokeRule { Cap = 25, Active = True, SparesTicks = True }";
            Expect("第367期までのグザ ／ ヴィオ ／ ヒサ（`*H367`・第368期から `All` の外）／ 軛は今どおり（札・既定の軛は刻みも切る・既定の文字列は `docs/rules.md` の既定値の列と同じ）", d && y, YokeRule.Default.ToString() + " ／ Y: " + YokeY.ToString());
        }

        // ---- G（直に） ----
        {
            int[] Spread(UnitDef g, int nFoes)
            {
                var foe = nFoes == 1 ? Formation.Build(center: Dummy) : Formation.Build(front1: Dummy, front3: Dummy, center: Dummy, back1: Dummy, back3: Dummy);
                var (ctx, p, e) = Board(Formation.Build(front1: UnitCatalog.Gald, center: g), foe);
                Act(ctx, p.First(u => u.Def.Id == "guza"), TraitId.Miasma);
                return e.Select(Psn).Concat(new[] { -1 }).Concat(p.Select(Psn)).ToArray();
            }
            var g1 = Spread(UnitCatalog.GuzaG, 1); var g5 = Spread(UnitCatalog.GuzaG, 5); var d1 = Spread(guza, 1); var d5 = Spread(guza, 5);
            bool ok = g1.SequenceEqual(new[] { 10, -1, 1, 1 }) && g5.SequenceEqual(new[] { 2, 2, 2, 2, 2, -1, 1, 1 }) && d1.SequenceEqual(new[] { 2, -1, 1, 1 }) && d5.SequenceEqual(d5.Length == 8 ? new[] { 2, 2, 2, 2, 2, -1, 1, 1 } : d5);
            Expect("G: 敵1体で瘴気が 10 層 ／ 敵5体で 2 層ずつ ／ 味方への漏れは規定と同じ（+1）・規定のグザは敵1体でも 2 層", ok,
                $"G 敵1 [{string.Join(",", g1)}]・G 敵5 [{string.Join(",", g5)}]・規定 敵1 [{string.Join(",", d1)}]（-1 の後ろは味方）");
        }

        // ---- V（直に） ----
        {
            (int[] Foes, int Belly) Cat(int[] layers, int belly)
            {
                var foe = Formation.Build(front1: Dummy, front3: Dummy, center: Dummy);
                var (ctx, p, e) = Board(Formation.Build(front1: UnitCatalog.Gald, back1: UnitCatalog.VioV), foe);
                var v = p.First(u => u.Def.Id == "vio");
                for (int i = 0; i < layers.Length; i++) if (layers[i] > 0) ctx.Poison(e[i], layers[i], p[0], PoisonRoute.Venom);
                v.SetCounter(SpitTrait.BellyKey, belly);
                Act(ctx, v, TraitId.BlightCatalyst);
                return (e.Select(Psn).ToArray(), SpitTrait.BellyOf(v));
            }
            var a = Cat(new[] { 3, 8, 5 }, 4); var t = Cat(new[] { 6, 6, 0 }, 2); var n = Cat(new[] { 0, 0, 0 }, 5); var z = Cat(new[] { 0, 0, 0 }, 0); var e0 = Cat(new[] { 3, 8, 5 }, 0);
            Expect("V: 最も深い敵が倍になり腹の分が足される（3 ／ 8 ／ 5・腹 4 → 3 ／ 20 ／ 5）・腹は空になる", a.Foes.SequenceEqual(new[] { 3, 20, 5 }) && a.Belly == 0 && e0.Foes.SequenceEqual(new[] { 3, 16, 5 }), $"[{string.Join(",", a.Foes)}]・腹 {a.Belly}・腹 0 のとき [{string.Join(",", e0.Foes)}]");
            Expect("V: 同じ層なら席番号の小さい方（6 ／ 6 ／ 0・腹 2 → 14 ／ 6 ／ 0）", t.Foes.SequenceEqual(new[] { 14, 6, 0 }) && t.Belly == 0, $"[{string.Join(",", t.Foes)}]");
            Expect("V: 毒の敵がいないときは腹の分だけを席番号の最も小さい敵へ（腹 5 → 5 ／ 0 ／ 0）・腹も無ければ何もしない", n.Foes.SequenceEqual(new[] { 5, 0, 0 }) && n.Belly == 0 && z.Foes.All(x => x == 0), $"[{string.Join(",", n.Foes)}]・空 [{string.Join(",", z.Foes)}]");
        }

        // ---- Y（直に） ----
        {
            (int Tick, int Hit) Hp(YokeRule? y, bool burn)
            {
                var (ctx, p, e) = Board(Formation.Build(center: UnitCatalog.Gald), Formation.Build(front1: Dummy, center: YokeHolder), y);
                var d = e[0];
                if (burn) ctx.Ignite(d); else ctx.Poison(d, 40, p[0], PoisonRoute.Venom);
                int h0 = d.Hp; ctx.TickStatuses(); int tick = h0 - d.Hp;
                int h1 = d.Hp; ctx.ApplyDamage(d, 40, p[0]); int hit = h1 - d.Hp;
                return (tick, hit);
            }
            var d0 = Hp(null, false); var y0 = Hp(YokeY, false);
            var db = Hp(new YokeRule(5, true), true); var yb = Hp(new YokeRule(5, true) { SparesTicks = true }, true);
            Expect("Y: 毒の刻みは 25 を超えて通る（40）／ 刻み以外の一撃は今どおり 25 で切る ／ 既定の軛は刻みも 25 で切る", y0.Tick == 40 && y0.Hit == 25 && d0.Tick == 25 && d0.Hit == 25, $"既定 刻み {d0.Tick}・一撃 {d0.Hit} ／ Y 刻み {y0.Tick}・一撃 {y0.Hit}");
            // 燃えている的への一撃は被弾の燃焼（刻みの1回・`BurnTickOnce`）も起こすので、一撃の側は「一撃 5 ＋ 被弾の燃焼」になる。基準は軛を外した盤（`Active = false`）の刻み。
            var fb = Hp(new YokeRule(5, false), true);
            Expect($"Y: 燃焼の刻みも切らない（上限 5 の軛で燃焼の刻み {fb.Tick}: 既定 5 ／ Y {fb.Tick}・一撃の後の被弾の燃焼も同じ）", fb.Tick > 5 && db.Tick == 5 && yb.Tick == fb.Tick && db.Hit == 5 + 5 && yb.Hit == 5 + fb.Tick,
                $"刻み 軛なし {fb.Tick}・既定 {db.Tick} ／ Y {yb.Tick}・一撃（＋被弾の燃焼）既定 {db.Hit} ／ Y {yb.Hit}");
        }

        // ---- H1（戦の中で） ----
        {
            var fB = Presets.HisaBoard366(hisa);
            int same = 0, n = 0; long quiet = 0, defQuiet = 0, featherRally = 0, otherRally = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                n++;
                var (rh, _, _) = I.Raw(H1(fB), I.Boss, seed);
                var (rc, _, _) = X.Raw(fB, I.Boss, seed, 1, false, verbose: false);
                if (rh.PlayerWon == rc.PlayerWon && rh.Turns == rc.Turns && rh.PlayerSurvivors == rc.PlayerSurvivors) same++;
                if (rh.TallyByUnit.TryGetValue("hisa", out var ht)) quiet += ht.Rally367FeatherQuiet;
                var (rd, _, _) = I.Raw(fB, I.Boss, seed);
                if (rd.TallyByUnit.TryGetValue("hisa", out var dt)) defQuiet += dt.Rally367FeatherQuiet;
            }
            foreach (var bd in S366.BoardList())
                for (int seed = 0; seed < 5; seed++)
                {
                    var a = S366.One(H1(bd.F), I.Boss, seed, true);
                    for (int t = 0; t < BattleContext.Ch365.Width; t++) { featherRally += a.F(true, BattleContext.Ch365.RallyFeather, t); otherRally += a.F(true, BattleContext.Ch365.Rally, t) - a.F(true, BattleContext.Ch365.RallyFeather, t); }
                }
            Expect("H1: 羽の一撃で叫ばない（台 B × 勇者が第365期の切り 1 と全戦同じ・seed 0..19）／ 黙った回数がある ／ 規定のヒサは黙らない", same == n && quiet > 0 && defQuiet == 0, $"{same} ／ {n} 戦が切り 1 と同じ・黙った {quiet}・規定 {defQuiet}");
            Expect("H1: 羽以外の一撃では今どおり叫ぶ（標軸の5台 × 勇者・seed 0..4: 羽のまとまりの叫び 0・それ以外の叫び > 0）", featherRally == 0 && otherRally > 0, $"羽 {featherRally}・羽以外 {otherRally}");
            Expect("スイッチは戻る（器具の外の戦は数えない）", BattleContext.ChainCut365 == 0 && !BattleContext.ChainCount365);
        }

        // ---- verbose の有無 ----
        {
            long vdiff = 0, cells = 0;
            var fs = new List<Formation>();
            foreach (var b in PBoards()) foreach (var v in PVers.Skip(1)) fs.Add(ApplyP(b.F, v));
            fs.Add(H1(Presets.HisaBoard366(hisa)));
            foreach (var f in fs)
                foreach (var w in new[] { I.Boss, I.Guard, I.M4, S366.BossNoTax })
                    for (int seed = 0; seed < 5; seed++)
                        foreach (YokeRule? y in new YokeRule?[] { null, YokeY })
                        {
                            var (r0, _, _) = Raw(f, w, seed, y);
                            var (r1, _, _) = Raw(f, w, seed, y, verbose: true);
                            cells++;
                            if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns || r0.PlayerSurvivors != r1.PlayerSurvivors) vdiff++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ（版の台 × 勇者 ／ 近衛 ／ 第4波 ／ 蝕みなし × 規定 ／ Y の軛・seed 0..4）", vdiff == 0, $"{cells} 戦・差 {vdiff}");
        }

        // ---- 器具の中だけ ----
        {
            var bomb = Row(BombRow);
            bool noTax = !S366.BossNoTax.Make().Any(u => u.HasTrait(TraitId.VenomTaxSeal)) && EnemyCatalog.BossRegular.Traits.Contains(TraitId.VenomTaxSeal);
            bool guards = Guards.All(g => S307.Seats(HeroBoardOf(g)) == "ガルド・スィド・グザ・ヴィオ・" + Short(g)) && S307.Seats(bomb) == "ガルド・スィド・グザ・ヴィオ・ラウ"
                && !CompareBuilds().Any(r => r.Name.StartsWith("毒特化", StringComparison.Ordinal));
            Expect("蝕みなしの勇者 ／ 守りの差し替えは器具の中だけ（規定の勇者は蝕みを持つ・`compare` の毒爆弾はラウのまま）", noTax && guards, S307.Seats(bomb));
            var picked = Presets.PoisonHero367();
            var (pg, _) = PickGuard();
            Expect("毒特化・勇者（`Presets.PoisonHero367`）＝ 毒爆弾のラウの席に `pick` で選んだ守り（規定の駒）", S307.Seats(picked) == S307.Seats(HeroBoardOf(pg)) && picked.Occupied().All(o => UnitCatalog.All.Contains(o.Def)), $"{S307.Seats(picked)}・pick {Short(pg)}");
        }

        // ---- 試遊の台 ----
        {
            // 第368期: 第367期の「版・」の4台は外した（G ／ V ／ H1 を規定にした）。「試遊・毒」は規定の駒のまま（中身は GV）。
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.Take(8).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒" })
                && !vr.Any(n => n is "版・G グザ" or "版・V ヴィオ" or "版・GV" or "版・H1 ヒサ");
            Expect("`Presets.Versions` の頭 ＝ 試遊の7台 ＋ 「試遊・毒」・第367期の「版・G ／ V ／ GV ／ H1」は無い（第368期に外した）", rows, string.Join("、", vr));
            var play = Presets.Versions.First(r => r.Name == "試遊・毒").F;
            bool ok = S307.Seats(play) == S307.Seats(Presets.PoisonHero367()) && play.Occupied().All(o => UnitCatalog.All.Contains(o.Def))
                && S307.Seats(Hero367()) == S307.Seats(play) && Hero367()[2] == guza && Hero367()[3] == vio;
            Expect("「試遊・毒」＝ 毒特化・勇者（規定の駒）・この器具の毒特化・勇者は第367期までのグザ ／ ヴィオ", ok, S307.Seats(play));
        }

        // ---- PickOne ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 715359fa:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh367*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第366期（715359fa）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && r0 + RollsAdded369 == r1 /* 第369期: グザ Z の噴き出しの引き（`Common.RollsAdded369`）*/ && re0 == re1 && e0 > 0 && mine == 0,
                $"engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
