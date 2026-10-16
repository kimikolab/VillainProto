using BattleCore;
using static Common;
using T323 = Tou323Diag;

// flow347 の自己検査（第347期）。前半は盤を直に組んでクグに状態異常を書き（`T323.Ctx`・書き手は第94期の印 `BeginTrait`）、後半は戦の中で見る。
static partial class Flow347Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static UnitDef Dummy(string id) => new()
    {
        Id = id, Name = id, MaxHp = 200, Attack = 5, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };

    /// <summary>クグ（前1）＋ 味方の書き手（前3）／ 敵 2 体（前1 ＝ 組み付いた敵・前3 ＝ 敵の書き手）。<paramref name="held"/> なら組み付いた形にする。</summary>
    static (BattleContext Ctx, UnitState K, UnitState Ally, UnitState Foe, UnitState FoeW) Board(UnitDef kugu, bool held)
    {
        var ctx = T323.Ctx(Formation.Build(front1: kugu, front3: Dummy("ally347")), Formation.Build(front1: Dummy("foe347"), front3: Dummy("foew347")), out var p, out var e);
        var k = T323.Seat(p, 0); var a = T323.Seat(p, 1); var f = T323.Seat(e, 0); var fw = T323.Seat(e, 1);
        if (held) { f.SetCounter(StatusKeys.Grappled, 1); k.SetCounter(GrappleTrait.TargetKey, f.InstanceId + 1); }
        return (ctx, k, a, f, fw);
    }

    /// <summary><paramref name="writer"/> の印の下でクグに書く（量のキーは今の値 ＋ <paramref name="v"/>・ほかは <paramref name="v"/>）。</summary>
    static void Write(BattleContext ctx, UnitState writer, UnitState k, string key, int v)
    {
        var prev = ctx.BeginTrait(TraitId.Venom, writer);
        k.SetCounter(key, ThreadFlowRule.IsAdditive(key) ? k.RawCounter(key) + v : v);
        ctx.EndTrait(prev);
    }

    static long Out(BattleContext ctx, string id) { var t = T323.Tal(ctx, id); return t.StatusOutInTurn + t.StatusOutOffTurn; }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# flow347 check —— 第347期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ================= 定義 =================
        Expect("規定のクグは流す札を持たない（`FlowMode` 0）・版は規定のクグの末尾に札を1枚",
            !UnitCatalog.Kugu.Traits.Any(t => t is TraitId.ThreadFlowCopy or TraitId.ThreadFlowMove or TraitId.ThreadFlowAll)
            && UnitCatalog.KuguFLa.Traits.SequenceEqual(UnitCatalog.Kugu.Traits.Append(TraitId.ThreadFlowCopy))
            && UnitCatalog.KuguFLb.Traits.SequenceEqual(UnitCatalog.Kugu.Traits.Append(TraitId.ThreadFlowMove))
            && UnitCatalog.KuguFLc.Traits.SequenceEqual(UnitCatalog.Kugu.Traits.Append(TraitId.ThreadFlowAll))
            && UnitCatalog.KuguFLa.MaxHp == UnitCatalog.Kugu.MaxHp && UnitCatalog.KuguFLa.Speed == UnitCatalog.Kugu.Speed,
            string.Join("・", new[] { UnitCatalog.KuguFLa, UnitCatalog.KuguFLb, UnitCatalog.KuguFLc }.Select(d => d.Traits.Last())));
        {
            var mats = Vers.Select(v => BattleEngine.Materialize(Formation.Build(front1: v.Kugu ?? UnitCatalog.Kugu), BattleContext.PlayerTeam)[0].FlowMode).ToArray();
            Expect("駒の `FlowMode`: 規定 0 ／ FL-a 1 ／ FL-b 2 ／ FL-c 3", mats.SequenceEqual(new[] { 0, 1, 2, 3 }), string.Join(",", mats));
        }
        Expect("分類表: 害のあるもの 13（毒・燃焼がダメージ系）・害の無いもの 8・電気 ／ 糸 ／ 手番 ／ 組み付きは数えない",
            ThreadFlowRule.Counted.Length == 21 && ThreadFlowRule.HarmfulCount == 13 && ThreadFlowRule.IsDot(StatusKeys.Poison) && ThreadFlowRule.IsDot(StatusKeys.Burn)
            && new[] { StatusKeys.Shock, StatusKeys.Web, StatusKeys.IdleTurn, StatusKeys.Grappled }.All(k => ThreadFlowRule.IndexOf(k) < 0)
            && ThreadFlowRule.Counted.Distinct().Count() == 21 && ThreadFlowRule.Counted.All(k => StatusKeys.All.Contains(k)));

        // ================= 共通 =================
        foreach (var v in Vers.Skip(1))
        {
            {
                var (ctx, k, a, f, _) = Board(v.Kugu!, held: false);
                Write(ctx, a, k, StatusKeys.Poison, 3); Write(ctx, a, k, StatusKeys.Stun, 1);
                Expect($"{v.Ascii}: 組み付いていないときは流れない（毒 3 ／ 痺れはクグに付く・敵は 0）",
                    k.RawCounter(StatusKeys.Poison) == 3 && k.RawCounter(StatusKeys.Stun) == 1 && f.RawCounter(StatusKeys.Poison) == 0 && f.RawCounter(StatusKeys.Stun) == 0
                    && ctx.Events.All(x => x.Kind != BattleEventKind.ThreadFlow));
            }
            {
                var (ctx, k, a, f, _) = Board(v.Kugu!, held: true);
                foreach (var key in ThreadFlowRule.Counted.Skip(ThreadFlowRule.HarmfulCount)) Write(ctx, a, k, key, 5);
                k.SetCounter(StatusKeys.Shock, 1);
                bool kept = ThreadFlowRule.Counted.Skip(ThreadFlowRule.HarmfulCount).All(key => k.RawCounter(key) == 5 && f.RawCounter(key) == 0);
                Expect($"{v.Ascii}: 害の無いもの8種（破片・預かり・据え・紅蓮・灰・板・聖痕・負債）と感電は流れない（クグに付く・敵は 0）",
                    kept && k.RawCounter(StatusKeys.Shock) == 1 && f.RawCounter(StatusKeys.Shock) == 0 && ctx.Events.All(x => x.Kind != BattleEventKind.ThreadFlow));
            }
            {
                var (ctx, k, a, f, fw) = Board(v.Kugu!, held: true);
                Write(ctx, a, k, StatusKeys.Marked, 1);
                Expect($"{v.Ascii}: 味方の付けた標は流れない", k.RawCounter(StatusKeys.Marked) == 1 && f.RawCounter(StatusKeys.Marked) == 0);
            }
            {
                var (ctx, k, a, f, _) = Board(v.Kugu!, held: true);
                long o0 = Out(ctx, "ally347"); int hp0 = f.Hp, ev0 = ctx.Events.Count;
                Write(ctx, a, k, StatusKeys.Poison, 3);
                var evs = ctx.Events.Skip(ev0).ToList();
                var fl = evs.FirstOrDefault(x => x.Kind == BattleEventKind.ThreadFlow);
                long dOut = Out(ctx, "ally347") - o0;
                bool copy = v.Ascii == "a";
                Expect($"{v.Ascii}: 書き手はもとの書き手（味方の書き手の状態異常の書き込みが {(copy ? "2" : "1")} 件増える・出来事の `PartnerId` ＝ 書き手）・クグ自身は書き手にならない",
                    dOut == (copy ? 2 : 1) && Out(ctx, "kugu") == 0 && fl is not null && fl.PartnerId == a.InstanceId && fl.ActorId == k.InstanceId && fl.TargetId == f.InstanceId,
                    $"増えた書き込み {dOut}");
                Expect($"{v.Ascii}: 流すのは攻撃ではない（敵の HP は変わらず・出来事は `ThreadFlow` 1件だけ・`Attack` ／ `Damage` は出ない）",
                    f.Hp == hp0 && evs.Count(x => x.Kind == BattleEventKind.ThreadFlow) == 1 && evs.All(x => x.Kind is not (BattleEventKind.Attack or BattleEventKind.Damage)), $"出来事 {evs.Count} 件");
            }
        }

        // ================= 版ごと =================
        {
            var (ctx, k, a, f, _) = Board(UnitCatalog.KuguFLa, held: true);
            Write(ctx, a, k, StatusKeys.Poison, 3); Write(ctx, a, k, StatusKeys.Poison, 2); Write(ctx, a, k, StatusKeys.Burn, BurnRules.Turns); Write(ctx, a, k, StatusKeys.Stun, 1);
            Expect("FL-a: ダメージ系がクグと組み付いた敵の両方に同じ量（毒 3 ＋ 2 ＝ 5 ／ 燃焼 3）・痺れはクグにだけ",
                k.RawCounter(StatusKeys.Poison) == 5 && f.RawCounter(StatusKeys.Poison) == 5 && k.RawCounter(StatusKeys.Burn) == 3 && f.RawCounter(StatusKeys.Burn) == 3
                && k.RawCounter(StatusKeys.Stun) == 1 && f.RawCounter(StatusKeys.Stun) == 0,
                $"クグ 毒 {k.RawCounter(StatusKeys.Poison)} 燃 {k.RawCounter(StatusKeys.Burn)} ／ 敵 毒 {f.RawCounter(StatusKeys.Poison)} 燃 {f.RawCounter(StatusKeys.Burn)}");
        }
        {
            var (ctx, k, a, f, _) = Board(UnitCatalog.KuguFLb, held: true);
            f.SetCounter(StatusKeys.Poison, 4);
            Write(ctx, a, k, StatusKeys.Poison, 3); Write(ctx, a, k, StatusKeys.Burn, BurnRules.Turns); Write(ctx, a, k, StatusKeys.Stun, 1); Write(ctx, a, k, StatusKeys.Daunted, 1);
            int gains = ctx.Events.Count(x => x.Kind == BattleEventKind.StatusGain && x.TargetId == k.InstanceId);
            Expect("FL-b: ダメージ系が組み付いた敵にだけ付く（敵の毒 4 ＋ 3 ＝ 7 ／ 燃焼 3・クグは 0）・痺れ ／ 萎縮はクグに付く",
                k.RawCounter(StatusKeys.Poison) == 0 && f.RawCounter(StatusKeys.Poison) == 7 && k.RawCounter(StatusKeys.Burn) == 0 && f.RawCounter(StatusKeys.Burn) == 3
                && k.RawCounter(StatusKeys.Stun) == 1 && k.RawCounter(StatusKeys.Daunted) == 1 && f.RawCounter(StatusKeys.Stun) == 0 && f.RawCounter(StatusKeys.Daunted) == 0,
                $"敵 毒 {f.RawCounter(StatusKeys.Poison)}");
        }
        {
            var (ctx, k, a, f, fw) = Board(UnitCatalog.KuguFLc, held: true);
            var harm = ThreadFlowRule.Counted.Take(ThreadFlowRule.HarmfulCount).ToArray();
            foreach (var key in harm) Write(ctx, key == StatusKeys.Marked ? fw : a, k, key, key == StatusKeys.Burn ? BurnRules.Turns : 1);
            foreach (var key in ThreadFlowRule.Counted.Skip(ThreadFlowRule.HarmfulCount)) Write(ctx, a, k, key, 2);
            var miss = harm.Where(key => k.RawCounter(key) != 0 || f.RawCounter(key) <= 0).ToList();
            bool harmless = ThreadFlowRule.Counted.Skip(ThreadFlowRule.HarmfulCount).All(key => k.RawCounter(key) == 2 && f.RawCounter(key) == 0);
            Expect("FL-c: 害のあるもの13種（敵の付けた標を含む）が組み付いた敵にだけ付く・害の無いもの8種はクグに残る",
                miss.Count == 0 && harmless, miss.Count == 0 ? $"流した {ctx.Events.Count(x => x.Kind == BattleEventKind.ThreadFlow)} 件" : "外れ: " + string.Join(",", miss.Select(StatusKeys.LabelOf)));
        }
        {
            // 手番を奪う状態を受け付けない敵（勇者の動じない）には痺れは付かない——クグにも残らない（移す）。
            var ctx = T323.Ctx(Formation.Build(front1: UnitCatalog.KuguFLc, front3: Dummy("ally347")), Formation.Build(center: EnemyCatalog.BossRegular), out var p, out var e);
            var k = T323.Seat(p, 0); var a = T323.Seat(p, 1);
            var boss = e.FirstOrDefault(u => u.ControlProof);
            if (boss is null) Expect("FL-c: 動じない敵へ流した痺れは付かない", false, "ボスの規定形に動じない駒がいない");
            else
            {
                boss.SetCounter(StatusKeys.Grappled, 1); k.SetCounter(GrappleTrait.TargetKey, boss.InstanceId + 1);
                Write(ctx, a, k, StatusKeys.Stun, 1);
                var t = T323.Tal(ctx, "kugu");
                Expect("FL-c: 動じない敵（勇者）へ流した痺れは付かず、クグにも残らない（「敵に付かなかった」に数える）",
                    boss.RawCounter(StatusKeys.Stun) == 0 && k.RawCounter(StatusKeys.Stun) == 0 && (t.Fl347FlowNoGain?[ThreadFlowRule.IndexOf(StatusKeys.Stun)] ?? 0) == 1, boss.Name);
            }
        }
        {
            // 計数: 規定のクグも浴びたものを数える（種類 × 書き手 × 組み付き）。
            var (ctx, k, a, f, fw) = Board(UnitCatalog.Kugu, held: true);
            Write(ctx, a, k, StatusKeys.Poison, 2); Write(ctx, fw, k, StatusKeys.Stun, 1);
            k.SetCounter(GrappleTrait.TargetKey, 0);
            Write(ctx, a, k, StatusKeys.Burn, 3);
            var t = T323.Tal(ctx, "kugu");
            long C(string key, int wi, int hi) => Cell(t.Fl347Cnt, ThreadFlowRule.IndexOf(key), wi, hi);
            Expect("規定のクグ: 流さずに数える（毒 ＝ 味方・組み付いている ／ 痺れ ＝ 敵・組み付いている ／ 燃焼 ＝ 味方・組み付いていない）",
                C(StatusKeys.Poison, 0, 0) == 1 && Cell(t.Fl347Amt, 0, 0, 0) == 2 && C(StatusKeys.Stun, 1, 0) == 1 && C(StatusKeys.Burn, 0, 2) == 1
                && f.RawCounter(StatusKeys.Poison) == 0 && k.RawCounter(StatusKeys.Poison) == 2 && t.Fl347Flowed is null);
        }

        // ================= 戦の中 =================
        {
            // 流れが1度も起きなかった戦は、規定と勝敗 ／ 決着T ／ 全員の HP ／ 電気と濃さの流れ（糸の帯電・濃さの流れ・糸玉）が同じ。
            int n = 0, same = 0, flowed = 0;
            var boards = new[] { Boards()[0], Boards()[2] };
            foreach (var v in Vers.Skip(1))
                foreach (var (_, f) in boards)
                    foreach (var w in Waves)
                        for (int sd = 0; sd < 20; sd++)
                        {
                            var pv = BattleEngine.Materialize(Apply(f, v), BattleContext.PlayerTeam);
                            var rv = BattleEngine.Run(pv, w.Make(), sd, verbose: false);
                            var tv = rv.TallyByUnit.GetValueOrDefault("kugu");
                            if ((tv?.Fl347Flowed?.Sum() ?? 0) > 0) { flowed++; continue; }
                            var pb = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var rb = BattleEngine.Run(pb, w.Make(), sd, verbose: false);
                            var tb = rb.TallyByUnit.GetValueOrDefault("kugu");
                            n++;
                            if (rv.PlayerWon == rb.PlayerWon && rv.Turns == rb.Turns && pv.Select(u => u.Hp).SequenceEqual(pb.Select(u => u.Hp))
                                && tv?.ThreadCharged == tb?.ThreadCharged && tv?.TdKuguFlowed == tb?.TdKuguFlowed && tv?.KuguDisIn == tb?.KuguDisIn && tv?.CreditToEnemy == tb?.CreditToEnemy)
                                same++;
                        }
            Expect($"流れが起きなかった戦（ポンの台 ／ 光の盾 重 × 5 波 × seed 0..19 × 3 版のうち {n} 戦）は、規定と勝敗 ／ 決着T ／ 全員の HP ／ 糸の帯電 ／ 濃さの流れ ／ 放電の受け ／ 仕掛け(敵) が同じ（電気と濃さの流れは今と同じ）",
                n > 0 && same == n, $"同じ {same} ／ {n}・流れた戦 {flowed}");
        }
        {
            int diff = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, f) in Boards())
                    foreach (var w in new[] { Waves[0], Waves[1], Waves[3] })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            var fv = Apply(f, v);
                            var p1 = BattleEngine.Materialize(fv, BattleContext.PlayerTeam); var p0 = BattleEngine.Materialize(fv, BattleContext.PlayerTeam);
                            var r1 = BattleEngine.Run(p1, w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(p0, w.Make(), sd, verbose: false);
                            n++;
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns || !p1.Select(u => u.Hp).SequenceEqual(p0.Select(u => u.Hp))) diff++;
                        }
            Expect($"代表台 4 × ボス ／ 近衛 ／ 第2波 × seed 0..9 × 規定 ＋ 3 版（{n} 戦）: verbose の有無で勝敗 ／ 決着T ／ 味方の HP が同じ", diff == 0, $"違い {diff}");
        }
        {
            // 台本: 移したときはクグへの書き手の StatusGain が出ない（流した出来事が代わりに出る）。
            var r = BattleEngine.Run(BattleEngine.Materialize(Apply(Boards()[1].F, V("b")), BattleContext.PlayerTeam), Waves[1].Make(), 0, verbose: true);
            int flows = r.Events.Count(x => x.Kind == BattleEventKind.ThreadFlow);
            Expect("台本（縛め非収入型 × 近衛 × seed 0 × FL-b）: `ThreadFlow` が出る・`Text` は状態異常の表示名・`StatusRemaining` 0（移した）",
                flows > 0 && r.Events.Where(x => x.Kind == BattleEventKind.ThreadFlow).All(x => x.Text == "燃" && x.StatusRemaining == 0), $"{flows} 件");
        }
        {
            var rows = Presets.Versions.Select(r => r.Name).ToArray();
            bool ok = rows.Contains("試遊・感電 ポンの台") && rows.Contains("試遊・ミサ 書き手なし") && rows.Count(n => n.StartsWith("版・", StringComparison.Ordinal)) == 3
                && Presets.Versions.Where(r => r.Name.StartsWith("版・FL-", StringComparison.Ordinal)).Select(r => r.F[2]).SequenceEqual(new[] { UnitCatalog.KuguFLa, UnitCatalog.KuguFLb, UnitCatalog.KuguFLc });
            Expect("試遊の台: 「試遊・」2台を残し、「版・FL-a ／ FL-b ／ FL-c 縛め非収入型」の3台（中央のクグが版）", ok, string.Join("・", rows));
        }
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 527a5bd8:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第346期（527a5bd8）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
