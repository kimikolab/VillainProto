using Godot;
using System;

// 本人の胴から弾ける赤い針状火花。濃さは表示量だけに使い、判定へ戻さない。
// 年齢は親の粉焼が渡すため、一時停止・倍速・中断の時計が分かれない。
public partial class PowderBurnBurst3D : Node3D
{
    private readonly ImmediateMesh _mesh = new();
    private Sprite3D _flash = null!;
    private Vector3 _right, _up, _front;
    private bool _drawing;
    private bool _compact;
    internal int RayCount { get; private set; }
    internal int PulseCount { get; private set; }
    internal float Radius { get; private set; }
    internal float Duration { get; private set; }
    internal float Brightness { get; set; } = 1;
    internal static float Level(int density) => Math.Min(7, MathF.Log2(1f + Math.Max(0, density)));
    // 火花の連続閃光とSEは同じ小・中・大の段階を使う。
    internal static int SizeTier(int density) => 1 + Math.Min(2, (int)(Level(density) / 2));
    internal static float DurationFor(int density) => .72f + Level(density) * .045f;

    internal void Configure(Camera3D camera, int density, bool compact = false)
    {
        _compact = compact;
        _right = camera.GlobalBasis.X; _up = camera.GlobalBasis.Y; _front = camera.GlobalBasis.Z;
        float level = Level(density);
        Radius = compact ? .20f + level * .055f : .62f + level * .23f;
        RayCount = compact ? 8 + (int)(level * 3) : 16 + (int)(level * 7);
        PulseCount = compact ? 1 + Math.Min(3, (int)(level / 1.5f)) : SizeTier(density);
        Duration = compact ? .15f + (PulseCount - 1) * .045f : DurationFor(density);
        AddChild(new MeshInstance3D {
            Mesh = _mesh,
            MaterialOverride = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                NoDepthTest = true, RenderPriority = 2,
            }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        _flash = ShockMarkFx.Sprite(this, GlobalPosition, ShockMarkFx.Halo,
            compact ? .25f + level * .035f : .72f + level * .10f, Colors.White);
        _flash.NoDepthTest = true; _flash.RenderPriority = 3;
    }

    private static float Variation(int index) => Mathf.PosMod(Mathf.Sin(index * 12.9898f + 78.233f) * 43758.5453f, 1);

    internal void Draw(float age, Vector3 at)
    {
        Vector3 center = at - _up * .10f + _front * .46f;
        _mesh.ClearSurfaces(); _drawing = false;
        float flash = 0;
        for (int pulse = 0; pulse < PulseCount; pulse++)
        {
            float elapsed = age - pulse * (_compact ? .045f : .16f);
            if (elapsed < 0 || elapsed > .68f) continue;
            float life = _compact ? .15f : .46f + pulse * .035f;
            float fade = Math.Max(0, 1 - elapsed / life);
            flash += MathF.Exp(-elapsed * (_compact ? 30 : 13)) * (pulse == 0 ? 1 : .65f);
            for (int i = 0; i < RayCount; i++)
            {
                float rayAge = elapsed - i % 7 * .004f;
                if (rayAge <= 0) continue;
                float angle = i * 2.399963f + pulse * .29f;
                float varied = Variation(i + pulse * 71);
                Vector3 direction = _right * Mathf.Cos(angle) + _up * Mathf.Sin(angle);
                float length = Radius * (.38f + varied * .82f) * (i % 9 == 0 ? 1.25f : 1);
                float head = .08f + length * (1 - MathF.Exp(-rayAge * 15));
                float tail = Math.Max(.07f, head - length * (.70f - Math.Min(.5f, rayAge * .60f)));
                float brightness = fade * (.65f + varied * .35f) * Brightness;
                if (brightness <= 0) continue;
                Vector3 a = center + direction * tail;
                Vector3 b = center + direction * head;
                float width = (i % 9 == 0 ? .020f : .008f) * (1 + Radius * .16f) * (_compact ? .65f : 1);
                // 外縁は深紅、針は朱赤、細い芯は赤みの白。麻痺の黄色と区別する。
                Needle(a, b, width * 3.0f, new Color(new Color("d81935"), brightness * .24f));
                Needle(a, b, width, new Color(new Color("ff3045"), brightness));
                Needle(a, b, width * .28f, new Color(new Color("ffd1d8"), brightness * .95f));
                if (i % 3 == 0)
                {
                    Vector3 grain = center + direction * (head + .10f);
                    Diamond(grain, .018f + varied * .016f, new Color(new Color("ff596a"), brightness));
                }
            }
            // 薄い衝撃の輪が胴から広がり、発火点と広がりを一緒に読めるようにする。
            float ringRadius = Radius * (.16f + elapsed * .75f);
            float ringFade = Math.Max(0, 1 - elapsed / (_compact ? .12f : .32f)) * .48f * Brightness;
            if (ringFade > 0)
                for (int i = 0; i < 64; i++)
                {
                    Vector3 Point(int k) => center + (_right * Mathf.Cos(k * Mathf.Tau / 64)
                        + _up * Mathf.Sin(k * Mathf.Tau / 64)) * ringRadius;
                    Needle(Point(i), Point(i + 1), .006f, new Color(new Color("ff3045"), ringFade));
                }
        }
        if (_drawing) _mesh.SurfaceEnd();
        _flash.GlobalPosition = center;
        _flash.Modulate = new Color(new Color("ffaaa8"), Math.Min(.95f, flash) * Brightness);
        _flash.Scale = Vector3.One * (1 + Math.Min(age, .20f) * 1.8f);
    }

    private void Vertex(Vector3 point, Color tint)
    {
        if (!_drawing) { _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles); _drawing = true; }
        _mesh.SurfaceSetColor(tint); _mesh.SurfaceAddVertex(ToLocal(point));
    }

    private void Needle(Vector3 a, Vector3 b, float width, Color tint)
    {
        Vector3 side = (b - a).Cross(_front).Normalized() * width;
        Vector3 shoulder = a.Lerp(b, .73f);
        // 帯の尻と先端を両方細くする。太い放電の塊に見える四角い端は作らない。
        foreach (var p in new[] { a, shoulder - side, shoulder + side,
            shoulder - side, b, shoulder + side }) Vertex(p, tint);
    }

    private void Diamond(Vector3 at, float radius, Color tint)
    {
        foreach (var p in new[] { at - _right * radius, at - _up * radius, at + _right * radius,
            at - _right * radius, at + _right * radius, at + _up * radius }) Vertex(p, tint);
    }
}
