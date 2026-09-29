using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MovementCheck
{
    private static void CheckLandingIndex()
    {
        var events = new[] {
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Tailwind, ActorId = 1, TargetId = 2, PartnerId = 1 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Move, ActorId = 1, TargetId = 1 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Landing, ActorId = 1, TargetId = 2, PartnerId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Move, ActorId = 1, TargetId = 2 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 4 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 4 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Death, TargetId = 4 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Move, ActorId = 1, TargetId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Landing, ActorId = 1, TargetId = 2, PartnerId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.TurnStart },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Move, ActorId = 1, TargetId = 2 },
        };
        var index = new MovementPresentation(events);
        Require(index.Moves[1].Kind == BattleEventKind.Tailwind
            && ReferenceEquals(index.Moves[3], events[2]) && ReferenceEquals(index.Moves[7], events[2]),
            "着地の2件を割り込み越しに結び、追い風が横取りしない");
        Require(index.Moves.Count == 3 && !index.Moves.ContainsKey(10), "空振りの着地を次ターンへ持ち越さない");
    }

    private async Task LandingPreview()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            field.BeginBattle(new DemoOpening[] {
                new(1, team, "hane", "ハネ", 0, 100, 100, 11, AttackPattern.Pierce, true),
                new(2, team, "sero", "味方B", 1, 100, 100, 10, AttackPattern.Pierce, false),
                new(3, team, "shio", "味方C", 2, 100, 100, 10, AttackPattern.Single, false),
                new(4, 1-team, "knight", "敵", 0, 100, 100, 10, AttackPattern.Single, true),
            }, "ハネ・救援と着地", 0);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            await Wait(0.15);
            var hane = field.FindPawn(1)!;
            var b = field.FindPawn(2)!;
            var c = field.FindPawn(3)!;
            var home = hane.Home;
            // X字の前1と前3（0と1）は非隣接。
            var guard = new BattleEvent { Turn = 1, Kind = BattleEventKind.SpringGuard, ActorId = 1, TargetId = 2, PartnerId = 4 };
            field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 4 }, speed, guard);
            Require(hane.MovementPortrait == "hane_rescue", "離れた味方へは横っ飛び");
            await Wait(0.10 / speed);
            await Capture($"hane-rescue-{team}-{speed}");
            await hane.PalmStrikeImpact;
            Require(hane.MovementPortrait == "hane_spring_guard" && hane.Home == home && hane.Slot == 0,
                "掌へつなぎ、ハネの席を動かさない");
            await Wait(0.45 / speed);
            string? previous = null;
            var camera = (Camera3D)typeof(BattlefieldView3D).GetField("_camera", Flags)!.GetValue(field)!;
            var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
            for (int n = 0; n < 4; n++)
            {
                if (n == 0)
                {
                    // Landingは直前のMoveの描画終了を待たず届く。
                    hane.Position = home - camera.GlobalBasis.X * 1.2f;
                    hane.AnimateMovement(home, 0.32f, 0.26);
                }
                var cue = new BattleEvent { Turn = 1, Kind = BattleEventKind.Landing, ActorId = 1, TargetId = 2, PartnerId = 3, Slot = n + 1 };
                field.ShowMovementCue(cue, speed);
                Require(hane.MovementPortrait is "hane_bump_rebound" or "hane_bump_apology", "指定の2枚を毎回表示");
                Require(previous != hane.MovementPortrait, "直前の差分を繰り返さない");
                previous = hane.MovementPortrait;
                var sprite = hane.GetChildren().OfType<Sprite3D>().Single();
                Vector3 spriteStart = sprite.GlobalPosition;
                float dx = (b.Home - hane.Home).Dot(camera.GlobalBasis.X);
                Require(sprite.FlipH == (System.Math.Abs(dx) < 0.01f ? team != 0 : dx < 0), "対象の画面方向へ反転");
                Require(!hane.AllyBumpImpact.IsCompleted && audio.MovementSoundPlays.GetValueOrDefault(MovementSound.AllyBump) == n,
                    "接近前にボヨンを鳴らさない");
                await hane.AllyBumpImpact;
                Require(sprite.GlobalPosition.DistanceTo(b.FxPoint) < spriteStart.DistanceTo(b.FxPoint)
                    && hane.Home == home && hane.Slot == 0, "対象へ本体が寄り、席は動かない");
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.AllyBump) == n + 1,
                    "接触にボヨンを1回だけ鳴らす");
                Vector2 contactPixel = hane.MovementPortrait == "hane_bump_rebound" ? new(910, 475) : new(755, 740);
                Vector3 contact = hane.MovementPortraitPoint(contactPixel, camera);
                Vector3 targetEdge = b.FxPoint + camera.GlobalBasis.X * (sprite.FlipH ? 0.40f : -0.40f);
                Require(contact.DistanceTo(targetEdge) < 0.03f, "直前の移動中でもお尻の接点を味方へ合わせる");
                await Capture($"hane-contact-{team}-{speed}-{n}");
                int bSlot = b.Slot;
                field.MoveWithCue(b, c.Slot, cue, hane);
                Require(b.AllyBouncing && !b.WindCarried && !c.AllyBouncing, "対象だけ弾み、相手のMoveを先取りしない");
                await Wait(0.09 / speed);
                await Capture($"hane-landing-{team}-{speed}-{n}");
                field.MoveWithCue(c, bSlot, cue, hane);
                Require(c.AllyBouncing && !c.WindCarried, "後続の相手も台本の時点で渦を使わず弾む");
                await Wait(0.50 / speed);
                Require(!b.AllyBouncing && !c.AllyBouncing && hane.MovementPortrait is null && !hane.AllyBumpActive
                    && sprite.FlipH == (team == 1), "着地後に差分と反転を解除");
            }
            Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Collision)
                && !audio.MovementSoundPlays.ContainsKey(MovementSound.Tornado)
                && !audio.MovementSoundPlays.ContainsKey(MovementSound.Landing), "味方弾きに氷・竜巻・転倒音を重ねない");
            var landing = new BattleEvent { Turn = 1, Kind = BattleEventKind.Landing, ActorId = 1, TargetId = 2, PartnerId = 3 };
            field.ShowMovementCue(landing, speed);
            var interrupted = hane.AllyBumpImpact;
            hane.AnimateDeath();
            Require(interrupted.IsCompleted && !hane.AllyBumpActive, "接近中の死亡で待機を解除");
            await Wait(0.50 / speed);
            Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.AllyBump) == 4, "死亡後にボヨンが復活しない");
            hane.AnimateRevive();
            await Wait(0.50 / speed);
            field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 4 }, speed, guard);
            var pending = hane.PalmStrikeImpact;
            hane.AnimateDeath();
            Require(pending.IsCompleted && !hane.PalmStrikeActive && hane.MovementPortrait is null,
                "横っ飛び中の死亡でも待機・差分を残さない");
            hane.AnimateRevive();
            await Wait(0.50 / speed);
            field.ShowMovementCue(landing, speed);
            interrupted = hane.AllyBumpImpact;
            field.BeginBattle(System.Array.Empty<DemoOpening>(), "再開", 0);
            await Wait(0.50 / speed);
            Require(interrupted.IsCompleted && audio.MovementSoundPlays.Count == 0, "再開で待機を解き古い音を鳴らさない");
            GD.Print($"HANE_LANDING_OK team={team} speed={speed}");
        }
        field.QueueFree();
    }
}
