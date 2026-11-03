using Godot;
using System.Collections.Generic;

public partial class Map11Main
{
    private readonly Dictionary<int, Sprite3D> _outpostArt = new();
    private readonly Dictionary<string, Texture2D> _terrainTextures = new();

    private Texture2D OutpostTexture(int cell, bool mine)
    {
        int lane = St.Graph[cell].Lane;
        string kind = lane == 0 ? "fort" : lane == St.Stage.LaneCount - 1 ? "gate" : "camp";
        string key = kind + (mine ? "_ally" : "_enemy");
        if (_terrainTextures.TryGetValue(key, out var cached)) return cached;
        // 生PNGから読むので、新しく配置した素材もエディタの再インポート待ちにならない。
        using Image source = Image.LoadFromFile(ProjectSettings.GlobalizePath($"res://assets/map/terrain/{key}.png"));
        source.Resize(384, 256, Image.Interpolation.Lanczos);
        source.GenerateMipmaps();
        return _terrainTextures[key] = ImageTexture.CreateFromImage(source);
    }

    private void AddOutpostArt(int cell)
    {
        var art = new Sprite3D
        {
            Texture = OutpostTexture(cell, false), PixelSize = .023f,
            Position = PosOf(cell) + new Vector3(0, 1.7f, -.5f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Shaded = false, AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
        };
        AddChild(art);
        _outpostArt[cell] = art;
    }
}
