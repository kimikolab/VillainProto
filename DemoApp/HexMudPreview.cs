using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 本番と同じ表示口で、2倍速の間合い・しみの寿命・共有の区切りを確認する独立シーン。
public partial class HexMudPreview : Control
{
    public override async void _Ready()
    {
        try
        {
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            field.BeginBattle(new DemoOpening[]
            {
                new(1, 0, "mudo", "ムド", 0, 100, 100, 10, AttackPattern.Single, true),
                new(2, 1, "borg", "ボルグ", 0, 100, 100, 10, AttackPattern.Single, true),
                new(3, 1, "gald", "ガルド", 1, 100, 100, 10, AttackPattern.Single, true),
                new(4, 1, "utsu", "ウツ", 3, 100, 100, 10, AttackPattern.Single, true),
            }, "演出素材確認", 0);
            await Wait(0.5);
            var source = field.FindPawn(1)!;
            var victims = new[] { field.FindPawn(2)!, field.FindPawn(3)!, field.FindPawn(4)! };
            var root = source.GetParent<Node3D>();
            foreach (var victim in victims)
            {
                await field.ShowCurseApplication(source, victim, 2);
                victim.SetStatusIcon(StatusKeys.Curse, true);
                Require(victim.HasCurseStain && victim.HasStatusIcon(StatusKeys.Curse), "付与直後のしみと印");
            }
            await Capture("stains");
            for (int i = 0; i < 5; i++)
            {
                victims[0].AnimateHit();
                await Wait(0.08);
                var transfer = field.ShowCurseTransfer(victims[0], victims.Skip(1).ToArray(), 2);
                await Wait(0.065);
                if (i == 0) await Capture("transfer");
                await transfer;
                foreach (var victim in victims.Skip(1)) victim.AnimateHit();
                if (i == 0) await Capture("splash");
                await Wait(0.15);
            }
            await Wait(0.6);
            if (root.GetChildren().Any(n => n.Name == "HexMudTransfer"))
                throw new InvalidOperationException("飛行後の表示部品が残っています");
            var pawn = victims[0];
            pawn.BeginStatusSnapshot();
            pawn.ReadStatusSnapshot(StatusKeys.LabelOf(StatusKeys.Curse), 1);
            pawn.CommitStatusSnapshot();
            Require(pawn.HasCurseStain, "次ターンのしみ保持");
            pawn.BeginStatusSnapshot();
            pawn.CommitStatusSnapshot();
            Require(!pawn.HasCurseStain, "解除後のしみ除去");
            pawn.SetStatusIcon(StatusKeys.Curse, true);
            pawn.AnimateDeath();
            Require(!pawn.HasCurseStain, "死亡後のしみ除去");
            pawn.SetHp(100);
            pawn.AnimateRevive();
            Require(!pawn.HasCurseStain, "蘇生で古いしみが戻らない");
            source.SetStatusIcon(StatusKeys.Curse, true);
            Require(source.HasCurseStain, "味方も同じしみ");
            await field.ShowCurseTransfer(victims[1], new[] { source }, 2);
            source.AnimateVictory();
            Require(!source.HasCurseStain, "勝利後のしみ除去");
            CheckBatchBoundaries();
            GD.Print("HEX_MUD_PREVIEW_COMPLETE speed=2 recipients=2 bursts=5 lifecycle=ok boundaries=ok");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PrintErr(error);
            GetTree().Quit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckBatchBoundaries()
    {
        static BattleEvent Hit(int? origin, int target) => new()
        { Kind = BattleEventKind.Damage, Turn = 1, ActorId = 1, TargetId = target, ShareFromId = origin };
        BattleEvent[] events = { Hit(null, 2), Hit(2, 3), Hit(2, 4), Hit(null, 2), Hit(2, 3),
            new() { Kind = BattleEventKind.Death, Turn = 1, TargetId = 3 }, Hit(2, 4), Hit(3, 2) };
        Require(HexShareBatch.End(events, 1) == 3, "元の一撃と次の一撃を混ぜない");
        Require(HexShareBatch.End(events, 4) == 5, "死亡を飛び越えない");
        Require(HexShareBatch.End(events, 6) == 7, "異なる起点を混ぜない");
    }

    private async Task Capture(string name)
    {
        string? directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--hex-capture="))?[14..];
        if (directory is null || DisplayServer.GetName() == "headless") return;
        DirAccess.MakeDirRecursiveAbsolute(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
    }

    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
