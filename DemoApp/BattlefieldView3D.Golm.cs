using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private readonly Dictionary<int, GolmFx3D> _golmEffects = new();
    private bool _golmPaused;
    private double _golmSpeed = 1;
    internal int GolmSwallows, GolmSpits, GolmLaunches, GolmGrabs, GolmReturns, GolmHits, GolmStomps;
    internal IEnumerable<GolmFx3D> GolmEffects => _golmEffects.Values;

    private GolmFx3D GolmEffect(BattlePawn3D owner)
    {
        if (_golmEffects.TryGetValue(owner.InstanceId, out var effect) && IsInstanceValid(effect) && !effect.IsQueuedForDeletion()) return effect;
        effect = new GolmFx3D(); _fxRoot.AddChild(effect); effect.Configure(owner, _camera, _golmSpeed, _golmPaused);
        _golmEffects[owner.InstanceId] = effect; return effect;
    }
    internal void SetGolmClock(bool paused, double speed)
    {
        _golmPaused = paused; _golmSpeed = speed;
        foreach (var effect in _golmEffects.Values) if (IsInstanceValid(effect)) effect.SetClock(paused, speed);
    }
    internal void EndGolmPresentation()
    {
        foreach (var effect in _golmEffects.Values) if (IsInstanceValid(effect)) effect.Cancel();
        _golmEffects.Clear();
    }
    private void ResetGolmPresentation()
    {
        EndGolmPresentation(); _golmPaused = false;
        GolmSwallows = GolmSpits = GolmLaunches = GolmGrabs = GolmReturns = GolmHits = GolmStomps = 0;
    }
    internal async Task ShowGolmCue(BattlePawn3D? actor, BattlePawn3D? target, BattleEvent e)
    {
        if (actor is null) return;
        var fx = GolmEffect(actor);
        if (e.Kind == BattleEventKind.GolmArm)
        {
            switch (e.Text)
            {
                case GolmArmLabels.Launch when target is not null:
                    GolmLaunches++; await fx.Launch(target); break;
                case GolmArmLabels.Grab when target is not null:
                    GolmGrabs++; await fx.Grab(target, e.Slot == 1); break;
                case GolmArmLabels.Return:
                    GolmReturns++; await fx.Return(); break;
            }
        }
        else if (e.Text == SwallowLabels.Swallow && target is not null)
        {
            GolmSwallows++;
            Float(actor, "精気を呑む", GolmFx3D.Soul, true);
            await fx.Swallow(target);
        }
        else if (e.Text?.StartsWith(SwallowLabels.Spit + ":", System.StringComparison.Ordinal) == true && target is not null)
        {
            GolmSpits++;
            var parts = GolmPresentation.ParseSpit(e.Text);
            Float(actor, "吐き捨てる", GolmFx3D.Soul, true);
            await fx.Spit(target, parts);
        }
    }
    internal async Task PlayGolmImpact(BattlePawn3D? actor, BattlePawn3D? target, BattleEvent attack, bool stomp, bool captureFollows = false)
    {
        if (actor is null || target is null) return;
        var fx = GolmEffect(actor);
        bool live;
        if (stomp) { GolmStomps++; live = await fx.Stomp(target); }
        else { GolmHits++; live = await fx.Impact(attack.Amount, captureFollows: captureFollows); }
        if (!live) return;
        AttackContact?.Invoke(target);
    }
    internal async Task ReturnGolmAfterDeath(int owner)
    {
        if (!_golmEffects.TryGetValue(owner, out var effect) || !IsInstanceValid(effect)) return;
        GolmReturns++; await effect.Return();
    }
}
