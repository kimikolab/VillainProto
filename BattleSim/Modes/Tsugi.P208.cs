using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第208期） —— ツギの板を「撃ち返す盾」にする（反射・燃えやすさの差し替え・分かちの取り分）
//
// 指示書は design/PHASE208_TSUGI2_SPEC.md ／ 報告は design/PHASE208_TSUGI2.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 tsugi phase208   # Q0-1・Q0-4 の数え物（戦闘0回）
// =====================================================================================

static partial class TsugiDiag
{
    static void Phase208()
    {
        Console.WriteLine("# 第208期 `tsugi phase208` —— Q0-1・Q0-4（戦闘0回）");
        Console.WriteLine();
        Console.WriteLine("## Q0-1 敵の範囲攻撃（第2〜5波・`EnemyCatalog.Stages`・盤面の倍率は掛けていない素の値）");
        Console.WriteLine();
        Console.WriteLine("**範囲** ＝ 素の攻撃型が単体でない、または手番の周期に単体でない攻撃型の上書きがある。**溜め** ＝ 周期に `Charge` がある（次の手番に大技）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 席 | 駒 | 攻 | 攻撃型 | 周期（攻撃型の上書き・倍率） | 範囲 |");
        Console.WriteLine("|--:|---|---|--:|---|---|:-:|");
        var areaByWave = new int[5];
        for (int st = 1; st < 5; st++)
        {
            foreach (var (slot, d) in EnemyCatalog.Stages[st].Enemy.Occupied())
            {
                var acts = d.Actions ?? Array.Empty<UnitAction>();
                bool area = d.Pattern != AttackPattern.Single
                            || acts.Any(a => a.PatternOverride is AttackPattern p && p != AttackPattern.Single);
                if (area) areaByWave[st]++;
                string cyc = acts.Count == 0 ? "—" : string.Join(" → ", acts.Select(a =>
                    a.Kind + (a.PatternOverride is AttackPattern p ? "・" + p : "") + (a.AttackPercent != 100 ? "・" + a.AttackPercent + "%" : "")));
                Console.WriteLine("| " + (st + 1) + " | " + SlotName(slot) + " | " + d.Name + " | " + d.Attack + " | " + d.Pattern + " | " + cyc + " | " + (area ? "○" : "") + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("範囲の敵の数: " + string.Join(" ／ ", Enumerable.Range(1, 4).Select(st => "第" + (st + 1) + "波 " + areaByWave[st])) + "。");
        Console.WriteLine();

        Console.WriteLine("## Q0-4 分かち（ドハ）と破片");
        Console.WriteLine();
        Console.WriteLine("ドハと、ドハ以外の破片の書き手（ヒビ・ウケ・ササ・ウロ・ツギ）が同じ行にいる `compare` 行（**U3 で動きうる行**）:");
        Console.WriteLine();
        string[] writers = { "hibi", "uke", "sasa", "uro", "tsugi" };
        Console.WriteLine("| 行 | 書き手 | ツギ |");
        Console.WriteLine("|---|---|:-:|");
        int n = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            var occ = f.Occupied().ToList();
            if (!occ.Any(o => o.Def.Id == "doha")) continue;
            var w = occ.Where(o => writers.Contains(o.Def.Id)).Select(o => o.Def.Name).ToList();
            n++;
            Console.WriteLine("| " + name + " | " + (w.Count == 0 ? "—" : string.Join("・", w)) + " | " + (occ.Any(o => o.Def.Id == "tsugi") ? "○" : "") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("ドハの在席: `compare` **" + n + "** 行。");
        Console.WriteLine();
    }
}
