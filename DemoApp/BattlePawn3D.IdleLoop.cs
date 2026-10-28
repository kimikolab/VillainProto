using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 待機ループ: ComfyUI（VillainProto/08_戦闘待機ループ）で作った透過PNGの連番を、通常の待機絵の代わりに回す。
// 連番は res://assets/portraits/battle/idle_loop/<駒ID>/ に置くだけで使われる（.cs に駒を書かない）。
// 元の待機絵と同じキャンバスを縮小した連番なので、足元の余白比はそのまま使える。
// 通常の動作差分・勝利・死亡では止める。燃焼差分には専用の待機ループを使う。
// エレの歌唱（ere_singing）は同じ読み込みを使うが、巡回せず1フレーズの終端を保つ。
public partial class BattlePawn3D
{
    private const double IdleLoopFps = 16.0;
    private static readonly Dictionary<string, Texture2D[]> IdleLoopCache = new(StringComparer.Ordinal);

    private Texture2D[] _idleLoopFrames = Array.Empty<Texture2D>();
    private bool _idleLoopActive;
    private double _idleLoopTime;
    private int _idleLoopFrame = -1;
    private float _idleLoopHeight;
    private BattleIdleLoopDriver? _idleLoopDriver;
    private bool _idleLoopWarmed;
    private Texture2D[]? _ereIdlePreview, _ereSongPreview;

    // 比較シーン専用。候補の採用前に、同じ本番の再生経路で切り替えを確かめる。
    internal void PreviewEreMotion(Texture2D[] idle, Texture2D[] song)
    {
        if (_unitId != "ere") throw new InvalidOperationException("エレ以外に哀歌の候補を設定できません");
        _ereIdlePreview = idle; _ereSongPreview = song;
        RefreshBattlePortrait();
    }

    internal bool IdleLoopPlaying => _idleLoopActive;
    internal int IdleLoopFrame => _idleLoopFrame;
    internal int IdleLoopFrameCount => _idleLoopFrames.Length;
    internal double DirgeSongSeconds => DirgeSinging && _idleLoopActive
        ? Math.Clamp(_idleLoopFrames.Length / IdleLoopFps, .80, 3.0) : .80;

    internal static Texture2D[] IdleLoopFramesOf(string unitId)
    {
        EnsureAnimationCacheShutdown();
        if (_animationCacheClosing) return Array.Empty<Texture2D>();
        if (IdleLoopCache.TryGetValue(unitId, out Texture2D[]? cached)) return cached;
        string dir = $"res://assets/portraits/battle/idle_loop/{unitId}";
        Texture2D[] frames = Array.Empty<Texture2D>();
        if (DirAccess.DirExistsAbsolute(dir))
        {
            frames = DirAccess.GetFilesAt(dir)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(f => UiKit.LoadTexture($"{dir}/{f}", mipmaps: true))
                .ToArray();
        }
        IdleLoopCache[unitId] = frames;
        return frames;
    }

    // RefreshBattlePortrait の最後に呼ぶ。待機、またはエレの演奏中に対応する連番へ差し替える。
    private void UpdateIdleLoop(string key, float height)
    {
        // 燃焼の差分絵を持つ駒（ホタ）は、燃えているあいだ差分の連番（idle_loop/<駒ID>_burning/）を回す。
        // 差分の連番が無ければ、差分の静止絵をそのまま出す（連番を空にして止める）。
        bool hasBurningVariant = UiKit.HasCustomBattlePortrait(_unitId + "_burning");
        // 戦闘の途中で初めて燃えたときに PNG の読み込みで止まらないよう、開戦時に差分の連番も読んでおく。
        if (!_idleLoopWarmed && hasBurningVariant) _ = IdleLoopFramesOf(_unitId + "_burning");
        if (!_idleLoopWarmed && _unitId == "ere") _ = IdleLoopFramesOf("ere_singing");
        _idleLoopWarmed = true;
        bool burningVariant = _burning && hasBurningVariant;
        bool ricaStage = _unitId == "rica" && GraveStage > 0 && key == RicaIdleKey;
        bool ereSong = _unitId == "ere" && key == "ere_singing" && DirgeSinging;
        _idleLoopFrames = _unitId == "ere" ? (ereSong ? _ereSongPreview : _ereIdlePreview)
            ?? IdleLoopFramesOf(ereSong ? "ere_singing" : "ere")
            : IdleLoopFramesOf(ricaStage ? RicaIdleLoopKey : burningVariant ? _unitId + "_burning" : _unitId);
        // 攻撃の連番（BattlePawn3D.AttackClip.cs）の再生中は待機ループを止める。
        bool active = (key == _unitId || ricaStage || ereSong) && _alive && !_victory && _idleLoopFrames.Length > 0 && _attackClip is null;
        _idleLoopActive = active;
        _idleLoopFrame = -1;
        if (!active) return;
        _idleLoopHeight = height;
        if (_idleLoopDriver is null)
        {
            // 駒ごとに位相をずらし、同じ駒が並んでも揃って弾まないようにする。
            _idleLoopTime = _phase / Mathf.Tau * (_idleLoopFrames.Length / IdleLoopFps);
            EnsureLoopDriver();
        }
        ApplyIdleLoopFrame();
    }

    private void EnsureLoopDriver()
    {
        if (_idleLoopDriver is not null) return;
        _idleLoopDriver = new BattleIdleLoopDriver { Pawn = this, Name = "IdleLoopDriver" };
        AddChild(_idleLoopDriver);
    }

    internal void ProcessIdleLoop(double delta)
    {
        if (_unitId == "rica" && _ricaMotionPaused) return;
        if (_unitId == "hagi" && HagiPaused) return;
        if (_unitId == "utsu" && UtsuPaused) return;
        if (_unitId == "mudo" && MudoIdlePaused) return;
        if (!_idleLoopActive) return;
        // 勝利絵・倒れた駒は連番で上書きしない（勝利・死亡の演出は RefreshBattlePortrait を通らない）。
        if (_victory || !_alive) { _idleLoopActive = false; return; }
        _idleLoopTime += delta * Math.Max(0.1, AnimationSpeed);
        ApplyIdleLoopFrame();
    }

    private void ApplyIdleLoopFrame()
    {
        int count = _idleLoopFrames.Length;
        int frame = (int)(_idleLoopTime * IdleLoopFps);
        // 待機は巡回、哀歌の演奏は1回のフレーズ。終端は歌唱終了まで保つ。
        frame = _unitId == "ere" && DirgeSinging ? Math.Min(frame, count - 1) : frame % count;
        if (frame < 0) frame += count;
        if (frame == _idleLoopFrame) return;
        _idleLoopFrame = frame;
        Texture2D texture = _idleLoopFrames[frame];
        _sprite.Texture = texture;
        _sprite.PixelSize = _idleLoopHeight / Math.Max(1, texture.GetHeight());
        _portraitMaterial.SetShaderParameter("portrait_texture", texture);
    }
}

// BattlePawn3D の _Process を増やさずに待機ループと攻撃の連番を毎フレーム進めるための小さな子ノード。
public partial class BattleIdleLoopDriver : Node
{
    public BattlePawn3D? Pawn;
    public override void _Process(double delta)
    {
        Pawn?.ProcessIdleLoop(delta);
        Pawn?.ProcessAttackClip(delta);
    }
}
