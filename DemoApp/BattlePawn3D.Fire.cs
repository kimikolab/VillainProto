using BattleCore;
using Godot;
using System;

public partial class BattlePawn3D
{
    private ShaderMaterial? _fireBodyMaterial, _fireFloorMaterial, _fireMarkMaterial, _fireRiseMaterial;
    private ShaderMaterial? _fireMantleMaterial;
    private Texture2D? _fireMantlePortrait;
    private MeshInstance3D? _fireBody, _fireFloor, _fireMark, _fireRise;
    private OmniLight3D? _fireLight;
    private float _fireClock, _firePulse, _fireSpent;
    private float _fireMantleReach = 0.55f;
    private int _fireRemaining = 1;
    internal int FireLevel { get; private set; }
    internal bool FireMarkActive { get; private set; }
    internal bool FireInvasive { get; private set; }
    internal int FireRemaining => _fireRemaining;

    private void BuildFireVisual()
    {
        _fire = new Node3D { Visible = false };
        AddChild(_fire);
        _fireBodyMaterial = FireFx.Material(0, FireFx.ColorOf(1), _phase);
        _fireBodyMaterial.SetShaderParameter("persistent", true);
        _fireBody = FireFx.Card(_fire, new(0, 0.8f, 0), new(2.1f, 2.0f), _fireBodyMaterial);
        _fireMantleMaterial = FireMantleFx.Material(false, _phase);
        _fireRiseMaterial = FireMantleFx.Material(true, _phase + 2);
        _fireRise = FireFx.Card(_fire, new(0, 1.3f, 0.12f), new(2.1f, 2.0f), _fireRiseMaterial);
        _fireRise.Visible = false;
        _fireFloorMaterial = FireFx.Material(2, FireFx.ColorOf(1), _phase);
        _fireFloorMaterial.SetShaderParameter("persistent", true);
        _fireFloorMaterial.SetShaderParameter("progress", 0.45f);
        _fireFloor = FireFx.Card(_fire, new(0, 0.08f, 0), Vector2.One * 2, _fireFloorMaterial, true);
        _fireLight = new OmniLight3D { Position = new(0, 0.7f, 0), OmniRange = 2.8f,
            LightEnergy = 0.25f, ShadowEnabled = false };
        _fire.AddChild(_fireLight);
        _fireMarkMaterial = FireFx.Material(4, new Color("ffe7a0"));
        _fireMarkMaterial.SetShaderParameter("persistent", true);
        _fireMark = FireFx.Card(this, new(0, 1.5f, 0.15f), Vector2.One * 0.7f, _fireMarkMaterial);
        _fireMark.Visible = false;
    }

    internal void SetFireLevel(int level, bool spent = false)
    {
        if (FireLevel == 0 && level > 0) FireInvasive = Team == BattleContext.EnemyTeam;
        FireLevel = Math.Clamp(level, 0, 4);
        _firePulse = spent ? 0 : 1;
        if (spent) _fireSpent = 1;
        if (level == 0) SetFireMark(false);
        SetBurning(level > 0);
    }

    internal void SetFireRemaining(int remaining)
    {
        _fireRemaining = Math.Max(0, remaining);
    }

    internal void SetFireMark(bool active)
    {
        FireMarkActive = active && _alive && !_victory;
        if (_fireMark is not null) _fireMark.Visible = FireMarkActive;
    }

    internal void SetFireInvasive(bool invasive) => FireInvasive = invasive;

    // 表示だけを一拍強める。火勢・燃焼残り・手番の規則には触れない。
    internal void PulseFireGift() => _firePulse = 2.2f;

    private string _firePowerText = "";
    private Label3D? _firePowerLabel;
    internal string FirePowerText => _firePowerText;
    internal void ShowFirePower(FireAttackCue cue, int power)
    {
        _firePowerText = FirePresentation.PowerText(cue, power);
        if (_firePowerLabel is null)
        {
            _firePowerLabel = MakeLabel("", 23, new("fff0ae"), 0.0062f);
            _firePowerLabel.NoDepthTest = true;
            AddChild(_firePowerLabel);
        }
        _firePowerLabel.Position = _stats.Position + Vector3.Up * 0.28f;
        _firePowerLabel.Text = _firePowerText;
        _firePowerLabel.Visible = _alive && !_victory;
    }
    internal void ClearFirePower()
    {
        _firePowerText = "";
        _firePowerLabel?.Hide();
    }

    private void ClearFireVisual()
    {
        ClearFirePower();
        SetFireHoard(0);
        SetFireGiftHoard(0);
        SetFireGiftOrder(0);
        UpdateFireGrowth();
        FireLevel = 0;
        _fireRemaining = 1;
        FireInvasive = false;
        _fireSpent = _firePulse = 0;
        _fireMantleReach = 0.55f;
        SetFireMark(false);
        if (_portraitMaterial is not null)
        {
            _portraitMaterial.SetShaderParameter("fire_heat", 0f);
            _portraitMaterial.SetShaderParameter("fire_damage", 0f);
        }
    }

