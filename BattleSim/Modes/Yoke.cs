using BattleCore;
using static Common;

// =====================================================================================
// yoke モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "yoke")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 yoke
// =====================================================================================

static class YokeDiag
{
// yoke モード: 第四波に「軛」を置く（第25期）。1回のダメージ量に上限を課す盤面ルール。
//
// 第四波は 100% が 21/35・中間帯 7 で、**第一波を除けば最も飽和している波**だった。
// 第22期 spread で作った物差しの上で、第三波を渇き（回復禁止）で分離させたのと同じことをここでやる。
// 課金する資源は「**1発の重さ**」——第二波（後列到達力）・第三波（持続）・第五波（総合）の
// どれとも重ならない。
//
// **敵側の打点は全部 15 以下**（重装 12・詠唱兵の溜め 16・従軍司祭 9）なので、
// この波で課税されるのは味方の大打点だけ（ドルガ38・カドの反撃・セロの狙撃・墓守の層）。
// 「硬いので大打点で押し切れない」は第四波の既存の性格と一貫していて、新しい教え事が要らない。
//
// **版は 5 つ。** 中央の1枚と Cap だけを動かす（gullet と同じく規則は引数で渡す）。
//
//     V0 現行    中央 城塞の重装兵。**差し替え前の docs/balance.md と一致するはず＝検算**
//     V1 壁のみ  中央 軛の重装兵・規則は無効。**数値が同一なので V0 と一致するはず＝検算**
//     V2 上限25  本命（採用した規則＝YokeTrait.Cap）
//     V3 上限30  上限を緩めた側
//     V4 上限20  上限を締めた側
//
// **Cap は計画（15）ではなく `yoke sweep` の実測で 25 に決めた。** 12〜50 を振ると、
// 15 では 16編成・20 でも 12編成が 0% に落ち、第四波の平均が第五波（59.8）を下回る
// ——「波を分離する」ではなく「波を壁にする」になっていた。帯の選び方は sweep の側を見ること。
//
// **V0 と V1 の対照が要。** 逆位（第20期）は保持者の数値を壁から動かしたせいで
// 「壁が変わったのか、ルールが効いたのか」の切り分けに追加測定が要った。ここでは
// 数値を1つも動かしていないので、V1 が V0 と1セルも違わないことがそのまま切り分けになる。
//
// **判定に使う編成は中間帯を持つものから選ぶ**（第24期 yield の教訓）。飽和した台では
// 誰に何をしても増分が決着の短縮に消える。主表には `中` 列を出して、V0 の第四波が
// 5% < x < 95% の編成を印してある——**100% に張り付いている編成は「落ちたかどうか」だけを見る。**
//
// 機構の確認は**ログの文字列ではなく tally** で行う。`敵被ダメ/戦`（＝味方の出力の総量）が
// V1 → V2 でどれだけ削られたかが、そのまま「切られた量」になる。
// `yoke log` は §2.3 の監査（破片・肩代わり・棘守り・惨禍・毒の刻み）で、
// **そちらだけはログの文字列を数えている**（gullet log と同じ理由——「その行がどの順で出たか」
// そのものを見たいので、盤面の値では代用できない）。
//
// docs/ には置かない（診断用）。
//
//     dotnet run --project BattleSim -c Release 0 yoke [sweep|log]
public static void Run(string[] args, int stageIndex)
{
    string yokeMode = args.Length > 2 ? args[2] : "";
    var yokeBuilds = CompareBuilds();
    const int YokeSeeds = 200;   // compare / spread / gullet と同じ。balance.md と突き合わせる
    const int Wave4 = 3;         // 第四波（0 起点）

    // 第四波の中央だけを差し替えた版を診断のローカルで組む（gradient / aim と同じ扱い）。
    // 残り4枠も他の4波も EnemyCatalog のまま——**動く変数は中央の1枚と Cap だけ。**
    Formation Wave4With(UnitDef center) => Formation.Build(
        front1: EnemyCatalog.Warden, front3: EnemyCatalog.Warden, center: center,
        back1: EnemyCatalog.Chanter, back3: EnemyCatalog.Priest);

    // 第四波の敵の Def.Id。tally を敵味方に割るのに使う（味方の召喚駒まで正しく味方側に落ちる）。
    var wave4EnemyIds = new HashSet<string>(new[]
    {
        EnemyCatalog.Warden.Id, EnemyCatalog.Yoker.Id, EnemyCatalog.Chanter.Id, EnemyCatalog.Priest.Id
    });

    // ---- map: 上限の地図（第132期 段1）--------------------------------------------------------
    //
    // **測定だけ。既定（`YokeRule.Default`）は1ビットも触らない。**
    //
    // 第25期に軛を採ってから、「何が何回・何点切られたか」を数える窓口が1つも無かった。
    // そのせいで「型ごとに上限との相性が逆を向く」が**第129〜131期の指示書に3回書き継がれた
    // まま未測定**だった（第131期に指摘されて落ちた）。ここで地図を埋める。
    //
    // **版は3つ。保持者は3版とも盤上に生きている**（第25期の V0/V1 と同じ作法で、
    // 動く変数を `Cap` 1つに絞る）:
    //
    //     V1 上限あり  Cap 25（現行）
    //     V2 上限なし  Cap 1,000,000（**保持者はそのまま**。切られないだけ）
    //     V3 規則off   Active=false。**V2 と1セルも違わないはず＝検算**
    //
    // 帳簿は `BattleResult.Yoke`（`YokeLedger`）。V2 でも `YokeBinding` は真なので
    // **「切られなかった世界の名目量」が同じ器具で取れる**——これが無いと
    // 「切られた量」の分母が版で動く（第115期「同じ比を作る2つの計数は同じ瞬間に取る」）。
    if (yokeMode == "map")
    {
        int ymNb = yokeBuilds.Length;
        Formation wave4map = EnemyCatalog.Stages[Wave4].Enemy;
        var vers = new (string Name, YokeRule Rule)[]
        {
            ("V1 上限あり (Cap 25)", YokeRule.Default),
            ("V2 上限なし (Cap 1,000,000)", new YokeRule(1_000_000, Active: true)),
            ("V3 規則 off (Active=false)", new YokeRule(YokeTrait.Cap, Active: false)),
        };
        int ymNv = vers.Length;

        long[][] cutHits = new long[ymNv][], cutLost = new long[ymNv][], cutPassed = new long[ymNv][],
                 near = new long[ymNv][], inHits = new long[ymNv][], inAmt = new long[ymNv][],
                 kills = new long[ymNv][], over = new long[ymNv][];
        long[] armor = new long[ymNv], relH = new long[ymNv], relA = new long[ymNv],
               burnH = new long[ymNv], burnA = new long[ymNv], levyH = new long[ymNv], levyA = new long[ymNv],
               direct = new long[ymNv], cutPlayerH = new long[ymNv], cutPlayerL = new long[ymNv],
               cutFoeH = new long[ymNv], cutFoeL = new long[ymNv];
        var cutBy = new Dictionary<string, (long Hits, long Lost)>[ymNv];
        var winRate = new double[ymNv][];
        var turnAvg = new double[ymNv][];
        for (int v = 0; v < ymNv; v++)
        {
            cutHits[v] = new long[10]; cutLost[v] = new long[10]; cutPassed[v] = new long[10];
            near[v] = new long[10]; inHits[v] = new long[10]; inAmt[v] = new long[10];
            kills[v] = new long[10]; over[v] = new long[10];
            cutBy[v] = new Dictionary<string, (long, long)>();
            winRate[v] = new double[ymNb]; turnAvg[v] = new double[ymNb];
        }
        // 行ごとの型内訳（V1 の「敵に入った量」で割る）。**分類は測定から引く。手で分けない。**
        var rowIn = new long[ymNb][];
        for (int b = 0; b < ymNb; b++) rowIn[b] = new long[5];

        for (int v = 0; v < ymNv; v++)
        {
            for (int b = 0; b < ymNb; b++)
            {
                int wins = 0; long tsum = 0;
                for (int seed = 0; seed < YokeSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(yokeBuilds[b].F, wave4map, seed, verbose: false, null, vers[v].Rule);
                    if (r.PlayerWon) wins++;
                    tsum += r.Turns;
                    YokeLedger y = r.Yoke;
                    for (int i = 0; i < 10; i++)
                    {
                        cutHits[v][i] += y.CutHits[i]; cutLost[v][i] += y.CutLost[i];
                        cutPassed[v][i] += y.CutPassed[i]; near[v][i] += y.NearHits[i];
                        inHits[v][i] += y.InHits[i]; inAmt[v][i] += y.InAmount[i];
                        kills[v][i] += y.Kills[i]; over[v][i] += y.Overkill[i];
                    }
                    if (v == 0) for (int i = 0; i < 5; i++) rowIn[b][i] += y.InAmount[i];
                    armor[v] += y.ArmorSoak; direct[v] += y.DirectHpLoss;
                    relH[v] += y.InRelayedHits; relA[v] += y.InRelayedAmount;
                    burnH[v] += y.InBurnHits; burnA[v] += y.InBurnAmount;
                    levyH[v] += y.InLevyHits; levyA[v] += y.InLevyAmount;
                    cutPlayerH[v] += y.CutOnPlayerHits; cutPlayerL[v] += y.CutOnPlayerLost;
                    cutFoeH[v] += y.CutOnEnemyHits; cutFoeL[v] += y.CutOnEnemyLost;
                    foreach (var kv in y.CutBy)
                    {
                        cutBy[v].TryGetValue(kv.Key, out var acc);
                        cutBy[v][kv.Key] = (acc.Hits + kv.Value.Hits, acc.Lost + kv.Value.Lost);
                    }
                }
                winRate[v][b] = wins * 100.0 / YokeSeeds;
                turnAvg[v][b] = tsum / (double)YokeSeeds;
            }
            Console.Error.WriteLine("  " + vers[v].Name + " 完了");
        }

        string[] patName = { "単体", "薙ぎ", "貫き", "全体", "型なし" };
        double Per(long x) => x / (double)(ymNb * YokeSeeds);

        Console.WriteLine("# 上限の地図（yoke map・第132期 段1）");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` {ymNb} 行 × 第四波 × seed 0..{YokeSeeds - 1} = {ymNb * YokeSeeds:N0} 戦 × 3 版。");
        Console.WriteLine("**測定だけ。`YokeRule` の既定は1ビットも触っていない。**");
        Console.WriteLine();
        Console.WriteLine("**3版とも保持者（軛の重装兵）は盤上に生きている**——動く変数は `Cap` だけ（第25期の V0/V1 と同じ作法）。");
        Console.WriteLine("V2 でも帳簿は回るので、**「切られなかった世界の名目量」が同じ器具で取れる**。");
        Console.WriteLine();

        Console.WriteLine("## 0. 検算");
        Console.WriteLine();
        int diff23 = Enumerable.Range(0, ymNb).Count(b => Math.Abs(winRate[1][b] - winRate[2][b]) > 1e-9);
        Console.WriteLine($"- **V2（Cap 1,000,000）と V3（規則 off）の勝率は {ymNb} 行中 ずれ {diff23} 件**"
            + "（`amount > Cap` が一度も立たないので切る行に到達しない＝同じ盤面でなければならない）。");
        Console.WriteLine($"- V2 の切られた回数 **{cutHits[1].Sum()}**（0 でなければならない）／ V3 **{cutHits[2].Sum()}**（規則 off なので帳簿も回らない）。");
        Console.WriteLine();

        Console.WriteLine("## 表A —— 攻撃型ごとの上限の帳簿（V1・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("`切られた` は一撃が上限を超えた回数、`切られた量` は落とされた総量（`amount - Cap`）。");
        Console.WriteLine("`入った` は上限を通した後に HP へ届いた回数と量、`撃破` はその一撃で相手が倒れた回数。");
        Console.WriteLine($"**`惜しい` は切られなかったが {YokeTrait.Cap * 4 / 5} 超の一撃**（上限が効いている境界）。");
        Console.WriteLine("**`型なし`** は継続ダメージ・反撃・肩代わりの中継・徴収（`pattern` を渡さない経路）。");
        Console.WriteLine();
        Console.WriteLine("| 受け手 | 型 | 切られた/戦 | 切られた量/戦 | 惜しい/戦 | 入った/戦 | 入った量/戦 | 撃破/戦 | 過剰/戦 | 切られた率 | 1撃あたり |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int side = 0; side < 2; side++)
            for (int k = 0; k < 5; k++)
            {
                int i = k + side * 5;
                if (inHits[0][i] == 0 && cutHits[0][i] == 0) continue;
                double cutRate = inHits[0][i] == 0 ? 0 : cutHits[0][i] * 100.0 / inHits[0][i];
                double each = inHits[0][i] == 0 ? 0 : inAmt[0][i] / (double)inHits[0][i];
                Console.WriteLine($"| {(side == 0 ? "**敵**（味方の刃）" : "味方（敵の刃）")} | {patName[k]} "
                    + $"| {Per(cutHits[0][i]):F2} | {Per(cutLost[0][i]):F1} | {Per(near[0][i]):F2} "
                    + $"| {Per(inHits[0][i]):F2} | {Per(inAmt[0][i]):F1} | {Per(kills[0][i]):F3} | {Per(over[0][i]):F1} "
                    + $"| {cutRate:F1}% | {each:F1} |");
            }
        Console.WriteLine();
        Console.WriteLine($"- 切られた側: **敵 {Per(cutFoeH[0]):F2} 回 / {Per(cutFoeL[0]):F1} 点、味方 {Per(cutPlayerH[0]):F2} 回 / {Per(cutPlayerL[0]):F1} 点**（1戦あたり）。");
        Console.WriteLine();

        Console.WriteLine("## 表B —— 上限の下での効率（V1 対 V2・**敵に入った側だけ**）");
        Console.WriteLine();
        Console.WriteLine("**同じ台・同じ seed で `Cap` だけを動かした**ので、`名目` は V2 の入った量、`実額` は V1 の入った量。");
        Console.WriteLine("**`撃破/一撃` が P4 の主判定**——上限は1発を平準化するので、体数の多い型が得をするなら撃破の側に出る。");
        Console.WriteLine();
        Console.WriteLine("| 型 | 一撃/戦 (V1) | 名目/戦 (V2) | 実額/戦 (V1) | 通過率 | 撃破/戦 (V1) | 撃破/戦 (V2) | 撃破/一撃 (V1) | 撃破/一撃 (V2) | 過剰率 (V1) | 過剰率 (V2) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int k = 0; k < 5; k++)
        {
            if (inHits[0][k] == 0 && inHits[1][k] == 0) continue;
            double pass = inAmt[1][k] == 0 ? 0 : inAmt[0][k] * 100.0 / inAmt[1][k];
            double kp1 = inHits[0][k] == 0 ? 0 : kills[0][k] / (double)inHits[0][k];
            double kp2 = inHits[1][k] == 0 ? 0 : kills[1][k] / (double)inHits[1][k];
            double ov1 = inAmt[0][k] == 0 ? 0 : over[0][k] * 100.0 / inAmt[0][k];
            double ov2 = inAmt[1][k] == 0 ? 0 : over[1][k] * 100.0 / inAmt[1][k];
            Console.WriteLine($"| {patName[k]} | {Per(inHits[0][k]):F2} | {Per(inAmt[1][k]):F1} | {Per(inAmt[0][k]):F1} | {pass:F1}% "
                + $"| {Per(kills[0][k]):F3} | {Per(kills[1][k]):F3} | {kp1:F3} | {kp2:F3} | {ov1:F1}% | {ov2:F1}% |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表C —— 誰の一撃が切られたか（V1・上位 12）");
        Console.WriteLine();
        Console.WriteLine("| # | 出どころ | 切られた/戦 | 切られた量/戦 | 1回あたり |");
        Console.WriteLine("|--:|---|--:|--:|--:|");
        int rank = 0;
        foreach (var kv in cutBy[0].OrderByDescending(x => x.Value.Lost).Take(12))
        {
            UnitDef? def = UnitCatalog.All.FirstOrDefault(d => d.Id == kv.Key)
                        ?? EnemyCatalog.Stages.SelectMany(st => st.Enemy.Occupied().Select(o => o.Def)).FirstOrDefault(d => d.Id == kv.Key);
            Console.WriteLine($"| {++rank} | {(def is null ? kv.Key : def.Name)} | {Per(kv.Value.Hits):F3} "
                + $"| {Per(kv.Value.Lost):F2} | {kv.Value.Lost / (double)kv.Value.Hits:F1} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表D —— 上限を通らない経路（V1・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 回数/戦 | 量/戦 | 上限との関係 |");
        Console.WriteLine("|---|--:|--:|---|");
        Console.WriteLine($"| 破片（`Armor`） | — | {Per(armor[0]):F2} | **上限の手前**で引かれる（切る前に減るので上限に触れない） |");
        Console.WriteLine($"| 肩代わりの中継 | {Per(relH[0]):F2} | {Per(relA[0]):F2} | **段ごとに別の `ApplyDamage`** なので独立に切られる＝分割は回避経路 |");
        Console.WriteLine($"| 継続ダメージ（毒・燃焼） | {Per(burnH[0]):F2} | {Per(burnA[0]):F2} | 固定量なので**一度も上限に触れない**（第130期） |");
        Console.WriteLine($"| 徴収（生贄・吸い・置き去り） | {Per(levyH[0]):F2} | {Per(levyA[0]):F2} | `ApplyDamage` は通るので**切られうる** |");
        Console.WriteLine($"| 繕いの代金（`self.Hp -= paid`） | — | {Per(direct[0]):F2} | **`ApplyDamage` を1度も通らない**（上限も破片も肩代わりも通らない） |");
        Console.WriteLine();

        Console.WriteLine("## 表E —— 行ごとの勝率（V1 対 V2）と、その行が振った型");
        Console.WriteLine();
        Console.WriteLine("`主型` は V1 で**敵に入った量**がいちばん多い型（**測定から引く。手で分類しない**）。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 主型 | 上限あり | 上限なし | Δ | 決着T (V1) | 決着T (V2) |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        for (int b = 0; b < ymNb; b++)
        {
            long tot = rowIn[b].Sum();
            int top = 0; for (int k = 1; k < 5; k++) if (rowIn[b][k] > rowIn[b][top]) top = k;
            Console.WriteLine($"| {yokeBuilds[b].Name} | {(tot == 0 ? "—" : $"{patName[top]} {rowIn[b][top] * 100.0 / tot:F0}%")} "
                + $"| {winRate[0][b]:F1} | {winRate[1][b]:F1} | {winRate[0][b] - winRate[1][b]:+0.0;-0.0;0.0} "
                + $"| {turnAvg[0][b]:F2} | {turnAvg[1][b]:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 主型ごとの集計");
        Console.WriteLine();
        Console.WriteLine("| 主型 | 行数 | 上限あり | 上限なし | Δ |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int k = 0; k < 5; k++)
        {
            var rows = Enumerable.Range(0, ymNb).Where(b =>
            {
                long tot = rowIn[b].Sum(); if (tot == 0) return false;
                int top = 0; for (int j = 1; j < 5; j++) if (rowIn[b][j] > rowIn[b][top]) top = j;
                return top == k;
            }).ToArray();
            if (rows.Length == 0) continue;
            double a1 = rows.Average(b => winRate[0][b]), a2 = rows.Average(b => winRate[1][b]);
            Console.WriteLine($"| {patName[k]} | {rows.Length} | {a1:F1} | {a2:F1} | {a1 - a2:+0.0;-0.0;0.0} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**全 {ymNb} 行の平均: 上限あり {winRate[0].Average():F1}% / 上限なし {winRate[1].Average():F1}%"
            + $"（Δ {winRate[0].Average() - winRate[1].Average():+0.0;-0.0}pt）。**");
        Console.WriteLine();

        // ---- 表F: §0-3 の矛盾を解く 2×2（段 × 上限）------------------------------------------
        //
        // 第128期にドルガへ段違い（`GradeStep`）を載せたとき、いちばん跳ねたのが**第四波**だった
        // （責め苦 1.0 → 65.5 / 仇討ち 18.5 → 94.0）。「全体は1発が大きいので上限に最も切られる
        // はずなのに、上限の波で最も勝率が上がる」が §0-3 の矛盾。
        //
        // **段を外した版のドルガを診断のローカルで作って 2×2 にする**（`UnitCatalog` は触らない）。
        // 対照は「段なし × 上限なし」まで取る——これが無いと、段の効きが上限のせいなのか
        // ただ強いだけなのかが割れない（第59期「符号の違う2つの効果は片側だけを 0 にする対照が要る」）。
        var plainDolga = new UnitDef
        {
            Id = UnitCatalog.Dolga.Id, Name = UnitCatalog.Dolga.Name + "（段なし）",
            MaxHp = UnitCatalog.Dolga.MaxHp, Attack = UnitCatalog.Dolga.Attack, Speed = UnitCatalog.Dolga.Speed,
            Advances = UnitCatalog.Dolga.Advances, Pattern = UnitCatalog.Dolga.Pattern,
            Actions = UnitCatalog.Dolga.Actions,
            Traits = UnitCatalog.Dolga.Traits.Where(t => t != TraitId.GradeStep).ToArray(),
        };
        var dolgaRows = Enumerable.Range(0, ymNb)
            .Where(b => yokeBuilds[b].F.Occupied().Any(o => o.Def.Id == UnitCatalog.Dolga.Id))
            .ToArray();

        Console.WriteLine("## 表F —— §0-3 の矛盾（段 × 上限の 2×2・ドルガを含む行）");
        Console.WriteLine();
        Console.WriteLine($"ドルガを含む行は **{dolgaRows.Length} / {ymNb}**。`段なし` は `GradeStep` だけを外した"
            + "ローカルの `UnitDef`（**`UnitCatalog` は1文字も触らない**。数値・席・他の4枚は同一）。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 主型 | 段あり×上限あり | 段なし×上限あり | **段の効き（上限あり）** | 段あり×上限なし | 段なし×上限なし | 段の効き（上限なし） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        double s11 = 0, s01 = 0, s10 = 0, s00 = 0;
        foreach (int b in dolgaRows)
        {
            Formation noStep = yokeBuilds[b].F.Clone();
            foreach (var (slot, def) in yokeBuilds[b].F.Occupied())
                if (def.Id == UnitCatalog.Dolga.Id) noStep[slot] = plainDolga;
            double[] w = new double[2];
            for (int v = 0; v < 2; v++)
            {
                int wins = 0;
                for (int seed = 0; seed < YokeSeeds; seed++)
                    if (BattleEngine.Run(noStep, wave4map, seed, verbose: false, null, vers[v].Rule).PlayerWon) wins++;
                w[v] = wins * 100.0 / YokeSeeds;
            }
            long tot = rowIn[b].Sum();
            int top = 0; for (int k = 1; k < 5; k++) if (rowIn[b][k] > rowIn[b][top]) top = k;
            s11 += winRate[0][b]; s01 += w[0]; s10 += winRate[1][b]; s00 += w[1];
            Console.WriteLine($"| {yokeBuilds[b].Name} | {(tot == 0 ? "—" : patName[top])} "
                + $"| {winRate[0][b]:F1} | {w[0]:F1} | **{winRate[0][b] - w[0]:+0.0;-0.0;0.0}** "
                + $"| {winRate[1][b]:F1} | {w[1]:F1} | {winRate[1][b] - w[1]:+0.0;-0.0;0.0} |");
        }
        if (dolgaRows.Length > 0)
        {
            int n = dolgaRows.Length;
            Console.WriteLine($"| **平均** | — | {s11 / n:F1} | {s01 / n:F1} | **{(s11 - s01) / n:+0.0;-0.0;0.0}** "
                + $"| {s10 / n:F1} | {s00 / n:F1} | {(s10 - s00) / n:+0.0;-0.0;0.0} |");
            Console.WriteLine();
            Console.WriteLine($"**相乗（段 × 上限）= {((s11 - s01) - (s10 - s00)) / n:+0.0;-0.0;0.0}pt**"
                + "——正なら「段は上限の下でこそ効く」（§0-3 の解）、負なら上限は段の値打ちを削っているだけ。");
        }
        return;
    }

    // ---- sweep: Cap の帯を振る -----------------------------------------------------------
    //
    // 4版の対照（V0/V1/V2/V3/V4）で **Cap 15 も 20 も落としすぎる**ことが分かったので、
    // 上限そのものを帯で振る。計画 §6 の「失敗の形」に書いてある手当（15 が当たりすぎたら 20 へ）を
    // 実際に測ると 20 でも平均 87.0 → 45.6 まで落ちる——**第五波（59.8）より難しい波になる。**
    //
    // 他の4波は保持者が不在なので Cap をいくら振っても1セルも動かない（引数なしの実行で
    // 560 セル 0 件を確認済み）。だから**基準の5波を1回だけ測って、第四波だけを Cap ごとに測り直す**。
    // 固有の敗者・勝者の判定と第2波との相関は、その基準の他波と組み合わせて引く。
    if (yokeMode == "sweep")
    {
        int[] caps = { 12, 15, 20, 25, 30, 35, 40, 50 };
        int nbS = yokeBuilds.Length, nwS = EnemyCatalog.Stages.Count;

        // 基準（V0 = 差し替え前の盤面）。他の4波はこの値をそのまま使い回す。
        var basis = new double[nbS][];
        for (int b = 0; b < nbS; b++)
        {
            basis[b] = new double[nwS];
            for (int w = 0; w < nwS; w++)
            {
                Formation foe = w == Wave4 ? Wave4With(EnemyCatalog.Warden) : EnemyCatalog.Stages[w].Enemy;
                int wins = 0;
                for (int seed = 0; seed < YokeSeeds; seed++)
                    if (BattleEngine.Run(yokeBuilds[b].F, foe, seed, verbose: false).PlayerWon) wins++;
                basis[b][w] = wins * 100.0 / YokeSeeds;
            }
        }
        Console.Error.WriteLine("  基準（軛なし）完了");

        // Cap ごとの第四波。
        var capRate = new double[caps.Length][];
        for (int c = 0; c < caps.Length; c++)
        {
            capRate[c] = new double[nbS];
            Formation foe = Wave4With(EnemyCatalog.Yoker);
            var rule = new YokeRule(caps[c], Active: true);
            for (int b = 0; b < nbS; b++)
            {
                int wins = 0;
                for (int seed = 0; seed < YokeSeeds; seed++)
                    if (BattleEngine.Run(yokeBuilds[b].F, foe, seed, verbose: false, null, rule).PlayerWon) wins++;
                capRate[c][b] = wins * 100.0 / YokeSeeds;
            }
            Console.Error.WriteLine($"  Cap {caps[c]} 完了");
        }

        Console.WriteLine("# 軛の上限を振る（yoke sweep）");
        Console.WriteLine();
        Console.WriteLine($"代表編成 {nbS} × Cap {caps.Length} 通り、seed 0..{YokeSeeds - 1}。診断用なので docs/ には置かない。");
        Console.WriteLine();
        Console.WriteLine("第四波以外は保持者が不在で1セルも動かない（引数なしの `yoke` で確認済み）ので、");
        Console.WriteLine("**基準の5波を1回測って、第四波だけを Cap ごとに測り直している。**");
        Console.WriteLine();
        Console.WriteLine("比較のための他波の現状値（`spread` と同じ計算）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 平均 | 100%の編成 | 0%の編成 | 中間帯 | 標準偏差 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        for (int w = 0; w < nwS; w++)
        {
            double[] col = Enumerable.Range(0, nbS).Select(b => basis[b][w]).ToArray();
            double mean = col.Average();
            double sd = Math.Sqrt(col.Select(x => (x - mean) * (x - mean)).Sum() / col.Length);
            Console.WriteLine($"| 第{w + 1}波 | {mean:F1} | {col.Count(x => x >= 100.0)} / {nbS} "
                + $"| {col.Count(x => x <= 0.0)} | {col.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} |");
        }

        Console.WriteLine();
        Console.WriteLine("## Cap ごとの第四波");
        Console.WriteLine();
        Console.WriteLine("**波の難度そのものを上げるのが目的ではない。** 見るのは 中間帯 と 固有の敗者 で、");
        Console.WriteLine("平均は「第五波（59.8）より難しい波にしていないか」の歯止めとして読む。");
        Console.WriteLine();
        Console.WriteLine("| Cap | 平均 | 100%の編成 | 0%の編成 | 中間帯 | 標準偏差 | 固有の敗者 | 固有の勝者 | 第2波との相関 | 第2〜4波すべて100% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

        double[] Col(int c) => c < 0
            ? Enumerable.Range(0, nbS).Select(b => basis[b][Wave4]).ToArray()
            : capRate[c];
        double At(int c, int b, int w) => w == Wave4 ? Col(c)[b] : basis[b][w];

        List<string> Losers(int c) => Enumerable.Range(0, nbS)
            .Where(b => At(c, b, Wave4) <= 0.0
                        && Enumerable.Range(1, nwS - 1).All(o => o == Wave4 || At(c, b, o) > 0.0))
            .Select(b => yokeBuilds[b].Name).ToList();
        List<string> Winners(int c) => Enumerable.Range(0, nbS)
            .Where(b => At(c, b, Wave4) >= 100.0
                        && Enumerable.Range(1, nwS - 1).All(o => o == Wave4 || At(c, b, o) < 100.0))
            .Select(b => yokeBuilds[b].Name).ToList();

        void Line(string label, int c)
        {
            double[] col = Col(c);
            double mean = col.Average();
            double sd = Math.Sqrt(col.Select(x => (x - mean) * (x - mean)).Sum() / col.Length);
            double corr = Corr2(Enumerable.Range(0, nbS).Select(b => basis[b][1]).ToArray(), col);
            int allTop = Enumerable.Range(0, nbS)
                .Count(b => Enumerable.Range(1, 3).All(w => At(c, b, w) >= 100.0));
            Console.WriteLine($"| {label} | {mean:F1} | {col.Count(x => x >= 100.0)} / {nbS} "
                + $"| {col.Count(x => x <= 0.0)} | {col.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} "
                + $"| {Losers(c).Count} | {Winners(c).Count} "
                + $"| {(double.IsNaN(corr) ? "—" : $"{corr:+0.00;-0.00}")} | {allTop} / {nbS} |");
        }

        Line("軛なし", -1);
        for (int c = 0; c < caps.Length; c++) Line($"Cap {caps[c]}", c);

        Console.WriteLine();
        for (int c = 0; c < caps.Length; c++)
        {
            var lose = Losers(c);
            Console.WriteLine($"- **Cap {caps[c]}** 固有の敗者 ({lose.Count}): "
                + (lose.Count == 0 ? "**なし**" : string.Join(" / ", lose)));
        }

        Console.WriteLine();
        Console.WriteLine("## 編成 × Cap");
        Console.WriteLine();
        Console.WriteLine("`中` は軛なしの第四波が中間帯（5% < x < 95%）にある編成。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 中 | 軛なし |" + string.Concat(caps.Select(c => $" {c} |")));
        Console.WriteLine("|---|:-:|---:|" + string.Concat(caps.Select(_ => "---:|")));
        for (int b = 0; b < nbS; b++)
        {
            bool mid = basis[b][Wave4] > 5.0 && basis[b][Wave4] < 95.0;
            Console.WriteLine($"| {yokeBuilds[b].Name} | {(mid ? "●" : "")} | {basis[b][Wave4]:F1} |"
                + string.Concat(Enumerable.Range(0, caps.Length).Select(c => $" {capRate[c][b]:F1} |")));
        }
        return;
    }

    // ---- log: 計画 §2.3 の監査 -----------------------------------------------------------
    if (yokeMode == "log")
    {
        Formation wave4 = EnemyCatalog.Stages[Wave4].Enemy;

        void Audit(string title, string buildKey, int seed, string[] marks, string note)
        {
            var (name, f) = yokeBuilds.First(b => b.Name.Contains(buildKey));
            BattleResult r = BattleEngine.Run(f, wave4, seed, verbose: true);

            // 保持者の InstanceId。ctx.Add は 味方（スロット昇順）→ 敵（スロット昇順）の順に振る。
            int id = f.Occupied().Count(), yokerId = -1;
            foreach (var (_, def) in wave4.Occupied())
            {
                if (def.Id == EnemyCatalog.Yoker.Id) yokerId = id;
                id++;
            }
            // **保持者の生死はターンではなくイベントの並びで割る。** 倒れたのと同じターンの
            // 後続のダメージはもう上限の外側にあるので、ターンで割ると「上限を超えた」と誤検出する
            // （実際に踏んだ: 反撃改2 の seed 0 で 78 が生存中に見えていた）。
            var events = r.Events.ToList();
            int deathAt = events.FindIndex(e => e.Kind == BattleEventKind.Death && e.TargetId == yokerId);
            if (deathAt < 0) deathAt = events.Count;
            int deathTurn = deathAt < events.Count ? events[deathAt].Turn : int.MaxValue;

            int maxUnder = events.Take(deathAt)
                .Where(e => e.Kind == BattleEventKind.Damage).Select(e => e.Amount).DefaultIfEmpty(0).Max();
            int maxAfter = events.Skip(deathAt)
                .Where(e => e.Kind == BattleEventKind.Damage).Select(e => e.Amount).DefaultIfEmpty(0).Max();
            var text = r.Log.Select(l => l.Text).ToList();
            int cuts = text.Count(t => t.Contains("軛が") && t.Contains("切った"));

            Console.WriteLine();
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine(note);
            Console.WriteLine();
            Console.WriteLine($"{name} / 第四波 / seed {seed} / {(r.PlayerWon ? "勝利" : "敗北")} {r.Turns}ターン");
            Console.WriteLine();
            Console.WriteLine($"- 軛が切った回数: **{cuts} 回**");
            Console.WriteLine("- 保持者が倒れたターン: "
                + (deathTurn == int.MaxValue ? "**最後まで生存**" : $"**T{deathTurn}**"));
            Console.WriteLine($"- 保持者の生存中の最大ダメージ: **{maxUnder}**"
                + $"（**{YokeTrait.Cap} 以下でなければならない**）");
            Console.WriteLine("- 保持者が倒れた後の最大ダメージ: "
                + (deathTurn == int.MaxValue ? "—" : $"{maxAfter}") + "（上限が外れたことの確認）");

            // 抜粋。前後に数行付けて順序が読めるようにする（破片 → 軛 → ダメージ の並び）。
            foreach (string mark in marks)
            {
                // 上限との関係が読める箇所を優先する。無ければ最初の一致に落とす。
                int at = Enumerable.Range(0, text.Count).FirstOrDefault(
                    i => text[i].Contains(mark)
                         && Enumerable.Range(i, Math.Min(4, text.Count - i)).Any(j => text[j].Contains("軛が")),
                    -1);
                if (at < 0) at = text.FindIndex(t => t.Contains(mark));
                Console.WriteLine();
                if (at < 0) { Console.WriteLine($"- `{mark}` を含む行は出なかった"); continue; }
                Console.WriteLine($"- `{mark}` の周辺:");
                Console.WriteLine();
                Console.WriteLine("```");
                for (int i = Math.Max(0, at - 1); i < Math.Min(text.Count, at + 4); i++)
                    Console.WriteLine(text[i]);
                Console.WriteLine("```");
            }
        }

