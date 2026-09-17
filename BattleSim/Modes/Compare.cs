using BattleCore;
using static Common;

// =====================================================================================
// compare モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "compare")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 compare
// =====================================================================================

static class CompareDiag
{
// compare モード: 代表的な編成を全ステージで比較する。
// 総当たりは駒が増えるほど爆発するので、系統ごとの当たり外れはこちらで見る。
public static void Run(string[] args, int stageIndex)
{
    var builds = CompareBuilds();

    const int CompareSeeds = 200;

    // 第126期 段1 —— 勝ち方の質（残存・圧勝率・完全勝利・全滅勝ち）。
    //
    // **`docs/balance.md` には1文字も足さない。別の生成物（`docs/quality.md`）にする。**
    // 指示書 §4-1 は「`compare` の出力に足す」と書いているが、**同じ節が挙げている理由
    // （(G8) の検算が使えなくなる）が、足した瞬間に現実になる**——`docs/balance.md` を読む
    // 自己検査は「`| ` で始まり `%` を含む行」を**位置で**突き合わせる版（`curse check`）と、
    // **行名をキーに 2..6 列目を数値として読む**版（`encore check`）の2種類があり、
    // 勝ち方の質の表は**どちらにも引っかかる**（実測で 61 行 / 293 件のずれが出た）。
    // **指示書の字と、指示書が挙げた理由が食い違っている場所なので、理由のほうを採る。**
    //
    // **戦闘は1回も増えない**——勝率表と同じ `BattleResult` から読むだけ。
    //
    //     dotnet run --project BattleSim -c Release 0 compare > docs/balance.md          # 勝率表（不変）
    //     dotnet run --project BattleSim -c Release 0 compare quality > docs/quality.md  # 勝ち方の質
    bool cqQuality = args.Length > 2 && args[2] == "quality";

    // そのまま docs/balance.md になるので、見出しと注意書きもここで吐く。
    // 手で足した文章はリダイレクトのたびに消えるため、文書の体裁ごと生成物にする。
    if (!cqQuality)
    {
        Console.WriteLine("# 勝率表");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 compare > docs/balance.md` の出力。手で編集しない。");
        Console.WriteLine($"代表編成 × 全ステージ、seed 0..{CompareSeeds - 1} の {CompareSeeds} 試行。");
        Console.WriteLine();

        // 列はステージ数から作るので、ステージを足しても勝手に増える。
        // 固定幅で揃えるのはやめた。全角の編成名では桁が合わない（`,-24` は表示幅ではなく文字数を数える）。
        Console.WriteLine("| 編成 |" + string.Concat(EnemyCatalog.Stages.Select((st, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|---|" + string.Concat(EnemyCatalog.Stages.Select(_ => "---:|")));
    }

    var qWins = new int[builds.Length];
    var qSurv = new double[builds.Length];
    var qBlow = new int[builds.Length];      // 生存 >= 4（`run` の `圧勝率` と同じ定義）
    var qPerf = new int[builds.Length];      // 生存 >= 出撃数（**この期の新しい列**）
    var qNarrow = new int[builds.Length];    // 生存 <= 1
    var qParty = new int[builds.Length];

    // 第127期 段0-2 —— **波別の完全勝利率**。
    // 通算だけだと「第三波だけを見た人には 76.1% がその戦の確率に読める」（指示書 §3-1）。
    // **戦闘は1回も増えない**（同じ `BattleResult` から波ごとに落とすだけ）。
    var qWinsW = new int[builds.Length, EnemyCatalog.Stages.Count];
    var qPerfW = new int[builds.Length, EnemyCatalog.Stages.Count];

    // 第129期 段1 —— **無傷勝利 / 実質無傷勝利 / 決着ターンの分布**。
    //
    // **現行の `完全勝利` は緩い。** 判定式は `PlayerSurvivors >= 出撃数` だが、
    // `PlayerSurvivors` は `ctx.LivingMembers(PlayerTeam).Count()` なので**戦闘中に湧いた駒
    // （胞子・餌）も数える**——出撃5枚のうち3枚が落ちて胞子が3体湧いた戦も通る。
    // 分母も「勝った試行」だけなので、負けた試行が1つも効かない。
    //
    // **ここは `BattleResult.PlayerStarterFallen`（出撃した駒だけを個体の同一性で見る）を読む。**
    // **戦闘は1回も増えていない**——上の表とまったく同じ `BattleResult` から落とすだけ。
    var qTrials = new int[builds.Length];                          // 分母（第2〜5波の**全試行**）
    var qClean = new int[builds.Length];                           // 無傷勝利
    var qClean2 = new int[builds.Length];                          // 実質無傷勝利
    var qTrialsW = new int[builds.Length, EnemyCatalog.Stages.Count];
    var qCleanW = new int[builds.Length, EnemyCatalog.Stages.Count];
    var qClean2W = new int[builds.Length, EnemyCatalog.Stages.Count];
    var qPerfDirty = new int[builds.Length];   // 現行の「完全勝利」のうち**出撃駒が欠けている**試行
    var qOver = new int[builds.Length];        // `PlayerSurvivors` > 出撃数（現行の判定式を通せる形）
    var qSummonAlive = new int[builds.Length]; // `PlayerSurvivors` ≠ 出撃数 − 欠けた数（湧いた駒が残った戦）
    var qExcused = new HashSet<string>[builds.Length];             // 免除する駒（`OnDeath` の保持者）
    var qTurnAll = new List<int>[builds.Length];                   // 決着T（第2〜5波・全試行）
    var qTurnW = new List<int>[builds.Length][];

    // === 第140期 —— 行（編成）ごとに並列化した（器具の期・値は1ビットも変えない） ===
    //
    // **この表の集計は、どの行も自分の `[bi]` の枠にしか書かない**——`qWins[bi]` も
    // `qTurnAll[bi]` も行の私物なので、行をまたいだ競合が原理的に起きない。
    // **波（`w`）では割らない**——`qWins[bi] += ...` のように波をまたいで足す量があるので、
    // 同じ行の2つの波を別スレッドに置くと壊れる。**割ってよい軸は行だけ。**
    //
    // 印字は従来どおり直列に `builds` の順で行う（行の文字列を控えて後でまとめて出す）。
    var cqRow = new string[builds.Length];
    Parallel.For(0, builds.Length, bi =>
    {
        (string name, Formation f) = builds[bi];
        qParty[bi] = f.Occupied().Count();
        qExcused[bi] = SurviveScan.ExcusedIds(f);
        qTurnAll[bi] = new List<int>();
        qTurnW[bi] = new List<int>[EnemyCatalog.Stages.Count];
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++) qTurnW[bi][w] = new List<int>();
        var cells = new List<string>();
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
        {
            EnemyCatalog.Stage st = EnemyCatalog.Stages[w];
            int wins = 0;
            for (int seed = 0; seed < CompareSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, st.Enemy, seed, verbose: false);

                // --- 第129期 段1 ——「勝ったか」より前に**全試行**を分母に取る ---------------
                // **第一波は実行して除外**（規約 (G10)。ここも同じ分母の切り方にそろえる）。
                if (w > 0)
                {
                    qTrials[bi]++; qTrialsW[bi, w]++;
                    qTurnAll[bi].Add(r.Turns); qTurnW[bi][w].Add(r.Turns);
                    if (r.PlayerSurvivors > qParty[bi]) qOver[bi]++;
                    // **湧いた駒が数えられている証拠は「> 出撃数」ではなく「≠ 出撃数 − 欠けた数」**
                    // ——前者は「欠けた数 ≦ 湧いた数」まで要求するので、1枚欠けて1体湧いた戦を数え落とす。
                    if (r.PlayerSurvivors != qParty[bi] - r.PlayerStarterFallen.Count) qSummonAlive[bi]++;
                    if (r.PlayerWon)
                    {
                        bool clean = r.PlayerStarterFallen.Count == 0;
                        if (clean) { qClean[bi]++; qCleanW[bi, w]++; }
                        // **免除するのは `OnDeath` を上書きする札の保持者だけ**
                        // （`OnAnyDeath` / `OnAllyDeath` は「他人の死を読む側」なので混ぜない
                        //   ——混ぜるとリィカ・ラウ・ハギの死軸が丸ごと免除される）。
                        if (r.PlayerStarterFallen.All(id => qExcused[bi].Contains(id)))
                        { qClean2[bi]++; qClean2W[bi, w]++; }
                        if (r.PlayerSurvivors >= qParty[bi] && !clean) qPerfDirty[bi]++;
                    }
                }

                if (!r.PlayerWon) continue;
                wins++;
                if (w == 0) continue;
                qWins[bi]++;
                qSurv[bi] += r.PlayerSurvivors;
                if (r.PlayerSurvivors >= 4) qBlow[bi]++;
                if (r.PlayerSurvivors >= qParty[bi]) qPerf[bi]++;
                if (r.PlayerSurvivors <= 1) qNarrow[bi]++;
                qWinsW[bi, w]++;
                if (r.PlayerSurvivors >= qParty[bi]) qPerfW[bi, w]++;
            }
            cells.Add($" {wins * 100.0 / CompareSeeds:F1}% |");
        }
        cqRow[bi] = $"| {name} |" + string.Concat(cells);
    });

    if (!cqQuality)
    {
        foreach (string row in cqRow) Console.WriteLine(row);
        return;
    }

    Console.WriteLine("# 勝ち方の質");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 compare quality > docs/quality.md` の出力。手で編集しない。");
    Console.WriteLine($"代表編成 × 全ステージ、seed 0..{CompareSeeds - 1} の {CompareSeeds} 試行"
        + "（**勝率表と同じ戦・同じ帯**。戦闘は1回も増えていない）。");
    Console.WriteLine();
    Console.WriteLine("**`docs/balance.md` とは別のファイルにしてある。** 勝率表に節を足すと、"
        + "`docs/balance.md` を読む自己検査（`curse check` は `| ` で始まり `%` を含む行を位置で、"
        + "`encore check` は行名をキーに 2..6 列目を数値として読む）が両方とも壊れる"
        + "——実測で 61 行 / 293 件のずれが出た。規約 (G8) の必須1 を守るほうを採った。");
    Console.WriteLine();
    Console.WriteLine("## 勝ち方の質（第2〜5波）");
    Console.WriteLine();
    Console.WriteLine("**分母は「第2〜5波で勝った試行」**（規約 (G10)。第一波は全行必勝の教習波なので入れない）。");
    Console.WriteLine("定義は `run` の `RunSolo` から写した——`残存` は勝った試行の平均生存数、");
    Console.WriteLine("`圧勝率` は生存4体以上、`全滅勝ち` は生存1体以下の割合。");
    Console.WriteLine();
    Console.WriteLine("`完全勝利` は**この期に足した列**で、**生存数 ≧ 出撃数**（＝1体も失わずに勝った割合）。");
    Console.WriteLine("`圧勝率`（4体以上）とは別の量である——`PlayerSurvivors` は戦闘中に湧いた駒");
    Console.WriteLine("（胞子・餌）も数えるので、`==` ではなく `>=` で書いてある。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 出撃 | 勝った試行 | 残存 | 圧勝率 | **完全勝利** | 全滅勝ち |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    for (int bi = 0; bi < builds.Length; bi++)
    {
        int n = qWins[bi];
        Console.WriteLine($"| {builds[bi].Name} | {qParty[bi]} | {n} "
            + (n == 0
                ? "| — | — | — | — |"
                : $"| {qSurv[bi] / n:F2} | {qBlow[bi] * 100.0 / n:F1}% "
                  + $"| **{qPerf[bi] * 100.0 / n:F1}%** | {qNarrow[bi] * 100.0 / n:F1}% |"));
    }

    int tw = qWins.Sum();
    if (tw > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"**全 {builds.Length} 行の通算**（勝った試行 {tw}）: "
            + $"残存 **{qSurv.Sum() / tw:F2}** ／ 圧勝率 **{qBlow.Sum() * 100.0 / tw:F1}%** ／ "
            + $"**完全勝利 {qPerf.Sum() * 100.0 / tw:F1}%** ／ 全滅勝ち **{qNarrow.Sum() * 100.0 / tw:F1}%**。");
    }

    // --- 第127期 段0-2 —— 波別の完全勝利率 -------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 波別の完全勝利率（第127期 段0-2）");
    Console.WriteLine();
    Console.WriteLine("上の表の `完全勝利` は**第2〜5波の通算**なので、**1つの波だけを見たときの確率ではない**。");
    Console.WriteLine("分母は**その波で勝った試行**（波ごとに違う）。`—` はその波で1度も勝っていない行。");
    Console.WriteLine("**戦闘は1回も増えていない**——上の表とまったく同じ `BattleResult` から波ごとに落としただけ。");
    Console.WriteLine("**第一波は判定に使わない**（規約 (G10)。全行必勝の教習波）ので列にも出さない。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
        .Select(i => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
        .Select(_ => "---:|")));
    for (int bi = 0; bi < builds.Length; bi++)
    {
        var cells = new List<string>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
            cells.Add(qWinsW[bi, w] == 0
                ? " — |"
                : $" {qPerfW[bi, w] * 100.0 / qWinsW[bi, w]:F1}% |");
        Console.WriteLine($"| {builds[bi].Name} |" + string.Concat(cells));
    }
    Console.WriteLine();
    for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
    {
        int ww = 0, pp = 0;
        for (int bi = 0; bi < builds.Length; bi++) { ww += qWinsW[bi, w]; pp += qPerfW[bi, w]; }
        Console.WriteLine($"- **第{w + 1}波の通算**: 勝った試行 {ww} ／ 完全勝利 "
            + (ww == 0 ? "—" : $"**{pp * 100.0 / ww:F1}%**"));
    }

    // --- 第129期 段1 —— 無傷勝利 / 実質無傷勝利 --------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 無傷勝利と実質無傷勝利（第129期 段1）");
    Console.WriteLine();
    Console.WriteLine("**上の `完全勝利` は緩い。** 判定式は `PlayerSurvivors >= 出撃数` だが、");
    Console.WriteLine("`PlayerSurvivors` は `ctx.LivingMembers(PlayerTeam).Count()` なので"
        + "**戦闘中に湧いた駒（胞子・餌）も数える**。");
    Console.WriteLine("分母も「勝った試行」だけなので、負けた試行が1つも効かない。");
    Console.WriteLine();
    Console.WriteLine("**ここは `BattleResult.PlayerStarterFallen` を読む**"
        + "——出撃した駒だけを、**個体（`UnitState`）の同一性で**見る。");
    Console.WriteLine("**戦闘は1回も増えていない**（上の表とまったく同じ `BattleResult` から落としただけ）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 定義 | 分母 |");
    Console.WriteLine("|---|---|---|");
    Console.WriteLine("| `完全勝利(参考)` | 上の表と同じ値（`PlayerSurvivors >= 出撃数`） | 勝った試行 |");
    Console.WriteLine("| `無傷勝利` | **出撃した駒が1枚も欠けずに勝った**（`PlayerStarterFallen` が空） | **全試行** |");
    Console.WriteLine("| `実質無傷勝利` | 上から**自分の死が起動条件の駒**を免除したもの"
        + "（欠けたのが免除対象だけなら成功とみなす） | **全試行** |");
    Console.WriteLine("| `免除` | その行で免除した駒。`—` は免除対象が1枚もいない行 | — |");
    Console.WriteLine();
    Console.WriteLine($"**免除の判定は実装から引く**——`OnDeath` を上書きする札 "
        + $"**{SurviveScan.DeathTraits.Count} 本**"
        + $"（{SurviveScan.NameList(SurviveScan.DeathTraits)}）の保持者。");
    Console.WriteLine($"**`OnAnyDeath` / `OnAllyDeath`（他人の死を読む側）は混ぜない**"
        + $"——{SurviveScan.OtherDeathTraits.Count} 本"
        + $"（{SurviveScan.NameList(SurviveScan.OtherDeathTraits)}）は**1つも免除に使っていない**。");
    Console.WriteLine("混ぜると墓守リィカ・疫みのラウ・追い打ちのハギを含む死軸が丸ごと免除される。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 出撃 | 全試行 | 完全勝利(参考) | **無傷勝利** | **実質無傷勝利** | 免除 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|---|");
    for (int bi = 0; bi < builds.Length; bi++)
    {
        int n = qTrials[bi], nw = qWins[bi];
        string ex = qExcused[bi].Count == 0
            ? "—"
            : string.Join("・", builds[bi].F.Occupied()
                .Where(o => qExcused[bi].Contains(o.Def.Id)).Select(o => o.Def.Name));
        Console.WriteLine($"| {builds[bi].Name} | {qParty[bi]} | {n} "
            + (nw == 0 ? "| — " : $"| {qPerf[bi] * 100.0 / nw:F1}% ")
            + (n == 0
                ? "| — | — "
                : $"| **{qClean[bi] * 100.0 / n:F1}%** | **{qClean2[bi] * 100.0 / n:F1}%** ")
            + $"| {ex} |");
    }

    int tt = qTrials.Sum();
    if (tt > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"**全 {builds.Length} 行の通算**（全試行 {tt} ／ 勝った試行 {tw}）: "
            + $"完全勝利(参考) **{qPerf.Sum() * 100.0 / Math.Max(1, tw):F1}%** ／ "
            + $"無傷勝利 **{qClean.Sum() * 100.0 / tt:F1}%** ／ "
            + $"実質無傷勝利 **{qClean2.Sum() * 100.0 / tt:F1}%**。");
        Console.WriteLine();
        Console.WriteLine($"- 現行の「完全勝利」に数えられた {qPerf.Sum()} 試行のうち、"
            + $"**出撃した駒が1枚以上欠けているもの {qPerfDirty.Sum()} 件**"
            + $"（{qPerfDirty.Sum() * 100.0 / Math.Max(1, qPerf.Sum()):F1}%）"
            + "——**召喚体が欠けを埋めた戦**。");
        Console.WriteLine($"- 決着時に**湧いた駒が盤上に残っていた**試行 **{qSummonAlive.Sum()} 件**"
            + $"（全試行の {qSummonAlive.Sum() * 100.0 / tt:F1}%。"
            + "`PlayerSurvivors` ≠ 出撃数 − 欠けた数）——**湧いた駒が数えられていることの直接の証拠**。"
            + $"うち `PlayerSurvivors` が出撃数を上回った試行は **{qOver.Sum()} 件**"
            + "（＝現行の判定式を実際に通せる形）。");
        Console.WriteLine($"- 免除対象を1枚以上含む行 **{qExcused.Count(h => h.Count > 0)} / {builds.Length}**。");
    }

    // --- 第129期 段1 —— 波別の無傷勝利率 ---------------------------------------------
    Console.WriteLine();
    Console.WriteLine("### 波別の無傷勝利率（分母はその波の全試行）");
    Console.WriteLine();
    Console.WriteLine("`—` は分母 0 の波。**第一波は判定に使わない**（規約 (G10)）ので列にも出さない。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
        .Select(i => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
        .Select(_ => "---:|")));
    for (int bi = 0; bi < builds.Length; bi++)
    {
        var cells = new List<string>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
            cells.Add(qTrialsW[bi, w] == 0
                ? " — |"
                : $" {qCleanW[bi, w] * 100.0 / qTrialsW[bi, w]:F1}% |");
        Console.WriteLine($"| {builds[bi].Name} |" + string.Concat(cells));
    }

    // --- 第129期 段1 —— 決着ターンの分布 ---------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 決着ターンの分布（第129期 段1）");
    Console.WriteLine();
    Console.WriteLine("**勝率からは「安定した」と「運の幅が広がった」が区別できない**"
        + "（第128期の宿題。8ターン決着と9ターン全滅が同居しうる）。");
    Console.WriteLine("**分母は全試行**（勝ち負けを問わない）。`σ` は標本標準偏差、"
        + "`Q1`/`中`/`Q3` は四分位（線形補間なしの順位法）。");
    Console.WriteLine("**戦闘は1回も増えていない**（`BattleResult.Turns` を読んだだけ）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 第2〜5波 平均 | σ | Q1 | 中 | Q3 |"
        + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
            .Select(i => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|--:|--:|--:|--:|--:|"
        + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1).Select(_ => "---:|")));
    for (int bi = 0; bi < builds.Length; bi++)
    {
        List<int> all = qTurnAll[bi];
        var cells = new List<string>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
        {
            List<int> v = qTurnW[bi][w];
            cells.Add(v.Count == 0 ? " — |" : $" {SurviveScan.Mean(v):F2}±{SurviveScan.Sd(v):F2} |");
        }
        Console.WriteLine($"| {builds[bi].Name} "
            + (all.Count == 0
                ? "| — | — | — | — | — "
                : $"| {SurviveScan.Mean(all):F2} | {SurviveScan.Sd(all):F2} "
                  + $"| {SurviveScan.Quantile(all, 0.25)} | {SurviveScan.Quantile(all, 0.50)} "
                  + $"| {SurviveScan.Quantile(all, 0.75)} ")
            + "|" + string.Concat(cells));
    }
    return;
}
}
