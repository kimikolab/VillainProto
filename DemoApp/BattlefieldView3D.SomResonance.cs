using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    private readonly HashSet<int> _somStanding = new();
    private readonly Dictionary<int, int> _somOwners = new();
    private readonly HashSet<int> _somThrowIntroduced = new();
    private readonly List<SomLightning3D> _somBolts = new();
    internal IEnumerable<SomLightning3D> SomBolts => _somBolts.Where(b => IsInstanceValid(b) && !b.IsQueuedForDeletion());
    internal int SomThrows, SomReturns, SomReturnBranches, SomHushedReturns, SomBackswings, SomThreadThrows, SomBackfires, SomScraps;
    internal bool UsePurchasedSomFx { get; set; } = true;

    internal void AddSomStandingPawn(DemoOpening opening, double speed)
    {
        AddSummon(opening, ordinaryEffect: false, appearanceSpeed: speed);
        var pawn = FindPawn(opening.InstanceId)!; pawn.AnimationSpeed = speed;
        pawn.Hide(); pawn.Hud.Hide(); _somStanding.Add(opening.InstanceId);
    }
    private void RemoveSomStandingPawn(int id)
    {
        if (!_somStanding.Remove(id) || !_pawns.Remove(id, out var pawn)) return;
        CutWebFor(pawn); ClearBindingsFor(pawn);
        pawn.SetHp(0); pawn.Hide(); pawn.Hud.Hide(); pawn.QueueFree();
        // Hudの所有権と解放はBattlePawn3DのPredelete通知が管理する。
    }
    private void EndSomResonance()
    {
        _somBolts.Clear();
        _somOwners.Clear(); _somThrowIntroduced.Clear();
        foreach (int id in _somStanding.ToArray()) RemoveSomStandingPawn(id);
    }

    private void SomBolt(Vector3 from, Vector3 to, bool reverse, double speed, double seconds, Color? tint = null)
    {
        var bolt = new SomLightning3D(); SomRoot.AddChild(bolt);
        bolt.Configure(_camera, from, to, reverse, speed, seconds, tint);
        bolt.SetClock(_somPaused, speed); _somBolts.Add(bolt);
    }
    internal void ThrowSomLightning(BattleEvent e, double speed)
    {
        if (ElectricPoint(e.ActorId) is not Vector3 from || ElectricPoint(e.TargetId) is not Vector3 to) return;
        SomThrows++;
        if (e.ActorId is int id && _somBeasts.TryGetValue(id, out var beast)) beast.Throw();
        if (e.ActorId is int source && _somOwners.TryGetValue(source, out int owner) && FindPawn(owner) is { } som)
        {
            som.ShowSomExpression("som_stunned", .9);
            if (_somThrowIntroduced.Add(owner)) Float(som, "おまえまで……", SomFx.Violet, false);
        }
        SomBolt(from, to, false, speed, SomFx.ThrowSeconds, SomFx.SpiritRebel);
        _attackAudio.PlaySom(SomSound.Throw);
    }
    internal void ReceiveSomLightning(BattleEvent e, double speed)
    {
        var ally = FindPawn(e.TargetId); if (ally is null) return;
        bool resonance = e.Text is BeastThrowLabels.Resonance or BeastThrowLabels.Hushed;
        if (resonance)
        {
            var pulse = ShockMarkFx.Sprite(SomRoot, ally.DischargePoint, SomFx.Resonance,
                ally.SomHeight * .6f, ThunderFx.Cyan);
            var tween = pulse.CreateTween();
            tween.TweenProperty(pulse, "scale", Vector3.One * (e.Text == BeastThrowLabels.Hushed ? 1.05f : 1.5f), .09 / speed);
            tween.TweenProperty(pulse, "modulate:a", 0f, .13 / speed);
            tween.TweenCallback(Callable.From(pulse.QueueFree)); TrackSomTween(tween, speed);
            if (e.Text == BeastThrowLabels.Hushed)
            {
                SomHushedReturns++;
                SomSeal(ally.DischargePoint, speed);
                _attackAudio.PlaySom(SomSound.Stop);
            }
            else
            {
                SomReturns++;
                SomPurchasedBurst(ally.DischargePoint, ally.InstanceId, speed);
                _attackAudio.PlaySom(SomSound.Resonance);
            }
        }
        else SomPurchasedBurst(ally.DischargePoint, ally.InstanceId, speed);
        if (e.Text == BeastThrowLabels.Thread && FindPawn(e.PartnerId) is { } enemy)
        {
            SomThreadThrows++;
            ShowThreadDischarge(ally, enemy, null, false, speed);
        }
    }
    private void SomSeal(Vector3 point, double speed)
    {
        var right = _camera.GlobalBasis.X * .28f;
        var up = _camera.GlobalBasis.Y * .30f;
        // 二本の紫の札が開きかけた共鳴を押さえる。反射する電弧は作らない。
        ShockMarkFx.Beam(SomRoot, point - right - up, point + right + up, SomFx.Murky, .055f, .17 / speed);
        ShockMarkFx.Beam(SomRoot, point - right + up, point + right - up, SomFx.Murky, .055f, .17 / speed);
    }
    internal void ReturnSomLightning(BattleEvent e, double speed)
    {
        if (ElectricPoint(e.ActorId) is Vector3 from && ElectricPoint(e.TargetId) is Vector3 to)
            SomBolt(from, to, true, speed, SomFx.ReturnSeconds);
    }
    internal void BranchSomLightning(BattleEvent e, double speed)
    {
        if (ElectricPoint(e.ActorId) is not Vector3 from || ElectricPoint(e.TargetId) is not Vector3 to) return;
        SomReturnBranches++;
        SomBolt(from, to, false, speed, .10);
        SomPurchasedBurst(to, e.TargetId ?? 0, speed);
    }
    internal void SomBackswing(BattlePawn3D shiga, double speed)
    {
        SomBackswings++; shiga.SetFrightened(false); shiga.InterruptWhip = true; shiga.WhipChainSize = 0;
        shiga.ShowMovementPortrait("shiga_interrupt", .9);
        Float(shiga, "雷を浴びて、打ち返す！", ThunderFx.Cyan, false);
        _attackAudio.PlayShockMark(ShockMarkSound.Interrupt, speed: speed);
    }
    internal bool RemoveSomBeast(int? id, double speed, bool backfire = false, bool scriptedBurst = true)
    {
        if (id is not int beast || !_somBeasts.ContainsKey(beast)) return false;
        if (backfire) SomBackfires++;
        BurstSomBeasts(new[] { beast }, speed);
        if (!scriptedBurst) SomBursts--; // Deathで倒れた獣とBeastBurstは別々に数える。
        RemoveSomStandingPawn(beast);
        return true;
    }
    internal bool GatherSomScrap(BattleEvent e, BattlePawn3D? tsugi, double speed)
    {
        if (tsugi is null || (e.SpreadFromId ?? e.TargetId) is not int id
            || !_somBeastPoints.TryGetValue(id, out var point)) return false;
        SomScraps++;
        PlankFlight(point, tsugi.ScrapPoint, Math.Max(1, e.Amount), .22 / speed, true);
        return true;
    }
}
