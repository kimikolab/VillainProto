using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// 攻撃の連番（単発）: res://assets/portraits/battle/attack_clip/<名前>/ の PNG と clip.json を 1 回だけ再生する。
// 連番は待機絵と別のキャンバスなので、clip.json の「腰の x・足底の y」と倍率で待機絵の同じ点へ重ねる
// （design/art/borg/attack-sweep-v1/export_clip.py が書く）。再生中は待機ループと動作差分を止め、終わったら通常の絵へ戻す。
// clip.json の forward（コマごとに体を前へ足す量）で、踏み込んだ位置を振り切りまで保ち、最後のコマを見せる間に元の位置へ戻す。
public partial class BattlePawn3D
{
    internal sealed record AttackClip(Texture2D[] Frames, double Fps, int Contact, Vector2 Anchor, Vector2 IdleAnchor, float Scale,
        float[] Forward, string? ReferencePortrait, float[] Vertical);

    private static readonly Dictionary<string, AttackClip?> AttackClipCache = new(StringComparer.Ordinal);
    private static bool _animationCacheHooked, _animationCacheClosing;

    private static void EnsureAnimationCacheShutdown()
    {
        if (_animationCacheHooked || Engine.GetMainLoop() is not SceneTree tree) return;
        _animationCacheHooked = true;
        tree.Root.TreeExiting += () => {
            // ImageTextureをCLRの終了処理まで残すと、RenderingServer破棄後に解放される。
            _animationCacheClosing = true;
            var textures = AttackClipCache.Values.Where(c => c is not null).SelectMany(c => c!.Frames)
                .Concat(IdleLoopCache.Values.SelectMany(f => f))
                .Concat(RicaApproaches.Values.Where(p=>p is not null).Select(p=>p!.Texture)).Distinct().ToArray();
            AttackClipCache.Clear();
            IdleLoopCache.Clear();
            RicaApproaches.Clear();
            foreach (var texture in textures)
                if (IsInstanceValid(texture)) texture.Dispose();
        };
    }

    private AttackClip? _attackClip;
    private double _attackClipTime;
    private double _attackClipHold;
    private int _attackClipFrame = -1;
    private Action? _attackClipContact;
    private TaskCompletionSource? _attackClipDone;

    internal const float PortraitGroundYForCheck = PortraitGroundY;
    internal bool AttackClipPlaying => _attackClip is not null;
    internal bool CanPlayAttackClip => _alive && !_victory;
    internal Task AttackClipFinished => _attackClipDone?.Task ?? Task.CompletedTask;
    internal float AttackClipForward { get; private set; }
    internal float AttackClipVertical { get; private set; }
    internal int AttackClipFrame => _attackClipFrame;

