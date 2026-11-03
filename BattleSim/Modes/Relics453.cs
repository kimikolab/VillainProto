using BattleCore;
using static Common;

// =====================================================================================
// relics モード（第453期・指示書 design/PHASE453_RELIC_BRIDGE_SPEC.md §0-1）—— レリックの 12 枚と「両端」（引き金 → 効果）の表。**戦闘0回。**
// 両端の表は `BattleCore/Relics.cs` の `RelicBridges`（手で書く・表示と作戦マップの候補の規則だけが読む）。
//
//     dotnet run --project BattleSim -c Release 0 relics > docs/relics.md
// =====================================================================================

static class Relics453Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        Console.WriteLine("# レリックの札と両端");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 relics > docs/relics.md` の出力。手で編集しない。札は `RelicCatalog.All`、両端は `RelicBridges`（`BattleCore/Relics.cs`・手で書く）。"
            + "**両端は表示と作戦マップの候補の規則だけが読む——どの戦闘の規則も読まない。**");
        Console.WriteLine();
        Console.WriteLine("- 引き金 ＝ 何が起きると働くか ／ 効果 ＝ 何になるか。語は駒のタグ（`docs/tags.md`）。「誰でも」＝ その端はどの駒でも満たす");
        Console.WriteLine();
        Console.WriteLine("| 札 | 種類 | 引き金 | 効果 | 文 | 何を何に変えるか |");
        Console.WriteLine("|---|---|---|---|---|---|");
        int missing = 0;
        foreach (RelicInfo r in RelicCatalog.All)
        {
            RelicEnds? e = RelicBridges.Of(r.Id);
            if (e is null) missing++;
            Console.WriteLine($"| {r.Name} | {r.Kind} | {(e is null ? "**（無い）**" : RelicBridges.Label(e.Trigger))} | {(e is null ? "**（無い）**" : RelicBridges.Label(e.Effect))} | {r.Text} | {r.Bridge} |");
        }
        Console.WriteLine();
        Console.WriteLine($"{RelicCatalog.All.Count} 枚（両端の無い札 {missing} 枚）。");
        Console.WriteLine();
        Console.WriteLine("## 端の語ごとの札（引き金 ／ 効果）");
        Console.WriteLine();
        Console.WriteLine("| 語 | 引き金に持つ札 | 効果に持つ札 |");
        Console.WriteLine("|---|---|---|");
        foreach (string w in UnitTags.Axes.Concat(UnitTags.Roles).Prepend(RelicBridges.Anyone))
        {
            var t = RelicCatalog.All.Where(r => RelicBridges.Of(r.Id)?.Trigger == w).Select(r => r.Name).ToList();
            var f = RelicCatalog.All.Where(r => RelicBridges.Of(r.Id)?.Effect == w).Select(r => r.Name).ToList();
            if (t.Count + f.Count == 0) continue;
            Console.WriteLine($"| {RelicBridges.Label(w)} | {(t.Count == 0 ? "—" : string.Join("、", t))} | {(f.Count == 0 ? "—" : string.Join("、", f))} |");
        }
    }
}
