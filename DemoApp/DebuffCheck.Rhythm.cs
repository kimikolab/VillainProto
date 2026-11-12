using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class DebuffCheck
{
    private void CheckRhythmReplay(BattleResult shown)
    {
        var plan = _main.PlaybackProjection!;
        var source = _main.SourceBattleResult!;
        Require(plan.OriginalIndices.OrderBy(i => i).SequenceEqual(Enumerable.Range(0, source.Events.Count)), "表示は元の通知の置換で、追加・欠落・重複がない");
        for (int i = 0; i < shown.Events.Count; i++)
        {
            static string Body(BattleEvent e)
            {
                var node = JsonSerializer.SerializeToNode(e)!.AsObject(); node.Remove(nameof(BattleEvent.HpAfter));
                if (e.Kind == BattleEventKind.Scar) node.Remove(nameof(BattleEvent.Slot));
                return node.ToJsonString();
            }
            Require(Body(shown.Events[i]) == Body(source.Events[plan.OriginalIndices[i]]), "表示順の変更で打点・対象・その他の通知を変えない");
        }
        foreach (var last in source.Events.Where(e => e.TargetId is not null && e.Kind is BattleEventKind.Damage
            or BattleEventKind.Heal or BattleEventKind.Revive or BattleEventKind.Death or BattleEventKind.Scar)
            .GroupBy(e => e.TargetId).Select(g => g.Last()))
            if (_main.ViewerField.FindPawn(last.TargetId) is { } pawn)
                Require(pawn.Hp == Math.Max(0, last.HpAfter), $"元の計算結果の最終HPと一致 {pawn.UnitId} {pawn.Hp}/{last.HpAfter}");
        foreach (var scar in source.Events.Where(e => e.Kind == BattleEventKind.Scar && e.TargetId is not null).GroupBy(e => e.TargetId).Select(g => g.Last()))
            if (_main.ViewerField.FindPawn(scar.TargetId) is { } pawn) Require(pawn.MaxHp == scar.Slot, "元の計算結果の最大HPと一致");
        Require(_main.MudoBurstPoseConflicts == 0, "連撃を終えて帰還してからムドを再生する");
        GD.Print($"DEBUFF_RHYTHM_REPLAY_OK deferred={plan.DeferredBursts} same-events=True source-hp=True no-frozen-pose=True");
    }

    private async Task CheckUpdatedBrief()
    {
        var readings = new DebuffReadout();
        var openings = Openings();
        openings[3] = openings[3] with { Attack = UnitCatalog.Mudo.Attack, Traits = UnitCatalog.Mudo.Traits };
        readings.Begin(openings);
        readings.Read(new() { Kind = BattleEventKind.StatusGain, Turn = 0, TargetId = 4, Text = "dull", Amount = 18 });
        Require(readings.MudPower(4) == 49, "攻0でも次の泥の1発49を読める");
        var field = _main.ViewerField; field.Visible = true; field.BeginBattle(openings, "刻印", 0); field.SetDeathClock(false, 1);
        var owner = field.FindPawn(1)!; var target = field.FindPawn(6)!;
        field.ShowKarzMark(owner, target);
        await Frame(); field.SetDeathClock(true, 1);
        var mark = Descendants(field).OfType<DebuffKarzMark3D>().Single(); double age = mark.Age;
        await Wait(.1); Require(mark.Age == age, "刻印の焼き付きも停止する");
        field.SetDeathClock(false, 2); await Wait(.19);
        Require(Descendants(field).OfType<DebuffKarzMark3D>().Count() == 1, "焼き付きの後は薄い印で残す");
        owner.AnimateDeath(); await Frame(); await Frame();
        Require(!Descendants(field).OfType<DebuffKarzMark3D>().Any(), "書き手が死ぬと刻印が消える");
        field.BeginBattle(openings, "鎖の並行", 0); field.SetDeathClock(false, 1);
        field.ShowCurseTransferParallel(field.FindPawn(6), new[] { field.FindPawn(7)! });
        await Frame(); field.SetDeathClock(true, 1);
        var flow = Descendants(field).OfType<HexShareFlow3D>().Single(); double flowAge = flow.Age;
        await Wait(.1); Require(flow.Age == flowAge, "待たない鎖も停止時計に従う");
        field.SetDeathClock(false, 2); await Wait(.2);
        Require(!Descendants(field).OfType<HexShareFlow3D>().Any(), "並行する鎖は短時間で片付く");
        field.Visible = false; await Frame();
        _main.SetPlaybackSpeed(1);
        _main.EnterScripted(openings, new BattleEvent[] {
            new() { Kind = BattleEventKind.Damage, Turn = 1, ActorId = 2, TargetId = 7, ShareFromId = 6, Amount = 1, HpAfter = 299 },
            new() { Kind = BattleEventKind.TurnStart, Turn = 2 },
        }, "鎖は連撃を待たせない");
        await Until(() => !_main.IsPlaying);
        Require(Descendants(_main.ViewerField).OfType<HexShareFlow3D>().Any(f => f.Age < .24),
            "筋がまだ飛行中でも後続の通知を処理し終わる");
        _main.SetPlaybackSpeed(1000);
        GD.Print("DEBUFF_UPDATED_BRIEF_OK mud49 mark-owner-death parallel-hex pause speed cleanup=True");
    }

    private async Task CaptureUpdatedBrief(BattlefieldView3D field, Func<string, Task> save)
    {
        var marked = Openings();
        marked[0] = marked[0] with { UnitId = "kari", Name = "祟りのカーズ", Traits = UnitCatalog.Kari.Traits };
        field.BeginBattle(marked, "", 0); field.SetDeathClock(false, 1);
        field.ShowKarzMark(field.FindPawn(1)!, field.FindPawn(6)!);
        await Until(() => Descendants(field).OfType<DebuffKarzMark3D>().Any(m => m.Age >= .10));
        field.SetDeathClock(true, 1); await save("karz-mark.png");
        field.Visible = false;
        _main = new Main { ViewerMode = true }; _main.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_main);
        _main.ViewerFastPlayback = true; _main.SetPlaybackSpeed(1000);
        var openings = Openings();
        openings[3] = openings[3] with { Attack = UnitCatalog.Mudo.Attack, Traits = UnitCatalog.Mudo.Traits };
        _main.EnterScripted(openings, new BattleEvent[] {
            new() { Kind = BattleEventKind.CursePass, Turn = 0, ActorId = 1, TargetId = 2, PartnerId = 3, Text = CursePassLabels.Feed, Amount = 12 },
            new() { Kind = BattleEventKind.StatusGain, Turn = 0, ActorId = 1, TargetId = 2, Text = "dull", Amount = 12 },
            new() { Kind = BattleEventKind.CursePass, Turn = 0, ActorId = 1, TargetId = 4, PartnerId = 3, Text = CursePassLabels.Feed, Amount = 18 },
            new() { Kind = BattleEventKind.StatusGain, Turn = 0, ActorId = 1, TargetId = 4, Text = "dull", Amount = 18 },
            new() { Kind = BattleEventKind.StatusGain, Turn = 0, ActorId = 2, TargetId = 2, Text = BattleContext.UtsuHitsText, Amount = 5 },
            new() { Kind = BattleEventKind.StatSnapshot, Turn = 0, TargetId = 4, Amount = 0, Pattern = AttackPattern.Single },
            new() { Kind = BattleEventKind.StatSnapshot, Turn = 0, TargetId = 2, Amount = 21, Pattern = AttackPattern.Single },
            new() { Kind = BattleEventKind.TurnStart, Turn = 1 },
        }, "実打点と発数の欄");
        await Until(() => !_main.IsPlaying);
        await _main.SeekTo(7);
        _main.ResumePlayback(); await Until(() => !_main.IsPlaying); await Wait(1.1);
        await save("party-readout.png");
        await _main.StopViewerPlayback();
    }
}
