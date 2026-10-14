using BattleCore;
using static Common;
using T323 = Tou323Diag;

// mark337 の自己検査（第337期）。盤を直に組み、標を付けた敵を ApplyDamage で倒す（`T323.Ctx`）。
static partial class Mark337Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Dummy = new()
    {
        Id = "dummy337", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    /// <summary>標が付いた瞬間の羽（MF）を持たない羽のミサ（第286〜298期の規定 `TomeMb`）——標を直に書いても羽が積まれない盤のため。＋羽 の札だけを足した写しも。</summary>
    static readonly UnitDef MisaQuiet = UnitCatalog.TomeMb;
    static readonly UnitDef MisaQuietF = new()
    {
        Id = UnitCatalog.TomeMb.Id, Name = UnitCatalog.TomeMb.Name, MaxHp = UnitCatalog.TomeMb.MaxHp, Attack = UnitCatalog.TomeMb.Attack, Speed = UnitCatalog.TomeMb.Speed,
        Traits = UnitCatalog.TomeMb.Traits.Append(TraitId.MarkInheritFeathers).ToArray(), PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly Formation Foes = Formation.Build(front1: Dummy, front3: Dummy, center: Dummy, back1: Dummy, back3: Dummy);

    /// <summary>ヒサ（版）＋ ミサ（羽の札の有無）の盤。敵は木偶 5 体。戻り値: ctx・ミサ・ヒサ・敵（席番号 0..4）。</summary>
    static (BattleContext Ctx, UnitState Misa, UnitState Hisa, List<UnitState> E) Board(UnitDef hisa, UnitDef misa)
    {
        var ctx = T323.Ctx(Formation.Build(back1: misa, back3: hisa), Foes, out var p, out var e);
        return (ctx, T323.Seat(p, 3), T323.Seat(p, 4), Enumerable.Range(0, 5).Select(i => T323.Seat(e, i)).ToList());
    }
    static int L(UnitState u) => u.RawCounter(StatusKeys.Marked);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# mark337 check —— 第337期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義と試遊の台
        {
            UnitDef H = UnitCatalog.HisaH337, M = UnitCatalog.Tome;   // 第338期: 版は第322〜337期の規定のヒサから作り直した
            bool vers = UnitCatalog.HisaMIb.Traits.SequenceEqual(H.Traits.Append(TraitId.MarkInheritSpread)) && UnitCatalog.HisaMIa.Traits.SequenceEqual(H.Traits.Append(TraitId.MarkInheritOne))
                && UnitCatalog.MisaMIf.Traits.SequenceEqual(M.Traits.Append(TraitId.MarkInheritFeathers))
                && UnitCatalog.HisaMIb.PlusText.EndsWith("。標を付けられた敵が倒れると、その恨みが周りの敵に散る", StringComparison.Ordinal)
                && UnitCatalog.HisaMIa.PlusText.EndsWith("。標を付けられた敵が倒れると、その恨みは隣の敵が背負う", StringComparison.Ordinal)
                && UnitCatalog.MisaMIf.PlusText.EndsWith("。散った恨みの数だけ、羽も増える", StringComparison.Ordinal)
                && new[] { UnitCatalog.HisaMIb, UnitCatalog.HisaMIa, UnitCatalog.MisaMIf }.All(d => !UnitCatalog.Everyone.Contains(d))
                && UnitCatalog.Everyone.All(d => !d.Traits.Any(t => t is TraitId.MarkInheritOne or TraitId.MarkInheritFeathers))
                && UnitCatalog.Everyone.Where(d => d.Traits.Contains(TraitId.MarkInheritSpread)).SequenceEqual(new[] { UnitCatalog.Hisa });   // 第338期: MI-b は規定のヒサの札
            Expect("版の定義: MI-b ／ MI-a ＝ 規定のヒサ ＋ 札1枚・＋ 羽 ＝ 規定のミサ ＋ 札1枚（文面は末尾に叩き台）・`Everyone` の外・保持者 0", vers);
            // 第338期: MI-b を規定にしたので「版・MI」の3台を外した——1行目（ポンの台）と、MI の版が試遊の台に残っていないことだけを見る
            var v = Presets.Versions;
            bool rows = v.Length >= 1 && v[0].Name == "試遊・感電 ポンの台" && Presets.Playtest.Length == 17
                && v.SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, UnitCatalog.HisaMIb) && !ReferenceEquals(o.Def, UnitCatalog.HisaMIa) && !ReferenceEquals(o.Def, UnitCatalog.MisaMIf));
            Expect("試遊の台: `Presets.Versions` の1行目は「試遊・感電 ポンの台」・第337期の「版・MI」の3台は第338期に外した・`Playtest` は 17 台のまま", rows);
        }

        // ---- MI-b: 層 3 の敵（前1）が倒れると、隣の生きている敵それぞれに ⌈3/2⌉ ＝ 2・隣でない敵は変わらない・羽は増えない
        {
            var (ctx, misa, hisa, e) = Board(UnitCatalog.HisaMIb, MisaQuiet);
            var nb = Enumerable.Range(1, 4).Where(i => FormationRules.AreAdjacent(e[0], e[i])).ToArray();
            e[0].SetCounter(StatusKeys.Marked, 3);
            int f0 = FeathersTrait.Count(misa);
            ctx.ApplyDamage(e[0], 99999, hisa);
            bool ok = !e[0].IsAlive && nb.Length > 0 && Enumerable.Range(1, 4).All(i => L(e[i]) == (nb.Contains(i) ? 2 : 0)) && FeathersTrait.Count(misa) == f0
                && T323.Tal(ctx, "hisa").Mi337Fires == 1 && T323.Tal(ctx, "hisa").Mi337Layers == 2 * nb.Length;
            Expect("MI-b: 層 3 の敵が倒れると、隣の生きている敵それぞれに ⌈3/2⌉ ＝ 2 が足される・隣でない敵は 0 のまま・羽は増えない", ok,
                $"隣 {string.Join(",", nb)}・層 {string.Join(" ／ ", e.Skip(1).Select(L))}・羽 {f0} → {FeathersTrait.Count(misa)}");

            // 連鎖: 層 2 を受けた隣が倒れると、⌈2/2⌉ ＝ 1 がその隣へ（2段目）
            var (ctx2, _, hisa2, e2) = Board(UnitCatalog.HisaMIb, MisaQuiet);
            e2[0].SetCounter(StatusKeys.Marked, 3);
            ctx2.ApplyDamage(e2[0], 99999, hisa2);
            int k = Enumerable.Range(1, 4).First(i => L(e2[i]) == 2);
            ctx2.ApplyDamage(e2[k], 99999, hisa2);
            Expect("MI-b: 引き継いだ標の敵が倒れるとまた散る（連鎖の長さ 2）", T323.Tal(ctx2, "hisa").Mi337Fires == 2 && T323.Tal(ctx2, "hisa").Mi337ChainMax == 2, $"引き継ぎ {T323.Tal(ctx2, "hisa").Mi337Fires}・連鎖 {T323.Tal(ctx2, "hisa").Mi337ChainMax}");

            // 隣がいなければ消える
            var (ctx3, _, hisa3, e3) = Board(UnitCatalog.HisaMIb, MisaQuiet);
            foreach (int i in Enumerable.Range(1, 4).Where(i => FormationRules.AreAdjacent(e3[0], e3[i]))) e3[i].Hp = 0;
            e3[0].SetCounter(StatusKeys.Marked, 3);
            ctx3.ApplyDamage(e3[0], 99999, hisa3);
            Expect("MI-b: 隣に生きている敵がいなければ層は消える（どこにも付かない）", T323.Tal(ctx3, "hisa").Mi337Lost == 3 && T323.Tal(ctx3, "hisa").Mi337Fires == 0 && e3.Skip(1).All(u => L(u) == 0),
                $"消えた {T323.Tal(ctx3, "hisa").Mi337Lost}");
        }

        // ---- MI-b ＋ 羽: 移った層の数だけ羽が増える
        {
            var (ctx, misa, hisa, e) = Board(UnitCatalog.HisaMIb, MisaQuietF);
            int nb = Enumerable.Range(1, 4).Count(i => FormationRules.AreAdjacent(e[0], e[i]));
            e[0].SetCounter(StatusKeys.Marked, 3);
            int f0 = FeathersTrait.Count(misa);
            ctx.ApplyDamage(e[0], 99999, hisa);
            Expect("MI-b ＋ 羽: 移った層（2 × 隣の数）の数だけミサの羽が増える", FeathersTrait.Count(misa) - f0 == 2 * nb && T323.Tal(ctx, "tome").Mi337Feathers == 2 * nb, $"羽 {f0} → {FeathersTrait.Count(misa)}（隣 {nb}）");
        }

        // ---- MI-a: 層をすべて、隣の層の最も多い1体へ（同じなら席の若い方）
        {
            var (ctx, _, hisa, e) = Board(UnitCatalog.HisaMIa, MisaQuiet);
            var nb = Enumerable.Range(1, 4).Where(i => FormationRules.AreAdjacent(e[0], e[i])).ToArray();
            foreach (int i in nb) e[i].SetCounter(StatusKeys.Marked, 1);
            e[0].SetCounter(StatusKeys.Marked, 3);
            ctx.ApplyDamage(e[0], 99999, hisa);
            bool tie = nb.Length > 0 && L(e[nb[0]]) == 4 && nb.Skip(1).All(i => L(e[i]) == 1);
            var (ctx2, _, hisa2, e2) = Board(UnitCatalog.HisaMIa, MisaQuiet);
            foreach (int i in nb) e2[i].SetCounter(StatusKeys.Marked, 1);
            int last = nb[^1];
            e2[last].SetCounter(StatusKeys.Marked, 2);
            e2[0].SetCounter(StatusKeys.Marked, 3);
            ctx2.ApplyDamage(e2[0], 99999, hisa2);
            bool most = L(e2[last]) == 5 && nb.Where(i => i != last).All(i => L(e2[i]) == 1);
            Expect("MI-a: 層 3 をすべて、隣のうち層の最も多い1体へ（同じなら席の若い方）", tie && most, $"同じとき {string.Join(" ／ ", nb.Select(i => L(e[i])))}・最も多いとき {string.Join(" ／ ", nb.Select(i => L(e2[i])))}");
        }

        // ---- 引き継ぎは攻撃ではない: 仇討ち ／ 指差し ／ 標が付いた瞬間の羽（割り込み）が起きない・層 0 の敵が倒れても何も起きない
        {
            var ctx = T323.Ctx(Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Zan, center: UnitCatalog.Doha, back1: UnitCatalog.Tome, back3: UnitCatalog.HisaMIb), Foes, out var p, out var e0);
            var e = Enumerable.Range(0, 5).Select(i => T323.Seat(e0, i)).ToList();
            var hisa = T323.Seat(p, 4);
            e[0].SetCounter(StatusKeys.Marked, 3);   // 規定のミサ（MF）が羽を積む——ここから数える
            var zt = T323.Tal(ctx, "zan"); var ht = T323.Tal(ctx, "hisa"); var mt = T323.Tal(ctx, "tome");
            long v0 = zt.VendettaFires, a0 = ht.FrameAccuses, q0 = mt.MfQueuedFresh + mt.MfQueuedLayer, s0 = mt.MfShotsFoe + mt.MfShotsAlly;
            int lay0 = L(e[0]);
            ctx.ApplyDamage(e[0], 99999, hisa);
            zt = T323.Tal(ctx, "zan"); ht = T323.Tal(ctx, "hisa"); mt = T323.Tal(ctx, "tome");   // 帳簿は初めて書かれたときに作られる——倒した後に引き直す
            string dbg = $"層 {lay0}・倒れた {!e[0].IsAlive}・標の死 {ht.Mi337MarkedDeaths}";
            bool quiet = ht.Mi337Fires == 1 && zt.VendettaFires == v0 && ht.FrameAccuses == a0 && mt.MfQueuedFresh + mt.MfQueuedLayer == q0 && mt.MfShotsFoe + mt.MfShotsAlly == s0;
            e[4].SetCounter(StatusKeys.Marked, 0);
            long f1 = ht.Mi337Fires;
            ctx.ApplyDamage(e[4], 99999, hisa);
            bool zero = T323.Tal(ctx, "hisa").Mi337Fires == f1;
            Expect("引き継ぎで仇討ち ／ 指差し ／ 標が付いた瞬間の羽が起きない（規定のザン ／ ミサのいる盤）・層 0 の敵が倒れても何も起きない", quiet && zero,
                $"{dbg}・引き継ぎ {ht.Mi337Fires}・仇討ち {zt.VendettaFires - v0}・指差し {ht.FrameAccuses - a0}・羽の積み {mt.MfQueuedFresh + mt.MfQueuedLayer - q0}・層 0 で {ht.Mi337Fires - f1}");
        }

        // ---- verbose の有無で勝敗 ／ 決着T が同じ（代表台 5 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 3 版）
        {
            int diff = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, f) in Boards())
                    foreach (var w in new[] { Waves[0], Waves[1], Waves[2] })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            var fv = Apply(f, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            n++;
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        }
            Expect($"代表台 5 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 3 版（{n} 戦）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 75ec3d3:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第336期（75ec3d3）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
