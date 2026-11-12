using Godot;
using System;

// すべて再生時計で進める。TIMEシェーダー・自動反復・独立Tweenを使わない。
public abstract partial class DebuffFx3D : Node3D
{
    internal static readonly Color Tint = new("bd9aed");
    protected Func<(bool Paused, double Speed)> Clock = () => (false, 1);
    internal double Age { get; private set; }
    protected static bool Live(BattlePawn3D? pawn) => IsInstanceValid(pawn) && pawn!.IsInsideTree() && pawn.CanReceiveMovementImpact;
    protected bool Advance(double delta)
    {
        var (paused, speed) = Clock();
        if (paused) return false;
        Age += delta * speed;
        return true;
    }
    protected void End() { Hide(); QueueFree(); }
    protected Sprite3D Sprite(string path, float height, Color tint)
    {
        var texture = GD.Load<Texture2D>(path);
        var sprite = new Sprite3D { Texture = texture, PixelSize = height / texture.GetHeight(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false,
            NoDepthTest = true, Modulate = tint, RenderPriority = 8,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(sprite);
        return sprite;
    }
}

public partial class DebuffThread3D : DebuffFx3D
{
    private BattlePawn3D _from = null!, _to = null!;
    private Camera3D _camera = null!;
    private double _duration;
    private readonly ImmediateMesh _mesh = new();
    private MeshInstance3D _ribbon = null!;
    private Sprite3D _sigil = null!, _spark = null!;

    internal void Start(BattlePawn3D from, BattlePawn3D to, Camera3D camera,
        Func<(bool, double)> clock, double duration)
    {
        _from = from; _to = to; _camera = camera; Clock = clock; _duration = duration;
        _ribbon = new MeshInstance3D { Mesh = _mesh, MaterialOverride = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true, RenderPriority = 7,
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
        }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 15 };
        AddChild(_ribbon);
        _sigil = Sprite("res://assets/fx/debuff_dull.svg", .28f, Tint);
        // 購入済みEffectBlocksの光粒を先端に1枚だけ。原本・共有リソースは変えない。
        _spark = new Sprite3D { Texture = HagiLight3D.Sparkle, PixelSize = .24f / HagiLight3D.Sparkle.GetWidth(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false, NoDepthTest = true,
            Modulate = new Color(Tint, .65f), RenderPriority = 9 };
        AddChild(_spark);
        Draw();
    }
    public override void _Process(double delta)
    {
        if (!Live(_from) || !Live(_to)) { End(); return; }
        if (!Advance(delta)) return;
        if (Age >= _duration) { End(); return; }
        Draw();
    }
    private void Draw()
    {
        float t = (float)Math.Min(1, Age / _duration);
        Vector3 from = _from.FxPoint + Vector3.Down * .32f, to = _to.FxPoint + Vector3.Down * .32f;
        Vector3 Point(float u) => from.Lerp(to, u) + Vector3.Up * Mathf.Sin(u * Mathf.Pi) * .22f;
        float head = Mathf.SmoothStep(0, 1, t), tail = Math.Max(0, head - .28f);
        _sigil.GlobalPosition = Point(head); _spark.GlobalPosition = Point(head);
        _sigil.Scale = Vector3.One * (.72f + Mathf.Sin(t * Mathf.Pi) * .3f);
        _mesh.ClearSurfaces();
        if (head - tail < .001f) return;
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        void V(Vector3 p, float alpha) { _mesh.SurfaceSetColor(new Color(Tint, alpha)); _mesh.SurfaceAddVertex(ToLocal(p)); }
        for (int i = 0; i < 18; i++)
        {
            float u = i / 18f, v = (i + 1) / 18f;
            Vector3 a = Point(Mathf.Lerp(tail, head, u)), b = Point(Mathf.Lerp(tail, head, v));
            Vector3 side = (b - a).Cross(_camera.GlobalPosition - a).Normalized() * .016f;
            V(a - side, u * .8f); V(b - side, v * .8f); V(a + side, u * .8f);
            V(a + side, u * .8f); V(b - side, v * .8f); V(b + side, v * .8f);
        }
        _mesh.SurfaceEnd();
    }
}

public partial class DebuffSigil3D : DebuffFx3D
{
    private BattlePawn3D _pawn = null!;
    private Sprite3D _sigil = null!;
    private bool _cost;
    internal void Start(BattlePawn3D pawn, Camera3D camera, Func<(bool, double)> clock, bool cost)
    {
        _pawn = pawn; Clock = clock; _cost = cost;
        _sigil = Sprite("res://assets/fx/debuff_dull.svg", cost ? .46f : .6f,
            cost ? new Color("b87baf") : Tint);
        Draw();
    }
    public override void _Process(double delta)
    {
        if (!Live(_pawn)) { End(); return; }
        if (!Advance(delta)) return;
        if (Age >= .32) { End(); return; }
        Draw();
    }
    private void Draw()
    {
        float t = (float)(Age / .32);
        _sigil.GlobalPosition = _pawn.FxPoint + Vector3.Down * (.38f + t * .2f);
        _sigil.Scale = new Vector3(1 - t * .3f, 1 - Mathf.Sin(t * Mathf.Pi) * (_cost ? .5f : .25f), 1);
        _sigil.Modulate = new Color(_cost ? new Color("b87baf") : Tint, 1 - t);
    }
}

public partial class DebuffLayers3D : DebuffFx3D
{
    private BattlePawn3D _pawn = null!;
    private Camera3D _camera = null!;
    private readonly Sprite3D[] _blades = new Sprite3D[5];
    private readonly float[] _amounts = new float[5];
    private bool _over;
    private int _strike = -1;
    private double _strikeAt = -1;
    internal int Count { get; private set; }

    internal void Start(BattlePawn3D pawn, Camera3D camera, Func<(bool, double)> clock)
    {
        _pawn = pawn; _camera = camera; Clock = clock;
        for (int k = 0; k < _blades.Length; k++)
            _blades[k] = Sprite("res://assets/fx/debuff_blade.svg", .86f, new Color(Tint, 0));
        Draw(0);
    }
    internal void SetLayers(int count, bool over) { Count = Math.Clamp(count, 0, 5); _over = over; }
    internal void Strike(int ordinal) { _strike = Math.Clamp(ordinal - 1, 0, 4); _strikeAt = Age; }

    public override void _Process(double delta)
    {
        // 死亡・勝利後は停止中であっても残像を残さない。
        if (!Live(_pawn)) { End(); return; }
        if (!Advance(delta)) return;
        var (_, speed) = Clock();
        Draw((float)Math.Min(1, delta * speed / .18));
    }
    private void Draw(float step)
    {
        for (int k = 0; k < _blades.Length; k++)
        {
            float goal = k < Count ? 1 : 0;
            _amounts[k] = Mathf.MoveToward(_amounts[k], goal, step);
            float amount = _amounts[k], side = k - 2;
            var blade = _blades[k];
            blade.Visible = amount > .001f;
            blade.GlobalPosition = _pawn.FxPoint + _camera.GlobalBasis.X * (side * .32f + side * (1 - amount) * .14f)
                + _camera.GlobalBasis.Y * (-.65f + Math.Abs(side) * .12f + Mathf.Sin((float)Age * 1.7f + k) * .025f);
            blade.Scale = Vector3.One * (.78f + amount * .22f);
            bool lit = _strike == k && Age - _strikeAt < .16;
            blade.Modulate = new Color(lit ? new Color("f0e5ff") : Tint, amount * (lit ? .95f : _over ? .70f : .48f));
        }
    }
}

// 一度の既存接触につき一筋。実際の連撃が5回なら計5筋で、追加の命中を作らない。
public partial class DebuffBlade3D : DebuffFx3D
{
    private BattlePawn3D _pawn = null!;
    private Camera3D _camera = null!;
    private Sprite3D _blade = null!;
    private int _ordinal;
    internal void Start(BattlePawn3D pawn, Camera3D camera, Func<(bool, double)> clock, int ordinal)
    {
        _pawn = pawn; _camera = camera; Clock = clock; _ordinal = ordinal;
        _blade = Sprite("res://assets/fx/debuff_blade.svg", 1.4f, new Color(Tint, .8f));
        Draw();
    }
    public override void _Process(double delta)
    {
        if (!Live(_pawn)) { End(); return; }
        if (!Advance(delta)) return;
        if (Age >= .17) { End(); return; }
        Draw();
    }
    private void Draw()
    {
        float t = (float)(Age / .17), direction = _ordinal % 2 == 0 ? -1 : 1;
        _blade.GlobalPosition = _pawn.FxPoint + _camera.GlobalBasis.X * (direction * (.65f - t * 1.1f))
            + _camera.GlobalBasis.Y * (-.15f + t * .42f);
        _blade.Scale = new Vector3(1.2f, .8f + t * .3f, 1);
        _blade.Modulate = new Color(Tint, (1 - t) * .85f);
    }
}
