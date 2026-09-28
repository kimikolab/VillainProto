using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 隊列の実記録との接続、左右・段・倍速、離陸中の再開と死亡を確認する。
public partial class BasaWindCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            var sample = new[] {
                Move(2, 1), Move(3, 0),
                new BattleEvent { Turn = 1, Kind = BattleEventKind.Tailwind, ActorId = 1, TargetId = 2, PartnerId = 3 },
                Move(2, 0), Move(3, 1), Move(4, 1), Move(5, 0),
                new BattleEvent { Turn = 2, Kind = BattleEventKind.Move, ActorId = 1, TargetId = 2, Slot = 1 },
            };
            var index = new MovementPresentation(sample);
            Require(index.ShuffleStarts.Count == 2 && index.ShuffleStarts[0].Count == 4, "一度の嵐に両陣営をまとめる");
            Require(!index.ShuffleMoves.Contains(3) && !index.ShuffleMoves.Contains(4), "追い風を隊列入れ替えから除外");
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            var fx = (Node3D)typeof(BattlefieldView3D).GetField("_fxRoot", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(field)!;
            void Reset(int team, double speed)
            {
                field.BeginBattle(new DemoOpening[] {
                    new(1, team, "basa", "バサ", 2, 100, 100, 10, AttackPattern.Sweep, true),
                    new(2, team, "yomi", "ヨミ", 0, 100, 100, 10, AttackPattern.Single, true),
                    new(3, team, "sero", "セロ", 3, 100, 100, 10, AttackPattern.Single, false),
                    new(4, 1-team, "knight", "標的A", 0, 100, 100, 10, AttackPattern.Single, true),
                    new(5, 1-team, "axeman", "標的B", 3, 100, 100, 10, AttackPattern.Single, true),
                }, "バサの嵐・追い風", 0);
                foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            }
            void Stage(int stage, double speed) => field.ShowMovementCue(new BattleEvent {
                Turn = 1, Kind = BattleEventKind.DisarrayStage, ActorId = 1, TargetId = 1, Slot = stage }, speed);
            await Wait(0.3);
            foreach (int team in new[] { 0, 1 })
            foreach (double speed in new[] { 1.0, 2.0 })
            foreach (int stage in new[] { 0, 1, 2, 3 })
            {
                Reset(team, speed);
                Stage(stage, speed);
                var basa = field.FindPawn(1)!;
                var home = basa.Home;
                var moves = new[] { Move(2, 3), Move(3, 0), Move(4, 3), Move(5, 0) };
                await field.ShowBasaShuffle(basa, moves, speed);
                foreach (var move in moves) field.MoveWithCue(field.FindPawn(move.TargetId)!, move.Slot, null, basa, storm: true);
                Require(moves.All(e => field.FindPawn(e.TargetId)!.WindCarried) && !basa.WindCarried, "移動対象だけを回転させる");
                await Wait(0.22 / speed);
                Require(field.BasaStormPlays == 1 && basa.Home == home && basa.Hp == 100, "羽ばたきで戦闘状態を変えない");
                if (team == 0 && speed == 1)
                {
                    var sprite = basa.GetChildren().OfType<Sprite3D>().Single();
                    Require(basa.MovementPortrait == "basa_flap", "羽ばたき差分を表示");
                    using var image = sprite.Texture.GetImage();
                    Require(image.GetPixel(0, 0).A == 0, "透過した差分");
                    await Capture($"basa-storm-stage{stage}");
                }
                await Wait(2.1 / speed);
                Require(!basa.BasaFlying && basa.MovementPortrait is null, "着地して待機差分へ戻る");
                Require(field.Pawns.Values.All(p => !p.WindCarried && p.Position.DistanceTo(p.Home) < 0.01f), "回転が終わり全員が記録の席へ着地");
                Require(!fx.GetChildren().Any(n => n.Name.ToString().StartsWith("BasaTornado")), "竜巻が残らない");
                field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Tailwind,
                    ActorId = 1, TargetId = 2, PartnerId = 3, SpreadFromId = 4 }, speed);
                field.MoveWithCue(field.FindPawn(2)!, 0, sample[2], basa);
                field.MoveWithCue(field.FindPawn(3)!, 3, sample[2], basa);
                await Wait(0.12 / speed);
                Require(field.TailwindGustPlays == 1, "追い風の方向を持つ突風");
                if (stage == 3 && speed == 1) await Capture($"basa-tailwind-team{team}");
                await Wait(0.5 / speed);
                if (stage is 0 or 3)
                {
                    var attack = field.Attack(basa, field.FindPawn(4), AttackPattern.Sweep,
                        new[] { field.FindPawn(4)!, field.FindPawn(5)! }, advance: false);
                    await Wait(0.12 / speed);
                    if (stage == 3 && speed == 1) await Capture($"basa-sweep-team{team}");
                    await attack;
                    Require(field.BasaSweepPlays == 1, "通常の薙ぎも風圧の波にする");
                    await Wait(0.6 / speed);
                    await field.Attack(basa, field.FindPawn(4), AttackPattern.Sweep,
                        new[] { field.FindPawn(4)!, field.FindPawn(5)! }, advance: false,
                        movementCue: new BattleEvent { Turn = 1, Kind = BattleEventKind.Squall, ActorId = 1, TargetId = 4 });
                    Require(field.BasaSweepPlays == 2, "突風の追加攻撃も一回だけ風圧の波にする");
                    await Wait(0.6 / speed);
                }
                GD.Print($"BASA_WIND_OK team={team} speed={speed} stage={stage}");
            }
            Reset(0, 1);
            Stage(0, 1);
            await field.ShowBasaShuffle(field.FindPawn(1)!, new[] { Move(2, 3), Move(4, 3) }, 1);
            await Wait(0.05);
            var tornadoes = fx.GetChildren().OfType<MeshInstance3D>().Where(n => n.Name.ToString().StartsWith("BasaTornado")).ToArray();
            Require(tornadoes.Count(n => n.GetAabb().Size.Length() > 0.1f) == 2, "段0は両陣地に1本ずつ");
            Stage(3, 1);
            await Wait(0.05);
            Require(tornadoes.Count(n => n.GetAabb().Size.Length() > 0.1f) == 4, "表示中の段上昇で両陣地の竜巻が増える");
            Reset(0, 1);
            var pending = field.ShowBasaShuffle(field.FindPawn(1)!, new[] { Move(2, 3), Move(4, 3) }, 1);
            Reset(0, 1);
            await pending;
            Require(field.BasaStormPlays == 0, "再開で古い竜巻予約を捨てる");
            pending = field.ShowBasaShuffle(field.FindPawn(1)!, new[] { Move(2, 3) }, 1);
            field.FindPawn(1)!.AnimateDeath();
            await pending;
            Require(!field.FindPawn(1)!.BasaFlying && field.BasaStormPlays == 0, "死亡で離陸と予約を止める");
            Reset(0, 1);
            field.MoveWithCue(field.FindPawn(2)!, 3, null, field.FindPawn(1), storm: true);
            field.FindPawn(2)!.AnimateDeath();
            Require(!field.FindPawn(2)!.WindCarried, "死亡で対象の回転を止める");
            Reset(0, 1);
            field.MoveWithCue(field.FindPawn(2)!, 3, null, field.FindPawn(1), storm: true);
            field.FindPawn(2)!.AnimateMovement(field.FindPawn(2)!.Home);
            Require(!field.FindPawn(2)!.WindCarried, "別の移動へ切り替えたら回転を残さない");
            GD.Print("BASA_WIND_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static BattleEvent Move(int target, int slot) => new() { Turn = 1, Kind = BattleEventKind.Move, ActorId = 1, TargetId = target, Slot = slot };
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
