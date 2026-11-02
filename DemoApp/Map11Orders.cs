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
/// 行き先（マス1つ・第438期から盤 <see cref="MapGraph"/> のマス Id）。
/// <c>Lane</c> は<b>そのマスの経路</b>で、敵の拠点だけは<b>どの経路を通って入るか</b>（1-1 では北と南がそこで合流する）。
/// </summary>
public readonly record struct Dest(int Id, int Lane, MapGraph.Kind Kind)
{
    /// <summary>拠点（＝戻る。着いたら休む）。<b>拠点の Id はどの盤でも <see cref="MapGraph.HomeId"/></b>。</summary>
    public static readonly Dest Home = new(MapGraph.HomeId, -1, MapGraph.Kind.Home);

    public bool AtHome => Kind == MapGraph.Kind.Home;

    /// <summary>行き先の経路（第437期の <c>Road</c>）。拠点は -1。</summary>
    public int Road => Lane;

    /// <summary>そのマスへ（敵の拠点なら経路は決まらない——<see cref="Portal"/> を使う）。</summary>
    public static Dest Of(MapGraph g, int id) => new(id, g[id].Lane, g[id].Kind);

    /// <summary>その経路のいちばん奥（＝抜き切るまで進み続ける）。<b>敵の拠点は含まない。</b></summary>
    public static Dest Deep(MapGraph g, int lane) => Of(g, g.Lanes[lane][^1]);

    /// <summary>その経路の先の<b>敵の拠点</b>（第176期）。拠点なしの設定では誰も立てない。</summary>
    public static Dest Portal(MapGraph g, int lane) => new(g.Portal, lane, MapGraph.Kind.Portal);

    /// <summary>行き先が敵の拠点か。</summary>
    public bool IsPortal => Kind == MapGraph.Kind.Portal;
}

public static class Map11Orders
{
    /// <summary>
    /// 1 隊ぶんの命令。<b>第174期の <c>Map11Session.Order</c> をここへ移しただけ</b>
    /// （「組み直す」の枝は落とした——組み直しは作戦ターンを使わない操作になった）。
    /// </summary>
    public enum Order { Wait, Advance, Back, Rest, Warp, Move }

    /// <summary>マスの名前（空きマスだけ名前を持ち、残りは区画の番号で呼ぶ）。</summary>
    public static string CellName(int cell)
        => cell >= Map11.PortalCell ? Map11.PortalName : cell <= 0 ? "手前" : $"{cell} 戦目の地点";

    /// <summary>行き先の1語（隊の札と盤の矢印に同じものを出す）。<b>第438期からマスの名前</b>（1-1 では第437期と同じ文字列）。</summary>
    public static string Where(MapGraph g, Dest dest)
        => dest.AtHome ? "拠点"
         : dest.IsPortal ? Map11.PortalName
         : g[dest.Id].Name;

    /// <summary>
    /// 行き先から、この作戦ターンの命令を1つ決める（<b>盤面は1ビットも動かさない</b>）。
    ///
    /// <list type="bullet">
    /// <item>行き先が拠点 —— 道の上なら戻る。拠点に着いていれば、傷が残っていれば休み、満タンなら待つ</item>
    /// <item>行き先が道の上 —— 盤の最短路（<see cref="MapGraph.Path"/>）の次のマスへ1マス。別の道なら<b>いったん拠点まで戻る</b>
    /// ——<b>第438期から別の規則は書かない</b>: 敵の拠点を経由しない最短路は 1-1 では拠点を通る</item>
    /// <item>着いたら待つ。<b>ただし同じマスに敵がいればもう一度当たる</b>（前の戦闘が決着しなかったとき）</item>
    /// </list>
    /// </summary>
    public static Order Plan(Map11State st, int squad, Dest dest)
    {
        Map11State.Squad s = st.Squads[squad];
        if (s.Lost || !st.CanSend(squad)) return Order.Wait;
        bool home = s.Pos == st.Graph.Home;

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
        if (st.AtDest(squad, dest))
        {
            // 着いている。敵が同じマスまで来ていれば、もう一度当たる。
            if (st.FoeFacing(squad) is not null) return Order.Advance;
            // 制圧した敵の拠点（第2拠点）に立っているなら、傷が残っていれば休む。
            return st.AtSecondBase(squad) && st.Hurt(squad) ? Order.Rest : Order.Wait;
        }
        return Toward(st, squad, Next(st, squad, dest));
    }

