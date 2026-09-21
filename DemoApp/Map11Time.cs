using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// =====================================================================================
// 第174期 段B1 / 第175期 §3 —— **時間を器具で当てる（UI の前）。**
//
// **遊ぶ側と同じ `Map11State` を頭なしで回す**（R259）。ここが持つのは
// 「自動で打つ手」＝方針だけで、**規則は1行も持たない**
// ——進む・戻る・休む・敵の前進・迎撃・陥落は全部 `Map11State` にある。
//
//     Godot_console.exe --path DemoApp --headless -- --map11-time[=seeds[,起点]]
//
// **第175期に方針の書き方を変えた**——4 方針とも「行き先を1つ選ぶ」だけになり、
// そこから1ターンの命令を作るのは `Map11Orders`（<b>遊ぶ側と同じ1本</b>）。
// **盤面の規則は1つも通っていない**ので、貪欲・全快は第174期と1ビットも違わない
// （自己検査 (b) が実測で確かめる）。
//
// 方針は4つ:
//   貪欲    休まない。担当の道の奥まで進み続ける
//   全快    傷が残っていれば行き先を拠点にする（戻って休む）。全快したらまた出る
//   籠城    行き先はずっと拠点。**1 マスも動かない**（拠点で傷が残っていれば休む）
//   即出し  **1 作戦ターン目から3隊とも出す**（第174期の観察ログ——ポンの打ち方は
//           器具の2方針のどちらでもなく、これに近い）
// =====================================================================================

public static class Map11Time
{
    public const int DefaultSeeds = 800;

    /// <summary>
    /// 採用した K（敵が1マス前進する間隔）。<b>第174期に測って決めた値で、この期では振らない</b>
    /// ——§2 で変えたのは迎撃の回復だけなので、K の当落は第174期の表がそのまま使える。
    /// </summary>
    public const int AdoptedK = 1;

    /// <summary>
    /// 自動で打つ手。<b>籠城は第174期の観察ログから、即出しは第175期 §3 から足した。</b>
    /// 籠城はポンの3プレイ目（拠点から一歩も出ず迎撃だけで殲滅）、
    /// 即出しは1・2プレイ目（1ターン目から3隊とも出す）を写したものである。
    /// </summary>
    public enum Policy
    {
        Greedy, Full, Turtle, TurtleLast, Rush, RushSplit,
        Seize, SeizeSwap, SeizeLate, SeizeAll,
    }

    /// <summary>「遅い制圧」が拠点で待つ作戦ターン数（第176期 §2-1 の対照）。</summary>
    public const int SeizeDelay = 6;

    public static string NameOf(Policy p) => p switch
    {
        Policy.Greedy => "貪欲",
        Policy.Full => "全快",
        Policy.Turtle => "籠城",
        Policy.TurtleLast => "籠城（控えから出す）",
        Policy.Rush => "即出し",
        Policy.RushSplit => "即出し（控えは南）",
        Policy.Seize => "制圧狙い",
        Policy.SeizeSwap => "制圧狙い（道を入れ替え）",
        Policy.SeizeLate => $"遅い制圧（{SeizeDelay}T 待つ）",
        Policy.SeizeAll => "総力制圧（3 隊とも出す・参考）",
        _ => "?",
    };

    /// <summary>敵の拠点を狙う方針か（第176期）。</summary>
    public static bool IsSeize(Policy p)
        => p is Policy.Seize or Policy.SeizeSwap or Policy.SeizeLate or Policy.SeizeAll;

    /// <summary>第174期の実測（K = 1・迎撃の回復あり）。<b>自己検査 (b) の相手。</b></summary>
    public static readonly (double Full, double Intercept, double Fall, double Turn) Phase174Greedy
        = (94.0, 44.8, 6.0, 3.0);
    public static readonly (double Full, double Intercept, double Fall, double Turn) Phase174Full
        = (88.0, 91.2, 12.0, 4.0);
    public const double Phase174Tolerance = 0.1;

