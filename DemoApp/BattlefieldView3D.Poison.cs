using Godot;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    public Task ShowPoisonDrain(IReadOnlyList<BattlePawn3D> sources, BattlePawn3D receiver, double speed)
    {
        SetPoisonClock(_poisonPaused, speed);
        return Task.WhenAll(sources.Select(source => ShowPoisonDrainFlow(source, receiver).Arrival));
    }

    // 経路と始点は台本で確定済み。陣営や隣接から戦闘の判定を再現しない。
    public Task ShowPoisonTransfer(BattlePawn3D? source, BattlePawn3D target,
        bool leak, double speed, bool drain = false, int amount = 1)
    {
        if (source is null) return Task.CompletedTask;
        SetPoisonClock(_poisonPaused, speed);
        Vector3 from = leak ? source.GlobalPosition + Vector3.Up * 0.09f : source.FxPoint;
        Vector3 to = leak ? target.GlobalPosition + Vector3.Up * 0.09f : target.FxPoint;
        return PoisonFlow(from, to, drain ? PoisonFlowFx.Kind.Drain
            : leak ? PoisonFlowFx.Kind.Leak : PoisonFlowFx.Kind.Touch, amount).Arrival;
    }
}