    private void UpdateFireVisual(float delta)
    {
        if (_fireBodyMaterial is null) return;
        float dt = delta * (float)Math.Clamp(AnimationSpeed, 0.1, 8);
        _fireClock += dt;
        UpdateFireGrowth();
        _firePulse = Math.Max(0, _firePulse - dt * 2);
        _fireSpent = Math.Max(0, _fireSpent - dt * 2.5f);
        int level = Math.Max(1, FireLevel);
        var color = FireFx.ColorOf(level);
        // 残り時間の補正と放出直後の縮み。味方の炎の基本の広がりは火勢から決める。
        float size = (0.65f + Math.Min(_fireRemaining, 6) * 0.09f) * (1 - _fireSpent * 0.48f);
        bool aura = Team == BattleContext.PlayerTeam && !FireInvasive;
        float mantle = _unitId == "borg" ? 1.35f : 1f;
        float levelReach = level switch { 1 => 0.55f, 2 => 0.95f, 3 => 1.55f, _ => 2.35f };
        _fireMantleReach = Mathf.Lerp(_fireMantleReach, levelReach * size, 1 - Mathf.Exp(-dt * 9));
        // キャンバスだけを広げ、UVも同じ倍率で戻す。立ち絵から火が離れない。
        float auraPadding = 1.20f + _fireMantleReach * 0.40f;
        // シルエットと武器に密着して纏う。立ち絵の差し替えと動きにも毎フレーム合わせる。
        float auraHeight = _sprite.Texture.GetHeight() * _sprite.PixelSize * auraPadding;
        float auraWidth = _sprite.Texture.GetWidth() * _sprite.PixelSize * auraPadding;
        _fireBody!.MaterialOverride = aura ? _fireMantleMaterial : _fireBodyMaterial;
        _fireBody.Scale = aura ? new(auraWidth / 2.1f, auraHeight / 2, 1) : new(size, size * mantle, size);
        _fireBody.Position = aura ? _sprite.Position + new Vector3(0, 0, -0.04f) : new(0, size * 0.8f * mantle, 0);
        _fireRise!.Visible = aura;
        _fireRise.Scale = new(auraWidth / 2.1f, auraHeight / 2, 1);
        _fireRise.Position = _sprite.Position + new Vector3(0, 0, 0.04f);
        if (_fireMantlePortrait != _sprite.Texture)
        {
            _fireMantlePortrait = _sprite.Texture;
            _fireMantleMaterial!.SetShaderParameter("portrait", _fireMantlePortrait);
            _fireRiseMaterial!.SetShaderParameter("portrait", _fireMantlePortrait);
        }
        _fireMantleMaterial!.SetShaderParameter("flip", _sprite.FlipH);
        _fireRiseMaterial!.SetShaderParameter("flip", _sprite.FlipH);
        _fireMantleMaterial.SetShaderParameter("clock", _fireClock);
        _fireMantleMaterial.SetShaderParameter("tint", color);
        _fireMantleMaterial.SetShaderParameter("reach", _fireMantleReach);
        _fireRiseMaterial.SetShaderParameter("reach", _fireMantleReach);
        _fireMantleMaterial.SetShaderParameter("canvas_scale", auraPadding);
        _fireRiseMaterial.SetShaderParameter("canvas_scale", auraPadding);
        _fireMantleMaterial.SetShaderParameter("strength", 0.80f + level * 0.16f + _firePulse * 0.35f);
        _fireRiseMaterial!.SetShaderParameter("clock", _fireClock);
        _fireRiseMaterial.SetShaderParameter("tint", color);
        _fireRiseMaterial.SetShaderParameter("strength", 0.45f + level * 0.08f);
        _fireFloor!.Visible = !aura;
        _fireFloor!.Scale = Vector3.One * size;
        _fireBodyMaterial.SetShaderParameter("clock", _fireClock);
        _fireBodyMaterial.SetShaderParameter("tint", color);
        _fireBodyMaterial.SetShaderParameter("strength", (aura ? 0.68f + level * 0.16f : 0.55f + level * 0.13f) + _firePulse * 0.45f);
        _fireBodyMaterial.SetShaderParameter("invasive", FireInvasive);
        _fireFloorMaterial!.SetShaderParameter("tint", color);
        _fireFloorMaterial.SetShaderParameter("mode", 2);
        _fireFloorMaterial.SetShaderParameter("clock", _fireClock);
        _fireFloorMaterial.SetShaderParameter("strength", aura ? 0.42f : FireInvasive ? 0.10f : 0.20f);
        _fireMarkMaterial!.SetShaderParameter("clock", _fireClock);
        _fireMark!.Position = new(0, _fxHeight, 0.2f);
        _fireLight!.LightColor = color;
        _fireLight.Position = new(0, aura ? _fxHeight : 0.7f, 0);
        _fireLight.OmniRange = aura ? 1.5f : 2.8f;
        _fireLight.LightEnergy = 0.18f + level * 0.08f + _firePulse * 0.2f;
        _portraitMaterial.SetShaderParameter("fire_tint", color);
        _portraitMaterial.SetShaderParameter("fire_heat", _burning ? 0.25f + level * 0.12f + _firePulse * 0.35f : 0f);
        _portraitMaterial.SetShaderParameter("fire_damage", _burning && FireInvasive ? level / 4f : 0f);
    }

    internal void TintGurenFire(int level)
    {
        if (_gurenFlower?.MaterialOverride is ShaderMaterial material)
            material.SetShaderParameter("tint", new Color("ed294f").Lerp(FireFx.ColorOf(level), Math.Clamp(level / 6f, 0, 0.66f)));
    }
}
