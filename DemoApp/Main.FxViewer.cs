using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    /// <summary>編成・戦績・作戦マップへの精算を使わない、演出だけの埋め込み。</summary>
    public bool ViewerMode { get; init; }
    public bool IsPlaying => _playing;
    public bool IsPaused => _paused;
    public int EventIndex => _eventIndex;
    public IReadOnlyList<BattleEvent> PlaybackEvents => _result?.Events ?? Array.Empty<BattleEvent>();
    internal BattlefieldView3D ViewerField => _battleField;
    internal IReadOnlyList<DemoOpening> ViewerOpenings => _battleOpening;
    internal double PlaybackSpeed => _speed;
    internal double ViewerHoldSeconds { get; set; }
    internal double ViewerLeadInSeconds { get; set; }
    internal bool ViewerFastPlayback { get => _fastSmoke; set => _fastSmoke = value; }
    internal Exception? ViewerPlaybackError => _playbackTask.Exception?.GetBaseException();
    private bool CanPreviewViewerEvent(int index) => _seekTarget is not int target || index < target;
    private Task _playbackTask = Task.CompletedTask;
    private int? _seekTarget;
    private TaskCompletionSource? _seekCompletion;
    private double _seekSpeed;
    private bool _seekFastSmoke;
    private int? _viewerRicaLayers;
    private string? _viewerRicaIdleVariant;
    private void ApplyViewerRicaStage()
    {
        if (!ViewerMode || _viewerRicaLayers is not int layers) return;
        foreach (var pawn in _battleField.Pawns.Values.Where(p => p.UnitId == "rica"))
        {
            pawn.SetRicaIdleVariant(_viewerRicaIdleVariant);
            _battleField.ApplyDeathState(new DeathPresentation.State(pawn.InstanceId,layers,0,0,pawn.MaxHp));
        }
    }

    // 待機は空台本のまま保持する。停止時間を数えず、途中の速度変更にも追従する。
    private async Task HoldViewerIdle(int token)
    {
        double elapsed = 0;
        while (elapsed < ViewerHoldSeconds && token == _playToken && _battleMode)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_paused) elapsed += GetProcessDeltaTime() * _speed;
        }
    }

    // 比較撮影の前置き。駒と待機の時計は動かしたまま、同じ盤面で台本の開始だけを待つ。
    private async Task HoldViewerLeadIn(int token)
    {
        double elapsed = 0;
        while (elapsed < ViewerLeadInSeconds && token == _playToken && _battleMode)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_paused) elapsed += GetProcessDeltaTime() * _speed;
        }
    }

    /// <summary>外から渡した台本を、本番と同じ索引・描画経路で再生する。</summary>
    public void EnterScripted(IReadOnlyList<DemoOpening> openings, IReadOnlyList<BattleEvent> events,
        string title, int stageIndex = 0, int? ricaLayers = null, string? ricaIdleVariant = null)
    {
        _battleTitle = title;
        _battleStageIndex = stageIndex;
        _battleOpening = openings.ToList();
        _playerShape = openings.FirstOrDefault(o => o.Team == 0)?.Shape ?? FormationShape.X;
        _result = ScriptedResult(events.ToArray());
        _death = null;
        _viewerRicaLayers = ricaLayers;
        _viewerRicaIdleVariant = ricaIdleVariant;
        PreparePlayback(title, stageIndex, 0);
    }

    public void EnterViewerLive(Formation formation, int stage, int seed, string title)
    {
        _viewerRicaLayers = null; _viewerRicaIdleVariant = null;
        EnterBattle(BattleEngine.Materialize(formation, 0),
            BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1), seed, stage, title);
    }

    internal void EnterViewerLive(FxLivePlayback playback, FxCue cue)
    {
        _viewerRicaLayers = null;
        _viewerRicaIdleVariant = null;
        _battleTitle = cue.Title;
        _battleStageIndex = cue.Stage;
        _battleOpening = playback.Openings.ToList();
        _playerShape = playback.Openings.FirstOrDefault(o => o.Team == 0)?.Shape ?? FormationShape.X;
        _result = playback.Result;
        _death = playback.Death;
        PreparePlayback(cue.Title, cue.Stage, cue.Seed);
    }

    public void SetPlaybackSpeed(double speed)
    {
        if (!double.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
        _speed = speed;
        UpdateViewerClock();
    }

    private void UpdateViewerClock()
    {
        _battleField.SetSomClock(_paused, _speed);
        _battleField.SetWhipClock(_paused, _speed);
        _battleField.SetTouPowderClock(_paused, _speed);
        _battleField.SetGolmClock(_paused, _speed);
        _battleField.SetGatherClock(_paused, _speed);
        _battleField.SetPoisonClock(_paused, _speed);
        _battleField.SetDeathClock(_paused, _speed);
        foreach (var pawn in _battleField.Pawns.Values) pawn.AnimationSpeed = _speed;
        if (ViewerMode) _battleField.ProcessMode = _paused ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
    }

    public void PausePlayback()
    {
        _paused = true;
        UpdateViewerClock();
    }

    public void ResumePlayback()
    {
        if (!_playing) ReplayBattle();
        _paused = false;
        UpdateViewerClock();
    }

    /// <summary>待ち手も止めてから次の Cue へ進む。古い非同期演出を次の盤面に混ぜない。</summary>
    public async Task StopViewerPlayback()
    {
        ++_playToken;
        _seekCompletion?.TrySetCanceled();
        _seekCompletion = null;
        if (_seekTarget is not null)
        {
            _speed = _seekSpeed;
            _fastSmoke = _seekFastSmoke;
            _seekTarget = null;
        }
        _paused = false;
        UpdateViewerClock();
        _battleField.BeginBattle(Array.Empty<DemoOpening>(), "", 0);
        try { await _playbackTask; }
        finally { _playing = false; _playbackTask = Task.CompletedTask; }
        // 接触待ちの解放は子ノードの退場通知からも返る。その場で次の子を追加しない。
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>N 件目の直前まで同じ ApplyEvent で早送りする。完了時は停止し、呼び手が再開する。</summary>
    public async Task SeekTo(int eventIndex)
    {
        if (_result is null) return;
        int target = Math.Clamp(eventIndex, 0, _result.Events.Count);
        await StopViewerPlayback();
        _seekSpeed = _speed;
        _seekFastSmoke = _fastSmoke;
        _seekTarget = target;
        var completion = _seekCompletion = new TaskCompletionSource();
        _fastSmoke = true;
        SetPlaybackSpeed(1000);
        ReplayBattle();
        await completion.Task;
    }

    private void CompleteViewerSeek()
    {
        if (_seekTarget is not int target || _eventIndex < target) return;
        _seekTarget = null;
        _fastSmoke = _seekFastSmoke;
        _paused = true;
        SetPlaybackSpeed(_seekSpeed);
        _seekCompletion?.TrySetResult();
        _seekCompletion = null;
    }
}
