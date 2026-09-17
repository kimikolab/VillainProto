using BattleCore;
using static Common;

// =====================================================================================
// power モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "power")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 power
// =====================================================================================

static class PowerDiag
{
// power モード: 編成の「地力」を分解する（第12期 Phase CA）。
//
// 第4〜11期はどれも「地力とは別の2本目の軸」を探して失敗した——部隊列の順序・攻撃パターンの
// 向き・自傷率・敵のチャージ・味方スキルのタイミング。**何を作っても編成の序列が同じ順位で
// 出てくる**（順位相関 0.83〜1.00）。支配的な次元が1本あることは分かっている。
//
// ところがその1本を一度も測っていない。総HPなのか、総攻撃力なのか、その積なのか、
// 特定の駒の存在なのか、盤面配置なのか——分からないまま2本目を探しても、
// **何に対して直交させたいのかが決められない。**
//
// ここは純粋な測定で、数値も特性もパターンも一切変えない。編成ごとに
//   静的（Formation / UnitDef から計算できる。戦わなくても分かる）8種
//   動的（UnitTally から。既存フィールドだけで足りる）7種
// を出す。目的変数は突破度（第8期 Phase U）。
//
// 台は2種。主 = チャージ台（bridge の7列目 = ChargeBench。第10期 AB-0）、
// 従 = 既存5波（順路）。第8期の「136% で測ると何も見えない」が効くので、片方だけでは
// 判定できない——主で出た第一近似が従で入れ替わるなら、「地力」は台ごとに違うものを
// 指していたことになる。
//
// 却下した案: 多変量回帰で一気に説明する。n = 31 しかないので3変数以上は確実に過学習する
// （§4-1）。単相関 → 第一近似 → 残差 → 残差との相関の1段だけ、多変量は2変数まで。
// 却下した案: 目的変数を勝率にする。2部隊だと突破率が飽和して序列が潰れるのと同じ理由で、
// 勝率は上下端に張り付いて特徴量との差を吸収する（第5期の持ち越し論点(2)）。
//
// 診断用で docs/ には置かない（seats / bill / charge / timing と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 power [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    const int PowerSeeds = 200;   // bridge / bill / charge / timing と同じ
    var all = CompareBuilds();

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    // 台。列は Name で引く（Columns の並び順は GodotApp の EngagementColumn が使うので
    // 当てにしない。engage / seats と同じ作法）。
    var benches = new (string Tag, string Name, string Note, IReadOnlyList<Formation> Squads)[]
    {
        ("主", "チャージ台", "bridge の7列目。3波・合計代金 116.6%・突破率(1) 39.1%", ChargeBench()),
        ("従", "既存5波", "順路。第1期からの基準列", EnemyCatalog.Columns.First(c => c.Name == "順路").Squads),
    };

