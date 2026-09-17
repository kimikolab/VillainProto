using BattleCore;
using static Common;

// =====================================================================================
// gullet モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "gullet")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 gullet
// =====================================================================================

static class GulletDiag
{
// gullet モード: 巨躯の「吐き戻し」は肩代わりに価値を運ばせるか（第23期）。
//
// 肩代わり4種のうち、**見返りを持たないのは巨躯だけ**だった（庇う＝肩代わりした分だけ攻撃+/
// 分かち＝被弾に応じて攻撃+/ 後備え＝保持者が Rage を併せ持つ）。ゴルムは後方全員への攻撃を
// 90% 引き受けて、**そこで価値が消える**。第19期 route（ナラの削り7のうち6をゴルムが食い、
// ムドの Rage が +3 のはずが +1 に潰れた）・第21期 swap（ノノの回復の最大の受け手がゴルム）・
// 第22期 渇き（大喰らいが隠れた ctx.Heal 経路だった）と、3期続けて同じ吸い込み口が出ている。
//
// **見返りはゴルムではなく「守った相手」に返す。** ゴルム自身に返す形（庇う・分かちと同経路）だと
// route の問題は直らないし、後ろに誰を置くかも判断にならない。
//
// **4版の対照を最初から組む。** 逆位（第22期）で「効いていたのは壁で、ルールではない」を
// 後から切り分ける羽目になったため。V0/V2 が肩代わり率だけの辺、V1/V3 が吐き戻しの辺。
//
//   V0  90% / 吐き戻し無し   baseline。**現行の docs/balance.md と一致するはず＝検算**
//   V1  90% / 吐き戻し有り   本命（＝既定。ColossusRule.Default）
//   V2  60% / 吐き戻し無し   引き下げ単独の効果
//   V3  60% / 吐き戻し有り   両方
//
// 版は ColossusRule を Run に渡して切り替える。**書き換え可能な static のノブは置いていない**
// （Trait は共有シングルトンで layout は並列実行する。理由は ColossusRule の doc を参照）。
//
// **CompareBuilds() / Stages を触らない**ので docs/ の差分はゼロ。診断用なので docs/ には置かない。
//
//     dotnet run --project BattleSim -c Release 0 gullet        # 4版の対照
//     dotnet run --project BattleSim -c Release 0 gullet gain   # DamagePerGain を 2/4/6/8 で振る
//     dotnet run --project BattleSim -c Release 0 gullet log    # 1戦の監査（受け入れ基準 3〜5）
public static void Run(string[] args, int stageIndex)
{
    string gulletMode = args.Length > 2 ? args[2] : "";
    var gulletBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> gulletStages = EnemyCatalog.Stages;
    const int GulletSeeds = 200;   // compare / spread と同じ。balance.md と突き合わせるので変えない

    bool HasGolm(Formation f) => f.Occupied().Any(o => o.Item2.Id == UnitCatalog.Golm.Id);

    // ---- belly4: 腹の4版対照（第36期 Phase 3）----------------------------------------------
    //
    //   V0 現行     腹の出口なし（＝ ColossusRule.Default。docs/balance.md と一致するはず＝検算）
    //   V2 まどろみ 腹が N に達した手番を失う（→ IdleTurn → 号令・据えが買う）
    //   V3 還し     倒れたとき、腹の残りの P% を生存味方へ回復として分配（1戦1回）
    //   V4 両方     腹は共有カウンター。**眠って売るか、抱えて還すか**が成立するかを見る
    //
    // 版は ColossusRule を Run に渡して切り替える（static のノブは置かない。第23期と同じ作法）。
    //
    //     dotnet run --project BattleSim -c Release 0 gullet belly4
    if (gulletMode == "belly4")
    {
        // **V0 は明示的に両方を切る。** 第36期の採用手順で `ColossusRule.Default` は
        // 還し有効になったので、Default をそのまま V0 に使うと梯子が崩れる
        // （V0 と V3 が同じものになる）。逆に **V3 が現在の既定と一致する**ので、
        // 検算の相手は V0 ではなく V3 になった。
        ColossusRule Base = ColossusRule.Default with { Slumber = false, Refund = false };
        var vers = new (string Name, string Note, ColossusRule Rule)[]
        {
            ("V0 腹なし",   "腹の出口なし（第35期までの盤面）", Base),
            ("V2 まどろみ", $"腹 {ColossusTrait.SlumberThreshold} で手番を失う",
                Base with { Slumber = true }),
            ("V3 還し",     $"倒れたとき腹の {ColossusTrait.RefundPercent}% を分配（＝**現在の既定**）",
                Base with { Refund = true }),
            ("V4 両方",     "眠りが腹を食い、残りを還す", Base with { Slumber = true, Refund = true }),
        };
        var dels = new[] { ("V2−V0", 1, 0), ("V3−V0", 2, 0), ("V4−V0", 3, 0) };

        int bnv = vers.Length, bnb = gulletBuilds.Length, bnw = gulletStages.Count;

        var brate = new double[bnv][][];              // brate[版][編成][波] = 勝率(%)
        var tal = new UnitTally[bnv][];              // ゴルムの集計（編成ごと・全波合算）
        var battles = new int[bnv][];
        var survWin = new long[bnv][];               // 勝った試行の生存数合計
        var wins = new int[bnv][];
        var soloWin = new int[bnv][];                // 生存1体での勝利
        // UnitTally.Add は LastActiveTurn を Math.Max で畳む（ターン番号は足せない）ので、
        // 「1戦あたりの生存T」は別に足し上げる。
        var aliveT = new long[bnv][];
        // 波ごとの還しの内訳（第三波の封じを見る）。[版][波]
        var wRefunds = new long[bnv, bnw];
        var wDeliver = new long[bnv, bnw];

        for (int v = 0; v < bnv; v++)
        {
            brate[v] = new double[bnb][];
            tal[v] = new UnitTally[bnb];
            battles[v] = new int[bnb];
            survWin[v] = new long[bnb];
            wins[v] = new int[bnb];
            soloWin[v] = new int[bnb];
            aliveT[v] = new long[bnb];

            for (int b = 0; b < bnb; b++)
            {
                brate[v][b] = new double[bnw];
                tal[v][b] = new UnitTally();
                bool golm = HasGolm(gulletBuilds[b].F);

                for (int w = 0; w < bnw; w++)
                {
                    int won = 0;
                    for (int seed = 0; seed < GulletSeeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                          seed, verbose: false, vers[v].Rule);
                        if (r.PlayerWon) won++;
                        if (!golm) continue;

                        battles[v][b]++;
                        if (r.PlayerWon)
                        {
                            wins[v][b]++;
                            survWin[v][b] += r.PlayerSurvivors;
                            if (r.PlayerSurvivors == 1) soloWin[v][b]++;
                        }
                        if (r.TallyByUnit.TryGetValue(UnitCatalog.Golm.Id, out UnitTally? g))
                        {
                            tal[v][b].Add(g);
                            aliveT[v][b] += g.LastActiveTurn;
                            wRefunds[v, w] += g.Refunds;
                            wDeliver[v, w] += g.Refunded;
                        }
                    }
                    brate[v][b][w] = won * 100.0 / GulletSeeds;
                }
            }
            Console.Error.WriteLine($"  {vers[v].Name} 完了");
        }

        double BAvg(int v, int b) => brate[v][b].Average();

        Console.WriteLine("# 腹という通貨 —— まどろみと還しの4版対照（gullet belly4 / 第36期）");
        Console.WriteLine();
        Console.WriteLine($"代表編成 {bnb} 行 × 全ステージ、seed 0..{GulletSeeds - 1}。診断用なので docs/ には置かない。");
        Console.WriteLine();
        foreach (var vv in vers) Console.WriteLine($"- **{vv.Name}**: {vv.Note}");

        // --- 検算 1: V0 が balance.md と一致するか ------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 検算1: V3 × 全編成（V3 ＝ 現在の既定）");
        Console.WriteLine();
        Console.WriteLine("**このセルは `docs/balance.md` と1つ残らず一致しなければならない。**");
        Console.WriteLine("第36期の採用手順で `ColossusRule.Default` が還し有効になったので、");
        Console.WriteLine("**検算の相手は V0 ではなく V3**（V0 は第35期までの盤面）。");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 |"))
                          + " V0（第35期まで） |");
        Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")) + "---|");
        for (int b = 0; b < bnb; b++)
            Console.WriteLine($"| {gulletBuilds[b].Name} |"
                + string.Concat(brate[2][b].Select(x => $" {x:F1}% |"))
                + " " + string.Join(" / ", brate[0][b].Select(x => $"{x:F1}")) + " |");

