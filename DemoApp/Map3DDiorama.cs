using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 3D のジオラマの組み立て（発注書 §2-2）。<b>見た目だけ</b>——盤の規則は 1 行も読まない（マスの位置は `Map3DLayout` の表から）。
// 地形・木・岩・山・道・川は購入素材（NatureBlocks）の部品と道具、建物（砦・墓地・本陣・橋）は仮の箱。
// 原本の場面とマテリアルは書き換えない（実体を置くか、複製したリソースを変える）。
public partial class Map3DStage
{
    private const string Nature = "res://PolyBlocks/NatureBlocks/assets/";
    private const string Materials = "res://PolyBlocks/NatureBlocks/source_files/materials/";

    // 光と色（夕暮れ寄り・明るすぎない）。ポンが触る値なのでここに並べる。
    internal static readonly Color SunColor = Color.FromHtml("#ffb07a");
    internal const float SunEnergy = 1.30f;
    internal static readonly Vector3 SunAngles = new(-31, -66, 0);
    internal static readonly Color FillColor = Color.FromHtml("#7a8ad8");
    internal const float FillEnergy = 0.30f;
    internal static readonly Color AmbientColor = Color.FromHtml("#6d6a96");
    internal const float AmbientEnergy = 0.66f;
    internal static readonly Color HazeColor = Color.FromHtml("#4a3d5c");
    internal static readonly Color VoidColor = Color.FromHtml("#191522");

    private static readonly Dictionary<string, PackedScene?> Scenes = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Aabb> Boxes = new(StringComparer.Ordinal);

    private Map3DLayout _layout = null!;
    private Map3DLayout.Table? _sheet;
    private readonly Random _random = new(1101);
    /// <summary>木や岩を置かない場所（マス・待つ場所・本陣の手前）。</summary>
    private readonly List<Vector3> _keepClear = new();
    private readonly List<Vector3[]> _rivers = new();

    /// <summary>使った素材の部品（報告と検証の層に出す・重複なし）。</summary>
    internal SortedSet<string> UsedParts { get; } = new(StringComparer.Ordinal);
    internal int PartCount { get; private set; }

    internal void Build(Map3DLayout layout, MapGraph graph)
    {
        _layout = layout;
        _sheet = layout.Sheet;
        foreach (MapGraph.Cell c in graph.Cells) _keepClear.Add(layout.PosOf(c.Id));
        _keepClear.AddRange(layout.Approach);
        if (_sheet is not null)
        {
            _keepClear.AddRange(_sheet.HomeSlots);
            foreach (Vector3[] r in _sheet.Rivers) _rivers.Add(Map3DLayout.Smooth(r, out _));
        }

        BuildLight();
        BuildGround();
        BuildRoads();
        BuildRivers();
        if (_sheet is not null)
        {
            BuildFort(_sheet.Fort);
            BuildGraveyard(_sheet.Graveyard);
            if (graph.HasPortal) BuildCamp(_sheet.Camp);
            BuildFoothills();
            foreach (var (at, radius) in _sheet.Wetlands) BuildWetland(at, radius);
            foreach (var (at, radius, count, kind) in _sheet.Woods) BuildWood(at, radius, count, kind);
            BuildMeadow(new Rect2(-30, -10, 60, 26), 260);
        }
        else
        {
            // 表の無いステージ: 盤のまわりに木を散らすだけ。
            Vector3 mid = layout.Look;
            BuildWood(mid + new Vector3(0, 0, -24), new Vector2(34, 5), 60, "pine");
            BuildMeadow(new Rect2(mid.X - 34, mid.Z - 20, 68, 40), 260);
        }
        GD.Print($"MAP3D_DIORAMA table={_sheet is not null} parts={PartCount} kinds={UsedParts.Count} tools={Map3DMode.ToolsAvailable}");
    }

    private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
    private T Pick<T>(params T[] items) => items[_random.Next(items.Length)];

    // ---------------- 光 ----------------

