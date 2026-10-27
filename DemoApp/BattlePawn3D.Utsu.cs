using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class BattlePawn3D
{
    private sealed record UtsuTrack(Vector2[] Roots, Vector2[] Tips, int Active, bool Trail, string Pose, float? Advance);
    private static readonly Dictionary<string, UtsuTrack[]> UtsuTracks = new(StringComparer.Ordinal);
    private string? _utsuClipName, _utsuHeldName;
    private int _utsuHeldFrame;
    private bool _utsuKeepPose;
    private Action<float>? _utsuAdvance;
    internal bool UtsuPaused { get; set; }
    internal string? UtsuClipName => _unitId == "utsu" ? AttackClipPlaying ? _utsuClipName : _utsuHeldName : null;
    internal bool UtsuTrailActive => AttackClipPlaying && UtsuCurrentTrack()?.Trail == true;
    internal string? UtsuPose => UtsuCurrentTrack()?.Pose;
    internal int UtsuActiveBlade => UtsuCurrentTrack()?.Active ?? 0;
    internal bool UtsuBladeActive(int blade) => UtsuActiveBlade < 0 || UtsuActiveBlade == blade;

    private UtsuTrack? UtsuCurrentTrack()
    {
        if (UtsuClipName is not { } name) return null;
        if (!UtsuTracks.TryGetValue(name, out var tracks))
        {
            using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString($"res://assets/portraits/battle/attack_clip/{name}/motion.json"));
            Vector2 V(JsonElement p) => new(p[0].GetSingle(), p[1].GetSingle());
            int active = doc.RootElement.GetProperty("activeBlade").GetInt32();
            tracks = doc.RootElement.GetProperty("frames").EnumerateArray().Select(frame => {
                var blades = frame.GetProperty("geometry").GetProperty("blades").EnumerateArray().ToArray();
                return new UtsuTrack(blades.Select(b => V(b.GetProperty("root"))).ToArray(),
                    blades.Select(b => V(b.GetProperty("tip"))).ToArray(), active,
                    frame.GetProperty("trail").GetBoolean(), frame.GetProperty("pose").GetString()!,
                    frame.TryGetProperty("advance", out var advance) && advance.ValueKind == JsonValueKind.Number ? advance.GetSingle() : null);
            }).ToArray();
            UtsuTracks[name] = tracks;
        }
        return tracks[Math.Clamp(AttackClipPlaying ? AttackClipFrame : _utsuHeldFrame, 0, tracks.Length - 1)];
    }

    internal (Vector3 Root, Vector3 Tip) UtsuBladeSegment(Camera3D camera, int blade)
    {
        var track = UtsuCurrentTrack();
        if (track is null) return (FxPoint, FxPoint);
        var size = _sprite.Texture.GetSize();
        return (MovementPortraitPoint(track.Roots[blade] * size, camera), MovementPortraitPoint(track.Tips[blade] * size, camera));
    }

    internal Task PlayUtsuClip(string name, Action contact, bool keep, Action<float>? advance = null)
    {
        _utsuHeldName = null; _utsuKeepPose = keep; _utsuClipName = name; _utsuAdvance = advance;
        return PlayAttackClip(name, contact, hold: keep ? 1.0 / 30 : 0.04);
    }

    // 原画の時計から前進量を読む。踏着セルで1に達し、構え直し中は足元を滑らせない。
    private void ProcessUtsuAdvance()
    {
        if (_unitId != "utsu" || _utsuAdvance is null || !AttackClipPlaying || UtsuCurrentTrack()?.Advance is not float current) return;
        var tracks = UtsuTracks[_utsuClipName!];
        int frame = Math.Clamp(AttackClipFrame, 0, tracks.Length - 1);
        float next = tracks[Math.Min(frame + 1, tracks.Length - 1)].Advance ?? current;
        float fraction = Math.Clamp((float)(_attackClipTime * _attackClip!.Fps - frame), 0, 1);
        _utsuAdvance(Mathf.Lerp(current, next, fraction));
    }

    private bool HoldUtsuLastFrame(bool completed)
    {
        if (!completed || _unitId != "utsu" || !_utsuKeepPose || !CanPlayAttackClip) return false;
        _utsuHeldName = _utsuClipName; _utsuHeldFrame = _attackClipFrame;
        return true;
    }

    private bool ApplyUtsuHeldPortrait()
    {
        if (_unitId != "utsu" || !_alive || _victory || _utsuHeldName is not { } name || AttackClipPlaying) return false;
        var clip = AttackClipOf(name)!; var texture = clip.Frames[_utsuHeldFrame];
        _sprite.Texture = texture; _portraitMaterial.SetShaderParameter("portrait_texture", texture);
        _idleLoopActive = false;
        return true;
    }

    internal void ReleaseUtsuPose()
    {
        _utsuKeepPose = false; _utsuHeldName = null; _utsuAdvance = null; AttackClipVertical = 0;
        if (_unitId == "utsu" && CanPlayAttackClip) RefreshBattlePortrait();
    }

    internal void CancelUtsuMotion()
    {
        if (_unitId != "utsu") return;
        _utsuKeepPose = false; _utsuHeldName = null; _utsuClipName = null; _utsuAdvance = null;
        CancelAttackClip(); AttackClipVertical = 0;
    }
}
