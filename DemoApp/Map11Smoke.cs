using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// =====================================================================================
// 第177期 §5 (c) —— 「制圧 → ワープ → 第2拠点で組み直し → 勝利」を通す通しの**方針**。
//
// **ここには Godot の型を1つも使わない**（`Map11.cs` / `Map11Orders.cs` と同じ扱い）
// ——画面を通す通し（`Map11Main`）と、seed を選ぶための頭なしの試し走り（`Probe`）が
// **同じこのクラスを通る**。方針を2箇所に書くと、seed を選んだ走りと本番の走りが別物になる（R259）。
//
//     Godot_console.exe --path DemoApp --headless -- --map11-portal-seed[=本数]
// =====================================================================================

/// <summary>
/// 自己検査 (c) の方針。<b>規則は1つも持たない</b>——行き先を置き、
/// 第2拠点にいる隊を1度だけ組み直すだけで、進む・戦う・制圧するの中身は <see cref="Map11State"/> にある。
/// </summary>
public sealed class Map11Smoke
{
    /// <summary>ワープを1度でも通したか（(c) の2つ目。<b>実際に動いた回数で読む</b>）。</summary>
    public bool Warped { get; private set; }
    /// <summary>第2拠点で1度でも組み直したか（(c) の3つ目）。</summary>
    public bool Reformed { get; private set; }

    /// <summary>
    /// 1 作戦ターンぶんの行き先を置く。<paramref name="set"/> に
    /// 「その隊の行き先」を渡す（画面なら <c>Map11Session.SetDestination</c>、
    /// 頭なしなら配列への代入）。
    ///
    /// <para><b>隊 0 は南の道を抜いたら、湧きが 1 部隊出るまで道の奥で待つ。</b>
    /// これが無いと<b>制圧した瞬間に盤上の敵が 0 になって勝ってしまい</b>、
    /// ワープも第2拠点の組み直しも通せない——敵は必ず拠点へ向かって歩くので、
    /// <b>ポータルに辿り着けた時点で最初の4部隊は全部倒れている</b>（R278 の裏側）。</para>
    /// </summary>
    public void Orders(Map11State st, Action<int, Dest> set, Action<string>? log = null)
    {
        // **実際に動いた回数で読む**——`CanWarp` が真でも、同じ作戦ターンに
        // 別の隊が先にポータルを使えばリキャストに阻まれる（実際に踏んだ）。
        if (st.Warps > 0) Warped = true;

        // 第2拠点に立っている隊を1度だけ組み直す（席を2つ入れ替えるだけ）。
        if (st.Captured && !Reformed)
            for (int i = 0; i < st.Squads.Length; i++)
                if (st.AtSecondBase(i) && st.SwapSeats(i, 0, i, 3))
                {
                    Reformed = true;
                    log?.Invoke($"reform-at-second-base squad={st.Squads[i].Label}");
                    break;
                }

        for (int i = 0; i < st.Squads.Length; i++)
        {
            Map11State.Squad s = st.Squads[i];
            if (s.Lost) { set(i, Dest.Home); continue; }

            if (!st.Captured)
            {
                // 隊 1 は北の道を抜いたら拠点へ戻る（湧いた敵を迎撃で受けるため）。隊 2 は拠点で待つ。
                if (i != 0)
                {
                    set(i, i == 1 && st.NextNode(0) is not null ? Dest.Deep(0) : Dest.Home);
                    continue;
                }
                // **湧いた 1 部隊がポータルのマスから離れるまで待つ。**
                // 湧いた直後に入ると、そこで戦って勝った瞬間に盤上の敵が 0 になり、
                // やはり「制圧 ＝ 勝利」で終わってしまう（ワープを通せない）。
                bool go = st.SpawnCount > 0 && st.PortalFoe() is null;
                set(i, go ? Dest.Portal(1) : new Dest(1, Map11.RoadCells - 1));
                continue;
            }

            // 制圧した。**全部の隊を拠点へ引く**——湧いた敵は拠点へ向かって歩くので、
            // 迎撃で片付けるのがいちばん確実である。
            // 第2拠点に立っている隊は、この行き先が<b>そのままワープになる</b>
            // （`Map11Orders.Plan` が `CanWarp` を見て `Warp` を選ぶ）。
            if (st.AtSecondBase(i) && st.CanWarp(i, Dest.Home))
                log?.Invoke($"warp squad={s.Label} turn={st.Turn + 1}");
            set(i, Dest.Home);
        }
    }

    // =================================================================================
    // seed の選び方（**頭なしの試し走り**）
    //
    // 自己検査 (c) は「勝利まで通す」ことを求めるので、seed に依る。
    // **その seed をどう選んだかを、器具として残す。**
    // =================================================================================

    public sealed record Result(int Seed, bool Won, bool Warped, bool Reformed,
                                int CaptureTurn, int Battles, int Turns, int Spawns)
    {
        public bool Ok => Won && Warped && Reformed;
    }

