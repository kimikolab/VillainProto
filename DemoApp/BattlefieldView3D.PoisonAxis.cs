using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    private bool _poisonPaused;
    private double _poisonSpeed = 1;
    internal int PoisonVents, PoisonBacklashes, PoisonRetorts, PoisonBites, BloodSips, PoisonTickWaves;
    internal int PurchasedPoisonSmokes;
    internal PoisonFlowFx[] ActivePoisonFlows => _fxRoot.GetChildren().OfType<PoisonFlowFx>().Where(f => !f.IsQueuedForDeletion()).ToArray();
    internal void SetPoisonClock(bool paused, double speed)
    {
        _poisonPaused = paused; _poisonSpeed = Math.Max(0.1, speed);
        foreach (var pawn in _pawns.Values) pawn.SetPoisonClock(paused, speed);
    }
    private void ResetPoisonPresentation()
    {
        foreach (var pawn in _pawns.Values) pawn.StopPoisonPresentation();
        if (_fxRoot is not null)
            foreach (var child in _fxRoot.GetChildren()) if (child is PoisonFlowFx) { ((Node3D)child).Hide(); child.QueueFree(); }
        PoisonVents = PoisonBacklashes = PoisonRetorts = PoisonBites = BloodSips = PoisonTickWaves = PurchasedPoisonSmokes = 0;
        _poisonPaused = false;
        _poisonSpeed = 1;
    }
    private PoisonFlowFx PoisonFlow(Vector3 from, Vector3 to, PoisonFlowFx.Kind kind, int amount)
    {
        var fx = new PoisonFlowFx(); _fxRoot.AddChild(fx);
        fx.Start(from, to, kind, amount, () => (_poisonPaused, _poisonSpeed));
        if (fx.UsesPurchasedSmoke) PurchasedPoisonSmokes++;
        return fx;
    }
    private Vector3 PoisonMouth(BattlePawn3D pawn) => pawn.MovementPortraitPoint(
        new Vector2(pawn.UnitId == "vio" ? 0.94f : 0.80f, pawn.UnitId == "vio" ? 0.44f : 0.24f)
        * new Vector2(pawn.PoisonPortraitSize.X, pawn.PoisonPortraitSize.Y), _camera);

    internal void ShowGuzaVent(BattlePawn3D guza, IReadOnlyList<BattlePawn3D> targets, int amount, double speed)
    {
        PoisonVents++; guza.VentPoison(speed);
        foreach (var target in targets) PoisonFlow(PoisonMouth(guza), target.FxPoint, PoisonFlowFx.Kind.Vent, amount);
    }
    internal void ShowPoisonRetort(BattlePawn3D source, BattlePawn3D target, int amount, bool backlash)
    {
        if (backlash) PoisonBacklashes++; else PoisonRetorts++;
        PoisonFlow(source.FxPoint, target.FxPoint, PoisonFlowFx.Kind.Retort, amount);
    }
    internal void ShowBloodSip(BattlePawn3D vio, BattlePawn3D ally, int amount)
    {
        BloodSips++;
        PoisonFlow(ally.FxPoint + Vector3.Up * 0.35f, PoisonMouth(vio), PoisonFlowFx.Kind.Blood, amount);
        ally.SwayPoisonSip();
    }
    internal void ShowPoisonBite(BattlePawn3D vio, BattlePawn3D target)
    {
        PoisonBites++;
        PoisonFlow(PoisonMouth(vio), target.FxPoint, PoisonFlowFx.Kind.Bite, vio.AttackValue);
    }
    internal PoisonFlowFx ShowPoisonDrainFlow(BattlePawn3D source, BattlePawn3D receiver)
        => PoisonFlow(source.FxPoint, PoisonMouth(receiver), PoisonFlowFx.Kind.Drain, source.PoisonIconAmount);
}
