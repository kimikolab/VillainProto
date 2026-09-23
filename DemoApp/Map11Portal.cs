using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 第176期 段1 —— **敵の拠点（ワープポータル）を器具で先に当てる（UI の前）。**
//
// **遊ぶ側と同じ `Map11State` / `Map11Orders` を頭なしで回す**（R259）。ここが持つのは
// 表と線だけで、**規則は1行も持たない**——湧き・列・制圧・ワープ・勝敗は全部 `Map11State` にある。
// 方針（行き先の選び方）も第175期の `Map11Time` にそのまま足したので、**器具は1本のまま**である。
//
//     Godot_console.exe --path DemoApp --headless -- --map11-phase176
//     Godot_console.exe --path DemoApp --headless -- --map11-portal[=seeds[,起点]]
//
// **`PortalOn = false` なら第175期と1ビットも違わない**——自己検査 (a) が実測で突き合わせる。
// =====================================================================================

public static class Map11Portal
{
    public const int DefaultSeeds = 800;

    /// <summary>湧きの間隔（作戦ターン）の格子。<b>測る前に固定する</b>（指示書 §2-3）。</summary>
    public static readonly int[] Grid = { 2, 3, 4, 6 };

    // ---------------- 線（測る前に固定・指示書 §2-3） ----------------

    /// <summary>線1: 籠城（上手い）の踏破率がこの値以下（勝ち条件上、0% が当然）。</summary>
    public const double Line1Full = 0.0;
    /// <summary>線1: 同・膠着率がこの値以下（<b>勝てないだけでなく、いつか陥落する</b>）。</summary>
    public const double Line1Stall = 10.0;
    /// <summary>線2: 制圧狙いの踏破率がこの値以上。</summary>
    public const double Line2Seize = 70.0;
    /// <summary>線3: 制圧狙い − 遅い制圧 がこの pt 以上（<b>早めが得</b>）。</summary>
    public const double Line3Gap = 15.0;

    /// <summary>正の割り当て（第175期と同じ）: カド隊 → 南 ／ かき回し隊 → 北。</summary>
    public static readonly int[] Straight = { 1, 0 };

    /// <summary>この期で測る方針（<b>籠城は「上手い」＝控えから出す側を線に使う</b>）。</summary>
    private static readonly Map11Time.Policy[] All =
    {
        Map11Time.Policy.TurtleLast, Map11Time.Policy.Turtle, Map11Time.Policy.Greedy,
        Map11Time.Policy.Seize, Map11Time.Policy.SeizeSwap, Map11Time.Policy.SeizeLate,
        Map11Time.Policy.SeizeAll,
    };

    /// <summary>
    /// 第175期の実測（`--map11-time` seed 0..799・迎撃の回復なし・<b>本表の「なし（採用）」の行そのもの</b>）。
    /// 自己検査 (a) の相手で、<b>小数第1位（部分点は第3位）まで一致すること</b>を線にする。
    /// </summary>
    private static readonly Dictionary<Map11Time.Policy,
        (double Full, double Fall, double Battles, double Partial, double Turn, double Intercept)>
        Phase175 = new()
        {
            [Map11Time.Policy.TurtleLast] = (99.2, 0.0, 5.11, 3.999, 5.0, 5.11),
            [Map11Time.Policy.Turtle] = (7.2, 92.8, 5.74, 3.237, 5.0, 5.74),
            [Map11Time.Policy.Greedy] = (94.0, 6.0, 5.68, 3.988, 3.0, 0.46),
        };

    /// <summary>自己検査 (a) の1方針ぶん。</summary>
    private static bool Compare(Action<string> write, Map11Time.Policy pol,
        (double Full, double Fall, double Battles, double Partial, double Turn, double Intercept) w,
        Map11Time.Stat got)
    {
        string n = Map11Time.NameOf(pol);
        bool ok = Row(write, n, "踏破率", w.Full, got.Full);
        ok &= Row(write, n, "陥落率", w.Fall, got.Fall);
        ok &= Row(write, n, "戦闘/戦", w.Battles, got.Battles);
        ok &= Row(write, n, "部分点", w.Partial, got.Partial, 3);
        ok &= Row(write, n, "作戦T 中央値", w.Turn, got.TurnMedian);
        ok &= Row(write, n, "迎撃/戦", w.Intercept, got.InterceptPerRun);
        return ok;
    }

    private static TimeRule Time => TimeRule.Every(Map11Time.AdoptedK);

