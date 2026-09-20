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

    /// <summary>
    /// <b>戦闘1回につき1行</b>（第170期 §1-5）。第169期の「出した順」は
    /// <b>送り先を変えたときだけ</b>1行足す作りだったので、戦闘回数と合わなかった
    /// ——3 回目の走行は戦闘 5 回に対して 3 行しか出ず、
    /// <b>どの隊が何戦目を抜いたか後から復元できなかった。</b>
    /// </summary>
    /// <param name="No">何戦目か（1 始まり）。</param>
    /// <param name="Squad">出した隊。</param>
    /// <param name="Road">道。</param>
    /// <param name="Foe">相手の部隊名（その道の何番目か込み）。</param>
    /// <param name="Won">抜いたか。</param>
    /// <param name="Alive">戦闘後にその隊に残った枚数。</param>
    /// <param name="HpPercent">同・残り HP 割合。</param>
    /// <param name="Notes">盤面ルールが実際にしたこと（<see cref="Map11Info.RuleNotes"/>）。</param>
    public sealed record Line(int No, string Squad, string Road, string Foe,
                              bool Won, int Alive, double HpPercent, IReadOnlyList<string> Notes);

    public static Map11State? State { get; private set; }
    public static int Seed { get; private set; }
    public static List<Line> BattleLog { get; } = new();
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
        BattleLog.Clear();
        Battles = 0;
        PendingSquad = -1;
        PendingNode = null;
        LastOutcome = null;
    }

    public static void EnsureStarted()
    {
        if (State is null) Reset();
    }

    /// <summary>隊を道へ送る（プレイヤーの判断）。<b>記録は戦闘のたびに付ける</b>ので、ここでは残さない。</summary>
    public static void Send(int squad, int road)
    {
        if (State is null) return;
        if (State.Squads[squad].Lost) return;
        State.Send(squad, road);
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

    /// <summary>
    /// 戦闘から戻った1回だけ精算する（再生のやり直しでは二度走らない）。
    /// <paramref name="result"/> を渡すと、<b>盤面ルールが実際にしたこと</b>を記録に添える
    /// （第170期 §1-4）。<b>判定には1ビットも使わない——読むだけ。</b>
    /// </summary>
    public static void CompleteBattle(bool playerWon, BattleResult? result = null)
    {
        if (State is null || PendingNode is null) return;
        Map11State.Node node = PendingNode;
        int squad = PendingSquad;
        PendingNode = null;
        PendingSquad = -1;

        Map11State.Squad sq = State.Squads[squad];
        string squadName = sq.Def.Name;
        int road = node.Def.Road;
        State.Resolve(squad, node, playerWon);
        Battles = State.Battles;

        var notes = result is null ? new List<string>() : Map11Info.RuleNotes(result);
        int maxHp = sq.Def.F.Occupied().Sum(o => o.Def.MaxHp);
        int alive = sq.Units?.Count(u => u.IsAlive) ?? 0;
        double hpPct = sq.Units is null || maxHp == 0
            ? 0 : 100.0 * sq.Units.Where(u => u.IsAlive).Sum(u => u.Hp) / maxHp;
        BattleLog.Add(new Line(Battles, squadName, Map11.RoadNames[road],
                               $"{node.Def.Index + 1}. {node.Def.Name}",
                               playerWon, alive, hpPct, notes));

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

    /// <summary>盤上に残っている枚数（未出撃の隊は数えない）。</summary>
    public static int AliveOnMap =>
        State is null ? 0 : State.Squads.Sum(s => s.Units?.Count(u => u.IsAlive) ?? 0);

    /// <summary>まだ失っていない枚数（<b>未出撃の隊も数える</b>）。第169期に画面内で2通りに割れていた量。</summary>
    public static int AliveIncludingReserve =>
        State is null ? 0 : State.Squads.Sum(s =>
            s.Units is { } u ? u.Count(x => x.IsAlive)
            : s.Lost ? 0 : s.Def.F.Occupied().Count());
}
