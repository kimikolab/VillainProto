using BattleCore;
using static Common;

// =====================================================================================
// run モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "run")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 run
// =====================================================================================

static class RunDiag
{
// run モード: 会戦の指標と「勝ち方の質」を測る（第100期・調査）。
//
// 96期のあいだ測っていなかったものが2つある。
//   (1) 会戦の強さ。compare 61行は各波を**独立に**測っている（第98期の実測では
//       単発で5波全勝が 12/73・会戦突破が 0/73 で、順位すら一致しない）。**別の物差し**である。
//   (2) 勝ち方の質。chain が持つ `残存` / `全滅勝ち` は、測ってあったのに 96期のあいだ
//       一度も判定に使われていない。
//
// **盤面は1ビットも動かさない。** 駒・特性・数値・engine の規則には触れていない。
// **既存の診断も1文字も書き換えていない**——`chain` の定義は**写した**。写した箇所は
// C-1 の見出しに明記してあり、`run solo` は自分の値を `docs/chain.md` と突き合わせて検算する
// （受け入れ4。新しい器具は既知の値を再現できて初めて使える・第94期）。
//
// **この期は目標値を決めない。**「残存が何体なら良いか」を先に決めず、まず分布を見る。
//
//     dotnet run --project BattleSim -c Release 0 run phase0        # 表A ＋ Phase 0（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 run engage <col>  # 表B・D（col = 5 / 3）
//     dotnet run --project BattleSim -c Release 0 run solo          # 表C（＋ chain の再現検算）
//     dotnet run --project BattleSim -c Release 0 run tables        # 表A〜E を通しで
public static void Run(string[] args, int stageIndex)
{
    string focusId = args.Length > 1 ? args[1] : "";

    string sub = args.Length > 2 ? args[2] : "tables";
    const int RunSeeds = 200;

    // 73行 = compare 61 ＋ 交差帯 12。どちらも第97期に BattleCore へ移してあるので、
    // 同じ呼び出しで引ける（Phase 0 の (3)）。行名の重複が無いことは phase0 が数える。
    var runRows = CompareBuilds().Concat(CrossBuilds()).ToArray();

    // 列は**実装から引く**（指示書の記述を信用しない。手作り表のずれが第98期で7例目）。
    // 5波の列は EngagementColumn がいま返しているものを**参照同一**で特定し、
    // 3波の列は長さ3の列を取る。**名前で決め打ちしない**——名前で引くと「実装から引く」が
    // 「実装に書いてある文字列を信用する」に化ける。
    EnemyCatalog.Column runCol5 = EnemyCatalog.Columns
        .First(c => ReferenceEquals(c.Squads, EnemyCatalog.EngagementColumn));
    EnemyCatalog.Column runCol3 = EnemyCatalog.Columns
        .Where(c => c.Squads.Count == 3).OrderBy(c => c.Name).First();

    // ---- 会戦の1行ぶんの計測 ----
    //
    // 味方部隊は**1つだけ**（Phase 0 の (4)。EngagementEngine.Run は playerSquads[pi] を
    // 順に投入するので、長さ1を渡せば1敗で会戦が終わる）。
    //
    // `突破波数` は指示書の定義どおり「**落ちた波 − 1**」。抜け切った試行は列長とする。
    // EnemySquadsCleared との差は相打ち（最終戦で両軍全滅）のときだけ出るので、検算として併記する。
    (int Win, double BrokeSum, double ClearedSum, int[] FellAt,
     double[] AliveSum, double[] HpSum, int[] Reached,
     int Wins, double SurvSum, int Blow, int Narrow, double TurnSum, int Party)
        RunEngage(Formation f, EnemyCatalog.Column col)
    {
        int n = col.Squads.Count;
        var playerColumn = new[] { f };
        // HP割合の分母は**編成全体の定義上総最大HP**（不変値）。engage と同じ判断で、
        // SquadEntry.DefMaxHpSum は使わない（死んだ駒が分子と分母から一緒に抜けて、
        // % が「部隊の残存戦力」ではなく「生き残りの健康度」に化ける）。
        int defTotal = f.Occupied().Sum(x => x.Def.MaxHp);
        int party = f.Occupied().Count();

        int win = 0;
        double brokeSum = 0, clearedSum = 0;
        var fellAt = new int[n + 2];
        var aliveSum = new double[n];
        var hpSum = new double[n];
        var reached = new int[n];
        int wins = 0, blow = 0, narrow = 0;
        double survSum = 0, turnSum = 0;

        for (int seed = 0; seed < RunSeeds; seed++)
        {
            EngagementResult r = EngagementEngine.Run(playerColumn, col.Squads, seed, verbose: false);
            clearedSum += r.EnemySquadsCleared;
            int battles = r.Battles.Count;
            if (r.PlayerWon)
            {
                win++;
                brokeSum += n;
                // 会戦の勝ち方の質は**最後の波を勝ったときの生存数**（指示書 A-1b）。
                BattleResult last = r.Battles[battles - 1];
                wins++;
                survSum += last.PlayerSurvivors;
                if (last.PlayerSurvivors >= 4) blow++;
                if (last.PlayerSurvivors <= 1) narrow++;
                turnSum += r.Battles.Sum(b => b.Turns);
            }
            else
            {
                brokeSum += battles - 1;
                fellAt[Math.Min(battles, n + 1)]++;
            }

            for (int b = 0; b < r.PlayerEntries.Count && b < n; b++)
            {
                aliveSum[b] += r.PlayerEntries[b].Alive;
                hpSum[b] += (double)r.PlayerEntries[b].HpSum / defTotal;
                reached[b]++;
            }
        }
        return (win, brokeSum, clearedSum, fellAt, aliveSum, hpSum, reached,
                wins, survSum, blow, narrow, turnSum, party);
    }

    // ---- 単発の1行ぶんの計測（波ごと） ----
    //
    // **`chain` から写した定義**（`残存` = 勝った試行の平均生存数 / `全滅勝ち` = 生存1体で
    // 勝った試行の割合 / `決着T` = 勝った試行の平均ターン数）。写し元は Program.cs の
    // `focusId == "chain"` の集計ループ。**`chain` は1文字も書き換えていない。**
    // 圧勝率（生存4体以上）だけがこの期の新しい列。
    (int Trials, int Wins, double SurvSum, int Blow, int Narrow, double TurnSum, int Party)[]
        RunSolo(Formation f)
    {
        var stages = EnemyCatalog.Stages;
        var acc = new (int, int, double, int, int, double, int)[stages.Count];
        int party = f.Occupied().Count();
        for (int w = 0; w < stages.Count; w++)
        {
            int trials = 0, wins = 0, blow = 0, narrow = 0;
            double survSum = 0, turnSum = 0;
            for (int seed = 0; seed < RunSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, stages[w].Enemy, seed, verbose: false);
                trials++;
                if (!r.PlayerWon) continue;
                wins++;
                turnSum += r.Turns;
                survSum += r.PlayerSurvivors;
                if (r.PlayerSurvivors >= 4) blow++;
                if (r.PlayerSurvivors <= 1) narrow++;
            }
            acc[w] = (trials, wins, survSum, blow, narrow, turnSum, party);
        }
        return acc;
    }

