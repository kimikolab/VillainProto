using BattleCore;
using Godot;
using System.Linq;

public partial class BattlePawn3D
{
    private MisaFeathers3D? _misaFeathers;
    internal MisaFeathers3D? MisaFeathers => _misaFeathers;

    private void AlignMisaPortrait(string key)
    {
        if (_unitId != "tome") return;
        // 待機絵は左へ流れる髪の余白が広い。両足の中点を席の中心へ合わせる。
        // Spriteの描画原点なので、敵側への反転とカメラの向きにも追従する。
        float offset = key == "tome" ? _sprite.Texture.GetWidth() * (148f / 1024f) : 0;
        _sprite.Offset = Vector2.Right * (_sprite.FlipH ? offset : -offset);
    }

    private void BuildMisaFeathers(DemoOpening opening)
    {
        if (_unitId != "tome" || opening.Traits?.Contains(TraitId.Feathers) == false) return;
        _ = UiKit.BattlePortrait(_atlas, "tome_attack");
        _misaFeathers = new MisaFeathers3D();
        AddChild(_misaFeathers);
        _misaFeathers.Configure(this, _portraitHeight);
    }
}
