using Godot;

/// <summary>第四波の城門前。中央の戦闘域は平らに保ち、城壁と守備隊の旗で場所を示す。</summary>
public partial class FortressEnvironment3D : Node3D
{
    public override void _Ready()
    {
        var stone = new ShaderMaterial { Shader = new Shader { Code = @"shader_type spatial;
varying vec3 p;
varying vec3 n;
void vertex() { p = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz; n = MODEL_NORMAL_MATRIX * NORMAL; }
float hash(vec2 v) { return fract(sin(dot(v,vec2(127.1,311.7)))*43758.54); }
void fragment() {
    vec2 uv = abs(n.y)>0.7 ? p.xz : vec2(abs(n.z)>0.5?p.x:p.z,p.y);
    uv /= vec2(0.95,0.52);
    uv.x += mod(floor(uv.y),2.0)*0.5;
    vec2 f = fract(uv);
    float edge = min(min(f.x,1.0-f.x),min(f.y,1.0-f.y));
    float mortar = smoothstep(0.015,0.055,edge);
    float variation = hash(floor(uv));
    float grain = hash(floor(p.xz*85.0+p.y*29.0));
    vec3 slab = mix(vec3(0.13,0.15,0.155),vec3(0.26,0.27,0.25),variation);
    ALBEDO = mix(vec3(0.065,0.072,0.07),slab*(0.88+grain*0.2),mortar);
    ROUGHNESS = 0.96;
}" } };
        var iron = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#282e32"), Metallic = 0.65f, Roughness = 0.7f };
        var wood = new ShaderMaterial { Shader = new Shader { Code = @"shader_type spatial;
varying vec3 p;
void vertex() { p = (MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
    float grain = sin(p.x*73.0+sin(p.y*3.0)*2.0)*sin(p.x*29.0+p.y*0.3);
    ALBEDO = mix(vec3(0.055,0.029,0.016),vec3(0.15,0.083,0.034),grain*0.5+0.5);
    ROUGHNESS = 0.9;
}" } };
        Box(new Vector3(0,-0.13f,0), new Vector3(42,0.24f,38), stone);
        // 門の左右の壁と、奥行きのある控え壁。
        Box(new Vector3(-8,1.6f,-7), new Vector3(12.5f,3.2f,1), stone);
        Box(new Vector3(8,1.6f,-7), new Vector3(12.5f,3.2f,1), stone);
        for (int i = -14; i <= 14; i++)
        {
            if (Mathf.Abs(i) < 2) continue;
            Box(new Vector3(i,3.48f,-7),new Vector3(0.55f,0.56f,1.05f),stone);
        }
        foreach (float x in new[] { -7.8f, 7.8f })
        {
            Box(new Vector3(x,1.9f,-6.8f),new Vector3(2.15f,3.8f,2.1f),stone);
            Box(new Vector3(x,3.65f,-6.8f),new Vector3(2.4f,0.22f,2.35f),stone);
            for (int i = -1; i <= 1; i++)
                Box(new Vector3(x+i*0.8f,4.05f,-5.85f),new Vector3(0.45f,0.6f,0.48f),stone);
            Box(new Vector3(x,2.45f,-5.73f),new Vector3(0.18f,0.8f,0.02f),iron);
        }
        foreach (float x in new[] { -11f, 11f })
        {
            Box(new Vector3(x,1.1f,-0.8f),new Vector3(1,2.2f,11.5f),stone);
            for (int i = -5; i <= 4; i++)
                Box(new Vector3(x,2.45f,i),new Vector3(1.05f,0.5f,0.5f),stone);
        }
        // アーチは一つずつの迫石で組み、門扉も上端をアーチに沿わせる。
        for (int i = 0; i < 13; i++)
        {
            float angle = Mathf.Pi * (i + 0.5f) / 13;
            var block = Box(new Vector3(Mathf.Cos(angle)*1.8f,1.25f+Mathf.Sin(angle)*1.8f,-6.85f),
                new Vector3(0.46f,0.63f,1.3f),stone);
            block.Rotation = new Vector3(0,0,angle-Mathf.Pi/2);
        }
        foreach (float x in new[] { -1.8f,1.8f })
            Box(new Vector3(x,0.62f,-6.85f),new Vector3(0.6f,1.25f,1.3f),stone);
        for (int i = -4; i <= 4; i++)
        {
            float x = i*0.33f;
            float height = 1.25f+Mathf.Sqrt(1.52f*1.52f-x*x);
            Box(new Vector3(x,height/2,-7.2f),new Vector3(0.31f,height,0.22f),wood);
        }
        foreach (float y in new[] { 0.45f,1.25f,2.0f })
            Box(new Vector3(0,y,-7.04f),new Vector3(3,0.13f,0.1f),iron);

        BuildBanners(iron);
        foreach (float x in new[] { -2.8f,2.8f }) BuildBrazier(x,stone,iron);
        foreach (float x in new[] { -8.7f,8.7f })
        {
            var crate = Box(new Vector3(x,0.45f,-2),new Vector3(1.05f,0.9f,0.95f),wood);
            crate.RotationDegrees = new Vector3(0,x*3,0);
            Box(new Vector3(x,0.15f,-2.9f),new Vector3(0.8f,0.3f,0.6f),stone);
        }
    }

    private MeshInstance3D Box(Vector3 position, Vector3 size, Material material)
    {
        var mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = material };
        AddChild(mesh);
        return mesh;
    }

    private void BuildBanners(Material iron)
    {
        var cloth = new ShaderMaterial { Shader = new Shader { Code = @"shader_type spatial;
render_mode cull_disabled;
void vertex() { VERTEX.y += sin(TIME*1.8+VERTEX.z*4.0+MODEL_MATRIX[3].x)*0.065*UV.y; }
void fragment() {
    if (UV.y>0.87+abs(UV.x-0.5)*0.25) discard;
    float gold = step(abs(UV.x-0.5)+abs(UV.y-0.40)*0.65,0.18);
    gold = max(gold,1.0-step(0.045,min(UV.x,1.0-UV.x)));
    ALBEDO = mix(vec3(0.19,0.025,0.033),vec3(0.54,0.37,0.12),gold);
    ROUGHNESS = 0.95;
}" } };
        foreach (float x in new[] { -4.6f,4.6f })
        {
            Box(new Vector3(x,2.9f,-6.28f),new Vector3(1.35f,0.07f,0.1f),iron);
            AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(1.12f,1.65f), SubdivideDepth = 12 },
                Position = new Vector3(x,2.05f,-6.25f), RotationDegrees = new Vector3(90,0,0), MaterialOverride = cloth,
            });
        }
    }

    private void BuildBrazier(float x, Material stone, Material iron)
    {
        Box(new Vector3(x,0.48f,-5.7f),new Vector3(0.45f,0.96f,0.45f),stone);
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.16f, Height = 0.25f },
            Position = new Vector3(x,1.02f,-5.7f), MaterialOverride = iron,
        });
        var flame = new ShaderMaterial { Shader = new Shader { Code = @"shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never;
void fragment() {
    float y = 1.0-UV.y;
    float x = abs(UV.x-0.5+sin(y*11.0-TIME*6.0)*0.06*y);
    float a = (1.0-smoothstep(0.0,0.32*(1.0-y),x))*(1.0-smoothstep(0.55,0.95,y));
    ALBEDO = mix(vec3(1.0,0.8,0.2),vec3(1.0,0.15,0.01),y);
    ALPHA = a; EMISSION = ALBEDO*2.0;
}" } };
        AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(0.75f,0.95f) }, Position = new Vector3(x,1.48f,-5.7f),MaterialOverride=flame });
        AddChild(new OmniLight3D { Position = new Vector3(x,1.5f,-5.4f), LightColor = Color.FromHtml("#ffac55"), LightEnergy = 1.0f, OmniRange = 3.0f });
    }
}