        // --- 検算 2: ゴルムを含まない行は動かないか ------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 検算2: ゴルムを含まない行の回帰");
        Console.WriteLine();
        Console.WriteLine("腹・まどろみ・還しはすべて巨躯の分岐の中にしかない。");
        Console.WriteLine("**ゴルム不在の行は全版 ±0.0 でなければならない**（停止条件）。");
        Console.WriteLine();
        var strays = new List<string>();
        int noGolm = 0;
        for (int b = 0; b < bnb; b++)
        {
            if (HasGolm(gulletBuilds[b].F)) continue;
            noGolm++;
            for (int v = 1; v < bnv; v++)
                for (int w = 0; w < bnw; w++)
                    if (Math.Abs(brate[v][b][w] - brate[0][b][w]) > 1e-9)
                        strays.Add($"{gulletBuilds[b].Name} / {vers[v].Name} / 第{w + 1}波: "
                                   + $"{brate[0][b][w]:F1}% → {brate[v][b][w]:F1}%");
        }
        Console.WriteLine($"ゴルム不在 {noGolm} 行 × {bnv - 1}版 × {bnw} 波 = {noGolm * (bnv - 1) * bnw} セル中、"
                          + $"**V0 と食い違ったセル {strays.Count} 件**。");
        foreach (string x in strays.Take(40)) Console.WriteLine($"- {x}");

