using Godot;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 第439期 §5-2 —— 時計の門（`--map11-clock-smoke`）
//
// **間隔 0 で `ClockStep` を手で回す**（`_Process` のフレームには刻ませない・1 回の遅延呼び出しの中で全部回す）。
//   (a) 1 刻で `Turn` が 1 進む
//   (b) 接敵で止まり、`TurnRunning` が真のあいだは ▶ を入れ直しても刻まない
//   (c) 一時停止中は `Turn` が動かない
//   (d) 誰も出さずに刻み続けると迎撃（または陥落）が立つ
//   (d') 1 枚も配らずに ▶ を押しても刻は進み、陥落で終わる（第438期まで画面が押せなくしていた形）
// 合否は `MAP11_CLOCK_SMOKE_COMPLETE ok=True` の行で読む（終了コードは第169期から 0 にならない）。
// 末尾の「報告のみ」の行は §7（誰も出ていなくても刻は進む・規則は不変なので器具の数で見る）。
// =====================================================================================

public partial class Map11Main
{
    private bool _clockSmoke;

    private void ClockSmoke()
    {
        Map11Clock.IntervalOverride = 0;
        Map11Clock.Fast = false;
        var fails = new List<string>();

        // ---- 既定の3隊・全員拠点で待つ（行き先は既定の「拠点」） ----
        Restart(draft: false);

        // (c) 一時停止中は刻まない
        int t0 = St.Turn;
        for (int i = 0; i < 20; i++) ClockStep(1.0);
        bool c = St.Turn == t0 && !Map11Clock.Running;
        GD.Print($"MAP11_CLOCK_SMOKE c paused turn={St.Turn} ok={c}");
        if (!c) fails.Add("c");

        // (a) 1 刻で 1 作戦ターン
        ToggleClock();
        bool started = Map11Clock.Running;
        ClockStep(0);
        bool a = started && St.Turn == t0 + 1;
        GD.Print($"MAP11_CLOCK_SMOKE a one-tick turn={t0}->{St.Turn} ok={a}");
        if (!a) fails.Add("a");

        // (d) 誰も出さずに刻み続ける（止まったら ▶ を押し直す）
        string why = Run(() => _interceptOverlay.Visible || _resultOverlay.Visible);
        bool d = _interceptOverlay.Visible || (St.Fallen && _resultOverlay.Visible);
        GD.Print($"MAP11_CLOCK_SMOKE d wait-at-home turn={St.Turn} intercept={_interceptOverlay.Visible}"
            + $" fallen={St.Fallen} turnRunning={Map11Session.TurnRunning} stops=[{why}] ok={d}");
        if (!d) fails.Add("d");

        // (d') 1 枚も配らずに ▶
        Restart(draft: true);
        bool empty = St.Squads.All(s => s.Units is not { Count: > 0 });
        why = Run(() => _interceptOverlay.Visible || _resultOverlay.Visible);
        bool d2 = empty && St.Fallen && _resultOverlay.Visible;
        GD.Print($"MAP11_CLOCK_SMOKE d' empty-hand turn={St.Turn} fallen={St.Fallen}"
            + $" result={_resultOverlay.Visible} stops=[{why}] ok={d2}");
        if (!d2) fails.Add("d'");

        // (b) 接敵で止まり、戦闘の最中は刻まない
        Restart(draft: false);
        Map11Session.SetDestination(0, Dest.Deep(St.Graph, 1));
        why = Run(() => _encounterOverlay.Visible || _interceptOverlay.Visible || _resultOverlay.Visible);
        bool met = _encounterOverlay.Visible && Map11Session.TurnRunning && !Map11Clock.Running;
        int tb = St.Turn;
        Map11Clock.Start();                         // ▶ を入れ直しても
        for (int i = 0; i < 20; i++) ClockStep(1.0);
        bool b = met && St.Turn == tb && Map11Session.TurnRunning;
        GD.Print($"MAP11_CLOCK_SMOKE b encounter turn={tb}->{St.Turn} turnRunning={Map11Session.TurnRunning}"
            + $" foe={Map11Session.PendingNode?.Def.Name} stops=[{why}] ok={b}");
        if (!b) fails.Add("b");
        Map11Clock.Stop();

        ReportWaiting();

        GD.Print($"MAP11_CLOCK_SMOKE_COMPLETE ok={fails.Count == 0}"
            + (fails.Count == 0 ? "" : " failed=" + string.Join(",", fails)));
        GetTree().Quit(fails.Count == 0 ? 0 : 1);
    }

