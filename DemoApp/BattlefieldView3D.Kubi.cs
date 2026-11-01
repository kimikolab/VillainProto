using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    internal int KubiBellPlays { get; private set; }

    internal void ShowKubiBell(BattlePawn3D source, IReadOnlyList<BattlePawn3D> targets)
    {
        var recipients = targets.Distinct().Where(p => p.CanReceiveMovementImpact).ToArray();
        if (!IsVisibleInTree() || source.UnitId != "kubi" || !source.CanReceiveMovementImpact || recipients.Length == 0) return;
        KubiBellPlays++;
        source.TrembleWithKubiBell(UtsuClock);
        var resonance = new KubiBellFx3D(); _fxRoot.AddChild(resonance);
        resonance.Start(source, recipients, _camera, UtsuClock);
    }

    private void ResetKubiBellPresentation()
    {
        KubiBellPlays = 0;
        foreach (var pawn in _pawns.Values) pawn.ClearKubiBell();
        if (_fxRoot is not null)
            foreach (var fx in _fxRoot.GetChildren().OfType<KubiBellFx3D>()) { fx.Hide(); fx.QueueFree(); }
    }
}