    // 静的特徴量。**定義値だけから取る**——会戦中の目減り（継ぎ接ぎの最大HP半減）は
    // 動的側の話で、ここに混ぜると「戦わずに分かる量」でなくなる（§3-2）。
    var statics = new (string Name, string Def, Func<Formation, double> Get)[]
    {
        ("体数",     "編成の駒数（4 or 5）",
            f => f.Count),
        ("総HP",     "Def.MaxHp の合計",
            f => f.Occupied().Sum(x => x.Def.MaxHp)),
        ("総攻",     "Def.Attack の合計",
            f => f.Occupied().Sum(x => x.Def.Attack)),
        ("積",       "総HP × 総攻",
            f => (double)f.Occupied().Sum(x => x.Def.MaxHp) * f.Occupied().Sum(x => x.Def.Attack)),
        ("最薄HP",   "編成中いちばん低い Def.MaxHp。閾値仮説の候補",
            f => f.Occupied().Min(x => x.Def.MaxHp)),
        ("後列HP",   "後列（slot 4/5）の Def.MaxHp 合計",
            f => f.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)),
        ("平均速度", "Def.Speed の平均",
            f => f.Occupied().Average(x => x.Def.Speed)),
        ("範囲枚数", "Def.Pattern が薙ぎ/全体の駒数（AoeCount。cost 以来ずっと同じ区分）",
            f => AoeCount(f)),
    };

    // 動的特徴量。**既存の UnitTally だけで足りることを確認した**（§3-2。足りなければ
    // 足す前に報告する、が指示だった）。既存の出力には列を増やしていない。
    //
    // 第13期 Phase DA で `与ダメ/戦`・`撃破/戦`・`与ダメ効率` の3つを**受け手側**へ移した。
    // 残る4つは味方側のままでよい——`被ダメ/戦`・`回復/戦`・`自傷率` はそもそも
    // 味方が受け手なので穴が無く、`干渉/戦` は「誰が起点になったか」を数える量なので
    // 受け手側に対応物が無い（毒は出どころを持たないので、干渉は依然として過小のまま。
    // これは診断の限界として残る）。
    var dynNames = new (string Name, string Def)[]
    {
        ("与ダメ/戦",  "**敵**の DamageTaken 合計 − 敵の TakenFromAlly ÷ 部隊戦数（受け手側。過剰殺傷を含む）"),
        ("被ダメ/戦",  "DamageTaken の合計 ÷ 部隊戦数"),
        ("撃破/戦",    "**敵の死亡数** ÷ 部隊戦数（誰が仕留めたかを問わない）"),
        ("干渉/戦",    "Interventions の合計 ÷ 部隊戦数（**活動量の本体**。味方側のまま）"),
        ("回復/戦",    "Healed の合計 ÷ 部隊戦数"),
        ("自傷率",     "TakenFromAlly ÷ DamageTaken（第9期の定義そのまま）"),
        ("与ダメ効率", "与ダメ ÷ 撃破数。**オーバーキルの指標**（大きいほど1体を落とすのに無駄が多い）"),
    };

    Console.WriteLine($"# 地力の分解（seed 0..{PowerSeeds - 1} の {PowerSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("編成ごとの特徴量と突破度を並べたもの。**測定だけで、盤面は何も変えていない。**");
    Console.WriteLine("突破度は突破した部隊数 + 最後に負けた部隊戦での削り割合（0.0 〜 列長。第8期 Phase U）。");
    Console.WriteLine("投入部隊数は 1——2部隊だと突破率が飽和して序列が潰れる。");
    Console.WriteLine();
    foreach (var (tag, name, note, squads) in benches)
        Console.WriteLine($"- **{tag}: {name}**（{squads.Count}波）: {note}");
    Console.WriteLine();

    // --- 計測 ---
    int nB = benches.Length;
    var deg = new double[nB][];
    var dyn = new double[nB][][];   // [台][編成][特徴量]
    var leg = new double[nB][][];   // [台][編成][旧定義3種]。第12期との対比だけが読む
    var bps = new double[nB][];     // [台][編成] 部隊戦数 ÷ 試行。第14期 EA の同語反復の検査が読む
    long foeFromAlly = 0;           // 敵同士の巻き込み。0 のはず（第13期 §3-1）
    int deathGaps = 0;              // 敵が DamageTaken を経由せずに死んだ疑いの件数
    for (int b = 0; b < nB; b++)
    {
        deg[b] = new double[nT];
        dyn[b] = new double[nT][];
        leg[b] = new double[nT][];
        bps[b] = new double[nT];
        for (int t = 0; t < nT; t++)
        {
            var m = MeasurePower(targets[t].F, benches[b].Squads, PowerSeeds);
            deg[b][t] = m.Degree;
            dyn[b][t] = m.Dynamics;
            leg[b][t] = m.Legacy;
            bps[b][t] = m.BattlesPerSeed;
            foeFromAlly += m.FoeTakenFromAlly;
            deathGaps += m.DeathGaps;
        }
    }
    var stat = new double[nT][];
    for (int t = 0; t < nT; t++)
        stat[t] = statics.Select(s => s.Get(targets[t].F)).ToArray();

    // --- 検算 ---
    // (1) tally は Def.Id で引くので、味方と敵で Id が衝突していると敵の被弾が
    //     味方の動的特徴量に混ざる（MeasureBill の注記と同じ穴。あちらは「呼び出し側が
    //     検算する」と書いてあるだけだったので、ここで実際に検算する）。
    var clash = new List<string>();
    foreach (var (_, bench, _, squads) in benches)
    {
        var foeIds = squads.SelectMany(s => s.Occupied()).Select(x => x.Def.Id).ToHashSet();
        foreach (var (name, f) in targets)
            foreach (string id in f.Occupied().Select(x => x.Def.Id).Where(foeIds.Contains))
                clash.Add($"{bench} × {name}: {id}");
    }
    // (2) 列長1では最終戦＝初戦なので突破度の2つの削り割合が一致するはず（第8期 §2-3）。
    double maxGap = 0;
    Formation lastWave = benches[0].Squads[^1];
    for (int t = 0; t < nT; t++)
        for (int seed = 0; seed < 20; seed++)
        {
            EngagementResult r = EngagementEngine.Run(new[] { targets[t].F }, new[] { lastWave }, seed, verbose: false);
            maxGap = Math.Max(maxGap, Math.Abs(r.LastBattleAttrition - r.FirstBattleAttrition));
        }

    Console.WriteLine("### 検算");
    Console.WriteLine();
    Console.WriteLine($"- **味方と敵の Def.Id の衝突: {clash.Count} 件**"
        + (clash.Count == 0 ? "（0 でなければ動的特徴量に敵の数字が混ざっている）"
                            : $" ← **混入している**: {string.Join(" / ", clash.Take(5))}"));
    Console.WriteLine($"- **突破度（列長1）: |LastBattleAttrition − FirstBattleAttrition| の最大 = {maxGap:F6}**"
        + $"（{nT} 編成 × seed 0..19。0 でなければ分母がずれている）");
    // 受け手側から測るための2つの前提（第13期 §3-1・§6）。どちらも「0 のはず」で、
    // 0 でなければ方法のほうが間違っている。
    Console.WriteLine($"- **敵の TakenFromAlly の総和: {foeFromAlly}**"
        + (foeFromAlly == 0
            ? "（0 = 敵側に巻き込みが無い。与ダメから引いた量も 0）"
            : " ← **敵同士の巻き込みがある。** 与ダメからこの量を引いている"));
    Console.WriteLine($"- **勝った部隊戦で敵の死亡数が投入数と合わなかった件数: {deathGaps}**"
        + (deathGaps == 0
            ? "（0 = 敵は必ず `DamageTaken` を経由して死んでいる）"
            : " ← **`DamageTaken` を経由しない死亡経路がある。受け手側の撃破は信用できない**"));
    Console.WriteLine();

    // --- 特徴量の定義 ---
    Console.WriteLine("### 特徴量の定義");
    Console.WriteLine();
    Console.WriteLine("| 区分 | 特徴量 | 定義 |");
    Console.WriteLine("|---|---|---|");
    foreach (var (name, def, _) in statics) Console.WriteLine($"| 静的 | {name} | {def} |");
    foreach (var (name, def) in dynNames) Console.WriteLine($"| 動的 | {name} | {def} |");
    Console.WriteLine();
    Console.WriteLine("動的の分母「戦」は**部隊戦の数**（会戦の中の Battle の総数）。会戦は深く抜いた");
    Console.WriteLine("編成ほど戦闘数が増えるので、seed 数で割ると「長く戦った」だけで値が膨らむ。");
    Console.WriteLine("`撃破/戦` が 0 の編成では `与ダメ効率` が定義できないので `—` を出す。");
    Console.WriteLine();
    Console.WriteLine("> **`与ダメ/戦` と `撃破/戦` は受け手側（敵の tally）から取っている**（第13期 Phase DA）。");
    Console.WriteLine("> `TickStatuses` は `ApplyDamage(u, poison, null)` と source を渡さずに呼ぶので、");
    Console.WriteLine("> 毒・燃焼の削りは出どころの駒の `DamageToEnemy` にも `Kills` にも載らない");
    Console.WriteLine("> （`ApplyDamage` の `source is not null` 分岐、`HandleDeath` の `killer is not null` 分岐）。");
    Console.WriteLine("> 第12期はこれを味方側から合計していたので、**毒軸の編成の出力が構造的に過小に出ていた。**");
    Console.WriteLine("> どの経路で削っても敵の `DamageTaken` には必ず載るので、敵側から数えれば穴が塞がる。");
    Console.WriteLine("> **`BattleCore` は1行も触っていない**——エンジンではなく読み方を変えただけ。");
    Console.WriteLine(">");
    Console.WriteLine("> **`干渉/戦` は味方側のまま**（毒は出どころを持たないので受け手側に対応物が無い）。");
    Console.WriteLine("> 毒軸の編成の `干渉/戦` は依然として過小で、`docs/pulse.md` の毒軸の行も同じく過小のまま。");
    Console.WriteLine();
    // 受け手側へ移すと 撃破/戦 の r が跳ね上がるが、**跳ね上がった分のかなりは算術**。
    // 穴があったころは毒軸の過小がこの結び付きを隠していたので、穴を塞いだ結果として
    // 見えるようになった。ここを書かずに r² だけ出すと、同語反復を発見だと読ませることになる。
    Console.WriteLine("> **警告: `撃破/戦` と突破度は構造的に結び付いている。** 部隊を全滅させることが");
    Console.WriteLine("> その部隊を突破することなので、`撃破/戦` の高さは突破度の言い換えにかなり近い。");
    Console.WriteLine("> 味方1部隊では **部隊戦数 = 突破数 + 1**（全抜き時だけ = 突破数）なので、");
    Console.WriteLine("> `撃破/戦` は突破数の決定的な関数に、最終戦で倒した数を足したものになる。");
    Console.WriteLine("> 実際、チャージ台（駒数 5+4+4 = 13）で全抜きした編成の `撃破/戦` は例外なく");
    Console.WriteLine("> **13 ÷ 3 = 4.33** で、これは測定結果ではなく算術。");
    Console.WriteLine(">");
    Console.WriteLine("> `与ダメ/戦` も程度は軽いが同じ性質を持つ（突破度の小数部＝最終戦で削った敵HPの割合）。");
    Console.WriteLine("> **同語反復から自由なのは静的特徴量だけ**——「静的だけの説明力」を別項で出しているのは");
    Console.WriteLine("> そのため。動的側の r² は「地力の説明力」ではなく「どれだけ言い換えに近いか」として読む。");
    Console.WriteLine("> 残差の側は意味を保つ（**倒した数の割に突破できていない / できている**編成が出る）。");
    Console.WriteLine();

    // 主の台の突破度の降順で並べる。序列そのものが読みたいものなので、
    // 表の並びを目的変数に揃えておく（相関の計算順には影響しない）。
    int[] order = Enumerable.Range(0, nT).OrderByDescending(t => deg[0][t]).ToArray();

    Console.WriteLine("### 静的特徴量（戦わなくても分かる量。台に依らない）");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(statics.Select(s => $" {s.Name} |")));
    Console.WriteLine("|---|" + string.Concat(statics.Select(_ => "--:|")));
    foreach (int t in order)
        Console.WriteLine($"| {targets[t].Name} |"
            + $" {stat[t][0]:F0} | {stat[t][1]:F0} | {stat[t][2]:F0} | {stat[t][3]:F0} |"
            + $" {stat[t][4]:F0} | {stat[t][5]:F0} | {stat[t][6]:F1} | {stat[t][7]:F0} |");
    Console.WriteLine();

    for (int b = 0; b < nB; b++)
    {
        Console.WriteLine($"### {benches[b].Tag}の台（{benches[b].Name}・列長 {benches[b].Squads.Count}）: 突破度と動的特徴量");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 突破度 |" + string.Concat(dynNames.Select(d => $" {d.Name} |")));
        Console.WriteLine("|---|--:|" + string.Concat(dynNames.Select(_ => "--:|")));
        foreach (int t in order)
        {
            double[] d = dyn[b][t];
            Console.WriteLine($"| {targets[t].Name} | {deg[b][t]:F3} |"
                + $" {d[0]:F0} | {d[1]:F0} | {d[2]:F2} | {d[3]:F2} | {d[4]:F0} |"
                + $" {d[5] * 100:F1}% | {(double.IsNaN(d[6]) ? "—" : $"{d[6]:F1}")} |");
        }
        Console.WriteLine();
        Console.Out.Flush();
    }

    // ================= Phase CB: 分解 =================
    //
    // n = 31 しかない。多変量回帰を回すと確実に過学習するので、単相関 → 第一近似 →
    // 残差 → 残差との相関の1段だけ。多変量は2変数まで（§4-1）。
    //
    // **因果は主張しない。** 「総HPが高い編成が強い」は「総HPを上げれば強くなる」を
    // 意味しない。ここで出るのは相関だけで、推測は推測と書く。

    // 特徴量を1本の並びにまとめる（静的8 + 動的7 = 15）。静的は台に依らないので
    // どちらの台でも同じ値が入る。
    int nS = statics.Length, nF = statics.Length + dynNames.Length;
    string[] featNames = statics.Select(s => s.Name).Concat(dynNames.Select(d => d.Name)).ToArray();
    bool[] isStatic = Enumerable.Range(0, nF).Select(k => k < nS).ToArray();
    var feat = new double[nB][][];   // [台][特徴量][編成]
    for (int b = 0; b < nB; b++)
    {
        feat[b] = new double[nF][];
        for (int k = 0; k < nF; k++)
            feat[b][k] = Enumerable.Range(0, nT)
                .Select(t => k < nS ? stat[t][k] : dyn[b][t][k - nS]).ToArray();
    }

    // 旧定義（第12期・味方側）の特徴量行列。**差し替えた3つだけを入れ替えた写し**で、
    // 残る12個には同じ値が入る（したがって突破度との r も動かない。動くのは順位のほうで、
    // それ自体が「何が第一近似になるか」の答えを変える）。
    // 同じ実行・同じ seed から作っているので、新旧の差は定義の差だけになる。
    int[] swapped = { 0, 2, 6 };   // dynNames 内の位置。Legacy の並びは 与ダメ・撃破・効率
    var featOld = new double[nB][][];
    for (int b = 0; b < nB; b++)
    {
        featOld[b] = new double[nF][];
        for (int k = 0; k < nF; k++)
        {
            int sw = k < nS ? -1 : Array.IndexOf(swapped, k - nS);
            featOld[b][k] = sw >= 0
                ? Enumerable.Range(0, nT).Select(t => leg[b][t][sw]).ToArray()
                : feat[b][k];
        }
    }

    var ordered = new (int K, double R, double Rho, int N)[nB][];
    // 第12期（味方側）の並びも台ごとに残す。第14期 Phase EA の三期対比表が読む。
    var orderedOldAll = new (int K, double R, double Rho, int N)[nB][];

    for (int b = 0; b < nB; b++)
    {
        Console.WriteLine($"## 分解: {benches[b].Tag}の台（{benches[b].Name}）");
        Console.WriteLine();

        // 目的変数が天井に張り付いていると、そこの編成同士の差が測れない。
        // 相関を読む前に何編成が飽和しているかを出す（列長ちょうど = 全抜き）。
        int ceil = Enumerable.Range(0, nT).Count(t => deg[b][t] >= benches[b].Squads.Count - 1e-9);
        if (ceil > 0)
        {
            Console.WriteLine($"> **注意: {ceil} 編成が突破度の天井（{benches[b].Squads.Count}.000 = 全抜き）に"
                + $"張り付いている。** その {ceil} 編成の間の差はこの台では測れていない——"
                + "相関の上限がその分だけ下がる。");
            Console.WriteLine();
        }

        // --- 1. 単相関の全一覧 ---
        ordered[b] = Enumerable.Range(0, nF)
            .Select(k => { var c = Correlate(feat[b][k], deg[b]); return (K: k, c.R, c.Rho, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();

        Console.WriteLine("### 単相関（全15特徴量 × 突破度。|r| の降順）");
        Console.WriteLine();
        Console.WriteLine("`r` はピアソン（突破度は連続量なので生の値で当てる）、`ρ` はスピアマン");
        Console.WriteLine("（同順位は平均順位。第8期以降の順位相関と同じ計算）。`n` は点の数——");
        Console.WriteLine("撃破 0 で与ダメ効率が定義できない編成はその行から落ちる。");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 区分 | 特徴量 | r | ρ | n |");
        Console.WriteLine("|--:|:-:|---|--:|--:|--:|");
        for (int i = 0; i < nF; i++)
        {
            var x = ordered[b][i];
            Console.WriteLine($"| {i + 1} | {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} "
                + $"| {x.R:+0.00;-0.00} | {x.Rho:+0.00;-0.00} | {x.N} |");
        }
        Console.WriteLine();

        // --- 1b. 第12期（味方側）との対比 ---
        // 指示は「第12期の値と並べて、どれがどれだけ動いたかを出す」（§3-3）。
        // 別の実行から数字を引いてくると、動いたのが定義のせいか実行のせいかが決まらないので、
        // 同じ実行の中で旧定義も計算して並べる。
        var orderedOld = Enumerable.Range(0, nF)
            .Select(k => { var c = Correlate(featOld[b][k], deg[b]); return (K: k, c.R, c.Rho, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();
        orderedOldAll[b] = orderedOld;

        Console.WriteLine("#### 第12期（味方側）との対比 — 単相関はどう動いたか");
        Console.WriteLine();
        Console.WriteLine("**値が動くのは受け手側へ移した3つだけ**（`与ダメ/戦`・`撃破/戦`・`与ダメ効率`）。");
        Console.WriteLine("残る12個は同じ値・同じ r で、**順位だけがその3つに押されて動く**。");
        Console.WriteLine("突破度は新旧で完全に同じ（測り方を変えただけで盤面は動かない）。");
        Console.WriteLine();
        Console.WriteLine("| 区分 | 特徴量 | 旧 r | 旧順位 | 新 r | 新順位 | Δr | 順位の動き |");
        Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < nF; i++)
        {
            var x = ordered[b][i];
            int oldPos = Array.FindIndex(orderedOld, y => y.K == x.K);
            double dr = x.R - orderedOld[oldPos].R;
            int move = oldPos - i;   // 正なら順位が上がった
            Console.WriteLine($"| {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} "
                + $"| {orderedOld[oldPos].R:+0.00;-0.00} | {oldPos + 1} "
                + $"| {x.R:+0.00;-0.00} | {i + 1} | {dr:+0.00;-0.00} "
                + $"| {(move == 0 ? "—" : $"{move:+0;-0}")} |");
        }
        Console.WriteLine();

        // 値そのものの動き。相関の変化だけだと「どの編成が過小だったのか」が見えない——
        // 穴が毒軸に集中していたという主張は、ここの倍率で確かめる。
        Console.WriteLine("#### 受け手側へ移して値がどう動いたか（倍率の降順）");
        Console.WriteLine();
        Console.WriteLine("`旧` は味方の `DamageToEnemy` / `Kills` の合計、`新` は敵の `DamageTaken`（− 敵の");
        Console.WriteLine("`TakenFromAlly`）/ 敵の死亡数。**差はそのまま「帰属を持たない削り」の量**");
        Console.WriteLine("（毒・燃焼、および胞子のように編成の定義に無い駒が通した削り）。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 与ダメ 旧 | 与ダメ 新 | 倍率 | 撃破 旧 | 撃破 新 | 倍率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (int t in Enumerable.Range(0, nT)
                     .OrderByDescending(t => leg[b][t][0] == 0 ? double.PositiveInfinity : dyn[b][t][0] / leg[b][t][0]))
        {
            string Ratio(double n, double o) => o == 0 ? "—" : $"×{n / o:F2}";
            Console.WriteLine($"| {targets[t].Name} | {leg[b][t][0]:F0} | {dyn[b][t][0]:F0} | {Ratio(dyn[b][t][0], leg[b][t][0])} "
                + $"| {leg[b][t][1]:F2} | {dyn[b][t][2]:F2} | {Ratio(dyn[b][t][2], leg[b][t][1])} |");
        }
        Console.WriteLine();

        // --- 2. 第一近似と残差 ---
        int first = ordered[b][0].K;
        double[] pred = LinearFit(feat[b][first], deg[b]);
        double[] resid = Enumerable.Range(0, nT).Select(t => deg[b][t] - pred[t]).ToArray();
        double r2 = ordered[b][0].R * ordered[b][0].R;

        Console.WriteLine($"### 第一近似 = **{featNames[first]}**（r = {ordered[b][0].R:+0.00;-0.00} / "
            + $"r² = {r2:F2}。突破度のばらつきの {r2 * 100:F0}% を1変数で説明する）");
        Console.WriteLine();
        Console.WriteLine("残差 = 実測の突破度 − この1変数からの線形予測。**残差が大きい編成が、");
        Console.WriteLine("地力以外の何かを持っている編成**——次に設計する効果の入口はここにある。");
        Console.WriteLine();

        // --- 3. 残差と他の特徴量の相関（1段だけ） ---
        var rcors = Enumerable.Range(0, nF).Where(k => k != first)
            .Select(k => { var c = Correlate(feat[b][k], resid); return (K: k, c.R, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();

        Console.WriteLine("#### 残差と他の特徴量の相関（1段のみ。|r| の降順・上位8）");
        Console.WriteLine();
        Console.WriteLine("| 区分 | 特徴量 | 残差との r |");
        Console.WriteLine("|:-:|---|--:|");
        foreach (var x in rcors.Take(8))
            Console.WriteLine($"| {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} | {x.R:+0.00;-0.00} |");
        Console.WriteLine();
        Console.WriteLine($"2変数（{featNames[first]} + 残差との相関1位の {featNames[rcors[0].K]}）の R² = "
            + $"**{R2Two(ordered[b][0].R, Correlate(feat[b][rcors[0].K], deg[b]).R, Correlate(feat[b][first], feat[b][rcors[0].K]).R):F2}**"
            + $"（1変数の {r2:F2} から）。**3変数以上は n = {nT} では意味を持たないのでやらない。**");
        Console.WriteLine();

        // --- 4. 残差の上位・下位5編成 ---
        var byResid = Enumerable.Range(0, nT).Where(t => !double.IsNaN(resid[t]))
            .OrderByDescending(t => resid[t]).ToArray();
        Console.WriteLine("#### 残差の上位・下位5編成");
        Console.WriteLine();
        Console.WriteLine("| 向き | 編成 | 実測 | 予測 | 残差 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (int t in byResid.Take(5))
            Console.WriteLine($"| 予測より**強い** | {targets[t].Name} | {deg[b][t]:F3} | {pred[t]:F3} | {resid[t]:+0.000;-0.000} |");
        foreach (int t in byResid.Reverse().Take(5))
            Console.WriteLine($"| 予測より**弱い** | {targets[t].Name} | {deg[b][t]:F3} | {pred[t]:F3} | {resid[t]:+0.000;-0.000} |");
        Console.WriteLine();

        // --- 5. 静的だけでどこまで説明できるか ---
        // 「編成を組んだ時点で結果がほぼ決まっている」かどうかは設計上とても重いので、
        // 動的を混ぜた値とは別に、静的だけの説明力をはっきり出す（§4-2）。
        var bestS = ordered[b].First(x => isStatic[x.K]);
        var statPairs = new List<(int A, int B, double R2)>();
        for (int i = 0; i < nS; i++)
            for (int j = i + 1; j < nS; j++)
                statPairs.Add((i, j, R2Two(Correlate(feat[b][i], deg[b]).R,
                                       Correlate(feat[b][j], deg[b]).R,
                                       Correlate(feat[b][i], feat[b][j]).R)));
        var topPairs = statPairs.OrderByDescending(p => p.R2).Take(3).ToArray();

        Console.WriteLine("#### 静的だけの説明力（戦わずにどこまで分かるか）");
        Console.WriteLine();
        Console.WriteLine($"- 静的1変数の最良: **{featNames[bestS.K]}** r = {bestS.R:+0.00;-0.00} / r² = {bestS.R * bestS.R:F2}");
        foreach (var p in topPairs)
            Console.WriteLine($"- 静的2変数: {featNames[p.A]} + {featNames[p.B]} → R² = **{p.R2:F2}**");
        Console.WriteLine();
        Console.WriteLine("**静的だけで 0.8 以上説明できるなら、編成を組んだ時点で結果がほぼ決まっている**");
        Console.WriteLine("ことになる（§4-2）。良し悪しの評価ではなく、事実としてこの数字を読む。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 6. 台による違い ---
    Console.WriteLine("## 台による違い（第一近似は入れ替わるか）");
    Console.WriteLine();
    Console.WriteLine("入れ替わるなら、**「地力」は台ごとに違うものを指していた**ことになる（§4-2）。");
    Console.WriteLine();
    Console.WriteLine("| 順位 |" + string.Concat(benches.Select(x => $" {x.Tag}: {x.Name} | r |")));
    Console.WriteLine("|--:|" + string.Concat(benches.Select(_ => "---|--:|")));
    for (int i = 0; i < 5; i++)
        Console.WriteLine($"| {i + 1} |" + string.Concat(Enumerable.Range(0, nB)
            .Select(b => $" {featNames[ordered[b][i].K]} | {ordered[b][i].R:+0.00;-0.00} |")));
    Console.WriteLine();
    var degCor = Correlate(deg[0], deg[1]);
    Console.WriteLine($"**突破度そのものの台間相関: r = {degCor.R:F2} / ρ = {degCor.Rho:F2}**"
        + "（1.00 に近いほど、台を替えても同じ序列が出てくる = 第4〜11期が当たり続けた壁そのもの）。");
    Console.WriteLine();

    // --- 7. 与ダメ効率（オーバーキル）の位置 ---
    // 閾値仮説（「一撃圏を跨ぐかどうかで結果が変わる」）が正しければ、無駄撃ちの指標が
    // 上位に来るはず。来なければ仮説の側を疑う材料になる（§4-2）。
    int over = Array.IndexOf(featNames, "与ダメ効率");
    Console.WriteLine("## 与ダメ効率（オーバーキル）の位置");
    Console.WriteLine();
    Console.WriteLine("| 台 | 単相関の順位 | r | ρ |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        int pos = Array.FindIndex(ordered[b], x => x.K == over);
        Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | {pos + 1} / {nF} "
            + $"| {ordered[b][pos].R:+0.00;-0.00} | {ordered[b][pos].Rho:+0.00;-0.00} |");
    }
    Console.WriteLine();
    Console.WriteLine("**閾値仮説が正しければここが上位に来るはず。** 来なければ仮説の再考が要る。");
    Console.WriteLine();

    // ================= Phase EA: 同語反復を除いた分解（第14期） =================
    //
    // 第13期で毒の穴を塞いだ結果、第一近似は `撃破/戦` r² = 0.90 になった。
    // **これは発見ではなく算術。** 部隊を全滅させることがその部隊を突破することなので、
    // 味方1部隊では 部隊戦数 = 突破数 + 1 になり、全抜きした編成の `撃破/戦` は例外なく
    // 13 ÷ 3 = 4.33 に落ちる（上の警告の通り）。
    //
    // 同じ理屈は第12期の r² 0.41 にも効く。あれは「地力の説明力が4割」ではなく
    // **「言い換えのはずの量が、毒の穴のせいで4割まで落ちていた」**——だから第12期の
    // 「地力は単一の量ではない」という結論は根拠を失っている。**分解はやり直しになる。**
    //
    // ここでやるのは**候補集合だけを変えた測り直し**で、手順も計算方法も第12期・第13期と同じ。
    // 説明力が落ちるのは失敗ではない——落ちた値のほうが本当の説明力（§3-3）。
    // **数字を上げるために候補を戻さない。**
    //
    // 却下した案: 目的変数のほうを言い換えから遠い量に取り替える（勝率・残HP など）。
    // 目的変数を替えると第8期以降の測定すべてと繋がらなくなるうえ、勝率は上下端に張り付いて
    // 序列を潰す（第5期の持ち越し論点(2)）。**動かすのは候補集合の側だけ。**

    // 同語反復の判定。**基準は「突破という結果の言い換えになっていないか」の1本だけ**にする。
    // 「信頼できるか」は混ぜない——混ぜると基準が二重になり、次に特徴量を足すときに使えない。
    //
    // 言い換えが入り込む経路は2つある。
    //   分子経路 — 量そのものが突破の定義に含まれる（部隊を全滅させる＝その部隊を突破する）
    //   分母経路 — 味方1部隊では **部隊戦数 = 突破数 + 1**（全抜き時だけ = 突破数）なので、
    //              `/戦` で割る量はすべて「目的変数 + 1」で割っている
    // **外すのは分子経路だけ。** 分母経路は「平均を取る」操作であって、量の中身を突破の写しに
    // 変えるわけではない。ただし言い張らずに、下の検査表で分母が実際にどれだけ突破度を
    // 運んでいるかを測って出す。比（自傷率・与ダメ効率）は分子分母が同じ部隊戦数で
    // 割られているので、**分母経路が丸ごと打ち消える**。
    var taut = new (bool Excluded, string Reason)[]
    {
        // 与ダメ/戦
        (true,  "**分子経路。** 敵の総HPを削り切ることが突破なので、突破した部隊の数だけ分子が積み上がる。突破度の小数部（最終戦で削った割合）も分子そのもの"),
        // 被ダメ/戦
        (false, "分子は「敵に殴られた量」で、突破の定義に入らない。**分母経路だけ**——最後の1戦（負け戦）が高く、勝ち戦を重ねるほど薄まる構造はあるので、平均として読む"),
        // 撃破/戦
        (true,  "**分子経路。もっとも露骨な言い換え。** 部隊の全滅＝突破。全抜きした編成の値は 13 ÷ 3 = 4.33 に固定され、これは測定結果ではなく算術"),
        // 干渉/戦
        (false, "分子は「誰が起点になったか」の回数で、突破の定義に入らない。**毒軸で構造的に過小**（第13期の残る穴）だが、それは信頼性の問題であって同語反復ではない——**基準を混ぜないので残す。** 単相関は下位帯なので、外しても入れても第一近似は動かない"),
        // 回復/戦
        (false, "分子は味方の回復量で、突破の定義に入らない。**分母経路だけ**"),
        // 自傷率
        (false, "**比なので分母経路が打ち消える**（分子分母とも同じ部隊戦数で割られる）。分子は味方同士の削りで、突破の写しではない"),
        // 与ダメ効率
        (false, "分子・分母とも分子経路の量だが、**比を取ると言い換えの部分がそのまま打ち消える**——`与ダメ ÷ 撃破` は「1体倒すのに振った量」で、部隊戦数も消える。**台で符号が反転することが証拠**（下の検査表）: 言い換えなら符号は反転しない"),
    };

    Console.WriteLine("## 同語反復の除外（第14期 Phase EA）");
    Console.WriteLine();
    Console.WriteLine("**第13期の第一近似 `撃破/戦` r² 0.90 は発見ではなく算術だった。** ここでは");
    Console.WriteLine("「突破という結果の言い換えになっている量」を候補から外して、**同じ手順・同じ計算方法で**");
    Console.WriteLine("分解をやり直す。目的変数も台も seed も変えていない——動かしたのは候補集合だけ。");
    Console.WriteLine();
    Console.WriteLine("### 判定の基準");
    Console.WriteLine();
    Console.WriteLine("基準は「**突破という結果の言い換えになっていないか**」の1本だけ。");
    Console.WriteLine("**「信頼できるか」は混ぜない**（混ぜると基準が二重になり、次に特徴量を足すときに使えない）。");
    Console.WriteLine();
    Console.WriteLine("言い換えが入り込む経路は2つある。");
    Console.WriteLine();
    Console.WriteLine("- **分子経路** — 量そのものが突破の定義に含まれる（部隊を全滅させる＝その部隊を突破する）");
    Console.WriteLine("- **分母経路** — 味方1部隊では `部隊戦数 = 突破数 + 1`（全抜き時だけ `= 突破数`）なので、");
    Console.WriteLine("  `/戦` で割る量はすべて「目的変数 + 1」で割っている");
    Console.WriteLine();
    Console.WriteLine("**外すのは分子経路だけ。** 分母経路は平均を取る操作であって、量の中身を突破の写しに");
    Console.WriteLine("変えるわけではない。比（`自傷率`・`与ダメ効率`）は分子分母が同じ部隊戦数で割られているので、");
    Console.WriteLine("**分母経路が丸ごと打ち消える**。静的特徴量はどちらの経路も持たない（戦わずに決まる量なので）。");
    Console.WriteLine();

    // 分母経路が実際にどれだけ目的変数を運んでいるか。言葉ではなく数字で出す。
    Console.WriteLine("### 検算: 分母（部隊戦数）はどれだけ突破度を運んでいるか");
    Console.WriteLine();
    Console.WriteLine("`部隊戦数 ÷ 試行` そのものを突破度に当てる。**算術の恒等式なので 1.00 に近いはず**——");
    Console.WriteLine("近ければ「`/戦` の分母は目的変数そのもの」であることの確認になり、");
    Console.WriteLine("分母経路を残す判断はその上での判断になる。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 部隊戦数/試行 × 突破度 の r | ρ |");
    Console.WriteLine("|---|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        var c = Correlate(bps[b], deg[b]);
        Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | {c.R:+0.000;-0.000} | {c.Rho:+0.000;-0.000} |");
    }
    Console.WriteLine();

    Console.WriteLine("### 判定表（動的7種。静的8種はすべて残す）");
    Console.WriteLine();
    Console.WriteLine("`部隊戦数 r` は特徴量と分母の相関——**これが ±1 に近い特徴量は、中身が分母そのもの**。");
    Console.WriteLine();
    Console.WriteLine("| 特徴量 | 突破度 r 主 | 突破度 r 従 | 部隊戦数 r 主 | 部隊戦数 r 従 | 判定 | 理由 |");
    Console.WriteLine("|---|--:|--:|--:|--:|:-:|---|");
    for (int k = 0; k < dynNames.Length; k++)
    {
        var col = Enumerable.Range(0, nB)
            .Select(b => Enumerable.Range(0, nT).Select(t => dyn[b][t][k]).ToArray()).ToArray();
        Console.WriteLine($"| {dynNames[k].Name} "
            + $"| {Correlate(col[0], deg[0]).R:+0.00;-0.00} | {Correlate(col[1], deg[1]).R:+0.00;-0.00} "
            + $"| {Correlate(col[0], bps[0]).R:+0.00;-0.00} | {Correlate(col[1], bps[1]).R:+0.00;-0.00} "
            + $"| {(taut[k].Excluded ? "**除外**" : "残す")} | {taut[k].Reason} |");
    }
    Console.WriteLine();

    bool[] keep = Enumerable.Range(0, nF).Select(k => k < nS || !taut[k - nS].Excluded).ToArray();
    int[] cand = Enumerable.Range(0, nF).Where(k => keep[k]).ToArray();
    Console.WriteLine($"**除外後の候補は {cand.Length} 種**（静的 {nS} + 動的 {cand.Length - nS}）。"
        + $"外したのは {string.Join(" / ", Enumerable.Range(0, nF).Where(k => !keep[k]).Select(k => "`" + featNames[k] + "`"))}。");
    Console.WriteLine();

    // --- 除外後の分解（第12期・第13期と同じ手順） ---
    var orderedEa = new (int K, double R, double Rho, int N)[nB][];
    var residEa = new double[nB][];
    var r2Ea = new double[nB];
    for (int b = 0; b < nB; b++)
    {
        orderedEa[b] = cand
            .Select(k => { var c = Correlate(feat[b][k], deg[b]); return (K: k, c.R, c.Rho, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();

        Console.WriteLine($"### 除外後の分解: {benches[b].Tag}の台（{benches[b].Name}）");
        Console.WriteLine();
        Console.WriteLine($"#### 単相関（{cand.Length} 特徴量 × 突破度。|r| の降順）");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 区分 | 特徴量 | r | ρ | n |");
        Console.WriteLine("|--:|:-:|---|--:|--:|--:|");
        for (int i = 0; i < orderedEa[b].Length; i++)
        {
            var x = orderedEa[b][i];
            Console.WriteLine($"| {i + 1} | {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} "
                + $"| {x.R:+0.00;-0.00} | {x.Rho:+0.00;-0.00} | {x.N} |");
        }
        Console.WriteLine();

        int first = orderedEa[b][0].K;
        double[] pred = LinearFit(feat[b][first], deg[b]);
        residEa[b] = Enumerable.Range(0, nT).Select(t => deg[b][t] - pred[t]).ToArray();
        r2Ea[b] = orderedEa[b][0].R * orderedEa[b][0].R;
        double r2Old = ordered[b][0].R * ordered[b][0].R;

        Console.WriteLine($"#### 第一近似 = **{featNames[first]}**（r = {orderedEa[b][0].R:+0.00;-0.00} / "
            + $"**r² = {r2Ea[b]:F3}**）");
        Console.WriteLine();
        Console.WriteLine($"第13期の第一近似は `{featNames[ordered[b][0].K]}` で r² = {r2Old:F3} だった。"
            + $"**差の {r2Old - r2Ea[b]:F3} は言い換えが持っていた分**で、地力の説明力ではない。");
        Console.WriteLine();
        if (r2Ea[b] < 0.30)
        {
            Console.WriteLine("> **停止条件（§6-7）に触れている: 除外後の最良が r² 0.30 を下回った。**");
            Console.WriteLine("> この台では **「地力は既存の特徴量では表せない」**——15種のうち言い換えでない");
            Console.WriteLine($"> {cand.Length}種を当てても、突破度のばらつきの3割を説明できない。");
            Console.WriteLine("> 次に何を測るべきかが変わるので、先へ進む前に報告すること。");
            Console.WriteLine();
        }

        var rcors = cand.Where(k => k != first)
            .Select(k => { var c = Correlate(feat[b][k], residEa[b]); return (K: k, c.R, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();
        Console.WriteLine("#### 残差と他の特徴量の相関（1段のみ。|r| の降順・上位8）");
        Console.WriteLine();
        Console.WriteLine("| 区分 | 特徴量 | 残差との r |");
        Console.WriteLine("|:-:|---|--:|");
        foreach (var x in rcors.Take(8))
            Console.WriteLine($"| {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} | {x.R:+0.00;-0.00} |");
        Console.WriteLine();
        Console.WriteLine($"2変数（{featNames[first]} + {featNames[rcors[0].K]}）の R² = "
            + $"**{R2Two(orderedEa[b][0].R, Correlate(feat[b][rcors[0].K], deg[b]).R, Correlate(feat[b][first], feat[b][rcors[0].K]).R):F3}**"
            + $"（1変数の {r2Ea[b]:F3} から）。**3変数以上は n = {nT} では意味を持たないのでやらない。**");
        Console.WriteLine();

        var byResid = Enumerable.Range(0, nT).Where(t => !double.IsNaN(residEa[b][t]))
            .OrderByDescending(t => residEa[b][t]).ToArray();
        Console.WriteLine("#### 残差の上位・下位5編成");
        Console.WriteLine();
        Console.WriteLine("| 向き | 編成 | 実測 | 予測 | 残差 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (int t in byResid.Take(5))
            Console.WriteLine($"| 予測より**強い** | {targets[t].Name} | {deg[b][t]:F3} | {pred[t]:F3} | {residEa[b][t]:+0.000;-0.000} |");
        foreach (int t in byResid.Reverse().Take(5))
            Console.WriteLine($"| 予測より**弱い** | {targets[t].Name} | {deg[b][t]:F3} | {pred[t]:F3} | {residEa[b][t]:+0.000;-0.000} |");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 第12期・第13期・第14期の対比 ---
    // 静的だけの説明力は3期とも同じ値になる（静的特徴量は一度も定義を変えていない）。
    // **それ自体が答えの一部**——同語反復を外しても静的の数字は動かないので、
    // 「静的では 0.35 / 0.16 しか説明できない」は第12期からずっと変わらない事実だった。
    Console.WriteLine("### 第12期・第13期・第14期の対比");
    Console.WriteLine();
    Console.WriteLine("| 台 | 期 | 候補 | 第一近似 | r | r² | 静的1変数 r² | 静的2変数 R² |");
    Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        var bestS = ordered[b].First(x => isStatic[x.K]);
        double bestPair = 0;
        for (int i = 0; i < nS; i++)
            for (int j = i + 1; j < nS; j++)
                bestPair = Math.Max(bestPair, R2Two(Correlate(feat[b][i], deg[b]).R,
                                                    Correlate(feat[b][j], deg[b]).R,
                                                    Correlate(feat[b][i], feat[b][j]).R));
        void Row(string era, int n, (int K, double R, double Rho, int N) x) =>
            Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | {era} | {n} | {featNames[x.K]} "
                + $"| {x.R:+0.00;-0.00} | **{x.R * x.R:F3}** | {bestS.R * bestS.R:F2} | {bestPair:F2} |");
        Row("第12期（味方側）", nF, orderedOldAll[b][0]);
        Row("第13期（受け手側）", nF, ordered[b][0]);
        Row("**第14期（言い換え除外）**", cand.Length, orderedEa[b][0]);
    }
    Console.WriteLine();
    Console.WriteLine("**静的だけの説明力は3期とも同じ値**——静的特徴量は一度も定義を変えていないので");
    Console.WriteLine("動きようがない。**それ自体が答えの一部**で、「編成を組んだ時点ではほとんど決まっていない」");
    Console.WriteLine("という第12期の観察は、同語反復とは無関係に最初から正しかった。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= Phase EB: 反撃軸の残差（第14期） =================
    //
    // 第13期の残差で、穴を塞いだ後も沈んだままだったのが反撃軸（惨禍×被弾強化 −0.307 /
    // 反撃改2 −0.297）。反撃は `ctx.ApplyDamage(source, back, self)` と source 付きなので
    // 受け手側へ移しても 1pt も動かない——**与ダメも撃破も出ているのに突破度に届いていない。**
    //
    // 仮説（§4-1）: 反撃軸は出力を HP で買っている。HP は会戦を跨ぐ唯一の持ち越し資源なので、
    // 単発戦の額面ほど会戦では価値が無い。正しければ **自傷率と残差が負に相関する**はず。
    //
    // **§4-3 が要になる。** 第9期で 逆しま改 は自傷率 61.6% で上位だが強い編成なので、
    // 自傷率が高いこと自体は弱さの原因ではない。反撃軸だけが沈んでいるなら、原因は
    // 「HP で買っているから」ではなく**反撃という出力の出し方に固有の何か**を指す。
    //
    // 新しい計測は足さない。`bill`（第9期・測定台113%・HP ベースの自傷率）と単発戦の勝率
    // （`docs/balance.md` と同じ計算）を**同じ実行の中で**取り直して突き合わせるだけ。
    // 別の実行から数字を引くと、動いたのが定義のせいか実行のせいか決まらない（第13期の作法）。
    var bill113 = BenchColumn113();
    var billRate = targets.Select(t => MeasureBill(t.F, bill113, PowerSeeds).SelfHarmRate * 100).ToArray();
    var soloWin = new double[nT];
    for (int t = 0; t < nT; t++)
    {
        double sum = 0;
        foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
        {
            int wins = 0;
            for (int seed = 0; seed < PowerSeeds; seed++)
                if (BattleEngine.Run(targets[t].F, st.Enemy, seed, verbose: false).PlayerWon) wins++;
            sum += wins * 100.0 / PowerSeeds;
        }
        soloWin[t] = sum / EnemyCatalog.Stages.Count;
    }
    double[] soloRank = AverageRanksDesc(soloWin);
    var degRank = Enumerable.Range(0, nB).Select(b => AverageRanksDesc(deg[b])).ToArray();
    // 第13期の残差（`撃破/戦` からの残差）。仮説が語っていたのはこちらの残差なので併記する。
    var resid13 = new double[nB][];
    for (int b = 0; b < nB; b++)
    {
        double[] p13 = LinearFit(feat[b][ordered[b][0].K], deg[b]);
        resid13[b] = Enumerable.Range(0, nT).Select(t => deg[b][t] - p13[t]).ToArray();
    }

    Console.WriteLine("## 反撃軸の残差（第14期 Phase EB）");
    Console.WriteLine();
    Console.WriteLine("第13期の残差で、穴を塞いだ後も沈んだままだったのが反撃軸。反撃は");
    Console.WriteLine("`ctx.ApplyDamage(source, back, self)` と source 付きなので受け手側へ移しても 1pt も動かない");
    Console.WriteLine("——**与ダメも撃破も出ているのに突破度に届いていない。**");
    Console.WriteLine();
    Console.WriteLine("仮説: **反撃軸は出力を HP で買っている。** HP は会戦を跨ぐ唯一の持ち越し資源なので、");
    Console.WriteLine("単発戦の額面ほど会戦では価値が無い。正しければ**自傷率と残差が負に相関する**はず。");
    Console.WriteLine();
    Console.WriteLine("`bill 自傷率` は第9期の定義（測定台113%・失った HP のうち自分で削った割合）、");
    Console.WriteLine("`power 自傷率` は同じ台の tally 比（`TakenFromAlly ÷ DamageTaken`）。**台も定義も違う**ので");
    Console.WriteLine("両方出す——片方だけだと、出た/出なかったのが定義のせいか台のせいか決まらない。");
    Console.WriteLine();

    Console.WriteLine("### 1. 自傷率と残差の相関");
    Console.WriteLine();
    Console.WriteLine("残差は **Phase EA の残差**（同語反復を除いた第一近似からの残差）。");
    Console.WriteLine("第13期の残差（`撃破/戦` からの残差）も並べる——仮説が語っていたのはそちらの残差なので。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 自傷率 | EA 残差との r | 第13期 残差との r |");
    Console.WriteLine("|---|---|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        double[] pw = Enumerable.Range(0, nT).Select(t => dyn[b][t][5] * 100).ToArray();
        Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | bill（測定台113%） "
            + $"| {Correlate(billRate, residEa[b]).R:+0.00;-0.00} | {Correlate(billRate, resid13[b]).R:+0.00;-0.00} |");
        Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | power（同台の tally 比） "
            + $"| {Correlate(pw, residEa[b]).R:+0.00;-0.00} | {Correlate(pw, resid13[b]).R:+0.00;-0.00} |");
    }
    Console.WriteLine();
    Console.WriteLine("**仮説が正しければ全部が負。** 符号が揃わないなら、自傷率は残差の説明になっていない。");
    Console.WriteLine();

    // 名指しの表。**4編成は必ず出す**（§4-2 の2）。反撃改3 も同じ軸なので添える。
    var ebRows = new (string Group, string Key)[]
    {
        ("反撃軸", "惨禍×被弾強化"), ("反撃軸", "反撃 ("), ("反撃軸", "反撃改 ("),
        ("反撃軸", "反撃改2"), ("反撃軸", "反撃改3"),
        ("逆しま系", "逆しま ("), ("逆しま系", "逆しま改"), ("逆しま系", "逆しま+後備え"),
    };
    Console.WriteLine("### 2. 反撃軸と逆しま系の名指しの表");
    Console.WriteLine();
    Console.WriteLine("**逆しま系を同じ表に並べるのが要点**（§4-3）。第9期で 逆しま改 は自傷率 61.6% で上位");
    Console.WriteLine("だったが強い編成なので、**自傷率が高いこと自体は弱さの原因ではない。**");
    Console.WriteLine("反撃軸だけが沈んでいるなら、原因は自傷率ではなく反撃そのものにある。");
    Console.WriteLine();
    Console.WriteLine("| 軸 | 編成 | bill 自傷率 | power 自傷率(主) | 被ダメ/戦(主) | 与ダメ効率(主) | 突破度(主) | EA 残差(主) | 第13期 残差(主) | 突破度(従) | EA 残差(従) |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var (group, key) in ebRows)
    {
        int[] hit = Enumerable.Range(0, nT).Where(t => targets[t].Name.Contains(key)).ToArray();
        if (hit.Length != 1)
        {
            Console.WriteLine($"| {group} | `{key}` に一致する編成が {hit.Length} 件 | — | — | — | — | — | — | — | — | — |");
            continue;
        }
        int t2 = hit[0];
        Console.WriteLine($"| {group} | {targets[t2].Name} | {billRate[t2]:F1}% | {dyn[0][t2][5] * 100:F1}% "
            + $"| {dyn[0][t2][1]:F0} | {dyn[0][t2][6]:F1} | {deg[0][t2]:F3} | {residEa[0][t2]:+0.000;-0.000} "
            + $"| {resid13[0][t2]:+0.000;-0.000} | {deg[1][t2]:F3} | {residEa[1][t2]:+0.000;-0.000} |");
    }
    Console.WriteLine();

    Console.WriteLine("### 3. 単発戦と会戦の順位");
    Console.WriteLine();
    Console.WriteLine($"`単発戦` は全 {EnemyCatalog.Stages.Count} ステージの独立勝率の平均（`docs/balance.md` と");
    Console.WriteLine($"同じ計算・同じ seed 0..{PowerSeeds - 1}）。会戦は突破度。**順位は 1 が最良。**");
    Console.WriteLine("反撃軸が単発戦で強く会戦で弱いなら、順位の差がプラスに大きく出る。");
    Console.WriteLine();
    Console.WriteLine("| 軸 | 編成 | 単発戦 平均勝率 | 単発戦 順位 | 突破度 順位(主) | 差(主) | 突破度 順位(従) | 差(従) |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    foreach (var (group, key) in ebRows)
    {
        int[] hit = Enumerable.Range(0, nT).Where(t => targets[t].Name.Contains(key)).ToArray();
        if (hit.Length != 1) continue;
        int t2 = hit[0];
        Console.WriteLine($"| {group} | {targets[t2].Name} | {soloWin[t2]:F1}% | {soloRank[t2]:F1} "
            + $"| {degRank[0][t2]:F1} | {degRank[0][t2] - soloRank[t2]:+0.0;-0.0} "
            + $"| {degRank[1][t2]:F1} | {degRank[1][t2] - soloRank[t2]:+0.0;-0.0} |");
    }
    Console.WriteLine();
    var solo0 = Correlate(soloWin, deg[0]);
    var solo1 = Correlate(soloWin, deg[1]);
    Console.WriteLine($"**全 {nT} 編成での単発戦 × 突破度: 主 r = {solo0.R:F2} / ρ = {solo0.Rho:F2}、"
        + $"従 r = {solo1.R:F2} / ρ = {solo1.Rho:F2}。**");
    Console.WriteLine("単発戦の平均勝率と突破度の相関がそもそも高ければ、「単発戦では強いのに会戦で弱い」は");
    Console.WriteLine("編成の一般的な性質ではなく、名指しの編成に固有の話になる。");
    Console.WriteLine();
    Console.Out.Flush();

    return;
}
}
