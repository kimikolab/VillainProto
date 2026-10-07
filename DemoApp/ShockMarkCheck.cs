using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 台本の5プリセットと再戦、左右・倍速・消去を実際の演出口で検証する。
public partial class ShockMarkCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            CheckSounds();
            if (!OS.GetCmdlineUserArgs().Contains("--replay-only")) await CheckVisuals();
            if (OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                await Replay("試遊・感電 火の型", 1, 0);
                await Replay("試遊・感電 雷の型", 1, 0);
                await Replay("試遊・感電 糸", 0, 0);
                await Replay("試遊・標 ボス台", 0, 0);
                await Replay("試遊・標 道中", 1, 4);
            }
            GD.Print("SHOCK_MARK_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void CheckSounds()
    {
        var audio = new BattleAttackAudio(); AddChild(audio);
        int existingVoices = audio.GetChildren().OfType<AudioStreamPlayer>().Count();
        foreach (var cue in Enum.GetValues<ShockMarkSound>())
        {
            audio.PlayShockMark(cue);
            Require(audio.GetChildren().OfType<AudioStreamPlayer>().Any(p => p.Stream?.GetLength() > 0), "専用SEを生成して再生");
        }
        Require(audio.GetChildren().OfType<AudioStreamPlayer>().Count() <= existingVoices + 6, "専用SEの同時発音を制限");
        audio.StopAll();
        Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(p => !p.Playing), "終了時にSEを止める");
        audio.QueueFree();
        GD.Print("SHOCK_MARK_SOUNDS_OK");
    }

    private async Task CheckVisuals()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening O(int id, int side, UnitDef unit, int slot) => new(id, side, unit.Id, unit.Name, slot,
                100, 100, 10, AttackPattern.Single, false, unit.Traits);
            DemoOpening[] opening = [ O(1, team, UnitCatalog.Tome, 2), O(2, team, UnitCatalog.Shiga, 0),
                O(3, team, UnitCatalog.Kata, 4), O(4, team, UnitCatalog.Tou, 3), O(5, team, UnitCatalog.Kugu, 1),
                new(6, 1 - team, "knight", "標のある敵", 0, 100, 100, 10, AttackPattern.Single, false),
                new(7, 1 - team, "axeman", "次の標", 2, 100, 100, 10, AttackPattern.Single, false) ];
            field.BeginBattle(opening, "感電・標 演出確認", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var misa = field.FindPawn(1)!; var shiga = field.FindPawn(2)!; var kata = field.FindPawn(3)!;
            var tou = field.FindPawn(4)!; var kugu = field.FindPawn(5)!; var enemy = field.FindPawn(6)!;
            await Wait(0.2);
            field.ShowCharge(shiga, 4, true, speed); field.ShowCloud(kata, 8, speed);
            field.ShowMarkLayer(enemy, 5, speed); field.ShowScar(enemy, 70, 50, speed);
            enemy.SetHp(70);
            Require(enemy.MaxHp == 70 && Math.Abs(enemy.Hud.ScarFraction - 0.3f) < 0.001f, "回復しても欠損が残る");
            shiga.SetShocked(false);
            Require(shiga.StoredCharge == 4, "感電が消えても蓄電は残る");
            misa.MisaFeathers!.SetCount(8); await field.BeginMisaVolley(misa, 8, speed);
            await Wait(0.3 / speed);
            await Capture($"team{team}-speed{speed}-stock-cloud-mark");
            var shot = field.PlayMisaShot(misa, enemy, new() { Turn = 1, Kind = BattleEventKind.Feather,
                Text = FeatherLabels.Chase, Slot = 1, Amount = 8 }, speed, false);
            await Wait(0.20 / speed); await Capture($"team{team}-speed{speed}-beam"); await shot;
            Require(misa.MisaFeathers.BeamCount == 1, "羽から光線が出た");
            field.ShowInterrupt(shiga, enemy, speed);
            Require(shiga.InterruptWhip && shiga.MovementPortrait == "shiga_interrupt", "割り込み差分");
            var interrupt = field.Attack(shiga, enemy, AttackPattern.Single, new[] { enemy }, reaction: true);
            await Wait(0.40 / speed); await Capture($"team{team}-speed{speed}-interrupt"); await interrupt;
            field.ShowCharge(shiga, 3, false, speed);
            Require(!shiga.InterruptWhip && shiga.StoredCharge == 3, "割り込み後の消費");
            field.StrikeThunder(null, enemy, 1, 2, speed, kata);
            await Capture($"team{team}-speed{speed}-thunder");
            Require(kata.Thundercloud == 8, "落雷しても雲を消費しない");
            field.BeginPowderAttack(tou);
            var powder = field.ShowPowder(tou, null, enemy, PowderRoute.Main, speed);
            await Wait(0.10 / speed); await Capture($"team{team}-speed{speed}-powder"); await powder;
            await field.ShowPowder(tou, enemy, field.FindPawn(7), PowderRoute.Spread, speed);
            await field.ShowPowder(tou, null, shiga, PowderRoute.Leak, speed);
            field.SetBinding(kugu, enemy, true); await Wait(0.25 / speed);
            field.ShowThreadDischarge(kugu, enemy, kata, false, speed);
            await Wait(0.10 / speed); await Capture($"team{team}-speed{speed}-thread");
            field.ClearBindingsFor(kugu);
            field.ShowThreadDischarge(kugu, enemy, null, true, speed);
            await Wait(0.21 / speed); await Capture($"team{team}-speed{speed}-snap");
            await Wait(0.6 / speed);
            Require(field.ActiveBindingCount == 0 && field.ThreadPlays == 2 && field.ThreadReleasePlays == 1, "解除と通電");
            Require(field.Pawns.Values.Where(p => p != enemy).All(p => p.Hp == 100), "演出からHPを減らさない");
            field.ShowCower(shiga, speed);
            field.EndShockMarkPresentation(); await Wait(0.05);
            Require(field.Pawns.Values.All(p => !p.ShockMarkActive), "終了で常駐演出を消す");
            field.BeginBattle(opening, "再開", 0);
            var pending = field.ShowPowder(field.FindPawn(4), null, field.FindPawn(6), PowderRoute.Main, speed);
            field.BeginBattle(opening, "再開", 0); await pending;
            Require(field.PowderMainPlays == 0 && field.Pawns.Values.All(p => p.StoredCharge == 0 && p.Thundercloud == 0
                && p.MarkLayers == 0 && p.Hud.ScarFraction == 0), "再戦に旧状態を持ち越さない");
            GD.Print($"SHOCK_MARK_VISUAL_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(0.2);
    }

    private async Task Replay(string name, int stage, int seed)
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
        object? Read(string key) => typeof(Main).GetField(key, Flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        var formation = Presets.Playtest.First(p => p.Name == name).F;
        var enemy = EnemyCatalog.PlaytestStages[stage];
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), BattleEngine.MaterializeEnemy(enemy.Enemy, enemy.Scale), seed, stage, name });
        var result = (BattleResult)Read("_result")!;
        var field = (BattlefieldView3D)Read("_battleField")!;
        int Count(BattleEventKind kind, string? label = null) => result.Events.Count(e => e.Kind == kind && (label is null || e.Text == label));
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < 1200 && (bool)Read("_playing")!; k++) await Wait(0.05);
            Require(!(bool)Read("_playing")!, "実戦の再生完走");
            Require(field.InterruptPlays == Count(BattleEventKind.ShockGauge, ShockGaugeLabels.Interrupt), "割り込み件数");
            Require(field.CloudPlays == Count(BattleEventKind.ShockGauge, ShockGaugeLabels.Cloud), "雷雲件数");
            Require(field.CowerPlays == Count(BattleEventKind.ShockGauge, ShockGaugeLabels.Cower), "怖気件数");
            Require(field.ChargePlays == result.Events.Count(e => e.Kind == BattleEventKind.ShockGauge
                && e.Text is ShockGaugeLabels.ChargeGain or ShockGaugeLabels.ChargeSpent or ShockGaugeLabels.ChargeDrained), "蓄電件数");
            Require(field.PowderMainPlays == result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.PowderRoute == PowderRoute.Main), "主目標への粉");
            Require(field.PowderSpreadPlays == result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.PowderRoute == PowderRoute.Spread), "隣への粉");
            Require(field.PowderLeakPlays == result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.PowderRoute == PowderRoute.Leak), "味方への粉");
            Require(field.ThreadPlays == result.Events.Count(e => e.Kind == BattleEventKind.Discharge && e.SourceTrait == TraitId.Thread), "糸の放電件数");
            Require(field.ThreadReleasePlays == result.Events.Count(e => e.Kind == BattleEventKind.Discharge && e.SourceTrait == TraitId.Thread && e.Text == ThreadLabels.Release), "解除時の放電件数");
            Require(field.MarkLayerPlays == Count(BattleEventKind.MarkLayer) && field.ScarPlays == Count(BattleEventKind.Scar), "標と爪痕の件数");
            Require(field.MisaShots == MisaPresentation.Build(result.Events).HitsByCue.Count, "光線の発数");
            foreach (var pawn in field.Pawns.Values)
            {
                var lastHp = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind is BattleEventKind.Damage
                    or BattleEventKind.Heal or BattleEventKind.Revive or BattleEventKind.Death or BattleEventKind.Scar);
                if (lastHp is not null) Require(pawn.Hp == Math.Max(0, lastHp.HpAfter), $"最終HP id={pawn.InstanceId}");
                var scar = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.Scar);
                if (scar is not null) Require(pawn.MaxHp == scar.Slot, "最終最大HP");
                var mark = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.MarkLayer);
                Require(pawn.MarkLayers == (mark?.Amount ?? 0), "最終標層数");
                var cloud = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.ShockGauge && e.Text == ShockGaugeLabels.Cloud);
                Require(pawn.Thundercloud == (cloud?.Amount ?? 0), "最終雷雲");
                var charge = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.ShockGauge
                    && e.Text is ShockGaugeLabels.ChargeGain or ShockGaugeLabels.ChargeSpent or ShockGaugeLabels.ChargeDrained);
                Require(pawn.StoredCharge == (charge?.Amount ?? 0), "最終蓄電");
                Require(!pawn.ShockMarkActive, "終了で常駐効果を止める");
            }
            GD.Print($"SHOCK_MARK_REPLAY_OK {name} pass={pass} interrupt={field.InterruptPlays} cloud={field.CloudPlays} powder={field.PowderMainPlays}/{field.PowderSpreadPlays}/{field.PowderLeakPlays} thread={field.ThreadPlays}/{field.ThreadReleasePlays} beam={field.MisaShots} scar={field.ScarPlays} mark={field.MarkLayerPlays}");
            if (pass == 0) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
        }
        main.QueueFree(); await Wait(0.2);
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null || DisplayServer.GetName() == "headless") return;
        DirAccess.MakeDirRecursiveAbsolute(arg[14..]);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(System.IO.Path.Combine(arg[14..], name + ".png")) == Error.Ok, "画像保存");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
