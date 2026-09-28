using BattleCore;
using System.Collections.Generic;

// 台本の見出しと直後の実行を結ぶ索引。席・対象・発動条件は計算しない。
internal sealed class MovementPresentation
{
    internal readonly Dictionary<int, BattleEvent> Moves = new();
    internal readonly Dictionary<int, BattleEvent> Attacks = new();
    internal readonly Dictionary<int, BattleEvent> SpringGuards = new();
    internal readonly Dictionary<int, string> ArrowStates = new();
    internal readonly Dictionary<int, int?> BlastDestinations = new();
    internal readonly Dictionary<int, List<BattleEvent>> ShuffleStarts = new();
    internal readonly HashSet<int> ShuffleMoves = new();
    internal readonly HashSet<int> ShuffleEnds = new();

    internal static bool IsCue(BattleEventKind kind) => kind is
        BattleEventKind.Retreat or BattleEventKind.Regroup or BattleEventKind.ShioStage or BattleEventKind.Overflow
        or BattleEventKind.Evade or BattleEventKind.EvadeRiposte or BattleEventKind.EvadeStage
        or BattleEventKind.Decoy or BattleEventKind.LastDodge or BattleEventKind.Barrage or BattleEventKind.StatusArrow
        or BattleEventKind.Disarray or BattleEventKind.DisarrayStage or BattleEventKind.Squall
        or BattleEventKind.Blast or BattleEventKind.Spring or BattleEventKind.Tailwind
        or BattleEventKind.KillImpact or BattleEventKind.StaggerBreach
        or BattleEventKind.SpringGuard or BattleEventKind.DecoyShow or BattleEventKind.MoveShot;

    internal MovementPresentation(IReadOnlyList<BattleEvent> events)
    {
        for (int i = 0; i < events.Count; i++)
        {
            var cue = events[i];
            // 隣への介入と弾く本体は1組。次の別の弾き返しへ持ち越さない。
            if (cue.Kind == BattleEventKind.SpringGuard)
                for (int j = i + 1; j < events.Count; j++)
                {
                    var next = events[j];
                    if (next.Kind == BattleEventKind.TurnStart) break;
                    if (next.ActorId != cue.ActorId) continue;
                    if (next.Kind == BattleEventKind.Spring)
                    {
                        if (next.TargetId == cue.PartnerId) SpringGuards[j] = cue;
                        break;
                    }
                    if (next.Kind is BattleEventKind.SpringGuard or BattleEventKind.Attack) break;
                }
            if (cue.Kind is BattleEventKind.Retreat or BattleEventKind.Regroup or BattleEventKind.Evade
                or BattleEventKind.Spring or BattleEventKind.Tailwind or BattleEventKind.KillImpact or BattleEventKind.Blast)
            {
                var pending = new HashSet<int>();
                if (cue.Kind == BattleEventKind.Evade || cue.Kind == BattleEventKind.KillImpact && cue.Text == ImpactLabels.Tumble)
                { if (cue.ActorId is int actor) pending.Add(actor); }
                if (cue.Kind != BattleEventKind.Evade && cue.TargetId is int target) pending.Add(target);
                if (cue.PartnerId is int partner) pending.Add(partner);
                int attacks = 0;
                for (int j = i + 1; j < events.Count && pending.Count > 0; j++)
                {
                    var next = events[j];
                    if (next.Kind == BattleEventKind.TurnStart) break;
                    if (next.Kind == BattleEventKind.Death && next.TargetId is int dead) pending.Remove(dead);
                    if (next.ActorId == cue.ActorId)
                    {
                        if (next.Kind == BattleEventKind.Attack && ++attacks > (cue.Kind == BattleEventKind.Blast ? 1 : 0)) break;
                        if (next.Kind == cue.Kind) break;
                        if (next.Kind == BattleEventKind.Move && next.TargetId is int moved && pending.Remove(moved))
                            Moves[j] = cue;
                    }
                }
            }
            if (cue.Kind is BattleEventKind.Barrage or BattleEventKind.EvadeRiposte or BattleEventKind.Blast or BattleEventKind.Squall
                or BattleEventKind.MoveShot)
            {
                for (int j = i + 1; j < events.Count; j++)
                {
                    var next = events[j];
                    if (next.Kind == BattleEventKind.TurnStart) break;
                    if (next.ActorId != cue.ActorId || next.Kind != BattleEventKind.Attack) continue;
                    Attacks[j] = cue;
                    break;
                }
            }
            // 状態の矢は命中後の記録。その矢にだけ色を戻す。ログ本文は解析しない。
            if (cue.Kind == BattleEventKind.StatusArrow)
                for (int j = i - 1; j >= 0; j--)
                {
                    var previous = events[j];
                    if (previous.Kind == BattleEventKind.TurnStart) break;
                    if (previous.Kind != BattleEventKind.Attack || previous.ActorId != cue.ActorId) continue;
                    ArrowStates[j] = cue.Text ?? "";
                    break;
                }
        }
        // 吹っ飛ばしの着地点も台本から取る。死亡・移動拒否でMoveが無ければ席は予測しない。
        foreach (var attack in Attacks)
        {
            if (attack.Value.Kind != BattleEventKind.Blast) continue;
            foreach (var move in Moves)
                if (ReferenceEquals(move.Value, attack.Value) && events[move.Key].TargetId == attack.Value.TargetId)
                { BlastDestinations[attack.Key] = events[move.Key].Slot; break; }
        }
        // バサのターン頭の入れ替えには専用見出しが無い。追い風などに結んだMoveを除き、
        // 同じターン・書き手の最初のMoveにまとめる。書き手がバサかは再生側の駒IDで確認する。
        var starts = new Dictionary<(int Turn, int Actor), int>();
        var ends = new Dictionary<(int Turn, int Actor), int>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind != BattleEventKind.Move || e.Turn <= 0 || e.ActorId is not int actor || Moves.ContainsKey(i)) continue;
            var key = (e.Turn, actor);
            if (!starts.TryGetValue(key, out int first))
            {
                starts[key] = first = i;
                ShuffleStarts[first] = new();
            }
            ShuffleStarts[first].Add(e);
            ShuffleMoves.Add(i);
            ends[key] = i;
        }
        foreach (int last in ends.Values) ShuffleEnds.Add(last);
    }
}
