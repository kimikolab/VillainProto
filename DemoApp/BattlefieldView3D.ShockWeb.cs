using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    private readonly Dictionary<int, BindingSilk3D> _webs = new();
    private readonly Dictionary<int, SilkBallVisual3D> _silkBalls = new();
    private readonly Dictionary<int, Vector3> _shockPoints = new();
    private readonly List<BindingSilk3D> _webRemnants = new();
    private ColorRect? _whipFlash;
    private Tween? _whipFlashTween;
    internal int WebSpinPlays, WebRechargePlays, WebBallPlays, WebCutPlays;
    internal int SilkPlacePlays, SilkPopPlays, SilkRechargePlays, SilkDischargePlays;
    internal int WhipChainPlays, WhipGatherStrands, WhipWhiteFlashes;
    internal int ActiveWebCount => _webs.Count;
    internal int SilkBallCount => _silkBalls.Count;
    internal int ChargedSilkBallCount => _silkBalls.Values.Count(b => b.Charged);

    private void ResetShockWebCounts()
    {
        WebSpinPlays = WebRechargePlays = WebBallPlays = WebCutPlays = 0;
        SilkPlacePlays = SilkPopPlays = SilkRechargePlays = SilkDischargePlays = 0;
        WhipChainPlays = WhipGatherStrands = WhipWhiteFlashes = 0;
    }

    private void ResetShockWeb()
    {
        foreach (Node3D node in _webs.Values.Cast<Node3D>().Concat(_silkBalls.Values).Concat(_webRemnants))
            if (IsInstanceValid(node)) { node.Hide(); node.QueueFree(); }
        _webs.Clear(); _silkBalls.Clear(); _webRemnants.Clear(); _shockPoints.Clear();
        _whipFlashTween?.Kill();
        if (_whipFlash is not null) _whipFlash.Hide();
    }

    private static Func<Vector3> WebAnchor(BattlePawn3D pawn)
    {
        Vector3 last = pawn.FxPoint;
        return () => {
            if (IsInstanceValid(pawn) && pawn.PresentationAlive) last = pawn.FxPoint;
            return last;
        };
    }

    internal void ShowWeb(BattleEvent e, double speed)
    {
        if (e.TargetId is not int id) return;
        if (e.Text == WebLabels.Recharge)
        {
            WebRechargePlays++;
            if (_webs.TryGetValue(id, out var thread)) thread.Conduct(false);
            return;
        }
        if (e.Text == WebLabels.Ball)
        {
            // 糸玉は直前の SilkBall.Place で一度だけ作る。
            WebBallPlays++;
            return;
        }
        var source = FindPawn(e.ActorId); var target = FindPawn(id);
        if (e.Text != WebLabels.Spin || source is null || target is null) return;
        WebSpinPlays++;
        AddWeb(id, source, WebAnchor(target), FindPawn(e.PartnerId));
        _attackAudio.PlayShockMark(ShockMarkSound.Thread);
    }

    private void AddWeb(int id, BattlePawn3D source, Func<Vector3> endpoint, BattlePawn3D? hub)
    {
        if (_webs.ContainsKey(id)) return;
        var thread = new BindingSilk3D(); _fxRoot.AddChild(thread);
        double lastSpeed = source.AnimationSpeed;
        thread.ConfigureWeb(WebAnchor(source), endpoint, hub is null ? null : WebAnchor(hub),
            () => IsInstanceValid(source) ? source.AnimationSpeed : lastSpeed, _camera);
        _webs.Add(id, thread);
    }

    internal void CutWebFor(BattlePawn3D? target)
    {
        if (target is null || !_webs.Remove(target.InstanceId, out var thread)) return;
        WebCutPlays++;
        _webRemnants.RemoveAll(n => !IsInstanceValid(n));
        _webRemnants.Add(thread);
        thread.Release();
    }

    internal void ShowSilkBall(BattleEvent e, double speed)
    {
        if (e.Text == SilkBallLabels.Recharge)
        {
            SilkRechargePlays++;
            foreach (var pair in _silkBalls)
            {
                if (pair.Value.Charged) continue;
                pair.Value.SetCharged(true, speed);
                if (_webs.TryGetValue(pair.Key, out var thread)) thread.Conduct(false);
            }
            return;
        }
        if (e.TargetId is not int id) return;
        if (e.Text == SilkBallLabels.Pop)
        {
            SilkPopPlays++;
            if (_silkBalls.TryGetValue(id, out var ball)) ball.SetCharged(false, speed);
            return;
        }
        if (e.Text != SilkBallLabels.Place || _silkBalls.ContainsKey(id)) return;
        SilkPlacePlays++;
        var visual = new SilkBallVisual3D(); _fxRoot.AddChild(visual);
        visual.GlobalPosition = PawnPosition(e.Team ?? 1, e.Slot) + Vector3.Up * 1.55f;
        visual.Configure(_camera, speed);
        _silkBalls.Add(id, visual);
        var source = FindPawn(e.ActorId);
        if (source is not null) AddWeb(id, source, () => visual.GlobalPosition, FindPawn(e.PartnerId));
        visual.SetCharged(true, speed);
        _attackAudio.PlayShockMark(ShockMarkSound.Thread);
    }

    internal Vector3? ElectricPoint(int? id)
    {
        if (id is not int key) return null;
        if (_silkBalls.TryGetValue(key, out var ball)) return ball.GlobalPosition;
        return FindPawn(key)?.DischargePoint;
    }

    internal void RememberShockPoint(int? id)
    {
        if (id is int key && ElectricPoint(id) is Vector3 point) _shockPoints[key] = point;
    }

    internal bool ShowSilkBallDischarge(int? from, int? to, double speed)
    {
        if (!(from is int a && _silkBalls.ContainsKey(a)) && !(to is int b && _silkBalls.ContainsKey(b))) return false;
        if (ElectricPoint(from) is not Vector3 start || ElectricPoint(to) is not Vector3 end) return true;
        SilkDischargePlays++; DischargePlays++;
        ThunderFx.Arc(_fxRoot, start, end, 0.04f, 0.24 / speed);
        ThunderFx.Burst(_fxRoot, end, 0.55f, 0.22 / speed);
        return true;
    }

    internal void ShowWhipChain(BattlePawn3D? actor, int count, IReadOnlyList<int> sources, double speed)
    {
        if (actor is null) return;
        WhipChainPlays++;
        actor.WhipChainSize = Math.Max(0, count);
        int generation = _specialGeneration;
        foreach (int id in sources)
        {
            Vector3? source = _shockPoints.TryGetValue(id, out var old) ? old : ElectricPoint(id);
            if (source is not Vector3 start) continue;
            WhipGatherStrands++;
            var head = ShockMarkFx.Sprite(_fxRoot, start, ShockMarkFx.Halo, 0.45f, ThunderFx.Cyan);
            Vector3 end = actor.WhipOrigin(_camera);
            ThunderFx.Arc(_fxRoot, start, end, 0.016f + count * 0.004f, 0.25 / speed);
            var tween = head.CreateTween();
            tween.TweenMethod(Callable.From<float>(t => {
                if (generation != _specialGeneration || !IsInstanceValid(actor)) return;
                head.GlobalPosition = start.Lerp(actor.WhipOrigin(_camera), t * t);
            }), 0f, 1f, 0.20 / speed);
            tween.TweenCallback(Callable.From(head.QueueFree));
        }
        ShockMarkFx.Glow(_fxRoot, actor.WhipOrigin(_camera), ThunderFx.Cyan, 0.9f + count * 0.3f, 0.35 / speed);
    }

    private void WhipChainContact(BattlePawn3D actor, double speed)
    {
        if (!actor.InterruptWhip || actor.WhipChainSize < 4) return;
        WhipWhiteFlashes++;
        if (_whipFlash is null)
        {
            _whipFlash = new ColorRect { MouseFilter = MouseFilterEnum.Ignore };
            _whipFlash.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_whipFlash);
        }
        _whipFlashTween?.Kill();
        _whipFlash.Color = new Color(0.88f, 0.97f, 1, 0.55f);
        _whipFlash.Show();
        _whipFlashTween = _whipFlash.CreateTween();
        _whipFlashTween.TweenProperty(_whipFlash, "color:a", 0f, 0.18 / speed);
        _whipFlashTween.TweenCallback(Callable.From(_whipFlash.Hide));
    }
}

