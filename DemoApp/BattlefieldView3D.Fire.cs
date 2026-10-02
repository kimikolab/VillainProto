using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private int _fireGeneration;
    private ColorRect? _fireShade;
    private Tween? _fireShadeTween;
    private Godot.Environment? _fireEnvironment;
    internal int FireCuePlays, FireAttackPlays, FireRainPlays;
    internal readonly HashSet<string> FireLabelsShown = new();

    private void ResetFire()
    {
        _fireGeneration++;
        _attackAudio?.StopFireSounds();
        _fireShadeTween?.Kill();
        _fireShade?.Hide();
        if (_fireEnvironment is not null) _fireEnvironment.GlowEnabled = false;
        FireCuePlays = FireAttackPlays = FireRainPlays = 0;
        FireBlazePlays = FireEmbersHitPlays = 0;
        FireLabelsShown.Clear();
    }

    private void FireShade(BattlePawn3D pawn, double seconds)
    {
        if (_fireShade is null)
        {
            _fireShade = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Color = Colors.White,
                Material = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type canvas_item;
uniform vec2 focus=vec2(0.5);
uniform float amount=0.0;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
void fragment(){
    float d=length((UV-focus)*vec2(1.5,1.0));
    vec3 scene=texture(screen_texture,SCREEN_UV).rgb;
    float light=smoothstep(0.70,0.96,max(scene.r,max(scene.g,scene.b)));
    COLOR=vec4(0.012,0.018,0.038,smoothstep(0.05,0.65,d)*amount*0.68*(1.0-light));
}" } } };
            _fireShade.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_fireShade);
            MoveChild(_fireShade, 1); // 戦場だけに掛ける。HP以外の操作UI・字幕は暗くしない。
        }
        _fireShadeTween?.Kill();
        var material = (ShaderMaterial)_fireShade.Material;
        material.SetShaderParameter("focus", _camera.UnprojectPosition(pawn.FxPoint) / (Vector2)_viewport.Size);
        _fireShade.Show();
        _fireShadeTween = CreateTween();
        _fireShadeTween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("amount", p)), 0f, 1f, seconds * 0.20);
        _fireShadeTween.TweenInterval(seconds * 0.40);
        _fireShadeTween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("amount", p)), 1f, 0f, seconds * 0.40);
        _fireShadeTween.TweenCallback(Callable.From(_fireShade.Hide));
    }

    private void FireFlow(BattlePawn3D? from, BattlePawn3D? to, Color color, double speed, bool grand = false)
    {
        if (from is null || to is null) return;
        double seconds = (grand ? 0.52 : 0.32) / Math.Max(0.1, speed);
        int count = grand ? 3 : 1;
        for (int k = 0; k < count; k++)
            FireFx.Ribbon(_fxRoot, from.FxPoint + Vector3.Up * k * 0.12f, to.FxPoint,
                color, grand ? 0.15f : 0.085f, 0.55f + k * 0.28f, seconds, _camera.GlobalBasis.Z);
        FireFx.Bloom(_fxRoot, to.FxPoint, color, grand ? 2.4f : 1.3f, seconds, delay: (float)(seconds * 0.5));
    }

    internal double ShowFireCue(BattleEvent e, BattlePawn3D? actor, BattlePawn3D? target, double speed,
        FireAllyOutcome allyOutcome = FireAllyOutcome.Quiet)
    {
        speed = Math.Max(0.1, speed);
        FireCuePlays++;
        if (_fireEnvironment is not null) _fireEnvironment.GlowEnabled = true;
        FireLabelsShown.Add(e.Text ?? "");
        Color tint = FireFx.ColorOf(target?.FireLevel ?? 2);
        if (e.Kind == BattleEventKind.FireLevel)
        {
            if (FirePresentation.ChangesLevel(e.Text))
            {
                bool reachedFull = target is not null && target.FireLevel < 4 && e.Amount == 4;
                target?.SetFireLevel(e.Amount, e.Text == FireLevelLabels.Spent);
                if (target is null) return 0;
                tint = FireFx.ColorOf(e.Amount);
                if (e.Text == FireLevelLabels.Out)
                    FireFx.Bloom(_fxRoot, target.FxPoint, new Color("87858b"), 2, 0.65 / speed, 5);
                else if (e.Text == FireLevelLabels.Spent)
                    FireFx.Bloom(_fxRoot, target.GlobalPosition + Vector3.Up * 0.1f, new Color("ffad56"), 2, 0.28 / speed, 2, true);
                else if (e.Text != FireLevelLabels.Wilt)
                {
                    FireFx.Bloom(_fxRoot, target.FxPoint - Vector3.Up * 0.3f, tint, 1.5f, 0.3 / speed);
                    // 煽りなどの見出しに大きな流れがある。成長通知は細い糸だけ。
                    if (actor != target) FireFlow(actor, target, tint, speed * 1.4);
                    _attackAudio.PlayFireSound(reachedFull ? "aura" : "grow");
                }
                return e.Text == FireLevelLabels.Out ? 0.12 : 0.045;
            }
            switch (e.Text)
            {
                case FireLevelLabels.Overflow:
                case FireLevelLabels.Fed:
                    ShowFireFuel(actor, target, e, speed);
                    return 0.22;
                case FireLevelLabels.BlazeSolo:
                    Float(actor, "独りで解放", new("ffb773"), true);
                    actor?.PulseFireGift();
                    return 0.16;
                case FireLevelLabels.Stoke:
                    actor?.ShowMovementPortrait("hiyo_fan", 0.65);
                    FireFlow(actor, target, new("ffb44c"), speed);
                    _attackAudio.PlayFireSound("flow");
                    return 0.28;
                case FireLevelLabels.Gift:
                    actor?.ShowMovementPortrait("hiyo_gift", 0.9);
                    if (actor is not null && target is not null)
                    {
                        FireGiftFx.Transfer(_fxRoot, actor.FxPoint, target.FxPoint, _camera, 0.64 / speed);
                        FireGiftFx.Dial(_fxRoot, target, _camera, false, 0.48 / speed, 0.26 / speed);
                        FireFx.Light(_fxRoot, actor.FxPoint, new("ffd784"), 1.1f, 0.38 / speed);
                    }
                    _attackAudio.PlayFireSound("gift");
                    return 0.68;
                case FireLevelLabels.GiftTurn:
                    if (target is not null)
                    {
                        _attackAudio.PlayFireSound("gift_turn");
                        FireGiftFx.Dial(_fxRoot, target, _camera, true, 0.48 / speed);
                        target.PulseFireGift();
                        Float(target, "追加手番", new("fff0ac"), true, 3.05f, 1.15f);
                        FireFx.Light(_fxRoot, target.FxPoint, new("ffe3a0"), 1.8f, 0.35 / speed);
                        // 上半身からほどける火の筋を、続く溜め・攻撃へ繋ぐ。
                        for (int k = -1; k <= 1; k++)
                        {
                            Vector3 from = target.FxPoint + _camera.GlobalBasis.X * k * 0.35f;
                            FireFx.Ribbon(_fxRoot, from, from + Vector3.Up * 1.4f + _camera.GlobalBasis.X * k * 0.3f,
                                new("ffe19a"), 0.065f, 0.18f, 0.40 / speed, _camera.GlobalBasis.Z);
                        }
                    }
                    return 0.26;
                case FireLevelLabels.Spark:
                    target?.ShowMovementPortrait("hiyo_collect", 0.7);
                    FireFlow(actor, target, new("ffd485"), speed, true);
                    return 0.25;
                case FireLevelLabels.CallMark:
                case FireLevelLabels.Radiate:
                    FireFlow(actor, target, new("ffd485"), speed, true);
                    target?.SetFireMark(true);
                    _attackAudio.PlayFireSound("mark");
                    return 0.32;
                case FireLevelLabels.CallLost:
                    target?.SetFireMark(false);
                    return 0;
                case FireLevelLabels.Called:
                case FireLevelLabels.RadiateUse:
                    if (target is not null)
                        FireFx.Bloom(_fxRoot, target.FxPoint, new("fff4d1"), 2.3f, 0.32 / speed, 4);
                    target?.SetFireMark(false);
                    return 0.18;
                case FireLevelLabels.Spread:
                    FireFlow(target, actor, tint, speed); // 敵から吸い上げる向き。
                    return 0.10;
                case FireLevelLabels.CallFire:
                case FireLevelLabels.FoeSpread:
                    FireFlow(actor, target, tint, speed, e.Text == FireLevelLabels.FoeSpread);
                    return 0.18;
                default:
                    // 段・大技の見出しは次のAttackで溜めから着弾までを一度だけ再生する。
                    return 0;
            }
        }
        if (target is null) return 0;
        switch (e.Text)
        {
            case FireArmorLabels.BlazeAlly:
                ShowBlazeAlly(actor, target, allyOutcome, speed);
                return 0.10;
            case FireArmorLabels.Guard:
            case FireArmorLabels.Ward:
                actor?.ShowMovementPortrait("borg_guard", 0.45);
                if (actor != target) FireFlow(actor, target, tint, speed);
                FireFx.Bloom(_fxRoot, target.FxPoint, new("ffbd69"), 2.4f, 0.4 / speed, 6);
                FireFx.Light(_fxRoot, target.FxPoint, tint, 1.2f, 0.25 / speed);
                _attackAudio.PlayFireSound("guard");
                return 0.12;
            case FireArmorLabels.Convert:
            case FireArmorLabels.Mend:
            case FireArmorLabels.Feed:
                target.SetFireInvasive(false);
                ShowFireTick(target, true, false, 0.35 / speed);
                return 0.10;
            case FireArmorLabels.Foe:
            case FireArmorLabels.Splash:
                FireFlow(actor, target, new("ff8233"), speed);
                return 0.14;
            case FireArmorLabels.Smolder:
                FireFx.Bloom(_fxRoot, target.FxPoint, new("eab09a"), 2.5f, 0.55 / speed, 5);
                target.SetFireLevel(0);
                return 0.22;
            default:
                FireFx.Bloom(_fxRoot, target.FxPoint - Vector3.Up * 0.6f, new("bc4028"), 1.3f, 0.4 / speed);
                return 0.08;
        }
    }

    internal void ShowFireTick(BattlePawn3D? pawn, bool heal, bool damage, double seconds)
    {
        if (pawn is null) return;
        pawn.SetFireInvasive(damage);
        var color = damage ? FireFx.ColorOf(Math.Max(2, pawn.FireLevel)) : new Color("ffd998");
        FireFx.Bloom(_fxRoot, pawn.FxPoint - Vector3.Up * 0.25f, color, damage ? 1.7f : 2.1f,
            seconds, damage ? 1 : 6);
        if (heal)
            FireFx.Bloom(_fxRoot, pawn.GlobalPosition + Vector3.Up * 0.09f, new("d9ffe1"), 2.4f, seconds, 2, true);
    }

    internal async Task PlayFireAttack(BattlePawn3D? actor, BattlePawn3D? target,
        IReadOnlyList<BattlePawn3D> targets, FireAttackCue cue, double speed)
    {
        if (actor is null || target is null) return;
        speed = Math.Max(0.1, speed);
        int generation = _fireGeneration;
        int soundGeneration = _attackAudio.FireSoundGeneration;
        bool Alive() => generation == _fireGeneration && soundGeneration == _attackAudio.FireSoundGeneration
            && IsInstanceValid(actor) && actor.IsInsideTree() && actor.Hp > 0;
        async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(Math.Max(0.001, s / speed)), SceneTreeTimer.SignalName.Timeout);
        FireAttackPlays++;
        if (_fireEnvironment is not null) _fireEnvironment.GlowEnabled = true;
        bool burnout = cue.Kind == FireLevelLabels.Burnout;
        bool sweep = cue.Kind == FireLevelLabels.Unleash;
        bool rain = cue.Kind == FireLevelLabels.Rain;
        bool embers = cue.Kind == FireLevelLabels.Embers;
        int level = Math.Clamp(cue.Level, 1, 4);
        Color color = FireFx.ColorOf(rain ? 3 : level);
        var hits = targets.Count > 0 ? targets.Distinct().ToArray() : new[] { target };
        if (burnout) { await FireSwordSlam(actor, hits, speed); return; }
        if (cue.Kind == FireLevelLabels.Blaze) { await FireBlaze(actor, hits, speed); return; }
        if (cue.Kind == FireLevelLabels.EmbersHit) { await FireEmbersHit(actor, target, cue.Ordinal, speed); return; }
        if (sweep) { await FireGreatCleave(actor, hits, speed); return; }
        if (rain)
        {
            FireRainPlays++;
            // 台本上は火の雨。絵は叩きつけた剣から連続して噴く火柱として繋ぐ。
            actor.ShowMovementPortrait("hota_sword_slam", 0.55);
            FireUltimateFx.Pillar(_fxRoot, target.GlobalPosition + Vector3.Up * 0.05f,
                3.3f + cue.Ordinal % 3 * 0.35f, 1.65f, 0.34 / speed, _camera, false);
            FireFx.Bloom(_fxRoot, target.FxPoint, new("ffcd6b"), 2.4f, 0.24 / speed);
            FireFx.Bloom(_fxRoot, target.GlobalPosition + Vector3.Up * 0.08f, new("ffc35b"), 2.9f, 0.3 / speed, 2, true);
            FireFx.Light(_fxRoot, target.FxPoint, new("ffbb59"), 1.4f, 0.20 / speed);
            _attackAudio.PlayFireSound("rain");
            await Wait(0.065);
            return;
        }
        if (actor.UnitId == "hota" && !embers && level is 2 or 3)
        {
            await FirePierce(actor, hits, level, speed);
            return;
        }
        double charge = embers ? 0 : level >= 3 ? 0.38 : 0.10;
        if (charge > 0)
        {
            actor.ShowMovementPortrait(actor.UnitId == "borg" ? "borg_guard" : "hota_charge", charge + 0.1);
            FireFx.Bloom(_fxRoot, actor.FxPoint, color, 2.8f, charge / speed, 3);
            if (level >= 3)
            {
                _attackAudio.PlayFireSound("charge", speed, charge);
                for (int k = 0; k < 7; k++)
                {
                    float angle = k * Mathf.Tau / 7;
                    Vector3 source = actor.FxPoint + new Vector3(Mathf.Cos(angle) * 2.0f, 0.8f + Mathf.Sin(angle), Mathf.Sin(angle));
                    FireFx.Ribbon(_fxRoot, source, actor.FxPoint, color, 0.10f, 0.2f, charge / speed, _camera.GlobalBasis.Z);
                }
            }
            await Wait(charge);
            if (!Alive()) return;
        }
        actor.ShowMovementPortrait(actor.UnitId == "borg" ? "borg_release" : embers ? "hota_embers" : "hota_release", 0.7);
        if (actor.UnitId == "borg" && !embers)
        {
            FireUltimateFx.Crescent(_fxRoot, actor.FxPoint, target.FxPoint, _camera, 0.40 / speed, 1.6f + level * 0.22f);
            await Wait(0.15);
            if (!Alive()) return;
        }
        else if (!embers)
        {
            _attackAudio.PlayFireSound(level == 1 ? "projectile" : "jet", speed, level == 1 ? 0.30 : 0.50);
            var far = hits.OrderByDescending(p => p.FxPoint.DistanceSquaredTo(actor.FxPoint)).First();
            if (level == 1) FireFx.Fireball(_fxRoot, actor.FxPoint, far.FxPoint, color, 0.20 / speed);
            FireFx.Ribbon(_fxRoot, actor.FxPoint, far.FxPoint, color, level == 1 ? 0.27f : 0.16f + level * 0.09f,
                level == 1 ? 0.35f : 0, 0.28 / speed, _camera.GlobalBasis.Z);
            await Wait(0.20);
            if (!Alive()) return;
        }
        _attackAudio.PlayFireSound("impact");
        foreach (var hit in hits)
        {
            if (!IsInstanceValid(hit)) continue;
            FireFx.Bloom(_fxRoot, hit.FxPoint, color, embers ? 2 : 2.8f,
                0.40 / speed, embers ? 0 : 1);
            FireFx.Bloom(_fxRoot, hit.GlobalPosition + Vector3.Up * 0.10f, color, 2.6f,
                0.55 / speed, 2, true);
        }
        await Wait(0.08);
    }
}
