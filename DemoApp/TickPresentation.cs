using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// 戦闘の再計算はしない。実際に発行された刻みだけを、順序を保ったまま演出の拍へ割る。
internal sealed record TickCue(int Start, int End, int Ordinal, int Count, double Seconds, bool Last);

internal sealed class TickPresentation
{
    internal const double MaxSeconds = 0.96;
    internal readonly Dictionary<int, TickCue> Starts = new();
    internal readonly Dictionary<int, TickCue> Outcomes = new();
    internal readonly Dictionary<int, double> Budgets = new();

    internal static bool IsTick(BattleEvent e) => e.Kind == BattleEventKind.Status
        && (e.Text == "毒" || e.Text == "燃焼");

    internal static TickPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var result = new TickPresentation();
        var segments = new List<(int Start, int End)>();
        for (int i = 0; i < events.Count; i++)
        {
            if (!IsTick(events[i])) continue;
            int end = i + 1;
            while (end < events.Count && events[end].Kind is not
                (BattleEventKind.Status or BattleEventKind.TurnStart or BattleEventKind.Attack
                or BattleEventKind.Skill or BattleEventKind.Charge or BattleEventKind.StatusSnapshot
                or BattleEventKind.StatSnapshot or BattleEventKind.Highlight)) end++;
            segments.Add((i, end));
        }
        for (int first = 0; first < segments.Count;)
        {
            int stop = first + 1;
            while (stop < segments.Count)
            {
                var prior = segments[stop - 1];
                var next = segments[stop];
                var a = events[prior.Start];
                var b = events[next.Start];
                // 起爆は毒→火→毒→火。同じ何回目かも許すが、新たな1回目へは跨がない。
                bool follows = b.TickIndex == a.TickIndex + 1
                    || (b.TickIndex == a.TickIndex && a.Text == "毒" && b.Text == "燃焼" && a.ActorId is not null);
                if (prior.End != next.Start || a.TickIndex is null || b.TickIndex is null
                    || a.TickCount != b.TickCount || a.TargetId != b.TargetId
                    || a.ActorId != b.ActorId || a.Turn != b.Turn || !follows) break;
                stop++;
            }
            int count = stop - first;
            double total = Math.Min(MaxSeconds, 0.48 + (count - 1) * 0.15);
            // 後半ほど間隔を詰める。死亡で打ち切られた台本も最後の実在する件を強める。
            double weights = Enumerable.Range(0, count).Sum(k => 1.0 - 0.45 * k / Math.Max(1, count - 1));
            for (int k = 0; k < count; k++)
            {
                var (start, end) = segments[first + k];
                double seconds = total * (1.0 - 0.45 * k / Math.Max(1, count - 1)) / weights;
                var cue = new TickCue(start, end, k, count, seconds, k == count - 1);
                result.Starts[start] = cue;
                var status = events[start];
                // 啜りや反撃の回復を、元の刻みの数字として拾わない。
                for (int j = start + 1; j < end; j++)
                {
                    var e = events[j];
                    if (e.Kind == BattleEventKind.InverseSip) break;
                    bool outcome = status.SourceTrait == TraitId.Inverse
                        ? e.Kind == BattleEventKind.Heal && e.ActorId == status.InverterId
                        : e.Kind == BattleEventKind.Damage && e.ActorId is null;
                    if (!outcome || e.TargetId != status.TargetId) continue;
                    result.Outcomes[j] = cue;
                    break;
                }
                result.Budgets[start] = seconds * 0.55;
                for (int j = start + 1; j < end; j++)
                    result.Budgets[j] = seconds * 0.45 / Math.Max(1, end - start - 1);
            }
            first = stop;
        }
        return result;
    }
}
