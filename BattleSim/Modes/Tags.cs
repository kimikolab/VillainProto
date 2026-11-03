using BattleCore;
using static Common;

// =====================================================================================
// tags モード（第450期・指示書 design/PHASE450_TAGS_SPEC.md §1-1 ／ §4）—— 駒のタグの確定版を吐く。
// 表は `BattleCore/UnitCatalog.cs` の `UnitTags`（手で書く・`Id` で引く）。**戦闘0回。**
// `units.md` に列を足さず別ファイルにしたのは、`checkup check` が `units.md` の列を位置で読むため。
//
//     dotnet run --project BattleSim -c Release 0 tags > docs/tags.md
// =====================================================================================

static class TagsDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        Console.WriteLine("# 駒のタグ");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 tags > docs/tags.md` の出力。手で編集しない。表は `UnitTags`（`BattleCore/UnitCatalog.cs`・手で書く・`Id` で引く）。**表示だけで、どの規則も読まない。**");
        Console.WriteLine();
        Console.WriteLine($"- 軸（何で働くか・1〜2 個）: {string.Join("・", UnitTags.Axes)}");
        Console.WriteLine($"- 役割（隊の中で何をするか・1 個）: {string.Join("・", UnitTags.Roles)}");
        Console.WriteLine();
        Console.WriteLine("## タグの 1 行（タグに触れると出る説明）");
        Console.WriteLine();
        Console.WriteLine("| タグ | 種類 | 1 行 |");
        Console.WriteLine("|---|---|---|");
        foreach (var (tag, line) in UnitTags.Lines)
            Console.WriteLine($"| {tag} | {(UnitTags.Axes.Contains(tag) ? "軸" : UnitTags.Roles.Contains(tag) ? "役割" : "盤面ルール")} | {line} |");
        Console.WriteLine();
        Console.WriteLine("## 編成に選べる駒");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 軸 | 役割 |");
        Console.WriteLine("|---|---|---|");
        int missing = 0;
        foreach (UnitDef u in UnitCatalog.All)
        {
            if (UnitTags.EntryOf(u.Id) is { } e)
                Console.WriteLine($"| {u.Name} | {string.Join("・", e.Axes)} | {e.Role} |");
            else { missing++; Console.WriteLine($"| {u.Name} | **（無い）** | **（無い）** |"); }
        }
        Console.WriteLine();
        Console.WriteLine($"{UnitCatalog.All.Count} 枚（タグの無い駒 {missing} 枚）。");
        Console.WriteLine();
        Console.WriteLine("### 軸ごとの枚数（2 つ持つ駒は両方に数える）");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 枚数 | 駒 |");
        Console.WriteLine("|---|--:|---|");
        foreach (string a in UnitTags.Axes)
        {
            var who = UnitCatalog.All.Where(u => UnitTags.EntryOf(u.Id)?.Axes.Contains(a) == true).Select(u => u.Name).ToList();
            Console.WriteLine($"| {a} | {who.Count} | {string.Join("、", who)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 役割ごとの枚数");
        Console.WriteLine();
        Console.WriteLine("| 役割 | 枚数 | 駒 |");
        Console.WriteLine("|---|--:|---|");
        foreach (string r in UnitTags.Roles)
        {
            var who = UnitCatalog.All.Where(u => UnitTags.EntryOf(u.Id)?.Role == r).Select(u => u.Name).ToList();
            Console.WriteLine($"| {r} | {who.Count} | {string.Join("、", who)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 作戦マップの敵部隊（`EnemyCatalog.MapSquads`）");
        Console.WriteLine();
        Console.WriteLine("| 部隊 | 前1 | 前3 | 中央 | 後1 | 後3 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (EnemyCatalog.MapSquad m in EnemyCatalog.MapSquads)
        {
            var cells = new[] { 0, 1, 2, 3, 4 }.Select(sl => m.Enemy.Occupied().FirstOrDefault(o => o.Slot == sl).Def is { } d
                ? $"{d.Name}（{string.Join("・", d.Tags)}）" : "—");
            Console.WriteLine($"| {m.Name} | {string.Join(" | ", cells)} |");
        }
    }
}
