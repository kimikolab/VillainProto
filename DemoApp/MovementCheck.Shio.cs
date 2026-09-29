using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MovementCheck
{
    // 本番のApplyEventを通し、満タン・横流し・次ターンの写しとの境界を検査する。
    private async Task ShioPreview()
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        main.Visible = false;
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        typeof(Main).GetField("_battleField", Flags)!.SetValue(main, field);
        var result = BattleEngine.Run(Array.Empty<UnitState>(), Array.Empty<UnitState>(), 0, verbose: true);
        var events = (List<BattleEvent>)result.Events;
        typeof(Main).GetField("_result", Flags)!.SetValue(main, result);
        var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
        int Voices() => (int)typeof(BattleAttackAudio).GetField("_nextVoice", Flags)!.GetValue(audio)!;
        Task Play(BattleEvent e)
        {
            events.Add(e);
            return (Task)typeof(Main).GetMethod("ApplyEvent", Flags)!.Invoke(main, new object[] { e, events.Count - 1 })!;
        }
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            events.Clear();
            typeof(Main).GetField("_speed", Flags)!.SetValue(main, speed);
            field.BeginBattle(new DemoOpening[] {
                new(1, team, "shio", "シオ", 2, 100, 100, 10, AttackPattern.Single, false),
                new(2, team, "sero", "動いた味方", 0, 40, 100, 10, AttackPattern.Pierce, false),
                new(3, team, "hane", "満タンの味方", 1, 100, 100, 10, AttackPattern.Pierce, true),
            }, "シオ・回復と強化", 0);
            var shio = field.FindPawn(1)!;
            var target = field.FindPawn(2)!;
            var full = field.FindPawn(3)!;
            Vector3 home = shio.Home;
            await Wait(0.20);
            await Play(new BattleEvent { Kind = BattleEventKind.Move, Turn = 1, ActorId = 3, TargetId = 2, Slot = 3 });
            var healing = Play(new BattleEvent { Kind = BattleEventKind.Heal, Turn = 1, ActorId = 1, TargetId = 2, Amount = 20, HpAfter = 60 });
            Require(field.ShioHealPlays == 1 && target.Hp == 60 && target.HealingDropsActive, "回復を実イベントで表示");
            Require(shio.MovementPortrait == "shio_support", "新しい支援差分を使う");
            var sprite = shio.GetChildren().OfType<Sprite3D>().Single();
            Require(sprite.Texture == UiKit.BattlePortrait((Texture2D)typeof(BattlefieldView3D).GetField("_atlas", Flags)!.GetValue(field)!, "shio_support"),
                "支援差分を実際に読み込む");
            await Wait(0.06 / speed);
            await Capture($"shio-heal-{team}-{speed}");
            await healing;
            await Play(new BattleEvent { Kind = BattleEventKind.Whet, Turn = 1, ActorId = 1, TargetId = 2, IntendedId = 2,
                WhetRoute = WhetRoute.Drifter, Amount = 5, AttackAfter = 15 });
            Require(target.AttackValue == 15 && target.AttackDeltaText == "+5", "移動直後に攻撃値と累計表示を更新");
            await Capture($"shio-buff-{team}-{speed}");
            await Wait(0.65);
            int sounds = Voices();
            await Play(new BattleEvent { Kind = BattleEventKind.StatSnapshot, Turn = 2, TargetId = 2, Amount = 15, Pattern = AttackPattern.Pierce });
            Require(Voices() == sounds && !target.PowerMistActive, "同値の次ターン写しで強化音・オーラを重複させない");
            await Play(new BattleEvent { Kind = BattleEventKind.Whet, Turn = 2, ActorId = 1, TargetId = 3, IntendedId = 3,
                WhetRoute = WhetRoute.Drifter, Amount = 8, AttackAfter = 18 });
            Require(full.Hp == 100 && !full.HealingDropsActive && full.AttackValue == 18 && field.ShioHealPlays == 1,
                "満タンは回復を捏造せず強化だけ表示");
            Require(Voices() == sounds + 1, "満タンの強化にも既存SEを1回");
            var camera = (Camera3D)typeof(BattlefieldView3D).GetField("_camera", Flags)!.GetValue(field)!;
            float dx = (full.FxPoint - shio.FxPoint).Dot(camera.GlobalBasis.X);
            Require(sprite.FlipH == (Math.Abs(dx) < 0.01f ? team != 0 : dx < 0), "対象方向へ支援差分を反転");
            await Capture($"shio-full-{team}-{speed}");
            // 溢れの名目対象と実際の受け手が違う例。Whetが示す相手だけを更新する。
            await Play(new BattleEvent { Kind = BattleEventKind.Whet, Turn = 2, ActorId = 1, TargetId = 2, IntendedId = 3,
                WhetRoute = WhetRoute.Drifter, Amount = 3, AttackAfter = 18 });
            int buffs = field.ShioStrengthPlays;
            sounds = Voices();
            await Play(new BattleEvent { Kind = BattleEventKind.Overflow, Turn = 2, ActorId = 1, TargetId = 3, Amount = 3, StatusRemaining = 3 });
            Require(field.ShioStrengthPlays == buffs && Voices() == sounds && full.AttackValue == 18 && target.AttackValue == 18,
                "溢れの見出しで二重再生せず、名目対象への追加強化も捏造しない");
            await Play(new BattleEvent { Kind = BattleEventKind.Whet, Turn = 2, ActorId = 1, TargetId = 2,
                WhetRoute = WhetRoute.Drifter, Amount = 5, AttackAfter = 8 });
            Require(target.AttackValue == 8, "逆しまなどの受け手でもAmountを足さず台本の値へ更新");
            await Wait(0.65 / speed);
            Require(shio.MovementPortrait is null && shio.Home == home && shio.Slot == 2 && sprite.FlipH == (team == 1),
                "支援後は席を動かさず通常絵へ復帰");
            field.ShowShioHealing(shio, target, 10, speed);
            shio.AnimateDeath();
            Require(shio.MovementPortrait is null, "死亡で支援差分を解除");
            field.BeginBattle(Array.Empty<DemoOpening>(), "再開", 0);
            await Wait(0.60 / speed);
            Require(field.ShioHealPlays == 0 && field.ShioStrengthPlays == 0 && Voices() == 0,
                "再開で計数と音を消し古い演出を戻さない");
            GD.Print($"SHIO_SUPPORT_OK team={team} speed={speed}");
        }
        field.QueueFree();
        main.QueueFree();
        await Wait(0.10);
    }
}