        // 2つの行が近接する事例を seed で探す。破片・肩代わりと上限の同時発火は
        // 「起きるかどうか」自体が結果なので、**見つからなかったときは走査した seed 数を書く**
        // （1戦だけ見て「出なかった」と書くと、偶然か構造かが分からない）。
        void AuditPair(string title, string buildKey, string first, string second, int seeds, string note)
        {
            var (name, f) = yokeBuilds.First(b => b.Name.Contains(buildKey));
            Console.WriteLine();
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine(note);

            for (int seed = 0; seed < seeds; seed++)
            {
                var text = BattleEngine.Run(f, wave4, seed, verbose: true).Log.Select(l => l.Text).ToList();
                int at = Enumerable.Range(0, text.Count).FirstOrDefault(
                    i => text[i].Contains(first)
                         && Enumerable.Range(i, Math.Min(3, text.Count - i)).Any(j => text[j].Contains(second)),
                    -1);
                if (at < 0) continue;

                Console.WriteLine();
                Console.WriteLine($"{name} / 第四波 / seed {seed} で `{first}` と `{second}` が同時に出た:");
                Console.WriteLine();
                Console.WriteLine("```");
                for (int i = Math.Max(0, at - 1); i < Math.Min(text.Count, at + 4); i++)
                    Console.WriteLine(text[i]);
                Console.WriteLine("```");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"{name} / 第四波 / seed 0..{seeds - 1} を走査したが、"
                + $"`{first}` と `{second}` が同時に出る事例は **0 件**。");
        }

