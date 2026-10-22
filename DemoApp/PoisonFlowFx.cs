using Godot;
using System;
using System.Threading.Tasks;

// 流れの形と購入毒煙を束ねる。一時停止・倍速・破棄は発動ごとの時計だけで制御する。
public partial class PoisonFlowFx : Node3D
{
    internal const string SmokePath = "res://PolyBlocks/EffectBlocks/assets/smoke/smoke_toxic.tscn";
    internal enum Kind { Vent, Retort, Drain, Blood, Bite, Guren, Touch, Leak, Contagion, Contact }
    private static Shader? _shader;
    private MeshInstance3D[] _drops = Array.Empty<MeshInstance3D>();
    private ShaderMaterial[] _materials = Array.Empty<ShaderMaterial>();
    private GpuParticles3D? _smoke;
    private Vector3 _from, _to, _side;
    private Kind _kind;
    private float _weight;
    private double _age, _seconds;
    private Func<(bool Paused, double Speed)> _clock = () => (false, 1);
    internal bool UsesPurchasedSmoke => _smoke is not null;
    internal double Age => _age;
    internal Kind FlowKind => _kind;
    internal Vector3 From => _from;
    internal Vector3 To => _to;
    private readonly TaskCompletionSource<bool> _arrival = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<bool> Arrival => _arrival.Task;

