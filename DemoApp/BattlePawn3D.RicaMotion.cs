using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

// 動画のシャベルを追跡したUV。戦闘の層・打数は台本から受け、ここでは絵だけを選ぶ。
public partial class BattlePawn3D
{
    internal readonly record struct RicaAnchors(Vector2 Socket, Vector2 Tip, Vector2 Eye, int Shape, bool EyesVisible = true, float Turn = 0, float TrailAlpha = 1);
    private static readonly Dictionary<string, RicaAnchors[]> RicaTracks = new(StringComparer.Ordinal);
    private static readonly Dictionary<int, RicaAnchors> RicaSourceIdles = new();
    private sealed record RicaIdleLayout(float Height,float Padding,RicaAnchors Anchors);
    private static readonly Dictionary<int,RicaIdleLayout?> RicaIdleLayouts=new();
    private static RicaIdleLayout? IdleLayoutOf(int stage)
    {
        if(RicaIdleLayouts.TryGetValue(stage,out var cached))return cached;
        string path=$"res://assets/portraits/battle/idle_layout/rica_stage{stage}.json";
        if(!Godot.FileAccess.FileExists(path))return RicaIdleLayouts[stage]=null;
        using var doc=JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));var root=doc.RootElement;
        Vector2 V(string key)=>new(root.GetProperty(key)[0].GetSingle(),root.GetProperty(key)[1].GetSingle());
        return RicaIdleLayouts[stage]=new(root.GetProperty("heightScale").GetSingle(),root.GetProperty("bottomPadding").GetSingle(),
            new(V("socket"),V("tip"),V("eye"),Math.Min(stage,2)));
    }
    private RicaMotionFx3D? _ricaMotionFx;
    private string? _ricaClipName;
    private string? _ricaHeldName;
    private int _ricaHeldFrame;
    private bool _ricaKeepPose;
    private bool _ricaIdleB;
    internal bool RicaLinkActive { get; private set; }
    private bool _ricaMotionPaused;
    private Tween? _ricaClockPausedTween;
    private double _ricaMotionTweenSpeed = 1;
    internal void SetRicaMotionClock(bool paused,double speed)
    {
        if (_unitId != "rica") return;
        AnimationSpeed = Math.Max(.1,speed);
        if (_motion?.IsValid() == true)
        {
            _motion.SetSpeedScale((float)(AnimationSpeed / Math.Max(.1,_ricaMotionTweenSpeed)));
            if (paused && _motion.IsRunning())
            {
                _ricaClockPausedTween = _motion;
                _motion.Pause();
            }
            else if (!paused && _ricaMotionPaused && _ricaClockPausedTween == _motion)
                _motion.Play();
        }
        if (!paused) _ricaClockPausedTween = null;
        _ricaMotionPaused = paused;
    }
    private int _ricaSwing;
    internal string? RicaClipName => AttackClipPlaying ? _ricaClipName : _ricaHeldName;
    internal double RicaFxAge => _ricaMotionFx?.Age ?? 0;
    internal int RicaWeaponShape => _ricaMotionFx?.WeaponShape ?? 0;
    internal bool RicaWeaponTrailVisible => _ricaMotionFx?.WeaponTrailVisible ?? false;
    internal float RicaWeaponLength(int shape) => GraveStage switch {
        1=>1.4f,2=>2.9f,_=>RicaClipName is null?3.1f:shape==3?3.6f:3.2f,
    };
    internal (Vector3 Root,Vector3 Tip) RicaBladeSegment(Camera3D camera)
    {
        var anchors=CurrentRicaAnchors();var size=DeathSprite.Texture.GetSize();
        Vector3 socket=MovementPortraitPoint(anchors.Socket*size,camera);
        Vector3 axis=(MovementPortraitPoint(anchors.Tip*size,camera)-socket).Normalized();
        float length=RicaWeaponLength(anchors.Shape);
        return(socket+axis*length*.12f,socket+axis*length*.88f);
    }
    internal float RicaClipPosition => (float)(_attackClipTime * (_attackClip?.Fps ?? 30));
    internal float RicaSpinSweep => _ricaMotionFx?.SpinSweep ?? 0;
    internal bool RicaSpinBothSides => _ricaMotionFx?.SpinBothSides ?? false;
    internal float CurrentRicaTurn()
    {
        var track = RicaTrackOf(RicaClipName ?? "");
        if (track.Length == 0) return 0;
        float frame = Math.Clamp(RicaClipPosition,0,track.Length-1);
        int index = (int)frame;
        // 角度を0〜360へ巻き戻さず、原画間も同じ回転方向で進める。
        return Mathf.Lerp(track[index].Turn,track[Math.Min(index+1,track.Length-1)].Turn,frame-index);
    }
    internal float RicaSpinStart => RicaTrackOf(RicaClipName??"") is { Length:>0 } track ? track[0].Turn : 0;
    internal float CurrentRicaTrailAlpha()
    {
        var track=RicaTrackOf(RicaClipName??"");
        if(track.Length==0)return 1;
        float frame=Math.Clamp(RicaClipPosition,0,track.Length-1);
        int index=(int)frame;
        return Mathf.Lerp(track[index].TrailAlpha,track[Math.Min(index+1,track.Length-1)].TrailAlpha,frame-index);
    }

    internal void EnsureRicaMotion(Camera3D camera, Func<(bool Paused, double Speed)> clock)
    {
        if (_unitId != "rica" || _ricaMotionFx is not null) return;
        _ricaMotionFx = new RicaMotionFx3D();
        AddChild(_ricaMotionFx);
        _ricaMotionFx.Start(this, camera, clock);
    }

    internal string NextRicaClip(bool first)
    {
        if (first) _ricaSwing = 0;
        int swing = _ricaSwing++;
        string key = GraveStage switch {
            0 => "shovel", 1 => "crescent",
            2 => swing % 2 == 0 ? "rise" : "return",
            _ => (swing % 3) switch { 0 => "thrust", 1 => "sweep", _ => "descend" },
        };
        return "rica_" + key;
    }

    internal System.Threading.Tasks.Task PlayRicaClip(string name, Action contact, bool keepPose)
    {
        RicaLinkActive=false;
        EndRicaApproach(refresh:false);
        _ricaHeldName = null;
        _ricaKeepPose = keepPose;
        _ricaClipName = name;
        double hold=keepPose && name is "rica_rise" or "rica_thrust" or "rica_sweep" ? 1.0/30 : .10;
        return PlayAttackClip(name, contact, hold:hold);
    }
    internal async System.Threading.Tasks.Task<bool> PounceRicaTo(Vector3 target,bool first,Action<string> startedLink)
    {
        string? link=first?null:(GraveStage,_ricaHeldName) switch {
            (2,"rica_rise")=>"rica_link_rise_return",
            (3,"rica_thrust")=>"rica_link_thrust_sweep",
            (3,"rica_sweep")=>"rica_link_sweep_descend",
            _=>null,
        };
        if(link is null || AttackClipOf(link) is null)
        {
            BeginRicaApproach();
            bool arrived=await PoisonPounce(target);
            if(!arrived)EndRicaApproach();
            return arrived;
        }
        _ricaHeldName=null;_ricaKeepPose=true;_ricaClipName=link;RicaLinkActive=true;
        startedLink(link);
        // 次の標的へ移動する0.3秒にも全身セルを進める。接触通知は一つも発生させない。
        var animation=PlayAttackClip(link,hold:0);
        bool moved=await PoisonPounce(target);
        if(!moved && IsInstanceValid(this))CancelAttackClip();
        await animation;
        RicaLinkActive=false;
        return moved && IsInstanceValid(this) && DeathActive;
    }
    internal void ReleaseRicaPose()
    {
        RicaLinkActive=false;
        _ricaKeepPose = false;
        if (_ricaHeldName is null) return;
        _ricaHeldName = null;
        AttackClipVertical = 0;
        if (_alive && !_victory) RefreshBattlePortrait();
    }
    private bool HoldRicaLastFrame(bool restore)
    {
        if (!restore || _unitId != "rica" || !_ricaKeepPose || !DeathActive) return false;
        _ricaHeldName = _ricaClipName; _ricaHeldFrame = _attackClipFrame;
        return true;
    }

    internal static RicaAnchors[] RicaTrackOf(string key)
    {
        if (RicaTracks.TryGetValue(key, out var cached)) return cached;
        bool idle = key.StartsWith("rica_stage", StringComparison.Ordinal);
        string path = key.StartsWith("rica_approach_stage",StringComparison.Ordinal)
            ? $"res://assets/portraits/battle/approach/{key}/motion.json"
            : idle ? $"res://assets/portraits/battle/idle_loop/{key}/motion.json"
            : $"res://assets/portraits/battle/attack_clip/{key}/motion.json";
        if (!Godot.FileAccess.FileExists(path)) return RicaTracks[key] = Array.Empty<RicaAnchors>();
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
        var frames = json.RootElement.GetProperty("frames");
        var result = new RicaAnchors[frames.GetArrayLength()];
        int i = 0;
        foreach (var frame in frames.EnumerateArray())
        {
            Vector2 V(string name) => new(frame.GetProperty(name)[0].GetSingle(), frame.GetProperty(name)[1].GetSingle());
            result[i++] = new RicaAnchors(V("socket"), V("tip"), V("eye"), frame.GetProperty("shape").GetInt32(),
                !frame.TryGetProperty("eyesVisible",out var visible) || visible.GetBoolean(),
                frame.TryGetProperty("turn",out var turn) ? turn.GetSingle() : 0,
                frame.TryGetProperty("trailAlpha",out var alpha) ? alpha.GetSingle() : 1);
        }
        return RicaTracks[key] = result;
    }

    internal RicaAnchors CurrentRicaAnchors()
    {
        if(RicaApproachActive && RicaApproachOf(GraveStage) is {} approach)return approach.Anchors;
        string key = RicaClipName ?? RicaIdleLoopKey;
        var frames = RicaTrackOf(key);
        int index = AttackClipPlaying ? AttackClipFrame : _ricaHeldName is not null ? _ricaHeldFrame : Math.Max(0, IdleLoopFrame);
        if (frames.Length > 0) return frames[Math.Clamp(index, 0, frames.Length - 1)];
        // 静止した構えの基準点は、元絵のmanifestだけを正とする。
        if (RicaSourceIdles.Count == 0)
        {
            using var source = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(
                "res://assets/portraits/battle/motion_sources/rica/v1/manifest.json"));
            foreach (var pose in source.RootElement.GetProperty("poses").EnumerateArray())
            {
                if (pose.GetProperty("key").GetString() != "idle") continue;
                Vector2 V(string name) => new(pose.GetProperty(name)[0].GetSingle(),pose.GetProperty(name)[1].GetSingle());
                int stage=pose.GetProperty("stage").GetInt32();
                RicaSourceIdles[stage]=new(V("weaponSocket"),V("weaponTip"),V("eyeAnchor"),Math.Min(stage,2));
            }
        }
        return IdleLayoutOf(GraveStage)?.Anchors ?? RicaSourceIdles[GraveStage];
    }

    private string RicaIdleKey => $"rica_stage{GraveStage}";
    private string RicaIdleLoopKey => RicaIdleKey + (_ricaIdleB ? "_b" : "");
    internal void SetRicaIdleVariant(string? variant)
    {
        _ricaIdleB = variant == "B";
        if (_unitId == "rica" && _alive && !_victory && !AttackClipPlaying) RefreshBattlePortrait();
    }
    private void ApplyRicaIdlePortrait(ref string key, ref float height, ref float padding)
    {
        if (key != "rica" || GraveStage == 0 || !_alive || _movementPortrait is not null) return;
        key = RicaIdleKey;
        if(IdleLayoutOf(GraveStage) is{} layout)
        { padding=layout.Padding;height*=layout.Height;return; }
        // 透過素材ごとの身長と足元。動画も同じキャンバスの縮小で作る。
        padding = GraveStage switch { 1 => 172f/1536f, 2 => 15f/1536f, _ => 13f/1536f };
        height *= GraveStage switch { 1 => 1.02f, 2 => 1.13f, _ => 1.04f };
    }
}

