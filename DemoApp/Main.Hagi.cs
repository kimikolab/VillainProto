using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private HagiPresentation _hagi = new();
    private int _hagiFrame = -1;
    private readonly HashSet<int> _shownHagiDamage = new();
    private void ObserveHagiEvent(BattleEvent e, int index, BattlePawn3D? target)
    {
        if (index == 0) { _hagiFrame = -1; _shownHagiDamage.Clear(); }
        foreach (var frame in _hagi.Frames.Where(p => p.Key > _hagiFrame && p.Key <= index))
            foreach (var state in frame.Value) _battleField.ApplyHagiState(state);
        _hagiFrame = index;
        if (e.Kind == BattleEventKind.Death && target is not null) _battleField.ShowHagiOffering(target);
    }
    private async Task PlayHagiAttack(int index, BattlePawn3D? actor, BattlePawn3D? primary)
    {
        if (!_hagi.Swings.TryGetValue(index, out var cue)) return;
        int token = _playToken;
        var targets = cue.Damage.Select(i => _battleField.FindPawn(_result!.Events[i].TargetId))
            .OfType<BattlePawn3D>().Distinct().ToList();
        if (targets.Count == 0 && primary is not null) targets.Add(primary);
        await _battleField.PlayHagiSwing(actor, targets, cue, () => {
            if (token != _playToken || !_battleMode) return;
            // 列の刈り取りだけを同時接触へ寄せる。Death・回復・通常被弾の順序は元の台本で流す。
            foreach (int hit in cue.Damage.Where(_hagi.Reaps.Contains))
            {
                if (!CanPreviewViewerEvent(hit) || !_batchedDamageIndices.Add(hit)) continue;
                var damage = _result!.Events[hit];
                ShowDamage(hit, damage, actor, _battleField.FindPawn(damage.TargetId), false);
            }
        });
    }
    private bool ShowHagiDamage(int index, BattleEvent e, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (target is null || actor?.UnitId != "hagi" || (!_hagi.Reaps.Contains(index) && !e.FriendlyFire)) return false;
        target.SetHp(e.HpAfter);
        // 燃焼の接触口から同じDamageが戻ってきても、かすり傷と数字を二重に出さない。
        if (!_shownHagiDamage.Add(index)) return true;
        _battleField.ShowHagiDamage(target, e.Amount, _hagi.Reaps.Contains(index), e.FriendlyFire);
        FireHitDamage(index, target);
        return true;
    }
}
