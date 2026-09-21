using Godot;
using System;

/// <summary>戦闘の席を平らに保ち、周縁の地形と植生で奥行きを作る表示専用の草原。</summary>
public partial class MeadowEnvironment3D : Node3D
{
    private readonly Random _random = new(7319);

    public override void _Ready()
    {
        BuildGround();
        BuildGrass();
        BuildWoodland();
    }

    private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    private static float Height(float x, float z)
    {
        float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp(Mathf.Max(Mathf.Abs(x) - 7, Mathf.Abs(z) - 4.8f) / 6, 0, 1));
        // 観戦側は平地へ開く。水平視点のカメラと戦場の間に丘を作らない。
        float viewingSide = 1 - Mathf.SmoothStep(4.8f, 9f, z);
        return viewingSide * edge * (1.1f + Mathf.Sin(x * 0.23f + z * 0.14f) * 0.8f
            + Mathf.Cos(z * 0.31f - x * 0.11f) * 0.6f);
    }

    private void BuildGround()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const int steps = 90;
        void Vertex(float x, float z)
        {
            surface.SetUV(new Vector2(x, z));
            surface.SetNormal(new Vector3(Height(x - 0.1f, z) - Height(x + 0.1f, z), 0.2f,
                Height(x, z - 0.1f) - Height(x, z + 0.1f)).Normalized());
            surface.AddVertex(new Vector3(x, Height(x, z) - 0.015f, z));
        }
        for (int x = -steps / 2; x < steps / 2; x++)
        for (int z = -steps / 2; z < steps / 2; z++)
        {
            Vertex(x, z); Vertex(x + 1, z); Vertex(x, z + 1);
            Vertex(x + 1, z); Vertex(x + 1, z + 1); Vertex(x, z + 1);
        }
        var shader = new Shader { Code = @"shader_type spatial;
render_mode cull_disabled;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p); f = f*f*(3.0-2.0*f);
    return mix(mix(hash(i), hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
    float patches = noise(UV * 0.43) * 0.65 + noise(UV * 1.5) * 0.35;
    float grain = noise(UV * 33.0);
    float path = 1.0 - smoothstep(0.3, 1.6, abs(UV.y - 0.4 * sin(UV.x * 0.55)) + noise(UV * 2.0) * 0.5);
    path *= 0.55;
    vec3 grass = mix(vec3(0.065,0.105,0.028), vec3(0.19,0.24,0.075), patches);
    vec3 earth = mix(vec3(0.15,0.115,0.065),vec3(0.27,0.22,0.12), patches);
    float clouds = noise(UV * 0.14 + vec2(TIME * 0.008, 0.0));
    ALBEDO = mix(grass, earth, path) * (0.86 + grain * 0.23) * mix(0.72, 1.0, smoothstep(0.3,0.65,clouds));
    ROUGHNESS = 0.96;
}" };
        AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = new ShaderMaterial { Shader = shader } });
    }

    private void BuildGrass()
    {
        // 交差した細い葉をまとめて描画する。席の内側では丈を抑えて足元の輪を隠さない。
        var blade = new SurfaceTool();
        blade.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.Pi / 3;
            Vector3 side = new(Mathf.Cos(a) * 0.035f, 0, Mathf.Sin(a) * 0.035f);
            blade.SetUV(new Vector2(0, 1)); blade.AddVertex(-side);
            blade.SetUV(new Vector2(0.5f, 0)); blade.AddVertex(new Vector3(0.06f, 1, 0.03f));
            blade.SetUV(new Vector2(1, 1)); blade.AddVertex(side);
        }
        blade.GenerateNormals();
        var shader = new Shader { Code = @"shader_type spatial;
render_mode cull_disabled;
varying float shade;
void vertex() {
    vec3 p = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz;
    float bend = (1.0-UV.y)*(1.0-UV.y);
    VERTEX.x += sin(TIME*1.7 + p.x*1.3 + p.z*0.7)*0.18*bend;
    shade = fract(sin(dot(p.xz,vec2(12.4,37.1)))*4375.2);
}
void fragment() {
    ALBEDO = mix(vec3(0.065,0.11,0.026),vec3(0.24,0.30,0.09),(1.0-UV.y)*0.75+shade*0.25);
    ROUGHNESS = 0.95;
    BACKLIGHT = vec3(0.14,0.19,0.035);
}" };
        const int count = 6200;
        var instances = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = blade.Commit(), InstanceCount = count };
        for (int i = 0; i < count; i++)
        {
            float x = Range(-22, 22), z = Range(-24, 8);
            bool inField = Mathf.Abs(x) < 6.9f && Mathf.Abs(z) < 4.5f;
            float height = inField ? Range(0.035f, 0.10f) : Range(0.18f, 0.52f);
            float spread = inField ? Range(0.25f, 0.45f) : Range(0.7f, 1.2f);
            var basis = new Basis(Vector3.Up, Range(0, Mathf.Tau)).Scaled(new Vector3(spread, height, spread));
            instances.SetInstanceTransform(i, new Transform3D(basis, new Vector3(x, Height(x, z), z)));
        }
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = instances,
            MaterialOverride = new ShaderMaterial { Shader = shader },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 1,
        });
    }

    private static StandardMaterial3D Material(string color) => new()
    {
        AlbedoColor = Color.FromHtml(color), Roughness = 0.95f,
    };

    private void BuildWoodland()
    {
        var bark = Material("#493e30");
        var leaves = new Shader { Code = @"shader_type spatial;
uniform vec4 leaf_color : source_color;
varying vec3 local_pos;
void vertex() {
    local_pos = VERTEX;
    VERTEX += NORMAL * sin(VERTEX.x*13.0+VERTEX.z*9.0)*sin(VERTEX.y*11.0)*0.11;
    VERTEX.x += sin(TIME*1.1 + MODEL_MATRIX[3].x + VERTEX.y)*0.025;
}
void fragment() {
    float flecks = sin(local_pos.x*45.0+sin(local_pos.y*31.0))*sin(local_pos.z*39.0+local_pos.y*23.0);
    ALBEDO = leaf_color.rgb*(0.88+flecks*0.15);
    ROUGHNESS = 0.94;
}" };
        ShaderMaterial[] foliage = new ShaderMaterial[3];
        string[] leafColors = { "#30462b", "#455631", "#56603b" };
        for (int i = 0; i < foliage.Length; i++)
        {
            foliage[i] = new ShaderMaterial { Shader = leaves };
            foliage[i].SetShaderParameter("leaf_color", Color.FromHtml(leafColors[i]));
        }
        var crown = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 9, Rings = 5 };
        for (int i = 0; i < 40; i++)
        {
            float x = Range(-25, 25), z = Range(-30, -10);
            float size = Range(0.5f, 0.9f);
            var tree = new Node3D { Position = new Vector3(x, Height(x, z), z), Scale = Vector3.One * size };
            AddChild(tree);
            tree.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.10f, BottomRadius = 0.24f, Height = 3.4f, RadialSegments = 7 },
                Position = new Vector3(0, 1.7f, 0), RotationDegrees = new Vector3(0, 0, Range(-8, 8)), MaterialOverride = bark,
            });
            for (int j = 0; j < 3; j++)
                tree.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.09f, Height = 1.5f, RadialSegments = 6 },
                    Position = new Vector3((j % 2 == 0 ? -1 : 1) * 0.38f, 2.2f + j * 0.23f, 0),
                    RotationDegrees = new Vector3(15 * j, 60 * j, j % 2 == 0 ? 40 : -45), MaterialOverride = bark,
                });
            for (int j = 0; j < 7; j++)
            {
                float angle = j * 2.399f;
                tree.AddChild(new MeshInstance3D
                {
                    Mesh = crown,
                    Position = new Vector3(Mathf.Cos(angle) * Range(0.3f, 1.0f), Range(2.7f, 4.0f), Mathf.Sin(angle) * 0.75f),
                    Scale = new Vector3(Range(0.8f, 1.3f), Range(0.6f, 1.1f), Range(0.7f, 1.2f)),
                    RotationDegrees = new Vector3(Range(-20, 20), Range(0, 360), Range(-20, 20)),
                    MaterialOverride = foliage[(i + j) % foliage.Length],
                });
            }
        }
        var stone = new ShaderMaterial { Shader = new Shader { Code = @"shader_type spatial;
varying vec3 p;
void vertex() { p = VERTEX; }
void fragment() {
    float grain = fract(sin(dot(floor(p*65.0),vec3(17.1,31.7,93.2)))*43758.54);
    float seam = smoothstep(0.85,1.0,sin(p.y*22.0+p.x*7.0+sin(p.z*9.0)));
    vec3 rock = mix(vec3(0.10,0.11,0.095),vec3(0.23,0.24,0.20),grain);
    ALBEDO = rock*(1.0-seam*0.24);
    ROUGHNESS = 0.98;
}" } };
        var rockMesh = new SphereMesh { Radius = 1, Height = 1.6f, RadialSegments = 7, Rings = 4 };
        for (int i = 0; i < 34; i++)
        {
            float x = Range(-12, 12), z = Range(-9, 5);
            if (Mathf.Abs(x) < 7 && z > -4.8f) continue;
            float size = Range(0.15f, 0.8f);
            AddChild(new MeshInstance3D
            {
                Mesh = rockMesh, MaterialOverride = stone,
                Position = new Vector3(x, Height(x, z) + size * 0.25f, z),
                Scale = new Vector3(size * 1.3f, size * 0.7f, size),
                RotationDegrees = new Vector3(Range(-25, 25), Range(0, 360), Range(-20, 20)),
            });
        }
    }
}
