using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh361 の自己検査（第361期・指示書 §10）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿で確かめる（盤面は動かさない）。
static partial class Sh361Diag
{
    /// <summary>「試遊・破片」に選んだ並び（`seats` の規定の両立の最良・報告 §5）。</summary>
    internal const string PlayKey = "ネル・ササ・ソム・ヒビ・ガレ";   // 規定（SS-w）・SS-s ・SS-n のどれでも「両立の最良」の1位（勇者 40〜60%・近衛 ／ 大隊 100%）

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

    /// <summary>ササを前1 に、相手を前1 と隣でない席に置いた盤面（隣の味方がいない）。<paramref name="near"/> を足すと、ササの隣にもう1体置く。</summary>
    static BattleContext Lone(UnitDef sasa, UnitDef other, out UnitState s, out UnitState o, out UnitState foe, UnitDef? near = null)
    {
        int far = Enumerable.Range(1, 4).First(k => !FormationRules.AreAdjacent(0, k));
        int adj = Enumerable.Range(1, 4).First(k => FormationRules.AreAdjacent(0, k));
        var pl = new Formation(); pl[0] = sasa; pl[far] = other; if (near is not null) pl[adj] = near;
        var ctx = new BattleContext(0, false);
        var p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(Formation.Build(center: Plain("foe", hp: 999)), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        s = p.First(u => u.Def.Id == "sasa"); o = p.First(u => u.Slot == far); foe = e[0];
        foreach (var u in p) u.SetCounter(StatusKeys.Armor, 0);
        TraitCatalog.Get(TraitId.Brace).OnAction(ctx, s, new UnitAction(ActionKind.Skill));
        return ctx;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh361 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            bool def = UnitCatalog.Sasa.Traits.SequenceEqual(UnitCatalog.SasaSSw.Traits) && UnitCatalog.Sasa.PlusText == UnitCatalog.SasaSSw.PlusText
                && UnitCatalog.SasaH360.Traits.SequenceEqual(new[] { TraitId.Brace }) && UnitCatalog.All.Contains(UnitCatalog.Sasa) && !UnitCatalog.All.Contains(UnitCatalog.SasaH360)
                && ReferenceEquals(PinHB359(UnitCatalog.Sasa), UnitCatalog.SasaH360) && ReferenceEquals(PinSa360(UnitCatalog.Sasa), UnitCatalog.SasaH360);
            Expect("段0: 規定のササ ＝ SS-w（札 ／ 文面が `SasaSSw` と同じ）・旧（`SasaH360`）は身構えだけ・`All` は規定だけ・古い口の終端からササも旧へ", def);
            (int Held, int Nb) Run(UnitDef sasa)
            {
                var pl = Formation.Build(front1: Plain("a"), front3: Plain("b"), center: sasa, back1: Plain("c"), back3: Plain("d"));
                var ctx = new BattleContext(0, false);
                var p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
                var e = BattleEngine.Materialize(Formation.Build(center: Plain("foe", hp: 999)), BattleContext.EnemyTeam, EnemyScaleRule.None);
                foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
                foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
                TurnP.SetValue(ctx, 2);
                var s = p.First(u => u.Def.Id == "sasa");
                foreach (var u in p) u.SetCounter(StatusKeys.Armor, 0);
                s.SetCounter(BraceTrait.BracedKey, 2);   // 前のターンに固めた（下ごしらえ）
                int h0 = s.Hp; ctx.ApplyDamage(s, 20, e[0]);
                return (h0 - s.Hp, p.Count(u => u != s && Armor(u) > 0));
            }
            var now = Run(UnitCatalog.Sasa); var old = Run(UnitCatalog.SasaH360);
            Expect("段0: 規定のササは前のターンの構えで次の一撃も 7 に抑え、切り落とした分を隣の味方全員（中央なら4体）に配る・旧の定義では今までどおり（素のまま受け、配らない）",
                now.Held == 7 && now.Nb == 4 && old.Held == 20 && old.Nb == 0, $"規定: 受けた {now.Held}・配った先 {now.Nb} ／ 旧: 受けた {old.Held}・配った先 {old.Nb}");
        }

        // ---- 版の定義 ----
        {
            bool ok = UnitCatalog.SasaSSs.Traits.SequenceEqual(UnitCatalog.Sasa.Traits.Append(TraitId.BraceScatter)) && UnitCatalog.SasaSSn.Traits.SequenceEqual(UnitCatalog.Sasa.Traits.Append(TraitId.BraceNear));
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.BraceScatter or TraitId.BraceNear)).Select(d => d.Id).ToArray();
            Expect("版の定義: SS-s ＝ 規定のササ ＋ `BraceScatter` ／ SS-n ＝ ＋ `BraceNear`・新しい札の保持者は 0 枚", ok && held.Length == 0, held.Length == 0 ? "" : string.Join(",", held));
        }

        // ---- SS-s ----
        {
            var c1 = Lone(UnitCatalog.SasaSSs, UnitCatalog.Gare, out var s1, out var g1, out var f1);
            int fh = f1.Hp;
            c1.ApplyDamage(s1, 20, f1);
            int peb = g1.RawCounter(PebbleTrait.Key);
            Expect("SS-s: 隣に誰もいないとき、切り落とした 13 が砕け散り、礫を拾う駒（ガレ）が 13 拾う・ササの破片は増えない・返しは起こさない（敵の HP は変わらない）",
                peb == 13 && Armor(s1) == 0 && f1.Hp == fh && Tly(c1, "sasa").Sh361Scattered == 13, $"礫 {peb}・ササの破片 {Armor(s1)}・敵 {fh} → {f1.Hp}");
            var c2 = Lone(UnitCatalog.SasaSSs, UnitCatalog.Tsugi, out var s2, out var t2, out var f2);
            c2.ApplyDamage(s2, 20, f2);
            Expect("SS-s: 瓦礫拾い（ツギ）も同じく拾う（`NoteArmorLost` と同じ口・砕けた量 13）", Tly(c2, "tsugi").ScrapArmor == 13, $"瓦礫に数えた量 {Tly(c2, "tsugi").ScrapArmor}");
            var c3 = Lone(UnitCatalog.SasaSSs, Plain("x"), out var s3, out var x3, out var f3);
            c3.ApplyDamage(s3, 20, f3);
            Expect("SS-s: 読み手がいなければ消える（誰にも付かない）", Armor(x3) == 0 && Tly(c3, "sasa").Sh361Scattered == 13 && Tly(c3, "sasa").Sh361ScatterPebble == 0 && Tly(c3, "sasa").Sh361ScatterScrap == 0);
            var c4 = Lone(UnitCatalog.Sasa, UnitCatalog.Gare, out var s4, out var g4, out var f4);
            c4.ApplyDamage(s4, 20, f4);
            Expect("規定のササ（SS-w）: 隣に誰もいなければ散らない（礫にならない・今どおり消える）", g4.RawCounter(PebbleTrait.Key) == 0 && Tly(c4, "sasa").Sh361Scattered == 0);
        }

        // ---- SS-n ----
        {
            var c1 = Lone(UnitCatalog.SasaSSn, Plain("x"), out var s1, out var x1, out var f1);
            c1.ApplyDamage(s1, 20, f1);
            Expect("SS-n: 隣に誰もいないとき、いちばん近い味方（隣でない席の1体）へ 13 付く・ササ自身には付かない", Armor(x1) == 13 && Armor(s1) == 0, $"遠い味方 {Armor(x1)}・ササ {Armor(s1)}");
            var c2 = Lone(UnitCatalog.SasaSSn, Plain("x"), out var s2, out var x2, out var f2, near: Plain("n"));
            c2.ApplyDamage(s2, 20, f2);
            var n2 = c2.LivingMembers(BattleContext.PlayerTeam).First(u => u.Def.Id == "n");
            Expect("SS-n: 隣がいればいまどおり隣へ（遠くへは付けない）", Armor(n2) == 13 && Armor(x2) == 0, $"隣 {Armor(n2)}・遠い {Armor(x2)}");
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
            Expect("verbose の有無 ／ 計数の有無で勝敗 ／ T が同じ（SS-s ／ SS-n・代表台 × ボス ／ 近衛 ／ 大隊 ／ 本編5・seed 0..1）", diff == 0, $"{n} 戦・差 {diff}");
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            string[] want = { "試遊・破片", "版・SS-s 破片", "版・SS-n 破片" };
            bool rows = vr.Take(5).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁" }) && vr.Skip(5).SequenceEqual(want);
            Expect("`Presets.Versions` ＝ 試遊の5台 ＋ 「試遊・破片」＋ 「版・」の2台（第360期の「版・」の3台は外した）", rows, string.Join("、", vr));
            if (rows)
            {
                Formation F(string nm) => Presets.Versions.First(r => r.Name == nm).F;
                bool ok = want.All(nm => Seats(F(nm)) == PlayKey) && F(want[0]).Occupied().All(o => UnitCatalog.All.Contains(o.Def)) && !Has(F(want[0]), "tsugi")
                    && F(want[1]).Occupied().Any(o => o.Def == UnitCatalog.SasaSSs) && F(want[2]).Occupied().Any(o => o.Def == UnitCatalog.SasaSSn);
                Expect("「試遊・破片」＝ ツギなしの並び（規定の駒）・「版・」の2台は同じ並びでササだけ版", ok, Seats(F(want[0])));
            }
        }

        // ---- PickOne ----
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 754128b6:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh361*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第360期（754128b6）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
