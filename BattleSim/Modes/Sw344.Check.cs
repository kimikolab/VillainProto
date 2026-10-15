using BattleCore;
using static Common;
using T323 = Tou323Diag;

// sw344 の自己検査（第344期）。盤を直に組んでゴルムの手番（`GolmTurn343`）を呼ぶ（`T323.Ctx`）。
static partial class Sw344Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static UnitDef Dummy(int hp) => new()
    {
        Id = "dummy344", Name = "木偶", MaxHp = hp, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };

    /// <summary>ゴルム（前1）・ザン・ミサ・ヒサ ＋ 木偶 5 体（最大 HP を席ごとに・HP は後で下げる）。</summary>
    static (BattleContext Ctx, UnitState G, List<UnitState> P, List<UnitState> E) Board(UnitDef golm, int belly, params int[] maxHp)
    {
        var foes = Formation.Build(front1: Dummy(maxHp[0]), front3: Dummy(maxHp[1]), center: Dummy(maxHp[2]), back1: Dummy(maxHp[3]), back3: Dummy(maxHp[4]));
        var ctx = T323.Ctx(Formation.Build(front1: golm, front3: UnitCatalog.Zan, back1: UnitCatalog.Tome, back3: UnitCatalog.Hisa), foes, out var p, out var e);
        var g = T323.Seat(p, 0);
        g.SetCounter(ColossusTrait.BellyKey, belly);
        return (ctx, g, p, Enumerable.Range(0, 5).Select(i => T323.Seat(e, i)).ToList());
    }
    static int Belly(UnitState g) => g.RawCounter(ColossusTrait.BellyKey);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# sw344 check —— 第344期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義と試遊の台
        {
            UnitDef A = UnitCatalog.GolmGTa2;
            bool def = UnitCatalog.GolmSWa.Traits.SequenceEqual(A.Traits.Append(TraitId.GolmSwallowSpend)) && UnitCatalog.GolmSWb.Traits.SequenceEqual(A.Traits.Append(TraitId.GolmSwallowWeak))
                && UnitCatalog.GolmSWab.Traits.SequenceEqual(A.Traits.Concat(new[] { TraitId.GolmSwallowSpend, TraitId.GolmSwallowWeak }))
                && UnitCatalog.GolmSWa.PlusText.Contains("。手番で、腹に溜めた量を使って、敵の精気を呑み干す", StringComparison.Ordinal)
                && UnitCatalog.GolmSWb.PlusText.Contains("。手番で、弱った敵（最大 HP の3割以下）の精気を呑み干す", StringComparison.Ordinal) && UnitCatalog.GolmSWab.PlusText == UnitCatalog.GolmSWb.PlusText
                && new[] { UnitCatalog.GolmSWa, UnitCatalog.GolmSWb, UnitCatalog.GolmSWab }.All(d => !UnitCatalog.Everyone.Contains(d) && d.PlusText.EndsWith("。呑み込めない毒や雷は、いちばん大きな敵に吐き捨てる", StringComparison.Ordinal))
                && UnitCatalog.Everyone.All(d => !d.Traits.Any(t => t is TraitId.GolmSwallowSpend or TraitId.GolmSwallowWeak));
            Expect("版の定義: SW-a ／ SW-b ／ SW-a＋b ＝ 第343期の GT-a2 ＋ 札（腹を使う ／ 弱った敵だけ）・手番の句だけ差し替え（吐き捨ての句は残る）・`Everyone` の外・保持者 0", def);
            var v = Presets.Versions; var guard = Presets.Playtest.First(r => r.Name == Golm343Diag.GuardRow).F; var poison = Golm343Diag.PoisonGolm();
            (string Name, Formation Base, UnitDef Golm)[] want = { ("版・SW-a 守り型", guard, UnitCatalog.GolmSWa), ("版・SW-b 守り型", guard, UnitCatalog.GolmSWb), ("版・SW-a＋b 守り型", guard, UnitCatalog.GolmSWab), ("版・SW-a＋b 毒", poison, UnitCatalog.GolmSWab) };
            bool rows = v.Length == 6 && v[0].Name == "試遊・感電 ポンの台" && v[1].Name == "試遊・ミサ 書き手なし" && Presets.Playtest.Length == 17
                && want.All(x => v.Any(r => r.Name == x.Name && r.F.Occupied().Select(o => o.Def).SequenceEqual(x.Base.Occupied().Select(o => o.Def.Id == "golm" ? x.Golm : o.Def))
                    && r.F.Occupied().Select(o => o.Slot).SequenceEqual(x.Base.Occupied().Select(o => o.Slot))));
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」「試遊・ミサ 書き手なし」＋「版・SW-a ／ SW-b ／ SW-a＋b 守り型」「版・SW-a＋b 毒」（第343期の「版・GT」の4台は外した）・`Playtest` は 17 台のまま", rows);
        }

        // ---- SW-a: 呑むと腹が呑んだ HP だけ減る・腹を超える敵は呑めない
        {
            var (ctx, g, _, e) = Board(UnitCatalog.GolmSWa, 50, 40, 300, 300, 300, 300);
            ctx.GolmTurn343(g);
            int b1 = Belly(g);
            e[1].Hp = 20;
            var h = e.ToDictionary(u => u, u => u.Hp);
            ctx.GolmTurn343(g);
            var t = T323.Tal(ctx, "golm");
            bool ok = !e[0].IsAlive && b1 == 10 && e[1].IsAlive && t.Gt343Swallows == 1 && t.Gt343Attacks == 1 && t.Sw344BellySpent == 40;
            Expect("SW-a: 呑むと腹が呑んだ敵の残りの HP だけ減る（50 → 10）・減った腹を超える敵（HP 20）は呑めずに殴る", ok, $"腹 50 → {b1}・2手番目 呑んだ {t.Gt343Swallows} ／ 殴った {t.Gt343Attacks}");
        }

        // ---- SW-b: 最大 HP の 3 割を超える敵は腹以下でも呑めない・腹は減らない
        {
            var (ctx, g, _, e) = Board(UnitCatalog.GolmSWb, 100, 100, 100, 300, 300, 300);
            e[0].Hp = 40;   // 40% → 呑めない
            e[1].Hp = 25;   // 25% → 呑める
            ctx.GolmTurn343(g);
            bool ok = e[0].IsAlive && !e[1].IsAlive && Belly(g) == 100;
            var (ctx2, g2, _, e2) = Board(UnitCatalog.GolmSWb, 100, 100, 300, 300, 300, 300);
            e2[0].Hp = 31;   // 31% → 呑めない（腹以下でも）
            ctx2.GolmTurn343(g2);
            bool none = e2[0].IsAlive && T323.Tal(ctx2, "golm").Gt343Swallows == 0 && T323.Tal(ctx2, "golm").Gt343Attacks == 1;
            Expect("SW-b: 最大 HP の 3 割を超える敵（40% ／ 31%）は腹以下でも呑めない・3 割以下（25%）を呑む・腹は減らない", ok && none, $"腹 100 → {Belly(g)}");
        }

        // ---- SW-a ＋ b: 両方
        {
            var (ctx, g, _, e) = Board(UnitCatalog.GolmSWab, 30, 100, 100, 300, 300, 300);
            e[0].Hp = 40;   // 腹を超える・40%
            e[1].Hp = 25;   // 腹以下・25%
            ctx.GolmTurn343(g);
            bool ok = e[0].IsAlive && !e[1].IsAlive && Belly(g) == 5;
            var (ctx2, g2, _, e2) = Board(UnitCatalog.GolmSWab, 20, 100, 300, 300, 300, 300);
            e2[0].Hp = 25;   // 3 割以下だが腹（20）を超える
            ctx2.GolmTurn343(g2);
            Expect("SW-a＋b: 3 割以下かつ腹以下の敵だけを呑み、腹が減る（30 → 5）・3 割以下でも腹を超えれば呑めない", ok && e2[0].IsAlive && T323.Tal(ctx2, "golm").Gt343Swallows == 0, $"腹 30 → {Belly(g)}");
        }

        // ---- 吐き捨ては第343期の GT-a2 と同じ（同じ盤・同じ手番で、吐きかけた先の状態が一致）
        {
            string Spit(UnitDef d)
            {
                var (ctx, g, _, e) = Board(d, 50, 100, 300, 500, 200, 100);
                e[0].Hp = 20;
                e[0].SetCounter(StatusKeys.Poison, 3); e[0].SetCounter(StatusKeys.Burn, 2); e[0].SetCounter(StatusKeys.Shock, 1); e[0].SetCounter(StatusKeys.Marked, 2);
                ctx.GolmTurn343(g);
                var top = e[2];
                return $"{!e[0].IsAlive}:{top.RawCounter(StatusKeys.Poison)}:{top.RawCounter(StatusKeys.Burn)}:{top.RawCounter(StatusKeys.Shock)}:{top.RawCounter(StatusKeys.Marked)}";
            }
            string gt = Spit(UnitCatalog.GolmGTa2);
            bool same = new[] { UnitCatalog.GolmSWa, UnitCatalog.GolmSWb, UnitCatalog.GolmSWab }.All(d => Spit(d) == gt) && gt == "True:3:2:1:2";
            Expect("吐き捨て: 3 版とも第343期の GT-a2 と同じ（呑んだ毒 3・火 2・雷・標 2 が HP の最も多い敵に足される）", same, gt);
        }

        // ---- verbose ／ PickOne
        {
            int diff = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, f) in Golm343Diag.Boards())
                    foreach (var w in new[] { Waves[0], Waves[1], Waves[3] })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            var fv = Apply(f, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            n++;
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        }
            Expect($"代表台 5 × ボス ／ 近衛 ／ 第2波 × seed 0..9 × 規定 ＋ 4 版（{n} 戦）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show ae9d8fb5:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第343期（ae9d8fb5）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
