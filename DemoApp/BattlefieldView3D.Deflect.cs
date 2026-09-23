using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int DeflectionPlays { get; private set; }

    // 台本の三者だけを読む。赤い布 → 金白の飛行で、盾・泥・毒とは形も色も分ける。
    // 戻った時点が着弾。HP と数字は呼び手が Damage の値をそのまま反映する。
    public async Task ShowDeflection(BattlePawn3D? attacker, BattlePawn3D? sora,
        BattlePawn3D? target, double speed)
    {
        if (sora is null || target is null) return;
        DeflectionPlays++;
        double duration = 0.72 / Math.Clamp(speed, 0.1, 2.0);
        Vector3 origin = sora.FxPoint;
        Vector3 end = target.FxPoint;
        Vector3 right = _camera.GlobalBasis.X.Normalized();
        Vector3 up = _camera.GlobalBasis.Y.Normalized();
        Vector3 view = (_camera.GlobalPosition - origin).Normalized();
        float facing = sora.Team == 0 ? 1 : -1;
        Vector3 side = right * facing;
        Vector3 anchor = origin + up * 0.18f + view * 0.35f;
        Vector3 release = anchor + side * 1.0f - up * 0.12f;
        Vector3 bend = release + side * 1.1f - up * 0.75f;
        Vector3 incoming = attacker?.FxPoint ?? origin + side * 1.8f;
        bool released = false;
        sora.AnimateDeflection(-side, duration);

        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
        };
        var fx = new MeshInstance3D
        {
            Name = "SoraDeflection", Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 12,
        };
        _fxRoot.AddChild(fx);
        Vector3 Local(Vector3 point) => fx.ToLocal(point);
        void Vertex(Vector3 point, Color color)
        {
            mesh.SurfaceSetColor(color);
            mesh.SurfaceAddVertex(Local(point));
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Vertex(a, color); Vertex(b, color); Vertex(c, color);
            Vertex(c, color); Vertex(b, color); Vertex(d, color);
        }
        void Ribbon(Func<float, Vector3> point, float head, float tail, float width, Color color)
        {
            for (int i = 0; i < 20; i++)
            {
                float u = Mathf.Lerp(tail, head, i / 20f), v = Mathf.Lerp(tail, head, (i + 1) / 20f);
                Vector3 a = point(u), b = point(v);
                Vector3 normal = (b - a).Cross(view).Normalized();
                float taper = (i + 1) / 20f;
                Vector3 w = normal * width * taper;
                Quad(a - w, a + w, b - w, b + w, color);
            }
        }
        void Draw(float t)
        {
            anchor = sora.FxPoint + up * 0.18f + view * 0.35f;
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            // 刃の続きが布へ入る短い拍。盾のような閉じた輪を作らない。
            if (t < 0.25f)
            {
                float head = Mathf.Clamp(t / 0.20f, 0, 1);
                Ribbon(u => incoming.Lerp(anchor, u), head, Mathf.Max(0, head - 0.30f),
                    0.045f, new Color(1, 0.88f, 0.66f, 1 - t / 0.25f));
            }
            // 手元から垂れる布。裾の長さとひだを揺らし、扇や盾の円弧にはしない。
            float sweep = Mathf.Clamp(t / 0.52f, 0, 1);
            float alpha = 1 - Mathf.SmoothStep(0.48f, 0.75f, t);
            Vector3 Cloth(float u, float v)
            {
                float angle = Mathf.Lerp(-2.8f, 0.10f, sweep) - u * 0.6f;
                float fold = Mathf.Sin(u * 16 - sweep * 9) * 0.12f * v;
                return anchor + side * (Mathf.Cos(angle) * u * 1.75f + fold)
                    + up * (Mathf.Sin(angle) * u * 0.70f
                        - v * (0.40f + 0.70f * Mathf.Sin(u * 2.4f))) + view * fold;
            }
            for (int i = 0; i < 32; i++)
            {
                float u = i / 32f, v = (i + 1) / 32f;
                Color red = new Color("8f1529").Lerp(new Color("e3483c"),
                    (Mathf.Sin(u * 16 - sweep * 9) + 1) * 0.5f);
                red.A = alpha;
                for (int j = 0; j < 6; j++)
                    Quad(Cloth(u, j / 6f), Cloth(v, j / 6f), Cloth(u, (j + 1) / 6f),
                        Cloth(v, (j + 1) / 6f), red);
                Quad(Cloth(u, 0.96f), Cloth(v, 0.96f), Cloth(u, 1),
                    Cloth(v, 1), new Color(0.95f, 0.70f, 0.30f, alpha));
            }
            if (t >= 0.40f)
            {
                if (!released)
                {
                    released = true;
                    release = Cloth(1, 0.35f);
                    bend = release + side * 1.1f - up * 0.75f;
                }
                float head = Mathf.Clamp((t - 0.40f) / 0.60f, 0, 1);
                Vector3 Path(float u) => (1 - u) * (1 - u) * release
                    + 2 * (1 - u) * u * bend + u * u * end;
                float tail = Mathf.Max(0, head - 0.24f);
                Ribbon(Path, head, tail, 0.10f, new Color(1, 0.65f, 0.22f, 0.65f));
                Ribbon(Path, head, tail, 0.028f, new Color("fff4d9"));
            }
            mesh.SurfaceEnd();
        }
        Draw(0.001f);
        var tween = fx.CreateTween();
        tween.TweenMethod(Callable.From<float>(Draw), 0.001f, 1f, duration);
        await ToSignal(tween, Tween.SignalName.Finished);
        fx.QueueFree();
        // 飛行の終点だけで弾ける。ダメージ表示もこの直後に出る。
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5;
            Vector3 ray = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            MakeBeam(end + ray * 0.08f, end + ray * 0.40f, new Color("ffe3a0"), 0.035f, 0.12);
        }
    }
}
