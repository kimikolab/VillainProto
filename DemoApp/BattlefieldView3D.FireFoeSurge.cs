using BattleCore;
using Godot;
using System;
using System.Collections.Generic;

public partial class BattlefieldView3D
{
    internal int FireFoeSurgePlays, FireFoeSurgeTargets;

    internal void ShowFireFoeSurge(IReadOnlyList<BattleEvent> events, double speed)
    {
        speed = Math.Max(0.1, speed);
        FireFoeSurgePlays++;
        // 連続する通知の対象だけを、同じフレームで跳ね上げる。死亡・他の出来事は跨がない。
        foreach (var e in events)
        {
            var actor = FindPawn(e.ActorId);
            var target = FindPawn(e.TargetId);
            if (actor is null || target is null) { GD.PushWarning("爆炎・敵上げ: 台本の駒が見つからない"); continue; }
            if (target.Team == actor.Team) { GD.PushWarning("爆炎・敵上げ: 味方を指す通知は表示できない"); continue; }
            if (target.Hp <= 0) continue;
            target.SetFireLevel(e.Amount);
            target.SetFireInvasive(true);
            target.PulseFireGift();
            FireFoeSurgeTargets++;
            Color tint = FireFx.ColorOf(e.Amount);
            int rise = Math.Max(0, e.Amount - e.Slot); // 表示の跳ね幅だけ。火勢自体はAmountを写す。
            float height = 2.3f + rise * 0.85f;
            FireUltimateFx.Pillar(_fxRoot, target.GlobalPosition + Vector3.Up * 0.04f,
                height, 1.3f + rise * 0.30f, 0.62 / speed, _camera, false, tint);
            FireFx.Bloom(_fxRoot, target.FxPoint, tint, 2.2f + rise * 0.4f, 0.26 / speed, 7);
            FireFx.Light(_fxRoot, target.FxPoint, tint, 1.4f + rise * 0.35f, 0.44 / speed);
            // 胴のひびから柱へ走る光。駒の既存の侵食シェーダーも新しい火勢へ更新される。
            for (int k = -1; k <= 1; k++)
            {
                Vector3 foot = target.FxPoint - Vector3.Up * 0.6f + _camera.GlobalBasis.X * k * 0.20f;
                Vector3 bend = foot + Vector3.Up * 0.45f + _camera.GlobalBasis.X * 0.16f;
                FireFx.Ribbon(_fxRoot, foot, bend, tint, 0.035f, 0, 0.25 / speed, _camera.GlobalBasis.Z);
                FireFx.Ribbon(_fxRoot, bend, bend + Vector3.Up * 0.55f - _camera.GlobalBasis.X * 0.24f,
                    tint, 0.025f, 0, 0.32 / speed, _camera.GlobalBasis.Z);
            }
        }
        _attackAudio.PlayFireSound("aura", speed);
    }
}
