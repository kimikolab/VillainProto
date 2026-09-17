using BattleCore;
using static Common;

// =====================================================================================
// choice モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "choice")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 choice
// =====================================================================================

static class ChoiceDiag
{
// ============================================================================
// choice モード（第102期）—— 境界に選択を置く（蘇生・回復・持ち越しから1つ）
//
// 起点は第101期。**持ち越しの代金は HP ではなく体だった**（毎境界まるごと全快でも
// 突破率 0.97 → 5.81%・**帯までの距離の 91% を死者の側が持っている**）。
// 通貨が3つとも実測されたので、**「3つの罰のうち1つだけ免除する」**形にして並べる。
//
// 触るのは**会戦の境界の規則1つ**（`BoundaryRule`）だけ。駒・特性・数値・戦闘中の判定・
// `Presets` / `CompareBuilds()` / `CrossBuilds()` / `Stages` / `Columns` は 1 行も触っていない。
//
// **判定の本体は勝率ではない**——各境界で最良だった選択肢の分布。1つに偏るなら選択肢ではない。
//
//     dotnet run --project BattleSim -c Release 0 choice phase0   # 表A（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 choice tables   # 表A〜G を通しで（既定）
public static void Run(string[] args, int stageIndex)
{
    string bcSub = args.Length > 2 ? args[2] : "tables";
    const int BcSeeds = 200;

    // ---- 判定の線（**測る前に固定する**。ここを結果を見てから緩めない・第64期） ----
    const double Q1SkewMax = 60.0;      // 最良だった選択肢の分布が 1 つに この% 以上偏らないこと
    const double Q2MedianLo = 5.0, Q2MedianHi = 50.0;
    const int Q2ZeroMax = 37;
    const double Q3GapLine = 1.0;       // 「最良」と固定最良の差（pt）。これ未満なら選ばせる意味が無い

    var bcRows = CompareBuilds().Concat(CrossBuilds()).ToArray();

    EnemyCatalog.Column bcCol3 = EnemyCatalog.Columns
        .Where(c => c.Squads.Count == 3).OrderBy(c => c.Name).First();
    EnemyCatalog.Column bcCol5 = EnemyCatalog.Columns
        .First(c => ReferenceEquals(c.Squads, EnemyCatalog.EngagementColumn));

    var bcChoices = new[] { BoundaryChoice.Revive, BoundaryChoice.Heal, BoundaryChoice.Carry };
    static string BcLabel(BoundaryChoice c) => c switch
    {
        BoundaryChoice.Revive => "蘇生",
        BoundaryChoice.Heal => "回復",
        BoundaryChoice.Carry => "持ち越し",
        _ => "現行",
    };

    // ---------------- 表A（Phase 0・戦闘0回） ----------------
    void BcEmitA()
    {
        Console.WriteLine("## 表A —— Phase 0（**戦闘0回**）");
        Console.WriteLine();

        // (1) OnCarryOver の全数（**実装から引く**。手で写さない・第94期）
        Console.WriteLine("### (1) `OnCarryOver` を実装している特性の全数（reflection で引いた）");
        Console.WriteLine();
        var impl = typeof(Trait).Assembly.GetTypes()
            .Where(t => typeof(Trait).IsAssignableFrom(t))
            .Select(t => t.GetMethod("OnCarryOver"))
            .Where(m => m != null && m!.DeclaringType != typeof(Trait))
            .Select(m => m!.DeclaringType!.Name)
            .Distinct().OrderBy(x => x).ToArray();
        Console.WriteLine("| 宣言している型 | 何をしているか | `Carry` で呼ぶか |");
        Console.WriteLine("|---|---|---|");
        foreach (string decl in impl)
        {
            (string what, string why) = decl switch
            {
                "RedirectGainTrait" => ("肩代わりの印（`guardPending`）を落とす", "**呼ぶ**（印は `StatusKeys` に無い。消去とは独立）"),
                "NecroTrait" => ("帳簿を 0 に戻し、層を1つ落として積み直す", "**呼ぶ**（ただし下の (1') の順序が要る）"),
                "ColossusTrait" => ("腹と還しの使用済み印を落とす", "**呼ぶ**（Battle スコープの資源。消去とは独立）"),
                "SplitterTrait" => ("崩れた記録を落とす", "**呼ぶ**（消去とは独立）"),
                "ThornGuardTrait" => ("入れ替えの印と相方を落とす", "**呼ぶ**（消去とは独立）"),
                "ShoveTrait" => ("最後に突き返したターン番号を落とす", "**呼ぶ**（消去とは独立）"),
                "ScapegoatTrait" => ("引き取りの控えを落とす", "**呼ぶ**（業は盤面に居ないので影響ゼロ）"),
                "GoadTrait" => ("標的の記憶（`InstanceId`）を落とす", "**必ず呼ぶ**——`InstanceId` は戦闘ごとに振り直される"),
                "FixateTrait" => ("執着の記憶（`InstanceId`）を落とす", "**必ず呼ぶ**——同上"),
                _ => ("（未分類。**手で分類を足すこと**）", "—"),
            };
            Console.WriteLine($"| `{decl}` | {what} | {why} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**{impl.Length} 型**（`Trait` 本体の空実装を除く）。");
        Console.WriteLine("**うち 2 型（`GoadTrait` / `FixateTrait`）は `InstanceId` を持つ記憶を落としており、");
        Console.WriteLine("呼ばないと前の戦闘の番号が次の戦闘の無関係な駒に当たる**（CLAUDE.md の明文の規則）。");
        Console.WriteLine("**だから `Carry` でも `OnCarryOver` は全部呼ぶ。**");
        Console.WriteLine();
        Console.WriteLine("### (1') 呼ぶと決めた結果、順序が要った（**この期で唯一の非自明な実装判断**）");
        Console.WriteLine();
        Console.WriteLine("`NecroTrait.OnCarryOver` は **「`AtkBonus` はエンジンが一律 0 にした後」を前提に**");
        Console.WriteLine("帳簿（`necroBonus`）を 0 へ戻してから層ぶんを積み直す。");
        Console.WriteLine("**素直に「消さずに呼ぶ」と、層のぶんが二重計上される**（`AtkBonus` = 旧 + 新）");
        Console.WriteLine("——CLAUDE.md が繰り返し警告している「積み上げが発散する」形そのもので、");
        Console.WriteLine("しかも**増えるのは墓守のリィカを含む行＝この期がいちばん見たい行**である。");
        Console.WriteLine();
        Console.WriteLine("> **採った形**: 既存の段（消去・`ResetAtkBonus`・`WhetReceived = 0`・`ActionIndex = 0`・");
        Console.WriteLine("> `OnCarryOver`）を**1文字も変えずにそのまま通し**、その後で控えた値へ戻す");
        Console.WriteLine("> （`ResetAtkBonus(keptBonus)`。帳簿に載せずに書く窓口・第68期）。");
        Console.WriteLine("> これで `AtkBonus` は「消さなかった」値そのものになる。");
        Console.WriteLine();
        Console.WriteLine("**残る差**（報告する）: 層の counter は現行どおり 1 つ落ちるのに、`AtkBonus` は落ちる前の値。");
        Console.WriteLine("次の戦闘で味方が倒れると `desired − applied` の差分だけが載るので**発散はしない**が、");
        Console.WriteLine("**`Carry` は墓守にわずかに甘い**（層1つぶん）。engine が特性私有のキーを触らずに直す手が無い。");
        Console.WriteLine();

        // (2) StatusKeys.All
        Console.WriteLine("### (2) `StatusKeys.All` の全数（実装から引いた）");
        Console.WriteLine();
        Console.WriteLine($"**{StatusKeys.All.Length} 本**: "
            + string.Join(" / ", StatusKeys.All.Select(k => $"{StatusKeys.LabelOf(k)}（`{k}`）")));
        Console.WriteLine();
        Console.WriteLine("`Carry` はこの 9 本を**そのまま持ち越す**——**負も一緒に**。");
        Console.WriteLine("正だけを持ち越すと常に得になり、選択にならない（§0-2）。");
        Console.WriteLine();

        // (3) 併用の扱い
        Console.WriteLine("### (3) `RecoverRule`（第101期）と `BoundaryRule` の併用");
        Console.WriteLine();
        Console.WriteLine($"既定 `BoundaryRule.Default` = `{BoundaryRule.Default.Choice}` "
            + $"→ `Active` = **{BoundaryRule.Default.Active}**。**測定中も既定は `None` のまま動かしていない。**");
        Console.WriteLine();
        Console.WriteLine("**併用しない。** `RecoverRule.Active` が真のときは `BoundaryRule` を**無視する**");
        Console.WriteLine("（`Engagement.Run` の 1 行。コメントに理由を書いた）——どちらも同じ場所（境界の HP と体）を");
        Console.WriteLine("触るので、両方効かせると「どちらが効いたのか」が原理的に割れない。");
        Console.WriteLine($"`RecoverRule.Default` = `({RecoverRule.Default.HpPercent}, "
            + $"{RecoverRule.Default.ReviveDead.ToString().ToLowerInvariant()})` → `Active` = "
            + $"**{RecoverRule.Default.Active}** なので、既定では常に `BoundaryRule` の側が読まれる。");
        Console.WriteLine();

        // (4) 敵側
        Console.WriteLine("### (4) 敵側に境界の規則が走っていないこと");
        Console.WriteLine();
        Console.WriteLine("`Engagement.Run` の `enemyCur = clearedE ? Materialize(...) : CarryOver(aliveE)`");
        Console.WriteLine("——**敵側の `CarryOver` には規則を1つも渡していない**（既定 `None`）。");
        Console.WriteLine("**表G の (d) で実測でも確かめる**（9通り全部で敵の入場戦力が 1 ビットも動かないこと）。");
        Console.WriteLine();

        // (5) docs
        Console.WriteLine("### (5) `docs/engage.md` は既定を変えたときだけ動く");
        Console.WriteLine();
        Console.WriteLine("`engage` は `EngagementEngine.Run` に `boundary` を渡していないので、"
            + "**既定が `None` である限り差分は 0**。");
        Console.WriteLine();

        // (6) 測る列
        bool prefix = bcCol3.Squads.Count <= bcCol5.Squads.Count
            && bcCol3.Squads.Select((sq, i) => ReferenceEquals(sq, bcCol5.Squads[i])).All(x => x);
        Console.WriteLine("### (6) 測る列と方策（実装から引いた。名前で決め打ちしない）");
        Console.WriteLine();
        Console.WriteLine($"**3波の列 = 「{bcCol3.Name}」**（{bcCol3.Squads.Count}波・5波の列「{bcCol5.Name}」の"
            + $"先頭3つと参照同一: **{(prefix ? "○" : "×")}**）。**境界は 2 つ、探索は 3 × 3 = 9 通り。**");
        Console.WriteLine();
        Console.WriteLine($"行は `Presets.Compare` {CompareBuilds().Length} ＋ `Presets.Cross` {CrossBuilds().Length} "
            + $"= **{bcRows.Length}行** × seed 0..{BcSeeds - 1} × **(現行 ＋ 9 通り)** "
            + $"= **{bcRows.Length * BcSeeds * 10:N0} 会戦**。");
        Console.WriteLine();
        Console.WriteLine("**「最良」の定義（測る前に固定）**: 突破波数が最大 → 同値なら残存が最大 → "
            + "同値なら先に評価した順（蘇生 → 回復 → 持ち越し）。");
        Console.WriteLine("**死者が0体の境界では蘇生を選べない**——候補から外し、その境界を「選択肢2つ」として分母を分けて数える。");
        Console.WriteLine();
        Console.WriteLine("### (7) 規約 (G10)（**測定より先にコミットしてある**）");
        Console.WriteLine();
        Console.WriteLine("> **判定に使う分母は第2〜3波（地点）とする。第一波を分母に入れない。**");
        Console.WriteLine();
        Console.WriteLine("地点の会戦では**第一波で落ちる試行が 0**（第100・101期の実測）なので、"
            + "`突破率` は定義上「第2〜3波を抜けた割合」であり、そのまま (G10) を満たす。");
        Console.WriteLine();
    }

    if (bcSub == "phase0")
    {
        Console.WriteLine("# 第102期 —— 境界に選択を置く（`choice phase0`）");
        Console.WriteLine();
        BcEmitA();
        return;
    }

    // ---------------- 測定 ----------------
    Console.WriteLine("# 第102期 —— 境界に選択を置く（蘇生・回復・持ち越しから1つ）");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 choice {bcSub}` の出力。");
    Console.WriteLine("**触ったのは会戦の境界の規則 1 つ（`BoundaryRule`）だけ。** 駒・特性・数値・戦闘中の判定・");
    Console.WriteLine("`Presets` / `CompareBuilds()` / `CrossBuilds()` / `Stages` / `Columns` は 1 行も触っていない。");
    Console.WriteLine("**既定は `None` のまま。この期は測って並べるところまでで、採否は決めない。**");
    Console.WriteLine("**`docs/` には置かない。**");
    Console.WriteLine();
    BcEmitA();
    Console.Out.Flush();

    int nw = bcCol3.Squads.Count;               // 3
    int nb = nw - 1;                            // 境界の数 = 2
    int bcCells = bcRows.Length * BcSeeds;

    // 方策: 0 現行 / 1 常に蘇生 / 2 常に回復 / 3 常に持ち越し / 4 最良
    var polName = new[] { "現行（`None`）", "常に蘇生", "常に回復", "常に持ち越し", "**最良（上限）**" };
    const int NP = 5, BEST = 4;

    // 9 通りの計画（蘇生 → 回復 → 持ち越し の順で列挙する＝同値の tie-break の順）
    var plans = (from a in bcChoices from b in bcChoices select new[] { a, b }).ToArray();
    int idxRevive = Array.FindIndex(plans, x => x[0] == BoundaryChoice.Revive && x[1] == BoundaryChoice.Revive);
    int idxHeal = Array.FindIndex(plans, x => x[0] == BoundaryChoice.Heal && x[1] == BoundaryChoice.Heal);
    int idxCarry = Array.FindIndex(plans, x => x[0] == BoundaryChoice.Carry && x[1] == BoundaryChoice.Carry);

    // 集計器
    var pWin = new int[NP][]; var pSurvRow = new double[NP][];
    var pFell = new int[NP][]; var pSurvSum = new double[NP]; var pBlow = new int[NP]; var pNarrow = new int[NP];
    var pEntAlive = new double[NP][]; var pEntHp = new double[NP][]; var pEntReach = new int[NP][];
    for (int p = 0; p < NP; p++)
    {
        pWin[p] = new int[bcRows.Length]; pSurvRow[p] = new double[bcRows.Length];
        pFell[p] = new int[nw + 2];
        pEntAlive[p] = new double[nw]; pEntHp[p] = new double[nw]; pEntReach[p] = new int[nw];
    }
    // 選択の分布 [境界][選択] と分母 [境界]（選択肢3つ / 2つ）
    var pick = new int[nb][]; var denom3 = new int[nb]; var denom2 = new int[nb];
    for (int b = 0; b < nb; b++) pick[b] = new int[bcChoices.Length];
    // Carry が災厄になった回数（proxy）: 常に持ち越し の突破波数 が 現行 を下回った試行
    int carryWorse = 0, carryBetter = 0;
    int enemyMismatch = 0;                          // 自己検査 (d)
    // 行ごとの「最良」で拾われた選択（第1境界のみ。育つ駒 / ヴェルの内訳用）
    var rowPick = new int[bcRows.Length][];
    for (int i = 0; i < bcRows.Length; i++) rowPick[i] = new int[bcChoices.Length];

    var bcLock = new object();

    Parallel.For(0, bcRows.Length, i =>
    {
        var lWin = new int[NP]; var lSurv = new double[NP]; var lBlow = new int[NP]; var lNarrow = new int[NP];
        var lFell = new int[NP][]; var lEA = new double[NP][]; var lEH = new double[NP][]; var lER = new int[NP][];
        for (int p = 0; p < NP; p++) { lFell[p] = new int[nw + 2]; lEA[p] = new double[nw]; lEH[p] = new double[nw]; lER[p] = new int[nw]; }
        var lPick = new int[nb][]; var lD3 = new int[nb]; var lD2 = new int[nb];
        for (int b = 0; b < nb; b++) lPick[b] = new int[bcChoices.Length];
        var lRowPick = new int[bcChoices.Length];
        int lCarryWorse = 0, lCarryBetter = 0, lEnemyBad = 0;

        Formation f = bcRows[i].F;
        var column = new[] { f };
        int defTotal = f.Occupied().Sum(x => x.Def.MaxHp);

        var res = new EngagementResult[plans.Length];
        var pol = new EngagementResult[NP];

        for (int seed = 0; seed < BcSeeds; seed++)
        {
            for (int k = 0; k < plans.Length; k++)
                res[k] = EngagementEngine.Run(column, bcCol3.Squads, seed, verbose: false,
                    boundary: new BoundaryRule(BoundaryChoice.None, plans[k]));
            EngagementResult none = EngagementEngine.Run(column, bcCol3.Squads, seed, verbose: false);

            // 自己検査 (d): 敵側は版で1ビットも動かない
            for (int k = 0; k < plans.Length; k++)
            {
                var a = none.EnemyEntries; var b2 = res[k].EnemyEntries;
                for (int t = 0; t < Math.Min(a.Count, b2.Count); t++) if (!a[t].Equals(b2[t])) lEnemyBad++;
            }

            // 合法性: 死者が0体の境界では蘇生を選べない
            bool Legal(int k)
            {
                EngagementResult r = res[k];
                for (int b = 0; b < nb && b + 1 < r.Battles.Count; b++)
                {
                    if (plans[k][b] != BoundaryChoice.Revive) continue;
                    if (r.PlayerEntries[b].Alive - r.PlayerExits[b].Alive <= 0) return false;
                }
                return true;
            }

            int best = -1;
            for (int k = 0; k < plans.Length; k++)
            {
                if (!Legal(k)) continue;
                if (best < 0) { best = k; continue; }
                int c1 = res[k].EnemySquadsCleared, c0 = res[best].EnemySquadsCleared;
                if (c1 > c0 || (c1 == c0 && res[k].PlayerExits[^1].Alive > res[best].PlayerExits[^1].Alive)) best = k;
            }
            if (best < 0) best = idxHeal;   // 起こらないはず（回復だけの計画は必ず合法）

            // 選択の分布と分母（「最良」の計画が実際に渡った境界だけ数える）
            for (int b = 0; b < nb && b + 1 < res[best].Battles.Count; b++)
            {
                int deaths = res[best].PlayerEntries[b].Alive - res[best].PlayerExits[b].Alive;
                if (deaths > 0) lD3[b]++; else lD2[b]++;
                int ci = Array.IndexOf(bcChoices, plans[best][b]);
                if (ci >= 0) { lPick[b][ci]++; if (b == 0) lRowPick[ci]++; }
            }

            pol[0] = none; pol[1] = res[idxRevive]; pol[2] = res[idxHeal]; pol[3] = res[idxCarry]; pol[BEST] = res[best];

            if (pol[3].EnemySquadsCleared < pol[0].EnemySquadsCleared) lCarryWorse++;
            if (pol[3].EnemySquadsCleared > pol[0].EnemySquadsCleared) lCarryBetter++;

            for (int p = 0; p < NP; p++)
            {
                EngagementResult r = pol[p];
                if (r.PlayerWon)
                {
                    lWin[p]++;
                    BattleResult last = r.Battles[^1];
                    lSurv[p] += last.PlayerSurvivors;
                    if (last.PlayerSurvivors >= 4) lBlow[p]++;
                    if (last.PlayerSurvivors <= 1) lNarrow[p]++;
                }
                else lFell[p][Math.Min(r.Battles.Count, nw + 1)]++;
                for (int b = 0; b < r.PlayerEntries.Count && b < nw; b++)
                {
                    lEA[p][b] += r.PlayerEntries[b].Alive;
                    lEH[p][b] += (double)r.PlayerEntries[b].HpSum / defTotal;
                    lER[p][b]++;
                }
            }
        }

        lock (bcLock)
        {
            for (int p = 0; p < NP; p++)
            {
                pWin[p][i] = lWin[p];
                pSurvRow[p][i] = lWin[p] > 0 ? lSurv[p] / lWin[p] : 0;
                pSurvSum[p] += lSurv[p]; pBlow[p] += lBlow[p]; pNarrow[p] += lNarrow[p];
                for (int k = 0; k < lFell[p].Length; k++) pFell[p][k] += lFell[p][k];
                for (int b = 0; b < nw; b++)
                { pEntAlive[p][b] += lEA[p][b]; pEntHp[p][b] += lEH[p][b]; pEntReach[p][b] += lER[p][b]; }
            }
            for (int b = 0; b < nb; b++)
            {
                for (int c = 0; c < bcChoices.Length; c++) pick[b][c] += lPick[b][c];
                denom3[b] += lD3[b]; denom2[b] += lD2[b];
            }
            carryWorse += lCarryWorse; carryBetter += lCarryBetter; enemyMismatch += lEnemyBad;
            for (int c = 0; c < bcChoices.Length; c++) rowPick[i][c] = lRowPick[c];
        }
    });
    Console.Error.WriteLine("[choice] measured");

    static double BcMedian(double[] v)
    {
        var s = v.OrderBy(x => x).ToArray();
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2.0;
    }
    var polRate = new double[NP][];
    for (int p = 0; p < NP; p++) polRate[p] = pWin[p].Select(w => w * 100.0 / BcSeeds).ToArray();
    var polTotal = new int[NP];
    for (int p = 0; p < NP; p++) polTotal[p] = pWin[p].Sum();

    // ---------------- 表B ----------------
    Console.WriteLine($"## 表B —— 方策ごとの集計（「{bcCol3.Name}」{nw}波 × {bcRows.Length}行 × seed 0..{BcSeeds - 1}）");
    Console.WriteLine();
    Console.WriteLine($"`突破率` はその列を最後まで抜けた試行の割合（全 {bcCells:N0} 試行）。");
    Console.WriteLine($"`中央値` は**行ごとの突破率**の中央値（{bcRows.Length}行）。`0%の行` は 1 試行も抜けなかった行の数。");
    Console.WriteLine();
    Console.WriteLine("| 方策 | 突破率 | 中央値 | 0%の行 | 100%の行 | 抜けた行 | 落ちた波(第1/第2/第3) | 第2波クリア率 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|---|--:|");
    for (int p = 0; p < NP; p++)
    {
        int zero = polRate[p].Count(x => x == 0), full = polRate[p].Count(x => x >= 100);
        int broke = polRate[p].Count(x => x > 0);
        double clear2 = (bcCells - pFell[p][1] - pFell[p][2]) * 100.0 / bcCells;
        Console.WriteLine($"| **{polName[p]}** | {polTotal[p] * 100.0 / bcCells:F2}% | {BcMedian(polRate[p]):F1}% "
            + $"| {zero} | {full} | {broke} | "
            + string.Join(" / ", Enumerable.Range(1, nw).Select(k => $"{pFell[p][k]}")) + $" | {clear2:F1}% |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表C ----------------
    Console.WriteLine($"## 表C —— 行ごとの突破率（{bcRows.Length}行 × {NP}方策）");
    Console.WriteLine();
    Console.WriteLine("並びは「最良」の降順。`最良−固定最良` は Q3（選ばせる意味があるか）の材料。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | " + string.Join(" | ", polName) + " | 最良−固定最良 |");
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, NP).Select(_ => "--:|")) + "--:|");
    var ordC = Enumerable.Range(0, bcRows.Length)
        .OrderByDescending(i => polRate[BEST][i]).ThenByDescending(i => polRate[2][i]).ToArray();
    foreach (int i in ordC)
    {
        double fixedBest = Math.Max(polRate[1][i], Math.Max(polRate[2][i], polRate[3][i]));
        Console.WriteLine($"| {bcRows[i].Name} | "
            + string.Join(" | ", Enumerable.Range(0, NP).Select(p => $"{polRate[p][i]:F1}%"))
            + $" | {polRate[BEST][i] - fixedBest:+0.0;-0.0}pt |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表D（Q1 の本体） ----------------
    Console.WriteLine("## 表D —— 各境界で最良だった選択肢の分布（**この期の主判定**）");
    Console.WriteLine();
    Console.WriteLine("「最良」の計画が**実際に渡った境界**だけを数える（渡らなかった境界は選択が存在しない）。");
    Console.WriteLine("`選択肢3つ` は死者が1体以上いた境界、`選択肢2つ` は死者が0体で**蘇生を選べなかった**境界。");
    Console.WriteLine();
    Console.WriteLine("| 境界 | 回数 | 選択肢3つ | 選択肢2つ | " + string.Join(" | ", bcChoices.Select(BcLabel)) + " | 最大の偏り |");
    Console.WriteLine("|---|--:|--:|--:|" + string.Concat(bcChoices.Select(_ => "--:|")) + "--:|");
    var skew = new double[nb];
    for (int b = 0; b < nb; b++)
    {
        int tot = pick[b].Sum();
        skew[b] = tot == 0 ? 0 : pick[b].Max() * 100.0 / tot;
        Console.WriteLine($"| 第{b + 1}戦の後 | {tot} | {denom3[b]} | {denom2[b]} | "
            + string.Join(" | ", pick[b].Select(v => tot == 0 ? "—" : $"{v} ({v * 100.0 / tot:F1}%)"))
            + $" | {skew[b]:F1}% |");
    }
    int allTot = Enumerable.Range(0, nb).Sum(b => pick[b].Sum());
    var allPick = Enumerable.Range(0, bcChoices.Length).Select(c => Enumerable.Range(0, nb).Sum(b => pick[b][c])).ToArray();
    double skewAll = allTot == 0 ? 0 : allPick.Max() * 100.0 / allTot;
    Console.WriteLine($"| **合計** | {allTot} | {denom3.Sum()} | {denom2.Sum()} | "
        + string.Join(" | ", allPick.Select(v => allTot == 0 ? "—" : $"{v} ({v * 100.0 / allTot:F1}%)"))
        + $" | **{skewAll:F1}%** |");
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表E（判定） ----------------
    Console.WriteLine("## 表E —— 判定 Q1〜Q3（**線は測る前に固定してある**）");
    Console.WriteLine();
    Console.WriteLine($"- **Q1（選択が成立しているか・主判定）**: 最良だった選択肢の分布が"
        + $"**どれか1つに {Q1SkewMax:F0}% 以上偏らない**こと");
    Console.WriteLine($"- **Q2（帯）**: 「最良」の突破率の**中央値が {Q2MedianLo:F0}〜{Q2MedianHi:F0}%** に入り、"
        + $"**突破率 0% の行が {Q2ZeroMax} 行を下回る**こと");
    Console.WriteLine($"- **Q3（選択肢の価値）**: 「最良」と固定3つの最良との差が **{Q3GapLine:F1}pt 以上**あること");
    Console.WriteLine();
    double medBest = BcMedian(polRate[BEST]);
    int zeroBest = polRate[BEST].Count(x => x == 0);
    double fixedBestTotal = Math.Max(polTotal[1], Math.Max(polTotal[2], polTotal[3])) * 100.0 / bcCells;
    double bestTotal = polTotal[BEST] * 100.0 / bcCells;
    bool q1 = skewAll < Q1SkewMax;
    bool q2 = medBest >= Q2MedianLo && medBest <= Q2MedianHi && zeroBest < Q2ZeroMax;
    bool q3 = bestTotal - fixedBestTotal >= Q3GapLine;
    Console.WriteLine("| 判定 | 実測 | 線 | 判定 |");
    Console.WriteLine("|---|--:|--:|:-:|");
    Console.WriteLine($"| **Q1** 最大の偏り | {skewAll:F1}% | < {Q1SkewMax:F0}% | {(q1 ? "**○**" : "**×**")} |");
    Console.WriteLine($"| Q2 中央値 | {medBest:F1}% | {Q2MedianLo:F0}〜{Q2MedianHi:F0}% | {(medBest >= Q2MedianLo && medBest <= Q2MedianHi ? "○" : "×")} |");
    Console.WriteLine($"| Q2 0%の行 | {zeroBest} | < {Q2ZeroMax} | {(zeroBest < Q2ZeroMax ? "○" : "×")} |");
    Console.WriteLine($"| Q3 最良−固定最良 | {bestTotal - fixedBestTotal:+0.00;-0.00}pt | ≥ {Q3GapLine:F1}pt | {(q3 ? "○" : "×")} |");
    Console.WriteLine();
    Console.WriteLine($"**総合: {(q1 && q2 && q3 ? "○" : "×")}**（主判定は Q1）。");
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表F（あわせて見るもの・判定ではない） ----------------
    Console.WriteLine("## 表F —— あわせて見るもの（**判定ではない**）");
    Console.WriteLine();
    Console.WriteLine("### F-1. 勝ち方の質（規約 (G11)(G12)）");
    Console.WriteLine();
    Console.WriteLine("**比べる相手は現行ではなく第101期の `R100`**（規約 (G11)。現行の圧勝率は下限に張り付いていて");
    Console.WriteLine("下回りようが無い）。**抜けた試行数を必ず併記する**（規約 (G12)。分母が版で動く）。");
    Console.WriteLine();
    Console.WriteLine("| 方策 | 抜けた試行 | 残存 | 圧勝率 | 全滅勝ち |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int p = 0; p < NP; p++)
        Console.WriteLine($"| **{polName[p]}** | {polTotal[p]} | "
            + (polTotal[p] == 0 ? "— | — | — |"
               : $"{pSurvSum[p] / polTotal[p]:F2} | {pBlow[p] * 100.0 / polTotal[p]:F0}% | {pNarrow[p] * 100.0 / polTotal[p]:F0}% |"));
    Console.WriteLine();

    Console.WriteLine("### F-2. 入場戦力");
    Console.WriteLine();
    Console.WriteLine("各波の開始時点の生存数と HP 合計の割合（**分母は編成全体の定義上総最大HP**・不変値）。");
    Console.WriteLine("到達しなかった試行は分母から外し、到達率を括弧で併記する。");
    Console.WriteLine();
    Console.WriteLine("| 方策 |" + string.Concat(Enumerable.Range(0, nw).Select(b => $" 第{b + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "---|")));
    for (int p = 0; p < NP; p++)
        Console.WriteLine($"| **{polName[p]}** |" + string.Concat(Enumerable.Range(0, nw).Select(b =>
            pEntReach[p][b] == 0 ? " — |"
            : $" {pEntAlive[p][b] / pEntReach[p][b]:F2}体 {pEntHp[p][b] * 100 / pEntReach[p][b]:F0}%"
              + $" (到達 {pEntReach[p][b] * 100.0 / bcCells:F0}%) |")));
    Console.WriteLine();

    Console.WriteLine("### F-3. 同値塊（規約 (G13)）");
    Console.WriteLine();
    Console.WriteLine("**同値塊の割合が高い版では順位相関を判定に使わない**（第101期は 46/73 行が 0% で");
    Console.WriteLine("スピアマンとピアソンが逆を言った）。この期は順位相関を1つも判定に使っていない。");
    Console.WriteLine();
    Console.WriteLine("| 方策 | 0% の行 | 100% の行 | 同値塊の割合 |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int p = 0; p < NP; p++)
    {
        int z = polRate[p].Count(x => x == 0), fl = polRate[p].Count(x => x >= 100);
        Console.WriteLine($"| **{polName[p]}** | {z} | {fl} | {(z + fl) * 100.0 / bcRows.Length:F1}% |");
    }
    Console.WriteLine();

    Console.WriteLine("### F-4. 育つ駒は選択で救われるか");
    Console.WriteLine();
    Console.WriteLine("対象は**駒で引く**（行名ではない）: 泥人形ムド（被弾強化）・棘鎧のカド（棘）・");
    Console.WriteLine("軋みのヨミ（移動で育つ）・墓守のリィカ（層）。**`Carry` が最良になる割合**を見る。");
    Console.WriteLine();
    var growIds = new[] { UnitCatalog.Mudo.Id, UnitCatalog.Kado.Id, UnitCatalog.Yomi.Id, UnitCatalog.Rica.Id };
    var isGrow = bcRows.Select(r => r.F.Occupied().Any(u => growIds.Contains(u.Def.Id))).ToArray();
    var velIds = new[] { UnitCatalog.Vel.Id };
    var isVel = bcRows.Select(r => r.F.Occupied().Any(u => velIds.Contains(u.Def.Id))).ToArray();

    void BcGroup(string label, bool[] flag)
    {
        int inT = Enumerable.Range(0, bcRows.Length).Where(i => flag[i]).Sum(i => rowPick[i].Sum());
        int outT = Enumerable.Range(0, bcRows.Length).Where(i => !flag[i]).Sum(i => rowPick[i].Sum());
        string Cell(bool sel, int c)
        {
            int t = Enumerable.Range(0, bcRows.Length).Where(i => flag[i] == sel).Sum(i => rowPick[i].Sum());
            int v = Enumerable.Range(0, bcRows.Length).Where(i => flag[i] == sel).Sum(i => rowPick[i][c]);
            return t == 0 ? "—" : $"{v * 100.0 / t:F1}%";
        }
        Console.WriteLine($"| {label}・含む ({flag.Count(x => x)}行) | {inT} | "
            + string.Join(" | ", Enumerable.Range(0, bcChoices.Length).Select(c => Cell(true, c))) + " |");
        Console.WriteLine($"| {label}・含まない ({flag.Count(x => !x)}行) | {outT} | "
            + string.Join(" | ", Enumerable.Range(0, bcChoices.Length).Select(c => Cell(false, c))) + " |");
    }
    Console.WriteLine("| 群 | 第1境界の回数 | " + string.Join(" | ", bcChoices.Select(BcLabel)) + " |");
    Console.WriteLine("|---|--:|" + string.Concat(bcChoices.Select(_ => "--:|")));
    BcGroup("育つ駒", isGrow);
    BcGroup("ヴェル", isVel);
    Console.WriteLine();
    Console.WriteLine("**ヴェルとの競合**: 境界の蘇生が無料だと、自分の `MaxHp` を半分払って蘇生する駒の意義が薄れる。");
    Console.WriteLine("ヴェルを含む行で蘇生が選ばれる割合が、含まない行より**低ければ**競合していない証拠になる。");
    Console.WriteLine();

    Console.WriteLine("### F-5. `Carry` は災厄になるか（可変コスト型として働いているか）");
    Console.WriteLine();
    Console.WriteLine("**proxy**: 「常に持ち越し」の突破波数が「現行」を**下回った**試行を数える。");
    Console.WriteLine("負の状態異常だけを取り出して帰属させる器具は無いので、これは代理指標である（そう書く）。");
    Console.WriteLine();
    Console.WriteLine($"- **現行より悪くなった試行: {carryWorse} / {bcCells:N0}（{carryWorse * 100.0 / bcCells:F2}%）**");
    Console.WriteLine($"- 現行より良くなった試行: {carryBetter} / {bcCells:N0}（{carryBetter * 100.0 / bcCells:F2}%）");
    Console.WriteLine();
    Console.WriteLine("**両方が 0 なら `Carry` は盤面を1ビットも動かしていない**（持ち越すものが無い）。");
    Console.WriteLine("**悪くなる側が 0 なら「常に得」で、選択にならない**（§0-2 の懸念がそのまま出た形）。");
    Console.WriteLine();
    Console.Out.Flush();

    // ---------------- 表G（受け入れ・自己検査） ----------------
    Console.WriteLine("## 表G —— 受け入れ（自己検査）");
    Console.WriteLine();
    const int P100Fell2 = 9669, P100Fell3 = 4790, P100Broke = 141, P100Rows = 1;
    const int P101HealBroke = 848;      // 第101期 R100 の 5.81% = 848 / 14,600
    const int P101HealZero = 46;
    bool acc2 = pFell[0][2] == P100Fell2 && pFell[0][3] == P100Fell3
        && polTotal[0] == P100Broke && polRate[0].Count(x => x > 0) == P100Rows;
    Console.WriteLine("### (a) `None` が第100・101期の `R0` を再現するか");
    Console.WriteLine();
    Console.WriteLine("**既知の値を再現できて初めて器具として使える**（第94期）。");
    Console.WriteLine();
    Console.WriteLine("| 量 | 第100・101期 | この期の `None` | 一致 |");
    Console.WriteLine("|---|--:|--:|:-:|");
    Console.WriteLine($"| 突破した試行 | {P100Broke} | {polTotal[0]} | {(polTotal[0] == P100Broke ? "○" : "×")} |");
    Console.WriteLine($"| 突破率 | 0.97% | {polTotal[0] * 100.0 / bcCells:F2}% | {(polTotal[0] == P100Broke ? "○" : "×")} |");
    Console.WriteLine($"| 1試行でも抜けた行 | {P100Rows} | {polRate[0].Count(x => x > 0)} | {(polRate[0].Count(x => x > 0) == P100Rows ? "○" : "×")} |");
    Console.WriteLine($"| 第2波で落ちた試行 | {P100Fell2} | {pFell[0][2]} | {(pFell[0][2] == P100Fell2 ? "○" : "×")} |");
    Console.WriteLine($"| 第3波で落ちた試行 | {P100Fell3} | {pFell[0][3]} | {(pFell[0][3] == P100Fell3 ? "○" : "×")} |");
    Console.WriteLine();
    Console.WriteLine($"→ **{(acc2 ? "○ 再現した" : "× 再現しない")}**");
    Console.WriteLine();

    Console.WriteLine("### (b) 「常に回復」が第101期の `R100` と一致するか");
    Console.WriteLine();
    Console.WriteLine("`BoundaryChoice.Heal` は `RecoverRule(100, false)` と**同じ処理のはず**"
        + "（生存者を `MaxHp` まで戻し、死者は戻さない）。**一致しなければ実装が違う。**");
    Console.WriteLine();
    int healZero = polRate[2].Count(x => x == 0);
    Console.WriteLine("| 量 | 第101期 `R100` | この期の「常に回復」 | 一致 |");
    Console.WriteLine("|---|--:|--:|:-:|");
    Console.WriteLine($"| 突破率 | 5.81% | {polTotal[2] * 100.0 / bcCells:F2}% | {(polTotal[2] == P101HealBroke ? "○" : "×")} |");
    Console.WriteLine($"| 突破した試行 | {P101HealBroke} | {polTotal[2]} | {(polTotal[2] == P101HealBroke ? "○" : "×")} |");
    Console.WriteLine($"| 0%の行 | {P101HealZero} | {healZero} | {(healZero == P101HealZero ? "○" : "×")} |");
    Console.WriteLine($"| 第2波クリア率 | 57.5% | {(bcCells - pFell[2][1] - pFell[2][2]) * 100.0 / bcCells:F1}% | "
        + $"{(Math.Abs((bcCells - pFell[2][1] - pFell[2][2]) * 100.0 / bcCells - 57.5) < 0.05 ? "○" : "×")} |");
    Console.WriteLine();

    Console.WriteLine("### (c) 「最良」は固定3つのどれ以上か（上限であることの検算）");
    Console.WriteLine();
    bool upper = Enumerable.Range(0, bcRows.Length)
        .All(i => polRate[BEST][i] >= Math.Max(polRate[1][i], Math.Max(polRate[2][i], polRate[3][i])) - 1e-9);
    Console.WriteLine($"全 {bcRows.Length} 行で「最良」≥ 固定3つの最大: **{(upper ? "○" : "×")}**");
    Console.WriteLine("（**行ごとの突破率では成り立つはずだが、試行ごとの上限であることの含意ではない**"
        + "——蘇生が非合法な境界を落としているので、行の値が固定版を下回りうる。下回ったらそう書く。）");
    Console.WriteLine();

    Console.WriteLine("### (d) 敵側に境界の規則が漏れていないこと");
    Console.WriteLine();
    Console.WriteLine($"9 通り × 全試行で、敵の入場戦力（`EnemyEntries`）が `None` と違ったセル: "
        + $"**{enemyMismatch} 件**{(enemyMismatch == 0 ? "（○）" : "（**×**）")}");
    Console.WriteLine();

    Console.WriteLine("### (e)（この実行の外で確かめるもの）");
    Console.WriteLine();
    Console.WriteLine("| # | 受け入れ | 確かめ方 |");
    Console.WriteLine("|--:|---|---|");
    Console.WriteLine("| 1 | `compare` 305 セルが `docs/balance.md` と 0 件 | **単発は境界を1度も通らない**ので原理的に立たない。実測でも確認する |");
    Console.WriteLine("| 4 | `docs/` を再生成して差分を報告 | 既定は `None` のままなので `engage.md` は差分0。`rules.md` は `BoundaryRule` が増えるぶん動く |");
    Console.WriteLine("| 5 | `ctx.PickOne` の箇所数が第101期と同数 | `grep -c` で数える（この期は 1 つも足していない） |");
    Console.WriteLine("| 6 | 境界で `ctx.Heal` を呼んでいない | `Engagement.cs` に `Heal` の呼び出しが 0 箇所 |");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}
}
