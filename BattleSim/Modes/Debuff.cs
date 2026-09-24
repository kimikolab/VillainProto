using BattleCore;
using static Common;

// =====================================================================================
// debuff モード（第189期） —— デバッファー3枚の転生（ネル・クビ・ハネ）
//
// 指示書は design/PHASE189_DEBUFF_SPEC.md ／ 報告は design/PHASE189_DEBUFF.md。
//
// **線は置かない**（指示書の冒頭）。回帰確認（3枚を含まない行が動かないこと）と、
// 1枚ずつの対照・マイナスだけ 0 の対照・帳簿・診断台だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 debuff phase0   # Q0-1 の数え物（戦闘0回）
// =====================================================================================

static partial class DebuffDiag
{
    const int Seeds = 200;

    static readonly string[] Three = { "nel", "kubi", "hane" };

    /// <summary>Q0-1 で名指しする同席の相手（指示書の並び）。毒の駒は書き手・増幅・吐き戻し・うつし。</summary>
    static readonly UnitDef[] Partners =
    {
        UnitCatalog.Utsu, UnitCatalog.Mudo, UnitCatalog.Yomi, UnitCatalog.Shio, UnitCatalog.Hisa, UnitCatalog.Shiga,
        UnitCatalog.Guza, UnitCatalog.Sid, UnitCatalog.Mio, UnitCatalog.Rau, UnitCatalog.Vio, UnitCatalog.Beni,
    };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            default:
                if (RunMore(mode, arg)) return;
                Console.WriteLine("debuff: モードは phase0 / run / ledger / bench / check。");
                return;
        }
    }

    static partial void RunMoreImpl(string mode, string arg, ref bool handled);

    static bool RunMore(string mode, string arg)
    {
        bool handled = false;
        RunMoreImpl(mode, arg, ref handled);
        return handled;
    }

    static string SlotName(int s) => s switch
    {
        0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => "○" + s,
    };

    // =================================================================================
    // Phase 0 —— Q0-1 の数え物（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第189期 `debuff phase0` —— Q0-1（戦闘0回）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var cross = CrossBuilds();
        Console.WriteLine($"`compare` {rows.Length} 行 ／ 交差帯 {cross.Length} 行。");
        Console.WriteLine();

        foreach (string id in Three)
        {
            UnitDef me = UnitCatalog.ById(id);
            Console.WriteLine($"## {me.Name} を含む行");
            Console.WriteLine();
            Console.WriteLine("| 帯 | 行 | 席 | 隣の駒 | 同席する相手（**隣**は太字） |");
            Console.WriteLine("|---|---|---|---|---|");
            foreach (var (band, list) in new[] { ("compare", rows), ("交差帯", cross) })
            foreach (var (name, f) in list)
            {
                var occ = f.Occupied().ToList();
                var mine = occ.Where(o => o.Def.Id == id).ToList();
                if (mine.Count == 0) continue;
                int slot = mine[0].Slot;
                var adj = occ.Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot))
                             .Select(o => o.Def.Name).ToList();
                var partners = new List<string>();
                foreach (UnitDef p in Partners)
                {
                    var hit = occ.Where(o => o.Def.Id == p.Id).ToList();
                    if (hit.Count == 0) continue;
                    bool isAdj = hit.Any(o => FormationRules.AreAdjacent(slot, o.Slot));
                    partners.Add(isAdj ? "**" + p.Name + "**" : p.Name);
                }
                Console.WriteLine($"| {band} | {name} | {SlotName(slot)} | {string.Join("・", adj)} | "
                                  + (partners.Count == 0 ? "—" : string.Join("・", partners)) + " |");
            }
            Console.WriteLine();
        }

        Console.WriteLine("## 2枚以上が同席する行");
        Console.WriteLine();
        foreach (var (band, list) in new[] { ("compare", rows), ("交差帯", cross) })
        foreach (var (name, f) in list)
        {
            int n = f.Occupied().Count(o => Three.Contains(o.Def.Id));
            if (n >= 2) Console.WriteLine($"- {band}: {name}");
        }
        Console.WriteLine();

        Console.WriteLine("## 3枚のどれも含まない行");
        Console.WriteLine();
        int none = rows.Count(r => !r.F.Occupied().Any(o => Three.Contains(o.Def.Id)));
        int noneX = cross.Count(r => !r.F.Occupied().Any(o => Three.Contains(o.Def.Id)));
        Console.WriteLine($"`compare` {none} / {rows.Length} 行 ／ 交差帯 {noneX} / {cross.Length} 行。");
        Console.WriteLine();
    }
}
