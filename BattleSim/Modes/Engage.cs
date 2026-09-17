using BattleCore;
using static Common;

// =====================================================================================
// engage モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "engage")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 engage
// =====================================================================================

static class EngageDiag
{
// engage モード: 会戦（部隊連戦・持ち越しあり）を「列 × 投入部隊数」で測る。
//
// compare は各波を独立した1戦として測るが、会戦は勝った部隊が生存駒の状態
// （HP・最大HPの損耗・蘇生回数・墓守の層-1）を持ち越して次の波と戦う。
// 主表は地点（3波）× 投入部隊数 1〜3。5波1本の順路では全編成が突破 0% に潰れて序列に
// ならず、部隊数を積んだ地点だけが 0〜100% に散る（第3期で切り替え。順路は参考、
// 逆順は第1削り専用へ格下げ）。投入部隊数は同一編成の複製。組み合わせ（別編成×別編成）は
// 多すぎるので測らない。
//
// 却下した案: 独立積（各波の独立勝率の積）・突破分布（0..N 抜きの試行数）・引分の列を残す——
// 会戦の効き目（独立積との差）は第2期で確認済みで役目を終えた。独立勝率そのものは
// docs/balance.md が持ち続けるので、ここに残すと waveCache と順路↔逆順一致検算の維持費だけが残る。
//
//     dotnet run --project BattleSim -c Release 0 engage [絞り込み] > docs/engage.md
public static void Run(string[] args, int stageIndex)
{
    var all = CompareBuilds();
    const int EngageSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 列は Name で引く（Columns の並び順は GodotApp の EngagementColumn が使うので当てにしない）
    EnemyCatalog.Column spot = EnemyCatalog.Columns.First(c => c.Name == "地点");
    EnemyCatalog.Column route = EnemyCatalog.Columns.First(c => c.Name == "順路");
    EnemyCatalog.Column rev = EnemyCatalog.Columns.First(c => c.Name == "逆順");

    // 1編成 × 1列 × 投入 nSquads 部隊（同一編成の複製）の一括計測。
    // 突破率は PlayerWon で数える（EnemySquadsCleared == N は相打ち全滅を突破に数えてしまう）。
    // 入場戦力・敵側検算は1部隊のときだけ集計する。「第 i 戦の入場 = PlayerEntries[i]、
    // 敵は毎回新規投入」という 1:1 の前提（負けた時点で会戦が終わる）が2部隊以上では崩れるため。
    (int Full, double Cleared, double Attr, int Draws, int[] Dist,
     double[] AliveSum, double[] HpRatioSum, int[] Reached,
     double[] EnemyEroded, int[] EnemyReached)
        Sweep(Formation f, IReadOnlyList<Formation> column, int nSquads)
    {
        int squads = column.Count;
        Formation[] playerColumn = Enumerable.Repeat(f, nSquads).ToArray();

        // HP割合の分母は**編成全体**の定義上総最大HP（不変値）。SquadEntry.DefMaxHpSum を
        // そのまま分母にする案は却下した——あれは「その戦闘に入った駒」だけの合計なので、
        // 死んだ駒が分子と分母から一緒に抜け、% が「部隊の残存戦力」ではなく「生き残りの
        // 健康度」に化ける（1体だけ全快で残った部隊が 100% に見える）。
        int playerDefTotal = f.Occupied().Sum(x => x.Def.MaxHp);
        int[] enemyDefTotal = column.Select(e => e.Occupied().Sum(x => x.Def.MaxHp)).ToArray();

        var dist = new int[squads + 1];
        int full = 0, draws = 0;
        double clearedSum = 0, attrSum = 0;
        var aliveSum = new double[squads];
        var hpRatioSum = new double[squads];
        var reached = new int[squads];
        var enemyEroded = new double[squads];
        var enemyReached = new int[squads];

        for (int seed = 0; seed < EngageSeeds; seed++)
        {
            EngagementResult r = EngagementEngine.Run(playerColumn, column, seed, verbose: false);
            dist[r.EnemySquadsCleared]++;
            if (r.PlayerWon) full++;
            clearedSum += r.EnemySquadsCleared;
            attrSum += r.FirstBattleAttrition;
            draws += r.Draws;

            if (nSquads != 1) continue;
            for (int b = 0; b < r.PlayerEntries.Count && b < squads; b++)
            {
                aliveSum[b] += r.PlayerEntries[b].Alive;
                hpRatioSum[b] += (double)r.PlayerEntries[b].HpSum / playerDefTotal;
                reached[b]++;

                // 敵側の分母はその戦闘の敵部隊の定義上総最大HP（味方1部隊では ei = b）
                enemyEroded[b] += 1.0 - (double)r.EnemyEntries[b].HpSum
                    / enemyDefTotal[r.Pairings[b].EnemySquad];
                enemyReached[b]++;
            }
        }
        return (full, clearedSum, attrSum, draws, dist,
                aliveSum, hpRatioSum, reached, enemyEroded, enemyReached);
    }

    // 主表1節ぶん: 突破率(1/2/3)・期待突破数(1/2/3)・非線形 → 入場戦力（1部隊）→ 敵側検算行。
    void EmitSection(EnemyCatalog.Column col)
    {
        int squads = col.Squads.Count;
        Console.WriteLine("### 突破率と期待突破数（1部隊 / 2部隊 / 3部隊）");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 突破率(1) | 突破率(2) | 突破率(3) | 期待突破数(1) | 期待突破数(2) | 期待突破数(3) | 非線形(2部隊/1部隊×2) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");

        var entryRows = new List<string>();
        var enemyErodedAll = new double[squads];
        var enemyReachedAll = new int[squads];

        // === 第140期 —— 3つの `Sweep` を編成をまたいで1つの平坦な並列パスに前出しした ===
        //
        // `Sweep` は (編成, 部隊列, 投入部隊数) の純関数で、内側で作る配列も呼び出しごとに新しい。
        // `EngagementEngine.Run` は `BattleEngine.Run` を繋ぐだけなので同じく seed 決定的。
        // **各ジョブは自分の添字にしか書かず、印字は従来どおり直列に `targets` の順で行う。**
        var egS = new (int Full, double Cleared, double Attr, int Draws, int[] Dist,
                       double[] AliveSum, double[] HpRatioSum, int[] Reached,
                       double[] EnemyEroded, int[] EnemyReached)[targets.Length, 3];
        Parallel.For(0, targets.Length * 3, k =>
        {
            int t = k / 3, n = k % 3;
            egS[t, n] = Sweep(targets[t].F, col.Squads, n + 1);
        });

        for (int t = 0; t < targets.Length; t++)
        {
            string name = targets[t].Name;
            var s1 = egS[t, 0];
            var s2 = egS[t, 1];
            var s3 = egS[t, 2];

            // 非線形 = 期待突破数(2部隊) ÷ (期待突破数(1部隊)×2)。1.00 超なら第1部隊の削りを
            // 第2部隊が拾えている。期待(1) が 0 の編成は分母が立たないので —（現状は出ない）。
            string nonlinear = s1.Cleared == 0 ? "—" : $"{s2.Cleared / (2 * s1.Cleared):F2}";

            Console.WriteLine($"| {name} | {s1.Full * 100.0 / EngageSeeds:F1}% | {s2.Full * 100.0 / EngageSeeds:F1}% "
                + $"| {s3.Full * 100.0 / EngageSeeds:F1}% | {s1.Cleared / EngageSeeds:F2} | {s2.Cleared / EngageSeeds:F2} "
                + $"| {s3.Cleared / EngageSeeds:F2} | {nonlinear} |");
            Console.Out.Flush();

            entryRows.Add($"| {name} |" + string.Concat(Enumerable.Range(0, squads).Select(b =>
                s1.Reached[b] == 0
                    ? $" — (0/{EngageSeeds}) |"
                    : $" {s1.AliveSum[b] / s1.Reached[b]:F1}体 {s1.HpRatioSum[b] * 100 / s1.Reached[b]:F0}%"
                      + $" ({s1.Reached[b]}/{EngageSeeds}) |")));
            for (int b = 0; b < squads; b++)
            {
                enemyErodedAll[b] += s1.EnemyEroded[b];
                enemyReachedAll[b] += s1.EnemyReached[b];
            }
        }

        Console.WriteLine();
        Console.WriteLine("### 入場戦力（味方・1部隊）");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(0, squads).Select(b => $" 第{b + 1}戦 |")));
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, squads).Select(_ => "---|")));
        foreach (string row in entryRows) Console.WriteLine(row);
        Console.WriteLine();
        Console.WriteLine("持ち越された敵部隊が削れていた割合の平均（全編成・全試行）: "
            + string.Join(" / ", Enumerable.Range(0, squads).Select(b => enemyReachedAll[b] == 0
                ? $"第{b + 1}戦 —"
                : $"第{b + 1}戦 {enemyErodedAll[b] * 100 / enemyReachedAll[b]:F0}%"))
            + "（味方1部隊では敵は毎回新規投入なので全戦 0%＝入場HP 100% のはず。ずれていたら実装がおかしい）");
    }

    Console.WriteLine("# 会戦");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 engage > docs/engage.md` の出力。手で編集しない。");
    Console.WriteLine($"各編成を3本の部隊列にぶつけ、それぞれ seed 0..{EngageSeeds - 1} の {EngageSeeds} 試行。");
    Console.WriteLine("投入部隊数 1〜3 は同一編成の複製（組み合わせは測らない）。");
    Console.WriteLine();
    Console.WriteLine("勝った部隊は生存駒の HP・最大HPの損耗・蘇生回数・墓守の層(-1) を持ち越して次の波と戦う。");
    Console.WriteLine("状態異常（毒・燃焼・痺れ・標的・破片）と攻撃力の一時変動は波の境界で消える。");
    Console.WriteLine();
    Console.WriteLine("部隊列は3本。敵の中身はどれも既存5波のままで、並びと長さだけが違う。");
    Console.WriteLine();
    foreach (EnemyCatalog.Column c in EnemyCatalog.Columns)
        Console.WriteLine($"- **{c.Name}**（{c.Squads.Count}部隊） — {c.Note}");
    Console.WriteLine();
    Console.WriteLine("### 表の読み方");
    Console.WriteLine();
    Console.WriteLine("- `突破率(n)` は n 部隊投入で列の全部隊を抜いた試行の割合。`期待突破数(n)` は抜いた部隊数の平均。");
    Console.WriteLine("- `非線形` は 期待突破数(2部隊) ÷ (期待突破数(1部隊)×2)。**1.00 を超えるなら第1部隊の削りを");
    Console.WriteLine("  第2部隊が拾えている**＝複数部隊制が噛み合っている証拠。期待突破数(1部隊) が列の長さに");
    Console.WriteLine("  近い編成は ×2 が列の長さを超えるので、頭打ちで 1.00 を下回る（弱いのではなく測り切れないだけ）。");
    Console.WriteLine("- `入場戦力` は各部隊戦に入る時点の味方の生存数と HP（**編成全体の定義上の**総最大HPに");
    Console.WriteLine("  対する割合。死んだ駒の枠も分母に残るので、% は部隊の残存戦力を表す。生き残りの健康度");
    Console.WriteLine("  ではない）。到達しなかった試行は分母から外し、到達率を併記する。1部隊投入の走行から集計する。");
    Console.WriteLine("- `第1削り` は最初の Battle で敵の先頭部隊の総 MaxHp を削った割合。**勝てなくても削れる編成**");
    Console.WriteLine("  （特攻隊）はここに出る。逆順の節にだけ載せる（順路・地点では第一波が全編成必勝で");
    Console.WriteLine("  一律 100% になり無情報）。");

    Console.WriteLine();
    Console.WriteLine($"## 地点（{spot.Squads.Count}部隊） — 標準の測定系");
    Console.WriteLine();
    Console.WriteLine("マップ上の1地点は敵1〜3部隊（design/concept_wave_engagement.md §7）。5波1本の順路では");
    Console.WriteLine("全編成が突破 0% に潰れるのに対し、この列は部隊数を積むと突破率が 0〜100% に散る——");
    Console.WriteLine("現時点で唯一、編成の序列として機能する分布なので主表とする。");
    Console.WriteLine();
    EmitSection(spot);

    Console.WriteLine();
    Console.WriteLine($"## 順路（{route.Squads.Count}部隊） — 参考。1地点としては長すぎる");
    Console.WriteLine();
    Console.WriteLine("第2期まで主表だった5波1本の列。全編成が突破 0%・期待突破数 1.00〜2.00 に潰れて序列として");
    Console.WriteLine("機能せず、コンセプト上も1地点は敵1〜3部隊なので参考へ降格した。第2戦→第3戦で駒も HP も");
    Console.WriteLine("一気に落ちる消耗（第二波が代金）の位置は、この列の入場戦力で読む。");
    Console.WriteLine();
    EmitSection(route);

    Console.WriteLine();
    Console.WriteLine("## 逆順 — 第1削り専用");
    Console.WriteLine();
    Console.WriteLine("強い波が先頭の列。**突破数の列は載せない**——逆順は全編成が 0 か 1 抜きで初戦＝第五波の");
    Console.WriteLine("勝敗しか測らず、突破数は docs/balance.md の第5波（独立勝率）の測り直しにしかならない。");
    Console.WriteLine("勝てない編成が先頭の強敵をどれだけ削るか（特攻隊の価値）だけをこの列で読む。");
    Console.WriteLine();
    Console.WriteLine("### 第1削り");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 第1削り |");
    Console.WriteLine("|---|--:|");
    foreach (var (name, f) in targets)
    {
        var m1 = Sweep(f, rev.Squads, 1);
        Console.WriteLine($"| {name} | {m1.Attr * 100 / EngageSeeds:F0}% |");
        Console.Out.Flush();
    }
    return;
}
}
