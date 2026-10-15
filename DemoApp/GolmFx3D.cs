using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 腕・捕獲・吸気を一つの時計で動かす。待ち手も同じ時計なので停止・速度変更で先走らない。
public partial class GolmFx3D : Node3D
{
    private BattlePawn3D _owner = null!;
    private BattlePawn3D? _captive;
    private Camera3D _camera = null!;
    private Node3D _hand = null!;
    private Sprite3D _closedHand = null!, _openHand = null!;
    private readonly List<Sprite3D> _breathClouds = new();
    private readonly List<GpuParticles3D> _particles = new();
    private readonly List<Sprite3D> _bits = new();
    private readonly List<GolmPunchImpact3D> _punchImpacts = new();
    internal IEnumerable<GolmPunchImpact3D> PunchImpacts => _punchImpacts;
    private readonly ImmediateMesh _threads = new();
    private Sprite3D? _proxy;
    private MeshInstance3D _mouthGlow = null!;
    private ShaderMaterial _mouthMaterial = null!;
    private AudioStreamPlayer _voice = null!;
    private AudioStreamPlayer _punchVoice = null!;
    private const string PunchHitPath = "res://assets/audio/se/golm_punch_hit.mp3";
    private TaskCompletionSource<bool>? _wait;
    private Action<float>? _frame;
    private double _elapsed, _duration, _speed = 1;
    private float _clock, _shake;
    private Vector3 _shakeOffset;
    private bool _paused, _cancelled;
    internal bool HandOut { get; private set; }
    internal bool HasCaptive => _proxy is not null && IsInstanceValid(_proxy);
    internal Vector3? CaptivePoint => HasCaptive ? _proxy!.GlobalPosition : null;
    internal double Elapsed => _elapsed;
    internal int PurchasedParticleCount => _particles.Count;
    internal Action<string>? PhaseChanged;
    internal string Phase { get; private set; } = "待機";
    private Vector3 Up => _camera.GlobalBasis.Y;
    private Vector3 Right => _camera.GlobalBasis.X;
    private Vector3 Front => _camera.GlobalBasis.Z;
    private Vector3 Mouth => _owner.GolmMouth(_camera);
    private Vector3 Wrist => _owner.GolmWrist(_camera);
    private float Facing => _owner.Team == 0 ? 1 : -1;
    private const float KnuckleReach = 1.10f;
    private Vector3 Knuckles => _hand.GlobalPosition + Right * Facing * KnuckleReach;
    internal static readonly Color Soul = new("bcb1ff");

    internal void Configure(BattlePawn3D owner, Camera3D camera, double speed, bool paused)
    {
        _owner = owner; _camera = camera; SetClock(paused, speed);
        _voice = new AudioStreamPlayer { VolumeDb = -12 }; AddChild(_voice);
        _punchVoice = new AudioStreamPlayer { VolumeDb = -12, Stream = GD.Load<AudioStream>(PunchHitPath) }; AddChild(_punchVoice);
        var lines = new MeshInstance3D { Mesh = _threads, MaterialOverride = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true, RenderPriority = 4,
        }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(lines);
        _mouthMaterial = FireFx.Material(3, Soul); _mouthMaterial.SetShaderParameter("persistent", true);
        _mouthGlow = FireFx.Card(this, Mouth, Vector2.One * 1.0f, _mouthMaterial); _mouthGlow.Hide();
        BuildHand();
    }

    internal void SetClock(bool paused, double speed)
    {
        _paused = paused; _speed = Math.Max(.1, speed);
        if (_voice is not null) { _voice.StreamPaused = paused; _voice.PitchScale = (float)Math.Clamp(_speed, .6, 2.5); }
        if (_punchVoice is not null) { _punchVoice.StreamPaused = paused; _punchVoice.PitchScale = (float)Math.Clamp(_speed, .6, 2.5); }
        foreach (var p in _particles) if (IsInstanceValid(p)) p.SpeedScale = paused ? 0 : _speed;
        _punchImpacts.RemoveAll(p => !IsInstanceValid(p) || p.IsQueuedForDeletion());
        foreach (var hit in _punchImpacts) hit.SetClock(paused,_speed);
    }

    private Task<bool> Animate(string phase, double seconds, Action<float> frame)
    {
        if (_cancelled) return Task.FromResult(false);
        _wait?.TrySetResult(false);
        _elapsed = 0; _duration = seconds; _frame = frame;
        // Godotの時計から後続へ通知する。await後も中断を確認し、破棄済みの腕に触れない。
        _wait = new();
        Phase = phase; PhaseChanged?.Invoke(phase); frame(0);
        return _wait.Task;
    }

