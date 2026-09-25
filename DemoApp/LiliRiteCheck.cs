using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 通常の儀式と、儀式が最後の敵を倒す台本。どちらも本番の入口から再生する。
public partial class LiliRiteCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                await Replay(false);
                await Replay(true);
            }
            else if (OS.GetCmdlineUserArgs().Contains("--widths")) await PreviewWidths();
            else await Preview();
            GD.Print("LILI_RITE_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private async Task Replay(bool finish)
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        object? Read(string name) => typeof(Main).GetField(name, Flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        var players = BattleEngine.Materialize(Formation.Build(back1: UnitCatalog.Lili), 0);
        var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[0].Enemy, 1);
        foreach (var enemy in enemies)
        {
            enemy.SetCounter(StatusKeys.Stigma, 1);
            if (finish) enemy.Hp = 1;
        }
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] { players, enemies, 0, 0, "" });
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < 1000 && (bool)Read("_playing")!; k++) await Wait(0.02);
            Require(!(bool)Read("_playing")!, "儀式を含む台本の完走");
            var tape = (BattleResult)Read("_result")!;
            var field = (BattlefieldView3D)Read("_battleField")!;
            var starts = Enumerable.Range(0, tape.Events.Count)
                .Where(i => tape.Events[i].Kind == BattleEventKind.Kiss && tape.Events[i].Text == KissLabels.Rite).ToArray();
            Require(starts.Length > 0, "儀式の陽性対照");
            Require(starts.All(i => LiliRiteSpan.End(tape.Events, i) > i), "実台本の儀式の境界");
            Require(field.LiliRites == starts.Length && field.LiliRiteReleases == starts.Length, "儀式と降光は一度ずつ");
            Require(field.LiliRiteStrikes == starts.Length, "敵への光柱は一度ずつ");
            if (finish) Require(tape.PlayerWon && field.LiliRiteFinishes > 0, "最後の敵を儀式で倒す陽性対照");
            else Require(field.LiliRiteFinishes == 0, "通常の儀式を決着扱いしない");
            Require(!field.LiliRiteActive, "完走後に光と暗転を残さない");
            foreach (var group in tape.Events.Where(e => e.TargetId is not null
                && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal).GroupBy(e => e.TargetId))
                Require(field.FindPawn(group.Key)!.Hp == Math.Max(0, group.Last().HpAfter), "HPと出来事の順序は台本どおり");
            GD.Print($"LILI_RITE_REPLAY_OK finish={finish} pass={pass} rites={field.LiliRites} finales={field.LiliRiteFinishes}");
            if (pass == 0) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
        }
        main.QueueFree();
        await Wait(0.15);
    }

    private async Task Preview()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        foreach (bool finish in new[] { false, true })
        {
            DemoOpening[] openings = [
                new(1, team, "lili", "施しのリリ", 3, 78, 78, 3, AttackPattern.Single, false),
                new(2, team, "hota", "ホタ", 0, 30, 100, 10, AttackPattern.Single, false),
                new(3, team, "kugu", "クグ", 4, 30, 100, 10, AttackPattern.Single, false),
                new(4, 1-team, EnemyCatalog.Knight.Id, "敵", 0, 100, 100, 10, AttackPattern.Single, true),
                new(5, 1-team, EnemyCatalog.Knight.Id, "敵", 4, 100, 100, 10, AttackPattern.Single, true),
                new(6, 1-team, EnemyCatalog.Knight.Id, "敵", 1, 100, 100, 10, AttackPattern.Single, true),
            ];
            field.BeginBattle(openings, "", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var lili = field.FindPawn(1)!;
            var allies = new[] { lili, field.FindPawn(2)!, field.FindPawn(3)! };
            var enemies = new[] { field.FindPawn(4)!, field.FindPawn(5)!, field.FindPawn(6)! };
            foreach (var enemy in enemies) enemy.SetStigma(true);
            await Wait(0.4);
            string name = $"{team}-{speed}-{(finish ? "finish" : "rite")}";
            field.BeginLiliRite(lili, enemies, speed, finish);
            await Wait(0.20 / speed); await Capture(name + "-marks");
            field.StrikeLiliRite(enemies, finish);
            await Wait(0.12 / speed);
            foreach (var enemy in enemies)
            {
                enemy.SetHp(finish ? 0 : 80);
                field.DamagePopup(enemy, finish ? 100 : 20, "", LiliFx.Rose, true, false);
                if (finish) enemy.AnimateDeath();
            }
            await Capture(name + "-strike");
            field.GatherLiliRite(enemies);
            await Wait(0.16 / speed); await Capture(name + "-gather");
            await Wait((0.16 + (finish ? 0.18 : 0.06)) / speed);
            field.ReleaseLiliRite(allies, finish);
            await Wait(0.16 / speed);
            foreach (var ally in allies) ally.AnimateHeal();
            foreach (var enemy in enemies) enemy.SetStigma(false);
            await Capture(name + "-light");
            double tail = finish ? 0.85 : 0.48;
            field.EndLiliRite(tail / speed);
            await Wait(tail / speed + 0.1);
            Require(!field.LiliRiteActive, "光の余韻を終了する");
            // 再生途中のリセットで、遅れた暗転解除が次の儀式を消さない。
            field.BeginLiliRite(lili, [], speed, false);
            field.BeginBattle(openings, "", 0);
            Require(!field.LiliRiteActive, "再戦で儀式を解除する");
        }
    }
    private async Task PreviewWidths()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (int count in new[] { 5, 3, 2, 1 })
        {
            var openings = new System.Collections.Generic.List<DemoOpening> {
                new(1, team, "lili", "施しのリリ", 3, 78, 78, 3, AttackPattern.Single, false) };
            for (int k = 0; k < count; k++)
                openings.Add(new(2 + k, 1-team, EnemyCatalog.Knight.Id, "敵", k, 100, 100, 10, AttackPattern.Single, true));
            field.BeginBattle(openings, "", 0);
            var lili = field.FindPawn(1)!;
            var enemies = Enumerable.Range(2, count).Select(i => field.FindPawn(i)!).ToArray();
            // 同じ総量100の見比べ。余りは最初の柱に載せ、実再生と同じ変換口へ渡す。
            var head = new BattleEvent { Kind = BattleEventKind.Kiss, Turn = 1, Text = KissLabels.Rite, ActorId = 1, Amount = 100, Slot = count };
            var drains = enemies.Select((pawn, k) => new BattleEvent { Kind = BattleEventKind.Kiss, Turn = 1,
                Text = KissLabels.RiteDrain, ActorId = 1, TargetId = pawn.InstanceId,
                Amount = 100 / count + (k == 0 ? 100 % count : 0) }).ToArray();
            var widths = LiliRiteWeight.Widths(head, drains);
            foreach (var pawn in enemies) { pawn.SetStigma(true); pawn.AnimationSpeed = 2; }
            field.BeginLiliRite(lili, enemies, 2, true, widths);
            await Wait(0.11);
            field.StrikeLiliRite(enemies, true);
            await Wait(0.06);
            await Capture($"width-{team}-{count}");
            await Wait(0.65);
            field.ResetLiliRite();
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