    private readonly record struct Once(
        bool Full, bool Fallen, bool Intercepted, int Turns, int Battles, double Partial,
        int InterceptNorth, int InterceptSouth, int InterceptWins, int Rests,
        int[] BySquad, int[] WonBySquad,
        bool Captured, int CaptureTurn, int Spawns, bool Wiped, bool Stalled, int SecondFalls,
        int FallenRoad);

    public sealed record Stat(
        double Full, double Intercept, double Fall, double TurnMedian,
        double Battles, double Partial, double InterceptPerRun,
        double NorthShare, double InterceptWin, double Rests,
        int[] BySquad, int[] WonBySquad,
        double Capture, double CaptureTurnMedian, double SpawnMedian,
        double Wipe, double Stall, int SecondFalls, double FallNorthShare);

    // =================================================================================
    // 1 マップぶん（方針が打つ手）
    // =================================================================================

    private sealed class Run
    {
        public Map11State St = null!;
        public Policy Pol;
        public int[] Home = null!;
        public bool ReserveSent;
        public int IntN, IntS, IntWins;
        /// <summary>迎撃に出た回数と勝った回数（隊ごと・予測 W3 のため）。</summary>
        public readonly int[] IntBy = new int[Map11.Squads.Length];
        public readonly int[] IntWonBy = new int[Map11.Squads.Length];

        /// <summary>1 戦。<b>戦闘・回復・勝敗の規則は `Map11State` の `Resolve` に任せる。</b></summary>
        public void Fight(int squad, Map11State.Node node, bool intercept)
        {
            var prep = intercept ? St.PrepareIntercept(squad, node) : St.Prepare(squad, node);
            if (prep is not { } p) return;
            if (intercept) { if (node.Def.Road == 0) IntN++; else IntS++; IntBy[squad]++; }
            BattleResult r = BattleEngine.Run(p.Players, p.Enemies, p.Seed, verbose: false);
            // 第175期 §2: **迎撃で勝っても道中の回復は乗らない**（`TimeRule.InterceptRecover`）。
            St.Resolve(squad, p.Node, r.PlayerWon, intercept);
            if (intercept && r.PlayerWon) { IntWins++; IntWonBy[squad]++; }

            // 控えは第169期と同じ自動規則——**隊が全滅した道へ1回だけ**。
            if (St.Squads[squad].Lost && !ReserveSent
                && St.Squads[^1].Units is null && !St.Squads[^1].Deployed)
            {
                ReserveSent = true;
                Home[^1] = node.Def.Road;
            }
        }

        /// <summary>
        /// 拠点の迎撃。<b>抜くか、出せる隊が尽きるまで</b>。
        /// <b>籠城だけ選び方を人に寄せる</b>（第175期 §3）——「まだ戦っていない隊」→「HP% の高い隊」。
        /// R271（拠点を守れるのは<b>まだ出していない隊</b>）をそのまま手にした形で、
        /// <b>貪欲・全快・即出しは第174期のまま番号順</b>（自己検査 (b) を厳密に保つため）。
        /// </summary>
        public void Intercept(Map11State.Node node)
        {
            while (!St.Finished)
            {
                int j = Pol is Policy.Greedy or Policy.Full or Policy.Rush or Policy.RushSplit
                    ? St.NextInterceptor(node) : PickInterceptor(node);
                if (j < 0) break;
                Fight(j, node, intercept: true);
                if (node.Cleared) break;
            }
            St.CloseIntercept(node);
        }

