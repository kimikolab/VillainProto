using BattleCore;
using Godot;
using System;

public partial class BattlePawn3D
{
    private bool _ashReleasing;
    private float _portraitOffsetX;
    private bool _bowDrawn;
    private Tween? _bowPortraitTween;
    public bool IsAshReleasing => _ashReleasing;
    public bool IsBinding { get; private set; }
    public bool IsFrightened { get; private set; }
    public bool DirgeSinging { get; private set; }

    public void SetDirgeSinging(bool active)
    {
        bool next = active && _unitId == "ere" && _alive && !_victory;
        if (DirgeSinging == next) return;
        DirgeSinging = next;
        RefreshBattlePortrait();
    }

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
        if (ApplyRicaApproachPortrait()) return;
        string key = _unitId;
        float height = _portraitHeight;
        float padding = UiKit.BattlePortraitBottomPaddingRatio(key);
        _portraitOffsetX = 0;
        if (key == "vio" && _alive && VioGrowthStage > 0)
        {
            key = VioGrowthStage == 2 ? "vio_gorged" : "vio_fed";
            padding = UiKit.BattlePortraitBottomPaddingRatio(key);
        }
        if (key == "sero" && _bowDrawn && _alive)
        {
            key = "sero_attack";
            padding = UiKit.BattlePortraitBottomPaddingRatio(key);
        }
        if (key == "gald" && SwordDrawn)
        {
            key = QuietLastStand ? "gald_kneel" : "gald_sword";
            padding = QuietLastStand ? 0.086f : 0.066f;
            if (QuietLastStand) height *= 0.76f;
        }
        if (key == "beni" && GurenReleasing) { key = "beni_release"; padding = 0.024f; }
        if (key == "ere" && DirgeSinging && _alive)
        {
            key = "ere_singing";
            padding = UiKit.BattlePortraitBottomPaddingRatio(key);
        }
        if (key == "sekki" && SekkiWrapping && _alive) key = "sekki_wrap";
        if (key == "susu" && _alive)
        {
            if (_ashReleasing)
            {
                key = "susu_release";
                // アカは通常・溜め・放出に同じ立ち絵を使用するため、体格と足元を固定する。
            }
            else if (IsCharging)
            {
                key = "susu_charging";
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
        if (_movementPortrait is { } movement && _alive)
        {
            key = movement;
            padding = UiKit.BattlePortraitBottomPaddingRatio(key);
            // ヒヨの旧動作絵と新待機絵の本体比率を揃える。基準身長の変更時も同率で拡大する。
            if (key.StartsWith("hiyo_")) height *= 2.10f / 1.89f;
            // 横に踏み込む追加居合は1254角。1536高の待機絵と画素あたりの体格を揃える。
            if (key == "yomi_iai_extra") height *= 1254f / 1536f;
            if (key == "basa_flap") height *= 0.90f;
            // 掲剣は剣先までを含む。頭から足の身長を待機絵と揃える。
            if (key == "hota_sword_raise") height *= 1.42f;
            if (key is "hane_dropkick" or "hane_flying_kick") height *= 1024f / 1536f;
            // 低い踏み込みは1024高。待機絵と画素あたりの体格を揃える。
            if (key is "hane_palm" or "hane_spring_guard") height *= 1024f / 1536f;
            if (key is "hane_rescue" or "hane_bump_rebound") height *= 1024f / 1536f;
            // 右向き待機絵は頭〜足が1488px、攻撃絵は1371px。同じ身長へ揃える。
            if (key == "tome_attack") height *= 1488f / 1371f;
            if (key == "kata_thunder") height *= 1.05f; // 掲げた手より頭身で合わせる。
            // 被弾・受け流しの差分は本体が少し小さいため、待機絵と体格を合わせる。
            if (key is "gald_hit" or "gald_parry") height *= 1.05f;
            if (key == "gare_load") height *= 1435f / 1501f;
            if (key == "gare_fire") height *= 1435f / 1330f;
            if (key == "gare_empty") height *= 1435f / 1497f;
            if (key == "susu_read") height *= 1480f / 1493f;
            // 前傾突進の差分は1374px高。しゃがんだ胴を拡大せず、待機絵と画素スケールを揃える。
            if (key == "hagi_step") height *= 1374f / 1536f;
        }
        if (_golmRocketPose && _unitId == "golm")
        {
            key = "golm_rocket";
            padding = UiKit.BattlePortraitBottomPaddingRatio(key);
            // 右向き差分の頭〜足1179pxを待機絵の1232pxと揃える。
            height *= 1.045f;
        }
        if (HushShattered && _unitId == "husher")
        {
            key = _alive && _movementPortrait == "husher_shatter_burst"
                ? "husher_shatter_burst" : "husher_shattered";
            // 破砕後も立ち続ける差分。通常時の身長を保ち、足元だけ合わせる。
            padding = key == "husher_shatter_burst" ? .0195f : .0234f;
        }
        ApplyRicaIdlePortrait(ref key, ref height, ref padding);
        Texture2D portrait = UiKit.BattlePortrait(_atlas, key, _burning);
        _portraitGroundDistance = height * (0.5f - padding);
        _portraitBaseY = PortraitGroundY + _portraitGroundDistance;
        _sprite.Texture = portrait;
        _sprite.FlipH = _golmRocketPose ? Team == BattleContext.EnemyTeam
            : _movementFlip ?? (Team == BattleContext.EnemyTeam);
        AlignMisaPortrait(key);
        _sprite.PixelSize = height / Math.Max(1, portrait.GetHeight());
        _sprite.Position = new Vector3(_portraitOffsetX, _portraitBaseY, 0);
        _portraitMaterial.SetShaderParameter("portrait_texture", portrait);
        UpdateIdleLoop(key, height);
    }

    public void BeginAshRelease()
    {
        if (_unitId != "susu" || !_alive || _victory) return;
        _ashReleasing = true;
        RefreshBattlePortrait();
    }

    // 射撃の拍だけ弓を構える。連射では前の復帰予約を取り消して表示を延長する。
    public void AnimateBowAttack()
    {
        if (_unitId != "sero" || !_alive || _victory) return;
        ClearMovementPortrait();
        _bowPortraitTween?.Kill();
        _bowDrawn = true;
        RefreshBattlePortrait();
        _bowPortraitTween = CreateTween();
        _bowPortraitTween.TweenInterval(0.40 / AnimationSpeed);
        _bowPortraitTween.TweenCallback(Callable.From(ResetBowPortrait));
    }

    private void ResetBowPortrait()
    {
        _bowPortraitTween?.Kill();
        _bowPortraitTween = null;
        if (!_bowDrawn) return;
        _bowDrawn = false;
        RefreshBattlePortrait();
    }

    public void EndAshRelease()
    {
        if (!_ashReleasing) return;
        _ashReleasing = false;
        RefreshBattlePortrait();
    }
}