    /// <summary>通しを seed 0 で始め直す（<paramref name="draft"/> が偽なら既定の3隊）。</summary>
    private void Restart(bool draft)
    {
        Map11Session.Reset(0);
        Map11Clock.Bind(St);
        if (!draft) St.UseDefaultSquads();
        _reform = false;
        ClearHold();
        _encounterOverlay.Visible = _interceptOverlay.Visible = _resultOverlay.Visible = false;
        Refresh();
    }

    /// <summary>
    /// ▶ を押し、止まったら押し直して刻み続ける。<paramref name="until"/> が真になったら終わる。
    /// 返すのは<b>止まった理由の一覧</b>（トーストの文・作戦ターン）。
    /// </summary>
    private string Run(System.Func<bool> until)
    {
        var stops = new List<string>();
        for (int guard = 0; guard < Map11.TurnCap * 3 && !until(); guard++)
        {
            if (!Map11Clock.Running) ToggleClock();
            bool before = Map11Clock.Running;
            ClockStep(0);
            if (before && !Map11Clock.Running && !until()) stops.Add($"T{St.Turn} {_toast.Text}");
        }
        return string.Join(" | ", stops);
    }

    /// <summary>
    /// 報告のみ（§7）——「誰も出ていなくても刻は進む」。<b>規則は第438期と1ビットも違わない</b>ので、
    /// 遊ぶ側の規則（K ＝ 1・敵の拠点あり）で器具の方針を回して陥落の割合を出す。
    /// </summary>
    private static void ReportWaiting()
    {
        const int seeds = 100;
        var empty = Enumerable.Range(0, seeds).Select(EmptyRun).ToArray();
        GD.Print($"MAP11_CLOCK_SMOKE report empty-hand seeds={seeds}"
            + $" fallen={100.0 * empty.Count(x => x.Fallen) / seeds:F1}%"
            + $" turn-median={empty.Select(x => x.Turn).OrderBy(x => x).ElementAt(seeds / 2)}");
        foreach (Map11Time.Policy pol in new[]
                 { Map11Time.Policy.Greedy, Map11Time.Policy.Turtle, Map11Time.Policy.TurtleLast })
        {
            Map11Time.Stat s = Map11Time.Band(pol, Map11.AdoptedTime, Map11Portal.Straight, seeds,
                                              0, Map11.AdoptedPortal);
            GD.Print($"MAP11_CLOCK_SMOKE report {Map11Time.NameOf(pol)} seeds={seeds}"
                + $" full={s.Full:F1}% fallen={s.Fall:F1}% intercept={s.Intercept:F1}% turn-median={s.TurnMedian:F1}");
        }
    }

    /// <summary>1 枚も配らずに作戦ターンだけを回す（頭なし・戦闘は 1 回も起きない）。</summary>
    private static (bool Fallen, int Turn) EmptyRun(int seed)
    {
        var st = new Map11State(seed, Map11.AdoptedTime, Map11.AdoptedPortal, true);
        while (!st.Finished)
        {
            for (int i = 0; i < st.Squads.Length && !st.Finished; i++)
                Map11Orders.Apply(st, i, Dest.Home, Map11Orders.Plan(st, i, Dest.Home));
            if (st.Finished) break;
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (st.Finished) break;
                if (e.Intercept) st.CloseIntercept(e.Node);
            }
        }
        return (st.Fallen, st.Turn);
    }
}
