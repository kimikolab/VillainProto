using BattleCore;
using static Common;
using G343 = Golm343Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// rp345 の自己検査（第345期）。段0 は定義 ／ 固定の口 ／ 戦の中で、本体は盤を直に組んでゴルムの手番（`GolmTurn343`）を呼ぶ（`T323.Ctx`）。
static partial class Rp345Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static UnitDef Dummy(int hp) => new()
    {
        Id = "dummy345", Name = "木偶", MaxHp = hp, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };

    /// <summary>ゴルム（前1）だけ、または ゴルム・ザン・ミサ・ヒサ ＋ 木偶（最大 HP を席ごとに・0 なら空席）。HP は後で下げる。</summary>
    static (BattleContext Ctx, UnitState G, List<UnitState> P, UnitState?[] E) Board(UnitDef golm, int belly, bool mates, params int[] maxHp)
    {
        UnitDef? D(int i) => maxHp[i] > 0 ? Dummy(maxHp[i]) : null;
        var foes = Formation.Build(front1: D(0), front3: D(1), center: D(2), back1: D(3), back3: D(4));
        var pl = mates ? Formation.Build(front1: golm, front3: UnitCatalog.Zan, back1: UnitCatalog.Tome, back3: UnitCatalog.Hisa) : Formation.Build(front1: golm);
        var ctx = T323.Ctx(pl, foes, out var p, out var e);
        var g = T323.Seat(p, 0);
        g.SetCounter(ColossusTrait.BellyKey, belly);
        return (ctx, g, p, Enumerable.Range(0, 5).Select(i => e.FirstOrDefault(u => u.Slot == i)).ToArray());
    }
    static int Belly(UnitState g) => g.RawCounter(ColossusTrait.BellyKey);
    static bool Damaged(BattleContext ctx, UnitState u) => ctx.Events.Any(x => x.Kind == BattleEventKind.Damage && x.TargetId == u.InstanceId);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# rp345 check —— 第345期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ================= 段0 =================
        {
            UnitDef G = UnitCatalog.GolmH345, O = UnitCatalog.GolmH344;   // 第346期: 第345期の規定は `GolmH345`
            bool def = G.Traits.SequenceEqual(O.Traits.Concat(new[] { TraitId.GolmSwallowSpit, TraitId.GolmSwallowSpend, TraitId.GolmSwallowWeak })) && G.Traits.SequenceEqual(UnitCatalog.GolmSWab.Traits)
                && G.PlusText == UnitCatalog.GolmSWab.PlusText && G.PlusText == O.PlusText + "。手番で、弱った敵（最大 HP の3割以下）の精気を呑み干す" + "。呑み込めない毒や雷は、いちばん大きな敵に吐き捨てる"
                && G.MinusText == O.MinusText && G.Flavor == UnitCatalog.GolmSWab.Flavor && G.Actions is { Count: 1 } a && a[0].Kind == ActionKind.Skill
                && new[] { UnitCatalog.GolmGTa2, UnitCatalog.GolmGTa1, UnitCatalog.GolmGTa3 }.Zip(new[] { TraitId.GolmSwallowSpit, TraitId.GolmSwallowHeal, TraitId.GolmSwallowGrow }).All(x => x.First.Traits.SequenceEqual(O.Traits.Append(x.Second)))
                && UnitCatalog.GolmDrink.Traits.SequenceEqual(O.Traits.Append(TraitId.ColossusDrink)) && UnitCatalog.GolmGL.Traits.SequenceEqual(O.Traits.Append(TraitId.ColossusToStriker))
                && UnitCatalog.GolmSWa.Traits.SequenceEqual(UnitCatalog.GolmGTa2.Traits.Append(TraitId.GolmSwallowSpend))
                && UnitCatalog.All.Contains(UnitCatalog.Golm) && !UnitCatalog.Everyone.Contains(O) && !UnitCatalog.Everyone.Contains(G) && UnitCatalog.All.Count == 52;
            Expect("段0 定義: 規定のゴルム ＝ 第1〜344期の規定（`GolmH344`）＋ 吐き捨て ＋ 腹を使う ＋ 弱った敵だけ（札 ／ 文面 ／ フレーバー ／ 手番 `[Skill]` は第344期の `GolmSWab` と同じ）・第335期 ／ 第343期の版は旧から作り直した・旧は `Everyone` の外", def);
            var guard = Presets.Playtest.First(r => r.Name == G343.GuardRow).F;
            bool pin = ReferenceEquals(PinG344(G), O) && ReferenceEquals(PinK342(G), O) && ReferenceEquals(PinG344(UnitCatalog.Golm), O) && ReferenceEquals(Pin344(guard)[0], O) && IsGolmNow(O) && IsGolmNow(G)
                && new Func<UnitDef, UnitDef>[] { PinM339, PinH337, PinD335, PinZ334, PinM333 }.All(p => ReferenceEquals(p(G), O))
                && ReferenceEquals(Pin342(guard)[0], O) && ReferenceEquals(Pin339(guard)[0], O)
                && G343.Apply(guard, G343.Vers[0]).Occupied().All(o => !ReferenceEquals(o.Def, G)) && Sw344Diag.Apply(guard, Sw344Diag.Vers[0]).Occupied().All(o => !ReferenceEquals(o.Def, G))
                && CompareBuildsH344().Concat(PlaytestH344).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, G));
            Expect("段0 固定の口: `PinG344` ／ `Pin344` は規定のゴルムを `GolmH344` へ・古い口の終端（`PinK342` ← `PinM339` ← … ← `PinM333`）も・`golm343` ／ `sw344` の「規定」は `GolmH344`", pin);

            // 戦の中で: 規定は第344期の SW-a＋b と同じ戦をし、旧は呑まない
            long swNew = 0, swOld = 0; int n = 0, diff = 0;
            foreach (var w in new[] { Waves[1], Waves[2], Waves[3] })
                for (int sd = 0; sd < 20; sd++)
                {
                    var rn = BattleEngine.Run(BattleEngine.Materialize(Pin345(guard), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);   // 第346期: 第345期の規定
                    var ro = BattleEngine.Run(BattleEngine.Materialize(Pin344(guard), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    var rv = BattleEngine.Run(BattleEngine.Materialize(S316.SwapId(Pin344(guard), "golm", UnitCatalog.GolmSWab), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    n++;
                    long a1 = rn.TallyByUnit.GetValueOrDefault("golm")?.Gt343Swallows ?? 0, a0 = ro.TallyByUnit.GetValueOrDefault("golm")?.Gt343Swallows ?? 0, av = rv.TallyByUnit.GetValueOrDefault("golm")?.Gt343Swallows ?? 0;
                    swNew += a1; swOld += a0;
                    if (rn.PlayerWon != rv.PlayerWon || rn.Turns != rv.Turns || a1 != av) diff++;
                }
            Expect($"段0 戦の中で: 守り型 × 近衛 ／ 大隊 ／ 第2波 × seed 0..19（{n} 戦）——規定のゴルムは呑み、旧（`GolmH344`）は呑まない・規定は第344期の SW-a＋b（`GolmSWab`）と勝敗 ／ 決着T ／ 呑んだ回数が一致",
                swNew > 0 && swOld == 0 && diff == 0, $"呑んだ 規定 {swNew} ／ 旧 {swOld}・SW-a＋b との違い {diff}");

            // 盤を直に: 3 割以下かつ腹以下だけを呑み、腹が減り、吐き捨てる
            var (ctx, g, _, e) = Board(G, 50, false, 100, 100, 300, 300, 200);
            e[0]!.Hp = 40;   // 40%（腹以下だが 3 割を超える）
            e[1]!.Hp = 25; e[1]!.SetCounter(StatusKeys.Poison, 3);   // 25%・毒 3
            ctx.GolmTurn343(g);
            bool ok = e[0]!.IsAlive && !e[1]!.IsAlive && Belly(g) == 25 && e[2]!.RawCounter(StatusKeys.Poison) == 3 && !Damaged(ctx, e[1]!);
            Expect("段0 盤を直に: 規定のゴルムは 3 割以下かつ腹以下の敵（25%）だけを呑み（40% は呑まない）、腹が減り（50 → 25）、呑んだ毒を HP の最も多い敵へ吐き捨てる", ok, $"腹 50 → {Belly(g)}・吐いた先の毒 {e[2]!.RawCounter(StatusKeys.Poison)}");
        }

        // ================= 第346期: RP-b を規定に =================
        {
            UnitDef N = UnitCatalog.Golm, O = UnitCatalog.GolmH345;
            bool def = N.Traits.SequenceEqual(O.Traits.Concat(new[] { TraitId.GolmRocketPunch, TraitId.GolmRocketGrab })) && N.PlusText == UnitCatalog.GolmRPb.PlusText
                && N.MinusText == O.MinusText && N.Flavor == O.Flavor && N.Actions == O.Actions && UnitCatalog.All.Contains(N) && !UnitCatalog.Everyone.Contains(O)
                && ReferenceEquals(PinG345(N), O) && ReferenceEquals(Pin345(Presets.Playtest.First(r => r.Name == G343.GuardRow).F)[0], O)
                && CompareBuildsH345().Concat(PlaytestH345).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, N))
                && Apply(Presets.Playtest.First(r => r.Name == G343.GuardRow).F, Vers[0]).Occupied().All(o => !ReferenceEquals(o.Def, N));
            Expect("第346期 定義: 規定のゴルム ＝ 第345期の規定（`GolmH345`）＋ ロケットパンチ ＋ 捕獲（文面は第345期の RP-b）・`PinG345` ／ `Pin345` は規定を `GolmH345` へ・`rp345` の「規定」は `GolmH345`", def);
            var guard = Presets.Playtest.First(r => r.Name == G343.GuardRow).F;
            long armNew = 0, armOld = 0; int n = 0, diff = 0;
            foreach (var w in new[] { Waves[0], Waves[2], Waves[3] })
                for (int sd = 0; sd < 20; sd++)
                {
                    var rn = BattleEngine.Run(BattleEngine.Materialize(guard, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    var ro = BattleEngine.Run(BattleEngine.Materialize(Pin345(guard), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    var rv = BattleEngine.Run(BattleEngine.Materialize(S316.SwapId(Pin345(guard), "golm", UnitCatalog.GolmRPb), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    n++;
                    UnitTally? tn = rn.TallyByUnit.GetValueOrDefault("golm"), to = ro.TallyByUnit.GetValueOrDefault("golm"), tv = rv.TallyByUnit.GetValueOrDefault("golm");
                    long a1 = (tn?.Rp345Punches ?? 0) + (tn?.Rp345GrabNoPunch ?? 0), a0 = (to?.Rp345Punches ?? 0) + (to?.Rp345GrabNoPunch ?? 0), av = (tv?.Rp345Punches ?? 0) + (tv?.Rp345GrabNoPunch ?? 0);
                    armNew += a1; armOld += a0;
                    if (rn.PlayerWon != rv.PlayerWon || rn.Turns != rv.Turns || a1 != av || (tn?.Gt343Swallows ?? 0) != (tv?.Gt343Swallows ?? 0)) diff++;
                }
            Expect($"第346期 戦の中で: 守り型 × ボス ／ 大隊 ／ 第2波 × seed 0..19（{n} 戦）——規定のゴルムは手を飛ばし（RP-b が働く）、旧（`GolmH345`）は飛ばさない・規定は第345期の RP-b（`GolmRPb`）と勝敗 ／ 決着T ／ 手の回数 ／ 呑んだ回数が一致",
                armNew > 0 && armOld == 0 && diff == 0, $"手 規定 {armNew} ／ 旧 {armOld}・RP-b との違い {diff}");
        }

        // ================= 版の定義と試遊の台 =================
        {
            UnitDef G = UnitCatalog.GolmH345;   // 第346期: 版は第345期の規定から作り直した
            bool def = UnitCatalog.GolmRPa.Traits.SequenceEqual(G.Traits.Append(TraitId.GolmRocketPunch)) && UnitCatalog.GolmRPb.Traits.SequenceEqual(G.Traits.Append(TraitId.GolmRocketGrab))
                && UnitCatalog.GolmGAa.Traits.SequenceEqual(G.Traits.Append(TraitId.GolmStomp))
                && UnitCatalog.GolmRPa.PlusText == G.PlusText + "。呑めないときは、腕を飛ばして殴る（後ろの敵にも届く）"
                && UnitCatalog.GolmRPb.PlusText == G.PlusText.Replace("。手番で、弱った敵（最大 HP の3割以下）の精気を呑み干す", "。手番で、腕を飛ばして殴る（後ろの敵にも届く）。弱った敵（最大 HP の3割以下）は、その手で掴んで持ち帰り、精気を呑み干す", StringComparison.Ordinal)
                && UnitCatalog.GolmRPb.PlusText != G.PlusText
                && UnitCatalog.GolmGAa.PlusText == G.PlusText + "。呑めないときは、巨体で踏み潰す（隣の敵にも半分）"
                && new[] { UnitCatalog.GolmRPa, UnitCatalog.GolmRPb, UnitCatalog.GolmGAa }.All(d => !UnitCatalog.Everyone.Contains(d) && d.MinusText == G.MinusText && d.Actions == G.Actions)
                && UnitCatalog.Everyone.Where(d => d.Traits.Any(t => t is TraitId.GolmRocketPunch or TraitId.GolmRocketGrab)).SequenceEqual(new[] { UnitCatalog.Golm })   // 第346期: 規定のゴルムの札
                && UnitCatalog.Everyone.All(d => !d.Traits.Contains(TraitId.GolmStomp));
            Expect("版の定義: RP-a ／ RP-b ／ GA-a ＝ 第345期の規定のゴルム（S3・`GolmH345`）＋ 札1枚・文面は叩き台（RP-b は手番の句を差し替え）・`Everyone` の外・ロケットパンチ ／ 捕獲の札は第346期から規定のゴルムの札（踏み潰しは保持者 0）", def);
            var v = Presets.Versions; var guard = Presets.Playtest.First(r => r.Name == G343.GuardRow).F; var poison = G343.PoisonGolm();
            (string Name, Formation Base, UnitDef Golm)[] want = { ("版・RP-a 守り型", guard, UnitCatalog.GolmRPa), ("版・RP-b 守り型", guard, UnitCatalog.GolmRPb), ("版・GA-a 守り型", guard, UnitCatalog.GolmGAa), ("版・RP-b 毒", poison, UnitCatalog.GolmRPb) };
            // 第346期: 「版・RP ／ GA」の4台を外した——1〜2行目（ポンの台 ／ 書き手なし）と、第344〜345期の版が試遊の台に残っていないことだけを見る（`want` は第345期の並びの記録）
            // 第347期: 「版・」の台は期ごとに入れ替わるので、3行目以降の数は見ない（第347期の「版・FL」の3台が並ぶ）
            bool rows = v.Length >= 2 && v[0].Name == "試遊・感電 ポンの台" && v[1].Name == "試遊・ミサ 書き手なし" && Presets.Playtest.Length == 17 && want.Length == 4
                && v.SelectMany(r => r.F.Occupied()).All(o => !new[] { UnitCatalog.GolmSWa, UnitCatalog.GolmSWb, UnitCatalog.GolmSWab, UnitCatalog.GolmRPa, UnitCatalog.GolmRPb, UnitCatalog.GolmGAa }.Any(d => ReferenceEquals(o.Def, d)));
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」「試遊・ミサ 書き手なし」が1〜2行目・第344〜345期の版は無い（第345期の「版・RP-a ／ RP-b ／ GA-a 守り型」「版・RP-b 毒」は第346期に外した）・`Playtest` は 17 台のまま", rows);
        }

        // ================= RP-a =================
        {
            // 呑める敵なし（腹 50・HP 300 ／ 200 ／ 250）: 状態異常の量の多い後列へ届く・量は 攻 ＋ 腹の 1 割・腹は減らない
            var (ctx, g, _, e) = Board(UnitCatalog.GolmRPa, 50, false, 300, 300, 300, 300, 300);
            e[3]!.Hp = 200; e[3]!.SetCounter(StatusKeys.Poison, 2);
            e[4]!.Hp = 250; e[4]!.SetCounter(StatusKeys.Poison, 2);
            int amount = g.CurrentAttack + 50 / 10;
            ctx.GolmTurn343(g);
            var t = T323.Tal(ctx, "golm");
            bool ok = e[4]!.Hp == 250 - amount && e[3]!.Hp == 200 && e.Take(3).All(u => u!.Hp == 300) && Belly(g) == 50 && t.Rp345Punches == 1 && t.Rp345PunchBack == 1 && t.Gt343Attacks == 0 && amount == 15;
            Expect("RP-a: 呑めないときは後列の敵にも届く（量の同じ後1 ／ 後3 のうち HP の高い後3）・量は 攻 ＋ 腹の 1 割（10 ＋ 5）・腹は減らない", ok, $"後3 250 → {e[4]!.Hp}・腹 {Belly(g)}");

            // 同じ量・同じ HP なら席の若い方
            var (ctx2, g2, _, e2) = Board(UnitCatalog.GolmRPa, 50, false, 300, 300, 300, 200, 200);
            e2[3]!.SetCounter(StatusKeys.Marked, 1); e2[4]!.SetCounter(StatusKeys.Poison, 1);
            ctx2.GolmTurn343(g2);
            bool tie = e2[3]!.Hp < 200 && e2[4]!.Hp == 200;
            // 量の多い方が先（前列の HP が高くても）
            var (ctx3, g3, _, e3) = Board(UnitCatalog.GolmRPa, 50, false, 300, 300, 300, 200, 300);
            e3[2]!.SetCounter(StatusKeys.Poison, 1); e3[3]!.SetCounter(StatusKeys.Poison, 4);
            ctx3.GolmTurn343(g3);
            Expect("RP-a: 狙いの並び ＝ 帯びた状態異常の量 → HP の高い方 → 席の若い方（量 4 の後1 を量 1 の中央より先に・量 ／ HP が同じなら後1）", tie && e3[3]!.Hp < 200 && e3[2]!.Hp == 300);

            // 呑める敵がいれば今どおり呑む（殴らない）
            var (ctx4, g4, _, e4) = Board(UnitCatalog.GolmRPa, 50, false, 100, 300, 300, 300, 300);
            e4[0]!.Hp = 20;
            ctx4.GolmTurn343(g4);
            var t4 = T323.Tal(ctx4, "golm");
            Expect("RP-a: 呑める敵がいれば今どおり呑む（手は飛ばさない）", !e4[0]!.IsAlive && !Damaged(ctx4, e4[0]!) && t4.Gt343Swallows == 1 && t4.Rp345Punches == 0 && Belly(g4) == 30);
        }

        // ================= RP-b =================
        {
            // 呑める敵は殴らずに掴む
            var (ctx, g, _, e) = Board(UnitCatalog.GolmRPb, 50, false, 100, 300, 300, 300, 300);
            e[0]!.Hp = 20;
            ctx.GolmTurn343(g);
            var t = T323.Tal(ctx, "golm");
            bool grab0 = !e[0]!.IsAlive && !Damaged(ctx, e[0]!) && t.Rp345GrabNoPunch == 1 && t.Rp345Punches == 0 && t.Gt343Swallows == 1
                && ctx.Events.Any(x => x.Kind == BattleEventKind.GolmArm && x.Text == GolmArmLabels.Grab && x.TargetId == e[0]!.InstanceId && x.Slot == 0);
            Expect("RP-b: 呑める敵は殴らずに掴む（ダメージは出ない・「掴む」→「呑む」）", grab0);

            // 殴れば呑める敵を先に狙い、殴った後に呑める条件なら同じ手番で掴んで呑む（腹が減る・吐き捨て・引き継ぎ ／ 飛び火なし）
            UnitState B, A2, Top; int hpAfter = -1; long spent = -1; bool ok;
            {
                var (c2, g2, _, e2) = Board(UnitCatalog.GolmRPb, 200, true, 300, 300, 300, 300, 300);
                e2[0]!.SetCounter(StatusKeys.Poison, 5);                 // 量 5・殴っても呑めない
                e2[1]!.Hp = 100; e2[1]!.SetCounter(StatusKeys.Marked, 2); // 量 2・100 − 30 ＝ 70（3 割 90 以下・腹 200 以下）
                B = e2[1]!; A2 = e2[0]!; Top = e2[0]!;
                c2.GolmTurn343(g2);
                var t2 = T323.Tal(c2, "golm"); var h2 = T323.Tal(c2, "hisa");
                var dmg = c2.Events.Where(x => x.Kind == BattleEventKind.Damage && x.TargetId == B.InstanceId).Sum(x => x.Amount);
                hpAfter = 100 - dmg; spent = t2.Sw344BellySpent;
                ok = !B.IsAlive && Damaged(c2, B) && A2.Hp == 300 && t2.Rp345Punches == 1 && t2.Rp345AimEdible == 1 && t2.Rp345GrabAfter == 1 && t2.Gt343Swallows == 1
                    && Belly(g2) == 200 - hpAfter && Top.RawCounter(StatusKeys.Marked) == 2 && h2.Mi337Fires == 0 && e2[2]!.RawCounter(StatusKeys.Marked) == 0 && e2[4]!.RawCounter(StatusKeys.Marked) == 0
                    && c2.Events.Any(x => x.Kind == BattleEventKind.Death && x.TargetId == B.InstanceId && x.ActorId == g2.InstanceId)
                    && c2.Events.Any(x => x.Kind == BattleEventKind.GolmArm && x.Text == GolmArmLabels.Grab && x.TargetId == B.InstanceId && x.Slot == 1);
            }
            // 同じ盤で RP-a は量の多い前1 を殴る（対照）
            var (c3, g3, _, e3) = Board(UnitCatalog.GolmRPa, 200, true, 300, 300, 300, 300, 300);
            e3[0]!.SetCounter(StatusKeys.Poison, 5); e3[1]!.Hp = 100; e3[1]!.SetCounter(StatusKeys.Marked, 2);
            c3.GolmTurn343(g3);
            bool ctl = e3[0]!.Hp < 300 && e3[1]!.Hp == 100;
            Expect("RP-b: 殴れば呑める敵（HP 100・量 30 で 3 割以下）を量の多い敵より先に狙い、殴った後に同じ手番で掴んで呑む——腹が減る・標は吐き捨てられる・ヒサの引き継ぎ ／ 隣の標は起きない（RP-a は同じ盤で量の多い前1 を殴る）",
                ok && ctl, $"殴った後の HP {hpAfter}・減った腹 {spent}・RP-a の前1 300 → {e3[0]!.Hp}");

            // 殴って倒れたら、ふつうの撃破（引き継ぎは起きる）
            var (c4, g4, _, e4) = Board(UnitCatalog.GolmRPb, 0, true, 300, 100, 300, 300, 300);
            e4[1]!.Hp = 8; e4[1]!.SetCounter(StatusKeys.Marked, 3);
            c4.GolmTurn343(g4);
            var t4 = T323.Tal(c4, "golm"); var h4 = T323.Tal(c4, "hisa");
            bool kill = !e4[1]!.IsAlive && Damaged(c4, e4[1]!) && t4.Rp345PunchKills == 1 && t4.Gt343Swallows == 0 && t4.Rp345GrabAfter == 0 && h4.Mi337Fires >= 1
                && c4.Events.Any(x => x.Kind == BattleEventKind.Death && x.TargetId == e4[1]!.InstanceId && x.ActorId == g4.InstanceId);
            Expect("RP-b: 殴って倒れたら、それで終わり（ふつうの撃破・ヒサの引き継ぎは起きる・呑まない）", kill, $"引き継ぎ {h4.Mi337Fires}");
        }

        // ================= GA-a =================
        {
            var (ctx, g, _, e) = Board(UnitCatalog.GolmGAa, 50, false, 300, 300, 300, 300, 300);
            e[3]!.SetCounter(StatusKeys.Poison, 9);
            int amount = g.CurrentAttack + 5;
            ctx.GolmTurn343(g);
            var t = T323.Tal(ctx, "golm");
            int l0 = 300 - e[0]!.Hp, l1 = 300 - e[1]!.Hp;
            bool ok = ((l0 == amount && l1 == amount / 2) || (l1 == amount && l0 == amount / 2)) && e.Skip(2).All(u => u!.Hp == 300) && Belly(g) == 50 && t.Ga345Stomps == 1 && t.Ga345SplashHits == 1 && t.Gt343Attacks == 0;
            // 同じ列に隣がいなければ半分は誰にも当たらない（中央は隣ではない）
            var (c2, g2, _, e2) = Board(UnitCatalog.GolmGAa, 50, false, 300, 0, 300, 300, 300);
            c2.GolmTurn343(g2);
            bool solo = e2[0]!.Hp == 300 - amount && e2[2]!.Hp == 300 && e2[3]!.Hp == 300 && e2[4]!.Hp == 300 && T323.Tal(c2, "golm").Ga345SplashHits == 0;
            Expect("GA-a: 的（通常攻撃と同じく前から・量 9 の後1 には届かない）に 攻 ＋ 腹の 1 割（15）・同じ列の左右の隣に半分（7）・前列が1体なら半分は当たらない・腹は減らない", ok && solo, $"前1 −{l0}・前3 −{l1}");
        }

        // ================= verbose ／ PickOne =================
        {
            int diff = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, f) in G343.Boards())
                    foreach (var w in new[] { Waves[0], Waves[1], Waves[3] })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            var fv = Apply(f, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            n++;
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        }
            Expect($"代表台 5 × ボス ／ 近衛 ／ 第2波 × seed 0..9 × 規定 ＋ 3 版（{n} 戦）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 6ab5ca32:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第344期（6ab5ca32）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
