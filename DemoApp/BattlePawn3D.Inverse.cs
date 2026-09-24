using Godot;

public partial class BattlePawn3D
{
    private MeshInstance3D? _inverseBarrier;
    internal bool InverseBarrierVisible => _inverseBarrier?.Visible == true;
    public void SetInverseBarrier(bool covered)
    {
        covered &= _alive && !_victory && Hp > 0;
        if (!covered && _inverseBarrier is null) return;
        if (_inverseBarrier is null)
        {
            var material = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never;
void fragment() {
    vec2 p = UV - vec2(0.5);
    float d = length(p);
    float ring = 1.0 - smoothstep(0.008, 0.026, abs(d - 0.40));
    float inner = (1.0 - smoothstep(0.02, 0.37, d)) * 0.10;
    float ripple = 0.75 + 0.18 * sin(TIME * 2.0 + atan(p.y,p.x) * 3.0);
    ALBEDO = vec3(0.80, 0.08, 0.33);
    EMISSION = ALBEDO * 0.6;
    ALPHA = (ring * 0.45 + inner) * ripple;
}" } };
            _inverseBarrier = new MeshInstance3D
            {
                Name = "InverseBarrier", Mesh = new QuadMesh { Size = new Vector2(2.7f, 2.7f) },
                RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(0, 0.075f, 0),
                MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_inverseBarrier);
        }
        _inverseBarrier.Visible = covered;
    }
}
