using Godot;
using System;
using System.Linq;
using System.Text.Json;

public partial class BattlePawn3D
{
    private static Vector2[][]? _hagiIdleBladeTracks;
    private static Vector2[]? _hagiStillBladeTrack;
    private Vector2[]? HagiIdleBladeContour
    {
        get
        {
            if (_movementPortrait is not null) return null;
            if (_hagiIdleBladeTracks is null)
            {
                using var doc=JsonDocument.Parse(Godot.FileAccess.GetFileAsString(
                    "res://assets/portraits/battle/idle_loop/hagi/weapon.json"));
                Vector2[] Points(JsonElement points)=>points.EnumerateArray()
                    .Select(p=>new Vector2(p[0].GetSingle(),p[1].GetSingle())).ToArray();
                _hagiIdleBladeTracks=doc.RootElement.GetProperty("frames").EnumerateArray()
                    .Select(f=>Points(f.GetProperty("contour"))).ToArray();
                _hagiStillBladeTrack=Points(doc.RootElement.GetProperty("still"));
            }
            return IdleLoopPlaying ? _hagiIdleBladeTracks[Math.Clamp(IdleLoopFrame,0,_hagiIdleBladeTracks.Length-1)]
                : _hagiStillBladeTrack;
        }
    }
    // 立ち絵の実頂点と同じSprite3D変換。カメラの上方向で再構築すると斜めの戦場で浮く。
    internal Vector3 HagiBladePoint(Vector2 uv)
    {
        var size=_sprite.Texture.GetSize();
        var point=new Vector3((uv.X-.5f)*size.X*_sprite.PixelSize*(_sprite.FlipH?-1:1),
            (.5f-uv.Y)*size.Y*_sprite.PixelSize,0);
        return _sprite.ToGlobal(point);
    }
}