    public override void _Process(double delta)
    {
        if (_cancelled) return;
        if (!IsInstanceValid(_owner) || !_owner.IsInsideTree() || _owner.Hp <= 0) { Cancel(); return; }
        if (_paused) return;
        float dt = (float)(delta * _speed); _clock += dt;
        if (_frame is not null)
        {
            _elapsed += dt;
            _frame((float)Math.Min(1, _elapsed / _duration));
            if (_elapsed >= _duration) { _frame = null; var completed = _wait; _wait = null; completed?.TrySetResult(true); }
        }
        if (_mouthGlow.Visible) { _mouthGlow.GlobalPosition = Mouth; _mouthMaterial.SetShaderParameter("clock", _clock); }
        if (IsInstanceValid(_camera))
        {
            _camera.Position -= _shakeOffset;
            _shake = Math.Max(0, _shake - dt * 4);
            _shakeOffset = new Vector3(MathF.Sin(_clock * 83), MathF.Cos(_clock * 67), 0) * _shake * .085f;
            _camera.Position += _shakeOffset;
        }
    }

    private void Sound(string cue)
    {
        // 命中音の余韻を、直後の帰還・捕獲の音で切らない。
        var voice = cue == "punch_hit" ? _punchVoice : _voice;
        if (cue != "punch_hit") voice.Stream = GD.Load<AudioStream>($"res://assets/audio/se/golm_{cue}.wav");
        voice.PitchScale = (float)Math.Clamp(_speed, .6, 2.5); voice.Play(); voice.StreamPaused = _paused;
    }

    internal async Task<bool> Launch(BattlePawn3D target, bool open = false)
    {
        if (_cancelled) return false;
        HandOut = true; _owner.SetGolmRocketPose(true); _hand.Show(); Curl(open ? 0 : 1);
        _hand.GlobalBasis = _camera.GlobalBasis;
        // 手首を接続点として扱い、右腕の先に拳を一拍見せてから射出する。
        // 構えと飛行を同じ時計で進め、境界で停止しても待ち手を先へ進めない。
        Vector3 from = Wrist, to = target.FxPoint + Up * .30f + Front * .12f - Right * Facing * KnuckleReach;
        var fire = PurchasedFlame();
        Sound("launch");
        bool live = await Animate("飛ばす", .32, p => {
            float flight = Math.Clamp((p * .32f - .09f) / .23f,0,1);
            _hand.GlobalPosition = from.Lerp(to, flight * flight * (3 - 2 * flight));
            _hand.GlobalBasis = _camera.GlobalBasis;
            if (fire is not null) {
                fire.Visible = flight > 0;
                fire.GlobalPosition = _hand.GlobalPosition - (to-from).Normalized() * .45f;
            }
            DrawJet(from, _hand.GlobalPosition, flight > 0 ? 1-flight : 0);
        });
        if (!live || _cancelled) return false;
        _threads.ClearSurfaces(); StopParticles();
        return live;
    }

