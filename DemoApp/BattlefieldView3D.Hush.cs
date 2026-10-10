using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private readonly HashSet<int> _brokenHush = new(), _openHush = new(), _knightReleased = new();
    private readonly Dictionary<int, HushGauge> _hushGauges = new();
    private AudioEffectLowPassFilter? _hushFilter;
    private SubViewportContainer _battleImage = null!;
    internal int HushCracks, HushShatters, KnightRipostes, KnightFirstRipostes, HushSlaps;
    internal bool HushMuted => _hushFilter is not null;
    internal int HushGaugeCount => _hushGauges.Count;
    internal static double HushHold(double speed) => Math.Max(.85, 1.35 / Math.Max(.1, speed));
    private bool HushActive(int id) => !_fallenForSeal.Contains(id) && !_brokenHush.Contains(id) && !_openHush.Contains(id);

    private void EndHushPresentation()
    {
        _sealGeneration++;
        SetHushAtmosphere(false);
        foreach (var gauge in _hushGauges.Values)
            if (IsInstanceValid(gauge)) { gauge.Hide(); gauge.QueueFree(); }
        _hushGauges.Clear();
        foreach (var chain in _hushChains.Values)
            if (IsInstanceValid(chain)) { chain.Hide(); chain.QueueFree(); }
        _hushChains.Clear();
    }
    private void ResetHushPresentation()
    {
        EndHushPresentation();
        _brokenHush.Clear(); _openHush.Clear(); _knightReleased.Clear();
        HushCracks = HushShatters = KnightRipostes = KnightFirstRipostes = HushSlaps = 0;
    }
    private void RegisterHushGauge(DemoOpening opening)
    {
        if (opening.Traits?.Contains(TraitId.Hush) != true) return;
        var gauge = new HushGauge(); _popupLayer.AddChild(gauge);
        _hushGauges[opening.InstanceId] = gauge;
        // 戦の頭では破壊回数を予測しない。初回のHushStateで上限を受け取る。
        gauge.SetCracks(0, 0);
        SetHushAtmosphere(true);
    }
    private void PlaceHushGauges()
    {
        foreach (var (id, gauge) in _hushGauges)
        {
            var pawn = FindPawn(id);
            gauge.Visible = pawn is not null && HushActive(id);
            if (pawn is not null) gauge.Position = _camera.UnprojectPosition(pawn.HudAnchor + Vector3.Up * 1.15f);
        }
    }
    internal void SetHushLimits(IReadOnlyList<BattleEvent> events)
    {
        foreach (var group in events.Where(e => e.Kind == BattleEventKind.HushState
            && e.Text == HushStateLabels.Crack && e.ActorId is not null).GroupBy(e => e.ActorId!.Value))
            if (_hushGauges.TryGetValue(group.Key, out var gauge)) gauge.SetCracks(0, group.First().Slot);
    }
    private void SetHushAtmosphere(bool active)
    {
        if (active == HushMuted) return;
        if (active)
        {
            _battleImage.Material = new ShaderMaterial { Shader = HushAtmosphereShader() };
            _hushFilter = new AudioEffectLowPassFilter { CutoffHz = 1400, Resonance = .5f };
            AudioServer.AddBusEffect(BattleAudioRouting.EnsureFieldBus(), _hushFilter);
        }
        else
        {
            CancelHushOpening();
            _battleImage.Material = null;
            // 自分が加えた効果だけを外す。既存の音量・他のエフェクトを変更しない。
            int bus = AudioServer.GetBusIndex(BattleAudioRouting.FieldBus);
            if (bus >= 0)
                for (int i = AudioServer.GetBusEffectCount(bus) - 1; i >= 0; i--)
                    if (AudioServer.GetBusEffect(bus, i) == _hushFilter) AudioServer.RemoveBusEffect(bus, i);
            _hushFilter = null;
        }
    }
    private void ReleaseHush(int id)
    {
        foreach (var key in _hushChains.Keys.Where(k => k.Holder == id).ToArray())
        { _hushChains[key].Shatter(); _hushChains.Remove(key); }
        if (_hushGauges.TryGetValue(id, out var gauge)) gauge.Hide();
        SetHushAtmosphere(_hushHolders.Any(HushActive));
        if (!HushMuted)
            foreach (var pawn in _pawns.Values.Where(p => p.UnitId == "hisa"))
            {
                _quietTurns.Remove(pawn.InstanceId);
                if (_rallyCaptions.Remove(pawn.InstanceId, out var caption) && LivePopup(caption))
                { caption.Hide(); caption.QueueFree(); }
            }
    }
    internal async Task ShowHushState(BattlePawn3D? holder, BattlePawn3D? stopped, BattleEvent cue, double speed)
    {
        if (holder is null) return;
        int id = holder.InstanceId;
        speed = Math.Max(.1, speed);
        if (cue.Text == HushStateLabels.Crack)
        {
            HushCracks++;
            if (_hushGauges.TryGetValue(id, out var gauge)) gauge.SetCracks(cue.Amount, cue.Slot);
            if (stopped is not null)
                ShockMarkFx.Beam(_fxRoot, stopped.FxPoint, holder.HudAnchor + Vector3.Up * .7f,
                    new Color("c7deee"), .025f, Math.Max(.12, .24 / speed));
            await ToSignal(GetTree().CreateTimer(.12 / speed), SceneTreeTimer.SignalName.Timeout);
        }
        else if (cue.Text == HushStateLabels.Shatter && _brokenHush.Add(id))
        {
            HushShatters++;
            ReleaseHush(id);
            _attackAudio.PlayHushBreak();
            _attackAudio.PlayShockMark(ShockMarkSound.HushCry);
            holder.ShatterHushPortrait();
            ShatterHushClothes(holder);
            ShockMarkFx.Sparks(_fxRoot, holder.FxPoint, new Color("c8d5eb"), 24, 2.8f, .65);
            ShockMarkFx.Ring(_fxRoot, holder.FxPoint, Colors.White, 4.5f, .42);
            CameraPunch(holder.FxPoint, AttackPattern.All);
            // 次のDamage/Deathを進めない実時間の拍。2倍速でも報いを読める。
            await ToSignal(GetTree().CreateTimer(HushHold(speed)), SceneTreeTimer.SignalName.Timeout);
        }
        else if (cue.Text == HushStateLabels.Break)
        { _openHush.Add(id); ReleaseHush(id); }
        else if (cue.Text == HushStateLabels.Close && !_brokenHush.Contains(id))
        { _openHush.Remove(id); ConnectSeals(); SetHushAtmosphere(_hushHolders.Any(HushActive)); }
    }
    internal async Task ShowKnightRiposte(BattlePawn3D? knight, BattlePawn3D? target, double speed)
    {
        if (knight is null || target is null) return;
        int generation = _sealGeneration;
        KnightRipostes++;
        bool first = _brokenHush.Count > 0 && _knightReleased.Add(knight.InstanceId);
        if (first) KnightFirstRipostes++;
        Float(knight, first ? "解き放たれた斬り返し" : "斬り返し", UiKit.Enemy, first, 3.4f);
        knight.MovementPose(-.22f, .28f);
        if (first) ShockMarkFx.Glow(_fxRoot, knight.FxPoint, UiKit.Enemy, 1.6f, .30);
        await ToSignal(GetTree().CreateTimer((first ? .18 : .08) / Math.Max(.1, speed)), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree() || generation != _sealGeneration) return;
        MakeSingleSlash(knight, target, first ? UiKit.Gold : UiKit.Enemy);
        _attackAudio.PlayZan(ZanSound.Slash);
    }
    private void ShatterHushClothes(BattlePawn3D holder)
    {
        // 硬い銀の破片と紫の法衣を散らす。寿命は見せ場の実時間に合わせる。
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Tau / 12;
            bool armor = i % 2 == 0;
            var shard = new MeshInstance3D {
                Mesh = new PrismMesh { Size = armor ? new Vector3(.23f, .38f, .08f) : new Vector3(.35f, .6f, .025f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(armor ? "c2d8e3" : "8878a1"),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _fxRoot.AddChild(shard);
            Vector3 from = holder.FxPoint;
            Vector3 drift = _camera.GlobalBasis.X * Mathf.Cos(angle) * 2 + Vector3.Up * (1 + Mathf.Sin(angle));
            var tween = shard.CreateTween();
            tween.TweenMethod(Callable.From<float>(t => {
                shard.GlobalPosition = from + drift * t + Vector3.Down * t * t * 2;
                shard.Rotation = new Vector3(t * 4, t * 3, angle + t * 5);
                shard.Scale = Vector3.One * (1 - t * .8f);
            }), 0f, 1f, .72);
            tween.TweenCallback(Callable.From(shard.QueueFree));
        }
    }
    public override void _ExitTree() => EndHushPresentation();
}