// ビルボード原画の後ろへ身体の輪郭と武器を重ねる。原画・連番には発光を焼き込まない。
public partial class RicaMotionFx3D : Node3D
{
    private BattlePawn3D _pawn = null!;
    private Camera3D _camera = null!;
    private Func<(bool Paused, double Speed)> _clock = null!;
    private MeshInstance3D _body = null!, _weapon = null!;
    private ShaderMaterial _bodyMat = null!, _weaponMat = null!, _returnWeaponMat = null!;
    private RicaWeaponTrail3D _trail = null!;
    private RicaSpinTrail3D _spinTrail = null!;
    internal double Age { get; private set; }
    internal int WeaponShape { get; private set; }
    internal bool WeaponTrailVisible => _trail is not null && _trail.Visible;
    internal float SpinSweep => _spinTrail?.SweepRadians ?? 0;
    internal bool SpinBothSides => _spinTrail is not null && _spinTrail.FrontSegments>0 && _spinTrail.RearSegments>0;

    internal void Start(BattlePawn3D pawn, Camera3D camera, Func<(bool Paused, double Speed)> clock)
    {
        _pawn = pawn; _camera = camera; _clock = clock;
        Shader Spatial(string path)
        {
            bool beam = path.EndsWith("weapon_aura.gdshader", StringComparison.Ordinal);
            string code = Godot.FileAccess.GetFileAsString(path)
                .Replace("shader_type canvas_item;", "shader_type spatial;")
                .Replace("render_mode unshaded, blend_add;", beam
                    ? "render_mode unshaded,blend_add,cull_disabled,depth_draw_never;"
                    : "render_mode unshaded,blend_mix,cull_disabled,depth_draw_never;");
            // 同じ形状式を2D素材の確認画面と共有する。
            code = code.Replace("COLOR=", "vec4 result=");
            if (path.EndsWith("body_aura.gdshader", StringComparison.Ordinal))
                code = code.Replace("void fragment() {", "uniform bool flip=false;\nvoid fragment() {")
                    .Replace("float center=silhouette(p);", "if(flip) p.x=1.0-p.x;\nfloat center=silhouette(p);");
            int end = code.LastIndexOf('}');
            code = code.Insert(end, beam ? "ALBEDO=result.rgb; EMISSION=result.rgb*0.80; ALPHA=result.a;\n"
                : "ALBEDO=result.rgb; EMISSION=result.rgb*0.35; ALPHA=result.a;\n");
            return new Shader { Code = code };
        }
        _bodyMat = new ShaderMaterial { Shader = Spatial("res://assets/fx/rica_motion_v1/body_aura.gdshader") };
        _weaponMat = new ShaderMaterial { Shader = Spatial("res://assets/fx/rica_motion_v1/weapon_aura.gdshader") };
        _returnWeaponMat=new ShaderMaterial { Shader=new Shader { Code=_weaponMat.Shader!.Code
            .Replace("depth_draw_never;","depth_draw_never,depth_test_disabled;") } };
        MeshInstance3D Plane(ShaderMaterial material, string name)
        {
            var mesh = new MeshInstance3D { Name = name, Mesh = new QuadMesh { Size = Vector2.One },
                MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true };
            AddChild(mesh); return mesh;
        }
        _body = Plane(_bodyMat, "RicaBodyAura"); _weapon = Plane(_weaponMat, "RicaEnergyWeapon");
        _trail = new RicaWeaponTrail3D(); AddChild(_trail); _trail.Start();
        _spinTrail = new RicaSpinTrail3D(); AddChild(_spinTrail); _spinTrail.Start();
        ProcessPriority = 20; // 待機／攻撃の連番を更新した後の同じコマを読む。
        ProcessMode = ProcessModeEnum.Always; // 停止中もカメラ変更へ位置だけ追従する。
    }

