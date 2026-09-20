using Godot;
using System;
using System.Collections.Generic;

// =====================================================================================
// 関係の線の描画（第172期 部B）—— **第171期に `SeatDiagram` の中にあった `_Draw` を、
// 席の四角を外から渡す形に切り出しただけ。** 判定も元データも1つも持たない。
//
// 切り出した理由は1つ——ポンの要望「**編成UIにこれは欲しい**」（第171期 問い1）。
// マップの図（`SeatDiagram`・札は自前の座標）と編成画面（`BattlefieldView`・席は画面の広さで動く）は
// **席の四角の出どころが違うだけ**で、線の引き方は同じである。
// **写しを作らない**（自己検査 (e)）ので、両方がこの1本を呼ぶ。
// =====================================================================================

public static class SeatLinks
{
    /// <summary>線に添える語は日本語なので、<b>フォールバックではなくシステムフォント</b>で描く。</summary>
    public static readonly SystemFont LabelFont = new()
    {
        FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" },
        AllowSystemFallback = true,
    };

    /// <summary>
    /// 関係の線を1層ぶん描く。
    /// <paramref name="words"/> が偽なら線と矢の先だけ、真なら選んだ札に関わる線の語だけ。
    /// </summary>
    /// <param name="canvas">描き先（<c>_Draw</c> の中から呼ぶこと）。</param>
    /// <param name="links">引く線（<see cref="Map11Relations.Of"/> の戻り）。</param>
    /// <param name="selected">選んでいる席（-1 ＝ なし）。選んでいる間は関係ない線を消す。</param>
    /// <param name="accent">陣営の色。</param>
    /// <param name="rectOf">席番号 → その札の四角。<b>ここだけが呼び出し側で違う。</b></param>
    /// <param name="fontSize">語の大きさ。</param>
    public static void Draw(CanvasItem canvas, IReadOnlyList<Map11Relations.Link> links,
                            int selected, Color accent, Func<int, Rect2> rectOf, bool words,
                            int fontSize = 10)
    {
        // 同じ2席のあいだに何本も走るので、語が重ならないよう線上で位置をずらす。
        var lane = new Dictionary<(int, int), int>();
        foreach (Map11Relations.Link l in links)
        {
            bool hot = selected >= 0 && (l.From == selected || l.To == selected);
            if (selected >= 0 && !hot) continue;   // 選んでいる間は関係ない線を消す
            var key = (Math.Min(l.From, l.To), Math.Max(l.From, l.To));
            int n = lane.GetValueOrDefault(key);
            lane[key] = n + 1;

            (Vector2 a, Vector2 b) = Clip(rectOf(l.From), rectOf(l.To));
            Color c = hot ? accent : new Color(accent, 0.20f);
            if (!words)
            {
                canvas.DrawLine(a, b, c, hot ? 2.4f : 1.2f, true);
                canvas.DrawCircle(b, hot ? 4.5f : 2.5f, c);
                continue;
            }
            if (!hot) continue;
            // 0 本目は中央、以降は手前・奥へ交互に寄せる。
            float t = 0.5f + (n % 2 == 0 ? 1 : -1) * ((n + 1) / 2) * 0.24f;
            Vector2 mid = a.Lerp(b, Math.Clamp(t, 0.12f, 0.88f));
            Vector2 size = LabelFont.GetStringSize(l.Word, HorizontalAlignment.Left, -1, fontSize);
            // **札の隙間に入らない語は出さない。** 同じ列の2枚（前1↔前3・後1↔後3）は
            // 隙間が数十 px しかないので、無理に置くと札の上に乗って名前を隠す
            // ——関係そのものは下の欄に文で出ているので、線と矢印だけ残す。
            if (size.X + 10 > a.DistanceTo(b)) continue;
            Vector2 at = mid - new Vector2(size.X / 2f, -4);
            canvas.DrawRect(new Rect2(at + new Vector2(-3, -fontSize - 2), size + new Vector2(6, 5)),
                            new Color(0.02f, 0.05f, 0.04f, 0.90f));
            canvas.DrawString(LabelFont, at, l.Word, HorizontalAlignment.Left, -1, fontSize, UiKit.Gold);
        }
    }

    /// <summary>線の両端を、札の四角の外へ切り詰める（札の上に線を重ねない）。</summary>
    private static (Vector2, Vector2) Clip(Rect2 from, Rect2 to)
    {
        Vector2 a = from.GetCenter(), b = to.GetCenter();
        return (Exit(from, a, b), Exit(to, b, a));
    }

    /// <summary>中心 <paramref name="c"/> から <paramref name="toward"/> へ向かう線が矩形を出る点。</summary>
    private static Vector2 Exit(Rect2 rect, Vector2 c, Vector2 toward)
    {
        Vector2 d = toward - c;
        if (d.LengthSquared() < 0.001f) return c;
        float hx = rect.Size.X / 2f + 3f, hy = rect.Size.Y / 2f + 3f;
        float sx = Math.Abs(d.X) < 0.001f ? float.MaxValue : hx / Math.Abs(d.X);
        float sy = Math.Abs(d.Y) < 0.001f ? float.MaxValue : hy / Math.Abs(d.Y);
        return c + d * Math.Min(sx, sy);
    }
}
