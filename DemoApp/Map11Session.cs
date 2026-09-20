using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// マップ 1-1 の通し（第169期）。**シーンを跨いで残る唯一の場所。**
//
// `CampaignSession`（既存の作戦マップ）とは**別の入れ物**にしてある——あちらは
// 「どこに居るか」と勝敗しか持っておらず、こちらは駒そのもの（`UnitState`）を持ち越す。
// 混ぜると、既存のデモ（単発の戦闘確認）が壊れる。
// =====================================================================================

public static class Map11Session
{
    public const string Scene = "res://Map11Main.tscn";

    /// <summary>1 回の通しの記録（どの隊をどの道へ何番目に出したか）。結果画面が出す。</summary>
    public sealed record Order(int Turn, string Squad, string Road);

    public static Map11State? State { get; private set; }
    public static int Seed { get; private set; }
    public static List<Order> Orders { get; } = new();
    public static int Battles { get; private set; }

    /// <summary>いま戦闘シーンへ送り出している隊と区画。戻ってきた1回だけ精算する。</summary>
    public static int PendingSquad { get; private set; } = -1;
    public static Map11State.Node? PendingNode { get; private set; }

    public static string? LastOutcome { get; set; }

    public static bool Active => State is not null;
    public static bool HasPendingBattle => PendingNode is not null;

    /// <summary>新しい通しを始める。seed を渡さなければ毎回振り直す（結果画面に出す）。</summary>
    public static void Reset(int? seed = null)
    {
        Seed = seed ?? new Random().Next(0, 1_000_000);
        State = new Map11State(Seed);
        Orders.Clear();
        Battles = 0;
        PendingSquad = -1;
        PendingNode = null;
        LastOutcome = null;
    }

    public static void EnsureStarted()
    {
        if (State is null) Reset();
    }

    /// <summary>隊を道へ送る（プレイヤーの判断）。記録に残す。</summary>
    public static void Send(int squad, int road)
    {
        if (State is null) return;
        Map11State.Squad s = State.Squads[squad];
        if (s.Lost) return;
        // 「送り先を変えた」だけを記録する（同じ道へ進め続けるのは1行にまとめる）。
        bool changed = !s.Deployed || s.Road != road;
        State.Send(squad, road);
        if (changed) Orders.Add(new Order(Orders.Count + 1, s.Def.Name, Map11.RoadNames[road]));
    }

    /// <summary>
    /// 接敵。<b>持っている <c>UnitState</c> のリストをそのまま戦闘シーンへ渡す</b>
    /// ——`Materialize` はしない（傷も死者もそのまま持ち込む）。
    /// </summary>
    public static bool BeginBattle(int squad)
    {
        if (State is null || HasPendingBattle) return false;
        if (State.Prepare(squad) is not { } prep) return false;
        PendingSquad = squad;
        PendingNode = prep.Node;
        CampaignSession.BeginCarriedBattle(prep.Players, prep.Enemies, prep.Seed, prep.Node.Def.Name, prep.Node.Def.StageIndex);
        return true;
    }

    /// <summary>戦闘から戻った1回だけ精算する（再生のやり直しでは二度走らない）。</summary>
    public static void CompleteBattle(bool playerWon)
    {
        if (State is null || PendingNode is null) return;
        Map11State.Node node = PendingNode;
        int squad = PendingSquad;
        PendingNode = null;
        PendingSquad = -1;

        string squadName = State.Squads[squad].Def.Name;
        State.Resolve(squad, node, playerWon);
        Battles = State.Battles;

        // **「抜けなかった」には2種類ある。** 全滅と、30 ターンで決着しない膠着。
        // 膠着は engine の上限ターン（`MaxTurns`）で、**同じ相手にもう一度当てても同じことが起きる**
        // ——判定は1つも足していないので、そう書いて引き返す判断をプレイヤーに返すだけ。
        LastOutcome = playerWon
            ? $"{squadName} が {node.Def.Name} を抜いた"
            : State.Squads[squad].Lost
                ? $"{squadName} は {node.Def.Name} に全滅した"
                : $"{squadName} は {node.Def.Name} と 30 ターン決着せず（両軍とも残っている）。"
                  + "もう一度当てても同じになる——引き返すか、別の隊を当てること";
    }

    /// <summary>結果画面に出す1行の並び。</summary>
    public static string OrderSummary() =>
        Orders.Count == 0 ? "（出撃の記録なし）"
        : string.Join(" → ", Orders.Select(o => $"{o.Squad}:{o.Road}"));
}