        Console.WriteLine("# 軛の監査（yoke log）");
        Console.WriteLine();
        Console.WriteLine("受け入れ基準 3（計画 §2.3）を1戦ずつ確かめる。"
            + $"規則は既定（`YokeRule.Default` / Cap {YokeTrait.Cap}）。");
        Console.WriteLine();
        Console.WriteLine("**ここだけはログの文字列を数えている。** UI は `LogKind` を見るという規約に");
        Console.WriteLine("反して見えるが、確かめたいのは「その行がどの順で出たか」そのもので、");
        Console.WriteLine("盤面の値では代用できない（`gullet log` と同じ理由）。");

        AuditPair("A. 破片は上限の外側で効くか", "範囲耐性", "破片が", "軛が", 50,
              "破片（`StatusKeys.Armor`）は上限**より前**に引かれる別資源。"
            + "破片が吸った後の残りが上限で切られる。"
            + "**破片が付くのは味方だけ**なので、この波では同時に働くには"
            + "味方が 25 を超える一撃（＝味方由来の巻き込み）を浴びる必要がある。");

        AuditPair("B. 肩代わりの各段は独立に切られるか", "耐久 (ガルド", "立ちはだかる", "軛が", 50,
              "巨躯で分割された段はそれぞれ別の `ApplyDamage` 呼び出しなので、段ごとに切られる。"
            + "**分割は上限を回避する経路**——意図した帰結（重い一撃は分けて受けろ）。"
            + "肩代わりは味方への攻撃にしか働かないので、これも味方が 25 超えを浴びたときだけ出る。");

