using Godot;
using System;
using System.Collections.Generic;

// ガレの結晶弾幕。時刻はGatherCast3Dの時計だけを読み、命中通知は増やさない。
public partial class GatherCast3D
{
    internal const float GareFocusExtraCharge = .70f;
    internal int GareCrystalCount { get; private set; }
    internal int GareRingCount { get; private set; }
    internal bool GareFocused { get; private set; }
    private StandardMaterial3D? _prismRibbonMaterial;
    private static readonly Color Ice = new("c9faff"), Cyan = new("58dfff"), Violet = new("9c7dff");

    internal void GarePrismCharge(Func<Vector3> muzzle, Camera3D camera, float power, float release)
    {
        var halo = Sprite(muzzle(), GatherArt.Light, 1.1f * power, Cyan);
        var core = Sprite(muzzle(), GareCrystalArt.Sparkle, .65f * power, Ice);
        Draw(age => {
            float p = (age - .07f) / (release - .07f);
            halo.Visible = core.Visible = p >= 0 && p < 1;
            p = Mathf.Clamp(p, 0, 1);
            float build = p * p;
            halo.GlobalPosition = core.GlobalPosition = muzzle();
            halo.Scale = Vector3.One * (.18f + build * .9f);
            halo.Modulate = new Color(Cyan, .08f + build * .72f);
            core.Scale = Vector3.One * (.12f + build * .8f);
            core.Modulate = new Color(Ice, build * .9f);
            core.Rotation = new Vector3(0, 0, p * p * 4);
        });
        for (int k = 0; k < 28; k++)
        {
            int key = k;
            var crystal = PrismCrystal((.07f + Noise(k + 613) * .07f) * power);
            Draw(age => {
                float p = (age - .07f) / (release - .07f);
                crystal.Visible = p >= 0 && p < 1 && key / 28f < .25f + p * .75f;
                p = Mathf.Clamp(p, 0, 1);
                float angle = key * 2.399963f + p * 2 + p * p * 10;
                float phase = Mathf.PosMod(key * .137f + p * (1.1f + p * 1.4f), 1);
                float radius = (.35f + Noise(key + 71) * .5f) * (1 - phase) * (1 - p * .35f);
                crystal.GlobalPosition = muzzle() + (camera.GlobalBasis.X * MathF.Cos(angle)
                    + camera.GlobalBasis.Y * MathF.Sin(angle)) * radius;
                AimPrism(crystal, camera.GlobalBasis.X, camera, angle);
            });
        }
    }

    internal void GarePrismaticVolley(Vector3 muzzle, IReadOnlyList<Vector3> targets, Camera3D camera, float power, float delay = 0)
    {
        GareFocused = targets.Count == 1;
        Vector3 forward = targets.Count > 0 ? (targets[0] - muzzle).Normalized() : camera.GlobalBasis.X;
        PrismBurst(muzzle, forward, camera, GareFocused ? 42 : 34, .32f + delay, .42f,
            (GareFocused ? 1.1f : .85f) * power, 701);
        PrismHalo(muzzle, camera, (GareFocused ? 1.6f : 1.15f) * power, .30f + delay, .35f, Cyan);
        if (GareFocused) PrismRing(muzzle, forward, camera, .85f * power, .32f + delay, .36f, false);
        if (GareFocused) FocusedPrism(muzzle, targets[0], camera, power, .32f + delay);
        else
        {
            for (int n = 0; n < targets.Count; n++)
                ScatterPrisms(muzzle, targets[n], camera, power, n);
        }
        for (int n = 0; n < targets.Count; n++)
        {
            Vector3 end = targets[n];
            Vector3 direction = (end - muzzle).Normalized();
            if (GareFocused)
            {
                FocusedPrismImpact(end, direction, camera, power, .70f + delay);
                continue;
            }
            PrismBurst(end, direction, camera, 12, .70f + delay, .48f, .65f * power, 907 + n * 31);
            PrismHalo(end, camera, .85f, .70f + delay, .40f, Ice);
            PrismRing(end, direction, camera, .52f, .70f + delay, .38f, false);
        }
    }

