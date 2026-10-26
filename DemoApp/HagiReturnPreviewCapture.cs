using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// 追加済みの返し刃を本編の Main から撮る。攻撃・HP・命中順を撮影側で作らない。
public partial class HagiReturnPreviewCapture : Control
{
    private const int Fps = 60;
    private readonly List<object> _segments = new();
    private readonly List<object> _observations = new();
    private string _directory = null!;
    private sealed record Shot(string Name, FxCue Cue, FxLivePlayback Playback, HagiPresentation Index,
        int Start, int End, KeyValuePair<int, HagiPresentation.Swing>[] Steps);

    public override async void _Ready()
    {
        try
        {
            _directory = ProjectSettings.GlobalizePath("res://../design/art/hagi/return-preview-v1");
            Directory.CreateDirectory(_directory);
            var groups = new List<Shot>();
            foreach (string id in FxCatalog.Registered.Where(c => c.Id.StartsWith("hagi.live.")).Select(c => c.Id))
            {
                var cue = FxCatalog.Find(id);
                var playback = FxLivePlayback.Create(cue);
                var index = HagiPresentation.Build(playback.Result.Events, playback.Openings, playback.Death?.Hagi);
                var steps = index.Swings.Concat(index.Returns).OrderBy(p => p.Key).ToArray();
                for (int i = 0; i < steps.Length; i++)
                {
                    int first = i;
                    while (i + 1 < steps.Length && steps[i].Value.Keep) i++;
                    var sequence = steps.Skip(first).Take(i - first + 1).ToArray();
                    int end = Math.Max(sequence[^1].Key, sequence.SelectMany(p => p.Value.Damage).DefaultIfEmpty(sequence[^1].Key).Max()) + 1;
                    groups.Add(new("", cue, playback, index, sequence[0].Key, end, sequence));
                }
            }
            Write("candidates.json", groups.Select(g => new { cue = g.Cue.Id, g.Cue.Title, g.Start, g.End,
                steps = g.Steps.Select(p => new { index = p.Key, p.Value.Reap, p.Value.Return, p.Value.Reverse,
                    p.Value.Encore, p.Value.Pursuit, p.Value.Keep, p.Value.Kind, p.Value.Damage,
                    hits = p.Value.Damage.Select(i => g.Playback.Result.Events[i]) }) }));
            if (OS.GetCmdlineUserArgs().Contains("--plan-only"))
            { GD.Print("HAGI_RETURN_PREVIEW_PLAN_OK"); GetTree().Quit(); return; }

            var after = groups.Where(g => g.Steps.Any(p => p.Value.Return) && !g.Steps[0].Value.Reap
                && !g.Steps.Any(p => p.Value.Reverse && !p.Value.Return))
                .OrderBy(g => g.Steps.Length).ThenBy(g => g.Steps.SelectMany(p => p.Value.Damage)
                    .Select(i => g.Playback.Result.Events[i].TargetId).Distinct().Count()).First()
                with { Name = "after-hit-return" };
            var twin = groups.First(g => g.Cue.Id == "hagi.live.4.0" && g.Steps.Any(p => p.Value.Reverse && !p.Value.Return))
                with { Name = "isolated-twin" };
            var hunt = groups.Where(g => g.Cue.Id == "hagi.live.3.2" && g.Steps.Any(p => p.Value.Return))
                .OrderByDescending(g => g.Steps.Count(p => p.Value.Encore > 0)).ThenByDescending(g => g.Steps.Length).First()
                with { Name = "carry-chain" };
            foreach (var shot in new[] { after, twin, hunt }) await Record(shot);
            Write("capture-segments.json", new { fps = Fps, speed = 1, renderer = "Godot GPU / Main.EnterViewerLive",
                segments = _segments, observations = _observations });
            GD.Print("HAGI_RETURN_PREVIEW_CAPTURE_OK actual-main-path speed=1 fps=60"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task Record(Shot shot)
    {
        var main = new Main { ViewerMode = true }; main.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(main);
        main.SetPlaybackSpeed(1); main.EnterViewerLive(shot.Playback, shot.Cue);
        await main.SeekTo(shot.Start);
        main.ViewerField.SetCameraZoom(1.3f);
        var pawn = main.ViewerField.FindPawn(shot.Steps[0].Value.Actor)!;
        int beforeSwings = main.ViewerField.HagiSwings, beforeReturns = main.ViewerField.HagiReturns,
            beforeReaps = main.ViewerField.HagiReaps;
        await Frames(8);
        int start = Engine.GetFramesDrawn(); await Frames(24);
        main.ResumePlayback();
        int frameLimit = Engine.GetFramesDrawn() + Fps * 45;
        int oldSwings = beforeSwings, oldReturns = beforeReturns;
        while (Engine.GetFramesDrawn() < frameLimit)
        {
            await Frames(1);
            if (main.ViewerPlaybackError is { } error) throw error;
            if (oldSwings != main.ViewerField.HagiSwings || oldReturns != main.ViewerField.HagiReturns)
            {
                oldSwings = main.ViewerField.HagiSwings; oldReturns = main.ViewerField.HagiReturns;
                _observations.Add(new { shot = shot.Name, frame = Engine.GetFramesDrawn(), eventIndex = main.EventIndex,
                    swings = oldSwings - beforeSwings, returns = oldReturns - beforeReturns,
                    clip = pawn.HagiClipName, cel = pawn.AttackClipFrame,
                    hp = main.ViewerField.Pawns.Values.Select(p => new { p.InstanceId, p.UnitId, p.Hp }).ToArray() });
            }
            if (main.EventIndex >= shot.End && !pawn.AttackClipPlaying && pawn.HagiClipName is null) break;
        }
        if (Engine.GetFramesDrawn() >= frameLimit) throw new Exception("撮影が完了しません: " + shot.Name);
        main.PausePlayback();
        await Frames(36);
        int end = Engine.GetFramesDrawn();
        int swings = main.ViewerField.HagiSwings - beforeSwings, returns = main.ViewerField.HagiReturns - beforeReturns;
        if (swings != shot.Steps.Count(p => !p.Value.Return) || returns != shot.Steps.Count(p => p.Value.Return))
            throw new Exception($"撮影した振りと台本が違います: {shot.Name} swings={swings} returns={returns}");
        _segments.Add(new { name = shot.Name, cue = shot.Cue.Id, shot.Cue.Title, startFrame = start, endFrame = end,
            eventStart = shot.Start, eventEnd = shot.End, swings, returns, reaps = main.ViewerField.HagiReaps - beforeReaps,
            steps = shot.Steps.Select(p => new { eventIndex = p.Key, p.Value.Return, p.Value.Reverse, p.Value.Reap, p.Value.Damage }) });
        GD.Print($"HAGI_RETURN_PREVIEW_SEGMENT {shot.Name} frames={start}..{end} swings={swings} returns={returns}");
        await main.StopViewerPlayback(); main.QueueFree(); await Frames(6);
    }
    private void Write(string name, object value) => File.WriteAllText(Path.Combine(_directory, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    private async Task Frames(int count)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
}
