using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// =====================================================================================
// 頭なしの通し確認（第169期・自己検査 (c)）
//
// **`Map11State` を、第168期 `BandOnce` と同じ自動規則で回す。**
// 遊ぶ側（`Map11Main`）と<b>同じクラス・同じメソッド</b>を通るので、
// 「器具の数字が出る」ことがそのまま「マップの進行が器具と同じ規則である」ことになる。
//
// 違いは1つだけ——**誰が「どの隊をどの道へ」を決めるか**。ここでは器具と同じ固定の割り当て
// （正: カド隊 → 南 ／ ハネ隊 → 北。逆はその入れ替え）で、控えは「隊が全滅した道へ1回だけ」。
//
//     Godot_console.exe --path DemoApp --headless -- --map11-verify
//     Godot_console.exe --path DemoApp --headless -- --map11-verify=200   # seed 本数を変える
// =====================================================================================

public static class Map11Verify
{
    /// <summary>1 帯あたりの seed 本数（第168期 <c>BandSeeds</c> と同じ）。</summary>
    public const int DefaultSeeds = 800;

    /// <summary>第168期 部B `S4/S4` × 回復 50% の実測（自己検査 (c) の相手）。</summary>
    public const double ExpectStraight = 95.6;
    public const double ExpectCross = 32.0;
    public const double ExpectRallyCross = 38.5;
    public const double ExpectTwoKadoSouth = 40.4;
    public const double ExpectTwoHaneNorth = 38.0;
    public const double Tolerance = 3.0;

    private readonly record struct Once(
        int Cleared, double Partial, bool TwoRunA, bool TwoRunB, bool ReserveUsed, bool ReserveCleared);

    public sealed record Stat(double Full, double Partial, double TwoA, double TwoB,
                              int ReserveN, double Rally);

    /// <summary>
    /// 1 マップぶん自動で回す。<b>第168期 <c>BandOnce</c> の進行をそのまま写した</b>
    /// ——隊 0 → 1 → 2 の順に 1 戦ずつ、担当の道が抜け切っていれば残っている道へ回る。
    /// </summary>
    private static Once RunOne(int[] assign, int seed)
    {
        var st = new Map11State(seed);
        st.Send(0, assign[0]);
        st.Send(1, assign[1]);
        bool usedReserve = false;

        while (st.Battles < Map11.BattleCap)
        {
            bool acted = false;
            for (int s = 0; s < st.Squads.Length; s++)
            {
                Map11State.Squad sq = st.Squads[s];
                if (sq.Units is null || sq.Road < 0) continue;

                // 担当の道が抜け切っていたら、残っている道のいちばん手前の番号へ回る
                // （`Home` は動かさない——2 本抜きは担当の道でしか数えない）。
                if (st.NextNode(sq.Road) is null)
                {
                    int alt = -1;
                    for (int road = 0; road < Map11.RoadCount; road++)
                        if (st.NextNode(road) is not null) { alt = road; break; }
                    if (alt < 0) break;
                    sq.Road = alt;
                }

                if (st.Prepare(s) is not { } prep) continue;
                BattleResult r = BattleEngine.Run(prep.Players, prep.Enemies, prep.Seed, verbose: false);
                acted = true;
                st.Resolve(s, prep.Node, r.PlayerWon);

                // 隊が全滅したら、控えがその道の続きへ **1 回だけ** 出る。
                if (sq.Units is null && !usedReserve && st.Squads[^1].Units is null
                    && !st.Squads[^1].Deployed)
                {
                    usedReserve = true;
                    st.Send(st.Squads.Length - 1, prep.Node.Def.Road);
                }

                if (st.AllCleared || st.Squads.All(x => x.Units is null)) goto fin;
            }
            if (!acted) break;
        }
    fin:
        Map11State.Squad res = st.Squads[^1];
        bool rallied = usedReserve && res.Home >= 0 && st.NextNode(res.Home) is null;
        return new Once(st.ClearedCount, st.Partial(),
                        st.Squads[0].Cleared >= 2, st.Squads[1].Cleared >= 2,
                        usedReserve, rallied);
    }

    public static Stat Band(int[] assign, int seed0, int seeds)
    {
        var o = new Once[seeds];
        Parallel.For(0, seeds, i => o[i] = RunOne(assign, seed0 + i));
        int rn = o.Count(x => x.ReserveUsed);
        return new Stat(
            100.0 * o.Count(x => x.Cleared >= Map11.TotalNodes) / seeds,
            o.Average(x => x.Partial),
            100.0 * o.Count(x => x.TwoRunA) / seeds,
            100.0 * o.Count(x => x.TwoRunB) / seeds,
            rn, rn == 0 ? 0 : 100.0 * o.Count(x => x.ReserveCleared) / rn);
    }

    /// <summary>標準出力へ表を出し、自己検査 (c) の合否を返す。</summary>
    public static bool Report(int seeds, Action<string> write)
    {
        int[] straight = { 1, 0 };   // 正: カド隊 → 南(1) ／ ハネ隊 → 北(0)
        int[] cross = { 0, 1 };      // 逆

        Stat p = Band(straight, 0, seeds);
        Stat q = Band(cross, 0, seeds);

        write("# 第169期 自己検査 (c) —— `DemoApp` の進行ロジックを頭なしで回す");
        write("");
        write($"seed 0..{seeds - 1}。道中の回復 {Map11.RecoverPercent}%。"
            + "隊・道・控えの出し方は第168期 部B の自動規則と同じ。");
        write("");
        write("| 量 | 器具（第168期 `S4/S4` × 50%） | `DemoApp` | 差 | 線 ±3.0pt |");
        write("|---|--:|--:|--:|:-:|");

        bool ok = true;
        void Row(string name, double want, double got)
        {
            double d = got - want;
            bool hit = Math.Abs(d) <= Tolerance;
            ok &= hit;
            write($"| {name} | {want:F1}% | **{got:F1}%** | {d:+0.0;-0.0;0.0} | {(hit ? "**○**" : "×")} |");
        }
        Row("踏破率 正（カド隊→南／ハネ隊→北）", ExpectStraight, p.Full);
        Row("踏破率 逆", ExpectCross, q.Full);
        Row("2 本抜き カド隊×南（正）", ExpectTwoKadoSouth, p.TwoA);
        Row("2 本抜き ハネ隊×北（正）", ExpectTwoHaneNorth, p.TwoB);
        Row("挽回率 逆", ExpectRallyCross, q.Rally);
        write("");
        write($"部分点 正 {p.Partial:F3} ／ 逆 {q.Partial:F3}（対比 {p.Partial - q.Partial:+0.000;-0.000}）。"
            + $"控えが出た試行 正 {p.ReserveN} / {seeds} ／ 逆 {q.ReserveN} / {seeds}。");
        write("");
        write(ok ? "**(c) ○ —— 5 つの量すべてが ±3.0pt 以内。**"
                 : "**(c) × —— 帯から外れた量がある。**");
        return ok;
    }
}
