using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// =====================================================================================
// 第174期 段B1 —— **時間を器具で先に当てる（UI の前）。**
//
// **遊ぶ側と同じ `Map11State` を頭なしで回す**（R259）。ここが持つのは
// 「自動で打つ手」＝方針だけで、**規則は1行も持たない**
// ——進む・戻る・休む・敵の前進・迎撃・陥落は全部 `Map11State` にある。
//
//     Godot_console.exe --path DemoApp --headless -- --map11-time[=seeds]
//
// 方針は2つ（指示書 §2-2）:
//   貪欲  休まない。正の割り当てで進み続ける
//   全快  戦う前に、生存者の HP が 100% でなければ拠点へ戻って休む。休み切ってから出る
// =====================================================================================

public static class Map11Time
{
    public const int DefaultSeeds = 800;

    /// <summary>掃引する K（敵が1マス前進する間隔）。</summary>
    public static readonly int[] Ks = { 1, 2, 3, 4 };

    /// <summary>
    /// 自動で打つ手。<b>籠城は第174期の観察ログから足した</b>——ポンの3プレイ目が
    /// 「<b>拠点から一歩も出ず、迎撃だけで殲滅した</b>」だったので、
    /// <b>それが実際に何%通るのか</b>を数えるために作った方針である（判定には使わない）。
    /// </summary>
    public enum Policy { Greedy, Full, Turtle }

    public static string NameOf(Policy p) => p switch
    {
        Policy.Greedy => "貪欲",
        Policy.Full => "全快",
        _ => "籠城",
    };

    private readonly record struct Once(
        bool Full, bool Fallen, bool Intercepted, int Turns, int Battles, double Partial,
        int InterceptNorth, int InterceptSouth, int InterceptWins, int Rests,
        int[] BySquad, int[] WonBySquad);

    public sealed record Stat(
        double Full, double Intercept, double Fall, double TurnMedian,
        double Battles, double Partial, double InterceptPerRun,
        double NorthShare, double InterceptWin, double Rests,
        int[] BySquad, int[] WonBySquad);

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
            var prep = intercept ? St.PrepareIntercept(squad, node) : St.Prepare(squad);
            if (prep is not { } p) return;
            if (intercept) { if (node.Def.Road == 0) IntN++; else IntS++; IntBy[squad]++; }
            BattleResult r = BattleEngine.Run(p.Players, p.Enemies, p.Seed, verbose: false);
            St.Resolve(squad, p.Node, r.PlayerWon);
            if (intercept && r.PlayerWon) { IntWins++; IntWonBy[squad]++; }

