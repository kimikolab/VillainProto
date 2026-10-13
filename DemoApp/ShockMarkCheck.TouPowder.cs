using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckTouPowderVisuals()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int side in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening[] opening = [
                new(1, side, "tou", "トウ", 2, 100, 100, 10, AttackPattern.Single, false),
                new(2, side, "kata", "カタ", 0, 100, 100, 10, AttackPattern.Single, false),
                new(3, side, "kugu", "クグ", 1, 100, 100, 10, AttackPattern.Single, false),
                new(4, 1 - side, EnemyCatalog.BossRegular.Id, "勇者", 2, 500, 500, 10, AttackPattern.Single, false),
                new(5, 1 - side, "levy", "隣の敵", 0, 100, 100, 10, AttackPattern.Single, false),
            ];
            field.BeginBattle(opening, "トウの粉：濃さ・返す・粉焼・伝染", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            field.SetTouPowderClock(false, speed);
            var kata = field.FindPawn(2)!; var kugu = field.FindPawn(3)!; var hero = field.FindPawn(4)!;
            BattleEvent Cue(string text, int actor = 1, int target = 4, int amount = 3, int slot = 0, int? partner = null)
                => new() { Turn = 1, Kind = BattleEventKind.ShockGauge, Text = text, ActorId = actor, TargetId = target,
                    Amount = amount, Slot = slot, PartnerId = partner };
            field.ShowTouPowder(Cue(ShockGaugeLabels.PowderMark), speed);
            field.ShowTouPowder(Cue(ShockGaugeLabels.Dense, target: 2, amount: 6), speed);
            field.ShowTouPowder(Cue(ShockGaugeLabels.Dense, amount: 90), speed);
            await Wait(.28 / speed);
            Require(hero.PowderMarked && hero.PowderDensity == 90, "濃さは見た目の上限で丸めない");
            await Capture($"tou-density-side{side}-speed{speed}");
            field.LiftThunderPowder(kata, speed);
            var lift = field.PowderEffects.Single();
            field.SetTouPowderClock(true, speed);
            float stopped = lift.Age;
            await Wait(.12);
            Require(lift.Age == stopped, "アプリの一時停止で吸い上げも停止");
            field.SetTouPowderClock(false, speed * 2);
            lift._Process(.02 / speed);
            Require(lift.Age > stopped + .03f, "途中の倍速変更で粉の時計も速くなる");
            await Wait(.16 / speed);
            field.StrikeThunder(null, hero, 1, 0, speed, kata, powderReturn: true);
            field.ShowTouPowder(Cue(ShockGaugeLabels.DenseMove, actor: 2, amount: 96, slot: 6), speed);
            Require(kata.PowderDensity == 0 && hero.PowderDensity == 96, "返したカタの濃さだけが0になる");
            field.SetBinding(kugu, hero, true);
            var flow = Cue(ShockGaugeLabels.DenseFlow, actor: 3, amount: 97, slot: 96, partner: 1);
            field.ShowTouPowder(flow, speed);
            await Wait(.16 / speed); await Capture($"tou-thread-side{side}-speed{speed}");
            await Wait(.18 / speed); field.FinishPowderFlow(flow);
            Require(kugu.PowderDensity == 0 && hero.PowderDensity == 97, "糸の到着で敵に濃さが残りクグには残らない");
            hero.SetShocked(false);
            field.ShowTouPowder(Cue(ShockGaugeLabels.DensePop, amount: 97), speed);
            Require(hero.PowderDensity == 97 && hero.PowderMarked, "帯電の弾けで濃さと粉の印を消さない");
            await Wait(.25 / speed);
            field.UsePurchasedPowderFx = side == 0;
            field.ShowTouPowder(Cue(ShockGaugeLabels.DenseBurn, actor: 4, amount: 776, slot: 97), speed);
            var burn = field.PowderEffects.Single();
            Require(burn.UsesPurchasedSparks == (side == 0 && ResourceLoader.Exists(TouPowderFx3D.SparkPath)),
                "購入火花の採用と素材なしの代替描画");
            burn._Process(.08 / speed);
            field.SetTouPowderClock(true, speed);
            float bodyStopped = hero.PowderBurnBodyHeat;
            Require(bodyStopped > .1f, "粉焼は本人の立ち絵を内側から光らせる");
            await Capture($"tou-burn-side{side}-speed{speed}");
            float burnStopped = burn.Age;
            await Wait(.10); Require(burn.Age == burnStopped, "粉焼も一時停止で止まる");
            Require(hero.PowderBurnBodyHeat == bodyStopped, "一時停止で本人の光も止まる");
            field.SetTouPowderClock(false, speed);
            await Wait(.35 / speed);
            field.ShowTouPowder(Cue(ShockGaugeLabels.Contagion, actor: 4, target: 5, amount: 1), speed);
            await Wait(.08 / speed); await Capture($"tou-contagion-side{side}-speed{speed}");
            field.ShowTouPowder(Cue(ShockGaugeLabels.DenseBurn, actor: 4, amount: 776, slot: 97), speed);
            hero.AnimateDeath();
            Require(hero.PowderBurnBodyHeat == 0, "死亡時に本人の点火を消す");
            Require(hero.PowderDensity == 0 && !hero.PowderMarked, "死亡で常駐粉を消去");
            kugu.SetPowderDensity(2);
            field.ShowTouPowder(Cue(ShockGaugeLabels.DenseFlow, actor: 3, target: 5, amount: 1, partner: 1), speed);
            Require(kugu.PowderDensity == 2, "新しい粉を糸へ流しても以前の濃さを勝手に消さない");
            var pending = field.PowderArrival;
            field.SetTouPowderClock(true, speed);
            await Wait(.05);
            Require(!pending.IsCompleted, "停止中は糸への到着も待つ");
            field.Hide();
            Require(pending.IsCompleted && !await pending, "離脱で待ち手をキャンセルして古い戦闘へ到着を通知しない");
            Require(field.ActivePowderEffects == 0 && field.Pawns.Values.All(p => p.PowderDensity == 0 && !p.PowderMarked), "離脱で粉を残さない");
            field.Show(); field.BeginBattle(opening, "粉・再戦", 0);
            Require(field.PowderDensePlays == 0 && field.ActivePowderEffects == 0, "再戦時に粉の演出を初期化");
            GD.Print($"TOU_POWDER_VISUAL_OK side={side} speed={speed}");
        }
        field.QueueFree(); await Wait(.2);
    }

    private async Task ReplayTouPowder(string name, int stage, int seed)
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
        object? Read(string key) => typeof(Main).GetField(key, Flags)!.GetValue(main);
        bool dustLive = OS.GetCmdlineUserArgs().Contains("--dust-live");
        bool live = dustLive || OS.GetCmdlineUserArgs().Contains("--powder-live");
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, !live);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, live ? 4.0 : 1000.0);
        var formation = Presets.Playtest.Concat(Presets.Versions).Single(p => p.Name == name).F;
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0),
            BattleEngine.MaterializeEnemy(EnemyCatalog.PlaytestStages[stage].Enemy, EnemyCatalog.PlaytestStages[stage].Scale),
            seed, stage, name });
        var result = (BattleResult)Read("_result")!;
        var field = (BattlefieldView3D)Read("_battleField")!;
        bool adjacent = name.EndsWith("隣あり") || name == "試遊・感電 ポンの台";
        if (stage == 0)
        {
            if (adjacent) Require(result.Events.Any(e => e.Text == ShockGaugeLabels.DenseBurn), "隣あり・単体ボスで粉焼の陽性対照");
            Require(result.Events.Any(e => e.Text == ShockGaugeLabels.DenseMove) == adjacent, "ボス台は隣ありだけでカタが粉を返す");
        }
        if (!adjacent) Require(!result.Events.Any(e => e.Text is ShockGaugeLabels.DenseMove or ShockGaugeLabels.DenseFlow),
            "隣なしの台に粉返しを捏造しない");
        bool checkedPause = false;
        for (int pass = 0; pass < (live ? 1 : 2); pass++)
        {
            for (int k = 0; k < 2400 && (bool)Read("_playing")!; k++)
            {
                if (live && !checkedPause && (dustLive ? field.DustChain is not null && !field.DustBurstCompletion.IsCompleted : !field.PowderArrival.IsCompleted))
                {
                    var arrival = dustLive ? field.DustBurstCompletion : field.PowderArrival;
                    typeof(Main).GetMethod("TogglePause", Flags)!.Invoke(main, null);
                    int pausedIndex = (int)Read("_eventIndex")!;
                    await Wait(.15);
                    Require(!arrival.IsCompleted && (int)Read("_eventIndex")! == pausedIndex,
                        "本編の一時停止で粉の到着と台本の進行が一緒に止まる");
                    typeof(Main).GetMethod("SetSpeed", Flags)!.Invoke(main, new object[] { 2 });
                    Require(!arrival.IsCompleted, "停止中の倍速変更だけでは到着しない");
                    await Capture("tou-live-paused");
                    typeof(Main).GetMethod("TogglePause", Flags)!.Invoke(main, null);
                    checkedPause = true;
                    GD.Print(dustLive ? "DUST_BURST_LIVE_PAUSE_OK" : "TOU_POWDER_LIVE_PAUSE_OK");
                }
                await Wait(.05);
            }
            Require(!(bool)Read("_playing")!, "粉の実台本を完走");
            CheckTouPowderReplay(field, result);
            foreach (var pawn in field.Pawns.Values)
            {
                var hp = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId
                    && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Death or BattleEventKind.Revive);
                if (hp is not null) Require(pawn.Hp == Math.Max(0, hp.HpAfter), "台本の最終HPと一致");
            }
            GD.Print($"TOU_POWDER_FIXTURE_OK {name} stage={stage} seed={seed} pass={pass}");
            if (pass == 0 && !live) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
        }
        if (live) Require(checkedPause, "実台本の粉の到着待ちで停止を確認");
        main.QueueFree(); await Wait(.2);
    }

    private static void CheckTouPowderReplay(BattlefieldView3D field, BattleResult result)
    {
        int Count(string label) => result.Events.Count(e => e.Kind == BattleEventKind.ShockGauge && e.Text == label);
        Require(field.PowderDensePlays == Count(ShockGaugeLabels.Dense), "濃さの件数");
        Require(field.PowderMovePlays == Count(ShockGaugeLabels.DenseMove), "雷で返す件数");
        Require(field.PowderFlowPlays == Count(ShockGaugeLabels.DenseFlow), "糸で返す件数");
        Require(field.PowderPopPlays == Count(ShockGaugeLabels.DensePop), "濃い弾けの件数");
        Require(field.PowderBurnPlays == Count(ShockGaugeLabels.DenseBurn), "粉焼の件数");
        Require(field.PowderMarkPlays == Count(ShockGaugeLabels.PowderMark), "粉の印の件数");
        Require(field.PowderContagionPlays == Count(ShockGaugeLabels.Contagion), "伝染の件数");
        Require(field.DustBurstPlays == Count(ShockGaugeLabels.DustBurst), "糸玉を含む粉塵爆発の件数");
        int dustChains = DustBurstPresentation.Build(result.Events).Starts.Count;
        Require(field.DustChainPlays == dustChains, $"爆竹の連鎖の束ね件数 {field.DustChainPlays}/{dustChains}");
        var plan = TouPowderPresentation.Build(result.Events);
        Require(plan.ThunderReturns.Count == Count(ShockGaugeLabels.DenseMove), "雷の1発目と粉返しを全件接続");
        Require(plan.ContagionGains.Count == Count(ShockGaugeLabels.Contagion), "伝染の付与だけを移動に接続");
        Require(field.ActivePowderEffects == 0 && field.Pawns.Values.All(p => !p.PowderMarked && p.PowderDensity == 0), "終了で粉を残さない");
        GD.Print($"TOU_POWDER_REPLAY_OK dense={field.PowderDensePlays} return={field.PowderMovePlays}/{field.PowderFlowPlays} burn={field.PowderBurnPlays} contagion={field.PowderContagionPlays} heavy={field.PowderHeavyPlays} dust={field.DustBurstPlays}/{field.DustChainPlays}");
    }
}
