using BattleCore;
using static Common;

// =====================================================================================
// form2 モード（第200期） —— 味方の陣形「パターン2（ひし形・前衛1枚）」
//
// 指示書は design/PHASE200_FORMATION2_SPEC.md ／ 報告は design/PHASE200_FORMATION2.md。
//
// **線は置かない**（採否はポンが遊んで決める）。回帰確認（X 字の隊は1ビットも動かない）と、
// 毒パの主表・タンクの土俵・帳簿だけ。
//
//     dotnet run --project BattleSim -c Release 0 form2 phase0   # Q0-1〜Q0-6 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 form2 run      # 表A（毒パ）・表B（タンクの土俵）・帳簿
//     dotnet run --project BattleSim -c Release 0 form2 check [採用前のbalance.md]  # 自己検査
// =====================================================================================

static partial class Formation2Diag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            default:
                bool handled = false;
                RunMoreImpl(mode, arg, ref handled);
                if (!handled) Console.WriteLine("form2: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunMoreImpl(string mode, string arg, ref bool handled);

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    /// <summary>
    /// 表B の6行（**測る前に固定**・Phase 0 で選んだ。理由は報告の Q0-7）。
    /// どれもゴルム・ササ・スィドを含まない（D に置くタンクが行に2枚立たないように）。
    /// </summary>
    internal static readonly string[] TankRows =
    {
        "反撃 (ヒサ×カド)",           // 棘・標（不動のカドとヒサ）
        "燃焼 (ボルグ×ホタ)",         // 燃焼（火の粉・熾）
        "礫 (ガレ×ヒビ×ウロ)",        // 破片（砕け・礫・鱗）
        "移動改 (バサ×ヨミ×シオ)",    // 移動（喧噪・軋み・移り木）
        "責め苦 (トウ×シガ)",         // 手番封じ（痺れ・見せしめ）
        "駆り立て (カリ×ドルガ)",     // 強化（駆り立て・号令・段）
    };

    // =================================================================================
    // Phase 0 —— 戦闘0回
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第200期 `form2 phase0` —— Q0-1〜Q0-7（戦闘0回）");
        Console.WriteLine();
        if (!ParryScan.Init()) return;
        string root = ParryScan.Root!;

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 `FormationRules` を読んでいる箇所");
        Console.WriteLine();
        string[] members =
        {
            "AreAdjacent", "SweepTargets", "LanesOf", "LanePath", "IsLanePredecessor", "AreSameRowPair",
            "IsSummonSlot", "SummonSlots", "PlayableSlots", "PlayableSlotsOfRow", "PlayableSlotCount",
            "RowOf", "DepthOf", "SeatNames", "TotalSlots", "LaneCount",
        };
        string[] projects = { "BattleCore", "BattleSim", "DemoApp", "GodotApp", "PrototypeApp" };
        var text = projects.ToDictionary(p => p, p =>
            Directory.Exists(Path.Combine(root, p))
                ? Directory.EnumerateFiles(Path.Combine(root, p), "*.cs", SearchOption.AllDirectories)
                           .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                                    && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                                    && !f.EndsWith("Formation2.cs"))
                           .Select(File.ReadAllLines).SelectMany(x => x)
                           .Where(l => !l.TrimStart().StartsWith("//")).ToList()
                : new List<string>());
        Console.WriteLine("| 名前 | 陣形で変わるか | " + string.Join(" | ", projects) + " |");
        Console.WriteLine("|---|:-:|" + string.Concat(projects.Select(_ => "--:|")));
        var shaped = new HashSet<string> { "AreAdjacent", "SweepTargets", "LanesOf", "LanePath", "IsLanePredecessor",
                                           "IsSummonSlot", "SummonSlots", "PlayableSlots", "PlayableSlotsOfRow" };
        foreach (string m in members)
        {
            string tok = "FormationRules" + "." + m;
            Console.WriteLine("| `" + m + "` | " + (shaped.Contains(m) ? "**変わる**" : "変わらない（幾何）") + " | "
                              + string.Join(" | ", projects.Select(p => text[p].Sum(l => Count(l, tok)).ToString())) + " |");
        }
        Console.WriteLine();
        int core = shaped.Sum(m => text["BattleCore"].Sum(l => Count(l, "FormationRules" + "." + m + "(")
                                                             + Count(l, "FormationRules" + "." + m + ")")
                                                             + Count(l, "FormationRules" + "." + m + " ")
                                                             + Count(l, "FormationRules" + "." + m + ";")));
        Console.WriteLine("- **行（`RowOf` / `DepthOf` / `.Row`）は陣形で変わらない**——9マスは今のまま 3レーン × 3列の格子で、"
                          + "パターン2の A〜E はちょうど今の召喚枠（○中1・○後2・○前2・○中3）と中央に乗る。列（前・中・後）は幾何なので表を持ち替えなくてよい");
        Console.WriteLine("- 陣形で変わる9本を BattleCore の中で呼んでいる箇所: **" + core + "**（`BattleSim` / `DemoApp` の呼び出しは X 字の表のまま残す＝診断と画面は X 字を前提にしている）");
        Console.WriteLine("- 差し替えの形: `UnitState.Shape`（陣形）を足し、`FormationRules.AreAdjacent(a, b)` の**駒を受け取る版**を足して "
                          + "`AreAdjacent(a.Slot, b.Slot)` を機械的に置き換える。**X 字の陣形は今の表をそのまま引く**ので1ビットも変わらない");
        Console.WriteLine();

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 「前列が生きている限り後列は狙われない」");
        Console.WriteLine();
        Console.WriteLine("実装は `BattleContext.PoolOf`（`SelectTargetChain` から呼ぶ）: `Row.Front` の生存者 → いなければ `Row.Mid` → いなければ全員。"
                          + "**列は `FormationRules.RowOf`（席の表）で決まり、陣形に依らない**。");
        Console.WriteLine();
        Console.WriteLine("| 席 | 番号 | 列 | X 字 | パターン2 |");
        Console.WriteLine("|---|--:|---|---|---|");
        string[] p2 = { "（空き）", "（空き）", "C", "（空き）", "（空き）", "A", "E", "D", "B" };
        for (int s = 0; s < FormationRules.TotalSlots; s++)
            Console.WriteLine("| " + FormationRules.SeatNames[s] + " | " + s + " | " + FormationRules.RowOf(s) + " | "
                              + (s < 5 ? "編成" : "召喚") + " | " + p2[s] + " |");
        Console.WriteLine();
        Console.WriteLine("- §2.2 の 2（生きている駒がいる一番前の列）は `PoolOf` そのもの。**X 字では前1・前3・○前2 が前、中央・○中1・○中3 が中、後1・後3・○後2 が後**で、今と同じ");
        Console.WriteLine("- パターン2: D（○前2）が前。D が倒れると A・C・E（○中1・中央・○中3）の3枚が前になる");
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 召喚の湧き先");
        Console.WriteLine();
        var summoners = UnitCatalog.All.Where(d => d.Traits.Any(t => t is TraitId.Splitter or TraitId.Betrayed)).Select(d => d.Name).ToList();
        Console.WriteLine("- 味方陣に湧く召喚は `ctx.Summon(…, self.TeamId)` の1本（分裂の胞子）。敵陣に湧くのは背かれの餌（ソム・**敵陣 ○前2 固定**。敵は X 字のままなので変わらない）");
        Console.WriteLine("- 保持者: " + string.Join("・", summoners));
        Console.WriteLine("- **決め: パターン2の湧き先は四隅、後ろから `後1 → 後3 → 前1 → 前3`**（X 字の `○中1 → ○中3 → ○前2 → ○後2` は今のまま）。"
                          + "前の角を先に埋めると湧いた胞子が前の列に立ち、D の前に盾が1枚増える＝「前衛1枚」の形がすぐ崩れる");
        Console.WriteLine("- 四隅の隣接・薙ぎ・貫通は §2.2 の規則を9マスに広げて作る（前1 は D・A・C と隣、など）。表は `form2 check` が全セル出す");
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 席・列・レーンを読む札");
        Console.WriteLine();
        Console.WriteLine("| 札 / 規則 | 読むもの | パターン2で |");
        Console.WriteLine("|---|---|---|");
        foreach (var (who, what, p) in new[]
        {
            ("逃亡（セロ）・後退の行き先 `FindBackSlotFor`", "1つ後ろの列の**編成枠**", "陣形の編成枠を引く（D → A/C/E、中 → B）。**変わる**（陣形の表へ）"),
            ("入れ替え・喧噪（バサ）", "生きている駒どうしの入れ替え", "席を交換するだけなので変わらない。敵側の召喚枠除外は陣形の召喚枠で"),
            ("曝き（敵）`HaulOutPair`", "後列の最大HP ⇔ 前列の最小HP", "B ⇔ D。変わらない（列は幾何）"),
            ("庇う（ガルド）・殉教", "`Row.Front`", "**D に立ったときだけ庇える**（前が1枚）"),
            ("後備え（セッキ）", "`Row.Back` と相手が前でないか", "B に立ったときだけ。A・C・E を守る"),
            ("棘守り（カド）`Covers`", "同じ列 ＋ 同じレーンの1つ前", "**変わる**——レーンの前後を陣形の表へ（D → C → B の縦1本）"),
            ("巨躯（ゴルム）", "`DepthOf` が自分より後ろ", "変わらない（列は幾何）"),
            ("突き返し（ハネ）", "敵の前1・前3（0/1 決め打ち）", "敵は X 字のままなので変わらない"),
            ("背かれ（ソム）", "敵陣の ○前2（7 決め打ち）", "敵は X 字のままなので変わらない"),
            ("突き崩し（旧 `Shove`・保持者 0）", "`PlayableSlots` の走査", "**変わる**（陣形の編成枠へ）"),
            ("勢い余って（ハネ）・`IsSummonSlot` の濾し", "召喚枠を除く", "**変わる**（陣形の召喚枠へ）"),
            ("鱗（後列到達）・狙撃（後退した駒）", "`Row.Back`", "変わらない（列は幾何）"),
        })
            Console.WriteLine("| " + who + " | " + what + " | " + p + " |");
        Console.WriteLine();

        // ---------------- Q0-5 ----------------
        Console.WriteLine("## Q0-5 会戦・作戦マップ・DemoApp");
        Console.WriteLine();
        string view = File.ReadAllText(Path.Combine(root, "DemoApp", "BattlefieldView3D.cs"));
        int pp = view.IndexOf("Vector3 " + "PawnPosition(");
        Console.WriteLine("- DemoApp の戦闘再生（`BattlefieldView3D.PawnPosition`）は**0〜8 の9席すべてに座標を持っている**（" + (pp >= 0 ? "確認" : "**見つからない**")
                          + "）。パターン2の駒は今の召喚枠の座標に立つので、**再生の側は描き足しが要らない見込み**。要るのは「パターン2の隊で戦を始める口」（`--demo-shape=p2`）だけ");
        Console.WriteLine("- 編成画面（`Main.tscn` の `FormationSlot` 5枠）は席 0〜4 の X 字で決め打ち。**今期は触らない**（§6-7。重いので次期）");
        Console.WriteLine("- 会戦（`EngagementEngine`）は `UnitState` を持ち越すので、駒に陣形を持たせれば次の戦へそのまま渡る。作戦マップ（Map11）は X 字のまま触らない");
        Console.WriteLine();

        // ---------------- Q0-6 ----------------
        Console.WriteLine("## Q0-6 過去の類似測定");
        Console.WriteLine();
        Console.WriteLine("- `design/` を「陣形」「前衛1枚」「ENEMY_FORMATION」「ひし形」で grep: **測定は0件**。触れているのは第198期の指示書の「この期に決めないこと」1行だけ");
        Console.WriteLine("- 盤面の形を変えたのは X 字化（2026-08-30・旧6枠 → 9枠）の1度だけで、そのときの測定値は旧盤面前提（`HANDOFF_NEXT_SESSION.md`）");
        Console.WriteLine();

        // ---------------- Q0-7 表A / 表B の行と、敵の範囲攻撃 ----------------
        Console.WriteLine("## Q0-7 表A・表B の行（測る前に固定）と、敵の範囲攻撃");
        Console.WriteLine();
        Console.WriteLine("**表A** は第196期の主表（`SidDiag.MainForms197`）の7行をそのまま使う。");
        Console.WriteLine();
        Console.WriteLine("**表B の候補** —— ガルドの在席行（`compare`）。ゴルム・ササ・スィドを含まない行だけが候補:");
        Console.WriteLine();
        Console.WriteLine("| 行 | ガルドの席 | ゴルム/ササ/スィド | 並び | 表B |");
        Console.WriteLine("|---|---|:-:|---|:-:|");
        foreach (var (name, f) in CompareBuilds().Where(r => Has(r.F, "gald")))
        {
            int gs = f.Occupied().First(o => o.Def.Id == "gald").Slot;
            bool clash = Has(f, "golm") || Has(f, "sasa") || Has(f, "sid");
            Console.WriteLine("| " + name + " | " + FormationRules.SeatNames[gs] + " | " + (clash ? "あり" : "—") + " | "
                              + string.Join(" ", f.Occupied().Select(o => o.Def.Name)) + " | " + (TankRows.Contains(name) ? "**○**" : "") + " |");
        }
        Console.WriteLine();
        var missing = TankRows.Where(n => !CompareBuilds().Any(r => r.Name == n)).ToList();
        if (missing.Count > 0) Console.WriteLine("**表B の行名が引けない: " + string.Join(", ", missing) + "**");
        Console.WriteLine();
        Console.WriteLine("**敵の攻撃型**（`EnemyCatalog.Stages`・素の型）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 単体 | 薙ぎ | 貫き | 全体 | 並び |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
        {
            var occ = EnemyCatalog.Stages[st].Enemy.Occupied().Select(o => o.Def).ToList();
            Console.WriteLine("| " + (st + 1) + " | " + occ.Count(d => d.Pattern == AttackPattern.Single) + " | "
                              + occ.Count(d => d.Pattern == AttackPattern.Sweep) + " | " + occ.Count(d => d.Pattern == AttackPattern.Pierce) + " | "
                              + occ.Count(d => d.Pattern == AttackPattern.All) + " | "
                              + string.Join(" ", occ.Select(d => d.Name + "(" + d.Pattern.ToString()[0] + ")")) + " |");
        }
        Console.WriteLine();
    }

    static int Count(string s, string tok)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(tok, i, StringComparison.Ordinal)) >= 0) { n++; i += tok.Length; }
        return n;
    }
}
