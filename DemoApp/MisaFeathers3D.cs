using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 羽は在庫と飛行を分けて持つ。枚数と行き先は台本からのみ受け取り、標的を選ばない。
public partial class MisaFeathers3D : Node3D
{
    public const double ShotIntervalSeconds = 0.045;
    public const double DeploySeconds = 0.58;
    private const float ReturnSeconds = 0.52f;
    private sealed class Feather
    {
        public Sprite3D Sprite = null!;
        public Sprite3D[] Trail = Array.Empty<Sprite3D>();
        public Vector3 From, To;
        public float Time, Angle;
        public int Stage; // 0: 待機、1: 照準、2: 照射・反動、3: 帰還、4: 消失、5: 全域へ展開、6: 空中待機
        public bool Spray, WillLose;
    }

    private static Texture2D? _texture;
    private readonly List<Feather> _feathers = new();
    private Feather[] _volley = Array.Empty<Feather>();
    private BattlePawn3D _owner = null!;
    private float _phase, _direction = 1;
    private bool _active = true;
    private Vector3? _aim;
    private bool _deployed;
    private float _returnAfter = -1;
    private Vector3 _fieldCenter;
    private Vector2 _fieldRadius = new(5.55f, 2.85f);
    internal Vector3 LastMuzzle { get; private set; }
    internal int BeamCount { get; private set; }
    public int Count => _feathers.Count;
    public int VisibleCount => _feathers.Count(f => f.Sprite.Visible);
    public int InFlight => _feathers.Count(f => f.Stage is 1 or 2 or 3 or 5);
    internal Vector3[] VisiblePositions => _feathers.Where(f => f.Sprite.Visible).Select(f => f.Sprite.GlobalPosition).ToArray();
    internal bool IsDeployed => _deployed;
    internal void SetField(Vector3 center, Vector2 radius) { _fieldCenter = center; _fieldRadius = radius; }

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
            foreach (var trail in _feathers[i].Trail) trail.QueueFree();
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
            var feather = new Feather { Sprite = sprite, Trail = new Sprite3D[3] };
            for (int i = 0; i < feather.Trail.Length; i++)
            {
                var trail = new Sprite3D { Texture = _texture, PixelSize = sprite.PixelSize,
                    Shaded = false, DoubleSided = true, Visible = false,
                    Modulate = new Color(0.73f, 0.62f, 1, 0.21f / (i + 1)),
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(trail); feather.Trail[i] = trail;
            }
            sprite.Position = Home(_feathers.Count);
            if (_deployed) { feather.Stage = 5; feather.From = sprite.Position; }
            _feathers.Add(feather);
        }
        LayoutResting();
    }

    public void ConfirmLoss(int count)
    {
        SetCount(count);
        foreach (var f in _feathers.Where(f => f.Spray))
        {
            f.Stage = 6; f.Spray = false; f.Sprite.Visible = _active;
            f.Sprite.Modulate = Colors.White;
        }
        ReturnVolley();
        _volley = Array.Empty<Feather>();
        _aim = null;
    }

    public void BeginVolley(int count)
    {
        _deployed = false;
        _returnAfter = -1;
        // 直前の演出が倍速で終わり切っていなくても、台本の在庫から次の一振りを始める。
        foreach (var f in _feathers)
        {
            f.Stage = 0; f.Spray = false; f.Sprite.Visible = _active;
        }
        SetCount(count);
        _volley = _feathers.ToArray();
        _deployed = true;
        foreach (var f in _feathers)
        {
            f.From = f.Sprite.Position; f.Time = 0; f.Stage = 5;
            f.Sprite.Modulate = Colors.White;
            foreach (var trail in f.Trail) trail.Position = f.From;
        }
    }

    // 発数や命中では在庫を減らさず、残っている羽だけを手元へ帰す。
    internal void ReturnVolley()
    {
        _deployed = false; _returnAfter = -1; _aim = null;
        foreach (var f in _feathers.Where(f => f.Stage != 4 && f.Sprite.Visible))
        { f.From = f.Sprite.Position; f.Time = 0; f.Stage = 3; }
    }

    public void AimAt(Vector3 point) => _aim = point;

    public bool Launch(int shot, Vector3 destination, bool spray, bool willLose)
    {
        if (!_active || shot < 1 || shot > _volley.Length) return false;
        var f = _volley[shot - 1];
        if (!_feathers.Contains(f)) return false;
        _aim = spray ? null : destination;
        f.From = f.Sprite.Position;
        f.To = destination;
        f.Time = 0; f.Stage = 1; f.Spray = spray; f.WillLose = willLose;
        f.Sprite.Visible = true;
        f.Sprite.Modulate = Colors.White;
        return true;
    }

    internal Vector3? Fire(int shot)
    {
        if (!_active || shot < 1 || shot > _volley.Length) return null;
        var f = _volley[shot - 1];
        if (!_feathers.Contains(f) || f.Stage != 1) return null;
        f.Sprite.Position = GunPosition(shot - 1, f.Spray);
        var direction = (f.To - f.Sprite.GlobalPosition).Normalized();
        LastMuzzle = f.Sprite.GlobalPosition + direction * 0.30f;
        BeamCount++;
        f.Stage = 2; f.Time = 0;
        if (shot == _volley.Length) _returnAfter = 0.25f;
        return LastMuzzle;
    }

    private Vector3 GunPosition(int index, bool spray)
    {
        // 全域の楕円をゆっくり巡回。連射の順は左右・奥行きの異なる位置を渡る。
        float angle = index * 2.399963f + _phase * (spray ? 0.34f : 0.12f);
        float lane = 0.84f + index % 3 * 0.08f;
        var world = _fieldCenter + new Vector3(Mathf.Cos(angle) * _fieldRadius.X * lane * _direction,
            2.45f + index % 3 * 0.30f + Mathf.Sin(_phase * 1.1f + index) * 0.16f,
            Mathf.Sin(angle) * _fieldRadius.Y * lane);
        // 視点を寄せても羽だけが見切れないよう、画面の余白へ収める。
        if (GetViewport().GetCamera3D() is { } camera)
        {
            Vector2 size = GetViewport().GetVisibleRect().Size;
            Vector2 screen = camera.UnprojectPosition(world);
            screen = screen.Clamp(size * new Vector2(0.07f, 0.18f), size * new Vector2(0.93f, 0.82f));
            float depth = (world - camera.GlobalPosition).Dot(-camera.GlobalBasis.Z);
            world = camera.ProjectPosition(screen, depth);
        }
        return ToLocal(world);
    }

    public void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active; Visible = active; _aim = null;
        _deployed = false; _returnAfter = -1;
        _volley = Array.Empty<Feather>();
        foreach (var f in _feathers)
        {
            f.Stage = 0; f.Spray = false; f.Sprite.Visible = active;
            f.Sprite.Modulate = Colors.White;
            foreach (var trail in f.Trail) trail.Hide();
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
        if (_returnAfter >= 0)
        {
            _returnAfter -= dt;
            if (_returnAfter <= 0) ReturnVolley();
        }
        for (int i = 0; i < Count; i++)
        {
            var f = _feathers[i];
            UpdateTrail(f, dt);
            if (f.Stage == 4)
            {
                // 「失った」が無い一振りでは在庫が残る。着弾後に手元へ再出現させる。
                // 下限や特性から残数を計算せず、台本に減少があるかだけで描き分ける。
                f.Time += dt;
                if (f.WillLose || f.Time < 0.30f) continue;
                f.Stage = 0; f.Spray = false; f.Sprite.Visible = true;
            }
            if (f.Stage is 0 or 6)
            {
                Vector3 home = f.Stage == 6 ? GunPosition(i, f.Spray) : Home(i);
                home += new Vector3(Mathf.Cos(_phase * 1.2f + i) * 0.07f, Mathf.Sin(_phase * 1.8f + i * 2.1f) * 0.06f, 0);
                f.Sprite.Position = f.Sprite.Position.Lerp(home, Math.Min(1, dt * 7));
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
            float t = Mathf.Clamp(f.Time / (float)(f.Stage == 5 ? DeploySeconds : f.Stage == 1 ? ShotIntervalSeconds : ReturnSeconds), 0, 1);
            var direction = GlobalBasis.Inverse() * (f.To - f.Sprite.GlobalPosition);
            if (direction.LengthSquared() > 0.0001f)
                f.Sprite.Rotation = new Vector3(0, 0, Mathf.Atan2(direction.Y, direction.X));
            if (f.Stage == 5)
            {
                Vector3 destination = GunPosition(i, false);
                f.Sprite.Position = f.From.Lerp(destination, t * t * (3 - 2 * t))
                    + new Vector3((i % 2 == 0 ? 1 : -1) * 0.40f, 0.80f, 0.12f) * Mathf.Sin(t * Mathf.Pi);
                Vector3 travel = destination - f.From;
                f.Sprite.Rotation = new Vector3(0, 0, Mathf.Atan2(travel.Y, travel.X));
                if (t >= 1) { f.Stage = 6; f.Time = 0; }
            }
            else if (f.Stage == 1)
            {
                f.Sprite.Position = f.From.Lerp(GunPosition(i, f.Spray), Mathf.Sin(t * Mathf.Pi * 0.5f));
                f.Sprite.Modulate = Colors.White.Lerp(new Color("dfcdff"), t * 0.5f);
            }
            else if (f.Stage == 2)
            {
                f.Sprite.Position -= direction.Normalized() * dt * 0.25f;
                if (f.Time < 0.12f) continue;
                f.Stage = f.Spray && f.WillLose ? 4 : 6;
                f.Time = 0; f.From = f.Sprite.Position;
                if (f.Stage == 4)
                {
                    ShockMarkFx.Sparks(this, f.Sprite.GlobalPosition, ShockMarkFx.Feather, 9, 0.55f, 0.35 / _owner.AnimationSpeed);
                    f.Sprite.Visible = false;
                }
            }
            else
            {
                f.Sprite.Position = f.From.Lerp(Home(i), t * t * (3 - 2 * t))
                    + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * 0.40f;
                if (t >= 1) { f.Stage = 0; f.Time = 0; f.Spray = false; f.Sprite.Modulate = Colors.White; }
            }
        }
    }

    private static void UpdateTrail(Feather f, float dt)
    {
        Vector3 previous = f.Sprite.Position;
        foreach (var trail in f.Trail)
        {
            var before = trail.Position;
            trail.Position = trail.Position.Lerp(previous, Math.Min(1, dt * 20));
            trail.Rotation = f.Sprite.Rotation;
            trail.Visible = f.Sprite.Visible && f.Stage is 3 or 5 && trail.Position.DistanceTo(f.Sprite.Position) > 0.08f;
            previous = before;
        }
    }
}
