using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;

// db356 の自己検査（第356期・指示書 §7）。
static partial class Db356Diag
{
    /// <summary>§5 の代表台（`seats` の結果から選んだ並び・報告 §1 の決めたこと）。</summary>
    static readonly (string Label, string Key)[] RepKeys =
    {
        ("A ボス最良 ＝ ムド残しの最良 ＝ 精鋭と両立の最良", "ウツ・ワタ・ネル・ムド・ツギ"),   // `seats` の1位（§3-3 の 1〜3 がこの1並びに重なった）
        ("B 全並び勝ちの組（クビ＋ツギ）の最良", "クビ・ツギ・ウツ・ネル・ワタ"),          // 120 ／ 120 並びがボスに勝つ唯一の組
        ("C ツギなしの最良（試遊・呪いのネルとクビの入れ替え）", "ワタ・ムド・ネル・クビ・ウツ"),   // ヒーラーを入れない並びの1位（ムド＋クビ）
    };

    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} db356 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        var seats = AllSeats();
        bool pop = seats.Length == 1800 && seats.Select(s => s.Key).Distinct().Count() == 1800 && seats.Select(s => s.Pair).Distinct().Count() == 15
            && seats.All(s => s.F.Occupied().Count() == 5 && Fixed.All(d => s.F.Occupied().Any(o => ReferenceEquals(o.Def, d))));
        Expect("母集団: ネル ／ ワタ ／ ウツ（規定）＋ 6枚から2枚の 15 組 × 120 並び ＝ 1,800 並び（重なりなし）", pop, $"{seats.Length} 並び・{seats.Select(s => s.Pair).Distinct().Count()} 組");
        bool def = ReferenceEquals(Fixed[0], UnitCatalog.Nel) && UnitCatalog.Nel.Traits.Contains(TraitId.CurseScar) && Pool.All(d => UnitCatalog.All.Contains(d));
        Expect("段0 入りの規定の駒で組む（ネル ＝ N-100・6枚はどれも `UnitCatalog.All` の規定）", def);

        // 器具の有無で勝敗が変わらない: 計数を渡す戦（`inv348` の Raw）と渡さない戦（この器具の Fight）で、勝敗 ／ T ／ 倒れた数が同じ
        int diff = 0, n = 0;
        foreach (var s in seats.Where((_, i) => i % 97 == 0))
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat }) for (int seed = 0; seed < 3; seed++)
            {
                var c = Fight(s.F, w, seed);
                var (r, p, _) = I.Raw(s.F, w, seed);
                var (rv, _, _) = I.Raw(s.F, w, seed, verbose: true);
                n++;
                if ((c.Wins == 1) != r.PlayerWon || (c.Wins == 1 && c.WinT != r.Turns) || c.Fell != p.Count(u => u.LastDeathTurn > 0) || r.PlayerWon != rv.PlayerWon || r.Turns != rv.Turns) diff++;
            }
        Expect("器具の有無で勝敗が変わらない（計数あり ／ なし ／ verbose の3つで勝敗 ／ T ／ 倒れた数が同じ・19 並び × 3波 × seed 0..2）", diff == 0, $"{n} 戦・差 {diff}");

        var vr = Presets.Versions.Select(r => r.Name).ToArray();
        // 第358期: 「試遊・呪い 壁」の後ろに別の期の「版・」の台が続いてよい（第355期の「版・」の3台が無いことだけを見る）。
        bool rows = vr.Take(4).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", D.PlayRow }) && vr.Skip(4).Take(1).SequenceEqual(new[] { WallRow })
            && vr.Skip(5).All(x => x.StartsWith("版・", StringComparison.Ordinal)) && !vr.Any(x => x.StartsWith("版・", StringComparison.Ordinal) && x.EndsWith(" 呪い", StringComparison.Ordinal));
        Expect($"`Presets.Versions` ＝ 試遊の4台 ＋ 「{WallRow}」（第355期の「版・」の3台は外した・後ろは別の期の「版・」の台だけ）", rows, string.Join("、", vr));
        if (rows)
        {
            var wall = Presets.Versions.First(r => r.Name == WallRow).F;
            Expect($"「{WallRow}」＝ 代表台の「{WallPick}」・規定の駒", Reps.Any(r => r.Key == Seats(wall)) && Seats(wall) == WallPick && wall.Occupied().All(o => UnitCatalog.All.Contains(o.Def)), Seats(wall));
        }

        int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
        string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show ccd7bb3a:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
        int e0 = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, t0 = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
        int e1 = Now("BattleCore/BattleEngine.cs"), t1 = Now("BattleCore/Traits.cs");
        string tok = "ctx." + "PickOne(";
        int mine = Directory.GetFiles("BattleSim/Modes", "Db356*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
        Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第355期（ccd7bb3a）と同じ・器具の中も 0）", e0 == e1 && t0 == t1 && e0 > 0 && mine == 0, $"engine {e0} → {e1}・traits {t0} → {t1}・器具 {mine}");

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    /// <summary>「試遊・呪い 壁」に選んだ並び（報告 §1）。</summary>
    internal const string WallPick = "ウツ・ワタ・ネル・ムド・ツギ";
}
