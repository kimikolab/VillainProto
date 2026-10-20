using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh364 の自己検査（第364期・指示書 §9）。跳ね撃ちは盤面を直に組んで1回ずつ確かめ、台の上では帳簿で確かめる（盤面は動かさない）。
static partial class Sh364Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static UnitDef Foe(string id) => new() { Id = id, Name = id, MaxHp = 999, Attack = 0, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single };
    static UnitTally Tly(BattleContext c, string id) => ((Dictionary<string, UnitTally>)typeof(BattleContext)
        .GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!).GetValueOrDefault(id) ?? new UnitTally();

    /// <summary>撃ち手1枚（中央）× 敵 <paramref name="n"/> 体（席 0..n−1・HP 999・攻 0）。<paramref name="hp"/> を渡すと敵の HP をその順に置く。</summary>
    static BattleContext Board(UnitDef shooter, int n, out UnitState me, out List<UnitState> foes, int[]? hp = null)
    {
        var ctx = new BattleContext(0, false);
        var p = BattleEngine.Materialize(Formation.Build(center: shooter), BattleContext.PlayerTeam);
        var e = BattleEngine.MaterializeEnemy(EnemyWave.Of(Enumerable.Range(0, n).Select(k => (k, Foe($"f{k}"))).ToArray()), EnemyScaleRule.None);   // 席 0..n−1（9 体まで）
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        me = p[0]; foes = e.OrderBy(u => u.Slot).ToList();
        if (hp is not null) for (int k = 0; k < hp.Length; k++) foes[k].Hp = hp[k];
        return ctx;
    }

    /// <summary>アカ（AK-n 系）を傷 20 にして1手番撃たせ、敵それぞれの減りを返す（量 ＝ 20）。</summary>
    static int[] AkaShot(UnitDef aka, int n, out BattleContext ctx, int[]? hp = null)
    {
        ctx = Board(aka, n, out var me, out var foes, hp);
        me.Hp = me.MaxHp - 20;
        var before = foes.Select(f => f.Hp).ToArray();
        TraitCatalog.Get(TraitId.BloodRead).OnAction(ctx, me, new UnitAction(ActionKind.Skill));
        return foes.Select((f, i) => before[i] - Math.Max(0, f.Hp)).ToArray();
    }

    /// <summary>ガレに礫 10 を持たせて1回投げさせ、敵それぞれの減りと1回の量（礫 × 倍率 ＋ 攻撃力）を返す。</summary>
    static int[] GareShot(UnitDef gare, int n, out int dmg)
    {
        var ctx = Board(gare, n, out var me, out var foes);
        me.SetCounter(PebbleTrait.Key, 10);
        dmg = 10 * ctx.Shrapnel.Multiplier + me.CurrentAttack;
        var before = foes.Select(f => f.Hp).ToArray();
        TraitCatalog.Get(TraitId.PebbleGather).OnAction(ctx, me, new UnitAction(ActionKind.Skill));
        return foes.Select((f, i) => before[i] - Math.Max(0, f.Hp)).ToArray();
    }
    static string S(int[] a) => string.Join("/", a);

    /// <summary>第365期: 第364期の「版・」の4台（`Presets.Versions` から外した・並びはここに固定）。</summary>
    internal static (string Name, Formation F)[] Boards364() => new (string, Formation)[]
    {
        ("版・AKn-A 自傷", Presets.SelfHarmPlay365(UnitCatalog.SusuAKnA)),
        ("版・AKn-B 自傷", Presets.SelfHarmAka(UnitCatalog.SusuAKnB)),
        ("版・GR-A 破片", Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Hibi, center: UnitCatalog.Som, back1: UnitCatalog.GareGRA, back3: UnitCatalog.Nel)),
        ("版・GR-B 破片", Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Som, center: UnitCatalog.Nel, back1: UnitCatalog.GareGRB, back3: UnitCatalog.Hibi)),
    };

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh364 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義 ----
        {
            bool ok = UnitCatalog.SusuAKnA.Traits.SequenceEqual(UnitCatalog.SusuAKn.Traits.Append(TraitId.ScatterGather)) && UnitCatalog.SusuAKnB.Traits.SequenceEqual(UnitCatalog.SusuAKn.Traits.Append(TraitId.ScatterShot))
                && UnitCatalog.GareGRA.Traits.SequenceEqual(UnitCatalog.GareH364.Traits.Append(TraitId.ScatterGather)) && UnitCatalog.GareGRB.Traits.SequenceEqual(UnitCatalog.GareH364.Traits.Append(TraitId.ScatterShot))
                && new[] { UnitCatalog.SusuAKnA, UnitCatalog.SusuAKnB }.All(d => d.Id == "susu" && d.MaxHp == UnitCatalog.SusuH364.MaxHp && d.Speed == UnitCatalog.SusuH364.Speed)
                && new[] { UnitCatalog.GareGRA, UnitCatalog.GareGRB }.All(d => d.Id == "gare" && d.MaxHp == UnitCatalog.GareH364.MaxHp && d.Speed == UnitCatalog.GareH364.Speed && d.Attack == UnitCatalog.GareH364.Attack)
                && UnitCatalog.SusuAKnA.PlusText.Contains("一体に集まる") && UnitCatalog.SusuAKnB.PlusText.Contains("5発に分けて") && UnitCatalog.GareGRA.PlusText.Contains("一体に集まる") && UnitCatalog.GareGRB.PlusText.Contains("5発に分けて");
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d != UnitCatalog.Susu && d != UnitCatalog.Gare && d.Traits.Any(x => x is TraitId.ScatterGather or TraitId.ScatterShot)).Select(d => d.Id).ToArray();
            Expect("版の定義: AKn-A ／ AKn-B ＝ AK-n ＋ `ScatterGather` ／ `ScatterShot`・GR-A ／ GR-B ＝ 規定のガレ ＋ 同じ札（体 ／ Id は今のまま・文面は叩き台）・新しい札の保持者は 0 枚・規定のアカ ／ ガレの札は今のまま",
                ok && held.Length == 0 && UnitCatalog.SusuH364.Traits.SequenceEqual(new[] { TraitId.Ash }) && UnitCatalog.GareH364.Traits.SequenceEqual(new[] { TraitId.PebbleGather }), held.Length == 0 ? "" : string.Join(",", held));
        }

        // ---- 形 A ----
        {
            var a5 = AkaShot(UnitCatalog.SusuAKnA, 5, out _); var a1 = AkaShot(UnitCatalog.SusuAKnA, 1, out _); var a2 = AkaShot(UnitCatalog.SusuAKnA, 2, out _); var a9 = AkaShot(UnitCatalog.SusuAKnA, 9, out var c9);
            bool ok = a5.Length == 5 && a5.All(x => x == 20) && a1.SequenceEqual(new[] { 100 }) && a2.All(x => x == 40) && a9.Length == 9 && a9.All(x => x == 20);
            Expect("A（アカ・量 20）: 敵 5 体で今と同じ（20 ずつ）／ 1 体で 5 倍を1回（100）／ 2 体で 2 倍（40 ずつ）／ 9 体で今と同じ（20 ずつ）", ok, $"5体 {S(a5)}・1体 {S(a1)}・2体 {S(a2)}・9体 {S(a9)}");
            var g5 = GareShot(UnitCatalog.GareGRA, 5, out int d); var g1 = GareShot(UnitCatalog.GareGRA, 1, out _); var g2 = GareShot(UnitCatalog.GareGRA, 2, out _);
            Expect($"A（ガレ・礫 10 → 1回 {d}）: 5 体で {d} ずつ ／ 1 体で {d * 5} を1回 ／ 2 体で {d * 2} ずつ", g5.All(x => x == d) && g1.SequenceEqual(new[] { d * 5 }) && g2.All(x => x == d * 2), $"{S(g5)}・{S(g1)}・{S(g2)}");
        }

        // ---- 形 B ----
        {
            var b5 = AkaShot(UnitCatalog.SusuAKnB, 5, out var c5);
            var b1 = AkaShot(UnitCatalog.SusuAKnB, 1, out var c1);
            var b3 = AkaShot(UnitCatalog.SusuAKnB, 3, out _);
            Expect("B（アカ・量 20）: 5 発・1発は量の半分（10）——敵 5 体で 10 ずつ", b5.All(x => x == 10) && Tly(c5, "susu").Sh364Shots == 5, $"{S(b5)}・弾 {Tly(c5, "susu").Sh364Shots}");
            Expect("B: 敵 1 体なら 5 発とも同じ敵（50）・1発ずつ別の一撃（着弾 5 回）", b1.SequenceEqual(new[] { 50 }) && c1.AshHits == 5, $"{S(b1)}・着弾 {c1.AshHits}");
            Expect("B: 敵 3 体なら席番号の順に 2 ／ 2 ／ 1 発（20 ／ 20 ／ 10）", b3.SequenceEqual(new[] { 20, 20, 10 }), S(b3));
            var bk = AkaShot(UnitCatalog.SusuAKnB, 3, out _, hp: new[] { 10, 999, 999 });
            Expect("B: 倒れた敵を飛ばす（席 0 の敵が1発目で倒れたら、残りの 4 発は席 1 ／ 2 に 2 発ずつ）", bk.SequenceEqual(new[] { 10, 20, 20 }), S(bk));
            var g1 = GareShot(UnitCatalog.GareGRB, 1, out int d); var g3 = GareShot(UnitCatalog.GareGRB, 3, out _);
            Expect($"B（ガレ・1回 {d} → 1発 {d / 2}）: 1 体で {d / 2 * 5}・3 体で 2 ／ 2 ／ 1 発", g1.SequenceEqual(new[] { d / 2 * 5 }) && g3.SequenceEqual(new[] { d / 2 * 2, d / 2 * 2, d / 2 }), $"{S(g1)}・{S(g3)}");
        }

        // ---- 規定は今どおり ----
        {
            var n1 = AkaShot(UnitCatalog.SusuAKn, 1, out _); var n5 = AkaShot(UnitCatalog.SusuAKn, 5, out _);
            var g1 = GareShot(UnitCatalog.GareH364, 1, out int d); var g5 = GareShot(UnitCatalog.GareH364, 5, out _);
            Expect("札の無いアカ（AK-n）／ 規定のガレは今どおり（敵の数に依らず1体に1回・量そのまま）", n1.SequenceEqual(new[] { 20 }) && n5.All(x => x == 20) && g1.SequenceEqual(new[] { d }) && g5.All(x => x == d), $"AK-n {S(n1)} ／ {S(n5)}・ガレ {S(g1)} ／ {S(g5)}");
            var c = Board(R4, 1, out var me, out var foes);
            int h = foes[0].Hp; c.TakeTurn(me);
            Expect("Phase 0 の R4（素体・何もしない）は手番で何もしない（敵が減らない）", foes[0].Hp == h, $"{h} → {foes[0].Hp}");
        }

        // ---- 台の上 ----
        {
            int diff = 0, n = 0;
            foreach (var (b, vers) in new[] { (AkaBoards()[0], AkaVers), (GareBoards()[0], GareVers) })
                foreach (var v in vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Nine })
                        for (int seed = 0; seed < 2; seed++)
                        {
                            var f = Apply(b.F, v);
                            var cl = Fight(f, w, seed, v.Id);
                            var (r, _, _) = I.Raw(f, w, seed);
                            var (rv, _, _) = I.Raw(f, w, seed, verbose: true);
                            n++;
                            if ((cl.Wins == 1) != r.PlayerWon || r.PlayerWon != rv.PlayerWon || r.Turns != rv.Turns) diff++;
                        }
            Expect("verbose の有無 ／ 計数の有無で勝敗 ／ T が同じ（台 B ／ 試遊・破片 × 版 × ボス ／ 近衛 ／ 九体・seed 0..1）", diff == 0, $"{n} 戦・差 {diff}");
        }

        // ---- 試遊の台 ----
        {
            // 第365期: 「版・」の4台は `Presets.Versions` から外した。第364期の4台は `Boards364` に固定して確かめる。
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            string[] want = { "版・AKn-A 自傷", "版・AKn-B 自傷", "版・GR-A 破片", "版・GR-B 破片" };
            bool rows = vr.Take(6).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片" }) && !vr.Any(want.Contains);
            Expect("`Presets.Versions` ＝ 試遊の6台が頭に残る・第364期の「版・」の4台は第365期に外した", rows, string.Join("、", vr));
            if (rows)
            {
                var b364 = Boards364();
                Formation F(string nm) => b364.First(r => r.Name == nm).F;
                bool ok = F(want[0]).Occupied().Any(o => o.Def == UnitCatalog.SusuAKnA) && F(want[1]).Occupied().Any(o => o.Def == UnitCatalog.SusuAKnB)
                    && F(want[2]).Occupied().Any(o => o.Def == UnitCatalog.GareGRA) && F(want[3]).Occupied().Any(o => o.Def == UnitCatalog.GareGRB)
                    && Seats(F(want[1])) == KeyB && !Trio(F(want[0])) && Has(F(want[2]), "nel") && Has(F(want[3]), "nel");
                Expect("「版・」の4台: AKn-A ＝ ミサ＋ボルグ＋ヒサを含まない最良 ／ AKn-B ＝ 台 B ／ GR-A ／ GR-B ＝ 総当たりの最良（ネルを含む）", ok, string.Join(" ／ ", want.Select(x => Seats(F(x)))));
            }
        }

        // ---- PickOne ----
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 78cb2208:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs"), r1 = File.ReadAllText("BattleCore/Traits.cs").Split("Roll(").Length - 1;
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh364*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第363期（78cb2208）と同じ・Traits.cs の `Roll(` も同じ・器具の中も 0）", e0 == e1 && t0 == t1 && r0 == r1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・Roll {r0} → {r1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
