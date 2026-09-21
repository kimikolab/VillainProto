using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;

// 小さくても輪郭で区別できるベクター図案。フォントの絵文字には依存しない。
public static class StatusIconArt
{
    private static readonly Dictionary<string, (string Color, string Shape)> Art = new()
    {
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
