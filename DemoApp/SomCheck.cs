using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 本物の台本・再戦と、描画だけの左右・倍速・消去を確認する専用シーン。
public partial class SomCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            CheckLedger();
            string[] args = OS.GetCmdlineUserArgs();
            if (!args.Contains("--verify-only")) await Visuals();
            if (!args.Contains("--visual-only"))
            {
                await Replay("試遊・感電 光の盾", 0, false);
                await Replay("試遊・感電 光の盾", 1, false);
                await Replay("試遊・感電 雷の型", 1, false);
                await Replay("試遊・感電 光の盾", 1, true);
                await Replay("反転確認", 1, false);
            }
            GD.Print("SOM_CHECK_COMPLETE"); GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
    private static void Require(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); }

    private static void CheckLedger()
    {
        var ledger = new SomVeilLedger();
        ledger.Observe(40); ledger.AddVeil(14, 54); ledger.Observe(47);
        Require(ledger.Veil == 7 && ledger.Total == 47, "板と衣の混在で衣から先に7を失う");
        ledger.AddVeil(7, 49);
        Require(ledger.Veil == 9 && ledger.Total == 49, "次の増加通知から途中の消費5を先に照合");
        ledger.Observe(60); Require(ledger.Veil == 9, "他の破片の増加を衣として数えない");
        ledger.Observe(0); Require(ledger.Veil == 0, "破片の行が無いターンは0");
        Require(SomVeilLedger.Brightness(7) < SomVeilLedger.Brightness(49)
            && SomVeilLedger.Brightness(int.MaxValue) <= .381f, "衣の量は単調・輝度は有界");
        GD.Print("SOM_LEDGER_OK");
    }

    private async Task Replay(string name, int stage, bool campaign)
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
        object? Read(string key) => typeof(Main).GetField(key, Flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        var formation = name == "反転確認"
            ? Formation.Build(front1: UnitCatalog.Tou, front3: UnitCatalog.Kugu, center: UnitCatalog.Beni,
                back1: UnitCatalog.Kata, back3: UnitCatalog.Som)
            : Presets.Playtest.First(p => p.Name == name).F;
        var enemy = campaign ? BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1)
            : BattleEngine.MaterializeEnemy(EnemyCatalog.PlaytestStages[stage].Enemy, EnemyCatalog.PlaytestStages[stage].Scale);
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), enemy, 0, stage, name });
        var result = (BattleResult)Read("_result")!;
        var field = (BattlefieldView3D)Read("_battleField")!;
        var plan = SomPresentation.Build(result.Events);
        foreach (var (index, rain) in plan.Rains)
            Require(rain.Pops.Length == result.Events[index].Amount,
                $"光の数と出どころ {index}: {rain.Pops.Length}/{result.Events[index].Amount}");
        int Count(string label) => result.Events.Count(e => e.Kind == BattleEventKind.Spark && e.Text == label);
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < 2400 && (bool)Read("_playing")!; i++) await Wait(.025);
            Require(!(bool)Read("_playing")!, "台本の完走");
            Require(field.SomRains == Count(SparkLabels.Release) && field.SomStops == Count(SparkLabels.Silenced), "降る・止まるが台本件数と一致");
            Require(field.SomVeils == Count(SparkLabels.Veil), "衣の付与が台本件数と一致");
            Require(field.SomLights == plan.Rains.Sum(p => p.Value.Pops.Length), "糸玉・獣・敵から光を欠落なく回収");
            Require(field.SomSummons == result.Events.Count(e => e.Kind == BattleEventKind.Summon && e.Text == UnitCatalog.Fodder.Name), "召喚の回数");
            Require(field.SomRisingCount == 0 && field.SomVeilCount == 0, "終了・再戦で光と衣を残さない");
            foreach (var pawn in field.Pawns.Values)
            {
                var last = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind is BattleEventKind.Damage
                    or BattleEventKind.Heal or BattleEventKind.Revive or BattleEventKind.Death or BattleEventKind.Scar);
                if (last is not null) Require(pawn.Hp == Math.Max(0, last.HpAfter), "台本どおりの最終HP");
            }
            if (name == "反転確認") Require(plan.Rains.Values.Any(r => r.InvertedTargets.Count > 0), "反転する相手に濁った光を送る");
            else if (campaign) Require(field.SomStops > 0 && field.HushCracks > 0, "粛で光が消えて既存のひびが出る");
            else if (name == "試遊・感電 雷の型")
                Require(field.SomRains == 0 && field.SomVeils == 0 && field.SomSummons == 0, "ソム不在なら専用演出が出ない");
            else Require(field.SomImpacts > 0 && field.SomVeils > 0, "衣と被弾の反応が実戦で出る");
            GD.Print($"SOM_REPLAY_OK name={name} stage={stage} campaign={campaign} pass={pass} events={result.Events.Count} rains={field.SomRains} stopped={field.SomStops} lights={field.SomLights} veil={field.SomVeils} impacts={field.SomImpacts} breaks={field.SomBreaks}");
            if (pass == 0) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
        }
        if (field.Pawns.Values.FirstOrDefault(p => p.PresentationAlive) is { } survivor)
        {
            field.SetSomVeil(survivor, 14, false, 1);
            field.RaiseSomLight(-1, survivor.InstanceId, 1);
            typeof(Main).GetMethod("ReturnToFormation", Flags)!.Invoke(main, null);
            Require(field.SomRisingCount == 0 && field.SomVeilCount == 0, "編成へ戻る途中で光と衣を消す");
        }
        main.QueueFree(); await Wait(.1);
    }

    private async Task Visuals()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        await Wait(.15);
        Require(field.Size.X > 1000 && field.Size.Y > 600, "描画確認の盤面サイズ");
        static DemoOpening Open(UnitDef def, int id, int team, int slot) => new(id, team, def.Id, def.Name,
            slot, def.MaxHp, def.MaxHp, def.Attack, def.Pattern, def.Advances, def.Traits);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 4.0 })
        {
            var units = new[] { Open(UnitCatalog.Som, 1, team, 4), Open(UnitCatalog.Kugu, 2, team, 0),
                Open(UnitCatalog.Tou, 3, team, 2), Open(UnitCatalog.Kata, 5, team, 3),
                Open(UnitCatalog.Kado, 6, 1 - team, 0) };
            field.BeginBattle(units, "ソム — 召喚・光の雨・光の衣", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var som = field.FindPawn(1)!;
            field.BeginSomSummon(som, speed, true);
            await Wait(.18 / speed);
            await Capture($"team{team}-speed{speed}-summon");
            field.AddSummon(Open(UnitCatalog.Fodder, 4, 1 - team, 7), false, speed);
            var beast = field.FindPawn(4)!; beast.AnimationSpeed = speed;
            Vector3 enemySeat = beast.Home;
            field.SomBeastLooksBack(som, beast);
            Require(beast.Position.IsEqualApprox(BattlefieldView3D.SomBeastOrigin(som))
                && beast.Home.IsEqualApprox(enemySeat) && beast.Team == 1 - team && beast.Slot == 7,
                "見た目はソムの側、所属と席は最初から敵陣");
            Require(beast.HasShockAura, "出現から雷をまとう");
            await Wait(.38 / speed);
            await Capture($"team{team}-speed{speed}-lookback");
            field.SomBeastSnubs(som, beast, true);
            await Wait(.20 / speed);
            Require(beast.SomBetraying && beast.Position.DistanceTo(enemySeat) > .1f
                && beast.Position.DistanceTo(BattlefieldView3D.SomBeastOrigin(som)) > .1f,
                "敵陣へ走る途中の位置を通る");
            await Capture($"team{team}-speed{speed}-snub");
            await Wait((SomFx.BeastRunSeconds - .20 + .05) / speed);
            field.SomBeastArrives(beast, speed);
            Require(!beast.SomBetraying && beast.Position.IsEqualApprox(enemySeat)
                && beast.MovementPortrait is null && beast.HasShockAura, "敵の席へ着地して通常の向きに戻る");
            await Capture($"team{team}-speed{speed}-arrived");
            beast.SetShocked(true);
            field.ShowSilkBall(new() { Turn = 1, Kind = BattleEventKind.SilkBall, Text = SilkBallLabels.Place,
                ActorId = 2, TargetId = 20, Team = 1 - team, Slot = 6 }, speed);
            for (int i = 0; i < 6; i++) field.RaiseSomLight(i, i % 2 == 0 ? 4 : 20, speed);
            await Wait(.2 / speed);
            field.GatherSomLight(som, Enumerable.Range(0, 6).ToArray(), false, speed);
            await Wait(.13 / speed);
            await Capture($"team{team}-speed{speed}-gather");
            await Wait(.14 / speed);
            field.RainSomLight(som, 6, new HashSet<int> { 3 }, speed);
            await Wait(.14 / speed);
            await Capture($"team{team}-speed{speed}-rain");
            field.SetSomVeil(som, 7, true, speed);
            field.SetSomVeil(field.FindPawn(2)!, 70, true, speed);
            field.SetSomVeil(field.FindPawn(5)!, 7000, true, speed);
            await Wait(.5);
            await Capture($"team{team}-speed{speed}-veil-tiers");
            field.SomVeilContact(som, speed); await Wait(.04 / speed);
            await Capture($"team{team}-speed{speed}-veil-hit");
            field.SetSomVeil(som, 0, false, speed);
            for (int i = 10; i < 13; i++) field.RaiseSomLight(i, 4, speed);
            field.GatherSomLight(som, new[] { 10, 11, 12 }, true, speed);
            await Wait(.13 / speed); await Capture($"team{team}-speed{speed}-stopped");
            field.SomBeastLooksBack(som, beast);
            field.SomBeastSnubs(som, beast, false);
            field.EndSomPresentation(); await Wait((SomFx.BeastRunSeconds + .05) / speed);
            Require(field.SomRisingCount == 0 && field.SomVeilCount == 0, "描画終了の消去");
            Require(!beast.SomBetraying && beast.Position.IsEqualApprox(enemySeat)
                && beast.MovementPortrait is null && !beast.HasShockAura, "走る途中の中断で位置・差分・電気を残さない");
            Require(som.Hp == som.MaxHp && beast.Hp == beast.MaxHp, "演出だけでHPを変えない");
            field.BeginBattle(units, "再戦", 0);
            Require(field.SomRains == 0 && field.SomStops == 0 && field.SomVeilCount == 0, "再戦の初期化");
            GD.Print($"SOM_VISUAL_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(.1);
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        string path = ProjectSettings.GlobalizePath("res://../design/art/som/run-check");
        DirAccess.MakeDirRecursiveAbsolute(path);
        double priorScale = Engine.TimeScale;
        Engine.TimeScale = 0;
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = GetViewport().GetTexture().GetImage();
            Require(image.SavePng(System.IO.Path.Combine(path, name + ".png")) == Error.Ok, "画像の保存");
            // PNG保存の実時間を次の短い倍速アニメーションに持ち込まない。
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        finally { Engine.TimeScale = priorScale; }
    }
}
