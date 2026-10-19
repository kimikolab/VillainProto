using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh359 の自己検査（第359期・指示書 §10）。版の規則は盤面を直に組んで1回ずつ確かめ、台の上では帳簿と台本で確かめる（盤面は動かさない）。
static partial class Sh359Diag
{
    /// <summary>§7 の代表台に足した §5 の 2 ／ 3（`seats` の結果から選んだ並び・報告 §1 の決めたこと）。</summary>
    static readonly (string Label, string Key)[] RepKeys =
    {
        ("C HB-f のツギなしの最良", "ササ・ゴルム・ドルガ・ガレ・ヒビ"),   // `seats` の HB-f §3 の1位（＝ ツギなし ／ ネルなしの1位・ボス 0%・近衛 ／ 大隊 100%）
        ("D HB-f のネルを含む最良", "ツギ・ゴルム・ガレ・ネル・ヒビ"),     // `seats` の HB-f §4 の1位（＝ 全体の1位・ボス 100%・10.0T）
    };

    /// <summary>第359期の「版・」の3台（第360期に `Presets.Versions` から外した並びを器具の中に固定）。</summary>
    internal static (string Name, Formation F)[] Boards359() => new (string, Formation)[]
    {
        ("版・HB-s 破片", Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Gare, back3: UnitCatalog.HibiHBs)),
        ("版・HB-f 破片", Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Gare, back3: UnitCatalog.HibiHBf)),
        ("版・HB-f 破片 呪い", Formation.Build(front1: UnitCatalog.Tsugi, front3: UnitCatalog.Golm, center: UnitCatalog.Gare, back1: UnitCatalog.Nel, back3: UnitCatalog.HibiHBf)),
    };

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly MethodInfo StartM = typeof(Trait).GetMethod("OnBattleStart")!;
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
    static int Key(UnitState u) => u.RawCounter(ShardGiftTrait.Key);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh359 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            bool def = UnitCatalog.Gare.Traits.SequenceEqual(new[] { TraitId.PebbleGather }) && UnitCatalog.GareH358.Traits.SequenceEqual(new[] { TraitId.Shrapnel })
                && UnitCatalog.All.Contains(UnitCatalog.Gare) && !UnitCatalog.All.Contains(UnitCatalog.GareH358) && UnitCatalog.Gare.PlusText == UnitCatalog.GareGRr.PlusText
                && UnitCatalog.All.Where(d => d.Traits.Contains(TraitId.PebbleGather)).Select(d => d.Id).SequenceEqual(new[] { "gare" })
                && ReferenceEquals(PinR358(UnitCatalog.Gare), UnitCatalog.GareH358) && ReferenceEquals(PinN355(UnitCatalog.Gare), UnitCatalog.GareH358);
            Expect("段0: 規定のガレ ＝ GR-r（`PebbleGather`・文面も第358期の版と同じ）・旧（`GareH358`）は礫（砕く）・`All` は規定だけ・古い口の終端（`PinN355`）からガレも旧へ", def);
            // 規定のガレで礫を拾って投げ、旧の定義では砕く（直に組む盤面）
            bool Run(UnitDef gare, out string note)
            {
                var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: gare, back1: Plain("c"), back3: Plain("d")), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
                var a = U(p, "a"); var g = U(p, "gare");
                a.SetCounter(StatusKeys.Armor, 10);
                ctx.ApplyDamage(a, 7, e[0]);
                int peb = g.RawCounter(PebbleTrait.Key), a0 = Armor(a), f0 = e[0].Hp;
                foreach (var t in g.Traits) if (t.CanAct(ctx, g, ActionKind.Skill)) t.OnAction(ctx, g, Skill);
                note = $"礫 {peb}・味方の破片 {a0} → {Armor(a)}・敵 {f0} → {e[0].Hp}";
                return gare == UnitCatalog.Gare ? peb == 7 && Armor(a) == 3 && f0 - e[0].Hp == 7 * ctx.Shrapnel.Multiplier + g.CurrentAttack
                                                : peb == 0 && Armor(a) == 0 && f0 - e[0].Hp == 3 * ctx.Shrapnel.Multiplier + g.CurrentAttack;
            }
            bool now = Run(UnitCatalog.Gare, out var n1), old = Run(UnitCatalog.GareH358, out var n2);
            Expect("段0: 規定のガレは味方の破片が敵の一撃で割れた量（7）を礫にして投げる（味方の破片は残る）・旧の定義は最も破片を纏う味方を砕いて撃つ", now && old, $"規定 {n1} ／ 旧 {n2}");
        }

        // ---- 版の定義 ----
        {
            var s = UnitCatalog.HibiHBs; var f = UnitCatalog.HibiHBf;
            bool hs = s.Traits.SequenceEqual(new[] { TraitId.ShardGift, TraitId.ShardOpen, TraitId.ShardBareNoPlank }) && !s.Traits.Contains(TraitId.Frail) && s.MaxHp == UnitCatalog.HibiSturdyHp
                && s.Attack == UnitCatalog.HibiH359.Attack && s.Speed == UnitCatalog.HibiH359.Speed && s.Id == "hibi";
            bool hf = f.Traits.SequenceEqual(s.Traits.Append(TraitId.ShardFull)) && f.MaxHp == s.MaxHp;
            // 第360期: 規定のヒビ ＝ HB-f（札と HP が `HibiHBf` と同じ）。`ShardBare` の保持者は 0 枚、HB-f の4枚の保持者はヒビだけ。
            var held = UnitCatalog.All.Concat(UnitCatalog.Retired).Where(d => d.Traits.Any(t => t is TraitId.ShardBare)).Select(d => d.Id).ToArray();
            bool now = UnitCatalog.Hibi.Traits.SequenceEqual(f.Traits) && UnitCatalog.Hibi.MaxHp == f.MaxHp && UnitCatalog.Hibi.PlusText == f.PlusText
                && UnitCatalog.All.Where(d => d.Traits.Any(t => t is TraitId.ShardOpen or TraitId.ShardBareNoPlank or TraitId.ShardFull or TraitId.ShardGift)).Select(d => d.Id).SequenceEqual(new[] { "hibi" })
                && UnitCatalog.HibiH359.Traits.SequenceEqual(new[] { TraitId.Shatter, TraitId.Frail }) && ReferenceEquals(PinR358(UnitCatalog.Hibi), UnitCatalog.HibiH359);
            hf = hf && now;
            Expect($"版の定義: HB-s ＝ 脆弱なし・HP {UnitCatalog.HibiSturdyHp}（攻 ／ 速はヒビのまま）・配る ＋ 開戦の配り ＋ 纏えない・板も来ない ／ HB-f ＝ HB-s ＋ 全量を返す・第360期: 規定のヒビ ＝ HB-f（旧は `HibiH359`・古い口の終端からヒビも旧へ）・`ShardBare` の保持者は 0 枚", hs && hf && held.Length == 0, held.Length == 0 ? "" : string.Join(",", held));
        }

        // ---- HB-s ----
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: UnitCatalog.HibiHBs, back1: Plain("c"), back3: Plain("d")), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
            var hibi = U(p, "hibi");
            foreach (var t in hibi.Traits) StartM.Invoke(t, new object[] { ctx, hibi });
            bool open = p.Where(u => u != hibi).All(u => Armor(u) == ShardGiftTrait.Gift && Key(u) == ShardGiftTrait.Gift) && Armor(hibi) == 0 && Tly(ctx, "hibi").Sh359OpenGifts == 1;
            hibi.SetCounter(StatusKeys.Armor, 9);
            Expect($"HB-s: 戦の始まりに1回、ヒビ以外の味方 4 体へ欠片 {ShardGiftTrait.Gift} を配る・ヒビには付かない（書いても消える）・脆弱が無い（10 の一撃で HP −10）", open && Armor(hibi) == 0 && Hit(ctx, hibi, e[0], 10) == 10,
                string.Join(" ", p.Select(u => $"{u.Def.Id}:{Armor(u)}")));
            var a = U(p, "a"); int f0 = e[0].Hp;
            ctx.ApplyDamage(a, 20, e[0]);
            Expect("HB-s: 返す量は割れた量のまま（20 の一撃で欠片 6 が割れ、6 を返す）", f0 - e[0].Hp == ShardGiftTrait.Gift && Tly(ctx, "hibi").Sh359Full == 0, $"敵 {f0} → {e[0].Hp}");
        }
        static int Hit(BattleContext ctx, UnitState u, UnitState from, int amt) { int h = u.Hp; ctx.ApplyDamage(u, amt, from); return h - u.Hp; }

        // ---- HB-f ----
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: Plain("b"), center: UnitCatalog.HibiHBf, back1: Plain("c"), back3: Plain("d")), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
            var hibi = U(p, "hibi"); var a = U(p, "a"); var b = U(p, "b"); var foe = e[0];
            TraitCatalog.Get(TraitId.ShardGift).OnAction(ctx, hibi, Skill);
            int f0 = foe.Hp, a0 = a.Hp;
            ctx.ApplyDamage(a, 20, foe);
            bool full = f0 - foe.Hp == 20 && a0 - a.Hp == 20 - ShardGiftTrait.Gift && Armor(a) == 0;
            Expect("HB-f: 欠片が1でも割れたら一撃の全量（20）を返す・受けた側は欠片の分だけ止まり残りは HP へ（14）", full, $"敵 {f0} → {foe.Hp}・味方 HP {a0} → {a.Hp}");
            int f1 = foe.Hp;
            ctx.ApplyDamage(b, 2, foe);
            int f2 = foe.Hp;
            ctx.ApplyDamage(b, 3, foe);
            bool again = f1 - f2 == 2 && f2 - foe.Hp == 3 && Armor(b) == 1;
            Expect("HB-f: 欠片が割れ切らなければ次の一撃もまた全量を返す（2 → 2・3 → 3・欠片は 6 → 4 → 1）", again, $"敵 {f1} → {f2} → {foe.Hp}・b の破片 {Armor(b)}");
            int f3 = foe.Hp;
            ctx.ApplyDamage(a, 15, foe);   // a は欠片が尽きている
            var c = U(p, "c"); c.SetCounter(StatusKeys.Armor, 0); c.SetCounter(ShardGiftTrait.Key, 0); c.SetCounter(StatusKeys.Armor, 10);   // 欠片ではない破片（返しの印なし）
            ctx.ApplyDamage(c, 15, foe);
            Expect("HB-f: 欠片の無い駒（欠片が尽きた駒・欠片ではない破片だけの駒）は返さない", foe.Hp == f3, $"敵 {f3} → {foe.Hp}");
        }
        {
            // ツギの板と重なる: 全量の返しが出た一撃では板は返さない（敵へ返る計 ＝ 一撃の全量）
            var ctx = Ctx(Formation.Build(front1: Plain("a"), front3: UnitCatalog.Tsugi, center: UnitCatalog.HibiHBf, back1: Plain("c"), back3: Plain("d")), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
            var hibi = U(p, "hibi"); var foe = e[0]; var a = U(p, "a");
            foreach (var u in p) { u.SetCounter(StatusKeys.Armor, 0); u.SetCounter(StatusKeys.Plank, 0); }
            a.SetCounter(StatusKeys.Armor, 20); a.SetCounter(StatusKeys.Plank, PlankTrait.Plain | PlankTrait.Rebound);
            TraitCatalog.Get(TraitId.ShardGift).OnAction(ctx, hibi, Skill);
            int f0 = foe.Hp, hit = Armor(a) + 5;
            ctx.ApplyDamage(a, hit, foe);
            Expect("HB-f ＋ ツギの板: 板 20 ＋ 欠片 6 の駒が 31 の一撃を受けたら、返るのは全量 31 だけ（板の 20 を足さない・全量を超えない）", f0 - foe.Hp == hit && Tly(ctx, "hibi").Sh359FullPlankSkipped == 1, $"敵へ計 {f0 - foe.Hp}（一撃 {hit}）");
        }

        // ---- §4-3 ツギの板の宛先 ----
        {
            UnitState? Target(UnitDef hibi)
            {
                var ctx = Ctx(Formation.Build(front1: Plain("a", hp: 100), front3: UnitCatalog.Tsugi, center: hibi, back1: Plain("c", hp: 100), back3: Plain("d", hp: 100)), Formation.Build(center: Plain("foe", hp: 999)), out var p, out var e);
                foreach (var u in p) { u.SetCounter(StatusKeys.Armor, 0); u.SetCounter(StatusKeys.Plank, 0); }
                U(p, "hibi").Hp = 5;   // ヒビがいちばん危ない（下ごしらえ）
                U(p, "a").Hp = 50;
                TraitCatalog.Get(TraitId.Plank).OnAction(ctx, U(p, "tsugi"), Skill);
                return p.FirstOrDefault(u => u.RawCounter(StatusKeys.Plank) != 0);
            }
            var tr = Target(UnitCatalog.HibiHBr); var ts = Target(UnitCatalog.HibiHBs); var t0 = Target(UnitCatalog.HibiH359);
            Expect("§4-3: ツギの手番の板は「板も来ない」ヒビ（HB-s ／ HB-f）を外して次に危ない味方へ・HB-r と規定のヒビでは今どおり（いちばん危ないヒビへ）",
                ts?.Def.Id == "a" && tr?.Def.Id == "hibi" && t0?.Def.Id == "hibi", $"HB-s → {ts?.Def.Id}・HB-r → {tr?.Def.Id}・規定 → {t0?.Def.Id}");
        }

        // ---- 台の上・verbose ----
        {
            int diff = 0, n = 0;
            foreach (var b in Boards())
                foreach (var v in new[] { V("hbs"), V("hbf") })
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int seed = 0; seed < 2; seed++)
                        {
                            var f = Apply(b.F, v);
                            var c = Fight(f, w, seed);
                            var (r, _, _) = I.Raw(f, w, seed);
                            var (rv, _, _) = I.Raw(f, w, seed, verbose: true);
                            n++;
                            if ((c.Wins == 1) != r.PlayerWon || r.PlayerWon != rv.PlayerWon || r.Turns != rv.Turns || (c.Wins == 1 && c.WinT != r.Turns)) diff++;
                        }
            Expect("verbose の有無 ／ 計数の有無で勝敗 ／ T が同じ（HB-s ／ HB-f・代表台 × ボス ／ 近衛 ／ 大隊 ／ 本編5・seed 0..1）", diff == 0, $"{n} 戦・差 {diff}");
        }

        // ---- 試遊の台 ----
        // 第360期: 第359期の「版・」の3台は `Presets.Versions` から外した。器具の中に旧の並びを固定して、並びと版の駒だけを確かめる。
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            string[] want = { "版・HB-s 破片", "版・HB-f 破片", "版・HB-f 破片 呪い" };
            bool gone = vr.Take(5).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁" }) && !vr.Any(want.Contains);
            Expect("`Presets.Versions` ＝ 試遊の5台が頭に残る・第359期の「版・」の3台は第360期に外した", gone, string.Join("、", vr));
            var b = Boards359();
            bool ok = Seats(b[0].F) == RepKeys[0].Key && Seats(b[1].F) == RepKeys[0].Key && Seats(b[2].F) == RepKeys[1].Key
                && b[0].F.Occupied().Any(o => o.Def == UnitCatalog.HibiHBs) && b[1].F.Occupied().Any(o => o.Def == UnitCatalog.HibiHBf) && b[2].F.Occupied().Any(o => o.Def == UnitCatalog.HibiHBf);
            Expect("第359期の「版・」の台（器具の中に固定）: 並びは §5 のツギなしの最良（呪いの台はネルを含む最良）・ヒビだけが版", ok, $"{Seats(b[0].F)} ／ {Seats(b[2].F)}");
        }

        // ---- PickOne ----
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 8af61ea1:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Sh359*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第358期（8af61ea1）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
