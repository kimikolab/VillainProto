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

    /// <summary>
    /// 札を押したときに呼ばれる（席番号）。<b>第172期に「押された席をそのまま渡す」へ変えた</b>
    /// ——選択の入り切りは呼び出し側が決める（組み直しでは「2 枚目を押す」が入れ替えになるので、
    /// ここで -1 に潰すと 2 枚目が拾えない）。
    /// </summary>
    public Action<int>? SlotSelected;

    /// <summary>空席も押せるか（組み直しのとき＝控えの駒をそこへ入れられる）。</summary>
    public bool AllowEmptyPress { get; set; }

    /// <summary>
    /// 倒れた駒の札に出す語（第173期 §1-3 #3）。既定は「（戦死）」。
    /// <b>全滅した隊では「（失われた）」に替える</b>——第172期は隊が全滅すると <c>Units</c> が
    /// null になるので、図が<b>満タンの既定編成</b>を描いていた（失われた隊が、まだ出していない隊と
    /// 同じ絵になる）。
    /// </summary>
    public string DeadLabel { get; set; } = "（戦死）";

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

    /// <summary>
    /// 図を引き直す。<paramref name="cells"/> は 5 席ぶん（空席は <c>Def</c> が null）。
    /// <b>選んでいる席は呼び出し側から渡す</b>——引き直しで選択が飛ばないようにするため。
    /// </summary>
    public void Render(IReadOnlyList<Cell> cells, int selected)
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
                card.Pressed += () => SlotSelected?.Invoke(captured);
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
                card.Text = (slot == Selected ? "▶ " : "") + $"{seat}\n（空席）";
                card.AddThemeColorOverride("font_color", UiKit.Faint);
                card.Modulate = slot == Selected ? new Color(1.0f, 0.90f, 0.62f)
                                                 : new Color(1, 1, 1, 0.45f);
                card.Disabled = !AllowEmptyPress;
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
                : $"{head}  {cell.Def.Name}\n{DeadLabel}";
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
        _lines.Set(links, Selected);
        _words.Set(links, Selected);
    }

    /// <summary>
    /// 関係の線（<see cref="DrawWords"/> が偽）と語（真）。
    /// <b>色は 得／損／両方</b>（第173期・`SeatLinks.ColorOf`）。
    /// <b>常時は薄く、選んだ札に関わる線だけ濃く出して語を添える</b>
    /// ——常時ぜんぶ描くと読めない（指示書 §1-2 の最後）。
    ///
    /// <para><b>第172期に描画そのものを <see cref="SeatLinks"/> へ出した</b>——編成画面
    /// （`BattlefieldView`）も同じ線を引くようになったため。**ここに写しは残っていない。**</para>
    /// </summary>
    private sealed partial class LinkLayer : Control
    {
        public bool DrawWords { get; init; }

        private IReadOnlyList<Map11Relations.Link> _links = Array.Empty<Map11Relations.Link>();
        private int _selected = -1;

        public void Set(IReadOnlyList<Map11Relations.Link> links, int selected)
        {
            _links = links;
            _selected = selected;
            QueueRedraw();
        }

        public override void _Draw()
            => SeatLinks.Draw(this, _links, _selected, RectOf, DrawWords);
    }
}