    public override void _Process(double delta)
    {
        var (paused, speed) = _clock();
        paused |= GetTree().Paused;
        if (!paused) Age += delta * speed;
        bool live = _pawn.DeathActive && _pawn.IsVisibleInTree() && _pawn.GraveStage > 0;
        _body.Visible = _weapon.Visible = live;
        WeaponShape = 0;
        if (!live) { _trail.Hide(); _spinTrail.Clear(); return; }
        var sprite = _pawn.DeathSprite;
        var texture = sprite.Texture;
        var anchors = _pawn.CurrentRicaAnchors();
        var basis = _camera.GlobalBasis.Orthonormalized();
        var tint = BattlefieldView3D.GraveColor(_pawn.GraveLayers);
        float stage = _pawn.GraveStage;
        var size = new Vector2(texture.GetWidth(),texture.GetHeight()) * sprite.PixelSize;
        _body.GlobalTransform = new Transform3D(new Basis(basis.X * size.X * 1.18f,basis.Y * size.Y * 1.18f,basis.Z),
            sprite.GlobalPosition - basis.Z * .025f);
        _bodyMat.SetShaderParameter("portrait", texture);
        _bodyMat.SetShaderParameter("flip",sprite.FlipH);
        _bodyMat.SetShaderParameter("portrait_size", new Vector2(texture.GetWidth(),texture.GetHeight()));
        Vector2 eye = anchors.EyesVisible ? anchors.Eye : new Vector2(-5,-5);
        _bodyMat.SetShaderParameter("eye_anchor", eye);
        _bodyMat.SetShaderParameter("phase", (float)Age);
        _bodyMat.SetShaderParameter("strength", stage * .50f);
        _bodyMat.SetShaderParameter("tint", tint);
        _pawn.SetRicaEyeAnchor(eye);
        Vector3 Point(Vector2 uv) => _pawn.MovementPortraitPoint(uv * new Vector2(texture.GetWidth(),texture.GetHeight()),_camera);
        Vector3 socket = Point(anchors.Socket), tip = Point(anchors.Tip);
        Vector3 axis = (tip - socket).Normalized();
        if (axis.LengthSquared() < .5f) { _weapon.Hide(); return; }
        bool idle = _pawn.RicaClipName is null;
        float length = _pawn.RicaWeaponLength(anchors.Shape);
        float width = stage >= 3 ? idle ? 1.65f : anchors.Shape == 3 ? 1.05f : 1.55f
            : stage == 1 ? 1.0f : 1.25f;
        Vector3 up = basis.Z.Cross(axis).Normalized();
        _weapon.GlobalTransform = new Transform3D(new Basis(axis*length,up*width,basis.Z),
            socket + axis * (length * .42f) - basis.Z * .018f);
        WeaponShape = anchors.Shape;
        // 振り下ろしの発光する刃も、地面で途中を四角く切らず先端まで見せる。
        var weaponMat=_pawn.RicaClipName=="rica_return"?_returnWeaponMat:_weaponMat;
        if(_weapon.MaterialOverride!=weaponMat)_weapon.MaterialOverride=weaponMat;
        weaponMat.SetShaderParameter("shape",anchors.Shape);
        weaponMat.SetShaderParameter("tint",tint);
        weaponMat.SetShaderParameter("phase",(float)Age);
        weaponMat.SetShaderParameter("power",.80f + stage * .10f);
        weaponMat.SetShaderParameter("reveal",1.0f);
        bool spin = _pawn.RicaClipName is "rica_crescent" or "rica_sweep";
        _trail.Feed(Age,paused,_pawn.AttackClipPlaying&&!spin,_pawn.RicaClipName,
            socket+axis*length*.12f-basis.Z*.035f,socket+axis*length*.88f-basis.Z*.035f,tint,
            recovery:_pawn.RicaLinkActive);
        _spinTrail.Feed(Age,paused,_pawn.AttackClipPlaying,_pawn.RicaClipName,_pawn.RicaClipPosition,
            _pawn.CurrentRicaTurn(),Point(new Vector2(552f/1152f,480f/896f)),basis,sprite.FlipH,(int)stage,tint,_pawn.CurrentRicaTrailAlpha(),_pawn.RicaSpinStart);
    }
}
