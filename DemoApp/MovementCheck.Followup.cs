using BattleCore;
using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class MovementCheck
{
    private static void CheckFollowupIndex()
    {
        var events = new[] {
            new BattleEvent { Turn = 1, Kind = BattleEventKind.SpringGuard, ActorId = 1, TargetId = 2, PartnerId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 3, PartnerId = 4 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Move, ActorId = 1, TargetId = 3, Slot = 2 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Move, ActorId = 1, TargetId = 4, Slot = 0 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.MoveShot, ActorId = 2, TargetId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.DecoyShow, ActorId = 2, Slot = 0 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, TargetId = 3, Pattern = AttackPattern.Pierce },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.StatusArrow, ActorId = 2, TargetId = 3, Text = "毒,感電" },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, TargetId = 4 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.SpringGuard, ActorId = 1, TargetId = 2, PartnerId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.TurnStart },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 3 },
        };
        var index = new MovementPresentation(events);
        Require(index.SpringGuards.Count == 1 && index.SpringGuards[1].TargetId == 2,
            "隣への弾き返しだけを結び、通常の弾き返しや次ターンに残さない");
        Require(index.Moves.Count == 2 && index.Moves.Values.All(e => e.Kind == BattleEventKind.Spring),
            "守った味方やハネに存在しないMoveを作らない");
        Require(index.Attacks.Count == 1 && index.Attacks[7].Kind == BattleEventKind.MoveShot
            && index.ArrowStates[7] == "毒,感電", "移動の1矢だけに状態と見出しを結ぶ");
    }

    private async Task Followup()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        void Reset(int team, double speed)
        {
            field.BeginBattle(new DemoOpening[] {
                new(1, team, "hane", "ハネ", 1, 100, 100, 11, AttackPattern.Pierce, true),
                new(2, team, "sero", "セロ", 0, 100, 100, 10, AttackPattern.Pierce, false),
                new(3, 1-team, "knight", "殴った敵", 0, 100, 100, 10, AttackPattern.Single, true),
                new(4, 1-team, "archer", "奥の敵", 2, 100, 100, 10, AttackPattern.Single, false),
                new(5, team, "shio", "シオ", 4, 100, 100, 10, AttackPattern.Single, false),
            }, "移動軸・追記の確認", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
        }
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            Reset(team, speed);
            await Wait(0.20);
            var hane = field.FindPawn(1)!;
            var sero = field.FindPawn(2)!;
            var foe = field.FindPawn(3)!;
            var back = field.FindPawn(4)!;
            var home = hane.Home;
            var shio = field.FindPawn(5)!;
            field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Retreat,
                ActorId = 5, TargetId = 2, PartnerId = 1, StatusRemaining = 2 }, speed);
            Require(shio.MovementPortrait == "shio_retreat", "緊急退避は蔓を引く専用差分");
            await Wait(0.07 / speed);
            await Capture($"movement-followup-{team}-{speed}-retreat");
            await Wait(0.45 / speed);
            var decoy = new BattleEvent { Turn = 1, Kind = BattleEventKind.DecoyShow, ActorId = 2, Slot = 1, Amount = 70 };
            field.ShowMovementCue(decoy, speed);
            await Wait(0.40 / speed);
            Require(sero.DecoyShown && sero.MovementPortrait is null, "身振りの後も挑発の印が残る");
            sero.SetStatusEffects(1, 0, 0);
            field.MoveWithCue(sero, 3, null, hane);
            await Wait(0.25 / speed);
            for (int i = 0; i < 60 && sero.Position.DistanceTo(sero.Home) > 0.01f; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(sero.DecoyShown && sero.GetNode<Node3D>("DecoySign").GlobalPosition.DistanceTo(sero.Home) < 0.2f,
                "標と共存して移動先へ追従する");
            await Capture($"movement-followup-{team}-{speed}-decoy");
            field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.DecoyShow, ActorId = 2, Slot = 0 }, speed);
            Require(!sero.DecoyShown, "台本の消灯で即座に印を消す");

            var guard = new BattleEvent { Turn = 1, Kind = BattleEventKind.SpringGuard, ActorId = 1, TargetId = 2, PartnerId = 3 };
            var spring = new BattleEvent { Turn = 1, Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 3, PartnerId = 4 };
            field.ShowMovementCue(guard, speed);
            Require(!hane.PalmStrikeActive, "介入の見出しでは掌の攻撃を重ねない");
            field.ShowMovementCue(spring, speed, guard);
            Require(hane.MovementPortrait == "hane_spring_guard", "味方への救援は片掌の専用差分");
            await hane.PalmStrikeImpact;
            Require(field.HanePalmPlays == 1 && hane.Slot == 1 && hane.Home == home && sero.Slot == 3
                && foe.Hp == 100, "隣への押し返しは1回、味方の席もHPも変えない");
            await Capture($"movement-followup-{team}-{speed}-guard");
            field.MoveWithCue(foe, 2, spring, hane);
            field.MoveWithCue(back, 0, spring, hane);
            await Wait(0.6 / speed);
            Require(!hane.PalmStrikeActive && hane.Position.IsEqualApprox(home), "横からの介入後は元の姿勢へ戻る");

            var shot = new BattleEvent { Turn = 1, Kind = BattleEventKind.MoveShot, ActorId = 2, TargetId = 3, Amount = 1 };
            field.MoveWithCue(sero, 0, null, hane);
            await Wait(0.07 / speed);
            field.ShowMovementCue(shot, speed);
            Require(field.MovementArrowPlays == 0, "追撃の見出しだけでは矢を出さない");
            var attacking = field.Attack(sero, foe, AttackPattern.Pierce, new[] { foe, back }, advance: false,
                reaction: true, movementCue: shot, arrowStates: "毒,燃焼,感電");
            Require(sero.MovementPortrait == "sero_move_shot", "移動追撃は振り向いて射る専用差分");
            await Wait(0.045 / speed);
            await Capture($"movement-followup-{team}-{speed}-shot");
            await attacking;
            await Wait(0.4 / speed);
            Require(field.MovementShotPlays == 1 && field.MovementArrowPlays == 1 && field.MovementPiercePlays == 1
                && sero.Position.DistanceTo(sero.Home) < 0.01f && sero.Slot == 0,
                "移動を止めず貫きの1矢を撃ち、着地する");
            field.ShowMovementCue(decoy, speed);
            sero.AnimateDeath();
            Require(!sero.DecoyShown, "死亡で挑発を消す");
            sero.AnimateRevive();
            Require(!sero.DecoyShown, "蘇生だけでは古い挑発を戻さない");
            field.ShowMovementCue(decoy, speed);
            if (team == 0) { sero.AnimateVictory(); Require(!sero.DecoyShown, "勝利絵に挑発を残さない"); }
            Reset(team, speed);
            Require(!field.FindPawn(2)!.DecoyShown && field.MovementShotPlays == 0, "再開時に印と追撃の記録を初期化");
            GD.Print($"MOVEMENT_FOLLOWUP_OK team={team} speed={speed}");
        }
        field.QueueFree();
    }
}