    // ---------------- 表A（Phase 0・戦闘0回） ----------------
    void EmitTableA()
    {
        Console.WriteLine("## 表A —— `EnemyCatalog.Columns` の全数（Phase 0・**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("**実装から引いた**（指示書の記述は使っていない）。`使用` は、その列が");
        Console.WriteLine("`EnemyCatalog.EngagementColumn`（GodotApp と会戦の既定）として使われているか。");
        Console.WriteLine();
        Console.WriteLine("| # | 名前 | 波数 | 中身（敵の波） | 使用 | メモ |");
        Console.WriteLine("|--:|---|--:|---|:-:|---|");
        for (int i = 0; i < EnemyCatalog.Columns.Count; i++)
        {
            EnemyCatalog.Column c = EnemyCatalog.Columns[i];
            // 中身は Stages と**参照同一**で照合して波の名前で出す。
            string body = string.Join(" → ", c.Squads.Select(sq =>
            {
                EnemyCatalog.Stage? st = EnemyCatalog.Stages.FirstOrDefault(x => ReferenceEquals(x.Enemy, sq));
                return st is null ? "?" : st.Name;
            }));
            bool used = ReferenceEquals(c.Squads, EnemyCatalog.EngagementColumn);
            Console.WriteLine($"| {i} | **{c.Name}** | {c.Squads.Count} | {body} | {(used ? "**○**" : "—")} | {c.Note} |");
        }
        Console.WriteLine();
        Console.WriteLine($"この期に測る2本: **5波の列 = 「{runCol5.Name}」**（{runCol5.Squads.Count}波）／"
            + $"**3波の列 = 「{runCol3.Name}」**（{runCol3.Squads.Count}波）。");
        Console.WriteLine();
        // 3波の列が5波の列の先頭3つと同じ中身であることを**参照同一で確かめる**。
        // 「中身も順序も順路と同じ」はコメントに書いてあるだけなので信用しない。
        bool prefix = runCol3.Squads.Count <= runCol5.Squads.Count
            && runCol3.Squads.Select((sq, i) => ReferenceEquals(sq, runCol5.Squads[i])).All(x => x);
        Console.WriteLine($"3波の列が5波の列の**先頭3つと参照同一**か: **{(prefix ? "○（同一）" : "×（違う）")}**"
            + "——ここが × なら、予測1（3波の列の落ち方は5波の列の先頭3波と同じ）が最初から成り立たない。");
        Console.WriteLine();
    }

    // ---------------- Phase 0 の残り（戦闘0回） ----------------
    void EmitPhase0Rest()
    {
        Console.WriteLine("## Phase 0 の残り（**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("### (2) `chain` の残存・全滅勝ちの定義（実装から読んだ。写す定義を確定させる）");
        Console.WriteLine();
        Console.WriteLine("| 列 | `chain` の定義（`Program.cs` の `focusId == \"chain\"` の集計ループ） | `run` で写したか |");
        Console.WriteLine("|---|---|:-:|");
        Console.WriteLine("| `残存` | `PlayerWon` の試行だけで `BattleResult.PlayerSurvivors` を平均し、`f.Occupied().Count()` を分母に併記 | **○** |");
        Console.WriteLine("| `全滅勝ち` | `PlayerWon` かつ `PlayerSurvivors <= 1` の試行 ÷ 勝った試行 | **○** |");
        Console.WriteLine("| `決着T` | `PlayerWon` の試行だけの `BattleResult.Turns` の平均 | **○** |");
        Console.WriteLine("| `圧勝率` | **`chain` に無い。この期の新しい列**（`PlayerSurvivors >= 4` ÷ 勝った試行） | — |");
        Console.WriteLine();
        Console.WriteLine("`PlayerSurvivors` は `BattleEngine.Run` の返り値で `ctx.LivingMembers(PlayerTeam).Count()`");
        Console.WriteLine("——**戦闘中に湧いた駒（胞子・分裂）も数に入る**ので、**召喚を持つ行では出撃数を超えうる**");
        Console.WriteLine("（`chain` も同じ性質を持つ。`残存` の分子が分母の 5 を超えることがある）。");
        Console.WriteLine();
        Console.WriteLine("### (3) 61行と交差帯12行を同じ呼び出しで回せるか");
        Console.WriteLine();
        var cmp = CompareBuilds();
        var crs = CrossBuilds();
        int dupes = runRows.GroupBy(r => r.Name).Count(g => g.Count() > 1);
        Console.WriteLine($"`Presets.Compare` **{cmp.Length}行** ＋ `Presets.Cross` **{crs.Length}行** = **{runRows.Length}行**。"
            + $"行名の重複 **{dupes}件**。→ {(runRows.Length == 73 && dupes == 0 ? "**○ 同じ呼び出しで回せる**" : "**× 前提が崩れている**")}");
        Console.WriteLine();
        int five = runRows.Count(r => r.F.Occupied().Count() == 5);
        Console.WriteLine($"出撃数が5体の行: **{five} / {runRows.Length}**"
            + "——`圧勝率` を「生存4体以上」で定義するので、出撃数が揃っていないと列の意味が行ごとに変わる。");
        Console.WriteLine();
        Console.WriteLine("### (4) `EngagementEngine.Run` が味方部隊を1つしか取らないこと");
        Console.WriteLine();
        Console.WriteLine("`Run(playerSquads, enemySquads, seed, verbose)` は `playerSquads[pi]` を順に投入し、");
        Console.WriteLine("`lostP = aliveP.Count == 0 || !r.PlayerWon` が立つたび `pi++`。");
        Console.WriteLine("`playerOut = lostP && pi == playerSquads.Count` なので、**長さ1の配列を渡せば1敗で会戦が終わる。**");
        Console.WriteLine("`run` は `new[] { f }` を渡す（`engage` の投入部隊数 1〜3 とは違い、**この期は1固定**）。");
        Console.WriteLine("**境界に回復は1点も無い**（`Engagement.cs` に `Heal` も `Hp +=` も0箇所。第99期）。");
        Console.WriteLine();
    }

    // ---------------- 表B・D（会戦） ----------------
    (double BreakRate, double AvgBroke, int[] FellAt, int BrokeRows,
     double[] EntryAlive, double[] EntryHp, int[] EntryReached,
     double EngSurv, double EngBlow, double EngNarrow, int EngWins)
        EmitEngage(EnemyCatalog.Column col)
    {
        int n = col.Squads.Count;
        int cells = runRows.Length * RunSeeds;

        Console.WriteLine($"## 表B（{col.Name}・{n}波） —— 会戦の指標");
        Console.WriteLine();
        Console.WriteLine($"73行 × seed 0..{RunSeeds - 1}。**味方部隊は1つ**（1敗で会戦が終わる）。");
        Console.WriteLine("`突破率` はその列を最後まで抜けた seed の割合。`突破波数` は**落ちた波 − 1** の平均");
        Console.WriteLine("（抜け切った試行は列長）。`抜いた部隊数` は `EnemySquadsCleared` の平均で**検算用**");
        Console.WriteLine("——相打ち（最終戦で両軍全滅）の試行だけ 突破波数 より 1 大きく出る。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 突破率 | 突破波数 | 抜いた部隊数 | 落ちた波の分布("
            + string.Join("/", Enumerable.Range(1, n).Select(i => $"第{i}")) + ") |");
        Console.WriteLine("|---|--:|--:|--:|---|");

        int totBreak = 0;
        double totBroke = 0;
        var totFell = new int[n + 2];
        var totAlive = new double[n];
        var totHp = new double[n];
        var totReached = new int[n];
        int totEngWins = 0, totEngBlow = 0, totEngNarrow = 0;
        double totEngSurv = 0;
        var entryRows = new List<string>();
        var winnerRows = new List<string>();

        foreach (var (name, f) in runRows)
        {
            var m = RunEngage(f, col);
            totBreak += m.Win;
            totBroke += m.BrokeSum;
            for (int i = 0; i < m.FellAt.Length && i < totFell.Length; i++) totFell[i] += m.FellAt[i];
            for (int b = 0; b < n; b++)
            {
                totAlive[b] += m.AliveSum[b];
                totHp[b] += m.HpSum[b];
                totReached[b] += m.Reached[b];
            }
            totEngWins += m.Wins; totEngBlow += m.Blow; totEngNarrow += m.Narrow; totEngSurv += m.SurvSum;

            Console.WriteLine($"| {name} | {m.Win * 100.0 / RunSeeds:F1}% | {m.BrokeSum / RunSeeds:F2} "
                + $"| {m.ClearedSum / RunSeeds:F2} | "
                + string.Join(" / ", Enumerable.Range(1, n).Select(i => $"{m.FellAt[i]}")) + " |");
            Console.Out.Flush();

            entryRows.Add($"| {name} |" + string.Concat(Enumerable.Range(0, n).Select(b =>
                m.Reached[b] == 0
                    ? $" — (0/{RunSeeds}) |"
                    : $" {m.AliveSum[b] / m.Reached[b]:F2}体 {m.HpSum[b] * 100 / m.Reached[b]:F0}%"
                      + $" ({m.Reached[b]}/{RunSeeds}) |")));

            if (m.Wins > 0)
                winnerRows.Add($"| {name} | {m.Win * 100.0 / RunSeeds:F1}% | {m.SurvSum / m.Wins:F2}/{m.Party} "
                    + $"| {m.Blow * 100.0 / m.Wins:F0}% | {m.Narrow * 100.0 / m.Wins:F0}% | {m.TurnSum / m.Wins:F1} |");
        }

        Console.WriteLine();
        Console.WriteLine($"**列の集計**: 突破率 **{totBreak * 100.0 / cells:F2}%**（{totBreak} / {cells} 試行）／"
            + $"**1試行でも抜けた行 {winnerRows.Count} / {runRows.Length}**／"
            + $"突破波数の平均 **{totBroke / cells:F2}** / {n}");
        Console.WriteLine();
        Console.WriteLine("落ちた波の分布（全73行 × 全試行）: "
            + string.Join(" / ", Enumerable.Range(1, n).Select(i =>
                $"第{i}波 {totFell[i]}（{totFell[i] * 100.0 / cells:F1}%）")));
        Console.WriteLine();

        Console.WriteLine($"### 入場戦力（{col.Name}）");
        Console.WriteLine();
        Console.WriteLine("各波の開始時点の生存数と HP 合計の割合（**分母は編成全体の定義上総最大HP**・不変値）。");
        Console.WriteLine("到達しなかった試行は分母から外し、到達率を括弧で併記する（第99期の Phase 0 と同じ定義）。");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(0, n).Select(b => $" 第{b + 1}波 |")));
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, n).Select(_ => "---|")));
        foreach (string r in entryRows) Console.WriteLine(r);
        Console.WriteLine();
        Console.WriteLine("全73行の平均: " + string.Join(" / ", Enumerable.Range(0, n).Select(b =>
            totReached[b] == 0 ? $"第{b + 1}波 —"
            : $"第{b + 1}波 {totAlive[b] / totReached[b]:F2}体 {totHp[b] * 100 / totReached[b]:F1}%"
              + $"（到達 {totReached[b] * 100.0 / cells:F0}%）")));
        Console.WriteLine();

        Console.WriteLine($"### 表D（{col.Name}） —— 会戦の勝ち方の質");
        Console.WriteLine();
        if (winnerRows.Count == 0)
        {
            Console.WriteLine("**抜けた行が1つも無い**（1試行でも突破した行が 0 / 73）。");
            Console.WriteLine("`残存` / `圧勝率` / `全滅勝ち` は勝った試行の上でしか定義できないので、この列では出せない。");
        }
        else
        {
            Console.WriteLine("**最後の波を勝ったときの**生存数で測る（指示書 A-1b）。`決着T` は会戦全体の総ターン数。");
            Console.WriteLine("1試行でも突破した行だけを載せる。");
            Console.WriteLine();
            Console.WriteLine("| 編成 | 突破率 | 残存 | 圧勝率 | 全滅勝ち | 決着T(会戦計) |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|");
            foreach (string r in winnerRows) Console.WriteLine(r);
            Console.WriteLine();
            Console.WriteLine($"抜けた行 **{winnerRows.Count} / {runRows.Length}**・突破した試行 **{totEngWins}**。"
                + $"残存の平均 **{totEngSurv / totEngWins:F2}**・圧勝率 **{totEngBlow * 100.0 / totEngWins:F0}%**"
                + $"・全滅勝ち **{totEngNarrow * 100.0 / totEngWins:F0}%**。");
        }
        Console.WriteLine();
        Console.Out.Flush();

        return (totBreak * 100.0 / cells, totBroke / cells, totFell, winnerRows.Count,
                totAlive, totHp, totReached,
                totEngWins == 0 ? 0 : totEngSurv / totEngWins,
                totEngWins == 0 ? 0 : totEngBlow * 100.0 / totEngWins,
                totEngWins == 0 ? 0 : totEngNarrow * 100.0 / totEngWins, totEngWins);
    }

    // ---------------- 表C（単発の勝ち方） ----------------
    (double[] WaveSurv, double[] WaveBlow, double[] WaveNarrow, double[] WaveRate,
     double PoolSurv, double PoolBlow, double PoolNarrow,
     double TailSurv, double TailBlow, double TailNarrow,
     int RicaRows, double RicaSurv, double RicaNarrow, double OtherSurv, double OtherNarrow,
     double RicaTailNarrow, double OtherTailNarrow,
     bool ChainOk)
        EmitSolo()
    {
        var stages = EnemyCatalog.Stages;
        int nw = stages.Count;

        Console.WriteLine("## 表C —— 勝ち方の質（単発・73行 × 5波）");
        Console.WriteLine();
        Console.WriteLine($"73行 × 5波 × seed 0..{RunSeeds - 1}。**勝った試行についてだけ**集計する。");
        Console.WriteLine("**`残存` / `全滅勝ち` / `決着T` の定義は `chain` から写した**"
            + "（写し元は `Program.cs` の `focusId == \"chain\"` の集計ループ。**`chain` は1文字も書き換えていない**）。");
        Console.WriteLine("`圧勝率` = **生存4体以上で勝った試行 ÷ 勝った試行**。この期の新しい列。");
        Console.WriteLine();
        Console.WriteLine("### C-1. 行ごと（5波を通算。**`chain` と同じ集計単位**）");
        Console.WriteLine();
        Console.WriteLine("末尾3列は**第一波を除いた第2〜5波**の同じ量。第一波は全行必勝の教習波なので、");
        Console.WriteLine("**通算に混ぜると `圧勝率` の意味が反転する**（第70期の「天井の波を集計に混ぜた瞬間に");
        Console.WriteLine("判定が反転する」の再発）。**予測の判定は指示書どおり5波通算で行い、この3列は判定に使わない。**");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 勝率 | 残存 | 圧勝率 | 全滅勝ち | 決着T | 残存(2-5波) | 圧勝率(2-5波) | 全滅勝ち(2-5波) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");

        var waveTrials = new int[nw]; var waveWins = new int[nw];
        var waveSurv = new double[nw]; var waveBlow = new int[nw];
        var waveNarrow = new int[nw]; var waveTurn = new double[nw];
        var poolByName = new Dictionary<string, (double Surv, double Narrow, int Party, double Rate, double Turn)>();
        var survBuckets = new int[7];
        var tailBuckets = new int[7];
        int ricaRows = 0, otherRows = 0;
        double ricaSurv = 0, ricaNarrow = 0, otherSurv = 0, otherNarrow = 0;
        double ricaTailNarrow = 0, otherTailNarrow = 0;

        foreach (var (name, f) in runRows)
        {
            var acc = RunSolo(f);
            int trials = 0, wins = 0, blow = 0, narrow = 0;
            double surv = 0, turn = 0;
            for (int w = 0; w < nw; w++)
            {
                trials += acc[w].Trials; wins += acc[w].Wins; blow += acc[w].Blow;
                narrow += acc[w].Narrow; surv += acc[w].SurvSum; turn += acc[w].TurnSum;
                waveTrials[w] += acc[w].Trials; waveWins[w] += acc[w].Wins;
                waveSurv[w] += acc[w].SurvSum; waveBlow[w] += acc[w].Blow;
                waveNarrow[w] += acc[w].Narrow; waveTurn[w] += acc[w].TurnSum;
            }
            int party = acc[0].Party;
            double sAvg = wins > 0 ? surv / wins : 0;
            double nAvg = wins > 0 ? narrow * 100.0 / wins : 0;
            // 第一波（w = 0）を除いた集計。**判定には使わない**（第70期の但し書き）。
            int tWins = 0, tBlow = 0, tNarrow = 0; double tSurv = 0;
            for (int w = 1; w < nw; w++)
            { tWins += acc[w].Wins; tBlow += acc[w].Blow; tNarrow += acc[w].Narrow; tSurv += acc[w].SurvSum; }
            double tsAvg = tWins > 0 ? tSurv / tWins : 0;
            double tnAvg = tWins > 0 ? tNarrow * 100.0 / tWins : 0;

            Console.WriteLine($"| {name} | {wins * 100.0 / trials:F1}% | {sAvg:F1}/{party} "
                + $"| {(wins > 0 ? blow * 100.0 / wins : 0):F0}% | {nAvg:F0}% "
                + $"| {(wins > 0 ? turn / wins : 0):F1} "
                + $"| {(tWins > 0 ? tsAvg.ToString("F1") : "—")} "
                + $"| {(tWins > 0 ? (tBlow * 100.0 / tWins).ToString("F0") + "%" : "—")} "
                + $"| {(tWins > 0 ? tnAvg.ToString("F0") + "%" : "—")} |");
            Console.Out.Flush();
            poolByName[name] = (sAvg, nAvg, party, wins * 100.0 / trials, wins > 0 ? turn / wins : 0);

            if (wins > 0)
            {
                survBuckets[Math.Min(6, Math.Max(0, (int)Math.Round(sAvg)))]++;
                // リィカ（墓守）を含む行かどうか。**行名ではなく駒で引く**
                bool hasRica = f.Occupied().Any(u => u.Def.Id == UnitCatalog.Rica.Id);
                if (hasRica) { ricaRows++; ricaSurv += sAvg; ricaNarrow += nAvg; ricaTailNarrow += tnAvg; }
                else { otherRows++; otherSurv += sAvg; otherNarrow += nAvg; otherTailNarrow += tnAvg; }
            }
            if (tWins > 0) tailBuckets[Math.Min(6, Math.Max(0, (int)Math.Round(tsAvg)))]++;
        }

        int allTrials = waveTrials.Sum(), allWins = waveWins.Sum();
        double allSurv = waveSurv.Sum(); int allBlow = waveBlow.Sum(), allNarrow = waveNarrow.Sum();

        Console.WriteLine();
        Console.WriteLine("### C-2. 波ごと（73行を通算）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 残存 | 圧勝率 | 全滅勝ち | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        for (int w = 0; w < nw; w++)
            Console.WriteLine($"| {stages[w].Name} | {waveWins[w] * 100.0 / waveTrials[w]:F1}% "
                + $"| {(waveWins[w] > 0 ? waveSurv[w] / waveWins[w] : 0):F2} "
                + $"| {(waveWins[w] > 0 ? waveBlow[w] * 100.0 / waveWins[w] : 0):F1}% "
                + $"| {(waveWins[w] > 0 ? waveNarrow[w] * 100.0 / waveWins[w] : 0):F1}% "
                + $"| {(waveWins[w] > 0 ? waveTurn[w] / waveWins[w] : 0):F1} |");
        Console.WriteLine($"| **通算（5波・判定はこちら）** | **{allWins * 100.0 / allTrials:F1}%** "
            + $"| **{allSurv / allWins:F2}** | **{allBlow * 100.0 / allWins:F1}%** "
            + $"| **{allNarrow * 100.0 / allWins:F1}%** | **{waveTurn.Sum() / allWins:F1}** |");
        int tailTrials = 0, tailWins = 0, tailBlow = 0, tailNarrow = 0;
        double tailSurv = 0, tailTurn = 0;
        for (int w = 1; w < nw; w++)
        {
            tailTrials += waveTrials[w]; tailWins += waveWins[w]; tailBlow += waveBlow[w];
            tailNarrow += waveNarrow[w]; tailSurv += waveSurv[w]; tailTurn += waveTurn[w];
        }
        Console.WriteLine($"| **第2〜5波（教習波を除く・参考）** | **{tailWins * 100.0 / tailTrials:F1}%** "
            + $"| **{tailSurv / tailWins:F2}** | **{tailBlow * 100.0 / tailWins:F1}%** "
            + $"| **{tailNarrow * 100.0 / tailWins:F1}%** | **{tailTurn / tailWins:F1}** |");
        Console.WriteLine();
        Console.WriteLine("**第一波は全行必勝の教習波**なので、5波通算の `圧勝率` はその1波に引きずられる。");
        Console.WriteLine("**予測の判定は指示書どおり5波通算で行う**（結果を見てから分母を緩めない・第64期）。");
        Console.WriteLine("第2〜5波の行は**参考**として並べるだけで、判定には使わない。");
        Console.WriteLine();

        Console.WriteLine("### C-3. 残存の分布（行ごとの通算残存を四捨五入した箱）");
        Console.WriteLine();
        Console.WriteLine("| 残存(四捨五入) | 0 | 1 | 2 | 3 | 4 | 5 | 6+ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        Console.WriteLine("| 行数（5波通算） | " + string.Join(" | ", survBuckets) + " |");
        Console.WriteLine("| 行数（第2〜5波・参考） | " + string.Join(" | ", tailBuckets) + " |");
        Console.WriteLine();
        Console.WriteLine($"リィカを含む行 **{ricaRows}**（残存 {(ricaRows > 0 ? ricaSurv / ricaRows : 0):F2}"
            + $" / 全滅勝ち {(ricaRows > 0 ? ricaNarrow / ricaRows : 0):F0}%"
            + $" / 全滅勝ち(2-5波) {(ricaRows > 0 ? ricaTailNarrow / ricaRows : 0):F0}%）対 "
            + $"含まない行 **{otherRows}**（残存 {(otherRows > 0 ? otherSurv / otherRows : 0):F2}"
            + $" / 全滅勝ち {(otherRows > 0 ? otherNarrow / otherRows : 0):F0}%"
            + $" / 全滅勝ち(2-5波) {(otherRows > 0 ? otherTailNarrow / otherRows : 0):F0}%）。"
            + "**駒で引いている**（行名ではない）。");
        Console.WriteLine();

        // ---- 受け入れ4: docs/chain.md との突き合わせ ----
        Console.WriteLine("### C-4. 受け入れ4 —— `chain` の残存・全滅勝ちを再現するか");
        Console.WriteLine();
        Console.WriteLine("**新しい器具は既知の値を再現できて初めて使える**（第94期の第一版は欠落 105 件のうち");
        Console.WriteLine("78 件が器具自身の誤りだった）。`docs/chain.md`（`chain` の生成物・同じ 61 行・");
        Console.WriteLine($"同じ seed 0..{RunSeeds - 1}・同じ5波）の `勝率` / `決着T` / `残存` / `全滅勝ち` と、C-1 を突き合わせる。");
        Console.WriteLine("**同じ実行の中で `chain` を呼び直すのではなく、生成物と比べる**"
            + "——同じコードを2回走らせても自分自身との一致にしかならない（第81期の自己検査 (g)）。");
        Console.WriteLine();
        bool chainOk = false;
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "docs", "chain.md")))
            root = Path.GetDirectoryName(root);
        if (root == null)
        {
            Console.WriteLine("**`docs/chain.md` が見つからない**（リポジトリの外から実行している）。検算できない。");
        }
        else
        {
            int checkedRows = 0, mismatch = 0;
            var bad = new List<string>();
            foreach (string line in File.ReadAllLines(Path.Combine(root, "docs", "chain.md")))
            {
                if (!line.StartsWith("| ")) continue;
                string[] cell = line.Split('|').Select(x => x.Trim()).ToArray();
                // | 編成 | 勝率 | 連鎖深度(平均) | 連鎖深度(最大) | 決着T | 残存 | 全滅勝ち |
                if (cell.Length < 9 || cell[1] == "編成") continue;
                if (!poolByName.TryGetValue(cell[1], out var mine)) continue;
                checkedRows++;
                string want = $"{mine.Rate:F1}%|{mine.Turn:F1}|{mine.Surv:F1}/{mine.Party}|{mine.Narrow:F0}%";
                string got = $"{cell[2]}|{cell[5]}|{cell[6]}|{cell[7]}";
                if (want != got) { mismatch++; if (bad.Count < 12) bad.Add($"`{cell[1]}` chain=`{got}` run=`{want}`"); }
            }
            chainOk = checkedRows == CompareBuilds().Length && mismatch == 0;
            Console.WriteLine($"突き合わせた行 **{checkedRows} / {CompareBuilds().Length}**・ずれ **{mismatch}件** → "
                + $"{(chainOk ? "**○ 再現した**" : "**× 再現しない**")}");
            foreach (string b in bad) Console.WriteLine($"- {b}");
        }
        Console.WriteLine();
        Console.Out.Flush();

        return (Enumerable.Range(0, nw).Select(w => waveWins[w] > 0 ? waveSurv[w] / waveWins[w] : 0).ToArray(),
                Enumerable.Range(0, nw).Select(w => waveWins[w] > 0 ? waveBlow[w] * 100.0 / waveWins[w] : 0).ToArray(),
                Enumerable.Range(0, nw).Select(w => waveWins[w] > 0 ? waveNarrow[w] * 100.0 / waveWins[w] : 0).ToArray(),
                Enumerable.Range(0, nw).Select(w => waveWins[w] * 100.0 / waveTrials[w]).ToArray(),
                allSurv / allWins, allBlow * 100.0 / allWins, allNarrow * 100.0 / allWins,
                tailSurv / tailWins, tailBlow * 100.0 / tailWins, tailNarrow * 100.0 / tailWins,
                ricaRows, ricaRows > 0 ? ricaSurv / ricaRows : 0, ricaRows > 0 ? ricaNarrow / ricaRows : 0,
                otherRows > 0 ? otherSurv / otherRows : 0, otherRows > 0 ? otherNarrow / otherRows : 0,
                ricaRows > 0 ? ricaTailNarrow / ricaRows : 0, otherRows > 0 ? otherTailNarrow / otherRows : 0,
                chainOk);
    }

    Console.WriteLine("# 第100期 —— 会戦を測る／「どう勝ったか」を測る");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 run {sub}` の出力。");
    Console.WriteLine("**盤面は1ビットも動かしていない**（駒・特性・数値・engine の規則に触れていない）。");
    Console.WriteLine("**`docs/` には置かない**（この期は分布を見るだけで、常設にするかは分布を見てから決める）。");
    Console.WriteLine();

    if (sub == "phase0") { EmitTableA(); EmitPhase0Rest(); return; }

    if (sub == "engage")
    {
        string which = args.Length > 3 ? args[3] : "5";
        EmitEngage(which == "3" ? runCol3 : runCol5);
        return;
    }

    if (sub == "solo") { EmitSolo(); return; }

    // ---------------- tables: 表A〜E ----------------
    EmitTableA();
    EmitPhase0Rest();
    var e5 = EmitEngage(runCol5);
    var e3 = EmitEngage(runCol3);
    var c = EmitSolo();

    Console.WriteLine("## 表E —— 予測の答え合わせ");
    Console.WriteLine();
    Console.WriteLine("**予測は測る前に指示書 §2 に書いてある。**");
    Console.WriteLine();
    Console.WriteLine("| # | 予測 | 実測 | 判定 |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| 1 | **3波の列では突破率が 0 を超える** | 「{runCol3.Name}」**{e3.BreakRate:F2}%**"
        + $"（1試行でも抜けた行 {e3.BrokeRows}/73）／ 「{runCol5.Name}」{e5.BreakRate:F2}%（{e5.BrokeRows}/73） "
        + $"| {(e3.BreakRate > 0 ? "**○**" : "**×**")} |");
    Console.WriteLine($"| 1' | 3波で落ちるのは 49+23 = 72 行、抜けるのは 1 行（第99期 seed 0 の分布から） "
        + $"| 1試行でも抜けた行 **{e3.BrokeRows} / 73** | — |");
    Console.WriteLine($"| 2 | **残存は 2〜3 に集中し、圧勝率（4体以上）は 1 割に満たない** "
        + $"| 通算 残存 **{c.PoolSurv:F2}** ／ 圧勝率 **{c.PoolBlow:F1}%** ／ 全滅勝ち {c.PoolNarrow:F1}% "
        + $"| {(c.PoolSurv >= 2 && c.PoolSurv < 3.5 && c.PoolBlow < 10 ? "**○**" : "**×**")} |");
    Console.WriteLine($"| 3 | **リィカを含む行は残存が低く全滅勝ちが高い** "
        + $"| リィカ有 {c.RicaRows}行 残存 {c.RicaSurv:F2} / 全滅勝ち {c.RicaNarrow:F0}% 対 "
        + $"無 残存 {c.OtherSurv:F2} / 全滅勝ち {c.OtherNarrow:F0}% "
        + $"| {(c.RicaSurv < c.OtherSurv && c.RicaNarrow > c.OtherNarrow ? "**○**" : "**×**")} |");
    Console.WriteLine($"| 4 | **第一波は全行 100% 勝ち、残存も高い。第二波以降で落ちる** "
        + $"| 第1波 勝率 {c.WaveRate[0]:F1}% / 残存 {c.WaveSurv[0]:F2} → 第2波 {c.WaveRate[1]:F1}% / {c.WaveSurv[1]:F2} "
        + $"→ 第3波 {c.WaveSurv[2]:F2} → 第4波 {c.WaveSurv[3]:F2} → 第5波 {c.WaveSurv[4]:F2} "
        + $"| {(c.WaveRate[0] >= 99.95 && c.WaveSurv[0] > c.WaveSurv[1] ? "**○**" : "**×**")} |");
    Console.WriteLine();
    Console.WriteLine($"**予測2の参考値（判定には使わない）**: 第一波を除いた第2〜5波では "
        + $"残存 **{c.TailSurv:F2}**・圧勝率 **{c.TailBlow:F1}%**・全滅勝ち **{c.TailNarrow:F1}%**。");
    Console.WriteLine("**5波通算の 圧勝率 を作っているのは第一波（教習波・圧勝率 96.4%）である。**");
    Console.WriteLine();

    // 検算: 3波の列は5波の列の先頭3波と参照同一なので、落ちた波の分布も第1〜3波で一致するはず。
    // 一致しなければ、会戦の乱数が列の長さに依存していることになる（DeriveSeed は列長を見ない）。
    bool fellSame = Enumerable.Range(1, runCol3.Squads.Count).All(i => e5.FellAt[i] == e3.FellAt[i]);
    int broke3 = runRows.Length * RunSeeds - Enumerable.Range(1, runCol3.Squads.Count).Sum(i => e3.FellAt[i]);
    Console.WriteLine("### 検算 —— 2本の列は同じ会戦を測っているか");
    Console.WriteLine();
    Console.WriteLine($"3波の列は5波の列の**先頭3波と参照同一**（表A）。会戦の seed は `DeriveSeed(seed, battleIndex)` で");
    Console.WriteLine("**列の長さを見ない**ので、第1〜3波の落ち方は2本の列で**厳密に一致するはず**。");
    Console.WriteLine();
    Console.WriteLine("| 波 | 順路で落ちた数 | 地点で落ちた数 |");
    Console.WriteLine("|---|--:|--:|");
    for (int i = 1; i <= runCol3.Squads.Count; i++)
        Console.WriteLine($"| 第{i}波 | {e5.FellAt[i]} | {e3.FellAt[i]} |");
    Console.WriteLine();
    Console.WriteLine($"一致: **{(fellSame ? "○" : "×")}**。さらに **地点を抜けた試行 {broke3} 件** は、"
        + $"**順路で第4波に到達して落ちた試行 {e5.FellAt[4]} 件**と一致するはず → "
        + $"**{(broke3 == e5.FellAt[4] ? "○" : "×")}**");
    Console.WriteLine();
    Console.WriteLine($"受け入れ4（`chain` の再現）: {(c.ChainOk ? "**○**" : "**×**")}（C-4 を見ること）");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}
}