    private void BuildLight()
    {
        // 空は映らない（固定の見下ろし）ので、背景は暗い紫の単色。遠くは薄い霞で沈める。
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = VoidColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = AmbientColor,
                AmbientLightEnergy = AmbientEnergy,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapExposure = 1.0f,
                GlowEnabled = true,
                GlowIntensity = 0.55f,
                GlowBloom = 0.04f,
                GlowHdrThreshold = 1.05f,
                FogEnabled = true,
                FogMode = Godot.Environment.FogModeEnum.Depth,
                FogLightColor = HazeColor,
                FogDepthBegin = 66,
                FogDepthEnd = 150,
                FogSkyAffect = 0,
                SsaoEnabled = true,
                SsaoRadius = 1.2f,
                SsaoIntensity = 1.6f,
                AdjustmentEnabled = true,
                AdjustmentContrast = 1.06f,
                AdjustmentSaturation = 1.06f,
            },
        });
        // 夕日は西（左）から少し手前向きに差す——駒と木の影が右奥へ長く伸びる。
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = SunAngles,
            LightColor = SunColor,
            LightEnergy = SunEnergy,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 120,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            ShadowBlur = 1.4f,
            ShadowOpacity = 0.82f,
        });
        // 影の側を青紫で起こす（真っ黒にしない）。
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-48, 118, 0),
            LightColor = FillColor,
            LightEnergy = FillEnergy,
            ShadowEnabled = false,
        });
    }

    // ---------------- 地面 ----------------

    private static float Smooth(float from, float to, float x) => Mathf.SmoothStep(from, to, x);

    /// <summary>楕円の中なら 0〜1 未満（中心 0・縁 1）。</summary>
    private static float Oval(Vector3 p, Vector3 at, Vector2 radius)
        => new Vector2((p.X - at.X) / radius.X, (p.Z - at.Z) / radius.Y).Length();

    /// <summary>地面の高さ。盤の上（道・マス・建物）は 0 のまま、山すそだけせり上げ、川と湿地だけ掘る。</summary>
    internal float Height(float x, float z)
    {
        var p = new Vector3(x, 0, z);
        float h = 0;
        float foot = _sheet?.FoothillZ ?? -1000;
        if (z < foot)
        {
            float rise = Smooth(0, 10, foot - z);
            h += rise * (3.4f + 1.3f * Mathf.Sin(x * 0.21f) + 0.8f * Mathf.Cos(x * 0.47f + 1.3f));
        }
        // 手前と左右の縁はゆるい起伏（UI の下・画面の外）。
        float rim = Math.Max(Smooth(15, 26, z), Smooth(27, 38, Math.Abs(x)));
        h += rim * (0.9f + 0.5f * Mathf.Sin(x * 0.3f + z * 0.2f));
        foreach (Vector3[] river in _rivers)
            h -= 1.0f * (1 - Smooth(1.0f, 2.5f, Map3DLayout.DistanceTo(river, p))) * Math.Max(0.35f, 1 - Math.Max(0, h) * 0.25f);
        if (_sheet is not null)
            foreach (var (at, radius) in _sheet.Wetlands)
                h -= 0.26f * (1 - Smooth(0.45f, 1.0f, Oval(p, at, radius)));
        return h;
    }

    private Color GroundTint(float x, float z)
    {
        var p = new Vector3(x, 0, z);
        float grain = 0.5f + 0.5f * Mathf.Sin(x * 1.7f + Mathf.Sin(z * 1.3f) * 2.1f) * Mathf.Cos(z * 1.1f - x * 0.6f);
        Color c = new Color(0.78f, 0.86f, 0.66f).Lerp(new Color(0.92f, 0.90f, 0.70f), grain * 0.55f);
        if (_sheet is null) return c;
        foreach (var (at, radius, _, kind) in _sheet.Woods)
            c = c.Lerp(kind == "pine" ? new Color(0.52f, 0.60f, 0.52f) : new Color(0.50f, 0.64f, 0.46f), 0.75f * (1 - Smooth(0.6f, 1.25f, Oval(p, at, radius))));
        foreach (var (at, radius) in _sheet.Wetlands)
            c = c.Lerp(new Color(0.40f, 0.50f, 0.42f), 0.85f * (1 - Smooth(0.6f, 1.3f, Oval(p, at, radius))));
        // 砦と墓地のまわりは枯れた土、本陣のまわりは踏み固めた土。
        c = c.Lerp(new Color(0.58f, 0.52f, 0.56f), 0.80f * (1 - Smooth(0.55f, 1.2f, Oval(p, _sheet.Fort + new Vector3(1.5f, 0, 0), new Vector2(8.5f, 6.5f)))));
        c = c.Lerp(new Color(0.50f, 0.46f, 0.52f), 0.85f * (1 - Smooth(0.6f, 1.2f, Oval(p, _sheet.Graveyard, new Vector2(5.2f, 3.6f)))));
        c = c.Lerp(new Color(0.96f, 0.82f, 0.60f), 0.75f * (1 - Smooth(0.55f, 1.2f, Oval(p, _sheet.Camp - new Vector3(1.5f, 0, 0), new Vector2(8.0f, 6.0f)))));
        // 山すそは岩の灰色へ。
        c = c.Lerp(new Color(0.70f, 0.68f, 0.72f), Smooth(0, 6, _sheet.FoothillZ - z));
        foreach (Vector3[] river in _rivers)
            c = c.Lerp(new Color(0.56f, 0.60f, 0.56f), 1 - Smooth(1.6f, 3.2f, Map3DLayout.DistanceTo(river, p)));
        return c;
    }

    private void BuildGround()
    {
        Vector3 mid = _layout.Look;
        const float step = 1.0f;
        int nx = 96, nz = 76;
        float x0 = Mathf.Round(mid.X) - nx / 2 * step, z0 = Mathf.Round(mid.Z) - 40 * step;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(int i, int j)
        {
            float x = x0 + i * step, z = z0 + j * step;
            st.SetColor(GroundTint(x, z));
            st.SetUV(new Vector2(x, z) * 0.1f);
            st.AddVertex(new Vector3(x, Height(x, z), z));
        }
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                Vertex(i, j); Vertex(i + 1, j); Vertex(i, j + 1);
                Vertex(i + 1, j); Vertex(i + 1, j + 1); Vertex(i, j + 1);
            }
        st.Index();
        st.GenerateNormals();

        // 素材の地形のマテリアル（世界座標の三面貼り）を複製し、頂点の色で場所ごとに染める。原本は変えない。
        Material material;
        if (Load<StandardMaterial3D>(Materials + "terrain/nbmat_terrain_verdant.tres") is { } terrain)
        {
            var copy = (StandardMaterial3D)terrain.Duplicate();
            copy.VertexColorUseAsAlbedo = true;
            copy.DisableReceiveShadows = false;
            copy.AlbedoColor = new Color(1.02f, 1.0f, 0.94f);
            material = copy;
            UsedParts.Add("materials/terrain/nbmat_terrain_verdant");
        }
        else material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, AlbedoColor = Color.FromHtml("#5d7a48"), Roughness = 1 };
        AddChild(new MeshInstance3D { Name = "Ground", Mesh = st.Commit(), MaterialOverride = material });
    }

    // ---------------- 道・川（素材の道具） ----------------

    /// <summary>
    /// 購入素材の道具を 1 本動かす（<see cref="Generate"/> と同じ手順に、書き出しの値の設定を足したもの）。作れなければ null。
    /// </summary>
    private Node3D? RunTool(string toolScene, string pathProperty, string method, IReadOnlyList<Vector3> points, float y,
        List<(string Name, Variant Value)> materials, params (string Name, Variant Value)[] settings)
    {
        if (!Map3DMode.ToolsAvailable || !ResourceLoader.Exists(toolScene) || points.Count < 2) return null;
        var curve = new Curve3D { BakeInterval = 0.25f };
        foreach (Vector3 p in points) curve.AddPoint(new Vector3(p.X, y, p.Z));
        var path = new Path3D { Curve = curve };
        AddChild(path);
        var tool = GD.Load<PackedScene>(toolScene).Instantiate<Node3D>();
        AddChild(tool);
        foreach (var (name, value) in settings) tool.Set(name, value);
        foreach (var (name, value) in materials) tool.Set(name, value);
        tool.Set(pathProperty, path);
        tool.Call(method);
        if (!tool.GetChildren().OfType<MeshInstance3D>().Any()) { tool.QueueFree(); path.QueueFree(); return null; }
        foreach (MeshInstance3D mesh in tool.GetChildren().OfType<MeshInstance3D>()) mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return tool;
    }

    /// <summary>道具が無いときの帯（点の列に沿った平らな帯）。</summary>
    private void Ribbon(IReadOnlyList<Vector3> points, float width, float y, Color color)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i + 1 < points.Count; i++)
        {
            Vector3 a = points[i], b = points[i + 1];
            Vector3 side = (b - a).Normalized().Cross(Vector3.Up) * width / 2;
            Vector3 up = Vector3.Up * y;
            st.SetNormal(Vector3.Up); st.AddVertex(a - side + up); st.AddVertex(b - side + up); st.AddVertex(a + side + up);
            st.AddVertex(b - side + up); st.AddVertex(b + side + up); st.AddVertex(a + side + up);
        }
        AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Disabled },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    private void BuildRoads()
    {
        foreach (Vector3[] road in _layout.Roads)
        {
            int cuts = Math.Clamp((int)(Map3DLayout.LengthOf(road) * 2.5f), 8, 200);
            var settings = new List<(string, Variant)>();
            if (Tinted("nbmat_terrain_sand", new Color(1.02f, 0.92f, 0.76f)) is { } sand) settings.Add(("road_material", sand));
            if (Tinted("nbmat_terrain_swamp", new Color(0.80f, 0.74f, 0.62f)) is { } verge) settings.Add(("road_side_material", verge));
            Node3D? made = RunTool(PurchasedAssets.NatureRoadTool, "road_path", "generate_road", road, 0, settings,
                ("road_width", 1.7f), ("road_level", 0.07f), ("side_top_width", 2.3f), ("side_height", 0.10f), ("outer_width", 3.2f),
                ("length_subdivisions", cuts), ("width_variance", 0.22f), ("width_random_seed", 11));
            if (made is null) Ribbon(road, 1.7f, 0.05f, Color.FromHtml("#a08c66"));
            else UsedParts.Add("tools/generator_road");
        }
    }

    private void BuildRivers()
    {
        for (int r = 0; r < _rivers.Count; r++)
        {
            Vector3[] river = _rivers[r];
            // 川底を地面より下に置き、水面が地面のすぐ下（-0.14）に来るようにする（地面は `Height` で同じ線に沿って掘ってある）。
            var settings = new List<(string, Variant)>();
            if (DuskWater("nbmat_water_flow") is { } flow) settings.Add(("water_material", flow));
            Node3D? made = RunTool(PurchasedAssets.NatureRiverTool, "river_path", "generate_river", river, -0.62f, settings,
                ("water_width", 2.9f), ("water_level", 0.48f), ("bed_width", 2.2f), ("bed_depth", 0.0f),
                ("bank_top_width", 3.5f), ("bank_height", 0.54f), ("outer_width", 5.4f),
                ("length_subdivisions", 140), ("width_variance", 0.35f), ("width_random_seed", 11 + r), ("uv_tile_size", 9.0f));
            if (made is null) Ribbon(river, 2.6f, -0.12f, Color.FromHtml("#3f6f86"));
            else UsedParts.Add("tools/generator_river");
            // 道と交わる所に板の橋（仮の箱）。
            foreach (Vector3[] road in _layout.Roads)
                for (int i = 0; i + 1 < road.Length; i++)
                    for (int j = 0; j + 1 < river.Length; j++)
                        if (Crossing(road[i], road[i + 1], river[j], river[j + 1]) is { } at)
                            BuildBridge(at, (road[i + 1] - road[i]).Normalized());
        }
    }

    private static Vector3? Crossing(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Vector2 p = new(a.X, a.Z), r = new Vector2(b.X, b.Z) - p, q = new(c.X, c.Z), s = new Vector2(d.X, d.Z) - q;
        float cross = r.Cross(s);
        if (Mathf.Abs(cross) < 1e-6f) return null;
        float t = (q - p).Cross(s) / cross, u = (q - p).Cross(r) / cross;
        return t is >= 0 and < 1 && u is >= 0 and < 1 ? a.Lerp(b, t) : null;
    }

    // ---------------- 仮の箱 ----------------

    private static StandardMaterial3D Paint(string html, float roughness = 0.9f, Color? glow = null, float energy = 1.0f)
    {
        var m = new StandardMaterial3D { AlbedoColor = Color.FromHtml(html), Roughness = roughness };
        if (glow is { } e) { m.EmissionEnabled = true; m.Emission = e; m.EmissionEnergyMultiplier = energy; }
        return m;
    }

    private static MeshInstance3D Box(Node3D parent, Vector3 at, Vector3 size, Material material, float yaw = 0)
    {
        var mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = at, MaterialOverride = material, RotationDegrees = new Vector3(0, yaw, 0) };
        parent.AddChild(mesh);
        return mesh;
    }

    private static MeshInstance3D Shape(Node3D parent, Mesh mesh, Vector3 at, Material material, Vector3? rotation = null)
    {
        var node = new MeshInstance3D { Mesh = mesh, Position = at, MaterialOverride = material, RotationDegrees = rotation ?? Vector3.Zero };
        parent.AddChild(node);
        return node;
    }

    /// <summary>旗（竿 ＋ 布）。布は少し光らせて夕暮れでも色が読めるようにする。</summary>
    internal static Node3D Banner(Node3D parent, Vector3 at, float height, Color cloth, float width = 0.9f)
    {
        var root = new Node3D { Position = at };
        parent.AddChild(root);
        Shape(root, new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = height, RadialSegments = 6 }, new Vector3(0, height / 2, 0), Paint("#3a3028"));
        var fabric = new StandardMaterial3D
        {
            AlbedoColor = cloth, Roughness = 0.8f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = true, Emission = cloth, EmissionEnergyMultiplier = 0.35f,
        };
        Box(root, new Vector3(width / 2 + 0.05f, height - 0.42f, 0), new Vector3(width, 0.64f, 0.04f), fabric);
        return root;
    }

    private void BuildBridge(Vector3 at, Vector3 along)
    {
        var root = new Node3D { Position = at };
        AddChild(root);
        root.LookAt(at + along, Vector3.Up);   // -Z が道の向き
        StandardMaterial3D deck = Paint("#7a6044"), rail = Paint("#4f3d2c");
        for (float z = -2.5f; z <= 2.5f; z += 0.42f)
            Box(root, new Vector3(0, 0.15f + 0.05f * Mathf.Cos(z * 0.6f), z), new Vector3(2.3f, 0.10f, 0.36f), deck);
        foreach (float side in new[] { -1.15f, 1.15f })
        {
            Box(root, new Vector3(side, 0.68f, 0), new Vector3(0.10f, 0.10f, 5.2f), rail);
            foreach (float z in new[] { -2.5f, -0.85f, 0.85f, 2.5f })
                Box(root, new Vector3(side, 0.34f, z), new Vector3(0.16f, 0.9f, 0.16f), rail);
        }
        foreach (float z in new[] { -1.9f, 1.9f })
            Box(root, new Vector3(0, -0.35f, z), new Vector3(2.0f, 0.9f, 0.5f), Paint("#5b5a60"));
    }

    /// <summary>辺境の砦（ヴィラン側の拠点）。暗い石・紫の屋根・青緑の灯り。門は東（道の側）。</summary>
    private void BuildFort(Vector3 at)
    {
        var root = new Node3D { Name = "Fort", Position = at, Scale = Vector3.One * 0.84f };
        AddChild(root);
        StandardMaterial3D stone = Paint("#55505f"), dark = Paint("#3d3947"), roof = Paint("#3a2f52", 0.7f);
        StandardMaterial3D lit = Paint("#0d2a26", 0.5f, Color.FromHtml("#58f0c8"), 1.5f);
        // 天守
        Box(root, new Vector3(-0.6f, 2.0f, 0), new Vector3(4.2f, 4.0f, 4.4f), stone);
        Box(root, new Vector3(-0.6f, 4.25f, 0), new Vector3(4.7f, 0.5f, 4.9f), dark);
        for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
                if (ix != 0 || iz != 0) Box(root, new Vector3(-0.6f + ix * 2.0f, 4.8f, iz * 2.1f), new Vector3(0.7f, 0.6f, 0.7f), stone);
        // 塔（奥の左）と尖り屋根
        Shape(root, new CylinderMesh { TopRadius = 1.15f, BottomRadius = 1.3f, Height = 7.0f, RadialSegments = 8 }, new Vector3(-2.6f, 3.5f, -2.2f), stone);
        Shape(root, new CylinderMesh { TopRadius = 0, BottomRadius = 1.6f, Height = 2.4f, RadialSegments = 8 }, new Vector3(-2.6f, 8.2f, -2.2f), roof);
        Shape(root, new CylinderMesh { TopRadius = 0, BottomRadius = 2.9f, Height = 1.9f, RadialSegments = 4 }, new Vector3(-0.6f, 5.5f, 0), roof, new Vector3(0, 45, 0));
        // 城壁（門は東）
        Box(root, new Vector3(-0.4f, 1.1f, -4.0f), new Vector3(8.2f, 2.2f, 0.6f), dark);
        Box(root, new Vector3(-0.4f, 1.1f, 4.0f), new Vector3(8.2f, 2.2f, 0.6f), dark);
        Box(root, new Vector3(-4.4f, 1.1f, 0), new Vector3(0.6f, 2.2f, 8.2f), dark);
        Box(root, new Vector3(3.6f, 1.1f, -2.8f), new Vector3(0.6f, 2.2f, 2.6f), dark);
        Box(root, new Vector3(3.6f, 1.1f, 2.8f), new Vector3(0.6f, 2.2f, 2.6f), dark);
        foreach (float z in new[] { -1.3f, 1.3f })
        {
            Box(root, new Vector3(3.6f, 1.7f, z), new Vector3(1.0f, 3.4f, 1.0f), stone);
            Box(root, new Vector3(3.6f, 3.6f, z), new Vector3(1.2f, 0.4f, 1.2f), dark);
        }
        Box(root, new Vector3(3.6f, 2.75f, 0), new Vector3(0.7f, 0.5f, 1.8f), dark);
        // 窓の灯り（手前と東）
        foreach (float x in new[] { -1.7f, 0.5f })
            Box(root, new Vector3(x, 2.6f, 2.21f), new Vector3(0.42f, 0.8f, 0.06f), lit);
        Box(root, new Vector3(1.51f, 2.6f, 0.9f), new Vector3(0.06f, 0.8f, 0.42f), lit);
        Box(root, new Vector3(-2.6f, 5.6f, -0.9f), new Vector3(0.4f, 0.7f, 0.1f), lit);
        Banner(root, new Vector3(3.6f, 3.8f, -1.3f), 2.2f, Color.FromHtml("#3fbfa4"));
        Banner(root, new Vector3(3.6f, 3.8f, 1.3f), 2.2f, Color.FromHtml("#3fbfa4"));
        root.AddChild(new OmniLight3D { Position = new Vector3(4.6f, 2.4f, 0), LightColor = Color.FromHtml("#58f0c8"), LightEnergy = 0.9f, OmniRange = 6.0f, ShadowEnabled = false });
    }

    /// <summary>砦の隣の墓地。石の柵・墓石・枯れ木（素材の spooky の木）・青白い灯り。</summary>
    private void BuildGraveyard(Vector3 at)
    {
        var root = new Node3D { Name = "Graveyard", Position = at };
        AddChild(root);
        StandardMaterial3D stone = Paint("#77737f"), fence = Paint("#33303a");
        foreach (float z in new[] { -2.5f, 2.5f })
            for (float x = -3.6f; x <= 3.6f; x += 0.9f)
                if (!(z > 0 && Mathf.Abs(x - 1.8f) < 0.8f)) Box(root, new Vector3(x, 0.4f, z), new Vector3(0.12f, 0.8f, 0.12f), fence);
        foreach (float z in new[] { -2.5f, 2.5f }) Box(root, new Vector3(z > 0 ? -1.3f : 0, 0.62f, z), new Vector3(z > 0 ? 4.6f : 7.2f, 0.08f, 0.08f), fence);
        foreach (float x in new[] { -3.6f, 3.6f }) Box(root, new Vector3(x, 0.62f, 0), new Vector3(0.08f, 0.08f, 5.0f), fence);
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 5; col++)
            {
                var p = new Vector3(-2.8f + col * 1.35f + Range(-0.15f, 0.15f), 0, -1.2f + row * 1.9f + Range(-0.2f, 0.2f));
                float lean = Range(-9, 9);
                if ((row + col) % 3 == 0)
                {
                    Box(root, p + new Vector3(0, 0.6f, 0), new Vector3(0.16f, 1.2f, 0.14f), stone, lean);
                    Box(root, p + new Vector3(0, 0.85f, 0), new Vector3(0.62f, 0.16f, 0.14f), stone, lean);
                }
                else
                {
                    Box(root, p + new Vector3(0, 0.42f, 0), new Vector3(0.56f, 0.84f, 0.18f), stone, lean);
                    Shape(root, new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.28f, Height = 0.18f, RadialSegments = 10 },
                        p + new Vector3(0, 0.84f, 0), stone, new Vector3(90, lean, 0));
                }
                Box(root, p + new Vector3(0, 0.03f, 0.55f), new Vector3(0.7f, 0.06f, 0.9f), Paint("#4a4048"));
            }
        root.AddChild(new OmniLight3D { Position = new Vector3(0, 1.4f, 0), LightColor = Color.FromHtml("#9af0c8"), LightEnergy = 1.5f, OmniRange = 6.5f, ShadowEnabled = false });
        foreach (Vector3 offset in new[] { new Vector3(-4.6f, 0, -1.6f), new Vector3(4.4f, 0, -2.2f), new Vector3(-4.2f, 0, 2.6f) })
            Put($"trees/generic/spooky/nb_tree{Pick(1, 3, 5)}_spooky.tscn", at + offset, Range(3.2f, 4.2f), Range(0, Mathf.Tau));
    }

    /// <summary>討伐隊の本陣。柵・天幕・赤い旗・物見やぐら・かがり火。門は西（道の側）。</summary>
    private void BuildCamp(Vector3 at)
    {
        var root = new Node3D { Name = "Camp", Position = at, Scale = Vector3.One * 0.88f };
        AddChild(root);
        StandardMaterial3D wood = Paint("#6a5238"), cloth = Paint("#d8cdb4", 0.85f), stripe = Paint("#9c2f2a", 0.85f), dark = Paint("#4a3a2a");
        Color red = Color.FromHtml("#c8392f");
        // 柵（西に口を開けた輪）
        for (int i = 0; i < 40; i++)
        {
            float a = i / 40f * Mathf.Tau;
            if (Mathf.Abs(Mathf.AngleDifference(a, Mathf.Pi)) < 0.62f) continue;
            var p = new Vector3(Mathf.Cos(a) * 5.4f, 0, Mathf.Sin(a) * 4.6f);
            float h = Range(1.5f, 1.9f);
            Shape(root, new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.16f, Height = h, RadialSegments = 5 }, p + new Vector3(0, h / 2, 0), wood);
        }
        // 本営の天幕（大）と小さい天幕
        Shape(root, new PrismMesh { Size = new Vector3(4.2f, 2.6f, 3.6f) }, new Vector3(1.4f, 1.3f, -0.4f), cloth);
        Box(root, new Vector3(1.4f, 0.45f, 1.42f), new Vector3(4.0f, 0.5f, 0.06f), stripe);
        Shape(root, new PrismMesh { Size = new Vector3(2.2f, 1.6f, 2.2f) }, new Vector3(-1.6f, 0.8f, -2.6f), cloth, new Vector3(0, 28, 0));
        Shape(root, new PrismMesh { Size = new Vector3(2.2f, 1.6f, 2.2f) }, new Vector3(-1.2f, 0.8f, 2.9f), cloth, new Vector3(0, -22, 0));
        Shape(root, new PrismMesh { Size = new Vector3(2.0f, 1.4f, 2.0f) }, new Vector3(3.6f, 0.7f, 2.8f), cloth, new Vector3(0, 12, 0));
        // 物見やぐら（奥）
        foreach (var (x, z) in new[] { (-0.5f, -0.5f), (0.5f, -0.5f), (-0.5f, 0.5f), (0.5f, 0.5f) })
            Box(root, new Vector3(3.9f + x, 2.1f, -3.0f + z), new Vector3(0.16f, 4.2f, 0.16f), wood);
        Box(root, new Vector3(3.9f, 4.2f, -3.0f), new Vector3(1.6f, 0.14f, 1.6f), dark);
        Shape(root, new CylinderMesh { TopRadius = 0, BottomRadius = 1.3f, Height = 1.0f, RadialSegments = 4 }, new Vector3(3.9f, 5.5f, -3.0f), stripe, new Vector3(0, 45, 0));
        // 門の両脇の旗と、天幕の旗
        Banner(root, new Vector3(-5.0f, 0, -2.4f), 3.6f, red, 1.1f);
        Banner(root, new Vector3(-5.0f, 0, 2.4f), 3.6f, red, 1.1f);
        Banner(root, new Vector3(1.4f, 2.5f, -0.4f), 2.0f, red, 1.0f);
        // かがり火と荷
        Shape(root, new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.36f, Height = 0.3f, RadialSegments = 8 }, new Vector3(-2.4f, 0.15f, 0.3f), dark);
        Shape(root, new CylinderMesh { TopRadius = 0, BottomRadius = 0.34f, Height = 0.9f, RadialSegments = 6 }, new Vector3(-2.4f, 0.7f, 0.3f),
            Paint("#ff7a2a", 0.4f, Color.FromHtml("#ff8a30"), 3.0f));
        root.AddChild(new OmniLight3D { Position = new Vector3(-2.4f, 1.5f, 0.3f), LightColor = Color.FromHtml("#ff9a50"), LightEnergy = 2.6f, OmniRange = 8.5f, ShadowEnabled = false });
        foreach (var (x, z, s) in new[] { (-3.6f, -1.6f, 0.7f), (-3.0f, -2.1f, 0.5f), (0.2f, 3.4f, 0.6f) })
            Box(root, new Vector3(x, s / 2, z), new Vector3(s, s, s), wood, Range(0, 40));
    }

    // ---------------- 素材の部品 ----------------

    private static T? Load<T>(string path) where T : Resource => ResourceLoader.Exists(path) ? GD.Load<T>(path) : null;

    /// <summary>
    /// 素材の水のマテリアルを複製して、夕暮れの色に落とす（原本は昼の明るい水色で、暗い盤の上では光って見える）。原本は変えない。
    /// </summary>
    private static Material? DuskWater(string name)
    {
        if (Load<ShaderMaterial>(Materials + $"water/{name}.tres") is not { } water) return null;
        var copy = (ShaderMaterial)water.Duplicate();
        foreach (string key in new[] { "water_color", "ripple_color" })
            if (copy.GetShaderParameter(key).VariantType == Variant.Type.Color)
            {
                Color c = copy.GetShaderParameter(key).AsColor();
                bool ripple = key == "ripple_color";
                copy.SetShaderParameter(key, new Color(c.R * (ripple ? 0.62f : 0.30f), c.G * (ripple ? 0.52f : 0.30f), c.B * (ripple ? 0.56f : 0.36f), c.A));
            }
        return copy;
    }

    /// <summary>素材の地形のマテリアルを複製して色を掛ける（道を明るい土色にするなど）。</summary>
    private static Material? Tinted(string name, Color tint)
    {
        if (Load<StandardMaterial3D>(Materials + $"terrain/{name}.tres") is not { } terrain) return null;
        var copy = (StandardMaterial3D)terrain.Duplicate();
        copy.AlbedoColor = tint;
        copy.DisableReceiveShadows = false;
        return copy;
    }

    /// <summary>
    /// 素材の場面を 1 つ置く。<paramref name="height"/> は置いたときの高さ（素材ごとの元の大きさは測って合わせる）。
    /// 足元は地面の高さに合わせる。無ければ何もしない。
    /// </summary>
    private Node3D? Put(string part, Vector3 at, float height, float yaw = 0, float sink = 0, bool shadow = true)
    {
        string path = Nature + part;
        if (!Scenes.TryGetValue(path, out PackedScene? scene))
            Scenes[path] = scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
        if (scene is null) return null;
        var node = scene.Instantiate<Node3D>();
        if (!Boxes.TryGetValue(path, out Aabb box)) Boxes[path] = box = Measure(node);
        float k = box.Size.Y <= 0.001f ? 1 : height / box.Size.Y;
        node.Scale = Vector3.One * k;
        node.Rotation = new Vector3(0, yaw, 0);
        node.Position = new Vector3(at.X, Height(at.X, at.Z) - box.Position.Y * k - sink, at.Z);
        if (!shadow) Each<GeometryInstance3D>(node, g => g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off);
        AddChild(node);
        UsedParts.Add(part[..part.LastIndexOf('/')]);
        PartCount++;
        return node;
    }

    private static void Each<T>(Node node, Action<T> act) where T : Node
    {
        if (node is T hit) act(hit);
        foreach (Node child in node.GetChildren()) Each(child, act);
    }

    /// <summary>場面の中の見える物ぜんぶを囲む箱（根の座標で）。</summary>
    private static Aabb Measure(Node3D root)
    {
        Aabb? all = null;
        void Walk(Node node, Transform3D t)
        {
            if (node is Node3D n && node != root) t *= n.Transform;
            if (node is VisualInstance3D v)
            {
                Aabb box = t * v.GetAabb();
                all = all is { } a ? a.Merge(box) : box;
            }
            foreach (Node child in node.GetChildren()) Walk(child, t);
        }
        Walk(root, Transform3D.Identity);
        return all ?? new Aabb(Vector3.Zero, Vector3.One);
    }

    /// <summary>その点に物を置いてよいか（道・川・マス・建物から離れているか）。</summary>
    private bool Clear(Vector3 p, float road = 2.1f, float cell = 3.0f)
    {
        foreach (Vector3[] line in _layout.Roads) if (Map3DLayout.DistanceTo(line, p) < road) return false;
        foreach (Vector3[] river in _rivers) if (Map3DLayout.DistanceTo(river, p) < 2.5f) return false;
        foreach (Vector3 c in _keepClear) if (new Vector2(c.X - p.X, c.Z - p.Z).Length() < cell) return false;
        if (_sheet is null) return true;
        if (Oval(p, _sheet.Fort, new Vector2(5.6f, 5.2f)) < 1) return false;
        if (Oval(p, _sheet.Graveyard, new Vector2(4.6f, 3.3f)) < 1) return false;
        if (Oval(p, _sheet.Camp, new Vector2(6.6f, 5.8f)) < 1) return false;
        return true;
    }

    /// <summary>
    /// その高さの物をそこに置くと、奥の道・マス・駒を隠すか（カメラは手前（+Z）から見下ろすので、物は自分より奥を隠す）。
    /// </summary>
    private bool Hides(Vector3 p, float height)
    {
        float reach = height / 1.25f + 0.9f;
        bool Behind(Vector3 q) => q.Z < p.Z && p.Z - q.Z < reach && Mathf.Abs(q.X - p.X) < 1.9f;
        foreach (Vector3 c in _keepClear) if (Behind(c + new Vector3(0, 0, 1.2f))) return true;
        foreach (Vector3[] line in _layout.Roads)
            for (int i = 0; i < line.Length; i += 2) if (Behind(line[i])) return true;
        return false;
    }

    private void BuildWood(Vector3 at, Vector2 radius, int count, string kind)
    {
        for (int placed = 0, tries = 0; placed < count && tries < count * 12; tries++)
        {
            float a = Range(0, Mathf.Tau), r = Mathf.Sqrt(Range(0, 1));
            var p = at + new Vector3(Mathf.Cos(a) * radius.X * r, 0, Mathf.Sin(a) * radius.Y * r);
            float height = kind == "pine" ? Range(4.2f, 6.2f) : Range(3.2f, 4.8f);
            if (!Clear(p) || Hides(p, height)) continue;
            string tone = Pick("forest", "forest", "forest", "olive", "autumn");
            string part = kind == "pine"
                ? $"trees/pine/nb_pine{Pick(1, 2)}_{Pick("forest", "forest", "olive")}.tscn"
                : _random.Next(6) == 0 ? $"trees/birch/nb_birch{Pick(1, 2, 3)}_{Pick("forest", "autumn")}.tscn"
                : $"trees/generic/{tone}/nb_tree{Pick(1, 2, 3, 4, 5)}_{tone}.tscn";
            if (Put(part, p, height, Range(0, Mathf.Tau)) is null) continue;
            placed++;
            // 木の根元に茂みと羊歯。
            if (_random.Next(3) == 0)
            {
                var q = p + new Vector3(Range(-1.2f, 1.2f), 0, Range(0.3f, 1.2f));
                if (Clear(q, 1.6f, 2.4f))
                    Put(_random.Next(2) == 0 ? $"vegetation/shrubs/forest/nb_shrub{Pick(1, 2)}_forest.tscn" : "vegetation/ferns/nb_fern1_forest.tscn",
                        q, Range(0.7f, 1.2f), Range(0, Mathf.Tau), shadow: false);
            }
        }
    }

    private void BuildWetland(Vector3 at, Vector2 radius)
    {
        // 水たまり（素材の水のマテリアル）。地面は `Height` で少し掘ってある。
        var water = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 0.02f, RadialSegments = 28 },
            Position = at + new Vector3(0, -0.13f, 0),
            Scale = new Vector3(radius.X * 0.9f, 1, radius.Y * 0.9f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = DuskWater("nbmat_water")
                ?? new StandardMaterial3D { AlbedoColor = new Color(0.20f, 0.36f, 0.40f, 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
        };
        AddChild(water);
        UsedParts.Add("materials/water/nbmat_water");
        int reeds = (int)(radius.X * radius.Y * 2.2f);
        for (int i = 0; i < reeds; i++)
        {
            float a = Range(0, Mathf.Tau), r = Range(0.55f, 1.05f);
            var p = at + new Vector3(Mathf.Cos(a) * radius.X * r, 0, Mathf.Sin(a) * radius.Y * r);
            if (!Clear(p, 1.5f, 2.2f)) continue;
            Put($"vegetation/reeds/nb_reed{Pick(1, 2)}.tscn", p, Range(0.9f, 1.5f), Range(0, Mathf.Tau), shadow: false);
        }
        for (int i = 0; i < 5; i++)
        {
            float a = Range(0, Mathf.Tau), r = Range(0.1f, 0.6f);
            var p = at + new Vector3(Mathf.Cos(a) * radius.X * r, 0, Mathf.Sin(a) * radius.Y * r);
            if (Put("vegetation/flowers/nb_flower_lilypods.tscn", p, 0.12f, Range(0, Mathf.Tau), shadow: false) is { } pad)
                pad.Position = new Vector3(p.X, -0.11f, p.Z);
        }
        for (int i = 0; i < 4; i++)
        {
            float a = Range(0, Mathf.Tau);
            var p = at + new Vector3(Mathf.Cos(a) * radius.X * 1.05f, 0, Mathf.Sin(a) * radius.Y * 1.05f);
            if (Clear(p, 1.8f, 2.6f)) Put($"ground/boulders/swamp/nb_boulder{Pick(1, 2, 3, 4)}_swamp.tscn", p, Range(0.5f, 0.9f), Range(0, Mathf.Tau), 0.1f);
        }
        for (int i = 0, placed = 0; i < 14 && placed < 3; i++)
        {
            float a = Range(0, Mathf.Tau);
            var p = at + new Vector3(Mathf.Cos(a) * radius.X * 1.15f, 0, Mathf.Sin(a) * radius.Y * 1.15f);
            float height = Range(3.6f, 4.6f);
            if (!Clear(p) || Hides(p, height)) continue;
            if (Put($"trees/willow/nb_willow1_{Pick("forest", "olive")}.tscn", p, height, Range(0, Mathf.Tau)) is not null) placed++;
        }
    }

    /// <summary>北の山すそ: 崖と岩を道の奥に並べ、その奥に山並み。</summary>
    private void BuildFoothills()
    {
        float foot = _sheet!.FoothillZ;
        for (float x = -34; x <= 34; x += Range(4.2f, 6.0f))
        {
            var p = new Vector3(x + Range(-0.8f, 0.8f), 0, foot - Range(1.6f, 3.4f));
            if (_rivers.Any(r => Map3DLayout.DistanceTo(r, p) < 3.4f)) continue;
            Put($"terrain/cliffs/grey/nb_cliff{Pick(1, 2, 3, 4, 5)}_grey.tscn", p, Range(3.0f, 4.6f), Range(0, Mathf.Tau), 0.5f);
        }
        for (int i = 0; i < 26; i++)
        {
            var p = new Vector3(Range(-32, 32), 0, foot - Range(-0.4f, 4.5f));
            if (!Clear(p, 2.0f, 2.8f)) continue;
            string tone = Pick("grey", "grey", "moss");
            Put(_random.Next(2) == 0 ? $"ground/boulders/{tone}/nb_boulder{Pick(1, 2, 3, 4)}_{tone}.tscn"
                    : $"ground/rocks/{tone}/nb_rock{Pick(1, 2, 3, 4, 5, 6)}_{tone}.tscn",
                p, Range(0.5f, 1.3f), Range(0, Mathf.Tau), 0.12f);
        }
        // 山並み（奥）。川の谷だけ少し空ける。
        float[] xs = { -38, -27, -16, -6, 13, 23, 33, 42 };
        for (int i = 0; i < xs.Length; i++)
        {
            var p = new Vector3(xs[i] + Range(-1.5f, 1.5f), 0, foot - Range(7.5f, 10.5f));
            string tone = i % 3 == 2 ? "moss" : "grey";
            Put($"terrain/mountains/{tone}/nb_mountain{1 + i % 4}_{tone}.tscn", p, Range(13, 19), Range(0, Mathf.Tau), 2.2f);
        }
        // 左右の端にも低い山（画面の外へ盤が続いて見えるように）。
        Put("terrain/mountains/moss/nb_mountain2_moss.tscn", new Vector3(-41, 0, 4), 11, 1.1f, 2.0f);
        Put("terrain/mountains/moss/nb_mountain3_moss.tscn", new Vector3(42, 0, 2), 12, 2.6f, 2.0f);
    }

    /// <summary>草・花・小石を野原に散らす（道とマスは避ける）。</summary>
    private void BuildMeadow(Rect2 area, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var p = new Vector3(Range(area.Position.X, area.End.X), 0, Range(area.Position.Y, area.End.Y));
            if (!Clear(p, 1.5f, 2.3f)) continue;
            int roll = _random.Next(20);
            if (roll < 14) Put($"vegetation/grass/verdant/nb_grass{Pick("1.1", "1.2", "2.1", "2.2")}_verdant.tscn", p, Range(0.35f, 0.7f), Range(0, Mathf.Tau), shadow: false);
            else if (roll < 16) Put($"vegetation/flowers/nb_flower{Pick(1, 2)}_{Pick("white", "white", "blue", "red")}.tscn", p, Range(0.3f, 0.5f), Range(0, Mathf.Tau), shadow: false);
            else
            {
                string tone = roll < 18 ? Pick("grey", "moss") : Pick("forest", "olive");
                if (roll < 18) Put($"ground/rocks/{tone}/nb_rock{Pick(1, 2, 3, 4, 5, 6)}_{tone}.tscn", p, Range(0.25f, 0.45f), Range(0, Mathf.Tau), 0.05f, shadow: false);
                else Put($"vegetation/shrubs/{tone}/nb_shrub{Pick(1, 2)}_{tone}.tscn", p, Range(0.6f, 1.0f), Range(0, Mathf.Tau), shadow: false);
            }
        }
    }
}
