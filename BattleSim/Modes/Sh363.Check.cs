using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh363 の自己検査（第363期・指示書 §10）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿で確かめる（盤面は動かさない）。
static partial class Sh363Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static UnitDef Plain(string id, int hp = 200, int atk = 10) => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single };
    static UnitTally Tly(BattleContext c, string id) => ((Dictionary<string, UnitTally>)typeof(BattleContext)
        .GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!).GetValueOrDefault(id) ?? new UnitTally();
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

    /// <summary>アカを中央に、前1 ／ 前3 に素体の味方 a ／ b（HP 200）。敵は前1 ／ 前3 に f1 ／ f2（HP 999・攻 10）。</summary>
    static BattleContext AkaBoard(UnitDef aka, out UnitState k, out UnitState a, out UnitState b, out UnitState f1, out UnitState f2, bool verbose = false)
    {
        var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: aka), Formation.Build(front1: Plain("f1", hp: 999), front3: Plain("f2", hp: 999)),
            out var p, out var e, verbose);
        k = p.First(u => u.Def.Id == "susu"); a = p.First(u => u.Def.Id == "a"); b = p.First(u => u.Def.Id == "b");
        f1 = e.First(u => u.Def.Id == "f1"); f2 = e.First(u => u.Def.Id == "f2");
        return ctx;
    }

    /// <summary>敵2体（f1 ／ f2）の HP の和のうち f2 の分（f1 は呼び出し側で見る）。</summary>
    static int e0(BattleContext c) => c.LivingMembers(BattleContext.EnemyTeam).Where(u => u.Def.Id == "f2").Sum(u => u.Hp);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh363 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            var k = UnitCatalog.Kado; var h = UnitCatalog.KadoH362; var r = UnitCatalog.KadoKDr; var t = UnitCatalog.KadoKDt;
            bool def = k.Traits.SequenceEqual(r.Traits) && k.PlusText == r.PlusText && k.MinusText == r.MinusText
                && h.Traits.SequenceEqual(new[] { TraitId.ThornGuard, TraitId.Thorns, TraitId.Immobile, TraitId.Havoc, TraitId.ThornsArmored })
                && t.Traits.SequenceEqual(h.Traits.Append(TraitId.ThornsWound)) && r.Traits.SequenceEqual(h.Traits.Append(TraitId.ThornsWound).Append(TraitId.ThornsBare))
                && UnitCatalog.All.Contains(k) && !UnitCatalog.All.Contains(h) && !UnitCatalog.All.Contains(r)
                && ReferenceEquals(PinKd362(k), h) && ReferenceEquals(PinSa361(k), h) && ReferenceEquals(PinSa360(k), h) && ReferenceEquals(PinKd362(UnitCatalog.KadoKDr), UnitCatalog.KadoKDr);
            Expect("段0: 規定のカド ＝ KD-r（札 ／ 文面が `KadoKDr` と同じ）・旧（`KadoH362`）＝ 第362期までの5枚・KD-t ／ KD-r は旧から・`All` は規定だけ・古い口の終端からカドも旧へ（`PinSa360` ／ `PinSa361` ／ `PinKd362`）", def);
            bool Bares(UnitDef kado)
            {
                var c = Ctx(Formation.Build(center: kado), Formation.Build(front1: Plain("foe", hp: 999)), out var p, out _);
                var u = p[0]; u.Hp = 48; c.ApplyDamage(u, 1, null);
                return ThornsBareTrait.IsBare(u);
            }
            Expect("段0: 規定のカドは HP が5割を切ると棘を剥く ／ 旧（`KadoH362`）では今までどおり剥かない", Bares(UnitCatalog.Kado) && !Bares(UnitCatalog.KadoH362));
        }

        // ---- 版の定義 ----
        {
            var s = UnitCatalog.Susu; var n = UnitCatalog.SusuAKn; var x = UnitCatalog.SusuAKs;
            bool ok = n.Traits.SequenceEqual(new[] { TraitId.BloodRead }) && x.Traits.SequenceEqual(new[] { TraitId.BloodPool })
                && new[] { n, x }.All(d => d.Id == s.Id && d.Name == s.Name && d.MaxHp == s.MaxHp && d.Attack == s.Attack && d.Speed == s.Speed && d.Actions == s.Actions && d.Advances == s.Advances && d.Flavor == s.Flavor)
                && n.PlusText.Contains("傷の深さを詠み") && x.PlusText.Contains("残らず溜める") && !n.MinusText.Contains("敵から受けた傷") && !x.MinusText.Contains("敵から受けた傷")
                && !UnitCatalog.All.Contains(n) && !UnitCatalog.All.Contains(x) && s.Traits.SequenceEqual(new[] { TraitId.Ash });
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(y => y is TraitId.BloodRead or TraitId.BloodPool)).Select(d => d.Id).ToArray();
            Expect("版の定義: AK-n ＝ アカの体 ＋ `BloodRead` ／ AK-s ＝ ＋ `BloodPool`（`Ash` と差し替え・体 ／ 速さ ／ 手番 ／ Id は規定のまま・文面は叩き台・「敵から受けた傷は血にならない」は外した）・新しい札の保持者は 0 枚・規定のアカは `Ash` のまま", ok && held.Length == 0, held.Length == 0 ? "" : string.Join(",", held));
        }

        // ---- AK-n ----
        {
            var c = AkaBoard(UnitCatalog.SusuAKn, out var k, out var a, out var b, out var f1, out var f2);
            a.Hp = 150; b.Hp = 170; k.Hp = 54;   // 傷 50 ＋ 30 ＋ 10 ＝ 90
            int h1 = f1.Hp, h2 = f2.Hp;
            var o1 = c.TakeTurn(k);
            Expect("AK-n: 撃つ量 ＝ 生きている味方全員の今の傷の合計（アカ自身を含む・50 ＋ 30 ＋ 10 ＝ 90）を敵全体へ", o1 == TurnOutcome.Skill && h1 - f1.Hp == 90 && h2 - f2.Hp == 90 && Tly(c, "susu").Sh363CastSum == 90, $"{o1}・f1 −{h1 - f1.Hp}・f2 −{h2 - f2.Hp}");
            c.Heal(a, 50, b);
            h1 = f1.Hp; c.TakeTurn(k);
            Expect("AK-n: 治すと弱くなる（a を 50 治した後は 0 ＋ 30 ＋ 10 ＝ 40）", h1 - f1.Hp == 40, $"f1 −{h1 - f1.Hp}");
            c.ApplyDamage(b, 999, f1);
            h1 = f1.Hp; c.TakeTurn(k);
            Expect("AK-n: 倒れた味方は入らない（b が倒れた後は 0 ＋ 10 ＝ 10）", !b.IsAlive && h1 - f1.Hp == 10, $"f1 −{h1 - f1.Hp}");
            var c0 = AkaBoard(UnitCatalog.SusuAKn, out var k0, out _, out _, out var g1, out _);
            int g = g1.Hp + e0(c0); c0.TakeTurn(k0);
            int lostAll = g - g1.Hp - e0(c0);
            Expect("AK-n: 傷が 0 の手番は素の一撃（撃たない・灰が無い手番と同じ・敵の誰かが 3 減る）", Tly(c0, "susu").Sh363Casts == 0 && Tly(c0, "susu").AshDry == 1 && lostAll == UnitCatalog.Susu.Attack, $"敵の減り {lostAll}");
            // 倒れたときに降りかかる量 ＝ そのときの（生きている）味方の傷の合計の半分（切り捨て）を、隣の生きている味方に等分。
            var c2 = AkaBoard(UnitCatalog.SusuAKn, out var k2, out var a2, out var b2, out var e1, out _);
            a2.Hp = 100; b2.Hp = 161;   // 傷 100 ＋ 39 ＝ 139 → 半分 69
            int nb = new[] { a2, b2 }.Count(u => FormationRules.AreAdjacent(k2, u));
            int share = 69 / Math.Max(1, nb);
            c2.ApplyDamage(k2, 999, e1);
            Expect($"AK-n: 倒れたとき、味方全員の傷の合計の半分（139 → 69）が隣の味方 {nb} 体に等分（{share} ずつ）で降る", !k2.IsAlive && 100 - a2.Hp == share && 161 - b2.Hp == share && Tly(c2, "susu").AshFalloutOut == share * nb && Tly(c2, "susu").AshAtDeath == 69,
                $"a −{100 - a2.Hp}・b −{161 - b2.Hp}");
        }

        // ---- AK-s ----
        {
            var c = AkaBoard(UnitCatalog.SusuAKs, out var k, out var a, out var b, out var f1, out var f2);
            c.ApplyDamage(a, 10, f1);       // 敵から
            c.ApplyDamage(a, 11, b);        // 味方から
            c.ApplyDamage(a, 12, a);        // 自分から
            c.ApplyDamage(b, 13, null);     // 刻み（出どころなし）
            var t = Tly(c, "susu");
            int blood = AshTrait.AshOf(k);
            Expect("AK-s: 敵 ／ 味方 ／ 自分 ／ 刻みの傷がすべて溜まる（10 ＋ 11 ＋ 12 ＋ 13 ＝ 46）", blood == 46 && t.Sh363BloodFoe == 10 && t.Sh363BloodAlly == 11 && t.Sh363BloodSelf == 12 && t.Sh363BloodDot == 13,
                $"血 {blood}・敵 {t.Sh363BloodFoe} ／ 味方 {t.Sh363BloodAlly} ／ 自分 {t.Sh363BloodSelf} ／ 刻み {t.Sh363BloodDot}");
            a.SetCounter(StatusKeys.Armor, 15);
            int hp = a.Hp; c.ApplyDamage(a, 20, f1);
            Expect("AK-s: 破片で受けた分は溜まらない（破片 15 ／ 一撃 20 → HP が 5 減って血も 5）", AshTrait.AshOf(k) == 51 && hp - a.Hp == 5, $"血 {AshTrait.AshOf(k)}・HP −{hp - a.Hp}");
            c.Heal(a, 30, b);
            Expect("AK-s: 治しても溜まった血は減らない", AshTrait.AshOf(k) == 51, $"血 {AshTrait.AshOf(k)}");
            b.Hp = 5; c.ApplyDamage(b, 50, f1);
            Expect("AK-s: 溜まるのは実際に減った HP（残り 5 の味方に 50 → 血 +5）", AshTrait.AshOf(k) == 56, $"血 {AshTrait.AshOf(k)}");
            int h1 = f1.Hp, h2 = f2.Hp;
            c.TakeTurn(k);
            Expect($"AK-s: 閾（{BloodPoolTrait.Threshold}）に届かない手番は何もしない（溜め・敵は減らない・血はそのまま）", Tly(c, "susu").AshHolds == 1 && Tly(c, "susu").Sh363Casts == 0 && f1.Hp == h1 && f2.Hp == h2 && AshTrait.AshOf(k) == 56, $"溜め {Tly(c, "susu").AshHolds}");
            k.SetCounter(StatusKeys.Ash, BloodPoolTrait.Threshold + 7);
            var o2 = c.TakeTurn(k);
            int w = BloodPoolTrait.Threshold + 7;
            Expect($"AK-s: 届いた手番で全部使って敵全体を撃つ（{w} ずつ・血は 0 に）", o2 == TurnOutcome.Skill && h1 - f1.Hp == w && h2 - f2.Hp == w && AshTrait.AshOf(k) == 0 && Tly(c, "susu").Sh363Casts == 1, $"{o2}・f1 −{h1 - f1.Hp}・血 {AshTrait.AshOf(k)}");
            var c2 = AkaBoard(UnitCatalog.SusuAKs, out var k2, out var a2, out var b2, out var e1, out _);
            k2.SetCounter(StatusKeys.Ash, 80);
            int nb = new[] { a2, b2 }.Count(u => FormationRules.AreAdjacent(k2, u));
            c2.ApplyDamage(k2, 999, e1);
            Expect($"AK-s: 血を溜めたまま倒れると隣の味方 {nb} 体に等分（{80 / nb} ずつ）で降る・倒れた保持者には溜まらない", 200 - a2.Hp == 80 / nb && 200 - b2.Hp == 80 / nb && AshTrait.AshOf(k2) == 0, $"a −{200 - a2.Hp}・b −{200 - b2.Hp}");
            var c3 = AkaBoard(UnitCatalog.Susu, out var k3, out var a3, out _, out var e3, out _);
            c3.ApplyDamage(a3, 10, e3);
            Expect("規定のアカ: 敵から受けた傷は今どおり灰にならない", AshTrait.AshOf(k3) == 0);
        }

        // ---- 台の上 ----
        {
            int diff = 0, n = 0, casts = 0, skills = 0, snap = 0, castsN = 0;
            foreach (var bd in P0Boards().Take(2))
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int seed = 0; seed < 2; seed++)
                        {
                            var f = Apply(bd.F, v);
                            var cl = Fight(f, w, seed);
                            var (r, _, _) = I.Raw(f, w, seed);
                            var (rv, pv, _) = I.Raw(f, w, seed, verbose: true);
                            n++;
                            if ((cl.Wins == 1) != r.PlayerWon || r.PlayerWon != rv.PlayerWon || r.Turns != rv.Turns || (cl.Wins == 1 && cl.WinT != r.Turns)) diff++;
                            if (v == Vers[2])
                            {
                                var aka = pv.First(u => u.Def.Id == "susu");
                                casts += rv.TallyByUnit.TryGetValue("susu", out var at) ? (int)at.Sh363Casts : 0;
                                skills += rv.Events.Count(x => x.Kind == BattleEventKind.Skill && x.ActorId == aka.InstanceId && x.Text == AshActionLabels.Release);
                                snap += rv.Events.Count(x => x.Kind == BattleEventKind.StatusSnapshot && x.TargetId == aka.InstanceId && x.Text == StatusKeys.LabelOf(StatusKeys.Ash));
                            }
                            if (v == Vers[1]) castsN += rv.TallyByUnit.TryGetValue("susu", out var an) ? (int)an.Sh363Casts : 0;
                        }
            Expect("verbose の有無 ／ 計数の有無で勝敗 ／ T が同じ（規定 ／ AK-n ／ AK-s・S ／ N × ボス ／ 近衛 ／ 大隊 ／ 本編5・seed 0..1）", diff == 0, $"{n} 戦・差 {diff}");
            Expect("AK-s が撃つたびに術の出来事が1つ出る・溜まっていく血はターン頭の状態（灰の表示）に出る（表示専用）", casts > 0 && casts == skills && snap > 0, $"撃った {casts}・術 {skills}・血の表示 {snap}・AK-n の撃った回数 {castsN}");
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            string[] want = { "版・AK-n 自傷", "版・AK-s 自傷", "版・AK-s 自傷 リリ" };
            bool rows = vr.Take(6).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片" }) && vr.Skip(6).SequenceEqual(want)
                && !vr.Any(x => x.Contains("KD-"));
            Expect("`Presets.Versions` ＝ 試遊の6台 ＋ 「版・」の3台（第362期の「版・」の3台は外した）", rows, string.Join("、", vr));
            if (rows)
            {
                Formation F(string nm) => Presets.Versions.First(r => r.Name == nm).F;
                bool ok = F(want[0]).Occupied().Any(o => o.Def == UnitCatalog.SusuAKn) && F(want[1]).Occupied().Any(o => o.Def == UnitCatalog.SusuAKs) && F(want[2]).Occupied().Any(o => o.Def == UnitCatalog.SusuAKs)
                    && Has(F(want[2]), "lili") && !Has(F(want[1]), "lili") && Seats(F(want[0])) == Seats(F(want[1]))
                    && want.All(nm => F(nm).Occupied().Any(o => o.Def == UnitCatalog.Kado));
                Expect("「版・」の3台: AK-n ／ AK-s は同じ並び（リリなし）・リリの台はリリを含む・どれも規定のカド（KD-r）を含む", ok, $"{Seats(F(want[1]))} ／ {Seats(F(want[2]))}");
            }
        }

        // ---- PickOne ----
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show a208d08d:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs"), r1 = File.ReadAllText("BattleCore/Traits.cs").Split("Roll(").Length - 1;
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh363*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第362期（a208d08d）と同じ・Traits.cs の `Roll(` も同じ・器具の中も 0）", e0 == e1 && t0 == t1 && r0 == r1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・Roll {r0} → {r1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