    /// <summary>行き先への最短路の次のマス（<b>盤面は1ビットも動かさない</b>）。</summary>
    private static int Next(Map11State st, int squad, Dest dest)
    {
        Map11State.Squad s = st.Squads[squad];
        return st.Graph.NextStep(s.Pos, s.Road, dest.Id, dest.Lane);
    }

    /// <summary>
    /// 次のマスへの 1 歩を命令に直す: 自分の経路の奥へなら進む（<see cref="Map11State.Advance"/>）、
    /// 拠点寄りへなら戻る（<see cref="Map11State.StepBack"/>）、<b>経路をまたぐ辺なら移る</b>
    /// （<see cref="Map11State.MoveTo"/>・1-1 には無い辺——玩具の盤のために置いた）。
    /// </summary>
    private static Order Toward(Map11State st, int squad, int next)
    {
        Map11State.Squad s = st.Squads[squad];
        if (next == s.Pos) return Order.Wait;
        int at = st.Graph.IndexOn(s.Road, next);
        if (at == int.MinValue) return Order.Move;
        return at > s.Cell ? Order.Advance : Order.Back;
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
                int road = st.Squads[squad].Pos == st.Graph.Home ? dest.Road : st.Squads[squad].Road;
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
            case Order.Move:
                return st.MoveTo(squad, Next(st, squad, dest));
            default:
                return null;
        }
    }

    /// <summary>
    /// この作戦ターンに当たる相手（当たらなければ null）。<b>盤面は1ビットも動かさない。</b>
    ///
    /// <para>第177期（第176期 §3-1 #2）—— 画面の「この作戦ターン」に
    /// <b>「〈地点〉で〈敵の名前〉と接敵」</b>と出すためだけにある。
    /// <b>止まる場所の式は <see cref="Map11State.Advance"/> と同じ</b>
    /// （敵は追い越せないので、行き先は先頭の敵のマスで切られる）。</para>
    /// </summary>
    public static Map11State.Node? Meeting(Map11State st, int squad, Dest dest)
    {
        if (Plan(st, squad, dest) != Order.Advance) return null;
        Map11State.Squad s = st.Squads[squad];
        // すでに同じマスにいる（前の戦闘が決着しなかった）ならその相手。
        if (st.FoeFacing(squad) is { } now) return now;
        int road = s.Pos == st.Graph.Home ? dest.Road : s.Road;
        if (road < 0) return null;
        Map11State.Node? front = st.FrontFoe(road);
        int limit = front?.Cell ?? st.MaxCell;
        int next = Math.Min(s.Cell < 0 ? 0 : s.Cell + 1, Math.Min(limit, st.MaxCell));
        return front is not null && front.Cell == next ? front : null;
    }

    /// <summary>その隊がこの作戦ターンに何をするか（画面に出す1語）。</summary>
    public static string Describe(Map11State st, int squad, Dest dest) => Plan(st, squad, dest) switch
    {
        // 第177期: **接敵するターンはそう書く**（「〜へ進む」だと何が起きるか読めない）。
        Order.Advance => Meeting(st, squad, dest) is { } foe
            ? (foe.Def.Road < 0 ? $"{Map11.PortalName} で {foe.Def.Name} と接敵"   // 第441期: 待機中の予備隊
               : $"{st.Stage.LaneNames[foe.Def.Road]} {CellName(foe.Cell)} で {foe.Def.Name} と接敵")
            : $"{Where(st.Graph, dest)} へ進む",
        Order.Move => $"{Where(st.Graph, dest)} へ進む",
        Order.Back => dest.AtHome ? "拠点へ戻る" : $"{Where(st.Graph, dest)} へ（拠点を経由）",
        Order.Rest => st.AtSecondBase(squad) ? $"休む（{Map11.PortalName}）" : "休む（拠点）",
        Order.Warp => dest.AtHome ? "拠点へワープ" : $"{Map11.PortalName} へワープ",
        _ => dest.AtHome ? "拠点で待つ" : $"{Where(st.Graph, dest)} に到着（待機）",
    };
}
