using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;

// 小さくても輪郭で区別できるベクター図案。フォントの絵文字には依存しない。
public static class StatusIconArt
{
    private static readonly Dictionary<string, (string Color, string Shape)> Art = new()
    {
        [StatusKeys.Cowed] = ("#d9e9eb", "<circle cx='32' cy='23' r='7'/><path d='M20 47q0-17 12-17t12 17M12 18l-4 7 5 7m39-14 4 7-5 7' fill='none'/>"),
        // 第189期・萎縮（クビ）。次の一撃が半分——縮む矢印。
        [StatusKeys.Daunted] = ("#c7c9a8", "<path d='M14 14l12 12m24-12L38 26M14 50l12-12m24 12L38 38' fill='none'/><rect x='26' y='26' width='12' height='12' rx='2'/>"),
        [StatusKeys.Footing] = ("#e3b577", "<path d='M17 12h13v21l16 5v8H15V32z'/><path d='M10 53h44' fill='none'/>"),
        [StatusKeys.Poison] = ("#bba0ee", "<path d='M20 17h24v26H20z'/><circle cx='25' cy='29' r='5' fill='#111821'/><circle cx='39' cy='29' r='5' fill='#111821'/><path d='M24 43v7m8-7v7m8-7v7'/>"),
        [StatusKeys.Burn] = ("#ff975b", "<path d='M31 10Q45 25 39 30L46 23Q57 49 34 54Q10 54 17 32L24 20Q22 34 30 35Q38 29 31 10z'/>"),
        [StatusKeys.Stun] = ("#ffe079", "<path d='M35 9L16 36h15l-3 19 20-30H34z'/>"),
        [StatusKeys.Stagger] = ("#f2ba83", "<circle cx='43' cy='20' r='6'/><path d='M34 27L24 36l-11-2m13 1 9 10 15 1M32 30l14 4M11 53h42' fill='none'/>"),
        [StatusKeys.Confused] = ("#e7a0e8", "<path d='M32 34q-9-1-6-8t15-2q11 13-2 21T16 34Q11 13 33 12q17 1 19 18' fill='none'/>"),
        [StatusKeys.Wound] = ("#ff898d", "<path d='M25 13L13 47M39 13L27 50M51 18L40 50' fill='none'/>"),
        [StatusKeys.Marked] = ("#ffbd9f", "<circle cx='32' cy='32' r='15' fill='none'/><path d='M32 9v14m0 18v14M9 32h14m18 0h14' fill='none'/>"),
        [StatusKeys.Armor] = ("#9fdfef", "<path d='M20 10l13 9-9 21-12-15zM42 17l11 15-14 7-5-13zM30 42l15 4-13 10-10-9z'/>"),
        [StatusKeys.Deep] = ("#f86689", "<path d='M32 10l-9 16 9 4-12 24 23-25-10-5 9-14z'/>"),
        [StatusKeys.Curse] = ("#bd9aed", "<path d='M10 32Q32 8 54 32Q32 56 10 32z' fill='none'/><circle cx='32' cy='32' r='7'/>"),
        [StatusKeys.Ward] = ("#a1edcd", "<path d='M13 16h38v22L32 54 13 38z' fill='none'/><path d='M32 22v20m-10-10h20' fill='none'/>"),
        // 第179期・灰（拾い屋のスス）。**破片と同じ見せ方**（数字ではなく札1つ）。
        [StatusKeys.Ash] = ("#b9b2a6", "<path d='M12 50h40L32 18z' fill='none'/><path d='M24 50l8-14 8 14z'/><path d='M26 12v6m12-9v7m-6 2v5' fill='none'/>"),
        [StatusKeys.Debt] = ("#ccbdad", "<path d='M18 10h28v44l-7-4-7 4-7-4-7 4z' fill='none'/><path d='M25 26h14m-14 10h14' fill='none'/>"),
    };
    private static readonly Dictionary<string, Texture2D> Cache = new();
    public static IEnumerable<string> Keys => Art.Keys;
    public static string? KeyOf(string keyOrLabel)
        => Art.ContainsKey(keyOrLabel) ? keyOrLabel : Art.Keys.FirstOrDefault(k => StatusKeys.LabelOf(k) == keyOrLabel);

    public static Texture2D Texture(string key)
    {
        if (Cache.TryGetValue(key, out var texture)) return texture;
        var art = Art[key];
        string svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'><rect x='2' y='2' width='60' height='60' rx='11' fill='#111821' stroke='{art.Color}' stroke-width='2'/><g fill='{art.Color}' stroke='{art.Color}' stroke-width='3.5' stroke-linecap='round' stroke-linejoin='round'>{art.Shape}</g></svg>";
        var image = new Image();
        image.LoadSvgFromString(svg);
        texture = ImageTexture.CreateFromImage(image);
        Cache[key] = texture;
        return texture;
    }
}
