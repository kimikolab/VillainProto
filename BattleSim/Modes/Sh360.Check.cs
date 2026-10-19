using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh360 の自己検査（第360期・指示書 §10）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿で確かめる（盤面は動かさない）。
static partial class Sh360Diag
{
    /// <summary>§7 の代表台に足した §5 の 2 ／ 3（`seats` の結果から選んだ並び・報告 §1 の決めたこと）。</summary>
    static readonly (string Label, string Key)[] RepKeys =
    {
        // SS-w のネルを含まない最良（ソムなし）は C と同じ並び（ササ・ゴルム・ドルガ・ガレ・ヒビ）なので足さない。
        ("F SS-w のネルを含む最良（ソムなし）", "バン・ササ・ネル・ヒビ・ガレ"),   // `seats` の SS-w §4 の1位（ボス 20%・近衛 ／ 大隊 100%）
        ("G SS-w の1位（ネル＋ソム）", "ヒビ・ガレ・ササ・ソム・ネル"),          // `seats` の SS-w §2 の1位 ＝ ネルを含む最良（ボス 100%・12.8T・近衛 ／ 大隊 0%）
    };

    /// <summary>第360期の「版・」の3台（第361期に `Presets.Versions` から外した並びを器具の中に固定）。</summary>
    internal static (string Name, Formation F)[] Boards360() => new (string, Formation)[]
    {
        ("版・SS-k 破片", Formation.Build(front1: UnitCatalog.SasaSSk, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Gare, back3: UnitCatalog.Hibi)),
        ("版・SS-w 破片", Formation.Build(front1: UnitCatalog.SasaSSw, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Gare, back3: UnitCatalog.Hibi)),
        ("版・SS-w 破片 呪い", Formation.Build(front1: UnitCatalog.Hibi, front3: UnitCatalog.Gare, center: UnitCatalog.SasaSSw, back1: UnitCatalog.Som, back3: UnitCatalog.Nel)),
    };

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e, int turn = 1)
    {
        var ctx = new BattleContext(0, false);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, turn);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 200, int atk = 10) => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static UnitTally Tly(BattleContext c, string id) => ((Dictionary<string, UnitTally>)typeof(BattleContext)
        .GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!).GetValueOrDefault(id) ?? new UnitTally();
    static readonly UnitAction Skill = new(ActionKind.Skill);
    static int Armor(UnitState u) => u.RawCounter(StatusKeys.Armor);
    static int Hit(BattleContext ctx, UnitState u, UnitState? from, int amt, bool burn = false) { int h = u.Hp; ctx.ApplyDamage(u, amt, from, burnTick: burn); return h - u.Hp; }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh360 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            bool def = UnitCatalog.Hibi.Traits.SequenceEqual(UnitCatalog.HibiHBf.Traits) && UnitCatalog.Hibi.MaxHp == UnitCatalog.HibiHBf.MaxHp && UnitCatalog.Hibi.PlusText == UnitCatalog.HibiHBf.PlusText
                && UnitCatalog.HibiH359.Traits.SequenceEqual(new[] { TraitId.Shatter, TraitId.Frail }) && UnitCatalog.All.Contains(UnitCatalog.Hibi) && !UnitCatalog.All.Contains(UnitCatalog.HibiH359)
                && ReferenceEquals(PinR358(UnitCatalog.Hibi), UnitCatalog.HibiH359) && ReferenceEquals(PinHB359(UnitCatalog.Hibi), UnitCatalog.HibiH359);
            Expect("段0: 規定のヒビ ＝ HB-f（札・HP 160・文面が `HibiHBf` と同じ）・旧（`HibiH359`）は砕け ＋ 脆弱・`All` は規定だけ・古い口の終端からヒビも旧へ", def);
            int Back(UnitDef hibi)
            {
                var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: hibi, back1: Plain("c"), back3: Plain("d")), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
                var h = U(p, "hibi");
                foreach (var t in h.Traits) if (t.CanAct(ctx, h, ActionKind.Skill)) t.OnAction(ctx, h, Skill);
                int f0 = e[0].Hp;
                ctx.ApplyDamage(U(p, "a"), 20, e[0]);
                return f0 - e[0].Hp;
            }
            int now = Back(UnitCatalog.Hibi), old = Back(UnitCatalog.HibiH359);
            Expect("段0: 規定のヒビで全量の返しが働き（20 の一撃で 20 を返す）、旧の定義では働かない（0）", now == 20 && old == 0, $"規定 {now} ／ 旧 {old}");
        }
        {
            // ツギの駆け込み: 板の付かない駒（規定のヒビ）へは駆け込まない・旧のヒビへは今どおり
            (int Armor, long Skipped) Aid(UnitDef hibi)
            {
                var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: UnitCatalog.Tsugi, center: hibi, back1: Plain("c"), back3: Plain("d")), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
                foreach (var u in p) { u.SetCounter(StatusKeys.Armor, 0); u.SetCounter(StatusKeys.Plank, 0); }
                var h = U(p, "hibi");
                h.Hp = h.MaxHp / 3;   // 下ごしらえ: 4割を切る
                ctx.ApplyDamage(h, 5, e[0]);
                return (p.Where(u => u != h).Sum(Armor) + Armor(h), Tly(ctx, "tsugi").Sh360AidSkipped);
            }
            var now = Aid(UnitCatalog.Hibi); var old = Aid(UnitCatalog.HibiH359);
            Expect("段0: ツギの駆け込みは板の付かない規定のヒビへ行かない（ほかに宛先が無いので駆け込まない）・旧のヒビへは今どおり駆け込む", now.Armor == 0 && now.Skipped == 1 && old.Armor > 0 && old.Skipped == 0,
                $"規定: 板 {now.Armor}・外した {now.Skipped} ／ 旧: 板 {old.Armor}");
            int Open(UnitDef hibi, out int other)
            {
                var ctx = Ctx(Formation.Build(front1: hibi, front3: Plain("b"), center: UnitCatalog.Tsugi, back1: Plain("c"), back3: Plain("d")), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
                foreach (var u in p) { u.SetCounter(StatusKeys.Armor, 0); u.SetCounter(StatusKeys.Plank, 0); }
                TraitCatalog.Get(TraitId.PlankOpening).OnBattleStart(ctx, U(p, "tsugi"));
                other = Armor(U(p, "b"));
                return U(p, "hibi").RawCounter(StatusKeys.Plank);
            }
            int pn = Open(UnitCatalog.Hibi, out int bn), po = Open(UnitCatalog.HibiH359, out int bo);
            Expect("段0（決めたこと）: 出撃前の板も前列の規定のヒビには貼らない（同じ前列の味方には今どおり）・旧のヒビには今どおり貼る", pn == 0 && bn > 0 && po != 0 && bo > 0, $"規定: ヒビの印 {pn}・前3 の板 {bn} ／ 旧: ヒビの印 {po}");
        }

        // ---- 版の定義 ----
        {
            var k = UnitCatalog.SasaSSk; var w = UnitCatalog.SasaSSw; var s = UnitCatalog.SasaH360;
            bool ok = k.Traits.SequenceEqual(new[] { TraitId.Brace, TraitId.BraceHold }) && w.Traits.SequenceEqual(new[] { TraitId.Brace, TraitId.BraceHold, TraitId.BraceWide })
                && new[] { k, w }.All(d => d.Id == s.Id && d.MaxHp == s.MaxHp && d.Attack == s.Attack && d.Speed == s.Speed && d.Actions == s.Actions)
                && s.Traits.SequenceEqual(new[] { TraitId.Brace });
            // 第361期: 規定のササ ＝ SS-w（`BraceHold` ／ `BraceWide` の保持者はササだけ）
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.BraceHold or TraitId.BraceWide) && d.Id != "sasa").Select(d => d.Id).ToArray();
            ok = ok && UnitCatalog.Sasa.Traits.SequenceEqual(w.Traits) && UnitCatalog.Sasa.PlusText == w.PlusText;
            Expect("版の定義: SS-k ＝ 身構え ＋ `BraceHold` ／ SS-w ＝ ＋ `BraceWide`（体 ／ 手番はササのまま）・旧のササ（`SasaH360`）は身構えだけ・第361期: 規定のササ ＝ SS-w（札 ／ 文面が同じ・ほかに保持者なし）", ok && held.Length == 0, held.Length == 0 ? "" : string.Join(",", held));
        }

        // ---- SS-k ----
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: UnitCatalog.SasaSSk, center: Plain("b"), back1: Plain("c"), back3: Plain("d")), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e, turn: 2);
            var s = U(p, "sasa");
            s.SetCounter(BraceTrait.BracedKey, 2);   // 前のターン（T1）に固めた（下ごしらえ）
            int d1 = Hit(ctx, s, e[0], 20);
            int dp = Hit(ctx, s, null, 20);          // 毒の刻み（出どころなし）
            int db = Hit(ctx, s, e[0], 20, burn: true);   // 燃焼の刻み
            Expect("SS-k: 前のターンに固めた構えが、このターンの自分の手番の前に来る敵の一撃も 7 に抑える（20 → 7）・毒 ／ 燃焼の刻みには効かない（20 ／ 20）", d1 == 7 && dp == 20 && db == 20, $"一撃 {d1}・毒 {dp}・燃焼 {db}");
            TraitCatalog.Get(TraitId.Brace).OnAction(ctx, s, Skill);
            bool re = s.RawCounter(BraceTrait.BracedKey) == 3;
            TurnP.SetValue(ctx, 3);
            int d3 = Hit(ctx, s, e[0], 20);
            TurnP.SetValue(ctx, 4);
            int d4 = Hit(ctx, s, e[0], 20);
            Expect("SS-k: 自分の手番の頭で固め直す（T2 に固め直した構えは T3 の一撃にも効く）・2ターン前の構えは効かない", re && d3 == 7 && d4 == 20, $"T3 {d3}・T4 {d4}");
            s.SetCounter(BraceTrait.BracedKey, 4);
            ctx.ApplyDamage(s, 999, null);   // 出どころの無い一撃（構えは効かない）で倒す
            Expect("SS-k: 倒れたら構えが解ける", !s.IsAlive && s.RawCounter(BraceTrait.BracedKey) == 0);
            var ctx0 = Ctx(Formation.Build(front1: Plain("a"), front3: UnitCatalog.SasaH360, center: Plain("b")), Formation.Build(center: Plain("foe", hp: 999)), out var p0, out var e0, turn: 2);
            var s0 = U(p0, "sasa"); s0.SetCounter(BraceTrait.BracedKey, 2);
            Expect("規定のササ: 前のターンの構えはこのターンの一撃に効かない（今どおり）", Hit(ctx0, s0, e0[0], 20) == 20);
        }

        // ---- SS-w ----
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: UnitCatalog.SasaSSw, back1: Plain("c"), back3: Plain("d")), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
            var s = U(p, "sasa");
            foreach (var u in p) u.SetCounter(StatusKeys.Armor, 0);
            var nbs = p.Where(u => u != s && FormationRules.AreAdjacent(s, u)).ToList();
            TraitCatalog.Get(TraitId.Brace).OnAction(ctx, s, Skill);
            ctx.ApplyDamage(s, 20, e[0]);
            bool same = nbs.Count >= 2 && nbs.All(u => Armor(u) == 13) && p.Where(u => u != s && !nbs.Contains(u)).All(u => Armor(u) == 0);
            ctx.ApplyDamage(s, 20, e[0]);
            bool once = Tly(ctx, "sasa").BraceShoves == 1;
            Expect($"SS-w: 切り落とした分（13）が、殴られる前の席の隣の味方それぞれに同じ量付く（隣 {nbs.Count} 体）・弾くのは1ターン1回のまま", same && once,
                $"{string.Join(" ", p.Select(u => $"{u.Def.Id}:{Armor(u)}"))}・弾いた {Tly(ctx, "sasa").BraceShoves}");
            var ctx1 = Ctx(Formation.Build(front1: UnitCatalog.SasaSSw, back3: Plain("d"), center: Plain("b")), Formation.Build(center: Plain("foe", hp: 999)), out var p1, out var e1);
            var s1 = U(p1, "sasa");
            foreach (var u in p1) u.SetCounter(StatusKeys.Armor, 0);
            var nb1 = p1.Where(u => u != s1 && FormationRules.AreAdjacent(s1, u)).ToList();
            TraitCatalog.Get(TraitId.Brace).OnAction(ctx1, s1, Skill);
            ctx1.ApplyDamage(s1, 20, e1[0]);
            Expect("SS-w: 隣が1人ならその1人だけに付く", nb1.Count == 1 && Armor(nb1[0]) == 13 && p1.Where(u => u != s1 && u != nb1[0]).All(u => Armor(u) == 0),
                $"隣 {string.Join(",", nb1.Select(u => u.Def.Id))}・{string.Join(" ", p1.Select(u => $"{u.Def.Id}:{Armor(u)}"))}");
        }

        // ---- 台の上 ----
        {
            int diff = 0, n = 0;
            foreach (var b in Boards())
                foreach (var v in Vers.Skip(1))
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int seed = 0; seed < 2; seed++)
                        {
                            var f = Apply(b.F, v);
                            var c = Sh359Diag.Fight(f, w, seed);
                            var (r, _, _) = I.Raw(f, w, seed);
                            var (rv, _, _) = I.Raw(f, w, seed, verbose: true);
                            n++;
                            if ((c.Wins == 1) != r.PlayerWon || r.PlayerWon != rv.PlayerWon || r.Turns != rv.Turns || (c.Wins == 1 && c.WinT != r.Turns)) diff++;
                        }
            Expect("verbose の有無 ／ 計数の有無で勝敗 ／ T が同じ（SS-k ／ SS-w・代表台 × ボス ／ 近衛 ／ 大隊 ／ 本編5・seed 0..1）", diff == 0, $"{n} 戦・差 {diff}");
            var veil = BraceOf(ByKey("ソム・ササ・ドルガ・ガレ・ヒビ"), I.Bat, 10);
            Expect("ソムの光の衣は破片（`StatusKeys.Armor`）なので、敵の一撃で割れた衣もガレの礫になる（礫 ＝ 敵の一撃で割れた破片の延べ・衣が受け止めた量 ＞ 0）",
                veil.VeilSoaked > 0 && veil.Picked == veil.Broken && veil.Broken > 0, $"衣が受け止めた {Per(veil.VeilSoaked, veil.N)}・割れた破片 {Per(veil.Broken, veil.N)}・礫 {Per(veil.Picked, veil.N)}（／戦）");
        }

        // ---- 試遊の台 ----
        // 第361期: 第360期の「版・」の3台は `Presets.Versions` から外した。器具の中に旧の並びを固定して確かめる。
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            string[] want = { "版・SS-k 破片", "版・SS-w 破片", "版・SS-w 破片 呪い" };
            bool gone = vr.Take(5).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁" }) && !vr.Any(want.Contains);
            Expect("`Presets.Versions` ＝ 試遊の5台が頭に残る・第360期の「版・」の3台は第361期に外した", gone, string.Join("、", vr));
            var b = Boards360();
            bool ok = Seats(b[0].F) == KeyC && Seats(b[1].F) == KeyC && Seats(b[2].F) == RepKeys[1].Key
                && b[0].F.Occupied().Any(o => o.Def == UnitCatalog.SasaSSk) && b[1].F.Occupied().Any(o => o.Def == UnitCatalog.SasaSSw) && b[2].F.Occupied().Any(o => o.Def == UnitCatalog.SasaSSw);
            Expect("第360期の「版・」の台（器具の中に固定）: 並びは §5 のネルを含まない最良（呪いの台はネルを含む最良）・ササだけが版", ok, $"{Seats(b[0].F)} ／ {Seats(b[2].F)}");
        }

        // ---- PickOne ----
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 72336448:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh360*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第359期（72336448）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
