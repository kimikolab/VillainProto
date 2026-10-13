using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private Node3D? _touPowderRoot;
    private readonly Dictionary<int, TouPowderAura3D> _powderAuras = new();
    private readonly List<TouPowderFx3D> _powderEffects = new();
    private bool _powderPaused;
    private double _powderSpeed = 1;
    internal Task<bool> PowderArrival { get; private set; } = Task.FromResult(true);
    internal bool UsePurchasedPowderFx { get; set; } = true;
    internal int PowderDensePlays, PowderFlowPlays, PowderMovePlays, PowderPopPlays, PowderBurnPlays, PowderMarkPlays, PowderContagionPlays;
    internal int PowderHeavyPlays;
    internal int ActivePowderEffects => _powderEffects.Count(f => IsInstanceValid(f) && !f.IsQueuedForDeletion());
    internal IEnumerable<TouPowderFx3D> PowderEffects => _powderEffects.Where(f => IsInstanceValid(f) && !f.IsQueuedForDeletion());

    private Node3D PowderRoot
    {
        get
        {
            if (_touPowderRoot is null || !IsInstanceValid(_touPowderRoot))
            { _touPowderRoot = new Node3D(); _fxRoot.AddChild(_touPowderRoot); }
            return _touPowderRoot;
        }
    }

    internal void SetTouPowderClock(bool paused, double speed)
    {
        _powderPaused = paused; _powderSpeed = Math.Max(.1, speed);
        _attackAudio.SetPowderBurnClock(paused, speed);
        _attackAudio.SetDustCrackleClock(paused, speed);
        DustChain?.SetClock(paused, speed);
        _powderEffects.RemoveAll(f => !IsInstanceValid(f) || f.IsQueuedForDeletion());
        foreach (var fx in _powderEffects) fx.SetClock(paused, _powderSpeed);
        foreach (var aura in _powderAuras.Values)
            if (IsInstanceValid(aura)) aura.SetClock(paused, _powderSpeed);
    }

    private void EndTouPowder()
    {
        _attackAudio.StopPowderBurn();
        _attackAudio.StopDustCrackle(); DustChain?.Cancel(); _dustChain = null;
        DustBurstCompletion = Task.FromResult(true); _ballPowder.Clear();
        foreach (var fx in PowderEffects.ToArray()) fx.Cancel();
        if (_touPowderRoot is not null && IsInstanceValid(_touPowderRoot))
        { _touPowderRoot.Hide(); _touPowderRoot.QueueFree(); }
        _touPowderRoot = null; _powderAuras.Clear(); _powderEffects.Clear(); _powderPaused = false;
        PowderArrival = Task.FromResult(true);
        foreach (var pawn in _pawns.Values) pawn.ClearTouPowder();
    }

    private void ResetTouPowder()
    {
        EndTouPowder();
        PowderDensePlays = PowderFlowPlays = PowderMovePlays = PowderPopPlays = PowderBurnPlays = PowderMarkPlays = PowderContagionPlays = PowderHeavyPlays = 0;
        DustBurstPlays = DustChainPlays = DustDamageSummaries = 0;
    }

    private void PowderAura(BattlePawn3D target)
    {
        if (_powderAuras.ContainsKey(target.InstanceId)) return;
        var aura = new TouPowderAura3D(); PowderRoot.AddChild(aura);
        aura.Configure(target, _camera); aura.SetClock(_powderPaused, _powderSpeed);
        _powderAuras.Add(target.InstanceId, aura);
    }

    private TouPowderFx3D PowderEffect(TouPowderFxKind kind, Vector3 from, Vector3 to, double speed, int amount = 1)
    {
        _powderEffects.RemoveAll(f => !IsInstanceValid(f) || f.IsQueuedForDeletion());
        var fx = new TouPowderFx3D(); PowderRoot.AddChild(fx);
        fx.Configure(kind, from, to, _camera, amount, UsePurchasedPowderFx);
        fx.SetClock(_powderPaused, speed); _powderEffects.Add(fx);
        return fx;
    }

    internal void LiftThunderPowder(BattlePawn3D? actor, double speed)
    {
        if (actor is null) return;
        PowderEffect(TouPowderFxKind.Lift, actor.FxPoint, actor.FxPoint + Vector3.Up * 4, speed, actor.PowderDensity);
    }

    internal bool ShowTouPowder(BattleEvent e, double speed, bool scheduledDust = false)
    {
        if (e.Kind != BattleEventKind.ShockGauge) return false;
        if (_powderSpeed != speed) SetTouPowderClock(_powderPaused, speed);
        PowderArrival = Task.FromResult(true);
        var actor = FindPawn(e.ActorId); var target = FindPawn(e.TargetId);
        switch (e.Text)
        {
            case ShockGaugeLabels.Dense:
                PowderDensePlays++;
                if (e.TargetId is int denseBall) BallPowder(denseBall, e.Amount);
                if (target is not null)
                {
                    target.SetPowderDensity(e.Amount); PowderAura(target);
                    PowderArrival = PowderEffect(TouPowderFxKind.Settle, target.FxPoint + Vector3.Up * .7f, target.FxPoint, speed, e.Amount).Arrival;
                }
                break;
            case ShockGaugeLabels.PowderMark:
                PowderMarkPlays++;
                if (e.TargetId is int markedBall) BallPowder(markedBall, marked: true);
                if (target is not null) { target.SetPowderMark(); PowderAura(target); }
                break;
            case ShockGaugeLabels.DenseFlow:
                PowderFlowPlays++;
                // 今回の粉を流す通知。以前に積もった濃さを消す通知ではない。
                if (actor is not null && target is not null)
                {
                    if (FindPawn(e.PartnerId) is { } tou)
                        PowderEffect(TouPowderFxKind.Settle, tou.FxPoint, actor.FxPoint, speed);
                    // 三本の拘束糸と同じ端点・垂れ方で金色の粉を流す。
                    PowderArrival = PowderEffect(TouPowderFxKind.Thread, actor.FxPoint, target.FxPoint, speed, e.Amount).Arrival;
                }
                break;
            case ShockGaugeLabels.DenseMove:
                PowderMovePlays++; actor?.SetPowderDensity(0);
                if (target is not null)
                {
                    target.SetPowderDensity(e.Amount); PowderAura(target);
                    PowderEffect(TouPowderFxKind.Settle, target.FxPoint + Vector3.Up * .7f, target.FxPoint, speed, e.Amount);
                }
                break;
            case ShockGaugeLabels.DensePop:
                PowderPopPlays++;
                if (scheduledDust || DustChain is not null) break;
                if (target is not null)
                    PowderEffect(TouPowderFxKind.Pop, target.DischargePoint, target.DischargePoint, speed, e.Amount);
                break;
            case ShockGaugeLabels.DenseBurn:
                PowderBurnPlays++;
                if (scheduledDust || DustChain is not null) break; // 爆竹の束へ重ねて大きな閃光を出さない。
                if (target is not null)
                {
                    // 同じ本人の発火が続いても、古い光と札を重ねて白い塊にしない。
                    foreach (var old in PowderEffects.Where(f => f.Kind == TouPowderFxKind.Burn
                        && f.BurnPawnId == target.InstanceId).ToArray()) old.Cancel();
                    var burn = PowderEffect(TouPowderFxKind.Burn, target.DischargePoint, target.DischargePoint, speed, e.Slot);
                    burn.AttachBurnBody(target);
                    PowderArrival = burn.Arrival;
                    float pan = Math.Clamp((_camera.UnprojectPosition(target.DischargePoint).X / _viewport.Size.X - .5f) * 1.4f, -.7f, .7f);
                    _attackAudio.PlayPowderBurn(e.Slot, pan, speed);
                }
                break;
            case ShockGaugeLabels.DustBurst:
                DustBurstPlays++;
                if (!scheduledDust && DustChain is null)
                {
                    // 単独通知にも同じ演出を出す。連鎖の着弾は先読み側で接続する。
                    var one = new DustBurstPresentation.Chain(0, 0, Array.Empty<int>(),
                        new[] { new DustBurstPresentation.Burst(0, Array.Empty<int>()) }, new Dictionary<int, int>());
                    BeginDustBurstChain(one, new[] { e }, speed);
                }
                break;
            case ShockGaugeLabels.Contagion:
                PowderContagionPlays++;
                if (actor is not null && target is not null)
                    PowderArrival = PowderEffect(TouPowderFxKind.Contagion, actor.DischargePoint, target.DischargePoint, speed).Arrival;
                break;
            default: return false;
        }
        return true;
    }

    internal void FinishPowderFlow(BattleEvent e)
    {
        if (e.Text != ShockGaugeLabels.DenseFlow || FindPawn(e.TargetId) is not { } target) return;
        target.SetPowderDensity(e.Amount); PowderAura(target);
    }

    internal bool ShowHeavyPowderDischarge(BattleEvent e, double speed)
    {
        // 太さは台本に書かれた放電量だけを読む（8は通常の描画基準）。
        if (e.SourceTrait == TraitId.Thread || e.Amount <= 8) return false;
        if (ElectricPoint(e.ActorId) is not Vector3 from || ElectricPoint(e.TargetId) is not Vector3 to) return false;
        PowderHeavyPlays++;
        PowderEffect(TouPowderFxKind.Heavy, from, to, speed, e.Amount);
        return true;
    }
}
