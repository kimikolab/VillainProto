using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 実台本の件数・再戦・死亡順と、1倍/2倍・左右の観察を同じ演出口で確かめる。
public partial class SpecialsCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--verify")) await VerifyReplay();
            else await Preview();
            GD.Print("SPECIALS_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task VerifyReplay()
    {
        var poison = Formation.Build(front1: UnitCatalog.Sid, front3: UnitCatalog.Rau,
            center: UnitCatalog.Beni, back1: UnitCatalog.Mio, back3: UnitCatalog.Kata);
        await Replay(poison, EnemyCatalog.Stages[0].Enemy, 0, false);
        // 実際の代表編成から相打ち勝ちを探す。演出専用の偽イベントは作らない。
        foreach (var entry in Presets.Compare.Where(p => Enumerable.Range(0, 5).Any(i => p.F[i]?.Id == "gald")))
        for (int stage = 1; stage < EnemyCatalog.Stages.Count; stage++)
        for (int seed = 0; seed < 40; seed++)
        {
            var result = BattleEngine.Run(BattleEngine.Materialize(entry.F, 0),
                BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1), seed, verbose: true);
            if (!result.Events.Any(e => e.Kind == BattleEventKind.LastStandVictory)) continue;
            GD.Print($"SPECIALS_MUTUAL_FIXTURE {entry.Name} stage={stage} seed={seed}");
            await Replay(entry.F, EnemyCatalog.Stages[stage].Enemy, seed, true);
            return;
        }
        throw new InvalidOperationException("相打ち勝ちの陽性対照が見つからない");
    }

    private async Task Replay(Formation formation, Formation foes, int seed, bool mutual)
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        object? Read(string name) => typeof(Main).GetField(name, Flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), BattleEngine.Materialize(foes, 1), seed, 0, "" });
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < 600 && (bool)Read("_playing")!; k++) await Wait(0.05);
            Require(!(bool)Read("_playing")!, "再生が完走する");
            var result = (BattleResult)Read("_result")!;
            var field = (BattlefieldView3D)Read("_battleField")!;
            int Count(BattleEventKind kind) => result.Events.Count(e => e.Kind == kind);
            Require(field.GurenGains == Count(BattleEventKind.GurenGain), "紅蓮の加算が台本と同数");
            Require(field.GurenReleases == result.Events.Count(e => e.Kind == BattleEventKind.GurenRelease && e.TargetId is null), "奔流が一度ずつ");
            Require(field.SwordDraws == Count(BattleEventKind.LastStand), "抜剣が台本と同数");
            Require(field.SwordRipostes == Count(BattleEventKind.LastStandRiposte), "斬り返しが台本と同数");
            Require(field.NumbSwings == result.Events.Count(e => e.Kind == BattleEventKind.Attack && e.NumbPercent > 0), "鈍りが台本と同数");
            Require(field.Spews == result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.PoisonRoute == PoisonRoute.Spew), "吐く経路が台本と同数");
            Require(field.VenomReturns == result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.PoisonRoute == PoisonRoute.Venom), "返り毒が台本と同数");
            if (!mutual) Require(field.GurenReleases > 0 && field.NumbSwings > 0 && field.VenomReturns > 0 && field.Spews > 0, "毒系の陽性対照");
            else
            {
                var last = result.Events.Single(e => e.Kind == BattleEventKind.LastStandVictory);
                var pawn = field.FindPawn(last.ActorId)!;
                Require(pawn.Hp == 0 && pawn.QuietLastStand && pawn.SwordDrawn, "相打ちの終端が膝つきで残る");
                field.ShowVictoryPortraits();
                Require(pawn.QuietLastStand, "勝利演出でも最終姿勢を保つ");
            }
            foreach (var group in result.Events.Where(e => e.TargetId is not null && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal).GroupBy(e => e.TargetId))
                Require(field.FindPawn(group.Key)!.Hp == Math.Max(0, group.Last().HpAfter), "最終HPは台本と一致");
            GD.Print($"SPECIALS_REPLAY_OK mutual={mutual} pass={pass} guren={field.GurenReleases} numb={field.NumbSwings} sword={field.SwordDraws} riposte={field.SwordRipostes}");
            if (pass == 0) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
        }
        main.QueueFree();
        await Wait(0.2);
    }

    private async Task Preview()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            field.BeginBattle(new DemoOpening[] {
                new(1, team, "beni", "ベニ", 0, 64, 64, 4, AttackPattern.Single, false),
                new(2, team, "sid", "スィド", 2, 100, 100, 0, AttackPattern.Single, false),
                new(3, team, "gald", "ガルド", 4, 100, 100, 10, AttackPattern.Sweep, true),
                new(4, 1-team, EnemyCatalog.Knight.Id, "敵", 0, 100, 100, 10, AttackPattern.Single, true),
                new(5, 1-team, EnemyCatalog.Knight.Id, "敵", 4, 100, 100, 10, AttackPattern.Single, true),
            }, "", 0);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            var beni = field.FindPawn(1)!; var sid = field.FindPawn(2)!; var gald = field.FindPawn(3)!;
            var enemy = field.FindPawn(4)!; var second = field.FindPawn(5)!;
            await Wait(0.4);
            field.ShowGurenGain(sid, beni, 7, speed);
            await Wait(0.3 / speed);
            field.ShowGurenGain(sid, beni, 18, speed);
            await Capture($"{team}-{speed}-flower");
            await Wait(0.3 / speed);
            field.ShowGurenRelease(beni, [enemy, second], speed);
            await Wait(0.3 / speed);
            await Capture($"{team}-{speed}-guren");
            enemy.SetBurning(true); second.SetBurning(true); enemy.SetPoisoned(true); second.SetPoisoned(true);
            enemy.FlashGurenImpact(); second.FlashGurenImpact();
            await Wait(0.16 / speed);
            await Capture($"{team}-{speed}-impact");
            await Wait(0.3 / speed);
            beni.SetGurenRelease(false);
            field.ShowVenom(sid, enemy, false, speed);
            await Wait(0.4 / speed);
            field.ShowVenom(sid, enemy, true, speed);
            enemy.SetStatusIcon(StatusKeys.Numbed, true);
            await Wait(0.3 / speed);
            var attack = field.Attack(enemy, sid, AttackPattern.Single, [sid], numbPercent: 60);
            await Wait(0.30 / speed);
            await Capture($"{team}-{speed}-numb");
            await attack;
            await field.ShowSwordDraw(gald, 18, speed);
            await Wait(0.2 / speed);
            await Capture($"{team}-{speed}-sword");
            field.ShowSwordSlash(gald, [enemy, second], speed);
            await Wait(0.3 / speed);
            field.Parry(enemy, gald);
            await Wait(0.3 / speed);
            field.ShowSwordSlash(gald, [enemy], speed, true);
            await Wait(0.2 / speed);
            field.PlayDeath(enemy); field.PlayDeath(second);
            await Wait(0.5 / speed);
            gald.QuietLastStand = true;
            field.PlayDeath(gald);
            field.ShowVictoryPortraits();
            await Wait(0.5);
            await Capture($"{team}-{speed}-kneel");
        }
    }

    private async Task Capture(string phase)
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(arg[14..] + "/" + phase + ".png") == Error.Ok, "画像保存");
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