    private void ScatterPrisms(Vector3 from, Vector3 end, Camera3D camera, float power, int targetIndex)
    {
        Vector3 side = camera.GlobalBasis.Z.Cross((end - from).Normalized()).Normalized();
        for (int k = 0; k < 5; k++)
        {
            int seed = targetIndex * 37 + k * 17 + 11;
            float bend = (k - 2) * .14f;
            Vector3 Path(float p) => from.Lerp(end, p) + side * (Mathf.Sin(p * Mathf.Pi) * bend)
                + camera.GlobalBasis.Y * (Mathf.Sin(p * Mathf.Pi) * (.05f + Noise(seed) * .12f));
            float start = .32f + k * .012f;
            float duration = .70f - start;
            float size = (.20f + Noise(seed + 3) * .20f) * power;
            var crystal = PrismCrystal(size);
            Draw(age => {
                float p = (age - start) / duration;
                crystal.Visible = p >= 0 && p < 1;
                p = Mathf.Clamp(p, 0, 1);
                crystal.GlobalPosition = Path(p);
                AimPrism(crystal, Path(Math.Min(1, p + .02f)) - Path(Math.Max(0, p - .02f)), camera, p * 2 + seed);
            });
            PrismRibbon(Path, camera, start, duration, .24f, .025f * power,
                k % 2 == 0 ? Cyan : Violet, false);
            for (int j = 0; j < 4; j++)
            {
                float along = .13f + j * .18f + Noise(seed + j) * .07f;
                Vector3 at = Path(along) + side * ((Noise(seed + j + 9) - .5f) * .32f);
                PrismStar(at, camera, .06f + Noise(seed + j + 4) * .09f,
                    start + along * duration, .25f, seed + j);
            }
        }
    }

    private void FocusedPrism(Vector3 from, Vector3 end, Camera3D camera, float power, float fireAt)
    {
        Vector3 direction = (end - from).Normalized();
        Vector3 side = camera.GlobalBasis.Z.Cross(direction).Normalized();
        Vector3 Path(float p) => from.Lerp(end, p);
        // 白い芯、青い外縁、紫の残光。到達前に的まで線を伸ばさない。
        PrismRibbon(Path, camera, fireAt, .38f, 1f, .44f * power, new Color("597cff70"), true);
        PrismRibbon(Path, camera, fireAt, .38f, 1f, .23f * power, new Color("6ce7ffc0"), true);
        PrismRibbon(Path, camera, fireAt, .38f, 1f, .080f * power, new Color("f3ffff"), true);
        for (int k = 0; k < 5; k++)
        {
            int phase = k;
            Vector3 Helix(float p) => Path(p) + side * (Mathf.Sin(p * 23 + phase * 1.26f) * .24f * power)
                + direction * (Mathf.Cos(p * 23 + phase * 1.26f) * .08f);
            PrismRibbon(Helix, camera, fireAt, .38f, 1f, .022f * power, k % 2 == 1 ? Violet : Cyan, true);
        }
        foreach (float p in new[] { .08f, .30f, .56f, .80f })
            PrismRing(Path(p), direction, camera, (.52f + p * .42f) * power,
                fireAt + p * .38f, .45f, true);
        var spear = PrismCrystal(.85f * power);
        Draw(age => {
            float p = (age - fireAt) / .38f;
            spear.Visible = p >= 0 && p < 1;
            spear.GlobalPosition = Path(Mathf.Clamp(p, 0, 1));
            AimPrism(spear, direction, camera, age * 8);
        });
        for (int k = 0; k < 26; k++)
        {
            float p = .05f + k / 28f;
            PrismStar(Path(p) + side * ((Noise(k + 311) - .5f) * .65f), camera,
                .06f + Noise(k + 19) * .10f, fireAt + p * .38f, .32f, k);
        }
    }