// 糸玉は駒ではない。HPバーや攻撃判定を持たない常駐する糸の塊。
public partial class SilkBallVisual3D : MeshInstance3D
{
    private readonly ImmediateMesh _mesh = new();
    private readonly StandardMaterial3D _material = new() {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        EmissionEnabled = true,
    };
    private Camera3D _camera = null!;
    private float _age, _sparkClock;
    private double _speed = 1;
    internal bool Charged { get; private set; }

    internal void Configure(Camera3D camera, double speed)
    {
        _camera = camera; _speed = speed; Mesh = _mesh; MaterialOverride = _material;
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    internal void SetCharged(bool charged, double speed)
    {
        Charged = charged; _speed = Math.Max(0.1, speed);
        _material.AlbedoColor = charged ? new Color("c8f5ff") : new Color("8c8b91");
        _material.Emission = charged ? ThunderFx.Cyan : Colors.Black;
        _material.EmissionEnergyMultiplier = charged ? 1.3f : 0;
        if (charged) ShockMarkFx.Glow(this, GlobalPosition, ThunderFx.Cyan, 1.0f, 0.25 / _speed);
        else ThunderFx.Burst(this, GlobalPosition, 0.85f, 0.22 / _speed);
    }

    public override void _Process(double delta)
    {
        _age += (float)(delta * _speed);
        float grow = Mathf.Clamp(_age / 0.22f, 0.01f, 1);
        _mesh.ClearSurfaces(); _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int ring = 0; ring < 9; ring++)
        {
            var rotation = new Basis(Vector3.Up, ring * 2.39996f) * new Basis(Vector3.Right, ring * 0.53f);
            Vector3 Point(float t) => rotation * new Vector3(Mathf.Cos(t) * 0.42f, Mathf.Sin(t) * 0.50f, 0)
                * grow + Vector3.Up * Mathf.Sin(_age * 2) * 0.05f;
            for (int i = 0; i < 40; i++) Segment(Point(i * Mathf.Tau / 40), Point((i + 1) * Mathf.Tau / 40), 0.012f);
        }
        Segment(Vector3.Up * 0.4f, Vector3.Up * 1.1f, 0.008f);
        _mesh.SurfaceEnd();
        _sparkClock -= (float)delta;
        if (Charged && _sparkClock <= 0)
        {
            _sparkClock = 0.23f;
            ThunderFx.Arc(this, GlobalPosition + Vector3.Up * 0.3f, GlobalPosition + _camera.GlobalBasis.X * 0.35f,
                0.016f, 0.15);
        }
    }

    private void Segment(Vector3 a, Vector3 b, float width)
    {
        Vector3 side = (b - a).Cross(ToLocal(_camera.GlobalPosition) - a).Normalized() * width;
        foreach (Vector3 v in new[] { a - side, a + side, b + side, a - side, b + side, b - side }) _mesh.SurfaceAddVertex(v);
    }
}
