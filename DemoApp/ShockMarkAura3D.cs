using Godot;
using System;
using System.Collections.Generic;

// 蓄電、雷雲、標の層は互いに独立。汎用感電の消費で蓄電を消さない。
public partial class ShockMarkAura3D : Node3D
{
    private BattlePawn3D _owner = null!;
    private readonly List<Sprite3D> _marks = new();
    private readonly List<Sprite3D> _clouds = new();
    private float _time, _sparkAt;
    private Label3D? _layerNumber;
    internal void Configure(BattlePawn3D owner) => _owner = owner;

    internal void Refresh()
    {
        // 深い層も値は丸めない。密集した分は数値を併記して描画物だけ抑える。
        int visibleMarks = Math.Min(_owner.MarkLayers, 32);
        while (_marks.Count < visibleMarks)
        {
            var marker = ShockMarkFx.Sprite(this, _owner.FxPoint, ShockMarkFx.Reticle, 0.64f, new Color("ff5579"));
            marker.NoDepthTest = true;
            marker.RenderPriority = 7;
            _marks.Add(marker);
        }
        for (int i = 0; i < _marks.Count; i++) _marks[i].Visible = i < visibleMarks;
        if (_owner.MarkLayers > 0)
        {
            if (_layerNumber is null)
            {
                _layerNumber = new Label3D { FontSize = 34, OutlineSize = 8, PixelSize = 0.009f,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color("ffd4db"),
                    NoDepthTest = true, RenderPriority = 8 };
                AddChild(_layerNumber);
            }
            _layerNumber.Text = "×" + _owner.MarkLayers;
        }
        if (_layerNumber is not null) _layerNumber.Visible = _owner.MarkLayers > 0;
        int clouds = _owner.Thundercloud == 0 ? 0 : 1 + (_owner.Thundercloud + 1) / 3;
        while (_clouds.Count < clouds)
            _clouds.Add(ShockMarkFx.Sprite(this, _owner.FxPoint, ShockMarkFx.Cloud, 1.55f, Colors.White));
        for (int i = 0; i < _clouds.Count; i++) _clouds[i].Visible = i < clouds;
    }

    public override void _Process(double delta)
    {
        Visible = _owner.ShockMarkActive;
        if (!Visible) return;
        float dt = (float)(delta * _owner.AnimationSpeed);
        _time += dt; _sparkAt -= dt;
        var camera = GetViewport().GetCamera3D();
        var right = camera?.GlobalBasis.X ?? Vector3.Right;
        var up = camera?.GlobalBasis.Y ?? Vector3.Up;
        var front = camera?.GlobalBasis.Z ?? Vector3.Back;
        for (int i = 0; i < _marks.Count; i++)
        {
            int ring = i / 8;
            float a = i % 8 * Mathf.Tau / Math.Min(8, Math.Max(1, _owner.MarkLayers - ring * 8)) + _time * (ring % 2 == 0 ? 0.25f : -0.18f);
            float radius = _owner.MarkLayers == 1 ? 0 : 0.82f + ring * 0.21f;
            _marks[i].GlobalPosition = _owner.FxPoint + front * 0.25f
                + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
            _marks[i].Modulate = new Color("ff5579") { A = 0.87f + 0.12f * Mathf.Sin(_time * 3 + i) };
        }
        if (_layerNumber is not null) _layerNumber.GlobalPosition = _owner.FxPoint + front * 0.30f + up * 0.72f + right * 0.98f;
        for (int i = 0; i < _clouds.Count; i++)
        {
            var cloud = _clouds[i];
            cloud.GlobalPosition = _owner.FxPoint + Vector3.Up * (1.9f + i * 0.14f)
                + right * (Mathf.Sin(_time * 0.38f + i * 2.1f) * (0.10f + i * 0.10f)) - front * (0.20f + i * 0.05f);
            cloud.Scale = Vector3.One * (0.72f + _owner.Thundercloud * 0.055f + i * 0.06f);
            float flash = Mathf.Pow(Mathf.Max(0, Mathf.Sin(_time * 3.2f + i)), 18) * 0.32f;
            cloud.Modulate = new Color(0.76f + flash, 0.84f + flash, 1, 0.80f);
        }
        if (_sparkAt > 0) return;
        _sparkAt = Math.Max(0.08f, 0.36f - _owner.StoredCharge * 0.055f);
        int charge = _owner.StoredCharge;
        for (int i = 0; i < charge; i++)
        {
            float a = _time * 4 + i * 2.4f;
            var p = _owner.FxPoint + right * (Mathf.Cos(a) * 0.42f) + up * (Mathf.Sin(a) * 0.75f);
            ThunderFx.Arc(this, p, p + right * 0.20f + up * 0.35f, 0.012f + charge * 0.003f, 0.18 / _owner.AnimationSpeed);
        }
        if (_owner.Thundercloud > 0 && Mathf.Sin(_time * 3.2f) > 0.9f)
        {
            var p = _owner.FxPoint + Vector3.Up * 1.95f;
            ThunderFx.Arc(this, p - right * 0.6f, p + right * 0.65f, 0.018f + _owner.Thundercloud * 0.004f, 0.16 / _owner.AnimationSpeed);
        }
    }
}
