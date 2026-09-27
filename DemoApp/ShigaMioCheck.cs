using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 観察: Godot_console.exe --path DemoApp res://ShigaMioCheck.tscn
// 台本との照合と再戦: 上記に --headless と -- --verify を付ける。
public partial class ShigaMioCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            CheckMedia();
            if (OS.GetCmdlineUserArgs().Contains("--verify")) await Verify();
            else await Preview();
            GD.Print("SHIGA_MIO_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void CheckMedia()
    {
        var audio = new BattleAttackAudio();
        AddChild(audio);
        foreach (string path in BattleAttackAudio.MireSounds)
        {
            var stream = (AudioStream)typeof(BattleAttackAudio).GetMethod("LoadSound", Flags)!.Invoke(audio, new object[] { path })!;
            Require(stream is not null && stream.GetLength() > 0, $"音源の読込: {path}");
            GD.Print($"MIRE_SOUND_OK path={path} seconds={stream!.GetLength():F3}");
        }
        var texture = UiKit.LoadTexture(BattlefieldView3D.MireSplashPath);
        Require(texture.GetWidth() > 0 && texture.GetHeight() > 0, "飛沫素材の読込");
        audio.PlayMireSlam(); audio.PlayMireBurst(false); audio.PlayMireBurst(true);
        audio.PlayMireConduct(); audio.PlayMireFlow();
        audio.StopAll();
        Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(v => !v.Playing), "再戦・中断で音を止める");
        audio.QueueFree();
    }

    private async Task Verify()
    {
        int slams = 0, conducts = 0, flows = 0, bursts = 0, wires = 0;
        var formation = Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Beni,
            center: UnitCatalog.Kata, back1: UnitCatalog.Mio, back3: UnitCatalog.Guza);
        for (int fixture = 0; fixture < 3; fixture++)
        {
            int stage = fixture == 0 ? 0 : 2, seed = 0;
            var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
            object? Read(string n) => typeof(Main).GetField(n, Flags)!.GetValue(main);
            typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
            typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
            var players = BattleEngine.Materialize(fixture == 2 ? Formation.Build(back1: UnitCatalog.Mio) : formation, 0);
            var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1);
            // 通電専用の初期状態。毒の刻みで先に放電しないよう、傷と感電から実エンジンで台本を作る。
            if (fixture == 2)
                foreach (var enemy in enemies) { enemy.SetCounter(StatusKeys.Shock, 1); enemy.SetCounter(StatusKeys.Wound, 1); }
            typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] { players, enemies, seed, stage, "" });
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < 1200 && (bool)Read("_playing")!; i++) await Wait(0.025);
                Require(!(bool)Read("_playing")!, "再生の完走");
                var r = (BattleResult)Read("_result")!; var f = (BattlefieldView3D)Read("_battleField")!;
                int Count(BattleEventKind k) => r.Events.Count(e => e.Kind == k);
                Require(f.MireSlams == Count(BattleEventKind.MireSlam), "叩きつけの件数");
                Require(f.MireConducts == Count(BattleEventKind.MireConduct), "通電の件数");
                Require(f.DischargePlays == Count(BattleEventKind.Discharge), "澱みの割り込み後も放電を一度ずつ");
                Require(f.MireFlows == Count(BattleEventKind.MireCarried) + Count(BattleEventKind.MireHandedOff), "移動の件数");
                Require(f.MireBursts == r.Events.Where(e => e.Kind == BattleEventKind.MireBurst).Select(e => (e.Turn, e.SpreadFromId)).Distinct().Count(), "爆発は出どころごとに一回");
                Require(f.ConcentratePlays == Count(BattleEventKind.ConcentrateMark), "印を二重に描かない");
                Require(f.WhipSweeps == r.Events.Count(e => e.Kind == BattleEventKind.Attack && e.Pattern == AttackPattern.Sweep && f.FindPawn(e.ActorId)?.UnitId == "shiga"), "鞭は一振りずつ");
                Require(f.ElectricWhipSweeps == Count(BattleEventKind.LiveWire), "感電を持つ一振りだけに雷が乗る");
                foreach (var group in r.Events.Where(e => e.Kind is BattleEventKind.Damage or BattleEventKind.Heal).GroupBy(e => e.TargetId))
                    Require(f.FindPawn(group.Key)!.Hp == Math.Max(0, group.Last().HpAfter), "HPは台本の終端と一致");
                slams += f.MireSlams; conducts += f.MireConducts; flows += f.MireFlows; bursts += f.MireBursts; wires += Count(BattleEventKind.LiveWire);
                GD.Print($"SHIGA_MIO_REPLAY fixture={fixture} stage={stage} seed={seed} pass={pass} slam={f.MireSlams} conduct={f.MireConducts} flow={f.MireFlows} burst={f.MireBursts} whip={f.WhipSweeps} wire={Count(BattleEventKind.LiveWire)}");
                if (pass == 0) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
            }
            main.QueueFree(); await Wait(0.1);
        }
        Require(slams > 0 && conducts > 0 && flows > 0 && bursts > 0 && wires > 0, "各演出の陽性対照");
        Require(BattlefieldView3D.MireBurstSize(100) > BattlefieldView3D.MireBurstSize(5), "後半の爆発が大きい");
    }

    private async Task Preview()
    {
        var f = new BattlefieldView3D(); f.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(f);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            f.BeginBattle(new DemoOpening[] {
                new(1, team, "shiga", "シガ", 0, 100, 100, 10, AttackPattern.Sweep, false),
                new(2, team, "mio", "ミオ", 4, 100, 100, 2, AttackPattern.Single, false),
                new(3, 1-team, EnemyCatalog.Knight.Id, "敵", 0, 100, 100, 10, AttackPattern.Single, false),
                new(4, 1-team, EnemyCatalog.Knight.Id, "敵", 3, 100, 100, 10, AttackPattern.Single, false),
                new(5, 1-team, EnemyCatalog.Knight.Id, "敵", 2, 100, 100, 10, AttackPattern.Single, false)
            }, "", 0);
            foreach (var p in f.Pawns.Values) p.AnimationSpeed = speed;
            var shiga = f.FindPawn(1)!; var mio = f.FindPawn(2)!; var a = f.FindPawn(3)!; var b = f.FindPawn(4)!; var c = f.FindPawn(5)!;
            await Wait(0.4);
            foreach (bool electric in new[] { false, true })
            {
                shiga.SetShocked(electric);
                var swing = f.ShowWhipSweep(shiga, new[] { a,c,b }, p => { p.AnimateHit(); f.ShowWhipFlash(p, 28, speed); });
                await Wait(0.4 / speed); await Capture($"{team}-{speed}-whip-{electric}"); await swing;
            }
            foreach (bool electric in new[] { false, true })
            {
                f.ShowMireSlam(mio, a, electric, speed);
                await Wait(0.35 / speed); await Capture($"{team}-{speed}-slam-{electric}"); await Wait(0.3 / speed);
            }
            f.ShowMireConduct(a, b, speed); await Wait(0.10 / speed); await Capture($"{team}-{speed}-conduct"); await Wait(0.4 / speed);
            f.ShowMireFlow(a,b,true,3,speed); await Wait(0.12 / speed); await Capture($"{team}-{speed}-carried"); await Wait(0.4 / speed);
            f.PlayDeath(a); await Wait(0.3 / speed);
            foreach (int amount in new[] { 5, 100 })
            {
                f.ShowMireBurst(a, new[] { new BattleEvent { Turn = 1, Kind = BattleEventKind.MireBurst, TargetId = 4, Amount = amount, Slot = 1, BrittleExtra = 5 }, new BattleEvent { Turn = 1, Kind = BattleEventKind.MireBurst, TargetId = 5, Amount = amount, Slot = 1 } }, speed);
                await Wait(0.25 / speed); await Capture($"{team}-{speed}-burst-{amount}"); await Wait(0.5 / speed);
            }
            f.ShowMireFlow(a,b,false,5,speed); await Wait(0.15 / speed); await Capture($"{team}-{speed}-handoff"); await Wait(0.5 / speed);
            if (OS.GetCmdlineUserArgs().Contains("--once")) return;
        }
    }
    private async Task Capture(string phase)
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir=")); if (arg is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(arg[14..] + "/" + phase + ".png") == Error.Ok, "画像保存");
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
