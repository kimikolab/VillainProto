using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class BattlePawn3D
{
    internal sealed record HagiAnchors(Vector2 Socket, Vector2 Tip, Vector2[] Contour, bool Trail);
    private static readonly Dictionary<string, HagiAnchors[]> HagiTracks = new(StringComparer.Ordinal);
    private string? _hagiClipName, _hagiHeldName;
    private int _hagiHeldFrame;
    private bool _hagiKeepPose;
    internal string? HagiClipName => _unitId == "hagi" ? AttackClipPlaying ? _hagiClipName : _hagiHeldName : null;
    internal HagiAnchors? CurrentHagiAnchors
    {
        get
        {
            if (HagiClipName is not { } name) return null;
            if (!HagiTracks.TryGetValue(name, out var track))
            {
                using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(
                    $"res://assets/portraits/battle/attack_clip/{name}/motion.json"));
                Vector2 V(JsonElement p) => new(p[0].GetSingle(), p[1].GetSingle());
                track = doc.RootElement.GetProperty("frames").EnumerateArray().Select(f => {
                    var g = f.GetProperty("geometry");
                    return new HagiAnchors(V(g.GetProperty("socket")), V(g.GetProperty("tip")),
                        g.GetProperty("contour").EnumerateArray().Select(V).ToArray(), f.GetProperty("trail").GetBoolean());
                }).ToArray();
                HagiTracks[name] = track;
            }
            return track[Math.Clamp(AttackClipPlaying ? AttackClipFrame : _hagiHeldFrame, 0, track.Length - 1)];
        }
    }
    internal Task PlayHagiClip(string name, Action contact, bool keepPose, bool quickReturn = false)
    {
        _hagiHeldName = null; _hagiKeepPose = keepPose; _hagiClipName = name;
        var done = PlayAttackClip(name, contact, keepPose ? 1.0 / 30 : .04);
        if (quickReturn && _attackClip is { } clip)
        {
            // 返し刃は既存の帰りのセルを使い、接触2コマ前から。長い溜めを挟まない。
            _attackClip = clip with { Fps = clip.Fps * 1.5 };
            _attackClipTime = Math.Max(0, clip.Contact - 2) / _attackClip.Fps;
            ApplyAttackClipFrame();
        }
        return done;
    }
    private bool HoldHagiLastFrame(bool completed)
    {
        if (!completed || _unitId != "hagi" || !_hagiKeepPose || !HagiActive) return false;
        _hagiHeldName = _hagiClipName; _hagiHeldFrame = _attackClipFrame;
        return true;
    }
    internal void ReleaseHagiPose()
    {
        _hagiKeepPose = false; _hagiHeldName = null; AttackClipVertical = 0;
        if (_unitId == "hagi" && HagiActive) RefreshBattlePortrait();
    }
    internal void CancelHagiMotion()
    {
        if (_unitId != "hagi") return;
        _hagiKeepPose = false; _hagiHeldName = null; _hagiClipName = null;
        CancelAttackClip();
    }
}
