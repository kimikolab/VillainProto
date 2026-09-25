using BattleCore;
using static Common;

// =====================================================================================
// form2 の第202期 —— パターン2の貫きの偏りを直す（向かいの敵から撃たれる）／スィドを S+反 に
//
// 指示書は design/PHASE202_PIERCE_SID_SPEC.md ／ 報告は design/PHASE202_PIERCE_SID.md。
// **線は置かない**（採否はポンが遊んで決める）。
//
//     dotnet run --project BattleSim -c Release 0 form2 phase202   # Q0-1〜Q0-4 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 form2 run202     # 表A・表B の P2 列を選び直す・上下の入れ替え・帳簿
//     dotnet run --project BattleSim -c Release 0 form2 check202 [採用前のbalance.md]  # 自己検査
// =====================================================================================

static partial class Formation2Diag
{
    static void Phase202()
    {
        Console.WriteLine("# 第202期 `form2 phase202` —— Q0-1〜Q0-4（戦闘0回）");
        Console.WriteLine();
        string root = FindRoot();
        string eng = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 貫きの2レーンを選ぶ入口");
        Console.WriteLine();
        bool branch = eng.Contains("if (shape.DeterministicPierce)");
        bool call = eng.Contains("return SelectPierceEntry(foes, out lane);");
        Console.WriteLine("- 入口は `BattleContext.SelectPierceEntry(foes, out lane)` の `DeterministicPierce` の枝（" + (branch ? "○ 在る" : "**見つからない**") + "）。"
                          + "呼び口は `SelectTargetChain` の1箇所（" + (call ? "○" : "**?**") + "）で、そこでは `attacker` が手元にある——**引数を1本足せば撃つ敵の席が取れる**");
        Console.WriteLine("- 撃つ敵の席のレーンは `FormationShape.GridLane(attacker.Slot)`（9マスの格子のレーン・陣形に依らない）。敵は X 字なので:");
        Console.WriteLine();
        Console.WriteLine("| 敵の席 | 格子のレーン | 新しい規則で抜ける2レーン |");
        Console.WriteLine("|---|--:|---|");
        foreach (int s in Enumerable.Range(0, FormationRules.TotalSlots))
        {
            int gl = FormationShape.GridLane(s);
            Console.WriteLine("| " + FormationRules.SeatNames[s] + " | " + gl + " | " + (gl == 1 ? "1-2" : gl == 3 ? "2-3" : "多い方（同数なら交互・最初は 1-2）") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- パターン2の経路は `BuildGrid` の `lanes = { Path(1, 2), Path(2, 3) }`——添字 0 ＝ 1-2、添字 1 ＝ 2-3");
        Console.WriteLine("- X 字は `DeterministicPierce` が偽なのでこの枝を通らない（`Roll` の引き方は1ビットも変わらない）");
        Console.WriteLine();

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 第2〜5波の貫き持ち（席のレーン別）");
        Console.WriteLine();
        Console.WriteLine("`Pattern == Pierce` の駒と、条件つきで貫きに化ける札（狙撃・熾火・段・突き）の保持者。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 1レーン側（前1・後1） | 2レーン（中央） | 3レーン側（前3・後3） | 上下の偏り |");
        Console.WriteLine("|---|---|---|---|---|");
        TraitId[] becomes = { TraitId.Sniper, TraitId.Pyre, TraitId.GradeStep, TraitId.Thrust };
        for (int st = 1; st < 5; st++)
        {
            Formation e = EnemyCatalog.Stages[st].Enemy;
            var cells = new[] { new List<string>(), new List<string>(), new List<string>() };
            foreach ((int slot, UnitDef d) in e.Occupied())
            {
                string? tag = d.Pattern == AttackPattern.Pierce ? "" : d.Traits.Any(t => becomes.Contains(t)) ? "（条件つき: " + string.Join("・", d.Traits.Where(t => becomes.Contains(t))) + "）" : null;
                if (tag is null) continue;
                cells[FormationShape.GridLane(slot) - 1].Add(FormationRules.SeatNames[slot] + " " + d.Name + tag);
            }
            string C(List<string> l) => l.Count == 0 ? "—" : string.Join("、", l);
            string bias = cells[0].Count == cells[2].Count ? (cells[1].Count > 0 ? "上下は対称（中央は交互）" : cells[0].Count == 0 ? "貫き持ちなし" : "対称")
                        : cells[0].Count > cells[2].Count ? "**1-2 側に偏る**" : "**2-3 側に偏る**";
            Console.WriteLine("| " + EnemyCatalog.Stages[st].Name + " | " + C(cells[0]) + " | " + C(cells[1]) + " | " + C(cells[2]) + " | " + bias + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 敵が倒れて（あるいは突き返し・喧噪で）席を移ると撃つレーンも変わる。上の表は開幕の席");
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 スィドの在席行（`compare`）");
        Console.WriteLine();
        var rows = CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "sid")).ToList();
        Console.WriteLine("| 行 | スィドの席 | 隣の味方 |");
        Console.WriteLine("|---|---|---|");
        foreach (var r in rows)
        {
            int ss = r.F.Occupied().First(o => o.Def.Id == "sid").Slot;
            var adj = r.F.Occupied().Where(o => FormationRules.AreAdjacent(ss, o.Slot)).Select(o => o.Def.Name);
            Console.WriteLine("| " + r.Name + " | " + FormationRules.SeatNames[ss] + " | " + string.Join("、", adj) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 在席 **" + rows.Count + " / " + CompareBuilds().Length + " 行**。交差帯: "
                          + CrossBuilds().Count(r => r.F.Occupied().Any(o => o.Def.Id == "sid")) + " 行");
        Console.WriteLine("- S+反 は殴られたときの毒だけ +4 → +8（`VenomHeavy`）。**スィドが殴られない戦は1ビットも動かない**。"
                          + "第196期の表（主表の7行・ガルドの席）では S 75.7 → S+反 88.2（+12.5）。`compare` の在席行は既に高い（第196期 99.6〜100 ／ 97.0）ので、動くのは主に `毒+ベニ+ラウ`");
        Console.WriteLine("- 切り替えは札の差し替え（`UnitCatalog.Sid.Traits` の `Venom` → `VenomHeavy`）。`Venom` を名指ししている周り（`Map11Relations` の関係の表・`Seats2` / `Wound2` の分類）に `VenomHeavy` を足す");
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 戦闘再生の席札の出どころ");
        Console.WriteLine();
        string demo = Path.Combine(root, "DemoApp");
        int C2(string file, string tok) => Count(File.ReadAllText(Path.Combine(demo, file)), tok);
        Console.WriteLine("| 所 | 今 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 駒の頭上の札（`BattlePawn3D`） | `UiKit.SeatLabel(Slot)`（" + C2("BattlePawn3D.cs", "UiKit.SeatLabel(") + " 箇所）→ `FormationRules.SeatNames[slot]`（X 字の名前・パターン2は「○前2」など） |");
        Console.WriteLine("| 味方の列（`PartyBar`） | `UiKit.SeatLabel(o.Slot)` ／ `(pawn.Slot)`（" + C2("PartyBar.cs", "UiKit.SeatLabel(") + " 箇所） |");
        Console.WriteLine("| 駒の開幕の情報（`DemoOpening`） | 陣形を運んでいない（`Main.EnterBattle` と召喚の2箇所で作る） |");
        Console.WriteLine();
        Console.WriteLine("- 入口は1本足す: `DemoOpening` に陣形（既定 null ＝ X 字）、`FormationShape.SeatName(slot)`（編成の席なら `FrameNames`、召喚枠は `SeatNames`）、"
                          + "`UiKit.SeatLabel(slot, shape)`。**盤面には触らない**");
        Console.WriteLine();
    }
}

static partial class Formation2Diag
{
    static partial void Run202Impl(string arg);
    static partial void Check202Impl(string arg);
    static void Run202(string arg) => Run202Impl(arg);
    static void Check202(string arg) => Check202Impl(arg);
}
