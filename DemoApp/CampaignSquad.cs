using Godot;
using System;

public partial class CampaignSquad : Node3D
{
    private Sprite3D _sprite = null!;
    private MeshInstance3D _selection = null!;
    private MeshInstance3D _shadow = null!;
    private Label3D _label = null!;
    private Vector3 _destination;
    private float _phase;
    private bool _moving;

    public string SquadId { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public int Team { get; private set; }
    public int StageIndex { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsMoving => _moving;
    public Vector3 Destination => _destination;

    public void Configure(string id, string displayName, int team, int stageIndex, Texture2D texture, Vector3 position)
    {
        SquadId = id;
        DisplayName = displayName;
        Team = team;
        StageIndex = stageIndex;
        Position = new Vector3(position.X, 0.08f, position.Z);
        _destination = Position;
        _phase = id.GetHashCode() * 0.013f;

        Color accent = team == 0 ? Color.FromHtml("#60d6bf") : Color.FromHtml("#ed6a52");

        _shadow = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1.0f, BottomRadius = 1.0f, Height = 0.025f, RadialSegments = 32 },
            Position = new Vector3(0, 0.015f, 0),
            MaterialOverride = CampaignMain.MakeMaterial(new Color(0.02f, 0.025f, 0.02f, 0.46f), transparent: true, unshaded: true),
        };
        AddChild(_shadow);

        _selection = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.76f, OuterRadius = 0.91f, Rings = 24, RingSegments = 8 },
            Position = new Vector3(0, 0.065f, 0),
            MaterialOverride = CampaignMain.MakeMaterial(new Color(accent, 0.88f), transparent: true, unshaded: true, emission: accent),
            Visible = false,
        };
        AddChild(_selection);

        var shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_opaque;
uniform sampler2D squad_texture : source_color, filter_nearest;
void fragment() {
    vec4 c = texture(squad_texture, UV);
    float neutral = max(abs(c.r - c.g), max(abs(c.g - c.b), abs(c.r - c.b)));
    float lightness = (c.r + c.g + c.b) / 3.0;
    // The generator baked a neutral checker into the RGB image. Remove only its
    // mid/high neutral range; darker armor and every colored character pixel remain.
    if (neutral < 0.20 && lightness > 0.34) discard;
    ALBEDO = c.rgb;
    ALPHA = c.a;
}"
        };
        var shaderMaterial = new ShaderMaterial { Shader = shader };
        shaderMaterial.SetShaderParameter("squad_texture", texture);

        _sprite = new Sprite3D
        {
            Texture = texture,
            PixelSize = 0.0022f,
            Position = new Vector3(0, 1.44f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            MaterialOverride = shaderMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_sprite);

        _label = new Label3D
        {
            Text = displayName,
            Position = new Vector3(0, 3.15f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Font = new SystemFont
            {
                FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" },
                AllowSystemFallback = true,
            },
            FontSize = 28,
            PixelSize = 0.007f,
            Modulate = Colors.White,
            OutlineModulate = new Color(0.01f, 0.015f, 0.012f, 0.95f),
            OutlineSize = 8,
            NoDepthTest = true,
        };
        AddChild(_label);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        _selection.Visible = selected;
        _label.Modulate = selected
            ? (Team == 0 ? Color.FromHtml("#93f3df") : Color.FromHtml("#ff9b83"))
            : Colors.White;
    }

    public void SetDestination(Vector3 destination)
    {
        _destination = new Vector3(destination.X, Position.Y, destination.Z);
        _moving = Position.DistanceTo(_destination) > 0.25f;
    }

    public void Stop()
    {
        _destination = Position;
        _moving = false;
    }

    public void Step(double delta, float speedScale = 1.0f)
    {
        float distance = Position.DistanceTo(_destination);
        if (distance > 0.12f)
        {
            Vector3 direction = (_destination - Position).Normalized();
            Position += direction * Math.Min(distance, 3.25f * speedScale * (float)delta);
            _moving = true;
            _sprite.FlipH = direction.X < -0.05f;
        }
        else
        {
            Position = _destination;
            _moving = false;
        }

        _phase += (float)delta * (_moving ? 9.0f : 2.1f);
        float bob = Mathf.Sin(_phase) * (_moving ? 0.12f : 0.045f);
        _sprite.Position = new Vector3(0, 2.0f + bob, 0);
        _sprite.Scale = new Vector3(1.0f + Mathf.Abs(bob) * 0.035f, 1.0f - Mathf.Abs(bob) * 0.025f, 1.0f);
        _selection.Rotation = new Vector3(0, _phase * 0.08f, 0);
    }
}
