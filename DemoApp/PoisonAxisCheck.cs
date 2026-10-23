using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 毒の見せ場の検証と撮影。戦闘結果を作り替えず、表示だけの固定場面も確かめる。
public partial class PoisonAxisCheck : Control
{
    private BattlefieldView3D _field = null!;
    private bool _capture;
    public override async void _Ready()
    {
        try
        {
            _capture = OS.GetCmdlineUserArgs().Contains("--capture") || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--capture-dir="));
            CheckRoutes();
            _field = new BattlefieldView3D(); _field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_field);
            Open();
            var vio = _field.FindPawn(1)!; var guza = _field.FindPawn(2)!;
            var sid = _field.FindPawn(3)!; var enemy = _field.FindPawn(4)!;
            enemy.SetPoisonRemaining(40); enemy.SetHp(30);
            Require(enemy.PoisonDoomVisible, "毒がHPを超えた敵の予告");
            enemy.SetHp(100); Require(!enemy.PoisonDoomVisible, "回復で予告を解除");
            sid.SetPoisonRemaining(100); Require(sid.PoisonMantleVisible, "衣の保持者の膜");
            guza.SetPoisonRemaining(100); Require(!guza.PoisonMantleVisible, "袋持ちを衣の保持者と混同しない");
            vio.SetAttack(50); Require(vio.VioGrowthStage == 1, "第一段階");
            vio.SetAttack(150); Require(vio.VioGrowthStage == 2, "第二段階");
            vio.SetAttack(6); Require(vio.VioGrowthStage == 0, "写しが戻れば通常姿へ");
            foreach (int n in new[] { 1, 999, 9999, 10_000, 19_775, 85_009, 9_999_999, 99_999_999, int.MaxValue })
                Require(StatusIconRow2D.FormatAmount(n).Length <= 4, "大きな状態数値の幅");
            int before = enemy.PoisonBurstWaves;
            _field.ShowTick(enemy, false, false, false, 0.5, poisonWaves: 3);
            Require(enemy.PoisonBurstWaves - before == 3, "実在する刻み3回を3波にする");
            before = guza.PoisonBurstWaves;
            await Wait(0.10);
            Require(guza.PoisonBurstWaves == before, "毒に強い駒に刻みを捏造しない");

            _field.SetPoisonClock(false, 1);
            _field.ShowGuzaVent(guza, new[] { enemy, sid }, 100, 1);
            Require(_field.PoisonVents == 1 && _field.ActivePoisonFlows.Length == 2, "噴き出しの実対象だけに流す");
            var flow = _field.ActivePoisonFlows[0];
            _field.SetPoisonClock(true, 1);
            double age = flow.Age;
            await Wait(0.12); Require(flow.Age == age, "停止中の毒煙と飛沫の時計");
            _field.SetPoisonClock(false, 2);
            await Wait(0.08); Require(flow.Age - age > 0.10, "倍速で進む毒煙と飛沫");
            _field.SetPoisonClock(false, 1);
            if (ResourceLoader.Exists(PoisonFlowFx.SmokePath)) Require(_field.PurchasedPoisonSmokes > 0, "購入毒煙を複製して利用");
            await Wait(0.7);

            enemy.SetPoisonRemaining(1200); sid.SetPoisonRemaining(4000); guza.SetPoisonRemaining(100);
            vio.SetAttack(180, animate: false); await Wait(0.15); await Capture("stock-mantle-growth");
            var vent = FxCatalog.Find("guza.vent").Events!();
            _field.ShowGuzaVent(guza, vent.Select(e => _field.FindPawn(e.TargetId)!).ToArray(), vent[0].Amount, 1);
            await Wait(0.23); await Capture("guza-vent"); await Wait(0.6);
            var drain = _field.ShowPoisonDrain(new[] { guza }, vio, 1);
            await Wait(0.30); await Capture("vio-drain"); await drain;
            _field.ShowPoisonRetort(sid, enemy, 4000, false);
            await Wait(0.18); await Capture("sid-retort"); await Wait(0.5);
            _field.ShowTick(enemy, false, false, true, 0.8, poisonWaves: 3);
            await Wait(0.16); await Capture("poison-burst"); await Wait(0.7);
            _field.ShowBloodSip(vio, sid, 4);
            await Wait(0.22); await Capture("vio-blood-light"); await Wait(0.5);
            _field.ShowGurenRelease(_field.FindPawn(6)!, new[] { enemy, _field.FindPawn(5)! }, 1, 19000);
            await Wait(0.25); await Capture("beni-guren"); await Wait(0.6);
            vio.AnimationSpeed = 1;
            var jump = _field.Attack(vio, enemy, AttackPattern.Single, new[] { enemy }, holdPosition: true);
            await Wait(0.15); await Capture("vio-pounce"); await jump;
            Require(vio.Position.DistanceTo(vio.Home) > 0.5f, "噛みつく的へ跳躍");
            var previous = vio.Position;
            var other = _field.FindPawn(5)!;
            await _field.Attack(vio, other, AttackPattern.Single, new[] { other }, advance: false, holdPosition: true);
            Require(vio.Position.DistanceTo(previous) > 0.4f && _field.PoisonBites == 2, "連撃の二発目は次の実対象へ跳躍");
            _field.PlayDeath(vio); Require(!vio.PoisonDoomVisible && !vio.PoisonMantleVisible, "死亡で常設演出を解除");
            Open(); Require(_field.ActivePoisonFlows.Length == 0 && _field.PoisonBites == 0, "再戦で飛沫と集計を破棄");
            var cancelledDrain = _field.ShowPoisonDrain(new[] { _field.FindPawn(2)! }, _field.FindPawn(1)!, 1);
            Open(); await cancelledDrain;
            var cancelledJump = _field.FindPawn(1)!.PoisonPounce(_field.FindPawn(4)!.Home);
            _field.FindPawn(3)!.SetPoisonRemaining(10);
            _field.Hide();
            Require(!await cancelledJump, "中断した跳躍から着弾へ進まない");
            Require(!_field.FindPawn(3)!.PoisonMantleVisible, "中断後に常設の膜が復活しない");
            _field.Show(); Open();
            var resetJump = _field.FindPawn(1)!.PoisonPounce(_field.FindPawn(4)!.Home);
            Open(); Require(!await resetJump, "再戦で古い跳躍の待ち手を解放する");
            _field.FindPawn(1)!.SetAttack(150);
            _field.FindPawn(1)!.AnimateVictory();
            Require(!_field.FindPawn(1)!.PoisonMantleVisible, "勝利時に常設演出を解除");
            await Wait(0.55);
            _field.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GC.Collect(); GC.WaitForPendingFinalizers();
            GD.Print("POISON_AXIS_CHECK_OK routes growth mantle doom tick-waves pause speed targets reset death victory large-counts");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private void Open() => _field.BeginBattle(FxCatalog.GetOpenings("guza.vent"), "毒の見せ場", 0);
    private static void CheckRoutes()
    {
        BattleEvent[] script = {
            new() { Turn=1, Kind=BattleEventKind.Attack, ActorId=8, TargetId=2 },
            new() { Turn=1, Kind=BattleEventKind.StatusGain, ActorId=7, TargetId=2, Text=StatusKeys.Poison, PoisonRoute=PoisonRoute.Backlash },
            new() { Turn=2, Kind=BattleEventKind.TurnStart },
            new() { Turn=2, Kind=BattleEventKind.StatusGain, ActorId=7, TargetId=2, Text=StatusKeys.Poison, PoisonRoute=PoisonRoute.Backlash },
        };
        Require(PoisonAxisPresentation.BacklashSource(script,1)==8, "返り毒は書き手の袋持ちでなく殴った敵から");
        Require(PoisonAxisPresentation.BacklashSource(script,3) is null, "前ターンの攻撃を拾わない");
        GD.Print("POISON_AXIS_ROUTES_OK");
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        if (!_capture) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        string dir = arg is null ? ProjectSettings.GlobalizePath("res://../design/art/poison-axis-v1/captures") : arg[14..];
        System.IO.Directory.CreateDirectory(dir);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir,name+".png")) == Error.Ok, "画面保存");
    }
    private static void Require(bool pass,string message) { if(!pass) throw new Exception(message); }
}