        /// <summary>
        /// 迎撃に出す隊を「まだ戦っていない → HP% の高い」の順で選ぶ（籠城だけ）。
        ///
        /// <para><b>第175期の観察ログから <c>TurtleLast</c> を足した</b>——最初の到着では3隊とも
        /// 未出撃・HP 100% で<b>完全に同点</b>になるので、実際に効くのは<b>同点の割り方</b>だけである。
        /// <c>Turtle</c> は隊の番号順（＝カド隊が最初の到着を受ける）、
        /// <c>TurtleLast</c> は逆順（＝控え隊が受ける）。
        /// <b>ポンの1巡目はカド隊を北の粛へ出して失い、3巡目は控え隊に受けさせて通した。</b></para>
        /// </summary>
        private int PickInterceptor(Map11State.Node node)
        {
            int best = -1;
            double bestKey = double.NegativeInfinity;
            bool last = Pol == Policy.TurtleLast;
            for (int k = 0; k < St.Squads.Length; k++)
            {
                int i = last ? St.Squads.Length - 1 - k : k;
                Map11State.Squad s = St.Squads[i];
                if (node.Tried.Contains(i) || s.Lost || s.Cell >= 0 || !St.CanSend(i)) continue;
                double hp = 100.0;
                if (s.Units is { } u)
                {
                    int max = u.Sum(x => x.MaxHp);
                    hp = max == 0 ? 0 : 100.0 * u.Where(x => x.IsAlive).Sum(x => x.Hp) / max;
                }
                double key = (s.Deployed ? 0 : 1000) + hp;
                if (key > bestKey) { bestKey = key; best = i; }
            }
            return best;
        }

        /// <summary>その隊が向かう道（担当の道が抜け切っていれば、残っている道）。</summary>
        public int RoadFor(int i)
        {
            int want = Home[i];
            if (want >= 0 && St.NextNode(want) is not null) return want;
            for (int r = 0; r < Map11.RoadCount; r++)
                if (St.NextNode(r) is not null) return r;
            return -1;
        }

        /// <summary>
        /// その隊の<b>行き先</b>。<b>方針が持つのはこれだけ</b>（第175期 §1-1）——
        /// 1 ターンの命令はここから <see cref="Map11Orders.Plan"/> が作る。
        /// </summary>
        public Dest DestFor(int i)
        {
            if (St.Squads[i].Lost || Home[i] < 0) return Dest.Home;
            // 籠城: **1 マスも動かない。** 戦うのは拠点へ来た敵だけ（拠点にいるので傷は休んで戻す）。
            if (Pol is Policy.Turtle or Policy.TurtleLast) return Dest.Home;
            // 制圧狙い（第176期 §2-1）: **カド隊は拠点に残し**、残り2隊を敵の拠点へ向ける。
            if (IsSeize(Pol))
            {
                // **カド隊は拠点に残る**——`SeizeAll`（参考）だけは残さない。
                if (i == 0 && Pol != Policy.SeizeAll) return Dest.Home;
                // 遅い制圧: 最初の `SeizeDelay` 作戦ターンは拠点で休んで待つ（線3 の対照）。
                if (Pol == Policy.SeizeLate && St.Turn < SeizeDelay) return Dest.Home;
                // 制圧した後——傷が残っていれば第2拠点で休み、満タンなら拠点へ戻って掃除する
                // （勝ち条件は「制圧 ＋ 盤上の敵が 0」なので、残りを片付ける必要がある）。
                if (St.Captured)
                    return St.AtSecondBase(i) && St.Hurt(i) ? Dest.Portal(St.Squads[i].Road) : Dest.Home;
                return Dest.Portal(Home[i]);
            }
            // 全快: 傷が残っていれば行き先を拠点にする（戻って休む）。全快したらまた出る。
            if (Pol == Policy.Full && St.Hurt(i)) return Dest.Home;
            int road = RoadFor(i);
            return road < 0 ? Dest.Home : Dest.Deep(road);
        }

        /// <summary>
        /// 1 隊ぶんの手。<b>遊ぶ側とまったく同じ層</b>（`Map11Orders`）を通る——
        /// 器具が持つのは行き先の選び方だけで、規則は1行も持たない（R259）。
        /// </summary>
        public void Act(int i)
        {
            Dest dest = DestFor(i);
            Map11Orders.Order order = Map11Orders.Plan(St, i, dest);
            if (Map11Orders.Apply(St, i, dest, order) is { } node) Fight(i, node, intercept: false);
        }
    }

