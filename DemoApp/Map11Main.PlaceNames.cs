using System.Collections.Generic;

public partial class Map11Main
{
    // 表示名だけの差し替え口。ポンが地名を決めたら右側だけ替える。
    // 盤の ID・経路・命令・保存データは元の MapGraph のまま。
    private static readonly Dictionary<(string Stage, string Cell), string> PlaceNames = new()
    {
        [("1-3", "北の道 1 マス目")] = "北の坂",
        [("1-3", "北の道 2 マス目")] = "見張りの丘",
        [("1-3", "北の道 4 マス目")] = "砦裏の道",
        [("1-3", "中の道 1 マス目")] = "浅瀬の入口",
        [("1-3", "中の道 2 マス目")] = "石畳の辻",
        [("1-3", "中の道 4 マス目")] = "本陣前の街道",
        [("1-3", "中の道 5 マス目")] = "門前広場",
        [("1-3", "南の道 1 マス目")] = "南の小道",
        [("1-3", "南の道 2 マス目")] = "古い街道",
        [("1-3", "南の道 4 マス目")] = "関所裏の道",
    };
    private static string PlaceName(int cell)
    {
        string original = St.Graph[cell].Name;
        return PlaceNames.GetValueOrDefault((St.Stage.Id, original), original);
    }
}