    // 着弾は一回。結晶・放射光・遅れて広がる光輪は、同じ命中を強調する余韻。
    private void FocusedPrismImpact(Vector3 at, Vector3 direction, Camera3D camera, float power, float hitAt)
    {
        PrismBurst(at, direction, camera, 56, hitAt, .56f, 1.65f * power, 907);
        PrismBurst(at, -direction, camera, 22, hitAt + .07f, .48f, 1.2f * power, 1223);
        Purchased(at, false, Cyan, 2.2f * power, hitAt);
        PrismHalo(at, camera, 1.25f * power, hitAt, .13f, Ice);
        PrismHalo(at, camera, 2.8f * power, hitAt, .50f, Cyan);
        PrismRing(at, direction, camera, 1.3f * power, hitAt, .48f, false);
        for (int layer = 0; layer < 3; layer++)
            PrismShockwave(at, camera, (1.25f + layer * .28f) * power,
                hitAt + layer * .055f, .42f, layer % 2 == 0 ? Cyan : Violet);
        for (int k = 0; k < 18; k++)
        {
            int key = k;
            float angle = k * Mathf.Tau / 18 + Noise(k + 933) * .18f;
            Vector3 radial = camera.GlobalBasis.X * MathF.Cos(angle)
                + camera.GlobalBasis.Y * MathF.Sin(angle);
            float length = (1.05f + Noise(k + 811) * 1.05f) * power;
            Vector3 Ray(float p) => at + radial * (length * p);
            PrismRibbon(Ray, camera, hitAt, .22f + Noise(k + 719) * .09f,
                .65f, .035f * power, key % 3 == 0 ? Violet : Cyan, false);
            PrismStar(at + radial * length * .75f, camera, .18f * power,
                hitAt + .10f, .40f, key);
        }
    }

    private void PrismShockwave(Vector3 at, Camera3D camera, float radius, float start, float duration, Color tint)
    {
        GareRingCount++;
        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Draw(age => {
            mesh.ClearSurfaces();
            float p = (age - start) / duration;
            if (p < 0 || p >= 1) return;
            float size = radius * (.15f + .85f * (1 - MathF.Pow(1 - p, 3)));
            float thickness = (.045f + .035f * (1 - p)) * (1 - p * .5f);
            mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            for (int j = 0; j <= 96; j++)
            {
                float angle = j * Mathf.Tau / 96;
                Vector3 radial = camera.GlobalBasis.X * MathF.Cos(angle)
                    + camera.GlobalBasis.Y * (MathF.Sin(angle) * .8f);
                Color color = tint.Lerp(Ice, .3f);
                color.A = .7f * (1 - p) * Mathf.Clamp(p * 14, 0, 1);
                mesh.SurfaceSetColor(color); mesh.SurfaceAddVertex(ToLocal(at + radial * (size - thickness)));
                mesh.SurfaceSetColor(color); mesh.SurfaceAddVertex(ToLocal(at + radial * (size + thickness)));
            }
            mesh.SurfaceEnd();
        });
    }

    private void PrismBurst(Vector3 at, Vector3 forward, Camera3D camera, int count,
        float start, float duration, float radius, int seed)
    {
        Vector3 right = camera.GlobalBasis.X, up = camera.GlobalBasis.Y;
        for (int k = 0; k < count; k++)
        {
            int key = seed + k * 13;
            float angle = k * 2.399963f;
            Vector3 spread = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * (.35f + Noise(key));
            Vector3 velocity = (spread + forward * (.3f + Noise(key + 2) * .7f)) * radius;
            var crystal = PrismCrystal((.10f + Noise(key + 1) * .25f) * MathF.Sqrt(radius));
            Draw(age => {
                float p = (age - start) / duration;
                crystal.Visible = p >= 0 && p < 1;
                p = Mathf.Clamp(p, 0, 1);
                float flight = 1 - MathF.Pow(1 - p, 2);
                crystal.GlobalPosition = at + velocity * flight;
                AimPrism(crystal, velocity, camera, key + p * 3);
                crystal.GetChild<MeshInstance3D>(0).Transparency = Mathf.Clamp((p - .4f) / .6f, 0, 1);
            });
            PrismStar(at + velocity * .7f, camera, .12f + Noise(key + 3) * .18f,
                start + duration * .16f, duration * .7f, key);
        }
    }

