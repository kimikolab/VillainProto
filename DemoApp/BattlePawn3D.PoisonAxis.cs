using BattleCore;
using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlePawn3D
{
    private bool _poisonCloak, _poisonProof, _poisonPaused, _poisonStopped;
    private double _poisonSpeed = 1, _poisonJumpAge, _poisonJumpSeconds;
    private double _poisonPoseSpeed = 1;
    private Vector3 _poisonJumpFrom, _poisonJumpTo;
    private TaskCompletionSource<bool>? _poisonJumpCompletion;
    private float _poisonSipAge = 1;
    internal int VioGrowthStage { get; private set; }
    internal Vector2 PoisonPortraitSize => _sprite.Texture.GetSize();
    internal bool PoisonDoomVisible => _poison.Lethal;
    internal bool PoisonMantleVisible => _poison.Mantle;
    internal int PoisonBurstWaves => _poison.BurstWaves;

    private void SyncPoisonVisual()
    {
        if (_sprite is null || _statusIcons is null || !_alive || _victory || _poisonStopped) return;
        _poison.Track(_sprite, PoisonIconAmount, _poisonCloak,
            Team == BattleContext.EnemyTeam && !_poisonProof && Hp > 0 && PoisonIconAmount >= Hp,
            _unitId == "guza");
    }

    internal void SetPoisonClock(bool paused, double speed)
    {
        _poisonPaused = paused; _poisonSpeed = Math.Max(0.1, speed);
        _poison.Paused = paused; _poison.Speed = _poisonSpeed;
        if (_unitId is "guza" or "vio" or "rau" && _movementPortraitTween?.IsValid() == true)
        {
            _movementPortraitTween.SetSpeedScale((float)(_poisonSpeed / Math.Max(0.1, _poisonPoseSpeed)));
            if (paused) _movementPortraitTween.Pause(); else _movementPortraitTween.Play();
        }
    }

    internal void BurstPoison(double seconds, int waves = 1) { SyncPoisonVisual(); _poison.Burst(seconds, waves); }
    internal void VentPoison(double speed)
    {
        AnimationSpeed = _poisonPoseSpeed = speed;
        ShowMovementPortrait("guza_vent", 0.60);
    }
    internal void RauPose(bool spreading, double speed)
    {
        if (_unitId != "rau") return;
        AnimationSpeed = _poisonPoseSpeed = speed;
        ShowMovementPortrait(spreading ? "rau_spread" : "rau_touch", spreading ? 0.90 : 0.65);
        SetPoisonClock(_poisonPaused, speed);
    }
    internal void SwayPoisonSip() => _poisonSipAge = 0;
    private void UpdateVioGrowth()
    {
        if (_unitId != "vio") return;
        int next = AttackValue >= 150 ? 2 : AttackValue >= 50 ? 1 : 0;
        if (next == VioGrowthStage) return;
        VioGrowthStage = next;
        RefreshBattlePortrait();
    }

    // 次の的は台本のAttackだけが決める。前の的に留まった連撃でも、毎回その的へ跳ぶ。
    internal Task<bool> PoisonPounce(Vector3 target)
    {
        if (!_alive || _victory) return Task.FromResult(false);
        _poisonJumpCompletion?.TrySetResult(false);
        _motion?.Kill();
        Vector3 planar = new(target.X - Position.X, 0, target.Z - Position.Z);
        _poisonJumpFrom = Position;
        _poisonJumpTo = planar.Length() > 1.7f ? target - planar.Normalized() * 1.7f : Position;
        _poisonJumpTo.Y = _home.Y;
        _poisonJumpAge = 0; _poisonJumpSeconds = 0.30;
        _poisonJumpCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return _poisonJumpCompletion.Task;
    }

    private void ProcessPoisonMotion(float delta)
    {
        if (_poisonSipAge < 0.34f)
        {
            if (!_poisonPaused) _poisonSipAge = Math.Min(0.34f, _poisonSipAge + delta * (float)_poisonSpeed);
            _sprite.Rotation += Vector3.Back * Mathf.Sin(_poisonSipAge / 0.34f * Mathf.Pi) * 0.07f * (Team == 0 ? 1 : -1);
        }
        if (_poisonPaused) return;
        if (_poisonJumpAge >= _poisonJumpSeconds) return;
        _poisonJumpAge = Math.Min(_poisonJumpSeconds, _poisonJumpAge + delta * _poisonSpeed);
        float t = (float)(_poisonJumpAge / _poisonJumpSeconds);
        Position = _poisonJumpFrom.Lerp(_poisonJumpTo, Mathf.SmoothStep(0, 1, t))
            + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * 0.75f;
        if (_poisonJumpAge >= _poisonJumpSeconds) { _poisonJumpCompletion?.TrySetResult(true); _poisonJumpCompletion = null; }
    }
    internal void ClearPoisonAxis()
    {
        _poisonJumpCompletion?.TrySetResult(false); _poisonJumpCompletion = null;
        _poisonJumpAge = _poisonJumpSeconds;
        _poisonSipAge = 1;
        _poison.Clear();
        if (_unitId is "guza" or "vio" or "rau") ClearMovementPortrait();
    }
    internal void StopPoisonPresentation() { _poisonStopped = true; ClearPoisonAxis(); }
}
