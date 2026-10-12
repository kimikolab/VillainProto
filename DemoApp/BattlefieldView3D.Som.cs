using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    private Node3D? _somRoot;
    private readonly Dictionary<int, (Sprite3D Mote, Tween Rise)> _somRising = new();
    private readonly Dictionary<int, Vector3> _somPopPoints = new();
    private readonly Dictionary<int, SomBeast3D> _somBeasts = new();
    private readonly Dictionary<int, Vector3> _somBeastPoints = new();
    private readonly List<(Tween Tween, double Speed)> _somTweens = new();
    private readonly List<GpuParticles3D> _somParticles = new();
    private readonly List<SomSpiritScatter3D> _somScatters = new();
    internal IEnumerable<SomSpiritScatter3D> SomScatters => _somScatters.Where(s => IsInstanceValid(s) && !s.IsQueuedForDeletion());
    private bool _somPaused;
    internal int SomCalls, SomBursts, SomEmergencies, SomMaxSwarm, SomFocusedRains, SomScatteredRains, SomDaunts, SomWeakAttacks;
    internal int SomStances, SomShortStances, SomBookAbsorptions;
    internal Vector3 SomBookPoint(BattlePawn3D som) => som.SomBookPoint(_camera);
    internal int SomBeastCount => _somBeasts.Count;
    internal IEnumerable<SomBeast3D> SomBeasts => _somBeasts.Values;
    internal int SomSummons, SomRains, SomStops, SomLights, SomVeils, SomImpacts, SomBreaks;
    internal int SomRisingCount => _somRising.Count;
    internal int SomVeilCount => _pawns.Values.Count(p => p.SomVeilAmount > 0);
    internal static Vector3 SomBeastOrigin(BattlePawn3D som)
        => som.Position + new Vector3(som.Team == 0 ? 1.15f : -1.15f, 0, .35f);
    private Node3D SomRoot
    {
        get
        {
            if (_somRoot is null || !IsInstanceValid(_somRoot))
            { _somRoot = new Node3D { ProcessMode = _somPaused ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit }; _fxRoot.AddChild(_somRoot); }
            return _somRoot;
        }
    }

    internal void EndSomPresentation()
    {
        if (_somRoot is not null && IsInstanceValid(_somRoot)) { _somRoot.Hide(); _somRoot.QueueFree(); }
        _somRoot = null; _somRising.Clear(); _somPopPoints.Clear();
        _somBeasts.Clear(); _somBeastPoints.Clear(); _somTweens.Clear(); _somParticles.Clear(); _somPaused = false;
        _somScatters.Clear();
        EndSomResonance();
        foreach (var pawn in _pawns.Values) pawn.ClearSomPresentation();
        _attackAudio.StopSomSounds();
    }
    private void ResetSomPresentation()
    {
        EndSomPresentation();
        SomSummons = SomRains = SomStops = SomLights = SomVeils = SomImpacts = SomBreaks = 0;
        SomCalls = SomBursts = SomEmergencies = SomMaxSwarm = SomFocusedRains = SomScatteredRains = SomDaunts = SomWeakAttacks = 0;
        SomStances = SomShortStances = SomBookAbsorptions = 0;
        SomThrows = SomReturns = SomReturnBranches = SomHushedReturns = SomBackswings = SomThreadThrows = SomBackfires = SomScraps = 0;
    }

    private void TrackSomTween(Tween tween, double speed)
    {
        _somTweens.Add((tween, speed));
        // 完了直後はIsValidがまだ真でもPlayできない。再開対象からその場で外す。
        tween.Finished += () => _somTweens.RemoveAll(t => t.Tween == tween);
    }
    internal void SetSomClock(bool paused, double speed)
    {
        _somPaused = paused;
        _attackAudio.PauseSomSounds(paused);
        if (_somRoot is not null && IsInstanceValid(_somRoot))
            _somRoot.ProcessMode = paused ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        _somTweens.RemoveAll(t => !t.Tween.IsValid());
        foreach (var (tween, original) in _somTweens)
        { tween.SetSpeedScale((float)(Math.Max(.1, speed) / original)); if (paused) tween.Pause(); else tween.Play(); }
        _somParticles.RemoveAll(p => !IsInstanceValid(p));
        foreach (var p in _somParticles) p.SpeedScale = paused ? 0 : speed;
        foreach (var beast in _somBeasts.Values) beast.SetClock(paused, speed);
        _somScatters.RemoveAll(s => !IsInstanceValid(s) || s.IsQueuedForDeletion());
        foreach (var scatter in _somScatters) scatter.SetClock(paused, speed);
        _somBolts.RemoveAll(b => !IsInstanceValid(b) || b.IsQueuedForDeletion());
        foreach (var bolt in _somBolts) bolt.SetClock(paused, speed);
        foreach (var pawn in _pawns.Values) pawn.SetSomClock(paused, speed);
    }

    internal void BeginSomCall(BattlePawn3D som, int count, bool emergency, BattlePawn3D? partner, bool speak, double speed)
    {
        som.AnimationSpeed = speed;
        SomCalls++; SomSummons += count; SomMaxSwarm = Math.Max(SomMaxSwarm, count);
        if (emergency) SomEmergencies++;
        som.ShowSomExpression("som_summon", 1.1);
        Vector3 origin = emergency && partner is not null
            ? partner.Position + new Vector3(som.Team == 0 ? .9f : -.9f, 0, .35f) : SomBeastOrigin(som);
        if (emergency && partner is not null)
        {
            var warning = ShockMarkFx.Sprite(SomRoot, partner.SomCenter + _camera.GlobalBasis.Z * .08f,
                SomFx.Danger, partner.SomHeight * .65f, Colors.White);
            warning.RenderPriority = 3;
            var pulse = warning.CreateTween();
            pulse.TweenProperty(warning, "scale", Vector3.One * 1.25f, .14 / speed);
            pulse.TweenProperty(warning, "scale", Vector3.One * .95f, .14 / speed);
            pulse.TweenProperty(warning, "modulate:a", 0f, .24 / speed);
            pulse.TweenCallback(Callable.From(warning.QueueFree)); TrackSomTween(pulse, speed);
        }
        SomCircle(origin, emergency ? 1.5f : 1.1f, speed);
        if (speak) Float(som, emergency ? "今度こそ、本命の下僕だ！！"
            : count > 1 ? "いでよ、我が下僕ども！！" : "いでよ、我が忠実なる下僕よ！！", SomFx.Gold, false, 3.5f);
        _attackAudio.PlaySom(SomSound.Summon);
    }

    private void SomCircle(Vector3 origin, float size, double speed)
    {
        var circle = ShockMarkFx.Sprite(SomRoot, origin + Vector3.Up * .065f, SomFx.Circle, size, SomFx.Violet);
        circle.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
        circle.Rotation = new Vector3(-Mathf.Pi / 2, 0, 0); circle.Scale = Vector3.One * .3f;
        var tween = circle.CreateTween();
        tween.TweenProperty(circle, "scale", Vector3.One, .14 / speed);
        tween.TweenInterval(.22 / speed);
        tween.TweenProperty(circle, "modulate:a", 0f, .3 / speed);
        tween.TweenCallback(Callable.From(circle.QueueFree)); TrackSomTween(tween, speed);
    }

    internal void PlaceSomBurstBeast(BattlePawn3D som, BattleEvent summon, bool emergency,
        BattlePawn3D? partner, int ordinal, int count, double speed)
    {
        if (summon.TargetId is not int id) return;
        Vector3 origin = emergency && partner is not null
            ? partner.Position + new Vector3(som.Team == 0 ? .9f : -.9f, 0, .35f) : SomBeastOrigin(som);
        origin += new Vector3(-(som.Team == 0 ? 1 : -1) * (ordinal % 2) * .58f, 0, (ordinal - (count - 1) * .5f) * .60f);
        Vector3 destination = PawnPosition(summon.Team ?? 1 - som.Team, summon.Slot);
        // 同じ席へ重ねた5匹目も輪郭を分ける。台本の席番号は動かさない。
        if (ordinal >= 4) destination += new Vector3(0, 0, .35f);
        var beast = new SomBeast3D { UsePurchasedCharge = UsePurchasedSomFx }; SomRoot.AddChild(beast);
        beast.Configure(origin, destination, emergency, som.Team == 1, speed);
        _somBeasts[id] = beast; _somBeastPoints[id] = beast.DestinationPoint;
        _somOwners[id] = som.InstanceId;
    }

    internal void RunSomBurstBeasts(BattlePawn3D som, IEnumerable<int> ids, bool speak)
    {
        foreach (int id in ids) if (_somBeasts.TryGetValue(id, out var beast)) beast.Run();
        som.ShowSomExpression("som_stunned", SomFx.BeastRunSeconds + SomFx.BeastStanceSeconds + .2);
        if (speak) Float(som, "……え？", SomFx.Violet, false, 3.1f);
        _attackAudio.PlaySom(SomSound.Snub);
    }

    internal void TakeSomStance(BattlePawn3D som, IEnumerable<int> ids, bool shortened)
    {
        foreach (int id in ids)
            if (_somBeasts.TryGetValue(id, out var beast))
            { beast.TakeStance(shortened); SomStances++; if (shortened) SomShortStances++;
                if (_somStanding.Contains(id)) FindPawn(id)?.UseSomBeastProxy(); }
        som.ShowSomExpression("som_stunned", (shortened ? SomFx.BeastShortStanceSeconds : SomFx.BeastStanceSeconds) + .2);
    }

    internal void BurstSomBeasts(IEnumerable<int> ids, double speed)
    {
        foreach (int id in ids)
        {
            if (!_somBeasts.Remove(id, out var beast)) continue;
            beast.Arrive(); var point = beast.DestinationPoint;
            _somBeastPoints[id] = point; _shockPoints[id] = point;
            var scatter = new SomSpiritScatter3D(); SomRoot.AddChild(scatter);
            scatter.Configure(point, _camera, id, beast.Emergency, speed);
            scatter.SetClock(_somPaused, speed); _somScatters.Add(scatter);
            beast.Hide(); beast.QueueFree(); SomBursts++;
            SomPurchasedBurst(point, id, speed, SomFx.SpiritRebel);
        }
        _attackAudio.PlaySom(SomSound.Break);
    }

    private void SomPurchasedBurst(Vector3 point, int seed, double speed, Color? tint = null)
    {
        const string path = "res://PolyBlocks/EffectBlocks/assets/energy/electric_sparks_1.tscn";
        if (!UsePurchasedSomFx || !ResourceLoader.Exists(path)) return;
        var particles = GD.Load<PackedScene>(path).Instantiate<GpuParticles3D>();
        particles.SetScript(default);
        var material = (ParticleProcessMaterial)particles.ProcessMaterial.Duplicate(true);
        particles.ProcessMaterial = material; material.Color = tint ?? ThunderFx.Cyan;
        material.InitialVelocityMin = 1; material.InitialVelocityMax = 2;
        material.ScaleMin = .06f; material.ScaleMax = .1f;
        particles.Amount = 8; particles.Lifetime = .24; particles.OneShot = true; particles.Preprocess = 0;
        particles.SpeedScale = speed; particles.UseFixedSeed = true; particles.Seed = (uint)seed;
        particles.Emitting = false; SomRoot.AddChild(particles); particles.GlobalPosition = point;
        particles.Emitting = true; particles.Restart(); _somParticles.Add(particles);
        var life = particles.CreateTween(); life.TweenInterval(.32 / speed);
        life.TweenCallback(Callable.From(particles.QueueFree)); TrackSomTween(life, speed);
    }

    internal bool ShowSomBeastDischarge(int? from, int? to, double speed)
    {
        if (from is not int id || !_somBeastPoints.TryGetValue(id, out var point)) return false;
        if (ElectricPoint(to) is Vector3 end)
        { DischargePlays++; ThunderFx.Arc(SomRoot, point, end, .028f, .20 / speed); ThunderFx.Burst(SomRoot, end, .4f, .18 / speed); }
        return true;
    }

    internal void ShowSomDaunt(BattlePawn3D pawn, double speed)
    {
        SomDaunts++; pawn.FlinchSomDaunt();
        ShockMarkFx.Ring(SomRoot, pawn.FxPoint, new Color("c7c9a8"), .6f, .20 / speed);
    }
    internal void ShowSomWeakAttack(BattlePawn3D pawn)
    { SomWeakAttacks++; pawn.WeakenSomAttack(); Float(pawn, "萎縮 · 威力½", new Color("c7c9a8"), false); }

    internal void BeginSomSummon(BattlePawn3D som, double speed, bool first)
    {
        SomSummons++;
        som.ShowMovementPortrait("som_summon", 1.05);
        var circle = ShockMarkFx.Sprite(SomRoot, SomBeastOrigin(som) + Vector3.Up * .065f,
            SomFx.Circle, 1.1f, SomFx.Violet);
        circle.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
        circle.Rotation = new Vector3(-Mathf.Pi / 2, 0, 0);
        circle.Scale = Vector3.One * .3f;
        var tween = circle.CreateTween();
        tween.TweenProperty(circle, "scale", Vector3.One, .18 / speed);
        tween.TweenInterval(.25 / speed);
        tween.TweenProperty(circle, "modulate", new Color(SomFx.Violet, 0), .42 / speed);
        tween.TweenCallback(Callable.From(circle.QueueFree));
        if (first) Float(som, "いでよ、我が忠実なる下僕よ！！", SomFx.Gold, false, 3.5f);
        _attackAudio.PlaySom(SomSound.Summon);
    }

    internal void SomBeastLooksBack(BattlePawn3D som, BattlePawn3D beast)
    {
        // 旧い台本の単体召喚にも、同じ電気の精霊を使う。
        beast.PlaceSomBeast(SomBeastOrigin(som));
    }
    internal void SomBeastSnubs(BattlePawn3D som, BattlePawn3D beast, bool first)
    {
        // 光塊が敵陣へ飛ぶ。途中の戦闘イベントは発生させない。
        beast.RunSomBeast(flip: som.Team == 1);
        som.ShowMovementPortrait("som_stunned", SomFx.BeastRunSeconds + .2);
        if (first) Float(som, "……え？", SomFx.Violet, false, 3.1f);
        _attackAudio.PlaySom(SomSound.Snub);
    }

    internal void SomBeastArrives(BattlePawn3D beast, double speed)
    {
        beast.FinishSomBeastRun();
        MakeGroundRing(beast.Home, beast.Team == 0 ? UiKit.Player : UiKit.Enemy, .7f, .24 / speed);
    }

    internal void RaiseSomLight(int index, int? source, double speed)
    {
        if (_somRising.ContainsKey(index) || ElectricPoint(source) is not Vector3 point) return;
        _somPopPoints[index] = point;
        var mote = ShockMarkFx.Sprite(SomRoot, point, SomFx.Light, .30f, SomFx.Gold);
        var tween = mote.CreateTween();
        tween.TweenProperty(mote, "global_position", point + Vector3.Up * .65f, .24 / speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _somRising[index] = (mote, tween);
        TrackSomTween(tween, speed);
    }

    internal void GatherSomLight(BattlePawn3D som, IReadOnlyList<int> sources, bool blocked, double speed)
    {
        if (blocked) SomStops++; else SomRains++;
        Vector3 book = SomBookPoint(som);
        foreach (int source in sources)
        {
            Sprite3D? mote = null;
            if (_somRising.Remove(source, out var rising) && IsInstanceValid(rising.Mote))
            { rising.Rise.Kill(); mote = rising.Mote; }
            else if (_somPopPoints.TryGetValue(source, out Vector3 point))
                mote = ShockMarkFx.Sprite(SomRoot, point + Vector3.Up * .65f, SomFx.Light, .30f, SomFx.Gold);
            if (mote is null) continue;
            SomLights++;
            Vector3 end = blocked ? mote.GlobalPosition.Lerp(book, .45f) : book;
            TrackSomTween(SomFx.Flight(mote, end, .45f, .26 / speed, blocked,
                follow: blocked ? null : () => SomBookPoint(som)), speed);
        }
        _attackAudio.PlaySom(blocked ? SomSound.Stop : SomSound.Gather);
    }

    internal void AbsorbSomBookLight(BattlePawn3D som, int count, double speed)
    {
        SomBookAbsorptions++;
        var seal = ShockMarkFx.Sprite(SomRoot, SomBookPoint(som), SomFx.Circle, .43f, SomFx.Gold);
        var halo = ShockMarkFx.Sprite(SomRoot, SomBookPoint(som), ShockMarkFx.Halo, .52f, new Color(SomFx.Gold, .5f));
        seal.NoDepthTest = halo.NoDepthTest = true;
        seal.RenderPriority = halo.RenderPriority = 3;
        var pulse = seal.CreateTween();
        pulse.TweenMethod(Callable.From<float>(t => {
            seal.GlobalPosition = halo.GlobalPosition = SomBookPoint(som);
            seal.Scale = Vector3.One * (.72f + .18f * MathF.Sin(t * MathF.PI));
            seal.Modulate = new Color(SomFx.Gold, 1 - t);
            halo.Modulate = new Color(SomFx.Gold, Math.Min(.55f, .25f + count * .025f) * (1 - t));
        }), 0f, 1f, .22 / speed);
        pulse.TweenCallback(Callable.From(() => { seal.QueueFree(); halo.QueueFree(); })); TrackSomTween(pulse, speed);
    }

    internal void RainSomLight(BattlePawn3D som, int count, IReadOnlySet<int> inverted, double speed,
        BattlePawn3D? focused = null, BattlePawn3D? scattered = null)
    {
        Vector3 book = SomBookPoint(som);
        if (focused is not null)
        {
            SomFocusedRains++;
            ShockMarkFx.Ring(SomRoot, focused.FxPoint, SomFx.Gold, .9f, .24 / speed);
        }
        if (scattered is not null)
        {
            SomScatteredRains++;
            var scattering = scattered.FxPoint + Vector3.Up * .6f;
            var pulse = SomRoot.CreateTween(); pulse.TweenInterval(.09 / speed);
            pulse.TweenCallback(Callable.From(() => ShockMarkFx.Ring(SomRoot, scattering, SomFx.Murky, .75f, .22 / speed)));
            TrackSomTween(pulse, speed);
        }
        // 本数は小さな粒で示し、各粒の大きさと一斉の光量には上限を掛ける。
        int drops = Math.Clamp(count, 1, 18);
        foreach (var ally in _pawns.Values.Where(p => p.Team == som.Team && p.PresentationAlive
            && (focused is null || p == focused)))
        {
            bool murky = inverted.Contains(ally.InstanceId);
            for (int i = 0; i < drops; i++)
            {
                float offset = (i - (drops - 1) * .5f) * .12f;
                Vector3 end = ally.FxPoint + _camera.GlobalBasis.X * offset;
                var mote = ShockMarkFx.Sprite(SomRoot, book, SomFx.Light, .27f, murky ? SomFx.Murky : SomFx.Gold);
                TrackSomTween(SomFx.Flight(mote, end, (focused is null ? .75f : .15f) + (i % 3) * .16f,
                    (.28 + (i % 4) * .018 + (scattered is null ? 0 : .1)) / speed,
                    via: scattered?.FxPoint + Vector3.Up * .6f), speed);
            }
        }
        ShockMarkFx.Glow(SomRoot, book, new Color(SomFx.Gold, .25f), .42f + Math.Min(count, 14) * .015f, .26 / speed);
        _attackAudio.PlaySom(SomSound.Rain);
    }

    internal void SetSomVeil(BattlePawn3D pawn, int amount, bool gained, double speed)
    {
        int old = pawn.SomVeilAmount;
        pawn.SetSomVeil(amount, gained);
        if (gained)
        {
            SomVeils++;
            ShockMarkFx.Sparks(SomRoot, pawn.FxPoint, SomFx.Gold, 4, .36f, .20 / speed);
            _attackAudio.PlaySom(SomSound.Veil);
        }
        else if (old > 0 && amount == 0 && pawn.PresentationAlive)
        {
            SomBreaks++;
            ShockMarkFx.Sparks(SomRoot, pawn.FxPoint, SomFx.Gold, 10, .8f, .32 / speed);
            _attackAudio.PlaySom(SomSound.Break);
        }
    }
    internal void SomVeilContact(BattlePawn3D pawn, double speed)
    {
        if (!pawn.PresentationAlive || pawn.SomVeilAmount <= 0) return;
        SomImpacts++; pawn.RippleSomVeil();
        ShockMarkFx.Sparks(SomRoot, pawn.FxPoint, SomFx.Gold, 5, .4f, .22 / speed);
        _attackAudio.PlaySom(SomSound.Impact);
    }
}
