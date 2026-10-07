using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 羽は在庫と飛行を分けて持つ。枚数と行き先は台本からのみ受け取り、標的を選ばない。
public partial class MisaFeathers3D : Node3D
{
    public const double TravelSeconds = 0.22;
    private sealed class Feather
    {
        public Sprite3D Sprite = null!;
        public Vector3 From, To;
        public float Time, Angle;
        public int Stage; // 0: 待機、1: 射出、2: 帰還、3: 乱射で消失
        public bool Spray, WillLose;
    }

    private static Texture2D? _texture;
    private readonly List<Feather> _feathers = new();
    private Feather[] _volley = Array.Empty<Feather>();
    private BattlePawn3D _owner = null!;
    private float _phase, _direction = 1;
    private bool _active = true;
    private Vector3? _aim;
    public int Count => _feathers.Count;
    public int VisibleCount => _feathers.Count(f => f.Sprite.Visible);
    public int InFlight => _feathers.Count(f => f.Stage is 1 or 2);

    public void Configure(BattlePawn3D owner, float height)
    {
        _owner = owner;
        _direction = owner.Team == 0 ? 1 : -1;
        Position = Vector3.Up * height * 0.51f;
        _texture ??= UiKit.LoadTexture("res://assets/fx/misa_feather.png", mipmaps: true);
        SetCount(1);
    }

    public void SetCount(int count)
    {
        count = Math.Max(1, count);
        // 戻らない羽を先に取り除く。最後の1枚は台本の下限として復帰する。
        while (_feathers.Count > count)
        {
            int i = _feathers.FindLastIndex(f => f.Spray);
            if (i < 0) i = _feathers.Count - 1;
            _feathers[i].Sprite.QueueFree();
            _feathers.RemoveAt(i);
        }
        while (_feathers.Count < count)
        {
            var sprite = new Sprite3D {
                Texture = _texture, PixelSize = 0.88f / _texture!.GetWidth(),
                Shaded = false, DoubleSided = true,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(sprite);
            _feathers.Add(new Feather { Sprite = sprite });
        }
        LayoutResting();
    }

    public void ConfirmLoss(int count)
    {
        SetCount(count);
        foreach (var f in _feathers.Where(f => f.Spray))
        {
            f.Stage = 0; f.Spray = false; f.Sprite.Visible = _active;
            f.Sprite.Modulate = Colors.White;
        }
        _volley = Array.Empty<Feather>();
        _aim = null;
        LayoutResting();
    }

    public void BeginVolley(int count)
    {
        // 直前の演出が倍速で終わり切っていなくても、台本の在庫から次の一振りを始める。
        foreach (var f in _feathers)
        {
            f.Stage = 0; f.Spray = false; f.Sprite.Visible = _active;
        }
        SetCount(count);
        _volley = _feathers.ToArray();
    }

    public void AimAt(Vector3 point) => _aim = point;

    public bool Launch(int shot, Vector3 destination, bool spray, bool willLose)
    {
        if (!_active || shot < 1 || shot > _volley.Length) return false;
        var f = _volley[shot - 1];
        if (!_feathers.Contains(f)) return false;
        _aim = spray ? null : destination;
        f.From = f.Sprite.GlobalPosition;
        f.To = destination;
        f.Time = 0; f.Stage = 1; f.Spray = spray; f.WillLose = willLose;
        f.Sprite.Visible = true;
        f.Sprite.Modulate = Colors.White;
        return true;
    }

    public void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active; Visible = active; _aim = null;
        _volley = Array.Empty<Feather>();
        foreach (var f in _feathers)
        {
            f.Stage = 0; f.Spray = false; f.Sprite.Visible = active;
            f.Sprite.Modulate = Colors.White;
        }
        LayoutResting();
    }

    private Vector3 Home(int index)
    {
        int tier = index / 6, level = index % 6 / 2;
        float side = index % 2 == 0 ? -1 : 1;
        return new Vector3(side * (0.78f + level * 0.21f + tier * 0.14f),
            0.02f + level * 0.33f + tier * 0.10f, -0.13f - tier * 0.04f);
    }

    private void LayoutResting()
    {
        for (int i = 0; i < Count; i++)
        {
            var f = _feathers[i];
            if (f.Stage != 0) continue;
            f.Sprite.Position = Home(i);
            f.Angle = _direction > 0 ? 0.30f : Mathf.Pi - 0.30f;
            f.Sprite.Rotation = new Vector3(0, 0, f.Angle);
        }
    }

    public override void _Process(double delta)
    {
        if (!_active) return;
        if (GetViewport().GetCamera3D() is { } camera) GlobalBasis = camera.GlobalBasis;
        float dt = (float)(delta * _owner.AnimationSpeed);
        _phase += dt;
        for (int i = 0; i < Count; i++)
        {
            var f = _feathers[i];
            if (f.Stage == 3)
            {
                // 「失った」が無い一振りでは在庫が残る。着弾後に手元へ再出現させる。
                // 下限や特性から残数を計算せず、台本に減少があるかだけで描き分ける。
                f.Time += dt;
                if (f.WillLose || f.Time < 0.30f) continue;
                f.Stage = 0; f.Spray = false; f.Sprite.Visible = true;
            }
            if (f.Stage == 0)
            {
                f.Sprite.Position = Home(i) + Vector3.Up * Mathf.Sin(_phase * 1.8f + i * 2.1f) * 0.025f;
                float angle = _direction > 0 ? 0.30f : Mathf.Pi - 0.30f;
                if (_aim is { } aim)
                {
                    var d = ToLocal(aim) - f.Sprite.Position;
                    angle = Mathf.Atan2(d.Y, d.X);
                }
                f.Angle = Mathf.LerpAngle(f.Angle, angle, Math.Min(1, dt * 10));
                f.Sprite.Rotation = new Vector3(0, 0, f.Angle);
                continue;
            }
            f.Time += dt;
            float t = Mathf.Clamp(f.Time / (float)(f.Stage == 1 ? TravelSeconds : 0.30), 0, 1);
            Vector3 to = f.Stage == 1 ? f.To : ToGlobal(Home(i));
            Vector3 bend = GlobalBasis.Y * Mathf.Sin(t * Mathf.Pi) * (f.Spray ? 0.60f : 0.16f);
            Vector3 next = f.From.Lerp(to, t) + bend;
            var direction = GlobalBasis.Inverse() * (to - f.Sprite.GlobalPosition);
            if (direction.LengthSquared() > 0.0001f)
                f.Sprite.Rotation = new Vector3(0, 0, Mathf.Atan2(direction.Y, direction.X));
            f.Sprite.GlobalPosition = next;
            if (t < 1) continue;
            if (f.Stage == 1 && f.Spray)
            {
                f.Stage = 3;
                f.Time = 0;
                f.Sprite.Visible = false;
            }
            else if (f.Stage == 1)
            {
                f.Stage = 2; f.Time = 0; f.From = next;
            }
            else
            {
                f.Stage = 0; f.Time = 0;
                if (!_feathers.Any(x => x.Stage is 1 or 2)) _aim = null;
            }
        }
    }
}
