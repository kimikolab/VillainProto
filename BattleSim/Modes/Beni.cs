using BattleCore;
using static Common;

// =====================================================================================
// beni モード（第190期） —— 毒喰らいのベニの転生（反転の結界）
//
// 指示書は design/PHASE190_BENI_SPEC.md ／ 報告は design/PHASE190_BENI.md。
//
// **線は置かない**（指示書の冒頭）。回帰確認（ベニを含まない行が動かないこと）と、
// 版の対照・差し替え表・診断台・帳簿だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 beni phase0   # Q0-1〜Q0-7 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 beni run      # 版の対照 × ベニの在席行・差し替え表（主表）
//     dotnet run --project BattleSim -c Release 0 beni ledger   # 帳簿（1戦あたり・第2〜5波）
//     dotnet run --project BattleSim -c Release 0 beni bench    # 診断台3つ × 版
//     dotnet run --project BattleSim -c Release 0 beni check [採用前のbalance.md]  # 自己検査
// =====================================================================================

static partial class BeniDiag
{
    const int Seeds = 200;

    /// <summary>毒か燃焼の書き手（指示書 §5.2 の並び）。</summary>
    static readonly string[] Writers = { "guza", "sid", "mio", "rau", "borg", "zoto", "kata" };

    /// <summary>回復役（指示書 §2.1 が名指しした4枚）。</summary>
    static readonly string[] Healers = { "lili", "vel", "shio", "tsugi" };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "phase191": Phase191(); return;
            case "phase192": Phase192(); return;
            case "phase193": Phase193(); return;
            case "phase197": Phase197(); return;
            default:
                if (RunMore(mode, arg)) return;
                Console.WriteLine("beni: モードは phase0 / run / ledger / bench / check。");
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
    // Phase 0 —— 戦闘0回
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第190期 `beni phase0` —— Q0-1〜Q0-7（戦闘0回）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var cross = CrossBuilds();

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 ベニの在席行と隣の駒");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | ベニの席 | 隣の駒 | 隣の書き手 | 隣の回復役 | 隣のヴィオ | 隣の支援拒否 | 隣の自己回復 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|---|");
        foreach (var (band, list) in new[] { ("compare", rows), ("交差帯", cross) })
        foreach (var (name, f) in list)
        {
            var occ = f.Occupied().ToList();
            var me = occ.Where(o => o.Def.Id == "beni").ToList();
            if (me.Count == 0) continue;
            int slot = me[0].Slot;
            var adj = occ.Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)).Select(o => o.Def).ToList();
            string selfHeal = string.Join("・", adj.Where(d => SelfHealers.Contains(d.Id)).Select(d => d.Name));
            Console.WriteLine("| " + band + " | " + name + " | " + SlotName(slot) + " | "
                              + string.Join("・", adj.Select(d => d.Name)) + " | "
                              + adj.Count(d => Writers.Contains(d.Id)) + " | " + adj.Count(d => Healers.Contains(d.Id)) + " | "
                              + adj.Count(d => d.Id == "vio") + " | " + adj.Count(d => d.Traits.Contains(TraitId.Stoic)) + " | "
                              + (selfHeal.Length == 0 ? "—" : selfHeal) + " |");
        }
        Console.WriteLine();
        int none = rows.Count(r => !r.F.Occupied().Any(o => o.Def.Id == "beni"));
        int noneX = cross.Count(r => !r.F.Occupied().Any(o => o.Def.Id == "beni"));
        Console.WriteLine("ベニを含まない行: `compare` **" + none + " / " + rows.Length + "** ／ 交差帯 **" + noneX + " / " + cross.Length + "**。");
        Console.WriteLine();

        // §5.2 の差し替え表の分母（毒か燃焼の書き手を含み、ベニを含まない行）
        int swapRows = rows.Count(r => r.F.Occupied().Any(o => Writers.Contains(o.Def.Id))
                                     && !r.F.Occupied().Any(o => o.Def.Id == "beni"));
        Console.WriteLine("§5.2 の差し替え表の分母（書き手を含みベニを含まない `compare` 行）: **" + swapRows + "** 行。");
        Console.WriteLine();

        if (!ParryScan.Init()) return;
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 `Heal` の呼び出し口");
        Console.WriteLine();
        Console.WriteLine("`Traits.cs` の `ctx.Heal(` と `BattleEngine.cs` の地の `Heal(`。宛先の式と、属するクラス・保持者（`UnitCatalog.All`）。");
        Console.WriteLine();
        Console.WriteLine("| ファイル | 行 | クラス | 宛先 | 保持者（All） |");
        Console.WriteLine("|---|--:|---|---|---|");
        foreach (var (file, src, needle) in new[] { ("Traits.cs", traits, "ctx." + "Heal("), ("BattleEngine.cs", engine, " Heal" + "(u,") })
        {
            string[] lines = src.Split('\n');
            string cls = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                int ci = l.IndexOf("class ");
                if (ci >= 0 && (l.Contains("sealed class") || l.Contains("abstract class") || l.Contains("partial class")))
                    cls = l.Substring(ci + 6).Split(' ', ':', '\r')[0];
                int hi = l.IndexOf(needle);
                if (hi < 0 || l.TrimStart().StartsWith("//") || l.TrimStart().StartsWith("///")) continue;
                string arg0 = l.Substring(hi + needle.Length).Split(',')[0].Trim();
                if (file == "BattleEngine.cs") arg0 = "u";
                string holders = HoldersOf(cls);
                Console.WriteLine("| " + file + " | " + (i + 1) + " | " + cls + " | `" + arg0 + "` | " + holders + " |");
            }
        }
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 `OnDamaged` を上書きする札と、味方を出どころとする一撃への門");
        Console.WriteLine();
        Console.WriteLine("本文の先頭 16 行に `source.TeamId == self.TeamId` の早期 return があれば「味方では止まる」。");
        Console.WriteLine();
        Console.WriteLine("| クラス | 味方の一撃で止まる | 本文に出る語 | 保持者（All） |");
        Console.WriteLine("|---|---|---|---|");
        {
            string[] lines = traits.Split('\n');
            string cls = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                int ci = l.IndexOf("class ");
                if (ci >= 0 && (l.Contains("sealed class") || l.Contains("abstract class"))) cls = l.Substring(ci + 6).Split(' ', ':', '\r')[0];
                if (!l.Contains("override void " + "OnDamaged(")) continue;
                string body = string.Join("\n", lines.Skip(i).Take(16));
                bool stops = body.Contains("source.TeamId == self.TeamId") || body.Contains("source.TeamId != ctx.Opponent");
                var words = new[] { "PerformAttack", "Poison(", "Curse", "Ignite(", "Whet(", "Dull(", "SwapSlots", "Armor" }
                    .Where(w => body.Contains(w)).ToList();
                Console.WriteLine("| " + cls + " | " + (stops ? "止まる" : "**止まらない**") + " | "
                                  + (words.Count == 0 ? "—" : string.Join("・", words)) + " | " + HoldersOf(cls) + " |");
            }
        }
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 行動順（1ターンの中）");
        Console.WriteLine();
        Console.WriteLine("ターンは `TickStatuses`（毒 → 燃焼）→ `OnTurnStart`（席順）→ 行動順ループ（速さ降順・同速は乱数）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 速さ | 働く場所 |");
        Console.WriteLine("|---|--:|---|");
        foreach (var (d, where) in new[]
        {
            (UnitCatalog.Beni, "手番（`Actions = [Skill]`）"), (UnitCatalog.Kata, "手番（起爆）"),
            (UnitCatalog.Guza, "手番（瘴気・味方漏れ）"), (UnitCatalog.Vio, "ターン頭（吸い上げ）"),
            (UnitCatalog.Borg, "攻撃の後（火の粉）"), (UnitCatalog.Lili, "手番（繕い）"), (UnitCatalog.Vel, "手番（縫い合わせ）"),
            (UnitCatalog.Golm, "手番（吸い＝自己回復）"),
        })
            Console.WriteLine("| " + d.Name + " | " + d.Speed + " | " + where + " |");
        Console.WriteLine();

        // ---------------- Q0-6 ----------------
        Console.WriteLine("## Q0-6 名前の衝突");
        Console.WriteLine();
        foreach (string nm in new[] { "Inverse", "Taint", "InverseLeak" })
        {
            bool enumHit = Enum.GetNames<TraitId>().Contains(nm);
            Console.WriteLine("- `TraitId." + nm + "`: " + (enumHit ? "既にある" : "無い"));
        }
        Console.WriteLine("- 既存の `TraitId.Inversion`（逆位・行動順の反転）: " + (Enum.GetNames<TraitId>().Contains("Inversion") ? "ある（別の札）" : "無い"));
        Console.WriteLine();

        // ---------------- Q0-7 ----------------
        Console.WriteLine("## Q0-7 DemoApp の検証用マップ");
        Console.WriteLine();
        string map = ParryScan.Read(Path.Combine("DemoApp", "Map11.cs"));
        foreach (string rowName in new[] { "反撃 (ヒサ×カド)", "突き返し (ハネ×ウツ)", "死軸×ヒヨ (ゾト×火選り)" })
        {
            var f = rows.FirstOrDefault(r => r.Name == rowName).F;
            bool has = f is not null && f.Occupied().Any(o => o.Def.Id == "beni");
            Console.WriteLine("- 隊の元の行 `" + rowName + "` にベニ: " + (has ? "**いる**" : "いない"));
        }
        Console.WriteLine("- `Map11.cs` の名指しの控えにベニ: " + (map.Contains("UnitCatalog." + "Beni") ? "**いる**" : "いない")
                          + "（規則で選ぶ控えの4枚は `--map11-phase172` で確かめる）");
        Console.WriteLine();
    }

    /// <summary>自己回復を持つ駒（ベニの隣に置くと自分の回復が反転しうる）。</summary>
    static readonly string[] SelfHealers = { "golm", "rica", "mudo" };

    static string HoldersOf(string cls)
    {
        if (cls.Length == 0) return "—";
        var hit = UnitCatalog.All.Where(d => d.Traits.Any(t =>
        {
            try { return TraitCatalog.Get(t).GetType().Name == cls; } catch (KeyNotFoundException) { return false; }
        })).Select(d => d.Name).ToList();
        return hit.Count == 0 ? "0 枚" : string.Join("・", hit);
    }
}
