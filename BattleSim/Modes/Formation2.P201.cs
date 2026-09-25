using BattleCore;
using static Common;

// =====================================================================================
// form2 の第201期 —— パターン2の席を選んで測り直す（X 字も同じ条件で）
//
// 指示書は design/PHASE201_FORMATION2B_SPEC.md ／ 報告は design/PHASE201_FORMATION2B.md。
// **線は置かない**（採否はポンが遊んで決める）。
//
//     dotnet run --project BattleSim -c Release 0 form2 phase201   # Q0-2〜Q0-4 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 form2 run201     # 表A・表B・表C・帳簿（席の総当たり）
//     dotnet run --project BattleSim -c Release 0 form2 check201 [採用前のbalance.md]  # 自己検査
//
// 席の選び方（**測る前に固定**・指示書 §3.1）:
//   - 対象の行ごとに 5 枚の並べ方 120 通りをすべて試す（X 字もパターン2も同じ）
//   - **選ぶ**: 第2〜5波 × seed 0..49 の勝ち数の合計が最大の並び。同値は列挙順で最初
//     （列挙順＝`SlotAssignments(5)`。元の行の駒の順に、枠番号を辞書順で振る）
//   - **測る**: 選んだ並びを第2〜5波 × seed 100..299 で測る（選んだ試行と重ねない）
// =====================================================================================

static partial class Formation2Diag
{
    const int PickSeed0 = 0, PickSeeds = 50, MeasSeed0 = 100, MeasSeeds = 200;

    /// <summary>パターン2の枠 A〜E の表示名（指示書 §2・右が前）。1レーンを上とする。</summary>
    internal static readonly string[] DiamondSeatNames = { "中衛・上", "後衛", "中衛・中央", "前衛", "中衛・下" };

    static string SeatName(FormationShape s, int i) => s == FormationShape.Diamond ? DiamondSeatNames[i] : FormationRules.SeatNames[i];

    static string SeatsNamed(Formation f) => string.Join(" ／ ", f.Occupied().Select(o => SeatName(f.Shape, o.Slot) + ":" + o.Def.Name));

