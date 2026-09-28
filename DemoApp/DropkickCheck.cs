using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 本体の接触と音、台本Move、左右・倍速、死亡・停止・再開の競合を表示入口で検査。
public partial class DropkickCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(field)!;
            var cue = new BattleEvent { Kind = BattleEventKind.Blast, ActorId = 1, TargetId = 2, Slot = 3, Turn = 1 };
            void Reset(int team, double speed)
            {
                field.BeginBattle(new DemoOpening[] {
                    new(1, team, "hane", "ハネ", 0, 100, 100, 11, AttackPattern.Single, true),
                    new(2, 1-team, "knight", "押し返す相手", 0, 100, 100, 10, AttackPattern.Single, true),
                }, "ハネ・ドロップキックと双掌打", 0);
                foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            }
            foreach (int team in new[] { 0, 1 })
            foreach (double speed in new[] { 1.0, 2.0 })
            {
                Reset(team, speed);
                await Wait(0.25);
                var hane = field.FindPawn(1)!; var foe = field.FindPawn(2)!;
                var sprite = hane.GetChildren().OfType<Sprite3D>().Single();
                Vector3 home = hane.Home;
                var attack = field.Attack(hane, foe, AttackPattern.Pierce, new[] { foe }, advance: false,
                    movementCue: cue, blastDestination: 3);
                Task impact = hane.DropkickImpact;
                Require(hane.DropkickActive && !audio.MovementSoundPlays.ContainsKey(MovementSound.Dropkick), "跳ぶ前にはキック音を鳴らさない");
                Require(!impact.IsCompleted, "台本Moveは実際の接触を待つ");
                // Tween完了通知から開始した場合、次フレームまで跳躍が始まらない。
                // 固定の壁時計ではなく、実際に差分が切り替わる拍で調べる。
                for (int i = 0; i < 120 && hane.MovementPortrait is null; i++) await Wait(0.005);
                Require(hane.MovementPortrait == "hane_dropkick" && sprite.FlipH == (team == 1), "左右とも差分を選ぶ");
                Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Dropkick)
                    && !audio.MovementSoundPlays.ContainsKey(MovementSound.Collision), "接触前にはキック・射出音を鳴らさない");
                await Wait(0.045 / speed);
                await Capture($"dropkick-team{team}-speed{speed}-flight");
                // 描画待ちの時間に依存せず、実際の接触通知を待つ。
                await impact;
                Require(field.HaneDropkickPlays == 1 && audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Dropkick) == 1, "接触で大キックを1回");
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Collision) == 1
                    && foe.BlastActive, "敵の射出に氷音を1回重ねる");
                await Capture($"dropkick-team{team}-speed{speed}-impact");
                Require(hane.Position.IsEqualApprox(home) && hane.Home == home && foe.Slot == 0 && foe.Hp == 100, "接触までは台本の席とHPを変えない");
                await attack;
                await Wait(0.45 / speed);
                field.MoveWithCue(foe, 3, cue, hane);
                await Wait(0.6 / speed);
                Require(foe.Slot == 3 && foe.Position.DistanceTo(foe.Home) < 0.01f, "後続Moveで押し返す");
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Landing) == 1, "押し返した敵の着地を1回");
                Require(!hane.DropkickActive && hane.MovementPortrait is null && hane.Home == home, "空中姿勢を片付ける");
                Reset(team, speed);
                hane = field.FindPawn(1)!;
                foe = field.FindPawn(2)!;
                await Wait(0.20);
                await foe.AdvanceToAttack(hane.Home);
                foe.ReturnFromAttack();
                var spring = new BattleEvent { Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 2, Turn = 1 };
                field.ShowMovementCue(spring, speed);
                Require(hane.MovementPortrait == "hane_palm" && hane.PalmStrikeActive && !hane.DropkickActive,
                    "弾き返しは両掌を打ち出す専用差分");
                Require(!hane.PalmStrikeImpact.IsCompleted && !audio.MovementSoundPlays.ContainsKey(MovementSound.SpringBlock),
                    "両掌の接触前は押し返しも音も待つ");
                await hane.PalmStrikeImpact;
                Require(field.HanePalmPlays == 1 && foe.Slot == 0 && foe.Hp == 100 && !foe.BlastActive,
                    "接触は1回、手番の射出を混ぜない");
                var palmSprite = hane.GetChildren().OfType<Sprite3D>().Single();
                float soleY = palmSprite.Position.Y - palmSprite.Texture.GetHeight() * palmSprite.PixelSize
                    * (0.5f - UiKit.BattlePortraitBottomPaddingRatio("hane_palm"));
                Require(Math.Abs(soleY - 0.05f) < 0.005f, "両掌の接触時にも足元を浮かせない");
                await Capture($"palm-team{team}-speed{speed}-impact");
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.SpringBlock) == 1
                    && !audio.MovementSoundPlays.ContainsKey(MovementSound.Dropkick)
                    && !audio.MovementSoundPlays.ContainsKey(MovementSound.Collision), "弾き返しはパンチを受け止める音だけ");
                field.MoveWithCue(foe, 3, spring, hane);
                await Wait(0.60 / speed);
                Require(!hane.PalmStrikeActive && hane.MovementPortrait is null && hane.Home == home
                    && hane.Position.DistanceTo(home) < 0.01f && foe.Slot == 3 && foe.Position.DistanceTo(foe.Home) < 0.01f,
                    "両掌の差分と押し返しが完了し、待機へ戻る");
                GD.Print($"DROPKICK_OK team={team} speed={speed}");
            }
            foreach (bool palm in new[] { false, true })
            foreach (string cancel in new[] { "actor-death", "target-death", "stop", "reset", "portrait" })
            {
                Reset(0, 1);
                if (palm) field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 2 }, 1);
                var attack = palm ? field.FindPawn(1)!.PalmStrikeImpact
                    : field.Attack(field.FindPawn(1), field.FindPawn(2), AttackPattern.Pierce,
                        new[] { field.FindPawn(2)! }, advance: false, movementCue: cue, blastDestination: 3);
                if (cancel == "actor-death") { field.FindPawn(1)!.AnimateDeath(); field.FindPawn(1)!.AnimateRevive(); }
                if (cancel == "target-death") field.FindPawn(2)!.AnimateDeath();
                if (cancel == "stop") audio.StopAll();
                if (cancel == "reset") Reset(0, 1);
                if (cancel == "portrait") field.FindPawn(1)!.ShowMovementPortrait("hane_spring", 0.3);
                await Wait(0.65);
                await attack;
                Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Dropkick)
                    && !audio.MovementSoundPlays.ContainsKey(MovementSound.Collision)
                    && !audio.MovementSoundPlays.ContainsKey(MovementSound.SpringBlock)
                    && field.HaneDropkickPlays == 0 && field.HanePalmPlays == 0, "中断後に古い接触音を出さない: " + cancel);
                Require(!field.FindPawn(1)!.DropkickActive && !field.FindPawn(1)!.PalmStrikeActive, "中断後に攻撃姿勢を残さない");
            }
            GD.Print("DROPKICK_CHECK_OK");
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
