using BattleCore;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private TickPresentation _ticks = new();
    private TurnTickPresentation _turnTicks = new();
    private TurnTickRange? _collectingTurnTicks;
    private double? _tickDelayBudget;
    private int _tickPlays, _inverseTickPlays;

    private void CollectTurnTick(BattleEvent e, BattlePawn3D? actor, BattlePawn3D? target)
    {
        // 台本は元の順で通る。HPと数字の表示だけは、この区間全体の一拍へ渡す。
        if (e.Kind == BattleEventKind.Status)
        {
            _tickPlays++;
            if (e.SourceTrait == TraitId.Inverse) _inverseTickPlays++;
            AppendLog($"  [{e.Text}] → {NameOf(e.TargetId)}");
        }
        else if (e.Kind == BattleEventKind.FireArmor)
        {
            _battleField.ShowFireCue(e, actor, target, _speed, groupedTick: true);
            AppendLog($"  [color=#ffbd72]{e.Text}[/color] → {NameOf(e.TargetId)}");
        }
        else
        {
            bool heal = e.Kind == BattleEventKind.Heal;
            AppendLog($"  [color=#{(heal ? UiKit.Heal : UiKit.Poison).ToHtml(false)}]"
                + $"{(heal ? "＋" : "−")}{e.Amount}[/color] → {NameOf(e.TargetId)}");
        }
    }

    private async Task PlayTurnTicks(TurnTickRange range, int token)
    {
        var events = _result!.Events;
        _collectingTurnTicks = range;
        try
        {
            for (int i = range.Start; i < range.End; i++)
            {
                if (token != _playToken || !_battleMode) return;
                _eventIndex = i + 1;
                await ApplyEvent(events[i], i);
            }
        }
        finally { _collectingTurnTicks = null; _tickDelayBudget = null; _fireFastEvent = false; _misaFastEvent = false; }
        while (_paused && token == _playToken && _battleMode) await Delay(0.06, raw: true);
        if (token != _playToken || !_battleMode) return;
        // 敵味方とも同じフレームで反映する。人数分の待ちを積まず、最長の演出だけ待つ。
        foreach (var beat in range.Beats)
        {
            var pawn = _battleField.FindPawn(beat.TargetId);
            // 啜りなど同区間の副作用も含め、最後の写しを表示する。足し引きでHPを再計算しない。
            var hp = events.Skip(range.Start).Take(range.End - range.Start).LastOrDefault(e =>
                e.TargetId == beat.TargetId && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal);
            if (hp is not null) pawn?.SetHp(hp.HpAfter);
            if (beat.FireDamage && pawn?.PlankPieceCount > 0)
                _battleField.PlankImpact(pawn, 40, _speed, true);
            _battleField.ShowTurnTickBeat(pawn, beat, _speed);
        }
        _partyBar.Sync(_battleField, _shownOwner);
        await Delay(range.Beats.Max(beat => beat.Seconds));
    }

    private async Task<bool> PlayTickEvent(BattleEvent e, int index, BattlePawn3D? target)
    {
        if (_ticks.Starts.TryGetValue(index, out var cue))
        {
            bool inverse = e.SourceTrait == TraitId.Inverse;
            if (e.Text == "燃焼" && !inverse && target?.PlankPieceCount > 0)
                _battleField.PlankImpact(target, 40, _speed, true);
            if (e.Text == "燃焼")
            {
                // 体質やヒヨの生死を推定しない。この刻みの実際の帰結だけを読む。
                bool damage = false, healing = false;
                foreach (var outcome in _ticks.Outcomes)
                    if (outcome.Value.Start == index)
                    {
                        damage |= _result!.Events[outcome.Key].Kind == BattleEventKind.Damage
                            && _result.Events[outcome.Key].Amount > 0;
                        healing |= _result!.Events[outcome.Key].Kind == BattleEventKind.Heal;
                    }
                _battleField.ShowFireTick(target, healing, damage, cue.Seconds / System.Math.Max(0.1, _speed));
            }
            else _battleField.ShowTick(target, false, inverse,
                cue.Last && e.TickCount > 1, cue.Seconds / System.Math.Max(0.1, _speed));
            _tickPlays++;
            if (inverse) _inverseTickPlays++;
            AppendLog($"  [{e.Text}] → {NameOf(e.TargetId)}");
            await Delay(cue.Seconds * 0.55);
            return true;
        }
        if (!_ticks.Outcomes.TryGetValue(index, out cue)) return false;
        bool heal = e.Kind == BattleEventKind.Heal;
        target?.SetHp(e.HpAfter);
        // 色の反転は Status の絵の中で行う。通常の回復光を重ねて隠さない。
        var status = _result!.Events[cue.Start];
        _battleField.TickNumber(target, e.Amount, heal, cue.Ordinal, cue.Last && status.TickCount > 1,
            status.Text == "燃焼", brittle: e.BrittleExtra > 0);
        if (!heal && e.Amount > 0)
            _battleField.PlayStatusDamageSound(status.Text);
        AppendLog($"  [color=#{(heal ? UiKit.Heal : UiKit.Poison).ToHtml(false)}]"
            + $"{(heal ? "＋" : "−")}{e.Amount}[/color] → {NameOf(e.TargetId)}");
        await Delay(cue.Seconds * 0.45);
        return true;
    }
}
