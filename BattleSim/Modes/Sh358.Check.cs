using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh358 の自己検査（第358期・指示書 §9）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿と台本で確かめる（盤面は動かさない）。
static partial class Sh358Diag
{
    /// <summary>§6 の代表台（`seats` の結果から選んだ並び・報告 §1 の決めたこと）。</summary>
    static readonly (string Label, string Key)[] RepKeys =
    {
        ("A ツギありの最良", "ウロ・ガルド・ガレ・ツギ・ヒビ"),     // `seats hbgr` の1位（ボスはどの並びも 0%・近衛 ／ 大隊 100%・4.0T）
        ("B ツギなしの最良", "ドルガ・ササ・ゴルム・ヒビ・ガレ"),   // ツギを含まない並びの1位（近衛 ／ 大隊 100%・規定では 35 ／ 40%）
    };

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e, bool verbose = false)
    {
        var ctx = new BattleContext(0, verbose);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 200, int atk = 10, AttackPattern pat = AttackPattern.Single) =>
        new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = pat };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static UnitTally Tly(BattleContext c, string id) => ((Dictionary<string, UnitTally>)typeof(BattleContext)
        .GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!).GetValueOrDefault(id) ?? new UnitTally();
    static readonly UnitAction Skill = new(ActionKind.Skill);
    static int Armor(UnitState u) => u.RawCounter(StatusKeys.Armor);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh358 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義 ----
        {
            var h = UnitCatalog.HibiHBr; var g = UnitCatalog.GareGRr;
            bool hb = h.Traits.SequenceEqual(new[] { TraitId.ShardGift, TraitId.ShardBare, TraitId.Frail }) && h.Id == "hibi" && h.MaxHp == UnitCatalog.Hibi.MaxHp && h.Attack == UnitCatalog.Hibi.Attack
                && h.Speed == UnitCatalog.Hibi.Speed && h.Actions.Count == 1 && h.Actions[0].Kind == ActionKind.Skill;
            bool gr = g.Traits.SequenceEqual(new[] { TraitId.PebbleGather }) && g.Id == "gare" && g.MaxHp == UnitCatalog.Gare.MaxHp && g.Attack == UnitCatalog.Gare.Attack
                && g.Speed == UnitCatalog.Gare.Speed && g.Actions.Count == 1 && g.Actions[0].Kind == ActionKind.Skill;
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.ShardGift or TraitId.ShardBare or TraitId.PebbleGather)).Select(d => d.Id).ToArray();
            bool def = UnitCatalog.Hibi.Traits.SequenceEqual(new[] { TraitId.Shatter, TraitId.Frail }) && UnitCatalog.Gare.Traits.SequenceEqual(new[] { TraitId.Shrapnel });
            Expect("版の定義: HB-r ＝ 体はヒビのまま・札は `ShardGift` ＋ `ShardBare` ＋ 脆弱（砕けは外す）・手番は術 ／ GR-r ＝ 体はガレのまま・札は `PebbleGather` だけ・手番は術", hb && gr, $"hb {hb}・gr {gr}");
            Expect("規定のヒビ ／ ガレは触っていない・新しい札の保持者は 0 枚（`All` ／ `Retired`）", def && held.Length == 0, held.Length == 0 ? "" : string.Join(",", held));
        }

        // ---- HB-r ----
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: UnitCatalog.HibiHBr, back1: Plain("c"), back3: Plain("d")),
                Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
            var hibi = U(p, "hibi"); var foe = e[0];
            TraitCatalog.Get(TraitId.ShardGift).OnAction(ctx, hibi, Skill);
            bool gift = p.Where(u => u != hibi).All(u => Armor(u) == ShardGiftTrait.Gift && u.RawCounter(ShardGiftTrait.Key) == ShardGiftTrait.Gift) && Armor(hibi) == 0 && hibi.RawCounter(ShardGiftTrait.Key) == 0;
            Expect($"HB-r: 手番でヒビ以外の生きている味方 4 体に破片を {ShardGiftTrait.Gift} ずつ配る（返しの印つき）・ヒビには付かない", gift, string.Join(" ", p.Select(u => $"{u.Def.Id}:{Armor(u)}")));
            hibi.SetCounter(StatusKeys.Armor, 10);
            Expect("HB-r: ヒビに破片を書いても付かずに消える（纏えない・捨てた量は計数だけ）", Armor(hibi) == 0 && Tly(ctx, "hibi").Sh358Refused >= 10, $"破片 {Armor(hibi)}・捨てた {Tly(ctx, "hibi").Sh358Refused}");
            var a = U(p, "a"); var b = U(p, "b");
            int f0 = foe.Hp, a0 = a.Hp;
            ctx.ApplyDamage(a, 10, foe);
            bool ret = foe.Hp == f0 - ShardGiftTrait.Gift && a.Hp == a0 - (10 - ShardGiftTrait.Gift) && Armor(a) == 0 && a.RawCounter(ShardGiftTrait.Key) == 0 && Tly(ctx, "hibi").Sh358Returns == 1;
            Expect("HB-r: 返しの破片が敵の一撃で割れた量（6）だけ、その一撃の主へ返す（残り 4 は HP へ）", ret, $"敵 HP {f0} → {foe.Hp}・味方 HP {a0} → {a.Hp}");
            int f1 = foe.Hp;
            ctx.ApplyDamage(b, 2, foe);
            bool part = foe.Hp == f1 - 2 && Armor(b) == ShardGiftTrait.Gift - 2 && b.RawCounter(ShardGiftTrait.Key) == ShardGiftTrait.Gift - 2;
            int f2 = foe.Hp;
            ctx.ApplyDamage(b, 3, b);   // 味方（自分）からの一撃では返さない
            bool self = foe.Hp == f2 && Armor(b) == 1 && b.RawCounter(ShardGiftTrait.Key) == 1;
            Expect("HB-r: 割れた分だけ返す（2 なら 2）・敵以外の一撃で割れた欠片は返さない（欠片の帳簿は破片を超えない）", part && self, $"敵 {f1} → {f2} → {foe.Hp}・b の破片 {Armor(b)}・欠片 {b.RawCounter(ShardGiftTrait.Key)}");
        }
        {
            // 範囲攻撃: 全体攻撃の敵が1回殴ると、欠片を持つ味方それぞれから一撃の主へ返しが集まる（同じ通し番号）
            var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: UnitCatalog.HibiHBr, back1: Plain("c"), back3: Plain("d")),
                Formation.Build(center: Plain("foe", hp: 999, atk: 20, pat: AttackPattern.All)), out var p, out var e, verbose: true);
            var hibi = U(p, "hibi"); var foe = e[0];
            TraitCatalog.Get(TraitId.ShardGift).OnAction(ctx, hibi, Skill);
            int f0 = foe.Hp;
            ctx.PerformAttack(foe);
            var evs = ctx.Events.Where(x => x.Kind == BattleEventKind.Shard && x.Text == ShardLabels.Return).ToList();
            bool group = evs.Count == 4 && evs.All(x => x.TargetId == foe.InstanceId && x.Amount == ShardGiftTrait.Gift && x.Slot > 0) && evs.Select(x => x.Slot).Distinct().Count() == 1
                && foe.Hp == f0 - 4 * ShardGiftTrait.Gift;
            Expect("HB-r: 全体攻撃1回で、欠片を持つ味方 4 体から一撃の主へ返しが集まる（4 本・同じ通し番号・計 24）", group, $"返し {evs.Count} 本・敵 HP {f0} → {foe.Hp}");
        }
        {
            // ツギの板と重なる: 欠片の分は返し、残りの破片の分だけ板が返す（二重に返さない）
            var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: UnitCatalog.Tsugi, center: UnitCatalog.HibiHBr, back1: Plain("c"), back3: Plain("d")),
                Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
            var hibi = U(p, "hibi"); var foe = e[0]; var planked = U(p, "a");
            // 板（撃ち返す印・厚さの倍率なし）を直に置く（検査の下ごしらえ・ツギの手番の選び方は確かめない）
            foreach (var u in p) { u.SetCounter(StatusKeys.Armor, 0); u.SetCounter(StatusKeys.Plank, 0); }
            planked.SetCounter(StatusKeys.Armor, 20); planked.SetCounter(StatusKeys.Plank, PlankTrait.Plain | PlankTrait.Rebound);
            int plank = Armor(planked);
            TraitCatalog.Get(TraitId.ShardGift).OnAction(ctx, hibi, Skill);
            int f0 = foe.Hp, armor = Armor(planked), hp0 = planked.Hp;
            int hit = armor + 5;
            ctx.ApplyDamage(planked, hit, foe);
            int back = f0 - foe.Hp;
            var ht = Tly(ctx, "hibi");
            // 板の返しは「失った破片 ＋ 残りの厚さ × 倍率」・残りは 0 なので失った破片（板の分）だけ
            bool once = ht.Sh358ReturnNominal == ShardGiftTrait.Gift && back == armor && planked.Hp == hp0 - 5;
            Expect("HB-r ＋ ツギの板: 板を貼られた駒の破片が割れたら、欠片の分（6）は返し・残り（板の分）だけ板が返す（返した計 ＝ 割れた破片・二重に返さない）", once,
                $"板 {plank}・破片 {armor}・返し {ht.Sh358ReturnNominal}・敵へ計 {back}");
        }

        // ---- GR-r ----
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: UnitCatalog.GareGRr, back1: Plain("c"), back3: Plain("d")),
                Formation.Build(front1: Plain("f1", hp: 999), center: Plain("f2", hp: 999)), out var p, out var e);
            var gare = U(p, "gare"); var a = U(p, "a"); var b = U(p, "b");
            var tr = TraitCatalog.Get(TraitId.PebbleGather);
            bool idle = !tr.CanAct(ctx, gare, ActionKind.Skill);
            int e0 = e.Sum(u => u.Hp);
            tr.OnAction(ctx, gare, Skill);
            bool nothing = e.Sum(u => u.Hp) == e0;
            a.SetCounter(StatusKeys.Armor, 10); gare.SetCounter(StatusKeys.Armor, 4); b.SetCounter(StatusKeys.Armor, 5);
            ctx.ApplyDamage(a, 7, e[0]);      // 敵の一撃で 7 割れる
            ctx.ApplyDamage(gare, 9, e[1]);   // ガレ自身の破片 4 が割れる（残り 5 は HP）
            ctx.ApplyDamage(b, 3, b);         // 味方（自分）の一撃では拾わない
            int peb = gare.RawCounter(PebbleTrait.Key);
            Expect("GR-r: 味方（ガレを含む）の破片が敵の一撃で割れた量だけ礫が溜まる（7 ＋ 4 ＝ 11・味方の一撃の分は拾わない）", peb == 11, $"礫 {peb}");
            Expect("GR-r: 礫 0 の手番は何もできない（`CanAct` が偽・撃っても敵は減らない）", idle && nothing);
            int armBefore = p.Sum(Armor);
            int[] hp0 = e.Select(u => u.Hp).ToArray();
            bool can = tr.CanAct(ctx, gare, ActionKind.Skill);
            tr.OnAction(ctx, gare, Skill);
            int want = 11 * ctx.Shrapnel.Multiplier + gare.CurrentAttack;
            bool hitAll = can && e.Select((u, i) => hp0[i] - u.Hp).All(d => d == want) && gare.RawCounter(PebbleTrait.Key) == 0 && p.Sum(Armor) == armBefore;
            Expect($"GR-r: 手番で礫を全部使って 礫 × {ctx.Shrapnel.Multiplier} ＋ 攻撃力（{want}）を敵全体へ撃つ・礫は 0 に・味方の破片は減らない", hitAll,
                $"敵の減り {string.Join("・", e.Select((u, i) => hp0[i] - u.Hp))}・味方の破片 {armBefore} → {p.Sum(Armor)}");
        }

        // ---- 台の上（帳簿と台本）・器具の有無で勝敗が変わらない ----
        {
            int diff = 0, n = 0;
            var vh = V("hbgr");
            foreach (var b in P0Boards().Concat(Boards()))
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int seed = 0; seed < 3; seed++)
                    {
                        var f = Apply(b.F, vh);
                        var c = Db356Diag.Fight(f, w, seed);
                        var (r, _, _) = I.Raw(f, w, seed);
                        var (rv, _, _) = I.Raw(f, w, seed, verbose: true);
                        n++;
                        if ((c.Wins == 1) != r.PlayerWon || r.PlayerWon != rv.PlayerWon || r.Turns != rv.Turns || (c.Wins == 1 && c.WinT != r.Turns)) diff++;
                    }
            Expect("verbose の有無 ／ 計数の有無で勝敗 ／ T が同じ（HB-r ＋ GR-r・代表台 × ボス ／ 近衛 ／ 大隊 ／ 本編5・seed 0..2）", diff == 0, $"{n} 戦・差 {diff}");
            var gb = Measure(Apply(Row(GareRow), vh), I.Boss, 5);
            Expect("台の上: 礫の台（HB-r ＋ GR-r）× ボスで欠片を配り ／ 返し ／ 礫を拾い ／ 投げている（帳簿 ＞ 0）・返しが同じ主へ集まる塊がある", gb.Given > 0 && gb.RetN > 0 && gb.Picked > 0 && gb.Throws > 0 && gb.RetGroups > 0,
                $"配った {Per(gb.Given, gb.N)}・返し {Per2(gb.RetN, gb.N)} 回・塊 {Per2(gb.RetGroups, gb.N)}・拾った {Per(gb.Picked, gb.N)}・投げた {Per2(gb.Throws, gb.N)}");
            var g0 = Measure(Row(GareRow), I.Boss, 5);
            Expect("台の上: 規定では版の帳簿が 0（返し ／ 礫 ／ 欠片）", g0.Given == 0 && g0.RetN == 0 && g0.Picked == 0 && g0.Throws == 0);
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            string[] want = { "版・HB-r 破片", "版・GR-r 破片", "版・HB-r＋GR-r 破片", "版・HB-r＋GR-r 破片 ツギ" };
            bool rows = vr.Take(5).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, WallRow }) && vr.Skip(5).SequenceEqual(want);
            Expect("`Presets.Versions` ＝ 試遊の5台 ＋ 「版・」の4台（HB-r ／ GR-r ／ HB-r＋GR-r ／ HB-r＋GR-r ツギ）", rows, string.Join("、", vr));
            if (rows)
            {
                Formation F(string n) => Presets.Versions.First(r => r.Name == n).F;
                bool ok = Seats(F(want[0])) == RepKeys[1].Key && Seats(F(want[3])) == RepKeys[0].Key
                    && F(want[0]).Occupied().Any(o => o.Def == UnitCatalog.HibiHBr) && F(want[0]).Occupied().Any(o => o.Def == UnitCatalog.Gare)
                    && F(want[1]).Occupied().Any(o => o.Def == UnitCatalog.Hibi) && F(want[1]).Occupied().Any(o => o.Def == UnitCatalog.GareGRr)
                    && F(want[2]).Occupied().Any(o => o.Def == UnitCatalog.HibiHBr) && F(want[2]).Occupied().Any(o => o.Def == UnitCatalog.GareGRr)
                    && F(want[3]).Occupied().Any(o => o.Def == UnitCatalog.HibiHBr) && F(want[3]).Occupied().Any(o => o.Def == UnitCatalog.GareGRr);
                Expect("「版・」の台: 並びは §4 のツギなしの最良（ツギありの台はツギありの最良）・ヒビ ／ ガレだけが版", ok, $"{Seats(F(want[0]))} ／ {Seats(F(want[3]))}");
            }
        }

        // ---- PickOne ----
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 773bcf51:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh358*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第357期（773bcf51）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
