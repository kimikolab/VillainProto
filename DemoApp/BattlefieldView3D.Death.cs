using BattleCore;
using Godot;
using System;
using System.Linq;

public partial class BattlefieldView3D
{
    private bool _deathPaused;
    private double _deathSpeed = 1;
    private Vector3? _lastDeathPlace;
    internal int GraveAwakenings, GraveOfferings, GraveSlashes, SoulStitches, DeathSongs;
    internal static Color GraveColor(int layers) => layers >= 9 ? new Color("a366f5")
        : layers >= 6 ? new Color("9b9bff") : new Color("c7edff");
    internal void SetDeathClock(bool paused, double speed)
    {
        _deathPaused = paused; _deathSpeed = Math.Max(0.1, speed);
        _attackAudio?.PauseRicaEvents(paused);
        foreach (var pawn in _pawns.Values) pawn.SetRicaMotionClock(paused,_deathSpeed);
        foreach (var pawn in _pawns.Values)
        {
            pawn.HagiPaused = paused;
            if (pawn.UnitId == "hagi") pawn.AnimationSpeed = _deathSpeed;
        }
    }
    private void ResetDeathPresentation()
    {
        ResetHagiPresentation();
        _attackAudio?.StopRicaEvents();
        foreach (var pawn in _pawns.Values)
        {
            pawn.StopDeathVisual();
            if (pawn.UnitId == "rica") pawn.CancelAttackClip();
        }
        if (_fxRoot is not null)
        {
            foreach (var fx in _fxRoot.GetChildren().OfType<DeathFx3D>()) { fx.Hide(); fx.QueueFree(); }
            foreach (var fx in _fxRoot.GetChildren().OfType<RicaSlashFx3D>()) { fx.Hide(); fx.QueueFree(); }
        }
        _lastDeathPlace = null;
        GraveAwakenings = GraveOfferings = GraveSlashes = SoulStitches = DeathSongs = 0;
        RicaAttackClipPlays = 0; RicaPlayedClips.Clear(); RicaLinksPlayed.Clear();
        RicaReturnContactPoint=Vector3.Zero;
        _deathPaused = false; _deathSpeed = 1;
    }
    private void DeathFlow(Vector3 from, Vector3 to, Color tint, DeathFx3D.Shape shape = DeathFx3D.Shape.Flow, float strength = 1)
    {
        var fx = new DeathFx3D(); _fxRoot.AddChild(fx);
        fx.Start(from, to, tint, shape, strength, () => (_deathPaused, _deathSpeed));
    }
    internal bool ApplyDeathState(DeathPresentation.State state)
    {
        if (FindPawn(state.Id) is not { } pawn) return false;
        int before = pawn.GraveStage, oldLives = pawn.SoulKnots;
        if (pawn.UnitId is "rica" or "wight" or "ere" || state.Split)
            pawn.EnsureDeathAura(() => (_deathPaused, _deathSpeed));
        pawn.SetDeathState(state.Layers, state.Lives, state.Sound);
        if (state.Split) pawn.SetSoulMaxHp(state.MaxHp);
        bool awake = pawn.UnitId == "rica" && pawn.GraveStage > before && pawn.DeathActive;
        if (awake)
        {
            GraveAwakenings++;
            if (IsVisibleInTree()) _attackAudio.PlayRicaAwakening(pawn.GraveStage);
            DeathFlow(pawn.FxPoint - Vector3.Up * 0.6f, pawn.FxPoint, GraveColor(state.Layers), DeathFx3D.Shape.Burst, 1 + pawn.GraveStage * 0.28f);
        }
        if (state.Lives > oldLives)
        {
            var vel = _pawns.Values.FirstOrDefault(p => p.UnitId == "vel" && p.Team == pawn.Team && p.Hp > 0);
            if (vel is not null) { SoulStitches++; DeathFlow(vel.FxPoint, pawn.FxPoint, new Color("edf6ff")); }
        }
        return awake;
    }
    internal void ShowDeathOffering(BattlePawn3D dead)
    {
        _lastDeathPlace = dead.FxPoint;
        bool absorbs = false;
        foreach (var rica in _pawns.Values.Where(p => p.UnitId == "rica" && p != dead && p.Team == dead.Team && p.Hp > 0))
        {
            GraveOfferings++;
            absorbs |= rica.DeathActive;
            DeathFlow(dead.FxPoint, rica.FxPoint, GraveColor(rica.GraveLayers));
        }
        if (absorbs && IsVisibleInTree()) _attackAudio.PlayRicaAbsorption();
        if (dead.UnitId is "spore" or "wight" or "vel")
            DeathFlow(dead.FxPoint, dead.FxPoint, dead.UnitId == "spore" ? new Color("b1bd78") : new Color("d1e3f2"), DeathFx3D.Shape.Burst);
    }
    internal void ShowDeathCost(BattlePawn3D pawn, bool rot)
    {
        if (rot) pawn.ShowWightDecay();
        DeathFlow(pawn.FxPoint, rot ? pawn.FxPoint - Vector3.Up : _lastDeathPlace ?? pawn.FxPoint,
            rot ? new Color("89909d") : GraveColor(pawn.GraveLayers), rot ? DeathFx3D.Shape.Ash : DeathFx3D.Shape.Flow);
        if (!rot) pawn.SwayPoisonSip();
    }
    internal void ShowDeathSummon(BattlePawn3D? caster, BattlePawn3D summon)
    {
        summon.EnsureDeathAura(() => (_deathPaused, _deathSpeed));
        if (summon.UnitId == "wight")
        {
            DeathFlow(_lastDeathPlace ?? summon.FxPoint, summon.FxPoint, new Color("a8bccd"));
            DeathFlow(summon.GlobalPosition, summon.FxPoint, new Color("c4d9e5"), DeathFx3D.Shape.Rise);
        }
        else if (summon.UnitId == "spore")
        {
            caster?.MovementPose(0.18f, 0.30f);
            if (caster is not null) DeathFlow(caster.FxPoint, summon.FxPoint, new Color("aeba79"));
            DeathFlow(summon.GlobalPosition, summon.FxPoint, new Color("b1bd78"), DeathFx3D.Shape.Rise);
        }
    }
    internal void ShowSoulRise(BattlePawn3D pawn)
    {
        DeathFlow(pawn.GlobalPosition, pawn.FxPoint + Vector3.Up * 0.4f, new Color("edf6ff"), DeathFx3D.Shape.Rise);
    }
    private void ShowGraveSlash(BattlePawn3D from, System.Collections.Generic.IReadOnlyList<BattlePawn3D> hits)
    {
        GraveSlashes++;
        int stage = from.GraveStage;
        if (stage >= 3 && from.RicaClipName == "rica_descend" && from.AttackClipVertical == 0)
            _attackAudio.PlayRicaLanding();
        if (stage == 0)
        {
            foreach (var hit in hits)
            {
                Vector3 point=hit.FxPoint+Vector3.Up*.35f;
                if(from.AttackClipPlaying && from.RicaClipName=="rica_shovel")
                {
                    var sprite=from.DeathSprite;
                    Vector3 blade=from.MovementPortraitPoint(from.CurrentRicaAnchors().Tip*sprite.Texture.GetSize(),_camera);
                    point=blade.Lerp(point,.25f);
                }
                var impact = new RicaSlashFx3D(); _fxRoot.AddChild(impact);
                impact.Start(point,"rica_shovel",new Color("ffd7a6"),_camera,
                    () => (_deathPaused,_deathSpeed),stage:0);
            }
        }
        else
        {
            // 薙ぎは敵ごとの線ではなく、実際の命中点を覆う一枚の刃として見せる。
            Vector3 center = hits.Aggregate(Vector3.Zero, (p, hit) => p + hit.FxPoint) / Math.Max(1,hits.Count);
            if(from.RicaClipName=="rica_return")
            {
                // 独立した円弧を回さず、現在のエネルギー刃が対象を通る点で光らせる。
                var (root,tip)=from.RicaBladeSegment(_camera);Vector3 blade=tip-root;
                center=root+blade*Math.Clamp((center-root).Dot(blade)/Math.Max(.0001f,blade.LengthSquared()),0,1);
                RicaReturnContactPoint=center;
            }
            Vector3 right = _camera.GlobalBasis.X.Normalized();
            float span = hits.Count > 1 ? hits.Max(p => p.FxPoint.Dot(right)) - hits.Min(p => p.FxPoint.Dot(right)) : 0;
            var flash = new RicaSlashFx3D(); _fxRoot.AddChild(flash);
            flash.Start(center, from.RicaClipName ?? "rica_crescent", GraveColor(from.GraveLayers),
                _camera, () => (_deathPaused, _deathSpeed),span,stage);
        }
        // 画面を覆う面は増やさず、段が上がるほど刃の幅・余韻と身体の振りを強める。
        if (!from.AttackClipPlaying) from.MovementPose(0.14f + stage * 0.06f, 0.18f);
    }
}
