using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 待機ループ: ComfyUI（VillainProto/08_戦闘待機ループ）で作った透過PNGの連番を、通常の待機絵の代わりに回す。
// 連番は res://assets/portraits/battle/idle_loop/<駒ID>/ に置くだけで使われる（.cs に駒を書かない）。
// 元の待機絵と同じキャンバスを縮小した連番なので、足元の余白比はそのまま使える。
// 動作差分・勝利・死亡のあいだは止め、通常の待機絵へ戻ったところで再開する。
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

    internal bool IdleLoopPlaying => _idleLoopActive;
    internal int IdleLoopFrame => _idleLoopFrame;
    internal int IdleLoopFrameCount => _idleLoopFrames.Length;

    internal static Texture2D[] IdleLoopFramesOf(string unitId)
    {
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

    // RefreshBattlePortrait の最後に呼ぶ。通常の待機絵のときだけ連番へ差し替える。
    private void UpdateIdleLoop(string key, float height)
    {
        if (_idleLoopFrames.Length == 0) _idleLoopFrames = IdleLoopFramesOf(_unitId);
        bool active = key == _unitId && _alive && !_victory && _idleLoopFrames.Length > 0;
        _idleLoopActive = active;
        _idleLoopFrame = -1;
        if (!active) return;
        _idleLoopHeight = height;
        if (_idleLoopDriver is null)
        {
            // 駒ごとに位相をずらし、同じ駒が並んでも揃って弾まないようにする。
            _idleLoopTime = _phase / Mathf.Tau * (_idleLoopFrames.Length / IdleLoopFps);
            _idleLoopDriver = new BattleIdleLoopDriver { Pawn = this, Name = "IdleLoopDriver" };
            AddChild(_idleLoopDriver);
        }
        ApplyIdleLoopFrame();
    }

    internal void ProcessIdleLoop(double delta)
    {
        if (!_idleLoopActive) return;
        // 勝利絵・倒れた駒は連番で上書きしない（勝利・死亡の演出は RefreshBattlePortrait を通らない）。
        if (_victory || !_alive) { _idleLoopActive = false; return; }
        _idleLoopTime += delta * Math.Max(0.1, AnimationSpeed);
        ApplyIdleLoopFrame();
    }

    private void ApplyIdleLoopFrame()
    {
        int count = _idleLoopFrames.Length;
        int frame = (int)(_idleLoopTime * IdleLoopFps) % count;
        if (frame < 0) frame += count;
        if (frame == _idleLoopFrame) return;
        _idleLoopFrame = frame;
        Texture2D texture = _idleLoopFrames[frame];
        _sprite.Texture = texture;
        _sprite.PixelSize = _idleLoopHeight / Math.Max(1, texture.GetHeight());
        _portraitMaterial.SetShaderParameter("portrait_texture", texture);
    }
}

// BattlePawn3D の _Process を増やさずに待機ループを毎フレーム進めるための小さな子ノード。
public partial class BattleIdleLoopDriver : Node
{
    public BattlePawn3D? Pawn;
    public override void _Process(double delta) => Pawn?.ProcessIdleLoop(delta);
}
