using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 踏込・射出中の見た目・台本の席への接続・巻き込みの限定・再開の競合を同じ入口で検査する。
public partial class SmashCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            void Reset(int team, double speed)
            {
                field.BeginBattle(new DemoOpening[] {
                    new(1, team, "hane", "ハネ", 0, 100, 100, 11, AttackPattern.Single, UnitCatalog.Hane.Advances),
                    new(2, team, "yomi", "ヨミ", 2, 100, 100, 6, AttackPattern.Single, UnitCatalog.Yomi.Advances),
                    new(3, 1-team, "knight", "先頭", 0, 100, 100, 10, AttackPattern.Single, true),
                    new(4, 1-team, "knight", "巻き込み1", 2, 100, 100, 10, AttackPattern.Single, true),
                    new(5, 1-team, "knight", "巻き込み2", 3, 100, 100, 10, AttackPattern.Single, true),
                    new(6, 1-team, "knight", "経路外", 4, 100, 100, 10, AttackPattern.Single, true),
                }, "踏込・薙ぎ・スマッシュ確認", 0);
                foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            }
            foreach (int team in new[] { 0, 1 })
            foreach (double speed in new[] { 1.0, 2.0 })
            {
                Reset(team, speed);
                await Wait(0.12);
                var hane = field.FindPawn(1)!; var yomi = field.FindPawn(2)!;
                var a = field.FindPawn(3)!; var b = field.FindPawn(4)!; var c = field.FindPawn(5)!;
                var outside = field.FindPawn(6)!;
                Vector3 home = hane.Home;
                var cue = new BattleEvent { Kind = BattleEventKind.Blast, Turn = 1, ActorId = 1, TargetId = 3 };
                var attack = field.Attack(hane, a, AttackPattern.Pierce, new[] { a, b, c }, movementCue: cue, blastDestination: 3);
                for (int i = 0; i < 100 && field.HaneBlastPlays == 0; i++) await Wait(0.01);
                Require(field.HaneBlastPlays == 1, "吹っ飛ばし開始");
                Require(hane.Advances && hane.Position.DistanceTo(home) > 0.5f && hane.Home == home && hane.Slot == 0, "踏込は席を動かさない");
                Require(field.HanePinPlays == 2 && !outside.BlastActive, "実際の被弾者2体だけがピンになる");
                await Wait(0.08 / speed);
                Require(a.BlastVisualPosition.Y > a.Position.Y + 0.2f, "射出で浮き上がる");
                Require(a.Slot == 0 && a.Hp == 100, "飛行中も台本前の席とHP");
                await Capture($"smash-team{team}-speed{speed}-launch");
                await attack;
                await Wait(0.45 / speed);
                Require(a.BlastActive && !b.BlastActive && !c.BlastActive, "主目標は着地待ち、ピンは復帰");
                field.MoveWithCue(a, 3, cue, hane);
                field.MoveWithCue(b, 0, null, hane);
                field.MoveWithCue(c, 2, null, hane);
                Require(!a.BlastActive && a.Position.IsEqualApprox(a.Home) && a.Slot == 3, "Moveで着地を確定し二重に投げない");
                await Wait(0.30 / speed);
                Require(hane.Position.DistanceTo(home) < 0.02f, "ハネが踏込から帰る");
                await field.Attack(yomi, b, AttackPattern.Sweep, new[] { b, c }, reaction: true, attackPower: 40);
                Require(yomi.Advances && yomi.Position.DistanceTo(yomi.Home) > 0.5f, "ヨミが踏み込む");
                Require(field.YomiSweepPlays == 1, "薙ぎの面を1回だけ描く");
                await Wait(0.06 / speed);
                await Capture($"smash-team{team}-speed{speed}-sweep");
                await Wait(0.65 / speed);
                Require(yomi.Position.DistanceTo(yomi.Home) < 0.02f, "ヨミが席へ帰る");
                await field.Attack(yomi, b, AttackPattern.Single, new[] { b }, advance: false, attackPower: 40);
                Require(field.YomiSweepPlays == 1, "単体に薙ぎの面を出さない");
                b.BlastFlight(b.Position + Vector3.Right, false, false, 1);
                b.AnimateDeath(); b.AnimateRevive();
                Require(!b.BlastActive, "死亡・蘇生で飛行を解除");
                GD.Print($"SMASH_CASE_OK team={team} speed={speed}");
            }
            Reset(0, 1);
            var old = field.FindPawn(1)!; var foe = field.FindPawn(3)!;
            var pending = field.Attack(old, foe, AttackPattern.Pierce, new[] { foe }, advance: false,
                movementCue: new BattleEvent { Kind = BattleEventKind.Blast, Turn = 1, ActorId = 1, TargetId = 3 }, blastDestination: 3);
            Reset(0, 1);
            await pending;
            Require(field.HaneBlastPlays == 0 && !field.FindPawn(3)!.BlastActive, "再開後に旧射出を出さない");
            var current = field.FindPawn(3)!;
            await field.Attack(field.FindPawn(1), current, AttackPattern.Pierce, new[] { current }, advance: false,
                movementCue: new BattleEvent { Kind = BattleEventKind.Blast, Turn = 1, ActorId = 1, TargetId = 3 });
            await Wait(0.5);
            Require(!current.BlastActive && current.Slot == 0 && current.Position.IsEqualApprox(current.Home), "Moveなしの射出は元の席へ復帰");
            GD.Print("SMASH_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Capture(string name)
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null || DisplayServer.GetName() == "headless") return;
        string directory = arg[14..];
        DirAccess.MakeDirRecursiveAbsolute(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, "画像保存");
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