    /// <summary>1 通しを頭なしで回す（<b>画面の通しとまったく同じ層</b>を通る）。</summary>
    public static Result RunOne(int seed)
    {
        var st = new Map11State(seed, Map11.AdoptedTime, Map11.AdoptedPortal, draft: true);
        st.UseDefaultSquads();
        var smoke = new Map11Smoke();
        var dest = Enumerable.Repeat(Dest.Home, st.Squads.Length).ToArray();

        void Fight(int squad, Map11State.Node node, bool intercept)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            if (prep is not { } p) return;
            BattleResult r = BattleEngine.Run(p.Players, p.Enemies, p.Seed, verbose: false);
            st.Resolve(squad, p.Node, r.PlayerWon, intercept);
        }

        while (!st.Finished)
        {
            smoke.Orders(st, (i, d) => dest[i] = d);
            for (int i = 0; i < st.Squads.Length && !st.Finished; i++)
            {
                Map11Orders.Order order = Map11Orders.Plan(st, i, dest[i]);
                if (Map11Orders.Apply(st, i, dest[i], order) is { } node) Fight(i, node, false);
            }
            if (st.Finished) break;
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (st.Finished) break;
                if (!e.Intercept) { Fight(e.Squad, e.Node, false); continue; }
                while (!st.Finished)
                {
                    int j = st.NextInterceptor(e.Node);
                    if (j < 0) break;
                    Fight(j, e.Node, true);
                    if (e.Node.Cleared) break;
                }
                st.CloseIntercept(e.Node);
            }
        }
        return new Result(seed, st.Won, smoke.Warped, smoke.Reformed,
                          st.CapturedTurn + 1, st.Battles, st.Turn, st.SpawnCount);
    }

    /// <summary>
    /// 通る seed を探して並べる（<c>--map11-portal-seed</c>）。
    /// <b>判定も線も持たない</b>——「(c) の4つが全部立つ seed はどれか」を出すだけ。
    /// </summary>
    public static bool Probe(int max, Action<string> write)
    {
        var got = new Result[max];
        Parallel.For(0, max, i => got[i] = RunOne(i));
        var ok = got.Where(x => x.Ok).ToArray();

        write("# 第177期 自己検査 (c) —— 通る seed（**頭なしの試し走り**）");
        write("");
        write($"seed 0..{max - 1}。方針は `Map11Smoke.Orders`（**画面の通しと同じ1本**）。");
        write($"`{Map11.AdoptedPortal}` ／ `{Map11.AdoptedTime}`。");
        write("");
        write($"**4つ全部が立つ seed: {ok.Length} / {max} 本**"
            + $"（勝利 {got.Count(x => x.Won)} ／ ワープ {got.Count(x => x.Warped)}"
            + $" ／ 第2拠点の組み直し {got.Count(x => x.Reformed)}"
            + $" ／ 制圧 {got.Count(x => x.CaptureTurn > 0)}）。");
        write("");
        write($"湧き 中央値 {got.OrderBy(x => x.Spawns).ElementAt(max / 2).Spawns}"
            + $" ／ 作戦T 中央値 {got.OrderBy(x => x.Turns).ElementAt(max / 2).Turns}"
            + $" ／ 戦闘 中央値 {got.OrderBy(x => x.Battles).ElementAt(max / 2).Battles}。");
        write("");
        write("先頭 8 本（**通らなかった seed も出す**——どこで止まるかを読むため）:");
        write("");
        write("| seed | 勝利 | ワープ | 組み直し | 制圧T | 戦闘 | 作戦T | 湧き |");
        write("|--:|:-:|:-:|:-:|--:|--:|--:|--:|");
        foreach (Result r in got.Take(8))
            write($"| {r.Seed} | {(r.Won ? "○" : "×")} | {(r.Warped ? "○" : "×")} "
                + $"| {(r.Reformed ? "○" : "×")} | {r.CaptureTurn} | {r.Battles} | {r.Turns} | {r.Spawns} |");
        write("");
        write("| seed | 勝利 | ワープ | 第2拠点で組み直し | 制圧T | 戦闘 | 作戦T | 湧き |");
        write("|--:|:-:|:-:|:-:|--:|--:|--:|--:|");
        foreach (Result r in ok.Take(12))
            write($"| {r.Seed} | {(r.Won ? "○" : "×")} | {(r.Warped ? "○" : "×")} "
                + $"| {(r.Reformed ? "○" : "×")} | {r.CaptureTurn} | {r.Battles} | {r.Turns} | {r.Spawns} |");
        write("");
        write($"MAP11_PORTAL_SEED first={(ok.Length > 0 ? ok[0].Seed : -1)} ok={ok.Length > 0}");
        return ok.Length > 0;
    }
}
