using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// sh375 の自己検査（第375期・指示書 §7）。
static partial class Sh375Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh375 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            var s = UnitCatalog.Sid; var h = UnitCatalog.SidH374;
            bool cards = s.Traits.SequenceEqual(h.Traits.Append(TraitId.SidOpeningCloak10)) && s.Traits.SequenceEqual(UnitCatalog.SidS10.Traits) && s.PlusText == UnitCatalog.SidS10.PlusText && s.MinusText == UnitCatalog.SidS10.MinusText
                && s.PlusText.StartsWith("戦いの前から毒をまとっている。") && UnitCatalog.All.Contains(s) && !UnitCatalog.All.Contains(h) && UnitCatalog.All.Count == 52
                && h.Traits.SequenceEqual(UnitCatalog.SidY.Traits);
            bool pins = PinS374(s) == h && PinS374(UnitCatalog.Gald) == UnitCatalog.Gald && PinV372(s) == h && PinS371(s) == UnitCatalog.SidH371 && PinS371(h) == UnitCatalog.SidH371
                && PinGVS371(h) == UnitCatalog.SidH371 && PinGVH367(s) == UnitCatalog.SidH371
                && Pin374(Pin395(Presets.PoisonHero367())).Occupied().Select(o => o.Def).SequenceEqual(Pin395(Presets.PoisonHero367(vio: UnitCatalog.VioH375, sid: h)).Occupied().Select(o => o.Def));   // 第376期: `Pin374` はヴィオも第375期までの規定へ
            Expect("段0: 規定のスィド ＝ 第374期の S10（`SidH374` ＋ 開戦の衣 +10）・文面は S10 の文面・旧の定義 `SidH374`（`All` の外 ＝ 第372〜374期の規定）・固定の口（`Pin374` ／ `PinV372` ／ `PinS371` ／ `PinGVS371` が旧へ）", cards && pins);
        }
        {
            long diff = 0, cells = 0;
            var pairs = new (Formation Now, Formation S10)[]
            {
                (Pin375(Presets.Versions.First(r => r.Name == "試遊・毒").F), Pin375(Pin395(Presets.PoisonHero367(sid: UnitCatalog.SidS10)))),
                (Pin375(Presets.Versions.First(r => r.Name == "試遊・毒 席替え").F), Pin375(Pin395(Presets.PoisonSeat372(sid: UnitCatalog.SidS10)))),
                (Pin375(Presets.Versions.First(r => r.Name == "試遊・毒 紅").F), Formation.Build(front1: UnitCatalog.Borg, front3: UnitCatalog.SidS10, center: UnitCatalog.Beni, back1: UnitCatalog.Rau, back3: UnitCatalog.Guza)),
            };
            foreach (var (now, s10) in pairs)
                foreach (var w in Sh367Diag.PWaves)
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(now, w, sd).R, I.Raw(s10, w, sd).R)) diff++;
                    }
            Expect("段0: 規定の台（試遊・毒 ／ 席替え ／ 紅）が第374期の S10 の台と勝敗 ／ T ／ 生存数が同じ（6 波 × seed 0..9）", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- C の台 ----
        {
            var bl = BoardList();
            bool noSupplier = bl.SelectMany(b => b.Vs).All(f => !f.Occupied().Any(o => PoisonSuppliers.Contains(o.Def.Id)));
            var sh = bl[1].Vs; var bb = bl[3].Vs;
            bool seats = S307.Seats(sh[0]) == "カド・リリ・ムド・ヴィオ・アカ" && S307.Seats(bb[0]) == "ヴィオ・ミサ・ヒサ・カド・アカ"
                && sh[0][3] == UnitCatalog.VioH375 && sh[1][3] == UnitCatalog.VioC && bb[0][0] == UnitCatalog.VioH375 && bb[1][0] == UnitCatalog.VioC
                && FormationRules.AreAdjacent(3, 0) && FormationRules.AreAdjacent(0, 3);
            Expect("C の台に毒の供給役（グザ ／ スィド ／ ベニ ／ ミオ ／ ラウ）がいない・自傷 ＋ ヴィオ ＝ カド・リリ・ムド・ヴィオ・アカ（ヴィオの隣はカド ／ ムド）・台 B ＋ ヴィオ ＝ ヴィオ・ミサ・ヒサ・カド・アカ（ヴィオの隣はヒサ ／ カド）",
                noSupplier && seats, $"{S307.Seats(sh[0])} ／ {S307.Seats(bb[0])}");
            bool vc = UnitCatalog.VioC.Traits.SequenceEqual(UnitCatalog.VioH375.Traits.Append(TraitId.VioBloodSip)) && !UnitCatalog.All.Contains(UnitCatalog.VioC);
            Expect("C ＝ 規定のヴィオ ＋ 飢えると血を啜る（第374期の札のまま・`All` の外）", vc);
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in BoardList())
                foreach (var f in b.Vs)
                    foreach (var w in new[] { I.Boss, I.Guard })
                        for (int sd = 0; sd < 5; sd++)
                        {
                            cells++;
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（6 並び × 勇者 ／ 近衛・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool ok = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.SidOpeningCloak10\][^\n]*第375期から規定") && UnitCatalog.Sid.Traits.All(t => src.Contains($"[TraitId.{t}]", StringComparison.Ordinal));
            Expect("`checkup` の札の表で開戦の衣 +10 は「第375期から規定」・規定のスィドの札がそろう（ソースを読んで確かめる）", ok);
        }

        // ---- 試遊の台 ----
        {
            // 第376期: 第375期の「版・C 自傷にヴィオ」「版・C 台Bにヴィオ」は外した（C を規定にした・並びはこの器具の中で組む）。検査は「頭の10台」と「第375期の版の台が無い」に。
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.Take(10).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "試遊・毒 席替え", "試遊・毒 紅" })
                && !vr.Any(n => n is "版・C 自傷にヴィオ" or "版・C 台Bにヴィオ");
            Expect("`Presets.Versions` の頭 ＝ 試遊の8台 ＋「試遊・毒 席替え」＋「試遊・毒 紅」・第375期の版の2台は無い（第376期に外した）", rows, string.Join("、", vr));
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show f76fe1ae:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第374期（f76fe1ae）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
