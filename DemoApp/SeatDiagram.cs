using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 配置の図（第171期 §1-1）—— **5 席を盤面と同じ並びで描くだけ。判定は1つも持たない。**
//
// 第170期の観察ログ:
//   「カド隊の他のメンツが**どう配置されているか**が隊の中身を良く見ないと分からない。
//    しかも**何でカドが後衛って戦闘始まってから気づいた**」（問い2）
//   「隊の中身、**文章が長すぎて毎回読んでいられない**」（問い5）
//
// **足りないのは量ではなく形**なので、説明文はここに載せない——札を押したときだけ、
// 下の欄に「何をする」「どの道で」を出す（`Map11Main` 側）。
//
// 席の並びは `FormationRules` の X 字そのもの（レーン0 ＝ 前1→中央→後1／
// レーン1 ＝ 前3→中央→後3）。**席の名前も隣接も写しを持たず `FormationRules` から引く。**
// =====================================================================================

public partial class SeatDiagram : Control
{
    /// <summary>1 席ぶんの中身。<c>Def</c> が null の席は<b>空席として描く</b>（先遣の後1 が見える）。</summary>
    public readonly record struct Cell(int Slot, UnitDef? Def, int Hp, int MaxHp, bool Alive);

    private const float CardW = 124f;
    private const float CardH = 64f;
    private const float GapX = 40f;
    private const float GapY = 26f;

    /// <summary>図の並び。<b>盤面と同じ X 字</b>（前が上、後ろが下、中央が真ん中）。</summary>
    private static readonly (int Slot, int Col, int Row)[] Layout =
    {
        (0, 0, 0),   // 前1
        (1, 2, 0),   // 前3
        (2, 1, 1),   // 中央
        (3, 0, 2),   // 後1
        (4, 2, 2),   // 後3
    };

    private static readonly float ColStep = (CardW + GapX) / 2f;
    private static readonly float RowStep = CardH + GapY;

    public static readonly Vector2 Size2 = new(CardW + ColStep * 2, CardH + RowStep * 2);

    /// <summary>札を押したときに呼ばれる（席番号）。同じ札をもう一度押すと -1 が来る。</summary>
    public Action<int>? SlotSelected;

    /// <summary>いま選んでいる席（-1 ＝ なし）。</summary>
    public int Selected { get; private set; } = -1;

    private readonly Dictionary<int, Button> _cards = new();
    /// <summary>札の下端の HP バー（指示書 §1-1）。<b>数字とは別に、長さで残りが分かるように。</b></summary>
    private readonly Dictionary<int, ColorRect> _bars = new();
    /// <summary>同・下敷き。<b>空席では下敷きごと消す</b>（残していると「HP 0 の駒」に見える）。</summary>
    private readonly Dictionary<int, ColorRect> _barBacks = new();
    private readonly Dictionary<int, Cell> _cells = new();
    private LinkLayer _lines = null!;
    private LinkLayer _words = null!;

    public override void _Ready()
    {
        CustomMinimumSize = Size2;
        MouseFilter = MouseFilterEnum.Pass;
        // **線は札の下、語は札の上。** 1 枚にまとめると、線が名前を隠すか語が札に隠れるかの
        // どちらかになる（作っている最中に両方踏んだ）。
        _lines = AddLayer(words: false);
        _words = AddLayer(words: true);

        LinkLayer AddLayer(bool words)
        {
            var layer = new LinkLayer { MouseFilter = MouseFilterEnum.Ignore, DrawWords = words };
            layer.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(layer);
            return layer;
        }
    }

    private static Vector2 PosOf(int slot)
    {
        foreach ((int s, int col, int row) in Layout)
            if (s == slot) return new Vector2(col * ColStep, row * RowStep);
        return Vector2.Zero;
    }

    private static Rect2 RectOf(int slot) => new(PosOf(slot), new Vector2(CardW, CardH));

    private static Vector2 CenterOf(int slot) => RectOf(slot).GetCenter();

