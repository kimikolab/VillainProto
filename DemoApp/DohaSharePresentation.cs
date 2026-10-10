using BattleCore;
using System.Collections.Generic;
using System.Linq;

// 同じ加害者の連続した被弾・肩代わりだけを束ねる。次の行動や死亡を先取りしない。
internal sealed class DohaSharePresentation
{
    internal readonly Dictionary<int, List<BattleEvent>> Ends = new();
    internal readonly HashSet<int> Members = new(), FastEvents = new();

    internal static DohaSharePresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new DohaSharePresentation();
        var shares = new List<int>();
        int turn = -1;
        int? source = null;
        bool hasSource = false, reaction = false;
        void Flush()
        {
            if (shares.Count > 0)
            {
                plan.Ends[shares[^1]] = shares.Select(i => events[i]).ToList();
                foreach (int index in shares) plan.Members.Add(index);
                if (shares.Count > 1)
                    for (int i = shares[0]; i <= shares[^1]; i++) plan.FastEvents.Add(i);
            }
            shares.Clear(); hasSource = false;
        }
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            bool share = e.Kind == BattleEventKind.ShareGive && e.Text == ShareGiveLabels.Power;
            if (e.Turn != turn) { Flush(); turn = e.Turn; }
            // 知らない通知も区切る。状態異常・反撃・移動をまとめて飛ばさない。
            if (e.Kind is not (BattleEventKind.Damage or BattleEventKind.Whet) && !share)
            { Flush(); continue; }
            if (e.Kind == BattleEventKind.Damage)
            {
                if (hasSource && (source != e.ActorId || reaction != e.Reaction)) Flush();
                source = e.ActorId; reaction = e.Reaction; hasSource = true;
            }
            if (share) shares.Add(i);
        }
        Flush();
        return plan;
    }
}
