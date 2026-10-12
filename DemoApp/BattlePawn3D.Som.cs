using Godot;
using System;

public partial class BattlePawn3D
{
    private SomVeil3D? _somVeil;
    private Tween? _somRunTween;
    private Node3D? _somRunSparks;
    private SomBeast3D? _somLegacySpirit;
    private double _somLegacySpeed = 1;
    internal bool SomBetraying { get; private set; }
    internal float SomHeight => _portraitHeight;
    internal Vector3 SomCenter => _sprite.GlobalPosition;
    internal int SomVeilAmount => _somVeil?.Amount ?? 0;
    internal void UseSomBeastProxy()
    {
        // HP・対象番号は本物の駒、絵だけは共通の寝返り／威嚇用ノードが描く。
        Show(); _sprite.Hide(); _shadow.Hide(); _ring.Hide();
        if (IsInstanceValid(_lifeTransition)) _lifeTransition!.Hide();
    }
    internal Vector3 SomBookPoint(Camera3D camera)
        => MovementPortraitPoint(new Vector2(_sprite.Texture.GetWidth() * .445f,
            _sprite.Texture.GetHeight() * (_movementPortrait == "som_summon" ? .235f : .238f)), camera)
            + camera.GlobalBasis.Z * .08f;
    private double _somExpressionSpeed = 1;
    private float _somDauntTime, _somWeakTime;
    private bool _somClockPaused;

    internal void ShowSomExpression(string key, double seconds)
    { _somExpressionSpeed = Math.Max(.1, AnimationSpeed); ShowMovementPortrait(key, seconds); }
    internal void SetSomClock(bool paused, double speed)
    {
        _somClockPaused = paused;
        AnimationSpeed = Math.Max(.1, speed);
        _somLegacySpirit?.SetClock(paused, speed);
        if (_somRunTween?.IsValid() == true)
        {
            _somRunTween.SetSpeedScale((float)(AnimationSpeed / _somLegacySpeed));
            if (paused) _somRunTween.Pause(); else _somRunTween.Play();
        }
        if (_movementPortrait?.StartsWith("som_") == true && _movementPortraitTween?.IsValid() == true)
        {
            _movementPortraitTween.SetSpeedScale((float)(speed / _somExpressionSpeed));
            if (paused) _movementPortraitTween.Pause(); else _movementPortraitTween.Play();
        }
        if (_somVeil is not null) _somVeil.ProcessMode = paused ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
    }
    internal void FlinchSomDaunt() => _somDauntTime = .3f;
    internal void WeakenSomAttack() => _somWeakTime = .65f;
    private void ProcessSomDaunt(float delta)
    {
        if (!_somClockPaused) { _somDauntTime = Math.Max(0, _somDauntTime - delta); _somWeakTime = Math.Max(0, _somWeakTime - delta); }
        if (_somDauntTime > 0)
        {
            float strength = MathF.Sin(_somDauntTime / .3f * MathF.PI);
            _sprite.Rotation += new Vector3(0, 0, (Team == 0 ? -1 : 1) * .14f * strength);
            _sprite.Position -= Vector3.Up * .08f * strength;
        }
        if (_somWeakTime > 0) _sprite.Scale *= new Vector3(.94f, .94f, 1);
    }

    internal void PlaceSomBeast(Vector3 origin)
    {
        // 敵の席・陣営・帰着位置は保ち、出現位置だけをソムの側へ移す。
        SomBetraying = true;
        Position = origin;
        _ring.Hide(); _sprite.Hide(); _shadow.Hide();
        if (IsInstanceValid(_lifeTransition)) _lifeTransition!.Hide();
        if (IsInstanceValid(_somLegacySpirit)) _somLegacySpirit!.QueueFree();
        _somLegacySpirit = new SomBeast3D { TopLevel = true }; AddChild(_somLegacySpirit);
        _somLegacySpirit.Configure(origin, _home, false, Team == 0, AnimationSpeed);
        SetShocked(true);
        if (_shockAura is not null) _shockAura.Position = Vector3.Up * _fxHeight;
    }

    internal void RunSomBeast(bool flip)
    {
        if (!SomBetraying || !_alive) return;
        Vector3 start = Position;
        Scale = Vector3.One;
        _sprite.Modulate = Colors.White;
        _somLegacySpirit?.Run(); _somLegacySpeed = Math.Max(.1, AnimationSpeed);
        _somRunSparks = new Node3D(); AddChild(_somRunSparks);
        int lastSpark = -1;
        _somRunTween = BeginMotion();
        _somRunTween.TweenMethod(Callable.From<float>(t =>
        {
            Position = start.Lerp(_home, t) + Vector3.Up * MathF.Sin(t * MathF.PI) * .28f;
            // 倍速でも体表の電気が読めるよう、走る進み具合に合わせて細い電弧を出す。
            int step = (int)(t * 6);
            if (step != lastSpark)
            {
                lastSpark = step;
                Vector3 point = GlobalPosition + new Vector3(-.22f, .25f, .25f);
                ThunderFx.Arc(_somRunSparks!, point, point + new Vector3(.42f, .26f, 0),
                    .012f, .14 / Math.Max(.1, AnimationSpeed));
            }
        }), 0f, 1f, SomFx.BeastRunSeconds / Math.Max(.1, AnimationSpeed));
        _somRunTween.TweenCallback(Callable.From(FinishSomBeastRun));
    }

    internal void FinishSomBeastRun()
    {
        if (!SomBetraying) return;
        _somRunTween?.Kill(); _somRunTween = null;
        if (IsInstanceValid(_somRunSparks)) { _somRunSparks!.Hide(); _somRunSparks.QueueFree(); }
        _somRunSparks = null;
        SomBetraying = false;
        Position = _home; Scale = Vector3.One;
        _ring.Visible = _alive;
        _somLegacySpirit?.TakeStance(false);
        ClearMovementPortrait();
    }

    internal void SetSomVeil(int amount, bool gained = false)
    {
        if (_somVeil is null && amount > 0 && PresentationAlive)
        {
            _somVeil = new SomVeil3D(); AddChild(_somVeil); _somVeil.Configure(this);
        }
        _somVeil?.SetAmount(PresentationAlive ? amount : 0, gained);
    }
    internal void RippleSomVeil() => _somVeil?.Ripple();
    internal void ClearSomPresentation()
    {
        _somClockPaused = false; _somDauntTime = _somWeakTime = 0;
        if (SomBetraying) { FinishSomBeastRun(); SetShocked(false); }
        if (IsInstanceValid(_somLegacySpirit)) { _somLegacySpirit!.Hide(); _somLegacySpirit.QueueFree(); }
        _somLegacySpirit = null;
        if (UnitId == "fodder") { _sprite.Show(); _shadow.Hide(); }
        SetSomVeil(0);
        if (_movementPortrait?.StartsWith("som_") == true || _movementPortrait?.StartsWith("fodder_") == true)
            ClearMovementPortrait();
    }
}
