using System;

// =====================================================================================
// 行き先から1ターンの命令を作る層（第175期 §1-1）
//
// **規則は1つも持たない。** 進む・戻る・休むの中身はすべて `Map11State` にあり、
// ここがするのは「**いまの位置と行き先から、この作戦ターンに何をするか**」を1つ選ぶことだけ。
//
// **`Map11State` の外に置いた**のは、遊ぶ側（`Map11Session` / `Map11Main`）と
// 頭なしの器具（`Map11Time`）が<b>同じこの層</b>を通れるようにするため（R259）。
// **ここには Godot の型を1つも使わない**——`Map11.cs` と同じ扱いである。
//
// 第174期は「1作戦ターンに1命令」を毎ターン置く形だった。**規則は1ビットも変えず、
// 命令の持ち方だけを変えた**——命令は行き先から毎ターン自動で作られる。
// =====================================================================================

/// <summary>
/// 行き先（マス1つ）。<c>Road &lt; 0</c> ＝ 拠点。
/// <b>道の上の位置はマスの番号で持つ</b>（0 が空きマス、1・2 が敵部隊の初期位置）。
/// </summary>
public readonly record struct Dest(int Road, int Cell)
{
    /// <summary>拠点（＝戻る。着いたら休む）。</summary>
    public static readonly Dest Home = new(-1, -1);

    public bool AtHome => Road < 0;

    /// <summary>その道のいちばん奥（＝抜き切るまで進み続ける）。<b>敵の拠点は含まない。</b></summary>
    public static Dest Deep(int road) => new(road, Map11.RoadCells - 1);

    /// <summary>その道の先の<b>敵の拠点</b>（第176期）。拠点なしの設定では誰も立てない。</summary>
    public static Dest Portal(int road) => new(road, Map11.PortalCell);

    /// <summary>行き先が敵の拠点か。</summary>
    public bool IsPortal => Road >= 0 && Cell >= Map11.PortalCell;
}

public static class Map11Orders
{
    /// <summary>
    /// 1 隊ぶんの命令。<b>第174期の <c>Map11Session.Order</c> をここへ移しただけ</b>
    /// （「組み直す」の枝は落とした——組み直しは作戦ターンを使わない操作になった）。
    /// </summary>
    public enum Order { Wait, Advance, Back, Rest, Warp }

    /// <summary>マスの名前（空きマスだけ名前を持ち、残りは区画の番号で呼ぶ）。</summary>
    public static string CellName(int cell)
        => cell >= Map11.PortalCell ? Map11.PortalName : cell <= 0 ? "手前" : $"{cell} 戦目の地点";

    /// <summary>行き先の1語（隊の札と盤の矢印に同じものを出す）。</summary>
    public static string Where(Dest dest)
        => dest.AtHome ? "拠点"
         : dest.IsPortal ? Map11.PortalName
         : $"{Map11.RoadNames[dest.Road]} {CellName(dest.Cell)}";

    /// <summary>
    /// 行き先から、この作戦ターンの命令を1つ決める（<b>盤面は1ビットも動かさない</b>）。
    ///
    /// <list type="bullet">
    /// <item>行き先が拠点 —— 道の上なら戻る。拠点に着いていれば、傷が残っていれば休み、満タンなら待つ</item>
    /// <item>行き先が道の上 —— 近づくほうへ1マス。別の道なら<b>いったん拠点まで戻る</b>（一瞬では移れない）</item>
    /// <item>着いたら待つ。<b>ただし同じマスに敵がいればもう一度当たる</b>（前の戦闘が決着しなかったとき）</item>
    /// </list>
    /// </summary>
    public static Order Plan(Map11State st, int squad, Dest dest)
    {
        Map11State.Squad s = st.Squads[squad];
        if (s.Lost || !st.CanSend(squad)) return Order.Wait;
        bool home = s.Cell < 0;

        // 制圧した拠点どうしは1作戦ターンで行き来できる（第176期 §1-3）。
        if (st.CanWarp(squad, dest)) return Order.Warp;

        if (dest.AtHome)
        {
            if (!home) return Order.Back;
            return st.Hurt(squad) ? Order.Rest : Order.Wait;
        }

        // 抜け切った道へは出ない（出ても当たる相手がいない）。**敵の拠点があるならその先へ行ける。**
        if (!st.CanHead(dest.Road)) return home ? Order.Wait : Order.Back;
        if (home) return Order.Advance;
        if (s.Road != dest.Road) return Order.Back;
        if (s.Cell < dest.Cell) return Order.Advance;
        if (s.Cell > dest.Cell) return Order.Back;
        // 着いている。敵が同じマスまで来ていれば、もう一度当たる。
        if (st.FoeFacing(squad) is not null) return Order.Advance;
        // 制圧した敵の拠点（第2拠点）に立っているなら、傷が残っていれば休む。
        return st.AtSecondBase(squad) && st.Hurt(squad) ? Order.Rest : Order.Wait;
    }

    /// <summary>
    /// 命令を実行する。<b>戻り値が null でなければ、そこで戦闘</b>（相手の区画）。
    /// <b>中身は `Map11State` の <c>Advance</c> / <c>StepBack</c> / <c>Rest</c> を呼ぶだけ。</b>
    /// </summary>
    public static Map11State.Node? Apply(Map11State st, int squad, Dest dest, Order order)
    {
        switch (order)
        {
            case Order.Advance:
                int road = st.Squads[squad].Cell < 0 ? dest.Road : st.Squads[squad].Road;
                return road < 0 ? null : st.Advance(squad, road);
            case Order.Back:
                st.StepBack(squad);
                return null;
            case Order.Rest:
                st.Rest(squad);
                return null;
            case Order.Warp:
                st.Warp(squad, dest);
                return null;
            default:
                return null;
        }
    }

    /// <summary>その隊がこの作戦ターンに何をするか（画面に出す1語）。</summary>
    public static string Describe(Map11State st, int squad, Dest dest) => Plan(st, squad, dest) switch
    {
        Order.Advance => $"{Where(dest)} へ進む",
        Order.Back => dest.AtHome ? "拠点へ戻る" : $"{Where(dest)} へ（拠点を経由）",
        Order.Rest => st.AtSecondBase(squad) ? $"休む（{Map11.PortalName}）" : "休む（拠点）",
        Order.Warp => dest.AtHome ? "拠点へワープ" : $"{Map11.PortalName} へワープ",
        _ => dest.AtHome ? "拠点で待つ" : $"{Where(dest)} に到着（待機）",
    };
}
