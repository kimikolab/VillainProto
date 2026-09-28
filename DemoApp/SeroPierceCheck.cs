using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 通常矢との描き分け、左右と倍速、残光の終了・再開の片付けを確認する。
public partial class SeroPierceCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            var fx = (Node3D)typeof(BattlefieldView3D).GetField("_fxRoot", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(field)!;
            void Reset(int team, double speed)
            {
                field.BeginBattle(new DemoOpening[] {
                    new(1, team, "sero", "セロ", 3, 100, 100, 10, AttackPattern.Pierce, false),
                    new(2, 1-team, "knight", "前衛", 0, 100, 100, 10, AttackPattern.Single, true),
                    new(3, 1-team, "axeman", "中衛", 2, 100, 100, 10, AttackPattern.Single, true),
                    new(4, 1-team, "knight", "後衛", 3, 100, 100, 10, AttackPattern.Single, true),
                }, "セロ・光を引く貫通矢", 0);
                foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            }
            foreach (int team in new[] { 0, 1 })
            foreach (double speed in new[] { 1.0, 2.0 })
            {
                Reset(team, speed);
                await Wait(0.30);
                var sero = field.FindPawn(1)!;
                var hits = new[] { field.FindPawn(2)!, field.FindPawn(3)!, field.FindPawn(4)! };
                await field.Attack(sero, hits[0], AttackPattern.Single, new[] { hits[0] }, advance: false);
                Require(field.MovementPiercePlays == 0, "通常矢は従来のまま");
                await Wait(0.3 / speed);
                var barrage = field.Attack(sero, hits[0], AttackPattern.Single, new[] { hits[0] }, advance: false,
                    movementCue: new BattleEvent { Turn = 1, Kind = BattleEventKind.Barrage, ActorId = 1, TargetId = 2 });
                await Wait(0.075 / speed);
                await Capture($"sero-barrage-team{team}-speed{speed}-flight");
                await barrage;
                Require(field.MovementBarragePlays == 1 && field.MovementPiercePlays == 0, "連撃と貫通を描き分ける");
                await Wait(0.3 / speed);
                Require(!fx.GetChildren().Any(n => n.Name.ToString().StartsWith("SeroBarrageArrow")), "連撃の残光は短く片付ける");
                var attack = field.Attack(sero, hits[0], AttackPattern.Pierce, hits, advance: false, arrowStates: "毒,感電");
                Require(field.MovementPiercePlays == 1, "複数対象でも発光矢は1本");
                await Wait(0.075 / speed);
                await Capture($"sero-pierce-team{team}-speed{speed}-flight");
                await attack;
                await Capture($"sero-pierce-team{team}-speed{speed}-trail");
                await Wait(0.6 / speed);
                Require(!fx.GetChildren().Any(n => n.Name.ToString().StartsWith("SeroPiercingArrow")), "残光を片付ける");
                Require(field.Pawns.Values.All(p => p.Hp == 100 && p.Position.DistanceTo(p.Home) < 0.01f), "演出でHP・席を変えない");
                attack = field.Attack(sero, hits[0], AttackPattern.Pierce, hits, advance: false);
                Reset(team, speed);
                await attack;
                await Wait(0.05);
                Require(field.MovementPiercePlays == 0
                    && !fx.GetChildren().Any(n => n.Name.ToString().StartsWith("SeroPiercingArrow")), "再開に旧矢の光を持ち越さない");
                GD.Print($"SERO_PIERCE_OK team={team} speed={speed}");
            }
            GD.Print("SERO_PIERCE_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null || DisplayServer.GetName() == "headless") return;
        string directory = arg[14..];
        DirAccess.MakeDirRecursiveAbsolute(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, "画像保存");
    }
    private static void Require(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); }
}