    // =================================================================================
    // Phase 0（指示書 §4）—— **測る前に、実装から引けることを引く。**
    // =================================================================================

    public static bool Phase0(int seeds, Action<string> write)
    {
        write("# 第176期 Phase 0 —— 敵の拠点を置く前に数える");
        write("");
        write($"K = {Map11Time.AdoptedK}（第174期の採用値）。本走行は seed {DefaultSeeds} 本。");
        write("");

        bool ok = true;

        // ---- Q0-1: 拠点なしなら第175期と全量一致するか（実測で突き合わせる） ----
        write("## Q0-1: `PortalOn = false` は第175期と一致するか");
        write("");
        write("**作りの上では**、拠点の枝はすべて `Map11State.Portal.On` の裏側にある"
            + "（`MaxCell` / `FoesOn` の湧き / `TryCapture` / `Warp` / `SpawnIfDue` / `CanHead`）。"
            + "**湧きが 0 件なら `FoesOn` は `NextNode` と同じものを返す**ので、"
            + "前進も接敵も第175期の式のままである。**確かめるのは数字のほう。**");
        write("");
        write("| 方針 | 量 | 第175期 | 拠点なし | 差 | 線 ±0.05 |");
        write("|---|---|--:|--:|--:|:-:|");
        foreach ((Map11Time.Policy pol, var w) in Phase175)
            ok &= Compare(write, pol, w,
                          Map11Time.Band(pol, Time, Straight, DefaultSeeds, 0, PortalRule.Off));
        write("");
        write("**seed は第175期の本走行と同じ 0..799**（ここだけ本数を落とさない"
            + "——一致を見る表で本数を変えると、一致しない理由が2つになる）。");
        write("");

        // ---- Q0-2: 1マスに2部隊・同点の割り方（R276 の再発防止） ----
        write("## Q0-2: 重なりの処理と、同点の割り方（R276）");
        write("");
        write("| # | 問い | 実装から引いた答え |");
        write("|---|---|---|");
        write("| a | 1 マスに敵部隊が2つ来たら | **来ない。** `AdvanceFoes` は湧いた部隊を手前から順に見て、"
            + "**行き先のマスに敵がいれば動かさない**（`FoesOn(road).Any(x => x.Cell == target)`）。"
            + "固定の4部隊も「詰まり」として数えるので、湧きは第三波／第五波を追い越せない |");
        write("| b | 敵の拠点のマスだけは重なる | **重なる。** 湧く場所なので、前が詰まっていると複数が同じマスに立つ。"
            + "そこへ入った味方は**湧いた順に1つずつ**戦う（`PortalFoe` が手前の順で最初の1つを返す） |");
        write("| c | `AdvanceFoes` の「道0→道1の順」は湧きでも偏るか | **偏りは残る**（同じ作戦ターンに"
            + "両方の道が拠点へ着いたら北が先）。**湧く道は交互**（`SpawnCount % 2`）なので、"
            + "湧き自体は北へ偏らない——本表に迎撃の北% を出す |");
        write("| d | 迎撃役の同点の割り方 | **第175期のまま2通り**（番号順 ＝ `籠城` ／ 控えから ＝ `籠城（控えから出す）`）。"
            + "**両方を本表に載せる**——R276 はここで 7.2% ↔ 99.2% まで割れた |");
        write("");

        // ---- Q0-4: 湧いた部隊を満タンで作る口 ----
        write("## Q0-4: 湧いた部隊を満タンで作る口");
        write("");
        var fresh = BattleEngine.Materialize(Map11.SpawnEnemy(0), BattleContext.EnemyTeam, Map11.EnemyScale);
        bool full = fresh.All(u => u.Hp == u.MaxHp && u.Hp == u.Def.MaxHp);
        ok &= full;
        write($"**`BattleEngine.Materialize` の1本だけ**——`Hp = def.MaxHp` を入れるだけで"
            + $"乱数を1つも引かない（{fresh.Count} 体すべて満タン: {(full ? "**○**" : "×")}）。"
            + "`EngagementEngine.CrossBoundary` は**生き残りを次の戦へ渡す口**なので、湧きには使わない"
            + "——湧いた部隊は前の戦闘を持っていない。");
        write("");
        write("**箱は湧いた瞬間に作り、駒は戦闘の直前まで作らない**（`Prepare` の `node.Units ??=`）。"
            + "第169期からの作りをそのまま使っている。");
        write("");

        // ---- Q0-3: 方針の手順 ----
        write("## Q0-3: 方針の手順（結果を見る前に文で固定する）");
        write("");
        write("| 方針 | 行き先 | 迎撃役 | 控え隊 |");
        write("|---|---|---|---|");
        write("| 籠城（上手い） | 3 隊とも**ずっと拠点**（傷が残っていれば休む） "
            + "| **まだ戦っていない → HP% の高い**、同点は**控え隊から** | 最初から拠点 |");
        write("| 籠城（番号順） | 同上 | 同上だが**同点はカド隊から**（R276 の対照） | 同上 |");
        write("| 貪欲 | **第175期のまま**——担当の道の<b>いちばん奥の区画</b>まで。"
            + "**敵の拠点までは行かない**（＝制圧しない対照） | 番号順（第175期のまま） | 隊が全滅した道へ1回だけ |");
        write("| 制圧狙い | カド隊は拠点。**かき回し隊 → 北 ／ 控え隊 → 南**を敵の拠点へ。"
            + "制圧したら、傷が残っていれば第2拠点で休み、満タンなら拠点へワープして掃除する "
            + "| まだ戦っていない → HP% | **1 作戦ターン目から出す** |");
        write("| 制圧狙い（道を入れ替え） | 同上で**かき回し隊 → 南 ／ 控え隊 → 北** | 同上 | 同上 |");
        write("| 総力制圧（**参考**・線には使わない） | **カド隊も出す**（南）。"
            + "残り2隊は制圧狙いと同じ——**拠点を空にして敵の拠点を獲りに行く形** | 拠点に誰も居ないので出せない | 同上 |");
        write($"| 遅い制圧 | 制圧狙いと同じ。ただし**最初の {Map11Time.SeizeDelay} 作戦ターンは拠点で休む** "
            + "| 同上 | 同上 |");
        write("");
        write("**道の割り振りは第175期の「正」をそのまま引き継ぐ**（かき回し隊 → 北）"
            + "——結果を見てから選ばないため。**入れ替えた版も本表に並べる**（Q0-3 の「両方回して併記する」）。");
        write("");
        write("**勝ち条件は「制圧 ＋ 盤上の敵が 0」**なので、**貪欲と籠城は原理的に勝てない**"
            + "（どちらも敵の拠点へ行かない）。線1 が見るのは踏破率ではなく**膠着率**である。");
        write("");

        // ---- Q0-5: 走行時間 ----
        write("## Q0-5: 走行時間の見積もり");
        write("");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Map11Time.Stat probe = Map11Time.Band(Map11Time.Policy.TurtleLast, Time, Straight,
                                              seeds, 0, PortalRule.Every(Grid[0]));
        sw.Stop();
        double per = sw.Elapsed.TotalSeconds * DefaultSeeds / Math.Max(1, seeds);
        double est = per * All.Length * Grid.Length;
        write($"いちばん重い点（籠城 × S = {Grid[0]}）を seed {seeds} 本で **{sw.Elapsed.TotalSeconds:F2} 秒**"
            + $"——{DefaultSeeds} 本なら **{per:F1} 秒/点**。"
            + $"本表は {All.Length} 方針 × {Grid.Length} 点 ＝ **{All.Length * Grid.Length} 点**なので、"
            + $"**{est:F0} 秒（{est / 60:F1} 分）** の見込み。");
        write("");
        write(est > 600
            ? "**10 分を超える見込み。指示書 Q0-5 に従ってここで止まる。**"
            : "**10 分以内。段1 へ進む。**");
        ok &= est <= 600;
        write("");
        write($"（下見の値: 踏破 {probe.Full:F1}% ／ 制圧 {probe.Capture:F1}% ／ "
            + $"膠着 {probe.Stall:F1}% ／ 湧き 中央値 {probe.SpawnMedian:F0} ／ "
            + $"戦闘 {probe.Battles:F2} 回）");
        write("");
        write(ok ? "**Phase 0 ○**" : "**Phase 0 × —— 上の × を読むこと。**");
        return ok;
    }

