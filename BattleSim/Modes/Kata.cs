using BattleCore;
using static Common;

// =====================================================================================
// kata モード（第188期） —— ナタの枠に新駒「触媒のカタ」（起爆）
//
// 指示書は design/PHASE188_KATA_SPEC.md ／ 報告は design/PHASE188_KATA.md。
//
// **線は置かない**（指示書 §0）。回帰確認（カタを含まない行が動かないこと）と、
// 診断台3つのカタあり／なし・帳簿だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 kata phase0  # Q0-1 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 kata run     # 診断台3つ × カタあり／素体 ＋ 帳簿 ＋ 残存・全滅勝ち
//     dotnet run --project BattleSim -c Release 0 kata check [balance.md]  # 自己検査
//
// **`Presets` は1行も触らない**（ナタの1行はそのまま残る＝盤面は1ビットも動かない）ので、
// カタを載せた台は**この診断のローカル**で組む（第179期のススと同じ扱い）。
// =====================================================================================

static class KataDiag
{
    const int Seeds = 200;

    /// <summary>Q0-1 で名指しする軸の駒（指示書の並び）。</summary>
    static readonly UnitDef[] AxisUnits =
    {
        UnitCatalog.Guza, UnitCatalog.Sid, UnitCatalog.Mio, UnitCatalog.Rau, UnitCatalog.Vio,
        UnitCatalog.Beni, UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo, UnitCatalog.Zoto,
        UnitCatalog.Rica, UnitCatalog.Susu,
    };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            default:
                Console.WriteLine("kata: モードは phase0。");
                return;
        }
    }

    // =================================================================================
    // Phase 0 —— Q0-1 の数え物（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第188期 `kata phase0` —— Q0-1（戦闘0回）");
        Console.WriteLine();
        var rows = CompareBuilds();
        Console.WriteLine("`compare` " + rows.Length + " 行。");
        Console.WriteLine();

        Console.WriteLine("## ナタを含む行");
        Console.WriteLine();
        foreach (var (name, f) in rows)
            if (f.Occupied().Any(o => o.Def.Id == "nata")) Console.WriteLine("- " + name);
        Console.WriteLine();

        Console.WriteLine("## 軸の駒を含む行（駒ごと）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 行数 | 行 |");
        Console.WriteLine("|---|--:|---|");
        foreach (UnitDef d in AxisUnits)
        {
            var hit = rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == d.Id)).Select(r => r.Name).ToList();
            Console.WriteLine("| " + d.Name + " | " + hit.Count + " | " + string.Join(" ／ ", hit) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 毒の書き手と燃焼の書き手が同席する行");
        Console.WriteLine();
        string[] poison = { "guza", "sid", "mio", "rau", "vio" };   // 毒を書く（ミオは増幅・ヴィオは吐き戻し）
        string[] burn = { "borg", "zoto" };                          // 燃焼を書く（ボルグの火の粉・ゾトの破裂）
        foreach (var (name, f) in rows)
        {
            var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
            if (ids.Overlaps(poison) && ids.Overlaps(burn)) Console.WriteLine("- " + name);
        }
        Console.WriteLine();
    }
}
