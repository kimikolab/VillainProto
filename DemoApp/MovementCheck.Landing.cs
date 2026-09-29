using BattleCore;
using Godot;
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
            for (int n = 0; n < 4; n++)
            {
                var cue = new BattleEvent { Turn = 1, Kind = BattleEventKind.Landing, ActorId = 1, TargetId = 2, PartnerId = 3, Slot = n + 1 };
                field.ShowMovementCue(cue, speed);
                Require(hane.MovementPortrait is "hane_bump_rebound" or "hane_bump_apology", "指定の2枚を毎回表示");
                Require(previous != hane.MovementPortrait, "直前の差分を繰り返さない");
                previous = hane.MovementPortrait;
                var sprite = hane.GetChildren().OfType<Sprite3D>().Single();
                float dx = (b.Home - hane.Home).Dot(camera.GlobalBasis.X);
                Require(sprite.FlipH == (System.Math.Abs(dx) < 0.01f ? team != 0 : dx < 0), "対象の画面方向へ反転");
                int bSlot = b.Slot;
                field.MoveWithCue(b, c.Slot, cue, hane);
                Require(b.WindCarried, "弾かれた味方は渦付きで飛ぶ");
                await Wait(0.09 / speed);
                await Capture($"hane-landing-{team}-{speed}-{n}");
                field.MoveWithCue(c, bSlot, cue, hane);
                Require(c.WindCarried, "後続の相手も台本の時点で飛ぶ");
                await Wait(0.50 / speed);
                Require(!b.WindCarried && !c.WindCarried && hane.MovementPortrait is null
                    && sprite.FlipH == (team == 1), "着地後に差分と反転を解除");
            }
            field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 4 }, speed, guard);
            var pending = hane.PalmStrikeImpact;
            hane.AnimateDeath();
            Require(pending.IsCompleted && !hane.PalmStrikeActive && hane.MovementPortrait is null,
                "横っ飛び中の死亡でも待機・差分を残さない");
            GD.Print($"HANE_LANDING_OK team={team} speed={speed}");
        }
        field.QueueFree();
    }
}
