using BattleCore;
using Godot;
using System;

public partial class BattlefieldView3D
{
    internal int ShioHealPlays { get; private set; }
    internal int ShioStrengthPlays { get; private set; }

    private Vector3 ShioSupportHand(BattlePawn3D source, BattlePawn3D target, double speed)
    {
        source.AnimationSpeed = Math.Max(0.1, speed);
        float dx = (target.FxPoint - source.FxPoint).Dot(_camera.GlobalBasis.X);
        bool flip = Math.Abs(dx) < 0.01f ? source.Team != 0 : dx < 0;
        source.ShowMovementPortrait("shio_support", 0.52, flip: flip);
        // 1024×1536の支援差分。差し出した掌から、台本が指定した受け手へ送る。
        return source.MovementPortraitPoint(new Vector2(945, 445), _camera);
    }

    internal void ShowShioHealing(BattlePawn3D source, BattlePawn3D target, int amount, double speed)
    {
        if (source.UnitId != "shio" || !source.CanReceiveMovementImpact || !target.CanReceiveMovementImpact || amount <= 0) return;
        double s = Math.Max(0.1, speed);
        Vector3 hand = ShioSupportHand(source, target, s);
        SupportLight(hand, target.FxPoint, new Color("a8e69b"), 0.24 / s);
        MovementFx.Flow(_fxRoot, _camera, hand, target.FxPoint, new Color("a8e69b"), 0.24 / s, 0.14f, 3);
        MovementLeaves(target);
        target.ShowHealingDrops(amount);
        _attackAudio.PlayHeal(source.UnitId, source == target);
        ShioHealPlays++;
    }

    internal bool ShowShioStrength(BattlePawn3D source, BattlePawn3D target, BattleEvent e, double speed)
    {
        if (source.UnitId != "shio" || e.Kind != BattleEventKind.Whet || e.WhetRoute != WhetRoute.Drifter
            || e.AttackAfter is not int attack || e.Amount <= 0
            || !source.CanReceiveMovementImpact || !target.CanReceiveMovementImpact) return false;
        double s = Math.Max(0.1, speed);
        Vector3 hand = ShioSupportHand(source, target, s);
        // 計算し直さず、実際の受け手と強化後の攻撃値をその場で反映する。
        // 次ターンの写しが同値なら差分0になり、音・矢印を二重に出さない。
        int change = attack - target.AttackValue;
        target.SetAttack(attack, animate: false);
        Color tint = new(change < 0 ? "8fcaff" : "ffdc83");
        SupportLight(hand, target.FxPoint, tint, 0.24 / s);
        MovementFx.Flow(_fxRoot, _camera, hand, target.FxPoint, tint, 0.24 / s, 0.06f, 3);
        MovementFx.Coil(_fxRoot, _camera, target.FxPoint, tint, 0.38f, 0.32 / s, 1);
        // 逆しま・倍率・横流しでも「名目の+量＝攻撃の増分」と誤表示しない。
        Float(target, $"攻撃 {attack}", tint, true, 3.50f);
        _attackAudio.PlayAttackChange(change);
        ShioStrengthPlays++;
        return true;
    }
}
