using BattleCore;
using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int FireBlazePlays, FireEmbersHitPlays;

    private async Task FireBlaze(BattlePawn3D actor, BattlePawn3D[] hits, double speed, int stored = 0)
    {
        int generation = _fireGeneration;
        int soundGeneration = _attackAudio.FireSoundGeneration;
        bool Live() => generation == _fireGeneration && soundGeneration == _attackAudio.FireSoundGeneration
            && IsInstanceValid(actor) && actor.IsInsideTree() && actor.Hp > 0;
        async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(Math.Max(0.001, seconds / speed)), SceneTreeTimer.SignalName.Timeout);
        FireBlazePlays++;
        // 台本の解放量だけで炎の量を変える。威力はAttack.Amountの表示に任せる。
        float abundance = 1 + Math.Min(Math.Max(0, stored), 12) * 0.10f;
        actor.ShowMovementPortrait("borg_guard", 0.7);
        FireShade(actor, 1.3 / speed);
        _attackAudio.PlayFireSound("charge", speed, 0.44);
        // 鎧へ圧縮してから、ボルグを中心に全方向へ爆ぜる。
        int streams = 8 + Math.Min(Math.Max(0, stored), 12);
        for (int i = 0; i < streams; i++)
        {
            float a = i * Mathf.Tau / streams;
            Vector3 edge = actor.FxPoint + _camera.GlobalBasis.X * Mathf.Cos(a) * 2.0f
                + _camera.GlobalBasis.Y * Mathf.Sin(a) * 1.6f;
            FireFx.Ribbon(_fxRoot, edge, actor.FxPoint, new("ff8a37"), 0.11f, 0.15f, 0.42 / speed, _camera.GlobalBasis.Z);
        }
        FireFx.Bloom(_fxRoot, actor.FxPoint, new("ff8030"), 3.6f, 0.5 / speed, 3);
        await Wait(0.48);
        if (!Live()) return;
        actor.ShowMovementPortrait("borg_release", 0.85);
        actor.PulseFireGift();
        FireImpactCamera(actor.FxPoint, speed, 0.85f);
        _attackAudio.PlayFireSound("blaze", speed);
        FireFx.Bloom(_fxRoot, actor.FxPoint, new("ff8b2c"), 10.5f * abundance, 0.64 / speed);
        FireFx.Bloom(_fxRoot, actor.FxPoint, new("fff0bb"), 5.5f, 0.18 / speed, 7);
        FireUltimateFx.Sparks(_fxRoot, actor.FxPoint, _camera, 0.70 / speed);
        for (int i = 0; i < 3; i++)
            FireFx.Bloom(_fxRoot, actor.GlobalPosition + Vector3.Up * (0.08f + i * 0.02f),
                new("ffb447"), 12 + i * 3, (0.50 + i * 0.12) / speed, 2, true);
        // 敵味方へ同時に広がる熱。味方の結果は各BlazeAllyの台本で別に描く。
        foreach (var pawn in _pawns.Values)
            if (pawn != actor && pawn.Hp > 0)
                FireFx.Ribbon(_fxRoot, actor.FxPoint, pawn.FxPoint, new("ffbe60"),
                    0.25f, 0.30f, 0.35 / speed, _camera.GlobalBasis.Z);
        await Wait(0.10);
        if (!Live()) return;
        foreach (var hit in hits)
        {
            if (!IsInstanceValid(hit)) continue;
            FireUltimateFx.Pillar(_fxRoot, hit.GlobalPosition + Vector3.Up * 0.05f,
                3.8f * abundance, 2.8f * abundance, 0.6 / speed, _camera, false);
            FireFx.Bloom(_fxRoot, hit.FxPoint, new("ffad4a"), 3.5f, 0.42 / speed);
            FireFx.Light(_fxRoot, hit.FxPoint, new("ffad4a"), 1.8f, 0.35 / speed);
            NotifyAttackContact(hit);
        }
    }

    private void ShowBlazeAlly(BattlePawn3D? actor, BattlePawn3D target, FireAllyOutcome outcome, double speed)
    {
        bool damage = outcome == FireAllyOutcome.Damage;
        target.SetFireInvasive(damage);
        Color tint = new(damage ? "ff6d32" : "ffe9b0");
        if (actor is not null)
            FireFx.Ribbon(_fxRoot, actor.FxPoint, target.FxPoint, tint, 0.13f, 0.3f, 0.22 / speed, _camera.GlobalBasis.Z);
        if (damage)
        {
            ShowFireTick(target, false, true, 0.44 / speed);
            FireFx.Bloom(_fxRoot, target.FxPoint - Vector3.Up * 0.35f, tint, 2.4f, 0.45 / speed, 0);
            Float(target, "爆炎に巻き込まれる", tint);
        }
        else
        {
            target.PulseFireGift();
            for (int k = -1; k <= 1; k++)
            {
                Vector3 from = target.FxPoint + _camera.GlobalBasis.X * k * 0.35f;
                FireFx.Ribbon(_fxRoot, from, from + Vector3.Up * 1.15f,
                    tint, 0.10f, 0.12f, 0.40 / speed, _camera.GlobalBasis.Z);
            }
            if (outcome == FireAllyOutcome.Heal)
            {
                Float(target, "爆炎が癒しに", new("cffff0"));
                _attackAudio.PlayFireSound("heal");
            }
        }
        FireFx.Light(_fxRoot, target.FxPoint, tint, 0.8f, 0.3 / speed);
    }

    private async Task FireEmbersHit(BattlePawn3D actor, BattlePawn3D target, int ordinal, double speed)
    {
        int generation = _fireGeneration;
        int soundGeneration = _attackAudio.FireSoundGeneration;
        FireEmbersHitPlays++;
        actor.ShowMovementPortrait("hota_embers", 0.65);
        // 台本1件につき1発だけ。対象も台本に従い、1体への5発も同じ入口を通る。
        Vector3 from = actor.FxPoint + Vector3.Up * (1.0f + ordinal % 2 * 0.45f)
            + _camera.GlobalBasis.X * ((ordinal % 3 - 1) * 0.45f);
        bool previewFireball = PurchasedAssets.EffectsAvailable && (AssetPreviewFireballs ?? true);
        Vector3 impactPoint = target.FxPoint + _camera.GlobalBasis.Z * 0.4f;
        if (previewFireball)
            AssetFireballFx.Launch(_fxRoot, from, impactPoint, 0.11 / speed, ordinal);
        else
        {
            FireFx.Fireball(_fxRoot, from, target.FxPoint, new("ff9141"), 0.11 / speed);
            FireFx.Ribbon(_fxRoot, from, target.FxPoint, new("ffd189"), 0.11f, 0.15f, 0.16 / speed, _camera.GlobalBasis.Z);
        }
        await ToSignal(GetTree().CreateTimer(Math.Max(0.001, 0.11 / speed), processAlways: false), SceneTreeTimer.SignalName.Timeout);
        if (generation != _fireGeneration || soundGeneration != _attackAudio.FireSoundGeneration
            || !IsInstanceValid(actor) || actor.Hp <= 0 || !IsInstanceValid(target)) return;
        if (previewFireball)
        {
            AssetFireballFx.Impact(_fxRoot, impactPoint, speed, ordinal);
            AssetPreviewFireballImpacts++;
        }
        else
        {
            FireFx.Bloom(_fxRoot, target.FxPoint, new("ffae59"), 2.6f, 0.24 / speed);
            FireFx.Bloom(_fxRoot, target.FxPoint, new("fff0bc"), 2.2f, 0.13 / speed, 7);
        }
        _attackAudio.PlayFireSound("embers");
        NotifyAttackContact(target);
    }

    private void ShowFireFuel(BattlePawn3D? actor, BattlePawn3D? target, BattleEvent e, double speed)
    {
        if (target is null) return;
        bool overflow = e.Text == FireLevelLabels.Overflow;
        Color tint = new(overflow ? "92e5ff" : "ff672c");
        Vector3 ember = target.FxPoint - Vector3.Up * 0.25f + _camera.GlobalBasis.X * (target.Team == 0 ? 0.35f : -0.35f);
        if (actor is not null && actor != target)
            FireFx.Ribbon(_fxRoot, actor.FxPoint, ember, tint, 0.10f, 0.45f, 0.30 / speed, _camera.GlobalBasis.Z);
        for (int i = 0; i < (overflow ? 6 : 3); i++)
        {
            float a = i * Mathf.Tau / (overflow ? 6 : 3);
            Vector3 from = ember + _camera.GlobalBasis.X * Mathf.Cos(a) * 0.85f + Vector3.Up * Mathf.Sin(a) * 0.8f;
            FireFx.Ribbon(_fxRoot, from, ember, tint, 0.055f, 0.15f, 0.32 / speed, _camera.GlobalBasis.Z);
        }
        FireFx.Bloom(_fxRoot, ember, tint, overflow ? 1.6f : 1.2f, 0.50 / speed, 3);
        FireFx.Bloom(_fxRoot, ember, tint, 1.5f, 0.28 / speed, 7, delay: (float)(0.18 / speed));
        FireFx.Light(_fxRoot, ember, tint, 0.8f, 0.4 / speed);
        Float(target, $"{(overflow ? "火を凝縮" : "熾にくべる")}  攻+{e.Amount}", tint);
        _attackAudio.PlayFireSound(overflow ? "overflow" : "fed");
    }
}
