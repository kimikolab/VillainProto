using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 規定ムドの実台本と、攻撃力0のボルグで停止した近衛戦をMainの入口から回す。
public partial class MudoCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private Main _main = null!;
    private T Read<T>(string name) => (T)typeof(Main).GetField(name, Flags)!.GetValue(_main)!;
    private void Call(string name, params object[] args) => typeof(Main).GetMethod(name, Flags)!.Invoke(_main, args);

    public override async void _Ready()
    {
        try
        {
            string? capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--mudo-capture="))?["--mudo-capture=".Length..];
            if (capture is not null)
            {
                await CapturePreview(capture);
                GetTree().Quit();
                return;
            }
            _main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
            AddChild(_main);
            typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(_main, true);
            typeof(Main).GetField("_speed", Flags)!.SetValue(_main, 1000.0);
            Read<SpinBox>("_seed").Value = 0;
            int playBase = EnemyCatalog.Stages.Count + 1 + EnemyCatalog.TestStages.Count + 1;
            Read<OptionButton>("_stagePicker").Select(playBase + 1);
            Call("StartBattle");
            await Finished();
            Require(Read<BattleResult>("_result").Events.Any(e => e.Kind == BattleEventKind.Attack
                && e.Turn == 14 && e.Amount == 0), "停止した攻撃力0の手番を通る");
            GD.Print("MUDO_BORG_ZERO_ATTACK_OK");
            Call("ReturnToFormation");
            var rows = Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).Concat(Presets.Versions).ToArray();
            int curse = Array.FindIndex(rows, r => r.Name == "試遊・呪い");
            Require(curse >= 0, "規定の呪い編成");
            Call("ApplyPreset", curse);
            foreach (int stage in new[] { playBase, 3 })
            {
                Read<OptionButton>("_stagePicker").Select(stage);
                Call("StartBattle");
                await Finished();
                CheckReplay();
                var result = Read<BattleResult>("_result");
                Call("ReplayBattle");
                await Finished();
                Require(ReferenceEquals(result, Read<BattleResult>("_result")), "再戦の台本を維持");
                CheckReplay();
                GD.Print($"MUDO_REPLAY_OK stage={stage} events={result.Events.Count} replay=True");
                Call("ReturnToFormation");
            }
            await CheckPulse();
            GD.Print("MUDO_CHECK_COMPLETE ok=True");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void CheckReplay()
    {
        var result = Read<BattleResult>("_result");
        var field = Read<BattlefieldView3D>("_battleField");
        var opening = Read<List<DemoOpening>>("_battleOpening").ToDictionary(o => o.InstanceId);
        Require(Read<int>("_eventIndex") == result.Events.Count, "全通知を消費");
        bool Mudo(int? id) => id is int value && opening.TryGetValue(value, out var o) && o.UnitId == "mudo";
        int bursts = result.Events.Count(e => e.Kind == BattleEventKind.Highlight && Mudo(e.ActorId));
        int dulls = result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.Text == "dull"
            && e.Amount == 1 && Mudo(e.ActorId) && e.TargetId is int target && opening[target].Team != opening[e.ActorId!.Value].Team);
        Require(bursts > 0 && dulls > 0 && field.MudoBursts == bursts && field.MudoDulls == dulls, "暴発と弱体の表示件数一致");
        foreach (var pawn in field.Pawns.Values)
        {
            var hp = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId
                && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Revive);
            if (hp is not null) Require(pawn.Hp == hp.HpAfter, "最終HP一致");
        }
        GD.Print($"MUDO_EVENTS_OK bursts={bursts} dulls={dulls} hp=True");
    }

    private async Task CheckPulse()
    {
        var field = Read<BattlefieldView3D>("_battleField");
        var openings = new DemoOpening[] {
            new(1, 0, "mudo", "ムド", 0, 100, 100, 10, AttackPattern.Single, true),
            new(2, 1, "knight", "標的", 0, 100, 100, 10, AttackPattern.Single, true),
        };
        field.BeginBattle(openings, "泥の演出確認", 0);
        field.Visible = true;
        var pawn = field.FindPawn(1)!;
        _main.SetPlaybackSpeed(1);
        field.ShowMudoBurst(pawn);
        var pulse = Descendants(field).OfType<MudoMudPulse3D>().Single();
        _main.PausePlayback();
        double frozen = pulse.Elapsed;
        await Wait(0.1);
        Require(pulse.Elapsed == frozen, "停止中は泥も停止");
        _main.SetPlaybackSpeed(2);
        field.SetMudoClock(false, 2);
        await Wait(0.08);
        Require(pulse.Elapsed >= 0.12 && pulse.Elapsed < 0.36, "2倍速で進む");
        field.ShowMudoDull(field.FindPawn(2)!);
        Require(Descendants(field).OfType<PopupLabel2D>().Any(l => l.Text == "−1" && l.Modulate == HexMudFx.Tint), "泥色の小さな−1");
        field.BeginBattle(openings, "再戦", 0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!Descendants(field).OfType<MudoMudPulse3D>().Any()
            && !Descendants(field).OfType<PopupLabel2D>().Any(), "再戦で泥と数字を片付ける");
        field.ShowMudoBurst(field.FindPawn(1)!);
        field.FindPawn(1)!.AnimateDeath();
        await Wait(0.05);
        Require(!Descendants(field).OfType<MudoMudPulse3D>().Any(), "死亡後に泥を残さない");
        field.FindPawn(1)!.SetHp(100);
        field.FindPawn(1)!.AnimateRevive();
        field.ShowMudoBurst(field.FindPawn(1)!);
        field.ResetMudoPresentation();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!Descendants(field).OfType<MudoMudPulse3D>().Any(), "中断時に泥を片付ける");
        GD.Print("MUDO_PULSE_OK pause speed death replay cancel label=True");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private async Task CapturePreview(string path)
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        field.BeginBattle(new DemoOpening[] {
            new(1, 0, "mudo", "泥人形ムド", 2, 100, 100, 10, AttackPattern.Single, true),
            new(2, 1, "knight", "近衛騎士", 2, 100, 100, 10, AttackPattern.Single, true),
        }, "泥が揺れる ／ 得物が鈍る −1", 0);
        await Wait(0.3);
        field.SetMudoClock(true, 1);
        field.ShowMudoBurst(field.FindPawn(1)!);
        field.ShowMudoDull(field.FindPawn(2)!);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"MUDO_PREVIEW_SAVED {path}");
    }

    private async Task Finished()
    {
        for (int i = 0; i < 800; i++)
        {
            if (!Read<bool>("_playing")) return;
            await ToSignal(GetTree().CreateTimer(0.025), SceneTreeTimer.SignalName.Timeout);
        }
        var task = Read<Task>("_playbackTask");
        if (task.Exception is { } error) throw error;
        int index = Read<int>("_eventIndex") - 1;
        var e = Read<BattleResult>("_result").Events[index];
        var pawn = Read<BattlefieldView3D>("_battleField").FindPawn(e.ActorId);
        var motion = pawn is null ? null : (Tween?)typeof(BattlePawn3D).GetField("_motion", Flags)!.GetValue(pawn);
        throw new InvalidOperationException($"再生停止 index={index} T{e.Turn} {e.Kind} amount={e.Amount} actor={pawn?.UnitId}"
            + $" clip={pawn?.AttackClipPlaying}/{pawn?.AttackClipFrame} motion={motion?.IsValid()}/{(motion?.IsValid() == true ? motion.IsRunning() : false)}");
    }
}
