using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private sealed record SekkiWrapCue(int Event, int Beat, int Actor);
    private readonly Dictionary<int, SekkiWrapCue> _sekkiWrapEvents = new();
    private readonly Dictionary<int, List<SekkiWrapCue>> _sekkiWrapDamage = new();
    private readonly HashSet<(int Beat, int Actor)> _sekkiWrapShown = new();
    private readonly Dictionary<BattlePawn3D, int> _sekkiWrapActive = new();
    private int _sekkiWrapSerial, _sekkiWrapEventCount, _sekkiWrapPlays;

    private void ResetSekkiPlayback(IReadOnlyList<BattleEvent> events)
    {
        EndSekkiWraps();
        _sekkiWrapEvents.Clear();
        _sekkiWrapDamage.Clear();
        _sekkiWrapShown.Clear();
        _sekkiWrapEventCount = _sekkiWrapPlays = 0;
        int beat = -1;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            // 同じ範囲の一振りで味方ごとに出る包むを、保持者ごとに1回へ畳む。
            if (e.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart
                || e.Kind == BattleEventKind.Slime && e.Text == SlimeLabels.Burst) beat = i;
            if (e.Kind != BattleEventKind.Slime || e.Text != SlimeLabels.Wrap || e.ActorId is not int actor) continue;
            var cue = new SekkiWrapCue(i, beat, actor);
            _sekkiWrapEvents.Add(i, cue);
            // 元の出来事は動かさず、同時着弾で先に描かれる本来の被弾へ絵だけを寄せる。
            for (int j = i + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart
                    || next.Kind == BattleEventKind.Slime && next.Text == SlimeLabels.Wrap) break;
                if (next.Kind != BattleEventKind.Damage || next.Relayed || next.TargetId != e.TargetId) continue;
                if (!_sekkiWrapDamage.TryGetValue(j, out var cues)) _sekkiWrapDamage[j] = cues = new();
                cues.Add(cue);
                break;
            }
        }
    }

    private void ObserveSekkiEvent(BattleEvent e, int index)
    {
        if (e.Kind != BattleEventKind.Slime || e.Text != SlimeLabels.Wrap) return;
        _sekkiWrapEventCount++;
        if (_sekkiWrapEvents.TryGetValue(index, out var cue)) ShowSekkiWrap(cue);
    }

    private void ShowSekkiWrapDamage(int index)
    {
        if (!_sekkiWrapDamage.TryGetValue(index, out var cues)) return;
        foreach (var cue in cues)
            if (CanPreviewViewerEvent(cue.Event)) ShowSekkiWrap(cue);
    }

    private void ShowSekkiWrap(SekkiWrapCue cue)
    {
        if (!_sekkiWrapShown.Add((cue.Beat, cue.Actor))) return;
        var pawn = _battleField.FindPawn(cue.Actor);
        if (pawn is null) return;
        pawn.SetSekkiWrapping(true);
        if (!pawn.SekkiWrapping) return;
        _sekkiWrapPlays++;
        int serial = ++_sekkiWrapSerial;
        _sekkiWrapActive[pawn] = serial;
        _ = HoldSekkiWrap(pawn, serial, _playToken);
    }

    // 実時間のタイマーではなく再生の時計で0.5秒。停止・途中の速度変更に追従する。
    private async Task HoldSekkiWrap(BattlePawn3D pawn, int serial, int token)
    {
        double elapsed = 0;
        try
        {
            while (elapsed < 0.50 && token == _playToken && _battleMode && _playing
                && IsInstanceValid(pawn) && pawn.IsInsideTree() && pawn.SekkiWrapping)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!_paused) elapsed += GetProcessDeltaTime() * _speed;
            }
        }
        finally
        {
            // 古い戦闘の復帰予約で、新しい戦闘や次の一振りの差分を消さない。
            if (_sekkiWrapActive.TryGetValue(pawn, out int active) && active == serial)
            {
                _sekkiWrapActive.Remove(pawn);
                if (IsInstanceValid(pawn)) pawn.SetSekkiWrapping(false);
            }
        }
    }

    private void EndSekkiWraps()
    {
        foreach (var pawn in _sekkiWrapActive.Keys.ToArray())
            if (IsInstanceValid(pawn)) pawn.SetSekkiWrapping(false);
        _sekkiWrapActive.Clear();
    }

    private void FinishSekkiPlayback()
    {
        EndSekkiWraps();
        if (_quitAfterPlayback)
            GD.Print($"SEKKI_WRAP_COMPLETE script={_sekkiWrapEvents.Count} events={_sekkiWrapEventCount}"
                + $" beats={_sekkiWrapEvents.Values.Select(c => (c.Beat, c.Actor)).Distinct().Count()} plays={_sekkiWrapPlays} active={_sekkiWrapActive.Count}");
    }
}
