using Godot;
using System;

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
    private float _conduct = -1;
    private bool _snap;
    private bool _sparked;
    private Func<Vector3>? _webFrom, _webTo, _webHub;
    private Func<double>? _webSpeed;
    private readonly StandardMaterial3D _current = new() {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        AlbedoColor = new Color("baffff"),
    };

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
    }

    public void Release() => _release = 0;

    // 網は拘束と寿命が違う。倒れた書き手の位置も保持できる表示用の端点を受け取る。
    public void ConfigureWeb(Func<Vector3> from, Func<Vector3> to, Func<Vector3>? hub,
        Func<double> speed, Camera3D camera)
    {
        _webFrom = from; _webTo = to; _webHub = hub; _webSpeed = speed; _camera = camera;
        Mesh = _mesh;
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _material = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
    }

    public void Conduct(bool snap)
    {
        _conduct = 0;
        _snap = snap;
        _sparked = false;
        if (snap) Release();
    }

    public override void _Process(double delta)
    {
        if (_webFrom is null && (!GodotObject.IsInstanceValid(_source) || !GodotObject.IsInstanceValid(_target)))
        { QueueFree(); return; }
        double speed = Math.Max(0.1, _webSpeed?.Invoke() ?? _source.AnimationSpeed);
        Vector3 from = _webFrom?.Invoke() ?? _source.FxPoint;
        Vector3 to = _webTo?.Invoke() ?? _target.FxPoint;
        float dt = (float)(delta * speed);
        _age += dt;
        if (_conduct >= 0)
        {
            _conduct += dt;
            if (_conduct >= 0.18f && !_sparked)
            {
                _sparked = true;
                ShockMarkFx.Sparks(this, to, ThunderFx.Cyan, _snap ? 16 : 8,
                    _snap ? 1.0f : 0.55f, 0.22 / speed);
                ShockMarkFx.Glow(this, to, ThunderFx.Cyan, _snap ? 1.5f : 0.9f, 0.22 / speed);
            }
        }
        // 解除通知が放電より先に来る台本でも、電流が的まで走ってから糸を切る。
        if (_release >= 0 && (_conduct < 0 || _conduct >= 0.26f))
        {
            _release += dt;
            if (_release >= 0.26f) { QueueFree(); return; }
        }
        float fade = _release < 0 ? 1 : 1 - _release / 0.26f;
        Color tint = _conduct is >= 0 and < 0.30f ? ThunderFx.Cyan : new Color(0.96f, 0.89f, 0.72f);
        bool web = _webFrom is not null;
        _material.AlbedoColor = new Color(tint, fade * (web ? (_conduct is >= 0 and < 0.30f ? 0.95f : 0.55f) : 1));
        float grow = Mathf.Clamp(_age / 0.22f, 0, 1);
        // 切れた端は的から離れて垂れる。書き手の死亡や拘束解除ではここへ来ない。
        if (web && _release >= 0) to = to.Lerp(from, (1 - fade) * 0.12f) - Vector3.Up * (1 - fade) * 0.9f;
        _mesh.ClearSurfaces();
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _material);
        int threads = web ? 1 : 3;
        for (int i = 0; i < threads; i++)
        {
            Vector3 start = from + Vector3.Up * (i - (threads - 1) * 0.5f) * 0.14f;
            Vector3 end = to + Vector3.Up * (i - (threads - 1) * 0.5f) * 0.24f;
            Vector3 mid = start.Lerp(end, 0.5f) - Vector3.Up * (1 - fade) * 0.5f;
            Segment(start, start.Lerp(mid, Mathf.Min(grow * 2, 1)), web ? 0.009f : 0.016f);
            if (grow > 0.5f) Segment(mid, mid.Lerp(end, (grow - 0.5f) * 2), web ? 0.009f : 0.016f);
        }
        // 体を二周半する帯。締まりながら現れ、解除では広がって消える。
        float radius = 0.46f + (1 - grow) * 0.22f + (1 - fade) * 0.22f;
        for (int i = 0; i < 80; i++)
        {
            if (i / 80f > grow) break;
            Vector3 Point(float t) => to + new Vector3(Mathf.Cos(t * Mathf.Tau * 2.5f) * radius,
                -0.65f + t * 1.2f, Mathf.Sin(t * Mathf.Tau * 2.5f) * radius);
            Segment(Point(i / 80f), Point((i + 1) / 80f), web ? 0.012f : 0.022f);
        }
        _mesh.SurfaceEnd();
        if (_webHub is not null)
        {
            // 組み付いた敵を中心に扇形の網目。解除してもこの枝は残る。
            Vector3 hub = _webHub();
            _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _material);
            for (int strand = 0; strand < 3; strand++)
            {
                Vector3 a = hub + Vector3.Up * (strand - 1) * 0.35f;
                Vector3 b = to + Vector3.Up * (strand - 1) * 0.35f;
                for (int i = 0; i < 16; i++)
                {
                    Vector3 Point(float t) => a.Lerp(b, t * grow)
                        - Vector3.Up * Mathf.Sin(t * Mathf.Pi) * (0.15f + (1 - fade) * 0.8f);
                    Segment(Point(i / 16f), Point((i + 1) / 16f), 0.008f);
                }
            }
            for (int i = 1; i < 6; i++)
            {
                Vector3 p = hub.Lerp(to, i / 6f * grow);
                Segment(p - Vector3.Up * 0.35f, p + Vector3.Up * 0.35f, 0.006f);
            }
            _mesh.SurfaceEnd();
        }
        if (_conduct is >= 0 and < 0.24f)
        {
            float p = Mathf.Clamp(_conduct / 0.18f, 0, 1);
            _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _current);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Clamp(p - 0.24f + i * 0.03f, 0, 1);
                float b = Mathf.Clamp(p - 0.24f + (i + 1) * 0.03f, 0, 1);
                Vector3 jitter = _camera.GlobalBasis.Y * (i % 2 == 0 ? 0.055f : -0.055f);
                Segment(from.Lerp(to, a) + jitter, from.Lerp(to, b) - jitter, _snap ? 0.05f : 0.03f);
            }
            _mesh.SurfaceEnd();
        }
    }

    private void Segment(Vector3 a, Vector3 b, float width)
    {
        Vector3 side = (b - a).Cross(_camera.GlobalPosition - (a + b) * 0.5f).Normalized() * width;
        _mesh.SurfaceAddVertex(ToLocal(a - side));
        _mesh.SurfaceAddVertex(ToLocal(a + side));
        _mesh.SurfaceAddVertex(ToLocal(b + side));
        _mesh.SurfaceAddVertex(ToLocal(a - side));
        _mesh.SurfaceAddVertex(ToLocal(b + side));
        _mesh.SurfaceAddVertex(ToLocal(b - side));
    }
}
