using BattleCore;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Main
{
    private UtsuPresentation _utsu = new();
    private bool _utsuFastEvent;
    private async Task PlayUtsuAttack(int index, BattlePawn3D? actor, IReadOnlyList<BattlePawn3D> targets)
    {
        if (actor is null || !_utsu.Swings.TryGetValue(index, out var cue)) return;
        await _battleField.PlayUtsuSwing(actor, targets, cue);
    }
}