    private static Once RunOne(Policy pol, TimeRule time, int[] assign, int seed,
                               PortalRule? portal = null)
    {
        var run = new Run
        {
            St = new Map11State(seed, time, portal),
            Pol = pol,
            // 即出し（第175期 §3）: **1 作戦ターン目から3隊とも出す**。
            // 控え隊はかき回し隊と同じ道（北）へ——ポンの1・2プレイ目がその形だった。
            // 参考の `RushSplit` は控えを**もう一方の道**へ出す——即出しが負ける原因を
            // 「拠点を空にしたこと」と「2 隊を同じ道へ重ねたこと」に割るためだけの点（線には使わない）。
            // 制圧狙い（第176期）: **控え隊も1 作戦ターン目から出す**——カド隊が拠点に残るので、
            // 道へ出せるのは残り2隊しかいない。道の割り振りは `Seize` と `SeizeSwap` で入れ替える。
            Home = new[]
            {
                assign[0],
                pol == Policy.SeizeSwap ? assign[0] : assign[1],
                pol == Policy.Rush ? assign[1] : pol == Policy.RushSplit ? assign[0]
                    : pol is Policy.Seize or Policy.SeizeLate ? assign[0]
                    : pol == Policy.SeizeSwap ? assign[1] : -1,
            },
            ReserveSent = pol is Policy.Rush or Policy.RushSplit || IsSeize(pol),
        };
        Map11State st = run.St;

        while (!st.Finished)
        {
            for (int i = 0; i < st.Squads.Length && !st.Finished; i++) run.Act(i);
            if (st.Finished) break;
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (st.Finished) break;
                if (e.Intercept) run.Intercept(e.Node);
                else run.Fight(e.Squad, e.Node, intercept: false);
            }
        }

