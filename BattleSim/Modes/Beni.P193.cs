using BattleCore;
using static Common;

// =====================================================================================
// beni phase193 / ledger193（第193期） —— 溢れた回復をベニが啜る
//
// 指示書は design/PHASE193_BENI_SIP_SPEC.md ／ 報告は design/PHASE193_BENI_SIP.md。
//
//     dotnet run --project BattleSim -c Release 0 beni phase193    # Q0-3 / Q0-4（`OverflowToHolder = false` のビルドで回す＝盤面は第192期のまま・計数だけ）
//     dotnet run --project BattleSim -c Release 0 beni ledger193   # 帳簿（書き手あり／なし・ベニの隣の枚数・流し込み）。**`OverflowToHolder` はビルドで切り替える**
// =====================================================================================

static partial class BeniDiag
{
    /// <summary>
    /// 第192期の `beni phase192` で、ベニ自身に入る毒・燃焼の刻みが 1/戦未満だった 6 行（＝書き手がベニに届かない行）。
    /// <b>第192期の報告の表をそのまま写す</b>（この期に選び直さない）。
    /// </summary>
    static readonly string[] NoWriterRows =
    {
        "毒爆弾 (ラウ×ヴィオ)", "澱み喰い (グザ×ヴィオ)", "範囲耐性 (ヒビ×ボルグ)",
        "刻み×澱み (ノミ×ミオ)", "置き去り×死の連鎖", "鱗改 (ウロ×ヒビ)",
    };

    readonly record struct Sip(double Eligible, double Room, double Gained, double Early, double DiedShare, double DeathTurn,
                               double Nominal, double Healed);

    static Sip SipOf(Formation f)
    {
        double el = 0, ro = 0, ga = 0, ea = 0, nom = 0, hea = 0, dt = 0; int n = 0, deaths = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                n++;
                if (!r.TallyByUnit.TryGetValue("beni", out UnitTally? b)) continue;
                el += b.SipEligible; ro += b.SipRoom; ga += b.SipGained; ea += b.SipEarly;
                nom += b.InverseNominal; hea += b.InversePoisonHealed + b.InverseBurnHealed + b.InverseDetonateHealed;
                if (b.Deaths > 0) { deaths++; dt += b.LastActiveTurn; }
            }
        return new Sip(el / n, ro / n, ga / n, ea / n, (double)deaths / n, deaths == 0 ? 0 : dt / deaths, nom / n, hea / n);
    }

    static IEnumerable<(string Name, Formation F)> Rows193()
    {
        foreach (var (_, n, f) in BeniRows()) yield return (n, f);
        foreach (var (n, g) in SwapRows()) yield return (n, g);
    }

    static (int Slot, int Adj) BeniSeat(Formation f)
    {
        var occ = f.Occupied().ToList();
        int slot = occ.First(o => o.Def.Id == "beni").Slot;
        return (slot, occ.Count(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)));
    }

    static void Phase193()
    {
        Console.WriteLine("# 第193期 `beni phase193` —— Q0-3 / Q0-4（`OverflowToHolder = " + InverseTrait.OverflowToHolder + "`）");
        Console.WriteLine();
        if (InverseTrait.OverflowToHolder)
            Console.WriteLine("**注意: `OverflowToHolder` が真のビルド。Q0-3 は流す前の見積もりなので、偽のビルドで回すこと。**");
        Console.WriteLine("`溢れ` ＝ 隣の味方への反転の回復のうち満タンで溢れた量（渇き・支援拒否で止められた分とベニ自身の分を除く）。"
                          + "`受け取れた上限` ＝ そのときのベニの失った HP と溢れの小さいほう（1段ずつ足した。**ベニが先に満たされる分は数えていない**ので上振れする）。第2〜5波・seed 0..199。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 書き手 | ベニの席 | 隣の枚数 | 溢れ（/戦） | 受け取れた上限（/戦） | 反転の名目 | 実際に癒えた | ベニが倒れた戦 | 倒れたT |");
        Console.WriteLine("|---|:-:|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in Rows193())
        {
            var (slot, adj) = BeniSeat(f);
            Sip s = SipOf(f);
            Console.WriteLine("| " + name + " | " + (NoWriterRows.Contains(name) ? "**なし**" : "あり") + " | " + SlotName(slot) + " | " + adj + " | "
                              + s.Eligible.ToString("F1") + " | " + s.Room.ToString("F1") + " | " + s.Nominal.ToString("F1") + " | " + s.Healed.ToString("F1")
                              + " | " + (100 * s.DiedShare).ToString("F1") + "% | " + s.DeathTurn.ToString("F2") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- Q0-4 の 6 行（書き手がベニに届かない）は第192期の報告の表をそのまま写した: " + string.Join(" ／ ", NoWriterRows));
        Console.WriteLine();
    }

    static void RunLedger193()
    {
        Console.WriteLine("# 第193期 `beni ledger193` —— 啜りの帳簿（`OverflowToHolder = " + InverseTrait.OverflowToHolder + "`・1戦あたり・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`流れ込んだ` は実際にベニの HP が増えた量、`捨てた` ＝ 溢れ − 流れ込んだ（ベニが満タン・倒れていた）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 書き手 | 隣の枚数 | 溢れ | 流れ込んだ（うち 1〜3T） | 捨てた | 反転の名目 | ベニが倒れた戦 | 倒れたT |");
        Console.WriteLine("|---|:-:|--:|--:|---|--:|--:|--:|--:|");
        var groups = new Dictionary<string, List<Sip>>();
        foreach (var (name, f) in Rows193())
        {
            var (_, adj) = BeniSeat(f);
            Sip s = SipOf(f);
            bool noWriter = NoWriterRows.Contains(name);
            Console.WriteLine("| " + name + " | " + (noWriter ? "**なし**" : "あり") + " | " + adj + " | " + s.Eligible.ToString("F1") + " | "
                              + s.Gained.ToString("F1") + "（" + s.Early.ToString("F1") + "） | " + (s.Eligible - s.Gained).ToString("F1") + " | "
                              + s.Nominal.ToString("F1") + " | " + (100 * s.DiedShare).ToString("F1") + "% | " + s.DeathTurn.ToString("F2") + " |");
            foreach (string g in new[] { noWriter ? "書き手なし" : "書き手あり", "隣 " + adj + " 枚", "全体" })
                (groups.TryGetValue(g, out var l) ? l : groups[g] = new List<Sip>()).Add(s);
        }
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | 溢れ | 流れ込んだ | うち 1〜3T | ベニが倒れた戦 | 倒れたT |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (g, l) in groups.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            Console.WriteLine("| " + g + " | " + l.Count + " | " + l.Average(x => x.Eligible).ToString("F1") + " | " + l.Average(x => x.Gained).ToString("F1")
                              + " | " + l.Average(x => x.Early).ToString("F1") + " | " + (100 * l.Average(x => x.DiedShare)).ToString("F1") + "% | "
                              + l.Average(x => x.DeathTurn).ToString("F2") + " |");
        Console.WriteLine();
    }
}