    /// <summary>
    /// 図を引き直す。<paramref name="cells"/> は 5 席ぶん（空席は <c>Def</c> が null）。
    /// <b>選んでいる席は呼び出し側から渡す</b>——引き直しで選択が飛ばないようにするため。
    /// </summary>
    public void Render(IReadOnlyList<Cell> cells, Color accent, int selected)
    {
        Selected = selected;
        _cells.Clear();
        foreach (Cell c in cells) _cells[c.Slot] = c;

        foreach ((int slot, int _, int _) in Layout)
        {
            if (!_cards.TryGetValue(slot, out Button? card))
            {
                int captured = slot;
                card = new Button
                {
                    Position = PosOf(slot),
                    Size = new Vector2(CardW, CardH),
                    CustomMinimumSize = new Vector2(CardW, CardH),
                    Alignment = HorizontalAlignment.Left,
                    FocusMode = FocusModeEnum.None,
                    ClipText = true,
                };
                card.AddThemeFontSizeOverride("font_size", 11);
                card.Pressed += () => SlotSelected?.Invoke(Selected == captured ? -1 : captured);
                AddChild(card);
                var back = new ColorRect
                {
                    Color = new Color(0.10f, 0.15f, 0.13f, 0.95f),
                    Position = new Vector2(6, CardH - 8),
                    Size = new Vector2(CardW - 12, 4),
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                card.AddChild(back);
                _barBacks[slot] = back;
                var fill = new ColorRect
                {
                    Color = UiKit.Heal,
                    Position = new Vector2(6, CardH - 8),
                    Size = new Vector2(CardW - 12, 4),
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                card.AddChild(fill);
                _bars[slot] = fill;
                // 語の層は常に最前面に置き直す（札を足すたびに順番が後ろへ回るため）。
                MoveChild(_words, GetChildCount() - 1);
                _cards[slot] = card;
            }

            Cell cell = _cells.TryGetValue(slot, out Cell c2) ? c2 : new Cell(slot, null, 0, 0, false);
            string seat = FormationRules.SeatNames[slot];
            if (cell.Def is null)
            {
                card.Text = $"{seat}\n（空席）";
                card.AddThemeColorOverride("font_color", UiKit.Faint);
                card.Modulate = new Color(1, 1, 1, 0.45f);
                card.Disabled = true;
                _bars[slot].Visible = false;
                _barBacks[slot].Visible = false;
                continue;
            }
            card.Disabled = false;
            int pct = cell.MaxHp <= 0 ? 0 : cell.Hp * 100 / cell.MaxHp;
            // 盤面ルールの保持者には ★（戦闘中の駒の上の札と**同じ語**）。
            // **3 行目に独立して置く**——2 行目の末尾に足すと `ClipText` で真っ先に切れる。
            string tag = BoardRuleTags.LabelFor(cell.Def.Traits);
            string head = (slot == Selected ? "▶ " : "") + seat;
            card.Text = cell.Alive
                ? $"{head}  {cell.Def.Name}\nHP {cell.Hp}/{cell.MaxHp}  攻{cell.Def.Attack} 速{cell.Def.Speed}"
                  + (tag.Length > 0 ? "\n" + tag : "")
                : $"{head}  {cell.Def.Name}\n（戦死）";
            card.AddThemeColorOverride("font_color",
                !cell.Alive ? UiKit.Faint
                : pct <= 30 ? UiKit.Hurt
                : pct <= 60 ? UiKit.Gold
                : UiKit.Ink);
            card.Modulate = slot == Selected ? new Color(1.0f, 0.90f, 0.62f)
                          : cell.Alive ? Colors.White : new Color(1, 1, 1, 0.55f);
            ColorRect bar = _bars[slot];
            bar.Visible = true;
            _barBacks[slot].Visible = true;
            bar.Size = new Vector2((CardW - 12) * Math.Clamp(pct / 100f, 0f, 1f), 4);
            bar.Color = !cell.Alive ? UiKit.Faint
                      : pct <= 30 ? UiKit.Hurt
                      : pct <= 60 ? UiKit.Gold
                      : UiKit.Heal;
        }

        // 線は「いま立っている駒」だけから引く（倒れた駒の関係は描かない）。
        var seats = _cells.Values
            .Where(c => c.Def is not null && c.Alive)
            .ToDictionary(c => c.Slot, c => c.Def!);
        var links = Map11Relations.Of(seats);
        _lines.Set(links, Selected, accent);
        _words.Set(links, Selected, accent);
    }

    /// <summary>
    /// 関係の線（<see cref="DrawWords"/> が偽）と語（真）。
    /// <b>常時は薄く、選んだ札に関わる線だけ濃く出して語を添える</b>
    /// ——常時ぜんぶ描くと読めない（指示書 §1-2 の最後）。
    /// </summary>
    private sealed partial class LinkLayer : Control
    {
        /// <summary>線に添える語は日本語なので、<b>フォールバックではなくシステムフォント</b>で描く。</summary>
        private static readonly SystemFont LabelFont = new()
        {
            FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" },
            AllowSystemFallback = true,
        };

        public bool DrawWords { get; init; }

        private IReadOnlyList<Map11Relations.Link> _links = Array.Empty<Map11Relations.Link>();
        private int _selected = -1;
        private Color _accent = UiKit.Player;

        public void Set(IReadOnlyList<Map11Relations.Link> links, int selected, Color accent)
        {
            _links = links;
            _selected = selected;
            _accent = accent;
            QueueRedraw();
        }

        public override void _Draw()
        {
            // 同じ2席のあいだに何本も走るので、語が重ならないよう線上で位置をずらす。
            var lane = new Dictionary<(int, int), int>();
            foreach (Map11Relations.Link l in _links)
            {
                bool hot = _selected >= 0 && (l.From == _selected || l.To == _selected);
                if (_selected >= 0 && !hot) continue;   // 選んでいる間は関係ない線を消す
                var key = (Math.Min(l.From, l.To), Math.Max(l.From, l.To));
                int n = lane.GetValueOrDefault(key);
                lane[key] = n + 1;

                (Vector2 a, Vector2 b) = Clip(l.From, l.To);
                Color c = hot ? _accent : new Color(_accent, 0.20f);
                if (!DrawWords)
                {
                    DrawLine(a, b, c, hot ? 2.4f : 1.2f, true);
                    DrawCircle(b, hot ? 4.5f : 2.5f, c);
                    continue;
                }
                if (!hot) continue;
                // 0 本目は中央、以降は手前・奥へ交互に寄せる。
                float t = 0.5f + (n % 2 == 0 ? 1 : -1) * ((n + 1) / 2) * 0.24f;
                Vector2 mid = a.Lerp(b, Math.Clamp(t, 0.12f, 0.88f));
                Vector2 size = LabelFont.GetStringSize(l.Word, HorizontalAlignment.Left, -1, 10);
                // **札の隙間に入らない語は出さない。** 同じ列の2枚（前1↔前3・後1↔後3）は
                // 隙間が数十 px しかないので、無理に置くと札の上に乗って名前を隠す
                // ——関係そのものは下の欄に文で出ているので、線と矢印だけ残す。
                if (size.X + 10 > a.DistanceTo(b)) continue;
                Vector2 at = mid - new Vector2(size.X / 2f, -4);
                DrawRect(new Rect2(at + new Vector2(-3, -12), size + new Vector2(6, 5)),
                         new Color(0.02f, 0.05f, 0.04f, 0.90f));
                DrawString(LabelFont, at, l.Word, HorizontalAlignment.Left, -1, 10, UiKit.Gold);
            }
        }

        /// <summary>線の両端を、札の四角の外へ切り詰める（札の上に線を重ねない）。</summary>
        private static (Vector2, Vector2) Clip(int from, int to)
        {
            Vector2 a = CenterOf(from), b = CenterOf(to);
            return (Exit(RectOf(from), a, b), Exit(RectOf(to), b, a));
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
}
