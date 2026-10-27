using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// 本編Mainと同じ経路で、連撃の接頭列・接触・帰還・中断と実台本を確かめる。
public partial class UtsuDanceCheck : Control
{
    private Main _main = null!;
    private int _assertions;
    private bool _movie;
    private readonly List<object> _segments = new();
    public override async void _Ready()
    {
        try
        {
            _movie = OS.GetCmdlineUserArgs().Contains("--movie");
            CheckAssets(); CheckIndex();
            _main = new Main { ViewerMode = true }; _main.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_main);
            _main.ViewerField.UtsuEffectsEnabled = !OS.GetCmdlineUserArgs().Contains("--bare");
            _main.ViewerFastPlayback = !_movie; _main.ViewerLeadInSeconds = _movie ? .55 : 0;
            foreach (int count in OS.GetCmdlineUserArgs().Contains("--two-only") ? new[] { 2 }
                : OS.GetCmdlineUserArgs().Contains("--five-only") ? new[] { 5 } : new[] { 2, 3, 4, 5 }) await Run(count, mirror: false, changeTargets: false);
            if (!_movie)
            {
                await Run(1, false, false);
                await Run(5, true, false);
                await Run(5, false, true);
                _main.ViewerField.UtsuEffectsEnabled = false;
                await Run(2, false, false);
                _main.ViewerField.UtsuEffectsEnabled = true;
                await StepPoseCheck();
                await PauseAndCancel();
                await RealBattle();
                await SeekAndReplay();
            }
            await _main.StopViewerPlayback(); _main.QueueFree(); await Frame(); await Frame();
            if (_movie)
            {
                string path = ProjectSettings.GlobalizePath("res://../design/art/utsu/attack-flow-v4/capture-segments.json");
                if (OS.GetCmdlineUserArgs().Contains("--bare")) path = path.Replace("capture-segments.json", "capture-bare.json");
                if (OS.GetCmdlineUserArgs().Contains("--bare") && OS.GetCmdlineUserArgs().Contains("--five-only")) path = path.Replace("capture-bare.json", "capture-bare-five.json");
                File.WriteAllText(path, JsonSerializer.Serialize(new { fps = 30, speed = 1, segments = _segments }, new JsonSerializerOptions { WriteIndented = true }));
            }
            GD.Print($"UTSU_DANCE_CHECK_OK checks={_assertions} prefix=2,3,4,5 contact-once dual-blades pause speed mirror death cancel reset seek replay actual-hp idle-return movie={_movie}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckAssets()
    {
        // 顔の縦線を手で測る方式では顔幅の違いを見逃す。共通頭部の実表示倍率を照合する。
        string path = ProjectSettings.GlobalizePath("res://../design/art/utsu/attack-flow-v4/cel-animation.json");
        using var spec = JsonDocument.Parse(File.ReadAllText(path));
        Vector2 Point(JsonElement p) => new(p[0].GetSingle(), p[1].GetSingle());
        var bodyPoses = spec.RootElement.GetProperty("poses").EnumerateArray().Where(p => p.TryGetProperty("body", out _)).ToArray();
        var ready = bodyPoses.Single(p => p.GetProperty("key").GetString() == "ready");
        Require(bodyPoses.Length == spec.RootElement.GetProperty("poses").GetArrayLength(), "全姿勢に配置用の基準点がある");
        float HeadLength(JsonElement pose) => Point(pose.GetProperty("head")[0]).DistanceTo(Point(pose.GetProperty("head")[1])) * pose.GetProperty("scale").GetSingle();
        foreach (var pose in bodyPoses)
        {
            if (pose.TryGetProperty("headPart", out var part))
            {
                Require(spec.RootElement.GetProperty("headParts").TryGetProperty(part.GetProperty("id").GetString()!, out _)
                    && !part.TryGetProperty("scale", out _), "各差分は同じ頭部パーツと共通倍率を使う");
                Require(pose.GetProperty("sheet").GetString()!.EndsWith("-refit.png"), "途中に旧体格の身体セルが混ざらない");
            }
            else Require(pose.GetProperty("sheet").GetString()!.EndsWith("idle_loop/utsu/utsu_idle_00.png"), "帰還は採用済み待機Aそのもの");
            // 投影した折れ線の合計長は体格一致を保証しない。体格は等倍全身比較と実描画で確認する。
        }
        Require(Math.Abs(spec.RootElement.GetProperty("headPartScale").GetSingle() * 555 / HeadLength(ready) - 1) < .001f,
            "共通頭部の寸法を採用待機Aの頭部寸法に合わせる");
        Require(Math.Abs(ready.GetProperty("scale").GetSingle() * spec.RootElement.GetProperty("clipScale").GetSingle() / 1536 - 1f / 960) < .000001f, "帰還セルと待機Aの実表示倍率が同じ");
        using var readyImage = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://../design/art/utsu/attack-flow-v4/poses/ready.png"));
        using var returnImage = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://../design/art/utsu/attack-flow-v4/poses/ready_new.png"));
        Require(readyImage.GetData().SequenceEqual(returnImage.GetData()), "待機直前の類似構えも同一原画に統一");
        var cross = bodyPoses.Single(p => p.GetProperty("key").GetString() == "cross5");
        for (int blade = 0; blade < 2; blade++)
        {
            Vector2 root = Point(cross.GetProperty("blades")[blade][0]);
            Vector2 tip = Point(cross.GetProperty("blades")[blade][1]);
            Vector2 pommel = Point(cross.GetProperty("pommels")[blade]);
            Require((tip-root).Normalized().Dot((pommel-root).Normalized()) < -.99f, "クロス斬りの各刀身と柄が一直線につながる");
            Require(tip.Y < root.Y && pommel.Y > root.Y, "交差する二本とも切先が鍔の上、柄が下");
        }
        for (int hit = 1; hit <= 5; hit++)
        {
            var end = BattlePawn3D.AttackClipOf($"utsu_hit{hit}_end_v4");
            Require(end is not null && end.Contact > 0 && end.Contact < end.Frames.Length, "各打に接触コマ");
            Require(end!.Frames.All(f => f.GetSize() == new Vector2(1600, 1100)), "全身の共通キャンバス");
            string motionPath = ProjectSettings.GlobalizePath($"res://assets/portraits/battle/attack_clip/utsu_hit{hit}_end_v4/motion.json");
            using var motion = JsonDocument.Parse(File.ReadAllText(motionPath));
            Require(motion.RootElement.GetProperty("activeBlade").GetInt32() == (hit == 5 ? -1 : (hit - 1) % 2), "左右交互から最後だけ双剣同時へ");
            var frames = motion.RootElement.GetProperty("frames");
            Require(frames[end.Contact].GetProperty("trail").GetBoolean(), "接触時は実際の刀身に剣閃");
            Require(!frames[0].GetProperty("trail").GetBoolean() && !frames[frames.GetArrayLength()-1].GetProperty("trail").GetBoolean(), "予備動作と帰還に追加の剣閃を作らない");
            if (hit == 5) continue;
            var link = BattlePawn3D.AttackClipOf($"utsu_hit{hit}_link_v4")!;
            Require(link.Contact == end.Contact, "終わる場合と続く場合の接触時刻は同じ");
            for (int k = 0; k <= end.Contact; k++)
            {
                using var a = link.Frames[k].GetImage(); using var b = end.Frames[k].GetImage();
                Require(a.GetData().SequenceEqual(b.GetData()), "連撃段階を変えても接触までの全身原画が同じ");
            }
            var next = BattlePawn3D.AttackClipOf($"utsu_hit{hit + 1}_{(hit + 1 < 5 ? "link" : "end")}_v4")!;
            using var last = link.Frames[^1].GetImage(); using var first = next.Frames[0].GetImage();
            Require(last.GetData().SequenceEqual(first.GetData()), "前の振り抜きと次の予備動作が同じセルでつながる");
            Require(Math.Abs(link.Vertical[^1] - next.Vertical[0]) < .001f, "連撃間の跳躍高が連続");
        }
    }

    private void CheckIndex()
    {
        var openings = FxCatalog.UtsuOpenings();
        for (int count = 1; count <= 5; count++)
        {
            var events = FxCatalog.UtsuEvents(count);
            var index = UtsuPresentation.Build(events, openings);
            Require(index.Swings.Count == count, "台本にあるAttackだけを数える");
            Require(index.Swings.OrderBy(p => p.Key).Select(p => p.Value.Ordinal).SequenceEqual(Enumerable.Range(1, count)), "連撃の順番");
            Require(index.Swings.Values.All(s => s.Count == count) && !index.Swings.Values.Last().Keep, "実際の終端で着地へ分岐");
        }
        var interrupted = FxCatalog.UtsuEvents(3).ToList();
        interrupted.InsertRange(3, new[] {
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, TargetId = 1, Reaction = true },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 2, TargetId = 1, HpAfter = 290, Reaction = true }
        });
        var parsed = UtsuPresentation.Build(interrupted, openings);
        Require(parsed.Swings.Count == 3 && parsed.Swings.Values.All(s => s.Count == 3), "割り込む敵反撃をウツの追加打にしない");
        Require(!parsed.FastEvents.Contains(3) && !parsed.FastEvents.Contains(4), "敵の反撃の間は詰めない");
        var onlyDamage = new[] { new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2, HpAfter = 0 } };
        Require(UtsuPresentation.Build(onlyDamage, openings).Swings.Count == 0, "Damageから攻撃を捏造しない");
    }

    private async Task Run(int count, bool mirror, bool changeTargets)
    {
        await _main.StopViewerPlayback(); _main.SetPlaybackSpeed(_movie ? 1 : 12);
        var openings = FxCatalog.UtsuOpenings().Select(o => mirror ? o with { Team = 1 - o.Team } : o).ToArray();
        var events = FxCatalog.UtsuEvents(count, changeTargets);
        ulong begin = Engine.GetProcessFrames();
        _main.EnterScripted(openings, events, $"双剣の演舞 ／ {count}連撃", 0);
        var actor = _main.ViewerField.FindPawn(1)!;
        _main.ViewerField.SetCameraZoom(_movie ? 1.3f : 1.0f);
        await Until(() => !_main.IsPlaying);
        var field = _main.ViewerField;
        Require(field.UtsuContacts == count && field.UtsuPlayedClips.Count == count, "一打につき一度だけ命中");
        Require(field.UtsuReturns == 1 && actor.Position.DistanceTo(actor.Home) < .01f, "各段階の終わりから一度だけ席へ戻る");
        Require(actor.IdleLoopPlaying && !actor.AttackClipPlaying && actor.UtsuClipName is null, "全打のあと採用Aの待機へ戻る");
        Require(actor.DeathSprite.FlipH == mirror, "双剣も左右を反転");
        foreach (var damage in events.Where(e => e.Kind == BattleEventKind.Damage).GroupBy(e => e.TargetId).Select(g => g.Last()))
            Require(field.FindPawn(damage.TargetId)!.Hp == damage.HpAfter, "対象ごとの確定HPを維持");
        if (_movie)
        {
            await Wait(.45);
            _segments.Add(new { name = $"dance-{count}", count, start_frame = begin, end_frame = Engine.GetProcessFrames() });
        }
    }

    private async Task PauseAndCancel()
    {
        await _main.StopViewerPlayback(); _main.SetPlaybackSpeed(1);
        _main.EnterScripted(FxCatalog.UtsuOpenings(), FxCatalog.UtsuEvents(5), "停止と中断", 0);
        var actor = _main.ViewerField.FindPawn(1)!;
        await Until(() => actor.AttackClipPlaying && actor.UtsuClipName == "utsu_hit1_link_v4");
        _main.PausePlayback();
        int before = actor.AttackClipFrame; var position = actor.Position;
        await Wait(.12);
        Require(actor.AttackClipFrame == before && actor.Position == position, "停止中は原画と踏み込みが同時に止まる");
        _main.SetPlaybackSpeed(4); _main.ResumePlayback();
        await Until(() => !_main.IsPlaying);
        Require(_main.ViewerField.UtsuContacts == 5, "途中の速度変更でも5回の接触");
        await _main.StopViewerPlayback(); _main.SetPlaybackSpeed(1);
        _main.EnterScripted(FxCatalog.UtsuOpenings(), FxCatalog.UtsuEvents(5), "命中前の中断", 0);
        actor = _main.ViewerField.FindPawn(1)!;
        await Until(() => actor.AttackClipPlaying);
        Require(_main.ViewerField.UtsuContacts == 0, "中断検査は初撃の接触前");
        await _main.StopViewerPlayback();
        Require(_main.ViewerField.UtsuContacts == 0 && _main.ViewerField.Pawns.Count == 0, "中断・再戦で古い接触を作らず待ち手を解放");
        var field = _main.ViewerField;
        field.BeginBattle(FxCatalog.UtsuOpenings(), "死亡の中断", 0); field.SetDeathClock(false, 1);
        actor = field.FindPawn(1)!; var target = field.FindPawn(2)!;
        var animation = field.PlayUtsuSwing(actor, new[] { target }, new(1, 1, 5));
        await Until(() => actor.AttackClipPlaying);
        field.PlayDeath(actor);
        await animation;
        Require(field.UtsuContacts == 0 && !actor.AttackClipPlaying, "命中前の死亡でも剣閃・接触を残さない");
        field.RevivePawn(actor); Require(actor.IdleLoopPlaying, "蘇生で待機Aへ復帰");
        var cancelled = actor.PlayUtsuClip("utsu_hit4_link_v4", () => throw new InvalidOperationException("勝利の中断後に接触"), true);
        actor.AnimateVictory();
        await cancelled;
        Require(!actor.AttackClipPlaying && actor.UtsuClipName is null && actor.AttackClipVertical == 0, "空中攻撃中の勝利で姿勢・高さ・未命中の接触を解除");
    }

    private async Task StepPoseCheck()
    {
        await _main.StopViewerPlayback(); _main.SetPlaybackSpeed(1);
        _main.EnterScripted(FxCatalog.UtsuOpenings(), FxCatalog.UtsuEvents(2), "踏み込みの足と前進", 0);
        var actor = _main.ViewerField.FindPawn(1)!;
        await Until(() => actor.UtsuPose == "step_load");
        Require(actor.Position.DistanceTo(actor.Home) < .001f, "後ろ足へ荷重する間はその場にいる");
        await Until(() => actor.UtsuPose == "step_push" && actor.AttackClipFrame >= 7);
        Require(actor.Position.DistanceTo(actor.Home) > .05f, "蹴り出しセルの間に前進する");
        _main.PausePlayback();
        var position = actor.Position; int frame = actor.AttackClipFrame;
        await Wait(.1);
        Require(actor.Position == position && actor.AttackClipFrame == frame, "蹴り出し途中の停止で足と移動が同時に止まる");
        _main.ResumePlayback();
        await Until(() => actor.UtsuPose == "step_plant");
        var planted = actor.Position;
        await Until(() => actor.UtsuPose == "drive1");
        Require(actor.Position.DistanceTo(planted) < .001f, "踏着後は前進せず腰の溜めへ接続する");
        await Until(() => !_main.IsPlaying);
        Require(_main.ViewerField.UtsuContacts == 2, "踏み込みを追加しても命中は二回だけ");
    }

    private async Task RealBattle()
    {
        var cue = FxCatalog.Find("utsu.live"); var playback = FxLivePlayback.Create(cue);
        var original = JsonSerializer.Serialize(playback.Result.Events);
        var plan = UtsuPresentation.Build(playback.Result.Events, playback.Openings);
        var ids = playback.Openings.Where(o => o.UnitId == "utsu").Select(o => o.InstanceId).ToHashSet();
        Require(plan.Swings.Count == playback.Result.Events.Count(e => e.Kind == BattleEventKind.Attack && e.ActorId is int id && ids.Contains(id) && !e.Relayed), "実台本の打数と演出の打数");
        await _main.StopViewerPlayback(); _main.SetPlaybackSpeed(32); _main.EnterViewerLive(playback, cue);
        await Until(() => !_main.IsPlaying);
        Require(_main.ViewerField.UtsuContacts == plan.Swings.Count, "実戦の全接触が一度ずつ");
        Require(JsonSerializer.Serialize(playback.Result.Events) == original, "演出は台本を変更しない");
        foreach (var e in playback.Result.Events.Where(e => e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Revive).GroupBy(e => e.TargetId).Select(g => g.Last()))
            if (_main.ViewerField.FindPawn(e.TargetId) is { } pawn) Require(pawn.Hp == e.HpAfter, "実戦の最終HP");
    }

    private async Task SeekAndReplay()
    {
        await Run(3, false, false);
        await _main.SeekTo(0); _main.ResumePlayback();
        await Until(() => !_main.IsPlaying);
        Require(_main.ViewerField.UtsuContacts == 3 && _main.ViewerField.UtsuReturns == 1, "シーク・再戦でも二重命中や残った空中姿勢がない");
    }

    private async Task Until(Func<bool> done)
    {
        ulong end = Time.GetTicksMsec() + 60000;
        while (!done() && Time.GetTicksMsec() < end)
        {
            if (_main?.ViewerPlaybackError is { } error) throw error;
            await Frame();
        }
        Require(done(), "待ち時間内に完了");
    }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private void Require(bool ok, string label) { _assertions++; if (!ok) throw new InvalidOperationException(label); }
}
