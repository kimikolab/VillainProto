using BattleCore;
using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlePawn3D
{
    public bool SwordDrawn { get; private set; }
    public bool QuietLastStand { get; set; }
    public bool GurenReleasing { get; private set; }
    private MeshInstance3D? _gurenFlower;
    private float _scarGlow;
    private float _numbTime, _numbStrength;
    private float _gurenImpact;
    public void FlashGurenImpact() => _gurenImpact = 0.38f;
    public Vector3 FlowerPoint => GlobalPosition + new Vector3(Team == 0 ? 0.40f : -0.40f, _portraitHeight * 0.77f, 0.12f);
    internal int GurenAmount => _statusIcons.Amount(StatusKeys.Guren);

    public void SetGuren(int amount, bool animate = true)
    {
        if (!_alive || _victory) return;
        _statusIcons.SetAmount(StatusKeys.Guren, amount, animate);
        if (amount <= 0)
        {
            _gurenFlower?.QueueFree(); _gurenFlower = null;
            return;
        }
        _gurenFlower ??= SpecialsFx.Card(this, ToLocal(FlowerPoint), new Vector2(0.57f, 0.57f), 0, new Color("ed294f"));
        var material = (ShaderMaterial)_gurenFlower.MaterialOverride;
        material.SetShaderParameter("strength", Math.Min(1f, amount / 12f));
        material.SetShaderParameter("ready", amount >= 12);
    }

    public void SetGurenRelease(bool active)
    {
        GurenReleasing = active;
        RefreshBattlePortrait();
    }

    public void DrawSword(int scars)
    {
        SwordDrawn = true;
        _scarGlow = Math.Clamp(scars / 24f, 0, 1);
        RefreshBattlePortrait();
    }

    public async Task NumbWindup(int percent)
    {
        _numbStrength = Math.Clamp(percent / 60f, 0, 1);
        _numbTime = 0.54f;
        await ToSignal(GetTree().CreateTimer(Math.Max(0.01, 0.24 / AnimationSpeed)), SceneTreeTimer.SignalName.Timeout);
    }

    private void UpdateSpecialEffects(float delta)
    {
        if (_gurenFlower is not null)
            _gurenFlower.Position = ToLocal(FlowerPoint);
        if (_numbTime > 0)
        {
            _numbTime = Math.Max(0, _numbTime - delta);
            _sprite.Position += Vector3.Right * Mathf.Sin(_numbTime * 150) * _numbStrength * 0.09f;
        }
        _portraitMaterial.SetShaderParameter("numb_amount", _numbTime > 0 ? _numbStrength : 0f);
        _portraitMaterial.SetShaderParameter("scar_glow", SwordDrawn ? _scarGlow : 0f);
        _gurenImpact = Math.Max(0, _gurenImpact - delta);
        _portraitMaterial.SetShaderParameter("guren_flash", Mathf.Sin(_gurenImpact / 0.38f * Mathf.Pi));
    }

    private void ClearSpecialEffects()
    {
        _gurenFlower?.QueueFree(); _gurenFlower = null;
        _numbTime = 0;
        _gurenImpact = 0;
        _portraitMaterial.SetShaderParameter("guren_flash", 0f);
        _portraitMaterial.SetShaderParameter("numb_amount", 0f);
        _portraitMaterial.SetShaderParameter("scar_glow", 0f);
    }
}
