using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

// 本番の入口で実台本の件数・最終HPを照合し、素材時計と中断も確かめる。
public partial class ChiriCheck : Control
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private Main _main = null!;
    private T Read<T>(string name) => (T)typeof(Main).GetField(name, Flags)!.GetValue(_main)!;
    private void Call(string name, params object[] args) => typeof(Main).GetMethod(name, Flags)!.Invoke(_main, args);
    // 表示用の写しを持つ実装では元台本も照合し、その窓口がない版では本体の台本を使う。
    private BattleResult SourceResult => typeof(Main).GetProperty("SourceBattleResult", Flags)?.GetValue(_main)
        as BattleResult ?? Read<BattleResult>("_result");

    public override async void _Ready()
    {
        try
        {
            _main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
            AddChild(_main);
            string? capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?["--capture-dir=".Length..];
            if (capture is not null) { await Capture(capture); GetTree().Quit(); return; }
            _main.ViewerFastPlayback = true;
            _main.SetPlaybackSpeed(1000);
            Read<SpinBox>("_seed").Value = 0;
            var rows = Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).Concat(Presets.Versions).ToArray();
            int preset = Array.FindIndex(rows, r => r.Name == "試遊・導火線 チリ");
            Require(preset >= 0, "チリの試遊台がある"); Call("ApplyPreset", preset);
            int boss = EnemyCatalog.Stages.Count + 1 + EnemyCatalog.TestStages.Count + 1;
            foreach (int stage in new[] { boss, 4, boss + 1 })
            {
                Read<OptionButton>("_stagePicker").Select(stage); Call("StartBattle");
                var result = Read<BattleResult>("_result");
                string before = JsonSerializer.Serialize(result.Events);
                string sourceBefore = JsonSerializer.Serialize(SourceResult.Events);
                await Until(() => !_main.IsPlaying);
                CheckReplay(result);
                Require(JsonSerializer.Serialize(result.Events) == before, "再生側の台本を変えていない");
                Require(JsonSerializer.Serialize(SourceResult.Events) == sourceBefore, "BattleCoreの台本を変えていない");
                Call("ReplayBattle"); await Until(() => !_main.IsPlaying); CheckReplay(result);
                if (stage == boss + 1)
                {
                    var f = _main.ViewerField;
                    Require(f.ChiriPlants == 9 && f.ChiriBlasts + f.ChiriMirrors == 5
                        && f.ChiriMirrors == 1 && f.ChiriDuds == 4, "近衛seed0の診断の件数に一致");
                }
                var field = _main.ViewerField;
                GD.Print($"CHIRI_REPLAY_OK stage={stage} events={result.Events.Count} replay=True hp=True"
                    + $" plant={field.ChiriPlants} fuse={field.ChiriFuses} blast={field.ChiriBlasts} mirror={field.ChiriMirrors} dud={field.ChiriDuds}");
                Call("ReturnToFormation");
            }
            await CheckClock();
            await CheckShake();
            await CheckScriptTargets();
            GD.Print("CHIRI_CHECK_COMPLETE ok=True"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckReplay(BattleResult result)
    {
        var f = _main.ViewerField;
        int Count(string text) => result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.Text == text);
        Require(f.ChiriPlants == Count(BattleContext.BombPlantText), "設置件数");
        Require(f.ChiriFuses == Count(BattleContext.BombFuseText), "導火線件数");
        Require(f.ChiriBlasts == Count(BattleContext.BombBlastText), "敵の爆発件数");
        Require(f.ChiriMirrors == Count(BattleContext.BombMirrorText), "転送件数");
        Require(f.ChiriShakePlays == f.ChiriBlasts + f.ChiriMirrors, "揺れは起爆ごとに1回で巻き込みには追加しない");
        Require(f.ChiriDuds == Count(BattleContext.BombDudText), "不発件数");
        Require(f.ChiriMarkerCount == 0 && f.ChiriEffectCount == 0, "終戦後に爆弾を残さない");
        Require(!f.Pawns.Values.Any(p => p.MovementPortrait is "chiri_plant" or "chiri_release"), "行動差分を終戦へ持ち込まない");
        foreach (var pawn in f.Pawns.Values)
        {
            var last = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId
                && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Revive);
            if (last is not null) Require(pawn.Hp == last.HpAfter, "最終HPが台本と一致する");
            Require(!pawn.HasStatusIcon(BattleContext.BombPlantText), "汎用状態アイコンを付けない");
        }
    }

    internal static DemoOpening[] Openings(bool mirror = false) => new DemoOpening[] {
        new(1, mirror ? 1 : 0, "chiri", "導火線のチリ", 3, 60, 60, 3, AttackPattern.Single, false),
        new(2, mirror ? 1 : 0, "sero", "味方", 2, 1000, 1000, 5, AttackPattern.Single, true),
        new(3, mirror ? 0 : 1, "knight", "敵1", 0, 1000, 1000, 10, AttackPattern.Single, true),
        new(4, mirror ? 0 : 1, "knight", "敵2", 1, 1000, 1000, 10, AttackPattern.Single, true),
        new(5, mirror ? 0 : 1, "knight", "敵3", 4, 1000, 1000, 10, AttackPattern.Single, true),
    };

    private async Task CheckClock()
    {
        var f = _main.ViewerField; f.Visible = true; f.BeginBattle(Openings(), "時計", 0);
        var actor = f.FindPawn(1)!; var holder = f.FindPawn(3)!;
        Require(actor.IdleLoopPlaying && actor.IdleLoopFrameCount == 20, "本番の開戦からチリの待機A");
        f.SetDeathClock(false, 1); await Wait(.10);
        f.SetDeathClock(true, 1);
        int pausedFrame = actor.IdleLoopFrame;
        await Wait(.16);
        Require(actor.IdleLoopFrame == pausedFrame, "通常プレイの停止時計でチリの待機も止まる");
        var cast = f.BeginChiriCast(actor, true);
        Require(actor.MovementPortrait == "chiri_plant", "仕掛ける差分を使用");
        f.PlantChiriBomb(actor, holder, 3);
        var marker = f.ChiriMarker(3)!;
        double age = marker.Get("age").AsDouble();
        await Wait(.12);
        Require(marker.Get("age").AsDouble() == age && !cast.IsCompleted, "停止中は素材・待ち手とも停止");
        Require(actor.MovementPortrait == "chiri_release", "手を離す差分を使用");
        f.SetDeathClock(false, 2);
        await Wait(.08);
        Require(marker.Get("age").AsDouble() - age >= .10, "2倍速で素材時計が進む");
        var oldPosition = marker.GlobalPosition; holder.Position += Vector3.Right;
        await Frame(); await Frame();
        Require(Math.Abs(marker.GlobalPosition.X - oldPosition.X - 1) < .01, "持ち主の移動に印が追従");
        foreach (int remaining in new[] { 2, 1, 0 })
        { f.ShortenChiriFuse(holder, remaining); Require(marker.Get("remaining").AsInt32() == remaining, "台本の残り段数を使う"); }
        holder.AnimateDeath(); f.DudChiriBomb(holder);
        Require(f.ChiriMarkerCount == 0 && marker.Get("remaining").AsInt32() == 0, "死亡後の不発で火花を止める");
        await Wait(.40);
        Require(!IsInstanceValid(marker) || marker.IsQueuedForDeletion(), "不発は爆発せず消える");
        await f.FinishChiriPlant(actor);
        Require(actor.MovementPortrait is null && actor.IdleLoopPlaying && actor.IdleLoopFrameCount == 20, "待機Aへ復帰");
        var cue = new ChiriPresentation.Burst(0, new BattleEvent {
            Kind = BattleEventKind.StatusGain, Turn = 1, Text = BattleContext.BombMirrorText, ActorId = 1, TargetId = 2, FromId = 5, Amount = 20 }, 5, false);
        f.SetDeathClock(true, 1);
        var flight = f.ShowChiriBurst(cue);
        await Wait(.08);
        Require(!flight.IsCompleted && f.ChiriImpactTargets.Count == 0, "停止中の火球は到達先を爆発させない");
        f.BeginBattle(Openings(true), "再戦", 0);
        await Until(() => flight.IsCompleted); await flight; await Frame();
        Require(f.ChiriMarkerCount == 0 && f.ChiriEffectCount == 0, "中断・再戦で火球と印を残さない");
        f.SetDeathClock(false, 1); f.PlantChiriBomb(f.FindPawn(1), f.FindPawn(2), 3);
        f.Visible = false; await Frame();
        Require(f.ChiriEffectCount == 0 && f.ChiriMarkerCount == 0, "編成へ戻ると全素材を片付ける");
        GD.Print("CHIRI_CLOCK_OK pause speed follow fuse dud pose cancel replay hide=True");
    }

    private static BattleEvent Status(string text, int target, int amount = 3, int? from = null)
        => new() { Kind = BattleEventKind.StatusGain, Turn = 1, ActorId = 1, TargetId = target, Amount = amount, Text = text, FromId = from };

    private async Task CheckShake()
    {
        var f = _main.ViewerField; f.Visible = true; f.BeginBattle(Openings(), "起爆の画面揺れ", 0);
        var camera = (Camera3D)typeof(BattlefieldView3D).GetField("_camera", Flags)!.GetValue(f)!;
        Vector3 local = camera.Position, world = camera.GlobalPosition;
        float fov = camera.Fov, oldH = camera.HOffset, oldV = camera.VOffset;
        // 他の技が投影オフセットを使っていても、その値や視点のTweenを上書きしない。
        camera.HOffset = .013f; camera.VOffset = -.007f;
        ChiriPresentation.Burst Cue(int index) => new(index, Status(BattleContext.BombBlastText, 3, 20), 3, false);
        f.SetDeathClock(false, 1);
        var burst = f.ShowChiriBurst(Cue(0));
        Require(f.ChiriShakeActive && f.ChiriShakeDisplacement.Length() > .01f, "起爆の瞬間に揺れが始まる");
        Require(!camera.GlobalPosition.IsEqualApprox(world) && camera.Position.IsEqualApprox(local)
            && camera.Fov == fov && camera.HOffset == .013f && camera.VOffset == -.007f, "構図と他の技の投影を保って揺れる");
        f.ShowChiriDamage(Cue(0), f.FindPawn(5));
        Require(f.ChiriShakePlays == 1, "巻き込みで揺れを追加しない");
        f.SetDeathClock(true, 1);
        Vector3 frozen = f.ChiriShakeDisplacement;
        await Wait(.08);
        Require(f.ChiriShakeDisplacement.IsEqualApprox(frozen), "一時停止で揺れも止まる");
        f.SetDeathClock(false, 2); await Wait(.12); await burst;
        Require(!f.ChiriShakeActive && f.ChiriShakeDisplacement.IsZeroApprox()
            && camera.GlobalPosition.IsEqualApprox(world), "倍速で短い揺れが終わり、視点が元へ戻る");
        f.SetDeathClock(false, 1);
        var chain1 = f.ShowChiriBurst(Cue(1)); var chain2 = f.ShowChiriBurst(Cue(2));
        Require(f.ChiriShakePlays == 3 && f.ChiriShakeDisplacement.Length() <= .052f, "誘爆が重なっても振幅を加算しない");
        typeof(BattlefieldView3D).GetMethod("ApplyCameraView", Flags)!.Invoke(f, null);
        Require(!f.ChiriShakeActive && f.ChiriShakeDisplacement.IsZeroApprox(), "視点調整時は揺れを消す");
        var interrupted = f.ShowChiriBurst(Cue(3));
        f.BeginBattle(Openings(), "再戦", 0);
        Require(!f.ChiriShakeActive && f.ChiriShakeDisplacement.IsZeroApprox(), "再戦時に揺れを残さない");
        var hidden = f.ShowChiriBurst(Cue(4)); f.Visible = false;
        Require(!f.ChiriShakeActive && f.ChiriShakeDisplacement.IsZeroApprox(), "非表示で揺れを即座に解除");
        await Task.WhenAll(chain1, chain2, interrupted, hidden);
        camera.HOffset = oldH; camera.VOffset = oldV;
        GD.Print("CHIRI_SHAKE_OK impact splash pause speed chain camera replay hide=True");
    }

    private static BattleEvent[] Script() => new BattleEvent[] {
        new() { Kind = BattleEventKind.TurnStart, Turn = 1 },
        new() { Kind = BattleEventKind.Skill, Turn = 1, ActorId = 1, Text = "仕掛ける" },
        Status(BattleContext.BombPlantText, 3), Status(BattleContext.BombPlantText, 2), Status(BattleContext.BombPlantText, 4),
        Status(BattleContext.BombFuseText, 3, 2), Status(BattleContext.BombFuseText, 3, 1), Status(BattleContext.BombFuseText, 3, 0),
        Status(BattleContext.BombBlastText, 3, 20),
        new() { Kind = BattleEventKind.Highlight, Turn = 1, ActorId = 3, Text = "爆弾の見出し" },
        new() { Kind = BattleEventKind.Damage, Turn = 1, ActorId = 1, TargetId = 3, Amount = 20, HpAfter = 980 },
        new() { Kind = BattleEventKind.Damage, Turn = 1, ActorId = 1, TargetId = 5, Amount = 20, HpAfter = 980 },
        Status(BattleContext.BombMirrorText, 2, 20, 5),
        new() { Kind = BattleEventKind.Highlight, Turn = 1, ActorId = 2, Text = "転送の見出し" },
        new() { Kind = BattleEventKind.Damage, Turn = 1, ActorId = 1, TargetId = 5, Amount = 20, HpAfter = 960 },
        new() { Kind = BattleEventKind.Death, Turn = 1, TargetId = 4 },
        Status(BattleContext.BombDudText, 4, 0),
    };

    private async Task CheckScriptTargets()
    {
        foreach (bool mirrored in new[] { false, true })
        {
            _main.ViewerField.Visible = true; _main.SetPlaybackSpeed(1000);
            _main.EnterScripted(Openings(mirrored), Script(), "対象の確認");
            await Until(() => !_main.IsPlaying);
            var f = _main.ViewerField;
            Require(f.ChiriImpactTargets.SequenceEqual(new[] { (8, 3), (8, 5), (12, 5) }),
                "席を再計算せず台本にある持ち主・非隣接の対象だけへ爆発");
            Require(f.ChiriMirrorDestinations.SequenceEqual(new[] { 5 }), "FromIdの敵だけへ転送し味方を爆発させない");
        }
        GD.Print("CHIRI_TARGETS_OK scripted sparse mirror=True");
    }

    private async Task Capture(string directory)
    {
        Directory.CreateDirectory(directory);
        _main.SetPlaybackSpeed(.75);
        _main.ViewerFastPlayback = false;
        _main.EnterScripted(Openings(), Script(), "チリ — 本番経路の演出確認");
        await Until(() => _main.ViewerField.ChiriPlants == 3);
        await Wait(.48); _main.PausePlayback(); await Save("plant.png");
        _main.ResumePlayback();
        await Until(() => _main.ViewerField.ChiriBlasts == 1);
        await Wait(.12); _main.PausePlayback(); await Save("blast.png");
        _main.ResumePlayback();
        await Until(() => _main.ViewerField.ChiriMirrors == 1);
        await Wait(.42); _main.PausePlayback(); await Save("mirror.png");
        _main.ResumePlayback(); await Until(() => !_main.IsPlaying);
        GD.Print("CHIRI_CAPTURE_COMPLETE " + directory);
        async Task Save(string name)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, name));
        }
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Until(Func<bool> done)
    {
        ulong until = Time.GetTicksMsec() + 60000;
        while (!done() && Time.GetTicksMsec() < until)
        { if (_main.ViewerPlaybackError is { } error) throw error; await Frame(); }
        Require(done(), "時間内に再生が完了");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