    internal void Start(Vector3 from, Vector3 to, Kind kind, int amount,
        Func<(bool Paused, double Speed)> clock, bool purchased = true)
    {
        _from = from; _to = to; _kind = kind; _clock = clock;
        _side = (to - from).Cross(Vector3.Up).Normalized();
        _weight = Math.Clamp((float)Math.Log(1.0 + Math.Max(0, amount)) / 10f, 0.12f, 1f);
        if (kind == Kind.Vent) _weight = Math.Clamp(amount / 6f, 0.15f, 1f);
        _seconds = kind switch { Kind.Drain => 0.65, Kind.Blood => 0.55, Kind.Bite => 0.32,
            Kind.Vent => 0.60, Kind.Touch => 0.52, Kind.Leak => 0.64, Kind.Contagion => 0.48,
            Kind.Contact => 0.20, _ => 0.42 };
        int count = kind == Kind.Bite ? 6 : 6 + (int)(_weight * 10);
        if (kind == Kind.Guren) count = 6;
        if (kind == Kind.Leak) count = 11;
        _drops = new MeshInstance3D[count]; _materials = new ShaderMaterial[count];
        _shader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_mix,depth_draw_never;
uniform vec4 tint : source_color;
uniform float progress=0.0;
uniform bool light=false;
uniform bool tooth=false;
uniform bool veil=false;
uniform bool ground=false;
uniform float tooth_direction=1.0;
void vertex(){if(!ground){MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);}}
void fragment(){
 vec2 p=(UV-0.5)*2.0;
 float radius=length(p),angle=atan(p.y,p.x);
 float lobes=light?1.0:0.85+0.12*sin(angle*5.0+progress*7.0);
 float blob=1.0-smoothstep(lobes*0.62,lobes,radius);
 float edge=1.0-smoothstep(0.02,0.10,abs(radius-(0.35+progress*0.45)));
 if(tooth){p.y*=tooth_direction;float width=max(0.0,(1.0-p.y)*0.32);blob=(1.0-smoothstep(width,width+0.08,abs(p.x)))*(1.0-smoothstep(0.80,1.0,abs(p.y)));edge=0.0;}
 if(veil){float curl=sin(p.x*6.0+progress*11.0+sin(p.y*8.0)*1.1)*sin(p.y*8.0-progress*7.0);blob=exp(-dot(p*1.2,p*1.2))*smoothstep(-0.55,0.55,curl)*0.48;edge=0.0;}
 ALBEDO=mix(tint.rgb*0.20,tint.rgb,clamp(1.0-radius+edge*0.5,0.0,1.0));
 EMISSION=tint.rgb*(light?1.8:0.30);
 ALPHA=(blob*0.85+edge*0.25)*sin(progress*3.14159);
}" };
        Color tint = kind switch { Kind.Blood => new Color("ff395f"), Kind.Drain => new Color("7534a3"),
            Kind.Guren => new Color("e83d50"), Kind.Contact => new Color("d4c5ae"), _ => new Color("ba56f4") };
        for (int k = 0; k < count; k++)
        {
            var material = new ShaderMaterial { Shader = _shader, RenderPriority = 7 };
            material.SetShaderParameter("tint", kind == Kind.Guren && k % 3 == 0 ? new Color("782f9e") : tint);
            material.SetShaderParameter("light", kind == Kind.Blood);
            material.SetShaderParameter("tooth", kind == Kind.Bite);
            material.SetShaderParameter("veil", kind == Kind.Guren);
            material.SetShaderParameter("ground", kind == Kind.Leak);
            material.SetShaderParameter("tooth_direction", k < 3 ? 1f : -1f);
            _materials[k] = material;
            float size = kind == Kind.Blood ? 0.13f : kind == Kind.Bite ? 0.20f : 0.18f + _weight * 0.36f;
            if (kind == Kind.Guren) size = 0.65f + _weight * 0.90f;
            if (kind is Kind.Touch or Kind.Contact) size = 0.12f + _weight * 0.12f;
            if (kind == Kind.Leak) size = 0.30f;
            if (kind == Kind.Contagion) size = 0.22f + _weight * 0.18f;
            var drop = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * size }, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            _drops[k] = drop; AddChild(drop);
            if (kind == Kind.Leak) drop.Rotation = new Vector3(-Mathf.Pi * 0.5f, 0, 0);
        }
        if (purchased && kind is Kind.Vent or Kind.Retort or Kind.Guren or Kind.Contagion && ResourceLoader.Exists(SmokePath))
        {
            _smoke = GD.Load<PackedScene>(SmokePath).Instantiate<GpuParticles3D>();
            _smoke.SetScript(default);
            var process = (ParticleProcessMaterial)_smoke.ProcessMaterial.Duplicate(true);
            process.Gravity = Vector3.Up * 0.20f;
            process.EmissionSphereRadius = 0.06f;
            process.ScaleMin = 0.10f + _weight * 0.10f; process.ScaleMax = 0.22f + _weight * 0.15f;
            process.Color = Colors.White;
            process.ColorRamp = new GradientTexture1D { Gradient = new Gradient {
                Offsets = new[] { 0f, 0.3f, 1f }, Colors = new[] { new Color("862ca982"), new Color("54226860"), new Color("38204400") } } };
            _smoke.ProcessMaterial = process;
            var draw = (StandardMaterial3D)_smoke.DrawPass1.SurfaceGetMaterial(0).Duplicate(true);
            draw.BlendMode = BaseMaterial3D.BlendModeEnum.Mix;
            _smoke.MaterialOverride = draw;
            _smoke.Amount = 8 + (int)(_weight * 8); _smoke.Lifetime = 0.45;
            _smoke.LocalCoords = true;
            _smoke.OneShot = true; _smoke.Explosiveness = 0.8f; _smoke.Preprocess = 0;
            _smoke.UseFixedSeed = true; _smoke.Seed = 376;
            _smoke.Emitting = false; AddChild(_smoke); _smoke.GlobalPosition = from;
            _smoke.Emitting = true; _smoke.Restart();
        }
        Draw(0);
    }

    public override void _Process(double delta)
    {
        var (paused, speed) = _clock();
        if (_smoke is not null) _smoke.SpeedScale = paused ? 0 : speed;
        if (paused) return;
        _age += delta * speed;
        Draw((float)Math.Min(1, _age / _seconds));
        if (_age >= _seconds) _arrival.TrySetResult(true);
        if (_age >= _seconds + 0.12) QueueFree();
    }
    public override void _ExitTree() => _arrival.TrySetResult(false);

    private void Draw(float p)
    {
        for (int k = 0; k < _drops.Length; k++)
        {
            float t = Mathf.Clamp((p - k * 0.012f) / 0.80f, 0, 1);
            float arc = Mathf.Sin(t * Mathf.Pi);
            bool pull = _kind is Kind.Drain or Kind.Blood;
            float travel = pull ? t * t : t;
            Vector3 at = _from.Lerp(_to, travel);
            if (_kind == Kind.Leak)
            {
                // 小さい滴が足元から順に滲む。空中の伝染と形・高さを分ける。
                t = Mathf.Clamp((p - k * 0.055f) / 0.38f, 0, 1);
                at = _from.Lerp(_to, k / (float)(_drops.Length - 1))
                    + _side * Mathf.Sin(k * 2.4f) * 0.07f;
                _drops[k].Scale = Vector3.One * (0.35f + t * 0.75f);
            }
            else if (_kind is Kind.Contagion or Kind.Contact)
            {
                // 死骸のIDは台本にない。発生元を補わず、実際の受け手だけに局所的な飛沫を描く。
                float angle = k * 2.4f;
                float radius = (_kind == Kind.Contact ? 0.18f : 0.42f) * arc;
                at = _to + new Vector3(Mathf.Cos(angle) * radius,
                    (t - 0.5f) * (_kind == Kind.Contact ? 0.30f : 0.80f), Mathf.Sin(angle) * radius);
                _drops[k].Scale = Vector3.One * (0.60f + arc * 0.55f);
            }
            else if (_kind == Kind.Bite)
            {
                // 対になる牙が上下から閉じる。血や肉片は描かない。
                float side = k < 3 ? 1 : -1;
                at = _to + Vector3.Up * side * (0.45f * (1 - p) + 0.05f)
                    + _side * (k % 3 - 1) * 0.22f;
                _drops[k].Scale = new Vector3(0.40f, 1.8f, 1);
            }
            else
            {
                float curl = _kind == Kind.Guren ? 0.60f : _kind == Kind.Vent ? 0.45f : _kind == Kind.Touch ? 0.28f : pull ? 0.25f : 0.14f;
                at += _side * arc * Mathf.Sin(t * Mathf.Tau + k) * curl * (pull ? 1 - t : 1)
                    + Vector3.Up * arc * (_kind == Kind.Touch ? 0.55f + k % 3 * 0.16f
                        : _kind == Kind.Vent ? 0.40f : 0.18f + k % 3 * 0.09f);
                _drops[k].Scale = Vector3.One * (pull ? 1 - travel * 0.65f : 0.7f + t * 0.6f);
            }
            _drops[k].GlobalPosition = at;
            _drops[k].Visible = p > k * (_kind == Kind.Leak ? 0.055f : 0.012f) && p < 1;
            _materials[k].SetShaderParameter("progress", _kind == Kind.Bite ? p : t);
        }
        if (_smoke is not null) _smoke.GlobalPosition = _from.Lerp(_to, p);
    }
}
