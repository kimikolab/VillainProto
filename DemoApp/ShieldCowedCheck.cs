using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 本番と同じ演出口を2倍速で観察する。盤面は書き換えず、台本の発行も検査する。
public partial class ShieldCowedCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--replay"))
            {
                await CheckReplay();
                // 大きなMainシーンを破棄した直後にMonoを終了させない。
                // Godot側へ積まれたResource解放をフレーム境界まで流す。
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Wait(0.3);
                GetTree().Quit();
                return;
            }
            var formation = Formation.Build(front1: UnitCatalog.Kugu, front3: UnitCatalog.Ban,
                center: UnitCatalog.Shiga, back1: UnitCatalog.Gald, back3: UnitCatalog.Dolga);
            int shields = 0, screams = 0, lost = 0, absorbed = 0;
            for (int seed = 0; seed < 12; seed++)
            {
                var result = BattleEngine.Run(BattleEngine.Materialize(formation, 0),
                    BattleEngine.Materialize(EnemyCatalog.Stages[2].Enemy, 1), seed, verbose: true);
                shields += result.Events.Count(e => e.Kind == BattleEventKind.Intercept && e.Text == InterceptLabels.RangeShield);
                screams += result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Cowed && e.SpreadFromId is not null);
                lost += result.Events.Count(e => e.Kind == BattleEventKind.Cowed && e.Text == CowedLabels.Lost);
                absorbed += result.Events.Count(e => e.Kind == BattleEventKind.Cowed && e.Text == CowedLabels.Absorbed);
                var routed = Enumerable.Range(0, result.Events.Count)
                    .Where(i => result.Events[i].Kind == BattleEventKind.Attack)
                    .SelectMany(i => Main.ShieldShareIndices(result.Events, i)).ToArray();
                Require(routed.Distinct().Count() == routed.Length, "同じ引受を2回再生しない");
                Require(routed.Length == result.Events.Count(e => e.Kind == BattleEventKind.Intercept && e.Text == InterceptLabels.RangeShield),
                    "実台本の引受を攻撃へ漏れなく結ぶ");
            }
            Require(shields > 0 && screams > 0, $"実台本 shield={shields} scream={screams}");
            Require(lost > 0, "竦みによる手番喪失通知");
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            DemoOpening[] openings = [
                new(1, 0, "ban", "据えのバン", 2, 100, 100, 10, AttackPattern.Single, false),
                new(2, 0, "kugu", "縛めのクグ", 0, 100, 100, 10, AttackPattern.Single, false),
                new(3, 0, "shiga", "責め苦のシガ", 1, 100, 100, 10, AttackPattern.Single, false),
                new(4, 1, EnemyCatalog.Chanter.Id, "被害者", 2, 100, 100, 10, AttackPattern.All, false),
                new(5, 1, EnemyCatalog.Archer.Id, "隣の敵", 0, 100, 100, 10, AttackPattern.Single, false),
                new(6, 1, EnemyCatalog.Archer.Id, "隣の敵", 1, 100, 100, 10, AttackPattern.Single, false),
            ];
            field.BeginBattle(openings, "", 1);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = 2;
            var ban = field.FindPawn(1)!;
            var kugu = field.FindPawn(2)!;
            var shiga = field.FindPawn(3)!;
            var victim = field.FindPawn(4)!;
            var recipients = new[] { field.FindPawn(5)!, field.FindPawn(6)! };
            await Wait(0.5);
            ban.AddFooting(1); ban.AddFooting(1);
            Vector3 home = ban.Position;
            var shield = field.ShowRangeShield(victim, new[] { ban }, new[] { (kugu, ban), (shiga, ban) }, AttackPattern.All, 2);
            await Wait(0.15);
            await Capture("shield-bend");
            await shield;
            await Capture("shield-impact");
            Require(ban.Position == home && kugu.Hp == 100 && shiga.Hp == 100, "据わったまま引受・保護先HP不変");
            Require(ban.HasStatusIcon(StatusKeys.Footing), "層の札");
            await Wait(0.7);
            field.SetBinding(kugu, victim, true);
            await Wait(0.4);
            var whipAttack = field.Attack(shiga, victim, AttackPattern.Single, new[] { victim });
            await Wait(0.10);
            await Capture("shiga-whip");
            await whipAttack;
            var scream = field.ShowCowedSpread(victim, recipients, 2);
            await Wait(0.14);
            await Capture("scream-wave");
            await scream;
            await Capture("cowed");
            Require(recipients.All(p => p.HasStatusIcon(StatusKeys.Cowed)), "通知された対象に竦み");
            Require(!victim.HasStatusIcon(StatusKeys.Cowed), "波紋の出どころに誤付与しない");
            field.SetTurnOwner(recipients[0], "", UiKit.Enemy);
            recipients[0].PulseCowedLost();
            await Wait(0.12);
            await Capture("cowed-lost");
            await Wait(0.2);
            recipients[0].SetStatusIcon(StatusKeys.Cowed, false);
            Require(!recipients[0].HasStatusIcon(StatusKeys.Cowed), "消費通知で竦み解除");
            recipients[0].BeginStatusSnapshot(); recipients[0].CommitStatusSnapshot();
            Require(!recipients[0].HasStatusIcon(StatusKeys.Cowed), "残量ゼロで解除");
            recipients[1].AnimateDeath(); recipients[1].AnimateRevive();
            field.BeginBattle(openings, "", 1);
            await Wait(0.2);
            Require(field.Pawns.Values.All(p => p.StatusIconCount == 0), "再開で破棄");
            GD.Print($"SHIELD_COWED_CHECK_OK shields={shields} screams={screams} lost={lost} absorbed={absorbed} speed=2");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task CheckReplay()
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Read(string name) => typeof(Main).GetField(name, flags)!.GetValue(main);
        var formation = Formation.Build(front1: UnitCatalog.Kugu, front3: UnitCatalog.Ban,
            center: UnitCatalog.Shiga, back1: UnitCatalog.Gald, back3: UnitCatalog.Dolga);
        typeof(Main).GetMethod("EnterBattle", flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), BattleEngine.Materialize(EnemyCatalog.Stages[2].Enemy, 1),
            0, 2, "" });
        for (int i = 0; i < 180 && (bool)Read("_playing")!; i++) await Wait(0.5);
        Require(!(bool)Read("_playing")!, "通常再生が完走");
        var result = (BattleResult)Read("_result")!;
        int expectedLost = result.Events.Count(e => e.Kind == BattleEventKind.Cowed && e.Text == CowedLabels.Lost);
        Require(expectedLost > 0 && (int)Read("_cowedLostPlays")! == expectedLost, "消費通知を1回ずつ再生");
        var field = (BattlefieldView3D)Read("_battleField")!;
        int expectedTorment = Enumerable.Range(0, result.Events.Count).Count(i =>
            field.FindPawn(result.Events[i].ActorId)?.UnitId == "shiga" && Main.IsTormentHit(result.Events, i));
        Require(expectedTorment > 0 && field.TormentHitPlays == expectedTorment,
            "責め苦の特性ダメージごとに鞭と音を1回再生");
        Require(field.Pawns.Values.All(p => !p.HasStatusIcon(StatusKeys.Cowed)), "終了後に竦みを残さない");
        GD.Print($"SHIELD_COWED_REPLAY_OK events={result.Events.Count} lost={expectedLost} torment={field.TormentHitPlays}");
        main.QueueFree();
        await Wait(1);
    }
    private async Task Capture(string phase)
    {
        string? directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (directory is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(directory[14..] + "/" + phase + ".png") == Error.Ok, "画像保存");
    }
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