        Audit("C. 棘守りの二重上限 / 惨禍の増幅", "反撃改2", 0, new[] { "鎧は貫かれ", "軛が" },
              "棘守り（カド）は `AbsorbCap` で既に別の上限を持つ。中継先への超過分は"
            + "別の呼び出しなので独立に切られる。惨禍（+50%）は**増幅が先・上限が後**なので、"
            + "増幅は上限の下で消える（意図どおり）。");

        Audit("D. 毒の刻みも切られるか", "毒 (グザ", 0, new[] { "毒に蝕まれている" },
              "毒は除外しない。渇きが `source == null` を外したのとは違い、こちらは"
            + "**「1発の重さ」に課金する規則**なので出どころは関係ない。");

        Audit("E. 墓守の層は上限に当たるか", "死の連鎖 (リィカ", 0, new[] { "軛が" },
              "リィカの層は攻撃力が三角数で伸びる（実測 5 → 35 → 64）。**上層が潰れる**のがここ。");
        return;
    }

    // ---- 版の並び ------------------------------------------------------------------------
    var versions = new (string Name, string Note, UnitDef Center, YokeRule Rule)[]
    {
        ("V0 現行",   "中央 城塞の重装兵（軛なし）＝**差し替え前の盤面**",
            EnemyCatalog.Warden, YokeRule.Default),
        ("V1 壁のみ", "中央 軛の重装兵・**規則は無効**（数値は V0 と同一）",
            EnemyCatalog.Yoker, new YokeRule(YokeTrait.Cap, Active: false)),
        ("V2 上限25", $"中央 軛の重装兵・Cap {YokeTrait.Cap}（**本命＝採用した規則**）",
            EnemyCatalog.Yoker, YokeRule.Default),
        ("V3 上限30", "Cap 30（上限を緩めた側）",
            EnemyCatalog.Yoker, new YokeRule(30, Active: true)),
        ("V4 上限20", "Cap 20（上限を締めた側。**ここから下は波が壁になる**）",
            EnemyCatalog.Yoker, new YokeRule(20, Active: true)),
    };

    int nv = versions.Length, nb = yokeBuilds.Length, nw = EnemyCatalog.Stages.Count;

    // 版ごとの敵5波。第四波以外は EnemyCatalog のものをそのまま指す。
    var board = new Formation[nv][];
    for (int v = 0; v < nv; v++)
    {
        board[v] = new Formation[nw];
        for (int w = 0; w < nw; w++)
            board[v][w] = w == Wave4 ? Wave4With(versions[v].Center) : EnemyCatalog.Stages[w].Enemy;
    }

    var rate = new double[nv][][];          // rate[版][編成][波] = 勝率(%)
    var outFoe = new double[nv][];          // 第四波・1戦あたりの「敵が受けたダメージ」＝味方の出力
    var outAlly = new double[nv][];         // 第四波・1戦あたりの「味方が受けたダメージ」
    var turns4 = new double[nv][];          // 第四波・決着ターン

    for (int v = 0; v < nv; v++)
    {
        rate[v] = new double[nb][];
        outFoe[v] = new double[nb];
        outAlly[v] = new double[nb];
        turns4[v] = new double[nb];

        for (int b = 0; b < nb; b++)
        {
            rate[v][b] = new double[nw];
            for (int w = 0; w < nw; w++)
            {
                int wins = 0;
                long foe = 0, ally = 0, turns = 0;
                for (int seed = 0; seed < YokeSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(yokeBuilds[b].F, board[v][w], seed,
                                                      verbose: false, null, versions[v].Rule);
                    if (r.PlayerWon) wins++;
                    if (w != Wave4) continue;

                    turns += r.Turns;
                    // **与ダメは受け手側から取る**（第13期 Phase DA）。毒・燃焼は source が
                    // null なので味方側から合計すると毒軸の出力が構造的に過小になる。
                    foreach ((string id, UnitTally t) in r.TallyByUnit)
                        if (wave4EnemyIds.Contains(id)) foe += t.DamageTaken;
                        else ally += t.DamageTaken;
                }
                rate[v][b][w] = wins * 100.0 / YokeSeeds;
                if (w != Wave4) continue;
                outFoe[v][b] = (double)foe / YokeSeeds;
                outAlly[v][b] = (double)ally / YokeSeeds;
                turns4[v][b] = (double)turns / YokeSeeds;
            }
        }
        Console.Error.WriteLine($"  {versions[v].Name} 完了");
    }

    Console.WriteLine("# 第四波の軛（yoke）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 {nb} × 全 {nw} 波 × {nv} 版、seed 0..{YokeSeeds - 1}。"
        + "診断用なので docs/ には置かない。");
    Console.WriteLine();
    foreach (var vv in versions) Console.WriteLine($"- **{vv.Name}**: {vv.Note}");

    // --- 検算 1: V0 が差し替え前の balance.md と一致するか --------------------------------
    Console.WriteLine();
    Console.WriteLine("## 検算 1: V0 × 全編成");
    Console.WriteLine();
    Console.WriteLine("**このセルは差し替え前の `docs/balance.md`（`git show HEAD:docs/balance.md`）と");
    Console.WriteLine("一致しなければならない。** ずれていたら診断の組み方（seed 帯・台・編成リスト）が");
    Console.WriteLine("balance.md と揃っていない。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, nw).Select(i => $" 第{i}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "---:|")));
    for (int b = 0; b < nb; b++)
        Console.WriteLine($"| {yokeBuilds[b].Name} |" + string.Concat(rate[0][b].Select(x => $" {x:F1}% |")));

    // --- 検算 2: V1（壁のみ）は V0 と一致するか -------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 検算 2: V1（壁のみ）= V0");
    Console.WriteLine();
    Console.WriteLine("軛の重装兵は城塞の重装兵と**数値が1つも違わない**ので、規則を切れば盤面は完全に同じになる。");
    Console.WriteLine("**ここが 0 件でなければ、差し替えが数値も動かしている**（逆位の失敗の直接の原因）。");
    Console.WriteLine();
    var v1stray = new List<string>();
    for (int b = 0; b < nb; b++)
        for (int w = 0; w < nw; w++)
            if (Math.Abs(rate[1][b][w] - rate[0][b][w]) > 1e-9)
                v1stray.Add($"{yokeBuilds[b].Name} / 第{w + 1}波: {rate[0][b][w]:F1}% → {rate[1][b][w]:F1}%");
    Console.WriteLine($"{nb} 編成 × {nw} 波 = {nb * nw} セル中、**食い違い {v1stray.Count} 件**。");
    foreach (string x in v1stray.Take(40)) Console.WriteLine($"- {x}");

    // --- 検算 3: 第四波以外は全版で動かないか ---------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 検算 3: 第一・二・三・五波は全版 ±0.0");
    Console.WriteLine();
    Console.WriteLine("軛の保持者は第四波にしかいないので、他の4波は Cap を振っても1セルも動いてはいけない");
    Console.WriteLine("（受け入れ基準 1）。**動いていたら規則が保持者の不在下でも効いている。**");
    Console.WriteLine();
    var otherStray = new List<string>();
    for (int v = 1; v < nv; v++)
        for (int b = 0; b < nb; b++)
            for (int w = 0; w < nw; w++)
                if (w != Wave4 && Math.Abs(rate[v][b][w] - rate[0][b][w]) > 1e-9)
                    otherStray.Add($"{yokeBuilds[b].Name} / {versions[v].Name} / 第{w + 1}波: "
                                   + $"{rate[0][b][w]:F1}% → {rate[v][b][w]:F1}%");
    Console.WriteLine($"{nb} 編成 × {nv - 1} 版 × {nw - 1} 波 = {nb * (nv - 1) * (nw - 1)} セル中、"
                      + $"**食い違い {otherStray.Count} 件**。");
    foreach (string x in otherStray.Take(40)) Console.WriteLine($"- {x}");

    // --- 主表: 第四波 ---------------------------------------------------------------------
    bool Mid(int b) => rate[0][b][Wave4] > 5.0 && rate[0][b][Wave4] < 95.0;

    Console.WriteLine();
    Console.WriteLine("## 主表: 第四波の勝率 × 各版");
    Console.WriteLine();
    Console.WriteLine("`中` は **V0 の第四波が中間帯（5% < x < 95%）にある編成**＝この台で増分が読める編成");
    Console.WriteLine("（第24期 yield の教訓。飽和したセルでは誰に何をしても 0 に潰れる）。");
    Console.WriteLine("100% に張り付いている編成は「落ちたかどうか」だけを見る。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 中 |" + string.Concat(versions.Select(v => $" {v.Name} |"))
                      + " V2−V0 | V3−V0 | V4−V0 |");
    Console.WriteLine("|---|:-:|" + string.Concat(versions.Select(_ => "---:|")) + "---:|---:|---:|");
    for (int b = 0; b < nb; b++)
        Console.WriteLine($"| {yokeBuilds[b].Name} | {(Mid(b) ? "●" : "")} |"
            + string.Concat(Enumerable.Range(0, nv).Select(v => $" {rate[v][b][Wave4]:F1}% |"))
            + $" {rate[2][b][Wave4] - rate[0][b][Wave4]:+0.0;-0.0} |"
            + $" {rate[3][b][Wave4] - rate[0][b][Wave4]:+0.0;-0.0} |"
            + $" {rate[4][b][Wave4] - rate[0][b][Wave4]:+0.0;-0.0} |");

    // --- 判定（計画 §6 の表）--------------------------------------------------------------
    List<string> UniqueLosers(int v, int w) => Enumerable.Range(0, nb)
        .Where(b => rate[v][b][w] <= 0.0
                    && Enumerable.Range(1, nw - 1).All(o => o == w || rate[v][b][o] > 0.0))
        .Select(b => yokeBuilds[b].Name).ToList();
    List<string> UniqueWinners(int v, int w) => Enumerable.Range(0, nb)
        .Where(b => rate[v][b][w] >= 100.0
                    && Enumerable.Range(1, nw - 1).All(o => o == w || rate[v][b][o] < 100.0))
        .Select(b => yokeBuilds[b].Name).ToList();

    Console.WriteLine();
    Console.WriteLine("## 判定（計画 §6）");
    Console.WriteLine();
    Console.WriteLine("`spread` の (1)(2)(3) を第四波について版ごとに引き直したもの。");
    Console.WriteLine("**固有の敗者/勝者は第一波を比較から外して数える**（第22期 Phase 2b。");
    Console.WriteLine("第一波は全編成 100% を意図して維持しているので、入れると恒等的に 0 になる）。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 平均 | 100%の編成 | 0%の編成 | 中間帯 | 標準偏差 | 固有の敗者 | 固有の勝者 | 第2波との相関 | 第2〜4波すべて100% |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int v = 0; v < nv; v++)
    {
        double[] col = Enumerable.Range(0, nb).Select(b => rate[v][b][Wave4]).ToArray();
        double mean = col.Average();
        double sd = Math.Sqrt(col.Select(x => (x - mean) * (x - mean)).Sum() / col.Length);
        double corr = Corr2(Enumerable.Range(0, nb).Select(b => rate[v][b][1]).ToArray(), col);
        int allTop = Enumerable.Range(0, nb)
            .Count(b => Enumerable.Range(1, 3).All(w => rate[v][b][w] >= 100.0));
        Console.WriteLine($"| {versions[v].Name} | {mean:F1} | {col.Count(x => x >= 100.0)} / {nb} "
            + $"| {col.Count(x => x <= 0.0)} | {col.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} "
            + $"| {UniqueLosers(v, Wave4).Count} | {UniqueWinners(v, Wave4).Count} "
            + $"| {(double.IsNaN(corr) ? "—" : $"{corr:+0.00;-0.00}")} | {allTop} / {nb} |");
    }
    Console.WriteLine();
    for (int v = 0; v < nv; v++)
    {
        var lose = UniqueLosers(v, Wave4);
        var win = UniqueWinners(v, Wave4);
        Console.WriteLine($"- **{versions[v].Name}** 固有の敗者 ({lose.Count}): "
            + (lose.Count == 0 ? "**なし**" : string.Join(" / ", lose))
            + $" ／ 固有の勝者 ({win.Count}): " + (win.Count == 0 ? "なし" : string.Join(" / ", win)));
    }

    // --- 動いた編成 -----------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 動いた編成（V2−V0 の順）");
    Console.WriteLine();
    Console.WriteLine("計画 §5 の予測（大打点を持つ編成から落ちる）と突き合わせる列。");
    Console.WriteLine("**`中` が付いていない行の 0.0 は「無風」ではなく「読めない」**——飽和したセルなので。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 中 | V0 | V2 | 差 | 敵被ダメ/戦 V1→V2 | 味方被ダメ/戦 V1→V2 | 決着T V1→V2 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|");
    foreach (int b in Enumerable.Range(0, nb).OrderBy(b => rate[2][b][Wave4] - rate[0][b][Wave4]))
        Console.WriteLine($"| {yokeBuilds[b].Name} | {(Mid(b) ? "●" : "")} | {rate[0][b][Wave4]:F1} "
            + $"| {rate[2][b][Wave4]:F1} | {rate[2][b][Wave4] - rate[0][b][Wave4]:+0.0;-0.0} "
            + $"| {outFoe[1][b]:F0} → {outFoe[2][b]:F0} ({outFoe[2][b] - outFoe[1][b]:+0;-0}) "
            + $"| {outAlly[1][b]:F0} → {outAlly[2][b]:F0} ({outAlly[2][b] - outAlly[1][b]:+0;-0}) "
            + $"| {turns4[1][b]:F1} → {turns4[2][b]:F1} |");

    Console.WriteLine();
    Console.WriteLine("`敵被ダメ/戦` は**受け手側から数えた味方の出力**（第13期 Phase DA。毒・燃焼は");
    Console.WriteLine("`source` が null なので味方側から合計すると毒軸が構造的に過小になる）。");
    Console.WriteLine("V1 → V2 の減りが、そのまま**上限で切られた量**。ここが動いていない編成は、");
    Console.WriteLine("そもそも上限に当たる打点を持っていない。");
    return;

    // ピアソン相関。片方の分散が 0 なら定義できないので NaN を返す（呼び出し側で — に置く）。
    static double Corr2(double[] a, double[] b)
    {
        double ma = a.Average(), mb = b.Average();
        double num = 0, da = 0, db = 0;
        for (int i = 0; i < a.Length; i++)
        {
            num += (a[i] - ma) * (b[i] - mb);
            da += (a[i] - ma) * (a[i] - ma);
            db += (b[i] - mb) * (b[i] - mb);
        }
        return da <= 0 || db <= 0 ? double.NaN : num / Math.Sqrt(da * db);
    }
}
}
