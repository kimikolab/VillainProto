using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    private LiliRite3D? _liliRite;
    private Godot.Environment? _liliEnvironment;
    private DirectionalLight3D? _liliSun;
    private float _liliAmbient, _liliSunlight;
    private Tween? _liliLighting, _liliCleanup;
    internal int LiliRites, LiliRiteStrikes, LiliRiteReleases, LiliRiteFinishes;
    internal bool LiliRiteActive => _liliRite is not null;

    public void BeginLiliRite(BattlePawn3D actor, IReadOnlyList<BattlePawn3D> enemies, double speed, bool finish,
        IReadOnlyDictionary<int, float>? widths = null)
    {
        ResetLiliRite();
        LiliRites++;
        if (finish) LiliRiteFinishes++;
        _liliEnvironment = _world.GetChildren().OfType<WorldEnvironment>().First().Environment;
        _liliSun = _world.GetChildren().OfType<DirectionalLight3D>().First();
        _liliAmbient = _liliEnvironment.AmbientLightEnergy;
        _liliSunlight = _liliSun.LightEnergy;
        _liliLighting = CreateTween().SetParallel();
        _liliLighting.TweenProperty(_liliEnvironment, "ambient_light_energy", _liliAmbient * 0.32f, 0.22 / speed);
        _liliLighting.TweenProperty(_liliSun, "light_energy", _liliSunlight * 0.28f, 0.22 / speed);
        _liliRite = new LiliRite3D();
        _fxRoot.AddChild(_liliRite);
        _liliRite.Configure(actor, enemies, speed, widths);
    }
    public void GatherLiliRite(IReadOnlyList<BattlePawn3D> enemies) => _liliRite?.Gather(enemies);
    public void StrikeLiliRite(IReadOnlyList<BattlePawn3D> enemies, bool finish)
    {
        if (_liliRite is null) return;
        LiliRiteStrikes++;
        _liliRite.Strike(enemies, finish);
    }
    public void ReleaseLiliRite(IReadOnlyList<BattlePawn3D> allies, bool finish)
    {
        if (_liliRite is null) return;
        LiliRiteReleases++;
        _liliRite.Release(allies, finish);
    }
    public void EndLiliRite(double seconds)
    {
        if (_liliRite is null) return;
        _liliLighting?.Kill();
        _liliLighting = CreateTween().SetParallel();
        _liliLighting.TweenProperty(_liliEnvironment!, "ambient_light_energy", _liliAmbient, seconds);
        _liliLighting.TweenProperty(_liliSun!, "light_energy", _liliSunlight, seconds);
        _liliCleanup = CreateTween();
        _liliCleanup.TweenInterval(seconds);
        _liliCleanup.TweenCallback(Callable.From(ResetLiliRite));
    }
    public void ResetLiliRite()
    {
        _liliLighting?.Kill(); _liliLighting = null;
        _liliCleanup?.Kill(); _liliCleanup = null;
        if (IsInstanceValid(_liliRite)) { _liliRite!.Visible = false; _liliRite.QueueFree(); }
        _liliRite = null;
        if (IsInstanceValid(_liliEnvironment)) _liliEnvironment!.AmbientLightEnergy = _liliAmbient;
        if (IsInstanceValid(_liliSun)) _liliSun!.LightEnergy = _liliSunlight;
        _liliEnvironment = null; _liliSun = null;
    }

    public void LiliFlow(Vector3 from, Vector3 to, double seconds) => LiliFx.Flow(_fxRoot, from, to, seconds);
    public void LiliIcon(Vector3 from, Vector3 to, string key, int ordinal, double seconds)
    {
        if (StatusIconArt.KeyOf(key) is not null)
            LiliFx.Travel(_fxRoot, from, to, seconds, StatusIconArt.Texture(key), ordinal * 0.25f);
    }
    public void LiliOverflow(BattlePawn3D target, double speed) => LiliFx.Shell(_fxRoot, target.FxPoint, speed);
}
