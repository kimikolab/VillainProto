using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第207期） —— 置き去りのナラを「継ぎ当てのツギ」に転生させる（治さず、塞ぐ）
//
// 指示書は design/PHASE207_TSUGI_SPEC.md ／ 報告は design/PHASE207_TSUGI.md。
//
// **線は置かない**（指示書の冒頭）。回帰確認（ツギを含まない行が動かないこと）と、
// 版（T0〜T3）の対照・差し替え診断・火の診断・帳簿だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 tsugi phase0   # Q0-1・Q0-6 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 tsugi run      # 在席行 × 版（T0〜T3）
//     dotnet run --project BattleSim -c Release 0 tsugi swap     # リリの席にツギ（§4.2）＋ 火の診断（§4.3）＋ ベニ（§4.4）
//     dotnet run --project BattleSim -c Release 0 tsugi ledger   # 帳簿（§4.5）
//     dotnet run --project BattleSim -c Release 0 tsugi check [第206期のbalance.md]  # 自己検査
// =====================================================================================

static partial class TsugiDiag
{
    const int Seeds = 200;

    /// <summary>ツギ（転生前はナラ）の Id。<b>Phase 0 は転生の前に回すので両方を見る。</b></summary>
    static bool IsTsugi(UnitDef d) => d.Id is "tsugi" or "nara";

    static bool HasTsugi(Formation f) => f.Occupied().Any(o => IsTsugi(o.Def));

    /// <summary>味方に火を点ける駒（<b>手で持つ表</b>・Q0-1 の見当。実測は `ledger` が数える）。</summary>
    static readonly (string Id, string How)[] FireWriters =
    {
        ("borg", "火の粉＝隣の味方に燃え移る"), ("beni", "手番で隣の味方に火を分ける"), ("zoto", "破裂が味方も焼く"),
        ("lili", "燃えた敵から火を吸って味方へ移す"),
    };

    /// <summary>破片の書き手・読み手（Q0-1）。</summary>
    static readonly (string Id, string Role)[] ArmorRoles =
    {
        ("hibi", "書く（砕け）"), ("uke", "書く（引き受け）"), ("sasa", "書く（身構え）"), ("uro", "書く・読む（鱗）"),
        ("gare", "読む（礫＝砕く）"), ("lili", "書く（溢れ・規定では捨てる）"),
    };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            default:
                if (RunMore(mode, arg)) return;
                Console.WriteLine("tsugi: モードは phase0 / run / swap / ledger / check。");
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

    static string Tag(IEnumerable<(int Slot, UnitDef Def)> occ, IEnumerable<(string Id, string T)> table)
    {
        var l = occ.SelectMany(o => table.Where(w => w.Id == o.Def.Id).Select(w => o.Def.Name + "（" + w.T + "）")).ToList();
        return l.Count == 0 ? "—" : string.Join("・", l);
    }

    // =================================================================================
    // Phase 0 —— 戦闘0回
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第207期 `tsugi phase0` —— Q0-1・Q0-6（戦闘0回）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var cross = CrossBuilds();

        Console.WriteLine("## Q0-1 在席行と、火の書き手・破片の書き手／読み手");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 席 | 隣の駒 | 火の書き手（同じ行） | 破片（同じ行） |");
        Console.WriteLine("|---|---|---|---|---|---|");
        int n = 0;
        foreach (var (band, list) in new[] { ("compare", rows), ("交差帯", cross) })
        foreach (var (name, f) in list)
        {
            var occ = f.Occupied().ToList();
            var me = occ.Where(o => IsTsugi(o.Def)).ToList();
            if (me.Count == 0) continue;
            if (band == "compare") n++;
            int slot = me[0].Slot;
            var adj = occ.Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)).Select(o => o.Def.Name).ToList();
            Console.WriteLine("| " + band + " | " + name + " | " + SlotName(slot) + " | " + string.Join("・", adj) + " | "
                              + Tag(occ, FireWriters) + " | " + Tag(occ, ArmorRoles) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("在席: `compare` **" + n + " / " + rows.Length + "** 行 ／ 含まない行 **" + rows.Count(r => !HasTsugi(r.F)) + "** 行。");
        Console.WriteLine();

        Console.WriteLine("### 差し替え診断の台（リリの在席行・§4.2）");
        Console.WriteLine();
        Console.WriteLine("| 行 | リリの席 | 隣の駒 | 火の書き手 | 破片 | 被弾で動く駒（Q0-6） |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (name, f) in rows)
        {
            var occ = f.Occupied().ToList();
            var li = occ.Where(o => o.Def.Id == "lili").ToList();
            if (li.Count == 0) continue;
            int slot = li[0].Slot;
            var adj = occ.Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)).Select(o => o.Def.Name).ToList();
            var hurt = occ.Where(o => DamageReaders(o.Def).Count > 0).Select(o => o.Def.Name).ToList();
            Console.WriteLine("| " + name + " | " + SlotName(slot) + " | " + string.Join("・", adj) + " | " + Tag(occ, FireWriters) + " | "
                              + Tag(occ, ArmorRoles) + " | " + (hurt.Count == 0 ? "—" : string.Join("・", hurt)) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("### ベニの在席行（§4.4）");
        Console.WriteLine();
        foreach (var (name, f) in rows)
        {
            var occ = f.Occupied().ToList();
            var be = occ.Where(o => o.Def.Id == "beni").ToList();
            if (be.Count == 0) continue;
            int slot = be[0].Slot;
            var adj = occ.Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)).Select(o => o.Def.Name + "（" + SlotName(o.Slot) + "）");
            Console.WriteLine("- " + name + " —— ベニ " + SlotName(slot) + "・隣 " + string.Join("・", adj));
        }
        Console.WriteLine();

        Console.WriteLine("## Q0-6 被弾で動く駒（`OnDamaged` / `OnAllyDamaged` を上書きする札・リフレクションで引く）");
        Console.WriteLine();
        Console.WriteLine("破片が一撃を全部吸うと `ApplyDamage` はそこで返り、この2つのフックは鳴らない（`BattleEngine.cs` の破片の段）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 札（フック） | `compare` の在席行 |");
        Console.WriteLine("|---|---|--:|");
        int readers = 0;
        foreach (UnitDef u in UnitCatalog.All)
        {
            var l = DamageReaders(u);
            if (l.Count == 0) continue;
            readers++;
            Console.WriteLine("| " + u.Name + " | " + string.Join("・", l) + " | " + rows.Count(r => r.F.Occupied().Any(o => o.Def.Id == u.Id)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine(readers == 0 ? "**走査が空。止める**（R034）。" : "該当 **" + readers + "** 枚。");
        Console.WriteLine();
    }

    /// <summary>その駒の札のうち、被弾で鳴るフックを上書きしているもの（R264: 窓口が engine 側の札は拾えない）。</summary>
    static List<string> DamageReaders(UnitDef u)
    {
        var l = new List<string>();
        foreach (TraitId id in u.Traits)
        {
            Trait t = TraitCatalog.Get(id);
            foreach (string hook in new[] { "OnDamaged", "OnAllyDamaged" })
            {
                MethodInfo? m = t.GetType().GetMethod(hook, BindingFlags.Public | BindingFlags.Instance);
                if (m is not null && m.DeclaringType != typeof(Trait)) l.Add(id + "（" + hook + "）");
            }
        }
        return l;
    }
}
