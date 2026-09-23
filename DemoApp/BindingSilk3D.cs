using Godot;

// 書き手から伸びる三本の糸と、対象を巻く螺旋。位置は描画中の駒に追従する。
public partial class BindingSilk3D : MeshInstance3D
{
    private readonly ImmediateMesh _mesh = new();
    private BattlePawn3D _source = null!;
    private BattlePawn3D _target = null!;
    private Camera3D _camera = null!;
    private StandardMaterial3D _material = null!;
    private float _age;
    private float _release = -1;

    public void Configure(BattlePawn3D source, BattlePawn3D target, Camera3D camera)
    {
        _source = source;
        _target = target;
        _camera = camera;
        Mesh = _mesh;
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = new Color(0.96f, 0.89f, 0.72f),
        };
        MaterialOverride = _material;
    }

    public void Release() => _release = 0;

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(_source) || !GodotObject.IsInstanceValid(_target))
        { QueueFree(); return; }
        float dt = (float)(delta * _source.AnimationSpeed);
        _age += dt;
        if (_release >= 0)
        {
            _release += dt;
            if (_release >= 0.26f) { QueueFree(); return; }
        }
        float fade = _release < 0 ? 1 : 1 - _release / 0.26f;
        _material.AlbedoColor = new Color(0.96f, 0.89f, 0.72f, fade);
        float grow = Mathf.Clamp(_age / 0.22f, 0, 1);
        Vector3 from = _source.FxPoint;
        Vector3 to = _target.FxPoint;
        _mesh.ClearSurfaces();
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 3; i++)
        {
            Vector3 start = from + Vector3.Up * (i - 1) * 0.14f;
            Vector3 end = to + Vector3.Up * (i - 1) * 0.24f;
            Vector3 mid = start.Lerp(end, 0.5f) - Vector3.Up * (1 - fade) * 0.5f;
            Segment(start, start.Lerp(mid, Mathf.Min(grow * 2, 1)), 0.016f);
            if (grow > 0.5f) Segment(mid, mid.Lerp(end, (grow - 0.5f) * 2), 0.016f);
        }
        // 体を二周半する帯。締まりながら現れ、解除では広がって消える。
        float radius = 0.46f + (1 - grow) * 0.22f + (1 - fade) * 0.22f;
        for (int i = 0; i < 80; i++)
        {
            if (i / 80f > grow) break;
            Vector3 Point(float t) => to + new Vector3(Mathf.Cos(t * Mathf.Tau * 2.5f) * radius,
                -0.65f + t * 1.2f, Mathf.Sin(t * Mathf.Tau * 2.5f) * radius);
            Segment(Point(i / 80f), Point((i + 1) / 80f), 0.022f);
        }
        _mesh.SurfaceEnd();
    }

    private void Segment(Vector3 a, Vector3 b, float width)
    {
        Vector3 side = (b - a).Cross(_camera.GlobalPosition - (a + b) * 0.5f).Normalized() * width;
        _mesh.SurfaceAddVertex(a - side);
        _mesh.SurfaceAddVertex(a + side);
        _mesh.SurfaceAddVertex(b + side);
        _mesh.SurfaceAddVertex(a - side);
        _mesh.SurfaceAddVertex(b + side);
        _mesh.SurfaceAddVertex(b - side);
    }
}
