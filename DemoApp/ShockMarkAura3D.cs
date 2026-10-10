using Godot;
using System;
using System.Collections.Generic;

// 蓄電、雷雲、標の層は互いに独立。汎用感電の消費で蓄電を消さない。
public partial class ShockMarkAura3D : Node3D
{
    private BattlePawn3D _owner = null!;
    private Sprite3D? _mark;
    private readonly List<Sprite3D> _satelliteMarks = new();
    private readonly List<Sprite3D> _clouds = new();
    private float _time, _sparkAt;
    internal void Configure(BattlePawn3D owner) => _owner = owner;

    internal void Refresh()
    {
        // 中心を主照準、周囲を積み重なった狙いにする。実数はHUD、描画は最大7個。
        if (_owner.MarkLayers > 0 && _mark is null)
        {
            _mark = ShockMarkFx.Sprite(this, _owner.FxPoint, ShockMarkFx.MarkTexture(_owner.Team), 1.10f, Colors.White);
            _mark.NoDepthTest = true;
            _mark.RenderPriority = 7;
        }
        if (_mark is not null) _mark.Visible = _owner.MarkLayers > 0;
        int satellites = Math.Clamp(_owner.MarkLayers - 1, 0, 6);
        while (_satelliteMarks.Count < satellites)
        {
            var mark = ShockMarkFx.Sprite(this, _owner.FxPoint, ShockMarkFx.MarkTexture(_owner.Team), 0.44f,
                _owner.Team == 0 ? Colors.White : new Color(1, 0.64f, 0.74f, 0.90f));
            mark.NoDepthTest = true;
            mark.RenderPriority = 6;
            _satelliteMarks.Add(mark);
        }
        for (int i = 0; i < _satelliteMarks.Count; i++) _satelliteMarks[i].Visible = i < satellites;
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
        if (_mark is not null)
        {
            _mark.GlobalPosition = _owner.FxPoint + front * 0.30f;
            _mark.Scale = Vector3.One * (1 + 0.035f * Mathf.Sin(_time * 2.5f));
        }
        int satellites = Math.Clamp(_owner.MarkLayers - 1, 0, 6);
        for (int i = 0; i < satellites; i++)
        {
            // 横へ広げすぎず、体を囲む縦長の軌道。層が深くても外周は増やさない。
            float angle = i * Mathf.Tau / satellites - Mathf.Pi * 0.5f + _time * 0.18f;
            _satelliteMarks[i].GlobalPosition = _owner.FxPoint + front * 0.26f
                + right * (Mathf.Cos(angle) * 0.78f) + up * (Mathf.Sin(angle) * 0.97f);
            _satelliteMarks[i].Scale = Vector3.One * (1 + 0.04f * Mathf.Sin(_time * 2 + i));
        }
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