            // 控えは第169期と同じ自動規則——**隊が全滅した道へ1回だけ**。
            if (St.Squads[squad].Lost && !ReserveSent
                && St.Squads[^1].Units is null && !St.Squads[^1].Deployed)
            {
                ReserveSent = true;
                Home[^1] = node.Def.Road;
            }
        }

        /// <summary>拠点の迎撃。<b>拠点の隊を番号順に出し、抜くか出せる隊が尽きるまで。</b></summary>
        public void Intercept(Map11State.Node node)
        {
            while (!St.Finished)
            {
                int j = St.NextInterceptor(node);
                if (j < 0) break;
                Fight(j, node, intercept: true);
                if (node.Cleared) break;
            }
            St.CloseIntercept(node);
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

        /// <summary>1 隊ぶんの手。<b>ここが方針の全部</b>——規則はすべて `Map11State` が持つ。</summary>
        public void Act(int i)
        {
            Map11State.Squad s = St.Squads[i];
            if (s.Lost || Home[i] < 0) return;
            // 籠城: **1 マスも動かない。** 戦うのは拠点へ来た敵だけ。
            if (Pol == Policy.Turtle) return;

            // 全快: 傷が残っていれば拠点へ戻って休む。休み切ってから出る。
            if (Pol == Policy.Full && St.Hurt(i))
            {
                if (!St.AtHome(i)) { St.StepBack(i); return; }
                if (St.Rest(i)) return;
            }

            int road = RoadFor(i);
            if (road < 0) return;
            // 担当の道が抜け切って別の道へ回るには、いったん拠点まで戻る（一瞬では移れない）。
            if (!St.AtHome(i) && s.Road != road) { St.StepBack(i); return; }

            if (St.Advance(i, road) is { } node) Fight(i, node, intercept: false);
        }
    }

    private static Once RunOne(Policy pol, TimeRule time, int[] assign, int seed)
    {
        var run = new Run
        {
            St = new Map11State(seed, time),
            Pol = pol,
            Home = new[] { assign[0], assign[1], -1 },
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

        return new Once(st.Won, st.Fallen, st.Intercepts > 0, st.Turn, st.Battles, st.Partial(),
                        run.IntN, run.IntS, run.IntWins, st.Rests, run.IntBy, run.IntWonBy);
    }

    public static Stat Band(Policy pol, TimeRule time, int[] assign, int seeds, int seed0 = 0)
    {
        var o = new Once[seeds];
        Parallel.For(0, seeds, i => o[i] = RunOne(pol, time, assign, seed0 + i));
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
            Enumerable.Range(0, Map11.Squads.Length).Select(s => o.Sum(x => x.WonBySquad[s])).ToArray());
    }

    // =================================================================================
    // 表
    // =================================================================================

    /// <summary>線（測る前に固定・指示書 §2-2）。</summary>
    public const double Line1Intercept = 50.0;   // 全快: 迎撃が起きた率 >= 50%
    public const double Line2Fall = 10.0;        // 貪欲: 陥落率 <= 10%
    public const double Line3Gap = 15.0;         // 踏破率の差 <= 15pt
    public const double Line3Ceiling = 95.0;     // どちらも 95% 未満

    public static bool Report(int seeds, Action<string> write, int seed0 = 0)
    {
        int[] straight = { 1, 0 };   // 正: カド隊 -> 南(1) ／ かき回し隊 -> 北(0)

        write("# 第174期 段B1 —— 時間を器具で当てる");
        write("");
        write($"seed {seed0}..{seed0 + seeds - 1}。道中の回復 {Map11.RecoverPercent}%（第168期の本丸の条件・そのまま）。"
            + $"休む 1 回で {TimeRule.Every(1).RestPercent}%。割り当ては正"
            + "（カド隊 → 南 ／ かき回し隊 → 北）、控えは第169期と同じ自動規則。");
        write("");

        // ---- 対照: 時間なし ----
        Map11Verify.Stat off = Map11Verify.Band(straight, seed0, seeds);
        write("## 対照（`TimeOn = false`）");
        write("");
        write($"踏破率 **{off.Full:F1}%**（第169期 {Map11Verify.ExpectStraight:F1}%・"
            + $"差 {off.Full - Map11Verify.ExpectStraight:+0.0;-0.0;0.0}pt）／ 部分点 {off.Partial:F3}。");
        write("");

        // ---- 本表 ----
        write("## 本表（K × 方針）");
        write("");
        write("| K | 方針 | 踏破率 | 迎撃が起きた率 | 陥落率 | 作戦T 中央値 | 戦闘/戦 | 部分点 | 迎撃/戦 | 迎撃の北% | 迎撃の勝率 | 休/戦 |");
        write("|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

        var table = new Dictionary<(int, Policy), Stat>();
        foreach (int k in Ks)
            foreach (Policy pol in new[] { Policy.Greedy, Policy.Full, Policy.Turtle })
            {
                Stat st = Band(pol, TimeRule.Every(k), straight, seeds, seed0);
                table[(k, pol)] = st;
                write($"| {k} | {NameOf(pol)} | {st.Full:F1}% | {st.Intercept:F1}% | {st.Fall:F1}% "
                    + $"| {st.TurnMedian:F1} | {st.Battles:F2} | {st.Partial:F3} | {st.InterceptPerRun:F2} "
                    + $"| {st.NorthShare:F1}% | {st.InterceptWin:F1}% | {st.Rests:F2} |");
            }
        write("");

        // ---- 線 ----
        write("## 線（測る前に固定）");
        write("");
        write("| K | 線1 全快の迎撃 ≥ 50% | 線2 貪欲の陥落 ≤ 10% | 線3 差 ≤ 15pt かつ両方 < 95% | 本丸 |");
        write("|--:|:-:|:-:|:-:|:-:|");
        int adopted = -1;
        foreach (int k in Ks)
        {
            Stat g = table[(k, Policy.Greedy)], f = table[(k, Policy.Full)];
            bool l1 = f.Intercept >= Line1Intercept;
            bool l2 = g.Fall <= Line2Fall;
            double gap = Math.Abs(g.Full - f.Full);
            bool l3 = gap <= Line3Gap && g.Full < Line3Ceiling && f.Full < Line3Ceiling;
            bool all = l1 && l2 && l3;
            if (all && adopted < 0) adopted = k;
            write($"| {k} | {(l1 ? "**○**" : "×")} {f.Intercept:F1}% | {(l2 ? "**○**" : "×")} {g.Fall:F1}% "
                + $"| {(l3 ? "**○**" : "×")} 差 {gap:F1}pt ／ {g.Full:F1}% ・ {f.Full:F1}% "
                + $"| {(all ? "**○**" : "×")} |");
        }
        write("");

        // ---- 迎撃に出たのは誰か（予測 W3） ----
        write("## 迎撃に出た隊（予測 W3）");
        write("");
        write("| K | 方針 | " + string.Join(" | ", Map11.Squads.Select(x => x.Name + " 回/勝率")) + " |");
        write("|--:|---|" + string.Concat(Map11.Squads.Select(_ => "--:|")));
        foreach (int k in Ks)
            foreach (Policy pol in new[] { Policy.Greedy, Policy.Full })
            {
                Stat st = table[(k, pol)];
                string cells = string.Join(" | ", Enumerable.Range(0, Map11.Squads.Length)
                    .Select(i => st.BySquad[i] == 0 ? "—"
                        : $"{st.BySquad[i]} / {100.0 * st.WonBySquad[i] / st.BySquad[i]:F1}%"));
                write($"| {k} | {NameOf(pol)} | {cells} |");
            }
        write("");

        // ---- 籠城（第174期の観察ログ・判定には使わない） ----
        write("## 籠城（拠点から一歩も出ない）—— 観察ログ 問い3 への数字");
        write("");
        write("**判定には使わない。** ポンの3プレイ目が「拠点から一歩も出ず殲滅した」"
            + "「待ってるだけでクリアできるのはゲームの趣旨と合わない」だったので、"
            + "**それが何%通るのか**を数えただけ。");
        write("");
        write("| K | 籠城の踏破率 | 貪欲の踏破率 | 差 | 籠城の作戦T 中央値 |");
        write("|--:|--:|--:|--:|--:|");
        foreach (int k in Ks)
        {
            Stat t = table[(k, Policy.Turtle)], g = table[(k, Policy.Greedy)];
            write($"| {k} | **{t.Full:F1}%** | {g.Full:F1}% | {t.Full - g.Full:+0.0;-0.0;0.0}pt "
                + $"| {t.TurnMedian:F1} |");
        }
        write("");

        write(adopted > 0
            ? $"**本丸 ○ —— 線1〜3 が同時に通る K のうち最も小さいのは K = {adopted}。既定にする。**"
            : "**本丸 × —— どの K でも線1〜3 は同時に通らない。段B2 へは進まない。**");
        return adopted > 0;
    }
}
