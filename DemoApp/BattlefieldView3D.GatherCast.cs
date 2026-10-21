using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    private readonly List<GatherCast3D> _gatherEffects = new();
    private readonly Dictionary<int, (Sprite3D Bag, Sprite3D Light, int Amount)> _pebbleBags = new();
    private bool _gatherPaused;
    private double _gatherSpeed = 1;
    internal int PebblePickPlays, PebbleVolleyPlays, PebbleEmptyPlays, BloodReadPlays, BloodFalloutPlays, BloodDryPlays;
    internal int LastBloodMotes, LastGatherTargets;
    internal float LastGatherWidth;
    internal IEnumerable<GatherCast3D> GatherEffects => _gatherEffects.Where(f => IsInstanceValid(f) && !f.IsQueuedForDeletion());
    internal int PebblesShown(int id) => _pebbleBags.TryGetValue(id, out var b) ? b.Amount : 0;
    internal static int WoundMotes(BattlePawn3D pawn)
        => pawn.PresentationAlive ? Math.Clamp((int)Math.Ceiling((pawn.MaxHp - pawn.Hp) / 6.0), 0, 18) : 0;

    private GatherCast3D GatherEffect(float duration, float contact, BattlePawn3D? pose = null)
    {
        var fx = new GatherCast3D(); _fxRoot.AddChild(fx);
        fx.Configure(duration, contact, pose is null ? null : () => { if (IsInstanceValid(pose)) pose.EndGatherPose(); });
        fx.SetClock(_gatherPaused, _gatherSpeed); _gatherEffects.RemoveAll(f => !IsInstanceValid(f) || f.IsQueuedForDeletion());
        _gatherEffects.Add(fx); return fx;
    }
    internal void SetGatherClock(bool paused, double speed)
    {
        _gatherPaused = paused; _gatherSpeed = Math.Max(.1, speed);
        foreach (var fx in GatherEffects) fx.SetClock(paused, speed);
    }
    internal void EndGatherPresentation()
    {
        foreach (var fx in GatherEffects.ToArray()) fx.Cancel();
        _gatherEffects.Clear();
        foreach (var bag in _pebbleBags.Values)
        { if (IsInstanceValid(bag.Bag)) bag.Bag.QueueFree(); if (IsInstanceValid(bag.Light)) bag.Light.QueueFree(); }
        _pebbleBags.Clear();
        foreach (var pawn in _pawns.Values) pawn.EndGatherPose();
        _gatherPaused = false;
    }
    private void ResetGatherPresentation()
    {
        EndGatherPresentation();
        PebblePickPlays = PebbleVolleyPlays = PebbleEmptyPlays = BloodReadPlays = BloodFalloutPlays = BloodDryPlays = 0;
        BloodGatherSources = BloodReleaseTargets = LastBloodMotes = LastGatherTargets = 0;
    }
    private Vector3 BagPoint(BattlePawn3D gare)
        => gare.GatherPortraitPoint(gare.MovementPortrait == "gare_load" ? new Vector2(455, 640) : new Vector2(410, 660), _camera);
    private Vector3 BookPoint(BattlePawn3D aka)
        => aka.GatherPortraitPoint(aka.MovementPortrait == "susu_read" ? new Vector2(835, 335) : new Vector2(590, 450), _camera);

    private void SetPebbleBag(BattlePawn3D gare, int amount)
    {
        if (!_pebbleBags.TryGetValue(gare.InstanceId, out var bag))
        {
            bag = (AkaMagic.Sprite(_fxRoot, BagPoint(gare), GatherArt.Pouch, .28f),
                AkaMagic.Sprite(_fxRoot, BagPoint(gare) + Vector3.Up * .1f, GatherArt.Light, .75f), 0);
        }
        bag.Amount = amount;
        bag.Bag.GlobalPosition = BagPoint(gare); bag.Light.GlobalPosition = BagPoint(gare) + Vector3.Up * .12f;
        bag.Bag.Visible = bag.Light.Visible = amount > 0 && gare.PresentationAlive;
        float fill = Math.Clamp(amount / 50f, 0, 1);
        bag.Bag.Scale = Vector3.One * (1 + fill * .38f);
        bag.Light.Scale = Vector3.One * (.45f + fill * .65f);
        bag.Light.Modulate = new Color(1, 1, 1, .35f + fill * .4f);
        _pebbleBags[gare.InstanceId] = bag;
    }
    internal GatherCast3D PickPebbles(BattlePawn3D gare, BattlePawn3D source, int amount, int remaining)
    {
        PebblePickPlays++;
        var fx = GatherEffect(.52f, .40f);
        fx.Flow(source.FxPoint, BagPoint(gare), GatherArt.Crystal, Math.Clamp(amount / 2 + 2, 3, 16), 0, .32f, .15f);
        fx.Flash(BagPoint(gare), GatherArt.Light, .7f, .30f, .22f);
        fx.At(.40f, () => { if (gare.PresentationAlive) SetPebbleBag(gare, remaining); });
        // 表示在庫は通知で更新する。頭なしの高速再生で演出を省いても一致させる。
        SetPebbleBag(gare, remaining);
        return fx;
    }
    internal GatherCast3D EmptyPebbles(BattlePawn3D gare)
    {
        PebbleEmptyPlays++; SetPebbleBag(gare, 0); gare.BeginGatherPose("gare_empty");
        var fx = GatherEffect(.64f, .50f, gare);
        Float(gare, "礫がない", UiKit.Muted);
        return fx;
    }
    internal GatherCast3D ReadWounds(BattlePawn3D aka)
    {
        BloodReadPlays++; aka.BeginGatherPose("susu_read");
        var fx = GatherEffect(.64f, .62f, aka);
        fx.Sound("aka_circle", -10);
        LastBloodMotes = 0;
        foreach (var source in _pawns.Values.Where(p => p.Team == aka.Team && p.PresentationAlive))
        {
            int count = WoundMotes(source);
            if (count == 0) continue;
            BloodGatherSources++; LastBloodMotes += count;
            fx.Flow(source.FxPoint, BookPoint(aka), AkaMagic.Glow, count, 0, .30f, .13f + count * .003f);
        }
        fx.Flash(BookPoint(aka), AkaMagic.Glow, .85f, .30f, .34f);
        return fx;
    }

    // 対象数は台本の直撃先だけ。敵が減るほど各束の幅が増え、1体では一本になる。
    internal GatherCast3D GatherVolley(BattlePawn3D actor, IReadOnlyList<BattlePawn3D> targets, bool pebble, int stock = 0)
    {
        if (pebble) return GareVolley(actor, targets, stock);
        actor.BeginGatherPose("susu_cast");
        BloodReleaseTargets += targets.Count;
        var fx = GatherEffect(1.02f, .70f, actor);
        fx.Sound("aka_ruby_burst", -10, .32f);
        LastGatherTargets = targets.Count;
        float focus = 1f / Math.Max(1, targets.Count);
        LastGatherWidth = .045f + .15f * focus;
        foreach (var target in targets)
        {
            Vector3 end = target.FxPoint;
            {
                Vector3 above = end + Vector3.Up * 1.4f;
                var seal = fx.Sprite(above, AkaMagic.Seal, targets.Count == 1 ? .8f : 1.2f);
                fx.Draw(age => {
                    seal.Rotation = new Vector3(0, 0, age * .7f);
                    seal.Scale = Vector3.One * (.6f + Math.Min(1, age / .25f) * .4f);
                    seal.Modulate = new Color(1, 1, 1, Mathf.Clamp((.83f - age) / .16f, 0, .85f));
                });
                int needles = targets.Count == 1 ? 1 : 3;
                for (int k = 0; k < needles; k++)
                {
                    Vector3 from = above + _camera.GlobalBasis.X * ((k - (needles - 1) * .5f) * .22f);
                    var spear = fx.Sprite(from, AkaMagic.Spear, 1.7f);
                    fx.Draw(age => {
                        float p = (age - .32f) / .38f;
                        spear.Visible = p >= 0 && p < 1;
                        spear.GlobalPosition = from.Lerp(end, Mathf.Clamp(p * p, 0, 1));
                        spear.Scale = new Vector3(targets.Count == 1 ? 2.6f : .65f + focus, 1, 1);
                    });
                }
            }
            fx.Flash(end, AkaMagic.Glow, .8f + focus * .5f, .70f, .30f);
            fx.Purchased(end, false, new Color("f65b7b"), .8f + focus, .70f);
        }
        return fx;
    }
    private GatherCast3D GareVolley(BattlePawn3D actor, IReadOnlyList<BattlePawn3D> targets, int stock)
    {
        PebbleVolleyPlays++; actor.BeginGatherPose("gare_load"); SetPebbleBag(actor, 0);
        bool focused = targets.Count == 1;
        float delay = focused ? GatherCast3D.GareFocusExtraCharge : 0;
        float fireAt = .32f + delay;
        // 単体だけ集光を延ばす。発射後の飛翔・命中・着地はまとめて同じ分だけ後ろへ送る。
        var fx = GatherEffect(1.40f + delay, .70f + delay, actor);
        LastGatherTargets = targets.Count;
        float power = Math.Clamp(MathF.Sqrt(Math.Max(1, stock) / 12f), .65f, 1.8f);
        LastGatherWidth = (.045f + .15f / Math.Max(1, targets.Count)) * power;
        fx.Flow(BagPoint(actor), actor.GatherPortraitPoint(new Vector2(932, 305), _camera),
            GatherArt.Crystal, 6, 0, .22f, .16f);
        if (focused)
        {
            fx.GarePrismCharge(() => actor.GatherPortraitPoint(actor.MovementPortrait == "gare_fire"
                ? new Vector2(984, 350) : new Vector2(932, 305), _camera), _camera, power, fireAt);
            // 集光中だけ溜め音を立ち上げ、反動が始まる瞬間に重いビーム砲へ渡す。
            fx.Sound("gare_focus_charge.wav", -2, .04f, offset: .08f, fadeIn: .82f, fadeOut: .04f, end: fireAt);
            fx.Sound("gare_focus_beam", -6, fireAt, fadeOut: .22f);
            fx.ImpactShake(_camera, .70f + delay, .065f * (.85f + .15f * power));
        }
        // 集束砲の決め音はビームと銃声だけ。結晶のシャキーンは弾幕に残す。
        if (!focused) fx.Sound("gare_crystal_shine", -8, .35f, offset: .10f, fadeOut: .22f);
        fx.Sound("gare_shotgun", focused ? -11 : -12, fireAt, fadeOut: .22f);
        float hitAt = .70f + delay;
        // 対象の人数分は重ねず、一斉着弾の芯と、その後の結晶の飛散を一回ずつ鳴らす。
        fx.Sound(focused ? "gare_hit_focus.wav" : "gare_hit_scatter.wav", focused ? -7 : -10, hitAt, fadeOut: .16f);
        fx.Sound("gare_crystal_scatter.wav", focused ? -6 : -8, hitAt + .07f, fadeOut: .22f);
        fx.At(.28f, () => {
            actor.BeginGatherPose("gare_fire");
            Vector3 muzzle = actor.GatherPortraitPoint(new Vector2(984, 350), _camera);
            fx.GarePrismaticVolley(muzzle, targets.Select(p => p.FxPoint).ToArray(), _camera, power, delay);
        });
        fx.Draw(age => actor.GatherBlastRecoil(age, power, focused, fireAt));
        return fx;
    }
    internal GatherCast3D DryBlood(BattlePawn3D aka, BattlePawn3D target)
    {
        BloodDryPlays++; aka.BeginGatherPose("susu_cast");
        var fx = GatherEffect(.55f, .33f, aka);
        fx.Flash(BookPoint(aka), AkaMagic.Glow, .28f, 0, .22f);
        fx.Flow(BookPoint(aka), target.FxPoint, AkaMagic.Glow, 1, .04f, .29f, .12f);
        return fx;
    }
    internal void BloodFallout(BattlePawn3D aka, IReadOnlyList<BattlePawn3D> targets)
    {
        if (targets.Count == 0) return;
        BloodFalloutPlays++;
        var fx = GatherEffect(.80f, .40f);
        fx.Flash(aka.FxPoint, AkaMagic.Glow, 1.4f, 0, .40f);
        foreach (var target in targets)
        {
            fx.Flow(aka.FxPoint, target.FxPoint, AkaMagic.Glow, 8, 0, .40f, .25f);
            fx.Purchased(target.FxPoint, true, new Color("cf4969"), 3.5f, .38f);
            fx.Flash(target.FxPoint, AkaMagic.Glow, 1.1f, .38f, .40f);
        }
    }
    internal void ClearGatherPawn(BattlePawn3D pawn)
    {
        if (_pebbleBags.TryGetValue(pawn.InstanceId, out var bag)) { bag.Bag.Hide(); bag.Light.Hide(); }
        pawn.EndGatherPose();
    }
}