    private void PrismRibbon(Func<float, Vector3> path, Camera3D camera, float start, float travel,
        float tailLength, float width, Color tint, bool hold)
    {
        var mesh = new ImmediateMesh();
        _prismRibbonMaterial ??= new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        };
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = _prismRibbonMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node);
        Draw(age => {
            mesh.ClearSurfaces();
            float head = (age - start) / travel;
            float endAt = start + travel + (hold ? .16f : .06f);
            if (head <= 0 || age >= endAt) return;
            float fade = age < start + travel ? 1 : Mathf.Clamp((endAt - age) / (hold ? .16f : .06f), 0, 1);
            float begin = Math.Max(0, head - tailLength), finish = Math.Min(1, head);
            if (hold) begin = 0;
            if (finish <= begin) return;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            const int segments = 24;
            void Vertex(float t, float cross)
            {
                float p = Mathf.Lerp(begin, finish, t);
                Vector3 point = path(p);
                Vector3 direction = path(Math.Min(1, p + .005f)) - path(Math.Max(0, p - .005f));
                Vector3 side = camera.GlobalBasis.Z.Cross(direction).Normalized();
                float taper = hold ? .75f + .25f * MathF.Sin(t * Mathf.Pi) : .12f + .88f * t;
                float soft = MathF.Pow(1 - MathF.Abs(cross), 1.4f);
                mesh.SurfaceSetColor(new Color(tint, tint.A * fade * (hold ? 1 : t) * soft));
                mesh.SurfaceAddVertex(ToLocal(point + side * width * taper * cross));
            }
            for (int j = 0; j < segments; j++)
            for (int band = 0; band < 4; band++)
            {
                float a = j / (float)segments, b = (j + 1) / (float)segments;
                float left = -1 + band * .5f, right = left + .5f;
                Vertex(a, left); Vertex(a, right); Vertex(b, left);
                Vertex(a, right); Vertex(b, right); Vertex(b, left);
            }
            mesh.SurfaceEnd();
        });
    }

    private void PrismRing(Vector3 center, Vector3 direction, Camera3D camera, float radius,
        float start, float duration, bool focus)
    {
        GareRingCount++;
        Vector3 major = camera.GlobalBasis.Z.Cross(direction).Normalized();
        Vector3 minor = direction * .27f;
        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Draw(age => {
            mesh.ClearSurfaces();
            float p = (age - start) / duration;
            if (p < 0 || p >= 1) return;
            float grow = focus ? .9f + MathF.Sin(p * Mathf.Pi) * .12f : .25f + p * 1.4f;
            float alpha = MathF.Sin(Mathf.Clamp(p * 4, 0, 1) * Mathf.Pi / 2) * (1 - p);
            for (int layer = 0; layer < 3; layer++)
            {
                float r = radius * grow * (1 + layer * .08f);
                float thickness = layer == 0 ? .034f : layer == 1 ? .017f : .07f;
                mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
                for (int j = 0; j <= 80; j++)
                {
                    float angle = j * Mathf.Tau / 80 + age * (layer % 2 == 0 ? 4 : -5);
                    Vector3 radial = major * Mathf.Cos(angle) + minor * Mathf.Sin(angle);
                    Vector3 at = center + radial * r;
                    Color color = Cyan.Lerp(Violet, .5f + .5f * MathF.Sin(angle * 2 + age * 7));
                    if (layer == 0) color = color.Lerp(Ice, MathF.Pow(MathF.Abs(MathF.Cos(angle * 2)), 12) * .5f);
                    color.A = alpha * (layer == 0 ? .9f : layer == 1 ? .65f : .12f);
                    mesh.SurfaceSetColor(color); mesh.SurfaceAddVertex(ToLocal(at + radial.Normalized() * thickness));
                    mesh.SurfaceSetColor(color); mesh.SurfaceAddVertex(ToLocal(at - radial.Normalized() * thickness));
                }
                mesh.SurfaceEnd();
            }
        });
    }

    private void PrismHalo(Vector3 at, Camera3D camera, float size, float start, float duration, Color tint)
    {
        var halo = Sprite(at, GatherArt.Light, size, tint);
        halo.RenderPriority = 1;
        Draw(age => {
            float p = (age - start) / duration;
            halo.Visible = p >= 0 && p < 1;
            halo.Scale = Vector3.One * (.8f + Mathf.Clamp(p, 0, 1) * 1.6f);
            halo.Modulate = new Color(tint, .65f * (1 - Mathf.Clamp(p, 0, 1)));
        });
    }

    private void PrismStar(Vector3 at, Camera3D camera, float size, float start, float duration, int seed)
    {
        var star = Sprite(at, GareCrystalArt.Sparkle, size, seed % 3 == 0 ? Violet : Ice);
        Draw(age => {
            float p = (age - start) / duration;
            star.Visible = p >= 0 && p < 1;
            float scale = Math.Max(.001f, MathF.Sin(Mathf.Clamp(p, 0, 1) * Mathf.Pi));
            star.Scale = Vector3.One * scale;
            star.GlobalPosition = at + camera.GlobalBasis.Y * (Mathf.Clamp(p, 0, 1) * .05f);
        });
    }

    private Node3D PrismCrystal(float size)
    {
        GareCrystalCount++;
        var mesh = GareCrystalArt.Crystal;
        var bounds = mesh.GetAabb();
        float longest = Math.Max(bounds.Size.X, Math.Max(bounds.Size.Y, bounds.Size.Z));
        var crystal = new MeshInstance3D { Mesh = mesh, MaterialOverride = GareCrystalArt.Material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        // 素材メッシュの長軸をZへ合わせ、各弾を進行方向へ向ける。
        Basis correction = bounds.Size.Y >= bounds.Size.Z && bounds.Size.Y >= bounds.Size.X
            ? new Basis(Vector3.Right, Mathf.Pi / 2) : bounds.Size.X >= bounds.Size.Z
                ? new Basis(Vector3.Up, Mathf.Pi / 2) : Basis.Identity;
        var root = new Node3D(); AddChild(root); root.AddChild(crystal);
        crystal.Visible = true;
        crystal.Transform = new Transform3D(correction.Scaled(Vector3.One * (size / Math.Max(.001f, longest))),
            -(correction * bounds.GetCenter()) * (size / Math.Max(.001f, longest)));
        return root;
    }
    private static void AimPrism(Node3D root, Vector3 direction, Camera3D camera, float roll)
    {
        if (direction.LengthSquared() < .000001f) return;
        root.GlobalBasis = Basis.LookingAt(direction.Normalized(), camera.GlobalBasis.Z)
            * new Basis(Vector3.Forward, roll);
    }
    private static float Noise(int seed)
    {
        float n = MathF.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
        return n - MathF.Floor(n);
    }
}

