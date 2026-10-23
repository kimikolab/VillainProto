using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class FxViewer
{
    // 第2段の目視確認。基本動作・実戦の見せ場と、敵2種の表示を通常の入口で撮る。
    private async Task CaptureGenerated()
    {
        _loop.SetPressedNoSignal(false);
        await LoadCue(FxCatalog.Find("unit.kata.idle"));
        await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
        await Capture("catalog.all");
        var ids = new List<string>();
        foreach (string id in new[] { "kata", "basa", "golm" })
        { ids.Add($"unit.{id}.attack"); ids.Add($"unit.{id}.live.0"); }
        foreach (string id in new[] { EnemyCatalog.Knight.Id, EnemyCatalog.Axeman.Id })
        foreach (string action in new[] { "idle", "attack", "hit", "death" }) ids.Add($"unit.{id}.{action}");
        foreach (string id in ids)
        {
            var cue = FxCatalog.Find(id);
            _unit.Selected = _unitIds.IndexOf(cue.Subject!.Definition.Id) + 1;
            _collapsedGroups["@" + cue.Subject.Key] = false;
            Filter();
            var row = _cueItems[cue.Id];
            await LoadCue(cue);
            Require(ReferenceEquals(row, _cueItems[cue.Id]), "再生を選んでも一覧を作り直さない");
            if (cue.Kind == FxCueKind.Live)
            {
                Require(_highlights[cue.Id].Length > 0, $"撮影する実戦に見せ場がある {id}");
                await _main.SeekTo(_highlights[cue.Id][0]);
                _main.ResumePlayback();
            }
            await ToSignal(GetTree().CreateTimer(cue.Id.EndsWith(".idle") ? .8 : 1.6), SceneTreeTimer.SignalName.Timeout);
            _main.PausePlayback();
            await Capture(cue.Id);
        }
        GD.Print($"FX_VIEWER_GENERATED_CAPTURE_OK cues={ids.Count} enemies=2");
        await QuitViewer();
    }
}
