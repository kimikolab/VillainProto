using BattleCore;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private int _liliRiteStart = -1, _liliRiteEnd = -1;
    private bool _liliRiteReleased, _liliRiteFinish;
    private int? _liliRiteActor;
    private BattlePawn3D[] _liliRiteAllies = [];
    private BattlePawn3D[] _liliRiteEnemies = [];
    private bool InLiliRite(int index) => _liliRiteStart >= 0 && index > _liliRiteStart && index <= _liliRiteEnd;
    private void ResetLiliRitePlayback()
    {
        _liliRiteStart = _liliRiteEnd = -1;
        _liliRiteActor = null;
        _liliRiteReleased = _liliRiteFinish = false;
        _liliRiteAllies = [];
        _liliRiteEnemies = [];
    }

    private async Task<bool> PlayLiliRite(BattleEvent e, int index, BattlePawn3D? actor)
    {
        var events = _result!.Events;
        int token = _playToken;
        if (e.Kind == BattleEventKind.Kiss && e.Text == KissLabels.Rite && actor is not null)
        {
            int end = LiliRiteSpan.End(events, index);
            if (end < 0) return false;
            _liliRiteStart = index; _liliRiteEnd = end; _liliRiteActor = e.ActorId;
            _liliRiteReleased = false;
            // 既存の終戦音と同じ台本上の退場点を使う。敵HPから勝敗を再計算しない。
            _liliRiteFinish = _finishSoundIndex > index && _finishSoundIndex < end;
            var segment = events.Skip(index + 1).Take(end - index - 1).ToArray();
            var enemies = segment.Where(x => x.Kind == BattleEventKind.Kiss && x.Text == KissLabels.RiteDrain && x.ActorId == e.ActorId)
                .Select(x => _battleField.FindPawn(x.TargetId)).OfType<BattlePawn3D>().Distinct().ToArray();
            _liliRiteEnemies = enemies;
            _liliRiteAllies = segment.Where(x => x.Kind == BattleEventKind.Kiss && x.Text == KissLabels.RiteGive && x.ActorId == e.ActorId)
                .Select(x => _battleField.FindPawn(x.TargetId)).OfType<BattlePawn3D>().Distinct().ToArray();
            _battleField.BeginLiliRite(actor, enemies, _speed, _liliRiteFinish, LiliRiteWeight.Widths(e, segment));
            await Delay(0.22);
            if (token != _playToken || !_battleMode) return true;
            _battleField.StrikeLiliRite(enemies, _liliRiteFinish);
            // 柱の立ち上がりの直後に台本のDamageとDeathを通す。
            await Delay(0.12);
            return true;
        }
        if (!InLiliRite(index)) return false;
        // 儀式の見出しは光と鐘に置き換える。他の駒の割り込みは従来通り再生する。
        if (e.Kind == BattleEventKind.Highlight && index == _liliRiteStart + 1 && e.ActorId == _liliRiteActor)
        {
            AppendLog(e.Text ?? "");
            return true;
        }
        if (e.Kind != BattleEventKind.Kiss || e.ActorId != _liliRiteActor) return false;
        if (e.Text == KissLabels.RiteDrain) { _liliDrains++; return true; }
        if (e.Text == KissLabels.RiteGive)
        {
            _liliGifts++;
            if (!_liliRiteReleased)
            {
                _liliRiteReleased = true;
                await GatherAndReleaseLiliRite(actor);
            }
            return true;
        }
        if (e.Text == KissLabels.Return)
        {
            // 死亡時の還りは収束中の光へ合流する。HPの更新は直後のHealが担う。
            _liliGifts++;
            if (actor is not null && _battleField.FindPawn(e.TargetId) is { } recipient)
                _battleField.LiliFlow(actor.ChalicePoint, recipient.FxPoint, 0.16 / _speed);
            return true;
        }
        if (e.Text != KissLabels.RiteEnd) return false;
        if (!_liliRiteReleased)
            await GatherAndReleaseLiliRite(actor);
        if (token != _playToken || !_battleMode) return true;
        if (actor is not null)
            foreach (var pawn in _battleField.Pawns.Values.Where(p => p.Team != actor.Team))
            {
                pawn.SetStigma(false);
                SetDisplayedStatus(pawn, DisplayStatusKey(StatusKeys.Stigma), 0);
            }
        double tail = _liliRiteFinish ? 0.85 : 0.48;
        _battleField.EndLiliRite(tail / _speed);
        await Delay(tail);
        if (token != _playToken || !_battleMode) return true;
        _battleField.ResetLiliRite();
        ResetLiliRitePlayback();
        return true;
    }

    private async Task GatherAndReleaseLiliRite(BattlePawn3D? actor)
    {
        int token = _playToken;
        // HPと死亡を先に反映し、敵がいた位置の光を杯へ回収する。
        _battleField.GatherLiliRite(_liliRiteEnemies);
        await Delay(0.32);
        if (token != _playToken || !_battleMode) return;
        await Delay(_liliRiteFinish ? 0.18 : 0.06);
        if (token != _playToken || !_battleMode) return;
        _battleField.ReleaseLiliRite(_liliRiteAllies.Length > 0 ? _liliRiteAllies : actor is null ? [] : [actor], _liliRiteFinish);
        await Delay(0.16);
    }
}

internal static class LiliRiteSpan
{
    // 別の手番・儀式まで読まない。不完全な台本でも暗転を残さない。
    internal static int End(System.Collections.Generic.IReadOnlyList<BattleEvent> events, int start)
    {
        var first = events[start];
        for (int i = start + 1; i < events.Count; i++)
        {
            var next = events[i];
            if (next.Kind == BattleEventKind.TurnStart) break;
            if (next.Kind != BattleEventKind.Kiss || next.ActorId != first.ActorId) continue;
            if (next.Text == KissLabels.RiteEnd) return i;
            if (next.Text is KissLabels.Rite or KissLabels.Drain) break;
        }
        return -1;
    }
}
