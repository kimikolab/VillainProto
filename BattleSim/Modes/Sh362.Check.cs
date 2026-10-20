using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh362 の自己検査（第362期・指示書 §10）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿で確かめる（盤面は動かさない）。
static partial class Sh362Diag
{
    /// <summary>第363期: 第362期の「版・」の3台（`Presets.Versions` から外した・並びはここに固定）。</summary>
    internal static (string Name, Formation F)[] Boards362() => new (string, Formation)[]
    {
        ("版・KD-t 自傷", Presets.SelfHarmPlay(UnitCatalog.KadoKDt)),
        ("版・KD-r 自傷", Presets.SelfHarmPlay(UnitCatalog.KadoKDr)),
        ("版・KD-r 自傷 リリ", Formation.Build(front1: UnitCatalog.Tome, front3: UnitCatalog.KadoKDr, center: UnitCatalog.Hisa, back1: UnitCatalog.SusuH364, back3: UnitCatalog.Lili)),
    };

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
    static int Armor(UnitState u) => u.RawCounter(StatusKeys.Armor);
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

    /// <summary>カドを中央に、隣（前1 ／ 前3）に素体の味方 a ／ b。敵は前1 に攻 10 の small、前3 に攻 20 の big（どちらも HP 999）。</summary>
    static BattleContext KadoBoard(UnitDef kado, out UnitState k, out UnitState a, out UnitState b, out UnitState small, out UnitState big, bool verbose = false)
    {
        var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: kado), Formation.Build(front1: Plain("small", hp: 999, atk: 10), front3: Plain("big", hp: 999, atk: 20)),
            out var p, out var e, verbose);
        k = p.First(u => u.Def.Id == "kado"); a = p.First(u => u.Def.Id == "a"); b = p.First(u => u.Def.Id == "b");
        small = e.First(u => u.Def.Id == "small"); big = e.First(u => u.Def.Id == "big");
        return ctx;
    }

    /// <summary>反撃1回の重さ（`Sh362RiposteSum`）: カドの HP ／ 最大HP ／ 破片を置いてから small が <paramref name="hit"/> で殴る。</summary>
    static long RipBack(UnitDef kado, int hp, int maxHp, int hit, int armor = 0)
    {
        var c = KadoBoard(kado, out var k, out _, out _, out var small, out _);
        k.MaxHp = maxHp; k.Hp = hp; k.SetCounter(StatusKeys.Armor, armor);
        c.ApplyDamage(k, hit, small);
        return Tly(c, "kado").Sh362RiposteSum;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh362 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            bool def = UnitCatalog.Sasa.Traits.SequenceEqual(UnitCatalog.SasaSSn.Traits) && UnitCatalog.Sasa.PlusText == UnitCatalog.SasaSSn.PlusText && UnitCatalog.Sasa.MinusText == UnitCatalog.SasaSSn.MinusText
                && UnitCatalog.SasaH361.Traits.SequenceEqual(UnitCatalog.SasaSSw.Traits) && UnitCatalog.SasaH361.PlusText == UnitCatalog.SasaSSw.PlusText
                && UnitCatalog.All.Contains(UnitCatalog.Sasa) && !UnitCatalog.All.Contains(UnitCatalog.SasaH361) && !UnitCatalog.All.Contains(UnitCatalog.SasaSSn)
                && ReferenceEquals(PinSa361(UnitCatalog.Sasa), UnitCatalog.SasaH361) && ReferenceEquals(PinSa360(UnitCatalog.Sasa), UnitCatalog.SasaH360) && ReferenceEquals(PinSa360(UnitCatalog.SasaH361), UnitCatalog.SasaH360)
                && UnitCatalog.SasaSSs.Traits.SequenceEqual(UnitCatalog.SasaH361.Traits.Append(TraitId.BraceScatter));
            Expect("段0: 規定のササ ＝ SS-n（札 ／ 文面が `SasaSSn` と同じ）・旧（`SasaH361`）＝ SS-w・SS-s は旧から・`All` は規定だけ・古い口の終端からササも旧へ（`PinSa360` → `SasaH360`・`PinSa361` → `SasaH361`）", def);
            int Far(UnitDef sasa)
            {
                int far = Enumerable.Range(1, 4).First(k => !FormationRules.AreAdjacent(0, k));
                var pl = new Formation(); pl[0] = sasa; pl[far] = Plain("x");
                var c = Ctx(pl, Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
                var s = p.First(u => u.Def.Id == "sasa"); var x = p.First(u => u.Def.Id == "x");
                foreach (var u in p) u.SetCounter(StatusKeys.Armor, 0);
                TraitCatalog.Get(TraitId.Brace).OnAction(c, s, new UnitAction(ActionKind.Skill));
                c.ApplyDamage(s, 20, e[0]);
                return Armor(x) * 1000 + Armor(s);
            }
            int now = Far(UnitCatalog.Sasa), old = Far(UnitCatalog.SasaH361);
            Expect("段0: 規定のササで隣に誰もいないとき、切り落とした 13 がいちばん近い味方へ付く（ササ自身には付かない）／ 旧（SS-w）では今までどおり消える", now == 13000 && old == 0, $"規定 遠い味方 {now / 1000}・ササ {now % 1000} ／ 旧 遠い味方 {old / 1000}");
        }

        // ---- 版の定義 ----
        {
            var t = UnitCatalog.KadoKDt; var r = UnitCatalog.KadoKDr; var k = UnitCatalog.KadoH362;   // 第363期: 旧のカドに固定
            bool ok = t.Traits.SequenceEqual(k.Traits.Append(TraitId.ThornsWound)) && r.Traits.SequenceEqual(k.Traits.Append(TraitId.ThornsWound).Append(TraitId.ThornsBare))
                && new[] { t, r }.All(d => d.Id == k.Id && d.MaxHp == k.MaxHp && d.Attack == k.Attack && d.Speed == k.Speed && d.Actions == k.Actions && d.Advances == k.Advances)
                && t.PlusText.Contains("負った傷の深さ") && r.PlusText.Contains("棘を剥く") && r.MinusText.Contains("傷が浅いうちは") && !UnitCatalog.All.Contains(t) && !UnitCatalog.All.Contains(r);
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d != UnitCatalog.Kado && d.Traits.Any(x => x is TraitId.ThornsWound or TraitId.ThornsBare)).Select(d => d.Id).ToArray();   // 第363期: 規定のカド（KD-r）は数えない
            Expect("版の定義: KD-t ＝ 規定のカド ＋ `ThornsWound` ／ KD-r ＝ ＋ `ThornsBare`（体 ／ 手番は規定のまま・文面は叩き台）・新しい札の保持者は 0 枚", ok && held.Length == 0, held.Length == 0 ? "" : string.Join(",", held));
        }

        // ---- KD-t ----
        {
            long d0 = RipBack(UnitCatalog.KadoH362, 66, 96, 10), t1 = RipBack(UnitCatalog.KadoKDt, 66, 96, 10);
            Expect("KD-t: 反撃 ＝ 攻撃力 × 2 ＋ 傷（HP 66 ／ 96 で 10 を受けた後の傷 40 → 22 ＋ 40 ＝ 62）・規定のカドは 22 のまま", t1 == 62 && d0 == 22, $"KD-t {t1}・規定 {d0}");
            long t0 = RipBack(UnitCatalog.KadoKDt, 96, 96, 10, armor: 50), k0 = RipBack(UnitCatalog.KadoH362, 96, 96, 10, armor: 50);
            Expect("KD-t: 傷 0（破片が一撃を受け切った）なら反撃は今と同じ（22）", t0 == 22 && k0 == 22, $"KD-t {t0}・規定 {k0}");
            long tm = RipBack(UnitCatalog.KadoKDt, 66, 70, 10);
            Expect("KD-t: 削られた最大HP は傷に入らない（最大HP 70 ／ HP 66 で 10 を受けた後の傷 14 → 22 ＋ 14 ＝ 36）", tm == 36, $"{tm}");
        }

        // ---- KD-r ----
        {
            var c = KadoBoard(UnitCatalog.KadoKDr, out var k, out var a, out var b, out var small, out var big);
            var o1 = c.TakeTurn(k);
            bool before = o1 == TurnOutcome.Skill && k.RawCounter(ThornGuardTrait.PendingKey) == 1 && !ThornsBareTrait.IsBare(k) && Tly(c, "kado").Sh362Strikes == 0;
            Expect("KD-r: 剥く前は今どおり（HP が5割以上の手番は構え・身代わりの印が立つ・殴らない）", before, $"{o1}");
            k.Hp = 49; c.ApplyDamage(k, 1, null);
            bool edge = !ThornsBareTrait.IsBare(k);
            k.Hp = 48; c.ApplyDamage(k, 1, null);
            bool bared = ThornsBareTrait.IsBare(k) && k.RawCounter(ThornGuardTrait.PendingKey) == 0 && Tly(c, "kado").Sh362Bares == 1 && Tly(c, "kado").Sh362Strikes == 0;
            Expect("KD-r: 5割を切った瞬間に剥く（HP 48 ／ 96 ではまだ・47 で剥く）・構え（身代わりの印）を捨てる・剥いた瞬間は殴らない", edge && bared, $"HP {k.Hp}");
            int w = 11 * 2 + (96 - 47);
            int bigHp = big.Hp, smallHp = small.Hp, aHp = a.Hp, bHp = b.Hp;
            var o2 = c.TakeTurn(k);
            var kt = Tly(c, "kado");
            bool hit = o2 == TurnOutcome.Attack && kt.Sh362Strikes == 1 && kt.Sh362StrikeSum == w && bigHp - big.Hp == w;
            Expect($"KD-r: 次の手番から殴りかかる（手番は攻撃）・最も手強い敵（攻 20 の big）へ 攻撃力 × 2 ＋ 傷 ＝ {w}", hit, $"{o2}・重さ {kt.Sh362StrikeSum}・big −{bigHp - big.Hp}・small −{smallHp - small.Hp}");
            bool splash = smallHp - small.Hp == w * ThornsTrait.SplashPercent / 100 && aHp > a.Hp && bHp > b.Hp && kt.Sh362ThornGain > 0 && k.AtkBonus == kt.Sh362ThornGain;
            Expect("KD-r: 殴りかかりも反撃と同じく隣の敵に届き（6 割）、隣の味方を巻き込み、攻撃力が伸びる", splash, $"small −{smallHp - small.Hp}・a −{aHp - a.Hp}・b −{bHp - b.Hp}・伸び {kt.Sh362ThornGain}");
            bool noGuard = k.RawCounter(ThornGuardTrait.PendingKey) == 0;
            k.Hp = k.MaxHp;
            var o3 = c.TakeTurn(k);
            bool keep = o3 == TurnOutcome.Attack && ThornsBareTrait.IsBare(k) && Tly(c, "kado").Sh362Strikes == 2 && k.RawCounter(ThornGuardTrait.PendingKey) == 0;
            Expect("KD-r: 身代わりしない（剥いた後の手番で構えの印が立たない）・回復して5割を超えても戻らない（次の手番も殴りかかる）", noGuard && keep, $"{o3}");
            long rip0 = Tly(c, "kado").Sh362RiposteSum;
            c.ApplyDamage(k, 5, small);
            Expect("KD-r: 剥いた後も反撃（棘）は続く", Tly(c, "kado").Sh362RiposteSum > rip0 && Tly(c, "kado").ThornRipostes == 1, $"反撃の重さ {Tly(c, "kado").Sh362RiposteSum - rip0}");
            var c2 = KadoBoard(UnitCatalog.KadoH362, out var k2, out _, out _, out _, out _);
            k2.Hp = 20; c2.ApplyDamage(k2, 1, null);
            var o4 = c2.TakeTurn(k2);
            Expect("規定のカド: HP が5割を切っても今どおり構える（剥かない・殴らない）", o4 == TurnOutcome.Skill && !ThornsBareTrait.IsBare(k2) && Tly(c2, "kado").Sh362Strikes == 0, $"{o4}");
        }

        // ---- 台の上 ----
        {
            int diff = 0, n = 0, bareEv = 0, bareTl = 0, baseEv = 0;
            foreach (var (name, f0) in KadoRows().Take(3))
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int seed = 0; seed < 2; seed++)
                        {
                            var f = Apply(f0, v);
                            var c = Fight(f, w, seed);
                            var (r, _, _) = I.Raw(f, w, seed);
                            var (rv, _, _) = I.Raw(f, w, seed, verbose: true);
                            n++;
                            if ((c.Wins == 1) != r.PlayerWon || r.PlayerWon != rv.PlayerWon || r.Turns != rv.Turns || (c.Wins == 1 && c.WinT != r.Turns)) diff++;
                            int ev = rv.Events.Count(x => x.Kind == BattleEventKind.ThornsBare);
                            if (v == Vers[0]) baseEv += ev; else { bareEv += ev; bareTl += rv.TallyByUnit.TryGetValue("kado", out var kt) ? (int)kt.Sh362Bares : 0; }
                        }
            Expect("verbose の有無 ／ 計数の有無で勝敗 ／ T が同じ（規定 ／ KD-t ／ KD-r・カドの行 3 × ボス ／ 近衛 ／ 大隊 ／ 本編5・seed 0..1）", diff == 0, $"{n} 戦・差 {diff}");
            Expect("棘を剥く出来事（表示専用）は剥いた回数と同じだけ出る・規定のカドには出ない", bareEv == bareTl && baseEv == 0, $"出来事 {bareEv}・帳簿 {bareTl}・規定 {baseEv}");
        }

        // ---- 試遊の台 ----
        {
            // 第363期: 「版・」の3台は `Presets.Versions` から外した。第362期の3台は `Boards362` に固定して確かめる。
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            string[] want = { "版・KD-t 自傷", "版・KD-r 自傷", "版・KD-r 自傷 リリ" };
            bool rows = vr.Take(6).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片" }) && !vr.Any(want.Contains)
                && !vr.Any(x => x.Contains("SS-s") || x.Contains("SS-n"));
            Expect("`Presets.Versions` ＝ 試遊の6台（「試遊・破片」を含む）が頭に残る・第362期の「版・」の3台は第363期に外した", rows, string.Join("、", vr));
            if (rows)
            {
                var b362 = Boards362();
                Formation F(string nm) => b362.First(r => r.Name == nm).F;
                bool ok = F(want[0]).Occupied().Any(o => o.Def == UnitCatalog.KadoKDt) && F(want[1]).Occupied().Any(o => o.Def == UnitCatalog.KadoKDr) && F(want[2]).Occupied().Any(o => o.Def == UnitCatalog.KadoKDr)
                    && Has(F(want[2]), "lili") && !Has(F(want[1]), "lili") && Seats(F(want[0])) == Seats(F(want[1])) && Has(F(want[0]), "susu") && Has(F(want[2]), "susu");
                Expect("「版・」の3台: KD-t ／ KD-r は同じ並び（リリなし）・リリの台はリリを含む・どれもアカを含む", ok, $"{Seats(F(want[1]))} ／ {Seats(F(want[2]))}");
            }
        }

        // ---- PickOne ----
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 0d943ac3:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs"), r1 = File.ReadAllText("BattleCore/Traits.cs").Split("Roll(").Length - 1;
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh362*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第361期（0d943ac3）と同じ・Traits.cs の `Roll(` も同じ・器具の中も 0）", e0 == e1 && t0 == t1 && r0 == r1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・Roll {r0} → {r1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