internal static class GareCrystalArt
{
    private static Mesh? _mesh;
    private static ShaderMaterial? _material;
    private static Texture2D? _sparkle;
    internal static Mesh Crystal => _mesh ??= ResourceLoader.Exists("res://PolyBlocks/EffectBlocks/source_files/meshes/crystal_1.mesh")
        ? GD.Load<Mesh>("res://PolyBlocks/EffectBlocks/source_files/meshes/crystal_1.mesh")
        : new PrismMesh { Size = new Vector3(.4f, 1.2f, .4f) };
    internal static Texture2D Sparkle => _sparkle ??= ResourceLoader.Exists("res://PolyBlocks/EffectBlocks/source_files/textures/sparkle.png")
        ? GD.Load<Texture2D>("res://PolyBlocks/EffectBlocks/source_files/textures/sparkle.png") : GatherArt.Light;
    internal static ShaderMaterial Material => _material ??= new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled;
void fragment() {
    vec3 n = normalize(NORMAL);
    float face = abs(dot(n, normalize(vec3(0.3, 0.6, 1.0))));
    float prism = smoothstep(-0.65, 0.65, n.x + n.y * 0.6);
    vec3 color = mix(vec3(0.025, 0.33, 0.83), vec3(0.48, 0.20, 0.95), prism);
    color = mix(color, vec3(0.34, 0.91, 1.0), face * face * 0.75);
    color = mix(color, vec3(0.87, 1.0, 1.0), pow(face, 14.0) * 0.9);
    ALBEDO = color;
    EMISSION = color * 0.18;
}" } };
}
