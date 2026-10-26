using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// 実戦の攻撃列から動作だけを抜き出す録画用シーン。各振りは本編の時計と描画を使う。
public partial class HagiMovieCapture : Control
{
    private const int Fps = 60;
    private readonly List<object> _segments = new();
    public override async void _Ready()
    {
        try
        {
            var cue = FxCatalog.Find("hagi.live.0.2");
            var playback = FxLivePlayback.Create(cue);
            var index = HagiPresentation.Build(playback.Result.Events, playback.Openings, playback.Death?.Hagi);
            var swings = index.Swings.Concat(index.Returns).OrderBy(p => p.Key).ToArray();
            foreach (var swing in swings)
                GD.Print($"HAGI_MOVIE_CANDIDATE index={swing.Key} reap={swing.Value.Reap} pursuit={swing.Value.Pursuit} link={swing.Value.Link} keep={swing.Value.Keep} targets={string.Join(',',swing.Value.Damage.Select(i => playback.Result.Events[i].TargetId))}");
            var single = swings.First(p => p.Value.Reap && !p.Value.Pursuit);
            int chainAt = Array.FindIndex(swings, p => p.Value.Reap && p.Value.Keep);
            if (chainAt < 0) throw new Exception("実戦台本に連続する刈り取りがありません");
            int chainCount = 1;
            while (chainAt + chainCount < swings.Length && swings[chainAt + chainCount].Value.Link) chainCount++;
            if (OS.GetCmdlineUserArgs().Contains("--hagi-acrobatics"))
                await Record("sweep", playback, index, swings.First(p => !p.Value.Reap && !p.Value.Pursuit).Key, 1);
            await Record("reap", playback, index, single.Key, 1);
            await Record("chain", playback, index, swings[chainAt].Key, chainCount);
            string output = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--hagi-movie-dir=",StringComparison.Ordinal))
                ?["--hagi-movie-dir=".Length..] ?? "res://../design/art/hagi/reaper-effects-v1/video";
            string dir = ProjectSettings.GlobalizePath(output);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"capture-segments.json"), JsonSerializer.Serialize(new { fps = Fps, cue = cue.Id, speed = 1, segments = _segments }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("HAGI_MOVIE_CAPTURE_OK actual-attack-sequence isolated-view speed=1 fps=60"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private async Task Record(string name, FxLivePlayback playback, HagiPresentation index, int attack, int count)
    {
        var steps = index.Swings.Concat(index.Returns).Where(p => p.Key >= attack).OrderBy(p => p.Key).Take(count).ToArray();
        int actorId = steps[0].Value.Actor;
        var ids = steps.SelectMany(p => p.Value.Damage).Select(i => playback.Result.Events[i].TargetId!.Value).Append(actorId).ToHashSet();
        int HpBefore(int id)
        {
            var last = playback.Result.Events.Take(attack).LastOrDefault(e => e.TargetId == id && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Revive);
            return last?.HpAfter ?? playback.Openings.Single(o => o.InstanceId == id).Hp;
        }
        var openings = playback.Openings.Where(o => ids.Contains(o.InstanceId)).Select(o => o with { Hp = HpBefore(o.InstanceId) }).ToArray();
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        field.BeginBattle(openings,"ハギの動作確認",0); field.SetCameraZoom(1.3f); field.SetDeathClock(false,1);
        void ApplySnapshot(int sourceIndex)
        {
            foreach (var state in index.Frames.Where(p => p.Key <= sourceIndex).SelectMany(p => p.Value)
                .Where(s => ids.Contains(s.Id)).GroupBy(s => s.Id).Select(g => g.Last())) field.ApplyHagiState(state);
        }
        ApplySnapshot(attack); await Frames(12);
        int start = Engine.GetFramesDrawn();
        await Frames(36);
        for (int s=0;s<steps.Length;s++)
        {
            var step = steps[s]; ApplySnapshot(step.Key);
            var targets = step.Value.Damage.Select(i => field.FindPawn(playback.Result.Events[i].TargetId)).OfType<BattlePawn3D>().Distinct().ToArray();
            await field.PlayHagiSwing(field.FindPawn(actorId),targets,step.Value,() => {
                foreach (int hit in step.Value.Damage)
                {
                    var damage = playback.Result.Events[hit]; var pawn = field.FindPawn(damage.TargetId)!;
                    pawn.SetHp(damage.HpAfter); field.ShowHagiDamage(pawn,damage.Amount,index.Reaps.Contains(hit),false);
                    if (damage.HpAfter == 0) field.PlayDeath(pawn);
                }
            });
            if (s+1 < steps.Length) { ApplySnapshot(steps[s+1].Key); await Frames(8); }
        }
        if (field.HagiSwings + field.HagiReturns != count) throw new Exception("録画した刃の件数が実台本と違います");
        await Frames(66);
        int end = Engine.GetFramesDrawn();
        _segments.Add(new { name, startFrame = start, endFrame = end, attackEvent = attack, attacks = count, reaps = field.HagiReaps });
        GD.Print($"HAGI_MOVIE_SEGMENT name={name} start={start} end={end} attacks={count}");
        field.BeginBattle(Array.Empty<DemoOpening>(),"",0); field.QueueFree(); await Frames(6);
    }
    private async Task Frames(int count)
    {
        for (int i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }
}
