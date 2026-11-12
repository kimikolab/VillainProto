using BattleCore;

public partial class Main
{
    private readonly DebuffReadout _debuffReadout = new();
    private DebuffPlayback? _debuffPlayback;
    internal BattleResult? SourceBattleResult => _debuffPlayback?.Source ?? _result;
    internal int DeferredMudoBursts => _debuffPlayback?.DeferredBursts ?? 0;
    internal DebuffPlayback? PlaybackProjection => _debuffPlayback;
    internal int MudoBurstPoseConflicts { get; private set; }

    private void ReadDebuffReadout(BattleEvent e, BattlePawn3D? target)
    {
        _debuffReadout.Read(e);
        if (target is not null && _debuffReadout.MudPower(target.InstanceId) is int mud) target.MudPower = mud;
        if (e.Kind == BattleEventKind.StatusGain && e.Text == BattleContext.UtsuHitsText && target?.UnitId == "utsu")
        {
            target.UtsuHits = System.Math.Clamp(e.Amount, 1, PerverseTrait.MaxHits);
            _battleField.SetUtsuDebuff(target, target.UtsuHits > 1 ? target.UtsuHits : 0, false);
        }
    }
}
