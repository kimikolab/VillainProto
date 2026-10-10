using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckHisaVisuals()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening O(int id, UnitDef unit, int slot) => new(id, team, unit.Id, unit.Name, slot,
                60, 100, 10, AttackPattern.Single, false, unit.Traits);
            DemoOpening[] opening = [O(1, UnitCatalog.Hisa, 4), O(2, UnitCatalog.Tome, 2), O(3, UnitCatalog.Golm, 1),
                new(10, 1 - team, "knight", "標の敵", 0, 100, 100, 10, AttackPattern.Single, false)];
            field.BeginBattle(opening, "ヒサ・号令と庇い", 3);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            var hisa = field.FindPawn(1)!; var misa = field.FindPawn(2)!;
            var ally = field.FindPawn(3)!; var enemy = field.FindPawn(10)!;
            misa.MisaFeathers!.SetCount(5);
            await Wait(.75 / speed);
            Require(field.CommandOrbCount(1) == 0, "開始時の玉は空");
            await Capture($"hisa-team{team}-speed{speed}-empty");
            for (int n = 1; n <= 3; n++)
            {
                field.ShowCommandBall(hisa, new() { Kind = BattleEventKind.CommandBall, Turn = 1,
                    Amount = n, Text = CommandBallLabels.Gain }, speed);
                await Wait(.15 / speed);
                Require(field.CommandOrbCount(1) == n, "玉は通知の数で灯る");
            }
            for (int n = 0; n < 80; n++) field.ShowCommandBall(hisa,
                new() { Kind = BattleEventKind.CommandBall, Turn = 1, Amount = 3, Text = CommandBallLabels.Spill }, speed);
            Require(field.CommandSpills == 1, "満杯の溢れは連続区間に一度だけ");
            await Capture($"hisa-team{team}-speed{speed}-full");
            BattleEvent[] layers = Enumerable.Range(1, 3).Select(n => new BattleEvent {
                Kind = BattleEventKind.MarkLayer, Turn = 1, ActorId = 1, TargetId = 10, Amount = n }).ToArray();
            var command = new BattleEvent { Kind = BattleEventKind.Command, Turn = 1, ActorId = 1, TargetId = 10,
                Amount = 3, Slot = 60, Text = CommandLabels.Turn };
            var plan = HisaCommandPresentation.Build([.. layers, command]);
            Require(plan.DeferredLayers.SetEquals(new[] { 0, 1, 2 }), "号令直前の層だけを遅らせる");
            var fresh = new BattleEvent { Kind = BattleEventKind.StatusGain, Turn = 1, ActorId = 1, TargetId = 10,
                Text = StatusKeys.Marked, Amount = 1 };
            var freshPlan = HisaCommandPresentation.Build([layers[0], fresh, layers[1], layers[2], command]);
            Require(freshPlan.DeferredMarks.SetEquals(new[] { 1 }) && freshPlan.FreshCommands.Contains(4),
                "初回の標通知も号令の着弾まで保留し、音を二重にしない");
            var plainPlan = HisaCommandPresentation.Build([fresh, new() { Kind = BattleEventKind.Command, Turn = 1,
                ActorId = 1, TargetId = 10, Amount = 1, Slot = 20, Text = CommandLabels.Turn }]);
            Require(plainPlan.DeferredMarks.Contains(0) && plainPlan.FreshCommands.Contains(1), "層のない台本も標の着弾へ合わせる");
            var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
            var pending = field.ShowCommand(hisa, enemy, command, plan.Layers[3], speed);
            await Wait(.45 / speed);
            Require(enemy.MarkLayers == 0, "玉の到着前には層を増やさない");
            Require(field.CommandFocusVisible && audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.MarkAdd) == 0,
                "玉の消費を強調し、着弾前に標の音を鳴らさない");
            await Capture($"hisa-team{team}-speed{speed}-command");
            await Wait(.16 / speed);
            await Capture($"hisa-team{team}-speed{speed}-charge");
            // 描画保存に要した時間とタイマーの継続順に依存せず、刻印した直後を撮る。
            for (int frame = 0; frame < 60 && enemy.MarkLayers == 0; frame++) await Wait(.015 / speed);
            await Wait(.02 / speed);
            await Capture($"hisa-team{team}-speed{speed}-stamp");
            await pending;
            Require(enemy.MarkLayers == 3 && field.MarkLayerPlays == 3, "着弾で3層を一度に表示");
            Require(misa.MisaFeathers.BeamCount == 0, "号令の予告は羽を勝手に撃たない");
            Require(audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.MarkAdd) == 1,
                "3層の同時着弾で支給音を1回鳴らす");
            int beams = audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.Beam);
            int impacts = audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.BeamHit);
            await field.PlayFeatherMark(misa, enemy, new() { Kind = BattleEventKind.FeatherMark,
                Turn = 1, Text = FeatherMarkLabels.Foe }, speed);
            Require(misa.MisaFeathers.MarkBeamCount == 1, "台本の1件で1本だけ撃つ");
            Require(audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.Beam) == beams + 1
                && audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.BeamHit) == impacts + 1,
                "敵への標撃ちは発射音と着弾音を1回ずつ同時に鳴らす");
            await Capture($"hisa-team{team}-speed{speed}-laser");
            var firstMuzzle = misa.MisaFeathers.LastMuzzle;
            await field.PlayFeatherMark(misa, enemy, new() { Kind = BattleEventKind.FeatherMark,
                Turn = 1, Text = FeatherMarkLabels.Foe }, speed);
            Require(misa.MisaFeathers.LastMuzzle.DistanceTo(firstMuzzle) > 1f
                && misa.MisaFeathers.MarkBeamCount == 2 && misa.MisaFeathers.Count == 5 && enemy.Hp == 100,
                "次の台本で別の羽から集中射撃し、在庫とHPは変えない");
            await Capture($"hisa-team{team}-speed{speed}-crossfire");
            field.ShowCommandBall(hisa, new() { Kind = BattleEventKind.CommandBall, Turn = 1, Amount = 0, Text = CommandBallLabels.Use }, speed);
            Require(field.CommandOrbCount(1) == 0, "使う通知で空に戻る");
            int hp = ally.Hp;
            await field.ShowHisaCover(hisa, ally, enemy, speed);
            Require(hisa.IsGuarding && hisa.Position.DistanceTo(hisa.Home) > .1f
                && ally.Hp == hp && hisa.MovementPortrait == "hisa_cover", "自分を指して仲間の前に出る・味方HP不変");
            await Capture($"hisa-team{team}-speed{speed}-cover");
            hisa.ReturnFromHisaCover(); await Wait(.3 / speed);
            Require(!hisa.IsGuarding && hisa.Position.DistanceTo(hisa.Home) < .01f, "被弾後は席へ戻る");
            field.ShowHisaQuiet(hisa, 2, speed); field.ShowHisaQuiet(hisa, 2, speed);
            Require(field.QuietPlays == 1, "SealedとFramedの沈黙を重ねない");
            await Capture($"hisa-team{team}-speed{speed}-quiet");
            field.ShowMarkLayer(enemy, 4, speed);
            field.ShowMarkLayer(enemy, 4, speed);
            field.ShowMarkLayer(enemy, 0, speed);
            Require(audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.MarkAdd) == 2,
                "通常の層加算で鳴り、同値と除去では鳴らない");
            var plain = field.ShowCommand(hisa, enemy, new() { Kind = BattleEventKind.Command, Turn = 1,
                Amount = 1, Text = CommandLabels.Turn }, null, speed, freshMark: true);
            Require(!field.CommandFocusVisible, "消費なしの通知には強調暗転を付けない");
            await plain;
            Require(audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.MarkAdd) == 3,
                "層のない初回付与でも支給音を鳴らす");
            pending = field.ShowCommand(hisa, enemy, command, layers, speed);
            field.BeginBattle(opening, "途中から再戦", 3);
            await pending;
            Require(field.CommandPlays == 0 && field.FindPawn(10)!.MarkLayers == 0 && field.CommandOrbCount(1) == 0
                && !field.CommandFocusVisible,
                "前の号令が再戦へ漏れない");
            field.EndShockMarkPresentation();
            Require(field.CommandOrbCount(1) == 0, "終了で玉を片付ける");
            field.BeginBattle(opening, "庇いの直後に終了", 3);
            hisa = field.FindPawn(1)!; ally = field.FindPawn(3)!; enemy = field.FindPawn(10)!;
            hisa.AnimationSpeed = speed;
            await field.ShowHisaCover(hisa, ally, enemy, speed);
            field.EndShockMarkPresentation();
            await Wait(.3 / speed);
            Require(hisa.PresentationAlive && ally.Hp == 60 && !hisa.IsGuarding,
                "庇いの途中終了はHPを変えず帰還する");
            GD.Print($"HISA_VISUAL_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(.2);
    }
}
