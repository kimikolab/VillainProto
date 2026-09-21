using Godot;

// 台本の混乱状態に追従する、頭上のヒヨコ。戦闘の判定は持たない。
public partial class ConfusionEffect3D : Node3D
{
    private readonly Sprite3D[] _chicks = new Sprite3D[3];
    private float _angle;
    private static Texture2D? _texture;

    public void Configure(float height, float phase)
    {
        Position = new Vector3(0, height + 0.12f, 0);
        _angle = phase;
        if (_texture is null)
        {
            var image = new Image();
            image.LoadSvgFromString("""
                <svg xmlns="http://www.w3.org/2000/svg" width="96" height="80" viewBox="0 0 96 80">
                  <g stroke="#79501b" stroke-width="3" stroke-linejoin="round">
                    <path d="M32 62l-5 9m17-8l-1 9" fill="none" stroke="#f7a12d"/>
                    <path d="M21 40L9 30l3 23" fill="#ffe05b"/>
                    <ellipse cx="39" cy="47" rx="26" ry="22" fill="#ffe05b"/>
                    <path d="M43 35q-26-12-19 9q5 13 19-9" fill="#fff397"/>
                    <path d="M50 18l-4-12 11 8 7-10 1 15" fill="#ffe05b"/>
                    <circle cx="61" cy="28" r="19" fill="#ffe76a"/>
                    <path d="M77 26l15 7-16 5" fill="#ff9c30"/>
                  </g>
                  <circle cx="67" cy="24" r="4" fill="#34261a"/>
                  <circle cx="68" cy="23" r="1.2" fill="white"/>
                  <ellipse cx="68" cy="36" rx="5" ry="3" fill="#ffa15c"/>
                </svg>
                """);
            _texture = ImageTexture.CreateFromImage(image);
        }
        for (int i = 0; i < _chicks.Length; i++)
        {
            _chicks[i] = new Sprite3D
            {
                Texture = _texture,
                PixelSize = 0.0058f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Shaded = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_chicks[i]);
        }
        UpdateOrbit();
        SetActive(false);
    }

    public void SetActive(bool active)
    {
        Visible = active;
        SetProcess(active);
    }

    public override void _Process(double delta)
    {
        _angle = Mathf.PosMod(_angle + (float)delta * 3.3f, Mathf.Tau);
        UpdateOrbit();
    }

    private void UpdateOrbit()
    {
        for (int i = 0; i < _chicks.Length; i++)
        {
            float angle = _angle + i * Mathf.Tau / _chicks.Length;
            _chicks[i].Position = new Vector3(Mathf.Cos(angle) * 0.78f,
                Mathf.Sin(angle * 2) * 0.08f, Mathf.Sin(angle) * 0.40f);
            _chicks[i].FlipH = Mathf.Sin(angle) > 0;
        }
    }
}
