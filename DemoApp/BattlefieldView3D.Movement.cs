using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal readonly Dictionary<BattleEventKind, int> MovementPlays = new();
    private readonly Dictionary<int, int> _windStages = new();
    internal int MovementArrowPlays { get; private set; }
    internal int MovementPiercePlays { get; private set; }
    internal int MovementBarragePlays { get; private set; }
    internal int MovementShotPlays { get; private set; }

    private void ResetMovement()
    {
        MovementPlays.Clear();
        _windStages.Clear();
        MovementArrowPlays = 0;
        MovementPiercePlays = 0;
        MovementBarragePlays = 0;
        MovementShotPlays = 0;
        HaneDropkickPlays = 0;
        HanePalmPlays = 0;
        BasaStormPlays = 0;
        TailwindGustPlays = 0;
        BasaSweepPlays = 0;
    }

    internal double ShowMovementCue(BattleEvent e, double speed, BattleEvent? springGuard = null)
    {
        MovementPlays[e.Kind] = MovementPlays.GetValueOrDefault(e.Kind) + 1;
        var actor = FindPawn(e.ActorId);
        var target = FindPawn(e.TargetId);
        var partner = FindPawn(e.PartnerId);
        double s = Math.Max(0.1, speed);
        void Flow(BattlePawn3D? a, BattlePawn3D? b, Color tint, float curl = 0.25f)
        {
            if (a is not null && b is not null)
                MovementFx.Flow(_fxRoot, _camera, a.FxPoint, b.FxPoint, tint, 0.32 / s, curl);
        }
        void Coil(BattlePawn3D? p, Color tint, float radius = 0.65f, double duration = 0.34)
        {
            if (p is not null)
            {
                Vector3 last = p.FxPoint;
                MovementFx.Coil(_fxRoot, _camera, () => IsInstanceValid(p) ? p.FxPoint : last,
                    tint, radius, duration / s);
            }
        }
        switch (e.Kind)
        {
            case BattleEventKind.Retreat:
            case BattleEventKind.Regroup:
                _attackAudio.PlayMovementSound(MovementSound.Vine);
                bool urgent = e.Kind == BattleEventKind.Retreat;
                int nth = Math.Clamp(e.StatusRemaining ?? 1, 1, 3);
                if (urgent && actor?.UnitId == "shio")
                {
                    actor.ShowMovementPortrait("shio_retreat", 0.36 / (1 + (nth - 1) * 0.08));
                    if (target is not null)
                        MovementFx.Flow(_fxRoot, _camera, actor.MovementPortraitPoint(new Vector2(975, 340), _camera),
                            target.FxPoint, MovementFx.Leaf, 0.32 / s, 0.48f);
                }
                else Flow(actor, target, MovementFx.Leaf, urgent ? 0.48f : 0.16f);
                Coil(target, MovementFx.Leaf, urgent ? 0.58f : 0.78f, urgent ? 0.26 / (1 + nth * 0.12) : 0.45);
                Flow(target, partner, MovementFx.Leaf, 0.12f);
                actor?.MovementPose(urgent ? 0.25f + nth * 0.07f : 0.10f);
                return urgent ? 0.055 / (1 + nth * 0.12) : 0.15;
            case BattleEventKind.Evade:
                if (actor is not null && actor.MovementPortrait != "sero_last_dodge")
                    _attackAudio.PlayMovementSound(MovementSound.Evade);
                actor?.ShowMovementPortrait("sero_evade", 0.32);
                actor?.MovementPose(0.60f, 0.26f);
                Coil(actor, MovementFx.Wind, 0.44f, 0.26);
                return 0.045;
            case BattleEventKind.LastDodge:
                if (actor is not null) _attackAudio.PlayMovementSound(MovementSound.Evade);
                actor?.ShowMovementPortrait("sero_last_dodge", 0.58);
                actor?.MovementPose(1.25f, 0.50f);
                if (actor is not null)
                {
                    actor.BeginBonusAfterimage(MovementFx.Wind, 0.44 / s, false);
                    MovementFx.Flow(_fxRoot, _camera, actor.FxPoint + Vector3.Up * 0.9f,
                        actor.FxPoint + Vector3.Down * 0.5f, MovementFx.Wind, 0.38 / s, 0.7f, 5);
                }
                return 0.28;
            case BattleEventKind.Decoy:
                actor?.ShowMovementPortrait("sero_decoy", 0.36);
                actor?.MovementPose(-0.32f, 0.32f);
                Flow(target, actor, MovementFx.Arrow, 0.12f);
                Coil(actor, MovementFx.Arrow, 0.35f + Math.Clamp(e.Amount, 0, 100) * 0.002f);
                return 0.10;
            case BattleEventKind.DecoyShow:
                actor?.SetDecoyShown(e.Slot == 1, e.Amount);
                if (e.Slot == 1)
                {
                    actor?.ShowMovementPortrait("sero_decoy", 0.20);
                    Coil(actor, MovementFx.Arrow, 0.42f, 0.18);
                }
                return 0;
            case BattleEventKind.ShioStage:
                if (actor is not null) MovementFx.Coil(_fxRoot, _camera, actor.Home + Vector3.Up * 0.22f,
                    MovementFx.Leaf, 0.42f + e.Slot * 0.12f, 0.40 / s, 1);
                return 0;
            case BattleEventKind.EvadeStage:
                Coil(actor, MovementFx.Wind, 0.38f + e.Slot * 0.10f);
                return 0;
            case BattleEventKind.DisarrayStage:
                if (actor is not null) _windStages[actor.InstanceId] = e.Slot;
                Coil(actor, MovementFx.Wind, 0.60f + e.Slot * 0.18f);
                return 0;
            case BattleEventKind.Disarray:
                Flow(actor, target, MovementFx.Wind, 0.40f);
                target?.SetStatusIcon(StatusKeys.Confused, true);
                Coil(target, MovementFx.Wind, 0.42f);
                return 0.045;
            case BattleEventKind.Spring:
                ShowHanePalmStrike(actor, target, FindPawn(springGuard?.TargetId));
                return 0;
            case BattleEventKind.SpringGuard:
                // 味方へ手を差し出す因果だけ。掌の接触・音・敵の移動は次のSpringで1回。
                Flow(actor, target, MovementFx.Bounce, 0.16f);
                return 0;
            case BattleEventKind.Tailwind:
                ShowTailwindGust(actor, target, partner, s);
                return 0.12;
            case BattleEventKind.KillImpact:
                if (e.Text == ImpactLabels.Tumble)
                {
                    actor?.ShowMovementPortrait("yomi_stumble", 0.42);
                    actor?.MovementPose(0.8f, 0.38f);
                    Flow(actor, target, MovementFx.Creak, 0.35f);
                }
                else
                {
                    Flow(FindPawn(e.SpreadFromId), target, MovementFx.Creak, 0.05f);
                    Coil(target, MovementFx.Creak, 0.45f);
                }
                return 0.045;
            case BattleEventKind.Overflow:
                if (target is not null)
                {
                    float strength = Math.Clamp((e.StatusRemaining ?? 0) / 15f, 0, 1);
                    Vector3 fist = target.FxPoint + Vector3.Right * (target.Team == 0 ? 0.35f : -0.35f);
                    for (int i = 0; i < 3; i++)
                        MovementFx.Flow(_fxRoot, _camera, target.Home + new Vector3((i - 1) * 0.45f, 0.35f, 0),
                            fist, MovementFx.Leaf.Lerp(new Color("d9e99d"), strength), 0.32 / s, 0.12f, 1);
                    MovementFx.Coil(_fxRoot, _camera, fist, MovementFx.Leaf, 0.10f + strength * 0.18f, 0.32 / s, 1);
                }
                return 0.025;
            case BattleEventKind.StaggerBreach:
                if (actor is not null && target is not null)
                    MovementFx.Ribbon(_fxRoot, _camera,
                        t => actor.FxPoint.Lerp(target.FxPoint, t) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * 0.8f,
                        MovementFx.Arrow, 0.016f, 0.30 / s, true);
                return 0;
            // 見出しでは発射しない。直後の Attack が矢や突風を一度だけ出す。
            default: return 0;
        }
    }

    internal void MoveWithCue(BattlePawn3D pawn, int slot, BattleEvent? cue, BattlePawn3D? mover, bool storm = false)
    {
        if (pawn.FinishBlastMove(PawnPosition(pawn.Team, slot)))
        {
            pawn.Slot = slot;
            return;
        }
        float arc = 0.12f;
        double seconds = 0.18;
        Color tint = MovementFx.Wind;
        if (cue?.Kind is BattleEventKind.Retreat or BattleEventKind.Regroup) tint = MovementFx.Leaf;
        if (cue?.Kind == BattleEventKind.Regroup) seconds = 0.28;
        if (cue?.Kind == BattleEventKind.Evade && cue.ActorId == pawn.InstanceId) arc = 0.30f;
        bool thrown = cue?.TargetId == pawn.InstanceId && (cue.Kind is BattleEventKind.Blast or BattleEventKind.Spring
            || cue.Kind == BattleEventKind.KillImpact && cue.Text == ImpactLabels.Blow);
        if (thrown)
        {
            arc = cue!.Kind == BattleEventKind.Blast ? 1.25f : 0.65f;
            seconds = cue.Kind == BattleEventKind.Blast ? 0.34 : 0.24;
            tint = cue.Kind == BattleEventKind.KillImpact ? MovementFx.Creak : MovementFx.Bounce;
            pawn.MovementPose(0.95f, (float)seconds);
        }
        if (storm) { arc = 0.95f; seconds = 0.64; }
        if (cue?.Kind == BattleEventKind.Tailwind) { arc = 0.32f; seconds = 0.26; }
        if (mover?.UnitId == "basa" && !storm && cue?.Kind != BattleEventKind.Tailwind)
            _attackAudio.PlayMovementSound(MovementSound.Wind);
        Vector3 start = pawn.FxPoint;
        Vector3 end = PawnPosition(pawn.Team, slot);
        float curl = mover?.UnitId == "basa" ? 0.30f + _windStages.GetValueOrDefault(mover.InstanceId) * 0.14f : 0.15f;
        MovementFx.Flow(_fxRoot, _camera, start, end + Vector3.Up * 0.85f, tint,
            (seconds + 0.10) / pawn.AnimationSpeed, curl, thrown ? 5 : 2);
        pawn.Slot = slot;
        pawn.AnimateMovement(end, arc, seconds, thrown ? MovementLandingSound(pawn) : null, windCarry: storm);
        if (storm) MovementFx.CarryVortex(_fxRoot, _camera, pawn, seconds / pawn.AnimationSpeed);
    }

    // 着地を台本のMove時刻ではなく、実際の描画の終了へ合わせる。停止・再開で古い予約は無効。
    private Action MovementLandingSound(BattlePawn3D pawn, bool secondary = false)
        => MovementCompletionSound(pawn, MovementSound.Landing, secondary);

    // 音の予約は演出開始時に作り、途中で停止された場合は鳴らさない。
    private Action MovementCompletionSound(BattlePawn3D pawn, MovementSound sound, bool secondary = false)
    {
        int generation = _specialGeneration;
        int audioGeneration = _attackAudio.MovementSoundGeneration;
        return () => {
            if (generation == _specialGeneration && audioGeneration == _attackAudio.MovementSoundGeneration
                && IsInsideTree() && IsInstanceValid(pawn) && pawn.IsInsideTree() && pawn.Hp > 0)
                _attackAudio.PlayMovementSound(sound, secondary);
        };
    }

    internal void MovementLeaves(BattlePawn3D pawn)
    {
        MovementFx.Leaves(_fxRoot, pawn, 0.36 / pawn.AnimationSpeed);
    }

    private async Task<bool> MovementAttack(BattlePawn3D from, BattlePawn3D to,
        IReadOnlyList<BattlePawn3D> hits, AttackPattern pattern, BattleEvent? cue, string? arrowStates, int? blastDestination)
    {
        double s = Math.Max(0.1, from.AnimationSpeed);
        if (from.UnitId == "sero")
        {
            MovementArrowPlays++;
            if (cue?.Kind == BattleEventKind.MoveShot)
            {
                MovementShotPlays++;
                from.ShowMovementPortrait("sero_move_shot", 0.40);
                from.MovementPose(-0.38f, 0.22f);
                from.BeginBonusAfterimage(MovementFx.Wind, 0.20 / s, false);
            }
            var arrowHit = MovementCompletionSound(to, MovementSound.ArrowHit);
            Vector3 origin = cue?.Kind == BattleEventKind.MoveShot
                ? from.MovementPortraitPoint(new Vector2(938, 425), _camera) : from.FxPoint;
            Vector3 end = pattern == AttackPattern.Pierce
                ? hits.OrderByDescending(p => p.FxPoint.DistanceSquaredTo(from.FxPoint)).First().FxPoint : to.FxPoint;
            if (pattern == AttackPattern.Pierce) end += (end - from.FxPoint).Normalized() * 0.9f;
            if (pattern == AttackPattern.Pierce)
            {
                MovementPiercePlays++;
                MovementFx.PiercingShot(_fxRoot, _camera, origin, end,
                    hits.Select(p => p.FxPoint).ToArray(), 0.16 / s);
            }
            else if (cue?.Kind == BattleEventKind.Barrage)
            {
                MovementBarragePlays++;
                MovementFx.PiercingShot(_fxRoot, _camera, origin, end, Array.Empty<Vector3>(), 0.16 / s, barrage: true);
            }
            else MovementFx.Shot(_fxRoot, _camera, origin, end, MovementFx.Arrow, 0.16 / s);
            foreach (string state in (arrowStates ?? "").Split(','))
            {
                Color? tint = state switch {
                    "毒" => UiKit.Poison,
                    "燃焼" => new Color("ff943f"), "感電" => ThunderFx.Cyan, _ => null };
                if (tint is Color color)
                    MovementFx.Flow(_fxRoot, _camera, origin, end, color, 0.22 / s, 0.12f, 1);
            }
            await ToSignal(GetTree().CreateTimer(Math.Max(0.005, 0.16 / s)), SceneTreeTimer.SignalName.Timeout);
            arrowHit();
            return true;
        }
        if (from.UnitId == "basa" && (pattern == AttackPattern.Sweep || cue?.Kind == BattleEventKind.Squall))
        {
            await ShowBasaSweep(from, hits, s);
            return true;
        }
        if (cue?.Kind == BattleEventKind.Squall)
        {
            foreach (var hit in hits) MovementFx.Flow(_fxRoot, _camera, from.FxPoint, hit.FxPoint,
                MovementFx.Wind, 0.24 / s, 0.5f, 4);
            return true;
        }
        if (cue?.Kind == BattleEventKind.Blast)
        {
            await ShowHaneBlast(from, to, hits, blastDestination);
            return true;
        }
        return false;
    }

}
