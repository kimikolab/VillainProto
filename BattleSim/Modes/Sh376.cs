using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// =====================================================================================
// sh376 —— 第376期【軽い期】段0: ヴィオの「飢えると血を啜る」（第374期の版 C）を規定に ／ 啜りを台本に出す（`BattleEventKind.BloodSip`）。
// 指示書は design/PHASE376_VIO_HUNGER_SPEC.md ／ 報告は design/PHASE376_VIO_HUNGER.md。本体の版は無い。
//
//     dotnet run --project BattleSim -c Release 0 sh376 check     # 自己検査（段0 ／ 出来事 ／ verbose と再現性 ／ checkup ／ 試遊の台 ／ PickOne ／ Roll）
//     dotnet run --project BattleSim -c Release 0 sh376 summary   # 毒の代表の台 ＋ 試遊・ヴィオ 台B × 4 波: 第375期の規定（ヴィオ ＝ Va）→ 規定（C）・1戦の啜り（seed 0..99）
// =====================================================================================
static class Sh376Diag
{
    const string Tag = "第376期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode == "check") { Check(); return; }
        if (mode == "summary") { Summary(); return; }
        Console.WriteLine("sh376: モードは check / summary。");
    }

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate376", Name = "仲間", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe376", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly I.W[] Waves = Sh367Diag.PWaves;

    static void Summary()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = new (string Name, Formation F)[]
        {
            ("試遊・毒", Presets.Versions.First(r => r.Name == "試遊・毒").F),
            ("試遊・毒 席替え", Presets.Versions.First(r => r.Name == "試遊・毒 席替え").F),
            ("毒特化・集団", CompareBuilds().First(r => r.Name == Sh367Diag.BombRow).F),
            ("試遊・ヴィオ 台B", Presets.Versions.First(r => r.Name == "試遊・ヴィオ 台B").F),
        };
        var waves = new[] { I.Boss, Sh366Diag.BossNoTax, I.Guard, I.Bat };
        Console.WriteLine($"# {Tag} 段0 の動き —— 第375期の規定（ヴィオ ＝ Va・`Pin375`）→ 規定（C）× {waves.Length} 波（seed 0..99）");
        Console.WriteLine();
        Console.WriteLine("各セル: 第375期の勝率・倒しT → 今の勝率・倒しT（1戦の啜り: 回 ／ HP）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "---|")));
        foreach (var (name, f) in boards)
        {
            var cells = new string[waves.Length];
            Parallel.For(0, waves.Length, wi =>
            {
                var a0 = Sh370Diag.Measure(Pin375(f), waves[wi], 100); var a1 = Sh370Diag.Measure(f, waves[wi], 100);
                long st = 0, hp = 0;
                for (int sd = 0; sd < 100; sd++) { var r = I.Raw(f, waves[wi], sd).R; if (r.TallyByUnit.TryGetValue("vio", out var vt)) { st += vt.Z374SipTurns; hp += vt.Z374SipHp; } }
                cells[wi] = $"{S307.F1(a0.Win)}・{a0.WinTs}T → {S307.F1(a1.Win)}・{a1.WinTs}T（{S307.F1(st / 100.0)} ／ {S307.F1(hp / 100.0)}）";
            });
            Console.WriteLine($"| {name} | {string.Join(" | ", cells)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh376 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            var v = UnitCatalog.Vio; var h = UnitCatalog.VioH375;
            bool cards = v.Traits.SequenceEqual(h.Traits.Append(TraitId.VioBloodSip)) && v.Traits.SequenceEqual(UnitCatalog.VioC.Traits) && v.MinusText == "仲間が汚れていないターンは、飢えて隣の仲間の血を啜る" && v.MinusText == UnitCatalog.VioC.MinusText
                && v.PlusText == h.PlusText && UnitCatalog.All.Contains(v) && !UnitCatalog.All.Contains(h) && UnitCatalog.All.Count == 52 && h.Traits.SequenceEqual(UnitCatalog.VioVa.Traits);
            bool pins = PinV375(v) == h && PinV375(UnitCatalog.Gald) == UnitCatalog.Gald && PinS374(v) == h && PinV372(v) == UnitCatalog.VioH372 && PinV372(h) == UnitCatalog.VioH372
                && PinGVS371(h) == UnitCatalog.VioH371 && PinGVH367(h) == UnitCatalog.VioH367
                && Pin375(Pin395(Presets.PoisonHero367())).Occupied().Select(o => o.Def).SequenceEqual(Pin395(Presets.PoisonHero367(vio: h)).Occupied().Select(o => o.Def));
            Expect("段0: 規定のヴィオ ＝ 第375期の規定（`VioH375` ＝ Va）＋ 血を啜る・マイナスは第374期の版 C の文面・旧の定義 `VioH375`（`All` の外）・固定の口（`Pin375` ／ `PinS374` ／ `PinV372` ／ `PinGVS371` ／ `PinGVH367` が旧へ）", cards && pins);
        }
        {
            long diff = 0, cells = 0, unexplained = 0;
            var diffs = new List<string>();
            foreach (var n in new[] { "試遊・毒", "試遊・毒 席替え", "試遊・毒 紅" })
            {
                var now = Pin395(Presets.Versions.First(r => r.Name == n).F);   // 第396期: ドルガは第395期までの規定に固定
                var old = Pin375(now);
                foreach (var w in Waves)
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        var rn = I.Raw(now, w, sd).R;
                        if (Same(rn, I.Raw(old, w, sd).R)) continue;
                        diff++;
                        if (diffs.Count < 6) diffs.Add($"{n} × {w.Name} × seed {sd}");
                        if (!(rn.TallyByUnit.TryGetValue("vio", out var vt) && vt.Z374SipTurns > 0)) unexplained++;
                    }
            }
            // 毒の台でも啜りはまれに起きる（第374期: 1戦 0.1〜0.2 回）ので、勝敗 ／ T ／ 生存数が違う戦は「啜りが起きた戦」だけであることを確かめる（違う戦の数は報告に書く）。
            Expect("段0: 規定の台（試遊・毒 ／ 席替え ／ 紅）と第375期の規定（ヴィオ ＝ Va）の台で勝敗 ／ T ／ 生存数が違う戦は、どれも啜りが起きた戦（6 波 × seed 0..9）", unexplained == 0, $"{cells} 戦・差 {diff}（啜りの起きていない差 {unexplained}）{(diffs.Count == 0 ? "" : "：" + string.Join("・", diffs))}");
            long d2 = 0, c2 = 0;
            var play = Presets.Versions.First(r => r.Name == "試遊・ヴィオ 台B").F;
            var v375 = Presets.BoardBVio375(UnitCatalog.VioC);
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M5 })
                for (int sd = 0; sd < 20; sd++) { c2++; if (!Same(I.Raw(play, w, sd).R, I.Raw(v375, w, sd).R)) d2++; }
            Expect("「試遊・ヴィオ 台B」（ヴィオ・ミサ・ヒサ・カド・アカ・規定の駒）が第375期の「版・C 台Bにヴィオ」と勝敗 ／ T ／ 生存数が同じ（5 波 × seed 0..19）", d2 == 0 && S307.Seats(play) == "ヴィオ・ミサ・ヒサ・カド・アカ", $"{c2} 戦・差 {d2}・{S307.Seats(play)}");
        }

        // ---- 出来事 ----
        {
            var ctx = new BattleContext(0, true);
            var f = new Formation(); f[3] = UnitCatalog.Vio; f[0] = Mate; f[2] = Mate; f[4] = Mate;
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = BattleEngine.Materialize(Formation.Build(center: Foe), BattleContext.EnemyTeam, EnemyScaleRule.None);
            foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
            var v = p.First(u => u.Def.Id == "vio");
            p.First(u => u.Slot == 2).Hp = 3;
            foreach (var t in v.Traits) t.OnTurnStart(ctx, v);
            var res = ctx.Events;
            var ev = res.Where(x => x.Kind == BattleEventKind.BloodSip).ToList();
            var n0 = p.First(u => u.Slot == 0); var n2 = p.First(u => u.Slot == 2);
            bool ok = ev.Count == 2 && ev.All(x => x.ActorId == v.InstanceId)
                && ev.Any(x => x.TargetId == n0.InstanceId && x.Amount == 4 && x.HpAfter == n0.Hp && n0.Hp == n0.MaxHp - 4)
                && ev.Any(x => x.TargetId == n2.InstanceId && x.Amount == 2 && x.HpAfter == 1 && n2.Hp == 1)
                && p.First(u => u.Slot == 4).Hp == Mate.MaxHp && !res.Any(x => x.Kind == BattleEventKind.Damage);
            Expect("出来事 `BloodSip`: 啜られた味方ごとに1つ（隣2体 ＝ 2つ・離れた味方には出ない）・啜った駒 ／ 啜られた駒 ／ 量 ／ 啜られた後の HP が盤面と一致（HP 3 からは 2 で HP 1）・`Damage` は出ない",
                ok, $"{ev.Count} 件 [{string.Join(",", ev.Select(x => $"{x.Amount}→HP{x.HpAfter}"))}]");
        }
        {
            long evN = 0, evAmt = 0, sipHp = 0, mism = 0, battles = 0, hpBad = 0;
            var play = Presets.Versions.First(r => r.Name == "試遊・ヴィオ 台B").F;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat })
                for (int sd = 0; sd < 20; sd++)
                {
                    battles++;
                    var (r, p, _) = I.Raw(play, w, sd, verbose: true);
                    var ev = r.Events.Where(x => x.Kind == BattleEventKind.BloodSip).ToList();
                    evN += ev.Count; evAmt += ev.Sum(x => (long)x.Amount);
                    long tally = r.TallyByUnit.TryGetValue("vio", out var vt) ? vt.Z374SipHp : 0;
                    sipHp += tally;
                    if (ev.Sum(x => (long)x.Amount) != tally) mism++;
                    foreach (var (id, t) in r.TallyByUnit)
                    {
                        var ids = p.Where(u => u.Def.Id == id).Select(u => u.InstanceId).ToHashSet();
                        if (ev.Where(x => x.TargetId is int ti && ids.Contains(ti)).Sum(x => (long)x.Amount) != t.Z374SippedFrom) mism++;
                    }
                    if (ev.Any(x => x.HpAfter < 1)) hpBad++;
                }
            Expect("出来事の数と量が啜りの計数と一致（試遊・ヴィオ 台B × 勇者 ／ 近衛 ／ 大隊・seed 0..19: 量の合計 ＝ ヴィオの啜った HP・啜られた駒ごとの量 ＝ その駒の啜られた HP・HP は 1 未満にならない）", mism == 0 && hpBad == 0 && evAmt == sipHp && evN > 0,
                $"{battles} 戦・出来事 {evN} 件・量 {evAmt} ＝ 啜った HP {sipHp}・食い違い {mism}");
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var n in new[] { "試遊・毒", "試遊・毒 席替え", "試遊・毒 紅", "試遊・ヴィオ 台B" })
            {
                var f = Presets.Versions.First(r => r.Name == n).F;
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（4台 × 勇者 ／ 近衛 ／ 大隊・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool ok = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.VioBloodSip\][^\n]*第376期から規定") && UnitCatalog.Vio.Traits.All(t => src.Contains($"[TraitId.{t}]", StringComparison.Ordinal));
            Expect("`checkup` の札の表で血を啜るは「第376期から規定」・規定のヴィオの札がそろう（ソースを読んで確かめる）", ok);
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.Take(11).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "試遊・毒 席替え", "試遊・毒 紅", "試遊・ヴィオ 台B" });
            Expect("`Presets.Versions` ＝ 試遊の8台 ＋「試遊・毒 席替え」＋「試遊・毒 紅」＋「試遊・ヴィオ 台B」・第375期の版の台は無い", rows, string.Join("、", vr));
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show aa917fa8:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第375期の後の HEAD（aa917fa8）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
