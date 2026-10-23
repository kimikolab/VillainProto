using BattleCore;
using static Common;
using I = Inv348Diag;

// sh385 check —— 第385期の自己検査（指示書 §5 の「新しい器具に最低限入れる」）。
static partial class Sh385Diag
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
        Console.WriteLine($"# {Tag} sh385 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var ere = UnitCatalog.Ere; var g1 = UnitCatalog.Bard384G1; var uke = UnitCatalog.Uke;

        {
            bool card = ere.Id == "ere" && ere.Name == "哀歌のエレ" && ere.Traits.SequenceEqual(new[] { TraitId.DirgeSoundG1, TraitId.DirgeEchoAlways }) && ere.Traits.SequenceEqual(g1.Traits)
                && ere.MaxHp == 40 && ere.Attack == 3 && ere.Speed == 9 && !ere.Advances && ere.Actions is { Count: 1 } a && a[0].Kind == ActionKind.Skill
                && ere.PlusText == g1.PlusText && ere.MinusText == g1.MinusText && ere.Flavor.StartsWith("葬列の後ろでしか歌わせてもらえない");
            bool roster = UnitCatalog.All.Contains(ere) && !UnitCatalog.All.Contains(uke) && UnitCatalog.Retired.Contains(uke) && !UnitCatalog.Retired.Contains(ere) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.Contains(uke) && UnitCatalog.Everyone.Contains(ere) && AllH384().Contains(uke) && !AllH384().Contains(ere)
                && Array.IndexOf(UnitCatalog.All.ToArray(), ere) == Array.IndexOf(AllH384(), uke);
            bool vers = new[] { UnitCatalog.Bard382, UnitCatalog.Bard382n, UnitCatalog.Bard383, g1, UnitCatalog.Bard384G2 }.All(d => d.Id == "bard382" && !UnitCatalog.Everyone.Contains(d));
            Expect("段0: 哀歌のエレ ＝ 第384期の G1（札・体・文面）・`Id` ＝ `ere`・`All` にエレ（ウケの位置）・ウケは `Retired`（定義は残す）・ロスター 52 枚・版の楽士は `bard382` のまま", card && roster && vers,
                $"「{ere.PlusText}」／「{ere.MinusText}」／「{ere.Flavor}」");
        }
        {
            long cells = 0, diff = 0;
            foreach (var f in new[] { Presets.DeathBard382(ere), Presets.BurnBard384(ere), Sh382Diag.ChainWith(ere) })
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++) { cells++; if (!Same(I.Raw(f, w, sd).R, I.Raw(Pin385(f), w, sd).R)) diff++; }
            Expect("エレは第384期の `Bard384G1` と同じ動き（エレの3台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..9 で勝敗 ／ T ／ 生存数が同じ）", diff == 0 && cells == 120, $"{cells} 戦・差 {diff}");
        }
        {
            var rows = CompareBuilds();
            bool added = CompareRows385.All(n => rows.Any(r => r.Name == n) && !Baseline.PrimaryRows.Contains(n)) && rows.Length == 66 && CompareBuildsH384().Length == 64
                && rows.First(r => r.Name == CompareRows385[0]).F.Occupied().Select(o => o.Def).SequenceEqual(Presets.BurnBard384(ere).Occupied().Select(o => o.Def))
                && rows.First(r => r.Name == CompareRows385[1]).F.Occupied().Select(o => o.Def).SequenceEqual(Presets.DeathBard382(ere).Occupied().Select(o => o.Def));
            bool ukeRow = rows.Concat(CrossBuilds()).Any(r => r.Name == "弱体×破片 (ウケ×ネル×ウロ)" && r.F.Occupied().Any(o => ReferenceEquals(o.Def, uke)));
            Expect("`compare` に足した2行（燃焼×哀歌 ／ 死軸×哀歌）がある・主判定19行に入っていない・66 行（過去の器具の `CompareBuildsH384` は 64 行）・ウケの行（交差帯の「弱体×破片 (ウケ×ネル×ウロ)」）は前例どおり残す", added && ukeRow, string.Join("・", CompareRows385));
        }
        {
            var vr = VersionsH385().Select(r => r.Name).ToArray();   // 第386期: 版の2台を足す前の一覧で見る
            bool ok = vr.Contains("試遊・死 エレ") && vr.Contains("試遊・燃焼 エレ") && !vr.Any(n => n.StartsWith("版・"))
                && Presets.Versions.First(r => r.Name == "試遊・死 エレ").F.Occupied().Any(o => ReferenceEquals(o.Def, ere))
                && Presets.Versions.First(r => r.Name == "試遊・燃焼 エレ").F.Occupied().Any(o => ReferenceEquals(o.Def, ere))
                && Presets.Versions.All(r => r.F.Occupied().All(o => o.Def.Id != "bard382"));
            Expect("試遊の台: 「版・楽士 死 ／ 外」→「試遊・死 エレ ／ 試遊・燃焼 エレ」（規定の駒）・「版・」の台は 0・版の楽士は試遊の台にいない", ok, string.Join("、", vr.Skip(11)));
        }
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool ok = new[] { TraitId.DirgeSoundG1, TraitId.DirgeEchoAlways }.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第385期から規定（エレ）"))
                && System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.Bear\]\s*=\s*\(HcBothL,");
            Expect("`checkup` の札の分類表: エレの札（哀歌の音 ／ 哀歌は敵の耳にも届く）が「第385期から規定（エレ）」・ウケの札（引き受け）の行は前例どおり残す（ソースを読んで確かめる）", ok);
        }
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var f in new[] { Presets.DeathBard382(ere), Presets.BurnBard384(ere) })
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（エレの2台 × ボス ／ 近衛 ／ 大隊 ／ 第5波・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show f709c49c:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第384期の HEAD（f709c49c）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
