using BattleCore;
using static Common;
using K342 = Kata342Diag;
using T323 = Tou323Diag;

// golm343 の自己検査（第343期）。段0 は定義 ／ 固定の口 ／ 代表台の戦の中で、本体は盤を直に組んでゴルムの手番（`GolmTurn343`）を呼ぶ（`T323.Ctx`）。
static partial class Golm343Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static UnitDef Dummy(int hp) => new()
    {
        Id = "dummy343", Name = "木偶", MaxHp = hp, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };

    /// <summary>ゴルム（前1）・ザン（前3）・ミサ（後1）・ヒサ（後3）＋ 木偶 5 体（HP を席ごとに指定）。</summary>
    static (BattleContext Ctx, UnitState G, List<UnitState> P, List<UnitState> E) Board(UnitDef golm, params int[] hp)
    {
        var foes = Formation.Build(front1: Dummy(hp[0]), front3: Dummy(hp[1]), center: Dummy(hp[2]), back1: Dummy(hp[3]), back3: Dummy(hp[4]));
        var ctx = T323.Ctx(Formation.Build(front1: golm, front3: UnitCatalog.Zan, back1: UnitCatalog.Tome, back3: UnitCatalog.Hisa), foes, out var p, out var e);
        return (ctx, T323.Seat(p, 0), p, Enumerable.Range(0, 5).Select(i => T323.Seat(e, i)).ToList());
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# golm343 check —— 第343期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ================= 段0 =================
        {
            UnitDef K = UnitCatalog.Kata, O = UnitCatalog.KataH342;
            bool def = K.Traits.SequenceEqual(O.Traits.Concat(new[] { TraitId.ThundercloudAllyPops, TraitId.KataRod, TraitId.KataRodChain })) && K.Traits.SequenceEqual(UnitCatalog.KataKTrJ.Traits)
                && K.PlusText == O.PlusText + "。味方の感電が弾けても雷雲は育つ" + "。感電した身で殴られると、雷は避雷針を伝って敵に落ち、帯びた隣の敵へ跳ねる（自分は痺れない）"
                && K.MinusText.Contains("その味方（自分を除く）が半々の確率で痺れる", StringComparison.Ordinal)
                && UnitCatalog.KataKTb.Traits.SequenceEqual(O.Traits.Append(TraitId.ThundercloudAllyPops)) && UnitCatalog.KataKTr1.Traits.SequenceEqual(O.Traits.Append(TraitId.ThundercloudAllyPops).Append(TraitId.KataRod))
                && UnitCatalog.All.Contains(K) && !UnitCatalog.Everyone.Contains(O) && UnitCatalog.All.Count == 52;
            Expect("段0 定義: 規定のカタ ＝ 第332〜342期の規定（`KataH342`）＋ KT-b ＋ 避雷針 ＋ 跳ねる（札は第342期の `KataKTrJ` と同じ・文面に跳ねるを足した）・第342期の版は旧から作り直した・旧は `Everyone` の外", def);
            var pon = Presets.Versions.First(r => r.Name == K342.PonRow).F;
            bool pin = ReferenceEquals(PinK342(K), O) && ReferenceEquals(Pin342(pon)[0], O)
                && new Func<UnitDef, UnitDef>[] { PinM339, PinH337, PinD335, PinZ334, PinM333 }.All(p => ReferenceEquals(p(K), O))
                && new Func<UnitDef, UnitDef>[] { PinT326, PinTK327, PinT328, PinTK331 }.All(p => ReferenceEquals(p(K), UnitCatalog.KataH331) && ReferenceEquals(p(O), UnitCatalog.KataH331))
                && ReferenceEquals(PinTKK324(K), UnitCatalog.KataH324) && ReferenceEquals(PinTKK324(O), UnitCatalog.KataH324)
                && ReferenceEquals(PinKataTou311(K), UnitCatalog.KataH311) && ReferenceEquals(PinKataTou311(O), UnitCatalog.KataH311) && IsKataNow(O)
                && K342.Apply(pon, K342.Vers[0]).Occupied().All(o => !ReferenceEquals(o.Def, K))
                && CompareBuildsH342().Concat(PlaytestH342).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, K));
            Expect("段0 固定の口: `PinK342` ／ `Pin342` は規定のカタを `KataH342` へ・古い口の終端（`PinM339` ← … ← `PinM333`）も・もっと古い規定へ替える口（`PinT326` ／ `PinTK327` ／ `PinT328` ／ `PinTK331` ／ `PinTKK324` ／ `PinKataTou311`）は `KataH342` も受ける・`kata342` の「規定」は `KataH342`", pin);
            long rodNew = 0, rodOld = 0, allyNew = 0; int n = 0, diff = 0;
            foreach (var (_, f) in K342.Boards().Take(2))
                foreach (var w in new[] { K342.Waves[1], K342.Waves[2] })
                    for (int sd = 0; sd < 20; sd++)
                    {
                        var rn = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var ro = BattleEngine.Run(BattleEngine.Materialize(Pin342(f), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var rv = BattleEngine.Run(BattleEngine.Materialize(K342.Apply(f, K342.Vers[3]), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        n++;
                        var tn = rn.TallyByUnit.GetValueOrDefault("kata"); var to = ro.TallyByUnit.GetValueOrDefault("kata");
                        rodNew += tn?.Kt342RodFires ?? 0; rodOld += to?.Kt342RodFires ?? 0;
                        if (rn.PlayerWon != rv.PlayerWon || rn.Turns != rv.Turns || (tn?.Kt342RodFires ?? 0) != (rv.TallyByUnit.GetValueOrDefault("kata")?.Kt342RodFires ?? 0)) diff++;
                    }
            Expect($"段0 戦の中で: ポンの台 ／ 光の盾 鞭 × 近衛 ／ 大隊 × seed 0..19（{n} 戦）——規定のカタでは逆雷が落ち、旧（`KataH342`）では落ちない・規定は第342期の版 K3（`kata342` の rj）と勝敗 ／ 決着T ／ 逆雷の回数が一致",
                rodNew > 0 && rodOld == 0 && diff == 0, $"逆雷 規定 {rodNew} ／ 旧 {rodOld}・K3 との違い {diff}");
        }

        // ================= 本体: 版の定義と試遊の台 =================
        {
            UnitDef G = UnitCatalog.GolmH344;   // 第345期: 版は第1〜344期の規定のゴルムから作り直した
            (UnitDef D, TraitId Card, string Tail)[] vs = { (UnitCatalog.GolmGTa2, TraitId.GolmSwallowSpit, "。呑み込めない毒や雷は、いちばん大きな敵に吐き捨てる"), (UnitCatalog.GolmGTa1, TraitId.GolmSwallowHeal, "。呑んだ精気で仲間を癒す"), (UnitCatalog.GolmGTa3, TraitId.GolmSwallowGrow, "。呑むほど膨れ上がる") };
            bool def = vs.All(x => x.D.Traits.SequenceEqual(G.Traits.Append(x.Card)) && x.D.PlusText == G.PlusText + "。手番で、腹に収まるほど弱った敵の精気を呑み干す" + x.Tail
                    && x.D.MinusText == G.MinusText && x.D.Actions is { Count: 1 } a && a[0].Kind == ActionKind.Skill && !UnitCatalog.Everyone.Contains(x.D))
                && UnitCatalog.GolmGTa2.Flavor == "呑んだ精気は糧になる。呑み込めない毒と雷は、吐き捨てる。"
                && UnitCatalog.Everyone.Where(d => d.Traits.Any(t => t is TraitId.GolmSwallowSpit or TraitId.GolmSwallowHeal or TraitId.GolmSwallowGrow)).SequenceEqual(new[] { UnitCatalog.Golm });   // 第345期: 吐き捨ては規定のゴルムの札
            Expect("版の定義: GT-a2 ／ a1 ／ a3 ＝ 第1〜344期の規定のゴルム（`GolmH344`）＋ 札1枚・手番は `[Skill]`・文面は叩き台（a2 はフレーバーも）・`Everyone` の外・札の保持者は第345期から規定のゴルム（吐き捨て）だけ", def);
            var v = Presets.Versions; var guard = Presets.Playtest.First(r => r.Name == GuardRow).F; var shock = ShockGolm();
            (string Name, Formation Base, UnitDef Golm)[] want = { ("版・GT吐き捨て 守り型", guard, UnitCatalog.GolmGTa2), ("版・GT消化 守り型", guard, UnitCatalog.GolmGTa1), ("版・GT膨れ 守り型", guard, UnitCatalog.GolmGTa3), ("版・GT吐き捨て 感電", shock, UnitCatalog.GolmGTa2) };
            // 第344期: 「版・GT」の4台を外した——1〜2行目（ポンの台 ／ 書き手なし）と、GT の版が試遊の台に残っていないことだけを見る（`want` は第343期の並びの記録）
            bool rows = v[0].Name == K342.PonRow && v[1].Name == "試遊・ミサ 書き手なし" && Presets.Playtest.Length == 17 && want.Length == 4
                && v.SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, UnitCatalog.GolmGTa2) && !ReferenceEquals(o.Def, UnitCatalog.GolmGTa1) && !ReferenceEquals(o.Def, UnitCatalog.GolmGTa3));
            Expect("試遊の台: `Presets.Versions` の1〜2行目は「試遊・感電 ポンの台」「試遊・ミサ 書き手なし」・第343期の「版・GT」の4台は第344期に外した・`Playtest` は 17 台のまま", rows);
        }

        // ================= 本体: 共通（呑む） =================
        {
            // 腹 50: HP 40（何も帯びない）／ 45（毒 3）／ 100（毒 9・腹を超える）→ 45 を呑む
            var (ctx, g, p, e) = Board(UnitCatalog.GolmGTa3, 40, 45, 100, 200, 300);
            g.SetCounter(ColossusTrait.BellyKey, 50);
            e[1].SetCounter(StatusKeys.Poison, 3); e[2].SetCounter(StatusKeys.Poison, 9);
            ctx.GolmTurn343(g);
            var death = ctx.Events.LastOrDefault(x => x.Kind == BattleEventKind.Death);
            bool ok = !e[1].IsAlive && e[0].IsAlive && e[2].IsAlive && death?.ActorId == g.InstanceId && death?.TargetId == e[1].InstanceId && T323.Tal(ctx, "golm").Kills == 1 && e[2].Hp == 100;
            Expect("共通: HP が腹以下の敵だけを呑む（腹 50 で HP 100 の敵は呑まない）・候補は帯びた状態異常の量の多い方・呑まれた敵は倒れ、撃破者はゴルム（ダメージは出ない）", ok,
                $"呑まれた {(e[1].IsAlive ? "—" : "席 1")}・撃破者 {(death?.ActorId == g.InstanceId ? "ゴルム" : "?")}");

            // 同じ量なら HP の高い方、さらに同じなら席の若い方
            var (ctx2, g2, _, e2) = Board(UnitCatalog.GolmGTa3, 30, 40, 40, 300, 300);
            g2.SetCounter(ColossusTrait.BellyKey, 50);
            ctx2.GolmTurn343(g2);
            bool tie = e2[0].IsAlive && !e2[1].IsAlive && e2[2].IsAlive;
            Expect("共通: 状態異常の量が同じなら HP の高い方、さらに同じなら席の若い方（HP 30 ／ 40 ／ 40 → 席 1 の 40）", tie);

            // 呑んだ敵の引き継ぎ ／ 飛び火が起きない（規定のヒサの MI-b・標の層 3 で隣あり）
            var (ctx3, g3, _, e3) = Board(UnitCatalog.GolmGTa3, 40, 300, 300, 300, 300);
            g3.SetCounter(ColossusTrait.BellyKey, 50);
            e3[0].SetCounter(StatusKeys.Marked, 3); e3[0].SetCounter(StatusKeys.Concentrated, 2);
            ctx3.GolmTurn343(g3);
            var h3 = T323.Tal(ctx3, "hisa");
            bool quiet = !e3[0].IsAlive && e3.Skip(1).All(u => u.RawCounter(StatusKeys.Marked) == 0 && u.RawCounter(StatusKeys.Concentrated) == 0) && h3.Mi337Fires == 0 && T323.Tal(ctx3, "golm").Gt343SwMarkedNbr == 1;
            Expect("共通: 呑んだ敵の標 ／ 印は腹に入り、引き継ぎ（規定のヒサの MI-b）や移りが起きない", quiet, $"引き継ぎ {h3.Mi337Fires}・隣の標 {string.Join(",", e3.Skip(1).Select(u => u.RawCounter(StatusKeys.Marked)))}");

            // 呑めなければ通常攻撃
            var (ctx4, g4, _, e4) = Board(UnitCatalog.GolmGTa1, 300, 300, 300, 300, 300);
            g4.SetCounter(ColossusTrait.BellyKey, 50);
            var h4 = e4.ToDictionary(u => u, u => u.Hp);
            ctx4.GolmTurn343(g4);
            Expect("共通: 呑める敵がいなければ通常攻撃（GT-a1）", e4.Any(u => u.Hp < h4[u]) && e4.All(u => u.IsAlive) && T323.Tal(ctx4, "golm").Gt343Attacks == 1);
        }

        // ================= GT-a2 =================
        {
            var (ctx, g, p, e) = Board(UnitCatalog.GolmGTa2, 40, 300, 500, 200, 100);
            g.SetCounter(ColossusTrait.BellyKey, 50);
            e[0].SetCounter(StatusKeys.Poison, 3); e[0].SetCounter(StatusKeys.Burn, 2); e[0].SetCounter(StatusKeys.Shock, 1); e[0].SetCounter(StatusKeys.Marked, 2);
            var zt0 = T323.Tal(ctx, "zan").VendettaFires; var ft0 = FeathersTrait.Count(T323.Seat(p, 3));
            ctx.GolmTurn343(g);
            var top = e[2];
            var zt = T323.Tal(ctx, "zan"); var ht = T323.Tal(ctx, "hisa");
            bool ok = !e[0].IsAlive && top.RawCounter(StatusKeys.Poison) == 3 && top.RawCounter(StatusKeys.Burn) == 2 && top.RawCounter(StatusKeys.Shock) > 0 && top.RawCounter(StatusKeys.Marked) == 2
                && e.Where(u => u != top && u != e[0]).All(u => u.RawCounter(StatusKeys.Poison) == 0 && u.RawCounter(StatusKeys.Marked) == 0)
                && zt.VendettaFires == zt0 && ht.FrameAccuses == 0 && FeathersTrait.Count(T323.Seat(p, 3)) == ft0 && GolmSwallowTrait.StashKeys.All(k => g.RawCounter(k) == 0)
                && T323.Tal(ctx, "golm").Gt343Spits == 1;
            Expect("GT-a2: 呑んだ敵の状態異常（毒 3・火 2・雷・標 2）が同じ手番のうちに HP の最も多い敵（HP 500）にそのまま足される・吐きかけで仇討ち ／ 指差し ／ 羽（合図）が起きない・腹は空になる", ok,
                $"先 毒 {top.RawCounter(StatusKeys.Poison)}・火 {top.RawCounter(StatusKeys.Burn)}・雷 {top.RawCounter(StatusKeys.Shock)}・標 {top.RawCounter(StatusKeys.Marked)}・羽 {ft0} → {FeathersTrait.Count(T323.Seat(p, 3))}");

            // 呑めない手番で腹に吐くものがあれば吐く（殴らない）
            var (ctx2, g2, _, e2) = Board(UnitCatalog.GolmGTa2, 300, 300, 400, 300, 300);
            g2.SetCounter(ColossusTrait.BellyKey, 10);
            g2.SetCounter("gtPoison", 4);
            var h2 = e2.ToDictionary(u => u, u => u.Hp);
            ctx2.GolmTurn343(g2);
            Expect("GT-a2: 呑める敵がいなくても、腹に吐くものがあれば殴らずに吐く", e2[2].RawCounter(StatusKeys.Poison) == 4 && e2.All(u => u.Hp == h2[u]) && T323.Tal(ctx2, "golm").Gt343Attacks == 0);
        }

        // ================= GT-a1 =================
        {
            var (ctx, g, p, e) = Board(UnitCatalog.GolmGTa1, 40, 300, 300, 300, 300);
            g.SetCounter(ColossusTrait.BellyKey, 50);
            foreach (var u in p) u.Hp = Math.Max(1, u.Hp - 30);
            var h = p.ToDictionary(u => u, u => u.Hp);
            ctx.GolmTurn343(g);
            int each = 40 / p.Count;
            bool ok = !e[0].IsAlive && p.All(u => u.Hp - h[u] == each) && T323.Tal(ctx, "golm").Gt343Healed == each * p.Count;
            Expect($"GT-a1: 呑んだ敵の残りの HP（40）が生きている味方全員（{p.Count} 体・ゴルムを含む）に等分で癒える（{each} ずつ）", ok, string.Join(",", p.Select(u => u.Hp - h[u])));
        }

        // ================= GT-a3 =================
        {
            var (ctx, g, _, e) = Board(UnitCatalog.GolmGTa3, 40, 300, 300, 300, 300);
            g.SetCounter(ColossusTrait.BellyKey, 50);
            int m0 = g.MaxHp, h0 = g.Hp;
            ctx.GolmTurn343(g);
            Expect("GT-a3: 最大 HP と HP が呑んだ敵の残りの HP（40）だけ増える", !e[0].IsAlive && g.MaxHp == m0 + 40 && g.Hp == h0 + 40, $"最大 HP {m0} → {g.MaxHp}・HP {h0} → {g.Hp}");
        }

        // ================= verbose ／ PickOne =================
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
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 5f3d0b08:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第342期（5f3d0b08）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
