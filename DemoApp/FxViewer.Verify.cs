using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class FxViewer
{
    private void VerifyBasic(FxCue cue)
    {
        var subject = cue.Subject!;
        var pawn = _main.ViewerField.FindPawn(1)!;
        Require(pawn.Team == (subject.Enemy ? 1 : 0) && pawn.UnitId == subject.Definition.Id, "生成した本人と陣営");
        Require(pawn.Slot == 2, "生成した本人は中央");
        var damage = _main.PlaybackEvents.LastOrDefault(e => e.Kind == BattleEventKind.Damage && e.TargetId == 1);
        Require(pawn.Hp == (damage?.HpAfter ?? subject.Definition.MaxHp), "生成した本人の確定HP");
        if (cue.Id.EndsWith(".idle"))
            Require(_main.PlaybackEvents.Count == 0 && cue.HoldSeconds == 3, "待機は空台本を3秒間保持");
        if (cue.Id.EndsWith(".attack"))
            foreach (var e in _main.PlaybackEvents.Where(e => e.Kind == BattleEventKind.Damage))
                Require(_main.ViewerField.FindPawn(e.TargetId)!.Hp == e.HpAfter, "攻撃の対象とHP");
    }

    private void VerifyCatalogFilter()
    {
        _unit.Selected = _unitIds.IndexOf("kata") + 1; Filter();
        var catalog = _list.GetRoot().GetFirstChild();
        Require(catalog.GetText(0) == "駒カタログ", "駒カタログを先頭に表示");
        var unit = catalog.GetFirstChild();
        Require(unit is not null && unit.GetNext() is null && unit.Collapsed, "駒の絞り込みと既定の折りたたみ");
        unit!.Collapsed = false;
        _collapsedGroups[unit.GetMetadata(0).AsString()] = false;
        Filter();
        Require(!_list.GetRoot().GetFirstChild().GetFirstChild().Collapsed, "選び直しても展開状態を保つ");
        _unit.Selected = 0; Filter();
        GD.Print("FX_VIEWER_FILTER_OK");
    }

    private async Task WaitForEnd()
    {
        ulong deadline = Time.GetTicksMsec() + 60000;
        while (_main.IsPlaying && Time.GetTicksMsec() < deadline)
        {
            if (_main.ViewerPlaybackError is { } error) throw error;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Require(!_main.IsPlaying && _main.EventIndex == _main.PlaybackEvents.Count, "台本の終端に到達");
    }

    private async Task Smoke()
    {
        int registered = 0, generated = 0;
        var skipped = new System.Collections.Generic.HashSet<string>();
        var basics = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var cue in _cues)
        {
            string key = cue.Subject?.Key ?? cue.Id;
            if (skipped.Contains(key)) continue;
            try
            {
                await LoadCue(cue); await WaitForEnd();
                if (cue.Generated && cue.Kind == FxCueKind.Scripted) VerifyBasic(cue);
                if (cue.Generated)
                {
                    generated++;
                    if (cue.Kind == FxCueKind.Scripted) basics[key] = basics.GetValueOrDefault(key) + 1;
                }
                else registered++;
                GD.Print($"FX_VIEWER_CUE_OK id={cue.Id} events={_main.EventIndex}");
                if (cue.Kind == FxCueKind.Scripted && !cue.Generated)
                {
                    _mirror.SetPressedNoSignal(true);
                    await LoadCue(cue); await WaitForEnd();
                    foreach (var opening in cue.Openings!())
                    {
                        var pawn = _main.ViewerField.FindPawn(opening.InstanceId)!;
                        Require(pawn.Team == 1 - opening.Team, "左右反転の陣営");
                    }
                    GD.Print($"FX_VIEWER_MIRROR_OK id={cue.Id}");
                    _mirror.SetPressedNoSignal(false);
                }
            }
            catch (Exception e) when (cue.Subject is not null)
            {
                skipped.Add(key);
                GD.Print($"FX_VIEWER_SMOKE_SKIP {cue.Subject.Definition.Id} {e.Message.Replace('\n', ' ')}");
                try { await _main.StopViewerPlayback(); } catch { /* 失敗済みの待ち手を回収し、次の駒へ進む。 */ }
            }
        }
        foreach (var d in UnitCatalog.All)
            Require(basics.GetValueOrDefault("player:" + d.Id) == 4, $"基本4本が完走 {d.Id}");
        foreach (string id in new[] { "kata", "basa", "golm" })
        {
            int count = _cues.Where(c => c.Subject is { Enemy: false } u && u.Definition.Id == id && c.Kind == FxCueKind.Live)
                .Sum(c => _highlights.GetValueOrDefault(c.Id)?.Length ?? 0);
            Require(count > 0, $"実戦の見せ場がある {id}");
            GD.Print($"FX_VIEWER_HIGHLIGHTS_OK id={id} count={count}");
        }
        var enemies = FxCatalog.EnemyUnits.ToArray();
        Require(basics.GetValueOrDefault("enemy:" + EnemyCatalog.Knight.Id) == 4, "巡礼騎士の基本4本");
        Require(enemies.Where(d => d.Id != EnemyCatalog.Knight.Id).Any(d => basics.GetValueOrDefault("enemy:" + d.Id) == 4), "もう1種の敵の基本4本");
        VerifyCatalogFilter();
        await VerifyIdleClock();
        GD.Print($"FX_VIEWER_BASIC_OK players={UnitCatalog.All.Count} enemies={enemies.Length}");
        var live = _cues.First(c => c.Kind == FxCueKind.Live);
        await LoadCue(live);
        var events = _main.PlaybackEvents;
        int checkpoint = events.Count / 2;
        _main.SetPlaybackSpeed(2);
        await _main.SeekTo(checkpoint);
        Require(_main.EventIndex == checkpoint && _main.IsPaused, "指定イベント直前で停止");
        Require(_main.PlaybackSpeed == 2, "シーク後に再生速度を戻す");
        foreach (var pawn in _main.ViewerField.Pawns.Values)
        {
            var opening = _main.ViewerOpenings.FirstOrDefault(o => o.InstanceId == pawn.InstanceId);
            var hp = events.Take(checkpoint).LastOrDefault(e => e.TargetId == pawn.InstanceId
                && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.BloodSip or BattleEventKind.Revive);
            if (hp is not null) Require(pawn.Hp == hp.HpAfter, $"中盤のHP {pawn.UnitId}");
            else if (opening is not null) Require(pawn.Hp == opening.Hp, $"未被弾のHP {pawn.UnitId}");
            var move = events.Take(checkpoint).LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.Move);
            if (move is not null) Require(pawn.Slot == move.Slot, $"中盤の席 {pawn.UnitId}");
            else if (opening is not null) Require(pawn.Slot == opening.Slot, $"未移動の席 {pawn.UnitId}");
            // 感電は付与・消費・写し・死亡だけで表示が変わる。台本から独立に照合する。
            bool shock = false, snapshot = false;
            foreach (var e in events.Take(checkpoint))
            {
                if (e.Kind == BattleEventKind.TurnStart) snapshot = false;
                if (e.TargetId != pawn.InstanceId) continue;
                if (e.Kind == BattleEventKind.StatusSnapshot && e.Text == StatusKeys.LabelOf(StatusKeys.Shock)) snapshot = e.Amount > 0;
                if (e.Kind == BattleEventKind.StatSnapshot) shock = snapshot;
                if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Shock) shock = e.Amount > 0;
                if (e.Kind is BattleEventKind.ShockSpent or BattleEventKind.Death or BattleEventKind.Revive) shock = false;
            }
            Require(pawn.HasStatusIcon(StatusKeys.Shock) == shock, $"中盤の感電アイコン {pawn.UnitId}");
        }
        int stopped = _main.EventIndex;
        await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
        Require(_main.EventIndex == stopped, "停止中はイベントを消費しない");
        _main.SetPlaybackSpeed(1000);
        _main.ResumePlayback(); await WaitForEnd();
        foreach (var group in events.Where(e => e.TargetId is not null && e.Kind is BattleEventKind.Heal or BattleEventKind.Damage).GroupBy(e => e.TargetId))
            Require(_main.ViewerField.FindPawn(group.Key)?.Hp == group.Last().HpAfter, "シーク後の最終HPは台本に一致");
        GD.Print($"FX_VIEWER_SEEK_OK id={live.Id} checkpoint={checkpoint} final={_main.EventIndex} hp=true status=true seats=true pause=true");
        await LoadCue(_cues[0]); await NextHighlight(); await WaitForEnd();
        _loop.SetPressedNoSignal(false);
        await NextHighlight();
        Require(_main.EventIndex == _main.PlaybackEvents.Count && !_main.IsPlaying, "繰り返しOFFは終端で止まる");
        _loop.SetPressedNoSignal(true);
        await NextHighlight();
        Require(_main.IsPlaying, "繰り返しONは先頭の見せ場へ戻る");
        _loop.SetPressedNoSignal(false);
        await WaitForEnd();
        GD.Print("FX_VIEWER_NEXT_OK loop=true end=true");
        // Cue を切り替える時は古い非同期処理が次の盤面に残らない。
        await LoadCue(_cues[0]); await LoadCue(_cues[1]); await WaitForEnd();
        GD.Print($"FX_VIEWER_SMOKE_OK cues={registered + generated} registered={registered} generated={generated} skipped={skipped.Count}");
        await QuitViewer();
    }

    private async Task VerifyIdleClock()
    {
        await LoadCue(FxCatalog.Find("unit.kata.idle"));
        _main.SetPlaybackSpeed(1);
        await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
        Require(_main.IsPlaying, "待機は開始直後に終わらない");
        _main.PausePlayback();
        await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
        Require(_main.IsPlaying && _main.IsPaused, "待機中も停止する");
        ulong start = Time.GetTicksMsec();
        _main.SetPlaybackSpeed(1000); _main.ResumePlayback(); await WaitForEnd();
        Require(Time.GetTicksMsec() - start < 1000, "待機中の速度変更に追従する");
        GD.Print("FX_VIEWER_IDLE_OK seconds=3 pause=true speed=true");
    }

    // 同じ登録を画面ありで撮る。通常の撮影ボタンと同じ保存処理を使う。
    private async Task CaptureAll()
    {
        _loop.ButtonPressed = false;
        foreach (var cue in _cues.Where(c => !c.Generated && c.Kind == FxCueKind.Scripted))
        foreach (bool mirror in new[] { false, true })
        {
            _mirror.SetPressedNoSignal(mirror);
            await LoadCue(cue);
            double seconds = cue.Id switch { "golm.swallow" => 1.7, "basa.storm" => 1.25, "kata.thunder.chain" => 1.3, _ => .85 };
            await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
            _main.PausePlayback();
            await Capture(cue.Id + (mirror ? ".mirror" : ""));
            _main.ResumePlayback();
        }
        GD.Print("FX_VIEWER_CAPTURE_OK cues=5 mirrors=5");
        await QuitViewer();
    }

    private async Task QuitViewer()
    {
        SetProcess(false);
        await _main.StopViewerPlayback();
        foreach (var tween in GetTree().GetProcessedTweens()) tween.Kill();
        foreach (Node child in GetChildren()) child.QueueFree();
        Theme = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _main = null!;
        _list = null!;
        _axis = _unit = _speed = null!;
        _loop = _mirror = null!;
        _search = null!;
        _status = null!;
        _pause = null!;
        _transport.Clear();
        GC.Collect(); GC.WaitForPendingFinalizers();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit();
    }

    private static void Require(bool ok, string message)
    { if (!ok) throw new InvalidOperationException($"FxViewer 検査: {message}"); }
}
