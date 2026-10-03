using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

// 実台本の再生・再開と、左右・1/2倍速の描画確認。同じ表示入口を通す。
public partial class MovementCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public override async void _Ready()
    {
        try
        {
            CheckArrowIndex();
            CheckFollowupIndex();
            CheckLandingIndex();
            if (OS.GetCmdlineUserArgs().Contains("--landing")) await LandingPreview();
            else if (OS.GetCmdlineUserArgs().Contains("--shio")) await ShioPreview();
            else if (OS.GetCmdlineUserArgs().Contains("--followup")) await Followup();
            else if (OS.GetCmdlineUserArgs().Contains("--replay")) await Replay();
            else await Preview();
            GD.Print("MOVEMENT_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void CheckArrowIndex()
    {
        var events = new[] {
            new BattleEvent { Turn = 1, Kind = BattleEventKind.EvadeRiposte, ActorId = 2, TargetId = 6 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, TargetId = 6 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 2, TargetId = 6 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.StatusArrow, ActorId = 2, TargetId = 6, Text = "毒,感電" },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Barrage, ActorId = 2, TargetId = 7 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, TargetId = 7 },
            new BattleEvent { Turn = 2, Kind = BattleEventKind.TurnStart },
            new BattleEvent { Turn = 2, Kind = BattleEventKind.StatusArrow, ActorId = 2, TargetId = 6, Text = "燃焼" },
        };
        var index = new MovementPresentation(events);
        Require(index.Attacks.Count == 2 && index.Attacks[1].Kind == BattleEventKind.EvadeRiposte
            && index.Attacks[5].Kind == BattleEventKind.Barrage, "各矢を対応するAttackだけに接続");
        Require(index.ArrowStates.Count == 1 && index.ArrowStates[1] == "毒,感電",
            "後続の命中記録から矢の色を取り、別の矢やターンへ持ち越さない");
    }

    private async Task Replay()
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        T Read<T>(string name) => (T)typeof(Main).GetField(name, Flags)!.GetValue(main)!;
        void Call(string name, params object[] args) => typeof(Main).GetMethod(name, Flags)!.Invoke(main, args);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        Call("ClearFormation");
        string[] keys = { "sero", "yomi", "basa", "shio", "hane" };
        for (int i = 0; i < keys.Length; i++) Call("DropUnit", i, keys[i]);
        var picker = Read<OptionButton>("_stagePicker");
        int stage = EnemyCatalog.Stages.Count + 1;
        picker.Select(stage);
        picker.EmitSignal(OptionButton.SignalName.ItemSelected, stage);
        Read<SpinBox>("_enemyHp").GetLineEdit().Text = "200";
        Read<SpinBox>("_enemyAttack").GetLineEdit().Text = "200";
        var formation = new Formation();
        for (int i = 0; i < keys.Length; i++) formation[i] = UnitCatalog.All.Single(u => u.Id == keys[i]);
        int chosen = -1;
        int coverage = -1;
        for (int seed = 0; seed < 200; seed++)
        {
            var sample = BattleEngine.Run(BattleEngine.Materialize(formation, 0),
                BattleEngine.MaterializeEnemy(EnemyCatalog.TestStages[0].Enemy, new EnemyScaleRule(200, 200)), seed);
            int kinds = sample.Events.Where(e => MovementPresentation.IsCue(e.Kind)).Select(e => e.Kind).Distinct().Count();
            var indexed = new MovementPresentation(sample.Events);
            if (kinds > coverage && indexed.Moves.Values.Any(e => e.Kind == BattleEventKind.Retreat)
                && indexed.Moves.Values.Any(e => e.Kind == BattleEventKind.Blast)) { chosen = seed; coverage = kinds; }
        }
        Require(chosen >= 0, "緊急退避と吹っ飛ばしの実台本を見つける");
        Read<SpinBox>("_seed").Value = chosen;
        GD.Print($"MOVEMENT_SEED {chosen}");
        Call("StartBattle");
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < 2400 && Read<bool>("_playing"); i++) await Wait(0.025);
            Require(!Read<bool>("_playing"), "実戦再生が完走");
            var result = Read<BattleResult>("_result");
            var field = Read<BattlefieldView3D>("_battleField");
            var indexed = Read<MovementPresentation>("_movement");
            Require(indexed.Moves.Values.Any(e => e.Kind == BattleEventKind.Blast), "吹っ飛ばしを実際のMoveへ接続");
            Require(indexed.Moves.Values.Any(e => e.Kind == BattleEventKind.Retreat), "緊急退避を実際のMoveへ接続");
            foreach (var group in result.Events.Where(e => MovementPresentation.IsCue(e.Kind)).GroupBy(e => e.Kind))
            {
                Require(field.MovementPlays.GetValueOrDefault(group.Key) == group.Count(), $"{group.Key} の漏れ・重複");
                GD.Print($"MOVEMENT_EVENT {group.Key}={group.Count()}");
            }
            int sero = field.Pawns.Values.Single(p => p.UnitId == "sero").InstanceId;
            int shio = field.Pawns.Values.Single(p => p.UnitId == "shio").InstanceId;
            Require(field.ShioHealPlays == result.Events.Count(e => e.Kind == BattleEventKind.Heal && e.ActorId == shio && e.Amount > 0),
                "シオの回復を実台本の件数どおり表示");
            Require(field.ShioStrengthPlays == result.Events.Count(e => e.Kind == BattleEventKind.Whet && e.ActorId == shio
                && e.WhetRoute == WhetRoute.Drifter && e.AttackAfter is not null && e.Amount > 0), "通常・溢れの強化を各Whetで1回");
            GD.Print($"SHIO_REPLAY_OK heals={field.ShioHealPlays} buffs={field.ShioStrengthPlays}");
            int yomi = field.Pawns.Values.Single(p => p.UnitId == "yomi").InstanceId;
            var iais = result.Events.Where(e => e.Kind == BattleEventKind.Attack && e.ActorId == yomi).ToList();
            int hane = field.Pawns.Values.Single(p => p.UnitId == "hane").InstanceId;
            int basa = field.Pawns.Values.Single(p => p.UnitId == "basa").InstanceId;
            int storms = indexed.ShuffleStarts.Values.Count(moves => moves[0].ActorId == basa);
            Require(storms > 0 && field.BasaStormPlays == storms, "バサの隊列入れ替えごとに1回だけ嵐を出す");
            Require(field.TailwindGustPlays == result.Events.Count(e => e.Kind == BattleEventKind.Tailwind), "追い風を漏れなく突風にする");
            GD.Print($"BASA_REPLAY_OK storms={storms} tailwinds={field.TailwindGustPlays}");
            Require(field.BasaSweepPlays == result.Events.Select((e, i) => (e, i)).Count(x =>
                x.e.Kind == BattleEventKind.Attack && x.e.ActorId == basa && (x.e.Pattern == AttackPattern.Sweep
                || indexed.Attacks.GetValueOrDefault(x.i)?.Kind == BattleEventKind.Squall)), "バサの薙ぎ・突風を風の攻撃に接続");
            Require(field.Pawns.Values.All(p => !p.WindCarried), "再生終了に竜巻の回転が残らない");
            Require(field.FindPawn(yomi)!.Advances && field.FindPawn(hane)!.Advances, "現行ヨミとハネは踏込");
            Require(field.YomiSweepPlays == iais.Count(e => e.Pattern == AttackPattern.Sweep), "薙ぎの大剣閃が台本と一致");
            Require(field.HaneBlastPlays == indexed.Attacks.Count(p => p.Value.Kind == BattleEventKind.Blast), "吹っ飛ばしが台本と一致");
            Require(field.Pawns.Values.All(p => !p.BlastActive), "実戦終了に吹っ飛ばし姿勢が残らない");
            GD.Print($"SMASH_REPLAY_OK blasts={field.HaneBlastPlays} pins={field.HanePinPlays} sweeps={field.YomiSweepPlays}");
            Require(iais.Any(e => e.Reaction) && iais.Any(e => !e.Reaction), "ヨミの通常・追加攻撃を実台本で検証");
            Require(field.YomiIaiPlays == iais.Count && field.YomiIaiExtraPlays == iais.Count(e => e.Reaction), "居合は実際のAttackごとに1回");
            GD.Print($"YOMI_REPLAY_OK attacks={field.YomiIaiPlays} extra={field.YomiIaiExtraPlays}");
            Require(field.MovementArrowPlays == result.Events.Count(e => e.Kind == BattleEventKind.Attack && e.ActorId == sero), "矢はAttackごとに1本");
            Require(field.MovementPiercePlays == result.Events.Count(e => e.Kind == BattleEventKind.Attack
                && e.ActorId == sero && e.Pattern == AttackPattern.Pierce), "貫通矢だけを発光させる");
            Require(field.MovementShotPlays == result.Events.Count(e => e.Kind == BattleEventKind.MoveShot),
                "移動の追撃は後続Attackで1回だけ撃つ");
            Require(indexed.SpringGuards.Count == result.Events.Count(e => e.Kind == BattleEventKind.SpringGuard),
                "隣の味方への介入を直後の弾き返しへ接続");
            Require(field.Pawns.Values.All(p => !p.DecoyShown), "決着時に挑発の印が残らない");
            foreach (var pawn in field.Pawns.Values)
            {
                var hp = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal);
                if (hp is not null) Require(pawn.Hp == Math.Clamp(hp.HpAfter, 0, pawn.MaxHp), "最終HPが台本と一致");
                var move = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.Move);
                if (move is not null) Require(pawn.Slot == move.Slot, "最終席が台本と一致");
            }
            Require(field.MovementPlays.GetValueOrDefault(BattleEventKind.Retreat) > 0, "実戦に緊急退避がある");
            Require(field.MovementPlays.GetValueOrDefault(BattleEventKind.Blast) > 0, "実戦に吹っ飛ばしがある");
            GD.Print($"MOVEMENT_REPLAY_OK pass={pass} events={result.Events.Count} arrows={field.MovementArrowPlays}");
            if (pass == 0) Call("ReplayBattle");
        }
        main.QueueFree();
        await Wait(0.1);
    }

    private async Task Preview()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            void Reset()
            {
                field.BeginBattle(new DemoOpening[] {
                    new(1, team, "shio", "シオ", 3, 100, 100, 10, AttackPattern.Single, false),
                    new(2, team, "sero", "セロ", 0, 30, 100, 10, AttackPattern.Single, false),
                    new(3, team, "hane", "ハネ", 1, 100, 100, 10, AttackPattern.Pierce, true),
                    new(4, team, "yomi", "ヨミ", 2, 100, 100, 10, AttackPattern.Single, true),
                    new(5, team, "basa", "バサ", 4, 100, 100, 10, AttackPattern.Sweep, false),
                    new(6, 1-team, "knight", "前の敵", 0, 100, 100, 10, AttackPattern.Single, true),
                    new(7, 1-team, "archer", "中央の敵", 2, 100, 100, 10, AttackPattern.Single, false),
                    new(8, 1-team, "priest", "後ろの敵", 3, 100, 100, 10, AttackPattern.Single, false),
                }, "移動演出の確認", 0);
                foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            }
            Reset();
            await Wait(0.3);
            var retreat = new BattleEvent { Turn = 1, Kind = BattleEventKind.Retreat, ActorId = 1, TargetId = 2, PartnerId = 1, StatusRemaining = 3 };
            field.ShowMovementCue(retreat, speed);
            field.MovementLeaves(field.FindPawn(2)!);
            field.MoveWithCue(field.FindPawn(2)!, 3, retreat, field.FindPawn(1));
            field.MoveWithCue(field.FindPawn(1)!, 0, retreat, field.FindPawn(1));
            await Wait(0.075 / speed);
            await Capture($"movement-{team}-{speed}-retreat");
            await Wait(0.5 / speed);
            field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.LastDodge, ActorId = 2, TargetId = 6 }, speed);
            await Wait(0.18 / speed);
            await Capture($"movement-{team}-{speed}-dodge");
            await Wait(0.5 / speed);
            var shot = field.Attack(field.FindPawn(2), field.FindPawn(6), AttackPattern.Pierce,
                new[] { field.FindPawn(6)!, field.FindPawn(7)!, field.FindPawn(8)! }, advance: false, arrowStates: "毒,燃焼,感電");
            await Wait(0.07 / speed);
            await Capture($"movement-{team}-{speed}-arrow");
            await shot;
            var blast = new BattleEvent { Turn = 1, Kind = BattleEventKind.Blast, ActorId = 3, TargetId = 6, Slot = 0, Amount = 3 };
            field.ShowMovementCue(blast, speed);
            await field.Attack(field.FindPawn(3), field.FindPawn(6), AttackPattern.Pierce,
                new[] { field.FindPawn(6)!, field.FindPawn(7)!, field.FindPawn(8)! }, advance: false, movementCue: blast);
            field.MoveWithCue(field.FindPawn(6)!, 3, blast, field.FindPawn(3));
            field.MoveWithCue(field.FindPawn(7)!, 0, null, field.FindPawn(3));
            field.MoveWithCue(field.FindPawn(8)!, 2, null, field.FindPawn(3));
            await Wait(0.13 / speed);
            await Capture($"movement-{team}-{speed}-blast");
            await Wait(0.5 / speed);
            field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Disarray, ActorId = 3, TargetId = 7, PartnerId = 5 }, speed);
            field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Tailwind, ActorId = 3, TargetId = 4, PartnerId = 2, SpreadFromId = 6 }, speed);
            field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Overflow, ActorId = 1, TargetId = 4, StatusRemaining = 15 }, speed);
            await Wait(0.14 / speed);
            await Capture($"movement-{team}-{speed}-wind");
            await Wait(0.7 / speed);
            // 低頻度の分岐も実体のある対象へ描く。状態の色・勢い余り・弾き返しを含む。
            foreach (var kind in new[] { BattleEventKind.Regroup, BattleEventKind.ShioStage, BattleEventKind.EvadeStage,
                BattleEventKind.DisarrayStage, BattleEventKind.Decoy, BattleEventKind.Spring, BattleEventKind.KillImpact,
                BattleEventKind.StaggerBreach })
            {
                int actor = kind is BattleEventKind.Regroup or BattleEventKind.ShioStage ? 1
                    : kind is BattleEventKind.Decoy or BattleEventKind.EvadeStage ? 2
                    : kind == BattleEventKind.KillImpact ? 4 : 3;
                field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = kind, ActorId = actor,
                    TargetId = kind == BattleEventKind.KillImpact ? 2 : 7, PartnerId = 5,
                    SpreadFromId = 6, Slot = 3, Amount = 70, Text = ImpactLabels.Tumble }, speed);
                await Wait(0.25 / speed);
            }
            await field.Attack(field.FindPawn(5), field.FindPawn(7), AttackPattern.Sweep,
                new[] { field.FindPawn(6)!, field.FindPawn(7)!, field.FindPawn(8)! }, advance: false,
                movementCue: new BattleEvent { Turn = 1, Kind = BattleEventKind.Squall, ActorId = 5 });
            for (int i = 0; i < 5; i++)
            {
                var hit = field.FindPawn(6 + i % 3)!;
                await field.Attack(field.FindPawn(2), hit, AttackPattern.Single, new[] { hit }, advance: false);
            }
            await Wait(0.5 / speed);
            Require(field.Pawns.Values.All(p => p.Hp == (p.InstanceId == 2 ? 30 : 100)), "演出はHPを変えない");
            Require(field.Pawns.Values.All(p => p.Position.DistanceTo(p.Home) < 0.01f), "動いた駒が席に着く");
            foreach (var kind in Enum.GetValues<BattleEventKind>().Where(MovementPresentation.IsCue))
                field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = kind }, speed); // 欠けた対象で落ちない。
            field.FindPawn(2)!.MovementPose(1.2f);
            field.FindPawn(2)!.AnimateDeath();
            field.FindPawn(2)!.AnimateRevive();
            Reset();
            Require(field.MovementPlays.Count == 0 && field.MovementArrowPlays == 0, "再開で演出記憶を消す");
            await Wait(0.5);
        }
    }

    private async Task Capture(string name)
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null || DisplayServer.GetName() == "headless") return;
        string directory = arg[14..];
        DirAccess.MakeDirRecursiveAbsolute(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, "画像保存");
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); }
}
