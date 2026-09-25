using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 表示の状態と実台本の再生を検査する。戦闘側の値や規則は変えない。
public partial class LiliCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--verify")) await Replay();
            else await Preview();
            GD.Print("LILI_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private async Task Replay()
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        object? Read(string name) => typeof(Main).GetField(name, Flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        var players = BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Gald,
            front3: UnitCatalog.Golm, center: UnitCatalog.Sero, back1: UnitCatalog.Lili, back3: UnitCatalog.Dolga), 0);
        var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[3].Enemy, 1);
        foreach (var enemy in enemies)
        {
            enemy.SetCounter(StatusKeys.Poison, 3);
            enemy.SetCounter(StatusKeys.Burn, 3);
            enemy.SetCounter(StatusKeys.Curse, 2);
        }
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] { players, enemies, 0, 3, "" });
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < 1200 && (bool)Read("_playing")!; k++) await Wait(0.025);
            Require(!(bool)Read("_playing")!, "再生が完走する");
            var result = (BattleResult)Read("_result")!;
            var field = (BattlefieldView3D)Read("_battleField")!;
            int transfers = result.Events.Count(e => e.Kind == BattleEventKind.StatusTransfer);
            Require(transfers > 0, "状態移動の陽性対照");
            Require((int)Read("_liliTransfers")! == transfers, "状態を漏れなく一度ずつ移す");
            Require((int)Read("_liliDrains")! == result.Events.Count(e => e.Kind == BattleEventKind.Kiss
                && e.Text is KissLabels.Drain or KissLabels.RiteDrain), "吸い取りが台本と同数");
            Require((int)Read("_liliGifts")! == result.Events.Count(e => e.Kind == BattleEventKind.Kiss
                && e.Text is KissLabels.Give or KissLabels.RiteGive or KissLabels.Return), "施しが台本と同数");
            foreach (var group in result.Events.Where(e => e.TargetId is not null
                && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal).GroupBy(e => e.TargetId))
                Require(field.FindPawn(group.Key)!.Hp == Math.Max(0, group.Last().HpAfter), "最終HPが台本と一致");
            GD.Print($"LILI_REPLAY_OK pass={pass} transfers={transfers} drains={Read("_liliDrains")} gifts={Read("_liliGifts")}");
            GD.Print($"LILI_CASES rite={result.Events.Count(e => e.Kind == BattleEventKind.Kiss && e.Text == KissLabels.Rite)} "
                + $"return={result.Events.Count(e => e.Kind == BattleEventKind.Kiss && e.Text == KissLabels.Return)} "
                + $"self={result.Events.Count(e => e.Kind == BattleEventKind.Kiss && e.Text == KissLabels.Give && e.ActorId == e.TargetId)} "
                + $"overflow={result.Events.Count(e => e.Kind == BattleEventKind.Kiss && e.Text == KissLabels.Armor)}");
            if (pass == 0) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
        }
        // 実際に発行された複数状態の組を、Main の入口から一拍ずつ検査する。
        var tape = (BattleResult)Read("_result")!;
        var replayField = (BattlefieldView3D)Read("_battleField")!;
        replayField.BeginBattle((System.Collections.Generic.List<DemoOpening>)Read("_battleOpening")!, "", 3);
        ((System.Collections.Generic.HashSet<int>)Read("_specialShown")!).Clear();
        int first = Enumerable.Range(0, tape.Events.Count).First(i => tape.Events[i].Kind == BattleEventKind.StatusTransfer);
        var start = tape.Events[first];
        var batch = tape.Events.Skip(first).TakeWhile(e => e.Kind == BattleEventKind.StatusTransfer
            && e.ActorId == start.ActorId && e.TargetId == start.TargetId && e.SpreadFromId == start.SpreadFromId).ToArray();
        Require(batch.Length >= 2, "複数状態を同じ一拍で運ぶ陽性対照");
        var source = replayField.FindPawn(start.SpreadFromId)!;
        var recipient = replayField.FindPawn(start.TargetId)!;
        foreach (var transfer in batch) source.ApplyTransferredStatus(transfer.Text!, transfer.Amount);
        await (Task<bool>)typeof(Main).GetMethod("PlayLili", Flags)!.Invoke(main,
            new object?[] { start, first, replayField.FindPawn(start.ActorId), recipient })!;
        foreach (var transfer in batch)
        {
            Require(!source.HasStatusIcon(transfer.Text!) && recipient.HasStatusIcon(transfer.Text!), "再生の一拍で出入りが揃う");
            if (transfer.Text == StatusKeys.Poison)
                Require(recipient.PoisonIconAmount == transfer.StatusRemaining, "台本の移動後の毒残量に一致");
        }
        main.QueueFree();
        await Wait(0.2);
        await Preview(verify: true);
    }

    private async Task Preview(bool verify = false)
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            field.BeginBattle(new DemoOpening[] {
                new(1, team, "lili", "施しのリリ", 3, 78, 78, 3, AttackPattern.Single, false),
                new(2, team, "hota", "ホタ", 0, 30, 100, 10, AttackPattern.Single, false),
                new(3, 1-team, EnemyCatalog.Knight.Id, "敵", 0, 100, 100, 10, AttackPattern.Single, true),
                new(4, 1-team, EnemyCatalog.Knight.Id, "敵", 4, 100, 100, 10, AttackPattern.Single, true),
            }, "", 0);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            var lili = field.FindPawn(1)!; var receiver = field.FindPawn(2)!; var enemy = field.FindPawn(3)!;
            string[] keys = [StatusKeys.Poison, StatusKeys.Burn, StatusKeys.Curse, StatusKeys.Wound];
            foreach (string key in keys) enemy.ApplyTransferredStatus(key, 3);
            enemy.SetStigma(true);
            Require(enemy.HasStigma && enemy.HasStatusIcon(StatusKeys.Stigma), "聖痕の印とアイコン");
            enemy.BeginStatusSnapshot();
            enemy.ReadStatusSnapshot("聖", 1);
            foreach (string key in keys) enemy.ReadStatusSnapshot(StatusKeys.LabelOf(key), 3);
            enemy.CommitStatusSnapshot();
            Require(enemy.HasStigma, "ターン頭でも聖痕が残る");
            if (!verify) await Wait(0.5);
            lili.ReachWithChalice();
            field.LiliFlow(enemy.FxPoint, lili.ChalicePoint, 0.26 / speed);
            for (int k = 0; k < keys.Length; k++) field.LiliIcon(enemy.FxPoint, lili.ChalicePoint, keys[k], k, 0.26 / speed);
            if (!verify) { await Wait(0.13 / speed); await Capture($"{team}-{speed}-drain"); await Wait(0.13 / speed); }
            field.LiliFlow(lili.ChalicePoint, receiver.FxPoint, 0.32 / speed);
            for (int k = 0; k < keys.Length; k++) field.LiliIcon(lili.ChalicePoint, receiver.FxPoint, keys[k], k, 0.32 / speed);
            if (!verify) { await Wait(0.16 / speed); await Capture($"{team}-{speed}-transfer"); await Wait(0.16 / speed); }
            foreach (string key in keys) { enemy.ApplyTransferredStatus(key, 0); receiver.ApplyTransferredStatus(key, 5); }
            Require(keys.All(k => !enemy.HasStatusIcon(k) && receiver.HasStatusIcon(k)), "元は消え受け手に貼り付く");
            Require(enemy.PoisonIconAmount == 0 && receiver.PoisonIconAmount == 5, "受け手の既存量へ二重加算しない");
            Require(!enemy.HasCurseStain && receiver.HasCurseStain, "呪いの染みも移る");
            field.LiliOverflow(receiver, speed);
            if (!verify) { await Wait(0.08 / speed); await Capture($"{team}-{speed}-arrival"); await Wait(0.6 / speed); }
            field.LiliFlow(lili.ChalicePoint, lili.FxPoint, 0.32 / speed);
            if (!verify) { await Wait(0.16 / speed); await Capture($"{team}-{speed}-self"); await Wait(0.5); }
            enemy.SetStigma(false);
            Require(!enemy.HasStigma && !enemy.HasStatusIcon(StatusKeys.Stigma), "儀式後の消去");
            enemy.SetStigma(true);
            enemy.AnimateDeath();
            Require(!enemy.HasStigma, "死亡後に印を残さない");
            await Wait(verify ? 0.01 : 0.4);
        }
    }
    private async Task Capture(string name)
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(arg[14..] + "/" + name + ".png") == Error.Ok, "画像保存");
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
