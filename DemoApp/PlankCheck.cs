using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 実台本を Main の入口へ通す。表示用の台であり、カタログや戦闘規則は変更しない。
public partial class PlankCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            CheckGrouping();
            CheckAudio();
            if (!OS.GetCmdlineUserArgs().Contains("--preview-only")) await Replay();
            await Preview();
            GD.Print("PLANK_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
    private static void CheckGrouping()
    {
        BattleEvent Shot(int turn, int slot, int target) => new()
        { Kind = BattleEventKind.Plank, Text = PlankLabels.Reflect, Turn = turn, Slot = slot, ActorId = 1, TargetId = target };
        var tape = new[] { Shot(1, 4, 8), Shot(1, 4, 8), Shot(1, 5, 8), Shot(1, 0, 8),
            Shot(1, 0, 8), Shot(2, 4, 8), Shot(1, 4, 9) };
        var plan = new PlankPresentation(tape);
        Require(plan.Volleys.Count == 6 && plan.Volleys[0].SequenceEqual(new[] { 0, 1 }), "通し番号・ターン・対象で束ね、0は単発");
    }
    private void CheckAudio()
    {
        var audio = new BattleAttackAudio();
        AddChild(audio);
        audio.PlayPlankRush();
        audio.PlayPlankPaste();
        audio.PlayPlankPaste();
        audio.PlayPlankSkillUp();
        var accents = audio.GetChildren().OfType<AudioStreamPlayer>()
            .Where(p => p.VolumeDb == -3).ToArray();
        Require(accents.Length == 3 && accents.All(p => p.Stream is not null && p.Stream.GetLength() > 0 && p.Playing),
            "ツギの3音源が読み込まれ再生される");
        for (int i = 0; i < 8; i++) audio.PlayPlankReflect();
        Require(accents.All(p => p.Playing && p.VolumeDb == -3), "連続攻撃で補助演出音を打ち切らない");
        audio.StopAll();
        Require(accents.All(p => !p.Playing), "戦闘終了時は専用SEも止める");
        audio.QueueFree();
        GD.Print("PLANK_AUDIO_OK voices=3 volume=-3dB");
    }
    private async Task Replay()
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        object? Read(string name) => typeof(Main).GetField(name, Flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 100.0);
        var players = BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Golm,
            center: UnitCatalog.Tsugi, back1: UnitCatalog.Hiyo, back3: UnitCatalog.Sero), 0);
        var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[4].Enemy, 1);
        foreach (var pawn in players)
        {
            pawn.SetCounter(StatusKeys.Armor, 80);
            pawn.SetCounter(StatusKeys.Plank, PlankTrait.Plain | PlankTrait.Rebound | PlankTrait.Scorch | PlankTrait.Thick);
        }
        players[0].SetCounter(StatusKeys.Burn, 2);
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] { players, enemies, 7, 4, "" });
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < 2000 && (bool)Read("_playing")!; k++) await Wait(0.01);
            Require(!(bool)Read("_playing")!, "実台本が完走する");
            var result = (BattleResult)Read("_result")!;
            var field = (BattlefieldView3D)Read("_battleField")!;
            var plan = new PlankPresentation(result.Events);
            int reflections = result.Events.Count(e => e.Kind == BattleEventKind.Plank && e.Text == PlankLabels.Reflect);
            Require(reflections > 0, "反射の陽性対照");
            Require(plan.DamageToReflect.Count == reflections, "各反射の直後の打点を紐づける");
            Require(plan.DamageToReflect.Keys.Any(i => result.Events[i].HpAfter <= 0), "反射で倒す陽性対照");
            Require(plan.Volleys.Values.Any(v => v.Count > 1), "複数反射の陽性対照");
            Require((int)Read("_plankFlights")! == reflections, "全反射を一度ずつ飛ばす");
            Require((int)Read("_plankVolleys")! == plan.Volleys.Count, "同じ攻撃を一拍にまとめる");
            Require((int)Read("_plankPastes")! == result.Events.Count(e => e.Kind == BattleEventKind.Plank && e.Text == PlankLabels.Paste), "貼る回数が一致");
            Require((int)Read("_plankScraps")! == result.Events.Count(e => e.Kind == BattleEventKind.Plank && e.Text == PlankLabels.Scrap), "拾う回数が一致");
            int aid = result.Events.Count(e => e.Kind == BattleEventKind.Plank && e.Text == PlankLabels.FirstAid);
            int skill = result.Events.Count(e => e.Kind == BattleEventKind.Plank && e.Text == PlankLabels.Skill);
            Require((int)Read("_plankFirstAids")! == aid, "応急処置を一度ずつ表示");
            Require((int)Read("_plankSkillUps")! == skill && skill > 0, "腕上昇を一度ずつ表示・陽性対照");
            GD.Print($"PLANK_AID_CASES pass={pass} aid={aid} skill={skill}");
            foreach (var group in result.Events.Where(e => e.TargetId is not null && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal).GroupBy(e => e.TargetId))
                Require(field.FindPawn(group.Key)!.Hp == Math.Max(0, group.Last().HpAfter), "再生の最終HPが台本と一致");
            GD.Print($"PLANK_REPLAY_OK pass={pass} shots={reflections} volleys={plan.Volleys.Count} max={plan.Volleys.Values.Max(v => v.Count)} paste={Read("_plankPastes")} scrap={Read("_plankScraps")}");
            if (pass == 0) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
        }
        var wounded = BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Golm,
            center: UnitCatalog.Tsugi, back1: UnitCatalog.Hiyo, back3: UnitCatalog.Sero), 0);
        foreach (var pawn in wounded) pawn.Hp = Math.Max(1, pawn.MaxHp * 3 / 10);
        var firstWave = BattleEngine.Materialize(EnemyCatalog.Stages[0].Enemy, 1);
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] { wounded, firstWave, 7, 0, "" });
        for (int k = 0; k < 2000 && (bool)Read("_playing")!; k++) await Wait(0.01);
        Require(!(bool)Read("_playing")!, "応急処置の実台本が完走");
        var aidTape = (BattleResult)Read("_result")!;
        var aids = aidTape.Events.Where(e => e.Kind == BattleEventKind.Plank && e.Text == PlankLabels.FirstAid).ToArray();
        Require(aids.Length > 0 && (int)Read("_plankFirstAids")! == aids.Length, "応急処置の陽性対照と表示回数");
        var aidField = (BattlefieldView3D)Read("_battleField")!;
        foreach (var group in aidTape.Events.Where(e => e.TargetId is not null && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal).GroupBy(e => e.TargetId))
            Require(aidField.FindPawn(group.Key)!.Hp == Math.Max(0, group.Last().HpAfter), "応急処置後もHPは台本に一致");
        GD.Print($"PLANK_FIRST_AID_REPLAY_OK aid={aids.Length}");
        // 実在する応急処置を単独でも通し、回復していないことと帰還を確認する。
        aidField.BeginBattle((List<DemoOpening>)Read("_battleOpening")!, "", 0);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, false);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 2.0);
        var aidEvent = aids[0];
        var worker = aidField.FindPawn(aidEvent.ActorId)!;
        var patient = aidField.FindPawn(aidEvent.TargetId)!;
        worker.AnimationSpeed = 2;
        var home = worker.Position;
        patient.SetHp(aidEvent.HpAfter);
        await (Task<bool>)typeof(Main).GetMethod("PlayPlank", Flags)!.Invoke(main,
            new object?[] { aidEvent, -1, worker, patient })!;
        Require(patient.Hp == aidEvent.HpAfter && patient.PlankPieceCount > 0, "応急処置はHPを戻さず板を貼る");
        Require(worker.Position.DistanceTo(home) < 0.01f && !worker.PlankAidActive, "実入口からも帰還する");
        main.QueueFree();
        await Wait(0.05);
    }
    private async Task Preview()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        bool verify = OS.GetCmdlineUserArgs().Contains("--verify");
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            field.BeginBattle(new DemoOpening[] {
                new(1, team, "tsugi", "ツギ", 3, 62, 62, 9, AttackPattern.Single, false),
                new(2, team, "gald", "ガルド", 0, 100, 100, 10, AttackPattern.Single, true),
                new(3, team, "sasa", "ササ", 1, 100, 100, 10, AttackPattern.Single, true),
                new(4, team, "hiyo", "ヒヨ", 4, 100, 100, 10, AttackPattern.Single, false),
                new(5, 1-team, "knight", "敵", 0, 100, 100, 10, AttackPattern.All, true)
            }, "", 0);
            await Wait(0.4); // 初回レイアウトと配置Tweenが完了してから距離を測る。
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            var tsugi = field.FindPawn(1)!; var enemy = field.FindPawn(5)!;
            var holders = new[] { field.FindPawn(2)!, field.FindPawn(3)!, field.FindPawn(4)! };
            var origin = tsugi.Position;
            tsugi.RushToPlank(holders[0]);
            await Wait(0.12 / speed);
            Require(tsugi.Position.DistanceTo(holders[0].Position) < origin.DistanceTo(holders[0].Position), "応急処置で対象へ接近");
            for (int hit = 0; hit < 2; hit++)
            {
                tsugi.HammerPlank();
                field.PlankImpact(holders[0], 20, speed);
                field.PlayPlankPasteSound();
                await Wait(0.04 / speed);
                if (!verify && hit == 0) await Capture($"aid-{team}-{speed}");
                await Wait(0.045 / speed);
            }
            tsugi.ReturnFromPlank();
            await Wait(0.12 / speed + 0.15);
            Require(tsugi.Position.DistanceTo(origin) < 0.01f && !tsugi.PlankAidActive, "処置後に元の位置へ戻る");
            tsugi.RushToPlank(tsugi);
            Require(tsugi.Position.IsEqualApprox(origin), "自分への処置では移動しない");
            tsugi.ReturnFromPlank();
            tsugi.RaisePlankCraft(2);
            tsugi.RaisePlankCraft(1);
            Require(tsugi.PlankCraftTier == 2, "腕の段は下がらない");
            field.PlankCraftSpark(tsugi, 2, speed);
            int depletionSounds = 0;
            holders[0].ArmorDepleted += () => depletionSounds++;
            holders[0].ObserveArmor(20);
            holders[0].ObserveArmor(10);
            Require(depletionSounds == 0, "破片が残っていれば割れる音を出さない");
            holders[0].ObserveArmor(0);
            holders[0].ObserveArmor(0);
            Require(depletionSounds == 1, "破片切れは一度だけ通知");
            holders[0].ObserveArmor(10);
            foreach (var p in holders) p.SetPlank(70);
            Require(holders.All(p => p.PlankPieceCount == 5 && p.HasStatusIcon(StatusKeys.Plank)), "板の厚さと印");
            holders[0].SetPlank(10);
            Require(holders[0].PlankPieceCount == 1, "板の量で枚数が変わる");
            tsugi.SetScrapStock(40);
            await Wait(verify ? 0.01 : 0.5);
            foreach (var p in holders)
            {
                field.PlankReflect(p.FxPoint, enemy.FxPoint, 35, speed);
            }
            await Wait(0.12 / speed);
            if (!verify) await Capture($"shield-{team}-{speed}");
            await Wait(0.12 / speed);
            if (!verify) await Capture($"gather-{team}-{speed}");
            await Wait(0.15 / speed);
            if (!verify) await Capture($"volley-{team}-{speed}");
            await Wait(0.11 / speed);
            field.PlankReflectImpact(enemy, 105, speed);
            holders[0].SetBurning(true);
            field.PlankImpact(holders[0], 40, speed, true);
            await Wait(verify ? 0.01 : 0.25);
            if (!verify) await Capture($"burn-{team}-{speed}");
            enemy.AnimateDeath(); enemy.PlankKnockout();
            holders[0].AnimateDeath();
            Require(depletionSounds == 1, "死亡の掃除では割れる音を出さない");
            Require(holders[0].PlankPieceCount == 0, "死亡時に板を消す");
            holders[1].BeginStatusSnapshot(); holders[1].CommitStatusSnapshot();
            Require(holders[1].PlankPieceCount == 0, "空のスナップショットで板を消す");
            await Wait(verify ? 0.01 : 0.5);
        }
    }
    private async Task Capture(string name)
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(arg[14..] + "/" + name + ".png") == Error.Ok, "画像保存");
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