    internal async Task<bool> Impact(int amount, bool stomp = false, bool captureFollows = false)
    {
        if (_cancelled) return false;
        Sound(stomp ? "hit" : "punch_hit"); _shake = !stomp && !captureFollows ? 1.55f : amount >= 40 ? 1.25f : .60f;
        Vector3 point = Knuckles;
        if (stomp) point = _impactPoint;
        if (!stomp)
        {
            var burst = new GolmPunchImpact3D(); AddChild(burst);
            burst.Configure(point+Front*.16f,_camera,!captureFollows,_paused,_speed);
            _punchImpacts.RemoveAll(p => !IsInstanceValid(p) || p.IsQueuedForDeletion());
            _punchImpacts.Add(burst);
        }
        var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = .65f, OuterRadius = .74f, Rings = 32, RingSegments = 6 },
            MaterialOverride = Metal(new Color("cdb793")), Position = point, RotationDegrees = new Vector3(90,0,0) };
        AddChild(ring);
        bool live = await Animate(stomp ? "踏み潰す" : "殴る", .10, p => {
            ring.Scale = Vector3.One * (.35f + p * .9f); ring.Visible = p < .95f;
            if (!stomp) _hand.Scale = Vector3.One * (1 + MathF.Sin(p * Mathf.Pi) * .12f);
        });
        if (!live || _cancelled) return false;
        ring.QueueFree(); return true;
    }
    private Vector3 _impactPoint;
    internal void PrepareStomp(BattlePawn3D target) => _impactPoint = target.Home + Vector3.Up * .10f;
    internal async Task<bool> Stomp(BattlePawn3D target)
    {
        if (_cancelled) return false;
        PrepareStomp(target);
        var foot = Box(this, _impactPoint + Vector3.Up * 1.5f, new Vector3(1.15f,.60f,.85f), new Color("655b50"));
        if (!await Animate("足を上げる", .18, p => foot.Position = _impactPoint + Vector3.Up * (1-p*p)*1.5f) || _cancelled) return false;
        bool live = await Impact(50, true);
        if (!live || _cancelled) return false;
        foot.QueueFree(); return true;
    }

    internal async Task<bool> Grab(BattlePawn3D target, bool afterHit)
    {
        if (_cancelled) return false;
        if (!afterHit || !HandOut) if (!await Launch(target, open: true) || _cancelled) return false;
        _captive = target;
        _proxy = target.MakeGolmProxy(this); _proxy.RenderPriority = 4;
        Vector3 offset = Front * .12f + Up * .22f + Right * Facing * .65f;
        Vector3 start = _proxy.GlobalPosition, grip = _hand.GlobalPosition + offset;
        Sound("dock");
        if (!await Animate("掴む", .16, p => {
            Curl(p); _proxy.GlobalPosition = start.Lerp(grip, p); _proxy.Scale = Vector3.One * Mathf.Lerp(1, .50f, p);
        }) || _cancelled) return false;
        Vector3 from = _hand.GlobalPosition, to = Mouth - Right * Facing * .25f + Front * .3f;
        var fire = PurchasedFlame();
        Sound("launch");
        bool live = await Animate("持ち帰る", .28, p => {
            _hand.GlobalPosition = from.Lerp(to, p * p * (3-2*p));
            _proxy.GlobalPosition = _hand.GlobalPosition + offset;
            if (fire is not null) fire.GlobalPosition = _hand.GlobalPosition - (to-from).Normalized() * .45f;
        });
        if (!live || _cancelled) return false;
        StopParticles(); return live;
    }

    internal async Task<bool> Swallow(BattlePawn3D target)
    {
        if (_cancelled) return false;
        target.PrepareGolmSwallow(); _captive = target;
        bool captured = HasCaptive;
        Vector3 from = HasCaptive ? _proxy!.GlobalPosition : target.FxPoint;
        _mouthGlow.Show(); Sound("inhale");
        if (!await Animate("呑む", .43, p => {
            float gray = captured ? 0 : p;
            target.SetGolmDrain(gray, 0);
            if (HasCaptive) ((ShaderMaterial)_proxy!.MaterialOverride).SetShaderParameter("golm_gray", gray);
            DrawSoul(from, Mouth, p);
        }) || _cancelled) return false;
        if (captured)
        {
            _threads.ClearSurfaces(); _mouthGlow.Hide();
            if (!await TossCaptive(target) || _cancelled) return false;
            return await Return(.16);
        }
        _threads.ClearSurfaces(); ClearBits(); Sound("crumble");
        for (int i = 0; i < 24; i++) _bits.Add(ShockMarkFx.Sprite(this, from, ShockMarkFx.Halo, .05f + (i%3)*.018f, new Color("92919c")));
        bool live = await Animate("崩れる", .24, p => {
            target.SetGolmDrain(1, p);
            if (HasCaptive) ((ShaderMaterial)_proxy!.MaterialOverride).SetShaderParameter("golm_crumble", p);
            for (int i = 0; i < _bits.Count; i++) {
                float angle = i * 2.39996f;
                _bits[i].GlobalPosition = from + Right * MathF.Cos(angle) * (.12f+p*.48f)
                    + Up * (MathF.Sin(angle)*.45f - p*p*.85f) + Front * .15f;
                _bits[i].Modulate = new Color("92919c", 1-p); }
        });
        if (!live || _cancelled) return false;
        ClearBits(); _mouthGlow.Hide();
        if (HasCaptive) { _proxy!.Hide(); _proxy.QueueFree(); _proxy = null; }
        if (live && HandOut) await Return(.16);
        return live;
    }

    private async Task<bool> TossCaptive(BattlePawn3D target)
    {
        if (_cancelled || !HasCaptive) return false;
        var proxy = _proxy!;
        var material = (ShaderMaterial)proxy.MaterialOverride;
        material.SetShaderParameter("golm_gray", 0f);
        material.SetShaderParameter("golm_crumble", 0f);
        Vector3 from = proxy.GlobalPosition, to = target.GolmDiscardPoint(_camera);
        Vector3 handFrom = _hand.GlobalPosition;
        float turn = target.Team == 0 ? -1 : 1;
        Sound("toss");
        if (!await Animate("ぽいっと投げ返す", .38, p => {
            Curl(1-Math.Min(1,p*4));
            _hand.GlobalPosition = handFrom + (Up*.12f + Right*turn*.18f)*MathF.Sin(p*Mathf.Pi);
            proxy.GlobalPosition = from.Lerp(to,p) + Vector3.Up*MathF.Sin(p*Mathf.Pi)*1.35f;
            proxy.Scale = Vector3.One*Mathf.Lerp(.50f,.82f,p);
            proxy.Rotation = new Vector3(0,0,turn*p*1.15f);
        }) || _cancelled) return false;
        Sound("land");
        if (!await Animate("元の場所でへたり込む", .12, p => {
            proxy.GlobalPosition = to + Vector3.Up*MathF.Sin(p*Mathf.Pi)*.11f;
            proxy.Rotation = new Vector3(0,0,turn*(1.15f+MathF.Sin(p*Mathf.Pi)*.08f));
        }) || _cancelled) return false;
        target.LeaveGolmDiscarded(to);
        proxy.Hide(); proxy.QueueFree(); _proxy = null;
        return true;
    }

    internal async Task<bool> Return(double duration = .27)
    {
        if (_cancelled) return false;
        if (!HandOut) return true;
        Vector3 from = _hand.GlobalPosition; Sound("launch");
        var fire = PurchasedFlame();
        bool docked = false;
        bool live = await Animate("戻る", duration, p => {
            float flight = Math.Min(1,p*(float)(duration/Math.Max(.01,duration-.07)));
            _hand.GlobalPosition = from.Lerp(Wrist, flight*flight*(3-2*flight)); _hand.Scale = Vector3.One;
            if (!docked && fire is not null) fire.GlobalPosition = _hand.GlobalPosition - (Wrist-from).Normalized() * .4f;
            if (flight >= 1 && !docked) { docked = true; StopParticles(); Curl(1); Sound("dock"); }
        });
        if (!live || _cancelled) return false;
        _hand.Hide(); HandOut = false;
        if (IsInstanceValid(_owner)) _owner.SetGolmRocketPose(false);
        return true;
    }

    internal async Task<bool> Spit(BattlePawn3D target, IReadOnlyList<GolmPresentation.SpitPart> parts)
    {
        if (_cancelled) return false;
        ClearBits(); Sound("spit");
        var sizes = new List<float>();
        var smokeByPart = new List<GpuParticles3D?>();
        foreach (var part in parts)
        {
            float size = SpitSize(part.Amount); sizes.Add(size);
            Color tint = SpitColor(part.Label);
            _bits.Add(ShockMarkFx.Sprite(this, Mouth, part.Label == "標" ? ShockMarkFx.MarkTexture(target.Team) : ShockMarkFx.Halo, size, tint));
            smokeByPart.Add(part.Label is "毒" or "火" or "濃" ? PurchasedSmoke(tint) : null);
            for (int i=0;i<8;i++)
                _breathClouds.Add(ShockMarkFx.Sprite(this, Mouth, ShockMarkFx.Halo, size * .8f, new Color(tint,.3f)));
        }
        bool live = await Animate("吐く", .55, p => {
            _threads.ClearSurfaces();
            for (int i = 0; i < parts.Count; i++) {
                float q = Math.Clamp(p * 1.7f - i * .025f, 0, 1);
                Vector3 to = target.FxPoint + Right * ((i-(parts.Count-1)*.5f)*.25f);
                Vector3 at = Mouth.Lerp(to, q) + Up * MathF.Sin(q*Mathf.Pi)*.15f;
                _bits[i].GlobalPosition = at;
                _bits[i].Scale = Vector3.One * (.45f + q*.85f);
                _bits[i].Modulate = new Color(SpitColor(parts[i].Label), 1-Math.Max(0,p-.75f)*4);
                if (parts[i].Label == "雷") DrawBreath(at-Right*.2f,at+Right*.2f,SpitColor("雷"),.015f,1-p,true);
                for (int k=0;k<8;k++) {
                    float trail=Math.Clamp(q-k*.028f,0,1);
                    Vector3 drift=Right*MathF.Sin(k*2.4f+i)*.20f+Up*MathF.Cos(k*3.1f+i)*.20f;
                    var cloud=_breathClouds[i*8+k];
                    cloud.GlobalPosition=Mouth.Lerp(to,trail)+drift*(.3f+q*.7f)+Up*Math.Max(0,p-.6f)*.7f;
                    cloud.Scale=Vector3.One*(.65f+q*.9f);
                    cloud.Modulate=new Color(SpitColor(parts[i].Label), .20f*(1-Math.Max(0,p-.7f)/.3f));
                }
                if (smokeByPart[i] is { } smoke) smoke.GlobalPosition=at;
            }
        });
        if (!live || _cancelled) return false;
        _threads.ClearSurfaces(); ClearBits(); StopParticles(); return live;
    }
    internal static float SpitSize(int amount) => .30f + MathF.Sqrt(Math.Max(0, amount)) * .12f;
    private static Color SpitColor(string label) => label switch {
        "毒" => new("b065e9"), "火" => new("ff8d38"), "雷" => new("84e5ff"), "標" => new("3b254c"), _ => new("8272ac") };

    private void DrawSoul(Vector3 from, Vector3 to, float p)
    {
        _threads.ClearSurfaces();
        for (int strand = 0; strand < 7; strand++) {
            _threads.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            for (int j = 0; j <= 32; j++) {
                float q = j / 32f;
                float envelope = MathF.Sin(q*Mathf.Pi);
                Vector3 center = from.Lerp(to,q) + Up * MathF.Sin(q*9 + strand*1.8f - p*13)*envelope*.16f
                    + Right * MathF.Cos(q*7+strand*2.2f-p*10)*envelope*.12f;
                float light = (.25f+.75f*MathF.Pow(Math.Max(0,MathF.Sin(q*10-p*18+strand)),4))*MathF.Sin(p*Mathf.Pi);
                _threads.SurfaceSetColor(new Color(Soul, light));
                float width = .008f + (strand%3)*.003f;
                _threads.SurfaceAddVertex(center+Up*width); _threads.SurfaceAddVertex(center-Up*width);
            }
            _threads.SurfaceEnd();
        }
    }
    private void DrawJet(Vector3 from, Vector3 to, float alpha)
    { _threads.ClearSurfaces(); DrawBreath(from,to,new Color("ffc16a"),.035f,alpha*.6f,false); }
    private void DrawBreath(Vector3 from, Vector3 to, Color tint, float width, float alpha, bool zigzag)
    {
        _threads.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i=0;i<=20;i++) {
            float q=i/20f; Vector3 at=from.Lerp(to,q)+Up*(zigzag ? MathF.Sin(i*4.8f+_clock*40)*.065f : MathF.Sin(q*8-_clock*12)*q*.06f);
            _threads.SurfaceSetColor(new Color(tint,alpha*(.3f+.7f*q)));
            float w=width*(.20f+q*.8f);
            _threads.SurfaceAddVertex(at+Up*w); _threads.SurfaceAddVertex(at-Up*w);
        }
        _threads.SurfaceEnd();
    }

    private void BuildHand()
    {
        _hand = new Node3D(); AddChild(_hand); _hand.GlobalBasis = _camera.GlobalBasis;
        // 本体の右腕と同じ向きに、右手の甲・親指を描いた水平の差分を使う。
        _closedHand=HandSprite("res://assets/fx/golm_fist_right.png", 5);
        _openHand=HandSprite("res://assets/fx/golm_hand_open_right.png", 3);
        _hand.Hide();
    }
    private Sprite3D HandSprite(string path,int priority)
    {
        var texture=UiKit.LoadTexture(path,mipmaps:true);
        var sprite=new Sprite3D { Texture=texture, PixelSize=1.38f/texture.GetWidth(),
            FlipH=_owner.Team==1, Shaded=false, NoDepthTest=true, RenderPriority=priority,
            TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off };
        _hand.AddChild(sprite); return sprite;
    }
    private void Curl(float p)
    {
        _closedHand.Modulate=new Color(1,1,1,p); _openHand.Modulate=new Color(1,1,1,1-p);
        _openHand.Scale=new Vector3(1-p*.20f,1-p*.10f,1);
        _closedHand.Scale=Vector3.One*(.90f+p*.10f);
        // 開閉で大きさが変わっても、左右それぞれのカフ中心を同じ接続点に保つ。
        _closedHand.Position=new Vector3(.55f*Facing,.02f,0)*_closedHand.Scale;
        _openHand.Position=new Vector3(.60f*Facing,.01f,0)*_openHand.Scale;
    }
    private static StandardMaterial3D Metal(Color tint) => new() {
        AlbedoColor=tint, ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest=true, RenderPriority=5, Roughness=.9f,
    };
    private static MeshInstance3D Box(Node3D parent, Vector3 at, Vector3 size, Color color)
    { var mesh=new MeshInstance3D { Mesh=new BoxMesh { Size=size }, Position=at, MaterialOverride=Metal(color), CastShadow=GeometryInstance3D.ShadowCastingSetting.Off }; parent.AddChild(mesh); return mesh; }

    private GpuParticles3D? PurchasedFlame()
    {
        const string path="res://PolyBlocks/EffectBlocks/assets/fire/fire_light.tscn";
        if (!ResourceLoader.Exists(path)) return null;
        var source=GD.Load<PackedScene>(path).Instantiate<Node3D>();
        var flame=source.GetNode<GpuParticles3D>("Flame"); source.RemoveChild(flame); source.Free();
        ConfigureParticles(flame, new Color("ffae54"), .12f, 16); AddChild(flame);
        return flame;
    }
    private GpuParticles3D? PurchasedSmoke(Color tint)
    {
        if (!ResourceLoader.Exists(DohaShareFx.SmokePath)) return null;
        var smoke=GD.Load<PackedScene>(DohaShareFx.SmokePath).Instantiate<GpuParticles3D>();
        ConfigureParticles(smoke,new Color(tint,.40f),.15f,18); AddChild(smoke); return smoke;
    }
    private void ConfigureParticles(GpuParticles3D p, Color tint, float scale, int count)
    {
        p.SetScript(default); p.ProcessMaterial=(ParticleProcessMaterial)p.ProcessMaterial.Duplicate(true);
        var process=(ParticleProcessMaterial)p.ProcessMaterial;
        process.Gravity=Vector3.Zero; process.InitialVelocityMin=.3f; process.InitialVelocityMax=.8f;
        process.Direction=Vector3.Up; process.Spread=25; process.ScaleMin=scale; process.ScaleMax=scale*1.6f;
        process.Color=tint; process.ColorRamp=null;
        var material=(StandardMaterial3D)p.DrawPass1.SurfaceGetMaterial(0).Duplicate(); p.MaterialOverride=material;
        p.LocalCoords=false; p.Amount=count; p.Lifetime=.18; p.Preprocess=0;
        p.UseFixedSeed=true; p.Seed=345; p.SpeedScale=_paused?0:_speed; p.Emitting=true;
        _particles.Add(p);
    }
    private void StopParticles()
    { foreach(var p in _particles) if(IsInstanceValid(p)) { p.Emitting=false; p.Hide(); p.QueueFree(); } _particles.Clear(); }
    private void ClearBits()
    {
        foreach(var bit in _bits) if(IsInstanceValid(bit)) bit.QueueFree(); _bits.Clear();
        foreach(var cloud in _breathClouds) if(IsInstanceValid(cloud)) cloud.QueueFree(); _breathClouds.Clear();
    }
    internal void Cancel()
    {
        if(_cancelled) return; _cancelled=true;
        _frame=null; _wait?.TrySetResult(false); _wait=null;
        if (_voice is not null && IsInstanceValid(_voice)) _voice.Stop();
        if (_punchVoice is not null && IsInstanceValid(_punchVoice)) _punchVoice.Stop();
        Restore(); Hide(); QueueFree();
    }
    private void Restore()
    {
        if(IsInstanceValid(_owner)) _owner.SetGolmRocketPose(false);
        if(_captive is not null && IsInstanceValid(_captive) && _captive.Hp > 0) _captive.ClearGolmPresentation();
        if(IsInstanceValid(_camera)) _camera.Position-=_shakeOffset;
        _shakeOffset=Vector3.Zero;
    }
    public override void _ExitTree() { _cancelled=true; _wait?.TrySetResult(false); Restore(); }
}