    internal static AttackClip? AttackClipOf(string name)
    {
        EnsureAnimationCacheShutdown();
        if (_animationCacheClosing) return null;
        if (AttackClipCache.TryGetValue(name, out AttackClip? cached)) return cached;
        string dir = $"res://assets/portraits/battle/attack_clip/{name}";
        AttackClip? clip = null;
        if (DirAccess.DirExistsAbsolute(dir) && Godot.FileAccess.FileExists($"{dir}/clip.json"))
        {
            using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString($"{dir}/clip.json"));
            var root = json.RootElement;
            Vector2 Vec(string key) => new(root.GetProperty(key)[0].GetSingle(), root.GetProperty(key)[1].GetSingle());
            var frames = DirAccess.GetFilesAt(dir)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(f => UiKit.LoadTexture($"{dir}/{f}", mipmaps: true))
                .ToArray();
            float[] forward = root.TryGetProperty("forward", out var fw)
                ? fw.EnumerateArray().Select(v => v.GetSingle()).ToArray() : new float[frames.Length];
            float[] vertical = root.TryGetProperty("vertical", out var vy)
                ? vy.EnumerateArray().Select(v => v.GetSingle()).ToArray() : new float[frames.Length];
            if (frames.Length > 0)
                clip = new AttackClip(frames, root.GetProperty("fps").GetDouble(), root.GetProperty("contact").GetInt32(),
                    Vec("anchor"), Vec("idleAnchor"), root.GetProperty("scale").GetSingle(), forward,
                    root.TryGetProperty("referencePortrait", out var reference) ? reference.GetString() : null, vertical);
        }
        AttackClipCache[name] = clip;
        return clip;
    }

    /// <summary>連番を 1 回再生する。<paramref name="onContact"/> は刃が当たるコマ（clip.json の contact）で 1 度だけ呼ぶ。
    /// 戻り値は最後のコマを <paramref name="hold"/> 秒見せ（その間に踏み込んだ位置から元の位置へ戻り）、通常の絵へ戻ったときに終わる。</summary>
    internal Task PlayAttackClip(string name, Action? onContact = null, double hold = 0.30)
    {
        AttackClip? clip = AttackClipOf(name);
        if (clip is null || !_alive || _victory)
        {
            onContact?.Invoke();
            return Task.CompletedTask;
        }
        FinishAttackClip(restore: false);
        ClearMovementPortrait();
        _idleLoopActive = false;
        _attackClip = clip;
        _attackClipTime = 0;
        _attackClipHold = hold;
        _attackClipFrame = -1;
        _attackClipContact = onContact;
        _attackClipDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EnsureLoopDriver();
        ApplyAttackClipFrame();
        return _attackClipDone.Task;
    }

    internal void CancelAttackClip() => FinishAttackClip(restore: true);

    internal void ProcessAttackClip(double delta)
    {
        if (_unitId == "rica" && _ricaMotionPaused) return;
        if (_attackClip is null) return;
        if (_victory || !_alive) { FinishAttackClip(restore: !_victory); return; }
        _attackClipTime += delta * Math.Max(0.1, AnimationSpeed);
        ApplyAttackClipFrame();
    }

    private void ApplyAttackClipFrame()
    {
        AttackClip clip = _attackClip!;
        int frame = (int)(_attackClipTime * clip.Fps);
        int last = clip.Frames.Length - 1;
        bool completed = false;
        float forward;
        if (frame > last)
        {
            double after = _attackClipTime - clip.Frames.Length / clip.Fps;
            completed = after >= _attackClipHold;
            frame = last;
            // 振り切った姿勢のまま、踏み込んだ位置から元の位置へ戻る。
            float t = Math.Clamp((float)(after / Math.Max(0.001, _attackClipHold)),0,1);
            forward = Forward(clip, last) * (1 - t * t * (3 - 2 * t));
        }
        else forward = Forward(clip, frame);
        AttackClipForward = forward;
        _attackClipFrame = frame;
        if (frame >= clip.Contact && _attackClipContact is { } contact)
        {
            _attackClipContact = null;
            contact();
        }
        // 毎フレーム当て直す（再生中に RefreshBattlePortrait が呼ばれても連番へ戻す）。
        Texture2D idle = UiKit.BattlePortrait(_atlas, clip.ReferencePortrait ?? _unitId);
        float idlePixel = _portraitHeight / Math.Max(1, idle.GetHeight());
        float clipPixel = idlePixel * clip.Scale;
        Texture2D texture = clip.Frames[frame];
        float side = _sprite.FlipH ? -1 : 1;
        float hipX = side * (clip.IdleAnchor.X - idle.GetWidth() * 0.5f) * idlePixel;
        _portraitOffsetX = hipX - side * (clip.Anchor.X - texture.GetWidth() * 0.5f - forward) * clipPixel;
        // 地面（足底）から絵の中心までの高さ。足底は中心より下にあるので「足底の y − 中心の y」。
        _portraitGroundDistance = (clip.Anchor.Y - texture.GetHeight() * 0.5f) * clipPixel;
        _portraitBaseY = PortraitGroundY + _portraitGroundDistance;
        // 開始したフレームから足元を合わせる。次の親_Processを待つと一瞬だけ待機絵の中心位置が残る。
        _sprite.Position = new Vector3(_portraitOffsetX, _portraitBaseY, 0);
        AttackClipVertical = frame < clip.Vertical.Length ? clip.Vertical[frame] : 0;
        _sprite.Position += Vector3.Up * AttackClipVertical;
        if (_sprite.Texture != texture)
        {
            _sprite.Texture = texture;
            _portraitMaterial.SetShaderParameter("portrait_texture", texture);
        }
        _sprite.PixelSize = clipPixel;
        // 倍速や描画負荷で最終コマを飛び越しても、保持する絵・武器・高さを最後の姿勢へ揃える。
        if (completed) FinishAttackClip(restore: true, completed: true);
    }

    private static float Forward(AttackClip clip, int frame) => frame < clip.Forward.Length ? clip.Forward[frame] : 0;

    private void FinishAttackClip(bool restore, bool completed = false)
    {
        if (_attackClip is null) return;
        TaskCompletionSource? done = _attackClipDone;
        Action? contact = _attackClipContact;
        bool heldRica = HoldRicaLastFrame(restore && completed);
        _attackClip = null;
        _attackClipFrame = -1;
        AttackClipForward = 0;
        if (!heldRica) AttackClipVertical = 0;
        _attackClipContact = null;
        _attackClipDone = null;
        contact?.Invoke(); // 当たる前に途切れても、待っている側を止めない
        if (restore && !heldRica) RefreshBattlePortrait(); // 連撃のリィカは振り切りを次の一打まで保持する。
        done?.TrySetResult();
    }
}
