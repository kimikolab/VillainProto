using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 羽の台本対応、増減、左右、倍速、実戦と再生し直しを同じ演出口で確かめる。
public partial class MisaCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            CheckTimeline();
            await CheckFeathers();
            if (OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                await CheckReplay("試遊・標 ボス台", 0);
                await CheckReplay("試遊・標 道中", 1);
            }
            GD.Print("MISA_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void CheckTimeline()
    {
        int chased = 0, sprayed = 0, flowed = 0, friendly = 0, lost = 0;
        foreach (var row in Presets.Playtest.Where(x => x.Name.Contains("標")))
        for (int stage = 0; stage < 5; stage++)
        for (int seed = 0; seed < 4; seed++)
        {
            var result = BattleEngine.Run(BattleEngine.Materialize(row.F, 0),
                BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1), seed, verbose: true);
            var plan = MisaPresentation.Build(result.Events);
            Require(plan.HitsByCue.Count == result.Events.Count(e => e.Kind == BattleEventKind.Feather
                && e.Text is FeatherLabels.Chase or FeatherLabels.Flow or FeatherLabels.Spray), "全発に的がある");
            foreach (var (cueIndex, hitIndex) in plan.HitsByCue)
            {
                var cue = result.Events[cueIndex]; var hit = result.Events[hitIndex];
                Require(hitIndex > cueIndex && hit.ActorId == cue.ActorId && hit.TargetId is not null,
                    "同じ1発の的を参照");
                if (cue.Text == FeatherLabels.Spray)
                {
                    sprayed++;
                    Require(hit.Kind is BattleEventKind.Damage or BattleEventKind.Parry, "乱射はAttackを作らない");
                    if (hit.FriendlyFire) friendly++;
                }
                else
                {
                    chased++;
                    Require(plan.Attacks.Contains(hitIndex), "追尾で二重斬撃を出さない");
                    if (cue.Text == FeatherLabels.Flow) flowed++;
                }
            }
            lost += plan.LosingSprays.Count;
        }
        Require(chased > 0 && sprayed > 0 && flowed > 0 && friendly > 0 && lost > 0,
            $"陽性対照 chase={chased} spray={sprayed} flow={flowed} friendly={friendly} lost={lost}");
        BattleEvent[] boundary = [
            new() { Turn = 1, Kind = BattleEventKind.Feather, ActorId = 1, Text = FeatherLabels.Spray },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 3, Relayed = true },
            new() { Turn = 1, Kind = BattleEventKind.Parry, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Feather, ActorId = 1, Text = FeatherLabels.Chase },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 4, TargetId = 5 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 },
        ];
        var bounded = MisaPresentation.Build(boundary);
        Require(bounded.HitsByCue.Count == 1 && bounded.HitsByCue[0] == 2,
            "肩代わりや次の別攻撃の的を借りない");
        GD.Print($"MISA_TIMELINE_OK chase={chased} spray={sprayed} flow={flowed} friendly={friendly} lost={lost}");
    }

    private async Task CheckFeathers()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening[] openings = [
                new(1, team, "tome", "見境なしのミサ", 2, 100, 100, 10, AttackPattern.Single, false, UnitCatalog.Tome.Traits),
                new(2, 1 - team, "knight", "標のある敵", 0, 100, 100, 10, AttackPattern.Single, false),
                new(3, 1 - team, "axeman", "次の標", 4, 100, 100, 10, AttackPattern.Single, false),
                new(4, team, "sora", "味方", 4, 100, 100, 10, AttackPattern.Single, false),
            ];
            field.BeginBattle(openings, "ミサ・分離した羽の動作確認", 0);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            var misa = field.FindPawn(1)!;
            var feathers = misa.MisaFeathers!;
            await Wait(0.30);
            Require(feathers.Count == 1, "初期は1枚");
            feathers.SetCount(8);
            Require(feathers.Count == 8 && feathers.VisibleCount == 8, "在庫に応じた増加");
            await Capture($"misa-team{team}-speed{speed}-idle");
            feathers.BeginVolley(8);
            var shot = field.PlayMisaShot(misa, field.FindPawn(2), Cue(FeatherLabels.Chase, 1), speed, false);
            Require(misa.MovementPortrait == "tome_attack", "射出時の攻撃立ち絵");
            await Wait(0.08 / speed);
            await Capture($"misa-team{team}-speed{speed}-attack");
            await shot;
            feathers.SetCount(9);
            await field.PlayMisaShot(misa, field.FindPawn(3), Cue(FeatherLabels.Flow, 2), speed, false);
            await Wait(0.40 / speed);
            Require(feathers.InFlight == 0 && feathers.VisibleCount == 9, "連射中の増加と追尾の帰還");
            await field.PlayMisaShot(misa, field.FindPawn(4), Cue(FeatherLabels.Spray, 3), speed, true);
            await Wait(0.05 / speed);
            Require(feathers.VisibleCount == 8, "味方への乱射も着弾で消える");
            feathers.ConfirmLoss(8);
            Require(feathers.Count == 8 && feathers.VisibleCount == 8, "減少後の在庫と描画の一致");
            feathers.BeginVolley(1);
            await field.PlayMisaShot(misa, field.FindPawn(2), Cue(FeatherLabels.Spray, 1), speed, false);
            await Wait(0.75 / speed);
            Require(feathers.Count == 1 && feathers.VisibleCount == 1 && feathers.InFlight == 0,
                "減少通知の無い最後の1枚も再表示");
            Require(misa.MovementPortrait is null, "射出後に通常絵へ戻る");
            Require(field.Pawns.Values.All(p => p.Hp == 100 && p.Position.DistanceTo(p.Home) < 0.01f),
                "演出はHPや席を変えない");
            Require(field.MisaShots == 4 && field.MisaSprays == 2 && field.MisaFlows == 1, "発数は一度ずつ");
            feathers.SetCount(5);
            field.PlayDeath(misa);
            Require(!feathers.Visible, "死亡で羽を隠す");
            misa.SetHp(50); field.RevivePawn(misa);
            Require(feathers.Visible && feathers.Count == 5, "蘇生で在庫を保持");
            misa.AnimateVictory();
            Require(feathers.Visible == (team != 0), "味方の勝利絵の羽と二重描画しない");
            field.BeginBattle(openings, "", 0);
            misa = field.FindPawn(1)!; feathers = misa.MisaFeathers!;
            misa.AnimationSpeed = speed;
            feathers.BeginVolley(1);
            shot = field.PlayMisaShot(misa, field.FindPawn(2), Cue(FeatherLabels.Chase, 1), speed, false);
            field.BeginBattle(openings, "", 0);
            await shot;
            Require(field.MisaShots == 0 && field.FindPawn(1)!.MisaFeathers!.Count == 1,
                "再開時に旧在庫と射出を持ち越さない");
            GD.Print($"MISA_FEATHERS_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(0.1);
    }

    private async Task CheckReplay(string name, int stage)
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        await Wait(0.15);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Read(string key) => typeof(Main).GetField(key, flags)!.GetValue(main);
        typeof(Main).GetField("_speed", flags)!.SetValue(main, 4.0);
        var formation = Presets.Playtest.First(x => x.Name == name).F;
        var enemy = EnemyCatalog.PlaytestStages[stage];
        typeof(Main).GetMethod("EnterBattle", flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), BattleEngine.MaterializeEnemy(enemy.Enemy, enemy.Scale),
            stage == 0 ? 0 : 4, stage, name });
        var result = (BattleResult)Read("_result")!;
        var plan = MisaPresentation.Build(result.Events);
        var field = (BattlefieldView3D)Read("_battleField")!;
        for (int replay = 0; replay < 2; replay++)
        {
            if (replay != 0) typeof(Main).GetMethod("ReplayBattle", flags)!.Invoke(main, null);
            for (int k = 0; k < 240 && (bool)Read("_playing")!; k++) await Wait(0.25);
            Require(!(bool)Read("_playing")!, "実台本が完走");
            Require(field.MisaShots == plan.HitsByCue.Count && field.MisaShots > 0, "実台本の全発を再生");
            foreach (var group in result.Events.Where(e => e.TargetId is not null
                && e.Kind is BattleEventKind.Heal or BattleEventKind.Damage or BattleEventKind.Death or BattleEventKind.Revive)
                .GroupBy(e => e.TargetId))
            {
                int expected = Math.Max(0, group.Last().HpAfter);
                int? actual = field.FindPawn(group.Key)?.Hp;
                if (actual != expected)
                    foreach (var (e, i) in result.Events.Select((e, i) => (e, i)).Where(x => x.e.TargetId == group.Key).TakeLast(10))
                        GD.Print($"MISA_HP_TRACE #{i} {e.Kind} actor={e.ActorId} target={e.TargetId} hp={e.HpAfter} amount={e.Amount} text={e.Text}");
                Require(actual == expected, $"HPは台本の最終値と一致 id={group.Key} actual={actual} expected={expected}");
            }
            foreach (var pawn in field.Pawns.Values.Where(p => p.MisaFeathers is not null))
            {
                int count = result.Events.LastOrDefault(e => e.Kind == BattleEventKind.Feather && e.TargetId == pawn.InstanceId
                    && e.Text is FeatherLabels.Gain or FeatherLabels.Lost)?.Amount ?? 1;
                Require(pawn.MisaFeathers!.Count == count, "最終在庫は台本の最終値と一致");
            }
            GD.Print($"MISA_REPLAY_OK stage={stage} replay={replay} shots={field.MisaShots} spray={field.MisaSprays} flow={field.MisaFlows}");
        }
        main.QueueFree(); await Wait(0.2);
    }

    private static BattleEvent Cue(string label, int shot) => new() {
        Turn = 1, Kind = BattleEventKind.Feather, ActorId = 1, Text = label, Slot = shot,
    };
    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null || DisplayServer.GetName() == "headless") return;
        string directory = arg[14..];
        DirAccess.MakeDirRecursiveAbsolute(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, "画像保存");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
