using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 伝染の始点・実対象・到着時刻と、中断後の再戦を実台本まで通して確認する。
public partial class RauCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private BattlefieldView3D _field = null!;
    private bool _capture;

    public override async void _Ready()
    {
        try
        {
            _capture = OS.GetCmdlineUserArgs().Contains("--capture");
            CheckBatches();
            _field = new BattlefieldView3D();
            _field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_field);
            foreach (int team in new[] { 0, 1 })
            {
                Open(team);
                var rau = _field.FindPawn(1)!;
                var source = _field.FindPawn(3)!;
                var target = _field.FindPawn(4)!;
                var ally = _field.FindPawn(2)!;
                source.SetPoisonRemaining(12);
                _field.SetPoisonClock(false, 1);
                await _field.Attack(rau, source, AttackPattern.Single, new[] { source }, advance: false);
                Require(rau.MovementPortrait == "rau_touch", "接触攻撃の差分");
                Require(source.PoisonIconAmount == 12 && target.PoisonIconAmount == 0, "攻撃だけでは毒を足さない");
                await Capture($"{team}-touch");
                var transfer = _field.ShowPoisonTransfer(source, target, false, 1, amount: 6);
                var flow = _field.ActivePoisonFlows.Single(f => f.FlowKind == PoisonFlowFx.Kind.Touch);
                Require(flow.From == source.FxPoint && flow.To == target.FxPoint, "飛沫は殴られた敵から隣の実対象へ");
                _field.SetPoisonClock(true, 1);
                double age = flow.Age;
                await Wait(0.18);
                Require(flow.Age == age && !transfer.IsCompleted && rau.MovementPortrait == "rau_touch", "停止中は移動と差分の復帰を止める");
                _field.SetPoisonClock(false, 2);
                await Wait(0.08);
                Require(flow.Age - age > 0.10 && target.PoisonIconAmount == 0, "倍速と到着前の残量");
                await Capture($"{team}-transfer");
                await transfer;
                target.AddPoisonIconAmount(6);
                Require(target.PoisonIconAmount == 6, "到着してから一度だけ加算");
                _field.SetPoisonClock(false, 1);
                rau.RauPose(true, 1);
                var leak = _field.ShowPoisonTransfer(rau, ally, true, 1);
                await Wait(0.28);
                Require(ally.PoisonIconAmount == 0 && rau.MovementPortrait == "rau_spread", "漏れの反応と到着前の味方");
                await Capture($"{team}-leak");
                await leak;
                ally.AddPoisonIconAmount(1);
                var local = _field.ShowRauContagion(target, 40);
                Require(local.From == target.FxPoint && local.To == target.FxPoint, "死骸のIDを推定せず受け手で表す");
                await Wait(0.22);
                await Capture($"{team}-contagion");
                await local.Arrival;
                var cancelled = _field.ShowPoisonTransfer(source, target, false, 1);
                Open(team);
                await cancelled;
                Require(_field.ActivePoisonFlows.Length == 0 && _field.Pawns.Values.All(p => p.PoisonIconAmount == 0), "飛行中の再戦で古い飛沫と残量を消す");
                rau = _field.FindPawn(1)!;
                rau.RauPose(true, 1);
                _field.PlayDeath(rau);
                Require(rau.MovementPortrait is null, "死亡で反応差分を解除");
                Open(team);
                rau = _field.FindPawn(1)!;
                rau.RauPose(true, 1);
                var hidden = _field.ShowPoisonTransfer(rau, _field.FindPawn(2)!, true, 1);
                _field.Hide();
                await hidden;
                Require(rau.MovementPortrait is null && _field.ActivePoisonFlows.Length == 0, "非表示で漏れと差分を破棄");
                _field.Show();
            }
            Open(0);
            _field.FindPawn(1)!.RauPose(true, 1);
            _field.ShowVictoryPortraits();
            Require(_field.FindPawn(1)!.MovementPortrait is null, "勝利で反応差分を解除");
            _field.QueueFree();
            await Wait(0.10);
            await Replay();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GC.Collect(); GC.WaitForPendingFinalizers();
            GD.Print("RAU_CHECK_OK contact transfer leak contagion targets arrival pause speed mirrored death victory hide reset replay");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private async Task Replay()
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        object? Read(string name) => typeof(Main).GetField(name, Flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        var formation = Presets.Compare.First(x => x.Name == "毒+ベニ+ラウ").F;
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), BattleEngine.Materialize(EnemyCatalog.Stages[2].Enemy, 1), 0, 2, "ラウ検証" });
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < 1600 && (bool)Read("_playing")!; k++) await Wait(0.025);
            Require(!(bool)Read("_playing")!, "実台本の再生が完走");
            var tape = (BattleResult)Read("_result")!;
            var field = (BattlefieldView3D)Read("_battleField")!;
            int touches = tape.Events.Count(e => RauPresentation.IsTransfer(e) && e.PoisonRoute == PoisonRoute.Touch && e.Amount > 0);
            int leaks = tape.Events.Count(e => RauPresentation.IsTransfer(e) && e.PoisonRoute == PoisonRoute.TouchLeak && e.Amount > 0);
            int deaths = tape.Events.Count(e => RauPresentation.IsTransfer(e) && e.PoisonRoute == PoisonRoute.Contagion && e.Amount > 0);
            Require(touches > 0 && leaks > 0 && deaths > 0, "伝染・漏れ・疫みの陽性対照");
            Require((int)Read("_poisonSpreadPlays")! == touches && (int)Read("_poisonLeakPlays")! == leaks
                && field.RauContagionHits == deaths, "実台本の各対象を一度ずつ表示");
            Require(field.RauContacts == tape.Events.Count(e => e.Kind == BattleEventKind.Attack
                && field.FindPawn(e.ActorId)?.UnitId == "rau"), "攻撃の件数と接触差分が一致");
            foreach (var group in tape.Events.Where(e => e.TargetId is not null
                && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Death).GroupBy(e => e.TargetId))
                Require(field.FindPawn(group.Key)!.Hp == (group.Last().Kind == BattleEventKind.Death ? 0 : Math.Max(0, group.Last().HpAfter)), "最終HPは台本と一致");
            GD.Print($"RAU_REPLAY_OK pass={pass} touch={touches} leak={leaks} contagion={deaths} contacts={field.RauContacts}");
            if (pass == 0) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
        }
        main.QueueFree();
        await Wait(0.10);
    }

    private void Open(int team) => _field.BeginBattle(new DemoOpening[] {
        new(1, team, "rau", "疫みのラウ", 3, 100, 100, 5, AttackPattern.Single, false, UnitCatalog.Rau.Traits),
        new(2, team, "guza", "瘴気袋のグザ", 2, 100, 100, 2, AttackPattern.Single, false, UnitCatalog.Guza.Traits),
        new(3, 1-team, "knight", "触れた敵", 0, 100, 100, 8, AttackPattern.Single, false),
        new(4, 1-team, "axeman", "うつる敵", 2, 100, 100, 8, AttackPattern.Single, false),
    }, "ラウ：接触・伝染・漏れ", 0);

    private static void CheckBatches()
    {
        BattleEvent Gain(PoisonRoute route, int source = 8, int actor = 1) => new() {
            Turn = 1, Kind = BattleEventKind.StatusGain, Text = StatusKeys.Poison,
            PoisonRoute = route, ActorId = actor, TargetId = 9, SpreadFromId = source, Amount = 3 };
        BattleEvent[] tape = { Gain(PoisonRoute.Touch), Gain(PoisonRoute.Touch), Gain(PoisonRoute.Touch, 7),
            Gain(PoisonRoute.TouchLeak), Gain(PoisonRoute.TouchLeak, actor: 2),
            Gain(PoisonRoute.Contagion), new() { Turn = 1, Kind = BattleEventKind.Death }, Gain(PoisonRoute.Contagion) };
        Require(RauPresentation.BatchEnd(tape, 0) == 2, "同じ伝染元の連続対象だけを束ねる");
        Require(RauPresentation.BatchEnd(tape, 2) == 3 && RauPresentation.BatchEnd(tape, 3) == 4, "経路と書き手の境界");
        Require(RauPresentation.BatchEnd(tape, 5) == 6, "途中の死を跨いでまとめない");
    }

    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        if (!_capture) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string dir = ProjectSettings.GlobalizePath("res://../design/art/rau/effects-captures");
        System.IO.Directory.CreateDirectory(dir);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, name + ".png")) == Error.Ok, "画面保存");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
