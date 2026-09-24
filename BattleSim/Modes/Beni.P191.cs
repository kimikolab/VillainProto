using BattleCore;
using static Common;

// =====================================================================================
// beni phase191（第191期） —— ベニの手番を「火→毒→毒」の周期にする前の数え物
//
// 指示書は design/PHASE191_BENI_CYCLE_SPEC.md ／ 報告は design/PHASE191_BENI_CYCLE.md。
//
//     dotnet run --project BattleSim -c Release 0 beni phase191   # Q0-1〜Q0-5（戦闘は Q0-3 の席の選定のぶんだけ）
// =====================================================================================

static partial class BeniDiag
{
    static void Phase191()
    {
        Console.WriteLine("# 第191期 `beni phase191` —— Q0-1〜Q0-5");
        Console.WriteLine();

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 周期3と燃焼の持続（机上・1ターンずつ）");
        Console.WriteLine();
        Console.WriteLine("ターンの順は `TickStatuses`（刻み・燃焼の残りを1減らす）→ `OnTurnStart` → 行動順。"
                          + "ベニの手番は毎ターン1回・周期は 火 → 毒 → 毒。着火は残り `BurnRules.Turns` = " + BurnRules.Turns
                          + "（点け直しは残りを戻すだけ）。**滲み則の燃焼側（`SoakRule.Burn`）の既定は "
                          + (SoakRule.Default.Burn ? "true（傷を持つ相手は残り +1）" : "false（傷を持つ相手でも残り +0）") + "**。");
        Console.WriteLine();
        Console.WriteLine("| ターン | 刻みの前の残り | 刻み（燃焼） | 刻みの後の残り | ベニの手番 | 手番の後の残り | 毒の層（手番の後） |");
        Console.WriteLine("|--:|--:|:-:|--:|---|--:|--:|");
        int left = 0, layers = 0, ticks = 0, gaps = 0;
        for (int t = 1; t <= 12; t++)
        {
            int before = left;
            bool tick = left > 0;
            if (tick) { left--; ticks++; }
            else if (t > 1) gaps++;
            string act = ((t - 1) % 3) switch { 0 => "火を分ける", _ => "澱みを分ける" };
            if ((t - 1) % 3 == 0) left = BurnRules.Turns; else layers++;
            Console.WriteLine("| " + t + " | " + before + " | " + (tick ? "○" : "—") + " | " + (before > 0 ? left.ToString() : "—")
                              + " | " + act + " | " + left + " | " + layers + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 2〜12 ターン目で燃焼の刻みが無いターン: **" + gaps + "**（0 なら途切れない）／ 刻み " + ticks + " 回");
        Console.WriteLine("- 1 ターン目は刻みが無い（ベニの手番は刻みの後）。**手番を1回失うと周期が止まり**、止まった手番が「火」の直前なら次の火は1ターン遅れる");
        Console.WriteLine();

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 燃焼を読む札（`Traits.cs` の走査）");
        Console.WriteLine();
        if (!ParryScan.Init()) return;
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
        Console.WriteLine("| ファイル | 行 | クラス | 行の中身 | 保持者（All） |");
        Console.WriteLine("|---|--:|---|---|---|");
        foreach (var (file, src) in new[] { ("Traits.cs", traits), ("BattleEngine.cs", engine) })
        {
            string[] lines = src.Split('\n');
            string cls = "";
            string needle = "StatusKeys." + "Burn";
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                int ci = l.IndexOf("class ");
                if (ci >= 0 && (l.Contains("sealed class") || l.Contains("abstract class") || l.Contains("partial class")))
                    cls = l.Substring(ci + 6).Split(' ', ':', '\r')[0];
                if (!l.Contains(needle)) continue;
                string tl = l.Trim();
                if (tl.StartsWith("//") || tl.StartsWith("///")) continue;
                if (!(tl.Contains("> 0") || tl.Contains("<= 0") || tl.Contains(">= "))) continue;   // 読む行（条件）だけ
                Console.WriteLine("| " + file + " | " + (i + 1) + " | " + cls + " | `" + tl.Replace("|", "\\|").TrimEnd('\r') + "` | "
                                  + (file == "Traits.cs" ? HoldersOf(cls) : "—") + " |");
            }
        }
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 ベニの隣にホタ・ヒヨが立つ行");
        Console.WriteLine();
        Console.WriteLine("在席行（`compare` ＋ 交差帯）と、第190期 §5.2 の差し替え表 25 行（**同じ規則で席を選び直す**＝素体差し替えの戦闘だけ回す）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | ベニの席 | 隣の駒 | 隣のホタ | 隣のヒヨ |");
        Console.WriteLine("|---|---|---|---|:-:|:-:|");
        int hota = 0, hiyo = 0, n = 0;
        IEnumerable<(string Band, string Name, Formation F)> Rows()
        {
            foreach (var r in BeniRows()) yield return r;
            foreach (var (name, g) in SwapRows()) yield return ("差し替え", name, g);
        }
        foreach (var (band, name, f) in Rows())
        {
            n++;
            var occ = f.Occupied().ToList();
            int slot = occ.First(o => o.Def.Id == "beni").Slot;
            var adj = occ.Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)).Select(o => o.Def).ToList();
            bool ho = adj.Any(d => d.Id == "hota"), hi = adj.Any(d => d.Id == "hiyo");
            if (ho) hota++;
            if (hi) hiyo++;
            Console.WriteLine("| " + band + " | " + name + " | " + SlotName(slot) + " | " + string.Join("・", adj.Select(d => d.Name))
                              + " | " + (ho ? "○" : "") + " | " + (hi ? "○" : "") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- " + n + " 行のうち ホタが隣 **" + hota + "** 行 ／ ヒヨが隣 **" + hiyo + "** 行");
        Console.WriteLine();
    }
}
