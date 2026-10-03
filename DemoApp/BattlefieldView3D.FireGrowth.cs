using BattleCore;
using Godot;
using System;

public partial class BattlefieldView3D
{
    private double ShowFireGrowth(BattleEvent e, BattlePawn3D? actor, BattlePawn3D? target, double speed)
    {
        if (target is null) return 0;
        switch (e.Text)
        {
            case FireLevelLabels.KindleGuard:
                target.ShowMovementPortrait("borg_guard", 0.35);
                target.PulseFireGift();
                FireFx.Bloom(_fxRoot, target.FxPoint, FireFx.ColorOf(e.Amount), 2.3f, 0.28 / speed, 6);
                Float(target, "守りが火に", new("ffdda3"));
                return 0.06;
            case FireLevelLabels.Hoard:
                target.SetFireHoard(e.Slot);
                FireFlow(actor, target, new("b4eeff"), speed * 1.4);
                _attackAudio.PlayFireSound("grow");
                return 0.05;
            case FireLevelLabels.HoardRelease:
                target.SetFireHoard(0);
                Float(target, $"溜め火 {e.Amount} を放つ", new("c6f4ff"), true);
                return 0; // 次の爆炎の溜めで解放する。ここに別の待ちを入れない。
            case FireLevelLabels.GiftHoardAdd:
                target.SetFireGiftHoard(e.Slot);
                target.PulseFireGift();
                return 0.035;
            case FireLevelLabels.GiftOrder:
                FireFlow(actor, target, new("ffe4a2"), speed);
                Float(target, "先に渡す", new("ffe4a2"));
                return 0.08;
            default: return 0;
        }
    }
}
