using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 差分の実イベント接続、足元、優先度、遅延復帰と生死の競合を検査する。
public partial class MovementPortraitCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            var cases = new[] {
                ("sero", "sero_evade", BattleEventKind.Evade),
                ("sero", "sero_last_dodge", BattleEventKind.LastDodge),
                ("sero", "sero_decoy", BattleEventKind.Decoy),
                ("sero", "sero_move_shot", BattleEventKind.MoveShot),
                ("hane", "hane_flying_kick", BattleEventKind.Spring),
                ("hane", "hane_spring_guard", BattleEventKind.Spring),
                ("shio", "shio_retreat", BattleEventKind.Retreat),
                ("yomi", "yomi_stumble", BattleEventKind.KillImpact),
            };
            foreach (int team in new[] { 0, 1 })
            foreach (var (unit, key, kind) in cases)
            {
                field.BeginBattle(new DemoOpening[] {
                    new(1, team, unit, unit, 2, 100, 100, 10, AttackPattern.Single, false),
                    new(2, 1-team, "knight", "相手", 2, 100, 100, 10, AttackPattern.Single, true),
                    new(3, team, "sero", "仲間", 0, 100, 100, 10, AttackPattern.Single, false),
                }, "移動差分の確認", 0);
                var pawn = field.FindPawn(1)!;
                pawn.AnimationSpeed = 2;
                var sprite = pawn.GetChildren().OfType<Sprite3D>().Single();
                await Wait(0.1);
                var texture = UiKit.BattlePortrait(atlas, key);
                Require(texture != UiKit.BattlePortrait(atlas, unit), "差分が実ファイルから読み込まれる");
                using var pixels = texture.GetImage();
                Require(pixels.GetPixel(0, 0).A == 0, "PNGが透過");
                var cue = new BattleEvent { Turn = 1, Kind = kind, ActorId = 1,
                    TargetId = kind == BattleEventKind.Retreat ? 3 : 2, Text = ImpactLabels.Tumble };
                field.ShowMovementCue(cue, 2, key == "hane_spring_guard"
                    ? new BattleEvent { Turn = 1, Kind = BattleEventKind.SpringGuard, ActorId = 1, TargetId = 3, PartnerId = 2 } : null);
                Task? shot = kind == BattleEventKind.MoveShot
                    ? field.Attack(pawn, field.FindPawn(2), AttackPattern.Pierce, new[] { field.FindPawn(2)! },
                        advance: false, movementCue: cue) : null;
                Require(pawn.MovementPortrait == key && sprite.Texture == texture, "実イベントで差分を選ぶ");
                // 画像の最下端の不透明な靴底が地面付近へ来る（全差分を同じ基準で測る）。
                float bottom = sprite.Position.Y - texture.GetHeight() * sprite.PixelSize
                    * (0.5f - UiKit.BattlePortraitBottomPaddingRatio(key));
                Require(Math.Abs(bottom - 0.05f) < 0.005f, "切り替え時の靴底位置");
                pawn.SetBurning(true);
                Require(sprite.Texture == texture, "燃焼通知で差分を消さない");
                pawn.SetBurning(false);
                if (kind == BattleEventKind.LastDodge)
                {
                    field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Evade, ActorId = 1, TargetId = 2 }, 2);
                    Require(pawn.MovementPortrait == key, "直後のEvadeで必死の逃げ足を消さない");
                }
                await Wait(0.04);
                await Capture($"{key}-team{team}");
                if (shot is not null) await shot;
                await Wait(0.4);
                Require(pawn.MovementPortrait is null && sprite.Texture == UiKit.BattlePortrait(atlas, unit), "待機絵へ復帰");
                pawn.ShowMovementPortrait(key, 0.4);
                await Wait(0.1);
                pawn.ShowMovementPortrait(key, 0.4);
                await Wait(0.13);
                Require(pawn.MovementPortrait == key, "古い復帰予約で新しい差分を消さない");
                if (unit == "sero")
                {
                    pawn.AnimateBowAttack();
                    Require(pawn.MovementPortrait is null && sprite.Texture == UiKit.BattlePortrait(atlas, "sero_attack"), "回避から撃ち返しへ接続");
                }
                pawn.ShowMovementPortrait(key, 0.4);
                pawn.AnimateDeath();
                Require(pawn.MovementPortrait is null, "死亡で差分を解除");
                pawn.AnimateRevive();
                await Wait(0.4);
                Require(sprite.Texture == UiKit.BattlePortrait(atlas, unit), "蘇生後に古い差分を戻さない");
                if (team == 0)
                {
                    pawn.ShowMovementPortrait(key, 0.4);
                    pawn.AnimateVictory();
                    await Wait(0.55);
                    Require(pawn.MovementPortrait is null && sprite.Texture == UiKit.Portrait(atlas, unit), "勝利絵を遅延復帰で壊さない");
                }
                GD.Print($"MOVEMENT_PORTRAIT_OK {key} team={team}");
            }
            field.QueueFree();
            await Wait(0.2);
            GD.Print("MOVEMENT_PORTRAIT_CHECK_OK");
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
