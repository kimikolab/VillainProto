using Godot;

/// <summary>標・痺・破片の表示。残量の判断は再生側に任せ、戦闘ルールを持たない。</summary>
public partial class StatusEffects3D : Node3D
{
    private MeshInstance3D _mark = null!;
    private MeshInstance3D _stun = null!;
    private Node3D _shards = null!;
    private readonly MeshInstance3D[] _pieces = new MeshInstance3D[6];
    private float _time;

    public void Configure(float height, float phase)
    {
        _time = phase;
        _mark = Billboard(new Vector2(1.15f,1.15f), new Vector3(0,height*0.73f,0.55f), @"
    vec2 p = UV-vec2(0.5);
    float d = length(p);
    float ring = 1.0-smoothstep(0.013,0.023,abs(d-0.31));
    float arms = (1.0-smoothstep(0.012,0.022,min(abs(p.x),abs(p.y)))) * step(0.22,max(abs(p.x),abs(p.y))) * (1.0-step(0.45,max(abs(p.x),abs(p.y))));
    ALBEDO = vec3(1.0,0.24,0.22);
    EMISSION = ALBEDO*0.65;
    ALPHA = max(ring,arms)*(0.65+0.22*sin(TIME*3.0));
");
        _stun = Billboard(new Vector2(2.1f,height*0.9f), new Vector3(0,height*0.5f,0.5f), @"
    float tick = floor(TIME*11.0);
    float zig = abs(fract(UV.y*5.0+tick*0.17)*2.0-1.0);
    float left = 0.12+zig*0.12;
    float right = 0.88-zig*0.12;
    float d = min(abs(UV.x-left),abs(UV.x-right));
    float arc = 1.0-smoothstep(0.008,0.025,d);
    float ends = smoothstep(0.06,0.18,UV.y)*(1.0-smoothstep(0.8,0.96,UV.y));
    ALBEDO = mix(vec3(1.0,0.66,0.04),vec3(1.0,1.0,0.68),1.0-smoothstep(0.0,0.01,d));
    EMISSION = ALBEDO*1.2;
    ALPHA = arc*ends*(0.58+0.35*step(0.3,sin(TIME*19.0)));
");
        _shards = new Node3D();
        AddChild(_shards);
        var mesh = new SphereMesh { Radius = 0.09f, Height = 0.32f, RadialSegments = 4, Rings = 1 };
        var material = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml("#a9dfe9"), Metallic = 0.65f, Roughness = 0.3f,
            EmissionEnabled = true, Emission = Color.FromHtml("#396b81"), EmissionEnergyMultiplier = 0.65f,
        };
        for (int i = 0; i < _pieces.Length; i++)
        {
            _pieces[i] = new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _shards.AddChild(_pieces[i]);
        }
        SetAmounts(0,0,0);
        UpdateShards();
    }

    private MeshInstance3D Billboard(Vector2 size, Vector3 position, string fragment)
    {
        var shader = new Shader { Code = @"shader_type spatial;
render_mode unshaded,cull_disabled,blend_mix,depth_draw_never;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0],INV_VIEW_MATRIX[1],INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);
}
void fragment() {" + fragment + "}" };
        var mesh = new MeshInstance3D { Mesh = new QuadMesh { Size = size }, Position = position,
            MaterialOverride = new ShaderMaterial { Shader = shader }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(mesh);
        return mesh;
    }

    public void SetAmounts(int marked, int stunned, int armor)
    {
        _mark.Visible = marked > 0;
        _stun.Visible = stunned > 0;
        _shards.Visible = armor > 0;
        // 欠片の数は装飾。正確な残量は既存の状態札が表示する。
        SetProcess(_shards.Visible);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        UpdateShards();
    }

    private void UpdateShards()
    {
        for (int i = 0; i < _pieces.Length; i++)
        {
            float angle = _time*0.75f+i*Mathf.Tau/_pieces.Length;
            _pieces[i].Position = new Vector3(Mathf.Cos(angle)*0.93f,0.8f+Mathf.Sin(angle*2+_time)*0.12f,Mathf.Sin(angle)*0.93f);
            _pieces[i].Rotation = new Vector3(0,angle,0.3f*Mathf.Sin(angle));
        }
    }
}
