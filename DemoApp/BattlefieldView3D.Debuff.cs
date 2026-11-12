using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private readonly Dictionary<int, DebuffLayers3D> _debuffLayers = new();
    internal int DebuffIntakes { get; private set; }
    internal int DebuffDeliveries { get; private set; }
    internal int DebuffCosts { get; private set; }
    internal int DebuffBladeStrokes { get; private set; }
    internal int UtsuDebuffLayers(int id) => _debuffLayers.TryGetValue(id, out var layers)
        && IsInstanceValid(layers) && !layers.IsQueuedForDeletion() ? layers.Count : 0;

    internal void ResetDebuffPresentation()
    {
        ResetDebuffCastCounts();
        foreach (var pawn in _pawns.Values) pawn.ClearKubiBell();
        _debuffLayers.Clear();
        if (_fxRoot is not null)
            foreach (var fx in _fxRoot.GetChildren().OfType<DebuffFx3D>()) { fx.Hide(); fx.QueueFree(); }
        DebuffIntakes = DebuffDeliveries = DebuffCosts = DebuffBladeStrokes = 0;
    }

    internal void SetUtsuDebuff(BattlePawn3D pawn, int layers, bool over)
    {
        if (!pawn.CanReceiveMovementImpact) return;
        if (!_debuffLayers.TryGetValue(pawn.InstanceId, out var aura) || !IsInstanceValid(aura) || aura.IsQueuedForDeletion())
        {
            aura = new DebuffLayers3D();
            _fxRoot.AddChild(aura);
            aura.Start(pawn, _camera, UtsuClock);
            _debuffLayers[pawn.InstanceId] = aura;
        }
        aura.SetLayers(layers, over);
    }

    internal async Task ShowDebuffPass(BattlePawn3D relay, BattlePawn3D from, BattlePawn3D to, bool intake)
    {
        int generation = _specialGeneration;
        bool Live(BattlePawn3D p) => IsInstanceValid(p) && p.IsInsideTree() && p.CanReceiveMovementImpact;
        if (!Live(relay) || !Live(to)) return;
        if (intake && Live(from) && relay != from)
        {
            DebuffIntakes++;
            var suction = new DebuffThread3D(); _fxRoot.AddChild(suction);
            suction.Start(from, relay, _camera, UtsuClock, .18);
            await UtsuWait(.18, generation, _ => { });
        }
        if (generation != _specialGeneration || !IsVisibleInTree() || !Live(relay) || !Live(to)) return;
        DebuffDeliveries++;
        var thread = new DebuffThread3D(); _fxRoot.AddChild(thread);
        thread.Start(relay, to, _camera, UtsuClock, .23);
        await UtsuWait(.23, generation, _ => { });
        if (generation != _specialGeneration || !IsVisibleInTree() || !Live(to)) return;
        // 敵へ届いたときだけ鈍い刃の印。味方への渡しと同じ紫で、鎖の泥とは形を分ける。
        if (relay.Team != to.Team) DebuffPulse(to, false);
    }

    internal void ShowDebuffCost(BattlePawn3D pawn)
    {
        DebuffCosts++;
        DebuffPulse(pawn, true);
    }

    private void DebuffPulse(BattlePawn3D pawn, bool cost)
    {
        var pulse = new DebuffSigil3D(); _fxRoot.AddChild(pulse);
        pulse.Start(pawn, _camera, UtsuClock, cost);
    }

    private void ShowUtsuDebuffBlade(BattlePawn3D actor, int ordinal)
    {
        if (!_debuffLayers.TryGetValue(actor.InstanceId, out var aura) || !IsInstanceValid(aura) || aura.Count == 0) return;
        DebuffBladeStrokes++;
        aura.Strike(ordinal);
        var slash = new DebuffBlade3D(); _fxRoot.AddChild(slash);
        slash.Start(actor, _camera, UtsuClock, ordinal);
    }
}
