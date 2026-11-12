using BattleCore;
using System.Collections.Generic;
using System.Linq;

// 台本の受け渡し札を読むだけ。元の受け手は既存のPartnerIdに載っているので推定しない。
internal sealed partial class DebuffPresentation
{
    internal sealed record Pass(int Actor, int From, int To, bool Intake);
    internal readonly Dictionary<int, Pass> Passes = new();
    internal readonly HashSet<int> DeliveredDulls = new();
    internal readonly HashSet<int> Costs = new();

    internal static DebuffPresentation Build(IReadOnlyList<BattleEvent> events, IReadOnlyList<DemoOpening> openings)
    {
        var plan = new DebuffPresentation();
        var wata = openings.Where(o => o.UnitId == "wata").Select(o => o.InstanceId).ToHashSet();
        int? relay = null, from = null;
        bool continuing = false;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            // 別作業の表示通知が途中に挟まっても、1回の受け渡しを分断しない。
            if (e.Kind == BattleEventKind.StatusGain && e.Text is "utsu_hits" or "wata_take" or "karz_mark") continue;
            if (e.Kind == BattleEventKind.CursePass && e.ActorId is int actor && wata.Contains(actor)
                && e.PartnerId is int origin && e.TargetId is int to && e.Amount > 0
                && e.Text is CursePassLabels.Feed or CursePassLabels.Push)
            {
                plan.Passes[i] = new(actor, origin, to, !continuing || relay != actor || from != origin);
                relay = actor; from = origin; continuing = true;
                // 直後の実際の弱体だけを既描画として扱う。別の書き手・別の対象は消さない。
                if (i + 1 < events.Count && events[i + 1] is { Kind: BattleEventKind.StatusGain, Text: "dull" } dull
                    && dull.ActorId == actor && dull.TargetId == to)
                    plan.DeliveredDulls.Add(i + 1);
                continue;
            }
            if (continuing && e.Kind == BattleEventKind.StatusGain && e.Text == "dull" && e.ActorId == relay) continue;
            if (continuing && e.Kind == BattleEventKind.Damage && e.TargetId == relay
                && (e.ActorId is null || e.ActorId == relay) && e.FriendlyFire)
                plan.Costs.Add(i);
            continuing = false;
        }
        plan.IndexCasts(events, openings);
        return plan;
    }
}
