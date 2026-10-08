using BattleCore;
using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private ZanPresentation _zan = new();
    private bool _zanFastEvent;

    private async Task<bool> PlayZan(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (e.Kind == BattleEventKind.TurnStart) _battleField.ResetZanTurn();
        if (_zan.Starts.TryGetValue(index, out var start) && actor is not null && target is not null)
        {
            int token = _playToken;
            double duration = _battleField.ShowZanFlurry(actor, target, start, _speed);
            // 束の待ちだけ残す。HPはこの後、台本順に同期する。
            _zanFastEvent = false;
            await Delay(duration);
            if (token != _playToken || !_battleMode) return true;
            _zanFastEvent = true;
        }
        if (_zan.Hits.TryGetValue(index, out var group))
        {
            _battleField.VengeancePlays++;
            if (e.Kind == BattleEventKind.Damage) target?.SetHp(e.HpAfter);
            _battleField.RecordZanHit(actor, e.Kind == BattleEventKind.Damage ? e.Amount : 0);
            AppendLog($"  [color=#ff526c]【仇討ち】{NameOf(e.ActorId)} → {NameOf(e.TargetId)}  {e.Amount}[/color]");
            return true;
        }
        if (_zan.Recoils.TryGetValue(index, out var recoil))
        {
            target?.SetHp(e.HpAfter);
            if (index == recoil.Recoils[0]) _battleField.ShowZanRecoil(target, _speed);
            AppendLog($"  [color=#c86b7b]【返り血】{NameOf(e.TargetId)} −{e.Amount}[/color]");
            return true;
        }
        return false;
    }

    private async Task FinishZan(int index)
    {
        if (!_zan.Ends.TryGetValue(index, out var group)) return;
        int token = _playToken;
        _battleField.FinishZanFlurry(group);
        _zanFastEvent = false;
        // 傷の一拍を挟んで叫びと癒しを見せる。各HealのHP同期は済んでいる。
        if (group.Recoils.Count > 0) await Delay(0.10);
        if (token != _playToken || !_battleMode) return;
        foreach (var voice in group.Rallies.GroupBy(r => r.Cues[0].ActorId))
        {
            var merged = new MarkRallyPresentation.Rally { EnemyId = group.Enemy };
            foreach (var recipient in voice.SelectMany(r => r.Cues).GroupBy(e => e.TargetId))
            {
                var last = recipient.Last();
                merged.Cues.Add(new BattleEvent { Kind = BattleEventKind.MarkRally, ActorId = last.ActorId,
                    TargetId = last.TargetId, PartnerId = last.PartnerId, Turn = last.Turn,
                    Slot = last.Slot, Amount = recipient.Sum(e => e.Amount), HpAfter = last.HpAfter });
            }
            _battleField.ShowMarkRally(merged, _speed);
            foreach (var cue in merged.Cues)
                if (cue.Amount > 0) _battleField.HealPopup(_battleField.FindPawn(cue.TargetId), cue.Amount);
        }
        if (group.Rallies.Count > 0) await Delay(0.28);
    }
}