        // --- 主表 --------------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 主表: ゴルムを含む行 × 各版（全波平均）");
        Console.WriteLine();
        Console.WriteLine("**第一波は全編成 100% なので平均は 20pt ぶん薄まっている**——動いた波は次節。");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(vers.Select(v => $" {v.Name} |"))
                          + string.Concat(dels.Select(d => $" {d.Item1} |")));
        Console.WriteLine("|---|" + string.Concat(vers.Select(_ => "---:|"))
                          + string.Concat(dels.Select(_ => "---:|")));
        var golmIdx = Enumerable.Range(0, bnb).Where(b => HasGolm(gulletBuilds[b].F)).ToList();
        foreach (int b in golmIdx)
            Console.WriteLine($"| {gulletBuilds[b].Name} |"
                + string.Concat(Enumerable.Range(0, bnv).Select(v => $" {BAvg(v, b):F1}% |"))
                + string.Concat(dels.Select(d => $" {BAvg(d.Item2, b) - BAvg(d.Item3, b):+0.0;-0.0}pt |")));
        Console.WriteLine($"| **平均** |"
            + string.Concat(Enumerable.Range(0, bnv).Select(v => $" **{golmIdx.Average(b => BAvg(v, b)):F1}%** |"))
            + string.Concat(dels.Select(d =>
                $" **{golmIdx.Average(b => BAvg(d.Item2, b)) - golmIdx.Average(b => BAvg(d.Item3, b)):+0.0;-0.0}pt** |")));

        // --- 波ごとの内訳 -------------------------------------------------------------------
        for (int dv = 2; dv <= 3; dv++)
        {
            Console.WriteLine();
            Console.WriteLine($"## 波ごとの内訳（V0 → {vers[dv].Name}）");
            Console.WriteLine();
            if (dv == 2)
            {
                Console.WriteLine("**第三波は渇き（回復禁止）の波**。還しは `ctx.Heal` を通るので、");
                Console.WriteLine("ここだけ V0 と1セルも違わないはず——動いていたら還しが `Heal` を通っていない。");
                Console.WriteLine();
            }
            Console.WriteLine("| 編成 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 |")));
            Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")));
            foreach (int b in golmIdx)
                Console.WriteLine($"| {gulletBuilds[b].Name} |"
                    + string.Concat(Enumerable.Range(0, bnw).Select(w =>
                        $" {brate[0][b][w]:F1} → {brate[dv][b][w]:F1} "
                        + $"({brate[dv][b][w] - brate[0][b][w]:+0.0;-0.0}) |")));
        }

        // --- 機構の発火 ---------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 機構の発火（ゴルムの集計 / 1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("`飲み込み` は肩代わりで腹に入った量、`眠り` はまどろんだ回数、");
        Console.WriteLine("`還し発火` は還しが走った戦の割合、`届いた` は**実際に味方の HP が増えた量**");
        Console.WriteLine("（額面ではない——渇き・支援拒否・満タンで消えた分は入らない）。");
        Console.WriteLine();
        Console.WriteLine("`落ちた/戦` は蘇生で戻って再び倒れると 2 になる（`還し発火` が 100% で頭打ちなのが");
        Console.WriteLine("**1戦1回の担保が効いている証拠**）。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 版 | 飲み込み | 眠り | 還し発火 | 届いた | 与ダメ(敵) | 生存T | 落ちた/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int b in golmIdx)
            for (int v = 0; v < bnv; v++)
            {
                double n = Math.Max(1, battles[v][b]);
                UnitTally t = tal[v][b];
                Console.WriteLine($"| {(v == 0 ? gulletBuilds[b].Name : "")} | {vers[v].Name} "
                    + $"| {t.Swallowed / n:F0} | {t.Slumbers / n:F2} | {t.Refunds * 100.0 / n:F0}% "
                    + $"| {t.Refunded / n:F1} | {t.DamageToEnemy / n:F0} "
                    + $"| {aliveT[v][b] / n:F1} | {t.Deaths / n:F2} |");
            }

        // --- 第三波の封じ -------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 還しは第三波で封じられるか（波ごと・ゴルム行の合算）");
        Console.WriteLine();
        Console.WriteLine("**第三波（渇きの祭司）だけ `届いた` が 0 になるはず。** 発火（`還し`）は起きていて、");
        Console.WriteLine("`ctx.Heal` の入口で止まる——「発火したが届かなかった」が渇きの課税の形。");
        Console.WriteLine();
        Console.WriteLine("| 版 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 還し/戦 | 第{i + 1}波 届いた/戦 |")));
        Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|---:|")));
        for (int v = 0; v < bnv; v++)
        {
            double n = Math.Max(1, golmIdx.Count * GulletSeeds);
            Console.Write($"| {vers[v].Name} |");
            for (int w = 0; w < bnw; w++)
                Console.Write($" {wRefunds[v, w] / n:F2} | {wDeliver[v, w] / n:F1} |");
            Console.WriteLine();
        }

        // --- 勝ち方の質 ---------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 勝ち方の質（ゴルム行・勝った試行だけ）");
        Console.WriteLine();
        Console.WriteLine("`残存` は勝った試行の平均生存数、`全滅勝ち` は生存1体での勝利の割合。");
        Console.WriteLine("**還しは勝率より先にここを動かすはず**（`chain` の見方）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率(ゴルム行平均) | 残存 | 全滅勝ち |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int v = 0; v < bnv; v++)
        {
            long w0 = golmIdx.Sum(b => (long)wins[v][b]);
            long s0 = golmIdx.Sum(b => survWin[v][b]);
            long q0 = golmIdx.Sum(b => (long)soloWin[v][b]);
            Console.WriteLine($"| {vers[v].Name} | {golmIdx.Average(b => BAvg(v, b)):F1}% "
                + $"| {(double)s0 / Math.Max(1, w0):F2} | {q0 * 100.0 / Math.Max(1, w0):F1}% |");
        }

        // --- 全波100% -----------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 全波 100% の編成数（壊れ検知）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 全波100% | 該当編成 |");
        Console.WriteLine("|---|--:|---|");
        for (int v = 0; v < bnv; v++)
        {
            var perfect = Enumerable.Range(0, bnb).Where(b => brate[v][b].All(x => x >= 100.0))
                                    .Select(b => gulletBuilds[b].Name).ToList();
            Console.WriteLine($"| {vers[v].Name} | {perfect.Count} | {string.Join(" / ", perfect)} |");
        }

        // --- まどろみは実際に売れたか（試験行・ログを数える）--------------------------------
        //
        // **ここはログの文字列を数えている**（gullet log / yoke log と同じ理由）。
        // 号令の買い取りも据えの買い取りも、盤面の値には「減った後の数字」しか残らないので、
        // 「その行が出たか」を数える以外に発火を捕まえる方法が無い。
        Console.WriteLine();
        Console.WriteLine("## まどろみは実際に売れたか（買い手を持つ行 / seed 0..49 × 全波）");
        Console.WriteLine();
        Console.WriteLine("号令（次のターン 攻撃+8）と据え（そのターン 被ダメ-50%）が、");
        Console.WriteLine("**ゴルムのまどろみを買った回数**。ドルガののろま・シガの自傷痺れが作る `IdleTurn` と");
        Console.WriteLine("混ざらないよう、買われた駒がゴルムである行だけを数える。");
        Console.WriteLine();
        Console.WriteLine("**号令は「次のターン」に払う。** 眠った同じターンにゴルムが倒れると売り物ごと消えるので、");
        Console.WriteLine("最後の列（眠った後も生きていた戦の割合）を並べて読む。");
        Console.WriteLine();
        const int SaleSeeds = 50;
        string gn = UnitCatalog.Golm.Name;

        // 買い手を持つゴルム行だけを verbose で回す（他の行には号令も据えもいないので、
        // 構造的に0件と分かっている。数えても情報が増えず時間だけ4倍になる）。
        var saleRows = golmIdx.Where(b =>
            gulletBuilds[b].F.Occupied().Any(o => o.Item2.Id == UnitCatalog.Gan.Id
                                               || o.Item2.Id == UnitCatalog.Ban.Id)).ToList();

        Console.WriteLine("| 編成 | 版 | まどろみ/戦 | 眠りを生き延びた | 号令→ゴルム | 据え→ゴルム | 号令→全体 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (int b in saleRows)
            foreach (int v in new[] { 0, 1, 3 })
            {
                int slumber = 0, rally = 0, bul = 0, rallyAll = 0, survived = 0, n = 0;
                for (int w = 0; w < bnw; w++)
                    for (int seed = 0; seed < SaleSeeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                          seed, verbose: true, vers[v].Rule);
                        n++;
                        // 眠ったターンごとに「その戦闘がその後も続き、ゴルムも生きていたか」を数える。
                        // 号令は**次のターン**に払うので、眠った直後に落ちる／決着すると売り物が消える。
                        var sleeps = new List<int>();
                        var golmDeaths = new List<int>();
                        int turn = 0, lastTurn = 0;
                        foreach (LogLine l in r.Log)
                        {
                            string t = l.Text;
                            if (l.Kind == LogKind.Turn) { turn++; lastTurn = turn; continue; }
                            if (t.Contains($"{gn} は腹が満ちてまどろんだ")) { slumber++; sleeps.Add(turn); }
                            else if (t.Contains($"の号令で {gn} の溜めが乗った")) rally++;
                            else if (t.Contains("の号令で")) rallyAll++;
                            else if (t.Contains($"据えが差し出した {gn} の被弾を")) bul++;
                            else if (t.Contains($"{gn} は倒れた")) golmDeaths.Add(turn);
                        }
                        survived += sleeps.Count(x => x < lastTurn && !golmDeaths.Contains(x));
                    }
                Console.WriteLine($"| {(v == 0 ? gulletBuilds[b].Name : "")} | {vers[v].Name} "
                    + $"| {(double)slumber / n:F2} | {(slumber == 0 ? 0 : survived * 100.0 / slumber):F0}% "
                    + $"| {(double)rally / n:F2} | {(double)bul / n:F2} | {(double)rallyAll / n:F2} |");
            }

        // --- ログ実例 -----------------------------------------------------------------------
        void Excerpt(string title, string note, string buildKey, int stage, int seed,
                     ColossusRule rule, string[] keys)
        {
            var (name, f) = gulletBuilds.First(x => x.Name.Contains(buildKey));
            BattleResult r = BattleEngine.Run(f, gulletStages[stage].Enemy, seed, verbose: true, rule);
            Console.WriteLine();
            Console.WriteLine($"### {title}");
            Console.WriteLine();
            Console.WriteLine(note);
            Console.WriteLine();
            Console.WriteLine($"{name} / 第{stage + 1}波 / seed {seed} / {(r.PlayerWon ? "勝利" : "敗北")} {r.Turns}ターン");
            Console.WriteLine();
            Console.WriteLine("```");
            string turn = "";
            foreach (LogLine l in r.Log)
            {
                if (l.Kind == LogKind.Turn) { turn = l.Text; continue; }
                if (!keys.Any(k => l.Text.Contains(k))) continue;
                if (turn.Length > 0) { Console.WriteLine(turn); turn = ""; }
                Console.WriteLine(l.Text);
            }
            Console.WriteLine("```");
        }

        Console.WriteLine();
        Console.WriteLine("## ログ実例");

        string golmName = UnitCatalog.Golm.Name;
        // A は**探して出す**。売却は眠りのうちの一部でしか起きないので、seed を決め打ちすると
        // 「起きなかった戦」を貼ることになる。探索は決定的（行→波→seed の昇順で最初の1件）。
        //
        // **試験行「腹×号令」は第36期の採用手順で compare から外した**（まどろみの棄却で
        // 目的を失ったため）。ここは買い手を持つ行を総当たりするので、行が消えても動く。
        {
            string want = $"の号令で {golmName} の溜めが乗った";
            (int B, int W, int S) hit = (-1, -1, -1);
            foreach (int b in saleRows)
            {
                for (int w = 0; w < bnw && hit.B < 0; w++)
                    for (int seed = 0; seed < GulletSeeds && hit.B < 0; seed++)
                    {
                        BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                          seed, verbose: true, vers[3].Rule);
                        if (r.Log.Any(l => l.Text.Contains(want))) hit = (b, w, seed);
                    }
                if (hit.B >= 0) break;
            }
            if (hit.B >= 0)
                Excerpt("A. まどろみが売れる（V4 / 売却の起きた最初の戦）",
                    "腹が満ちて手番を失い、**号令が次のターンの攻撃を買う**。",
                    gulletBuilds[hit.B].Name, hit.W, hit.S, vers[3].Rule,
                    new[] { "腹が満ちてまどろんだ", $"の号令で {golmName}",
                            $"据えが差し出した {golmName}", "が飲み込んだものが還った",
                            $"{golmName} は倒れた" });
            else
                Console.WriteLine("### A. まどろみが売れる —— **1件も見つからなかった**");
        }

        Excerpt("B. 第三波では還しが封じられる（V3 / 渇きの祭司）",
            "**発火はしているのに `届いた 0`。** `ctx.Heal` の入口で渇きが止めている"
            + "（規則は engine の1箇所のまま、特性側は判定を1文字も持っていない）。",
            "置き去り×被弾強化", 2, 0, vers[2].Rule,
            new[] { "が飲み込んだものが還った", "は倒れた" });

        // D は**探して出す**。第三波は「渇きの祭司が生きている間だけ」封じられるので、
        // 祭司を先に割った戦では還しが解禁される——これが `spread` で
        // 第三波の勝率が 18行中1行だけ動いた（逆しま +1.5pt）の正体。
        // **渇きが「回避可能な課税」として働いた初の実例**なので、診断に据えて再現可能にしてある。
        {
            (int B, int S) hitD = (-1, -1);
            foreach (int b in golmIdx)
            {
                for (int seed = 0; seed < GulletSeeds && hitD.B < 0; seed++)
                {
                    BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[2].Enemy,
                                                      seed, verbose: true, vers[2].Rule);
                    if (r.Log.Any(l => l.Text.Contains("が飲み込んだものが還った")
                                       && !l.Text.Contains("/ 届いた 0）"))) hitD = (b, seed);
                }
                if (hitD.B >= 0) break;
            }
            if (hitD.B >= 0)
                Excerpt("D. 第三波でも、祭司を先に割れば還しは解禁される（V3）",
                    "渇きは**保持者が生きている間だけ**効く（`ctx.Heal` の入口で `Drought` の生存を見る）。"
                    + "祭司を先に倒した戦では回復が戻るので、還しが届く。"
                    + "**渇きが「回避可能な課税」として働いた初の実例。**",
                    gulletBuilds[hitD.B].Name, 2, hitD.S, vers[2].Rule,
                    new[] { "が飲み込んだものが還った", "渇きの祭司 は倒れた",
                            $"{golmName} は倒れた" });
            else
                Console.WriteLine("### D. 祭司を先に割った戦 —— **1件も見つからなかった**");
        }

        Excerpt("C. 同じ行の第五波では届く（V3 / 渇きなし）",
            "同じ編成・同じ規則で、渇きの無い波なら回復が通る。**Bとの差は波だけ。**",
            "置き去り×被弾強化", 4, 0, vers[2].Rule,
            new[] { "が飲み込んだものが還った", "は倒れた" });
        return;
    }

    // ---- belly: 腹の規模の実測（第36期 Phase 0-1）------------------------------------------
    //
    // まどろみの閾値 N と還し率を、掃引ではなく**現行盤面の実測から**導くための表。
    // 盤面は1つも動かさない（腹のカウンタは V0 では誰も読まない純粋な記録）。
    //
    //     dotnet run --project BattleSim -c Release 0 gullet belly
    if (gulletMode == "belly")
    {
        // 1戦ぶんの記録。飲み込み量は UnitTally.Swallowed（ApplyDamage の blocked と同額）。
        var rows = new List<(string Build, int Wave, int Swallowed, int Deaths, int AliveT, int Turns)>();

        for (int b = 0; b < gulletBuilds.Length; b++)
        {
            if (!HasGolm(gulletBuilds[b].F)) continue;
            for (int w = 0; w < gulletStages.Count; w++)
                for (int seed = 0; seed < GulletSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                      seed, verbose: false);
                    r.TallyByUnit.TryGetValue(UnitCatalog.Golm.Id, out UnitTally? g);
                    rows.Add((gulletBuilds[b].Name, w + 1, g?.Swallowed ?? 0, g?.Deaths ?? 0,
                              g?.LastActiveTurn ?? 0, r.Turns));
                }
        }

        static double Median(List<int> xs)
        {
            if (xs.Count == 0) return 0;
            var v = xs.OrderBy(x => x).ToList();
            return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2.0;
        }

        Console.WriteLine("# 腹の規模の実測（gullet belly / 第36期 Phase 0-1）");
        Console.WriteLine();
        Console.WriteLine($"ゴルムを含む編成 × 全ステージ、seed 0..{GulletSeeds - 1}。**現行の盤面のまま**測っている");
        Console.WriteLine("（腹のカウンタは `ApplyDamage` の巨躯の分岐で `blocked` を積むだけで、誰も読まない）。");
        Console.WriteLine();
        Console.WriteLine("`飲み込み` は1戦あたりの肩代わり吸収量の合計（吐き戻しが返した元の量と同額）。");
        Console.WriteLine("`生存T` はゴルムが最後に生きていたターン、`落ちた` は倒れた戦の割合。");

        // --- 表1: 行 × 波 -------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 表1: 行 × 波（平均 / 中央値 / 最大）");
        Console.WriteLine();
        Console.WriteLine("**第一波はチュートリアル波（全編成 100%）なので N の導出には使わない。**");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, gulletStages.Count).Select(i => $" 第{i}波 |")));
        Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")));
        foreach (var g in rows.GroupBy(x => x.Build))
        {
            Console.Write($"| {g.Key} |");
            for (int w = 1; w <= gulletStages.Count; w++)
            {
                var xs = g.Where(x => x.Wave == w).Select(x => x.Swallowed).ToList();
                Console.Write($" {xs.Average():F0} / {Median(xs):F0} / {xs.Max()} |");
            }
            Console.WriteLine();
        }

        // --- 表2: 行ごとの集約（第2〜5波）---------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 表2: 行ごとの集約（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`落ちた戦の飲み込み` が**還しの原資の目安**（ゴルムが倒れた戦だけの平均）。");
        Console.WriteLine("`N=x で眠る回数` は `min(floor(飲み込み / N), 生存T)` を1戦ずつ数えた平均——");
        Console.WriteLine("**盤面は動かしていない**（腹は単調に増え、眠るたび N 引かれるので回数は算術で決まる）。");
        Console.WriteLine("生存Tで頭を打つのは、眠るには手番が回ってくる必要があるため。");
        Console.WriteLine();
        int[] cands = { 20, 30, 40, 60, 80, 120 };
        Console.WriteLine("| 編成 | 平均 | 中央値 | 最大 | 生存T | 落ちた | 落ちた戦の飲み込み |"
                          + string.Concat(cands.Select(n => $" N={n} |")));
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|" + string.Concat(cands.Select(_ => "--:|")));
        var late = rows.Where(x => x.Wave >= 2).ToList();
        foreach (var g in late.GroupBy(x => x.Build))
        {
            var xs = g.Select(x => x.Swallowed).ToList();
            var died = g.Where(x => x.Deaths > 0).Select(x => x.Swallowed).ToList();
            Console.Write($"| {g.Key} | {xs.Average():F0} | {Median(xs):F0} | {xs.Max()} "
                + $"| {g.Average(x => x.AliveT):F1} | {g.Count(x => x.Deaths > 0) * 100.0 / g.Count():F0}% "
                + $"| {(died.Count == 0 ? 0 : died.Average()):F0} |");
            foreach (int n in cands)
                Console.Write($" {g.Average(x => Math.Min(x.Swallowed / n, x.AliveT)):F2} |");
            Console.WriteLine();
        }

        // --- 表3: 全体 ----------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 表3: 全体（第2〜5波・ゴルム13行をまとめて）");
        Console.WriteLine();
        var all = late.Select(x => x.Swallowed).ToList();
        var allDied = late.Where(x => x.Deaths > 0).Select(x => x.Swallowed).ToList();
        Console.WriteLine($"- 戦数: {late.Count}");
        Console.WriteLine($"- 飲み込み: 平均 **{all.Average():F0}** / 中央値 **{Median(all):F0}** / 最大 {all.Max()}");
        Console.WriteLine($"- ゴルムの生存T: 平均 {late.Average(x => x.AliveT):F1}（決着T 平均 {late.Average(x => x.Turns):F1}）");
        Console.WriteLine($"- 落ちた戦: {allDied.Count * 100.0 / late.Count:F0}%、その戦の飲み込み 平均 **{(allDied.Count == 0 ? 0 : allDied.Average()):F0}** / 中央値 {Median(allDied):F0}");
        Console.WriteLine();
        Console.WriteLine($"参考: ノノの繕い1回は {MenderTrait.Amount} 点（`MenderTrait.Amount`）。");
        Console.WriteLine("還し率は「腹の残り × 率」が繕い1〜2回ぶん（14〜28）になる規模を採る。");
        return;
    }

    // ---- log: 1戦ずつの監査 --------------------------------------------------------------
    //
    // **ここだけはログの文字列を数えている。** UI は LogKind を見るという規約（README）に
    // 反しているように見えるが、確かめたいのは「その行が出たか／出なかったか」そのもので、
    // 盤面の値では代用できない（発火しなかったことは数値に痕跡を残さない）。
    if (gulletMode == "log")
    {
        const string Blocked = "の前に立ちはだかる";
        const string Regurg = "飲み込んだ力を";

        void Audit(string title, string buildKey, int stage, int seed, string[] watch)
        {
            var (name, f) = gulletBuilds.First(b => b.Name.Contains(buildKey));
            BattleResult r = BattleEngine.Run(f, gulletStages[stage].Enemy, seed,
                                              verbose: true, ColossusRule.Default);

            var text = r.Log.Select(l => l.Text).ToList();
            int blocked = text.Count(t => t.Contains(Blocked));
            int regurg = text.Count(t => t.Contains(Regurg));

            Console.WriteLine();
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine($"{name} / 第{stage + 1}波 / seed {seed} / {(r.PlayerWon ? "勝利" : "敗北")} {r.Turns}ターン");
            Console.WriteLine();
            Console.WriteLine($"- 立ちはだかった: **{blocked} 回**");
            Console.WriteLine($"- 吐き戻した: **{regurg} 回**（差 {blocked - regurg} = source が null の刻み等）");

            // 受け手の内訳。ゴルム自身がここに出てはいけない（**ゴルムは育たない**）。
            Console.WriteLine("- 受け手の内訳:");
            foreach (var (_, def) in f.Occupied())
            {
                int n = text.Count(t => t.Contains($"{Regurg} {def.Name} へ返した"));
                if (n > 0 || def.Id == UnitCatalog.Golm.Id)
                    Console.WriteLine($"    - {def.Name}: {n} 回"
                        + (def.Id == UnitCatalog.Golm.Id ? "  ← **0 でなければならない**" : ""));
            }

            // 攻撃力の推移は StatSnapshot（文字列ではなく構造化イベント）から取る。
            // InstanceId は Deploy の順に振られるので、味方はスロット昇順で 0 から数えれば引ける。
            var idOf = new Dictionary<string, int>();
            int id = 0;
            foreach (var (_, def) in f.Occupied()) idOf[def.Name] = id++;

            foreach (string w in watch)
            {
                var (_, def) = f.Occupied().First(o => o.Item2.Name.Contains(w));
                var series = r.Events
                    .Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == idOf[def.Name])
                    .Select(e => e.Amount).ToList();
                Console.WriteLine($"- {def.Name} の攻撃力（素 {def.Attack}）: "
                    + string.Join(" → ", series));
            }
        }

        Console.WriteLine("# 吐き戻しの監査（gullet log）");
        Console.WriteLine();
        Console.WriteLine("受け入れ基準 3〜5 を1戦ずつ確かめる。規則は既定（`ColossusRule.Default`）。");

        // 3. ウツに対しては強化が弱体として働く（Perverse は AtkBonus > 0 で攻撃が半減する）
        Audit("A. ウツへの吐き戻しは弱体として働くか", "逆しま (ネル×ウツ)", 4, 0,
              new[] { "ウツ", "ゴルム" });

        // 4. 毒・燃焼の刻み（source が null）では発火しない。
        //    立ちはだかりは刻みでも起きるので、**2つの回数の差**がそのまま除外できた件数になる。
        Audit("B. 毒の刻みでは発火しないか", "追撃×毒 (ハギ×グザ)", 2, 0,
              new[] { "グザ", "ゴルム" });

        // 5. 燃料の経路（第19期 route の対象編成）
        Audit("C. 燃料はムドまで届くか", "置き去り×被弾強化", 4, 0,
              new[] { "ムド", "ゴルム" });
        return;
    }

    // ---- 版の並び ------------------------------------------------------------------------
    (string Name, string Note, ColossusRule Rule)[] versions;
    (string Label, int Hi, int Lo)[] deltas;
    int detail;   // 「波ごとの内訳」で並べる版（先頭版 → この版）

    if (gulletMode == "gain")
    {
        // 上がりすぎたときに最初に振るノブ（計画 §5 の失敗の形）。
        // **肩代わり率は 90% に固定**して、返す効率だけを動かす。
        versions = new (string, string, ColossusRule)[]
        {
            ("g∞ 返し無し", "90% / 吐き戻し無し", new ColossusRule(90, 4, Regurgitate: false)),
            ("g2",  "90% / 2点につき攻撃+1", new ColossusRule(90, 2, Regurgitate: true)),
            ("g4",  "90% / 4点につき攻撃+1（既定）", new ColossusRule(90, 4, Regurgitate: true)),
            ("g6",  "90% / 6点につき攻撃+1", new ColossusRule(90, 6, Regurgitate: true)),
            ("g8",  "90% / 8点につき攻撃+1", new ColossusRule(90, 8, Regurgitate: true)),
        };
        deltas = new[] { ("g4−無", 2, 0), ("g6−無", 3, 0), ("g8−無", 4, 0) };
        detail = 2;
    }
    else
    {
        versions = new (string, string, ColossusRule)[]
        {
            ("V0 現行",     "90% / 吐き戻し無し",
                new ColossusRule(90, ColossusTrait.DamagePerGain, Regurgitate: false)),
            ("V1 吐き戻し", "90% / 吐き戻し有り（本命＝既定）",
                new ColossusRule(90, ColossusTrait.DamagePerGain, Regurgitate: true)),
            ("V2 60%",      "60% / 吐き戻し無し",
                new ColossusRule(60, ColossusTrait.DamagePerGain, Regurgitate: false)),
            ("V3 60%+返し", "60% / 吐き戻し有り",
                new ColossusRule(60, ColossusTrait.DamagePerGain, Regurgitate: true)),
        };
        deltas = new[] { ("V1−V0", 1, 0), ("V3−V2", 3, 2) };
        detail = 1;
    }

    // 燃料の行き先を見る先。route（第19期）が未解決のまま置いていった編成。
    const string FuelBuild = "置き去り×被弾強化";

    int nv = versions.Length, nb = gulletBuilds.Length, nw = gulletStages.Count;

    var rate = new double[nv][][];                 // rate[版][編成][波] = 勝率(%)
    var fuel = new UnitTally[nv];                  // FuelBuild のムドだけ集計する
    var fuelGolm = new UnitTally[nv];
    var fuelBattles = new int[nv];
    var fuelTurns = new long[nv];

    for (int v = 0; v < nv; v++)
    {
        rate[v] = new double[nb][];
        fuel[v] = new UnitTally();
        fuelGolm[v] = new UnitTally();

        for (int b = 0; b < nb; b++)
        {
            rate[v][b] = new double[nw];
            bool track = gulletBuilds[b].Name == FuelBuild;

            // gain 版は検算（ゴルム不在の編成が動かないこと）を既に既定版で済ませているので、
            // ゴルム入りだけを回す。全編成を回しても結果は同じで、時間だけ4倍かかる。
            if (gulletMode == "gain" && !HasGolm(gulletBuilds[b].F)) continue;

            for (int w = 0; w < nw; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < GulletSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                      seed, verbose: false, versions[v].Rule);
                    if (r.PlayerWon) wins++;
                    if (!track) continue;

                    fuelBattles[v]++;
                    fuelTurns[v] += r.Turns;
                    if (r.TallyByUnit.TryGetValue(UnitCatalog.Mudo.Id, out UnitTally? mt)) fuel[v].Add(mt);
                    if (r.TallyByUnit.TryGetValue(UnitCatalog.Golm.Id, out UnitTally? gt)) fuelGolm[v].Add(gt);
                }
                rate[v][b][w] = wins * 100.0 / GulletSeeds;
            }
        }
        Console.Error.WriteLine($"  {versions[v].Name} 完了");
    }

    double Avg(int v, int b) => rate[v][b].Average();

    Console.WriteLine("# 巨躯の吐き戻し（gullet）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 × 全ステージ、seed 0..{GulletSeeds - 1}。診断用なので docs/ には置かない。");
    Console.WriteLine();
    foreach (var vv in versions) Console.WriteLine($"- **{vv.Name}**: {vv.Note}");

    if (gulletMode != "gain")
    {
        // --- 検算 1: V0 が現行の balance.md と一致するか -------------------------------
        Console.WriteLine();
        Console.WriteLine("## 検算: V0 × 全編成");
        Console.WriteLine();
        Console.WriteLine("**このセルは `docs/balance.md`（吐き戻し導入前の世代）と一致しなければならない。**");
        Console.WriteLine("ずれていたら診断の組み方（seed 帯・台・編成リスト）が balance.md と揃っていない。");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")));
        for (int b = 0; b < nb; b++)
            Console.WriteLine($"| {gulletBuilds[b].Name} |"
                + string.Concat(rate[0][b].Select(x => $" {x:F1}% |")));

        // --- 検算 2: ゴルムを含まない編成は動かないか ----------------------------------
        Console.WriteLine();
        Console.WriteLine("## 検算: ゴルムを含まない編成");
        Console.WriteLine();
        Console.WriteLine("吐き戻しは巨躯の分岐の中にしかないので、**ゴルム不在の編成は全波 ±0.0 でなければならない**");
        Console.WriteLine("（受け入れ条件）。ここが 0 件でなければ、規則が意図しない場所から漏れている。");
        Console.WriteLine();
        var strays = new List<string>();
        int noGolm = 0;
        for (int b = 0; b < nb; b++)
        {
            if (HasGolm(gulletBuilds[b].F)) continue;
            noGolm++;
            for (int v = 1; v < nv; v++)
                for (int w = 0; w < nw; w++)
                    if (Math.Abs(rate[v][b][w] - rate[0][b][w]) > 1e-9)
                        strays.Add($"{gulletBuilds[b].Name} / {versions[v].Name} / 第{w + 1}波: "
                                   + $"{rate[0][b][w]:F1}% → {rate[v][b][w]:F1}%");
        }
        Console.WriteLine($"ゴルム不在 {noGolm} 編成 × {nv - 1}版 × {nw} 波 = {noGolm * (nv - 1) * nw} セル中、"
                          + $"**V0 と食い違ったセル {strays.Count} 件**。");
        if (strays.Count > 0)
        {
            Console.WriteLine();
            foreach (string x in strays.Take(40)) Console.WriteLine($"- {x}");
        }
    }

    // --- 主表: ゴルム入りの編成 -----------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 主表: ゴルムを含む編成 × 各版");
    Console.WriteLine();
    Console.WriteLine("セルは全波の単純平均。**第一波は全編成 100% なので平均は 20pt ぶん薄まっている**");
    Console.WriteLine("——動いた波は次節で見る。");
    Console.WriteLine();
    Console.WriteLine("**予測は両方向。** 逆しま系3本は落ちる（`Perverse` は `AtkBonus > 0` で攻撃が半減する");
    Console.WriteLine("＝ウツにとって吐き戻しは毒）／死の連鎖系は上がる。**片方向しか出なければ、");
    Console.WriteLine("予測の立て方かウツの扱いを読み違えている。**");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(versions.Select(v => $" {v.Name} |"))
                      + string.Concat(deltas.Select(d => $" {d.Label} |")) + " ゴルムの席 |");
    Console.WriteLine("|---|" + string.Concat(versions.Select(_ => "---:|"))
                      + string.Concat(deltas.Select(_ => "---:|")) + "---|");
    for (int b = 0; b < nb; b++)
    {
        Formation f = gulletBuilds[b].F;
        if (!HasGolm(f)) continue;
        string seat = string.Join("", f.Occupied()
            .Where(o => o.Item2.Id == UnitCatalog.Golm.Id)
            .Select(o => FormationRules.SeatNames[o.Item1]));
        Console.WriteLine($"| {gulletBuilds[b].Name} |"
            + string.Concat(Enumerable.Range(0, nv).Select(v => $" {Avg(v, b):F1}% |"))
            + string.Concat(deltas.Select(d => $" {Avg(d.Hi, b) - Avg(d.Lo, b):+0.0;-0.0}pt |"))
            + $" {seat} |");
    }

    // --- 波ごとの内訳 ---------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine($"## 波ごとの内訳（{versions[0].Name} → {versions[detail].Name}）");
    Console.WriteLine();
    Console.WriteLine("**第三波は第22期に渇きを置いて初めて分離した波**なので、ここが天井へ押し上げられて");
    Console.WriteLine("いないかを併せて見る（波の分離度は `spread` の側で測り直す）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")));
    for (int b = 0; b < nb; b++)
    {
        if (!HasGolm(gulletBuilds[b].F)) continue;
        Console.WriteLine($"| {gulletBuilds[b].Name} |"
            + string.Concat(Enumerable.Range(0, nw).Select(w =>
                $" {rate[0][b][w]:F1} → {rate[detail][b][w]:F1} "
                + $"({rate[detail][b][w] - rate[0][b][w]:+0.0;-0.0}) |")));
    }

    // --- 燃料の行き先 ---------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine($"## 燃料の行き先（{FuelBuild} のムド）");
    Console.WriteLine();
    Console.WriteLine("第19期 `route` の未解決がここで解ける、というのが主眼。**勝率ではなく");
    Console.WriteLine("`ムド 与ダメ(敵)` が上がるかを先に見る。** 動かないなら、吐き戻しの量が小さすぎるか、");
    Console.WriteLine("ムドが先に落ちている（`落ちた` 列と合わせて読む）。");
    Console.WriteLine();
    Console.WriteLine("`ゴルム 与ダメ(敵)` は**ゴルム自身が育っていないこと**の検算。吐き戻しは");
    Console.WriteLine("守った相手にしか返さないので、版をまたいで大きく動いてはいけない。");
    Console.WriteLine();
    Console.WriteLine("**`決着T` を必ず並べて読む。** 数字は1戦あたりの平均なので、");
    Console.WriteLine("早く決着するようになると与ダメの総量は据え置きに見える（1ターンあたりでは増えている）。");
    Console.WriteLine();
    Console.WriteLine("| 版 | ムド 与ダメ(敵) | ムド 与ダメ/T | ムド 撃破 | ムド 被(味) | ムド 被ダメ | ムド 落ちた | ゴルム 与ダメ(敵) | ゴルム 被(味) | 決着T |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int v = 0; v < nv; v++)
    {
        double n = Math.Max(1, fuelBattles[v]);
        double turns = Math.Max(1, fuelTurns[v]);
        Console.WriteLine($"| {versions[v].Name} | {fuel[v].DamageToEnemy / n:F0} "
            + $"| {fuel[v].DamageToEnemy / turns:F2} | {fuel[v].Kills / n:F2} "
            + $"| {fuel[v].TakenFromAlly / n:F0} | {fuel[v].DamageTaken / n:F0} | {fuel[v].Deaths / n:F2} "
            + $"| {fuelGolm[v].DamageToEnemy / n:F0} | {fuelGolm[v].TakenFromAlly / n:F0} "
            + $"| {fuelTurns[v] / n:F1} |");
    }

    // --- 全5波 100% の編成数 ---------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 全波 100% の編成数");
    Console.WriteLine();
    Console.WriteLine("`spread` の (1) と同じ見方。増えていたら上がりすぎ——まず `DamagePerGain` を");
    Console.WriteLine("4 → 6, 8 と振る（`gullet gain`。ゴルムの数値 150/10/3 は触らない）。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 全波100% | 該当編成 |");
    Console.WriteLine("|---|--:|---|");
    for (int v = 0; v < nv; v++)
    {
        var perfect = Enumerable.Range(0, nb)
            .Where(b => (gulletMode != "gain" || HasGolm(gulletBuilds[b].F)) && rate[v][b].All(x => x >= 100.0))
            .Select(b => gulletBuilds[b].Name).ToList();
        Console.WriteLine($"| {versions[v].Name} | {perfect.Count} | {string.Join(" / ", perfect)} |");
    }
    return;
}
}
