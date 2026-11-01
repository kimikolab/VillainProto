using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private Node3D? _chiriKit;
    private readonly Dictionary<int, Node3D> _chiriMarkers = new();
    private readonly HashSet<(int Burst, int Target)> _chiriImpacts = new();
    private int _chiriGeneration;
    private Node3D? _chiriShakeRig;
    private const double ChiriShakeDuration = .18;
    private double _chiriShakeAge;
    internal bool ChiriShakeActive { get; private set; }
    internal int ChiriShakePlays { get; private set; }
    internal Vector3 ChiriShakeDisplacement => IsInstanceValid(_chiriShakeRig) ? _chiriShakeRig!.Position : Vector3.Zero;
    internal int ChiriPlants, ChiriFuses, ChiriBlasts, ChiriMirrors, ChiriDuds, ChiriCasts;
    internal readonly List<(int Burst, int Target)> ChiriImpactTargets = new();
    internal readonly List<int> ChiriMirrorDestinations = new();
    internal int ChiriMarkerCount => _chiriMarkers.Count;
    internal int ChiriEffectCount => ChiriKitLive ? _chiriKit!.Call("active_count").AsInt32() : 0;
    internal Node3D? ChiriMarker(int id) => _chiriMarkers.GetValueOrDefault(id);
    private bool ChiriKitLive => IsInstanceValid(_chiriKit) && !_chiriKit!.IsQueuedForDeletion();
    private static bool ChiriPawnLive(BattlePawn3D? pawn) => IsInstanceValid(pawn) && pawn!.IsInsideTree();
    private Vector3 ChiriMarkerPoint(BattlePawn3D pawn) => pawn.HudAnchor
        + _camera.GlobalBasis.Y * .42f + _camera.GlobalBasis.X * (pawn.Team == 0 ? -.48f : .48f);

    private Node3D ChiriKit()
    {
        if (ChiriKitLive) return _chiriKit!;
        _chiriKit = GD.Load<PackedScene>("res://assets/fx/chiri/chiri_material_kit.tscn").Instantiate<Node3D>();
        _chiriKit.Set("camera", _camera);
        _chiriKit.Set("clock", Callable.From(() => new Vector2(_deathPaused ? 1 : 0, (float)_deathSpeed)));
        _fxRoot.AddChild(_chiriKit);
        return _chiriKit;
    }

    private void ResetChiriPresentation()
    {
        EndChiriPresentation();
        ChiriPlants = ChiriFuses = ChiriBlasts = ChiriMirrors = ChiriDuds = ChiriCasts = 0;
        ChiriShakePlays = 0;
        ChiriImpactTargets.Clear(); ChiriMirrorDestinations.Clear(); _chiriImpacts.Clear();
    }

    internal void EndChiriPresentation()
    {
        ++_chiriGeneration;
        ResetChiriShake();
        foreach (var pawn in _pawns.Values) if (IsInstanceValid(pawn)) pawn.SetChiriPose(null);
        _chiriMarkers.Clear();
        if (ChiriKitLive) { _chiriKit!.Call("clear"); _chiriKit.Hide(); _chiriKit.QueueFree(); }
        _chiriKit = null;
    }

    private void UpdateChiriPresentation()
    {
        foreach (var (id, marker) in _chiriMarkers)
            if (IsInstanceValid(marker) && !marker.IsQueuedForDeletion() && FindPawn(id) is { } holder)
                marker.GlobalPosition = ChiriMarkerPoint(holder);
    }

    internal async Task BeginChiriCast(BattlePawn3D? actor, bool hasPlant)
    {
        ChiriCasts++;
        int generation = _chiriGeneration;
        if (!ChiriPawnLive(actor)) return;
        actor!.SetChiriPose("chiri_plant");
        await ChiriWait(.18, generation);
        if (!hasPlant && generation == _chiriGeneration && ChiriPawnLive(actor)) actor.SetChiriPose(null);
    }

    internal void PlantChiriBomb(BattlePawn3D? actor, BattlePawn3D? holder, int remaining)
    {
        ChiriPlants++;
        if (!ChiriPawnLive(holder)) return;
        RemoveChiriMarker(holder!.InstanceId);
        Vector3 head = ChiriMarkerPoint(holder);
        var kit = ChiriKit();
        Node3D marker;
        if (ChiriPawnLive(actor))
        {
            actor!.SetChiriPose("chiri_release");
            Vector3 hand = actor.UnitId == "chiri"
                ? actor.MovementPortraitPoint(new Vector2(936, 460), _camera) : actor.FxPoint;
            marker = kit.Call("plant", hand, head, actor.Team == holder.Team).As<Node3D>();
        }
        else marker = kit.Call("marker", head, remaining, false, 0.0).As<Node3D>();
        marker.Scale = Vector3.One * .72f;
        marker.Call("set_fuse", remaining);
        _chiriMarkers[holder.InstanceId] = marker;
    }

    internal async Task FinishChiriPlant(BattlePawn3D? actor)
    {
        int generation = _chiriGeneration;
        await ChiriWait(.38, generation);
        if (generation == _chiriGeneration && ChiriPawnLive(actor)) actor!.SetChiriPose(null);
    }

    internal void ShortenChiriFuse(BattlePawn3D? holder, int remaining)
    {
        ChiriFuses++;
        if (holder is not null && _chiriMarkers.TryGetValue(holder.InstanceId, out var marker) && ChiriKitLive)
            _chiriKit!.Call("shorten", marker, remaining);
    }

    internal void DudChiriBomb(BattlePawn3D? holder)
    {
        ChiriDuds++;
        if (holder is not null && _chiriMarkers.Remove(holder.InstanceId, out var marker)
            && IsInstanceValid(marker) && !marker.IsQueuedForDeletion() && ChiriKitLive)
            _chiriKit!.Call("dud", marker);
    }

    private void RemoveChiriMarker(int id)
    {
        if (_chiriMarkers.Remove(id, out var marker) && IsInstanceValid(marker))
        { marker.Hide(); marker.QueueFree(); }
    }

    internal async Task ShowChiriBurst(ChiriPresentation.Burst cue)
    {
        int generation = _chiriGeneration;
        var holder = FindPawn(cue.Event.TargetId);
        bool mirror = cue.Event.Text == BattleContext.BombMirrorText;
        if (mirror) ChiriMirrors++; else ChiriBlasts++;
        if (holder is not null) RemoveChiriMarker(holder.InstanceId);
        if (cue.Chain) await ChiriWait(.12, generation);
        if (generation != _chiriGeneration || !IsVisibleInTree()) return;
        var center = FindPawn(cue.Center);
        if (!ChiriPawnLive(center)) return; // FromIdが無いときに別の敵を選ばない。
        if (mirror && ChiriPawnLive(holder))
        {
            ChiriMirrorDestinations.Add(center!.InstanceId);
            ChiriKit().Call("mirror", holder!.FxPoint, center.FxPoint);
            await ChiriWait(.48, generation);
            if (generation != _chiriGeneration || !ChiriPawnLive(center)) return;
        }
        ChiriImpact(cue, center!, primary: true);
        await ChiriWait(.06, generation);
    }

    internal void ShowChiriDamage(ChiriPresentation.Burst cue, BattlePawn3D? target)
    {
        if (!ChiriPawnLive(target)) return;
        ChiriImpact(cue, target!, primary: target!.InstanceId == cue.Center);
    }

    private void ChiriImpact(ChiriPresentation.Burst cue, BattlePawn3D target, bool primary)
    {
        if (!_chiriImpacts.Add((cue.Index, target.InstanceId))) return;
        ChiriImpactTargets.Add((cue.Index, target.InstanceId));
        float size = primary ? 1.20f : .80f;
        var burst = ChiriKit().Call("blast", target.FxPoint, size, 0.0).As<Node3D>();
        if (primary) StartChiriShake();
        // 音は爆弾1個につき1回。巻き込み先の数だけ重ねない。
        if (!primary) burst.Set("sound_enabled", false);
    }

    private void StartChiriShake()
    {
        // 巻き込みでは呼ばない。誘爆が重なっても振幅を加算せず、短い一撃を更新する。
        ChiriShakePlays++;
        _chiriShakeAge = 0;
        ChiriShakeActive = true;
        if (!_deathPaused) DrawChiriShake();
    }

    private void UpdateChiriShake(double delta)
    {
        if (!ChiriShakeActive || _deathPaused) return;
        _chiriShakeAge += delta * _deathSpeed;
        if (_chiriShakeAge >= ChiriShakeDuration) { ResetChiriShake(); return; }
        DrawChiriShake();
    }

    private void DrawChiriShake()
    {
        if (!IsInstanceValid(_chiriShakeRig) || !IsInstanceValid(_camera)) return;
        float t = (float)_chiriShakeAge;
        float fade = MathF.Pow(1 - t / (float)ChiriShakeDuration, 2);
        // カメラの親だけを平行移動する。既存の視点Tween・集束砲のオフセットを上書きしない。
        _chiriShakeRig!.Position = (_camera.Basis.X * MathF.Cos(t * 72)
            - _camera.Basis.Y * (.55f * MathF.Cos(t * 87 + .25f))) * (.045f * fade);
    }

    private void ResetChiriShake()
    {
        ChiriShakeActive = false;
        _chiriShakeAge = 0;
        if (IsInstanceValid(_chiriShakeRig)) _chiriShakeRig!.Position = Vector3.Zero;
    }

    private async Task ChiriWait(double seconds, int generation)
    {
        double age = 0;
        while (age < seconds && generation == _chiriGeneration && IsVisibleInTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (generation != _chiriGeneration || !IsVisibleInTree()) return;
            if (!_deathPaused) age += GetProcessDeltaTime() * _deathSpeed;
        }
    }

    internal async Task FinishChiriEffects()
    {
        int generation = _chiriGeneration;
        // 最後の爆発・不発の消失と短い音を切らず、未起爆の印だけは終戦で片付ける。
        while (generation == _chiriGeneration && IsVisibleInTree() && ChiriKitLive
            && _chiriKit!.Call("transient_count").AsInt32() > 0)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
