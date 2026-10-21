using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 起動すると5体／1体・傷の有無・弾切れを順番に見られる。--verify は実戦と再戦も照合する。
public partial class GatherCastCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private BattlefieldView3D _field = null!;
    private Label _label = null!;
    internal bool ForceVerify { get; set; }
    private bool Verify => ForceVerify || OS.GetCmdlineUserArgs().Contains("--verify");
    private bool Capture => OS.GetCmdlineUserArgs().Contains("--capture");
    public override async void _Ready()
    {
        try
        {
            _field = new BattlefieldView3D(); _field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_field);
            _label = new Label { Position = new Vector2(24, 104) };
            _label.AddThemeFontSizeOverride("font_size", 22); AddChild(_label);
            bool clip = OS.GetCmdlineUserArgs().Contains("--gare-clip");
            if (clip) await CaptureGareClip();
            else if (!OS.GetCmdlineUserArgs().Contains("--replay-only")) await Visuals();
            if (Verify || clip)
            {
                _field.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                bool visualOnly = OS.GetCmdlineUserArgs().Contains("--visual-only");
                if (!visualOnly && !clip)
                    foreach (string preset in new[] { "試遊・破片", "試遊・自傷" })
                        foreach (int stage in new[] { 0, 2 }) await Replay(preset, stage);
                GD.Print(clip ? "GARE_PRISM_CAPTURE_OK" : visualOnly ? "GATHER_VISUAL_CHECK_OK" : "GATHER_CAST_CHECK_OK pick stock empty wounds heal focus dry fallout clock replay cleanup");
                if (ForceVerify) GD.Print("SUSU_PORTRAIT_CHECK_OK current-wounds grimoire cast dry fallout replay");
                foreach (var tween in GetTree().GetProcessedTweens()) tween.Kill();
                foreach (Node child in GetChildren()) child.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                GC.Collect(); GC.WaitForPendingFinalizers();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); GetTree().Quit();
            }
            else _label.Text = "確認完了：ガレの回収・装填・集束／アカの詠唱・槍・赤い霧（再実行で一巡）";
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private void Reset(int enemies = 5, bool mirror = false)
    {
        var opening = new[] {
            new DemoOpening(1, mirror ? 1 : 0, "gare", "礫のガレ", 3, 62, 62, 9, AttackPattern.All, false),
            new DemoOpening(2, mirror ? 1 : 0, "susu", "血詠みのアカ", 4, 60, 60, 8, AttackPattern.All, false),
            new DemoOpening(3, mirror ? 1 : 0, "gald", "傷ついた味方", 0, 40, 100, 8, AttackPattern.Single, false),
        }.Concat(Enumerable.Range(0, enemies).Select(i => new DemoOpening(10 + i, mirror ? 0 : 1,
            "soldier", "標的", i, 100, 100, 8, AttackPattern.Single, false))).ToArray();
        _field.BeginBattle(opening, "ガレとアカ：演出確認", 0);
        _field.SetGatherClock(false, 1);
    }
    private BattlePawn3D Pawn(int id) => _field.FindPawn(id)!;
    private Camera3D BattleCamera => (Camera3D)typeof(BattlefieldView3D).GetField("_camera", Flags)!.GetValue(_field)!;
    private Vector2 CameraOffset => new(BattleCamera.HOffset, BattleCamera.VOffset);
    private BattlePawn3D[] Targets => _field.Pawns.Values.Where(p => p.InstanceId >= 10).ToArray();
    private async Task Visuals()
    {
        Reset();
        _label.Text = "ガレ：味方の破片を拾い、袋に光を溜める";
        var pick = _field.PickPebbles(Pawn(1), Pawn(3), 36, 36);
        await pick.Finished;
        Require(_field.PebblesShown(1) == 36, "通知の残量を袋に表示");
        await Snapshot("gare-stock");
        _label.Text = "ガレ：装填 → 5体へ扇状に撃つ";
        var wide = _field.GatherVolley(Pawn(1), Targets, true, 36);
        float wideWidth = _field.LastGatherWidth;
        await SnapAt(wide, .16f, "gare-load");
        await SnapAt(wide, .51f, "gare-five");
        Require(CameraOffset.IsZeroApprox(), "集団向け弾幕には集束砲の揺れを付けない");
        Require(wide.GareCrystalCount >= 100 && !wide.GareFocused, "集団へ結晶と破片を広く飛ばす");
        Require(Pawn(1).GatherRecoilDistance > .7f && Pawn(1).GatherRecoilLift > .25f, "弾幕の反動で後ろへ跳ね飛ぶ");
        await wide.Finished;
        Require(Pawn(1).MovementPortrait is null && Pawn(1).IdleLoopPlaying, "射撃後は待機ループへ復帰");
        Require(Pawn(1).Position.IsEqualApprox(Pawn(1).Home), "跳ね飛び後も編成位置へ戻る");
        Reset(1); _label.Text = "ガレ：敵が1体なら、太い結晶の光に集束";
        var focused = _field.GatherVolley(Pawn(1), Targets, true, 36);
        float chargeDelay = GatherCast3D.GareFocusExtraCharge;
        Vector2 cameraBase = CameraOffset;
        Require(_field.LastGatherWidth > wideWidth * 2, "同じ礫で1体への束が太くなる");
        await SnapAt(focused, .72f, "gare-focus-charge");
        Require(!focused.Impact.IsCompleted && Pawn(1).GatherRecoilDistance == 0, "長い溜め中は命中も反動も先取りしない");
        Require(CameraOffset.IsEqualApprox(cameraBase), "着弾前は画面を揺らさない");
        _field.SetGatherClock(true, 1); float chargeAge = focused.Age;
        await Wait(.06); Require(focused.Age == chargeAge, "溜めの光と音の時計も停止");
        _field.SetGatherClock(false, 1);
        await SnapAt(focused, .51f + chargeDelay, "gare-one");
        Require(focused.GareFocused && focused.GareRingCount >= 5, "単体へ複数の光輪で収束させる");
        await SnapAt(focused, .64f + chargeDelay, "gare-focus-rings");
        await SnapAt(focused, .76f + chargeDelay, "gare-focus-impact");
        Require(!CameraOffset.IsEqualApprox(cameraBase), "集束砲の着弾に合わせて小さく揺れる");
        _field.SetGatherClock(true, 1); Vector2 shakeHeld = CameraOffset;
        await Wait(.06); Require(CameraOffset.IsEqualApprox(shakeHeld), "停止中は揺れも止まる");
        _field.SetGatherClock(false, 1);
        await focused.Finished;
        Require(CameraOffset.IsEqualApprox(cameraBase), "揺れが減衰して元の視点へ戻る");
        var shakeCancel = _field.GatherVolley(Pawn(1), Targets, true, 36);
        shakeCancel._Process(1.42);
        Require(!CameraOffset.IsEqualApprox(cameraBase), "中断の前に実際に揺らす");
        shakeCancel.Cancel();
        Require(CameraOffset.IsEqualApprox(cameraBase), "着弾直後の中断でも揺れを解除");
        var empty = _field.EmptyPebbles(Pawn(1)); _label.Text = "ガレ：空の袋を逆さにする";
        await SnapAt(empty, .22f, "gare-empty"); await empty.Finished;
        Require(_field.PebblesShown(1) == 0, "弾切れで袋の光が消える");
        Reset(); _label.Text = "アカ：今ある傷だけを魔導書へ詠み込む";
        var read = _field.ReadWounds(Pawn(2));
        int deep = _field.LastBloodMotes;
        Require(deep > 0 && BattlefieldView3D.WoundMotes(Pawn(1)) == 0, "満タンの味方から粒を出さない");
        await SnapAt(read, .18f, "aka-read"); await read.Finished;
        Pawn(3).SetHp(88);
        var healed = _field.ReadWounds(Pawn(2));
        Require(_field.LastBloodMotes < deep, "回復後は流れが細る"); await healed.Finished;
        var rain = _field.GatherVolley(Pawn(2), Targets, false); _label.Text = "アカ：5体の頭上から赤い針を降らせる";
        await SnapAt(rain, .50f, "aka-five"); await rain.Finished;
        Reset(1); _label.Text = "アカ：敵1体の頭上へ術式を絞り、太い一本の槍にする";
        var spear = _field.GatherVolley(Pawn(2), Targets, false);
        await SnapAt(spear, .50f, "aka-one"); await spear.Finished;
        Pawn(3).SetHp(100);
        var dry = _field.DryBlood(Pawn(2), Targets[0]); _label.Text = "アカ：傷がなければ小さな光だけ";
        await SnapAt(dry, .18f, "aka-dry"); await dry.Finished;
        Pawn(3).SetHp(40);
        _field.BloodFallout(Pawn(2), new[] { Pawn(3) });
        Pawn(2).SetHp(0); Pawn(2).AnimateDeath();
        _label.Text = "アカ：倒れると、隣へ赤い光の霧が降りかかる";
        await Wait(.56); await Snapshot("aka-fallout"); await Wait(.3);

        // 停止中の命中・姿勢変更を防ぎ、途中の速度変更にも同じ時計で追従する。
        Reset(1, mirror: true);
        var clock = _field.GatherVolley(Pawn(1), Targets, true, 60);
        _field.SetGatherClock(true, 1); float age = clock.Age;
        await Wait(.06); Require(clock.Age == age && !clock.Impact.IsCompleted, "停止中は命中しない");
        _field.SetGatherClock(false, 1); clock._Process(.53 + chargeDelay);
        _field.SetGatherClock(true, 1);
        Vector3 recoil = Pawn(1).Position;
        Require(recoil.X > Pawn(1).Home.X && !clock.Impact.IsCompleted, "敵側は右へ跳ね、着弾を先取りしない");
        await Wait(.06);
        Require(Pawn(1).Position.IsEqualApprox(recoil), "空中の反動も停止する");
        _field.SetGatherClock(false, 4); await clock.Finished;
        Require(Pawn(1).MovementPortrait is null && Pawn(1).Position.IsEqualApprox(Pawn(1).Home), "敵側も元の席へ復帰");
        _field.SetGatherClock(false, 1);
        var interrupted = _field.GatherVolley(Pawn(1), Targets, true, 60);
        interrupted._Process(.50 + chargeDelay);
        Require(Pawn(1).GatherRecoilDistance > .7f, "死亡前に実際の跳ね飛びを通る");
        Pawn(1).AnimateDeath(); interrupted.Cancel();
        Require(Pawn(1).Position.IsEqualApprox(Pawn(1).Home) && Pawn(1).MovementPortrait is null, "空中で死亡しても反動を解除");
        var cancel = _field.ReadWounds(Pawn(2));
        _field.EndGatherPresentation();
        Require(!await cancel.Finished && !_field.GatherEffects.Any(), "中断は待機を解放して演出を除去");
        Reset(1);
        Require(!_field.GatherEffects.Any() && _field.PebblesShown(1) == 0, "再戦の袋と光を初期化");
    }
    private async Task Replay(string name, int stage)
    {
        _label.Text = $"実戦照合：{name}／{stage + 1}";
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
        object? Read(string key) => typeof(Main).GetField(key, Flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        var formation = Presets.Versions.Concat(Presets.Playtest).First(p => p.Name == name).F;
        var stageDef = EnemyCatalog.PlaytestStages[stage];
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), BattleEngine.MaterializeEnemy(stageDef.Enemy, stageDef.Scale), 0, stage, name });
        var result = (BattleResult)Read("_result")!;
        var field = (BattlefieldView3D)Read("_battleField")!;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < 1400 && (bool)Read("_playing")!; k++) await Wait(.025);
            Require(!(bool)Read("_playing")!, "実戦と再戦を完走");
            int Expected(string label) => result.Events.Count(e => e.Kind == BattleEventKind.Shard && e.Text == label);
            Require(field.PebblePickPlays == Expected(ShardLabels.Pick) && field.PebbleVolleyPlays == Expected(ShardLabels.Throw)
                && field.PebbleEmptyPlays == Expected(ShardLabels.Empty), "回収・射撃・弾切れの件数が台本と一致");
            Require(field.BloodReadPlays == result.Events.Count(e => e.Kind == BattleEventKind.Skill && e.Text == AshActionLabels.Release),
                "傷の詠唱件数が台本と一致");
            Require(!field.GatherEffects.Any(), "再生終了時に一時演出が残らない");
            GD.Print($"GATHER_REPLAY_OK {name} stage={stage} pass={pass} pick={field.PebblePickPlays} shot={field.PebbleVolleyPlays} empty={field.PebbleEmptyPlays} read={field.BloodReadPlays}");
            if (pass == 0) main.Call("ReplayBattle");
        }
        main.Call("ReturnToFormation"); main.QueueFree(); await Wait(.03);
    }
    private async Task CaptureGareClip()
    {
        string dir = ProjectSettings.GlobalizePath("res://../.tmp/gare-prism-clip");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        int frame = 0;
        foreach (int enemies in new[] { 5, 1 })
        {
            Reset(enemies);
            _label.Text = enemies == 5 ? "ガレ：結晶を散らす弾幕" : "ガレ：光と音を溜め、一気に放つ集束砲";
            _field.SetGatherClock(true, 1);
            var shot = _field.GatherVolley(Pawn(1), Targets, true, 36);
            for (int k = 0; k < (enemies == 1 ? 90 : 60); k++)
            {
                if (!shot.Finished.IsCompleted)
                {
                    shot.SetClock(false, 1); shot._Process(1.0 / 30); shot.SetClock(true, 1);
                }
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng($"{dir}/frame-{frame++:D4}.png") == Error.Ok, "動画のフレーム保存");
            }
        }
    }
    private async Task SnapAt(GatherCast3D fx, float time, string name)
    {
        await Wait(Math.Max(0, time - fx.Age));
        _field.SetGatherClock(true, 1); await Snapshot(name); _field.SetGatherClock(false, 1);
    }
    private async Task Snapshot(string name)
    {
        if (!Capture) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string dir = ProjectSettings.GlobalizePath("res://../design/art/gare-aka");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        Require(GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png") == Error.Ok, "画面保存");
    }
    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(Math.Max(.001, seconds)), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string label)
    { if (!ok) throw new InvalidOperationException(label); }
}
