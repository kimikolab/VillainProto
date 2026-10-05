using Godot;
using System;

public partial class BattlePawn3D
{
    internal int FireHoard { get; private set; }
    internal int FireGiftHoard { get; private set; }
    internal int FireGiftOrder { get; private set; }
    private MeshInstance3D? _hoardSword, _giftEmber;
    private ShaderMaterial? _hoardSwordMaterial;
    private Label3D? _fireReserveLabel, _fireGiftOrderLabel;

    internal void SetFireHoard(int count)
    {
        FireHoard = Math.Max(0, count);
        if (_hoardSword is null && count > 0)
        {
            _hoardSwordMaterial = FireHoardFx.Material();
            _hoardSword = FireFx.Card(this, Vector3.Zero, Vector2.One, _hoardSwordMaterial);
        }
        ReadFireReserveLabel();
    }
    internal void SetFireGiftHoard(int count)
    {
        FireGiftHoard = Math.Max(0, count);
        if (_giftEmber is null && count > 0)
        {
            var material = FireFx.Material(7, new("a2e9ff"), _phase);
            material.SetShaderParameter("persistent", true);
            _giftEmber = FireFx.Card(this, Vector3.Zero, Vector2.One * 0.55f, material);
        }
        ReadFireReserveLabel();
    }
    private void ReadFireReserveLabel()
    {
        if (_fireReserveLabel is null && (FireHoard > 0 || FireGiftHoard > 0))
        {
            _fireReserveLabel = MakeLabel("", 21, new("c1f3ff"), 0.0062f);
            _fireReserveLabel.NoDepthTest = true;
            AddChild(_fireReserveLabel);
        }
        if (_fireReserveLabel is null) return;
        _fireReserveLabel.Text = FireHoard > 0 ? $"溜め火 {FireHoard}" : $"渡す火 {FireGiftHoard}";
        _fireReserveLabel.Position = _headLabelBase + Vector3.Up * 0.50f;
        _fireReserveLabel.Visible = _alive && !_victory && (FireHoard > 0 || FireGiftHoard > 0);
    }
    internal void SetFireGiftOrder(int order)
    {
        FireGiftOrder = order;
        if (_fireGiftOrderLabel is null && order > 0)
        {
            _fireGiftOrderLabel = MakeLabel("", 24, new("ffe5aa"), 0.0064f);
            _fireGiftOrderLabel.NoDepthTest = true;
            AddChild(_fireGiftOrderLabel);
        }
        if (_fireGiftOrderLabel is null) return;
        _fireGiftOrderLabel.Position = _headLabelBase + Vector3.Up * 0.73f;
        _fireGiftOrderLabel.Text = $"ギフト {order}";
        _fireGiftOrderLabel.Visible = order > 0 && _alive && !_victory;
    }
    private void UpdateFireGrowth()
    {
        if (_hoardSword is not null)
        {
            _hoardSword.Visible = FireHoard > 0 && _alive && !_victory;
            _hoardSword.Position = _sprite.Position + new Vector3(0, 0, 0.06f);
            _hoardSword.Scale = new(_sprite.Texture.GetWidth() * _sprite.PixelSize,
                _sprite.Texture.GetHeight() * _sprite.PixelSize, 1);
            _hoardSwordMaterial!.SetShaderParameter("clock", _fireClock);
            _hoardSwordMaterial.SetShaderParameter("strength", 0.55f + Math.Min(FireHoard, 12) * 0.13f);
            _hoardSwordMaterial.SetShaderParameter("flip", _sprite.FlipH);
            bool release = MovementPortrait == "borg_release";
            _hoardSwordMaterial.SetShaderParameter("start", release ? new Vector2(0.36f, 0.42f) : new Vector2(0.43f, 0.56f));
            _hoardSwordMaterial.SetShaderParameter("end", release ? new Vector2(0.97f, 0.63f) : new Vector2(0.96f, 0.89f));
        }
        if (_giftEmber is not null)
        {
            _giftEmber.Visible = FireGiftHoard > 0 && _alive && !_victory;
            _giftEmber.Position = _sprite.Position + new Vector3(0.45f, 0.2f + Mathf.Sin(_fireClock * 3) * 0.12f, 0.12f);
            _giftEmber.Scale = Vector3.One * (0.8f + FireGiftHoard * 0.2f);
            ((ShaderMaterial)_giftEmber.MaterialOverride).SetShaderParameter("clock", _fireClock);
        }
    }
}
