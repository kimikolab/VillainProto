using BattleCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private readonly HashSet<int> _shieldShown = new();
    private readonly HashSet<int> _cowedShown = new();
    private int _cowedLostPlays, _cowedAbsorbed;

    // 台本の引受通知だけを読む。席や特性から守られる相手を推測しない。
    private List<(BattlePawn3D Covered, BattlePawn3D Shield)> FindShieldShares(int start)
    {
        var shares = new List<(BattlePawn3D, BattlePawn3D)>();
        if (_result is null) return shares;
        foreach (int i in ShieldShareIndices(_result.Events, start))
        {
            var e = _result.Events[i];
            var shield = _battleField.FindPawn(e.ActorId);
            var covered = _battleField.FindPawn(e.TargetId);
            if (shield is null || covered is null) continue;
            shares.Add((covered, shield));
            _shieldShown.Add(i);
        }
        return shares;
    }

    internal static List<int> ShieldShareIndices(IReadOnlyList<BattleEvent> events, int start)
    {
        var indices = new List<int>();
        var attack = events[start];
        for (int i = start + 1; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.TurnStart ||
                (e.Kind == BattleEventKind.Attack && e.ActorId == attack.ActorId)) break;
            if (e.Kind != BattleEventKind.Intercept || e.Text != InterceptLabels.RangeShield) continue;
            var hit = events.Skip(i + 1).FirstOrDefault(n => n.Kind is BattleEventKind.Damage
                or BattleEventKind.Parry or BattleEventKind.Attack or BattleEventKind.Intercept or BattleEventKind.TurnStart);
            if (hit is null || hit.Kind is not (BattleEventKind.Damage or BattleEventKind.Parry)
                || hit.ActorId != attack.ActorId || hit.Pattern != attack.Pattern) continue;
            indices.Add(i);
        }
        return indices;
    }

    private async Task PlayCowedGain(int start)
    {
        if (_result is null || _cowedShown.Contains(start)) return;
        var events = _result.Events;
        var first = events[start];
        var targets = new List<BattlePawn3D>();
        for (int i = start; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind != BattleEventKind.StatusGain || e.Text != StatusKeys.Cowed ||
                e.ActorId != first.ActorId || e.SpreadFromId != first.SpreadFromId || e.Turn != first.Turn) break;
            _cowedShown.Add(i);
            if (_battleField.FindPawn(e.TargetId) is not { } pawn) continue;
            if (e.Amount > 0) targets.Add(pawn);
            else pawn.SetStatusIcon(StatusKeys.Cowed, false);
        }
        if (targets.Count == 0) return;
        await _battleField.ShowCowedSpread(_battleField.FindPawn(first.SpreadFromId), targets.Distinct().ToArray(), _speed);
    }
}
