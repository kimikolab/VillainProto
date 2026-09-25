using BattleCore;
using Godot;
using System;

public partial class BattlePawn3D
{
    private bool _ashReleasing;
    private float _portraitOffsetX;
    public bool IsAshReleasing => _ashReleasing;
    public bool IsBinding { get; private set; }
    public bool IsFrightened { get; private set; }

    public void SetFrightened(bool active)
    {
        bool next = active && _unitId == "shiga" && _alive && !_victory;
        if (IsFrightened == next) return;
        IsFrightened = next;
        RefreshBattlePortrait();
    }

    public void SetBinding(bool active)
    {
        IsBinding = active && _alive && !_victory;
        RefreshBattlePortrait();
    }

    // 灰の描画まで含むキャンバスでも、本体の大きさと足元を揃える。
    private void RefreshBattlePortrait()
    {
        if (_sprite is null || _victory) return;
        string key = _unitId;
        float height = _portraitHeight;
        float padding = UiKit.BattlePortraitBottomPaddingRatio(key);
        _portraitOffsetX = 0;
        if (key == "gald" && SwordDrawn)
        {
            key = QuietLastStand ? "gald_kneel" : "gald_sword";
            padding = QuietLastStand ? 0.086f : 0.066f;
            if (QuietLastStand) height *= 0.76f;
        }
        if (key == "beni" && GurenReleasing) { key = "beni_release"; padding = 0.024f; }
        if (key == "susu" && _alive)
        {
            if (_ashReleasing)
            {
                key = "susu_release";
                height *= 1.50f;
                padding = 0.0196f;
                _portraitOffsetX = (Team == BattleContext.PlayerTeam ? 1 : -1) * 0.25f;
            }
            else if (IsCharging)
            {
                key = "susu_charging";
                padding = 0.0286f;
            }
        }
        if (key == "kugu" && IsBinding && _alive)
        {
            key = "kugu_binding";
            padding = 0.04232f;
        }
        if (key == "shiga" && IsFrightened && _alive)
        {
            key = "shiga_frightened";
            padding = 0.02083f;
        }
        Texture2D portrait = UiKit.BattlePortrait(_atlas, key, _burning);
        _portraitGroundDistance = height * (0.5f - padding);
        _portraitBaseY = PortraitGroundY + _portraitGroundDistance;
        _sprite.Texture = portrait;
        _sprite.PixelSize = height / Math.Max(1, portrait.GetHeight());
        _sprite.Position = new Vector3(_portraitOffsetX, _portraitBaseY, 0);
        _portraitMaterial.SetShaderParameter("portrait_texture", portrait);
    }

    public void BeginAshRelease()
    {
        if (_unitId != "susu" || !_alive || _victory) return;
        _ashReleasing = true;
        RefreshBattlePortrait();
    }

    public void EndAshRelease()
    {
        if (!_ashReleasing) return;
        _ashReleasing = false;
        RefreshBattlePortrait();
    }
}
