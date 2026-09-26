using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第213期） —— ササの身構え：上限だけ破片より前に（受け切った一撃では弾かない）
//
// 指示書は design/PHASE213_BRACE_CAP_SPEC.md ／ 報告は design/PHASE213_BRACE_CAP.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 tsugi phase213  # Q0-1・Q0-2・Q0-4（Z2 ／ Z3 の盤面で数える）
// =====================================================================================

static partial class TsugiDiag
{
    static partial void RunMore213(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "phase213": Phase213(); handled = true; return;
            default: RunMore213b(mode, arg, ref handled); return;
        }
    }

    static partial void RunMore213b(string mode, string arg, ref bool handled);

    /// <summary>第213期の2台: `継ぎ当て×分散回復`（X 字）とポンの編成（パターン2）。</summary>
    static IEnumerable<(string Name, string Key, Formation F)> BraceTables()
    {
        yield return ("継ぎ当て×分散回復", "継ぎ当て×分散回復", TsugiRows().First(r => r.Name == "継ぎ当て×分散回復").F);
        yield return ("ポンの編成", "pon", PonFormation());
    }

    static void Phase213()
    {
        Console.WriteLine("# 第213期 `tsugi phase213` —— Q0-1・Q0-2・Q0-4（seed 0..199・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**盤面は第212期のまま**（足したのは計数だけ）。Z2 ＝ ササの札は `Brace` だけ ／ Z3 ＝ ＋ `BraceArmored`。ツギはどちらも第212期の Z2。");
        Console.WriteLine();

        Console.WriteLine("## Q0-1 W で消える弾き（Z2）");
        Console.WriteLine();
        Console.WriteLine("**該当** ＝ 身を固めているササへの敵の一撃のうち、上限（" + BraceRule.Default.Cap + "）を超え、破片が一部だけ吸って残りが HP に届き、しかも破片が上限以上あった回数/戦"
                          + "（W なら上限が先に掛かって破片が受け切り、HP に届かない）。（ ）はその HP/戦。**うち弾いた** ＝ その一撃で弾き（錯乱）が起きた回数/戦（そのターン最初の弾き）。"
                          + "**弾き** は Z2 の弾き/戦の全部。**比** ＝ うち弾いた ÷ 弾き（W で Z2 より弾きが減る分の見当）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 該当（HP） | うち弾いた | 弾き | 比 | 受け切り（Z2 で既に鳴らない） |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|");
        foreach (var (name, key, f) in BraceTables())
            foreach (int st in Waves)
            {
                var a = Measure(key + "|Z2", ApplyZ(f, "tsugi", "Z2"), st);
                var s = a.Of("sasa");
                Console.WriteLine("| " + name + " | " + (st + 1) + " | " + a.Per(s.BraceWouldMute).ToString("F2") + "（" + a.Per(s.BraceWouldMuteHp).ToString("F1") + "） | "
                                  + a.Per(s.BraceWouldMuteShoved).ToString("F2") + " | " + a.Per(s.BraceShoves).ToString("F2") + " | " + Pct(s.BraceWouldMuteShoved, s.BraceShoves) + " | "
                                  + a.Per(s.BraceArmorFullMuted).ToString("F2") + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-2 宛先の無い保留（Z3）");
        Console.WriteLine();
        Console.WriteLine("**先に切った** ＝ 破片より先に上限で切った回数/戦（量/戦）。**消えた** ＝ 宛先が無いままターンが変わって消えた保留/戦（`BraceLost`・全部の切り落としの合計）。"
                          + "**受け切りで配った** ＝ Z3 で破片が受け切った一撃の中で配った量/戦——W1 ではこれが配られず、宛先が既にいなければ W2 でも配られない。"
                          + "**受け切りで弾いた** ＝ Z3 で破片が受け切った一撃の中で弾いた回数/戦（W1・W2 では宛先が作られない）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 切り落とし（全部） | 先に切った（量） | 配った | 消えた | 受け切りで配った | 受け切りで弾いた |");
        Console.WriteLine("|---|--:|---|--:|---|--:|--:|--:|--:|");
        foreach (var (name, key, f) in BraceTables())
            foreach (int st in Waves)
                foreach (var tag in new[] { "Z2", "Z3" })
                {
                    var a = Measure(key + "|" + tag, ApplyZ(f, "tsugi", tag), st);
                    var s = a.Of("sasa");
                    Console.WriteLine("| " + name + " | " + (st + 1) + " | " + tag + " | " + a.Per(s.BraceRefused).ToString("F1") + " | " + a.Per(s.BraceArmorEarly).ToString("F2") + "（" + a.Per(s.BraceArmorEarlyAmt).ToString("F1") + "） | "
                                      + a.Per(s.BraceGiven).ToString("F1") + " | " + a.Per(s.BraceLost).ToString("F1") + " | " + a.Per(s.BraceGivenArmorOnly).ToString("F1") + " | "
                                      + a.Per(s.BraceShovesArmorOnly).ToString("F2") + " |");
                }
        Console.WriteLine();

        Console.WriteLine("## Q0-4 ササが破片を持ちうる行");
        Console.WriteLine();
        var sasaRows = CompareBuilds().Concat(CrossBuilds()).Where(r => r.F.Occupied().Any(o => o.Def.Id == "sasa")).ToList();
        Console.WriteLine("ササを含む行（`compare` 61 行 ＋ 交差帯 12 行）: " + (sasaRows.Count == 0 ? "なし" : string.Join(" ／ ", sasaRows.Select(r => r.Name + "（" + string.Join("・", r.F.Occupied().Select(o => o.Def.Name)) + "）"))) + "。");
        Console.WriteLine();
        Console.WriteLine("その行でササが破片で受けた一撃（Z2・第2〜5波の合計/戦）: 板の印つき " + string.Join(" / ", Waves.Select(st => Measure("継ぎ当て×分散回復|Z2", ApplyZ(sasaRows.First().F, "tsugi", "Z2"), st)).Select(a => a.Per(a.Of("sasa").PlankHitsTaken).ToString("F2")))
                          + " ・ 身を固めていて上限を超えた " + string.Join(" / ", Waves.Select(st => Measure("継ぎ当て×分散回復|Z2", ApplyZ(sasaRows.First().F, "tsugi", "Z2"), st)).Select(a => a.Per(a.Of("sasa").BraceArmorHits).ToString("F2"))) + "。");
        Console.WriteLine();
        Console.WriteLine("ササを含まない行では、ササの身構えの札は1度も読まれない（保持者がいないので `_braceArmoredLive` が偽）。");
        Console.WriteLine();
    }
}