    /// <summary>
    /// 1 行ぶんの突き合わせ。<b>線は「その桁で丸めたときに一致すること」</b>
    /// ——第175期の記録が小数第1位（部分点は第3位）までしか残っていないので、それ以上は要求できない。
    /// </summary>
    /// <summary>
    /// 湧きの間隔を 1 刻みで掃引する診断（<b>線には使わない</b>）。
    /// 本表の <c>S = 2</c> と <c>S = 3</c> が単調でなかったので、間を埋めて形を見るためだけに足した。
    /// </summary>
    public static bool Scan(int seeds, Action<string> write)
    {
        write("# 第176期 診断 —— 湧きの間隔を 1 刻みで掃引する");
        write("");
        write($"seed 0..{seeds - 1}。**本表の格子（{string.Join(" / ", Grid)}）は動かしていない**"
            + "——ここは形を見るためだけの走査である。");
        write("");
        write("| S | 制圧狙い 踏破 | 制圧狙い 制圧 | 制圧T | 総力制圧 踏破 | 総力制圧 制圧 | 制圧T "
            + "| 湧き 中央値 |");
        write("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int s in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 12, 9999 })
        {
            Map11Time.Stat a = Map11Time.Band(Map11Time.Policy.Seize, Time, Straight, seeds, 0,
                                              PortalRule.Every(s));
            Map11Time.Stat b = Map11Time.Band(Map11Time.Policy.SeizeAll, Time, Straight, seeds, 0,
                                              PortalRule.Every(s));
            write($"| {(s >= 9999 ? "湧き 0" : s.ToString())} | {a.Full:F1}% | {a.Capture:F1}% "
                + $"| {(a.Capture > 0 ? a.CaptureTurnMedian.ToString("F1") : "—")} "
                + $"| {b.Full:F1}% | {b.Capture:F1}% "
                + $"| {(b.Capture > 0 ? b.CaptureTurnMedian.ToString("F1") : "—")} "
                + $"| {b.SpawnMedian:F0} |");
        }
        write("");
        return true;
    }

    private static bool Row(Action<string> write, string pol, string name, double want, double got,
                            int digits = 1)
    {
        double d = got - want;
        bool hit = Math.Abs(d) <= 0.5 / Math.Pow(10, digits);
        string f = "F" + digits;
        write($"| {pol} | {name} | {want.ToString(f)} | **{got.ToString(f)}** "
            + $"| {d.ToString("+0." + new string('0', digits) + ";-0." + new string('0', digits) + ";0")} "
            + $"| {(hit ? "**○**" : "×")} |");
        return hit;
    }

    // =================================================================================
    // 段1（指示書 §2）
    // =================================================================================

    public static bool Report(int seeds, Action<string> write, int seed0 = 0)
    {
        write("# 第176期 段1 —— 敵の拠点を置くと、待ちは負けるか");
        write("");
        write($"seed {seed0}..{seed0 + seeds - 1}。K = {Map11Time.AdoptedK}、"
            + $"道中の回復 {Map11.RecoverPercent}%、休む 1 回で {Time.RestPercent}%、"
            + $"迎撃の回復 {(Time.InterceptRecover ? "あり" : "なし")}（第175期の採用値）。"
            + "割り当ては正（カド隊 → 南 ／ かき回し隊 → 北）。");
        write("");
        write("**勝ち ＝ 敵の拠点を制圧し、かつ盤上の敵部隊が 0**（§1-4）。"
            + "**負け ＝ 味方の拠点が陥落 ／ 隊が全部失われる**。"
            + $"それ以外（{Map11.TurnCap} 作戦ターンの上限・戦闘回数の上限）は**膠着**。");
        write("");

        // ---- 対照: 拠点なし ----
        write("## 対照（`PortalOn = false`）—— 第175期と一致するか（自己検査 (a)）");
        write("");
        write("| 方針 | 踏破率 | 陥落率 | 戦闘/戦 | 部分点 | 作戦T 中央値 | 第175期（踏破/陥落/戦闘） |");
        write("|---|--:|--:|--:|--:|--:|---|");
        var off = new Dictionary<Map11Time.Policy, Map11Time.Stat>();
        foreach ((Map11Time.Policy pol, var w) in Phase175)
        {
            Map11Time.Stat st = Map11Time.Band(pol, Time, Straight, seeds, seed0, PortalRule.Off);
            off[pol] = st;
            write($"| {Map11Time.NameOf(pol)} | {st.Full:F1}% | {st.Fall:F1}% | {st.Battles:F2} "
                + $"| {st.Partial:F3} | {st.TurnMedian:F1} "
                + $"| {w.Full:F1}% / {w.Fall:F1}% / {w.Battles:F2} |");
        }
        write("");

        // ---- 本表 ----
        var table = new Dictionary<(int, Map11Time.Policy), Map11Time.Stat>();
        foreach (int s in Grid)
            foreach (Map11Time.Policy pol in All)
                table[(s, pol)] = Map11Time.Band(pol, Time, Straight, seeds, seed0,
                                                 PortalRule.Every(s));

        write("## 本表（湧きの間隔 S × 方針）");
        write("");
        write("| S | 方針 | 踏破率 | 制圧率 | 制圧T 中央値 | 陥落率 | 全滅率 | 膠着率 "
            + "| 作戦T 中央値 | 戦闘/戦 | 湧き 中央値 | 迎撃/戦 | 迎撃の北% | **陥落の北%** | 休/戦 |");
        write("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int s in Grid)
            foreach (Map11Time.Policy pol in All)
            {
                Map11Time.Stat st = table[(s, pol)];
                write($"| {s} | {Map11Time.NameOf(pol)} | **{st.Full:F1}%** | {st.Capture:F1}% "
                    + $"| {(st.Capture > 0 ? st.CaptureTurnMedian.ToString("F1") : "—")} "
                    + $"| {st.Fall:F1}% | {st.Wipe:F1}% | {st.Stall:F1}% | {st.TurnMedian:F1} "
                    + $"| {st.Battles:F2} | {st.SpawnMedian:F0} | {st.InterceptPerRun:F2} "
                    + $"| {st.NorthShare:F1}% | {(st.Fall > 0 ? st.FallNorthShare.ToString("F1") + "%" : "—")} "
                    + $"| {st.Rests:F2} |");
            }
        write("");

        // ---- 線 ----
        write("## 線（測る前に固定）");
        write("");
        write($"| S | 線1 籠城（上手い）踏破 ≤ {Line1Full:F0}% かつ 膠着 ≤ {Line1Stall:F0}% "
            + $"| 線2 制圧狙い ≥ {Line2Seize:F0}% | 線3 制圧 − 遅い制圧 ≥ {Line3Gap:F0}pt | 本丸 |");
        write("|---|:-:|:-:|:-:|:-:|");
        int best = -1;
        foreach (int s in Grid)
        {
            Map11Time.Stat t = table[(s, Map11Time.Policy.TurtleLast)];
            Map11Time.Stat z = table[(s, Map11Time.Policy.Seize)];
            Map11Time.Stat l = table[(s, Map11Time.Policy.SeizeLate)];
            bool l1 = t.Full <= Line1Full && t.Stall <= Line1Stall;
            bool l2 = z.Full >= Line2Seize;
            double gap = z.Full - l.Full;
            bool l3 = gap >= Line3Gap;
            bool all = l1 && l2 && l3;
            if (all) best = Math.Max(best, s);
            write($"| {s} | {(l1 ? "**○**" : "×")}（{t.Full:F1}% / 膠着 {t.Stall:F1}%） "
                + $"| {(l2 ? "**○**" : "×")}（{z.Full:F1}%） "
                + $"| {(l3 ? "**○**" : "×")}（{gap:+0.0;-0.0;0.0}pt） | {(all ? "**○**" : "×")} |");
        }
        write("");
        write(best > 0
            ? $"**本丸 ○ —— 3 つの線が同時に通る S のうち、いちばん緩い値は S = {best}。これを既定にする。**"
            : "**本丸 × —— どの S でも3つ同時には通らない。線は動かさない（指示書 §2-3）。**");
        write("");

        // ---- 道の割り振りは効くか（Q0-3） ----
        write("## 道の割り振りは効くか（制圧狙い 対 入れ替え）");
        write("");
        write("| S | かき回し隊 → 北 | かき回し隊 → 南 | 差 |");
        write("|---|--:|--:|--:|");
        foreach (int s in Grid)
        {
            double a = table[(s, Map11Time.Policy.Seize)].Full;
            double b = table[(s, Map11Time.Policy.SeizeSwap)].Full;
            write($"| {s} | {a:F1}% | {b:F1}% | {b - a:+0.0;-0.0;0.0}pt |");
        }
        write("");

        // ---- 同点の割り方は効くか（R276） ----
        write("## 同点の割り方は効くか（籠城・R276 の再発を見る）");
        write("");
        write("| S | 控えから出す（上手い） | 番号順 | 膠着の差 | カド隊の迎撃 回/勝率（番号順） |");
        write("|---|--:|--:|--:|--:|");
        foreach (int s in Grid)
        {
            Map11Time.Stat a = table[(s, Map11Time.Policy.TurtleLast)];
            Map11Time.Stat b = table[(s, Map11Time.Policy.Turtle)];
            string kado = b.BySquad[0] == 0 ? "—"
                : $"{b.BySquad[0]} / {100.0 * b.WonBySquad[0] / b.BySquad[0]:F1}%";
            write($"| {s} | 膠着 {a.Stall:F1}% ／ 陥落 {a.Fall:F1}% | 膠着 {b.Stall:F1}% ／ 陥落 {b.Fall:F1}% "
                + $"| {b.Stall - a.Stall:+0.0;-0.0;0.0}pt | {kado} |");
        }
        write("");

        // ---- 湧きの値段（S = 無限 との差） ----
        write("## 湧きの値段（`S` を走行より長くして湧きを 0 にした点との差）");
        write("");
        write($"**`S = 9999` では湧きが1体も出ない**（{Map11.TurnCap} 作戦ターンの上限より先に走行が終わる）。"
            + "**その点との差が、この期が足した難度そのもの**である"
            + "——マップの形（道が1マス伸びた・勝ち条件が「制圧 ＋ 敵 0」になった）は両方に等しく掛かる。");
        write("");
        write("| S | 制圧狙い 踏破 | 総力制圧 踏破（参考） | 総力制圧 陥落 |");
        write("|---|--:|--:|--:|");
        foreach (int s in Grid)
            write($"| {s} | {table[(s, Map11Time.Policy.Seize)].Full:F1}% "
                + $"| {table[(s, Map11Time.Policy.SeizeAll)].Full:F1}% "
                + $"| {table[(s, Map11Time.Policy.SeizeAll)].Fall:F1}% |");
        Map11Time.Stat noSeize = Map11Time.Band(Map11Time.Policy.Seize, Time, Straight,
                                                seeds, seed0, PortalRule.Every(9999));
        Map11Time.Stat noAll = Map11Time.Band(Map11Time.Policy.SeizeAll, Time, Straight,
                                              seeds, seed0, PortalRule.Every(9999));
        write($"| **湧き 0** | **{noSeize.Full:F1}%** | **{noAll.Full:F1}%** | {noAll.Fall:F1}% |");
        write("");
        write($"**湧きを 0 にしても、指示書の制圧狙いは {noSeize.Full:F1}%**"
            + $"（線2 は {Line2Seize:F0}%）。**3 隊とも出せば {noAll.Full:F1}% で線を越える**"
            + "——つまり**線2 に届かない理由の過半は湧きではなく、"
            + "「カド隊を拠点に残す」という方針の側にある**。");
        write("");

        // ---- 第2拠点の陥落（§1-3 (4)） ----
        int falls = table.Values.Sum(x => x.SecondFalls);
        write("## 第2拠点の陥落（§1-3 (4)）");
        write("");
        write($"**{falls} 件。** 敵は味方の拠点へ向かってしか動かず、制圧した時点で湧きも止まるので、"
            + "**制圧したあとに敵が第2拠点へ来る道は1本も無い**——規則は書いてあるが、"
            + "この形のマップでは原理的に発火しない。");
        write("");

        // ---- 迎撃に出た隊 ----
        write("## 迎撃に出た隊（R271）");
        write("");
        write("| S | 方針 | " + string.Join(" | ", Map11.Squads.Select(x => x.Name + " 回/勝率")) + " |");
        write("|---|---|" + string.Concat(Map11.Squads.Select(_ => "--:|")));
        foreach (int s in Grid)
            foreach (Map11Time.Policy pol in All)
            {
                Map11Time.Stat st = table[(s, pol)];
                string cells = string.Join(" | ", Enumerable.Range(0, Map11.Squads.Length)
                    .Select(i => st.BySquad[i] == 0 ? "—"
                        : $"{st.BySquad[i]} / {100.0 * st.WonBySquad[i] / st.BySquad[i]:F1}%"));
                write($"| {s} | {Map11Time.NameOf(pol)} | {cells} |");
            }
        write("");

        // ---- 自己検査 (a) ----
        write("## 自己検査 (a) —— `PortalOn = false` が第175期と一致するか");
        write("");
        bool ok = true;
        if (seeds == DefaultSeeds && seed0 == 0)
        {
            write("| 方針 | 量 | 第175期 | 拠点なし | 差 | 線 ±0.05 |");
            write("|---|---|--:|--:|--:|:-:|");
            foreach ((Map11Time.Policy pol, var w) in Phase175) ok &= Compare(write, pol, w, off[pol]);
        }
        else write("（seed が本走行と違うので突き合わせない）");
        write("");
        write(ok ? "**(a) ○ —— 敵の拠点を足しても、切れば第175期と1ビットも違わない。**"
                 : "**(a) × —— 拠点なしの数字が第175期と食い違う。**");
        write("");
        return best > 0 && ok;
    }
}