    /// <summary>120 通りの並び（列挙順）。</summary>
    static List<Formation> AllArrangements(IReadOnlyList<UnitDef> members, FormationShape shape)
    {
        var list = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation { Shape = shape };
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            list.Add(f);
        }
        return list;
    }

    /// <summary>鏡像（1レーン ↔ 3レーン）。X 字は前1↔前3・後1↔後3、パターン2は A↔E。</summary>
    static int MirrorSlot(FormationShape s, int i) => s == FormationShape.Diamond
        ? i switch { 0 => 4, 4 => 0, _ => i }
        : i switch { 0 => 1, 1 => 0, 3 => 4, 4 => 3, _ => i };

    static string Key(Formation f) => string.Join(",", Enumerable.Range(0, 5).Select(i => f[i]?.Id + "#" + (f[i] is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(f[i]!))));

    /// <summary>選ぶ試行の勝ち数（第2〜5波 × seed 0..49 の合計・最大 200）。並びごと。</summary>
    static int[] PickScores(List<Formation> arr)
    {
        var score = new int[arr.Count];
        Parallel.For(0, arr.Count * 4, j =>
        {
            int i = j / 4, st = 1 + j % 4;
            Formation en = EnemyCatalog.Stages[st].Enemy;
            int w = 0;
            for (int s = PickSeed0; s < PickSeed0 + PickSeeds; s++)
                if (BattleEngine.Run(arr[i], en, s, verbose: false).PlayerWon) w++;
            Interlocked.Add(ref score[i], w);
        });
        return score;
    }

    sealed record Pick(Formation Best, int Index, int Score, int MirrorScore, int Second, List<Formation> All, int[] Scores);

    static Pick Select(IReadOnlyList<UnitDef> members, FormationShape shape, Func<Formation, bool>? filter = null)
    {
        var all = AllArrangements(members, shape);
        var sc = PickScores(all);
        int best = -1;
        for (int i = 0; i < all.Count; i++)
            if ((filter is null || filter(all[i])) && (best < 0 || sc[i] > sc[best])) best = i;
        Formation b = all[best];
        var mirror = new Formation { Shape = shape };
        for (int i = 0; i < 5; i++) mirror[MirrorSlot(shape, i)] = b[i];
        int mi = all.FindIndex(f => Key(f) == Key(mirror));
        int second = Enumerable.Range(0, all.Count).Where(i => i != best && (filter is null || filter(all[i]))).Select(i => sc[i]).DefaultIfEmpty(0).Max();
        return new Pick(b, best, sc[best], mi >= 0 ? sc[mi] : -1, second, all, sc);
    }

    static double PickPct(int score) => 100.0 * score / (4 * PickSeeds);

    // =================================================================================
    // Phase 0 —— 戦闘0回
    // =================================================================================

    static void Phase201()
    {
        Console.WriteLine("# 第201期 `form2 phase201` —— Q0-1〜Q0-4（戦闘0回）");
        Console.WriteLine();
        string root = FindRoot();

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 編成画面で陣形を選ぶ入口と、作戦マップ");
        Console.WriteLine();
        string demo = Path.Combine(root, "DemoApp");
        int Cnt(string file, string tok) => File.Exists(Path.Combine(demo, file)) ? Count(File.ReadAllText(Path.Combine(demo, file)), tok) : -1;
        Console.WriteLine("| 触る所 | 今 | 変える量 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 入口（`Main.BuildFooter` / `BuildHeader`） | プリセット・波の選択と「この配置で出撃」 | 陣形の切り替えボタン1つ（X 字 ⇔ パターン2） |");
        Console.WriteLine("| 出撃（`Main.StartBattle`） | `new Formation()` に 5 枠を写す → `ApplyDemoShape` | `Shape` を1行で渡す（`--demo-shape=p2` は残す） |");
        Console.WriteLine("| 枠の位置（`BattlefieldView.SetupTopLeft`） | 5 枠の座標を switch で決め打ち（右が前） | 陣形ごとの座標を1本足す |");
        Console.WriteLine("| 枠の席名（`FormationSlot.Configure` → `SeatNames[slot]`） | 1度だけ設定（`SeatNames` 参照 " + Cnt("UiKit.cs", "SeatNames[") + " 箇所） | 付け替える口を1本足す |");
        Console.WriteLine("| 関係の線（`Map11Relations.Of` → `FormationRules.AreAdjacent(int,int)`） | X 字の静的な表（`AreAdjacent(` " + Cnt("Map11Relations.cs", "AreAdjacent(") + " 箇所） | 陣形を受け取る版（既定 X 字）。**X 字は1ビットも変えない** |");
        Console.WriteLine("| 通知文（`Main` の席名） | `SeatNames[slot]`（" + Cnt("Main.cs", "SeatNames[") + " 箇所） | 陣形の席名へ |");
        Console.WriteLine();
        Console.WriteLine("- **戦闘の再生は触らなくてよい**（第200期の `--demo-shape=p2` で確認済み。`PawnPosition` が 9 席すべてに座標を持つ）");
        Console.WriteLine("- 作戦マップ（Map11）の部隊は `Formation` を持たず、`UnitState.Slot`（席 0〜4 を X 字と仮定）を組み直しの操作が直に書き換える"
                          + "（`SwapSeats` / `SwapWithBench` / `ResetSquad` / `PickUp`・`SeatDiagram.Layout` の決め打ち・`--map11-verify` の門）。"
                          + "**重いので次期**（指示書 §2 の但し書き）");
        Console.WriteLine();

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 既存の席の総当たりの道具");
        Console.WriteLine();
        string reseat = File.ReadAllText(Path.Combine(root, "BattleSim", "Modes", "Reseat.cs"));
        string layout = File.ReadAllText(Path.Combine(root, "BattleSim", "Modes", "Layout.cs"));
        bool rsX = reseat.Contains("var f = new " + "Formation();");
        bool lyX = layout.Contains("new " + "Formation()");
        Console.WriteLine("| 道具 | 並び方 | 選ぶ試行 | 測る試行 | 陣形 |");
        Console.WriteLine("|---|---|---|---|---|");
        Console.WriteLine("| `reseat` | `SlotAssignments(5)` の 120 通り | 5波 × seed 0..49 | 上位を seed 0..199（**選ぶ試行と重なる**） | " + (rsX ? "**X 字だけ**（`new Formation()` 決め打ち）" : "?") + " |");
        Console.WriteLine("| `layout` | 同上 | 5波 × seed 0..49 | 波別最良を測り直す | " + (lyX ? "**X 字だけ**" : "?") + " |");
        Console.WriteLine("| `confirm` | `reseat` の候補を別の帯（seed 200..599）で追試 | — | — | X 字だけ |");
        Console.WriteLine();
        Console.WriteLine("- **並べ方の列挙（`Common.SlotAssignments`）だけ流用する**。`reseat` は第1波を選ぶ試行に含め（全行 100% の教習波・規約 (G10)）、"
                          + "測る帯が選ぶ帯を含むので §3.1 の「選ぶ試行と測る試行を分ける」に合わない。陣形も X 字の決め打ち");
        Console.WriteLine("- 新しく書く（`Formation2.P201.cs`）: 選ぶ＝第2〜5波 × seed 0..49 の勝ち数の合計が最大・同値は列挙順で最初／測る＝第2〜5波 × seed 100..299");
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 戦闘数");
        Console.WriteLine();
        int rowsA = SidDiag.MainForms197().Count, rowsB = TankRows.Length;
        long perSel = 120L * 4 * PickSeeds;
        int selA = rowsA * 2, selB = rowsB * 5, selC = rowsA;   // 表C は「スィドを D に置いた並び」の選び直しが要る行だけ（上限の見積もり）
        long selBattles = (selA + selB) * perSel;
        long measCfg = rowsA * (1 + 2 + 2) + rowsB * 5 + rowsA * 3 * 2;   // A: 元・X選・P2選・第200期の X/P2（参考）／B: 5列／C: 3版 × 最大2並び
        long measBattles = measCfg * 5L * MeasSeeds;
        Console.WriteLine("| 段 | 数え方 | 戦闘数 |");
        Console.WriteLine("|---|---|--:|");
        Console.WriteLine("| 選ぶ（表A） | " + rowsA + " 行 × 2 陣形 × 120 通り × 4 波 × " + PickSeeds + " seed | " + (selA * perSel).ToString("N0") + " |");
        Console.WriteLine("| 選ぶ（表B） | " + rowsB + " 行 × 5 列 × 120 通り × 4 波 × " + PickSeeds + " seed | " + (selB * perSel).ToString("N0") + " |");
        Console.WriteLine("| 選ぶ（表C・スィドを D に置いた並び） | 表A の P2・選 の並びを再利用（120 通りの勝ち数から D＝スィドの 24 通りを濾すだけ） | 0 |");
        Console.WriteLine("| 測る | 最大 " + measCfg + " 編成 × 5 波 × " + MeasSeeds + " seed | " + measBattles.ToString("N0") + " |");
        Console.WriteLine("| **合計** | | **" + (selBattles + measBattles).ToString("N0") + "** |");
        Console.WriteLine();
        Console.WriteLine("- 所要の目安: `compare`（61 行 × 5 波 × 200 seed ＝ 61,000 戦）が数秒なので、**1.1M 戦で 1〜3 分**。削らなくてよい");
        Console.WriteLine("- 表C の選び直しに新しい戦闘は要らない——P2・選 は 120 通りすべての勝ち数を持っているので、スィドが D の 24 通りの中の最大を引けばよい（選ぶ試行は同じ S の版）");
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 並べ方の重複（鏡像は同じ盤面か）");
        Console.WriteLine();
        Console.WriteLine("鏡像＝1レーン ↔ 3レーンの入れ替え（X 字: 前1↔前3・後1↔後3 ／ パターン2: 中衛・上↔中衛・下）。**盤の表は鏡像で閉じている**:");
        Console.WriteLine();
        bool symX = MirrorClosed(FormationShape.X), symD = MirrorClosed(FormationShape.Diamond);
        Console.WriteLine("- X 字の隣接・薙ぎを鏡像で写すと同じ表になるか: " + (symX ? "○" : "×") + " ／ パターン2: " + (symD ? "○" : "×"));
        Console.WriteLine();
        Console.WriteLine("**ただし盤面の外側が対称でない**:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵 前1 ／ 前3 | 敵 後1 ／ 後3 | 敵は上下対称か |");
        Console.WriteLine("|---|---|---|:-:|");
        for (int st = 1; st < 5; st++)
        {
            Formation e = EnemyCatalog.Stages[st].Enemy;
            string N(int i) => e[i]?.Name ?? "—";
            bool sym = e[0]?.Id == e[1]?.Id && e[3]?.Id == e[4]?.Id;
            Console.WriteLine("| " + (st + 1) + " | " + N(0) + " ／ " + N(1) + " | " + N(3) + " ／ " + N(4) + " | " + (sym ? "○" : "×") + " |");
        }
        Console.WriteLine();
        string eng = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
        bool tie12 = eng.Contains("DeterministicPierce");
        Console.WriteLine("- 敵の編成は上下対称でない。さらに**パターン2の貫きは同数なら 1-2 レーン**（" + (tie12 ? "`DeterministicPierce` の枝" : "?") + "）で、"
                          + "標的の抽選（`Roll`）は生きている駒の並び順の添字を引くので、**鏡像の2つは同じ seed で同じ戦にならない**（期待値でも同じとは言えない）");
        Console.WriteLine("- **重複は落とさない**。戦闘数は 1.1M 戦で削る必要が無い。代わりに `run201` は選んだ並びの**鏡像の勝ち数**も並べる（鏡像との差が選ぶ試行のノイズの目安になる）");
        Console.WriteLine();
    }

    static bool MirrorClosed(FormationShape s)
    {
        int M(int slot)
        {
            int i = s.PlayableSlots.ToList().IndexOf(slot);
            if (i >= 0) return s.PlayableSlots[MirrorSlot(s, i)];
            // 召喚枠: 格子のレーン 1↔3 で写す（行は同じ）
            int lane = FormationShape.GridLane(slot);
            if (lane == 2) return slot;
            return Enumerable.Range(0, FormationRules.TotalSlots).First(o => FormationRules.RowOf(o) == FormationRules.RowOf(slot)
                                                                       && FormationShape.GridLane(o) == 4 - lane
                                                                       && s.IsPlayable(o) == s.IsPlayable(slot));
        }
        for (int a = 0; a < 9; a++)
            for (int b = 0; b < 9; b++)
                if (s.AreAdjacent(a, b) != s.AreAdjacent(M(a), M(b))) return false;
        for (int a = 0; a < 9; a++)
            if (!s.SweepTargets(a).Select(M).OrderBy(x => x).SequenceEqual(s.SweepTargets(M(a)).OrderBy(x => x))) return false;
        return true;
    }

    static string FindRoot()
    {
        string d = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(d, "CLAUDE.md"))) d = Path.GetDirectoryName(d) ?? throw new InvalidOperationException("CLAUDE.md が見つからない");
        return d;
    }
}

static partial class Formation2Diag
{
    static partial void Run201Impl(string arg);
    static partial void Check201Impl(string arg);
    static void Run201(string arg) => Run201Impl(arg);
    static void Check201(string arg) => Check201Impl(arg);
}