        // 膠着 ＝ 決着しなかった（作戦ターンの上限・戦闘回数の上限）。**全滅と陥落は負けで、膠着ではない。**
        bool wiped = !st.Won && !st.Fallen && st.NoSquadsLeft;
        bool stalled = !st.Won && !st.Fallen && !wiped;
        return new Once(st.Won, st.Fallen, st.Intercepts > 0, st.Turn, st.Battles, st.Partial(),
                        run.IntN, run.IntS, run.IntWins, st.Rests, run.IntBy, run.IntWonBy,
                        st.Captured, st.CapturedTurn, st.SpawnCount, wiped, stalled, st.SecondBaseFalls,
                        st.FallenRoad);
    }

    private static double Median(IEnumerable<double> xs)
    {
        double[] a = xs.OrderBy(x => x).ToArray();
        if (a.Length == 0) return 0;
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2.0;
    }

    public static Stat Band(Policy pol, TimeRule time, int[] assign, int seeds, int seed0 = 0,
                            PortalRule? portal = null)
    {
        var o = new Once[seeds];
        Parallel.For(0, seeds, i => o[i] = RunOne(pol, time, assign, seed0 + i, portal));
        var turns = o.Select(x => (double)x.Turns).OrderBy(x => x).ToArray();
        double med = turns.Length == 0 ? 0
            : turns.Length % 2 == 1 ? turns[turns.Length / 2]
            : (turns[turns.Length / 2 - 1] + turns[turns.Length / 2]) / 2.0;
        int intN = o.Sum(x => x.InterceptNorth), intS = o.Sum(x => x.InterceptSouth);
        int intAll = intN + intS;
        return new Stat(
            100.0 * o.Count(x => x.Full) / seeds,
            100.0 * o.Count(x => x.Intercepted) / seeds,
            100.0 * o.Count(x => x.Fallen) / seeds,
            med,
            o.Average(x => (double)x.Battles),
            o.Average(x => x.Partial),
            (double)intAll / seeds,
            intAll == 0 ? 0 : 100.0 * intN / intAll,
            intAll == 0 ? 0 : 100.0 * o.Sum(x => x.InterceptWins) / intAll,
            o.Average(x => (double)x.Rests),
            Enumerable.Range(0, Map11.Squads.Length).Select(s => o.Sum(x => x.BySquad[s])).ToArray(),
            Enumerable.Range(0, Map11.Squads.Length).Select(s => o.Sum(x => x.WonBySquad[s])).ToArray(),
            100.0 * o.Count(x => x.Captured) / seeds,
            Median(o.Where(x => x.Captured).Select(x => (double)x.CaptureTurn)),
            Median(o.Select(x => (double)x.Spawns)),
            100.0 * o.Count(x => x.Wiped) / seeds,
            100.0 * o.Count(x => x.Stalled) / seeds,
            o.Sum(x => x.SecondFalls),
            o.Count(x => x.Fallen) == 0 ? 0
                : 100.0 * o.Count(x => x.Fallen && x.FallenRoad == 0) / o.Count(x => x.Fallen));
    }
    // =================================================================================
    // 表（第175期 §3）
    //
    // **K は振らない**（採用値 1）。振るのは **方針 4 つ × 迎撃の回復 あり/なし** の 8 点。
    // =================================================================================

    /// <summary>線（測る前に固定・指示書 §3）。</summary>
    public const double Line1Turtle = 10.0;   // 迎撃の回復なしで、籠城の踏破率 <= 10%
    public const double Line2Rush = 85.0;     // 迎撃の回復なしで、即出しの踏破率 >= 85%

    private static readonly Policy[] All =
        { Policy.Greedy, Policy.Full, Policy.Turtle, Policy.TurtleLast,
          Policy.Rush, Policy.RushSplit };

    public static bool Report(int seeds, Action<string> write, int seed0 = 0)
    {
        int[] straight = { 1, 0 };   // 正: カド隊 -> 南(1) ／ かき回し隊 -> 北(0)

        write("# 第175期 §3 —— 待ちの抜け道を塞いだか");
        write("");
        write($"seed {seed0}..{seed0 + seeds - 1}。K = {AdoptedK}（第174期の採用値・この期では振らない）。"
            + $"道中の回復 {Map11.RecoverPercent}%、休む 1 回で {TimeRule.Every(AdoptedK).RestPercent}%。"
            + "割り当ては正（カド隊 → 南 ／ かき回し隊 → 北）。");
        write("");
        write("**この期で変えた規則は1つだけ**——`InterceptRecover`"
            + "（迎撃戦に勝った隊に道中の回復を乗せるか）。**`true` が第174期の姿で、既定は `false`。**");
        write("");

        // ---- 対照: 時間なし ----
        Map11Verify.Stat off = Map11Verify.Band(straight, seed0, seeds);
        write("## 対照（`TimeOn = false`）");
        write("");
        write($"踏破率 **{off.Full:F1}%**（第169期 {Map11Verify.ExpectStraight:F1}%・"
            + $"差 {off.Full - Map11Verify.ExpectStraight:+0.0;-0.0;0.0}pt）／ 部分点 {off.Partial:F3}。"
            + "**時間なしでは迎撃が原理的に起きない**ので、`InterceptRecover` は1ビットも効かない。");
        write("");

        // ---- 本表 ----
        var table = new Dictionary<(bool, Policy), Stat>();
        foreach (bool rec in new[] { true, false })
            foreach (Policy pol in All)
                table[(rec, pol)] = Band(pol, TimeRule.Every(AdoptedK, interceptRecover: rec),
                                         straight, seeds, seed0);

        write("## 本表（迎撃の回復 × 方針）");
        write("");
        write("| 迎撃の回復 | 方針 | 踏破率 | 迎撃が起きた率 | 陥落率 | 作戦T 中央値 | 戦闘/戦 | 部分点 | 迎撃/戦 | 迎撃の北% | 迎撃の勝率 | 休/戦 |");
        write("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (bool rec in new[] { true, false })
            foreach (Policy pol in All)
            {
                Stat st = table[(rec, pol)];
                write($"| {(rec ? "あり（第174期）" : "**なし（採用）**")} | {NameOf(pol)} "
                    + $"| {st.Full:F1}% | {st.Intercept:F1}% | {st.Fall:F1}% "
                    + $"| {st.TurnMedian:F1} | {st.Battles:F2} | {st.Partial:F3} | {st.InterceptPerRun:F2} "
                    + $"| {st.NorthShare:F1}% | {st.InterceptWin:F1}% | {st.Rests:F2} |");
            }
        write("");

        // ---- 迎撃の回復を外したぶん ----
        write("## 迎撃の回復を外したぶん（なし − あり）");
        write("");
        write("| 方針 | 踏破率 | 陥落率 | 戦闘/戦 | 部分点 |");
        write("|---|--:|--:|--:|--:|");
        foreach (Policy pol in All)
        {
            Stat a = table[(true, pol)], b = table[(false, pol)];
            write($"| {NameOf(pol)} | {b.Full - a.Full:+0.0;-0.0;0.0}pt | {b.Fall - a.Fall:+0.0;-0.0;0.0}pt "
                + $"| {b.Battles - a.Battles:+0.00;-0.00;0.00} | {b.Partial - a.Partial:+0.000;-0.000;0.000} |");
        }
        write("");

        // ---- 線 ----
        Stat turtle = table[(false, Policy.Turtle)], rush = table[(false, Policy.Rush)];
        bool line1 = turtle.Full <= Line1Turtle;
        bool line2 = rush.Full >= Line2Rush;
        write("## 線（測る前に固定）");
        write("");
        write("| # | 線 | 実測 | 合否 |");
        write("|---|---|--:|:-:|");
        write($"| 線1 | 迎撃の回復なしで、籠城の踏破率 ≤ {Line1Turtle:F0}% | **{turtle.Full:F1}%** "
            + $"| {(line1 ? "**○**" : "×")} |");
        write($"| 線2 | 迎撃の回復なしで、即出しの踏破率 ≥ {Line2Rush:F0}% | **{rush.Full:F1}%** "
            + $"| {(line2 ? "**○**" : "×")} |");
        write("");
        if (!line1)
            write("**線1 が × —— まだ効いている待ちの理由を下の表で読む**"
                + "（「1部隊ずつ来る」を外すのは次の期の判断）。");
        if (line2 && rush.Full >= 98.0)
            write($"**即出しが {rush.Full:F1}% と高すぎる（≥ 98%）。報告だけする**"
                + "——難度は第176期の「配られた駒から組む」で上げる。");
        if (!line2)
        {
            Stat greedy = table[(false, Policy.Greedy)], split = table[(false, Policy.RushSplit)];
            write($"**線2 が × —— しかも即出しは4方針で最も高くない**"
                + $"（貪欲 {greedy.Full:F1}% > 即出し {rush.Full:F1}%）。**指示書 §3 の想定と逆である。**");
            write("");
            write("| 何が違うか | 踏破率 | 陥落率 | 読み |");
            write("|---|--:|--:|---|");
            write($"| 貪欲（控えは拠点に残る） | {greedy.Full:F1}% | {greedy.Fall:F1}% | 拠点に隊がいる |");
            write($"| 即出し（控えも北へ） | {rush.Full:F1}% | {rush.Fall:F1}% "
                + $"| 拠点が空 ＋ 2 隊が同じ道 |");
            write($"| 参考 即出し（控えは南へ） | {split.Full:F1}% | {split.Fall:F1}% "
                + $"| 拠点が空・道は割れている |");
            write("");
            write($"**踏破率の落ち（{rush.Full - greedy.Full:+0.0;-0.0;0.0}pt）と"
                + $"陥落率の増え（{rush.Fall - greedy.Fall:+0.0;-0.0;0.0}pt）がほぼ同じ大きさ**"
                + "——即出しが失うのは**全部、拠点を空にした代金**である（R271）。");
        }
        write("");

        // ---- 待ちが得だった3つの理由 ----
        write("## 待ちが得だった3つの理由（線1 が × のとき読む表）");
        write("");
        write("| 理由 | この期で外したか | 実測 |");
        write("|---|:-:|---|");
        write($"| (b) 迎撃に勝っても道中の回復が乗る | **外した** "
            + $"| 籠城の踏破率 {table[(true, Policy.Turtle)].Full:F1}% → **{turtle.Full:F1}%** |");
        write($"| (a) 移動ですり減らない | 外していない "
            + $"| 籠城の戦闘数 {turtle.Battles:F2} 回（貪欲 {table[(false, Policy.Greedy)].Battles:F2} 回） |");
        write($"| (c) 相手を1部隊ずつ迎えられる | 外していない "
            + $"| 籠城の迎撃が起きた率 {turtle.Intercept:F1}% ／ 迎撃の勝率 {turtle.InterceptWin:F1}% |");
        write("");

        // ---- 迎撃に出たのは誰か（R271） ----
        write("## 迎撃に出た隊（R271: 拠点を守れるのは「まだ出していない隊」）");
        write("");
        write("| 迎撃の回復 | 方針 | " + string.Join(" | ", Map11.Squads.Select(x => x.Name + " 回/勝率")) + " |");
        write("|---|---|" + string.Concat(Map11.Squads.Select(_ => "--:|")));
        foreach (bool rec in new[] { true, false })
            foreach (Policy pol in All)
            {
                Stat st = table[(rec, pol)];
                string cells = string.Join(" | ", Enumerable.Range(0, Map11.Squads.Length)
                    .Select(i => st.BySquad[i] == 0 ? "—"
                        : $"{st.BySquad[i]} / {100.0 * st.WonBySquad[i] / st.BySquad[i]:F1}%"));
                write($"| {(rec ? "あり" : "なし")} | {NameOf(pol)} | {cells} |");
            }
        write("");

        // ---- 自己検査 (b) ----
        write("## 自己検査 (b) —— `InterceptRecover = true` が第174期と一致するか");
        write("");
        write("**第175期は方針の書き方も変えた**（4 方針とも `Map11Orders` を通す）ので、"
            + "**貪欲・全快が第174期と1ビットも違わないこと**が、その置き換えの検算になる。"
            + "**籠城は迎撃役の選び方と休みを人に寄せたので対照外**（§3）。");
        write("");
        write($"| 方針 | 量 | 第174期 | いま | 差 | 線 ±{Phase174Tolerance:F1} |");
        write("|---|---|--:|--:|--:|:-:|");
        bool ok = true;
        void Row(string pol, string name, double want, double got)
        {
            double d = got - want;
            bool hit = Math.Abs(d) <= Phase174Tolerance;
            ok &= hit;
            write($"| {pol} | {name} | {want:F1} | **{got:F1}** | {d:+0.0;-0.0;0.0} | {(hit ? "**○**" : "×")} |");
        }
        Stat g174 = table[(true, Policy.Greedy)], f174 = table[(true, Policy.Full)];
        Row("貪欲", "踏破率", Phase174Greedy.Full, g174.Full);
        Row("貪欲", "迎撃が起きた率", Phase174Greedy.Intercept, g174.Intercept);
        Row("貪欲", "陥落率", Phase174Greedy.Fall, g174.Fall);
        Row("貪欲", "作戦T 中央値", Phase174Greedy.Turn, g174.TurnMedian);
        Row("全快", "踏破率", Phase174Full.Full, f174.Full);
        Row("全快", "迎撃が起きた率", Phase174Full.Intercept, f174.Intercept);
        Row("全快", "陥落率", Phase174Full.Fall, f174.Fall);
        Row("全快", "作戦T 中央値", Phase174Full.Turn, f174.TurnMedian);
        write("");
        write(ok ? "**(b) ○ —— 迎撃の回復を戻すと第174期と一致する。行き先の層は盤面を1ビットも動かしていない。**"
                 : "**(b) × —— 第174期と食い違う量がある。**");
        write("");

        bool all = line1 && line2 && ok;
        write(all
            ? "**本丸 ○ —— 線1・線2 が通り、対照も一致した。**"
            : "**本丸 × —— 上の × を読むこと。**");
        return all;
    }
}
