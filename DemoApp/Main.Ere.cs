using Godot;
using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private async Task<bool> PlayEreGift(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (e.Kind == BattleEventKind.TurnStart) _battleField.ClearDirgeAuras();
        if (e.Kind != BattleEventKind.Dirge) return false;
        if (actor is null || target is null || !target.PresentationAlive) return true;
        bool begins = e.Text == DirgeLabels.Act;
        if (!begins && e.Text != DirgeLabels.Gift) return true;
        if (begins) actor.SetDirgeSinging(false);
        int token = _playToken;
        _battleField.SetGatherClock(_paused, _speed);
        var fx = _battleField.ShowDirgeGift(actor, target, begins,
            keepSinging: !begins && _result is not null && ErePresentation.HasFollowingGift(_result.Events, index, e.ActorId));
        AppendLog($"  [color=#{(actor.Team == target.Team ? EreGiftArt.Ally : EreGiftArt.Enemy).ToHtml(false)}]"
            + $"{actor.UnitName} の哀歌 → {target.UnitName}：{(begins ? "追加手番 開始" : "手番を託す")}[/color]");
        if (_fastSmoke) fx.Cancel();
        else await fx.Impact;
        if (token != _playToken || !_battleMode) fx.Cancel();
        return true;
    }

    // 連番があれば1フレーズを見せる。停止中は進めず、途中の速度変更にも追従する。
    private async Task HoldDirgeSong(int token, BattlePawn3D singer)
    {
        if (_fastSmoke) return;
        double seconds = singer.DirgeSongSeconds;
        _battleField.SetGatherClock(_paused, _speed);
        var performance = _battleField.ShowDirgePerformance(singer, (float)seconds);
        double elapsed = 0;
        while (elapsed < seconds && token == _playToken && _battleMode && singer.PresentationAlive)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_paused) elapsed += GetProcessDeltaTime() * _speed;
        }
        if (token != _playToken || !_battleMode || !singer.PresentationAlive) performance.Cancel();
    }
}
