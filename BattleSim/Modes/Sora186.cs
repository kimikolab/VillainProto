using BattleCore;
using static Common;

// =====================================================================================
// sora186 モード（第186期） —— 逸らしのソラ（半分を逸らす）
//
// 指示書は design/PHASE186_SORA_SPEC.md ／ 報告は design/PHASE186_SORA.md。
//
// **数値の線は1本も置かない**（第178期から引き継ぐ約束）。
// 回帰確認（ソラを含まない行が動かないこと）と対照の表・帳簿だけで、採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 sora186 phase0  # Q0-1 の在席（戦闘0回）
// =====================================================================================

static partial class Sora186Diag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": StageRun(); return;
            case "ledger": Ledger(); return;
            case "bench": Bench(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("sora186: モードは phase0 / run / ledger / bench / check。");
                return;
        }
    }

    static IEnumerable<(string Band, string Name, Formation F)> Bands()
    {
        foreach ((string n, Formation f) in CompareBuilds()) yield return ("compare", n, f);
        foreach ((string n, Formation f) in CrossBuilds()) yield return ("交差帯", n, f);
    }

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    static string Short(UnitDef d)
    {
        int i = d.Name.LastIndexOf('の');
        return i >= 0 && i + 1 < d.Name.Length ? d.Name[(i + 1)..] : d.Name;
    }

    static string Neigh(Formation f, string id)
    {
        var me = f.Occupied().FirstOrDefault(o => o.Def.Id == id);
        if (me.Def is null) return "";
        return string.Join("・", f.Occupied().Where(o => FormationRules.AreAdjacent(me.Slot, o.Slot)).Select(o => Short(o.Def)));
    }

    static readonly string[] SlotName = { "前1", "前3", "中央", "後1", "後3" };

    static string SeatOf(Formation f, string id)
    {
        var me = f.Occupied().FirstOrDefault(o => o.Def.Id == id);
        return me.Def is null ? "" : me.Slot < SlotName.Length ? SlotName[me.Slot] : "○" + me.Slot;
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第186期 `sora186 phase0` —— Q0-1 在席（`compare` 61 行 ＋ 交差帯 12 行・戦闘0回）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | ソラの席 | ソラの隣 | ヒサ | ザン | トメ | カリ | ヒサの隣 | ヒサがソラを指しうるか |");
        Console.WriteLine("|---|---|---|---|:-:|:-:|:-:|:-:|---|---|");
        int nSora = 0, nNone = 0, nHisa = 0;
        foreach ((string band, string name, Formation f) in Bands())
        {
            if (!Has(f, "sora")) { if (band == "compare") nNone++; continue; }
            if (band == "compare") nSora++;
            string M(string id) => Has(f, id) ? "●" : "";
            string hisaPick = "";
            if (Has(f, "hisa"))
            {
                nHisa++;
                var h = f.Occupied().First(o => o.Def.Id == "hisa");
                var adj = f.Occupied().Where(o => o.Def.Id != "hisa" && FormationRules.AreAdjacent(h.Slot, o.Slot))
                    .OrderByDescending(o => o.Def.MaxHp).ThenBy(o => o.Slot).ToList();
                hisaPick = adj.Count == 0 ? "隣なし" : "開戦時は " + Short(adj[0].Def) + "（" + adj[0].Def.MaxHp + "）"
                           + (adj.Any(o => o.Def.Id == "sora") ? "・ソラは隣" : "・**ソラは隣にいない**");
            }
            Console.WriteLine("| " + name + " | " + band + " | " + SeatOf(f, "sora") + " | " + Neigh(f, "sora") + " | "
                              + M("hisa") + " | " + M("zan") + " | " + M("tome") + " | " + M("kari") + " | "
                              + Neigh(f, "hisa") + " | " + hisaPick + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- `compare` 61 行: ソラを含む **" + nSora + " 行** ／ 含まない **" + nNone + " 行**（回帰の分母）");
        Console.WriteLine("- ヒサとソラが同席する行（両帯）: **" + nHisa + " 行**");
        Console.WriteLine();

        Console.WriteLine("## 敵の単体攻撃（波別・`Stages` の駒の攻撃型）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 単体 | 薙ぎ | 貫き | 全体 | 単体の攻撃力 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        for (int st = 0; st < EnemyCatalog.Stages.Count(); st++)
        {
            var ds = EnemyCatalog.Stages[st].Enemy.Occupied().Select(o => o.Def).ToList();
            int C(AttackPattern p) => ds.Count(d => d.Pattern == p);
            Console.WriteLine("| 第" + (st + 1) + "波 | " + C(AttackPattern.Single) + " | " + C(AttackPattern.Sweep) + " | "
                              + C(AttackPattern.Pierce) + " | " + C(AttackPattern.All) + " | "
                              + string.Join("・", ds.Where(d => d.Pattern == AttackPattern.Single).Select(d => d.Attack)) + " |");
        }
        Console.WriteLine();
    }
}
