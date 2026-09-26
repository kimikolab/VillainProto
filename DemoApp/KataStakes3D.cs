using Godot;
using System;

// 本体から分離した3本を保持する。発射は同じスプライトを動かし、二重に描かない。
public partial class KataStakes3D : Node3D
{
    private readonly Sprite3D[] _stakes = new Sprite3D[3];
    private readonly Tween?[] _flights = new Tween?[3];
    private readonly bool[] _away = new bool[3];
    private int _next;
    private float _phase, _sparkClock;
    private float _direction = 1;
    private bool _active = true;
    public int Available => Array.FindAll(_away, x => !x).Length;
    private Vector3 Home(int i) => new Vector3((i == 0 ? -0.47f : i == 1 ? 0.32f : 0.72f) * _direction,
        i == 0 ? 1.96f : i == 1 ? 2.40f : 2.02f, 0.04f);

    public void Configure(bool enemy)
    {
        _direction = enemy ? -1 : 1;
        for (int i = 0; i < 3; i++)
        {
            var sprite = new Sprite3D { Texture = ThunderFx.Stake,
                PixelSize = 0.85f / ThunderFx.Stake.GetHeight(), Shaded = false,
                Position = Home(i), Rotation = new Vector3(0,0,0.43f * _direction),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(sprite); _stakes[i] = sprite;
        }
    }

    public void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active; Visible = active;
        for (int i = 0; i < 3; i++)
        {
            _flights[i]?.Kill(); _flights[i] = null; _away[i] = false;
            _stakes[i].Position = Home(i);
            _stakes[i].Rotation = new Vector3(0,0,0.43f * _direction);
        }
    }

    public bool Launch(Vector3 target, double speed)
    {
        if (!_active) return false;
        int index = -1;
        for (int k = 0; k < 3; k++)
        {
            int i = (_next + k) % 3;
            if (!_away[i]) { index = i; break; }
        }
        if (index < 0) return false;
        _next = (index + 1) % 3;
        _away[index] = true;
        var stake = _stakes[index];
        var from = stake.GlobalPosition;
        var destination = target + Vector3.Up * 0.18f;
        var tween = stake.CreateTween(); _flights[index] = tween;
        tween.TweenMethod(Callable.From<float>(t => {
            stake.GlobalPosition = from.Lerp(destination,t);
            stake.Rotation = new Vector3(0,0,Mathf.Lerp(0.43f * _direction,Mathf.Pi,t));
        }),0f,1f,0.18/speed);
        tween.TweenInterval(0.34/speed);
        tween.TweenMethod(Callable.From<float>(t => {
            stake.GlobalPosition = destination.Lerp(ToGlobal(Home(index)),t);
            stake.Rotation = new Vector3(0,0,Mathf.Lerp(Mathf.Pi,0.43f * _direction,t));
        }),0f,1f,0.20/speed);
        tween.TweenCallback(Callable.From(() => { _away[index] = false; _flights[index] = null; }));
        return true;
    }

    public override void _Process(double delta)
    {
        if (!_active) return;
        if (GetViewport().GetCamera3D() is { } camera) GlobalBasis = camera.GlobalBasis;
        _phase += (float)delta;
        for(int i=0;i<3;i++) if(!_away[i])
            _stakes[i].Position = Home(i) + Vector3.Up * Mathf.Sin(_phase * 1.8f + i*2) * 0.035f;
        _sparkClock -= (float)delta;
        if(_sparkClock > 0) return;
        _sparkClock = 0.43f;
        int n = (int)(_phase * 3) % 3;
        if (!_away[n]) ThunderFx.Arc(this, _stakes[n].GlobalPosition,
            _stakes[n].GlobalPosition + GlobalBasis.Y * 0.22f + GlobalBasis.X * 0.07f,0.009f,0.12);
    }
}
