using Godot;
using System;

public partial class BattlePawn3D
{
    private SomVeil3D? _somVeil;
    private Tween? _somRunTween;
    private Node3D? _somRunSparks;
    internal bool SomBetraying { get; private set; }
    internal float SomHeight => _portraitHeight;
    internal Vector3 SomCenter => _sprite.GlobalPosition;
    internal int SomVeilAmount => _somVeil?.Amount ?? 0;

    internal void PlaceSomBeast(Vector3 origin)
    {
        // 敵の席・陣営・帰着位置は保ち、出現位置だけをソムの側へ移す。
        SomBetraying = true;
        Position = origin;
        _ring.Hide();
        if (IsInstanceValid(_lifeTransition)) _lifeTransition!.GlobalPosition = GlobalPosition;
        SetShocked(true);
        if (_shockAura is not null) _shockAura.Position = Vector3.Up * _fxHeight;
    }

    internal void RunSomBeast(bool flip)
    {
        if (!SomBetraying || !_alive) return;
        Vector3 start = Position;
        Scale = Vector3.One;
        _sprite.Modulate = Colors.White;
        ShowMovementPortrait("fodder_run", SomFx.BeastRunSeconds + .15, flip: flip);
        _somRunSparks = new Node3D(); AddChild(_somRunSparks);
        int lastSpark = -1;
        _somRunTween = BeginMotion();
        _somRunTween.TweenMethod(Callable.From<float>(t =>
        {
            Position = start.Lerp(_home, t) + Vector3.Up * (MathF.Abs(MathF.Sin(t * MathF.PI * 4)) * .12f);
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
        if (SomBetraying) { FinishSomBeastRun(); SetShocked(false); }
        SetSomVeil(0);
        if (_movementPortrait?.StartsWith("som_") == true || _movementPortrait?.StartsWith("fodder_") == true)
            ClearMovementPortrait();
    }
}
