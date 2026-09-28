using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    internal int YomiIaiPlays { get; private set; }
    internal int YomiIaiExtraPlays { get; private set; }
    internal int YomiSweepPlays { get; private set; }

    // 台本の打点を開戦時の攻撃力と比べる表示量。特性の閾値やダメージ判定は持たない。
    // 4倍で見た目を上限にして、高火力でも盤面が光で埋まらないようにする。
    internal static float YomiIaiStrength(int power, int openingAttack)
        => Math.Clamp(MathF.Log2(Math.Max(1f, power / (float)Math.Max(1, openingAttack))) / 2f, 0, 1);

    private void ShowYomiIai(BattlePawn3D from, IReadOnlyList<BattlePawn3D> hits,
        AttackPattern pattern, bool reaction, int power)
    {
        YomiIaiPlays++;
        if (reaction) YomiIaiExtraPlays++;
        float strength = YomiIaiStrength(power, from.OpeningAttack);
        double speed = Math.Max(0.1, from.AnimationSpeed);
        double life = (0.32 + strength * 0.16) / speed;
        from.ShowMovementPortrait(reaction ? "yomi_iai_extra" : "yomi_iai", 0.46 + strength * 0.12,
            MovementCompletionSound(from, MovementSound.Sheathe));
        from.MovementPose(reaction ? 0.45f : 0.22f, 0.26f);
        if (reaction || strength >= 0.5f)
            from.BeginBonusAfterimage(MovementFx.Creak, 0.24 / speed, false, 0.38f);

        // 薙ぎは実際の対象を1本の刃でつなぐ。単体は標的一人の前で抜き切る。
        Vector3 right = _camera.GlobalBasis.X.Normalized() * (from.Team == 0 ? 1 : -1);
        Vector3 view = _camera.GlobalBasis.Z.Normalized();
        Vector3 facing = (hits[0].FxPoint - from.FxPoint).Normalized();
        float FacingAngle(BattlePawn3D p)
        {
            Vector3 d = p.FxPoint - from.FxPoint;
            return Mathf.Atan2(facing.X * d.Z - facing.Z * d.X, facing.X * d.X + facing.Z * d.Z);
        }
        var points = hits.Where(IsInstanceValid).OrderBy(FacingAngle)
            .Select(p => p.FxPoint + view * 0.12f + Vector3.Down * (reaction ? 0.18f : 0)).ToList();
        if (points.Count == 0) return;
        float reach = 0.65f + strength * 0.45f + (pattern == AttackPattern.Sweep ? 0.55f : 0);
        Vector3 swingOrigin = from.FxPoint;
        // 追加攻撃は低い前傾からの横抜き。差分の刀と剣閃の向きを揃える。
        Vector3 slope = right + Vector3.Up * (reaction ? 0.015f : 0.10f);
        Vector3 entry = points.Count == 1 ? slope : (points[1] - points[0]).Normalized();
        Vector3 exit = points.Count == 1 ? slope : (points[^1] - points[^2]).Normalized();
        points.Insert(0, points[0] - entry * reach);
        points.Add(points[^1] + exit * reach);
        Vector3 Blade(float t)
        {
            float f = t * (points.Count - 1);
            int i = Math.Min((int)f, points.Count - 2);
            // 対象の角度順に滑らかに通す。折れ線にすると雷のように見えてしまう。
            Vector3 blade = points[i].CubicInterpolate(points[i + 1],
                i > 0 ? points[i - 1] : points[0] - entry * reach,
                i + 2 < points.Count ? points[i + 2] : points[^1] + exit * reach, f - i)
                + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * (reaction ? 0.04f : 0.12f + strength * 0.12f));
            if (pattern == AttackPattern.Sweep)
                blade += (blade - swingOrigin).Normalized() * Mathf.Sin(t * Mathf.Pi) * (0.55f + strength * 0.35f);
            return blade;
        }
        if (pattern == AttackPattern.Sweep)
        {
            YomiSweepPlays++;
            MovementFx.SweepSheet(_fxRoot, _camera, from.FxPoint, Blade, MovementFx.Creak,
                0.85f + strength * 0.85f, (0.44 + strength * 0.14) / speed);
        }
        // 太い金の縁と白い芯。残光も同じ一振りの軌道なので連撃数を誤認させない。
        MovementFx.Ribbon(_fxRoot, _camera, Blade, MovementFx.Creak,
            0.040f + strength * 0.065f, life, revealFraction: 0.09f);
        MovementFx.Ribbon(_fxRoot, _camera, t => Blade(t) + view * 0.015f, new Color("fff9df"),
            0.014f + strength * 0.027f, life * 0.68, revealFraction: 0.09f);
        if (strength > 0.15f)
            MovementFx.Ribbon(_fxRoot, _camera, t => Blade(t) - Vector3.Up * (0.10f + strength * 0.09f),
                MovementFx.Creak.Darkened(0.22f), 0.014f + strength * 0.021f, life, revealFraction: 0.09f);
        foreach (var hit in hits.Where(IsInstanceValid))
        {
            Vector3 center = hit.FxPoint + view * 0.15f;
            for (int i = 0; i < 2 + (int)(strength * 4); i++)
            {
                float angle = i * 2.4f;
                Vector3 ray = (right * Mathf.Cos(angle) + Vector3.Up * Mathf.Sin(angle)) * (0.23f + strength * 0.42f);
                MovementFx.Ribbon(_fxRoot, _camera, t => center + ray * (0.18f + t),
                    MovementFx.Creak, 0.014f, 0.20 / speed, true, 0.12f);
            }
        }
    }
}
