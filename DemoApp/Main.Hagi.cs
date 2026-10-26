using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private HagiPresentation _hagi = new();
    private int _hagiFrame = -1;
    private bool _hagiFastEvent;
    private readonly HashSet<int> _shownHagiDamage = new();
    private async Task<bool> PlayHagiEvent(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        _hagiFastEvent = _hagi.FastEvents.Contains(index);
        if (index == 0) { _hagiFrame = -1; _shownHagiDamage.Clear(); }
        foreach (var frame in _hagi.Frames.Where(p => p.Key > _hagiFrame && p.Key <= index))
            foreach (var state in frame.Value) _battleField.ApplyHagiState(state);
        _hagiFrame = index;
        if (e.Kind == BattleEventKind.Reap && e.Text == ReapLabels.Soul && e.Amount > 0
            && e.StatusRemaining is >= 0 and <= 9 && e.Slot is ReapLabels.SoulDeath or ReapLabels.SoulMark or ReapLabels.SoulAlly or 80 or 50 or 20
            && actor?.UnitId == "hagi" && target is not null)
            _battleField.ShowHagiOffering(target, actor, e.Slot);
        if (_hagi.Returns.TryGetValue(index, out var cue))
        {
            await PlayHagiCue(cue, actor, target);
            return true;
        }
        return false;
    }
    private async Task PlayHagiAttack(int index, BattlePawn3D? actor, BattlePawn3D? primary)
    {
        if (!_hagi.Swings.TryGetValue(index, out var cue)) return;
        await PlayHagiCue(cue, actor, primary);
    }
    private async Task PlayHagiCue(HagiPresentation.Swing cue, BattlePawn3D? actor, BattlePawn3D? primary)
    {
        int token = _playToken;
        var targets = cue.Damage.Select(i => _battleField.FindPawn(_result!.Events[i].TargetId))
            .OfType<BattlePawn3D>().Distinct().ToList();
        if (targets.Count == 0 && primary is not null) targets.Add(primary);
        await _battleField.PlayHagiSwing(actor, targets, cue, () => {
            if (token != _playToken || !_battleMode) return;
            // 通常の命中も接触へ合わせる。巻き込みの途中の返し刃より先のHPは先取りしない。
            int first = cue.Damage.DefaultIfEmpty(int.MaxValue).Min();
            int nextReturn = _hagi.Returns.Keys.Where(i => i > first).DefaultIfEmpty(int.MaxValue).Min();
            foreach (int hit in cue.Damage.Where(i => i < nextReturn))
            {
                if (!CanPreviewViewerEvent(hit) || !_batchedDamageIndices.Add(hit)) continue;
                var damage = _result!.Events[hit];
                ShowDamage(hit, damage, actor, _battleField.FindPawn(damage.TargetId), false);
            }
        }, _battleField.FindPawn(cue.Partner));
    }
    private bool ShowHagiDamage(int index, BattleEvent e, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (target is null || actor?.UnitId != "hagi" || (!_hagi.Reaps.Contains(index) && !e.FriendlyFire && !_hagi.HeavyHits.Contains(index))) return false;
        target.SetHp(e.HpAfter);
        // 燃焼の接触口から同じDamageが戻ってきても、かすり傷と数字を二重に出さない。
        if (!_shownHagiDamage.Add(index)) return true;
        _battleField.ShowHagiDamage(target, e.Amount, _hagi.Reaps.Contains(index), e.FriendlyFire, _hagi.HeavyHits.Contains(index));
        FireHitDamage(index, target);
        return true;
    }
}
